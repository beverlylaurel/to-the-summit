using System;
using System.Collections.Generic;
using UnityEngine;

/// ROUTE DATA. Where the player starts, which lines lead up the mountain, where the camps are
/// pitched along the way — all of it is marked by hand (`RoutePainter`) and stored here.
///
/// POSITIONS ARE NORMALIZED (0-1), NOT world coordinates. When the terrain is regenerated or the
/// mountain is rescaled, world coordinates lose their meaning: the marks end up either in the air
/// or under the ground. Normalized XZ lives relative to the terrain bounds, and the elevation is
/// read from the ground at the point of use (see `SCALE.md`).
///
/// ELEVATION IS NOT STORED. Stored, it would go stale on every terrain edit and there would be no
/// way to notice the staleness.
[CreateAssetMenu(menuName = "To The Summit/Rota", fileName = "MountainRoute")]
public class MountainRoute : ScriptableObject
{
    /// A route point. The radius is the brush's thickness: the corridor's width and the area a
    /// camp flattens come from it — no separate number is needed.
    ///
    /// RADIUS = HALF THE ROAD'S OWN WIDTH. The shoulder allowance, the transition band and the
    /// distance over which the flattening blends into the slope DO NOT GO HERE — those are
    /// settings of the terrain shaping and are applied there as multipliers.
    ///
    /// Squeezing "road + shoulder + transition" into a single number was tried and abandoned:
    /// when the shoulder later needs widening it becomes unreadable which allowance changes, and
    /// the number loses its physical meaning. If it says 3.2 here, the road is 6.4 metres.
    [Serializable]
    public struct Mark
    {
        [Tooltip("Normalized position on the terrain (0-1).")]
        public Vector2 position;
        [Tooltip("Half width of the road (metres). The shoulder allowance is not included.")]
        public float radius;
    }

    /// One climbing line. The three lines are stored independently, not as a tree: branching
    /// already happens while drawing, and forcing a tree onto the data structure would mean
    /// rewriting everything the moment a line needed splitting or merging.
    [Serializable]
    public class Branch
    {
        public string name = "Hat";
        public List<Mark> marks = new();
    }

    [Header("Start")]
    [Tooltip("Where the player spawns, normalized (0-1).")]
    public Vector2 spawn = new(0.5f, 0.5f);

    [Tooltip("The facing direction at spawn (degrees, counter-clockwise from +X). Marked so " +
             "the game starts with the mountain in view.")]
    public float spawnYaw;

    [Tooltip("Whether the spawn is marked. If it is not, setup falls back to the old behaviour: " +
             "a point computed outside the foot of the mountain.")]
    public bool spawnSet;

    [Header("Yol")]
    [Tooltip("The road the bus arrives on and returns along. A single line: out and back are the same. "
             + "The radius is the road's width — if a vehicle passes it has to be wider than a path.")]
    public List<Mark> road = new();

    [Header("Hatlar")]
    public List<Branch> branches = new();

    [Header("Marks")]
    [Tooltip("Where camps are pitched. The radius gives the area to flatten.")]
    public List<Mark> camps = new();

    [Tooltip("Where supplies are bought. The last shopping point before the climb, and those along the way.")]
    public List<Mark> shops = new();

    /// Converts a normalized position to world XZ. The elevation is the CALLER's job: whether it
    /// is read from the ground collision or from the heightmap depends on the use, and the two
    /// differ by a few centimetres.
    public static Vector3 ToWorld(Vector2 normalized, Terrain terrain)
    {
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;

        return new Vector3(origin.x + normalized.x * size.x, origin.y,
                           origin.z + normalized.y * size.z);
    }

    /// Converts world XZ to a normalized position.
    public static Vector2 ToNormalized(Vector3 world, Terrain terrain)
    {
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;

        return new Vector2((world.x - origin.x) / Mathf.Max(1f, size.x),
                           (world.z - origin.z) / Mathf.Max(1f, size.z));
    }
}
