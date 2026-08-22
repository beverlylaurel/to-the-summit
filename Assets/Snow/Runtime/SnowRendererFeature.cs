// ROL: kar simülasyon geçişini URP renderer'ına takar.
// Çağıran: URP renderer asset'i (PC_Renderer).

using UnityEngine;
using UnityEngine.Rendering.Universal;

public class SnowRendererFeature : ScriptableRendererFeature
{
    SnowRenderPass simPass;

    public override void Create()
    {
        // OPAK ÇİZİMDEN ÖNCE (spec §15.2): zemin mesh'i ve nesne kaplaması durum
        // dokularını aynı karede okuyacak; simülasyon onlardan önce bitmeli.
        simPass = new SnowRenderPass
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.camera.cameraType == CameraType.Preview) return;

        // KAR YOKSA HİÇ İŞ YOK (spec §15.2). Yönetici devre dışıysa geçiş
        // kaydedilmiyor; yaz aylarında oyun kar sistemi yokmuş gibi davranmalı.
        if (SnowManager.Active == null || !SnowManager.Active.IsReady) return;

        renderer.EnqueuePass(simPass);
    }
}
