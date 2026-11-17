# THE IDK HAIRSTYLE: ITS BACK SPIKES REDONE AND ITS EDGES ROLLED INTO THE HEAD (owner, 2026-11-16).
#
# The back spikes: "the back looks pretty rough too i did a bad trim, can you make the spikes in the middle look like more like the ones on the sides? you could
# also just copy them. just make it blend smoothly", then the good ones circled: the group on either side and the thin pair just off the middle. Between them the
# hem only dipped 1 to 3 cm over 10 cm (blunt bumps), where the good spikes drop 5 to 7 cm from notch to tip every 3.5 to 4 cm. Those two stretches get more
# vertices and their lowest band is drawn down into the good spikes' pattern: notches where the good ones' are, thin tips as deep as theirs, faded into the hair
# above and into the good spikes at either end.
#
# The edges rolled in: "give this hairstyle the same treatment you gave the kick hairstyle where it goes inward in the spots that have visibility underneath".
# The hair's one open edge runs round its underside: by the jaw it ran under the lowest side locks, which hang off the head there, and at the nape it stood 3 to
# 5 cm off. Looking in under it the eye met the far side of the shell from inside, which is not drawn, and saw straight through. Rolled into the head as the kick
# hair's was (SourceArt/Hair/hair_roll_in.py).
#
# The head is where the owner's fit puts it (position 0, -2.26, 0.01, scale 0.99): in the model, a sphere round (0, 2.2828, -0.0101), radius 0.35 / 0.99.
# Blender's axes: +Y is the back of the head, +X the side the owner's picture of the back shows on the left.
# Run it on IdkNeedsWork-original.glb (the model as it came), never on its own output: then CityX.UIEditor.HairAssets.RebakeIdkNeedsWork.
#   blender --background --factory-startup --python idk_roll_in.py -- IdkNeedsWork-original.glb ../../Assets/CityX/Character/Hair/IdkNeedsWork.glb
import os
import sys

import bmesh
from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Hair"))
import hair_roll_in  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:]
source, out = args[0], args[1]

FIT_POSITION = (0.0, -2.26, 0.01)   # HairFits.asset, idkneedswork (glTF's axes, which are the head bone's)
FIT_SCALE = 0.99
HEAD_RADIUS = 0.35                  # FusedBodyShape.HeadRadius
# The head bone's origin in the model, in Blender's axes (glTF's x, -z, y).
HEAD = Vector((-FIT_POSITION[0] / FIT_SCALE, FIT_POSITION[2] / FIT_SCALE, -FIT_POSITION[1] / FIT_SCALE))

# THE BACK SPIKES, heights below the head's centre, across the back (x) in metres.
BACK_FROM = 0.06         # a point this far behind the head's centre (y) is on the back
BAND_TOP = -0.20         # the hair above this is left as it is; the band below it is drawn down into the spikes
# Each stretch: its notches across the back (the ends are the good spikes' own notches, where it meets them) and the depth of the tip after each one. Bigger
# than the good spikes on the sides (owner, of a first try at their size, 4 cm apart: "that doesnt read as hair spikes ... make them bigger spikes in the back"):
# 5 to 6 cm wide and 9 to 10 cm long, the tips 34 to 35 cm under the head's centre, notches 24.5.
STRETCHES = [
    {"notches": [-0.205, -0.142, -0.078, -0.015], "notch_depth": -0.245, "tips": [-0.345, -0.352, -0.340]},
    {"notches": [0.058, 0.105, 0.152, 0.200], "notch_depth": -0.245, "tips": [-0.338, -0.348, -0.342]},
]
# Each side of a spike curves in to a thin point, like a lock of hair: halfway out from the notch it has come down only this share of the way to the tip.
SIDE_BEND_AT = 0.5
SIDE_BEND_DOWN = 0.3


