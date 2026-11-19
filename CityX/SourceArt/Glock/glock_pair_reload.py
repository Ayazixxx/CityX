# A PAIR OF GLOCKS RELOADED (Dual Pistols, owner 2026-11-19: docs/DUAL_PISTOLS.md), as key specs for firearm_pose.py. A gun in each hand and
# no free hand, so each gun is reloaded against the body, the one-handed way: both magazines are dropped together (muzzles up, the thumbs on
# the releases), both guns go down to the hips, where a fresh magazine stands in a pouch on each side of the belt, and each gun's well is pushed
# straight down onto its magazine (the seat: both at once, the ammo in). On an empty pair each gun's slide is then hooked on the front of the
# belt and the gun pushed down, which racks it. Then both back up into the akimbo stance the game raises them to (DualPistolHold).
# Tactical 90 frames (1.5 s), empty 112 (1.87 s) at 60 fps.
#
# exec'd after firearm_pose.py (glock_pair_build_all.py). Unity axes, the character's chest frame; the left gun mirrors the right (x flipped).

from mathutils import Vector, Quaternion
import math

# The Glock's own hold (glock_reload.py), and the left hand's: the same seen in a mirror (the game's DualPistolHold.Mirror).
HOLD_POS = Vector((-0.056, -0.0795, 0.037))
HOLD_DIP = -22.0
HOLD_ROT = hold_rotation()
HOLD_POS_L = Vector((-HOLD_POS.x, HOLD_POS.y, HOLD_POS.z))
HOLD_ROT_L = Quaternion((HOLD_ROT.w, HOLD_ROT.x, -HOLD_ROT.y, -HOLD_ROT.z))

P = POINTS
MAG_MOUTH = Vector(P["mag_mouth"])
MAG_OUT = Vector(P["mag_axis_out"]).normalized()     # down the grip, raked back
MAG_TRAVEL = P["mag_travel"]
SLIDE_TRAVEL = P["slide_travel"]

# The akimbo stance: the game's DualPistolHold.RightGunInChest, the barrels straight ahead.
READY_AT = (0.28, -0.12, 0.78)

# The grip upright (its rake taken out by tipping the muzzle up), so the well comes straight down onto a magazine standing in its pouch.
RAKE = math.atan2(-MAG_OUT.z, -MAG_OUT.y)
UPRIGHT_FWD = (0.0, math.sin(RAKE), math.cos(RAKE))
UPRIGHT_UP = (0.0, math.cos(RAKE), -math.sin(RAKE))
# The fresh magazine's top, standing in the right pouch on the belt (in front of the hip, a little out): the left pouch mirrors it.
POUCH_TOP = Vector((0.33, -0.90, 0.27))


def upright_at(mouth):
    """Where the gun's origin goes for its well's mouth to be at `mouth` (chest frame) with the grip upright."""
    fwd, up = Vector(UPRIGHT_FWD), Vector(UPRIGHT_UP)
    right = up.cross(fwd)      # Unity's left-handed cross: x = up x fwd
    return Vector(mouth) - (right * MAG_MOUTH.x + up * MAG_MOUTH.y + fwd * MAG_MOUTH.z)


SEATED_AT = upright_at(POUCH_TOP)
OVER_POUCH_AT = SEATED_AT + Vector((0.0, MAG_TRAVEL, 0.0))


def gun(at, fwd=(0.0, 0.0, 1.0), up=(0.0, 1.0, 0.0)):
    return {"at": tuple(at), "fwd": tuple(fwd), "up": tuple(up)}


def mirrored(g):
    m = lambda v: (-v[0], v[1], v[2])
    return {"at": m(g["at"]), "fwd": m(g["fwd"]), "up": m(g["up"])}


def mag(travel=0.0, visible=1):
    return {"pos": tuple(MAG_OUT * (MAG_TRAVEL * travel)), "rot": Quaternion(), "visible": visible}


def slide(back=0.0):
    return {"pos": (0.0, 0.0, -SLIDE_TRAVEL * back), "rot": Quaternion(), "visible": 1}


def props(mag_spec, slide_back=0.0):
    return {"Magazine": mag_spec, "Slide": slide(slide_back), "Trigger": {"pos": (0, 0, 0), "rot": Quaternion(), "visible": 1}}


