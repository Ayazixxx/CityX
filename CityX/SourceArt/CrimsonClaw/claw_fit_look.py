"""Render a held-body fit (docs/CRIMSON_CLAW.md): blender -b -P claw_fit_look.py -- <CrimsonClaw.glb> <out prefix> size pitch roll yaw psi px py pz"""
import bpy, sys, math
import numpy as np
from mathutils import Vector, Matrix

args = sys.argv[sys.argv.index("--") + 1:]
glb, out = args[0], args[1]
S, pitch, roll, yaw, psi = map(float, args[2:7])
p = np.array(list(map(float, args[7:10])))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
for o in list(bpy.context.scene.objects):
    if o.type == 'MESH' and o.name != 'RightClaw':
        bpy.data.objects.remove(o)
claw = bpy.data.objects['RightClaw']
me = claw.data
me.transform(claw.matrix_world); claw.matrix_world = Matrix.Identity(4)
for v in me.vertices:   # to Unity model coords (a mirror: the picture is mirrored, the fit is not)
    x, y, z = v.co
    v.co = Vector((-x, z, -y))
me.flip_normals()
V = np.array([v.co[:] for v in me.vertices])
wrist = np.array([(V[:, 0].min() + V[:, 0].max()) / 2, (V[:, 1].min() + V[:, 1].max()) / 2, V[:, 2].min()])

def rot(px, py, pz):
    x, y, z = map(math.radians, (px, py, pz))
    Rx = np.array([[1, 0, 0], [0, math.cos(x), -math.sin(x)], [0, math.sin(x), math.cos(x)]])
    Ry = np.array([[math.cos(y), 0, math.sin(y)], [0, 1, 0], [-math.sin(y), 0, math.cos(y)]])
    Rz = np.array([[math.cos(z), -math.sin(z), 0], [math.sin(z), math.cos(z), 0], [0, 0, 1]])
    return Ry @ Rx @ Rz

# the claw turned about its wrist; everything in model units x S = metres, the wrist at the origin
Rc = rot(pitch, yaw, roll)
for v in me.vertices:
    q = Rc @ (np.array(v.co[:]) - wrist)
    v.co = Vector((q * S).tolist())
mid = np.array([0, 1.4, 0])
Rb = rot(0, psi, 0)
centre = Rc @ p * S

def place(a):   # a body point (metres, feet at origin) to the world here
    return Vector((centre + Rb @ (np.array(a, float) - mid)).tolist())

skin = bpy.data.materials.new("skin"); skin.diffuse_color = (0.55, 0.38, 0.3, 1)
white = bpy.data.materials.new("white"); white.diffuse_color = (0.92, 0.92, 0.9, 1)
me.materials.append(white)

def capsule(a, b, r):
    A, B = place(a), place(b)
    for c in (A, B):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=c, segments=20, ring_count=10)
        bpy.context.object.data.materials.append(skin)
    d = B - A
    if d.length > 1e-4:
        bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=d.length, location=(A + B) / 2, vertices=20)
        cyl = bpy.context.object
        cyl.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
        cyl.data.materials.append(skin)

capsule((0, 1.0, 0), (0, 1.64, 0), 0.26); capsule((-0.17, 0.9, 0), (0.17, 0.9, 0), 0.15)
capsule((-0.35, 1.84, 0), (0.35, 1.84, 0), 0.12)
capsule((-0.35, 1.84, 0), (-0.52, 0.95, 0), 0.09); capsule((0.35, 1.84, 0), (0.52, 0.95, 0), 0.09)
capsule((0, 2.25, 0), (0, 2.25, 0), 0.35)
capsule((-0.17, 0.9, 0), (-0.17, 0.14, 0), 0.135); capsule((0.17, 0.9, 0), (0.17, 0.14, 0), 0.135)
capsule((0, 2.25, 0.3), (0, 2.25, 0.3), 0.08)   # a nose: which way the body faces

# back to Blender's frame (z up) to look at: Unity (x, y, z) -> (-x, -z, y)
M = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
for o in bpy.context.scene.objects:
    if o.type == 'MESH':
        o.matrix_world = M @ o.matrix_world
toB = lambda v: Vector((-v[0], -v[2], v[1]))
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'; scene.display.shading.color_type = 'MATERIAL'; scene.display.shading.show_cavity = True
scene.render.resolution_x = scene.render.resolution_y = 450
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scene.collection.objects.link(cam); scene.camera = cam
look = toB(centre.tolist())
# Blender's view of Unity coords: y is up here, so the camera's up is +y
for name, d in (("front", (0, 0.15, 1)), ("outer", (-1, 0.15, 0)), ("inner", (1, 0.15, 0.2)), ("threequarter", (-0.7, 0.3, 0.7)), ("above", (0.05, 1, 0.3))):
    dv = toB(Vector(d).normalized())
    cam.location = look + dv * 4.5
    cam.rotation_euler = (look - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = out + name + ".png"
    bpy.ops.render.render(write_still=True)
