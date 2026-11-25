using UnityEngine;

// speed impulse (1.65 * add, 0.3). Several shakes add up.
public static class GameFeel
{
    struct S { public float amount, left, dur, seed; }
    static readonly S[] shakes = new S[8];
    static int next;
    const float Scale = 15f;

    public static void Shake(float amount, float duration)
    {
        shakes[next] = new S { amount = amount, left = duration, dur = Mathf.Max(0.01f, duration), seed = Random.value * 100f };
        next = (next + 1) % shakes.Length;
    }

    public static Vector3 Rotation()
    {
        var r = Vector3.zero; float t = Time.unscaledTime * Scale;
        for (int i = 0; i < shakes.Length; i++)
        {
            if (shakes[i].left <= 0f) continue;
            shakes[i].left -= Time.unscaledDeltaTime;
            float k = shakes[i].amount * Mathf.Clamp01(shakes[i].left / shakes[i].dur) * 0.5f;
            float sd = shakes[i].seed;
            r += new Vector3(Mathf.PerlinNoise(t, sd) - 0.5f, Mathf.PerlinNoise(sd, t) - 0.5f, (Mathf.PerlinNoise(t, sd + 7f) - 0.5f) * 0.5f) * k;
        }
        return r;
    }

    public static Vector3 Offset() => Vector3.zero;
}
