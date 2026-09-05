# Su Degirmeni -- ustten beslemeli carkli kucuk dere degirmeni.
#
# Neden bu bicim: ustten beslemeli cark, suyun DUSME enerjisini kullanir; bu
# yuzden su bir OLUK (savak) ile carkin tepesine tasinir ve oluk sehpalar
# uzerinde gelir. Alt kat tas orgudur -- carkin milini tasiyan yatak ve su
# sicramasi ancak tasa dayanir. Ust kat hafif ahsaptir: un ambari.
#
# Silueti kutle + BUYUK DAIRE + egik oluk cizgisi. Onceki dokuz yapinin
# hicbirinde daire yok; en uzaktan bile tek bakista ayrilir.

import math
import bpy
from mathutils import Matrix

import outpost_kit as K

# ---------------------------------------------------------------- olculer
HX, HY = 1.75, 2.15         # bina yari olculeri
Z_STONE = 2.25              # tas katin ustu
Z_WALL = 3.72               # ahsap katin ustu
Z_RIDGE = 5.05
OVER = 0.34
WALL_T = 0.30

WR = 1.62                   # cark yaricapi
WW = 0.94                   # cark genisligi
# Cark ekseni sacak hattinin DISINDA olmali: 0.62 m'de carkin cemberi ve
# savak sehpasi cati tasmasini deliyordu (olculdu: 110 ve 12 yuz cifti).
WX = HX + 0.95              # cark ekseni (X)
WZ = 2.42                   # cark merkezi yuksekligi
BUCKETS = 16

FLUME_W = 0.46
SLOPE = (Z_RIDGE - Z_WALL) / HX
_L = math.hypot(HX + OVER, SLOPE * (HX + OVER))
NX, NZ = SLOPE / math.hypot(SLOPE, 1.0), 1.0 / math.hypot(SLOPE, 1.0)
C = HX * NX + Z_WALL * NZ
UX, UZ = -NZ, NX


def surf_dist(v):
    return abs(v.x) * NX + v.z * NZ - C


def at(side, t, d):
    x = side * (HX + OVER + UX * t) - side * NX * d
    z = Z_WALL - SLOPE * OVER + UZ * t - NZ * d
    return (x, z)


def _rot_box(name, a, r, size, z, col, tilt=0.0):
    """Merkezden a acisi ve r yaricapinda, kendi ekseninde donmus kutu."""
    o = K.box(name, (0, 0, 0), size, col)
    m = Matrix.Rotation(a, 4, "Z")
    if tilt:
        m = m @ Matrix.Rotation(tilt, 4, "Y")
    o.data.transform(m)
    o.location = (r * math.cos(a), r * math.sin(a), z)
    return o


