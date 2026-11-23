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

# The tab lifted (PhizzCan.TabOpenDegrees about its rivet, along the can's Z, at the lid: CanMesh.LidHeight): where its free end has swung to.
TAB_OPEN_DEGREES = 75.0
TAB_HINGE = V(0.0, 0.1185 * LIFE, 0.0)
TAB_END_OPEN = TAB_HINGE + Matrix.Rotation(math.radians(TAB_OPEN_DEGREES), 3, "Z") @ (TAB_END - TAB_HINGE)

# The mouth on the head (a sphere of 0.35 m about the head bone; faces are painted by the player, so the mouth is where a drawn one sits): low on its front,
# a little inside the surface, the lips.
MOUTH = V(0.0, -0.35 * math.sin(math.radians(22.0)), 0.33 * math.cos(math.radians(22.0)))

# The head bone at the rig's rest, in the game's frame (its turn the character's own): the frame the mouth is on.
HEAD_REST = u_trs(tuple(to_unity(HEAD["head"])), Quaternion())

# THE CARRY: the bottle's (Bottle/bottle_moves.py), so a can taken in hand is held as a bottle is and the drink begins where the arm already is.
CARRY = nodding(ikey((0.46, 1.48, 0.50), (-0.50, 1.04, 0.06), wrist=(0.0, -12.0, -10.0), fist_pole=(0.45, -1.0, -0.35)))

# The full PHIZZ bottle carried lower: it is as long as an arm, and held upright at the chest its neck stood before the face.
LOW_CARRY = nodding(ikey((0.46, 1.18, 0.46), (-0.50, 1.04, 0.06), wrist=(0.0, -12.0, -10.0), fist_pole=(0.45, -1.0, -0.35)))

# The can brought in before the chest for the other hand to open it.
OPENING_RIGHT = dict(fist=V(0.30, 1.50, 0.56), wrist=V(0.0, -8.0, -4.0), fist_pole=V(0.55, -1.0, -0.2))

# Standing again, the hands empty.
STAND = nodding(dict(READY, wrist=V(0.0, 0.0, 0.0)))


def can_in_head(tilt):
    """The can in the head's frame with its hole's rim at the lips, tipped `tilt` degrees from upright toward the face (its top to the mouth, its base out
    ahead and up), the hole at the lowest point of the tipped lid, where the drink runs out."""
    s, c = math.sin(math.radians(tilt)), math.cos(math.radians(tilt))
    turn = Matrix(((0.0, 0.0, -1.0), (s, c, 0.0), (c, -s, 0.0)))          # columns: the can's X, Y, Z in the head's axes
    lip = V(-DRINK["rim"], DRINK["height"], 0.0)
    m = turn.to_4x4()
    m.translation = MOUTH - turn @ lip
    return m


def at_mouth(k, tilt, name):
    """Key `k` with the right hand where it holds the can tipped `tilt` degrees with its rim at the lips."""
    hold = DRINK["hold"].inverted()
    fit_hand(k, lambda head: head @ can_in_head(tilt) @ hold, name, "head", HEAD_REST)
    return k


def tab_end_in_character(k, end=None):
    """Where the opener (the can's tab, the bottle's cap: `end` in the drink's frame, shut if none) is in the character's frame, with the right hand as
    key `k` holds the drink."""
    D, pos = _solved_key(k, True)
    return (unity_frame(D, pos, "hand_r", HAND_REST) @ DRINK["hold"]) @ (DRINK["opener"] if end is None else end)


# THE LEFT HAND AT THE TAB (owner, with a photo of a can being opened: "make it so the hand his turned knuckles upward and the finger is pinching the 'tab'. no
# clipping into the can"): over the lid, knuckles up, the fingers angled down at it, the index and the thumb pinching the tab's free end (the game shapes
# the fingers as the slingshot's pinch: CanDrinker.TabHand). The solver's hand runs straight on from its forearm, so the hand is turned here, after it.
# Where the index fingertip's last joint is in the hand (HandGrip.TipInHand, the slingshot pinch), the game's hand frame: along the fingers, toward the back
# of the hand, toward the thumb. It is put a fingertip's thickness above the tab, so its pad is on it (aimed between the index and the thumb, the index
# went 4.5 cm through the lid: CanDrinkPlayTests).
PINCH_IN_HAND = (0.185, -0.072, 0.087)
FINGERTIP_OVER = 0.03

# Which way the left hand's fingers point at the tab (the character's frame): in from the left, ahead, and steeply down at the lid.
PINCH_FINGERS = V(0.50, -0.62, 0.60).normalized()

_T3 = TO_BLENDER.to_3x3()


def _hand_frame(fingers, back):
    """The left hand's frame from its fingers' way and its back's (the character's frame): fingers, back, thumb (the left thumb is the back crossed with
    the fingers)."""
    back = (back - fingers * back.dot(fingers)).normalized()
    return fingers, back, back.cross(fingers)


