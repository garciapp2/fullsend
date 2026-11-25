using System.Collections.Generic;
using UnityEngine;

//            target pace; close to it you take damage, inside it you die).
//  Infinito: levels never end; the collapse is always on and gets faster every level.
public enum RunMode { Corrida, Fuga, Infinito }

public class RunManager : MonoBehaviour
{
    public static RunManager instance;
    public static RunMode mode = RunMode.Corrida;
    public static bool chosen;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { chosen = false; mode = RunMode.Corrida; }

    public enum State { Menu, Countdown, Run, Results, Dead, RunOver, Paused, Loading }
    public State state = State.Menu;
    public PlayerCharacter player;
    public Light sun;
    public Material chaseMat;
    public int levelsPerRun = 5;
    public float levelSpeed = 80f;

    public int level, seed;
    public float time, runTime, levelMaxTime, progress, countdown;
    public int sparks, sparksLevel, scoreAtLevelStart;
    public float chaseGap = 9999f, danger;
    public readonly List<string> ranks = new List<string>();
    public string lastRank;
    public List<Item> offer;
    public BiomeDef Biome => Track.instance.biome;
    State before;
    Transform chaseWall; Corruption corruption;
    string deathReason;
    public float ChaseLag = 600f;
    readonly Dictionary<Transform, float> ringCooldown = new Dictionary<Transform, float>();
    int lastIdx;
    float chaseTick;

    public static readonly string[] RankNames = { "S", "A", "B", "C", "D", "E" };
    public static readonly Color[] RankColors = { new Color(1f, 0.82f, 0.25f), new Color(0.45f, 1f, 0.6f), new Color(0.45f, 0.85f, 1f), new Color(0.8f, 0.7f, 1f), new Color(1f, 0.6f, 0.4f), new Color(0.75f, 0.75f, 0.8f) };
    static readonly float[] TierMult = { 0.5f, 0.4f, 0.3f, 0.2f, 0.1f, 0f };

    public bool Chase => mode != RunMode.Corrida;
    public string LevelName => mode == RunMode.Infinito ? "Entrega " + (level + 1) : "Entrega " + (level + 1) + "/" + levelsPerRun;

    void Awake() { instance = this; if (!GetComponent<Tutorial>()) gameObject.AddComponent<Tutorial>(); }

    // still climbing in Infinito.
    public float Intensity => Mathf.Lerp(0.7f, 1.3f, Mathf.Clamp01(level / 4f)) + (mode == RunMode.Infinito ? Mathf.Max(0, level - 4) * 0.1f : 0f);

    void Start()
    {
        Obst.InitLayers();
        seed = Random.Range(1, 999999);
        player.onDeath += () => Die(deathReason ?? (Chase && chaseGap < 5f ? "O MUNDO TE ENGOLIU" : "A PIZZA VIROU PURÊ"));
        player.onLanding += OnLanding;
        if (!player.GetComponent<HazardSensor>()) player.gameObject.AddComponent<HazardSensor>();
        corruption = Corruption.Build(); chaseWall = corruption.transform;
        level = 0;
        StartCoroutine(LoadLevel(() =>
        {
            if (!chosen) { state = State.Menu; Freeze(true); Hud.instance.ShowMenu(); }   // time keeps running: the scene lives behind the menu
            else StartCountdown();
        }));
    }

    // Builds the delivery a little each frame behind the loading screen (the pizza fills with the real progress).
    System.Collections.IEnumerator LoadLevel(System.Action then)
    {
        state = State.Loading;
        Freeze(true);
        var lib = BiomeLibrary.instance;
        var b = lib.biomes[(level + seed) % lib.biomes.Length];
        Hud.instance.ShowLoading(LevelName, b.name);
        yield return null; yield return null;   // let the screen show before the heavy work
        var it = Track.instance.GenerateRoutine(seed + level * 7919, mode == RunMode.Infinito ? Mathf.Min(level, 6) : level, b, (p, s) => Hud.instance.SetLoading(p, s));
        while (it.MoveNext()) yield return null;
        FinishLevel(b);
        Hud.instance.SetLoading(1f, "PIZZA NA CAIXA");
        float until = Time.unscaledTime + 0.6f;
        while (Time.unscaledTime < until) yield return null;
        Hud.instance.HideLoading();
        then?.Invoke();
    }

