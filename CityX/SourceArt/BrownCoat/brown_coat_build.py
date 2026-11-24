# THE BROWN COAT from Meshy's two models, headless (owner, 2026-11-24, with "Hooded Coat Character" (the hood up) and "Hooded Wanderer" (the
# hood down), each the coat on a grey mannequin: "cleanly remove separate the playermodel form the coat with these two models, also lower the
# face count to a reasonable amount ... whatever would be best to get the cleanest trim. use the brown material vs the playermodel's grey as a
# guide. then put it into the game as a functional item called brown coat"; docs/BROWN_COAT.md).
#
#   blender --background --factory-startup --python brown_coat_build.py -- <hood down folder> <hood up folder> <out folder for the .json>
#                                                                         <out folder for the .jpg> [--render <folder>]
# (each Meshy folder holds its .fbx and its _texture.png). For each model:
#   1. the mannequin is cut away AT FULL DETAIL (a third of a million faces: the trim's edge is as fine as Meshy's own), by the paint: a face
#      is the coat's where its texture is brown (red well over blue) and the mannequin's where it is grey; loose scraps left behind are dropped;
#   2. it is put on this game's body: Meshy's mannequin scaled to the body's 2.6 m is the body's own size (its arms, legs and head as wide,
#      measured), so the coat is only scaled and stood on the ground, not refitted;
#   3. it is reduced to COAT_TRIANGLES;
#   4. the cloth is kept off the body (a shirt goes under it) and off the head, which here is a ball where Meshy's is an egg: any point nearer
#      than it should be is moved straight out;
#   5. skinned as the coats are (../garment_rig.py): the nearest skin, only the sleeves on the arms (told apart here by where they hang), the
#      skirt on the skirt bones, the armpits shared, the collar and what lies near the head on the collar bone; hood up, the hood is the head's;
#   6. written for BlenderGarmentMesh, its paint as one 1024 JPEG.
import glob
import os
import sys

import bpy
import bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE_ART = os.path.dirname(HERE)
args = sys.argv[sys.argv.index("--") + 1:]
DOWN_FOLDER, UP_FOLDER, OUT_MODELS, OUT_MAPS = (os.path.abspath(a) for a in args[:4])
RENDER = os.path.abspath(args[args.index("--render") + 1]) if "--render" in args else None

BROWN_OVER_GREY = 0.06     # the coat's paint has this much more red than blue (measured: brown 0.39 0.33 0.27, grey 0.63 0.63 0.63)
SCRAP_SHARE = 0.01         # a loose piece smaller than this share of the coat is a scrap of the trim
HOLE_SIDES = 40            # a hole in the cloth with no more edges than this is closed
BODY_HEIGHT = 2.601        # the game's body, head and all (BodyRig)
COAT_TRIANGLES = 9000
OFF_THE_BODY = 0.028       # the cloth stays this far off the skin (a shirt is 1 to 2 cm off it)
HEAD_CENTRE = Vector((0.0, 0.0, 2.251))
HEAD_RADIUS = 0.35
OFF_THE_HEAD = 0.022
MAP_SIZE = 1024
JPEG_QUALITY = 88
# The skinning: the parka's measures (Parka/parka_skin.py), a coat of the same cut.
ARM = [np.array([0.35, 0.0, 1.9]), np.array([0.465, 0.0, 1.39]), np.array([0.545, 0.0, 1.033]), np.array([0.58, 0.0, 0.88])]
SLEEVE_NEAR_ARM = 0.19     # a point this near an arm's line, and outside the trunk, is a sleeve's
TRUNK_HALF_WIDTH = 0.355
ARMS_FROM = (1.58, 1.72)   # the cloth just under the armpit stays with the body: lower, flaps of it went out with a lifted arm
SLEEVES_LOOSE_BELOW = 1.62  # under the armpit the sleeves are cut loose from the coat's sides
ARMPIT_REACH, ARMPIT_ABOVE, ARMPIT_SMOOTHING = 0.40, 0.04, 16
SKIRT_FROM, SKIRT_FULL = 1.15, 0.9
COLLAR_FROM, COLLAR_RISE, COLLAR_REACH, COLLAR_FRONT = 1.95, 0.11, 0.55, 0.12
HEAD_NEAR = (0.04, 0.10)
HOOD_ON_HEAD = (1.96, 2.08)   # hood up, the cloth goes over to the head's own bone between these heights


