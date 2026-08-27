using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// PUTS THE PLAYER ON THE BIKE AND TAKES THEM OFF. Walking and riding are two separate
/// controllers; this component makes the transition between them without interfering with either.
/// The walking controller does not know about the bike, the bike does not know about the player.
///
/// THE HEAD IS INDEPENDENT WHILE RIDING. In reality a rider turns their body with the bike but
/// can turn their head aside without looking at the road — like looking over a shoulder. The
/// mouse only turns the head; A and D set the bike's direction. Left unbounded the rider would
/// look behind them like an owl.
///
/// THE CAMERA STAYS ON THE PLAYER. On mounting, the player becomes a child of the bike and the
/// camera comes along: had a separate riding camera been set up, both cameras' settings (field of
/// view, sway, effects) would have to be kept separately.
public class BikeRider : MonoBehaviour
{
    [Header("Oyuncu")]
    [SerializeField] FirstPersonController walker;
    [SerializeField] MouseLook look;
    [SerializeField] CharacterController body;
    [SerializeField] Transform cameraPivot;

    [Header("Bisiklet")]
    [SerializeField] BikeController bike;
    [SerializeField] BikePlayerInput bikeInput;

    [Tooltip("The saddle's place in bike space. The setup script measures it from the saddle " +
             "part and writes it; entered by hand it would become a lie once the model changed.")]
    [SerializeField] Vector3 seat = new Vector3(0f, 0.9f, -0.2f);

    [Header("Binme")]
    [Tooltip("The bike can be mounted from this distance (metres). Kept short: mounting from " +
             "far away looks like the player is being teleported.")]
    [Range(0.5f, 4f)] [SerializeField] float reach = 2.2f;

    [Tooltip("The seated rider's eye height above the saddle (metres). The saddle is at 0.91 m; " +
             "given 0.75 the eye rises to 1.66 m and the rider feels like they are standing. " +
             "For a seated adult the eye is 1.45-1.55 m off the ground.")]
    [Range(0.3f, 1.1f)] [SerializeField] float eyeAboveSeat = 0.58f;

    [Tooltip("How far ahead of the saddle the eye is (metres). The rider leans their body " +
             "towards the handlebar; sitting exactly above the saddle the bar would fall " +
             "outside the frame and the sense of riding a bike would be lost.")]
    [Range(0f, 0.5f)] [SerializeField] float eyeAhead = 0.12f;

    [Tooltip("The lateral distance the player is dropped at when dismounting (metres). Dropped " +
             "inside the bike, the collision flings them aside.")]
    [Range(0.4f, 2f)] [SerializeField] float dismountSide = 0.9f;

    [Header("Kafa")]
    [Tooltip("The angle the head can be turned independently of the body (degrees). Looking " +
             "over a shoulder goes to eighty degrees; more is not a neck but an owl.")]
    [Range(30f, 110f)] [SerializeField] float headYawLimit = 80f;

    [Tooltip("The up-down look limit while riding (degrees). Narrower than while walking: in " +
             "the saddle the body leans forward and the neck is not enough to look at the summit.")]
    [Range(30f, 85f)] [SerializeField] float headPitchLimit = 65f;

    [Tooltip("Mouse sensitivity. Kept the same as the walking look, otherwise the hand's " +
             "habit breaks on mounting the bike.")]
    [Range(0.02f, 0.5f)] [SerializeField] float sensitivity = 0.12f;

    float headYaw;
    float headPitch;

    public bool Riding { get; private set; }

    public void Bind(FirstPersonController walkerRef, MouseLook lookRef,
        CharacterController bodyRef, Transform pivot,
        BikeController bikeRef, BikePlayerInput inputRef, Vector3 seatLocal)
    {
        walker = walkerRef;
        look = lookRef;
        body = bodyRef;
        cameraPivot = pivot;
        bike = bikeRef;
        bikeInput = inputRef;
        seat = seatLocal;
    }

