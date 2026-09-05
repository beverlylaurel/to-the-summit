# Ortak uretim kiti: orman karakollari (outposts).
#
# Cabin_Refuge'de olculerek oturmus kurallari tek yerde toplar. Yeni kabinler
# bu dosyayi import eder; malzeme zinciri, UV kurallari ve denetim tek kaynaktan
# gelir, her kabinde yeniden yazilmaz.
#
# Kullanim (Blender Python konsolundan veya MCP'den):
#     import sys; sys.path.append(SCRIPTS); import outpost_kit as K
#     K.build_wood(mat, "pine_bark", tiling=1.2, grain='U')
#     K.audit(bpy.data.collections['Outpost'])

import math
import os
import itertools
import bpy
import bmesh
from mathutils import Matrix, Vector

# ---------------------------------------------------------------- yollar

_HERE = os.path.dirname(os.path.abspath(__file__))
BLENDER_ROOT = os.path.dirname(_HERE)
TEX_OUTPOST = os.path.join(BLENDER_ROOT, "outposts", "textures")
TEX_REFUGE = os.path.join(BLENDER_ROOT, "refuge", "textures")
BAKE_OUTPOST = os.path.join(BLENDER_ROOT, "outposts", "bake")


# ---------------------------------------------------------------- renk uzayi
#
# TUZAK: image.pixels 8-bit JPEG'de sRGB dondurur, shader renk soketleri ise
# lineer bekler. Yipranma rengini dokudan turetirken donusturmeden yazmak,
# koyulastirma katmanini acik hale getirir. Cabin_Refuge'de uc tur kaybettirdi.

def s2l(c):
    """sRGB bileseni -> lineer."""
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def s2l3(rgb):
    return tuple(s2l(c) for c in rgb) + (1.0,)


# ---------------------------------------------------------------- doku yukleme

_SUFFIX = {
    "diff": ("_diff_", "_diffuse", "_Diffuse", "_albedo", "_col"),
    "nor": ("_nor_gl", "_nor_dx", "_normal"),
    "rough": ("_rough", "_Rough"),
    "arm": ("_arm",),
    "metal": ("_metal", "_Metal"),
}


def find_map(asset, kind, roots=None):
    """asset adina ait <kind> dokusunun tam yolunu bulur; yoksa None."""
    roots = roots or (TEX_OUTPOST, TEX_REFUGE)
    # Once varligin kendi klasoru: "rock_wall" ile "rock_wall_03" ayni onekle
    # basladigi icin duz tarama yanlis dosyayi secebilir.
    scan = [os.path.join(r, asset) for r in roots] + list(roots)
    for root in scan:
        if not os.path.isdir(root):
            continue
        for dirpath, _dirs, files in os.walk(root):
            for f in sorted(files):
                low = f.lower()
                if not low.startswith(asset.lower()):
                    continue
                if not low.endswith((".jpg", ".png", ".exr")):
                    continue
                for suf in _SUFFIX[kind]:
                    if suf.lower() in low:
                        return os.path.join(dirpath, f)
    return None


def load_img(path, non_color):
    """Ayni dosyayi ikinci kez yuklemez; renk uzayini zorlar."""
    name = os.path.basename(path)
    img = bpy.data.images.get(name)
    if img is None:
        img = bpy.data.images.load(path, check_existing=True)
        img.name = name
    img.colorspace_settings.name = "Non-Color" if non_color else "sRGB"
    return img


# ---------------------------------------------------------------- damar olcumu
#
# Her dokunun damar ekseni farkli. Tahmin etmek yerine olculur: parlaklik
# gradyaninin U ve V yonundeki ortalama buyuklugu karsilastirilir. Damar
# hangi eksende uzaniyorsa o eksende gradyan KUCUK olur.

def measure_grain(img, step=8):
    w, h = img.size
    px = list(img.pixels)
    ch = img.channels

    def lum(x, y):
        i = (y * w + x) * ch
        return 0.2126 * px[i] + 0.7152 * px[i + 1] + 0.0722 * px[i + 2]

    du = dv = 0.0
    n = 0
    for y in range(0, h - step, step):
        for x in range(0, w - step, step):
            c = lum(x, y)
            du += abs(lum(x + step, y) - c)
            dv += abs(lum(x, y + step) - c)
            n += 1
    du /= max(n, 1)
    dv /= max(n, 1)
    return ("U" if du < dv else "V"), du, dv


# ---------------------------------------------------------------- UV
#
# Dunya kilitli kup izdusum: doku olcegi parcanin boyutuna degil dunyaya
# baglidir, boylece kalin kiris ile ince cita ayni tahta genisligini gosterir.

def cube_uv(obj, size=2.0):
    # bpy.ops KULLANILMIYOR. Operatorle yapmak edit moduna girmeyi ve dogru
    # baglami kurmayi gerektiriyor; taze dosyada ve toplu uretimde surekli
    # poll() hatasi veriyordu. Izdusum zaten uc satirlik bir hesap.
    #
    # Eksen eslemesi align_grain ile AYNI olmak zorunda:
    #   normal X -> (u, v) = (y, z)
    #   normal Y -> (u, v) = (x, z)
    #   normal Z -> (u, v) = (x, y)
    # DUNYA koordinati kullanilir, yerel degil: yerelde her parcanin agi kendi
    # merkezine gore 0 civarinda oldugu icin butun parcalar dokunun AYNI
    # bolgesini orneklerdi. Dunya kilidi hem tekrari kirar hem de kalin kiris
    # ile ince citanin ayni tahta genisligini gostermesini saglar.
    bpy.context.view_layer.update()
    me = obj.data
    mw = obj.matrix_world
    n3 = mw.to_3x3()
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")
    uv = me.uv_layers.active.data
    inv = 1.0 / size
    for p in me.polygons:
        n = (n3 @ p.normal).normalized()
        nax = max(range(3), key=lambda i: abs(n[i]))
        a, b = ((1, 2), (0, 2), (0, 1))[nax]
        for li in p.loop_indices:
            co = mw @ me.vertices[me.loops[li].vertex_index].co
            uv[li].uv = (co[a] * inv, co[b] * inv)


