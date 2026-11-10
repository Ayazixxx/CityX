# OCTASLASH, posed: Sephiroth's held up special as a keyframed animation on CityX's own skeleton (CityXRig, built by octaslash_rig.py).
#
# Run inside Blender (Scripting tab, or the MCP bridge):   exec(open(r"<this file>").read())
#
# THE MOVE, from the two reference GIFs (SmashWiki's hitbox view, frame by frame, and a match clip):
#   coil_in  frames   0-22   from the sword stance the body sinks into the coil, the sword hand drawn back to the hip, the blade kept aimed ahead
#   hold     frames  22-70   the coil, held (the game loops this while the 3 s wind-up runs): low lunge, blade at the hip aimed where the dash goes
#   dash     frames  70-86   16 frames facing along the dash in a flying lunge (left knee driven forward, right leg stretched back), the sword
#                            whipping from side to side in front: a cut every two frames with the blade straight ahead (Smash: frames 36, 38 ...
#                            50), the torso twisting open and closed between them; then the blade up overhead and the last cut down the middle
#   recover  frames  86-116  the last cut carries on down into the follow-through: upright, the blade trailing low behind, rising, then settling
#                            back toward the sword stance
#
# Everything is authored in the character's own frame (Unity axes: x right, y up, z forward; the move dashes along +z). The game turns the body to
# the real dash direction itself. Bone names and the rest pose are BodyRig's; see octaslash_export.py for the trip back to Unity.

import bpy
import math
from mathutils import Vector, Quaternion, Matrix

RIG = "CityXRig"
SWORD = "Masamune"
FPS = 60

COIL_IN = (0, 22)
HOLD = (22, 70)
DASH = (70, 86)
RECOVER = (86, 116)
CUT_FRAMES = [DASH[0] + k for k in (2, 4, 6, 8, 10, 12, 14, 16)]

DASH_METRES = 14.0   # only for the preview: the game moves the body itself


def U(x, y, z):
    """Unity (x right, y up, z forward) -> Blender (the character faces -Y, its left is +X, up is +Z)."""
    return Vector((-x, -z, y))


FWD, UP, RIGHT = U(0, 0, 1), U(0, 1, 0), U(1, 0, 0)
BACK, DOWN, LEFT = -FWD, -UP, -RIGHT


def pitch(deg):
    """Tips the top forward."""
    return Quaternion(LEFT, math.radians(deg))


def roll(deg):
    """Rolls the right side down (clockwise seen from behind)."""
    return Quaternion(FWD, math.radians(deg))


def yaw(deg):
    """Turns to the left."""
    return Quaternion(UP, math.radians(deg))


def rot(p=0.0, y=0.0, r=0.0):
    return yaw(y) @ roll(r) @ pitch(p)


def unit(v):
    v = Vector(v)
    return v.normalized() if v.length > 1e-9 else v


def frame_of(d, r):
    d = unit(d)
    r = Vector(r) - d * Vector(r).dot(d)
    if r.length < 1e-6:
        r = UP - d * UP.dot(d)
        if r.length < 1e-6:
            r = FWD - d * FWD.dot(d)
    r.normalize()
    return Matrix((d, r, d.cross(r))).transposed()


def delta(d0, r0, d1, r1):
    """The rotation taking direction d0 (with r0 as its 'up') onto d1 (with r1)."""
    return (frame_of(d1, r1) @ frame_of(d0, r0).inverted()).to_quaternion()


def two_bone(root, target, l1, l2, pole):
    v = target - root
    d = max(abs(l1 - l2) + 1e-4, min(l1 + l2 - 1e-4, v.length))
    along = unit(v)
    a = (l1 * l1 - l2 * l2 + d * d) / (2.0 * d)
    h = math.sqrt(max(0.0, l1 * l1 - a * a))
    side = Vector(pole) - along * Vector(pole).dot(along)
    side = unit(side) if side.length > 1e-6 else unit(UP - along * UP.dot(along))
    return root + along * a + side * h, root + along * d


