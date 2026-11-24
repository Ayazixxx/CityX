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
#   5. under each arm the sleeve is cut loose from the coat's side, and each side closed with its own wall, as a coat made with its arms out is
#      (flag_sleeves, close_armpits);
#   6. skinned from the body's skin, each part from its own part (part_weights: the sleeves from the arms', the rest from the trunk's), the
#      skirt on the skirt bones (../garment_rig.py), the collar and what lies near the head on the collar bone; hood up, the hood is the head's;
#   7. written for BlenderGarmentMesh, its paint as one 1024 JPEG.
import glob
import os
import sys

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector
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
SLEEVE_NEAR_ARM = 0.22     # a point this near an arm's line can be a sleeve's
TRUNK_HALF_WIDTH = 0.355   # over the shoulder, a sleeve's points are those further out than this
# Under the shoulder a point is a sleeve's when it lies nearer the arm's surface than the trunk's (the body's arm and trunk radii): the
# sleeve's inner side, under the arm, is no further out than the coat's own side, and counted as the coat's it stayed with the body and
# webbed out under a lifted arm (owner, 2026-11-24: "the coat is still stretching").
ARM_SURFACE, TRUNK_SURFACE = 0.09, 0.26
SHOULDER_CAP_FROM = 1.72   # above this the shoulder's cap, where sleeve and coat are one
SLEEVES_LOOSE_BELOW = 1.81  # below this the seam between a sleeve and the coat is split (and each side walled: close_armpits)
ARMPIT_RELAX_POSE = 75.0      # the arms out this far (degrees from hanging) for relax_armpits
ARMPIT_RELAX_REACH = 0.18     # ... which smooths the cloth this far round each armpit, metres
ARMPIT_RELAX_PASSES = 12
LIP_RELAX_PASSES = 12        # the cut's zigzag edge is smoothed this many times before it is walled (close_armpits)
PARTS_JOIN_OVER = 0.06      # above the cut's top, over this far, sleeve and coat go from their own part of the body's skin to the whole skin's
VENT = (-0.2, 0.2, 0.45, 0.95)  # the back vent, x and height (metres, the body's): its inner layer dropped, the cloth there smoothed
VENT_RAY_STEP = 0.004
VENT_IRON_PASSES = 8
STRAY_PIECE = 40           # a piece of sleeve or coat this small, cut off by the other, is a stray
STRAY_ENCLOSED = 0.6        # ... and closed round by the other on this share of its edges at least
# The skirt swings with the legs from SKIRT_FROM down (wholly by SKIRT_FULL); above, the coat hangs from the hips alone (on_the_hips).
SKIRT_FROM, SKIRT_FULL = 0.95, 0.72
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


def drop_vent_lining(obj):
    """THE BACK VENT'S INNER LAYER (owner, 2026-11-24, of a mark on the coat's back at the seat: "is that line around where the butt is a hole
    in the coat?"). Meshy made a vent up the back from the hem to the seat, two layers of cloth over each other, and at its top the inner one
    came out through the outer one: a grey patch ringed in black. Seen from behind, every face a ray meets after the first, up the vent (VENT),
    is the inner layer, and goes. Done on the reduced cloth (at full detail it left a fine ragged edge, which reducing kept)."""
    tree = BVHTree.FromPolygons([v.co for v in obj.data.vertices], [p.vertices for p in obj.data.polygons])
    inner = set()
    x0, x1, z0, z1 = VENT
    for x in np.arange(x0, x1, VENT_RAY_STEP):
        for z in np.arange(z0, z1, VENT_RAY_STEP):
            origin, first = Vector((x, 1.0, z)), True
            while True:
                hit, _, face, _ = tree.ray_cast(origin, Vector((0.0, -1.0, 0.0)))
                if hit is None or hit.y < 0.0:
                    break
                if not first:
                    inner.add(face)
                first = False
                origin = hit + Vector((0.0, -1e-4, 0.0))
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in inner], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    print("%s: %d faces of the back vent's inner layer dropped" % (obj.name, len(inner)))


