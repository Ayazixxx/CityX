# THE RIFLE STANCE, shared by the reloads of every gun held as a rifle (ak47_reload.py, shotgun_reload.py): the torso bladed 30 degrees to the right,
# the stock in the right shoulder pocket, the support hand far out under the gun. The game's rifle pose (PlayerLocomotion.RifleAim) uses the same
# numbers, so a reload starts and ends where the aim is.
#
# exec'd after firearm_pose.py and after the gun's own script has set BUTT_LOCAL (the butt's place in the gun's frame). Unity axes.

from mathutils import Vector, Quaternion
import math

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


def x_rot(deg):
    return Quaternion(Vector((1.0, 0.0, 0.0)), math.radians(deg))


# The fresh ammunition: a chest rig at the belly, and the hand on its way there.
POUCH = {"at": (-0.05, -0.62, 0.30), "fingers": (0.2, 0.95, 0.15), "palm": (0.8, 0.0, 0.5), "space": "chest", "pole": (-0.8, -0.8, -0.2)}
TO_POUCH = {"at": (-0.02, -0.50, 0.38), "fingers": (0.4, 0.85, 0.25), "palm": (0.85, 0.1, 0.4), "space": "chest", "pole": (-0.8, -0.8, -0.2)}


def spec(gun, l_hand, fingers_l, props_, head_down=0.0, extra_twist=0.0):
    """One key in the rifle stance: where the gun is, what the support hand does, where the gun's parts are, how far the head looks down at it."""
    return {
        "hips": (0.0, 0.9, 0.0), "body": Quaternion(),
        "spine": rot(y=-(BLADE + extra_twist)), "neck": rot(p=head_down * 0.4, y=(BLADE + extra_twist) * 0.5),
        "head": rot(p=head_down * 0.6, y=(BLADE + extra_twist) * 0.5),
        "gun": gun, "r_pole": (0.75, -1.0, -0.35), "l_hand": l_hand,
        "fingers_r": RIFLE_STRONG, "fingers_l": fingers_l, "props": props_,
    }
