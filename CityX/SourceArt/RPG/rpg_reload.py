# THE RPG'S RELOAD, as key specs for firearm_pose.py. A single shot: after every rocket the launcher comes off the shoulder, muzzle up, the free
# hand fetches a fresh round from the pouch at the back of the left hip, brings it round in front, lines its tail up with the muzzle and pushes it
# home (the `seat` event: loaded), goes back to the front guard, and the right thumb cocks the hammer (the `rack` event); then back onto the
# shoulder. How it is done for real (the RPG-7 crew drill, Small Arms Defense Journal's "RPG-7 system primer"): the round goes in at the muzzle,
# indexed and held by its gasket, the fuze's cap off, the hammer cocked, the safety off. Here one player does it alone, in about 2.3 s (the games:
# Call of Duty's and Battlefield's RPG-7 take 3 to 4.5 s).
#
# exec'd after firearm_pose.py (rpg_build_all.py). Unity axes; the launcher's frame has its origin at the web of the grip.

from mathutils import Vector, Quaternion
import math
import os

P = POINTS
BUTT_LOCAL = Vector(P["butt"])
SUPPORT_LOCAL = Vector(P["support"])
ROCKET_REST = Vector(P["rocket"])
ROCKET_TAIL_BACK = Vector(P["rocket"]).z - Vector(P["rocket_tail"]).z     # how far the round reaches back inside the tube
HAMMER_COCK = P["hammer_cock_degrees"]

_STANCE = os.path.join(REPO, "CityX", "SourceArt", "Firearms", "firearm_rifle_stance.py")
exec(compile(open(_STANCE).read(), _STANCE, "exec"), globals())     # BLADE, BODY_FWD, POCKET, gun_from_butt, turned, spec

# On the shoulder (what the game's aim pose holds): level along the body's forward.
READY_GUN = gun_from_butt(POCKET, BODY_FWD, (0.0, 1.0, 0.0))
# Off the shoulder to load: down at the belly, muzzle up 18 degrees and swung well to the left, so the free hand meets the muzzle in front
# without crossing the face.
LOAD_GUN = gun_from_butt(POCKET + Vector((-0.10, -0.42, 0.16)), turned(BODY_FWD, pitch_up=18.0, yaw_left=38.0), (0.0, 1.0, 0.0))
# Cocking: back up toward the shoulder, the muzzle still a little high.
COCK_GUN = gun_from_butt(POCKET + Vector((-0.01, -0.08, 0.03)), turned(BODY_FWD, pitch_up=8.0, yaw_left=3.0), (0.0, 1.0, 0.0))

# The round in the free hand (the LEFT hand's frame: fingers run -x, palm -y, thumb +z), held round the stalk behind the warhead, the warhead
# ahead of the thumb: the hand comes in from the left with its palm toward the launcher and its fingers round under the round.
_HAND_IN_GUN = rot_cols((0, 1, 0), (-1, 0, 0), (0, 0, 1))          # the hand's axes in the gun's frame when it holds the round along the tube
ROCKET_IN_HAND = (Vector((-0.09, -0.06, -0.087)), _HAND_IN_GUN.inverted())


def rocket(offset=0.0, visible=1, in_hand=0):
    """The round `offset` ahead of where it sits loaded, along the tube."""
    return {"pos": (0.0, 0.0, offset), "rot": Quaternion(), "visible": visible, "in_hand": in_hand, "hand": ROCKET_IN_HAND}


def hammer(cocked=1.0):
    """Cocked is the hammer's rest (as the model is built): fired, it lies forward."""
    return {"pos": (0.0, 0.0, 0.0), "rot": Quaternion(Vector((1.0, 0.0, 0.0)), math.radians(-HAMMER_COCK * (1.0 - cocked))), "visible": 1}


def props(rocket_spec, cocked=1.0):
    return {"Rocket": rocket_spec, "Hammer": hammer(cocked), "Trigger": {"pos": (0, 0, 0), "rot": Quaternion(), "visible": 1}}


def hand_with_rocket(offset):
    return dict(hand_holding(ROCKET_REST + Vector((0.0, 0.0, offset)), Quaternion(), ROCKET_IN_HAND), pole=(-0.9, -0.6, 0.2))


