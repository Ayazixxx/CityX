# THE PARACHUTE'S POSES (docs/PARACHUTE.md; owner, 2026-11-19: "make a parachute with good parachuting mechanics ... make it look and feel really good"). Whole-body poses
# the game blends between as the keys move (ParachuteRider), each a short held segment but the pull:
#   * skydive_box: spread face down in the box a skydiver falls in: the back arched, the arms out with the elbows bent and the hands by the head, the knees bent and the
#     shins up; skydive_track: the dive (W): the arms swept back along the sides, the legs long, the head low;
#   * chute_pull: from the box, the right hand goes back to the pilot chute's pouch at the bottom of the pack and throws it out to the side, the left arm reaching forward
#     to keep the body flat (the game then swings the body upright under the opening canopy and brings the hands up to the toggles);
#   * chute_hang: under the canopy, sitting a little back in the harness, the hands up on the toggles over the shoulders, the legs hanging;
#   * chute_left, chute_right: one toggle pulled down to the chest (a turn); chute_flare: both down by the hips, the legs out together for the landing;
#     chute_dive: the hands on the front risers, pulled down before the face (W).
# The skydive poses are made standing: the game lays the body face down (BakedPose.Turn), so here "up" is the way the head goes, "forward" is the ground and "back" the sky.
#
# Needs the rig (../Octaslash/octaslash_rig.py) and octaslash_pose.py's solver (with OCTASLASH_NO_BAKE set) loaded first; parachute_build.py does both.
#
# Positions in the character's own frame (Unity axes: x right, y up, z forward, metres). The shoulders are at (+-0.35, 1.90), the hips at 0.90; the upper arm is 0.52 m long,
# the forearm 0.37, the thigh and the shin 0.38 each. An arm or a leg is written as the way each of its two bones points, in the chest's (the hips') frame.

import math

FPS = 60
UPPER_ARM, FOREARM = 0.523, 0.366
THIGH, SHIN = 0.383, 0.382


def _unit(v):
    length = math.sqrt(sum(c * c for c in v))
    return tuple(c / length for c in v)


def arm(upper, fore, mirror=False):
    """An arm from the way its upper arm and forearm point, in the chest's frame, the right one (mirror: the left)."""
    u, f = _unit(upper), _unit(fore)
    s = -1.0 if mirror else 1.0
    hand = tuple(u[i] * UPPER_ARM + f[i] * FOREARM for i in range(3))
    return {"hand": (hand[0] * s, hand[1], hand[2]), "pole": (u[0] * s, u[1], u[2]), "space": "chest"}


def reach(hand, pole, mirror=False):
    """An arm by where its wrist is from the shoulder, in the chest's frame, and the way the elbow sticks out."""
    s = -1.0 if mirror else 1.0
    return {"hand": (hand[0] * s, hand[1], hand[2]), "pole": (pole[0] * s, pole[1], pole[2]), "space": "chest"}


def leg(thigh, shin, toe, mirror=False):
    """A leg from the way the thigh and the shin point and the toes, in the hips' frame, the right one (mirror: the left)."""
    t, k = _unit(thigh), _unit(shin)
    s = -1.0 if mirror else 1.0
    foot = tuple(t[i] * THIGH + k[i] * SHIN for i in range(3))
    return {"foot": (foot[0] * s, foot[1], foot[2]), "knee": (t[0] * s, t[1], t[2]), "toe": (toe[0] * s, toe[1], toe[2]), "space": "hips"}


def pose(hips, body, chest, head, right, left, r_leg, l_leg):
    """A whole pose: where the hips are, the hips', the chest's and the head's pitch forward (degrees), the arms and the legs."""
    return {"hips": hips, "body": body, "chest": chest, "head": head, "right": right, "free": left, "r_leg": r_leg, "l_leg": l_leg}


# --- in the air, face down (made standing: up = where the head goes, forward = the ground, back = the sky) ---

