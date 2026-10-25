# THE BLACK JEANS, FITTED ONTO THIS GAME'S BODY, headless (owner, 2026-10-24: "add them as "Black Jeans" and try to fit it to the player the
# best you can il make adjustments if needed"):
#   blender --background --factory-startup --python black_jeans_fit.py -- BlackJeansCut.glb black_jeans_color.jpg <out BlackJeans.glb> [<preview folder>]
# BlackJeansCut.glb is THE OWNER'S CUT of Meshy's "Blank Avatar in Jeans" (one 363k-triangle surface, 1.9 m tall): the jeans alone, reduced
# to 11,335 triangles and smoothed by hand in Blender, still where Meshy's figure stood. black_jeans_color.jpg is Meshy's black denim baked
# onto that cut (2048; the game takes it at 1024).
# The output is the fit black_jeans_build.py skins: the owner adjusts THAT file in Blender (on ../Octaslash/octaslash_rig.py's body) and runs
# the build again; this script is only run again to start over from the cut.
#   1. scaled to our body's height and moved as the T-shirt's Meshy figure was (../TShirt/tshirt_build.py): it then lies within a few cm of
#      our body;
#   2. Meshy's legs splay out below the knee (each ankle about 3 cm further out than ours): each leg's middle moved, height by height, onto
#      our leg's, fading out up to the knee;
#   3. nothing of the body comes through: what lies inside it or nearer than CLEAR is put just outside, a little at a time, each push spread
#      over its neighbours (pushed one by one, single points stand out as spikes) and given to the cloth lying just outside it (the
#      waistband's lining pushed alone met its outer layer: faces on faces, flickering);
#   3b. the waistband's top drawn in toward the body, so a shirt's hem hangs over it (the belt loops came through the shirt in a sprint);
#   4. shaded smooth with its seams crisp, and written with its texture, so it opens in Blender as it will look.
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

HERE = os.path.dirname(os.path.abspath(__file__))
OCTASLASH = os.path.join(os.path.dirname(HERE), "Octaslash")
args = sys.argv[sys.argv.index("--") + 1:]
CUT, TEXTURE, OUT = (os.path.abspath(a) for a in args[:3])
PREVIEW = os.path.abspath(args[3]) if len(args) > 3 else None

# 1. The T-shirt's Meshy figure, measured on it: its feet scaled to our body's height, its head over ours.
MESHY_FLOOR, MESHY_SCALE = -0.951734, 1.367869
MESHY_OFFSET = np.array([0.001599, -0.002361, -0.001130])
# 2. Each leg's middle measured every LEG_STEP up to LEG_TOP, taken as the middle of its cross-section's extent (the points' mean leans
#    toward the denser seams and pockets), smoothed over LEG_SMOOTHING passes, and moved fully below LEG_FULL, not at all above LEG_FADE.
LEG_STEP, LEG_SLICE, LEG_TOP = 0.02, 0.012, 0.66
LEG_FULL, LEG_FADE = 0.50, 0.62
LEG_SMOOTHING = 3
LEG_INNER = 0.03          # points nearer the middle than this are the crotch, not a leg's
BODY_REACH = 0.42         # the body's legs and hips, without the hands hanging beside them
# 3. Nothing nearer the body than this; out in rounds, each push spread over its neighbours.
CLEAR = 0.012
PUSH_ROUNDS, PUSH_SPREAD = 12, 3
LAYER_REACH = 0.015      # cloth this near outside a pushed point is pushed with it (a layer over another)
# 3b. A shirt's hem hangs over the waistband from 0.92 up, 1.2-2 cm off the body at its edge and 2 cm or more above; the waistband stood
#     out to 4.6 cm (the belt loops). From TUCK_FROM up (wholly from TUCK_FULL) how far the cloth stands out past TUCK_DEPTH is cut to
#     TUCK_KEEP of itself: drawn in, its layers (lining, band, loops) kept in their order (cut to 0.3, a loop met the band: faces on faces).
TUCK_FROM, TUCK_FULL = 0.89, 0.94
TUCK_DEPTH, TUCK_KEEP = 0.010, 0.5
SHARP_ANGLE = np.radians(30.0)   # 4. edges folded more than this are shaded crisp


