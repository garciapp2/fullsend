using UnityEngine;

//  - offset (0, 2, -15) when fast / (0, 2, -7) on foot, divided by (fovM^2 + 1) so the camera tucks in as FOV opens
//  - fovM = FRILerp(.., Lerp(1, 1.65, EaseInOut(InverseLerp(50, 130, speed))) * airM, 2); airM -> 0.8 in the air
//  - FOV = 70 * fovM + fovPos, where fovPos is a spring (fovSpring 5, drag 10) kicked by AddFovImpulse
//  - wobble spring from velocity changes (wobble 0.25, spring 10, drag 10), target leads by smoothed velocity
//  - spherecast to keep the camera out of the terrain, plus GameFeel shake as rotation
public class CameraRig : MonoBehaviour
{
    public static CameraRig instance;
    public PlayerCharacter player;
    public Camera cam;
    public Vector3 offsetFast = new Vector3(0f, 2f, -15f), offsetSlow = new Vector3(0f, 2f, -7f);
    public float prediction = 0.03f, fovMax = 118f;
    const float GroundClear = 0.9f;
    Vector3 offset, smoothVel, lastVel, wobblePos, wobbleVel;
    float fovM = 1f, airM = 1f, fovPos, fovVel, tilt;

    void Awake() { instance = this; if (!cam) cam = GetComponentInChildren<Camera>(); offset = offsetSlow; }

    public void AddFovImpulse(float amount) => fovVel += amount;
    public void Kick(Vector3 v) => wobbleVel += v * 4f;

    // main menu: a cinematic shot per mode, the camera glides between them
    //  0 Corrida: low angle at the start, valley opening ahead      1 Fuga: looking back uphill at the collapse
    //  2 Infinito: wide shot, the fox tiny against endless mountains
    Vector3 menuPos, menuLook; float menuFov = 40f; bool menuInit;
    void MenuFraming()
    {
        float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        var p = player.transform.position;
        var fwd = player.FlatForward; var right = player.FlatRight;
        int shot = Hud.instance ? Hud.instance.MenuSelection : 0;
        Vector3 pos, look; float fov;
        if (shot == 0)
        {
            // Corrida: behind the fox, the whole descent opening up ahead
            pos = p - fwd * 4.6f + right * (1.7f + Mathf.Sin(t * 0.3f) * 0.3f) + Vector3.up * 1.5f;
            look = p + fwd * 40f - Vector3.up * 6f - right * 6f; fov = 50f;
        }
        else if (shot == 1)
        {
            // Fuga: in front, low, looking back up the slope at the collapse looming over the fox
            pos = p + fwd * 5.5f - right * (1.6f + Mathf.Sin(t * 0.4f) * 0.25f) + Vector3.up * 0.55f;
            look = p - fwd * 30f + Vector3.up * 14f + right * 4f; fov = 56f;
        }
        else
        {
            pos = p + fwd * 26f + right * (16f + Mathf.Sin(t * 0.2f) * 3f) + Vector3.up * 11f;
            look = p + Vector3.up * 2f - fwd * 40f + right * 10f; fov = 42f;
        }
        if (!menuInit) { menuPos = pos; menuLook = look; menuFov = fov; menuInit = true; }
        float k = 1f - Mathf.Exp(-3.2f * dt);
        menuPos = Vector3.Lerp(menuPos, pos, k); menuLook = Vector3.Lerp(menuLook, look, k); menuFov = Mathf.Lerp(menuFov, fov, k);
        transform.position = menuPos;
        transform.rotation = Quaternion.LookRotation(menuLook - menuPos, Vector3.up);
        cam.transform.localPosition = Vector3.zero; cam.transform.localRotation = Quaternion.Euler(0f, 0f, shot == 1 ? -6f : shot == 0 ? 4f : 0f);
        cam.fieldOfView = menuFov;
    }

