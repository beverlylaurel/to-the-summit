// ROL: kar sisteminin VFX Graph asset'lerini KODDAN üretir (spec Faz 8, 9, 13).
// Çağıran: menü.

using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// VFX GRAFİKLERİ KODDAN KURULUYOR.
///
/// Spec Faz 8/9/13 altı `.vfx` asset'i istiyor. `.vfx` bir grafik asset'i;
/// normalde Unity içinde elle çizilir. Bu projede kullanıcı tıklamaz — kod,
/// dosya, ayar hepsi buradan (`CLAUDE.md` → Rol dağılımı).
///
/// GRAFİK MODELİ `internal`, O YÜZDEN REFLECTION. `UnityEditor.VFX` altındaki
/// sınıflar (`VFXGraph`, `VFXContext`, `VFXBlock`, ...) dışarıya kapalı.
/// Erişilebilir olduğu ÖLÇÜLDÜ (`SnowVfxApiProbe`): 70 somut blok tipi,
/// 24 bağlam tipi, `AddChild` ve `LinkTo` yerinde, blok örneklenebiliyor.
///
/// Reflection kırılgan: Unity sürümü değişirse tip veya imza kayabilir. O yüzden
/// her arama BAŞARISIZLIĞI FIRLATIYOR — sessizce yarım grafik üretmektense hiç
/// üretmemek doğru. Yarım grafik ekranda "kar yağmıyor" olarak görünür ve
/// sebebi günler sürer.
public static class SnowVfxBuilder
{
    const string Folder = "Assets/Snow/VFX";
    const string EditorAsm = "Unity.VisualEffectGraph.Editor";

