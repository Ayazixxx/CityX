# THE WRAITH'S NEW HEAD AND BODY, from the owner's Meshy model (2026-11-05: "make the head replaced with this model's head, I also like the body of the model", and "the model's
# arms are all fucked up because they are meshy generated" so "use the arms you have now that are the same as the playermodel"):
#   blender --background --factory-startup --python wraith_body.py -- <the Meshy fbx> [--render <out_dir>]
#
# Takes the model's arms off at the sides of the torso, closes the openings, stands it in the body's frame with its head the size and place of the player's, cuts it down, and writes
# PhantomMawBody.glb beside this file. The shoulder joints it is cut at are where the player's arms stand, so their tops cover the cuts.
import os
import sys

import bmesh
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
args = sys.argv[sys.argv.index("--") + 1:]
SOURCE = args[0]

# On the model, measured slice by slice: its head.
MODEL_HEAD_CENTRE_Z = 0.68
MODEL_HEAD_RADIUS = 0.27

# On the player (FusedBodyShape.HeadRadius, BodyRig).
HEAD_RADIUS = 0.35
HEAD_CENTRE_Y = 2.25

SCALE = HEAD_RADIUS / MODEL_HEAD_RADIUS

# Where the arms come off, on the model: the torso is within 0.21 either side, its arms from 0.24 out.
ARM_CUT_X = 0.235

# And the top of the shoulders: above this is the head, and nothing is cut.
HEAD_BOTTOM_Z = 0.50

# The triangles it keeps. The grooves down the tail and the teeth are its look.
TARGET_TRIANGLES = 20000

# Pieces smaller than this share of the whole are slivers of arm left by the cut.
SLIVER_SHARE = 0.01

# What counts as a tooth, in the body's frame: outside the head's ball by this share of its radius, this far forward, and above the chest.
TOOTH_OUTSIDE_SHARE = 1.01
TOOTH_FRONT_Y = -0.14
TOOTH_LOWEST_Z = 1.87

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SOURCE)
ob = next(o for o in bpy.context.scene.objects if o.type == "MESH")
bpy.context.view_layer.objects.active = ob
ob.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

bm = bmesh.new()
bm.from_mesh(ob.data)
# Only below the head: it is wider than the torso. First a clean line round the body there, then the two cuts on what is under it.
geometry = list(bm.verts) + list(bm.edges) + list(bm.faces)
bmesh.ops.bisect_plane(bm, geom=geometry, plane_co=Vector((0.0, 0.0, HEAD_BOTTOM_Z)), plane_no=Vector((0.0, 0.0, 1.0)))
for side in (1.0, -1.0):
    below = [v for v in bm.verts if v.co.z <= HEAD_BOTTOM_Z + 1e-5]
    faces = [f for f in bm.faces if all(v.co.z <= HEAD_BOTTOM_Z + 1e-5 for v in f.verts)]
    edges = [e for e in bm.edges if all(v.co.z <= HEAD_BOTTOM_Z + 1e-5 for v in e.verts)]
    bmesh.ops.bisect_plane(bm, geom=below + edges + faces, plane_co=Vector((side * ARM_CUT_X, 0.0, 0.0)), plane_no=Vector((side, 0.0, 0.0)), clear_outer=True, clear_inner=False)

# Slivers of the arms' inner sides, left inside the cut.
bm.verts.ensure_lookup_table()
seen = set()
islands = []
for start in bm.verts:
    if start in seen:
        continue
    stack, island = [start], []
    seen.add(start)
    while stack:
        v = stack.pop()
        island.append(v)
        for e in v.link_edges:
            other = e.other_vert(v)
            if other not in seen:
                seen.add(other)
                stack.append(other)
    islands.append(island)
total = len(bm.verts)
removed = 0
for island in islands:
    if len(island) < total * SLIVER_SHARE:
        removed += len(island)
        bmesh.ops.delete(bm, geom=island, context='VERTS')
print("pieces after the cut: %d, slivers removed: %d verts" % (len(islands), removed))

boundary = [e for e in bm.edges if e.is_boundary]
print("open edges after the cut: %d" % len(boundary))
bmesh.ops.holes_fill(bm, edges=boundary, sides=0)
bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
bm.to_mesh(ob.data)
bm.free()

# Stand it in the body's frame: Blender axes, facing -Y, up +Z, the feet's floor at 0.
for v in ob.data.vertices:
    v.co = Vector((v.co.x * SCALE, v.co.y * SCALE, (v.co.z - MODEL_HEAD_CENTRE_Z) * SCALE + HEAD_CENTRE_Y))
