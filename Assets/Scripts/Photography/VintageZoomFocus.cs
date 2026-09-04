using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Camera-scoped, transient lens defocus. Owns only a runtime volume, never the world grade.
internal sealed class VintageZoomFocus : IDisposable
{
    readonly Camera camera;
    readonly VintageDslrProfile settings;
    readonly GameObject volumeObject;
    readonly Volume volume;
    readonly VolumeProfile volumeProfile;
    readonly DepthOfField focus;
    float amount;

    internal VintageZoomFocus(Camera camera, VintageDslrProfile settings)
    {
        this.camera = camera;
        this.settings = settings;
        volumeObject = new GameObject("Vintage Zoom Focus (Runtime)")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        int mask = camera.GetUniversalAdditionalCameraData().volumeLayerMask.value;
        for (int layer = 0; layer < 32; layer++)
            if ((mask & (1 << layer)) != 0) { volumeObject.layer = layer; break; }
        volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1000f;
        volume.weight = 0f;
        // A referenced profile also keeps URP's build-time post-process stripping aware
        // that depth of field is used. Only the runtime clone is ever animated.
        volume.sharedProfile = settings.zoomFocusProfile;
        volumeProfile = volume.profile;
        volumeProfile.hideFlags = HideFlags.HideAndDontSave;
        volume.sharedProfile = volumeProfile;
        if (!volumeProfile.TryGet(out focus))
            throw new InvalidOperationException("Zoom focus profile requires DepthOfField.");
        focus.mode.Override(DepthOfFieldMode.Bokeh);
        // A small circle of confusion, including distant mountains. This is a lens
        // settling cue, not an autofocus simulation or a second exposure model.
        focus.focalLength.Override(24f);
        focus.aperture.Override(8f);
        focus.bladeCount.Override(7);
        RenderPipelineManager.beginCameraRendering += BeginCamera;
        RenderPipelineManager.endCameraRendering += EndCamera;
    }

    internal void Tick(float relativeZoomSpeed, float deltaTime)
    {
        float target = Mathf.Clamp01(relativeZoomSpeed / settings.zoomDefocusSpeed)
            * settings.zoomDefocusStrength;
        // Fast acquisition, then a finite recovery instead of an endless exponential tail.
        float seconds = target > amount ? 0.04f : settings.zoomFocusRecoverySeconds;
        amount = Mathf.MoveTowards(amount, target, deltaTime / Mathf.Max(0.01f, seconds));
        focus.focusDistance.value = 1f / Mathf.Max(amount, 0.001f);
    }

    internal void Reset()
    {
        amount = 0f;
        volume.weight = 0f;
    }

    void BeginCamera(ScriptableRenderContext context, Camera renderingCamera)
    {
        volume.weight = renderingCamera == camera && amount > 0.002f ? 1f : 0f;
    }

    void EndCamera(ScriptableRenderContext context, Camera renderingCamera)
    {
        volume.weight = 0f;
    }

    public void Dispose()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCamera;
        RenderPipelineManager.endCameraRendering -= EndCamera;
        if (volume != null) volume.weight = 0f;
        UnityEngine.Object.Destroy(volumeObject);
        UnityEngine.Object.Destroy(focus);
        UnityEngine.Object.Destroy(volumeProfile);
    }
}
