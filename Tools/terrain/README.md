# Arazi üretimi — L0 / L1

Dağ **pişmiş içerik**: burada bir kez üretilir, çıktı repoya yazılır, Unity yalnız okur.
Çalışma zamanında Python yok. Kararlar `DECISIONS.md` → "Arazi mimarisi" ve "Arazi ölçeği".

## Neden Python

`divtree_synthesis.py` scipy + scikit-image + **optimal transport** kullanıyor. C#'a portu
haftalar sürer ve OT'nin yanlış portu sessizce yanlış dağılım verir. Dağ bir kez
üretildiği için çalışma zamanında hiç gerekmiyor. Gerekçe `DECISIONS.md`'de.

## Kurulum

```
python -m pip install -r Tools/terrain/requirements.txt
```

## Dış bağımlılıklar (repoda DEĞİL)

| ne | yol |
|---|---|
| Referans implementasyon | `C:\Users\musta\Desktop\tts\specs\terrain\orometry-terrains-master` |
| Kirmse zirve veritabanı | `C:\Users\musta\Desktop\tts\specs\terrain\{prominence-p100,alliso-sorted}.txt` |

Veritabanı 1.4 GB, repoya girmiyor. Bölge CSV'si ondan bir kez kesilir.

## Dosyalar

| dosya | ne |
|---|---|
| `region_profile.py` | 540 km'lik bölgenin karakter maskesi — `probMap`, `elevMap` |
| `synth_l0.py` | L0: Divide Tree sentezi (zirve/boyun/sırt grafiği) |
| `l1_region.py` | L1 doğrulama: bölge ölçeğinde DEM ve hillshade |
| `noise.py` | `noise` paketinin yedeği (Windows'ta derlenmiyor) |

## Koşu

```
python Tools/terrain/synth_l0.py --seed 36044
python Tools/terrain/l1_region.py
```

Aynı tohum → aynı çıktı. Co-op şartı bu; dağ herkeste aynı.
