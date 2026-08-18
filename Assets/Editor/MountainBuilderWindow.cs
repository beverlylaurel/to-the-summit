using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// Dağı sıfırdan yapma atölyesi: boş düzlemden başlar, fırça ve toplu işlemlerle
/// şekillenir, arazinin yükseklik haritası olarak yazılır.
///
/// NEDEN VAR: dağ önce Python'da istatistikle üretiliyordu ve şekli ancak süzgeçle
/// dolaylı ayarlanabiliyordu — "şuraya bir sırt koy" denemiyordu, her tur dakikalar
/// sürüyordu ve istenen görüntüye ulaşılamadı.
///
/// İKİ IZGARA. Çalışma 1025²'de (29.3 m/hücre), görüntü 513²'de. Fırça yalnız kendi
/// yarıçapındaki hücrelere dokunuyor; pahalı olan örgüyü GPU'ya yüklemek, o yüzden
/// çizim sırasında saniyede ~10 kez yenileniyor. Kaydederken 4097²'ye büyütülüyor.
public class MountainBuilderWindow : EditorWindow
{
    const int Grid = 1025;
    const int View = 513;
    const int Export = 4097;
    const float ArenaM = 30000f;
    const float MaxM = 8000f;   // arazinin dikey tavanı (`TerrainData.size.y`)

    // ÇALIŞMA DOSYASI. Pencerenin ızgarası serileştirilmiyor ve her derleme onu siliyordu;
    // saatlerce yontulan dağ tek bir kod değişikliğiyle gidiyordu. Ham float32 yazılıyor:
    // PNG'ye çevirmek 16 bit'e yuvarlar ve geri okurken kot kayardı.
    const string SculptDir = "Assets/Terrain/Sculpts";
    const string AutoName = "_son";
    const string RescueName = "_kurtarma";   // üzerine yazmadan önceki son hâl

    // Değişiklik oldu mu ve en son ne zaman kaydedildi. Editör çökerse ya da zorla
    // kapatılırsa `OnDisable` çalışmıyor; periyodik kayıt o boşluğu kapatıyor.
    bool dirtySinceSave;
    double lastAutoSave;

    static float CellM => ArenaM / (Grid - 1);

    enum Tab { Brush, Ops, Mask, Route, Measure, Save }
    enum BrushKind { Raise, Lower, Smooth, Flatten, Ridge, Valley, Erode, Sharpen, Noise }
    enum MaskKind { None, Height, Slope, Ridges, Valleys }

    static readonly string[] TabNames =
    { "Fırça", "İşlemler", "Maske", "Rota", "Ölçüm", "Kaydet" };

    static readonly string[] BrushNames =
    { "Yükselt", "Alçalt", "Yumuşat", "Düzleştir", "Sırt", "Vadi", "Aşındır", "Keskinleştir", "Gürültü" };

    /// Her fırçanın NEREYE çizileceği. "Ne yapar" yetmiyordu: vadinin yamaca mı ovaya mı
    /// çizileceği, sırtın nereden başlayacağı bilinmeden araç kullanılamıyor.
    static readonly string[] BrushHelp =
    {
        // Yükselt
        "NEREYE: dağın gövdesi. Merkeze büyük yarıçapla (2-4 km) bas, kütleyi kur.\n"
        + "Küçük yarıçapla omuz, basamak ve yan tepe eklersin.\n"
        + "İPUCU: tek seferde çok bastırma; üst üste hafif geçişler daha doğal duruyor.",

        // Alçalt
        "NEREYE: kütlenin içine oyulacak çanaklar — buzul sirki (zirvenin altındaki kaşık\n"
        + "biçimli çukur), boyun (iki zirve arası çökük), ovada göl yatağı.\n"
        + "Vadi için bunu DEĞİL, Vadi fırçasını kullan: vadi çizgiseldir, çanak değil.",

        // Yumuşat
        "NEREYE: fırça izlerinin kaldığı yer, testere görünümü, tek hücrelik sivri uçlar.\n"
        + "Aşınmadan sonra kalan sertlikleri alır.\n"
        + "DİKKAT: geniş yarıçapla uzun sürtmek dağı hamurlaştırır, detay gider.",

        // Düzleştir
        "NEREYE: kamp yerleri, sırt üstündeki omuzlar, iki zirve arasındaki kol (col),\n"
        + "ovadaki teraslar. Gerçek dağda düzlükler SIRT ÜSTÜNDE ve BOYUNDA olur —\n"
        + "dik yamacın ortasında düzlük doğal durmaz.\n"
        + "Önce düzlüğü istediğin kota tıkla (o kot hedef olur), sonra etrafa yay.",

        // Sırt
        "NEREYE: zirveden AŞAĞI DOĞRU, parmak gibi dışa açılan hatlar. Gerçek dağda sırt\n"
        + "zirveden başlar, alçalarak etek boyunca uzar; üç-beş tane olur.\n"
        + "Sırtlar vadileri BİRBİRİNDEN AYIRIR: iki vadinin arasında mutlaka bir sırt var.\n"
        + "NASIL: En/boy 3-5 yap, Açı'yı çizeceğin yöne çevir, zirveden dışa sürükle.\n"
        + "Rotalar sırttan gider (25-30°), duvardan değil — tırmanış hattını burada kurarsın.",

        // Vadi
        "NEREYE: İKİ SIRTIN ARASINA, yukarıdan aşağı. Vadi asla zirveden geçmez ve asla\n"
        + "yokuş yukarı gitmez — su nereye akarsa vadi oradadır.\n"
        + "Yukarıda dar ve dik başlar, aşağı indikçe genişler ve yatıklaşır.\n"
        + "Ovaya vardığında sığ bir oluğa döner; ovada derin vadi olmaz.\n"
        + "NASIL: En/boy 3-5, zirvenin biraz altından başla, eteğe kadar sürükle.",

        // Aşındır
        "NEREYE: fazla dik kalan yüzler ve keskin kenarlar. Duruş açısını (38°) aşan\n"
        + "yerleri indirir, sırt hattını korur.\n"
        + "Kütleyi kurduktan sonra genel bir geçiş yap; İşlemler sekmesindeki termal\n"
        + "aşınma aynı işi tüm araziye uygular.",

        // Keskinleştir
        "NEREYE: aşınma ya da yumuşatma sonrası silikleşen sırt hatları.\n"
        + "Mevcut kabartıyı güçlendirir — olmayan bir sırtı YARATMAZ, önce Sırt fırçasıyla\n"
        + "çiz, sonra burayla belirginleştir.",

        // Gürültü
        "NEREYE: fazla düz kalan yamaçlar ve ova. Ovada 'tepecikli düz' görüntüyü bu verir.\n"
        + "Küçük güçle geniş alana sürt; büyük güç arazi ölçeğinde çöp üretir.\n"
        + "Zirve çevresinde az kullan — orada kaya yapısı gürültüden değil sırtlardan gelir.",
    };


    // --- veri
    float[] h;
    Mesh mesh;
    Material mat;

    // ÖRGÜ ÖNBELLEĞİ. Köşe dizileri ve ÜÇGEN İNDEKSLERİ bir kez kuruluyor: topoloji
    // hiç değişmiyor, yalnız köşe kotu ve rengi değişiyor. Eskiden her yenilemede
    // `Mesh.Clear` + üçgenlerin yeniden ataması yapılıyordu — 525 bin indeks, saniyede
    // on kez. Fırça takılmasının kaynağı buydu.
    Vector3[] vVerts;
    Color[] vCols;
    bool topoBuilt;

    // Fırçanın değdiği bölge (görüntü ızgarası koordinatı). Yalnız burası yeniden
    // hesaplanıyor; tüm alanı taramak 263 bin köşe demek.
    int pdx0 = int.MaxValue, pdz0 = int.MaxValue, pdx1 = int.MinValue, pdz1 = int.MinValue;

    // DEĞİŞİM İZİ. Yumuşak vuruşlarda kot birkaç metre oynuyor ve 30 km'lik bir arenada
    // gözle seçilmiyor — fırçanın işe yarayıp yaramadığı anlaşılmıyordu. Değişen köşeler
    // parlıyor ve bir saniyede sönüyor; iz kotun kendisine değil DEĞİŞİMİNE bakıyor.
    float[] heat;
    int hx0 = int.MaxValue, hz0 = int.MaxValue, hx1 = int.MinValue, hz1 = int.MinValue;
    double lastHeatTick;
    PreviewRenderUtility prev;

    // --- fırça
    BrushKind brush = BrushKind.Raise;
    float radiusM = 900f, strength = 0.5f, hardness = 0.35f, aspect = 1f, angleDeg = 0f;
    float flattenTarget = float.NaN;

    // --- maske
    MaskKind maskKind = MaskKind.None;
    float maskLo = 500f, maskHi = 4000f, maskFeather = 200f;
    float maskSlopeLo = 0f, maskSlopeHi = 30f, maskSlopeFeather = 5f;
    float maskCurveRadius = 400f, maskCurveStrength = 40f;
    bool maskPreview;
    float[] maskCache;

    // --- işlem ayarları
    // TERMAL VARSAYILANLARI ÖLÇÜMLE SEÇİLDİ. 20 tur / 0.14 hız 1025²'lik ızgarada
    // yakınsamıyor: komşu eğimi duruş açısının çok üstünde kalıyor ve sivri duruyor.
    // 60 tur / 0.5 hız ile komşu eğimi tam duruş açısına oturuyor (sentetik sivri
    // üstünde ölçüldü: 500 m'lik tek hücre -> 36.0 derece koni).
    float opTalus = 36f, opRate = 0.5f; int opIters = 60;
    // VARSAYILANLAR SebLague/Hydraulic-Erosion (MIT) REFERANSINDAN. Kendi seçtiğim
    // sayılarla damla yönünü çok ağır değiştiriyor ve ızgaraya hizalı oluklar açıyordu;
    // referansın ataleti (0.05) çok daha düşük.
    int hydDroplets = 120000, hydSteps = 30;
    // ATALET REFERANSTAN YÜKSEK. Referans (0.05) FRAKTAL GÜRÜLTÜ üstünde çalışıyor:
    // orada düşüş hatları zaten dağınık. Bizimki elle yontulmuş pürüzsüz bir kütle ve
    // bütün düşüş hatları ışınsal — düşük atalet damlayı her adımda en dik yöne
    // kilitliyor, yan yana paralel oluklar açılıyor ve ekranda dikey taranmış çizgiler
    // kalıyor. Atalet damlaya savrulma payı veriyor, oluklar birleşip dallanıyor.
    float hydInertia = 0.35f, hydCapacity = 4f, hydErode = 0.3f, hydDeposit = 0.3f,
          hydEvap = 0.01f, hydGravity = 4f;
    int hydBrush = 5;
    float noiseWl = 1200f, noiseAmp = 120f, noisePers = 0.5f, noiseLac = 2f; int noiseOct = 6;
    float warpWl = 3000f, warpAmp = 400f;
    float terraceStep = 120f, terraceSharp = 0.6f;
    float sharpRadius = 200f, sharpGain = 1.6f;
    float remapMin = 0f, remapMax = 5709f;
    float coneRadius = 9000f, coneHeight = 4500f;
    // ÖLÇÜMLE AYARLANDI. İlk değerlerle etek geçişi kenarı düzeltti (kenar kotu
    // 1202 -> 133 m) ama ova bandı 11.5 derecede kaldı — bisikletle geçilecek yer için
    // dik. Geçiş daha içeriden başlıyor ve ovada kalan kabartı payı düşürüldü.
    float apronInner = 7500f, apronOuter = 14500f, apronKeep = 0.12f;
    float calmBelow = 900f, calmFeather = 350f, calmKeep = 0.18f, calmScale = 700f;
    // OVA SIFIRDAN BAŞLIYOR. 186 m eski ÜRETİLEN arazinin ölçülmüş kotuydu, tasarım
    // kararı değil. Tek bağı sıcaklık: donma seviyesi mutlak kottan türüyor, 186 m fark
    // 1.2 °C demek — ihmal edilir.
    float plainM = 0f;

    // İNCE DETAY — KAYDETME ANINDA, 4097²'DE. Yontma ızgarası 29.3 m; aradaki büyütme
    // ondan ince hiçbir şey üretemiyor ve dağ yakından çıplak görünüyor. Arazinin kendi
    // ızgarası 7.32 m, yani 14.65 m'ye kadar dalga taşıyabiliyor — o bant burada
    // dolduruluyor.
    bool fineDetail = true;
    float fineWavelength = 420f, fineAmplitude = 26f;
    int fineOctaves = 5;
    float fineSteepBias = 0.7f;
    int opSeed = 12345;

    // --- kamera / durum
    // SERBEST UÇUŞ KAMERASI. Önce sabit bir eksen etrafında yörüngeye oturuyordu:
    // mesafe sabitti, dağın dibine girilemiyordu. Artık konum bağımsız — WASD uçurur,
    // sağ tık bakış yönünü çevirir.
    float yaw = 35f, pitch = 30f, vScale = 1f, viewShare = 0.5f;
    Vector3 flyPos = new Vector3(0f, 12f, -26f);   // km
    float flySpeed = 1.2f;                          // km/s
    readonly HashSet<KeyCode> keysDown = new HashSet<KeyCode>();
    double lastFlyTick;
    Tab tab = Tab.Brush;
    bool painting, meshDirty;
    double lastMeshBuild;
    Vector3 cursor; bool cursorValid;

    // KAMERA DURUMU ELDE TUTULUYOR. `PreviewRenderUtility`'nin kamerası ancak
    // `BeginPreview` içinde doğru piksel dikdörtgenine kavuşuyor; fare hareketinde
    // `ScreenPointToRay` ve `WorldToScreenPoint` eski/boş değerle çalışıyor ve fırça
    // halkası hiç görünmüyordu. Işın da izdüşüm de artık aynı matristen türüyor.
    Vector3 camPos; Quaternion camRot; float camFov = 32f; Rect viewRect;

