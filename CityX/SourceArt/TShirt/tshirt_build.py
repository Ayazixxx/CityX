# THE T-SHIRT, headless (owner, 2026-10-19: "put this in as a wearable top called T-Shirt ... separate the body from the playermodel from the shirt
# and flatten out the lump and wrinks at the front so we can cleanly put a various textures and logos on here ... lower the face count", then
# "the fabric should wrap around the bicep tighter"):
#   blender --background --factory-startup --python tshirt_build.py -- <Meshy .fbx> <out .json> [<preview folder>]
# The source is Meshy's "Blank Slate Character" (Meshy_AI_Blank_Slate_Character_1020040122_generate.fbx, 5 MB, not kept here): one 180k-triangle
# surface, a ball-headed mannequin in a plain tee, 1.9 m tall.
#   1. scaled to our body's height (2.6 m) and centred on its head; it then lies within 1-3 cm of our body everywhere;
#   2. the shirt cut out of it: roughly by zones, then cleanly along the hem's level, each cuff's plane and the neckline, every new edge point
#      on the line (straight edges, the hem's and cuffs' bands kept); holes where the arms touched the sides closed;
#   3. the sleeves pulled in round the arms (they stood 4-8 cm off them, a bell);
#   4. the front and back evened out: their distance off the body smoothed (no lumps or wrinkles for a design to ride on), never inside it;
#   5. reduced to about 5,800 triangles (6,192 with the seams);
#   6. unwrapped into four pieces at fixed places on the texture (front, back, two sleeves), split along smooth seam lines, upright, not
#      mirrored, one scale; the template a design is drawn on (tshirt_uv_template.png) and a test design (tshirt_test_design.png) written;
#   7. skinned from the body like the Midnight Coat (../garment_rig.py): the hem on the hips, never a leg; the armpits; the collar;
#   8. written for BlenderGarmentMesh.
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
OCTASLASH = os.path.join(os.path.dirname(HERE), "Octaslash")
args = sys.argv[sys.argv.index("--") + 1:]
FBX, OUT = args[0], args[1]
PREVIEW = args[2] if len(args) > 2 else None

SHOULDERS = {"l": np.array([0.35, 0.0, 1.9]), "r": np.array([-0.35, 0.0, 1.9])}   # Blender +x is the body's left, -y its front
HANDS = {"l": np.array([0.545, 0.0, 1.033]), "r": np.array([-0.545, 0.0, 1.033])}
HEAD_CENTRE, HEAD_CLEAR = np.array([0.0, 0.0, 2.251]), 0.36
NECKLINE_FRONT, NECKLINE_SIDE = 1.921, 0.035   # the collar's inner edge: this high in front and behind, this much higher at the sides,
NECK_REACH = 0.24                              # within this of the neck's axis (beyond it are the shoulders' tops)
HEM_Z = 0.932                # the hem's fold is level all round at 0.922-0.928: cut just above it (the turned-under part goes, the band stays)
CUFF_T, CUFF_TILT, CUFF_R = 0.409, 0.021, 0.11   # each cuff's fold: 0.394 along the arm on its outside, 0.436 on its inside; cut 6 mm short of
                                                 # it on a plane: CUFF_T - CUFF_TILT * (how far outward, in sleeve radii CUFF_R) along the arm
