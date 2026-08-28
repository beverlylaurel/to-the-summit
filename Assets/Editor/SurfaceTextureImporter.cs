using UnityEditor;
using UnityEngine;

/// Import settings for RAW surface maps under `Assets/Terrain`.
/// Not configured manually in the Inspector: when a texture is refreshed, settings reset and get forgotten —
/// reading a normal map as "color" inverts relief direction, passing roughness through sRGB corrupts glossiness.
///
/// Does not touch stochastic baker OUTPUTS (`_T`, `_LUT`): the baker configures them
/// directly with different rules — LUTs are not wrapped or mipmapped.
public class SurfaceTextureImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(TextureIngest.Folder)) return;
        if (assetPath.Contains("_T.png") || assetPath.Contains("_LUT.png")) return;

        bool isNormal = assetPath.EndsWith("_Normal.png");
        bool isData = isNormal || assetPath.EndsWith("_Roughness.png")
                                || assetPath.EndsWith("_Height.png");
        if (!isData) return;

        var importer = (TextureImporter)assetImporter;

        // Tiled across meters; clamping edges leaves seams.
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 1024;

        if (isNormal)
        {
            importer.textureType = TextureImporterType.NormalMap;
            return;
        }

        // Roughness and height are DATA, not color.
        importer.textureType = TextureImporterType.SingleChannel;
        importer.sRGBTexture = false;
    }
}
