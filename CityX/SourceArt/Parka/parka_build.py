# THE FUR HOODED PARKA, headless (owner, 2026-11-20: "make the item an equipable item ingame make it blue rarity"):
#   blender --background --python parka_build.py -- <Parka.glb> <out .json> <out .jpg>
# Parka.glb is Meshy's olive parka with a fur-edged hood, remodelled onto this game's body in the owner's Blender (parka_remodel.py: sleeves
# cut loose and fitted to the wrists with ribbed cuffs, the left front re-hung, cut to under 15,000 triangles, one 1024 map laid out afresh),
# standing where the owner placed it. Nothing here moves it: it is skinned (parka_skin.py) and written out for BlenderGarmentMesh.
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE_ART = os.path.dirname(HERE)
GLB, OUT_JSON, OUT_JPG = sys.argv[sys.argv.index("--") + 1:][:3]


def run(path):
    exec(compile(open(path).read(), path, "exec"), globals())


bpy.ops.wm.read_factory_settings(use_empty=True)
run(os.path.join(SOURCE_ART, "Octaslash", "octaslash_rig.py"))
run(os.path.join(SOURCE_ART, "garment_rig.py"))
run(os.path.join(HERE, "parka_skin.py"))
body, rig = bpy.data.objects["Body"], bpy.data.objects["CityXRig"]
before = set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=GLB)
coat = [o for o in bpy.data.objects if o not in before and o.type == "MESH"][0]
bpy.ops.object.select_all(action="DESELECT")
coat.select_set(True)
bpy.context.view_layer.objects.active = coat
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
mesh = coat.data
co, weights = skin_parka(coat, body, rig)
print("parka: %d vertices, %d triangles, from %s to %s" % (len(co), len(mesh.polygons), co.min(0).round(3), co.max(0).round(3)))
mesh.uv_layers.active = mesh.uv_layers[0]      # the second set only says which points are a sleeve's
write_garment(mesh, co, weights, OUT_JSON)
image = next(n.image for m in mesh.materials for n in m.node_tree.nodes if n.type == "TEX_IMAGE" and n.image)
with open(OUT_JPG, "wb") as f:
    f.write(image.packed_file.data)
print("WROTE %s: %d x %d" % (OUT_JPG, *image.size))