ARM_REACH = 0.22
SNAP = 0.2                   # 2, 6. a cut or seam line passing this near (of the edge) an existing point moves the point onto it
OPENINGS = 4                 # the shirt's openings: the neck, the hem and the two cuffs; any other hole is closed
SLEEVE_GAP = 0.018           # 3. the sleeves (4-8 cm off the arm, a bell) pulled in: the smooth looseness beyond SLEEVE_GAP cut to SLEEVE_KEEP of
SLEEVE_KEEP = 0.3            # itself; the detail on it (folds, the cuff's roll) kept at SLEEVE_DETAIL
SLEEVE_DETAIL, SLEEVE_SMOOTHING = 0.8, 40
PANEL_SMOOTHING = 200        # 4. the front and back: how far they stand off the body evened out (no lumps or wrinkles), the hang kept
PANEL_MIN = 0.015            # and at least this far off it (the chest came through)
PANEL_SIDES = (62.0, 88.0)   # degrees round from the front (and the back): flat up to the first, Meshy's folds again by the second
PANEL_TOP, PANEL_BOTTOM = (1.80, 1.88), (1.02, 0.98)   # and from the collar down to the hem's roll, fading over these heights
CLEAR = 0.012                # nothing nearer the body than this (it came through at the chest and the shoulders)
TRIANGLES = 5800             # 5. reduced to about this many (the seams' lines then add about 1,700)
PIECES = ["front", "back", "sleeve_r", "sleeve_l"]   # 6. where each piece goes on the texture (u0, v0, u1, v1): the front on the left, the
PIECE_BOXES = [(0.0, 0.4, 0.5, 1.0), (0.5, 0.4, 1.0, 1.0), (0.0, 0.0, 0.5, 0.4), (0.5, 0.0, 1.0, 0.4)]   # back on the right, sleeves below
PIECE_MARGIN = 0.01
SEAM_SMOOTHING = 40          # the armholes' line evened out this much (it followed every fold)
TEMPLATE_SIZE, TEMPLATE_GRID = 1024, 0.1   # the design template: its size, and a grid line every 10 cm of shirt
TEMPLATE_PIECE, TEMPLATE_GRID_COLOUR, TEMPLATE_EDGE = (0.93, 0.93, 0.93), (0.8, 0.8, 0.8), (0.2, 0.2, 0.2)
TEMPLATE_MARKS = {"front": 1.62, "back": 1.62}   # the middle of the chest and of the back (a logo's usual place) crossed, at this height
ARMPIT_REACH, ARMPIT_ABOVE, ARMPIT_SMOOTHING = 0.24, 0.04, 6   # 7. skinned as the Midnight Coat is (../garment_rig.py)
COLLAR_FROM, COLLAR_RISE, COLLAR_REACH = 1.87, 0.05, 0.26     # the collar band (1.89-1.96) on the collar bone


def run(path):
    exec(compile(open(path).read(), path, "exec"), globals())


def coords(o):
    co = np.empty(len(o.data.vertices) * 3)
    o.data.vertices.foreach_get("co", co)
    return co.reshape(-1, 3)


def select_only(o):
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o


def body_tree(objects):
    bm = bmesh.new()
    for o in objects:
        part = bmesh.new()
        part.from_mesh(o.data)
        part.transform(o.matrix_world)
        mesh = bpy.data.meshes.new("part")
        part.to_mesh(mesh)
        bm.from_mesh(mesh)
        part.free()
    return BVHTree.FromBMesh(bm)


def arm_frame(co):
    """Each point against the arm on its side: how far along it from the shoulder, how far from its axis, and the way out from the axis."""
    t, r, out = np.zeros(len(co)), np.zeros(len(co)), np.zeros_like(co)
    for side in SHOULDERS:
        here = co[:, 0] * np.sign(SHOULDERS[side][0]) >= 0.0
        axis = HANDS[side] - SHOULDERS[side]
        axis /= np.linalg.norm(axis)
        along = (co[here] - SHOULDERS[side]) @ axis
        radial = co[here] - SHOULDERS[side] - np.outer(along, axis)
        t[here], r[here] = along, np.linalg.norm(radial, axis=1)
        out[here] = radial / np.maximum(r[here], 1e-6)[:, None]
    return t, r, out


def arm_outside(side):
    """The way straight out from the arm's axis, away from the body."""
    axis = HANDS[side] - SHOULDERS[side]
    axis /= np.linalg.norm(axis)
    outside = np.array([np.sign(SHOULDERS[side][0]), 0.0, 0.0])
    outside -= axis * (outside @ axis)
    return outside / np.linalg.norm(outside)


def arm_front(side):
    """The way from the arm's axis towards the body's front (-y), square to the arm and its outside."""
    axis = HANDS[side] - SHOULDERS[side]
    front = np.cross(axis / np.linalg.norm(axis), arm_outside(side))
    return front * -np.sign(front[1])


