# ANOTHER COLOUR OF THE SNEAKERS, headless (owner, 2026-10-26: "add these as a colorway for the shoes called "BW Sneakers""):
#   blender --background --factory-startup --python sneaker_colour_bake.py -- <white Urban Soles .fbx> <its colour .png>
#                                                                            <recoloured Urban Soles .fbx> <its colour .png>
#                                                                            <WhiteSneaker.glb> <out .jpg>
# Meshy's recolours of "Urban Soles" are the white pair face for face, each with its own UV layout. The legs are cut off by the white pair's
# grey (a recolour's shoe can be grey itself), the right shoe is put on our foot as the white pair's was (sneaker_meshy.py) and mirrored for
# the left, and its colour is baked across onto the game's pair (WhiteSneaker.glb: the owner's trim and the smoothing, its own UVs). The
# collar band's faces, smoothed, all share one texel of that layout (their tiny islands broke the white canvas into steps): it takes the
# median colour of the recolour's faces nearest them.
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils.bvhtree import BVHTree

args = sys.argv[sys.argv.index("--") + 1:]
WHITE_FBX, WHITE_COLOUR, FBX, COLOUR, GAME_GLB, OUT_JPG = (os.path.abspath(a) for a in args[:6])
HERE = os.path.dirname(os.path.abspath(__file__))
JPEG_QUALITY = 85


def run(path):
    exec(compile(open(path).read(), path, "exec"), globals())


def load_pixels(path):
    image = bpy.data.images.load(path)
    return image, np.array(image.pixels[:], dtype=np.float32).reshape(image.size[1], image.size[0], 4)


def imported_fbx(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    o = [o for o in bpy.data.objects if o not in before and o.type == "MESH"][0]
    only([o])
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return o


run(os.path.join(HERE, "sneaker_meshy.py"))
bpy.ops.wm.read_factory_settings(use_empty=True)

# ---- 1. the recolour's legs off, by the white pair's grey ----
white = imported_fbx(WHITE_FBX)
_, white_pixels = load_pixels(WHITE_COLOUR)
grey = face_colours(white.data, white_pixels).max(1) < GREY
bpy.data.objects.remove(white)
high = imported_fbx(FBX)
image, pixels = load_pixels(COLOUR)
legs = take_legs_off(high, grey)

# ---- 2. on our feet: the right shoe, and the left its mirror ----
sole = min(v.co.z for v in high.data.vertices) + TREAD_CUT
bm = bmesh.new()
bm.from_mesh(high.data)
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_center_median().x >= 0.0], context="FACES")
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
for v, co in zip(bm.verts, on_our_feet(np.array([v.co[:] for v in bm.verts]), legs["right"], sole)):
    v.co = co
mirrored = bmesh.ops.duplicate(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:])
for v in (g for g in mirrored["geom"] if isinstance(g, bmesh.types.BMVert)):
    v.co.x = -v.co.x
bmesh.ops.reverse_faces(bm, faces=[g for g in mirrored["geom"] if isinstance(g, bmesh.types.BMFace)])
bm.to_mesh(high.data)
bm.free()
high.data.update()

before = set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=GAME_GLB)
low = [o for o in bpy.data.objects if o not in before and o.type == "MESH"][0]
print("the game's pair: %d triangles; the recolour on our feet: %d" % (len(low.data.polygons), len(high.data.polygons)))

# ---- 3. the colour baked across ----
scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.samples = 4
scene.cycles.device = "CPU"
scene.render.bake.use_selected_to_active = True
scene.render.bake.cage_extrusion = REACH * SCALE
scene.render.bake.max_ray_distance = REACH * SCALE * 2.0
scene.render.bake.margin = 16   # wide: the small islands sit in black, which a smaller copy (mips) would bleed in
shown = bpy.data.materials.new("Shown")
shown.use_nodes = True
nodes = shown.node_tree.nodes
nodes.clear()
source = nodes.new("ShaderNodeTexImage")
source.image = image
emit = nodes.new("ShaderNodeEmission")
out = nodes.new("ShaderNodeOutputMaterial")
shown.node_tree.links.new(source.outputs["Color"], emit.inputs["Color"])
shown.node_tree.links.new(emit.outputs["Emission"], out.inputs["Surface"])
high.data.materials.clear()
high.data.materials.append(shown)
target = bpy.data.images.new("SneakerColour", TEXTURE_SIZE, TEXTURE_SIZE, alpha=False)
catch = bpy.data.materials.new("Catch")
catch.use_nodes = True
node = catch.node_tree.nodes.new("ShaderNodeTexImage")
node.image = target
catch.node_tree.nodes.active = node
low.data.materials.clear()
low.data.materials.append(catch)
only([high, low])
bpy.ops.object.bake(type="EMIT")

# ---- 4. the collar band's one texel ----
me = low.data
n = len(me.polygons)
uv = np.zeros(len(me.loops) * 2)
me.uv_layers.active.data.foreach_get("uv", uv)
starts = np.zeros(n, dtype=int)
me.polygons.foreach_get("loop_start", starts)
totals = np.zeros(n, dtype=int)
me.polygons.foreach_get("loop_total", totals)
texel = (np.add.reduceat(uv.reshape(-1, 2), starts) / totals[:, None] * TEXTURE_SIZE).astype(int)
cells, counts = np.unique(texel, axis=0, return_counts=True)
band_texel = cells[counts.argmax()]
band = np.all(texel == band_texel, axis=1)
centres = np.zeros(n * 3)
me.polygons.foreach_get("center", centres)
centres = centres.reshape(n, 3)
tree = BVHTree.FromPolygons([v.co.copy() for v in high.data.vertices], [p.vertices[:] for p in high.data.polygons])
colours = face_colours(high.data, pixels)
nearest = np.array([colours[tree.find_nearest(c)[2]] for c in centres[band]])
value = nearest.max(1)
print("collar band: %d faces at texel %s; their colour's value: %s at the 10/25/50/75/90th centiles" % (
    band.sum(), band_texel, np.percentile(value, [10, 25, 50, 75, 90]).round(2)))
median = np.median(nearest, axis=0)
baked = np.array(target.pixels[:], dtype=np.float32).reshape(TEXTURE_SIZE, TEXTURE_SIZE, 4)
x, y = band_texel
baked[max(0, y - 2):y + 3, max(0, x - 2):x + 3, :3] = median
target.pixels = baked.ravel()
print("the band's texel painted %s" % median.round(3))

settings = scene.render.image_settings
settings.file_format, settings.color_mode, settings.quality = "JPEG", "RGB", JPEG_QUALITY
target.save_render(OUT_JPG, scene=scene)
print("WROTE %s: %d bytes" % (OUT_JPG, os.path.getsize(OUT_JPG)))
