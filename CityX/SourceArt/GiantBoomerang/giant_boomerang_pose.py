# THE GIANT BOOMERANG'S MOVES AS FULL-BODY CLIPS (docs/BOOMERANG.md). Owner, 2026-11-12: "make a giant version of the boomerang", carried on the back, thrown heavy and slow, and
# "make an animation when you hold right click where it grabs the boomerang and does like an aiming it pose and aims in. also make it so you can press click and it throws it right
# away". Three clips, all thrown right-handed, side-arm and flat, the boomerang held near its upper end:
#   * giant_draw: the right hand goes up over the right shoulder to the grip on the back, pulls it off, and comes to the aim (its last frame is the aim held);
#   * giant_throw_aimed: from the aim, the hips, the shoulders and the arm unwind, the arm sweeps it round flat at shoulder height and lets it go in front;
#   * giant_throw_quick: from standing, over the shoulder for it and round into the throw in one go.
# The grab and the release are moments the game keeps in step with these (GiantBoomerangMoves: GrabShare, ReleaseShare, Seconds).
#
# Needs the rig (../Octaslash/octaslash_rig.py), octaslash_pose.py's solver (with OCTASLASH_NO_BAKE set), ../planted_feet.py and ../FistCombo/fist_pose.py (its key, sample, eased,
# and body_pose) loaded first; giant_boomerang_build.py does all of it.
#
# Positions in the character's own frame (Unity axes: x right, y up, z forward, metres, the ground at y 0). The shoulders are at (+-0.35, 1.90), the head's base at 2.25.

import math

# The right wrist on the grip behind the right shoulder, the elbow up and out: where the boomerang's grip rides on the back (GiantBoomerangItem.BackGrip).
GRAB, GRAB_POLE = (0.30, 2.42, -0.14), (0.9, 1.0, 0.2)

# THE AIM: turned side-on, the left arm out at what it is thrown at, the right hand cocked back by the right shoulder with the boomerang lying flat out behind it, the weight back.
AIM = key((0.80, 1.96, -0.46), (-0.22, 1.86, 0.86), twist=42.0, hips=18.0, lean=-2.0, tilt=3.0, drop=0.11, shift=(0.03, -0.05),
          fist_pole=(0.5, -0.8, -0.6), off_pole=(-0.6, -1.0, 0.0))

# The release: the arm swept round flat at shoulder height and straight out in front, the hips and the shoulders turned through, the left hand pulled in to the ribs.
RELEASE = key((-0.02, 1.86, 1.12), (-0.32, 1.50, -0.12), twist=-46.0, hips=-16.0, lean=12.0, tilt=-3.0, drop=0.12, shift=(-0.03, 0.06), chin=4.0,
              fist_pole=(0.4, -1.0, 0.2), off_pole=(-0.2, -0.6, -1.0), control=(0.80, 1.92, 0.40), staged=True)

# The arm carried on across after it, and the body round with it.
FOLLOW = key((-0.62, 1.58, 0.58), (-0.34, 1.42, -0.10), twist=-62.0, hips=-18.0, lean=14.0, tilt=-4.0, drop=0.12, shift=(-0.03, 0.06), chin=4.0,
             fist_pole=(0.2, -1.0, 0.4), off_pole=(-0.2, -0.6, -1.0))

STANCE_LEAD = (-STANCE_X, 0.0)
STANCE_REAR = (STANCE_X, 0.0)