# ---------------------------------------------------------------------------------------------------------------------------------------------
# The rest: bone heads and orientations straight from the armature (arms hanging, as the game's body is bound), and where the sword sits on the hand.

rig = bpy.data.objects[RIG]
sword = bpy.data.objects[SWORD]
BONES = {b.name: b for b in rig.data.bones}
ORDER = [b.name for b in rig.data.bones]          # created parents-first
PARENT = {n: (b.parent.name if b.parent else None) for n, b in BONES.items()}
HEAD = {n: b.head_local.copy() for n, b in BONES.items()}
TAIL = {n: b.tail_local.copy() for n, b in BONES.items()}
REST = {n: b.matrix_local.copy() for n, b in BONES.items()}

saved_location = rig.location.copy()
rig.location = (0, 0, 0)
for pb in rig.pose.bones:
    pb.rotation_mode = 'QUATERNION'
    pb.matrix_basis = Matrix.Identity(4)
bpy.context.view_layer.update()
_sw = sword.matrix_world.copy()
BLADE_REST = unit(_sw.to_quaternion() @ Vector((0, 0, 1)))
EDGE_REST = unit(_sw.to_quaternion() @ Vector((1, 0, 0)))
BLADE_LENGTH = 2.1


def child_head(D, pos, child, parent):
    return pos[parent] + D[parent] @ (HEAD[child] - HEAD[parent])


# ---------------------------------------------------------------------------------------------------------------------------------------------
# A POSE is a dict:
#   hips   (x, y, z)          where the hips are, Unity metres (rest (0, 0.9, 0))
#   body   Quaternion         the hips' turn from upright (rot/pitch/roll/yaw above, composed)
#   spine, neck, head         each one's turn on top of its parent's (rot(...))
#   sword  {hand: where the right wrist goes, from the right shoulder; blade, edge: which way the blade points and its cutting edge faces;
#           space: "chest" (the default: everything turns with the chest) or "root" (the character's own upright frame)}
#          the hand is turned until the blade lies along `blade`; the elbow sits where it does easily (SWORD_ELBOW, or `elbow` in the chest's
#          frame) unless the blade needs it elsewhere.
#   free   {hand: where the left wrist goes, from the left shoulder; pole: which way the elbow points; space as above, or "world": the
#           hand is a place in the character's own frame, not an offset from the shoulder}
#   right  the right arm as an empty hand, written as `free` is, in place of `sword` (a punch: ../FistCombo/fist_pose.py)
#   r_leg / l_leg  {foot: the ankle from the hip joint; knee: which way the knee points; toe: which way the foot points; space: "root" or "hips"}

# Where the sword arm's elbow sits easily, in the chest's frame: down, a little out to the right and back.
SWORD_ELBOW = (0.45, -1.0, -0.25)

# The elbow's place is a trade: a degree of wrist bend against an elbow turned from where it sits easily (this much for turning it right round).
ELBOW_WEIGHT = 80.0

# And the blade's roll: a degree of wrist bend against this many degrees of roll away from the last pose's (see `edge_near`).
EDGE_KEEP = 0.3

# And a degree of the sword hand's twist about its own length, against the forearm's turn: a hand turned far round its forearm wrings the
# arm's tube into a ribbon at the wrist. Off unless a move asks (SOLVER_TWIST_WEIGHT; the stab dash's thrust grip needs it), so Octaslash
# and the spin solve as they were posed.
TWIST_WEIGHT = globals().get("SOLVER_TWIST_WEIGHT", 0.0)

# The grip holds the blade this far off the hand's own line (SwordWeapon's default hold), so a straight wrist leaves the blade at this angle to
# the forearm.
HAND_TO_BLADE = BLADE_REST.angle(unit(TAIL["hand_r"] - HEAD["hand_r"]))


