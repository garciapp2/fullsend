using UnityEngine;

// (speed 130, drag 10, air control 0.4, jump 30, gravity 200). Holding crouch charges for ~1 s, then you
public class PlayerSlowMovement : MonoBehaviour
{
    PlayerCharacter player;
    Rigidbody rig;
    public bool allowMovement = true;
    public float chargeCounter;     // 0..1.1, shown by the HUD and the visuals
    bool groundCollision;
    float sinceContact = 9f;        // since the capsule last touched something you could stand or lean on

    void Awake() { player = GetComponent<PlayerCharacter>(); rig = GetComponent<Rigidbody>(); }

    SlowRunStats S => player.slowStats;

    void Update()
    {
        if (player.dead || Time.timeScale <= 0f) return;
        if (allowMovement) { CheckMode(); CheckJump(); }
    }

    void CheckJump()
    {
        if (!(player.data.sinceGrounded > 0.3f) && !(player.data.sinceJumped < 0.35f) && player.input.jumpWasPressed) Jump();
    }

    void Jump()
    {
        rig.AddForce(Vector3.up * S.jumpForce, ForceMode.VelocityChange);
        player.data.sinceGrounded = 0f;
        player.data.sinceJumped = 0f;
        if (PlayerVisual.instance) PlayerVisual.instance.Jump();
    }

    void FixedUpdate()
    {
        if (player.dead) return;
        if (allowMovement && !player.data.enteringFastRun) Movement();
        Gravity();
        Drag();
        if (player.data.sinceJumped < 0.2f) player.data.isGrounded = false;
        else player.data.isGrounded = GroundCollision() || groundCollision;
        if (player.data.isGrounded) player.data.sinceGrounded = 0f;
        else player.data.sinceGrounded += Time.fixedDeltaTime;
        groundCollision = false;
        sinceContact += Time.fixedDeltaTime;
        player.data.turnAmount = 0f;
        player.data.lastPos = transform.position;
    }

    public float GetControl()
    {
        float a = player.data.isGrounded ? 1f : S.airControl;
        return Mathf.Lerp(a, 0.1f, 1f - Mathf.Clamp01(player.data.sinceSwitchMovementType * 0.75f));
    }

    void CheckMode()
    {
        if (player.data.sinceSwitchMovementType < 0.1f) return;
        player.data.enteringFastRun = player.input.crouchIsPressed;
        if (player.data.enteringFastRun && CanCharge() && player.data.canEnterFastMode)
        {
            chargeCounter = Mathf.MoveTowards(chargeCounter, 1.1f, Time.deltaTime);
            GameFeel.Shake(chargeCounter * 0.6f, 0.1f);
            if (chargeCounter > 1f) GoFast();
        }
        else if (!player.data.enteringFastRun) chargeCounter = Mathf.MoveTowards(chargeCounter, 0f, Time.deltaTime);
        // (Shift still held but the feet lost the ground for a moment: the charge waits instead of draining)
    }

    // The centre ray (mostlyGrounded) misses when you stand on a sloped edge, a rim or a crack between two pieces of
    // terrain: the capsule is resting on something but nothing is right below its centre. Any recent contact you could
    // lean on, or a short sphere cast the width of the body, counts as ground for charging.
    bool CanCharge()
    {
        if (player.data.mostlyGrounded || sinceContact < 0.2f) return true;
        return Physics.SphereCast(transform.position + Vector3.up * 1.2f, 0.45f, Vector3.down, out _, 1.6f, PlayerCharacter.RunnableMask, QueryTriggerInteraction.Ignore);
    }

    public void GoFast()
    {
        chargeCounter = 0f;
        var v = player.FlatForward;
        if (player.data.mostlyGrounded) v = Vector3.ProjectOnPlane(v, player.data.groundNormal);
        player.data.enteringFastRun = false;
        player.movement.Launch(v.normalized);
        player.movement.EnableMode();
        DisableMode();
        GameFeel.Shake(15f, 0.5f);
        if (CameraRig.instance) CameraRig.instance.Kick(-v.normalized * 6f);
    }

    public void EnableMode()
    {
        player.data.lastPos = transform.position;
        player.data.sinceGrounded = 0f;
        player.data.sinceSwitchMovementType = 0f;
        enabled = true;
        player.data.movementType = MovementType.Slow;
    }

    public void DisableMode() { enabled = false; }

    void Gravity() => rig.AddForce(Vector3.down * S.gravity * player.data.sinceGrounded, ForceMode.Acceleration);

    void Drag() => rig.linearVelocity -= rig.linearVelocity * Time.fixedDeltaTime * S.drag * GetControl();

    void Movement()
    {
        float num = Mathf.Clamp01(player.input.movementInput.magnitude);
        rig.AddForce(MovementDir() * S.speed * GetControl() * num, ForceMode.Acceleration);
    }

    Vector3 MovementDir()
    {
        var f = player.FlatForward; var r = player.FlatRight;
        if (player.data.isGrounded && player.data.groundNormal != Vector3.zero)
        {
            f = Vector3.Cross(r, player.data.groundNormal);
            r = Vector3.Cross(-f, player.data.groundNormal);
        }
        return (f * player.input.movementInput.y + r * player.input.movementInput.x).normalized;
    }

    void OnCollisionStay(Collision c)
    {
        if (c.rigidbody) return;
        for (int i = 0; i < c.contactCount; i++)
        {
            float a = Vector3.Angle(Vector3.up, c.GetContact(i).normal);
            if (a <= 60f && player.data.sinceJumped >= 0.2f) groundCollision = true;
            if (a <= 85f) sinceContact = 0f;
        }
    }

    static RaycastHit LineCheck(Vector3 a, Vector3 b)
    {
        Physics.Linecast(a, b, out var hit, PlayerCharacter.RunnableMask, QueryTriggerInteraction.Ignore);
        return hit;
    }

    bool GroundCollision()
    {
        float num = 2f;
        var hit = LineCheck(transform.position + Vector3.up * num, player.data.lastPos + Vector3.up * num);
        if (!hit.transform)
        {
            hit = LineCheck(player.data.lastPos + Vector3.up, transform.position + Vector3.up);
            if (hit.transform) transform.position = hit.point;
        }
        if (!hit.transform) hit = LineCheck(transform.position + Vector3.up * num, transform.position + Vector3.down * 0.05f);
        player.data.mostlyGrounded = hit.transform;
        if (!hit.transform) return false;
        if (Vector3.Angle(-player.data.gravityDir, hit.normal) > 70f || hit.rigidbody) return false;
        rig.MovePosition(hit.point);
        rig.linearVelocity = Vector3.ProjectOnPlane(rig.linearVelocity, hit.normal);
        player.data.groundNormal = hit.normal;
        return true;
    }
}