    public void Choose(RunMode m)
    {
        mode = m; chosen = true;
        Time.timeScale = 1f;
        StartCountdown();
    }

    // ------------------------------------------------------------ levels
    void BeginLevel()
    {
        var lib = BiomeLibrary.instance;
        var b = lib.biomes[(level + seed) % lib.biomes.Length];
        Track.instance.Generate(seed + level * 7919, mode == RunMode.Infinito ? Mathf.Min(level, 6) : level, b);
        FinishLevel(b);
    }

    void FinishLevel(BiomeDef b)
    {
        var lib = BiomeLibrary.instance;
        lib.Apply(b, sun);
        if (Sfx.instance && b.music != null && b.music.Length > 0) Sfx.instance.SetMusic(b.music[0], b.ambience);
        PlaceAtStart();
        float speed = levelSpeed * (mode == RunMode.Infinito ? 1f + level * 0.06f : 1f + level * 0.03f);
        levelMaxTime = Track.instance.Length / speed;
        time = 0f; progress = 0f; sparksLevel = 0; chaseGap = 9999f; danger = 0f; deathReason = null;
        if (chaseWall) chaseWall.gameObject.SetActive(false);
        if (Sfx.instance) Sfx.instance.danger = 0f;
        scoreAtLevelStart = TrickSystem.instance.score;
        ringCooldown.Clear();
        PlayerMods.instance.NewLevel();
    }

    void PlaceAtStart()
    {
        var tr = Track.instance;
        var pos = tr.PointAt(8) + Vector3.up * 1.2f;
        Freeze(false);
        player.rig.position = pos; player.transform.position = pos; player.data.lastPos = pos;
        player.rig.linearVelocity = Vector3.zero;
        player.movement.DisableMode(); player.slowMovement.EnableMode();
        player.SetLookDirection(tr.T[8]);
        player.dead = false;
        Physics.SyncTransforms();
    }

    void StartCountdown()
    {
        state = State.Countdown; countdown = 2.99f;
        Freeze(true);
        Hud.instance.LockCursor(true);
        Hud.instance.ShowLevelIntro(LevelName, Biome.name);
    }

    // hold the player still (countdown, results) without making the body kinematic
    void Freeze(bool on)
    {
        player.rig.linearVelocity = Vector3.zero;
        player.slowMovement.enabled = !on && player.data.movementType == MovementType.Slow;
        player.movement.enabled = !on && player.data.movementType == MovementType.Fast;
        if (on) { player.slowMovement.enabled = false; player.movement.enabled = false; }
    }

    void Go()
    {
        state = State.Run;
        Freeze(false);
        player.slowMovement.GoFast();
        if (Tutorial.instance) Tutorial.instance.OnLevelStart(level);
        Sfx.Play(Sfx.instance ? Sfx.instance.go : null, 0.8f);
        if (CameraRig.instance) CameraRig.instance.AddFovImpulse(60f);
        GameFeel.Shake(4f, 0.3f);
        ComicFX.Impact(0.9f, 0f, Hud.instance.Accent, 0.35f);
    }

