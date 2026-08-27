// ROLE: queues all of the snow system's draws and compute dispatches in a SINGLE
// CommandBuffer, before the opaque draw (spec §15.2).
// CALLED BY: SnowRendererFeature.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// ONE PASS, ONE BUFFER. `Graphics.ExecuteCommandBuffer` is not called
/// separately: every call brings its own synchronization point and opens a gap
/// between the dispatches.
public class SnowRenderPass : ScriptableRenderPass
{
    class PassData
    {
        public SnowManager Manager;
        public Matrix4x4 View;
        public Matrix4x4 Projection;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        SnowManager manager = SnowManager.Active;
        if (manager == null || !manager.IsReady) return;

        using var builder = renderGraph.AddUnsafePass<PassData>("Snow Simulation", out PassData passData);

        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

        passData.Manager = manager;

        // THE CAMERA MATRICES ARE CARRIED INTO THE PASS. The capture writes its own
        // orthographic matrix; it takes the value to restore from here. They are given
        // unconverted to GPU form — `SetViewProjectionMatrices` does the conversion
        // itself.
        passData.View = cameraData.GetViewMatrix();
        passData.Projection = cameraData.GetProjectionMatrix();

        // The computes write to persistent RTs the RenderGraph does not know about; the graph's
        // resource tracking could cull this pass as "nobody reads it".
        builder.AllowPassCulling(false);

        builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
        {
            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
            data.Manager.Dispatch(cmd, data.View, data.Projection);
        });
    }
}
