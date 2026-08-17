using System;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

/// `Assets/Terrain/MountainHeightmap.png`'i sahnedeki araziye uygular.
///
/// YÜKSEKLİK HARİTASI ARTIK KAYNAK, TÜRETİLMİŞ DEĞİL. `.gitignore` bir dönem "arazi
/// varlıkları tohum ve ayarlardan yeniden üretiliyor" gerekçesiyle 165 MB'ı dışlıyordu.
/// L1'den sonra o önerme yanlış: arazi `Tools/terrain/` + Argudo + 1.4 GB Kirmse
/// veritabanı olmadan üretilemiyor ve bunların hiçbiri repoda değil, olamaz da.
///
/// Bu yüzden 13.7 MB'lık PNG repoda duruyor; `TerrainData` ve yüzey haritaları (toplam
/// ~165 MB) dışlanmaya devam ediyor — onlar bundan tek adımda pişiyor.
public static class HeightmapImporter
{
    const string HeightmapPath = "Assets/Terrain/MountainHeightmap.png";

    /// Nicemleme ölçeği. `bake_heightmap.py` da bu sayıyı kullanıyor; ikisi ayrılırsa
    /// dağın boyu sessizce kayar.
    const float TerrainHeight = 6189f;
    const float TerrainSize = 17517f;
    const int Resolution = 4097;

    /// `MountainRoute.asset`'in spawn'ı, normalize arazi koordinatı. Eksen denetimi
    /// bunu kullanıyor — zirve merkezde olduğu için devriklikten KAÇAR, ayırt eden
    /// nokta gerekiyor.
    static readonly Vector2 SpawnUv = new Vector2(0.036218f, 0.029233f);

    [MenuItem("To The Summit/Arazi/Yükseklik Haritasını Uygula", false, 12)]
    static void Apply()
    {
        var generator = UnityEngine.Object.FindAnyObjectByType<MountainGenerator>();
        if (generator == null)
            throw new InvalidOperationException(
                "Sahnede MountainGenerator yok; önce kurulum çalışmalı.");

        Apply(generator.GetComponent<Terrain>());
    }

    /// Haritayı okur ve araziye yazar. Bootstrap de bunu çağırıyor.
    public static void Apply(Terrain terrain)
    {
        float[,] heights = Read();

        TerrainData data = terrain.terrainData;
        data.heightmapResolution = Resolution;
        data.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);

        // Boyut ne olursa olsun zirve origin'de kalsın — `SCALE.md`, "arazi konumu".
        terrain.transform.position = new Vector3(-TerrainSize * 0.5f, 0f, -TerrainSize * 0.5f);

        data.SetHeights(0, 0, heights);
        terrain.Flush();

