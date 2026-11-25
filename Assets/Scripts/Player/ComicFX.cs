using UnityEngine;

//  RadialBlur pulse, RGB split ("ScreenOffset"), a colour flash, hit-stop (time nearly stops for a few
//  frames on big moments) and the film overlay flickering at 12 fps like a scanned photocopy.
public class ComicFX : MonoBehaviour
{
    public static ComicFX instance;
    float radial, split, flash, hitstop, grainT, grainSeed;
    Color flashCol = Color.white;
    bool stopped;
    public float continuousRadial;

    void Awake() { instance = this; Shader.SetGlobalColor("_InkColor", new Color(0.05f, 0.04f, 0.08f, 1f)); }

    // strength ~0.3 small, 1 big. hitstopTime in real seconds.
    public static void Impact(float strength, float hitstopTime = 0f, Color? flashColor = null, float flashAmount = 0f)
    {
        var f = instance; if (!f) return;
        f.radial = Mathf.Max(f.radial, strength);
        f.split = Mathf.Max(f.split, strength * 0.8f);
        if (flashColor.HasValue && flashAmount > f.flash) { f.flashCol = flashColor.Value; f.flash = flashAmount; }
        if (hitstopTime > f.hitstop) f.hitstop = hitstopTime;
    }

    public static void Split(float amount) { if (instance) instance.split = Mathf.Max(instance.split, amount); }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        radial = Mathf.MoveTowards(radial, 0f, dt * 3.5f);
        split = Mathf.MoveTowards(split, 0f, dt * 3f);
        flash = Mathf.MoveTowards(flash, 0f, dt * 6f);
        var ts = TrickSystem.instance;
        float boost = ts && ts.boosting ? 0.35f : 0f;
        continuousRadial = Mathf.Lerp(continuousRadial, boost, dt * 6f);
        Shader.SetGlobalFloat("_FxRadial", radial * radial * 0.07f + continuousRadial * 0.05f);
        Shader.SetGlobalFloat("_FxSplit", split * split * 0.008f);
        Shader.SetGlobalFloat("_FxFlash", flash);
        Shader.SetGlobalColor("_FxFlashColor", flashCol);
        grainT -= dt;
        if (grainT <= 0f) { grainT = 1f / 12f; grainSeed = Random.Range(0f, 100f); Shader.SetGlobalFloat("_FxGrainSeed", grainSeed); }

        // hit-stop: only while the game is actually running (not paused / in menus)
        if (hitstop > 0f)
        {
            hitstop -= dt;
            if (!stopped && Time.timeScale > 0.99f) { Time.timeScale = 0.06f; stopped = true; }
            if (hitstop <= 0f && stopped) { if (Mathf.Approximately(Time.timeScale, 0.06f)) Time.timeScale = 1f; stopped = false; }
        }
    }

    void OnDisable() { if (stopped && Mathf.Approximately(Time.timeScale, 0.06f)) Time.timeScale = 1f; }
}
