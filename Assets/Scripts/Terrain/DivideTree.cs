using System;
using UnityEngine;

/// Arazinin İSKELETİ (L0): zirve/boyun/sırt grafiği.
///
/// Bu asset arazi DEĞİL, arazinin YAPISI. Yükseklik haritası (L1) bundan üretilir ve
/// içerik — kamp, konak, mağara, anıt, zirve modülü — bu grafiğin düğümlerine
/// çapalanır. Kararlar `DECISIONS.md` → "Arazi mimarisi: dört katman, sembolik çapa".
///
/// DAĞ PİŞMİŞ İÇERİK. `Tools/terrain/synth_l0.py` editör dışında bir kez çalışır,
/// çıktısı `Assets/Terrain/DivideTree.txt` olarak repoya yazılır, `DivideTreeImporter`
/// onu bu asset'e çevirir. Çalışma zamanında üretim yok; herkeste aynı dağ, senkronlanacak
/// bir durum da yok.
///
/// KİMLİK KARARLILIĞI TÜM ÇAPA MİMARİSİNİN DAYANDIĞI NOKTA. Düğümün kimliği dizideki
/// SIRASIDIR. Aynı tohum aynı sırayı verir; erozyon veya yükseklik haritası yeniden
/// üretilse bile "47 numaralı boyun" aynı boyun kalır. Referansın kendi okuyucusu
/// (`utils/divtree_reader.readDivideTree`) kırpma sınırında düğümleri yeniden sıralıyor
/// (`peakReorder`, `saddleReorder`); bu yüzden o okuyucu KULLANILMIYOR, diziler
/// doğrudan yazılıyor.
[CreateAssetMenu(fileName = "DivideTree", menuName = "To The Summit/Divide Tree")]
public class DivideTree : ScriptableObject
{
    /// Bir zirve. Konum METRE ve bölge MERKEZİNE göre — merkez zirvenin kendisi ve
    /// Unity arazisinin origin'i (`MountainGenerator` araziyi `-terrainSize/2`'ye
    /// koyuyor, `SCALE.md`).
    [Serializable]
    public struct Peak
    {
        [Tooltip("Doğu (+) yönünde metre, bölge merkezine göre.")]
        public float east;
        [Tooltip("Kuzey (+) yönünde metre, bölge merkezine göre.")]
        public float north;
        [Tooltip("Deniz seviyesinden metre.")]
        public float elevation;
        [Tooltip("Prominence — zirvenin bağımsızlık ölçüsü, metre.")]
        public float prominence;
    }

    /// Bir boyun (saddle) ve bağladığı iki zirve. Boyun, iki zirve arasındaki en alçak
    /// geçiş noktası; gerçek rotalar ve kamp yerleri buralardan geçer.
    [Serializable]
    public struct Saddle
    {
        public float east;
        public float north;
        public float elevation;
        [Tooltip("Bağladığı iki zirvenin kimliği (dizi indeksi).")]
        public int peakA;
        public int peakB;
    }

    [Header("Üretim")]
    [Tooltip("Üretimde kullanılan tohum. Aynı tohum aynı dağı ve aynı kimlikleri verir.")]
    public int seed;

    [Tooltip("Üretilen bölgenin bir kenarı, metre. Oyun alanı bunun merkezindedir.")]
    public float regionSize;

    [Tooltip("Oyun alanının bir kenarı, metre. Unity arazisi bu kadardır.")]
    public float playSize;

    [Tooltip("Zirvenin kotu, metre. Bölgedeki en yüksek nokta bu olmalıdır.")]
    public float summitElevation;

    [Tooltip("Bu prominence'ın altındaki zirveler hiç üretilmedi, metre. " +
             "Gerekçe fizik: 100 km'de bir ekran pikseli ~47 m, daha alçak tümsek " +
             "zaten çözülmüyor. Ayrıntı DECISIONS.md → 'L0 girdisi'.")]
    public float prominenceFloor;

    [Tooltip("Gerçek Everest bölgesi istatistiklerine uygulanan kot ölçeği. " +
             "Dominans bir ORAN olduğu için değişmiyor, izolasyon YATAY olduğu için " +
             "ölçeklenmiyor.")]
    public float elevationScale;

    [Header("Grafik")]
    public Peak[] peaks;
    public Saddle[] saddles;

    /// Bölgenin en yüksek zirvesinin kimliği. İçerik çapaları için doğal başlangıç:
    /// zirve modülü, son kamp ve rotanın bitişi buraya bağlanır.
    public int SummitId
    {
        get
        {
            if (peaks == null || peaks.Length == 0)
                throw new InvalidOperationException($"{name}: grafik boş.");

            int best = 0;
            for (int i = 1; i < peaks.Length; i++)
                if (peaks[i].elevation > peaks[best].elevation) best = i;
            return best;
        }
    }

    /// Oyun alanının yarı genişliği, metre. Bir düğümün oynanan arazide olup olmadığı
    /// buradan sorulur.
    public float PlayHalfSize => playSize * 0.5f;

    /// Düğüm oynanan arazinin içinde mi. Dışarıdakiler yalnız manzara — çarpışmasız
    /// uzak bantlarda kalıyorlar (üç bantlı temsil, `DECISIONS.md`).
    public bool InPlayArea(Peak p)
        => Mathf.Abs(p.east) <= PlayHalfSize && Mathf.Abs(p.north) <= PlayHalfSize;
}
