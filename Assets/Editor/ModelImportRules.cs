using UnityEditor;

/// MODEL VE DOKU İÇE AKTARMA AYARLARI. Inspector'dan elle tıklanmaz: model yeniden
/// indirildiğinde ayarların da yeniden kurulması gerekirdi ve o an unutulur.
///
/// RIG YALNIZ KARAKTERDE. Humanoid bütün `Assets/Models/` ağacına uygulanıyordu ve
/// bisikleti insan iskeleti sanıp "Hips bulunamadı" hatası basıyordu; üstelik her içe
/// aktarmada kurulum betiğinin rig ayarını eziyordu. Kapsam klasöre bağlandı.
///
/// Yalnızca `Assets/Models/` altındaki dosyalara dokunur.
public class ModelImportRules : AssetPostprocessor
{
    const string Root = "Assets/Models/";
    const string Character = Root + "ArcticExplorer/";

    bool InScope => assetPath.StartsWith(Root);
    bool IsCharacter => assetPath.StartsWith(Character);

    /// Kural değişince modellerin yeniden içe aktarılmasını Unity buradan anlıyor. Sayı
    /// artmazsa dosyalar eski ayarla kalıyor ve değişiklik ancak elle "Reimport" ile
    /// uygulanıyor — yani unutuluyor. Kuralların içeriği her değiştiğinde artırılır.
    public override uint GetVersion() => 2;

    void OnPreprocessModel()
    {
        if (!InScope) return;

        var importer = (ModelImporter)assetImporter;
        bool character = IsCharacter;

        // HUMANOID yalnız karakterde: Unity'nin insan iskeleti soyutlaması sayesinde
        // kemik isimleri ne olursa olsun animasyon aynı avatara oturuyor. Bisiklette
        // iskelet yok — rig kurulmaya çalışılırsa içe aktarma hata basıyor.
        importer.animationType = character
            ? ModelImporterAnimationType.Human
            : ModelImporterAnimationType.None;
        importer.avatarSetup = character
            ? ModelImporterAvatarSetup.CreateFromThisModel
            : ModelImporterAvatarSetup.NoAvatar;
        importer.importAnimation = character;
        importer.importBlendShapes = character;

        // Materyali FBX'ten ALMIYORUZ. Gömülü materyal Standard shader'a bağlı gelir,
        // URP'de macenta çizer. Kendi materyalimizi kurulum betiği atıyor.
        importer.materialImportMode = ModelImporterMaterialImportMode.None;

        importer.importCameras = false;
        importer.importLights = false;
        importer.importVisibility = false;

        // Karakter dışındaki modeller CPU'dan okunuyor: parça ölçümü ve bölge aracı
        // mesh verisine erişiyor. Karakterde gerek yok, okunabilirlik mesh'in ikinci bir
        // kopyasını bellekte tutuyor.
        importer.isReadable = !character;

        // Teğet yalnız normal haritası olan modelde gerekli. Bisiklet yüzeyi prosedürel
        // ve üç milyon üçgen — teğet dizisi bedava değil.
        importer.importTangents = character
            ? ModelImporterTangents.CalculateMikk
            : ModelImporterTangents.None;

        // Dosyanın kendi ölçeği metre cinsinden geliyor; Unity'nin 0.01 varsayılanı
        // modeli santimetreye çevirip minyatüre döndürüyordu.
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
