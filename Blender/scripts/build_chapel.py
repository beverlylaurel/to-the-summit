# Dag Sapeli -- yol kenari kucuk ibadet yapisi.
#
# Neden bu bicim: sapel gecen yolcunun UZAKTAN gorecegi bir isarettir. Bu
# yuzden taban alani kucuk tutulup yukseklik ve dikey vurgu artirilir: cok dik
# cati, ustunde can kulesi. Duvarlar tas orgu uzerine kirec sivadir; beyaz
# yuzey karli ormanda bile ayirt edilir.
#
# Silueti dar ve dikey, tepesinde ince bir cikinti. Terk edilmis evle karistigi
# nokta yok: o genis, koyu ve cokmus; bu dar, acik renkli ve saglam.

import math
import random
import bpy
from mathutils import Matrix

import outpost_kit as K

# ---------------------------------------------------------------- olculer
HX = 1.30                  # duvar ekseninden yari genislik
DY = 3.90                  # derinlik
WALL = 0.24
PLINTH = 0.34
Z_EAVE = 2.70
Z_APEX = 4.30
OVER_X, OVER_Y = 0.30, 0.24
TH_SHINGLE = 0.020
SLOPE = (Z_APEX - Z_EAVE) / HX
_L = math.hypot(HX + OVER_X, SLOPE * (HX + OVER_X))
NX, NZ = SLOPE / math.hypot(SLOPE, 1.0), 1.0 / math.hypot(SLOPE, 1.0)
C = HX * NX + Z_EAVE * NZ
UX, UZ = -NZ, NX

DOOR_W, DOOR_H = 0.84, 1.96      # kemer basina kadar dik kisim
WIN_W, WIN_H = 0.46, 0.72
BELL_Z0, BELL_Z1 = Z_APEX - 0.12, Z_APEX + 1.22


def surf_dist(v):
    return abs(v.x) * NX + v.z * NZ - C - TH_SHINGLE


def at(side, t, d):
    x = side * (HX + OVER_X + UX * t) - side * NX * d
    z = Z_EAVE - SLOPE * OVER_X + UZ * t - NZ * d
    return (x, z)


def _arch(w, h, n=9, y=0.0):
    """Ust ucu kemerli dusey acikligin XZ profili (kapi, pencere)."""
    pts = [(-w * 0.5, 0.0), (w * 0.5, 0.0)]
    for i in range(n + 1):
        a = math.pi * i / n
        pts.append((w * 0.5 * math.cos(a), h + w * 0.5 * math.sin(a)))
    return pts