def spike_points(stretch, hem_at=None):
    """
    The new hem's corners, (x, height) left to right: each notch, a bend halfway down each side, and the tip midway to the next notch. The end notches stay where
    the hem is now (<paramref name="hem_at"/>(x); without it only the corners' x are wanted).
    """
    notches = stretch["notches"]
    last = len(notches) - 1
    heights = [hem_at(x) if hem_at is not None and k in (0, last) else stretch["notch_depth"] for k, x in enumerate(notches)]
    points = []
    for k, x in enumerate(notches):
        points.append((x, heights[k]))
        if k < last:
            tip_x = (x + notches[k + 1]) * 0.5
            tip = stretch["tips"][k]
            points.append((x + (tip_x - x) * SIDE_BEND_AT, heights[k] + (tip - heights[k]) * SIDE_BEND_DOWN))
            points.append((tip_x, tip))
            points.append((notches[k + 1] + (tip_x - notches[k + 1]) * SIDE_BEND_AT, heights[k + 1] + (tip - heights[k + 1]) * SIDE_BEND_DOWN))
    return points


def on_the_back(p):
    return p.y - HEAD.y > BACK_FROM and p.z - HEAD.z < BAND_TOP


def hem_under(rim, x):
    """The hem's height at x: the open edge along the back's bottom (the lowest, should it fold), or None outside it."""
    best = None
    for a, b in rim:
        if min(a.x, b.x) <= x <= max(a.x, b.x) and abs(b.x - a.x) > 1e-7:
            t = (x - a.x) / (b.x - a.x)
            z = a.z + (b.z - a.z) * t - HEAD.z
            best = z if best is None else min(best, z)
    return best


def redo_back_spikes(hair):
    mesh = bmesh.new()
    mesh.from_mesh(hair.data)

    # A clean cut down the band at every new notch and tip, so the hem has a corner exactly there.
    cuts = 0
    for stretch in STRETCHES:
        for x, _ in spike_points(stretch):
            band = [f for f in mesh.faces if any(on_the_back(v.co) for v in f.verts)]
            geom = list({e for f in band for e in f.edges}) + band + list({v for f in band for v in f.verts})
            bmesh.ops.bisect_plane(mesh, geom=geom, plane_co=(x, 0.0, 0.0), plane_no=(1.0, 0.0, 0.0), dist=1e-5)
            cuts += 1
    bmesh.ops.triangulate(mesh, faces=[f for f in mesh.faces if len(f.verts) > 3], quad_method="BEAUTY", ngon_method="BEAUTY")

    # The hem is the back's open edge; the band above each point of it is stretched (or squeezed) down to the new outline, straight between its corners.
    rim = [(e.verts[0].co.copy(), e.verts[1].co.copy()) for e in mesh.edges
           if e.is_boundary and on_the_back(e.verts[0].co) and on_the_back(e.verts[1].co)]
    outlines = [spike_points(stretch, lambda x: hem_under(rim, x)) for stretch in STRETCHES]
    moved = 0
    for vert in mesh.verts:
        p = vert.co
        if not on_the_back(p):
            continue
        for corners in outlines:
            if not (corners[0][0] < p.x < corners[-1][0]):
                continue
            old = hem_under(rim, p.x)
            if old is None:
                continue
            for (x0, z0), (x1, z1) in zip(corners, corners[1:]):
                if x0 <= p.x <= x1:
                    new = z0 + (z1 - z0) * (p.x - x0) / (x1 - x0)
                    break
            share = min(1.0, (p.z - HEAD.z - BAND_TOP) / (old - BAND_TOP))
            p.z = HEAD.z + BAND_TOP + share * (new - BAND_TOP)
            moved += 1
    print("back spikes: %d cuts, %d vertices moved" % (cuts, moved))
    mesh.to_mesh(hair.data)
    mesh.free()
    hair.data.update()


def smoothstep(edge0, edge1, x):
    t = min(1.0, max(0.0, (x - edge0) / (edge1 - edge0)))
    return t * t * (3.0 - 2.0 * t)


