# Tas Mahzen -- toprak ortulu erzak kileri.
#
# Arastirmadan gelen zorunluluklar:
#   - Ortu KEMERLI olur, duz degil. Duz tavan uzerindeki toprak yukunu tasimaz
#     ve yogusma damlasi ortadan duser; kemerde nem kenarlara akar.
#   - Ust ortu cim/toprak; tas yalniz giris cephesinde ve istinat duvarlarinda
#     gorunur. Yapinin tamami tas olsaydi kule gibi okurdu, mahzen gibi degil.
#   - Girisin iki yaninda istinat duvari olur, yoksa toprak kapinin onune akar.
#
# Silueti tumsek: digger dort yapinin hicbiri yatay ve yuvarlak degil.

import math
import bpy
from mathutils import Matrix

import outpost_kit as K

# ---------------------------------------------------------------- olculer
MX = 2.50          # tumsek yari genisligi
MY0, MY1 = 0.38, 4.20
HM = 2.62          # tumsek tepe yuksekligi
FACE_Y = 0.40      # cephe kalinligi
FX = 2.20          # cephe yari genisligi
OPEN_X, SPRING = 0.55, 1.35     # kemer acikligi ve kemer basi
VOUS = 0.22        # kemer bilezigi derinligi


def mound(x, y):
    """Toprak tumsegin yuksekligi. Onde tam, arkada kubbelenerek kapanir.

    Duz kubbe plastik gibi okunuyordu; toprak oturur, cokur ve yer yer sisar.
    Genligi 8 cm'de tutuluyor: kemerin kendi egrisinden kucuk kalmali, yoksa
    tumsek dalgali bir orte donusur.
    """
    cx = max(0.0, 1.0 - (x / MX) ** 2) ** 0.62
    if y <= 3.10:
        cy = 1.0
    else:
        t = min(1.0, (y - 3.10) / 1.10)
        cy = math.sqrt(max(0.0, 1.0 - t * t))
    lump = (0.110 * math.sin(1.9 * x + 0.7) * math.sin(1.35 * y - 0.4)
            + 0.055 * math.sin(3.7 * x - 1.1) * math.sin(2.9 * y + 1.6)
            + 0.028 * math.sin(6.1 * x + 2.3) * math.sin(5.3 * y - 0.9))
    return HM * cx * cy + lump * cx * cy


def face_top(x):
    """Cephe duvarinin ust kenari: tumsegin on kesitinin 45 mm altindan gecer.

    Bagimsiz bir yay denendi ve cephe tumsekten dar kaldi; yanlardan toprak
    kesitinin dusey eteği acikta kalip kara bir delik gibi okundu. Cepheyi
    tumsegin kendi egrisine baglamak bunu kokten kapatir.
    """
    return mound(x, MY0) - 0.045


def build(col):
    for o in list(col.objects):
        bpy.data.objects.remove(o, do_unlink=True)

    # --- giris cephesi. Kapi acikligi alt kenardan bir CENTIK oldugu icin
    # profil delikli degil, tek kapali poligon: K.prism yeterli.
    n = 12
    prof = [(-FX, 0.0), (-OPEN_X, 0.0), (-OPEN_X, SPRING)]
    for i in range(n + 1):                       # kemer icyuzu
        a = math.pi - math.pi * i / n
        prof.append((OPEN_X * math.cos(a), SPRING + OPEN_X * math.sin(a)))
    prof += [(OPEN_X, 0.0), (FX, 0.0)]
    for i in range(n * 2 + 1):                   # cephe tepesi
        x = FX - 2 * FX * i / (n * 2)
        prof.append((x, face_top(x)))
    K.prism("Cell_Facade", prof, 0.0, FACE_Y, col)

    # --- kemer bilezigi: cephenin 6 cm onunde, kemeri okunur kilar
    ring = []
    for i in range(n + 1):
        a = math.pi - math.pi * i / n
        ring.append(((OPEN_X + VOUS) * math.cos(a), SPRING + (OPEN_X + VOUS) * math.sin(a)))
    for i in range(n + 1):
        a = math.pi * i / n
        ring.append((OPEN_X * math.cos(a), SPRING + OPEN_X * math.sin(a)))
    K.prism("Cell_Archivolt", ring, -0.065, 0.004, col)
    # Kemer ayagi acikliga 3 cm TASAR. Ic yuzunu tam soveyle ayni duzleme
    # koymak z-fighting yapiyordu; tasmak hem onu cozer hem de ayak tasinin
    # gercek bicimidir.
    for s in (1, -1):
        K.box("Cell_Impost_%s" % ("R" if s > 0 else "L"),
              (s * (OPEN_X - 0.03 + (VOUS + 0.03) * 0.5), -0.031, SPRING * 0.5),
              (VOUS + 0.03, 0.069, SPRING), col)

    # --- toprak ortu
    K.heightfield("Cell_Mound", mound, -MX, MX, MY0, MY1, 22, 20, col)

    # --- havalandirma bacasi: kare orulmus tas govde. Silindir olarak
    # denendi ve ahsap direk gibi okundu -- yuvarlak kesit tas orguyu inkar
    # ediyor, kare kesit ve tacli kapak masonlugu tek bakista soyluyor.
    vx, vy = 0.90, 3.00
    vz = mound(vx, vy)
    K.box("Cell_Vent", (vx, vy, vz + 0.09), (0.31, 0.31, 0.94), col)
    K.box("Cell_VentCap", (vx, vy, vz + 0.585), (0.44, 0.44, 0.07), col)

    # --- istinat duvarlari: girisin iki yaninda, one dogru alcalan surekli
    # duvar. Ayri bloklar merdiven gibi okunuyordu.
    for s in (1, -1):
        prof = [(0.42, 0.0), (-1.05, 0.0), (-1.05, 0.55), (-0.62, 0.82),
                (-0.15, 1.12), (0.42, 1.42)]
        K.prism("Cell_Wing%s" % ("R" if s > 0 else "L"), prof,
                s * 1.72, s * 2.06, col, axis="X")

    # --- esik tasi
    K.box("Cell_Threshold", (0, -0.16, 0.045), (1.78, 0.44, 0.09), col)

    # --- kapi: kemerli agir kereste kanat, mentese ekseninde doner
    dw, dh = 0.50, SPRING
    dp = [(-dw, 0.02), (dw, 0.02)]
    for i in range(n + 1):
        a = math.pi * i / n
        dp.append((dw * math.cos(a), dh + dw * math.sin(a)))
    leaf = K.prism("Cell_DoorLeaf", dp, 0.30, 0.375, col)
    leaf.data.transform(Matrix.Translation((dw, 0, 0)))
    leaf.location = (-dw, 0, 0)
    for i, zc in enumerate((0.42, 1.18)):        # demir menteşe kolu
        st = K.box("Cell_DoorStrap%d" % i, (0, 0, 0), (0.86, 0.016, 0.085), col)
        st.data.transform(Matrix.Translation((0.43 - dw, 0, 0)))
        st.location = (0, 0.293, zc)
        st.parent = leaf
        st.matrix_parent_inverse = leaf.matrix_world.inverted()
    K.log("Cell_DoorRing", (0.62, 0.245, 0.95), (0.62, 0.293, 0.95), 0.030, 8, col)
    r = col.objects["Cell_DoorRing"]
    r.parent = leaf
    r.matrix_parent_inverse = leaf.matrix_world.inverted()

    return dict(envelope=(-MX - 0.005, MX + 0.005, -1.465, MY1 + 0.005))


