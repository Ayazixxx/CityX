# THE BEARDS, built in code (owner, 2026-11-22: "try making a few basic beard styles and put them into the editor, i dont have a beard im not a beard
# guy i wouldnt know which ones are good. make them not super big so they clip into clothing and stuff make them not much farther out then the head";
# "research common beard styles in games"). Headless:
#   blender --background --factory-startup --python beard_build.py -- [--only <name>] [--render <out_dir>] [--export <dir>]
# then CityX/Bake Beards in Unity (docs/BEARDS.md).
#
# The head is a ball (FusedBodyShape.HeadRadius, 0.35 m) with a face drawn on it, so a beard is laid on the ball: a thickness over the head's
# surface, given as a shape on the face's own map (how far round from the front, how far up or down). Nothing stands more than 3 cm off the
# head, and nothing goes below the head's lower curve toward the neck, so a collar or a coat never meets it.
#
# Two ways of making one are here. The FULL beards (full_beard with clean lines, spiky_beard with a spiked cheek line and hem) are one smooth
# skin with a traced edge: see "the full beards" below. The moustache and the circle beard are the older make, a base with locks of hair over
# it, which the owner called cheap in the full beard and has set aside ("pause on the circle beard and moustache. i may end up remove those
# completely"): left as they were. Stubble is skin colour, not a shape: it belongs to the paint.
#
# Where the mouth is: the face is drawn by the player, so it is wherever they put it. These sit round where a face's mouth usually goes on this
# head: a third of the way down the front (MOUTH_LAT).
#
# Axes: the head's own frame, origin at the ball's centre, Unity's (x right, y up, z forward); written to Blender's (-x, -z, y), which the glTF
# exporter and Unity's importer turn back.
import math
import os
import sys

import bpy
from mathutils import Vector
from mathutils.geometry import delaunay_2d_cdt

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

HEAD_RADIUS = 0.35
# Where the face's features sit, degrees of latitude on the ball (0 the middle, up positive): the mouth, and the lowest a beard goes (below it the
# ball curves in to the neck and a collar).
MOUTH_LAT = -31.0
LOWEST_LAT = -68.0
# A beard stands this far off the skin at most, metres, and its edge goes this far into the skin.
SUNK = 0.004
EDGE_DEGREES = 1.6
STEP_DEGREES = 1.0
# The base under the locks is thinned to about this many triangles (a whole beard comes to about a hairstyle's 800 to 1,600).
MOST_TRIANGLES = 700


def smooth(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3.0 - 2.0 * u)


def capsule(x, y, ax, ay, bx, by, r):
    """Signed distance (degrees on the face map) to a rounded stroke from a to b, r wide either side."""
    px, py, dx, dy = x - ax, y - ay, bx - ax, by - ay
    h = max(0.0, min(1.0, (px * dx + py * dy) / (dx * dx + dy * dy)))
    return math.hypot(px - dx * h, py - dy * h) - r


def ellipse(x, y, cx, cy, rx, ry):
    k = math.hypot((x - cx) / rx, (y - cy) / ry)
    return (k - 1.0) * min(rx, ry)


# ---- the base: a thin solid layer under the locks, so no skin shows between them. Signed distance in degrees (negative inside) at a point x
# ---- (degrees round the front, scaled by cos lat so it is real distance on the ball) and y (degrees of latitude) ----

def smooth_min(a, b, k):
    """The nearer of two shapes, the corner where they meet rounded over k degrees: a join that grows out of both rather than a seam."""
    h = max(k - abs(a - b), 0.0) / k
    return min(a, b) - h * h * k * 0.25


def moustache(x, y):
    # Thick in the middle, thinning to the corners of the mouth and on past them, its ends drooping; the parting under the nose notches its top.
    ax = abs(x)
    yc = MOUTH_LAT + 7.0 - 0.014 * x * x
    half = 3.6 * math.sqrt(max(0.0, 1.0 - (x / 21.0) ** 2))
    top = yc + half - 1.6 * smooth(1.0 - ax / 3.0)
    return max(y - top, (yc - half) - y, ax - 21.0)


def goatee(x, y):
    d = ellipse(x, y, 0.0, MOUTH_LAT - 18.0, 13.0, 12.0)
    return max(d, (LOWEST_LAT + 7.0) - y)


def mouth_gap(x, y, wider, taller):
    return ellipse(x, y, 0.0, MOUTH_LAT, 15.0 + wider, 3.0 + taller)