# The left hand at the rig's rest: hanging, the fingers down, the palm to the body, its back out to the left.
_REST_L = _hand_frame(to_unity(TAIL["hand_l"] - HEAD["hand_l"]).normalized(), V(-1.0, 0.0, 0.0))
_PINCH_L = _hand_frame(PINCH_FINGERS, V(0.0, 1.0, 0.0))


def _turn_between(frame_from, frame_to):
    """The turn (Blender's axes, as the solver's are) carrying one hand frame onto another."""
    a = Matrix((frame_from[0], frame_from[1], frame_from[2])).transposed()
    b = Matrix((frame_to[0], frame_to[1], frame_to[2])).transposed()
    return (_T3 @ (b @ a.transposed()) @ _T3.inverted()).to_quaternion()


LEFT_PINCH_TURN = _turn_between(_REST_L, _PINCH_L)


def pinching(k, at, name):
    """Key `k` with the left hand turned knuckles up and its pinch at `at` (the character's frame)."""
    f, b, t = _PINCH_L
    a, up, side = PINCH_IN_HAND
    k = dict(k, off=at - (f * a + b * up + t * side), lhand=1.0)
    print("%s: the left wrist at (%.2f, %.2f, %.2f)" % (name, k["off"].x, k["off"].y, k["off"].z))
    return k


def turn_left_hand(D, keys, t01):
    """The left hand turned to the pinch, as much as the keys say (their `lhand`, eased as the shoulders are)."""
    w = sample_field(keys, t01, "lhand", 0.0, 1.6)
    if w > 0.0:
        D["hand_l"] = D["hand_l"].slerp(LEFT_PINCH_TURN, min(1.0, w))
        D["fingertip_l"] = D["hand_l"]


def build_keys():
    """The clip's keys: [(share of the clip, key, ease)]. Built by fitting, so it runs once the rig and CAN_HOLD are there."""
    opening = ikey(tuple(DRINK["opening_fist"]), (-0.40, 1.60, 0.40), wrist=tuple(OPENING_RIGHT["wrist"]), fist_pole=tuple(OPENING_RIGHT["fist_pole"]),
                   off_pole=(-0.6, -0.6, -0.3), twist=8.0, lean=4.0, chin=10.0)
    tab = tab_end_in_character(opening)
    # The fingertip comes over the top to just past the tab's free end, under it, then flicks up.
    # The pinch comes down onto the tab's free end, a little above the lid, and lifts it as it is pulled up and back.
    under = pinching(dict(opening), tab + V(0.0, FINGERTIP_OVER, 0.0), "pinching the tab")
    flicked = pinching(dict(opening, chin=12.0), tab_end_in_character(opening, DRINK["opened"]) + V(0.0, FINGERTIP_OVER, 0.0), "the tab pulled up")
    above = pinching(dict(opening), tab + V(-0.04, FINGERTIP_OVER + 0.16, 0.0), "over the can")
    opened = dict(opening, off=V(-0.50, 1.04, 0.06), off_pole=V(-0.4, -0.2, -1.0), chin=6.0)

    # The recording's moments, mirrored (its head and chest turns, as lean and chin; the can's tip at the lips).
    sip = at_mouth(ikey((0.20, 1.90, 0.55), (-0.50, 1.04, 0.06), lean=18.0, chin=26.0, twist=6.0, hips=2.0, drop=0.08, fist_pole=(0.8, -0.6, -0.3)), 75.0, "the sip")
    sipped = at_mouth(ikey((0.20, 1.95, 0.55), (-0.50, 1.04, 0.06), lean=10.0, chin=14.0, twist=6.0, hips=2.0, drop=0.08, fist_pole=(0.8, -0.5, -0.3)), 85.0, "after the sip")
    gulp = at_mouth(ikey((0.20, 2.10, 0.50), (-0.50, 1.04, 0.06), lean=-14.0, chin=-22.0, twist=4.0, hips=2.0, drop=0.06, shift=(0.0, -0.03), fist_pole=(0.5, -0.7, 1.0)),
                    95.0, "the gulp")
    gulped = at_mouth(ikey((0.20, 2.10, 0.50), (-0.50, 1.04, 0.06), lean=-16.0, chin=-24.0, twist=4.0, hips=2.0, drop=0.06, shift=(0.0, -0.03), fist_pole=(0.5, -0.7, 1.0)),
                      100.0, "the last of it")
    lowering = ikey((0.36, 1.62, 0.56), (-0.50, 1.04, 0.06), wrist=(0.0, -10.0, -10.0), lean=2.0, chin=4.0, fist_pole=(0.5, -1.0, -0.3))
    carried = dict(DRINK["carry"])
    let_go = ikey((0.50, 1.30, 0.42), (-0.50, 1.04, 0.06), wrist=(0.0, -10.0, -6.0), fist_pole=(0.45, -1.0, -0.4))

    def at(seconds):
        return seconds / SECONDS

    # The recording's frames 55 to 175 from 0.75 s, 1.65 times as fast: frame f at 0.75 + (f - 55) / 30 / 1.65 s.
    def frame(f):
        return at(0.75 + (f - 55) / 30.0 / 1.65)

    return [
        (0.0, DRINK["carry"], None),
        (at(0.22), dict(above), "smooth"),
        (at(0.32), under, "smooth"),
        (at(0.44), flicked, "linear"),
        (at(0.56), dict(above), "smooth"),
        (frame(55), opened, "smooth"),
        (frame(85), sip, "smooth"),
        (frame(97), sipped, "smooth"),
        (frame(122), gulp, "smooth"),
        (frame(140), gulped, "linear"),
        (frame(158), lowering, "smooth"),
        (frame(172), carried, "smooth"),
    ] + ([(1.0, dict(DRINK["end"]), "smooth")] if DRINK["kept"] else [(at(3.28), let_go, "smooth"), (1.0, STAND, "smooth")])


