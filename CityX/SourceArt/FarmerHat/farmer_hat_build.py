# THE FARMER HAT, headless (owner, 2026-10-26: "separate the white playermodel from the hat. then lower face count to a reasonable amount and
# add it as a wearable hat ingame called "Farmer Hat""):
#   blender --background --factory-startup --python farmer_hat_build.py -- <Meshy Straw Hat Wanderer .fbx> <its colour .png>
#                                                                          <out FarmerHat.glb> <out FarmerHat.jpg> [<out .blend>]
# Meshy's "Straw Hat Wanderer": a straw hat fused to a white figure like ours (one 3M-triangle surface, a 4096 colour map).
#   1. The figure traced up from its feet through white faces only (pure white, where the straw is yellow and brown), so pale straw stays.
#   2. Its head (the white above 80% of its height) fitted with a sphere: the hat scaled and moved so that sphere is ours (FusedBodyShape's
#      HeadRadius, centred at BodyRig.HeadCentreY), standing on a body at the origin facing Blender's -y, the game's +z.
#   3. The figure cut away, crumbs dropped, the hat reduced to HAT_TRIANGLES, unwrapped and Meshy's colour baked across onto one map.
# FarmerHat.glb is in the game's frame through glTF (Blender x, y, z -> the game's -x, z, -y), as the Knight Helmet's: FarmerHatBuilder takes the
# head's centre off. The .blend, if asked for, holds the hat on our body (Logs/PlayerBodyReference.obj) for adjusting by hand.
import os
import sys

import bmesh
import bpy
import numpy as np

args = sys.argv[sys.argv.index("--") + 1:]
FBX, COLOUR, OUT_GLB, OUT_JPG = (os.path.abspath(a) for a in args[:4])
OUT_BLEND = os.path.abspath(args[4]) if len(args) > 4 else None
HERE = os.path.dirname(os.path.abspath(__file__))
BODY_OBJ = os.path.join(HERE, "..", "..", "Logs", "PlayerBodyReference.obj")

HAT_TRIANGLES = 5000             # tools/model_qa.py's hat budget: 4k fine, 6k most; the frayed straw needs the upper end
TEXTURE_SIZE = 1024
JPEG_QUALITY = 85
HEAD_RADIUS = 0.35               # FusedBodyShape.HeadRadius
HEAD_CENTRE_Y = 2.251            # BodyRig.HeadCentreY (the game's up is Blender's z)
WHITE_VALUE, WHITE_SATURATION = 0.7, 0.15   # the figure: brighter than this, less coloured than this (the straw is saturated)
FEET_SEED = 0.25                 # the trace starts from the figure's faces this low a share of its height
HEAD_SHARE = 0.8                 # the head: the figure above this share of its height
CRUMB = 30                       # loose pieces smaller than this are dropped
REACH = 0.03                     # how far the reduced hat may lie from Meshy's when the colour is baked across (our metres)
LINING_GAP = 0.015               # the crown's inside, modelled resting on the head, held this far off it: reduced, the crown's top tore into
                                 # shards, and the head's skin showed through the gaps wherever the lining lay on it


def off_the_head(verts):
    """Every point of the hat nearer the head's centre than its surface plus LINING_GAP pushed straight out to that distance."""
    centre = np.array((0.0, 0.0, HEAD_CENTRE_Y))
    out = verts - centre
    distance = np.linalg.norm(out, axis=1)
    near = distance < HEAD_RADIUS + LINING_GAP
    verts[near] = centre + out[near] / distance[near, None] * (HEAD_RADIUS + LINING_GAP)
    print("held %d points of the hat off the head" % near.sum())
    return verts


def only(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[-1]


def face_colours(me, pixels):
    h, w = pixels.shape[:2]
    n = len(me.polygons)
    uv = np.zeros(len(me.loops) * 2)
    me.uv_layers.active.data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 2)
    starts = np.zeros(n, dtype=int)
    me.polygons.foreach_get("loop_start", starts)
    totals = np.zeros(n, dtype=int)
    me.polygons.foreach_get("loop_total", totals)
    mean = np.add.reduceat(uv, starts) / totals[:, None]
    return pixels[np.clip((mean[:, 1] * h).astype(int), 0, h - 1), np.clip((mean[:, 0] * w).astype(int), 0, w - 1), :3]


def sphere_fit(points):
    """Least squares: |p - c|^2 = r^2 as a linear system in c and r^2 - |c|^2."""
    a = np.hstack([2.0 * points, np.ones((len(points), 1))])
    b = (points ** 2).sum(1)
    sol, *_ = np.linalg.lstsq(a, b, rcond=None)
    centre = sol[:3]
    return centre, np.sqrt(sol[3] + (centre ** 2).sum())


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
high = [o for o in bpy.data.objects if o.type == "MESH"][0]
only([high])
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
image = bpy.data.images.load(COLOUR)
pixels = np.array(image.pixels[:], dtype=np.float32).reshape(image.size[1], image.size[0], 4)

# ---- 1. the figure traced from its feet through white ----
me = high.data
n = len(me.polygons)
centres = np.zeros(n * 3)
me.polygons.foreach_get("center", centres)
centres = centres.reshape(n, 3)
rgb = face_colours(me, pixels)
value = rgb.max(1)
saturation = np.where(value > 0, (value - rgb.min(1)) / np.maximum(value, 1e-6), 0.0)
white = (value > WHITE_VALUE) & (saturation < WHITE_SATURATION)
bottom, top = centres[:, 2].min(), centres[:, 2].max()
bm = bmesh.new()
bm.from_mesh(me)
bm.faces.ensure_lookup_table()
figure = np.zeros(n, dtype=bool)
stack = list(np.nonzero(white & (centres[:, 2] < bottom + FEET_SEED * (top - bottom)))[0])
figure[stack] = True
while stack:
    f = bm.faces[stack.pop()]
    for e in f.edges:
        for g in e.link_faces:
            if not figure[g.index] and white[g.index]:
                figure[g.index] = True
                stack.append(g.index)
