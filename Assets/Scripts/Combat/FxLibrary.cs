using UnityEngine;

// builder) plus a couple of materials. Play() spawns a one-shot effect and removes it after `life` seconds.
public class FxLibrary : MonoBehaviour
{
    public static FxLibrary instance;
    public GameObject xpOrb, spawnFx, hitFx, playerHitFx;
    public GameObject axe, fireball, lightning, katana, arrow, bone;
    public Material goldMaterial, shockwaveMaterial;

    void Awake() { instance = this; }

    public GameObject Play(GameObject prefab, Vector3 pos, float life, Quaternion? rot = null, float scale = 1f)
    {
        if (!prefab) return null;
        var go = Instantiate(prefab, pos, rot ?? Quaternion.identity);
        go.transform.localScale *= scale;
        Destroy(go, life);
        return go;
    }
}
