# THE GIANT BOOMERANG'S MOVES AS FULL-BODY CLIPS (docs/BOOMERANG.md). Owner, 2026-11-12: "make a giant version of the boomerang", carried on the back, thrown heavy and slow, and
# "make an animation when you hold right click where it grabs the boomerang and does like an aiming it pose and aims in. also make it so you can press click and it throws it right
# away". Remade 2026-11-20 from the owner's clip of a fighting game's boomerang thrower (Downloads/jeans.mp4, 18 frames: "copy these movements the best you can for the regular
# giant boomerang throw. there is a bit that is cut out because she does a spin but i trust you can smoothly transition in between positions ... i like the way she grabs it off
# her back as well"). What she does, read frame by frame: the right hand goes up over the shoulder to the top of it on her back and pulls it up and over, its tail swinging out
# behind, until it lies level over the shoulder, its front end by the head (a javelin's carry); [the spin, cut]; a deep lunge, the left arm straight out at what it is thrown
# at, the right arm back and low with it trailing level behind; the arm whipped round and up, let go stretched out ahead at head height, the left arm flung back, the lunge held.
# Four clips, thrown right-handed:
#   * giant_draw: over the shoulder for it, up and over into the carry, then (where she spins) swung down and back into the lunge, which is the aim (its last frame, held);
#   * giant_throw_aimed: from the lunge the arm whips round and up and lets it go ahead; the lunge is held, then the feet come back under the body;
#   * giant_throw_quick: from standing, all of it in one go;
#   * giant_catch (owner, 2026-11-12: "on the return of the boomerang ... he actually catches it and puts it back or just holds it in his hand if you holding right
#     click"): the right hand out ahead and to the right for it, palm up, as it comes in, the arm and the body giving with its weight, and on down into the aim; from there
#     the game holds the aim or plays the draw backward to put it on the back.
# The grab, the release and the catch are moments the game keeps in step with these (GiantBoomerangMoves.Bone: its Seconds).
#
# Needs the rig (../Octaslash/octaslash_rig.py), octaslash_pose.py's solver (with OCTASLASH_NO_BAKE set), ../planted_feet.py, ../FistCombo/fist_pose.py (its key, sample, eased,
# and body_pose) and ../head_carry.py loaded first; giant_boomerang_build.py does all of it.
#
# Positions in the character's own frame (Unity axes: x right, y up, z forward, metres, the ground at y 0). The shoulders are at (+-0.35, 1.90), the head's base at 2.25.
#
# THE RIGHT HAND HOLDS IT BY ITS SIDE, PALM UP (owner, 2026-11-20: "make it so he holds it by the side instead of his fingers clipping through"; GiantBoomerangItem's grip): the
# palm under its inner edge at the leather, the thumb over the edge, its length running on from the forearm. The hand is one with its forearm on this rig and its palm faces
# square to the arm's bend, so the palm is up, and the boomerang level, while the elbow keeps to the same side of the hand all the way round: out to the right of an arm held
# back, ahead of one held out to the side, inside one thrown out ahead. Every pole below is chosen for that.

import math

# Standing with it on the back: the head carried ahead of it, as the idle carries it (../head_carry.py).
STAND = nodding(READY)

# Over the shoulder for it: the right hand behind the right shoulder on the grip where it rides on the back (GiantBoomerangItem.OnTheBack), the elbow out to the right, the body
# leant a little forward and the chin down, away from it (owner, 2026-11-20, of the head leant back into it as the hand went up: "watch out for clipping through the head").
# The wrist is as far back as the boomerang lies on the back: held nearer the neck, it came into the hand through the back of the head.
GRAB = nodding(key((0.30, 2.46, -0.45), (-0.40, 1.30, 0.32), twist=8.0, hips=4.0, lean=4.0, tilt=-2.0, drop=0.08, chin=4.0, fist_pole=(1.0, 0.6, -0.2),
                   off_pole=(-0.6, -1.0, 0.0)))

# The carry: pulled up and over, level beside the head over the right shoulder, the hand out past the shoulder with its palm up under it and the elbow ahead of the hand, the
# left hand coming up toward what it is thrown at. (She carries it with her hand by her head; on this rig a hand that high over its shoulder cannot turn its palm up.)
CARRY = nodding(key((0.88, 1.98, -0.46), (-0.30, 1.70, 0.78), twist=16.0, hips=6.0, lean=2.0, tilt=0.0, drop=0.08, shift=(0.0, 0.0),
                    fist_pole=(0.2, 0.1, 1.0), off_pole=(-0.6, -1.0, 0.0)), NOD * 0.5)

# THE AIM: the lunge, sunk and leant forward, side-on, the left arm straight out at what it is thrown at, the right arm back and low with the boomerang trailing level behind.
AIM = key((0.45, 1.38, -0.70), (-0.17, 1.50, 1.46), twist=50.0, hips=22.0, lean=24.0, tilt=4.0, drop=0.30, shift=(0.0, 0.10),
          fist_pole=(1.0, 0.0, 0.3), off_pole=(-0.6, -1.0, 0.0))

