using System;
using UnityEngine;

/// BİSİKLET. Projeden bağımsız: rota, arazi, hava ve kar sistemlerini bilmiyor, onlar da
/// bunu bilmiyor. Bağımlılığı yalnız Unity — bir `CharacterController`, bir `LayerMask`,
/// bir ayar asset'i. Başka bir projeye klasör olarak taşınır ve çalışır.
///
/// HIZ TABLODAN DEĞİL FİZİKTEN GELİYOR. "Yolda 5 m/s, patikada 4" diye yazılsaydı her
/// eğim için yeni bir kural gerekirdi ve arazi değiştiğinde hepsi yalan olurdu. Burada
/// sürücünün GÜCÜ veriliyor; hız güç, kütle, eğim ve dirençlerin dengesinden çıkıyor:
///
///     P = v · (Crr·m·g + m·g·sin(eğim) + ½·ρ·CdA·v²)
///
/// Sonuç kendiliğinden doğru davranıyor: düzde hızlı, %10 yokuşta yürüme temposunda,
/// inişte fren gerektirecek kadar hızlı. Tek satır "yokuşta yavaşla" kuralı yok.
///
/// KİNEMATİK, RIGIDBODY DEĞİL. Arazi üstünde tekerlek fiziği kurmak zıplama, takılma ve
/// tahmin edilemez tepme getiriyor; `CharacterController` çarpışmayı çözüyor, hareketi
/// bu bileşen hesaplıyor. Ayrıca bu, oyuncunun yürüyüş kontrolcüsüyle aynı model.
[RequireComponent(typeof(CharacterController))]
public class BikeController : MonoBehaviour
{
    [SerializeField] BikeSettings settings;

    CharacterController controller;
    BikeInput input;

    /// Zemine dik yüzey normali. Eğim ve yatma bundan türüyor.
    Vector3 groundNormal = Vector3.up;

    float verticalSpeed;
    float lean;

    /// Yol boyunca hız (m/s). Geriye gitmek yok: bisiklet geri pedallanmaz.
    public float Speed { get; private set; }

    /// Gidiş yönündeki eğim (oran, 0.10 = %10 yokuş yukarı).
    public float Grade { get; private set; }

    public bool Grounded { get; private set; }

    /// Görsel yatma açısı (derece). Gövde modeli bunu okuyor; kontrolcü kendisi hiçbir
    /// mesh döndürmüyor — görsel ile fizik ayrı kalsın diye.
    public float LeanAngle => lean;

    /// Ayardaki en büyük yatma açısı. Görsel bileşenler yatmayı ORANA çevirmek için
    /// okuyor; ayarın kendisini dışarı açmak, her tüketicinin istediği alanı okumasına
    /// kapı açardı.
    public float MaxLean => settings != null ? settings.maxLean : 1f;

    /// Zeminin yuvarlanma direnci. Oyun dünyası biliyorsa (asfalt, çakıl, kar) buradan
    /// verir; vermezse ayardaki değer geçerli. Bisiklet zemin TÜRLERİNİ bilmiyor,
    /// yalnız sayıyı okuyor.
    public float RollingResistance { get; set; } = -1f;

    /// Ayar asset'i dışarıdan da verilebiliyor: aynı sahnede iki farklı bisiklet.
    public void Bind(BikeSettings source) => settings = source;

