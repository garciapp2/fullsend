using System.Collections.Generic;
using UnityEngine;

// WG_SplineHeightmapModifier: plateauWidth ~50, rim ~50 with noise) and the world around it rises into
// hills and mountains. Pieces shape the road: Tiny Wings hills, kickers with long landing slopes, cliffs,
// chasms crossed by giant sliding sleds (Clustertruck), halfpipes, rails, banked turns and boost rings.
public class Track : MonoBehaviour
{
    public static Track instance;
    public enum PieceType { Start, Flow, Kicker, Cliff, Gap, Halfpipe, Rails, Banked, Rings, Finish }
    public struct Piece { public PieceType type; public int i0, i1; }

    [Header("Assets (builder)")]
    public Material terrainMat, farMat, railMat, sparkMat, ringMat, finishMat;
    public GameObject[] obstacles;
    public GameObject customerHouse;
    public GameObject pizzeria;         // the start: Fullsend Pizza stall (built by the builder)
    public Material[] customerSkins;    // penguin, duck, frog, panda (toon)
    public GameObject ringPrefab;

    [Header("Shape")]
    public float step = 2f;
    public float slope = 0.05f;
    public float rim = 50f;

    // road samples
    public readonly List<Vector3> P = new List<Vector3>();
    public readonly List<Vector3> T = new List<Vector3>(), R = new List<Vector3>();
    public readonly List<float> W = new List<float>(), Bank = new List<float>(), Wall = new List<float>(), Gap = new List<float>();
    public readonly List<Piece> pieces = new List<Piece>();
    public readonly List<int> checkpoints = new List<int>();
    public float Length => P.Count * step;
    public int Count => P.Count;
    public BiomeDef biome;

    float ox, oz;
    Vector3 bmin, bmax;
    const float Cell = 8f;
    int gw, gh; int[] near;
    Transform root;

    // runtime objects
    public readonly List<Transform> sparks = new List<Transform>();
    public readonly List<Transform> rings = new List<Transform>();
    public Transform finish;
    public int finishIndex;             // the customer's house (the level ends just in front of the customer)

    void Awake() { instance = this; }

    // =====================================================================
    public void Generate(int seed, int level, BiomeDef b)
    {
        var it = GenerateRoutine(seed, level, b, null);
        while (it.MoveNext()) { }
    }

    public System.Collections.IEnumerator GenerateRoutine(int seed, int level, BiomeDef b, System.Action<float, string> progress)
    {
        progress?.Invoke(0.02f, "TRAÇANDO A ROTA"); yield return null;
        biome = b;
        if (root) Destroy(root.gameObject);
        root = new GameObject("Pista").transform; root.SetParent(transform, false);
        P.Clear(); T.Clear(); R.Clear(); W.Clear(); Bank.Clear(); Wall.Clear(); Gap.Clear(); pieces.Clear(); checkpoints.Clear();
        sparks.Clear(); rings.Clear();
        var r = new System.Random(seed);
        ox = (float)r.NextDouble() * 900f; oz = (float)r.NextDouble() * 900f;
        BuildPath(r, level);
        ApplyHills(seed ^ 0x4111, level);
        SmoothGap();
        BuildLookup();
        progress?.Invoke(0.12f, "ESCULPINDO A MONTANHA"); yield return null;
        foreach (var f in BuildTerrainSteps()) { progress?.Invoke(0.12f + 0.5f * f, null); yield return null; }
        BuildFar();
        progress?.Invoke(0.66f, "ESPALHANDO O CENÁRIO"); yield return null;
        BuildPieceObjects(r);
        ScatterProps(r);
        finishIndex = P.Count - 26;
        progress?.Invoke(0.8f, "A AGENCY ARMANDO OS PERIGOS"); yield return null;
        BuildHazards(new System.Random(seed ^ 0x5eed), level);
        progress?.Invoke(0.95f, "ACHANDO A CASA DO CLIENTE"); yield return null;
        BuildFinish();
        // BuildPizzeria();   // off for now: the stall didn't read well (prefab kept for a redo)
    }

    // the start line: the pizzeria the deliveries leave from, beside the road
    void BuildPizzeria()
    {
        if (!pizzeria) return;
        int i = 24;   // ahead of the start line, on the left edge of the road: in view during the countdown
        var side = -R[i]; side.y = 0f;
        var p = PointAt(i, -(W[i] - 1f)); p.y = HeightAt(p.x, p.z);
        var go = Instantiate(pizzeria, p, Quaternion.LookRotation(-side, Vector3.up), root);
        go.name = "Pizzaria";
    }

    // ---------------------------------------------------------------- path
    float yaw, baseY; Vector3 cur;

