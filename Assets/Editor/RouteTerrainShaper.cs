using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// ROTAYI ARAZİYE İŞLER. Çizilen hat araziyi tanımıyordu: fırça yalnız nereden
/// geçileceğini söylüyor, zemin olduğu gibi kalıyordu. Gerçek bir yol araziyi keser ve
/// doldurur — yamacın üstü kazılır, altı doldurulur, arada düz bir taban kalır.
///
/// İKİ AŞAMA:
///   1. BOYUNA TESVİYE — hattın kendi kesiti eğim sınırına çekiliyor. Otobüs %10'un,
///      yürüyüş %25'in üstüne çıkamaz; arazi ne derse desin yol o eğimi tutmak zorunda.
///   2. KAZI VE DOLGU — taban çizgisinden uzaklaştıkça arazinin sapmasına izin verilen
///      pay artıyor. Kazı şevi 1:1 (45°, sıkışmış zemin), dolgu şevi 1:1.5 (34°,
///      dolgunun duruş açısı). Sabit bir geçiş genişliği YOK: pay kot farkından türüyor,
///      yani yüksek yamaçta geniş, düzlükte dar.
///
/// SIRA ÖNEMLİ. Arazi üretildikten HEMEN SONRA, yüzey haritaları pişmeden önce çalışır:
/// haritalar araziden türüyor ve tesviye sonradan yapılırsa eğim, gölge ve kar
/// hesapları eski araziye ait kalır.
///
/// TEKRAR ÇALIŞTIRILAMAZ. Şekillenmiş arazinin üstüne ikinci kez uygulanmak, kesilmiş
/// yamacı yeniden kesmek demek. Her zaman taze üretilmiş arazi üzerinde çalışır.
public static class RouteTerrainShaper
{
    /// TESVİYE SÜRÜMÜ. Buradaki sabitler (iz derinliği, omuz payı, şev açıları, doğuş
    /// düzlüğü) araziyi değiştiriyor ama arazi imzası `MountainSettings`'ten geliyordu:
    /// tesviye ayarları değişince arazi yeniden üretilmiyor ve DEĞİŞİKLİK HİÇ
    /// UYGULANMIYORDU. İz 45 cm'den 1 metreye çıkarıldığında da, doğuş düzlüğü
    /// eklendiğinde de bu oldu.
    ///
    /// Sabitlerden biri değiştiğinde bu sayı da artırılır.
    public const int Version = 9;

    /// Kazı şevi: dik kesilen yamaç bu açıda durur. 1:1 = 45 derece.
    const float CutSlope = 1.0f;

    /// Dolgu şevi: yığılan malzeme bu açıdan dik duramaz. 1:1.5 = 34 derece.
    const float FillRun = 1.5f;

    /// Tesviyenin taban yarım genişliğine eklediği omuz payı (metre). Yolun kendi
    /// genişliği rota verisinde; omuz şekillendirmenin ayarı ve buraya ait
    /// (bkz. `MountainRoute.Mark`).
    ///
    /// 2.5 metreydi ve HİÇBİR ŞEY GÖRÜNMÜYORDU: patika 1.8-3.5 m geniş, arazi ızgarası
    /// 4.28 m/hücre. Toplam koridor tek hücreye düşünce yükseklik haritası onu
    /// taşıyamıyor. Beş metre omuz koridoru 12-14 metreye çıkarıyor, yani üç hücre —
    /// ızgaranın çözebildiği en dar şerit.
    ///
    /// Gerçek bir patika iki metre geniştir; o görüntü DOKUDAN gelecek, geometriden
    /// değil. Buradaki iş yolun oturduğu tabanı açmak.
    const float Shoulder = 5f;

    /// Araç yolunun omuz payı (metre). Patikadan geniş: toprak yolun hendeği, banketi
    /// ve şevi patikanın yanından geçtiği yerden çok daha fazla yer kaplar.
    const float RoadShoulder = 11f;

