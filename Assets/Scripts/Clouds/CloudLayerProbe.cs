using UnityEngine;
using UnityEngine.Rendering;

/// THE SINGLE SOURCE OF THE CLOUD LAYER. The system drawing the clouds is a render feature;
/// consumers on the game side (precipitation cutoff, climb HUD) cannot ask it directly.
/// This component reads the same Volume settings and the same weather map and provides the elevations.
///
/// The contract: whatever data draws the sky is what is read here. No second approach is
/// built — if one were, a ceiling would appear where there is no cloud in the sky.
public class CloudLayerProbe : MonoBehaviour
{
    [Tooltip("The Volume carrying the cloud settings.")]
    [SerializeField] Volume cloudVolume;

    [Tooltip("The weather driver the ceiling is pushed to.")]
    [SerializeField] AltitudeWeatherDriver driver;

    [Tooltip("The point the ceiling is read at — the player.")]
    [SerializeField] Transform observer;

    static readonly int CloudBottomId = Shader.PropertyToID("_CloudBottom");
    static readonly int CloudTopId = Shader.PropertyToID("_CloudTop");

    VolumetricClouds clouds;
    Texture2D map;

    /// Base of the layer (metres). It does not vary by column.
    public float Bottom => clouds.bottomAltitude.value;

    /// The highest elevation the layer can reach (metres). The HUD writes it as the upper end
    /// of the range; for the top of a specific column use `TopAt`.
    public float MaxTop => clouds.bottomAltitude.value + clouds.altitudeRange.value;

    void OnEnable()
    {
        if (cloudVolume == null || driver == null || observer == null)
            throw new System.InvalidOperationException($"{nameof(CloudLayerProbe)}: dependencies are not assigned.");

        if (!cloudVolume.profile.TryGet(out clouds))
            throw new System.InvalidOperationException($"{nameof(CloudLayerProbe)}: profilde {nameof(VolumetricClouds)} yok.");

        map = clouds.cloudMap.value as Texture2D;
        if (map == null)
            throw new System.InvalidOperationException($"{nameof(CloudLayerProbe)}: the weather map is not assigned.");
    }

    void LateUpdate()
    {
        driver.CloudColumnTop = TopAt(observer.position);

        // LINK 8: shared globals. The lightning bolt (`LightningBolt.shader`) intersects the
        // flash with the cloud shell and reads the elevations from here. `AtmosphereController`
        // used to publish them — they were the elevations of the deleted cloud model and had
        // nothing to do with what was drawn in the sky. Because the shell is spherical the
        // layer's maximum is given rather than the column top.
        Shader.SetGlobalFloat(CloudBottomId, Bottom);
        Shader.SetGlobalFloat(CloudTopId, MaxTop);
    }

    /// The cloud top of that column (metres). The weather map's B channel carries the maximum
    /// cloud height (`w_h`, `[H18 p.11]`); the shader also cuts the density at exactly that elevation.
    ///
    /// With no cloud at all in the column it returns infinity: "no top" and "top on the ground"
    /// are not the same thing. The second would cut the precipitation everywhere.
    public float TopAt(Vector3 worldPosition)
    {
        Color sample = Sample(worldPosition);
        if (CoverageOf(sample) <= 0f) return float.PositiveInfinity;

        return clouds.bottomAltitude.value + clouds.altitudeRange.value * sample.b;
    }

    /// The coverage of that column [0,1]. Not how much of the sky is closed — the probability
    /// of cloud in that column.
    public float CoverageAt(Vector3 worldPosition) => CoverageOf(Sample(worldPosition));

    Color Sample(Vector3 worldPosition)
    {
        float size = clouds.cloudMapSize.value;
        return map.GetPixelBilinear(worldPosition.x / size, worldPosition.z / size);
    }

    /// EXACTLY the formula in the shader: `WM_c = max(w_c0, SAT(g_c - 0.5) x w_c1 x 2)`
    /// `[H18 p.11]`. With two formulas in two places the HUD would contradict the sky.
    float CoverageOf(Color sample) => Mathf.Max(sample.r,
        Mathf.Clamp01(clouds.cloudCoverage.value - 0.5f) * sample.g * 2f);

    public void Bind(Volume cloudVolumeRef, AltitudeWeatherDriver driverRef, Transform observerRef)
    {
        cloudVolume = cloudVolumeRef;
        driver = driverRef;
        observer = observerRef;
    }
}
