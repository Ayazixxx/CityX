# THE GET-UP FROM THE GROUND, as full-body clips for this body. Owner, 2026-11-10, of the get-up borrowed from a Mixamo body: "the limbs bend all
# awkwardly clearly in an unintented way", "its like the knees bend the wrong way", "i think it needs a better standing up animation". The same
# get-up ends the fall from the sky and every knockdown in play.
#
# Two ways down, one way up:
#   getup_back   lying on the back (the fall lands him so): the head comes up, the left knee bends and the foot plants, the right arm goes out
#                past the head, and the left foot and the reaching left arm roll him over his right side and up onto his hands and knees
#   getup_front  lying face down: the hands come in under the chest, push it up, and the hips go back onto the knees
#   both then    the right foot comes forward between the hands, the hands lift onto the right knee, the legs drive the body up over that foot,
#                the left foot steps up beside it, and the body settles standing as the game's idle stands
#
# This body is long in the arm (shoulder to wrist 0.89 m) and short in the leg (hip to ankle 0.77 m) under a 1 m torso, so on hands and knees its
# shoulders sit well above its hips; the keys are written for those lengths, every contact at the height its limb's skin touches the ground.
#
# Needs the rig (../Octaslash/octaslash_rig.py) and octaslash_pose.py's solver (with OCTASLASH_NO_BAKE set) loaded first; getup_build.py does both.
# Keys are in the character's own frame (Unity axes: x right, y up, z forward, metres, the ground at y 0); the body stands up facing +z.

import bpy
import math
from mathutils import Vector, Quaternion

FPS = 60

# The heights a limb's joint sits at with its skin on the ground.
HAND = 0.10          # the wrist, the palm flat
ANKLE = 0.135        # the ankle, the foot flat (BodyRig's FootY)
KNEE = 0.135         # the knee joint, kneeling
LYING = 0.27         # the hips, lying on the back or the front
TOES_DOWN = (0.0, -0.55, -0.83)   # a foot behind a kneeling leg, curled onto its toes


def key(t, hips, body, spine=(0, 0, 0), neck=(0, 0, 0), head=(0, 0, 0), lh=None, rh=None, lf=None, rf=None):
    """One moment. body, spine, neck, head: (pitch forward, roll right side down, yaw left), degrees, each on top of its parent's;
    lh, rh: (wrist, elbow pole, fingers or None); lf, rf: (ankle, knee pole, toes)."""
    return {"t": t, "hips": hips, "body": body, "spine": spine, "neck": neck, "head": head, "lh": lh, "rh": rh, "lf": lf, "rf": rf}


def then(k, t, **changes):
    """The key after `k`: the same but for `changes`."""
    n = dict(k)
    n.update(changes)
    n["t"] = t
    return n


def hand(at, pole, fingers=None):
    return (at, pole, fingers)


def foot(at, knee, toe):
    return (at, knee, toe)


# --- the way up, from hands and knees (both clips) ---

ALL_FOURS = key(0.0, (0.0, 0.53, -0.35), (68, 0, 0), spine=(6, 0, 0), neck=(-22, 0, 0), head=(-14, 0, 0),
                lh=hand((-0.42, HAND, 0.60), (-0.4, 0.0, -1.0), (0.0, 0.0, 1.0)),
                rh=hand((0.42, HAND, 0.60), (0.4, 0.0, -1.0), (0.0, 0.0, 1.0)),
                lf=foot((-0.20, 0.17, -0.71), (0.0, -1.0, 0.2), TOES_DOWN),
                rf=foot((0.20, 0.17, -0.71), (0.0, -1.0, 0.2), TOES_DOWN))


