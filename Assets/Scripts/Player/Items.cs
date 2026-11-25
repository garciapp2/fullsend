using System.Collections.Generic;
using UnityEngine;

// most stack. PlayerMods holds the numbers the systems read.
public class PlayerMods : MonoBehaviour
{
    public static PlayerMods instance;
    public float ollie = 1f, energyGain = 1f, spin = 1f, boostAccel = 1f, boostDrain = 1f, sparkMagnet = 1f, sparkValue = 1f;
    public float comboWindow = 0f, perfectSpeed = 0f, trickSpeed = 0f, grindAccel = 0f, regenAtSpeed = 0f, ringOnPerfect = 0f;
    public int bailShield, bailShieldLeft;
    public readonly List<Item> owned = new List<Item>();
    void Awake() { instance = this; }
    public void NewLevel() { bailShieldLeft = bailShield; }
}

public class Item
{
    public string name, desc; public int rarity; public System.Action<PlayerCharacter, PlayerMods> apply; public int max = 5;
    public static readonly Color[] RarityColor = { new Color(0.75f, 0.85f, 1f), new Color(0.45f, 1f, 0.65f), new Color(1f, 0.55f, 0.9f), new Color(1f, 0.8f, 0.3f) };
    public static readonly string[] RarityName = { "comum", "incomum", "raro", "lendário" };

    public static List<Item> All()
    {
        return new List<Item>
        {
            new Item { name = "Marmita Térmica", desc = "Pousos dão 35% mais energia de boost.", rarity = 0, apply = (p, m) => m.energyGain += 0.35f },
            new Item { name = "Giro Rápido", desc = "Piruetas e mortais 25% mais rápidos.", rarity = 0, apply = (p, m) => m.spin += 0.25f },
            new Item { name = "Turbina", desc = "Boost acelera 30% mais.", rarity = 0, apply = (p, m) => m.boostAccel += 0.3f },
            new Item { name = "Bateria Extra", desc = "Boost dura 30% mais.", rarity = 0, apply = (p, m) => m.boostDrain *= 0.7f },
            new Item { name = "Ímã de Gorjeta", desc = "Pega gorjetas de muito mais longe.", rarity = 0, apply = (p, m) => m.sparkMagnet += 0.8f },
            new Item { name = "Pizza Família", desc = "Pizza maior: aguenta 25% mais e chega inteira.", rarity = 0, apply = (p, m) => { p.maxHealth += 20f; p.health = p.maxHealth; } },
            new Item { name = "Prancha Leve", desc = "Curva 20% melhor, no chão e no ar.", rarity = 1, apply = (p, m) => { p.stats.turnSpeed *= 1.2f; p.stats.airSpeed *= 1.2f; } },
            new Item { name = "Gravitron", desc = "Descida rápida 50% mais forte.", rarity = 1, apply = (p, m) => p.stats.fastFallSpeed *= 1.5f },
            new Item { name = "Combo Longo", desc = "+1 s para emendar truques num combo.", rarity = 1, apply = (p, m) => m.comboWindow += 1f },
            new Item { name = "Gorjeta em Dobro", desc = "Cada gorjeta vale 2.", rarity = 1, apply = (p, m) => m.sparkValue += 1f, max = 2 },
            new Item { name = "Caixa Reforçada", desc = "Um tombo por entrega não te freia.", rarity = 1, apply = (p, m) => m.bailShield++ },
            new Item { name = "Pouso Explosivo", desc = "Pouso perfeito: +12 de velocidade.", rarity = 2, apply = (p, m) => m.perfectSpeed += 12f },
            new Item { name = "Trilho Turbo", desc = "Grind acelera em vez de frear.", rarity = 2, apply = (p, m) => m.grindAccel += 25f },
            new Item { name = "Forno Portátil", desc = "Acima de 100 m/s a pizza se recupera.", rarity = 2, apply = (p, m) => m.regenAtSpeed += 3f },
            new Item { name = "Embalo", desc = "Cada truque acertado: +4 de velocidade.", rarity = 2, apply = (p, m) => m.trickSpeed += 4f },
            new Item { name = "Amortecedor", desc = "Pousos são julgados com mais folga.", rarity = 2, apply = (p, m) => p.movement.landingLeniency += 0.03f, max = 2 },
            new Item { name = "Anel Fantasma", desc = "Pouso perfeito cria um anel de impulso na sua frente.", rarity = 3, apply = (p, m) => m.ringOnPerfect += 1f, max = 1 },
        };
    }

    public static List<Item> Roll(System.Random r, int n, PlayerMods mods)
    {
        var pool = new List<Item>();
        foreach (var it in All())
        {
            int have = mods.owned.FindAll(x => x.name == it.name).Count;
            if (have >= it.max) continue;
            int weight = it.rarity == 0 ? 6 : it.rarity == 1 ? 4 : it.rarity == 2 ? 2 : 1;
            for (int k = 0; k < weight; k++) pool.Add(it);
        }
        var picked = new List<Item>();
        while (picked.Count < n && pool.Count > 0)
        {
            var it = pool[r.Next(pool.Count)];
            pool.RemoveAll(x => x.name == it.name);
            picked.Add(it);
        }
        return picked;
    }
}
