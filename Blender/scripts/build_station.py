# Telsiz Istasyonu -- ciglar arasi roleyi tasiyan kucuk kulube ve gergili
# kafes direk.
#
# Neden bu bicim: bir role direginin ISI gorunmek degil, ANTENI yukari
# tasimaktir. Bu yuzden direk mumkun oldugunca ince ve hafif kurulur, yanal
# yuku gergi telleri alir. Kulube sadece ekipman ve aku icindir: penceresiz,
# kucuk, sac kapli, beton kaideye oturur.
#
# Silueti alcak kutle + cok ince dusey cizgi. Onceki sekiz yapinin hepsi
# ahsap/tas ve el yapimi; bu tek MODERN parca. Ormanda bulundugunda "burada
# baska biri var" der -- digerleri "burada biri vardi" der.

import math
import bpy
from mathutils import Matrix

import outpost_kit as K

# ---------------------------------------------------------------- olculer
HX, HY = 1.35, 1.62        # kulube yari olculeri
PAD_H = 0.24
Z_WALL = 2.42              # kulube duvar ustu
ROOF_RISE = 0.34           # tek egim
MAST_X, MAST_Y = 3.05, 0.0
MAST_R = 0.30              # kafes direk yari acikligi (ucgen)
MAST_TOP = 10.60
LEG = 0.075
GUY_Z = (4.10, 7.60)
GUY_R = 4.55
DOOR_W, DOOR_H = 0.92, 2.04


def roof_z(x, y):
    return Z_WALL + ROOF_RISE * (0.5 - (y + HY) / (2 * HY)) + ROOF_RISE * 0.5


def surf_dist(v):
    if abs(v.x) > HX + 0.20 or abs(v.y) > HY + 0.20:
        return -99.0
    return v.z - roof_z(v.x, v.y)


# Gergi ucgeninin yonu iki kez olcumle duzeltildi:
#   90/210/330  -> 210 derecedeki tel kapinin onunden geciyor, kanat acilmiyor.
#   150/270/30  -> 150 derecedeki tel kulubenin CATISINI deliyor (20 yuz cifti).
#   120/240/0   -> iki simetrik tel kulubenin yanindan gecer, ucuncu direkten
#                  uzaga gider. Kapi da cati da serbest.
GUY_A0 = 120


def _leg_xy(i, r=None):
    r = MAST_R if r is None else r
    a = math.radians(90 + 120 * i)
    return MAST_X + r * math.cos(a), MAST_Y + r * math.sin(a)


