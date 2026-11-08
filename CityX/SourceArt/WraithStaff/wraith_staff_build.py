# THE WRAITH STAFF, headless (owner, 2026-11-08: "make an item called "Wraith Staff" ... make the red orb the same color as the wraith's eyes
# and make it glow as well. also lower it to a reasonable face count"):
#   blender --background --factory-startup --python wraith_staff_build.py -- <Meshy Crimson Oracle Staff .fbx> <its colour .png>
#                                                                            <out WraithStaff.glb> <out WraithStaff.jpg>
# Meshy's "Crimson Oracle Staff": a grey shaft and a clawed cage holding a red orb, one surface of 101,932 triangles, a 4096 colour map.
#   1. The orb is the red of the colour map (the cage and shaft are grey): those faces are the orb.
#   2. The staff is stood as the game's staffs are (StaffBuilder): its long axis up, as long as theirs, the grip at the origin and the orb's
#      middle OrbHeight above it.
#   3. The orb and the rest are each reduced (ORB_TRIANGLES, STAFF_TRIANGLES); the rest is unwrapped and Meshy's colour baked across onto one
#      small map. The orb needs none: the game draws it glowing in the Wraith's eyes' red.
#   4. Exported as one model in two parts by material, "WraithStaffBody" and "WraithStaffOrb" (WraithStaffBuilder finds them by name), in
#      the game's frame through glTF (Blender x, y, z -> the game's -x, z, -y).
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
FBX, COLOUR, OUT_GLB, OUT_JPG = (os.path.abspath(a) for a in args[:4])

STAFF_TRIANGLES = 5300           # with the orb, tools/model_qa.py's weapon budget: 6k fine, 10k most
ORB_TRIANGLES = 700              # a ball round to the eye at arm's length (400 showed corners on its outline)
TEXTURE_SIZE = 512
JPEG_QUALITY = 88
LENGTH = 2.36                    # the game's staffs, ferrule to crystal top (StaffBuilder: -0.98 to about 1.38)
ORB_HEIGHT = 1.29                # StaffBuilder.OrbHeight: the head's middle above the grip
RED = 0.3                        # an orb face: red past this, and more than twice its green and blue
REACH = 0.03                     # how far the reduced staff may lie from Meshy's when the colour is baked across (metres)
MATERIALS = ("WraithStaffBody", "WraithStaffOrb")


def only(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[-1]


def face_colours(me, pixels):
    """Each face's colour on the map at the middle of its texture coordinates."""
    h, w = pixels.shape[:2]
    uv = np.zeros(len(me.loops) * 2)
    me.uv_layers.active.data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 2)
    colours = np.zeros((len(me.polygons), 3))
    for i, p in enumerate(me.polygons):
        c = uv[p.loop_start:p.loop_start + p.loop_total].mean(0)
        x = min(w - 1, max(0, int(c[0] % 1.0 * w)))
        y = min(h - 1, max(0, int(c[1] % 1.0 * h)))
        colours[i] = pixels[y, x, :3]
    return colours


