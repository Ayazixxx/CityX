# THE PUMP SHOTGUN'S RELOAD, as key specs for firearm_pose.py. A tube magazine is filled ONE SHELL AT A TIME, so the reload is not one motion but
# four pieces the game strings together (docs/SHOTGUN.md), each ending where the next begins:
#   start     the support hand leaves the forend for the shells at the belly, the gun drawn in and rolled so the loading port under the
#             receiver turns toward that hand;
#   shell     a shell is taken, brought up under the receiver across the fingers, laid in the port and thumbed forward into the tube (the
#             `seat` event: one more round is in), and the hand goes back for the next. Played once for every shell;
#   end       the hand back to the forend, the gun back in the shoulder;
#   endempty  the same, but the gun was run dry: the forend is pulled back and shoved home (the `rack` event) to chamber the first round.
# How a pump gun is loaded (Lucky Gunner, "How to reload a defensive shotgun"; Shooting Illustrated, "How to Reload a Shotgun: Tips From the
# Pros"): the strong hand keeps the gun up by its grip, the support hand feeds the tube from underneath, the thumb pushing each shell forward
# until the latch catches it. Timing from the games: half a second a shell (Counter-Strike's Nova), 4.4 to 5.7 s to fill a Fortnite pump gun.
#
# exec'd after firearm_pose.py (shotgun_build_all.py). Unity axes; the gun's frame has its origin at the web of the grip.

from mathutils import Vector, Quaternion
import math
import os

P = POINTS
BUTT_LOCAL = Vector(P["butt"])
SUPPORT_LOCAL = Vector(P["support"])
SHELL_REST = Vector(P["shell_rest"])
PUMP_TRAVEL = P["pump_travel"]

_STANCE = os.path.join(REPO, "CityX", "SourceArt", "Firearms", "firearm_rifle_stance.py")
exec(compile(open(_STANCE).read(), _STANCE, "exec"), globals())     # BLADE, BODY_FWD, POCKET, gun_from_butt, turned, POUCH, TO_POUCH, spec

# Shouldered (what the game's aim pose holds): level, along the body's forward, the butt in the pocket.
READY_GUN = gun_from_butt(POCKET, BODY_FWD, (0.0, 1.0, 0.0))
# The loading hold: the butt out of the pocket and down a little, the muzzle up 14 degrees, the top rolled 30 degrees to the right so the port
# under the receiver turns toward the support hand, the whole gun drawn in.
_work_fwd = turned(BODY_FWD, pitch_up=14.0, yaw_left=6.0)
_work_up = Quaternion(_work_fwd, math.radians(-30.0)) @ Vector((0.0, 1.0, 0.0))
WORK_GUN = gun_from_butt(POCKET + Vector((-0.05, -0.08, 0.02)), _work_fwd, _work_up)
# Pumping: level again and back in the shoulder, a touch low.
RACK_GUN = gun_from_butt(POCKET + Vector((-0.01, -0.02, 0.0)), turned(BODY_FWD, pitch_up=3.0), (0.0, 1.0, 0.0))

# The shell in the support hand (the LEFT hand's frame: fingers run -x, palm -y, thumb +z), as it is under the receiver: the hand comes in from
# the left with its palm up and its fingers across under the gun, the shell lying across the fingers along the bore, the thumb behind it.
SHELL_IN_HAND = (Vector((-0.125, -0.032, 0.012)), rot_cols((-1, 0, 0), (0, -1, 0), (0, 0, 1)))

BELOW_PORT = Vector((0.0, -0.085, -0.035))     # under the receiver, a little behind the port
AT_PORT = Vector((0.0, -0.036, -0.012))        # at the port's mouth, the fingers still clear of the receiver
IN_TUBE = Vector((0.0, 0.0, 0.085))            # thumbed up and forward: gone into the tube
THUMB_THROUGH = Vector((0.0, -0.036, 0.055))   # where the hand is as the thumb follows it in


def shell(offset=(0.0, 0.0, 0.0), visible=0, in_hand=0):
    return {"pos": tuple(offset), "rot": Quaternion(), "visible": visible, "in_hand": in_hand, "hand": SHELL_IN_HAND}


def pump(back=0.0):
    return {"pos": (0.0, 0.0, -PUMP_TRAVEL * back), "rot": Quaternion(), "visible": 1}


def props(shell_spec, pump_back=0.0):
    return {"Shell": shell_spec, "Pump": pump(pump_back), "Trigger": {"pos": (0, 0, 0), "rot": Quaternion(), "visible": 1}}