def build(col):
    for o in list(col.objects):
        bpy.data.objects.remove(o, do_unlink=True)

    # --- beton kaide
    K.box("Sta_Pad", (0, 0, PAD_H * 0.5), (2 * HX + 0.44, 2 * HY + 0.44, PAD_H), col)

    # --- sac kapli kutu: dort duvar, on duvarda kapi acikligi
    # Duvar ustu catinin ALTINI izler. Once butun duvarlar tek yukseklikte
    # kuruldu ve tek egimli cati ile aralarinda 34 cm'ye kadar acik kaldi:
    # yan duvarlarin ust kenari da egimli olmak zorunda.
    zb = roof_z(0.0, HY) - 0.055     # arka (alcak) uc
    zf = roof_z(0.0, -HY) - 0.055    # on (yuksek) uc
    walls = []
    walls.append(K.box("wl", (0, HY - 0.045, (PAD_H + zb) * 0.5),
                       (2 * HX, 0.09, zb - PAD_H), col))
    for s in (1, -1):
        walls.append(K.prism("wl", [(-HY, PAD_H), (HY, PAD_H), (HY, zb), (-HY, zf)],
                             s * (HX - 0.09), s * HX, col, axis="X"))
    side = (2 * HX - DOOR_W) * 0.5
    for s in (1, -1):
        walls.append(K.box("wl", (s * (HX - side * 0.5), -(HY - 0.045),
                                  (PAD_H + zf) * 0.5), (side, 0.09, zf - PAD_H), col))
    walls.append(K.box("wl", (0, -(HY - 0.045), (PAD_H + DOOR_H + zf) * 0.5),
                       (DOOR_W, 0.09, zf - PAD_H - DOOR_H), col))
    K.join("Sta_Walls", walls, col)

    # --- tek egimli sac cati, dort yanda kucuk tasma
    K.shell("Sta_Roof", roof_z, -HX - 0.16, HX + 0.16, -HY - 0.16, HY + 0.16,
            4, 4, 0.055, col)

    # --- celik kapi: mentese ekseninde doner
    leaf = K.box("Sta_DoorLeaf", (0, 0, 0), (DOOR_W - 0.03, 0.045, DOOR_H - 0.03), col)
    leaf.data.transform(Matrix.Translation(((DOOR_W - 0.03) * 0.5, 0, 0)))
    # Kanat duvar kalinliginin DISINDA durur. Duvar duzlemine gomulu birakmak
    # kanadi her iki yone donerken duvari kesiyordu: kapi modelde var ama
    # oyunda acilamiyordu (BVH testiyle olculdu).
    leaf.location = (-(DOOR_W - 0.03) * 0.5, -(HY + 0.0225), PAD_H + DOOR_H * 0.5)
    # matrix_world TAZELENMEDEN okunursa matrix_parent_inverse birim kalir ve
    # cocuk parca ebeveynin donusumu kadar kayar (olculdu: 1.42 m).
    bpy.context.view_layer.update()
    for i, z in enumerate((0.32, DOOR_H - 0.36)):
        h = K.box("Sta_Hinge_%d" % i, (0, 0, 0), (0.10, 0.055, 0.14), col)
        # x'te 7 cm iceride: kanadin kenar yuzuyle ayni duzleme dusmesin.
        h.location = (-(DOOR_W - 0.03) * 0.5 + 0.07, -(HY + 0.068),
                      PAD_H + z)
        h.parent = leaf
        h.matrix_parent_inverse = leaf.matrix_world.inverted()

    # --- kafes direk: uc bacak, yatay halkalar ve caprazlar
    mast = []
    for i in range(3):
        x0, y0 = _leg_xy(i)
        mast.append(K.box("ms", (x0, y0, (0.34 + MAST_TOP) * 0.5),
                          (LEG, LEG, MAST_TOP - 0.34), col))
    rungs = 11
    for k in range(rungs + 1):
        z = 0.34 + (MAST_TOP - 0.34) * k / rungs
        for i in range(3):
            a, b = _leg_xy(i), _leg_xy((i + 1) % 3)
            mast.append(_strut(a[0], a[1], z, b[0], b[1], z, 0.045, col))
            if k < rungs:
                z2 = 0.34 + (MAST_TOP - 0.34) * (k + 1) / rungs
                mast.append(_strut(a[0], a[1], z, b[0], b[1], z2, 0.038, col))
    K.join("Sta_Mast", mast, col)
    K.box("Sta_MastBase", (MAST_X, MAST_Y, 0.17), (1.05, 1.05, 0.34), col)

    # --- anten ve anemometre
    K.log("Sta_Antenna", (MAST_X, MAST_Y, MAST_TOP - 0.10),
          (MAST_X, MAST_Y, MAST_TOP + 1.55), 0.032, 8, col)
    # Kollar merkezden 5 cm uzakta baslar: ucu ucuna birlestirmek uc kapaklarini
    # ayni duzleme dusuruyordu.
    for i in range(3):
        a = math.radians(120 * i)
        K.log("Sta_Cup_%d" % i,
              (MAST_X + 0.018 * math.cos(a), MAST_Y + 0.018 * math.sin(a),
               MAST_TOP + 1.30),
              (MAST_X + 0.30 * math.cos(a), MAST_Y + 0.30 * math.sin(a),
               MAST_TOP + 1.30), 0.022, 6, col)

    # --- gergi telleri ve beton ankrajlar
    guys = []
    for i in range(3):
        a = math.radians(GUY_A0 + 120 * i)
        ax = MAST_X + GUY_R * math.cos(a)
        ay = MAST_Y + GUY_R * math.sin(a)
        K.box("Sta_Anchor_%d" % i, (ax, ay, 0.15), (0.44, 0.44, 0.30), col)
        for z in GUY_Z:
            lx, ly = _leg_xy(i, MAST_R * 0.9)
            guys.append(K.log("gy", (lx, ly, z), (ax, ay, 0.28), 0.016, 5, col))
    K.join("Sta_Guys", guys, col)

    # --- kablo tavasi: kulubeden direge
    K.box("Sta_Tray", ((HX + MAST_X - MAST_R) * 0.5 + 0.1, 0.0, 0.42),
          (MAST_X - MAST_R - HX + 0.2, 0.16, 0.09), col)

    # Dis kabugu gergi ankrajlari belirler; kulube bunun icinde kalir.
    return dict(envelope=(min(-HX - 0.24, MAST_X - GUY_R - 0.23),
                          MAST_X + GUY_R + 0.23,
                          MAST_Y - GUY_R - 0.23, MAST_Y + GUY_R + 0.23))


