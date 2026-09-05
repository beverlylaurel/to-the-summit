using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// Mouse look. Yaw is applied to the body, pitch to the camera pivot.
/// It is independent of the movement system: it works the same while walking and while free flying.
public class MouseLook : MonoBehaviour
{
    [SerializeField] Transform cameraPivot;

    [SerializeField] float sensitivity = 0.12f;
    [Range(60f, 89f)]
    [SerializeField] float pitchLimit = 88f;

    float pitch;

    /// Angular input consumed this frame, in degrees. Camera presentation can respond to the
    /// same physical turn without reading and interpreting raw device pixels a second time.
    public Vector2 LastFrameDeltaDegrees { get; private set; }

    /// Temporarily blocked by full-screen interactions such as reviewing a photograph.
    /// The component remains enabled so its pitch state is not reconstructed mid-frame.
    public bool InputEnabled { get; set; } = true;

    public void Bind(Transform pivot) => cameraPivot = pivot;

    void OnEnable()
    {
        if (cameraPivot == null)
            throw new InvalidOperationException($"{nameof(MouseLook)}: {nameof(cameraPivot)} is not assigned.");

        pitch = cameraPivot.localEulerAngles.x;
        if (pitch > 180f) pitch -= 360f;
    }

    void Update()
    {
        LastFrameDeltaDegrees = Vector2.zero;

        // While the cursor is free the input belongs to the UI, not the game
        if (!InputEnabled || Cursor.lockState != CursorLockMode.Locked) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 delta = mouse.delta.ReadValue() * sensitivity;
        LastFrameDeltaDegrees = delta;

        transform.Rotate(Vector3.up, delta.x, Space.World);

        pitch = Mathf.Clamp(pitch - delta.y, -pitchLimit, pitchLimit);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
