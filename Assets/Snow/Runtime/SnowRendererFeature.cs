// ROL: kar simülasyon geçişini URP renderer'ına takan özellik.
// Çağıran: URP renderer asset'i (PC_Renderer).

using UnityEngine.Rendering.Universal;

public class SnowRendererFeature : ScriptableRendererFeature
{
    SnowRenderPass simPass;
    SnowOcclusionCapture.Pass occlusionPass;

    public override void Create()
    {
        // OPAK ÇİZİMDEN ÖNCE (§11.1): zemin mesh'i ve nesne kaplaması durum dokusunu
        // aynı karede okuyacak, simülasyon onlardan önce bitmiş olmalı.
        simPass = new SnowRenderPass
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques
        };

        // ENGEL ÇİZİMİ OPAKTAN SONRA: URP kendi opak geçişinde aynı nesneleri kendi
        // malzemeleriyle çiziyor, bizim geçiş rengi tamamen üzerine yazıyor.
        occlusionPass = new SnowOcclusionCapture.Pass
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        UnityEngine.Camera camera = renderingData.cameraData.camera;
        if (camera.cameraType == UnityEngine.CameraType.Preview) return;

        if (SnowOcclusionCapture.IsCaptureCamera(camera))
        {
            renderer.EnqueuePass(occlusionPass);
            return;
        }

        if (SnowManager.Active == null) return;

        renderer.EnqueuePass(simPass);
    }
}
