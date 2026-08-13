using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// ROTA FIRÇASI. Otobüs yolu, doğuş noktası, tırmanış hatları, kamplar ve marketler Scene View'da
/// araziye çizilerek işaretleniyor.
///
/// Neden araç: bunlar sayıyla verilemez. "Doğuş noktası (4210, 186, 9330)" diye bir
/// koordinat yazmak, dağa bakıp "şu sırtın arkasından çıkalım" demenin yerini tutmuyor.
/// Karar araziye bakarak veriliyor, o yüzden işaret de araziye konuyor.
///
/// FIRÇA YARIÇAPI VERİ. Ayrı bir "koridor genişliği" sayısı tutulmuyor: hattı kalın
/// çizersen yol geniş, ince çizersen patika olur; kampı büyük çizersen düzleştirilecek
/// alan büyük olur.
public class RoutePainter : EditorWindow
{
    const string RoutePath = "Assets/Settings/MountainRoute.asset";

    /// Sürüklerken iki nokta arası mesafe, FIRÇA BOYUNA bağlı. Sabit 25 metreydi ve
    /// ince fırçayla eğri çizilemiyordu: yakınlaşıp yol çizerken noktalar arası mesafe
    /// yolun kendisinden yedi kat büyük kalıyor, hat zikzak çıkıyordu.
    ///
    /// Oran 0.6: nokta aralığı yolun genişliğinden dar, yani dönüş yolun kendi
    /// kalınlığından küçük adımlarla örnekleniyor ve eğri okunuyor. Kalın fırçada
    /// aralık kendiliğinden büyüyor, gereksiz nokta birikmiyor.
    float Spacing => Mathf.Max(2f, radius * 0.6f);

    enum Layer { Spawn, Road, Branch, Camp, Shop }

    /// BÖLGE. Başlangıç çevresi (otobüs, doğuş, yaklaşma) ile dağın kendisi ayrı
    /// tutuluyor: ikisi farklı zamanlarda, farklı sorularla çiziliyor ve tek bir düz
    /// katman listesinde karışıyorlardı.
    enum Region { Start, Mountain }

    static readonly Layer[] StartLayers =
        { Layer.Spawn, Layer.Road, Layer.Branch, Layer.Camp, Layer.Shop };

    static readonly string[] StartLayerNames =
        { "Doğuş", "Yol", "Hat", "Kamp", "Market" };

    static readonly Color[] BranchColors =
    {
        new(1f, 0.55f, 0.1f),      // turuncu
        new(0.95f, 0.25f, 0.75f),  // macenta
        new(0.2f, 0.85f, 0.9f),    // camgöbeği
        new(0.95f, 0.3f, 0.25f),   // kırmızı
        new(0.7f, 0.9f, 0.25f),    // fıstık yeşili
        new(0.75f, 0.6f, 1f),      // lila
    };

    /// HATLAR SABİT. "Hat ekle" butonu vardı ve kaldırıldı: hat sayısı bir tasarım
    /// kararı, çizim sırasında verilecek bir şey değil. Değişmesi gerekiyorsa burası
    /// değişir ve eksik olan kendiliğinden açılır.
    static readonly string[] BranchNames = { "Hat 1", "Hat 2", "Hat 3", "Ana hat" };

    static readonly Color SpawnColor = new(0.25f, 1f, 0.35f);
    static readonly Color RoadColor = new(0.9f, 0.88f, 0.82f);
    static readonly Color CampColor = new(0.35f, 0.55f, 1f);
    static readonly Color ShopColor = new(1f, 0.9f, 0.2f);

    MountainRoute route;
    Region region = Region.Start;
    Layer layer = Layer.Spawn;
    int branchIndex;
    float radius = 40f;
    bool painting;

    readonly Dictionary<List<MountainRoute.Mark>, Vector3[]> groundCache = new();

    Vector3 lastPaint;
    bool hasLastPaint;

    [MenuItem("To The Summit/Rota Fırçası", false, 40)]
    static void Open() => GetWindow<RoutePainter>("Rota Fırçası").Show();