def hand_with_shell(offset):
    return dict(hand_holding(SHELL_REST + Vector(offset), Quaternion(), SHELL_IN_HAND), pole=(-0.8, -1.0, 0.1))


def on_forend(back=0.0):
    """The support hand under the forend, wherever the pump has it: the palm up, the hand nearly flat across its underside, the knuckles in a row
    along its lower right edge and the fingers curled up its right side (the game's FirearmProfile.Shotgun hold: the same wrist, fingers and palm)."""
    at = SUPPORT_LOCAL + Vector((-0.077, -0.0432, -0.0525 - PUMP_TRAVEL * back))
    return {"at": tuple(at), "fingers": (0.95, 0.15, 0.1), "palm": (0.15, 1.0, 0.0), "space": "gun", "pole": (-0.7, -1.0, -0.1)}


FOREND_GRIP = fingers(0.75, 0.8, 0.8, 0.75, 1.0, thumb_forward=-50.0)   # the game's FingerAmounts.ForendCradle
PINCH = fingers(0.45, 0.5, 0.55, 0.6, 0.75, thumb_forward=10.0)      # round a shell
THUMBED = fingers(0.5, 0.55, 0.6, 0.65, 0.2, thumb_forward=35.0)     # the thumb pushed through behind it
FLAT = fingers(0.05, 0.05, 0.05, 0.05, 0.1)

READY = spec(READY_GUN, on_forend(), FOREND_GRIP, props(shell()))
AT_BELT = spec(WORK_GUN, POUCH, FLAT, props(shell()), head_down=14)


def start_keys(f):
    return [
        (f + 0, READY, "inout"),
        (f + 12, spec(WORK_GUN, TO_POUCH, FLAT, props(shell()), head_down=10), "inout"),
        (f + 24, AT_BELT, "linear"),
    ]


def shell_keys(f):
    return [
        (f + 0, AT_BELT, "linear"),
        (f + 3, spec(WORK_GUN, POUCH, PINCH, props(shell(visible=1, in_hand=1)), head_down=14), "inout"),                                # a shell in the fingers
        (f + 15, spec(WORK_GUN, hand_with_shell(BELOW_PORT), PINCH, props(shell(BELOW_PORT, 1, 1)), head_down=18), "out"),               # up under the receiver
        (f + 19, spec(WORK_GUN, hand_with_shell(AT_PORT), PINCH, props(shell(AT_PORT, 1, 0)), head_down=18), "in"),                      # at the port: it leaves the fingers
        (f + 23, spec(WORK_GUN, hand_with_shell(THUMB_THROUGH), THUMBED, props(shell(IN_TUBE, 1, 0)), head_down=18), "linear"),          # thumbed home: click
        (f + 24, spec(WORK_GUN, hand_with_shell(THUMB_THROUGH), THUMBED, props(shell()), head_down=18), "inout"),
        (f + 34, AT_BELT, "linear"),
    ]


def end_keys(f):
    return [
        (f + 0, AT_BELT, "inout"),
        (f + 12, spec(WORK_GUN, on_forend(), FLAT, props(shell()), head_down=8), "inout"),
        (f + 24, READY, "linear"),
    ]


def end_empty_keys(f):
    return [
        (f + 0, AT_BELT, "inout"),
        (f + 13, spec(RACK_GUN, on_forend(), FOREND_GRIP, props(shell()), head_down=6), "in"),                        # the hand on the forend
        (f + 20, spec(RACK_GUN, on_forend(1.0), FOREND_GRIP, props(shell(), 1.0), head_down=6), "out"),               # pulled to the rear
        (f + 35, spec(RACK_GUN, on_forend(0.0), FOREND_GRIP, props(shell(), 0.0), head_down=4), "inout"),             # shoved home: a round chambered (0.25 s after
        # the pull, as in the owner's recording of a pump: ShotgunBuilder.RackSoundPath, played from its pull at the rack event)
        (f + 48, READY, "linear"),
    ]


START, SHELL, END, END_EMPTY = 0, 40, 90, 130
KEYS = start_keys(START) + shell_keys(SHELL) + end_keys(END) + end_empty_keys(END_EMPTY)
SEGMENTS = {"start": (START, START + 24), "shell": (SHELL, SHELL + 34), "end": (END, END + 24), "endempty": (END_EMPTY, END_EMPTY + 48)}
SEGMENT_NAMES = ("start", "shell", "end", "endempty")
EVENTS = {"grab": SHELL + 3, "seat": SHELL + 23, "rack": END_EMPTY + 20}   # the rack: the forend at the rear, where the pump recording is played from
