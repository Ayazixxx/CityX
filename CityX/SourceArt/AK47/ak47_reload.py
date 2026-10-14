# THE AK-47'S RELOADS, as key specs for firearm_pose.py. From the research (docs/THIRD_PERSON_PISTOL.md, "The AK-47"):
#   * an AK magazine is ROCKED, not pushed: its front lug hooks into the front of the well and the back rocks up until the latch clicks; it comes
#     out the same way in reverse, the support hand's thumb pressing the paddle release behind the well while the hand rocks it forward;
#   * the fresh magazine comes from a chest rig at the belly; the gun is kept up, pulled in a little and rolled so the well faces the hand;
#   * on an EMPTY rifle the support hand then comes UNDER the gun to the charging handle on the right side and, with an open palm, pulls it back
#     and lets it go ("resist the urge to ride the bolt forward");
#   * timing from the games: PUBG's AKM 2.25 s tactical and 2.9 s empty, CoD's AK-47 1.75 s to its cancel point. Here 2.17 s and 2.8 s.
# The torso is bladed 30 degrees to the right (the rifle stance: the stock in the right shoulder pocket, the support hand far out on the
# handguard); the game's rifle pose (PlayerLocomotion.RifleAim) uses the same numbers, so the reload starts and ends where the aim is.
#
# exec'd after firearm_pose.py (ak47_build_all.py). Unity axes; the gun's frame has its origin at the web of the grip.

from mathutils import Vector, Quaternion
import math

P = POINTS
LUG = Vector(P["mag_front_lug"])
BUTT_LOCAL = Vector(P["butt"])
SUPPORT_LOCAL = Vector(P["support"])
HANDLE = Vector(P["bolt_handle"])
BOLT_TRAVEL = P["bolt_travel"]

BLADE = 30.0                      # the torso's turn to the right in the rifle stance (PlayerLocomotion.RifleBladeDegrees)
_s, _c = math.sin(math.radians(BLADE)), math.cos(math.radians(BLADE))
BODY_FWD = Vector((-_s, 0.0, _c))     # the body's forward, in the bladed chest's frame
BODY_RIGHT = Vector((_c, 0.0, _s))
POCKET = Vector((0.25, -0.06, 0.10))  # the right shoulder pocket, in the chest's frame (PlayerLocomotion.RifleShoulderPocket)


def gun_from_butt(butt, fwd, up):
    """The gun spec whose BUTT sits at `butt` (chest frame), pointing `fwd` with its top toward `up`."""
    fwd, up = Vector(fwd).normalized(), Vector(up).normalized()
    right = up.cross(fwd).normalized()          # Unity is left-handed: right = up x forward
    up = fwd.cross(right).normalized()
    web = Vector(butt) - (right * BUTT_LOCAL.x + up * BUTT_LOCAL.y + fwd * BUTT_LOCAL.z)
    return {"at": tuple(web), "fwd": tuple(fwd), "up": tuple(up)}


def turned(v, pitch_up=0.0, roll_right=0.0, yaw_left=0.0):
    """A direction in the body's terms turned: the muzzle raised, the top rolled to the right, swung left."""
    q = (Quaternion(Vector((0, 1, 0)), math.radians(-yaw_left)) @ Quaternion(BODY_RIGHT, math.radians(-pitch_up)))
    return q @ Vector(v)


# Shouldered (what the game's aim pose holds): level, along the body's forward, the butt in the pocket.
READY_GUN = gun_from_butt(POCKET, BODY_FWD, (0.0, 1.0, 0.0))
# The reload hold: the butt out of the pocket and down a little, the muzzle up 12 degrees, the top rolled 25 degrees to the right so the well
# turns toward the support hand, the whole gun drawn in.
_work_fwd = turned(BODY_FWD, pitch_up=12.0, yaw_left=6.0)
_work_up = Quaternion(_work_fwd, math.radians(-25.0)) @ Vector((0.0, 1.0, 0.0))
WORK_GUN = gun_from_butt(POCKET + Vector((-0.05, -0.08, 0.02)), _work_fwd, _work_up)
_rack_fwd = turned(BODY_FWD, pitch_up=8.0, yaw_left=4.0)
_rack_up = Quaternion(_rack_fwd, math.radians(15.0)) @ Vector((0.0, 1.0, 0.0))      # rolled LEFT for the rack: the right side up to the hand
RACK_GUN = gun_from_butt(POCKET + Vector((-0.03, -0.05, 0.02)), _rack_fwd, _rack_up)


