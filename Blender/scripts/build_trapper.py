# Avci Siginagi -- A-frame tuzakci barinagi.
#
# Ana siginaktan ayrilan taraf: cati duzlemi zemine kadar iner, dusey duvar
# yoktur. Silueti saf ucgen; tek egimli ana siginakla karistirilamaz.
#
# Malzeme kontrasti yapinin kendisinden gelir: cati yarma sindiri (duz, ince
# lamalar), kalkan duvar yatay yuvarlak tomruk (kalin, silindirik). Ikisi ayni
# dokuyla kaplansa yapi tek parca bir yuzey gibi okunuyordu.
#
# Olcu zinciri (her biri bir oncekini zorunlu kildi):
#   - Kapi acikligi >= 1.75 m olmali, yoksa oyuncu giremez.
#   - Kalkan ucgeninin o yukseklikteki genisligi kapiyi + yatak payini almali.
#   - Bu da tabani 4.60 m'ye, mahyayi 3.80 m'ye cikardi. Egim 55.4 derece:
#     kar atmasi icin fazlasiyla dik, kapi icin yeterince genis.

import math
import random
import bpy
from mathutils import Matrix

import outpost_kit as K

# ---------------------------------------------------------------- olculer
XH = 2.30          # sacak yari genisligi
DEPTH = 3.60       # derinlik (Y)
H = 3.80           # mahya yuksekligi
ZE = 0.46          # sacak yuksekligi

L = math.hypot(XH, H - ZE)
UX, UZ = -XH / L, (H - ZE) / L     # sacaktan mahyaya birim yon
NX, NZ = UZ, -UX                   # dis normal
C = XH * NX + ZE * NZ              # duzlem sabiti
SLOPE = (H - ZE) / XH

TH_SHAKE = 0.022                   # sindiri kalinligi; dis kabuk -TH'de
D_PURLIN, D_RAFTER = 0.077, 0.207
# Kalkan tomruklarinin yaricapi ADIMDAN buyuk: 12-gen silindirin duz kenari
# yaricapa cos(15) kadar yaklasir, r = adim/2 verilince aralarindan isik
# siziyordu (olculdu: 9.5 mm acik).
R_SILL, R_JOIST, R_RAFT, R_PUR, R_GLOG = 0.145, 0.08, 0.075, 0.055, 0.1040

ZFLOOR = 0.27                      # doseme ust yuzu
GLOG0, GLOG_STEP, GLOG_N = 0.3675, 0.195, 15
DOOR_LINTEL = 9                    # bu seviyedeki tomruk kapi lentosu
DOOR_X, JAMB_W = 0.45, 0.12
DOOR_TOP = GLOG0 + DOOR_LINTEL * GLOG_STEP - R_GLOG
ZR_TOP, ZR_BOT = (C - TH_SHAKE) / NZ - 0.003, 2.90
FX, FY, FR = 0.311705, 2.60, 0.07  # soba borusu, mahyaya yakin cikar


def surf_dist(v):
    """Cati kabugundan dik isaretli uzaklik; disarisi arti.

    Kabuk sindirinin DIS yuzeyi: kalin dip referans duzlemin TH_SHAKE kadar
    disinda durur, o yuzden sabit kaydirilir.
    """
    return abs(v.x) * NX + v.z * NZ - C - TH_SHAKE


def at(side, t, d):
    """Sacaktan t kadar yukari, kabuktan d kadar iceri olan nokta (x, z)."""
    return (side * (XH + UX * t) - side * NX * d, ZE + UZ * t - NZ * d)


def gable_top(x):
    """Mertek alt yuzunun o x'teki z'si -- kalkan tomruklarinin ucu buraya biter.

    Turetim: duzlem uzerindeki noktadan d kadar dik iceri gidince, ayni x'te
    olculen dusey dusus d*L/XH olur.
    """
    d = D_RAFTER + R_RAFT
    return H - SLOPE * abs(x) - d * L / XH


