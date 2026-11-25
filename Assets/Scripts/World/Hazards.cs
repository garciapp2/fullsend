using System.Collections.Generic;
using UnityEngine;

//  - Laser.cs / AgencyLaser: a beam is a fat trigger capsule (radius 3) whose length is a terrain raycast; touching it
//    costs 51 health with a 5 s cooldown (TriggerEffect). Our beams use the same numbers (scaled by the level).
//  - LaserSpinner: a tower with laser heads that rotate 20 deg/s, so the beams sweep the ground around it.
//  - LaserRing: a ring standing across the road, lasers rotating inside it around the forward axis: ride through a gap.
//  - Laser_Flying (MovingObject + SpringFollow): a drone sweeping side to side, firing straight down.
//  - DesertSpike / IceSpike: spikes bursting out of the ground a beat after you come close.
// Shapes are tested against the player's body swept over each physics step, so nothing is skipped at 100 m/s.
// Several components share this file on purpose: they are only ever added at runtime (AddComponent), never saved in
// a scene. A component saved in a scene must live in its own file named after the class, or the player build breaks.
public abstract class Hazard : MonoBehaviour
{
    public static readonly List<Hazard> all = new List<Hazard>();
    [HideInInspector] public Vector3 center;
    [HideInInspector] public float reach = 5f;

    protected virtual void OnEnable() => all.Add(this);
    protected virtual void OnDisable() => all.Remove(this);

    // a..b: the player's body centre (0.9 m above the feet) swept over this physics step
    public abstract bool Test(Vector3 a, Vector3 b, out Vector3 normal);
    public abstract void Hit(PlayerCharacter pc, Vector3 normal);

    public static float Intensity => RunManager.instance ? RunManager.instance.Intensity : 1f;

    // optionally with a push and a speed cut.
    public static void Hurt(PlayerCharacter pc, float damage, string text, Vector3 push, float slow = 1f)
    {
        if (pc.Invulnerable()) return;
        if (TrickSystem.instance) TrickSystem.instance.Bail(text, 0f, false);
        if (slow < 1f) pc.SlowDown(slow);
        if (push != Vector3.zero) pc.rig.linearVelocity += push;
        pc.TakeDamage(damage, pc.transform.position);
        ComicFX.Impact(0.9f, 0.07f, new Color(1f, 0.25f, 0.2f), 0.3f);
        if (Sfx.instance) Sfx.Play(Sfx.instance.wallHit, 1f, 1f);
    }

    // closest distance between segments p1q1 and p2q2
    public static float SegSeg(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
    {
        Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
        float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
        float s, t;
        if (a < 1e-6f && e < 1e-6f) return r.magnitude;
        if (a < 1e-6f) { s = 0f; t = Mathf.Clamp01(f / e); }
        else
        {
            float c = Vector3.Dot(d1, r);
            if (e < 1e-6f) { t = 0f; s = Mathf.Clamp01(-c / a); }
            else
            {
                float b = Vector3.Dot(d1, d2), den = a * e - b * b;
                s = den > 1e-6f ? Mathf.Clamp01((b * f - c * e) / den) : 0f;
                t = (b * s + f) / e;
                if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
            }
        }
        return ((p1 + d1 * s) - (p2 + d2 * t)).magnitude;
    }

    // the swept point closest (in XZ) to a vertical axis through c
    protected static Vector3 ClosestToAxis(Vector3 a, Vector3 b, Vector3 c)
    {
        var ab = new Vector2(b.x - a.x, b.z - a.z);
        float l = ab.sqrMagnitude;
        float t = l > 1e-6f ? Mathf.Clamp01(Vector2.Dot(new Vector2(c.x - a.x, c.z - a.z), ab) / l) : 1f;
        return Vector3.Lerp(a, b, t);
    }

    static readonly Dictionary<Color, Material> mats = new Dictionary<Color, Material>();
    public static Material Mat(Color c, Color emission)
    {
        if (mats.TryGetValue(c, out var m) && m) return m;
        m = new Material(Track.instance.railMat) { name = "perigo" };
        m.SetFloat("_PaletteSlot", -1f); m.SetFloat("_TexStrength", 0f);
        m.SetColor("_BaseColor", c); m.SetColor("_Emission", emission);
        mats[c] = m;
        return m;
    }

    public static Material LaserMat => Mat(new Color(1f, 0.12f, 0.22f), new Color(1f, 0.15f, 0.25f));
    public static Material MetalMat => Mat(new Color(0.16f, 0.15f, 0.22f), Color.black);

    public static GameObject Prim(PrimitiveType t, Transform parent, Material m)
    {
        var go = GameObject.CreatePrimitive(t); Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        return go;
    }

    protected static void Solid(GameObject go)
    {
        go.layer = PlayerCharacter.PropLayer;
        var info = go.AddComponent<PropInfo>(); info.kind = PropInfo.Kind.Solid;
    }
}

// Shared by every big laser: a set of beams (origin + direction, length from a terrain raycast), 51 damage, 5 s cooldown.
// Each beam is layered for the comic look: an opaque white-hot core (gets the ink outline), an additive glow with
// energy stripes running along it, a wide faint halo, and where it meets the ground a pulsing flare with sparks.
public abstract class BigLaser : Hazard
{
    public const float Radius = 3f, Damage = 51f, Cooldown = 5f;

