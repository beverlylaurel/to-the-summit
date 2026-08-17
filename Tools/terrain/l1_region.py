"""L1 -- bolge olceginde DEM. Yalniz DOGRULAMA icin: karakterler araziye
donusunce dogru gorunuyor mu? Oyun alaninin ince cozunurluklu uretimi ayri.
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

d = np.load(os.path.join(OUT, "l0_region.npz"))
pc, pe = d["peakCoords"], d["peakElevs"]
sc, se, sp_ = d["saddleCoords"], d["saddleElevs"], d["saddlePeaks"]
terrainSize = [540.0, 540.0]
print("zirve %d  boyun %d  kot %.0f..%.0f" % (pe.size, se.size, pe.min(), pe.max()))

# Referansin olcegi 90 km'ye gore: refine 0.12, poisson 0.18. Bolge 6 kat
# buyuk, ayni ORAN korunuyor -- 0.72 / 1.08 km.
PR, REFINE = 1.08, 0.72
pf = os.path.join(OUT, "poisson_region.npy")
if os.path.exists(pf):
    ps = np.load(pf)
else:
    t0 = time.perf_counter()
    ps = PoissonDisc(width=540, height=540, r=PR, k=15).sample()
    ps = np.array([[s[0], s[1]] for s in ps]); np.save(pf, ps)
    print("poisson %.0f s" % (time.perf_counter()-t0))
print("poisson ornek %d" % ps.shape[0])

params = {'minTerrainElev': 0.5*se.min(), 'maxSlopeCoeff': 0.5, 'refineDistance': REFINE,
          'riversPerturbation': 0.20, 'ridgesPerturbation': 0.15, 'useDrainageArea': True,
          'maxRiverWidth': 0.3, 'coarseRiverSmoothIters': 4, 'refinedRiverSmoothIters': 5,
          'refinedRiverSmoothPosIters': 1, 'srcElevRndMean': 50, 'srcElevRndStd': 20,
          'momentumCoarseRiverSourceElevs': 0.5, 'momentumRiverSourceElev': 0.75,
          'momentumRiverSourceCoords': 0.7}
np.random.seed(36044)
t0 = time.perf_counter()
mv, me, mt, dbg = divideTreeToMesh(pc, pe, sc, se, sp_, terrainSize, ps, params)
print("MESH %.0f s  kose %d  ucgen %d" % (time.perf_counter()-t0, mv.shape[0], mt.shape[0]))

def hfield(coords, elevs, ts, hfsize):
    x = np.linspace(0,1,hfsize[0]+1)[:-1] + 0.5/hfsize[0]
    y = np.linspace(0,1,hfsize[1]+1)[:-1] + 0.5/hfsize[1]
    xv,yv = np.meshgrid(x,y); pix = np.array([xv.flatten(), yv.flatten()]).T
    pC = np.concatenate([coords/ts, np.array([[0,0],[0,1],[1,1],[0,0]])])
    pE = np.concatenate([elevs, np.array([0,0,0,0])])[:,None]
    dt = Delaunay(pC); tr = dt.find_simplex(pix)
    X = dt.transform[tr,:2]; Y = pix - dt.transform[tr,2]
    b = np.einsum('...ij,...j->...i', X, Y); bc = np.c_[b, 1-b.sum(axis=1)]
    ie = np.einsum('ij,ijk->ik', bc, pE[dt.simplices[tr]])
    px = (hfsize*pix).astype(int); hf = np.zeros(hfsize)
    hf[px[:,0], px[:,1]] = ie.flatten(); return hf

PIX = 150.0
hs_ = (np.array(terrainSize)*1000/PIX).astype(int)
t0 = time.perf_counter()
hf = hfield(mv, me, terrainSize, hs_)
print("DEM %.0f s  %s  %.0f..%.0f m" % (time.perf_counter()-t0, hs_, hf.min(), hf.max()))
np.save(os.path.join(OUT,"l1_region_dem.npy"), hf)

def shade(z,dx,az=315,alt=45):
    gy,gx = np.gradient(z,dx)
    sl=np.pi/2-np.arctan(np.hypot(gx,gy)); asp=np.arctan2(-gx,gy)
    a,z0=np.radians(az),np.radians(alt)
    return np.clip(np.sin(z0)*np.sin(sl)+np.cos(z0)*np.cos(sl)*np.cos(a-asp),0,1)

img = np.flipud(hf.T)          # X-ilk -> ekranda kuzey yukari
fig,ax = plt.subplots(figsize=(13,13))
ax.imshow(img, cmap='terrain', vmin=0, vmax=5709)
ax.imshow(shade(img,PIX), cmap='gray', alpha=0.45)
n = hs_[0]; h = (17.517/540)*n/2
from matplotlib.patches import Rectangle
ax.add_patch(Rectangle((n/2-h,n/2-h),2*h,2*h,fill=False,ec='red',lw=2))
ax.set_title('L1 bolge — 540x540 km, 150 m/piksel (kuzey yukari)'); ax.axis('off')
fig.savefig(os.path.join(OUT,"07_l1_bolge.png"), dpi=110, bbox_inches='tight')

# kesitler: kuzey-guney hat, karakterleri sayiyla gostersin
mid = hs_[0]//2
prof = hf[mid, :]
km = np.arange(hs_[1])*PIX/1000 - 270
fig,ax = plt.subplots(figsize=(15,4))
ax.plot(km, prof, lw=0.8, color='#333'); ax.fill_between(km, 0, prof, color='#c9b79c')
ax.set_xlabel('guney  <-  km  ->  kuzey'); ax.set_ylabel('kot (m)')
ax.set_title('Kuzey-guney kesit (merkezden)'); ax.grid(alpha=.3)
fig.tight_layout(); fig.savefig(os.path.join(OUT,"08_kesit.png"), dpi=110, bbox_inches='tight')
for name, sl in [("ova (G 150..250 km)", slice(int((270-250)/540*hs_[1]), int((270-150)/540*hs_[1]))),
                 ("silsile (merkez ±40 km)", slice(int((270-40)/540*hs_[1]), int((270+40)/540*hs_[1]))),
                 ("plato (K 120..250 km)", slice(int((270+120)/540*hs_[1]), int((270+250)/540*hs_[1])))]:
    v = hf[mid, sl]
    print("  %-24s kot %6.0f..%6.0f  ortanca %6.0f  std %5.0f" % (name, v.min(), v.max(), np.median(v), v.std()))
print("TAMAM")
