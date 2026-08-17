"""L0 ciktisini Unity'nin okuyacagi metin bicimine cevirir.

KARARLI KIMLIK: dugumun kimligi dizideki SIRASIDIR. Sabit zirveler once,
sonra uretilenler; ayni tohum ayni sirayi verir. Icerik (kamp, konak, magara,
anit, zirve modulu) bu kimliklere capalanacak -- karar `DECISIONS.md`.

`utils/divtree_reader.readDivideTree` KULLANILMIYOR: o okurken kirpma
sinirinda dugumleri yeniden siraliyor (`peakReorder`, `saddleReorder`) ve
kimlik kararliligini bozar. Diziler npz'den dogrudan yaziliyor.

Konumlar METRE ve bolge MERKEZINE gore. Merkez = zirve = Unity arazisinin
origini (`MountainGenerator` araziyi -terrainSize/2'ye koyuyor, yani zirve
origin'de kaliyor -- `SCALE.md`).
"""
import argparse, os, numpy as np

OUT = os.path.dirname(os.path.abspath(__file__))
ap = argparse.ArgumentParser()
ap.add_argument("--npz", default=os.path.join(OUT, "l0_region.npz"))
ap.add_argument("--out", default=os.path.normpath(
    os.path.join(OUT, "..", "..", "Assets", "Terrain", "DivideTree.txt")))
ap.add_argument("--seed", type=int, default=36044)
ap.add_argument("--prom-floor", type=float, default=100.0)
ap.add_argument("--play-km", type=float, default=17.517)
args = ap.parse_args()

d = np.load(args.npz)
pc, pe, pp = d["peakCoords"], d["peakElevs"], d["peakProms"]
sc, se, sp_ = d["saddleCoords"], d["saddleElevs"], d["saddlePeaks"]
region_km = float(d["terrainSize"][0])
scale = float(d["scale"])
c = region_km / 2.0

lines = ["# to-the-summit divide tree L0",
         "format 1",
         "seed %d" % args.seed,
         "regionKm %.3f" % region_km,
         "playKm %.3f" % args.play_km,
         "summitM %.1f" % pe.max(),
         "promFloorM %.1f" % args.prom_floor,
         "elevScale %.6f" % scale,
         "peaks %d" % pe.size]
for i in range(pe.size):
    lines.append("%d %.2f %.2f %.2f %.2f" % (
        i, (pc[i, 0] - c) * 1000.0, (pc[i, 1] - c) * 1000.0, pe[i], pp[i]))
lines.append("saddles %d" % se.size)
for i in range(se.size):
    lines.append("%d %.2f %.2f %.2f %d %d" % (
        i, (sc[i, 0] - c) * 1000.0, (sc[i, 1] - c) * 1000.0, se[i],
        sp_[i, 0], sp_[i, 1]))

os.makedirs(os.path.dirname(args.out), exist_ok=True)
open(args.out, "w", encoding="utf-8", newline=chr(10)).write(chr(10).join(lines) + chr(10))

top = int(np.argmax(pe))
print("yazildi: %s" % args.out)
print("  zirve %d  boyun %d" % (pe.size, se.size))
print("  en yuksek %.0f m, merkezden %.1f m" % (
    pe[top], np.hypot(pc[top, 0] - c, pc[top, 1] - c) * 1000.0))
print("  boyut %.1f KB" % (os.path.getsize(args.out) / 1024))