def solve(P):
    D, pos = {}, {}
    D["hips"] = P["body"]
    pos["hips"] = U(*P["hips"])
    for child, parent in (("spine", "hips"), ("neck", "spine"), ("head", "neck")):
        D[child] = D[parent] @ P.get(child, Quaternion())
        pos[child] = child_head(D, pos, child, parent)
    chest = D["spine"]

    for side in ("l", "r"):
        a, f, h, t = "arm_" + side, "forearm_" + side, "hand_" + side, "fingertip_" + side
        shoulder = child_head(D, pos, a, "spine")
        l1 = (HEAD[f] - HEAD[a]).length
        l2 = (HEAD[h] - HEAD[f]).length
        u_rest = unit(HEAD[f] - HEAD[a])
        empty = side == "l" or "right" in P
        spec = P["free"] if side == "l" else P["right"] if empty else P["sword"]
        space = chest if spec.get("space", "chest") == "chest" else Quaternion()
        target = U(*spec["hand"]) if spec.get("space") == "world" else shoulder + space @ U(*spec["hand"])
        # The elbow can go anywhere on a circle about the shoulder-wrist line; `out` is the way it sticks out from that line.
        reach_dir = unit(target - shoulder)
        d = max(abs(l1 - l2) + 1e-4, min(l1 + l2 - 1e-4, (target - shoulder).length))
        along_a = (l1 * l1 - l2 * l2 + d * d) / (2.0 * d)
        radius = math.sqrt(max(0.0, l1 * l1 - along_a * along_a))
        centre = shoulder + reach_dir * along_a
        wrist = shoulder + reach_dir * d
        if not empty:
            # THE SWORD ARM: the elbow where it sits easily (down, a little out and back) unless the blade needs it elsewhere, and the blade turned
            # about its own length to whichever way lets the wrist stay straightest. A wrist bent far shows on this body as a kink in the arm's tube,
            # an elbow pointing into the chest or up looks broken, and which way a thin blade's edge faces does not show at all in a fast cut.
            blade = unit(space @ U(*spec["blade"]))
            natural = unit(chest @ U(*spec.get("elbow", SWORD_ELBOW)))
            ring_a = unit(reach_dir.orthogonal())
            ring_b = reach_dir.cross(ring_a)
            best = None
            for i in range(48):
                angle = 2.0 * math.pi * i / 48.0
                out = ring_a * math.cos(angle) + ring_b * math.sin(angle)
                forearm = unit(wrist - (centre + out * radius))
                cost = abs(math.degrees(forearm.angle(blade) - HAND_TO_BLADE)) + ELBOW_WEIGHT * (1.0 - out.dot(natural))
                if best is None or cost < best[0]:
                    best = (cost, out)
            out = best[1]
            elbow = centre + out * radius
            forearm = unit(wrist - elbow)
            if spec.get("edge") is not None:
                edges = [unit(space @ U(*spec["edge"]))]
            else:
                across = unit(blade.orthogonal())
                edges = [Quaternion(blade, math.radians(5.0 * i)) @ across for i in range(72)]
            # `edge_near`: the last pose's edge. Turning the blade far about its own length between two poses spins the sword in the hand for
            # nothing, so a roll close to it is preferred.
            near = unit(space @ U(*spec["edge_near"])) if spec.get("edge_near") is not None else None
            # The forearm's own turn, as it is set below, for the hand's twist against it.
            upper_now = unit(elbow - shoulder)
            forearm_turn = upper_now.rotation_difference(forearm) @ delta(u_rest, FWD, upper_now, -(out - upper_now * out.dot(upper_now)))
            hand_axis = unit(TAIL[h] - HEAD[h])
            best = None
            for edge in edges:
                turn = delta(BLADE_REST, EDGE_REST, blade, edge)
                bent = forearm.angle(unit(turn @ (TAIL[h] - HEAD[h])))
                cost = math.degrees(bent) + (EDGE_KEEP * math.degrees(edge.angle(near)) if near is not None else 0.0)
                if TWIST_WEIGHT > 0.0:
                    r = forearm_turn.inverted() @ turn
                    twist = 2.0 * math.degrees(math.atan2(Vector((r.x, r.y, r.z)).dot(hand_axis), r.w))
                    cost += TWIST_WEIGHT * abs((twist + 180.0) % 360.0 - 180.0)
                if best is None or cost < best[0]:
                    best = (cost, bent, turn)
            _, bent, Dh = best
            P["_wrist_bend"] = math.degrees(bent)
            e = space.inverted() @ (Dh @ EDGE_REST)
            P["_edge"] = (-e.x, e.z, -e.y)                     # the edge chosen, back in the spec's own Unity axes
        else:
            Dh = None
            pole = space @ U(*spec.get("pole", (-0.3 if side == "l" else 0.3, -1.0, -0.6)))
            out = pole - reach_dir * pole.dot(reach_dir)
            out = unit(out) if out.length > 1e-6 else unit(UP - reach_dir * UP.dot(reach_dir))
            elbow = centre + out * radius
        upper = unit(elbow - shoulder)
        forearm = unit(wrist - elbow)
        # The upper arm's own turn follows where the elbow sticks out (the forearm swings the other way: FWD at rest, the elbow pointing back), so
        # it never flips about its length even with the arm almost straight.
        bend = -(out - upper * out.dot(upper))
        Du = delta(u_rest, FWD, upper, bend)
        Df = upper.rotation_difference(forearm) @ Du
        if Dh is None:
            Dh = Df
        D[a], D[f], D[h], D[t] = Du, Df, Dh, Dh
        pos[a], pos[f], pos[h] = shoulder, elbow, wrist
        pos[t] = wrist + Dh @ (HEAD[t] - HEAD[h])

    for side in ("l", "r"):
        th, sn, ft = "thigh_" + side, "shin_" + side, "foot_" + side
        spec = P[side + "_leg"]
        hip = child_head(D, pos, th, "hips")
        l1 = (HEAD[sn] - HEAD[th]).length
        l2 = (HEAD[ft] - HEAD[sn]).length
        frame = D["hips"] if spec.get("space", "root") == "hips" else Quaternion()
        knee, ankle = two_bone(hip, hip + frame @ U(*spec["foot"]), l1, l2, frame @ U(*spec["knee"]))
        thigh = unit(knee - hip)
        shin = unit(ankle - knee)
        bend = shin - thigh * shin.dot(thigh)                  # the shin swings back from the thigh: BACK at rest
        if bend.length < 1e-3:
            k = frame @ U(*spec["knee"])
            bend = -(k - thigh * k.dot(thigh))
        Dt = delta(DOWN, BACK, thigh, bend)
        Ds = thigh.rotation_difference(shin) @ Dt
        Dft = delta(FWD, UP, unit(frame @ U(*spec["toe"])), -shin)
        D[th], D[sn], D[ft] = Dt, Ds, Dft
        pos[th], pos[sn], pos[ft] = hip, knee, ankle
    return D, pos


