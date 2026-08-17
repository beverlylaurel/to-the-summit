"""L1 -- oyun alaninin yukseklik haritasi. Ciktisi Unity'nin KAYNAGI.

Zincir: L0 grafigi -> merkez cevresinde kirp -> divideTreeToMesh -> 4097^2
rasterle -> multifraktal gurultu + kisitli erozyon -> PNG-16.

NEDEN PNG REPOYA GIRIYOR. `.gitignore` bir donem "arazi varliklari tohum ve
ayarlardan yeniden uretiliyor" gerekcesiyle 165 MB'i disliyordu (LFS'e 3.8 GB
gittigi icin). L1 SONRASI O ONERME YANLIS: arazi Python + Argudo + 1.4 GB
Kirmse veritabani olmadan uretilemiyor ve bunlarin hicbiri repoda degil.

Yani yukseklik haritasi artik TURETILMIS degil KAYNAK. Olculdu:
    PNG-16 sikistirilmis  13.8 MB   <- bu
    npz (deflate)         28.8 MB
    ham .r16              32.0 MB
Nicemleme hatasi ortalama 2.4 cm, en kotu 4.7 cm; Unity arazisi zaten 16 bit.

Turetilenler (TerrainData 34.6 MB, normal 44.7, ufuk 67, yuzey 11.2,
birikinti 2.8) `.gitignore`'da kalmaya devam ediyor -- onlar bu haritadan tek
adimda pisiyor.
"""
import argparse
import os
import sys
import time

import numpy as np
from PIL import Image
from scipy.spatial import Delaunay

REPO = r"C:\Users\musta\Desktop\tts\specs\terrain\orometry-terrains-master"
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(1, REPO)

# --- olcek sabitleri. Ucu de MountainSettings.asset'ten; degistirilirse
#     SCALE.md ile birlikte degisir.
#
# OYUN ALANI 17.5 -> 30 km. Olculdu: 17.5 km'de arazinin KUZEY VE DOGU
# KENARININ TAMAMI 3000-4000 m'de kesiliyordu (kenar kotu ortancasi 3665 ve
# 3873 m) ve dikey duvar olarak kaliyordu. Kutle o kareye sigmiyor.
#
# Not: buyutmek kesigi TAMAMEN kaldirmiyor. Olculdu -- 1500 m esiginde kutle
# 379 km'ye kadar uzaniyor, cunku bu bir SILSILE. Kesigi asil kapatan yakin
# bant (18-60 km) olacak; buyutme kutlenin govdesini iceri aliyor.
#
# 4097 ornekte aralik 4.28 -> 7.32 m. Tirmanilan yuzeyler mesh modul olacagi
# icin (karar DECISIONS.md'de) yukseklik haritasinin isi yurunen zemin.
PLAY_M = 30000.0
HEIGHT_SUMMIT_M = 5709.0          # oyun alaninin bir kenari
HEIGHT_M = 6189.0         # terrainHeight -- NICEMLEME BUNA GORE
RES = 4097                # heightmapResolution
CROP_KM = 40.0            # mesh uretim penceresi; oyun alanindan genis olmali
REFINE_KM = 0.030         # mesh ucgen boyu
POISSON_KM = 0.045
R_INFL_M = 400.0          # zirve/boyun koruma yaricapi
NOISE_AMP_M = 52.0        # < prominence tabani (100 m, olcekli 65 m) -- spec §5.7
NOISE_BASE_M = 320.0
# `MountainRoute.asset` spawn'i, normalize arazi koordinati. Eksen probu
# bunu kullaniyor -- kutupsal yaklasim degil.
SPAWN_UV = (0.229200, 0.225120)


