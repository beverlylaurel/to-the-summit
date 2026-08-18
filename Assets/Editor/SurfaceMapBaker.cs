using System;
using UnityEditor;
using UnityEngine;

/// Yükseklik haritasından yüzey verisi çıkarır. Materyal katmanlarının "nerede" sorusunu
/// gürültüyle değil dağın kendi biçimiyle cevaplaması için gerekli.
///
/// Terrain'i değiştirmez, yalnızca okur — onaylanmış dağ olduğu gibi kalır.
///
/// Kanallar:
///   R  birikim   — yukarıdan buraya akan malzeme; çakıl oluklarda toplanır
///   G  konkavlık — yerel çukurluk; nem yarıklarda tutunur, sırtlarda kurur
///   B  maruziyet — göğü ne kadar gördüğü; kapalı yerler nemli ve gölgeli kalır
///   A  eğim      — normalin dikey bileşeni (cos), yumuşak zeminden
public static class SurfaceMapBaker
{
    const string MapPath = "Assets/Terrain/MountainSurfaceMaps.asset";
    const string NormalPath = "Assets/Terrain/MountainNormals.asset";

    /// Zemin normalinin çözünürlüğü. Köşe ızgarasının iki katı genişlikte texel:
    /// üçgen deseni taşıyamayacak kadar geniş, dağın formunu taşıyacak kadar dar.
    /// Aradaki incelik prosedürel kabartının işi.
    /// ÖRGÜYLE EŞİT. 2048'di ve arazi 17.5 km'yken 8.55 m/texel veriyordu; 30 km'de
    /// 14.65 m'ye çıktı, örgü ise 7.32 m. Aradaki fark ekranda zikzak olarak görünüyor:
    /// aydınlık-gölge sınırı doku ızgarasına çapraz düştüğünde bilinear okuma keskin
    /// geçişi texel sınırları boyunca basamaklara çeviriyor (bkz. `BakeNormals`'taki
    /// bulanıklaştırma yorumu — o yumuşatma 8.55 m için ayarlanmıştı).
    ///
    /// 4096'da texel 7.33 m, yani yükseklik ızgarasının kendisi. Maliyet: RGBA32
    /// 4096² = 67 MB, `.gitignore` dışında zaten.
    public const int NormalResolution = 4096;

    /// Normal dokusunun adında taşınan sürüm; pişirme değişince eskisi elenir.
    /// Ad çözünürlüğü de taşıyor: 2048'den 4096'ya çıkıldı, eski harita bayat sayılmalı.
    const string NormalName = "MountainNormals-4096-blur4";

    const string HeightPath = "Assets/Terrain/MountainHeight.asset";

    /// Arazi yüksekliği, dokuya pişirilmiş hâli. Sis katmanları YERDEN yüksekliğe göre
    /// sönmek zorunda (rüzgârın kaldırdığı kar sırta yapışır, deniz seviyesine değil) ve
    /// shader arazinin yüksekliğini başka türlü bilmiyor.
    ///
    /// Unity'nin kendi `heightmapTexture`'ı bedava ama ölçek dönüşümü belgelenmemiş;
    /// yanlış olursa katman yanlış kotta durur ve bu ancak ekranda fark edilir. Sayı
    /// bizim olduğu için burada pişiriliyor: dönüşüm yok, tahmin yok.
    ///
    /// 1024 texel / 17.5 km = 17 metre. 512'de texel 34 metreydi ve keskin sırtlar
    /// tamamen yumuşuyordu — oysa sürüklenen kar tam olarak o sırtlardan fışkırıyor,
    /// kret testi orada sıfır çıkıyordu. İki katı çözünürlük 2 MB tutuyor.
    public const int HeightResolution = 1024;
    const string HeightName = "MountainHeight-r16-1k";

    const string HorizonPath = "Assets/Terrain/MountainHorizon.asset";

    /// Ufuk haritasının adında taşınan sürüm.
    /// SÜRÜM ADI PİŞİRME KURALINI TAŞIYOR. Kural değişince ad da değişir, yoksa eski
    /// harita "güncel" sayılıp diskte kalır ve düzeltme hiç görünmez.
    /// `nolocal`: noktanın kendi eğimi ufuktan çıkarılıyor (bkz. `BakeHorizon`).
    const string HorizonName = "MountainHorizon-r16-nolocal";

    /// Pusula yönü sayısı. Güneşin azimutu iki komşu yönün arasında harmanlanır.
    public const int HorizonDirections = 16;

    const int HorizonResolution = 1024;

    /// Asset'in adında taşınan sürüm. Kanal düzeni değiştiğinde eski harita çözünürlük
    /// kontrolünden geçmeye devam ediyor ve bayat veriyle çizim sürüyor; ad tutmayınca
    /// kurulum betiği yeniden pişiriyor.
    const string MapName = "MountainSurfaceMaps-slope";

    /// Haritalar geniş ölçekli bilgi taşır: hangi olukta malzeme toplanır, nerede nem
    /// tutunur, neresi göğü görür. Tam heightmap çözünürlüğünde hesaplamak milyarlarca
    /// işlem demek ve karşılığında görünür hiçbir şey kazandırmıyor.
    public const int MapResolution = 1024;