def locals_of(D):
    """Each bone's turn relative to its parent's (what gets interpolated)."""
    return {n: (D[n] if PARENT[n] is None else D[PARENT[n]].inverted() @ D[n]) for n in ORDER}


def world_of(L, hips):
    D, pos = {}, {}
    for n in ORDER:
        p = PARENT[n]
        if p is None:
            D[n] = L[n]
            pos[n] = hips
        else:
            D[n] = D[p] @ L[n]
            pos[n] = pos[p] + D[p] @ (HEAD[n] - HEAD[p])
    return D, pos


# ---------------------------------------------------------------------------------------------------------------------------------------------
# THE POSES (Unity axes: x right, y up, z forward)

def lunge_legs(drop=0.0, space="root"):
    """The coil's legs: right foot forward and flat, left leg back with its knee low, on the ball of the foot."""
    return {
        "r_leg": {"foot": (0.05, -0.45 + drop, 0.40), "knee": (0.15, 0.35, 1.0), "toe": (0.05, -0.05, 1.0), "space": space},
        "l_leg": {"foot": (-0.04, -0.40 + drop, -0.50), "knee": (-0.1, -1.0, 0.35), "toe": (0.0, -0.75, 0.65), "space": space},
    }


def standing_legs(space="root"):
    return {
        "r_leg": {"foot": (0.03, -0.72, 0.06), "knee": (0.1, 0.2, 1.0), "toe": (0.05, 0.0, 1.0), "space": space},
        "l_leg": {"foot": (-0.03, -0.71, -0.08), "knee": (-0.1, 0.2, 1.0), "toe": (-0.05, -0.1, 1.0), "space": space},
    }


