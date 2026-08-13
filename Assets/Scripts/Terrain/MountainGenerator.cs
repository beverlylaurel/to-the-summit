using System;
using UnityEngine;

/// Prosedürel dağ yükseklik haritası. Tüm parametreler MountainSettings asset'inden gelir.
[RequireComponent(typeof(Terrain))]
public class MountainGenerator : MonoBehaviour
{
    [SerializeField] MountainSettings settings;

    /// Yükseklik kuşağı başına eğim dağılımı. Sıra: yürünebilir (0-30°), zorlu (30-45°),
    /// tırmanma (45-70°), duvar (70°+). Yüzde cinsinden.
    [System.Serializable]
    public struct SlopeBand
    {
        public float walkable;
        public float strenuous;
        public float climbable;
        public float wall;
        public float meanDegrees;
    }

    public const int AltitudeBandCount = 4;

    // Türetilmiş veri: her kurulum çalışmasında Generate ya da Measure yeniden
    // hesaplıyor ve tüketiciler (sürücü bağlama, rapor, Tuner) hep o hesaptan sonra
    // okuyor. Serileştirilirken her hesap sahneyi kirletiyordu — Play'e her basış
    // commit'e eğim istatistiği farkı olarak giriyordu.
    [System.NonSerialized] public SlopeBand[] bands = new SlopeBand[AltitudeBandCount];
    [System.NonSerialized] public float meanSlopeDegrees;
    /// Üretilen arazinin gerçek zirvesi (metre). terrainHeight yalnızca tavandır.
    [System.NonSerialized] public float peakAltitude;
    [HideInInspector] public string lastBuildSignature;

    struct Peak
    {
        public Vector2 center;
        public float radius;
        public float height;
    }

    Vector2 warpOffsetA, warpOffsetB, warpDetailOffsetA, warpDetailOffsetB;
    Vector2 radialOffset, terraceOffset, gridOffset;
    Vector2[] octaveOffsets;
    Peak[] peaks;
    int effectiveOctaves;

    public MountainSettings Settings => settings;

    /// Çözünürlüğün taşıyabildiği oktav sayısı. Fazlası aliasing üretir.
    public int EffectiveOctaves => effectiveOctaves;

    public void Bind(MountainSettings source) => settings = source;

    public void Generate() => Generate(settings.heightmapResolution);

    /// <param name="resolution">Önizleme için düşük çözünürlük verilebilir.</param>
    public void Generate(int resolution)
    {
        if (settings == null)
            throw new System.InvalidOperationException($"{nameof(MountainGenerator)}: ayarlar atanmadı.");
        if (settings.heightProfile == null || settings.heightProfile.length == 0)
            throw new System.InvalidOperationException($"{nameof(MountainGenerator)}: profil eğrisi boş.");

        var terrain = GetComponent<Terrain>();
        var data = terrain.terrainData;

        data.heightmapResolution = resolution;
        data.size = new Vector3(settings.terrainSize, settings.terrainHeight, settings.terrainSize);

        // Boyut değişse de dağın zirvesi origin'de kalsın
        transform.position = new Vector3(-settings.terrainSize * 0.5f, 0f, -settings.terrainSize * 0.5f);

        InitRandomState();

        int res = data.heightmapResolution;
        effectiveOctaves = MaxOctavesFor(res);
        var heights = new float[res, res];
        float inv = 1f / (res - 1f);

        // SATIR SATIR PARALEL. Dört bin kare örnek tek çekirdekte yaklaşık yarım dakika
        // sürüyordu; her hücre birbirinden bağımsız hesaplanıyor ve paylaşılan hiçbir
        // durum yok, yani bölünmesi bedava.
        //
        // İKİ ŞART SAĞLANDI. Bir: `AnimationCurve.Evaluate` iş parçacığı güvenli değil
        // (içinde önbellek tutuyor), o yüzden profil eğrisi önce diziye pişiriliyor.
        // İki: arazi türü ağırlıkları paylaşılan bir alandaydı, yığına taşındı.
        BakeProfileLut();

        System.Threading.Tasks.Parallel.For(0, res, z =>
        {
            float v = z * inv;
            for (int x = 0; x < res; x++)
                heights[z, x] = SampleHeight(x * inv, v);
        });

        Erode(heights, res);
        FileCrests(heights, res);
        VerifyFinite(heights, res);

        data.SetHeights(0, 0, heights);
        terrain.Flush();

        ComputeSlopeStats(heights, res);
    }

