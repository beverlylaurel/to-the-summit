using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

/// TIRTIK İŞARETLEYİCİ. Kullanıcı ekranın ortasını tırtıklı yere doğrultup M'ye
/// basıyor; ışın araziye çarptığı dünya koordinatı dosyaya yazılıyor.
///
/// NEDEN VAR: testere belirtisi beş turda çözülemedi ve her turda yanlış KATMAN
/// tahmin edildi — yükseklik haritası, kar geometrisi, normal dokusu, LOD, ufuk
/// haritası. Eksik olan tek şey YER'di: hangi noktada olduğunu bilmeden hangi
/// katmanın taşıdığı ölçülemiyor.
///
/// Koordinat elde olunca ölçüm tahminden çıkıyor: o noktada yükseklik alanı,
/// yüzey haritaları ve kabartı genliği tek tek okunabiliyor.
///
/// Belirti çözülünce bu bileşen ve `Logs/notches.log` birlikte silinir.
public class NotchMarker : MonoBehaviour
{
    [Tooltip("Işının çıktığı kamera. Ekran ortası kullanılıyor.")]
    [SerializeField] Camera view;

    const string LogPath = "Logs/notches.log";
    const float MaxDistance = 20000f;

    int count;

    /// Kurulum bağlıyor; `FindObjectOfType` yok.
    public void Bind(Camera viewRef) => view = viewRef;

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.mKey.wasPressedThisFrame) return;

        if (view == null)
            throw new System.InvalidOperationException($"{nameof(NotchMarker)}: kamera atanmadı.");

        Ray ray = view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        // Yalnız arazi: tırtık aranan yer o. Başka çarpan varsa ölçüm kirlenir.
        if (!Physics.Raycast(ray, out RaycastHit hit, MaxDistance))
        {
            Debug.LogWarning("İşaret konmadı: ışın hiçbir şeye çarpmadı.");
            return;
        }

        count++;
        Vector3 p = hit.point;

        Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
        File.AppendAllText(LogPath, string.Format(CultureInfo.InvariantCulture,
            "{0}\t{1:F2}\t{2:F2}\t{3:F2}\tmesafe {4:F0} m\n",
            count, p.x, p.y, p.z, hit.distance));

        Debug.Log($"İşaret {count}: ({p.x:F0}, {p.y:F0}, {p.z:F0}), mesafe {hit.distance:F0} m");
    }
}