def _strut(x0, y0, z0, x1, y1, z1, w, col):
    import bmesh
    import mathutils
    p0 = mathutils.Vector((x0, y0, z0))
    d = mathutils.Vector((x1, y1, z1)) - p0
    me = bpy.data.meshes.new("st")
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=mathutils.Vector((w, w, d.length)), verts=bm.verts)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new("st", me)
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = mathutils.Vector((0, 0, 1)).rotation_difference(d.normalized())
    obj.location = p0 + d * 0.5
    col.objects.link(obj)
    return obj


# ---------------------------------------------------------------- malzeme

CONCRETE_PARTS = ("Sta_Pad", "Sta_Anchor", "Sta_MastBase")
STEEL_PARTS = ("Sta_Mast", "Sta_Guys", "Sta_Antenna", "Sta_Cup", "Sta_Tray",
               "Sta_DoorLeaf", "Sta_Hinge")
LONG_AXIS = [("Sta_Pad", "X"), ("Sta_Walls", "Z"), ("Sta_Roof", "X"),
             ("Sta_DoorLeaf", "Z"), ("Sta_Hinge", "X"), ("Sta_Mast", "Z"),
             ("Sta_MastBase", "X"), ("Sta_Antenna", "Z"), ("Sta_Cup", "X"),
             ("Sta_Guys", "Z"), ("Sta_Anchor", "X"), ("Sta_Tray", "X")]


def _mat(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    return m


def materials(col):
    """Sac kaplama / galvaniz celik / beton.

    Pas burada AZ olmali: istasyon bakimli, galvanizli ve genc. Terk edilmis
    evle ayni pas siddeti verilseydi "modern" okumasi kaybolurdu -- yapiyi
    digerlerinden ayiran sey tam olarak bakimli gorunmesi.
    """
    clad, steel, conc = _mat("Sta_Clad"), _mat("Sta_Steel"), _mat("Sta_Concrete")
    # Kaplama gri-mavi: yesil birakilinca gozetleme kulesinin boyasiyla ayni
    # aileye dusuyordu. Ayni ormanda iki yesil yapi kimlikleri karistirir.
    K.build_metal(clad, "container_side", tiling=1.10, grain="V", metallic=0.40,
                  sat=0.22, val=0.96, base_tint=(0.66, 0.71, 0.75),
                  rust=(0.85, 2.10), rust_color=(0.36, 0.180, 0.085), rust_amt=0.34)
    # Galvaniz celik parlak degil ama SIYAH da degil; koyu birakinca direk
    # silueti kaybolup tek bir cizgiye donusuyordu.
    K.build_metal(steel, "metal_plate_02", tiling=2.40, grain="V", metallic=0.34,
                  sat=0.45, val=1.55, base_tint=(0.80, 0.82, 0.84),
                  rust=(0.70, 2.60), rust_color=(0.34, 0.165, 0.075), rust_amt=0.26)
    K.build_stone(conc, "concrete_wall_007", tiling=1.60, grain="U",
                  sat=0.70, val=0.92, var=(0.10, 1.1),
                  wet=(0.22, 0.02), wet_color=(0.115, 0.113, 0.108), wet_amt=0.55,
                  lichen=(0.34, 0.10), lichen_color=(0.250, 0.262, 0.215),
                  lichen_amt=0.26)

    grain = {clad.name: "V", steel.name: "V", conc.name: "U"}
    for o in col.objects:
        n = o.name
        m = (conc if n.startswith(CONCRETE_PARTS) else
             steel if n.startswith(STEEL_PARTS) else clad)
        o.data.materials.clear()
        o.data.materials.append(m)
        K.cube_uv(o, 2.0)
        K.align_grain(o, next((a for p, a in LONG_AXIS if n.startswith(p)), "Z"),
                      grain[m.name])
    return clad, steel, conc
