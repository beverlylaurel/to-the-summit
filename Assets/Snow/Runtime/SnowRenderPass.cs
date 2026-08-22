// ROL: kar sisteminin bütün çizim ve compute dispatch'lerini TEK CommandBuffer
// içinde, opak çizimden önce kuyruğa alır (spec §15.2).
// Çağıran: SnowRendererFeature.

using UnityEngine;
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
        public Matrix4x4 View;
        public Matrix4x4 Projection;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        SnowManager manager = SnowManager.Active;
        if (manager == null || !manager.IsReady) return;

        using var builder = renderGraph.AddUnsafePass<PassData>("Kar Simülasyonu", out PassData passData);

        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

        passData.Manager = manager;

        // KAMERA MATRİSLERİ GEÇİŞE TAŞINIYOR. Yakalama kendi ortografik
        // matrisini yazıyor; geri koyacak değeri buradan alıyor. GPU'ya
        // çevrilmemiş halleri veriliyor — `SetViewProjectionMatrices` çevirmeyi
        // kendi yapıyor.
        passData.View = cameraData.GetViewMatrix();
        passData.Projection = cameraData.GetProjectionMatrix();

        // Compute'lar RenderGraph'in tanımadığı kalıcı RT'lere yazıyor; grafın
        // kaynak takibi bu geçişi "kimse okumuyor" diye eleyebilir.
        builder.AllowPassCulling(false);

        builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
        {
            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
            data.Manager.Dispatch(cmd, data.View, data.Projection);
        });
    }
}
