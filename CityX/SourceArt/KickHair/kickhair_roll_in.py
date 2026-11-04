# THE KICK HAIRSTYLE'S EDGES ROLLED INTO THE HEAD (owner, 2026-11-04: "there is a slit where you can see underneath the hair and the sky shows behind it, can you make it
# roll inward into the head"). The hair is one open shell standing up to 8 cm off the head, and a surface is only drawn from outside: looking in under its edge, through the
# gap, you saw through the far side of the shell to the sky. Every open edge is carried on, curling under and in, to end inside the head, so there is no way in under it.
# The head is where the owner's fit puts it (position 0, -2.27, 0, scale 1): in the model, a sphere round (0, 2.27, 0), radius 0.35, in glTF's axes.
# Run it on KickHair-reduced.glb (kickhair_reduce.py's output), never on its own output: then CityX.UIEditor.HairAssets.RebakeKickHair.
#   blender --background --factory-startup --python kickhair_roll_in.py -- KickHair-reduced.glb ../../Assets/CityX/Character/Hair/KickHair.glb [--render <folder>]
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
source, out = args[0], args[1]
render_folder = args[args.index("--render") + 1] if "--render" in args else None

HEAD = Vector((0.0, 0.0, 2.27))   # Blender's axes: glTF's y is its z
HEAD_RADIUS = 0.35
INSIDE_HEAD = 0.05       # how far under the head's surface the edge ends
CURL_OUT = 0.15          # how far the edge carries on the way it was going before it turns in, as a share of its way to the head: more, and the locks' thin tips go fat
SMALLEST_LOOP = 8        # an opening of fewer edges is a pinhole: filled
CURL_WELD = 0.02         # the open edge is very finely cut (under 1 cm a side), and only the edge itself draws the locks' outline: past it, corners are merged to save triangles
BURIED_WELD = 0.05       # and the part buried in the head is nobody's to see

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=source)
hair = next(o for o in bpy.context.scene.objects if o.type == "MESH")
bpy.context.view_layer.objects.active = hair
hair.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

mesh = bmesh.new()
mesh.from_mesh(hair.data)
before = sum(len(f.verts) - 2 for f in mesh.faces)
outward_before = sum(1 for f in mesh.faces if f.normal.dot(f.calc_center_median() - HEAD) > 0.0)
print("before: %d triangles, %d of %d faces facing away from the head" % (before, outward_before, len(mesh.faces)))


def open_loops():
    seen = set()
    loops = []
    for start in mesh.edges:
        if start in seen or not start.is_boundary:
            continue
        group, stack = [], [start]
        seen.add(start)
        while stack:
            edge = stack.pop()
            group.append(edge)
            for vert in edge.verts:
                for other in vert.link_edges:
                    if other.is_boundary and other not in seen:
                        seen.add(other)
                        stack.append(other)
        loops.append(group)
    return loops


pinholes = [loop for loop in open_loops() if len(loop) < SMALLEST_LOOP]
for loop in pinholes:
    bmesh.ops.holes_fill(mesh, edges=loop, sides=0)
print("filled %d pinholes" % len(pinholes))


def out_of_the_surface(vert):
    """The way the surface was going as it reached this open edge: out of its faces, past the edge, along the surface."""
    direction = Vector((0.0, 0.0, 0.0))
    for edge in vert.link_edges:
        if edge.is_boundary:
            face = edge.link_faces[0]
            along = (edge.other_vert(vert).co - vert.co).normalized()
            across = face.normal.cross(along).normalized()
            if across.dot(face.calc_center_median() - vert.co) > 0.0:
                across = -across
            direction += across
    return direction.normalized() if direction.length > 1e-6 else Vector((0.0, 0.0, 0.0))


def end_in_head(point):
    from_head = point - HEAD
    return HEAD + from_head.normalized() * (HEAD_RADIUS - INSIDE_HEAD) if from_head.length > 1e-6 else HEAD.copy()


rim = [e for e in mesh.edges if e.is_boundary]
rim_verts = {v for e in rim for v in e.verts}
start = {v: v.co.copy() for v in rim_verts}
carry = {v: out_of_the_surface(v) for v in rim_verts}
print("rolling %d open edges" % len(rim))