    /// DOĞUŞ DÜZLÜĞÜ yarıçapı (metre). Otobüs durağı tümseklerin arasına kurulmaz;
    /// düzlenmiş bir sahanlıkta olur — otobüsün dönebileceği, insanın inebileceği yer.
    /// Kamp düzlüğünden geniş, ama açıklık hissi verecek kadar da sınırlı.
    const float SpawnClearing = 45f;

    /// İZ DERİNLİĞİ (metre). Tesviye tek başına yalnız eğimi sınırlıyor ve düz arazide
    /// hiçbir şey yapmıyor: ovanın ortalama eğimi %2, bisiklet sınırı %12, kırpacak bir
    /// şey yok ve hedef mevcut kotun aynısı çıkıyor.
    ///
    /// Yol çevresinden AŞAĞIDA olmalı — yürünen zemin aşınır, malzeme kenara atılır.
    ///
    /// Kırk beş santimdi ve ölçüldü: 13 metrelik koridora yayılınca %7'lik sığ bir tekne
    /// oluyor ve gözle "yol" olarak okunmuyor. Bir metre, aynı koridorda %15 kenar
    /// eğimi demek — belirgin ama hendek değil. Gerçek dünyada uzun kullanılan yollar
    /// yarım ile iki metre arasında çöker; bu aralığın alt ucu.
    const float TreadCut = 1.0f;

    /// Kazı/dolgunun ulaşabileceği en uzak mesafe (metre). Sınırsız bırakılırsa dik bir
    /// yamaçta tek bir hat yüzlerce metrelik bir yarma açıyor ve dağın silueti bozuluyor.
    /// Sınırın dışında arazi kendi hâline bırakılıyor — orası artık yol değil, uçurum.
    const float MaxReach = 70f;

    /// Kamp alanı tesviyesi TAM DÜZ: çadır eğimli zemine kurulmaz.
    const float CampShoulder = 4f;

    /// DOKUNULAN HÜCRELER. "Tesviye yanlış yerde yapılmış" iddiası sayılarla
    /// çözülemedi: rotaya dik kesitler ovanın kendi tepecikleri yüzünden gürültülü
    /// çıkıyor. Maske, şekillendirmenin dokunduğu her hücreyi işaretliyor ve harita
    /// olarak çizilebiliyor — kırmızı rotanın üstündeyse doğru yerde demektir.
    ///
    /// Ova ve yol dokusu oturunca maske de silinir.
    public static bool[] TouchedMask { get; private set; }
    public static int MaskResolution { get; private set; }

    public static void Shape(Terrain terrain, MountainRoute route)
    {
        if (terrain == null || route == null) return;

        TerrainData data = terrain.terrainData;
        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);

        TouchedMask = new bool[res * res];
        MaskResolution = res;

        float metresPerCell = data.size.x / (res - 1);
        float vertical = Mathf.Max(1f, data.size.y);
        Vector3 origin = terrain.transform.position;

        // Yol otobüs için (%10), hatlar BİSİKLET için (%12). Yürüyüş eşiği (%25)
        // kullanılıyordu ve bisikletle çıkılamayacak yokuşlar bırakıyordu; yaklaşma
        // bisikletle geçiliyor ve süre bütçesi buna bağlı (bkz. DECISIONS.md).
        Carve(heights, res, metresPerCell, vertical, origin, terrain,
              route.road, RouteProfile.RoadGrade, RoadShoulder);

        foreach (MountainRoute.Branch branch in route.branches)
            Carve(heights, res, metresPerCell, vertical, origin, terrain,
                  branch.marks, RouteProfile.BikeGrade, Shoulder);

        // DÜZLÜKLER EN SON. Önce açılıyordu ve ölçüldü: doğuş çevresinde kot aralığı
        // 2.97 metre kalıyordu. Sebep sıralama — düzlük açıldıktan sonra yol ve dört
        // hat oradan geçiyor, her biri KENDİ profiline göre bir metre oyuyor ve beş
        // ayrı kot üst üste biniyor.
        //
        // Otobüs durağı ve kamp düz bir sahanlıktır; yolun izi orada biter, ortasından
        // geçmez. Son sırada açılınca hatların bıraktığı basamakları da siliyorlar.
        if (route.spawnSet)
        {
            Vector3 spawn = MountainRoute.ToWorld(route.spawn, terrain);
            Flatten(heights, res, metresPerCell, vertical, origin,
                    spawn, SpawnClearing, CampShoulder * 2f);
        }

