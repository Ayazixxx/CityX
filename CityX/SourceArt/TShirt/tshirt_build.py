# THE T-SHIRT, headless. The owner, 2026-10-19/20: "put this in as a wearable top called T-Shirt ... separate the body from the playermodel
# from the shirt and flatten out the lump and wrinks at the front so we can cleanly put a various textures and logos on here ... lower the
# face count"; "the fabric should wrap around the bicep tighter"; with their own cut, "fill the missing space on the collar from the meshy
# models head being removed"; "make it rounded inward and evenly dont make it raise around the shoulders"; "i want there to be a visible
# collar though"; "the shirt stretches around the armpits please detatch the sleeve from the body"; "it still stretches around the armpits.
# also the shoulder stitch line is jagged can you make it straight".
#   blender --background --factory-startup --python tshirt_build.py -- TShirtCut.glb <out .json> [<preview folder>]
# TShirtCut.glb is THE OWNER'S CUT of Meshy's "Blank Slate Character" (a ball-headed mannequin in a plain tee, one 180k-triangle surface,
# 1.9 m tall): the shirt alone, selected by hand in Blender, 8,550 triangles, still where Meshy's figure stood.
#   1. scaled to our body's height (2.6 m) and centred on its head as the whole figure was; it then lies within 1-3 cm of our body everywhere;
#   2. Meshy's collar cut away along an oval round the neck (notched where the mannequin's neck had been, standing up at the shoulders);
#   3. the sleeves pulled in round the arms (they stood 4-8 cm off them, a bell);
#   4. the front and back evened out: their distance off the body smoothed (no lumps or wrinkles for a design to ride on), never inside it;
#   5. reduced; points standing out of the surface put back level with their neighbours; a crew neck's band sewn round the oval, the same
#      all round; the sleeves and the sides under the arms made anew (Meshy's shirt webbed the arm to the ribs): a straight armhole seam
#      from the armpit's crease over the shoulder, each sleeve a tube shaped like Meshy's, a gap under each arm;
#   6. unwrapped into four pieces at fixed places on the texture (front, back, two sleeves), split along smooth seam lines, upright, not
#      mirrored, one scale; the template a design is drawn on (tshirt_uv_template.png) and a test design (tshirt_test_design.png) written;
#   7. skinned from the body (../garment_rig.py): each sleeve on its arm, the shirt's body on the torso, never a leg or an arm; round the
#      armhole's seam the half-turning shoulder bone, fading out down the sleeve and into the body; the collar's sides on the neck;
#   8. written for BlenderGarmentMesh.
import os
import sys

import bmesh
import bpy
import mathutils
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
COLLAR_NECK_SHARE, NECKLINE_FLOOR = 0.6, 1.88   # 7. its sides, above this, follow the neck by this much
COLLAR_STEPS, COLLAR_HEIGHT_ORDER = 5, 3   # points round the half turn; terms of the smooth curve the neckline's heights are put on
SNAP = 0.2                   # 6. a seam line passing this near (of the edge) an existing point moves the point onto it
SLEEVE_GAP = 0.018           # 3. the sleeves (4-8 cm off the arm) pulled in: the smooth looseness beyond SLEEVE_GAP cut to SLEEVE_KEEP of
SLEEVE_KEEP = 0.3            # itself; the detail on it (folds, the cuff's roll) kept at SLEEVE_DETAIL
SLEEVE_DETAIL, SLEEVE_SMOOTHING = 0.8, 40
PANEL_SMOOTHING = 200        # 4. the front and back: how far they stand off the body evened out (no lumps or wrinkles), the hang kept
PANEL_MIN = 0.015            # and at least this far off it (the chest came through)
PANEL_SIDES = (50.0, 80.0)   # degrees round from the front (and the back): flat up to the first, Meshy's folds again by the second
PANEL_TOP, PANEL_BOTTOM = (1.62, 1.78), (1.02, 0.98)   # and from the collar down to the hem's roll, fading over these heights
CLEAR = 0.012                # nothing nearer the body than this (it came through at the chest and the shoulders)
PUSH_ROUNDS, PUSH_SPREAD = 12, 3   # out in rounds, each push spread over its neighbours; what is left after them, pushed point by point
TRIANGLES = 5000             # 5. reduced to about this many (the collar, the seams and the armholes then add about 2,300)
SPIKE_LONG, SPIKE_OUT, SPIKE_PASSES = 3.0, 0.35, 3   # a point standing out of its neighbours' middle by this many typical edges and this part of
                                                     # its own edges is a spike
