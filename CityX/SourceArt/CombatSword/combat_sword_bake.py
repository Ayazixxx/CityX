# THE COMBAT SWORD'S PAINT, headless (owner, 2026-10-19, with Meshy's "White Sword", the same sword fully textured: "implement the texture from
# this file onto the combat sword, if the file is unreasonably large make a smaller version of it"):
#   blender --background --python combat_sword_bake.py -- <CombatSword.glb> <Meshy White Sword .fbx> <its colour .png> <its metallic .png>
#                                                       <its roughness .png> <out .glb> <out colour .png> <out metal .png>
# Meshy's model is the game's combat sword exactly (the same 1.9 m, the same place) at 50,698 triangles with four 2048 maps (19 MB, about 22 MB
# of video memory). The game keeps its own 3,802 triangles; they are unwrapped and the paint is baked across from Meshy's:
#   colour   -> one COLOUR_SIZE map;
#   metallic -> the red of one METAL_SIZE map, smoothness (1 - roughness) its alpha: URP's metallic map. The blade is white steel, the grip
#               black wrapping, so which part shines matters; the normal map is left out (surface detail too fine to see on a swung sword).
# The game's glb is written back with its UVs.
import os
import sys

import bpy
import numpy as np

args = sys.argv[sys.argv.index("--") + 1:]
GAME_GLB, MESHY_FBX, COLOUR, METALLIC, ROUGHNESS, OUT_GLB, OUT_COLOUR, OUT_METAL = args[:8]
COLOUR_SIZE, METAL_SIZE = 512, 256
REACH = 0.02   # how far apart the two models' surfaces may be: they are the same sword


def only(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[-1]


def imported(op, path):
    before = set(bpy.data.objects)
    op(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    only(new)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return [o for o in new if o.type == "MESH"][0]


bpy.ops.wm.read_factory_settings(use_empty=True)
low = imported(bpy.ops.import_scene.gltf, GAME_GLB)
high = imported(bpy.ops.import_scene.fbx, MESHY_FBX)
for name, o in (("game", low), ("meshy", high)):
    co = np.array([v.co[:] for v in o.data.vertices])
    print("%s: %d triangles, from %s to %s" % (name, sum(len(p.vertices) - 2 for p in o.data.polygons), co.min(0).round(3), co.max(0).round(3)))

# ---- the game's mesh unwrapped ----
only([low])
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.01, area_weight=1.0, scale_to_bounds=True)
bpy.ops.uv.pack_islands(rotate=True, margin=0.008)
bpy.ops.object.mode_set(mode="OBJECT")

# ---- the paint baked across ----
scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.samples = 4
scene.cycles.device = "CPU"
scene.render.bake.use_selected_to_active = True
scene.render.bake.cage_extrusion = REACH
scene.render.bake.max_ray_distance = REACH * 2.0
scene.render.bake.margin = 6


def bake(source_path, size, name, colour_space):
    shown = bpy.data.materials.new("Shown" + name)
    shown.use_nodes = True
    nodes = shown.node_tree.nodes
    nodes.clear()
    source = nodes.new("ShaderNodeTexImage")
    source.image = bpy.data.images.load(source_path)
    source.image.colorspace_settings.name = colour_space
    emit = nodes.new("ShaderNodeEmission")
    out = nodes.new("ShaderNodeOutputMaterial")
    shown.node_tree.links.new(source.outputs["Color"], emit.inputs["Color"])
    shown.node_tree.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    high.data.materials.clear()
    high.data.materials.append(shown)
    target = bpy.data.images.new(name, size, size, alpha=True)
    target.colorspace_settings.name = colour_space
    catch = bpy.data.materials.new("Catch" + name)
    catch.use_nodes = True
    node = catch.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = target
    catch.node_tree.nodes.active = node
    low.data.materials.clear()
    low.data.materials.append(catch)
    only([high, low])
    bpy.ops.object.bake(type="EMIT")
    return np.array(target.pixels[:]).reshape(size, size, 4)


def save(pixels, size, path, colour_space):
    image = bpy.data.images.new(os.path.basename(path), size, size, alpha=True)
    image.colorspace_settings.name = colour_space
    image.pixels.foreach_set(pixels.ravel().astype(np.float32))
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()


colour = bake(COLOUR, COLOUR_SIZE, "Colour", "sRGB")
colour[..., 3] = 1.0
save(colour, COLOUR_SIZE, OUT_COLOUR, "sRGB")
metal = bake(METALLIC, METAL_SIZE, "Metal", "Non-Color")[..., 0]
smooth = 1.0 - bake(ROUGHNESS, METAL_SIZE, "Rough", "Non-Color")[..., 0]
packed = np.zeros((METAL_SIZE, METAL_SIZE, 4))
packed[..., 0] = metal
packed[..., 3] = smooth
save(packed, METAL_SIZE, OUT_METAL, "Non-Color")
print("metal: %.0f%% of the map is metal, smoothness %.2f..%.2f" % (100.0 * (metal > 0.5).mean(), smooth.min(), smooth.max()))

# ---- the game's glb, with its UVs ----
low.data.materials.clear()
only([low])
bpy.ops.export_scene.gltf(filepath=OUT_GLB, export_format="GLB", use_selection=True, export_materials="NONE",
                          export_normals=True, export_texcoords=True, export_vertex_color="NONE", export_animations=False)
print("WROTE %s, %s (%d), %s (%d)" % (OUT_GLB, OUT_COLOUR, COLOUR_SIZE, OUT_METAL, METAL_SIZE))