def bake(npz_path, seed, out_path, verify):
    os.chdir(REPO)
    from utils.poisson import PoissonDisc
    from synthesis.divtree_to_dem import divideTreeToMesh
    import detail

    d = np.load(npz_path)
    pc, pe = d["peakCoords"], d["peakElevs"]
    sc, se, sp = d["saddleCoords"], d["saddleElevs"], d["saddlePeaks"]
    centre = float(d["terrainSize"][0]) / 2.0

    # --- kirpim: mesh tum 540 km'de uretilemez (4.28 m'de 126 000^2 ornek)
    keep = (np.abs(pc[:, 0] - centre) <= CROP_KM / 2) & (np.abs(pc[:, 1] - centre) <= CROP_KM / 2)
    idx = np.where(keep)[0]
    remap = -np.ones(pe.size, dtype=int)
    remap[idx] = np.arange(idx.size)
    sk = keep[sp[:, 0]] & keep[sp[:, 1]]
    origin = centre - CROP_KM / 2
    npc, npe = pc[idx] - origin, pe[idx]
    nsc, nse, nsp = sc[sk] - origin, se[sk], remap[sp[sk]]
    print("kirpim %.0f km: zirve %d -> %d, boyun %d -> %d"
          % (CROP_KM, pe.size, npe.size, se.size, nse.size))

    pfile = os.path.join(HERE, "poisson_%dkm_%dm.npy" % (CROP_KM, REFINE_KM * 1000))
    if os.path.exists(pfile):
        ps = np.load(pfile)
    else:
        t0 = time.perf_counter()
        ps = PoissonDisc(width=CROP_KM, height=CROP_KM, r=POISSON_KM, k=15).sample()
        ps = np.array([[s[0], s[1]] for s in ps])
        np.save(pfile, ps)
        print("poisson %.0f s" % (time.perf_counter() - t0))

    params = {
        'minTerrainElev': 0.5 * nse.min(), 'maxSlopeCoeff': 0.5,
        'refineDistance': REFINE_KM, 'riversPerturbation': 0.20,
        'ridgesPerturbation': 0.15, 'useDrainageArea': True, 'maxRiverWidth': 0.3,
        'coarseRiverSmoothIters': 4, 'refinedRiverSmoothIters': 5,
        'refinedRiverSmoothPosIters': 1, 'srcElevRndMean': 50, 'srcElevRndStd': 20,
        'momentumCoarseRiverSourceElevs': 0.5, 'momentumRiverSourceElev': 0.75,
        'momentumRiverSourceCoords': 0.7,
    }
    np.random.seed(seed)
    t0 = time.perf_counter()
    mv, me, _, _ = divideTreeToMesh(npc, npe, nsc, nse, nsp, [CROP_KM, CROP_KM], ps, params)
    print("mesh %.0f s  kose %d" % (time.perf_counter() - t0, mv.shape[0]))

    # --- merkezdeki oyun alanini rasterle
    off = (CROP_KM - PLAY_M / 1000.0) / 2.0
    t0 = time.perf_counter()
    hf = _raster(mv, me, [CROP_KM, CROP_KM], RES, off, PLAY_M / 1000.0)
    print("raster %.0f s  %dx%d  %.0f..%.0f m" % (time.perf_counter() - t0, RES, RES, hf.min(), hf.max()))

    # --- detay katmani (spec §5.7, §5.8)
    cell = PLAY_M / (RES - 1)
    nodes = _local_nodes(d, centre)
    u = detail.uplift_field(hf.shape, cell, (0.0, 0.0), nodes, R_INFL_M)
    noise = detail.multifractal(hf.shape, cell, NOISE_BASE_M, 8, NOISE_AMP_M, seed=seed)
    h = detail.multiscale_erosion(hf + noise * (1.0 - u), cell, u)

    # SIVRI TORPUSU. Ucgenlestirme + gurultu, izgaraya capraz sirtlarda tek
    # hucrelik igneler birakiyor: torpusuz olculdu, 5249 hucre komsularinin
    # 400 m ustunde, en kotusu 1343 m. Ekranda siyah sivri olarak gorunuyor.
    #
    # Torpu YALNIZ DAGA uygulaniyor. Ovada pencere 2 ornek = 14.7 m, yani ovanin
    # en ince tepeciginin boyu; dort turda %4'e inip duzlugu cam gibi yapiyor.
    yy, xx = np.meshgrid(np.arange(RES), np.arange(RES), indexing="ij")
    rr = np.hypot(yy - (RES - 1) * 0.5, xx - (RES - 1) * 0.5) * cell
    #
    # DUGUMLER KORUNUYOR. Egim tavani tek basina birakilinca zirveyi 5709 -> 5608 m'ye
    # indirdi: zirve konisi son 15 metrede 70 m dusuyor, yani 78 derece, tavanin
    # ustunde. Ama o GERCEK -- L0'dan gelen bir dugum, ucgenlestirme artigi degil.
    # Ayirt eden sey zaten elimizde: `u`, gurultunun ve erozyonun zirveleri korumak
    # icin kullandigi ayni alan. Ucuncu bir olcut uydurulmuyor.
    skirt = np.clip((9500.0 - rr) / 1500.0, 0.0, 1.0).astype(np.float32) * (1.0 - u)
    h = detail.file_crests(h, cell, skirt_mask=skirt)
    print("detay  %.0f..%.0f m" % (h.min(), h.max()))

    # --- 16 bit. NICEMLEME `terrainHeight`E GORE, h.max()'a gore DEGIL:
    #     maksimuma normalize edilirse dagin boyu tohumdan tohuma kayar ve
    #     SCALE.md'deki her sey bozulur.
    q = np.clip(h / HEIGHT_M, 0.0, 1.0)
    u16 = (q * 65535.0 + 0.5).astype(np.uint16)
    # UNITY SIRASINDA YAZILIYOR. Dizi [dogu, kuzey] indeksli; Unity
    # `SetHeights` [z, x] yani [kuzey, dogu] bekliyor. Devrik yazilirsa dag
    # 90 derece doner -- L0'da tam bu bir kez yasandi (dogu-bati silsile
    # kuzey-guney serit cikmisti). Cevrim BURADA yapiliyor, ice aktarmada
    # ikinci bir cevrim YOK.
    Image.fromarray(u16.T).save(out_path, optimize=True)
    size_mb = os.path.getsize(out_path) / 1048576
    print("yazildi: %s  (%.1f MB)" % (out_path, size_mb))

    if verify:
        raw = np.asarray(Image.open(out_path)).astype(np.float64) / 65535.0 * HEIGHT_M
        back = raw.T                       # [kuzey, dogu] -> [dogu, kuzey]
        err = np.abs(back - h)
        top = np.unravel_index(np.argmax(back), back.shape)
        print("DENETIM")
        print("  geri okuma hatasi: ortalama %.4f m  en kotu %.4f m" % (err.mean(), err.max()))
        print("  zirve %.1f m, merkezden %.1f m"
              % (back.max(), np.hypot(top[0] - (RES - 1) / 2, top[1] - (RES - 1) / 2) * cell))

        # EKSEN PROBU. Zirve merkezde oldugu icin devriklik yukaridaki
        # denetimden KACAR -- merkez devrik altinda da merkezdir. Ayirt eden
        # nokta gerekiyor: SPAWN. `MountainRoute.asset` onu normalize (0..1)
        # arazi koordinatinda tutuyor, guneybati kosesinde ve ovada. Devrik
        # yazilsaydi kuzeydoguya duserdi ve orasi daglik.
        #
        # Konum kutupsal YAKLASIMLA hesaplanmiyor: bir tur "212 derece, 11.58
        # km" denendi ve dizi sinirini asti. Gercek spawn 225.4 derecede.
        sx, sy = SPAWN_UV
        px, py = int(sx * (RES - 1)), int(sy * (RES - 1))
        spawn_m = back[px, py]
        ne_m = back[RES - 1 - px, RES - 1 - py]
        print("  spawn (GB kose, ova)  %6.0f m" % spawn_m)
        print("  karsi kose (KD)       %6.0f m" % ne_m)
        if spawn_m > 1500.0:
            raise SystemExit("HATA: spawn %.0f m -- ova orada degil, eksen devrik olabilir." % spawn_m)
        if ne_m < spawn_m:
            raise SystemExit("HATA: kuzeydogu kosesi guneybatidan alcak -- eksen devrik.")
        if err.max() > 0.1:
            raise SystemExit("HATA: geri okuma farki 0.1 m'yi asiyor.")

        # KENAR DENETIMI -- SERT. Sart: dag arazi sinirinda KESILMEYECEK ve
        # dagin 360 derece cevresinde yurunebilir ova olacak.
        #
        # Bir tur bu denetim yoktu ve 17.5 km'lik karede kuzey/dogu kenarinin
        # TAMAMI 3665-3873 m'de kesildi; ekranda dikey duvar olarak kaldi.
        # Bir daha sessizce olmasin diye uretim burada duruyor.
        EDGE_MAX_M = 1200.0
        for name, strip in (("bati", back[0, :]), ("dogu", back[-1, :]),
                            ("guney", back[:, 0]), ("kuzey", back[:, -1])):
            print("  kenar %-6s ortanca %5.0f m  max %5.0f m" % (name, np.median(strip), strip.max()))
            if strip.max() > EDGE_MAX_M:
                raise SystemExit(
                    "HATA: %s kenarinda kot %.0f m (tavan %.0f m). Dag sinirda kesiliyor; "
                    "maskedeki yalitim halkasi yetmiyor ya da arazi kucuk."
                    % (name, strip.max(), EDGE_MAX_M))

        # ZIRVE SPAWN'DAN GORUNMELI. Gidilecek yer o; gorunmezse oyunun hedefi yok.
        # Bir tur 124 m ile kapaliydi ve ekranda "dikdortgen dag" olarak okundu.
        # Aciklik su an 24 m -- ince. Gurultu tohumu degisince sessizce kapanabilir,
        # onun icin uretim burada duruyor.
        #
        # ALET ONCE YANILTTI: zirvenin KENDI hucresi engel sayiliyordu (dunya
        # egriligi gorus hattini son metrelerde zirvenin altina indiriyor) ve
        # "+12 m KAPALI" okunuyordu. Isinin son 150 m'si disarida.
        p0 = np.array([SPAWN_UV[0] * (RES - 1), SPAWN_UV[1] * (RES - 1)])
        p1 = np.array([(RES - 1) * 0.5, (RES - 1) * 0.5])
        tt = np.linspace(0.0, 1.0, 4000)
        pp = p0[None, :] + (p1 - p0)[None, :] * tt[:, None]
        zz = back[np.round(pp[:, 0]).astype(int), np.round(pp[:, 1]).astype(int)]
        dd = np.hypot(*(p1 - p0)) * cell * tt
        los = (zz[0] + 1.7) + (HEIGHT_SUMMIT_M - zz[0] - 1.7) * tt - dd * dd / (2.0 * 6371000.0)
        seg = (dd > 50.0) & (dd < dd[-1] - 150.0)
        clear = -(zz - los)[seg].max()
        print("  zirve spawn'dan: aciklik %+.0f m (%.1f km)" % (clear, dd[-1] / 1000.0))
        if clear <= 0.0:
            raise SystemExit(
                "HATA: zirve spawn'dan gorunmuyor, %.0f m ile kapali." % -clear)
    return h