ARMPIT_CREASE = 1.76         # 5c. the armpit's crease: the height where the arm's skin meets the ribs'
ARMHOLE_TOP = (0.40, 2.0)    # the armhole's plane runs through the crease's ends and this point on the shoulder's top (out, up)
SIDE_LOW, SIDE_SPREAD = 1.30, np.radians(40.0)   # the side under each arm made anew from the crease down to here, this far round either way
SLEEVE_SPACING, SLEEVE_ROUNDING = 0.03, 4    # the made sleeve's rings this far apart; passes evening each ring's distances off the arm
SLEEVE_REACH, SLEEVE_WALL = 0.045, 0.02      # the original sleeve read up to this far off the arm's skin; where it had none, this far off it
SLEEVE_EASE = 0.25           # the top part of the sleeve over which it eases from the armhole's own distance off the arm to its own
CUFF_ROLL, CUFF_ROLL_STEPS = 0.005, 4        # the cuff's edge rolled under in a half turn of this radius
PATCH_SPACING, PATCH_SMOOTHING = 0.03, 200    # the side's new points this far apart; passes blending their distance off the body
SLEEVE_ROUND, TORSO_ROUND = 0.12, 0.28       # the round frames' radii, so a step round them is about as long as a step along them
PIECES = ["front", "back", "sleeve_r", "sleeve_l"]   # 6. where each piece goes on the texture (u0, v0, u1, v1): the front on the left, the
PIECE_BOXES = [(0.0, 0.4, 0.5, 1.0), (0.5, 0.4, 1.0, 1.0), (0.0, 0.0, 0.5, 0.4), (0.5, 0.0, 1.0, 0.4)]   # back on the right, sleeves below
PIECE_MARGIN = 0.01
TEMPLATE_SIZE, TEMPLATE_GRID = 1024, 0.1   # the design template: its size, and a grid line every 10 cm of shirt
TEMPLATE_PIECE, TEMPLATE_GRID_COLOUR, TEMPLATE_EDGE = (0.93, 0.93, 0.93), (0.8, 0.8, 0.8), (0.2, 0.2, 0.2)
TEMPLATE_MARKS = {"front": 1.62, "back": 1.62}   # the middle of the chest and of the back (a logo's usual place) crossed, at this height
ARMHOLE_BLEND, ARMHOLE_BODY_BLEND = 0.30, 0.20   # 7. how far the half-turning shoulder bone's hold reaches from the armhole's seam: down the
                                                 # sleeve, into the shirt's body


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
    """The mesh's open edges, as loops, longest first; each walked from its lowest-numbered edge, so a rebuild comes out the same."""
    bm.edges.index_update()
    left, loops = {e for e in bm.edges if e.is_boundary}, []
    while left:
        start = min(left, key=lambda e: e.index)
        left.remove(start)
        loop, frontier = [], [start]
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


def sleeve_frame(side):
    """The arm's own round frame: a point's (u, v, distance off the arm's axis), u round the arm (0 straight toward the torso, growing toward
    the front, in metres at SLEEVE_ROUND), v along it from the shoulder; and back."""
    axis = (HANDS[side] - SHOULDERS[side]) / np.linalg.norm(HANDS[side] - SHOULDERS[side])
    inward, front = -arm_outside(side), arm_front(side)

    def to_frame(p):
        q = p - SHOULDERS[side]
        along = q @ axis
        q = q - along * axis
        return np.array([np.arctan2(q @ front, q @ inward) * SLEEVE_ROUND, along, np.linalg.norm(q)])

    def from_frame(u, v, off):
        turn = u / SLEEVE_ROUND
        return SHOULDERS[side] + v * axis + off * (np.cos(turn) * inward + np.sin(turn) * front)

    return to_frame, from_frame


