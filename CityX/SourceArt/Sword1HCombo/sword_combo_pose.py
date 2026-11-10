# THE ONE-HANDED SWORD'S THREE CUTS AS FULL-BODY CLIPS. Owner, 2026-11-05: full-body Blender animation, with "our animations look alright right now could you just use those
# as referance". So the hand's path, the blade and the timing are SwordStrikes' own (ported below, keep them in step), and the rest of the body is authored around them:
# the weight going onto the back foot and then over the front one, the feet planted in the world while the body lunges past them, the rear heel coming up, the back foot pulled
# along behind a long lunge, the head staying on the target, the free arm working against the sword.
#
# Needs the rig (../Octaslash/octaslash_rig.py), octaslash_pose.py's solver (with OCTASLASH_NO_BAKE set) and ../planted_feet.py loaded first; sword_combo_build.py does all three.
#
# Poses are written in the character's own frame (Unity axes: x right, y up, z forward). The FEET are written in the stage's frame, which the game's lunge carries the body through:
# a planted foot's place in the clip is its place on the stage less how far the body has lunged by then, so it stays still on the ground as the capsule moves.

import bpy
import math
from mathutils import Vector, Quaternion

ARM_METRES = 0.85 * 1.0456          # BodyRig.WristT * BodyRig.ArmLength
SHOULDER_X, SHOULDER_Y = 0.35, 1.9
HIP_HEIGHT = 0.9
STANCE_X = 0.20


def V(x, y, z):
    return Vector((x, y, z))


def right_shoulder(twist):
    r = math.radians(twist)
    return V(SHOULDER_X * math.cos(r), SHOULDER_Y, -SHOULDER_X * math.sin(r))


class Plane:
    def __init__(self, forward, across):
        self.forward = V(*forward).normalized()
        a = V(*across)
        self.across = (a - self.forward * a.dot(self.forward)).normalized()

    def blade(self, degrees):
        r = math.radians(degrees)
        return (self.forward * math.cos(r) + self.across * math.sin(r)).normalized()

    def edge(self, degrees):
        r = math.radians(degrees)
        return (-self.forward * math.sin(r) + self.across * math.cos(r)).normalized()


FOREHAND_FLAT = Plane((0.0, -0.05, 1.0), (-1.0, -0.2, 0.0))
DIAGONAL_DOWN = Plane((0.247, -0.165, 0.955), (-0.883, -0.442, 0.152))
DIAGONAL_BACKHAND = Plane((-0.247, -0.165, 0.955), (0.883, -0.442, 0.152))

FREE_RELAXED = V(-0.20, -0.94, 0.06)
FREE_SWING = 0.12


def key(hand, blade=None, edge=None, plane=None, angle=None, elbow=0.3, twist=0.0, hips=0.0, lean=0.0, tilt=0.0, drop=0.0, free=None, control=None, control_twist=0.0,
        staged=False, shift=(0.0, 0.0)):
    if plane is not None:
        blade, edge = plane.blade(angle), plane.edge(angle)
    else:
        blade = V(*blade).normalized()
        edge = V(*edge)
        edge = (edge - blade * edge.dot(blade)).normalized()
    k = {"hand": (V(*hand) - right_shoulder(twist)) / ARM_METRES, "blade": blade, "edge": edge, "plane": plane, "angle": angle, "elbow": elbow, "twist": twist, "hips": hips,
         "lean": lean, "tilt": tilt, "drop": drop, "staged": staged, "shift": V(shift[0], 0.0, shift[1])}
    k["control"] = (V(*control) - right_shoulder(control_twist)) / ARM_METRES if control is not None else None
    k["free"] = V(*free) if free is not None else FREE_RELAXED + V(0.0, 0.0, max(-1.0, min(1.0, (k["hand"].x - 0.15) * 0.5)) * FREE_SWING)
    return k


READY = key((0.30, 1.34, 0.36), blade=(-0.10, 0.50, 0.86), edge=(0.0, -1.0, 0.0), elbow=0.30, twist=12.0, hips=6.0, lean=3.0, drop=0.08)

