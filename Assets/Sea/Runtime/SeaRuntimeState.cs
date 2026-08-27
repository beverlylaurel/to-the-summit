// ROL: denizin YAYINLADIGI durum. Baska sistemler okuyabilir; deniz
// hicbirini uygulamiyor.
// Cagiran: SeaManager (yazar), HUD ve ses sistemleri (okur).

/// DENİZ YAYINLAR, UYGULAMAZ.
///
/// Spec §3.3. Buradaki değerleri kimse okumak zorunda değil; deniz onları
/// yazıp geçiyor. `RenderSettings`, `VolumeProfile` veya `Light.intensity`'ye
/// dokunmuyor — Faz 1 kabul kriteri bunu kod aramasıyla doğruluyor.
public static class SeaRuntimeState
{
    /// Belirgin dalga yüksekliği Hs (m). En yüksek üçte birlik dalgaların
    /// ortalaması; oşinografide denizin "kaç metre" olduğunu söyleyen sayı.
    public static float SignificantWaveHeight { get; internal set; }

    /// Tepe periyodu Tp (s). Spektrumun en çok enerji taşıdığı periyot.
    public static float PeakPeriod { get; internal set; }

    /// Açık denizde tepe köpüğünün kapladığı alan oranı.
    public static float WhitecapCoverage01 { get; internal set; }

    /// Kıyı köpüğünün o andaki şiddeti. Kabarma (run-up) fazından türüyor.
    public static float ShoreFoamIntensity01 { get; internal set; }

    /// Deniz sistemi çalışıyor mu. `SeaManager` bir `ISeaEnvironmentSource`
    /// bulamazsa false kalıyor.
    public static bool Active { get; internal set; }
}
