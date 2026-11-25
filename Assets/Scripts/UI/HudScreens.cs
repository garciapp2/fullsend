using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Hud, part 2: clickable buttons (every key-labelled action also works with the mouse, with hover and press feedback,
// and greys out with a "can't" shake when it can't be used) and the loading screen shown while a delivery is built
// (the pizza assembles slice by slice with the real progress of the generation, the phase, the delivery and a tip).
public partial class Hud
{
    // ---------------------------------------------------------------- buttons
    class Btn { public RectTransform root; public Graphic hit; public Para glow; public bool hover; public System.Func<bool> can; }
    readonly List<Btn> buttons = new List<Btn>();

    void Clickable(Graphic hit, RectTransform root, System.Action click, System.Func<bool> can = null)
    {
        hit.raycastTarget = true;
        var b = new Btn { root = root, hit = hit, can = can };
        // a slanted accent plate behind it lights up on hover
        b.glow = P(root, Accent, new Vector2(0.5f, 0.5f), new Vector2(-6, 6), root.sizeDelta + new Vector2(14, 10)); acc.Add(b.glow);
        b.glow.transform.SetAsFirstSibling(); b.glow.enabled = false;
        var btn = hit.gameObject.GetComponent<Button>(); if (!btn) btn = hit.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None; btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            if (b.can != null && !b.can()) { Nudge(root); Sfx.Play(Sfx.instance ? Sfx.instance.wallHit : null, 0.35f, 1.6f); return; }
            Punch(root, 1.15f, 0.2f);
            click();
        });
        var et = hit.gameObject.GetComponent<EventTrigger>(); if (!et) et = hit.gameObject.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => { b.hover = true; Sfx.Play(Sfx.instance ? Sfx.instance.uiHover : null, 0.35f); });
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => b.hover = false);
        et.triggers.Add(enter); et.triggers.Add(exit);
        buttons.Add(b);
    }

    void ClickableTag(Tag t, System.Action click, System.Func<bool> can = null) => Clickable(t.bg, t.box, click, can);

    // a little "no" shake
    readonly List<(RectTransform rt, Vector2 at, float t)> nudges = new List<(RectTransform, Vector2, float)>();
    void Nudge(RectTransform rt) { nudges.RemoveAll(n => n.rt == rt); nudges.Add((rt, rt.anchoredPosition, 0.3f)); }

    void UpdateButtons(float dt)
    {
        foreach (var b in buttons)
        {
            if (!b.root || !b.root.gameObject.activeInHierarchy) { b.hover = false; continue; }
            bool can = b.can == null || b.can();
            b.glow.enabled = b.hover && can;
            if (b.glow.enabled) b.glow.rectTransform.sizeDelta = b.root.sizeDelta + new Vector2(14, 10);
        }
        for (int i = nudges.Count - 1; i >= 0; i--)
        {
            var n = nudges[i]; n.t -= dt;
            if (!n.rt) { nudges.RemoveAt(i); continue; }
            n.rt.anchoredPosition = n.at + new Vector2(Mathf.Sin(n.t * 60f) * 10f * Mathf.Clamp01(n.t / 0.3f), 0f);
            if (n.t <= 0f) { n.rt.anchoredPosition = n.at; nudges.RemoveAt(i); } else nudges[i] = n;
        }
    }

    // grey a tag out (can't afford / nothing to do) or back to normal
    void Dim(Tag t, bool off)
    {
        t.bg.color = off ? new Color(0.25f, 0.25f, 0.3f, 0.85f) : Ink;
        t.text.color = off ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
    }

    // ---------------------------------------------------------------- loading
    GameObject loading;
    Text loadTitle, loadPhase, loadPct, loadTip;
    Tag loadLevel;
    RectTransform loadPizza, loadStreaks;
    readonly List<RawImage> loadSlices = new List<RawImage>();
    RawImage loadDots;
    Image loadBg;
    float loadShown = -1f, loadTarget, loadValue, loadHide = -1f;
    static readonly string[] Tips =
    {
        "POUSE ALINHADO COM A DESCIDA: POUSO PERFEITO DÁ ENERGIA PRO BOOST.",
        "SHIFT GASTA A ENERGIA DOS POUSOS. SEM POUSO BOM, SEM BOOST.",
        "ESPAÇO NO AR = DESCIDA RÁPIDA. SOLTE ANTES DE ENCOSTAR NO CHÃO.",
        "A CÚPULA LARANJA É O RAIO DA BOMBA. FICOU 0,35 S DENTRO, EXPLODE.",
        "COLUNA E MARCA NO CHÃO PISCANDO: ALGO VAI CAIR ALI.",
        "LINHA VERMELHA NO AR É A MIRA DE UM MÍSSIL. O PONTO É ONDE ELE CAI.",
        "CRISTAIS VERDES BRILHANDO: O LASER DE MINERAÇÃO VAI DESCER.",
        "ANÉIS DE IMPULSO DÃO VELOCIDADE. NOS ORBES DE LASER, ELES FICAM LÁ DENTRO.",
        "PASSOU DO TEMPO MÁXIMO + 5 S, A PIZZA ESFRIA E A ENTREGA ACABA.",
        "BATER DE FRENTE EM ESTRUTURA DÓI MUITO. DE RASPÃO, SÓ UM POUCO.",
        "ENTRE AS ENTREGAS, GASTE A GORJETA: UM ITEM, UMA PIZZA NOVA OU TROCAR AS OPÇÕES.",
    };

    void BuildLoading()
    {
        loading = new GameObject("carregando", typeof(RectTransform)); loading.transform.SetParent(transform, false);
        var r = (RectTransform)loading.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.sizeDelta = Vector2.zero;
        var p = loading.transform;
        loadBg = Full(p, MBlue); loadBg.raycastTarget = true;
        loadDots = R("pontos", p, Vector2.zero, Vector2.zero, Vector2.zero, null, Vector2.one).gameObject.AddComponent<RawImage>();
        loadDots.texture = DotsTex(false); loadDots.uvRect = new Rect(0, 0, 4f, 2.25f); loadDots.color = MBlueD; loadDots.raycastTarget = false;
        // speed streaks rushing left
        loadStreaks = R("riscos", p, Vector2.zero, Vector2.zero, Vector2.zero, null, Vector2.one);
        var rnd = new System.Random(11);
        for (int i = 0; i < 26; i++)
        {
            var st = R("risco", loadStreaks, new Vector2(0, 0), new Vector2((float)rnd.NextDouble() * 1920f, (float)rnd.NextDouble() * 1080f), new Vector2(160 + (float)rnd.NextDouble() * 420, 4 + rnd.Next(5)));
            var im = st.gameObject.AddComponent<Image>(); im.color = new Color(MCream.r, MCream.g, MCream.b, 0.35f + (float)rnd.NextDouble() * 0.4f); im.raycastTarget = false;
            st.localRotation = Quaternion.Euler(0, 0, 8f);
        }
        // a red band behind the pizza
        var band = R("faixa", p, new Vector2(1, 0.5f), new Vector2(-380, 0), new Vector2(560, 1700)).gameObject.AddComponent<Image>();
        band.color = MRed; band.raycastTarget = false; band.rectTransform.localRotation = Quaternion.Euler(0, 0, -18f);
        // the pizza assembling: 8 slices pop in with the progress, the whole thing spins
        loadPizza = R("pizza", p, new Vector2(1, 0.5f), new Vector2(-380, 30), new Vector2(420, 420));
        var plate = R("prato", loadPizza, new Vector2(0.5f, 0.5f), new Vector2(14, -16), new Vector2(440, 440)).gameObject.AddComponent<RawImage>();
        plate.texture = DiscTex(); plate.color = MInk; plate.raycastTarget = false;
        var plate2 = R("prato2", loadPizza, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440, 440)).gameObject.AddComponent<RawImage>();
        plate2.texture = DiscTex(); plate2.color = MCream; plate2.raycastTarget = false;
        var sliceTex = Resources.Load<Texture2D>("PizzaSliceUI");
        float sr = 205f, aspect = sliceTex ? (float)sliceTex.width / sliceTex.height : 0.83f;
        for (int k = 0; k < Slices; k++)
        {
            var sl = R("fatia", loadPizza, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(sr * aspect, sr), new Vector2(0.5f, 0f));
            sl.localRotation = Quaternion.Euler(0f, 0f, -k * 360f / Slices);
            var ri = sl.gameObject.AddComponent<RawImage>(); ri.texture = sliceTex; ri.raycastTarget = false;
            loadSlices.Add(ri);
        }
        loadPct = Layered(p, "0%", 96, MCream, MInk, MRed, new Vector2(1, 0.5f), new Vector2(-380, -260), new Vector2(0.5f, 1));
        loadPct.alignment = TextAnchor.UpperCenter;
        // title + delivery + phase + tip on the left
        loadTitle = Layered(p, "SAINDO PRA\nENTREGA", 150, MCream, MInk, MRed, new Vector2(0, 1), new Vector2(110, -90), new Vector2(0, 1));
        loadTitle.rectTransform.localRotation = Quaternion.Euler(0, 0, 4f);
        loadLevel = MakeTag(p, "", cond, 46, MInk, MCream, new Vector2(0, 1), new Vector2(120, -470), new Vector2(0, 0.5f), false, 22f, 8f);
        loadPhase = MT(p, "", cond, 40, MCream, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(126, -560), new Vector2(0, 0.5f));
        var tipBox = MP(p, MInk, new Vector2(0, 0), new Vector2(100, 90), new Vector2(1060, 130), new Vector2(0, 0), 0.12f);
        var tipTag = MP(tipBox.transform, MRed, new Vector2(0, 1), new Vector2(26, 24), new Vector2(120, 46), new Vector2(0, 0.5f), 0.2f);
        MT(tipTag.transform, "DICA", cond, 34, MCream, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(4, 0));
        loadTip = MT(tipBox.transform, "", cond, 34, MCream, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(40, -36), new Vector2(0, 1));
        loadTip.horizontalOverflow = HorizontalWrapMode.Wrap; loadTip.rectTransform.sizeDelta = new Vector2(980, 90);
        loading.SetActive(false);
    }

    public void ShowLoading(string level, string biome)
    {
        if (!loading) return;
        loading.SetActive(true); loading.transform.SetAsLastSibling();
        loadLevel.Set((level + "  ·  " + biome).ToUpper()); FitTag(loadLevel);
        loadTip.text = Tips[Random.Range(0, Tips.Length)];
        loadTarget = 0f; loadValue = 0f; loadShown = Time.unscaledTime; loadHide = -1f;
        loadPhase.text = "";
        foreach (var s in loadSlices) s.enabled = false;
        Slide(loadTitle.rectTransform, new Vector2(-1400, 0), 0f, 0.45f);
        Slide(loadLevel.box, new Vector2(-1400, 0), 0.08f, 0.45f);
        Slide(loadPizza, new Vector2(900, 0), 0.05f, 0.5f);
        LockCursor(false);
    }

    public void SetLoading(float progress, string phase)
    {
        loadTarget = Mathf.Max(loadTarget, Mathf.Clamp01(progress));
        if (!string.IsNullOrEmpty(phase) && loadPhase) loadPhase.text = phase + "...";
    }

    public void HideLoading() { if (loading && loading.activeSelf) { loadTarget = 1f; loadHide = Time.unscaledTime + 0.35f; } }
    public bool LoadingVisible => loading && loading.activeSelf;

    void UpdateLoading(float dt)
    {
        if (!loading || !loading.activeSelf) return;
        loadValue = Mathf.MoveTowards(loadValue, loadTarget, dt * 2.5f);
        int shown = Mathf.Clamp(Mathf.FloorToInt(loadValue * Slices + 0.001f), 0, Slices);
        for (int k = 0; k < loadSlices.Count; k++)
        {
            bool on = k < shown;
            if (on && !loadSlices[k].enabled) Punch(loadSlices[k].rectTransform, 1.35f, 0.25f);
            loadSlices[k].enabled = on;
        }
        loadPizza.localRotation = Quaternion.Euler(0, 0, -Time.unscaledTime * 40f);
        loadPct.text = Mathf.RoundToInt(loadValue * 100f) + "%";
        loadDots.uvRect = new Rect(Time.unscaledTime * 0.15f, 0, 4f, 2.25f);
        foreach (RectTransform st in loadStreaks)
        {
            var q = st.anchoredPosition; q.x -= dt * (900f + st.sizeDelta.x * 1.5f);
            if (q.x < -st.sizeDelta.x) q.x = 1920f + st.sizeDelta.x;
            st.anchoredPosition = q;
        }
        if (loadHide > 0f && Time.unscaledTime > loadHide && loadValue >= 0.999f)
        {
            loading.SetActive(false); loadHide = -1f;
        }
    }
}
