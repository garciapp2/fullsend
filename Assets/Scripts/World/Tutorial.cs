using UnityEngine;

// Controls it teaches: on the sled the MOUSE steers (it goes downhill by itself, W isn't needed); in the air Space
// = fast fall and W/S/A/D = flips and spins; Shift = boost (energy from landings) and, on foot, hold to get back on.
// moment (first time in the air, first landing, enough energy...), slows time down and shows the keys, and lets
// time run again once you do it. During the tutorial delivery the collapse and the cold-pizza timer are off.
// Runs on the first ever delivery (PlayerPrefs) or from the menu (T · TUTORIAL).
// Also always on, tutorial or not: fall off the sled and a prompt tells you to hold Shift, with the charge bar.
public class Tutorial : MonoBehaviour
{
    public static Tutorial instance;
    public static bool forceNext;           // menu: play the tutorial on the next run
    public static bool Active => instance && instance.active;
    const string DoneKey = "fullsend_tutorial_done";

    enum Step { Steer, WaitAir, FastFall, Landing, Boost, WaitTrick, Trick, Done, Off }
    Step step = Step.Off;
    bool active;
    float stepT, realT, doneFor, slow = 1f, baseFixed = 0.02f, footT, steerT;
    int landings, tricksAtStart, airsSeen;
    bool wasAir;

    void Awake() { instance = this; baseFixed = Time.fixedDeltaTime; }

    PlayerCharacter P => RunManager.instance ? RunManager.instance.player : null;
    static Hud H => Hud.instance;

    // called by RunManager when a delivery starts (Go)
    public void OnLevelStart(int level)
    {
        active = level == 0 && (forceNext || PlayerPrefs.GetInt(DoneKey, 0) == 0);
        forceNext = false;
        landings = 0; airsSeen = 0; boostShown = false; boostT = 0f;
        SetSlow(1f);
        if (active) { Enter(Step.Steer); if (P) P.onLanding -= Landed; if (P) P.onLanding += Landed; }
        else step = Step.Off;
    }

    void Landed(LandingType t, float speed)
    {
        if (step != Step.Landing) return;
        landings++;
        if (t == LandingType.Good || t == LandingType.Perfect) Complete(t == LandingType.Perfect ? "POUSO PERFEITO!" : "BOM POUSO!", Step.Boost);
        else if (landings >= 3) Complete("VAI PEGANDO O JEITO", Step.Boost);
    }

    void Enter(Step s) { step = s; stepT = 0f; realT = 0f; }

    void Complete(string text, Step next)
    {
        SetSlow(1f);
        H.PromptDone(text);
        doneFor = 1.1f;
        Enter(next);
    }

    void SetSlow(float s)
    {
        bool same = Mathf.Approximately(slow, s) && s == 1f;   // don't cut a hit stop short
        slow = s;
        var rm = RunManager.instance;
        if (same || rm && rm.state == RunManager.State.Paused) return;
        Time.timeScale = s; Time.fixedDeltaTime = baseFixed * s;
    }

    void OnDisable() { Time.fixedDeltaTime = baseFixed; TrickSystem.infiniteBoost = false; }

