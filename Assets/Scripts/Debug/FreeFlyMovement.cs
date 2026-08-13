using UnityEngine;
using UnityEngine.InputSystem;

/// Test amaçlı serbest uçuş. Yerçekimi ve çarpışma yok.
/// Açıkken FirstPersonController kapatılır; ikisi aynı CharacterController'ı kullanır.
[RequireComponent(typeof(CharacterController))]
public class FreeFlyMovement : MonoBehaviour
{
    [SerializeField] Transform cameraPivot;
    [SerializeField] float baseSpeed = 20f;
    [SerializeField] float boostMultiplier = 6f;

    public float SpeedMultiplier { get; set; } = 1f;

    CharacterController controller;

    public void Bind(Transform pivot) => cameraPivot = pivot;

    void OnEnable()
    {
        controller = GetComponent<CharacterController>();
        controller.detectCollisions = false;
        controller.enabled = false;
    }

    void OnDisable()
    {
        if (controller == null) return;

        controller.detectCollisions = true;
        controller.enabled = true;
    }

    void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        var direction = Vector3.zero;
        if (keyboard.wKey.isPressed) direction += cameraPivot.forward;
        if (keyboard.sKey.isPressed) direction -= cameraPivot.forward;
        if (keyboard.dKey.isPressed) direction += transform.right;
        if (keyboard.aKey.isPressed) direction -= transform.right;
        if (keyboard.eKey.isPressed) direction += Vector3.up;
        if (keyboard.qKey.isPressed) direction -= Vector3.up;

        float speed = baseSpeed * SpeedMultiplier;
        if (keyboard.leftShiftKey.isPressed) speed *= boostMultiplier;

        transform.position += direction.normalized * (speed * Time.deltaTime);
    }
}
