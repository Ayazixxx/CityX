# THE WORN BOOTS (owner, 2026-11-23, with two photos of a short suede ankle boot, tan and a darker brown: "can you make a shoe model ingame that
# looks like something and looks good with the jeans. use the sneakers we built as a base for sizing but these can be a bit bulkier than the
# sneakers, i think the jeans will rest on them a bit better if they are a tad bulkier it will look fashionable, not too bulky", "a bit browner
# like the browner image", "doesnt have to be the exact design just make a boot a homeless person would wear ... use that as a guide though";
# docs/WORN_BOOTS.md).
#
# Made from nothing, our own design: a short ankle boot, a round bulbous toe, a thick sole with a ribbed welt, a low shaft the game's chunky
# ankle goes into, a rolled collar that sags a little, a pull tab at the heel. Each boot is one closed skin lofted from level outlines (an egg
# shaped footprint at the sole drawing back from the toe as it rises into the shaft's oval), with an inner wall and a floor inside the collar.
# Its paint is drawn here too, kept plain (the owner: "just make it brown all over doesnt have to be super detailed"): dark brown suede, a
# little dirt climbing up from the sole, a stitched seam round the vamp, the sole, welt and collar darker.
#
# Sized from the sneakers as worn (0.19 m wide, 0.41 m long, on feet 0.17 m either side of the middle): a quarter wider, a little longer.
# Built in the game's frame (x right, y up, z forward, metres, the boots standing on the body's feet) and written out as Blender has it
# (glTF and the importer mirror x): the game takes it as it takes the sneakers' pair (WornBootsBuilder).
#
# "C:/Program Files/Blender Foundation/Blender 4.2/blender.exe" --background --factory-startup --python CityX/SourceArt/WornBoots/worn_boots_build.py --
#     --export CityX/Assets/CityX/Wearables/Models/WornBoots.glb --texture CityX/Assets/CityX/Wearables/WornBoots.jpg [--render <folder>]
import math
import os
import sys

import bpy
import bmesh
import numpy as np
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]


def arg(name):
    return os.path.abspath(args[args.index(name) + 1]) if name in args else None


EXPORT, TEXTURE, RENDER = arg("--export"), arg("--texture"), arg("--render")

# ---- the shape, in the game's metres (the right boot; the left is its mirror) ----
FOOT_X = 0.17            # the ankle's side of the body's middle (BodyRig.HipX)
TOE = 0.335              # the toe's tip, forward of the ankle
HEEL = -0.120            # the heel's back
BALL_HALF = 0.112        # half the width at the ball of the foot
EGG = 0.10               # how much wider the footprint is toward the toe than toward the heel
SHAFT_Z = 0.025          # the shaft's middle, forward of the ankle (standing, the shin leans forward over the foot: the sneakers' 2.7 cm)
SHAFT_HALF_X = 0.108
SHAFT_HALF_Z = 0.114
SOLE_TOP = 0.040
WELT_OUT = 0.013
UPPER_FROM = 0.060
TOE_HOLDS_TO = 0.075     # up to here the toe keeps its full reach; above, it draws back to the shaft
VAMP_MEETS = 0.185       # where the drawn-back front meets the shaft's front
TOP = 0.275
WALL = 0.014
INSIDE_DEPTH = 0.055
SAG = 0.007              # the collar's edge rides up and down this much round the shaft
AROUND = 28              # points round each outline
UPPER_RINGS = 15

SUEDE = np.array([0.335, 0.270, 0.190])
SUEDE_DARK = np.array([0.250, 0.200, 0.140])
DIRT = np.array([0.185, 0.160, 0.125])
SOLE = np.array([0.170, 0.135, 0.100])
WELT = np.array([0.225, 0.175, 0.120])
THREAD = np.array([0.560, 0.500, 0.390])
LINING = np.array([0.420, 0.380, 0.300])
INSIDE = np.array([0.060, 0.050, 0.040])
TEXTURE_SIZE = 512

# The paint's bands, as V: the sole's wall, the welt, the upper, the collar's roll, the inside.
V_SOLE, V_WELT, V_UPPER, V_COLLAR, V_INSIDE = (0.02, 0.14), (0.15, 0.20), (0.22, 0.88), (0.90, 0.95), (0.96, 1.0)


