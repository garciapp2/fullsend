using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

//                 markers that move with time (where you'd need to be for S/A/B/C), the centre kept clear
//                 shadows (black + the biome's second colour), and a portrait in the corner: here the fox,
//                 rendered live by its own camera
//  shared       - halftone dots (same as the world shader) behind the speed and on the result pages
// Screens: menu, level results (+ items), death, run over, pause. Keys: 1/2/3, W/S + Enter, Q, E, R, M, Esc.
public partial class Hud : MonoBehaviour
{
    public static Hud instance;
    public Font titleFont, bodyFont;
    public PlayerCharacter player;
    public Material markerMat;
    public Texture2D heartTex;

    public Color Accent { get; private set; } = new Color(0.3f, 1f, 0.8f);
    public Color Accent2 { get; private set; } = new Color(1f, 0.35f, 0.75f);
    static Color Ink = new Color(0.04f, 0.04f, 0.055f, 1f);
    static readonly Color Danger = new Color(0.95f, 0.16f, 0.22f), Faint = new Color(1, 1, 1, 0.28f);
    static readonly Color Collapse = new Color(0.45f, 0.12f, 0.75f);
    static readonly List<Graphic> inkG = new List<Graphic>();
    static readonly List<Shadow> inkShadows = new List<Shadow>();
    static bool IsInk(Color c) => Mathf.Abs(c.r - Ink.r) + Mathf.Abs(c.g - Ink.g) + Mathf.Abs(c.b - Ink.b) < 0.001f;
    Font heavy, cond;
    readonly List<Graphic> acc = new List<Graphic>(), acc2 = new List<Graphic>();
    readonly List<Shadow> acc2Shadows = new List<Shadow>(), accShadows = new List<Shadow>();
    readonly List<Outline> inkOutlines = new List<Outline>();
    const int PortraitLayer = 9, PortraitBackLayer = 10;
    public static string GameTitle = "FULLSEND";