    protected class Beam
    {
        public Transform core, glow, halo, flare; public ParticleSystem sparks;
        public readonly List<Transform> rings = new List<Transform>();
        public MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        public Vector3 a, b; public bool grounded; public float len = -1f;
        public bool on = true;
        public float radius = 3f, width = 1f, lastHit = -10f;
    }

    // switch a beam off (hidden, harmless) or back on: timed lasers (mining laser) blink their beams
    protected void ShowBeam(int k, bool show)
    {
        var bm = beamList[k];
        if (bm.on == show) return;
        bm.on = show;
        foreach (var t in new[] { bm.core, bm.glow, bm.halo, bm.flare }) t.gameObject.SetActive(show);
        foreach (var rg in bm.rings) rg.gameObject.SetActive(show);
        if (!show && bm.sparks.isPlaying) bm.sparks.Stop();
    }
    protected readonly List<Beam> beamList = new List<Beam>();
    float lastHit = -10f, nearCd;
    AudioSource hum;

    static Material beamMat, flareMat;
    static AudioClip humClip, hitClip;
    public static Material BeamMat { get { Load(); return beamMat; } }
    public static Material FlareMat { get { Load(); return flareMat; } }
    public static Material CoreMat => Mat(new Color(1f, 0.82f, 0.9f), new Color(1f, 0.35f, 0.55f));

    static void Load()
    {
        if (beamMat) return;
        var src = Resources.Load<Material>("BeamMat");   // in Resources so the shader ships in the build
        beamMat = new Material(src);
        flareMat = new Material(src); flareMat.SetFloat("_Radial", 1f);
        humClip = Resources.Load<AudioClip>("Sfx/Au_Laser_Warm_Loop");
        hitClip = Resources.Load<AudioClip>("Sfx/Au_LaserHit1");
    }

    protected Beam AddBeam(Transform parent, float radius = Radius)
    {
        var bm = new Beam { radius = radius };
        bm.core = Prim(PrimitiveType.Cylinder, parent, CoreMat).transform;
        bm.glow = Prim(PrimitiveType.Cylinder, parent, BeamMat).transform;
        bm.halo = Prim(PrimitiveType.Cylinder, parent, BeamMat).transform;
        bm.flare = Prim(PrimitiveType.Quad, parent, FlareMat).transform;
        foreach (var t in new[] { bm.core, bm.glow, bm.halo, bm.flare })
            t.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        bm.sparks = Sparks(parent);
        // energy rings racing down the beam from the emitter to the ground
        for (int k = 0; k < 2; k++)
        {
            var rg = new GameObject("pulso"); rg.transform.SetParent(parent, false);
            rg.AddComponent<MeshFilter>().sharedMesh = Track.TorusMesh(5f, 0.45f);
            var mr = rg.AddComponent<MeshRenderer>(); mr.sharedMaterial = BeamMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var rp = new MaterialPropertyBlock(); rp.SetFloat("_Intensity", 2.2f); rp.SetFloat("_Hot", 1f); rp.SetFloat("_Length", 1f); rp.SetFloat("_Opacity", 0.9f);
            mr.SetPropertyBlock(rp);
            bm.rings.Add(rg.transform);
        }
        beamList.Add(bm);
        if (!hum)
        {
            // the hum lives on its own child: it slides along the nearest beam, the laser itself never moves
            var hg = new GameObject("zumbido"); hg.transform.SetParent(transform, false);
            hum = hg.AddComponent<AudioSource>();
            hum.clip = humClip; hum.loop = true; hum.spatialBlend = 1f; hum.rolloffMode = AudioRolloffMode.Linear;
            hum.minDistance = 12f; hum.maxDistance = 200f; hum.volume = 0.85f; hum.dopplerLevel = 0f;
            if (humClip) hum.Play();
        }
        return bm;
    }

