using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// Draws near-field precipitation after the volumetric cloud composite.
///
/// The cloud package composites at AfterRenderingTransparents so that its distant layer can
/// correctly cover the transparent sea. Rain is nearer than both and therefore needs one later
/// pass of its own; otherwise the full-screen cloud composite overwrites rain only where the
/// camera sees sky, producing a silhouette-shaped cutoff along mountains.
public class PrecipitationRenderFeature : ScriptableRendererFeature
{
    RainPass pass;

    public override void Create()
    {
        pass = new RainPass
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents + 1
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        CameraType type = renderingData.cameraData.cameraType;
        if (type == CameraType.Preview || type == CameraType.Reflection) return;
        if (PrecipitationRenderer.Active == null) return;

        renderer.EnqueuePass(pass);
    }

    sealed class RainPass : ScriptableRenderPass
    {
        sealed class PassData
        {
            public PrecipitationRenderer Rain;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            PrecipitationRenderer rain = PrecipitationRenderer.Active;
            if (rain == null) return;

            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            using var builder = renderGraph.AddRasterRenderPass<PassData>(
                "Precipitation After Clouds", out PassData passData);

            passData.Rain = rain;
            builder.SetRenderAttachment(resources.activeColorTexture, 0);
            builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            {
                data.Rain.DrawAfterClouds(context.cmd);
            });
        }
    }
}
