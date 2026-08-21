// ROL: kar simülasyonunun render geçişi. SnowManager'ın biriktirdiği işi TEK bir
// CommandBuffer'a kuyruklar (§11.1) ve opak çizimden önce koşturur.
// Çağıran: SnowRendererFeature.

using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class SnowRenderPass : ScriptableRenderPass
{
    class PassData
    {
        public SnowManager Manager;
        public SnowFrameWork Work;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        SnowManager manager = SnowManager.Active;
        if (manager == null) return;

        // İş TÜKETİLİYOR. Sahnede birden çok kamera varsa (oyun + sahne görünümü)
        // ikincisi boş döner; simülasyon karede bir kez koşar.
        if (!manager.BeginFrameWork(out SnowFrameWork work)) return;

        using var builder = renderGraph.AddUnsafePass<PassData>("Kar Simülasyonu", out PassData passData);

        passData.Manager = manager;
        passData.Work = work;

        // Geçişin çıktısı render graph'ın görmediği kalıcı RT'ler; kültürlenmesin.
        builder.AllowPassCulling(false);

        builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
            data.Manager.Dispatch(CommandBufferHelpers.GetNativeCommandBuffer(context.cmd), data.Work));
    }
}
