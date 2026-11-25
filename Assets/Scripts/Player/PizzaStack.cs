using System.Collections.Generic;
using UnityEngine;

// The pizzas on the sled: one box per delivery left (5 in a Corrida, one leaves at each delivery, 3 in Infinito).
// Each box is a kraft tray with a lid and the slice printed on top. Every box sits on springs (lift, pitch, roll)
// fed by the sled's own change of velocity, so bumps and ollies toss the stack, landings squash it and it bounces
// back, braking and turning lean it, and in the air it loosens and wobbles. The springs are clamped: the boxes
// jump around but never fall off.
public class PizzaStack : MonoBehaviour
{
    public static PizzaStack instance;
    const float Size = 0.62f, Thick = 0.085f;
    const float MaxGap = 0.16f, MinGap = -0.012f, MaxTilt = 18f;

    class Box
    {
        public Transform t; public float baseY, yaw;
        public float lift, vLift, pitch, vPitch, roll, vRoll;
    }
    readonly List<Box> boxes = new List<Box>();
    Vector3 lastVel; bool hasVel;
    static Material kraft, kraftLid, logo;

    public static bool Enabled = true;

    public static void Attach(Transform sled)
    {
        if (Enabled && sled && !sled.GetComponent<PizzaStack>()) sled.gameObject.AddComponent<PizzaStack>();
    }

    void Awake() { instance = this; }

    static int Wanted()
    {
        var rm = RunManager.instance;
        if (!rm) return 5;
        if (RunManager.mode == RunMode.Infinito) return 3;
        return Mathf.Clamp(rm.levelsPerRun - rm.level, 1, 5);
    }

    static Material Mat(Color c, Texture tex = null)
    {
        var src = Track.instance ? Track.instance.railMat : null;
        var m = src ? new Material(src) : new Material(Shader.Find("Sledbonk/Toon"));
        m.SetFloat("_PaletteSlot", -1f); m.SetFloat("_TexStrength", 0f);
        m.SetColor("_BaseColor", c); m.SetColor("_Emission", new Color(0.05f, 0.04f, 0.02f));
        if (tex) { m.SetTexture("_BaseMap", tex); m.SetFloat("_Cutoff", 0.5f); m.SetColor("_Emission", Color.black); }
        return m;
    }

    void Rebuild(int n)
    {
        foreach (var b in boxes) if (b.t) Destroy(b.t.gameObject);
        boxes.Clear();
        if (!kraft)
        {
            kraft = Mat(new Color(0.87f, 0.66f, 0.42f));
            kraftLid = Mat(new Color(0.95f, 0.78f, 0.54f));
            logo = Mat(Color.white, Resources.Load<Texture2D>("PizzaLogo"));
        }
        // the boxes render on the same layer as the sled's own mesh (the menu sticker only draws that layer)
        var root = transform.Find("pizzas");
        int layer = gameObject.layer;
        foreach (var r in GetComponentsInChildren<Renderer>(true)) { if (root && r.transform.IsChildOf(root)) continue; layer = r.gameObject.layer; break; }
        if (!root) { root = new GameObject("pizzas").transform; root.SetParent(transform, false); }
        for (int k = 0; k < n; k++)
        {
            var b = new Box { baseY = 0.30f + k * Thick * 1.08f, yaw = Mathf.Sin(k * 2.3f + 0.7f) * 7f };
            var box = new GameObject("caixa " + (k + 1)).transform; box.SetParent(root, false);
            Part(box, PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(Size, Thick * 0.78f, Size), kraft, layer);
            Part(box, PrimitiveType.Cube, new Vector3(0f, Thick * 0.42f, 0f), new Vector3(Size * 1.035f, Thick * 0.3f, Size * 1.035f), kraftLid, layer);
            var l = Part(box, PrimitiveType.Quad, new Vector3(0f, Thick * 0.58f, 0f), new Vector3(Size * 0.62f, Size * 0.62f, 1f), logo, layer);
            l.localRotation = Quaternion.Euler(90f, 0f, 0f);
            b.t = box;
            boxes.Add(b);
        }
        Place(0f);
    }