    // a soft additive glow ball (emitters, drone eyes)
    public static Transform GlowBall(Transform parent, Vector3 localPos, float size, float intensity)
    {
        var g = Prim(PrimitiveType.Sphere, parent, BeamMat).transform;
        g.localPosition = localPos; g.localScale = Vector3.one * size;
        var mpb = new MaterialPropertyBlock(); mpb.SetFloat("_Intensity", intensity); mpb.SetFloat("_Hot", 1.5f); mpb.SetFloat("_Length", 1f);
        g.GetComponent<MeshRenderer>().SetPropertyBlock(mpb);
        return g;
    }

    static ParticleSystem Sparks(Transform parent)
    {
        var go = new GameObject("faiscas"); go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(18f, 45f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
        main.startColor = new Color(1f, 0.75f, 0.8f);
        main.gravityModifier = 3f; main.maxParticles = 250;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 25f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 50f; sh.radius = 2f;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = FlareMat; r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = 1.5f;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    // aim beam k from o along dir until it meets the terrain. Where there is no terrain (over a gap, past the edge of
    // the world) it stops at the height of the ground under the laser instead of shooting off into the void, and the
    // length slides toward its target so a beam crossing a crest never pops between short and long.
    protected void Aim(int k, Vector3 o, Vector3 dir, float maxLen, float groundY)
    {
        var bm = beamList[k];
        float target; bool hit = Physics.Raycast(o, dir, out var h, maxLen, 1 << PlayerCharacter.TerrainLayer, QueryTriggerInteraction.Ignore);
        if (hit) target = h.distance;
        else target = dir.y < -0.05f ? Mathf.Min(maxLen, (o.y - groundY) / -dir.y) : maxLen;
        bm.len = bm.len < 0f ? target : Mathf.MoveTowards(bm.len, target, Time.deltaTime * Mathf.Max(60f, Mathf.Abs(target - bm.len) * 8f));
        bm.width = Mathf.Lerp(bm.width, hit ? 1f : 0.25f, Time.deltaTime * 2f);
        Span(k, o, o + dir * bm.len, hit);
    }

    protected void Span(int k, Vector3 a, Vector3 b, bool grounded = false)
    {
        var bm = beamList[k];
        bm.a = a; bm.b = b; bm.grounded = grounded;
        var d = b - a; float len = d.magnitude;
        var rot = Quaternion.FromToRotation(Vector3.up, d / Mathf.Max(0.001f, len));
        var mid = (a + b) * 0.5f;
        float t = Time.time;
        float pulse = 1f + Mathf.Sin(t * 7f + k * 1.7f) * 0.05f;
        float R = bm.radius * bm.width;
        Place(bm.core, mid, rot, R * 0.38f * pulse, len);
        Place(bm.glow, mid, rot, R * 2f * pulse, len);
        Place(bm.halo, mid, rot, R * 3.6f, len);
        bm.mpb.SetFloat("_Length", len); bm.mpb.SetFloat("_Intensity", 2.8f); bm.mpb.SetFloat("_Hot", 0.9f); bm.mpb.SetFloat("_Opacity", 1f);
        bm.glow.GetComponent<MeshRenderer>().SetPropertyBlock(bm.mpb);
        bm.mpb.SetFloat("_Intensity", 0.9f); bm.mpb.SetFloat("_Hot", 0f); bm.mpb.SetFloat("_Opacity", 0.5f);
        bm.halo.GetComponent<MeshRenderer>().SetPropertyBlock(bm.mpb);
        // rings: 4 evenly spaced, travelling at 90 m/s, growing a little as they go
        var dirN = d / Mathf.Max(0.001f, len);
        for (int r = 0; r < bm.rings.Count; r++)
        {
            float f = Mathf.Repeat(t * 90f / Mathf.Max(20f, len) + r / (float)bm.rings.Count, 1f);
            var rt = bm.rings[r];
            rt.position = a + d * f;
            rt.rotation = Quaternion.LookRotation(dirN);
            float s = R * (1.25f + f * 0.5f) / 5f;
            rt.localScale = new Vector3(s, s, 3f);
        }
        if (!bm.on) return;
        bm.flare.gameObject.SetActive(grounded);
        if (grounded)
        {
            bm.flare.position = b + Vector3.up * 1.5f;
            bm.flare.localScale = Vector3.one * R * (5f + Mathf.Sin(t * 6f + k) * 0.8f);
            bm.sparks.transform.position = b;
            bm.sparks.transform.rotation = Quaternion.LookRotation(Vector3.up);
            if (!bm.sparks.isPlaying) bm.sparks.Play();
        }
        else if (bm.sparks.isPlaying) bm.sparks.Stop();
    }

    static void Place(Transform t, Vector3 mid, Quaternion rot, float width, float len)
    {
        t.SetPositionAndRotation(mid, rot);
        t.localScale = new Vector3(width, len * 0.5f, width);
    }

    protected virtual void LateUpdate()
    {
        var cam = Camera.main; var pc = RunManager.instance ? RunManager.instance.player : null;
        if (cam) foreach (var bm in beamList) if (bm.flare.gameObject.activeSelf) bm.flare.rotation = Quaternion.LookRotation(bm.flare.position - cam.transform.position);
        if (!pc) return;
        // the hum slides along the nearest beam; close calls split the colours and rattle the camera
        var p = pc.transform.position + Vector3.up * 0.9f;
        float best = float.MaxValue; var bestPt = transform.position;
        foreach (var bm in beamList)
        {
            var ab = bm.b - bm.a; float tt = Mathf.Clamp01(Vector3.Dot(p - bm.a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
            var q = bm.a + ab * tt; float dd = (p - q).magnitude;
            if (dd < best) { best = dd; bestPt = q; }
        }
        if (hum) hum.transform.position = bestPt;
        float surf = best - Radius;
        if (RunManager.instance.state == RunManager.State.Run)
        {
            if (surf < 25f) ComicFX.Split(Mathf.InverseLerp(25f, 2f, surf) * 1.1f);
            nearCd -= Time.deltaTime;
            if (surf < 6f && nearCd <= 0f) { GameFeel.Shake(4f, 0.25f); nearCd = 0.6f; }
        }
    }

    public override bool Test(Vector3 a, Vector3 b, out Vector3 normal)
    {
        normal = Vector3.up;
        foreach (var bm in beamList)
            if (bm.on && Time.time >= bm.lastHit + Cooldown && SegSeg(a, b, bm.a, bm.b) < bm.radius * bm.width + 0.5f) { hitBeam = bm; return true; }
        return false;
    }

    Beam hitBeam;
    public override void Hit(PlayerCharacter pc, Vector3 normal)
    {
        if (pc.Invulnerable()) return;
        lastHit = Time.time; if (hitBeam != null) hitBeam.lastHit = Time.time;
        if (hitClip) Sfx.Play(hitClip, 1f, 1f);
        Hurt(pc, Damage, "LASER!", Vector3.zero, 0.85f);
    }
}

// LaserSpinner: a tower, heads rotating 20 deg/s, beams angled down sweeping the ground around it.
public class LaserSpinner : BigLaser
{
    Transform head; float angle, tilt, height; int count;

    public static LaserSpinner Build(Transform parent, Vector3 ground, float height, int count, float phase)
    {
        var go = new GameObject("Torre de laser"); go.transform.SetParent(parent, false);
        go.transform.position = ground;
        var s = go.AddComponent<LaserSpinner>();
        s.height = height; s.count = count; s.angle = phase; s.tilt = 40f;
        var tower = Prim(PrimitiveType.Cylinder, go.transform, MetalMat);
        tower.transform.localPosition = Vector3.up * (height * 0.5f - 2f); tower.transform.localScale = new Vector3(5f, height * 0.5f + 2f, 5f);
        tower.AddComponent<CapsuleCollider>(); Solid(tower);
        s.head = Prim(PrimitiveType.Sphere, go.transform, CoreMat).transform;
        s.head.localPosition = Vector3.up * height; s.head.localScale = Vector3.one * 6f;
        GlowBall(go.transform, Vector3.up * height, 16f, 1.4f);
        var collar = Prim(PrimitiveType.Cylinder, go.transform, MetalMat).transform;
        collar.localPosition = Vector3.up * (height - 4f); collar.localScale = new Vector3(12f, 1f, 12f);
        for (int k = 0; k < count; k++) s.AddBeam(go.transform);
        s.center = ground + Vector3.up * height * 0.5f;
        s.reach = height / Mathf.Tan(s.tilt * Mathf.Deg2Rad) + 20f;
        s.Update();
        return s;
    }

    void Update()
    {
        angle += 20f * Time.deltaTime;
        var o = head.position;
        for (int k = 0; k < count; k++)
        {
            var dir = Quaternion.Euler(tilt, angle + k * 360f / count, 0f) * Vector3.forward;
            Aim(k, o, dir, 160f, transform.position.y);
        }
    }
}

// LaserRing: a ring across the road, lasers spinning inside it around the forward axis. Ride through a gap.
public class LaserRing : BigLaser
{
    Vector3 fwd; float radius, angle, speed; int count;

    public static LaserRing Build(Transform parent, Vector3 roadCenter, Vector3 forward, float radius, int count, float speed, float phase)
    {
        var go = new GameObject("Anel de laser"); go.transform.SetParent(parent, false);
        var s = go.AddComponent<LaserRing>();
        s.fwd = forward.normalized; s.radius = radius; s.count = count; s.speed = speed; s.angle = phase;
        var c = roadCenter + Vector3.up * radius * 0.82f;
        go.transform.position = c; go.transform.rotation = Quaternion.LookRotation(s.fwd, Vector3.up);
        var rg = new GameObject("aro"); rg.transform.SetParent(go.transform, false);
        rg.AddComponent<MeshFilter>().sharedMesh = Track.TorusMesh(5f, 0.45f);
        rg.AddComponent<MeshRenderer>().sharedMaterial = MetalMat;
        rg.transform.localScale = new Vector3(radius / 5f, radius / 5f, 6f);
        var mc = rg.AddComponent<MeshCollider>(); mc.sharedMesh = Track.TorusMesh(5f, 0.45f);
        rg.layer = PlayerCharacter.PropLayer; rg.AddComponent<PropInfo>().kind = PropInfo.Kind.Runnable;
        for (int k = 0; k < count; k++) s.AddBeam(go.transform);
        s.center = c; s.reach = radius + 8f;
        s.Update();
        return s;
    }

    void Update()
    {
        angle += speed * Time.deltaTime;
        var c = transform.position;
        var right = Vector3.Cross(Vector3.up, fwd).normalized;
        for (int k = 0; k < count; k++)
        {
            float a = (angle + k * 180f / count) * Mathf.Deg2Rad;
            var d = right * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a);
            Span(k, c - d * radius * 0.95f, c + d * radius * 0.95f);
        }
    }
}

// Laser_Flying: a drone high above the road sweeping side to side, its beam straight down.
public class LaserDrone : BigLaser
{
    Transform body; Vector3 basePos, side; float span, period, t0, groundY;

    public static LaserDrone Build(Transform parent, Vector3 roadCenter, Vector3 right, float height, float span, float period, float phase)
    {
        var go = new GameObject("Drone de laser"); go.transform.SetParent(parent, false);
        var s = go.AddComponent<LaserDrone>();
        s.basePos = roadCenter + Vector3.up * height; s.side = right.normalized; s.span = span; s.period = period; s.t0 = phase;
        s.groundY = roadCenter.y;
        s.body = new GameObject("corpo").transform; s.body.SetParent(go.transform, false);
        var hull = Prim(PrimitiveType.Cube, s.body, MetalMat).transform; hull.localScale = new Vector3(14f, 4f, 9f);
        var eye = Prim(PrimitiveType.Sphere, s.body, CoreMat).transform; eye.localPosition = Vector3.down * 2.5f; eye.localScale = Vector3.one * 4f;
        GlowBall(s.body, Vector3.down * 2.5f, 11f, 1.4f);
        foreach (float x in new[] { -9f, 9f })
        {
            var w = Prim(PrimitiveType.Cylinder, s.body, MetalMat).transform;
            w.localPosition = new Vector3(x, 1.5f, 0f); w.localScale = new Vector3(8f, 0.4f, 8f);
        }
        s.AddBeam(go.transform);
        s.center = roadCenter + Vector3.up * height * 0.5f; s.reach = span + height;
        s.Update();
        return s;
    }

    void Update()
    {
        // MovingObject ping-pong with a SpringFollow feel: a sine sweep across the road
        float ph = (Time.time + t0) / period * Mathf.PI * 2f;
        body.position = basePos + side * (Mathf.Sin(ph) * span);
        // face along the road and bank into the sweep (no flipping when it turns around)
        float vel = Mathf.Cos(ph);
        var fwd = Vector3.Cross(side, Vector3.up);
        body.rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(0f, 0f, -vel * 18f);
        Aim(0, body.position + Vector3.down * 3f, Vector3.down, 200f, groundY);
    }
}

public class SpikeTrap : Hazard
{
    float radius, delay, triggerDist = 60f, spikeH = 3.4f;
    float armedAt = -1f, rise;
    Transform spikes; MeshRenderer ringR;
    Material warnMat, hotMat;
    static Mesh cone;

    public static SpikeTrap Build(Transform parent, Vector3 pos, Vector3 up, float radius, float delay, Material spikeMat)
    {
        var go = new GameObject("Espinhos"); go.transform.SetParent(parent, false);
        go.transform.position = pos; go.transform.rotation = Quaternion.FromToRotation(Vector3.up, up);
        var s = go.AddComponent<SpikeTrap>();
        s.radius = radius; s.delay = delay; s.center = pos + Vector3.up * 1.5f; s.reach = radius + 2f;
        s.warnMat = Mat(new Color(1f, 0.75f, 0.15f), new Color(0.6f, 0.4f, 0f));
        s.hotMat = Mat(new Color(1f, 0.15f, 0.2f), new Color(0.9f, 0.1f, 0.1f));
        var ringGo = new GameObject("aviso"); ringGo.transform.SetParent(go.transform, false);
        ringGo.AddComponent<MeshFilter>().sharedMesh = Track.TorusMesh(5f, 0.45f);
        s.ringR = ringGo.AddComponent<MeshRenderer>(); s.ringR.sharedMaterial = s.warnMat;
        s.ringR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ringGo.transform.localPosition = Vector3.up * 0.15f; ringGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ringGo.transform.localScale = new Vector3(radius / 5f, radius / 5f, 0.4f);
        s.spikes = new GameObject("pontas").transform; s.spikes.SetParent(go.transform, false);
        int n = Mathf.RoundToInt(radius * radius * 0.9f) + 3;
        var rnd = new System.Random(pos.GetHashCode());
        for (int k = 0; k < n; k++)
        {
            float ang = k * 2.39996f, d = Mathf.Sqrt((k + 0.5f) / n) * radius * 0.85f;
            var sp = new GameObject("ponta"); sp.transform.SetParent(s.spikes, false);
            sp.AddComponent<MeshFilter>().sharedMesh = Cone();
            sp.AddComponent<MeshRenderer>().sharedMaterial = spikeMat;
            sp.transform.localPosition = new Vector3(Mathf.Cos(ang) * d, -0.2f, Mathf.Sin(ang) * d);
            sp.transform.localRotation = Quaternion.Euler((float)rnd.NextDouble() * 16f - 8f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 16f - 8f);
            float sc = 0.8f + (float)rnd.NextDouble() * 0.5f;
            sp.transform.localScale = new Vector3(sc, sc * (s.spikeH / 3f), sc);
        }
        s.spikes.localScale = new Vector3(1f, 0.12f, 1f);   // tips poke out so you can see them coming
        return s;
    }

    static Mesh Cone()
    {
        if (cone) return cone;
        const int sides = 7; var v = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i < sides; i++)
        {
            float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
            int b = v.Count;
            v.Add(new Vector3(Mathf.Cos(a0) * 0.75f, 0f, Mathf.Sin(a0) * 0.75f));
            v.Add(new Vector3(0f, 3f, 0f));
            v.Add(new Vector3(Mathf.Cos(a1) * 0.75f, 0f, Mathf.Sin(a1) * 0.75f));
            t.Add(b); t.Add(b + 1); t.Add(b + 2);
        }
        cone = new Mesh { name = "espinho" }; cone.SetVertices(v); cone.SetTriangles(t, 0); cone.RecalculateNormals();
        return cone;
    }

    void Update()
    {
        var pc = RunManager.instance ? RunManager.instance.player : null;
        if (!pc) return;
        if (armedAt < 0f)
        {
            var d = pc.transform.position - transform.position; d.y = 0f;
            if (d.magnitude < triggerDist && RunManager.instance.state == RunManager.State.Run) armedAt = Time.time;
            return;
        }
        float t = Time.time - armedAt;
        ringR.sharedMaterial = t < delay && Mathf.Repeat(t * 10f, 1f) < 0.5f ? hotMat : (rise > 0.5f ? hotMat : warnMat);
        if (t > delay && rise < 1f)
        {
            if (rise == 0f && (pc.transform.position - transform.position).magnitude < 50f) GameFeel.Shake(3f, 0.25f);
            rise = Mathf.Min(1f, rise + Time.deltaTime / 0.12f);
            spikes.localScale = new Vector3(1f, Mathf.Lerp(0.12f, 1f, rise), 1f);
        }
    }

    public override bool Test(Vector3 a, Vector3 b, out Vector3 normal)
    {
        normal = Vector3.up;
        if (rise < 0.4f) return false;
        var q = ClosestToAxis(a, b, transform.position);
        var d = new Vector3(q.x - transform.position.x, 0f, q.z - transform.position.z);
        return d.magnitude < radius * 0.9f + 0.4f && q.y - 0.9f < transform.position.y + spikeH * rise;
    }

    public override void Hit(PlayerCharacter pc, Vector3 normal) => Hurt(pc, 30f, "ESPINHOS!", Vector3.up * 12f, 0.8f);
}

// Sweeps the player's body each physics step against every hazard nearby.
public class HazardSensor : MonoBehaviour
{
    PlayerCharacter pc; Vector3 last; bool has;

    void Awake() { pc = GetComponent<PlayerCharacter>(); }

    void FixedUpdate()
    {
        var rm = RunManager.instance;
        var cur = pc.rig.position + Vector3.up * 0.9f;
        if (!rm || rm.state != RunManager.State.Run || pc.dead || !has || (cur - last).sqrMagnitude > 900f)
        {
            last = cur; has = true; return;
        }
        var mid = (last + cur) * 0.5f; float half = (cur - last).magnitude * 0.5f;
        for (int k = Hazard.all.Count - 1; k >= 0; k--)
        {
            var h = Hazard.all[k];
            float rr = h.reach + half + 2f;
            if ((h.center - mid).sqrMagnitude > rr * rr) continue;
            if (h.Test(last, cur, out var n)) { h.Hit(pc, n); break; }
        }
        last = cur;
    }
}
