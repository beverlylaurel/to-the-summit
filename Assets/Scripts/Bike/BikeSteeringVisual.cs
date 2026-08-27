using System;
using UnityEngine;

/// TURNS THE HANDLEBAR AND FORK. The controller rotates the whole bike; this component adds a
/// visual deviation ON TOP OF THAT — the handlebar turns and the body follows.
///
/// Why it is separate: physics and visuals have to stay apart. The controller works with no
/// model, and changing the model does not change the physics. And on a model where the fork
/// assembly is not a separate object this component is simply not added and the rest works unchanged.
///
/// THE ANGLE IS NOT DERIVED FROM PHYSICS, IT IS VISUAL. On a real bike at speed the handlebar
/// barely turns — a corner is taken by leaning and the bar moves a few degrees. Tying the turn
/// angle to the speed gives that for free: full angle at a standstill, a hair at speed.
public class BikeSteeringVisual : MonoBehaviour
{
    [SerializeField] BikeController bike;

    [Tooltip("The part that turns on the steering axis: fork, handlebar and head together. " +
             "If it is not a separate object in the model this component is not used.")]
    [SerializeField] Transform steeringAssembly;

    [Tooltip("Largest angle the handlebar can turn at a standstill (degrees). On a real " +
             "bike the bar turns 60-70 degrees, but that angle is never reached while " +
             "riding.")]
    [Range(5f, 70f)] [SerializeField] float maxAngle = 35f;

    [Tooltip("Above this speed the handlebar barely turns at all (m/s). Six metres per second " +
             "is about 22 km/h: at that pace a corner is taken by leaning and the bar moves a hair.")]
    [Range(1f, 15f)] [SerializeField] float fullLeanSpeed = 6f;

    [Tooltip("Smoothing of the turn (seconds). At zero the handlebar jumps.")]
    [Range(0.02f, 0.6f)] [SerializeField] float smoothing = 0.12f;

    /// The model's own zero pose: its local rotation at the moment the handlebar is straight.
    /// Built from zero the model would jump to its own axis at every start — the zero rotation
    /// of a mesh coming from the generator does not mean a straight handlebar.
    Quaternion rest;
    float angle;

    public void Bind(BikeController bikeRef, Transform assembly)
    {
        bike = bikeRef;
        steeringAssembly = assembly;
        if (assembly != null) rest = assembly.localRotation;
    }

    void OnEnable()
    {
        if (bike == null || steeringAssembly == null)
            throw new InvalidOperationException($"{nameof(BikeSteeringVisual)}: dependencies are not assigned.");

        rest = steeringAssembly.localRotation;
    }

    void LateUpdate()
    {
        // The lean angle is already the smoothed form of the steering input; rather than reading
        // a second input it is derived from that — with two sources the handlebar and the body
        // would turn at different moments.
        float steer = bike.LeanAngle / Mathf.Max(1f, MaxLeanOf(bike));

        // As the speed rises the handlebar is reined in: corners start being taken by leaning.
        float speedFade = 1f - Mathf.Clamp01(bike.Speed / Mathf.Max(0.1f, fullLeanSpeed));
        float target = steer * maxAngle * Mathf.Lerp(0.15f, 1f, speedFade);

        angle = Mathf.Lerp(angle, target,
            1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, smoothing)));

        steeringAssembly.localRotation = rest * Quaternion.Euler(0f, angle, 0f);
    }

    /// The largest lean angle in the controller's settings. It cannot be read directly because
    /// the settings are the controller's private field; this number is all the ratio needs.
    static float MaxLeanOf(BikeController bike) => bike.MaxLean;
}