def reduce(ob, triangles):
    mod = ob.modifiers.new("Reduce", "DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.ratio = min(1.0, triangles / max(1, len(ob.data.polygons)))
    mod.use_collapse_triangulate = True
    only([ob])
    bpy.ops.object.modifier_apply(modifier="Reduce")


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
only(meshes)
if len(meshes) > 1:
    bpy.ops.object.join()
high = bpy.context.view_layer.objects.active
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = high.data
if me.has_custom_normals:
    bpy.ops.mesh.customdata_custom_splitnormals_clear()
for name in ("sharp_edge", "sharp_face"):
    if name in me.attributes:
        me.attributes.remove(me.attributes[name])
print("in: %d triangles" % sum(len(p.vertices) - 2 for p in me.polygons))

image = bpy.data.images.load(COLOUR)
pixels = np.array(image.pixels[:]).reshape(image.size[1], image.size[0], image.channels)
colours = face_colours(me, pixels)
orb = (colours[:, 0] > RED) & (colours[:, 0] > 2.0 * colours[:, 1]) & (colours[:, 0] > 2.0 * colours[:, 2])
print("orb: %d faces of %d" % (orb.sum(), len(orb)))

# Stood up: the long axis (the largest extent) up, as long as the game's staffs, the orb at the top, its middle ORB_HEIGHT above the grip.
co = np.array([v.co[:] for v in me.vertices])
extent = co.max(0) - co.min(0)
axis = int(np.argmax(extent))
assert axis == 2, "the staff does not stand along z: %s" % extent
scale = LENGTH / extent[axis]
orb_verts = np.unique(np.concatenate([np.array(me.polygons[i].vertices[:]) for i in np.nonzero(orb)[0]]))
orb_middle = co[orb_verts].mean(0)
if orb_middle[2] < co[:, 2].mean():
    co[:, 2] = -co[:, 2]          # the orb at the bottom: turned over
    co[:, 1] = -co[:, 1]
    orb_middle = co[orb_verts].mean(0)
grip = np.array([orb_middle[0], orb_middle[1], orb_middle[2] - ORB_HEIGHT / scale])
co = (co - grip) * scale
me.vertices.foreach_set("co", co.ravel())
me.update()
print("stood: from %s to %s, the orb's middle at %s" % (co.min(0).round(3), co.max(0).round(3), ((orb_middle - grip) * scale).round(3)))

# The orb and the rest as two objects, each reduced.
bm = bmesh.new()
bm.from_mesh(me)
bm.faces.ensure_lookup_table()
orb_faces = [bm.faces[i] for i in np.nonzero(orb)[0]]
orb_bm = bm.copy()
orb_bm.faces.ensure_lookup_table()
bmesh.ops.delete(orb_bm, geom=[f for f in orb_bm.faces if not orb[f.index]], context="FACES")
bmesh.ops.delete(bm, geom=orb_faces, context="FACES")
bm.to_mesh(me)
orb_me = bpy.data.meshes.new("WraithStaffOrb")
orb_bm.to_mesh(orb_me)
orb_ob = bpy.data.objects.new("WraithStaffOrb", orb_me)
bpy.context.scene.collection.objects.link(orb_ob)
bm.free()
orb_bm.free()

only([high])
bpy.ops.object.duplicate()
low = bpy.context.view_layer.objects.active
low.name = "WraithStaff"
reduce(low, STAFF_TRIANGLES)
reduce(orb_ob, ORB_TRIANGLES)
print("reduced: the staff %d triangles, the orb %d" % (len(low.data.polygons), len(orb_ob.data.polygons)))

# The rest unwrapped and Meshy's colour baked across.
only([low])
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.004, area_weight=1.0, scale_to_bounds=True)
bpy.ops.uv.average_islands_scale()
bpy.ops.uv.pack_islands(rotate=True, margin=0.004)
bpy.ops.object.mode_set(mode="OBJECT")
for o in (low, orb_ob):
    o.data.shade_smooth()
low.data.set_sharp_from_angle(angle=np.radians(45.0))

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
source.image = image
emit = nodes.new("ShaderNodeEmission")
out = nodes.new("ShaderNodeOutputMaterial")
shown.node_tree.links.new(source.outputs["Color"], emit.inputs["Color"])
shown.node_tree.links.new(emit.outputs["Emission"], out.inputs["Surface"])
high.data.materials.clear()
high.data.materials.append(shown)
target = bpy.data.images.new("WraithStaffColour", TEXTURE_SIZE, TEXTURE_SIZE, alpha=False)
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

# One model, two parts by material (plain materials: the game puts the map and the glow on them).
bpy.data.objects.remove(high)
for o, name in ((low, MATERIALS[0]), (orb_ob, MATERIALS[1])):
    o.data.materials.clear()
    o.data.materials.append(bpy.data.materials.new(name))
only([orb_ob, low])
bpy.ops.object.join()
staff = bpy.context.view_layer.objects.active
staff.name = staff.data.name = "WraithStaff"
bpy.ops.export_scene.gltf(filepath=OUT_GLB, export_format="GLB", use_selection=True, export_materials="EXPORT",
                          export_normals=True, export_texcoords=True, export_apply=True)
print("WROTE %s: %d triangles" % (OUT_GLB, len(staff.data.polygons)))
