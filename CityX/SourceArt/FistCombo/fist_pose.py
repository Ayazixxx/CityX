# THE FOUR PUNCHES AS FULL-BODY CLIPS. Owner, 2026-11-09: "improve upon the fist fighting (mostly the animations)"; 2026-11-03, of the punches posed in code on the arms alone:
# "there needs to be some type of full body animation/movement just the arms doesnt look right". The moves are the ones the game has (docs/FIST_COMBAT.md, Souls' bare hands: a
# right straight, a left straight, the overhand, the uppercut), and so are their lengths and the moment each lands (FistStrikes.Seconds, ContactFraction: keep the two in step). What
# is made here is the body that throws them:
#   * a real step: the lead foot strides as the body is carried forward (the game moves it: FistStrikes.Carried), the rear foot stays planted on the ground and rolls up onto its
#     ball as the hips leave it, then steps up after the blow, so every punch ends standing as it began and the next can follow it;
#   * the weight: back over the rear foot as the blow is wound up, forward over the lead foot as it lands;
#   * the turn from the ground up: the hips first, the shoulders after them, the fist last and fastest, at full reach on the frame the blow lands;
#   * the other arm working against the blow: out in front as it is wound up, snatched back to the ribs as it lands;
#   * the head kept on what is ahead while the shoulders turn under it.
#
# Needs the rig (../Octaslash/octaslash_rig.py), octaslash_pose.py's solver (with OCTASLASH_NO_BAKE set) and ../planted_feet.py loaded first; fist_build.py does all three.
#
# Poses are written for a RIGHT-handed blow in the character's own frame (Unity axes: x right, y up, z forward, metres, the feet's ground at y 0); the left straight is the right
# one mirrored. The FEET are written on the stage (planted_feet.py).

import bpy
import math
from mathutils import Vector, Quaternion

FPS = 60
HIP_HEIGHT = 0.9
STANCE_X = 0.20
READY_DROP = 0.07                   # FistStrikes.ReadyHipDrop


def V(x, y, z):
    return Vector((x, y, z))


def key(fist, off, twist=0.0, hips=0.0, lean=2.0, tilt=0.0, drop=READY_DROP, shift=(0.0, 0.0), chin=0.0, fist_pole=(0.4, -0.2, -1.0), off_pole=(-0.4, -0.2, -1.0), control=None,
        staged=False):
    """One moment of a blow. fist, off: where the throwing wrist and the other one are; the poles: which way each elbow points; twist, hips: the shoulders' and the hips' turn to
    the right, degrees; lean: forward; tilt: the right side down; drop: how far the hips sink; shift: the hips' place over the feet (x, z); chin: the head tucked down; control:
    a place the fist passes near on its way here; staged: the hips lead into this key, the shoulders follow and the fist comes last."""
    return {"fist": V(*fist), "off": V(*off), "twist": twist, "hips": hips, "lean": lean, "tilt": tilt, "drop": drop, "shift": V(shift[0], 0.0, shift[1]), "chin": chin,
            "fist_pole": V(*fist_pole), "off_pole": V(*off_pole), "control": V(*control) if control is not None else None, "staged": staged}



# Standing ready: a little sunk, the arms hanging as they do in the walk, which is what the game blends to and from.
READY = key((0.50, 1.04, 0.06), (-0.50, 1.04, 0.06))

# The other hand as a blow lands: snatched back to the ribs, its elbow straight behind it and close to the body. (Lower and wider it is a hand on the hip.)
CHAMBER, CHAMBER_POLE = (-0.30, 1.52, 0.02), (-0.15, -0.5, -1.0)

# Both hands on their way back down after a blow: loosely up before the belly, the elbows down by the ribs. (Drawn straight back to the sides they pass through hands on the hips.)
FIST_BACK, FIST_BACK_POLE = (0.26, 1.42, 0.42), (0.35, -1.0, -0.2)
OFF_BACK, OFF_BACK_POLE = (-0.26, 1.36, 0.36), (-0.35, -1.0, -0.2)

