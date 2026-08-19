using UnityEngine;

/// Yakın çakmalarda buluttan yere inen görünür kanalı çizer.
///
/// Nerede çakıldığına karar vermez: `LightningFlash` yerleştiriyor, burası okuyor. Konumu
/// ikinci kez seçseydi ışık bir yerde, kol başka bir yerde olurdu.
///
/// Yalnızca yakın çakmalarda çizilir. Gerçekte de uzak şimşek kolunu göstermez — araya
/// giren bulut ve hava kanalı yutar, geriye denizin aydınlanması kalır. Kolun görünmesi
/// mesafenin kendisi hakkında bilgi taşıyor, o yüzden mesafeden bağımsız çizilmemeli.
public class LightningBolt : MonoBehaviour
{
    [SerializeField] LightningFlash flash;
    [SerializeField] Terrain terrain;
    [SerializeField] LightningSettings settings;
    [SerializeField] Material material;

    /// Kol bir AĞAÇ: ana kanal ve ondan doğan kuşaklar. Kaç çizgi gerekeceği çakmaya
    /// göre değişiyor (dallanma olasılıksal), o yüzden havuz — her çakmada nesne
    /// yaratmak çöp üretirdi.
    readonly System.Collections.Generic.List<LineRenderer> lines = new();
    int usedLines;
    Light contact;

    /// TEK TAMPON YETİYOR. Dal, ebeveyninin İZLENMİŞ noktalarından doğuyor; ama doğum
    /// noktası tamponu yeniden kullanmadan önce Vector3 olarak KOPYALANIP kuyruğa
    /// yazılıyor. Kuyruk değer taşıyor, tampona referans değil.
    Vector3[] points;

    readonly System.Collections.Generic.Queue<Branch> pending = new();

    /// Bir dalın doğum bilgisi. Ebeveyninden türeyen her şey burada; izleme sırası
    /// geldiğinde bunlardan geometri üretiliyor.
    struct Branch
    {
        public Vector3 from;
        public Vector3 direction;
        public float distance;
        public float width;
        public float waviness;
        public float chance;
        public int generation;
    }
    float elapsed;
    float life;
    bool active;

    public void Bind(LightningFlash source, Terrain ground, LightningSettings tuning,
        Material boltMaterial)
    {
        flash = source;
        terrain = ground;
        settings = tuning;
        material = boltMaterial;
    }

    void OnEnable()
    {
        if (flash == null || terrain == null || settings == null || material == null)
            throw new System.InvalidOperationException(
                $"{nameof(LightningBolt)}: bağımlılıklar atanmadı.");

        Build();
        flash.Placed += OnPlaced;
        Hide();
    }

    void OnDisable()
    {
        flash.Placed -= OnPlaced;
        Hide();
    }

    /// Çizgiler ve değme ışığı bir kez kurulur; her çakmada yeniden yaratmak çöp üretir.
    ///
    /// İki ayrı koşul, tek bayrak değil. Diziler ayardaki düğüm sayısına bağlı, nesneler
    /// ise yalnızca bir kez kurulmalı. Bunları "kanal var mı" sorusunun arkasına
    /// toplamak, sonradan eklenen bir dizinin sessizce ayrılmadan kalmasına yol açtı.
    void Build()
    {
        int count = settings.boltSegments + 1;

        if (points == null || points.Length != count) points = new Vector3[count];

        if (lines.Count > 0) return;

        // Değme noktasındaki ışık nokta ışık olabiliyor: yönlü olanın aksine burası
        // gerçekten yakında, menzili birkaç yüz metrede kalıyor ve kümelemeyi boğmuyor.
        var lit = new GameObject("Contact");
        lit.transform.SetParent(transform, false);
        contact = lit.AddComponent<Light>();
        contact.type = LightType.Point;
        contact.shadows = LightShadows.None;
        contact.color = settings.flashColor;
        contact.range = settings.groundRange;
        contact.intensity = 0f;
    }

