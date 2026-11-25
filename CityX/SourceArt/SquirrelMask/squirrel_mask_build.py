# The Squirrel Mask (docs/SQUIRREL_MASK.md), from the owner's Meshy model of the character wearing it (2026-11-25, "Foxmancer"): the furry
# mask cut off the body, its inward nose replaced by one that stands out, cut down for the game and placed round the game's head.
#
#   blender --background --factory-startup --python squirrel_mask_build.py -- <Meshy fbx> [out.blend]
#
# Writes Assets/CityX/Wearables/Models/SquirrelMask.glb: two parts in the Head bone's frame (the head's centre at the origin), "Fur" (the
# mask) and "Black" (the eyes' hollows and the nose). Every height and depth below is in the Meshy model's own units, as it comes in.
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
GLB = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "CityX", "Wearables", "Models", "SquirrelMask.glb"))

# Cutting the mask off: fur is rough, skin smooth. A vertex is smooth when its normal agrees with its neighbours' (1 - cos, averaged).
SMOOTH_BELOW = 0.012
NORMAL_RINGS, AVERAGE_RINGS = 4, 12
HEAD_FROM = 0.44            # below: the body
NECK_TOP = 0.47             # the bare skin is flooded from the neck, below this
SKIN_TOP = 0.605            # and never above the nose's foot: the smoother fur at the crown is not skin
MASK_FROM = 0.49            # the crease where the head meets the neck is rough, but not mask
SCRAP_FACES = 5000          # loose pieces smaller than this are dropped
TUFT_FACES = 40             # and after the final cut, tufts it left loose
HOLE_EDGES = 16             # small holes the cut opened in the fringe are closed (the open bottom has hundreds of edges)

WORKING_TRIANGLES = 60000
FINAL_TRIANGLES = 12000     # the fur and the eyes. Over the budget for a mask (2,000 to 5,000, at most 8,000): at 7,200 the fur was gone,
                            # and the owner wants its detail; as the Fur Hooded Parka's 14,600.
# The final cut takes first where the fur stands out most (the face), so the shallower fur on the sides and back keeps some of its own;
# the eyes' outlines (and this many rings of vertices round them) it spares.
RELIEF_FLOOR = 0.15
RELIEF_FACTOR = float(os.environ.get("SQUIRREL_RELIEF_FACTOR", "10"))
EYE_KEEP_RINGS = 2

# Each eye's hollow: flooded from its middle across edges bent less than this.
EYE_MIDDLE = (0.175, -0.18, 0.67)
EYE_VIEW = (0.7, -0.7, 0.15)
EYE_CREASE_DEGREES = 12.0

# The nose, where the pit was: an upside-down rounded triangle standing out of the snout (the owner's smoother model's nose).
NOSE = dict(tip=0.579, widest=0.616, top=0.636, half_width=0.036, back=-0.225, front=-0.292, rings=12, segments=20)

# On the game's head (FusedBodyShape.HeadRadius 0.35): the mask's ball (fitted below the ears) centred on the head, scaled so that all but its
# deepest dips (DIP_SHARE of the fur's points, and the eyes' hollows) clear the head by 3 mm; those few are lifted onto that.
EARS_FROM = 0.84
CLEARANCE = 0.353
DIP_SHARE = 2.0             # percent

FUR_COLOUR = (0.647, 0.322, 0.098, 1.0)
BLACK_COLOUR = (0.03, 0.03, 0.03, 1.0)


def vertices_of(mesh):
    a = np.empty(len(mesh.vertices) * 3)
    mesh.vertices.foreach_get("co", a)
    return a.reshape(-1, 3)


def edges_of(mesh):
    e = np.empty(len(mesh.edges) * 2, dtype=np.int64)
    mesh.edges.foreach_get("vertices", e)
    return e.reshape(-1, 2)


def averaged(values, edges, count, rings):
    degree = np.maximum(np.bincount(edges.ravel(), minlength=count), 1).astype(float)[:, None]
    for _ in range(rings):
        total = np.zeros_like(values)
        np.add.at(total, edges[:, 0], values[edges[:, 1]])
        np.add.at(total, edges[:, 1], values[edges[:, 0]])
        values = total / degree
    return values