    void OnEnable()
    {
        route = LoadOrCreate();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

    static MountainRoute LoadOrCreate()
    {
        var asset = AssetDatabase.LoadAssetAtPath<MountainRoute>(RoutePath);
        if (asset != null)
        {
            EnsureBranches(asset);
            return asset;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(RoutePath));
        asset = CreateInstance<MountainRoute>();

        AssetDatabase.CreateAsset(asset, RoutePath);
        EnsureBranches(asset);
        return asset;
    }

    /// Sabit listedeki her hattın var olduğunu garanti eder. Eksik olan eklenir, var
    /// olan ELLENMEZ — içindeki çizim kaybolmamalı. Fazlası da silinmez: elle silinmiş
    /// bir hattı geri getirmek, çizilmiş bir hattı yok etmekten ucuz.
    static void EnsureBranches(MountainRoute asset)
    {
        bool added = false;

        foreach (string name in BranchNames)
        {
            if (asset.branches.Exists(branch => branch.name == name)) continue;

            asset.branches.Add(new MountainRoute.Branch { name = name });
            added = true;
        }

        if (!added) return;

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
    }

    // ------------------------------------------------------------------ pencere

    void OnGUI()
    {
        if (route == null) route = LoadOrCreate();

        EditorGUILayout.HelpBox(
            "Sol tık: işaretle. Sürükleyerek hat çiz.\n" +
            "Shift + tık: sil.\n" +
            "Ctrl + tekerlek: fırça yarıçapı.\n\n" +
            "Doğuş: bir tık yer, ikinci tık BAKIŞ YÖNÜ.",
            MessageType.None);

        bool next = GUILayout.Toggle(painting, "Fırça açık", "Button", GUILayout.Height(28f));
        if (next != painting)
        {
            painting = next;
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space();
        region = (Region)GUILayout.SelectionGrid((int)region,
            new[] { "BAŞLANGIÇ", "DAĞ" }, 2, GUILayout.Height(24f));

        if (region == Region.Mountain)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Dağ rotası henüz tanımlanmadı. " +
                "Tırmanış hatları, geçitler ve tehlike bölgeleri buraya gelecek — " +
                "başlangıç çevresiyle karışmasın diye ayrı duruyor.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.Space();
        int selected = Mathf.Max(0, System.Array.IndexOf(StartLayers, layer));
        layer = StartLayers[GUILayout.SelectionGrid(selected, StartLayerNames,
            StartLayerNames.Length)];

        if (layer == Layer.Branch)
        {
            var names = new string[route.branches.Count];
            for (int i = 0; i < names.Length; i++) names[i] = route.branches[i].name;

            branchIndex = GUILayout.SelectionGrid(Mathf.Clamp(branchIndex, 0, names.Length - 1),
                names, names.Length);

            var swatch = GUILayoutUtility.GetRect(0f, 6f);
            EditorGUI.DrawRect(swatch, BranchColors[branchIndex % BranchColors.Length]);
        }

        EditorGUILayout.Space();
        radius = EditorGUILayout.Slider("Yarıçap (m)", radius, 5f, 300f);

        EditorGUILayout.Space();
        DrawStatus();

        EditorGUILayout.Space();
        if (GUILayout.Button("Yarıçapları gerçekçi dağıt")) SpreadRadii();
        if (GUILayout.Button("Seçili katmanı temizle")) ClearLayer();

        // Zorunlu değil: çizim kendiliğinden kaydediliyor. Bu buton ham veriyi
        // açıyor — hat adını değiştirmek, tek bir noktayı elle düzeltmek için.
        if (GUILayout.Button("Ham veriyi Inspector'da aç")) Selection.activeObject = route;
    }

    /// Seçili katmanın durumu. Yatayda makul görünen bir hat düşeyde duvar olabiliyor;
    /// eğim çizim anında okunmazsa hat körlemesine çiziliyor.
    ///
    /// YALNIZ KARAR VERDİREN SAYILAR: hattın kaç noktadan
    /// oluştuğu, yarıçapların ham dağılımı gibi iç veriler panelden çıkarıldı — okuyanı
    /// yormaktan başka işe yaramıyorlardı. Kalanların hepsinin bir karşılığı var:
    /// yürünebilir mi, ne kadar sürer, otobüs çıkabilir mi.
    void DrawStatus()
    {
        var terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain == null) return;

        if (layer == Layer.Spawn)
        {
            EditorGUILayout.LabelField(route.spawnSet
                ? $"Başlangıç işaretli — bakış {route.spawnYaw:F0}°"
                : "Başlangıç işaretlenmedi");
            return;
        }

        List<MountainRoute.Mark> marks = SelectedMarks();

        if (layer == Layer.Camp || layer == Layer.Shop)
        {
            string what = layer == Layer.Camp ? "kamp" : "market";
            if (marks.Count == 0)
            {
                EditorGUILayout.LabelField($"Henüz {what} yok");
                return;
            }

            float span = 0f;
            foreach (MountainRoute.Mark mark in marks) span += mark.radius * 2f;
            EditorGUILayout.LabelField($"{marks.Count} {what}", $"ortalama {span / marks.Count:F0} m çapında");
            return;
        }

        if (marks.Count < 2)
        {
            EditorGUILayout.LabelField("Bu hat henüz çizilmedi");
            return;
        }

        float threshold = SteepThreshold();
        RouteProfile.Reading reading = RouteProfile.Measure(terrain, marks, threshold);

        float width = 0f;
        foreach (MountainRoute.Mark mark in marks) width += mark.radius * 2f;
        width /= marks.Count;

        EditorGUILayout.LabelField("Genişlik", $"{width:F1} m");
        EditorGUILayout.LabelField("Uzunluk", $"{reading.length / 1000f:F2} km");
        EditorGUILayout.LabelField("Tırmanış", $"{reading.ascent:F0} m");

        // Yürüme süresi: düz mesafe artı yükselme cezası (600 m/saat tırmanma).
        // Koşu hedefi 90-120 dakika; bu hattın ondan ne kadar yiyeceği burada okunuyor.
        float minutes = reading.length / 2.2f / 60f + reading.ascent / 600f;
        EditorGUILayout.LabelField("Yürüme süresi", $"~{minutes:F0} dakika");

        // Eğim SAYIYLA DEĞİL HÜKÜMLE: "%38" karar verdirmiyor, "otobüs çıkamaz"
        // verdiriyor. Nerede olduğu sahnede kırmızı çiziliyor.
        bool road = layer == Layer.Road;
        bool tooSteep = reading.maxGrade > threshold;

        string verdict = road
            ? (tooSteep ? "Fazla dik — otobüs çıkamaz" : "Otobüs çıkabilir")
            : (tooSteep ? "Dik yerler var — bisikletten inilir" : "Baştan sona bisikletle");

        EditorGUILayout.HelpBox(verdict, tooSteep ? MessageType.Warning : MessageType.Info);

        if (tooSteep)
            EditorGUILayout.LabelField("En dik yokuş",
                $"%{reading.maxGrade * 100f:F0}   ({reading.steepLength:F0} m boyunca)");
    }

    List<MountainRoute.Mark> SelectedMarks() => layer switch
    {
        Layer.Road => route.road,
        Layer.Branch => route.branches[Mathf.Clamp(branchIndex, 0, route.branches.Count - 1)].marks,
        Layer.Camp => route.camps,
        Layer.Shop => route.shops,
        _ => null
    };

    /// Yol otobüs için, hatlar bisiklet için. Yaklaşma bisikletle geçiliyor; yürüyüş
    /// eşiği (%25) bisikletin inip ittiği yokuşu "uygun" gösteriyordu.
    float SteepThreshold() =>
        layer == Layer.Road ? RouteProfile.RoadGrade : RouteProfile.BikeGrade;

    /// TÜM katmanların yarıçaplarını gerçekçi ölçülere oturtur.
    ///
    /// Neden gerekli: fırça tek bir yarıçapla çiziyor ve bütün hat aynı kalınlıkta
    /// çıkıyor. Gerçekte yol ne sabit genişliktedir ne de patika ile aynı: otobüsün
    /// geçtiği toprak yol 7 metre, keçi yolu 2 metre.
    ///
    /// DEĞİŞİM HAT BOYUNCA YUMUŞAK, nokta başına rastgele değil. Nokta başına gürültü
    /// koridoru testere dişine çeviriyor; genişlik onlarca metrede değişir, adım başına
    /// değil. Bu yüzden dalga boyu sekiz nokta (~200 m).
    ///
    /// Tohum NOKTA İNDEKSİNDEN: aynı hatta tekrar basınca aynı sonuç çıkıyor, her
    /// tıklamada koridor yeniden zıplamıyor.
    void SpreadRadii()
    {
        Undo.RecordObject(route, "Yarıçapları dağıt");

        // Bütün katmanlar değişiyor; tek katman geçersiz kılmak yetmez.
        groundCache.Clear();

        // Yarıçap, genişliğin yarısı. Toprak dağ yolu ~7 m geniş, ana patika ~3.5 m,
        // yan patika ~2 m. Kamp bir çadır grubu ve ateş yeri; market tek yapı.
        Spread(route.road, 3.5f, 0.9f);

        // HER HAT KENDİ GENİŞLİĞİNDE. Üçüne aynı taban verilince üç patika birbirinin
        // kopyası oluyordu; gerçekte biri çok kullanılıp genişlemiş, biri neredeyse
        // kaybolmuş olur. Taban hattın indeksinden türüyor: aynı hat her çalıştırmada
        // aynı genişlikte kalıyor, yeni hat eklenince eskiler kaymıyor.
        for (int i = 0; i < route.branches.Count; i++)
        {
            if (route.branches[i].name.Contains("Ana"))
            {
                Spread(route.branches[i].marks, 1.8f, 0.5f);
                continue;
            }

            // Taban aralığa YAYILARAK veriliyor, saf karmayla değil. Karma denendi ve
            // üç hattı 1.14-1.30 arasına düşürdü: sayılar farklı ama gözle aynı.
            // Rastgelelik kümelenir; ayrı görünmesi gereken şeyler ayrı aralıklara
            // KONUR, sonra içlerinde oynatılır.
            //
            // Bölme sabit (6, palet boyu): hat sayısına bölünseydi yeni hat eklenince
            // eskilerin genişliği kayardı. 1.4 - 3.6 m: kaybolmaya yüz tutmuş izden
            // çok yürünmüş yola kadar.
            float slot = ((i % 6) + Hash(i * 977 + 31) * 0.7f) / 5.7f;
            // Alt sınır 0.9: bisikletin geçebileceği en dar tread 1.8 metre.
            float own = Mathf.Lerp(0.9f, 1.9f, slot);
            Spread(route.branches[i].marks, own, own * 0.22f);
        }

        Spread(route.camps, 12f, 4f);
        Spread(route.shops, 7f, 2f);

        Flush();
    }

    static void Spread(List<MountainRoute.Mark> marks, float baseRadius, float variation)
    {
        const int Wavelength = 8;

        for (int i = 0; i < marks.Count; i++)
        {
            // İki ölçek: yumuşak dalga hattın genelini, ince kırıntı tek tek noktaları
            // oynatıyor. İkincisi olmazsa genişlik matematiksel bir sinüs gibi okunuyor.
            float wave = Mathf.PerlinNoise(i / (float)Wavelength, 0.5f) * 2f - 1f;
            float grain = (Hash(i) - 0.5f) * 0.35f;

            MountainRoute.Mark mark = marks[i];
            mark.radius = Mathf.Max(0.5f, baseRadius + (wave + grain) * variation);
            marks[i] = mark;
        }
    }

    /// Tam sayı karması: aynı indeks her çalışmada aynı sayıyı veriyor.
    static float Hash(int value)
    {
        uint h = (uint)value * 2654435761u;
        h ^= h >> 15;
        h *= 2246822519u;
        h ^= h >> 13;
        return (h & 0xffffffu) / 16777216f;
    }

    void ClearLayer()
    {
        Undo.RecordObject(route, "Rota katmanını temizle");

        switch (layer)
        {
            case Layer.Spawn: route.spawnSet = false; break;
            case Layer.Branch: route.branches[branchIndex].marks.Clear(); break;
            case Layer.Road: route.road.Clear(); break;
            case Layer.Camp: route.camps.Clear(); break;
            case Layer.Shop: route.shops.Clear(); break;
        }

        Flush();
    }

    /// İşaret eklendi: asset kirli, ekran tazelensin. DİSKE YAZILMIYOR — bir hat
    /// çizerken saniyede birkaç nokta düşüyor ve her nokta için dosya yazmak editörü
    /// kilitliyor. Yazma darbe bitince (`Flush`).
    void Save()
    {
        // Yalnız DÜZENLENEN katmanın önbelleği atılıyor. Hepsini birden atmak, bir
        // hatta nokta eklerken öteki hatların kotlarını da yeniden okutuyordu.
        List<MountainRoute.Mark> edited = SelectedMarks();
        if (edited != null) groundCache.Remove(edited);

        Redraw();
    }

    /// Önbelleği KORUYAN kayıt. Sürükleyerek çizerken her nokta için bütün hattın
    /// kotları yeniden okunuyordu: bin noktalık bir hatta bu, tek bir darbede yarım
    /// milyon arazi sorgusu demek. Eklenen nokta önbelleğin sonuna yazılıyor.
    void SaveAppended(List<MountainRoute.Mark> target, Vector3 world)
    {
        if (groundCache.TryGetValue(target, out Vector3[] cached))
        {
            if (cached.Length == target.Count - 1)
            {
                var grown = new Vector3[target.Count];
                cached.CopyTo(grown, 0);
                grown[target.Count - 1] = world;
                groundCache[target] = grown;
            }
            else groundCache.Remove(target);
        }

        Redraw();
    }

    void Redraw()
    {
        EditorUtility.SetDirty(route);
        SceneView.RepaintAll();
        Repaint();
    }

    /// Diske yaz. Kirli işaretlemek tek başına yetmiyor: Unity kirli asset'leri kendi
    /// zamanlamasıyla yazıyor ve arada çökme olursa çizim gidiyor. Fırça darbesi
    /// bittiğinde yazmak hem güvenli hem ucuz.
    void Flush()
    {
        Save();
        AssetDatabase.SaveAssetIfDirty(route);
    }

    // -------------------------------------------------------------- scene view

    void OnSceneGUI(SceneView view)
    {
        // Referans BURADA da tazeleniyor. Script yeniden derlendiğinde pencere alanı
        // sıfırlanıyor ve `OnGUI` çalışana kadar boş kalıyordu: sahnede çizim yokmuş
        // gibi görünüyor, oysa veri asset'te duruyor. Pencereye tıklamak gerekiyordu.
        if (route == null) route = LoadOrCreate();

        var terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain == null) return;

        DrawExisting(terrain);
        if (!painting || region != Region.Start) return;

        // Fırça açıkken seçim kilitleniyor: aksi hâlde her tık sahnedeki nesneyi
        // seçiyor ve çizim yarıda kalıyor.
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (!Probe(terrain, out Vector3 point)) return;

        DrawCursor(point);
        view.Repaint();

        HandleShortcuts();
        HandleClicks(terrain, point);
    }

