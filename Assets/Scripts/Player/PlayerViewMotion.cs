using System;
using UnityEngine;

/// Adds restrained, vestibular-safe motion to the rendered camera only.
///
/// MouseLook owns yaw and pitch on the parent pivot. This component owns millimetre-scale
/// camera translation and sub-degree roll on the camera child, so the two systems never write
/// the same transform channels. Gait phase advances from travelled distance rather than a
/// timer, keeping the image and footsteps believable through acceleration and sprint blending.
[DisallowMultipleComponent]
public sealed class PlayerViewMotion : MonoBehaviour
{
    [SerializeField] FirstPersonController movement;
    [SerializeField] CharacterController body;
    [SerializeField] MouseLook look;
    [SerializeField] Transform view;
    [SerializeField] PlayerViewMotionSettings settings;

    Vector3 baseLocalPosition;
    Quaternion baseLocalRotation;
    float gaitPhase;
    float movementWeight;
    float movementWeightVelocity;
    float sprintWeight;
    float sprintWeightVelocity;
    float turnWeight;
    float turnWeightVelocity;
    float landingOffset;
    float landingVelocity;
    float previousVerticalSpeed;
    bool wasGrounded;

    public void Bind(FirstPersonController movementSource, CharacterController bodySource,
                     MouseLook lookSource, Transform renderedView,
                     PlayerViewMotionSettings sharedSettings)
    {
        movement = movementSource;
        body = bodySource;
        look = lookSource;
        view = renderedView;
        settings = sharedSettings;
        CaptureRestPose();
    }

    void OnEnable()
    {
        if (movement == null || body == null || look == null || view == null || settings == null)
        {
            // Editor bootstraps add the component before Bind can assign its references.
            if (Application.isPlaying)
                throw new InvalidOperationException($"{nameof(PlayerViewMotion)}: dependencies are not assigned.");
            return;
        }

        CaptureRestPose();
        wasGrounded = movement.OnGround;
        previousVerticalSpeed = body.velocity.y;
    }

    void OnDisable() => RestoreRestPose();

    void CaptureRestPose()
    {
        if (view == null) return;
        baseLocalPosition = view.localPosition;
        baseLocalRotation = view.localRotation;
    }

    void LateUpdate()
    {
        if (view == null || settings == null) return;

        // Riding owns the parent pivot and deliberately disables the walking controller.
        // Return the camera child to rest so the bike can turn the head without a second writer.
        if (movement == null || body == null || !movement.enabled || !body.enabled)
        {
            RestoreRestPose();
            movementWeight = sprintWeight = turnWeight = landingOffset = 0f;
            movementWeightVelocity = sprintWeightVelocity = turnWeightVelocity = landingVelocity = 0f;
            return;
        }

        bool acceptsMotion = movement.InputEnabled
                          && Cursor.lockState == CursorLockMode.Locked;
        float horizontalSpeed = acceptsMotion
            ? new Vector2(body.velocity.x, body.velocity.z).magnitude
            : 0f;
        bool grounded = acceptsMotion && movement.OnGround;
        bool sprinting = acceptsMotion && movement.IsSprinting;
        Vector2 lookDelta = acceptsMotion && look != null
            ? look.LastFrameDeltaDegrees
            : Vector2.zero;

        StepMotion(horizontalSpeed, grounded, sprinting, lookDelta,
            body != null ? body.velocity.y : 0f, Time.deltaTime);
    }

    // A single deterministic step keeps the camera response measurable in editor regression tests.
    void StepMotion(float horizontalSpeed, bool grounded, bool sprinting,
                    Vector2 lookDeltaDegrees, float verticalSpeed, float deltaTime)
    {
        float dt = Mathf.Max(0.0001f, deltaTime);
        float movementTarget = grounded
            ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(
                settings.movingThreshold, Mathf.Max(settings.movingThreshold + 0.01f,
                movement.walkSpeed * 0.65f), horizontalSpeed))
            : 0f;
        movementWeight = Mathf.SmoothDamp(movementWeight, movementTarget,
            ref movementWeightVelocity, settings.movementFadeSeconds,
            Mathf.Infinity, dt);
        sprintWeight = Mathf.SmoothDamp(sprintWeight, sprinting ? 1f : 0f,
            ref sprintWeightVelocity, settings.sprintBlendSeconds,
            Mathf.Infinity, dt);

        if (grounded && horizontalSpeed > settings.movingThreshold)
        {
            float stride = Mathf.Lerp(settings.walkStride, settings.sprintStride, sprintWeight);
            gaitPhase = Mathf.Repeat(gaitPhase
                + horizontalSpeed / Mathf.Max(0.1f, stride) * Mathf.PI * 2f * dt,
                Mathf.PI * 2f);
        }

        float turnVelocity = lookDeltaDegrees.x / dt;
        float turnTarget = Mathf.Clamp(turnVelocity / settings.fullTurnSpeedDegrees, -1f, 1f);
        float turnSmooth = Mathf.Abs(turnTarget) > Mathf.Abs(turnWeight)
            ? settings.turnResponseSeconds
            : settings.turnReturnSeconds;
        turnWeight = Mathf.SmoothDamp(turnWeight, turnTarget, ref turnWeightVelocity,
            turnSmooth, Mathf.Infinity, dt);

        if (grounded && !wasGrounded && previousVerticalSpeed < -settings.landingMinimumSpeed)
        {
            float severity = Mathf.InverseLerp(settings.landingMinimumSpeed, 8f,
                -previousVerticalSpeed);
            landingOffset = -settings.landingDip * severity;
            landingVelocity = 0f;
        }
        landingOffset = Mathf.SmoothDamp(landingOffset, 0f, ref landingVelocity,
            settings.landingReturnSeconds, Mathf.Infinity, dt);

        float verticalAmplitude = Mathf.Lerp(settings.walkVertical,
            settings.sprintVertical, sprintWeight);
        float lateralAmplitude = Mathf.Lerp(settings.walkLateral,
            settings.sprintLateral, sprintWeight);
        float gaitRollAmplitude = Mathf.Lerp(settings.walkRollDegrees,
            settings.sprintRollDegrees, sprintWeight);

        float lateral = Mathf.Sin(gaitPhase) * lateralAmplitude * movementWeight
                      - turnWeight * settings.turnLateral;
        float vertical = -Mathf.Cos(gaitPhase * 2f) * verticalAmplitude * movementWeight
                       + landingOffset;
        float roll = -Mathf.Sin(gaitPhase) * gaitRollAmplitude * movementWeight
                   - turnWeight * settings.turnRollDegrees;

        view.localPosition = baseLocalPosition + new Vector3(lateral, vertical, 0f);
        view.localRotation = baseLocalRotation * Quaternion.Euler(0f, 0f, roll);

        wasGrounded = grounded;
        previousVerticalSpeed = verticalSpeed;
    }

    void RestoreRestPose()
    {
        if (view == null) return;
        view.localPosition = baseLocalPosition;
        view.localRotation = baseLocalRotation;
    }
}