# name: seconds, how far the game carries the body and between which seconds, the keys [(share of the blow, key, how it is eased into)], and the feet on the stage
# (planted_feet.foot_on_stage): the lead foot (the one across from the fist) and the rear one.
PUNCHES = {
    # A committed lunge: the fist drawn back to the ribs, a stride, and the arm driven straight out from the turning hips.
    "punch_right": {
        "seconds": 0.50, "carry": (0.45, 0.05, 0.23),
        "keys": [
            (0.00, READY, None),
            (0.22, key((0.40, 1.36, -0.20), (-0.16, 1.60, 0.50), twist=38.0, hips=16.0, lean=-3.0, tilt=2.0, drop=0.10, shift=(0.03, -0.05),
                       fist_pole=(0.5, 0.1, -1.0), off_pole=(-0.7, -1.0, 0.1)), "smooth"),
            (0.46, key((0.05, 1.74, 1.60), CHAMBER, twist=-48.0, hips=-26.0, lean=17.0, tilt=-3.0, drop=0.12, shift=(-0.03, 0.05), chin=6.0,
                       fist_pole=(0.6, -1.0, 0.0), off_pole=CHAMBER_POLE, control=(0.30, 1.50, 0.45), staged=True), "linear"),
            (0.60, key((0.05, 1.74, 1.60), CHAMBER, twist=-52.0, hips=-28.0, lean=18.5, tilt=-3.0, drop=0.12, shift=(-0.03, 0.05), chin=6.0,
                       fist_pole=(0.6, -1.0, 0.0), off_pole=CHAMBER_POLE), "slow_in"),
            (0.80, key(FIST_BACK, OFF_BACK, twist=-14.0, hips=-8.0, lean=8.0, drop=0.09, shift=(0.0, 0.02), fist_pole=FIST_BACK_POLE, off_pole=OFF_BACK_POLE), "smooth"),
            (1.00, READY, "smooth"),
        ],
        "lead": [(0.04, 0.20, (-0.22, 0.51), 0.09)],
        "rear": [(0.30, 0.44, (STANCE_X, 0.43), 0.05)],
    },
    # The overhand: the fist wound back and high behind the head for a long moment (the tell), then brought over the top and down across with the whole body, the other arm
    # thrown out behind.
    "punch_heavy": {
        "seconds": 0.80, "carry": (0.50, 0.30, 0.48),
        "keys": [
            (0.00, READY, None),
            (0.20, key((0.52, 1.75, -0.10), (-0.20, 1.55, 0.45), twist=25.0, hips=12.0, lean=-4.0, drop=0.09, shift=(0.04, -0.06),
                       fist_pole=(1.0, 0.2, -0.6), off_pole=(-0.7, -1.0, 0.1)), "smooth"),
            (0.45, key((0.50, 2.45, -0.38), (-0.12, 1.85, 0.62), twist=50.0, hips=22.0, lean=-12.0, tilt=6.0, drop=0.08, shift=(0.05, -0.10),
                       fist_pole=(1.0, 0.3, -0.5), off_pole=(-0.8, -0.8, 0.0)), "smooth"),
            (0.60, key((-0.12, 1.40, 1.55), (-0.46, 1.10, -0.42), twist=-55.0, hips=-30.0, lean=24.0, tilt=-10.0, drop=0.16, shift=(-0.05, 0.08), chin=8.0,
                       fist_pole=(0.8, 0.6, -0.1), off_pole=(-0.3, 0.2, -1.0), control=(0.25, 2.55, 0.55), staged=True), "linear"),
            (0.72, key((-0.34, 1.10, 1.00), (-0.48, 1.08, -0.44), twist=-64.0, hips=-34.0, lean=27.0, tilt=-12.0, drop=0.17, shift=(-0.05, 0.08), chin=8.0,
                       fist_pole=(0.8, 0.4, -0.1), off_pole=(-0.3, 0.2, -1.0)), "slow_in"),
            (0.88, key(FIST_BACK, OFF_BACK, twist=-18.0, hips=-10.0, lean=10.0, drop=0.10, shift=(0.0, 0.03), fist_pole=FIST_BACK_POLE, off_pole=OFF_BACK_POLE), "smooth"),
            (1.00, READY, "smooth"),
        ],
        "lead": [(0.20, 0.44, (-0.24, 0.58), 0.12)],
        "rear": [(0.56, 0.74, (STANCE_X, 0.48), 0.06)],
    },
    # The uppercut: a dip, the fist dropped low by the hip, then the legs drive it up the middle under the chin, the elbow under the fist.
    "punch_uppercut": {
        "seconds": 0.60, "carry": (0.30, 0.10, 0.30),
        "keys": [
            (0.00, READY, None),
            (0.26, key((0.36, 0.98, 0.12), (-0.22, 1.50, 0.40), twist=28.0, hips=12.0, lean=20.0, tilt=8.0, drop=0.24, shift=(0.03, -0.02),
                       fist_pole=(0.6, 0.0, -1.0), off_pole=(-0.7, -1.0, 0.0)), "smooth"),
            (0.50, key((0.06, 2.05, 0.70), CHAMBER, twist=-36.0, hips=-22.0, lean=-8.0, tilt=-4.0, drop=0.01, shift=(-0.02, 0.05), chin=-4.0,
                       fist_pole=(0.25, -1.0, 0.1), off_pole=CHAMBER_POLE, control=(0.24, 1.20, 0.55), staged=True), "linear"),
            (0.64, key((0.05, 2.16, 0.64), CHAMBER, twist=-40.0, hips=-24.0, lean=-10.0, tilt=-4.0, drop=0.01, shift=(-0.02, 0.05), chin=-4.0,
                       fist_pole=(0.25, -1.0, 0.1), off_pole=CHAMBER_POLE), "slow_in"),
            (0.84, key(FIST_BACK, OFF_BACK, twist=-12.0, hips=-6.0, lean=4.0, drop=0.08, shift=(0.0, 0.02), fist_pole=FIST_BACK_POLE, off_pole=OFF_BACK_POLE), "smooth"),
            (1.00, READY, "smooth"),
        ],
        "lead": [(0.06, 0.24, (-0.21, 0.36), 0.07)],
        "rear": [(0.38, 0.52, (STANCE_X, 0.28), 0.05)],
    },
}
PUNCHES["punch_left"] = dict(PUNCHES["punch_right"], mirrored=True)
ORDER_OF_PUNCHES = ("punch_right", "punch_left", "punch_heavy", "punch_uppercut")


