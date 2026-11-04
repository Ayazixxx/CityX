# THE GLOCK'S RELOADS, as key specs for firearm_pose.py (owner, 2026-10-14: "realistic reload animations"). From the research
# (docs/THIRD_PERSON_PISTOL.md): the gun comes into the WORKSPACE in front of the chest, canted so the magazine well faces the support hand; the
# strong thumb presses the release and the magazine drops; the support hand fetches a fresh one from the left hip, seats it with the heel of the
# palm (the click is the "reload complete" moment: the ammo is in), and on an EMPTY gun comes over the top of the slide and racks it. Then back
# to the two-handed hold. Tactical 84 frames (1.4 s), empty 104 (1.73 s) at 60 fps.
#
# exec'd after firearm_pose.py (glock_build_all.py). Unity axes; the gun's frame has its origin at the web of the grip (glock_model.py WEB).

from mathutils import Vector, Quaternion
import math

# The Glock's own hold, measured against the hand: the game's FirearmProfile.HoldPosition and HoldDipDegrees (the rifles keep firearm_pose.py's).
HOLD_POS = Vector((-0.056, -0.0795, 0.037))
HOLD_DIP = -22.0
HOLD_ROT = hold_rotation()

P = POINTS
MAG_MOUTH = Vector(P["mag_mouth"])
MAG_OUT = Vector(P["mag_axis_out"])          # unit, down the grip
MAG_TRAVEL = P["mag_travel"]
SLIDE_TRAVEL = P["slide_travel"]
SLIDE_REAR_TOP = Vector(P["slide_rear_top"])

# The magazine in the support hand (the LEFT hand's frame: fingers run -x, the palm faces -y, the thumb's side is +z): its top (the well-mouth
# point, its origin) past the palm toward the fingertips, its body lying back along the forearm, the front strap against the palm, the flat
# right side toward the thumb (see firearm_pose.hand_holding).
MAG_IN_HAND = (Vector((-0.25, -0.08, 0.013)), rot_cols((0, 0, 1), (-1, 0, 0), (0, -1, 0)))   # the mag's +x, +y, +z in the hand's axes


def mag(travel=0.0, visible=1, in_hand=0):
    """The magazine's spec: `travel` 0 seated .. 1 fully out of the grip."""
    return {"pos": tuple(MAG_OUT * (MAG_TRAVEL * travel)), "rot": Quaternion(), "visible": visible, "in_hand": in_hand, "hand": MAG_IN_HAND}


def slide(back=0.0):
    return {"pos": (0.0, 0.0, -SLIDE_TRAVEL * back), "rot": Quaternion(), "visible": 1}


def trigger():
    return {"pos": (0, 0, 0), "rot": Quaternion(), "visible": 1}


def props(mag_spec, slide_back=0.0):
    return {"Magazine": mag_spec, "Slide": slide(slide_back), "Trigger": trigger()}


# ---- the hands on the gun -------------------------------------------------------------------------------------------------------------------
# The support hand's two-handed hold (gun frame): the palm's heel on the left grip panel below the strong fingers, the fingers wrapped over
# them, the thumb forward along the frame. The wrist sits left of and below the web.
SUPPORT_ON_GUN = {"at": (-0.0795, -0.135, 0.053), "fingers": (0.0, -0.375, 0.927), "palm": (1.0, 0.0, 0.0), "space": "gun", "pole": (-0.8, -0.9, -0.2)}

# Over the top of the slide for the rack: the palm down on the rear serrations, the fingers over the left side, the thumb on the right.
OVERHAND = {"at": (0.0, SLIDE_REAR_TOP.y + 0.045, SLIDE_REAR_TOP.z + 0.09), "fingers": (-0.85, -0.5, -0.1), "palm": (0.0, -1.0, 0.0), "space": "gun", "pole": (-0.5, -0.4, -0.9)}


def overhand_back(back):
    o = dict(OVERHAND)
    o["at"] = (OVERHAND["at"][0], OVERHAND["at"][1], OVERHAND["at"][2] - SLIDE_TRAVEL * back)
    return o


def hand_with_mag(travel):
    """The support hand holding the magazine at `travel` below the well (gun frame)."""
    return dict(hand_holding(MAG_MOUTH + Vector(mag(travel)["pos"]), Quaternion(), MAG_IN_HAND), pole=(-0.9, -0.7, 0.1))


# ---- the gun in the chest's frame ----------------------------------------------------------------------------------------------------------
READY_GUN = {"at": (0.24, 0.05, 0.5), "fwd": (0.0, 0.0, 1.0), "up": (0.0, 1.0, 0.0)}
# The workspace: in close, muzzle up, the top rolled OUTBOARD (to the right) so the grip's bottom and the well swing toward the support hand.
WORK_GUN = {"at": (0.06, 0.0, 0.40), "fwd": (-0.10, 0.42, 0.90), "up": (0.55, 0.78, -0.30)}
INSERT_GUN = {"at": (0.04, 0.03, 0.38), "fwd": (-0.12, 0.55, 0.82), "up": (0.58, 0.70, -0.40)}
RACK_GUN = {"at": (0.04, 0.04, 0.42), "fwd": (-0.08, 0.30, 0.95), "up": (0.35, 0.92, -0.15)}