def on_guard():
    """The support hand under the front guard, palm up, the fingers curled up its right side (the shotgun's cradle, FirearmProfile.RPG)."""
    at = SUPPORT_LOCAL + Vector((-0.077, -0.0432, -0.0525))
    return {"at": tuple(at), "fingers": (0.95, 0.15, 0.1), "palm": (0.15, 1.0, 0.0), "space": "gun", "pole": (-0.7, -1.0, -0.1)}


GUARD_GRIP = fingers(0.75, 0.8, 0.8, 0.75, 1.0, thumb_forward=-50.0)   # the game's FingerAmounts.ForendCradle
HOLD_ROUND = fingers(0.85, 0.9, 0.9, 0.9, 0.8, thumb_forward=15.0)
FLAT = fingers(0.05, 0.05, 0.05, 0.05, 0.1)
THUMB_BACK = dict(RIFLE_STRONG, thumb=0.2, thumb_forward=-20.0)         # the right thumb up and back over the hammer

# The pouch at the back of the left hip, and the hand on its way there.
TO_POUCH = {"at": (-0.30, -0.42, 0.05), "fingers": (0.1, -0.9, -0.3), "palm": (0.9, 0.0, -0.3), "space": "chest", "pole": (-0.9, -0.3, 0.3)}
AT_POUCH = {"at": (-0.40, -0.62, -0.16), "fingers": (0.1, -0.9, -0.3), "palm": (0.9, 0.0, -0.3), "space": "chest", "pole": (-0.9, -0.2, 0.4)}
LOADED_OUT = ROCKET_TAIL_BACK + 0.05          # the round held just clear of the muzzle, its tail lined up with it


def reload_keys(f):
    return [
        (f + 0, spec(READY_GUN, on_guard(), GUARD_GRIP, props(rocket(visible=0), 0.0)), "inout"),
        (f + 16, spec(LOAD_GUN, TO_POUCH, FLAT, props(rocket(visible=0), 0.0), head_down=6), "inout"),
        (f + 34, spec(LOAD_GUN, AT_POUCH, FLAT, props(rocket(visible=0), 0.0), head_down=10), "linear"),
        (f + 38, spec(LOAD_GUN, AT_POUCH, HOLD_ROUND, props(rocket(LOADED_OUT, 1, 1), 0.0), head_down=10), "inout"),           # grab: the round in hand
        (f + 62, spec(LOAD_GUN, hand_with_rocket(LOADED_OUT), HOLD_ROUND, props(rocket(LOADED_OUT, 1, 1), 0.0), head_down=14), "inout"),  # lined up
        (f + 84, spec(LOAD_GUN, hand_with_rocket(0.0), HOLD_ROUND, props(rocket(0.0, 1, 1), 0.0), head_down=14), "linear"),       # pushed home: seat
        (f + 86, spec(LOAD_GUN, hand_with_rocket(0.0), FLAT, props(rocket(), 0.0), head_down=12), "inout"),
        (f + 100, spec(COCK_GUN, on_guard(), GUARD_GRIP, props(rocket(), 0.0), head_down=6), "inout"),
        (f + 106, dict(spec(COCK_GUN, on_guard(), GUARD_GRIP, props(rocket(), 0.0), head_down=6), fingers_r=THUMB_BACK), "inout"),
        (f + 112, dict(spec(COCK_GUN, on_guard(), GUARD_GRIP, props(rocket(), 1.0), head_down=6), fingers_r=THUMB_BACK), "inout"),   # rack: cocked
        (f + 140, spec(READY_GUN, on_guard(), GUARD_GRIP, props(rocket(), 1.0)), "linear"),
    ]


START = 0
KEYS = reload_keys(START)
# One reload, empty or not (a single shot is always empty when it is reloaded): both names the game asks for play the same frames.
SEGMENTS = {"tactical": (START, START + 140), "empty": (START, START + 140)}
SEGMENT_NAMES = ("tactical", "empty")
EVENTS = {"grab": START + 38, "seat": START + 84, "rack": START + 112}