# The feet stay planted where they stand: a drink is taken standing, and walking the legs are the walk's (the game's driver).
STANCE_LEAD, STANCE_REAR = (-STANCE_X, 0.0), (STANCE_X, 0.0)


def drink_pose(keys, t01, stats):
    s = sample(keys, t01)
    P, hips_at, body = body_pose(s, 1.0)
    for leg, start in (("l", STANCE_LEAD), ("r", STANCE_REAR)):
        hip_b = U(*hips_at) + body @ (HEAD["thigh_" + leg] - HEAD["hips"])
        P[leg + "_leg"] = planted_leg(leg, to_unity(hip_b), [], start, t01 * SECONDS, 0.0, hips_at.z, 28.0, stats)
    return P


# THE DRINKS (each its own clip, 3.5 s): how the right hand holds it (the game's hold: keep in step), its rim, where the left hand pinches it shut and
# where that has gone once open, whether it stays in the hand after (the can is let fall; the PHIZZ bottle, drunk empty, is kept as a weapon: owner),
# its carry (the first frame: the game holds the arm there while it is in hand) and where it is opened. The can's tab swings up on its rivet; the
# bottle's crown cap is pulled up off its mouth, and its clip ends in the empty bottle's own carry, which it becomes.
SODA = 2.4 * 1.1                                    # BottleShape.Scale for the soda bottle
DRINKS = [
    dict(name="can_drink", hold=CAN_HOLD, height=CAN_HEIGHT, rim=RIM_RADIUS, opener=TAB_END, opened=TAB_END_OPEN, kept=False, carry=CARRY,
         opening_fist=OPENING_RIGHT["fist"]),
    dict(name="bottle_drink", hold=BOTTLE_HOLD, height=0.238 * SODA, rim=0.0128 * SODA, opener=V(0.03, 0.2425 * SODA, 0.0),
         opened=V(-0.01, 0.2425 * SODA + 0.08, 0.0), kept=True, carry=LOW_CARRY, opening_fist=V(0.30, 1.16, 0.56),
         end=CARRY),
]


def bake_drinks():
    """Every drink's clip, one after another on one action; returns {name: (first frame, frames)}."""
    global DRINK
    if rig.animation_data and rig.animation_data.action:
        old = rig.animation_data.action
        rig.animation_data.action = None
        bpy.data.actions.remove(old)
    rig.animation_data_create()
    action = bpy.data.actions.new("Drinks")
    rig.animation_data.action = action
    scene = bpy.context.scene
    scene.timeline_markers.clear()
    frames = int(round(SECONDS * FPS))
    previous = {}
    stats = {"reach": 0.0}
    spans = {}
    start = 0
    for drink in DRINKS:
        DRINK = drink
        keys = build_keys()
        scene.timeline_markers.new(drink["name"], frame=start)
        for i in range(frames):
            t01 = i / float(frames - 1)
            D, pos = solve(drink_pose(keys, t01, stats))
            turn_wrist(D, pos, sample_wrist(keys, t01))
            turn_left_hand(D, keys, t01)
            carry_head(D, pos, keys, t01)
            apply_pose(locals_of(D), pos["hips"])
            for n in ORDER:
                pb = rig.pose.bones[n]
                q = pb.rotation_quaternion.copy()
                if n in previous and previous[n].dot(q) < 0.0:
                    q = -q
                pb.rotation_quaternion = q
                previous[n] = q
                pb.keyframe_insert("rotation_quaternion", frame=start + i, group=n)
                if PARENT[n] is None:
                    pb.keyframe_insert("location", frame=start + i, group=n)
        spans[drink["name"]] = (start, frames)
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