def _raster(coords, elevs, terrain_km, n, off_km, span_km):
    ax = np.linspace(off_km, off_km + span_km, n)
    xv, yv = np.meshgrid(ax, ax, indexing='ij')
    pix = np.stack([xv.ravel(), yv.ravel()], 1)
    corners = np.array([[0, 0], [0, terrain_km[1]], [terrain_km[0], terrain_km[1]], [terrain_km[0], 0]])
    pc = np.concatenate([coords, corners])
    pe = np.concatenate([elevs, np.full(4, elevs.min())])[:, None]
    dt = Delaunay(pc)
    tri = dt.find_simplex(pix)
    X = dt.transform[tri, :2]
    Y = pix - dt.transform[tri, 2]
    b = np.einsum('...ij,...j->...i', X, Y)
    bc = np.c_[b, 1 - b.sum(axis=1)]
    return np.einsum('ij,ijk->ik', bc, pe[dt.simplices[tri]]).reshape(n, n)


def _local_nodes(d, centre):
    """Zirve ve boyunlar, oyun alaninin sol-alt kosesine gore metre."""
    def loc(a):
        return (a - centre) * 1000.0 + PLAY_M / 2.0
    nodes = np.vstack([
        np.stack([loc(d["peakCoords"][:, 0]), loc(d["peakCoords"][:, 1])], 1),
        np.stack([loc(d["saddleCoords"][:, 0]), loc(d["saddleCoords"][:, 1])], 1)])
    m = ((nodes[:, 0] > -3000) & (nodes[:, 0] < PLAY_M + 3000)
         & (nodes[:, 1] > -3000) & (nodes[:, 1] < PLAY_M + 3000))
    return nodes[m]


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--npz", default=os.path.join(HERE, "l0_region.npz"))
    ap.add_argument("--seed", type=int, default=36044)
    ap.add_argument("--out", default=os.path.normpath(
        os.path.join(HERE, "..", "..", "Assets", "Terrain", "MountainHeightmap.png")))
    ap.add_argument("--verify", action="store_true")
    a = ap.parse_args()
    bake(a.npz, a.seed, a.out, a.verify)