def sleeve_share(radius, t, r):
    """1 on a sleeve, 0 on the torso: whether a point is nearer the arm's surface than the torso's (octaslash_rig's sizes), from the shoulder."""
    return np.clip(((radius - TORSO_R) - (r - ARM_R)) / 0.06 + 0.5, 0.0, 1.0) * smoothstep(t, 0.0, 0.1)


def smoothstep(x, a, b):
    s = np.clip((x - a) / (b - a), 0.0, 1.0)
    return s * s * (3.0 - 2.0 * s)


def edge_list(me):
    edges = np.empty(len(me.edges) * 2, dtype=np.int64)
    me.edges.foreach_get("vertices", edges)
    return edges.reshape(-1, 2)


def smoothed(values, passes, edges):
    """values averaged with their neighbours' along edges, passes times."""
    degree = np.maximum(np.bincount(edges.ravel(), minlength=len(values)), 1).astype(float)
    values = values.copy()
    for _ in range(passes):
        total = np.zeros_like(values)
        np.add.at(total, edges[:, 0], values[edges[:, 1]])
        np.add.at(total, edges[:, 1], values[edges[:, 0]])
        values = 0.5 * values + 0.5 * total / degree
    return values


def iso_split(bm, level):
    """Splits the mesh along where the vertex layer level crosses zero, the new points placed on the crossing (other layers interpolated). A
    crossing within SNAP of an edge's end moves that end onto it instead: a split there would leave sliver faces that flicker."""
    for e in bm.edges:
        a, b = e.verts
        if a[level] != 0.0 and b[level] != 0.0 and (a[level] < 0.0) != (b[level] < 0.0):
            at = a[level] / (a[level] - b[level])
            if min(at, 1.0 - at) < SNAP:
                near = a if at < 0.5 else b
                near.co = a.co.lerp(b.co, at)
                near[level] = 0.0
    for e in [e for e in bm.edges if e.verts[0][level] != 0.0 and e.verts[1][level] != 0.0
              and (e.verts[0][level] < 0.0) != (e.verts[1][level] < 0.0)]:
        a, b = e.verts
        _, v = bmesh.utils.edge_split(e, a, a[level] / (a[level] - b[level]))
        v[level] = 0.0
    for f in list(bm.faces):
        on = [v for v in f.verts if v[level] == 0.0]
        if len(on) == 2 and not any(set(e.verts) == set(on) for e in f.edges):
            bmesh.utils.face_split(f, on[0], on[1])
    bmesh.ops.triangulate(bm, faces=bm.faces)


def iso_cut(bm, level):
    """Cuts the mesh along where the vertex layer level crosses zero; keeps the side at or above it."""
    iso_split(bm, level)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if any(v[level] < 0.0 for v in f.verts)], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bmesh.ops.triangulate(bm, faces=bm.faces)


def openings(bm):
    """The mesh's open edges, as loops, longest first."""
    left, loops = {e for e in bm.edges if e.is_boundary}, []
    while left:
        loop, frontier = [], [left.pop()]
        while frontier:
            e = frontier.pop()
            loop.append(e)
            for v in e.verts:
                for f in v.link_edges:
                    if f in left:
                        left.remove(f)
                        frontier.append(f)
        loops.append(loop)
    return sorted(loops, key=len, reverse=True)


