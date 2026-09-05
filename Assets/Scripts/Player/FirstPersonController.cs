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

    [Header("Slope response")]
    [Tooltip("Below this angle the ground behaves like flat terrain.")]
    [Range(0f, 30f)] public float slopeEffectStart = 8f;
    [Tooltip("Maximum uphill speed loss at the controller slope limit.")]
    [Range(0f, 0.4f)] public float uphillSpeedLoss = 0.18f;
    [Tooltip("Maximum across-slope speed loss at the controller slope limit.")]
    [Range(0f, 0.25f)] public float sideSlopeSpeedLoss = 0.08f;
    [Tooltip("Share of the sprint bonus retained at the controller slope limit.")]
    [Range(0f, 1f)] public float steepSprintRetention = 0.45f;
    [Tooltip("Share of normal braking retained while moving down the steepest slope.")]
    [Range(0.5f, 1f)] public float downhillBrakeRetention = 0.88f;

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

    /// Filtered only by the ground query; consumers use it to distinguish a step from a slope.
    public Vector3 GroundNormal { get; private set; } = Vector3.up;

    /// Current ground angle in degrees. Zero when no reliable ground contact was found.
    public float GroundSlopeDegrees { get; private set; }

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
        Vector3 wishDirection = transform.right * input.x + transform.forward * input.y;

        UpdateGroundSlope();
        float uphillAlignment = 0f;
        if (wishDirection.sqrMagnitude > 0.001f && GroundSlopeDegrees > 0f)
        {
            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, GroundNormal);
            if (downhill.sqrMagnitude > 0.0001f)
                uphillAlignment = Vector3.Dot(wishDirection.normalized, -downhill.normalized);
        }

        float speed = SlopeAdjustedSpeed(IsSprinting, GroundSlopeDegrees, uphillAlignment)
                    * SpeedMultiplier;
        Vector3 wish = wishDirection * speed;

        if (controller.isGrounded) lastGroundedTime = Time.time;

        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        float response = wish.sqrMagnitude > 0.001f
            ? groundAcceleration
            : groundDeceleration;
        if (wish.sqrMagnitude <= 0.001f && GroundSlopeDegrees > slopeEffectStart)
        {
            float slopeWeight = Mathf.InverseLerp(slopeEffectStart, slopeLimit,
                GroundSlopeDegrees);
            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, GroundNormal);
            float downhillTravel = downhill.sqrMagnitude > 0.0001f
                && horizontal.sqrMagnitude > 0.0001f
                ? Mathf.Max(0f, Vector3.Dot(horizontal.normalized, downhill.normalized))
                : 0f;
            response *= Mathf.Lerp(1f, downhillBrakeRetention,
                slopeWeight * downhillTravel);
        }

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

    float SlopeAdjustedSpeed(bool sprinting, float slopeDegrees, float uphillAlignment)
    {
        float slopeWeight = Mathf.InverseLerp(slopeEffectStart,
            Mathf.Max(slopeEffectStart + 0.01f, slopeLimit), slopeDegrees);
        float sprintShare = sprinting
            ? Mathf.Lerp(1f, steepSprintRetention, slopeWeight)
            : 0f;
        float flatSpeed = Mathf.Lerp(walkSpeed, sprintSpeed, sprintShare);

        float uphill = Mathf.Max(0f, uphillAlignment);
        float across = 1f - Mathf.Abs(uphillAlignment);
        float directionalLoss = uphill * uphillSpeedLoss + across * sideSlopeSpeedLoss;
        return flatSpeed * (1f - directionalLoss * slopeWeight);
    }

    void UpdateGroundSlope()
    {
        if (!controller.isGrounded)
        {
            GroundNormal = Vector3.up;
            GroundSlopeDegrees = 0f;
            return;
        }

        Vector3 origin = transform.position + Vector3.up * 0.35f;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits,
            stepOffset + 0.75f, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        Vector3 normal = Vector3.up;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.collider == null || hit.collider == controller || hit.distance >= nearest)
                continue;
            nearest = hit.distance;
            normal = hit.normal;
        }

        GroundNormal = normal;
        GroundSlopeDegrees = nearest < float.PositiveInfinity
            ? Vector3.Angle(normal, Vector3.up)
            : 0f;
    }

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