def pose_coil():
    """The held coil: deep lunge, side-on, the sword hand drawn back to the right hip and the blade aimed forward and up where the dash will go,
    the free hand reaching out toward it."""
    P = {
        "hips": (0.0, 0.60, -0.12), "body": rot(p=22, y=-18),
        "spine": rot(p=10, y=-12), "neck": rot(p=-14, y=16), "head": rot(p=-18, y=12),
        "sword": {"hand": (0.12, -0.42, -0.28), "blade": (0.10, 0.32, 1.0), "space": "root"},
        "free": {"hand": (0.10, -0.15, 0.72), "pole": (-0.5, -1.0, -0.2), "space": "root"},
    }
    P.update(lunge_legs())
    return P


def hold_wobble(P, frame):
    """Alive while held: a slow breath and a tremble in the blade."""
    t = (frame - HOLD[0]) / (HOLD[1] - HOLD[0])
    breath = math.sin(t * 2.0 * math.pi)
    shake = math.sin((frame - HOLD[0]) * 2.0 * math.pi / 6.0) * 0.012
    Q = dict(P)
    hx, hy, hz = P["hips"]
    Q["hips"] = (hx, hy + 0.015 * breath, hz)
    Q["spine"] = P["spine"] @ rot(p=1.5 * breath)
    s = dict(P["sword"])
    if s.get("edge") is None:
        probe = dict(P)
        solve(probe)
        s["edge"] = probe["_edge"]      # the still pose's own roll, kept: a tremble must not flip the hand to another roll
    bx, by, bz = s["blade"]
    s["blade"] = (bx + shake, by + shake * 0.7, bz)
    Q["sword"] = s
    return Q


# THE DASH: facing along it the whole way, in a flying lunge (the left knee driven forward, the right leg stretched back behind), the sword
# whipping from one side to the other in front of the body: a cut every two frames, the blade passing straight ahead on each one. Between cuts the
# torso is twisted open (the sword drawn back on the right) or closed (swung across to the left), as in the hitbox GIF's frames 34-49.

DASH_LEAN = 44.0
DASH_HIPS = (0.0, 1.10, 0.0)


def lunge(twist=0.0, lean=DASH_LEAN, hips=DASH_HIPS):
    """The flying lunge: leaning into the dash, the left knee forward and up, the right leg trailing; the torso turned by `twist` (negative:
    open, the right shoulder back; positive: closed), the head held looking ahead."""
    return {
        "hips": hips, "body": rot(p=lean, y=twist * 0.3),
        "spine": rot(p=8, y=twist * 0.7), "neck": rot(p=-18, y=-twist * 0.5), "head": rot(p=-16, y=-twist * 0.5),
        "l_leg": {"foot": (-0.02, -0.30, 0.46), "knee": (0.0, 0.65, 1.0), "toe": (0.0, -0.75, 0.65), "space": "hips"},
        "r_leg": {"foot": (0.02, -0.66, -0.34), "knee": (0.0, -0.25, 1.0), "toe": (0.0, -0.6, -0.8), "space": "hips"},
    }


# The sword hand (from the right shoulder) and the blade, in the character's own frame, for the three moments of a cut.
def drawn_back(height):
    """Open: the sword drawn back out to the right, the blade pointing right and back, high or low."""
    return {"hand": (0.62, -0.10 + height * 0.25, -0.25), "blade": (0.90, height, -0.40), "space": "root"}


def straight_ahead(height):
    """Mid-cut: the arm out in front, the blade straight ahead along the dash."""
    return {"hand": (0.10, -0.15 + height * 0.2, 0.80), "blade": (0.0, height, 1.0), "space": "root"}


def swung_across(height):
    """Closed: the sword swung across the front to the left, the blade pointing left and back."""
    return {"hand": (-0.45, -0.05 + height * 0.25, 0.45), "blade": (-0.90, height, -0.30), "space": "root"}


