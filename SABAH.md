# Sabah listesi — 2026-08-17 gecesi

**GEÇİCİ DOSYA.** Okunup iş bitince silinir.

---

## Önce bu: bir karar bekliyor, gerisi ona bağlı

**Ovanın kotu 186 m mi kalacak, 2400 m'ye mi çıkacak?**

Ölçüm: yeni arazi oyun alanında ova üretmiyor. Ortanca eğim 38°, yürünür alan %6.7,
en alçak nokta 517 m.

Sebep: 5709 m'lik zirveden 9 km'de 186 m'ye inmek gerçek dünyada yok, dolayısıyla
gerçek dağ istatistiklerinden de çıkmıyor.

Ama senin tarifin kot söylemiyordu — 186 m mevcut arazinin sayısı, tasarım kararı değil.
İki seçenek ve gerekçeleri `DECISIONS.md` → "Ovanın kotu". **Önerim: 2400 m.**

Bu cevaplanmadan L1 uygulanmaz; ova kotu maskeyi, maske sentezi, sentez de yükseklik
haritasını belirliyor.

---

## Sonra: Unity derleme kontrolü

Doğrulanmamış kod `feat/terrain-l0` dalında. `main`'e almadan önce derlenmeli.

```bash
cd /d "D:\ME\game\to the summit" && git checkout feat/terrain-l0
```

Unity'ye geç, derlemeyi bekle, konsola bak.

**Beklenen:** derleme temiz. Üç yeni dosya var:
`DivideTree.cs`, `DivideTreeImporter.cs`, `DivideTreeWindow.cs`.

Sonra menüden:

1. **To The Summit → Arazi → Divide Tree'yi İçe Aktar**
   Beklenen: `Logs/tools.log` içinde "7268 zirve, 7267 boyun, tohum 36044",
   uyarı yok. `Assets/Terrain/DivideTree.asset` oluşur.

2. **To The Summit → Arazi → Divide Tree Penceresi**
   Beklenen: üç ölçek düğmesi (Bölge / Çevre / Oyun alanı).
   - **Bölge 540 km**: doğu-batı uzanan bir dağ kuşağı, güneyde seyrelen tepecikler,
     kuzeyde daha az yoğun bölge. Kırmızı kare merkezde.
   - **Oyun alanı 24 km**: kareden geçen sırt hatları görünür.
   - Alt satır: "en yüksek #0: 5709 m, merkezden 0 m".

Uyarı çıkarsa metni bana ver — üç denetim var: ağaç mı, zirve merkezde mi, bölge dışında
zirve var mı.

---

## Gece ne yapıldı

`main`'e giren ve pushlanan:

| ne | nerede |
|---|---|
| Python araç zinciri | `Tools/terrain/` |
| Divide Tree verisi (7268 zirve, 579 KB) | `Assets/Terrain/DivideTree.txt` |
| Ölçülmüş kararlar ve sınırlar | `DECISIONS.md` |
| L0 ölçek sayıları | `SCALE.md` |
| İskelet bağı | `SYSTEMS.md` |
| Uygulama raporu | `.claude/PRPs/reports/` |

`feat/terrain-l0` dalında bekleyen: üç C# dosyası (doğrulanmamış).

## Görseller

`Tools/terrain/gorsel/` altında:

| dosya | ne |
|---|---|
| `05_karakter_maskesi.png` | Bölgenin karakter dağılımı — silsile / plato / ova / etek |
| `06_l0_sonuc.png` | Divide Tree, üç ölçekte |
| `07_l1_bolge.png` | 540 km'lik arazi, hillshade |
| `09_l1_oyun_alani.png` | Oyun alanı 17.5 km, 4.28 m/örnek |

## Açık kalan iki teknik iş

1. **Gerçek plato çıkmıyor** — yoğunluk düşükse ada ada kopuyor, yüksekse dağlıktan
   ayrılmıyor. Sebep vadi oyma derinliği, L1'in işi.
2. **Yüzey detayı** — yakın planda üçgenler görünüyor. Referansta erozyon kodu yok,
   Galin 2019'dan yazılacak.