# ---------------------------------------------------------------- malzeme

STONE_PARTS = ("Cell_Facade", "Cell_Archivolt", "Cell_Impost", "Cell_Wing",
               "Cell_Threshold", "Cell_Vent")
IRON_PARTS = ("Cell_DoorStrap", "Cell_DoorRing")
LONG_AXIS = [("Cell_Facade", "X"), ("Cell_Archivolt", "X"), ("Cell_Impost", "Z"),
             ("Cell_Mound", "Y"), ("Cell_Vent", "Z"), ("Cell_Wing", "Y"),
             ("Cell_Threshold", "X"), ("Cell_DoorLeaf", "Z"),
             ("Cell_DoorStrap", "X"), ("Cell_DoorRing", "Y")]


def _mat(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    return m


def materials(col):
    """Tas / cim / kereste / demir.

    Tas ahsaptan farkli yaslanir: gunes agartmasi yerine dipte kalici nem ve
    ust yuzeylerde liken tutar. Bu yuzden build_stone ayri bir zincir kurar.
    """
    stone, turf, timber, iron = (_mat("Cell_Stone"), _mat("Cell_Turf"),
                                 _mat("Cell_Timber"), _mat("Cell_Iron"))
    K.build_stone(stone, "stacked_stone_wall", tiling=1.15, grain="U",
                  wet=(0.75, 0.06), wet_color=(0.155, 0.150, 0.140), wet_amt=0.58,
                  lichen=(2.45, 1.10), lichen_color=(0.335, 0.350, 0.280),
                  lichen_amt=0.34)
    # Cim, kar hattinda parlak yesil olmaz: kisin altindan cikmis olu ot ve
    # aradan gorunen tas. Doyum yariya indirilip deger dusuruluyor.
    K.build_stone(turf, "aerial_grass_rock", tiling=1.55, grain="U",
                  sat=0.62, val=0.80, var=(0.16, 0.55),
                  wet=(0.55, 0.02), wet_color=(0.140, 0.132, 0.108), wet_amt=0.50,
                  lichen=(2.62, 1.30), lichen_color=(0.270, 0.278, 0.220),
                  lichen_amt=0.32)
    K.build_wood(timber, "rough_pine_door", tiling=0.62, grain="V", sat=0.74, val=0.86,
                 damp=(0.55, 1.30), damp_color=(0.140, 0.118, 0.098), damp_amt=0.62,
                 sun=(2.60, 1.20), sun_color=(0.330, 0.325, 0.315), sun_amt=0.26,
                 var=(0.12, 2.6))
    K.build_metal(iron, "rusty_metal_02", tiling=3.4, grain="U", metallic=0.20,
                  base_tint=(0.34, 0.32, 0.30),
                  rust=(1.40, 0.05), rust_color=(0.34, 0.155, 0.070), rust_amt=0.66)

    grain = {stone.name: "U", turf.name: "U", timber.name: "V", iron.name: "U"}
    for o in col.objects:
        n = o.name
        m = (iron if n.startswith(IRON_PARTS) else
             turf if n.startswith("Cell_Mound") else
             stone if n.startswith(STONE_PARTS) else timber)
        o.data.materials.clear()
        o.data.materials.append(m)
        K.cube_uv(o, 2.0)
        K.align_grain(o, next((a for p, a in LONG_AXIS if n.startswith(p)), "Z"),
                      grain[m.name])
    return stone, turf, timber, iron
