# Gozetleme Kulesi -- yangin gozetleme kulesi.
#
# Arastirmadan gelen zorunluluklar (USFS L-4 / R-6 tipi):
#   - Dort ayak CAPRAZ PAYANDALI olur; payanda olmadan yapi yanal ruzgara
#     dayanmaz ve gozle de "duruyor" gibi okunmaz.
#   - Ayaklar asagi dogru ACILIR (splay): tabanda genis, platformda dar.
#   - Kabin dort yani cam; kulenin isi gormek, gorunmemek degil.
#   - Platform kabinden genistir, cepecevre yurume yolu ve korkuluk vardir.
#
# Silueti tek dikey: digger dortlu yatay kutledir, bu gokyuzune cikar. Ormanda
# navigasyon isareti olarak calisir.

import math
import bpy

import outpost_kit as K

# ---------------------------------------------------------------- olculer
BASE, TOPS = 2.20, 1.55        # ayak yari acikligi: tabanda / platformda
ZP = 4.80                      # platform ust kotu
DECK = 0.10
CAT = 2.68                     # korkuluk icinde 0.93 m net yurume yolu
CAB = 1.70                     # kabin yari genisligi
Z_CAB0 = ZP + DECK
Z_EAVE = Z_CAB0 + 2.30
ROOF_X = 2.05
Z_APEX = Z_EAVE + 0.82
LEG = 0.18
RAIL_H = 1.06

_k = (Z_APEX - Z_EAVE) / ROOF_X


def roof_z(x, y):
    return Z_APEX - _k * max(abs(x), abs(y))


def surf_dist(v):
    """Kirma catinin dis yuzeyinden dusey uzaklik. Egim 21 derece: dusey
    olcum burada yeterli, A-frame'deki gibi dik olcume gerek yok."""
    if max(abs(v.x), abs(v.y)) > ROOF_X + 1e-6:
        return -99.0
    return v.z - roof_z(v.x, v.y)


def leg_x(z, s):
    """Ayak ekseninin z yuksekligindeki x (ya da y) konumu."""
    t = min(max(z / ZP, 0.0), 1.0)
    return s * (BASE + (TOPS - BASE) * t)