def circle(x, y):
    sides = min(capsule(x, y, 17.5, MOUTH_LAT + 4.0, 12.0, MOUTH_LAT - 14.0, 3.0), capsule(x, y, -17.5, MOUTH_LAT + 4.0, -12.0, MOUTH_LAT - 14.0, 3.0))
    ring = smooth_min(smooth_min(moustache(x, y), sides, 3.0), goatee(x, y), 4.0)
    return max(ring, -mouth_gap(x, y, 1.6, 1.6))


def sideburns(x, y):
    return min(capsule(x, y, 79.0, -2.0, 80.0, 10.0, 3.6), capsule(x, y, -79.0, -2.0, -80.0, 10.0, 3.6))


# The beards of locks. name: (base shape or None, the base's thickness at the cheeks, at the chin, at the sideburns), metres. A moustache is
# locks alone.
STYLES = {
    "moustache": (None, 0.0, 0.0, 0.0),
    "circle_beard": (circle, 0.009, 0.011, 0.009),
}
ORDER = ("moustache", "circle_beard", "full_beard", "spiky_beard")


def thickness(style, x, y):
    shape, cheek, chin, burns = STYLES[style]
    d = shape(x, y)
    if d > EDGE_DEGREES:
        return None
    toward_chin = smooth((-(y - MOUTH_LAT) - 8.0) / 25.0) * smooth(1.0 - abs(x) / 30.0)
    full = cheek + (chin - cheek) * toward_chin
    full += (burns - cheek) * smooth((abs(x) - 55.0) / 25.0) * smooth((y + 20.0) / 25.0)
    # Out of the skin over the edge, standing at its full thickness a degree and a half in.
    u = smooth((EDGE_DEGREES - d) / (2.0 * EDGE_DEGREES))
    return -SUNK + (full + SUNK) * u


# ---- the clumps: what makes it read as a beard. Cartoon beards in games (the low-poly Santas, Sketchfab's cartoon hair packs) are a handful of
# ---- big chunky locks with pointed tips laid over each other, not a smooth lump: a moustache in two swept wings, a beard ending in a few large
# ---- points. Each lock here runs along a path on the face map (x, y as above), as wide as `width` degrees at its root, tapering to a point,
# ---- standing up to `lift` metres off the head with a ridge down its middle, so the cel shading lights one side and shades the other.

CLUMP_SAMPLES = 9


def catmull(points, t):
    """A point t (0-1) along a smooth curve through the points."""
    n = len(points) - 1
    f = min(t * n, n - 1e-6)
    i = int(f)
    u = f - i
    p0, p1, p2, p3 = points[max(i - 1, 0)], points[i], points[i + 1], points[min(i + 2, n)]
    return tuple(0.5 * ((2.0 * p1[k]) + (-p0[k] + p2[k]) * u + (2.0 * p0[k] - 5.0 * p1[k] + 4.0 * p2[k] - p3[k]) * u * u
                        + (-p0[k] + 3.0 * p1[k] - 3.0 * p2[k] + p3[k]) * u * u * u) for k in (0, 1))


def on_ball(x, y, lift):
    """A face-map point (degrees) lifted off the ball, in Blender's axes."""
    lat = math.radians(y)
    lon = math.radians(x / max(math.cos(lat), 0.2))
    r = HEAD_RADIUS + lift
    ux, uy, uz = r * math.cos(lat) * math.sin(lon), r * math.sin(lat), r * math.cos(lat) * math.cos(lon)
    return (-ux, -uz, uy)


def lock(verts, faces, path, width, lift):
    """One lock of hair: rings of five across it (edge, shoulder, ridge, shoulder, edge) along its path, closed at the tip in a point."""
    rings = []
    for s in range(CLUMP_SAMPLES):
        t = s / float(CLUMP_SAMPLES)
        x, y = catmull(path, t)
        ax, ay = catmull(path, min(1.0, t + 0.02))
        bx, by = catmull(path, max(0.0, t - 0.02))
        dx, dy = ax - bx, ay - by
        length = math.hypot(dx, dy) or 1.0
        nx, ny = -dy / length, dx / length
        w = width * min(1.0, 0.55 + 3.0 * t) * (1.0 - t ** 1.5)   # a rounded root, widest a fifth of the way along, to a point
        top = lift * (0.6 + 0.4 * math.sin(math.pi * min(1.0, 0.35 + t)))
        ring = []
        for share, height in ((-1.0, -SUNK), (-0.5, 0.78), (0.0, 1.0), (0.5, 0.78), (1.0, -SUNK)):
            h = height if height < 0.0 else top * height
            ring.append(len(verts))
            verts.append(on_ball(x + nx * w * share, y + ny * w * share, h))
        rings.append(ring)
    tx, ty = path[-1]
    tip = len(verts)
    verts.append(on_ball(tx, ty, lift * 0.25))
    for a, b in zip(rings, rings[1:]):
        for k in range(4):
            faces.append((a[k], a[k + 1], b[k + 1], b[k]))
    last = rings[-1]
    for k in range(4):
        faces.append((last[k], last[k + 1], tip))


