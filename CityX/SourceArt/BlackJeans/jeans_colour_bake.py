# A NEW COLOUR OF THE JEANS, headless (owner, 2026-10-25: "get the textures from these two and add them as wearable jeans options ingame"):
#   blender --background --factory-startup --python jeans_colour_bake.py -- <BlackJeansCut.glb> <Meshy jeans .fbx> <its colour .png> <out .jpg>
# Meshy's "Blank Avatar in Jeans" again, retextured: the same figure where the owner's cut (BlackJeansCut.glb) still stands, each with its own
# UV layout. Its colour is baked across onto the cut's own layout (the one BlackJeans.glb and the game use), then the faces of the cut that
# caught the mannequin's grey skin rather than the cloth (the waistband's inside, the hems' edges) are painted the denim's own colour, as the
# Black Jeans' were. The result goes through black_jeans_build.py like the Black Jeans' colour.
import os
import sys

import bpy
import numpy as np

args = sys.argv[sys.argv.index("--") + 1:]
CUT_GLB, MESHY_FBX, COLOUR, OUT_JPG = args[:4]
SIZE = 2048
REACH = 0.03   # how far the cut lies from Meshy's surface: it is cut from it

# The mannequin's skin: pale grey, no colour to it (denim is saturated or dark).
SKIN_MIN_VALUE = 0.42
SKIN_MAX_SATURATION = 0.14
SKIN_SHARE = 0.6   # a face this much skin is painted denim; a thin white stitch inside a face is kept
DILATE_PASSES = 12   # texels the pieces' edges are grown by into the empty map round them (a 1024 copy's mips need a few)


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
    meshes = [o for o in new if o.type == "MESH"]
    return max(meshes, key=lambda o: len(o.data.polygons))


bpy.ops.wm.read_factory_settings(use_empty=True)
cut = imported(bpy.ops.import_scene.gltf, CUT_GLB)
meshy = imported(bpy.ops.import_scene.fbx, MESHY_FBX)
for name, o in (("cut", cut), ("meshy", meshy)):
    co = np.array([v.co[:] for v in o.data.vertices])
    print("%s: %d triangles, from %s to %s" % (name, sum(len(p.vertices) - 2 for p in o.data.polygons), co.min(0).round(3), co.max(0).round(3)))

scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.samples = 4
scene.cycles.device = "CPU"
scene.render.bake.use_selected_to_active = True
scene.render.bake.cage_extrusion = REACH
scene.render.bake.max_ray_distance = REACH * 2.0
scene.render.bake.margin = 8

shown = bpy.data.materials.new("Shown")
shown.use_nodes = True
nodes = shown.node_tree.nodes
nodes.clear()
source = nodes.new("ShaderNodeTexImage")
source.image = bpy.data.images.load(COLOUR)
emit = nodes.new("ShaderNodeEmission")
out = nodes.new("ShaderNodeOutputMaterial")
shown.node_tree.links.new(source.outputs["Color"], emit.inputs["Color"])
shown.node_tree.links.new(emit.outputs["Emission"], out.inputs["Surface"])
meshy.data.materials.clear()
meshy.data.materials.append(shown)

target = bpy.data.images.new("Baked", SIZE, SIZE, alpha=True)
catch = bpy.data.materials.new("Catch")
catch.use_nodes = True
node = catch.node_tree.nodes.new("ShaderNodeTexImage")
node.image = target
catch.node_tree.nodes.active = node
cut.data.materials.clear()
cut.data.materials.append(catch)
only([meshy, cut])
bpy.ops.object.bake(type="EMIT")
pixels = np.array(target.pixels[:]).reshape(SIZE, SIZE, 4)
rgb = pixels[..., :3]

value = rgb.max(-1)
saturation = np.where(value > 1e-4, (value - rgb.min(-1)) / np.maximum(value, 1e-4), 0.0)
skin = (value > SKIN_MIN_VALUE) & (saturation < SKIN_MAX_SATURATION)

# Each face's texels, from its UV triangles (the image's rows run up from the bottom, as UVs do).
uv = cut.data.uv_layers.active.data
cloth = np.zeros((SIZE, SIZE), dtype=bool)
skin_faces = np.zeros((SIZE, SIZE), dtype=bool)
painted = 0
cut.data.calc_loop_triangles()
for tri in cut.data.loop_triangles:
    p = np.array([uv[i].uv[:] for i in tri.loops]) * SIZE
    x0, y0 = np.floor(p.min(0)).astype(int)
    x1, y1 = np.ceil(p.max(0)).astype(int)
    x0, y0, x1, y1 = max(x0, 0), max(y0, 0), min(x1, SIZE - 1), min(y1, SIZE - 1)
    if x1 < x0 or y1 < y0:
        continue
    xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
    (ax, ay), (bx, by), (cx, cy) = p
    d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
    if abs(d) < 1e-9:
        continue
    wa = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / d
    wb = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / d
    inside = (wa >= -0.02) & (wb >= -0.02) & (wa + wb <= 1.02)
    if not inside.any():
        continue
    rows, cols = np.nonzero(inside)
    rows += y0
    cols += x0
    if skin[rows, cols].mean() >= SKIN_SHARE:
        skin_faces[rows, cols] = True
        painted += 1
    else:
        cloth[rows, cols] = True

denim = np.median(rgb[cloth & ~skin], axis=0)
rgb[skin_faces & ~cloth] = denim
print("painted %d of %d faces the denim's colour %s; %.1f%% of the cloth's texels were skin-like" %
      (painted, len(cut.data.loop_triangles), denim.round(3), 100.0 * (skin & cloth).mean() / max(cloth.mean(), 1e-9)))

# The gaps between the pieces filled outward from their edges: left black, the game's smaller copy (1024, and its mips) bled dark seams in.
filled = cloth | skin_faces
for _ in range(DILATE_PASSES):
    if filled.all():
        break
    grown = rgb.copy()
    took = filled.copy()
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        shifted_ok = np.roll(filled, (dy, dx), axis=(0, 1))
        shifted_rgb = np.roll(rgb, (dy, dx), axis=(0, 1))
        new = shifted_ok & ~took
        grown[new] = shifted_rgb[new]
        took |= new
    rgb, filled = grown, took
rgb[~filled] = denim   # beyond the grown edge, flat: nothing samples it, and flat costs the file nothing

image = bpy.data.images.new(os.path.basename(OUT_JPG), SIZE, SIZE, alpha=False)
out_pixels = np.ones((SIZE, SIZE, 4), dtype=np.float32)
out_pixels[..., :3] = rgb
image.pixels.foreach_set(out_pixels.ravel())
scene.render.image_settings.file_format = "JPEG"
scene.render.image_settings.color_mode = "RGB"
scene.render.image_settings.quality = 80
image.save_render(OUT_JPG, scene=scene)
print("wrote", OUT_JPG)
