using UnityEngine;

/// BIKE SETTINGS. Every number is here; the code keeps no value embedded. The same controller
/// with a different settings asset becomes a different bike — a loaded touring bike, a light city
/// bike, a child's bike.
///
/// THE NUMBERS ARE PHYSICAL. There is no speed table: the rider's power, the vehicle's mass, the
/// tyre's rolling resistance and the air's drag are given, and the speed COMES OUT of them. With
/// a table, every new gradient would need a new row and it would become a lie once the terrain changed.
///
/// Equilibrium speed: P = v * (Crr*m*g + m*g*sin(gradient) + 0.5*rho*CdA*v^2)
///
/// THE DEFAULTS ARE AN OLD CITY BIKE. The steel frame is heavy, the smooth tyre does not hold on
/// gravel, the upright posture eats wind. The measured result: 20 km/h on a dirt road, 15 on a
/// path, 6.4 on a 10% climb — that is, slower than walking uphill and two and a half times faster
/// on the flat. For a mountain bike, set the mass to 95, the resistance to 0.022, CdA to 0.65 and
/// the lean to 30.
[CreateAssetMenu(menuName = "To The Summit/Bike Settings", fileName = "BikeSettings")]
public class BikeSettings : ScriptableObject
{
    [Header("Mass and power")]
    [Tooltip("The total mass of rider, bike and load (kg). An old steel-framed city bike is "
             + "16-18 kg; with the rider and touring load, 100 kg.")]
    [Range(40f, 160f)] public float mass = 100f;

    [Tooltip("Sustained pedalling power (watts). A trained rider can hold 200-250 W for " +
             "hours; 400 W lasts only minutes.")]
    [Range(60f, 500f)] public float steadyPower = 230f;

    [Tooltip("Short sprint power (watts). It will be limited by the stamina system when that " +
             "arrives; unbounded for now.")]
    [Range(100f, 900f)] public float sprintPower = 400f;

    [Tooltip("The largest thrust the wheel can transfer to the ground (newtons). The power " +
             "formula goes to infinity at low speed: 230 W divided by zero speed when pulling " +
             "away from a standstill. Without the limit the bike shot off. 400 N is the order a " +
             "knobbly tyre can hold on dirt.")]
    [Range(50f, 1200f)] public float maxDriveForce = 400f;

    [Header("Resistance")]
    [Tooltip("Rolling resistance coefficient. Asphalt 0.005, dirt road 0.022, " +
             "gravel and path 0.035, loose sand 0.06. With a ground system it can be " +
             "changed at runtime.")]
    [Range(0.003f, 0.12f)] public float rollingResistance = 0.03f;

    [Tooltip("Drag area CdA (m²). 0.6-0.7 upright with a load on the back; 0.3 in a " +
             "racing posture.")]
    [Range(0.15f, 1.2f)] public float dragArea = 0.72f;

    [Tooltip("Air density (kg/m³). 1.225 at sea level; it falls with altitude and the " +
             "drag decreases. Leaving it constant is close enough.")]
    [Range(0.4f, 1.4f)] public float airDensity = 1.2f;

    [Header("Fren")]
    [Tooltip("Deceleration under full braking (m/s²). 4-6 m/s² on a bike; more is an endo.")]
    [Range(1f, 12f)] public float brakeDeceleration = 5f;

    [Tooltip("The top speed reached on a descent when freewheeling (m/s). The physics gives " +
             "64 km/h on a 10% descent — real but not playable, and the rider would brake " +
             "anyway. This ceiling stands in for that reflex.")]
    [Range(4f, 25f)] public float comfortMaxSpeed = 10f;

    [Header("Direksiyon")]
    [Tooltip("The largest lean angle (degrees). The corner radius follows from it: " +
             "r = v² / (g·tan(lean)). Thirty degrees is the limit that holds on dirt.")]
    [Range(10f, 45f)] public float maxLean = 25f;

    [Tooltip("The turn rate ceiling at low speed (degrees/second). The physics would let a " +
             "nearly stationary bike spin on the spot; in reality the bar angle is limited.")]
    [Range(30f, 360f)] public float maxYawRate = 120f;

    [Tooltip("Visual smoothing of the lean (seconds). At zero the bike leans instantly in a " +
             "corner and looks like a toy.")]
    [Range(0.02f, 1f)] public float leanSmoothing = 0.25f;

    [Header("Zemin")]
    [Tooltip("The layers counted as ground. The ray is cast at these; the player's own " +
             "collision MUST NOT be on this layer.")]
    public LayerMask groundLayers = ~0;

    [Tooltip("Gravity (m/s²). Used while airborne and when settling onto the ground.")]
    public float gravity = -9.81f;

    [Tooltip("Wheel radius (metres). The rotation rate follows from it: ω = v / r. " +
             "A 29 inch wheel with a knobbly tyre is about 0.37 m.")]
    [Range(0.15f, 0.6f)] public float wheelRadius = 0.37f;
}
