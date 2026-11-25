using System.Collections.Generic;
using UnityEngine;

// drawn with the game's comic glow. Runtime-only components.

// ------------------------------------------------------------------------------------------------ shots
// for 6 s (then shrinks for 1 s). Barrage bolts: 24x8x8 m box along their flight, 36. Mining gems: 36.
public class StickyShot : Hazard
{
    Vector3 vel, gravityV, axis; float life = -1f, lastHit = -10f, half, rad, damage; bool stuck, bounceProps, bounceSteep;
    Transform look, dash; Material flashMat; Transform flash; float flashT = -1f; string text; Vector3 spinAxis; float spin;

    public static StickyShot Fire(Transform parent, Vector3 from, Vector3 vel, float gravity, Transform look, float halfLen, float radius,
        float damage, string text, bool bounceProps, bool bounceSteep, float spin = 0f)
    {
        var go = new GameObject("tiro"); go.transform.SetParent(parent, false); go.transform.position = from;
        var s = go.AddComponent<StickyShot>();
        s.vel = vel; s.gravityV = Vector3.down * gravity; s.half = halfLen; s.rad = radius; s.damage = damage; s.text = text;
        s.bounceProps = bounceProps; s.bounceSteep = bounceSteep; s.spin = spin; s.spinAxis = Random.onUnitSphere;
        s.look = look; look.SetParent(go.transform, false); look.localPosition = Vector3.zero; look.localRotation = Quaternion.identity;
        s.center = from; s.reach = halfLen + radius + 2f;
        return s;
    }

    public void SetDash(Transform d) { dash = d; d.SetParent(transform, false); }

    void Update()
    {
        float dt = Time.deltaTime;
        if (flashT >= 0f)
        {
            float t = Time.time - flashT;   // Impact.anim: 4.84 -> 19.84 (0.083) -> 27.4 (0.5), fades by 0.517
            float sc = t < 0.083f ? Mathf.Lerp(4.84f, 19.84f, t / 0.083f) : Mathf.Lerp(19.84f, 27.4f, (t - 0.083f) / 0.417f);
            flash.localScale = Vector3.one * sc; flashMat.SetFloat("_Opacity", Mathf.Clamp01(1f - t / 0.517f));
            if (t > 0.52f) { flash.gameObject.SetActive(false); flashT = -1f; }
        }
        if (stuck)
        {
            life -= dt;
            if (life < 1f) look.localScale = Vector3.one * Mathf.Max(0.01f, life);
            if (life <= 0f) Destroy(gameObject);
            return;
        }
        vel += gravityV * dt;
        var prev = transform.position; var step = vel * dt; float len = step.magnitude;
        if (spin != 0f) look.Rotate(spinAxis, spin * dt, Space.World);
        else if (len > 1e-4f) transform.rotation = Quaternion.LookRotation(step / len);
        int mask = PlayerCharacter.RunnableMask;
        if (len > 1e-4f && Physics.SphereCast(prev, 2f, step / len, out var h, len, mask, QueryTriggerInteraction.Ignore))
        {
            bool terrain = h.collider.gameObject.layer == PlayerCharacter.TerrainLayer;
            bool steep = Vector3.Angle(h.normal, Vector3.up) > 60f;
            if ((!terrain && bounceProps) || (steep && bounceSteep)) { vel = Vector3.Reflect(vel, h.normal) * 0.6f; return; }
            transform.position = h.point + h.normal * 0.5f; stuck = true; life = 6f + 1f;
            axis = len > 1e-4f ? step / len : Vector3.forward; center = transform.position;
            if (dash) dash.gameObject.SetActive(false);
            flashMat = Obst.Glow(Obst.Orange, 2.4f, 1f, 1f, true);
            flash = BigLaser.Prim(PrimitiveType.Sphere, transform, flashMat).transform; flash.localPosition = Vector3.zero; flashT = Time.time;
            return;
        }
        transform.position = prev + step; center = transform.position;
        axis = len > 1e-4f ? step / len : axis;
        if (transform.position.y < -2000f) Destroy(gameObject);
    }

    public override bool Test(Vector3 a, Vector3 b, out Vector3 normal)
    {
        normal = Vector3.up;
        if (Time.time < lastHit + 0.5f) return false;
        var c = transform.position;
        float d = half > 0f ? Hazard.SegSeg(a, b, c - axis * half, c + axis * half) : Obst.SegPointDist(a, b, c);
        return d < rad;
    }

    public override void Hit(PlayerCharacter pc, Vector3 normal)
    {
        if (pc.Invulnerable()) return;
        lastHit = Time.time;
        Hurt(pc, damage, text, Vector3.zero, 1f);
    }
}

// ------------------------------------------------------------------------------------------------ missile tower
// it launches every 0.5 s (Super: 0.3 s). Each missile leaves the muzzle straight up at 3500 m/s and homes at 700 m/s onto
// where you'll be (you + 50 m random + your step x 90, on the ground), the aim fixed after 0.3 s; a thin red aim line runs
// ahead of it with a red dot where it will land. The explosion swells to 53 m across; 51 while it's live (1.25 s).
public class MissileTowerAI : MonoBehaviour
{
    Transform muzzle; Vector3 triggerPoint; float rate, next; bool engaged, used;

    public static void Setup(GameObject go, bool super)
    {
        var s = go.AddComponent<MissileTowerAI>();
        s.muzzle = Obst.Find(go.transform, "SpawnPos");
        s.triggerPoint = go.transform.TransformPoint(new Vector3(0f, 0f, -500f) / Mathf.Max(0.01f, go.transform.lossyScale.x));
        s.rate = super ? 0.3f : 0.5f;
    }

    void Update()
    {
        var pc = Obst.Player; if (!pc || !Obst.Running || used) return;
        float d = Vector3.Distance(triggerPoint, pc.transform.position + Vector3.up);
        bool inRange = d > 200f && d < 800f;
        if (inRange) engaged = true;
        else { if (engaged) used = true; return; }
        if (Time.time < next) return;
        next = Time.time + rate;
        Missile.Launch(transform.parent, muzzle ? muzzle.position : transform.position + Vector3.up * 160f, muzzle ? muzzle.forward : Vector3.up, pc);
    }
}

public class Missile : MonoBehaviour
{
    Vector3 vel, target, lastPlayer; float t; PlayerCharacter pc; Transform aim, dot, ring; MeshRenderer ringR;