def largest_piece(bm, what):
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    pieces, seen = [], set()
    for f in bm.faces:
        if f in seen:
            continue
        piece, stack = [], [f]
        seen.add(f)
        while stack:
            g = stack.pop()
            piece.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h not in seen:
                        seen.add(h)
                        stack.append(h)
        pieces.append(piece)
    pieces.sort(key=len, reverse=True)
    print("%s: kept the largest of %d pieces (%d faces), dropped %s" % (what, len(pieces), len(pieces[0]), [len(p) for p in pieces[1:8]]))
    bmesh.ops.delete(bm, geom=[f for p in pieces[1:] for f in p], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")


def preview(objects, path, colours, colour_type="OBJECT"):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = colour_type
    scene.render.resolution_x, scene.render.resolution_y = 500, 700
    for o in bpy.data.objects:
        o.hide_render = o not in objects
    for o, c in zip(objects, colours):
        o.color = c
    camera = bpy.data.objects.get("PreviewCam")
    if camera is None:
        camera = bpy.data.objects.new("PreviewCam", bpy.data.cameras.new("PreviewCam"))
        scene.collection.objects.link(camera)
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 1.9
    scene.camera = camera
    tiles = []
    for direction in ((0, -1, 0.1), (1, 0, 0.1), (0, 1, 0.1), (0.7, -0.7, 0.5)):
        target = Vector((0, 0, 1.45))
        camera.location = target + Vector(direction).normalized() * 6
        camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
        shot = path + ".tile.png"
        scene.render.filepath = shot
        bpy.ops.render.render(write_still=True)
        image = bpy.data.images.load(shot)
        tiles.append(np.array(image.pixels[:], dtype=np.float32).reshape(700, 500, 4))
        bpy.data.images.remove(image)
    sheet = bpy.data.images.new("Sheet", 2000, 700)
    sheet.pixels.foreach_set(np.concatenate(tiles, 1).ravel())
    sheet.filepath_raw = path
    sheet.file_format = "PNG"
    sheet.save()
    os.remove(shot)


# ---- 1. our body, and Meshy's figure scaled onto it ----
bpy.ops.wm.read_factory_settings(use_empty=True)
run(os.path.join(OCTASLASH, "octaslash_rig.py"))
body, head = bpy.data.objects["Body"], bpy.data.objects["Head"]
ours = np.vstack([np.array([(o.matrix_world @ v.co)[:] for v in o.data.vertices]) for o in (body, head)])
headpoints = np.array([(head.matrix_world @ v.co)[:] for v in head.data.vertices])
before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=FBX)
shirt = [o for o in bpy.data.objects if o not in before and o.type == "MESH"][0]
for o in [o for o in bpy.data.objects if o not in before and o is not shirt]:
    bpy.data.objects.remove(o)
select_only(shirt)
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
co = coords(shirt)
scale = np.ptp(ours[:, 2]) / np.ptp(co[:, 2])
co = (co - [0, 0, co[:, 2].min()]) * scale + [0, 0, ours[:, 2].min()]
top = co[:, 2] > ours[:, 2].max() - 0.5
co += [headpoints[:, 0].mean() - co[top, 0].mean(), headpoints[:, 1].mean() - co[top, 1].mean(), 0]
shirt.data.vertices.foreach_set("co", co.ravel())
shirt.data.update()
print("figure: %d vertices, %d faces, scaled %.4f" % (len(co), len(shirt.data.polygons), scale))

# ---- 2. the shirt cut out ----
# First roughly, by zones (below the hem's level, the forearms and hands, the neck and head go), then cleanly along the hem's level, the
# cuffs' planes and the neckline, each new edge point placed on the line, so every edge ends straight; then the largest piece is kept.
tree = body_tree((body, head))
radius = np.hypot(co[:, 0], co[:, 1])
z = co[:, 2]
sideways = np.abs(co[:, 0]) / np.maximum(radius, 1e-6)
t, r, out = arm_frame(co)
on_arm = ((radius - TORSO_R) > (r - ARM_R)) & (t > -0.05)   # nearer the arm's surface (octaslash_rig's sizes) than the torso's
neckline = NECKLINE_FRONT + NECKLINE_SIDE * sideways * sideways - co[:, 2] + np.maximum(0.0, radius - NECK_REACH)
keep = (z > HEM_Z - 0.05) & (neckline > -0.05) & ~(on_arm & ((t > CUFF_T + 0.08) | (r > ARM_REACH)))
keep &= (radius < NECK_REACH) | (np.linalg.norm(co - HEAD_CENTRE, axis=1) > HEAD_CLEAR)   # round the neck the neckline decides
cut = np.minimum(z - HEM_Z, neckline)
for side in SHOULDERS:
    this = co[:, 0] * np.sign(SHOULDERS[side][0]) > 0.0
    outward = r * (out @ arm_outside(side))
    nearer_torso = np.maximum(0.0, (r - ARM_R) - (radius - TORSO_R))
    cut = np.where(this, np.minimum(cut, CUFF_T - CUFF_TILT * outward / CUFF_R - t + 10.0 * nearer_torso), cut)