MOVES = {
    "giant_draw": {
        "seconds": 0.45,
        "keys": [
            (0.00, READY, None),
            (0.42, key(GRAB, (-0.40, 1.30, 0.32), twist=8.0, hips=4.0, lean=-2.0, tilt=-4.0, drop=0.08, fist_pole=GRAB_POLE, off_pole=(-0.6, -1.0, 0.0)), "smooth"),
            (0.68, key((0.74, 2.25, -0.38), (-0.30, 1.62, 0.62), twist=28.0, hips=12.0, lean=-3.0, tilt=0.0, drop=0.10, shift=(0.02, -0.03),
                       fist_pole=(0.8, 0.4, -0.6), off_pole=(-0.6, -1.0, 0.0)), "smooth"),
            (1.00, AIM, "smooth"),
        ],
        # The left foot steps a little forward and out, side-on to the throw.
        "lead": [(0.20, 0.40, (-0.26, 0.24), 0.07)],
        "rear": [],
    },
    "giant_throw_aimed": {
        "seconds": 0.60,
        "keys": [
            (0.00, AIM, None),
            (0.30, RELEASE, "linear"),
            (0.48, FOLLOW, "slow_in"),
            (1.00, READY, "smooth"),
        ],
        "start_lead": (-0.26, 0.24),
        # The rear foot rolls up onto its ball as the hips turn through, and both come back to the stance after.
        "lead": [(0.40, 0.56, STANCE_LEAD, 0.06)],
        "rear": [(0.14, 0.30, (STANCE_X + 0.02, -0.04), 0.03), (0.44, 0.58, STANCE_REAR, 0.05)],
    },
    "giant_throw_quick": {
        "seconds": 0.75,
        "keys": [
            (0.00, READY, None),
            (0.22, key(GRAB, (-0.40, 1.34, 0.36), twist=10.0, hips=5.0, lean=-2.0, tilt=-4.0, drop=0.08, fist_pole=GRAB_POLE, off_pole=(-0.6, -1.0, 0.0)), "smooth"),
            (0.38, AIM, "smooth"),   # through the aim itself: cocked back, the boomerang flat out behind
            (0.55, RELEASE, "linear"),
            (0.68, FOLLOW, "slow_in"),
            (1.00, READY, "smooth"),
        ],
        "lead": [(0.12, 0.34, (-0.26, 0.24), 0.08), (0.66, 0.84, STANCE_LEAD, 0.06)],
        "rear": [(0.40, 0.55, (STANCE_X + 0.02, -0.04), 0.03), (0.62, 0.80, STANCE_REAR, 0.05)],
    },
}
ORDER_OF_MOVES = ("giant_draw", "giant_throw_aimed", "giant_throw_quick")


def move_pose(move, t01, stats):
    s = sample(move["keys"], t01)
    seconds = t01 * move["seconds"]
    P, hips_at, body = body_pose(s, 1.0)
    for leg, steps, start in (("l", move["lead"], move.get("start_lead", STANCE_LEAD)), ("r", move["rear"], STANCE_REAR)):
        hip_b = U(*hips_at) + body @ (HEAD["thigh_" + leg] - HEAD["hips"])
        timed = [(t0 * move["seconds"], t1 * move["seconds"], to, lift) for t0, t1, to, lift in steps]
        P[leg + "_leg"] = planted_leg(leg, to_unity(hip_b), timed, start, seconds, 0.0, hips_at.z, 28.0, stats)
    return P


def bake_moves():
    if rig.animation_data and rig.animation_data.action:
        old = rig.animation_data.action
        rig.animation_data.action = None
        bpy.data.actions.remove(old)
    rig.animation_data_create()
    action = bpy.data.actions.new("GiantBoomerang")
    rig.animation_data.action = action
    scene = bpy.context.scene
    scene.timeline_markers.clear()
    previous = {}
    start = 0
    spans = {}
    for name in ORDER_OF_MOVES:
        move = MOVES[name]
        frames = int(round(move["seconds"] * FPS))
        # The draw's last frame is the aim held: it is baked onto it, not one short of it.
        last_share = 1.0 if name == "giant_draw" else None
        scene.timeline_markers.new(name, frame=start)
        stats = {"reach": 0.0}
        tightest_knee = 180.0
        for i in range(frames):
            t01 = i / float(frames - 1) if last_share is not None else i / float(frames)
            D, pos = solve(move_pose(move, t01, stats))
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
        print("%-18s %d frames | tightest knee %.0f deg | furthest ankle %.2f m" % (name, frames, tightest_knee, stats["reach"]))
        spans[name] = (start, start + frames - 1, move)
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