def align_grain(obj, long_axis, grain):
    """Dokunun damarini parcanin uzun eksenine oturtur, YUZ YUZ.

    cube_project her yuze baskin normal eksenine gore UV verir:
        normal X -> (U, V) = (Y, Z)
        normal Y -> (U, V) = (X, Z)
        normal Z -> (U, V) = (X, Y)
    Damar hangi UV ekseninde uzaniyorsa (grain), o eksenin dunyada karsiligi
    parcanin uzun ekseni degilse o yuzun UV'si 90 derece cevrilir. Tek bir
    global donusum yetmez: ayni parcanin yan yuzu ile ust yuzu farkli eslesir.
    """
    axmap = {"X": ("Y", "Z"), "Y": ("X", "Z"), "Z": ("X", "Y")}
    long_axis = long_axis.upper()
    gi = 1 if grain.upper() == "V" else 0
    me = obj.data
    if not me.uv_layers:
        return
    n3 = obj.matrix_world.to_3x3()
    uv = me.uv_layers.active.data
    for p in me.polygons:
        n = (n3 @ p.normal).normalized()      # dunya normali: donuk parcalarda
        nax = "XYZ"[max(range(3), key=lambda i: abs(n[i]))]
        if nax == long_axis:
            continue                      # uc kapagi: damar zaten gorunmez
        if axmap[nax][gi] == long_axis:
            continue                      # zaten dogru
        for li in p.loop_indices:
            u, v = uv[li].uv
            uv[li].uv = (-v, u)


def rotate_uv_on_axis(obj, axis, quarter=1):
    """Normali <axis> yonunde olan yuzlerin UV'sini 90 derece cevirir.

    Doku kendi tahta derzini tasiyor: derz parcanin uzun ekseniyle ayni yone
    bakmazsa yuzey karo gibi okunur. Deck tahtalarinda bu hatayla karsilasildi.
    """
    idx = {"X": 0, "Y": 1, "Z": 2}[axis.upper()]
    me = obj.data
    uv = me.uv_layers.active.data
    for p in me.polygons:
        if abs(p.normal[idx]) < 0.8:
            continue
        for li in p.loop_indices:
            u, v = uv[li].uv
            for _ in range(quarter % 4):
                u, v = -v, u
            uv[li].uv = (u, v)


# ---------------------------------------------------------------- malzemeler
#
# Zincir (Cabin_Refuge'de olculdu, birebir korunuyor):
#   doseme -> DET (ayni olcekte kaydirilmis ikinci ornek, gurultu maskesi)
#          -> VAR (konum gurultusu ile hafif ton dalgalanmasi)
#          -> DAMP (dipte nem/sicrama: koyulastirir + purузlastirir)
#          -> SUN  (yukarida ruzgar/gunes agartmasi: aciklastirir)
#
# DET kurali: ikinci ornek AYNI olcekte, yalniz damar boyunca kaydirilir.
# Olcek degistirmek tahta genisligini gorunur bir sinirda degistirir.
#
# VAR kurali: nesne bazli rastgele ton KULLANILMAZ. Panel sinirlarinda sert
# dikey basamak yapiyordu (olculdu: %9.6). Gurultu Geometry->Position'dan
# beslenir; komsu panelde surekli kalir.

DETILE_OFFSET = 4.73


def _nodes_reset(mat):
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (1500, 0)
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (1200, 0)
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return nt, bsdf


def _tiling_chain(nt, bsdf, asset, tiling, grain, roots=None):
    """Doseme + detile + normal + puruz. (renk_soketi, puruz_soketi) doner."""
    L = nt.links
    diff = find_map(asset, "diff", roots)
    if diff is None:
        raise RuntimeError("albedo bulunamadi: %s" % asset)
    nor = find_map(asset, "nor", roots)
    rgh = find_map(asset, "rough", roots) or find_map(asset, "arm", roots)

    tc = nt.nodes.new("ShaderNodeTexCoord")
    tc.name = "TC"
    tc.location = (-1200, 0)

    mp = nt.nodes.new("ShaderNodeMapping")
    mp.name = "MAP"
    mp.location = (-1000, 0)
    mp.inputs["Scale"].default_value = (tiling, tiling, tiling)
    L.new(tc.outputs["UV"], mp.inputs["Vector"])

    img = nt.nodes.new("ShaderNodeTexImage")
    img.name = "BASE_img"
    img.image = load_img(diff, False)
    img.location = (-800, 200)
    L.new(mp.outputs["Vector"], img.inputs["Vector"])

    # --- detile: ayni olcek, yalniz damar boyunca kayma
    off = (DETILE_OFFSET, 0.0, 0.0) if grain.upper() == "U" else (0.0, DETILE_OFFSET, 0.0)
    dmap = nt.nodes.new("ShaderNodeMapping")
    dmap.name = "DET_map"
    dmap.location = (-1000, -260)
    dmap.inputs["Scale"].default_value = (tiling, tiling, tiling)
    dmap.inputs["Location"].default_value = off
    L.new(tc.outputs["UV"], dmap.inputs["Vector"])

    dimg = nt.nodes.new("ShaderNodeTexImage")
    dimg.name = "DET_img"
    dimg.image = img.image
    dimg.location = (-800, -260)
    L.new(dmap.outputs["Vector"], dimg.inputs["Vector"])

    dnz = nt.nodes.new("ShaderNodeTexNoise")
    dnz.name = "DET_noise"
    dnz.location = (-1000, -520)
    dnz.inputs["Scale"].default_value = 1.15
    dnz.inputs["Detail"].default_value = 3.0
    dnz.inputs["Roughness"].default_value = 0.55
    L.new(tc.outputs["Object"], dnz.inputs["Vector"])

    drp = nt.nodes.new("ShaderNodeValToRGB")
    drp.name = "DET_ramp"
    drp.location = (-800, -520)
    drp.color_ramp.elements[0].position = 0.14
    drp.color_ramp.elements[1].position = 0.86
    L.new(dnz.outputs["Factor"], drp.inputs["Factor"])

    dmix = nt.nodes.new("ShaderNodeMix")
    dmix.name = "DET_mix"
    dmix.data_type = "RGBA"
    dmix.blend_type = "MIX"
    dmix.location = (-520, 0)
    L.new(drp.outputs["Color"], dmix.inputs["Factor"])
    L.new(img.outputs["Color"], dmix.inputs[6])
    L.new(dimg.outputs["Color"], dmix.inputs[7])

    if nor:
        nimg = nt.nodes.new("ShaderNodeTexImage")
        nimg.name = "NRM_img"
        nimg.image = load_img(nor, True)
        nimg.location = (-800, 460)
        L.new(mp.outputs["Vector"], nimg.inputs["Vector"])
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nm.name = "NRM"
        nm.location = (-520, 460)
        L.new(nimg.outputs["Color"], nm.inputs["Color"])
        L.new(nm.outputs["Normal"], bsdf.inputs["Normal"])

    rough_socket = None
    if rgh:
        rimg = nt.nodes.new("ShaderNodeTexImage")
        rimg.name = "RGH_img"
        rimg.image = load_img(rgh, True)
        rimg.location = (-800, -760)
        L.new(mp.outputs["Vector"], rimg.inputs["Vector"])
        if "_arm" in os.path.basename(rgh).lower():
            sep = nt.nodes.new("ShaderNodeSeparateColor")
            sep.name = "RGH_sep"
            sep.location = (-600, -760)
            L.new(rimg.outputs["Color"], sep.inputs["Color"])
            rough_socket = sep.outputs["Green"]
        else:
            rough_socket = rimg.outputs["Color"]

    return dmix.outputs["Result"], rough_socket


