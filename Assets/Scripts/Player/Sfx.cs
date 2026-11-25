using UnityEngine;

// fast fall) whose volume and pitch follow speed, a boost loop, landing sounds per landing grade, sparks
// whose pitch climbs while you keep collecting them, and the biome music (A/B/C by level progress).
public class Sfx : MonoBehaviour
{
    public static Sfx instance;
    [Header("Loops")] public AudioClip windGround, windAir, windFastFall, boostLoop, sledLoop, crackLoop;
    [Header("Player")] public AudioClip jump, landBad, landOk, landGood, landPerfect, trick, bail, grindStart, ring, spark, wallHit, boostStart;
    [Header("UI")] public AudioClip uiSelect, uiHover, itemPick, levelEnd, countdown, go;
    AudioSource sGround, sAir, sFall, sBoost, sSled, sCrack, music, music2, amb, oneShot;
    float sparkPitch = 1f, sinceSpark = 9f;
    AudioClip currentMusic;

    void Awake()
    {
        instance = this;
        sGround = Loop(windGround); sAir = Loop(windAir); sFall = Loop(windFastFall); sBoost = Loop(boostLoop); sSled = Loop(sledLoop); sCrack = Loop(crackLoop);
        music = gameObject.AddComponent<AudioSource>(); music.loop = true; music.volume = 0f;
        music2 = gameObject.AddComponent<AudioSource>(); music2.loop = true; music2.volume = 0f;
        amb = Loop(null);
        oneShot = gameObject.AddComponent<AudioSource>();
    }

    AudioSource Loop(AudioClip c)
    {
        var s = gameObject.AddComponent<AudioSource>(); s.clip = c; s.loop = true; s.volume = 0f; s.playOnAwake = false;
        if (c) s.Play();
        return s;
    }

    public static void Play(AudioClip c, float vol = 1f, float pitch = 1f)
    {
        if (!instance || !c) return;
        instance.oneShot.pitch = pitch;
        instance.oneShot.PlayOneShot(c, vol);
    }

    public void Spark()
    {
        sparkPitch = sinceSpark < 0.6f ? Mathf.Min(sparkPitch + 0.04f, 2.2f) : 1f;
        sinceSpark = 0f;
        var s = gameObject.AddComponent<AudioSource>(); s.clip = spark; s.pitch = sparkPitch; s.volume = 0.35f; s.Play(); Destroy(s, 1f);
    }

    public void Landing(LandingType t)
    {
        switch (t)
        {
            case LandingType.Bad: Play(landBad, 0.9f); break;
            case LandingType.Ok: Play(landOk, 0.7f); break;
            case LandingType.Good: Play(landGood, 0.8f); break;
            case LandingType.Perfect: Play(landPerfect, 1f); Play(landGood, 0.5f, 1.3f); break;
        }
    }

    public void SetMusic(AudioClip c, AudioClip ambience)
    {
        if (c == currentMusic) return;
        currentMusic = c;
        var tmp = music; music = music2; music2 = tmp;   // crossfade to the other source
        music.clip = c; music.volume = 0f; if (c) music.Play();
        if (amb.clip != ambience) { amb.clip = ambience; if (ambience) amb.Play(); }
    }

    public float danger;   // 0..1 from the chasing collapse

    void Update()
    {
        sinceSpark += Time.unscaledDeltaTime;
        music.volume = Mathf.MoveTowards(music.volume, 0.42f, Time.unscaledDeltaTime * 0.4f);
        music2.volume = Mathf.MoveTowards(music2.volume, 0f, Time.unscaledDeltaTime * 0.6f);
        amb.volume = 0.12f;
        music.pitch = Mathf.Lerp(1f, 0.85f, danger);
        sCrack.volume = danger * 0.7f;

        var pc = PlayerCharacter.instance;
        if (!pc || Time.timeScale <= 0f) { sGround.volume = sAir.volume = sFall.volume = sBoost.volume = sSled.volume = 0f; return; }
        float v = pc.Speed; bool fast = pc.data.movementType == MovementType.Fast;
        const float mod = 0.025f;
        bool grounded = pc.data.isGrounded;
        float on = v > 40f ? 1f : v / 40f;
        Smooth(sGround, grounded ? v / 20f * 1.5f * mod * 2f * on : 0f, 1f + v / 20f * mod / 4f);
        Smooth(sAir, !grounded ? v / 20f * mod * 2.4f * on : 0f, 1f + v / 10f * mod / 2f);
        float vy = pc.rig.linearVelocity.y;
        Smooth(sFall, pc.data.fastFalling ? Mathf.Clamp01(-vy * mod / 10f * 6f) : 0f, 1f + Mathf.Abs(vy) * mod / 10f);
        var ts = TrickSystem.instance;
        Smooth(sBoost, ts && ts.boosting ? 0.3f : 0f, 2f + 2f * (ts ? ts.boostMeter : 0f) * 0.5f);
        Smooth(sSled, fast && grounded ? Mathf.Clamp01(v / 120f) * 0.35f : 0f, 0.8f + v / 200f);
    }

    static void Smooth(AudioSource s, float vol, float pitch)
    {
        if (!s.clip) return;
        s.volume = Mathf.MoveTowards(s.volume, vol, Time.deltaTime * 2f);
        s.pitch = Mathf.Lerp(s.pitch, pitch, Time.deltaTime * 5f);
    }
}