def build(col):
    for o in list(col.objects):
        bpy.data.objects.remove(o, do_unlink=True)

    # --- tas alt kat: dort duvar, on cephede kapi acikligi
    dw, dh = 0.92, 2.02
    st = []
    st.append(K.box("sm", (0, HY - WALL_T * 0.5, Z_STONE * 0.5),
                    (2 * HX, WALL_T, Z_STONE), col))
    for s in (1, -1):
        st.append(K.box("sm", (s * (HX - WALL_T * 0.5), 0, Z_STONE * 0.5),
                        (WALL_T, 2 * (HY - WALL_T), Z_STONE), col))
    side = (2 * HX - dw) * 0.5
    for s in (1, -1):
        st.append(K.box("sm", (s * (HX - side * 0.5), -(HY - WALL_T * 0.5),
                               Z_STONE * 0.5), (side, WALL_T, Z_STONE), col))
    st.append(K.box("sm", (0, -(HY - WALL_T * 0.5), (dh + Z_STONE) * 0.5),
                    (dw, WALL_T, Z_STONE - dh), col))
    K.join("Mil_Stone", st, col)

    # --- ahsap ust kat: kalkanlar ve yan duvarlar
    wd = []
    # Kalkanin ust kenari cati kabugunun 9 cm ALTINDA: tam kabuk hattina
    # oturtmak tahtanin alt yuzuyle ayni duzleme dusup z-fighting yapiyordu.
    gable = [(-HX, Z_STONE), (HX, Z_STONE), (HX, Z_WALL - 0.09),
             (0.0, Z_RIDGE - 0.09), (-HX, Z_WALL - 0.09)]
    wd.append(K.prism("wd", gable, HY - 0.14, HY, col))
    wd.append(K.prism("wd", gable, -HY, -HY + 0.14, col))
    for s in (1, -1):
        wd.append(K.box("wd", (s * (HX - 0.07), 0, (Z_STONE + Z_WALL) * 0.5),
                        (0.14, 2 * (HY - 0.14), Z_WALL - Z_STONE), col))
    K.join("Mil_Timber", wd, col)

    # --- cati: egim boyunca UZUN tahtalar + uzerlerinde cita.
    # Padavra sirasi kullanilmadi: siginak ve sapel zaten sirali; degirmen
    # farkli bir marangozluk gostersin diye tam boy tahta secildi.
    rf = []
    nb = 11
    bw = (2 * (HY + OVER)) / nb
    for s in (1, -1):
        for k in range(nb):
            y0 = -HY - OVER + k * bw
            pts = [at(s, 0.0, 0.0), at(s, _L, 0.0),
                   at(s, _L, 0.045), at(s, 0.0, 0.045)]
            rf.append(K.prism("rf", pts, y0 + 0.006, y0 + bw - 0.006, col))
        for k in range(nb + 1):
            y = -HY - OVER + k * bw
            pts = [at(s, 0.02, -0.030), at(s, _L - 0.02, -0.030),
                   at(s, _L - 0.02, 0.002), at(s, 0.02, 0.002)]
            rf.append(K.prism("rf", pts, y - 0.028, y + 0.028, col))
    # Mahya bindirmesi: her yamac icin ayri lama, karsi tarafa 6 cm tasar.
    # Tepede kesmek iki tahta arasinda 2*NX*kalinlik kadar acik birakiyor.
    for s in (1, -1):
        rf.append(K.prism("rf",
                          [at(s, _L - 0.22, -0.032), at(s, _L + 0.06, -0.032),
                           at(s, _L + 0.06, 0.012), at(s, _L - 0.22, 0.012)],
                          -HY - OVER, HY + OVER, col))
    K.join("Mil_Roof", rf, col)

    # --- su carki: yerel duzlemde kurulup Y ekseninde 90 derece dondurulur,
    # boylece mil X ekseni boyunca binaya girer.
    parts = []
    for zs in (-1, 1):
        z = zs * (WW * 0.5 - 0.06)
        rim = [(WR - 0.17, z - 0.055), (WR, z - 0.055), (WR, z + 0.055),
               (WR - 0.17, z + 0.055)]
        parts.append(K.revolve("wh", rim, BUCKETS, col, smooth_angle=0.0))
        for i in range(8):
            a = 2 * math.pi * i / 8
            parts.append(_rot_box("wh", a, (WR - 0.09) * 0.5,
                                  (WR - 0.09, 0.075, 0.075), z, col))
    for i in range(BUCKETS):
        a = 2 * math.pi * i / BUCKETS
        parts.append(_rot_box("wh", a, WR - 0.115, (0.24, 0.10, WW - 0.16),
                              0.0, col, tilt=math.radians(34)))
        parts.append(_rot_box("wh", a + math.pi / BUCKETS, WR - 0.21,
                              (0.10, 0.30, WW - 0.16), 0.0, col))
    # Mil ASIMETRIK: bina tarafina uzun uzanir. K.join ILK parcanin originini
    # korudugu icin mil ilk sirada olamaz -- ilk sirada oldugunda kendi orta
    # noktasi origin kabul edilip butun cark 40 cm kayiyordu (olculdu: gobek
    # uc kapagi x = 1.610, beklenen 1.210).
    parts.append(K.log("wh", (0, 0, -WW * 0.5 - 1.02), (0, 0, WW * 0.5 + 0.22),
                       0.115, 12, col))
    wheel = K.join("Mil_Wheel", parts, col)
    # K.log nesneyi QUATERNION moduna aliyor; o modda rotation_euler yazmak
    # hicbir sey yapmaz ve cark yatay duzlemde kaliyordu (olculdu: x acikligi
    # 3.65 m, yani yaricap X'te). Mod acikca geri aliniyor.
    wheel.rotation_mode = "XYZ"
    wheel.rotation_euler = (0.0, math.radians(90), 0.0)
    wheel.location = (WX, 0.0, WZ)

    # --- mil yatagi: tas ayak, carki tasir
    K.box("Mil_Bearing", (WX + 0.02, 0, WZ * 0.5 - 0.30),
          (0.62, 0.72, WZ - 0.60), col)

    # --- savak: suyu carkin tepesine tasiyan oluk ve sehpalari
    # Olugun alt ucu carkin TEPESINE gelmeli: ustten beslemeli carkta su
    # oradan dokulur. Binanin arkasindan baslatmak oluğu havada asili birakiyor
    # ve carkla iliskisi kopuyordu.
    fy0, fy1 = 0.62, 3.85
    fz0 = WZ + WR + 0.10
    fz1 = fz0 + 0.62
    fl = []
    for s in (1, -1):
        fl.append(K.prism("fl", [(fy0, fz0), (fy1, fz1), (fy1, fz1 + 0.34),
                                 (fy0, fz0 + 0.34)],
                          WX + s * FLUME_W * 0.5 - 0.045,
                          WX + s * FLUME_W * 0.5 + 0.045, col, axis="X"))
    fl.append(K.prism("fl", [(fy0, fz0), (fy1, fz1), (fy1, fz1 + 0.07),
                             (fy0, fz0 + 0.07)],
                      WX - FLUME_W * 0.5, WX + FLUME_W * 0.5, col, axis="X"))
    for y in (fy0 + 1.35, fy1 - 0.30):
        t = (y - fy0) / (fy1 - fy0)
        ztop = fz0 + (fz1 - fz0) * t
        for s in (1, -1):
            fl.append(K.box("fl", (WX + s * 0.34, y, ztop * 0.5),
                            (0.13, 0.13, ztop), col))
        fl.append(K.box("fl", (WX, y, ztop - 0.08), (0.92, 0.12, 0.12), col))
    K.join("Mil_Flume", fl, col)

    # --- kuyruk suyu kanali: carkin altindan cikan tas oluk
    tr = []
    for s in (1, -1):
        tr.append(K.box("tr", (WX + s * 0.62, -1.95, 0.19),
                        (0.24, 2.60, 0.38), col))
    # Kanal tabani zeminin 4 cm ALTINDA: tam z=0'a koymak zeminle z-fighting
    # yapip siyah bir serit birakiyordu.
    tr.append(K.box("tr", (WX, -1.95, 0.035), (1.48, 2.60, 0.15), col))
    K.join("Mil_Race", tr, col)

    # --- kapi kanadi
    leaf = K.box("Mil_DoorLeaf", (0, 0, 0), (dw - 0.05, 0.05, dh - 0.04), col)
    leaf.data.transform(Matrix.Translation(((dw - 0.05) * 0.5, 0, 0)))
    leaf.location = (-(dw - 0.05) * 0.5, -(HY - 0.045), (dh - 0.04) * 0.5 + 0.02)

    return dict(envelope=(-HX - OVER - 0.005, WX + WR + 0.06,
                          -3.32, fy1 + 0.06))