        Verify(heights);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
    }

    /// PNG'yi 16 bit olarak okur ve `[z, x]` düzeninde normalize yükseklik döndürür.
    ///
    /// DOSYA ZATEN UNITY SIRASINDA. `bake_heightmap.py` diziyi yazmadan önce devriğini
    /// alıyor, yani PNG satırı = kuzey, sütunu = doğu. Burada İKİNCİ BİR ÇEVRİM YOK;
    /// iki taraf birden çevirirse dağ eski hâline döner ve kimse fark etmez.
    static float[,] Read()
    {
        if (!File.Exists(HeightmapPath))
            throw new FileNotFoundException(
                $"Yükseklik haritası yok: {HeightmapPath}. " +
                "Önce `python Tools/terrain/bake_heightmap.py --verify` çalıştırılır.");

        EnsureImportSettings();

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(HeightmapPath);
        if (texture == null)
            throw new InvalidOperationException($"{HeightmapPath} doku olarak yüklenemedi.");
        if (texture.width != Resolution || texture.height != Resolution)
            throw new InvalidOperationException(
                $"Çözünürlük {texture.width}x{texture.height}, beklenen {Resolution}x{Resolution}.");
        if (texture.format != TextureFormat.R16)
            throw new InvalidOperationException(
                $"Doku biçimi {texture.format}, R16 bekleniyordu. 16 bit veri 8 bit'e " +
                "düşerse yükseklikte 24 metrelik basamak oluşur.");

        NativeArray<ushort> raw = texture.GetRawTextureData<ushort>();
        if (raw.Length != Resolution * Resolution)
            throw new InvalidOperationException($"Ham veri {raw.Length} örnek, beklenen {Resolution * Resolution}.");

        // KUZEY TERS ÇEVRİLİYOR. Unity dokuları ALTTAN YUKARI saklıyor; PNG satırları
        // yukarıdan aşağı. `GetRawTextureData`'nın 0. satırı görüntünün EN ALT satırı,
        // yani kuzey ucu. `SetHeights` ise `z = 0`'da arazinin GÜNEY kenarını bekliyor.
        //
        // Ölçüldü: düzeltmesiz spawn 3877 m okunuyordu (olması gereken 621 m). Beş aday
        // çevrim tek tek denendi, yalnız "kuzey devrik" 3875 m ile örtüştü — doğu ekseni
        // ve eksen takası tutmadı. Tahminle değil ölçümle bulundu.
        var heights = new float[Resolution, Resolution];
        const float Inv = 1f / 65535f;
        for (int z = 0; z < Resolution; z++)
        {
            int row = (Resolution - 1 - z) * Resolution;
            for (int x = 0; x < Resolution; x++)
                heights[z, x] = raw[row + x] * Inv;
        }
        return heights;
    }

    /// Dokunun 16 bit ve okunabilir gelmesini garanti eder. Varsayılan içe aktarma
    /// PNG'yi sıkıştırıp 8 bit'e düşürüyor; o hâlde yükseklik çözünürlüğü 65536'dan
    /// 256'ya iniyor, yani 24 metrelik basamak.
    static void EnsureImportSettings()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(HeightmapPath);
        if (importer == null)
            throw new InvalidOperationException($"{HeightmapPath} için içe aktarıcı yok.");

        bool dirty = false;

        if (importer.textureType != TextureImporterType.SingleChannel) { importer.textureType = TextureImporterType.SingleChannel; dirty = true; }
        if (importer.sRGBTexture) { importer.sRGBTexture = false; dirty = true; }
        if (!importer.isReadable) { importer.isReadable = true; dirty = true; }
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
        if (importer.npotScale != TextureImporterNPOTScale.None) { importer.npotScale = TextureImporterNPOTScale.None; dirty = true; }
        if (importer.maxTextureSize < 8192) { importer.maxTextureSize = 8192; dirty = true; }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
        if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; dirty = true; }

        var platform = importer.GetDefaultPlatformTextureSettings();
        if (platform.format != TextureImporterFormat.R16 || platform.overridden == false)
        {
            platform.overridden = true;
            platform.format = TextureImporterFormat.R16;
            platform.maxTextureSize = 8192;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(platform);
            dirty = true;
        }

        if (dirty)
        {
            importer.SaveAndReimport();
            ToolLog.Write("Yükseklik haritasının içe aktarma ayarları R16'ya çekildi.");
        }
    }

    /// SESSİZ BOZULMAYA KARŞI. 16 bit veri bir yerde 8 bit'e düşerse yükseklik
    /// 24 metrelik basamaklara oturur; ekranda fark edilmesi zor, ölçümde apaçık.
    /// Zirve merkezde olduğu için tek başına devrikliği yakalamıyor — spawn probu
    /// onun için.
    static void Verify(float[,] heights)
    {
        int bestZ = 0, bestX = 0;
        float best = -1f;
        for (int z = 0; z < Resolution; z++)
        for (int x = 0; x < Resolution; x++)
            if (heights[z, x] > best) { best = heights[z, x]; bestZ = z; bestX = x; }

        float summit = best * TerrainHeight;
        float cell = TerrainSize / (Resolution - 1);
        float half = (Resolution - 1) * 0.5f;
        float offCentre = Mathf.Sqrt((bestZ - half) * (bestZ - half) + (bestX - half) * (bestX - half)) * cell;

        if (Mathf.Abs(summit - 5709f) > 1f)
            throw new InvalidOperationException(
                $"Zirve {summit:F1} m, beklenen 5709 m. 16 bit veri 8 bit'e düşmüş olabilir " +
                "(o durumda basamak 24 m olur).");

        if (offCentre > 20f)
            throw new InvalidOperationException(
                $"Zirve merkezden {offCentre:F0} m ötede. Eksen devrik ya da harita kaymış.");

        int sx = Mathf.RoundToInt(SpawnUv.x * (Resolution - 1));
        int sz = Mathf.RoundToInt(SpawnUv.y * (Resolution - 1));
        float spawn = heights[sz, sx] * TerrainHeight;
        float opposite = heights[Resolution - 1 - sz, Resolution - 1 - sx] * TerrainHeight;

        if (spawn > 1500f)
            throw new InvalidOperationException(
                $"Spawn kotu {spawn:F0} m — ova orada değil. Eksen devrik olabilir.");
        if (opposite < spawn)
            throw new InvalidOperationException(
                $"Kuzeydoğu köşesi ({opposite:F0} m) güneybatıdan ({spawn:F0} m) alçak. Eksen devrik.");

        ToolLog.Write(
            $"Yükseklik haritası uygulandı: zirve {summit:F0} m (merkezden {offCentre:F0} m), " +
            $"spawn {spawn:F0} m, karşı köşe {opposite:F0} m, çözünürlük {Resolution}²  " +
            $"({cell:F2} m/örnek).");
    }
}