    static float FRI(float a, float b, float s, float dt) => Mathf.Lerp(a, b, 1f - Mathf.Exp(-s * dt));
    static Vector3 FRI(Vector3 a, Vector3 b, float s, float dt) => Vector3.Lerp(a, b, 1f - Mathf.Exp(-s * dt));

    void LateUpdate()
    {
        if (!player) return;
        if (RunManager.instance && RunManager.instance.state == RunManager.State.Menu) { MenuFraming(); return; }
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        if (Time.timeScale <= 0f) dt = 0f;
        bool fast = player.data.movementType == MovementType.Fast;
        var vel = player.rig.linearVelocity;
        float speed = vel.magnitude;

        // FOV
        airM = player.data.isGrounded || TrickSystem.instance && TrickSystem.instance.grinding ? FRI(airM, 1f, 2f, dt) : FRI(airM, 0.8f, 0.25f, dt);
        float cs = Mathf.InverseLerp(50f, 130f, speed);
        float ease = cs * cs * (3f - 2f * cs);
        float boost = TrickSystem.instance && TrickSystem.instance.boosting ? 1.08f : 1f;
        fovM = FRI(fovM, Mathf.Lerp(1f, 1.65f, ease) * airM * boost, 2f, dt);
        fovVel = FRI(fovVel, -fovPos * 5f, 10f, dt);
        fovPos += fovVel * dt;

        // wobble from velocity changes
        var dv = (vel - lastVel); lastVel = vel;
        dv.x *= 0.3f; dv.z *= 0.3f; if (player.data.fastFalling) dv.y *= 1.5f;
        if (dt > 0f) wobbleVel += -dv * 0.25f;
        wobbleVel = FRI(wobbleVel, -wobblePos * 10f, 10f, dt);
        wobblePos += wobbleVel * dt;
        wobblePos = Vector3.ClampMagnitude(wobblePos, 2.5f);

        // position
        offset = Vector3.Lerp(offset, fast ? offsetFast : offsetSlow, dt * 2f);
        smoothVel = FRI(smoothVel, vel, 5f, dt);
        var rot = Quaternion.Euler(-player.data.lookRotationValues.y + 6f, player.data.lookRotationValues.x, 0f);
        var target = player.transform.position + Vector3.up * 1.6f + smoothVel * prediction;
        var off = offset / (fovM * fovM + 1f) * 2f;
        // wobble goes in before the terrain checks (it used to be added after them and could push the camera under the
        // ground); the sphere is as wide as the near plane gets at our widest FOV, and a last downward ray keeps the
        // camera at least GroundClear above the terrain under it (steep downhill: the slope behind you is higher)
        var desired = target + rot * new Vector3(off.x, 0f, off.z) + Vector3.up * off.y + wobblePos;
        var from = player.transform.position + Vector3.up * 2.5f;
        var dir = desired - from;
        if (Physics.SphereCast(from, 0.8f, dir.normalized, out var hit, dir.magnitude, PlayerCharacter.GroundMask, QueryTriggerInteraction.Ignore))
            desired = from + dir.normalized * Mathf.Max(0.4f, hit.distance - 0.1f);
        var probe = new Vector3(desired.x, Mathf.Max(desired.y + 2f, from.y), desired.z);
        if (Physics.Raycast(probe, Vector3.down, out var g, probe.y - desired.y + 6f, PlayerCharacter.GroundMask, QueryTriggerInteraction.Ignore)
            && desired.y < g.point.y + GroundClear)
            desired.y = g.point.y + GroundClear;
        transform.position = desired;
        transform.rotation = Quaternion.LookRotation(target - desired, Vector3.up);
        cam.transform.localPosition = Vector3.zero;
        tilt = Mathf.Lerp(tilt, fast && player.data.isGrounded ? -player.data.turnAmount * 6f : 0f, dt * 6f);
        cam.transform.localRotation = Quaternion.Euler(GameFeel.Rotation() + new Vector3(0f, 0f, tilt));
        cam.fieldOfView = Mathf.Min(fovMax, 70f * fovM + fovPos);
    }
}
