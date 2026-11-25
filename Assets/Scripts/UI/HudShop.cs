using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Hud, part 3: the item shop on the delivery screen. Three comic cards like stickers on the results page: on the left a
// panel in the item's rarity colour (halftone) with the item's icon (Resources/ItemIcons, drawn by
// Tools/pizza/item_icons.py) popping out of it, the key as an ink chip on the corner; on the right the name big, the
// rarity, and the description in ink on cream. A hovered card lifts, straightens and shows "PEGAR". Above them:
// "ESCOLHA 1 ITEM · GRÁTIS" and your tip money; below them: what the money buys (trocar / pizza nova), greyed out when
// you can't.
public partial class Hud
{
    class ShopCard
    {
        public RectTransform holder, inner, icon; public Para panel, body; public RawImage dots, iconImg; public Text name, desc;
        public Tag rarity, key, take; public float tilt, iconTilt; public Btn btn;
    }
    readonly List<ShopCard> shop = new List<ShopCard>();
    Tag resWallet;
    static readonly Color Cream = new Color(1f, 0.957f, 0.878f);
    const float ShopY = 372f;

    static readonly Dictionary<string, string> IconOf = new Dictionary<string, string>
    {
        { "Marmita Térmica", "marmita_termica" }, { "Giro Rápido", "giro_rapido" }, { "Turbina", "turbina" },
        { "Bateria Extra", "bateria_extra" }, { "Ímã de Gorjeta", "ima_gorjeta" }, { "Pizza Família", "pizza_familia" },
        { "Prancha Leve", "prancha_leve" }, { "Gravitron", "gravitron" }, { "Combo Longo", "combo_longo" },
        { "Gorjeta em Dobro", "gorjeta_dobro" }, { "Caixa Reforçada", "caixa_reforcada" }, { "Pouso Explosivo", "pouso_explosivo" },
        { "Trilho Turbo", "trilho_turbo" }, { "Forno Portátil", "forno_portatil" }, { "Embalo", "embalo" },
        { "Amortecedor", "amortecedor" }, { "Anel Fantasma", "anel_fantasma" },
    };
    readonly Dictionary<string, Texture2D> iconCache = new Dictionary<string, Texture2D>();
    Texture2D ItemIcon(string item)
    {
        if (!IconOf.TryGetValue(item, out var slug)) slug = "pizza_familia";
        if (!iconCache.TryGetValue(slug, out var t)) iconCache[slug] = t = Resources.Load<Texture2D>("ItemIcons/" + slug);
        return t;
    }