    const string DriftPath = "Assets/Terrain/MountainSnowDrift.asset";

    /// Kar BİRİKİM AĞIRLIĞI haritası. Adında hem biçim sürümü hem hâkim rüzgâr açısı
    /// taşınıyor: harita o yöne göre pişiyor ve açı değişince yeniden pişmesi gerekiyor.
    static string DriftName(float prevailingDegrees) =>
        $"MountainSnowDrift-r8-{Mathf.RoundToInt(prevailingDegrees)}";

    /// SÜRÜM İÇE AKTARMA VERİSİNDE, nesne adında değil. Ad kullanılıyordu ve Unity her
    /// pişirmede "Main Object Name X does not match filename Y" uyarısı basıyordu:
    /// beş harita, her yeniden üretmede beş uyarı. Adın işi kimliktir, sürüm taşımak
    /// değil.
    ///
    /// `userData` asset'in .meta dosyasında duruyor; asset'in kendisi yeniden
    /// yazıldığında kaybolmuyor ve sürüm kontrolüne giriyor.
    static void StampVersion(string path, string version)
    {
        // AD DOSYA ADINA EŞİTLENİYOR. Sürüm artık `userData`'da ama eski asset'ler
        // sürümlü adı taşımaya devam ediyor ve Unity her kayıtta "Main Object Name X
        // does not match filename Y" basıyor. Ad yazmayı bırakmak yetmedi; var olan
        // nesnenin adını düzeltmek gerekiyor.
        var asset = AssetDatabase.LoadMainAssetAtPath(path);
        string fileName = System.IO.Path.GetFileNameWithoutExtension(path);

        if (asset != null && asset.name != fileName)
        {
            asset.name = fileName;
            EditorUtility.SetDirty(asset);
        }

        AssetImporter importer = AssetImporter.GetAtPath(path);
        if (importer == null || importer.userData == version) return;

        importer.userData = version;
        AssetDatabase.WriteImportSettingsIfDirty(path);
    }

    static bool VersionMatches(string path, string version)
    {
        AssetImporter importer = AssetImporter.GetAtPath(path);
        return importer != null && importer.userData == version;
    }

