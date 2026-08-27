using System;
using UnityEngine;

/// THE BIKE. Independent of the project: it does not know the route, terrain, weather or snow
/// systems, and they do not know it. Its only dependency is Unity — a `CharacterController`, a
/// `LayerMask`, a settings asset. Moved to another project as a folder, it works.
///
/// THE SPEED COMES FROM PHYSICS, NOT FROM A TABLE. Written as "5 m/s on the road, 4 on a path",
/// every gradient would need a new rule and they would all become lies once the terrain changed.
/// Here the rider's POWER is given; the speed comes out of the balance of power, mass, gradient
/// and resistances:
///
///     P = v · (Crr·m·g + m·g·sin(gradient) + ½·ρ·CdA·v²)
///
/// The result behaves correctly on its own: fast on the flat, at walking pace on a 10% climb,
/// fast enough on a descent to need braking. There is no single "slow down uphill" rule.
///
/// KINEMATIC, NOT A RIGIDBODY. Building wheel physics on terrain brings bouncing, snagging and
/// unpredictable kicks; `CharacterController` resolves the collision and this component computes
/// the motion. It is also the same model as the player's walking controller.
[RequireComponent(typeof(CharacterController))]
public class BikeController : MonoBehaviour
{
    [SerializeField] BikeSettings settings;

    CharacterController controller;
    BikeInput input;

    /// The surface normal perpendicular to the ground. The gradient and the lean derive from it.
    Vector3 groundNormal = Vector3.up;

    float verticalSpeed;
    float lean;

    /// Speed along the road (m/s). No going backwards: a bike is not pedalled in reverse.
    public float Speed { get; private set; }

    /// The gradient in the direction of travel (a ratio, 0.10 = a 10% climb).
    public float Grade { get; private set; }

    public bool Grounded { get; private set; }

    /// The visual lean angle (degrees). The body model reads it; the controller itself rotates no
    /// mesh — so that the visual and the physics stay apart.
    public float LeanAngle => lean;

    /// The largest lean angle in the settings. The visual components read it to convert the lean
    /// into a RATIO; exposing the settings themselves would open the door to every consumer
    /// reading whatever field it liked.
    public float MaxLean => settings != null ? settings.maxLean : 1f;

    /// The ground's rolling resistance. If the game world knows it (asphalt, gravel, snow) it
    /// supplies it here; if not, the value in the settings holds. The bike does not know ground
    /// TYPES, it only reads the number.
    public float RollingResistance { get; set; } = -1f;

    /// The settings asset can be supplied from outside too: two different bikes in the same scene.
    public void Bind(BikeSettings source) => settings = source;

