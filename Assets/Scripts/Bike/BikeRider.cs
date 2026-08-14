using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// OYUNCUYU BİSİKLETE BİNDİRİR VE İNDİRİR. Yürüyüş ile sürüş iki ayrı kontrolcü; bu
/// bileşen aralarındaki geçişi yapıyor, ikisinin de içine karışmıyor. Yürüyüş kontrolcüsü
/// bisikleti bilmiyor, bisiklet oyuncuyu bilmiyor.
///
/// SÜRERKEN KAFA BAĞIMSIZ. Gerçekte sürücü gövdesini bisikletle birlikte çevirir ama
/// başını yola bakmadan yana çevirebilir — omuz üstünden bakmak gibi. Fare yalnız kafayı
/// çeviriyor, bisikletin yönünü A ve D belirliyor. Sınırsız bırakılsaydı sürücü baykuş
/// gibi arkasına bakardı.
///
/// KAMERA OYUNCUNUN ÜSTÜNDE KALIYOR. Binerken oyuncu bisikletin çocuğu oluyor, kamera da
/// onunla geliyor: ayrı bir sürüş kamerası kurulsaydı iki kameranın ayarını (görüş açısı,
/// sallanma, efektler) ayrı ayrı tutmak gerekirdi.
public class BikeRider : MonoBehaviour
{
    [Header("Oyuncu")]
    [SerializeField] FirstPersonController walker;
    [SerializeField] MouseLook look;
    [SerializeField] CharacterController body;
    [SerializeField] Transform cameraPivot;

    [Header("Bisiklet")]
    [SerializeField] BikeController bike;
    [SerializeField] BikePlayerInput bikeInput;

    [Tooltip("Selenin bisiklet uzayındaki yeri. Kurulum betiği sele parçasından ölçüp " +
             "yazıyor; elle girilse model değişince yalan olurdu.")]
    [SerializeField] Vector3 seat = new Vector3(0f, 0.9f, -0.2f);

    [Header("Binme")]
    [Tooltip("Bisiklete bu mesafeden binilebiliyor (metre). Kısa tutuluyor: uzaktan " +
             "binmek oyuncuyu ışınlıyor gibi duruyor.")]
    [Range(0.5f, 4f)] [SerializeField] float reach = 2.2f;

    [Tooltip("Oturan sürücünün gözünün seleden yüksekliği (metre). Sele 0.91 m'de; 0.75 " +
             "verilince göz 1.66 m'ye çıkıyor ve sürücü ayakta duruyormuş gibi " +
             "hissediliyor. Oturmuş bir yetişkinde göz yerden 1.45-1.55 m.")]
    [Range(0.3f, 1.1f)] [SerializeField] float eyeAboveSeat = 0.58f;

    [Tooltip("Gözün seleden ne kadar önde olduğu (metre). Sürücü gövdesini gidona doğru " +
             "eğiyor; tam selenin üstünde otursaydı gidon kadrajın dışında kalır ve " +
             "bisiklete bindiği hissi kaybolurdu.")]
    [Range(0f, 0.5f)] [SerializeField] float eyeAhead = 0.12f;

    [Tooltip("İnerken oyuncunun bırakıldığı yan mesafe (metre). Bisikletin içine " +
             "bırakılırsa çarpışma onu bir yana fırlatıyor.")]
    [Range(0.4f, 2f)] [SerializeField] float dismountSide = 0.9f;

    [Header("Kafa")]
    [Tooltip("Kafanın gövdeden bağımsız çevrilebildiği açı (derece). Omuz üstünden " +
             "bakmak seksen dereceye kadar; fazlası boyun değil kuş dönüşü olur.")]
    [Range(30f, 110f)] [SerializeField] float headYawLimit = 80f;

    [Tooltip("Sürerken yukarı-aşağı bakış sınırı (derece). Yürürkenkinden dar: eyerde " +
             "gövde öne eğik, tepeye bakmak için boyun yetmiyor.")]
    [Range(30f, 85f)] [SerializeField] float headPitchLimit = 65f;

    [Tooltip("Fare duyarlılığı. Yürüyüşteki bakışla aynı tutuluyor, yoksa bisiklete " +
             "binince el alışkanlığı bozuluyor.")]
    [Range(0.02f, 0.5f)] [SerializeField] float sensitivity = 0.12f;

    float headYaw;
    float headPitch;

    public bool Riding { get; private set; }

    public void Bind(FirstPersonController walkerRef, MouseLook lookRef,
        CharacterController bodyRef, Transform pivot,
        BikeController bikeRef, BikePlayerInput inputRef, Vector3 seatLocal)
    {
        walker = walkerRef;
        look = lookRef;
        body = bodyRef;
        cameraPivot = pivot;
        bike = bikeRef;
        bikeInput = inputRef;
        seat = seatLocal;
    }