FREE_FORWARD = {"hand": (0.15, -0.35, 0.55), "pole": (-0.6, -0.8, -0.4), "space": "root"}
FREE_OUT = {"hand": (-0.45, -0.45, 0.10), "pole": (-0.6, -0.8, -0.4), "space": "root"}
FREE_BACK = {"hand": (-0.50, -0.30, -0.35), "pole": (-0.5, -0.8, 0.3), "space": "root"}

# Dash frame k -> (twist, sword, free arm). Even frames 2..16 are the cuts (Smash's 36, 38 ... 50): the blade straight ahead, sweeping through.
# Rising right-low to left-high, falling back to the right, a low sweep left, rising right, falling left, level right, up into the wind-up,
# and the last one down the middle.
DASH_SCHEDULE = {
    1: (-30.0, drawn_back(-0.25), FREE_FORWARD),
    2: (0.0, straight_ahead(0.15), FREE_OUT),
    3: (30.0, swung_across(0.40), FREE_BACK),
    4: (0.0, straight_ahead(-0.10), FREE_OUT),
    5: (-30.0, drawn_back(-0.50), FREE_FORWARD),
    6: (0.0, straight_ahead(-0.25), FREE_OUT),
    7: (30.0, swung_across(-0.30), FREE_BACK),
    8: (0.0, straight_ahead(0.10), FREE_OUT),
    9: (-30.0, drawn_back(0.35), FREE_FORWARD),
    10: (0.0, straight_ahead(0.05), FREE_OUT),
    11: (30.0, swung_across(0.15), FREE_BACK),
    12: (0.0, straight_ahead(0.0), FREE_OUT),
    13: (-30.0, drawn_back(0.0), FREE_FORWARD),
    14: (0.0, straight_ahead(0.35), FREE_OUT),
}


def dash_frame(k):
    """The pose for dash frame k (0..16)."""
    if k <= 0:
        # Out of the coil: tipping into the lunge, the blade still aimed ahead from the hip.
        P = lunge(twist=-12.0, lean=24.0, hips=(0.0, 0.88, 0.05))
        P["sword"] = {"hand": (0.20, -0.45, 0.20), "blade": (0.10, 0.25, 1.0), "space": "root"}
        P["free"] = FREE_FORWARD
        return P
    if k == 15:
        # The wind-up for the last cut: the blade swept up overhead and back, the body rearing a little.
        P = lunge(twist=-10.0, lean=24.0, hips=(0.0, 1.14, 0.0))
        P["sword"] = {"hand": (0.15, 0.55, 0.0), "blade": (0.10, 0.85, -0.50), "space": "root"}
        P["free"] = FREE_OUT
        return P
    if k >= 16:
        # The last cut, half way down: the whole body thrown into it, the blade straight ahead and falling.
        P = lunge(twist=5.0, lean=46.0, hips=(0.0, 1.06, 0.05))
        P["sword"] = {"hand": (0.05, -0.05, 0.85), "blade": (0.0, -0.35, 1.0), "space": "root"}
        P["free"] = FREE_BACK
        return P
    twist, sword, free = DASH_SCHEDULE[k]
    P = lunge(twist=twist)
    P["sword"] = sword
    P["free"] = free
    return P


def pose_follow_through():
    """The last cut's end: upright again, turned side-on, the blade trailing low behind and out to the right (the GIF's frames 50-60)."""
    P = {
        "hips": (0.0, 0.98, 0.0), "body": rot(p=-6, y=-40),
        "spine": rot(p=-4, y=-20), "neck": rot(p=4, y=30), "head": rot(p=2, y=26),
        "sword": {"hand": (0.30, -0.30, -0.78), "blade": (0.30, -0.42, -0.86), "space": "root"},
        "free": {"hand": (-0.10, -0.25, 0.65), "pole": (-0.8, -0.6, -0.2), "space": "root"},
        "l_leg": {"foot": (-0.05, -0.42, 0.32), "knee": (-0.1, 0.6, 1.0), "toe": (-0.05, -0.4, 1.0), "space": "hips"},
        "r_leg": {"foot": (0.06, -0.72, -0.22), "knee": (0.0, 0.2, 1.0), "toe": (0.0, -0.8, 0.6), "space": "hips"},
    }
    return P


