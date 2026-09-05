# Karakol denetimi -- KAYITLI .blend dosyalarini oldugu gibi olcer.
#
# build_*.py betikleri modeli uretir; bu arac uretilmis SONUCU denetler. Ikisi
# ayri sey: betik dogru olsa da kaydedilmis dosya baska bir surumden kalmis
# olabilir. Bu yuzden hicbir sey yeniden kurulmaz, dosya acilir ve olculur.
#
# Kapsam, yapiya ozel parametre GEREKTIRMEYEN anormallikler:
#   - topoloji: n-gon, sifir alanli yuz, manifold disi kenar, bosta vertex
#   - ayni duzleme dusen es yonlu yuz (z-fighting kaynagi)
#   - malzemesiz / UV'siz nesne, eksik doku dosyasi
#   - birim olmayan olcek (disa aktarimda donusum pisirir)
#   - zemin kotu: taban z = 0'in altina sarkmis mi, havada mi
#   - hareketli parcanin origini kendi agiyla ayni yerde mi (mentese yok demek)
#
# Kullanim:
#     exec(open(r"...\check_outposts.py").read())
#     REPORT = check_all()

import itertools
import os

import bpy
import bmesh
from math import radians
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

OUTPOSTS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "outposts")

FILES = ["Outpost_Trapper", "Outpost_Cellar", "Outpost_Shed", "Outpost_Tower",
         "Outpost_Homestead", "Outpost_Collier", "Outpost_Adit",
         "Outpost_Station", "Outpost_Chapel", "Outpost_Mill"]

# On izleme sahnesine ait yardimci nesneler modelin parcasi degil.
SKIP_PREFIX = ("PVW_",)

# Araziye oturan, hicbir modele degmesi beklenmeyen parcalar.
GROUND_PARTS = ("Sta_Anchor", "Sta_Pad", "Sta_MastBase", "Shed_Bearer",
                "Shed_Pad", "Chp_Plinth", "Mil_Stone", "Mil_Race",
                "Cell_Mound", "Cell_Wing", "Cell_Threshold",
                "Cell_Facade", "Tow_Pad", "Adit_Spoil")


def _mesh_objects(scene):
    return [o for o in scene.objects
            if o.type == "MESH" and len(o.data.vertices)
            and not o.name.startswith(SKIP_PREFIX)]


def _coplanar_pairs(objs):
    buckets = {}
    for o in objs:
        me = o.data
        mw = o.matrix_world
        n3 = mw.to_3x3()
        for p in me.polygons:
            nrm = (n3 @ p.normal).normalized()
            ctr = mw @ p.center
            key = (round(nrm.x, 2), round(nrm.y, 2), round(nrm.z, 2),
                   round(nrm.dot(ctr), 3))
            buckets.setdefault(key, []).append(
                (o.name, p.index, nrm,
                 [mw @ me.vertices[me.loops[li].vertex_index].co
                  for li in p.loop_indices]))
    found = []
    for key, fs in buckets.items():
        if len(fs) < 2:
            continue
        # Zemine oturan tabanlar: gorunmez, z-fighting olusmaz.
        if abs(key[0]) < 0.01 and abs(key[1]) < 0.01 and key[2] < -0.99 \
                and abs(key[3]) < 0.06:
            continue
        for (n1, i1, nr1, v1), (n2, i2, _n2, v2) in itertools.combinations(fs, 2):
            if n1 == n2:
                if i1 == i2:
                    continue
                s1 = sorted(tuple(round(c, 5) for c in p) for p in v1)
                s2 = sorted(tuple(round(c, 5) for c in p) for p in v2)
                if s1 != s2:
                    continue
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
                found.append((round(ov, 4), n1, n2))
    found.sort(reverse=True)
    return found