def iron_vent(obj):
    """The back where the vent was, smoothed: the reduced cloth there kept crumples a few millimetres deep. Its open edges stay where they are."""
    mesh = obj.data
    co = mesh_coords(mesh)
    edges = np.empty(len(mesh.edges) * 2, dtype=np.int64)
    mesh.edges.foreach_get("vertices", edges)
    edges = edges.reshape(-1, 2)
    degree = np.bincount(edges.ravel(), minlength=len(co)).astype(float)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    rim = np.array([v.is_boundary for v in bm.verts])
    bm.free()
    x0, x1, z0, z1 = VENT
    inside = (co[:, 0] > x0) & (co[:, 0] < x1) & (co[:, 2] > z0) & (co[:, 2] < z1) & (co[:, 1] > 0.0) & ~rim
    for _ in range(VENT_IRON_PASSES):
        total = np.zeros_like(co)
        np.add.at(total, edges[:, 0], co[edges[:, 1]])
        np.add.at(total, edges[:, 1], co[edges[:, 0]])
        mean = total / np.maximum(degree, 1.0)[:, None]
        co[inside] += 0.5 * (mean[inside] - co[inside])
    mesh.vertices.foreach_set("co", co.ravel())
    mesh.update()
    print("%s: %d points of the back by the vent ironed" % (obj.name, inside.sum()))


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
    """Which points are a sleeve's, into a second set of texture coordinates (u = 1), as sleeve_flags reads them: those near an
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
        trunk = np.hypot(co[:, 0], co[:, 1]) - TRUNK_SURFACE
        cap = co[:, 2] >= SHOULDER_CAP_FROM
        over_shoulder = cap & (co[:, 0] * side > TRUNK_HALF_WIDTH) & (co[:, 2] < SHOULDER_CAP_FROM + 0.23)
        under = ~cap & (co[:, 0] * side > 0.2) & (nearest - ARM_SURFACE < trunk)
        sleeve |= (nearest < SLEEVE_NEAR_ARM) & (over_shoulder | under)
    # WHOLE PIECES (owner, 2026-11-24, of spikes out of the armpit: "there is still these single spikes coming from the armpit"): a point told
    # apart by itself can land on the wrong side of the line, a single coat point among the sleeve's or the other way, and stayed behind when
    # the arm moved, pulling a spike. So each face is a sleeve's or the coat's by most of its points, and a piece of either smaller than
    # STRAY_PIECE faces, cut off by the other, goes over to the other.
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.verts.ensure_lookup_table()
    bm.faces.ensure_lookup_table()
    of_sleeve = {f: sum(sleeve[v.index] for v in f.verts) * 2 > len(f.verts) for f in bm.faces}
    flipped = 0
    for _ in range(3):
        seen, changed = set(), 0
        for start in bm.faces:
            if start in seen:
                continue
            piece, stack = [start], [start]
            seen.add(start)
            border = against = 0
            while stack:
                for edge in stack.pop().edges:
                    others = [o for o in edge.link_faces if of_sleeve[o] != of_sleeve[start]]
                    if len(edge.link_faces) < 2 or others:
                        border += 1
                        against += 1 if others else 0
                    for other in edge.link_faces:
                        if other not in seen and of_sleeve[other] == of_sleeve[start]:
                            seen.add(other)
                            piece.append(other)
                            stack.append(other)
            # Only a piece the other side closes round: a small strip of the coat's own (the front edges' piping) is a piece of its own and stays.
            if len(piece) < STRAY_PIECE and against >= STRAY_ENCLOSED * border:
                for f in piece:
                    of_sleeve[f] = not of_sleeve[f]
                changed += len(piece)
        flipped += changed
        if not changed:
            break
    # THE SLEEVES CUT LOOSE UNDER THE ARM (owner, 2026-11-24: "fix the armpit stretch"): Meshy joins each sleeve to the coat's side from the
    # armpit down to the elbow, and an arm lifted drew that join out into a web. Below SLEEVES_LOOSE_BELOW the seam between a sleeve's faces
    # and the coat's is split: each keeps its own faces, the sleeve swings free, and close_armpits gives each side its own wall. Above it the
    # two stay one skin, and there both read the whole body's skin (part_weights), so nothing is pulled.
    seam = [e for e in bm.edges if len(e.link_faces) == 2 and of_sleeve[e.link_faces[0]] != of_sleeve[e.link_faces[1]]
            and max(v.co.z for v in e.verts) < SLEEVES_LOOSE_BELOW]
    bmesh.ops.split_edges(bm, edges=seam)
    bm.verts.ensure_lookup_table()
    sleeve = np.array([sum(of_sleeve[f] for f in v.link_faces) * 2 > len(v.link_faces) if v.link_faces else False for v in bm.verts])
    bm.to_mesh(mesh)
    bm.free()
    print("%s: %d faces of stray pieces given to the side round them, %d seam edges split under the arms" % (obj.name, flipped, len(seam)))
    layer = mesh.uv_layers.new(name="Sleeve")
    corner = np.empty(len(mesh.loops), dtype=np.int64)
    mesh.loops.foreach_get("vertex_index", corner)
    flags = np.zeros((len(mesh.loops), 2), dtype=np.float32)
    flags[:, 0] = sleeve[corner]
    layer.data.foreach_set("uv", flags.ravel())
    mesh.uv_layers.active = mesh.uv_layers[0]
    print("%s: %d of %d points are a sleeve's" % (obj.name, sleeve.sum(), len(co)))


def sleeve_flags(mesh):
    """Which points are a sleeve's (flag_sleeves' second set of texture coordinates)."""
    flag = np.zeros(len(mesh.loops) * 2, dtype=np.float32)
    mesh.uv_layers["Sleeve"].data.foreach_get("uv", flag)
    corner = np.empty(len(mesh.loops), dtype=np.int64)
    mesh.loops.foreach_get("vertex_index", corner)
    sleeve = np.zeros(len(mesh.vertices))
    sleeve[corner] = flag.reshape(-1, 2)[:, 0]
    return sleeve > 0.5


def close_armpits(obj):
    """THE ARMPIT AS IT IS MADE FOR A T-POSE (owner, 2026-11-24: "there is holes in the armpit ... fix the holes without having a stretch when
    arms move", "make it wrap around the armpits cleanly from a tpose position"). Meshy made the coat with the arms down, and under each arm the
    sleeve's inner side and the coat's side are one sheet: joined, a lifted arm stretched it into a web; cut apart (flag_sleeves), it opened into
    a slit you saw the sky through. Here each side of the cut gets its own wall, as a coat made with its arms out has: the sleeve is closed into a
    tube (its underside), the coat's side is closed, and the two meet along the top of the armpit, where the cut ends. Lifted, the sleeve swings
    up on that line like a hinge: nothing is stretched and nothing is open.

    CLEAN (owner, the same day: "make the armplit fix line cleaner and less jagged"): the cut's edge, as the reduced mesh left it, zigzags; it
    is relaxed into a smooth line first (both sides of the cut together, so they still meet), and each wall is filled across in short rungs
    from the top of the armpit down to the cut's lowest point, front edge to back edge (an automatic fill made long fans that folded out into
    spikes). The walls are painted the cloth's brown."""
    mesh = obj.data
    sleeve = sleeve_flags(mesh)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.verts.ensure_lookup_table()
    uv_layer = bm.loops.layers.uv[0]
    flag_layer = bm.loops.layers.uv["Sleeve"]
    boundary = [e for e in bm.edges if e.is_boundary]
    seen, closed, relaxed, pairs, fins = set(), 0, 0, [], []
    for start in boundary:
        if start in seen:
            continue
        loop, stack = [], [start]
        seen.add(start)
        while stack:
            e = stack.pop()
            loop.append(e)
            for v in e.verts:
                for o in v.link_edges:
                    if o.is_boundary and o not in seen:
                        seen.add(o)
                        stack.append(o)
        zs = [v.co.z for e in loop for v in e.verts]
        xs = [abs(v.co.x) for e in loop for v in e.verts]
        if max(zs) > SLEEVES_LOOSE_BELOW + 0.02 or min(zs) < 1.2 or min(xs) < 0.2:
            continue   # the coat's own openings: the front and hem, the cuffs, the hood
        def of_sleeve(edge):
            face = edge.link_faces[0]
            return sum(sleeve[v.index] for v in face.verts) * 2 > len(face.verts)
        sides = {True: [e for e in loop if of_sleeve(e)], False: [e for e in loop if not of_sleeve(e)]}
        ends = [v for v in {v for e in loop for v in e.verts}
                if {of_sleeve(e) for e in v.link_edges if e.is_boundary and e in loop} == {True, False}]
        if len(ends) != 2 or not sides[True] or not sides[False]:
            print("armpit cut at x %.2f: %d ends, left open" % (np.mean(xs), len(ends)))
            continue
        lips = {s: lip_line(ends[0], ends[1], set(edges)) for s, edges in sides.items()}
        if len(lips[True]) != len(lips[False]) or any((a.co - b.co).length > 1e-4 for a, b in zip(lips[True], lips[False])):
            print("armpit cut at x %.2f: its two sides do not match, left open" % np.mean(xs))
            continue
        # The edge relaxed: each point toward the middle of its neighbours along the cut, both sides moved alike.
        for _ in range(LIP_RELAX_PASSES):
            moved = [(lips[True][i - 1].co + lips[True][i + 1].co) * 0.5 for i in range(1, len(lips[True]) - 1)]
            for i, target in enumerate(moved, start=1):
                co = lips[True][i].co.lerp(target, 0.5)
                lips[True][i].co = co
                lips[False][i].co = co
        relaxed += len(lips[True]) - 2
        pairs += [(a.index, b.index) for a, b in zip(lips[True][1:-1], lips[False][1:-1])]
        middle = lips[False][len(lips[False]) // 2]
        paint = next(l[uv_layer].uv.copy() for l in middle.link_loops)
        for is_sleeve, line in lips.items():
            wall = []
            for corners in rungs(line):
                if bm.faces.get(corners) is None:
                    wall.append(bm.faces.new(corners))
                    continue
                # A notch one face wide: that face of the cloth hangs on by one edge, its tip loose (a flap, which a moving arm drew out
                # into a spike). Once the coat is skinned its tip is merged into a corner of that edge (merge_flaps), and the flap is gone.
                lip = set(line)
                tip = next(v for v in corners if sum(1 for o in corners if o is not v and o in lip) == 2 and v not in (line[0], line[-1]))
                hinge = [v for v in corners if v is not tip]
                partner = lips[not is_sleeve][line.index(tip)]
                fins.append((tip.index, partner.index, hinge[0].index, hinge[1].index))
            # Turned the way the cloth beside it is, so it shades as part of it (a wall turned against it darkened the cut's edge).
            edge = bm.edges.get((line[0], line[1]))
            ways = [(l.vert, l.link_loop_next.vert) for f in edge.link_faces for l in f.loops if l.edge is edge]
            if len(ways) == 2 and ways[0] == ways[1]:
                bmesh.ops.reverse_faces(bm, faces=wall)
            for face in wall:
                face.smooth = True
                for l in face.loops:
                    l[uv_layer].uv = paint
                    l[flag_layer].uv = (1.0 if is_sleeve else 0.0, 0.0)
            closed += len(wall)
    bm.to_mesh(mesh)
    bm.free()
    print("%s: the armpits closed with %d faces (the sleeves' undersides and the coat's sides), %d points of the cut's edge relaxed" % (
        obj.name, closed, relaxed))
    return pairs, fins


def merge_flaps(obj, co, weights, fins):
    """Each flap's tip (close_armpits) merged into the nearer corner of the edge it hangs from: the flap, its two loose edges and its tip are
    gone, and the cloth round it is closed (a flap laid flat still hung there, open). Its partner across the cut keeps its place. Returns the
    points and weights without the tips."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    targets = {}
    for tip, _, a, b in fins:
        near = a if np.linalg.norm(co[a] - co[tip]) <= np.linalg.norm(co[b] - co[tip]) else b
        targets[bm.verts[tip]] = bm.verts[near]
    bmesh.ops.weld_verts(bm, targetmap=targets)
    bm.to_mesh(obj.data)
    bm.free()
    gone = sorted(tip for tip, _, _, _ in fins)
    print("%s: %d flaps at the bottom of the armpits merged away" % (obj.name, len(gone)))
    return np.delete(co, gone, axis=0), np.delete(weights, gone, axis=0)


def relax_armpits(obj, co, weights, pairs):
    """THE ARMPIT SMOOTHED WITH THE ARMS OUT (owner, 2026-11-24: "make it wrap around the armpits cleanly from a tpose position", "make the
    armplit fix line cleaner and less jagged"). The armpit is only seen with an arm up, and Meshy's cloth round it, made with the arms down, is
    ragged there. So the coat is put in a T-pose by its own skinning (each point turned about the shoulder by its arm's share), the cloth round
    each armpit is smoothed there, fading out over ARMPIT_RELAX_REACH, and each point is put back to the arms-down pose by the same skinning,
    undone. Lifted, the arm shows the smoothed armpit; down, the change folds away inside it. The two sides of the cut (pairs, close_armpits)
    are put back together where they meet with the arms down: each side was undone by its own skinning, and they no longer met (a notch).
    Returns the points as moved."""
    edges = np.empty(len(obj.data.edges) * 2, dtype=np.int64)
    obj.data.edges.foreach_get("vertices", edges)
    edges = edges.reshape(-1, 2)
    degree = np.bincount(edges.ravel(), minlength=len(co)).astype(float)
    posed = co.copy()
    turns = []
    for side, axis in (("l", (0.0, -1.0, 0.0)), ("r", (0.0, 1.0, 0.0))):
        turn = np.array(Matrix.Rotation(np.radians(ARMPIT_RELAX_POSE), 3, Vector(axis)))
        joint = SHOULDER_JOINTS[side]
        share = weights[:, BONE["arm_" + side]] + weights[:, BONE["forearm_" + side]] + weights[:, BONE["hand_" + side]]
        posed += share[:, None] * ((co - joint) @ turn.T + joint - co)
        turns.append((turn, joint, share))
    # Round each armpit: the top of its cut, on the shoulder joint's line, a little under it.
    fade = np.zeros(len(co))
    for side in ("l", "r"):
        centre = SHOULDER_JOINTS[side] + np.array([-np.sign(SHOULDER_JOINTS[side][0]) * 0.03, 0.0, -0.09])
        near = np.clip(1.0 - np.linalg.norm(co - centre, axis=1) / ARMPIT_RELAX_REACH, 0.0, 1.0)
        fade = np.maximum(fade, near * near * (3.0 - 2.0 * near))
    fade *= np.clip((np.abs(co[:, 0]) - 0.12) / 0.06, 0.0, 1.0)   # never the collar or hood in the middle
    for _ in range(ARMPIT_RELAX_PASSES):
        total = np.zeros_like(posed)
        np.add.at(total, edges[:, 0], posed[edges[:, 1]])
        np.add.at(total, edges[:, 1], posed[edges[:, 0]])
        mean = total / np.maximum(degree, 1.0)[:, None]
        posed += (0.5 * fade)[:, None] * (mean - posed)
    # Undone: the arms-down point that the same skinning takes to the smoothed one.
    moved = co.copy()
    for i in np.nonzero(fade > 0.0)[0]:
        blend, offset = np.eye(3), np.zeros(3)
        for turn, joint, share in turns:
            blend += share[i] * (turn - np.eye(3))
            offset += share[i] * (joint - turn @ joint)
        moved[i] = np.linalg.solve(blend, posed[i] - offset)
    for a, b in pairs:
        moved[a] = moved[b] = 0.5 * (moved[a] + moved[b])
    obj.data.vertices.foreach_set("co", moved.ravel())
    obj.data.update()
    print("%s: %d points round the armpits smoothed with the arms out, moved up to %.1f cm" % (
        obj.name, (fade > 0.0).sum(), 100.0 * np.linalg.norm(moved - co, axis=1).max()))
    return moved


def lip_line(start, end, edges):
    """The points along one side of the cut, from start to end, through the edges of that side."""
    line, here, used = [start], start, set()
    while here is not end:
        step = next(e for e in here.link_edges if e in edges and e not in used)
        used.add(step)
        here = step.other_vert(here)
        line.append(here)
    return line


def rungs(line):
    """A wall across a cut's side, line running from the top of the armpit at its front, down to the lowest point and up to the top at its back:
    triangles from the front edge to the back edge, the top first, each step down taken on whichever edge is higher next."""
    low = min(range(len(line)), key=lambda i: line[i].co.z)
    front, back = line[:low + 1], line[low:][::-1]
    i = j = 0
    while i < len(front) - 1 or j < len(back) - 1:
        if j == len(back) - 1 or (i < len(front) - 1 and front[i + 1].co.z >= back[j + 1].co.z):
            corners = (front[i], front[i + 1], back[j])
            i += 1
        else:
            corners = (front[i], back[j + 1], back[j])
            j += 1
        if len(set(corners)) == 3:
            yield corners


def part_weights(obj, body, rig):
    """EACH PART OF THE COAT FROM ITS OWN PART OF THE BODY (the "partitioned" weight transfer clothing artists use): a sleeve's point takes the
    weights of the nearest point of its ARM's skin, the coat's own point those of the nearest point of the TRUNK's (legs and head with it), each
    read across that skin's face. A point of the sleeve's underside, by the side, used to take the side's weights and web out from it, or the
    coat's side near a hanging hand the forearm's. So each moves as the skin under it does, and the body itself never tears. Over the shoulder,
    where sleeve and coat are one cloth, the whole body's nearest skin is read, so the two do not part there."""
    from mathutils.interpolate import poly_3d_calc
    mesh, skin = obj.data, body.data
    body_w = np.zeros((len(skin.vertices), len(TARGET_BONES)))
    for bone in rig.data.bones:
        target = FOLDED.get(bone.name, bone.name)
        if target in BONE:
            body_w[:, BONE[target]] += group_values(body, bone.name)
    body_co = [v.co.copy() for v in skin.vertices]
    arm = {s: body_w[:, BONE["arm_" + s]] + body_w[:, BONE["forearm_" + s]] for s in ("l", "r")}
    parts = {"l": [], "r": [], "trunk": [], "all": []}
    for poly in skin.polygons:
        vs = list(poly.vertices)
        parts["all"].append(vs)
        side = next((s for s in ("l", "r") if arm[s][vs].mean() > 0.5), None)
        parts[side or "trunk"].append(vs)
    trees = {name: BVHTree.FromPolygons(body_co, faces) for name, faces in parts.items()}
    sleeve = sleeve_flags(mesh)
    co = mesh_coords(mesh)
    weights = np.zeros((len(co), len(TARGET_BONES)))
    whole = np.clip((co[:, 2] - SLEEVES_LOOSE_BELOW) / PARTS_JOIN_OVER, 0.0, 1.0)
    whole = whole * whole * (3.0 - 2.0 * whole)
    for i, p in enumerate(co):
        point = Vector(p)
        own = ("l" if p[0] > 0.0 else "r") if sleeve[i] else "trunk"
        for name, share in ((own, 1.0 - whole[i]), ("all", whole[i])):
            if share <= 0.0:
                continue
            _, _, face, _ = trees[name].find_nearest(point)
            vs = parts[name][face]
            bary = poly_3d_calc([body_co[v] for v in vs], point)
            weights[i] += share * sum(b * body_w[v] for b, v in zip(bary, vs))
    weights /= np.maximum(weights.sum(1, keepdims=True), 1e-6)
    print("%s: weights from the arms' skin for %d sleeve points, from the trunk's for %d" % (obj.name, sleeve.sum(), (~sleeve).sum()))
    return weights


def on_the_hips(weights, co):
    """ABOVE THE CROTCH THE COAT HANGS FROM THE HIPS (owner, 2026-11-24: "the croatch clips through", "the coat needs to go over it instead of
    it clipping through like this", then, of a shirt's corner out over a panel mid-stride: "slightly clipping here with the white shirt going
    through the coat a tiny bit"). The skin's weights gave the front panels by the crotch a share of the thighs, which the skirt bones then
    carried: standing they drew the panels 7 cm back into the crotch, and a leg swinging back drew its panel back behind the shirt. From
    SKIRT_FROM up a point's share of a thigh goes to the hips (as a shirt's under the coat does, UnderCoat.OffTheLegs); below it the skirt
    swings with the legs, kept off them by SkirtCloth."""
    above = np.clip((co[:, 2] - SKIRT_FULL) / (SKIRT_FROM - SKIRT_FULL), 0.0, 1.0)
    above = above * above * (3.0 - 2.0 * above)
    for thigh in ("thigh_l", "thigh_r"):
        moved = weights[:, BONE[thigh]] * above
        weights[:, BONE[thigh]] -= moved
        weights[:, BONE["hips"]] += moved
    return weights


def skin_brown_coat(obj, body, rig, hood_up):
    mesh = obj.data
    co = mesh_coords(mesh)
    weights = part_weights(obj, body, rig)
    weights = on_the_hips(weights, co)
    weights = hang_skirt(weights, co, SKIRT_FROM, SKIRT_FULL)
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
    drop_vent_lining(coat)
    iron_vent(coat)
    keep_off(coat, body)
    flag_sleeves(coat)
    cut, flaps = close_armpits(coat)
    co, weights = skin_brown_coat(coat, body, rig, hood_up)
    co = relax_armpits(coat, co, weights, cut)
    co, weights = merge_flaps(coat, co, weights, flaps)
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