def build(col):
    for o in list(col.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    random.seed(7)

    # --- tas kaide
    K.box("Chp_Plinth", (0, DY * 0.5, PLINTH * 0.5),
          (2 * (HX + 0.11), DY + 0.22, PLINTH), col)

    # --- duvarlar. On duvar kemerli kapi acikligini tasir: profil alt kenardan
    # bir centik oldugu icin tek kapali poligon yeter, delik gerekmez.
    prof = [(-HX, PLINTH), (-DOOR_W * 0.5, PLINTH)]
    for i in range(9, -1, -1):
        a = math.pi * i / 9
        prof.append((DOOR_W * 0.5 * math.cos(a),
                     PLINTH + DOOR_H + DOOR_W * 0.5 * math.sin(a)))
    prof += [(DOOR_W * 0.5, PLINTH), (HX, PLINTH),
             (HX, Z_EAVE), (0.0, Z_APEX), (-HX, Z_EAVE)]
    K.prism("Chp_WallFront", prof, 0.0, WALL, col)
    back = [(-HX, PLINTH), (HX, PLINTH), (HX, Z_EAVE), (0.0, Z_APEX), (-HX, Z_EAVE)]
    K.prism("Chp_WallBack", back, DY - WALL, DY, col)

    # --- yan duvarlar: kemerli pencere acikligi
    for s in (1, -1):
        wy = DY * 0.52
        seg = []
        seg.append(K.box("sw", (s * (HX - WALL * 0.5), (WALL + wy - WIN_W * 0.5) * 0.5,
                               (PLINTH + Z_EAVE) * 0.5),
                         (WALL, wy - WIN_W * 0.5 - WALL, Z_EAVE - PLINTH), col))
        y1 = wy + WIN_W * 0.5
        seg.append(K.box("sw", (s * (HX - WALL * 0.5), (y1 + DY - WALL) * 0.5,
                               (PLINTH + Z_EAVE) * 0.5),
                         (WALL, DY - WALL - y1, Z_EAVE - PLINTH), col))
        zw = PLINTH + 0.95
        seg.append(K.box("sw", (s * (HX - WALL * 0.5), wy, (PLINTH + zw) * 0.5),
                         (WALL, WIN_W, zw - PLINTH), col))
        zt = zw + WIN_H + WIN_W * 0.5
        seg.append(K.box("sw", (s * (HX - WALL * 0.5), wy, (zt + Z_EAVE) * 0.5),
                         (WALL, WIN_W, Z_EAVE - zt), col))
        K.join("Chp_WallSide_%s" % ("R" if s > 0 else "L"), seg, col)

    # --- padavra cati: kalin dip asagi, ince uc yukari (Avci Siginagi ile ayni
    # kesit kurali; ters cevrilirse su lamanin altina girer)
    t0, te = 0.0, _L
    rows = 7
    e = (te - t0) / rows
    lap = 0.10
    ny, y0, yw = 15, -OVER_Y, (DY + 2 * OVER_Y) / 15.0
    parts = []
    for s in (1, -1):
        for i in range(rows):
            ta = t0 + i * e
            tb = min(ta + e + lap, te)
            for j in range(ny):
                jt = random.uniform(-0.012, 0.012)
                pts = [at(s, ta + jt, -TH_SHINGLE), at(s, tb, 0.0),
                       at(s, tb, TH_SHINGLE), at(s, ta + jt, TH_SHINGLE)]
                parts.append(K.prism("sh", pts, y0 + j * yw + 0.003,
                                     y0 + (j + 1) * yw - 0.003, col))
        pts = [at(s, te - 0.26, -TH_SHINGLE), at(s, te + 0.04, -TH_SHINGLE),
               at(s, te + 0.04, 0.0), at(s, te - 0.26, TH_SHINGLE)]
        parts.append(K.prism("cap", pts, y0, y0 + ny * yw, col))
    K.join("Chp_Shingles", parts, col)
    sheath = []
    for s in (1, -1):
        pts = [at(s, t0, TH_SHINGLE), at(s, te, TH_SHINGLE),
               at(s, te, TH_SHINGLE + 0.022), at(s, t0, TH_SHINGLE + 0.022)]
        sheath.append(K.prism("sk", pts, y0 + 0.004, y0 + ny * yw - 0.004, col))
    K.join("Chp_Sheathing", sheath, col)

    # --- can kulesi: on kalkanin uzerinde, iki dikme + kemer + kucuk kulah
    for s in (1, -1):
        K.box("Chp_BellPost_%s" % ("R" if s > 0 else "L"),
              (s * 0.30, WALL * 0.5, (BELL_Z0 + BELL_Z1) * 0.5),
              (0.11, 0.16, BELL_Z1 - BELL_Z0), col)
    # Baslik dikmelerin ICINDE kalir: ust yuzunu ayni kota koymak z-fighting
    # yapiyordu.
    K.box("Chp_BellHead", (0, WALL * 0.5, BELL_Z1 - 0.085), (0.82, 0.18, 0.11), col)
    K.prism("Chp_BellCap", [(-0.50, BELL_Z1), (0.50, BELL_Z1), (0.0, BELL_Z1 + 0.46)],
            WALL * 0.5 - 0.13, WALL * 0.5 + 0.13, col)
    prof_bell = [(0.005, 0.0), (0.175, 0.05), (0.155, 0.24), (0.075, 0.40),
                 (0.055, 0.44), (0.005, 0.44)]
    bell = K.revolve("Chp_BronzeBell", prof_bell, 14, col)
    bell.location = (0.0, WALL * 0.5, BELL_Z1 - 0.55)
    K.log("Chp_BellYoke", (-0.16, WALL * 0.5, BELL_Z1 - 0.155),
          (0.16, WALL * 0.5, BELL_Z1 - 0.155), 0.026, 8, col)

    # --- kapi kanadi: mentese ekseninde doner
    leaf = K.prism("Chp_DoorLeaf", _arch(DOOR_W - 0.05, DOOR_H - 0.03),
                   WALL - 0.085, WALL - 0.035, col)
    leaf.data.transform(Matrix.Translation(((DOOR_W - 0.05) * 0.5, 0, PLINTH + 0.02)))
    leaf.location = (-(DOOR_W - 0.05) * 0.5, 0.0, 0.0)
    for i, z in enumerate((0.36, DOOR_H - 0.30)):
        h = K.box("Chp_Strap_%d" % i, (0, 0, 0), (DOOR_W - 0.14, 0.016, 0.075), col)
        h.location = ((DOOR_W - 0.05) * 0.5 - (DOOR_W - 0.05) * 0.5,
                      WALL - 0.095, PLINTH + 0.02 + z)
        bpy.context.view_layer.update()
        h.parent = leaf
        h.matrix_parent_inverse = leaf.matrix_world.inverted()

    # Padavra dibi kabugun disina TH kadar tastigi ve butu duzensiz kesildigi
    # icin dis kabuk sacak hattindan biraz genis tanimlanir.
    ext = HX + OVER_X + 0.028
    return dict(envelope=(-ext, ext, -OVER_Y - 0.005, DY + OVER_Y + 0.005))


# ---------------------------------------------------------------- malzeme

STONE_PARTS = ("Chp_Plinth",)
WOOD_PARTS = ("Chp_Shingles", "Chp_Sheathing", "Chp_BellPost", "Chp_BellHead",
              "Chp_BellCap", "Chp_DoorLeaf")
IRON_PARTS = ("Chp_BronzeBell", "Chp_Strap", "Chp_BellYoke")
LONG_AXIS = [("Chp_Plinth", "X"), ("Chp_WallFront", "X"), ("Chp_WallBack", "X"),
             ("Chp_WallSide", "Y"), ("Chp_Shingles", "Z"), ("Chp_Sheathing", "Z"),
             ("Chp_BellPost", "Z"), ("Chp_BellHead", "X"), ("Chp_BellCap", "X"),
             ("Chp_BronzeBell", "Z"), ("Chp_BellYoke", "X"), ("Chp_DoorLeaf", "Z"),
             ("Chp_Strap", "X")]


def _mat(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    return m


def materials(col):
    """Kirec siva / padavra ve kereste / tas kaide / dokme bronz.

    Siva karda bile beyaz gorunmeli: yipranma yalniz DIPTE, kar sicramasinin
    ulastigi yerde birakiliyor. Butun yuzeye yayilirsa yapinin isaret olma
    islevi kaybolur -- sapel gorunmek icin vardir.
    """
    plaster, wood, stone, metal = (_mat("Chp_Plaster"), _mat("Chp_Wood"),
                                   _mat("Chp_Stone"), _mat("Chp_Metal"))
    K.build_stone(plaster, "plastered_stone_wall", tiling=1.15, grain="U",
                  sat=0.22, val=2.05, var=(0.09, 1.2),
                  wet=(0.95, 0.34), wet_color=(0.150, 0.142, 0.126), wet_amt=0.52,
                  lichen=(4.10, 2.60), lichen_color=(0.330, 0.336, 0.300),
                  lichen_amt=0.20)
    K.build_wood(wood, "old_planks_02", tiling=1.55, grain="V", sat=0.60, val=0.70,
                 damp=(0.55, 1.30), damp_color=(0.130, 0.112, 0.092), damp_amt=0.54,
                 sun=(4.60, 2.40), sun_color=(0.330, 0.328, 0.320), sun_amt=0.48,
                 var=(0.17, 3.2))
    K.build_stone(stone, "rock_wall_05", tiling=1.50, grain="U",
                  sat=0.88, val=0.82, var=(0.12, 0.9),
                  wet=(0.34, 0.02), wet_color=(0.120, 0.114, 0.104), wet_amt=0.58,
                  lichen=(0.44, 0.14), lichen_color=(0.265, 0.280, 0.220),
                  lichen_amt=0.34)
    K.build_metal(metal, "rusty_metal_02", tiling=3.20, grain="U", metallic=0.50,
                  sat=0.62, val=0.82, base_tint=(0.46, 0.41, 0.32),
                  rust=(4.90, 5.60), rust_color=(0.30, 0.190, 0.090), rust_amt=0.40)

    grain = {plaster.name: "U", wood.name: "V", stone.name: "U", metal.name: "U"}
    for o in col.objects:
        n = o.name
        m = (metal if n.startswith(IRON_PARTS) else
             stone if n.startswith(STONE_PARTS) else
             wood if n.startswith(WOOD_PARTS) else plaster)
        o.data.materials.clear()
        o.data.materials.append(m)
        K.cube_uv(o, 2.0)
        K.align_grain(o, next((a for p, a in LONG_AXIS if n.startswith(p)), "Z"),
                      grain[m.name])
    return plaster, wood, stone, metal
