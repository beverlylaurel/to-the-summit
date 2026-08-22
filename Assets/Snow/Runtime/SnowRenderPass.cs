// ROL: kar sisteminin bütün compute dispatch'lerini TEK CommandBuffer içinde,
// opak çizimden önce kuyruğa alır (spec §15.2).
// Çağıran: SnowRendererFeature.

using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// TEK GEÇİŞ, TEK TAMPON. `Graphics.ExecuteCommandBuffer` ayrı ayrı
/// çağrılmıyor: her çağrı kendi senkronizasyon noktasını getiriyor ve
/// dispatch'lerin arasına boşluk açıyor.
public class SnowRenderPass : ScriptableRenderPass
{
    class PassData
    {
        public SnowManager Manager;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        SnowManager manager = SnowManager.Active;
        if (manager == null || !manager.IsReady) return;

        using var builder = renderGraph.AddUnsafePass<PassData>("Kar Simülasyonu", out PassData passData);

        passData.Manager = manager;

        // Compute'lar RenderGraph'in tanımadığı kalıcı RT'lere yazıyor; grafın
        // kaynak takibi bu geçişi "kimse okumuyor" diye eleyebilir.
        builder.AllowPassCulling(false);

        builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
        {
            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
            data.Manager.Dispatch(cmd);
        });
    }
}
