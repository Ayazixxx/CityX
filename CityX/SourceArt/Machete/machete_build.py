# THE MACHETE (owner, 2026-11-23, with Meshy's "Machete" and a photo of a plain machete: "put this machete in the game as a one handed sword,
# texture it so the blade is silver and the handle is black with those 3 silver dots, also lower the face count to a reasonable amount and get rid of
# the square grid pattern on the handle just make it smooth"; docs/MACHETE.md).
#
# Meshy's model is 1.9 m long, 188,000 triangles, nearly all of them the handle's chequered grip; its blade is 3,000. So:
#   * the BLADE is Meshy's own, cut where the handle starts and reduced;
#   * the HANDLE is made new, smooth: rings round the line of the old one, each as tall and as thick as the old handle there (its finger lip by the
#     blade and its hook at the butt are kept), its grooves left out (the outline is read through a short rolling maximum, which steps over them);
#   * the RIVETS are three low silver domes on each face, where the photo has them (an eighth, nearly a half and three quarters of the way down).
# Laid in the game's sword frame: the guard (where the handle meets the blade) at the origin on the handle's middle line, the blade up, its edge (the
# thin side, Meshy's bottom) toward the sword's -X, which is Blender's +X here (glTF and the importer mirror X), the flats facing Blender's +-Y.
#
# "C:/Program Files/Blender Foundation/Blender 4.2/blender.exe" --background --factory-startup --python CityX/SourceArt/Machete/machete_build.py --
#     <Meshy_AI_Machete.fbx> --export CityX/Assets/CityX/Weapons/Machete.glb [--render <folder>]
import math
import os
import sys

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector

args = sys.argv[sys.argv.index("--") + 1:]
SOURCE = args[0]
EXPORT = args[args.index("--export") + 1] if "--export" in args else None
RENDER = args[args.index("--render") + 1] if "--render" in args else None

LENGTH = 1.21               # the machete end to end on the 2.6 m body, metres (a real one's 70 cm at the game's 1.3x, and 1.1x that: owner, 2026-11-23)
BLADE_TRIANGLES = 1600
HANDLE_STEP = 0.008         # between the handle's rings, in Meshy's metres
RING_POINTS = 20
SQUIRCLE = 4.0              # the handle's cross-section: a rectangle with round corners (2 would be an ellipse)
HANDLE_FROM_Y = 0.012       # Meshy's handle is the part thicker than this either side (the blade is 8 mm)
OVERLAP = 0.01              # the blade is kept this far into the handle, so no gap shows between them
RIVETS_AT = (0.125, 0.46, 0.73)  # down the handle from the blade, as shares of its length (the photo's three dots)
RIVET_RADIUS = 0.0085       # Meshy's metres
RIVET_PROUD = 0.0018
RIVET_SINK = 0.0005

STEEL = (0.78, 0.79, 0.82, 1.0)
GRIP = (0.04, 0.04, 0.04, 1.0)


def material(name, colour):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = colour
    m.diffuse_color = colour
    return m


def rolling(values, size, pick):
    out = []
    for i in range(len(values)):
        lo, hi = max(0, i - size), min(len(values), i + size + 1)
        out.append(pick(values[lo:hi]))
    return np.array(out)


def smoothed(values):
    padded = np.concatenate([[values[0]], values, [values[-1]]])
    return (padded[:-2] + 2.0 * padded[1:-1] + padded[2:]) / 4.0


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SOURCE)
meshy = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
meshy.data.transform(meshy.matrix_world)
meshy.matrix_world = Matrix.Identity(4)
co = np.array([v.co[:] for v in meshy.data.vertices])

