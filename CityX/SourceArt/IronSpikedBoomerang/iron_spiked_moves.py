# THE IRON SPIKED GIANT BOOMERANG'S OWN MOVES AS FULL-BODY CLIPS (docs/BOOMERANG.md). Owner, 2026-11-20: "I want to rework the grip on the throw for the
# spiked boomerang i want it to grab the boomerang by the rope ... then i want is grip while aiming in to be an above grip like how sasuke holds the demon
# wind shuriken in naruto ... thats how i want the throw to be", with the anime's throw (the arm held out wide behind, the whole body wound up, the arm
# whipped round flat and thrown out straight at what it is thrown at) and a game's copy of it, "imagine he isnt in mid air or spinning"; "also rework the
# catch and return to back animation ... with how the spiked boomerang sits on the back now". Four clips, right-handed, the palm on top of the wrap all
# the way (iron_spiked_hold.py):
#   * iron_draw: the right hand goes down and round behind the back to the wrap behind the right shoulder blade, takes it, and swings it out wide to the
#     right and back, flat, into the aim: sunk and leant forward, side-on, the arm stretched out behind with the boomerang lying under the palm, the left
#     hand out ahead. Its last frame is the aim, held. Played backward it is put back on the back.
#   * iron_throw_aimed: from the aim the body coils a little further, then the hips, the shoulders and the arm whip round, the arm sweeping it wide and
#     flat and thrown out straight ahead as it leaves, the left arm flung back; the arm carries on across the body.
#   * iron_throw_quick: from standing, behind the back for it, out through the aim and round into the throw in one go.
#   * iron_catch: the right hand out ahead and to the right for it, palm down, as it comes in; the arm swings back and round with its weight into the aim,
#     from where the game holds the aim or plays the draw backward.
# The grab, the release and the catch are moments the game keeps in step with these (GiantBoomerangMoves.IronSpiked: its Seconds).
#
# Needs the rig (../Octaslash/octaslash_rig.py), ../game_frames.py, iron_spiked_hold.py, octaslash_pose.py's solver (with OCTASLASH_NO_BAKE set), ../planted_feet.py,
# ../FistCombo/fist_pose.py (its key, sample, eased and body_pose), ../wrist_turn.py and ../head_carry.py loaded first; iron_spiked_moves_build.py does all of it.
#
# Positions in the character's own frame (Unity axes: x right, y up, z forward, metres, the ground at y 0). The shoulders are at (+-0.35, 1.90).
#
# THE WRIST (../wrist_turn.py). The solver's empty hand carries on its forearm's line, its palm turned with the elbow (the elbow behind an arm held out sideways: the
# palm down). A key's `wrist` turns the hand on the forearm after the solve, so the boomerang under the palm can lie flat whatever the arm's slope.

import math


# Behind the back for it: the right wrist below the wrap behind the right shoulder blade, the elbow out to the right and back, the palm to the boomerang.
GRAB = nodding(ikey((0.60, 1.36, -0.32), (-0.42, 1.24, 0.30), wrist=(0.0, 0.0, 0.0), twist=16.0, hips=6.0, lean=3.0, tilt=3.0, drop=0.08,
                    fist_pole=(1.0, -0.1, -0.2), off_pole=(-0.6, -1.0, 0.0)))

# THE AIM: sunk and leant forward, side-on, the right arm stretched out wide behind with the palm down on the wrap and the boomerang lying flat under it,
# the left hand out ahead and low.
AIM = ikey((1.02, 1.58, -0.66), (-0.46, 1.36, 0.62), wrist=(18.0, 0.0, 0.0), twist=46.0, hips=20.0, lean=16.0, tilt=5.0, drop=0.17, shift=(0.02, -0.05),
           fist_pole=(-0.7, 0.2, -0.7), off_pole=(-0.8, -0.6, -0.1))

# Coiled a little further before the throw: the tell.
COIL = ikey((0.94, 1.56, -0.82), (-0.38, 1.44, 0.68), wrist=(18.0, 0.0, 0.0), twist=58.0, hips=25.0, lean=14.0, tilt=6.0, drop=0.18, shift=(0.03, -0.07),
            fist_pole=(-0.7, 0.2, -0.7), off_pole=(-0.8, -0.6, -0.1))

