"""Yuzey detayi: multifraktal gurultu + KISITLI erozyon.

Spec `terrain-generation-spec.md`:
  §3.5  multifraktal yineleme -- t_{k+1} = alfa(t_k) * a_{k+1} * n(phi*p) + t_k
  §3.6  her oktava rotasyon+oteleme; hizali oktavlar IZGARA ARTEFAKTI uretir
  §5.7  genlik prominence esiginin ALTINDA olmali, yoksa orometri bozulur
  §5.8  erozyon TEK BASINA uygulanmaz: zirve/boyun cevresinde telafi edici
        uplift ile ic ice gecer. U = g(d/R), g(r) = (1-r^2)^3
        H_{i+1} = E_i + U * dH_i,  dH_i = E_i - H_i
        Uc olcek: 100 m, 50 m, 30 m

Ilk denemem (bu oturumda) basarisiz oldu cunku erozyon telafisiz calisti ve
eklenen detayi da orometriyi de yedi. Telafi §5.8'in tam olarak var olma
sebebi.
"""
import numpy as np


def _value_noise(shape, cell_m, wavelength_m, rot, offset, rng):
    """Tek oktav deger gurultusu, DONDURULMUS ornekleme ile (§3.6)."""
    ny, nx = shape
    yy, xx = np.mgrid[0:ny, 0:nx]
    px = xx * cell_m
    py = yy * cell_m
    ca, sa = np.cos(rot), np.sin(rot)
    rx = (ca * px - sa * py) / wavelength_m + offset[0]
    ry = (sa * px + ca * py) / wavelength_m + offset[1]
    x0 = np.floor(rx).astype(np.int64)
    y0 = np.floor(ry).astype(np.int64)
    tx, ty = rx - x0, ry - y0
    sx = tx * tx * (3 - 2 * tx)
    sy = ty * ty * (3 - 2 * ty)

    def h(a, b):
        k = (a * 374761393 + b * 668265263 + rng) & 0xFFFFFFFF
        k = (k ^ (k >> 13)) * 1274126177 & 0xFFFFFFFF
        return ((k ^ (k >> 16)) & 0xFFFFFF) / 0xFFFFFF

    v00, v10 = h(x0, y0), h(x0 + 1, y0)
    v01, v11 = h(x0, y0 + 1), h(x0 + 1, y0 + 1)
    return ((v00 * (1 - sx) + v10 * sx) * (1 - sy) +
            (v01 * (1 - sx) + v11 * sx) * sy) * 2.0 - 1.0


def multifractal(shape, cell_m, base_wavelength_m, octaves, amplitude_m, seed=1):
    """§3.5. `alfa` MAKALEDE VERILMIYOR (spec §9.1 acik nokta olarak isaretli).

    Burada alfa = o ana kadar birikmis degerin [0,1]'e normalize hali:
    dusuk yerde yuksek frekans bastirilir (vadi duzlesir), yuksek yerde
    guclendirilir (zirve detay kazanir) -- makalenin tarif ettigi DAVRANIS
    bu, formul degil. Secim burada kayitli, uydurma bir sey saklanmiyor.
    """
    rs = np.random.default_rng(seed)
    t = _value_noise(shape, cell_m, base_wavelength_m, 0.0, (0.0, 0.0), seed)
    amp, wl = 0.5, base_wavelength_m
    for k in range(1, octaves):
        amp *= 0.5
        wl *= 0.5
        rot = rs.uniform(0, np.pi)              # §3.6: her oktav dondurulur
        off = rs.uniform(0, 1000, 2)
        a = t - t.min()
        a /= max(1e-6, a.max())                 # alfa
        t = t + a * amp * _value_noise(shape, cell_m, wl, rot, off, seed + k * 7919)
    t -= t.mean()
    m = np.abs(t).max()
    return t / max(1e-6, m) * amplitude_m       # §5.7: genlik sinirli


def uplift_field(shape, cell_m, origin_m, nodes_m, radius_m):
    """§5.8. g(r) = (1-r^2)^3, r = d(p,T)/R_infl. Kompakt destekli, turevi
    sinirda sifir -- Mach bandi uretmez (SYMPTOMS.md 'sert kirpma')."""
    from scipy.spatial import cKDTree
    ny, nx = shape
    yy, xx = np.mgrid[0:ny, 0:nx]
    pts = np.stack([(xx * cell_m + origin_m[0]).ravel(),
                    (yy * cell_m + origin_m[1]).ravel()], 1)
    d, _ = cKDTree(nodes_m).query(pts, k=1)
    r = np.clip(d.reshape(shape) / radius_m, 0.0, 1.0)
    return (1.0 - r * r) ** 3


