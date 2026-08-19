"""Garg-Nayar yagmur izi veritabanini Unity'nin okuyabilecegi bloga paketler.

NEDEN PYTHON: kaynak dosyalar 16-bit tek kanal PNG (I;16). Unity'nin ImageConversion
katmani bunlari 8-bit'e indiriyor ve dosya sayisi 15 000 - projeye kopyalanamaz.
Pismis icerik kurali geregi uretimi Python yapiyor, Unity yalniz yukluyor.

NEDEN FLOAT16: spec 5.4.4 her dokunun kendi max carpaniyla olceklenmesini istiyor
(`radyans = PNG/65535 * max`). Point light carpanlari 0.002 ile 0.65 arasinda, yani
300 kat. Normalize edilmis degeri 16-bit TAM SAYIYA geri yazmak sonuk izleri tamamen
siliyordu. Half-float hem carpani iceri pisiriyor hem araligi koruyisu.

DCAM BASINA AYRI DOSYA: iz yuksekligi kamera acisiyla degisiyor - olculdu, size16'da
525/494/405/272/108. Orani cos(dcam) (1.000/0.940/0.771/0.517/0.206 karsi
1.000/0.940/0.766/0.500/0.174), yani dcam DIKLIKTEN SAPMA acisi ve theta_v = 90 - dcam.
Hepsini tek Texture2DArray'e doldurmak %40 bos piksel demekti; dcam basina ayri dosya
sifir bosluk.

CIKTI: <ad>.bytes  - float16, dilim dilim, satir satir (little-endian)
       <ad>.index.txt - boyutlar, dilim sayisi, eksen degerleri, varlik tablosu

Kullanim:
    python Tools/rain/pack_streaks.py <veritabani_kok> <cikti_klasoru>
"""

import os
import sys
import numpy as np
from PIL import Image

# Dosya adi kodlamasindaki eksen degerleri (spec 5.4.2). SIRA SABIT - Unity tarafi
# dilim indeksini bu siradan hesapliyor, isim aramiyor.
DCAM = [0, 20, 40, 60, 80]
VERT = [-90, -70, -50, -30, -10, 10, 30, 50, 70, 90]
HORZ = [10, 30, 50, 70, 90, 110, 130, 150, 170]
OSC = list(range(10))


def parse_env_max(path):
    """normalized_env_max.txt -> {cv: [10 carpan]}"""
    out, key = {}, None
    for line in open(path, encoding="utf-8"):
        line = line.strip()
        if not line:
            continue
        if line.startswith("cv"):
            key = int(line[2:])
            continue
        if key is None:
            raise ValueError(f"{path}: cv basligi gelmeden sayi satiri")
        vals = [float(x) for x in line.split()]
        if len(vals) != 10:
            raise ValueError(f"{path}: cv{key} icin {len(vals)} carpan, 10 bekleniyor")
        out[key] = vals
        key = None
    return out


def parse_point_max(path):
    """dcam{NN}_point_max.txt -> {(v, h): [10 carpan]}"""
    out, v = {}, None
    for line in open(path, encoding="utf-8"):
        line = line.strip()
        if not line or line.startswith("cv"):
            continue
        if line.startswith("v"):
            v = int(line[1:])
            continue
        if not line.startswith("h"):
            raise ValueError(f"{path}: beklenmeyen satir: {line[:40]}")
        if v is None:
            raise ValueError(f"{path}: v basligi gelmeden h satiri")
        parts = line.split()
        h = int(parts[0][1:])
        vals = [float(x) for x in parts[1:]]
        if len(vals) != 10:
            raise ValueError(f"{path}: v{v} h{h} icin {len(vals)} carpan, 10 bekleniyor")
        out[(v, h)] = vals
    return out


def read_streak(path, factor):
    """PNG -> [0,1] normalize -> carpanla olcekle. Spec 5.4.4."""
    a = np.asarray(Image.open(path))
    if a.dtype != np.uint16:
        raise ValueError(f"{path}: {a.dtype}, uint16 bekleniyor")
    return (a.astype(np.float32) / 65535.0) * factor