    void OnEnable()
    {
        if (walker == null || look == null || body == null || cameraPivot == null)
            throw new InvalidOperationException($"{nameof(BikeRider)}: oyuncu bağımlılıkları atanmadı.");

        if (bike == null || bikeInput == null)
            throw new InvalidOperationException($"{nameof(BikeRider)}: bisiklet atanmadı.");

        // Bisiklet oyuncusuz sürülmüyor: girdi bileşeni binene kadar kapalı, yoksa
        // oyuncu yürürken W'ye basınca bisiklet kendi kendine gidiyor.
        bikeInput.enabled = false;
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (Cursor.lockState != CursorLockMode.Locked) return;

        if (keyboard.eKey.wasPressedThisFrame)
        {
            if (Riding) Dismount();
            else if (Near()) Mount();
        }

        if (Riding) Look();
    }

    bool Near() =>
        Vector3.Distance(transform.position, bike.transform.position) <= reach;

    // ------------------------------------------------------------------ binme

    void Mount()
    {
        Riding = true;

        walker.enabled = false;
        look.enabled = false;

        // Çarpışma kapatılıyor: açık kalsaydı oyuncunun kapsülü bisikletin kendi
        // çarpışmasıyla itişir ve ikisi birbirini fırlatırdı.
        body.enabled = false;

        // GÖZ YÜKSEKLİĞİ ZİNCİRİN TOPLAMINDAN ölçülüyor, pivotun kendi yerel
        // konumundan değil: pivot bir ara nesnenin çocuğu ve yerel yüksekliği gerçek
        // göz yüksekliğini vermiyordu. Sonuç, sürücünün seleden iki metre yukarıda
        // durmasıydı — bisikletin üstünde ayakta gibi.
        float eyeOffset = cameraPivot.position.y - transform.position.y;

        transform.SetParent(bike.transform, false);
        transform.localRotation = Quaternion.identity;
        transform.localPosition = seat
            + Vector3.up * (eyeAboveSeat - eyeOffset)
            + Vector3.forward * eyeAhead;

        headYaw = 0f;
        headPitch = cameraPivot.localEulerAngles.x;
        if (headPitch > 180f) headPitch -= 360f;

        bikeInput.enabled = true;
    }

    void Dismount()
    {
        Riding = false;
        bikeInput.enabled = false;

        transform.SetParent(null, true);

        // Sol yana bırakılıyor: bisiklet sağa yatık park ediliyor ve gerçekte de o
        // taraftan inilmiyor.
        Vector3 target = bike.transform.position - bike.transform.right * dismountSide;
        transform.position = Ground(target);

        // Bakış yönü korunuyor: kafanın baktığı yön gövdeye geçiyor, yoksa yere basar
        // basmaz görüntü yana sıçrıyor.
        transform.rotation = Quaternion.Euler(0f, bike.transform.eulerAngles.y + headYaw, 0f);
        cameraPivot.localRotation = Quaternion.Euler(headPitch, 0f, 0f);

        body.enabled = true;
        walker.enabled = true;
        look.enabled = true;
    }

    /// İnilen noktanın zemini. Bisiklet yamaçta duruyorsa oyuncu havada ya da yerin
    /// içinde bırakılmasın diye ışın atılıyor.
    Vector3 Ground(Vector3 point)
    {
        var ray = new Ray(point + Vector3.up * 3f, Vector3.down);

        return Physics.Raycast(ray, out RaycastHit hit, 8f, ~0, QueryTriggerInteraction.Ignore)
            ? hit.point + Vector3.up * (body.height * 0.5f + body.skinWidth)
            : point;
    }

    // ------------------------------------------------------------------- kafa

    /// Kafa bisikletten bağımsız dönüyor ama gövdeye bağlı: yaw oyuncunun kendi
    /// dönüşüne, pitch kamera pivotuna yazılıyor. Oyuncu bisikletin çocuğu olduğu için
    /// yerel yaw doğrudan "bisiklete göre kafa açısı" oluyor.
    void Look()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 delta = mouse.delta.ReadValue() * sensitivity;

        headYaw = Mathf.Clamp(headYaw + delta.x, -headYawLimit, headYawLimit);
        headPitch = Mathf.Clamp(headPitch - delta.y, -headPitchLimit, headPitchLimit);

        transform.localRotation = Quaternion.Euler(0f, headYaw, 0f);
        cameraPivot.localRotation = Quaternion.Euler(headPitch, 0f, 0f);
    }
}