    /// Beş haritanın da damgasını siler, yani hepsini bayat ilan eder.
    ///
    /// SÜRÜM DAMGASI ARAZİNİN İÇERİĞİNİ BİLMİYOR. `MapsCurrent` yalnız bir ad
    /// karşılaştırıyor (`importer.userData`); yükseklik haritası değişse de haritalar
    /// "güncel" sayılıyor ve sessizce bayat kalıyorlar. Ölçüldü: L1'de yeni arazi
    /// uygulandı, haritalar eski araziden kaldı ve yüzey eriyen mum gibi aktı —
    /// dikey akıntılar, yanlış gölge, yanlış kar çizgisi.
    ///
    /// Bu yüzden yükseklik haritasını uygulayan her yol burayı çağırıyor
    /// (`Dağ Yapımı` penceresi). Damga silinince kurulum bir sonraki açılışta
    /// kendisi pişiriyor.
    public static void Invalidate()
    {
        foreach (string path in new[] { MapPath, NormalPath, HorizonPath, HeightPath, DriftPath })
        {
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer == null || importer.userData.Length == 0) continue;
            importer.userData = string.Empty;
            importer.SaveAndReimport();
        }
    }

    /// Beş haritanın da güncel olup olmadığı. Kurulum betiği tek soru soruyor;
    /// hangi haritanın hangi sürümü taşıdığı burada duruyor.
    public static bool MapsCurrent(float prevailingDegrees)
    {
        var maps = Load();
        return maps != null && maps.width == MapResolution
            && VersionMatches(MapPath, MapName)
            && VersionMatches(NormalPath, NormalName)
            && VersionMatches(HorizonPath, HorizonName)
            && VersionMatches(HeightPath, HeightName)
            && LoadDrift() != null
            && VersionMatches(DriftPath, DriftName(prevailingDegrees));
    }

    public static Texture2D Load() => AssetDatabase.LoadAssetAtPath<Texture2D>(MapPath);
    public static Texture2D LoadDrift() => AssetDatabase.LoadAssetAtPath<Texture2D>(DriftPath);
    public static Texture2D LoadNormals() => AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
    public static Texture2DArray LoadHorizon() => AssetDatabase.LoadAssetAtPath<Texture2DArray>(HorizonPath);
    public static Texture2D LoadHeight() => AssetDatabase.LoadAssetAtPath<Texture2D>(HeightPath);

    /// Haritayı üretir ve asset olarak yazar.
    public static Texture2D Bake(Terrain terrain, float prevailingDegrees)
    {
        if (terrain == null)
            throw new InvalidOperationException($"{nameof(SurfaceMapBaker)}: terrain yok.");

        var data = terrain.terrainData;
        int res = MapResolution;
        float[,] height = Downsample(data, res);

        // Yatay örnek aralığı ile dikey ölçek farklı birimlerde. Eğim ve akış hesabı
        // ikisini de gerçek metre cinsinden görmeli, yoksa dağın dikliği anlamsızlaşır.
        float spacing = data.size.x / (res - 1);
        float vertical = data.size.y;

        int cells = res * res;
        var accumulation = Accumulate(height, res, spacing, vertical);
        var concavity = new float[cells];
        var exposure = new float[cells];

        // SATIR SATIR PARALEL. Eğrilik 13x13 pencere, gökyüzü maruziyeti sekiz yönde
        // on adım: hücre başına yüzlerce okuma ve bin kare ızgara. İkisi de yalnız
        // `height` dizisini OKUYOR, her satır kendi hücrelerine yazıyor.
        System.Threading.Tasks.Parallel.For(0, res, y =>
        {
            for (int x = 0; x < res; x++)
            {
                int index = y * res + x;
                concavity[index] = Concavity(height, res, x, y, spacing, vertical);
                exposure[index] = SkyExposure(height, res, x, y, spacing, vertical);
            }
        });

        // Birikim ağırlığı HAM eğrilikten pişiyor, normalleşmiş kanaldan değil: ağırlık
        // işaretli bir büyüklük istiyor (çukur eksi, sırt artı) ve Normalize sıfır
        // noktasını 0.5'e taşıyıp aralığı dağılıma göre esnetiyor.
        BakeDriftWeight(height, concavity, res, spacing, vertical, prevailingDegrees);

        Normalize(accumulation);
        Normalize(concavity);
        Normalize(exposure);

        // Eğim mutlak kalır, Normalize edilmez: eşikler derece cinsinden ve cos'a
        // çevrilerek karşılaştırılıyor — dağılıma göre yayılmış bir eğim onlarla
        // karşılaştırılamaz.
        var slope = new float[cells];
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
            slope[y * res + x] = SlopeCosine(height, res, x, y, spacing, vertical);

        var pixels = new Color32[cells];
        for (int i = 0; i < cells; i++)
            pixels[i] = new Color32(ToByte(accumulation[i]), ToByte(concavity[i]),
                                    ToByte(exposure[i]), ToByte(slope[i]));

        var texture = Load();
        if (texture == null || texture.width != res)
        {
            texture = new Texture2D(res, res, TextureFormat.RGBA32, true, true);
            AssetDatabase.CreateAsset(texture, MapPath);
        }

        StampVersion(MapPath, MapName);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels32(pixels);
        texture.Apply(true);

        BakeNormals(data);
        BakeHorizon(data);
        BakeHeight(data);

        EditorUtility.SetDirty(texture);
        AssetDatabase.SaveAssets();

        return texture;
    }

    /// Zemin normali, piksel başına örneklenecek bir dokuya pişirilir.
    ///
    /// Arazinin köşe normalleri dört metrelik ızgarada yaşıyor ve GPU üçgenlerin
    /// arasını doldururken köşegen dikişler bırakıyor: alçak güneş yüzeyi yaladığında
    /// dağ yorgan gibi baklava desenine bölünüyordu. Doku bilinear okunur, köşegeni
    /// yoktur; kaynağı da indirgeme ortalamasından geçtiği için tek örneklik enerji
    /// taşımaz. İnce ayrıntı kaybolmaz — o zaten prosedürel kabartıdan geliyor.
    static void BakeNormals(TerrainData data)
    {
        int res = NormalResolution;
        float[,] height = Downsample(data, res);

        float spacing = data.size.x / (res - 1);
        float vertical = data.size.y;

        // Gradyan önce hesaplanır, sonra yumuşatılır. Keskin bir sırtta normal tek
        // texelde yön değiştirir; aydınlık-gölge sınırı doku ızgarasına çapraz düştüğünde
        // bilinear okuma o keskin geçişi texel sınırları boyunca zikzak basamaklara
        // çevirir — gölge çizgisi piramit dişleriyle çıkıyordu. Geçiş birkaç texele
        // yayılınca bilinear onu düzgün taşır. Kaybolan incelik prosedürel kabartının
        // zaten sağladığı ölçek.
        var gx = new float[res, res];
        var gz = new float[res, res];

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            int x0 = Mathf.Max(x - 1, 0), x1 = Mathf.Min(x + 1, res - 1);
            int y0 = Mathf.Max(y - 1, 0), y1 = Mathf.Min(y + 1, res - 1);

            gx[y, x] = (height[y, x1] - height[y, x0]) * vertical / ((x1 - x0) * spacing);
            gz[y, x] = (height[y1, x] - height[y0, x]) * vertical / ((y1 - y0) * spacing);
        }

        // YUMUŞATMA DÜNYA ÖLÇEĞİNDE SABİT KALMALI. Pencere TEXEL cinsinden; çözünürlük
        // iki katına çıkınca aynı pencere yarı dünya mesafesini kapsıyor. 4096'ya
        // çıkarıldığında bu atlandı ve ÖLÇÜLDÜ: zikzak azaldı ama dağın büyük ölçekli
        // formu kayboldu, kullanıcı "spec sonrası ilk hâline döndü" dedi. 2048'e geri
        // dönülünce form geldi, zikzak da geri geldi.
        //
        // İki geçiş = 2 kat yarıçap: 4096'da dünya ölçeği 2048'in tek geçişiyle aynı,
        // ama ızgara iki kat ince — aynı yumuşaklıkta daha az zikzak. Aranan bileşim bu.
        for (int pass = 0; pass < 2; pass++)
        {
            BoxBlur(gx, res);
            BoxBlur(gz, res);
        }

        var pixels = new Color32[res * res];

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            var normal = new Vector3(-gx[y, x], 1f, -gz[y, x]).normalized;

            pixels[y * res + x] = new Color32(
                (byte)((normal.x * 0.5f + 0.5f) * 255f),
                (byte)((normal.z * 0.5f + 0.5f) * 255f), 0, 255);
        }

        var texture = LoadNormals();
        if (texture == null || texture.width != res)
        {
            texture = new Texture2D(res, res, TextureFormat.RGBA32, true, true);
            AssetDatabase.CreateAsset(texture, NormalPath);
        }

        StampVersion(NormalPath, NormalName);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels32(pixels);
        texture.Apply(true);

        EditorUtility.SetDirty(texture);
    }

    /// Arazi yüksekliği doku olarak. Değer 0-1 normalize: metre karşılığı shader'da
    /// `_TerrainHeightArea.w` ile çarpılıyor, yani ölçek tek yerde duruyor.
    ///
    /// RHalf: değer 0-1 normalize saklanıyor ve `half`'in 1.0 civarındaki adımı 2^-11.
    /// Dikey tavan 8000 m olduğu için karşılığı 3.9 metre — 34 metrelik texel adımının
    /// yanında ihmal edilebilir. RGBA32'ye sıkıştırmak 31 metrelik basamaklar bırakırdı.
    static void BakeHeight(TerrainData data)
    {
        int res = HeightResolution;
        float[,] height = Downsample(data, res);

        var pixels = new Color[res * res];
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
            pixels[y * res + x] = new Color(height[y, x], 0f, 0f, 0f);

        var texture = LoadHeight();
        if (texture == null || texture.width != res)
        {
            texture = new Texture2D(res, res, TextureFormat.RHalf, false, true);
            AssetDatabase.CreateAsset(texture, HeightPath);
        }

        StampVersion(HeightPath, HeightName);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels(pixels);
        texture.Apply(false);

        EditorUtility.SetDirty(texture);
    }

    /// Her texel için, on altı pusula yönünde ufku kapatan en yüksek açı.
    ///
    /// Arazi gölgesi buradan okunur: güneş o yöndeki ufuk açısının altındaysa nokta
    /// gölgededir. Işın yürüyüşü denendi ve iki kez geri alındı — tek ışın artı eşik,
    /// kenarda ya jilet ya nokta üretiyor; zamansal birikim olmadığı için gürültü hiç
    /// çözülmüyordu. Ufuk açısı alanı pürüzsüz ve pişirilebilir: dağ statik, her
    /// noktanın her yöndeki ufku sabit. Çalışma zamanında iki okuma, sıfır rastgelelik.
    static void BakeHorizon(TerrainData data)
    {
        int res = HorizonResolution;
        float[,] height = Downsample(data, res);

        float spacing = data.size.x / (res - 1);
        float vertical = data.size.y;

        var texture = LoadHorizon();
        if (texture == null || texture.width != res || texture.depth != HorizonDirections
            || texture.format != TextureFormat.R16)
        {
            texture = new Texture2DArray(res, res, HorizonDirections,
                TextureFormat.R16, false, true);
            AssetDatabase.CreateAsset(texture, HorizonPath);
        }

        var slice = new ushort[res * res];

        for (int d = 0; d < HorizonDirections; d++)
        {
            EditorUtility.DisplayProgressBar("Yüzey",
                $"Ufuk haritası pişiyor ({d + 1}/{HorizonDirections})...",
                d / (float)HorizonDirections);

            float angle = d * Mathf.PI * 2f / HorizonDirections;
            float dirX = Mathf.Cos(angle);
            float dirZ = Mathf.Sin(angle);

            System.Threading.Tasks.Parallel.For(0, res, y =>
            {
                for (int x = 0; x < res; x++)
                {
                    float h0 = height[y, x] * vertical;
                    float steepest = 0f;

                    // Üstel adımlar: yakında sık, uzakta seyrek. Ufku belirleyen şey
                    // çoğunlukla yakın sırtlar; uzak zirveler alçak açıyla katılır.
                    float travelled = spacing;

                    while (true)
                    {
                        float sx = x + dirX * (travelled / spacing);
                        float sy = y + dirZ * (travelled / spacing);

                        if (sx < 0f || sy < 0f || sx > res - 2 || sy > res - 2) break;

                        int ix = (int)sx, iy = (int)sy;
                        float fx = sx - ix, fy = sy - iy;

                        float h = (height[iy, ix] * (1f - fx) + height[iy, ix + 1] * fx) * (1f - fy)
                                + (height[iy + 1, ix] * (1f - fx) + height[iy + 1, ix + 1] * fx) * fy;

                        float slope = (h * vertical - h0) / travelled;
                        if (slope > steepest) steepest = slope;

                        travelled *= 1.3f;
                    }

                    // NOKTANIN KENDİ EĞİMİ ÇIKARILIYOR. Ufuk yürüyüşü ilk adımda
                    // komşu texel'i okuyor; eğimli bir yamaçta o komşu zaten yukarıda
                    // ve "engel" sayılıyor. Ama eğimli bir DÜZLEMDE iki koşul birebir
                    // aynıdır: "ufuk güneşten yüksek" ile "N·L <= 0". Yani yamacın
                    // kendisi hem burada hem N·L'de sayılıyordu — iki kez.
                    //
                    // Ölçüldü (azimut 200, zirveden 6 km içinde): ufuk ortancası 16.5
                    // derece, kendi eğimi çıkınca gerçek engel 2.0 derece. Noktaların
                    // %46'sında ufkun TAMAMI kendi eğimi. Güneş 30 derecedeyken gölgede
                    // kalan yüzey %36'dan %9'a iniyor.
                    //
                    // Belirti: güneş tam karşıda ve yüksekken ayağının dibindeki yamaç
                    // gölgede. Kullanıcı "gölge oluşması için hiçbir sebep yok" dedi ve
                    // haklıydı.
                    //
                    // Çıkarma AÇI uzayında: eğimler tanjant, tanjant farkı açı farkı
                    // değil.
                    int lx0 = Mathf.Max(x - 1, 0), lx1 = Mathf.Min(x + 1, res - 1);
                    int ly0 = Mathf.Max(y - 1, 0), ly1 = Mathf.Min(y + 1, res - 1);
                    float gxLocal = (height[y, lx1] - height[y, lx0]) * vertical / ((lx1 - lx0) * spacing);
                    float gzLocal = (height[ly1, x] - height[ly0, x]) * vertical / ((ly1 - ly0) * spacing);
                    float localRise = Mathf.Atan(Mathf.Max(gxLocal * dirX + gzLocal * dirZ, 0f));
                    float occlusion = Mathf.Max(Mathf.Atan(steepest) - localRise, 0f);

                    // Açı 0..90 derece aralığında 16 bite sıkışır. R8 denendi ve
                    // yetmedi: 0.35 derecelik kuantalamanın konturları, yakın zeminde
                    // düz çizgi segmentleri olarak görünüyordu — arazi kafesini andıran
                    // "çatlaklar" buydu. 16 bitte kademe 0.0014 derece, göz sınırının
                    // çok altında.
                    slice[y * res + x] = (ushort)(occlusion / (Mathf.PI * 0.5f) * 65535f);
                }
            });

            texture.SetPixelData(slice, 0, d);
        }

        EditorUtility.ClearProgressBar();

        StampVersion(HorizonPath, HorizonName);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.Apply(false);

        EditorUtility.SetDirty(texture);
    }

    /// Ayrılabilir kutu bulanıklaştırma, yarıçap 2. İki geçiş: önce satırlar, sonra
    /// sütunlar — kare çekirdekle aynı sonuç, res² × yarıçap maliyetle.
    static void BoxBlur(float[,] values, int res)
    {
        const int Radius = 2;
        var row = new float[res];

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float sum = 0f;
                int count = 0;
                for (int k = -Radius; k <= Radius; k++)
                {
                    int i = x + k;
                    if (i < 0 || i >= res) continue;
                    sum += values[y, i];
                    count++;
                }
                row[x] = sum / count;
            }
            for (int x = 0; x < res; x++) values[y, x] = row[x];
        }

        for (int x = 0; x < res; x++)
        {
            for (int y = 0; y < res; y++)
            {
                float sum = 0f;
                int count = 0;
                for (int k = -Radius; k <= Radius; k++)
                {
                    int i = y + k;
                    if (i < 0 || i >= res) continue;
                    sum += values[i, x];
                    count++;
                }
                row[y] = sum / count;
            }
            for (int y = 0; y < res; y++) values[y, x] = row[y];
        }
    }

    static byte ToByte(float value) => (byte)(Mathf.Clamp01(value) * 255f);

    /// Normalin dikey bileşeni: 1 düz zemin, 0 dik duvar. Merkezi farkla, harita
    /// çözünürlüğünde — texel'i köşe ızgarasının dört katı geniş olduğu için üçgen
    /// deseninden arınmış, bilinear örneklemeyle de yumuşak.
    static float SlopeCosine(float[,] height, int res, int x, int y, float spacing, float vertical)
    {
        int x0 = Mathf.Max(x - 1, 0), x1 = Mathf.Min(x + 1, res - 1);
        int y0 = Mathf.Max(y - 1, 0), y1 = Mathf.Min(y + 1, res - 1);

        float gx = (height[y, x1] - height[y, x0]) * vertical / ((x1 - x0) * spacing);
        float gz = (height[y1, x] - height[y0, x]) * vertical / ((y1 - y0) * spacing);

        return 1f / Mathf.Sqrt(1f + gx * gx + gz * gz);
    }

    /// Kanalı kendi dağılımına göre 0-1'e yayar. Ölçeği elle seçilmiş sabitlere bırakmak
    /// değerleri bir uca yığıyor: birikimde her yer 0.6-0.9 bandında sıkışıyor, maruziyette
    /// etek tamamen doyuyordu. Uçlardaki %2'lik dilim kırpılır — birkaç aykırı hücre
    /// bütün bandı kendine ayırmasın — geri kalanı tüm aralığı kullanır.
    /// Dağ değişse bile ölçek kendiliğinden doğru kalır.
    /// KAR BİRİKİM AĞIRLIĞI. Arazi rüzgârın hızını değiştirir, hız da birikimi:
    /// rüzgârüstü ve dışbükey yüzeyde rüzgâr hızlanır ve kar kazınır; rüzgâraltı ve
    /// içbükey yüzeyde yavaşlar ve kar yığılır. Liston &amp; Sturm'ün SnowTran-3D /
    /// MicroMet formülasyonu:
    ///
    ///     W = 1 + 0.5·Ωs + 0.5·Ωc,  W ∈ [0.5, 1.5],  birikim ∝ 1/W
    ///
    /// Ωs rüzgâr yönündeki eğim, Ωc eğrilik; ikisi de [-0.5, 0.5]'e normalleniyor.
    /// Saha ölçümü rüzgâraltı yamacın rüzgârüstünün iki katını tuttuğunu söylüyor
    /// (taze karda dört katına kadar); bu formül uçlar arasında 3.0 kat veriyor.
    ///
    /// PİŞİRİLİYOR, çalışma anında hesaplanmıyor: hâkim rüzgâr yönü sabit bir ayar.
    /// Böylece ne fragman başına ek gradyan okuması var, ne de CPU ikizinin normal
    /// haritasını ayrıca örneklemesi gerekiyor — iki taraf aynı dokuyu okuyor.
    static void BakeDriftWeight(float[,] height, float[] concavity, int res,
        float spacing, float vertical, float prevailingDegrees)
    {
        float angle = prevailingDegrees * Mathf.Deg2Rad;
        float windX = Mathf.Cos(angle);
        float windZ = Mathf.Sin(angle);

        int cells = res * res;
        var alongWind = new float[cells];

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            int x0 = Mathf.Max(x - 1, 0), x1 = Mathf.Min(x + 1, res - 1);
            int y0 = Mathf.Max(y - 1, 0), y1 = Mathf.Min(y + 1, res - 1);

            // Yukarı bakan gradyan (metre/metre). Rüzgâr yönünde yükseliyorsa yüzey
            // rüzgâra bakıyor demektir — Ωs pozitif, rüzgâr hızlanır.
            float dx = (height[y, x1] - height[y, x0]) * vertical / ((x1 - x0) * spacing);
            float dz = (height[y1, x] - height[y0, x]) * vertical / ((y1 - y0) * spacing);

            alongWind[y * res + x] = dx * windX + dz * windZ;
        }

        NormalizeSigned(alongWind);

        var curvature = (float[])concavity.Clone();
        NormalizeSigned(curvature);

        // TEK KANAL. Değer tek bir sayı; RGBA32'de dört kopya olarak durunca harita
        // 11 MB tutuyordu ve her rüzgâr açısı değişimi LFS'e o kadar daha yazıyordu.
        var pixels = new byte[cells];
        for (int i = 0; i < cells; i++)
        {
            // Eğrilik dizisi ÇUKURDA POZİTİF (komşuların ortalaması merkezden yüksek);
            // Liston'un işareti tersi — içbükey negatif. Bu yüzden eksi.
            float w = Mathf.Clamp(1f + 0.5f * alongWind[i] - 0.5f * curvature[i], 0.5f, 1.5f);

            // Ağırlık 0.667-2.0 aralığında; ikiye bölünüp bayta sığıyor, shader ikiyle
            // çarpıp geri alıyor. Çözünürlük 0.008 — birikinti tavanının binde altısı.
            pixels[i] = ToByte(1f / w * 0.5f);
        }

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(DriftPath);
        if (texture == null || texture.width != res || texture.format != TextureFormat.R8)
        {
            texture = new Texture2D(res, res, TextureFormat.R8, true, true);
            AssetDatabase.CreateAsset(texture, DriftPath);
        }

        StampVersion(DriftPath, DriftName(prevailingDegrees));
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixelData(pixels, 0);
        texture.Apply(true);

        EditorUtility.SetDirty(texture);
        AssetDatabase.SaveAssetIfDirty(texture);
    }

    /// İşaretli normalleme: sıfır sıfırda kalır, uçlar ±0.5'e oturur. `Normalize`
    /// dağılımı 0-1'e yayıyor ve sıfır noktasını kaybediyor — işaretin anlamı olan
    /// büyüklüklerde (eğim yönü, eğrilik) bu yanlış.
    static void NormalizeSigned(float[] values)
    {
        var sorted = (float[])values.Clone();
        for (int i = 0; i < sorted.Length; i++) sorted[i] = Mathf.Abs(sorted[i]);
        Array.Sort(sorted);

        float scale = sorted[Mathf.FloorToInt(sorted.Length * 0.98f)];
        if (scale <= 1e-6f) return;

        for (int i = 0; i < values.Length; i++)
            values[i] = Mathf.Clamp(values[i] / scale, -1f, 1f) * 0.5f;
    }

    static void Normalize(float[] values)
    {
        var sorted = (float[])values.Clone();
        Array.Sort(sorted);

        float low = sorted[Mathf.FloorToInt(sorted.Length * 0.02f)];
        float high = sorted[Mathf.FloorToInt(sorted.Length * 0.98f)];
        float span = high - low;

        if (span <= 1e-6f) return;

        for (int i = 0; i < values.Length; i++)
            values[i] = Mathf.Clamp01((values[i] - low) / span);
    }

    /// Heightmap'i harita çözünürlüğüne indirger. Her hedef hücre, kaynaktaki bloğun
    /// **ortalamasını** alır.
    ///
    /// Tek bir bilinear örnek almak yetmiyor: 4097'den 1024'e inerken her hedef hücre
    /// kaynakta 4×4'lük bir alanı temsil ediyor, bilinear ise onun yalnızca 2×2'sine
    /// bakıp aradaki detayı atlıyor. Terrain'in yüksek frekanslı dokusu (erozyon izleri,
    /// teraslama) o boşluktan aliasing olarak geri geliyor ve haritaları benekliyor.
    static float[,] Downsample(TerrainData data, int res)
    {
        int source = data.heightmapResolution;
        float[,] full = data.GetHeights(0, 0, source, source);

        if (source == res) return full;

        var result = new float[res, res];
        float step = (source - 1f) / (res - 1f);
        int radius = Mathf.Max(1, Mathf.CeilToInt(step * 0.5f));

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            int cx = Mathf.RoundToInt(x * step);
            int cy = Mathf.RoundToInt(y * step);

            int x0 = Mathf.Max(cx - radius, 0), x1 = Mathf.Min(cx + radius, source - 1);
            int y0 = Mathf.Max(cy - radius, 0), y1 = Mathf.Min(cy + radius, source - 1);

            float sum = 0f;
            for (int sy = y0; sy <= y1; sy++)
            for (int sx = x0; sx <= x1; sx++)
                sum += full[sy, sx];

            result[y, x] = sum / ((x1 - x0 + 1) * (y1 - y0 + 1));
        }

        return result;
    }

    /// Akış birikimi: her hücre yükünü aşağı komşularına **eğimle orantılı** dağıtır.
    /// Yükü tek bir en dik komşuya vermek (D8) komşuları sıfırla yüksek değer arasında
    /// zıplatıyor; malzeme gerçekte de tek çizgide değil, yelpaze halinde iner.
    ///
    /// Hücreler yüksekten alçağa sıralı işlenir. Bir hücreye sıra geldiğinde yukarısındaki
    /// her şey zaten aktarılmıştır, dolayısıyla taşıdığı yük kesindir — tek geçiş yeter.
    /// İteratif aktarımda yük geçiş başına yalnızca bir hücre ilerliyordu; 17 kilometrelik
    /// dağda zirvenin suyu eteğe hiçbir zaman ulaşmıyor, üstelik çukurlarda salınıp tek
    /// hücrede birikerek haritayı benekliyordu.
    static float[] Accumulate(float[,] height, int res, float spacing, float vertical)
    {
        int cells = res * res;
        var load = new float[cells];
        var slopes = new float[8];

        for (int i = 0; i < cells; i++) load[i] = 1f;

        var order = new int[cells];
        var keys = new float[cells];

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            int index = y * res + x;
            order[index] = index;
            keys[index] = -height[y, x];   // negatif: sıralama yüksekten alçağa olsun
        }

        Array.Sort(keys, order);

        foreach (int here in order)
        {
            int x = here % res, y = here / res;
            float carried = load[here];
            float h = height[y, x] * vertical;

            float total = 0f;
            int n = 0;

            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0) continue;

                int nx = x + ox, ny = y + oy;
                if (nx < 0 || ny < 0 || nx >= res || ny >= res) { slopes[n++] = 0f; continue; }

                float drop = h - height[ny, nx] * vertical;
                if (drop <= 0f) { slopes[n++] = 0f; continue; }

                // Çapraz komşu daha uzakta; eğim gerçek mesafeye bölünmeli
                float distance = spacing * ((ox != 0 && oy != 0) ? 1.41421f : 1f);
                float slope = drop / distance;

                slopes[n++] = slope;
                total += slope;
            }

            // Çukur dibi: akacak yer yok, yük burada kalır. Tek geçişte işlendiği için
            // artık salınıp birikemiyor — göl gibi durur, o da doğrusu.
            if (total <= 0f) continue;

            n = 0;
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0) continue;

                float share = slopes[n++];
                if (share <= 0f) continue;

                load[(y + oy) * res + (x + ox)] += carried * (share / total);
            }
        }

        // Birikim üstel dağılır: logaritma olmadan dere yatakları dışındaki her yer
        // sıfıra yapışır. Ölçeklemeyi Normalize devralıyor, burada yalnızca sıkıştırılır.
        for (int i = 0; i < cells; i++)
            load[i] = Mathf.Log(1f + load[i]);

        return Blur(load, res);
    }

    /// Hafif bulanıklaştırma. Birikim geniş ölçekli bir bilgi — hangi olukta malzeme
    /// toplanıyor; tek piksel keskinliğe ihtiyacı yok. Teraslamadan gelen blok kenarları
    /// bununla yumuşuyor, çakıl maskesi dağda düz kenarlı yamalar bırakmıyor.
    static float[] Blur(float[] source, int res)
    {
        var result = new float[source.Length];

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float sum = 0f;
            int count = 0;

            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int nx = x + ox, ny = y + oy;
                if (nx < 0 || ny < 0 || nx >= res || ny >= res) continue;

                sum += source[ny * res + nx];
                count++;
            }

            result[y * res + x] = sum / count;
        }

        return result;
    }

    /// Yerel çukurluk: komşuların ortalaması bu noktadan yüksekse burası bir oyuk.
    /// Nem oyuklarda tutunur, sırtlarda rüzgâr kurutur. 0.5 düz zemin.
    ///
    /// Yarıçap vadi ölçeğinde olmalı. Birkaç hücreyle bakınca arazinin kendi
    /// dalgalanması okunuyor ve harita kontur çizgisi gürültüsüne dönüşüyor.
    const int ConcavityRadius = 6;

    /// Eğrilik: komşuluğun ortalama yüksekliği eksi merkez. Pozitif = çukur.
    ///
    /// ÇEKİRDEK DAİRESEL, kare değil. Kare kutu ortalamasıydı ve frekans cevabı
    /// eksenlere hizalı: kanal ızgaraya hizalı bir desen taşıyordu ve büyütüldüğü her
    /// yerde (kar kalınlığı, birikinti) yüzeyde tarama çizgisi olarak okunuyordu.
    /// Yarıçap zaten doğruydu — 6 texel × 17.1 m ≈ 103 m — sorun şekildeydi.
    ///
    /// Ağırlık Gauss: dairesel maske tek başına kenarında basamak bırakır ve o basamak
    /// da yönlü. σ yarıçapın yarısı.
    static float Concavity(float[,] height, int res, int x, int y, float spacing, float vertical)
    {
        int x0 = Mathf.Max(x - ConcavityRadius, 0), x1 = Mathf.Min(x + ConcavityRadius, res - 1);
        int y0 = Mathf.Max(y - ConcavityRadius, 0), y1 = Mathf.Min(y + ConcavityRadius, res - 1);

        const float Sigma = ConcavityRadius * 0.5f;
        const float TwoSigmaSquared = 2f * Sigma * Sigma;
        const float RadiusSquared = ConcavityRadius * ConcavityRadius;

        float sum = 0f;
        float weightTotal = 0f;

        for (int ny = y0; ny <= y1; ny++)
        for (int nx = x0; nx <= x1; nx++)
        {
            if (nx == x && ny == y) continue;

            float dx = nx - x, dy = ny - y;
            float distanceSquared = dx * dx + dy * dy;
            if (distanceSquared > RadiusSquared) continue;

            float weight = Mathf.Exp(-distanceSquared / TwoSigmaSquared);
            sum += height[ny, nx] * weight;
            weightTotal += weight;
        }

        // Ham eğim farkı; ölçeklemeyi Normalize yapıyor
        return (sum / weightTotal - height[y, x]) * vertical / (spacing * ConcavityRadius);
    }

    /// Gökyüzü maruziyeti: sekiz yönde ufuk açısı taranır. Vadi dibi göğü az görür,
    /// sırtlar tamamen açıktır. Hem nem hem yüzey gölgelenmesi buradan okunur.
    ///
    /// Adımlar üstel: yakını sık, uzağı seyrek örnekler. Sabit adımla menzil birkaç yüz
    /// metrede kalıyordu ve 17 kilometrelik bir dağda her yer "açık" çıkıyordu.
    static readonly int[] ExposureSteps = { 1, 2, 3, 5, 8, 13, 21, 34, 55, 89 };

    static float SkyExposure(float[,] height, int res, int x, int y, float spacing, float vertical)
    {
        const int Directions = 8;

        float h = height[y, x] * vertical;
        float open = 0f;

        for (int d = 0; d < Directions; d++)
        {
            float angle = d * Mathf.PI * 2f / Directions;
            float ux = Mathf.Cos(angle), uy = Mathf.Sin(angle);

            float highest = 0f;

            foreach (int step in ExposureSteps)
            {
                int nx = x + Mathf.RoundToInt(ux * step);
                int ny = y + Mathf.RoundToInt(uy * step);
                if (nx < 0 || ny < 0 || nx >= res || ny >= res) break;

                float rise = height[ny, nx] * vertical - h;
                if (rise <= 0f) continue;

                highest = Mathf.Max(highest, rise / (spacing * step));
            }

            // Ufuk açısı ne kadar yüksekse o yön o kadar kapalı.
            // Ham eğim; bandı Normalize dağıtıyor.
            open -= highest;
        }

        return open / Directions;
    }
}