# THE CURL UNDER THE BACK GOES STRAIGHT IN, not toward the head's centre. Toward the centre it also slid sideways toward the middle, by up to a quarter of the
# way across: from behind it showed through the gaps between the spikes and filled them with hair, so they read as stubs. Kept at its place across, it rises
# into the head behind the hair just above it. Out to the sides and round to the front it turns back to the centre (the side locks' curl, as it was).
STRAIGHT_IN_TO = 0.18    # across the back, out to this far either side of the middle
CENTRE_FROM = 0.28       # and toward the centre from this far out
CURL_DEPTH = (HEAD_RADIUS - 0.05) / FIT_SCALE
CURL_OUT = 0.05          # how far a curl carries on past its edge before it turns in: hardly at all, so it does not widen the spikes


def curl_end(point):
    from_head = point - HEAD
    centre = HEAD + from_head.normalized() * CURL_DEPTH
    if abs(point.x - HEAD.x) >= CURL_DEPTH:
        return centre
    across = Vector((0.0, from_head.y, from_head.z))
    straight = Vector((point.x, HEAD.y, HEAD.z)) + across.normalized() * (CURL_DEPTH ** 2 - (point.x - HEAD.x) ** 2) ** 0.5
    back = smoothstep(BACK_FROM - 0.04, BACK_FROM + 0.04, from_head.y)
    toward_centre = max(smoothstep(STRAIGHT_IN_TO, CENTRE_FROM, abs(point.x - HEAD.x)), 1.0 - back)
    mixed = straight.lerp(centre, toward_centre)
    return HEAD + (mixed - HEAD).normalized() * CURL_DEPTH


# AND IT STARTS AT THE NOTCHES, not at the spikes' tips. Hung from the spike's edges, it filled the gaps between them with hair seen from below and behind (owner,
# of that view: "that looks like shit ... that doesnt read as hair spikes"). From each edge of a back spike it goes straight up the inside of the spike, a
# little behind its face, to the line through the notches, and from there into the head: every spike has hair inside it, the underside is closed at the
# notches, and the gaps between the spikes stay open.
SPIKE_LINING = 0.012     # how far inside the spike's face its lining runs up
NOTCH_REACH = 0.032      # the notches either side of a point are found within this much across
UNDER_SPIKES_TO = 0.30   # across the back, out to this far either side of the middle (the spikes the owner circled reach about 0.30)
UNDER_SPIKES_FADE = 0.04


def notch_line(hair):
    """The line through the back hem's notches: at each x, the highest point of the hem within NOTCH_REACH either side."""
    mesh = bmesh.new()
    mesh.from_mesh(hair.data)
    hem = [v.co.copy() for e in mesh.edges if e.is_boundary for v in e.verts if v.co.y - HEAD.y > BACK_FROM]
    mesh.free()

    def at(x):
        near = [p.z for p in hem if abs(p.x - x) <= NOTCH_REACH]
        return max(near) if near else None
    return at


def curl_under_the_spikes(notches):
    def curl_at(point, usual):
        from_head = point - HEAD
        weight = smoothstep(BACK_FROM - 0.04, BACK_FROM + 0.04, from_head.y) * (1.0 - smoothstep(UNDER_SPIKES_TO, UNDER_SPIKES_TO + UNDER_SPIKES_FADE, abs(from_head.x)))
        notch = notches(point.x)
        if weight <= 0.0 or notch is None:
            return usual
        inward = Vector((0.0, -from_head.y, 0.0)).normalized() * SPIKE_LINING
        up_the_inside = Vector((point.x, point.y, max(point.z, notch + 0.005))) + inward
        return usual.lerp(up_the_inside, weight)
    return curl_at


hair = hair_roll_in.load(source)
redo_back_spikes(hair)
hair_roll_in.roll_in(hair, HEAD, HEAD_RADIUS / FIT_SCALE, curl_out=CURL_OUT, ends=curl_end, curl_at=curl_under_the_spikes(notch_line(hair)))
hair_roll_in.export(out)
