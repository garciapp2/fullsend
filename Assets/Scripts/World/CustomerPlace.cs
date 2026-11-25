using System.Collections.Generic;
using UnityEngine;

// palette, the customer waiting at the foot of it (the game's own character with another animal's body and skin),
// warm lights around it and a smoke signal from the chimney that reads from anywhere on the course. A small camera
// renders the customer's face for the ENTREGUE! screen.
public class CustomerPlace : MonoBehaviour
{
    public static CustomerPlace instance;
    public Camera portraitCam;
    public RenderTexture portraitRT;
    public string customerName = "CLIENTE";
    Animator anim;
    int mood = -1;      // emote for the delivery screen; -1 while waiting

    const int Wave = 0, Dance2 = 1, Dance1 = 2, Sad = 5, Happy = 6;

    struct Look { public string biome, name; public string[] parts; public int skin; }
    static readonly Look[] Looks =
    {
        new Look { biome = "Neve", name = "SR. PINGUIM", parts = new[] { "arms.001", "beak", "body.001", "face", "feet" }, skin = 0 },
        new Look { biome = "Floresta", name = "DONA PATA", parts = new[] { "duck_arms", "duck_beak", "duck_body", "duck_eyes", "duck_legs" }, skin = 1 },
        new Look { biome = "Deserto", name = "SR. SAPO", parts = new[] { "frog_arms", "frog_body", "frog_eyes", "frog_legs", "frog_mouth" }, skin = 2 },
        new Look { biome = "Etéreo", name = "DONA PANDA", parts = new[] { "panda_arms", "panda_body", "panda_ears", "panda_eyes", "panda_legs", "panda_nose" }, skin = 3 },
    };

