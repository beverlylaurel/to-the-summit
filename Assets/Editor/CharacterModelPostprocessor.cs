using UnityEditor;
using UnityEngine;

/// Karakter modellerinin içe aktarma ayarları. Inspector'dan elle tıklanmaz: model
/// yeniden indirildiğinde ayarların da yeniden kurulması gerekirdi ve o an unutulur —
/// rig "Generic" kalır, avatar kurulmaz, animasyon hiçbir şeye oturmaz.
///
/// Yalnızca `Assets/Models/` altındaki dosyalara dokunur. Projedeki başka bir modelin
/// ayarına karışmaz.
public class CharacterModelPostprocessor : AssetPostprocessor
{
    const string Root = "Assets/Models/";

    bool InScope => assetPath.StartsWith(Root);

    void OnPreprocessModel()
    {
        if (!InScope) return;

        var importer = (ModelImporter)assetImporter;

        // HUMANOID. Unity'nin insan iskeleti soyutlaması: kemik isimleri ne olursa olsun
        // animasyon aynı avatara oturur. Generic kalsaydı her animasyon klibi bu modelin
        // kendi kemik hiyerarşisine bağlı kalırdı.
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

        // Materyali FBX'ten ALMIYORUZ. Gömülü materyal Standard shader'a bağlı gelir,
        // URP'de macenta çizer. Kendi URP Lit materyalimizi bootstrap kuruyor.
        importer.materialImportMode = ModelImporterMaterialImportMode.None;

        importer.importCameras = false;
        importer.importLights = false;
        importer.importVisibility = false;

        // Dosyanın kendi ölçeği metre cinsinden geliyor; Unity'nin 0.01 varsayılanı
        // karakteri santimetreye çevirip minyatüre döndürüyordu.
        importer.useFileScale = true;
        importer.globalScale = 1f;
    }

    void OnPreprocessTexture()
    {
        if (!InScope) return;

        var importer = (TextureImporter)assetImporter;

        if (assetPath.EndsWith("_Normal.png"))
        {
            importer.textureType = TextureImporterType.NormalMap;
            return;
        }

        // Metallic/smoothness VERİ, renk değil: sRGB eğrisinden geçerse okunan
        // pürüzsüzlük yanlış olur. Smoothness alfa kanalında duruyor, o yüzden alfa
        // saydamlık olarak yorumlanmamalı.
        if (assetPath.EndsWith("_MetallicSmoothness.png"))
        {
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
        }
    }
}