# name: (wind-up, hold, strike, follow, settle, recover) seconds, the game's lunge metres, wind-up, contact, follow-through
CUTS = {
    "slash": ((0.13, 0.04, 0.11, 0.10, 0.05, 0.19), 0.20,
              key((0.85, 1.58, -0.08), plane=FOREHAND_FLAT, angle=-140.0, elbow=0.60, twist=55.0, hips=28.0, lean=-6.0, tilt=5.0, drop=0.12, free=(-0.18, -0.72, 0.60), shift=(0.06, -0.06)),
              key((-0.02, 1.50, 0.78), plane=FOREHAND_FLAT, angle=-8.0, elbow=0.25, twist=-15.0, hips=-28.0, lean=16.0, tilt=4.0, drop=0.15, free=(-0.42, -0.78, -0.38),
                  control=(0.80, 1.55, 0.55), control_twist=20.0, staged=True, shift=(-0.06, 0.12)),
              key((-0.52, 1.42, 0.42), plane=FOREHAND_FLAT, angle=110.0, elbow=0.40, twist=-55.0, hips=-40.0, lean=13.0, tilt=1.0, drop=0.14, free=(-0.44, -0.76, -0.40), shift=(-0.08, 0.10))),
    "cut": ((0.14, 0.04, 0.11, 0.085, 0.05, 0.195), 0.40,
            key((-0.40, 1.92, 0.30), plane=DIAGONAL_BACKHAND, angle=-127.5, elbow=0.60, twist=-48.0, hips=-24.0, lean=-6.0, tilt=-4.0, drop=0.12, free=(-0.40, -0.78, -0.36), shift=(-0.06, -0.05)),
            key((0.34, 1.44, 0.58), plane=DIAGONAL_BACKHAND, angle=10.0, elbow=0.35, twist=25.0, hips=30.0, lean=16.0, tilt=-6.0, drop=0.15, free=(-0.38, -0.76, 0.48),
                control=(-0.55, 1.80, 0.55), control_twist=-12.0, staged=True, shift=(0.06, 0.12)),
            key((0.58, 1.32, 0.28), plane=DIAGONAL_BACKHAND, angle=115.0, elbow=0.50, twist=48.0, hips=40.0, lean=13.0, tilt=-4.0, drop=0.14, free=(-0.38, -0.74, 0.52), shift=(0.08, 0.10))),
    "finisher": ((0.24, 0.08, 0.13, 0.095, 0.10, 0.55), 0.55,
                 key((0.70, 2.12, -0.10), plane=DIAGONAL_DOWN, angle=-130.0, elbow=0.70, twist=50.0, hips=22.0, lean=-10.0, tilt=-6.0, drop=0.12, free=(0.25, -0.30, 0.80), shift=(0.07, -0.08)),
                 key((0.0, 1.40, 0.76), plane=DIAGONAL_DOWN, angle=10.0, elbow=0.25, twist=-25.0, hips=-32.0, lean=30.0, tilt=10.0, drop=0.18, free=(-0.45, -0.55, -0.60),
                     control=(0.72, 1.92, 0.52), control_twist=12.5, shift=(-0.07, 0.06)),
                 key((-0.38, 1.26, 0.44), plane=DIAGONAL_DOWN, angle=115.0, elbow=0.45, twist=-60.0, hips=-46.0, lean=26.0, tilt=8.0, drop=0.18, free=(-0.45, -0.50, -0.65), shift=(-0.09, 0.04))),
}

# THE FEET on the stage: (from seconds, to seconds, where to (x, z), how high it lifts). Each starts from the stance before it.
READY_LEFT, READY_RIGHT = (-STANCE_X, 0.10), (STANCE_X, -0.10)
STEPS = {
    "slash": {"l": [(0.015, 0.15, (-0.22, 0.42), 0.10), (0.50, 0.60, (-STANCE_X, 0.30), 0.03)],
              "r": [(0.36, 0.55, (STANCE_X, 0.10), 0.06)]},
    # The right foot passes the left in this one; the left comes up after it.
    "cut": {"r": [(0.015, 0.16, (0.16, 0.50), 0.12)],
            "l": [(0.37, 0.57, (-STANCE_X, 0.30), 0.06)]},
    # A long stride, and so far a lunge that the back foot is pulled along on its toes behind it before it steps up.
    "finisher": {"l": [(0.10, 0.36, (-0.24, 0.60), 0.14), (0.85, 1.05, (-STANCE_X, 0.65), 0.03)],
                 "r": [(0.26, 0.47, (STANCE_X, 0.28), 0.02), (0.75, 1.00, (STANCE_X, 0.45), 0.08)]},
}


