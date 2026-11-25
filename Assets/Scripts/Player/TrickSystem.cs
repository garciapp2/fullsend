using System;
using System.Collections.Generic;
using UnityEngine;

// settles to the nearest clean angle on its own, so short airs still land; land mid-rotation (or still
// grabbing) and you bail. Tricks chain into a combo while you keep landing them; the combo is banked a
// moment after your last trick, multiplied by the chain length. Points are score only.
// Rails: brush or land on one and you grind it automatically; Space jumps off.
public class TrickSystem : MonoBehaviour
{
    public static TrickSystem instance;
    PlayerCharacter pc;

    [Header("Air")]
    public float spinRate = 620f, flipRate = 470f;
    public bool airborne;
    public float airT, spin, flip;
    float spinVel, flipVel;
    int grabNow; float grabT;
    readonly List<string> airTricks = new List<string>();
    int airPoints;
    public string LiveTrick { get; private set; } = "";
    public float visualSpin, visualFlip;   // what PlayerVisual shows
    public int GrabPose => airborne ? grabNow : 0;

    [Header("Combo / score")]
    public int score, comboPoints, comboChain;
    public float comboTimer, comboWindow = 2.4f;
    public float ComboMult => 1f + Mathf.Min(comboChain - 1, 10) * 0.5f;
    public int bestCombo, bails, tricksLanded;

    [Header("Boost")]
    public float boostMeter = 0.35f;
    public bool boosting;
    public float boostAccel = 34f;

    [Header("Ground")]
    float diveHold;
    public bool Diving => pc && pc.data.movementType == MovementType.Fast && pc.data.isGrounded && pc.input.fastFallIsPressed;

    [Header("Grind")]
    public bool grinding;
    Rail rail; float railS, railDir = 1f, grindSpeed, grindT, grindCooldown;

    LandingType lastLanding; float lastLandingTime = -9f;

    public event Action<string, int, Color> onFeed;   // text, points, color
    public event Action onBail;

    static readonly string[] GrabNames = { "", "Indy", "Melão", "Superman", "Nariz" };
    public static readonly Color Gold = new Color(1f, 0.82f, 0.3f), Pink = new Color(1f, 0.5f, 0.8f), Cyan = new Color(0.45f, 0.9f, 1f), Red = new Color(1f, 0.35f, 0.3f);

    void Awake() { instance = this; pc = GetComponent<PlayerCharacter>(); }
    void Start() { pc.onLanding += (t, s) => { lastLanding = t; lastLandingTime = Time.time; }; }

    public void ResetRun()
    {
        score = comboPoints = comboChain = bestCombo = bails = tricksLanded = 0;
        comboTimer = 0f; boosting = false; boostMeter = pc ? pc.energy : 0f; airborne = false; spin = flip = visualSpin = visualFlip = 0f;
        if (grinding) ExitGrind(0f);
    }

    bool Fast => pc.data.movementType == MovementType.Fast;
    PlayerMods Mods => PlayerMods.instance;

