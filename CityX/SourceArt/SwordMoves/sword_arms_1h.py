# THE ONE-HANDED SWORD ARM, KEYED JOINT BY JOINT (owner, 2026-11-15, after a day of the game working the arm out from a few points: "this arm is bent the
# wrong way", "WHY IS THE HAND STILL TWISTED THE WRONG WAY", "RESEARCH HOW HUMANS ... HOLD SWORDS"; "do hand posed with blender ... with proper grip and
# arm/hand placements"; "USE MIXAMO AS A TOOL IF NEEDED BUT DONT JUST COPY MIXAMO ANIMATIONS").
#
# THE GRIP is the game's own one-handed hold as the owner tuned it (sword_hold.py): the handle closed in the fingers right under the guard, the blade
# straight out past the thumb across the forearm, the edge in line with the knuckles. (A grip leaning the blade toward the fingertips, the "handshake"
# some fencing books teach, ran the handle back along the forearm: owner, 2026-11-15, "your grip is like slipping down the arm".) So the blade stands
# across the forearm, and the wrist, near straight, cocks it back toward the thumb in the wind-up and snaps it toward the little finger at the end.
#
# HOW A POSE IS WRITTEN: as an animator keys an arm on its rig, joint by joint, each joint on its parent: the shoulder on the CHEST (so the body's turn
# carries the arm and the blade, and the arm adds its own on top), in degrees: the upper arm raised FORWARD (flex) and OUT to the side (abduct) from
# hanging, and turned about its length (twist: + its front outward); the ELBOW bent; the forearm turning the hand (turn: + the thumb outward, from the
# handshake: thumb toward the way the elbow bends); and the WRIST, a little: toward the thumb (radial +) and toward the palm (palm +). Between keys every
# joint is eased as the body's keys are. Every pose is one a real arm can make (the limits below), and the numbers printed for each key say where the
# hand is, the room kept off the body, how low the tip goes, whether the edge leads the cut, and how far the trail bows seen from the game's camera.
#
# THE CUT'S TRAIL IS ONE STRAIGHT STROKE (owner, 2026-11-15: "make it straight across like the red line"). Each cut has a plane laid through the game
# camera's eye (a cutting plane, as a swordsman is taught); its keys from the moment the trail is drawn (a quarter into the strike,
# SwordStrikes.ActiveStart) to the end of the follow-through are posed with the blade on it and the edge leading, and between them, while the trail is
# drawn, the whole arm is turned a little at the shoulder to keep the outer half of the blade on it (what BladeTrail draws): the elbow, the forearm and
# the wrist are as keyed, so the arm moves as it was posed. (A cut solved wholly on the plane, the arm in it too, put the elbow into the chest and
# could not raise the hand; keyed joints alone swept the tip in a loop over the head.) The wrist is cocked toward the thumb early and snaps toward the
# little finger at the end (the Mixamo recordings and the fencing books agree).
#
# Mixamo's recorded sword clips (studied, not copied) gave the ranges: a cut's elbow opens from 70-90 degrees to 10-25 at the hit and bends again after
# it; the forearm turns 40-90 degrees through a cut; the wrist stays within about 20 degrees of straight and snaps a little past straight at the end.
#
# Needs the rig, the game's hold (sword_hold.py), octaslash_pose.py and sword_moves_pose.py loaded first (sword_moves_build.py).

import math

from mathutils import Quaternion, Vector

COMBAT_BLADE = 1.39            # the combat sword's blade (CombatSword.prefab)
# Where the game's hold (WeaponGrips.asset) puts the blade's root, further out along the blade than the rig's sword's guard, metres: the tip, the trail
# and the numbers printed are the game's.
GAME_ROOT_OUT = 0.18
MOST_BEND = 140.0              # an elbow folds about 145
MOST_TURN = 90.0               # a forearm turns the hand about a quarter of a turn either way from the handshake
MOST_RADIAL, MOST_ULNAR = 20.0, 30.0
MOST_PALM = 35.0               # the wrist kept near straight: well inside its 70-80 degrees either way


