using UnityEngine;
using UnityEngine.InputSystem;

// On foot: Space jump, Shift held = charge onto the sled.
// On the sled: Space held = dive into the slope (released = jump), Shift = boost.
// In the air: mouse steers, A/D spin, W/S flip, Q / E / mouse buttons grab, Space = fast fall.
public class PlayerInputState
{
    public Vector2 movementInput;
    public Vector2 lookInput;
    public bool fastFallIsPressed;
    public bool jumpWasPressed;
    public bool crouchIsPressed;
    public bool crouchWasPressed;
    public bool jumpWasReleased;
    public int grab;              // 0 none, 1..4 which grab is held
    public static bool testCrouch;  // tests: hold Shift from a script

    public float mouseSensitivity = 0.12f;
    public float stickSensitivity = 180f;

    public void Read()
    {
        movementInput = Vector2.zero; lookInput = Vector2.zero;
        fastFallIsPressed = jumpWasPressed = crouchIsPressed = crouchWasPressed = jumpWasReleased = false; grab = 0;
        var k = Keyboard.current; var m = Mouse.current; var g = Gamepad.current;
        if (k != null)
        {
            if (k.wKey.isPressed) movementInput.y += 1; if (k.sKey.isPressed) movementInput.y -= 1;
            if (k.dKey.isPressed) movementInput.x += 1; if (k.aKey.isPressed) movementInput.x -= 1;
            fastFallIsPressed |= k.spaceKey.isPressed; jumpWasPressed |= k.spaceKey.wasPressedThisFrame;
            crouchIsPressed |= k.leftShiftKey.isPressed || k.leftCtrlKey.isPressed;
            crouchWasPressed |= k.leftShiftKey.wasPressedThisFrame || k.leftCtrlKey.wasPressedThisFrame;
            jumpWasReleased |= k.spaceKey.wasReleasedThisFrame;
            if (k.qKey.isPressed) grab = 1; else if (k.eKey.isPressed) grab = 2;
        }
        if (m != null && grab == 0 && Cursor.lockState == CursorLockMode.Locked)
        {
            if (m.leftButton.isPressed) grab = 3; else if (m.rightButton.isPressed) grab = 4;
        }
        if (m != null && Cursor.lockState == CursorLockMode.Locked)
            lookInput += m.delta.ReadValue() * mouseSensitivity;
        if (g != null)
        {
            var ls = g.leftStick.ReadValue(); if (ls.sqrMagnitude > 0.04f) movementInput += ls;
            lookInput += g.rightStick.ReadValue() * stickSensitivity * Time.deltaTime;
            fastFallIsPressed |= g.buttonSouth.isPressed; jumpWasPressed |= g.buttonSouth.wasPressedThisFrame;
            crouchIsPressed |= g.leftTrigger.isPressed || g.buttonEast.isPressed;
            crouchWasPressed |= g.leftTrigger.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame;
            jumpWasReleased |= g.buttonSouth.wasReleasedThisFrame;
            if (grab == 0) { if (g.buttonWest.isPressed) grab = 1; else if (g.buttonNorth.isPressed) grab = 2; else if (g.leftShoulder.isPressed) grab = 3; else if (g.rightShoulder.isPressed) grab = 4; }
        }
        crouchIsPressed |= testCrouch;
        movementInput = Vector2.ClampMagnitude(movementInput, 1f);
    }

    public Vector3 MovementInput3D(bool autoRun)
    {
        var v = new Vector3(movementInput.x, 0f, movementInput.y);
        if (autoRun && v.z < 0.1f && Mathf.Abs(v.x) < 0.1f) v.z = 1f;
        else if (autoRun) v.z = Mathf.Max(v.z, 0.35f);
        return v;
    }
}
