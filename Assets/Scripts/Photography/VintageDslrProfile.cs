using UnityEngine;

public enum PhotoMeteringMode
{
    Evaluative,
    CenterWeighted,
    Spot
}

public enum PhotoWhiteBalance
{
    Daylight,
    Cloudy,
    Shade,
    Tungsten
}

/// The replaceable body/lens/film response of the in-game camera. Values describe a
/// mid-2000s APS-C DSLR rather than the player's eye or the game's display grade.
[CreateAssetMenu(menuName = "To The Summit/Photography/Vintage DSLR Profile")]
public sealed class VintageDslrProfile : ScriptableObject
{
    [Header("Capture")]
    [Min(320)] public int captureWidth = 1944;
    [Min(240)] public int captureHeight = 1296;
    [Min(640)] public int outputWidth = 3888;
    [Min(480)] public int outputHeight = 2592;
    [Range(0.8f, 1f)] public float viewfinderCoverage = 0.95f;
    [Min(1)] public int cardCapacity = 247;
    [Range(1, 100)] public int jpegQuality = 92;
    [Min(0.1f)] public float reviewSeconds = 2f;

    [Header("Viewfinder zoom")]
    [Range(1f, 10f)] public float maximumZoom = 4f;
    [Range(0.02f, 0.5f)] public float zoomStep = 0.12f;
    [Range(0.05f, 0.8f)] public float zoomSmoothSeconds = 0.18f;

    [Header("Exposure")]
    public PhotoMeteringMode metering = PhotoMeteringMode.Evaluative;
    [Range(0.05f, 0.5f)] public float meteringGray = 0.18f;
    [Range(-3f, 3f)] public float exposureCompensation;
    [Range(1.4f, 22f)] public float aperture = 8f;
    [Range(50, 3200)] public int iso = 200;
    [Min(0.00025f)] public float referenceShutterSeconds = 1f / 125f;

    [Header("Colour")]
    public PhotoWhiteBalance whiteBalance = PhotoWhiteBalance.Daylight;
    [Range(0f, 1f)] public float contrast = 0.35f;
    [Range(0f, 1f)] public float sharpen = 0.32f;

    [Header("Lens and sensor")]
    [Range(-0.08f, 0.08f)] public float barrelDistortion = -0.018f;
    [Range(0f, 0.006f)] public float lateralChromaticAberration = 0.00115f;
    [Range(0f, 1f)] public float vignetteStrength = 0.58f;
    [Range(0f, 1f)] public float grainStrength = 0.32f;
    [Range(0f, 1f)] public float purpleFringe = 0.22f;

    public Vector3 WhiteBalanceMultipliers => whiteBalance switch
    {
        PhotoWhiteBalance.Cloudy => new Vector3(1.075f, 1f, 0.91f),
        PhotoWhiteBalance.Shade => new Vector3(1.12f, 1f, 0.84f),
        PhotoWhiteBalance.Tungsten => new Vector3(0.72f, 1f, 1.42f),
        _ => new Vector3(1.035f, 1f, 0.965f)
    };

    void OnValidate()
    {
        captureWidth = Mathf.Max(320, captureWidth / 2 * 2);
        captureHeight = Mathf.Max(240, captureHeight / 2 * 2);
        outputWidth = Mathf.Max(640, outputWidth / 2 * 2);
        outputHeight = Mathf.Max(480, outputHeight / 2 * 2);
    }
}
