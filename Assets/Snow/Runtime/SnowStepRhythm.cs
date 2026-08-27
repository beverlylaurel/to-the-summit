// ROLE: produces the walk's foot phase and raises an event at the moment of a step.
// CALLED BY: the scene (on the player).

using System;
using UnityEngine;

/// THE STEP COMES FROM DISTANCE, NOT FROM TIME.
///
/// A fixed timer gives the wrong rhythm once the speed changes: walking slowly the feet
/// slide, running the step rate cannot keep up. The distance travelled is accumulated;
/// a step falls every `strideLength` metres. As the speed rises the steps grow more
/// frequent on their own.
///
/// IT DOES NOT DRIVE THE BODY, IT ONLY PRODUCES A PHASE.
///
/// It used to raise and lower the foot proxies with a half sine. Once we moved to a single
/// body that lost its meaning — there is no "foot in the air" — and it became harmful:
/// it overwrote the body's `localPosition.y` together with another component. With two
/// writers colliding the body height oscillated frame to frame and the groove depth turned
/// into saw teeth (measured: expected localY 0.27, actual 0.402 → 0.556).
///
/// The body's height no longer enters the trail AT ALL: the sinking depth is told by the
/// snow (`KDeform`, the bearing capacity).
///
/// THE SNOW SYSTEM DOES NOT KNOW THIS. The footprint, the sound and the dust puff
/// SUBSCRIBE to this event; nobody is called from here.
[DisallowMultipleComponent]
public class SnowStepRhythm : MonoBehaviour
{
    [Header("Kaynak")]
    [Tooltip("The body the speed is read from.")]
    [SerializeField] CharacterController body;

    [Header("Walking")]
    [Tooltip("The step frequency at the standing limit (cycles/second).")]
    [SerializeField, Min(0.1f)] float baseFrequency = 0.75f;

    [Tooltip("The speed's contribution to the frequency (cycles/second per m/s).")]
    [SerializeField, Min(0f)] float frequencyPerSpeed = 0.25f;

    [Tooltip("The lower bound of the stride length (m). Walking very slowly the steps " +
             "must not shorten without bound.")]
    [SerializeField, Min(0.05f)] float minStride = 0.55f;

    [Tooltip("Below this speed it does not count as walking; the feet stay on the ground.")]
    [SerializeField] float minSpeed = 0.15f;

    /// Raised when a step falls. 0 = left, 1 = right.
    public event Action<int> Stepped;

    /// Diagnostic: where we are in the step cycle (0..1).
    public float Phase01 { get; private set; }

    /// Diagnostic: which foot is on the ground right now (0 = left, 1 = right).
    public int PlantedFoot { get; private set; }

    /// The total number of steps taken. The sound and the dust puff subscribe to it.
    public int StepCount { get; private set; }

    /// Diagnostic: horizontal speed (m/s).
    public float Speed { get; private set; }

    float travelled;

    void LateUpdate()
    {
        if (body == null) return;

        Vector3 v = body.velocity;
        Speed = new Vector2(v.x, v.z).magnitude;

        if (Speed > minSpeed)
        {
            travelled += Speed * Time.deltaTime;

            // THE STRIDE LENGTH DERIVES FROM THE SPEED, IT IS NOT FIXED.
            //
            // A fixed 0.78 m was written and, whatever the speed, half a stride came
            // down at 39 cm. What is constant in human walking is not the length but the
            // FREQUENCY: the leg swings like a pendulum, and to go faster the stride first
            // lengthens and then quickens.
            //
            // At 2.2 m/s a real stride is ~1.1 m; with a fixed 0.78 the marks fall 39 cm
            // apart while a footprint's total length (a 30 cm boot plus the shoulder and
            // tail at both ends) is 62 cm — the marks overlapped (the user reported it:
            // "the steps are too close together, there is no gap between them").
            // THE FREQUENCY RISES WITH THE SPEED TOO, NOT ONLY THE LENGTH.
            //
            // At a fixed frequency the stride comes out 2.3 m at 2.2 m/s — absurd. In real
            // walking the speeding up goes to BOTH: the stride lengthens and quickens.
            // At 1.4 m/s the cycle is 1.1 Hz and the stride 1.3 m; at 2.2 m/s
            // 1.3 Hz and 1.7 m.
            float frekans = Mathf.Max(0.1f, baseFrequency + frequencyPerSpeed * Speed);
            float stride = Mathf.Max(minStride, Speed / frekans);
            float half = Mathf.Max(0.05f, stride * 0.5f);

            while (travelled >= half)
            {
                travelled -= half;
                PlantedFoot = 1 - PlantedFoot;
                StepCount++;
                Stepped?.Invoke(PlantedFoot);
            }

            Phase01 = travelled / half;
        }
        else
        {
            // THE PHASE RESETS ON STOPPING; a new walk starts from the beginning of a step.
            travelled = 0f;
            Phase01 = 0f;
        }
    }
}