def spec(gun, l_hand, fingers_r, fingers_l, props_, spine=0.0, head_down=0.0, r_pole=(0.6, -1.0, -0.2)):
    return {
        "hips": (0.0, 0.9, 0.0), "body": Quaternion(),
        "spine": rot(y=spine), "neck": rot(p=head_down * 0.4, y=-spine * 0.5), "head": rot(p=head_down * 0.6),
        "gun": gun, "r_pole": r_pole, "l_hand": l_hand,
        "fingers_r": fingers_r, "fingers_l": fingers_l, "props": props_,
    }


READY = spec(READY_GUN, SUPPORT_ON_GUN, PISTOL_STRONG, PISTOL_SUPPORT, props(mag()), spine=15.0)
THUMB_ON_RELEASE = dict(PISTOL_STRONG, thumb=1.0, thumb_forward=5.0, thumb_authored=0.0)
TO_HIP = {"at": (-0.22, -0.45, 0.22), "fingers": (-0.3, -0.9, 0.3), "palm": (0.8, -0.3, 0.5), "space": "chest", "pole": (-0.9, -0.6, -0.3)}
AT_POUCH = {"at": (-0.27, -0.70, 0.10), "fingers": (-0.2, -0.95, 0.2), "palm": (0.9, 0.1, 0.4), "space": "chest", "pole": (-0.9, -0.5, -0.4)}
# After the seat the palm heel pushes on up, then the hand leaves below the grip.
AFTER_SEAT = {"at": (-0.03, -0.46, -0.10), "fingers": (0.2, 0.9, 0.3), "palm": (0.1, 1.0, 0.0), "space": "gun", "pole": (-0.9, -0.7, 0.1)}


def tactical_keys(start=0):
    f = start
    return [
        (f + 0, READY, "inout"),
        (f + 8, spec(WORK_GUN, TO_HIP, THUMB_ON_RELEASE, OPEN_HAND, props(mag(0.0)), spine=-8, head_down=18), "out"),
        (f + 12, spec(WORK_GUN, TO_HIP, THUMB_ON_RELEASE, OPEN_HAND, props(mag(0.0)), spine=-8, head_down=18), "in"),
        (f + 18, spec(WORK_GUN, TO_HIP, PISTOL_STRONG, OPEN_HAND, props(mag(0.55, visible=0)), spine=-10, head_down=20), "inout"),   # drop: the mag is clear (the game throws it)
        (f + 30, spec(WORK_GUN, AT_POUCH, PISTOL_STRONG, OPEN_HAND, props(mag(1.0, visible=0)), spine=-10, head_down=20), "inout"),
        (f + 32, spec(WORK_GUN, AT_POUCH, PISTOL_STRONG, MAG_HOLD, props(mag(1.0, 1, 1)), spine=-10, head_down=20), "inout"),      # grab: the fresh mag is in the hand from here
        (f + 52, spec(INSERT_GUN, hand_with_mag(0.95), PISTOL_STRONG, MAG_HOLD, props(mag(0.95, 1, 1)), spine=-8, head_down=22), "out"),
        (f + 60, spec(INSERT_GUN, hand_with_mag(0.0), PISTOL_STRONG, MAG_HOLD, props(mag(0.0, 1, 1)), spine=-6, head_down=20), "in"),      # seat
        (f + 66, spec(INSERT_GUN, AFTER_SEAT, PISTOL_STRONG, OPEN_HAND, props(mag(0.0)), spine=-4, head_down=14), "inout"),
        (f + 84, READY, "linear"),
    ]


def empty_keys(start):
    keys = tactical_keys(start)[:-1]           # up to and including the push after the seat
    f = start
    keys += [
        (f + 74, spec(RACK_GUN, overhand_back(0.0), PISTOL_STRONG, fingers(0.8, 0.8, 0.8, 0.7, 0.8, thumb_forward=10.0), props(mag(0.0)), spine=-5, head_down=12), "inout"),
        (f + 82, spec(RACK_GUN, overhand_back(1.0), PISTOL_STRONG, fingers(0.8, 0.8, 0.8, 0.7, 0.8, thumb_forward=10.0), props(mag(0.0), 1.0), spine=-5, head_down=12), "out"),
        (f + 84, spec(RACK_GUN, overhand_back(1.15), PISTOL_STRONG, OPEN_HAND, props(mag(0.0), 0.0), spine=-5, head_down=10), "linear"),   # rack: the slide flies home
        (f + 88, spec(RACK_GUN, dict(OVERHAND, at=(-0.08, 0.25, OVERHAND["at"][2] - 0.08)), PISTOL_STRONG, OPEN_HAND, props(mag(0.0)), spine=-4, head_down=8), "inout"),
        (f + 104, READY, "linear"),
    ]
    return keys


TACTICAL_START, EMPTY_START = 0, 120
KEYS = tactical_keys(TACTICAL_START) + empty_keys(EMPTY_START)
SEGMENTS = {"tactical": (TACTICAL_START, TACTICAL_START + 84), "empty": (EMPTY_START, EMPTY_START + 104)}
EVENTS = {"drop": TACTICAL_START + 18, "grab": TACTICAL_START + 32, "seat": TACTICAL_START + 60,
          "drop2": EMPTY_START + 18, "grab2": EMPTY_START + 32, "seat2": EMPTY_START + 60, "rack2": EMPTY_START + 84}
