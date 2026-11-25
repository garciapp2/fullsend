using UnityEngine;

// Expanding ring shown by the landing shockwave.
public class Shockwave : MonoBehaviour
{
    float radius, t; Material mat;
    const float Duration = 0.45f;

    public static void Spawn(Vector3 pos, float radius, bool perfect)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(go.GetComponent<Collider>());
        go.transform.position = pos + Vector3.up * 0.3f;
        var lib = FxLibrary.instance;
        var r = go.GetComponent<Renderer>();
        if (lib && lib.shockwaveMaterial) r.sharedMaterial = lib.shockwaveMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var s = go.AddComponent<Shockwave>(); s.radius = radius; s.mat = r.material;
        if (perfect) s.mat.SetColor("_BaseColor", new Color(1f, 0.85f, 0.35f));
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = t / Duration;
        float d = Mathf.Lerp(1f, radius * 2f, Mathf.Sqrt(k));
        transform.localScale = new Vector3(d, Mathf.Lerp(0.6f, 0.05f, k), d);
        if (k >= 1f) Destroy(gameObject);
    }
}