def outline(height, out=0.0):
    """The boot's level outline at a height, as points (x, z) round from the toe (toward the outer side first), pushed out by `out`."""
    t = min(1.0, max(0.0, (height - TOE_HOLDS_TO) / (VAMP_MEETS - TOE_HOLDS_TO)))
    rise = min(1.0, max(0.0, (height - UPPER_FROM) / (TOP - UPPER_FROM)))
    shaft_front, shaft_back = SHAFT_Z + SHAFT_HALF_Z, SHAFT_Z - SHAFT_HALF_Z
    front = shaft_front + (TOE - shaft_front) * (1.0 - t ** 1.7) ** (1.0 / 1.7)     # a round toe, then the vamp up to the shaft
    back = HEEL + (shaft_back - HEEL) * rise
    half = BALL_HALF + (SHAFT_HALF_X - BALL_HALF) * rise
    egg = EGG * (1.0 - t)
    square = 2.5 - 0.5 * t                                                           # a little square at the toe, an oval at the shaft
    middle, reach = (front + back) * 0.5, (front - back) * 0.5
    points = []
    for i in range(AROUND):
        a = 2.0 * math.pi * i / AROUND
        c, s = math.cos(a), math.sin(a)
        z = math.copysign(abs(c) ** (2.0 / square), c) * reach
        x = math.copysign(abs(s) ** (2.0 / square), s) * half * (1.0 + egg * c)
        if out:
            n = Vector((x / max(1e-6, half) ** 2, z / max(1e-6, reach) ** 2)).normalized()
            x, z = x + n.x * out, z + n.y * out
        points.append((x, middle + z))
    return points


def sag(i):
    a = 2.0 * math.pi * i / AROUND
    return SAG * (math.sin(2.0 * a + 1.0) * 0.6 + math.sin(3.0 * a + 0.4) * 0.4)


# Every ring of the skin from the ground up and back down inside: (height, the outline's height, pushed out, V, how much of the collar's sag).
rings = [(0.000, UPPER_FROM, -0.004, V_SOLE[0], 0.0), (0.007, UPPER_FROM, WELT_OUT - 0.004, 0.05, 0.0),
         (SOLE_TOP, UPPER_FROM, WELT_OUT - 0.004, V_SOLE[1], 0.0),
         (SOLE_TOP + 0.003, UPPER_FROM, WELT_OUT, V_WELT[0], 0.0), (UPPER_FROM - 0.004, UPPER_FROM, WELT_OUT, V_WELT[1] - 0.01, 0.0),
         (UPPER_FROM, UPPER_FROM, 0.003, V_WELT[1], 0.0)]
for k in range(1, UPPER_RINGS + 1):
    f = k / UPPER_RINGS
    h = UPPER_FROM + (TOP - UPPER_FROM) * f
    rings.append((h, h, 0.0, V_UPPER[0] + (V_UPPER[1] - V_UPPER[0]) * f, f ** 3))
rings += [(TOP + 0.005, TOP, 0.007, V_COLLAR[0], 1.0), (TOP + 0.015, TOP, 0.004, 0.925, 1.0), (TOP + 0.016, TOP, -0.006, V_COLLAR[1], 1.0),
          (TOP + 0.006, TOP, -WALL, V_INSIDE[0], 1.0), (TOP - INSIDE_DEPTH, TOP, -WALL - 0.002, 0.99, 0.0)]