def _var_layer(nt, color_in, amount=0.15, scale=0.28):
    """Konum gurultusuyle ton dalgalanmasi. Nesne bazli rastgele YOK.

    scale, gurultunun dunya frekansi: 0.28 tum yapiyi kapsayan yavas dalga,
    3.5 tek bir sindiri/tahta genisligi. Ikincisi her lamanin ayri yaslanmasini
    verir. Nesne bazli rastgele ton yerine konum gurultusu kullanilir; ilki
    panel sinirlarinda sert dikey basamak yapiyordu (olculdu: %9.6).
    """
    L = nt.links
    pos = nt.nodes.new("ShaderNodeNewGeometry")
    pos.name = "VAR_pos"
    pos.location = (-520, -1000)
    nz = nt.nodes.new("ShaderNodeTexNoise")
    nz.name = "VAR_noise"
    nz.location = (-340, -1000)
    nz.inputs["Scale"].default_value = scale
    nz.inputs["Detail"].default_value = 5.0
    nz.inputs["Roughness"].default_value = 0.62
    L.new(pos.outputs["Position"], nz.inputs["Vector"])
    r = nt.nodes.new("ShaderNodeMapRange")
    r.name = "VAR_r"
    r.location = (-160, -1000)
    r.inputs["To Min"].default_value = 1.0 - amount
    r.inputs["To Max"].default_value = 1.0 + amount
    L.new(nz.outputs["Factor"], r.inputs["Value"])
    mix = nt.nodes.new("ShaderNodeMix")
    mix.name = "VAR_mix"
    mix.data_type = "RGBA"
    mix.blend_type = "MULTIPLY"
    mix.location = (0, -200)
    mix.inputs["Factor"].default_value = 1.0
    L.new(color_in, mix.inputs[6])
    L.new(r.outputs["Result"], mix.inputs[7])
    return mix.outputs["Result"]


def _zone_layer(nt, color_in, rough_in, tag, z_from, z_to, color_srgb,
                noise_scale, z_squash, amount, rough_to=None, xy=(140, 0)):
    """Yukseklige bagli yipranma katmani.

    z_from -> z_to arasinda 1..0 giden bir gradyan, dikey ezilmis gurultuyle
    carpilir. z_squash < 1 ise gurultu asagi akan cizgiler halinde uzar --
    su, pas ve is gercekte boyle iz birakir: dipte/dikiste baslar, asagi akar.
    """
    L = nt.links
    x, y = xy
    pos = nt.nodes.new("ShaderNodeNewGeometry")
    pos.name = tag + "_pos"
    pos.location = (x - 700, y - 600)
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.name = tag + "_map"
    mp.location = (x - 520, y - 600)
    mp.inputs["Scale"].default_value = (1.0, 1.0, z_squash)
    L.new(pos.outputs["Position"], mp.inputs["Vector"])
    nz = nt.nodes.new("ShaderNodeTexNoise")
    nz.name = tag + "_nz"
    nz.location = (x - 340, y - 600)
    nz.inputs["Scale"].default_value = noise_scale
    nz.inputs["Detail"].default_value = 7.0
    nz.inputs["Roughness"].default_value = 0.70
    L.new(mp.outputs["Vector"], nz.inputs["Vector"])
    rp = nt.nodes.new("ShaderNodeValToRGB")
    rp.name = tag + "_rp"
    rp.location = (x - 160, y - 600)
    rp.color_ramp.elements[0].position = 0.32
    rp.color_ramp.elements[1].position = 0.68
    L.new(nz.outputs["Factor"], rp.inputs["Factor"])

    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    sep.name = tag + "_sep"
    sep.location = (x - 520, y - 840)
    L.new(pos.outputs["Position"], sep.inputs["Vector"])
    g = nt.nodes.new("ShaderNodeMapRange")
    g.name = tag + "_g"
    g.location = (x - 340, y - 840)
    g.inputs["From Min"].default_value = z_from
    g.inputs["From Max"].default_value = z_to
    g.inputs["To Min"].default_value = 1.0
    g.inputs["To Max"].default_value = 0.0
    L.new(sep.outputs["Z"], g.inputs["Value"])

    mul = nt.nodes.new("ShaderNodeMath")
    mul.name = tag + "_amt"
    mul.operation = "MULTIPLY"
    mul.location = (x, y - 700)
    mul.inputs[1].default_value = amount
    m2 = nt.nodes.new("ShaderNodeMath")
    m2.name = tag + "_gate"
    m2.operation = "MULTIPLY"
    m2.location = (x - 160, y - 780)
    L.new(rp.outputs["Color"], m2.inputs[0])
    L.new(g.outputs["Result"], m2.inputs[1])
    L.new(m2.outputs["Value"], mul.inputs[0])

    col = nt.nodes.new("ShaderNodeMix")
    col.name = tag + "_col"
    col.data_type = "RGBA"
    col.blend_type = "MIX"
    col.location = (x + 200, y)
    L.new(color_in, col.inputs[6])
    col.inputs[7].default_value = s2l3(color_srgb)
    L.new(mul.outputs["Value"], col.inputs["Factor"])

    out_rough = rough_in
    if rough_in is not None and rough_to is not None:
        rg = nt.nodes.new("ShaderNodeMix")
        rg.name = tag + "_rgh"
        rg.data_type = "FLOAT"
        rg.blend_type = "MIX"
        rg.location = (x + 200, y - 320)
        L.new(rough_in, rg.inputs[2])
        rg.inputs[3].default_value = rough_to
        L.new(mul.outputs["Value"], rg.inputs["Factor"])
        out_rough = rg.outputs["Result"]

    return col.outputs["Result"], out_rough


