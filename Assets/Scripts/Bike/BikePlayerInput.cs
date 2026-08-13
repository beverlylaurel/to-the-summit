using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// KLAVYE GİRDİSİNİ BİSİKLETE AKTARIR. Ayrı bileşen olmasının sebebi: girdi okuma
/// projeden projeye değişen tek parça. Kontrolcünün içine gömülseydi her yeni projede
/// kontrolcü de değişirdi.
///
/// Aynı bisikleti bir yapay zekâ sürecekse bu bileşen kapatılır, yerine kendi
/// `SetInput` çağrısını yapan bir başkası konur. Kontrolcüye dokunulmaz.
[RequireComponent(typeof(BikeController))]
public class BikePlayerInput : MonoBehaviour
{
    [SerializeField] BikeController bike;

    [Tooltip("Pedal otomatik mi çevrilsin. Açıkken oyuncu ileri tuşuna basmadan da " +
             "tempo korunuyor; uzun düzlüklerde tuş basılı tutmak yormasın diye.")]
    [SerializeField] bool autoPedal;

    void Reset() => bike = GetComponent<BikeController>();

    void OnEnable()
    {
        if (bike == null) bike = GetComponent<BikeController>();
        if (bike == null)
            throw new InvalidOperationException($"{nameof(BikePlayerInput)}: bisiklet yok.");
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // İmleç serbestken girdi oyuna değil arayüze ait — projenin yürüyüş
        // kontrolcüsüyle aynı kural.
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            bike.SetInput(BikeInput.Coasting);
            return;
        }

        float steer = 0f;
        if (keyboard.aKey.isPressed) steer -= 1f;
        if (keyboard.dKey.isPressed) steer += 1f;

        float throttle = keyboard.wKey.isPressed || autoPedal ? 1f : 0f;
        float brake = keyboard.sKey.isPressed ? 1f : 0f;

        // Fren basılıyken pedal çevrilmiyor: ikisi aynı anda gerçek değil ve hızı
        // belirsiz bir dengeye sokuyor.
        if (brake > 0f) throttle = 0f;

        bike.SetInput(new BikeInput
        {
            throttle = throttle,
            brake = brake,
            steer = steer,
            sprint = keyboard.leftShiftKey.isPressed
        });
    }
}
