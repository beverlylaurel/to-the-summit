using System;
using UnityEngine;

/// TURNS THE WHEELS. Separate from the controller, because physics and visuals have to stay
/// apart: the bike works with no model, and changing the model does not change the physics.
///
/// The rotation rate derives FROM THE ROAD SPEED, not from a fixed multiplier: omega = v / r.
/// The wheel radius lives in the settings asset, so a bike with 26 inch wheels turns faster at
/// the same speed — correct by construction.
///
/// VISIBLE IN CO-OP. A distant player's wheel turns too; because the rotation rate derives only
/// from that player's position there is nothing extra to send over the network.
public class BikeWheels : MonoBehaviour
{
    [SerializeField] BikeController bike;
    [SerializeField] BikeSettings settings;

    [Tooltip("Front wheel. It turns on its own axis and is not steered separately — " +
             "the handlebar angle is already in the bike's own rotation.")]
    [SerializeField] Transform frontWheel;
    [SerializeField] Transform rearWheel;

    [Tooltip("Local axis the wheel turns about. In the model coming from Meshy the axis can " +
             "be X or Z; making it adjustable is cheaper than re-exporting the model.")]
    [SerializeField] Vector3 spinAxis = Vector3.right;

    float angle;

    public void Bind(BikeController bikeRef, BikeSettings settingsRef,
        Transform front, Transform rear, Vector3 axis)
    {
        bike = bikeRef;
        settings = settingsRef;
        frontWheel = front;
        rearWheel = rear;
        spinAxis = axis;
    }

    void OnEnable()
    {
        if (bike == null || settings == null)
            throw new InvalidOperationException($"{nameof(BikeWheels)}: dependencies are not assigned.");
    }

    void LateUpdate()
    {
        float radius = Mathf.Max(0.05f, settings.wheelRadius);

        // The angle is ACCUMULATED rather than rebuilt from zero every frame: the `Rotate` call
        // accumulates floating point error and over a long ride the wheel drifts off its axis.
        angle += bike.Speed / radius * Mathf.Rad2Deg * Time.deltaTime;
        angle %= 360f;

        Quaternion spin = Quaternion.AngleAxis(angle, spinAxis.normalized);

        if (frontWheel != null) frontWheel.localRotation = spin;
        if (rearWheel != null) rearWheel.localRotation = spin;
    }
}