bm = bmesh.new()
bm.from_mesh(shirt.data)
bm.verts.ensure_lookup_table()
level = bm.verts.layers.float.new("cut")
for v in bm.verts:
    v[level] = cut[v.index]
bmesh.ops.delete(bm, geom=[f for f in bm.faces if not all(keep[v.index] for v in f.verts)], context="FACES")
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
bm.verts.ensure_lookup_table()
iso_cut(bm, level)
largest_piece(bm, "cut")
small = [e for loop in openings(bm)[OPENINGS:] for e in loop]   # where the arms touched the sides: closed
bmesh.ops.holes_fill(bm, edges=small, sides=len(small))
bmesh.ops.triangulate(bm, faces=bm.faces)
bm.to_mesh(shirt.data)
bm.free()
shirt.data.update()
print("shirt: %d vertices, %d faces" % (len(shirt.data.vertices), len(shirt.data.polygons)))
if PREVIEW:
    preview([shirt, body, head], os.path.join(PREVIEW, "cut_on_body.png"), [(0.9, 0.3, 0.25, 1), (0.7, 0.7, 0.7, 1), (0.7, 0.7, 0.7, 1)])

# ---- 3 and 4. the sleeves pulled in round the arms; the front and back evened out ----
co = coords(shirt)
nearest = [tree.find_nearest(Vector(p)) for p in co]
normal = np.array([n[1][:] for n in nearest])
d = np.array([n[3] if (Vector(p) - n[0]).dot(n[1]) >= 0 else -n[3] for p, n in zip(co, nearest)])
mesh_edges = edge_list(shirt.data)
radius = np.hypot(co[:, 0], co[:, 1])
t, r, out = arm_frame(co)
# The sleeves: pulled in towards the arm's axis.
sleeve = smoothed(sleeve_share(radius, t, r), 10, mesh_edges)
loose = smoothed(d, SLEEVE_SMOOTHING, mesh_edges)
inward = sleeve * ((1.0 - SLEEVE_KEEP) * np.maximum(0.0, loose - SLEEVE_GAP) + (1.0 - SLEEVE_DETAIL) * (d - loose))
co -= out * inward[:, None]
# The front and back: their distance off the body evened out.
round_ = np.degrees(np.arctan2(np.abs(co[:, 0]), -co[:, 1]))   # 0 straight in front, 180 straight behind
panel = np.maximum(1.0 - smoothstep(round_, *PANEL_SIDES), 1.0 - smoothstep(180.0 - round_, *PANEL_SIDES))
panel *= (1.0 - smoothstep(co[:, 2], *PANEL_TOP)) * smoothstep(co[:, 2], *PANEL_BOTTOM[::-1]) * (1.0 - sleeve)
even = np.maximum(smoothed(d, PANEL_SMOOTHING, mesh_edges), PANEL_MIN)
co += normal * (panel * (even - d))[:, None]
# Nothing of the body comes through: what still lies inside it or on it is put just outside.
nearest = [tree.find_nearest(Vector(p)) for p in co]
for i, (p, n) in enumerate(zip(co, nearest)):
    depth = (Vector(p) - n[0]).dot(n[1])
    if depth < CLEAR:
        co[i] += np.array(n[1][:]) * (CLEAR - depth)
shirt.data.vertices.foreach_set("co", co.ravel())
shirt.data.update()
print("sleeves: %d points pulled in, by up to %.3f m; panels: %d points evened" % ((sleeve > 0.5).sum(), inward.max(), (panel > 0.5).sum()))

# ---- 5. reduced ----
select_only(shirt)
decimate = shirt.modifiers.new("Reduce", "DECIMATE")
decimate.ratio = TRIANGLES / sum(len(p.vertices) - 2 for p in shirt.data.polygons)
decimate.use_collapse_triangulate = True
bpy.ops.object.modifier_apply(modifier=decimate.name)
for p in shirt.data.polygons:
    p.use_smooth = True
print("reduced: %d vertices, %d faces" % (len(shirt.data.vertices), len(shirt.data.polygons)))

