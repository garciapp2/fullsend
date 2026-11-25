using UnityEngine;

// The delivery address, readable from anywhere on the course: a tall column of smoke rising from the customer's
// chimney (it replaces the finish beam), thin where it leaves the chimney and opening up as it climbs. Soft billboard puffs (Sledbonk/Smoke: flat comic tones, soft edges) that
// swell, churn and bend with the wind as they climb, so the column reads as one plume, not as separate balls.
public class SmokeSignal : MonoBehaviour
{
    public const int Layer = 13;
    static Material mat;

    public static SmokeSignal Build(Transform parent, Vector3 pos, float height = 140f)
    {
        var go = new GameObject("Sinal de fumaça"); go.transform.SetParent(parent, false);
        go.layer = Layer;   // drawn after the outline pass (see SledbonkBuilder.SetupSmokePass)
        go.transform.position = pos; go.transform.rotation = Quaternion.identity;
        var s = go.AddComponent<SmokeSignal>();
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        const float rise = 9f; float life = height / rise;

        var main = ps.main;
        main.loop = true; main.prewarm = false; main.playOnAwake = false;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.85f, life);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(2.4f, 3.4f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 600;

        var em = ps.emission; em.rateOverTime = 24f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.25f;   // a chimney: one point

        // climb + wind that grows with height (the plume leans over as it rises)
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        // (Unity ignores velocity unless x, y and z use the same curve mode: all three are curves)
        var lean = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
        vel.y = new ParticleSystem.MinMaxCurve(rise, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1f)));
        vel.x = new ParticleSystem.MinMaxCurve(7f, lean);
        vel.z = new ParticleSystem.MinMaxCurve(2f, lean);

        var size = ps.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.12f, 0.9f), new Keyframe(0.5f, 4.5f), new Keyframe(1f, 11f)));   // thin at the chimney, opening up as it rises

        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.96f, 0.95f, 0.97f), 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.06f), new GradientAlphaKey(0.9f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

        var noise = ps.noise; noise.enabled = true; noise.strength = 0.8f; noise.frequency = 0.08f; noise.scrollSpeed = 0.15f;
        noise.damping = true; noise.octaveCount = 2;

        var sheet = ps.textureSheetAnimation; sheet.enabled = true; sheet.numTilesX = 2; sheet.numTilesY = 2;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);
        sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.999f);
        sheet.cycleCount = 1;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);

        var r = go.GetComponent<ParticleSystemRenderer>();
        if (!mat) mat = Resources.Load<Material>("SmokeMat");
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sortMode = ParticleSystemSortMode.Distance;
        r.maxParticleSize = 6f;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // fast-forward one full climb so the whole column is already standing when the level starts
        ps.Simulate(life, true, true, false);
        ps.Play();
        return s;
    }
}