    public void SetInput(BikeInput value) => input = value.Sanitised();

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        FitCapsule();
    }

    void OnEnable()
    {
        if (settings == null)
            throw new InvalidOperationException($"{nameof(BikeController)}: the settings are not assigned.");
    }

    /// THE WHEEL IS SET ON THE GROUND. The capsule is built from the model, but there can be a
    /// gap between the model and the root: measured, the capsule's base was seven centimetres
    /// above the collision (the skin width, correct) while the model's bottom was thirty-five
    /// centimetres above the capsule. So the physics stood in the right place and the image hung in the air.
    ///
    /// The setup script does this once but the old position can survive in the scene; here it is
    /// remeasured on every launch. The capsule is switched off and on because while
    /// `CharacterController` is enabled a direct position assignment is undone on the next frame.
    void Start()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);

        var ray = new Ray(new Vector3(bounds.center.x, bounds.min.y + 3f, bounds.center.z),
            Vector3.down);

        if (!Physics.Raycast(ray, out RaycastHit hit, 20f, settings.groundLayers,
                QueryTriggerInteraction.Ignore))
            return;

        float gap = bounds.min.y - hit.point.y;
        if (Mathf.Abs(gap) < 0.005f) return;

        controller.enabled = false;
        transform.position -= Vector3.up * gap;
        controller.enabled = true;
    }

    /// THE COLLISION CAPSULE IS MEASURED FROM THE MODEL. The setup script used to write this once
    /// and the old values left in the scene took precedence: with the capsule's base forty
    /// centimetres below the model's bottom the bike stood in the air and its shadow came out a
    /// metre away beneath it.
    ///
    /// The measurement is taken again on every launch: whether the model changed or the component
    /// in the scene stayed old, the capsule sits on the model.
    void FitCapsule()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);

        float height = Mathf.Max(0.6f, bounds.size.y);
        float bottom = bounds.min.y - transform.position.y;

        controller.height = height;
        controller.radius = Mathf.Min(0.3f, height * 0.4f);

        // The centre is measured from the BOTTOM of the model: if there is a gap between the root
        // and the model's base, the capsule accounts for that gap too.
        controller.center = new Vector3(0f, bottom + height * 0.5f, 0f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        ReadGround();
        Steer(dt);
        Accelerate(dt);
        Move(dt);
    }

    // ------------------------------------------------------------------- zemin

    /// The ground normal and the gradient. The ray is cast slightly above the controller's BASE:
    /// a ray cast from the base exactly starts inside the surface on sloping ground and hits nothing.
    void ReadGround()
    {
        Grounded = controller.isGrounded;

        Vector3 origin = transform.position + Vector3.up * (controller.radius + 0.1f);
        float reach = controller.radius + controller.skinWidth + 0.6f;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, reach,
                settings.groundLayers, QueryTriggerInteraction.Ignore))
        {
            groundNormal = hit.normal;
            Grounded = true;
        }
        else groundNormal = Vector3.up;

        // The gradient in the direction of travel: how many metres we rise per metre forward.
        // It follows in closed form from the surface normal, no second ray is needed.
        var forward = new Vector2(transform.forward.x, transform.forward.z);
        if (forward.sqrMagnitude < 1e-6f || Mathf.Abs(groundNormal.y) < 1e-3f)
        {
            Grade = 0f;
            return;
        }

        forward.Normalize();
        Grade = -(groundNormal.x * forward.x + groundNormal.z * forward.y) / groundNormal.y;
    }

    // -------------------------------------------------------------- direksiyon

    /// The turn comes from the LEAN, not from a handlebar angle. A bike takes a corner by leaning
    /// and the radius is set by the speed: r = v² / (g·tan(lean)). So it turns wide when fast and
    /// tight when slow — a fixed "turn rate" number could never give that.
    ///
    /// On a stationary bike the formula goes to infinity; `maxYawRate` steps in as the
    /// handlebar's physical limit.
    void Steer(float dt)
    {
        float targetLean = input.steer * settings.maxLean;

        // The lean is not instantaneous but smoothed: a real rider shifts their body.
        lean = Mathf.Lerp(lean, targetLean,
            1f - Mathf.Exp(-dt / Mathf.Max(0.01f, settings.leanSmoothing)));

        if (Speed < 0.2f) return;

        float radians = lean * Mathf.Deg2Rad;
        float yawRate = Mathf.Abs(settings.gravity) * Mathf.Tan(radians) / Speed;

        yawRate = Mathf.Clamp(yawRate * Mathf.Rad2Deg,
            -settings.maxYawRate, settings.maxYawRate);

        transform.Rotate(Vector3.up, yawRate * dt, Space.World);
    }

    // ------------------------------------------------------------------- itme

    void Accelerate(float dt)
    {
        float mass = Mathf.Max(1f, settings.mass);
        float crr = RollingResistance >= 0f ? RollingResistance : settings.rollingResistance;

        // THRUST. Power divided by speed, but at low speed that goes to infinity — 230 W divided
        // by zero when pulling away. The largest force the wheel can transfer to the ground is the limit.
        float power = input.sprint ? settings.sprintPower : settings.steadyPower;
        power *= input.throttle;

        float drive = Speed > 0.3f
            ? Mathf.Min(power / Speed, settings.maxDriveForce)
            : settings.maxDriveForce * input.throttle;

        // RESISTANCES. Rolling is independent of speed, drag goes with the square of it, gravity
        // with the gradient. All three are real forces slowing the bike; none of them is a
        // coefficient added "for gameplay".
        float rolling = crr * mass * Mathf.Abs(settings.gravity);
        float drag = 0.5f * settings.airDensity * settings.dragArea * Speed * Speed;

        // The gradient force: braking uphill, pushing downhill. sin(atan(gradient)) in closed form.
        float slope = mass * settings.gravity * Grade / Mathf.Sqrt(1f + Grade * Grade);

        // While airborne the pedals do nothing, and there is no rolling resistance either.
        if (!Grounded) { drive = 0f; rolling = 0f; }

        float force = drive + slope - rolling - drag;
        Speed += force / mass * dt;

        // BRAKING IS SEPARATE: not a force but a deceleration directly. The braking power is
        // limited by the wheel's locking point and does not scale with mass — on a bike the endo
        // limit comes from the geometry, not from the mass.
        if (Grounded && input.brake > 0f)
            Speed -= settings.brakeDeceleration * input.brake * dt;

        // No going backwards. The ceiling stands in for the braking reflex on a descent.
        Speed = Mathf.Clamp(Speed, 0f, settings.comfortMaxSpeed);
    }

    // ---------------------------------------------------------------- hareket

    void Move(float dt)
    {
        // The direction is laid onto the ground: going forward on a slope means going along the
        // slope, not horizontally forward. Otherwise the bike sinks into the ground on a steep slope.
        Vector3 along = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
        if (along.sqrMagnitude < 1e-6f) along = transform.forward;

        if (Grounded && verticalSpeed < 0f) verticalSpeed = -2f;
        else verticalSpeed += settings.gravity * dt;

        controller.Move((along * Speed + Vector3.up * verticalSpeed) * dt);
    }
}