print("the figure: %d of %d faces (white: %d), up to %.3f" % (figure.sum(), n, white.sum(), centres[figure, 2].max()))

# ---- 2. its head fitted, the hat onto ours ----
figure_top = centres[figure, 2].max()
head_points = centres[figure & (centres[:, 2] > bottom + HEAD_SHARE * (figure_top - bottom))]
centre, radius = sphere_fit(head_points)
print("the figure's head: %d face centres, sphere at %s radius %.3f (the figure %.3f tall)" % (
    len(head_points), centre.round(3), radius, figure_top - bottom))
scale = HEAD_RADIUS / radius
bmesh.ops.delete(bm, geom=[bm.faces[i] for i in np.nonzero(figure)[0]], context="FACES")
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
for v in bm.verts:
    v.co = (np.array(v.co) - centre) * scale + np.array((0.0, 0.0, HEAD_CENTRE_Y))

# ---- 3. crumbs off, reduced, the colour baked across ----
bm.faces.ensure_lookup_table()
seen = set()
crumbs = []
for f in bm.faces:
    if f.index in seen:
        continue
    piece, stack = [], [f]
    seen.add(f.index)
    while stack:
        g = stack.pop()
        piece.append(g)
        for e in g.edges:
            for h in e.link_faces:
                if h.index not in seen:
                    seen.add(h.index)
                    stack.append(h)
    if len(piece) < CRUMB:
        crumbs.extend(piece)
bmesh.ops.delete(bm, geom=crumbs, context="FACES")
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
bm.to_mesh(me)
bm.free()
me.vertices.foreach_set("co", off_the_head(np.array([v.co[:] for v in me.vertices])).ravel())
me.update()
co = np.array([v.co[:] for v in me.vertices])
print("the hat: %d triangles after %d crumb faces; from %s to %s" % (
    sum(len(p.vertices) - 2 for p in me.polygons), len(crumbs), co.min(0).round(3), co.max(0).round(3)))

only([high])
bpy.ops.object.duplicate()
low = bpy.context.view_layer.objects.active
low.name = "FarmerHat"
mod = low.modifiers.new("Reduce", "DECIMATE")
mod.decimate_type = "COLLAPSE"
mod.ratio = min(1.0, HAT_TRIANGLES / sum(len(p.vertices) - 2 for p in low.data.polygons))
mod.use_collapse_triangulate = True
only([low])
bpy.ops.object.modifier_apply(modifier="Reduce")
print("reduced: %d triangles" % len(low.data.polygons))
# Collapsing edges moves points: some come back onto the head.
low.data.vertices.foreach_set("co", off_the_head(np.array([v.co[:] for v in low.data.vertices])).ravel())
low.data.update()

only([low])
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.004, area_weight=1.0, scale_to_bounds=True)
bpy.ops.uv.average_islands_scale()
bpy.ops.uv.pack_islands(rotate=True, margin=0.004)
bpy.ops.object.mode_set(mode="OBJECT")
low.data.shade_smooth()
low.data.set_sharp_from_angle(angle=np.radians(45.0))

scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.samples = 4
scene.cycles.device = "CPU"
scene.render.bake.use_selected_to_active = True
scene.render.bake.cage_extrusion = REACH
scene.render.bake.max_ray_distance = REACH * 2.0
scene.render.bake.margin = 16
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
target = bpy.data.images.new("FarmerHatColour", TEXTURE_SIZE, TEXTURE_SIZE, alpha=False)
catch = bpy.data.materials.new("Catch")
catch.use_nodes = True
node = catch.node_tree.nodes.new("ShaderNodeTexImage")
node.image = target
catch.node_tree.nodes.active = node
low.data.materials.clear()
low.data.materials.append(catch)
only([high, low])
bpy.ops.object.bake(type="EMIT")
settings = scene.render.image_settings
settings.file_format, settings.color_mode, settings.quality = "JPEG", "RGB", JPEG_QUALITY
target.save_render(OUT_JPG, scene=scene)
print("WROTE %s: %d bytes" % (OUT_JPG, os.path.getsize(OUT_JPG)))

only([low])
bpy.ops.export_scene.gltf(filepath=OUT_GLB, export_format="GLB", use_selection=True, export_materials="NONE",
                          export_normals=True, export_texcoords=True, export_apply=True)
print("WROTE %s: %d triangles" % (OUT_GLB, len(low.data.polygons)))

if OUT_BLEND:
    bpy.data.objects.remove(high)
    shown_tex = bpy.data.materials.new("FarmerHatShown")
    shown_tex.use_nodes = True
    bsdf = next(nd for nd in shown_tex.node_tree.nodes if nd.type == "BSDF_PRINCIPLED")
    tex = shown_tex.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(OUT_JPG)
    shown_tex.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    low.data.materials.clear()
    low.data.materials.append(shown_tex)
    if os.path.exists(BODY_OBJ):
        bpy.ops.wm.obj_import(filepath=BODY_OBJ)
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
    print("WROTE %s" % OUT_BLEND)