    /// Sivri uçları törpüler: komşu ortalamasının üstüne taşan hücreler, taşmalarının
    /// bir oranı kadar aşağı çekilir.
    ///
    /// Sırt gürültüsünün katlaması her tepeyi tek örneklik bir diş olarak üretiyor ve
    /// sırt çizgileri testereye dönüyordu. İçbükeyler taşma üretmediği için vadiler ile
    /// yamaçlar el değmeden kalır; geniş sırtın kendi kavisi dört metre ölçekte küçük
    /// taşma ürettiği için büyük form neredeyse hiç kıpırdamaz.
    ///
    /// Törpü ORANSALDIR, eşikli değil. Eşikli sürüm denendi ve yanlıştı: her dişi
    /// eşiğin hemen altına tıraşlıyor, geriye eşit boyda mini piramitlerden bir tarla
    /// kalıyordu — tekdüzelik, düzensiz büyük dişlerden daha belirgin bir desen.
    /// Oransalda büyük diş çok, küçük diş az iner; düzensizlik korunur.
    ///
    /// Termal erozyondan farkı: malzeme taşımaz, açıya bakmaz. Erozyon güçlendirilerek
    /// denendi ve dağı moloz konilerine çevirdi — sorun yamacın dikliği değil, ucun
    /// sivriliğiydi.
    void FileCrests(float[,] heights, int res)
    {
        if (settings.crestSoftening <= 0f) return;

        // Pencere iki örnek yarıçaplı. Bir örneklik pencere yalnızca tek örneklik sivri
        // uçları görüyor; ızgaraya çapraz uzanan keskin bir sırt ise iki-üç örneklik
        // basamaklara bölünüyor (her basamak bir quad) ve dar pencereden sağ çıkıyordu.
        // Yakında tam çözünürlük o merdiveni gösteriyor, uzakta LOD köşeleri atlayıp
        // silueti düzleştiriyor — "dişler yaklaşınca beliriyor"un sebebi buydu.
        const int Iterations = 4;
        const int Radius = 2;

        // TÖRPÜ YALNIZ DAĞA UYGULANIYOR. Ovada da çalışıyordu ve tepecikleri siliyordu:
        // pencere yarıçapı 2 örnek, yani 21 metre — ovanın en ince tepeciğinin tam boyu.
        // O boyuttaki bir kabartı pencere içinde tamamen "fazlalık" sayılıyor ve dört
        // turda 0.45^4 = %4'e iniyordu. Ölçüldü: ova katmanı ±5 metre üretiyor, araziye
        // 1.7 metre olarak varıyordu.
        //
        // Törpünün işi ızgaraya çapraz keskin sırtların merdivenleşmesini önlemek; o
        // sorun dağın dik yüzlerinde var, düzlükte yok.
        float centre = (res - 1) * 0.5f;
        float skirt = settings.mountainRadius * (res - 1);

        var next = new float[res, res];

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            System.Array.Copy(heights, next, heights.Length);

            // SATIR SATIR PARALEL. Dört tur, hücre başına yirmi beş örnek, dört bin
            // kare ızgara: bir buçuk milyar okuma. Her satır yalnız `heights`ten okuyup
            // `next`e yazıyor, yani bölünmesi güvenli.
            System.Threading.Tasks.Parallel.For(Radius, res - Radius, z =>
            {
            for (int x = Radius; x < res - Radius; x++)
            {
                float sum = 0f;

                for (int dz = -Radius; dz <= Radius; dz++)
                for (int dx = -Radius; dx <= Radius; dx++)
                    sum += heights[z + dz, x + dx];

                float mean = (sum - heights[z, x]) / 24f;

                float excess = heights[z, x] - mean;
                if (excess <= 0f) continue;

                float offsetX = x - centre, offsetZ = z - centre;
                float distance = Mathf.Sqrt(offsetX * offsetX + offsetZ * offsetZ);

                // Etek çizgisinin dışında sıfıra iniyor; geçiş bandı ovaya taşmasın
                // diye dar tutuldu.
                float strength = settings.crestSoftening
                    * (1f - Mathf.SmoothStep(skirt * 0.92f, skirt * 1.08f, distance));

                if (strength > 0f) next[z, x] = heights[z, x] - excess * strength;
            }
            });

            System.Array.Copy(next, heights, heights.Length);
        }
    }

    /// Termal erozyon: talus açısını aşan yamaçlardaki malzeme aşağı komşulara akar.
    /// Keskin kırıklar moloz yamacına döner, büyük ölçekli form korunur.
    void Erode(float[,] heights, int res)
    {
        if (settings.erosionIterations <= 0) return;

        float cellSize = settings.terrainSize / (res - 1f);

        // Talus açısının normalize yükseklik cinsinden karşılığı: iki komşu arasında
        // taşınmadan durabilen en büyük fark
        float maxDelta = Mathf.Tan(settings.talusAngle * Mathf.Deg2Rad)
                         * cellSize / settings.terrainHeight;

        var delta = new float[res, res];

        for (int iteration = 0; iteration < settings.erosionIterations; iteration++)
        {
            System.Array.Clear(delta, 0, delta.Length);

            // EROZYON PARALEL DEĞİL. Denendi ve geri alındı: her hücre komşu SATIRLARA
            // da yazıyor (`delta[z-1, x]`, `delta[z+1, x]`), yani satırlar bağımsız
            // değil. Bölünürse iki iş parçacığı aynı hücreyi aynı anda güncelliyor ve
            // taşınan malzemenin bir kısmı kayboluyor — sessiz, yerel, tekrarlanamayan
            // bir bozulma.
            for (int z = 1; z < res - 1; z++)
            for (int x = 1; x < res - 1; x++)
            {
                float h = heights[z, x];

                // Dört komşuya bak; talus açısını aşan farkların toplamını ölç
                float e0 = Excess(h, heights[z, x - 1], maxDelta);
                float e1 = Excess(h, heights[z, x + 1], maxDelta);
                float e2 = Excess(h, heights[z - 1, x], maxDelta);
                float e3 = Excess(h, heights[z + 1, x], maxDelta);
                float excess = e0 + e1 + e2 + e3;

                if (excess <= 0f) continue;

                // Taşan malzemeyi eğimle orantılı dağıt
                float moved = Mathf.Min(excess, (h - LowestNeighbour(heights, x, z)) * 0.5f)
                              * settings.erosionRate;
                if (moved <= 0f) continue;

                delta[z, x] -= moved;
                delta[z, x - 1] += moved * (e0 / excess);
                delta[z, x + 1] += moved * (e1 / excess);
                delta[z - 1, x] += moved * (e2 / excess);
                delta[z + 1, x] += moved * (e3 / excess);
            }

            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                heights[z, x] = Mathf.Clamp01(heights[z, x] + delta[z, x]);
        }
    }

    static float Excess(float h, float neighbour, float maxDelta)
    {
        float difference = h - neighbour - maxDelta;
        return difference > 0f ? difference : 0f;
    }

    static float LowestNeighbour(float[,] heights, int x, int z)
    {
        float lowest = heights[z, x - 1];
        if (heights[z, x + 1] < lowest) lowest = heights[z, x + 1];
        if (heights[z - 1, x] < lowest) lowest = heights[z - 1, x];
        if (heights[z + 1, x] < lowest) lowest = heights[z + 1, x];
        return lowest;
    }

    /// Bozuk değer araziye tek örneklik çukurlar olarak yansır ve fark edilmesi zordur.
    /// Sessizce geçmesin diye üretim sonrası doğrulanır.
    static void VerifyFinite(float[,] heights, int res)
    {
        for (int z = 0; z < res; z++)
        for (int x = 0; x < res; x++)
        {
            float h = heights[z, x];
            if (float.IsNaN(h) || float.IsInfinity(h))
                throw new System.InvalidOperationException(
                    $"{nameof(MountainGenerator)}: yükseklik haritasında geçersiz değer ({x}, {z}) = {h}");
        }
    }

    /// Mevcut araziyi yeniden üretmeden ölçer.
    public void Measure()
    {
        var data = GetComponent<Terrain>().terrainData;
        int res = data.heightmapResolution;

        effectiveOctaves = MaxOctavesFor(res);
        ComputeSlopeStats(data.GetHeights(0, 0, res, res), res);
    }

    /// Örnekleme hızının üstündeki gürültü aliasing üretir: tek örneklik rastgele
    /// sıçramalar, yani benek ve çukur. En ince dalgaboyu en az bu kadar örnek geniş olmalı.
    const float MinSamplesPerWavelength = 4f;

    const int OctaveCeiling = 12;

    int MaxOctavesFor(int resolution)
    {
        float sampleSize = settings.terrainSize / (resolution - 1f);
        float minWavelength = MinSamplesPerWavelength * sampleSize;

        // Oktav i'nin dalgaboyu: terrainSize / (baseFrequency * lacunarity^i)
        float ratio = settings.terrainSize / (settings.baseFrequency * minWavelength);
        if (ratio <= 1f) return 1;

        int limit = Mathf.FloorToInt(Mathf.Log(ratio) / Mathf.Log(settings.lacunarity)) + 1;
        return Mathf.Clamp(limit, 1, OctaveCeiling);
    }

    void InitRandomState()
    {
        var rng = new System.Random(settings.seed);

        warpOffsetA = RandomOffset(rng);
        warpOffsetB = RandomOffset(rng);
        warpDetailOffsetA = RandomOffset(rng);
        warpDetailOffsetB = RandomOffset(rng);
        radialOffset = RandomOffset(rng);
        terraceOffset = RandomOffset(rng);
        gridOffset = RandomOffset(rng);

        octaveOffsets = new Vector2[OctaveCeiling];
        for (int i = 0; i < OctaveCeiling; i++)
            octaveOffsets[i] = RandomOffset(rng);

        InitPeaks(rng);
    }

    /// Yan tepeler ana zirvenin çevresine dağıtılır; omuz ve ikincil doruk hissi verir
    void InitPeaks(System.Random rng)
    {
        peaks = new Peak[settings.secondaryPeaks];
        float angleStep = Mathf.PI * 2f / Mathf.Max(1, settings.secondaryPeaks);

        for (int i = 0; i < peaks.Length; i++)
        {
            // Eşit aralıklı taban açı + rastgele sapma: kümelenme de olsun, boşluk da
            float angle = angleStep * (i + (float)rng.NextDouble() * 0.7f - 0.35f);
            float distance = settings.mountainRadius * settings.peakSpread
                             * (0.6f + (float)rng.NextDouble() * 0.8f);

            peaks[i] = new Peak
            {
                center = new Vector2(0.5f + Mathf.Cos(angle) * distance, 0.5f + Mathf.Sin(angle) * distance),
                radius = settings.mountainRadius * Mathf.Lerp(
                    settings.peakRadiusRange.x, settings.peakRadiusRange.y, (float)rng.NextDouble()),
                height = Mathf.Lerp(
                    settings.peakHeightRange.x, settings.peakHeightRange.y, (float)rng.NextDouble())
            };
        }
    }

    static Vector2 RandomOffset(System.Random rng)
        => new((float)rng.NextDouble() * 10000f, (float)rng.NextDouble() * 10000f);

    /// PROFİL EĞRİSİ DİZİYE PİŞİYOR. `AnimationCurve.Evaluate` iş parçacığı güvenli
    /// değil: içinde son aranan anahtarı önbelleğe alıyor ve iki iş parçacığı aynı anda
    /// çağırınca yanlış değer dönebiliyor. Üretim paralelleştirilince bu bir kilitlenme
    /// değil, sessiz bozulma olurdu — arazide rastgele yerlerde yanlış yükseklik.
    ///
    /// İki bin örnek, eğrinin kendi çözünürlüğünün çok üstünde; ayrıca dizi okuması
    /// eğri değerlendirmesinden birkaç kat hızlı.
    const int ProfileLutSize = 2048;
    float[] profileLut;

    void BakeProfileLut()
    {
        profileLut = new float[ProfileLutSize];
        for (int i = 0; i < ProfileLutSize; i++)
            profileLut[i] = settings.heightProfile.Evaluate(i / (ProfileLutSize - 1f));
    }

    /// Pişmiş profil eğrisinden okuma, ara değerlemeli.
    float ProfileAt(float t)
    {
        float x = Mathf.Clamp01(t) * (ProfileLutSize - 1);
        int i = (int)x;
        int j = Mathf.Min(i + 1, ProfileLutSize - 1);
        return Mathf.Lerp(profileLut[i], profileLut[j], x - i);
    }

    float SampleHeight(float u, float v)
    {
        // Domain warp: koordinatları bozarak simetriyi kırar, doğal sırtlar verir
        float wx = Mathf.PerlinNoise(u * settings.warpFrequency + warpOffsetA.x,
                                     v * settings.warpFrequency + warpOffsetA.y) - 0.5f;
        float wz = Mathf.PerlinNoise(u * settings.warpFrequency + warpOffsetB.x,
                                     v * settings.warpFrequency + warpOffsetB.y) - 0.5f;

        float dx2 = Mathf.PerlinNoise(u * settings.warpDetailFrequency + warpDetailOffsetA.x,
                                      v * settings.warpDetailFrequency + warpDetailOffsetA.y) - 0.5f;
        float dz2 = Mathf.PerlinNoise(u * settings.warpDetailFrequency + warpDetailOffsetB.x,
                                      v * settings.warpDetailFrequency + warpDetailOffsetB.y) - 0.5f;

        float su = u + wx * settings.warpStrength + dx2 * settings.warpDetailStrength;
        float sv = v + wz * settings.warpStrength + dz2 * settings.warpDetailStrength;

        float profile = MainProfile(su, sv);

        foreach (var peak in peaks)
        {
            float d = Vector2.Distance(new Vector2(su, sv), peak.center) / Mathf.Max(0.001f, peak.radius);
            if (d >= 1f) continue;

            float contribution = ProfileAt(d) * peak.height;
            profile = Mathf.Max(profile, contribution);
        }

        RidgedFbm(su, sv, out float low, out float detail);

        // Sırt etkisi yükseklikle güçlenir: etek yumuşak kalır, zirve sivrilir
        float influence = settings.ridgeInfluence
                          * Mathf.Lerp(settings.ridgeFootDamping, 1f, profile);

        // Çarpanın ortalaması 1'de tutulur; sırt gürültüsü dağı sistematik olarak alçaltmaz
        float h = profile * (1f + influence * (low - 0.5f));

        // Teras yalnızca düşük frekanslı ana forma uygulanır. İnce detay kuantalanırsa
        // gürültü bant sınırlarını geçtiği her yerde tek örneklik çukurlar oluşur.
        h = ApplyTerraces(h, su, sv, profile);
        h += profile * influence * detail;

        h = ApplySummitPlateau(h);

        h = settings.baseHeight + h * (1f - settings.baseHeight);

        // OVA en son ekleniyor: genlikleri gerçek metre cinsinden verilmiş ve taban
        // ölçeklemesinden geçerse küçülürler.
        h += Foreland(su, sv, profile);

        return Mathf.Clamp01(h);
    }

    /// DAĞIN ÖNÜ. Arazi üreteci radyal bir dağ yapıyor ve yarıçapın dışında profil
    /// sıfıra iniyor: geriye dümdüz bir tabla kalıyordu.
    ///
    /// TEK GÜRÜLTÜ DEĞİL, BEŞ ARAZİ TÜRÜ. Önceki sürüm her yere aynı gürültüyü uygulayıp
    /// genliğini yer yer değiştiriyordu; sonuç "her yer birbirine benziyor" oldu ve
    /// haklıydı: karakteri değişmeyen bir alanın genliğini oynatmak farklı yer üretmiyor,
    /// aynı yerin yüksek ve alçak hâlini üretiyor.
    ///
    /// Gerçek bir dağ önü bölgelere ayrılır ve bölgeler BİRBİRİNE BENZEMEZ:
    ///   moren tarlası - kaotik höyükler, kapalı çukurlar, yönsüz
    ///   sel ovası     - neredeyse düz, örgülü sığ yataklar
    ///   teraslar      - basamaklı düzlükler, aralarında kısa dik yükseltiler
    ///   oyuk yamacı   - sık paralel dereler, keskin sırtlar
    ///   blok alanı    - kaya düşüğü önü, iri ve düzensiz
    ///
    /// Hangi türün nerede olduğu düşük frekanslı bir alandan geliyor (~2200 m), yani
    /// beş kilometrelik bir rota iki üç bölge geçiyor. Sınırlar bükülmüş: keskin daireler
    /// yapay okunuyor.
    ///
    /// ALT SINIR 20 METRE. Arazi ızgarası 4.28 m/örnek ve bir özellik en az dört beş
    /// örnek istiyor. Kaya, blok, taş yığını gibi metre ölçeğindeki her şey buradan
    /// çıkamaz; onlar ayrı model olarak gelir.
    float Foreland(float su, float sv, float profile)
    {
        // Dağın eteğine yaklaşınca sönüyor: orada arazi zaten dağın kendi formundan
        // geliyor ve iki kaynak üst üste binerse etek kabarcıklanıyor.
        float outside = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(profile / 0.12f));
        if (outside <= 0.001f) return 0f;

        float dx = su - 0.5f;
        float dz = sv - 0.5f;
        float radius = Mathf.Sqrt(dx * dx + dz * dz);

        // Bölge sınırları bükülüyor: bükülmemiş düşük frekanslı gürültü yuvarlak
        // lekeler veriyor ve geçişler daire yayı gibi okunuyor.
        float warpU = (SignedNoise(su * Frequency(900f) + 3.7f,
                                         sv * Frequency(900f) + 8.1f) * 0.5f) * 0.06f;
        float warpV = (SignedNoise(su * Frequency(900f) + 51.2f,
                                         sv * Frequency(900f) + 27.4f) * 0.5f) * 0.06f;

        float ru = su + warpU;
        float rv = sv + warpV;

        // ALÜVYON YELPAZESİ her bölgede var: dereler malzemeyi aşağı taşır, ova etekten
        // dışarı doğru alçalır. Bir tür değil, zeminin genel eğilimi.
        float beyond = Mathf.Max(0f, radius - settings.mountainRadius);
        float metres = -beyond * settings.forelandFanDrop
                     / Mathf.Max(0.01f, 0.707f - settings.mountainRadius);

        // Tür ağırlıkları: her tür kendi düşük frekanslı alanına sahip, en yükseği
        // baskın çıkıyor. Üs 5'ti ve kazanan payın ancak %70'ini alıyordu: kalan %30
        // öteki dört türe dağılıp birbirini götürüyor, bölge karakteri siliniyordu.
        // Onuncu kuvvette kazananın payı %95'e çıkıyor, sınırlar yine yumuşak.
        Span<float> landformWeights = stackalloc float[LandformCount];

        float total = 0f;
        for (int i = 0; i < LandformCount; i++)
        {
            float field = UnitNoise(ru * Frequency(2200f) + i * 37.13f + 9.4f,
                                            rv * Frequency(2200f) + i * 21.77f + 5.8f);
            landformWeights[i] = Mathf.Pow(Mathf.Clamp01(field), 10f);
            total += landformWeights[i];
        }

        if (total < 1e-5f) { landformWeights[0] = 1f; total = 1f; }

        metres += landformWeights[0] / total * MoraineField(ru, rv)
                + landformWeights[1] / total * OutwashPlain(ru, rv)
                + landformWeights[2] / total * Terraces(ru, rv)
                + landformWeights[3] / total * GullySlope(ru, rv)
                + landformWeights[4] / total * BoulderApron(ru, rv);

        // ORTAK TEPECİK KATMANI. Bölge türü ne olursa olsun zemin pürüzlü: sel ovasında
        // çakıl barı, terasta tümsek, moren tarlasında höyük. Ölçüldü — bu katman
        // olmadan sel ovası ve teras bölgelerinde 60 metrede yalnız 1.5 metre kabartı
        // kalıyor ve tepeler 160 metre arayla düşüyor; yürüyen için orası düzlük.
        //
        // Dalga boyları 34 ve 21 metre: yanından geçilen, üstüne çıkılmayan boyut.
        // Yirmi metrenin altına inilmiyor, arazi ızgarası (4.28 m) çözemiyor.
        metres += Bumps(ru, rv);

        return metres / Mathf.Max(1f, settings.terrainHeight) * outside;
    }

    /// TEPECİKLER. Her arazi türünün üstüne binen ortak pürüz. Yürürken yanından
    /// geçilen kabartılar; tırmanılacak engel değil, zeminin dokusu.
    ///
    /// Yoğunluk YAMALI: gerçek arazi her yerde aynı pürüzlülükte değil, bir yamaç taşlı
    /// ve tümsekli, yanındaki çayır düz. Tek tip pürüz "gürültü uygulanmış düzlük"
    /// olarak okunuyor.
    float Bumps(float u, float v)
    {
        // Üç ölçek: 55, 32 ve 21 metre. Tek ölçekte tepecikler aynı boyda çıkıyor ve
        // "aynı damganın tekrarı" olarak okunuyor; üç ölçek üst üste binince büyüklük
        // dağılımı doğal oluyor - iri, orta ve küçük yan yana.
        float wide = SignedNoise(u * Frequency(95f) + 5.5f, v * Frequency(95f) + 9.2f);
        float mid = SignedNoise(u * Frequency(58f) + 44.1f, v * Frequency(58f) + 12.7f);
        float fine = SignedNoise(u * Frequency(37f) + 77.3f, v * Frequency(37f) + 51.8f);

        float patch = UnitNoise(u * Frequency(600f) + 88.4f, v * Frequency(600f) + 23.6f);
        float density = Mathf.Lerp(0.5f, 1.6f, Mathf.SmoothStep(0f, 1f, patch));

        return (wide * 1.6f + mid * 1.2f + fine * 0.8f)
             * density * settings.hummockHeight * 0.7f;
    }

    /// MOREN TARLASI. Buzulun bıraktığı kaotik höyükler ve aralarındaki kapalı çukurlar.
    /// Yönsüz: sırt yok, yay yok, sadece yığın. Yürürken en yorucu zemin - düz bir hat
    /// tutamazsın, sürekli inip çıkarsın.
    float MoraineField(float u, float v)
    {
        // SİNÜS YOK. Yaylar `sin(yarıçap / aralık)` ile üretiliyordu ve tanımı gereği
        // tekrar ediyordu: eşit aralıklı, eşit boylu sırtlar. Konsantrik bir desene
        // teğetten bakınca ufukta aynı üçgen tekrar tekrar görünüyor - testere dişi.
        // Büküm eklemek düzeni gizliyor, kaldırmıyor.
        //
        // Yerine sırt gürültüsü: iki farklı ölçekte, birbirine bakmayan iki alan.
        // Hiçbir sırt ötekinin aynı değil, aralıkları da eşit değil.
        float coarse = SignedNoise(u * Frequency(430f) + 11.7f,
                                   v * Frequency(430f) + 4.1f);
        float ridgeCoarse = Mathf.Pow(1f - Mathf.Abs(coarse), 2.5f);

        float mid = SignedNoise(u * Frequency(190f) + 63.2f,
                                v * Frequency(190f) + 28.5f);
        float ridgeMid = Mathf.Pow(1f - Mathf.Abs(mid), 3f);

        // İki sırt ailesi TOPLANMIYOR, en yükseği alınıyor: toplandığında kesiştikleri
        // yerde iki kat yükseliyor ve kesişme noktaları düzenli bir ızgara kuruyor.
        float ridges = Mathf.Max(ridgeCoarse, ridgeMid * 0.75f);

        // Höyükler: iki ölçek, yönsüz ve sık.
        float mound = SignedNoise(u * Frequency(88f) + 5.5f,
                                  v * Frequency(88f) + 9.2f) * 0.65f
                    + SignedNoise(u * Frequency(44f) + 44.1f,
                                  v * Frequency(44f) + 12.7f) * 0.4f;

        // KAPALI ÇUKURLAR (buzul kazanı): moren tarlasının imzası. Erimiş buz bloğunun
        // bıraktığı çanaklar; suyla dolarsa gölcük olur.
        float kettle = UnitNoise(u * Frequency(160f) + 71.9f,
                                 v * Frequency(160f) + 33.2f);
        float basin = -Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(kettle - 0.5f) * 2.6f), 3f) * 7f;

        // Sırt yüksekliği yerden yere değişiyor: kimi belirgin, kimi silinmiş.
        float relief = Mathf.Lerp(0.3f, 1.4f, UnitNoise(u * Frequency(520f) + 45.9f,
                                                        v * Frequency(520f) + 12.1f));

        return ridges * relief * settings.moraineHeight
             + mound * settings.hummockHeight + basin;
    }

    /// SEL OVASI. Buzul suyunun taşıdığı çakılın yaydığı düzlük: neredeyse dümdüz,
    /// üstünde örgülü sığ yataklar. Hızlı yürünen, açık, sade olması gereken zemin -
    /// kolay rotanın karakteri bu.
    float OutwashPlain(float u, float v)
    {
        // Örgülü yataklar: sığ ve geniş, birbirine karışıyor. Derinlik bir metrenin
        // altında; buradan geçmek yavaşlatmaz, sadece zemin düz olmaktan çıkar.
        float braid = UnitNoise(u * Frequency(120f) + 17.3f,
                                        v * Frequency(120f) + 61.5f);
        float cut = -Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(braid - 0.5f) * 2f), 3f) * 1.6f;

        float ripple = (SignedNoise(u * Frequency(52f) + 2.9f,
                                          v * Frequency(52f) + 8.3f) * 0.5f) * 0.9f;

        return cut + ripple;
    }

    /// TERASLAR. Eski dere seviyelerinin bıraktığı basamaklı düzlükler: geniş düz
    /// alanlar, aralarında kısa ve dik yükseltiler. Kamp kurulacak yerler bunlar;
    /// yürüyüş kolay ama basamağı bulmak gerekiyor.
    float Terraces(float u, float v)
    {
        // EŞİT BASAMAK YOK. `floor(x * 5)` ile kuantalanıyordu: beş basamak, her biri
        // 5.2 metre, hepsi birbirinin aynı. Doğada teras yükseklikleri dereye, zamana
        // ve malzemeye göre değişir; eşit basamak merdiven olarak okunuyor.
        //
        // Basamak kenarları artık bir gürültünün kendi eşiklerinden geçtiği yerlerde:
        // yükseklikler de aralıklar da düzensiz.
        float field = UnitNoise(u * Frequency(1400f) + 13.1f,
                                v * Frequency(1400f) + 47.6f);

        // Düzlükler: alanı kendi gradyanına göre bastırmak yerine, yumuşak bir
        // basamak fonksiyonundan geçiriyorum. Eşik yerden yere kayıyor, yani iki
        // düzlük arası mesafe sabit değil.
        float shift = UnitNoise(u * Frequency(760f) + 5.1f,
                                v * Frequency(760f) + 88.3f);

        // Dört eşik, konumu ve yüksekliği ayrı ayrı kaydırılmış.
        float terrace = 0f;
        terrace += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.22f + shift * 0.10f,
                                                              0.30f + shift * 0.10f, field)) * 9f;
        terrace += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.44f - shift * 0.08f,
                                                              0.49f - shift * 0.08f, field)) * 6f;
        terrace += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.63f + shift * 0.12f,
                                                              0.72f + shift * 0.12f, field)) * 12f;
        terrace += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.84f - shift * 0.06f,
                                                              0.88f - shift * 0.06f, field)) * 5f;

        // Düzlükler tam düz değil: ince pürüz kalıyor.
        float grain = SignedNoise(u * Frequency(46f) + 6.2f,
                                  v * Frequency(46f) + 19.9f) * 0.55f;

        return terrace + grain;
    }

    /// OYUK YAMACI. Sık, paralel dere yatakları ve aralarındaki keskin sırtlar. Yatağa
    /// inip çıkmak yavaşlatır; boyunca gitmek hızlıdır ama yönü arazi dayatır.
    float GullySlope(float u, float v)
    {
        float gully = UnitNoise(u * Frequency(260f) + 71.2f,
                                        v * Frequency(260f) + 3.4f);

        float ridge = 1f - Mathf.Abs(gully - 0.5f) * 2f;

        // Yatak tabanlı, kenarları dik: altıncı kuvvet tabana genişlik bırakıyor.
        float cut = -Mathf.Pow(Mathf.Clamp01(ridge), 4f) * settings.channelDepth;

        // Sırtların kendisi de yükseliyor: oyuk kazıldıkça arası sırt olarak kalıyor.
        float crest = Mathf.Pow(Mathf.Clamp01(1f - ridge), 2f) * 6f;

        return cut + crest;
    }

    /// BLOK ALANI. Yamaçtan dökülen iri malzemenin önü: düzensiz, iri taneli, yönsüz.
    /// En yavaş yürünen zemin. Metre ölçeğindeki blokların kendisi buradan çıkmaz;
    /// bu, onların üstünde durduğu dalgalı taban.
    float BoulderApron(float u, float v)
    {
        float lump = (SignedNoise(u * Frequency(95f) + 88.1f,
                                        v * Frequency(95f) + 14.6f) * 0.5f) * 1.4f
                   + (SignedNoise(u * Frequency(41f) + 39.7f,
                                        v * Frequency(41f) + 71.3f) * 0.5f) * 1.0f;

        // Yığın önü dağdan uzaklaştıkça alçalır: kaynağa yakın kalın, ucunda incelir.
        float taper = UnitNoise(u * Frequency(700f) + 4.4f,
                                        v * Frequency(700f) + 9.9f);

        return lump * settings.hummockHeight * Mathf.Lerp(0.6f, 1.8f, taper);
    }

    /// Frekans = arazi boyu / istenen dalga boyu. Sayıyı doğrudan yazmak yerine
    /// metreden türetmek, dağın boyu değiştiğinde özelliklerin gerçek boyunu koruyor.
    /// Alt sınır 20 m: arazi ızgarası 4.28 m/örnek ve daha ince olan örtüşür.
    /// İŞARETLİ GÜRÜLTÜ, -1 ile 1 arası. `Mathf.PerlinNoise` teorik olarak 0-1 döndürüyor
    /// ama kütlesi 0.30-0.70 arasında toplanıyor: `(n - 0.5)` yazınca elde edilen genlik
    /// beklenen ±0.5 değil, gerçekte ±0.22 oluyor ve her katman sessizce yarıya iniyor.
    ///
    /// Ölçek 2.2 o daralmayı geri açıyor, kırpma da uçlardaki nadir taşmayı kesiyor.
    /// Bu düzeltme olmadan "5 metrelik tepecik" araziye 1.4 metre olarak iniyordu.
    static float SignedNoise(float x, float y) =>
        Mathf.Clamp(Mathf.PerlinNoise(x, y) * 2.2f - 1.1f, -1f, 1f);

    /// Aynı düzeltmenin 0-1 aralığındaki hâli. Eşik ve maske hesapları bunu okuyor:
    /// daralmış bir dağılıma eşik koymak, eşiği fiilen aralığın dışına atıyordu.
    static float UnitNoise(float x, float y) =>
        Mathf.Clamp01(Mathf.PerlinNoise(x, y) * 2.2f - 0.6f);

    /// Arazi türü sayısı. Ağırlıklar YIĞINDA tutuluyor: paylaşılan bir alan olsaydı
    /// paralel üretimde iş parçacıkları birbirinin ağırlıklarını ezerdi.
    const int LandformCount = 5;

    float Frequency(float wavelength) =>
        settings.terrainSize / Mathf.Max(36f, wavelength);

    /// Ana koninin profili. Taban daire olursa eş-yükseklik çizgileri de daire olur ve
    /// teraslar iç içe halka gibi görünür — yarıçap açıya göre bozulur.
    float MainProfile(float su, float sv)
    {
        float dx = su - 0.5f;
        float dz = sv - 0.5f;
        float radius = Mathf.Sqrt(dx * dx + dz * dz);

        float angle = Mathf.Atan2(dz, dx);
        float angularNoise = Mathf.PerlinNoise(
            Mathf.Cos(angle) * settings.radialFrequency + radialOffset.x,
            Mathf.Sin(angle) * settings.radialFrequency + radialOffset.y) - 0.5f;

        float effective = settings.mountainRadius * (1f + angularNoise * settings.radialDistortion * 2f);
        float dist = Mathf.Clamp01(radius / Mathf.Max(0.01f, effective));

        return Mathf.Max(0f, ProfileAt(dist));
    }

    float ApplyTerraces(float h, float su, float sv, float profile)
    {
        // Dağın dışında teras yok. Izgara kayması yükseklik sıfırken bile değer ürettiği
        // için düz araziyi çukurlaştırıyordu.
        if (profile <= 0.001f) return h;

        // Etekte hızla tam güce ulaşsın; yalnızca dağın bittiği yerde sönsün
        float footFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(profile / 0.08f));

        // Teras gücü yere göre değişir: bir yamaçta belirgin sahanlıklar, başka yamaçta düz eğim
        float variation = footFade * Mathf.Lerp(1f, Mathf.PerlinNoise(
            su * settings.terraceVariationFrequency + terraceOffset.x,
            sv * settings.terraceVariationFrequency + terraceOffset.y), settings.terraceVariation);

        // Bant kotları yere göre kayar; sabit kot halka deseni üretiyor
        float offset = (Mathf.PerlinNoise(
            su * settings.terraceOffsetFrequency + gridOffset.x,
            sv * settings.terraceOffsetFrequency + gridOffset.y) - 0.5f) * settings.terraceOffsetAmount;

        h = Terrace(h, settings.coarseTerraceBands, settings.coarseTerraceStrength * variation, offset);
        h = Terrace(h, settings.fineTerraceBands, settings.fineTerraceStrength * variation, offset * 3f);

        return h;
    }

    /// Yüksekliği basamaklara böler. Izgara kaydırıldığı için bantlar her yerde
    /// aynı kotta oluşmaz — gerçek kaya bantları gibi yamaçtan yamaca kayar.
    float Terrace(float h, int bands, float strength, float offset)
    {
        if (strength <= 0f) return h;

        float shift = offset / bands;
        float t = (h + shift) * bands;
        float band = Mathf.Floor(t);
        float frac = Mathf.Clamp01(t - band);
        float stepped = (band + Mathf.Pow(frac, settings.terraceSharpness)) / bands - shift;

        return Mathf.Lerp(h, stepped, strength);
    }

    float ApplySummitPlateau(float h)
    {
        float start = settings.summitPlateauStart;
        if (start >= 1f || h <= start) return h;

        return start + (h - start) * (1f - settings.summitFlatness);
    }

    /// Ridged multifractal: perlin'in mutlak değeri ters çevrilir, keskin sırtlar oluşur.
    /// <paramref name="low"/> yalnızca ilk oktavlar (0-1 aralığında) — teras buna uygulanır.
    /// <paramref name="detail"/> kalan yüksek frekanslı oktavlar, ortalaması sıfıra yakın.
    void RidgedFbm(float u, float v, out float low, out float detail)
    {
        float norm = 0f;
        float lowSum = 0f, lowNorm = 0f, highSum = 0f;
        float amp = 1f;
        float freq = settings.baseFrequency;

        int count = Mathf.Clamp(effectiveOctaves, 1, octaveOffsets.Length);

        // Teras yalnızca kaba yarıya uygulanır; ince detay kuantalanırsa benek oluşur
        int split = Mathf.Clamp(count / 2, 1, count);

        for (int i = 0; i < count; i++)
        {
            float n = Mathf.PerlinNoise(u * freq + octaveOffsets[i].x, v * freq + octaveOffsets[i].y);

            // PerlinNoise nadiren 0-1 dışına taşar; taban negatife düşerse kesirli üs NaN üretir
            n = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Abs(n * 2f - 1f)), settings.ridgeSharpness);

            norm += amp;

            if (i < split)
            {
                lowSum += n * amp;
                lowNorm += amp;
            }
            else
            {
                highSum += (n - 0.5f) * amp;
            }

            amp *= settings.gain;
            freq *= settings.lacunarity;
        }

        low = lowSum / lowNorm;
        detail = highSum / norm;
    }

    /// Eğim bütçesi. Alan ağırlıklı tek bir histogram dağın geniş eteği tarafından
    /// domine edildiği için ölçüm yükseklik kuşaklarına bölünür.
    void ComputeSlopeStats(float[,] heights, int res)
    {
        float cellSize = settings.terrainSize / (res - 1f);
        float inv = 1f / (res - 1f);

        var counts = new int[AltitudeBandCount, 4];
        var totals = new int[AltitudeBandCount];
        var sums = new double[AltitudeBandCount];

        double allSum = 0;
        int allCount = 0;
        float highest = 0f;

        for (int z = 0; z < res - 1; z++)
        {
            float dv = z * inv - 0.5f;

            for (int x = 0; x < res - 1; x++)
            {
                // Dağın dışındaki düz arazi ölçüme girmemeli, yürünebilir oranını şişirir
                float du = x * inv - 0.5f;
                if (Mathf.Sqrt(du * du + dv * dv) > settings.mountainRadius) continue;

                float dhx = (heights[z, x + 1] - heights[z, x]) * settings.terrainHeight;
                float dhz = (heights[z + 1, x] - heights[z, x]) * settings.terrainHeight;
                float grad = Mathf.Sqrt(dhx * dhx + dhz * dhz) / cellSize;
                float deg = Mathf.Atan(grad) * Mathf.Rad2Deg;

                if (heights[z, x] > highest) highest = heights[z, x];

                int band = Mathf.Clamp(
                    (int)(heights[z, x] * AltitudeBandCount), 0, AltitudeBandCount - 1);

                counts[band, deg < 30f ? 0 : deg < 45f ? 1 : deg < 70f ? 2 : 3]++;
                totals[band]++;
                sums[band] += deg;

                allSum += deg;
                allCount++;
            }
        }

        if (bands == null || bands.Length != AltitudeBandCount)
            bands = new SlopeBand[AltitudeBandCount];

        for (int b = 0; b < AltitudeBandCount; b++)
        {
            if (totals[b] == 0)
            {
                bands[b] = default;
                continue;
            }

            float scale = 100f / totals[b];
            bands[b] = new SlopeBand
            {
                walkable = counts[b, 0] * scale,
                strenuous = counts[b, 1] * scale,
                climbable = counts[b, 2] * scale,
                wall = counts[b, 3] * scale,
                meanDegrees = (float)(sums[b] / totals[b])
            };
        }

        meanSlopeDegrees = allCount > 0 ? (float)(allSum / allCount) : 0f;
        peakAltitude = highest * settings.terrainHeight;
    }
}