def run(path):
    exec(compile(open(path).read(), path, "exec"), globals())


def smoothstep(x, a, b):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def smoothed(values, passes, edges):
    degree = np.maximum(np.bincount(edges.ravel(), minlength=len(values)), 1)
    for _ in range(passes):
        total = np.zeros_like(values)
        np.add.at(total, edges[:, 0], values[edges[:, 1]])
        np.add.at(total, edges[:, 1], values[edges[:, 0]])
        values = 0.5 * values + 0.5 * total / degree
    return values


def middle(points):
    return (points[:, :2].min(0) + points[:, :2].max(0)) / 2.0


# ---- our body, and the owner's cut ----
bpy.ops.wm.read_factory_settings(use_empty=True)
run(os.path.join(OCTASLASH, "octaslash_rig.py"))
body = bpy.data.objects["Body"]
before = set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=CUT)
jeans = [o for o in bpy.data.objects if o not in before and o.type == "MESH"][0]
jeans.name = "BlackJeans"
bpy.ops.object.select_all(action="DESELECT")
jeans.select_set(True)
bpy.context.view_layer.objects.active = jeans
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
mesh = jeans.data
welded = bmesh.new()
welded.from_mesh(mesh)
bmesh.ops.remove_doubles(welded, verts=welded.verts, dist=1e-6)   # glTF splits the points along the texture's seams
welded.to_mesh(mesh)
welded.free()
co = np.array([v.co[:] for v in mesh.vertices])
edges = np.array([e.vertices[:] for e in mesh.edges], dtype=np.int64)
print("cut: %d points, %d triangles" % (len(co), len(mesh.polygons)))

# ---- 1. onto our body's height ----
co = (co - [0.0, 0.0, MESHY_FLOOR]) * MESHY_SCALE + MESHY_OFFSET

depsgraph = bpy.context.evaluated_depsgraph_get()
skin = body.evaluated_get(depsgraph).to_mesh()
skin_co = np.array([(body.matrix_world @ v.co)[:] for v in skin.vertices])
skin_polys = [p.vertices[:] for p in skin.polygons if abs(skin_co[list(p.vertices)][:, 0].mean()) < BODY_REACH]
tree = BVHTree.FromPolygons([Vector(p) for p in skin_co], skin_polys)

# ---- 2. each leg's middle onto ours (straight down from the hip joint: the body's own cross-section there takes in the foot) ----
heights = np.arange(co[:, 2].min() + LEG_SLICE, LEG_TOP, LEG_STEP)
for side in (1.0, -1.0):
    ours = np.array([side * HIP_X, 0.0])
    moves = []
    for z in heights:
        theirs = co[(np.abs(co[:, 2] - z) < LEG_SLICE) & (co[:, 0] * side > LEG_INNER)]
        moves.append(ours - middle(theirs) if len(theirs) else np.zeros(2))
    moves = np.array(moves)
    for _ in range(LEG_SMOOTHING):
        moves[1:-1] = (moves[:-2] + moves[1:-1] + moves[2:]) / 3.0
    on_leg = co[:, 0] * side > 0.0
    share = (1.0 - smoothstep(co[:, 2], LEG_FULL, LEG_FADE)) * smoothstep(co[:, 0] * side, 0.0, LEG_INNER) * on_leg
    for k in range(2):
        co[:, k] += np.interp(co[:, 2], heights, moves[:, k]) * share
    print("leg %s: its middle moved by up to %.3f m (at the ankle %.3f, %.3f)" % ("left" if side > 0 else "right",
                                                                                   np.abs(moves).max(), *moves[0]))