def build(col):
    for o in list(col.objects):
        bpy.data.objects.remove(o, do_unlink=True)

    corners = ((1, 1), (1, -1), (-1, -1), (-1, 1))

    # --- ayaklar: asagi dogru acilan egik direkler
    for i, (sx, sy) in enumerate(corners):
        p0 = (leg_x(0, sx), leg_x(0, sy), 0.0)
        p1 = (leg_x(ZP, sx), leg_x(ZP, sy), ZP)
        _beam("Tow_Leg_%d" % i, p0, p1, LEG, LEG, col)
        K.box("Tow_Pad_%d" % i, (p0[0], p0[1], 0.06), (0.46, 0.46, 0.12), col)

    # --- yatay kusaklar ve capraz payandalar, iki katta
    tiers = ((0.90, 2.75), (2.75, 4.55))
    for face in range(4):
        sx, sy = corners[face]
        tx, ty = corners[(face + 1) % 4]
        for ti, (za, zb) in enumerate(tiers):
            for z in ((za,) if ti == 0 else (za, zb)):
                a = (leg_x(z, sx), leg_x(z, sy), z)
                b = (leg_x(z, tx), leg_x(z, ty), z)
                _beam("Tow_Girt_%d%d_%.0f" % (face, ti, z * 10), a, b, 0.11, 0.13, col)
            a0 = (leg_x(za, sx), leg_x(za, sy), za)
            b0 = (leg_x(za, tx), leg_x(za, ty), za)
            a1 = (leg_x(zb, sx), leg_x(zb, sy), zb)
            b1 = (leg_x(zb, tx), leg_x(zb, ty), zb)
            _beam("Tow_Brace_%d%dA" % (face, ti), a0, b1, 0.085, 0.055, col)
            _beam("Tow_Brace_%d%dB" % (face, ti), b0, a1, 0.085, 0.055, col)

    # --- platform kirisleri ve dosemesi
    for s in (1, -1):
        K.box("Tow_PBeam_X%d" % (s > 0), (0, s * TOPS, ZP - 0.13),
              (2 * CAT, 0.16, 0.26), col)
        K.box("Tow_PBeam_Y%d" % (s > 0), (s * TOPS, 0, ZP - 0.13),
              (0.16, 2 * (TOPS - 0.08), 0.26), col)
    # yurume yolu: kabinin cevresini saran dort serit
    ring = []
    ring.append(K.box("dk", (0, (CAT + CAB) * 0.5, ZP + DECK * 0.5),
                      (2 * CAT, CAT - CAB, DECK), col))
    ring.append(K.box("dk", (0, -(CAT + CAB) * 0.5, ZP + DECK * 0.5),
                      (2 * CAT, CAT - CAB, DECK), col))
    ring.append(K.box("dk", ((CAT + CAB) * 0.5, 0, ZP + DECK * 0.5),
                      (CAT - CAB, 2 * CAB, DECK), col))
    ring.append(K.box("dk", (-(CAT + CAB) * 0.5, 0, ZP + DECK * 0.5),
                      (CAT - CAB, 2 * CAB, DECK), col))
    K.join("Tow_Catwalk", ring, col)
    K.box("Tow_CabFloor", (0, 0, ZP + DECK * 0.5), (2 * CAB, 2 * CAB, DECK), col)

    # --- korkuluk
    rails = []
    for s in (1, -1):
        for axis in (0, 1):
            for zr in (0.46, RAIL_H - 0.05):
                if axis == 0:
                    rails.append(K.box("rl", (0, s * (CAT - 0.05), Z_CAB0 + zr),
                                       (2 * CAT, 0.055, 0.075), col))
                else:
                    rails.append(K.box("rl", (s * (CAT - 0.05), 0, Z_CAB0 + zr),
                                       (0.055, 2 * (CAT - 0.11), 0.075), col))
    for sx in (1, -1):
        for sy in (1, -1):
            rails.append(K.box("rl", (sx * (CAT - 0.05), sy * (CAT - 0.05),
                                      Z_CAB0 + RAIL_H * 0.5),
                               (0.085, 0.085, RAIL_H), col))
    # Ara direkler cift dongunun DISINDA: icinde uretilince her biri iki kez
    # olusup ayni yerde ust uste biniyordu ve siyah z-fighting yapiyordu.
    for s in (1, -1):
        rails.append(K.box("rl", (s * (CAT - 0.05), 0.0, Z_CAB0 + RAIL_H * 0.5),
                           (0.07, 0.07, RAIL_H), col))
        rails.append(K.box("rl", (0.0, s * (CAT - 0.05), Z_CAB0 + RAIL_H * 0.5),
                           (0.07, 0.07, RAIL_H), col))
    K.join("Tow_Rail", rails, col)

    # --- kabin duvarlari: her cephede genis pencere, kose payandalari kalir
    WIN_Z0, WIN_Z1 = Z_CAB0 + 0.95, Z_CAB0 + 1.95
    JAMB = 0.34
    walls = []
    for s in (1, -1):
        for axis in (0, 1):
            def put(cx, cy, sx_, sy_, z0, z1):
                walls.append(K.box("wl", (cx, cy, (z0 + z1) * 0.5),
                                   (sx_, sy_, z1 - z0), col))
            if axis == 0:
                put(0, s * (CAB - 0.045), 2 * CAB, 0.09, Z_CAB0, WIN_Z0)
                put(0, s * (CAB - 0.045), 2 * CAB, 0.09, WIN_Z1, Z_EAVE)
                for t in (1, -1):
                    put(t * (CAB - JAMB * 0.5), s * (CAB - 0.045), JAMB, 0.09,
                        WIN_Z0, WIN_Z1)
            else:
                put(s * (CAB - 0.045), 0, 0.09, 2 * (CAB - 0.09), Z_CAB0, WIN_Z0)
                put(s * (CAB - 0.045), 0, 0.09, 2 * (CAB - 0.09), WIN_Z1, Z_EAVE)
                for t in (1, -1):
                    put(s * (CAB - 0.045), t * (CAB - 0.09 - JAMB * 0.5), 0.09,
                        JAMB, WIN_Z0, WIN_Z1)
    K.join("Tow_Cab", walls, col)

    # --- cam: dort cephede tek buyuk panel
    glass = []
    for s in (1, -1):
        glass.append(K.box("gl", (0, s * (CAB - 0.045), (WIN_Z0 + WIN_Z1) * 0.5),
                           (2 * (CAB - JAMB) - 0.02, 0.016, WIN_Z1 - WIN_Z0 - 0.02), col))
        glass.append(K.box("gl", (s * (CAB - 0.045), 0, (WIN_Z0 + WIN_Z1) * 0.5),
                           (0.016, 2 * (CAB - 0.09 - JAMB) - 0.02,
                            WIN_Z1 - WIN_Z0 - 0.02), col))
    K.join("Tow_Glass", glass, col)

    # --- kirma cati
    K.heightfield("Tow_Roof", roof_z, -ROOF_X, ROOF_X, -ROOF_X, ROOF_X,
                  18, 18, col, base=Z_EAVE - 0.14)

    # --- soba borusu
    K.log("Tow_Flue", (0.95, 0.95, Z_EAVE - 0.60), (0.95, 0.95, Z_APEX + 0.72), 0.062, 12, col)
    K.log("Tow_FlueCap", (0.95, 0.95, Z_APEX + 0.715), (0.95, 0.95, Z_APEX + 0.78),
          0.105, 12, col)

    # --- merdiven: -Y cephesinde dusey, platformdaki bosluga cikar
    lad = []
    for s in (1, -1):
        lad.append(K.box("ld", (s * 0.42, -(CAT - 0.06), (Z_CAB0 - 0.02) * 0.5),
                         (0.075, 0.055, Z_CAB0 - 0.02), col))
    z = 0.30
    while z < Z_CAB0 - 0.20:
        lad.append(K.box("ld", (0, -(CAT - 0.06), z), (0.92, 0.045, 0.045), col))
        z += 0.30
    K.join("Tow_Ladder", lad, col)

    # Genisletilen yurume yolu artik taban pabuclarindan disari tasar.
    ext = max(BASE + 0.23, CAT) + 0.005
    return dict(envelope=(-ext, ext, -ext, ext))