def pose_rise():
    """Recovery: the blade risen up behind (the GIF's frames 70-80)."""
    P = pose_follow_through()
    P["hips"] = (0.0, 0.94, 0.0)
    P["body"] = rot(p=-4, y=-35)
    P["spine"] = rot(p=-2, y=-18)
    P["sword"] = {"hand": (0.32, 0.05, -0.80), "blade": (0.35, 0.55, -0.76), "space": "root"}
    return P


def pose_settle():
    """Recovery's end: back toward the two-handed sword stance the game's idle holds, so handing back to it is short."""
    P = {
        "hips": (0.0, 0.88, 0.0), "body": rot(p=3, y=-5),
        "spine": rot(p=2, y=-4), "neck": rot(p=-2, y=4), "head": rot(p=-3, y=2),
        "sword": {"hand": (-0.12, -0.72, 0.32), "blade": (0.08, 0.50, 0.86), "space": "root"},
        "free": {"hand": (0.42, -0.72, 0.30), "pole": (-0.6, -0.6, -0.5), "space": "root"},
    }
    P.update(standing_legs())
    return P


# ---------------------------------------------------------------------------------------------------------------------------------------------
# THE TIMELINE: every frame's pose, as bone turns relative to their parents plus where the hips are.

def ease(kind, t):
    t = max(0.0, min(1.0, t))
    if kind == "in":
        return t * t
    if kind == "out":
        return 1.0 - (1.0 - t) * (1.0 - t)
    if kind == "inout":
        return t * t * (3.0 - 2.0 * t)
    return t


def solved(P, name=None):
    D, pos = solve(P)
    if name is not None:
        print("%-16s sword wrist bend %5.1f deg" % (name, P.get("_wrist_bend", 0.0)))
    return locals_of(D), pos["hips"]


def blend(a, b, t):
    La, ha = a
    Lb, hb = b
    L = {}
    for n in ORDER:
        qa, qb = La[n], Lb[n]
        if qa.dot(qb) < 0.0:
            qb = -qb
        L[n] = qa.slerp(qb, t)
    return L, ha.lerp(hb, t)


def keyed(keys, frame):
    """keys: [(frame, solved pose, ease into the next)]"""
    for (f0, p0, kind), (f1, p1, _) in zip(keys, keys[1:]):
        if f0 <= frame <= f1:
            return blend(p0, p1, ease(kind, (frame - f0) / float(f1 - f0)))
    return keys[-1][1]


def dash_pose(k, name=None):
    """Dash frame k (0..16): see DASH_SCHEDULE."""
    return solved(dash_frame(k), name)


# The last cut carries on down through the first frames of the recovery into the follow-through.
FINAL_CUT_FOLLOW_FRAMES = 3


def chained(P, previous, name=None):
    """Solves P with its blade's roll kept near the pose before's (`previous`, that pose's dict, or None); returns the solved pose and P."""
    sword = P["sword"]
    if previous is not None and sword.get("edge") is None and sword.get("space", "chest") == previous["sword"].get("space", "chest"):
        P["sword"] = dict(sword, edge_near=previous["_edge"])
    return solved(P, name), P