def boot(side):
    """One boot's mesh in the game's frame, on the side of the body `side` says (+1 the right)."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    made = []
    for height, at, out, v, sagging in rings:
        made.append([bm.verts.new((FOOT_X + x, height + sag(i) * sagging, z)) for i, (x, z) in enumerate(outline(at, out))])

    def face(verts, uvs):
        f = bm.faces.new(verts)
        for loop, st in zip(f.loops, uvs):
            loop[uv].uv = st

    for r in range(len(rings) - 1):
        v0, v1 = rings[r][3], rings[r + 1][3]
        for i in range(AROUND):
            j = (i + 1) % AROUND
            u0, u1 = i / AROUND, (i + 1) / AROUND
            face((made[r][i], made[r][j], made[r + 1][j], made[r + 1][i]), ((u0, v0), (u1, v0), (u1, v1), (u0, v1)))
    # Under the sole, and the floor inside the collar.
    for ring, v, flip in ((made[0], 0.0, True), (made[-1], 1.0, False)):
        middle = bm.verts.new(sum((p.co for p in ring), Vector()) / AROUND)
        for i in range(AROUND):
            j = (i + 1) % AROUND
            tri = (middle, ring[j], ring[i]) if flip else (middle, ring[i], ring[j])
            us = (0.5, i / AROUND, (i + 1) / AROUND)
            face(tri, tuple((u, v) for u in ((us[0], us[2], us[1]) if flip else us)))
    # The pull tab: a loop of webbing standing up from the collar at the heel.
    back = SHAFT_Z - SHAFT_HALF_Z
    tab = [bm.verts.new((FOOT_X + x, y, back + z)) for x, y, z in (
        (-0.016, TOP - 0.02, -0.004), (0.016, TOP - 0.02, -0.004), (0.016, TOP + 0.045, -0.008), (-0.016, TOP + 0.045, -0.008),
        (-0.016, TOP - 0.02, 0.006), (0.016, TOP - 0.02, 0.006), (0.016, TOP + 0.045, 0.002), (-0.016, TOP + 0.045, 0.002))]
    for quad in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
        face([tab[q] for q in quad], ((0.48, 0.905), (0.52, 0.905), (0.52, 0.945), (0.48, 0.945)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # Out from the boot: a point of the sole's wall must face away from the boot's middle.
    probe = min(bm.faces, key=lambda f: abs(f.calc_center_median().y - 0.02) + abs(f.calc_center_median().z - TOE))
    if probe.normal.z < 0.0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    if side < 0:
        for v in bm.verts:
            v.co.x = -v.co.x
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    # Into Blender's own frame, as glTF and the game's importer will turn it back: x mirrored, the game's forward Blender's -Y.
    for v in bm.verts:
        v.co = Vector((-v.co.x, -v.co.z, v.co.y))
    bmesh.ops.reverse_faces(bm, faces=bm.faces)
    return bm


def paint():
    """The boots' paint: one square map, U round the boot from the toe, V up it (the bands above)."""
    size = TEXTURE_SIZE
    rng = np.random.default_rng(11)
    u, v = np.meshgrid((np.arange(size) + 0.5) / size, (np.arange(size) + 0.5) / size)
    image = np.empty((size, size, 3))

    def blotches(count, small, big):
        field = np.zeros((size, size))
        for _ in range(count):
            cu, cv, r = rng.uniform(0, 1), rng.uniform(0, 1), rng.uniform(small, big)
            du = np.minimum(np.abs(u - cu), 1.0 - np.abs(u - cu))
            field += np.exp(-((du / r) ** 2 + ((v - cv) / r) ** 2))
        return field

    # Plain brown suede all over (owner, 2026-11-23: "just make it brown all over doesnt have to be super detailed").
    image[:] = SUEDE
    upper = (v - V_UPPER[0]) / (V_UPPER[1] - V_UPPER[0])
    front = np.minimum(u, 1.0 - u)                              # 0 at the toe, 0.5 at the heel
    # Dirt climbing up from the sole, ragged, higher at the toe and the heel.
    climb = 0.16 + 0.10 * np.cos(4.0 * math.pi * u) + 0.10 * (blotches(40, 0.02, 0.06) - 0.5)
    image[upper < climb] = DIRT
    image[(upper >= climb) & (upper < climb + 0.035)] = SUEDE_DARK
    # The seam round the vamp, stitched: lower at the sides, a dashed thread beside a dark line.
    seam = 0.62 - 0.18 * np.cos(2.0 * math.pi * u) ** 2
    image[np.abs(upper - seam) < 0.012] = SUEDE_DARK
    image[(np.abs(upper - seam - 0.03) < 0.007) & ((u * 60.0) % 1.0 < 0.55)] = THREAD
    # The sole, the ribbed welt, the collar's roll, the lining, the dark inside.
    image[v < V_SOLE[1] + 0.005] = SOLE
    image[(v < V_SOLE[1] + 0.005) & (v > 0.035) & (blotches(30, 0.01, 0.03) > 0.9)] = DIRT * 1.25
    welt = (v >= V_SOLE[1] + 0.005) & (v < V_UPPER[0] - 0.01)
    image[welt] = WELT
    image[welt & ((u * 90.0) % 1.0 < 0.3)] = WELT * 0.75
    collar = (v >= V_UPPER[1] + 0.01) & (v < V_COLLAR[1] + 0.003)
    image[collar] = SUEDE_DARK
    image[collar & ((u * 90.0) % 1.0 < 0.3)] = SUEDE_DARK * 0.8
    image[v >= V_COLLAR[1] + 0.003] = LINING
    image[v >= 0.975] = INSIDE
    return image