def both(path):
    """A path and its mirror on the other side of the face."""
    return [path, [(-x, y) for x, y in path]]


M = MOUTH_LAT


def moustache_locks():
    locks = []
    for side in both([(1.0, M + 8.6), (8.0, M + 8.2), (15.0, M + 6.0), (21.0, M + 2.0), (24.5, M - 2.5)]):
        locks.append((side, 5.0, 0.020))
    for side in both([(2.0, M + 9.6), (8.5, M + 9.8), (14.0, M + 8.4)]):
        locks.append((side, 2.4, 0.022))
    return locks


def chin_locks(long_tip, side_tip):
    locks = [([(0.0, M - 9.0), (0.0, M - 20.0), (0.0, long_tip)], 7.0, 0.025)]
    for side in both([(7.0, M - 9.0), (9.0, M - 19.0), (10.5, side_tip)]):
        locks.append((side, 5.6, 0.021))
    return locks


def circle_locks():
    locks = moustache_locks() + chin_locks(M - 35.0, M - 30.0)
    for side in both([(19.0, M + 3.0), (17.5, M - 6.0), (12.5, M - 13.0)]):
        locks.append((side, 3.0, 0.016))
    return locks


LOCKS = {"moustache": moustache_locks, "circle_beard": circle_locks}


# ---- the full beards: ONE smooth skin with a shaped edge, no locks (owner, 2026-11-22, of the locks: "it looks cheap and the attempt at improving
# ---- it was just adding a bunch of lumps to show texture"; "just make everything blend together well. get a good shape and have no visible creases
# ---- or lines"; "smooth but shaped with some hard edges so it looks like a stylized beard, toward the bottom like hair spikes, not roblox boxy";
# ---- then, of the first spiked one, "like this but smooth basically ... have the combination of spikey cheek line + spikey bottom be a style and
# ---- then the crisp lines on top and bottom be another style. make sure to smooth out the spikey one so the creases arent visible in celshade
# ---- just the shape"). The game's lines are drawn where the surface turns sharply (CityXOutline's fold, about 45 degrees between neighbouring
# ---- pixels), so the skin turns nowhere by that much: its thickness changes slowly. Only its rim is hard, for the one line round it (build_shell).
# ---- What reads is the outline, so that is traced exactly: the shape's edge found on a fine map, each corner kept, and the skin triangulated
# ---- inside it.

# The cheek line's spikes and the hem's, from the middle of the face out to the sideburn: (from, to, how far it stands out, where along it the
# point is), all in degrees on the face map. The cheek's lean back toward the ear and the hem's toward the chin, as hair spikes are swept;
# their sizes differ so no two neighbours match. The cheek's are the smaller and their points are rounded off (owner, 2026-11-22, of the first,
# smaller ones against bigger sharp ones: "i think the top spikes looked better here less sharp spikes").
CHEEK_SPIKES = ((25.0, 35.0, 5.5, 0.72), (35.0, 46.0, 7.5, 0.72), (46.0, 56.0, 5.5, 0.72), (56.0, 67.0, 7.5, 0.72), (67.0, 77.0, 5.0, 0.72))
CHEEK_POINTS_ROUNDED = 0.06
HEM_SPIKES = ((0.0, 9.0, 10.0, 0.0), (9.0, 22.0, 7.5, 0.25), (22.0, 36.0, 10.0, 0.25), (36.0, 51.0, 7.5, 0.25), (51.0, 66.0, 9.5, 0.25),
              (66.0, 80.0, 6.0, 0.25))
# The lines the spikes stand on are drawn in from the crisp beard's by this much, so the spiked beard covers about as much face.
CHEEK_SET_IN = 2.5
HEM_SET_IN = 4.0