# ---- 3. nothing of the body comes through ----
for step in range(PUSH_ROUNDS + 1):
    push = np.zeros_like(co)
    for i, p in enumerate(co):
        loc, n, _, _ = tree.find_nearest(Vector(p))
        depth = (Vector(p) - loc).dot(n)
        if depth < CLEAR:
            push[i] = np.array(n[:]) * (CLEAR - depth)
    if step == 0:
        print("near or inside the body before pushing: %d points" % (np.abs(push).sum(1) > 0).sum())
    # The cloth lying just outside a pushed point (the waistband's outer layer over its lining) goes out with it, or the two meet.
    near = KDTree(len(co))
    for i, p in enumerate(co):
        near.insert(Vector(p), i)
    near.balance()
    pushed = np.flatnonzero(np.abs(push).sum(1) > 0)
    spread = push.copy()
    for i in pushed:
        size = np.linalg.norm(push[i])
        for _, j, _ in near.find_range(Vector(co[i]), LAYER_REACH):
            if np.linalg.norm(spread[j]) < size and np.dot(co[j] - co[i], push[i]) > 0.0:
                spread[j] = push[i]
    push = spread
    if step < PUSH_ROUNDS:
        push = np.stack([smoothed(push[:, k], PUSH_SPREAD, edges) for k in range(3)], 1)
    co += push
# ---- 3b. the waistband's top tucked in under a shirt's hem ----
tuck = smoothstep(co[:, 2], TUCK_FROM, TUCK_FULL)
for i in np.flatnonzero(tuck > 0.0):
    loc, n, _, _ = tree.find_nearest(Vector(co[i]))
    depth = (Vector(co[i]) - loc).dot(n)
    if depth > TUCK_DEPTH:
        co[i] -= np.array(n[:]) * (depth - TUCK_DEPTH) * (1.0 - TUCK_KEEP) * tuck[i]
print("waistband: %d points tucked in" % (tuck > 0.0).sum())
mesh.vertices.foreach_set("co", co.ravel())
mesh.update()
# Smooth cloth with the seams, pockets and loops crisp, as the owner shaded the cut (welding dropped its split normals).
shading = bmesh.new()
shading.from_mesh(mesh)
for face in shading.faces:
    face.smooth = True
for edge in shading.edges:
    edge.smooth = not (len(edge.link_faces) == 2 and edge.calc_face_angle(0.0) > SHARP_ANGLE)
shading.to_mesh(mesh)
shading.free()
print("fitted: from %s to %s" % (co.min(0).round(3), co.max(0).round(3)))

# ---- 4. written with its texture ----
material = bpy.data.materials.new("BlackJeans")
material.use_nodes = True
nodes = material.node_tree.nodes
image = nodes.new("ShaderNodeTexImage")
image.image = bpy.data.images.load(TEXTURE)
shader = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
shader.inputs["Roughness"].default_value = 0.9
material.node_tree.links.new(image.outputs["Color"], shader.inputs["Base Color"])
mesh.materials.clear()
mesh.materials.append(material)
bpy.ops.object.select_all(action="DESELECT")
jeans.select_set(True)
bpy.ops.export_scene.gltf(filepath=OUT, use_selection=True, export_format="GLB", export_image_format="JPEG", export_apply=True)
print("WROTE %s" % OUT)

if PREVIEW:
    os.makedirs(PREVIEW, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.color_type = "TEXTURE"
    scene.display.shading.light = "STUDIO"
    scene.render.resolution_x, scene.render.resolution_y = 700, 900
    for o in bpy.data.objects:
        if o.type == "MESH" and o not in (body, jeans):
            o.hide_render = True
    camera = bpy.data.objects.new("PreviewCamera", bpy.data.cameras.new("PreviewCamera"))
    scene.collection.objects.link(camera)
    scene.camera = camera
    target = Vector((0.0, 0.0, 0.62))
    for name, direction in (("front", (0, -1, 0.1)), ("side", (1, 0, 0.1)), ("back", (0, 1, 0.1)), ("three_quarter", (0.7, -0.7, 0.3)),
                            ("ankles", (0.3, -1, 0.2)), ("waist", (0.4, -1, 0.5))):
        d = Vector(direction).normalized()
        close = name in ("ankles", "waist")
        aim = Vector((0.0, 0.0, 0.15)) if name == "ankles" else (Vector((0.0, 0.0, 0.95)) if name == "waist" else target)
        camera.location = aim + d * (1.4 if close else 3.2)
        camera.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(PREVIEW, name + ".png")
        bpy.ops.render.render(write_still=True)
    print("previews in %s" % PREVIEW)