    Vector2 scroll;
    string sculptName = "dag";

    // ROTALAR. Noktalar KİLOMETRE cinsinden yatay konum (x, z) tutuyor; kot saklanmıyor,
    // her çizimde araziden okunuyor. Dağ yeniden yontulduğunda hat yeni zemine
    // kendiliğinden oturuyor — mutlak kot saklansaydı havada asılı kalırdı.
    // ADI `RoutePath`, `Path` DEĞİL: `System.IO.Path`'i gölgeliyor ve dosya adı
    // işlemleri derlenmiyordu.
    // `[Serializable]` ŞART. Unity `EditorWindow`'un private alanlarını derleme
    // sonrasına taşıyor ama yalnız serileştirilebilir türleri: `float[] h` sağ
    // çıkıyordu, özel sınıf dizisi olan rotalar çıkmıyordu. Açılışta `h` dolu
    // görüldüğü için dosya da okunmuyor, rotalar boş kalıyor, sonraki kapanışta o
    // boşluk dosyanın üstüne yazılıyordu.
    [System.Serializable]
    class RoutePath
    {
        public string name;
        public Color color;
        public List<Vector2> pts = new List<Vector2>();
    }

    [SerializeField] RoutePath[] paths =
    {
        new RoutePath { name = "Otobüs yolu", color = new Color(1.00f, 0.60f, 0.10f) },
        new RoutePath { name = "Tırmanış rotası", color = new Color(0.95f, 0.25f, 0.25f) },
    };

    int activePath;   // 0 = otobüs yolu, 1 = tırmanış rotası
    float routeSpacingM = 25f;    // iki nokta arası mesafe
    float routeRadiusM = 3.2f;    // yolun yarı genişliği (MountainRoute.Mark.radius)
    bool drawingRoute;

    // ROTA GERİ ALMA AYRI YIĞIN. Yükseklik yığını dikdörtgen kopyası tutuyor; rota
    // noktası oraya sığmıyor. Vuruş başına kaç nokta eklendiği saklanıyor: bir sürükleme
    // onlarca nokta bırakıyor ve tek tek geri almak işkence olurdu.
    struct RouteEdit { public int path; public List<Vector2> pts; }
    readonly List<RouteEdit> routeUndo = new List<RouteEdit>();
    readonly List<RouteEdit> routeRedo = new List<RouteEdit>();
    int strokeStartCount;
    [SerializeField] Vector2 spawn = new Vector2(float.NaN, float.NaN);
    bool placingSpawn;

    // FARE İMLECİ HALKANIN ÜSTÜNDE GİZLENİYOR. İki imleç birden (ok + halka) nereye
    // vuracağını belirsizleştiriyor. Unity'nin `MouseCursor` listesinde "yok" değeri
    // olmadığı için saydam 1×1 bir imleç kullanılıyor.
    Texture2D blankCursor;
    bool cursorHidden;
    string info = "", report = "", stats = "";
    readonly HashSet<string> openHelp = new HashSet<string>();

    // --- geri alma
    struct Edit { public int x0, z0, w, d; public float[] before, after; }
    readonly List<Edit> undoStack = new List<Edit>();
    readonly List<Edit> redoStack = new List<Edit>();
    const int UndoLimit = 40;
    float[] strokeSnapshot;
    int sx0, sz0, sx1, sz1;

    [MenuItem("To The Summit/Arazi/Dağ Yapımı", false, 12)]
    static void Open()
    {
        var w = GetWindow<MountainBuilderWindow>("Dağ Yapımı");
        w.minSize = new Vector2(620f, 760f);
        w.Show();
    }

    void OnEnable()
    {
        // Fırça halkasının fare hareketinde güncellenmesi için şart.
        wantsMouseMove = true;
        EditorApplication.update += Fly;
        if (h == null && !LoadSculpt(AutoName)) NewFlat();

        // Yükseklik derlemeden sağ çıkmış ama rotalar boşsa, dosyadaki hatları geri
        // oku. `h != null` diye dosyayı hiç açmamak rotaları yok ediyordu.
        else if (h != null && RoutesEmpty()) LoadRoutesOnly(AutoName);
    }

    void OnDisable()
    {
        EditorApplication.update -= Fly;
        keysDown.Clear();

        // Derleme ve pencere kapanışı aynı yoldan geçiyor: çalışma burada diske iniyor,
        // açılışta geri okunuyor.
        if (h != null) SaveSculpt(AutoName);
        ShowSystemCursor();
        if (blankCursor != null) { DestroyImmediate(blankCursor); blankCursor = null; }
        if (prev != null) { prev.Cleanup(); prev = null; }
        if (mesh != null) { DestroyImmediate(mesh); mesh = null; topoBuilt = false; }
        if (mat != null) { DestroyImmediate(mat); mat = null; }
    }

    // ==================================================================== UI

    void OnGUI()
    {
        HandleShortcuts();
        DrawToolbar();
        DrawViewport();
        DrawInfoBar();

        tab = (Tab)GUILayout.Toolbar((int)tab, TabNames, GUILayout.Height(24f));

        scroll = EditorGUILayout.BeginScrollView(scroll);
        switch (tab)
        {
            case Tab.Brush: DrawBrushTab(); break;
            case Tab.Ops: DrawOpsTab(); break;
            case Tab.Mask: DrawMaskTab(); break;
            case Tab.Route: DrawRouteTab(); break;
            case Tab.Measure: DrawMeasureTab(); break;
            case Tab.Save: DrawSaveTab(); break;
        }
        EditorGUILayout.EndScrollView();
    }

    void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            bool routeMode = tab == Tab.Route;
            using (new EditorGUI.DisabledScope((routeMode ? routeUndo.Count : undoStack.Count) == 0))
                if (GUILayout.Button("↶ Geri", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                { if (routeMode) RouteUndo(); else Undo(); }
            using (new EditorGUI.DisabledScope((routeMode ? routeRedo.Count : redoStack.Count) == 0))
                if (GUILayout.Button("↷ İleri", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                { if (routeMode) RouteRedo(); else Redo(); }

            GUILayout.Space(10f);
            // SIFIRLAMA HER ZAMAN ERİŞİLEBİLİR. Izgara pencerenin alanında yaşıyor ve
            // derleme onu sıfırlamıyor; varsayılan değişince eski düzlem bellekte kalıyor.
            if (GUILayout.Button("Boş düzlem", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                NewFlat();
            plainM = EditorGUILayout.FloatField(plainM, EditorStyles.toolbarTextField,
                                                GUILayout.Width(44f));
            GUILayout.Label("m", EditorStyles.miniLabel, GUILayout.Width(12f));

            GUILayout.Space(10f);
            GUILayout.Label("dikey abartma", EditorStyles.miniLabel, GUILayout.Width(76f));
            vScale = GUILayout.HorizontalSlider(vScale, 1f, 4f, GUILayout.Width(70f));
            GUILayout.Label("görünüm payı", EditorStyles.miniLabel, GUILayout.Width(74f));
            viewShare = GUILayout.HorizontalSlider(viewShare, 0.25f, 0.75f, GUILayout.Width(70f));

            GUILayout.Space(10f);
            GUILayout.Label($"hız {flySpeed:F1} km/s", EditorStyles.miniLabel, GUILayout.Width(78f));
            if (GUILayout.Button("kamerayı sıfırla", EditorStyles.toolbarButton, GUILayout.Width(96f)))
            { flyPos = new Vector3(0f, 12f, -26f); yaw = 35f; pitch = 30f; flySpeed = 1.2f; Repaint(); }

            GUILayout.FlexibleSpace();
            GUILayout.Label("WASD uç · Q/E alçal-yüksel · Shift hızlı · sağ tık bak · "
                            + "tekerlek hız · sol tık boya",
                            EditorStyles.miniLabel);
        }
    }

    /// Bölüm başlığı + ⓘ. Açıklama katlanır: her zaman açık olsaydı panel okunmaz
    /// olurdu, hiç olmasaydı hangi vidanın ne yaptığı bilinmezdi.
    void Section(string title, string help)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            bool on = openHelp.Contains(title);
            if (GUILayout.Button(on ? "ⓘ kapat" : "ⓘ nedir?", EditorStyles.miniButton,
                                 GUILayout.Width(74f)))
            {
                if (on) openHelp.Remove(title); else openHelp.Add(title);
            }
        }
        if (openHelp.Contains(title)) EditorGUILayout.HelpBox(help, MessageType.Info);
    }

    static float Slider(string label, string tip, float v, float lo, float hi)
        => EditorGUILayout.Slider(new GUIContent(label, tip), v, lo, hi);

    static int IntSlider(string label, string tip, int v, int lo, int hi)
        => EditorGUILayout.IntSlider(new GUIContent(label, tip), v, lo, hi);

    // ------------------------------------------------------------- fırça

    void DrawBrushTab()
    {
        Section("Dağ anatomisi — nereye ne çizilir",
            "Bir dağ rastgele tümsek değil; parçaları belli bir düzende durur.\n\n"
            + "ZİRVE  — tek nokta. Sırtların birleştiği yer.\n"
            + "SIRT   — zirveden aşağı, parmak gibi dışa açılan hatlar. Üç-beş tane.\n"
            + "         Tırmanış rotası buradan gider (25-30°).\n"
            + "VADİ   — İKİ SIRTIN ARASI. Yukarıda dar ve dik, aşağı indikçe genişler.\n"
            + "         Asla yokuş yukarı gitmez, asla sırtı kesmez.\n"
            + "BOYUN  — iki zirve arasındaki çökük. Düzleştir fırçasıyla yapılır.\n"
            + "ETEK   — kütlenin ovaya indiği kuşak. Eğim burada hızla düşer.\n"
            + "OVA    — hafif tepecikli düz. Oyun burada başlıyor.\n\n"
            + "SIRA: önce kütle (Yükselt), sonra sırtlar, sonra aralarına vadiler, "
            + "sonra aşınma, en son yüzey dokusu.\n\n"
            + "KURAL: her vadinin iki yanında birer sırt olmalı. Sırt-vadi-sırt-vadi "
            + "diye dolanır; bu düzen bozulursa dağ yapay görünür.");

        Section("Fırça",
            "Sol tıkla araziye boyarsın. Halka nereye vuracağını gösterir; halkanın "
            + "şekli fırçanın şeklidir.\n\n"
            + "Yarıçap büyük olduğunda kütle, küçük olduğunda ayrıntı çalışırsın. "
            + "Sırt ve Vadi fırçaları uzatılmış (En/boy 3-5) kullanılır: Açı'yı "
            + "çizeceğin yöne çevir, sonra sürükle.");

        int b = GUILayout.SelectionGrid((int)brush, BrushNames, 3, GUILayout.Height(60f));
        if (b != (int)brush) { brush = (BrushKind)b; Repaint(); }
        EditorGUILayout.HelpBox(BrushHelp[(int)brush], MessageType.None);

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            radiusM = Slider("Yarıçap (m)", "Fırçanın etki mesafesi.", radiusM, 60f, 8000f);
            if (GUILayout.Button("−", EditorStyles.miniButtonLeft, GUILayout.Width(22f)))
                radiusM = Mathf.Max(60f, radiusM * 0.8f);
            if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(22f)))
                radiusM = Mathf.Min(8000f, radiusM * 1.25f);
        }

        strength = Slider("Güç", "Bir vuruşta taşınan miktar.", strength, 0.02f, 1f);
        hardness = Slider("Sertlik",
            "0 = kenarı tamamen yumuşak tümsek. 1 = keskin kenarlı disk; arazide "
            + "basamak bırakır.", hardness, 0f, 1f);
        aspect = Slider("En/boy",
            "1 = daire. Büyütünce fırça bir eksende uzar — sırt ve vadi çekmenin yolu.",
            aspect, 1f, 8f);
        using (new EditorGUI.DisabledScope(aspect <= 1.01f))
            angleDeg = Slider("Açı (derece)", "Uzatmanın yönü.", angleDeg, 0f, 180f);