    void Update()
    {
        if (state == State.Menu && chaseWall)
        {
            // Fuga shot: the collapse looms over the start
            bool show = Hud.instance && Hud.instance.MenuSelection == 1;
            chaseWall.gameObject.SetActive(show);
            if (show)
            {
                var tr0 = Track.instance;
                corruption.Set(tr0.PointAt(0) - tr0.T[0] * 150f, 150f);
            }
        }
        var k = UnityEngine.InputSystem.Keyboard.current;
        if (k != null && k.escapeKey.wasPressedThisFrame && (state == State.Run || state == State.Countdown || state == State.Paused)) TogglePause();
        if (state == State.Countdown)
        {
            int before = Mathf.CeilToInt(countdown);
            countdown -= Time.deltaTime;
            int now = Mathf.CeilToInt(countdown);
            if (now != before && now > 0) Sfx.Play(Sfx.instance ? Sfx.instance.countdown : null, 0.6f, 1f);
            Hud.instance.Countdown(countdown);
            var p = Track.instance.PointAt(8) + Vector3.up * 1.2f;
            player.rig.position = p; player.transform.position = p; player.rig.linearVelocity = Vector3.zero;
            if (countdown <= 0f) Go();
            return;
        }
        if (state != State.Run) return;
        float dt = Time.deltaTime;
        time += dt; runTime += dt;
        var tr = Track.instance;
        var pp = player.transform.position;
        progress = tr.Progress(pp, out int idx, out float lat);
        lastIdx = idx;
        var b = Biome;
        if (Sfx.instance && b.music != null && b.music.Length >= 3) Sfx.instance.SetMusic(b.music[Mathf.Clamp((int)(progress / tr.Length * 3f), 0, 2)], b.ambience);

        Sparks(pp);
        Rings(pp);
        var mods = PlayerMods.instance;
        if (mods.regenAtSpeed > 0f && player.Speed > 100f) player.Heal(mods.regenAtSpeed * dt);

        // fell into a chasm / off the world
        float roadY = tr.P[idx].y;
        if (pp.y < roadY - 40f || pp.y < tr.HeightAt(pp.x, pp.z) - 25f) Fall();

        // (the tutorial delivery has neither the cold-pizza timer nor the collapse)
        if (time > levelMaxTime + 5f && !player.dead && !Tutorial.Active) { deathReason = "A PIZZA ESFRIOU"; player.Kill(); return; }
        if (Chase && !Tutorial.Active) UpdateChase(dt);
        else if (chaseWall && chaseWall.gameObject.activeSelf) chaseWall.gameObject.SetActive(false);
        if (progress >= tr.finishIndex * tr.step - 10f) LevelComplete();
    }

    void Sparks(Vector3 pp)
    {
        var tr = Track.instance; var mods = PlayerMods.instance;
        float r = 3.2f * mods.sparkMagnet, pull = r * 3f;
        float spin = Time.time * 180f;
        foreach (var s in tr.sparks)
        {
            if (!s.gameObject.activeSelf) continue;
            var d = pp + Vector3.up - s.position; float sq = d.sqrMagnitude;
            if (sq > 14400f) continue;
            s.rotation = Quaternion.Euler(0f, spin, 0f);
            if (sq < r * r)
            {
                s.gameObject.SetActive(false);
                int v = Mathf.RoundToInt(mods.sparkValue);
                sparks += v; sparksLevel += v;
                TrickSystem.instance.AddPoints(10 * v, 0.012f);
                if (Sfx.instance) Sfx.instance.Spark();
            }
            else if (sq < pull * pull) s.position += d.normalized * Mathf.Min(Mathf.Sqrt(sq), (player.Speed + 20f) * Time.deltaTime);
        }
    }

    void Rings(Vector3 pp)
    {
        var tr = Track.instance;
        foreach (var ring in tr.rings)
        {
            if (!ring) continue;
            if (ringCooldown.TryGetValue(ring, out float cd) && Time.time < cd) continue;
            float rad = 5.5f * ring.localScale.x;
            var d = pp + Vector3.up - ring.position;
            if (d.sqrMagnitude > rad * rad) continue;
            ringCooldown[ring] = Time.time + 1.5f;
            Boost(ring.forward, 22f);
            Sfx.Play(Sfx.instance ? Sfx.instance.ring : null, 0.9f);
            TrickSystem.instance.AddPoints(150, 0.08f);
        }
    }

