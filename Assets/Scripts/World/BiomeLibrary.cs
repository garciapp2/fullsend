using UnityEngine;

// Holds the biomes and applies one (sky, fog, palette, UI theme). Kept in its own file so Unity can attach it as a component.
public class BiomeLibrary : MonoBehaviour
{
    public static BiomeLibrary instance;
    public BiomeDef[] biomes;
    public Material skybox;
    void Awake() { instance = this; }

    public void Apply(BiomeDef b, Light sun)
    {
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = b.fog; RenderSettings.fogDensity = b.fogDensity;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = b.ambTop; RenderSettings.ambientEquatorColor = b.ambMid; RenderSettings.ambientGroundColor = b.ambBot;
        if (sun) { sun.color = b.sun; sun.intensity = b.sunIntensity; sun.transform.rotation = Quaternion.Euler(b.sunEuler); }
        if (skybox)
        {
            skybox.SetColor("_Top", b.skyTop); skybox.SetColor("_Horizon", b.skyHorizon); skybox.SetColor("_Bottom", b.fog * 0.85f);
            skybox.SetColor("_SunColor", b.sun);
            if (sun) skybox.SetVector("_SunDir", -sun.transform.forward);
            RenderSettings.skybox = skybox;
        }
        for (int i = 0; i < 5; i++) Shader.SetGlobalColor("_Pal" + i, b.palette != null && i < b.palette.Length ? b.palette[i] : Color.white);
        Shader.SetGlobalColor("_ToneMid", b.toneMid); Shader.SetGlobalColor("_ToneLow", b.toneLow);
        Shader.SetGlobalFloat("_HalftoneScale", 5f); Shader.SetGlobalFloat("_GlobalTexStrength", 0.25f);
        var ink = b.ink; ink.a = 1f; Shader.SetGlobalColor("_InkColor", ink);
        if (Hud.instance) Hud.instance.SetTheme(b.ui, b.ui2, b.uiInk);
        DynamicGI.UpdateEnvironment();
    }
}