BOX_LEG_R = leg((0.25, -1.0, -0.05), (0.0, -0.35, -1.0), (0.0, -0.5, -0.85))
BOX = pose((0.0, 0.90, 0.05), -8.0, -10.0, -30.0,
           arm((1.0, 0.30, 0.0), (-0.15, 1.0, 0.15)), arm((1.0, 0.30, 0.0), (-0.15, 1.0, 0.15), mirror=True),
           BOX_LEG_R, leg((0.25, -1.0, -0.05), (0.0, -0.35, -1.0), (0.0, -0.5, -0.85), mirror=True))

TRACK = pose((0.0, 0.90, 0.0), 5.0, 8.0, -20.0,
             arm((0.35, -1.0, -0.15), (0.10, -1.0, -0.05)), arm((0.35, -1.0, -0.15), (0.10, -1.0, -0.05), mirror=True),
             leg((0.06, -1.0, -0.05), (0.02, -1.0, -0.12), (0.0, -1.0, -0.25)), leg((0.06, -1.0, -0.05), (0.02, -1.0, -0.12), (0.0, -1.0, -0.25), mirror=True))

# The pull: the right hand back to the pouch at the bottom of the pack (behind the right hip, on the sky side), and out to the side; the left arm forward of the head.
LEFT_FORWARD = arm((0.35, 1.0, -0.10), (0.10, 1.0, -0.05), mirror=True)
AT_THE_POUCH = reach((-0.10, -0.78, -0.30), (1.0, 0.0, 0.2))
GRIPPED = reach((-0.08, -0.80, -0.36), (1.0, 0.0, 0.3))
THROWN = reach((0.86, 0.05, -0.22), (0.0, -1.0, 0.0))

# --- under the canopy, hanging (made as it hangs) ---

TOGGLE_UP = arm((0.75, 0.35, 0.25), (-0.45, 1.0, -0.10))
PULLED_TO_THE_CHEST = reach((0.06, -0.30, 0.40), (1.0, -0.5, -0.3))
HANG_LEG_R = leg((0.08, -1.0, 0.28), (0.0, -1.0, -0.20), (0.0, -0.55, 0.85))
HANG_LEG_L = leg((0.08, -1.0, 0.28), (0.0, -1.0, -0.20), (0.0, -0.55, 0.85), mirror=True)
HANG = pose((0.0, 0.90, 0.0), -4.0, -3.0, 4.0, TOGGLE_UP, arm((0.75, 0.35, 0.25), (-0.45, 1.0, -0.10), mirror=True), HANG_LEG_R, HANG_LEG_L)
LEFT_TURN = pose((0.0, 0.90, 0.0), -4.0, -1.0, 4.0, TOGGLE_UP, reach((0.06, -0.30, 0.40), (1.0, -0.5, -0.3), mirror=True), HANG_LEG_R, HANG_LEG_L)
RIGHT_TURN = pose((0.0, 0.90, 0.0), -4.0, -1.0, 4.0, PULLED_TO_THE_CHEST, arm((0.75, 0.35, 0.25), (-0.45, 1.0, -0.10), mirror=True), HANG_LEG_R, HANG_LEG_L)
FLARE = pose((0.0, 0.90, 0.0), 4.0, 6.0, 8.0,
             reach((0.08, -0.80, 0.22), (1.0, 0.0, -0.5)), reach((0.08, -0.80, 0.22), (1.0, 0.0, -0.5), mirror=True),
             leg((0.05, -0.80, 0.60), (0.0, -1.0, -0.15), (0.0, -0.2, 1.0)), leg((0.05, -0.80, 0.60), (0.0, -1.0, -0.15), (0.0, -0.2, 1.0), mirror=True))
DIVE = pose((0.0, 0.90, 0.0), 0.0, 4.0, 2.0,
            reach((-0.04, 0.02, 0.44), (1.0, -1.0, 0.0)), reach((-0.04, 0.02, 0.44), (1.0, -1.0, 0.0), mirror=True), HANG_LEG_R, HANG_LEG_L)