def check_file(name):
    path = os.path.normpath(os.path.join(OUTPOSTS, name + ".blend"))
    bpy.ops.wm.open_mainfile(filepath=path)
    bpy.context.view_layer.update()
    sc = bpy.context.scene
    objs = _mesh_objects(sc)
    out = {"dosya": name, "parca": len(objs)}

    tris = 0
    ngon = zero = nonman = loose = 0
    no_mat, no_uv, scaled, flipped = [], [], [], []
    for o in objs:
        me = o.data
        if not me.materials or me.materials[0] is None:
            no_mat.append(o.name)
        if not me.uv_layers:
            no_uv.append(o.name)
        s = o.scale
        if abs(s.x - 1) > 1e-4 or abs(s.y - 1) > 1e-4 or abs(s.z - 1) > 1e-4:
            scaled.append((o.name, tuple(round(v, 3) for v in s)))
        bm = bmesh.new()
        bm.from_mesh(me)
        volume = 0.0
        for f in bm.faces:
            tris += len(f.verts) - 2
            if len(f.verts) > 4:
                ngon += 1
            if f.calc_area() < 1e-9:
                zero += 1
            # Isaretli hacim yuzlerin sarim yonunu olcer. Blender viewport iki
            # yuzu de cizdigi icin ters sarilmis bir kabuk burada dogru gorunur;
            # Unity on yuzu atar ve parca tamamen kaybolur.
            vs = f.verts[:]
            for k in range(1, len(vs) - 1):
                volume += vs[0].co.dot(vs[k].co.cross(vs[k + 1].co)) / 6.0
        if volume < -1e-6:
            flipped.append((o.name, round(volume, 3)))
        nonman += sum(1 for e in bm.edges if not e.is_manifold)
        loose += sum(1 for v in bm.verts if not v.link_edges)
        bm.free()
    out["ucgen"] = tris
    out["topo"] = dict(ngon=ngon, zero=zero, nonmanifold=nonman, loose=loose)
    out["malzemesiz"] = no_mat
    out["uvsiz"] = no_uv
    out["olcekli"] = scaled
    out["ters_normal"] = flipped

    # eksik doku dosyasi
    missing = sorted({os.path.basename(im.filepath) for im in bpy.data.images
                      if im.source == "FILE" and im.filepath
                      and not os.path.exists(bpy.path.abspath(im.filepath))})
    out["eksik_doku"] = missing

    # dunya kutusu ve zemin kotu
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            for i in range(3):
                lo[i] = min(lo[i], w[i])
                hi[i] = max(hi[i], w[i])
    out["kutu"] = (round(hi.x - lo.x, 2), round(hi.y - lo.y, 2), round(hi.z - lo.z, 2))
    out["taban_z"] = round(lo.z, 3)

    # havada kalan parca (araziye oturanlar haric)
    T = 0.0002
    BB = {}
    for o in objs:
        w = [o.matrix_world @ v.co for v in o.data.vertices]
        BB[o.name] = (Vector((min(v.x for v in w), min(v.y for v in w), min(v.z for v in w))),
                      Vector((max(v.x for v in w), max(v.y for v in w), max(v.z for v in w))))
    floating = []
    for o in objs:
        if o.name.startswith(GROUND_PARTS):
            continue
        amin, amax = BB[o.name]
        if any(b is not o and
               amin.x <= BB[b.name][1].x + T and BB[b.name][0].x <= amax.x + T and
               amin.y <= BB[b.name][1].y + T and BB[b.name][0].y <= amax.y + T and
               amin.z <= BB[b.name][1].z + T and BB[b.name][0].z <= amax.z + T
               for b in objs):
            continue
        floating.append(o.name)
    out["havada"] = floating

    out["cakisik"] = _coplanar_pairs(objs)[:5]

    # CATIYI DELEN parca: yapisal parcalarin birbirine gecmesi normaldir
    # (tomruk zivanasi, kirisin duvara oturmasi), ama cati ortusunun icinden
    # baska bir kutle gecmesi degildir. Degirmen carki ve savagi tam bunu
    # yapiyordu ve hicbir test yakalamiyordu.
    roof = [o for o in objs if "Roof" in o.name or "Shingle" in o.name
            or "Shake" in o.name or "Sheet" in o.name]
    pierce = []
    if roof:
        trees = {}
        for o in objs:
            w = [o.matrix_world @ v.co for v in o.data.vertices]
            p = [list(f.vertices) for f in o.data.polygons]
            trees[o.name] = BVHTree.FromPolygons(w, p, all_triangles=False)
        # Catiyi delmesi BEKLENEN parcalar: catiyi tasiyan duvar/iskelet ve
        # bacalik. Baca catidan gecmek zorundadir; kabin duvari catinin
        # oturdugu yerdir. Gergi teli ise gecmemeli -- o bir hatadir.
        allow = ("Wall", "Gable", "Timber", "Rafter", "Purlin", "Ridge",
                 "Barge", "Sheath", "Plate", "Cap", "Flash", "Flue", "Mast",
                 "Cab", "Chim")
        for r in roof:
            for b in objs:
                if b is r or b.name in roof or b.name.startswith(SKIP_PREFIX):
                    continue
                if any(k in b.name for k in allow):
                    continue
                ov = trees[r.name].overlap(trees[b.name])
                if ov:
                    pierce.append((len(ov), r.name, b.name))
    pierce.sort(reverse=True)
    out["cati_delen"] = pierce[:4]

    # Hareketli parca gercekten ACILIYOR mu? Kanat mentese ekseninde donduruldu
    # ve komsu kutlelerle kesisimi arandi. En az bir yon serbest olmali; iki yon
    # de kapaliysa kapi modelde vardir ama oyunda kullanilamaz.
    movers = []
    for o in objs:
        if "DoorLeaf" not in o.name:
            continue
        kin = {o.name} | {c.name for c in o.children}
        others = [b for b in objs if b.name not in kin]
        rest = o.matrix_world.copy()
        # GERCEK ucgen kesisimi kullanilir. Kutu testi burada ise yaramaz:
        # birlestirilmis cati agi tek nesnedir ve kutusu butun binayi kaplar,
        # her kapi konumu icinde kalir. (Iki tur bu yuzden kaybedildi.)
        #
        # Yalniz SERBEST UC test edilir; kanat mentese ucunda soveye zaten
        # degmek zorundadir.
        dist = [v.co.length for v in o.data.vertices]
        lim = 0.45 * max(dist)
        far = {i for i, d in enumerate(dist) if d > lim}
        tip_polys = [list(p.vertices) for p in o.data.polygons
                     if all(i in far for i in p.vertices)]
        free = []
        if tip_polys:
            keep = sorted({i for p in tip_polys for i in p})
            remap = {i: k for k, i in enumerate(keep)}
            local = [o.data.vertices[i].co.copy() for i in keep]
            polys = [[remap[i] for i in p] for p in tip_polys]
            trees = []
            for b in others:
                bw = [b.matrix_world @ v.co for v in b.data.vertices]
                bp = [list(p.vertices) for p in b.data.polygons]
                trees.append((b.name, BVHTree.FromPolygons(bw, bp, all_triangles=False)))
            for ang in (-85.0, 85.0):
                m = (Matrix.Translation(rest.translation)
                     @ Matrix.Rotation(radians(ang), 4, "Z")
                     @ Matrix.Translation(-rest.translation) @ rest)
                dw = [m @ c for c in local]
                dt = BVHTree.FromPolygons(dw, polys, all_triangles=False)
                hit = next((nm for nm, t in trees if dt.overlap(t)), None)
                if hit is None:
                    free.append(int(ang))
        movers.append((o.name, free or "ACILAMIYOR"))
    out["kapi"] = movers
    return out


def check_all(verbose=True):
    rep = []
    for f in FILES:
        r = check_file(f)
        rep.append(r)
        if verbose:
            t = r["topo"]
            bad = []
            if any(t.values()):
                bad.append("topo %s" % t)
            for k in ("malzemesiz", "uvsiz", "olcekli", "ters_normal",
                      "eksik_doku", "havada", "cati_delen"):
                if r[k]:
                    bad.append("%s=%s" % (k, r[k]))
            if r["cakisik"]:
                bad.append("cakisik=%s" % r["cakisik"])
            if r["taban_z"] < -0.10:
                bad.append("taban z=%.3f" % r["taban_z"])
            print("%-20s parca %3d  ucgen %6d  kutu %s  %s"
                  % (r["dosya"], r["parca"], r["ucgen"], r["kutu"],
                     "SORUN: " + " | ".join(bad) if bad else "temiz"))
            print("%-20s kapi: %s" % ("", r["kapi"] or "kapi yok"))
    return rep
