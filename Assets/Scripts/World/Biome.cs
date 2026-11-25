using UnityEngine;

[System.Serializable]
public class BiomeDef
{
    public string name;
    [Header("Sky / light")]
    public Color skyTop = new Color(0.3f, 0.5f, 0.95f), skyHorizon = new Color(0.85f, 0.9f, 1f);
    public Color fog = new Color(0.8f, 0.85f, 0.95f); public float fogDensity = 0.0016f;
    public Color sun = Color.white; public float sunIntensity = 1.3f; public Vector3 sunEuler = new Vector3(40f, 30f, 0f);
    public Color ambTop = new Color(0.75f, 0.82f, 1f), ambMid = new Color(0.6f, 0.65f, 0.78f), ambBot = new Color(0.35f, 0.35f, 0.42f);
    [Header("Flat palette")]
    public Color[] palette = { Color.white, Color.gray, Color.green, new Color(0.5f, 0.3f, 0.2f), Color.magenta };   // ground, rock, foliage, wood, accent
    public Color toneMid = new Color(0.8f, 0.8f, 0.92f), toneLow = new Color(0.52f, 0.52f, 0.75f);
    public Color ink = new Color(0.05f, 0.04f, 0.08f), ui = new Color(1f, 0.9f, 0.1f), ui2 = new Color(1f, 0.35f, 0.75f), uiInk = new Color(0.04f, 0.04f, 0.055f);
    [Header("Terrain")]
    public Color road = Color.white, flat = Color.white, slope = Color.gray, cliff = Color.gray, far = Color.gray;
    [Header("Props")]
    public GameObject[] small, medium, landmarks;
    public float propScale = 1f;
    [Header("Audio")]
    public AudioClip[] music;
    public AudioClip ambience;
}
