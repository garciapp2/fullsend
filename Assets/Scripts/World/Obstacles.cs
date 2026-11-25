using System.Collections.Generic;
using UnityEngine;

// their warnings drawn in the game's comic style. Runtime-only components (several per file is fine).

public static class Obst
{
    public const int HazardLayer = 14;   // laser cages: tested with casts, never collide with the sled

    public static PlayerCharacter Player => RunManager.instance ? RunManager.instance.player : null;
    public static bool Running => RunManager.instance && RunManager.instance.state == RunManager.State.Run;

    static bool layersSet;
    public static void InitLayers()
    {
        if (layersSet) return; layersSet = true;
        for (int l = 0; l < 32; l++) Physics.IgnoreLayerCollision(HazardLayer, l, true);
        // PlayerMovement.GroundCollision's line checks (PropInfo decides which)
        Physics.IgnoreLayerCollision(PlayerCharacter.PlayerLayer, PlayerCharacter.PropLayer, true);
    }

    public static GameObject Spawn(string name, Transform parent, Vector3 pos, Quaternion rot, float scale)
    {
        var src = Track.instance ? Track.instance.Obstacle(name) : null;
        if (!src) return null;
        var go = Object.Instantiate(src, parent);
        go.name = name;
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = src.transform.localScale * scale;
        return go;
    }

    public static Transform Find(Transform root, string name)
    {
        if (!root) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    public static Bounds VisibleBounds(GameObject go)
    {
        Bounds b = new Bounds(go.transform.position, Vector3.zero); bool has = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    public static float GroundMax(Vector3 p, float radius)
    {
        var tr = Track.instance;
        float h = tr.HeightAt(p.x, p.z);
        if (radius <= 0.5f) return h;
        for (int ring = 1; ring <= 2; ring++)
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f + ring * 0.4f, r = radius * ring / 2f;
                h = Mathf.Max(h, tr.HeightAt(p.x + Mathf.Cos(a) * r, p.z + Mathf.Sin(a) * r));
            }
        return h;
    }

    public static void EnableColliders(GameObject go, bool on) { foreach (var c in go.GetComponentsInChildren<Collider>(true)) if (c.gameObject.layer != HazardLayer) c.enabled = on; }

    // the game's beam material, tinted (warnings, glows, explosions)
    public static Material Glow(Color c, float intensity = 1.6f, float hot = 0.6f, float opacity = 0.85f, bool radial = false)
    {
        var m = new Material(BigLaser.BeamMat) { name = "brilho" };
        m.SetColor("_Color", c); m.SetFloat("_Intensity", intensity); m.SetFloat("_Hot", hot);
        m.SetFloat("_Opacity", opacity); m.SetFloat("_Radial", radial ? 1f : 0f); m.SetFloat("_Length", 1f);
        return m;
    }

    public static readonly Color Red = new Color(1f, 0.18f, 0.12f), Orange = new Color(1f, 0.45f, 0.08f),
        Cyan = new Color(0.25f, 0.85f, 1f), Green = new Color(0.2f, 1f, 0.55f);

    // a flat ring on the ground (warning decal)
    public static Transform Ring(Transform parent, Vector3 pos, float radius, Material m, float thick = 0.6f)
    {
        var g = new GameObject("aviso"); g.transform.SetParent(parent, false);
        g.AddComponent<MeshFilter>().sharedMesh = Track.TorusMesh(5f, 0.45f);
        var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        g.transform.position = pos + Vector3.up * 0.5f;
        g.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        g.transform.localScale = new Vector3(radius / 5f, radius / 5f, thick);
        return g.transform;
    }

    public static Transform Disc(Transform parent, Vector3 pos, float radius, Material m)
    {
        var d = BigLaser.Prim(PrimitiveType.Cylinder, parent, m).transform;
        d.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        d.position = pos + Vector3.up * 0.3f; d.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);
        return d;
    }

