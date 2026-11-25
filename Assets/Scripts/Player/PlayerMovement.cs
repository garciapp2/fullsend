using UnityEngine;

// momentum on slopes, landings graded bad/ok/good/perfect by how much speed survives the impact,
// fast fall (Space in the air), braking by turning around, and a soft push toward 60 speed.
public class PlayerMovement : MonoBehaviour
{
    class Landing
    {
        public Vector3 velBefore, velAfter;
        public float speedBefore, speedAfter, landingScore, sinceGroundedBeforeLanding;
        public RaycastHit hit;
    }

    PlayerCharacter player;
    Rigidbody rig;
    readonly float badLandingThreshold = 0.7f;
    public bool allowMovement = true;
    readonly float landingPrecisionDifficulty = 1f;
    public float landingLeniency = 0f;   // items make landings easier to grade

    void Awake() { player = GetComponent<PlayerCharacter>(); rig = GetComponent<Rigidbody>(); }

    void FixedUpdate()
    {
        if (player.dead) return;
        player.data.fastFalling = false;
        if (player.data.sinceSwitchMovementType > 0.1f && rig.linearVelocity.magnitude < 50f && player.data.isGrounded && player.data.canEnterSlowMode)
        {
            ToggleMovement();
            return;
        }
        CheckDistanceToGround();
        if (player.data.isGrounded) GroundMovement(); else AirMovement();
        player.data.landingThisFrame = LandingType.None;
        player.data.isGrounded = GroundCollision();
        if (player.data.isGrounded)
        {
            if (player.data.sinceGrounded > 0f) { player.data.lastAirTime = player.data.sinceGrounded; player.data.sinceGrounded = 0f; }
        }
        else player.data.sinceGrounded += Time.fixedDeltaTime;
        player.data.lastPos = rig.position;
    }

    static RaycastHit LineCheck(Vector3 a, Vector3 b)
    {
        Physics.Linecast(a, b, out var hit, PlayerCharacter.RunnableMask, QueryTriggerInteraction.Ignore);
        return hit;
    }

    void CheckDistanceToGround()
    {
        var hit = LineCheck(transform.position + Vector3.up, transform.position + Vector3.down * 10000f);
        if (hit.transform) player.data.distanceToGround = hit.distance;
    }

    void ToggleMovement() { DisableMode(); player.slowMovement.EnableMode(); }

    public void EnableMode()
    {
        player.data.sinceSwitchMovementType = 0f;
        player.data.movementType = MovementType.Fast;
        enabled = true;
    }

    public void DisableMode() { enabled = false; }

    public void Launch(Vector3 dir) { rig.linearVelocity = dir * 90f; }

    bool GroundCollision()
    {
        float num = 2f;
        var hit = LineCheck(player.data.lastPos + Vector3.up * num, transform.position + Vector3.up * num);
        if (hit.transform == null && player.data.dontLandFor <= 0f)
            hit = LineCheck(transform.position + Vector3.up * num, transform.position);
        CheckMostlyGrounded(num);
        if (!hit.transform) return false;
        if (hit.transform.root == transform.root) return false;
        var info = hit.collider.GetComponentInParent<PropInfo>();
        if (info && (info.kind == PropInfo.Kind.Breakable || (!info.Runnable && !info.lethal))) { info.HitBreakable(player); return false; }
        if (info && !info.Runnable) { Impact(hit.normal, hit.point, info.lethalDamage); return false; }
        var landing = GetLanding(hit);
        if (Vector3.Angle(-player.data.gravityDir, hit.normal) > 80f || hit.rigidbody)
        {
            EdgeLanding(hit);
            return false;
        }
        rig.MovePosition(hit.point);
        if (!player.data.isGrounded) Land(landing); else TouchGround(landing);
        return true;
    }

    void CheckMostlyGrounded(float rayOffset)
    {
        var h = LineCheck(transform.position + Vector3.up * rayOffset, transform.position + Vector3.down * 0.5f);
        player.data.mostlyGrounded = h.transform;
    }

    void EdgeLanding(RaycastHit hit)
    {
        var info = hit.collider ? hit.collider.GetComponentInParent<PropInfo>() : null;
        if (info && (info.kind == PropInfo.Kind.Breakable || !info.lethal)) { info.HitBreakable(player); return; }
        Impact(hit.normal, hit.point, info ? info.lethalDamage : 85f);
    }

