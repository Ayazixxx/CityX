# THE CHAIN'S ATTACKS AS FULL-BODY CLIPS. Owner, 2026-11-22, with a video of four games' clips: "i want our main left click combo to be two alternating diagonal swing
# animations that play consecutively just like delsin's" (inFamous Second Son), one-handed, the body moving as Dark Souls' whip swings do ("use these dark souls clips to
# figure out how the body should move one hand swinging ... make the combo start with right to left"); a swing in mid air that "looks like how the hxh 2d fighter clip does";
# and a downward one for whoever is "ragdolled or downed or dead" (docs/CHAIN.md has what each clip shows, frame by frame).
#
# The chain itself is not here: the game hangs it from the hand and swings it (Weapons/Melee/ChainWeapon, ChainLash). What is made here is the body that throws it, arms
# and all, on the game's skeleton, to the game's own timing (SwordStrikes.TimingOf) and step (SwordStrikes.StepMetres): keep them in step.
#   * the lash: the hand taken up and back over the right shoulder, the body arched and coiled after it and the other arm out ahead; a long stride onto the left foot, and
#     the hips, the shoulders and last the arm come over the top and down across to the lower left, the body folding after it into a deep lunge, the other arm flung back;
#   * the backhand: the hand taken up over the left shoulder, the shoulders turned away; the right foot strides through and the arm comes over and down to the lower right;
#   * in the air: the whole body arched back with the hand behind the head and the heels drawn up, then jack-knifed forward, the arm brought straight down ahead, one knee
#     tucked up and the other leg trailing;
#   * on someone down: the hand raised straight up behind the head, the body leaning back and the other hand pointing at them; then everything bent down over the front
#     knee, the arm driven at the ground ahead.
#
# Needs the rig (../Octaslash/octaslash_rig.py), octaslash_pose.py's solver (with OCTASLASH_NO_BAKE set), ../planted_feet.py and ../FistCombo/fist_pose.py (its keys, its
# sampling and its body: a chain's blow is keyed as a punch is, the wrists as places in the character's own frame) loaded first; chain_build.py does all of it.
#
# Poses are in the character's own frame (Unity axes: x right, y up, z forward, metres, the feet's ground at y 0). The FEET are written on the stage (planted_feet.py),
# which the game's step carries the body over. In the air the legs are written from the hip joints, in the hips' own frame.

import bpy
import math
from mathutils import Vector

CHAIN_STANCE_X = 0.20

# The hands on their way back down after a blow: loosely before the body, clear of the hips.
HAND_BACK, HAND_BACK_POLE = (0.40, 1.10, 0.40), (0.6, -0.6, -0.6)
OFF_HAND_BACK, OFF_HAND_BACK_POLE = (-0.40, 1.08, 0.30), (-0.6, -0.6, -0.6)


def phases(wind, hold, strike, follow, settle, recover):
    """An attack's moments in seconds, from its phases' lengths (SwordStrikes.StrikeTiming)."""
    return {"timing": (wind, hold, strike, follow, settle, recover), "wound": wind, "held": wind + hold, "contact": wind + hold + strike,
            "followed": wind + hold + strike + follow, "settled": wind + hold + strike + follow + settle, "seconds": wind + hold + strike + follow + settle + recover}


def swing(timing, step, wound, held, contact, followed, back, left, right, ready=None):
    """A blow from its phases, how far the game steps the body, and its keys: wound up, the hold's end, the contact, the follow-through's end (held through the settle),
    a moment on the way back, and the feet on the stage."""
    t = phases(*timing)
    ready = ready if ready is not None else READY
    return dict(t, step=step, left=left, right=right, keys=[
        (0.0, ready, None),
        (t["wound"], wound, "smooth"),
        (t["held"], held, "linear"),
        (t["contact"], contact, "linear"),
        (t["followed"], followed, "slow_in"),
        (t["settled"], followed, "linear"),
        (t["settled"] + (t["seconds"] - t["settled"]) * 0.55, back, "smooth"),
        (t["seconds"], ready, "smooth"),
    ])


