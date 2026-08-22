using UnityEngine;

/// BİR YÜZEYİN BÜTÜN HARİTALARI. Kar, kaya, çakıl, toprak — hepsi aynı yapıdan.
///
/// Shader'a "sekiz ayrı doku" geçirmek yerine tek asset geçiliyor. Kar için elle
/// yazılan on iki alan ikinci yüzeyde yirmi dört, üçüncüde otuz altı olurdu; o noktada
/// bir harita eklemek her dosyaya dokunmak demek.
///
/// Haritalar STOKASTİK DÖNÜŞÜMDEN geçmiş hâlleriyle duruyor (Gauss histogramı + ters
/// LUT). Ham doku sahnede kullanılmıyor: stokastik döşeme ancak Gauss uzayında
/// kontrastı koruyor.
[CreateAssetMenu(menuName = "To The Summit/Yüzey Malzemesi", fileName = "Surface")]
public class SurfaceMaterialSet : ScriptableObject
{
    [Tooltip("Kaynak klasör (proje dışı olabilir). `TextureIngest` bu klasörden " +
             "haritaları çıkarıp projeye alıyor; kayıt burada durur ki doku " +
             "yenilendiğinde nereden geldiği aranmasın.")]
    public string sourceFolder;

    [Tooltip("Proje içi dosya ön eki, örn. `RockCliff`. Haritalar bu adın " +
             "sonuna _Normal/_Roughness/_Height eklenerek aranır.")]
    public string assetPrefix;

    [Header("Stokastik dönüşümlü haritalar")]
    public Texture2D normal;
    public Texture2D normalLut;
    public Texture2D roughness;
    public Texture2D roughnessLut;
    public Texture2D height;
    public Texture2D heightLut;

    [Header("Ölçüm")]
    [Tooltip("Işık pişmişliği: renk parlaklığı ile yüzey eğimi arasındaki korelasyon. " +
             "0.3'ün üstü, dokuya yönlü güneş gömülü demek — albedo olarak " +
             "kullanılamaz. `TextureIngest` ölçüp buraya yazıyor.")]
    public float bakedLightCorrelation;

    [Tooltip("Yönlülük: normalin x ve y saçılım oranı. 1.0 yönsüz (toz kar), " +
             "0.7 altı belirgin yönlü (katmanlı kaya, damarlı yüzey).")]
    public float anisotropy;

    /// Sahnede kullanılabilir mi. Eksik harita varsa shader dalı hiç açılmaz.
    public bool IsComplete =>
        normal != null && normalLut != null &&
        roughness != null && roughnessLut != null &&
        height != null && heightLut != null;
}
