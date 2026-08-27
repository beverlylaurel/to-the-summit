using System;
using UnityEngine;

/// GARG-NAYAR STREAK DATABASE — `[Garg 2006]`, `rain-spec.md` §5.
///
/// The look of a rain streak is a complicated function of light direction, view direction and
/// the drop's oscillation; it needs ray tracing. The paper's answer: render offline, store, look
/// up at runtime. This asset is the data side of that lookup.
///
/// THREE ANGULAR AXES plus an oscillation variant:
///   `v` — the light's VERTICAL angle relative to the drop's fall axis  (10 values)
///   `h` — the light's HORIZONTAL angle                                 (9 values)
///   `dcam` — the camera's deviation from vertical, `θ_v = 90° − dcam`  (5 values)
///   `osc` — oscillation variant, random per drop                      (10 values)
///
/// `dcam` GETS ITS OWN ARRAY AT EVERY LEVEL. The streak shortens with the camera angle
/// (measured, `size16`: 525/494/405/272/108 — the ratio is `cos(dcam)`), because if the view
/// direction is not perpendicular to the drop's fall direction the streak is shorter on screen.
/// Packing them all into one array meant 40% empty pixels.
///
/// TWO LIGHTINGS. `point` is a directional source (the sun), `ambient` an overcast sky. The paper
/// computes the two SEPARATELY and sums them (`§6.3.3`); they look different — ambient fills the
/// whole streak softly, a directional source leaves a thin sharp filament.
[CreateAssetMenu(fileName = "RainStreakDatabase", menuName = "To The Summit/Rain Streak Database")]
public class RainStreakDatabase : ScriptableObject
{
    /// All resolution levels for one camera angle.
    [Serializable]
    public class CameraAngle
    {
        [Tooltip("The camera's deviation from vertical (degrees). θ_v = 90° − this.")]
        public int Dcam;

        [Tooltip("Directional source arrays, in the same order as `Sizes`. The slice index " +
                 "((v * 9) + h) * 10 + osc.")]
        public Texture2DArray[] Point;

        [Tooltip("Ambient arrays, in the same order as `Sizes`. The slice index is osc.")]
        public Texture2DArray[] Ambient;

        [Tooltip("Presence table, 900 entries. A 0 means that (v,h,osc) is NOT in the " +
                 "database — at extreme vertical angles the streak degenerates and was " +
                 "not rendered. Interpolation skips that neighbour and renormalizes the weights.")]
        public byte[] Present;
    }

    [Tooltip("Streak widths (pixels), ascending. The one just above the drop's on-screen " +
             "streak width is chosen (`§6.3`).")]
    public int[] Sizes;

    [Tooltip("The light's vertical angle axis (degrees).")]
    public int[] Vertical;

    [Tooltip("The light's horizontal angle axis (degrees).")]
    public int[] Horizontal;

    [Tooltip("Arrays per camera angle, `dcam` ascending.")]
    public CameraAngle[] Angles;

    /// Slice index — the order in the array is fixed by the baker, no name lookup.
    public static int SliceIndex(int vIndex, int hIndex, int osc) =>
        (vIndex * 9 + hIndex) * 10 + osc;
}