def legs(left, right, pitch=0.0):
    """A key's legs in the air and the whole body's pitch forward: each leg (the ankle from its hip joint, which way the knee points, which way the toes point), in the
    hips' own frame."""
    return {"pitch": pitch, "l_foot": V(*left[0]), "l_knee": V(*left[1]), "l_toe": V(*left[2]), "r_foot": V(*right[0]), "r_knee": V(*right[1]), "r_toe": V(*right[2])}


# Falling: the legs hanging a little bent, one a little ahead of the other.
FALLING = legs(((-0.03, -0.70, 0.08), (-0.1, 0.2, 1.0), (-0.05, -0.45, 1.0)), ((0.03, -0.69, -0.06), (0.1, 0.2, 1.0), (0.05, -0.5, 1.0)))
READY_AIR = dict(key((0.52, 1.10, 0.10), (-0.52, 1.10, 0.10), lean=4.0, drop=0.0), **FALLING)

CHAIN_MOVES = {
    # THE LASH: from over the right shoulder down to the lower left.
    "chain_lash": swing(
        (0.20, 0.05, 0.12, 0.08, 0.10, 0.25), 0.45,
        key((0.55, 2.45, -0.75), (-0.10, 1.65, 0.55), twist=48.0, hips=22.0, lean=-9.0, tilt=-5.0, drop=0.10, shift=(0.06, -0.10),
            fist_pole=(1.0, 0.0, -0.3), off_pole=(-0.7, -1.0, 0.1), control=(1.00, 1.60, -0.15)),
        key((0.57, 2.48, -0.80), (-0.08, 1.68, 0.58), twist=52.0, hips=24.0, lean=-11.0, tilt=-5.0, drop=0.11, shift=(0.06, -0.11),
            fist_pole=(1.0, 0.0, -0.3), off_pole=(-0.7, -1.0, 0.1)),
        key((-0.06, 1.23, 1.28), (-0.62, 1.30, -0.30), twist=-28.0, hips=-34.0, lean=30.0, tilt=9.0, drop=0.21, shift=(-0.07, 0.08), chin=8.0,
            fist_pole=(0.6, -1.0, 0.0), off_pole=(-0.3, 0.2, -1.0), control=(0.62, 2.30, 0.35), staged=True),
        key((-0.42, 0.92, 1.00), (-0.66, 1.35, -0.38), twist=-58.0, hips=-44.0, lean=34.0, tilt=8.0, drop=0.25, shift=(-0.09, 0.07), chin=8.0,
            fist_pole=(0.4, 0.2, 0.6), off_pole=(-0.3, 0.2, -1.0)),
        key(HAND_BACK, OFF_HAND_BACK, twist=-22.0, hips=-14.0, lean=12.0, drop=0.14, shift=(-0.02, 0.04), fist_pole=HAND_BACK_POLE, off_pole=OFF_HAND_BACK_POLE),
        left=[(0.08, 0.30, (-0.22, 0.58), 0.13)],
        right=[(0.48, 0.70, (CHAIN_STANCE_X, 0.45), 0.05)]),
    # THE BACKHAND: from over the left shoulder down to the lower right. The other arm does nothing: it stays before the body, its elbow a little bent (owner,
    # 2026-11-22, of it swung back behind the hip: "this weird bend backward ... just have it maybe elbow bent a bit but forward").
    "chain_backhand": swing(
        (0.20, 0.05, 0.12, 0.08, 0.10, 0.25), 0.40,
        key((-0.42, 1.86, 0.34), (-0.42, 1.22, 0.12), twist=-62.0, hips=-28.0, lean=-6.0, tilt=-4.0, drop=0.12, shift=(-0.06, -0.09),
            fist_pole=(0.3, -1.0, 0.6), off_pole=(-0.6, -1.0, -0.3), control=(0.30, 1.55, 0.80)),
        key((-0.46, 1.88, 0.30), (-0.42, 1.22, 0.12), twist=-66.0, hips=-30.0, lean=-7.0, tilt=-4.0, drop=0.13, shift=(-0.06, -0.10),
            fist_pole=(0.3, -1.0, 0.6), off_pole=(-0.6, -1.0, -0.3)),
        key((0.58, 1.30, 0.96), (-0.34, 1.12, 0.62), twist=26.0, hips=22.0, lean=20.0, tilt=-6.0, drop=0.17, shift=(0.04, 0.06), chin=6.0,
            fist_pole=(1.0, -0.6, -0.2), off_pole=(-0.6, -1.0, -0.3), control=(0.05, 2.05, 0.95), staged=True),
        key((0.84, 1.05, 0.28), (-0.22, 1.10, 0.78), twist=54.0, hips=30.0, lean=18.0, tilt=-5.0, drop=0.18, shift=(0.05, 0.05), chin=6.0,
            fist_pole=(0.5, 0.2, -1.0), off_pole=(-0.6, -1.0, -0.3)),
        key(HAND_BACK, OFF_HAND_BACK, twist=24.0, hips=16.0, lean=8.0, drop=0.12, shift=(0.03, 0.04), fist_pole=HAND_BACK_POLE, off_pole=OFF_HAND_BACK_POLE),
        # (The rear leg is not left far behind, nor the hips turned far from it: stretched back under hips turned 44 degrees, its thigh came out
        # through the back of a long coat: owner, 2026-11-22, "its the leg poking thru".)
        right=[(0.08, 0.30, (CHAIN_STANCE_X, 0.44), 0.12)],
        left=[(0.36, 0.56, (-CHAIN_STANCE_X, 0.38), 0.05)]),
    # ON SOMEONE DOWN: straight up behind the head, then down at the ground ahead.
    "chain_ground": swing(
        (0.26, 0.08, 0.12, 0.10, 0.16, 0.30), 0.20,
        key((0.45, 2.70, -0.65), (-0.20, 1.45, 0.60), twist=30.0, hips=14.0, lean=-14.0, drop=0.05, shift=(0.03, -0.10),
            fist_pole=(1.0, 0.0, -0.2), off_pole=(-0.7, -1.0, 0.0), control=(1.00, 1.75, -0.10)),
        key((0.45, 2.72, -0.72), (-0.18, 1.48, 0.62), twist=32.0, hips=15.0, lean=-17.0, drop=0.06, shift=(0.03, -0.11),
            fist_pole=(1.0, 0.0, -0.2), off_pole=(-0.7, -1.0, 0.0)),
        key((0.20, 0.80, 1.25), (-0.60, 1.25, -0.25), twist=-12.0, hips=-10.0, lean=44.0, tilt=4.0, drop=0.34, shift=(0.0, 0.10), chin=12.0,
            fist_pole=(0.7, -0.6, -0.3), off_pole=(-0.3, 0.2, -1.0), control=(0.40, 2.50, 0.80), staged=True),
        key((0.12, 0.62, 1.15), (-0.62, 1.28, -0.30), twist=-16.0, hips=-12.0, lean=48.0, tilt=4.0, drop=0.38, shift=(0.0, 0.10), chin=12.0,
            fist_pole=(0.7, -0.6, -0.3), off_pole=(-0.3, 0.2, -1.0)),
        key(HAND_BACK, OFF_HAND_BACK, twist=-6.0, hips=-4.0, lean=20.0, drop=0.18, shift=(0.0, 0.05), fist_pole=HAND_BACK_POLE, off_pole=OFF_HAND_BACK_POLE),
        left=[(0.24, 0.42, (-0.24, 0.34), 0.12)],
        right=[(0.74, 0.94, (CHAIN_STANCE_X, 0.12), 0.05)]),
}

