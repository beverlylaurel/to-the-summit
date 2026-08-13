using UnityEditor;
using UnityEngine;

/// `Assets/Terrain` altındaki HAM yüzey haritalarının içe aktarma ayarları.
/// Elle Inspector'dan tıklanmaz: doku yenilendiğinde ayar sıfırlanır ve unutulur —
/// normal harita "renk" olarak okunursa kabartma yönü tersine döner, pürüzlülük
/// sRGB'den geçerse parlaklık yanlış olur.
///
/// Stokastik pişiricinin ÇIKTILARINA dokunmaz (`_T`, `_LUT`): onları pişirici
/// kendisi ayarlıyor ve kuralları farklı — LUT sarılmaz, mip'lenmez.
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

        // Metrelerce döşeniyor; kenar kelepçelemek dikiş bırakır.
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

        // Pürüzlülük ve yükseklik VERİ, renk değil.
        importer.textureType = TextureImporterType.SingleChannel;
        importer.sRGBTexture = false;
    }
}