    public static void Launch(Transform parent, Vector3 from, Vector3 dir, PlayerCharacter pc)
    {
        var go = new GameObject("míssil"); go.transform.SetParent(parent, false); go.transform.position = from;
        var m = go.AddComponent<Missile>(); m.pc = pc; m.vel = dir.normalized * 3500f; m.lastPlayer = pc.transform.position;
        var body = BigLaser.Prim(PrimitiveType.Capsule, go.transform, Obst.Glow(Obst.Red, 2.2f, 1f, 1f)).transform;
        body.localRotation = Quaternion.Euler(90f, 0f, 0f); body.localScale = new Vector3(3f, 2f, 3f);
        var tr = go.AddComponent<TrailRenderer>(); tr.time = 3f; tr.widthMultiplier = 1.4f; tr.sharedMaterial = Obst.Glow(Obst.Red, 1.2f, 0.4f, 0.6f);
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        m.aim = BigLaser.Prim(PrimitiveType.Cylinder, parent, Obst.Glow(Obst.Red, 1.6f, 0.6f, 0.5f)).transform;
        m.dot = BigLaser.Prim(PrimitiveType.Sphere, parent, Obst.Glow(Obst.Red, 2.4f, 1f, 1f, true)).transform; m.dot.localScale = Vector3.one * 4f;
        m.target = m.Aim();
        m.ring = Obst.Ring(parent, m.target, 14f, Obst.Glow(Obst.Red, 2.2f, 0.8f, 1f), 1f); m.ringR = m.ring.GetComponent<MeshRenderer>();
        if (Sfx.instance) Sfx.Play(Sfx.instance.jump, 0.3f, 0.55f);
    }

    Vector3 Aim()
    {
        var step = (pc.transform.position - lastPlayer); lastPlayer = pc.transform.position;
        float fixedStep = Time.deltaTime > 0f ? Time.fixedDeltaTime / Time.deltaTime : 1f;
        var p = pc.transform.position + Random.insideUnitSphere * 50f + step * fixedStep * 90f;
        p.y = Track.instance.HeightAt(p.x, p.z);
        return p;
    }

    void Update()
    {
        float dt = Time.deltaTime; t += dt;
        if (t < 0.3f && pc) { target = Aim(); if (ring) ring.position = target + Vector3.up * 0.5f; }
        vel = Vector3.Lerp(vel, (target - transform.position).normalized * 700f, 1f - Mathf.Exp(-10f * dt));
        var prev = transform.position; var step = vel * dt;
        transform.rotation = Quaternion.LookRotation(vel);
        if (Physics.Linecast(prev, prev + step, out var h, PlayerCharacter.RunnableMask, QueryTriggerInteraction.Ignore) || t > 15f)
        {
            MissileBlast.Make(transform.parent, h.collider ? h.point : prev);
            Cleanup(); Destroy(gameObject); return;
        }
        transform.position = prev + step;
        // the aim line: 300 m ahead or to the ground, with the red dot where it lands
        var dir = vel.normalized; float len = 300f;
        if (Physics.Raycast(transform.position, dir, out var hh, 300f, 1 << PlayerCharacter.TerrainLayer)) len = hh.distance;
        aim.position = transform.position + dir * len * 0.5f; aim.rotation = Quaternion.FromToRotation(Vector3.up, dir); aim.localScale = new Vector3(0.25f, len * 0.5f, 0.25f);
        dot.position = transform.position + dir * len;
        if (ringR) ringR.enabled = Mathf.Repeat(Time.time * 8f, 1f) < 0.6f;
    }

    void Cleanup() { if (aim) Destroy(aim.gameObject); if (dot) Destroy(dot.gameObject); if (ring) Destroy(ring.gameObject); }
    void OnDestroy() => Cleanup();
}

public class MissileBlast : Hazard
{
    Transform ball, flash; Material mat, flashMat; float t0; bool hit;

    public static void Make(Transform parent, Vector3 at)
    {
        var go = new GameObject("explosão"); go.transform.SetParent(parent, false); go.transform.position = at;
        var b = go.AddComponent<MissileBlast>();
        b.mat = Obst.Glow(Obst.Orange, 2.2f, 1f, 1f, true); b.flashMat = Obst.Glow(Color.white, 3f, 2f, 1f, true);
        b.ball = Prim(PrimitiveType.Sphere, go.transform, b.mat).transform;
        b.flash = Prim(PrimitiveType.Sphere, go.transform, b.flashMat).transform;
        b.t0 = Time.time; b.center = at; b.reach = 30f;
        Obst.Shake(at, 5f, 300f, 0.3f);
        if (Sfx.instance) Sfx.Play(Sfx.instance.wallHit, 0.8f, 0.7f);
        b.Update();
    }

    // Explosionnn: Sphere_Explosion diameter 0 -> 0.83 (0.15 s) -> 53 (2.017 s), fading out by 2.117 s
    float Diameter(float t) => t < 0.15f ? Mathf.Lerp(0f, 0.83f, t / 0.15f) : Mathf.Lerp(0.83f, 53f, (t - 0.15f) / 1.867f);

    void Update()
    {
        float t = Time.time - t0;
        ball.localScale = Vector3.one * Diameter(t);
        mat.SetFloat("_Opacity", t < 0.5f ? 1f : Mathf.Clamp01(1f - (t - 0.5f) / 1.617f));
        float f = t < 0.05f ? Mathf.Lerp(0f, 14.5f, t / 0.05f) : t < 0.083f ? 15.5f : t < 0.133f ? Mathf.Lerp(15.5f, 2.6f, (t - 0.083f) / 0.05f) : Mathf.Lerp(2.6f, 0f, (t - 0.133f) / 0.067f);
        flash.localScale = Vector3.one * Mathf.Max(0f, f);
        if (t > 2.2f) Destroy(gameObject);
    }

    public override bool Test(Vector3 a, Vector3 b, out Vector3 normal)
    {
        normal = Vector3.up;
        float t = Time.time - t0;
        if (hit || t > 1.25f) return false;
        return Obst.SegPointDist(a, b, center) < 0.495f * Diameter(t) + 0.5f;
    }

    public override void Hit(PlayerCharacter pc, Vector3 normal)
    {
        if (pc.Invulnerable()) return;
        hit = true;
        Hurt(pc, 51f, "MÍSSIL!", Vector3.zero, 1f);
        Obst.Redirect(pc, pc.transform.position - center, 0.15f, 1f);
    }
}

// ------------------------------------------------------------------------------------------------ barrage
// dist (a lob); bolts leave at 220 m/s (0-15 deg spread), gravity 130, stick where they land as 24x8x8 m mines (36).
public class BarrageAI : MonoBehaviour
{
    Transform spawn, trigger; float r, until = -1f, next; bool fired;