# IN THE AIR. The same keys with the legs written out (no ground under them) and the whole body pitched: arched back, then jack-knifed.
_air = phases(0.19, 0.05, 0.11, 0.08, 0.10, 0.20)
CHAIN_MOVES["chain_air"] = dict(_air, step=0.0, air=True, keys=[
    (0.0, READY_AIR, None),
    (_air["wound"], dict(key((0.50, 2.62, -0.70), (-0.22, 1.90, 0.55), twist=34.0, hips=16.0, lean=-8.0, drop=0.0,
                             fist_pole=(1.0, 0.0, -0.2), off_pole=(-0.8, -0.6, 0.2), control=(1.00, 1.80, -0.10)),
                         **legs(((-0.04, -0.30, -0.50), (-0.15, -0.2, 1.0), (-0.05, -1.0, -0.2)), ((0.05, -0.38, -0.44), (0.15, -0.2, 1.0), (0.05, -1.0, -0.2)), pitch=-16.0)), "smooth"),
    (_air["held"], dict(key((0.50, 2.64, -0.76), (-0.22, 1.92, 0.57), twist=37.0, hips=17.0, lean=-10.0, drop=0.0,
                            fist_pole=(1.0, 0.0, -0.2), off_pole=(-0.8, -0.6, 0.2)),
                        **legs(((-0.04, -0.28, -0.52), (-0.15, -0.2, 1.0), (-0.05, -1.0, -0.2)), ((0.05, -0.36, -0.46), (0.15, -0.2, 1.0), (0.05, -1.0, -0.2)), pitch=-19.0)), "linear"),
    (_air["contact"], dict(key((0.10, 0.95, 1.15), (-0.62, 1.55, -0.40), twist=-20.0, hips=-14.0, lean=26.0, tilt=5.0, drop=0.0, chin=-6.0,
                               fist_pole=(0.7, -0.5, -0.4), off_pole=(-0.4, 0.3, -1.0), control=(0.42, 2.55, 0.75), staged=True),
                           **legs(((-0.05, -0.26, 0.44), (-0.1, 0.8, 1.0), (-0.05, -0.8, 0.6)), ((0.05, -0.64, -0.36), (0.1, -0.3, 1.0), (0.05, -1.0, 0.2)), pitch=18.0)), "linear"),
    (_air["followed"], dict(key((-0.02, 0.72, 1.00), (-0.64, 1.58, -0.46), twist=-28.0, hips=-18.0, lean=29.0, tilt=5.0, drop=0.0, chin=-6.0,
                                fist_pole=(0.7, -0.5, -0.4), off_pole=(-0.4, 0.3, -1.0)),
                            **legs(((-0.05, -0.24, 0.46), (-0.1, 0.8, 1.0), (-0.05, -0.8, 0.6)), ((0.05, -0.65, -0.38), (0.1, -0.3, 1.0), (0.05, -1.0, 0.2)), pitch=21.0)), "slow_in"),
    (_air["settled"], dict(key((-0.02, 0.72, 1.00), (-0.64, 1.58, -0.46), twist=-28.0, hips=-18.0, lean=29.0, tilt=5.0, drop=0.0, chin=-6.0,
                               fist_pole=(0.7, -0.5, -0.4), off_pole=(-0.4, 0.3, -1.0)),
                           **legs(((-0.05, -0.24, 0.46), (-0.1, 0.8, 1.0), (-0.05, -0.8, 0.6)), ((0.05, -0.65, -0.38), (0.1, -0.3, 1.0), (0.05, -1.0, 0.2)), pitch=21.0)), "linear"),
    (_air["settled"] + (_air["seconds"] - _air["settled"]) * 0.55,
     dict(key(HAND_BACK, OFF_HAND_BACK, twist=-8.0, hips=-5.0, lean=12.0, drop=0.0, fist_pole=HAND_BACK_POLE, off_pole=OFF_HAND_BACK_POLE),
          **legs(((-0.04, -0.60, 0.16), (-0.1, 0.3, 1.0), (-0.05, -0.6, 0.9)), ((0.04, -0.68, -0.14), (0.1, 0.1, 1.0), (0.05, -0.7, 0.8)), pitch=10.0)), "smooth"),
    (_air["seconds"], READY_AIR, "smooth"),
])

