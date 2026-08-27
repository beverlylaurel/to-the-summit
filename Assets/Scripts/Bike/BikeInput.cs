using UnityEngine;

/// THE INPUT GIVEN TO THE BIKE. The controller KNOWS NOTHING about the keyboard, the gamepad
/// or an AI; it only reads this struct.
///
/// Why it is separate: reading input is the one thing that changes from project to project.
/// Buried inside the controller, the controller would change with every new project too. This
/// way the same bike works under the player's hands, under an NPC, and in a replay.
public struct BikeInput
{
    /// Pedalling, 0-1. It does not mean full power: it is how hard the rider is pushing.
    public float throttle;

    /// Fren, 0-1.
    public float brake;

    /// Steering, -1 left, +1 right.
    public float steer;

    /// Whether sprint is held. The power becomes `sprintPower` instead of `steadyPower`.
    public bool sprint;

    public static BikeInput Coasting => default;

    /// Pulls the values into a safe range: the incoming value is not trusted, because the
    /// input source lives outside the controller and will be something else in another project.
    public BikeInput Sanitised() => new()
    {
        throttle = Mathf.Clamp01(throttle),
        brake = Mathf.Clamp01(brake),
        steer = Mathf.Clamp(steer, -1f, 1f),
        sprint = sprint
    };
}