    void Update()
    {
        if (Time.timeScale <= 0f || pc.dead) return;
        float dt = Time.deltaTime;
        var inp = pc.input;
        if (grindCooldown > 0f) grindCooldown -= dt;

        if (inp.crouchWasPressed) boostDepleted = false;
        if (infiniteBoost) { boostDepleted = false; pc.energy = 1f; }
        if (!boosting && Fast && inp.crouchIsPressed && !boostDepleted && pc.energy > BoostUse * BoostMinTime && pc.data.knockedDownFor <= 0f)
        {
            boosting = true; boostingFor = 0f;
            if (!pc.data.mostlyGrounded) pc.rig.AddForce(Vector3.up * BoostJump, ForceMode.VelocityChange);
        }
        if (boosting && (!Fast || pc.data.knockedDownFor > 0f || (!inp.crouchIsPressed && boostingFor > BoostMinTime))) boosting = false;
        boostMeter = pc.energy;

        // ---- grind ----
        if (grinding)
        {
            if (inp.jumpWasPressed) ExitGrind(11f);
            else { grindT += dt; LiveTrick = "Grind " + Mathf.RoundToInt(grindT * grindSpeed) + " m"; }
        }

        // ---- air ----
        bool inAir = Fast && !pc.data.isGrounded && !grinding;
        if (inAir)
        {
            if (!airborne) StartAir();
            airT += dt;
            if (airT > 0.1f)
            {
                float mx = inp.movementInput.x, my = inp.movementInput.y;
                if (Mathf.Abs(mx) > 0.2f) { spinVel = Mathf.MoveTowards(spinVel, Mathf.Sign(mx) * spinRate * Mods.spin, dt * 4000f); spin += spinVel * dt; }
                else { spinVel = 0f; spin = Mathf.MoveTowards(spin, Mathf.Round(spin / 180f) * 180f, 330f * dt); }
                if (Mathf.Abs(my) > 0.2f) { flipVel = Mathf.MoveTowards(flipVel, Mathf.Sign(my) * flipRate * Mods.spin, dt * 3000f); flip += flipVel * dt; }
                else { flipVel = 0f; flip = Mathf.MoveTowards(flip, Mathf.Round(flip / 360f) * 360f, 380f * dt); }
                // grabs
                if (inp.grab != 0 && airT > 0.15f)
                {
                    if (grabNow != inp.grab) { EndGrab(); grabNow = inp.grab; grabT = 0f; }
                    grabT += dt;
                }
                else EndGrab();
            }
            visualSpin = spin; visualFlip = flip;
            LiveTrick = Describe(false);
        }
        else
        {
            if (airborne && (pc.data.isGrounded || grinding)) Land();
            else if (airborne && !Fast) { airborne = false; airTricks.Clear(); }
            // after landing a 180 the sled turns forward again
            visualSpin = Mathf.MoveTowards(visualSpin, Mathf.Round(visualSpin / 360f) * 360f, 540f * dt);
            visualFlip = Mathf.MoveTowards(visualFlip, Mathf.Round(visualFlip / 360f) * 360f, 720f * dt);
            if (!grinding) LiveTrick = "";
        }

        // ---- combo banking ----
        if (comboTimer > 0f && !airborne && !grinding)
        {
            comboTimer -= dt;
            if (comboTimer <= 0f) BankCombo();
        }
    }

    // A_BoardBoost values: min speed 100, +10 m/s2 (half for the first 2 s), 0.3 energy per second, at least 0.65 s
    const float BoostTargetSpeed = 100f, BoostForce = 10f, BoostUse = 0.3f, BoostMinTime = 0.65f, BoostSuperAfter = 2f,
        BoostLerp = 5f, BoostMove = 5f, BoostJump = 10f;
    float boostingFor; bool boostDepleted;
    public static bool infiniteBoost;   // tutorial: Shift always boosts, no energy spent

    void BoardBoost(float dt)
    {
        boostingFor += dt;
        float k = boostingFor > BoostSuperAfter ? 1f : 0.5f;
        var v = pc.rig.linearVelocity; float speed = v.magnitude;
        if (speed < 0.1f) return;
        if (BoostTargetSpeed > speed)
        {
            speed = Mathf.MoveTowards(speed, BoostTargetSpeed, BoostMove * dt * k);
            speed = Mathf.Lerp(speed, BoostTargetSpeed, BoostLerp * dt * k);
            pc.rig.linearVelocity = v.normalized * speed;
        }
        pc.rig.AddForce(pc.rig.linearVelocity.normalized * BoostForce * Mods.boostAccel * k, ForceMode.Acceleration);
        if (!infiniteBoost) pc.energy = Mathf.Max(0f, pc.energy - dt * BoostUse * Mods.boostDrain);
        if (pc.energy <= 0f) { boosting = false; boostDepleted = true; }
    }

    void FixedUpdate()
    {
        if (pc.dead) return;
        if (boosting) BoardBoost(Time.fixedDeltaTime);
        if (grinding) GrindStep(Time.fixedDeltaTime);
        else if (Fast && grindCooldown <= 0f) TryStartGrind();
    }

    // ---------------- ground ----------------
    void Ollie(float charge)
    {
        var n = pc.data.groundNormal;
        pc.rig.linearVelocity += n * (8f + 11f * charge) * Mods.ollie;
        pc.data.dontLandFor = 0.18f;
        pc.data.sinceJumped = 0f;
        diveHold = 0f;
        if (PlayerVisual.instance) PlayerVisual.instance.Jump();
        Sfx.Play(Sfx.instance ? Sfx.instance.jump : null, 0.6f, 1f + charge * 0.2f);
    }

    // ---------------- air ----------------
    void StartAir()
    {
        airborne = true; airT = 0f; spin = flip = spinVel = flipVel = 0f;
        airTricks.Clear(); airPoints = 0; grabNow = 0; grabT = 0f;
    }