def flood(start, passable, edges):
    reached = start.copy()
    while True:
        grown = reached.copy()
        grown[edges[:, 1][reached[edges[:, 0]] & passable[edges[:, 1]]]] = True
        grown[edges[:, 0][reached[edges[:, 1]] & passable[edges[:, 0]]]] = True
        if grown.sum() == reached.sum():
            return reached
        reached = grown


def cut_mask(body):
    """The mask's faces: the head's, less the bare skin flooded from the neck, less small scraps."""
    mesh = body.data
    count = len(mesh.vertices)
    points = vertices_of(mesh)
    edges = edges_of(mesh)
    normals = np.empty(count * 3)
    mesh.vertices.foreach_get("normal", normals)
    normals = normals.reshape(-1, 3)
    around = averaged(normals, edges, count, NORMAL_RINGS)
    around /= np.maximum(np.linalg.norm(around, axis=1), 1e-9)[:, None]
    rough = averaged((1.0 - (normals * around).sum(1))[:, None], edges, count, AVERAGE_RINGS)[:, 0]
    head = points[:, 2] > HEAD_FROM
    skin_like = head & (rough < SMOOTH_BELOW) & (points[:, 2] < SKIN_TOP)
    skin = flood(skin_like & (points[:, 2] < NECK_TOP), skin_like, edges)
    mask = head & ~skin & (points[:, 2] > MASK_FROM)

    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if not all(mask[v.index] for v in f.verts)], context="FACES")
    seen = set()
    scraps = []
    for f in bm.faces:
        if f in seen:
            continue
        island, stack = [], [f]
        seen.add(f)
        while stack:
            g = stack.pop()
            island.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h not in seen:
                        seen.add(h)
                        stack.append(h)
        if len(island) < SCRAP_FACES:
            scraps.extend(island)
    bmesh.ops.delete(bm, geom=scraps, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    out = bpy.data.meshes.new("Mask")
    bm.to_mesh(out)
    bm.free()
    obj = bpy.data.objects.new("Mask", out)
    bpy.context.scene.collection.objects.link(obj)
    print("mask cut:", len(out.polygons), "faces")
    return obj


def decimate(obj, triangles, cut_first=None):
    """A collapse to about `triangles`; `cut_first` names a vertex group of where to take first (weight 1) and where to spare (0)."""
    modifier = obj.modifiers.new("Decimate", "DECIMATE")
    modifier.ratio = min(1.0, triangles / len(obj.data.polygons))
    modifier.use_collapse_triangulate = True
    if cut_first is not None:
        modifier.vertex_group = cut_first
        modifier.vertex_group_factor = RELIEF_FACTOR
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def material(name, colour):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    shader = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    shader.inputs["Base Color"].default_value = colour
    m.diffuse_color = colour
    return m


def mark_eyes(mask, black_index):
    """Each eye's hollow, flooded from its middle (the face nearest the eye's middle seen from in front of it) to the crease round it."""
    bm = bmesh.new()
    bm.from_mesh(mask.data)
    limit = math.radians(EYE_CREASE_DEGREES)
    for side in (1, -1):
        middle = Vector((EYE_MIDDLE[0] * side, EYE_MIDDLE[1], EYE_MIDDLE[2]))
        view = Vector((EYE_VIEW[0] * side, EYE_VIEW[1], EYE_VIEW[2])).normalized()
        start, nearest = None, 1e9
        for f in bm.faces:
            offset = f.calc_center_median() - middle
            if (offset - view * offset.dot(view)).length < 0.006 and f.normal.dot(view) > 0.3 and -offset.dot(view) < nearest:
                start, nearest = f, -offset.dot(view)
        hollow, stack = {start}, [start]
        while stack:
            f = stack.pop()
            for e in f.edges:
                for g in e.link_faces:
                    if g not in hollow and f.normal.angle(g.normal, 0.0) < limit:
                        hollow.add(g)
                        stack.append(g)
        for f in hollow:
            f.material_index = black_index
        print("eye", side, ":", len(hollow), "faces")
    bm.to_mesh(mask.data)
    bm.free()


def cut_order(mask):
    """The final cut's vertex group: how far the fur stands out at each point (its normal against its neighbours'), scaled to 1 at the
    roughest tenth and no lower than RELIEF_FLOOR; 0 over the eyes (their almond shape is a few big faces) and a few rings round them."""
    mesh = mask.data
    count = len(mesh.vertices)
    edges = edges_of(mesh)
    normals = np.empty(count * 3)
    mesh.vertices.foreach_get("normal", normals)
    normals = normals.reshape(-1, 3)
    around = averaged(normals, edges, count, 2)
    around /= np.maximum(np.linalg.norm(around, axis=1), 1e-9)[:, None]
    relief = averaged((1.0 - (normals * around).sum(1))[:, None], edges, count, 3)[:, 0]
    weight = np.clip(relief / np.percentile(relief, 90), RELIEF_FLOOR, 1.0)
    keep = np.zeros(count, bool)
    for p in mesh.polygons:
        if p.material_index == 1:
            keep[list(p.vertices)] = True
    for _ in range(EYE_KEEP_RINGS):
        grown = keep.copy()
        grown[edges[:, 1][keep[edges[:, 0]]]] = True
        grown[edges[:, 0][keep[edges[:, 1]]]] = True
        keep = grown
    weight[keep] = 0.0
    group = mask.vertex_groups.new(name="CutFirst")
    for i in range(count):
        group.add([i], float(weight[i]), "REPLACE")
    return group.name


def nose_width(z):
    if z <= NOSE["tip"]:
        return 0.0
    if z <= NOSE["widest"]:
        return ((z - NOSE["tip"]) / (NOSE["widest"] - NOSE["tip"])) ** 0.45
    s = min(1.0, (z - NOSE["widest"]) / (NOSE["top"] - NOSE["widest"]))
    return math.sqrt(max(0.0, 1.0 - s * s))


def make_nose():
    """Rings from the tip up: each an oval, wide as nose_width, deep as a half-sine over the height (round at the top and the tip)."""
    half_depth = (NOSE["back"] - NOSE["front"]) / 2
    height = NOSE["top"] - NOSE["tip"]
    bm = bmesh.new()
    rows = []
    for i in range(1, NOSE["rings"]):
        z = NOSE["tip"] + height * (1 - math.cos(math.pi * i / NOSE["rings"])) / 2
        w = nose_width(z)
        bulge = math.sqrt(max(0.0, math.sin(math.pi * (z - NOSE["tip"]) / height)))
        row = []
        for j in range(NOSE["segments"]):
            a = 2 * math.pi * j / NOSE["segments"]
            s = math.sin(a)
            depth = half_depth * bulge * s if s > 0 else half_depth * w * 0.3 * s
            row.append(bm.verts.new((NOSE["half_width"] * w * math.cos(a), NOSE["back"] - half_depth * bulge - depth, z)))
        rows.append(row)
    n = NOSE["segments"]
    for i in range(len(rows) - 1):
        for j in range(n):
            bm.faces.new((rows[i][j], rows[i][(j + 1) % n], rows[i + 1][(j + 1) % n], rows[i + 1][j]))
    tip = bm.verts.new((0.0, NOSE["back"], NOSE["tip"]))
    top = bm.verts.new((0.0, NOSE["back"], NOSE["top"]))
    for j in range(n):
        bm.faces.new((rows[0][(j + 1) % n], rows[0][j], tip))
        bm.faces.new((rows[-1][j], rows[-1][(j + 1) % n], top))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("Nose")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("Nose", mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def fit_sphere(points):
    a = np.c_[2 * points, np.ones(len(points))]
    x = np.linalg.lstsq(a, (points ** 2).sum(1), rcond=None)[0]
    centre = x[:3]
    return centre, math.sqrt(x[3] + (centre ** 2).sum())


def mask_ball(mask):
    """The ball the mask lies on, below the ears, refitted without the fur's tips and the eyes' dips."""
    points = vertices_of(mask.data)
    points = points[points[:, 2] < EARS_FROM]
    centre, radius = fit_sphere(points)
    for _ in range(3):
        off = np.linalg.norm(points - centre, axis=1) - radius
        centre, radius = fit_sphere(points[np.abs(off) < 2 * off.std()])
    print("mask ball: centre", centre.round(4), "radius", round(radius, 4))
    return Vector(centre), radius


def place_on_head(objects, centre, radius):
    """The ball's centre to the origin, scaled so the fur's deepest dips (but DIP_SHARE) clear the head; the rest lifted out onto CLEARANCE."""
    fur = objects[0]
    black = {i for p in fur.data.polygons if p.material_index == 1 for i in p.vertices}
    reach = np.array([(v.co - centre).length for v in fur.data.vertices if v.index not in black and v.co.z < EARS_FROM])
    scale = CLEARANCE / np.percentile(reach, DIP_SHARE)
    print("mask ball radius on the head", round(radius * scale, 4))
    lifted = 0
    for obj in objects:
        for v in obj.data.vertices:
            p = (v.co - centre) * scale
            if obj.name != "Nose" and p.length < CLEARANCE:
                p = p.normalized() * CLEARANCE
                lifted += 1
            v.co = p
        obj.data.update()
    print("scale", round(scale, 4), "- lifted out of the head:", lifted, "vertices")


def tidy(mask):
    """Closes the small holes the final cut opened in the fringe and drops the tufts it left loose."""
    bm = bmesh.new()
    bm.from_mesh(mask.data)
    filled = bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=HOLE_EDGES)["faces"]
    bmesh.ops.triangulate(bm, faces=filled)
    seen, loose = set(), []
    for f in bm.faces:
        if f in seen:
            continue
        island, stack = [], [f]
        seen.add(f)
        while stack:
            g = stack.pop()
            island.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h not in seen:
                        seen.add(h)
                        stack.append(h)
        if len(island) < TUFT_FACES:
            loose.extend(island)
    bmesh.ops.delete(bm, geom=loose, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.to_mesh(mask.data)
    bm.free()
    print("tidied:", len(filled), "holes closed,", len(loose), "loose faces dropped")


def unwrap(obj):
    """Texture coordinates, for a texture one day (it wears flat colours today)."""
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.003)
    bpy.ops.object.mode_set(mode="OBJECT")


def build():
    args = sys.argv[sys.argv.index("--") + 1:]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=args[0])
    body = next(o for o in bpy.data.objects if o.type == "MESH")
    bpy.context.view_layer.objects.active = body
    body.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    mask = cut_mask(body)
    bpy.data.objects.remove(body)
    decimate(mask, WORKING_TRIANGLES)
    fur, black = material("SquirrelMaskFur", FUR_COLOUR), material("SquirrelMaskBlack", BLACK_COLOUR)
    mask.data.materials.append(fur)
    mask.data.materials.append(black)
    mark_eyes(mask, 1)
    centre, radius = mask_ball(mask)
    nose = make_nose()
    decimate(mask, FINAL_TRIANGLES, cut_order(mask))
    place_on_head([mask, nose], centre, radius)
    tidy(mask)

    # The eyes into their own part with the nose: "Black". The rest is "Fur".
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = mask
    mask.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="DESELECT")
    mask.active_material_index = 1
    bpy.ops.object.material_slot_select()
    bpy.ops.mesh.separate(type="SELECTED")
    bpy.ops.object.mode_set(mode="OBJECT")
    eyes = next(o for o in bpy.context.selected_objects if o != mask)
    mask.name = mask.data.name = "Fur"
    mask.data.materials.pop(index=1)
    eyes.data.materials.clear()
    eyes.data.materials.append(black)
    nose.data.materials.append(black)
    bpy.ops.object.select_all(action="DESELECT")
    eyes.select_set(True)
    nose.select_set(True)
    bpy.context.view_layer.objects.active = eyes
    bpy.ops.object.join()
    eyes.name = eyes.data.name = "Black"

    # Blender's frame (faces -Y, up +Z) is kept: the glTF export turns it to the game's (faces +Z, up +Y).
    for obj in (mask, eyes):
        obj.data.shade_smooth()
        obj.data.set_sharp_from_angle(angle=math.radians(30.0))
        unwrap(obj)

    bpy.ops.object.select_all(action="DESELECT")
    for obj in (mask, eyes):
        obj.select_set(True)
    os.makedirs(os.path.dirname(GLB), exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=GLB, export_format="GLB", use_selection=True, export_apply=True, export_yup=True,
                              export_materials="EXPORT", export_animations=False, export_skins=False)
    print("wrote", GLB, "- fur", len(mask.data.polygons), "triangles, eyes and nose", len(eyes.data.polygons))
    if len(args) > 1:
        bpy.ops.wm.save_as_mainfile(filepath=args[1])


build()