# --- disa acik yapicilar --------------------------------------------------

def build_wood(mat, asset, tiling=1.2, grain="U", base_tint=None,
               damp=(0.55, 1.15), damp_color=(0.19, 0.16, 0.14), damp_amt=0.55,
               sun=(2.60, 0.95), sun_color=(0.36, 0.355, 0.35), sun_amt=0.38,
               var=(0.15, 0.28), sat=1.0, val=1.0, roots=None):
    """Ahsap: dipte nem koyulugu, yukarida ruzgar agartmasi.

    damp / sun: (z_bas, z_bit) metre. Yapinin kendi yuksekligine gore verilir.
    Renkler sRGB girilir, iceride lineere cevrilir.
    """
    nt, bsdf = _nodes_reset(mat)
    col, rgh = _tiling_chain(nt, bsdf, asset, tiling, grain, roots)
    if sat != 1.0 or val != 1.0:
        # Doyum ayri ele alinir: koyulastirmak kirmiziligi azaltmaz, sadece
        # koyu kirmizi yapar. Kabuk dokusu ham haliyle tugla gibi okunuyordu.
        hs = nt.nodes.new("ShaderNodeHueSaturation")
        hs.name = "DESAT"
        hs.location = (-400, 60)
        hs.inputs["Saturation"].default_value = sat
        hs.inputs["Value"].default_value = val
        nt.links.new(col, hs.inputs["Color"])
        col = hs.outputs["Color"]
    if base_tint:
        L = nt.links
        t = nt.nodes.new("ShaderNodeMix")
        t.name = "TINT"
        t.data_type = "RGBA"
        t.blend_type = "MULTIPLY"
        t.location = (-340, -60)
        t.inputs["Factor"].default_value = 1.0
        L.new(col, t.inputs[6])
        t.inputs[7].default_value = s2l3(base_tint)
        col = t.outputs["Result"]
    col = _var_layer(nt, col, var[0], var[1])
    col, rgh = _zone_layer(nt, col, rgh, "DAMP", damp[0], damp[1], damp_color,
                           7.0, 0.16, damp_amt, rough_to=0.94, xy=(300, 0))
    col, rgh = _zone_layer(nt, col, rgh, "SUN", sun[0], sun[1], sun_color,
                           2.2, 1.0, sun_amt, xy=(760, 0))
    nt.links.new(col, bsdf.inputs["Base Color"])
    if rgh is not None:
        nt.links.new(rgh, bsdf.inputs["Roughness"])
    bsdf.inputs["Metallic"].default_value = 0.0
    return mat


def build_stone(mat, asset, tiling=1.6, grain="U", sat=1.0, val=1.0,
                wet=(0.85, 0.0), wet_color=(0.17, 0.17, 0.16), wet_amt=0.55,
                lichen=(2.2, 0.6), lichen_color=(0.36, 0.38, 0.30), lichen_amt=0.30,
                var=(0.10, 0.28), roots=None):
    """Tas: dipte islak koyuluk, ust yuzeylerde liken/kir birikimi.

    Tas ahsaptan farkli yaslanir -- gunes agartmasi yerine liken ve is tutar,
    dipte kar erimesinden kalici nem olur.
    """
    nt, bsdf = _nodes_reset(mat)
    col, rgh = _tiling_chain(nt, bsdf, asset, tiling, grain, roots)
    if sat != 1.0 or val != 1.0:
        hs = nt.nodes.new("ShaderNodeHueSaturation")
        hs.name = "DESAT"
        hs.location = (-400, 60)
        hs.inputs["Saturation"].default_value = sat
        hs.inputs["Value"].default_value = val
        nt.links.new(col, hs.inputs["Color"])
        col = hs.outputs["Color"]
    col = _var_layer(nt, col, var[0], var[1])
    col, rgh = _zone_layer(nt, col, rgh, "WET", wet[0], wet[1], wet_color,
                           5.0, 0.22, wet_amt, rough_to=0.55, xy=(300, 0))
    col, rgh = _zone_layer(nt, col, rgh, "LICH", lichen[0], lichen[1], lichen_color,
                           3.4, 1.0, lichen_amt, rough_to=0.96, xy=(760, 0))
    nt.links.new(col, bsdf.inputs["Base Color"])
    if rgh is not None:
        nt.links.new(rgh, bsdf.inputs["Roughness"])
    bsdf.inputs["Metallic"].default_value = 0.0
    return mat