def torso_frame(side):
    """The torso's round frame on one side: a point's (u, v, distance off the body's upright axis), u round it (0 straight out to that side,
    growing toward the front, in metres at TORSO_ROUND), v its height; and back."""
    out, front = np.array([np.sign(SHOULDERS[side][0]), 0.0, 0.0]), np.array([0.0, -1.0, 0.0])

    def to_frame(p):
        return np.array([np.arctan2(p @ front, p @ out) * TORSO_ROUND, p[2], np.hypot(p[0], p[1])])

    def from_frame(u, v, off):
        turn = u / TORSO_ROUND
        return off * (np.cos(turn) * out + np.sin(turn) * front) + np.array([0.0, 0.0, v])

    return to_frame, from_frame


def inside_polygon(points, polygon):
    """Which of the 2D points lie inside the polygon (its corners in order)."""
    x, y = points[:, :1], points[:, 1:]
    x1, y1 = polygon[None, :, 0], polygon[None, :, 1]
    x2, y2 = np.roll(polygon[:, 0], -1)[None], np.roll(polygon[:, 1], -1)[None]
    crossing = ((y1 > y) != (y2 > y)) & (x < (x2 - x1) * (y - y1) / np.where(y2 == y1, 1e-12, y2 - y1) + x1)
    return crossing.sum(1) % 2 == 1


def distance_to_polygon(points, polygon):
    a, b = polygon, np.roll(polygon, -1, 0)
    ab = b - a
    ap = points[:, None, :] - a[None]
    along = np.clip((ap * ab[None]).sum(2) / np.maximum((ab * ab).sum(1), 1e-12)[None], 0.0, 1.0)
    off = ap - along[..., None] * ab[None]
    return np.sqrt((off * off).sum(2)).min(1)


def fill_patch(bm, ring, frame, spacing):
    """Fills a hole (its points in order round it) with cloth lying in a round frame (sleeve_frame or torso_frame): laid flat in the frame,
    the hole is triangulated with points about spacing apart inside it, and each new point's distance off the frame's axis is the smooth
    (harmonic) blend of the ring's, so the patch carries on the cloth round it. Returns the new points and faces."""
    to_frame, from_frame = frame
    rim = np.array([to_frame(np.array(v.co[:])) for v in ring])
    lo, hi = rim[:, :2].min(0), rim[:, :2].max(0)
    rows = np.arange(lo[1] + spacing * 0.43, hi[1], spacing * 0.866)   # rows half a step apart: near-even triangles
    grid = np.array([(u + (k % 2) * spacing * 0.5, v) for k, v in enumerate(rows) for u in np.arange(lo[0], hi[0], spacing)])
    grid = grid[inside_polygon(grid, rim[:, :2]) & (distance_to_polygon(grid, rim[:, :2]) > spacing * 0.5)] if len(grid) else grid
    flat = [Vector(p) for p in rim[:, :2]] + [Vector(p) for p in grid]
    _, _, triangles, origin, _, _ = mathutils.geometry.delaunay_2d_cdt(flat, [], [list(range(len(ring)))], 1, 1e-7)
    source = [o[0] for o in origin]
    assert all(origin), "the patch's triangulation made points of its own: the hole folds over itself in its frame"
    off = np.concatenate([rim[:, 2], np.full(len(grid), rim[:, 2].mean())])
    links = [[] for _ in flat]
    for tri in triangles:
        for i in range(3):
            a, b = source[tri[i]], source[tri[(i + 1) % 3]]
            links[a].append(b)
            links[b].append(a)
    for _ in range(PATCH_SMOOTHING):   # the harmonic blend: each inner point to its neighbours' mean, the ring held
        for i in range(len(ring), len(flat)):
            off[i] = np.mean(off[links[i]]) if links[i] else off[i]
    points = list(ring) + [bm.verts.new(from_frame(*grid[i], off[len(ring) + i])) for i in range(len(grid))]
    faces = []
    for tri in triangles:
        corners = [points[source[i]] for i in tri]
        middle = sum((c.co for c in corners), Vector()) / 3.0
        normal = (corners[1].co - corners[0].co).cross(corners[2].co - corners[0].co)
        outward = middle - Vector(from_frame(to_frame(np.array(middle[:]))[0], to_frame(np.array(middle[:]))[1], 0.0))
        if normal.dot(outward) < 0.0:
            corners.reverse()
        face = bm.faces.new(corners)
        face.smooth = True
        faces.append(face)
    return points[len(ring):], faces