def run(path):
    globals()["__file__"] = path
    exec(compile(open(path).read(), path, "exec"), globals())


def import_coat(folder, name):
    """Meshy's model with the mannequin cut away, on the game's body's scale."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=glob.glob(os.path.join(folder, "*.fbx"))[0])
    obj = [o for o in bpy.data.objects if o not in before and o.type == "MESH"][0]
    for other in [o for o in bpy.data.objects if o not in before and o is not obj]:
        bpy.data.objects.remove(other)
    obj.name = name
    mesh = obj.data
    mesh.transform(obj.matrix_world)
    obj.matrix_world.identity()
    image = bpy.data.images.load(glob.glob(os.path.join(folder, "*_texture.png"))[0])
    w, h = image.size
    pixels = np.array(image.pixels[:], dtype=np.float32).reshape(h, w, 4)

    # 1. by the paint: each face's corners read on the map.
    loops = len(mesh.loops)
    uv = np.empty(loops * 2, dtype=np.float32)
    mesh.uv_layers.active.data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 2)
    colour = pixels[np.clip((uv[:, 1] * h).astype(int), 0, h - 1), np.clip((uv[:, 0] * w).astype(int), 0, w - 1), :3]
    brown_corner = (colour[:, 0] - colour[:, 2]) > BROWN_OVER_GREY
    starts = np.array([p.loop_start for p in mesh.polygons])
    totals = np.array([p.loop_total for p in mesh.polygons])
    brown_face = np.add.reduceat(brown_corner.astype(np.int32), starts) * 2 > totals
    co = np.array([v.co[:] for v in mesh.vertices])
    low, height = co[:, 2].min(), co[:, 2].max() - co[:, 2].min()
    scale = BODY_HEIGHT / height

    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[f for f, keep in zip(bm.faces, brown_face) if not keep], context="FACES")
    # Loose scraps: pieces of a few faces the trim left on the mannequin's side.
    remaining = set(bm.faces)
    pieces = []
    while remaining:
        seed = remaining.pop()
        piece, stack = [seed], [seed]
        while stack:
            for edge in stack.pop().edges:
                for other in edge.link_faces:
                    if other in remaining:
                        remaining.discard(other)
                        piece.append(other)
                        stack.append(other)
        pieces.append(piece)
    whole = sum(len(p) for p in pieces)
    scraps = [f for p in pieces if len(p) < whole * SCRAP_SHARE for f in p]
    bmesh.ops.delete(bm, geom=scraps, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    # Small holes the trim or Meshy left in the cloth are closed (the coat's real openings, cuffs, hem and front, have hundreds of edges here),
    # each new face painted as the cloth beside it.
    uv_layer = bm.loops.layers.uv.active
    filled = bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=HOLE_SIDES)["faces"]
    for face in filled:
        for loop in face.loops:
            beside = next((l for l in loop.vert.link_loops if l.face not in filled), None)
            if beside is not None:
                loop[uv_layer].uv = beside[uv_layer].uv
    # 2. onto the game's body.
    for v in bm.verts:
        v.co = Vector(((v.co.x) * scale, v.co.y * scale, (v.co.z - low) * scale))
    bm.to_mesh(mesh)
    bm.free()
    print("%s: Meshy's %d faces, the coat's %d (%d scraps dropped in %d pieces, %d small holes closed), scale %.4f" % (
        name, len(brown_face), len(mesh.polygons), len(scraps), sum(1 for p in pieces if len(p) < whole * SCRAP_SHARE), len(filled), scale))
    return obj, image


def reduce_coat(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    triangles = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    modifier = obj.modifiers.new("reduce", "DECIMATE")
    modifier.ratio = min(1.0, COAT_TRIANGLES / triangles)
    bpy.ops.object.modifier_apply(modifier="reduce")
    modifier = obj.modifiers.new("tris", "TRIANGULATE")
    bpy.ops.object.modifier_apply(modifier="tris")
    obj.data.shade_smooth()


def keep_off(obj, body):
    """The cloth off the body's skin and off the head's ball: a point too near either is moved straight out to where it may be."""
    tree = BVHTree.FromObject(body, bpy.context.evaluated_depsgraph_get())
    moved_body = moved_head = 0
    for v in obj.data.vertices:
        p = v.co.copy()
        nearest, normal, _, distance = tree.find_nearest(p)
        if nearest is not None:
            outside = (p - nearest).dot(normal) >= 0.0
            signed = distance if outside else -distance
            if signed < OFF_THE_BODY:
                p = nearest + normal * OFF_THE_BODY
                moved_body += 1
        from_head = p - HEAD_CENTRE
        if from_head.length < HEAD_RADIUS + OFF_THE_HEAD:
            p = HEAD_CENTRE + from_head.normalized() * (HEAD_RADIUS + OFF_THE_HEAD)
            moved_head += 1
        v.co = p
    print("%s: %d points moved off the body, %d off the head" % (obj.name, moved_body, moved_head))


def flag_sleeves(obj):
    """Which points are a sleeve's, into a second set of texture coordinates (u = 1), as garment_rig.sleeves_only reads them: those near an
    arm's line and outside the trunk's width."""
    mesh = obj.data
    co = np.array([v.co[:] for v in mesh.vertices])
    sleeve = np.zeros(len(co), dtype=bool)
    for side in (1.0, -1.0):
        nearest = np.full(len(co), np.inf)
        for a, b in zip(ARM[:-1], ARM[1:]):
            a, b = a * [side, 1, 1], b * [side, 1, 1]
            along = np.clip(((co - a) @ (b - a)) / ((b - a) @ (b - a)), 0.0, 1.0)
            nearest = np.minimum(nearest, np.linalg.norm(co - (a + along[:, None] * (b - a)), axis=1))
        sleeve |= (nearest < SLEEVE_NEAR_ARM) & (co[:, 0] * side > TRUNK_HALF_WIDTH) & (co[:, 2] < ARMS_FROM[1] + 0.2)
    # THE SLEEVES CUT LOOSE UNDER THE ARM (owner, 2026-11-24: "fix the armpit stretch"): Meshy joins each sleeve to the coat's side from the
    # armpit down to the elbow, and an arm lifted drew that join out into a web. The faces joining a sleeve's points to the coat's below the
    # armpit are given to the coat's side whole: each keeps its place, but its sleeve points are made again as the coat's own, so the coat's
    # side has no hole and only the sleeve is let go (its opening is on its inner side, against the body). Taken out instead, they left a
    # hole in the coat's side that showed in a sprint.
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.verts.ensure_lookup_table()
    uv_layer = bm.loops.layers.uv.active
    joins = [f for f in bm.faces if f.calc_center_median().z < SLEEVES_LOOSE_BELOW
             and any(sleeve[v.index] for v in f.verts) and not all(sleeve[v.index] for v in f.verts)]
    own = {}
    for face in joins:
        verts, uvs = [], []
        for loop in face.loops:
            v = loop.vert
            if sleeve[v.index]:
                if v not in own:
                    own[v] = bm.verts.new(v.co)
                v = own[v]
            verts.append(v)
            uvs.append(loop[uv_layer].uv.copy())
        bm.faces.remove(face)
        made = bm.faces.new(verts)
        made.smooth = True
        for loop, st in zip(made.loops, uvs):
            loop[uv_layer].uv = st
    sleeve = np.concatenate([sleeve, np.zeros(len(own), dtype=bool)])   # the new points are the coat's
    bm.to_mesh(mesh)
    bm.free()
    print("%s: %d faces joining a sleeve to the coat's side given to the side (%d points made again)" % (obj.name, len(joins), len(own)))
    layer = mesh.uv_layers.new(name="Sleeve")
    corner = np.empty(len(mesh.loops), dtype=np.int64)
    mesh.loops.foreach_get("vertex_index", corner)
    flags = np.zeros((len(mesh.loops), 2), dtype=np.float32)
    flags[:, 0] = sleeve[corner]
    layer.data.foreach_set("uv", flags.ravel())
    mesh.uv_layers.active = mesh.uv_layers[0]
    print("%s: %d of %d points are a sleeve's" % (obj.name, sleeve.sum(), len(co)))


def skin_brown_coat(obj, body, rig, hood_up):
    mesh = obj.data
    co = mesh_coords(mesh)
    weights = body_weights(obj, body, rig)
    weights = sleeves_only(weights, co, mesh, ARMS_FROM)
    weights = hang_skirt(weights, co, SKIRT_FROM, SKIRT_FULL)
    weights = share_armpits(weights, co, mesh, ARMPIT_REACH, ARMPIT_ABOVE, ARMPIT_SMOOTHING)
    weights = lift_collar(weights, co, COLLAR_FROM, COLLAR_RISE, COLLAR_REACH, COLLAR_FRONT)
    # What lies near the head goes where the head goes (the parka's hug_head).
    off = np.linalg.norm(co - np.array(HEAD_CENTRE), axis=1) - HEAD_RADIUS
    near = np.clip((HEAD_NEAR[1] - off) / (HEAD_NEAR[1] - HEAD_NEAR[0]), 0.0, 1.0)
    near = near * near * (3.0 - 2.0 * near)
    took = near * (1.0 - weights[:, BONE["collar"]])
    weights = weights * (1.0 - took)[:, None]
    weights[:, BONE["collar"]] += took
    if hood_up:
        # The hood is the head's own, so it turns with the face.
        on_head = np.clip((co[:, 2] - HOOD_ON_HEAD[0]) / (HOOD_ON_HEAD[1] - HOOD_ON_HEAD[0]), 0.0, 1.0)
        on_head = on_head * on_head * (3.0 - 2.0 * on_head)
        weights = weights * (1.0 - on_head)[:, None]
        weights[:, BONE["head"]] += on_head
    return co, weights


def save_map(image, path):
    image.scale(MAP_SIZE, MAP_SIZE)
    scene = bpy.context.scene
    scene.render.image_settings.file_format = "JPEG"
    scene.render.image_settings.quality = JPEG_QUALITY
    scene.view_settings.view_transform = "Standard"
    image.save_render(path, scene=scene)
    print("WROTE %s" % path)


bpy.ops.wm.read_factory_settings(use_empty=True)
run(os.path.join(SOURCE_ART, "Octaslash", "octaslash_rig.py"))
run(os.path.join(SOURCE_ART, "garment_rig.py"))
body, rig = bpy.data.objects["Body"], bpy.data.objects["CityXRig"]
made = {}
for name, folder, hood_up in (("BrownCoat", DOWN_FOLDER, False), ("BrownCoatHoodUp", UP_FOLDER, True)):
    coat, image = import_coat(folder, name)
    reduce_coat(coat)
    keep_off(coat, body)
    flag_sleeves(coat)
    co, weights = skin_brown_coat(coat, body, rig, hood_up)
    print("%s: %d vertices, %d triangles, from %s to %s" % (name, len(co), len(coat.data.polygons), co.min(0).round(3), co.max(0).round(3)))
    write_garment(coat.data, co, weights, os.path.join(OUT_MODELS, name + ".json"))
    save_map(image, os.path.join(OUT_MAPS, name + ".jpg"))
    made[name] = (coat, image)

if RENDER:
    os.makedirs(RENDER, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.display.shading.show_backface_culling = False
    scene.render.image_settings.file_format = "PNG"
    scene.render.resolution_x, scene.render.resolution_y = 900, 1100
    scene.world = bpy.data.worlds.new("w")
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 2.9
    for name, (coat, image) in made.items():
        material = bpy.data.materials.new(name)
        material.use_nodes = True
        node = material.node_tree.nodes.new("ShaderNodeTexImage")
        node.image = image
        bsdf = next(n for n in material.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        material.node_tree.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
        coat.data.materials.clear()
        coat.data.materials.append(material)
    for name, (coat, _) in made.items():
        for other, _ in made.values():
            other.hide_render = other is not coat
        for view, eye in (("front", (0.0, -6.0, 1.4)), ("three_quarter", (3.5, -5.0, 1.9)), ("side", (6.0, 0.0, 1.4)), ("back", (-2.0, 6.0, 1.9))):
            cam.location = eye
            cam.rotation_euler = (Vector((0.0, 0.0, 1.4)) - Vector(eye)).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(RENDER, f"{name}_{view}.png")
            bpy.ops.render.render(write_still=True)