    [MenuItem("To The Summit/Snow/Generate VFX Graphs", false, 62)]
    static void BuildAll()
    {
        var r = new StringBuilder();
        r.AppendLine("# Kar — VFX grafikleri üretiliyor");

        try
        {
            BuildSnowfall(r);
            BuildPuff(r);
            BuildSpray(r);
            BuildSpindrift(r);
            BuildCurtain(r);
        }
        catch (Exception e)
        {
            Debug.LogError(r + "\nÜRETİM DURDU: " + e.GetType().Name + " — " + e.Message +
                           "\n" + e.StackTrace);
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(r.ToString());
    }

    // ------------------------------------------------------------ VFX_Snowfall

    /// YAKIN KATMAN (spec §17.1). Ayrı ayrı görünen taneler; rüzgârda savrulur,
    /// örtü altında kesilir.
    ///
    /// Bu ilk grafik MEKANİZMAYI KANITLIYOR: asset açılıyor mu, bloklar duruyor
    /// mu, Unity yeniden açılınca bozulmuyor mu. Çalıştığı görülmeden kalan beş
    /// grafiğe geçilmiyor — biri yanlışsa altısını birden yazmış olmayalım.
    static void BuildSnowfall(StringBuilder r)
    {
        object graph = NewGraph("VFX_Snowfall", r);

        // --- Spawn: sabit oran. Oran runtime'da `_FlakeRate` ile sürülüyor
        //     (spec §17.3); grafikte yalnız blok duruyor.
        object spawner = AddContext(graph, "VFXBasicSpawner", new Vector2(0, 0), r);
        object rate = AddBlock(spawner, "VFXSpawnerConstantRate", r);

        // Oran runtime'da `SnowfallLayers`'dan geliyor (spec §17.3): VFX
        // yoğunluğu ve `_SnowfallSWERate` AYNI `i01`'den türüyor.
        object rateParam = AddParameter(graph, "SpawnRate", typeof(float), 0f,
                                        new Vector2(-300, 0), r);
        LinkParameter(rateParam, rate, "Rate", r);

        // --- Initialize
        object init = AddContext(graph, "VFXBasicInitialize", new Vector2(0, 200), r);

        // KAPASİTE SPEC'İN ÜSTÜNDE — YOĞUNLUK İÇİN.
        //
        // Spec §17.1 kapasiteyi 40000, kutuyu (40, 26, 40) veriyor: birlikte
        // `40000 / 41600 = 0.96` tane/m³. Gerçek yoğun kar 3–10 tane/m³ ve
        // kullanıcı "yağış 1 iken yeterli kar göremiyorum" dedi.
        //
        // ÖNCE KUTU KÜÇÜLTÜLDÜ, GERİ ALINDI. (24,20,24) ve (20,16,20) denendi;
        // yoğunluk kâğıtta yükseldi ama ekranda kar AZALDI: rüzgâr 12 m/s'de
        // tane 10 metreyi 0.85 saniyede geçiyor, dar kutuda kameranın
        // çevresinde hiç kalmıyor. Spec'in geniş kutusu tam bunun için.
        //
        // Kutu spec'te bırakıldı, yoğunluk kapasiteden alındı:
        //   120000 / 41600 = 2.88 tane/m³
        SetSetting(init, "capacity", 120000u, r);

        // DÜNYA UZAYI. Kutu oyuncuyu 1 m ızgarasında takip ediyor; yerel uzayda
        // her snap yaşayan 89 bin taneyi birlikte ışınlıyordu.
        SetWorldSpace(init, r);

        // Sınır kutusu spawn kutusunu ve rüzgârın taşıdığı payı kapsıyor.
        SetBounds(init, new Vector3(60f, 42f, 60f), r);

        // Spec §17.1: `Set Position (AABox)`, kutu (40, 26, 40), KUTUNUN
        // TAMAMINA spawn (yüzeyine değil).
        //
        // `PositionBox` bir ŞEKİL, blok değil (ölçüldü). Bloğu `PositionShape`;
        // şekli `shape` ayarında taşıyor.
        object pos = AddBlock(init, "Block.PositionShape", r);

        // Enum'da `Box` yok, `OrientedBox` var (ölçüldü — PositionShape.Type).
        SetSetting(pos, "shape", "OrientedBox", r);
        SetSetting(pos, "positionMode", "Volume", r);

        // KUTU SPEC'TEN KÜÇÜK — YOĞUNLUK İÇİN.
        //
        // Spec §17.1 kutuyu (40, 26, 40) ve kapasiteyi 40000 veriyor. İkisi
        // birlikte `40000 / 41600 = 0.96` tane/m³ ediyor; gerçek yoğun kar
        // yağışı 3–10 tane/m³. Kullanıcı "yağış 1 iken yeterli kar
        // göremiyorum" dedi ve ölçüm doğruladı.
        //
        // Kapasiteyi üçe katlamak yerine kutu küçültüldü: yoğunluk
        // `kapasite / hacim` olduğu için ikisi aynı sonucu veriyor, ama
        // küçük kutu BEDAVA — aynı 40000 tane daha dar hacimde.
        // Kutu SPEC'TE: (40, 26, 40). Küçültmek denendi ve geri alındı —
        // gerekçe yukarıda, kapasitenin yanında.
        SetSlotField(pos, "Box", "size", new Vector3(40f, 26f, 40f), r);

        // Ömür 4–9 s (spec §17.1).
        object life = AddBlock(init, "Block.SetAttribute", r);
        SetSetting(life, "attribute", "lifetime", r);
        SetSetting(life, "Random", "Uniform", r);
        SetSlot(life, "A", 4f, r);
        SetSlot(life, "B", 9f, r);

        // Boyut: taban 0.018 m, random 0.6–1.7× (spec §17.1).
        object size = AddBlock(init, "Block.SetAttribute", r);
        SetSetting(size, "attribute", "size", r);
        SetSetting(size, "Random", "Uniform", r);

        // Taban 0.018 m, random 0.6–1.7× (spec §17.1). Uçlar burada çarpılıyor;
        // shader'da ikinci bir çarpan yok.
        SetSlot(size, "A", 0.018f * 0.6f, r);
        SetSlot(size, "B", 0.018f * 1.7f, r);

        // TERMİNAL HIZ (spec §17.1). Bu blok olmadan tane hiç düşmüyor:
        // türbülans onu havada savuruyor ama aşağı taşıyan bir şey yok —
        // ölçüldü, kar havada asılı kaldı.
        //
        // Spec: `terminalVel = lerp(random(0.6, 1.4), random(1.4, 3.0),
        // _SnowWetness)`. Islaklık sıcaklıktan geliyordu ve yağış sıcaklıktan
        // koparıldığında sabit 0'a düştü — tane her zaman kuru, yani
        // `random(0.6, 1.4)`.
        object vel = AddBlock(init, "Block.SetAttribute", r);
        SetSetting(vel, "attribute", "velocity", r);
        SetSetting(vel, "Random", "Uniform", r);
        // SLOT TİPİ `Vector3` DEĞİL. `velocity` slotu `UnityEditor.VFX.Vector`
        // sarmalayıcısı; Vector3 yazınca değer sessizce sıfır kalıyordu
        // (ölçüldü — asset'te `{"vector":{"x":0,"y":0,"z":0}}`).
        SetSlotField(vel, "A", "vector", new Vector3(0f, -0.6f, 0f), r);
        SetSlotField(vel, "B", "vector", new Vector3(0f, -1.4f, 0f), r);

        // --- Update: türbülans (spec §17.1)
        object update = AddContext(graph, "VFXBasicUpdate", new Vector2(0, 500), r);

        // TERMİNAL HIZ FİZİKTEN ÇIKIYOR, DAYATILMIYOR.
        //
        // İlk deneme başlangıç hızını yazıp bırakıyordu; türbülans `Relative`
        // modda onu bir saniyede yiyordu (o mod hızı hedefe ÇEKİYOR, hedef de
        // ortalama sıfır). Belirti: kar düşmüyor, yukarı-aşağı-sağa-sola
        // savruluyor — ölçüldü.
        //
        // Yerçekimi + sürükleme dengesi terminal hızı kendisi veriyor:
        // `v = g / drag = 9.81 / 9.81 = 1 m/s`. Spec §17.1'in kuru kar için
        // istediği 0.6–1.4 m/s bandının ortası.
        //
        // Yüksek sürükleme aynı zamanda karın NEDEN savrulduğunu açıklıyor:
        // hafif tane rüzgâr hızına hızla yaklaşır. İki davranış tek katsayıdan.
        AddBlock(update, "Block.Gravity", r);

        object turb = AddBlock(update, "Block.Turbulence", r);

        // TÜRBÜLANS KUVVET, HIZ DEĞİL. `Relative` hızı ezerdi; `Absolute`
        // kuvvet ekliyor, sürükleme onu dengeliyor.
        SetSetting(turb, "Mode", "Absolute", r);

        // Spec §17.1: `Intensity = 0.35 * _WindSpeed + 0.15`. Rüzgâra bağlı
        // olduğu için parametreden sürülüyor; taban değer 0.15.
        object turbParam = AddParameter(graph, "TurbulenceIntensity", typeof(float),
                                        0.15f, new Vector2(-300, 500), r);
        LinkParameter(turbParam, turb, "Intensity", r);
        SetSlot(turb, "frequency", 0.12f, r);
        SetSlot(turb, "octaves", 2, r);

        // Sürükleme kendi bloğunda: türbülansın `Drag` slotu yalnız `Relative`
        // modda anlamlı, `Absolute`'ta okunmuyor.
        // RÜZGÂR (spec §17.1: `Hız = _WindWS + float3(0, -terminalVel, 0)`).
        //
        // Hız dayatmak yerine KUVVET veriliyor, çünkü aşağıdaki sürükleme
        // zaten hızı sıfıra çekiyor: `F = wind * drag` dengesi tam
        // `velocity = wind` veriyor. Düşey eksende yerçekimi ayrı çalışıyor,
        // terminal hız bozulmuyor.
        //
        // Bu blok yoktu ve kar dimdik iniyordu — rüzgâr 13 m/s iken bile.
        object windForce = AddBlock(update, "Block.Force", r);
        SetSetting(windForce, "Mode", "Absolute", r);

        object windParam = AddParameter(graph, "WindForce", typeof(Vector3),
                                        Vector3.zero, new Vector2(-300, 620), r);
        LinkParameter(windParam, windForce, "Force", r);

        object drag = AddBlock(update, "Block.Drag", r);
        SetSlot(drag, "dragCoefficient", 9.81f, r);

        // --- Output: URP Lit Quad (spec §17.1)
        //
        // `VFXComposedParticleOutput` YETMİYOR: URP 17.5'te tek gölgeleme
        // seçeneği ShaderGraph (ölçüldü — somut `ParticleShading` tek). Spec
        // "Output Particle Lit Quad" istiyor; onun karşılığı URP paketindeki
        // `VFXURPLitPlanarPrimitiveOutput`, `primitiveType` varsayılanı Quad.
        object output = AddContext(graph, "URP.VFXURPLitPlanarPrimitiveOutput",
                                   new Vector2(0, 800), r);

        // Spec §17.1: `Blend = Alpha`, `Depth Write = Off`, `Soft Particles = On`.
        SetSetting(output, "blendMode", "Alpha", r);
        SetSetting(output, "zWriteMode", "Off", r);
        SetSetting(output, "useSoftParticle", true, r);

        // TANE SİYAH ÇIKIYORDU. `Orient: Face Camera Plane` normali kameraya
        // çeviriyor; güneş yandan gelince N·L ≈ 0 ve Lit quad kararıyor.
        // Gerçek kar tanesi çok saçıcı, ışığı her yöne dağıtıyor — spec bunu
        // emissive ile karşılıyor (§17.1).
        // KAR TANESİ DOKUSU — 4×4 FLIPBOOK ATLASI (spec §17.1).
        //
        // Varsayılan `DefaultDot` yuvarlak bir noktaydı; kar tanesi değil.
        // Atlas zaten üretiliyordu (`SnowTextureBaker`, 256², 4×4, on altı
        // ayrı tane) ama hiçbir yerde kullanılmıyordu.
        //
        // `texIndex` spawn'da rastgele sabitleniyor: tane ömrü boyunca AYNI
        // kareyi gösteriyor. Animasyon değil çeşitlilik isteniyor.
        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Snow/Textures/T_Flake_Atlas.png");

        if (atlas != null)
        {
            SetSetting(output, "uvMode", "Flipbook", r);
            SetSlot(output, "baseColorMap", atlas, r);
            // `flipBookSize` Vector2 DEĞİL, `UnityEditor.VFX.FlipBook` (iki int).
            // Vector2 yazmak denendi ve `SetSlot` doğrulaması yakaladı.
            SetSlotField(output, "flipBookSize", "x", 4, r);
            SetSlotField(output, "flipBookSize", "y", 4, r);

            object texIdx = AddBlock(init, "Block.SetAttribute", r);
            SetSetting(texIdx, "attribute", "texIndex", r);
            SetSetting(texIdx, "Random", "Uniform", r);
            SetSlot(texIdx, "A", 0f, r);
            SetSlot(texIdx, "B", 15.999f, r);
        }
        else
        {
            r.AppendLine("           [!] T_Flake_Atlas yok — DefaultDot kalıyor");
        }

        SetSetting(output, "useEmissive", true, r);

        // EMISSIVE ANA IŞIKTAN, SABİT DEĞİL (spec §17.1:
        // `Emissive = _FlakeEmissive * mainLightColor * 0.04`).
        //
        // Sabit renk konmuştu ve tane GECE DE aynı parlıyordu. Renk artık
        // runtime'da `SnowfallLayers` tarafından ana ışıktan türetiliyor.
        object emisParam = AddParameter(graph, "FlakeEmissive", typeof(Color),
                                        new Color(0.55f, 0.60f, 0.70f),
                                        new Vector2(-300, 820), r);
        LinkParameter(emisParam, output, "emissiveColor", r);

        // Spec §17.1: `Metallic = 0`, `Smoothness = 0.2`.
        SetSlot(output, "smoothness", 0.2f, r);
        SetSlot(output, "metallic", 0f, r);

        // Yönelim: `Face Camera Plane` (spec §17.1). Varsayılan zaten bu;
        // yine de yazılıyor ki varsayılan değişirse sessizce kaymasın.
        object orient = AddBlock(output, "Block.Orient", r);
        SetSetting(orient, "mode", "FaceCameraPlane", r);

        // ASGARİ EKRAN BOYUTU (spec §17.1).
        //
        // Hazır `ScreenSpaceSize` bloğu bunu VERMİYOR: `SizeMode`
        // seçeneklerinin hiçbiri asgari değil, hepsi boyutu ZORLUYOR (paket
        // kaynağından okundu). `PixelAbsolute` denendi — yakındaki taneyi de
        // 1.3 piksele kilitledi, kar toz gibi göründü.
        //
        // Formül o bloğun KENDİ kodundan alındı; tek fark son iki satırda
        // atama yerine `max`. Sıfırdan yazılmadı: aynı işi yapan çalışan
        // ifade zaten paketin içindeydi.
        //
        //   scaleX = newScale       -> tane TAM 1.3 piksel  (blok böyle)
        //   scaleX = max(1, ...)    -> tane EN AZ 1.3 piksel (spec böyle)
        //
        // 1.3 koda gömülü: spec sabit veriyor, ayarlanabilir olması istenmiyor.
        // SALINIM (spec §17.1). Asgari ekran boyutundan ÖNCE: o blok
        // `attributes.position`'dan kamera uzaklığını okuyor, tane kaydıktan
        // sonraki konumu görsün.
        //
        // Türbülans bunun yerini tutmuyor — ikisi ayrı iş: türbülans havanın
        // ORTAK hareketi (rüzgâra bağlı), salınım tanenin KENDİ çırpınması
        // (rüzgârdan bağımsız, komşularıyla ilişkisiz).
        object flutter = AddBlock(output, "Block.CustomHLSL", r);
        SetSetting(flutter, "m_BlockName", "Salınım", r);
        SetSetting(flutter, "m_HLSLCode", FlutterHlsl, r);

        object minPx = AddBlock(output, "Block.CustomHLSL", r);
        SetSetting(minPx, "m_BlockName", "Asgari ekran boyutu", r);
        SetSetting(minPx, "m_HLSLCode", MinScreenSizeHlsl, r);

        // ÖMÜR UÇLARINDA SOLMA + ZEMİN KESME (spec §17.1).
        //
        // Fade yoksa tane birdenbire beliriyor ve birdenbire kayboluyor;
        // spec ömrün ilk ve son %8'inde alpha rampası istiyor.
        //
        // Zemin kesme olmadan tane yerin ALTINA iniyor ve kar yüzeyinin
        // içinden görünüyor.
        object fade = AddBlock(update, "Block.CustomHLSL", r);
        SetSetting(fade, "m_BlockName", "Ömür solması ve zemin kesme", r);
        SetSetting(fade, "m_HLSLCode", FadeAndGroundHlsl, r);

        object groundParam = AddParameter(graph, "GroundY", typeof(float), 0f,
                                          new Vector2(-300, 700), r);
        // CustomHLSL parametre slotlari `_` onekiyle aciliyor
        // (`CustomHLSL.parameterPrefix`); ondeksiz ad bulunamiyor.
        LinkParameter(groundParam, fade, "_groundY", r);

        Link(spawner, init, r);
        Link(init, update, r);
        Link(update, output, r);

        Save(graph, r);
    }

    // ------------------------------------------------------------- ortak iskelet

    /// Dört kısa ömürlü sistem aynı iskeleti paylaşıyor: spawn → init →
    /// update → çıktı. Tek fark ayarları; iskeleti kopyalamak dört yerde
    /// aynı hatayı yapmak olurdu.
    /// SINIR KUTUSU YAZILMAZSA SİSTEM HİÇ ÇİZİLMEZ.
    ///
    /// `VFXBasicInitialize.bounds` varsayılanı 1 m³. Unity o kutuyu frustum'a
    /// göre kırpıyor: parçacıklar doğsa bile `VFXRenderer.isVisible` false
    /// kalıyor ve sistem tamamen kayboluyor. Ölçüldü — kar yağmıyordu, sebep
    /// buydu (`SYMPTOMS.md`).
    ///
    /// Kutu CÖMERT tutuluyor: fazla büyük olması yalnız kırpmayı gevşetir,
    /// küçük olması sistemi yok eder.
    static void SetBounds(object init, Vector3 size, StringBuilder r)
    {
        SetSlotField(init, "bounds", "center", Vector3.zero, r);
        SetSlotField(init, "bounds", "size", size, r);
    }

    static (object init, object update, object output) Skeleton(
        object graph, uint capacity, float lifeA, float lifeB,
        float sizeA, float sizeB, Vector3 boundsSize, StringBuilder r)
    {
        object spawner = AddContext(graph, "VFXBasicSpawner", new Vector2(0, 0), r);
        object rate = AddBlock(spawner, "VFXSpawnerConstantRate", r);

        // Oran dışarıdan sürülüyor (spec §17.3, §18.7). Parametre olmasaydı
        // `SetFloat` sessizce düşerdi.
        object rateParam = AddParameter(graph, "SpawnRate", typeof(float), 0f,
                                        new Vector2(-300, 0), r);
        LinkParameter(rateParam, rate, "Rate", r);

        object init = AddContext(graph, "VFXBasicInitialize", new Vector2(0, 200), r);
        SetSetting(init, "capacity", capacity, r);
        SetWorldSpace(init, r);
        SetBounds(init, boundsSize, r);

        object life = AddBlock(init, "Block.SetAttribute", r);
        SetSetting(life, "attribute", "lifetime", r);
        SetSetting(life, "Random", "Uniform", r);
        SetSlot(life, "A", lifeA, r);
        SetSlot(life, "B", lifeB, r);

        object size = AddBlock(init, "Block.SetAttribute", r);
        SetSetting(size, "attribute", "size", r);
        SetSetting(size, "Random", "Uniform", r);
        SetSlot(size, "A", sizeA, r);
        SetSlot(size, "B", sizeB, r);

        object update = AddContext(graph, "VFXBasicUpdate", new Vector2(0, 500), r);

        object output = AddContext(graph, "URP.VFXURPLitPlanarPrimitiveOutput",
                                   new Vector2(0, 800), r);
        SetSetting(output, "blendMode", "Alpha", r);
        SetSetting(output, "zWriteMode", "Off", r);

        Link(spawner, init, r);
        Link(init, update, r);
        Link(update, output, r);

        return (init, update, output);
    }

    // ------------------------------------------------------------- VFX_SnowPuff

    /// AYAK TOZ BULUTU (spec §19.3). Spec tetiği veriyor (`depth > 0.06 &&
    /// density01 < 0.50`) ama parçacığın sayılarını vermiyor; değerler
    /// `SnowPuffEmitter`'dan taşınıyor — Faz 9'da zaten ölçülüp yerleşmişler.
    static void BuildPuff(StringBuilder r)
    {
        object graph = NewGraph("VFX_SnowPuff", r);

        var (init, update, output) =
            Skeleton(graph, 512, 0.4f, 0.9f, 0.02f, 0.06f,
                     new Vector3(6f, 6f, 6f), r);

        AddBlock(update, "Block.Gravity", r);
        AddBlock(update, "Block.Drag", r);

        object orient = AddBlock(output, "Block.Orient", r);
        SetSetting(orient, "mode", "FaceCameraPlane", r);

        Save(graph, r);
    }

    // ------------------------------------------------------------ VFX_SnowSpray

    /// KOŞARKEN PÜSKÜRTME (spec §18.6)
    /// `[KAYNAK: Sumner, O'Brien & Hodgins, CGF 1999]`.
    ///
    /// Miktar uydurulmuyor, simülasyondan geliyor: `V̇ = genişlik × batma × hız`.
    /// O hesap `SnowSprayController`'da; grafik yalnız parçacığı çiziyor.
    static void BuildSpray(StringBuilder r)
    {
        object graph = NewGraph("VFX_SnowSpray", r);

        // Kapasite 3000, ömür 0.5–1.1 s, boyut 0.03–0.10 m (spec §18.6).
        var (init, update, output) =
            Skeleton(graph, 3000, 0.5f, 1.1f, 0.03f, 0.10f,
                     new Vector3(10f, 8f, 10f), r);

        // Yerçekimi −9.81 × 0.35, drag 2.5 (spec §18.6).
        AddBlock(update, "Block.Gravity", r);

        object drag = AddBlock(update, "Block.Drag", r);
        SetSlot(drag, "dragCoefficient", 2.5f, r);

        object orient = AddBlock(output, "Block.Orient", r);
        SetSetting(orient, "mode", "FaceCameraPlane", r);

        Save(graph, r);
    }

    // ------------------------------------------------------------ VFX_Spindrift

    /// SALTASYON KATMANI (spec §18.7 Sistem A)
    /// `[KAYNAK: Pomeroy & Gray 1990; PBSM 1993]`.
    ///
    /// YERE YAPIŞIK: 1–5 cm. 1.5 m'ye spawn edilmiyor — o süspansiyon, ayrı
    /// sistem. Spec bunu ayrıca uyarıyor.
    static void BuildSpindrift(StringBuilder r)
    {
        object graph = NewGraph("VFX_Spindrift", r);

        // Ömür 1.2–3.0 s (spec §18.7). Boyut küçük ve çok sayıda.
        var (init, update, output) =
            Skeleton(graph, 8000, 1.2f, 3.0f, 0.01f, 0.03f,
                     new Vector3(80f, 10f, 80f), r);

        // SPAWN KUTUSU — YERE YAPIŞIK ŞERİT (spec §18.7: "kameranın rüzgâr
        // yönündeki 30 m'lik şeridinde, `y = groundY + random(0, 0.05)`.
        // 1.5 m'ye spawn etme, o süspansiyondur").
        //
        // Kutu yoktu: 8000 tane objenin TAM MERKEZİNDE doğuyor ve `AlongVelocity`
        // ile uzatılmış çizgiler halinde radyal patlıyordu — ekranda tek bir
        // noktadan fışkıran havai fişek. Kullanıcı ekran görüntüsüyle bildirdi.
        //
        // Yükseklik 5 cm: saltasyon katmanı bu kadar.
        object spinPos = AddBlock(init, "Block.PositionShape", r);
        SetSetting(spinPos, "shape", "OrientedBox", r);
        SetSetting(spinPos, "positionMode", "Volume", r);
        SetSlotField(spinPos, "Box", "size", new Vector3(30f, 0.05f, 30f), r);

        // HIZ (spec §18.7: `_WindWS * random(0.7, 1.1)` + yukarı 0.2–0.8 m/s).
        //
        // Hız bloğu YOKTU ve `Orient: AlongVelocity` sıfır hızda çöküyordu:
        // konsolda "floating point division by zero", ekranda tek noktadan
        // koni gibi fışkıran uzun çizgiler. Kutu eklemek yetmedi çünkü sorun
        // konumda değil YÖNDE'ydi.
        //
        // Yukarı bileşen saltasyonun zıplamasını veriyor; rüzgâr bileşeni
        // `SnowDriftVfxController`'dan geliyor.
        object spinVel = AddBlock(init, "Block.SetAttribute", r);
        SetSetting(spinVel, "attribute", "velocity", r);
        SetSetting(spinVel, "Random", "Uniform", r);
        SetSlotField(spinVel, "A", "vector", new Vector3(0f, 0.2f, 0f), r);
        SetSlotField(spinVel, "B", "vector", new Vector3(0f, 0.8f, 0f), r);

        object spinWind = AddBlock(update, "Block.Force", r);
        SetSetting(spinWind, "Mode", "Absolute", r);

        object spinWindParam = AddParameter(graph, "WindForce", typeof(Vector3),
                                            Vector3.zero, new Vector2(-300, 520), r);
        LinkParameter(spinWindParam, spinWind, "Force", r);

        object spinDrag = AddBlock(update, "Block.Drag", r);
        SetSlot(spinDrag, "dragCoefficient", 4f, r);

        // `Orient: Along Velocity`, 4–8× uzatılmış (spec §18.7).
        object orient = AddBlock(output, "Block.Orient", r);
        SetSetting(orient, "mode", "AlongVelocity", r);

        Save(graph, r);
    }

    // ----------------------------------------------------------- VFX_SnowCurtain

    /// SÜSPANSİYON PERDELERİ (spec §18.7 Sistem B).
    ///
    /// KAPASİTE 14 — BİLİNÇLİ OLARAK DÜŞÜK. Her parçacık devasa (genişlik
    /// 12–25 m); maliyet fill-rate. Spec sayıyı ve gerekçesini birlikte veriyor.
    static void BuildCurtain(StringBuilder r)
    {
        object graph = NewGraph("VFX_SnowCurtain", r);

        // Ömür 6–12 s; boyut genişlik 12–25 m (spec §18.7).
        var (init, update, output) =
            Skeleton(graph, 14, 6f, 12f, 12f, 25f,
                     new Vector3(400f, 80f, 400f), r);

        // SPAWN KUTUSU (spec §18.7: "rüzgâr üstünde 35 m, yatayda ±40 m").
        //
        // Yükseklik dağılımı spec'te ÜSTEL (`h = −1.1·log(1−rand)`, tavan 5 m);
        // burada 5 m'lik kutuda düzgün dağılım var. Üstel profili alpha
        // taşıyor — spec'in kendi alpha formülü `0.16·exp(−h/1.1)` zaten
        // yükseldikçe soluklaştırıyor, yani görsel sonuç üstel kalıyor.
        object curPos = AddBlock(init, "Block.PositionShape", r);
        SetSetting(curPos, "shape", "OrientedBox", r);
        SetSetting(curPos, "positionMode", "Volume", r);
        SetSlotField(curPos, "Box", "size", new Vector3(80f, 5f, 80f), r);

        // HIZ (spec §18.7: `_WindWS * random(0.7, 0.95)`). Perde rüzgârla
        // sürükleniyor; `AlongVelocity` yönünü buradan alıyor.
        object curWind = AddBlock(update, "Block.Force", r);
        SetSetting(curWind, "Mode", "Absolute", r);

        object curWindParam = AddParameter(graph, "WindForce", typeof(Vector3),
                                           Vector3.zero, new Vector2(-300, 520), r);
        LinkParameter(curWindParam, curWind, "Force", r);

        object curDrag = AddBlock(update, "Block.Drag", r);
        SetSlot(curDrag, "dragCoefficient", 3f, r);

        object orient = AddBlock(output, "Block.Orient", r);
        SetSetting(orient, "mode", "AlongVelocity", r);

        Save(graph, r);
    }

    // -------------------------------------------------------------- reflection

    static Assembly asm;

    static Assembly Asm => asm ??= AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(a => a.GetName().Name == EditorAsm)
        ?? throw new InvalidOperationException(EditorAsm + " yüklü değil.");

    /// İKİ ASSEMBLY'DE ARANIYOR. Bloklar ve temel bağlamlar VFX Graph
    /// paketinde, ama `Output Particle URP Lit Quad` (spec §17.1) URP
    /// paketinde: `UnityEditor.VFX.URP.VFXURPLitPlanarPrimitiveOutput`.
    /// Yalnız VFX assembly'sine bakınca "tip bulunamadı" veriyordu.
    /// ASGARİ EKRAN BOYUTU — `ScreenSpaceSize` bloğunun kendi ifadesi,
    /// atama yerine `max` ile.
    ///
    /// `clipPosW` kameraya uzaklık (klip uzayının w'si). Payda taneyi ekranda
    /// bir piksele oturtan ölçek; `UNITY_MATRIX_P`'nin köşegeni fov'u,
    /// `_ScreenParams` çözünürlüğü taşıyor. Spec §17.1'in
    /// `distToCam * (px / h) * 2 * tan(fov/2)` formülüyle aynı büyüklük.
    const string MinScreenSizeHlsl =
@"void SnowMinScreenSize(inout VFXAttributes attributes)
{
    const float minPixelSize = 1.3f;

    float clipPosW = TransformPositionVFXToClip(attributes.position).w;

    float denom = attributes.size * 0.5f
                * min(abs(UNITY_MATRIX_P[0][0] * _ScreenParams.x),
                      abs(UNITY_MATRIX_P[1][1] * _ScreenParams.y));

    float2 newScale = (float2(minPixelSize, minPixelSize) * clipPosW) / max(denom, 1e-6f);

    float2 eski   = float2(attributes.scaleX, attributes.scaleY);
    float2 buyumus = max(eski, newScale);

    // ENERJI KORUNUMU. Taneyi piksel tabanina cekmek kapladigi ALANI buyutur;
    // alfa ayni kalirsa buyume orani kadar isik UYDURULUR. 40 m'deki tane
    // gercek alaninin 14 katini tam alfayla boyuyordu ve 89 bin tane ust uste
    // binince ekranda sut gibi bir ortu cikiyordu (olculdu).
    float alanOrani = (buyumus.x * buyumus.y) / max(eski.x * eski.y, 1e-6f);
    attributes.alpha *= saturate(1.0f / max(alanOrani, 1.0f));

    attributes.scaleX = buyumus.x;
    attributes.scaleY = buyumus.y;
}";

    /// TANENİN KENDİ ÇIRPINMASI (spec §17.1 "Salınım").
    ///
    /// Türbülans bunun yerini TUTMUYOR — bu ayrım ölçüldü. Türbülans uzayda
    /// TUTARLI bir alan (dalga boyu ~8 m): yan yana iki tane aynı yöne itiliyor,
    /// ve alan zamanla değişmediği için duran bir oyuncu tek lobun içinde
    /// kalıyor. Sonuç rüzgâr gibi okunuyor — kullanıcı "rüzgâr 0 ama kar belirli
    /// bir yöne yağıyor, yürürken düzeliyor" dedi.
    ///
    /// Salınım tam tersi: her tanenin fazı ayrı, komşular birbirinden bağımsız
    /// çırpınıyor, kümenin net yönü sıfır.
    ///
    /// FAZ PARÇACIK KİMLİĞİNDEN. Spec `flutterPhase` custom attribute'u istiyor;
    /// kimlik zaten benzersiz ve ömür boyu sabit, ikinci bir alan taşımaya gerek
    /// yok. İstatistiksel sonuç aynı: taneye düzgün dağılmış sabit faz.
    ///
    /// HIZ DEĞİL, YER DEĞİŞTİRME — spec'in ifadesinin İNTEGRALİ.
    ///
    /// Spec `Set Position (Add)` ile `... * 0.35 * dt` veriyor, yani her kare
    /// biraz ekliyor. CustomHLSL bloğu `deltaTime`'a ULAŞAMIYOR: VFX'te bu
    /// sembolü blokların kendisi `VFXNamedExpression(DeltaTime, "deltaTime")`
    /// ile bildiriyor (paket kaynağından okundu, `FlipbookPlay.cs:255`) ve
    /// CustomHLSL öyle bir bildirim yapamıyor. Derleme "undeclared identifier
    /// 'deltaTime'" ile düştü.
    ///
    /// Salınım sınırlı bir titreşim olduğu için integrali kapalı formda:
    /// `∫ 0.35·sin(ωt) dt = (0.35/ω)·(-cos(ωt))`. Genlik x'te 0.35/5.5 = 6.4 cm,
    /// z'de 0.35/4.6 = 7.6 cm. Toplam yerine DOĞRUDAN ofset yazılıyor: kare
    /// hızından bağımsız ve birikme hatası yok.
    ///
    /// ÇIKTI BAĞLAMINDA, update'te değil. Salınım yalnız tanenin NEREDE
    /// ÇİZİLDİĞİNİ değiştiriyor; zemin kesmesini ve birikmeyi etkilememeli.
    ///
    /// Genlik ~7 cm, tane boyutu 1.8 cm — birkaç katı, yani görünür.
    ///
    /// `_SnowWetness` çarpanı DÜŞTÜ: ıslaklık sıcaklıktan geliyordu ve yağış
    /// sıcaklıktan koparıldığında sabit 0'a indi, yani çarpan hep 1.
    const string FlutterHlsl =
@"void SnowFlakeFlutter(inout VFXAttributes attributes)
{
    float faz = frac(sin(float(attributes.particleId) * 12.9898) * 43758.5453) * 6.2831853;

    attributes.position += float3(-cos(attributes.age * 5.5 + faz) * (0.35 / 5.5),
                                   0.0,
                                   sin(attributes.age * 4.6 + faz) * (0.35 / 4.6));
}";

    /// Spec §17.1: ömrün ilk %8'inde fade-in, son %8'inde fade-out; tane
    /// zemin yüksekliğinin 2 cm altına inince ölüyor.
    ///
    /// `groundY` dışarıdan geliyor — VFX'in zemin dokusuna erişimi yok,
    /// oyuncunun ayak kotu yeterince iyi bir yaklaşım (kutu yalnız 24 m).
    const string FadeAndGroundHlsl =
@"void SnowFlakeFadeAndKill(inout VFXAttributes attributes, in float groundY)
{
    float t = attributes.age / max(attributes.lifetime, 1e-4);

    float fadeIn  = smoothstep(0.0, 0.08, t);
    float fadeOut = 1.0 - smoothstep(0.92, 1.0, t);

    attributes.alpha = fadeIn * fadeOut;

    // POZİSYON DÜNYA UZAYINDA, KOT DA DÜNYA GELİYOR.
    //
    // Sistem `SetWorldSpace` ile dünya uzayına alındı (gerekçe orada); artık
    // `attributes.position` doğrudan dünya koordinatı ve `SnowfallLayers`
    // zemin kotunu olduğu gibi yolluyor.
    //
    // Sistem YEREL iken bu satır iki kez karın TAMAMINI silmişti: bir kez
    // dünya kotu yerel y ile karşılaştırıldığı için, bir kez de
    // `TransformPositionVFXToWorld` beklendiği gibi davranmadığı için
    // (ölçüldü: `groundY = 0` iken bile hepsi öldü). Uzay düzelince ikisi de
    // konu dışı kaldı.
    if (attributes.position.y < groundY + 0.02)
        attributes.alive = false;
}";

    static Type Find(string shortName)
    {
        string full =
            shortName.StartsWith("Block.") ? "UnityEditor.VFX.Block." + shortName.Substring(6) :
            shortName.StartsWith("URP.")   ? "UnityEditor.VFX.URP." + shortName.Substring(4) :
                                             "UnityEditor.VFX." + shortName;

        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = a.GetType(full, false);
            if (t != null) return t;
        }

        throw new InvalidOperationException("VFX tipi bulunamadı: " + full);
    }