    // ================================================================ tweens (unscaled)
    class Tw { public RectTransform rt; public Vector2 from, to; public float delay, dur, t; public bool scale; }
    readonly List<Tw> tweens = new List<Tw>();
    void Slide(RectTransform rt, Vector2 from, float delay, float dur = 0.35f)
    {
        tweens.RemoveAll(x => x.rt == rt && !x.scale);
        var to = rt.anchoredPosition; rt.anchoredPosition = to + from;
        tweens.Add(new Tw { rt = rt, from = to + from, to = to, delay = delay, dur = dur });
    }
    void Punch(RectTransform rt, float amount = 1.3f, float dur = 0.25f)
    {
        tweens.RemoveAll(x => x.rt == rt && x.scale);
        tweens.Add(new Tw { rt = rt, from = Vector2.one * amount, to = Vector2.one, dur = dur, scale = true });
    }
    static float EaseOutBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1f; t -= 1f; return 1f + c3 * t * t * t + c1 * t * t; }
    void UpdateTweens(float dt)
    {
        for (int i = tweens.Count - 1; i >= 0; i--)
        {
            var w = tweens[i]; if (!w.rt) { tweens.RemoveAt(i); continue; }
            if (w.delay > 0f) { w.delay -= dt; if (!w.scale) w.rt.anchoredPosition = w.from; continue; }
            w.t += dt; float k = Mathf.Clamp01(w.t / w.dur), e = EaseOutBack(k);
            if (w.scale) w.rt.localScale = Vector3.one * Mathf.LerpUnclamped(w.from.x, w.to.x, e);
            else w.rt.anchoredPosition = Vector2.LerpUnclamped(w.from, w.to, e);
            if (k >= 1f) tweens.RemoveAt(i);
        }
    }

    // ================================================================ building blocks
    static RectTransform R(string n, Transform p, Vector2 a, Vector2 pos, Vector2 size, Vector2? pivot = null, Vector2? aMax = null)
    {
        var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false);
        var r = (RectTransform)go.transform; r.anchorMin = a; r.anchorMax = aMax ?? a; r.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = size; return r;
    }
    static Para P(Transform p, Color c, Vector2 a, Vector2 pos, Vector2 size, Vector2? pivot = null, float skew = 0.22f)
    {
        var g = R("para", p, a, pos, size, pivot).gameObject.AddComponent<Para>();
        g.color = c; g.skew = skew; g.raycastTarget = false;
        if (IsInk(c)) inkG.Add(g);
        return g;
    }
    static Image Full(Transform p, Color c)
    {
        var img = R("full", p, Vector2.zero, Vector2.zero, Vector2.zero, null, Vector2.one).gameObject.AddComponent<Image>();
        img.color = c; img.raycastTarget = false;
        if (IsInk(new Color(c.r, c.g, c.b))) inkG.Add(img);
        return img;
    }
    Text T(Transform p, string s, Font f, int size, Color col, TextAnchor anchor, Vector2 a, Vector2 pos, Vector2? pivot = null)
    {
        var t = R("txt", p, a, pos, new Vector2(10, 10), pivot).gameObject.AddComponent<Text>();
        t.font = f; t.text = s; t.fontSize = size; t.color = col; t.alignment = anchor; t.raycastTarget = false; t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.lineSpacing = 0.92f;
        if (IsInk(col)) inkG.Add(t);
        return t;
    }
    Text Hard(Transform p, string s, int size, Color col, TextAnchor anchor, Vector2 a, Vector2 pos, Vector2? pivot = null, bool second = true, float off = 1f)
    {
        var t = T(p, s, heavy, size, col, anchor, a, pos, pivot);
        float k = Mathf.Sqrt(size / 90f);
        if (second) { var s2 = t.gameObject.AddComponent<Shadow>(); s2.effectColor = Accent2; s2.effectDistance = new Vector2(8, -8) * k; acc2Shadows.Add(s2); }
        var s1 = t.gameObject.AddComponent<Shadow>(); s1.effectColor = Ink; s1.effectDistance = new Vector2(4, -5) * k; inkShadows.Add(s1);
        return t;
    }

    class Tag
    {
        public RectTransform box; public Para bg, shadow; public Text text; public Vector2 pad;
        public void Set(string s) { if (text.text == s) return; text.text = s; Fit(); }
        public void Fit()
        {
            var size = new Vector2(text.preferredWidth + pad.x * 2f, text.fontSize * 1.02f + pad.y * 2f);
            box.sizeDelta = size; bg.rectTransform.sizeDelta = size;
            if (shadow) shadow.rectTransform.sizeDelta = size;
        }
    }
    Tag MakeTag(Transform p, string s, Font f, int size, Color fg, Color bg, Vector2 a, Vector2 pos, Vector2 pivot, bool shadow = false, float padX = 18f, float padY = 5f)
    {
        var tg = new Tag { pad = new Vector2(padX, padY) };
        var holder = R("tag", p, a, pos, new Vector2(100, 40), pivot);
        if (shadow) { tg.shadow = P(holder, Accent2, new Vector2(0.5f, 0.5f), new Vector2(8, -8), new Vector2(100, 40)); acc2.Add(tg.shadow); }
        tg.bg = P(holder, bg, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100, 40));
        tg.box = holder;
        tg.text = T(tg.bg.transform, s, f, size, fg, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(tg.bg.skew * size * 0.5f, 0));
        tg.Fit();
        return tg;
    }
    void FitTag(Tag t) { t.Fit(); }

    static Texture2D DotsTex(bool fadeRight)
    {
        const int W = 256, H = 128, S = 16; var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapModeU = TextureWrapMode.Clamp, wrapModeV = TextureWrapMode.Repeat };
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float t = x / (float)W; if (!fadeRight) t = 1f - t;
                float r = 0.48f * t;
                int row = y / S; float ox = row % 2 == 1 ? S * 0.5f : 0f;
                float cx = (Mathf.Floor((x - ox) / S) + 0.5f) * S + ox, cy = (row + 0.5f) * S;
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / S;
                px[y * W + x] = new Color(1, 1, 1, Mathf.Clamp01((r - d) * S));
            }
        tex.SetPixels(px); tex.Apply(); return tex;
    }

    // ================================================================ state
    RectTransform hudRoot, screens;
    // portrait
    Camera portraitCam; RenderTexture portraitRT; readonly List<RawImage> portraitImages = new List<RawImage>();
    readonly List<RectTransform> portraitCards = new List<RectTransform>();
    // top
    Tag levelTag; Image paceFill; RectTransform paceLine, playerMark; readonly List<(RectTransform tick, Text label)> paceTicks = new List<(RectTransform, Text)>();
    Text timeText, rankText, chaseText; RectTransform rankBadge;
    // bottom
    const int Slices = 8;
    RectTransform pizzaRoot; readonly List<RawImage> slices = new List<RawImage>(); Text sliceText; int lastSlices = Slices;
    class Flyer { public RectTransform rt; public RawImage img; public Vector2 v; public float spin, t; }
    readonly List<Flyer> flyers = new List<Flyer>();
    Tag tempTag;
    // the pace line runs hot -> cold: how warm the pizza will arrive at each rank
    static readonly Color Hot = new Color(1f, 0.36f, 0.14f), Warm = new Color(1f, 0.75f, 0.2f), Cool = new Color(0.6f, 0.78f, 1f), Cold = new Color(0.31f, 0.55f, 1f);
    static readonly string[] TempNames = { "QUENTINHA", "QUENTE", "MORNA", "ESFRIANDO", "FRIA", "GELADA" };
    public static Color TempColor(float t)
    {
        if (t < 0.45f) return Color.Lerp(Hot, Warm, t / 0.45f);
        if (t < 0.62f) return Color.Lerp(Warm, Cool, (t - 0.45f) / 0.17f);
        return Color.Lerp(Cool, Cold, (t - 0.62f) / 0.38f);
    }
    public static string TempName(int rank) => TempNames[Mathf.Clamp(rank, 0, TempNames.Length - 1)];
    Text scoreText;
    RawImage resCustomerImg; Tag resCustomerName, resQuote; Para resQuoteTail;
    Text speedText, speedUnit; RawImage speedDots; readonly List<Para> tach = new List<Para>(), boostSegs = new List<Para>();
    Tag trick, trickPts, bannerTop, bannerBot; Text comboText, comboLabel, callout; Image comboFill; RectTransform comboRoot;
    float trickHold, calloutT, bannerT, flashT, comboShown, lastSpeed;
    string lastLive = "";
    Image flash; RawImage vignette, speedLines; LandingMarker marker;
    // screens
    GameObject menu, results, end, pause;
    int menuSel; readonly List<Tag> menuItems = new List<Tag>(); Text menuDesc;
    Text resRank; Tag resLevel, resDone, resNote; readonly List<(Tag label, Text value, Image rule)> resRows = new List<(Tag, Text, Image)>();
    readonly List<(RectTransform rt, Para stripe, Text name, Text rarity, Text desc)> cards = new List<(RectTransform, Para, Text, Text, Text)>();
    Tag resChoose; readonly List<Tag> resFooter = new List<Tag>(); bool resultsLast;
    Text endTitle; readonly List<(Tag label, Text value, Image rule)> endRows = new List<(Tag, Text, Image)>(); readonly List<Tag> endFooter = new List<Tag>(); Image endTint;

    static readonly string[] ModeNames = { "CORRIDA", "FUGA", "INFINITO" };
    static readonly string[] ModeDesc = {
        "5 ENTREGAS CONTRA O RELÓGIO. QUANTO MAIS QUENTE, MAIOR A GORJETA.\nGORJETAS COMPRAM ITENS ENTRE AS ENTREGAS.",
        "AS MESMAS 5 ENTREGAS, MAS O MUNDO SE DESFAZ ATRÁS DE VOCÊ.\nPERTO DAS RACHADURAS A PIZZA SOFRE. NO ESCURO, ACABOU.",
        "TURNO SEM FIM, CADA ENTREGA MAIS RÁPIDA.\nO MUNDO NUNCA PARA DE RACHAR. ATÉ ONDE VOCÊ ENTREGA?" };

    // ================================================================ setup
    void Awake()
    {
        instance = this;
        inkG.Clear(); inkShadows.Clear(); Ink = new Color(0.04f, 0.04f, 0.055f, 1f);
        heavy = Font.CreateDynamicFontFromOSFont(new[] { "Franklin Gothic Heavy Italic", "Franklin Gothic Heavy", "Arial Bold Italic", "Arial" }, 64);
        cond = titleFont ? titleFont : heavy;
        var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
        var cs = gameObject.AddComponent<CanvasScaler>(); cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080); scaler = cs; FitScaler();
        gameObject.AddComponent<GraphicRaycaster>();
        if (!FindAnyObjectByType<EventSystem>())
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            es.transform.SetParent(transform);
            es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
        }
        portraitRT = new RenderTexture(384, 384, 16, RenderTextureFormat.ARGB32) { name = "Retrato" };
        BuildRun(); BuildPrompt(); BuildMenu(); BuildResults(); BuildEnd(); BuildPause(); BuildLoading();
        var m = new GameObject("Marcador de pouso"); marker = m.AddComponent<LandingMarker>(); marker.Init(markerMat);
    }

    void Start()
    {
        if (TrickSystem.instance) TrickSystem.instance.onFeed += Feed;
        SetupPortraitCamera();
    }

    public void SetAccent(Color ui, Color second) => SetTheme(ui, second, Ink);

    // a biome's UI theme: accent, second colour and the deep "ink" used for tags, badges and hard shadows
    public void SetTheme(Color ui, Color second, Color ink)
    {
        ink.a = 1f;
        foreach (var g in inkG) if (g) { var c = ink; c.a = g.color.a; g.color = c; }
        foreach (var s in inkShadows) if (s) s.effectColor = ink;
        Ink = ink;
        Accent = ui; Accent2 = second;
        foreach (var g in acc) if (g) { var c = ui; c.a = g.color.a; g.color = c; }
        foreach (var d in pageDots) if (d) d.color = new Color(ui.r * 0.55f, ui.g * 0.55f, ui.b * 0.55f, 1f);
        foreach (var g in acc2) if (g) { var c = second; c.a = g.color.a; g.color = c; }
        foreach (var s in acc2Shadows) if (s) s.effectColor = second;
        foreach (var s in accShadows) if (s) s.effectColor = ui;
        foreach (var o in inkOutlines) if (o) o.effectColor = ink;
        if (portraitCam) portraitCam.backgroundColor = ui;
        RefreshPortraitBack();
        SelectMenu(menuSel);
    }

    void SetupPortraitCamera()
    {
        var pv = PlayerVisual.instance; if (!pv || !pv.character) return;
        foreach (var r in pv.character.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PortraitLayer;
        if (pv.sled) foreach (var r in pv.sled.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = PortraitLayer;
        var go = new GameObject("Câmera do retrato");
        portraitCam = go.AddComponent<Camera>();
        portraitCam.clearFlags = CameraClearFlags.SolidColor; portraitCam.backgroundColor = Accent;
        portraitCam.cullingMask = (1 << PortraitLayer) | (1 << PortraitBackLayer); portraitCam.fieldOfView = 22f; portraitCam.nearClipPlane = 0.05f; portraitCam.farClipPlane = 30f;
        portraitCam.targetTexture = portraitRT; portraitCam.depth = -10;
        var data = go.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = false; data.renderShadows = false;
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(quad.GetComponent<Collider>());
        quad.name = "fundo do retrato"; quad.layer = PortraitBackLayer; quad.transform.SetParent(go.transform, false);
        if (Camera.main) Camera.main.cullingMask &= ~(1 << PortraitBackLayer);
        quad.transform.localPosition = new Vector3(0f, 0f, 5f); quad.transform.localScale = new Vector3(2.6f, 2.6f, 1f);
        var sh = Shader.Find("Sledbonk/UnlitTex");
        if (sh) { portraitBackMat = new Material(sh); quad.GetComponent<Renderer>().sharedMaterial = portraitBackMat; quad.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
        RefreshPortraitBack();
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    }

    void OnBeginCamera(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera c) =>
        Shader.SetGlobalFloat("_FxOff", c == portraitCam || c == menuCam || (CustomerPlace.instance && c == CustomerPlace.instance.portraitCam) ? 1f : 0f);

    Material portraitBackMat;
    void RefreshPortraitBack()
    {
        if (!portraitBackMat) return;
        const int S = 128, C = 16; var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
        var dark = Color.Lerp(Accent, Ink, 0.22f); var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float r = 0.18f + 0.22f * (x / (float)S);
                int row = y / C; float ox = row % 2 == 1 ? C * 0.5f : 0f;
                float cx = (Mathf.Floor((x - ox) / C) + 0.5f) * C + ox, cy = (row + 0.5f) * C;
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / C;
                px[y * S + x] = d < r ? dark : Accent;
            }
        tex.SetPixels(px); tex.Apply();
        portraitBackMat.SetTexture("_MainTex", tex); portraitBackMat.SetTextureScale("_MainTex", new Vector2(3f, 3f));
    }

    void LateUpdate()
    {
        var pv = PlayerVisual.instance;
        if (!portraitCam || !pv || !pv.character) return;
        var ch = pv.character;
        var rot = ch.rotation;
        portraitCam.transform.position = ch.position + rot * new Vector3(1.1f, 1.15f, 2.6f);
        portraitCam.transform.rotation = Quaternion.LookRotation(ch.position + rot * new Vector3(0f, 0.85f, 0f) - portraitCam.transform.position, rot * Vector3.up);
    }

    RectTransform PortraitCard(Transform p, Vector2 a, Vector2 pos, float size, Vector2 pivot)
    {
        var holder = R("retrato", p, a, pos, new Vector2(size, size), pivot);
        var sh = P(holder, Accent2, new Vector2(0.5f, 0.5f), new Vector2(10, -10), new Vector2(size, size), null, 0.18f); acc2.Add(sh);
        P(holder, Ink, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size), null, 0.18f);
        var maskG = P(holder, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size - 16, size - 16), null, 0.18f);
        var mask = maskG.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = false;
        var raw = R("img", maskG.transform, new Vector2(0.5f, 0.5f), new Vector2(size * 0.1f, 0), new Vector2(size * 1.25f, size * 1.25f)).gameObject.AddComponent<RawImage>();
        raw.texture = portraitRT; raw.raycastTarget = false;
        portraitImages.Add(raw); portraitCards.Add(holder);
        return holder;
    }

    // ================================================================ in-run HUD
    void BuildRun()
    {
        var t = transform;
        speedLines = R("speedlines", t, Vector2.zero, Vector2.zero, Vector2.zero, null, Vector2.one).gameObject.AddComponent<RawImage>();
        speedLines.texture = SpeedLineTex(); speedLines.raycastTarget = false; speedLines.color = new Color(1, 1, 1, 0);
        vignette = R("vignette", t, Vector2.zero, Vector2.zero, Vector2.zero, null, Vector2.one).gameObject.AddComponent<RawImage>();
        vignette.texture = VignetteTex(); vignette.raycastTarget = false; vignette.color = new Color(Danger.r, Danger.g, Danger.b, 0f);
        flash = Full(t, new Color(1, 0.1f, 0.15f, 0));
        hudRoot = R("hud", t, Vector2.zero, Vector2.zero, Vector2.zero, null, Vector2.one);
        var h = hudRoot;

        // ---- top: level tag, pace line with moving rank markers, time, rank badge
        levelTag = MakeTag(h, "FASE 1/5  ·  NEVE", cond, 30, Color.white, Ink, new Vector2(0.5f, 1), new Vector2(-412, -66), new Vector2(1, 0.5f));
        paceLine = R("pace", h, new Vector2(0.5f, 1), new Vector2(0, -66), new Vector2(800, 6));
        var bgLine = paceLine.gameObject.AddComponent<Image>(); bgLine.raycastTarget = false;
        bgLine.sprite = Sprite.Create(TempTex(), new Rect(0, 0, 256, 1), Vector2.one * 0.5f); bgLine.color = Color.white;
        paceFill = R("fill", paceLine, Vector2.zero, Vector2.zero, Vector2.zero, null, Vector2.one).gameObject.AddComponent<Image>();
        paceFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
        paceFill.type = Image.Type.Filled; paceFill.fillMethod = Image.FillMethod.Horizontal; paceFill.color = new Color(1f, 1f, 1f, 0.45f); paceFill.raycastTarget = false;
        string[] tiers = { "S", "A", "B", "C" };
        for (int i = 0; i < 4; i++)
        {
            var tick = R("tick", paceLine, new Vector2(0, 0.5f), Vector2.zero, new Vector2(4, 22));
            tick.gameObject.AddComponent<Image>().raycastTarget = false;
            var lb = T(tick, tiers[i], cond, 26, Color.white, TextAnchor.LowerCenter, new Vector2(0.5f, 1), new Vector2(0, 6), new Vector2(0.5f, 0));
            var o = lb.gameObject.AddComponent<Outline>(); o.effectColor = Ink; o.effectDistance = new Vector2(2, -2);
            paceTicks.Add((tick, lb));
        }
        playerMark = P(paceLine, Accent, new Vector2(0, 0.5f), new Vector2(0, -18), new Vector2(26, 20)).rectTransform;
        playerMark.GetComponent<Para>().triangle = true; acc.Add(playerMark.GetComponent<Para>());
        timeText = Hard(h, "0:00.0", 64, Color.white, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -92), new Vector2(0.5f, 1), false);
        tempTag = MakeTag(h, "QUENTINHA", cond, 30, Ink, Hot, new Vector2(0.5f, 1), new Vector2(150, -128), new Vector2(0, 0.5f), true);
        rankBadge = R("rank", h, new Vector2(0.5f, 1), new Vector2(490, -24), new Vector2(104, 104), new Vector2(0, 1));
        var rs = P(rankBadge, Accent2, new Vector2(0.5f, 0.5f), new Vector2(8, -8), new Vector2(104, 104)); acc2.Add(rs);
        P(rankBadge, Ink, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(104, 104));
        rankText = T(rankBadge, "S", heavy, 96, Accent, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(10, 2)); acc.Add(rankText);
        chaseText = Hard(h, "", 34, Danger, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(0.5f, 1), false);

        // ---- bottom-left: portrait + name tag, hearts, score
        PortraitCard(h, new Vector2(0, 0), new Vector2(40, 54), 236, new Vector2(0, 0));
        var nameTag = MakeTag(h, "FOX", cond, 32, Ink, Accent, new Vector2(0, 0), new Vector2(54, 300), new Vector2(0, 0.5f)); acc.Add(nameTag.bg);
        pizzaRoot = R("pizza", h, new Vector2(0, 0), new Vector2(392, 250), new Vector2(136, 136));
        var plate = R("prato", pizzaRoot, new Vector2(0.5f, 0.5f), new Vector2(8, -9), new Vector2(142, 142)).gameObject.AddComponent<RawImage>();
        plate.texture = DiscTex(); plate.color = Accent2; plate.raycastTarget = false; acc2.Add(plate);
        var sliceTex = Resources.Load<Texture2D>("PizzaSliceUI");
        float sr = 68f, aspect = sliceTex ? (float)sliceTex.width / sliceTex.height : 0.83f;
        for (int k = 0; k < Slices; k++)
        {
            var sl = R("fatia", pizzaRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(sr * aspect, sr), new Vector2(0.5f, 0f));
            sl.localRotation = Quaternion.Euler(0f, 0f, -k * 360f / Slices);
            var ri = sl.gameObject.AddComponent<RawImage>(); ri.texture = sliceTex; ri.raycastTarget = false;
            slices.Add(ri);
        }
        sliceText = Hard(h, "8/8", 64, Color.white, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(474, 250), new Vector2(0, 0.5f));
        MakeTag(h, "PONTOS", cond, 28, Color.white, Ink, new Vector2(0, 0), new Vector2(318, 168), new Vector2(0, 0.5f));
        scoreText = Hard(h, "0", 74, Color.white, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(316, 58), new Vector2(0, 0));

        // ---- bottom-right: halftone trail, speed, tachometer, boost
        speedDots = R("dots", h, new Vector2(1, 0), new Vector2(-400, 104), new Vector2(380, 180), new Vector2(1, 0)).gameObject.AddComponent<RawImage>();
        speedDots.texture = DotsTex(true); speedDots.color = Accent; speedDots.raycastTarget = false; acc.Add(speedDots);
        speedText = Hard(h, "0", 190, Color.white, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(-150, 104), new Vector2(1, 0), true, 1.1f);
        speedUnit = T(h, "M/S", cond, 48, Accent, TextAnchor.LowerLeft, new Vector2(1, 0), new Vector2(-140, 132), new Vector2(0, 0)); acc.Add(speedUnit);
        var uo = speedUnit.gameObject.AddComponent<Outline>(); uo.effectColor = Ink; uo.effectDistance = new Vector2(2, -2);
        for (int i = 0; i < 18; i++)
        {
            float hgt = 10 + i * 2.6f;
            tach.Add(P(h, Faint, new Vector2(1, 0), new Vector2(-470 + i * 25, 50), new Vector2(18, hgt), new Vector2(0, 0), 0.35f));
        }
        MakeTag(h, "BOOST", cond, 24, Color.white, Ink, new Vector2(1, 0), new Vector2(-690, 26), new Vector2(0, 0.5f), false, 12f, 3f);
        for (int i = 0; i < 6; i++) boostSegs.Add(P(h, Faint, new Vector2(1, 0), new Vector2(-560 + i * 52, 16), new Vector2(44, 22), new Vector2(0, 0), 0.6f));

        // ---- centre: trick sticker + points, combo, callout
        trick = MakeTag(h, "", heavy, 46, Accent, Ink, new Vector2(0.5f, 0), new Vector2(-40, 320), new Vector2(0.5f, 0.5f), true, 26f, 8f); acc.Add(trick.text);
        trickPts = MakeTag(h, "", heavy, 38, Ink, Color.white, new Vector2(0.5f, 0), new Vector2(400, 324), new Vector2(0, 0.5f), false, 16f, 6f);
        comboRoot = R("combo", h, new Vector2(0.5f, 0), new Vector2(330, 420), new Vector2(10, 10), new Vector2(0, 0.5f));
        comboText = Hard(comboRoot, "×3", 96, Color.white, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 0.5f));
        comboLabel = T(comboRoot, "COMBO", cond, 38, Color.white, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(150, 10), new Vector2(0, 0.5f));
        var co = comboLabel.gameObject.AddComponent<Outline>(); co.effectColor = Ink; co.effectDistance = new Vector2(2, -2);
        var cbar = R("bar", comboRoot, new Vector2(0, 0.5f), new Vector2(150, -24), new Vector2(170, 7), new Vector2(0, 0.5f)).gameObject.AddComponent<Image>(); cbar.color = Ink; cbar.raycastTarget = false;
        comboFill = R("fill", cbar.transform, Vector2.zero, Vector2.zero, Vector2.zero, null, Vector2.one).gameObject.AddComponent<Image>();
        comboFill.sprite = paceFill.sprite; comboFill.type = Image.Type.Filled; comboFill.fillMethod = Image.FillMethod.Horizontal; comboFill.color = Accent; comboFill.raycastTarget = false; acc.Add(comboFill);
        callout = Hard(h, "", 150, Accent, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 230), null, true, 1.2f); acc.Add(callout);
        bannerTop = MakeTag(t, "", heavy, 78, Accent, Ink, new Vector2(0.5f, 0.5f), new Vector2(0, 360), new Vector2(0.5f, 0.5f), true, 50f, 10f); acc.Add(bannerTop.text);
        bannerBot = MakeTag(t, "", cond, 38, Color.white, Ink, new Vector2(0.5f, 0.5f), new Vector2(0, 262), new Vector2(0.5f, 0.5f), false, 26f, 6f);
        trick.box.gameObject.SetActive(false); trickPts.box.gameObject.SetActive(false); comboRoot.gameObject.SetActive(false); callout.gameObject.SetActive(false);
        bannerTop.box.gameObject.SetActive(false); bannerBot.box.gameObject.SetActive(false);
        screens = R("telas", t, Vector2.zero, Vector2.zero, Vector2.zero, null, Vector2.one);
    }

    // ================================================================ shared page pieces
    readonly List<RawImage> pageDots = new List<RawImage>();
    GameObject Page(string name, float dim)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(screens, false);
        var r = (RectTransform)go.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.sizeDelta = Vector2.zero;
        var bg = Full(go.transform, new Color(Ink.r, Ink.g, Ink.b, dim)); bg.raycastTarget = true;
        go.AddComponent<BlurWhileActive>();
        var dots = R("dots", go.transform, new Vector2(0, 0), Vector2.zero, new Vector2(900, 0), new Vector2(0, 0), new Vector2(0, 1)).gameObject.AddComponent<RawImage>();
        dots.texture = DotsTex(false); dots.uvRect = new Rect(0, 0, 1, 4.2f); dots.color = new Color(Accent.r * 0.55f, Accent.g * 0.55f, Accent.b * 0.55f, 1f); dots.raycastTarget = false;
        pageDots.Add(dots);
        go.SetActive(false);
        return go;
    }

    (Tag, Text, Image) Row(Transform p, float y, float x0 = 900f, float x1 = 1830f)
    {
        var label = MakeTag(p, "", cond, 34, Color.white, Ink, new Vector2(0, 1), new Vector2(x0, -y - 40), new Vector2(0, 0.5f));
        var value = Hard(p, "", 82, Color.white, TextAnchor.MiddleRight, new Vector2(0, 1), new Vector2(x1, -y - 40), new Vector2(1, 0.5f));
        var rule = R("rule", p, new Vector2(0, 1), new Vector2(x0, -y - 100), new Vector2(x1 - x0, 3), new Vector2(0, 0.5f)).gameObject.AddComponent<Image>();
        rule.color = new Color(1, 1, 1, 0.3f); rule.raycastTarget = false;
        return (label, value, rule);
    }

    // ================================================================ menu
    // MENU: the blue/red poster (the preview you liked), alive.
    //  - its own palette: cobalt, red-orange, cream, ink (not a biome's)
    //  - the fox is LIVE: a camera renders it on a magenta key, the UISticker shader cuts it out with a cream
    //    border + ink outline. Each mode gets its own camera angle and the camera glides between them.
    //  - life: streaks always running (burst on change), falling snow, a halftone burst pulsing behind the fox,
    //    mouse parallax, a wobbling logo; a snow slope under the sled says what the game is.
    public int MenuSelection => menuSel;
    static readonly Color MBlue = new Color(0.11f, 0.18f, 1f), MBlueD = new Color(0.06f, 0.1f, 0.67f), MRed = new Color(1f, 0.25f, 0.13f),
        MCream = new Color(1f, 0.957f, 0.878f), MInk = new Color(0.055f, 0.047f, 0.12f);
    RectTransform logoRoot, foxImgRT, burstRT, slopeRT, bandRT;
    readonly List<(RectTransform rt, Para bg, Para sh, Text t, Para badge, Text num)> slabs = new List<(RectTransform, Para, Para, Text, Para, Text)>();
    Text menuTag; RawImage foxImg, burst;
    readonly List<(RectTransform rt, float speed)> streaks = new List<(RectTransform, float)>();
    readonly List<(RectTransform rt, Vector2 vel)> flakes = new List<(RectTransform, Vector2)>();
    Camera menuCam; RenderTexture menuRT; Vector3 mcPos, mcLook; float mcRoll, streakBoost; bool mcInit;
    static readonly string[] ModeLine = { "5 ENTREGAS CONTRA O RELÓGIO  ·  GORJETA DE S A E", "O MUNDO SE DESFAZ ATRÁS DE VOCÊ", "TURNO SEM FIM  ·  CADA ENTREGA MAIS RÁPIDA" };

    Text MT(Transform p, string s, Font f, int size, Color col, TextAnchor anchor, Vector2 a, Vector2 pos, Vector2? pivot = null)
    {
        // menu text that keeps the menu palette (not registered for biome recolouring)
        var t = R("txt", p, a, pos, new Vector2(10, 10), pivot).gameObject.AddComponent<Text>();
        t.font = f; t.text = s; t.fontSize = size; t.color = col; t.alignment = anchor; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
    static Para MP(Transform p, Color c, Vector2 a, Vector2 pos, Vector2 size, Vector2? pivot = null, float skew = 0.25f)
    {
        var g = R("para", p, a, pos, size, pivot).gameObject.AddComponent<Para>(); g.color = c; g.skew = skew; g.raycastTarget = false; return g;
    }
    Text Layered(Transform p, string s, int size, Color fill, Color sh1, Color sh2, Vector2 a, Vector2 pos, Vector2 pivot)
    {
        var t = MT(p, s, heavy, size, fill, TextAnchor.UpperLeft, a, pos, pivot);
        var x2 = t.gameObject.AddComponent<Shadow>(); x2.effectColor = sh2; x2.effectDistance = new Vector2(size * 0.08f, -size * 0.09f);
        var x1 = t.gameObject.AddComponent<Shadow>(); x1.effectColor = sh1; x1.effectDistance = new Vector2(size * 0.037f, -size * 0.045f);
        return t;
    }

    void BuildMenu()
    {
        menu = new GameObject("menu", typeof(RectTransform)); menu.transform.SetParent(screens, false);
        var mr = (RectTransform)menu.transform; mr.anchorMin = Vector2.zero; mr.anchorMax = Vector2.one; mr.sizeDelta = Vector2.zero;
        var bg = Full(menu.transform, MBlue); bg.raycastTarget = true;
        menu.SetActive(false);
        var m = menu.transform;

        // halftone burst radiating from behind the fox
        burstRT = R("burst", m, new Vector2(1, 0.5f), new Vector2(-480, 40), new Vector2(2000, 2000));
        burst = burstRT.gameObject.AddComponent<RawImage>(); burst.texture = BurstTex(); burst.color = MBlueD; burst.raycastTarget = false;
        // red band + ink edge on the right
        bandRT = R("faixa", m, new Vector2(1, 0.5f), new Vector2(-40, 0), new Vector2(520, 1600));
        var band = bandRT.gameObject.AddComponent<Image>(); band.color = MRed; band.raycastTarget = false; bandRT.localRotation = Quaternion.Euler(0, 0, -24f);
        var bandInk = R("tinta", bandRT, new Vector2(0, 0.5f), new Vector2(-14, 0), new Vector2(28, 1600)).gameObject.AddComponent<Image>(); bandInk.color = MInk; bandInk.raycastTarget = false;
        // streaks
        var rnd = new System.Random(4);
        for (int i = 0; i < 22; i++)
        {
            var st = R("risco", m, new Vector2(0, 0), new Vector2((float)rnd.NextDouble() * 1920f, (float)rnd.NextDouble() * 1080f), new Vector2(120 + (float)rnd.NextDouble() * 320, 3 + rnd.Next(4)));
            var im = st.gameObject.AddComponent<Image>(); im.color = new Color(MCream.r, MCream.g, MCream.b, 0.55f + (float)rnd.NextDouble() * 0.45f); im.raycastTarget = false;
            st.localRotation = Quaternion.Euler(0, 0, 8f);
            streaks.Add((st, 500f + (float)rnd.NextDouble() * 900f));
        }
        // snow slope under the sled
        slopeRT = R("encosta", m, new Vector2(1, 0), new Vector2(240, -120), new Vector2(1500, 360), new Vector2(1, 0));
        var slopeInk = slopeRT.gameObject.AddComponent<Image>(); slopeInk.color = MInk; slopeInk.raycastTarget = false;
        var slopeSnow = R("neve", slopeRT, Vector2.zero, new Vector2(0, -14), new Vector2(0, 0), null, Vector2.one).gameObject.AddComponent<Image>(); slopeSnow.color = MCream; slopeSnow.raycastTarget = false;
        slopeRT.localRotation = Quaternion.Euler(0, 0, 9f);

        // the live fox sticker
        menuRT = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32) { name = "Menu Fox", antiAliasing = 4 };
        foxImgRT = R("raposa", m, new Vector2(1, 0), new Vector2(-150, -40), new Vector2(980, 980), new Vector2(1, 0));
        foxImg = foxImgRT.gameObject.AddComponent<RawImage>(); foxImg.texture = menuRT; foxImg.raycastTarget = false;
        var sticker = Shader.Find("Sledbonk/UISticker");
        if (sticker) { var mat = new Material(sticker); mat.SetColor("_Border", MCream); mat.SetColor("_Ink", MInk); mat.SetFloat("_BorderPx", 12f); mat.SetFloat("_InkPx", 8f); foxImg.material = mat; }

        // snow flakes
        for (int i = 0; i < 46; i++)
        {
            float sz = 4 + (float)rnd.NextDouble() * 7;
            var f = R("floco", m, new Vector2(0, 0), new Vector2((float)rnd.NextDouble() * 1920f, (float)rnd.NextDouble() * 1080f), new Vector2(sz, sz));
            var im = f.gameObject.AddComponent<Image>(); im.color = new Color(MCream.r, MCream.g, MCream.b, 0.85f); im.raycastTarget = false;
            flakes.Add((f, new Vector2(-60f - (float)rnd.NextDouble() * 90f, -40f - (float)rnd.NextDouble() * 70f)));
        }

        // logo: FULL / SEND
        logoRoot = R("logo", m, new Vector2(0, 1), new Vector2(110, -60), new Vector2(10, 10), new Vector2(0, 1));
        var half = GameTitle.Length / 2;
        Layered(logoRoot, GameTitle.Substring(0, half), 250, MCream, MInk, MRed, new Vector2(0, 1), Vector2.zero, new Vector2(0, 1));
        Layered(logoRoot, GameTitle.Substring(half), 250, MRed, MInk, MCream, new Vector2(0, 1), new Vector2(110, -230), new Vector2(0, 1));
        for (int i = 0; i < 3; i++)
        {
            var cut = R("corte", logoRoot, new Vector2(0, 1), new Vector2(-80 - i * 20, -320 - i * 46), new Vector2(110 + i * 20, 10), new Vector2(0, 0.5f)).gameObject.AddComponent<Image>();
            cut.color = MCream; cut.raycastTarget = false;
        }
        logoRoot.localRotation = Quaternion.Euler(0, 0, 6f);

        // mode slabs
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var holder = R("modo", m, new Vector2(0, 1), new Vector2(-50, -660 - i * 118), new Vector2(560, 96), new Vector2(0, 0.5f));
            var sh = MP(holder, MInk, new Vector2(0.5f, 0.5f), new Vector2(12, -12), new Vector2(560, 96), null, 0.3f);
            var bgp = MP(holder, MInk, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 96), null, 0.3f); bgp.raycastTarget = true;
            var badge = MP(holder, MRed, new Vector2(0, 0.5f), new Vector2(120, 0), new Vector2(62, 62), new Vector2(0, 0.5f), 0.25f);
            var num = MT(badge.transform, (i + 1).ToString(), heavy, 56, MInk, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(4, 2));
            var t = MT(holder, ModeNames[i], heavy, 80, MCream, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(210, 0), new Vector2(0, 0.5f));
            var btn = bgp.gameObject.AddComponent<Button>(); btn.transition = Selectable.Transition.None; btn.onClick.AddListener(() => PickMode(idx));
            var et = bgp.gameObject.AddComponent<EventTrigger>();
            var e = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter }; e.callback.AddListener(_ => SelectMenu(idx)); et.triggers.Add(e);
            slabs.Add((holder, bgp, sh, t, badge, num));
        }
        menuTag = MT(m, "", cond, 38, MCream, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(170, -740), new Vector2(0, 0.5f));
        var enter = MP(m, MCream, new Vector2(1, 0), new Vector2(-70, 54), new Vector2(230, 56), new Vector2(1, 0.5f), 0.3f);
        MT(enter.transform, "ENTER  ·  JOGAR", cond, 34, MInk, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(6, 0));
        Clickable(enter, enter.rectTransform, () => PickMode(menuSel));
        var tut = MP(m, MInk, new Vector2(1, 0), new Vector2(-330, 54), new Vector2(250, 56), new Vector2(1, 0.5f), 0.3f);
        MT(tut.transform, "T  ·  TUTORIAL", cond, 34, MCream, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(6, 0));
        Clickable(tut, tut.rectTransform, TutorialFromMenu);
        SelectMenu(0);
    }

    void SelectMenu(int i)
    {
        if (slabs.Count == 0) return;
        int prev = menuSel;
        menuSel = Mathf.Clamp(i, 0, 2);
        float y = -660f;
        for (int k = 0; k < slabs.Count; k++)
        {
            bool on = k == menuSel;
            var sl = slabs[k];
            float w = sl.t.preferredWidth + 260f, h = on ? 112f : 88f;
            sl.t.fontSize = on ? 92 : 72;
            w = sl.t.preferredWidth + 260f;
            sl.rt.sizeDelta = new Vector2(w, h); sl.bg.rectTransform.sizeDelta = sl.rt.sizeDelta; sl.sh.rectTransform.sizeDelta = sl.rt.sizeDelta;
            sl.bg.color = on ? MRed : MInk; sl.t.color = on ? MInk : MCream;
            sl.badge.color = on ? MCream : MRed; sl.sh.enabled = on;
            sl.rt.anchoredPosition = new Vector2(on ? -30f : -50f, y - h * 0.5f);
            if (on) { menuTag.text = ModeLine[k]; menuTag.rectTransform.anchoredPosition = new Vector2(170, y - h - 34f); y -= h + 76f; }
            else y -= h + 22f;
        }
        if (menu && menu.activeSelf && prev != menuSel)
        {
            Punch(slabs[menuSel].rt, 1.1f, 0.2f);
            Punch(foxImgRT, 1.06f, 0.3f);
            streakBoost = 1f;
            Sfx.Play(Sfx.instance ? Sfx.instance.uiHover : null, 0.5f);
        }
    }

    // the menu's own camera: renders the fox (portrait layer) on a magenta key, one angle per mode
    void MenuCamera()
    {
        var pv = PlayerVisual.instance; if (!pv || !pv.character) return;
        if (!menuCam)
        {
            var go = new GameObject("Câmera do menu");
            menuCam = go.AddComponent<Camera>();
            menuCam.clearFlags = CameraClearFlags.SolidColor; menuCam.backgroundColor = new Color(1, 0, 1, 1);
            menuCam.cullingMask = 1 << PortraitLayer; menuCam.nearClipPlane = 0.05f; menuCam.farClipPlane = 40f;
            menuCam.targetTexture = menuRT; menuCam.depth = -9;
            var data = go.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = false; data.renderShadows = false;
        }
        menuCam.enabled = menu.activeSelf;
        if (!menu.activeSelf) return;
        var ch = pv.character; var rot = pv.root ? pv.root.rotation : ch.rotation; var c = ch.position;
        float t = Time.unscaledTime;
        Vector3 off, look; float fov, roll;
        switch (menuSel)
        {
            case 0: off = new Vector3(1.7f, 1.1f, 4.6f); look = new Vector3(0f, 0.6f, 0f); fov = 27f; roll = 0f; break;                // 3/4 front
            case 1: off = new Vector3(-2.2f, 0.3f, 4.3f); look = new Vector3(0f, 0.75f, 0f); fov = 30f; roll = -9f; break;              // low, dramatic
            default: off = new Vector3(2.4f, 3.4f, 2.6f); look = new Vector3(0f, 0.55f, 0f); fov = 25f; roll = 6f; break;               // from above
        }
        off += new Vector3(Mathf.Sin(t * 0.6f) * 0.15f, Mathf.Sin(t * 0.9f) * 0.08f, 0f);
        var pos = c + rot * off; var lk = c + rot * look;
        if (!mcInit) { mcPos = pos; mcLook = lk; mcRoll = roll; mcInit = true; }
        float k = 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime);
        mcPos = Vector3.Lerp(mcPos, pos, k); mcLook = Vector3.Lerp(mcLook, lk, k); mcRoll = Mathf.Lerp(mcRoll, roll, k);
        menuCam.fieldOfView = Mathf.Lerp(menuCam.fieldOfView, fov, k);
        menuCam.transform.position = mcPos;
        menuCam.transform.rotation = Quaternion.LookRotation(mcLook - mcPos, rot * Vector3.up) * Quaternion.Euler(0, 0, mcRoll);
    }

    void AnimateMenu(float dt)
    {
        float t = Time.unscaledTime;
        streakBoost = Mathf.MoveTowards(streakBoost, 0f, dt * 2f);
        foreach (var (rt, speed) in streaks)
        {
            var p = rt.anchoredPosition; p.x -= speed * (1f + streakBoost * 3f) * dt; p.y -= speed * 0.14f * dt;
            if (p.x < -500f) { p.x = 2300f; p.y = Random.Range(0f, 1180f); }
            rt.anchoredPosition = p;
        }
        foreach (var (rt, vel) in flakes)
        {
            var p = rt.anchoredPosition + vel * dt;
            p.x += Mathf.Sin(t * 1.3f + p.y * 0.02f) * 12f * dt;
            if (p.y < -20f || p.x < -20f) { p = new Vector2(Random.Range(200f, 2100f), 1100f); }
            rt.anchoredPosition = p;
        }
        burstRT.localScale = Vector3.one * (1f + Mathf.Sin(t * 1.6f) * 0.03f);
        burstRT.localRotation = Quaternion.Euler(0, 0, t * 3f);
        logoRoot.localRotation = Quaternion.Euler(0, 0, 6f + Mathf.Sin(t * 1.4f) * 1.4f);
        // parallax with the mouse
        var mp = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        var n = new Vector2(mp.x / Mathf.Max(1, Screen.width) - 0.5f, mp.y / Mathf.Max(1, Screen.height) - 0.5f);
        foxImgRT.anchoredPosition = new Vector2(-150f, -40f) + n * 26f;
        bandRT.anchoredPosition = new Vector2(-40f, 0f) + n * 12f;
        burstRT.anchoredPosition = new Vector2(-480f, 40f) - n * 18f;
    }

    static Texture2D BurstTex()
    {
        const int S = 512; var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[S * S]; const float cell = 18f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                // rotated grid of dots, radius grows toward the centre
                float u = (x - S / 2f), v = (y - S / 2f);
                float ru = (u + v) * 0.7071f / cell, rv = (u - v) * 0.7071f / cell;
                float d = new Vector2(ru - Mathf.Round(ru), rv - Mathf.Round(rv)).magnitude;
                float r01 = Mathf.Sqrt(u * u + v * v) / (S / 2f);
                float rad = Mathf.Clamp01(1f - r01) * 0.5f;
                px[y * S + x] = new Color(1, 1, 1, Mathf.Clamp01((rad - d) * cell * 0.9f));
            }
        tex.SetPixels(px); tex.Apply(); return tex;
    }

    // ================================================================ results
    void BuildResults()
    {
        results = Page("resultados", 0.64f);
        var p = results.transform;
        resLevel = MakeTag(p, "", cond, 44, Color.white, Ink, new Vector2(0, 1), new Vector2(60, -78), new Vector2(0, 0.5f));
        resDone = MakeTag(p, "ENTREGUE!", cond, 44, Ink, Accent, new Vector2(0, 1), new Vector2(400, -78), new Vector2(0, 0.5f)); acc.Add(resDone.bg);
        resRank = Hard(p, "S", 330, Accent, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(70, -130), new Vector2(0, 1), true, 1.8f); acc.Add(resRank);
        // the customer's card: their face (live render at the delivery address), their name and what they say
        PortraitCard(p, new Vector2(0, 1), new Vector2(430, -150), 210, new Vector2(0, 1));
        resCustomerImg = portraitImages[portraitImages.Count - 1]; portraitImages.RemoveAt(portraitImages.Count - 1);
        resCustomerImg.rectTransform.anchoredPosition = Vector2.zero; resCustomerImg.rectTransform.sizeDelta = new Vector2(240, 240);
        resCustomerName = MakeTag(p, "CLIENTE", cond, 32, Ink, Accent, new Vector2(0, 1), new Vector2(444, -390), new Vector2(0, 0.5f)); acc.Add(resCustomerName.bg);
        resQuote = MakeTag(p, "", heavy, 28, Ink, Color.white, new Vector2(0, 1), new Vector2(430, -470), new Vector2(0, 0.5f), true, 22f, 10f);
        resQuoteTail = P(resQuote.box, Color.white, new Vector2(0.2f, 1), new Vector2(0, 14), new Vector2(30, 26), new Vector2(0.5f, 0.5f), 0f);
        resQuoteTail.triangle = true;
        resNote = MakeTag(p, "", cond, 34, Ink, Accent2, new Vector2(0, 1), new Vector2(70, -560), new Vector2(0, 0.5f));
        for (int i = 0; i < 4; i++) resRows.Add(Row(p, 150 + i * 118));
        resChoose = MakeTag(p, "GASTE A GORJETA: ESCOLHA UM ITEM", cond, 36, Ink, Accent, new Vector2(0, 0), new Vector2(100, 310), new Vector2(0, 0.5f)); acc.Add(resChoose.bg);
        BuildShop(p);
        for (int i = 0; i < 3; i++) resFooter.Add(MakeTag(p, "", cond, 32, Color.white, Ink, new Vector2(1, 0), new Vector2(-60, 40), new Vector2(1, 0.5f), true, 18f, 7f));
        ClickableTag(resFooter[0], () => { RunManager.instance.Reroll(); for (int i = 0; i < cards.Count; i++) Punch(cards[i].rt, 1.06f, 0.2f); }, () => !resultsLast && RunManager.instance.sparks >= RunManager.RerollCost);
        ClickableTag(resFooter[1], () => RunManager.instance.BuyHeal(), () => !resultsLast && RunManager.instance.sparks >= RunManager.HealCost && player && player.health < player.maxHealth);
        ClickableTag(resFooter[2], () => { if (resultsLast) RunManager.instance.PickItem(0); }, () => resultsLast);
    }

    // ================================================================ death / run over / pause
    void BuildEnd()
    {
        end = Page("fim", 0.78f);
        var p = end.transform;
        endTint = Full(p, new Color(Danger.r, Danger.g, Danger.b, 0.18f)); endTint.transform.SetSiblingIndex(1);
        endTitle = Hard(p, "", 170, Danger, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(70, -60), new Vector2(0, 1), true, 1.6f);
        // wraps between words only: the size shrinks until the longest word fits ("COMPLETA" never splits)
        endTitle.horizontalOverflow = HorizontalWrapMode.Wrap; endTitle.verticalOverflow = VerticalWrapMode.Truncate;
        endTitle.rectTransform.sizeDelta = new Vector2(760, 560);
        endTitle.resizeTextForBestFit = true; endTitle.resizeTextMinSize = 56; endTitle.resizeTextMaxSize = 150;
        PortraitCard(p, new Vector2(0, 0), new Vector2(90, 90), 230, new Vector2(0, 0));
        for (int i = 0; i < 4; i++) endRows.Add(Row(p, 150 + i * 118));
        for (int i = 0; i < 2; i++) endFooter.Add(MakeTag(p, "", heavy, 44, i == 0 ? Ink : Color.white, i == 0 ? Accent : Ink, new Vector2(1, 0), new Vector2(-60, 70), new Vector2(1, 0.5f), i == 0, 22f, 6f));
        acc.Add(endFooter[0].bg);
        ClickableTag(endFooter[0], () => RunManager.instance.Restart(false));
        ClickableTag(endFooter[1], () => RunManager.instance.Restart(true));
    }

    // the pause page doubles as the Esc overlay on the other screens (menu, results, death, run over): same look, the
    // title and the options depend on where you are
    Text pauseTitle; readonly List<Tag> pauseTags = new List<Tag>(); bool overlay;
    void BuildPause()
    {
        pause = Page("pausa", 0.6f);
        var p = pause.transform;
        pauseTitle = Hard(p, "PAUSADO", 170, Accent, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 90), null, true, 1.4f); acc.Add(pauseTitle);
        string[] keys = { "ESC  ·  CONTINUAR", "R  ·  REINICIAR", "M  ·  MENU", "X  ·  SAIR DO JOGO" };
        System.Action[] acts = { EscBack, () => RunManager.instance.Restart(false), () => RunManager.instance.Restart(true), QuitGame };
        for (int i = 0; i < keys.Length; i++)
        {
            var tg = MakeTag(p, keys[i], cond, 40, Color.white, Ink, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0.5f, 0.5f), false, 24f, 10f);
            ClickableTag(tg, acts[i]); pauseTags.Add(tg);
        }
        LayoutPause("PAUSADO", "ESC  ·  CONTINUAR", true, true);
    }

    void LayoutPause(string title, string back, bool restart, bool toMenu)
    {
        pauseTitle.text = title;
        var pd = pause.transform.Find("dots"); if (pd) pd.gameObject.SetActive(!overlay);
        pauseTags[0].Set(back);
        pauseTags[1].box.gameObject.SetActive(restart); pauseTags[2].box.gameObject.SetActive(toMenu);
        float total = 0f; const float gap = 34f;
        foreach (var t in pauseTags) if (t.box.gameObject.activeSelf) total += t.box.sizeDelta.x + gap;
        float x = -(total - gap) * 0.5f;
        foreach (var t in pauseTags)
        {
            if (!t.box.gameObject.activeSelf) continue;
            t.box.anchoredPosition = new Vector2(x + t.box.sizeDelta.x * 0.5f, -70); x += t.box.sizeDelta.x + gap;
        }
    }

    // Esc on a screen that isn't the run: open / close the options overlay
    public void ToggleOverlay()
    {
        if (overlay) { overlay = false; pause.SetActive(false); return; }
        overlay = true;
        if (menu.activeSelf) LayoutPause("SAIR DO JOGO?", "ESC  ·  VOLTAR", false, false);
        else LayoutPause("OPÇÕES", "ESC  ·  VOLTAR", true, true);
        pause.SetActive(true); pause.transform.SetAsLastSibling(); LockCursor(false);
        Punch((RectTransform)pauseTitle.transform, 1.15f, 0.2f);
    }

    void EscBack() { if (overlay) ToggleOverlay(); else RunManager.instance.TogglePauseUI(); }

    static void QuitGame()
    {
        Time.timeScale = 1f;
        Application.Quit();
    }

    // ================================================================ screens API
    public void LockCursor(bool locked) { Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !locked; }
    void HideScreens() { menu.SetActive(false); results.SetActive(false); end.SetActive(false); pause.SetActive(false); overlay = false; hudRoot.gameObject.SetActive(true); }

    public void ShowMenu()
    {
        HideScreens(); hudRoot.gameObject.SetActive(false);
        menu.SetActive(true); LockCursor(false); SelectMenu(menuSel);
        Slide(logoRoot, new Vector2(-1300, 0), 0.05f, 0.5f);
        for (int i = 0; i < slabs.Count; i++) Slide(slabs[i].rt, new Vector2(-900, 0), 0.25f + 0.08f * i, 0.4f);
        Slide(foxImgRT, new Vector2(900, 0), 0.15f, 0.55f);
    }

    void PickMode(int i)
    {
        if (!menu.activeSelf) return;
        menu.SetActive(false); hudRoot.gameObject.SetActive(true);
        Sfx.Play(Sfx.instance ? Sfx.instance.uiSelect : null, 0.9f);
        RunManager.instance.Choose((RunMode)i);
    }

    public void ShowLevelIntro(string level, string biome)
    {
        HideScreens();
        bannerTop.Set((level + "  ·  " + biome).ToUpper()); FitTag(bannerTop);
        bannerBot.Set(ModeNames[(int)RunManager.mode] + "   ·   ESPAÇO NO AR: DESCIDA RÁPIDA  ·  SHIFT: BOOST (ENERGIA DOS POUSOS)"); FitTag(bannerBot);
        bannerTop.box.gameObject.SetActive(true); bannerBot.box.gameObject.SetActive(true);
        bannerTop.box.anchoredPosition = new Vector2(0, 270); bannerBot.box.anchoredPosition = new Vector2(0, 185);   // under the HUD's top bar
        Slide(bannerTop.box, new Vector2(-2200, 0), 0f, 0.45f);
        Slide(bannerBot.box, new Vector2(2200, 0), 0.12f, 0.45f);
        bannerT = 2.6f;
        Slide(portraitCards[0], new Vector2(-600, 0), 0.3f, 0.4f);
    }

    public void Countdown(float t)
    {
        int n = Mathf.CeilToInt(t);
        string s = n > 0 ? n.ToString() : "VAI!";
        if (callout.text != s)
        {
            callout.text = s; callout.gameObject.SetActive(true); callout.color = n > 0 ? Color.white : Accent; callout.fontSize = 150;
            callout.rectTransform.anchoredPosition = new Vector2(0, 40);
            callout.rectTransform.localRotation = Quaternion.Euler(0, 0, n % 2 == 0 ? 4f : -4f);
            Punch(callout.rectTransform, n > 0 ? 1.6f : 2f, 0.3f);
        }
        calloutT = n > 0 ? 0.6f : 0.8f;
    }

    void SetRow((Tag label, Text value, Image rule) r, string k, string v, bool second = false)
    {
        r.label.Set(k); FitTag(r.label); r.value.text = v;
        // a long value shrinks instead of running over its label
        r.value.fontSize = 82;
        float room = 930f - r.label.box.sizeDelta.x - 40f;
        while (r.value.preferredWidth > room && r.value.fontSize > 40) r.value.fontSize -= 4;
    }

    public void ShowResults(int rank, int bonus, bool last)
    {
        var rm = RunManager.instance; var ts = TrickSystem.instance;
        HideScreens(); hudRoot.gameObject.SetActive(false); results.SetActive(true); LockCursor(false);
        resultsLast = last;
        resLevel.Set((rm.LevelName + "  ·  " + rm.Biome.name).ToUpper()); FitTag(resLevel);
        resDone.box.anchoredPosition = new Vector2(60 + resLevel.box.sizeDelta.x + 16, -78);
        resRank.text = RunManager.RankNames[rank];
        resNote.Set(rank > 0 ? TempName(rank) + "  ·  " + RunManager.RankNames[rank - 1] + " ABAIXO DE " + Clock(RunManager.TierTime(rank - 1, rm.levelMaxTime)) : "AINDA QUENTINHA"); FitTag(resNote);
        resNote.bg.color = TempColor(rank / 5f);
        SetRow(resRows[0], "TEMPO", Clock(rm.time));
        SetRow(resRows[1], "PIZZA", SlicesLeft() + "/" + Slices + " FATIAS");
        SetRow(resRows[2], "GORJETA", "+" + (rm.sparksLevel + bonus));
        SetRow(resRows[3], "PONTOS", (ts.score - rm.scoreAtLevelStart).ToString("N0"));
        var cpl = CustomerPlace.instance;
        resCustomerImg.texture = cpl && cpl.portraitRT ? (Texture)cpl.portraitRT : portraitRT;
        resCustomerName.Set(cpl ? cpl.customerName : "CLIENTE"); FitTag(resCustomerName);
        resQuote.text.fontSize = 28; resQuote.Set(CustomerPlace.Quote(rank, SlicesLeft())); FitTag(resQuote);
        while (resQuote.box.sizeDelta.x > 440f && resQuote.text.fontSize > 18) { resQuote.text.fontSize -= 2; FitTag(resQuote); }
        if (cpl) cpl.React(rank, SlicesLeft());
        Punch(resQuote.box, 1.25f, 0.3f);
        Punch(resRank.rectTransform, 1.9f, 0.45f);
        for (int i = 0; i < resRows.Count; i++) { Slide(resRows[i].label.box, new Vector2(1200, 0), 0.2f + i * 0.08f); Slide(resRows[i].value.rectTransform, new Vector2(1200, 0), 0.24f + i * 0.08f); }
        RefreshOffer();
        resChoose.box.gameObject.SetActive(!last);
        for (int i = 0; i < cards.Count; i++) Slide(cards[i].rt, new Vector2(0, -500), 0.55f + i * 0.08f, 0.45f);
    }

    public void RefreshOffer()
    {
        var rm = RunManager.instance;
        for (int i = 0; i < cards.Count; i++)
        {
            bool on = !resultsLast && rm.offer != null && i < rm.offer.Count;
            cards[i].rt.gameObject.SetActive(on);
            if (!on) continue;
            FillShopCard(i, rm.offer[i]);
        }
        string[] f = resultsLast ? new[] { "", "", "ENTER  ·  VER O RESULTADO" }
            : new[] { "Q  ·  TROCAR ITENS  −" + RunManager.RerollCost, "E  ·  PIZZA NOVA  −" + RunManager.HealCost, "" };
        RefreshWallet();
        if (!resultsLast)
        {
            Dim(resFooter[0], rm.sparks < RunManager.RerollCost);
            Dim(resFooter[1], rm.sparks < RunManager.HealCost || (player && player.health >= player.maxHealth));
            Dim(resFooter[2], false);
        }
        else Dim(resFooter[2], false);
        float x = -60f;
        for (int i = resFooter.Count - 1; i >= 0; i--)
        {
            resFooter[i].box.gameObject.SetActive(f[i].Length > 0);
            if (f[i].Length == 0) continue;
            resFooter[i].Set(f[i]); FitTag(resFooter[i]);
            resFooter[i].text.color = i == 2 && !resultsLast ? Accent : Color.white;
            resFooter[i].box.anchoredPosition = new Vector2(x, 40); x -= resFooter[i].box.sizeDelta.x + 26f;
        }
    }

    static string Wrap(string s, int max)
    {
        var words = s.Split(' '); var sb = new System.Text.StringBuilder(); int line = 0;
        foreach (var w in words) { if (line + w.Length > max && line > 0) { sb.Append('\n'); line = 0; } else if (line > 0) { sb.Append(' '); line++; } sb.Append(w); line += w.Length; }
        return sb.ToString();
    }

    void ShowEnd(string title, Color titleColor, string[] k, string[] v, string footer)
    {
        HideScreens(); hudRoot.gameObject.SetActive(false); end.SetActive(true); LockCursor(false);
        endTitle.text = title; endTitle.color = titleColor;
        endTint.color = new Color(titleColor.r, titleColor.g, titleColor.b, 0.16f);
        for (int i = 0; i < endRows.Count; i++) { SetRow(endRows[i], k[i], v[i]); Slide(endRows[i].label.box, new Vector2(1200, 0), 0.2f + i * 0.08f); Slide(endRows[i].value.rectTransform, new Vector2(1200, 0), 0.24f + i * 0.08f); }
        endFooter[0].Set("R  " + footer); FitTag(endFooter[0]);
        endFooter[1].Set("M  MENU"); FitTag(endFooter[1]);
        endFooter[1].box.anchoredPosition = new Vector2(-60, 70);
        endFooter[0].box.anchoredPosition = new Vector2(-60 - endFooter[1].box.sizeDelta.x - 26, 70);
        Punch(endTitle.rectTransform, 1.5f, 0.4f);
    }

    public void ShowDeath(string why)
    {
        var rm = RunManager.instance; var ts = TrickSystem.instance;
        ShowEnd(why, Danger,
            new[] { "MODO", "DISTÂNCIA", "NOTAS", "PONTOS" },
            new[] { ModeNames[(int)RunManager.mode] + " · " + rm.LevelName.ToUpper(), Mathf.RoundToInt(rm.progress) + " / " + Mathf.RoundToInt(Track.instance.Length) + " M", rm.ranks.Count > 0 ? string.Join(" ", rm.ranks) : "-", ts.score.ToString("N0") },
            "TENTAR DE NOVO");
    }

    public void ShowRunOver()
    {
        var rm = RunManager.instance; var ts = TrickSystem.instance;
        ShowEnd("TODAS ENTREGUES!", Accent,
            new[] { "NOTAS", "TEMPO TOTAL", "PONTOS", "TRUQUES" },
            new[] { string.Join(" ", rm.ranks), Clock(rm.runTime), ts.score.ToString("N0"), ts.tricksLanded + "  ·  " + ts.bails + " QUEDAS" },
            "NOVA CORRIDA");
    }

    public void ShowPause(bool on)
    {
        overlay = false; pause.SetActive(on); LockCursor(!on);
        if (on) { LayoutPause("PAUSADO", "ESC  ·  CONTINUAR", true, true); bannerTop.box.gameObject.SetActive(false); bannerBot.box.gameObject.SetActive(false); bannerT = 0f; }
    }

    int SlicesLeft() => player ? Mathf.Clamp(Mathf.CeilToInt(player.health / Mathf.Max(1f, player.maxHealth) * Slices - 0.001f), 0, Slices) : Slices;

    // a lost slice leaves the pizza, spinning off with a little gravity
    void ThrowSlice(int k)
    {
        var src = slices[k].rectTransform;
        var rt = R("fatia voando", hudRoot, new Vector2(0, 0), Vector2.zero, src.sizeDelta, new Vector2(0.5f, 0f));
        rt.position = src.position; rt.rotation = src.rotation;
        var img = rt.gameObject.AddComponent<RawImage>(); img.texture = slices[k].texture; img.raycastTarget = false;
        float a = (90f - k * 360f / Slices) * Mathf.Deg2Rad;
        flyers.Add(new Flyer { rt = rt, img = img, v = new Vector2(Mathf.Cos(a), Mathf.Abs(Mathf.Sin(a)) + 0.6f) * Random.Range(420f, 620f) + new Vector2(260f, 0f), spin = Random.Range(-540f, 540f) });
    }

    void UpdateFlyers(float dt)
    {
        for (int i = flyers.Count - 1; i >= 0; i--)
        {
            var f = flyers[i]; f.t += dt;
            f.v += Vector2.down * 1800f * dt;
            f.rt.anchoredPosition += f.v * dt;
            f.rt.Rotate(0f, 0f, f.spin * dt);
            f.img.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((f.t - 0.6f) / 0.3f));
            if (f.t > 0.9f) { Destroy(f.rt.gameObject); flyers.RemoveAt(i); }
        }
    }

    static Texture2D TempTex()
    {
        var t = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int x = 0; x < 256; x++) t.SetPixel(x, 0, TempColor(x / 255f));
        t.Apply(); return t;
    }

    static Texture2D DiscTex()
    {
        const int N = 128; var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float d = new Vector2(x - N / 2f + 0.5f, y - N / 2f + 0.5f).magnitude / (N / 2f);
            px[y * N + x] = new Color(1, 1, 1, Mathf.Clamp01((1f - d) * N / 2f));
        }
        t.SetPixels(px); t.Apply(); return t;
    }

    public static string Clock(float t) { int m = Mathf.FloorToInt(t / 60f); float s = t - m * 60f; return m + ":" + s.ToString("00.0", System.Globalization.CultureInfo.InvariantCulture); }

    // ================================================================ in-run feedback
    public void FlashDamage() { flashT = 0.3f; if (portraitCards.Count > 0) Punch(portraitCards[0], 1.12f, 0.25f); }

    public void Landing(LandingType t)
    {
        if (t == LandingType.None) return;
        string word; Color col; int size; float punch, life, rot;
        switch (t)
        {
            case LandingType.Perfect: word = "PERFEITO!"; col = Accent; size = 150; punch = 1.75f; life = 0.85f; rot = Random.value < 0.5f ? -4f : 4f; break;
            case LandingType.Good: word = "BOM!"; col = Accent2; size = 112; punch = 1.4f; life = 0.65f; rot = Random.value < 0.5f ? -2f : 2f; break;
            case LandingType.Ok: word = "OK"; col = Color.white; size = 80; punch = 1.2f; life = 0.45f; rot = 0f; break;
            default: word = "AMASSOU!"; col = Danger; size = 112; punch = 1.3f; life = 0.7f; rot = -7f; break;
        }
        callout.text = word; callout.color = col; callout.fontSize = size;
        callout.rectTransform.anchoredPosition = new Vector2(0, 210);
        callout.gameObject.SetActive(true);
        callout.rectTransform.localRotation = Quaternion.Euler(0, 0, rot);
        Punch(callout.rectTransform, punch, 0.28f);
        if (t == LandingType.Bad) Slide(callout.rectTransform, new Vector2(60f, 0f), 0f, 0.25f);   // a jolt
        if (portraitCards.Count > 0) Punch(portraitCards[0], t == LandingType.Perfect ? 1.15f : t == LandingType.Bad ? 0.85f : 1.06f, 0.3f);
        calloutT = life;
    }

    void Feed(string text, int pts, Color c)
    {
        bool bad = c == TrickSystem.Red;
        trick.Set(text.ToUpper()); FitTag(trick);
        trick.text.color = bad ? Danger : Accent;
        trick.box.gameObject.SetActive(true); Punch(trick.box, 1.22f, 0.22f);
        trickPts.box.gameObject.SetActive(pts > 0);
        if (pts > 0)
        {
            trickPts.Set("+" + pts.ToString("N0")); FitTag(trickPts);
            trickPts.box.anchoredPosition = new Vector2(-40 + trick.box.sizeDelta.x * 0.5f + 26f, 324);
            Punch(trickPts.box, 1.3f, 0.25f);
        }
        trickHold = 1.7f;
    }

    CanvasScaler scaler;
    // Taller than 16:9 (16:10, 4:3): follow the width so nothing is cut at the sides; wider (21:9): follow the height.
    void FitScaler() { if (scaler) scaler.matchWidthOrHeight = (float)Screen.width / Mathf.Max(1, Screen.height) < 16f / 9f ? 0f : 1f; }

    void Update()
    {
        FitScaler();
        var rm = RunManager.instance; var ts = TrickSystem.instance;
        float udt = Time.unscaledDeltaTime;
        UpdateTweens(udt);
        UpdateButtons(udt);
        UpdateLoading(udt);
        UpdateShop(udt);
        if (!player || !rm || !ts) return;
        bool running = rm.state == RunManager.State.Run || rm.state == RunManager.State.Countdown;
        var tr = Track.instance;

        // top: pace line, moving rank markers (where you'd need to be now for each grade), time, rank
        levelTag.Set((rm.LevelName + "  ·  " + rm.Biome.name).ToUpper()); FitTag(levelTag);
        float L = Mathf.Max(1f, tr.Length);
        paceFill.fillAmount = rm.progress / L;
        playerMark.anchoredPosition = new Vector2(800f * rm.progress / L, -18f);
        for (int i = 0; i < paceTicks.Count; i++)
        {
            float need = rm.time / Mathf.Max(0.01f, RunManager.TierTime(i, rm.levelMaxTime));
            bool show = need < 1f && rm.time > 0.5f;
            paceTicks[i].tick.gameObject.SetActive(show);
            paceTicks[i].tick.anchoredPosition = new Vector2(800f * Mathf.Clamp01(need), 0f);
            paceTicks[i].label.enabled = i == 0 || paceTicks[i - 1].tick.anchoredPosition.x - paceTicks[i].tick.anchoredPosition.x > 30f;
        }
        timeText.text = Clock(rm.time);
        int live = rm.LiveRank();
        string rk = RunManager.RankNames[live];
        if (rankText.text != rk)
        {
            rankText.text = rk; Punch(rankBadge, 1.3f, 0.25f);
            tempTag.Set(TempName(live)); FitTag(tempTag); tempTag.bg.color = TempColor(live / 5f); Punch(tempTag.box, 1.2f, 0.2f);
        }
        chaseText.text = rm.Chase && rm.chaseGap < 2000f ? "COLAPSO  " + Mathf.Max(0, Mathf.RoundToInt(rm.chaseGap)) + " M" : "";
        vignette.color = new Color(Collapse.r, Collapse.g, Collapse.b, Mathf.Lerp(vignette.color.a, rm.danger * 0.5f, udt * 4f));

        // bottom-left: the pizza (lost slices fly off) + score
        int left = SlicesLeft();
        if (left < lastSlices) { for (int k = left; k < lastSlices; k++) ThrowSlice(k); Punch(pizzaRoot, 0.85f, 0.25f); }
        else if (left > lastSlices) Punch(pizzaRoot, 1.15f, 0.25f);
        lastSlices = left;
        for (int k = 0; k < slices.Count; k++) slices[k].enabled = k < left;
        sliceText.text = left + "/" + Slices;
        UpdateFlyers(udt);
        scoreText.text = (ts.score + ts.comboPoints).ToString("N0");

        // bottom-right: speed + tachometer + boost
        float v = player.Speed; int vs = Mathf.RoundToInt(v);
        speedText.text = vs.ToString();
        if (vs / 25 != (int)lastSpeed / 25 && vs > lastSpeed) Punch(speedText.rectTransform, 1.08f, 0.15f);
        lastSpeed = vs;
        int lit = Mathf.RoundToInt(Mathf.Clamp01(v / 170f) * tach.Count);
        for (int i = 0; i < tach.Count; i++)
            tach[i].color = i < lit ? (i < 10 ? Accent : i < 15 ? Color.white : Danger) : Faint;
        int segs = Mathf.FloorToInt(ts.boostMeter * boostSegs.Count + 0.001f);
        for (int i = 0; i < boostSegs.Count; i++) boostSegs[i].color = i < segs ? (ts.boosting ? Color.white : Accent2) : Faint;
        speedDots.color = new Color(Accent.r, Accent.g, Accent.b, Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(60f, 140f, v)));

        // centre: live trick in white while in the air, the landed one in colour for a moment
        string liveTrick = ts.LiveTrick;
        if (!string.IsNullOrEmpty(liveTrick))
        {
            trick.Set(liveTrick.ToUpper()); FitTag(trick); trick.text.color = Color.white; trick.box.gameObject.SetActive(true);
            trickPts.box.gameObject.SetActive(false);
            if (liveTrick != lastLive) Punch(trick.box, 1.1f, 0.15f);
            trickHold = 0.4f;
        }
        lastLive = liveTrick;
        if (trickHold > 0f) { trickHold -= udt; if (trickHold <= 0f) { trick.box.gameObject.SetActive(false); trickPts.box.gameObject.SetActive(false); } }
        bool comboOn = ts.comboChain > 1 && ts.comboPoints > 0;
        comboRoot.gameObject.SetActive(comboOn);
        if (comboOn)
        {
            comboText.text = "×" + ts.ComboMult.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            comboLabel.rectTransform.anchoredPosition = new Vector2(comboText.preferredWidth + 18f, 14f);
            comboFill.transform.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(comboText.preferredWidth + 18f, -24f);
            comboFill.fillAmount = ts.comboTimer / (ts.comboWindow + PlayerMods.instance.comboWindow);
            if (!Mathf.Approximately(comboShown, ts.ComboMult)) Punch(comboRoot, 1.3f, 0.2f);
            comboShown = ts.ComboMult;
        }
        else comboShown = 0f;

        if (calloutT > 0f) { calloutT -= udt; if (calloutT <= 0f) callout.gameObject.SetActive(false); }
        if (bannerT > 0f) { bannerT -= udt; if (bannerT <= 0f) { bannerTop.box.gameObject.SetActive(false); bannerBot.box.gameObject.SetActive(false); } }
        if (flashT > 0f) { flashT -= udt; flash.color = new Color(1f, 0.1f, 0.15f, flashT * 0.6f); }

        float sl = Mathf.Clamp01((v - 85f) / 60f) * (player.data.isGrounded ? 0.55f : 0.8f) * (ts.boosting ? 1.4f : 1f);
        speedLines.color = new Color(1, 1, 1, Mathf.Lerp(speedLines.color.a, running ? sl * 0.3f : 0f, udt * 6f));
        speedLines.rectTransform.localRotation = Quaternion.Euler(0, 0, Random.Range(-2f, 2f));

        MenuCamera();
        if (menu.activeSelf) AnimateMenu(udt);
        Keys(rm);
    }

    void Keys(RunManager rm)
    {
        var k = Keyboard.current; if (k == null) return;
        bool onScreen = menu.activeSelf || results.activeSelf || end.activeSelf;
        if (k.escapeKey.wasPressedThisFrame && onScreen && rm.state != RunManager.State.Paused) { ToggleOverlay(); return; }
        if (overlay || pause.activeSelf)
        {
            if (k.xKey.wasPressedThisFrame) QuitGame();
            else if (pauseTags[1].box.gameObject.activeSelf && k.rKey.wasPressedThisFrame) rm.Restart(false);
            else if (pauseTags[2].box.gameObject.activeSelf && k.mKey.wasPressedThisFrame) rm.Restart(true);
            return;
        }
        if (menu.activeSelf)
        {
            if (k.digit1Key.wasPressedThisFrame) PickMode(0);
            else if (k.digit2Key.wasPressedThisFrame) PickMode(1);
            else if (k.digit3Key.wasPressedThisFrame) PickMode(2);
            else if (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame) SelectMenu((menuSel + 2) % 3);
            else if (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame) SelectMenu((menuSel + 1) % 3);
            else if (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame) PickMode(menuSel);
            else if (k.tKey.wasPressedThisFrame) TutorialFromMenu();
            return;
        }
        if (results.activeSelf)
        {
            if (resultsLast) { if (k.enterKey.wasPressedThisFrame || k.digit1Key.wasPressedThisFrame) rm.PickItem(0); return; }
            if (k.digit1Key.wasPressedThisFrame) rm.PickItem(0);
            else if (k.digit2Key.wasPressedThisFrame) rm.PickItem(1);
            else if (k.digit3Key.wasPressedThisFrame) rm.PickItem(2);
            else if (k.qKey.wasPressedThisFrame) { rm.Reroll(); for (int i = 0; i < cards.Count; i++) Punch(cards[i].rt, 1.06f, 0.2f); }
            else if (k.eKey.wasPressedThisFrame) { rm.BuyHeal(); if (resFooter.Count > 1) Punch(resFooter[1].box, 1.15f, 0.2f); }
            return;
        }
        if (end.activeSelf)
        {
            if (k.rKey.wasPressedThisFrame) rm.Restart(false);
            else if (k.mKey.wasPressedThisFrame) rm.Restart(true);
        }
        if (rm.state == RunManager.State.Run && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);
    }

    void OnDestroy() { UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= OnBeginCamera; if (portraitRT) portraitRT.Release(); }

    // ================================================================ procedural textures
    static Texture2D SpeedLineTex()
    {
        const int S = 512; var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        var rnd = new System.Random(3); var bins = new float[720];
        for (int i = 0; i < bins.Length; i++) bins[i] = rnd.NextDouble() < 0.16 ? 0.4f + (float)rnd.NextDouble() * 0.6f : 0f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - S / 2f) / (S / 2f), dy = (y - S / 2f) / (S / 2f);
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = (Mathf.Atan2(dy, dx) / Mathf.PI * 0.5f + 0.5f) * bins.Length;
                tex.SetPixel(x, y, new Color(1, 1, 1, bins[(int)a % bins.Length] * Mathf.Clamp01((r - 0.55f) / 0.45f) * 0.8f));
            }
        tex.Apply(); return tex;
    }

    static Texture2D VignetteTex()
    {
        const int S = 256; var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - S / 2f) / (S / 2f), dy = (y - S / 2f) / (S / 2f);
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(0f, 1f, (Mathf.Sqrt(dx * dx + dy * dy) - 0.5f) / 0.7f)));
            }
        tex.Apply(); return tex;
    }
}

