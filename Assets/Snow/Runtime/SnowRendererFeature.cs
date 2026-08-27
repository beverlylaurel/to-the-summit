// ROLE: hooks the snow simulation pass into the URP renderer.
// CALLED BY: the URP renderer asset (PC_Renderer).

using UnityEngine;
using UnityEngine.Rendering.Universal;

public class SnowRendererFeature : ScriptableRendererFeature
{
    SnowRenderPass simPass;

    public override void Create()
    {
        // BEFORE THE OPAQUE DRAW (spec §15.2): the ground mesh and the object cover will read the
        // state textures in the same frame; the simulation has to finish before them.
        simPass = new SnowRenderPass
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.camera.cameraType == CameraType.Preview) return;

        // NO SNOW, NO WORK AT ALL (spec §15.2). If the manager is disabled the pass is not
        // recorded; in summer the game has to behave as if the snow system did not exist.
        if (SnowManager.Active == null || !SnowManager.Active.IsReady) return;

        renderer.EnqueuePass(simPass);
    }
}