    public void SetInput(BikeInput value) => input = value.Sanitised();

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        FitCapsule();
    }

    void OnEnable()
    {
        if (settings == null)
            throw new InvalidOperationException($"{nameof(BikeController)}: ayar atanmadı.");
    }

    /// TEKERLEK YERE OTURTULUYOR. Kapsül modele göre kuruluyor ama modelin kökle arasında
    /// pay kalabiliyor: ölçüldü, kapsülün tabanı çarpışmanın yedi santim üstünde (deri
    /// payı kadar, doğru) iken modelin altı kapsülün otuz beş santim üstündeydi. Yani
    /// fizik doğru yerde duruyor, görüntü havada asılı kalıyordu.
    ///
    /// Kurulum betiği bunu bir kez yapıyor ama sahnede eski konum kalabiliyor; burada her
    /// açılışta yeniden ölçülüyor. Kapsül kapatılıp açılıyor çünkü `CharacterController`
    /// açıkken doğrudan konum ataması bir sonraki karede geri alınıyor.
    void Start()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);

        var ray = new Ray(new Vector3(bounds.center.x, bounds.min.y + 3f, bounds.center.z),
            Vector3.down);

        if (!Physics.Raycast(ray, out RaycastHit hit, 20f, settings.groundLayers,
                QueryTriggerInteraction.Ignore))
            return;

        float gap = bounds.min.y - hit.point.y;
        if (Mathf.Abs(gap) < 0.005f) return;

        controller.enabled = false;
        transform.position -= Vector3.up * gap;
        controller.enabled = true;
    }

    /// ÇARPIŞMA KAPSÜLÜ MODELDEN ÖLÇÜLÜYOR. Kurulum betiği bunu bir kez yazıyordu ve
    /// sahnede kalan eski değerler geçerli oluyordu: kapsülün tabanı modelin altından
    /// kırk santim aşağıda kalınca bisiklet havada duruyor, gölgesi altında bir metre
    /// ötede çıkıyordu.
    ///
    /// Ölçü her açılışta yeniden alınıyor: model değişse de, sahnedeki bileşen eski
    /// kalsa da kapsül modele oturuyor.
    void FitCapsule()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);

        float height = Mathf.Max(0.6f, bounds.size.y);
        float bottom = bounds.min.y - transform.position.y;

        controller.height = height;
        controller.radius = Mathf.Min(0.3f, height * 0.4f);

        // Merkez modelin ALTINDAN ölçülüyor: kök ile modelin tabanı arasında pay varsa
        // kapsül o payı da hesaba katıyor.
        controller.center = new Vector3(0f, bottom + height * 0.5f, 0f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        ReadGround();
        Steer(dt);
        Accelerate(dt);
        Move(dt);
    }

    // ------------------------------------------------------------------- zemin

    /// Zemin normali ve eğim. Işın kontrolcünün TABANINDAN biraz yukarıdan atılıyor:
    /// tam tabandan atılan ışın eğimli zeminde yüzeyin içinde başlıyor ve hiçbir şeye
    /// çarpmıyor.
    void ReadGround()
    {
        Grounded = controller.isGrounded;

        Vector3 origin = transform.position + Vector3.up * (controller.radius + 0.1f);
        float reach = controller.radius + controller.skinWidth + 0.6f;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, reach,
                settings.groundLayers, QueryTriggerInteraction.Ignore))
        {
            groundNormal = hit.normal;
            Grounded = true;
        }
        else groundNormal = Vector3.up;

        // Gidiş yönündeki eğim: yatayda bir metre ilerlerken kaç metre yükseliyoruz.
        // Yüzey normalinden kapalı biçimde çıkıyor, ikinci bir ışın gerekmiyor.
        var forward = new Vector2(transform.forward.x, transform.forward.z);
        if (forward.sqrMagnitude < 1e-6f || Mathf.Abs(groundNormal.y) < 1e-3f)
        {
            Grade = 0f;
            return;
        }

        forward.Normalize();
        Grade = -(groundNormal.x * forward.x + groundNormal.z * forward.y) / groundNormal.y;
    }

    // -------------------------------------------------------------- direksiyon

    /// Dönüş YATMADAN geliyor, gidon açısından değil. Bisiklet virajı yatarak alır ve
    /// yarıçapı hız belirler: r = v² / (g·tan(yatma)). Yani hızlıyken geniş, yavaşken
    /// dar dönüyor — sabit bir "dönüş hızı" sayısı bunu asla veremezdi.
    ///
    /// Duran bisiklette formül sonsuza gidiyor; `maxYawRate` gidonun fiziksel sınırı
    /// olarak devreye giriyor.
    void Steer(float dt)
    {
        float targetLean = input.steer * settings.maxLean;

        // Yatma anında değil, yumuşayarak: gerçek bir sürücü gövdesini kaydırıyor.
        lean = Mathf.Lerp(lean, targetLean,
            1f - Mathf.Exp(-dt / Mathf.Max(0.01f, settings.leanSmoothing)));

        if (Speed < 0.2f) return;

        float radians = lean * Mathf.Deg2Rad;
        float yawRate = Mathf.Abs(settings.gravity) * Mathf.Tan(radians) / Speed;

        yawRate = Mathf.Clamp(yawRate * Mathf.Rad2Deg,
            -settings.maxYawRate, settings.maxYawRate);

        transform.Rotate(Vector3.up, yawRate * dt, Space.World);
    }

    // ------------------------------------------------------------------- itme

    void Accelerate(float dt)
    {
        float mass = Mathf.Max(1f, settings.mass);
        float crr = RollingResistance >= 0f ? RollingResistance : settings.rollingResistance;

        // İTME. Güç bölü hız, ama düşük hızda bu sonsuza gidiyor — duruştan kalkarken
        // 230 W bölü sıfır. Tekerleğin zemine aktarabildiği en büyük kuvvet sınır.
        float power = input.sprint ? settings.sprintPower : settings.steadyPower;
        power *= input.throttle;

        float drive = Speed > 0.3f
            ? Mathf.Min(power / Speed, settings.maxDriveForce)
            : settings.maxDriveForce * input.throttle;

        // DİRENÇLER. Yuvarlanma hızdan bağımsız, sürükleme hızın karesiyle, yerçekimi
        // eğimden. Üçü de bisikleti yavaşlatan gerçek kuvvetler; hiçbiri "oynanış için"
        // eklenmiş bir katsayı değil.
        float rolling = crr * mass * Mathf.Abs(settings.gravity);
        float drag = 0.5f * settings.airDensity * settings.dragArea * Speed * Speed;

        // Eğim kuvveti: yokuş yukarı fren, aşağı itme. sin(atan(eğim)) kapalı biçimde.
        float slope = mass * settings.gravity * Grade / Mathf.Sqrt(1f + Grade * Grade);

        // Havadayken pedal işe yaramıyor, yuvarlanma direnci de yok.
        if (!Grounded) { drive = 0f; rolling = 0f; }

        float force = drive + slope - rolling - drag;
        Speed += force / mass * dt;

        // FREN AYRI: kuvvet değil doğrudan yavaşlama. Fren gücü tekerleğin kilitlenme
        // sınırına dayanıyor ve kütleyle ölçeklenmiyor — bisiklette ön takla sınırı
        // kütleden değil geometriden geliyor.
        if (Grounded && input.brake > 0f)
            Speed -= settings.brakeDeceleration * input.brake * dt;

        // Geri gitmek yok. Tavan inişte fren refleksinin yerine geçiyor.
        Speed = Mathf.Clamp(Speed, 0f, settings.comfortMaxSpeed);
    }

    // ---------------------------------------------------------------- hareket

    void Move(float dt)
    {
        // Yön zemine yatırılıyor: yokuşta ileri gitmek yamaç boyunca gitmek demek,
        // yatay ileri değil. Yoksa dik yamaçta bisiklet zemine gömülüyor.
        Vector3 along = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
        if (along.sqrMagnitude < 1e-6f) along = transform.forward;

        if (Grounded && verticalSpeed < 0f) verticalSpeed = -2f;
        else verticalSpeed += settings.gravity * dt;

        controller.Move((along * Speed + Vector3.up * verticalSpeed) * dt);
    }
}
