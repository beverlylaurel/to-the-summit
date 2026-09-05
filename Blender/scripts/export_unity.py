# Karakolları Unity'ye aktarma boru hattı.
#
# Iki ayri strateji var ve sebebi olcuye dayanir:
#
#   Ana siginak (Cabin_Refuge): oyuncu surekli dibinde. Yakin plan keskinligi
#   dosemeli dokudan gelir; bu yuzden doseme + tint/puruz atlasi + ozel shader
#   duzeni korunur. Sadece atlaslar tazelenir.
#
#   Karakollar: cok sayida dar tahta ve sindiri adasindan olusur. Tam albedo
#   atlasi bu adalarda yeterli texel birakmaz. Bu nedenle kabinle ayni hibrit
#   yontemi kullanirlar: yuksek frekansli doku UV0'da doseme, yipranma ve
#   puruz/metalik/detile verisi UV1 atlasinda kalir.

import os
import bpy


def _view3d():
    """Edit modu operatorleri bir VIEW_3D ALANI ister; yalniz nesne listesi
    vermek yetmez ve 'context is incorrect' hatasi verir."""
    for w in bpy.context.window_manager.windows:
        for a in w.screen.areas:
            if a.type == "VIEW_3D":
                r = next((x for x in a.regions if x.type == "WINDOW"), None)
                if r:
                    return w, a, r
    return None, None, None


def _ov(objs, active=None):
    """bpy.ops icin acik baglam. Taze dosyada aktif nesne bayat kalabiliyor."""
    active = active or objs[0]
    bpy.context.view_layer.update()
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = active
    kw = dict(active_object=active, object=active,
              selected_objects=objs, selected_editable_objects=objs)
    w, a, r = _view3d()
    if a:
        kw.update(window=w, area=a, region=r, screen=w.screen)
    return bpy.context.temp_override(**kw)


def build_export(src_name, out_name, movers=()):
    """Kaynak koleksiyonu malzemeye gore birlestirip disa aktarim koleksiyonu kurar.

    Hareketli parcalar (kapi kanadi ve cocuklari) BIRLESTIRILMEZ: pivotlari
    Unity'de menteseyi tasiyor.
    """
    src = bpy.data.collections[src_name]
    old = bpy.data.collections.get(out_name)
    if old:
        for o in list(old.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(old)
    out = bpy.data.collections.new(out_name)
    bpy.context.scene.collection.children.link(out)

    # Kaynak nesneler adlarini birakir: kopyalar temiz adi alsin diye. Dosya
    # ZATEN KAYDEDILMEZ (asagida export sonrasi geri donuluyor), yoksa kopya
    # "Trap_DoorLeaf.001" olarak gidip Unity'de adiyla bulunamaz.
    keep, groups = [], {}
    srcobjs = [o for o in src.objects if o.type == "MESH" and len(o.data.vertices)]
    for o in srcobjs:
        o.name = o.name + ".SRC"
    for o in srcobjs:
        base = o.name[:-4]                    # ".SRC" ekini at
        d = o.copy()
        d.data = o.data.copy()
        d.name = base
        d.data.name = base
        out.objects.link(d)
        if base.startswith(tuple(movers)) or (o.parent and o.parent.name.startswith(tuple(m + ".SRC" for m in movers))) \
           or (o.parent and o.parent.name[:-4].startswith(tuple(movers))):
            keep.append(d)
        else:
            groups.setdefault(d.data.materials[0].name if d.data.materials else "none", []).append(d)

    merged = []
    for mat, objs in groups.items():
        if len(objs) > 1:
            with _ov(objs):
                bpy.ops.object.join()
        o = objs[0]
        o.name = "%s_%s" % (out_name, mat)
        o.data.name = o.name
        merged.append(o)
    # Hareketli parcalarin ebeveyn bagi kopyada bozulur; yeniden kurulur.
    byname = {o.name: o for o in keep}
    for o in keep:
        s = src.objects.get(o.name + ".SRC")
        if s and s.parent:
            p = byname.get(s.parent.name[:-4] if s.parent.name.endswith(".SRC") else s.parent.name)
            if p:
                o.parent = p
                o.matrix_parent_inverse = p.matrix_world.inverted()
    return out, merged + keep


def make_uv2(objs, margin=0.0015):
    """Ikinci UV katmani: ust uste binmeyen atlas paketi.

    KENAR PAYI UV KESRIDIR, PIKSEL DEGIL. 0.012 verildiginde 1024'luk atlasta
    her adanin cevresine 12 piksel birakiliyor; bu yapilarda ~3700 ada var,
    paketleyici hepsini sigdirmak icin oyle kucultuyor ki yuzlerin %67'si
    4 pikselin altina dusuyor ve atlasin yalnizca %4'u kaplaniyor. Sonuc:
    filtreleme bos siyahi iceri cekiyor, cati lekeli cikiyor.
    1024'te 0.0015 ~ 1.5 piksel; olculdu, kaplama %60'in uzerine cikiyor."""
    for o in objs:
        if "UVBake" in o.data.uv_layers:
            o.data.uv_layers.remove(o.data.uv_layers["UVBake"])
        o.data.uv_layers.new(name="UVBake")
        o.data.uv_layers.active = o.data.uv_layers["UVBake"]
    with _ov(objs):
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=margin,
                                 correct_aspect=False, scale_to_bounds=False)
        bpy.ops.uv.select_all(action="SELECT")
        bpy.ops.uv.average_islands_scale()
        bpy.ops.uv.pack_islands(rotate=True, margin=margin)
        bpy.ops.object.mode_set(mode="OBJECT")
    for o in objs:
        o.data.uv_layers.active = o.data.uv_layers["UVMap"]