    /// Havuzdan çizgi verir, yoksa yaratır. Tavan `boltMaxLines`.
    LineRenderer TakeLine()
    {
        if (usedLines < lines.Count) return lines[usedLines++];
        if (lines.Count >= settings.boltMaxLines) return null;

        var line = CreateLine($"Bolt{lines.Count}", settings.boltWidth);
        lines.Add(line);
        usedLines++;
        return line;
    }

    LineRenderer CreateLine(string name, float width)
    {
        var holder = new GameObject(name);
        holder.transform.SetParent(transform, false);

        var line = holder.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.material = material;
        line.widthMultiplier = width;
        line.numCapVertices = 2;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;

        return line;
    }

    void OnPlaced(LightningStrike strike)
    {
        if (strike.Distance > settings.boltDistance)
        {
            Hide();
            return;
        }

        // Kanal bulut **tabanından** aşağı iner ve yere değer. Boşalmanın kütle içinde
        // kalan bölümü zaten görünmez; oradan başlatmak kanalı bulutun önüne asıyordu.
        // Bitiş noktasını yamacın kendisi belirliyor, sabit bir kot değil.
        Vector3 top = new(strike.Origin.x, strike.CloudBase, strike.Origin.z);
        Vector3 foot = new(top.x, terrain.SampleHeight(top) + terrain.transform.position.y, top.z);

        GrowTree(top, foot);

        contact.transform.position = foot;
        contact.range = settings.groundRange;

        elapsed = 0f;
        life = strike.Duration;
        active = true;
    }

    /// İki nokta arasına kanal örer.
    ///
    /// Sapma bir **yürüyüş**: her adımın kayması bir öncekini sürdürüyor. Her noktayı düz
    /// çizginin etrafında bağımsız kaydırmak testere dişi üretiyordu — ardışık iki nokta
    /// karşıt uçlara düşüyor, kanal keskin ve düzenli bir zikzağa dönüşüyordu. Gerçek
    /// kanal öyle değil: iyonlaşan yol kendi yönünü taşır, kıvrımları birbirine bağlıdır.
    ///
    /// Kayma kanala dik düzlemde kalıyor; eksen boyunca savurmak kanalı kendi üstüne
    /// katlıyor ve inişi geri sarıyordu. Uçlarda sıfıra iniyor: buluttan çıktığı ve yere
    /// değdiği noktalar sabit kalmalı.
    ///
    /// Tampon dışarıdan veriliyor: ana kanalın noktaları çatallar yerleşirken hâlâ
    /// okunuyor, aynı diziye yazmak onları bozardı.
    void Trace(LineRenderer line, Vector3 from, Vector3 to, float waviness, Vector3[] buffer)
    {
        int count = buffer.Length;

        // Sapma kanalın kendi uzunluğuna göre ölçekleniyor. Mutlak metre verilince
        // çatallar — ki ana kanaldan kat kat kısalar — oransal olarak iki kat kıvrımlı
        // çıkıyor ve keskin kırılma düğüm aralığına yaklaşıp testere dişine dönüyordu.
        float wander = waviness * Vector3.Distance(from, to);

        Vector3 axis = (to - from).normalized;
        Vector3 side = Vector3.Normalize(Vector3.Cross(axis, Vector3.forward));
        if (side.sqrMagnitude < 0.5f) side = Vector3.Normalize(Vector3.Cross(axis, Vector3.right));
        Vector3 other = Vector3.Cross(axis, side);

        // Sönümlü yürüyüş: hıza rastgele bir dürtü ekleniyor, hem hız hem kayma merkeze
        // doğru çekiliyor. Çekme olmadan kanal düz çizgiden koparak uzaklaşıyor.
        //
        // Yürüyüş çağıranın tamponuna yazılıyor, ikinci bir diziye değil. Ayrı bir dizi
        // tutmak döngünün bir dizinin boyuna göre dönüp başkasını indekslemesi demekti;
        // ikisinin uzunluğu iki ayrı yerde belirlendiği için eşleşmedikleri anda patlıyor.
        var drift = Vector2.zero;
        var speed = Vector2.zero;
        float widest = 0f;

        for (int i = 1; i < count - 1; i++)
        {
            speed = speed * 0.65f + Random.insideUnitCircle;
            drift = (drift + speed) * 0.85f;

            buffer[i] = new Vector3(drift.x, drift.y, 0f);
            widest = Mathf.Max(widest, drift.magnitude);
        }

        // Genlik sonradan ölçekleniyor: sönüm katsayıları kaymanın ne kadar büyüyeceğini
        // doğrudan söylemiyor, ayardaki metre değeri ise söylemeli.
        float scale = widest > 0.001f ? wander / widest : 0f;

        // İkinci ölçek: her düğümde bağımsız, keskin ve küçük bir kırılma. Yürüyüş tek
        // başına düşük frekanslı — geniş, yumuşak bir yay veriyor ve kanal cansız
        // duruyor. Yalnızca bu ikinci ölçek kullanılınca da testere dişi çıkıyordu.
        // Gerçek kanalda ikisi birden var: geniş salınımın üstüne binen çıtırtı.
        float kink = wander * settings.boltKink;

        for (int i = 1; i < count - 1; i++)
        {
            float t = (float)i / (count - 1);
            float taper = Mathf.Sin(t * Mathf.PI);

            Vector2 sharp = Random.insideUnitCircle * kink;
            Vector3 broad = buffer[i] * scale;

            float x = (broad.x + sharp.x) * taper;
            float y = (broad.y + sharp.y) * taper;

            buffer[i] = Vector3.Lerp(from, to, t) + side * x + other * y;
        }

        buffer[0] = from;
        buffer[count - 1] = to;

        line.positionCount = count;
        line.SetPositions(buffer);
    }