    public void Boost(Vector3 dir, float add)
    {
        var v = player.rig.linearVelocity; float s = v.magnitude;
        var nd = Vector3.Slerp(v.normalized, dir.normalized, 0.35f);
        player.rig.linearVelocity = nd * Mathf.Min(s + add, 190f);
        if (CameraRig.instance) CameraRig.instance.AddFovImpulse(50f + 3f * add);
        ComicFX.Impact(0.6f);
        GameFeel.Shake(1.65f * add * 0.2f, 0.3f);
    }

    void Fall()
    {
        player.TakeDamage(20f, player.transform.position);
        if (player.dead) return;
        Track.instance.Respawn(player, progress);
        player.data.invulnerability = 1.2f;
        if (TrickSystem.instance.airborne) TrickSystem.instance.Bail("Caiu no abismo!");
        if (CameraRig.instance) CameraRig.instance.AddFovImpulse(-30f);
    }

    void UpdateChase(float dt)
    {
        var tr = Track.instance;
        float pace = tr.Length / levelMaxTime;
        float paceS = time * pace;
        float m = Mathf.Clamp01(paceS / 800f);             // ramps in over the first 800 m
        float killR = 320f * m, hurtR = 450f * m;
        float wallS = paceS - ChaseLag, centerS = wallS - killR;
        chaseGap = m > 0.2f ? progress - wallS : 9999f;
        danger = Mathf.Clamp01(Mathf.InverseLerp(310f * m, 0f, chaseGap));
        if (Sfx.instance) Sfx.instance.danger = danger;
        chaseWall.gameObject.SetActive(m > 0.2f && wallS > -300f);
        if (chaseWall.gameObject.activeSelf)
        {
            // the dark edge sits on the kill line (wallS); the white cracks run ~half a radius ahead of it
            int ci = Mathf.Clamp(Mathf.RoundToInt(centerS / tr.step), 0, tr.Count - 1);
            var center = tr.PointAt(ci);
            if (centerS < 0f) center -= tr.T[0] * -centerS;
            corruption.Set(center, killR);
            corruption.SetDanger(danger);
            corruption.Crumble(tr, wallS, center);
        }
        if (chaseGap < 0f) { player.Kill(); return; }
        chaseTick -= dt;
        float band = hurtR - killR;
        if (chaseGap < band && chaseTick <= 0f) { chaseTick = 1f; player.TakeDamage(15f * Mathf.InverseLerp(band, 0f, chaseGap), player.transform.position); }
    }

    // ------------------------------------------------------------ landing hooks
    void OnLanding(LandingType t, float speed)
    {
        if (Sfx.instance) Sfx.instance.Landing(t);
        Hud.instance.Landing(t);
        if (PlayerVisual.instance) PlayerVisual.instance.LandSquash(t == LandingType.Perfect ? 0.35f : 0.2f);
        var mods = PlayerMods.instance;
        // An ok landing costs speed only (PlayerMovement scrubs it), so riding without timing the stomp is slow, not lethal.
        if (state == State.Run)
        {
            if (t == LandingType.Bad) player.TakeDamage(30f * Intensity, player.transform.position);
        }
        if (t == LandingType.Good) ComicFX.Impact(0.35f);
        if (t == LandingType.Bad) ComicFX.Split(1.2f);
        if (t == LandingType.Perfect)
        {
            ComicFX.Impact(1f, 0.07f, Hud.instance.Accent, 0.18f);
            if (CameraRig.instance) CameraRig.instance.AddFovImpulse(35f);
            if (mods.perfectSpeed > 0f) Boost(player.rig.linearVelocity, mods.perfectSpeed);
            if (mods.ringOnPerfect > 0f)
            {
                var v = player.rig.linearVelocity; v.y = 0f;
                Track.instance.SpawnRing(player.transform.position + v.normalized * 70f + Vector3.up * 3f, v.normalized);
            }
        }
    }