# ---- the handle's outline, measured ----
handle = co[np.abs(co[:, 1]) > HANDLE_FROM_Y]
front, butt = handle[:, 0].min(), handle[:, 0].max()
stations = np.arange(front, butt, HANDLE_STEP)
tops, bottoms, halves = [], [], []
for x in stations:
    s = handle[(handle[:, 0] >= x - HANDLE_STEP * 0.5) & (handle[:, 0] < x + HANDLE_STEP * 0.5)]
    tops.append(s[:, 2].max())
    bottoms.append(s[:, 2].min())
    halves.append(np.abs(s[:, 1]).max())
tops = smoothed(rolling(np.array(tops), 1, np.max))
bottoms = smoothed(rolling(np.array(bottoms), 1, np.min))
halves = smoothed(rolling(np.array(halves), 1, np.max))
centre_y = float((handle[:, 1].max() + handle[:, 1].min()) * 0.5)
# The grip's middle line: halfway between its top and its bottom along the stretch the hand closes on (not the lip or the hook).
grip = (stations > front + (butt - front) * 0.25) & (stations < front + (butt - front) * 0.75)
middle_z = float((np.median(tops[grip]) + np.median(bottoms[grip])) * 0.5)
print(f"HANDLE from x {front:.3f} to {butt:.3f}, {len(stations)} rings, middle line z {middle_z:.4f} y {centre_y:.4f}")


def section(x, top, bottom, half):
    ring = []
    centre, height = (top + bottom) * 0.5, (top - bottom) * 0.5
    for i in range(RING_POINTS):
        a = 2.0 * math.pi * i / RING_POINTS
        c, s = math.cos(a), math.sin(a)
        y = math.copysign(abs(c) ** (2.0 / SQUIRCLE), c) * half
        z = math.copysign(abs(s) ** (2.0 / SQUIRCLE), s) * height
        ring.append((x, centre_y + y, centre + z))
    return ring


bm = bmesh.new()
rings = [section(x, t, b, h) for x, t, b, h in zip(stations, tops, bottoms, halves)]
# The butt rounded off: one more ring past the end, smaller.
rings.append(section(butt + HANDLE_STEP * 0.4, tops[-1] - 0.006, bottoms[-1] + 0.010, halves[-1] * 0.6))
verts = [[bm.verts.new(p) for p in ring] for ring in rings]
for a, b in zip(verts[:-1], verts[1:]):
    for i in range(RING_POINTS):
        j = (i + 1) % RING_POINTS
        bm.faces.new((a[i], a[j], b[j], b[i]))
bm.faces.new(list(reversed(verts[0])))
bm.faces.new(verts[-1])
bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
handle_mesh = bpy.data.meshes.new("Handle")
bm.to_mesh(handle_mesh)
bm.free()
handle_obj = bpy.data.objects.new("Handle", handle_mesh)
bpy.context.scene.collection.objects.link(handle_obj)


def half_at(x):
    return float(np.interp(x, stations, halves))


# ---- the rivets: low domes on both faces ----
bm = bmesh.new()
for share in RIVETS_AT:
    x = front + (butt - front) * share
    for side in (1.0, -1.0):
        dome = bmesh.new()
        bmesh.ops.create_uvsphere(dome, u_segments=12, v_segments=8, radius=RIVET_RADIUS)
        bmesh.ops.delete(dome, geom=[v for v in dome.verts if v.co.y < -1e-6], context="VERTS")
        for v in dome.verts:
            v.co = Vector((v.co.x, v.co.y * RIVET_PROUD / RIVET_RADIUS, v.co.z))
        base = half_at(x) - RIVET_SINK
        for v in dome.verts:
            v.co = Vector((x + v.co.x, centre_y + side * (base + v.co.y), middle_z + v.co.z))
        if side < 0.0:
            bmesh.ops.reverse_faces(dome, faces=dome.faces)
        scratch = bpy.data.meshes.new("dome")
        dome.to_mesh(scratch)
        dome.free()
        bm.from_mesh(scratch)
        bpy.data.meshes.remove(scratch)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
