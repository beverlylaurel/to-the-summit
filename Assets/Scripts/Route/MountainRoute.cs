using System;
using System.Collections.Generic;
using UnityEngine;

/// ROTA VERİSİ. Oyuncunun nerede başlayacağı, dağa hangi hatlardan çıkılacağı, yolda
/// nerede kamp kurulacağı — hepsi elle işaretleniyor (`RoutePainter`) ve burada duruyor.
///
/// KONUMLAR NORMALİZE (0-1), dünya koordinatı DEĞİL. Arazi yeniden üretildiğinde ya da
/// dağ ölçeklendiğinde dünya koordinatları anlamını kaybediyor: işaretler ya havada
/// kalır ya zeminin altında. Normalize XZ arazi sınırlarına göre yaşıyor, yükseklik
/// kullanım anında zeminden okunuyor (bkz. `SCALE.md`).
///
/// YÜKSEKLİK SAKLANMIYOR. Saklansaydı arazinin her düzenlenişinde bayatlardı ve
/// bayatlığı fark etmenin yolu yoktu.
[CreateAssetMenu(menuName = "To The Summit/Rota", fileName = "MountainRoute")]
public class MountainRoute : ScriptableObject
{
    /// Rota noktası. Yarıçap fırçanın kalınlığı: koridorun genişliği, kampın
    /// düzleştirilecek alanı buradan geliyor — ayrı bir sayı tutmaya gerek yok.
    /// Rota noktası.
    ///
    /// YARIÇAP = YOLUN KENDİ GENİŞLİĞİNİN YARISI. Omuz payı, geçiş bandı ve
    /// düzleştirmenin yamaca karışma mesafesi BURAYA GİRMEZ — onlar arazi
    /// şekillendirmesinin ayarları ve oradan çarpan olarak uygulanır.
    ///
    /// Tek sayıya "yol + omuz + geçiş" sıkıştırmak denendi ve vazgeçildi: sonradan
    /// omzu genişletmek istendiğinde hangi payın değişeceği okunamaz oluyor ve sayının
    /// fiziksel karşılığı kalmıyor. Burada 3.2 yazıyorsa yol 6.4 metredir.
    [Serializable]
    public struct Mark
    {
        [Tooltip("Arazi üzerinde normalize konum (0-1).")]
        public Vector2 position;
        [Tooltip("Yolun yarı genişliği (metre). Omuz payı buraya dahil değil.")]
        public float radius;
    }

    /// Bir tırmanış hattı. Üç hat bağımsız saklanıyor, ağaç olarak değil: dallanma
    /// çizerken zaten oluşuyor ve veri yapısına ağaç dayatmak, hattı sonradan bölmek
    /// ya da birleştirmek istendiğinde her şeyi yeniden yazdırırdı.
    [Serializable]
    public class Branch
    {
        public string name = "Hat";
        public List<Mark> marks = new();
    }

    [Header("Başlangıç")]
    [Tooltip("Oyuncunun doğduğu yer, normalize (0-1).")]
    public Vector2 spawn = new(0.5f, 0.5f);

    [Tooltip("Doğuşta bakılan yön (derece, +X'ten saat yönünün tersine). Oyun dağı " +
             "görerek başlasın diye işaretleniyor.")]
    public float spawnYaw;

    [Tooltip("Doğuş işaretlendi mi. İşaretlenmemişse kurulum eski davranışa düşüyor: " +
             "dağ eteğinin dışında hesaplanmış bir nokta.")]
    public bool spawnSet;

    [Header("Yol")]
    [Tooltip("Otobüsün geldiği ve aynı izden döndüğü yol. Tek hat: gidiş ve dönüş aynı. "
             + "Yarıçap yolun genişliği — araç geçecekse patikadan geniş olmalı.")]
    public List<Mark> road = new();

    [Header("Hatlar")]
    public List<Branch> branches = new();

    [Header("İşaretler")]
    [Tooltip("Kamp kurulacak yerler. Yarıçap düzleştirilecek alanı veriyor.")]
    public List<Mark> camps = new();

    [Tooltip("Erzak alınan yerler. Tırmanış öncesi son alışveriş noktası ve yolda çıkanlar.")]
    public List<Mark> shops = new();

    /// Normalize konumu dünya XZ'sine çevirir. Yükseklik ÇAĞIRANIN işi: zemin
    /// çarpışmasından mı yoksa yükseklik haritasından mı okunacağı kullanıma göre
    /// değişiyor ve ikisi birkaç santim ayrışıyor.
    public static Vector3 ToWorld(Vector2 normalized, Terrain terrain)
    {
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;

        return new Vector3(origin.x + normalized.x * size.x, origin.y,
                           origin.z + normalized.y * size.z);
    }

    /// Dünya XZ'sini normalize konuma çevirir.
    public static Vector2 ToNormalized(Vector3 world, Terrain terrain)
    {
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;

        return new Vector2((world.x - origin.x) / Mathf.Max(1f, size.x),
                           (world.z - origin.z) / Mathf.Max(1f, size.z));
    }
}
