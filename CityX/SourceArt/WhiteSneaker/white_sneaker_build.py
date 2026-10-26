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
HERE = os.path.dirname(os.path.abspath(__file__))

SHOE_TRIANGLES = 7000            # one shoe (the pair is twice this)


def run(path):
    exec(compile(open(path).read(), path, "exec"), globals())


run(os.path.join(HERE, "sneaker_meshy.py"))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
high = [o for o in bpy.data.objects if o.type == "MESH"][0]
only([high])
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
image = bpy.data.images.load(COLOUR)
pixels = np.array(image.pixels[:], dtype=np.float32).reshape(image.size[1], image.size[0], 4)

# ---- 1. the legs off ----
legs = take_legs_off(high, face_colours(high.data, pixels).max(1) < GREY)

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
# The left shoe is the right one mirrored across the body's middle (its faces turned back round), the same UVs.
verts = on_our_feet(np.array([v.co[:] for v in low.data.vertices]), legs["right"], sole)
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