def arm(flex, abduct, twist, bend, turn, radial=0.0, palm=0.0):
    """A pose of the sword arm, in degrees (see above)."""
    if not 0.0 <= bend <= MOST_BEND or abs(turn) > MOST_TURN or not -MOST_ULNAR <= radial <= MOST_RADIAL or abs(palm) > MOST_PALM:
        raise ValueError("no arm bends %.0f, turns its hand %.0f, or bends its wrist %.0f/%.0f" % (bend, turn, radial, palm))
    return {"flex": flex, "abduct": abduct, "twist": twist, "bend": bend, "turn": turn, "radial": radial, "palm": palm}


def in_body(spec):
    """A pose of the arm held in the body's frame rather than on the chest: the carry. The hand stays where the pose puts it from the shoulder as
    the body stands ready, however the body turns and sinks: the elbow finds its place, and the hand turns with the forearm, the wrist as keyed.
    The chest still turning back after a cut swung an arm keyed on it 10 cm a frame as the attack handed the body back to the carry, which is the
    body's own."""
    return dict(spec, frame="body")


def plane(across, offset=(0.0, 0.0, 0.0)):
    """A cut's plane: through the game camera's eye, running `across` the screen (the body's frame, Unity axes), and through the right shoulder as it is
    at the hit, moved by `offset`."""
    return {"across": Vector(across), "offset": Vector(offset)}


# THE SWORD AT REST IN THE HAND, as the game carries it (the carry the attack starts from and goes back to): the arm hanging at the side, the
# elbow soft, the wrist tipped to the little finger so the blade points ahead and a little down. (Started and ended with the blade up ahead, the
# arm swung through the difference as the clip faded in and out.)
def ready():
    return in_body(arm(8.0, 8.0, 0.0, 15.0, 0.0, -30.0))


# Each attack: its plane, and its arm at its keys' moments (sword_moves_pose.attack): ready, [rising,] wound up, the hold's end, the contact, the
# follow-through's end, held through the settle, a moment on the way back, ready.
PLANES_1H = {
    # From over the right shoulder down to the lower left at 45 degrees on the screen (owner, 2026-11-17: "make the first swing with the combat sword more
    # diagonal like the red line"; it was 60 degrees, across (-0.5, -0.87)).
    "sword1h_slash": plane((-0.71, -0.71, 0.0), (0.15, 0.1, 0.0)),
    "sword1h_cut": plane((0.68, -0.73, 0.0)),                        # over the left shoulder down to the lower right
}


# The keys on a cut were found with the blade on its plane, the edge leading and the hand where the comment says, then read and kept as numbers.
def slash_arms():
    return [
        ready(),
        arm(10.0, 60.0, -40.0, 135.0, 80.0, -1.0),       # wound: the hand up by the right shoulder, the elbow out and down, the blade standing up
        arm(10.0, 60.0, -40.0, 137.0, 80.0, -1.0),       # (the owner's sketch)
        arm(83.0, 6.0, 14.0, 5.0, 11.0, -25.0),          # the hit: the arm reaching forward and down, the blade ahead
        arm(55.0, 5.0, 10.0, 15.0, 40.0, -20.0),         # carried through low to the left, the wrist snapped toward the little finger
        arm(55.0, 5.0, 10.0, 15.0, 40.0, -20.0),
        arm(30.0, 20.0, 0.0, 35.0, -10.0),               # on the way back, the arm out to the side clear of the belly
        ready(),
    ]


# The second cut ends as the owner showed with his own arm (2026-11-15, photos: "IT NEEDS TO BE LIKE THE HAND POSITION IN THE SECOND IMAGE ITS JUST A
# DIAGONAL SLASH"): the arm reaching down to the right, the back of the hand up and the knuckles leading, the wrist straight, the blade trailing back
# up to the left across the front. (The game's arm had ended it with the forearm turned over and the fingers facing back.) Its follow-through carries
# the tip as far across the screen as the first cut's (owner, 2026-11-17: "make the second swing follow through the same distance as the first slash.
# right now the second slash stops halfway through"): the arm goes on down to the right past the hip, the wrist snapped toward the little finger.
def cut_arms():
    return [
        ready(),
        arm(69.0, -20.0, 48.0, 122.0, -84.0, 4.0),       # wound: the hand by the left ear, the blade back over the left shoulder
        arm(69.0, -20.0, 48.0, 124.0, -84.0, 4.0),
        arm(76.0, -5.0, 0.0, 14.0, -41.0, -11.0),        # the hit: the arm reaching forward and down to the right, the back of the hand up
        arm(42.0, 6.0, -14.0, 5.0, -48.0, -25.0),        # carried through low to the right past the hip
        arm(42.0, 6.0, -14.0, 5.0, -48.0, -25.0),
        arm(25.0, 15.0, 0.0, 35.0, 10.0),                # on the way back, the arm out to the side clear of the belly
        ready(),
    ]


