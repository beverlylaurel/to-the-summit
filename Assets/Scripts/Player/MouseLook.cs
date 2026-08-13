using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// Fare bakışı. Yaw gövdeye, pitch kamera pivotuna uygulanır.
/// Hareket sisteminden bağımsızdır: yürürken de serbest uçarken de aynı şekilde çalışır.
public class MouseLook : MonoBehaviour
{
    [SerializeField] Transform cameraPivot;

    [SerializeField] float sensitivity = 0.12f;
    [Range(60f, 89f)]
    [SerializeField] float pitchLimit = 88f;

    float pitch;

    public void Bind(Transform pivot) => cameraPivot = pivot;

    void OnEnable()
    {
        if (cameraPivot == null)
            throw new InvalidOperationException($"{nameof(MouseLook)}: {nameof(cameraPivot)} atanmadı.");

        pitch = cameraPivot.localEulerAngles.x;
        if (pitch > 180f) pitch -= 360f;
    }

    void Update()
    {
        // İmleç serbestken girdi oyuna değil arayüze ait
        if (Cursor.lockState != CursorLockMode.Locked) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 delta = mouse.delta.ReadValue() * sensitivity;

        transform.Rotate(Vector3.up, delta.x, Space.World);

        pitch = Mathf.Clamp(pitch - delta.y, -pitchLimit, pitchLimit);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