    // an open band (a ring wall): radius 1, height 1, scale it (R, H, R)
    static Mesh band;
    public static Mesh Band()
    {
        if (band) return band;
        const int n = 72; var v = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i <= n; i++) { float a = i * Mathf.PI * 2f / n; var p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); v.Add(p); v.Add(p + Vector3.up); }
        for (int i = 0; i < n; i++) { int a = i * 2; t.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2, a, a + 2, a + 1, a + 1, a + 2, a + 3 }); }
        band = new Mesh { name = "faixa" }; band.SetVertices(v); band.SetTriangles(t, 0); band.RecalculateNormals();
        return band;
    }

    public static float Rnd(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
    public static float FlatDist(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

    public static float SegPointDist(Vector3 a, Vector3 b, Vector3 p)
    {
        var ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
        return Vector3.Distance(a + ab * t, p);
    }

    public static void Shake(Vector3 at, float amount, float range, float time = 0.4f)
    {
        var pc = Player; if (!pc) return;
        float k = Mathf.Clamp01(1f - Vector3.Distance(pc.transform.position, at) / range);
        if (k > 0f) GameFeel.Shake(amount * k, time);
    }

    public static void Redirect(PlayerCharacter pc, Vector3 dir, float amount, float slow)
    {
        var v = pc.rig.linearVelocity; float sp = v.magnitude;
        if (sp < 0.1f || dir.sqrMagnitude < 1e-4f) return;
        pc.rig.linearVelocity = Vector3.Slerp(v / sp, dir.normalized, Mathf.Clamp01(amount)) * sp * slow;
    }

    public static Material Warn(Color c, float alpha)
    {
        var src = Resources.Load<Material>("WarnMat");
        var m = src ? new Material(src) : Glow(c, 0.6f, 0f, alpha);
        if (src) m.SetColor("_BaseColor", new Color(c.r, c.g, c.b, alpha));
        return m;
    }
    public static void SetAlpha(Material m, float a) { if (m.HasProperty("_BaseColor")) { var c = m.GetColor("_BaseColor"); c.a = a; m.SetColor("_BaseColor", c); } else m.SetFloat("_Opacity", a); }

    static Material crystal, water;
    public static Material Crystal => crystal ? crystal : (crystal = Hazard.Mat(new Color(0.6f, 1f, 0.8f), new Color(0.05f, 0.45f, 0.2f)));
    public static Material Water => water ? water : (water = Hazard.Mat(new Color(0.55f, 0.85f, 1f), new Color(0.15f, 0.35f, 0.5f)));
}

// then each leg easing from 1.35x to 0.5x), SpringFollow (damped spring, 0.5 s per oscillation) and Laser (terrain-only
// raycast, beam cut at the hit, thinned to 25% where it hits nothing).
public class ObstacleRig : BigLaser
{
    class Mv { public ObstacleData.Mover m; public int id; public Transform target; public Vector3 vel; public float startDist, mult; public bool reached; }
    class Fl { public ObstacleData.Follow f; public Vector3 vel; }
    readonly List<(Transform t, Vector3 rot, bool world)> spins = new List<(Transform, Vector3, bool)>();
    readonly List<Mv> movers = new List<Mv>();
    readonly List<Fl> follows = new List<Fl>();
    readonly List<ObstacleData.Beam> beams = new List<ObstacleData.Beam>();
    public bool beamsOn = true;

    public static ObstacleRig Attach(GameObject go, System.Random r)
    {
        var data = go.GetComponent<ObstacleData>();
        var rig = go.AddComponent<ObstacleRig>();
        if (!data) return rig;
        foreach (var s in data.spins)
        {
            var rot = s.rotation;
            if (s.random > 0f) rot = new Vector3(rot.x * (1f + Obst.Rnd(r, -s.random, s.random)), rot.y * (1f + Obst.Rnd(r, -s.random, s.random)), rot.z * (1f + Obst.Rnd(r, -s.random, s.random)));
            rig.spins.Add((s.t, rot, s.world));
        }
        foreach (var m in data.movers)
        {
            var mv = new Mv { m = m, id = m.randomStart ? r.Next(m.points.Count) : Mathf.Clamp(m.startId, 0, m.points.Count - 1), mult = Obst.Rnd(r, m.mult.x, m.mult.y) };
            if (m.startAtFirst) m.t.position = m.points[mv.id].position;
            foreach (var p in m.points) p.SetParent(m.t.parent, true);
            mv.target = m.points[mv.id];
            rig.movers.Add(mv);
        }
        foreach (var f in data.follows) { f.t.SetParent(go.transform.parent, true); rig.follows.Add(new Fl { f = f }); }
        float sc = go.transform.lossyScale.x;
        foreach (var b in data.beams) { rig.beams.Add(b); rig.AddBeam(go.transform, b.radius * sc); }
        rig.center = go.transform.position; rig.reach = 900f;
        return rig;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        foreach (var (t, rot, world) in spins) if (t) t.Rotate(rot * dt, world ? Space.World : Space.Self);
        foreach (var mv in movers) Move(mv, dt);
        foreach (var fl in follows) Follow(fl, dt);
        for (int k = 0; k < beams.Count; k++)
        {
            var b = beams[k]; if (!b.t) continue;
            ShowBeam(k, beamsOn);
            if (beamsOn) Aim(k, b.t.position, b.t.forward, b.maxLength, -1e6f);
        }
        center = transform.position;
    }

    void Move(Mv mv, float dt)
    {
        var m = mv.m; if (!m.t || mv.target == null) return;
        float dist = Vector3.Distance(mv.target.position, m.t.position);
        if (dist < m.threshold) mv.reached = true;
        if (!mv.reached)
        {
            var dir = (mv.target.position - m.t.position).normalized;
            float speed = Mathf.Lerp(m.baseSpeed * 0.5f, m.baseSpeed * 1.35f, mv.startDist > 0f ? dist / mv.startDist : 1f) * mv.mult;
            mv.vel = Vector3.Lerp(mv.vel, dir * speed, m.lerp);
            m.t.position += mv.vel * dt * m.drag;
        }
        m.t.rotation = Quaternion.Lerp(m.t.rotation, Quaternion.LookRotation(mv.target.forward, Vector3.up), m.rotateSpeed * dt);
        if (!mv.reached) return;
        if (mv.id < m.points.Count - 1) mv.id++; else if (m.loop) mv.id = 0;
        mv.target = m.points[mv.id];
        mv.reached = false;
        mv.startDist = Vector3.Distance(mv.target.position, m.t.position);
    }

    // FRILerp.SimulateSpring: damped spring toward the target
    void Follow(Fl fl, float dt)
    {
        var f = fl.f; if (!f.t || !f.target) return;
        float w = 2f * Mathf.PI / Mathf.Max(0.05f, f.period);
        int steps = Mathf.CeilToInt(dt / 0.01f); float h = dt / Mathf.Max(1, steps);
        var p = f.t.position;
        for (int s = 0; s < steps; s++)
        {
            var acc = w * w * (f.target.position - p) - 2f * f.damp * w * fl.vel;
            fl.vel += acc * h; p += fl.vel * h;
        }
        f.t.position = p;
        f.t.rotation = Quaternion.RotateTowards(f.t.rotation, f.target.rotation, Mathf.Clamp01(1f - Mathf.Abs(Quaternion.Dot(f.t.rotation, f.target.rotation))) * f.rotSpring);
    }
}

// ------------------------------------------------------------------------------------------------ placement
// at a random point between lengthRange of the level, xOffset = U(widthRange) x (+-1) x 100 m from the road centre,
// scale lerp(min, max, U^pow), facing along the road / sideways / random, at least collisionSize x scale apart along
// the road from everything already placed.
public static class ObstacleSpawner
{
    public class Preset
    {
        public string name, prefab, biome; public float cost; public int spawns, minLevel;
        public Vector2 len = new Vector2(0.06f, 0.95f), width = new Vector2(0f, 1f);
        public float minScale = 1f, maxScale = 1f, scalePow = 2f, collision = 30f; public int dir;
        public bool lethal = true, runnable, groundFit; public float damage = 15f, lethalDamage = 85f;
        public System.Action<GameObject, System.Random> setup;
        public Preset(string name, string prefab, float cost, int spawns) { this.name = name; this.prefab = prefab; this.cost = cost; this.spawns = spawns; }
    }

    public static List<Preset> All()
    {
        return new List<Preset>
        {
            new Preset("drones", "Laser_Flying_Single", 5, 15) { len = new Vector2(0.172f, 0.94f), width = Vector2.zero, collision = 40 },
            new Preset("drones em linha", "Laser_flying_Group_Line", 6, 3) { len = new Vector2(0.143f, 0.94f), width = Vector2.zero, collision = 200 },
            new Preset("drones em quadrado", "Laser_flying_Group_Square", 6, 3) { len = new Vector2(0.172f, 0.912f), width = new Vector2(0f, 0.546f), collision = 180 },
            new Preset("drones em X", "Laser_flying_Group_X", 6, 3) { len = new Vector2(0.092f, 0.94f), width = new Vector2(0f, 0.394f), collision = 120 },
            new Preset("postes", "Pole_Modular_Fixed", 2, 20) { len = new Vector2(0.054f, 0.98f), collision = 25, dir = 2, lethal = false, setup = (g, r) => PoleRig.Setup(g, r) },
            new Preset("cerca de laser", "Pole_Modular_Fixed_Linked", 5, 20) { len = new Vector2(0.048f, 0.98f), width = new Vector2(0.054f, 1.197f), collision = 10, scalePow = 1f, lethal = false, setup = (g, r) => PoleRig.Setup(g, r) },
            new Preset("orbe de laser", "LaserGridOrb", 6, 4) { len = new Vector2(0.177f, 0.905f), minScale = 0.5f, scalePow = 1.5f, collision = 165, setup = (g, r) => OrbCage.Setup(g) },
            new Preset("orbe de laser grande", "LaserGridOrbBig", 8, 4) { len = new Vector2(0.177f, 0.905f), maxScale = 1.5f, scalePow = 1.5f, collision = 180, setup = (g, r) => OrbCage.Setup(g) },
            new Preset("orbe de laser enorme", "LaserGridOrbHuge", 9, 2) { len = new Vector2(0.177f, 0.905f), minScale = 0.5f, collision = 300, setup = (g, r) => OrbCage.Setup(g) },
            new Preset("bombas", "AgencyBomb", 5, 10) { len = new Vector2(0.083f, 0.888f), collision = 25, lethal = false, setup = (g, r) => BombAI.Setup(g, r) },
            new Preset("bombas móveis", "AgencyBomb_Moving", 6, 10) { len = new Vector2(0.083f, 0.888f), width = new Vector2(0f, 0.177f), collision = 50, lethal = false, setup = (g, r) => BombAI.Setup(g, r) },
            new Preset("torre de mísseis", "MissileTower", 9, 3) { len = new Vector2(0.258f, 0.944f), collision = 30, setup = (g, r) => MissileTowerAI.Setup(g, false) },
            new Preset("torre de mísseis super", "MissileTower", 12, 3) { len = new Vector2(0.258f, 0.944f), collision = 30, minLevel = 3, setup = (g, r) => MissileTowerAI.Setup(g, true) },
            new Preset("barragem", "Barrage", 6, 4) { len = new Vector2(0.169f, 0.944f), collision = 13, setup = (g, r) => BarrageAI.Setup(g) },
            new Preset("cápsulas", "DropPod", 5, 25) { len = new Vector2(0.066f, 0.94f), collision = 30, lethal = false, setup = (g, r) => DropPodAI.Setup(g, r) },
            new Preset("muros", "Wall", 3, 5) { len = new Vector2(0.066f, 0.94f), minScale = 0.7f, maxScale = 0.7f, scalePow = 1f, collision = 35, lethal = false, setup = (g, r) => WallAI.Setup(g, r) },
            new Preset("torre de laser", "LaserSpinner", 4, 5) { len = new Vector2(0.112f, 0.962f), collision = 45, groundFit = true },
            new Preset("anel de laser", "LaserRing", 5, 4) { len = new Vector2(0.177f, 0.905f), width = new Vector2(0f, 0.202f), collision = 180, runnable = true },
            new Preset("anel de laser gigante", "LaserRing_Big", 6, 1) { len = new Vector2(0.34f, 0.656f), width = new Vector2(0f, 0.202f), collision = 450, runnable = true },
            // desert
            new Preset("laser de mineração", "MiningLaser", 3, 13) { biome = "Deserto", len = new Vector2(0.083f, 0.475f), collision = 15, setup = (g, r) => MiningLaserAI.Setup(g, r) },
            new Preset("laser de mineração (fim)", "MiningLaser", 0, 13) { biome = "Deserto", len = new Vector2(0.569f, 0.829f), collision = 25, setup = (g, r) => MiningLaserAI.Setup(g, r) },
            new Preset("espinhos do deserto", "Desert_MovingSpike", 7, 10) { biome = "Deserto", len = new Vector2(0.083f, 0.888f), width = new Vector2(0f, 0.997f), minScale = 0.8f, maxScale = 1.2f, scalePow = 6f, collision = 60, lethal = false, runnable = true, setup = (g, r) => DesertSpikeAI.Setup(g, r) },
            new Preset("buraco de mineração", "MiningHole", 5, 5) { biome = "Deserto", len = new Vector2(0.172f, 0.94f), collision = 60, runnable = true, setup = (g, r) => MiningHoleAI.Setup(g) },
            // snow
            new Preset("tempestade de gelo", "IceSpike", 8, 2) { biome = "Neve", len = new Vector2(0.15f, 0.944f), width = new Vector2(0f, 0.598f), maxScale = 1.25f, collision = 250, lethal = false, setup = null },
            new Preset("gêiseres", "Gayser", 3, 13) { biome = "Neve", len = new Vector2(0.083f, 0.382f), minScale = 0.5f, collision = 30, setup = (g, r) => GeyserAI.Setup(g, r) },
            new Preset("gêiseres (meio)", "Gayser", 0, 7) { biome = "Neve", len = new Vector2(0.369f, 0.733f), minScale = 0.5f, collision = 30, setup = (g, r) => GeyserAI.Setup(g, r) },
            new Preset("gêiseres (fim)", "Gayser", 0, 13) { biome = "Neve", len = new Vector2(0.687f, 0.888f), minScale = 0.5f, collision = 30, setup = (g, r) => GeyserAI.Setup(g, r) },
        };
    }

    class Placed { public float s, size; }

    public static string Build(Track tr, Transform parent, System.Random r, int level, float budget)
    {
        Obst.InitLayers();
        string bio = tr.biome != null ? tr.biome.name : "";
        var all = All();
        var bands = all.FindAll(p => p.cost == 0f);
        var presets = all.FindAll(p => p.cost > 0f && (p.biome == null || p.biome == bio) && level >= p.minLevel);
        var placed = new List<Placed>();
        var picked = new List<string>();
        var poles = new List<GameObject>(); bool linked = false;
        for (int tries = 0; tries < 100 && budget >= 2f && presets.Count > 0; tries++)
        {
            var p = presets[r.Next(presets.Count)];
            if (p.cost > budget) continue;
            if ((float)r.NextDouble() > p.cost / budget && budget > p.cost * 1.5f) continue;   // KeyPropPreset: accept with difficulty / budget left
            budget -= p.cost; presets.Remove(p); picked.Add(p.name);
            var group = new List<Preset> { p };
            foreach (var b in bands) if (b.prefab == p.prefab) group.Add(b);
            foreach (var g in group)
                for (int k = 0; k < g.spawns; k++)
                {
                    var go = Place(tr, parent, g, r, placed);
                    if (!go) continue;
                    if (g.prefab.StartsWith("Pole_Modular")) { poles.Add(go); linked |= g.prefab.EndsWith("Linked"); }
                }
        }
        if (linked) PoleLinks.Build(parent, poles, r);
        // into Infinito, and half as often
        bool finalDelivery = RunManager.mode != RunMode.Infinito && RunManager.instance && level >= RunManager.instance.levelsPerRun - 1;
        if (finalDelivery || (RunManager.mode == RunMode.Infinito && level >= 5)) MeteorSpawner.Add(parent, level);
        return string.Join(", ", picked);
    }

    // test hook: one preset at road index i, x metres to the side, given scale (no spacing rules)
    public static GameObject SpawnForTest(string presetName, Transform parent, int i, float x, float scale, int seed)
    {
        var tr = Track.instance; var r = new System.Random(seed);
        var p = All().Find(q => q.name == presetName); if (p == null) return null;
        p = new Preset(p.name, p.prefab, p.cost, 1) { len = new Vector2((float)i / (tr.Count - 1), (float)i / (tr.Count - 1)), width = new Vector2(Mathf.Abs(x) / 100f, Mathf.Abs(x) / 100f),
            minScale = scale, maxScale = scale, dir = p.dir, lethal = p.lethal, runnable = p.runnable, damage = p.damage, lethalDamage = p.lethalDamage, setup = p.setup };
        return Place(tr, parent, p, r, new List<Placed>());
    }

    static GameObject Place(Track tr, Transform parent, Preset p, System.Random r, List<Placed> placed)
    {
        for (int t = 0; t < 60; t++)
        {
            float u = Obst.Rnd(r, p.len.x, p.len.y);
            int i = Mathf.Clamp(Mathf.RoundToInt(u * (tr.Count - 1)), 60, tr.finishIndex - 60);
            if (tr.Gap[i] > 0f) continue;
            float scale = Mathf.Lerp(p.minScale, p.maxScale, Mathf.Pow((float)r.NextDouble(), p.scalePow));
            float s = i * tr.step, size = p.collision * scale;
            bool clash = false;
            foreach (var q in placed) if (Mathf.Abs(q.s - s) < size + q.size) { clash = true; break; }
            if (clash) continue;
            float x = Obst.Rnd(r, p.width.x, p.width.y) * (r.NextDouble() < 0.5 ? -1f : 1f) * 100f;
            var pos = tr.P[i] + tr.R[i] * x; pos.y = tr.HeightAt(pos.x, pos.z);
            var fwd = tr.T[i]; fwd.y = 0f; fwd.Normalize();
            var side = tr.R[i]; side.y = 0f; side.Normalize();
            var rot = p.dir == 0 ? Quaternion.LookRotation(fwd) : p.dir == 1 ? Quaternion.LookRotation(side) : Quaternion.Euler(0f, Obst.Rnd(r, 0f, 360f), 0f);
            placed.Add(new Placed { s = s, size = size });
            if (p.name == "tempestade de gelo") return IceStormAI.Build(parent, pos, rot, scale, r);
            var go = Obst.Spawn(p.prefab, parent, pos, rot, scale);
            if (!go) return null;
            Snap(go);
            // so those stand on the ground instead
            if (p.groundFit) { var b = Obst.VisibleBounds(go); go.transform.position += Vector3.up * (Obst.GroundMax(b.center, b.extents.x * 0.5f) - b.min.y - 1f); }
            Props(go, p);
            ObstacleRig.Attach(go, r);
            var hd = go.GetComponent<ObstacleData>();
            if (hd) foreach (var f in hd.follows) PropFix.Claim(go, f.t);
            p.setup?.Invoke(go, r);
            Rings(go, tr);
            return go;
        }
        return null;
    }

    // KeyPropSnapToGround: each snap point drops to the highest terrain within its radius (world Y only)
    public static void Snap(GameObject go)
    {
        var data = go.GetComponent<ObstacleData>(); if (!data) return;
        foreach (var s in data.snaps)
        {
            if (!s.t) continue;
            var p = s.t.position; p.y = Obst.GroundMax(p, s.radius * go.transform.lossyScale.x);
            s.t.position = p;
        }
    }

    static void Props(GameObject go, Preset p)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
        {
            if (c.gameObject.layer == Obst.HazardLayer) continue;
            c.gameObject.layer = PlayerCharacter.PropLayer;
            if (c.GetComponentInParent<PropInfo>()) continue;
        }
        var info = go.AddComponent<PropInfo>();
        info.kind = p.runnable ? PropInfo.Kind.Runnable : PropInfo.Kind.Solid;
        info.lethal = p.lethal; info.damage = p.damage; info.lethalDamage = p.lethalDamage;
    }

    static void Rings(GameObject go, Track tr)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            if (t.name == "BoostRing") tr.SpawnRing(t.position, t.forward, Mathf.Max(1f, t.lossyScale.x * 1.65f), t.parent);
    }
}