    void Update()
    {
        if (!active) return;

        // Işık donduruldıysa kanal da donar: ikisi aynı çakmanın parçası
        if (!flash.Held) elapsed += Time.deltaTime;

        if (elapsed >= life)
        {
            Hide();
            return;
        }

        // Kanal parlamanın kendisinden daha kısa yaşar: ışık bulutta dağılıp sönerken
        // kanal çoktan sönmüş oluyor. Kare kare titriyor — boşalma sürekli değil.
        float remaining = 1f - elapsed / life;
        float flicker = flash.Held ? 1f : remaining * remaining * Random.Range(0.55f, 1f);

        SetVisible(true);
        contact.intensity = settings.groundIntensity * flicker;

        // Ana kanal en parlak, her kuşak sönük. Boşalmanın gücü dallandıkça azalıyor;
        // hepsini aynı parlaklıkta çizmek ağacı düz bir tel yumağına çeviriyordu.
        var tint = settings.flashColor * flicker;
        for (int i = 0; i < usedLines; i++)
            lines[i].startColor = lines[i].endColor = tint * lineTint[i];
    }

    void Hide()
    {
        active = false;
        SetVisible(false);

        if (contact != null) contact.intensity = 0f;
    }

    void SetVisible(bool visible)
    {
        for (int i = 0; i < lines.Count; i++)
            lines[i].enabled = visible && i < usedLines;
    }

