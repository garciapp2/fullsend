using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Hud, part 4: the tutorial / hint prompt. One comic box low in the middle of the screen: a small title in the accent
// colour ("NO AR", "CAIU DO TRENÓ!"), then a row of keycaps (cream, ink letters, hard ink shadow) and what they do, and
// optionally a bar that fills (the Shift charge back onto the sled). Driven by Tutorial.cs.
public partial class Hud
{
    RectTransform promptRoot, promptRow; Para promptBg, promptStripe, promptBarBg, promptBar; Text promptTitle;
    readonly List<GameObject> promptBits = new List<GameObject>();
    string promptKey = ""; float promptFill = -1f;

    void BuildPrompt()
    {
        promptRoot = R("tutorial", hudRoot, new Vector2(0.5f, 1f), new Vector2(0, -250), new Vector2(900, 150));
        var sh = P(promptRoot, Ink, new Vector2(0.5f, 0.5f), new Vector2(12, -12), new Vector2(900, 150), null, 0.12f);
        promptBg = P(promptRoot, Ink, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 150), null, 0.12f);
        sh.color = new Color(0, 0, 0, 0.35f);
        promptStripe = P(promptRoot, Accent, new Vector2(0, 0.5f), new Vector2(6, 0), new Vector2(18, 150), new Vector2(0, 0.5f), 0.12f); acc.Add(promptStripe);
        promptTitle = T(promptRoot, "", heavy, 30, Accent, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(50, -30), new Vector2(0, 0.5f)); acc.Add(promptTitle);
        promptRow = R("linha", promptRoot, new Vector2(0, 0.5f), new Vector2(50, -14), new Vector2(10, 70), new Vector2(0, 0.5f));
        promptBarBg = P(promptRoot, new Color(1, 1, 1, 0.18f), new Vector2(0, 0), new Vector2(50, 16), new Vector2(400, 12), new Vector2(0, 0.5f), 0.4f);
        promptBar = P(promptRoot, Accent, new Vector2(0, 0), new Vector2(50, 16), new Vector2(0, 12), new Vector2(0, 0.5f), 0.4f); acc.Add(promptBar);
        promptRoot.gameObject.SetActive(false);
    }

    // parts: "[W]" is a keycap, anything else is text. e.g. Prompt("NO AR", "[ESPAÇO]", "SEGURE PRA DESCER RÁPIDO")
    public void Prompt(string title, params string[] parts)
    {
        string key = title + "|" + string.Join("|", parts);
        if (promptRoot.gameObject.activeSelf && key == promptKey) return;
        bool fresh = !promptRoot.gameObject.activeSelf;
        promptKey = key;
        foreach (var b in promptBits) Destroy(b);
        promptBits.Clear();
        promptTitle.text = title;
        float x = 0f;
        foreach (var part in parts)
        {
            if (part.StartsWith("[") && part.EndsWith("]")) x += Keycap(part.Substring(1, part.Length - 2), x) + 12f;
            else
            {
                var t = T(promptRow, part, heavy, 40, Color.white, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(x + 6f, 0), new Vector2(0, 0.5f));
                promptBits.Add(t.gameObject); x += t.preferredWidth + 22f;
            }
        }
        float w = Mathf.Max(x + 70f, promptTitle.preferredWidth + 100f, 520f);
        foreach (var p in new[] { promptBg, promptBg.transform.parent.GetChild(0).GetComponent<Para>() }) p.rectTransform.sizeDelta = new Vector2(w, 150);
        promptRoot.sizeDelta = new Vector2(w, 150);
        promptBarBg.rectTransform.sizeDelta = new Vector2(w - 100f, 12f);
        promptRoot.gameObject.SetActive(true);
        if (fresh) { promptRoot.localScale = Vector3.one; promptRoot.anchoredPosition = new Vector2(0, -250); Slide(promptRoot, new Vector2(0, 60), 0f, 0.3f); }
        Punch(promptRoot, 1.08f, 0.2f);
        Sfx.Play(Sfx.instance ? Sfx.instance.uiSelect : null, 0.5f, 1.3f);
    }

    float Keycap(string label, float x)
    {
        var hold = R("tecla", promptRow, new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(10, 64), new Vector2(0, 0.5f));
        promptBits.Add(hold.gameObject);
        var t0 = T(hold, label, heavy, 36, Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 2));
        float w = Mathf.Max(64f, t0.preferredWidth + 34f);
        hold.sizeDelta = new Vector2(w, 64);
        var shd = P(hold, Accent, new Vector2(0.5f, 0.5f), new Vector2(5, -6), new Vector2(w, 64), null, 0.06f);
        var cap = P(hold, new Color(1f, 0.957f, 0.878f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, 64), null, 0.06f);
        shd.transform.SetAsFirstSibling(); cap.transform.SetSiblingIndex(1);
        return w;
    }

    public void PromptFill(float f)
    {
        promptFill = f;
        bool on = f >= 0f;
        promptBarBg.gameObject.SetActive(on); promptBar.gameObject.SetActive(on);
        if (on) promptBar.rectTransform.sizeDelta = new Vector2(promptBarBg.rectTransform.sizeDelta.x * Mathf.Clamp01(f), 12f);
    }

    public void HidePrompt()
    {
        if (!promptRoot || !promptRoot.gameObject.activeSelf) return;
        promptRoot.gameObject.SetActive(false); promptKey = "";
    }

    // little "ISSO!" pop when a step is done
    public void PromptDone(string text)
    {
        Prompt("", text);
        PromptFill(-1f);
        Punch(promptRoot, 1.25f, 0.3f);
    }

    // menu: replay the tutorial
    void TutorialFromMenu()
    {
        if (!menu.activeSelf || overlay) return;
        Tutorial.forceNext = true;
        PickMode(0);
    }
}