    public static CustomerPlace Build(Track tr, Transform parent, int i)
    {
        var fwd = -tr.T[i]; fwd.y = 0f; fwd.Normalize();          // faces back up the road, toward the rider
        var basePos = tr.PointAt(i, 0f);
        var go = new GameObject("Casa do cliente"); go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(basePos, Quaternion.LookRotation(fwd, Vector3.up));
        var cp = go.AddComponent<CustomerPlace>();
        instance = cp;
        var root = go.transform;

        // the hut
        Vector3 top = basePos + Vector3.up * 16f;
        if (tr.customerHouse)
        {
            var hut = Instantiate(tr.customerHouse, root);
            hut.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); hut.transform.localScale = Vector3.one * 0.36f;
            var rs = hut.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
                hut.transform.position += new Vector3(basePos.x - bb.center.x, basePos.y - bb.min.y - 0.4f, basePos.z - bb.center.z) - fwd * 6f;
                rs = hut.GetComponentsInChildren<Renderer>(); bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
                top = new Vector3(bb.center.x, bb.max.y - 1.5f, bb.center.z);
            }
        }
        Lights(tr, root);
        SmokeSignal.Build(root, top);
        cp.Customer(tr, root);
        return cp;
    }

    // a warm pool of light on the ground and lanterns on posts
    static void Lights(Track tr, Transform root)
    {
        var src = Resources.Load<Material>("BeamMat");
        if (!src) return;
        var glow = new Material(src); glow.SetFloat("_Radial", 1f); glow.SetColor("_Color", new Color(1f, 0.7f, 0.28f));
        glow.SetFloat("_Intensity", 1.0f); glow.SetFloat("_Hot", 0.2f); glow.SetFloat("_Opacity", 0.5f);
        var halo = new Material(src); halo.SetFloat("_Radial", 0f); halo.SetColor("_Color", new Color(1f, 0.72f, 0.3f));
        halo.SetFloat("_Intensity", 1.4f); halo.SetFloat("_Hot", 1.2f); halo.SetFloat("_Opacity", 0.6f); halo.SetFloat("_Length", 1f);
        var lamp = new Material(tr.railMat); lamp.SetFloat("_PaletteSlot", -1f); lamp.SetFloat("_TexStrength", 0f);
        lamp.SetColor("_BaseColor", new Color(1f, 0.88f, 0.5f)); lamp.SetColor("_Emission", new Color(1f, 0.7f, 0.25f));
        var pole = new Material(tr.railMat); pole.SetFloat("_PaletteSlot", 3f);
        Transform Part(PrimitiveType t, Material m)
        {
            var g = GameObject.CreatePrimitive(t); Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(root, false);
            var mr = g.GetComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g.transform;
        }
        foreach (var (off, size) in new[] { (Vector3.zero, 40f), (new Vector3(0f, 0f, 6f), 26f), (new Vector3(-5f, 0f, -3f), 22f) })
        {
            var q = Part(PrimitiveType.Quad, glow); var p = root.TransformPoint(off); p.y = tr.HeightAt(p.x, p.z) + 0.35f;
            q.position = p; q.rotation = Quaternion.Euler(90f, 0f, 0f); q.localScale = Vector3.one * size;
        }
        foreach (var lp in new[] { new Vector3(-6f, 0f, 5f), new Vector3(6.5f, 0f, 4.5f), new Vector3(-7f, 0f, -6f), new Vector3(7f, 0f, -6.5f) })
        {
            var p = root.TransformPoint(lp); p.y = tr.HeightAt(p.x, p.z);
            var po = Part(PrimitiveType.Cylinder, pole); po.position = p + Vector3.up * 1.6f; po.localScale = new Vector3(0.22f, 1.6f, 0.22f);
            var la = Part(PrimitiveType.Sphere, lamp); la.position = p + Vector3.up * 3.45f; la.localScale = Vector3.one * 0.75f;
            var ha = Part(PrimitiveType.Sphere, halo); ha.position = la.position; ha.localScale = Vector3.one * 2.6f;
        }
    }

    // the customer: a copy of the player's own character with this biome's animal body and skin
    void Customer(Track tr, Transform root)
    {
        var pv = PlayerVisual.instance;
        if (!pv || !pv.root) return;
        var look = Looks[0];
        foreach (var l in Looks) if (tr.biome != null && l.biome == tr.biome.name) look = l;
        customerName = look.name;
        var cust = Instantiate(pv.root.gameObject, root, false);
        cust.name = "Cliente";
        foreach (var mb in cust.GetComponentsInChildren<MonoBehaviour>(true)) if (!(mb is AnimationEventSink)) DestroyImmediate(mb);
        var sledCopy = cust.transform.Find(pv.sled.name); if (sledCopy) sledCopy.gameObject.SetActive(false);
        cust.transform.localPosition = new Vector3(-1.5f, 0f, 2.5f); cust.transform.localRotation = Quaternion.Euler(0f, -10f, 0f);
        cust.transform.localScale = Vector3.one * 1.6f;
        var cp = cust.transform.position; cp.y = tr.HeightAt(cp.x, cp.z); cust.transform.position = cp;
        var keep = new HashSet<string>(look.parts);
        var skin = tr.customerSkins != null && look.skin < tr.customerSkins.Length ? tr.customerSkins[look.skin] : null;
        foreach (var r in cust.GetComponentsInChildren<Renderer>(true))
        {
            bool on = keep.Contains(r.name);
            r.gameObject.SetActive(on);
            if (!on) continue;
            string n = r.name.ToLowerInvariant();
            if (skin && !n.Contains("eyes") && !n.Contains("face") && !n.Contains("mouth")) r.sharedMaterial = skin;
            for (var t = r.transform.parent; t && t != cust.transform; t = t.parent) t.gameObject.SetActive(true);
        }
        foreach (var t in cust.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
        anim = cust.GetComponentInChildren<Animator>(true);
        if (anim)
        {
            anim.Rebind();
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.SetBool("IsGrounded", true);
            anim.SetFloat("Speed", 0f);
            anim.SetBool("IsSitting", false);
        }

        // face camera for the delivery screen (only renders while the results are up)
        portraitRT = new RenderTexture(512, 512, 16) { name = "cliente" };
        var camGo = new GameObject("Câmera do cliente"); camGo.transform.SetParent(root, false);
        portraitCam = camGo.AddComponent<Camera>();
        camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        portraitCam.targetTexture = portraitRT; portraitCam.fieldOfView = 32f; portraitCam.nearClipPlane = 0.2f; portraitCam.farClipPlane = 600f;
        portraitCam.enabled = false;
        var face = cust.transform.position + Vector3.up * 1.45f;
        camGo.transform.position = face + root.forward * 4.3f + root.right * 0.8f + Vector3.up * 0.15f;
        camGo.transform.LookAt(face);
    }

    void LateUpdate()
    {
        var rm = RunManager.instance;
        bool results = rm && rm.state == RunManager.State.Results;
        if (portraitCam) portraitCam.enabled = results;
        if (!anim) return;
        // waiting: idle, then waves the rider in once they're in sight; on the delivery screen: reacts to the pizza
        int emote = -1;
        if (results) emote = mood;
        else if (rm && rm.player && (rm.player.transform.position - transform.position).sqrMagnitude < 320f * 320f) emote = Wave;
        anim.SetBool("IsGrounded", true);
        anim.SetBool("IsEmoting", emote >= 0);
        if (emote >= 0) anim.SetFloat("EmoteAnimationIndex", emote);
    }

    // how the customer takes it (called when the delivery screen opens)
    public void React(int rank, int slicesLeft)
    {
        if (slicesLeft <= 3 || rank >= 4) mood = Sad;
        else if (rank == 0) mood = Dance1;
        else if (rank == 1) mood = Dance2;
        else mood = Happy;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (portraitRT) portraitRT.Release();
    }

    // what the customer says, by how hot it arrived and how much of it is left
    public static string Quote(int rank, int slicesLeft)
    {
        if (slicesLeft <= 3) return "CADÊ O RESTO DA MINHA PIZZA?";
        switch (rank)
        {
            case 0: return "NEM ESFRIOU! VALEU!";
            case 1: return "TÁ QUENTE AINDA. BOA!";
            case 2: return "MORNA, MAS TÁ VALENDO.";
            case 3: return "ESFRIOU UM POUCO, HEIN...";
            case 4: return "TÁ FRIA. VOU PAGAR MENOS.";
            default: return "ISSO É PICOLÉ DE PIZZA?";
        }
    }
}