def _beam(name, p0, p1, wide, deep, col):
    """Iki nokta arasina dikdortgen kesitli egik kiris."""
    import bmesh
    import mathutils
    d = mathutils.Vector(p1) - mathutils.Vector(p0)
    ln = d.length
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=mathutils.Vector((wide, deep, ln)), verts=bm.verts)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = mathutils.Vector((0, 0, 1)).rotation_difference(d.normalized())
    obj.location = (mathutils.Vector(p0) + mathutils.Vector(p1)) * 0.5
    col.objects.link(obj)
    return obj


# ---------------------------------------------------------------- malzeme

PAINT_PARTS = ("Tow_Cab", "Tow_Rail")
ROOF_PARTS = ("Tow_Roof",)
# Yurume yolu ve merdiven AHSAP: ahsap iskeleli bir kulede celik izgara
# doseme yabanci duruyor, kirmizi bir levha gibi okunuyordu.
METAL_PARTS = ("Tow_Flue",)
GLASS_PARTS = ("Tow_Glass",)
LONG_AXIS = [("Tow_Leg", "Z"), ("Tow_Pad", "X"), ("Tow_Girt", "X"),
             ("Tow_Brace", "Z"), ("Tow_PBeam_X", "X"), ("Tow_PBeam_Y", "Y"),
             ("Tow_Catwalk", "X"), ("Tow_CabFloor", "X"), ("Tow_Rail", "Z"),
             ("Tow_Cab", "Z"), ("Tow_Glass", "Z"), ("Tow_Roof", "X"),
             ("Tow_Flue", "Z"), ("Tow_Ladder", "Z")]