def _img(name, size, float_buf=False, alpha=False, non_color=False):
    im = bpy.data.images.get(name)
    if im:
        bpy.data.images.remove(im)
    im = bpy.data.images.new(name, size, size, alpha=alpha, float_buffer=float_buf)
    if non_color:
        im.colorspace_settings.name = "Non-Color"
    return im


def _target(objs, image, uv="UVBake"):
    for o in objs:
        o.data.uv_layers.active = o.data.uv_layers[uv]
        for slot in o.material_slots:
            m = slot.material
            if not m or not m.use_nodes:
                continue
            nt = m.node_tree
            n = nt.nodes.get("BK_target")
            if n is None:
                n = nt.nodes.new("ShaderNodeTexImage")
                n.name = "BK_target"
                n.location = (900, -700)
            n.image = image
            nt.nodes.active = n


def _clean_targets(objs):
    for o in objs:
        for slot in o.material_slots:
            m = slot.material
            if m and m.use_nodes and m.node_tree.nodes.get("BK_target"):
                m.node_tree.nodes.remove(m.node_tree.nodes["BK_target"])
        if "UVMap" in o.data.uv_layers:
            o.data.uv_layers.active = o.data.uv_layers["UVMap"]


def bake_tint_rm(objs, name, out_dir, size=1024, margin=10, eps=2e-3, K=0.25):
    """Ana siginakla AYNI yontem: atlas yalniz YAVAS DEGISEN yipranma katsayisini
    tasir, yuzeyin kendi dokusu Unity'de doseme olarak gelir.

    Neden tam albedo pisirilmiyor -- olculdu: bu yapilar 240 ayri sindiri
    levhasindan olusuyor. smart_project her levhayi ayri ada yapiyor, 2048'lik
    atlasta ada basina ~30x8 piksel dusuyor ve kenar payi adadan buyuk kaliyor.
    Sonucta filtreleme adalar arasindaki siyahi iceri cekiyor: cati lekeli
    cikiyor. Tint atlasi ayni kotu paketlemeye DAYANIKLI, cunku tasidigi deger
    bir carpan ve komsu texel farki gozle secilmiyor.

    tint       = son_renk / (doseme_rengi + eps) * K      (K ile depolanir)
    roughmetal = R puruz, G metaliklik, B detile karisim maskesi
    """
    os.makedirs(out_dir, exist_ok=True)
    sc = bpy.context.scene
    prev = sc.render.engine
    sc.render.engine = "CYCLES"
    try:
        sc.cycles.device = "CPU"
        sc.cycles.samples = 1
        sc.cycles.use_denoising = False
    except Exception:
        pass
    sc.render.bake.margin = margin
    sc.render.bake.use_clear = True
    mats = {s.material for o in objs for s in o.material_slots if s.material}

    def rig(mode):
        saved = {}
        for m in mats:
            nt = m.node_tree
            L = nt.links
            b = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
            out = next((n for n in nt.nodes if n.type == "OUTPUT_MATERIAL"), None)
            if not b or not out:
                continue
            lk = out.inputs["Surface"].links[0] if out.inputs["Surface"].links else None
            saved[m] = (lk.from_node.name, lk.from_socket.name) if lk else None
            for n in [x for x in nt.nodes if x.name.startswith("BK_")]:
                nt.nodes.remove(n)
            x, y = out.location.x - 900, out.location.y - 1400
            det = nt.nodes.get("DET_mix")
            if mode == "tint":
                src = b.inputs["Base Color"].links[0].from_socket if b.inputs["Base Color"].links else None
                if src is None:
                    c = nt.nodes.new("ShaderNodeRGB"); c.name = "BK_c"; c.location = (x, y)
                    c.outputs[0].default_value = list(b.inputs["Base Color"].default_value)
                    src = c.outputs[0]
                if det:
                    ad = nt.nodes.new("ShaderNodeMixRGB"); ad.name = "BK_add"
                    ad.blend_type = "ADD"; ad.location = (x + 200, y); ad.inputs[0].default_value = 1
                    L.new(det.outputs["Result"], ad.inputs[1])
                    ad.inputs[2].default_value = (eps, eps, eps, 1)
                    dv = nt.nodes.new("ShaderNodeMixRGB"); dv.name = "BK_div"
                    dv.blend_type = "DIVIDE"; dv.location = (x + 380, y); dv.inputs[0].default_value = 1
                    L.new(src, dv.inputs[1]); L.new(ad.outputs[0], dv.inputs[2])
                    res = dv.outputs[0]
                else:
                    res = src
                sc_ = nt.nodes.new("ShaderNodeMixRGB"); sc_.name = "BK_sc"
                sc_.blend_type = "MULTIPLY"; sc_.location = (x + 560, y); sc_.inputs[0].default_value = 1
                L.new(res, sc_.inputs[1]); sc_.inputs[2].default_value = (K, K, K, 1)
                col = sc_.outputs[0]
            else:
                comb = nt.nodes.new("ShaderNodeCombineColor"); comb.name = "BK_comb"
                comb.location = (x + 380, y)
                r_in = b.inputs["Roughness"]
                if r_in.links:
                    L.new(r_in.links[0].from_socket, comb.inputs["Red"])
                else:
                    comb.inputs["Red"].default_value = r_in.default_value
                m_in = b.inputs["Metallic"]
                if m_in.links:
                    L.new(m_in.links[0].from_socket, comb.inputs["Green"])
                else:
                    comb.inputs["Green"].default_value = m_in.default_value
                ramp = nt.nodes.get("DET_ramp")
                if ramp:
                    L.new(ramp.outputs["Color"], comb.inputs["Blue"])
                col = comb.outputs[0]
            em = nt.nodes.new("ShaderNodeEmission"); em.name = "BK_em"; em.location = (x + 760, y)
            L.new(col, em.inputs["Color"]); L.new(em.outputs[0], out.inputs["Surface"])
        return saved

    def restore(saved):
        for m, prevlink in saved.items():
            nt = m.node_tree
            out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
            if prevlink:
                nt.links.new(nt.nodes[prevlink[0]].outputs[prevlink[1]], out.inputs["Surface"])
            for n in [x for x in nt.nodes if x.name.startswith("BK_")]:
                nt.nodes.remove(n)

    written = {}
    for mode, suffix, nc in (("tint", "_Tint", True), ("rm", "_RoughMetal", True)):
        im = _img(name + suffix, size, non_color=nc)
        s = rig(mode)
        _target(objs, im)
        with _ov(objs):
            bpy.ops.object.bake(type="EMIT")
        p = os.path.join(out_dir, name + suffix + ".png")
        im.filepath_raw = p
        im.file_format = "PNG"
        im.save()
        written[mode] = p
        restore(s)
    _clean_targets(objs)
    sc.render.engine = prev
    return written


