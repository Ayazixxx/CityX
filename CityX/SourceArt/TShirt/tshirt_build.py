# THE T-SHIRT, headless (owner, 2026-10-19: "put this in as a wearable top called T-Shirt ... separate the body from the playermodel from the shirt
# and flatten out the lump and wrinks at the front so we can cleanly put a various textures and logos on here ... lower the face count", then
# "the fabric should wrap around the bicep tighter"; then, with their own cut: "i have my own open i selected more precisely please lower the
# face count and fill the missing space on the collar from the meshy models head being removed"; then "make it rounded inward and evenly dont
# make it raise around the shoulders" and "i want there to be a visible collar though"):
#   blender --background --factory-startup --python tshirt_build.py -- TShirtCut.glb <out .json> [<preview folder>]
# TShirtCut.glb is THE OWNER'S CUT of Meshy's "Blank Slate Character" (a ball-headed mannequin in a plain tee, one 180k-triangle surface,
# 1.9 m tall): the shirt alone, selected by hand in Blender, 8,550 triangles, still where Meshy's figure stood.
#   1. scaled to our body's height (2.6 m) and centred on its head as the whole figure was; it then lies within 1-3 cm of our body everywhere;
#   2. Meshy's collar cut away along an oval round the neck (it was notched where the mannequin's neck had been, and stood up at the shoulders);
#   3. the sleeves pulled in round the arms (they stood 4-8 cm off them, a bell);
#   4. the front and back evened out: their distance off the body smoothed (no lumps or wrinkles for a design to ride on), never inside it;
#   5. reduced; points standing out of the surface put back level with their neighbours; then a crew neck's band sewn round the oval, the
#      same all round;
#   6. unwrapped into four pieces at fixed places on the texture (front, back, two sleeves), split along smooth seam lines, upright, not
#      mirrored, one scale; the template a design is drawn on (tshirt_uv_template.png) and a test design (tshirt_test_design.png) written;
#   7. skinned from the body like the Midnight Coat (../garment_rig.py): the hem on the hips, never a leg; the armpits;
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
CUT, OUT = args[0], args[1]
PREVIEW = args[2] if len(args) > 2 else None

SHOULDERS = {"l": np.array([0.35, 0.0, 1.9]), "r": np.array([-0.35, 0.0, 1.9])}   # Blender +x is the body's left, -y its front
HANDS = {"l": np.array([0.545, 0.0, 1.033]), "r": np.array([-0.545, 0.0, 1.033])}
# 1. Meshy's whole figure scaled from its feet to our body's height and moved so its head is over ours (measured on the figure itself).
MESHY_FLOOR, MESHY_SCALE = -0.951734, 1.367869
MESHY_OFFSET = np.array([0.001599, -0.002361, -0.001130])
OPENINGS = 4                 # the shirt's openings: the neck, the hem and the two cuffs
NECK_SIDE, NECK_FRONT, NECK_BACK = 0.28, 0.2, 0.2   # 2. the neck opened on an oval this far out to the sides, in front and behind (game
                                                    # metres from the neck's axis): outside Meshy's collar rib all round, room for the band
COLLAR_BAND = ((0.003, -0.002), (0.007, 0.004), (0.026, 0.005))     # 5b. the crew neck's band: (across toward the neck, out off the cloth)
                                                                    # from the seam: a groove, then the rib raised a little
COLLAR_ROLL = 0.006          # its far edge rounded over and down in a half turn of this radius
COLLAR_TILT, COLLAR_TILT_SIDE = np.radians(40.0), np.radians(20.0)   # the band's lean up toward the neck in front and behind; at the
                                                                     # shoulders, lower (it stood out beside the head)
