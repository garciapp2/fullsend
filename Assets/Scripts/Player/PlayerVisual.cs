using UnityEngine;

// (Speed / IsGrounded). On the sled: the sled shows under the character, which sits (IsSitting) and the
public class PlayerVisual : MonoBehaviour
{
    public static PlayerVisual instance;
    public PlayerCharacter player;
    public Transform root;          // rotates; parent of character + sled
    public Animator animator;
    public Transform character;
    public Transform sled;
    public float sledYOffset = 0.15f;
    public int sittingAnimationIndex = 0;
    Vector3 facing = Vector3.forward;
    Quaternion baseRot = Quaternion.identity;
    float squash;
    Vector3 grabPos;

    static readonly int pSpeed = Animator.StringToHash("Speed"), pGrounded = Animator.StringToHash("IsGrounded"),
        pSitting = Animator.StringToHash("IsSitting"), pSitIdx = Animator.StringToHash("SittingAnimationIndex");

    void Awake()
    {
        instance = this;
        if (animator && !animator.GetComponent<AnimationEventSink>()) animator.gameObject.AddComponent<AnimationEventSink>();
        PizzaStack.Attach(sled);   // the deliveries ride on the sled
    }

    public void Jump() { squash = -0.25f; }

    void LateUpdate()
    {
        if (!player) return;
        bool fast = player.data.movementType == MovementType.Fast;
        bool menuPose = RunManager.instance && RunManager.instance.state == RunManager.State.Menu;
        var v = player.rig.linearVelocity; var flat = new Vector3(v.x, 0f, v.z);
        if (fast) { if (flat.sqrMagnitude > 1f) facing = flat.normalized; }
        else
        {
            var input = player.GetWorldMoveDir(false);
            if (input.sqrMagnitude > 0.01f) facing = Vector3.Slerp(facing, input, Time.deltaTime * 12f).normalized;
            if (player.slowMovement.chargeCounter > 0.05f) facing = Vector3.Slerp(facing, player.FlatForward, Time.deltaTime * 20f).normalized;
        }
        // as you become fully airborne (FullyAirbourneValue, 0.2-0.4 s after leaving the ground) its "up"
        // turns perpendicular to the velocity, so the nose follows the trajectory: up on the climb, down on the drop.
        Quaternion target;
        if (fast && player.data.knockedDownFor <= 0f && v.sqrMagnitude > 1f)
        {
            float air = player.data.FullyAirbourneValue();
            if (TrickSystem.instance && TrickSystem.instance.grinding) air = 1f;
            var vn = v.normalized;
            var airUp = -Vector3.Cross(vn, Vector3.Cross(vn, Vector3.up)).normalized;
            if (airUp.sqrMagnitude < 0.01f) airUp = Vector3.up;
            var upT = Vector3.Lerp(player.data.groundNormal, airUp, air).normalized;
            var f = Vector3.Cross(upT, Vector3.Cross(Vector3.ProjectOnPlane(vn, Vector3.up), upT));
            if (f.sqrMagnitude < 0.0001f) f = facing;
            target = Quaternion.LookRotation(f, upT) * Quaternion.Euler(0f, 0f, -player.data.turnAmount * 14f);
        }
        else if (fast) target = baseRot;
        else target = Quaternion.LookRotation(facing, Vector3.up);
        baseRot = Quaternion.Slerp(baseRot, target, Time.deltaTime * 10f);
        var ts = TrickSystem.instance;
        bool tricking = ts && (Mathf.Abs(Mathf.DeltaAngle(ts.visualSpin, 0f)) > 0.5f || Mathf.Abs(Mathf.DeltaAngle(ts.visualFlip, 0f)) > 0.5f);
        root.rotation = tricking ? baseRot * Quaternion.Euler(ts.visualFlip, ts.visualSpin, 0f) : baseRot;

        // grab poses (SSX): the rider leans off / lifts the sled
        int grab = ts ? ts.GrabPose : 0;
        var sledRot = grab == 1 ? Quaternion.Euler(0f, 0f, 35f) : grab == 2 ? Quaternion.Euler(0f, 0f, -35f) : grab == 4 ? Quaternion.Euler(-40f, 0f, 0f) : Quaternion.identity;
        var charPos = grab == 3 ? new Vector3(0f, 0.9f, 0.6f) : Vector3.zero;
        var charRot = grab == 3 ? Quaternion.Euler(70f, 0f, 0f) : grab == 1 ? Quaternion.Euler(0f, 0f, -15f) : grab == 2 ? Quaternion.Euler(0f, 0f, 15f) : Quaternion.identity;
        if (sled) sled.localRotation = Quaternion.Slerp(sled.localRotation, sledRot, Time.deltaTime * 16f);
        grabPos = Vector3.Lerp(grabPos, charPos, Time.deltaTime * 14f);
        if (character) character.localRotation = Quaternion.Slerp(character.localRotation, charRot, Time.deltaTime * 14f);
        // Tiny Wings dive: tuck down
        if (ts && ts.Diving) squash = Mathf.Lerp(squash, 0.18f, Time.deltaTime * 10f);

        // squash for the charge and landings
        float charge = player.slowMovement.enabled ? player.slowMovement.chargeCounter : 0f;
        squash = Mathf.Lerp(squash, 0f, Time.deltaTime * 8f);
        float sq = 1f - charge * 0.25f + squash * -0.5f;
        root.localScale = new Vector3(1f + (1f - sq) * 0.5f, sq, 1f + (1f - sq) * 0.5f);

        if (sled) sled.gameObject.SetActive(fast || menuPose);
        if (character) character.localPosition = new Vector3(0f, fast || menuPose ? sledYOffset : 0f, 0f) + (fast ? grabPos : Vector3.zero);
        if (animator)
        {
            animator.SetFloat(pSpeed, fast ? 0f : flat.magnitude * 0.35f);
            animator.SetBool(pGrounded, player.data.isGrounded || player.data.mostlyGrounded);
            animator.SetBool(pSitting, fast || menuPose);
            animator.SetFloat(pSitIdx, sittingAnimationIndex);
        }
    }

    public void LandSquash(float amount) { squash = amount; }
}