def build_metal(mat, asset, tiling=2.0, grain="U", metallic=1.0, base_tint=None,
                sat=1.0, val=1.0,
                rust=(1.30, 0.0), rust_color=(0.42, 0.21, 0.10), rust_amt=0.70,
                roots=None):
    """Metal: pas dipte ve dikiste baslar, asagi akar.

    Duz yuzeyin ortasinda pas baslamaz. z_squash cok kucuk tutulur ki gurultu
    dikey akinti cizgilerine donussun. Pas metaligi dusurur -- oksit iletken
    degildir; bunu atlamak pasi parlak boya gibi gosterir.
    """
    nt, bsdf = _nodes_reset(mat)
    col, rgh = _tiling_chain(nt, bsdf, asset, tiling, grain, roots)
    if sat != 1.0 or val != 1.0:
        hs = nt.nodes.new("ShaderNodeHueSaturation")
        hs.name = "DESAT"
        hs.location = (-400, 60)
        hs.inputs["Saturation"].default_value = sat
        hs.inputs["Value"].default_value = val
        nt.links.new(col, hs.inputs["Color"])
        col = hs.outputs["Color"]
    if base_tint:
        t = nt.nodes.new("ShaderNodeMix")
        t.name = "TINT"
        t.data_type = "RGBA"
        t.blend_type = "MULTIPLY"
        t.location = (-340, -60)
        t.inputs["Factor"].default_value = 1.0
        nt.links.new(col, t.inputs[6])
        t.inputs[7].default_value = s2l3(base_tint)
        col = t.outputs["Result"]
    col = _var_layer(nt, col, amount=0.08)
    col, rgh = _zone_layer(nt, col, rgh, "RUST", rust[0], rust[1], rust_color,
                           6.0, 0.09, rust_amt, rough_to=0.92, xy=(300, 0))
    nt.links.new(col, bsdf.inputs["Base Color"])
    if rgh is not None:
        nt.links.new(rgh, bsdf.inputs["Roughness"])
    # pas metaligi yer yer sifirlar
    mfac = nt.nodes["RUST_amt"].outputs["Value"]
    mm = nt.nodes.new("ShaderNodeMix")
    mm.name = "RUST_met"
    mm.data_type = "FLOAT"
    mm.blend_type = "MIX"
    mm.location = (1000, -400)
    mm.inputs[2].default_value = metallic
    mm.inputs[3].default_value = 0.0
    nt.links.new(mfac, mm.inputs["Factor"])
    nt.links.new(mm.outputs["Result"], bsdf.inputs["Metallic"])
    return mat


def build_fabric(mat, asset, tiling=3.0, grain="U", base_tint=None,
                 dirt=(0.70, 0.0), dirt_color=(0.20, 0.18, 0.15), dirt_amt=0.60,
                 roots=None):
    """Branda/cuval: dipte camur ve kar sicramasi, yukarida solma."""
    nt, bsdf = _nodes_reset(mat)
    col, rgh = _tiling_chain(nt, bsdf, asset, tiling, grain, roots)
    if base_tint:
        t = nt.nodes.new("ShaderNodeMix")
        t.name = "TINT"
        t.data_type = "RGBA"
        t.blend_type = "MULTIPLY"
        t.location = (-340, -60)
        t.inputs["Factor"].default_value = 1.0
        nt.links.new(col, t.inputs[6])
        t.inputs[7].default_value = s2l3(base_tint)
        col = t.outputs["Result"]
    col = _var_layer(nt, col, amount=0.12)
    col, rgh = _zone_layer(nt, col, rgh, "DIRT", dirt[0], dirt[1], dirt_color,
                           8.0, 0.20, dirt_amt, rough_to=0.98, xy=(300, 0))
    nt.links.new(col, bsdf.inputs["Base Color"])
    if rgh is not None:
        nt.links.new(rgh, bsdf.inputs["Roughness"])
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Specular IOR Level"].default_value = 0.25
    return mat


# ---------------------------------------------------------------- geometri

def _finish(me, obj, col, smooth_angle):
    if col is not None:
        col.objects.link(obj)
    else:
        bpy.context.scene.collection.objects.link(obj)
    if smooth_angle:
        # Yuvarlak parcada duz golgeleme kutu gibi okunur (Cabin_Refuge'de baca
        # borusu tam bunu yapmisti). Puruzsuz golgeleme + aci esigini asan
        # kenarlarin keskin isaretlenmesi: govde yuvarlak kalir, uc kapaklari
        # keskin durur.
        import math as _m
        for p in me.polygons:
            p.use_smooth = True
        bm = bmesh.new()
        bm.from_mesh(me)
        lim = _m.radians(smooth_angle)
        for e in bm.edges:
            e.smooth = len(e.link_faces) == 2 and e.calc_face_angle(_m.pi) < lim
        bm.to_mesh(me)
        bm.free()
    return obj


def log(name, p0, p1, r, seg=8, col=None, smooth_angle=40.0):
    """p0 -> p1 arasinda yuvarlak tomruk."""
    import math
    p0 = Vector(p0)
    p1 = Vector(p1)
    d = p1 - p0
    ln = d.length
    if ln < 1e-6:
        raise ValueError("sifir uzunlukta tomruk: %s" % name)
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    # cap_tris: kapak ucgen yelpaze olur. n-gon kapak disa aktarimda zaten
    # ucgenlenir ama denetimde gurultu yapar ve UV izdusumunu bozar.
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=True, segments=seg,
                          radius1=r, radius2=r, depth=ln)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    z = Vector((0, 0, 1))
    q = z.rotation_difference(d.normalized())
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = q
    obj.location = (p0 + p1) * 0.5
    return _finish(me, obj, col, smooth_angle)


def box(name, center, size, col=None):
    """Eksene hizali kutu. size = (x, y, z) tam kenar uzunluklari."""
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    obj.location = center
    return _finish(me, obj, col, 0.0)