COLLAR_CLEAR = 0.008               # and nowhere nearer the body than this
COLLAR_STEPS, COLLAR_HEIGHT_ORDER = 5, 3   # points round the half turn; terms of the smooth curve the neckline's heights are put on
SNAP = 0.2                   # 6. a seam line passing this near (of the edge) an existing point moves the point onto it
SLEEVE_GAP = 0.018           # 3. the sleeves (4-8 cm off the arm, a bell) pulled in: the smooth looseness beyond SLEEVE_GAP cut to SLEEVE_KEEP of
SLEEVE_KEEP = 0.3            # itself; the detail on it (folds, the cuff's roll) kept at SLEEVE_DETAIL
SLEEVE_DETAIL, SLEEVE_SMOOTHING = 0.8, 40
PANEL_SMOOTHING = 200        # 4. the front and back: how far they stand off the body evened out (no lumps or wrinkles), the hang kept
PANEL_MIN = 0.015            # and at least this far off it (the chest came through)
PANEL_SIDES = (50.0, 80.0)   # degrees round from the front (and the back): flat up to the first, Meshy's folds again by the second
PANEL_TOP, PANEL_BOTTOM = (1.62, 1.78), (1.02, 0.98)   # and from the collar down to the hem's roll, fading over these heights
CLEAR = 0.012                # nothing nearer the body than this (it came through at the chest and the shoulders)
PUSH_ROUNDS, PUSH_SPREAD = 12, 3   # out in rounds, each push spread over its neighbours; what is left after them, pushed point by point
TRIANGLES = 5800             # 5. reduced to about this many (the seams' lines then add about 300)
SPIKE_LONG, SPIKE_OUT, SPIKE_PASSES = 3.0, 0.35, 3   # a point standing out of its neighbours' middle by this many typical edges and this share of
                                                     # its own edges is a spike
PIECES = ["front", "back", "sleeve_r", "sleeve_l"]   # 6. where each piece goes on the texture (u0, v0, u1, v1): the front on the left, the
PIECE_BOXES = [(0.0, 0.4, 0.5, 1.0), (0.5, 0.4, 1.0, 1.0), (0.0, 0.0, 0.5, 0.4), (0.5, 0.0, 1.0, 0.4)]   # back on the right, sleeves below
PIECE_MARGIN = 0.01
SEAM_SMOOTHING = 40          # the armholes' line evened out this much (it followed every fold)
TEMPLATE_SIZE, TEMPLATE_GRID = 1024, 0.1   # the design template: its size, and a grid line every 10 cm of shirt
TEMPLATE_PIECE, TEMPLATE_GRID_COLOUR, TEMPLATE_EDGE = (0.93, 0.93, 0.93), (0.8, 0.8, 0.8), (0.2, 0.2, 0.2)
TEMPLATE_MARKS = {"front": 1.62, "back": 1.62}   # the middle of the chest and of the back (a logo's usual place) crossed, at this height
ARMPIT_REACH, ARMPIT_ABOVE, ARMPIT_SMOOTHING = 0.24, 0.04, 6   # 7. skinned as the Midnight Coat is (../garment_rig.py)


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


def keep_joined_to_hem(bm):
    """Deletes every piece not joined to the hem (what a cut leaves loose)."""
    hem = min(bm.faces, key=lambda f: f.calc_center_median().z)
    joined, frontier = {hem}, [hem]
    while frontier:
        for e in frontier.pop().edges:
            for f in e.link_faces:
                if f not in joined:
                    joined.add(f)
                    frontier.append(f)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f not in joined], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")


def in_order(loop):
    """An opening's points in order round it, from its open edges, from its lowest-numbered point (so a rebuild comes out the same)."""
    ring = [min((v for e in loop for v in e.verts), key=lambda v: v.index)]
    previous = None
    while True:
        here = ring[-1]
        step = next(e for e in here.link_edges if e.is_boundary and e is not previous and e in loop)
        previous = step
        following = step.other_vert(here)
        if following is ring[0]:
            return ring
        ring.append(following)


