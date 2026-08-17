"""L1 -- OYUN ALANI icin ince cozunurluklu yukseklik haritasi.

Bolge 540 km; `divideTreeToMesh` tum alani isliyor ve 4.28 m/piksel orada
126 000^2 ornek demek -- imkansiz. Bu yuzden grafik merkez cevresinde
KIRPILIYOR (40 km), mesh o kirpimda uretiliyor, ortadaki 17.5 km aliniyor.
Kirpma kenarindaki bozulma oyun alanindan 11 km uzakta kaliyor.

Mesh MAKRO sekli veriyor; ince detay (erozyon + gurultu) ayri katman.
"""
import sys, os, time, numpy as np
import matplotlib; matplotlib.use("Agg")
import matplotlib.pyplot as plt
from scipy.spatial import Delaunay

REPO = r"C:\Users\musta\Desktop\tts\specs\terrain\orometry-terrains-master"
OUT  = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, OUT); sys.path.insert(1, REPO)
os.chdir(REPO)
from utils.poisson import PoissonDisc
from synthesis.divtree_to_dem import *

CROP_KM  = 26.0        # kirpim (ince mesh icin daralttildi)
PLAY_KM  = 17.517      # alinacak merkez
PIX_M    = PLAY_KM * 1000 / 4096   # 4097 ornek -> 4.28 m

d = np.load(os.path.join(OUT, "l0_region.npz"))
pc, pe = d["peakCoords"], d["peakElevs"]
sc, se, sp_ = d["saddleCoords"], d["saddleElevs"], d["saddlePeaks"]
c = float(d["terrainSize"][0]) / 2.0

keep = (np.abs(pc[:,0]-c) <= CROP_KM/2) & (np.abs(pc[:,1]-c) <= CROP_KM/2)
idx = np.where(keep)[0]
remap = -np.ones(pe.size, dtype=int); remap[idx] = np.arange(idx.size)
sk = keep[sp_[:,0]] & keep[sp_[:,1]]
npc = pc[idx] - (c - CROP_KM/2)
npe = pe[idx]
nsc = sc[sk] - (c - CROP_KM/2)
nse = se[sk]
nsp = remap[sp_[sk]]
print("kirpim %.0f km: zirve %d -> %d, boyun %d -> %d"
      % (CROP_KM, pe.size, npe.size, se.size, nse.size))
print("kot %.0f..%.0f m" % (npe.min(), npe.max()))

ts = [CROP_KM, CROP_KM]
pf = os.path.join(OUT, "poisson_play_fine.npy")
if os.path.exists(pf):
    ps = np.load(pf)
else:
    t0 = time.perf_counter()
    ps = PoissonDisc(width=CROP_KM, height=CROP_KM, r=0.045, k=15).sample()
    ps = np.array([[s[0], s[1]] for s in ps]); np.save(pf, ps)
    print("poisson %.0f s" % (time.perf_counter()-t0))
print("poisson ornek %d" % ps.shape[0])

params = {'minTerrainElev': 0.5*nse.min(), 'maxSlopeCoeff': 0.5, 'refineDistance': 0.030,
          'riversPerturbation': 0.20, 'ridgesPerturbation': 0.15, 'useDrainageArea': True,
          'maxRiverWidth': 0.3, 'coarseRiverSmoothIters': 4, 'refinedRiverSmoothIters': 5,
          'refinedRiverSmoothPosIters': 1, 'srcElevRndMean': 50, 'srcElevRndStd': 20,
          'momentumCoarseRiverSourceElevs': 0.5, 'momentumRiverSourceElev': 0.75,
          'momentumRiverSourceCoords': 0.7}
np.random.seed(36044)
t0 = time.perf_counter()
mv, me, mt, dbg = divideTreeToMesh(npc, npe, nsc, nse, nsp, ts, ps, params)
print("MESH %.0f s  kose %d" % (time.perf_counter()-t0, mv.shape[0]))

def hfield(coords, elevs, ts, n, x0, y0, span):
    """Yalniz [x0,x0+span] x [y0,y0+span] penceresini n x n rasterler."""
    ax = np.linspace(x0, x0+span, n)
    ay = np.linspace(y0, y0+span, n)
    xv, yv = np.meshgrid(ax, ay, indexing='ij')
    pix = np.stack([xv.ravel(), yv.ravel()], 1)
    pC = np.concatenate([coords, np.array([[0,0],[0,ts[1]],[ts[0],ts[1]],[ts[0],0]])])
    pE = np.concatenate([elevs, np.full(4, elevs.min())])[:,None]
    dt = Delaunay(pC); tr = dt.find_simplex(pix)
    X = dt.transform[tr,:2]; Y = pix - dt.transform[tr,2]
    b = np.einsum('...ij,...j->...i', X, Y); bc = np.c_[b, 1-b.sum(axis=1)]
    ie = np.einsum('ij,ijk->ik', bc, pE[dt.simplices[tr]])
    return ie.reshape(n, n)

N = 4097
o = (CROP_KM - PLAY_KM) / 2.0
t0 = time.perf_counter()
hf = hfield(mv, me, ts, N, o, o, PLAY_KM)
print("DEM %.0f s  %dx%d  %.1f m/ornek  kot %.0f..%.0f m"
      % (time.perf_counter()-t0, N, N, PIX_M, hf.min(), hf.max()))
np.save(os.path.join(OUT, "l1_play_dem_fine.npy"), hf)

def shade(z,dx,az=315,alt=45):
    gy,gx = np.gradient(z,dx)
    sl=np.pi/2-np.arctan(np.hypot(gx,gy)); asp=np.arctan2(-gx,gy)
    a,z0=np.radians(az),np.radians(alt)
    return np.clip(np.sin(z0)*np.sin(sl)+np.cos(z0)*np.cos(sl)*np.cos(a-asp),0,1)

img = np.flipud(hf.T)
fig,ax = plt.subplots(figsize=(12,12))
ax.imshow(img, cmap='terrain', vmin=0, vmax=5709)
ax.imshow(shade(img, PIX_M), cmap='gray', alpha=0.45)
ax.set_title('L1 OYUN ALANI — 17.5x17.5 km, 4.28 m/ornek (kuzey yukari)'); ax.axis('off')
fig.savefig(os.path.join(OUT,'09_l1_oyun_alani_ince.png'), dpi=115, bbox_inches='tight')

gy,gx = np.gradient(hf, PIX_M)
slope = np.degrees(np.arctan(np.hypot(gx,gy)))
print("\nEGIM DAGILIMI (oyun alani):")
for lo,hi,ad in [(0,15,'yurunur'),(15,30,'dik yurunur'),(30,45,'el-ayak'),(45,90,'tirmanis')]:
    print("  %-12s %2d-%2d derece  %%%.1f" % (ad, lo, hi, 100*((slope>=lo)&(slope<hi)).mean()))
print("  ortanca egim %.1f derece" % np.median(slope))