# The release: the arm whipped round wide and up and stretched out ahead at head height, the hips and the shoulders turned through, the left arm flung back, the lunge held.
RELEASE = key((0.18, 1.80, 1.62), (-0.56, 1.24, -0.34), twist=-40.0, hips=-18.0, lean=28.0, tilt=-3.0, drop=0.28, shift=(-0.02, 0.16), chin=4.0,
              fist_pole=(-1.0, 0.0, 0.3), off_pole=(-0.4, 0.4, -0.8), control=(1.08, 1.42, 0.36), staged=True)

# The arm carried on across and down after it, still in the lunge.
FOLLOW = key((-0.30, 1.42, 1.28), (-0.62, 1.30, -0.44), twist=-52.0, hips=-22.0, lean=30.0, tilt=-4.0, drop=0.28, shift=(-0.02, 0.16), chin=4.0,
             fist_pole=(0.2, -1.0, 0.4), off_pole=(-0.4, 0.4, -0.8))

# The feet (planted_feet.py): the lunge, the left foot far ahead and the right one as far behind as the leg reaches.
STANCE_LEAD = (-STANCE_X, 0.0)
STANCE_REAR = (STANCE_X, 0.0)
LEAD_LUNGE, REAR_LUNGE = (-0.22, 0.62), (0.26, -0.50)

MOVES = {
    "giant_draw": {
        "seconds": 0.52,
        "keys": [
            (0.00, STAND, None),
            (0.35, GRAB, "smooth"),
            (0.60, CARRY, "smooth"),
            (1.00, AIM, "smooth"),
        ],
        "lead": [(0.50, 0.78, LEAD_LUNGE, 0.08)],
        "rear": [(0.70, 0.95, REAR_LUNGE, 0.05)],
    },
    "giant_throw_aimed": {
        "seconds": 0.65,
        "keys": [
            (0.00, AIM, None),
            (0.28, RELEASE, "linear"),
            (0.50, FOLLOW, "slow_in"),
            (1.00, STAND, "smooth"),
        ],
        "start_lead": LEAD_LUNGE, "start_rear": REAR_LUNGE,
        "lead": [(0.68, 0.88, STANCE_LEAD, 0.06)],
        "rear": [(0.60, 0.80, STANCE_REAR, 0.05)],
    },
    "giant_throw_quick": {
        "seconds": 0.80,
        "keys": [
            (0.00, STAND, None),
            (0.16, GRAB, "smooth"),
            (0.30, CARRY, "smooth"),
            (0.44, AIM, "smooth"),   # through the lunge itself: sunk, the boomerang back and low
            (0.56, RELEASE, "linear"),
            (0.70, FOLLOW, "slow_in"),
            (1.00, STAND, "smooth"),
        ],
        "lead": [(0.18, 0.38, LEAD_LUNGE, 0.08), (0.80, 0.94, STANCE_LEAD, 0.06)],
        "rear": [(0.28, 0.44, REAR_LUNGE, 0.05), (0.74, 0.90, STANCE_REAR, 0.05)],
    },
    "giant_catch": {
        "seconds": 0.45,   # 0.60 at first; owner, 2026-11-12: "id like it a bit faster"
        "keys": [
            (0.00, STAND, None),
            # Out for it, the hand open ahead and to the right at shoulder height, palm up, the body turned a little toward it.
            (0.30, key((0.62, 1.98, 0.78), (-0.36, 1.40, 0.30), twist=-12.0, hips=-4.0, lean=3.0, tilt=0.0, drop=0.09,
                       fist_pole=(-1.0, 0.0, 0.3), off_pole=(-0.6, -1.0, 0.0)), "smooth"),
            # Caught: the arm gives with its weight, back and round past the shoulder, the body turning and sinking with it.
            (0.55, key((0.96, 1.70, -0.05), (-0.30, 1.56, 0.90), twist=24.0, hips=10.0, lean=10.0, tilt=2.0, drop=0.18, shift=(0.0, 0.04),
                       fist_pole=(-0.3, -0.2, 1.0), off_pole=(-0.6, -1.0, 0.0)), "slow_in"),
            (1.00, AIM, "smooth"),
        ],
        "lead": [(0.36, 0.62, LEAD_LUNGE, 0.07)],
        "rear": [(0.62, 0.90, REAR_LUNGE, 0.05)],
    },
}
ORDER_OF_MOVES = ("giant_draw", "giant_throw_aimed", "giant_throw_quick", "giant_catch")


def move_pose(move, t01, stats):
    s = sample(move["keys"], t01)
    seconds = t01 * move["seconds"]
    P, hips_at, body = body_pose(s, 1.0)
    for leg, steps, start in (("l", move["lead"], move.get("start_lead", STANCE_LEAD)), ("r", move["rear"], move.get("start_rear", STANCE_REAR))):
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
        # The draw's and the catch's last frames are the aim itself: baked onto it, not one short of it.
        last_share = 1.0 if name in ("giant_draw", "giant_catch") else None
        scene.timeline_markers.new(name, frame=start)
        stats = {"reach": 0.0}
        tightest_knee = 180.0
        for i in range(frames):
            t01 = i / float(frames - 1) if last_share is not None else i / float(frames)
            D, pos = solve(move_pose(move, t01, stats))
            carry_head(D, pos, move["keys"], t01)
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