def spikes(ax, teeth, rounded=0.0):
    for a, b, depth, peak in teeth:
        if a <= ax < b:
            u = (ax - a) / (b - a)
            t = (1.0 - u) if peak <= 0.0 else (u / peak if u < peak else (1.0 - u) / (1.0 - peak))
            if rounded > 0.0:
                # The point rounded over: a curve that follows the straight sides and turns under the tip.
                low = rounded - math.hypot(1.0, rounded)
                t = (rounded - math.hypot(1.0 - t, rounded) - low) / (1.0 - low) * (1.0 - rounded)
            return depth * t
    return 0.0


def cheek_line(ax):
    # Level over the lip, a little higher under the nose, then up the cheek to the sideburn: one clean curve.
    return MOUTH_LAT + 8.5 + 3.5 * smooth(1.0 - ax / 22.0) + 24.0 * smooth((ax - 20.0) / 62.0)


def hem_line(ax):
    return LOWEST_LAT + 3.0 + 30.0 * smooth((ax - 30.0) / 50.0)


def full_shape(x, y, spiked):
    """Over the jaw and the cheeks from sideburn to sideburn; the mouth left open, wider and taller than the circle beard's (owner, 2026-11-22:
    "make the full beard mouth hole slightly bigger")."""
    ax = abs(x)
    top, bottom = cheek_line(ax), hem_line(ax)
    if spiked:
        top += spikes(ax, CHEEK_SPIKES, CHEEK_POINTS_ROUNDED) - CHEEK_SET_IN * smooth((ax - 22.0) / 6.0)
        bottom += HEM_SET_IN - spikes(ax, HEM_SPIKES)
    d = max(y - top, bottom - y, ax - 82.0)
    d = smooth_min(d, sideburns(x, y), 4.0)
    return max(d, -mouth_gap(x, y, 3.2, 2.6))


def full_beard(x, y):
    return full_shape(x, y, False)


def spiky_beard(x, y):
    return full_shape(x, y, True)


# name: (shape, thickness at the cheeks, at the chin, at the sideburns, whether its edge is spiked), metres.
SHELLS = {
    "full_beard": (full_beard, 0.009, 0.013, 0.006, False),
    "spiky_beard": (spiky_beard, 0.009, 0.013, 0.006, True),
}
# Over the lip the beard is this share of the cheek's thickness.
LIP_SHARE = 0.6
# A spike thins to this share of the beard's thickness at its point, over this many degrees past the line it stands on (the cheek's, the hem's): a
# blade, not a block.
SPIKE_POINT_SHARE = 0.4
CHEEK_THINS_OVER = 5.0
HEM_THINS_OVER = 9.0
# The rim's skirt goes down to the head, and on under the skin, at this slope from the head's surface: wide enough on the screen (about
# 6 mm) for the line pass to find it at the game camera's distance.
EDGE_SLOPE = math.radians(35.0)
SHELL_SUNK = 0.002
# The skin comes down to this height at its rim, over this many degrees in from it; and the rim's skirt reaches this far out, degrees.
RIM_HEIGHT = 0.004
RIM_BEVEL = 3.5
# No further than this: leaning more, the rim falls into the cel shading's dark side wherever it leans away from the sun, a dark band.
RIM_LEAN = math.radians(22.0)
RIM_REACH = (RIM_HEIGHT + SHELL_SUNK) / math.tan(EDGE_SLOPE) / (HEAD_RADIUS * math.radians(1.0))
# The map the edge is traced on, the furthest a traced corner may be cut, the longest stretch of edge without a vertex, and how far apart the
# skin's vertices are inside it; degrees.
TRACE_STEP = 0.5
TRACE_TOLERANCE = 0.12
EDGE_LONGEST = 2.5
INSIDE_STEP = 3.5


def shell_thickness(style, x, y):
    _, cheek, chin, burns, spiked = SHELLS[style]
    ax = abs(x)
    toward_chin = smooth((-(y - MOUTH_LAT) - 8.0) / 25.0) * smooth(1.0 - ax / 30.0)
    full = cheek + (chin - cheek) * toward_chin
    full += (burns - cheek) * smooth((ax - 55.0) / 25.0) * smooth((y + 20.0) / 25.0)
    # Thinner over the lip, a narrow band between the cheek line and the mouth: its edges would meet in the middle otherwise.
    full *= 1.0 - (1.0 - LIP_SHARE) * smooth((y - MOUTH_LAT) / 5.0) * smooth((26.0 - ax) / 8.0)
    if spiked:
        up = (y - (cheek_line(ax) - CHEEK_SET_IN)) / CHEEK_THINS_OVER * smooth((ax - 22.0) / 6.0)
        down = ((hem_line(ax) + HEM_SET_IN) - y) / HEM_THINS_OVER
        full *= 1.0 - (1.0 - SPIKE_POINT_SHARE) * smooth(max(up, down))
    # Down to a low rim at its edge, over a slope too gentle to be inked: the rim's skirt is then never tall enough on the screen, from any
    # side, for the line pass to ink its top and its foot apart (owner: "now there is two beard lines", "i see two still").
    inside = -SHELLS[style][0](x, y) - RIM_REACH
    return RIM_HEIGHT + max(full - RIM_HEIGHT, 0.0) * smooth(inside / RIM_BEVEL)