def x_rot(deg):
    return Quaternion(Vector((1.0, 0.0, 0.0)), math.radians(deg))


# The magazine in the support hand (the LEFT hand's frame: fingers run -x, palm -y, thumb +z): its front lug toward the fingertips, its body
# lying back along the palm, the palm against its rear.
MAG_IN_HAND = (Vector((-0.24, -0.10, 0.03)), rot_cols((0, 0, 1), (-1, 0, 0), (0, -1, 0)))


def mag(rock=0.0, drop=0.0, visible=1, in_hand=0):
    """The magazine: `rock` degrees swung forward about its front lug (the bottom forward), then `drop` metres down and forward out of the well."""
    out = Vector((0.0, -0.95, 0.31)).normalized() * drop
    return {"pos": tuple(out), "rot": x_rot(-rock), "visible": visible, "in_hand": in_hand, "hand": MAG_IN_HAND}


def bolt(back=0.0):
    return {"pos": (0.0, 0.0, -BOLT_TRAVEL * back), "rot": Quaternion(), "visible": 1}


def props(mag_spec, bolt_back=0.0):
    return {"Magazine": mag_spec, "Bolt": bolt(bolt_back), "Trigger": {"pos": (0, 0, 0), "rot": Quaternion(), "visible": 1}}


def hand_on_mag(rock, drop):
    m = mag(rock, drop)
    return dict(hand_holding(LUG + Vector(m["pos"]), m["rot"], MAG_IN_HAND), pole=(-0.8, -1.0, 0.1))


# The support hand under the handguard: the palm up, the fingers curled up its right side, the thumb along its left.
ON_HANDGUARD = {"at": tuple(SUPPORT_LOCAL + Vector((-0.085, -0.055, -0.03))), "fingers": (0.75, 0.55, 0.1), "palm": (0.35, 0.9, 0.0),
                "space": "gun", "pole": (-0.7, -1.0, -0.1)}
# The thumb on the paddle release behind the well, the fingers round the magazine's front.
ON_RELEASE = hand_on_mag(0.0, 0.0)
POUCH = {"at": (-0.05, -0.62, 0.30), "fingers": (0.2, 0.95, 0.15), "palm": (0.8, 0.0, 0.5), "space": "chest", "pole": (-0.8, -0.8, -0.2)}
TO_POUCH = {"at": (-0.02, -0.50, 0.38), "fingers": (0.4, 0.85, 0.25), "palm": (0.85, 0.1, 0.4), "space": "chest", "pole": (-0.8, -0.8, -0.2)}
# Under the receiver to the charging handle: the open palm facing back, in front of the handle, the fingers to the right.
AT_HANDLE = {"at": tuple(HANDLE + Vector((-0.105, -0.045, 0.06))), "fingers": (0.9, 0.35, 0.05), "palm": (0.0, 0.1, -1.0), "space": "gun",
             "pole": (-0.6, -1.0, 0.0)}


def at_handle(back):
    h = dict(AT_HANDLE)
    h["at"] = (AT_HANDLE["at"][0], AT_HANDLE["at"][1], AT_HANDLE["at"][2] - BOLT_TRAVEL * back)
    return h


RIFLE_GRIP_L = fingers(0.75, 0.8, 0.8, 0.75, 1.0)
CUP = fingers(0.55, 0.75, 0.8, 0.8, 0.6, thumb_forward=15.0)
FLAT = fingers(0.05, 0.05, 0.05, 0.05, 0.1)
STRONG = RIFLE_STRONG