// Copies PropInfo onto hitboxes that a SpringFollow moved out from under the obstacle (drone hulls, moving bombs).
public static class PropFix
{
    public static void Claim(GameObject root, Transform moved)
    {
        var src = root.GetComponent<PropInfo>(); if (!src || !moved || moved.GetComponent<PropInfo>()) return;
        var pi = moved.gameObject.AddComponent<PropInfo>();
        pi.kind = src.kind; pi.lethal = src.lethal; pi.damage = src.damage; pi.lethalDamage = src.lethalDamage;
    }
}

// ------------------------------------------------------------------------------------------------ poles
// (pitch 13.5 x scale) and lift the head. The head's laser orb (trigger 37.5 x scale) hits for 51 with a shove.
public class PoleRig : Hazard
{
    Transform orb, top; float orbR; bool inside;

    public static void Setup(GameObject go, System.Random r)
    {
        var min = Obst.Find(go.transform, "Min"); var max = Obst.Find(go.transform, "Max");
        var top = Obst.Find(go.transform, "TopParent"); var blocks = Obst.Find(go.transform, "BlockParent");
        float sc = go.transform.lossyScale.x;
        float pitch = 13.5f * sc;
        if (min && max && top)
        {
            float dist = Vector3.Distance(min.position, max.position);
            float h = (float)r.NextDouble() < 0.5f ? dist * Obst.Rnd(r, 0.25f, 0.6f) : (float)r.NextDouble();
            int n = Mathf.FloorToInt(h / pitch);
            top.position = min.position + go.transform.up * n * pitch;
            for (int i = 0; i < n; i++)
            {
                var b = Obst.Spawn("Pole_Block", blocks ? blocks : go.transform, min.position + go.transform.up * pitch * i, Quaternion.LookRotation(go.transform.up, go.transform.forward), 1f);
                if (b) foreach (var c in b.GetComponentsInChildren<Collider>(true)) c.gameObject.layer = PlayerCharacter.PropLayer;
            }
        }
        var s = go.AddComponent<PoleRig>();
        s.top = top;
        s.orb = top ? Obst.Find(top, "Sphere") : null;
        if (s.orb)
        {
            var mr = s.orb.GetComponent<MeshRenderer>();
            if (mr) { mr.enabled = true; mr.sharedMaterial = BigLaser.CoreMat; }
            BigLaser.GlowBall(s.orb, Vector3.zero, 1.6f, 1.6f);
        }
        s.orbR = 37.5f * sc;
        s.center = s.orb ? s.orb.position : go.transform.position; s.reach = s.orbR + 2f;
    }