    void BuildShop(Transform p)
    {
        resChoose.Set("ESCOLHA 1 ITEM  ·  GRÁTIS"); FitTag(resChoose);
        resChoose.box.anchoredPosition = new Vector2(100, ShopY);
        resWallet = MakeTag(p, "", heavy, 34, Ink, Accent, new Vector2(0, 0), new Vector2(100, ShopY), new Vector2(0, 0.5f), true, 20f, 6f); acc.Add(resWallet.bg);
        const float W = 540, H = 214, PW = 196;
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var c = new ShopCard { tilt = (i - 1) * -1.6f, iconTilt = (i - 1) * 5f + 4f };
            c.holder = R("card", p, new Vector2(0, 0), new Vector2(100 + i * 590, 96), new Vector2(W, H), new Vector2(0, 0));
            c.inner = R("cartão", c.holder, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H));
            var shadow = P(c.inner, Ink, new Vector2(0.5f, 0.5f), new Vector2(12, -12), new Vector2(W, H), null, 0.1f);
            c.body = P(c.inner, Cream, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H), null, 0.1f);
            // the rarity panel with halftone, and an ink rule between it and the text
            c.panel = P(c.inner, Color.white, new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(PW, H), new Vector2(0, 0.5f), 0.1f);
            c.panel.raycastTarget = false;
            c.dots = R("pontos", c.panel.transform, new Vector2(0, 0), Vector2.zero, new Vector2(PW, H), new Vector2(0, 0)).gameObject.AddComponent<RawImage>();
            c.dots.texture = DotsTex(false); c.dots.uvRect = new Rect(0, 0, 1f, H / PW * 2f); c.dots.raycastTarget = false;
            var rule = P(c.inner, Ink, new Vector2(0, 0.5f), new Vector2(PW - 2, 0), new Vector2(8, H), new Vector2(0, 0.5f), 0.1f); rule.raycastTarget = false;
            // the icon pops out of the panel
            c.icon = R("ícone", c.inner, new Vector2(0, 0.5f), new Vector2(PW * 0.5f + 4, 14), new Vector2(206, 206));
            c.iconImg = c.icon.gameObject.AddComponent<RawImage>(); c.iconImg.raycastTarget = false;
            c.icon.localRotation = Quaternion.Euler(0, 0, c.iconTilt);
            // key chip on the corner
            c.key = MakeTag(c.inner, (i + 1).ToString(), heavy, 40, Accent, Ink, new Vector2(0, 1), new Vector2(-14, 18), new Vector2(0, 1), true, 16f, 2f);
            c.name = T(c.inner, "", heavy, 40, Ink, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(PW + 34, -46), new Vector2(0, 0.5f));
            c.rarity = MakeTag(c.inner, "", cond, 24, Color.white, Ink, new Vector2(0, 1), new Vector2(PW + 34, -88), new Vector2(0, 0.5f), false, 12f, 3f);
            c.desc = T(c.inner, "", cond, 29, Ink, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(PW + 34, -112), new Vector2(0, 1));
            c.take = MakeTag(c.inner, "PEGAR  ·  " + (i + 1), heavy, 28, Ink, Accent, new Vector2(1, 0), new Vector2(-16, 8), new Vector2(1, 0.5f), true, 16f, 5f);
            acc.Add(c.take.bg); c.take.box.gameObject.SetActive(false);
            Clickable(c.body, c.inner, () => RunManager.instance.PickItem(idx));
            c.btn = buttons[buttons.Count - 1];
            shadow.transform.SetSiblingIndex(1);
            shop.Add(c);
            cards.Add((c.holder, c.panel, c.name, c.rarity.text, c.desc));
        }
    }

    void FillShopCard(int i, Item it)
    {
        var c = shop[i]; var rc = Item.RarityColor[it.rarity];
        c.panel.color = rc;
        c.dots.color = new Color(rc.r * 0.62f, rc.g * 0.62f, rc.b * 0.62f, 0.85f);
        c.iconImg.texture = ItemIcon(it.name);
        c.name.text = it.name.ToUpper(); c.name.fontSize = 40;
        while (c.name.preferredWidth > 296f && c.name.fontSize > 26) c.name.fontSize -= 2;
        c.rarity.Set(Item.RarityName[it.rarity].ToUpper()); c.rarity.text.color = rc; FitTag(c.rarity);
        c.desc.text = Wrap(it.desc.ToUpper(), 22); c.desc.fontSize = 29;
        while (c.desc.preferredHeight > 96f && c.desc.fontSize > 22) c.desc.fontSize -= 1;
    }

    void RefreshWallet()
    {
        var rm = RunManager.instance; if (!rm || resWallet == null) return;
        resWallet.Set("GORJETA  " + rm.sparks); FitTag(resWallet);
        resWallet.box.anchoredPosition = new Vector2(100 + resChoose.box.sizeDelta.x + 22, ShopY);
        resWallet.box.gameObject.SetActive(!resultsLast);
    }

    void UpdateShop(float dt)
    {
        foreach (var c in shop)
        {
            if (!c.holder.gameObject.activeInHierarchy) continue;
            bool hover = c.btn != null && c.btn.hover;
            float k = 1f - Mathf.Exp(-14f * dt);
            var pos = c.inner.anchoredPosition; pos.y = Mathf.Lerp(pos.y, hover ? 16f : 0f, k); c.inner.anchoredPosition = pos;
            float z = Mathf.LerpAngle(c.inner.localEulerAngles.z, hover ? 0f : c.tilt, k);
            c.inner.localRotation = Quaternion.Euler(0, 0, z);
            float iz = Mathf.LerpAngle(c.icon.localEulerAngles.z, hover ? c.iconTilt - 8f + Mathf.Sin(Time.unscaledTime * 9f) * 3f : c.iconTilt, k);
            c.icon.localRotation = Quaternion.Euler(0, 0, iz);
            float s = Mathf.Lerp(c.icon.localScale.x, hover ? 1.12f : 1f, k); c.icon.localScale = new Vector3(s, s, 1f);
            if (c.take.box.gameObject.activeSelf != hover) { c.take.box.gameObject.SetActive(hover); if (hover) Punch(c.take.box, 1.25f, 0.2f); }
        }
    }
}