def timeline():
    stance, stance_p = chained(pose_settle(), None, "stance")
    coil, coil_p = chained(pose_coil(), stance_p, "coil")
    coil_in = [(0, stance, "inout"), (COIL_IN[1], coil, "linear")]
    held = dict(coil_p)
    held["sword"] = dict(coil_p["sword"], edge=coil_p["_edge"])

    frames = {}
    previous = coil_p
    for f in range(DASH[0], DASH[1] + 1):
        k = f - DASH[0]
        frames[f], previous = chained(dash_frame(k), previous, "dash frame %d" % k if k in (0, 1, 2, 3, 15, 16) else None)
    last_cut = frames[DASH[1]]
    follow, follow_p = chained(pose_follow_through(), previous, "follow-through")
    rise, rise_p = chained(pose_rise(), follow_p, "rise")
    settle, _ = chained(pose_settle(), rise_p, "settle")
    recover = [(RECOVER[0], last_cut, "out"), (RECOVER[0] + FINAL_CUT_FOLLOW_FRAMES, follow, "inout"), (RECOVER[0] + 8, follow, "inout"),
               (RECOVER[0] + 19, rise, "inout"), (RECOVER[1], settle, "linear")]

    for f in range(COIL_IN[0], COIL_IN[1] + 1):
        frames[f] = keyed(coil_in, f)
    for f in range(HOLD[0] + 1, HOLD[1]):          # the hold's last frame is the dash's first
        frames[f] = solved(hold_wobble(held, f))
    for f in range(RECOVER[0], RECOVER[1] + 1):
        frames[f] = keyed(recover, f)
    return frames


# ---------------------------------------------------------------------------------------------------------------------------------------------
# BAKE onto the armature: one key a frame, linear between.

def apply_pose(L, hips):
    D, pos = world_of(L, hips)
    X = {}
    for n in ORDER:
        Xn = Matrix.Translation(pos[n]) @ (D[n] @ REST[n].to_quaternion()).to_matrix().to_4x4()
        X[n] = Xn
        p = PARENT[n]
        if p is None:
            basis = REST[n].inverted() @ Xn
        else:
            basis = (X[p] @ REST[p].inverted() @ REST[n]).inverted() @ Xn
        pb = rig.pose.bones[n]
        pb.matrix_basis = basis
        if p is not None:
            pb.location = (0.0, 0.0, 0.0)
    return D, pos


def bake():
    frames = timeline()
    if rig.animation_data and rig.animation_data.action:
        old = rig.animation_data.action
        rig.animation_data.action = None
        bpy.data.actions.remove(old)
    rig.animation_data_create()
    action = bpy.data.actions.new("Octaslash")
    rig.animation_data.action = action
    previous = {}
    for f in sorted(frames):
        L, hips = frames[f]
        apply_pose(L, hips)
        for n in ORDER:
            pb = rig.pose.bones[n]
            q = pb.rotation_quaternion.copy()
            if n in previous and previous[n].dot(q) < 0.0:
                q = -q
            pb.rotation_quaternion = q
            previous[n] = q
            pb.keyframe_insert("rotation_quaternion", frame=f, group=n)
            if PARENT[n] is None:
                pb.keyframe_insert("location", frame=f, group=n)
    # The preview's flight: the rig itself carried along the dash (the game moves the body; the export ignores this).
    rig.location = (0, 0, 0)
    rig.keyframe_insert("location", frame=DASH[0])
    rig.location = U(0, 0, DASH_METRES)
    rig.keyframe_insert("location", frame=DASH[1])
    rig.location = U(0, 0, DASH_METRES + 1.2)
    rig.keyframe_insert("location", frame=RECOVER[0] + 12)
    for fc in action.fcurves:
        for kp in fc.keyframe_points:
            kp.interpolation = 'LINEAR'
    # markers name the segments for the exporter
    scene = bpy.context.scene
    scene.timeline_markers.clear()
    for name, (a, b) in (("coil_in", COIL_IN), ("hold", HOLD), ("dash", DASH), ("recover", RECOVER)):
        scene.timeline_markers.new(name, frame=a)
    for i, f in enumerate(CUT_FRAMES):
        scene.timeline_markers.new("cut%d" % (i + 1), frame=f)
    scene.timeline_markers.new("end", frame=RECOVER[1])
    scene.render.fps = FPS
    scene.frame_start = COIL_IN[0]
    scene.frame_end = RECOVER[1]
    scene.frame_set(COIL_IN[0])
    return len(frames)


if not globals().get("OCTASLASH_NO_BAKE"):   # set it before exec to load the poses without baking them
    count = bake()
    print("baked %d frames" % count)