# Each cut's first key on its plane, where the trail starts (TRAIL_FROM): the arm swinging out of the wind-up, the blade already on its way down, so the
# trail goes one way from its first frame. (The arm already straight out ahead swept across the chest in the two frames from the hold to it; the blade
# still back over the shoulder rose before it fell, and the trail turned back on itself.)
INTO_1H = {
    "sword1h_slash": arm(72.0, 18.0, 4.0, 22.0, 29.0, -21.0),
    "sword1h_cut": arm(40.0, 1.0, 42.0, 95.0, -59.0, -17.0),
}

# The attacks given an arm here (the others keep the game's own arm until they are posed).
ARMS_1H = {
    "sword1h_slash": slash_arms(),
    "sword1h_cut": cut_arms(),
}

# The bones the one-handed clips give the game besides the body's (sword_moves_build.py writes them to SwordMotion1H.json).
ARM_BONES_1H = ("arm_r", "forearm_r", "hand_r")

L_UPPER = (HEAD["forearm_r"] - HEAD["arm_r"]).length
L_FOREARM = (HEAD["hand_r"] - HEAD["forearm_r"]).length
UPPER_REST = unit(HEAD["forearm_r"] - HEAD["arm_r"])
HAND_ALONG_REST = unit(TAIL["hand_r"] - HEAD["hand_r"])
THUMB_REST = unit(FWD - HAND_ALONG_REST * FWD.dot(HAND_ALONG_REST))     # the arm hangs with the thumb forward
EDGE_IN_REST = EDGE_REST           # the edge toward the knuckles (SwordWeapon.DefaultOneHandGripRotation): off the blade toward the fingertips
SWORD_ROOT_REST = _sw.translation.copy()
TORSO_RADIUS = 0.26
HEAD_RADIUS = 0.35
HEAD_CENTRE_REST = U(0.0, HEAD_CENTRE_Y, 0.0) - HEAD["head"]

# The game's camera at rest, in the body's frame (ThirdPersonCamera: pivot 0.73 of 2.6 m, 0.87 m to the right and 0.6 m over it, 6 m back, 12 degrees
# down).
CAMERA_PITCH = math.radians(12.0)
CAMERA_EYE = Vector((0.87, 0.73 * 2.6 + 0.6 + 6.0 * math.sin(CAMERA_PITCH), -6.0 * math.cos(CAMERA_PITCH)))


def blender(v):
    return U(v.x, v.y, v.z)


def arm_lines(spec):
    """The upper arm's line, its front (where the elbow bends to), the forearm's line, the hand's and the thumb's, in the chest's frame (Unity axes)."""
    raised = Quaternion(Vector((-1.0, 0.0, 0.0)), math.radians(spec["flex"])) @ Quaternion(Vector((0.0, 0.0, 1.0)), math.radians(spec["abduct"]))
    upper = raised @ Vector((0.0, -1.0, 0.0))
    front = raised @ Vector((0.0, 0.0, 1.0))                                   # where the forearm bends to, hanging: forward
    front = Quaternion(upper, -math.radians(spec["twist"])) @ front            # the upper arm turned: + its front outward
    b = math.radians(spec["bend"])
    forearm = upper * math.cos(b) + front * math.sin(b)
    handshake = -upper * math.sin(b) + front * math.cos(b)                    # the thumb toward the way the elbow bends
    thumb = Quaternion(forearm, -math.radians(spec["turn"])) @ handshake
    # The wrist: toward the thumb, then toward the palm (a right hand's palm faces along x thumb in these axes).
    r, p = math.radians(spec["radial"]), math.radians(spec["palm"])
    along = forearm * math.cos(r) + thumb * math.sin(r)
    thumb = thumb * math.cos(r) - forearm * math.sin(r)
    along = along * math.cos(p) + along.cross(thumb) * math.sin(p)
    return upper.normalized(), front.normalized(), forearm.normalized(), along.normalized(), thumb.normalized()