    public static void Setup(GameObject go)
    {
        var s = go.AddComponent<BarrageAI>();
        s.spawn = Obst.Find(go.transform, "Spawn");
        s.trigger = Obst.Find(go.transform, "Trigger");
        s.r = 840f * go.transform.lossyScale.x;
    }

    void Update()
    {
        var pc = Obst.Player; if (!pc || !Obst.Running || !spawn) return;
        var tp = trigger ? trigger.position : spawn.position;
        if (!fired) { if (Vector3.Distance(tp, pc.transform.position) < r) { fired = true; until = Time.time + 2f; } else return; }
        if (Time.time > until) return;
        // LookAtPlayer: the turret (and the TOP mesh under it) swings onto the lob point
        float dist = Vector3.Distance(spawn.position, pc.transform.position);
        var aim = pc.transform.position + pc.rig.linearVelocity * dist * 0.003f + Vector3.up * dist * 1f;
        spawn.rotation = Quaternion.LookRotation(aim - spawn.position);
        if (Time.time < next) return;
        next = Time.time + 0.07f;
        var dir = Quaternion.AngleAxis(Random.Range(0f, 360f), spawn.forward) * Quaternion.AngleAxis(Random.Range(0f, 15f), spawn.right) * spawn.forward;
        var look = new GameObject("projétil").transform;
        var vis = Obst.Spawn("LaserProj", look, Vector3.zero, Quaternion.identity, 1f);
        if (vis) { vis.transform.localPosition = Vector3.zero; vis.transform.localRotation = Quaternion.identity; foreach (var c in vis.GetComponentsInChildren<Collider>(true)) Destroy(c); }
        else { var core = BigLaser.Prim(PrimitiveType.Cube, look, BigLaser.CoreMat).transform; core.localScale = new Vector3(5f, 5f, 12f); }
        var shot = StickyShot.Fire(transform.parent, spawn.position, dir * 220f, 130f, look, 12f, 4f, 36f, "BARRAGEM!", true, false);
        var dash = BigLaser.Prim(PrimitiveType.Sphere, transform, Obst.Glow(Obst.Orange, 1.8f, 0.8f, 0.5f)).transform;
        dash.localScale = new Vector3(14.4f, 14.4f, 34.9f) * 0.5f; shot.SetDash(dash);
        if (Sfx.instance) Sfx.Play(Sfx.instance.jump, 0.25f, 1.3f);
    }
}

// ------------------------------------------------------------------------------------------------ mining hole
// 200 deg/s); they bounce off steep faces and stick, 36 where they lie. It stops 4 s after you come within 120 m.
public class MiningHoleAI : MonoBehaviour
{
    Transform spawn, trigger, disable; float r, rOff, closeAt = -1f, next;

    public static void Setup(GameObject go)
    {
        var s = go.AddComponent<MiningHoleAI>();
        s.spawn = Obst.Find(go.transform, "Spawn"); s.trigger = Obst.Find(go.transform, "Trigger"); s.disable = Obst.Find(go.transform, "Disable");
        float sc = go.transform.lossyScale.x; s.r = 600f * sc; s.rOff = 120f * sc;
    }

    void Update()
    {
        var pc = Obst.Player; if (!pc || !Obst.Running || !spawn) return;
        var pp = pc.transform.position;
        if (closeAt < 0f && Vector3.Distance(disable ? disable.position : transform.position, pp) < rOff) closeAt = Time.time;
        if (closeAt > 0f && Time.time > closeAt + 4f) return;
        if (Vector3.Distance(trigger ? trigger.position : transform.position, pp) > r || Time.time < next) return;
        next = Time.time + 0.15f;
        var dir = Quaternion.AngleAxis(Random.Range(15f, 40f), Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * Vector3.right) * Vector3.up;
        var look = new GameObject("cristal").transform;
        Mesh gem = null;
        var src = Track.instance.Obstacle("GemProjectile");
        if (src) foreach (var mf in src.GetComponentsInChildren<MeshFilter>(true)) if (mf.name.StartsWith("Gem")) gem = mf.sharedMesh;
        var g = new GameObject("gema"); g.transform.SetParent(look, false);
        g.AddComponent<MeshFilter>().sharedMesh = gem ? gem : Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        g.AddComponent<MeshRenderer>().sharedMaterial = Obst.Crystal;
        g.transform.localScale = Vector3.one * 1.75f;
        StickyShot.Fire(transform.parent, spawn.position, dir * 80f, 30f, look, 0f, 5f, 36f, "CRISTAIS!", true, true, 200f * Random.Range(0.5f, 1.5f));
    }
}

// ------------------------------------------------------------------------------------------------ mining laser
// +1.67 s the beam bursts to r 15 and settles at 12.5; it hurts (51, hitbox r ~10-12) from +1.7 to +2.33 s, then shrinks away.
public class MiningLaserAI : BigLaser
{
    Vector3 ground; float start; Transform warn; Material warnMat, gemMat;

