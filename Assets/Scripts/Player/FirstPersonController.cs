using UnityEngine;
using UnityEngine.InputSystem;

/// Walking, running, jumping and gravity. Looking is MouseLook's job.
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Hareket")]
    public float walkSpeed = 2.2f;
    public float sprintSpeed = 4f;
    [Tooltip("Air control while airborne (0 = none, 1 = full control).")]
    [Range(0f, 1f)] public float airControl = 0.4f;

    [Header("Fizik")]
    [Tooltip("World gravity. An exaggerated value (-20) made the jump look agile " +
             "but it also doubles the fall speed: fall damage will derive from this " +
             "acceleration later, and a wrong acceleration gives wrong damage.")]
    public float gravity = -9.81f;
    [Tooltip("Vertical jump height (metres). A standing person clears about 0.4 m; " +
             "less with heavy boots, expedition clothing and a pack. 1.1 m was " +
             "superhuman — the player was rising above their own height.")]
    public float jumpHeight = 0.4f;
    [Tooltip("Slopes steeper than this angle cannot be walked. The climbing system will take over from here.")]
    [Range(20f, 80f)] public float slopeLimit = 45f;
    public float stepOffset = 0.4f;

    CharacterController controller;
    Vector3 velocity;

    /// The controller is hanging above the snow: because the capsule is not touching bare
    /// terrain, `isGrounded` returns false. Walking and jumping have to count this flag too.

    /// For filtering out its own capsule: the ground ray starts inside the player and,
    /// unfiltered, the first thing it hits is their own collision volume.
    readonly RaycastHit[] groundHits = new RaycastHit[4];
    /// WHETHER THEY ARE ON THE GROUND.
    public bool OnGround => controller != null && controller.isGrounded;

    /// Speed multiplier for testing. 1 in normal play.
    public float SpeedMultiplier { get; set; } = 1f;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        controller.slopeLimit = slopeLimit;
        controller.stepOffset = stepOffset;
    }

    void Update()
    {
        // While free flight is on the controller is off: both use the SAME CharacterController
        // and flight disables it. Calling Move on a disabled controller printed an error every
        // frame — walking should already be silent at that point.
        if (!controller.enabled) return;

        // While the cursor is free the input belongs to the UI, not the game
        if (Cursor.lockState == CursorLockMode.Locked) Move();

        ApplyGravity();
    }

    void Move()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        Vector2 input = Vector2.zero;
        if (kb.wKey.isPressed) input.y += 1f;
        if (kb.sKey.isPressed) input.y -= 1f;
        if (kb.dKey.isPressed) input.x += 1f;
        if (kb.aKey.isPressed) input.x -= 1f;
        input = Vector2.ClampMagnitude(input, 1f);

        float speed = (kb.leftShiftKey.isPressed ? sprintSpeed : walkSpeed) * SpeedMultiplier;
        Vector3 wish = (transform.right * input.x + transform.forward * input.y) * speed;

        if (controller.isGrounded)
        {
            velocity.x = wish.x;
            velocity.z = wish.z;

            if (kb.spaceKey.wasPressedThisFrame)
                velocity.y = Mathf.Sqrt(-2f * gravity * jumpHeight);
        }
        else
        {
            velocity.x = Mathf.Lerp(velocity.x, wish.x, airControl * Time.deltaTime * 10f);
            velocity.z = Mathf.Lerp(velocity.z, wish.z, airControl * Time.deltaTime * 10f);
        }
    }

    void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

    }

}