ORDER_OF_CHAIN_MOVES = ("chain_lash", "chain_backhand", "chain_air", "chain_ground")


def stepped(move, seconds):
    """How far the game has stepped a STANDING body by then (SwordStrikes.Stepped: keep the two in step): from halfway through the startup to the end of the live window."""
    wind, hold, strike, follow, _, _ = move["timing"]
    startup = wind + hold + strike * 0.25                       # SwordStrikes.ActiveStart
    window = wind + hold + strike + follow * 0.7                # SwordStrikes.ActiveEnd
    return move["step"] * smooth((seconds - startup * 0.5) / (window - startup * 0.5))


# A leg is never locked straight (sword_moves_pose.py's rule): past LEG_EASY of its length the ankle gives toward the hip, and never goes past LEG_MOST.
LEG_EASY, LEG_MOST = 0.89, 0.975


def within_reach(spec, stats):
    leg = (HEAD["shin_l"] - HEAD["thigh_l"]).length + (HEAD["foot_l"] - HEAD["shin_l"]).length
    foot = V(*spec["foot"])
    asked, easy, most = foot.length, LEG_EASY * leg, LEG_MOST * leg
    reach = asked if asked <= easy else easy + (most - easy) * (1.0 - math.exp(-(asked - easy) / (most - easy)))
    stats["used"] = max(stats.get("used", 0.0), reach)
    return dict(spec, foot=tuple(foot * (reach / asked)))


