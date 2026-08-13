# Araçlar

Projenin dışında çalışan, Unity'nin yapamadığı işler. Her biri komut satırından.

## `decimate.py` — model seyreltme

Üretilen modeller yüz binlerce üçgenle geliyor; oyun bandı bunun yüzde biri. Blender'ın
seyreltmesi **parça bölünmesini koruyor**, yani tekerlek ve gidon ayrı nesne kalıyor ve
rig bozulmuyor. Meshy'nin kendi remesh'i bunu yapmıyor.

Bütçe parça başına karekök ağırlıkla dağıtılıyor: tek oran verilseydi tekerlek hâlâ ağır
kalırken fren kolu lapa olurdu.

```bash
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background \
  --python Tools/decimate.py -- <girdi.fbx> <çıktı.fbx> <hedef_üçgen>
```

Bisiklet böyle üretildi: 3.095.936 → 199.971 üçgen, 26 parça korunmuş.

**Sonuç ölçülmeden kabul edilmez.** Seyreltmenin kaybettirdiği detay iki yönlü ölçülüyor:
seyreltilmişten orijinale bakmak yüzeyin ne kadar kaydığını söylüyor, orijinalden
seyreltilmişe bakmak SİLİNEN yapıyı gösteriyor — tek yön baksaydık kaybolan bir tel
görünmezdi. Bisiklette ortalama sapma 0.15 mm, en büyük kayıp 2.9 mm çıktı: hiçbir yapı
kaybolmamış.