def spec(gun, l_hand, fingers_l, props_, head_down=0.0, extra_twist=0.0):
    return {
        "hips": (0.0, 0.9, 0.0), "body": Quaternion(),
        "spine": rot(y=-(BLADE + extra_twist)), "neck": rot(p=head_down * 0.4, y=(BLADE + extra_twist) * 0.5),
        "head": rot(p=head_down * 0.6, y=(BLADE + extra_twist) * 0.5),
        "gun": gun, "r_pole": (0.75, -1.0, -0.35), "l_hand": l_hand,
        "fingers_r": STRONG, "fingers_l": fingers_l, "props": props_,
    }


READY = spec(READY_GUN, ON_HANDGUARD, RIFLE_GRIP_L, props(mag()))


def tactical_keys(f):
    return [
        (f + 0, READY, "inout"),
        (f + 12, spec(WORK_GUN, ON_RELEASE, CUP, props(mag()), head_down=14), "inout"),                                 # the hand on the mag, thumb on the paddle
        (f + 22, spec(WORK_GUN, hand_on_mag(26.0, 0.0), CUP, props(mag(26.0, 0.0, 1, 1)), head_down=16), "inout"),      # rocked forward off its lug
        (f + 32, spec(WORK_GUN, hand_on_mag(30.0, 0.16), CUP, props(mag(30.0, 0.16, 1, 1)), head_down=18), "in"),       # pulled clear
        (f + 38, spec(WORK_GUN, TO_POUCH, FLAT, props(mag(30.0, 0.30, 0, 0)), head_down=18), "inout"),                  # let go: the game throws it
        (f + 54, spec(WORK_GUN, POUCH, FLAT, props(mag(30.0, 0.30, 0, 0)), head_down=16), "inout"),
        (f + 58, spec(WORK_GUN, POUCH, CUP, props(mag(30.0, 0.30, 1, 1)), head_down=16), "inout"),                      # the fresh one in hand
        (f + 78, spec(WORK_GUN, hand_on_mag(28.0, 0.012), CUP, props(mag(28.0, 0.012, 1, 1)), head_down=18), "out"),    # its lug at the well
        (f + 84, spec(WORK_GUN, hand_on_mag(26.0, 0.0), CUP, props(mag(26.0, 0.0, 1, 1)), head_down=18), "in"),         # hooked
        (f + 92, spec(WORK_GUN, hand_on_mag(0.0, 0.0), CUP, props(mag(0.0, 0.0, 1, 1)), head_down=16), "out"),          # rocked home: click
        (f + 100, spec(WORK_GUN, hand_on_mag(0.0, 0.0), CUP, props(mag()), head_down=12), "inout"),                     # a tug to check it
        (f + 130, READY, "linear"),
    ]


def empty_keys(f):
    keys = tactical_keys(f)[:-1]
    keys += [
        (f + 112, spec(RACK_GUN, at_handle(0.0), FLAT, props(mag()), head_down=10), "inout"),                          # under the gun, palm on the handle
        (f + 120, spec(RACK_GUN, at_handle(1.0), CUP, props(mag(), 1.0), head_down=10), "out"),                         # pulled to the rear
        (f + 123, spec(RACK_GUN, at_handle(1.1), FLAT, props(mag(), 0.0), head_down=8), "linear"),                      # let go: it slams home
        (f + 136, spec(RACK_GUN, dict(at_handle(0.6), at=(at_handle(0.6)["at"][0] - 0.05, at_handle(0.6)["at"][1] - 0.06, at_handle(0.6)["at"][2])),
                       FLAT, props(mag()), head_down=6), "inout"),
        (f + 168, READY, "linear"),
    ]
    return keys


TACTICAL_START, EMPTY_START = 0, 150
KEYS = tactical_keys(TACTICAL_START) + empty_keys(EMPTY_START)
SEGMENTS = {"tactical": (TACTICAL_START, TACTICAL_START + 130), "empty": (EMPTY_START, EMPTY_START + 168)}
EVENTS = {"drop": TACTICAL_START + 38, "grab": TACTICAL_START + 58, "seat": TACTICAL_START + 92,
          "drop2": EMPTY_START + 38, "grab2": EMPTY_START + 58, "seat2": EMPTY_START + 92, "rack2": EMPTY_START + 123}