    void BuildPath(System.Random r, int level)
    {
        yaw = 0f; baseY = 0f; cur = Vector3.zero;
        float diff = Mathf.Clamp01(level / 4f);
        Emit(PieceType.Start, 110f, 30f, u => 0f, 0f);
        int count = 16 + Mathf.Min(level, 6) * 2;   // ~4.5 km to start, ~8 km by the 5th delivery
        PieceType last = PieceType.Start;
        for (int k = 0; k < count; k++)
        {
            var type = Pick(r, diff, last);
            last = type;
            float R01() => (float)r.NextDouble();
            float turn = TurnFor(r, 10f + R01() * 35f);
            switch (type)
            {
                case PieceType.Flow:
                {
                    float L = 260f + R01() * 160f, A = 4f + R01() * 7f + diff * 4f;
                    int waves = Mathf.Max(2, Mathf.RoundToInt(L / (70f + R01() * 50f)));
                    Emit(type, L, 26f + R01() * 12f, u => A * Mathf.Sin(u * Mathf.PI * 2f * waves), turn);
                    break;
                }
                case PieceType.Kicker:
                {
                    float L = 240f + R01() * 80f, H = 6f + R01() * 3f, D = H + 18f + R01() * 22f + diff * 10f;
                    const float a = 0.18f, lip = 0.3f;
                    Emit(type, L, 24f, u =>
                    {
                        if (u < a) return 0f;
                        if (u < lip) { float t = (u - a) / (lip - a); return H * t * t; }
                        float s = Mathf.Clamp01((u - lip) / (1f - lip)); return H - D * s * s * (3f - 2f * s);
                    }, turn * 0.3f);
                    break;
                }
                case PieceType.Cliff:
                {
                    float L = 240f + R01() * 60f, D = 28f + R01() * 25f + diff * 15f;
                    Emit(type, L, 30f, u =>
                    {
                        float t = Mathf.Clamp01((u - 0.35f) / 0.45f); return -D * (1f - (1f - t) * (1f - t) * (1f - t));
                    }, turn * 0.4f);
                    break;
                }
                case PieceType.Gap:
                {
                    // lower so a jump at speed lands on a down slope (perfect), a slow one falls short.
                    float gapLen = 50f + R01() * 25f + diff * 25f, run = 120f, L = run + gapLen + 220f, H = 5f, drop = 12f;
                    float g0 = run / L, g1 = (run + gapLen) / L;
                    Emit(type, L, 30f, u =>
                    {
                        if (u < g0 - 0.12f) return 0f;
                        if (u < g0) { float t = (u - (g0 - 0.12f)) / 0.12f; return H * t * t; }
                        if (u < g1) return H;
                        float s = Mathf.Clamp01((u - g1) / (1f - g1)); return H - drop - 10f * s * s * (3f - 2f * s);
                    }, 0f, gapFn: u => u > g0 && u < g1);
                    break;
                }
                case PieceType.Halfpipe:
                {
                    float L = 280f + R01() * 80f;
                    Emit(type, L, 22f, u => 0f, turn, wallFn: u => 13f * Mathf.Clamp01(Mathf.Min(u, 1f - u) * 8f));
                    break;
                }
                case PieceType.Rails:
                {
                    float L = 220f + R01() * 60f;
                    Emit(type, L, 30f, u => 2.5f * Mathf.Sin(u * Mathf.PI * 2f), turn * 0.3f);
                    break;
                }
                case PieceType.Banked:
                {
                    float big = TurnFor(r, 70f + R01() * 40f), L = 280f + R01() * 60f;
                    float bankMax = Mathf.Deg2Rad * (16f + R01() * 10f) * -Mathf.Sign(big);
                    Emit(type, L, 30f, u => 0f, big, bankFn: u => bankMax * Mathf.Sin(u * Mathf.PI));
                    break;
                }
                case PieceType.Rings:
                {
                    float L = 260f + R01() * 80f, A = 7f + R01() * 5f;
                    Emit(type, L, 28f, u => A * Mathf.Sin(u * Mathf.PI * 3f), turn);
                    break;
                }
            }
        }
        Emit(PieceType.Finish, 160f, 34f, u => 0f, 0f);
    }

    static PieceType Pick(System.Random r, float diff, PieceType last)
    {
        var w = new (PieceType t, float w)[] {
            (PieceType.Flow, 1.4f), (PieceType.Kicker, 1.3f), (PieceType.Rails, 0.9f), (PieceType.Rings, 0.7f),
            (PieceType.Banked, 0.8f), (PieceType.Halfpipe, 0.7f), (PieceType.Cliff, 0.5f + diff), (PieceType.Gap, 0.35f + diff * 0.9f) };
        for (int tries = 0; tries < 10; tries++)
        {
            float sum = 0f; foreach (var x in w) sum += x.w;
            float v = (float)r.NextDouble() * sum;
            foreach (var x in w) { v -= x.w; if (v <= 0f) { if (x.t != last || x.t == PieceType.Flow) return x.t; break; } }
        }
        return PieceType.Flow;
    }

    // turns always steer back toward "downhill" (+Z), so the road never folds back over itself
    float TurnFor(System.Random r, float amount)
    {
        float yd = yaw * Mathf.Rad2Deg;
        float sign = Mathf.Abs(yd) > 25f ? -Mathf.Sign(yd) : (r.NextDouble() < 0.5 ? -1f : 1f);
        return Mathf.Clamp(yd + sign * amount, -70f, 70f) - yd;
    }