# The release: the arm whipped round wide and flat and thrown out straight ahead, the hips and the shoulders turned through, the left arm flung back.
RELEASE = ikey((0.12, 1.70, 1.20), (-0.62, 1.52, -0.52), wrist=(8.0, 0.0, 0.0), twist=-50.0, hips=-22.0, lean=20.0, tilt=-4.0, drop=0.16,
               shift=(-0.03, 0.08), chin=4.0, fist_pole=(1.0, 0.15, -0.1), off_pole=(-0.5, 0.5, -0.8), control=(1.10, 1.66, 0.42), staged=True)

# The arm carried on across after it, the body round and down with it.
FOLLOW = ikey((-0.56, 1.46, 0.84), (-0.68, 1.62, -0.58), wrist=(0.0, 0.0, 0.0), twist=-68.0, hips=-26.0, lean=27.0, tilt=-6.0, drop=0.18,
              shift=(-0.03, 0.08), chin=5.0, fist_pole=(0.8, 0.5, 0.2), off_pole=(-0.5, 0.5, -0.8))

# Out for it as it comes home: the hand ahead and to the right at shoulder height, palm down.
REACH = ikey((0.62, 1.92, 0.80), (-0.38, 1.36, 0.28), wrist=(-6.0, 0.0, 0.0), twist=-12.0, hips=-4.0, lean=5.0, tilt=0.0, drop=0.10,
             fist_pole=(1.0, 0.25, -0.3), off_pole=(-0.6, -1.0, 0.0))

# Caught: the arm swung back and round with its weight, the body turning and sinking with it.
GIVE = ikey((1.06, 1.70, 0.02), (-0.40, 1.44, 0.52), wrist=(12.0, 0.0, 0.0), twist=22.0, hips=9.0, lean=9.0, tilt=3.0, drop=0.14, shift=(0.02, -0.02),
            fist_pole=(-0.2, 0.3, -1.0), off_pole=(-0.7, -0.8, 0.0))

# Standing with it on the back: the head carried ahead of it, as the idle carries it (../head_carry.py).
READY_I = nodding(dict(READY, wrist=V(0.0, 0.0, 0.0)))

# The feet (planted_feet.py): the left one ahead and out, the right one back, a wide base side-on to the throw.
LEAD_AIM, REAR_AIM = (-0.30, 0.34), (STANCE_X + 0.06, -0.20)
STANCE_LEAD, STANCE_REAR = (-STANCE_X, 0.0), (STANCE_X, 0.0)

