using System;
using UnityEngine;

// modes, look rotation, helpers (GetWorldMoveDir, LerpSpeedTo, energy) and health.
[RequireComponent(typeof(Rigidbody))]
public class PlayerCharacter : MonoBehaviour
{
    public static PlayerCharacter instance;

    [Serializable]
    public class Data
    {
        public bool isGrounded = true;
        public float sinceGrounded;
        public Vector3 gravityDir = Vector3.down;
        public Vector3 lastPos;
        public Vector2 lookRotationValues;
        public Vector3 lookDir = Vector3.forward;
        public Vector3 groundNormal = Vector3.up;
        public bool mostlyGrounded;
        public MovementType movementType = MovementType.Slow;
        public bool isBreaking;
        public float sinceSwitchMovementType = 100f;
        public float distanceToGround;
        public LandingType landingThisFrame;
        public float lastAirTime;
        public bool fastFalling;
        public float dontLandFor;
        public float perfectLandingsFor;
        public float speedBoost;
        public bool canEnterSlowMode = true;
        public bool canEnterFastMode = true;
        public bool enteringFastRun;
        public float sinceJumped = 10f;
        public float turnAmount;
        public float invulnerability;
        public float knockedDownFor;
        public float abilityTurnRate = 1f, itemTurnRate = 1f;

        public float GetBoost(RunStats s) => Mathf.Clamp(speedBoost + s.boost, 0f, 5f);
        public float MostlyGroundedValue() => 1f - Mathf.Clamp01(Mathf.Clamp01(sinceGrounded - 0.2f) * 5f);
        public float FullyAirbourneValue() => Mathf.Clamp01(Mathf.Clamp01(sinceGrounded - 0.2f) * 5f);
    }

    public Data data = new Data();
    public RunStats stats = new RunStats();
    public SlowRunStats slowStats = new SlowRunStats();
    public PlayerInputState input = new PlayerInputState();

    [Header("Health / energy")]
    public float maxHealth = 80f;
    public float health = 80f;
    public float energy;
    public float armor;

    [HideInInspector] public Rigidbody rig;
    [HideInInspector] public PlayerMovement movement;
    [HideInInspector] public PlayerSlowMovement slowMovement;
    public CapsuleCollider bodyCollider;

    public event Action<LandingType, float> onLanding;  // type, speed at landing
    public event Action onDeath;
    public bool dead;

    public const int TerrainLayer = 6, PlayerLayer = 7, EnemyLayer = 8, PropLayer = 12;
    public static int RunnableMask => (1 << TerrainLayer) | (1 << PropLayer);
    public static int GroundMask => 1 << TerrainLayer;

    public float Speed => rig ? rig.linearVelocity.magnitude : 0f;

    void Awake()
    {
        instance = this;
        rig = GetComponent<Rigidbody>();
        movement = GetComponent<PlayerMovement>();
        slowMovement = GetComponent<PlayerSlowMovement>();
        data.lastPos = transform.position;
    }

    void Start()
    {
        movement.DisableMode();
        slowMovement.EnableMode();
        SetLookDirection(transform.forward);
    }

    void Update()
    {
        if (Time.timeScale <= 0f) return;
        input.Read();
        UpdateLook();
        data.sinceSwitchMovementType += Time.deltaTime;
        data.sinceJumped += Time.deltaTime;
        if (data.dontLandFor > 0f) data.dontLandFor -= Time.deltaTime;
        if (data.invulnerability > 0f) data.invulnerability -= Time.deltaTime;
        if (data.knockedDownFor > 0f) data.knockedDownFor -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        if (data.perfectLandingsFor > 0f) data.perfectLandingsFor -= Time.fixedDeltaTime;
        if (data.speedBoost > 0f) data.speedBoost = Mathf.MoveTowards(data.speedBoost, 0f, Time.fixedDeltaTime * 0.5f);
    }

    void UpdateLook()
    {
        data.lookRotationValues += input.lookInput;
        data.lookRotationValues.y = Mathf.Clamp(data.lookRotationValues.y, -80f, 80f);
        data.lookDir = Quaternion.Euler(-data.lookRotationValues.y, data.lookRotationValues.x, 0f) * Vector3.forward;
    }

    // In the air WASD is for tricks, so the sled just follows the mouse (ignoreKeys).
    public Vector3 GetWorldMoveDir(bool autoRun = false, bool ignoreKeys = false)
    {
        var mv = ignoreKeys ? Vector3.forward : input.MovementInput3D(autoRun);
        return (Quaternion.Euler(0f, data.lookRotationValues.x, 0f) * mv).normalized;
    }

    public Vector3 FlatForward => Quaternion.Euler(0f, data.lookRotationValues.x, 0f) * Vector3.forward;
    public Vector3 FlatRight => Quaternion.Euler(0f, data.lookRotationValues.x, 0f) * Vector3.right;

    public void SetLookDirection(Vector3 dir)
    {
        var flat = Vector3.ProjectOnPlane(dir, Vector3.up);
        data.lookRotationValues.x = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        data.lookRotationValues.y = 10f;
    }

    public void LerpSpeedTo(float targetSpeed, float amount, float maxChange = 20f)
    {
        var v = rig.linearVelocity; float cur = v.magnitude;
        float n = Mathf.Clamp(Mathf.Lerp(cur, targetSpeed, amount), 0f, cur + maxChange);
        if (n >= cur && cur > 0.01f) rig.linearVelocity = v / cur * n;
    }

    public void AddEnergy(float amount) => energy = Mathf.Clamp01(energy + amount * (PlayerMods.instance ? PlayerMods.instance.energyGain : 1f));

    public void RaiseLanding(LandingType type, float speed) => onLanding?.Invoke(type, speed);

    public bool Invulnerable() => data.invulnerability > 0f || dead;

    public void TakeDamage(float dmg, Vector3 from)
    {
        if (Invulnerable() || dmg <= 0f) return;
        dmg = Mathf.Max(1f, dmg - armor);
        health -= dmg;
        data.invulnerability = 0.6f;
        GameFeel.Shake(6f, 0.25f);
        if (Hud.instance) Hud.instance.FlashDamage();
        if (health <= 0f && !dead) { health = 0f; dead = true; onDeath?.Invoke(); }
    }

    public void Heal(float amount) => health = Mathf.Min(maxHealth, health + amount);

    public void Kill() { if (dead) return; health = 0f; dead = true; onDeath?.Invoke(); }

    // Speed lost on impacts; speedLossResistance (Blindagem) keeps part of it.
    public void SlowDown(float factor)
    {
        factor = Mathf.Lerp(factor, 1f, Mathf.Clamp01(stats.speedLossResistance));
        rig.linearVelocity *= factor;
    }
}