# ---------------------------------------------------------------- malzeme

STONE_PARTS = ("Mil_Stone", "Mil_Bearing", "Mil_Race")
WHEEL_PARTS = ("Mil_Wheel",)
LONG_AXIS = [("Mil_Stone", "X"), ("Mil_Timber", "X"), ("Mil_Roof", "Z"),
             ("Mil_RidgeCap", "Y"), ("Mil_Wheel", "X"), ("Mil_Bearing", "X"),
             ("Mil_Flume", "Y"), ("Mil_Race", "Y"), ("Mil_DoorLeaf", "Z")]


def _mat(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    return m


def materials(col):
    """Moloz tas / bina kerestesi / cark kerestesi.

    Cark SUREKLI islak calisir: tek parca olarak butun yuksekligi boyunca koyu
    ve yosunlu olmali, digerleri gibi yalniz dipte degil. Su seviyesine gore
    ayrim yapmak burada yanlis olurdu -- carkin her noktasi her turda suya
    giriyor.
    """
    stone, timber, wheel = _mat("Mil_StoneM"), _mat("Mil_Timber_M"), _mat("Mil_WheelM")
    K.build_stone(stone, "rustic_stone_wall", tiling=1.10, grain="U",
                  sat=0.90, val=0.80, var=(0.13, 0.9),
                  wet=(1.55, 0.10), wet_color=(0.115, 0.116, 0.100), wet_amt=0.62,
                  lichen=(2.40, 0.90), lichen_color=(0.245, 0.268, 0.195),
                  lichen_amt=0.44)
    K.build_wood(timber, "brown_planks_07", tiling=1.40, grain="U",
                 sat=0.66, val=0.74,
                 damp=(2.55, 3.40), damp_color=(0.135, 0.114, 0.092), damp_amt=0.56,
                 sun=(5.00, 3.20), sun_color=(0.340, 0.336, 0.326), sun_amt=0.44,
                 var=(0.17, 2.6))
    K.build_wood(wheel, "medieval_wood", tiling=1.85, grain="V",
                 sat=0.58, val=0.46,
                 damp=(4.20, 5.60), damp_color=(0.088, 0.086, 0.066), damp_amt=0.72,
                 sun=(6.20, 5.80), sun_color=(0.190, 0.196, 0.176), sun_amt=0.20,
                 var=(0.18, 2.4))

    grain = {stone.name: "U", timber.name: "U", wheel.name: "V"}
    for o in col.objects:
        n = o.name
        m = (wheel if n.startswith(WHEEL_PARTS) else
             stone if n.startswith(STONE_PARTS) else timber)
        o.data.materials.clear()
        o.data.materials.append(m)
        K.cube_uv(o, 2.0)
        K.align_grain(o, next((a for p, a in LONG_AXIS if n.startswith(p)), "Z"),
                      grain[m.name])
    return stone, timber, wheel
