# Kereste Barakasi -- yari acik kereste kurutma barakasi.
#
# Arastirmadan gelen zorunluluklar:
#   - Kurutma barakasi HAVA ALMALI: on cephe tamamen acik, uclar kalkan
#     ucgeninden asagisi acik. Kapali kutu yapmak isin amacini yok eder.
#   - Sacak genis olur (yagmuru kerestenin uzerinden atar).
#   - Kereste dogrudan zemine degil, cita (sticker) araliklariyla istiflenir;
#     katlar arasindaki bosluk gorunur olmali, yoksa tek blok gibi okunur.
#
# Silueti: dar ve yuksek ucgen, alt yarisi bos. Diger dortluden tek "iceri
# bakilabilen" yapi; oyuncu yaklasmadan dolu mu bos mu gorur.

import math
import bpy

import outpost_kit as K

# ---------------------------------------------------------------- olculer
HX = 3.00          # yari uzunluk (X)
DY = 4.00          # derinlik (Y), mahya X boyunca
ZE, ZR = 3.20, 4.60    # sacak ve mahya
POST = 0.20
OVER_Y, OVER_X = 0.45, 0.35   # sacak tasmasi

_dy, _dz = DY * 0.5, ZR - ZE
_L = math.hypot(_dy, _dz)
UY, UZ = _dy / _L, _dz / _L
NY, NZ = -UZ, UY               # on egimin dis normali
C = NY * 0.0 + NZ * ZE
SLOPE = _dz / _dy

TH_SHEET = 0.022
D_PURLIN, D_RAFTER = TH_SHEET, TH_SHEET + 0.11


def surf_dist(v):
    """Cati kabugundan dik isaretli uzaklik; disarisi arti."""
    yy = min(v.y, DY - v.y)
    return NY * yy + NZ * v.z - C


def at(side, t, d):
    """Sacaktan t kadar yukari, kabuktan d kadar iceri nokta (y, z).

    side=+1 on egim (y kucuk), -1 arka egim.
    """
    y = UY * t - NY * d
    z = ZE + UZ * t - NZ * d
    return (y if side > 0 else DY - y, z)


def roof_z(y):
    return ZE + SLOPE * min(y, DY - y)


