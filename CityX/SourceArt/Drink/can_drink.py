# DRINKING A CAN OF PHIZZ AS ONE FULL-BODY CLIP (docs/PHIZZ_CAN.md). Owner, 2026-11-23: "make a drinking animation for the can ... he holds it in his right
# hand, and when you initiate the drinking. he reaches over with his left hand and does an opening the tab animation and a black hole appears where the
# drinking hole is. then it plays the drinking animation attached drops the can", "the whole drinking animation from click to completion last 3.5 seconds".
#
# One clip, can_drink, 3.5 s:
#   * from the carry (the bottle's: the forearm up before the right of the chest, the can standing up out of the fist, its hole toward the body), the can
#     comes in a little before the chest and the left hand reaches over the top of it; its fingertip gets under the tab's free end and flicks it up
#     (the game lifts the tab and shows the hole: CanDrinker.TabSeconds), and the left hand goes back down;
#   * the owner's Mixamo "Drinking" (Downloads/Drinking.fbx, the cup in its LEFT hand: mirrored here), from its frame 55 to 175, 1.65 times as fast: the head
#     bows down to the can for a sip (its frame 85), the can is tipped up as the head goes back to drink (frames 115 to 140), and comes back down. Its
#     head and chest turns are the keys' lean and chin; the can's rim is put to OUR mouth at each moment it is at the recording's (fit_hand, from the
#     head), so the can meets the lips on this body's own proportions;
#   * the arm comes down a little and the hand lets the can drop (CanDrinker.DropSeconds), and the arm hangs again.
#
# Needs the rig, ../game_frames.py, the solver (OCTASLASH_NO_BAKE), ../planted_feet.py, ../FistCombo/fist_pose.py, ../wrist_turn.py and ../head_carry.py
# loaded first, and CAN_HOLD (the can in the right hand bone's frame, as the game holds it); can_drink_build.py does all of it.
#
# Positions in the character's own frame (Unity axes: x right, y up, z forward, metres, the ground at y 0).

import math

from mathutils import Matrix, Quaternion

SECONDS = 3.5

# The can (Weapons/Drinks/CanMesh.cs, at the game's 2.4 times life): its height, its rim's radius, and its tab's free end, in its own frame (base at the
# origin, +Y up, the tab's free end along +X, the hole along -X).
LIFE = 2.4
CAN_HEIGHT = 0.1222 * LIFE
RIM_RADIUS = 0.0262 * LIFE
TAB_END = V(0.0160 * LIFE, 0.1205 * LIFE, 0.0)

# The mouth on the head (a sphere of 0.35 m about the head bone; faces are painted by the player, so the mouth is where a drawn one sits): low on its front,
# a little inside the surface, the lips.
MOUTH = V(0.0, -0.35 * math.sin(math.radians(22.0)), 0.33 * math.cos(math.radians(22.0)))

# The head bone at the rig's rest, in the game's frame (its turn the character's own): the frame the mouth is on.
HEAD_REST = u_trs(tuple(to_unity(HEAD["head"])), Quaternion())

# THE CARRY: the bottle's (Bottle/bottle_moves.py), so a can taken in hand is held as a bottle is and the drink begins where the arm already is.
CARRY = nodding(ikey((0.46, 1.48, 0.50), (-0.50, 1.04, 0.06), wrist=(0.0, -12.0, -10.0), fist_pole=(0.45, -1.0, -0.35)))

# The can brought in before the chest for the other hand to open it.
OPENING_RIGHT = dict(fist=V(0.30, 1.50, 0.56), wrist=V(0.0, -8.0, -4.0), fist_pole=V(0.55, -1.0, -0.2))

# Standing again, the hands empty.
STAND = nodding(dict(READY, wrist=V(0.0, 0.0, 0.0)))


