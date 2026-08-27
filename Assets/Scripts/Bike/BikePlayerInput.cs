using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// PASSES KEYBOARD INPUT TO THE BIKE. The reason it is a separate component: reading input is
/// the one piece that changes from project to project. Buried inside the controller, the
/// controller would change with every new project too.
///
/// If an AI is to ride the same bike this component is disabled and another one that makes its
/// own `SetInput` call takes its place. The controller is not touched.
[RequireComponent(typeof(BikeController))]
public class BikePlayerInput : MonoBehaviour
{
    [SerializeField] BikeController bike;

    [Tooltip("Whether the pedals turn automatically. While on, the tempo is kept without the " +
             "player holding the forward key; so long flats do not tire the hand.")]
    [SerializeField] bool autoPedal;

    void Reset() => bike = GetComponent<BikeController>();

    void OnEnable()
    {
        if (bike == null) bike = GetComponent<BikeController>();
        if (bike == null)
            throw new InvalidOperationException($"{nameof(BikePlayerInput)}: bisiklet yok.");
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // While the cursor is free the input belongs to the UI, not the game — the same rule
        // as the project's walking controller.
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            bike.SetInput(BikeInput.Coasting);
            return;
        }

        float steer = 0f;
        if (keyboard.aKey.isPressed) steer -= 1f;
        if (keyboard.dKey.isPressed) steer += 1f;

        float throttle = keyboard.wKey.isPressed || autoPedal ? 1f : 0f;
        float brake = keyboard.sKey.isPressed ? 1f : 0f;

        // The pedals do not turn while the brake is held: the two at once is not real and puts
        // the speed into an ambiguous equilibrium.
        if (brake > 0f) throttle = 0f;

        bike.SetInput(new BikeInput
        {
            throttle = throttle,
            brake = brake,
            steer = steer,
            sprint = keyboard.leftShiftKey.isPressed
        });
    }
}