    public static void Setup(GameObject go, System.Random r)
    {
        var s = go.AddComponent<MiningLaserAI>();
        s.ground = go.transform.position; s.start = Time.time + Obst.Rnd(r, 0f, 3f);
        s.gemMat = new Material(Obst.Crystal);
        foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true)) if (mr.name.StartsWith("Gem")) { mr.enabled = true; mr.sharedMaterial = s.gemMat; }
        s.AddBeam(go.transform, 0.5f); s.ShowBeam(0, false);
        s.warnMat = Obst.Glow(Obst.Cyan, 1.4f, 0.4f, 0.55f);
        s.warn = Prim(PrimitiveType.Cylinder, go.transform, s.warnMat).transform; s.warn.gameObject.SetActive(false);
        s.center = s.ground + Vector3.up * 50f; s.reach = 80f;
    }

    float V(float u)   // beam scale v over the laser's own time (radius = 0.5 v)
    {
        if (u < 0f) return 0f;
        if (u < 1.167f) return u / 1.167f;
        if (u < 1.667f) return 1f;
        if (u < 1.757f) return Mathf.Lerp(1f, 30.7f, (u - 1.667f) / 0.09f);
        if (u < 1.867f) return Mathf.Lerp(30.7f, 28.9f, (u - 1.757f) / 0.11f);
        if (u < 2.333f) return Mathf.Lerp(28.9f, 26.5f, (u - 1.867f) / 0.466f);
        return Mathf.Lerp(26.5f, 0f, (u - 2.333f) / 0.5f);
    }

    float u = -1f;
    void Update()
    {
        var pc = Obst.Player;
        if (!pc || Obst.FlatDist(pc.transform.position, ground) > 900f || Time.time < start) { ShowBeam(0, false); warn.gameObject.SetActive(false); u = -1f; return; }
        float c = Mathf.Repeat(Time.time - start, 5.7f);
        u = c - 1f;
        float v = V(u);
        bool on = u >= 0f && v > 0.01f;
        bool was = beamList[0].on;
        ShowBeam(0, on);
        if (on) { beamList[0].radius = 0.5f * v; Span(0, ground + Vector3.up * 700f, ground, true); }
        if (on && !was) { if (Sfx.instance) Sfx.Play(Sfx.instance.wallHit, 0.3f, 1.8f); }
        if (u >= 0.7f && u < 0.72f) Obst.Shake(ground, 4f, 300f, 0.25f);
        // the blue warning column: radius 0 -> 2.5 over 1 s, back to 0 from +2 s
        float w = u < 0f ? 0f : u < 1f ? u * 2.5f : u < 2f ? 2.5f : Mathf.Max(0f, 2.5f * (1f - (u - 2f)));
        warn.gameObject.SetActive(w > 0.01f);
        // the crystals charge up with the warning and flare with the burst
        float glow = u < 0f ? 0.15f : u < 1.667f ? 0.3f + 0.7f * Mathf.Abs(Mathf.Sin(Time.time * (6f + u * 8f))) * (u / 1.667f) : u < 2.333f ? 1.4f : 0.15f;
        gemMat.SetColor("_Emission", new Color(0.2f, 0.9f, 0.5f) * glow);
        if (w > 0.01f) { warn.position = ground + Vector3.up * 350f; warn.localScale = new Vector3(w * 2f, 400f, w * 2f); warnMat.SetFloat("_Opacity", 0.4f + Mathf.Abs(Mathf.Sin(Time.time * 12f)) * 0.3f); }
    }

    float burnHit = -10f;
    public override bool Test(Vector3 a, Vector3 b, out Vector3 normal)
    {
        normal = Vector3.up;
        if (u < 1.7f || u > 2.333f || Time.time < burnHit + 5f) return false;
        var q = Vector3.Lerp(a, b, 0.5f);
        bool hit = Obst.FlatDist(q, ground) < 0.4f * V(u) + 0.5f && q.y < ground.y + 700f;
        if (hit) burnHit = Time.time;
        return hit;
    }
}

// ------------------------------------------------------------------------------------------------ drop pod
// thin red core line and a 35 m decal on the ground grow in 0.33 s, blink from 1.0 s and go at 1.67 s; the pod appears 319 m
// up at 1.8 s and lands at 2.0 s (shake). Landed it's a solid prop (15, breaks).
public class DropPodAI : MonoBehaviour
{
    Transform mover, col, core, decal, mid, rocks; Vector3 rest; float r, delay, armedAt = -1f; bool done; Vector3 trig;
    Material colMat, coreMat, decalMat;

    public static void Setup(GameObject go, System.Random rnd)
    {
        var s = go.AddComponent<DropPodAI>();
        float sc = go.transform.lossyScale.x;
        s.mover = Obst.Find(go.transform, "Mover");
        s.trig = go.transform.TransformPoint(new Vector3(0f, 0f, 50f) * 2.5f / 2.5f);
        s.r = 400f * 2.5f * sc;
        s.delay = 0.6f * Obst.Rnd(rnd, 0.9f, 1.1f);
        var g = go.transform.position;
        s.colMat = Obst.Warn(Obst.Orange, 0.18f);
        s.coreMat = Obst.Glow(Obst.Red, 2f, 0.8f, 0.8f);
        s.decalMat = Obst.Glow(Obst.Orange, 2f, 0.8f, 0.9f);
        s.col = BigLaser.Prim(PrimitiveType.Cylinder, go.transform, s.colMat).transform; s.col.position = g + Vector3.up * 319f; s.col.localScale = new Vector3(27.4f, 319f, 27.4f) / sc;
        s.core = BigLaser.Prim(PrimitiveType.Cylinder, go.transform, s.coreMat).transform; s.core.position = g + Vector3.up * 319f; s.core.localScale = new Vector3(0.5f, 319f, 0.5f) / sc;
        s.decal = Obst.Ring(go.transform, g, 17.7f, s.decalMat, 1.2f);
        s.mid = Obst.Disc(go.transform, g, 1.3f, s.decalMat);
        foreach (var t in new[] { s.col, s.core, s.decal, s.mid }) t.gameObject.SetActive(false);
        if (s.mover) { s.rest = s.mover.localPosition; s.mover.gameObject.SetActive(false); }
        s.rocks = Obst.Find(go.transform, "Rocks"); if (s.rocks && (!s.mover || !s.rocks.IsChildOf(s.mover))) s.rocks.gameObject.SetActive(false);
        Obst.EnableColliders(go, false);
    }

    void Update()
    {
        if (done) return;
        var pc = Obst.Player; if (!pc) return;
        if (armedAt < 0f) { if (Obst.Running && Vector3.Distance(pc.transform.position, trig) < r) armedAt = Time.time + delay; return; }
        float t = Time.time - armedAt; if (t < 0f) return;
        float grow = Mathf.Clamp01(t / 0.333f);
        bool vis = t < 1.0f || (t >= 1.083f && t < 1.25f) || (t >= 1.333f && t < 1.5f) || (t >= 1.583f && t < 1.667f);
        foreach (var x in new[] { col, core, decal, mid }) x.gameObject.SetActive(vis);
        var sc = col.localScale; col.localScale = new Vector3(27.4f * grow, 319f, 27.4f * grow) / transform.lossyScale.x;
        if ((t >= 1.083f && t < 1.1f) || (t >= 1.333f && t < 1.35f) || (t >= 1.583f && t < 1.6f)) if (Sfx.instance) Sfx.Play(Sfx.instance.countdown, 0.5f, 1.8f);
        if (!mover) return;
        if (t >= 1.8f)
        {
            mover.gameObject.SetActive(true);
            float k = Mathf.Clamp01((t - 1.833f) / 0.167f);
            float y = Mathf.Lerp(500f, -4f, k) * 1f;
            mover.localPosition = new Vector3(rest.x, y, rest.z);
            mover.localScale = Vector3.one * Mathf.Clamp01((t - 1.8f) / 0.033f);
            if (k >= 1f)
            {
                done = true; mover.localPosition = new Vector3(rest.x, -4f, rest.z);
                if (rocks) rocks.gameObject.SetActive(true);
                Obst.EnableColliders(gameObject, true);
                Obst.Shake(transform.position, 5f, 500f, 0.5f);
                if (Sfx.instance) Sfx.Play(Sfx.instance.wallHit, 0.8f, 0.8f);
                foreach (var x in new[] { col, core, decal, mid }) x.gameObject.SetActive(false);
            }
        }
    }
}