def spec(g, fingers_, props_, head_down=0.0):
    """Both guns, the left one the right's mirror image, with the same fingers and parts."""
    return {
        "hips": (0.0, 0.9, 0.0), "body": Quaternion(),
        "spine": Quaternion(), "neck": rot(p=head_down * 0.4), "head": rot(p=head_down * 0.6),
        "gun": g, "gun_l": mirrored(g), "r_pole": (0.7, -1.0, -0.3), "l_pole": (-0.7, -1.0, -0.3),
        "fingers_r": fingers_, "fingers_l": fingers_, "props": props_, "props_l": props_,
    }


THUMB_ON_RELEASE = dict(PISTOL_STRONG, thumb=1.0, thumb_forward=5.0, thumb_authored=0.0)
READY = spec(gun(READY_AT), PISTOL_STRONG, props(mag()))
# Muzzles up, brought in a little: the thumbs press the releases and the magazines fall out of the upright grips.
DROP_GUN = gun((0.26, -0.18, 0.62), (0.0, 0.5, 0.866), (0.0, 0.866, -0.5))
OVER_POUCH = gun(OVER_POUCH_AT, UPRIGHT_FWD, UPRIGHT_UP)
SEATED = gun(SEATED_AT, UPRIGHT_FWD, UPRIGHT_UP)
# The rack on the belt: the muzzle tipped down and forward, the back of the slide against the belt's front edge, the gun pushed down along its
# barrel so the belt holds the slide while the frame goes on (the slide back in the gun's frame), then lifted off: the slide flies home.
RACK_FWD = (0.0, -0.8, 0.6)
RACK_UP = (0.0, 0.6, 0.8)
RACK_START = Vector((0.33, -0.70, 0.36))
RACK_END = RACK_START + Vector(RACK_FWD) * SLIDE_TRAVEL


def tactical_keys(start=0):
    f = start
    return [
        (f + 0, READY, "inout"),
        (f + 10, spec(DROP_GUN, THUMB_ON_RELEASE, props(mag(0.0)), head_down=10), "in"),
        (f + 14, spec(DROP_GUN, THUMB_ON_RELEASE, props(mag(0.55, visible=0)), head_down=12), "inout"),   # drop: both magazines clear (the game throws them)
        (f + 36, spec(OVER_POUCH, PISTOL_STRONG, props(mag(1.0, visible=0)), head_down=30), "inout"),
        (f + 38, spec(OVER_POUCH, PISTOL_STRONG, props(mag(1.0)), head_down=30), "in"),                    # grab: each well over its fresh magazine
        (f + 50, spec(SEATED, PISTOL_STRONG, props(mag(0.0)), head_down=30), "out"),                       # seat: both pushed home, the ammo in
        (f + 56, spec(gun(SEATED_AT + Vector((0.0, 0.06, 0.04)), UPRIGHT_FWD, UPRIGHT_UP), PISTOL_STRONG, props(mag(0.0)), head_down=24), "inout"),
        (f + 90, READY, "linear"),
    ]


def empty_keys(start):
    keys = tactical_keys(start)[:-1]           # up to and including the lift off the pouches
    f = start
    keys += [
        (f + 66, spec(gun(RACK_START, RACK_FWD, RACK_UP), PISTOL_STRONG, props(mag(0.0)), head_down=24), "in"),
        (f + 74, spec(gun(RACK_END, RACK_FWD, RACK_UP), PISTOL_STRONG, props(mag(0.0), 1.0), head_down=24), "linear"),
        (f + 76, spec(gun(RACK_END + Vector((0.0, 0.05, 0.03)), RACK_FWD, RACK_UP), PISTOL_STRONG, props(mag(0.0), 0.0), head_down=20), "inout"),   # rack: the slides fly home
        (f + 112, READY, "linear"),
    ]
    return keys


TACTICAL_START, EMPTY_START = 0, 120
KEYS = tactical_keys(TACTICAL_START) + empty_keys(EMPTY_START)
SEGMENTS = {"tactical": (TACTICAL_START, TACTICAL_START + 90), "empty": (EMPTY_START, EMPTY_START + 112)}
EVENTS = {"drop": TACTICAL_START + 14, "grab": TACTICAL_START + 38, "seat": TACTICAL_START + 50,
          "drop2": EMPTY_START + 14, "grab2": EMPTY_START + 38, "seat2": EMPTY_START + 50, "rack2": EMPTY_START + 76}