def build(col):
    for o in list(col.objects):
        bpy.data.objects.remove(o, do_unlink=True)

    px = (-2.62, -0.87, 0.87, 2.62)
    py = (0.30, DY - 0.30)

    # Sacak kirisinin ustu, mertegin alt yuzunun altinda kalmali. Once sacak
    # yuksekligine konuldu ve mertek kabugu deldi (olculdu: 21.7 mm).
    z_plate_top = ZE - NZ * D_RAFTER - 0.08
    z_plate_bot = z_plate_top - 0.17

    # --- tas pabuc + direk. Direk dogrudan topraga oturmaz; alt uc curur.
    for i, x in enumerate(px):
        for j, y in enumerate(py):
            K.box("Shed_Pad_%d%d" % (i, j), (x, y, 0.055), (0.40, 0.40, 0.11), col)
            K.box("Shed_Post_%d%d" % (i, j), (x, y, (0.11 + z_plate_bot) * 0.5),
                  (POST, POST, z_plate_bot - 0.11), col)

    # --- sacak kirisi (Y'de sabit, X boyunca) ve baglanti kirisleri
    for j, y in enumerate(py):
        K.box("Shed_Plate_%d" % j, (0, y, (z_plate_top + z_plate_bot) * 0.5),
              (2 * HX, 0.19, 0.17), col)
    for i, x in enumerate(px):
        # 2 cm asagida: ust yuzu direginkiyle ayni duzleme dusmesin.
        K.box("Shed_Tie_%d" % i, (x, DY * 0.5, z_plate_bot - 0.13),
              (0.16, py[1] - py[0], 0.22), col)

    # --- mertek ve asik. Mahya kirisinin tepesi de kabugun altinda kalir.
    z_ridge_top = ZR - SLOPE * 0.07 - 0.06
    K.box("Shed_Ridge", (0, DY * 0.5, z_ridge_top - 0.15), (2 * HX, 0.14, 0.30), col)
    # Mertekler mahya kirisine dayanip biter; karsi yamacinkiyle ust uste
    # binince yan yuzleri ayni duzleme dusuyor ve z-fighting yapiyordu.
    t_end = (DY * 0.5 - 0.07 + NY * D_RAFTER) / UY
    for i, x in enumerate(px + (-1.75, 1.75, 0.0)):
        for s in (1, -1):
            y0, z0 = at(s, 0.0, D_RAFTER)
            y1, z1 = at(s, t_end, D_RAFTER)
            K.box("Shed_Rafter_%s%d" % ("F" if s > 0 else "B", i), (x, 0, 0),
                  (0.10, 1.0, 1.0), col)
            o = col.objects["Shed_Rafter_%s%d" % ("F" if s > 0 else "B", i)]
            _fit_beam(o, x, (y0, z0), (y1, z1), 0.10, 0.16)
    for s in (1, -1):
        # En ustteki asik mahyaya fazla yaklasirsa karsi yamacinkiyle ust uste
        # biner ve yatay yuzleri ayni duzleme duser.
        for i, t in enumerate((0.70, 1.45, 2.10)):
            py_, pz = at(s, t, D_PURLIN + 0.055)
            K.box("Shed_Purlin_%s%d" % ("F" if s > 0 else "B", i),
                  (0, py_, pz), (2 * HX, 0.09, 0.11), col)

    # --- oluklu sac ortu: 0.82 m'lik levhalar, donusumlu 6 mm derinlik farki
    # bindirme cizgisi verir. Duz tek yuzey sac gibi okunmuyordu.
    sheets = []
    n = int(round((2 * HX + 2 * OVER_X) / 0.82))
    w = (2 * HX + 2 * OVER_X) / n
    for s in (1, -1):
        ya, za = at(s, -OVER_Y * (_L / _dy), 0.0)
        yb, zb = at(s, _L, 0.0)
        for k in range(n):
            d0 = 0.0 if k % 2 == 0 else 0.006
            pts = [(ya, za - d0 * NZ), (yb, zb - d0 * NZ),
                   (yb, zb - d0 * NZ - TH_SHEET), (ya, za - d0 * NZ - TH_SHEET)]
            x0 = -HX - OVER_X + k * w
            sheets.append(_prism_yz(pts, x0 + 0.004, x0 + w - 0.004, col))
    K.join("Shed_Roof", sheets, col)
    K.box("Shed_RidgeCap", (0, DY * 0.5, ZR + 0.055), (2 * HX + 2 * OVER_X, 0.46, 0.11), col)

    # --- arka duvar: dikey tahtalar, aralarinda 15 mm hava boslugu
    back = []
    bw, gap = 0.235, 0.015
    nb = int((2 * HX) // (bw + gap))
    for k in range(nb):
        x = -HX + (bw + gap) * k + bw * 0.5
        back.append(K.box("bb", (x, DY - 0.16, (0.10 + z_plate_top) * 0.5),
                          (bw, 0.032, z_plate_top - 0.10), col))
    K.join("Shed_BackWall", back, col)

    # --- kalkan ucgeni: iki ucta, sacak hizasinin uzerinde. Altta acik kalir.
    for s in (1, -1):
        pts = [(0.0, z_plate_top), (DY, z_plate_top), (DY * 0.5, z_ridge_top - 0.02)]
        # Kiris uclariyla ayni duzleme dusmemesi icin 5 mm iceride.
        K.prism("Shed_Gable_%s" % ("R" if s > 0 else "L"),
                pts, s * (HX - 0.045), s * (HX - 0.005), col, axis="X")

    # --- istiflenmis kereste: katlar cita ile ayrilir, bosluk gorunur
    # Cita kalinligi 45 mm: katlar arasindaki bosluk gorunur ama yigin tek
    # blok gibi degil, hava alan bir istif gibi okunur.
    stack = []
    for lay in range(9):
        z = 0.2775 + lay * 0.163
        for b in range(7):
            xx = -2.45 + b * 0.42
            stack.append(K.box("lb", (xx, 2.05, z), (0.38, 2.30, 0.115), col))
        for st in (-2.0, 0.0, 2.0):
            stack.append(K.box("ls", (-0.35, 2.05 + st * 0.5, z + 0.0805),
                               (4.90, 0.055, 0.046), col))
    K.join("Shed_Lumber", stack, col)
    for i, y in enumerate((0.95, 2.05, 3.15)):
        K.box("Shed_Bearer_%d" % i, (-0.35, y, 0.11), (4.90, 0.14, 0.22), col)

    return dict(envelope=(-HX - OVER_X - 0.005, HX + OVER_X + 0.005,
                          -OVER_Y - 0.02, DY + OVER_Y + 0.02))


def _prism_yz(pts_yz, x0, x1, col):
    return K.prism("sh", pts_yz, x0, x1, col, axis="X")


def _fit_beam(obj, x, p0, p1, wide, deep):
    """Kutuyu iki nokta arasina egik kiris olarak oturtur."""
    import mathutils
    y0, z0 = p0
    y1, z1 = p1
    ln = math.hypot(y1 - y0, z1 - z0)
    obj.data.clear_geometry()
    import bmesh
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=mathutils.Vector((wide, ln, deep)), verts=bm.verts)
    bm.to_mesh(obj.data)
    bm.free()
    obj.location = (x, (y0 + y1) * 0.5, (z0 + z1) * 0.5)
    obj.rotation_euler = (math.atan2(z1 - z0, y1 - y0), 0, 0)


