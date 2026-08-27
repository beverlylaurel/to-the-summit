// ROLE: hands the snow state to the CPU side. It reads back a small region around the
// player without blocking (spec §19).
// CALLED BY: SnowFootstepAudio, SnowMovementModifier, SnowSprayController.

using UnityEngine;
using UnityEngine.Rendering;

/// The snow at a point as the game side sees it (spec §19).
public struct SnowSample
{
    /// The snow surface's height above the ground (m). The state AFTER a trail has been opened.
    public float Depth;

    /// The snow column BEFORE a trail is opened (m). It changes only with precipitation and
    /// settling, i.e. it is constant on the scale of seconds.
    ///
    /// `Depth + SinkDepth` was used instead and it included `trail.g` (the ridge). The ridge
    /// accumulates with `max` and its position is offset along the velocity direction; the
    /// body's settling height followed it, jumped by 4 cm and stepped the trail's depth.
    public float BaseHeight;

    /// The excavation at this point (m) — the sinking depth.
    public float SinkDepth;

    /// 0 = toz, 1 = buz gibi.
    public float Density01;

    public float Wetness;

    /// The strength of the surface crust (spec §18.3). `RT_Trail.B`.
    public float Crust;

    public bool Valid;
}

/// A NON-BLOCKING READ (spec §19). Calling `GetData` stalls the GPU and doubles the frame
/// time; `AsyncGPUReadback` gives the same data with two frames of lag. For the footstep
/// sound and the speed multiplier two frames make no difference.
[DisallowMultipleComponent]
public class SnowSampler : MonoBehaviour
{
    /// The edge of the window read, in texels (spec §19).
    const int Window = 64;

    /// One request every this many frames (spec §19).
    const int Interval = 4;

    [SerializeField] SnowManager manager;

    [Tooltip("Pencerenin merkezinde duracak hedef — genelde oyuncu.")]
    [SerializeField] Transform followTarget;

    readonly Color[] snowCache = new Color[Window * Window];
    readonly Color[] trailCache = new Color[Window * Window];

    Vector2Int windowOrigin;
    Vector2 cachedAreaCenter;
    float cachedTexelSize;
    float cachedAreaSize;
    int cachedResolution;

    bool snowReady;
    bool trailReady;
    bool snowPending;
    bool trailPending;

    int lastRequestFrame = -Interval;

    public bool HasData => snowReady && trailReady;

    void OnEnable()
    {
        if (manager == null)
            throw new System.InvalidOperationException($"{nameof(SnowSampler)}: {nameof(manager)} is not assigned.");
        if (followTarget == null)
            throw new System.InvalidOperationException($"{nameof(SnowSampler)}: the follow target is not assigned.");

        snowReady = false;
        trailReady = false;
        snowPending = false;
        trailPending = false;
    }

    void LateUpdate()
    {
        if (!manager.IsReady) return;
        if (snowPending || trailPending) return;
        if (Time.frameCount - lastRequestFrame < Interval) return;

        lastRequestFrame = Time.frameCount;

        SnowQualityData q = manager.Settings.QualityData;

        cachedResolution = q.Resolution;
        cachedAreaSize = q.AreaSize;
        cachedTexelSize = manager.TexelSize;
        cachedAreaCenter = manager.AreaCenter;

        windowOrigin = WindowOrigin(followTarget.position, cachedAreaCenter,
                                    cachedAreaSize, cachedResolution);

        snowPending = true;
        trailPending = true;

        AsyncGPUReadback.Request(manager.SnowTexture, 0,
            windowOrigin.x, Window, windowOrigin.y, Window, 0, 1,
            TextureFormat.RGBAFloat, OnSnowRead);

        AsyncGPUReadback.Request(manager.TrailTexture, 0,
            windowOrigin.x, Window, windowOrigin.y, Window, 0, 1,
            TextureFormat.RGBAFloat, OnTrailRead);
    }