def eased(kind, u):
    u = max(0.0, min(1.0, u))
    if kind == "smooth":
        return u * u * (3.0 - 2.0 * u)
    if kind == "slow_in":
        return 1.0 - (1.0 - u) ** 3
    return u


def sample(keys, t01):
    """The blow's key at a share of it. Into a staged key the hips lead, the shoulders follow and the fist comes last and fastest."""
    i = 0
    while i < len(keys) - 2 and t01 >= keys[i + 1][0]:
        i += 1
    ta, a, _ = keys[i]
    tb, b, ease_kind = keys[i + 1]
    through = max(0.0, min(1.0, (t01 - ta) / max(tb - ta, 1e-4)))
    u = eased(ease_kind, through)
    # (Steeper, the fist crossed a third of its way in the blow's last frame: a jump, not a punch.)
    hips_u = through ** 1.3 if b["staged"] else u
    shoulders_u = through ** 1.6 if b["staged"] else u
    hand_u = through ** 2.0 if b["staged"] else u
    if b["control"] is not None:
        v = 1.0 - hand_u
        fist = a["fist"] * (v * v) + b["control"] * (2.0 * v * hand_u) + b["fist"] * (hand_u * hand_u)
    else:
        fist = a["fist"].lerp(b["fist"], hand_u)

    def mix(field, share):
        return a[field] + (b[field] - a[field]) * share

    return {"fist": fist, "off": a["off"].lerp(b["off"], shoulders_u), "fist_pole": a["fist_pole"].lerp(b["fist_pole"], hand_u), "off_pole": a["off_pole"].lerp(b["off_pole"], shoulders_u),
            "twist": mix("twist", shoulders_u), "hips": mix("hips", hips_u), "lean": mix("lean", shoulders_u), "tilt": mix("tilt", shoulders_u), "drop": mix("drop", hips_u),
            "shift": a["shift"].lerp(b["shift"], hips_u), "chin": mix("chin", shoulders_u)}


def carried(punch, seconds):
    """How far the game has carried the body by then (FistStrikes.Carried: keep the two in step)."""
    metres, start, end = punch["carry"]
    return metres * smooth((seconds - start) / (end - start))


def to_unity(b):
    return V(-b.x, b.z, -b.y)


def pose_at(punch, t01, stats):
    s = sample(punch["keys"], t01)
    seconds = t01 * punch["seconds"]
    side = -1.0 if punch.get("mirrored") else 1.0
    P, hips_at, body = body_pose(s, side)
    lead, rear = ("l", "r") if side > 0.0 else ("r", "l")
    for leg, steps, x in ((lead, punch["lead"], -STANCE_X * side), (rear, punch["rear"], STANCE_X * side)):
        hip_b = U(*hips_at) + body @ (HEAD["thigh_" + leg] - HEAD["hips"])
        on_stage = [(t0, t1, (to[0] * side, to[1]), lift) for t0, t1, to, lift in steps]
        P[leg + "_leg"] = planted_leg(leg, to_unity(hip_b), on_stage, (x, 0.0), seconds, carried(punch, seconds), hips_at.z, 28.0, stats)
    return P