def edge_reach(style, x, y):
    """How far out from the skin's rim its edge meets the head, degrees: the thickness there, down the slope."""
    return RIM_REACH


def trace(shape):
    """The shape's edge as closed loops of points on the face map, found square by square (marching squares) and joined end to end."""
    xs = [-96.0 + i * TRACE_STEP for i in range(int(192.0 / TRACE_STEP) + 1)]
    ys = [-80.0 + j * TRACE_STEP for j in range(int(98.0 / TRACE_STEP) + 1)]
    value = [[shape(x, y) for x in xs] for y in ys]
    points, joins = {}, {}

    def crossing(i0, j0, i1, j1):
        key = (i0, j0, i1, j1)
        if key not in points:
            a, b = value[j0][i0], value[j1][i1]
            u = a / (a - b)
            points[key] = (xs[i0] + (xs[i1] - xs[i0]) * u, ys[j0] + (ys[j1] - ys[j0]) * u)
        return key

    for j in range(len(ys) - 1):
        for i in range(len(xs) - 1):
            inside = [value[j][i] < 0.0, value[j][i + 1] < 0.0, value[j + 1][i + 1] < 0.0, value[j + 1][i] < 0.0]
            if all(inside) or not any(inside):
                continue
            sides = [(i, j, i + 1, j), (i + 1, j, i + 1, j + 1), (i, j + 1, i + 1, j + 1), (i, j, i, j + 1)]
            cut = [crossing(*s) for s, (a, b) in zip(sides, ((0, 1), (1, 2), (3, 2), (0, 3))) if inside[a] != inside[b]]
            pairs = [(cut[0], cut[1])] if len(cut) == 2 else ([(cut[0], cut[1]), (cut[2], cut[3])] if inside[1] else [(cut[0], cut[3]), (cut[1], cut[2])])
            for a, b in pairs:
                joins.setdefault(a, []).append(b)
                joins.setdefault(b, []).append(a)
    loops, seen = [], set()
    for start in joins:
        if start in seen:
            continue
        loop, at, came = [], start, None
        while at not in seen:
            seen.add(at)
            loop.append(points[at])
            onward = [n for n in joins[at] if n != came]
            came, at = at, onward[0]
        loops.append(loop)
    return loops


def thinned(loop):
    """The loop with every point dropped that lies within TRACE_TOLERANCE of the line through its neighbours (corners stay), and none of what is
    left further than EDGE_LONGEST from the next."""
    def keep(points):
        if len(points) < 3:
            return points
        (ax, ay), (bx, by) = points[0], points[-1]
        length = math.hypot(bx - ax, by - ay) or 1e-9
        far, where = 0.0, 0
        for k in range(1, len(points) - 1):
            off = abs((points[k][0] - ax) * (by - ay) - (points[k][1] - ay) * (bx - ax)) / length
            if off > far:
                far, where = off, k
        if far <= TRACE_TOLERANCE:
            return [points[0], points[-1]]
        return keep(points[:where + 1])[:-1] + keep(points[where:])

    # Cut at the two points furthest apart, so a closed loop is two open runs.
    far = max(range(len(loop)), key=lambda k: (loop[k][0] - loop[0][0]) ** 2 + (loop[k][1] - loop[0][1]) ** 2)
    kept = keep(loop[:far + 1])[:-1] + keep(loop[far:] + [loop[0]])[:-1]
    out = []
    for k, (ax, ay) in enumerate(kept):
        bx, by = kept[(k + 1) % len(kept)]
        pieces = max(1, int(math.ceil(math.hypot(bx - ax, by - ay) / EDGE_LONGEST)))
        for p in range(pieces):
            out.append((ax + (bx - ax) * p / pieces, ay + (by - ay) * p / pieces))
    return out