# name: (seconds, [(share, pose)]): a held pose is one key.
MOVES = {
    "skydive_box": (2.0 / FPS, [(0.0, BOX)]),
    "skydive_track": (2.0 / FPS, [(0.0, TRACK)]),
    "chute_pull": (0.30, [
        (0.00, BOX),
        (0.45, pose((0.0, 0.90, 0.05), -8.0, -8.0, -30.0, AT_THE_POUCH, LEFT_FORWARD, BOX_LEG_R, BOX["l_leg"])),
        (0.65, pose((0.0, 0.90, 0.05), -8.0, -8.0, -30.0, GRIPPED, LEFT_FORWARD, BOX_LEG_R, BOX["l_leg"])),
        (1.00, pose((0.0, 0.90, 0.05), -8.0, -10.0, -30.0, THROWN, LEFT_FORWARD, BOX_LEG_R, BOX["l_leg"])),
    ]),
    "chute_hang": (2.0 / FPS, [(0.0, HANG)]),
    "chute_left": (2.0 / FPS, [(0.0, LEFT_TURN)]),
    "chute_right": (2.0 / FPS, [(0.0, RIGHT_TURN)]),
    "chute_flare": (2.0 / FPS, [(0.0, FLARE)]),
    "chute_dive": (2.0 / FPS, [(0.0, DIVE)]),
}
ORDER_OF_MOVES = ("skydive_box", "skydive_track", "chute_pull", "chute_hang", "chute_left", "chute_right", "chute_flare", "chute_dive")


def _lerp(a, b, u):
    return tuple(a[i] + (b[i] - a[i]) * u for i in range(len(a)))


def _mix_limb(a, b, u):
    out = dict(a)
    for k in ("hand", "pole", "foot", "knee", "toe"):
        if k in a:
            out[k] = _lerp(a[k], b[k], u)
    return out


def _mix(a, b, u):
    return {"hips": _lerp(a["hips"], b["hips"], u), "body": a["body"] + (b["body"] - a["body"]) * u, "chest": a["chest"] + (b["chest"] - a["chest"]) * u,
            "head": a["head"] + (b["head"] - a["head"]) * u,
            "right": _mix_limb(a["right"], b["right"], u), "free": _mix_limb(a["free"], b["free"], u),
            "r_leg": _mix_limb(a["r_leg"], b["r_leg"], u), "l_leg": _mix_limb(a["l_leg"], b["l_leg"], u)}


def pose_at(keys, t01):
    i = 0
    while i < len(keys) - 2 and t01 >= keys[i + 1][0]:
        i += 1
    if len(keys) == 1:
        return keys[0][1]
    ta, a = keys[i]
    tb, b = keys[i + 1]
    u = max(0.0, min(1.0, (t01 - ta) / max(tb - ta, 1e-4)))
    return _mix(a, b, u * u * (3.0 - 2.0 * u))


def solver_pose(p):
    """A pose as octaslash_pose.solve takes it."""
    body = pitch(p["body"])
    chest = body @ pitch(p["chest"] - p["body"])
    head = chest @ pitch(p["head"] - p["chest"])
    neck = chest.slerp(head, 0.5)
    return {"hips": p["hips"], "body": body, "spine": body.inverted() @ chest, "neck": chest.inverted() @ neck, "head": neck.inverted() @ head,
            "right": p["right"], "free": p["free"], "r_leg": p["r_leg"], "l_leg": p["l_leg"]}


def bake_moves():
    if rig.animation_data and rig.animation_data.action:
        old = rig.animation_data.action
        rig.animation_data.action = None
        bpy.data.actions.remove(old)
    rig.animation_data_create()
    action = bpy.data.actions.new("Parachute")
    rig.animation_data.action = action
    scene = bpy.context.scene
    scene.timeline_markers.clear()
    previous = {}
    start = 0
    spans = {}
    for name in ORDER_OF_MOVES:
        seconds, keys = MOVES[name]
        frames = max(2, int(round(seconds * FPS)))
        scene.timeline_markers.new(name, frame=start)
        for i in range(frames):
            D, pos = solve(solver_pose(pose_at(keys, i / float(frames - 1))))
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
        print("%-14s %d frames" % (name, frames))
        spans[name] = (start, start + frames - 1)
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