    // ------------------------------------------------------------ ayar / slot

    /// Blok veya bağlam ayarı. Enum'lar ADLA veriliyor; sayısal değer yazmak
    /// enum sırası değişince sessizce başka bir şey seçer.
    static void SetSetting(object model, string name, object value, StringBuilder r)
    {
        // AYAR HER ZAMAN MODELİN KENDİ ALANI DEĞİL. `VFXBasicInitialize.capacity`
        // parçacık verisine yönlendiriliyor ve bağlamda öyle bir alan yok
        // (ölçüldü — `GetSetting` override'lı). O yüzden tip `GetSetting`'den
        // okunuyor, alandan değil.
        MethodInfo getSetting = model.GetType().GetMethod("GetSetting",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(string) }, null)
            ?? throw new InvalidOperationException("GetSetting yok.");

        object ayar = getSetting.Invoke(model, new object[] { name });

        FieldInfo alan = ayar?.GetType()
            .GetField("field", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(ayar) as FieldInfo
            ?? throw new InvalidOperationException(
                model.GetType().Name + " ayarı yok: " + name);

        object son = value is string str && alan.FieldType.IsEnum
            ? Enum.Parse(alan.FieldType, str)
            : Convert.ChangeType(value, alan.FieldType);

        MethodInfo set = model.GetType().GetMethod("SetSettingValue",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(string), typeof(object) }, null)
            ?? throw new InvalidOperationException("SetSettingValue yok.");

