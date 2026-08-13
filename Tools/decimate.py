# -*- coding: utf-8 -*-
"""Bisikleti parca bolunmesini koruyarak seyreltir.

Neden parca basina ayri oran: tekerlek 700 bin ucgen, fren kolu 5 bin. Tek oran
uygulaninca buyuk parca hala agir kalirken kucuk parca lapa oluyor. Butce karekokle
dagitiliyor - buyuk parca daha cok kirpiliyor ama kucuk parca da tamamen erimiyor.

Calistirma:
  blender --background --python decimate.py -- <girdi.fbx> <cikti.fbx> <hedef_ucgen>
"""
import bpy
import sys
import math

argv = sys.argv[sys.argv.index("--") + 1:]
source, target_path, budget = argv[0], argv[1], int(argv[2])

# Sahneyi bosalt: Blender varsayilan kup, kamera ve isikla aciliyor.
bpy.ops.wm.read_factory_settings(use_empty=True)

bpy.ops.import_scene.fbx(filepath=source)

meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
print("PARCA:", len(meshes))


def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


counts = {o.name: triangles(o) for o in meshes}
total = sum(counts.values())
print("GIRDI UCGEN: %d" % total)

# Karekok agirlik: buyuk parca daha cok pay aliyor ama oranti dogrusal degil, yani
# kucuk parcalar orantisiz kucultulmuyor.
weights = {name: math.sqrt(count) for name, count in counts.items()}
weight_sum = sum(weights.values())

for obj in meshes:
    count = counts[obj.name]
    share = budget * weights[obj.name] / weight_sum

    # Taban: hicbir parca ucyuz ucgenin altina inmiyor. Ince yapilar (fren kolu, kablo)
    # o sinirin altinda silueti kaybediyor.
    share = max(min(count, 300), share)
    ratio = min(1.0, share / count)

    if ratio >= 0.999:
        print("%-16s %7d ucgen  (dokunulmadi)" % (obj.name, count))
        continue

    modifier = obj.modifiers.new(name="Decimate", type='DECIMATE')
    modifier.decimate_type = 'COLLAPSE'
    modifier.ratio = ratio
    # Simetriyi koru: bisiklette sag ve sol ayni sekle sahip, ayri seyreltilirse
    # birbirinden farkli cikiyor.
    modifier.use_symmetry = False

    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)

    print("%-16s %7d -> %6d ucgen  (oran %.3f)" % (obj.name, count, triangles(obj), ratio))

after = sum(triangles(o) for o in meshes)
print("CIKTI UCGEN: %d" % after)

bpy.ops.export_scene.fbx(
    filepath=target_path,
    use_selection=False,
    object_types={'MESH'},
    use_mesh_modifiers=True,
    mesh_smooth_type='FACE',
    add_leaf_bones=False,
    bake_anim=False,
    apply_scale_options='FBX_SCALE_NONE',
)

print("YAZILDI:", target_path)
