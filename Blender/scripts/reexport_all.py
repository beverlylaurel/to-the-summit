"""Butun karakollari duzeltip Unity'ye yeniden aktarir.

Once yon denetimi: Blender viewport iki yuzu de cizdigi icin ters sarilmis bir
kabuk orada dogru gorunur, Unity'de on yuz atilir ve parca kaybolur. Isaretli
hacim bunu olcer -- disa donuk kapali kabuk pozitif hacim kapsar.

Run with:  blender --background --python reexport_all.py
"""
import os
import shutil
import sys
import json

import bmesh
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.append(HERE)

import export_unity as X

PROJECT = os.path.normpath(os.path.join(HERE, "..", ".."))
OUTPOSTS = os.path.normpath(os.path.join(HERE, "..", "outposts"))
BAKE = os.path.join(OUTPOSTS, "bake")
FBX_DIR = os.path.join(PROJECT, "Assets", "Models", "Outposts")
TEX_DIR = os.path.join(PROJECT, "Assets", "Textures", "Outposts")
TILING_DIR = os.path.join(TEX_DIR, "Tiling")
MANIFEST = os.path.join(PROJECT, "Assets", "Editor", "OutpostManifest.json")

MODELS = [("Outpost_Trapper", "Trapper", 2048),
          ("Outpost_Cellar", "Cellar", 1024),
          ("Outpost_Shed", "Shed", 1024),
          ("Outpost_Tower", "Tower", 1024),
          ("Outpost_Station", "Station", 1024),
          ("Outpost_Chapel", "Chapel", 1024),
          ("Outpost_Mill", "Mill", 1024)]

# Kapi kanadi ve baglantilari ayri parca kalir: malzemeye gore birlestirilirse
# menteseden donme ekseni kaybolur.
MOVERS = {"Trapper": ("Trap_DoorLeaf", "Trap_DoorBrace", "Trap_DoorHandle"),
          "Cellar": ("Cell_DoorLeaf", "Cell_DoorRing", "Cell_DoorStrap"),
          "Shed": (),
          "Tower": (),
          "Station": ("Sta_DoorLeaf", "Sta_Hinge"),
          "Chapel": ("Chp_DoorLeaf", "Chp_Strap"),
          "Mill": ("Mil_DoorLeaf",)}


def material_entries(collection_name):
    """Build Unity material data from the Blender nodes that authored the bake.

    Keeping this beside export prevents texture names and UV scale from becoming a
    second hand-maintained table in Unity.
    """
    mats = {slot.material for obj in bpy.data.collections[collection_name].objects
            if obj.type == "MESH" for slot in obj.material_slots if slot.material}
    entries = []
    for mat in sorted(mats, key=lambda m: m.name):
        nodes = mat.node_tree.nodes if mat.use_nodes else None
        base = nodes.get("BASE_img") if nodes else None
        normal = nodes.get("NRM_img") if nodes else None
        mapping = nodes.get("MAP") if nodes else None
        det_mapping = nodes.get("DET_map") if nodes else None
        procedural = base is None or base.image is None
        entry = {"mat": mat.name, "proc": procedural, "tiling": 1.0,
                 "detX": 0.0, "detY": 0.0, "baseTex": "", "nrmTex": ""}
        if not procedural:
            entry["tiling"] = float(mapping.inputs["Scale"].default_value[0]) if mapping else 1.0
            if det_mapping:
                loc = det_mapping.inputs["Location"].default_value
                entry["detX"], entry["detY"] = float(loc[0]), float(loc[1])
            entry["baseTex"] = os.path.basename(bpy.path.abspath(base.image.filepath))
            if normal and normal.image:
                entry["nrmTex"] = os.path.basename(bpy.path.abspath(normal.image.filepath))
        entries.append(entry)
    return entries


def copy_tiling_textures(entries):
    os.makedirs(TILING_DIR, exist_ok=True)
    by_name = {os.path.basename(bpy.path.abspath(im.filepath)): bpy.path.abspath(im.filepath)
               for im in bpy.data.images if im.source == "FILE" and im.filepath}
    for entry in entries:
        if entry["proc"]:
            continue
        for key in ("baseTex", "nrmTex"):
            name = entry[key]
            source = by_name.get(name)
            if not source or not os.path.isfile(source):
                raise FileNotFoundError("Missing source texture for %s: %s" % (entry["mat"], name))
            shutil.copyfile(source, os.path.join(TILING_DIR, name))


def signed_volume(mesh):
    bm = bmesh.new()
    bm.from_mesh(mesh)
    total = 0.0
    for f in bm.faces:
        vs = f.verts[:]
        for k in range(1, len(vs) - 1):
            a, b, c = vs[0].co, vs[k].co, vs[k + 1].co
            total += a.dot(b.cross(c)) / 6.0
    bm.free()
    return total


def fix_normals():
    """Ters sarilmis kabuklari cevirir; duz/acik parcalara dokunmaz."""
    fixed = []
    for obj in bpy.data.objects:
        if obj.type != "MESH" or obj.name.startswith("PVW_"):
            continue
        volume = signed_volume(obj.data)
        if volume >= -1e-6:
            continue
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.update()
        fixed.append("%s (%.3f)" % (obj.name, volume))
    return fixed


def main():
    log = []
    manifest = {"items": []}
    for src, out, atlas in MODELS:
        bpy.ops.wm.open_mainfile(filepath=os.path.join(OUTPOSTS, src + ".blend"))
        fixed = fix_normals()
        if fixed:
            bpy.ops.wm.save_mainfile()
        log.append("%s: normals fixed on %s" % (out, ", ".join(fixed) or "nothing"))

        entries = material_entries(out)
        copy_tiling_textures(entries)

        # Disa aktarim koleksiyonu kaynaktan AYRI adlanir; ayni ad verilirse
        # build_export kaynagi silinmis sayar.
        col, objs = X.build_export(out, out + "_Export", MOVERS.get(out, ()))
        X.make_uv2(objs)
        X.bake_tint_rm(objs, out, BAKE, size=atlas)
        X.export_fbx(col, os.path.join(FBX_DIR, out + ".fbx"))
        for suffix in ("_Tint.png", "_RoughMetal.png"):
            shutil.copyfile(os.path.join(BAKE, out + suffix),
                            os.path.join(TEX_DIR, out + suffix))
        log.append("  %s: %d meshes exported, atlas %d" % (out, len(objs), atlas))
        manifest["items"].append({"name": out, "atlas": atlas, "mats": entries})
    print("\n".join(log))
    with open(os.path.join(OUTPOSTS, "reexport.txt"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(log))
    with open(MANIFEST, "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, ensure_ascii=False, indent=2)
        fh.write("\n")


main()
