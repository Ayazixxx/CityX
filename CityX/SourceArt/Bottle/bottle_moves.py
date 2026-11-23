# THE BOTTLE'S CARRY, AIM AND THROW AS FULL-BODY CLIPS (docs/GLASS_BOTTLE.md). Owner, 2026-11-23: "improve upon how the player holds it in hand and the
# throw", "make him carry it like the demoman", "hold right click to aim and throw it". Two clips, right-handed, the bottle by its neck in the fist with
# its body standing up out of the thumb's side all the way (BottleWeapon's hold):
#   * bottle_ready: its FIRST frame is the carry (the forearm up before the right of the chest, the bottle upright: the game holds the right arm there
#     whenever a bottle is in hand, PlayerLocomotion.RaisedCarry). From it the body turns side-on and sinks onto its back foot, the arm goes up and
#     back behind the head with the bottle laid back, the left hand comes up ahead: the aim, its last frame, held. Played backward it comes back down.
#   * bottle_throw: from the aim the body coils a little further (the tell), then the hips, the shoulders and the arm come through one after the other,
#     the hand over the shoulder and out ahead, where the bottle is let go; the arm carries on down across the body and the body stands again.
# The release is a moment the game keeps in step with this (BottlePoseDriver.ReleaseSeconds: the move's seconds times its RELEASE key's share).
#
# Needs the rig (../Octaslash/octaslash_rig.py), ../game_frames.py, octaslash_pose.py's solver (with OCTASLASH_NO_BAKE set), ../planted_feet.py,
# ../FistCombo/fist_pose.py (its key, sample, eased and body_pose), ../wrist_turn.py and ../head_carry.py loaded first; bottle_moves_build.py does all of it.
#
# Positions in the character's own frame (Unity axes: x right, y up, z forward, metres, the ground at y 0). The shoulders are at (+-0.35, 1.90).

import math


# THE CARRY: the elbow down by the ribs, the forearm out ahead and a little up, the fist before the right of the belly, thumb up: the bottle stands
# up out of it, leant back a little toward the shoulder, the wrist all but straight. The other arm hangs as it does in the walk.
CARRY = nodding(ikey((0.46, 1.48, 0.50), (-0.50, 1.04, 0.06), wrist=(0.0, -12.0, -10.0), fist_pole=(0.45, -1.0, -0.35)))

# THE AIM: side-on, the weight back, the throwing hand up behind the head with the elbow out to the side and the bottle laid back, the other hand up
# ahead, pointing where it will go.
AIM = ikey((0.78, 2.22, -0.30), (-0.26, 1.78, 0.66), wrist=(0.0, -10.0, -95.0), twist=52.0, hips=24.0, lean=-5.0, tilt=3.0, drop=0.10, shift=(0.02, -0.08),
           fist_pole=(1.0, -0.35, -0.5), off_pole=(-0.6, -0.8, 0.0))

# Coiled a little further before the throw: the tell.
COIL = ikey((0.74, 2.16, -0.46), (-0.22, 1.84, 0.70), wrist=(0.0, -5.0, -95.0), twist=62.0, hips=28.0, lean=-8.0, tilt=4.0, drop=0.12, shift=(0.03, -0.10),
            fist_pole=(1.0, -0.35, -0.5), off_pole=(-0.6, -0.8, 0.0))

# The release: the arm come over the shoulder and out ahead and up, nearly straight, the hips and the shoulders turned through, the weight on the front
# foot, the other hand snatched back to the ribs.
RELEASE = ikey((0.20, 2.44, 0.68), (-0.34, 1.50, -0.06), wrist=(0.0, -20.0, -130.0), twist=-34.0, hips=-14.0, lean=17.0, tilt=-3.0, drop=0.10,
               shift=(-0.02, 0.11), chin=3.0, fist_pole=(1.0, 0.2, -0.3), off_pole=(-0.2, -0.5, -1.0), control=(0.66, 2.62, 0.02), staged=True)

# The arm carried on down across the body after it, the body over the front foot.
FOLLOW = ikey((-0.24, 1.34, 0.62), (-0.46, 1.42, -0.34), wrist=(0.0, 0.0, -60.0), twist=-56.0, hips=-22.0, lean=27.0, tilt=-5.0, drop=0.13,
              shift=(-0.02, 0.12), chin=4.0, fist_pole=(0.9, -0.4, 0.2), off_pole=(-0.5, -0.3, -1.0))