def d_off():
    return (D_RAFTER + R_RAFT) * L / XH


def build(col):
    for o in list(col.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    random.seed(11)

    # --- taban tomruklari. Enine olanlar boyunalara GECMELI girer: cokgen
    # silindir yaricapa tam ulasmadigi icin uc uca koymak 7 mm bosluk birakiyor.
    xs = at(1, 0, D_RAFTER)[0]
    K.log("Trap_Sill_R", (xs, 0, R_SILL), (xs, DEPTH, R_SILL), R_SILL, 10, col)
    K.log("Trap_Sill_L", (-xs, 0, R_SILL), (-xs, DEPTH, R_SILL), R_SILL, 10, col)
    xin = xs - 0.105
    for tag, y in (("F", R_SILL), ("B", DEPTH - R_SILL)):
        K.log("Trap_Sill_%s" % tag, (-xin, y, R_SILL), (xin, y, R_SILL), R_SILL, 10, col)

    # --- doseme: kirisler yan tomruklarin USTUNE degil ARASINA girer. Ustune
    # koymak dosemeyi mertek ayaginin hizasina cikariyor ve carpisiyordu.
    for i, y in enumerate((0.95, 1.80, 2.65)):
        K.log("Trap_Joist_%d" % i, (-1.99, y, R_SILL), (1.99, y, R_SILL), R_JOIST, 8, col)
    for i in range(18):
        K.box("Trap_Deck_%02d" % i, (-1.87 + i * 0.22, 1.80, ZFLOOR - 0.0225),
              (0.215, 3.02, 0.045), col)

    # --- mahya tahtasi + mertek + asik
    K.box("Trap_RidgeBoard", (0, DEPTH * 0.5, (ZR_TOP + ZR_BOT) * 0.5),
          (0.05, DEPTH - 0.55, ZR_TOP - ZR_BOT), col)
    t_top = (xs - 0.025) / -UX
    for s, tag in ((1, "R"), (-1, "L")):
        x0, z0 = at(s, 0, D_RAFTER)
        x1, z1 = at(s, t_top, D_RAFTER)
        for i, y in enumerate((0.27, 1.10, 1.90, 2.70, DEPTH - 0.27)):
            K.log("Trap_Rafter_%s%d" % (tag, i), (x0, y, z0), (x1, y, z1), R_RAFT, 8, col)
        for i, t in enumerate((0.75, 1.85, 2.95)):
            px, pz = at(s, t, D_PURLIN)
            K.log("Trap_Purlin_%s%d" % (tag, i), (px, 0, pz), (px, DEPTH, pz), R_PUR, 8, col)

    # --- sindiri: 8 sira. Her lama yarma sindirinin gercek kesitini tasir:
    # dip ucu kalin (2*TH), tepe ucu ince (TH). Dis yuzey dipte -TH, tepede 0
    # oldugundan bir ustteki siranin kalin dibi bunun ince ucunun UZERINE biner.
    # Su disarida kalir ve her sirada golge cizgisi olusur. Donusumlu derinlik
    # denendi ve yanlisti: ust sira alt siranin ALTINA giriyordu.
    t0, te = -0.09, L
    e = (te - t0) / 8.0
    lap = 0.12
    ny, y0, yw = 15, -0.06, 0.248
    parts = []
    for s in (1, -1):
        for i in range(8):
            ta = t0 + i * e
            tb = min(ta + e + lap, te)
            for j in range(ny):
                jt = random.uniform(-0.016, 0.016)     # yarma sindiri duz kesilmez
                pts = [at(s, ta + jt, -TH_SHAKE), at(s, tb, 0.0),
                       at(s, tb, TH_SHAKE), at(s, ta + jt, TH_SHAKE)]
                parts.append(K.prism("sh", pts, y0 + j * yw + 0.002,
                                     y0 + (j + 1) * yw - 0.002, col))
        # Mahya bindirmesi de karsi tarafa tasar, yoksa tepede ayni acik kalir.
        pts = [at(s, te - 0.34, -TH_SHAKE), at(s, te + 0.05, -TH_SHAKE),
               at(s, te + 0.05, 0.0), at(s, te - 0.34, TH_SHAKE)]
        parts.append(K.prism("cap", pts, y0, y0 + ny * yw, col))
    K.join("Trap_Shakes", parts, col)

    # --- kaplama tahtasi: sindirinin altina surekli bir katman. Olmadan
    # lamalar arasindaki derzler saf siyah bosluk gosteriyor ve cati cizilmis
    # gibi okunuyordu.
    sheath = []
    for s in (1, -1):
        pts = [at(s, t0, TH_SHAKE), at(s, te, TH_SHAKE),
               at(s, te, TH_SHAKE + 0.020), at(s, t0, TH_SHAKE + 0.020)]
        # Uc yuzleri sindirininkiyle ayni duzleme dusmesin diye 4 mm iceride.
        sheath.append(K.prism("sk", pts, y0 + 0.004, y0 + ny * yw - 0.004, col))
    K.join("Trap_Sheathing", sheath, col)

    # --- kalkan kenar tahtasi: sindirinin uc kesitini kapatir
    # Tepede te'de kesilirse iki tahta arasinda 2*NX*TH kadar (36 mm) acik
    # kaliyor ve mahya boyunca siyah bir kama gorunuyor. Karsi tarafa 60 mm
    # tasirilip kesistiriliyor.
    bg = []
    for s in (1, -1):
        pts = [at(s, t0, -TH_SHAKE), at(s, te + 0.06, -TH_SHAKE),
               at(s, te + 0.06, 0.10), at(s, t0, 0.10)]
        for ya, yb in ((y0 - 0.03, y0), (y0 + ny * yw, y0 + ny * yw + 0.03)):
            bg.append(K.prism("bg", pts, ya, yb, col))
    K.join("Trap_Barge", bg, col)

    # --- kalkan duvar: yatay yuvarlak tomruk dolgu.
    # Kapinin altindaki siralar sovelere dayanip kesilir; lento seviyesindeki
    # tomruk tam boy gecer ve yuku tasir.
    jamb_out = DOOR_X + JAMB_W
    for tag, ycen, split in (("F", R_GLOG, True), ("B", DEPTH - R_GLOG, False)):
        segs = []
        for k in range(GLOG_N):
            zc = GLOG0 + k * GLOG_STEP
            xw = (H - d_off() - (zc + R_GLOG)) / SLOPE
            if xw < 0.12:
                continue
            if split and k < DOOR_LINTEL:
                for s in (1, -1):
                    if xw - jamb_out < 0.06:
                        continue
                    segs.append(K.log("gl", (s * jamb_out, ycen, zc),
                                      (s * xw, ycen, zc), R_GLOG, 12, col))
            else:
                segs.append(K.log("gl", (-xw, ycen, zc), (xw, ycen, zc), R_GLOG, 12, col))
        K.join("Trap_Gable%s" % tag, segs, col)

    # --- rake kusagi: mertegin ic yuzunde, kalkan boyunca. Yuvarlak tomrugun
    # ucu dik kesildigi icin 55 derecelik rakiyla arasinda ucgen bosluk kaliyor
    # ve disari isik siziyordu. Tomruk uclari bu kusaga dayanir.
    dk = D_RAFTER + R_RAFT
    for tag, ya in (("F", 0.0), ("B", DEPTH - 2 * R_GLOG)):
        pl = []
        for s in (1, -1):
            pts = [at(s, 0.05, dk - 0.005), at(s, t_top, dk - 0.005),
                   at(s, t_top, dk + 0.085), at(s, 0.05, dk + 0.085)]
            # 12 mm iceride: tomruk ve sove yuzleriyle ayni duzleme dusmesin.
            pl.append(K.prism("rk", pts, ya + 0.012, ya + 2 * R_GLOG - 0.012, col))
        K.join("Trap_RakePlate%s" % tag, pl, col)

    # --- kapi sovesi ve kanadi
    for s in (1, -1):
        K.box("Trap_DoorJamb_%s" % ("R" if s > 0 else "L"),
              (s * (DOOR_X + JAMB_W * 0.5), R_GLOG, (ZFLOOR + DOOR_TOP) * 0.5),
              (JAMB_W, 2 * R_GLOG, DOOR_TOP - ZFLOOR), col)
    # Kanat tek levha degil, dort dikey tahta: uzaktan bile derzler kanadi
    # kalkan tomrugundan ayirir.
    w, ht = 2 * DOOR_X - 0.03, DOOR_TOP - ZFLOOR - 0.015
    pw = w / 4.0
    planks = [K.box("dp", (-DOOR_X + 0.015 + pw * (i + 0.5),
                           2 * R_GLOG + 0.023, (ZFLOOR + DOOR_TOP) * 0.5),
                    (pw - 0.006, 0.045, ht), col) for i in range(4)]
    leaf = K.join("Trap_DoorLeaf", planks, col)
    # join, ilk parcanin originini korur; origini mentese eksenine tasi.
    leaf.data.transform(Matrix.Translation((pw * 0.5, 0, 0)))
    leaf.location = (-DOOR_X + 0.015, 2 * R_GLOG + 0.023, (ZFLOOR + DOOR_TOP) * 0.5)
    # Kusakli-capraz kapi. K.box/K.log DUNYA koordinatinda uretir; kanat
    # donmemis oldugu icin yerel nokta = mentese + yerel offset. Bunu atlayip
    # yerel koordinati dogrudan vermek donanimi binanin disina birakiyordu.
    hx, hy, hz = leaf.location
    br = []
    for zc in (-ht * 0.34, ht * 0.34):
        br.append(K.box("br", (hx + w * 0.5, hy + 0.040, hz + zc),
                        (w - 0.02, 0.035, 0.13), col))
    dia = K.box("br", (0, 0, 0), (math.hypot(w, ht * 0.68) - 0.04, 0.033, 0.11), col)
    dia.data.transform(Matrix.Rotation(-math.atan2(ht * 0.68, w), 4, "Y"))
    dia.location = (hx + w * 0.5, hy + 0.040, hz)
    br.append(dia)
    brace = K.join("Trap_DoorBrace", br, col)
    brace.parent = leaf
    brace.matrix_parent_inverse = leaf.matrix_world.inverted()
    K.log("Trap_DoorHandle", (hx + w * 0.82, hy + 0.026, hz),
          (hx + w * 0.82, hy + 0.086, hz), 0.022, 8, col)
    h = col.objects["Trap_DoorHandle"]
    h.parent = leaf
    h.matrix_parent_inverse = leaf.matrix_world.inverted()

    # --- soba borusu: catidan mahyaya yakin cikar.
    # Bacalik kurali: 3 m yatay mesafedeki her catidan >= 0.6 m yukarida,
    # gectigi noktadan >= 0.9 m yukarida.
    K.log("Trap_Flue_Riser", (FX, FY, 2.05), (FX, FY, 4.46), FR, 12, col)
    K.log("Trap_Flue_Cap", (FX, FY, 4.455), (FX, FY, 4.52), 0.115, 12, col)
    # Bacalik etegi: catiya yatik, ince sac. Kalin bir levha catinin uzerinde
    # kutu gibi duruyordu.
    # Sindirinin USTUNE biner: dis yuzeyi tam ayni duzleme koymak z-fighting
    # yapiyordu (olculdu: 0.0044 m2 cakisik yuzey).
    fa = at(1, L - 0.55 - 0.30, -TH_SHAKE - 0.004)
    fb = at(1, L - 0.55 + 0.22, -TH_SHAKE - 0.004)
    K.prism("Trap_Flue_Flash",
            [fa, fb, (fb[0] - NX * 0.012, fb[1] - NZ * 0.012),
             (fa[0] - NX * 0.012, fa[1] - NZ * 0.012)],
            FY - 0.22, FY + 0.22, col)

    return dict(envelope=(-2.379, 2.379, -0.095, 3.695),
                door=(2 * DOOR_X, DOOR_TOP - ZFLOOR))


# ---------------------------------------------------------------- malzeme

BARK_PARTS = ("Trap_Sill", "Trap_Joist", "Trap_Rafter", "Trap_Purlin", "Trap_Gable")
IRON_PARTS = ("Trap_Flue", "Trap_DoorHandle")
LONG_AXIS = [
    ("Trap_Sill_R", "Y"), ("Trap_Sill_L", "Y"), ("Trap_Sill_F", "X"),
    ("Trap_Sill_B", "X"), ("Trap_Joist", "X"), ("Trap_Rafter", "Z"),
    ("Trap_Purlin", "Y"), ("Trap_Deck", "Y"), ("Trap_Shakes", "Z"),
    ("Trap_Sheathing", "Z"), ("Trap_Barge", "Z"), ("Trap_Gable", "X"),
    ("Trap_RakePlate", "Z"), ("Trap_RidgeBoard", "Y"), ("Trap_DoorJamb", "Z"),
    ("Trap_DoorLeaf", "Z"), ("Trap_DoorBrace", "X"), ("Trap_DoorHandle", "Y"),
    ("Trap_Flue_Riser", "Z"), ("Trap_Flue_Cap", "Z"), ("Trap_Flue_Flash", "X"),
]


def _mat(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    return m


def materials(col):
    """Uc malzeme kurar, atar ve UV'yi damar yonune oturtur.

    Kontrast yapinin kendisinden gelmeli: tomruklar sicak ve koyu, cati
    ruzgarla gri dusmus. Ikisi ayni tona getirildiginde yapi tek bloka
    donusuyor ve siluet disinda hicbir sey okunmuyor.
    """
    bark, timber, iron = _mat("Trap_Bark"), _mat("Trap_Timber"), _mat("Trap_Iron")
    K.build_wood(bark, "pine_bark", tiling=1.8, grain="V", sat=0.68, val=0.72,
                 damp=(0.55, 1.25), damp_color=(0.150, 0.126, 0.106), damp_amt=0.60,
                 sun=(3.40, 2.00), sun_color=(0.30, 0.295, 0.285), sun_amt=0.22,
                 var=(0.13, 1.9))
    K.build_wood(timber, "rough_wood", tiling=1.67, grain="V", sat=0.66, val=1.00,
                 damp=(0.70, 1.55), damp_color=(0.160, 0.136, 0.114), damp_amt=0.50,
                 sun=(3.65, 1.50), sun_color=(0.395, 0.392, 0.384), sun_amt=0.54,
                 var=(0.17, 3.4))
    K.build_metal(iron, "rusty_metal_02", tiling=3.0, grain="U", metallic=0.22,
                  base_tint=(0.40, 0.38, 0.36),
                  rust=(3.95, 4.45), rust_color=(0.34, 0.155, 0.070), rust_amt=0.62)

    grain = {bark.name: "V", timber.name: "V", iron.name: "U"}
    for o in col.objects:
        n = o.name
        m = iron if n.startswith(IRON_PARTS) else bark if n.startswith(BARK_PARTS) else timber
        o.data.materials.clear()
        o.data.materials.append(m)
        K.cube_uv(o, 2.0)
        K.align_grain(o, next((a for p, a in LONG_AXIS if n.startswith(p)), "Z"), grain[m.name])
    return bark, timber, iron