    // PoleLinker endpoint: the head (TopParent) + 10 m up
    public Vector3 Head => (top ? top.position : transform.position) + Vector3.up * 10f;

    public override bool Test(Vector3 a, Vector3 b, out Vector3 normal)
    {
        normal = Vector3.up;
        bool now = Obst.SegPointDist(a, b, center) < orbR;
        bool enter = now && !inside; inside = now;   // AgencyMissile: once per entry
        return enter;
    }

    public override void Hit(PlayerCharacter pc, Vector3 normal)
    {
        if (pc.Invulnerable()) return;
        Hurt(pc, 51f, "ORBE!", Vector3.zero, 1f);
        Obst.Redirect(pc, pc.transform.position - center, 0.15f, 0.95f);
    }
}

// Blocked by terrain: two lasers firing at each other. Up to 400 m: one beam (51; its hitbox r 2.4). 400-850 m: an extra
// pole goes in at 40-65 % of the way. Further: no link.
public class PoleLinks : BigLaser
{
    readonly List<(Vector3 a, Vector3 b, bool aim)> links = new List<(Vector3, Vector3, bool)>();

    public static void Build(Transform parent, List<GameObject> poles, System.Random r)
    {
        var tr = Track.instance;
        var go = new GameObject("Ligações de laser"); go.transform.SetParent(parent, false);
        var s = go.AddComponent<PoleLinks>();
        var list = new List<(float prog, Vector3 head)>();
        foreach (var p in poles)
        {
            var rig = p ? p.GetComponent<PoleRig>() : null; if (!rig) continue;
            list.Add((tr.Progress(p.transform.position, out _, out _), rig.Head));
        }
        list.Sort((x, y) => x.prog.CompareTo(y.prog));
        int mask = (1 << PlayerCharacter.TerrainLayer);
        for (int k = 1; k < list.Count; k++)
        {
            var a = list[k - 1].head; var b = list[k].head; float d = Vector3.Distance(a, b);
            if (d > 850f) continue;
            if (d > 400f)
            {
                // an extra pole in between
                var m = Vector3.Lerp(a, b, Obst.Rnd(r, 0.4f, 0.65f)); var ground = m; ground.y = tr.HeightAt(m.x, m.z);
                var extra = Obst.Spawn("Pole_Modular_Fixed_Linked", parent, ground, Quaternion.LookRotation(Vector3.forward), 1f);
                if (extra)
                {
                    ObstacleSpawner.Snap(extra); PoleRig.Setup(extra, r);
                    var head = extra.GetComponent<PoleRig>().Head;
                    s.Link(a, head, mask); s.Link(head, b, mask);
                }
                continue;
            }
            s.Link(a, b, mask);
        }
        s.center = tr.PointAt(tr.Count / 2); s.reach = 1e5f;
    }