    void OnEnable()
    {
        if (walker == null || look == null || body == null || cameraPivot == null)
            throw new InvalidOperationException($"{nameof(BikeRider)}: the player dependencies are not assigned.");

        if (bike == null || bikeInput == null)
            throw new InvalidOperationException($"{nameof(BikeRider)}: the bike is not assigned.");

        // The bike is not ridden without a player: the input component is off until mounted,
        // otherwise the bike drives itself when W is pressed while the player is walking.
        bikeInput.enabled = false;
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (Cursor.lockState != CursorLockMode.Locked) return;

        if (keyboard.eKey.wasPressedThisFrame)
        {
            if (Riding) Dismount();
            else if (Near()) Mount();
        }

        if (Riding) Look();
    }

    bool Near() =>
        Vector3.Distance(transform.position, bike.transform.position) <= reach;

    // ------------------------------------------------------------------ binme

    void Mount()
    {
        Riding = true;

        walker.enabled = false;
        look.enabled = false;

        // The collision is switched off: left on, the player's capsule would push against the
        // bike's own collision and the two would fling each other.
        body.enabled = false;

        // THE EYE HEIGHT IS MEASURED FROM THE SUM OF THE CHAIN, not from the pivot's own local
        // position: the pivot is the child of an intermediate object and its local height did not
        // give the real eye height. The result was the rider standing two metres above the saddle
        // — as if standing on top of the bike.
        float eyeOffset = cameraPivot.position.y - transform.position.y;

        transform.SetParent(bike.transform, false);
        transform.localRotation = Quaternion.identity;
        transform.localPosition = seat
            + Vector3.up * (eyeAboveSeat - eyeOffset)
            + Vector3.forward * eyeAhead;

        headYaw = 0f;
        headPitch = cameraPivot.localEulerAngles.x;
        if (headPitch > 180f) headPitch -= 360f;

        bikeInput.enabled = true;
    }

    void Dismount()
    {
        Riding = false;
        bikeInput.enabled = false;

        transform.SetParent(null, true);

        // Dropped on the left side: the bike is parked leaning to the right and in reality you
        // do not dismount on that side either.
        Vector3 target = bike.transform.position - bike.transform.right * dismountSide;
        transform.position = Ground(target);

        // The look direction is preserved: the direction the head was facing passes to the body,
        // otherwise the view jumps sideways the moment the feet touch the ground.
        transform.rotation = Quaternion.Euler(0f, bike.transform.eulerAngles.y + headYaw, 0f);
        cameraPivot.localRotation = Quaternion.Euler(headPitch, 0f, 0f);

        body.enabled = true;
        walker.enabled = true;
        look.enabled = true;
    }

    /// The ground at the dismount point. If the bike is standing on a slope a ray is cast so the
    /// player is not left in the air or inside the ground.
    Vector3 Ground(Vector3 point)
    {
        var ray = new Ray(point + Vector3.up * 3f, Vector3.down);

        return Physics.Raycast(ray, out RaycastHit hit, 8f, ~0, QueryTriggerInteraction.Ignore)
            ? hit.point + Vector3.up * (body.height * 0.5f + body.skinWidth)
            : point;
    }

    // ------------------------------------------------------------------- kafa

    /// The head turns independently of the bike but is attached to the body: yaw is written to
    /// the player's own rotation, pitch to the camera pivot. Because the player is a child of the
    /// bike, the local yaw is directly "the head's angle relative to the bike".
    void Look()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 delta = mouse.delta.ReadValue() * sensitivity;

        headYaw = Mathf.Clamp(headYaw + delta.x, -headYawLimit, headYawLimit);
        headPitch = Mathf.Clamp(headPitch - delta.y, -headPitchLimit, headPitchLimit);

        transform.localRotation = Quaternion.Euler(0f, headYaw, 0f);
        cameraPivot.localRotation = Quaternion.Euler(headPitch, 0f, 0f);
    }
}