def the_way_up(t0):
    """From hands and knees at t0 to standing, the right foot first."""
    four = then(ALL_FOURS, t0)
    # The right foot comes through under the belly to plant between the hands and the knee, its knee out to the side past the ribs.
    lunge = then(four, t0 + 0.45, hips=(0.0, 0.55, -0.33), body=(58, -3, 4), spine=(8, 0, 0), neck=(-18, 0, 0),
                 rf=foot((0.24, ANKLE, 0.06), (0.6, 0.4, 1.0), (0.08, 0.0, 1.0)))
    # Hands off the ground and onto the right knee, the body upright on the left knee.
    kneel = then(lunge, t0 + 0.85, hips=(0.02, 0.53, -0.30), body=(18, -2, 6), spine=(6, 0, -2), neck=(-4, 0, 0), head=(-4, 0, 0),
                 rh=hand((0.26, 0.64, 0.12), (0.6, 0.2, -1.0), (0.0, -0.6, 1.0)),
                 lh=hand((0.05, 0.62, 0.08), (-0.6, 0.0, -1.0), (0.4, -0.6, 1.0)),
                 rf=foot((0.24, ANKLE, 0.06), (0.2, 0.5, 1.0), (0.08, 0.0, 1.0)))
    # The drive: the weight forward over the right foot, the right hand pressing on its knee, the left knee off the ground.
    drive = then(kneel, t0 + 1.25, hips=(0.03, 0.74, -0.06), body=(24, -2, 3), spine=(4, 0, 0), neck=(-8, 0, 0), head=(-6, 0, 0),
                 rh=hand((0.28, 0.76, 0.14), (0.6, 0.0, -1.0), (0.0, -0.5, 1.0)),
                 lh=hand((-0.30, 0.80, 0.10), (-0.5, -0.3, -1.0)),
                 lf=foot((-0.19, 0.26, -0.40), (0.0, -0.2, 1.0), (0.0, -0.6, 0.8)))
    stand = then(drive, t0 + 1.6, hips=(0.0, 0.86, 0.0), body=(4, 0, 0), spine=(1, 0, 0), neck=(0, 0, 0), head=(0, 0, 0),
                 rh=hand((0.52, 0.98, 0.07), (0.4, -0.2, -1.0)),
                 lh=hand((-0.52, 0.98, 0.07), (-0.4, -0.2, -1.0)),
                 lf=foot((-0.18, ANKLE, -0.02), (-0.1, 0.2, 1.0), (-0.05, 0.0, 1.0)),
                 rf=foot((0.22, ANKLE, 0.04), (0.1, 0.2, 1.0), (0.06, 0.0, 1.0)))
    settled = then(stand, t0 + 1.95, hips=(0.0, 0.85, 0.01), body=(2, 0, 0), spine=(0, 0, 0),
                   rh=hand((0.55, 0.94, 0.05), (0.4, -0.2, -1.0)),
                   lh=hand((-0.55, 0.94, 0.05), (-0.4, -0.2, -1.0)))
    return [four, lunge, kneel, drive, stand, settled]


# --- from the back ---
# Supine with the head toward +z is the front-down body rolled half a turn (body roll 180, its right side at -x). Rolling back to a roll of 0 takes
# it over its right side, toward -x, so it starts that far over to +x.

def from_the_back():
    supine = key(0.0, (0.42, LYING, -0.35), (90, 180, 0), neck=(10, 0, 0), head=(4, 0, 0),
                 rh=hand((-0.10, HAND, 0.05), (-1.0, 0.4, 0.0), (0.0, 0.0, -1.0)),
                 lh=hand((0.94, HAND, 0.05), (1.0, 0.4, 0.0), (0.0, 0.0, -1.0)),
                 rf=foot((0.26, 0.14, -1.10), (0.0, 1.0, 0.0), (-0.1, 1.0, -0.3)),
                 lf=foot((0.60, 0.14, -1.10), (0.0, 1.0, 0.0), (0.1, 1.0, -0.3)))
    # The head up, the left knee bent and its foot planted, the right arm out past the head, the left arm lifting to reach across.
    gather = then(supine, 0.45, hips=(0.42, LYING + 0.01, -0.35), body=(88, 170, 0), neck=(30, 0, 0), head=(10, 0, 0),
                  rh=hand((0.02, HAND, 1.30), (-1.0, 0.2, 0.0), (0.0, 0.0, 1.0)),
                  lh=hand((0.50, 0.62, 0.40), (1.0, 0.0, -0.3)),
                  lf=foot((0.64, ANKLE, -0.78), (0.1, 1.0, 0.0), (0.0, 0.0, -1.0)))
    # Over onto the right side: the left foot pushes, the left knee comes across, the left hand plants in front of the chest.
    side = then(gather, 0.95, hips=(0.20, 0.42, -0.36), body=(84, 100, 0), spine=(6, -18, 0), neck=(18, -8, 0), head=(0, 0, 0),
                rh=hand((-0.02, HAND, 1.15), (-1.0, 0.3, 0.0), (0.2, 0.0, 1.0)),
                lh=hand((-0.36, HAND, 0.30), (0.2, 1.0, -0.5), (0.0, 0.0, 1.0)),
                lf=foot((-0.12, 0.19, -0.72), (-1.0, 0.2, 0.2), (-0.3, -0.25, -0.9)),
                rf=foot((0.22, 0.14, -0.95), (-1.0, 0.0, 0.0), (0.0, -0.2, -1.0)))
    # Pushed up off both hands, nearly over onto the knees.
    rising = then(side, 1.35, hips=(0.06, 0.53, -0.36), body=(72, 30, 0), spine=(6, 0, 0), neck=(-6, 0, 0), head=(-6, 0, 0),
                  rh=hand((0.36, HAND, 0.72), (0.2, -0.3, -1.0), (0.0, 0.0, 1.0)),
                  lh=hand((-0.42, HAND, 0.52), (-0.4, 0.3, -1.0), (0.0, 0.0, 1.0)),
                  lf=foot((-0.18, 0.17, -0.71), (-0.3, -1.0, 0.2), TOES_DOWN),
                  rf=foot((0.20, 0.16, -0.78), (-0.2, -1.0, 0.0), TOES_DOWN))
    return [supine, gather, side, rising] + the_way_up(1.7)