def signed_angle(a, b, axis):
    a = a - axis * a.dot(axis)
    b = b - axis * b.dot(axis)
    if a.length < 1e-6 or b.length < 1e-6:
        return 0.0
    return math.degrees(math.atan2(a.normalized().cross(b.normalized()).dot(axis), a.normalized().dot(b.normalized())))


def spec_of(lines, wrist, chest):
    """The joints of an arm given as its lines in Blender's frame (upper, front, forearm, along, thumb) with the wrist bent `wrist` toward the thumb:
    arm_lines' inverse, in the chest's frame."""
    upper, front, forearm, along, thumb = (to_unity(chest.inverted() @ v) for v in lines)
    abduct = math.degrees(math.asin(max(-1.0, min(1.0, upper.x))))
    flex = math.degrees(math.atan2(upper.z, -upper.y))
    raised = Quaternion(Vector((-1.0, 0.0, 0.0)), math.radians(flex)) @ Quaternion(Vector((0.0, 0.0, 1.0)), math.radians(abduct))
    twist = -signed_angle(raised @ Vector((0.0, 0.0, 1.0)), front, upper)
    bend = math.degrees(upper.angle(forearm))
    b = math.radians(bend)
    front = Quaternion(upper, -math.radians(twist)) @ (raised @ Vector((0.0, 0.0, 1.0)))
    handshake = -upper * math.sin(b) + front * math.cos(b)
    r = math.radians(wrist)
    thumb0 = thumb * math.cos(r) + along * math.sin(r)
    turn = -signed_angle(handshake, thumb0, forearm)
    return {"flex": flex, "abduct": abduct, "twist": twist, "bend": bend, "turn": turn, "radial": wrist, "palm": 0.0}


# ---- THE CUT'S PLANE ---------------------------------------------------------------------------------------------------------------------------------------------------------
_PLANES = {}


def body_at(name, seconds):
    """The body (its bones' turns and places) at a moment of the attack, without the arm."""
    D, pos = solve(pose_at(ATTACKS[name], seconds, {"reach": 0.0}))
    return D, pos


def plane_of(name):
    """The attack's cut plane in the body's frame (Unity axes): its two directions (A: the camera's line of sight into it; B: across, the way the cut
    goes) and its normal, through the eye."""
    if name not in _PLANES:
        spec = PLANES_1H[name]
        _, pos = body_at(name, ATTACKS[name]["contact"])
        through = to_unity(pos["arm_r"]) + spec["offset"]
        A = (through - CAMERA_EYE).normalized()
        B = spec["across"] - A * spec["across"].dot(A)
        B.normalize()
        _PLANES[name] = {"A": A, "B": B, "normal": A.cross(B)}
    return _PLANES[name]


def in_plane(pl, degrees):
    r = math.radians(degrees)
    return pl["A"] * math.cos(r) + pl["B"] * math.sin(r)


# The outer part of the blade the trail is drawn from (BladeTrail.InnerFraction) to the tip.
TRAIL_INNER = 0.55


def _onto(point, s, k, eye, n):
    """The turn about the axis `k` through `s` (radians, the least) that brings `point` onto the plane through `eye` with normal `n`."""
    v = point - s
    along = k * k.dot(v)
    across = v - along
    a, b, c = across.dot(n), k.cross(across).dot(n), (s - eye).dot(n) + along.dot(n)
    r = math.hypot(a, b)
    if r < 1e-6:
        return 0.0
    base = math.atan2(b, a)
    x = max(-1.0, min(1.0, -c / r))
    turns = [base + math.acos(x), base - math.acos(x)]
    return min(((t + math.pi) % (2.0 * math.pi) - math.pi for t in turns), key=abs)


def onto_the_plane(name, lines, pos, share=1.0):
    """The arm turned at the shoulder, as little as it takes, to put the outer part of its blade on the cut's plane: first about the line across the
    plane through the shoulder (the tip onto it), then about the line from the shoulder to the tip (the rest of the outer blade); `share` of that
    turn."""
    pl = plane_of(name)
    n, eye, s = blender(pl["normal"]), blender(CAMERA_EYE), pos["arm_r"]
    upper, front, forearm, along, thumb = lines
    hand = delta(HAND_ALONG_REST, THUMB_REST, along, thumb)
    root = s + upper * L_UPPER + forearm * L_FOREARM + hand @ (SWORD_ROOT_REST - HEAD["hand_r"])
    blade = hand @ BLADE_REST
    tip, inner = root + blade * (GAME_ROOT_OUT + COMBAT_BLADE), root + blade * (GAME_ROOT_OUT + COMBAT_BLADE * TRAIL_INNER)
    k = (tip - s).cross(n).normalized()
    first = Quaternion(k, _onto(tip, s, k, eye, n))
    tip, inner = s + first @ (tip - s), s + first @ (inner - s)
    k = (tip - s).normalized()
    turn = Quaternion().slerp(Quaternion(k, _onto(inner, s, k, eye, n)) @ first, share)
    return tuple(turn @ v for v in lines)


