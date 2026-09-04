using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

/// Persistent low-resolution live view. The meter reads a small HDR luminance buffer
/// asynchronously; exposure is smoothed in stops and shared with the JPEG at shutter time.
internal sealed class VintagePhotoPreview : IDisposable
{
    readonly VintageDslrProfile profile;
    readonly Material material;
    readonly RenderTexture scene, processed, blur, display, meter;
    bool pendingMeter, disposed, metered;
    int generation;
    float nextMeterTime, targetEV, currentEV;
    internal RTHandle SceneHandle { get; }
    internal bool Enabled { get; set; }
    internal int SourceFrame { get; set; } = -1;
    internal bool Ready { get; private set; }
    internal Vector4 Crop { get; set; }
    internal Texture Image => display;
    internal float Exposure { get; private set; } = 1f;
    internal float Luminance { get; private set; }
    internal float Seed { get; private set; }

    internal VintagePhotoPreview(VintageDslrProfile profile, Shader shader)
    {
        this.profile = profile;
        material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        int width = profile.previewWidth / 6 * 6;
        int height = width * 2 / 3;
        scene = Target(width, height, GraphicsFormat.R16G16B16A16_SFloat, "Live Scene HDR");
        SceneHandle = RTHandles.Alloc(scene);
        processed = Target(width, height, GraphicsFormat.R8G8B8A8_SRGB, "Live Vintage Colour");
        blur = Target(width, height, GraphicsFormat.R8G8B8A8_SRGB, "Live Focus Horizontal");
        display = Target(width, height, GraphicsFormat.R8G8B8A8_SRGB, "Live Viewfinder");
        meter = Target(96, 64, GraphicsFormat.R32G32B32A32_SFloat, "Live Meter");
    }

    internal void Reset()
    {
        Enabled = Ready = metered = false;
        generation++;
        nextMeterTime = 0f;
    }

    internal void Render(float compensation, float defocus)
    {
        if (disposed || !Enabled || SourceFrame != Time.frameCount) return;
        RenderTexture previous = RenderTexture.active;
        try
        {
            if (!pendingMeter && Time.unscaledTime >= nextMeterTime)
            {
                Graphics.Blit(scene, meter, material, 1);
                pendingMeter = true;
                nextMeterTime = Time.unscaledTime + profile.meterIntervalSeconds;
                int requestGeneration = generation;
                PhotoMeteringMode metering = profile.metering;
                AsyncGPUReadback.Request(meter, 0, request =>
                {
                    pendingMeter = false;
                    if (disposed || requestGeneration != generation || request.hasError) return;
                    var pixels = request.GetData<Color>();
                    double sum = 0, weights = 0;
                    for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 96; x++)
                    {
                        float radius = new Vector2((x + 0.5f) / 48f - 1f,
                            (y + 0.5f) / 32f - 1f).magnitude;
                        float weight = metering switch
                        {
                            PhotoMeteringMode.Spot => radius < 0.13f ? 1f : 0f,
                            PhotoMeteringMode.CenterWeighted => Mathf.Lerp(0.12f, 1f, Mathf.Clamp01(1f - radius)),
                            _ => Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(1f - radius * 0.72f))
                        };
                        sum += pixels[y * 96 + x].r * weight;
                        weights += weight;
                    }
                    float logLuminance = (float)(sum / Math.Max(weights, 0.0001));
                    if (float.IsNaN(logLuminance) || float.IsInfinity(logLuminance)) return;
                    Luminance = Mathf.Pow(2f, logLuminance);
                    targetEV = Mathf.Clamp(Mathf.Log(profile.meteringGray, 2f) - logLuminance, -7f, 7f);
                    if (!metered) currentEV = targetEV;
                    metered = true;
                });
            }
            if (!metered) return;
            float seconds = targetEV < currentEV ? profile.adaptToBrightSeconds : profile.adaptToDarkSeconds;
            currentEV = Mathf.Lerp(currentEV, targetEV,
                1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(seconds, 0.01f)));
            Exposure = Mathf.Pow(2f, Mathf.Clamp(currentEV + compensation, -7f, 7f));
            Seed = Time.unscaledTime * 173.17f;
            VintagePhotoProcessing.Configure(material, profile, Exposure, Seed);
            Graphics.Blit(scene, processed, material, 0);
            if (defocus > 0.002f)
            {
                material.SetVector("_FocusStep", new Vector4(defocus * profile.zoomBlurPixels / 1296f, 0f, 0f, 0f));
                Graphics.Blit(processed, blur, material, 2);
                material.SetVector("_FocusStep", new Vector4(0f, defocus * profile.zoomBlurPixels / 864f, 0f, 0f));
                Graphics.Blit(blur, display, material, 2);
            }
            else Graphics.Blit(processed, display);
            Ready = true;
        }
        finally { RenderTexture.active = previous; }
    }

    static RenderTexture Target(int width, int height, GraphicsFormat format, string name)
    {
        var target = new RenderTexture(new RenderTextureDescriptor(width, height)
        {
            graphicsFormat = format, depthBufferBits = 0, msaaSamples = 1
        })
        {
            name = name, hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
        };
        target.Create();
        return target;
    }

    public void Dispose()
    {
        disposed = true;
        Enabled = false;
        SceneHandle.Release();
        // Complete a pending readback before destroying its native source buffer.
        if (pendingMeter) AsyncGPUReadback.WaitAllRequests();
        foreach (var texture in new[] { scene, processed, blur, display, meter })
        {
            texture.Release();
            UnityEngine.Object.Destroy(texture);
        }
        UnityEngine.Object.Destroy(material);
    }
}