def in_sleeve(p, planes):
    """Whether a point is a sleeve's: beyond its armhole's plane, and below the crease nearer the arm's skin than the ribs' too."""
    point, normal = planes["l" if p[0] > 0.0 else "r"]
    _, r, _ = arm_frame(p[None])
    return (p - point) @ normal > 0.0 and (p[2] >= ARMPIT_CREASE or np.hypot(p[0], p[1]) - TORSO_R > r[0] - ARM_R)


def sew_sleeve(bm, top, side, original, cuff, plane):
    """Makes a sleeve: a tube of rings from the armhole (top: its points in order once round the arm) down to the cuff (cuff: points along
    Meshy's cuff edge), each point as far off the arm's axis as the original shirt's sleeve is there (read with a ray out from the axis), or,
    where it had none (inside the arm, where its cloth crossed to the ribs), SLEEVE_WALL off the arm's skin; the rings' distances evened
    round them, eased in from the armhole's own over the top SLEEVE_EASE of the sleeve; the edge rolled under at the cuff. Returns the new
    points and faces."""
    to_frame, from_frame = sleeve_frame(side)
    axis = (HANDS[side] - SHOULDERS[side]) / np.linalg.norm(HANDS[side] - SHOULDERS[side])
    point, normal = plane
    rim = np.array([to_frame(np.array(v.co[:])) for v in top])
    turn = rim[:, 0] / SLEEVE_ROUND
    winding = np.sum(np.angle(np.exp(1j * (np.roll(turn, -1) - turn))))
    assert abs(abs(winding) - 2.0 * np.pi) < 0.5, "the %s armhole does not go once round the arm (%.1f turns)" % (side, winding / 2.0 / np.pi)
    edge = np.array([to_frame(p) for p in cuff])
    order = np.argsort(edge[:, 0])
    cuff_at = np.interp(turn, edge[order, 0] / SLEEVE_ROUND, edge[order, 1], period=2.0 * np.pi)   # how far down the arm the cuff is
    rows = int(np.ceil(np.max(cuff_at - rim[:, 1]) / SLEEVE_SPACING))
    grid = [list(top)]
    made = []
    for k in range(1, rows + 1):
        s = k / rows
        along = rim[:, 1] + (cuff_at - rim[:, 1]) * s
        off = np.empty(len(top))
        for j in range(len(top)):
            origin = Vector(SHOULDERS[side] + along[j] * axis)
            direction = (Vector(from_frame(rim[j, 0], along[j], 1.0)) - origin).normalized()
            hit, _, _, distance = original.ray_cast(origin, direction, ARM_R + SLEEVE_REACH)
            facing_ribs = abs(turn[j]) < np.pi / 2.0 and along[j] > 0.0
            if hit is not None and not facing_ribs and (np.array(hit[:]) - point) @ normal > 0.0:
                off[j] = distance
            else:
                skin = tree.ray_cast(origin, direction, 0.5)[3]
                off[j] = (skin if skin is not None else ARM_R) + SLEEVE_WALL
        for _ in range(SLEEVE_ROUNDING):
            off = 0.5 * off + 0.25 * (np.roll(off, 1) + np.roll(off, -1))
        ease = smoothstep(np.full(len(top), s), 0.0, SLEEVE_EASE)
        off = rim[:, 2] + (off - rim[:, 2]) * ease
        row = []
        for j in range(len(top)):
            p = Vector(from_frame(rim[j, 0], along[j], off[j]))
            loc, n, _, _ = tree.find_nearest(p)
            row.append(bm.verts.new(p + n * max(0.0, CLEAR - (p - loc).dot(n))))
        grid.append(row)
        made += row
    # The cuff's edge rolled under: a half turn of CUFF_ROLL, down the arm and back in under itself.
    last = [np.array(v.co[:]) for v in grid[-1]]
    for k in range(1, CUFF_ROLL_STEPS + 1):
        a = np.pi * k / CUFF_ROLL_STEPS
        row = []
        for p in last:
            q = p - SHOULDERS[side]
            radial = q - (q @ axis) * axis
            radial /= np.linalg.norm(radial)
            row.append(bm.verts.new(p + axis * CUFF_ROLL * np.sin(a) - radial * CUFF_ROLL * (1.0 - np.cos(a))))
        grid.append(row)
        made += row
    # One winding for the whole tube, the one that faces its first row out from the arm (each face on its own, the cuff's turn under was
    # wound the other way round from the rest).
    quad = [grid[0][0], grid[0][1], grid[1][1], grid[1][0]]
    q = np.array(sum((v.co for v in quad), Vector())[:]) / 4.0 - SHOULDERS[side]
    outward = Vector(q - (q @ axis) * axis)
    reverse = (quad[1].co - quad[0].co).cross(quad[3].co - quad[0].co).dot(outward) < 0.0
    faces = []
    for upper, lower in zip(grid, grid[1:]):
        for j in range(len(top)):
            quad = [upper[j], upper[(j + 1) % len(top)], lower[(j + 1) % len(top)], lower[j]]
            face = bm.faces.new(quad[::-1] if reverse else quad)
            face.smooth = True
            faces.append(face)
    return made, faces


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

