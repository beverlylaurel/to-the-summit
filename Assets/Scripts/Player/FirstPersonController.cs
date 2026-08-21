using UnityEngine;
using UnityEngine.InputSystem;

/// Yürüme, koşma, zıplama ve yerçekimi. Bakış MouseLook'un işi.
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Hareket")]
    public float walkSpeed = 2.2f;
    public float sprintSpeed = 4f;
    [Tooltip("Havadayken yön değiştirme etkisi (0 = yok, 1 = tam kontrol).")]
    [Range(0f, 1f)] public float airControl = 0.4f;

    [Header("Fizik")]
    [Tooltip("Dünya yerçekimi. Abartılı bir değer (-20) zıplamayı çevik gösteriyordu " +
             "ama düşüşü de iki kat hızlandırıyor: ileride düşme hasarı bu ivmeden " +
             "türeyecek ve yanlış ivme yanlış hasar verir.")]
    public float gravity = -9.81f;
    [Tooltip("Dikey sıçrama yüksekliği (metre). Ayakta duran bir insan yaklaşık 0.4 m " +
             "çıkar; ağır bot, seferi kıyafet ve sırt yüküyle daha az. 1.1 m " +
             "insanüstüydü — oyuncu kendi boyunun üstüne kalkıyordu.")]
    public float jumpHeight = 0.4f;
    [Tooltip("Bu açının üstündeki yamaçlara yürünemez. Tırmanma sistemi buradan devreye girecek.")]
    [Range(20f, 80f)] public float slopeLimit = 45f;
    public float stepOffset = 0.4f;

    [Header("Kar")]
    [Tooltip("Karın çarpışma yüzeyi. Kar sistemi olmayan sahnelerde (test zemini) boş " +
             "bırakılır — orada kar yoktur, yokluğu bir hata değil.")]
    [SerializeField] SnowSurface snow;

    CharacterController controller;
    Vector3 velocity;

    /// Kontrolcü karın üstünde asılı duruyor: kapsül çıplak araziye değiyor olmadığı
    /// için `isGrounded` yanlış döner. Yürüme ve zıplama bu bayrağı da saymak zorunda.
    bool onSnow;

    /// Kendi kapsülünü ayıklamak için: zemin ışını oyuncunun içinden başlıyor ve
    /// filtrelenmezse ilk çarptığı şey kendi çarpışma hacmi oluyor.
    readonly RaycastHit[] groundHits = new RaycastHit[4];

    /// ZEMİNE BASIYOR MU. Kar deformasyonu bunu okuyor: havadayken iz bırakılmamalı.
    ///
    /// İki koşulun BİRLEŞİMİ, tek başına `isGrounded` değil — kapsül karın üstünde
    /// asılıyken `isGrounded` yanlış dönüyor ve zıplamadan yürürken iz kesilirdi.
    public bool OnGround => controller != null && (controller.isGrounded || onSnow);

    /// Test amaçlı hız çarpanı. Normal oyunda 1.
    public float SpeedMultiplier { get; set; } = 1f;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        controller.slopeLimit = slopeLimit;
        controller.stepOffset = stepOffset;
    }

    void Update()
    {
        // Serbest uçuş açıkken kontrolcü kapalı: ikisi AYNI CharacterController'ı
        // kullanıyor ve uçuş onu devre dışı bırakıyor. Kapalı kontrolcüye Move
        // çağırmak her karede hata basıyordu — yürüyüş o sırada zaten susmalı.
        if (!controller.enabled) return;

        // İmleç serbestken girdi oyuna değil arayüze ait
        if (Cursor.lockState == CursorLockMode.Locked) Move();

        ApplyGravity();
    }

    void Move()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        Vector2 input = Vector2.zero;
        if (kb.wKey.isPressed) input.y += 1f;
        if (kb.sKey.isPressed) input.y -= 1f;
        if (kb.dKey.isPressed) input.x += 1f;
        if (kb.aKey.isPressed) input.x -= 1f;
        input = Vector2.ClampMagnitude(input, 1f);

        float speed = (kb.leftShiftKey.isPressed ? sprintSpeed : walkSpeed) * SpeedMultiplier;
        Vector3 wish = (transform.right * input.x + transform.forward * input.y) * speed;

        if (controller.isGrounded || onSnow)
        {
            velocity.x = wish.x;
            velocity.z = wish.z;

            if (kb.spaceKey.wasPressedThisFrame)
                velocity.y = Mathf.Sqrt(-2f * gravity * jumpHeight);
        }
        else
        {
            velocity.x = Mathf.Lerp(velocity.x, wish.x, airControl * Time.deltaTime * 10f);
            velocity.z = Mathf.Lerp(velocity.z, wish.z, airControl * Time.deltaTime * 10f);
        }
    }

    void ApplyGravity()
    {
        if ((controller.isGrounded || onSnow) && velocity.y < 0f)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        if (snow != null) SettleOnSnow();
    }

    /// KAR İKİNCİ ZEMİN. Arazi çarpışması çıplak kayanın yüzeyi; kar onun üstünde
    /// geometrik olarak yükseliyor ama TerrainCollider bunu bilmiyor. Ayak karın
    /// üstünün altına düştüğü karede yukarı çekiliyor.
    ///
    /// Arazi yüksekliği ışınla ölçülüyor, `SampleHeight` ile değil: çarpışma yüzeyi
    /// yükseklik haritasının üçgenlenmiş hâli ve ikisi köşegende birkaç santim
    /// ayrışıyor. Kar eşiği 18 cm — o fark doğrudan gömülmeye dönüşürdü.
    void SettleOnSnow()
    {
        onSnow = false;

        // Yükselirken karışılmıyor: zıplayan oyuncu kendi karının içine geri çekilmemeli.
        if (velocity.y > 0f) return;

        var origin = transform.position + Vector3.up * 2f;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 8f,
            ~0, QueryTriggerInteraction.Ignore);

        float ground = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            if (groundHits[i].collider == controller) continue;
            ground = Mathf.Max(ground, groundHits[i].point.y);
        }

        if (float.IsNegativeInfinity(ground)) return;

        // Derinlik ZEMİN kotundan soruluyor, ayak kotundan değil: kot bandı okuması
        // shader tarafında da yüzey noktasından yapılıyor, ayak karın üstünde duruyor.
        float depth = snow.DepthAt(new Vector3(transform.position.x, ground,
                                               transform.position.z));
        if (depth < 0.001f) return;

        // Kar yüzeyine İKİ YÖNDE de yapışılıyor. Yalnız yukarı çekmek denendi ve
        // yamaçtan inerken zıplattı: kar yüzeyi eğim boyunca düşerken oyuncu ancak
        // -2 m/s ile iniyor, aradaki farkta havada kalıp serbest düşüyor, sonra bir
        // sonraki karede geri kaldırılıyordu. Zemin yüzeyi bir tarafa iterse zemin
        // değil tramplen olur.
        //
        // Aşağı yapışma MESAFEYLE sınırlı: uçurumdan atlayan oyuncu karın yüzeyine
        // geri çekilmemeli. Sınır basamak payı kadar — kontrolcünün zaten tırmandığı
        // yükseklik.
        float top = ground + depth;
        float delta = top - transform.position.y;
        if (delta < -stepOffset) return;

        controller.Move(Vector3.up * delta);
        velocity.y = -2f;
        onSnow = true;
    }
}
