// ROL: cevresine sicaklik alani yayar (spec 18.2, Grosbellet theta alani).
// Caginan: SnowHeatRegistry (kayit).

using UnityEngine;

/// UPDATE'TE HICBIR SEY YAPMIYOR. Bilesen yalniz kendini kaydediyor;
/// konumu ve alanlari `SnowHeatRegistry` okuyor. Ates/mesale prefab'larina
/// EKLENMEDI - o bir prefab degisikligidir ve kullanicinin karari (spec 1.4).
[DisallowMultipleComponent]
public class SnowHeatSource : MonoBehaviour
{
    [Tooltip("Etki yarıçapı (m) — bu mesafede etki TAM OLARAK sıfır.")]
    public float radius = 2.5f;

    [Tooltip("Merkezde çıkarılan kar yüksekliği (m). Grosbellet'nin theta alanı.")]
    public float strength = 0.45f;

    void OnEnable() => SnowHeatRegistry.Register(this);
    void OnDisable() => SnowHeatRegistry.Unregister(this);
}