# --- from the front ---

def from_the_front():
    prone = key(0.0, (0.0, LYING, 0.04), (90, 0, 0), neck=(-6, 0, 0), head=(-4, 0, 0),
                lh=hand((-0.56, HAND, 0.25), (-1.0, 0.5, 0.0), (0.0, 0.0, -1.0)),
                rh=hand((0.56, HAND, 0.25), (1.0, 0.5, 0.0), (0.0, 0.0, -1.0)),
                lf=foot((-0.20, 0.15, -0.72), (0.0, -1.0, 0.0), (0.0, -0.6, -0.8)),
                rf=foot((0.20, 0.15, -0.72), (0.0, -1.0, 0.0), (0.0, -0.6, -0.8)))
    # The hands in under the shoulders, the elbows up and back, the head lifting.
    hands = then(prone, 0.4, neck=(-26, 0, 0), head=(-8, 0, 0),
                 lh=hand((-0.46, HAND, 0.64), (-0.6, 0.8, -0.4), (0.0, 0.0, 1.0)),
                 rh=hand((0.46, HAND, 0.64), (0.6, 0.8, -0.4), (0.0, 0.0, 1.0)))
    # The push: chest up off the ground, the hips still low.
    push = then(hands, 0.8, hips=(0.0, 0.34, -0.02), body=(78, 0, 0), spine=(-4, 0, 0), neck=(-24, 0, 0),
                lh=hand((-0.44, HAND, 0.62), (-0.5, 0.2, -1.0), (0.0, 0.0, 1.0)),
                rh=hand((0.44, HAND, 0.62), (0.5, 0.2, -1.0), (0.0, 0.0, 1.0)),
                lf=foot((-0.20, 0.16, -0.72), (0.0, -1.0, 0.3), TOES_DOWN),
                rf=foot((0.20, 0.16, -0.72), (0.0, -1.0, 0.3), TOES_DOWN))
    return [prone, hands, push] + the_way_up(1.2)


CLIPS = (("getup_back", from_the_back()), ("getup_front", from_the_front()))


# --- between the keys ---

def _slopes(ts, ys):
    """Fritsch-Carlson slopes: a curve through every key with no overshoot, flat where two keys agree, still at the ends."""
    n = len(ts)
    d = [0.0] * n
    deltas = [(ys[i + 1] - ys[i]) / (ts[i + 1] - ts[i]) for i in range(n - 1)]
    for k in range(1, n - 1):
        a, b = deltas[k - 1], deltas[k]
        if a * b <= 0.0:
            continue
        h0, h1 = ts[k] - ts[k - 1], ts[k + 1] - ts[k]
        w1, w2 = 2.0 * h1 + h0, h1 + 2.0 * h0
        d[k] = (w1 + w2) / (w1 / a + w2 / b)
    return d


def _curve(ts, ys, t):
    if t <= ts[0]:
        return ys[0]
    if t >= ts[-1]:
        return ys[-1]
    i = max(j for j in range(len(ts) - 1) if ts[j] <= t)
    d = _slopes(ts, ys)
    h = ts[i + 1] - ts[i]
    s = (t - ts[i]) / h
    return ((2 * s ** 3 - 3 * s ** 2 + 1) * ys[i] + (s ** 3 - 2 * s ** 2 + s) * h * d[i] + (-2 * s ** 3 + 3 * s ** 2) * ys[i + 1]
            + (s ** 3 - s ** 2) * h * d[i + 1])


