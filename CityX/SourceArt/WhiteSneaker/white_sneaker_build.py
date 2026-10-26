# THE WHITE SNEAKERS, headless (owner, 2026-10-25: "remove the grey leg from the shoe and lower face count to a reasonable amount. then add it
# as a wearable shoe and try to fit is the best you can. Item name : White Sneaker"; "the details on the bottom of the shoe arent a priority
# so if you need to really lower face count focus on lowering that area"):
#   blender --background --factory-startup --python white_sneaker_build.py -- <Meshy Urban Soles .fbx> <its colour .png>
#                                                                            <out WhiteSneaker.glb> <out WhiteSneaker.jpg>
# Meshy's "Urban Soles": a pair of chunky white sneakers, each fused to a grey leg tube (two pieces, 960k triangles, a 4096 colour map).
#   1. The legs traced down from their tops through grey faces, so the cut follows the collars; the shoes have their own lining inside.
#   2. One shoe (Meshy's right) has its tread sliced off flat over the lugs (the pattern is baked onto the flat underside) and is reduced to
#      SHOE_TRIANGLES; the left is its mirror image, so the pair match.
#   3. Unwrapped and Meshy's colour baked across onto one TEXTURE_SIZE map.
#   4. Each shoe put on our body's foot (BodyRig, FusedBodyShape): the collar's middle over the ankle, the sole on the ground, the toe forward,
#      scaled by SCALE (the collar then fits round the shin's ankle, the shoe a little longer than the foot, as a sneaker is). The right shoe is
#      Meshy's piece on -x: the figure faces -y.
# WhiteSneaker.glb is in the game's frame through glTF (Blender x, y, z -> the game's -x, z, -y): the owner can move a shoe there in Blender and
# export over it; WhiteSneakerBuilder splits it by side and hangs each on its foot bone.
import os
import sys

import bmesh
import bpy
import numpy as np

args = sys.argv[sys.argv.index("--") + 1:]
FBX, COLOUR, OUT_GLB, OUT_JPG = (os.path.abspath(a) for a in args[:4])

SHOE_TRIANGLES = 7000            # one shoe (the pair is twice this)
TEXTURE_SIZE = 1024
SCALE = 0.30                     # Meshy's units to metres on our body
HIP_X = 0.17                     # BodyRig.HipX: the feet stand under the hips
LEG_SEED_Z = -0.15               # over this is leg alone (Meshy's units); traced down from there
COLLAR_RIM = -0.34               # the collars' rim (Meshy's units): a loose piece centred over it is left of the leg...
CRUMB = 40                       # ...as is one of fewer faces than this
TREAD_CUT = 0.036                # the tread is sliced off this far over the bottom (Meshy's units): over the lugs' grooves (0.025-0.035),
                                 # under the insole (0.04)
GREY = 0.6                       # the leg is grey under this; the shoe white
REACH = 0.045                    # how far the reduced surface may lie from Meshy's when the colour is baked across (past the lugs
                                 # under the flat sole)


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


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
high = [o for o in bpy.data.objects if o.type == "MESH"][0]
only([high])
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
image = bpy.data.images.load(COLOUR)
pixels = np.array(image.pixels[:], dtype=np.float32).reshape(image.size[1], image.size[0], 4)

# ---- 1. the legs off ----
me = high.data
n = len(me.polygons)
centres = np.zeros(n * 3)
me.polygons.foreach_get("center", centres)
centres = centres.reshape(n, 3)
value = face_colours(me, pixels).max(1)
legs = {}
grey = value < GREY
for side, mask in (("right", centres[:, 0] < 0.0), ("left", centres[:, 0] >= 0.0)):
    legs[side] = centres[mask & (centres[:, 2] > LEG_SEED_Z), :2].mean(0)
    print("%s shoe: the collar's middle at %s" % (side, legs[side].round(3)))
# The leg traced from its top down through grey faces only: it stops where the shoe's white begins, so the cut follows the collar's own edge
# (the grey eyelets, ringed by white, are never reached).
bm = bmesh.new()
bm.from_mesh(me)
bm.faces.ensure_lookup_table()
cut = np.zeros(n, dtype=bool)
stack = list(np.nonzero(centres[:, 2] > LEG_SEED_Z)[0])
cut[stack] = True
while stack:
    f = bm.faces[stack.pop()]
    for e in f.edges:
        for g in e.link_faces:
            if not cut[g.index] and grey[g.index]:
                cut[g.index] = True
                stack.append(g.index)
for lo in np.arange(-0.81, -0.1, 0.1):
    band = (centres[:, 2] >= lo) & (centres[:, 2] < lo + 0.1)
    print("  leg traced %.2f..%.2f: %d of %d faces" % (lo, lo + 0.1, (cut & band).sum(), band.sum()))
bmesh.ops.delete(bm, geom=[bm.faces[i] for i in np.nonzero(cut)[0]], context="FACES")
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
# The shoe has its own lining under the collar, so the opening needs no lid. What the trace left of the leg (paler specks in its grey, a
# scrap of tube) is now loose from the shoe: only each shoe's own body is kept.
bm.faces.ensure_lookup_table()
seen = set()
pieces = []
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
    pieces.append(piece)