        EditorGUILayout.LabelField($"etki alanı  {radiusM:F0} × {radiusM / aspect:F0} m  ·  "
                                   + $"hücre {CellM:F1} m", EditorStyles.miniLabel);
    }

    // ------------------------------------------------------------- işlemler

    void DrawOpsTab()
    {
        EditorGUILayout.HelpBox(
            "Buradaki işlemler TÜM araziye uygulanır — ya da Maske sekmesinde bir maske "
            + "seçtiysen yalnız onun gösterdiği yere.", MessageType.None);

        opSeed = EditorGUILayout.IntField(
            new GUIContent("Tohum", "Gürültü ve aşınmanın rastgeleliği. Aynı tohum aynı "
            + "sonucu verir; beğenmediğini değiştirip tekrar dene."), opSeed);

        EditorGUILayout.Space(6f);
        Section("Termal aşınma",
            "Gevşek malzeme duruş açısının üstünde durmaz, kayar. Dik yüzleri indirir "
            + "ama SIRT HATLARINI ve vadi ağını korur — bulanıklaştırmadan farkı bu.\n\n"
            + "Duruş açısı gerçek kayada 30-40 derece. Düşürmek dağı yürünür yapar.");
        opTalus = Slider("Duruş açısı (derece)", "Bu açının üstündeki yüzler akar.", opTalus, 25f, 55f);
        opRate = Slider("Hız", "Tur başına taşınan pay.", opRate, 0.02f, 0.5f);
        opIters = IntSlider("Tur", "Arttıkça yamaçlar duruş açısına yaklaşır.", opIters, 1, 120);
        if (GUILayout.Button("Termal aşınmayı uygula", GUILayout.Height(24f)))
            RunOp(g => TerrainOps.Thermal(g, Grid, CellM, opTalus, opRate, opIters, Mask()));

        EditorGUILayout.Space(8f);
        Section("Hidrolik aşınma",
            "Yağmur damlaları eğim yönünde akar, hızlandıkça malzeme çözer, yavaşlayınca "
            + "çökeltir. Termal aşınmanın veremediği şeyi verir: VADİ AĞI — dallanan "
            + "oluklar, birikinti yelpazeleri, keskinleşen sırtlar.\n\n"
            + "Termal malzemeyi komşuya taşır ve düzler; hidrolik uzağa taşır ve oyar. "
            + "İkisi ayrı olgu.");
        hydDroplets = IntSlider("Damla sayısı", "Çok olursa daha çok oluk, daha uzun sürer.",
                                hydDroplets, 5000, 400000);
        hydSteps = IntSlider("Damla ömrü", "Bir damlanın atacağı adım.", hydSteps, 8, 128);
        hydInertia = Slider("Atalet",
            "Düşük değer damlayı her adımda en dik yöne kilitler ve pürüzsüz yamaçta "
            + "paralel oluklar (dikey taranmış çizgiler) bırakır. Yükseltmek savrulma "
            + "payı verir, oluklar birleşip dallanır.", hydInertia, 0f, 0.95f);
        hydBrush = IntSlider("Aşınma fırçası (hücre)",
            "Bir damlanın kazıdığı alanın yarıçapı. Küçük değer tek hücrelik oluk, "
            + "yani taraklanma; geniş değer komşu olukları birleştirir.", hydBrush, 1, 10);
        hydCapacity = Slider("Taşıma kapasitesi", "Damlanın taşıyabileceği malzeme.",
                             hydCapacity, 0.5f, 16f);
        hydErode = Slider("Çözme", "Malzemeyi koparma hızı.", hydErode, 0.02f, 1f);
        hydDeposit = Slider("Çökeltme", "Malzemeyi bırakma hızı.", hydDeposit, 0.02f, 1f);
        hydEvap = Slider("Buharlaşma", "Damlanın ömrünü kısaltır.", hydEvap, 0.001f, 0.1f);
        hydGravity = Slider("Yerçekimi", "Hızlanma katsayısı.", hydGravity, 0.5f, 12f);
        if (GUILayout.Button("Hidrolik aşınmayı uygula", GUILayout.Height(24f)))
            RunOp(g => TerrainOps.Hydraulic(g, Grid, CellM, hydDroplets, hydSteps, hydInertia,
                                            hydCapacity, hydErode, hydDeposit, hydEvap,
                                            hydGravity, opSeed, hydBrush, Mask()));

        EditorGUILayout.Space(8f);
        Section("Fraktal gürültü",
            "Çok ölçekli doku. Her oktav bir öncekinin yarısı dalga boyunda ve döndürülmüş "
            + "olarak biner — hizalı oktavlar ızgara deseni üretir.\n\n"
            + "Dalga boyu 2 hücrenin (58 m) altına inen oktavlar OTOMATİK kesilir: ızgara "
            + "onları taşıyamaz, istenirse zikzağa döner. Bu proje o hatayla bir gün yaktı.");
        noiseWl = Slider("Dalga boyu (m)", "En kaba oktavın boyu.", noiseWl, 120f, 8000f);
        noiseOct = IntSlider("Oktav", "Kaç kademe ince detay.", noiseOct, 1, 10);
        noiseAmp = Slider("Genlik (m)", "Toplam kabartı yüksekliği.", noiseAmp, 2f, 800f);
        noisePers = Slider("Sönüm", "Her oktavın genlik payı.", noisePers, 0.2f, 0.8f);
        noiseLac = Slider("Sıklık artışı", "Her oktavda dalga boyu bölücüsü.", noiseLac, 1.6f, 3f);
        if (GUILayout.Button("Gürültü ekle", GUILayout.Height(24f)))
            RunOp(g => TerrainOps.FractalNoise(g, Grid, CellM, noiseWl, noiseOct, noiseAmp,
                                               noisePers, noiseLac, opSeed, Mask()));

        EditorGUILayout.Space(8f);
        Section("Bükme (warp)",
            "Araziyi yatayda kendi gürültüsüyle büker. Düzgün biçimleri organikleştirir: "
            + "dairesel etek dalgalı kıyıya, düz sırt kıvrımlı sırta döner.");
        warpWl = Slider("Bükme ölçeği (m)", "Kıvrımların boyu.", warpWl, 300f, 12000f);
        warpAmp = Slider("Bükme miktarı (m)", "Ne kadar kayacağı.", warpAmp, 10f, 2000f);
        if (GUILayout.Button("Bük", GUILayout.Height(24f)))
            RunOp(g => TerrainOps.Warp(g, Grid, CellM, warpWl, warpAmp, opSeed, Mask()));

        EditorGUILayout.Space(8f);
        Section("Teras / katmanlaşma",
            "Kotu basamaklara oturtur — tortul kaya katmanları, aşınmış plato kenarları. "
            + "Keskinlik 1'e yaklaştıkça basamak dikleşir.");
        terraceStep = Slider("Basamak yüksekliği (m)", "İki teras arası kot farkı.",
                             terraceStep, 10f, 600f);
        terraceSharp = Slider("Keskinlik", "0 = görünmez, 1 = dik duvar.", terraceSharp, 0f, 1f);
        if (GUILayout.Button("Teras uygula", GUILayout.Height(24f)))
            RunOp(g => TerrainOps.Terrace(g, Grid, terraceStep, terraceSharp, Mask()));

        EditorGUILayout.Space(8f);
        Section("Keskinleştir / yumuşat",
            "Belirli bir ölçekteki kabartıyı güçlendirir (kazanç > 1) ya da söndürür "
            + "(< 1). Aşınmadan sonra yumuşamış detayı geri getirmek için.");
        sharpRadius = Slider("Ölçek (m)", "Hangi boydaki kabartı etkilenecek.", sharpRadius, 60f, 2000f);
        sharpGain = Slider("Kazanç", "1 = değişiklik yok.", sharpGain, 0.2f, 3f);
        if (GUILayout.Button("Uygula", GUILayout.Height(24f)))
            RunOp(g => TerrainOps.Sharpen(g, Grid, CellM, sharpRadius, sharpGain, Mask()));

        EditorGUILayout.Space(8f);
        Section("Etek geçişi",
            "Dağa DOKUNMAZ. Arazinin dış bandındaki kabartıyı ovaya indirir; eteğin "
            + "arena kenarına çarpmadan, dizsiz inmesini sağlar.\n\n"
            + "Mesafe KARE olarak ölçülür (Çebişev), yarıçap olarak değil: arena kare ve "
            + "dairesel bir halka köşeleri yüksek bırakır.\n\n"
            + "Kalan kabartı 0 olursa ova cam gibi düz ve yapay olur; 0.1-0.2 hafif "
            + "tepecikli düz bırakır.");
        apronInner = Slider("Başlangıç (m)", "Bu mesafeye kadar hiç dokunulmaz — dağın "
            + "kütlesi burada bitmeli.", apronInner, 3000f, 14000f);
        apronOuter = Slider("Bitiş (m)", "Burada ova kotuna tam iner. Arena yarısı 15000 m.",
            apronOuter, 5000f, 15000f);
        apronKeep = Slider("Kalan kabartı", "Dış uçta hayatta kalan kabartı payı.",
            apronKeep, 0f, 0.5f);
        if (GUILayout.Button("Etek geçişini uygula", GUILayout.Height(24f)))
            RunOp(g => TerrainOps.Apron(g, Grid, CellM, apronInner, apronOuter, plainM, apronKeep));

        EditorGUILayout.Space(8f);
        Section("Ovayı yumuşat",
            "Belirli bir kotun ALTINDAKİ kabartıyı küçültür. Düzleştirmez — tepecikler "
            + "kalır, boyları düşer. Sabit bir kota oturtmak ovayı masa gibi yapıyor.");
        calmBelow = Slider("Kot eşiği (m)", "Bunun altı yumuşar, üstü hiç etkilenmez.",
            calmBelow, 50f, 3000f);
        calmFeather = Slider("Geçiş (m)", "Eşiğin yumuşama bandı.", calmFeather, 20f, 1000f);
        calmKeep = Slider("Kalan kabartı", "0 = düz, 1 = dokunma.", calmKeep, 0f, 1f);
        calmScale = Slider("Tepecik ölçeği (m)", "Bu boydan büyük şekiller korunur.",
            calmScale, 100f, 2000f);
        if (GUILayout.Button("Ovayı yumuşat", GUILayout.Height(24f)))
            RunOp(g => TerrainOps.CalmLowland(g, Grid, CellM, calmBelow, calmFeather,
                                              calmKeep, calmScale));

        EditorGUILayout.Space(8f);
        Section("Kot aralığı",
            "Bütün araziyi verilen alt-üst kota yeniden eşler. Dağın boyunu değiştirmenin "
            + "doğru yolu: kabartı ORANLARINI korur, biçimi bozmaz.");
        remapMin = Slider("En alçak (m)", "Ovanın kotu.", remapMin, 0f, 2000f);
        remapMax = Slider("En yüksek (m)", "Zirvenin kotu.", remapMax, 500f, MaxM);
        if (GUILayout.Button("Kotu yeniden eşle", GUILayout.Height(24f)))
            RunOp(g => TerrainOps.Remap(g, remapMin, remapMax, Mask()));

        EditorGUILayout.Space(8f);
        Section("Başlangıç kütlesi",
            "Boş düzlemde işe başlamak için merkeze bir kütle koyar. Kesit ovaya TEĞET "
            + "iner (açıyla çarpmaz), yani dağ ile ova arasında diz oluşmaz — "
            + "'dağ absürt bir anda yükseliyor' hissi o dizden gelir.");
        coneRadius = Slider("Taban yarıçapı (m)", "Kütlenin ovaya indiği mesafe.",
                            coneRadius, 1000f, 15000f);
        coneHeight = Slider("Yükseklik (m)", "Merkeze eklenecek kot.", coneHeight, 100f, MaxM);
        if (GUILayout.Button("Kütle ekle", GUILayout.Height(24f)))
            RunOp(g => TerrainOps.Cone(g, Grid, CellM, (Grid - 1) * 0.5f, (Grid - 1) * 0.5f,
                                       coneRadius, coneHeight, Mask()));
    }

    // ------------------------------------------------------------- maske

    void DrawMaskTab()
    {
        Section("Maske",
            "İşlemlerin NEREYE uygulanacağını sınırlar. Örnek: yalnız 3000 m üstüne kar "
            + "oluğu oymak, yalnız dik yüzleri aşındırmak, yalnız sırtları "
            + "keskinleştirmek.\n\n"
            + "Önizlemeyi açarsan maske sarıyla boyanır.");

        var k = (MaskKind)EditorGUILayout.EnumPopup(
            new GUIContent("Tür", "Maskenin neye göre kurulacağı."), maskKind);
        if (k != maskKind) { maskKind = k; maskCache = null; meshDirty = true; }

        EditorGUI.BeginChangeCheck();
        switch (maskKind)
        {
            case MaskKind.Height:
                maskLo = Slider("Alt kot (m)", "Bunun altı maskelenmez.", maskLo, 0f, MaxM);
                maskHi = Slider("Üst kot (m)", "Bunun üstü maskelenmez.", maskHi, 0f, MaxM);
                maskFeather = Slider("Geçiş (m)", "Kenar yumuşaklığı; sert eşik kontur çizgisi bırakır.",
                                     maskFeather, 1f, 800f);
                break;
            case MaskKind.Slope:
                maskSlopeLo = Slider("Alt eğim (derece)", "", maskSlopeLo, 0f, 90f);
                maskSlopeHi = Slider("Üst eğim (derece)", "", maskSlopeHi, 0f, 90f);
                maskSlopeFeather = Slider("Geçiş (derece)", "", maskSlopeFeather, 0.5f, 20f);
                break;
            case MaskKind.Ridges:
            case MaskKind.Valleys:
                maskCurveRadius = Slider("Ölçek (m)",
                    "Hangi boydaki sırt/vadi seçilecek.", maskCurveRadius, 60f, 3000f);
                maskCurveStrength = Slider("Eşik (m)",
                    "Bu kadar dışbükeylikte maske tam açılır.", maskCurveStrength, 2f, 300f);
                break;
        }
        if (EditorGUI.EndChangeCheck()) { maskCache = null; if (maskPreview) meshDirty = true; }

        bool p = EditorGUILayout.ToggleLeft("Maskeyi önizle (sarı)", maskPreview);
        if (p != maskPreview) { maskPreview = p; meshDirty = true; }
    }

    float[] Mask()
    {
        if (maskKind == MaskKind.None) return null;
        if (maskCache != null) return maskCache;

        switch (maskKind)
        {
            case MaskKind.Height:
                maskCache = TerrainOps.MaskByHeight(h, Grid, maskLo, maskHi, maskFeather); break;
            case MaskKind.Slope:
                maskCache = TerrainOps.MaskBySlope(h, Grid, CellM, maskSlopeLo, maskSlopeHi,
                                                   maskSlopeFeather); break;
            case MaskKind.Ridges:
                maskCache = TerrainOps.MaskByCurvature(h, Grid, CellM, maskCurveRadius, true,
                                                       maskCurveStrength); break;
            case MaskKind.Valleys:
                maskCache = TerrainOps.MaskByCurvature(h, Grid, CellM, maskCurveRadius, false,
                                                       maskCurveStrength); break;
        }
        return maskCache;
    }

    // ------------------------------------------------------------- ölçüm

    void DrawRouteTab()
    {
        Section("Rota",
            "Sol tık araziye nokta koyar, seçili hatta eklenir. Sağ tık kamerayı çevirir "
            + "(nokta koymaz).\n\n"
            + "Noktalar yalnız YATAY konum tutuyor; kot her çizimde araziden okunuyor, "
            + "yani dağı yeniden yontarsan hat yeni zemine kendiliğinden oturur.");

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Doğuş (spawn)", GUILayout.Width(110f));
            GUILayout.Label(float.IsNaN(spawn.x) ? "konmadı"
                            : $"({spawn.x * 1000f:F0}, {spawn.y * 1000f:F0}) m  ·  "
                              + $"kot {HeightAtKm(spawn.x, spawn.y):F0} m");
            GUILayout.FlexibleSpace();
            bool was = placingSpawn;
            placingSpawn = GUILayout.Toggle(placingSpawn, was ? "tıkla…" : "yerleştir",
                                            EditorStyles.miniButton, GUILayout.Width(80f));
        }

        EditorGUILayout.Space(6f);
        for (int i = 0; i < paths.Length; i++)
        {
            var pth = paths[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                // Renk kutusu: listedeki hat ile 3B'deki çizgi aynı renkte.
                var sw = GUILayoutUtility.GetRect(14f, 14f, GUILayout.Width(14f));
                EditorGUI.DrawRect(sw, pth.color);

                bool on = activePath == i && !placingSpawn;
                if (GUILayout.Toggle(on, $"{pth.name}  ({pth.pts.Count})",
                                     EditorStyles.miniButton) && !on)
                { activePath = i; placingSpawn = false; }

                using (new EditorGUI.DisabledScope(pth.pts.Count == 0))
                {
                    if (GUILayout.Button("yumuşat", EditorStyles.miniButton, GUILayout.Width(64f)))
                    { SmoothPath(pth); Repaint(); }
                    if (GUILayout.Button("son nokta", EditorStyles.miniButton, GUILayout.Width(72f)))
                    { pth.pts.RemoveAt(pth.pts.Count - 1); Repaint(); }
                    if (GUILayout.Button("temizle", EditorStyles.miniButton, GUILayout.Width(60f)))
                    { pth.pts.Clear(); Repaint(); }
                }
            }
        }

        EditorGUILayout.Space(6f);
        routeSpacingM = EditorGUILayout.Slider(
            new GUIContent("Nokta aralığı (m)", "Hattın örnekleme sıklığı. Küçük değer "
            + "daha akıcı çizgi, daha çok nokta."), routeSpacingM, 5f, 200f);
        routeRadiusM = EditorGUILayout.Slider(
            new GUIContent("Yol yarı genişliği (m)", "Kaydedilirken her noktaya yazılır. "
            + "3.2 yazıyorsa yol 6.4 metre."), routeRadiusM, 0.5f, 20f);

        EditorGUILayout.Space(6f);
        var a = paths[activePath];
        if (a.pts.Count > 1)
        {
            float len = 0f, gain = 0f, loss = 0f;
            for (int i = 1; i < a.pts.Count; i++)
            {
                var p0 = a.pts[i - 1]; var p1 = a.pts[i];
                len += Vector2.Distance(p0, p1);
                float d = HeightAtKm(p1.x, p1.y) - HeightAtKm(p0.x, p0.y);
                if (d > 0f) gain += d; else loss -= d;
            }
            EditorGUILayout.HelpBox(
                $"{a.name}:  yatay {len:F2} km  ·  çıkış {gain:F0} m  ·  iniş {loss:F0} m  ·  "
                + $"ortalama eğim {Mathf.Atan(gain / Mathf.Max(len * 1000f, 1f)) * Mathf.Rad2Deg:F1}°",
                MessageType.None);
        }
    }

    /// Hattı yumuşatır: elle çizilen çizgide fare titremesi kalıyor ve hat testere
    /// gibi okunuyor. Uç noktalar sabit kalıyor — hattın nerede başlayıp bittiği
    /// kullanıcının kararı.
    static void SmoothPath(RoutePath pth)
    {
        if (pth.pts.Count < 3) return;

        var src = new List<Vector2>(pth.pts);
        for (int i = 1; i < src.Count - 1; i++)
            pth.pts[i] = (src[i - 1] + src[i] * 2f + src[i + 1]) * 0.25f;
    }

    void DrawMeasureTab()
    {
        Section("Ölçüm",
            "Göz kararı yerine sayı. Rota şartı: sırtlar 25-30 derece, duvarlar 50-60, "
            + "ve yürünür pay yeterli olmalı.\n\n"
            + "Ölçüm çalışma ızgarasında (29.3 m) yapılıyor; gerçek arazi 7.32 m olduğu "
            + "için ince ölçekte biraz daha dik çıkacak.");

        if (GUILayout.Button("Ölç", GUILayout.Height(26f))) Measure();
        if (!string.IsNullOrEmpty(report)) EditorGUILayout.TextArea(report, EditorStyles.label);
    }

    void Measure()
    {
        var bands = new (float lo, float hi, string name)[]
            { (0, 3, "kütle 0-3 km"), (3, 6, "yamaç 3-6 km"), (6, 9, "etek 6-9 km"), (9, 15, "ova 9-15 km") };
        var lists = new List<float>[bands.Length];
        for (int i = 0; i < bands.Length; i++) lists[i] = new List<float>();

        float top = 0f, low = float.MaxValue, wide = 0f, edge = 0f;
        float c = (Grid - 1) * 0.5f;

        for (int z = 0; z < Grid; z++)
        for (int x = 0; x < Grid; x++)
        {
            float m = h[z * Grid + x];
            if (m > top) top = m;
            if (m < low) low = m;
            if (z == 0 || x == 0 || z == Grid - 1 || x == Grid - 1) edge = Mathf.Max(edge, m);

            float km = Mathf.Sqrt((x - c) * (x - c) + (z - c) * (z - c)) * CellM / 1000f;
            if (m > low + 100f) wide = Mathf.Max(wide, km);

            int xm = Mathf.Max(x - 1, 0), xp = Mathf.Min(x + 1, Grid - 1);
            int zm = Mathf.Max(z - 1, 0), zp = Mathf.Min(z + 1, Grid - 1);
            float gx = (h[z * Grid + xp] - h[z * Grid + xm]) / ((xp - xm) * CellM);
            float gz = (h[zp * Grid + x] - h[zm * Grid + x]) / ((zp - zm) * CellM);
            float deg = Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;

            for (int b = 0; b < bands.Length; b++)
                if (km >= bands[b].lo && km < bands[b].hi) { lists[b].Add(deg); break; }
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendFormat("zirve {0:F0} m · taban {1:F0} m · kabartı {2:F0} m\n", top, low, top - low);
        sb.AppendFormat("dağ genişliği {0:F1} km · kenar kotu {1:F0} m\n\n", wide * 2f, edge);
        sb.AppendLine("bant           ortanca eğim   yürünür (<30°)");
        for (int b = 0; b < bands.Length; b++)
        {
            if (lists[b].Count == 0) continue;
            lists[b].Sort();
            int walk = 0;
            foreach (var d in lists[b]) if (d < 30f) walk++;
            sb.AppendFormat("{0,-14} {1,8:F1}°   {2,10:F0}%\n", bands[b].name,
                            lists[b][lists[b].Count / 2], 100f * walk / lists[b].Count);
        }
        report = sb.ToString().TrimEnd();
        Repaint();
    }

    // ------------------------------------------------------------- kaydet

    void DrawSaveTab()
    {
        Section("Zemin",
            "Boş düzlem her şeyi siler ve tek kotta bir ova bırakır. Araziden oku, "
            + "sahnedeki mevcut dağı yükleyip üstünde çalışmanı sağlar.");
        plainM = Slider("Ova kotu (m)", "Oyunun başlayacağı kot.", plainM, 0f, 2000f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Boş düzlem", GUILayout.Height(24f))) NewFlat();
            if (GUILayout.Button("Araziden oku", GUILayout.Height(24f))) LoadFromTerrain();
        }

        EditorGUILayout.Space(10f);
        Section("İnce detay (kaydetme anında)",
            "Yontma ızgarası 29.3 m; arazi 7.32 m. Aradaki bant yontarken var olamıyor "
            + "ve dağ yakından çıplak görünüyor. Bu geçiş onu arazinin kendi "
            + "çözünürlüğünde dolduruyor — Nyquist sınırına (14.6 m) kadar.\n\n"
            + "Dik yamaçta çok, düz ovada az uygulanır. Sonda bir termal geçiş "
            + "eklenen dokunun hiçbir yerde duruş açısını aşmamasını garantiliyor.");

        fineDetail = EditorGUILayout.ToggleLeft("İnce detay eklensin", fineDetail);
        using (new EditorGUI.DisabledScope(!fineDetail))
        {
            fineWavelength = EditorGUILayout.Slider(
                new GUIContent("Doku ölçeği (m)", "En kaba oktavın dalga boyu."),
                fineWavelength, 80f, 1200f);
            fineOctaves = EditorGUILayout.IntSlider(
                new GUIContent("Oktav", "Kaç kademe. Nyquist altı otomatik kesilir."),
                fineOctaves, 1, 8);
            fineAmplitude = EditorGUILayout.Slider(
                new GUIContent("Genlik (m)", "Toplam kabartı. Büyütmek kaya yüzeyini "
                + "gürültülü yapar."), fineAmplitude, 2f, 120f);
            fineSteepBias = EditorGUILayout.Slider(
                new GUIContent("Dik yamaç eğilimi", "1 = yalnız dik yerler doku alır, "
                + "0 = her yer eşit."), fineSteepBias, 0f, 1f);
        }

        EditorGUILayout.Space(10f);
        Section("Sahneyi sıfırla",
            "Sahnedeki araziyi dümdüz yapar ve dikey tavanı bu pencereyle eşitler. "
            + "Yontulan çalışma etkilenmez — o ayrı dosyada duruyor. "
            + "Kaydet-uygula sonrası iki yapı üst üste görünüyorsa önce bunu çalıştır.");
        if (GUILayout.Button("Sahnedeki araziyi düzleştir", GUILayout.Height(24f)))
            FlattenScene();

        EditorGUILayout.Space(10f);
        Section("Çalışma dosyaları",
            "Yontulan ızgara ham olarak diske yazılır (1025², float32, kayıpsız). Pencere "
            + "her kapanışta ve her KAYDET'te kendini otomatik kaydeder, açılışta geri "
            + "yükler — derleme artık çalışmayı silmiyor.\n\n"
            + "Ada kaydedersen birden çok dağ saklayabilirsin.");

        using (new EditorGUILayout.HorizontalScope())
        {
            sculptName = SanitizeName(EditorGUILayout.TextField("Ad", sculptName));
            if (GUILayout.Button("Kaydet", GUILayout.Width(70f)))
            { SaveSculpt(sculptName, force: true); info = $"çalışma kaydedildi: {sculptName}"; }
        }

        if (Directory.Exists(SculptDir))
        {
            foreach (var f in Directory.GetFiles(SculptDir, "*.bytes"))
            {
                string n = Path.GetFileNameWithoutExtension(f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(n == AutoName ? $"{n}  (otomatik)" : n);
                    if (GUILayout.Button("Yükle", GUILayout.Width(60f))) LoadSculpt(n);
                    using (new EditorGUI.DisabledScope(n == AutoName))
                        if (GUILayout.Button("Sil", GUILayout.Width(50f)))
                        { AssetDatabase.DeleteAsset(f.Replace("\\", "/")); }
                }
            }
        }

        EditorGUILayout.Space(10f);
        Section("Kaydet",
            "Çalışma ızgarası 4097²'ye büyütülür, araziye yazılır, yükseklik haritası "
            + "PNG olarak kaydedilir ve yüzey haritaları (normal, ufuk, birikinti) bayat "
            + "ilan edilir — kurulum onları yeniden pişirir.");
        if (GUILayout.Button("KAYDET VE UYGULA", GUILayout.Height(36f))) SaveAndApply();
        if (!string.IsNullOrEmpty(info)) EditorGUILayout.HelpBox(info, MessageType.None);
    }

    // ================================================================ işlem koşucusu

    /// Her toplu işlem geri alınabilir ve süresi ölçülüyor: hangi vidanın pahalı
    /// olduğunu bilmeden ayarlanmıyor.
    void RunOp(System.Action<float[]> op)
    {
        var snap = (float[])h.Clone();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        op(h);
        for (int i = 0; i < h.Length; i++) h[i] = Mathf.Clamp(h[i], 0f, MaxM);
        PushEdit(0, 0, Grid - 1, Grid - 1, snap);
        maskCache = null;
        meshDirty = true;
        info = $"işlem {sw.ElapsedMilliseconds} ms";
        Repaint();
    }

    // ================================================================ geri alma

    static readonly KeyCode[] FlyKeys =
    { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.Q, KeyCode.E,
      KeyCode.Space, KeyCode.LeftShift, KeyCode.LeftAlt };

    void HandleShortcuts()
    {
        Event e = Event.current;

        if (e.type == EventType.KeyDown && (e.control || e.command))
        {
            // Hangi yığın: rota sekmesindeyken rota, diğerlerinde yükseklik.
            if (e.keyCode == KeyCode.Z)
            { if (tab == Tab.Route) RouteUndo(); else Undo(); e.Use(); return; }
            if (e.keyCode == KeyCode.Y)
            { if (tab == Tab.Route) RouteRedo(); else Redo(); e.Use(); return; }
        }

        // UÇUŞ TUŞLARI YUTULUYOR. Yutulmazsa Unity onları kendi kısayolları sayıyor
        // (Space sahne penceresini oynatıyor, A hepsini seçiyor).
        if (e.type == EventType.KeyDown && System.Array.IndexOf(FlyKeys, e.keyCode) >= 0)
        {
            if (keysDown.Add(e.keyCode)) lastFlyTick = EditorApplication.timeSinceStartup;
            e.Use();
        }
        else if (e.type == EventType.KeyUp && System.Array.IndexOf(FlyKeys, e.keyCode) >= 0)
        {
            keysDown.Remove(e.keyCode);
            e.Use();
        }
    }

    void RouteUndo()
    {
        if (routeUndo.Count == 0) return;
        var ed = routeUndo[routeUndo.Count - 1];
        routeUndo.RemoveAt(routeUndo.Count - 1);

        var list = paths[ed.path].pts;
        int n = Mathf.Min(ed.pts.Count, list.Count);
        list.RemoveRange(list.Count - n, n);

        routeRedo.Add(ed);
        Repaint();
    }

    void RouteRedo()
    {
        if (routeRedo.Count == 0) return;
        var ed = routeRedo[routeRedo.Count - 1];
        routeRedo.RemoveAt(routeRedo.Count - 1);

        paths[ed.path].pts.AddRange(ed.pts);
        routeUndo.Add(ed);
        Repaint();
    }

    void PushEdit(int x0, int z0, int x1, int z1, float[] snapshot)
    {
        int w = x1 - x0 + 1, d = z1 - z0 + 1;
        if (w <= 0 || d <= 0) return;

        var ed = new Edit { x0 = x0, z0 = z0, w = w, d = d,
                            before = new float[w * d], after = new float[w * d] };
        for (int z = 0; z < d; z++)
        for (int x = 0; x < w; x++)
        {
            int src = (z0 + z) * Grid + (x0 + x);
            ed.before[z * w + x] = snapshot[src];
            ed.after[z * w + x] = h[src];
        }
        undoStack.Add(ed);
        if (undoStack.Count > UndoLimit) undoStack.RemoveAt(0);
        redoStack.Clear();
        dirtySinceSave = true;
    }

    void Blit(Edit ed, float[] src)
    {
        for (int z = 0; z < ed.d; z++)
        for (int x = 0; x < ed.w; x++)
            h[(ed.z0 + z) * Grid + (ed.x0 + x)] = src[z * ed.w + x];
        maskCache = null;
        meshDirty = true;
        Repaint();
    }

    void Undo()
    {
        if (undoStack.Count == 0) return;
        var ed = undoStack[undoStack.Count - 1];
        undoStack.RemoveAt(undoStack.Count - 1);
        Blit(ed, ed.before);
        redoStack.Add(ed);
    }

    void Redo()
    {
        if (redoStack.Count == 0) return;
        var ed = redoStack[redoStack.Count - 1];
        redoStack.RemoveAt(redoStack.Count - 1);
        Blit(ed, ed.after);
        undoStack.Add(ed);
    }

    // ================================================================ görünüm

    void DrawViewport()
    {
        float hgt = Mathf.Max(240f, position.height * viewShare);
        Rect r = GUILayoutUtility.GetRect(10f, 10000f, hgt, hgt);
        HandleViewInput(r);

        if (Event.current.type != EventType.Repaint) return;
        if (r.width < 1f || r.height < 1f) return;

        if (prev == null) prev = new PreviewRenderUtility();
        if (mesh == null || meshDirty) BuildMesh();
        else UpdatePaintedRegion();
        DecayHeat();

        prev.BeginPreview(r, GUIStyle.none);
        var cam = prev.camera;
        camRot = Quaternion.Euler(pitch, yaw, 0f);
        camPos = flyPos;
        viewRect = r;
        cam.transform.position = camPos;
        cam.transform.rotation = camRot;

        // YAKIN KIRPMA 5 METRE. Birim kilometre olduğu için 1f yazmak 1 KM demekti ve
        // yüzeye yaklaşınca arazi kesiliyordu — "dibine giremiyorum" bundandı.
        cam.nearClipPlane = 0.005f;
        cam.farClipPlane = 300f;
        cam.fieldOfView = camFov;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.13f, 0.15f);
        prev.DrawMesh(mesh, Matrix4x4.Scale(new Vector3(1f, vScale, 1f)), mat, 0);
        cam.Render();
        GUI.DrawTexture(r, prev.EndPreview(), ScaleMode.StretchToFill, false);

        DrawRoutes(r);
        DrawBrushRing(r);

        // Halka çizildiyse sistem imleci gizli, değilse geri geliyor. Pencereden
        // çıkıldığında da geri geliyor (`OnDisable`), yoksa imleç kaybolmuş kalırdı.
        if (cursorValid && tab == Tab.Brush) HideSystemCursor(r);
        else ShowSystemCursor();
    }

    /// Dünya noktasını pencere koordinatına düşürür. Kameranın kendi matrisleri
    /// kullanılmıyor — bkz. `camPos` yorumu.
    bool Project(Vector3 world, Rect r, out Vector2 g)
    {
        g = Vector2.zero;
        Vector3 v = Quaternion.Inverse(camRot) * (world - camPos);
        if (v.z < 0.05f) return false;

        float t = Mathf.Tan(camFov * 0.5f * Mathf.Deg2Rad);
        float aspect = r.width / Mathf.Max(r.height, 1f);
        float ndcX = v.x / (v.z * t * aspect);
        float ndcY = v.y / (v.z * t);

        g = new Vector2(r.x + (ndcX * 0.5f + 0.5f) * r.width,
                        r.y + (0.5f - ndcY * 0.5f) * r.height);
        return true;
    }

    Ray MakeRay(Rect r, Vector2 mouse)
    {
        float u = (mouse.x - r.x) / Mathf.Max(r.width, 1f);
        float v = (mouse.y - r.y) / Mathf.Max(r.height, 1f);
        float t = Mathf.Tan(camFov * 0.5f * Mathf.Deg2Rad);
        float aspect = r.width / Mathf.Max(r.height, 1f);

        var dir = camRot * new Vector3((u * 2f - 1f) * t * aspect, (1f - v * 2f) * t, 1f);
        return new Ray(camPos, dir.normalized);
    }

    /// Rota hatlarını ve doğuş noktasını 3B görünümde çizer. Kot her karede araziden
    /// okunuyor, o yüzden hat yüzeye yapışık kalıyor.
    void DrawRoutes(Rect r)
    {
        Handles.BeginGUI();
        Color old = Handles.color;

        foreach (var pth in paths)
        {
            if (pth.pts.Count == 0) continue;

            var line = new List<Vector3>(pth.pts.Count);
            foreach (var q in pth.pts)
            {
                var w = new Vector3(q.x, HeightAtKm(q.x, q.y) / 1000f * vScale + 0.012f, q.y);
                if (Project(w, r, out Vector2 g)) line.Add(new Vector3(g.x, g.y, 0f));
            }

            if (line.Count > 1)
            {
                Handles.color = new Color(0f, 0f, 0f, 0.6f);
                Handles.DrawAAPolyLine(6f, line.ToArray());
                Handles.color = pth.color;
                Handles.DrawAAPolyLine(3f, line.ToArray());
            }

            foreach (var g in line)
                EditorGUI.DrawRect(new Rect(g.x - 2.5f, g.y - 2.5f, 5f, 5f), pth.color);
        }

        if (!float.IsNaN(spawn.x))
        {
            var w = new Vector3(spawn.x, HeightAtKm(spawn.x, spawn.y) / 1000f * vScale + 0.012f,
                                spawn.y);
            if (Project(w, r, out Vector2 g))
            {
                // Doğuş beyaz artı: hiçbir rota rengiyle karışmıyor.
                Handles.color = Color.black;
                Handles.DrawAAPolyLine(5f, new Vector3(g.x - 9f, g.y, 0f), new Vector3(g.x + 9f, g.y, 0f));
                Handles.DrawAAPolyLine(5f, new Vector3(g.x, g.y - 9f, 0f), new Vector3(g.x, g.y + 9f, 0f));
                Handles.color = Color.white;
                Handles.DrawAAPolyLine(2f, new Vector3(g.x - 9f, g.y, 0f), new Vector3(g.x + 9f, g.y, 0f));
                Handles.DrawAAPolyLine(2f, new Vector3(g.x, g.y - 9f, 0f), new Vector3(g.x, g.y + 9f, 0f));
            }
        }

        Handles.color = old;
        Handles.EndGUI();
    }

    void DrawBrushRing(Rect r)
    {
        if (!cursorValid) return;
        if (tab != Tab.Brush && tab != Tab.Route) return;

        // Rota çizerken halka fırça yarıçapını değil YOLUN GENİŞLİĞİNİ gösteriyor;
        // ikisi farklı büyüklükler ve fırça yarıçapını göstermek yanıltırdı.
        float ringR = tab == Tab.Route ? Mathf.Max(routeRadiusM * 6f, 40f) : radiusM;
        float ringAspect = tab == Tab.Route ? 1f : aspect;

        Handles.BeginGUI();
        Color old = Handles.color;

        const int seg = 56;
        var pts = new List<Vector3>(seg + 1);
        float ca = Mathf.Cos(angleDeg * Mathf.Deg2Rad), sa = Mathf.Sin(angleDeg * Mathf.Deg2Rad);

        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            float ex = Mathf.Cos(a) * ringR / 1000f;
            float ez = Mathf.Sin(a) * ringR / ringAspect / 1000f;
            var w = cursor + new Vector3(ex * ca - ez * sa, 0f, ex * sa + ez * ca);
            w.y = HeightAtKm(w.x, w.z) / 1000f * vScale;
            if (Project(w, r, out Vector2 g)) pts.Add(new Vector3(g.x, g.y, 0f));
        }

        if (pts.Count > 2)
        {
            // İKİ KAT ÇİZGİ: koyu alt katman her zeminde görünür kılıyor, kar üstünde
            // tek sarı çizgi kayboluyordu.
            Handles.color = new Color(0f, 0f, 0f, 0.55f);
            Handles.DrawAAPolyLine(4f, pts.ToArray());
            Handles.color = new Color(1f, 0.85f, 0.15f, 1f);
            Handles.DrawAAPolyLine(2f, pts.ToArray());
        }

        // Merkez artı: fırçanın tam nereye vurduğu.
        if (Project(cursor, r, out Vector2 c0))
        {
            Handles.color = new Color(1f, 0.85f, 0.15f, 1f);
            Handles.DrawAAPolyLine(2f, new Vector3(c0.x - 6f, c0.y, 0f), new Vector3(c0.x + 6f, c0.y, 0f));
            Handles.DrawAAPolyLine(2f, new Vector3(c0.x, c0.y - 6f, 0f), new Vector3(c0.x, c0.y + 6f, 0f));
        }

        Handles.color = old;
        Handles.EndGUI();
    }

    /// WASD uçuşu. Tuşlar `OnGUI`'de toplanıyor ama hareket burada işleniyor: IMGUI
    /// yalnız olay geldiğinde çalışıyor, tuş basılı tutulduğunda tekrar hızına bağlı
    /// kesik kesik ilerliyordu.
    void Fly()
    {
        AutoSaveTick();

        if (keysDown.Count == 0) { lastFlyTick = EditorApplication.timeSinceStartup; return; }
        if (focusedWindow != this) { keysDown.Clear(); return; }

        double now = EditorApplication.timeSinceStartup;
        float dt = Mathf.Clamp((float)(now - lastFlyTick), 0f, 0.1f);
        lastFlyTick = now;

        var rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 fwd = rot * Vector3.forward;
        Vector3 right = rot * Vector3.right;

        Vector3 move = Vector3.zero;
        if (keysDown.Contains(KeyCode.W)) move += fwd;
        if (keysDown.Contains(KeyCode.S)) move -= fwd;
        if (keysDown.Contains(KeyCode.D)) move += right;
        if (keysDown.Contains(KeyCode.A)) move -= right;
        if (keysDown.Contains(KeyCode.E) || keysDown.Contains(KeyCode.Space)) move += Vector3.up;
        if (keysDown.Contains(KeyCode.Q)) move -= Vector3.up;
        if (move.sqrMagnitude < 1e-6f) return;

        float sp = flySpeed;
        if (keysDown.Contains(KeyCode.LeftShift)) sp *= 5f;
        if (keysDown.Contains(KeyCode.LeftAlt)) sp *= 0.15f;

        // HIZ YERE OLAN YÜKSEKLİĞE GÖRE ÖLÇEKLENİYOR. Sabit hız 30 km'lik arenada
        // ikisini birden bozuyordu: yukarıdan bakarken sürünüyor, yüzeye inince
        // fırlıyordu. Yerden 2 km yukarıda tam hız, dibinde altıda bir.
        float ground = HeightAtKm(flyPos.x, flyPos.z) / 1000f * vScale;
        float above = Mathf.Max(0f, flyPos.y - ground);
        sp *= Mathf.Lerp(0.16f, 1f, Mathf.Clamp01(above / 2f));

        flyPos += move.normalized * sp * dt;

        // Arena 30 km; kamera biraz dışına çıkabiliyor ama kaybolmuyor.
        flyPos.x = Mathf.Clamp(flyPos.x, -40f, 40f);
        flyPos.z = Mathf.Clamp(flyPos.z, -40f, 40f);
        flyPos.y = Mathf.Clamp(flyPos.y, -1f, 60f);

        Repaint();
    }

    /// R3 — ÇÖKMEYE KARŞI. `OnDisable` editör çökerse ya da süreç öldürülürse
    /// çalışmıyor; o durumda son kayıttan sonraki her şey giderdi. İki dakikada bir,
    /// yalnız değişiklik varsa yazılıyor.
    void AutoSaveTick()
    {
        if (!dirtySinceSave || h == null) return;

        double now = EditorApplication.timeSinceStartup;
        if (now - lastAutoSave < 120.0) return;
        lastAutoSave = now;

        SaveSculpt(AutoName);
        dirtySinceSave = false;
    }

    void HideSystemCursor(Rect r)
    {
        if (blankCursor == null)
        {
            blankCursor = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            { hideFlags = HideFlags.HideAndDontSave };
            blankCursor.SetPixel(0, 0, new Color(0f, 0f, 0f, 0f));
            blankCursor.Apply();
        }

        EditorGUIUtility.AddCursorRect(r, MouseCursor.CustomCursor);
        if (!cursorHidden)
        {
            Cursor.SetCursor(blankCursor, Vector2.zero, CursorMode.Auto);
            cursorHidden = true;
        }
    }

    void ShowSystemCursor()
    {
        if (!cursorHidden) return;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        cursorHidden = false;
    }

    void HandleViewInput(Rect r)
    {
        Event e = Event.current;

        // LAYOUT OLAYINDA DOKUNMA. IMGUI her karede önce Layout sonra Repaint gönderiyor
        // ve Layout sırasında `GUILayoutUtility.GetRect` gerçek dikdörtgeni değil sahte
        // bir (0,0,1,1) döndürüyor. Buradaki "fare dışarıda" testi onu görüp `cursorValid`
        // bayrağını her karede siliyordu; Repaint geldiğinde imleç hep geçersizdi ve fırça
        // halkası hiç çizilmiyordu. Işın baştan beri çalışıyordu — 338 isabet, 0 çizim.
        if (e.type == EventType.Layout || e.type == EventType.Used) return;

        if (!r.Contains(e.mousePosition)) { cursorValid = false; ShowSystemCursor(); return; }

        if (e.type == EventType.MouseDrag && e.button == 1)
        { yaw += e.delta.x * 0.5f; pitch = Mathf.Clamp(pitch + e.delta.y * 0.5f, 2f, 88f); e.Use(); Repaint(); return; }
        if (e.type == EventType.ContextClick) { e.Use(); return; }
        if (e.type == EventType.ScrollWheel)
        {
            // Tekerlek artık yakınlaştırmıyor, UÇUŞ HIZINI değiştiriyor: mesafeyi WASD
            // belirliyor ve ikisi aynı anda olunca kontrol kayboluyordu.
            flySpeed = Mathf.Clamp(flySpeed * (1f - e.delta.y * 0.12f), 0.05f, 20f);
            e.Use(); Repaint(); return;
        }

        if (tab != Tab.Brush && tab != Tab.Route) { cursorValid = false; return; }

        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
        {
            cursorValid = Raycast(r, e.mousePosition, out cursor);
            Repaint();
        }

        // ROTA SEKMESİNDE SOL TIK BOYAMAZ, HAT ÇİZER. Basılı tutup sürüklemek fırça
        // gibi sürekli nokta ekliyor; tek tek tıklamak uzun bir yolda onlarca tık demekti.
        // Noktalar `routeSpacingM`'den sık konmuyor, yoksa hat binlerce noktaya çıkıyor.
        if (tab == Tab.Route)
        {
            if (e.type == EventType.MouseDown && e.button == 0 && cursorValid)
            {
                if (placingSpawn)
                {
                    spawn = new Vector2(cursor.x, cursor.z);
                    placingSpawn = false;
                }
                else
                {
                    drawingRoute = true;
                    strokeStartCount = paths[activePath].pts.Count;
                    paths[activePath].pts.Add(new Vector2(cursor.x, cursor.z));
                }
                e.Use(); Repaint();
            }
            else if (e.type == EventType.MouseDrag && e.button == 0 && drawingRoute && cursorValid)
            {
                // ARA DOLDURULUYOR. Fare olayları hızlı çekişte seyrek geliyor ve
                // noktalar arasında boşluk kalıyordu — hat fırça değil kırık çizgi
                // görünüyordu. Son noktadan imlece kadar `routeSpacingM` aralıklarla
                // ara noktalar konuyor, yani hız ne olursa olsun aynı sıklık.
                var list = paths[activePath].pts;
                var q = new Vector2(cursor.x, cursor.z);

                if (list.Count == 0) { list.Add(q); }
                else
                {
                    var last = list[list.Count - 1];
                    float distM = Vector2.Distance(last, q) * 1000f;
                    int steps = Mathf.FloorToInt(distM / Mathf.Max(routeSpacingM, 1f));
                    steps = Mathf.Min(steps, 400);   // tek karede kilitlenmeye karşı
                    for (int i = 1; i <= steps; i++)
                        list.Add(Vector2.Lerp(last, q, i / (float)steps));
                }
                e.Use(); Repaint();
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && drawingRoute)
            {
                drawingRoute = false;
                var list = paths[activePath].pts;
                int added = list.Count - strokeStartCount;
                if (added > 0)
                {
                    routeUndo.Add(new RouteEdit
                    {
                        path = activePath,
                        pts = list.GetRange(strokeStartCount, added),
                    });
                    if (routeUndo.Count > UndoLimit) routeUndo.RemoveAt(0);
                    routeRedo.Clear();
                    dirtySinceSave = true;
                }
                e.Use(); Repaint();
            }
            return;
        }

        if (e.type == EventType.MouseDown && e.button == 0 && cursorValid)
        {
            painting = true;
            flattenTarget = HeightAtKm(cursor.x, cursor.z);
            strokeSnapshot = (float[])h.Clone();
            sx0 = sz0 = int.MaxValue; sx1 = sz1 = int.MinValue;
            Paint(cursor); e.Use();
        }
        else if (e.type == EventType.MouseDrag && e.button == 0 && painting && cursorValid)
        { Paint(cursor); e.Use(); }
        else if (e.type == EventType.MouseUp && e.button == 0 && painting)
        {
            painting = false;
            flattenTarget = float.NaN;
            if (strokeSnapshot != null && sx1 >= sx0) PushEdit(sx0, sz0, sx1, sz1, strokeSnapshot);
            strokeSnapshot = null;
            maskCache = null;
            meshDirty = true;
            e.Use();
        }
    }

    bool Raycast(Rect r, Vector2 mouse, out Vector3 hit)
    {
        hit = Vector3.zero;
        if (prev == null) return false;

        Ray ray = MakeRay(r, mouse);

        float t = 0f; bool wasAbove = false;
        for (int i = 0; i < 700; i++)
        {
            t += 0.1f;
            if (t > 220f) return false;
            Vector3 p = ray.origin + ray.direction * t;
            if (Mathf.Abs(p.x) > 16f || Mathf.Abs(p.z) > 16f) { wasAbove = false; continue; }

            float d = p.y - HeightAtKm(p.x, p.z) / 1000f * vScale;
            if (wasAbove && d <= 0f)
            {
                float lo = t - 0.1f, hi = t;
                for (int k = 0; k < 22; k++)
                {
                    float mid = (lo + hi) * 0.5f;
                    Vector3 q = ray.origin + ray.direction * mid;
                    if (q.y - HeightAtKm(q.x, q.z) / 1000f * vScale > 0f) lo = mid; else hi = mid;
                }
                hit = ray.origin + ray.direction * hi;
                hit.y = HeightAtKm(hit.x, hit.z) / 1000f * vScale;
                return true;
            }
            wasAbove = d > 0f;
        }
        return false;
    }

    float HeightAtKm(float xKm, float zKm)
    {
        float fx = (xKm * 1000f + ArenaM * 0.5f) / CellM;
        float fz = (zKm * 1000f + ArenaM * 0.5f) / CellM;
        int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, Grid - 2);
        int z0 = Mathf.Clamp(Mathf.FloorToInt(fz), 0, Grid - 2);
        float tx = Mathf.Clamp01(fx - x0), tz = Mathf.Clamp01(fz - z0);
        float a = Mathf.Lerp(h[z0 * Grid + x0], h[z0 * Grid + x0 + 1], tx);
        float b = Mathf.Lerp(h[(z0 + 1) * Grid + x0], h[(z0 + 1) * Grid + x0 + 1], tx);
        return Mathf.Lerp(a, b, tz);
    }

    // ================================================================ fırça uygulama

    void Paint(Vector3 worldKm)
    {
        int cx = Mathf.RoundToInt((worldKm.x * 1000f + ArenaM * 0.5f) / CellM);
        int cz = Mathf.RoundToInt((worldKm.z * 1000f + ArenaM * 0.5f) / CellM);
        int rad = Mathf.Max(1, Mathf.CeilToInt(radiusM / CellM));

        int x0 = Mathf.Max(0, cx - rad), x1 = Mathf.Min(Grid - 1, cx + rad);
        int z0 = Mathf.Max(0, cz - rad), z1 = Mathf.Min(Grid - 1, cz + rad);
        if (x1 <= x0 || z1 <= z0) return;

        if (x0 < sx0) sx0 = x0;
        if (z0 < sz0) sz0 = z0;
        if (x1 > sx1) sx1 = x1;
        if (z1 > sz1) sz1 = z1;

        // Komşu okuyan fırçalar kopyadan okuyor: yerinde yazılırsa fırçanın gittiği
        // yön sonucu değiştirir (soldan sağa başka, sağdan sola başka).
        int sw = x1 - x0 + 1, sd = z1 - z0 + 1;
        float[] src = null;
        if (brush == BrushKind.Smooth || brush == BrushKind.Erode || brush == BrushKind.Sharpen)
        {
            src = new float[sw * sd];
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
                src[(z - z0) * sw + (x - x0)] = h[z * Grid + x];
        }

        float amp = strength * 40f;
        float ca = Mathf.Cos(-angleDeg * Mathf.Deg2Rad), sa = Mathf.Sin(-angleDeg * Mathf.Deg2Rad);
        float maxStep = Mathf.Tan(38f * Mathf.Deg2Rad) * CellM;

        for (int z = z0; z <= z1; z++)
        for (int x = x0; x <= x1; x++)
        {
            float dx = (x - cx) * CellM, dz = (z - cz) * CellM;
            float ux = dx * ca - dz * sa;
            float uz = (dx * sa + dz * ca) * aspect;
            float d = Mathf.Sqrt(ux * ux + uz * uz) / radiusM;
            if (d > 1f) continue;

            float w = Mathf.Lerp(Mathf.SmoothStep(1f, 0f, d), 1f, hardness);
            int i = z * Grid + x;

            switch (brush)
            {
                case BrushKind.Raise: h[i] += amp * w; break;
                case BrushKind.Lower: h[i] -= amp * w; break;

                case BrushKind.Flatten:
                    if (!float.IsNaN(flattenTarget))
                        h[i] = Mathf.Lerp(h[i], flattenTarget, w * strength);
                    break;

                case BrushKind.Smooth:
                {
                    float sum = 0f; int n = 0;
                    for (int oz = -2; oz <= 2; oz++)
                    for (int ox = -2; ox <= 2; ox++)
                    {
                        int px = x - x0 + ox, pz = z - z0 + oz;
                        if (px < 0 || pz < 0 || px >= sw || pz >= sd) continue;
                        sum += src[pz * sw + px]; n++;
                    }
                    if (n > 0) h[i] = Mathf.Lerp(h[i], sum / n, w * strength);
                    break;
                }

                case BrushKind.Ridge:
                case BrushKind.Valley:
                {
                    // Uzun eksene DİK mesafeye göre keskin kesit: sırt/oluk hattı.
                    float perp = Mathf.Abs(uz) / radiusM;
                    float crest = Mathf.Max(0f, 1f - perp * 3f);
                    float v = amp * w * crest * crest;
                    h[i] += brush == BrushKind.Ridge ? v : -v;
                    break;
                }

                case BrushKind.Erode:
                {
                    int px = x - x0, pz = z - z0;
                    float me = src[pz * sw + px];
                    float move = 0f;
                    for (int k = 0; k < 4; k++)
                    {
                        int ox = k == 0 ? 1 : k == 1 ? -1 : 0;
                        int oz = k == 2 ? 1 : k == 3 ? -1 : 0;
                        int qx = px + ox, qz = pz + oz;
                        if (qx < 0 || qz < 0 || qx >= sw || qz >= sd) continue;
                        float diff = me - src[qz * sw + qx] - maxStep;
                        if (diff > 0f) move += diff;
                    }
                    h[i] -= move * 0.15f * w * strength;
                    break;
                }

                case BrushKind.Sharpen:
                {
                    float sum = 0f; int n = 0;
                    for (int oz = -3; oz <= 3; oz++)
                    for (int ox = -3; ox <= 3; ox++)
                    {
                        int px = x - x0 + ox, pz = z - z0 + oz;
                        if (px < 0 || pz < 0 || px >= sw || pz >= sd) continue;
                        sum += src[pz * sw + px]; n++;
                    }
                    if (n > 0)
                    {
                        float avg = sum / n;
                        h[i] += (h[i] - avg) * w * strength;
                    }
                    break;
                }

                case BrushKind.Noise:
                {
                    // Dalga boyu 4 hücre = 117 m, Nyquist'in (2 hücre) üstünde.
                    float n1 = Mathf.PerlinNoise(x * 0.25f, z * 0.25f) - 0.5f;
                    float n2 = Mathf.PerlinNoise(x * 0.55f + 31.7f, z * 0.55f + 11.3f) - 0.5f;
                    h[i] += (n1 + n2 * 0.5f) * amp * w;
                    break;
                }
            }

            h[i] = Mathf.Clamp(h[i], 0f, MaxM);
        }

        // Görüntü ızgarasındaki karşılığı; bir hücre payı bırakılıyor çünkü normal
        // hesabı komşuyu okuyor.
        float toView = (View - 1) / (float)(Grid - 1);
        pdx0 = Mathf.Min(pdx0, Mathf.FloorToInt(x0 * toView) - 1);
        pdz0 = Mathf.Min(pdz0, Mathf.FloorToInt(z0 * toView) - 1);
        pdx1 = Mathf.Max(pdx1, Mathf.CeilToInt(x1 * toView) + 1);
        pdz1 = Mathf.Max(pdz1, Mathf.CeilToInt(z1 * toView) + 1);

        if (EditorApplication.timeSinceStartup - lastMeshBuild > 0.03)
        {
            lastMeshBuild = EditorApplication.timeSinceStartup;
            Repaint();
        }
    }

    // ================================================================ veri

    void NewFlat()
    {
        var snap = h;
        h = new float[Grid * Grid];
        for (int i = 0; i < h.Length; i++) h[i] = plainM;
        if (snap != null) PushEdit(0, 0, Grid - 1, Grid - 1, snap);
        maskCache = null; meshDirty = true; Repaint();
    }

    void LoadFromTerrain()
    {
        var gen = Object.FindAnyObjectByType<MountainGenerator>();
        if (gen == null) { info = "sahnede arazi yok"; return; }
        var data = gen.GetComponent<Terrain>().terrainData;
        int res = data.heightmapResolution;
        var src = data.GetHeights(0, 0, res, res);

        var snap = h;
        h = new float[Grid * Grid];
        float sc = (res - 1) / (float)(Grid - 1);
        for (int z = 0; z < Grid; z++)
        for (int x = 0; x < Grid; x++)
            h[z * Grid + x] = src[Mathf.Min(res - 1, Mathf.RoundToInt(z * sc)),
                                  Mathf.Min(res - 1, Mathf.RoundToInt(x * sc))] * data.size.y;
        if (snap != null) PushEdit(0, 0, Grid - 1, Grid - 1, snap);
        maskCache = null; meshDirty = true; Repaint();
    }

    /// Izgara tek kotta mı? Kaydetme koruması bunu soruyor.
    bool IsFlat()
    {
        if (h == null || h.Length == 0) return true;
        float first = h[0];
        for (int i = 1; i < h.Length; i++)
            if (Mathf.Abs(h[i] - first) > 0.01f) return false;
        return true;
    }

    /// Var olan dosyayı yedekler. En son üç yedek tutuluyor.
    static void Backup(string path)
    {
        if (!File.Exists(path)) return;

        string dir = System.IO.Path.GetDirectoryName(path);
        string name = System.IO.Path.GetFileNameWithoutExtension(path);
        string ext = System.IO.Path.GetExtension(path);

        for (int i = 3; i > 1; i--)
        {
            string older = $"{dir}/{name}.yedek{i}{ext}";
            string newer = $"{dir}/{name}.yedek{i - 1}{ext}";
            if (File.Exists(older)) File.Delete(older);
            if (File.Exists(newer)) File.Move(newer, older);
        }
        File.Copy(path, $"{dir}/{name}.yedek1{ext}", true);
    }

    /// Dosya adını güvenli hâle getirir.
    ///
    /// Windows'ta `: * ? " &lt; &gt; |` dosya adında yasak. Kullanıcı saat yazdı ("16:50"),
    /// yol geçersiz oldu ve `FileStream` OnGUI'nin ORTASINDA istisna fırlattı — açık bir
    /// yatay bloğun içinde. Düzen dengesi bozuldu, ardından her çizim "Invalid GUILayout
    /// state" verdi ve pencere kullanılamaz hâle geldi.
    static string SanitizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "dag";
        var bad = System.IO.Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (char c in name.Trim())
            sb.Append(System.Array.IndexOf(bad, c) >= 0 ? '_' : c);
        string clean = sb.ToString().Trim(' ', '.');
        return clean.Length == 0 ? "dag" : clean;
    }

    static string SculptPath(string name) => $"{SculptDir}/{SanitizeName(name)}.bytes";

    bool RoutesEmpty()
    {
        if (!float.IsNaN(spawn.x)) return false;
        foreach (var pth in paths) if (pth != null && pth.pts.Count > 0) return false;
        return true;
    }

    /// Dosyadan YALNIZ rota bloğunu okur; yükseklik alanına dokunmaz.
    void LoadRoutesOnly(string name)
    {
        string path = SculptPath(name);
        if (!File.Exists(path)) return;

        try
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                int first = br.ReadInt32();
                if (first >= 0) return;              // eski biçim, rota taşımıyor
                int res = br.ReadInt32();
                if (res != Grid) return;

                br.ReadSingle();                      // ova kotu
                fs.Seek((long)Grid * Grid * 4, SeekOrigin.Current);

                spawn = new Vector2(br.ReadSingle(), br.ReadSingle());
                int n = br.ReadInt32();
                for (int i = 0; i < n; i++)
                {
                    int c = br.ReadInt32();
                    var list = i < paths.Length ? paths[i].pts : null;
                    list?.Clear();
                    for (int k = 0; k < c; k++)
                    {
                        var q = new Vector2(br.ReadSingle(), br.ReadSingle());
                        list?.Add(q);
                    }
                }
            }
            info = "rotalar dosyadan geri okundu";
        }
        catch (System.Exception e)
        {
            // Hata yutulmuyor: rota sessizce kaybolmasın.
            Debug.LogWarning($"Rota bloğu okunamadı ({name}): {e.Message}");
        }
    }

    void SaveSculpt(string name, bool force = false)
    {
        if (h == null) return;
        Directory.CreateDirectory(SculptDir);

        // DÜZ IZGARA DOLU DOSYAYI EZMEZ — AMA YALNIZ OTOMATİK KAYITTA. Koruma bir kez
        // fazla geniş kondu ve kasıtlı düzleştirmeyi de engelledi: kullanıcı düzleştirip
        // kaydediyor, kapanışta yazma reddediliyor, açılışta eski dağ geri geliyordu.
        //
        // `force` = kullanıcı açıkça bastı (KAYDET, ada kaydet). Korumanın işi yalnız
        // KAZA ile boş ızgaranın üstüne yazılmasını önlemek.
        string path = SculptPath(name);
        // Rota çizilmişse dosya artık yalnız yükseklik taşımıyor; düz ızgara diye
        // atlamak çizilen hattı da götürürdü.
        bool hasRoutes = !RoutesEmpty();

        // BOŞ ROTA DOLU DOSYANIN ROTASINI SİLMEZ. Bellekteki hatlar boşsa ama dosyada
        // varsa, önce dosyadakiler geri okunuyor — kaydetme onları taşımalı, silmemeli.
        if (!hasRoutes && File.Exists(path)) LoadRoutesOnly(name);

        if (!force && !hasRoutes && IsFlat() && File.Exists(path)
            && new FileInfo(path).Length > 1024)
        {
            info = $"çalışma DÜZ, {name} üzerine yazılmadı (otomatik kayıt)";
            return;
        }
        Backup(path);

        // DOSYA HATASI OnGUI'Yİ KESMEZ. Kesince açık düzen blokları kapanmıyor ve
        // pencere "Invalid GUILayout state" ile kilitleniyor.
        try
        {
        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var bw = new BinaryWriter(fs))
        {
            // SÜRÜM DAMGASI NEGATİF. Eski dosyalar ilk alan olarak ızgara boyunu (pozitif)
            // yazıyordu; negatif değer yeni biçimi ayırt ediyor ve eskiler hâlâ okunuyor.
            bw.Write(-2);
            bw.Write(Grid);
            bw.Write(plainM);
            for (int i = 0; i < h.Length; i++) bw.Write(h[i]);

            bw.Write(spawn.x); bw.Write(spawn.y);
            bw.Write(paths.Length);
            foreach (var pth in paths)
            {
                bw.Write(pth.pts.Count);
                foreach (var q in pth.pts) { bw.Write(q.x); bw.Write(q.y); }
            }
        }
        }
        catch (System.Exception e)
        {
            info = $"kaydedilemedi ({name}): {e.Message}";
            Debug.LogWarning(info);
            return;
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    bool LoadSculpt(string name)
    {
        string path = SculptPath(name);
        if (!File.Exists(path)) return false;

        // R1 — ÜZERİNE YAZMADAN ÖNCE KURTARMA KOPYASI. "Yükle" düğmesine yanlış basmak
        // o ana kadarki çalışmayı yok ediyordu ve geri alma yığını da temizleniyordu.
        if (h != null && !IsFlat()) SaveSculpt(RescueName, force: true);

        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
        using (var br = new BinaryReader(fs))
        {
            int first = br.ReadInt32();
            int version = first < 0 ? -first : 1;
            int res = first < 0 ? br.ReadInt32() : first;

            // ÇÖZÜNÜRLÜK UYUŞMAZLIĞI SESSİZ GEÇMEZ. Izgara boyu değişirse eski dosya
            // yanlış okunur ve dağ çöp çıkar; açıkça reddediliyor.
            if (res != Grid)
            {
                info = $"çalışma dosyası {res}², pencere {Grid}² — okunmadı";
                return false;
            }
            // R4 — HER ŞEY GEÇİCİYE OKUNUYOR, EN SONDA ATANIYOR. Dosya yarıda kesikse
            // eskiden `h` yeni veriyle, rotalar yarım kalıyordu; artık ya hepsi ya hiçbiri.
            float newPlain = br.ReadSingle();
            var g = new float[Grid * Grid];
            for (int i = 0; i < g.Length; i++) g[i] = br.ReadSingle();

            var newSpawn = new Vector2(float.NaN, float.NaN);
            var newPaths = new List<List<Vector2>>();
            for (int i = 0; i < paths.Length; i++) newPaths.Add(new List<Vector2>());

            if (version >= 2)
            {
                newSpawn = new Vector2(br.ReadSingle(), br.ReadSingle());
                int n = br.ReadInt32();
                for (int i = 0; i < n; i++)
                {
                    int c = br.ReadInt32();
                    for (int k = 0; k < c; k++)
                    {
                        var q = new Vector2(br.ReadSingle(), br.ReadSingle());
                        if (i < paths.Length) newPaths[i].Add(q);
                    }
                }
            }

            h = g;
            plainM = newPlain;
            spawn = newSpawn;
            for (int i = 0; i < paths.Length; i++)
            {
                paths[i].pts.Clear();
                paths[i].pts.AddRange(newPaths[i]);
            }
        }

        undoStack.Clear(); redoStack.Clear();
        routeUndo.Clear(); routeRedo.Clear();
        maskCache = null; meshDirty = true;
        dirtySinceSave = false;
        info = $"çalışma yüklendi: {name}";
        Repaint();
        return true;
    }

    /// Sahnedeki araziyi tek kota indirir ve dikey tavanı pencereyle eşitler.
    /// Yontulan çalışmaya dokunmaz.
    void FlattenScene()
    {
        var gen = Object.FindAnyObjectByType<MountainGenerator>();
        if (gen == null) { info = "sahnede arazi yok"; return; }
        var terrain = gen.GetComponent<Terrain>();
        var data = terrain.terrainData;

        data.heightmapResolution = Export;
        data.size = new Vector3(ArenaM, MaxM, ArenaM);
        data.SetHeights(0, 0, new float[Export, Export]);
        terrain.Flush();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        info = $"sahne düzleştirildi · tavan {MaxM:F0} m";
        ToolLog.Write($"Arazi düzleştirildi, dikey tavan {MaxM:F0} m.");
        Repaint();
    }

    const string RouteAssetPath = "Assets/Settings/MountainRoute.asset";

    /// Doğuşu ve hatları `MountainRoute` asset'ine yazar.
    ///
    /// NEDEN ASSET: sahne kurulumu (`MountainSceneBootstrap.SpawnPose`) oyuncuyu oradan
    /// konumlandırıyor. Pencerede tutulan bir konum Play'e geçmezdi.
    ///
    /// BAKIŞ YÖNÜ HESAPLANIYOR, sorulmuyor: doğuştan arazinin en yüksek noktasına doğru.
    /// Oyun dağa tırmanmak üzerine; oyuncunun ilk karede sırtı dağa dönük olması hiçbir
    /// durumda istenmiyor.
    void SaveRoutes(Terrain terrain)
    {
        var route = AssetDatabase.LoadAssetAtPath<MountainRoute>(RouteAssetPath);
        if (route == null)
        {
            route = ScriptableObject.CreateInstance<MountainRoute>();
            Directory.CreateDirectory("Assets/Settings");
            AssetDatabase.CreateAsset(route, RouteAssetPath);
        }

        route.road.Clear();
        route.branches.Clear();

        for (int i = 0; i < paths.Length; i++)
        {
            var marks = new List<MountainRoute.Mark>(paths[i].pts.Count);
            foreach (var q in paths[i].pts)
                marks.Add(new MountainRoute.Mark { position = ToNorm(q), radius = routeRadiusM });

            if (i == 0) route.road.AddRange(marks);
            else route.branches.Add(new MountainRoute.Branch { name = paths[i].name, marks = marks });
        }

        if (!float.IsNaN(spawn.x))
        {
            route.spawn = ToNorm(spawn);
            route.spawnSet = true;

            // En yüksek noktayı bul ve ona bak.
            int bi = 0; float best = float.MinValue;
            for (int i = 0; i < h.Length; i++) if (h[i] > best) { best = h[i]; bi = i; }
            float sx = ((bi % Grid) * CellM - ArenaM * 0.5f) / 1000f;
            float sz = ((bi / Grid) * CellM - ArenaM * 0.5f) / 1000f;
            route.spawnYaw = Mathf.Atan2(sz - spawn.y, sx - spawn.x) * Mathf.Rad2Deg;
        }

        EditorUtility.SetDirty(route);
    }

    /// Kilometre cinsinden merkez-eksenli konumu araziye göre normalize (0-1) eder.
    static Vector2 ToNorm(Vector2 km)
        => new Vector2(km.x * 1000f / ArenaM + 0.5f, km.y * 1000f / ArenaM + 0.5f);

    /// Arazinin KENDİ çözünürlüğünde ince doku. Yontma ızgarasında var olamayan bant
    /// (14.65 - 60 m) burada doldurulyor.
    ///
    /// İKİ MASKE: dik yerde çok, düzde az (gerçek dağda çıplak kayada doku çoktur,
    /// çimenli etekte azdır) ve ova neredeyse hiç almaz — oyuncunun yürüyeceği yer.
    ///
    /// SONDA TERMAL GEÇİŞ: eklenen doku hiçbir yerde duruş açısını aşmasın diye. Tek
    /// hücrelik sivri bırakmamak kural; sekiz tur yerel eğimi oturtmaya yetiyor,
    /// büyük ölçekli biçime dokunmuyor.
    void AddFineDetail(float[,] norm)
    {
        int n = Export;
        float cell = ArenaM / (n - 1);
        var g = new float[n * n];
        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
            g[z * n + x] = norm[z, x] * MaxM;

        // Eğim ve kot maskesi tek geçişte.
        var mask = new float[n * n];
        float lowRef = plainM + 60f;
        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
        {
            int xm = Mathf.Max(x - 1, 0), xp = Mathf.Min(x + 1, n - 1);
            int zm = Mathf.Max(z - 1, 0), zp = Mathf.Min(z + 1, n - 1);
            float dx = (g[z * n + xp] - g[z * n + xm]) / ((xp - xm) * cell);
            float dz = (g[zp * n + x] - g[zm * n + x]) / ((zp - zm) * cell);
            float deg = Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;

            float steep = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, 38f, deg));
            float high = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lowRef, lowRef + 500f,
                                                                    g[z * n + x]));
            mask[z * n + x] = Mathf.Lerp(1f - fineSteepBias, 1f, steep) * high;
        }

        TerrainOps.FractalNoise(g, n, cell, fineWavelength, fineOctaves, fineAmplitude,
                                0.5f, 2f, 9137, mask);
        TerrainOps.Thermal(g, n, cell, 38f, 0.5f, 8);

        float inv = 1f / MaxM;
        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
            norm[z, x] = Mathf.Clamp01(g[z * n + x] * inv);
    }

    void SaveAndApply()
    {
        var gen = Object.FindAnyObjectByType<MountainGenerator>();
        if (gen == null) { info = "sahnede arazi yok"; return; }

        // Aynı koruma yükseklik haritası için: düz bir ızgarayı yazmak dağı siler ve
        // `Araziyi Yeniden Üret` o düz haritayı araziye uygular.
        if (IsFlat() && !EditorUtility.DisplayDialog(
                "Düz arazi kaydedilecek",
                "Çalışma ızgarası tek kotta. Kaydedersen sahnedeki arazi düzleşir."
                + "\n\nDevam edilsin mi?",
                "Evet, düz kaydet", "Vazgeç"))
        {
            info = "kaydetme iptal edildi";
            return;
        }

        var terrain = gen.GetComponent<Terrain>();
        var data = terrain.terrainData;

        var big = new float[Export, Export];
        float inv = 1f / MaxM;
        float sc = (Grid - 1) / (float)(Export - 1);
        for (int z = 0; z < Export; z++)
        for (int x = 0; x < Export; x++)
        {
            float fx = x * sc, fz = z * sc;
            int x0 = Mathf.Min(Grid - 2, (int)fx), z0 = Mathf.Min(Grid - 2, (int)fz);
            float tx = fx - x0, tz = fz - z0;
            float a = Mathf.Lerp(h[z0 * Grid + x0], h[z0 * Grid + x0 + 1], tx);
            float b = Mathf.Lerp(h[(z0 + 1) * Grid + x0], h[(z0 + 1) * Grid + x0 + 1], tx);
            big[z, x] = Mathf.Clamp01(Mathf.Lerp(a, b, tz) * inv);
        }

        if (fineDetail) AddFineDetail(big);

        data.heightmapResolution = Export;
        data.size = new Vector3(ArenaM, MaxM, ArenaM);
        data.SetHeights(0, 0, big);
        terrain.Flush();

        SurfaceMapBaker.Invalidate();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        SaveRoutes(terrain);
        SaveSculpt(AutoName, force: true);
        dirtySinceSave = false;
        // KURULUM AYNI ADIMDA KOŞUYOR. Haritaları bayat ilan edip bırakmak yetmiyordu:
        // tazeleyen bir şey olmadığı için doğru gölgelendirme ancak Play'e girilip
        // çıkılınca görülüyordu.
        EditorUtility.DisplayProgressBar("Dağ Yapımı", "Yüzey haritaları pişiriliyor...", 0.8f);
        try { MountainSceneBootstrap.Rebuild(); }
        finally { EditorUtility.ClearProgressBar(); }

        info = "kaydedildi · arazi, yüzey haritaları ve doğuş güncellendi";
        ToolLog.Write("Dağ yapımı araziye yazıldı ve kurulum koşturuldu.");
        Repaint();
    }

    // ================================================================ örgü

    /// Yalnız fırçanın değdiği dikdörtgeni yeniden hesaplar. Tüm alanı taramak 263 bin
    /// köşe demek ve fırça takılıyordu.
    void UpdatePaintedRegion()
    {
        if (pdx1 < pdx0 || vVerts == null || mesh == null) return;

        float cellKm = ArenaM / 1000f / (View - 1);
        float sc = (Grid - 1) / (float)(View - 1);
        var m = maskPreview ? Mask() : null;

        int x0 = Mathf.Max(0, pdx0), x1 = Mathf.Min(View - 1, pdx1);
        int z0 = Mathf.Max(0, pdz0), z1 = Mathf.Min(View - 1, pdz1);
        pdx0 = pdz0 = int.MaxValue; pdx1 = pdz1 = int.MinValue;

        for (int z = z0; z <= z1; z++)
        for (int x = x0; x <= x1; x++)
        {
            int gx = Mathf.Min(Grid - 1, Mathf.RoundToInt(x * sc));
            int gz = Mathf.Min(Grid - 1, Mathf.RoundToInt(z * sc));
            int gi = gz * Grid + gx;
            float val = h[gi];
            int vi = z * View + x;

            // Önceki kotla farkı ize yazılıyor. Ölçek fırçanın kendi gücüne göre:
            // güçlü vuruşta da hafif vuruşta da iz görünür kalıyor.
            float delta = Mathf.Abs(val - vVerts[vi].y * 1000f);
            float scale = Mathf.Max(2f, strength * 40f);
            heat[vi] = Mathf.Clamp01(Mathf.Max(heat[vi], delta / scale));

            vVerts[vi] = new Vector3((x - (View - 1) * 0.5f) * cellKm, val / 1000f,
                                     (z - (View - 1) * 0.5f) * cellKm);

            int xm = Mathf.Max(gx - 1, 0), xp = Mathf.Min(gx + 1, Grid - 1);
            int zm = Mathf.Max(gz - 1, 0), zp = Mathf.Min(gz + 1, Grid - 1);
            float dx = (h[gz * Grid + xp] - h[gz * Grid + xm]) / ((xp - xm) * CellM);
            float dz = (h[zp * Grid + gx] - h[zm * Grid + gx]) / ((zp - zm) * CellM);
            var nrm = new Vector3(-dx, 1f, -dz).normalized;

            float lam = Mathf.Clamp01(Vector3.Dot(nrm, SunDir)) * 0.8f + 0.2f;
            vCols[vi] = VertexColor(gi, val, lam, m, heat[vi]);
        }

        hx0 = Mathf.Min(hx0, x0); hz0 = Mathf.Min(hz0, z0);
        hx1 = Mathf.Max(hx1, x1); hz1 = Mathf.Max(hz1, z1);

        mesh.SetVertices(vVerts);
        mesh.SetColors(vCols);
        mesh.RecalculateBounds();
    }

    /// İzin sönümü. Yalnız izin bulunduğu dikdörtgen yeniden renklendiriliyor; tüm
    /// alanı taramak 263 bin köşe demek ve fırça takılırdı.
    void DecayHeat()
    {
        if (heat == null || hx1 < hx0 || vCols == null || mesh == null) return;

        double now = EditorApplication.timeSinceStartup;
        float dt = Mathf.Clamp((float)(now - lastHeatTick), 0f, 0.25f);
        lastHeatTick = now;
        if (dt <= 0f) return;

        // Yarılanma ~0.35 s: vuruş bitince iz hemen kaybolmuyor ama ekranda da kalmıyor.
        float k = Mathf.Exp(-dt / 0.35f);
        float cellKm = ArenaM / 1000f / (View - 1);
        float sc = (Grid - 1) / (float)(View - 1);
        var m = maskPreview ? Mask() : null;

        float peak = 0f;
        for (int z = hz0; z <= hz1; z++)
        for (int x = hx0; x <= hx1; x++)
        {
            int vi = z * View + x;
            if (heat[vi] <= 0.001f) { heat[vi] = 0f; continue; }

            heat[vi] *= k;
            if (heat[vi] < 0.004f) heat[vi] = 0f;
            peak = Mathf.Max(peak, heat[vi]);

            int gx = Mathf.Min(Grid - 1, Mathf.RoundToInt(x * sc));
            int gz = Mathf.Min(Grid - 1, Mathf.RoundToInt(z * sc));
            int gi = gz * Grid + gx;

            int xm = Mathf.Max(gx - 1, 0), xp = Mathf.Min(gx + 1, Grid - 1);
            int zm = Mathf.Max(gz - 1, 0), zp = Mathf.Min(gz + 1, Grid - 1);
            float dx = (h[gz * Grid + xp] - h[gz * Grid + xm]) / ((xp - xm) * CellM);
            float dz = (h[zp * Grid + gx] - h[zm * Grid + gx]) / ((zp - zm) * CellM);
            var nrm = new Vector3(-dx, 1f, -dz).normalized;
            float lam = Mathf.Clamp01(Vector3.Dot(nrm, SunDir)) * 0.8f + 0.2f;

            vCols[vi] = VertexColor(gi, h[gi], lam, m, heat[vi]);
        }

        mesh.SetColors(vCols);
        if (peak <= 0f) { hx0 = hz0 = int.MaxValue; hx1 = hz1 = int.MinValue; }
        else Repaint();
    }

    void DrawInfoBar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            GUILayout.Label(stats, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(info)) GUILayout.Label(info, EditorStyles.miniLabel);
            if (cursorValid)
                GUILayout.Label($"fırça  ({cursor.x * 1000f:F0}, {cursor.z * 1000f:F0}) m  ·  "
                                + $"kot {HeightAtKm(cursor.x, cursor.z):F0} m",
                                EditorStyles.miniLabel);
        }
    }

    static readonly Vector3 SunDir = new Vector3(0.45f, 0.62f, -0.64f).normalized;

    /// Bir köşenin rengi: kot bandı × Lambert, üstüne maske ve değişim izi.
    /// Üç yerden birden çağrılıyor (tam kurulum, boyanan bölge, iz sönümü) — üç ayrı
    /// kopya olsaydı biri güncellenip öteki unutulurdu.
    Color VertexColor(int gi, float val, float lam, float[] m, float glow)
    {
        float band = Mathf.Clamp01((val - plainM) / 5000f);
        var rock = Color.Lerp(new Color(0.30f, 0.32f, 0.28f),
                              new Color(0.88f, 0.90f, 0.94f), Mathf.SmoothStep(0f, 1f, band));
        var c = rock * lam;
        if (m != null) c = Color.Lerp(c, new Color(1f, 0.85f, 0.15f), m[gi] * 0.65f);
        if (glow > 0.001f) c = Color.Lerp(c, new Color(0.25f, 0.95f, 1f), glow * 0.8f);
        return c;
    }

    void BuildMesh()
    {
        meshDirty = false;
        pdx0 = pdz0 = int.MaxValue; pdx1 = pdz1 = int.MinValue;

        float sizeKm = ArenaM / 1000f;
        float cellKm = sizeKm / (View - 1);
        float sc = (Grid - 1) / (float)(View - 1);
        var m = maskPreview ? Mask() : null;

        if (mesh == null)
        {
            mesh = new Mesh { name = "DagYapimi", indexFormat = IndexFormat.UInt32 };
            mesh.hideFlags = HideFlags.HideAndDontSave;
            mesh.MarkDynamic();
        }
        if (vVerts == null) { vVerts = new Vector3[View * View]; vCols = new Color[View * View]; }
        if (heat == null) heat = new float[View * View];

        var verts = vVerts;
        var cols = vCols;
        var sun = SunDir;

        float top = 0f, low = float.MaxValue;
        for (int z = 0; z < View; z++)
        for (int x = 0; x < View; x++)
        {
            int gx = Mathf.Min(Grid - 1, Mathf.RoundToInt(x * sc));
            int gz = Mathf.Min(Grid - 1, Mathf.RoundToInt(z * sc));
            int gi = gz * Grid + gx;
            float val = h[gi];
            if (val > top) top = val;
            if (val < low) low = val;

            verts[z * View + x] = new Vector3((x - (View - 1) * 0.5f) * cellKm, val / 1000f,
                                              (z - (View - 1) * 0.5f) * cellKm);

            int xm = Mathf.Max(gx - 1, 0), xp = Mathf.Min(gx + 1, Grid - 1);
            int zm = Mathf.Max(gz - 1, 0), zp = Mathf.Min(gz + 1, Grid - 1);
            float dx = (h[gz * Grid + xp] - h[gz * Grid + xm]) / ((xp - xm) * CellM);
            float dz = (h[zp * Grid + gx] - h[zm * Grid + gx]) / ((zp - zm) * CellM);
            var nrm = new Vector3(-dx, 1f, -dz).normalized;

            float lam = Mathf.Clamp01(Vector3.Dot(nrm, sun)) * 0.8f + 0.2f;
            cols[z * View + x] = VertexColor(gi, val, lam, m, 0f);
        }

        // ÜÇGENLER BİR KEZ. Topoloji sabit; her yenilemede yeniden atamak 525 bin
        // indeksi boşuna GPU'ya yollamak demek.
        mesh.SetVertices(verts);
        mesh.SetColors(cols);
        if (!topoBuilt)
        {
            var tris = new int[(View - 1) * (View - 1) * 6];
            int t = 0;
            for (int z = 0; z < View - 1; z++)
            for (int x = 0; x < View - 1; x++)
            {
                int i = z * View + x;
                tris[t++] = i; tris[t++] = i + View; tris[t++] = i + 1;
                tris[t++] = i + 1; tris[t++] = i + View; tris[t++] = i + View + 1;
            }
            mesh.SetTriangles(tris, 0, true);
            topoBuilt = true;
        }
        else mesh.RecalculateBounds();

        if (mat == null)
        {
            // Sahne aydınlatmasından bağımsız: gölgeleme köşe renginde pişiyor. Işıktan
            // etkilenen bir teşhis görünümü yalan söyler.
            mat = new Material(Shader.Find("Hidden/Internal-Colored"))
            { hideFlags = HideFlags.HideAndDontSave };
            mat.SetInt("_SrcBlend", (int)BlendMode.One);
            mat.SetInt("_DstBlend", (int)BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.SetInt("_Cull", (int)CullMode.Back);
        }

        // İSTATİSTİK AYRI ALANDA. `info` işlem mesajlarıyla eziliyordu ve zirve kotu
        // ekranda kalmıyordu.
        stats = $"zirve {top:F0} m · taban {low:F0} m · kabartı {top - low:F0} m · "
              + $"tavan {MaxM:F0} m · arena {sizeKm:F0} km · hücre {CellM:F1} m";
    }
}