def _vector(keys, t, get):
    ts = [k["t"] for k in keys]
    vs = [get(k) for k in keys]
    return tuple(_curve(ts, [v[c] for v in vs], t) for c in range(len(vs[0])))


def _turn(keys, t, field):
    """A turn between the keys, on the short way round from each to the next."""
    ts = [k["t"] for k in keys]
    qs = []
    for k in keys:
        p, r, y = k[field]
        q = rot(p=p, r=r, y=y)
        if qs and qs[-1].dot(q) < 0.0:
            q = -q
        qs.append(q)
    q = Quaternion(tuple(_curve(ts, [v[c] for v in qs], t) for c in range(4)))
    q.normalize()
    return q


# A hand or foot moving from one place on the ground to another is lifted on the way, not slid.
GROUND = 0.3
LIFT = 0.16


def _limb(keys, t, field):
    at = Vector(_vector(keys, t, lambda k: k[field][0]))
    for a, b in zip(keys, keys[1:]):
        if a["t"] <= t < b["t"]:
            p, q = Vector(a[field][0]), Vector(b[field][0])
            flat = Vector((q.x - p.x, 0.0, q.z - p.z)).length
            if p.y < GROUND and q.y < GROUND and flat > 0.06:
                at.y += min(LIFT, 0.4 * flat) * math.sin(math.pi * (t - a["t"]) / (b["t"] - a["t"]))
    pole = Vector(_vector(keys, t, lambda k: k[field][1]))
    third = keys[0][field][2]
    tip = None
    if field in ("lf", "rf"):
        tip = unit(Vector(_vector(keys, t, lambda k: k[field][2])))
    else:
        # Fingers only while some key lays the hand on something; a hand with none carries on its forearm's line.
        before = max((k for k in keys if k["t"] <= t), key=lambda k: k["t"])
        after = min((k for k in keys if k["t"] > t), key=lambda k: k["t"], default=before)
        laid = [k[field][2] for k in (before, after) if k[field][2] is not None]
        if laid:
            tip = unit(Vector(laid[0]).lerp(Vector(laid[-1]), 0.5 if len(laid) == 2 else 0.0))
    return at, pole, tip


def _out_of_the_ground(root, end, l1, l2, pole, floor):
    """Where a knee or elbow between `root` and `end` (Blender axes) points to stay above `floor`: the pole turned round the root-end line as
    little as it takes, or None if it is above already. The keys are written for level ground; between them a limb's joint would dip into it."""
    v = end - root
    d = max(abs(l1 - l2) + 1e-4, min(l1 + l2 - 1e-4, v.length))
    along = unit(v)
    a = (l1 * l1 - l2 * l2 + d * d) / (2.0 * d)
    h = math.sqrt(max(0.0, l1 * l1 - a * a))
    centre = root + along * a
    base = pole - along * pole.dot(along)
    if base.length < 1e-6:
        return None
    base.normalize()
    other = along.cross(base)
    for step in range(0, 37):
        for sign in ((1.0,) if step == 0 else (1.0, -1.0)):
            angle = math.radians(5.0 * step * sign)
            out = base * math.cos(angle) + other * math.sin(angle)
            if (centre + out * h).z >= floor:
                return None if step == 0 else out
    return None


def _unity(v):
    return (-v.x, v.z, -v.y)


def keep_out_of_the_ground(P):
    """Turns any knee or elbow the pose would put into the ground out of it, about its limb's own line."""
    D, pos = solve(dict(P))
    for side, name in (("l", "free"), ("r", "right")):
        a, f, h = "arm_" + side, "forearm_" + side, "hand_" + side
        out = _out_of_the_ground(pos[a], U(*P[name]["hand"]), (HEAD[f] - HEAD[a]).length, (HEAD[h] - HEAD[f]).length, U(*P[name]["pole"]), ELBOW_FLOOR)
        if out is not None:
            P[name] = dict(P[name], pole=_unity(out))
    for side in ("l", "r"):
        th, sn, ft = "thigh_" + side, "shin_" + side, "foot_" + side
        spec = P[side + "_leg"]
        out = _out_of_the_ground(pos[th], pos[th] + U(*spec["foot"]), (HEAD[sn] - HEAD[th]).length, (HEAD[ft] - HEAD[sn]).length, U(*spec["knee"]),
                                 KNEE_FLOOR)
        if out is not None:
            P[side + "_leg"] = dict(spec, knee=_unity(out))
    return P


