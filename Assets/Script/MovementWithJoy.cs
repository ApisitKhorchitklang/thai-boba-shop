using UnityEngine;

/// <summary>
/// MovementWithJoy
/// - Works with a virtual joystick by calling SetJoystickInput(Vector2) from your UI joystick each frame.
/// - Falls back to Unity's "Horizontal"/"Vertical" axes when joystick input is not provided.
/// - Uses a CharacterController for movement (gravity and grounding handled).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class MovementWithJoy : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Movement speed in units/second")] public float speed = 5f;
    [Tooltip("Rotation speed when turning towards movement direction")] public float rotationSpeed = 10f;
    [Tooltip("If true, rotate the player to face movement direction")] public bool rotateToMovement = false;
    [Tooltip("If true, movement is relative to the main camera's forward/right vectors")] public bool moveRelativeToCamera = true; 
    [Tooltip("If true, injected joystick input (SetJoystickInput) will be used when non-zero. Otherwise fallback to keyboard axes.")] public bool preferInjectedInput = true;

    // Injected joystick input (call SetJoystickInput from your UI joystick script)
    private Vector2 injectedInput = Vector2.zero;

    [Header("Joystick Input")]
    [Tooltip("Optional UI Joystick. If set and non-zero, its input overrides keyboard and injected input.")]
    public Joystick joystick; 

    // Cached components
    private CharacterController characterController;
    private Transform cam;

    [Header("Character Controller Settings")]
    [Tooltip("Gravity acceleration (negative value)")] public float gravity = -9.81f;

    // Vertical velocity for gravity/grounding
    private float verticalVelocity = 0f;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        cam = Camera.main != null ? Camera.main.transform : null;
    }

    /// <summary>
    /// Call this from your virtual joystick UI each frame (e.g. in OnDrag) with the joystick's axis values in range [-1,1].
    /// </summary>
    public void SetJoystickInput(Vector2 input)
    {
        injectedInput = input;
    }

    /// <summary>
    /// Call this on pointer up to clear joystick input if your UI doesn't send a zero vector.
    /// </summary>
    public void ClearJoystickInput()
    {
        injectedInput = Vector2.zero;
    }

    void Update()
    {


        // Get 2D input (joystick > injected > keyboard)
        Vector2 input2D = Vector2.zero;
        if (joystick != null && joystick.Direction != Vector2.zero)
        {
            input2D = joystick.Direction;
        }
        else if (preferInjectedInput && injectedInput != Vector2.zero)
        {
            input2D = injectedInput;
        }
        else
        {
            input2D = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        }

        // Build 3D movement vector (x = strafe, z = forward)
        Vector3 rawMove = new Vector3(input2D.x, 0f, input2D.y);

        // Convert to camera-relative movement if requested
        Vector3 move = rawMove;
        if (moveRelativeToCamera && cam != null)
        {
            Vector3 forward = cam.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = cam.right;
            right.y = 0f;
            right.Normalize();

            move = forward * rawMove.z + right * rawMove.x;
        }

        // Desired velocity for movement
        Vector3 movement = move;
        if (movement.sqrMagnitude > 1f) movement = movement.normalized;
        movement *= speed;

        // Apply movement using CharacterController (gravity handled explicitly)
        if (characterController != null)
        {
            // Grounding: when grounded and moving downward, keep a small downward bias so controller stays grounded
            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -1f;
            }

            // Apply gravity
            verticalVelocity += gravity * Time.deltaTime;

            // Move (horizontal movement is in units/sec, verticalVelocity is units/sec)
            Vector3 finalMove = movement * Time.deltaTime + Vector3.up * verticalVelocity * Time.deltaTime;
            characterController.Move(finalMove);
        }
        else
        {
            // Fallback to transform movement if no CharacterController found (should not happen due to RequireComponent)
            transform.Translate(movement * Time.deltaTime, Space.World);
        }

        // Rotate player based on camera rotation (Y-axis only)
        if (cam != null)
        {
            // Extract only the Y-axis rotation from the camera
            float cameraYRotation = cam.eulerAngles.y;
            Quaternion targetRotation = Quaternion.Euler(0f, cameraYRotation, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Smooth rotation towards movement direction (only use horizontal component)
        if (rotateToMovement)
        {
            Vector3 lookDir = move;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion target = Quaternion.LookRotation(lookDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationSpeed * Time.deltaTime);
            }
        }
    }


}