// ------------------------------------------------------------------------------------------------ wall
// and rock jets (up at 1.77 s), then at 2.9 s the doors pull back and slam the gap shut at 3.27 s. Not lethal: 15, breaks.
public class WallAI : MonoBehaviour
{
    readonly List<(Transform side, Transform inner, float sign, float innerX, Vector3 sideRest)> sides = new List<(Transform, Transform, float, float, Vector3)>();
    Vector3 trig; float r, delay, armedAt = -1f; bool done; ParticleSystem[] jets; Transform ring; Material ringMat;

    public static void Setup(GameObject go, System.Random rnd)
    {
        var s = go.AddComponent<WallAI>();
        float sc = go.transform.lossyScale.x;
        s.trig = go.transform.TransformPoint(new Vector3(0f, 0f, -85f));
        s.r = 250f * sc; s.delay = 0.1f * Obst.Rnd(rnd, 0.75f, 1.05f);
        foreach (var n in new[] { "RightSide", "LeftSide" })
        {
            var side = Obst.Find(go.transform, n); if (!side) continue;
            var inner = Obst.Find(side, "Inner");
            float x0 = inner ? inner.localPosition.x : 0f;
            s.sides.Add((side, inner, x0 >= 0f ? 1f : -1f, x0, side.localPosition));
            side.localPosition = new Vector3(side.localPosition.x, -64.2f, side.localPosition.z);
        }
        var list = new List<ParticleSystem>();
        foreach (var x in new[] { -19.3f, 19.3f }) list.Add(Dust(go.transform, go.transform.TransformPoint(new Vector3(x, 0f, 0f))));
        s.jets = list.ToArray();
        s.ringMat = Obst.Glow(Obst.Orange, 1.8f, 0.6f, 0.9f);
        s.ring = BigLaser.Prim(PrimitiveType.Cube, go.transform, s.ringMat).transform;
        s.ring.position = go.transform.position + Vector3.up * 0.4f; s.ring.rotation = go.transform.rotation; s.ring.localScale = new Vector3(67f, 0.3f, 6f);
        s.ring.gameObject.SetActive(false);
    }

    public static ParticleSystem Dust(Transform parent, Vector3 at)
    {
        var go = new GameObject("poeira"); go.transform.SetParent(parent, false); go.transform.position = at;
        go.transform.rotation = Quaternion.LookRotation(Vector3.up);
        var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f); main.startSpeed = new ParticleSystem.MinMaxCurve(15f, 35f);
        main.startSize = new ParticleSystem.MinMaxCurve(4f, 9f); main.gravityModifier = 1.5f; main.maxParticles = 200; main.loop = true; main.playOnAwake = false;
        main.startColor = new Color(0.75f, 0.68f, 0.6f);
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 18f; sh.radius = 6f;
        var em = ps.emission; em.rateOverTime = 0f;
        var mat = Resources.Load<Material>("SmokeMat");
        var r = go.GetComponent<ParticleSystemRenderer>(); if (mat) r.sharedMaterial = mat; else r.sharedMaterial = BigLaser.FlareMat;
        go.layer = SmokeSignal.Layer;
        return ps;
    }

    // WallRiseAnim: side y -80.09 (0) -> -61.8 (0.383) -> +8.1 (1.767) -> 5.6 (2.283)
    static float SideY(float t)
    {
        if (t < 0.383f) return Mathf.Lerp(-80.09f, -61.8f, t / 0.383f);
        if (t < 1.767f) { float k = (t - 0.383f) / 1.384f; return Mathf.Lerp(-61.8f, 8.1f, 1f - (1f - k) * (1f - k)); }
        if (t < 2.283f) return Mathf.Lerp(8.1f, 5.6f, (t - 1.767f) / 0.516f);
        return 5.6f;
    }
    // doors: 2.5 held to 2.733 -> 0.4 (2.9) -> 10 (3.267) -> 11 (3.35) -> 10 (3.5), toward the gap
    static float DoorX(float t)
    {
        if (t < 2.733f) return 2.5f;
        if (t < 2.9f) return Mathf.Lerp(2.5f, 0.4f, (t - 2.733f) / 0.167f);
        if (t < 3.267f) return Mathf.Lerp(0.4f, 10f, (t - 2.9f) / 0.367f);
        if (t < 3.35f) return Mathf.Lerp(10f, 11f, (t - 3.267f) / 0.083f);
        if (t < 3.5f) return Mathf.Lerp(11f, 10f, (t - 3.35f) / 0.15f);
        return 10f;
    }

    void Update()
    {
        if (done) return;
        var pc = Obst.Player; if (!pc) return;
        if (armedAt < 0f) { if (Obst.Running && Vector3.Distance(pc.transform.position, trig) < r) { armedAt = Time.time + delay; ring.gameObject.SetActive(true); } return; }
        float t = Time.time - armedAt; if (t < 0f) return;
        foreach (var s in sides)
        {
            s.side.localPosition = new Vector3(s.sideRest.x, SideY(t), s.sideRest.z);
            if (s.inner) s.inner.localPosition = new Vector3(s.sign * DoorX(t), s.inner.localPosition.y, s.inner.localPosition.z);
        }
        float rate = t < 0.417f ? Mathf.Lerp(0f, 15f, t / 0.417f) : t < 1.767f ? Mathf.Lerp(15f, 35f, (t - 0.417f) / 1.35f) : t < 2.067f ? Mathf.Lerp(35f, 0f, (t - 1.767f) / 0.3f) : 0f;
        foreach (var j in jets) { var em = j.emission; em.rateOverTime = rate; if (rate > 0f && !j.isPlaying) j.Play(); }
        if (t > 0.5f && t < 1.767f) Obst.Shake(transform.position, 2.5f, 250f, 0.1f);
        ringMat.SetFloat("_Opacity", t < 1.767f ? 0.5f + Mathf.Abs(Mathf.Sin(Time.time * 10f)) * 0.5f : Mathf.Clamp01(1f - (t - 1.767f)));
        if (t > 3.6f) { done = true; ring.gameObject.SetActive(false); foreach (var j in jets) j.Stop(); }
    }
}