def _mat(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    return m


def materials(col):
    """Boyali kabin / yipranmis tasiyici / padavra cati / izgara / cam.

    Kule ormandaki TEK boyali yapi. Boya dokuldugu icin altindan ahsap cikar;
    bu, yapinin bakimli olup terk edildigini tek bakista soyler. Boyanin
    dokulmesi dipte ve kenarda baslar, tipki pasin basladigi yerlerde.
    """
    paint, timber, roof, metal, glass = (_mat("Tow_Paint"), _mat("Tow_Timber"),
                                         _mat("Tow_Roof"), _mat("Tow_Metal"),
                                         _mat("Tow_Glass"))
    # Solmus yesil boya: yangin kulelerinin standart rengi. Ormandaki tek
    # boyali yuzey oldugu icin kulenin kimligini bu tasiyor; grize birakildi
    # ve yapi diger dortlunun icinde kayboldu.
    K.build_wood(paint, "distressed_painted_planks", tiling=1.30, grain="V",
                 sat=0.78, val=0.92, base_tint=(0.60, 0.685, 0.585),
                 damp=(5.20, 6.10), damp_color=(0.150, 0.155, 0.130), damp_amt=0.50,
                 sun=(7.40, 5.90), sun_color=(0.395, 0.405, 0.375), sun_amt=0.44,
                 var=(0.14, 2.4))
    K.build_wood(timber, "weathered_peeling_timber", tiling=1.55, grain="V",
                 sat=0.66, val=0.74,
                 damp=(0.85, 2.10), damp_color=(0.140, 0.118, 0.096), damp_amt=0.64,
                 sun=(4.60, 1.90), sun_color=(0.345, 0.340, 0.330), sun_amt=0.40,
                 var=(0.15, 2.2))
    # Koyu cati / acik kabin: kulenin okunakli silueti bu karsitliktan geliyor.
    K.build_wood(roof, "dark_planks", tiling=1.25, grain="U", sat=0.68, val=0.62,
                 damp=(7.10, 7.70), damp_color=(0.105, 0.098, 0.088), damp_amt=0.40,
                 sun=(8.10, 7.10), sun_color=(0.235, 0.236, 0.232), sun_amt=0.46,
                 var=(0.16, 3.0))
    K.build_metal(metal, "metal_grate_rusty", tiling=2.30, grain="V", metallic=0.34,
                  base_tint=(0.60, 0.60, 0.60),
                  rust=(5.40, 3.20), rust_color=(0.38, 0.170, 0.075), rust_amt=0.66)

    # cam: doku yok, saf saydam
    nt = glass.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    b = nt.nodes.new("ShaderNodeBsdfPrincipled")
    b.location = (-300, 0)
    b.inputs["Base Color"].default_value = (0.82, 0.86, 0.87, 1)
    b.inputs["Roughness"].default_value = 0.06
    b.inputs["Transmission Weight"].default_value = 0.94
    b.inputs["IOR"].default_value = 1.45
    nt.links.new(b.outputs["BSDF"], out.inputs["Surface"])

    grain = {paint.name: "V", timber.name: "V", roof.name: "U",
             metal.name: "V", glass.name: "U"}
    for o in col.objects:
        n = o.name
        m = (glass if n.startswith(GLASS_PARTS) else
             metal if n.startswith(METAL_PARTS) else
             roof if n.startswith(ROOF_PARTS) else
             paint if n.startswith(PAINT_PARTS) else timber)
        o.data.materials.clear()
        o.data.materials.append(m)
        K.cube_uv(o, 2.0)
        K.align_grain(o, next((a for p, a in LONG_AXIS if n.startswith(p)), "Z"),
                      grain[m.name])
    return paint, timber, roof, metal, glass