MOVES = {
    "iron_draw": {
        "seconds": 0.50,
        "keys": [
            (0.00, READY_I, None),
            (0.40, GRAB, "smooth"),
            (0.72, ikey((0.98, 1.44, -0.50), (-0.44, 1.32, 0.50), wrist=(12.0, 0.0, 0.0), twist=34.0, hips=14.0, lean=9.0, tilt=5.0, drop=0.13,
                        shift=(0.02, -0.03), fist_pole=(0.2, 0.0, -1.0), off_pole=(-0.8, -0.7, -0.1)), "smooth"),
            (1.00, AIM, "smooth"),
        ],
        "lead": [(0.16, 0.40, LEAD_AIM, 0.07)],
        "rear": [(0.46, 0.66, REAR_AIM, 0.05)],
        "held": (0.40, 1.01),
    },
    "iron_throw_aimed": {
        "seconds": 0.70,
        "keys": [
            (0.00, AIM, None),
            (0.14, COIL, "smooth"),
            (0.36, RELEASE, "linear"),
            (0.54, FOLLOW, "slow_in"),
            (1.00, READY_I, "smooth"),
        ],
        "start_lead": LEAD_AIM, "start_rear": REAR_AIM,
        # The rear foot rolls up onto its ball as the hips turn through, then both come back to the stance.
        "lead": [(0.62, 0.80, STANCE_LEAD, 0.06)],
        "rear": [(0.22, 0.38, (STANCE_X + 0.04, -0.06), 0.04), (0.66, 0.84, STANCE_REAR, 0.05)],
        "held": (0.0, 0.36),
    },
    "iron_throw_quick": {
        "seconds": 0.85,
        "keys": [
            (0.00, READY_I, None),
            (0.22, GRAB, "smooth"),
            (0.44, COIL, "smooth"),
            (0.60, RELEASE, "linear"),
            (0.74, FOLLOW, "slow_in"),
            (1.00, READY_I, "smooth"),
        ],
        "lead": [(0.08, 0.26, LEAD_AIM, 0.08), (0.74, 0.90, STANCE_LEAD, 0.06)],
        "rear": [(0.28, 0.42, REAR_AIM, 0.04), (0.48, 0.60, (STANCE_X + 0.04, -0.06), 0.04), (0.78, 0.94, STANCE_REAR, 0.05)],
        "held": (0.22, 0.60),
    },
    "iron_catch": {
        "seconds": 0.50,
        "keys": [
            (0.00, READY_I, None),
            (0.27, REACH, "smooth"),
            (0.58, GIVE, "slow_in"),
            (1.00, AIM, "smooth"),
        ],
        "lead": [(0.34, 0.60, LEAD_AIM, 0.06)],
        "rear": [(0.64, 0.84, REAR_AIM, 0.05)],
        "held": (0.27, 1.01),
    },
}
ORDER_OF_MOVES = ("iron_draw", "iron_throw_aimed", "iron_throw_quick", "iron_catch")


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
    action = bpy.data.actions.new("IronSpikedBoomerang")
    rig.animation_data.action = action
    scene = bpy.context.scene
    scene.timeline_markers.clear()
    previous = {}
    start = 0
    spans = {}
    for name in ORDER_OF_MOVES:
        move = MOVES[name]
        frames = int(round(move["seconds"] * FPS))
        # The draw's and the catch's last frames are the aim itself: baked onto it, not one short of it.
        last_share = 1.0 if name in ("iron_draw", "iron_catch") else None
        scene.timeline_markers.new(name, frame=start)
        stats = {"reach": 0.0}
        tightest_knee = 180.0
        for i in range(frames):
            t01 = i / float(frames - 1) if last_share is not None else i / float(frames)
            D, pos = solved_at(move, t01, stats)
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


def grab_report():
    """At each grab, how far the boomerang as the hand holds it is from the boomerang as it hangs on the back: the game glides it across the difference
    (GiantBoomerangItem), so the less the better. Also where the wrist would have to be, and how the hand turned, for none."""
    for name, share in (("iron_draw", 0.40), ("iron_throw_quick", 0.22)):
        D, pos = solved_at(MOVES[name], share, {"reach": 0.0})
        hand = unity_frame(D, pos, "hand_r", HAND_REST)
        spine = unity_frame(D, pos, "spine", SPINE_REST)
        hangs = spine @ BACK_HANG @ BACK_MODEL
        held = hand @ IRON_HOLD
        wanted = hangs @ IRON_HOLD.inverted()
        off = (held.to_translation() - hangs.to_translation()).length
        turned = math.degrees(held.to_quaternion().rotation_difference(hangs.to_quaternion()).angle)
        w = wanted.to_translation()
        local = (hand.to_quaternion().inverted() @ wanted.to_quaternion())
        axis, angle = local.to_axis_angle()
        print("%-17s grab: held %.2f m and %.0f deg from where it hangs | the wrist is at (%.2f, %.2f, %.2f), would be at (%.2f, %.2f, %.2f), the hand turned %.0f deg about (%.2f, %.2f, %.2f) of its own"
              % (name, off, turned, hand.to_translation().x, hand.to_translation().y, hand.to_translation().z, w.x, w.y, w.z, math.degrees(angle), axis.x, axis.y, axis.z))


# THE GRAB PUT EXACTLY ON THE BOOMERANG: where the hand's hold of it is the boomerang as it hangs on the back, so it is taken off the back, and laid back on it, without a
# jump (the hand is between the back and the boomerang there: how its wrist is turned does not show).
fit_hand(GRAB, lambda spine: spine @ BACK_HANG @ BACK_MODEL @ IRON_HOLD.inverted())