    /// AĞACI ÜRETİR. Reed & Wyvill: dal ebeveyninden ortalama 16 derece sapar (normal
    /// dağılım), her kuşakta kalınlık/olasılık/uzunluk azalır, kıvrımlılık ARTAR.
    ///
    /// Genişlik-öncelikli kuyruk, özyineleme değil: ağacın büyüklüğü olasılıksal ve
    /// yığın derinliği önceden bilinmiyor. Kuyruk aynı zamanda bütçe tavanını doğal
    /// yerde uyguluyor — tavan dolunca kalan dallar hiç doğmuyor, yarım kalmış bir dal
    /// kalmıyor.
    void GrowTree(Vector3 top, Vector3 foot)
    {
        usedLines = 0;
        pending.Clear();

        pending.Enqueue(new Branch
        {
            from = top,
            direction = (foot - top).normalized,
            distance = Vector3.Distance(top, foot),
            width = settings.boltWidth,
            waviness = settings.boltWaviness,
            chance = settings.boltBranchCount,
            generation = 0,
        });

        while (pending.Count > 0)
        {
            var branch = pending.Dequeue();

            var line = TakeLine();
            if (line == null) break;              // bütçe doldu

            line.widthMultiplier = branch.width;
            EnsureTintCapacity();
            lineTint[usedLines - 1] = Mathf.Pow(0.7f, branch.generation);

            // ANA KANAL YERE DEĞER, dallar havada biter. Kanalın bitiş noktası yamacın
            // kendisi; dalın bitişi yönü ve boyu.
            Vector3 target = branch.generation == 0
                ? foot
                : branch.from + branch.direction * branch.distance;

            Trace(line, branch.from, target, branch.waviness, points);

            if (branch.generation >= settings.boltGenerations) continue;

            // ÇOCUKLAR EBEVEYNİN İZLENMİŞ NOKTALARINDAN doğuyor — düz çizgiden değil.
            // Düz çizgiden doğarlarsa kıvrımlı kanalın yanında havada asılı kalıyorlar.
            // Noktalar KOPYALANIYOR: tampon bir sonraki dalda yeniden yazılacak.
            // BEKLENEN SAYI düğüm başına olasılığa çevriliyor. Aday düğüm sayısı
            // `boltSegments`'e bağlı; olasılığı doğrudan vermek dal sayısını çözünürlüğe
            // bağlıyordu.
            int candidates = points.Length - 2;
            float perNode = candidates > 0 ? branch.chance / candidates : 0f;

            for (int i = 1; i < points.Length - 1; i++)
            {
                if (Random.value >= perNode) continue;

                Vector3 heading = ChildDirection(branch.direction);

                pending.Enqueue(new Branch
                {
                    from = points[i],
                    direction = heading,
                    distance = branch.distance * settings.boltBranchLength,
                    width = branch.width * settings.boltWidthDecay,
                    waviness = branch.waviness * settings.boltWavinessGrowth,
                    chance = branch.chance * settings.boltBranchCountDecay,
                    generation = branch.generation + 1,
                });
            }
        }

        SetVisible(true);
    }

    /// Dalın yönü: ebeveyninden ORTALAMA 16 derece sapar, sapma normal dağılır.
    ///
    /// Sabit açı (eski hâl) her çatalı aynı koniye diziyordu ve ağaç şemsiye gibi
    /// duruyordu. Normal dağılım Reed & Wyvill'in tek ampirik gözlemi: doğadaki dallar
    /// bu değer etrafında toplanıyor, kuyrukta nadiren sert sapanlar var.
    ///
    /// Tavan var çünkü normal dağılımın kuyruğu sınırsız: kırpılmazsa bir dal geri
    /// yukarı, bulutun içine dönebiliyor.
    Vector3 ChildDirection(Vector3 parent)
    {
        float deg = settings.boltBranchAngle + Gaussian() * settings.boltBranchSpread;
        deg = Mathf.Clamp(Mathf.Abs(deg), 1f, settings.boltBranchAngleMax);

        // Sapma ekseni: ebeveyne dik, azimutu rastgele.
        Vector3 axis = Vector3.Cross(parent, Random.onUnitSphere);
        if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(parent, Vector3.right);

        return (Quaternion.AngleAxis(deg, axis.normalized) * parent).normalized;
    }

    /// Box-Muller. Unity'de normal dağılım yok; `Random.value` düzgün dağılıyor ve
    /// düzgün dağılımla 16 derece "ortalama" kurulamaz — ortalama etrafında toplanma
    /// olmaz, bant olur.
    static float Gaussian()
    {
        float u1 = Mathf.Max(Random.value, 1e-6f);
        float u2 = Random.value;
        return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
    }

    float[] lineTint = new float[8];

    void EnsureTintCapacity()
    {
        if (lineTint.Length >= lines.Count) return;
        System.Array.Resize(ref lineTint, Mathf.Max(lines.Count, lineTint.Length * 2));
    }
}
