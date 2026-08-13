using UnityEngine;

/// BİSİKLETE VERİLEN GİRDİ. Kontrolcü klavyeyi, gamepad'i ya da yapay zekâyı BİLMİYOR;
/// yalnız bu yapıyı okuyor.
///
/// Neden ayrı: girdi okuma projeden projeye değişen tek şey. Kontrolcünün içine
/// gömülseydi her yeni projede kontrolcü de değişirdi. Böylece aynı bisiklet oyuncunun
/// elinde de, bir NPC'nin altında da, kayıttan oynatmada da çalışıyor.
public struct BikeInput
{
    /// Pedal çevirme, 0-1. Bir tam güç demek değil: sürücünün ne kadar bastığı.
    public float throttle;

    /// Fren, 0-1.
    public float brake;

    /// Direksiyon, -1 sol, +1 sağ.
    public float steer;

    /// Sprint basılı mı. Güç `steadyPower` yerine `sprintPower` oluyor.
    public bool sprint;

    public static BikeInput Coasting => default;

    /// Ölçüleri güvenli aralığa çekiyor: dışarıdan gelen değere güvenilmiyor, çünkü
    /// girdi kaynağı kontrolcünün dışında ve başka bir projede başka bir şey olacak.
    public BikeInput Sanitised() => new()
    {
        throttle = Mathf.Clamp01(throttle),
        brake = Mathf.Clamp01(brake),
        steer = Mathf.Clamp(steer, -1f, 1f),
        sprint = sprint
    };
}
