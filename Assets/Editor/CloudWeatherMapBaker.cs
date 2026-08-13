using System;
using UnityEditor;
using UnityEngine;

/// Bulutların 2B hava haritasını pişirir: gökyüzünün NERESİNDE ne tür bulut var.
///
/// Kanallar:
///   R  kapsama potansiyeli — çekirdek-birleşim: üstel yarıçaplı bulut çekirdekleri
///      organizasyon alanına göre serpilir, örtüşenler birleşip devasa kümeler kurar
///      (gerçek kümülüs alanlarının güç yasası dağılımı bu birleşmeden doğar).
///   G  bulut tipi — çekirdek başına sabit: yayvan/ince ↔ kabarık/opak.
///   B  taban kayması — çekirdek başına sabit: kimi bulut alçak, kimi yüksek oturur.
///   A  tavan alanı (katman oranı) — tip×dikey gelişim bileşimi PİŞİRMEDE yapılır ve
///      bütün olarak bulanıklaştırılır: tavanın dünya eğimi hiçbir yerde ~45°'yi
///      aşamaz, iğne/bıçak biçimli bulut matematiksel olarak üretilemez. Ayrıca
///      kenara-uzaklık kapağı: dar kapsama yüksek tavan alamaz (kubbe geometrisi —
///      küçük kümülüs boyundan geniştir, kule ancak geniş kütlede yaşar).
///
/// Algoritmanın kanıtı Python simülasyonunda (weather_bake_sim.py): kesitlerde
/// iğne sayısı sıfır, en-boy medyanı 0.7, fırtına süpürmesi 0.15→0.95 doğrulandı.
/// Deterministiktir: aynı tohum aynı haritayı üretir.
public static class CloudWeatherMapBaker
{
    public const string MapPath = "Assets/Settings/CloudWeatherMap.asset";
    public const string SkipPath = "Assets/Settings/CloudSkipMap.asset";
    const string PaintPath = "Assets/Settings/CloudWeatherMap_paint.png";

    /// Algoritma değişince artırılır. Ad ayrıca parametre imzası taşır: F1'de kalibre
    /// edilen değerler asset'e yazıldığında kayıtlı harita bayatlar ve bir sonraki
    /// editör açılışında kendiliğinden yeniden pişer — elle menü gerekmez.
    const int BakeVersion = 25;

    static string VersionedName(AtmosphereSettings s) =>
        $"CloudWeatherMap v{BakeVersion} "
        + $"[{s.weatherMapSeed}|{s.coreRadiusMax:F0}|{s.corePacking:F2}"
        + $"|{s.packingFloor:F2}|{s.patchWindow:F2}|{s.coreDensity:F2}"
        + $"|{s.cloudTopFloor:F2}|{s.cloudTop:F0}|{s.cumulonimbusHeight:F0}"
        + $"|{ArtSignature(s)}]";

    /// Elle boyanmış haritanın imzası. Dosya ADI değil İÇERİK hash'i: aynı dosya
    /// yeniden boyandığında ad değişmiyor ve kayıtlı harita bayat kalıyordu.
    static string ArtSignature(AtmosphereSettings s) =>
        s.artDirectionMap == null || s.artDirectionBlend <= 0f
            ? "-"
            : $"{s.artDirectionMap.imageContentsHash}@{s.artDirectionBlend:F2}";

    /// SÜRÜM İÇE AKTARMA VERİSİNDE, nesne adında değil. Ad kullanılıyordu ve karşılaştırma
    /// HİÇBİR ZAMAN tutmuyordu: `AssetDatabase.CreateAsset` ana nesnenin adını dosya
    /// adına çeviriyor, yani kaydedilen ad "CloudWeatherMap" oluyor ama beklenen
    /// "CloudWeatherMap v25 [...]". Sonuç: harita HER DERLEMEDE yeniden pişiyordu.
    ///
    /// Ölçüldü — kurulumun 1880 milisaniyesinin 1828'i buydu.
    static void StampVersion(string path, string version)
    {
        AssetImporter importer = AssetImporter.GetAtPath(path);
        if (importer == null || importer.userData == version) return;

        importer.userData = version;
        AssetDatabase.WriteImportSettingsIfDirty(path);
    }

    public static Texture2D LoadOrCreate(AtmosphereSettings settings)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(MapPath);
        AssetImporter importer = AssetImporter.GetAtPath(MapPath);

        if (existing != null && importer != null
            && importer.userData == VersionedName(settings)
            && AssetDatabase.LoadAssetAtPath<Texture2D>(SkipPath) != null) return existing;

        if (existing != null) AssetDatabase.DeleteAsset(MapPath);
        return Bake(settings);
    }

    /// Sıçrama haritası hava haritasıyla birlikte pişer; ayrı okunur.
    public static Texture2D LoadSkipMap() =>
        AssetDatabase.LoadAssetAtPath<Texture2D>(SkipPath);

    [MenuItem("To The Summit/Hava/Hava Haritasını Yeniden Pişir", false, 81)]
    static void Rebake()
    {
        var settings = AssetDatabase.LoadAssetAtPath<AtmosphereSettings>(
            "Assets/Settings/AtmosphereSettings.asset");
        if (settings == null)
            throw new InvalidOperationException("AtmosphereSettings bulunamadı.");

        AssetDatabase.DeleteAsset(MapPath);
        AssetDatabase.DeleteAsset(SkipPath);
        Bake(settings);
    }

    /// Boyanacak taban dosyayı üretir: pişmiş haritayı PNG olarak yazar ve import
    /// ayarlarını verinin gerektirdiği hâle getirir — sRGB KAPALI (kanallar renk
    /// değil, kapsama/tip/taban/tavan; sRGB eğrisi değerleri büker), sıkıştırma yok
    /// (blok sıkıştırma kapsama kenarlarına yalan söyler), Read/Write açık
    /// (pişirici CPU'dan okuyor), sarma Repeat (harita periyodik).
    [MenuItem("To The Summit/Hava/Hava Haritasını Dışa Aktar", false, 82)]
    static void ExportForPainting()
    {
        var map = AssetDatabase.LoadAssetAtPath<Texture2D>(MapPath);
        if (map == null)
            throw new InvalidOperationException(
                $"Pişmiş harita yok: {MapPath}. Önce haritayı pişir.");

        System.IO.File.WriteAllBytes(PaintPath, map.EncodeToPNG());
        AssetDatabase.ImportAsset(PaintPath);

        var importer = (TextureImporter)AssetImporter.GetAtPath(PaintPath);
        importer.sRGBTexture = false;
        importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.SaveAndReimport();

        Debug.Log($"Boyanacak taban yazıldı: {PaintPath}. Boyadıktan sonra "
                  + "AtmosphereSettings'te 'Art Direction Map' alanına ver ve payı aç.");
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(PaintPath));
    }

    static Texture2D Bake(AtmosphereSettings settings)
    {
        try
        {
            EditorUtility.DisplayProgressBar("Hava haritası", "Üretiliyor", 0.3f);

            var texture = CloudWeatherMapGenerator.Generate(settings);
            string version = VersionedName(settings);

            AssetDatabase.CreateAsset(texture, MapPath);
            StampVersion(MapPath, version);

            var skip = CloudWeatherMapGenerator.GenerateSkipMap(texture);
            skip.name = "CloudSkipMap";
            AssetDatabase.DeleteAsset(SkipPath);
            AssetDatabase.CreateAsset(skip, SkipPath);

            AssetDatabase.SaveAssets();
            Debug.Log($"Hava haritası pişti: {version}, "
                      + $"periyot {settings.weatherMapWorldSize / 1000f:F0} km.");
            return texture;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }
}