    void Link(Vector3 a, Vector3 b, int mask)
    {
        if (Physics.SphereCast(a, 3f, (b - a).normalized, out _, Vector3.Distance(a, b), mask))
        {
            links.Add((a, b, true)); AddBeam(transform, 3f);
            links.Add((b, a, true)); AddBeam(transform, 3f);
        }
        else { links.Add((a, b, false)); AddBeam(transform, 2.4f); }
    }

    void Update()
    {
        for (int k = 0; k < links.Count; k++)
        {
            var l = links[k];
            if (l.aim) Aim(k, l.a, (l.b - l.a).normalized, Vector3.Distance(l.a, l.b), -1e6f);
            else Span(k, l.a, l.b, false);
        }
    }

    public override bool Test(Vector3 a, Vector3 b, out Vector3 normal)
    {
        normal = Vector3.up;
        var pc = Obst.Player; if (!pc) return false;
        return base.Test(a, b, out normal);
    }
}

// ------------------------------------------------------------------------------------------------ laser cage orbs
public class OrbCage : Hazard
{
    MeshCollider cage; float lastHit = -10f;

    public static void Setup(GameObject go)
    {
        var t = Obst.Find(go.transform, "LaserCage");
        var s = go.AddComponent<OrbCage>();
        if (!t) return;
        var mr = t.GetComponent<MeshRenderer>();
        if (mr) { mr.enabled = true; mr.sharedMaterial = BigLaser.CoreMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
        s.cage = t.GetComponent<MeshCollider>();
        if (!s.cage) { s.cage = t.gameObject.AddComponent<MeshCollider>(); s.cage.sharedMesh = t.GetComponent<MeshFilter>().sharedMesh; }
        s.cage.enabled = true; t.gameObject.layer = Obst.HazardLayer;
        var pi = t.GetComponent<PropInfo>(); if (pi) Destroy(pi);
        var b = mr ? mr.bounds : new Bounds(t.position, Vector3.one * 100f);
        s.center = b.center; s.reach = b.extents.magnitude + 5f;
    }

    void Update() { if (cage) center = cage.bounds.center; }

    public override bool Test(Vector3 a, Vector3 b, out Vector3 normal)
    {
        normal = Vector3.up;
        if (!cage || Time.time < lastHit + 1f) return false;
        var d = b - a; float len = d.magnitude; if (len < 1e-3f) return false;
        int m = 1 << Obst.HazardLayer;
        if (Physics.SphereCast(a, 1f, d / len, out var h, len, m, QueryTriggerInteraction.Collide) && h.collider == cage) { normal = h.normal; return true; }
        if (Physics.SphereCast(b, 1f, -d / len, out h, len, m, QueryTriggerInteraction.Collide) && h.collider == cage) { normal = h.normal; return true; }
        return false;
    }

    public override void Hit(PlayerCharacter pc, Vector3 normal)
    {
        if (pc.Invulnerable()) return;
        lastHit = Time.time;
        Hurt(pc, 51f, "GAIOLA DE LASER!", Vector3.zero, 1f);
    }
}

// ------------------------------------------------------------------------------------------------ bombs
// in total (it doesn't reset) and it goes off once: 21, a shove away from it, shake; the fireball swells to ~30 m over 2 s.
public class BombAI : Hazard
{
    Transform bomb, hull, warn, ball; Material warnMat, ballMat; float stay, wind = -1f, boomT = -1f; bool inside, gone; float R;

