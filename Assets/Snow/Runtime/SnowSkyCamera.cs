// ROLE: draws the geometry blocking snowfall from above and produces RT_SkyVis.
// CALLED BY: SnowManager (from inside Dispatch, only while dirty).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// NOT EVERY FRAME (spec §12.1). The map is refreshed only when the region centre
/// moves more than 4 m or `MarkDirty()` is called. The silhouette of static geometry
/// does not change frame to frame; drawing it every frame would be a cost for nothing.
///
/// NOT A CAMERA COMPONENT, for the same reason as the capture camera: URP does not
/// support replacement shaders (measured) and the camera path requires a change to the
/// URP asset (spec §1.1 forbids it).
public sealed class SnowSkyCamera
{
    /// It looks down: the forward direction is −Y (spec §12.1).
    static readonly Quaternion LookDown = Quaternion.Euler(90f, 0f, 0f);

    static readonly Matrix4x4 FlipZ = Matrix4x4.Scale(new Vector3(1f, 1f, -1f));

    /// The camera stands this far above the region; it should see everything below it.
    const float Height = 300f;

    const float NearClip = 0.05f;
    const float FarClip = 1200f;

    readonly List<Renderer> occluders = new(128);

    Vector2 bakedCenter;
    bool dirty = true;

    /// The world XZ of the map's centre — the sampling reads this.
    public Vector2 Center => bakedCenter;

    public int OccluderCount => occluders.Count;

    public void MarkDirty() => dirty = true;

    /// Scans the obstacles in the scene once. The obstacles are static; searching every frame
    /// would be both an allocation and wasted work (spec §0.8).
    public void Rescan(int layer)
    {
        occluders.Clear();

        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            if (r.gameObject.layer == layer) occluders.Add(r);

        dirty = true;
    }

    /// Whether a refresh is needed. Yes if the region centre moved past the threshold.
    public bool NeedsRefresh(Vector2 areaCenter) =>
        dirty || (areaCenter - bakedCenter).sqrMagnitude >
                 SnowConstants.SkyMoveThreshold * SnowConstants.SkyMoveThreshold;

    public void Record(CommandBuffer cmd, RenderTexture target, RenderTexture depth,
                       Material skyMaterial, Vector2 areaCenter, float observerY,
                       Matrix4x4 restoreView, Matrix4x4 restoreProj)
    {
        bakedCenter = areaCenter;
        dirty = false;

        float half = SnowConstants.SkyAreaSize * 0.5f;

        var position = new Vector3(areaCenter.x, observerY + Height, areaCenter.y);

        Matrix4x4 view = FlipZ * Matrix4x4.TRS(position, LookDown, Vector3.one).inverse;
        Matrix4x4 proj = Matrix4x4.Ortho(-half, half, -half, half, NearClip, FarClip);

        cmd.SetRenderTarget(target, depth);

        // The background is −9999: "there is no cover here". The sampling looks at the
        // difference `occlY − posWS.y`; a very low value makes the difference negative and
        // the visibility stays 1.
        cmd.ClearRenderTarget(true, true, new Color(-9999f, 0f, 0f, 0f), 1f);

        cmd.SetViewProjectionMatrices(view, proj);

        var box = new Bounds(new Vector3(areaCenter.x, observerY, areaCenter.y),
                             new Vector3(SnowConstants.SkyAreaSize, FarClip * 2f,
                                         SnowConstants.SkyAreaSize));

        for (int i = 0; i < occluders.Count; i++)
        {
            Renderer r = occluders[i];
            if (r == null || !r.enabled) continue;
            if (!box.Intersects(r.bounds)) continue;

            cmd.DrawRenderer(r, skyMaterial, 0, 0);
        }

        cmd.SetViewProjectionMatrices(restoreView, restoreProj);
    }
}