def sew_collar(bm, ring):
    """Sews a crew neck's ribbed band onto the neckline, evenly all round. The neckline's heights are put on a smooth curve round the neck
    (a low Fourier series through them: the cut edge followed every fold) and the band is swept along it with one cross-section: from the
    seam a groove, then the rib raised a little off the cloth (COLLAR_BAND, across toward the neck and out), its far edge rounding over and
    down into the opening in a half turn of COLLAR_ROLL. It leans up toward the neck by COLLAR_TILT in front and behind, by less at the
    shoulders (COLLAR_TILT_SIDE: steeper, it stood out beside the head). Returns the faces made."""
    count = len(ring)
    at = np.array([v.co[:] for v in ring])
    round_ = np.arctan2(at[:, 0], -at[:, 1])
    basis = np.stack([np.ones(count)] + [f(k * round_) for k in range(1, COLLAR_HEIGHT_ORDER + 1) for f in (np.cos, np.sin)], 1)
    at[:, 2] = basis @ np.linalg.lstsq(basis, at[:, 2], rcond=None)[0]
    for v, p in zip(ring, at):
        v.co = Vector(p)
    up = np.array([0.0, 0.0, 1.0])
    inward = -np.stack([at[:, 0] / NECK_SIDE ** 2, at[:, 1] / np.where(at[:, 1] < 0.0, NECK_FRONT, NECK_BACK) ** 2, np.zeros(count)], 1)
    inward /= np.linalg.norm(inward, axis=1)[:, None]   # square to the oval, toward the neck
    facing = at[:, 1] ** 2 / np.maximum(at[:, 0] ** 2 + at[:, 1] ** 2, 1e-9)   # 1 in front and behind, 0 at the shoulders
    tilt = (COLLAR_TILT_SIDE + (COLLAR_TILT - COLLAR_TILT_SIDE) * facing)[:, None]
    onward = inward * np.cos(tilt) + up * np.sin(tilt)
    out = up * np.cos(tilt) - inward * np.sin(tilt)
    profile = list(COLLAR_BAND)
    across, raised = COLLAR_BAND[-1]
    for k in range(1, COLLAR_STEPS + 1):   # the far edge rounding over and down into the neck
        turn = np.pi * k / COLLAR_STEPS
        profile.append((across + COLLAR_ROLL * np.sin(turn), raised + COLLAR_ROLL * (np.cos(turn) - 1.0)))
    rings = [ring] + [[bm.verts.new(p) for p in at + onward * a + out * o] for a, o in profile]
    # Wound like the shirt: its open edge from point i to i+1 is walked the other way round by the new face.
    edge = next(e for e in ring[0].link_edges if e.other_vert(ring[0]) is ring[1])
    forward = edge.link_loops[0].vert is ring[0]
    made = []
    for inner, outer in zip(rings, rings[1:]):
        for i in range(count):
            j = (i + 1) % count
            quad = (inner[j], inner[i], outer[i], outer[j]) if forward else (inner[i], inner[j], outer[j], outer[i])
            made.append(bm.faces.new(quad))
            made[-1].smooth = True
    bm.normal_update()
    assert np.mean([f.normal.dot(Vector(o)) for f, o in zip(made[count:2 * count], out)]) > 0.5, "the collar is wound inside out"
    bmesh.ops.triangulate(bm, faces=made)
    return len(made)


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


# ---- 1. our body, and the owner's cut scaled onto it ----
bpy.ops.wm.read_factory_settings(use_empty=True)
run(os.path.join(OCTASLASH, "octaslash_rig.py"))
body, head = bpy.data.objects["Body"], bpy.data.objects["Head"]
before = set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=CUT)
shirt = [o for o in bpy.data.objects if o not in before and o.type == "MESH"][0]
select_only(shirt)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
co = (coords(shirt) - [0.0, 0.0, MESHY_FLOOR]) * MESHY_SCALE + MESHY_OFFSET
shirt.data.vertices.foreach_set("co", co.ravel())
shirt.data.update()

