using UnityEngine;

internal static class VintagePhotoProcessing
{
    internal static void Configure(Material material, VintageDslrProfile profile, float exposure, float seed)
    {
        material.SetFloat("_Exposure", exposure);
        material.SetFloat("_IsoScale", profile.iso / 100f);
        material.SetFloat("_FrameSeed", seed);
        material.SetFloat("_VignetteStrength", profile.vignetteStrength);
        material.SetFloat("_ChromaticAberration", profile.lateralChromaticAberration);
        material.SetFloat("_Distortion", profile.barrelDistortion);
        material.SetFloat("_Contrast", profile.contrast);
        material.SetFloat("_Sharpen", profile.sharpen);
        material.SetFloat("_GrainStrength", profile.grainStrength);
        material.SetFloat("_PurpleFringe", profile.purpleFringe);
        material.SetVector("_WhiteBalance", profile.WhiteBalanceMultipliers);
    }
}