    public static void Setup(GameObject go, System.Random r)
    {
        var s = go.AddComponent<BombAI>();
        s.bomb = Obst.Find(go.transform, "Bomb");
        s.hull = Obst.Find(go.transform, "Mo_Agency_FlyingStruct_3");
        s.R = 50f * go.transform.lossyScale.x;
        var follow = Obst.Find(go.transform, "Moving");
        if (follow) PropFix.Claim(go, follow);
        var vfx = s.bomb ? Obst.Find(s.bomb, "VFX") : null;
        s.warnMat = Obst.Warn(Obst.Orange, 0.16f);
        if (vfx && vfx.GetComponent<MeshRenderer>()) { s.warn = vfx; var mr = vfx.GetComponent<MeshRenderer>(); mr.enabled = true; mr.sharedMaterial = s.warnMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
        else { s.warn = Prim(PrimitiveType.Sphere, s.bomb ? s.bomb : go.transform, s.warnMat).transform; s.warn.localPosition = Vector3.zero; s.warn.localScale = Vector3.one * 100f; }
        s.ballMat = Obst.Glow(Obst.Orange, 2.4f, 1.2f, 1f, true);
        s.ball = Prim(PrimitiveType.Sphere, go.transform, s.ballMat).transform; s.ball.gameObject.SetActive(false);
        s.center = s.bomb ? s.bomb.position : go.transform.position; s.reach = s.R + 4f;
    }

    void Update()
    {
        if (bomb) center = bomb.position;
        if (wind >= 0f && hull)
        {
            float t = Time.time - wind;
            if (t < 0.5f) hull.Rotate(0f, 1440f / 0.5f * Time.deltaTime, 0f, Space.Self); else wind = -1f;
        }
        if (boomT >= 0f)
        {
            float t = Time.time - boomT;
            // ExplosionV3: radius 6.8 (0.017 s) -> 16 (0.13) -> 23 (0.32) -> 25 (0.5) -> 30 (2.1), gone by 3 s
            float rad = t < 0.133f ? Mathf.Lerp(6.8f, 16f, t / 0.133f) : t < 0.317f ? Mathf.Lerp(16f, 23f, (t - 0.133f) / 0.184f) : t < 0.5f ? Mathf.Lerp(23f, 25f, (t - 0.317f) / 0.183f) : Mathf.Lerp(25f, 30f, (t - 0.5f) / 1.6f);
            ball.position = center; ball.localScale = Vector3.one * rad * 2f * transform.lossyScale.x;
            ballMat.SetFloat("_Opacity", Mathf.Clamp01(1f - (t - 1.6f) / 1.4f));
            if (t > 3f) { ball.gameObject.SetActive(false); boomT = -1f; }
        }
    }

    public override bool Test(Vector3 a, Vector3 b, out Vector3 normal)
    {
        normal = Vector3.up;
        if (gone) return false;
        bool now = Obst.SegPointDist(a, b, center) < R;
        if (now && !inside) { wind = Time.time; if (Sfx.instance) Sfx.Play(Sfx.instance.countdown, 0.8f, 1.5f); }
        inside = now;
        if (now) stay += Time.fixedDeltaTime;
        if (warnMat) Obst.SetAlpha(warnMat, 0.16f + (stay > 0f ? 0.12f + Mathf.Abs(Mathf.Sin(Time.time * 30f)) * 0.12f : 0f));
        return now && stay > 0.35f;
    }

    public override void Hit(PlayerCharacter pc, Vector3 normal)
    {
        if (gone) return;
        gone = true; boomT = Time.time; ball.gameObject.SetActive(true);
        if (hull) hull.gameObject.SetActive(false);
        if (warn) warn.gameObject.SetActive(false);
        Obst.Shake(center, 20f, 500f, 0.5f);
        ComicFX.Impact(1f, 0.06f, Obst.Orange, 0.3f);
        if (Sfx.instance) Sfx.Play(Sfx.instance.wallHit, 1f, 0.6f);
        // knockback away from (bomb + 10 up), flattened against the bomb's forward axis
        var dir = pc.transform.position - (center + Vector3.up * 10f);
        var fwd = bomb ? bomb.forward : transform.forward;
        dir = Vector3.ProjectOnPlane(dir, fwd);
        float mult = Mathf.Lerp(1f, 0.25f, Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.down)));
        Hurt(pc, 21f, "BOMBA!", Vector3.zero, 1f);
        Obst.Redirect(pc, dir, 0.27f * mult * 2f, 0.95f);
    }
}