ob.data.update()
ob.name = "PhantomMawBody"

triangles = sum(len(p.vertices) - 2 for p in ob.data.polygons)
modifier = ob.modifiers.new("Reduce", "DECIMATE")
modifier.decimate_type = 'COLLAPSE'
modifier.ratio = TARGET_TRIANGLES / float(triangles)
bpy.ops.object.modifier_apply(modifier=modifier.name)
triangles_after = sum(len(p.vertices) - 2 for p in ob.data.polygons)
zs = [v.co.z for v in ob.data.vertices]
print("triangles %d -> %d; stands %.2f..%.2f m; across %.2f m" % (triangles, triangles_after, min(zs), max(zs), 2.0 * max(abs(v.co.x) for v in ob.data.vertices)))

# The teeth as their own object: the owner's wraith has solid white teeth on a see-through body. They are what stands out of the head's ball, at the front, above the chest.
HEAD_CENTRE = Vector((0.0, 0.0, HEAD_CENTRE_Y))


def is_tooth(co):
    return (co - HEAD_CENTRE).length > HEAD_RADIUS * TOOTH_OUTSIDE_SHARE and co.y < TOOTH_FRONT_Y and TOOTH_LOWEST_Z < co.z < HEAD_CENTRE_Y


teeth_ob = ob.copy()
teeth_ob.data = ob.data.copy()
teeth_ob.name = "PhantomMawTeeth"
bpy.context.scene.collection.objects.link(teeth_ob)

bm = bmesh.new()
bm.from_mesh(teeth_ob.data)
bmesh.ops.delete(bm, geom=[f for f in bm.faces if not all(is_tooth(v.co) for v in f.verts)], context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
print("teeth: %d triangles" % sum(len(f.verts) - 2 for f in bm.faces))
bm.to_mesh(teeth_ob.data)
bm.free()

bm = bmesh.new()
bm.from_mesh(ob.data)
bmesh.ops.delete(bm, geom=[f for f in bm.faces if all(is_tooth(v.co) for v in f.verts)], context='FACES')
bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=0)
bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])

# The eye sockets: what lies well inside the head's ball, at the front and above its middle.
for side, label in ((1.0, "left"), (-1.0, "right")):
    deep = [v.co.copy() for v in bm.verts if (v.co - HEAD_CENTRE).length < HEAD_RADIUS * 0.93 and v.co.y < -0.1 and v.co.x * side > 0.03 and v.co.z > HEAD_CENTRE_Y - 0.05]
    if deep:
        middle = sum(deep, Vector()) / len(deep)
        print("%s socket: %d points, centre in the head's frame (x right, y up, z forward): (%.3f, %.3f, %.3f)" % (label, len(deep), -middle.x, middle.z - HEAD_CENTRE_Y, -middle.y))
print("body: %d triangles" % sum(len(f.verts) - 2 for f in bm.faces))
bm.to_mesh(ob.data)
bm.free()
ob.data.update()
teeth_ob.data.update()

out = os.path.join(HERE, "PhantomMawBody.glb")
ob.select_set(True)
teeth_ob.select_set(True)
bpy.ops.export_scene.gltf(filepath=out, use_selection=True, export_format='GLB', export_apply=True, export_materials='NONE', export_animations=False, export_skins=False)
print("wrote", out, os.path.getsize(out), "bytes")

if "--render" in args:
    out_dir = args[args.index("--render") + 1]
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.color_type = 'OBJECT'
    ob.color = (0.55, 0.62, 0.72, 1.0)
    teeth_ob.color = (1.0, 1.0, 1.0, 1.0)
    scene.render.resolution_x, scene.render.resolution_y = 520, 720
    scene.world = bpy.data.worlds.new("W")
    scene.world.color = (0.55, 0.58, 0.62)
    scene.display.shading.show_shadows = False
    scene.display.shading.show_cavity = True
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    for name, position in (("front", Vector((0.0, -6.0, 1.5))), ("side", Vector((-6.0, 0.0, 1.5))), ("back", Vector((0.0, 6.0, 1.5))), ("three", Vector((-3.6, -4.8, 2.0)))):
        target = Vector((0.0, 0.0, 1.3))
        cam.location = position
        cam.rotation_euler = (target - position).to_track_quat('-Z', 'Y').to_euler()
        cam.data.angle = 0.55
        scene.render.filepath = os.path.join(out_dir, "body_%s.png" % name)
        bpy.ops.render.render(write_still=True)