def thermal(h, cell_m, talus_deg, rate, iters):
    """MountainGenerator.Erode'un birebir cevrimi."""
    md = np.tan(np.radians(talus_deg)) * cell_m
    for _ in range(iters):
        d = np.zeros_like(h)
        c = h[1:-1, 1:-1]
        nb = [h[1:-1, :-2], h[1:-1, 2:], h[:-2, 1:-1], h[2:, 1:-1]]
        ex = [np.maximum(c - n - md, 0.0) for n in nb]
        tot = ex[0] + ex[1] + ex[2] + ex[3]
        low = np.minimum(np.minimum(nb[0], nb[1]), np.minimum(nb[2], nb[3]))
        moved = np.where(tot > 0, np.minimum(tot, (c - low) * 0.5) * rate, 0.0)
        share = np.divide(moved, np.where(tot > 0, tot, 1.0))
        d[1:-1, 1:-1] -= moved
        d[1:-1, :-2] += ex[0] * share
        d[1:-1, 2:] += ex[1] * share
        d[:-2, 1:-1] += ex[2] * share
        d[2:, 1:-1] += ex[3] * share
        h = h + d
    return h


def constrained_erosion(h, cell_m, uplift, talus_deg=46.1, rate=0.1, iters=8):
    """§5.8: erozyon + telafi edici uplift, tek olcek.

    ISARET SPEC'TEN FARKLI. Spec soyle yaziyor:
        H_{i+1} = E_i + U * dH_i,  dH_i = E_i - H_i
    Erozyon alcalttigina gore E_i < H_i, yani dH < 0 ve `E_i + U*dH` daha da
    alcaltir -- zirvede (U=1) sonuc `2E - H`, yani erozyonun IKI KATI. Bu
    "telafi edici uplift" olamaz.

    Fiziksel anlami olan okuma: zirveye yakin yerde erozyonu GERI AL.
        H_{i+1} = (1-U) * E_i + U * H_i
    U=1 -> orijinal korunur (zirve asinmaz), U=0 -> tam erozyon.
    Spec zaten §9.2'de makale-kod celiskileri icin ayri bir bolum tutuyor;
    bu da oraya ait.
    """
    e = thermal(h, cell_m, talus_deg, rate, iters)
    return (1.0 - uplift) * e + uplift * h


def multiscale_erosion(h, cell_m, uplift, scales_m=(100.0, 50.0, 30.0),
                       talus_deg=46.1, rate=0.10, iters=12):
    """§5.8 -- erozyon UC AYRI IZGARA COZUNURLUGUNDE.

    OKUMA DUZELTMESI. Spec'in "100 m, 50 m, 30 m" olcekleri IZGARA
    COZUNURLUGUDUR, ince izgarada iterasyon sayisi degil. Once oyle
    okunmustu ve sonuc olculdu: 46.1 derece talus x 4.28 m hucre = 4.45 m'lik
    maksimum adim, yani 4.45 m'den dik olan HER SEY siliniyor. Eklenen ince
    detayin tamami o esigin ustunde oldugu icin erozyon onu bastan yiyordu.

    Dogrusu: kaba izgaraya indir, orada asindir, FARKI geri buyut. 30 m'nin
    altindaki detaya hic dokunulmuyor.
    """
    out = h
    for s in scales_m:
        step = max(1, int(round(s / cell_m)))
        if step < 2:
            continue
        ny = (out.shape[0] // step) * step
        nx = (out.shape[1] // step) * step
        coarse = out[:ny, :nx].reshape(ny // step, step, nx // step, step).mean(axis=(1, 3))
        eroded = thermal(coarse, s, talus_deg, rate, iters)
        # GERI BUYUTME PURUZSUZ OLMALI. Once `np.repeat` ile blok blok
        # buyutuluyordu ve sonuc dikdortgen izgara artefakti oldu -- turevi
        # kirilan her ifade Mach bandi uretir (SYMPTOMS.md 'sert kirpma').
        # Kubik spline ile buyutuluyor.
        from scipy.ndimage import zoom
        delta = zoom(eroded - coarse, step, order=3, mode='nearest')
        full = np.zeros_like(out)
        h_, w_ = min(ny, delta.shape[0]), min(nx, delta.shape[1])
        full[:h_, :w_] = delta[:h_, :w_]
        # telafi: zirve ve boyun cevresinde erozyon geri alinir
        out = out + full * (1.0 - uplift)
    return out
