using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// Copies scene-linear colour after atmosphere and precipitation, before gameplay grading.
/// The photo mode explicitly registers its camera and buffers; other cameras pay no cost.
public sealed class VintagePhotoPreviewFeature : ScriptableRendererFeature
{
    Camera camera;
    VintagePhotoPreview preview;
    PreviewPass pass;

    internal void Register(Camera camera, VintagePhotoPreview preview)
    {
        this.camera = camera;
        this.preview = preview;
    }

    internal void Unregister(VintagePhotoPreview owner)
    {
        if (preview != owner) return;
        camera = null;
        preview = null;
    }

    public override void Create() => pass = new PreviewPass
    {
        renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
    };

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        if (preview == null || !preview.Enabled || data.cameraData.camera != camera) return;
        pass.Preview = preview;
        pass.ConfigureInput(ScriptableRenderPassInput.Color);
        renderer.EnqueuePass(pass);
    }

    sealed class PreviewPass : ScriptableRenderPass
    {
        internal VintagePhotoPreview Preview;
        sealed class PassData
        {
            internal TextureHandle Source;
            internal Vector4 Crop;
            internal VintagePhotoPreview Preview;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
        {
            var resources = frame.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) return;
            using var builder = graph.AddRasterRenderPass<PassData>("Vintage Live HDR", out var data);
            data.Source = resources.activeColorTexture;
            data.Crop = Preview.Crop;
            data.Preview = Preview;
            builder.UseTexture(data.Source, AccessFlags.Read);
            builder.SetRenderAttachment(graph.ImportTexture(Preview.SceneHandle), 0);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
            {
                Blitter.BlitTexture(context.cmd, d.Source, d.Crop, 0, true);
                d.Preview.SourceFrame = Time.frameCount;
            });
        }
    }
}
