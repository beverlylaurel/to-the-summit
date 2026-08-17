"""540 km'lik bolgenin KARAKTER MASKESI.

Ciktisi iki alan:
  probMap  -- nerede zirve cikabilir (0 = hic)
  elevMap  -- kaba yukseklik kilavuzu, METRE

YAPI EKSENDEN TUREE, MERKEZDEN DEGIL. Ilk deneme karakteri azimuta baglamisti
ve sonuc pasta dilimi cikti -- kacmaya calistigimiz radyal yapayligin ta
kendisi. Gercek silsile CIZGISELDIR: Himalaya dogu-bati uzanir, kuzeyinde
Tibet platosu, guneyinde Nepal ovalari. Yani baskin yapi bir EKSEN.

Burada:
  eksen        -- egri bir cizgi (birkac sinusun toplami, paralel degil)
  d            -- eksene ISARETLI uzaklik; + kuzey, - guney
  daglik       -- |d| kucukken
  plato        -- d buyuk pozitif (kuzey)
  ova          -- d buyuk negatif (guney)
  etek         -- guneyde gecis kusagi
  ana kutle    -- eksen uzerinde, merkezde

Referanstan bilincli sapma: yazarlarin akisinda `probMap <= 0` olan yer
`saddleElev.min()` sabitine esitleniyor (Synthesis.ipynb hucre 16), yani
tanim geregi DUMDUZ cikiyor. Olculdu ve reddedildi -- kullanicinin tarifi
"tepecikli duz ova". Bu yuzden ovada probMap SIFIR DEGIL, DUSUK; alcak
prominence'li tepecikler kendiliginden dogar.
"""
import numpy as np

REGION_KM = 540.0
RES       = 1024
PLAY_KM   = 17.517
SUMMIT_M  = 5709.0
PLAIN_M   = 186.0
PLATEAU_M = 3600.0


def _smooth(x, a, b):
    """a'da 0, b'de 1; turevi sinirlarda sifir (Mach bandi yok)."""
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def _fbm(shape, cell_km, wavelength_km, octaves, seed, gain=0.5, lacunarity=2.1):
    """Deger gurultusu, oktavlarin TOPLAMI. Carpim degil -- carpim ayrisabilir
    ve duzenli kafes uretir (SYMPTOMS.md 'Duzenli kafes deseni')."""
    rng = np.random.default_rng(seed)
    out = np.zeros(shape, dtype=float)
    amp, wl, norm = 1.0, float(wavelength_km), 0.0
    yy, xx = np.mgrid[0:shape[0], 0:shape[1]]
    for _ in range(octaves):
        n = max(3, int(round(shape[0] * cell_km / wl)))
        g = rng.random((n + 2, n + 2))
        fy = yy / shape[0] * n
        fx = xx / shape[1] * n
        y0, x0 = fy.astype(int), fx.astype(int)
        ty, tx = fy - y0, fx - x0
        sy = ty * ty * (3 - 2 * ty)
        sx = tx * tx * (3 - 2 * tx)
        out += ((g[y0, x0] * (1 - sx) * (1 - sy) + g[y0, x0 + 1] * sx * (1 - sy) +
                 g[y0 + 1, x0] * (1 - sx) * sy + g[y0 + 1, x0 + 1] * sx * sy) - 0.5) * 2 * amp
        norm += amp
        amp *= gain
        wl /= lacunarity
    return out / norm