rivet_mesh = bpy.data.meshes.new("Rivets")
bm.to_mesh(rivet_mesh)
bm.free()
rivet_obj = bpy.data.objects.new("Rivets", rivet_mesh)
bpy.context.scene.collection.objects.link(rivet_obj)

# ---- the blade: Meshy's own, cut where the handle starts, reduced ----
blade_obj = meshy
blade_obj.name = blade_obj.data.name = "Blade"
bm = bmesh.new()
bm.from_mesh(blade_obj.data)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.x > front + OVERLAP], context="VERTS")
bm.to_mesh(blade_obj.data)
bm.free()
bpy.context.view_layer.objects.active = blade_obj
blade_obj.select_set(True)
faces = len(blade_obj.data.polygons)
decimate = blade_obj.modifiers.new("reduce", "DECIMATE")
decimate.ratio = min(1.0, BLADE_TRIANGLES / max(1, faces))
bpy.ops.object.modifier_apply(modifier="reduce")
bpy.ops.object.select_all(action="DESELECT")

# ---- into the sword frame ----
origin = Vector((front, centre_y, middle_z))
scale = LENGTH / float(co[:, 0].max() - co[:, 0].min())
# Meshy's -X (the tip) to +Z, its -Z (the edge) to +X: half a turn about (1, 0, -1).
turn = Matrix.Rotation(math.pi, 4, Vector((1.0, 0.0, -1.0)).normalized())
frame = Matrix.Scale(scale, 4) @ turn @ Matrix.Translation(-origin)
for obj, mat in ((blade_obj, material("MacheteSteel", STEEL)), (handle_obj, material("MacheteGrip", GRIP)), (rivet_obj, material("MacheteRivet", STEEL))):
    obj.data.transform(frame)
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    obj.data.shade_smooth()
    if hasattr(obj.data, "set_sharp_from_angle"):
        obj.data.set_sharp_from_angle(angle=math.radians(50.0))
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.select_set(False)

total = 0
for obj in (blade_obj, handle_obj, rivet_obj):
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    total += tris
    pts = np.array([v.co[:] for v in obj.data.vertices])
    print(f"PART {obj.name}: {tris} triangles, x {pts[:,0].min():+.3f}..{pts[:,0].max():+.3f} y {pts[:,1].min():+.3f}..{pts[:,1].max():+.3f} z {pts[:,2].min():+.3f}..{pts[:,2].max():+.3f}")
print(f"TOTAL {total} triangles, scale {scale:.4f}")

if EXPORT:
    for obj in bpy.context.scene.objects:
        obj.select_set(obj in (blade_obj, handle_obj, rivet_obj))
    bpy.ops.export_scene.gltf(filepath=os.path.abspath(EXPORT), export_format="GLB", use_selection=True, export_apply=True,
                              export_yup=True, export_normals=True, export_texcoords=True, export_materials="EXPORT")
    print("EXPORTED", EXPORT)

if RENDER:
    os.makedirs(RENDER, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.light = "STUDIO"
    scene.render.resolution_x, scene.render.resolution_y = 1600, 600
    world = bpy.data.worlds.new("w")
    scene.world = world
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.type = "ORTHO"
    views = {
        "side": ((0.0, -3.0, LENGTH * 0.35), 1.25),
        "handle": ((0.0, -1.0, -0.12), 0.40),
        "handle_back": ((0.0, 1.0, -0.12), 0.40),
        "edge": ((-3.0, 0.0, LENGTH * 0.35), 1.25),
    }
    for name, (eye, size) in views.items():
        cam.location = eye
        target = Vector((0.0, 0.0, eye[2]))
        cam.rotation_euler = (target - Vector(eye)).to_track_quat("-Z", "Z").to_euler()
        if name in ("side", "edge"):
            cam.rotation_euler = (target - Vector(eye)).to_track_quat("-Z", "X").to_euler()
        cam.data.ortho_scale = size
        scene.render.filepath = os.path.join(RENDER, f"machete_{name}.png")
        bpy.ops.render.render(write_still=True)
