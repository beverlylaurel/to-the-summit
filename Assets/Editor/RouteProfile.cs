using System.Collections.Generic;
using UnityEngine;

/// ROUTE PROFILING. Length, ascent, and gradient of a drawn route path.
///
/// Why needed: the brush drew on terrain but did not display gradient. A path looking reasonable
/// horizontally could be a vertical wall in cross-section, only noticed when walking in-game.
/// Without real-time measurement during drawing, paths are drawn blindly.
///
/// Elevation is read from TERRAIN, not stored — route data functions this way by design.
public static class RouteProfile
{
    public struct Reading
    {
        public float length;      // meters, along slope
        public float ascent;      // total ascent, meters
        public float descent;     // total descent, meters
        public float maxGrade;    // steepest segment gradient, ratio (0.25 = 25%)
        public float steepLength; // total length of segments exceeding threshold, meters
    }

    /// Comfortable walking grade limit. 25% ≈ 14 degrees: upper limit for loaded hiking.
    /// Above this is not climbing, but breaks cadence and inflates route time.
    public const float FootGrade = 0.25f;

    /// BICYCLE LIMIT. The approach is ridden by bike, and bikes cannot climb foot gradients:
    /// 5-8% comfortable, 10-12% loaded limit, above 15% requires dismounting and pushing.
    /// Approach route grading is calibrated to this threshold.
    public const float BikeGrade = 0.12f;

    /// Vehicle limit. 10% ≈ 5.7 degrees: practical upper limit for mountain dirt roads.
    /// Buses cannot climb beyond this.
    public const float RoadGrade = 0.10f;

    public static Reading Measure(Terrain terrain, List<MountainRoute.Mark> marks,
        float steepThreshold)
    {
        var reading = new Reading();
        if (marks == null || marks.Count < 2) return reading;

        Vector3 previous = Ground(terrain, marks[0].position);

        for (int i = 1; i < marks.Count; i++)
        {
            Vector3 current = Ground(terrain, marks[i].position);

            float run = Vector2.Distance(new Vector2(previous.x, previous.z),
                                         new Vector2(current.x, current.z));
            float rise = current.y - previous.y;

            reading.length += Mathf.Sqrt(run * run + rise * rise);

            if (rise > 0f) reading.ascent += rise;
            else reading.descent -= rise;

            // Divide by zero guard horizontally: two overlapping points produce no grade.
            if (run > 0.5f)
            {
                float grade = Mathf.Abs(rise) / run;
                reading.maxGrade = Mathf.Max(reading.maxGrade, grade);
                if (grade > steepThreshold) reading.steepLength += run;
            }

            previous = current;
        }

        return reading;
    }

    public static Vector3 Ground(Terrain terrain, Vector2 normalized)
    {
        Vector3 world = MountainRoute.ToWorld(normalized, terrain);
        world.y = terrain.SampleHeight(world) + terrain.transform.position.y;
        return world;
    }
}