    //  - glancing (velocity less than 150 deg from the surface normal): a "bonk", you slide along it, 15 damage;
    //  - head-on: a "death collision", 85 damage; if you survive you bounce straight back off it, knocked down 1 s.
    // Used for terrain walls and for solid props.
    public void Impact(Vector3 normal, Vector3 point, float lethalDamage = 85f)
    {
        if (Vector3.Angle(normal, rig.linearVelocity.normalized) < 150f)
        {
            var n = Vector3.ProjectOnPlane(rig.linearVelocity, normal).normalized;
            float k = Mathf.Clamp01(1f - player.stats.speedLossResistance);
            n = Vector3.Lerp(n, normal, 0.5f * k);
            rig.linearVelocity = n.normalized * rig.linearVelocity.magnitude;
            if (!player.Invulnerable()) player.TakeDamage(15f, point);
            GameFeel.Shake(10f, 0.3f);
            ComicFX.Impact(0.4f, 0.03f);
            if (Sfx.instance) Sfx.Play(Sfx.instance.wallHit, 0.7f, 1.2f);
            return;
        }
        bool survive = true;
        if (!player.Invulnerable())
        {
            survive = player.health - lethalDamage > 0f;
            if (survive)
            {
                player.data.knockedDownFor = 1f;
                if (TrickSystem.instance) TrickSystem.instance.Bail("BATEU!", 0f, false);
            }
            player.TakeDamage(lethalDamage, point);
            ComicFX.Impact(1f, 0.08f, new Color(1f, 0.25f, 0.2f), 0.3f);
            if (Sfx.instance) Sfx.Play(Sfx.instance.wallHit, 1f, 0.9f);
        }
        if (survive)
        {
            float y = rig.linearVelocity.y;
            var v = rig.linearVelocity.magnitude * normal * 0.8f;
            rig.linearVelocity = new Vector3(v.x, y, v.z);
        }
        GameFeel.Shake(10f, 0.2f);
    }

    void Land(Landing landing)
    {
        if (player.data.perfectLandingsFor > 0f) landing.landingScore = 1f;
        float num = Mathf.Lerp(-0.05f, 0f, landingPrecisionDifficulty) - landingLeniency;
        if (landing.landingScore < badLandingThreshold + num && rig.linearVelocity.magnitude > 50f
            && landing.hit.collider.GetComponentInParent<PropInfo>()) landing.landingScore = 0.8f;
        if (landing.landingScore < badLandingThreshold + num && rig.linearVelocity.magnitude > 50f)
        {
            BadLanding(landing);
            player.data.landingThisFrame = LandingType.Bad;
            player.RaiseLanding(LandingType.Bad, landing.speedBefore);
            return;
        }
        SetVelocity(landing.velAfter.normalized * landing.speedBefore);
        if (!(player.data.sinceGrounded > 0.6f)) return;
        float num2 = Mathf.Clamp01(player.data.sinceGrounded - 0.6f);
        LandingType type = landing.landingScore < 0.9f + num ? LandingType.Ok : landing.landingScore < 0.95f + num ? LandingType.Good : LandingType.Perfect;
        if (type == LandingType.Ok) { player.AddEnergy(0.03f * num2); GameFeel.Shake(10f, 0.3f); }
        else if (type == LandingType.Good) { player.AddEnergy(0.055f * num2); GameFeel.Shake(2.5f, 0.3f); }
        else { player.AddEnergy(0.085f * num2); player.LerpSpeedTo(100f, 0.3f); }
        player.data.landingThisFrame = type;
        SetVelocity(rig.linearVelocity * landing.landingScore);
        player.RaiseLanding(type, landing.speedBefore);
    }

    void BadLanding(Landing landing)
    {
        var v = Vector3.ProjectOnPlane(rig.linearVelocity.normalized, player.data.gravityDir);
        var inNormal = Vector3.Lerp(landing.hit.normal, v.normalized, 0.1f);
        rig.linearVelocity = Vector3.Reflect(rig.linearVelocity, inNormal);
        float t = Mathf.InverseLerp(0f, badLandingThreshold, landing.landingScore);
        rig.linearVelocity *= Mathf.Lerp(0.5f, badLandingThreshold, t);
        player.data.dontLandFor = 0.1f;
        GameFeel.Shake(10f, 0.3f);
    }

    void TouchGround(Landing landing) { SetVelocity(landing.velAfter.normalized * landing.speedBefore); }