// ------------------------------------------------------------------------------------------------ desert spikes
// bursts out of the ground (1.27 s), leaning over and swinging up across the road, in a cone of dust. Every spike is a prop
// (runnable, not lethal: 15 and it snaps). The ground where it'll come up glows from the trigger.
public class DesertSpikeAI : MonoBehaviour
{
    Transform mover; Vector3 rest; Quaternion restRot; float r, delay, armedAt = -1f; bool done; ParticleSystem dust; Transform decal; Material decalMat;

    public static void Setup(GameObject go, System.Random rnd)
    {
        var s = go.AddComponent<DesertSpikeAI>();
        s.mover = Obst.Find(go.transform, "Mover");
        s.r = 605f * go.transform.lossyScale.x;
        s.delay = 0.75f * Obst.Rnd(rnd, 0.5f, 1.5f);
        if (s.mover)
        {
            s.rest = s.mover.localPosition; s.restRot = s.mover.localRotation;
            s.mover.localPosition = new Vector3(10.35f, -200f, s.rest.z); s.mover.localScale = Vector3.zero;
            foreach (var mr in s.mover.GetComponentsInChildren<MeshRenderer>())
            {
                var pi = mr.gameObject.AddComponent<PropInfo>(); pi.kind = PropInfo.Kind.Runnable; pi.lethal = false; pi.damage = 15f;
            }
        }
        s.dust = WallAI.Dust(go.transform, go.transform.position);
        s.decalMat = Obst.Glow(new Color(1f, 0.35f, 0.05f), 1.2f, 0.3f, 0.6f);
        s.decal = Obst.Disc(go.transform, go.transform.position, 60f * go.transform.lossyScale.x, s.decalMat); s.decal.gameObject.SetActive(false);
    }

    static Vector3 Pos(float t)
    {
        if (t < 0.95f) { float k = t / 0.95f; return new Vector3(10.35f, Mathf.Lerp(-200f, -57.6f, 1f - (1f - k) * (1f - k)), 0f); }
        if (t < 1.267f) return Vector3.Lerp(new Vector3(10.35f, -57.6f, 0f), new Vector3(-3.9f, -37.3f, 0f), (t - 0.95f) / 0.317f);
        return new Vector3(-3.9f, -37.3f, 0f);
    }
    static float Roll(float t) => t < 0.333f ? Mathf.Lerp(30f, 75f, t / 0.333f) : t < 0.583f ? 75f : t < 1.267f ? Mathf.Lerp(75f, 18.7f, (t - 0.583f) / 0.684f) : 18.7f;

    void Update()
    {
        if (done || !mover) return;
        var pc = Obst.Player; if (!pc) return;
        if (armedAt < 0f) { if (Obst.Running && Vector3.Distance(pc.transform.position, transform.position) < r) { armedAt = Time.time + delay; decal.gameObject.SetActive(true); } return; }
        float t = Time.time - armedAt;
        decalMat.SetFloat("_Opacity", 0.35f + Mathf.Abs(Mathf.Sin(Time.time * 8f)) * 0.35f);
        if (t < 0f) return;
        if (t < 1.15f) Obst.Shake(transform.position, Mathf.Lerp(0.3f, 3f, t / 1.15f), 600f, 0.15f);
        var p = Pos(t); mover.localPosition = new Vector3(p.x, p.y, rest.z);
        mover.localRotation = restRot * Quaternion.Euler(0f, 0f, Roll(t) - 18.7f);
        mover.localScale = Vector3.one * Mathf.Clamp01(t / 0.333f);
        var em = dust.emission; em.rateOverTime = t > 0.333f && t < 1.4f ? 35f : 0f; if (!dust.isPlaying) dust.Play();
        if (t > 1.667f) { done = true; decal.gameObject.SetActive(false); dust.Stop(); }
    }
}

// ------------------------------------------------------------------------------------------------ ice storm
// 5-8x size. Each waits 0.1-0.6 s, then shows a cyan line from high above to the ground and cyan rings where it will hit,
// and comes down from 350 (x size) to land 1.83 s later, with a stretch, a shake and a squash. Not lethal: 15, breaks.
public class IceStormAI : MonoBehaviour
{
    class Spike { public Transform root, mover; public float size, delay; public Transform line, ring1, ring2; public bool landed; }
    readonly List<Spike> spikes = new List<Spike>();
    Vector3 trig; float r, armedAt = -1f; bool done;
    static Material lineMat, ringMat;