        set.Invoke(model, new[] { name, son });
        r.AppendLine("           ayar   " + name.PadRight(24) + son);
    }

    /// Giriş slotu değeri. Slot ADLA aranıyor — indeks kullanmak ayar
    /// değişince başka slotu yazar.
    static void SetSlot(object model, string slotName, object value, StringBuilder r)
    {
        MethodInfo nb = model.GetType().GetMethod("GetNbInputSlots",
            BindingFlags.Public | BindingFlags.Instance);
        MethodInfo get = model.GetType().GetMethod("GetInputSlot",
            BindingFlags.Public | BindingFlags.Instance);

        if (nb == null || get == null)
            throw new InvalidOperationException(model.GetType().Name + " slot taşımıyor.");

        int n = (int)nb.Invoke(model, null);

        for (int i = 0; i < n; i++)
        {
            object slot = get.Invoke(model, new object[] { i });
            if (Prop(slot, "name")?.GetValue(slot) as string != slotName) continue;

            PropertyInfo degerP = Prop(slot, "value")
                ?? throw new InvalidOperationException("Slot değeri yazılamıyor.");

            degerP.SetValue(slot, value);

            // YAZILAN GERİ OKUNUYOR. Slot tipi beklenenden farklıysa
            // `SetValue` sessizce varsayılanı bırakıyor: log "yazdım" derken
            // asset sıfır kalıyor. Bir kez oldu — `velocity` slotu `Vector3`
            // değil `Vector` sarmalayıcısıydı, kar hiç düşmedi.
            object geri = degerP.GetValue(slot);

            if (!Equals(geri, value))
                throw new InvalidOperationException(
                    model.GetType().Name + "." + slotName + " yazılamadı: " +
                    "verilen " + value.GetType().Name + " = " + value +
                    ", slotta " + (geri?.GetType().Name ?? "null") + " = " + geri +
                    ". Slot tipi farklı — alt alana yazmak için SetSlotField kullan.");

            r.AppendLine("           slot   " + slotName.PadRight(24) + value);
            return;
        }

        throw new InvalidOperationException(
            model.GetType().Name + " slotu yok: " + slotName);
    }

    /// Boş bir `.vfx` yaratıp grafiğini döndürüyor.
    static object NewGraph(string name, StringBuilder r)
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Snow", "VFX");

        string path = Folder + "/" + name + ".vfx";

        // VARSA SİLİNİYOR. Üretim tekrar koşturulabilir olmalı; üstüne yazmak
        // eski blokları bırakır ve grafik iki kez dolar.
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            AssetDatabase.DeleteAsset(path);

        Type util = Asm.GetType("UnityEditor.VisualEffectAssetEditorUtility", false)
            ?? throw new InvalidOperationException("VisualEffectAssetEditorUtility yok.");

        MethodInfo create = util.GetMethod("CreateNewAsset",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("CreateNewAsset yok.");

        create.Invoke(null, new object[] { path });

        Type resType = Asm.GetType("UnityEditor.VFX.VisualEffectResource", false)
            ?? Type.GetType("UnityEditor.VFX.VisualEffectResource, UnityEditor")
            ?? throw new InvalidOperationException("VisualEffectResource yok.");

        MethodInfo atPath = resType.GetMethod("GetResourceAtPath",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetResourceAtPath yok.");

        object resource = atPath.Invoke(null, new object[] { path })
            ?? throw new InvalidOperationException("Resource okunamadı: " + path);

        // `GetOrCreateGraph` bir uzantı metodu; statik olarak çağrılıyor.
        MethodInfo getGraph = Asm.GetType("UnityEditor.VFX.VFXGraphExtension", false)
            ?.GetMethod("GetOrCreateGraph", BindingFlags.Public | BindingFlags.Static);

        if (getGraph == null)
            getGraph = Asm.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .FirstOrDefault(m => m.Name == "GetOrCreateGraph");

        if (getGraph == null)
            throw new InvalidOperationException("GetOrCreateGraph bulunamadı.");

        object graph = getGraph.Invoke(null, new[] { resource })
            ?? throw new InvalidOperationException("Grafik yaratılamadı.");

        r.AppendLine("  [+] " + name + " yaratıldı — " + path);
        return graph;
    }

    static object AddContext(object graph, string typeName, Vector2 pos, StringBuilder r)
    {
        Type t = Find(typeName);
        var ctx = ScriptableObject.CreateInstance(t)
            ?? throw new InvalidOperationException(typeName + " örneklenemedi.");

        SetPosition(ctx, pos);
        AddChild(graph, ctx);

        r.AppendLine("      bağlam  " + typeName);
        return ctx;
    }

    static object AddBlock(object context, string typeName, StringBuilder r)
    {
        Type t = Find(typeName);
        var block = ScriptableObject.CreateInstance(t)
            ?? throw new InvalidOperationException(typeName + " örneklenemedi.");

        AddChild(context, block);
        r.AppendLine("        blok  " + typeName);

        return block;
    }

    /// AYARLARI VE SLOTLARI YAZ. Hangi bloğun hangi ayarı açtığını tahmin etmek
    /// "ayar bulunamadı" hatasına çıkıyor; adları modelden okuyoruz.
    ///
    /// Menüden açılıyor; üretim kapalı koşuyor.
    static bool Dump;

    [MenuItem("To The Summit/Snow/Generate VFX Graphs (dump)", false, 63)]
    static void BuildAllWithDump()
    {
        Dump = true;
        try { BuildAll(); }
        finally { Dump = false; }
    }

    static void DumpModel(object model, StringBuilder r)
    {
        // --- ayarlar
        MethodInfo getSettings = model.GetType().GetMethod("GetSettings",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(bool), typeof(object) }, null);

        var alanlar = model.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(f => f.GetCustomAttributes()
                         .Any(a => a.GetType().Name.StartsWith("VFXSetting")))
            .ToArray();

        foreach (FieldInfo f in alanlar)
            r.AppendLine("           ayar   " + f.Name.PadRight(28) +
                         f.FieldType.Name + " = " + (f.GetValue(model) ?? "null"));

        // --- slotlar
        MethodInfo nb = model.GetType().GetMethod("GetNbInputSlots",
            BindingFlags.Public | BindingFlags.Instance);
        MethodInfo get = model.GetType().GetMethod("GetInputSlot",
            BindingFlags.Public | BindingFlags.Instance);

        if (nb == null || get == null) return;

        int n = (int)nb.Invoke(model, null);

        for (int i = 0; i < n; i++)
        {
            object slot = get.Invoke(model, new object[] { i });
            if (slot == null) continue;

            string ad = Prop(slot, "name")?.GetValue(slot) as string;
            object deger = Prop(slot, "value")?.GetValue(slot);

            r.AppendLine("           slot   " + (ad ?? "?").PadRight(28) +
                         (deger == null ? "null" : deger.GetType().Name + " = " + deger));
        }
    }

    static void AddChild(object parent, object child)
    {
        MethodInfo add = parent.GetType().GetMethod("AddChild",
            BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("AddChild yok: " + parent.GetType().Name);

        add.Invoke(parent, new object[] { child, -1, true });
    }

    static void SetPosition(object model, Vector2 pos)
    {
        PropertyInfo p = model.GetType().GetProperty("position",
            BindingFlags.Public | BindingFlags.Instance);

        p?.SetValue(model, pos);
    }

    static void Link(object from, object to, StringBuilder r)
    {
        MethodInfo link = from.GetType().GetMethod("LinkTo",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { Find("VFXContext"), typeof(int), typeof(int) }, null)
            ?? throw new InvalidOperationException("LinkTo yok.");

        link.Invoke(from, new object[] { to, 0, 0 });
        r.AppendLine("      bağ     " + from.GetType().Name + " → " + to.GetType().Name);
    }

    /// EXPOSED PARAMETRE. Grafik dışarıdan sürülebilsin diye.
    ///
    /// Parametre YOKSA `VisualEffect.SetFloat` sessizce düşüyor: `HasFloat`
    /// false dönüyor, çağrı hiçbir şey yapmıyor ve dışarıdan "kar yağmıyor"
    /// görünüyor. Denetleyicinin yazdığı her ad burada karşılığını bulmalı.
    static object AddParameter(object graph, string name, Type type,
                               object value, Vector2 pos, StringBuilder r)
    {
        Type pt = Find("VFXParameter");

        var param = ScriptableObject.CreateInstance(pt)
            ?? throw new InvalidOperationException("VFXParameter örneklenemedi.");

        MethodInfo init = pt.GetMethod("Init",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(Type) }, null)
            ?? throw new InvalidOperationException("VFXParameter.Init yok.");

        init.Invoke(param, new object[] { type });

        SetPosition(param, pos);
        AddChild(graph, param);

        // `exposedName` ve `exposed` yalnız GET; alanları `[VFXSetting]`
        // (ölçüldü — VFXParameter.cs:56-59), o yüzden ayar yoluyla yazılıyor.
        // Çocuk eklendikten SONRA: ayar değişimi grafiği haberdar ediyor.
        SetSetting(param, "m_ExposedName", name, r);
        SetSetting(param, "m_Exposed", true, r);

        // Varsayılan değer parametrenin kendi çıkış slotunda duruyor.
        MethodInfo getOut = pt.GetMethod("GetOutputSlot",
            BindingFlags.Public | BindingFlags.Instance);

        if (getOut?.Invoke(param, new object[] { 0 }) is object outSlot)
            Prop(outSlot, "value")?.SetValue(outSlot, value);

        r.AppendLine("      param   " + name.PadRight(24) + type.Name + " = " + value);
        return param;
    }

    /// Parametrenin çıkışını bir bloğun giriş slotuna bağlıyor.
    static void LinkParameter(object param, object target, string slotName,
                              StringBuilder r)
    {
        object outSlot = param.GetType().GetMethod("GetOutputSlot",
            BindingFlags.Public | BindingFlags.Instance)
            ?.Invoke(param, new object[] { 0 })
            ?? throw new InvalidOperationException("Parametrenin çıkış slotu yok.");

        MethodInfo nb = target.GetType().GetMethod("GetNbInputSlots",
            BindingFlags.Public | BindingFlags.Instance);
        MethodInfo get = target.GetType().GetMethod("GetInputSlot",
            BindingFlags.Public | BindingFlags.Instance);

        int n = (int)nb.Invoke(target, null);

        for (int i = 0; i < n; i++)
        {
            object slot = get.Invoke(target, new object[] { i });
            if (Prop(slot, "name")?.GetValue(slot) as string != slotName) continue;

            MethodInfo link = outSlot.GetType().GetMethod("Link",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { outSlot.GetType(), typeof(bool) }, null)
                ?? throw new InvalidOperationException("VFXSlot.Link yok.");

            bool ok = (bool)link.Invoke(outSlot, new object[] { slot, true });

            r.AppendLine("      bağ     param → " + target.GetType().Name +
                         "." + slotName + (ok ? "" : "  BAŞARISIZ"));

            if (!ok) throw new InvalidOperationException(
                "Parametre bağlanamadı: " + slotName);

            return;
        }

        throw new InvalidOperationException(target.GetType().Name + " slotu yok: " + slotName);
    }

    /// YAPI SLOTU İÇİNDEKİ ALAN. `Box` slotu `OrientedBox` taşıyor; kutunun
    /// boyu onun `size` alanında. Slotun değerini komple değiştirmek yerine
    /// mevcut değeri alıp alanını yazıyoruz — merkez ve açı korunuyor.
    static void SetSlotField(object model, string slotName, string fieldName,
                             object value, StringBuilder r)
    {
        MethodInfo nb = model.GetType().GetMethod("GetNbInputSlots",
            BindingFlags.Public | BindingFlags.Instance);
        MethodInfo get = model.GetType().GetMethod("GetInputSlot",
            BindingFlags.Public | BindingFlags.Instance);

        int n = (int)nb.Invoke(model, null);

        for (int i = 0; i < n; i++)
        {
            object slot = get.Invoke(model, new object[] { i });
            if (Prop(slot, "name")?.GetValue(slot) as string != slotName) continue;

            PropertyInfo degerP = Prop(slot, "value");
            object kutu = degerP.GetValue(slot);

            FieldInfo f = kutu.GetType().GetField(fieldName,
                BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    kutu.GetType().Name + " alanı yok: " + fieldName);

            f.SetValue(kutu, value);
            degerP.SetValue(slot, kutu);

            r.AppendLine("           slot   " + (slotName + "." + fieldName).PadRight(24) + value);
            return;
        }

        throw new InvalidOperationException(model.GetType().Name + " slotu yok: " + slotName);
    }

    /// ÖZELLİK ARAYICI, BELİRSİZLİK GÜVENLİ.
    ///
    /// `children` hiyerarşide gölgeleniyor (`VFXModel` ve türevleri ayrı ayrı
    /// tanımlıyor); düz `GetProperty` `AmbiguousMatchException` atıyor. En
    /// türemiş tipten başlayıp yukarı yürüyor, ilk bulduğunu alıyor.
    static PropertyInfo Prop(object o, string name)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
        {
            PropertyInfo p = t.GetProperty(name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            if (p != null) return p;
        }

        return null;
    }

    /// GRAFİĞİN SON HÂLİ. Ayarlar uygulandıktan SONRA dökülüyor: slot adları
    /// ayara göre değişiyor (şekil kutu olunca `arcSphere` gidiyor, kutu
    /// geliyor). Kurulum sırasında dökmek eski adları gösterir.
    static void DumpGraph(object graph, StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Son hâl");

        if (Prop(graph, "children")?.GetValue(graph) is not System.Collections.IEnumerable ctxs)
        {
            r.AppendLine("  [!] grafik çocukları okunamadı");
            return;
        }

        foreach (object ctx in ctxs)
        {
            r.AppendLine("  bağlam  " + ctx.GetType().Name);
            DumpModel(ctx, r);

            if (Prop(ctx, "children")?.GetValue(ctx) is not System.Collections.IEnumerable blocks)
                continue;

            foreach (object b in blocks)
            {
                r.AppendLine("    blok  " + b.GetType().Name);
                DumpModel(b, r);
            }
        }
    }

    /// SİSTEM UZAYI: YEREL Mİ DÜNYA MI.
    ///
    /// Yerel uzayda `attributes.position` objeye GÖRE tutuluyor; obje kayınca
    /// yaşayan bütün parçacıklar onunla birlikte ışınlanıyor. Spawn kutusu
    /// oyuncuyu 1 m ızgarasında takip ettiği için yürürken saniyede birkaç kez
    /// tüm kar bir metre atlıyordu — kullanıcı "taneler çok hızlı yer
    /// değiştiriyor, sürekli yeniden render oluyor gibi" dedi.
    ///
    /// Dünya uzayında obje yalnız NEREYE DOĞDUKLARINI belirliyor; doğmuş tane
    /// dünyada kalıyor.
    ///
    /// Uzay `GetSetting` üzerinden görünmüyor (veri nesnesinin `ISpaceable`
    /// özelliği); bulunamazsa fırlatılıyor — sessizce yerel kalmak belirtiyi
    /// geri getirir.
    static void SetWorldSpace(object init, StringBuilder r)
    {
        MethodInfo getData = init.GetType().GetMethod("GetData",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("GetData yok.");

        object data = getData.Invoke(init, null)
            ?? throw new InvalidOperationException("Parçacık verisi yok.");

        PropertyInfo uzay = data.GetType().GetProperty("space",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                data.GetType().Name + " üzerinde `space` özelliği yok.");

        object dunya = Enum.Parse(uzay.PropertyType, "World");
        uzay.SetValue(data, dunya);

        object geri = uzay.GetValue(data);
        if (!Equals(geri, dunya))
            throw new InvalidOperationException("Uzay yazılamadı: " + geri);

        r.AppendLine("           uzay   World");
    }

    static void Save(object graph, StringBuilder r)
    {
        if (Dump) DumpGraph(graph, r);

        foreach (string ad in new[] { "UpdateSubAssets", "OnSaved" })
        {
            MethodInfo m = graph.GetType().GetMethod(ad,
                BindingFlags.Public | BindingFlags.Instance);

            m?.Invoke(graph, null);
        }

        EditorUtility.SetDirty((UnityEngine.Object)graph);
        r.AppendLine("  [+] kaydedildi");
    }
}