    /// The window's bottom-left corner, in texels. It does not go outside the texture bounds.
    public static Vector2Int WindowOrigin(Vector3 worldPos, Vector2 areaCenter,
                                          float areaSize, int resolution)
    {
        Vector2 uv = (new Vector2(worldPos.x, worldPos.z) - areaCenter) / areaSize
                     + new Vector2(0.5f, 0.5f);

        int cx = Mathf.RoundToInt(uv.x * resolution) - Window / 2;
        int cy = Mathf.RoundToInt(uv.y * resolution) - Window / 2;

        return new Vector2Int(Mathf.Clamp(cx, 0, resolution - Window),
                              Mathf.Clamp(cy, 0, resolution - Window));
    }

    void OnSnowRead(AsyncGPUReadbackRequest request)
    {
        snowPending = false;
        if (request.hasError) return;

        request.GetData<Color>().CopyTo(snowCache);
        snowReady = true;
    }

    void OnTrailRead(AsyncGPUReadbackRequest request)
    {
        trailPending = false;
        if (request.hasError) return;

        request.GetData<Color>().CopyTo(trailCache);
        trailReady = true;
    }

    public bool TrySampleSnow(Vector3 worldPos, out SnowSample sample)
    {
        sample = default;

        if (!HasData) return false;

        Vector2 uv = (new Vector2(worldPos.x, worldPos.z) - cachedAreaCenter) / cachedAreaSize
                     + new Vector2(0.5f, 0.5f);

        int tx = Mathf.RoundToInt(uv.x * cachedResolution) - windowOrigin.x;
        int ty = Mathf.RoundToInt(uv.y * cachedResolution) - windowOrigin.y;

        // There is no data outside the window. Rather than inventing it we return invalid —
        // saying "there is no snow" would be wrong, "we do not know" is right.
        if (tx < 0 || tx >= Window || ty < 0 || ty >= Window) return false;

        Color s = snowCache[ty * Window + tx];
        Color t = trailCache[ty * Window + tx];

        sample = Decode(s, t);
        return true;
    }

    /// THE SNOW COLUMN OF THE TRAIL-FREE WORLD (m). INSTANTANEOUS — no readback.
    ///
    /// The trail body's settling height reads this. Had it read the texture sample
    /// (`BaseHeight`) a CLOSED LOOP would form: as the body presses the density rises,
    /// `baseHeight = SWE×1000/ρ` falls, the body sinks and excavates more.
    /// In between there is an async readback refreshing once every 30 frames, so the loop is
    /// delayed — the result is an oscillator.
    ///
    /// Measured: the body's local Y oscillates regularly between 10 mm and 30 mm while
    /// walking and the trail's width rises and falls with it from 21 texels to 13
    /// (`SYMPTOMS.md`).
    ///
    /// The world value changes only with precipitation and settling; nothing the body writes
    /// comes back here.
    public float WorldColumnHeight
    {
        get
        {
            if (manager == null || manager.WorldSwe < 0f) return 0f;

            float rho = Mathf.Lerp(SnowConstants.RhoMin, SnowConstants.RhoMax,
                                   Mathf.Clamp01(manager.WorldRhoN));

            return manager.WorldSwe * SnowConstants.RhoWater / Mathf.Max(rho, 1f);
        }
    }

    /// From the texture values to what the game side sees. A pure function:
    /// it can be tested without entering Play.
    public static SnowSample Decode(Color snow, Color trail)
    {
        float rho = Mathf.Lerp(SnowConstants.RhoMin, SnowConstants.RhoMax,
                               Mathf.Clamp01(snow.g));

        float baseHeight = snow.r * SnowConstants.RhoWater / Mathf.Max(rho, 1f);

        return new SnowSample
        {
            Depth = Mathf.Max(baseHeight - trail.r + trail.g, 0f),
            BaseHeight = baseHeight,
            SinkDepth = trail.r,
            Density01 = Mathf.Clamp01(snow.g),
            Wetness = Mathf.Clamp01(snow.b),
            Crust = Mathf.Clamp01(trail.b),
            Valid = true,
        };
    }
}