    public static GameObject Build(Transform parent, Vector3 pos, Quaternion rot, float scale, System.Random rnd)
    {
        var go = new GameObject("Tempestade de gelo"); go.transform.SetParent(parent, false); go.transform.SetPositionAndRotation(pos, rot); go.transform.localScale = Vector3.one * scale;
        var s = go.AddComponent<IceStormAI>();
        s.trig = go.transform.TransformPoint(new Vector3(0f, 0f, -150f)); s.r = 300f * scale;
        if (!lineMat) { lineMat = Obst.Glow(Obst.Cyan, 1.8f, 0.6f, 0.7f); ringMat = Obst.Glow(Obst.Cyan, 2.2f, 0.8f, 0.9f); }
        var c = go.transform.TransformPoint(new Vector3(0f, 0f, 30f));
        for (int k = 0; k < 40; k++)
        {
            float rad = k < 25 ? 135f * Mathf.Sqrt((float)rnd.NextDouble()) : Mathf.Lerp(32f, 250f, Mathf.Sqrt((float)rnd.NextDouble()));
            float a = Obst.Rnd(rnd, 0f, Mathf.PI * 2f);
            var p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rad * scale; p.y = Track.instance.HeightAt(p.x, p.z);
            float size = Mathf.Lerp(5f, 8f, Mathf.Pow((float)rnd.NextDouble(), 2f));
            var n = Track.instance.NormalAt(p.x, p.z);
            var srot = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, n), 0.35f) * Quaternion.Euler(Obst.Rnd(rnd, -10f, 10f), Obst.Rnd(rnd, 0f, 360f), Obst.Rnd(rnd, -10f, 10f));
            var sp = Obst.Spawn("IceSpike", go.transform, p, srot, size / scale);
            if (!sp) continue;
            foreach (var col in sp.GetComponentsInChildren<Collider>(true)) col.gameObject.layer = PlayerCharacter.PropLayer;
            var info = sp.AddComponent<PropInfo>(); info.kind = PropInfo.Kind.Solid; info.lethal = false; info.damage = 15f;
            var mover = Obst.Find(sp.transform, "Mover");
            var spike = new Spike { root = sp.transform, mover = mover ? mover : sp.transform, size = size, delay = 0.5f * Obst.Rnd(rnd, 0.2f, 1.2f) };
            spike.mover.gameObject.SetActive(false);
            spike.line = BigLaser.Prim(PrimitiveType.Cylinder, go.transform, lineMat).transform; spike.line.gameObject.SetActive(false);
            spike.ring1 = Obst.Ring(go.transform, p, 9.25f * size, ringMat, 1f); spike.ring1.gameObject.SetActive(false);
            spike.ring2 = Obst.Ring(go.transform, p, 3.5f * size, ringMat, 1.6f); spike.ring2.gameObject.SetActive(false);
            Obst.EnableColliders(sp, false);
            s.spikes.Add(spike);
        }
        return go;
    }

    void Update()
    {
        if (done) return;
        var pc = Obst.Player; if (!pc) return;
        if (armedAt < 0f) { if (Obst.Running && Vector3.Distance(pc.transform.position, trig) < r) armedAt = Time.time; return; }
        float T = Time.time - armedAt; bool all = true;
        foreach (var s in spikes)
        {
            float t = T - s.delay;
            if (t < 0f) { all = false; continue; }
            var g = s.root.position;
            float grow = Mathf.Clamp01(t / 0.5f);
            // rings: 0 -> 1 (0.5 s) -> 1.5 (1.0 s) -> 0 (1.667 s)
            float rs = t < 0.5f ? grow : t < 1f ? Mathf.Lerp(1f, 1.5f, (t - 0.5f) / 0.5f) : Mathf.Lerp(1.5f, 0f, (t - 1f) / 0.667f);
            foreach (var (ring, rr) in new[] { (s.ring1, 9.25f), (s.ring2, 3.5f) })
            {
                ring.gameObject.SetActive(rs > 0.01f);
                float k = rr * s.size * rs / 5f; ring.localScale = new Vector3(k, k, ring.localScale.z); ring.Rotate(0f, 0f, -150f * Time.deltaTime, Space.Self);
            }
            if (s.landed) continue;
            all = false;
            s.mover.gameObject.SetActive(true);
            float y = t < 0.5f ? 350f : t < 1.817f ? Mathf.Lerp(350f, 14.57f, Mathf.Pow((t - 0.5f) / 1.317f, 2f)) : 0f;
            s.mover.localPosition = new Vector3(0f, y, 0f);
            s.mover.localScale = Vector3.one * grow;
            float top = g.y + y * s.size;
            s.line.gameObject.SetActive(true);
            s.line.position = new Vector3(g.x, (g.y + top) * 0.5f, g.z); s.line.localScale = new Vector3(1.6f, Mathf.Max(0.1f, (top - g.y) * 0.5f), 1.6f);
            if (t >= 1.833f)
            {
                s.landed = true; s.mover.localPosition = Vector3.zero; s.line.gameObject.SetActive(false);
                Obst.EnableColliders(s.root.gameObject, true);
                Obst.Shake(g, 5f, 300f, 0.2f);
                if (Sfx.instance) Sfx.Play(Sfx.instance.wallHit, 0.5f, 1.3f);
            }
        }
        if (all && T > 3f) done = true;
    }
}

// ------------------------------------------------------------------------------------------------ geyser
// -48 to +175 m over 5 s with the water column; switching off it sinks back over 5 s. The base bubbles before it goes.
public class GeyserAI : Hazard
{
    float sc, phase, lastHit = -10f, killY; Transform column, foam; Vector3 g;
    Material foamMat;

    public static void Setup(GameObject go, System.Random r)
    {
        var s = go.AddComponent<GeyserAI>();
        s.sc = go.transform.lossyScale.x; s.phase = Obst.Rnd(r, 0f, 5f); s.g = go.transform.position;
        var cyl = Obst.Find(go.transform, "Cylinder_1");
        if (cyl) { var mr = cyl.GetComponent<MeshRenderer>(); mr.enabled = true; mr.sharedMaterial = Obst.Water; s.column = cyl; }
        s.foamMat = Obst.Glow(new Color(0.7f, 0.95f, 1f), 1.6f, 0.6f, 0.8f);
        s.foam = Obst.Ring(go.transform, s.g, 12f * s.sc, s.foamMat, 1.6f);
        s.center = s.g + Vector3.up * 80f * s.sc; s.reach = 140f * s.sc;
    }

    void Update()
    {
        float c = Mathf.Repeat(Time.time + phase, 10f);
        bool on = c < 5f;
        killY = on ? Mathf.Lerp(-48f, 175.4f, c / 5f) : Mathf.Lerp(175.4f, -48f, (c - 5f) / 5f);
        // the water column follows the capsule's top
        float top = Mathf.Clamp((killY + 59.5f - 17.85f) * sc, 0f, 277f * sc);
        if (column)
        {
            column.gameObject.SetActive(top > 0.5f);
            var mf = column.GetComponent<MeshFilter>(); float my = mf && mf.sharedMesh ? mf.sharedMesh.bounds.size.y : 2f, mx = mf && mf.sharedMesh ? mf.sharedMesh.bounds.size.x : 1f;
            column.position = g + Vector3.up * top * 0.5f; column.rotation = Quaternion.identity;
            column.localScale = new Vector3(28.9f * sc / mx, top / my, 28.9f * sc / mx) / Mathf.Max(0.0001f, column.parent.lossyScale.x);
        }
        // bubbling base in the last second before it switches on
        bool bubbling = !on && c > 9f;
        foam.gameObject.SetActive(bubbling || top > 0.5f);
        if (bubbling) { float k = (12f + Mathf.Sin(Time.time * 30f) * 1.5f) * sc / 5f; foam.localScale = new Vector3(k, k, 1.6f); }
    }

    public override bool Test(Vector3 a, Vector3 b, out Vector3 normal)
    {
        normal = Vector3.up;
        if (Time.time < lastHit + 1f) return false;
        float cy = g.y + (killY - 17.85f) * sc, half = 59.5f * sc, rad = 9.35f * sc;
        return Hazard.SegSeg(a, b, new Vector3(g.x, cy - half + rad, g.z), new Vector3(g.x, cy + half - rad, g.z)) < rad;
    }

    public override void Hit(PlayerCharacter pc, Vector3 normal)
    {
        if (pc.Invulnerable()) return;
        lastHit = Time.time;
        Hurt(pc, 45f, "GÊISER!", Vector3.zero, 1f);
    }
}

// ------------------------------------------------------------------------------------------------ meteors
// side); it comes down in 0.93 s from 62 deg up, and 1.05 s after it starts a shockwave runs out to 320 m in 3.5 s. On the
// ground when it passes: 35 (less as it fades) and a push up; in the air it misses.
public class MeteorSpawner : MonoBehaviour
{
    float next; readonly List<GameObject> alive = new List<GameObject>();