def eased(kind, u):
    u = max(0.0, min(1.0, u))
    if kind == "smooth":
        return u * u * (3.0 - 2.0 * u)
    if kind == "slow_in":
        return 1.0 - (1.0 - u) ** 3
    if kind == "snap":
        return u * u * u
    return u


def timeline(name):
    (wind, hold, strike, follow_s, settle, recover), lunge, windup, contact, follow = CUTS[name]
    total = wind + hold + strike + follow_s + settle + recover
    t1 = wind / total
    t2 = (wind + hold) / total
    t3 = (wind + hold + strike) / total
    t4 = (wind + hold + strike + follow_s) / total
    t5 = (wind + hold + strike + follow_s + settle) / total
    return total, lunge, [(0.0, READY, None), (t1, windup, "smooth"), (t2, windup, "linear"), (t3, contact, "snap"), (t4, follow, "slow_in"), (t5, follow, "linear"), (1.0, READY, "smooth")]


def sample(keys, t01):
    """SwordStrikes.Sample."""
    i = 0
    while i < len(keys) - 2 and t01 >= keys[i + 1][0]:
        i += 1
    ta, a, _ = keys[i]
    tb, b, ease_kind = keys[i + 1]
    through = max(0.0, min(1.0, (t01 - ta) / max(tb - ta, 1e-4)))
    u = eased(ease_kind, through)
    hips_u = through ** 1.5 if b["staged"] else u
    shoulders_u = through ** 2.0 if b["staged"] else u
    hand_u = through ** 2.5 if b["staged"] else u
    if a["plane"] is not None and a["plane"] is b["plane"]:
        angle = a["angle"] + (b["angle"] - a["angle"]) * u
        blade = a["plane"].blade(angle)
        edge = a["plane"].edge(angle) * (1.0 if b["angle"] >= a["angle"] else -1.0)
        if abs(b["angle"] - a["angle"]) < 1e-3:
            edge = a["edge"]
    else:
        blade = a["blade"].slerp(b["blade"], u, a["blade"]).normalized()
        edge = a["edge"].slerp(b["edge"], u, a["edge"]).normalized()
    if b["control"] is not None:
        v = 1.0 - hand_u
        hand = a["hand"] * (v * v) + b["control"] * (2.0 * v * hand_u) + b["hand"] * (hand_u * hand_u)
    else:
        hand = a["hand"].lerp(b["hand"], hand_u)

    def mix(field, share):
        return a[field] + (b[field] - a[field]) * share

    return {
        "hand": hand, "blade": blade, "edge": edge, "elbow": mix("elbow", u), "twist": mix("twist", shoulders_u), "hips": mix("hips", hips_u), "lean": mix("lean", shoulders_u),
        "tilt": mix("tilt", shoulders_u), "drop": mix("drop", hips_u), "shift": a["shift"].lerp(b["shift"], hips_u),
        "free": a["free"].normalized().slerp(b["free"].normalized(), u, a["free"].normalized()) * (a["free"].length + (b["free"].length - a["free"].length) * u),
    }


def lunged(name, seconds):
    """How far the game's lunge has carried the body (SoulsSwordCombat.ApplyLunge): from halfway through the startup to the end of the live window, eased."""
    (wind, hold, strike, follow_s, _, _), metres = CUTS[name][0], CUTS[name][1]
    startup = wind + hold + strike * 0.25
    window = wind + hold + strike + follow_s * 0.7
    p = (seconds - startup * 0.5) / (window - startup * 0.5)
    return smooth(p) * metres


def to_unity(b):
    return V(-b.x, b.z, -b.y)


