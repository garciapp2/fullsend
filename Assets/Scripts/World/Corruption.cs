using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// inside it (Sledbonk/Corruption, a depth decal: white cracks running ahead, then the ground going dark violet cell by
// chromatic aberration. Behind the dark edge the world gives way: terrain slabs and everything on them tip over and
// drop into the void. Runtime-only; RunManager owns it and calls Set / SetDanger / Crumble.
public class Corruption : MonoBehaviour
{
    const float Outer = 450f / 320f;
    Material mat;
    Volume vol;
    ChromaticAberration ca;

    public static Corruption Build()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(go.GetComponent<Collider>());
        go.name = "Colapso";
        var mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        var src = Resources.Load<Material>("CorruptionMat");
        var c = go.AddComponent<Corruption>();
        if (src) { c.mat = new Material(src); mr.sharedMaterial = c.mat; }
        c.mat?.SetFloat("_Outer", Outer);

        var vgo = new GameObject("Colapso (tela)"); vgo.transform.SetParent(go.transform.parent, false);
        c.vol = vgo.AddComponent<Volume>(); c.vol.isGlobal = true; c.vol.priority = 20f; c.vol.weight = 0f;
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        c.ca = prof.Add<ChromaticAberration>(true); c.ca.intensity.Override(0.6f);
        c.vol.sharedProfile = prof;

        if (Camera.main) Camera.main.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
        go.SetActive(false);
        return c;
    }

    // dark radius (the kill front sits on it); the sphere is sized to the cracks' reach
    public void Set(Vector3 center, float radius)
    {
        radius = Mathf.Max(radius, 1f);
        transform.position = center;
        transform.localScale = Vector3.one * radius * Outer * 2f * 1.02f;
        if (mat) { mat.SetVector("_Center", center); mat.SetFloat("_Radius", radius); }
        MarkPlayer();
    }

    public void SetDanger(float d)
    {
        if (vol) vol.weight = Mathf.Lerp(vol.weight, d, Time.unscaledDeltaTime * 5f);
    }

    void OnDisable() { if (vol) vol.weight = 0f; }

    // ---------------------------------------------------------------- keep the decal off the player
    // The player's toon materials write stencil 1 and the decal skips it. Runs every time it's positioned, so a skin
    // swapped in the menu gets marked too (only materials not yet marked are copied).
    float markTimer;
    void MarkPlayer()
    {
        markTimer -= Time.unscaledDeltaTime;
        if (markTimer > 0f) return;
        markTimer = 0.5f;
        var pv = PlayerVisual.instance;
        if (!pv) return;
        foreach (var r in pv.GetComponentsInChildren<Renderer>(true))
        {
            var ms = r.sharedMaterials; bool changed = false;
            for (int i = 0; i < ms.Length; i++)
            {
                if (!ms[i] || !ms[i].HasProperty("_StencilRef") || ms[i].GetFloat("_StencilRef") == 1f) continue;
                ms[i] = new Material(ms[i]) { name = ms[i].name + " (jogador)" }; ms[i].SetFloat("_StencilRef", 1f); changed = true;
            }
            if (changed) r.sharedMaterials = ms;
        }
    }

    // ---------------------------------------------------------------- the world giving way
    class Piece { public Transform t; public float s; }
    class Falling { public Transform pivot; public Vector3 start; public Quaternion rot; public Vector3 axis; public float t, delay, tip, drop; }
    readonly List<Piece> pieces = new List<Piece>();
    readonly List<Falling> falling = new List<Falling>();
    int next;
    Transform collectedFor;

    // Everything the track built (terrain slabs, objects, props, hazards), ordered by how far down the road it sits.
    void Collect(Track tr, Transform root)
    {
        pieces.Clear(); falling.Clear(); next = 0; collectedFor = root;
        foreach (Transform group in root)
        {
            if (group.name == "Casa do cliente" || group.name == "Montanhas") continue;
            foreach (Transform t in group)
            {
                var rs = t.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                // goes once its far edge is behind the kill line, so the ground under the rider never drops early
                pieces.Add(new Piece { t = t, s = tr.Progress(b.center, out _, out _) + Mathf.Max(b.extents.x, b.extents.z) * 0.75f });
            }
        }
        pieces.Sort((a, b) => a.s.CompareTo(b.s));
    }

    // front = the kill line along the road; whatever is behind it goes, a slab at a time
    public void Crumble(Track tr, float front, Vector3 center)
    {
        var root = tr.transform.Find("Pista");
        if (!root) return;
        if (root != collectedFor) Collect(tr, root);
        int started = 0;
        while (next < pieces.Count && pieces[next].s < front && started < 12)
        {
            var p = pieces[next++];
            if (p.t) { Fall(p.t, center); started++; }
        }
        float dt = Time.deltaTime;
        for (int i = falling.Count - 1; i >= 0; i--)
        {
            var f = falling[i];
            if (!f.pivot) { falling.RemoveAt(i); continue; }
            f.t += dt;
            float u = f.t - f.delay;
            if (u < 0f)
            {
                // a short shudder before it lets go
                f.pivot.position = f.start + Random.insideUnitSphere * 0.25f;
                continue;
            }
            float ang = Mathf.Min(f.tip * u * u, 110f);
            f.pivot.SetPositionAndRotation(f.start + Vector3.down * (f.drop * u * u), Quaternion.AngleAxis(ang, f.axis) * f.rot);
            if (u > 9f) { f.pivot.gameObject.SetActive(false); falling.RemoveAt(i); }
        }
    }

    void Fall(Transform t, Vector3 center)
    {
        // pivot on the middle of what it shows, so a slab tips about its centre instead of the world origin
        var rs = t.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        var pivot = new GameObject("queda").transform;
        pivot.SetParent(t.parent, false); pivot.position = b.center;
        t.SetParent(pivot, true);
        foreach (var c in t.GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (var h in t.GetComponentsInChildren<Hazard>()) h.enabled = false;
        // tip over toward the void, a bit off-axis so neighbours don't move as one sheet
        var away = b.center - center; away.y = 0f;
        if (away.sqrMagnitude < 1f) away = Random.insideUnitSphere; away.y = 0f;
        var axis = Vector3.Cross(away.normalized, Vector3.up);
        axis = Quaternion.AngleAxis(Random.Range(-35f, 35f), Vector3.up) * axis;
        float size = Mathf.Max(b.extents.x, b.extents.z);
        falling.Add(new Falling
        {
            pivot = pivot, start = pivot.position, rot = pivot.rotation, axis = axis,
            delay = Random.Range(0f, 0.7f),
            tip = Random.Range(9f, 16f) * (size > 30f ? 1f : 2.2f),       // big slabs tilt slowly, props flip faster
            drop = Random.Range(5f, 9f),
        });
    }
}