# ---- 2. the neck opened on an oval: Meshy's collar cut away (notched where the mannequin's neck was joined to it, standing up at the shoulders) ----
bm = bmesh.new()
bm.from_mesh(shirt.data)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)   # the glb splits points along its normals
level = bm.verts.layers.float.new("neck")
for v in bm.verts:
    v[level] = np.hypot(v.co.x / NECK_SIDE, v.co.y / (NECK_FRONT if v.co.y < 0.0 else NECK_BACK)) - 1.0
iso_cut(bm, level)
keep_joined_to_hem(bm)
holes = openings(bm)
assert len(holes) == OPENINGS, "the shirt should open only at the neck, the hem and the cuffs: %d openings" % len(holes)
print("cut: %d vertices, %d faces" % (len(bm.verts), len(bm.faces)))
bm.to_mesh(shirt.data)
bm.free()
shirt.data.update()
tree = body_tree((body, head))
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
# Nothing of the body comes through: what still lies inside it or on it is put just outside, a little at a time and spread over its
# neighbours (pushed one by one, single points stood out as spikes).
for step in range(PUSH_ROUNDS + 1):
    push = np.zeros_like(co)
    for i, p in enumerate(co):
        loc, n, _, _ = tree.find_nearest(Vector(p))
        depth = (Vector(p) - loc).dot(n)
        if depth < CLEAR:
            push[i] = np.array(n[:]) * (CLEAR - depth)
    if step < PUSH_ROUNDS:
        push = np.stack([smoothed(push[:, k], PUSH_SPREAD, mesh_edges) for k in range(3)], 1)
    co += push
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
# Points standing out of the surface like spikes (Meshy's and the reduction's own; tools/model_qa.py's test, a little stricter) are put back
# level with the points round them: only out of the surface counts, and only that way are they moved (a point of a long thin triangle lies
# far to one side of its neighbours' middle, in the surface: moved there, it folded the cloth into a pit).
bm = bmesh.new()
bm.from_mesh(shirt.data)
bm.normal_update()
typical = float(np.median([e.calc_length() for e in bm.edges]))
flattened = set()
for _ in range(SPIKE_PASSES):
    for v in bm.verts:
        if v.is_boundary or len(v.link_edges) < 3:
            continue
        middle = sum((e.other_vert(v).co for e in v.link_edges), Vector()) / len(v.link_edges)
        reach = sum(e.calc_length() for e in v.link_edges) / len(v.link_edges)
        out = (v.co - middle).dot(v.normal)
        if abs(out) > max(SPIKE_LONG * typical, SPIKE_OUT * reach):
            v.co -= v.normal * out
            flattened.add(v.index)
    bm.normal_update()
bm.to_mesh(shirt.data)
bm.free()
print("reduced: %d vertices, %d faces; %d spikes flattened" % (len(shirt.data.vertices), len(shirt.data.polygons), len(flattened)))

# ---- 5b. the collar: a crew neck's band sewn round the neckline, evenly all round (after the reduction, which would lose its groove) ----
bm = bmesh.new()
bm.from_mesh(shirt.data)
neckline = max(openings(bm), key=lambda loop: np.mean([v.co.z for e in loop for v in e.verts]))
sewn_from = len(bm.verts)
print("collar: %d faces sewn round the neckline" % sew_collar(bm, in_order(neckline)))
bm.verts.ensure_lookup_table()
lifted = 0
for v in bm.verts[sewn_from:]:   # the band kept out of the body like the rest of the shirt (in front it leant into the chest's rise to the neck)
    loc, n, _, _ = tree.find_nearest(v.co)
    depth = (v.co - loc).dot(n)
    if depth < COLLAR_CLEAR:
        v.co += n * (COLLAR_CLEAR - depth)
        lifted += 1
print("collar: %d of its points lifted out of the body" % lifted)
bm.to_mesh(shirt.data)
bm.free()
shirt.data.update()


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