# ---- 2. the neck opened on an oval: Meshy's collar cut away (notched where the mannequin's neck had been, standing up at the shoulders) ----
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

# ---- 5c. the sleeves and the sides under the arms made anew: a gap under each arm, the armhole up at the armpit ----
# Owner, 2026-10-20: "it still stretches around the armpits". Meshy's mannequin stood arms down, and its shirt never went between the arm
# and the ribs: from the armpit down past the cuff's top it ran from the chest straight across to the front of the arm, round the arm's
# outside and back across to the shoulder blade, like a bat's wing; the sleeve had no inside and the shirt no side there. No skinning can
# move that between a swinging arm and the ribs without stretching it many times over (the game measured 16x). Games model clothes arms
# out, with a gap under the arm and the armhole at the armpit, and lay clean game meshes over the sculpted ones; so here, on each side:
#   - the armhole: a flat plane through the armpit's crease (where the arm's skin meets the ribs') and the shoulder's top, so its seam over
#     the shoulder is straight; under the arm, the crease itself;
#   - the sleeve is taken away and made again as a tube from the armhole to the cuff, its shape read off Meshy's sleeve (each point as far off
#     the arm's axis as Meshy's cloth is there), and where Meshy had no sleeve, inside the arm, SLEEVE_WALL off the arm's skin; its edge
#     rolled under at the cuff;
#   - the shirt's side under the arm, from the crease down to SIDE_LOW, SIDE_SPREAD round either way, is made again too: new cloth in the
#     torso's own round frame, carrying on the cloth round it.
bm = bmesh.new()
bm.from_mesh(shirt.data)
original = BVHTree.FromBMesh(bm)   # the sleeves' shape is read off the shirt as it was
co = np.array([v.co[:] for v in bm.verts])
cuffs = {side: np.array([v.co[:] for e in min(openings(bm), key=lambda loop: np.linalg.norm(
    np.mean([v.co[:] for e in loop for v in e.verts], 0) - HANDS[side])) for v in e.verts]) for side in SHOULDERS}
planes = {}
field = np.full(len(co), -1.0)
for side in SHOULDERS:
    out = np.sign(SHOULDERS[side][0])
    axis = (HANDS[side] - SHOULDERS[side]) / np.linalg.norm(HANDS[side] - SHOULDERS[side])
    axis_x = lambda z: SHOULDERS[side][0] + (z - SHOULDERS[side][2]) / axis[2] * axis[0]
    # The crease's two ends: where the line midway between the arm's skin and the ribs' crosses the crease's height, front and back.
    crossings = np.array([a.co.lerp(b.co, (ARMPIT_CREASE - a.co.z) / (b.co.z - a.co.z))[:] for a, b in (e.verts for e in bm.edges)
                          if a.co.x * out > 0.0 and (a.co.z - ARMPIT_CREASE) * (b.co.z - ARMPIT_CREASE) < 0.0])
    _, cr, _ = arm_frame(crossings)
    midway = np.abs((np.hypot(crossings[:, 0], crossings[:, 1]) - TORSO_R) - (cr - ARM_R))
    inside = crossings[:, 0] * out < axis_x(ARMPIT_CREASE) * out
    ends = [crossings[np.argmin(np.where(inside & ((crossings[:, 1] < 0.0) == front), midway, np.inf))] for front in (True, False)]
    over = np.array([out * ARMHOLE_TOP[0], 0.0, ARMHOLE_TOP[1]])
    normal = np.cross(ends[1] - ends[0], over - ends[0])
    normal *= np.sign(normal[0] * out) / np.linalg.norm(normal)   # above 0 out toward the arm
    planes[side] = (ends[0], normal)
    this = co[:, 0] * out > 0.0
    beyond = (co - ends[0]) @ normal
    field[this] = np.where(co[:, 2] >= ARMPIT_CREASE, beyond, np.minimum(beyond, -1e-3))[this]   # the seam cut above the crease only