    void Emit(PieceType type, float L, float hw, System.Func<float, float> elev, float turnDeg,
              System.Func<float, float> bankFn = null, System.Func<float, float> wallFn = null, System.Func<float, bool> gapFn = null)
    {
        int n = Mathf.Max(2, Mathf.CeilToInt(L / step));
        int i0 = P.Count;
        if (type != PieceType.Gap) checkpoints.Add(i0);
        float rate = turnDeg * Mathf.Deg2Rad / (n * step);
        for (int j = 0; j < n; j++)
        {
            float u = j / (float)n;
            float sn = Mathf.Sin(u * Mathf.PI);
            yaw += rate * 2f * sn * sn * step;
            var dir = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            cur += dir * step;
            baseY -= slope * step;
            P.Add(new Vector3(cur.x, baseY + elev(u), cur.z));
            T.Add(dir); R.Add(new Vector3(dir.z, 0f, -dir.x));
            // widths blend between pieces so the edges never jump
            float edge = Mathf.Clamp01(Mathf.Min(u, 1f - u) * 6f);
            W.Add(Mathf.Lerp(28f, hw, edge));
            Bank.Add(bankFn != null ? bankFn(u) : 0f);
            Wall.Add(wallFn != null ? wallFn(u) : 0f);
            Gap.Add(gapFn != null && gapFn(u) ? 1f : 0f);
        }
        baseY += elev(1f);
        pieces.Add(new Piece { type = type, i0 = i0, i1 = P.Count - 1 });
    }

    void SmoothGap()
    {
        // soften gap edges over a few samples so the chasm walls are steep but not vertical
        var g = Gap.ToArray();
        for (int i = 0; i < g.Length; i++)
        {
            if (Gap[i] > 0f) continue;
            for (int k = 1; k <= 3; k++)
                if (i - k >= 0 && Gap[i - k] > 0f || i + k < g.Length && Gap[i + k] > 0f) { g[i] = 1f - k / 4f; break; }
        }
        for (int i = 0; i < g.Length; i++) Gap[i] = g[i];
    }

    // ---------------------------------------------------------------- lookup
    void BuildLookup()
    {
        bmin = new Vector3(float.MaxValue, 0f, float.MaxValue); bmax = new Vector3(float.MinValue, 0f, float.MinValue);
        foreach (var p in P) { bmin = Vector3.Min(bmin, p); bmax = Vector3.Max(bmax, p); }
        bmin -= new Vector3(520f, 0f, 520f); bmax += new Vector3(520f, 0f, 520f);
        gw = Mathf.CeilToInt((bmax.x - bmin.x) / Cell) + 1; gh = Mathf.CeilToInt((bmax.z - bmin.z) / Cell) + 1;
        near = new int[gw * gh];
        // coarse pass: nearest of every 4th sample, then each cell refines locally later
        for (int gz = 0; gz < gh; gz++)
            for (int gx = 0; gx < gw; gx++)
            {
                float x = bmin.x + gx * Cell, z = bmin.z + gz * Cell;
                float bd = float.MaxValue; int bi = 0;
                for (int i = 0; i < P.Count; i += 4)
                {
                    float dx = P[i].x - x, dz = P[i].z - z; float d = dx * dx + dz * dz;
                    if (d < bd) { bd = d; bi = i; }
                }
                near[gz * gw + gx] = bi;
            }
    }

    public int Nearest(float x, float z, out float lat, out float along)
    {
        int gx = Mathf.Clamp(Mathf.RoundToInt((x - bmin.x) / Cell), 0, gw - 1), gz = Mathf.Clamp(Mathf.RoundToInt((z - bmin.z) / Cell), 0, gh - 1);
        int c = near[gz * gw + gx];
        float bd = float.MaxValue; int bi = c;
        for (int i = Mathf.Max(0, c - 10); i <= Mathf.Min(P.Count - 1, c + 10); i++)
        {
            float dx = P[i].x - x, dz = P[i].z - z; float d = dx * dx + dz * dz;
            if (d < bd) { bd = d; bi = i; }
        }
        var off = new Vector3(x - P[bi].x, 0f, z - P[bi].z);
        lat = Vector3.Dot(off, R[bi]); along = Vector3.Dot(off, T[bi]);
        return bi;
    }

    // road surface (with banking and halfpipe walls) at sample i, lateral offset lat
    public float RoadHeight(int i, float lat)
    {
        float hw = W[i];
        float h = P[i].y + lat * Mathf.Tan(Bank[i]);
        if (Wall[i] > 0f) { float a = Mathf.Min(Mathf.Abs(lat) / hw, 1.3f); h += Wall[i] * Mathf.Pow(a, 2.4f); }
        return h;
    }

    public float HeightAt(float x, float z) => HeightAt(x, z, out _, out _);

