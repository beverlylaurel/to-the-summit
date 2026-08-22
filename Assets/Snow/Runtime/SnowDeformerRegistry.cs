// ROL: sahnedeki etkin SnowDeformer'ların listesi. Yakalama pass'i bu listeyi
// gezer; arama yapmaz.
// Çağıran: SnowDeformer (kayıt), SnowCaptureCamera (okuma).

using System.Collections.Generic;

/// ARAMA YOK, KAYIT VAR. `FindObjectsByType` her karede sahneyi tarar ve
/// allocation yapar (spec §0.8). Bileşen kendini kaydeder; liste her zaman
/// güncel ve gezinme bedava.
///
/// Spec §18.2'deki `SnowHeatRegistry` ile aynı desen — orada da statik kayıt
/// isteniyor, burada da.
public static class SnowDeformerRegistry
{
    /// 64: bir sahnede aynı anda karda iz bırakan nesne sayısı için fazlasıyla
    /// yeterli. Aşılırsa liste büyür, hata olmaz — sadece bir kerelik allocation.
    const int InitialCapacity = 64;

    static readonly List<SnowDeformer> Active = new(InitialCapacity);

    public static int Count => Active.Count;

    public static SnowDeformer Get(int index) => Active[index];

    public static void Register(SnowDeformer deformer)
    {
        if (deformer == null || Active.Contains(deformer)) return;
        Active.Add(deformer);
    }

    public static void Unregister(SnowDeformer deformer) => Active.Remove(deformer);

    /// Editör alanı yeniden yüklenmediğinde (Play mode domain reload kapalı)
    /// statik liste eski oturumdan kalır ve ölü referans taşır.
    [UnityEngine.RuntimeInitializeOnLoadMethod(
        UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnLoad() => Active.Clear();
}