def build_shell(style):
    """A full beard: the skin triangulated inside its traced edge, lifted off the ball by its thickness, and a skirt from the edge down under the
    head's surface."""
    # The rim is drawn in from the shape by as much as the edge then runs out, so where the beard meets the head IS the shape.
    def shape(x, y):
        return SHELLS[style][0](x, y) + edge_reach(style, x, y)

    loops = [thinned(loop) for loop in trace(shape) if len(loop) > 12]
    flat, edges = [], []
    for loop in loops:
        first = len(flat)
        flat.extend(loop)
        edges.extend((first + k, first + (k + 1) % len(loop)) for k in range(len(loop)))
    on_edge = len(flat)
    row = 0
    y = -80.0
    while y < 18.0:
        x = -96.0 + (INSIDE_STEP * 0.5 if row % 2 else 0.0)
        while x < 96.0:
            if shape(x, y) < -INSIDE_STEP * 0.45:
                flat.append((x, y))
            x += INSIDE_STEP
        y += INSIDE_STEP * 0.866
        row += 1
    out_points, _, out_faces, came_from, _, _ = delaunay_2d_cdt([Vector(p) for p in flat], edges, [], 0, 1e-5)
    now = {}
    for index, sources in enumerate(came_from):
        for source in sources:
            now[source] = index
    verts = [on_ball(p.x, p.y, shell_thickness(style, p.x, p.y)) for p in out_points]
    faces = []
    for face in out_faces:
        cx, cy = sum(out_points[k].x for k in face) / len(face), sum(out_points[k].y for k in face) / len(face)
        if shape(cx, cy) < 0.0:
            faces.append(tuple(face))
    # The skirt: each edge point's twin, out from the beard and under the skin, as far out as the slope takes to get there.
    skin_faces, wall, rim_out = len(faces), {}, {}
    first = 0
    for loop in loops:
        twins = []
        for k, (x, y) in enumerate(loop):
            (px, py), (nx, ny) = loop[k - 1], loop[(k + 1) % len(loop)]
            # Straight out from each of the two stretches of rim that meet here, and between them far enough to be that far out from both: a
            # spike keeps its point.
            ax, ay, bx, by = y - py, -(x - px), ny - y, -(nx - x)
            al, bl = math.hypot(ax, ay) or 1e-9, math.hypot(bx, by) or 1e-9
            ax, ay, bx, by = ax / al, ay / al, bx / bl, by / bl
            ox, oy = ax + bx, ay + by
            length = math.hypot(ox, oy) or 1e-9
            ox, oy = ox / length, oy / length
            sharp = 1.0 / max(abs(ox * ax + oy * ay), 0.4)
            if shape(x + ox * 0.4, y + oy * 0.4) < shape(x - ox * 0.4, y - oy * 0.4):
                ox, oy = -ox, -oy
            out = sharp * (shell_thickness(style, x, y) + SHELL_SUNK) / math.tan(EDGE_SLOPE) / (HEAD_RADIUS * math.radians(1.0))
            twins.append(len(verts))
            verts.append(on_ball(x + ox * out, y + oy * out, -SHELL_SUNK))
            # Which way the skirt is SAID to face: flat out along the head from the rim, whatever its slope.
            rim, foot = Vector(verts[now[first + k]]), Vector(verts[-1])
            along = ((foot - rim) - rim.normalized() * (foot - rim).dot(rim.normalized())).normalized()
            wall[twins[-1]] = (along - rim.normalized() * 0.18).normalized()
            rim_out[now[first + k]] = along
        for k in range(len(loop)):
            a, b = now[first + k], now[first + (k + 1) % len(loop)]
            faces.append((a, b, twins[(k + 1) % len(loop)], twins[k]))
        first += len(loop)
    mesh = bpy.data.meshes.new(style)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    for p in mesh.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new(style, mesh)
    bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    ob.select_set(False)
    if sum(p.normal.dot(p.center) for p in mesh.polygons) < 0.0:
        mesh.flip_normals()
    # THE BLACK LINE round the beard (owner, 2026-11-22: "how can we get the black line on the edge of the beard like the hair above it. it only
    # has it in certain places"). The line pass inks where neighbouring pixels face more than about 45 degrees apart, and a gentle skirt shaded
    # smoothly never does. A skirt said to face flat out along the head is inked, but on BOTH its sides once it is a few pixels wide (zoomed
    # in in the creator: owner, "im still seeing two lines. i only want the black line against the skin"). So the turn is put on the beard's
    # side as a slow one and on the face's side as a sudden one: over RIM_BEVEL the skin is SAID to lean further and further out toward its
    # rim (RIM_LEAN there), too slowly to be inked at any zoom, the skirt carries on from that to flat out, and the face beside it faces
    # straight out: one step, against the skin, however many pixels the skirt is.
    smooth_normal = [Vector((0.0, 0.0, 0.0)) for _ in mesh.vertices]
    for p in mesh.polygons[:skin_faces]:
        for v in p.vertices:
            smooth_normal[v] += p.normal * p.area
    leaned = []
    for v, point in enumerate(out_points):
        normal = smooth_normal[v].normalized() if smooth_normal[v].length > 0.0 else Vector(verts[v]).normalized()
        lean = RIM_LEAN * (1.0 - smooth(-shape(point.x, point.y) / RIM_BEVEL))
        if lean > 0.0:
            if v in rim_out:
                out = rim_out[v]
            else:
                gx = shape(point.x + 0.2, point.y) - shape(point.x - 0.2, point.y)
                gy = shape(point.x, point.y + 0.2) - shape(point.x, point.y - 0.2)
                length = math.hypot(gx, gy) or 1e-9
                here = Vector(on_ball(point.x, point.y, 0.0))
                out = Vector(on_ball(point.x + gx / length * 0.3, point.y + gy / length * 0.3, 0.0)) - here
                out = (out - here.normalized() * out.dot(here.normalized())).normalized()
            normal = (normal * math.cos(lean) + out * math.sin(lean)).normalized()
        leaned.append(normal)
    said = []
    for p in mesh.polygons:
        for v in p.vertices:
            said.append(wall[v] if v in wall else leaned[v])
    mesh.normals_split_custom_set(said)
    return ob