_READY_SHOULDER = {}


def ready_shoulder(name):
    """Where the right shoulder is as the attack's body stands ready (its first moment): what a pose held in the body's frame is measured from."""
    if name not in _READY_SHOULDER:
        _READY_SHOULDER[name] = body_at(name, 0.0)[1]["arm_r"].copy()
    return _READY_SHOULDER[name]


def on_the_chest(name, key, D, pos):
    """A key's joints on the chest as it is now: as keyed, or (a pose held in the body's frame) the joints that put the hand where the pose has it,
    turned as it has it, from the shoulder where it is now."""
    if key.get("frame") != "body":
        return key
    pose = {k: v for k, v in key.items() if k != "frame"}
    upper, front, forearm, along, thumb = (blender(v) for v in arm_lines(pose))
    reference = ready_shoulder(name)
    elbow_then = reference + upper * L_UPPER
    hand_at = elbow_then + forearm * L_FOREARM
    elbow, wrist = two_bone(pos["arm_r"], hand_at, L_UPPER, L_FOREARM, elbow_then - (reference + hand_at) * 0.5)
    upper_now, forearm_now = unit(elbow - pos["arm_r"]), unit(wrist - elbow)
    with_forearm = forearm.rotation_difference(forearm_now)
    bend = forearm_now - upper_now * forearm_now.dot(upper_now)
    front = unit(bend) if bend.length > 0.02 else with_forearm @ front
    return spec_of((upper_now, front, forearm_now, with_forearm @ along, with_forearm @ thumb), pose["radial"], D["spine"])


def eased_arm(a, b, u):
    return {k: a[k] + (b[k] - a[k]) * u for k in a}


def chest_lines(spec, D):
    return tuple(D["spine"] @ blender(v) for v in arm_lines(spec))


# The share of the strike (from the hold's end to the contact) after which the trail is drawn: SwordStrikes.ActiveStart's.
TRAIL_FROM = 0.25


def trail_starts(name):
    """When the cut's first key on its plane is: a frame before the trail is drawn. The game plays the clip between its frames, so the trail's first
    moment falls between two of them, and the one before it must already be on the way (from a key at the moment itself, the tip still rose on the
    screen into the start of the trail, and it turned back on itself)."""
    move = ATTACKS[name]
    return move["held"] + (move["contact"] - move["held"]) * TRAIL_FROM - 1.0 / FPS


# The arm is back in the carry this long before the attack ends, and holds it while the body settles: the game hands the arm back to the carry as
# the attack ends, and an arm still coming down then went on 9 cm a frame and stopped dead.
ARM_HOME_EARLY = 0.04


def arm_keys(name):
    """The attack's arm keys as (seconds, pose, ease): the body's keys' moments, the moment the trail starts, which the cut's first key is at, and
    the carry reached ARM_HOME_EARLY before the end."""
    move = ATTACKS[name]
    keys = move["keys"]
    arms = ARMS_1H[name]
    if len(arms) != len(keys):
        raise ValueError("%s has %d keys and %d arms" % (name, len(keys), len(arms)))
    out = [(t, a, ease) for (t, _, ease), a in zip(keys, arms)]
    into = trail_starts(name)
    at = next(i for i, (t, _, _) in enumerate(out) if t > into)
    out.insert(at, (into, INTO_1H[name], "linear"))
    end, home, ease = out[-1]
    out[-1] = (end - ARM_HOME_EARLY, home, ease)
    out.append((end, home, "linear"))
    return out


def onto_the_plane_share(name, seconds):
    """How much of the turn onto the plane the arm takes at `seconds`: all of it while the trail is drawn and the follow-through is held, taken on
    over the hold's end and given back over the way back to ready (switched on and off, the arm jumped a frame's worth of the turn)."""
    move = ATTACKS[name]
    into = trail_starts(name)
    back = move["settled"] + (move["seconds"] - move["settled"]) * 0.55
    if seconds < into:
        return eased("smooth", (seconds - move["held"]) / (into - move["held"]))
    if seconds <= move["settled"]:
        return 1.0
    return 1.0 - eased("smooth", (seconds - move["settled"]) / (back - move["settled"]))