# A wrist is kept this far outside the head's ball and the trunk's bar, metres: room for the hand's own thickness and its fingers.
WRIST_OFF_HEAD = 0.16
WRIST_OFF_TRUNK = 0.10
# And this far from its own shoulder: nearer, the elbow folds shut (owner, 2026-11-20: no elbow folded back on itself). At this the elbow is open about 65 degrees.
LEAST_REACH = 0.50


def kept_clear(P):
    """The pose with each wrist moved straight out of the head and the trunk where its key, or the way between two keys, ran it through them."""
    _, pos = solve(P)
    head, low, high = to_unity(pos["head"]), to_unity(pos["hips"]), to_unity(pos["neck"])
    for arm, shoulder in (("right", to_unity(pos["arm_r"])), ("free", to_unity(pos["arm_l"]))):
        hand = V(*P[arm]["hand"])
        out = hand - shoulder
        if 1e-6 < out.length < LEAST_REACH:
            hand = shoulder + out.normalized() * LEAST_REACH
        out = hand - head
        if 1e-6 < out.length < HEAD_RADIUS + WRIST_OFF_HEAD:
            hand = head + out.normalized() * (HEAD_RADIUS + WRIST_OFF_HEAD)
        along = high - low
        on = low + along * max(0.0, min(1.0, (hand - low).dot(along) / along.length_squared))
        out = hand - on
        if 1e-6 < out.length < TRUNK_RADIUS + WRIST_OFF_TRUNK:
            hand = on + out.normalized() * (TRUNK_RADIUS + WRIST_OFF_TRUNK)
        P[arm]["hand"] = tuple(hand)
    return P


def chain_pose_at(move, seconds, stats):
    keys = [(t / move["seconds"], k, ease) for t, k, ease in move["keys"]]
    share = seconds / move["seconds"]
    s = sample(keys, share)
    P, hips_at, body = body_pose(s, 1.0)
    if move.get("air"):
        # The whole body pitched about the hips, the chest's lean on top of it; the legs from the hips, as keyed.
        whole = pitch(sample_field(keys, share, "pitch", 0.0, 1.3))
        P["body"] = whole @ P["body"]
        for side in ("l", "r"):
            P[side + "_leg"] = {"foot": tuple(sample_field(keys, share, side + "_foot", None, 1.3)), "knee": tuple(sample_field(keys, share, side + "_knee", None, 1.3)),
                                "toe": tuple(sample_field(keys, share, side + "_toe", None, 1.3)), "space": "hips"}
        return kept_clear(P)
    for side, steps, x in (("l", move["left"], -CHAIN_STANCE_X), ("r", move["right"], CHAIN_STANCE_X)):
        hip_b = U(*hips_at) + body @ (HEAD["thigh_" + side] - HEAD["hips"])
        P[side + "_leg"] = within_reach(planted_leg(side, to_unity(hip_b), steps, (x, 0.0), seconds, stepped(move, seconds), hips_at.z, 28.0, stats), stats)
    return kept_clear(P)