// ring on the ground, coloured by the landing grade you would get right now.
public class LandingMarker : MonoBehaviour
{
    MeshRenderer mr, targetR; MaterialPropertyBlock mpb; Transform target;
    public void Init(Material mat)
    {
        var mf = gameObject.AddComponent<MeshFilter>(); mf.sharedMesh = Ring(1f, 0.82f);
        mr = gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mpb = new MaterialPropertyBlock();
        mr.enabled = false;
        var tg = new GameObject("alvo"); tg.transform.SetParent(transform.parent, false); target = tg.transform;
        tg.AddComponent<MeshFilter>().sharedMesh = Ring(1f, 0.6f);
        targetR = tg.AddComponent<MeshRenderer>(); targetR.sharedMaterial = mat;
        targetR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; targetR.enabled = false;
    }

    void LateUpdate()
    {
        var pc = PlayerCharacter.instance; var ts = TrickSystem.instance;
        if (!pc || pc.data.movementType != MovementType.Fast || pc.data.isGrounded || ts && ts.grinding || pc.data.sinceGrounded < 0.15f) { mr.enabled = targetR.enabled = false; return; }
        float sinceAir = pc.data.sinceGrounded;
        var p = pc.transform.position; var v = pc.rig.linearVelocity;
        float g = pc.stats.gravity; float dt = 0.04f;
        for (float t = 0f; t < 5f; t += dt)
        {
            var acc = Vector3.down * g;
            if (pc.input.fastFallIsPressed) acc += Vector3.down * pc.stats.fastFallSpeed;
            v += acc * dt;
            var next = p + v * dt;
            if (Physics.Linecast(p + Vector3.up * 0.5f, next + Vector3.up * 0.5f, out var hit, PlayerCharacter.RunnableMask, QueryTriggerInteraction.Ignore))
            {
                if (t < 0.25f) break;
                var after = Vector3.Lerp(Vector3.ProjectOnPlane(v, hit.normal), v, 0.65f);
                float score = after.magnitude / Mathf.Max(0.01f, v.magnitude);
                float len = pc.movement.landingLeniency;
                Color c = score < 0.7f - len ? new Color(1f, 0.25f, 0.25f) : score < 0.9f - len ? new Color(1f, 1f, 1f) : score < 0.95f - len ? new Color(0.4f, 1f, 1f) : new Color(0.45f, 1f, 0.5f);
                mpb.SetColor("_BaseColor", c); mpb.SetColor("_Emission", c * 1.2f); mr.SetPropertyBlock(mpb);
                transform.SetPositionAndRotation(hit.point + hit.normal * 0.25f, Quaternion.FromToRotation(Vector3.up, hit.normal));
                bool perfect = score >= 0.95f - len;
                float pulse = perfect ? 1f + Mathf.Sin(Time.time * 18f) * 0.08f : 1f;
                transform.localScale = Vector3.one * Mathf.Lerp(3f, 7f, t / 3f) * pulse;
                mr.enabled = sinceAir > 0.3f;
                return;
            }
            p = next;
        }
        mr.enabled = targetR.enabled = false;
    }

    static Mesh Ring(float outer, float inner)
    {
        const int n = 48; var v = new Vector3[n * 2]; var tri = new int[n * 6];
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f; var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            v[i * 2] = d * outer; v[i * 2 + 1] = d * inner;
            int j = (i + 1) % n;
            tri[i * 6] = i * 2; tri[i * 6 + 1] = i * 2 + 1; tri[i * 6 + 2] = j * 2;
            tri[i * 6 + 3] = j * 2; tri[i * 6 + 4] = i * 2 + 1; tri[i * 6 + 5] = j * 2 + 1;
        }
        var m = new Mesh { name = "ring" }; m.vertices = v; m.triangles = tri; m.RecalculateNormals(); return m;
    }
}

// turns on the full-screen blur while a page (menu, results, death, pause) is open
public class BlurWhileActive : MonoBehaviour
{
    static int open;
    void OnEnable() { open++; Shader.SetGlobalFloat("_FxBlur", 7f); }
    void OnDisable() { open = Mathf.Max(0, open - 1); if (open == 0) Shader.SetGlobalFloat("_FxBlur", 0f); }
}