def prism(name, pts_xz, y0, y1, col=None, axis="Y"):
    """Kapali poligonu bir eksen boyunca supurur (kalkan duvar, kemer, kama).

    axis="Y": profil XZ duzleminde, supurme Y'de.
    axis="X": profil YZ duzleminde okunur (ilk bilesen Y), supurme X'te --
    girise dik uzanan istinat duvari gibi parcalar icin.
    """
    # Ust uste dusen ardisik nokta sifir uzunlukta kenar ve sifir alanli yuz
    # uretir; kemer profillerinde bas ve son nokta cakismasi kolayca olusuyor.
    clean = []
    for p in pts_xz:
        if not clean or (abs(p[0] - clean[-1][0]) > 1e-7 or abs(p[1] - clean[-1][1]) > 1e-7):
            clean.append(p)
    if len(clean) > 2 and abs(clean[0][0] - clean[-1][0]) < 1e-7 \
            and abs(clean[0][1] - clean[-1][1]) < 1e-7:
        clean.pop()
    if axis.upper() == "X":
        # Mesh -90 derece Z'de donduruluyor: (a, b, c) -> (b, -a, c). Profilin
        # ilk bileseni bu yuzden isaret degistirir; onceden negatiflenmezse
        # duvar ters yone bakar.
        clean = [(-p[0], p[1]) for p in clean]
    pts_xz = clean

    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    vs = [bm.verts.new((x, y0, z)) for x, z in pts_xz]
    bm.faces.new(vs)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    r = bmesh.ops.extrude_face_region(bm, geom=bm.faces[:])
    bmesh.ops.translate(bm, vec=(0, y1 - y0, 0),
                        verts=[v for v in r["geom"] if isinstance(v, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    # Dort kenardan buyuk kapaklari ucgenle: n-gon disa aktarimda ucgenlenir
    # ama once denetimde gurultu yapar ve UV izdusumunu bozar.
    big = [f for f in bm.faces if len(f.verts) > 4]
    if big:
        bmesh.ops.triangulate(bm, faces=big)
    bm.to_mesh(me)
    bm.free()
    if axis.upper() == "X":
        me.transform(Matrix.Rotation(math.radians(-90.0), 4, "Z"))
    obj = bpy.data.objects.new(name, me)
    return _finish(me, obj, col, 0.0)


def orient_outward(bm):
    """recalc_face_normals'in secimini hacimle dogrular, gerekirse cevirir.

    recalc_face_normals yuzleri yalniz BIRBIRIYLE tutarli yapar; kabuk kapali
    degilse hangi tarafin dis oldugunu tahmin eder ve yanilabilir. Ters secim
    Blender'da gorunmez -- viewport iki yuzu de cizer -- ama Unity on yuzu
    atar ve yapinin ici gorunur. Isaretli hacim bu tahmini olcer: disa donuk
    kapali bir kabuk pozitif hacim kapsar.
    """
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    volume = 0.0
    for f in bm.faces:
        vs = f.verts[:]
        for k in range(1, len(vs) - 1):
            a, b, c = vs[0].co, vs[k].co, vs[k + 1].co
            volume += a.dot(b.cross(c)) / 6.0
    if volume < 0.0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    return volume


def heightfield(name, fn, x0, x1, y0, y1, nx, ny, col=None, base=0.0,
                smooth_angle=34.0):
    """z = fn(x, y) yuzeyi + kenarlardan tabana inen etek + taban: kapali kati.

    Toprak ortulu yapilar icin. Acik birakmak manifold disi kenar birakir ve
    denetim hakli olarak isaretler.
    """
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    grid = []
    for j in range(ny + 1):
        y = y0 + (y1 - y0) * j / ny
        row = []
        for i in range(nx + 1):
            x = x0 + (x1 - x0) * i / nx
            row.append(bm.verts.new((x, y, max(fn(x, y), base))))
        grid.append(row)
    for j in range(ny):
        for i in range(nx):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))

    ring = ([grid[0][i] for i in range(nx + 1)] +
            [grid[j][nx] for j in range(1, ny + 1)] +
            [grid[ny][i] for i in range(nx - 1, -1, -1)] +
            [grid[j][0] for j in range(ny - 1, 0, -1)])
    low = [bm.verts.new((v.co.x, v.co.y, base)) for v in ring]
    n = len(ring)
    for i in range(n):
        a, b = ring[i], ring[(i + 1) % n]
        c, d = low[i], low[(i + 1) % n]
        if (a.co - b.co).length < 1e-9:
            continue
        if a.co.z - base < 1e-9 and b.co.z - base < 1e-9:
            continue          # yuzey zaten tabanda: etek yuzu sifir alanli olur
        bm.faces.new((a, b, d, c))
    bm.faces.new(low[::-1])
    # Etek, tabana oturan kenarlarda yuz atlar; kabuk delikli kalir ve
    # recalc tek basina yonu ters secebilir.
    orient_outward(bm)
    big = [f for f in bm.faces if len(f.verts) > 4]
    if big:
        bmesh.ops.triangulate(bm, faces=big)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
    dead = [f for f in bm.faces if f.calc_area() < 1e-9]
    if dead:
        bmesh.ops.delete(bm, geom=dead, context="FACES_ONLY")
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    return _finish(me, obj, col, smooth_angle)


def revolve(name, profile_rz, segments=20, col=None, a0=0.0, a1=None,
            smooth_angle=34.0):
    """Kapali (r, z) profilini Z ekseni etrafinda dondurur.

    a1 verilmezse tam tur. Kismi turda iki uc kapak eklenir -- konik bir
    barinagin kapi acikligi boyle birakilir; sonradan delik acmaya gerek kalmaz.
    """
    if a1 is None:
        a1 = a0 + 2.0 * math.pi
    full = abs((a1 - a0) - 2.0 * math.pi) < 1e-9
    n = segments
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    rings = []
    steps = n if full else n + 1
    for i in range(steps):
        a = a0 + (a1 - a0) * (i / n if full else i / n)
        ca, sa = math.cos(a), math.sin(a)
        rings.append([bm.verts.new((r * ca, r * sa, z)) for r, z in profile_rz])
    m = len(profile_rz)
    span = n if full else n
    for i in range(span):
        r0 = rings[i]
        r1 = rings[(i + 1) % len(rings)] if full else rings[i + 1]
        for j in range(m):
            k = (j + 1) % m
            bm.faces.new((r0[j], r0[k], r1[k], r1[j]))
    if not full:
        bm.faces.new(rings[0][::-1])
        bm.faces.new(rings[-1])
    # Eksen uzerindeki (r = 0) noktalar her dilimde ayri vertex uretir; once
    # kaynatilir, yoksa tepe noktasi acik kalir ve manifold disi kenar birakir.
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    dead = [f for f in bm.faces if f.calc_area() < 1e-9]
    if dead:
        bmesh.ops.delete(bm, geom=dead, context="FACES_ONLY")
    # Profilin eksen uzerinde uzanan pargasi donunce tek bir kenara cokuyor;
    # yuzleri sifir alanli oldugu icin silinince kenar bosta kalir.
    stray = [e for e in bm.edges if not e.link_faces]
    if stray:
        bmesh.ops.delete(bm, geom=stray, context="EDGES")
    big = [f for f in bm.faces if len(f.verts) > 4]
    if big:
        bmesh.ops.triangulate(bm, faces=big)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    return _finish(me, obj, col, smooth_angle)


def shell(name, fn, x0, x1, y0, y1, nx, ny, thick, col=None):
    """z = fn(x, y) yuzeyi ve altinda sabit kalinlikta ikizi: kapali levha.

    Cati ortusu icin. heightfield tabana kadar etek indirir; cati bunu degil,
    kendi kalinligini ister.
    """
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    top, bot = [], []
    for j in range(ny + 1):
        y = y0 + (y1 - y0) * j / ny
        rt, rb = [], []
        for i in range(nx + 1):
            x = x0 + (x1 - x0) * i / nx
            z = fn(x, y)
            rt.append(bm.verts.new((x, y, z)))
            rb.append(bm.verts.new((x, y, z - thick)))
        top.append(rt)
        bot.append(rb)
    for j in range(ny):
        for i in range(nx):
            bm.faces.new((top[j][i], top[j][i + 1], top[j + 1][i + 1], top[j + 1][i]))
            bm.faces.new((bot[j][i], bot[j + 1][i], bot[j + 1][i + 1], bot[j][i + 1]))
    for i in range(nx):
        bm.faces.new((top[0][i], bot[0][i], bot[0][i + 1], top[0][i + 1]))
        bm.faces.new((top[ny][i], top[ny][i + 1], bot[ny][i + 1], bot[ny][i]))
    for j in range(ny):
        bm.faces.new((top[j][0], top[j + 1][0], bot[j + 1][0], bot[j][0]))
        bm.faces.new((top[j][nx], bot[j][nx], bot[j + 1][nx], top[j + 1][nx]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    return _finish(me, obj, col, 0.0)


def join(name, objs, col=None):
    """Ayni malzemedeki parcalari tek nesnede birlestirir (renderer sayisi icin)."""
    # temp_override: bpy.ops.object.join taze bir dosyada, aktif nesne hic
    # kurulmadan cagrilinca poll() hatasi veriyor. Baglami acikca kuruyoruz.
    # Genel secim temizligi yapilmaz: nesne silindikten sonra view_layer listesi
    # bayat kalip None dondurebiliyor. temp_override zaten kumeyi acikca verir.
    bpy.context.view_layer.update()
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    with bpy.context.temp_override(active_object=objs[0], object=objs[0],
                                   selected_objects=objs,
                                   selected_editable_objects=objs):
        bpy.ops.object.join()
    o = objs[0]
    o.name = name
    o.data.name = name
    return o


def assign(objs, mat):
    for o in objs:
        o.data.materials.clear()
        o.data.materials.append(mat)


# ---------------------------------------------------------------- denetim
#
# Cabin_Refuge'un HUT_AUDIT'inden genellestirildi. Cati formulu artik gomulu
# degil: cagiran taraf yuzey fonksiyonunu verir. Formulu gomulu birakmak,
# cati degistiginde denetimin saglam parcalari isaretlemesine yol acmisti.

def audit(collection, roof_fn=None, roof_parts=(), roof_exempt=(),
          envelope=None, envelope_exempt=(), assemblies=(), float_exempt=(),
          verbose=True):
    """Yapiyi olcer. Donen sozlukteki her alan bos olmali.

    roof_fn(v) -> noktanin cati kabugundan DIK isaretli uzakligi; disarisi arti.
    Verilirse, roof_parts ve roof_exempt disindaki hicbir parca kabugu asmamali.

    Dik uzaklik kullanilir, "ayni x'te yuzeyin z'si" degil: 59 derecelik bir
    A-frame catida duseyde olcmek, kabugun icinde duran merteklerin tamamini
    yanlislikla "delen" olarak isaretler.
    envelope: (x_min, x_max, y_min, y_max) dis kabuk; disina tasan parca yok.
    """
    # matrix_world tembel hesaplanir. Tazelemeden olcmek her parcayi orijinde
    # gosterir; o durumda denetim "hepsi cakisik" der ve gercek hatalari gizler.
    bpy.context.view_layer.update()

    objs = [o for o in collection.objects if o.type == "MESH" and len(o.data.vertices)]
    if not objs:
        raise RuntimeError("koleksiyon bos: %s" % collection.name)

    W = {o.name: [o.matrix_world @ v.co for v in o.data.vertices] for o in objs}
    BB = {n: (Vector((min(v.x for v in w), min(v.y for v in w), min(v.z for v in w))),
              Vector((max(v.x for v in w), max(v.y for v in w), max(v.z for v in w))))
          for n, w in W.items()}
    out = {}

    # 1) havada kalan parca
    T = 0.0002
    floating = []
    for a in objs:
        # Dogrudan araziye oturan parcalar (kizak, pabuc) hicbir modele degmez;
        # onlari isaretlemek gercek havada kalan parcayi gurultuye bogar.
        if a.name.startswith(tuple(float_exempt)):
            continue
        amin, amax = BB[a.name]
        touch = False
        for b in objs:
            if b is a:
                continue
            bmin, bmax = BB[b.name]
            if (amin.x <= bmax.x + T and bmin.x <= amax.x + T and
                    amin.y <= bmax.y + T and bmin.y <= amax.y + T and
                    amin.z <= bmax.z + T and bmin.z <= amax.z + T):
                touch = True
                break
        if not touch:
            floating.append(a.name)
    out["havada"] = floating

    # 2) cati yuzeyini delen
    pierce = []
    if roof_fn is not None:
        for o in objs:
            n = o.name
            if n.startswith(tuple(roof_parts)) or n.startswith(tuple(roof_exempt)):
                continue
            d = max((roof_fn(v) for v in W[n]), default=-1.0)
            if d > 0.002:
                pierce.append((n, round(d, 4)))
        pierce.sort(key=lambda t: -t[1])
    out["delen"] = pierce

    # 3) dis kabugu asan
    env_bad = []
    if envelope is not None:
        x0, x1, y0, y1 = envelope
        e = 0.0005
        for o in objs:
            n = o.name
            if n.startswith(tuple(envelope_exempt)):
                continue
            d = max(max(x0 - v.x, v.x - x1, y0 - v.y, v.y - y1) for v in W[n])
            if d > e:
                env_bad.append((n, round(d, 4)))
        env_bad.sort(key=lambda t: -t[1])
    out["kabuk"] = env_bad

    # 4) hareketli parcadan kopuk
    orphan = []
    for a in assemblies:
        for o in objs:
            if o.name != a and o.name.startswith(a) and o.parent is None:
                orphan.append(o.name)
    out["kopuk"] = orphan

    # 5) topoloji
    ngon = zero = nonman = loose = 0
    for o in objs:
        bm = bmesh.new()
        bm.from_mesh(o.data)
        for f in bm.faces:
            if len(f.verts) > 4:
                ngon += 1
            if f.calc_area() < 1e-9:
                zero += 1
        nonman += sum(1 for e in bm.edges if not e.is_manifold)
        loose += sum(1 for v in bm.verts if not v.link_edges)
        bm.free()
    out["topo"] = dict(ngon=ngon, zero=zero, nonmanifold=nonman, loose=loose)

    # 6) ayni yone bakan cakisik yuzey (z-fighting kaynagi)
    buckets = {}
    for o in objs:
        me = o.data
        mw = o.matrix_world
        n3 = mw.to_3x3()
        for p in me.polygons:
            nrm = (n3 @ p.normal).normalized()
            ctr = mw @ p.center
            key = (round(nrm.x, 2), round(nrm.y, 2), round(nrm.z, 2), round(nrm.dot(ctr), 3))
            buckets.setdefault(key, []).append(
                (o.name, p.index, nrm,
                 [mw @ me.vertices[me.loops[li].vertex_index].co
                  for li in p.loop_indices]))
    pairs = 0
    worst = 0.0
    sample = []
    for key, fs in buckets.items():
        if len(fs) < 2:
            continue
        # Zemine oturan tabanlar: her parcanin z=0'daki alt yuzu ayni duzleme
        # duser. Hicbiri gorunmez, z-fighting da olusmaz -- oyuncu zemini asla
        # alttan gormez. Bunlari saymak gercek hatalari gurultuye bogar.
        if abs(key[0]) < 0.01 and abs(key[1]) < 0.01 and key[2] < -0.99 \
                and abs(key[3]) < 0.002:
            continue
        # AYNI nesne icindeki cift de sayilir: birlestirilmis parcalarda ayni
        # yerde iki kez uretilmis eleman (kule korkulugundaki yinelenen direk)
        # nesne disi karsilastirmada hic gorunmuyordu ve siyah z-fighting
        # birakiyordu.
        for (n1, i1, nr1, v1), (n2, i2, _nr2, v2) in itertools.combinations(fs, 2):
            if n1 == n2:
                if i1 == i2:
                    continue
                # Ayni nesnede yalniz TAM KOPYA yuz sayilir. Kutu kesisimi
                # bitisik ucgenlerde de olusur; ucgenlenmis bir kapakta bu
                # binlerce yanlis alarm veriyordu.
                s1 = sorted(tuple(round(c, 5) for c in p) for p in v1)
                s2 = sorted(tuple(round(c, 5) for c in p) for p in v2)
                if s1 != s2:
                    continue
            # duzlem ici cerceve DUNYA EKSENINDEN kurulur. Vector.orthogonal()
            # komsu yuzlerde farkli eksen secip ortusmeyi kacirir.
            ax = Vector((0, 1, 0)) if abs(nr1.y) < 0.9 else Vector((1, 0, 0))
            u = (ax - nr1 * ax.dot(nr1)).normalized()
            w = nr1.cross(u)

            def box(vs):
                q = [(x.dot(u), x.dot(w)) for x in vs]
                return (min(t[0] for t in q), max(t[0] for t in q),
                        min(t[1] for t in q), max(t[1] for t in q))

            b1, b2 = box(v1), box(v2)
            ov = (max(0.0, min(b1[1], b2[1]) - max(b1[0], b2[0])) *
                  max(0.0, min(b1[3], b2[3]) - max(b1[2], b2[2])))
            if ov >= 0.0015:
                pairs += 1
                if ov > worst:
                    worst = ov
                    sample = [n1, n2]
    out["cakisik"] = (pairs, round(worst, 4), sample)

    tris = sum(len(o.data.loop_triangles) if o.data.loop_triangles else
               sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs)
    out["tri"] = tris
    out["parca"] = len(objs)

    if verbose:
        print("parca / ucgen           : %d / %d" % (len(objs), tris))
        print("havada kalan (0.2 mm)   : %s" % (floating or "YOK"))
        print("cati yuzeyini delen     : %s" % (pierce[:12] or "YOK"))
        print("dis kabugu asan         : %s" % (env_bad[:10] or "YOK"))
        print("hareketliden kopuk      : %s" % (orphan or "YOK"))
        print("topoloji                : %s" % out["topo"])
        print("cakisik yuzey           : %d cift, en buyuk %.4f m2 %s"
              % (pairs, worst, sample))
    return out


def audit_clean(res):
    """Denetim sonucu temiz mi."""
    t = res["topo"]
    return (not res["havada"] and not res["delen"] and not res["kabuk"]
            and not res["kopuk"] and res["cakisik"][0] == 0
            and t["ngon"] == 0 and t["zero"] == 0 and t["nonmanifold"] == 0
            and t["loose"] == 0)