    void EndGrab()
    {
        if (grabNow != 0 && grabT > 0.12f)
        {
            int pts = Mathf.RoundToInt(150 + grabT * 320);
            airTricks.Add(GrabNames[grabNow] + (grabT > 1f ? " longo" : ""));
            airPoints += pts;
        }
        grabNow = 0; grabT = 0f;
    }

    string Describe(bool final)
    {
        var parts = new List<string>(airTricks);
        int halfSpins = Mathf.RoundToInt(Mathf.Abs(spin) / 180f);
        int flips = Mathf.RoundToInt(Mathf.Abs(flip) / 360f);
        if (!final)
        {
            halfSpins = Mathf.FloorToInt(Mathf.Abs(spin) / 180f);
            flips = Mathf.FloorToInt(Mathf.Abs(flip) / 360f);
            if (grabNow != 0 && grabT > 0.12f) parts.Add(GrabNames[grabNow]);
        }
        if (flips > 0) parts.Insert(0, (flips == 1 ? "" : flips == 2 ? "Duplo " : flips == 3 ? "Triplo " : flips + "x ") + (flip < 0 ? "Mortal de costas" : "Mortal"));
        if (halfSpins > 0) parts.Insert(0, (halfSpins * 180) + "°");
        return string.Join(" + ", parts);
    }

    void Land()
    {
        airborne = false;
        bool grabHeld = grabNow != 0 && grabT > 0.12f;
        EndGrab();
        float spinDev = Mathf.Abs(Mathf.DeltaAngle(spin, Mathf.Round(spin / 180f) * 180f));
        float flipDev = Mathf.Abs(Mathf.DeltaAngle(flip, 0f));
        bool rotating = Mathf.Abs(spin) > 40f || Mathf.Abs(flip) > 40f;
        bool bail = rotating && (spinDev > 55f || flipDev > 60f) || grabHeld && airT > 0.3f && !grinding;
        if (lastLanding == LandingType.Bad && Time.time - lastLandingTime < 0.15f && rotating) bail = true;
        if (bail) { Bail(grabHeld ? "Segurou demais!" : "CAIU!"); return; }

        int halfSpins = Mathf.RoundToInt(Mathf.Abs(spin) / 180f);
        int flips = Mathf.RoundToInt(Mathf.Abs(flip) / 360f);
        int pts = airPoints + halfSpins * 110 + flips * 450;
        if (airT > 1.4f) pts += Mathf.RoundToInt((airT - 1.4f) * 90f);
        string name = Describe(true);
        if (airT > 2.2f) name = (name.Length > 0 ? name + " + " : "") + "Voo " + airT.ToString("0.0") + "s";
        bool recent = Time.time - lastLandingTime < 0.15f;
        if (recent && lastLanding == LandingType.Perfect) { pts = Mathf.RoundToInt(pts * 1.5f) + 250; name = (name.Length > 0 ? name + " + " : "") + "Pouso perfeito"; }
        else if (recent && lastLanding == LandingType.Good) { pts += 100; if (pts > 100) name += " (limpo)"; else name = "Pouso bom"; }
        if (pts <= 0 || name.Length == 0) return;
        AddTrick(name, pts, halfSpins + flips >= 3 ? Pink : Gold);
        if (halfSpins + flips >= 3) ComicFX.Impact(0.6f, 0.05f);
        tricksLanded++;
    }

    public void AddTrick(string name, int pts, Color c)
    {
        comboPoints += pts; comboChain++;
        comboTimer = comboWindow + Mods.comboWindow;
        if (Mods.trickSpeed > 0f && Fast) pc.LerpSpeedTo(pc.Speed + Mods.trickSpeed, 1f, Mods.trickSpeed);
        onFeed?.Invoke(name, pts, c);
        Sfx.Play(Sfx.instance ? Sfx.instance.trick : null, 0.5f, 1f + Mathf.Min(comboChain, 10) * 0.05f);
    }

    // small stuff that doesn't extend the combo (sparks, rings)
    public void AddPoints(int pts, float boost)
    {
        score += pts;
    }

    public void ForceBank() { if (comboPoints > 0) BankCombo(); comboTimer = 0f; }

    void BankCombo()
    {
        if (comboPoints <= 0) { comboChain = 0; return; }
        int total = Mathf.RoundToInt(comboPoints * ComboMult);
        score += total; bestCombo = Mathf.Max(bestCombo, total);
        if (comboChain >= 2) { onFeed?.Invoke("COMBO x" + ComboMult.ToString("0.#"), total, Cyan); if (comboChain >= 4) ComicFX.Impact(0.7f, 0.04f); }
        comboPoints = 0; comboChain = 0;
    }