def bake_maps(objs, name, out_dir, size=2048, orm_size=1024, margin=14):
    """Albedo, tanjant normal ve metalik/parlaklik haritalarini pisirir.

    DIKKAT: cok parcali yapilarda kullanma -- ada basina texel dusuk kalir ve
    kenar payi siyah sizdirir. bake_tint_rm tercih edilir. Tek parcali, buyuk
    yuzeyli varliklar icin durusu dogru."""
    os.makedirs(out_dir, exist_ok=True)
    sc = bpy.context.scene
    prev_engine = sc.render.engine
    sc.render.engine = "CYCLES"
    try:
        sc.cycles.device = "CPU"
        sc.cycles.samples = 1
        sc.cycles.use_denoising = False
    except Exception:
        pass
    sc.render.bake.margin = margin
    sc.render.bake.use_clear = True
    written = {}

    # --- albedo: yalniz taban rengi, isik katkisi yok
    im = _img(name + "_A", size, alpha=False)
    _target(objs, im)
    sc.render.bake.use_pass_direct = False
    sc.render.bake.use_pass_indirect = False
    sc.render.bake.use_pass_color = True
    with _ov(objs):
        bpy.ops.object.bake(type="DIFFUSE")
    p = os.path.join(out_dir, name + "_BaseColor.png")
    im.filepath_raw = p
    im.file_format = "PNG"
    im.save()
    written["albedo"] = p

    # --- normal: tanjant uzayi
    im = _img(name + "_N", size, non_color=True)
    _target(objs, im)
    with _ov(objs):
        bpy.ops.object.bake(type="NORMAL")
    p = os.path.join(out_dir, name + "_Normal.png")
    im.filepath_raw = p
    im.file_format = "PNG"
    im.save()
    written["normal"] = p

    # --- puruz ve metaliklik: URP Lit R=metalik, A=parlaklik bekler
    rough = _img(name + "_R", orm_size, non_color=True)
    _target(objs, rough)
    with _ov(objs):
        bpy.ops.object.bake(type="ROUGHNESS")
    metal = _img(name + "_M", orm_size, non_color=True)
    _bake_metallic(objs, metal)

    out = _img(name + "_MS", orm_size, alpha=True, non_color=True)
    rp, mp = list(rough.pixels), list(metal.pixels)
    px = [0.0] * (orm_size * orm_size * 4)
    for i in range(orm_size * orm_size):
        m = mp[i * 4]
        r = rp[i * 4]
        px[i * 4] = m
        px[i * 4 + 1] = m
        px[i * 4 + 2] = m
        px[i * 4 + 3] = 1.0 - r          # parlaklik = 1 - puruz
    out.pixels = px
    p = os.path.join(out_dir, name + "_MetallicSmoothness.png")
    out.filepath_raw = p
    out.file_format = "PNG"
    out.save()
    written["ms"] = p

    _clean_targets(objs)
    sc.render.engine = prev_engine
    return written