def build(res=RES, region_km=REGION_KM, seed=36044):
    """Diziler [X, Y] indeksli dondurulur -- ilk indeks DOGU, ikincisi KUZEY.

    Bu referansin konvansiyonu, varsayim degil: `divtree_synthesis.py:24`
    `probMap[normCoords[:,0]*shape[0], normCoords[:,1]*shape[1]]` yaziyor ve
    `normCoords[:,0]` X'tir. Ilk denemede diziler [kuzey, dogu] kurulmustu ve
    silsile 90 derece donuk cikti -- dogu-bati zincir kuzey-guney serit oldu.
    Ciziimde `.T` ile geri cevrilir (kuzey yukari).
    """
    cell = region_km / res
    x, y = np.mgrid[0:res, 0:res]
    cx = cy = res / 2.0
    ex = (x - cx) * cell          # km, dogu +
    ny = (y - cy) * cell          # km, kuzey +

    # --- EKSEN: egri bir zincir, dogu-bati. Paralel olmayan uc bilesen
    #     toplaniyor; genlikler dalga boyundan kucuk (dalga boyu kurali).
    axis = (28.0 * np.sin(np.radians(ex * 0.42) + 0.7) +
            14.0 * np.sin(np.radians(ex * 1.10) + 2.3) +
             7.0 * np.sin(np.radians(ex * 2.30) + 4.1))
    d = ny - axis                                     # eksene isaretli uzaklik
    d = d + 26.0 * _fbm((res, res), cell, 170.0, 4, seed + 1)   # sinirlar duz olmasin

    ad = np.abs(d)
    r = np.hypot(ex, ny)

    # EKSEN BOYUNCA DEGISIM. Bantlar birbirine paralel cikmasin: zincirin
    # yari genisligi ve yogunlugu x boyunca degisiyor. Gercek silsilede de
    # oyle -- bazi yerde genis kutle, bazi yerde dar bogaz.
    along = _fbm((res, res), cell, 210.0, 3, seed + 5)
    along = along[:, res // 2:res // 2 + 1]          # yalniz X'e bagli
    along = np.repeat(along, res, axis=1)
    width = 75.0 + 34.0 * along                       # km, zincirin yari genisligi

    # MAHMUZLAR: ana eksenden guneye uzanan ikincil sirtlar
    spur = _fbm((res, res), cell, 95.0, 3, seed + 6)
    spur = spur[:, res // 2:res // 2 + 1]
    spur = np.repeat(spur, res, axis=1)
    spur_reach = 60.0 * np.clip(spur, 0.0, 1.0) ** 2   # km, guneye uzanma

    # --- KARAKTERLER (hepsi d ve r'den; azimut YOK) ----------------------
    ch = {}
    ch['massif']   = (1.0 - _smooth(r, 9.0, 26.0)) * (1.0 - _smooth(ad, 10.0, 30.0))
    ch['range']    = ((1.0 - _smooth(ad - (width - 75.0), 45.0, 105.0)) * _smooth(r, 6.0, 20.0))
    # mahmuzlar silsileyi guneye tasiyor
    ch['range'] = np.maximum(ch['range'],
                             (1.0 - _smooth(-d - spur_reach, 0.0, 38.0)) * _smooth(-d, 8.0, 30.0)
                             * (1.0 - _smooth(ad, 150.0, 220.0)))
    ch['foothill'] = (_smooth(-d, 55.0, 90.0) * (1.0 - _smooth(-d, 120.0, 175.0)))
    ch['plain']    = _smooth(-d, 130.0, 200.0)
    ch['plateau']  = _smooth(d, 70.0, 140.0)
    # yaklasma vadisi: ana kutleden GUNEYE inen tek bir oluk
    valley_x = 9.0 * np.sin(np.radians(ny * 1.6) + 1.1)
    ch['approach'] = ((1.0 - _smooth(np.abs(ex - valley_x), 4.0, 13.0))
                      * _smooth(-d, 2.0, 14.0) * (1.0 - _smooth(-d, 60.0, 95.0)))

    stack = np.stack([ch[k] for k in ch], axis=0)
    w = stack ** 3
    w /= np.maximum(w.sum(axis=0), 1e-6)
    weights = {k: w[i] for i, k in enumerate(ch)}

    # --- ZIRVE YOGUNLUGU. Ova SIFIR DEGIL: tepecikler icin.
    # YUKSEKLIGI ZIRVELER TASIR. Olculdu: plato 3600 m'de tasarlandi, yogunluk
    # 0.06 verilince arazide ortanca 103 m cikti. L1 yuksekligi divide tree'den
    # kuruyor, elevMap'ten DEGIL -- elevMap yalniz zirvenin NEREYE konacagini
    # soyluyor. Zirvesiz bolgede yukseklik bilgisi yok, arazi tabana cokuyor.
    #
    # Gercek plato zaten boyle: Tibet alcak tepelerle kapli YUKSEK bir duzluk,
    # bos bir levha degil. Ova da ayni sebeple yogunluk istiyor.
    dens = {'massif': 1.00, 'range': 0.85, 'foothill': 0.45,
            'approach': 0.30, 'plateau': 0.60, 'plain': 0.38}
    probMap = sum(weights[k] * dens[k] for k in ch)
    # YOGUNLUK DUZGUN DEGIL. Gercekte de degil: bazi vadi bosluklu, bazi
    # kutle sik. Genlik dusuk tutuldu, karakter siniri bozulmasin.
    probMap *= 1.0 + 0.45 * _fbm((res, res), cell, 120.0, 4, seed + 7)
    probMap = np.clip(probMap, 0.04, 1.0)

    # --- KABA YUKSEKLIK
    elev = {'massif': SUMMIT_M * 0.80, 'range': SUMMIT_M * 0.60, 'foothill': 1500.0,
            'approach': 1000.0, 'plateau': PLATEAU_M, 'plain': PLAIN_M}
    elevMap = sum(weights[k] * elev[k] for k in ch)
    elevMap += (SUMMIT_M - elevMap) * (1.0 - _smooth(r, 2.0, 13.0))
    elevMap += 210.0 * _fbm((res, res), cell, 95.0, 5, seed + 2) * _smooth(elevMap, 300.0, 1500.0)
    elevMap = np.clip(elevMap, PLAIN_M, SUMMIT_M)

    return probMap, elevMap, weights, dict(cell=cell, d=d, r=r, axis=axis)