def can_in_head(tilt):
    """The can in the head's frame with its hole's rim at the lips, tipped `tilt` degrees from upright toward the face (its top to the mouth, its base out
    ahead and up), the hole at the lowest point of the tipped lid, where the drink runs out."""
    s, c = math.sin(math.radians(tilt)), math.cos(math.radians(tilt))
    turn = Matrix(((0.0, 0.0, -1.0), (s, c, 0.0), (c, -s, 0.0)))          # columns: the can's X, Y, Z in the head's axes
    lip = V(-RIM_RADIUS, CAN_HEIGHT, 0.0)
    m = turn.to_4x4()
    m.translation = MOUTH - turn @ lip
    return m


def at_mouth(k, tilt, name):
    """Key `k` with the right hand where it holds the can tipped `tilt` degrees with its rim at the lips."""
    hold = CAN_HOLD.inverted()
    fit_hand(k, lambda head: head @ can_in_head(tilt) @ hold, name, "head", HEAD_REST)
    return k


def tab_end_in_character(k):
    """Where the tab's free end is, in the character's frame, with the right hand as key `k` holds the can."""
    D, pos = _solved_key(k, True)
    return (unity_frame(D, pos, "hand_r", HAND_REST) @ CAN_HOLD) @ TAB_END


def left_fingertip_to(k, target, name):
    """Moves key `k`'s left wrist until the left fingertip is at `target` (the solver's hand runs straight on from its forearm)."""
    for _ in range(8):
        D, pos = _solved_key(k, True)
        tip = to_unity(pos["fingertip_l"])
        k["off"] = k["off"] + (target - tip)
    D, pos = _solved_key(k, True)
    print("%s: left fingertip %.3f m from the tab" % (name, (to_unity(pos["fingertip_l"]) - target).length))
    return k


def build_keys():
    """The clip's keys: [(share of the clip, key, ease)]. Built by fitting, so it runs once the rig and CAN_HOLD are there."""
    opening = ikey(tuple(OPENING_RIGHT["fist"]), (-0.40, 1.60, 0.40), wrist=tuple(OPENING_RIGHT["wrist"]), fist_pole=tuple(OPENING_RIGHT["fist_pole"]),
                   off_pole=(-0.6, -0.6, -0.3), twist=8.0, lean=4.0, chin=10.0)
    tab = tab_end_in_character(opening)
    # The fingertip comes over the top to just past the tab's free end, under it, then flicks up.
    under = left_fingertip_to(dict(opening), tab + V(0.0, -0.005, 0.0) + V(0.012, 0.0, 0.0), "under the tab")
    flicked = left_fingertip_to(dict(opening, chin=12.0), tab + V(-0.02, 0.09, 0.0), "the tab flicked up")
    above = left_fingertip_to(dict(opening), tab + V(-0.06, 0.12, 0.0), "over the can")
    opened = dict(opening, off=V(-0.50, 1.04, 0.06), off_pole=V(-0.4, -0.2, -1.0), chin=6.0)

    # The recording's moments, mirrored (its head and chest turns, as lean and chin; the can's tip at the lips).
    sip = at_mouth(ikey((0.20, 1.90, 0.55), (-0.50, 1.04, 0.06), lean=18.0, chin=26.0, twist=6.0, hips=2.0, drop=0.08, fist_pole=(0.8, -0.6, -0.3)), 75.0, "the sip")
    sipped = at_mouth(ikey((0.20, 1.95, 0.55), (-0.50, 1.04, 0.06), lean=10.0, chin=14.0, twist=6.0, hips=2.0, drop=0.08, fist_pole=(0.8, -0.5, -0.3)), 85.0, "after the sip")
    gulp = at_mouth(ikey((0.20, 2.10, 0.50), (-0.50, 1.04, 0.06), lean=-14.0, chin=-22.0, twist=4.0, hips=2.0, drop=0.06, shift=(0.0, -0.03), fist_pole=(0.5, -0.7, 1.0)),
                    95.0, "the gulp")
    gulped = at_mouth(ikey((0.20, 2.10, 0.50), (-0.50, 1.04, 0.06), lean=-16.0, chin=-24.0, twist=4.0, hips=2.0, drop=0.06, shift=(0.0, -0.03), fist_pole=(0.5, -0.7, 1.0)),
                      100.0, "the last of it")
    lowering = ikey((0.36, 1.62, 0.56), (-0.50, 1.04, 0.06), wrist=(0.0, -10.0, -10.0), lean=2.0, chin=4.0, fist_pole=(0.5, -1.0, -0.3))
    carried = dict(CARRY)
    let_go = ikey((0.50, 1.30, 0.42), (-0.50, 1.04, 0.06), wrist=(0.0, -10.0, -6.0), fist_pole=(0.45, -1.0, -0.4))

    def at(seconds):
        return seconds / SECONDS

    # The recording's frames 55 to 175 from 0.75 s, 1.65 times as fast: frame f at 0.75 + (f - 55) / 30 / 1.65 s.
    def frame(f):
        return at(0.75 + (f - 55) / 30.0 / 1.65)

    return [
        (0.0, CARRY, None),
        (at(0.22), dict(above), "smooth"),
        (at(0.32), under, "smooth"),
        (at(0.44), flicked, "slow_in"),
        (at(0.56), dict(above), "smooth"),
        (frame(55), opened, "smooth"),
        (frame(85), sip, "smooth"),
        (frame(97), sipped, "smooth"),
        (frame(122), gulp, "smooth"),
        (frame(140), gulped, "linear"),
        (frame(158), lowering, "smooth"),
        (frame(172), carried, "smooth"),
        (at(3.28), let_go, "smooth"),
        (1.0, STAND, "smooth"),
    ]


