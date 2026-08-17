"""L0 -- kendi olcegimizde Divide Tree sentezi.

Yazarlarin Synthesis.ipynb akisi; degisen yerler ISARETLI ve gerekceli.
"""
import sys, os, time, argparse
import numpy as np, pandas as pd

REPO = r"C:\Users\musta\Desktop\tts\specs\terrain\orometry-terrains-master"
OUT  = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, OUT); sys.path.insert(1, REPO)

ap = argparse.ArgumentParser()
ap.add_argument("--seed", type=int, default=36044)     # MountainSettings.seed
ap.add_argument("--prom-floor", type=float, default=100.0)
ap.add_argument("--out", default="l0_region")
ap.add_argument("--res", type=int, default=1024)
args = ap.parse_args()

os.chdir(REPO)
from utils.coords import *
from utils.metrics import *
from analysis.peaksdata import *
from synthesis.divtree_synthesis import *
sys.path.insert(0, OUT)
import region_profile as rp

REGION_KM, SUMMIT_M = rp.REGION_KM, rp.SUMMIT_M
ANALYSIS_R = 120.0          # CSV'nin kesildigi kutu yaricapi
diskRadius, globalMaxElev = 30, 9000

np.random.seed(args.seed)

# --- veri
df = pd.read_csv(os.path.join(OUT, "data", "regionPeaks", "himalaya.csv"))
raw_max_ft = df['elevation in feet'].max()
# TAVAN ZIRVENIN ALTINDA. Olculdu: dagilimin tavani zirveyle ayni olunca
# prominence/dominance eslesme adimi sentezlenmis bir zirveyi 5789 m'ye
# cikardi ve bizimki ucuncu siraya dustu. Tavan 5500'e cekiliyor: zirve
# INSAAT GEREGI 209 m ustte kalir. Fark uydurma degil -- Everest 8840 ile
# Lhotse 8516 arasindaki 324 m'nin bizim olcegimizdeki karsiligi 209 m.
RIVAL_CEIL_M = 5500.0
SCALE = RIVAL_CEIL_M / (raw_max_ft * 0.3048)
# ÇAKMA. KOT OLCEKLENIYOR. Everest bolgesi 8840 m'ye kadar zirve istiyor,
# bizim zirvemiz 5709 m (korunacak, karar kayitli). Ham istatistikle sentez
# bizim zirveyi baskin BIRAKMAZ. Carpan 0.6458; dominans (prom/kot) bir ORAN
# oldugu icin degismiyor, izolasyon YATAY mesafe oldugu icin olceklenmiyor.
df['elevation in feet'] *= SCALE
df['prominence in feet'] *= SCALE

df = addExtraColumns(df)
# ÇAKMA. PROMINENCE TABANI. Ham yogunluk 540 km'ye tasininca ~38 000 zirve
# cikiyor ve sentez saatler suruyor. 100 m'den alcak tumsek 60 km oteden
# zaten cozulmuyor (bir ekran pikseli 100 km'de ~47 m). Karar kayitli.
before = len(df)
df = df[df['prom'] >= args.prom_floor * SCALE]
print("zirve %d -> %d  (prom tabani %.0f m, olcekli %.0f m)"
      % (before, len(df), args.prom_floor, args.prom_floor * SCALE))
distributions = computeDistributions(df, diskRadius)

# --- karakter maskesi
probMap, elevMapM, weights, meta = rp.build(res=args.res, region_km=REGION_KM, seed=args.seed)
probMapSaddles = probMap.copy()
terrainSize = np.array([REGION_KM, REGION_KM])

# ÇAKMA. mapToPDF UYGULANMIYOR. Yazarlarin akisi elevMap'i bolgenin zirve
# kot histogramina esitliyor (hucre 16) ve probMap<=0 olan yeri sabite
# cekiyor -- yani ova TANIM GEREGI dumduz cikiyor. Olculdu ve reddedildi.
# Zirve veritabaninda ova YOKTUR (270 km yaricapta bile 1000 m alti zirve
# orani %5.9), yani histogram ovayi hic tarif edemiyor. elevMap tasarimdan
# geliyor; dagilim eslesmesi yalniz DAGLIK maskede uygulaniyor.
# HISTOGRAM ESLEMESI DE UYGULANMIYOR. Yazarlarinki zorunlu cunku kontrol
# gorselleri BIRIMSIZ gri tonlama -- 0..255'in metre karsiligi yok, dagilima
# esitlenmesi gerekiyor. Bizim elevMap'imiz zaten METRE ve tasarimdan geliyor
# (ova 186, plato 3600, zirve 5709). Eslemek onu bozuyor: bir turda plato
# tasarim 3600 m iken arazide 1127 m'ye dustu, cunku esleme onu bolgenin
# zirve kot histogramina cekiyordu.
elevMap = np.clip(elevMapM.copy(), rp.PLAIN_M, SUMMIT_M)