# A scrap stands where the leg was (over the collar's rim) or is a crumb; the lining and the laces, loose now too, sit in the shoe.
scraps = [f for piece in pieces if len(piece) < CRUMB or np.mean([g.calc_center_median().z for g in piece]) > COLLAR_RIM for f in piece]
print("kept %d pieces (the largest %s), dropped %d faces of scraps" % (
    sum(1 for p in pieces if len(p) >= CRUMB and np.mean([g.calc_center_median().z for g in p]) <= COLLAR_RIM),
    sorted([len(p) for p in pieces], reverse=True)[:6], len(scraps)))
bmesh.ops.delete(bm, geom=scraps, context="FACES")
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
bm.to_mesh(me)
bm.free()
print("legs off: %d of %d faces cut, %d triangles left" % (cut.sum(), n, sum(len(p.vertices) - 2 for p in me.polygons)))

# ---- 2. one shoe, its tread sliced off flat, reduced ----
# One shoe is made and the other is its mirror image: reduced one by one, the pair came out different. Meshy's right shoe (on -x) is it.
only([high])
bpy.ops.object.duplicate()
low = bpy.context.view_layer.objects.active
low.name = "WhiteSneaker"
bottom = min(v.co.z for v in high.data.vertices)
bm = bmesh.new()
bm.from_mesh(low.data)
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_center_median().x >= 0.0], context="FACES")
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
# The tread sliced off just over its lugs' grooves, under the insole, and the cut closed flat: reduced, the lugs came out a row of teeth.
# Its pattern is baked onto the flat underside from Meshy's.
sole = bottom + TREAD_CUT
cut = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0.0, 0.0, sole), plane_no=(0.0, 0.0, 1.0),
                             clear_inner=True)
rim = [e for e in cut["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
filled = bmesh.ops.holes_fill(bm, edges=rim, sides=0)
bmesh.ops.triangulate(bm, faces=filled["faces"])
bm.to_mesh(low.data)
bm.free()
mod = low.modifiers.new("Reduce", "DECIMATE")
mod.decimate_type = "COLLAPSE"
mod.ratio = min(1.0, SHOE_TRIANGLES / sum(len(p.vertices) - 2 for p in low.data.polygons))
mod.use_collapse_triangulate = True
only([low])
bpy.ops.object.modifier_apply(modifier="Reduce")
print("one shoe: %d triangles, its sole flat %.3f over Meshy's bottom" % (len(low.data.polygons), TREAD_CUT))

# ---- 3. unwrapped, the colour baked across ----
only([low])
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.004, area_weight=1.0, scale_to_bounds=True)
bpy.ops.uv.pack_islands(rotate=True, margin=0.004)
bpy.ops.object.mode_set(mode="OBJECT")
low.data.shade_smooth()
low.data.set_sharp_from_angle(angle=np.radians(35.0))

scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.samples = 4
scene.cycles.device = "CPU"
scene.render.bake.use_selected_to_active = True
scene.render.bake.cage_extrusion = REACH
scene.render.bake.max_ray_distance = REACH * 2.0
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
target = bpy.data.images.new("WhiteSneakerColour", TEXTURE_SIZE, TEXTURE_SIZE, alpha=False)
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
settings.file_format, settings.color_mode, settings.quality = "JPEG", "RGB", 85
target.save_render(OUT_JPG, scene=scene)
print("WROTE %s: %d bytes" % (OUT_JPG, os.path.getsize(OUT_JPG)))

# ---- 4. on our feet ----
# The game's (X, Y, Z) is Blender's (-x, z, -y): the right foot's ankle at X = +HIP_X is Blender x = -HIP_X, the toe (Meshy's -y) forward.
# The left shoe is the right one mirrored across the body's middle (its faces turned back round), the same UVs.
axis = legs["right"]
verts = np.array([v.co[:] for v in low.data.vertices])
verts[:, 0] = (verts[:, 0] - axis[0]) * SCALE - HIP_X
verts[:, 1] = (verts[:, 1] - axis[1]) * SCALE
verts[:, 2] = (verts[:, 2] - sole) * SCALE
low.data.vertices.foreach_set("co", verts.ravel())
low.data.update()
print("each shoe on its foot: %.3f long, %.3f wide, %.3f to the collar's top; heel %.3f behind the ankle, toe %.3f ahead" % (
    np.ptp(verts[:, 1]), np.ptp(verts[:, 0]), verts[:, 2].max(), verts[:, 1].max(), -verts[:, 1].min()))
bm = bmesh.new()
bm.from_mesh(low.data)
mirrored = bmesh.ops.duplicate(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:])
for v in (g for g in mirrored["geom"] if isinstance(g, bmesh.types.BMVert)):
    v.co.x = -v.co.x
bmesh.ops.reverse_faces(bm, faces=[g for g in mirrored["geom"] if isinstance(g, bmesh.types.BMFace)])
bm.to_mesh(low.data)
bm.free()
low.data.update()

low.data.materials.clear()
low.vertex_groups.clear()
only([low])
bpy.ops.export_scene.gltf(filepath=OUT_GLB, export_format="GLB", use_selection=True, export_materials="NONE",
                          export_normals=True, export_texcoords=True, export_apply=True)
print("WROTE %s: %d triangles" % (OUT_GLB, len(low.data.polygons)))