    /// İmleç altındaki arazi noktası. Işın ÇARPIŞMADAN okunuyor: yükseklik haritası
    /// ile çarpışma yüzeyi köşegende birkaç santim ayrışıyor ve işaret gözle konurken
    /// gözün gördüğü yüzey çarpışma yüzeyi.
    static bool Probe(Terrain terrain, out Vector3 point)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        var collider = terrain.GetComponent<TerrainCollider>();

        if (collider != null && collider.Raycast(ray, out RaycastHit hit, 100000f))
        {
            point = hit.point;
            return true;
        }

        point = default;
        return false;
    }

    /// Yarıçap CTRL + TEKERLEK ile. Köşeli parantez denendi ve geri alındı: Türkçe
    /// klavyede o tuşlar AltGr istiyor. Tekerlek her boyama aracında aynı jest ve
    /// klavye düzeninden bağımsız.
    void HandleShortcuts()
    {
        Event e = Event.current;
        if (e.type != EventType.ScrollWheel || !e.control) return;

        radius = Mathf.Clamp(radius * (e.delta.y > 0f ? 1f / 1.15f : 1.15f), 5f, 300f);

        e.Use();
        Repaint();
    }

    void HandleClicks(Terrain terrain, Vector3 point)
    {
        Event e = Event.current;
        if (e.button != 0) return;

        bool down = e.type == EventType.MouseDown;
        bool drag = e.type == EventType.MouseDrag;
        if (!down && !drag && e.type != EventType.MouseUp) return;

        if (e.type == EventType.MouseUp)
        {
            hasLastPaint = false;
            Flush();
            e.Use();
            return;
        }

        if (e.shift)
        {
            if (down) { Erase(terrain, point); Flush(); }
            e.Use();
            return;
        }

        // Doğuş SÜRÜKLENMİYOR: tek nokta, ikinci tık yön veriyor.
        if (layer == Layer.Spawn)
        {
            if (down) { PaintSpawn(terrain, point); Flush(); }
            e.Use();
            return;
        }

        // Sürüklerken aralık kontrolü. Nokta katmanlarında (kamp, market) sürükleme
        // yok: her tık bir işaret.
        bool line = layer == Layer.Road || layer == Layer.Branch;
        if (drag && !line) return;

        if (hasLastPaint && Vector3.Distance(point, lastPaint) < Spacing) return;

        Undo.RecordObject(route, "Rota işaretle");
        var mark = new MountainRoute.Mark
        {
            position = MountainRoute.ToNormalized(point, terrain),
            radius = radius
        };

        List<MountainRoute.Mark> target = SelectedMarks();
        target.Add(mark);

        lastPaint = point;
        hasLastPaint = true;

        SaveAppended(target, new Vector3(point.x, point.y + 1f, point.z));
        e.Use();
    }

    /// Doğuş: işaretli değilse konum, işaretliyse BAKIŞ YÖNÜ. İki ayrı moda gerek yok —
    /// yer belliyken bir daha tıklamanın tek anlamlı karşılığı yön.
    void PaintSpawn(Terrain terrain, Vector3 point)
    {
        Undo.RecordObject(route, "Doğuş işaretle");

        if (!route.spawnSet)
        {
            route.spawn = MountainRoute.ToNormalized(point, terrain);
            route.spawnSet = true;
        }
        else
        {
            Vector3 from = MountainRoute.ToWorld(route.spawn, terrain);
            Vector2 direction = new Vector2(point.x - from.x, point.z - from.z);

            if (direction.sqrMagnitude > 1f)
                route.spawnYaw = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }

        Save();
    }

    /// Fırça çapındaki işaretleri siler. En yakını değil hepsi: üst üste binmiş
    /// noktaları teker teker avlamak çizmekten uzun sürüyordu.
    void Erase(Terrain terrain, Vector3 point)
    {
        Undo.RecordObject(route, "Rota işaretini sil");

        if (layer == Layer.Spawn)
        {
            route.spawnSet = false;
            Save();
            return;
        }

        var target = layer switch
        {
            Layer.Road => route.road,
            Layer.Branch => route.branches[branchIndex].marks,
            Layer.Camp => route.camps,
            _ => route.shops
        };

        for (int i = target.Count - 1; i >= 0; i--)
        {
            Vector3 world = MountainRoute.ToWorld(target[i].position, terrain);
            float dx = world.x - point.x, dz = world.z - point.z;
            if (dx * dx + dz * dz <= radius * radius) target.RemoveAt(i);
        }

        Save();
    }

    // ------------------------------------------------------------------ çizim

    void DrawCursor(Vector3 point)
    {
        Handles.color = layer switch
        {
            Layer.Spawn => SpawnColor,
            Layer.Road => RoadColor,
            Layer.Branch => BranchColors[branchIndex % BranchColors.Length],
            Layer.Camp => CampColor,
            _ => ShopColor
        };

        Handles.DrawWireDisc(point + Vector3.up * 2f, Vector3.up, radius);
    }

    void DrawExisting(Terrain terrain)
    {
        DrawLine(terrain, route.road, RoadColor, RouteProfile.RoadGrade);

        for (int i = 0; i < route.branches.Count; i++)
            DrawBranch(terrain, route.branches[i], BranchColors[i % BranchColors.Length]);

        Handles.color = CampColor;
        foreach (MountainRoute.Mark camp in route.camps)
        {
            Vector3 world = Ground(terrain, camp.position);
            Handles.DrawWireDisc(world, Vector3.up, camp.radius);
            Handles.CubeHandleCap(0, world + Vector3.up * 8f, Quaternion.identity, 16f,
                EventType.Repaint);
        }

        // Market KÜRE, kamp KÜP: renk körlüğünden bağımsız ayırt edilsinler ve uzaktan
        // silueti okunsun.
        Handles.color = ShopColor;
        foreach (MountainRoute.Mark shop in route.shops)
        {
            Vector3 world = Ground(terrain, shop.position);
            Handles.DrawWireDisc(world, Vector3.up, shop.radius);
            Handles.SphereHandleCap(0, world + Vector3.up * 8f, Quaternion.identity, 16f,
                EventType.Repaint);
        }

        if (!route.spawnSet) return;

        Vector3 spawn = Ground(terrain, route.spawn);
        Handles.color = SpawnColor;
        Handles.DrawWireDisc(spawn, Vector3.up, 12f);
        Handles.DrawWireDisc(spawn, Vector3.up, 4f);

        // Bakış yönü: ok gözle okunacak kadar uzun, hattı gölgelemeyecek kadar kısa.
        float yaw = route.spawnYaw * Mathf.Deg2Rad;
        var forward = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
        Handles.ArrowHandleCap(0, spawn + Vector3.up * 3f,
            Quaternion.LookRotation(forward), 90f, EventType.Repaint);
    }

    void DrawBranch(Terrain terrain, MountainRoute.Branch branch, Color color) =>
        DrawLine(terrain, branch.marks, color, RouteProfile.BikeGrade);

    /// Hat çizimi. Eşiği aşan parçalar KIRMIZI: sayı pencerede "%38" diyor ama nerede
    /// olduğunu söylemiyor. Renk yeri gösteriyor, ikisi birlikte hattı düzeltilebilir
    /// kılıyor.
    void DrawLine(Terrain terrain, List<MountainRoute.Mark> marks, Color color,
        float steepThreshold)
    {
        if (marks.Count == 0) return;

        Vector3[] ground = GroundCached(terrain, marks);

        // ÇİZGİ YUMUŞATILIYOR. Noktalar 25 metre aralıklı ve düz parçalarla
        // birleştirilince hat kırık bir zikzak oluyor; oysa çizilen şey bir patika,
        // köşeleri yok. Veri polyline kalıyor, yumuşatma yalnız gösterimde.
        Handles.color = color;
        Handles.DrawAAPolyLine(4f, Smooth(ground));

        // Dik parçalar HAM noktalar arasında ölçülüyor: yumuşatma eğimi hafifçe
        // değiştirir ve uyarı gerçek veriden gelmeli.
        Handles.color = Color.red;
        for (int i = 1; i < ground.Length; i++)
        {
            float run = Vector2.Distance(new Vector2(ground[i - 1].x, ground[i - 1].z),
                                         new Vector2(ground[i].x, ground[i].z));
            if (run < 0.5f) continue;

            if (Mathf.Abs(ground[i].y - ground[i - 1].y) / run > steepThreshold)
                Handles.DrawAAPolyLine(7f, ground[i - 1], ground[i]);
        }

        DrawCorridor(marks, ground, color);
    }

    /// KORİDORUN GERÇEK GENİŞLİĞİ. Merkez çizgisi sabit piksel kalınlığında çiziliyor
    /// ve yarıçapla ilgisi yok; nokta başına halka ise 17.5 km'lik arazide piksel altına
    /// düşüyor. İkisi birlikte "hepsi aynı kalınlıkta" gösteriyordu, oysa veri farklı.
    ///
    /// Bant iki kenar çizgisi olarak: dolgu yüzey uzaktan araziyi boyuyor ve altındaki
    /// eğim okunmaz oluyor.
    static void DrawCorridor(List<MountainRoute.Mark> marks, Vector3[] ground, Color color)
    {
        if (ground.Length < 2) return;

        var left = new Vector3[ground.Length];
        var right = new Vector3[ground.Length];

        for (int i = 0; i < ground.Length; i++)
        {
            // Yön komşulardan: uçlarda tek komşu, ortada ikisinin ortalaması. Tek
            // komşuyla kenar her noktada kırılıyor ve bant testere dişi oluyordu.
            Vector3 before = ground[Mathf.Max(i - 1, 0)];
            Vector3 after = ground[Mathf.Min(i + 1, ground.Length - 1)];

            var forward = new Vector2(after.x - before.x, after.z - before.z);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector2.right;
            forward.Normalize();

            var side = new Vector3(-forward.y, 0f, forward.x) * marks[i].radius;
            left[i] = ground[i] + side;
            right[i] = ground[i] - side;
        }

        Handles.color = new Color(color.r, color.g, color.b, 0.55f);
        Handles.DrawAAPolyLine(2f, Smooth(left));
        Handles.DrawAAPolyLine(2f, Smooth(right));
    }

    /// Catmull-Rom: eğri noktaların HEPSİNDEN geçiyor. Ortalama tabanlı yumuşatma
    /// çizgiyi noktalardan uzaklaştırır ve hat çizdiğin yerden kayar.
    static Vector3[] Smooth(Vector3[] points)
    {
        const int Steps = 4;
        if (points.Length < 3) return points;

        var output = new Vector3[(points.Length - 1) * Steps + 1];
        int w = 0;

        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector3 p0 = points[Mathf.Max(i - 1, 0)];
            Vector3 p1 = points[i];
            Vector3 p2 = points[i + 1];
            Vector3 p3 = points[Mathf.Min(i + 2, points.Length - 1)];

            for (int step = 0; step < Steps; step++)
            {
                float t = step / (float)Steps;
                float t2 = t * t, t3 = t2 * t;

                output[w++] = 0.5f * (2f * p1 + (-p0 + p2) * t
                    + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                    + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
            }
        }

        output[w] = points[points.Length - 1];
        return output;
    }

    /// ZEMİN KOTLARI ÖNBELLEKTE. Her yeniden çizimde nokta başına altı `SampleHeight`
    /// çağrılıyordu (merkez, eğim, iki koridor kenarı) ve altı yüz noktalık bir rotada
    /// bu kare başına üç binden fazla arazi sorgusu demek: fırça gözle görülür şekilde
    /// geride kalıyordu. Kotlar yalnız veri değiştiğinde yeniden okunuyor.
    Vector3[] GroundCached(Terrain terrain, List<MountainRoute.Mark> marks)
    {
        if (groundCache.TryGetValue(marks, out Vector3[] cached)
            && cached.Length == marks.Count)
            return cached;

        var ground = new Vector3[marks.Count];
        for (int i = 0; i < marks.Count; i++) ground[i] = Ground(terrain, marks[i].position);

        groundCache[marks] = ground;
        return ground;
    }

    /// Normalize konumun zemin üstündeki dünya karşılığı. Yükseklik SAKLANMIYOR,
    /// her çizimde araziden okunuyor — arazi değişince işaretler kendiliğinden oturuyor.
    static Vector3 Ground(Terrain terrain, Vector2 normalized)
    {
        Vector3 world = MountainRoute.ToWorld(normalized, terrain);
        world.y = terrain.SampleHeight(world) + terrain.transform.position.y + 1f;
        return world;
    }
}