        foreach (MountainRoute.Mark camp in route.camps)
        {
            Vector3 centre = MountainRoute.ToWorld(camp.position, terrain);
            Flatten(heights, res, metresPerCell, vertical, origin,
                    centre, camp.radius, CampShoulder);
        }

        data.SetHeights(0, 0, heights);
        SaveMask();
        ReportResult(heights, res, metresPerCell, vertical, origin, route, terrain);
    }

    /// Maske PNG olarak diske yazılıyor: bellekte tutmak derleme sonrası kaybediyor ve
    /// harita ancak arazi yeniden üretildiği anda görülebiliyordu.
    const string MaskPath = "Assets/Terrain/RouteShapeMask.png";
    const int MaskTexture = 1024;

    /// İşaretli pikselleri çevresine yayar. Kırmızı kanal işaret taşıyor.
    static void Dilate(Color32[] pixels, int radius)
    {
        var source = (Color32[])pixels.Clone();
        var red = new Color32(255, 40, 30, 255);

        for (int y = 0; y < MaskTexture; y++)
        for (int x = 0; x < MaskTexture; x++)
        {
            if (source[y * MaskTexture + x].r < 200) continue;

            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int ny = y + dy, nx = x + dx;
                if (ny < 0 || nx < 0 || ny >= MaskTexture || nx >= MaskTexture) continue;

                pixels[ny * MaskTexture + nx] = red;
            }
        }
    }

    static void SaveMask()
    {
        if (TouchedMask == null) return;

        var pixels = new Color32[MaskTexture * MaskTexture];
        int ratio = Mathf.Max(1, MaskResolution / MaskTexture);

        for (int y = 0; y < MaskTexture; y++)
        for (int x = 0; x < MaskTexture; x++)
        {
            bool touched = false;

            // Küçültürken HERHANGİ biri dokunulmuşsa işaretli sayılıyor: ortalama
            // alınırsa dar koridor küçültmede kayboluyor.
            for (int dy = 0; dy < ratio && !touched; dy++)
            for (int dx = 0; dx < ratio && !touched; dx++)
            {
                int sz = y * ratio + dy, sx = x * ratio + dx;
                if (sz < MaskResolution && sx < MaskResolution
                    && TouchedMask[sz * MaskResolution + sx]) touched = true;
            }

            pixels[y * MaskTexture + x] = touched
                ? new Color32(255, 40, 30, 255)
                : new Color32(18, 18, 22, 255);
        }

        // KALINLAŞTIRMA. Koridor 12-28 metre, yani 3-7 arazi hücresi; 1024'lük haritada
        // yarım texel ediyor ve pencereye sığdırılınca 0.3 piksele düşüyor. Çizilemiyor.
        // Maske gerçeği değil OKUNABİLİRLİĞİ taşıyor: işaret altı texel yayılıyor.
        Dilate(pixels, 3);

        var texture = new Texture2D(MaskTexture, MaskTexture, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply(false);

        System.IO.File.WriteAllBytes(MaskPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(MaskPath, ImportAssetOptions.ForceUpdate);
    }

    /// ŞEKİLLENDİRME SONUCU. "Yol hâlâ engebeli" ile "yol düz ama göremiyorum"
    /// arasındaki farkı sayı ayırır. Her üretimde konsola basılıyor: kaç hücreye
    /// dokunuldu, doğuş düzlüğü gerçekten düz mü, yol koridoru ne kadar dalgalı.
    static void ReportResult(float[,] heights, int res, float cell, float vertical,
        Vector3 origin, MountainRoute route, Terrain terrain)
    {
        int touched = 0;
        foreach (bool cellTouched in TouchedMask) if (cellTouched) touched++;

        var report = new System.Text.StringBuilder();
        report.Append($"[Tesviye] dokunulan hücre {touched}");

        if (route.spawnSet)
        {
            Vector3 spawn = MountainRoute.ToWorld(route.spawn, terrain);
            report.Append($"\n  doğuş düzlüğü ({SpawnClearing:F0} m): "
                        + Spread(heights, res, cell, vertical, origin, spawn, SpawnClearing));
        }

        if (route.road.Count > 2)
        {
            Vector3 middle = MountainRoute.ToWorld(
                route.road[route.road.Count / 2].position, terrain);
            report.Append($"\n  yol koridoru (14 m): "
                        + Spread(heights, res, cell, vertical, origin, middle, 14f));
        }

        Debug.Log(report.ToString());
    }

    /// Verilen yarıçaptaki yüzeyin PÜRÜZÜ: en küçük kareler düzleminden sapma. Ham kot
    /// aralığı yanıltıcıydı — tesviye edilmiş ama eğimli bir düzlemde de büyük çıkıyor
    /// ve "düz değil" sanılıyordu. Eğim ayrı, pürüz ayrı bildiriliyor.
    static string Spread(float[,] heights, int res, float cell, float vertical,
        Vector3 origin, Vector3 centre, float radius)
    {
        float low = float.MaxValue, high = float.MinValue;
        float n = 0f, sx = 0f, sz = 0f, sh = 0f;
        float sxx = 0f, szz = 0f, sxz = 0f, sxh = 0f, szh = 0f;

        int x0 = Mathf.Max(0, Mathf.FloorToInt((centre.x - radius - origin.x) / cell));
        int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((centre.x + radius - origin.x) / cell));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((centre.z - radius - origin.z) / cell));
        int z1 = Mathf.Min(res - 1, Mathf.CeilToInt((centre.z + radius - origin.z) / cell));

        for (int z = z0; z <= z1; z++)
        for (int x = x0; x <= x1; x++)
        {
            var point = new Vector2(origin.x + x * cell, origin.z + z * cell);
            if (Vector2.Distance(point, new Vector2(centre.x, centre.z)) > radius) continue;

            float h = heights[z, x] * vertical;
            low = Mathf.Min(low, h);
            high = Mathf.Max(high, h);

            float px = point.x - centre.x, pz = point.y - centre.z;
            n++; sx += px; sz += pz; sh += h;
            sxx += px * px; szz += pz * pz; sxz += px * pz;
            sxh += px * h; szh += pz * h;
        }

        if (low > high || n < 3f) return "hücre yok";

        float dxx = sxx - sx * sx / n, dzz = szz - sz * sz / n, dxz = sxz - sx * sz / n;
        float dxh = sxh - sx * sh / n, dzh = szh - sz * sh / n;
        float determinant = dxx * dzz - dxz * dxz;

        float ax = 0f, az = 0f;
        if (Mathf.Abs(determinant) > 1e-3f)
        {
            ax = (dxh * dzz - dzh * dxz) / determinant;
            az = (dzh * dxx - dxh * dxz) / determinant;
        }

        float grade = new Vector2(ax, az).magnitude;
        return $"eğim %{grade * 100f:F1}, kot aralığı {high - low:F2} m";
    }

    static void Mark(int z, int x, int res)
    {
        if (TouchedMask != null) TouchedMask[z * res + x] = true;
    }

    // ------------------------------------------------------------------ hatlar

    static void Carve(float[,] heights, int res, float cell, float vertical,
        Vector3 origin, Terrain terrain, List<MountainRoute.Mark> marks, float maxGrade,
        float shoulder)
    {
        if (marks.Count < 2) return;

        var points = new Vector3[marks.Count];
        for (int i = 0; i < marks.Count; i++)
        {
            Vector3 world = MountainRoute.ToWorld(marks[i].position, terrain);
            world.y = terrain.SampleHeight(world) + origin.y;
            points[i] = world;
        }

        // ÖNCE DÜZLEŞTİR, SONRA EĞİMİ SINIRLA. Sıra önemli: sınırlama tek başına
        // yetmiyordu çünkü ovanın 2-4 metrelik tepecikleri 40 metrede %10 eğim yapıyor
        // ve bisiklet sınırı %12 — kırpılacak bir şey çıkmıyor. Yol her tümseğin
        // üstünden aynen geçip yalnız bir metre aşağı kayıyordu: tümsekli zemin,
        // tümsekli yol, kontrast sıfır. Görülemedi.
        //
        // Yolu görünür yapan şey derinlik değil, ÇEVRESİ ENGEBELİYKEN KENDİSİNİN DÜZ
        // OLMASI. Profil düzleşince tümsek kazıya, çukur dolguya dönüşüyor.
        SmoothProfile(points, 60f);
        LimitGrade(points, maxGrade);

        for (int i = 1; i < points.Length; i++)
            CarveSegment(heights, res, cell, vertical, origin,
                         points[i - 1], points[i],
                         marks[i - 1].radius, marks[i].radius, shoulder);
    }

    /// BOYUNA DÜZLEŞTİRME. Hattın kesiti araziden okunuyor ve arazinin her tümseğini
    /// taşıyor. Gerçek bir yol arazinin dalgasını izlemez; kazı ve dolguyla kendi
    /// düzgün profilini kurar.
    ///
    /// Pencere YAY UZUNLUĞUNA göre: noktalar fırça yarıçapına bağlı olarak 1-2 metre
    /// aralıklı, yani sabit bir komşu sayısı hatta göre farklı mesafe demek olurdu.
    static void SmoothProfile(Vector3[] points, float window)
    {
        var distances = new float[points.Length];
        for (int i = 1; i < points.Length; i++)
            distances[i] = distances[i - 1]
                         + Vector2.Distance(new Vector2(points[i].x, points[i].z),
                                            new Vector2(points[i - 1].x, points[i - 1].z));

        var smoothed = new float[points.Length];

        for (int i = 0; i < points.Length; i++)
        {
            float sum = 0f, weight = 0f;

            // Üçgen ağırlık: pencerenin ortası tam, kenarı sıfır. Düz ortalama
            // basamak bırakıyor, üçgen sürekli bir profil veriyor.
            for (int k = i; k < points.Length && distances[k] - distances[i] <= window; k++)
            {
                float w = 1f - (distances[k] - distances[i]) / window;
                sum += points[k].y * w;
                weight += w;
            }

            for (int k = i - 1; k >= 0 && distances[i] - distances[k] <= window; k--)
            {
                float w = 1f - (distances[i] - distances[k]) / window;
                sum += points[k].y * w;
                weight += w;
            }

            smoothed[i] = weight > 0f ? sum / weight : points[i].y;
        }

        for (int i = 0; i < points.Length; i++) points[i].y = smoothed[i];
    }

    /// BOYUNA EĞİM SINIRI. Hattın kesiti araziden geliyor ve arazi %60 eğim veriyorsa
    /// yol da %60 oluyor. Profil iki yönde süpürülerek sınıra çekiliyor: ileri süpürme
    /// yokuş yukarı tırmanışı, geri süpürme yokuş aşağı inişi kısıtlıyor. Tek yön
    /// yeterli değil — biri düzeltilirken öteki bozuluyor.
    ///
    /// Sonuç bir ORTALAMA değil TAVAN: eğimi zaten uygun olan parçalara dokunulmuyor,
    /// yalnız aşan yerler kırpılıyor. Yoksa arazinin doğal dalgası da siliniyor.
    static void LimitGrade(Vector3[] points, float maxGrade)
    {
        for (int pass = 0; pass < 4; pass++)
        {
            for (int i = 1; i < points.Length; i++)
                Clamp(ref points[i], points[i - 1], maxGrade);

            for (int i = points.Length - 2; i >= 0; i--)
                Clamp(ref points[i], points[i + 1], maxGrade);
        }
    }

    static void Clamp(ref Vector3 point, Vector3 anchor, float maxGrade)
    {
        float run = Vector2.Distance(new Vector2(point.x, point.z),
                                     new Vector2(anchor.x, anchor.z));
        if (run < 0.01f) return;

        float limit = run * maxGrade;
        point.y = Mathf.Clamp(point.y, anchor.y - limit, anchor.y + limit);
    }

    /// Tek bir parçayı araziye işler. Yalnız parçanın kutusundaki hücreler geziliyor —
    /// dört bin kareyi her parça için taramak dakikalar sürerdi.
    static void CarveSegment(float[,] heights, int res, float cell, float vertical,
        Vector3 origin, Vector3 from, Vector3 to, float radiusFrom, float radiusTo,
        float shoulder)
    {
        float half = Mathf.Max(radiusFrom, radiusTo) + shoulder;

        var a = new Vector2(from.x, from.z);
        var b = new Vector2(to.x, to.z);

        float minX = Mathf.Min(a.x, b.x) - half - MaxReach;
        float maxX = Mathf.Max(a.x, b.x) + half + MaxReach;
        float minZ = Mathf.Min(a.y, b.y) - half - MaxReach;
        float maxZ = Mathf.Max(a.y, b.y) + half + MaxReach;

        int x0 = Mathf.Max(0, Mathf.FloorToInt((minX - origin.x) / cell));
        int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((maxX - origin.x) / cell));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((minZ - origin.z) / cell));
        int z1 = Mathf.Min(res - 1, Mathf.CeilToInt((maxZ - origin.z) / cell));

        Vector2 axis = b - a;
        float lengthSquared = Mathf.Max(1e-4f, axis.sqrMagnitude);

        for (int z = z0; z <= z1; z++)
        for (int x = x0; x <= x1; x++)
        {
            var point = new Vector2(origin.x + x * cell, origin.z + z * cell);

            // Parça üzerindeki en yakın nokta; uçlarda kırpılıyor ki komşu parçalar
            // kendi bölgelerini işlesin ve köşelerde çift kazı olmasın.
            float t = Mathf.Clamp01(Vector2.Dot(point - a, axis) / lengthSquared);
            Vector2 nearest = a + axis * t;

            float distance = Vector2.Distance(point, nearest);
            float edge = Mathf.Lerp(radiusFrom, radiusTo, t) + shoulder;

            if (distance > edge + MaxReach) continue;

            float target = Mathf.Lerp(from.y, to.y, t) - TreadCut;
            float current = heights[z, x] * vertical + origin.y;

            // Taban içinde tam tesviye; dışında arazinin sapmasına izin verilen pay
            // uzaklıkla artıyor. Kazı 45 derecede, dolgu 34 derecede duruyor.
            // TABAN İÇİNDE HEDEF KESİN. `beyond` sıfırken kelepçe aralığı da sıfır
            // oluyor ve `current` hedefe oturuyor; dışarıda pay uzaklıkla açılıyor.
            float beyond = Mathf.Max(0f, distance - edge);
            float allowedAbove = beyond * CutSlope;
            float allowedBelow = beyond / FillRun;

            float shaped = Mathf.Clamp(current, target - allowedBelow, target + allowedAbove);

            // Yalnız ALÇALTMA ve YÜKSELTME gerçekten gerekiyorsa yazılıyor: eşitse
            // dokunulmuyor ve komşu parçaların işi bozulmuyor.
            if (Mathf.Abs(shaped - current) < 0.001f) continue;

            heights[z, x] = Mathf.Clamp01((shaped - origin.y) / vertical);
            Mark(z, x, res);
        }
    }

    // ------------------------------------------------------------------ kamplar

    /// Kamp alanı tam düz. Yükseklik alanın MEVCUT ortalaması: en yüksek noktaya
    /// çekmek dolgu dağı, en alçağa çekmek çukur yaratıyor.
    static void Flatten(float[,] heights, int res, float cell, float vertical,
        Vector3 origin, Vector3 centre, float radius, float shoulder)
    {
        float half = radius + shoulder;

        int x0 = Mathf.Max(0, Mathf.FloorToInt((centre.x - half - MaxReach - origin.x) / cell));
        int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((centre.x + half + MaxReach - origin.x) / cell));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((centre.z - half - MaxReach - origin.z) / cell));
        int z1 = Mathf.Min(res - 1, Mathf.CeilToInt((centre.z + half + MaxReach - origin.z) / cell));

        // DÜZLEM, DÜZ DEĞİL. Tek bir ortalama kota çekmek 90 metrelik kusursuz bir tabla
        // bırakıyordu; doğada öyle bir yüzey yok. Tesviye edilmiş gerçek bir alan
        // DÜZLEMDİR ama eğimlidir — su akması gerekir — ve santimetre ölçeğinde pürüzü
        // kalır.
        //
        // En küçük kareler ile eğimli bir düzlem oturtuluyor: tümsekler siliniyor,
        // alanın kendi genel eğimi korunuyor.
        float n = 0f, sx = 0f, sz = 0f, sh = 0f;
        float sxx = 0f, szz = 0f, sxz = 0f, sxh = 0f, szh = 0f;

        for (int z = z0; z <= z1; z++)
        for (int x = x0; x <= x1; x++)
        {
            var point = new Vector2(origin.x + x * cell, origin.z + z * cell);
            if (Vector2.Distance(point, new Vector2(centre.x, centre.z)) > half) continue;

            float px = point.x - centre.x, pz = point.y - centre.z;
            float h = heights[z, x] * vertical + origin.y;

            n++; sx += px; sz += pz; sh += h;
            sxx += px * px; szz += pz * pz; sxz += px * pz;
            sxh += px * h; szh += pz * h;
        }

        if (n < 3f) return;

        // Merkez etrafında topladığımız için sx ve sz sıfıra yakın; yine de tam çözüm
        // kullanılıyor, disk arazi kenarında kırpılırsa merkez kayıyor.
        float dxx = sxx - sx * sx / n;
        float dzz = szz - sz * sz / n;
        float dxz = sxz - sx * sz / n;
        float dxh = sxh - sx * sh / n;
        float dzh = szh - sz * sh / n;

        float determinant = dxx * dzz - dxz * dxz;
        float slopeX = 0f, slopeZ = 0f;

        if (Mathf.Abs(determinant) > 1e-3f)
        {
            slopeX = (dxh * dzz - dzh * dxz) / determinant;
            slopeZ = (dzh * dxx - dxh * dxz) / determinant;
        }

        // Eğim TAVANLI: dik bir yamaca oturan düzlük, yamacın eğimini miras alıp
        // "tesviye" olmaktan çıkıyor.
        //
        // %4'tü ve ölçüldü: 90 metrelik doğuş düzlüğünde uçtan uca 3.6 metre kot farkı
        // demek. Su akıtmaya fazlasıyla yeter ama otobüs durağı için yokuş. %2 aynı işi
        // görüyor, düzlük düz kalıyor.
        var slope = new Vector2(slopeX, slopeZ);
        if (slope.magnitude > 0.02f) slope = slope.normalized * 0.02f;

        float level = sh / n - (slope.x * sx + slope.y * sz) / n;

        for (int z = z0; z <= z1; z++)
        for (int x = x0; x <= x1; x++)
        {
            var point = new Vector2(origin.x + x * cell, origin.z + z * cell);
            float distance = Vector2.Distance(point, new Vector2(centre.x, centre.z));
            if (distance > half + MaxReach) continue;

            float current = heights[z, x] * vertical + origin.y;
            float beyond = Mathf.Max(0f, distance - half);

            float target = level + slope.x * (point.x - centre.x)
                                 + slope.y * (point.y - centre.z);

            // PÜRÜZ KALIYOR. Hedefe tam oturtmak kusursuz bir düzlem bırakıyor;
            // aslın onda biri korununca yüzey tesviye edilmiş ama yaşayan bir zemin
            // gibi duruyor — greyder izi, oturma, su yolu.
            target = Mathf.Lerp(target, current, 0.1f);

            float shaped = Mathf.Clamp(current, target - beyond / FillRun,
                                                target + beyond * CutSlope);

            if (Mathf.Abs(shaped - current) < 0.001f) continue;

            heights[z, x] = Mathf.Clamp01((shaped - origin.y) / vertical);
            Mark(z, x, res);
        }
    }
}