# The feet stay planted where they stand: a drink is taken standing, and walking the legs are the walk's (the game's driver).
STANCE_LEAD, STANCE_REAR = (-STANCE_X, 0.0), (STANCE_X, 0.0)


def drink_pose(keys, t01, stats):
    s = sample(keys, t01)
    P, hips_at, body = body_pose(s, 1.0)
    for leg, start in (("l", STANCE_LEAD), ("r", STANCE_REAR)):
        hip_b = U(*hips_at) + body @ (HEAD["thigh_" + leg] - HEAD["hips"])
        P[leg + "_leg"] = planted_leg(leg, to_unity(hip_b), [], start, t01 * SECONDS, 0.0, hips_at.z, 28.0, stats)
    return P


def bake_drink():
    keys = build_keys()
    if rig.animation_data and rig.animation_data.action:
        old = rig.animation_data.action
        rig.animation_data.action = None
        bpy.data.actions.remove(old)
    rig.animation_data_create()
    action = bpy.data.actions.new("CanDrink")
    rig.animation_data.action = action
    scene = bpy.context.scene
    scene.timeline_markers.clear()
    scene.timeline_markers.new("can_drink", frame=0)
    frames = int(round(SECONDS * FPS))
    previous = {}
    stats = {"reach": 0.0}
    for i in range(frames):
        t01 = i / float(frames - 1)
        D, pos = solve(drink_pose(keys, t01, stats))
        turn_wrist(D, pos, sample_wrist(keys, t01))
        carry_head(D, pos, keys, t01)
        apply_pose(locals_of(D), pos["hips"])
        for n in ORDER:
            pb = rig.pose.bones[n]
            q = pb.rotation_quaternion.copy()
            if n in previous and previous[n].dot(q) < 0.0:
                q = -q
            pb.rotation_quaternion = q
            previous[n] = q
            pb.keyframe_insert("rotation_quaternion", frame=i, group=n)
            if PARENT[n] is None:
                pb.keyframe_insert("location", frame=i, group=n)
    scene.timeline_markers.new("end", frame=frames)
    for fc in action.fcurves:
        for kp in fc.keyframe_points:
            kp.interpolation = 'LINEAR'
    scene.render.fps = FPS
    scene.frame_start = 0
    scene.frame_end = frames - 1
    scene.frame_set(0)
    return frames