# --- zirve sayisi
densityFactor = probMap.mean()      # ÇAKMA: ikili maske degil surekli alan;
                                    # yazarlarin (probMap>0) sayimi 1.0 verirdi
scalingFactor = (REGION_KM / (2 * ANALYSIS_R)) ** 2
totalNumPeaks = int(round(densityFactor * scalingFactor * len(df)))
print("yogunluk %.3f  olcek %.2f  ZIRVE SAYISI %d" % (densityFactor, scalingFactor, totalNumPeaks))

# --- prominence gruplari (veriden, hucre 20/21)
pros = sorted(df['prom'])
thr = [int(pros[int(s * len(pros))]) for s in (8/15, 12/15, 14/15)]
promGroups = [globalMaxElev] + thr[::-1] + [0]
print("promGroups", promGroups)

binD, accD, stepN, limits = [], [], [], []
for gi in range(len(promGroups) - 1):
    mx, mn = promGroups[gi], promGroups[gi + 1]
    g = df[np.logical_and(df['prom'] >= mn, df['prom'] < mx)]
    a = df[df['prom'] >= mn]
    stepN.append(int(round(totalNumPeaks * (len(g) / len(df)))))
    binD.append(computeDistributions(g, diskRadius))
    accD.append(computeDistributions(a, diskRadius))
    limits.append((mn, mx))
print("adim basina", stepN)

# --- SABIT ZIRVE: oyun alaninin merkezinde, tam 5709 m
c = REGION_KM / 2.0
# IKI TANE, BIR TANE DEGIL. Referansta `fixedPeaks[:,2].squeeze()` tek satirda
# sifir boyuta dusuyor ve concatenate patliyor (yazarlarin ornegi dort sabit
# zirveyle yazilmis). Referans DEGISTIRILMIYOR; ikinci zirve konuyor.
# Zaten dogrusu bu: Everest'in yaninda Lhotse ve Nuptse var, tek koni tam da
# kacmaya calistigimiz sey. Ikincisi 4800 m ve 6 km oteye -- acik ara ikinci,
# rakip degil.
SECOND_M, SECOND_KM = 4800.0, 6.0
fixedData = {
    'fixedPeaks': np.array([[c, c, SUMMIT_M],
                            [c + SECOND_KM * 0.72, c - SECOND_KM * 0.69, SECOND_M]]),
    'peakRangeProm': np.array([[SUMMIT_M, SUMMIT_M], [800.0, 2200.0]]),
    'peakRangeDom': np.array([[1.0, 1.0], [800.0/SECOND_M, 2200.0/SECOND_M]]),
    'fixedSaddles': np.empty((0, 3)),
    'fixedSaddlesPeaks': np.empty((0, 2), dtype=int),
}

synthParams = {'promEpsilon': 30, 'globalMaxElev': globalMaxElev, 'terrainSize': terrainSize,
               'elevRangeFilter': 0.25,   # ÇAKMA: 0.5'te plato kotunu tutmuyordu
                                            # (tasarim 3600 m, olcum 1971 m).
                                            # Dusuk deger zirveyi elevMap'e
                                            # daha siki bagliyor. 'maxPeakTrials': 100, 'delaunayRidgeExp': 1.0,
               'updateProbMap': True, 'valleyFactor': 1, 'numHistogramIters': 5}

np.random.seed(args.seed)
t0 = time.perf_counter()
peakCoords, peakElevs, saddleCoords, saddleElevs, saddlePeaks, RidgeTree, dbg = \
    synthDivideTree(distributions, binD, accD, limits, stepN,
                    probMap, probMapSaddles, elevMap, fixedData, synthParams)
peakSaddle, peakParent, peakProms, _ = computeProminences(RidgeTree, peakElevs, saddleElevs, saddlePeaks)
dt = time.perf_counter() - t0

top = np.argmax(peakElevs)
d0 = np.hypot(peakCoords[top, 0] - c, peakCoords[top, 1] - c)
print("\nSENTEZ %.1f s" % dt)
print("  zirve %d  boyun %d" % (peakElevs.size, saddleElevs.size))
print("  kot %.0f..%.0f m" % (peakElevs.min(), peakElevs.max()))
print("  en yuksek: %.0f m, merkezden %.2f km" % (peakElevs[top], d0))
srt = np.sort(peakElevs)[::-1]
print("  ilk bes: %s" % np.round(srt[:5]).astype(int))
print("  ikinciyle fark: %.0f m" % (srt[0] - srt[1]))

np.savez(os.path.join(OUT, args.out + ".npz"),
         peakCoords=peakCoords, peakElevs=peakElevs, saddleCoords=saddleCoords,
         saddleElevs=saddleElevs, saddlePeaks=saddlePeaks, RidgeTree=RidgeTree,
         peakProms=peakProms, peakSaddle=peakSaddle, terrainSize=terrainSize,
         probMap=probMap, elevMap=elevMap, scale=SCALE)
print("yazildi:", args.out + ".npz")