def build(style):
    """The beard: a full one's smooth skin, or the base (thinned) and the locks over it, as one object."""
    if style in SHELLS:
        return build_shell(style)
    base = build_base(style) if STYLES[style][0] is not None else None
    verts, faces = [], []
    for path, width, lift in LOCKS[style]():
        lock(verts, faces, path, width, lift)
    mesh = bpy.data.meshes.new(style + "Locks")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    ob = bpy.data.objects.new(style, mesh)
    bpy.context.scene.collection.objects.link(ob)
    if base is not None:
        bpy.ops.object.select_all(action="DESELECT")
        base.select_set(True)
        ob.select_set(True)
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.join()
    # Smooth across each lock's sides, a crease down its ridge and along where it meets the next: the cel shading lights one side of a lock and
    # shades the other, as a drawn lock of hair is.
    for p in ob.data.polygons:
        p.use_smooth = True
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40.0))
    return ob


def build_base(style):
    """The base under the locks: a grid over the face map, each point lifted off the ball by the base's thickness there; cells wholly outside left out."""
    lons = [i * STEP_DEGREES for i in range(int(-100 / STEP_DEGREES), int(100 / STEP_DEGREES) + 1)]
    lats = [j * STEP_DEGREES for j in range(int(LOWEST_LAT / STEP_DEGREES) - 4, int(20 / STEP_DEGREES) + 1)]
    index, verts = {}, []
    lifted = {}
    for j, lat in enumerate(lats):
        for i, lon in enumerate(lons):
            x = lon * math.cos(math.radians(lat))
            t = thickness(style, x, lat)
            lifted[(i, j)] = t
    faces = []
    for j in range(len(lats) - 1):
        for i in range(len(lons) - 1):
            corners = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]
            if all(lifted[c] is None for c in corners) or any(lifted[c] is None for c in corners):
                continue
            if all(lifted[c] <= 0.0 for c in corners):
                continue
            ids = []
            for c in corners:
                if c not in index:
                    lon, lat = math.radians(lons[c[0]]), math.radians(lats[c[1]])
                    r = HEAD_RADIUS + lifted[c]
                    # Unity: x right, y up, z forward; longitude positive toward the character's left (-x) seen from the front... symmetric anyway.
                    ux, uy, uz = r * math.cos(lat) * math.sin(lon), r * math.sin(lat), r * math.cos(lat) * math.cos(lon)
                    index[c] = len(verts)
                    verts.append((-ux, -uz, uy))
                ids.append(index[c])
            faces.append((ids[0], ids[3], ids[2], ids[1]))
    mesh = bpy.data.meshes.new(style)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    for p in mesh.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new(style, mesh)
    bpy.context.scene.collection.objects.link(ob)
    if len(mesh.polygons) * 2 > MOST_TRIANGLES:
        thin = ob.modifiers.new("Thin", "DECIMATE")
        thin.ratio = MOST_TRIANGLES / (len(mesh.polygons) * 2.0)
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.modifier_apply(modifier=thin.name)
        for p in mesh.polygons:
            p.use_smooth = True
    return ob