def _bake_metallic(objs, image):
    """Metallik icin dogrudan pisirme turu yok: gecici olarak isima baglanir."""
    mats = {s.material for o in objs for s in o.material_slots if s.material}
    saved = {}
    for m in mats:
        nt = m.node_tree
        b = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
        out = next((n for n in nt.nodes if n.type == "OUTPUT_MATERIAL"), None)
        if not b or not out:
            continue
        link = out.inputs["Surface"].links[0] if out.inputs["Surface"].links else None
        saved[m] = (link.from_node.name, link.from_socket.name) if link else None
        em = nt.nodes.new("ShaderNodeEmission")
        em.name = "BK_met"
        em.location = (out.location.x - 250, out.location.y - 300)
        mi = b.inputs["Metallic"]
        if mi.links:
            nt.links.new(mi.links[0].from_socket, em.inputs["Color"])
        else:
            v = mi.default_value
            em.inputs["Color"].default_value = (v, v, v, 1.0)
        nt.links.new(em.outputs[0], out.inputs["Surface"])
    _target(objs, image)
    with _ov(objs):
        bpy.ops.object.bake(type="EMIT")
    for m, prev in saved.items():
        nt = m.node_tree
        out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
        if prev:
            nt.links.new(nt.nodes[prev[0]].outputs[prev[1]], out.inputs["Surface"])
        n = nt.nodes.get("BK_met")
        if n:
            nt.nodes.remove(n)


def export_fbx(col, path):
    """Unity icin FBX. Olcek 1:1, ileri -Z, yukari Y, uzay donusumu pisirilmez."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    objs = [o for o in col.objects if o.type == "MESH"]
    with _ov(objs):
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
            bake_space_transform=False, object_types={"MESH"},
            use_mesh_modifiers=True, mesh_smooth_type="FACE",
            use_tspace=True, path_mode="STRIP", batch_mode="OFF")
    return path