def arm_at(name, seconds, D, pos):
    """The arm of attack `name` at `seconds` as its lines in Blender's frame, with the body posed as `D`, `pos`: every joint eased from key to key, as
    the body's keys are, and through the cut turned at the shoulder onto the cut's plane."""
    keys = arm_keys(name)
    i = 0
    while i < len(keys) - 2 and seconds >= keys[i + 1][0]:
        i += 1
    ta, a, _ = keys[i]
    tb, b, ease_kind = keys[i + 1]
    spec = eased_arm(on_the_chest(name, a, D, pos), on_the_chest(name, b, D, pos),
                     eased(ease_kind, max(0.0, min(1.0, (seconds - ta) / max(tb - ta, 1e-4)))))
    lines = chest_lines(spec, D)
    share = onto_the_plane_share(name, seconds)
    if share > 0.0:
        lines = onto_the_plane(name, lines, pos, share)
    return lines, spec["radial"]


def off_the_body(point, pos, D):
    """How far `point` is outside a round torso from the hips to the neck and outside the head, metres (below 0 inside)."""
    a, b = pos["hips"], pos["neck"]
    ab = b - a
    t = max(0.0, min(1.0, (point - a).dot(ab) / ab.length_squared))
    torso = (point - (a + ab * t)).length - TORSO_RADIUS
    head = (point - (pos["head"] + D["head"] @ HEAD_CENTRE_REST)).length - HEAD_RADIUS
    return min(torso, head)


def on_screen(point_unity):
    """Where the game's camera at rest sees a point of the body's frame (Unity axes), on its image plane."""
    d = point_unity - CAMERA_EYE
    forward = Vector((0.0, -math.sin(CAMERA_PITCH), math.cos(CAMERA_PITCH)))
    up = Vector((0.0, math.cos(CAMERA_PITCH), math.sin(CAMERA_PITCH)))
    depth = max(d.dot(forward), 0.1)
    return Vector((d.x / depth, d.dot(up) / depth))


def pose_sword_arm(D, pos, arm_now, stats, seconds):
    """Sets the right arm's bones in D (Blender's frame, turns from the rest) for `arm_now` (arm_at's: the arm's lines and the wrist's bend), and
    records the numbers to check it by."""
    (upper, front, forearm, along, thumb), wrist = arm_now
    D_upper = delta(UPPER_REST, FWD, upper, front)
    D_forearm = upper.rotation_difference(forearm) @ D_upper
    hand = delta(HAND_ALONG_REST, THUMB_REST, along, thumb)
    # The hand's turn on the forearm, shared half and half between the forearm and the wrist (the skin keeps its thickness).
    q = D_forearm.inverted() @ hand
    turn = 2.0 * math.degrees(math.atan2(Vector((q.x, q.y, q.z)).dot(HAND_ALONG_REST), q.w))
    turn = (turn + 180.0) % 360.0 - 180.0
    D_forearm = D_forearm @ Quaternion(HAND_ALONG_REST, math.radians(turn * 0.5))
    elbow = pos["arm_r"] + upper * L_UPPER
    wrist_at = elbow + forearm * L_FOREARM
    D["arm_r"], D["forearm_r"], D["hand_r"], D["fingertip_r"] = D_upper, D_forearm, hand, hand
    pos["forearm_r"], pos["hand_r"] = elbow, wrist_at
    pos["fingertip_r"] = wrist_at + hand @ (HEAD["fingertip_r"] - HEAD["hand_r"])

    blade = unit(hand @ BLADE_REST)
    edge = unit(hand @ EDGE_IN_REST)
    root = wrist_at + hand @ (SWORD_ROOT_REST - HEAD["hand_r"]) + blade * GAME_ROOT_OUT
    tip = root + blade * COMBAT_BLADE
    blade_room = min(off_the_body(root + blade * (COMBAT_BLADE * s), pos, D) for s in (0.2, 0.4, 0.6, 0.8, 1.0))
    low = min((root + blade * (COMBAT_BLADE * s)).z for s in (0.2, 0.6, 1.0))
    rows = stats.setdefault("rows", [])
    lead = None
    if rows and rows[-1]["seconds"] < seconds:
        moved = tip - rows[-1]["tip"]
        across = moved - blade * moved.dot(blade)
        if across.length > 0.02:
            lead = unit(across).dot(edge)
    spec = spec_of((upper, front, forearm, along, thumb), wrist, D["spine"])
    rows.append({"seconds": seconds, "turn": turn, "spec": spec, "hand": to_unity(wrist_at - pos["arm_r"]), "tip": tip, "lead": lead,
                 "screen": on_screen(to_unity(tip)), "blade": to_unity(blade), "elbow_room": off_the_body(elbow, pos, D),
                 "hand_room": off_the_body(wrist_at, pos, D), "blade_room": blade_room, "tip_low": low})