    public float HeightAt(float x, float z, out float roadW, out int idx)
    {
        idx = Nearest(x, z, out float lat, out float along);
        int i = idx;
        float alat = Mathf.Abs(lat), hw = W[i];
        // past either end of the road the world closes around it (no plateau running off forever)
        if (i == 0 && along < 0f || i == P.Count - 1 && along > 0f) alat = Mathf.Sqrt(lat * lat + along * along);
        float roadY = RoadHeight(i, Mathf.Clamp(lat, -hw * 1.3f, hw * 1.3f));
        float rimN = Mathf.PerlinNoise(ox + x / 70f, oz + z / 70f);
        float t = (alat - hw) / (rim * (0.6f + rimN * 0.8f));
        roadW = alat <= hw ? 1f : 1f - Smooth01(t);
        // world around the road: low bumps near it, mountains and valleys further out
        float far = Smooth01((alat - hw) / 420f);
        float n1 = Mathf.PerlinNoise(ox + x / 280f, oz + z / 280f), n2 = Mathf.PerlinNoise(ox + 31f + x / 95f, oz + z / 95f), n3 = Mathf.PerlinNoise(ox + 77f + x / 24f, oz + z / 24f);
        float ridge = 1f - Mathf.Abs(n2 * 2f - 1f);
        float amp = Mathf.Lerp(7f, 170f, far) + Smooth01((alat - 600f) / 2200f) * 420f;
        float terr = roadY + 3f + amp * (n1 * 0.8f + ridge * 0.35f - 0.42f) + (n3 - 0.5f) * 3f;
        float h = Mathf.Lerp(terr, roadY, roadW);
        // chasm across the road
        float g = Gap[i];
        if (g > 0f)
        {
            float side = 1f - Smooth01((alat - hw - 60f) / 50f);
            h = Mathf.Lerp(h, P[i].y - 170f, g * side);
            if (side > 0.5f) roadW *= 1f - g;
        }
        return h;
    }

    static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    public Vector3 NormalAt(float x, float z)
    {
        float e = 1.5f;
        float hx = HeightAt(x + e, z) - HeightAt(x - e, z), hz = HeightAt(x, z + e) - HeightAt(x, z - e);
        return new Vector3(-hx, 2f * e, -hz).normalized;
    }

    // progress along the road for a world position (metres)
    public float Progress(Vector3 p, out int idx, out float lat)
    {
        idx = Nearest(p.x, p.z, out lat, out float along);
        return Mathf.Clamp(idx * step + along, 0f, Length);
    }

    public Vector3 PointAt(int i, float lat = 0f) => new Vector3(P[i].x, RoadHeight(i, lat), P[i].z) + R[i] * lat;

    // ---------------------------------------------------------------- terrain
    void BuildTerrain() { foreach (var _ in BuildTerrainSteps()) { } }

    // the terrain chunk by chunk, handing back its progress every ~25 ms so a loading screen keeps moving
    System.Collections.Generic.IEnumerable<float> BuildTerrainSteps()
    {
        const float chunk = 96f; const int res = 33; float st = chunk / (res - 1);
        int nx = Mathf.CeilToInt((bmax.x - bmin.x) / chunk), nz = Mathf.CeilToInt((bmax.z - bmin.z) / chunk);
        var terr = new GameObject("Terreno").transform; terr.SetParent(root, false);
        var sw = System.Diagnostics.Stopwatch.StartNew(); int done = 0, total = Mathf.Max(1, nx * nz);
        for (int cx = 0; cx < nx; cx++)
            for (int cz = 0; cz < nz; cz++)
            {
                done++;
                float x0 = bmin.x + cx * chunk, z0 = bmin.z + cz * chunk;
                Nearest(x0 + chunk / 2f, z0 + chunk / 2f, out float lat, out _);
                if (Mathf.Abs(lat) > 430f) continue;
                BuildChunk(terr, x0, z0, res, st);
                if (sw.ElapsedMilliseconds > 25) { sw.Restart(); yield return done / (float)total; }
            }
    }

    void BuildChunk(Transform parent, float x0, float z0, int n, float st)
    {
        int m = n + 2;   // one extra ring for normals
        var hs = new float[m * m]; var ws = new float[m * m];
        for (int j = 0; j < m; j++)
            for (int i = 0; i < m; i++)
            {
                hs[j * m + i] = HeightAt(x0 + (i - 1) * st, z0 + (j - 1) * st, out float rw, out _);
                ws[j * m + i] = rw;
            }
        var verts = new Vector3[n * n]; var cols = new Color[n * n]; var uvs = new Vector2[n * n];
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int k = (j + 1) * m + (i + 1);
                float h = hs[k];
                float dx = hs[k + 1] - hs[k - 1], dz = hs[k + m] - hs[k - m];
                var nrm = new Vector3(-dx, 2f * st, -dz).normalized;
                float x = x0 + i * st, z = z0 + j * st;
                verts[j * n + i] = new Vector3(x, h, z);
                uvs[j * n + i] = new Vector2(x / 10f, z / 10f);
                cols[j * n + i] = TerrainColor(x, z, h, nrm.y, ws[k]);
            }
        var tris = new int[(n - 1) * (n - 1) * 6]; int t = 0;
        for (int j = 0; j < n - 1; j++)
            for (int i = 0; i < n - 1; i++)
            {
                int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                tris[t++] = a; tris[t++] = c; tris[t++] = b; tris[t++] = b; tris[t++] = c; tris[t++] = d;
            }
        var mesh = new Mesh { name = "terrain" };
        mesh.vertices = verts; mesh.colors = cols; mesh.uv = uvs; mesh.triangles = tris;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var go = new GameObject("chunk"); go.layer = PlayerCharacter.TerrainLayer;
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var tmr = go.AddComponent<MeshRenderer>(); tmr.sharedMaterial = terrainMat;
        tmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // no terrain self-shadow acne; props still cast onto it
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    // flat colours with hard steps (comic): road / ground with a second patch shade, slope, cliff
    Color TerrainColor(float x, float z, float h, float ny, float roadW)
    {
        var b = biome;
        float steep = 1f - ny;
        float n = Mathf.PerlinNoise(x / 38f + ox, z / 38f + oz);
        Color c = steep > 0.42f ? b.cliff : steep > 0.2f ? b.slope : (roadW > 0.55f ? b.road : (n > 0.62f ? Color.Lerp(b.flat, b.slope, 0.35f) : b.flat));
        c.a = 1f;
        return c;
    }

