using System.Collections;
using UnityEngine;

// player's runnable mask, so the ground linecast meets them like terrain:
//  - Runnable (rocks, roots, ruins, arches, mushrooms): you land and ride on them; a landing on a prop is never Bad.
//  - Solid (trees, crystals, spikes): not runnable, so touching them is an EdgeLanding: bonk or head-on bounce.
//    Their colliders are triggers so the sled is not physically stopped by a shrub.
//  - None (grass, flowers, ferns): no collision.
public class PropInfo : MonoBehaviour
{
    public enum Kind { None, Breakable, Runnable, Solid }
    public Kind kind;
    public float lethalDamage = 85f;
    public bool lethal = true;
    public float damage = 15f;
    bool broken;

    public bool Runnable => kind == Kind.Runnable;

    public static Kind Classify(string name)
    {
        string n = name.ToLowerInvariant();
        if (n.Contains("grass") || n.Contains("flower") || n.Contains("fern") || n.Contains("lavender")) return Kind.None;
        if (n.Contains("bush") || n.Contains("cactus") || n.Contains("mushroomtree")) return Kind.Breakable;
        if (n.Contains("crystal") || n.Contains("spyke") || n.Contains("sword") || n.Contains("evergreen")) return Kind.Solid;
        if (n.Contains("rots") || n.Contains("root")) return Kind.Runnable;   // roots before "tree": Tree_Rots / Tree_Root
        if (n.Contains("tree")) return Kind.Solid;
        return Kind.Runnable;   // rocks, ruins, arches, bridges, mushrooms
    }

    // Adds colliders from the prop's own render meshes and tags it. Returns the kind.
    public static Kind Setup(GameObject go)
    {
        var kind = Classify(go.name);
        if (kind == Kind.None) return kind;
        var info = go.AddComponent<PropInfo>(); info.kind = kind;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (!mf.sharedMesh) continue;
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            if (kind == Kind.Breakable) mc.convex = true;
            mc.sharedMesh = mf.sharedMesh;
            mf.gameObject.layer = PlayerCharacter.PropLayer;
        }
        return kind;
    }

    void OnTriggerEnter(Collider other)
    {
        if (broken || kind != Kind.Breakable) return;
        var pc = other.GetComponentInParent<PlayerCharacter>();
        if (pc) HitBreakable(pc);
    }

    public void HitBreakable(PlayerCharacter pc)
    {
        if (broken) return;
        broken = true;
        if (!pc.Invulnerable()) { pc.TakeDamage(damage, transform.position); pc.SlowDown(0.9f); }
        GameFeel.Shake(10f, 0.3f);
        ComicFX.Impact(0.5f, 0.05f);
        if (Sfx.instance) Sfx.Play(Sfx.instance.wallHit, 0.7f, 1.4f);
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        StartCoroutine(Fly(pc.rig.linearVelocity));
    }

    IEnumerator Fly(Vector3 playerVel)
    {
        var vel = playerVel * 0.6f + Vector3.up * 9f;
        var spin = Random.onUnitSphere * 540f;
        var s0 = transform.localScale;
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            vel += Vector3.down * 30f * Time.deltaTime;
            transform.position += vel * Time.deltaTime;
            transform.Rotate(spin * Time.deltaTime, Space.World);
            transform.localScale = s0 * (1f - t / 0.8f);
            yield return null;
        }
        gameObject.SetActive(false);
    }
}
