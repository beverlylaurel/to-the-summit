using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Couples the sun's optical lens effects to the same atmosphere that lights the scene.
/// Geometry occlusion is handled by SRP lens-flare depth sampling; clouds and the project's
/// custom height fog are attenuated here because URP cannot infer those media automatically.
/// </summary>
[ExecuteAlways]
public sealed class SunLensEffects : MonoBehaviour
{
    [SerializeField] TimeOfDay time;
    [SerializeField] AtmosphereController atmosphere;
    [SerializeField] Camera view;
    [SerializeField] LensFlareComponentSRP flare;

    [Header("Physical response")]
    [SerializeField, Range(0f, 2f)] float peakIntensity = 0.32f;
    [SerializeField, Min(1f)] float representativeFogPath = 900f;

    public float Visibility01 { get; private set; }

    public void Bind(TimeOfDay timeOfDay, AtmosphereController atmosphereController,
                     Camera camera, LensFlareComponentSRP flareComponent)
    {
        time = timeOfDay;
        atmosphere = atmosphereController;
        view = camera;
        flare = flareComponent;
        Apply();
    }

    void OnEnable() => Apply();
    void Update() => Apply();

    void Apply()
    {
        if (flare == null || time == null || view == null)
        {
            Visibility01 = 0f;
            return;
        }

        float aboveHorizon = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(-0.01f, 0.06f, time.SunDirection.y));
        float viewAlignment = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(Mathf.Cos(18f * Mathf.Deg2Rad),
                              Mathf.Cos(2f * Mathf.Deg2Rad),
                              Vector3.Dot(view.transform.forward, time.SunDirection)));

        float cloudTransmission = atmosphere != null
            ? Mathf.Exp(-3.2f * Mathf.Clamp01(atmosphere.Coverage))
            : 1f;
        float visibility = atmosphere != null ? Mathf.Max(1f, atmosphere.Visibility) : 30000f;
        float fogTransmission = Mathf.Exp(-representativeFogPath / visibility);

        // Direct solar energy is already atmospherically extinguished by TimeOfDay. Normalize
        // against the package's 100 klux calibration; no exposure value enters this computation.
        float directEnergy = Mathf.Clamp01(time.SunLightIntensity / 3.030782f);
        Visibility01 = aboveHorizon * viewAlignment * cloudTransmission
                     * fogTransmission * directEnergy;

        flare.intensity = peakIntensity * Visibility01;
    }
}