bm = bmesh.new()
bm.from_mesh(shirt.data)
holes = openings(bm)
assert len(holes) == OPENINGS, "the shirt should open only at the neck, the hem and the cuffs: %d openings" % len(holes)

# ---- 6. unwrapped: the front, the back and each sleeve one flat piece, each in its own fixed part of the texture ----
# Seams down the sides and over the shoulders (front from back), round each armhole, and under each sleeve. Every piece the same number of
# texture pixels per metre, upright, and not mirrored: a logo drawn on the template reads the right way round on the shirt.
co = coords(shirt)
radius = np.hypot(co[:, 0], co[:, 1])
t, r, out = arm_frame(co)
fields = {"sleeve": smoothed(sleeve_share(radius, t, r), SEAM_SMOOTHING, edge_list(shirt.data)) - 0.5,   # above 0 on a sleeve
          "front": -co[:, 1],                                                                # above 0 in front
          "ahead": np.zeros(len(co)), "outward": np.zeros(len(co))}                          # round each arm: to the front, to the outside
for side in SHOULDERS:
    this = co[:, 0] * np.sign(SHOULDERS[side][0]) > 0.0
    fields["ahead"][this] = out[this] @ arm_front(side)
    fields["outward"][this] = out[this] @ arm_outside(side)
bm.verts.ensure_lookup_table()
layers = {name: bm.verts.layers.float.new(name) for name in fields}
for name, values in fields.items():
    for v in bm.verts:
        v[layers[name]] = values[v.index]
for name in ("sleeve", "front", "ahead"):   # the seams' lines become edges
    iso_split(bm, layers[name])
bm.faces.index_update()
mean = {name: np.array([np.mean([v[layer] for v in f.verts]) for f in bm.faces]) for name, layer in layers.items()}
centre = np.array([f.calc_center_median()[:] for f in bm.faces])
piece = np.where(mean["sleeve"] > 0.0, np.where(centre[:, 0] > 0.0, PIECES.index("sleeve_l"), PIECES.index("sleeve_r")),
                 np.where(mean["front"] > 0.0, PIECES.index("front"), PIECES.index("back")))
for e in bm.edges:
    if len(e.link_faces) != 2:
        continue
    a, b = (f.index for f in e.link_faces)
    under = PIECES[piece[a]].startswith("sleeve") and mean["outward"][a] < 0.0 and mean["outward"][b] < 0.0 \
        and (mean["ahead"][a] > 0.0) != (mean["ahead"][b] > 0.0)
    e.seam = bool(piece[a] != piece[b] or under)
bm.to_mesh(shirt.data)
bm.free()
select_only(shirt)
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.unwrap(method="ANGLE_BASED", margin=0.0)
bpy.ops.object.mode_set(mode="OBJECT")
mesh = shirt.data
co = coords(shirt)   # with the seams' new points
loop_vertex = np.empty(len(mesh.loops), dtype=np.int64)
mesh.loops.foreach_get("vertex_index", loop_vertex)
uv = np.empty(len(mesh.loops) * 2)
mesh.uv_layers.active.data.foreach_get("uv", uv)
uv = uv.reshape(-1, 3, 2)   # every face a triangle, its loops in order
corners = co[loop_vertex].reshape(-1, 3, 3)
upright = {"front": (np.array([0.0, 0.0, 1.0]), np.array([1.0, 0.0, 0.0])),   # (up, right) as seen from outside the piece
           "back": (np.array([0.0, 0.0, 1.0]), np.array([-1.0, 0.0, 0.0]))}
for side in SHOULDERS:
    toward_hand = HANDS[side] - SHOULDERS[side]
    upright["sleeve_" + side] = (-toward_hand / np.linalg.norm(toward_hand), np.cross(-arm_outside(side), np.array([0.0, 0.0, 1.0])))