bpy.ops.wm.read_factory_settings(use_empty=True)
beards = {}
only = args[args.index("--only") + 1] if "--only" in args else None
for style in ORDER:
    if only is not None and style != only:
        continue
    beards[style] = build(style)
    print("BEARD %-13s %5d triangles, at most %.1f cm off the head" % (
        style, sum(len(p.vertices) - 2 for p in beards[style].data.polygons),
        100.0 * (max(v.co.length for v in beards[style].data.vertices) - HEAD_RADIUS)))

if "--export" in args:
    out_dir = args[args.index("--export") + 1]
    os.makedirs(out_dir, exist_ok=True)
    for style, ob in beards.items():
        bpy.ops.object.select_all(action="DESELECT")
        ob.select_set(True)
        bpy.context.view_layer.objects.active = ob
        path = os.path.join(out_dir, "Beard_" + style + ".glb")
        bpy.ops.export_scene.gltf(filepath=path, use_selection=True, export_format="GLB", export_normals=True, export_materials="NONE")
        print("WROTE", path)

if "--render" in args:
    out_dir = args[args.index("--render") + 1]
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    try:
        scene.render.engine = "BLENDER_WORKBENCH"
    except TypeError as e:
        print("engine", e)
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_cavity = True
    scene.display.shading.show_object_outline = True
    scene.render.resolution_x, scene.render.resolution_y = 480, 480
    scene.render.image_settings.file_format = "PNG"
    scene.world = bpy.data.worlds.new("World")
    scene.world.color = (0.55, 0.58, 0.62)

    def paint(name, colour):
        m = bpy.data.materials.new(name)
        m.diffuse_color = colour
        return m

    skin, brown, dark = paint("Skin", (0.85, 0.62, 0.45, 1)), paint("Beard", (0.25, 0.14, 0.07, 1)), paint("Face", (0.08, 0.05, 0.04, 1))
    bpy.ops.mesh.primitive_uv_sphere_add(radius=HEAD_RADIUS, segments=64, ring_count=32)
    head = bpy.context.active_object
    head.data.materials.append(skin)
    for p in head.data.polygons:
        p.use_smooth = True
    # A face where a face usually goes: two eyes and a mouth, drawn as dark discs a hair off the ball.
    def disc(lon, lat, rx, ry):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=1.0, segments=16, ring_count=8)
        d = bpy.context.active_object
        lon, lat = math.radians(lon), math.radians(lat)
        r = HEAD_RADIUS - 0.002
        d.location = (-r * math.cos(lat) * math.sin(lon), -r * math.cos(lat) * math.cos(lon), r * math.sin(lat))
        d.scale = (rx, 0.006, ry)
        d.rotation_euler = (math.atan2(r * math.sin(lat), r * math.cos(lat)) * 0.0, 0.0, -lon)
        d.data.materials.append(dark)
    disc(14.0, 6.0, 0.026, 0.04)
    disc(-14.0, 6.0, 0.026, 0.04)
    disc(0.0, MOUTH_LAT, 0.075, 0.013)
    for ob in beards.values():
        ob.data.materials.append(brown)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.angle = math.radians(32.0)
    views = {"front": (0.0, 5.0), "three_quarter": (40.0, 6.0), "side": (90.0, 2.0), "below": (25.0, -25.0), "above": (20.0, 22.0), "under": (0.0, -55.0)}
    for style, ob in beards.items():
        for other in beards.values():
            other.hide_render = other is not ob
        for view, (yaw, pitch) in views.items():
            yaw_r, pitch_r = math.radians(yaw), math.radians(pitch)
            d = 1.6
            cam.location = Vector((-d * math.sin(yaw_r) * math.cos(pitch_r), -d * math.cos(yaw_r) * math.cos(pitch_r), d * math.sin(pitch_r)))
            cam.rotation_euler = (Vector((0, 0, -0.05)) - cam.location).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(out_dir, "%s_%s.png" % (style, view))
            bpy.ops.render.render(write_still=True)
    print("rendered to", out_dir)
