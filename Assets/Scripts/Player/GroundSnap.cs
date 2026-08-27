using System;
using UnityEngine;

/// Places the player on the ground and keeps them there.
///
/// The position stored in the scene becomes invalid every time the terrain is regenerated:
/// either in the air or below the ground. So the position is not taken on trust from the
/// scene but measured at every start. If the player falls below the ground they are placed again.
[RequireComponent(typeof(CharacterController))]
public class GroundSnap : MonoBehaviour
{
    [SerializeField] Terrain terrain;
    [Tooltip("Height the ray starts from (metres).")]
    [SerializeField] float probeHeight = 500f;
    [SerializeField] float clearance = 0.1f;
    [Tooltip("Falling this far below the ground triggers another placement.")]
    [SerializeField] float rescueDepth = 30f;

    CharacterController controller;

    public void Bind(Terrain target) => terrain = target;

    void Awake() => controller = GetComponent<CharacterController>();

    void Start()
    {
        if (terrain == null)
            throw new InvalidOperationException($"{nameof(GroundSnap)}: {nameof(terrain)} is not assigned.");

        Snap();
    }

    void Update()
    {
        // Rescue if they slipped under the terrain. A fall must not go on forever unnoticed.
        float floor = terrain.transform.position.y - rescueDepth;
        if (transform.position.y < floor) Snap();
    }

    void Snap()
    {
        Vector3 position = ClampInsideTerrain(transform.position);

        // Do not let their own capsule block the ray: it is measured with the controller
        // disabled. While enabled the ray hit the player's own collider first and the ground was never seen.
        controller.enabled = false;

        var origin = new Vector3(position.x, position.y + probeHeight, position.z);
        if (Physics.Raycast(origin, Vector3.down, out var hit, probeHeight * 4f,
                ~0, QueryTriggerInteraction.Ignore))
        {
            position.y = hit.point.y + clearance;
        }
        else
        {
            // If the ray missed, fall back to the height map
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y + clearance;
        }

        transform.position = position;
        controller.enabled = true;
    }

    /// A position outside the terrain finds no ground below it; it is pulled inside the bounds
    Vector3 ClampInsideTerrain(Vector3 position)
    {
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        const float edge = 20f;

        position.x = Mathf.Clamp(position.x, origin.x + edge, origin.x + size.x - edge);
        position.z = Mathf.Clamp(position.z, origin.z + edge, origin.z + size.z - edge);
        return position;
    }
}