def body_pose(s, side):
    """A sampled key as the solver's pose, all but the legs (the guard, ../FistGuard, stands on its own): the pose, where the hips are, and their turn."""

    def handed(v):
        return (v.x * side, v.y, v.z)

    twist, hips, tilt = s["twist"] * side, s["hips"] * side, s["tilt"] * side
    hips_at = V(s["shift"].x * side, HIP_HEIGHT - s["drop"], s["shift"].z)
    # The hips turn with their own share and carry a part of the lean; the chest has the rest of both. The body leans AT WHAT IT HITS, whichever way it is turned: leant along
    # its own turned chest, a right-hander's weight went off to its left. The head stays on what is ahead.
    # (The keys' turns are positive to the right; yaw is positive to the left.)
    body = pitch(s["lean"] * 0.35) @ yaw(-hips) @ roll(tilt * 0.3)
    chest = pitch(s["lean"]) @ yaw(-twist) @ roll(tilt)
    head = pitch(s["lean"] * 0.15 + s["chin"]) @ yaw(-twist * 0.1) @ roll(tilt * 0.2)
    neck = chest.slerp(head, 0.5)
    fist = {"hand": handed(s["fist"]), "pole": handed(s["fist_pole"]), "space": "world"}
    off = {"hand": handed(s["off"]), "pole": handed(s["off_pole"]), "space": "world"}
    P = {
        "hips": tuple(hips_at), "body": body,
        "spine": body.inverted() @ chest, "neck": chest.inverted() @ neck, "head": neck.inverted() @ head,
        "right": fist if side > 0.0 else off,
        "free": off if side > 0.0 else fist,
    }
    return P, hips_at, body


def bake_punches():
    if rig.animation_data and rig.animation_data.action:
        old = rig.animation_data.action
        rig.animation_data.action = None
        bpy.data.actions.remove(old)
    rig.animation_data_create()
    action = bpy.data.actions.new("FistCombo")
    rig.animation_data.action = action
    scene = bpy.context.scene
    scene.timeline_markers.clear()
    previous = {}
    start = 0
    spans = {}
    leg = (HEAD["shin_l"] - HEAD["thigh_l"]).length + (HEAD["foot_l"] - HEAD["shin_l"]).length
    arm = (HEAD["forearm_r"] - HEAD["arm_r"]).length + (HEAD["hand_r"] - HEAD["forearm_r"]).length
    for name in ORDER_OF_PUNCHES:
        punch = PUNCHES[name]
        frames = int(round(punch["seconds"] * FPS))
        scene.timeline_markers.new(name, frame=start)
        stats = {"reach": 0.0}
        tightest_knee = 180.0
        straightest_arm = 0.0
        thrower = "l" if punch.get("mirrored") else "r"
        for i in range(frames):
            D, pos = solve(pose_at(punch, i / float(frames), stats))
            for side in ("l", "r"):
                thigh = pos["thigh_" + side] - pos["shin_" + side]
                shin = pos["foot_" + side] - pos["shin_" + side]
                tightest_knee = min(tightest_knee, math.degrees(thigh.angle(shin)))
            straightest_arm = max(straightest_arm, (pos["hand_" + thrower] - pos["arm_" + thrower]).length / arm)
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
            # The preview's step forward: the game moves the body; the export ignores this.
            rig.location = U(0.0, 0.0, carried(punch, i / float(FPS)))
            rig.keyframe_insert("location", frame=f)
        print("%-15s %d frames | the arm reaches %.2f of its length | tightest knee %.0f deg | furthest ankle from its hip %.2f m of a %.2f m leg"
              % (name, frames, straightest_arm, tightest_knee, stats["reach"], leg))
        spans[name] = (start, start + frames - 1, punch)
        start += frames
    scene.timeline_markers.new("end", frame=start)    # one past the last frame: the exporter's segments end before their end marker
    for fc in action.fcurves:
        for kp in fc.keyframe_points:
            kp.interpolation = 'LINEAR'
    scene.render.fps = FPS
    scene.frame_start = 0
    scene.frame_end = start - 1
    scene.frame_set(0)
    return spans
