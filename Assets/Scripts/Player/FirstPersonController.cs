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

    CharacterController controller;
    Vector3 velocity;

    /// Kontrolcü karın üstünde asılı duruyor: kapsül çıplak araziye değiyor olmadığı
    /// için `isGrounded` yanlış döner. Yürüme ve zıplama bu bayrağı da saymak zorunda.

    /// Kendi kapsülünü ayıklamak için: zemin ışını oyuncunun içinden başlıyor ve
    /// filtrelenmezse ilk çarptığı şey kendi çarpışma hacmi oluyor.
    readonly RaycastHit[] groundHits = new RaycastHit[4];
    /// ZEMİNE BASIYOR MU.
    public bool OnGround => controller != null && controller.isGrounded;

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

        if (controller.isGrounded)
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
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

    }

}