    public void Bail(string text, float damage = -1f, bool physics = true)
    {
        airborne = false;
        if (Mods.bailShieldLeft > 0)
        {
            Mods.bailShieldLeft--; comboPoints = 0; comboChain = 0; comboTimer = 0f;
            visualSpin = Mathf.Round(visualSpin / 360f) * 360f; visualFlip = Mathf.Round(visualFlip / 360f) * 360f;
            onFeed?.Invoke("Capacete salvou!", 0, Cyan);
            return;
        }
        comboPoints = 0; comboChain = 0; comboTimer = 0f; bails++;
        // a bail costs health too (hazards pass 0 and deal their own damage)
        float dmg = damage < 0f ? 20f * (RunManager.instance ? RunManager.instance.Intensity : 1f) : damage;
        if (dmg > 0f) pc.TakeDamage(dmg, pc.transform.position);
        if (physics) { pc.SlowDown(0.55f); pc.data.knockedDownFor = 0.7f; }
        visualSpin = Mathf.Round(visualSpin / 360f) * 360f; visualFlip = Mathf.Round(visualFlip / 360f) * 360f;
        GameFeel.Shake(10f, 0.35f);
        ComicFX.Split(1.5f);
        onFeed?.Invoke(text, 0, Red);
        onBail?.Invoke();
        Sfx.Play(Sfx.instance ? Sfx.instance.bail : null, 0.8f, 1f);
    }

    // ---------------- grind ----------------
    void TryStartGrind()
    {
        if (Rail.all.Count == 0) return;
        var pos = pc.rig.position; var v = pc.rig.linearVelocity;
        if (v.y > 6f) return;   // still going up
        foreach (var r in Rail.all)
        {
            if (r.points.Count < 2) continue;
            float s = r.Closest(pos, out var pt, out var tan);
            var d = pos - pt;
            if (d.y < -1.3f || d.y > 2.4f) continue;
            if (new Vector2(d.x, d.z).sqrMagnitude > 1.9f * 1.9f) continue;
            var flatV = new Vector3(v.x, 0f, v.z); var flatT = new Vector3(tan.x, 0f, tan.z);
            if (flatV.sqrMagnitude < 25f) continue;
            float dot = Vector3.Dot(flatV.normalized, flatT.normalized);
            if (Mathf.Abs(dot) < 0.6f) continue;
            if (s < 0.5f && dot < 0f || s > r.Length - 0.5f && dot > 0f) continue;
            StartGrind(r, s, dot > 0f ? 1f : -1f, v.magnitude * Mathf.Abs(dot));
            return;
        }
    }

    void StartGrind(Rail r, float s, float dir, float speed)
    {
        if (airborne) Land();
        if (pc.data.knockedDownFor > 0f) return;
        grinding = true; rail = r; railS = s; railDir = dir; grindSpeed = Mathf.Max(speed, 35f); grindT = 0f;
        pc.movement.enabled = false;
        pc.data.isGrounded = false;
        Sfx.Play(Sfx.instance ? Sfx.instance.grindStart : null, 0.7f, 1f);
        GameFeel.Shake(2f, 0.1f);
    }

    void GrindStep(float dt)
    {
        rail.PointAt(railS, out var tan);
        grindSpeed = Mathf.Clamp(grindSpeed - tan.y * railDir * 20f * dt + Mods.grindAccel * dt, 30f, 170f);
        railS += railDir * grindSpeed * dt;
        if (railS <= 0f || railS >= rail.Length) { ExitGrind(4f); return; }
        var p = rail.PointAt(railS, out tan);
        pc.rig.MovePosition(p + Vector3.up * 0.4f);
        pc.rig.linearVelocity = tan * railDir * grindSpeed;
        pc.data.lastPos = p + Vector3.up * 0.4f;
    }

    void ExitGrind(float up)
    {
        if (!grinding) return;
        grinding = false; grindCooldown = 0.4f;
        rail.PointAt(railS, out var tan);
        pc.movement.enabled = true;
        pc.data.isGrounded = false; pc.data.sinceGrounded = 0f; pc.data.dontLandFor = 0.15f;
        pc.data.lastPos = pc.rig.position;
        pc.rig.linearVelocity = tan * railDir * grindSpeed + Vector3.up * up;
        int meters = Mathf.RoundToInt(grindT * grindSpeed);
        if (meters > 3) AddTrick("Grind " + meters + " m", 60 + meters * 18, Cyan);
        LiveTrick = "";
    }
}