def pose_at(name, keys, t01, total, stats):
    s = sample(keys, t01)
    seconds = t01 * total
    hips_at = V(s["shift"].x, HIP_HEIGHT - s["drop"], s["shift"].z)
    # The hips turn with their own share and carry a part of the lean; the spine takes the rest. The head stays on what is ahead.
    # (SwordFrame's turns are positive to the right; rot's yaw is positive to the left.)
    hips_lean = s["lean"] * 0.35
    body = rot(p=hips_lean, y=-s["hips"], r=s["tilt"] * 0.3)
    spine = rot(p=s["lean"] - hips_lean, y=-(s["twist"] - s["hips"]), r=s["tilt"] * 0.7)
    neck = rot(p=-s["lean"] * 0.45, y=s["twist"] * 0.45, r=-s["tilt"] * 0.4)
    head = rot(p=-s["lean"] * 0.45, y=s["twist"] * 0.45, r=-s["tilt"] * 0.4)

    P = {
        "hips": tuple(hips_at), "body": body, "spine": spine, "neck": neck, "head": head,
        "sword": {"hand": tuple(s["hand"] * ARM_METRES), "blade": tuple(s["blade"]), "edge": tuple(s["edge"]), "space": "root",
                  "elbow": (0.45 + s["elbow"] * 0.6, -1.0 + s["elbow"] * 0.9, -0.25)},
        "free": {"hand": tuple(s["free"] * ARM_METRES), "pole": (-0.5, -0.8, -0.3), "space": "root"},
    }
    for side in ("l", "r"):
        hip_b = U(*hips_at) + body @ (HEAD["thigh_" + side] - HEAD["hips"])
        P[side + "_leg"] = planted_leg(side, to_unity(hip_b), STEPS[name][side], READY_LEFT if side == "l" else READY_RIGHT, seconds, lunged(name, seconds), hips_at.z, 28.0, stats)
    return P


def bake_combo():
    if rig.animation_data and rig.animation_data.action:
        old = rig.animation_data.action
        rig.animation_data.action = None
        bpy.data.actions.remove(old)
    rig.animation_data_create()
    action = bpy.data.actions.new("Sword1HCombo")
    rig.animation_data.action = action
    scene = bpy.context.scene
    scene.timeline_markers.clear()
    previous = {}
    start = 0
    spans = {}
    for name in ("slash", "cut", "finisher"):
        total, lunge, keys = timeline(name)
        frames = int(round(total * FPS))
        scene.timeline_markers.new(name, frame=start)
        stats = {"reach": 0.0}
        worst_wrist = 0.0
        tightest_knee = 180.0
        for i in range(frames):
            P = pose_at(name, keys, i / float(frames), total, stats)
            D, pos = solve(P)
            worst_wrist = max(worst_wrist, P.get("_wrist_bend", 0.0))
            for side in ("l", "r"):
                thigh = pos["thigh_" + side] - pos["shin_" + side]
                shin = pos["foot_" + side] - pos["shin_" + side]
                tightest_knee = min(tightest_knee, math.degrees(thigh.angle(shin)))
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
            # The preview's lunge: the game moves the body; the export ignores this.
            rig.location = U(0.0, 0.0, lunged(name, i / float(FPS)))
            rig.keyframe_insert("location", frame=f)
        leg = (HEAD["shin_l"] - HEAD["thigh_l"]).length + (HEAD["foot_l"] - HEAD["shin_l"]).length
        print("%-9s %d frames | worst wrist bend %.0f deg | tightest knee %.0f deg | furthest ankle from its hip %.2f m of a %.2f m leg" % (name, frames, worst_wrist, tightest_knee, stats["reach"], leg))
        spans[name] = (start, start + frames - 1, total, keys)
        start += frames
    scene.timeline_markers.new("end", frame=start - 1)
    for fc in action.fcurves:
        for kp in fc.keyframe_points:
            kp.interpolation = 'LINEAR'
    scene.render.fps = FPS
    scene.frame_start = 0
    scene.frame_end = start - 1
    scene.frame_set(0)
    return spans