placed = {}
for p, name in enumerate(PIECES):
    faces = piece == p
    u, c = uv[faces], corners[faces]
    up, right = upright[name]
    area3 = 0.5 * np.linalg.norm(np.cross(c[:, 1] - c[:, 0], c[:, 2] - c[:, 0]), axis=1)
    e1, e2 = u[:, 1] - u[:, 0], u[:, 2] - u[:, 0]
    area2 = 0.5 * np.abs(e1[:, 0] * e2[:, 1] - e1[:, 1] * e2[:, 0])
    # Which way up and right run on the piece: each face's gradient of height and of rightness over its UVs, area-weighted.
    inverse = np.linalg.pinv(np.stack([e1, e2], 1))
    grad = lambda s: np.einsum("fij,fj->fi", inverse, np.stack([s[:, 1] - s[:, 0], s[:, 2] - s[:, 0]], 1))
    g_up = (grad(c @ up) * area3[:, None]).sum(0)
    g_right = (grad(c @ right) * area3[:, None]).sum(0)
    angle = np.arctan2(g_up[0], g_up[1])   # turned so up is +v
    turn_by = np.array([[np.cos(angle), -np.sin(angle)], [np.sin(angle), np.cos(angle)]])
    u = u @ turn_by.T
    if (turn_by @ g_right)[0] < 0.0:   # mirrored: flipped
        u[:, :, 0] *= -1.0
    u *= np.sqrt(area3.sum() / area2.sum())   # one metre of shirt, one unit
    placed[name] = (faces, u - u.reshape(-1, 2).min(0))
fit = min(min((box[2] - box[0] - 2 * PIECE_MARGIN) / np.ptp(u[:, :, 0]), (box[3] - box[1] - 2 * PIECE_MARGIN) / np.ptp(u[:, :, 1]))
          for (_, u), box in zip(placed.values(), PIECE_BOXES))
for (faces, u), box in zip(placed.values(), PIECE_BOXES):
    size = np.array([np.ptp(u[:, :, 0]), np.ptp(u[:, :, 1])]) * fit
    uv[faces] = u * fit + np.array(box[:2]) + (np.array([box[2] - box[0], box[3] - box[1]]) - size) / 2
mesh.uv_layers.active.data.foreach_set("uv", uv.ravel())
print("unwrapped: %s; %.0f texture pixels per metre on a %d map" % (", ".join("%s %d faces" % (n, f.sum()) for n, (f, _) in placed.items()),
                                                                   fit * TEMPLATE_SIZE, TEMPLATE_SIZE))

# The template a shirt's design is drawn on: each piece outlined, a 10 cm grid, the middle of the chest and of the back marked.
size = TEMPLATE_SIZE
image = np.ones((size, size, 3))
pixel = uv * size
for tri in pixel:
    lo, hi = np.floor(tri.min(0)).astype(int), np.ceil(tri.max(0)).astype(int)
    ys, xs = np.mgrid[max(lo[1], 0):min(hi[1], size), max(lo[0], 0):min(hi[0], size)]
    p = np.stack([xs + 0.5, ys + 0.5], -1)
    e1, e2, q = tri[1] - tri[0], tri[2] - tri[0], p - tri[0]
    det = e1[0] * e2[1] - e1[1] * e2[0]
    if abs(det) < 1e-9:
        continue
    b1 = (q[..., 0] * e2[1] - q[..., 1] * e2[0]) / det
    b2 = (e1[0] * q[..., 1] - e1[1] * q[..., 0]) / det
    inside = (b1 >= -0.01) & (b2 >= -0.01) & (b1 + b2 <= 1.01)
    image[ys[inside], xs[inside]] = TEMPLATE_PIECE
grid = fit * size * TEMPLATE_GRID
for faces, _ in placed.values():
    x0, y0 = (uv[faces].reshape(-1, 2).min(0) * size).astype(int)
    x1, y1 = (uv[faces].reshape(-1, 2).max(0) * size).astype(int)
    for gx in np.arange(x0, x1, grid):
        column = image[y0:y1, int(gx)]
        column[(column == TEMPLATE_PIECE).all(-1)] = TEMPLATE_GRID_COLOUR
    for gy in np.arange(y0, y1, grid):
        row = image[int(gy), x0:x1]
        row[(row == TEMPLATE_PIECE).all(-1)] = TEMPLATE_GRID_COLOUR


def draw(a, b, colour, width=1):
    for s in np.linspace(0.0, 1.0, int(np.linalg.norm(b - a)) * 2 + 2):
        x, y = (a + (b - a) * s).astype(int)
        image[max(y - width, 0):y + width, max(x - width, 0):x + width] = colour