def trail_bow(points):
    """How far a trail on the screen strays from the straight line between its ends, as a share of that line's length."""
    if len(points) < 3:
        return 0.0
    a, b = points[0], points[-1]
    chord = b - a
    if chord.length < 1e-6:
        return 0.0
    n = Vector((-chord.y, chord.x)).normalized()
    return max(abs((p - a).dot(n)) for p in points) / chord.length


def report_arm(name, stats):
    """Prints the arm at each of the attack's keys and the worst of it through the whole attack; how the edge leads through the strike, and how
    straight its trail looks from the game's camera."""
    rows = stats.get("rows", [])
    if not rows:
        return
    move = ATTACKS[name]
    for t, _, _ in move["keys"]:
        r = min(rows, key=lambda row: abs(row["seconds"] - t))
        h, bl, s = r["hand"], r["blade"], r["spec"]
        print("   %-16s %.2f s: arm(%4.0f %4.0f %4.0f | bend %3.0f turn %4.0f wrist %3.0f) hand (%+.2f %+.2f %+.2f) blade (%+.2f %+.2f %+.2f) | room: "
              "elbow %+.2f hand %+.2f blade %+.2f | tip %+.2f" % (name, r["seconds"], s["flex"], s["abduct"], s["twist"], s["bend"], s["turn"], s["radial"],
                                                                   h.x, h.y, h.z, bl.x, bl.y, bl.z, r["elbow_room"], r["hand_room"], r["blade_room"],
                                                                   r["tip_low"]))
    strike = [r["lead"] for r in rows if move["held"] <= r["seconds"] <= move["contact"] + 0.02 and r["lead"] is not None]
    print("   %-16s the edge through the strike, frame by frame: %s" % (name, " ".join("%+.1f" % r["lead"] if r["lead"] is not None else "."
                                                                              for r in rows if move["wound"] <= r["seconds"] <= move["followed"])))
    into = move["held"] + (move["contact"] - move["held"]) * TRAIL_FROM
    trail = [r["screen"] for r in rows if into <= r["seconds"] <= move["followed"]]
    way = (trail[-1] - trail[0]).normalized() if len(trail) > 1 else Vector((0.0, 0.0))
    back = sum(1 for a, b in zip(trail, trail[1:]) if (b - a).dot(way) < -1e-4)
    worst_twist = max(abs(r["spec"]["twist"]) for r in rows)
    for key in ("elbow_room", "hand_room", "blade_room"):
        r = min(rows, key=lambda row: row[key])
        if r[key] < 0.06:
            s = r["spec"]
            print("   %-16s %s %.2f at %.2f s: arm(%.0f %.0f %.0f | bend %.0f turn %.0f wrist %.0f)" % (name, key, r[key], r["seconds"], s["flex"], s["abduct"],
                                                                                                    s["twist"], s["bend"], s["turn"], s["radial"]))
    print("   %-16s WORST: room: elbow %.2f hand %.2f blade %.2f | tip %.2f | the edge leads the strike %s | the trail bows %.0f%%, goes back on %d frames | twist %.0f bend %.0f..%.0f"
          % (name, min(r["elbow_room"] for r in rows), min(r["hand_room"] for r in rows), min(r["blade_room"] for r in rows),
             min(r["tip_low"] for r in rows), ("at worst %.2f (1 = square on)" % min(strike)) if strike else "(no motion)", 100.0 * trail_bow(trail), back,
             worst_twist, min(r["spec"]["bend"] for r in rows), max(r["spec"]["bend"] for r in rows)))