    public static void Add(Transform parent, int level)
    {
        var s = parent.gameObject.AddComponent<MeteorSpawner>();
        s.next = Time.time + 14f * Random.Range(0.85f, 1.2f) * Random.Range(0.35f, 1f);
    }

    void Update()
    {
        var pc = Obst.Player; if (!pc || !Obst.Running || Time.time < next) return;
        next = Time.time + 14f * Random.Range(0.85f, 1.2f);
        var v = pc.rig.linearVelocity; v.y = 0f; if (v.sqrMagnitude < 1f) return;
        var dir = v.normalized; var side = Vector3.Cross(Vector3.up, dir);
        var p = pc.transform.position + dir * Random.Range(450f, 550f) + side * Random.Range(0f, 75f) * (Random.value < 0.5f ? -1f : 1f);
        p.y = Track.instance.HeightAt(p.x, p.z) - 1.5f;
        if (Mathf.Abs(p.y - pc.transform.position.y) > 100f) return;
        alive.RemoveAll(x => !x);
        if (alive.Count >= 5) { Destroy(alive[0]); alive.RemoveAt(0); }
        alive.Add(MeteorAI.Drop(transform, p, -dir));
    }
}

public class MeteorAI : Hazard
{
    Transform rock, trail, wave, groundRing; Material waveMat; Vector3 from, impact; float t0, lastHit = -10f; bool landed;
    const float WaveTime = 3.5f, WaveMax = 320f, WaveH = 35f, WaveW = 40f;

    public static GameObject Drop(Transform parent, Vector3 at, Vector3 facing)
    {
        var go = new GameObject("Meteoro"); go.transform.SetParent(parent, false); go.transform.position = at;
        go.transform.rotation = Quaternion.LookRotation(facing) * Quaternion.Euler(0f, Random.Range(-180f, 180f), 0f);
        var s = go.AddComponent<MeteorAI>();
        s.impact = at; s.from = go.transform.TransformPoint(new Vector3(481.1f, 927.9f, 0f));
        var vis = Obst.Spawn("Meteor", go.transform, s.from, Quaternion.identity, 1f);
        s.rock = vis ? vis.transform : BigLaser.Prim(PrimitiveType.Sphere, go.transform, MetalMat).transform;
        if (vis) foreach (var c in vis.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        else s.rock.localScale = Vector3.one * 60f;
        s.trail = BigLaser.Prim(PrimitiveType.Sphere, go.transform, Obst.Glow(Obst.Orange, 1.3f, 0.6f, 0.55f)).transform;
        s.groundRing = Obst.Ring(go.transform, at, 1f, Obst.Glow(new Color(1f, 0.2f, 0.05f), 2f, 0.8f, 1f), 1.5f); s.groundRing.gameObject.SetActive(false);
        var w = new GameObject("onda"); w.transform.SetParent(go.transform, false); w.transform.position = at;
        w.AddComponent<MeshFilter>().sharedMesh = Obst.Band();
        s.waveMat = Obst.Warn(new Color(1f, 0.25f, 0.08f), 0.3f);
        var mr = w.AddComponent<MeshRenderer>(); mr.sharedMaterial = s.waveMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        s.wave = w.transform; w.SetActive(false);
        s.t0 = Time.time + Random.Range(0.075f, 0.12f);
        s.center = at; s.reach = WaveMax + 10f;
        Destroy(go, 16f);
        return go;
    }

    float WaveR => Mathf.Clamp01((Time.time - t0 - 1.05f) / WaveTime) * WaveMax;
    float Fade => Mathf.Clamp01((WaveR - 15f) / (WaveMax - 15f));

    void Update()
    {
        float t = Time.time - t0; if (t < 0f) return;
        if (!landed)
        {
            float k = Mathf.Clamp01(t / 0.933f);
            var p = Vector3.Lerp(from, impact, k);
            rock.position = p; rock.localScale = Vector3.one * Mathf.Clamp01(t / 0.333f);
            var back = (from - impact).normalized;
            trail.position = p + back * 40f; trail.rotation = Quaternion.FromToRotation(Vector3.up, back); trail.localScale = new Vector3(18f, 80f, 18f) * Mathf.Clamp01(t / 0.117f);
            if (k >= 1f)
            {
                landed = true; rock.position = impact; trail.gameObject.SetActive(false);
                foreach (var c in rock.GetComponentsInChildren<Collider>(true)) { c.enabled = true; c.gameObject.layer = PlayerCharacter.PropLayer; }
                var pi = rock.gameObject.AddComponent<PropInfo>(); pi.kind = PropInfo.Kind.Runnable;
                Obst.Shake(impact, 8f, 2000f, 0.5f);
                if (Sfx.instance) Sfx.Play(Sfx.instance.wallHit, 1f, 0.5f);
            }
            return;
        }
        if (t < 1.05f) return;
        float wr = WaveR;
        wave.gameObject.SetActive(wr < WaveMax);
        float h = WaveH * (1f - Fade);
        wave.localScale = new Vector3(Mathf.Max(0.1f, wr), Mathf.Max(0.1f, h), Mathf.Max(0.1f, wr));
        Obst.SetAlpha(waveMat, 0.32f * (1f - Fade));
        groundRing.gameObject.SetActive(wr < WaveMax);
        float gs = Mathf.Max(0.1f, wr) / 5f; groundRing.localScale = new Vector3(gs, gs, 1.5f);
    }

    public override bool Test(Vector3 a, Vector3 b, out Vector3 normal)
    {
        normal = Vector3.up;
        if (!landed || Time.time - t0 < 1.05f || Time.time < lastHit + 0.5f) return false;
        float wr = WaveR; if (wr >= WaveMax) return false;
        float d = Obst.FlatDist(b, impact), off = Mathf.Abs(d - wr);
        if (off > WaveW) return false;
        var pc = Obst.Player; if (!pc) return false;
        return pc.data.distanceToGround < WaveH * (1f - off / WaveW) * (1f - Fade) - 2f;
    }

    public override void Hit(PlayerCharacter pc, Vector3 normal)
    {
        if (pc.Invulnerable()) return;
        lastHit = Time.time;
        Hurt(pc, 35f * Mathf.Lerp(1f, 0.2f, Fade), "ONDA DE CHOQUE!", pc.health - 25f > 0f ? Vector3.up * 15f : Vector3.zero, 1f);
    }
}
