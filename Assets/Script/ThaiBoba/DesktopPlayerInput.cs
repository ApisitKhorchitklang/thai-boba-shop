using UnityEngine;

// Player input for both phone and PC.
// Touch: joystick (MovementWithJoy) + drag on the right half to look + on-screen buttons.
// PC: WASD to walk, mouse to look (cursor locked), left click / E = use, right click / Q = throw, Esc = pause.
// The mode comes from GameSettings (Auto / Touch / PC); touch widgets are hidden in PC mode.
public class DesktopPlayerInput : MonoBehaviour
{
    public CameraMobile cameraLook;
    public RaycastAim aim;
    [Tooltip("Drag area on the HUD (right side of the screen)")]
    public TouchScreenCamera touchLook;
    [Tooltip("Joystick, look area and buttons; hidden in PC mode")]
    public GameObject touchControlsRoot;
    [Tooltip("Key hints shown in PC mode")]
    public GameObject pcHintsRoot;
    public float touchScale = 1f;
    public float mouseScale = 14f;
    public float stepInterval = 0.42f;

    CharacterController body;
    float stepTimer;
    bool appliedPc;

    void Start()
    {
        if (cameraLook == null) cameraLook = GetComponentInChildren<CameraMobile>();
        if (aim == null) aim = GetComponentInChildren<RaycastAim>();
        if (touchLook == null) touchLook = FindFirstObjectByType<TouchScreenCamera>();
        body = GetComponent<CharacterController>();
        GameSettings.Changed += ApplyMode;
        ApplyMode();
    }

    void OnDestroy()
    {
        GameSettings.Changed -= ApplyMode;
    }

    void ApplyMode()
    {
        bool pc = appliedPc = GameSettings.UsePCControls;
        if (touchControlsRoot != null) touchControlsRoot.SetActive(!pc);
        if (pcHintsRoot != null) pcHintsRoot.SetActive(pc);
    }

    void OnDisable()
    {
        if (cameraLook != null) cameraLook.LockAxis = Vector2.zero;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        bool pc = GameSettings.UsePCControls;
        if (pc != appliedPc) ApplyMode();               // e.g. editor switched between Game view and Device Simulator
        bool menu = GameMenus.IsOpen;

        // cursor: locked while playing on PC, free in menus / touch mode
        bool lockCursor = pc && !menu && Application.isFocused;
        Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !lockCursor;

        Vector2 look = Vector2.zero;
        if (!menu)
        {
            if (!pc && touchLook != null && touchLook.Pressed)
                look = touchLook.TouchDist * touchScale;
            else if (pc)
                look = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * mouseScale * GameSettings.MouseSensitivity;
        }
        if (cameraLook != null) cameraLook.LockAxis = look;

        if (!menu && aim != null && pc)
        {
            bool arranging = DecorPlacement.Active;
            if (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0)) { if (arranging) DecorPlacement.Instance.Use(); else aim.Interact(); }
            if (Input.GetKeyDown(KeyCode.Q) || Input.GetMouseButtonDown(1)) { if (arranging) DecorPlacement.Instance.Cancel(); else aim.Throw(); }
        }

        // footsteps
        if (body != null && !menu)
        {
            Vector3 v = body.velocity;
            v.y = 0f;
            if (v.magnitude > 0.6f && body.isGrounded)
            {
                stepTimer -= Time.deltaTime;
                if (stepTimer <= 0f) { GameAudio.Play(Sfx.Step); stepTimer = stepInterval; }
            }
            else stepTimer = 0.1f;
        }
    }

    // On-screen buttons
    public void InteractButton()
    {
        if (DecorPlacement.Active) { DecorPlacement.Instance.Use(); return; }
        if (aim != null && !GameMenus.IsOpen) aim.Interact();
    }

    public void ThrowButton()
    {
        if (DecorPlacement.Active) { DecorPlacement.Instance.Cancel(); return; }
        if (aim != null && !GameMenus.IsOpen) aim.Throw();
    }

    // placement mode's own on-screen buttons
    public void RotateButton() { if (DecorPlacement.Active) DecorPlacement.Instance.Rotate(45f); }
    public void DoneArrangingButton() { if (DecorPlacement.Active) DecorPlacement.Instance.End(); }
}