def save_texture(image, path):
    pixels = np.ones((image.shape[0], image.shape[1], 4), dtype=np.float32)
    pixels[:, :, :3] = image
    out = bpy.data.images.new("WornBoots", image.shape[1], image.shape[0], alpha=False)
    out.pixels.foreach_set(pixels.ravel())
    scene = bpy.context.scene
    scene.render.image_settings.file_format = "JPEG"
    scene.render.image_settings.quality = 90
    scene.view_settings.view_transform = "Standard"
    out.save_render(path, scene=scene)
    return out


bpy.ops.wm.read_factory_settings(use_empty=True)
pair = bmesh.new()
for side in (1, -1):
    one = boot(side)
    scratch = bpy.data.meshes.new("one")
    one.to_mesh(scratch)
    one.free()
    pair.from_mesh(scratch)
    bpy.data.meshes.remove(scratch)
mesh = bpy.data.meshes.new("WornBoots")
pair.to_mesh(mesh)
triangles = sum(len(f.verts) - 2 for f in pair.faces)
pair.free()
mesh.shade_smooth()
if hasattr(mesh, "set_sharp_from_angle"):
    mesh.set_sharp_from_angle(angle=math.radians(55.0))
obj = bpy.data.objects.new("WornBoots", mesh)
bpy.context.scene.collection.objects.link(obj)
points = np.array([v.co[:] for v in mesh.vertices])
print(f"BOOTS {triangles} triangles; across {points[:,0].min():+.3f}..{points[:,0].max():+.3f}, along {points[:,1].min():+.3f}..{points[:,1].max():+.3f}, up {points[:,2].min():+.3f}..{points[:,2].max():+.3f}")

painted = paint()
texture_path = TEXTURE or os.path.join(bpy.app.tempdir, "WornBoots.jpg")
save_texture(painted, texture_path)
print("TEXTURE", texture_path)
material = bpy.data.materials.new("WornBoots")
material.use_nodes = True
bsdf = next(n for n in material.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
node = material.node_tree.nodes.new("ShaderNodeTexImage")
node.image = bpy.data.images.load(texture_path)
material.node_tree.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
mesh.materials.append(material)

if EXPORT:
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.gltf(filepath=EXPORT, export_format="GLB", use_selection=True, export_apply=True, export_yup=True,
                              export_normals=True, export_texcoords=True, export_materials="NONE")
    print("EXPORTED", EXPORT)

if RENDER:
    os.makedirs(RENDER, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.color_type = "TEXTURE"
    scene.display.shading.light = "STUDIO"
    scene.render.resolution_x, scene.render.resolution_y = 1200, 800
    scene.world = bpy.data.worlds.new("w")
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 0.75
    target = Vector((0.0, -0.10, 0.13))
    for name, eye in (("front_three_quarter", (0.9, -1.1, 0.55)), ("outer_side", (-1.5, -0.1, 0.2)), ("back", (0.4, 1.4, 0.45)),
                      ("above", (0.0, -0.3, 1.6))):
        cam.location = eye
        cam.rotation_euler = (target - Vector(eye)).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(RENDER, f"boots_{name}.png")
        bpy.ops.render.render(write_still=True)