    Landing GetLanding(RaycastHit hit)
    {
        var l = new Landing();
        l.velBefore = rig.linearVelocity; l.speedBefore = rig.linearVelocity.magnitude;
        l.velAfter = Vector3.ProjectOnPlane(rig.linearVelocity, hit.normal);
        l.velAfter = Vector3.Lerp(l.velAfter, l.velBefore, 0.65f);
        l.speedAfter = l.velAfter.magnitude;
        l.landingScore = l.speedBefore > 0.001f ? l.speedAfter / l.speedBefore : 1f;
        l.sinceGroundedBeforeLanding = player.data.sinceGrounded;
        l.hit = hit;
        if (Vector3.Angle(Vector3.up, hit.normal) < 60f) player.data.groundNormal = hit.normal;
        return l;
    }

    void GroundMovement()
    {
        float t = Mathf.InverseLerp(15f, 50f, rig.linearVelocity.magnitude);
        var gravityDir = Vector3.Lerp(-player.data.groundNormal, player.data.gravityDir, t);
        Gravity(0.1f, gravityDir);
        Movement(player.GetWorldMoveDir(true), player.stats.runSpeed);
        ApplyDrag();
    }

    void AirMovement()
    {
        Gravity(1f, player.data.gravityDir);
        Movement(player.GetWorldMoveDir(true, true), Mathf.Lerp(player.stats.airSpeed, player.stats.runSpeed, player.data.MostlyGroundedValue()));
        ApplyDrag();
        if (player.input.fastFallIsPressed) FastFall();
    }

    void FastFall()
    {
        player.data.fastFalling = true;
        float a = Mathf.Clamp01((player.data.sinceGrounded - 0.2f) * 5f);
        float b = Mathf.Clamp01((player.data.sinceGrounded - 0.15f) * 8f);
        rig.AddForce(Vector3.down * a * player.stats.fastFallSpeed, ForceMode.Acceleration);
        float y = Mathf.MoveTowards(rig.linearVelocity.normalized.y, -0.8f, player.stats.fastFallLerp * Time.fixedDeltaTime * b);
        var n = rig.linearVelocity.normalized; n.y = y;
        rig.linearVelocity = n.normalized * rig.linearVelocity.magnitude;
    }

    void ApplyDrag() { SetVelocity(rig.linearVelocity * (1f - player.stats.drag)); }

    void Movement(Vector3 dir, float force)
    {
        force *= 1f + player.data.GetBoost(player.stats);
        rig.AddForce(dir * force * Time.fixedDeltaTime, ForceMode.Acceleration);
        if (Vector3.Angle(dir, Flat(rig.linearVelocity)) > 150f) player.data.isBreaking = true;
        if (player.data.isBreaking)
        {
            ApplyDrag();
            if (player.data.isGrounded) ApplyDrag();
            if (player.input.movementInput.x != 0f && player.input.movementInput.y != 0f) TurnTowards(dir, 0.5f);
            if (Vector3.Angle(dir, Flat(rig.linearVelocity)) < 90f || dir == Vector3.zero) player.data.isBreaking = false;
        }
        else TurnTowards(dir);
        if (!rig.isKinematic && !player.data.isBreaking && rig.linearVelocity.magnitude < 60f)
            rig.linearVelocity = Vector3.Lerp(rig.linearVelocity, rig.linearVelocity.normalized * 60f, Time.fixedDeltaTime * 3f * (1f - rig.linearVelocity.magnitude / 60f));
    }

    void TurnTowards(Vector3 dir, float multiplier = 1f)
    {
        if (!allowMovement || player.data.knockedDownFor > 0f) multiplier = 0f;
        var lv = rig.linearVelocity; var v = new Vector3(lv.x, 0f, lv.z);
        float num = Mathf.InverseLerp(0f, 15f, Vector3.Angle(dir, v));
        player.data.turnAmount = Vector3.Cross(v, dir).y < 0f ? -num : num;
        v = Vector3.RotateTowards(v, dir, num * Time.fixedDeltaTime * player.stats.turnSpeed * player.data.abilityTurnRate * player.data.itemTurnRate * multiplier, 0f);
        v += Vector3.up * lv.y;
        SetVelocity(v);
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    void SetVelocity(Vector3 vel) => rig.AddForce(vel - rig.linearVelocity, ForceMode.VelocityChange);
    void Gravity(float mult, Vector3 dir) => rig.AddForce(dir * mult * player.stats.gravity, ForceMode.Acceleration);
}