# Standing again, the hands empty: what the game blends back to.
STAND = nodding(dict(READY, wrist=V(0.0, 0.0, 0.0)))

# The feet (planted_feet.py): for the aim the left one a little ahead, the right one back and out, side-on to the throw.
STANCE_LEAD, STANCE_REAR = (-STANCE_X, 0.0), (STANCE_X, 0.0)
LEAD_AIM, REAR_AIM = (-0.24, 0.26), (STANCE_X + 0.06, -0.18)

MOVES = {
    "bottle_ready": {
        "seconds": 0.30,
        "keys": [
            (0.00, CARRY, None),
            (1.00, AIM, "smooth"),
        ],
        "lead": [(0.10, 0.50, LEAD_AIM, 0.05)],
        "rear": [(0.52, 0.86, REAR_AIM, 0.04)],
        "held": (0.0, 1.01),
    },
    "bottle_throw": {
        "seconds": 0.55,
        "keys": [
            (0.00, AIM, None),
            (0.14, COIL, "smooth"),
            (0.36, RELEASE, "linear"),
            (0.58, FOLLOW, "slow_in"),
            (1.00, STAND, "smooth"),
        ],
        "start_lead": LEAD_AIM, "start_rear": REAR_AIM,
        # The back foot rolls up onto its ball as the hips come through, then both come back to the stance.
        "lead": [(0.66, 0.84, STANCE_LEAD, 0.05)],
        "rear": [(0.24, 0.40, (STANCE_X + 0.04, -0.04), 0.04), (0.70, 0.90, STANCE_REAR, 0.05)],
        "held": (0.0, 0.36),
    },
}
ORDER_OF_MOVES = ("bottle_ready", "bottle_throw")


def move_pose(move, t01, stats):
    s = sample(move["keys"], t01)
    seconds = t01 * move["seconds"]
    P, hips_at, body = body_pose(s, 1.0)
    for leg, steps, start in (("l", move["lead"], move.get("start_lead", STANCE_LEAD)), ("r", move["rear"], move.get("start_rear", STANCE_REAR))):
        hip_b = U(*hips_at) + body @ (HEAD["thigh_" + leg] - HEAD["hips"])
        timed = [(t0 * move["seconds"], t1 * move["seconds"], to, lift) for t0, t1, to, lift in steps]
        P[leg + "_leg"] = planted_leg(leg, to_unity(hip_b), timed, start, seconds, 0.0, hips_at.z, 28.0, stats)
    return P


def solved_at(move, t01, stats):
    D, pos = solve(move_pose(move, t01, stats))
    turn_wrist(D, pos, sample_wrist(move["keys"], t01))
    carry_head(D, pos, move["keys"], t01)
    return D, pos


def bake_moves():
    if rig.animation_data and rig.animation_data.action:
        old = rig.animation_data.action
        rig.animation_data.action = None
        bpy.data.actions.remove(old)
    rig.animation_data_create()
    action = bpy.data.actions.new("Bottle")
    rig.animation_data.action = action
    scene = bpy.context.scene
    scene.timeline_markers.clear()
    previous = {}
    start = 0
    spans = {}
    for name in ORDER_OF_MOVES:
        move = MOVES[name]
        frames = int(round(move["seconds"] * FPS))
        # The wind-up's last frame is the aim itself: baked onto it, not one short of it.
        onto_the_last = name == "bottle_ready"
        scene.timeline_markers.new(name, frame=start)
        stats = {"reach": 0.0}
        tightest_knee, tightest_elbow = 180.0, 180.0
        for i in range(frames):
            t01 = i / float(frames - 1) if onto_the_last else i / float(frames)
            D, pos = solved_at(move, t01, stats)
            for side in ("l", "r"):
                thigh = pos["thigh_" + side] - pos["shin_" + side]
                shin = pos["foot_" + side] - pos["shin_" + side]
                tightest_knee = min(tightest_knee, math.degrees(thigh.angle(shin)))
            upper = pos["arm_r"] - pos["forearm_r"]
            lower = pos["hand_r"] - pos["forearm_r"]
            tightest_elbow = min(tightest_elbow, math.degrees(upper.angle(lower)))
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
        print("%-14s %d frames | tightest knee %.0f deg, tightest throwing elbow %.0f deg | furthest ankle %.2f m" % (
            name, frames, tightest_knee, tightest_elbow, stats["reach"]))
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