# ---------------------------------------------------------------- malzeme

METAL_PARTS = ("Shed_Roof", "Shed_RidgeCap")
STONE_PARTS = ("Shed_Pad",)
LUMBER_PARTS = ("Shed_Lumber",)
LONG_AXIS = [("Shed_Pad", "X"), ("Shed_Post", "Z"), ("Shed_Plate", "X"),
             ("Shed_Tie", "Y"), ("Shed_Ridge", "X"), ("Shed_RidgeCap", "X"),
             ("Shed_Rafter", "Y"), ("Shed_Purlin", "X"), ("Shed_Roof", "Y"),
             ("Shed_BackWall", "Z"), ("Shed_Gable", "Y"),
             ("Shed_Lumber", "Y"), ("Shed_Bearer", "X")]


def _mat(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    return m


def materials(col):
    """Kaba kereste / oluklu sac / kesilmis tahta / tas pabuc.

    Pas cizgileri sacin altindan baslar ve tahtaya akar: rust bolgesi catinin
    altindan asagi dogru tanimlanir, duz yuzeyin ortasindan degil.
    """
    frame, iron, lumber, stone = (_mat("Shed_Frame"), _mat("Shed_Iron"),
                                  _mat("Shed_Lumber"), _mat("Shed_Stone"))
    # Damar eksenleri OLCULDU, tahmin edilmedi: wooden_planks U (dU 0.0135 <
    # dV 0.0323), worn_corrugated_iron V. Ikisini de yanlis vermek direkleri
    # tugla, sac levhalari tahta gibi gosteriyordu.
    K.build_wood(frame, "wooden_planks", tiling=1.45, grain="U", sat=0.60, val=0.86,
                 damp=(0.62, 1.45), damp_color=(0.150, 0.128, 0.106), damp_amt=0.56,
                 sun=(4.20, 1.90), sun_color=(0.370, 0.366, 0.358), sun_amt=0.46,
                 var=(0.16, 2.8))
    K.build_metal(iron, "worn_corrugated_iron", tiling=2.15, grain="V", metallic=0.34,
                  base_tint=(0.52, 0.555, 0.585),
                  rust=(4.62, 3.15), rust_color=(0.40, 0.180, 0.075), rust_amt=0.74)
    K.build_wood(lumber, "planks_brown_10", tiling=1.30, grain="U", sat=0.86, val=0.96,
                 damp=(0.45, 1.05), damp_color=(0.150, 0.126, 0.104), damp_amt=0.45,
                 sun=(2.10, 1.10), sun_color=(0.345, 0.340, 0.330), sun_amt=0.20,
                 var=(0.15, 3.0))
    K.build_stone(stone, "stacked_stone_wall", tiling=2.2, grain="U", sat=0.86, val=0.84,
                  wet=(0.16, 0.02), wet_color=(0.140, 0.136, 0.126), wet_amt=0.60,
                  lichen=(0.30, 0.06), lichen_color=(0.280, 0.295, 0.235), lichen_amt=0.30)

    grain = {frame.name: "U", iron.name: "V", lumber.name: "U", stone.name: "U"}
    for o in col.objects:
        n = o.name
        m = (iron if n.startswith(METAL_PARTS) else
             stone if n.startswith(STONE_PARTS) else
             lumber if n.startswith(LUMBER_PARTS) else frame)
        o.data.materials.clear()
        o.data.materials.append(m)
        K.cube_uv(o, 2.0)
        K.align_grain(o, next((a for p, a in LONG_AXIS if n.startswith(p)), "Z"),
                      grain[m.name])
    return frame, iron, lumber, stone