KNEE_FLOOR = KNEE
ELBOW_FLOOR = 0.10


def pose_at(keys, t):
    return keep_out_of_the_ground(_pose_at(keys, t))


def _pose_at(keys, t):
    hips = _vector(keys, t, lambda k: k["hips"])
    body = _turn(keys, t, "body")
    P = {"hips": hips, "body": body, "spine": _turn(keys, t, "spine"), "neck": _turn(keys, t, "neck"), "head": _turn(keys, t, "head")}
    for field, name in (("lh", "free"), ("rh", "right")):
        at, pole, fingers = _limb(keys, t, field)
        spec = {"hand": tuple(at), "pole": tuple(pole), "space": "world"}
        if fingers is not None:
            spec["fingers"] = tuple(fingers)
        P[name] = spec
    for field, side in (("lf", "l"), ("rf", "r")):
        at, knee, toe = _limb(keys, t, field)
        hip = U(*hips) + body @ (HEAD["thigh_" + side] - HEAD["hips"])
        hip_u = Vector((-hip.x, hip.z, -hip.y))
        P[side + "_leg"] = {"foot": tuple(at - hip_u), "knee": tuple(knee), "toe": tuple(toe), "space": "root"}
    return P


# --- checks: what the owner saw go wrong ---

LEG_R, ARM_R, HEAD_R, TORSO_R = 0.135, 0.09, 0.35, 0.26


def checks(D, pos, worst):
    """Keeps the worst of: a knee turned to bend backward (the thigh twisted round more than a quarter turn from the hips), and any part's skin
    under the ground."""
    hips_right = D["hips"] @ LEFT * -1.0
    for side in ("l", "r"):
        hinge = D["thigh_" + side] @ RIGHT
        worst["knee_hinge"] = min(worst["knee_hinge"], hinge.dot(hips_right))
        worst["knee_low"] = min(worst["knee_low"], pos["shin_" + side].z - LEG_R)
        worst["ankle_low"] = min(worst["ankle_low"], pos["foot_" + side].z - 0.09)
        worst["wrist_low"] = min(worst["wrist_low"], pos["hand_" + side].z - 0.06)
        worst["elbow_low"] = min(worst["elbow_low"], pos["forearm_" + side].z - ARM_R)
    head_centre = pos["head"] + D["head"] @ (UP * 0.35)
    worst["head_low"] = min(worst["head_low"], head_centre.z - HEAD_R)
    for share in (0.0, 0.5, 1.0):
        spine_point = pos["hips"].lerp(pos["neck"], share)
        worst["torso_low"] = min(worst["torso_low"], spine_point.z - TORSO_R)


def bake_getups():
    if rig.animation_data and rig.animation_data.action:
        old = rig.animation_data.action
        rig.animation_data.action = None
        bpy.data.actions.remove(old)
    rig.animation_data_create()
    action = bpy.data.actions.new("GetUp")
    rig.animation_data.action = action
    scene = bpy.context.scene
    scene.timeline_markers.clear()
    previous = {}
    start = 0
    spans = {}
    for name, keys in CLIPS:
        seconds = keys[-1]["t"]
        frames = int(round(seconds * FPS)) + 1
        scene.timeline_markers.new(name, frame=start)
        worst = {k: 9.0 for k in ("knee_hinge", "knee_low", "ankle_low", "wrist_low", "elbow_low", "head_low", "torso_low")}
        for i in range(frames):
            D, pos = solve(pose_at(keys, i / float(FPS)))
            here = {k: 9.0 for k in worst}
            checks(D, pos, here)
            for k, v in here.items():
                worst[k] = min(worst[k], v)
            bad = ["%s %.2f" % kv for kv in sorted(here.items()) if kv[1] < -0.03]
            if bad and globals().get("GETUP_VERBOSE"):
                print("  %s %.2f s: %s" % (name, i / float(FPS), ", ".join(bad)))
            apply_pose(locals_of(D), pos["hips"])
            f = start + i
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
        print("%-12s %d frames | worst: %s" % (name, frames, ", ".join("%s %.3f" % kv for kv in sorted(worst.items()))))
        spans[name] = (start, start + frames - 1, keys)
        start += frames
    scene.timeline_markers.new("end", frame=start)
    for fc in action.fcurves:
        for kp in fc.keyframe_points:
            kp.interpolation = 'LINEAR'
    scene.render.fps = FPS
    scene.frame_start = 0
    scene.frame_end = start - 1
    scene.frame_set(0)
    return spans
