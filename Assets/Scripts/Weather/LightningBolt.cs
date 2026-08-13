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

    /// Ana kanal artı çatallar. Her biri kendi çizgisini taşıyor.
    LineRenderer channel;
    LineRenderer[] branches;
    Light contact;

    Vector3[] points;
    Vector3[] branchPoints;
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

        if (points == null || points.Length != count)
        {
            points = new Vector3[count];
            branchPoints = new Vector3[count];
        }

        if (channel != null) return;

        channel = CreateLine("Channel", settings.boltWidth);

        branches = new LineRenderer[settings.boltBranches];
        for (int i = 0; i < branches.Length; i++)
            branches[i] = CreateLine($"Branch{i}", settings.boltWidth * 0.45f);

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

        Trace(channel, top, foot, settings.boltWaviness, points);

        for (int i = 0; i < branches.Length; i++)
        {
            // Çatal ana kanaldan ayrılır ve **aşağı doğru** gider. Rastgele bir küre
            // yönüne göndermek onları yukarı da savurup düğüme çeviriyordu; boşalma yere
            // iniyor, dallar da onu izliyor.
            //
            // Ayrılma noktası kanalın her yerinden olabilir, yalnızca üst yarısından
            // değil: gerçek boşalma aşağı indikçe de dallanıyor ve tek bölgede toplanan
            // çatallar tepede bir düğüm, altta çıplak bir çizgi bırakıyordu.
            int from = Random.Range(1, points.Length - 3);
            Vector3 start = points[from];

            Vector3 down = (foot - start).normalized;
            Vector3 aside = Vector3.Normalize(Vector3.Cross(down, Random.onUnitSphere));
            Vector3 heading = Vector3.Normalize(down + aside * 0.7f);

            Vector3 end = start + heading * (Vector3.Distance(start, foot)
                                             * settings.boltBranchLength);

            // Çatal ana kanaldan daha düz iner: boşalmanın gücü orada azalmış oluyor
            Trace(branches[i], start, end, settings.boltWaviness * 0.7f, branchPoints);
        }

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

        var tint = settings.flashColor * flicker;
        channel.startColor = channel.endColor = tint;

        foreach (var branch in branches)
            branch.startColor = branch.endColor = tint * 0.7f;
    }

    void Hide()
    {
        active = false;
        SetVisible(false);

        if (contact != null) contact.intensity = 0f;
    }

    void SetVisible(bool visible)
    {
        if (channel == null) return;

        channel.enabled = visible;
        foreach (var branch in branches) branch.enabled = visible;
    }
}