level = bm.verts.layers.float.new("armhole")
for v, value in zip(bm.verts, field):
    v[level] = value
iso_split(bm, level)   # the armhole's seam over the shoulder becomes edges
bmesh.ops.delete(bm, geom=[f for f in bm.faces if in_sleeve(np.array(f.calc_center_median()[:]), planes)], context="FACES")   # the sleeves go
for v in bm.verts:   # the sides under the arms go: below the crease, SIDE_SPREAD round either way
    out = np.sign(v.co.x) or 1.0
    turn = abs(np.arctan2(-v.co.y, v.co.x * out))
    v[level] = -min(ARMPIT_CREASE - v.co.z, v.co.z - SIDE_LOW, SIDE_SPREAD - turn)
iso_cut(bm, level)
keep_joined_to_hem(bm)
sleeves, seams = set(), set()
for side in SHOULDERS:
    out = np.sign(SHOULDERS[side][0])
    point, normal = planes[side]
    under_arm = np.array([out * TORSO_R, 0.0, (ARMPIT_CREASE + SIDE_LOW) / 2.0])   # the hole left there: the side's, open up to the armhole
    ring = in_order(min(openings(bm), key=lambda loop: np.linalg.norm(
        np.mean([v.co[:] for e in loop for v in e.verts], 0) - under_arm)))
    on_seam = [abs((np.array(v.co[:]) - point) @ normal) < 1e-5 and v.co.z >= ARMPIT_CREASE - 1e-4 for v in ring]
    assert any(on_seam) and not all(on_seam), "the armhole's seam over the %s shoulder is missing from the hole's rim" % side
    start = next(i for i in range(len(ring)) if on_seam[i] and not on_seam[i - 1])   # the seam's run round the rim, in the rim's order
    ring, on_seam = ring[start:] + ring[:start], on_seam[start:] + on_seam[:start]
    length = on_seam.index(False)
    seam, rest = ring[:length], ring[length - 1:] + ring[:1]   # over the shoulder; and back round the side's hole, both from end to end
    # The crease: straight across under the arm between the seam's two ends, just clear of the body.
    steps = max(2, int(np.ceil((seam[0].co - seam[-1].co).length / PATCH_SPACING)))
    crease = []
    for k in range(1, steps):
        p = seam[-1].co.lerp(seam[0].co, k / steps)
        loc, n, _, _ = tree.find_nearest(p)
        crease.append(bm.verts.new(p + n * max(0.0, CLEAR - (p - loc).dot(n))))
    _, side_faces = fill_patch(bm, rest + crease[::-1], torso_frame(side), PATCH_SPACING)
    tube, sleeve_faces = sew_sleeve(bm, seam + crease, side, original, cuffs[side], planes[side])
    sleeves |= set(tube)
    seams |= set(seam + crease)
    print("underarm %s: the side remade with %d faces, the sleeve with %d" % (side, len(side_faces), len(sleeve_faces)))
for v in bm.verts:
    v[level] = 0.0 if v in seams else 1.0 if v in sleeves else -1.0
bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])
bm.to_mesh(shirt.data)
bm.free()
shirt.data.update()
if PREVIEW:
    preview([shirt, body, head], os.path.join(PREVIEW, "underarm_on_body.png"), [(0.9, 0.3, 0.25, 1), (0.7, 0.7, 0.7, 1), (0.7, 0.7, 0.7, 1)])


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
armhole = np.empty(len(co))
shirt.data.attributes["armhole"].data.foreach_get("value", armhole)
fields = {"sleeve": armhole,                                                       # above 0 on a sleeve (5c)
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
shirt.data.attributes.new("piece", "INT", "FACE").data.foreach_set("value", piece.astype(np.int32))   # for 7: which piece each face is
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
# Each sleeve follows its arm and the shirt's body the torso (owner: "the shirt stretches around the armpits please detatch the sleeve from
# the body"); the armhole's seam, where they are sewn, follows the half-turning shoulder bone, the sleeve going over to its arm smoothly
# along ARMHOLE_BLEND of it and the body back to the torso along ARMHOLE_BODY_BLEND (over 5 cm, the cloth at the seam was drawn out 6x in a
# sprint). With the web gone (5c) the seam runs close round the shoulder joint, where an arm's swing moves the cloth least.
bm = bmesh.new()
bm.from_mesh(mesh)
piece_of = bm.faces.layers.int.get("piece")
sleeve_pieces = {PIECES.index("sleeve_" + side): side for side in SHOULDERS}
bm.verts.index_update()
sleeve_of = np.full(len(bm.verts), "", dtype=object)   # the side of the sleeve each point is on, "" on the shirt's body
rims = {side: [] for side in SHOULDERS}
for v in bm.verts:
    sides = {sleeve_pieces.get(f[piece_of], "") for f in v.link_faces}
    for side in sides - {""}:
        sleeve_of[v.index] = side
        if "" in sides:
            rims[side].append(v.co[:])
bm.free()
rims = {side: np.array(points) for side, points in rims.items()}
print("armholes: %s points along their seams" % ", ".join("%d" % len(p) for p in rims.values()))

run(os.path.join(os.path.dirname(HERE), "garment_rig.py"))
weights = body_weights(shirt, body, bpy.data.objects["CityXRig"])
for leg in ("thigh_l", "thigh_r"):   # the hem ends at the hips: it follows them, never a leg
    weights[:, BONE["hips"]] += weights[:, BONE[leg]]
    weights[:, BONE[leg]] = 0.0
# The collar's band follows the neck at the sides (on the torso alone, the head dipping in a sprint sank 1.8 cm into it beside the neck);
# not in front and behind, where the neck's own turn pushed it into the chest.
sideways = co[:, 0] ** 2 / np.maximum(co[:, 0] ** 2 + co[:, 1] ** 2, 1e-9)
collar = (1.0 - smoothstep(np.hypot(co[:, 0] / NECK_SIDE, co[:, 1] / NECK_FRONT), 1.05, 1.2)) * (co[:, 2] > NECKLINE_FLOOR)   # the band
share = COLLAR_NECK_SHARE * sideways * collar
weights *= (1.0 - share)[:, None]
weights[:, BONE["neck"]] += share
on_body = sleeve_of == ""
collar_points = co[collar > 0.5]
for side in SHOULDERS:
    for bone in ("arm_" + side, "forearm_" + side, "hand_" + side):   # the shirt's body never follows an arm
        weights[on_body, BONE["spine"]] += weights[on_body, BONE[bone]]
        weights[on_body, BONE[bone]] = 0.0
    # A sleeve follows its arm as the skin under it does (near the cuff, partly the forearm: the elbow bends there), never the torso.
    on_sleeve = sleeve_of == side
    arm_bones = [BONE["arm_" + side], BONE["forearm_" + side]]
    kept = weights[np.ix_(on_sleeve, arm_bones)]
    kept[kept.sum(1) <= 1e-6, 0] = 1.0
    weights[on_sleeve] = 0.0
    weights[np.ix_(on_sleeve, arm_bones)] = kept / kept.sum(1, keepdims=True)
    from_rim = np.min(np.linalg.norm(co[:, None, :] - rims[side][None, :, :], axis=2), axis=1)
    this_side = co[:, 0] * np.sign(SHOULDERS[side][0]) > 0.0
    # Never the collar: on the shoulder bone, an arm swing lifted it into the head; the hold fades out on the way to it (cut off at it, the
    # cloth beside it stretched): from the whole of it at the seam to none at the collar, by how far each point is from the one and the other.
    from_collar = np.array([np.min(np.linalg.norm(collar_points - p, axis=1)) for p in co])
    off_collar = 1.0 - smoothstep(from_rim / np.maximum(from_rim + from_collar, 1e-9), 0.0, 1.0)
    for share in (on_sleeve * (1.0 - smoothstep(from_rim, 0.0, ARMHOLE_BLEND)),
                  (on_body & this_side) * np.minimum(off_collar, 1.0 - smoothstep(from_rim, 0.0, ARMHOLE_BODY_BLEND))):
        weights *= (1.0 - share)[:, None]
        weights[:, BONE["shoulder_" + side]] += share

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
