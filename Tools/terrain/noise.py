"""`noise` paketinin yedegi.

Windows'ta C uzantisi derlenmiyor. Referans kodunda `snoise2` yalnizca
`utils/noise.py` icinden, elevMap'e %5 agirlikla eklenen bir sarsinti icin
cagriliyor (Synthesis.ipynb hucre 16: weightNoise = 0.05). Yani gurultunun
TURU sonucu belirlemiyor, yalnizca duz bolgelerde beraberligi bozuyor.

Klasik Perlin gradyan gurultusu + fBm. Determinist; tohumsuz rastgelelik yok.
"""
import math

_P = list(range(256))
_rs = 1234567
for _i in range(255, 0, -1):
    _rs = (1103515245 * _rs + 12345) & 0x7FFFFFFF
    _j = _rs % (_i + 1)
    _P[_i], _P[_j] = _P[_j], _P[_i]
_P = _P * 2


def _fade(t):
    return t * t * t * (t * (t * 6 - 15) + 10)


def _grad(h, x, y):
    h &= 7
    u = x if h < 4 else y
    v = y if h < 4 else x
    return (u if (h & 1) == 0 else -u) + (2.0 * v if (h & 2) == 0 else -2.0 * v)


def _perlin2(x, y):
    xi = math.floor(x)
    yi = math.floor(y)
    xf = x - xi
    yf = y - yi
    xi &= 255
    yi &= 255
    u = _fade(xf)
    v = _fade(yf)
    aa = _P[_P[xi] + yi]
    ab = _P[_P[xi] + yi + 1]
    ba = _P[_P[xi + 1] + yi]
    bb = _P[_P[xi + 1] + yi + 1]
    g_aa = _grad(aa, xf, yf)
    g_ba = _grad(ba, xf - 1, yf)
    g_ab = _grad(ab, xf, yf - 1)
    g_bb = _grad(bb, xf - 1, yf - 1)
    x1 = g_aa + u * (g_ba - g_aa)
    x2 = g_ab + u * (g_bb - g_ab)
    return (x1 + v * (x2 - x1)) / 2.0


def snoise2(x, y, octaves=1, persistence=0.5, lacunarity=2.0):
    total = 0.0
    amp = 1.0
    freq = 1.0
    norm = 0.0
    for _ in range(octaves):
        total += _perlin2(x * freq, y * freq) * amp
        norm += amp
        amp *= persistence
        freq *= lacunarity
    return total / norm