bm = bmesh.new()
bm.from_mesh(mesh)
layer = bm.loops.layers.uv.active
for e in bm.edges:
    if e.seam or e.is_boundary:
        for loop in e.link_loops:
            draw(np.array(loop[layer].uv[:]) * size, np.array(loop.link_loop_next[layer].uv[:]) * size, TEMPLATE_EDGE)
bm.free()
for name, height in TEMPLATE_MARKS.items():
    faces = placed[name][0] & (np.abs(centre[:, 0]) < 0.02)
    near = np.argmin(np.where(faces, np.abs(centre[:, 2] - height), np.inf))
    x, y = uv[near].mean(0) * size
    draw(np.array([x - 18, y]), np.array([x + 18, y]), TEMPLATE_EDGE)
    draw(np.array([x, y - 18]), np.array([x, y + 18]), TEMPLATE_EDGE)
template = bpy.data.images.new("TShirtTemplate", size, size)
template.pixels.foreach_set(np.concatenate([image, np.ones((size, size, 1))], -1).ravel().astype(np.float32))
template.filepath_raw = os.path.join(HERE, "tshirt_uv_template.png")
template.file_format = "PNG"
template.save()
print("WROTE %s" % template.filepath_raw)

# A test design on the template, to check a design prints where it should (TShirtCapture wears it): the grid dark, a red disc 12 cm round
# the chest's and the back's crosses. Seen from afar the template's own grid is too faint.
test = image.copy()
test[(image == TEMPLATE_GRID_COLOUR).all(-1)] = (0.1, 0.2, 0.6)
ys, xs = np.mgrid[0:size, 0:size]
for name, height in TEMPLATE_MARKS.items():
    faces = placed[name][0] & (np.abs(centre[:, 0]) < 0.02)
    x, y = uv[np.argmin(np.where(faces, np.abs(centre[:, 2] - height), np.inf))].mean(0) * size
    test[(xs - x) ** 2 + (ys - y) ** 2 < (0.12 * fit * size) ** 2] = (0.8, 0.1, 0.1)
design = bpy.data.images.new("TShirtTestDesign", size, size)
design.pixels.foreach_set(np.concatenate([test, np.ones((size, size, 1))], -1).ravel().astype(np.float32))
design.filepath_raw = os.path.join(HERE, "tshirt_test_design.png")
design.file_format = "PNG"
design.save()
print("WROTE %s" % design.filepath_raw)

# ---- 7. skinned from the body under it (../garment_rig.py) ----
run(os.path.join(os.path.dirname(HERE), "garment_rig.py"))
weights = body_weights(shirt, body, bpy.data.objects["CityXRig"])
for leg in ("thigh_l", "thigh_r"):   # the hem ends at the hips: it follows them, never a leg
    weights[:, BONE["hips"]] += weights[:, BONE[leg]]
    weights[:, BONE[leg]] = 0.0
weights = share_armpits(weights, co, mesh, ARMPIT_REACH, ARMPIT_ABOVE, ARMPIT_SMOOTHING)
weights = lift_collar(weights, co, COLLAR_FROM, COLLAR_RISE, COLLAR_REACH, 0.0)

# ---- 8. written for the game ----
write_garment(mesh, co, weights, OUT)

if PREVIEW:
    material = bpy.data.materials.new("Template")
    material.use_nodes = True
    texture = material.node_tree.nodes.new("ShaderNodeTexImage")
    texture.image = design
    material.node_tree.nodes.active = texture   # what the viewport (and so the preview) shows
    shirt.active_material = material
    shader = next(n for n in material.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    material.node_tree.links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    mesh.materials.append(material)
    preview([shirt, body, head], os.path.join(PREVIEW, "shirt_on_body.png"), [(1, 1, 1, 1), (0.7, 0.7, 0.7, 1), (0.7, 0.7, 0.7, 1)], "TEXTURE")
    select_only(shirt)
    bpy.ops.export_scene.gltf(filepath=os.path.join(PREVIEW, "tshirt.glb"), export_format="GLB", use_selection=True)   # for tools/model_qa.py