# The head is a ball about the head bone and the trunk a round bar from the hips to the neck (FusedBodyShape): an arm is kept outside both (owner, 2026-11-20, of other
# moves: no clipping the head, measured every frame).
HEAD_RADIUS = 0.35
TRUNK_RADIUS = 0.26


def off_the_trunk(point, pos):
    a, b = pos["hips"], pos["neck"]
    ab = b - a
    t = max(0.0, min(1.0, (point - a).dot(ab) / ab.length_squared))
    return (point - (a + ab * t)).length - TRUNK_RADIUS


def bake_chain():
    if rig.animation_data and rig.animation_data.action:
        old = rig.animation_data.action
        rig.animation_data.action = None
        bpy.data.actions.remove(old)
    rig.animation_data_create()
    action = bpy.data.actions.new("ChainMoves")
    rig.animation_data.action = action
    scene = bpy.context.scene
    scene.timeline_markers.clear()
    previous = {}
    start = 0
    spans = {}
    leg = (HEAD["shin_l"] - HEAD["thigh_l"]).length + (HEAD["foot_l"] - HEAD["shin_l"]).length
    arm = (HEAD["forearm_r"] - HEAD["arm_r"]).length + (HEAD["hand_r"] - HEAD["forearm_r"]).length
    for name in ORDER_OF_CHAIN_MOVES:
        move = CHAIN_MOVES[name]
        frames = int(round(move["seconds"] * FPS))
        scene.timeline_markers.new(name, frame=start)
        stats = {"reach": 0.0}
        tightest_knee, tightest_elbow, straightest_arm, fastest = 180.0, (180.0, 0.0, ""), 0.0, 0.0
        head_room, torso_room = (9.0, 0.0, ""), (9.0, 0.0, "")
        last_hand = None
        for i in range(frames):
            seconds = i / float(FPS)
            D, pos = solve(chain_pose_at(move, seconds, stats))
            for side in ("l", "r"):
                thigh = pos["thigh_" + side] - pos["shin_" + side]
                shin = pos["foot_" + side] - pos["shin_" + side]
                tightest_knee = min(tightest_knee, math.degrees(thigh.angle(shin)))
                upper = pos["arm_" + side] - pos["forearm_" + side]
                fore = pos["hand_" + side] - pos["forearm_" + side]
                tightest_elbow = min(tightest_elbow, (math.degrees(upper.angle(fore)), seconds, side))
            for side in ("l", "r"):
                elbow, wrist, tip = pos["forearm_" + side], pos["hand_" + side], pos["fingertip_" + side]
                for what, point in (("elbow", elbow), ("forearm", (elbow + wrist) * 0.5), ("wrist", wrist), ("fingers", tip)):
                    head_room = min(head_room, ((point - pos["head"]).length - HEAD_RADIUS, seconds, what + "_" + side))
                    torso_room = min(torso_room, (off_the_trunk(point, pos), seconds, what + "_" + side))
            straightest_arm = max(straightest_arm, (pos["hand_r"] - pos["arm_r"]).length / arm)
            if last_hand is not None:
                fastest = max(fastest, (pos["hand_r"] - last_hand).length * FPS)
            last_hand = pos["hand_r"].copy()
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
            rig.location = U(0.0, 0.0, stepped(move, seconds))
            rig.keyframe_insert("location", frame=f)
        print("%-15s %3d frames | the arm reaches %.2f of its length, its hand at most %.1f m/s | tightest elbow %3.0f deg (%s at %.2f s), knee %3.0f deg | furthest ankle %.2f m of a %.2f m leg"
              % (name, frames, straightest_arm, fastest, tightest_elbow[0], tightest_elbow[2], tightest_elbow[1], tightest_knee, stats.get("used", 0.0), leg))
        print("%-15s nearest the head: %s %+.2f m at %.2f s | nearest the trunk: %s %+.2f m at %.2f s" % ("", head_room[2], head_room[0], head_room[1], torso_room[2], torso_room[0], torso_room[1]))
        spans[name] = (start, start + frames - 1, move)
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