    static Transform Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material m, int layer)
    {
        var g = GameObject.CreatePrimitive(type); Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = scale;
        var mr = g.GetComponent<MeshRenderer>(); mr.sharedMaterial = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        g.layer = layer;
        return g.transform;
    }

    void LateUpdate()
    {
        int want = Wanted();
        if (want != boxes.Count) Rebuild(want);
        var pv = PlayerVisual.instance; var pc = pv ? pv.player : null;
        float dt = Time.deltaTime;
        if (dt <= 0f || !pc || !pc.rig) { Place(0f); return; }

        // the sled's change of velocity this frame, in its own frame: what the boxes feel as a shove
        var v = pc.rig.linearVelocity;
        var dv = hasVel ? transform.InverseTransformDirection(v - lastVel) : Vector3.zero;
        lastVel = v; hasVel = true;
        if (dv.sqrMagnitude > 30f * 30f) dv = Vector3.zero;   // respawns and teleports are not bumps
        bool air = !pc.data.isGrounded && pc.data.sinceGrounded > 0.12f;
        float turn = pc.data.turnAmount, t = Time.time;

        for (int k = 0; k < boxes.Count; k++)
        {
            var b = boxes[k]; float h = (k + 1f) / boxes.Count;          // higher boxes swing more
            // kicks: a sudden push up (bump, ollie, landing) leaves the box behind, then it is thrown back up
            b.vLift += -dv.y * (0.10f + 0.10f * h);
            b.vPitch += dv.z * (6f + 9f * h);
            b.vRoll += -dv.x * (6f + 9f * h);
            // targets: loose and wobbling in the air, leaning into turns on the ground
            float liftT = air ? 0.022f + 0.012f * Mathf.Sin(t * 9f + k * 1.7f) : 0f;
            float pitchT = air ? Mathf.Sin(t * 6.5f + k * 2.1f) * 6f * h : 0f;
            float rollT = air ? Mathf.Sin(t * 5.3f + k * 1.3f) * 5f * h : -turn * 7f * h;
            // springs
            b.vLift += ((liftT - b.lift) * 260f - b.vLift * 13f) * dt; b.lift += b.vLift * dt;
            b.vPitch += ((pitchT - b.pitch) * 170f - b.vPitch * 11f) * dt; b.pitch += b.vPitch * dt;
            b.vRoll += ((rollT - b.roll) * 170f - b.vRoll * 11f) * dt; b.roll += b.vRoll * dt;
            // never through the box below (it bounces off it), never off the sled
            if (b.lift < MinGap) { b.lift = MinGap; if (b.vLift < 0f) b.vLift = -b.vLift * 0.45f; }
            if (b.lift > MaxGap * (0.6f + 0.4f * h)) { b.lift = MaxGap * (0.6f + 0.4f * h); if (b.vLift > 0f) b.vLift *= -0.3f; }
            b.pitch = Mathf.Clamp(b.pitch, -MaxTilt, MaxTilt);
            b.roll = Mathf.Clamp(b.roll, -MaxTilt, MaxTilt);
        }
        Place(t);
    }

    // each box rides on the one below: its gap adds to everything above it
    void Place(float t)
    {
        float stack = 0f;
        for (int k = 0; k < boxes.Count; k++)
        {
            var b = boxes[k];
            stack += b.lift;
            b.t.localPosition = new Vector3(Mathf.Sin(b.roll * Mathf.Deg2Rad) * 0.05f, b.baseY + stack, -0.74f + Mathf.Sin(b.pitch * Mathf.Deg2Rad) * 0.05f);
            b.t.localRotation = Quaternion.Euler(b.pitch, b.yaw, b.roll);
        }
    }
}