result = bmesh.ops.extrude_edge_only(mesh, edges=rim)
curl = [g for g in result["geom"] if isinstance(g, bmesh.types.BMVert)]
# The extruded corner knows the rim corner it came from by its one edge back to it.
for vert in curl:
    back = next(e.other_vert(vert) for e in vert.link_edges if e.other_vert(vert) in start)
    p = start[back]
    way = (end_in_head(p) - p).length
    # Partway along the curve from the edge to the head, carried on past the edge first.
    control = p + carry[back] * way * CURL_OUT
    t = 0.45
    vert.co = (1.0 - t) * (1.0 - t) * p + 2.0 * (1.0 - t) * t * control + t * t * end_in_head(p)
bmesh.ops.remove_doubles(mesh, verts=curl, dist=CURL_WELD)

result = bmesh.ops.extrude_edge_only(mesh, edges=[e for e in mesh.edges if e.is_boundary])
buried = [g for g in result["geom"] if isinstance(g, bmesh.types.BMVert)]
fresh = set(buried)
for vert in buried:
    back = next(e.other_vert(vert) for e in vert.link_edges if e.other_vert(vert) not in fresh)
    vert.co = end_in_head(back.co)
bmesh.ops.remove_doubles(mesh, verts=buried, dist=BURIED_WELD)
bmesh.ops.dissolve_degenerate(mesh, dist=1e-5, edges=mesh.edges[:])
# Merging can leave a short open edge on the curl: pulled into the head with the rest.
for vert in {v for e in mesh.edges if e.is_boundary for v in e.verts}:
    if (vert.co - HEAD).length > HEAD_RADIUS - 0.02:
        vert.co = end_in_head(vert.co)
bmesh.ops.triangulate(mesh, faces=[f for f in mesh.faces if len(f.verts) > 3])
mesh.to_mesh(hair.data)
mesh.free()
hair.data.validate()
hair.data.update()
# Validating can drop a bad face and open an edge of its own: the last pass after that.
mesh = bmesh.new()
mesh.from_mesh(hair.data)
for vert in {v for e in mesh.edges if e.is_boundary for v in e.verts}:
    if (vert.co - HEAD).length > HEAD_RADIUS - 0.02:
        vert.co = end_in_head(vert.co)
mesh.to_mesh(hair.data)
mesh.free()
hair.data.update()
for polygon in hair.data.polygons:
    polygon.use_smooth = True

check = bmesh.new()
check.from_mesh(hair.data)
left = [e for e in check.edges if e.is_boundary]
outside = [e for e in left if max((v.co - HEAD).length for v in e.verts) > HEAD_RADIUS]
print("after: %d triangles; %d open edges left, %d of them outside the head" % (sum(len(f.verts) - 2 for f in check.faces), len(left), len(outside)))
check.free()

bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_materials="NONE", export_texcoords=False, export_normals=True, use_selection=False)
print("wrote " + out)

if render_folder:
    # Looking up under the edges, against a sky the same colour the slit showed, with the head in place.
    bpy.ops.mesh.primitive_uv_sphere_add(radius=HEAD_RADIUS, location=HEAD, segments=48, ring_count=32)
    head = bpy.context.object
    bpy.ops.object.shade_smooth()
    skin = bpy.data.materials.new("skin")
    skin.diffuse_color = (0.9, 0.75, 0.2, 1.0)
    head.data.materials.append(skin)
    dark = bpy.data.materials.new("hair")
    dark.diffuse_color = (0.08, 0.05, 0.03, 1.0)
    hair.data.materials.append(dark)
    world = bpy.data.worlds.new("sky")
    world.color = (0.45, 0.65, 0.95)
    bpy.context.scene.world = world
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.show_backface_culling = True
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.background_type = "WORLD"
    scene.render.resolution_x = 700
    scene.render.resolution_y = 700
    camera_data = bpy.data.cameras.new("cam")
    camera_data.lens = 70
    camera = bpy.data.objects.new("cam", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    for name, azimuth, height in (("back-low", 0.0, -0.9), ("back-left-low", 50.0, -0.8), ("left-low", 95.0, -0.7), ("front-low", 180.0, -0.7), ("back", 0.0, 0.1), ("right-low", -95.0, -0.7)):
        a = math.radians(azimuth)
        camera.location = HEAD + Vector((math.sin(a) * 2.2, math.cos(a) * 2.2, height))
        direction = HEAD - camera.location
        camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(render_folder, "kick-roll-" + name + ".png")
        bpy.ops.render.render(write_still=True)