    void BuildFar()
    {
        const float st = 40f; float ext = 2600f;
        float x0 = bmin.x - ext, z0 = bmin.z - ext, x1 = bmax.x + ext, z1 = bmax.z + ext;
        int nx = Mathf.CeilToInt((x1 - x0) / st) + 1, nz = Mathf.CeilToInt((z1 - z0) / st) + 1;
        var verts = new Vector3[nx * nz]; var cols = new Color[nx * nz];
        for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                float x = x0 + i * st, z = z0 + j * st;
                // the far ring only shows past the detailed terrain (chunks reach ~430-480 m from the road): under
                // it, sink it deep, or its coarse 40 m triangles poke through halfpipe walls and hills
                Nearest(x, z, out float flat, out _);
                float under = 1f - Smooth01((Mathf.Abs(flat) - 340f) / 90f);
                float h = HeightAt(x, z, out float rw, out _) - 4f - rw * 6f - under * 120f;
                verts[j * nx + i] = new Vector3(x, h, z);
                float hn = Mathf.InverseLerp(-300f, 500f, h - P[P.Count / 2].y);
                cols[j * nx + i] = hn > 0.62f ? biome.flat : biome.far;
            }
        var tris = new List<int>((nx - 1) * (nz - 1) * 6);
        for (int j = 0; j < nz - 1; j++)
            for (int i = 0; i < nx - 1; i++)
            {
                int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d);
            }
        var mesh = new Mesh { name = "far", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = verts; mesh.colors = cols; mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var go = new GameObject("Montanhas"); go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = farMat ? farMat : terrainMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ---------------------------------------------------------------- piece objects
    void BuildPieceObjects(System.Random r)
    {
        var objs = new GameObject("Objetos").transform; objs.SetParent(root, false);
        foreach (var pc in pieces)
        {
            switch (pc.type)
            {
                case PieceType.Rails: BuildRails(objs, pc, r); break;
                case PieceType.Gap: BuildGapArc(objs, pc); break;
                case PieceType.Rings: BuildRingLine(objs, pc, r, 5); break;
                case PieceType.Kicker: if (r.NextDouble() < 0.6) BuildKickerRing(objs, pc); break;
            }
            if (pc.type != PieceType.Gap && pc.type != PieceType.Start && pc.type != PieceType.Finish) BuildSparkLine(objs, pc, r);
        }
    }

    void BuildRails(Transform parent, Piece pc, System.Random r)
    {
        int lanes = 2 + r.Next(2);
        for (int l = 0; l < lanes; l++)
        {
            float lat = (l - (lanes - 1) * 0.5f) * 11f + ((float)r.NextDouble() - 0.5f) * 3f;
            int a = pc.i0 + 15 + r.Next(15), b = pc.i1 - 10 - r.Next(25);
            if (b - a < 20) continue;
            var go = new GameObject("Trilho"); go.transform.SetParent(parent, false);
            var rail = go.AddComponent<Rail>();
            float lift = 1.1f + (float)r.NextDouble() * 1.5f;
            for (int i = a; i <= b; i += 2)
            {
                float rise = Mathf.Sin((i - a) / (float)(b - a) * Mathf.PI) * (l % 2 == 0 ? 2.5f : 0f);
                rail.points.Add(PointAt(i, lat) + Vector3.up * (lift + rise));
            }
            rail.Bake();
            TubeMesh(go, rail.points, 0.28f, railMat);
            // posts
            for (int k = 0; k < rail.points.Count; k += 6)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(post.GetComponent<Collider>());
                post.transform.SetParent(go.transform, false);
                var p = rail.points[k]; float ground = HeightAt(p.x, p.z);
                post.transform.position = new Vector3(p.x, (p.y + ground) * 0.5f, p.z);
                post.transform.localScale = new Vector3(0.18f, Mathf.Max(0.1f, (p.y - ground) * 0.5f), 0.18f);
                post.GetComponent<Renderer>().sharedMaterial = railMat;
            }
            // sparks along the rail
            for (int k = 0; k < rail.points.Count; k += 3) AddSpark(parent, rail.points[k] + Vector3.up * 1.4f);
        }
    }

    static void TubeMesh(GameObject go, List<Vector3> pts, float radius, Material mat)
    {
        const int sides = 8;
        var v = new List<Vector3>(); var tri = new List<int>();
        for (int i = 0; i < pts.Count; i++)
        {
            var fwd = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
            var right = Vector3.Cross(Vector3.up, fwd).normalized; var up = Vector3.Cross(fwd, right);
            for (int s = 0; s < sides; s++)
            {
                float a = s / (float)sides * Mathf.PI * 2f;
                v.Add(go.transform.InverseTransformPoint(pts[i] + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius));
            }
            if (i == 0) continue;
            int b0 = (i - 1) * sides, b1 = i * sides;
            for (int s = 0; s < sides; s++)
            {
                int s1 = (s + 1) % sides;
                tri.Add(b0 + s); tri.Add(b1 + s); tri.Add(b0 + s1); tri.Add(b0 + s1); tri.Add(b1 + s); tri.Add(b1 + s1);
            }
        }
        var m = new Mesh { name = "rail" }; m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals();
        go.AddComponent<MeshFilter>().sharedMesh = m; go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // sparks along the arc you should fly over the chasm, and a boost ring at the lip
    void BuildGapArc(Transform parent, Piece pc)
    {
        int lip = -1;
        for (int i = pc.i0; i <= pc.i1; i++) if (Gap[i] >= 1f) { lip = i - 1; break; }
        if (lip < 0) return;
        AddRing(parent, PointAt(lip) + Vector3.up * 4f, T[lip], 1.3f);
        for (float t = 0.1f; t < 3f; t += 0.1f)
        {
            var p = Simulate(lip, 85f, t);
            if (p.y < HeightAt(p.x, p.z) + 1.5f) break;
            AddSpark(parent, p);
        }
    }

    void BuildRingLine(Transform parent, Piece pc, System.Random r, int count)
    {
        for (int k = 0; k < count; k++)
        {
            int i = Mathf.RoundToInt(Mathf.Lerp(pc.i0 + 20, pc.i1 - 20, k / (float)(count - 1)));
            float lat = Mathf.Sin(k * 1.3f) * 9f;
            AddRing(parent, PointAt(i, lat) + Vector3.up * (5f + (float)r.NextDouble() * 6f), T[i], 1f);
        }
    }

    void BuildKickerRing(Transform parent, Piece pc)
    {
        // a ring at the top of the arc off the lip
        int lip = pc.i0 + Mathf.RoundToInt((pc.i1 - pc.i0) * 0.3f);
        var p = Simulate(lip, 85f, 1.0f);
        AddRing(parent, p, T[lip], 1.5f);
    }

    Vector3 Simulate(int i, float v, float t)
    {
        int j = Mathf.Min(i + 2, P.Count - 1);
        var dir = (P[j] - P[i]).normalized;
        return P[i] + dir * v * t + Vector3.down * 11f * t * t + Vector3.up * 1f;
    }

    void AddRing(Transform parent, Vector3 pos, Vector3 fwd, float scale)
    {
        GameObject go;
        if (ringPrefab) go = Instantiate(ringPrefab, parent);
        else
        {
            go = new GameObject("Anel"); go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = TorusMesh(5f, 0.45f);
            go.AddComponent<MeshRenderer>().sharedMaterial = ringMat;
        }
        go.name = "Anel de impulso";
        go.transform.position = pos; go.transform.rotation = Quaternion.LookRotation(fwd); go.transform.localScale *= scale;
        rings.Add(go.transform);
    }

    public void SpawnRing(Vector3 pos, Vector3 fwd) => AddRing(root, pos, fwd, 1f);
    public void SpawnRing(Vector3 pos, Vector3 fwd, float scale, Transform parent) => AddRing(parent ? parent : root, pos, fwd, scale);

    public GameObject Obstacle(string name)
    {
        if (obstacles != null) foreach (var o in obstacles) if (o && o.name == name) return o;
        return null;
    }

    static Mesh torus;
    public static Mesh TorusMesh(float R, float r)
    {
        if (torus) return torus;
        const int a = 48, b = 10; var v = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i <= a; i++)
            for (int j = 0; j <= b; j++)
            {
                float u = i / (float)a * Mathf.PI * 2f, w = j / (float)b * Mathf.PI * 2f;
                v.Add(new Vector3((R + r * Mathf.Cos(w)) * Mathf.Cos(u), (R + r * Mathf.Cos(w)) * Mathf.Sin(u), r * Mathf.Sin(w)));
                if (i < a && j < b) { int k = i * (b + 1) + j; t.Add(k); t.Add(k + b + 1); t.Add(k + 1); t.Add(k + 1); t.Add(k + b + 1); t.Add(k + b + 2); }
            }
        torus = new Mesh { name = "torus" }; torus.SetVertices(v); torus.SetTriangles(t, 0); torus.RecalculateNormals();
        return torus;
    }

    void BuildSparkLine(Transform parent, Piece pc, System.Random r)
    {
        float phase = (float)r.NextDouble() * 6f, ampL = 6f + (float)r.NextDouble() * 8f;
        if (pc.type == PieceType.Kicker)
        {
            int lip = pc.i0 + Mathf.RoundToInt((pc.i1 - pc.i0) * 0.3f);
            for (int i = pc.i0 + 10; i < lip; i += 5) AddSpark(parent, PointAt(i) + Vector3.up * 1.2f);
            for (float t = 0.15f; t < 3.5f; t += 0.12f)
            {
                var p = Simulate(lip, 85f, t);
                if (p.y < HeightAt(p.x, p.z) + 1.5f) break;
                AddSpark(parent, p);
            }
            return;
        }
        if (r.NextDouble() < 0.35) return;
        int start = pc.i0 + r.Next(10, 40), len = 30 + r.Next(40);
        for (int i = start; i < Mathf.Min(pc.i1, start + len); i += 3)
        {
            float lat = Mathf.Sin(i * 0.05f + phase) * ampL;
            AddSpark(parent, PointAt(i, lat) + Vector3.up * 1.2f);
        }
    }

    static Mesh sparkMesh;
    void AddSpark(Transform parent, Vector3 pos)
    {
        if (!sparkMesh)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere); sparkMesh = tmp.GetComponent<MeshFilter>().sharedMesh; Destroy(tmp);
        }
        var go = new GameObject("faísca"); go.transform.SetParent(parent, false);
        go.transform.position = pos; go.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
        go.AddComponent<MeshFilter>().sharedMesh = sparkMesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = sparkMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sparks.Add(go.transform);
    }

    // ---------------------------------------------------------------- props
    void ScatterProps(System.Random r)
    {
        var props = new GameObject("Props").transform; props.SetParent(root, false);
        var b = biome; if (b == null) return;
        for (int i = 0; i < P.Count; i += 4)
        {
            if (Gap[i] > 0f) continue;
            float hw = W[i];
            if (b.small != null && b.small.Length > 0 && r.NextDouble() < 0.55)
                Place(props, b.small, i, hw + 3f + (float)r.NextDouble() * 35f, 0.8f + (float)r.NextDouble() * 0.7f, r);
            if (b.medium != null && b.medium.Length > 0 && i % 12 == 0 && r.NextDouble() < 0.7)
                Place(props, b.medium, i, hw + 15f + (float)r.NextDouble() * 110f, 0.9f + (float)r.NextDouble() * 0.9f, r);
            if (b.landmarks != null && b.landmarks.Length > 0 && i % 40 == 0)
                Place(props, b.landmarks, i, hw + 90f + (float)r.NextDouble() * 320f, 1.6f + (float)r.NextDouble() * 2.4f, r);
        }
    }

    void Place(Transform parent, GameObject[] list, int i, float dist, float scale, System.Random r)
    {
        var prefab = list[r.Next(list.Length)]; if (!prefab) return;
        if (i > P.Count - 75 && dist < 130f) return;   // keep the delivery address clear
        float side = r.NextDouble() < 0.5 ? -1f : 1f;
        var p = P[i] + R[i] * (dist * side) + T[i] * (((float)r.NextDouble() - 0.5f) * 8f);
        float h = HeightAt(p.x, p.z, out float rw, out _);
        if (rw > 0.25f) return;
        var nrm = NormalAt(p.x, p.z);
        if (nrm.y < 0.55f) return;
        var rot = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, nrm), 0.15f) * Quaternion.Euler(0f, (float)r.NextDouble() * 360f, 0f);
        var go = Instantiate(prefab, new Vector3(p.x, h - 0.4f, p.z), rot, parent);
        go.transform.localScale *= scale * biome.propScale;
        if (dist < W[i] + 160f) PropInfo.Setup(go);
    }

    // ---------------------------------------------------------------- hazards
    // (LaserSpinners 4, LaserRing 5, Laser_Flying 6; spikes 2). Pieces keep their distance (KeyProp collisionSize).
    public string lastPresets = "";

    void BuildHazards(System.Random r, int level)
    {
        var parent = new GameObject("Perigos").transform; parent.SetParent(root, false);
        PropFields(parent, r, level);
        float d = Mathf.Clamp01(level / 4f);
        bool infinito = RunManager.mode == RunMode.Infinito;
        float budget = Mathf.Lerp(10f, 30f, d) + (infinito ? Mathf.Max(0, level - 4) * 3f : 0f);
        lastPresets = ObstacleSpawner.Build(this, parent, r, level, budget);
    }

    void PropFields(Transform parent, System.Random r, int level)
    {
        var b = biome; if (b == null) return;
        var rocks = new List<GameObject>(); var solids = new List<GameObject>(); var breaks = new List<GameObject>();
        foreach (var list in new[] { b.small, b.medium })
            if (list != null)
                foreach (var pf in list)
                {
                    if (!pf) continue;
                    var k = PropInfo.Classify(pf.name);
                    if (k == PropInfo.Kind.Runnable) rocks.Add(pf); else if (k == PropInfo.Kind.Solid) solids.Add(pf); else if (k == PropInfo.Kind.Breakable) breaks.Add(pf);
                }
        float thr = Mathf.Lerp(0.27f, 0.36f, Mathf.Clamp01(level / 4f));
        foreach (var pc in pieces)
        {
            if (pc.type != PieceType.Flow && pc.type != PieceType.Banked && pc.type != PieceType.Rings && pc.type != PieceType.Rails) continue;
            for (int i = pc.i0 + 15; i < pc.i1 - 15; i += 4)
            {
                if (Gap[i] > 0f) continue;
                float hw = W[i] - 2f;
                for (float lat = -hw; lat <= hw; lat += 8f)
                {
                    var p = PointAt(i, lat + ((float)r.NextDouble() - 0.5f) * 5f);
                    float n = Mathf.PerlinNoise(ox + 300f + p.x / 150f, oz + 300f + p.z / 150f);
                    if (n < thr)
                    {
                        if (r.NextDouble() > 0.5) continue;
                        bool tree = solids.Count > 0 && (rocks.Count == 0 || r.NextDouble() < 0.3);
                        var list = tree ? solids : rocks; if (list.Count == 0) continue;
                        SpawnRoadProp(parent, list[r.Next(list.Count)], p, 0.7f + (float)r.NextDouble() * 0.7f, r);
                    }
                    else if (breaks.Count > 0 && n > 0.6f && n < 0.635f && r.NextDouble() < 0.6)
                        SpawnRoadProp(parent, breaks[r.Next(breaks.Count)], p, 0.9f + (float)r.NextDouble() * 0.4f, r);
                }
            }
        }
    }

    void SpawnRoadProp(Transform parent, GameObject prefab, Vector3 p, float scale, System.Random r)
    {
        var nrm = NormalAt(p.x, p.z);
        var rot = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, nrm), 0.5f) * Quaternion.Euler(0f, (float)r.NextDouble() * 360f, 0f);
        var go = Instantiate(prefab, p - Vector3.up * 0.3f, rot, parent);
        go.transform.localScale *= scale * biome.propScale;
        // a set piece may not wall the road off: cap its footprint at a third of the road width
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            var bb = rs[0].bounds; foreach (var rr in rs) bb.Encapsulate(rr.bounds);
            float foot = Mathf.Max(bb.size.x, bb.size.z), cap = W[0] * 0.66f;
            if (foot > cap) go.transform.localScale *= cap / foot;
        }
        PropInfo.Setup(go);
    }

    // Bumps 15): the back slopes are what make a landing Bad. Our road keeps a little downhill and gets hills along
    // its length, toned down on pieces whose shape matters (kickers, cliffs, gaps), off at start and finish.
    void ApplyHills(int seed, int level)
    {
        var r = new System.Random(seed);
        float sx = (float)r.NextDouble() * 500f, sy = (float)r.NextDouble() * 500f;
        int n = P.Count; var m = new float[n];
        foreach (var pc in pieces)
        {
            float k;
            switch (pc.type)
            {
                case PieceType.Flow: case PieceType.Rings: k = 1f; break;
                case PieceType.Banked: k = 0.7f; break;
                case PieceType.Rails: k = 0.6f; break;
                case PieceType.Halfpipe: k = 0.4f; break;
                case PieceType.Kicker: case PieceType.Cliff: k = 0.35f; break;
                case PieceType.Gap: k = 0.15f; break;
                default: k = 0f; break;
            }
            for (int i = pc.i0; i <= pc.i1; i++) m[i] = k;
        }
        // blend between pieces: two box passes of +-40 samples (80 m)
        for (int pass = 0; pass < 2; pass++)
        {
            var pre = new float[n + 1];
            for (int i = 0; i < n; i++) pre[i + 1] = pre[i] + m[i];
            for (int i = 0; i < n; i++) { int lo = Mathf.Max(0, i - 40), hi = Mathf.Min(n, i + 41); m[i] = (pre[hi] - pre[lo]) / (hi - lo); }
        }
        float amp = Mathf.Lerp(0.8f, 1.2f, Mathf.Clamp01(level / 4f));
        float h0 = 0f;
        for (int i = 0; i < n; i++)
        {
            float d = i * step;
            float h = (Mathf.PerlinNoise(sx + d / 520f, sy) - 0.5f) * 2f * 34f
                    + (Mathf.PerlinNoise(sx + 40f + d / 170f, sy + 13f) - 0.5f) * 2f * 15f
                    + (Mathf.PerlinNoise(sx + 90f + d / 55f, sy + 27f) - 0.5f) * 2f * 4f;
            if (i == 0) h0 = h;
            P[i] += Vector3.up * (h - h0) * m[i] * amp;
        }
    }

    // the delivery address: the customer's hut with its smoke signal (replaces the old portal and beam)
    void BuildFinish()
    {
        finishIndex = P.Count - 26;
        finish = CustomerPlace.Build(this, root, finishIndex).transform;
    }

    public void Respawn(PlayerCharacter pc, float progress)
    {
        int best = checkpoints[0];
        foreach (var c in checkpoints) if (c * step <= progress) best = c;
        int i = Mathf.Min(best + 3, P.Count - 2);
        var pos = PointAt(i) + Vector3.up * 2f;
        pc.rig.position = pos; pc.transform.position = pos; pc.data.lastPos = pos;
        pc.rig.linearVelocity = T[i] * 70f;
        pc.SetLookDirection(T[i]);
        Physics.SyncTransforms();
    }
}