    void Update()
    {
        var rm = RunManager.instance; var pc = P;
        if (!rm || !pc || !H) return;
        TrickSystem.infiniteBoost = active && rm.state == RunManager.State.Run;
        if (rm.state == RunManager.State.Paused) return;
        if (rm.state != RunManager.State.Run)
        {
            if (slow != 1f) SetSlow(1f);
            if (rm.state != RunManager.State.Countdown) H.HidePrompt();
            if (rm.state == RunManager.State.Results || rm.state == RunManager.State.Menu) active = false;
            return;
        }
        if (slow != 1f && Time.timeScale != slow) SetSlow(slow);   // something reset the time scale (pause / resume)
        float dt = Time.unscaledDeltaTime;
        stepT += Time.deltaTime; realT += dt;

        // ---- always: fell off the sled -> hold Shift ----
        bool onFoot = pc.data.movementType == MovementType.Slow && !pc.dead;
        footT = onFoot ? footT + dt : 0f;
        if (footT > 0.5f)
        {
            if (slow != 1f) SetSlow(1f);
            H.Prompt("CAIU DO TRENÓ!", "SEGURE", "[SHIFT]", "PRA VOLTAR");
            H.PromptFill(pc.slowMovement.chargeCounter);
            return;
        }
        if (!active || step == Step.Off) { H.HidePrompt(); return; }

        if (doneFor > 0f) { doneFor -= dt; if (doneFor <= 0f) H.HidePrompt(); return; }
        var inp = pc.input; var ts = TrickSystem.instance;
        bool air = !pc.data.isGrounded && pc.data.sinceGrounded > 0.3f && pc.data.distanceToGround > 5f && !(ts && ts.grinding);
        if (air && !wasAir) airsSeen++;
        wasAir = air;

        switch (step)
        {
            case Step.Steer:
                H.Prompt("O TRENÓ DESCE SOZINHO  ·  NÃO PRECISA DE W", "MEXA O", "[MOUSE]", "PRA CURVAR");
                H.PromptFill(Mathf.Clamp01(steerT / 1.2f));
                if (Mathf.Abs(inp.lookInput.x) > 0.4f) steerT += dt;
                if (realT > 2.5f && steerT > 1.2f) Complete("ISSO!", Step.WaitAir);
                else if (realT > 9f) { SetSlow(1f); H.HidePrompt(); Enter(Step.WaitAir); }
                break;
            case Step.WaitAir:
                if (air) { Enter(Step.FastFall); SetSlow(0.25f); }
                break;
            case Step.FastFall:
                H.Prompt("NO AR", "SEGURE", "[ESPAÇO]", "PRA DESCER RÁPIDO");
                H.PromptFill(-1f);
                if (inp.fastFallIsPressed && realT > 0.4f) { Complete("DESCENDO!", Step.Landing); doneFor = 0.6f; }
                else if (!air && pc.data.isGrounded) { SetSlow(1f); H.HidePrompt(); Enter(Step.WaitAir); }
                break;
            case Step.Landing:
                H.Prompt("POUSE NA DESCIDA", "POUSO BOM =", "VELOCIDADE + ENERGIA");
                H.PromptFill(-1f);
                break;
            case Step.Boost:
                // shown once you're on the ground; from then on Shift counts on the ground or in the air
                if (!boostShown) { if (stepT < 0.3f || !pc.data.isGrounded) break; boostShown = true; boostT = 0f; SetSlow(0.35f); }
                H.Prompt("TURBO  ·  INFINITO NO TREINO, NO JOGO GASTA A ENERGIA DOS POUSOS", "SEGURE", "[SHIFT]", "PRA ACELERAR");
                H.PromptFill(Mathf.Clamp01(boostT / 0.8f));
                if (ts && ts.boosting) { if (slow != 1f) SetSlow(1f); boostT += dt; if (boostT > 0.8f) { boostShown = false; Complete("VOANDO!", Step.WaitTrick); } }
                break;
            case Step.WaitTrick:
                if (air && pc.data.distanceToGround > 7f) { tricksAtStart = ts ? ts.tricksLanded : 0; Enter(Step.Trick); SetSlow(0.3f); }
                break;
            case Step.Trick:
                H.Prompt("MANOBRAS NO AR", "[W]", "[S]", "MORTAL", "[A]", "[D]", "GIRO", "· POUSE RETO!");
                H.PromptFill(-1f);
                if (slow != 1f && (inp.movementInput.sqrMagnitude > 0.1f || inp.grab != 0)) SetSlow(1f);
                if (ts && ts.tricksLanded > tricksAtStart) Complete("MANDOU BEM!", Step.Done);
                else if (pc.data.isGrounded && stepT > 0.5f) { if (airsSeen > 6) Complete("TREINA NAS PRÓXIMAS", Step.Done); else { SetSlow(1f); H.HidePrompt(); Enter(Step.WaitTrick); } }
                break;
            case Step.Done:
                H.Prompt("PRONTO!", "AGORA É ENTREGAR ANTES DA PIZZA ESFRIAR");
                H.PromptFill(-1f);
                PlayerPrefs.SetInt(DoneKey, 1); PlayerPrefs.Save();
                if (realT > 3.5f) { H.HidePrompt(); step = Step.Off; }
                break;
        }
    }
    float boostT; bool boostShown;
}
