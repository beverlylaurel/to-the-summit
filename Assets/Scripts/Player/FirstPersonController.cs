using UnityEngine;
using UnityEngine.InputSystem;

/// Walking, running, jumping and gravity. Looking is MouseLook's job.
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Hareket")]
    public float walkSpeed = 2.2f;
    public float sprintSpeed = 4f;
    [Tooltip("Ground acceleration in metres per second squared.")]
    [Min(0.1f)] public float groundAcceleration = 11f;
    [Tooltip("Ground braking in metres per second squared. Kept higher than acceleration " +
             "so releasing input feels planted without stopping in one frame.")]
    [Min(0.1f)] public float groundDeceleration = 15f;
    [Tooltip("Air control while airborne (0 = none, 1 = full control).")]
    [Range(0f, 1f)] public float airControl = 0.4f;

    [Header("Girdi toleransi")]
    [Tooltip("A jump pressed this shortly before landing is remembered (seconds).")]
    [Range(0f, 0.25f)] public float jumpBufferSeconds = 0.12f;
    [Tooltip("A jump remains possible this shortly after walking off an edge (seconds).")]
    [Range(0f, 0.25f)] public float coyoteSeconds = 0.10f;

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
    float lastGroundedTime = float.NegativeInfinity;
    float lastJumpPressedTime = float.NegativeInfinity;

    /// The controller is hanging above the snow: because the capsule is not touching bare
    /// terrain, `isGrounded` returns false. Walking and jumping have to count this flag too.

    /// For filtering out its own capsule: the ground ray starts inside the player and,
    /// unfiltered, the first thing it hits is their own collision volume.
    readonly RaycastHit[] groundHits = new RaycastHit[4];
    /// WHETHER THEY ARE ON THE GROUND.
    public bool OnGround => controller != null && controller.isGrounded;

    /// Whether the current deliberate movement target is the sprint speed.
    public bool IsSprinting { get; private set; }

    /// Speed multiplier for testing. 1 in normal play.
    public float SpeedMultiplier { get; set; } = 1f;

    /// Full-screen interactions can stop deliberate movement while gravity and grounding keep
    /// running. Disabling the whole component would freeze an airborne player.
    public bool InputEnabled { get; set; } = true;

    /// A LIMIT SOMEONE ELSE IMPOSES ON HORIZONTAL MOVEMENT.
    ///
    /// Given the player's position and the velocity they want, it answers with the velocity
    /// they are allowed. The controller does not know or care what the reason is -- the sea
    /// registers one of these to stop a wade before the water reaches the eyes, and nothing
    /// about water is written here.
    ///
    /// Null means no limit, which is the normal case away from water.
    public System.Func<Vector3, Vector3, Vector3> LimitHorizontal { get; set; }

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

        // Movement still brakes when an interaction owns the input. Keeping the previous
        // horizontal velocity made the player drift behind a gallery or pause-like screen.
        Move(InputEnabled && Cursor.lockState == CursorLockMode.Locked);

        ApplyGravity();
    }

    void Move(bool acceptsInput)
    {
        var kb = Keyboard.current;

        Vector2 input = Vector2.zero;
        if (acceptsInput && kb != null)
        {
            if (kb.wKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed) input.y -= 1f;
            if (kb.dKey.isPressed) input.x += 1f;
            if (kb.aKey.isPressed) input.x -= 1f;
            if (kb.spaceKey.wasPressedThisFrame) lastJumpPressedTime = Time.time;
        }
        input = Vector2.ClampMagnitude(input, 1f);

        IsSprinting = acceptsInput && kb != null && kb.leftShiftKey.isPressed
                   && input.sqrMagnitude > 0.001f;
        float speed = (IsSprinting ? sprintSpeed : walkSpeed) * SpeedMultiplier;
        Vector3 wish = (transform.right * input.x + transform.forward * input.y) * speed;

        if (controller.isGrounded) lastGroundedTime = Time.time;

        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        float response = wish.sqrMagnitude > 0.001f
            ? groundAcceleration
            : groundDeceleration;

        if (controller.isGrounded)
        {
            horizontal = StepHorizontal(horizontal, wish, response, Time.deltaTime);
        }
        else
        {
            horizontal = StepHorizontal(horizontal, wish,
                groundAcceleration * airControl, Time.deltaTime);
        }

        velocity.x = horizontal.x;
        velocity.z = horizontal.z;

        bool buffered = Time.time - lastJumpPressedTime <= jumpBufferSeconds;
        bool mayJump = Time.time - lastGroundedTime <= coyoteSeconds;
        if (buffered && mayJump)
        {
            velocity.y = Mathf.Sqrt(-2f * gravity * jumpHeight);
            lastJumpPressedTime = float.NegativeInfinity;
            lastGroundedTime = float.NegativeInfinity;
        }
    }

    // Kept separate so acceleration and braking can be verified without synthesising keyboard input.
    static Vector3 StepHorizontal(Vector3 current, Vector3 target, float rate, float deltaTime) =>
        Vector3.MoveTowards(current, target, Mathf.Max(0f, rate) * Mathf.Max(0f, deltaTime));

    void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;

        // The limit is applied to the MOVE, not to `velocity`: written back into the state
        // it would accumulate, and the player would stay stuck after stepping away.
        Vector3 step = velocity;
        if (LimitHorizontal != null)
        {
            Vector3 allowed = LimitHorizontal(transform.position, new Vector3(step.x, 0f, step.z));
            step.x = allowed.x;
            step.z = allowed.z;
        }

        controller.Move(step * Time.deltaTime);

    }

}