    // ------------------------------------------------------------ results / items
    public static int RankFor(float t, float max)
    {
        float b = 0.85f * max;
        float[] f = { 1f, 0.8f, 0.6f, 0.4f, 0.2f };
        for (int i = 0; i < f.Length; i++) if (t < Mathf.Lerp(max, b, f[i])) return i;
        return 5;
    }

    public static float TierTime(int tier, float max) => tier >= 5 ? max : Mathf.Lerp(max, 0.85f * max, new[] { 1f, 0.8f, 0.6f, 0.4f, 0.2f }[tier]);

    public int LiveRank()
    {
        var tr = Track.instance;
        for (int i = 0; i < 5; i++) if (progress >= time * tr.Length / TierTime(i, levelMaxTime) - 1f) return i;
        return 5;
    }

    void LevelComplete()
    {
        state = State.Results;
        int rank = RankFor(time, levelMaxTime);
        lastRank = RankNames[rank];
        ranks.Add(lastRank);
        int bonus = Mathf.RoundToInt((sparksLevel + 20) * TierMult[rank]) + 20;
        sparks += bonus;
        TrickSystem.instance.ForceBank();
        Freeze(true);
        ComicFX.Impact(1.3f, 0f, Color.white, 0.6f);
        Sfx.Play(Sfx.instance ? Sfx.instance.levelEnd : null, 0.9f);
        bool last = mode != RunMode.Infinito && level + 1 >= levelsPerRun;
        offer = last ? null : Item.Roll(new System.Random(seed + level * 31 + ranks.Count), 3, PlayerMods.instance);
        Time.timeScale = 0f;
        Hud.instance.ShowResults(rank, bonus, last);
    }

    public void PickItem(int i)
    {
        if (state != State.Results) return;
        if (offer == null) { RunOver(); return; }
        if (i < 0 || i >= offer.Count) return;
        var it = offer[i];
        it.apply(player, PlayerMods.instance);
        PlayerMods.instance.owned.Add(it);
        Sfx.Play(Sfx.instance ? Sfx.instance.itemPick : null, 1f);
        NextLevel();
    }

    public void Reroll()
    {
        if (state != State.Results || offer == null || sparks < RerollCost) return;
        sparks -= RerollCost;
        offer = Item.Roll(new System.Random(Random.Range(0, 999999)), 3, PlayerMods.instance);
        Sfx.Play(Sfx.instance ? Sfx.instance.uiSelect : null, 0.8f);
        Hud.instance.RefreshOffer();
    }

    public void BuyHeal()
    {
        if (state != State.Results || sparks < HealCost || player.health >= player.maxHealth) return;
        sparks -= HealCost; player.Heal(player.maxHealth);   // "pizza nova": a whole new pizza
        Sfx.Play(Sfx.instance ? Sfx.instance.uiSelect : null, 0.8f, 1.2f);
        Hud.instance.RefreshOffer();
    }
    public const int RerollCost = 30, HealCost = 40;

    void NextLevel()
    {
        level++;
        Time.timeScale = 1f;
        StartCoroutine(LoadLevel(StartCountdown));
    }

    void RunOver()
    {
        state = State.RunOver;
        Time.timeScale = 0f;
        Hud.instance.ShowRunOver();
    }

    void Die(string why)
    {
        if (state == State.Dead) return;
        state = State.Dead;
        ComicFX.Impact(1.2f, 0f, new Color(0.9f, 0.05f, 0.1f), 0.5f);
        Time.timeScale = 0f;
        Hud.instance.ShowDeath(why);
    }

    public void TogglePauseUI() { if (state == State.Paused || state == State.Run || state == State.Countdown) TogglePause(); }

    void TogglePause()
    {
        if (state == State.Paused) { state = before; Time.timeScale = 1f; Hud.instance.ShowPause(false); }
        else { before = state; state = State.Paused; Time.timeScale = 0f; Hud.instance.ShowPause(true); }
    }

    public void Restart(bool toMenu)
    {
        if (toMenu) chosen = false;
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}