def pack_env(root, size, dcam, out_dir):
    src = os.path.join(root, "env_light_database")
    factors = parse_env_max(os.path.join(src, "txt", "normalized_env_max.txt"))

    slices, shape = [], None
    for osc in OSC:
        p = os.path.join(src, f"size{size}", f"cv{dcam}_osc{osc}.png")
        if not os.path.exists(p):
            raise FileNotFoundError(p)
        a = read_streak(p, factors[dcam][osc])
        shape = shape or a.shape
        if a.shape != shape:
            raise ValueError(f"{p}: {a.shape}, {shape} bekleniyor")
        slices.append(a)

    return write(out_dir, f"env_size{size}_dcam{dcam:02d}", slices,
                 [1] * len(slices), shape, axes=[("osc", OSC)])


def pack_point(root, size, dcam, out_dir):
    src = os.path.join(root, "point_light_database")

    slices, present = [], []
    shape = None
    missing = 0

    factors = parse_point_max(os.path.join(src, "txt", f"dcam{dcam:02d}_point_max.txt"))
    folder = os.path.join(src, f"size{size}", f"dcam{dcam:02d}")

    for v in VERT:
        for h in HORZ:
            for osc in OSC:
                # ONEK KLASORLE AYNI, `cv0` DEGIL. Spec 5.4.2 "dosya adi icinde
                # daima cv0" diyor; olculdu, yanlis: dcam20 klasorunde onek cv20.
                p = os.path.join(folder, f"cv{dcam}_v{v}_h{h}_osc{osc}.png")
                # EKSIK KOMBINASYONLAR NORMAL (spec 5.4.5): uc dikey acilarda bazi
                # yatay acilar yok - her dcam'de tam 160/900, olculdu. Sifirla
                # dolduruluyor ve varlik tablosuna 0 yaziliyor; interpolasyon
                # agirligini oradan yeniden normalize edecek.
                if not os.path.exists(p):
                    slices.append(None)
                    present.append(0)
                    missing += 1
                    continue
                a = read_streak(p, factors[(v, h)][osc])
                shape = shape or a.shape
                if a.shape != shape:
                    raise ValueError(f"{p}: {a.shape}, {shape} bekleniyor")
                slices.append(a)
                present.append(1)

    zero = np.zeros(shape, np.float32)
    slices = [s if s is not None else zero for s in slices]

    return write(out_dir, f"point_size{size}_dcam{dcam:02d}", slices, present, shape,
                 axes=[("v", VERT), ("h", HORZ), ("osc", OSC)], missing=missing)


def write(out_dir, name, slices, present, shape, axes, missing=0):
    os.makedirs(out_dir, exist_ok=True)
    data = np.stack(slices).astype(np.float16)

    blob = os.path.join(out_dir, name + ".bytes")
    with open(blob, "wb") as f:
        f.write(data.tobytes())

    with open(os.path.join(out_dir, name + ".index.txt"), "w", encoding="utf-8") as f:
        f.write(f"width {shape[1]}\nheight {shape[0]}\nslices {len(slices)}\n")
        f.write("format R16F\n")
        for axis, values in axes:
            f.write(f"axis {axis} {' '.join(str(v) for v in values)}\n")
        f.write("present " + "".join(str(p) for p in present) + "\n")

    mb = data.nbytes / 1048576
    live = data[np.array(present, bool)]
    gap = f", eksik {missing}" if missing else ""
    print(f"  {name}: {len(slices)} dilim {shape[1]}x{shape[0]}, {mb:.1f} MB, "
          f"deger araligi {live.min():.3e} - {live.max():.3e}{gap}")
    return mb


if __name__ == "__main__":
    root, out = sys.argv[1], sys.argv[2]
    total = 0.0

    # UST LOD size16. Kagitta: 1.6 mm damla 0.5 m'de 60 derece FOV / 1920 px ile 5.9 px,
    # 0.2 m'de 15 px genisliginde iz birakiyor. size32 ancak 25 cm'den yakin damla icin
    # gerekiyor; gerekirse ayni betikle eklenir.
    for size in (4, 8, 16):
        for dcam in DCAM:
            total += pack_env(root, size, dcam, out)
            total += pack_point(root, size, dcam, out)
    print(f"toplam {total:.1f} MB")
