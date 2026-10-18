# THE THRUST GRIP the stab dash is posed with: SwordWeapon's (ThrustTiltDegrees, FistPoint, ThrustBladeAxisInHand, ThrustGripRotation), which
# the game holds the sword in while the move plays. The handle sits in the fist where the one-handed hold has it, turned within the fist so the
# blade comes out between the thumb and the index finger leaning toward the fingertips.
#
# Needs octaslash_rig.py run first (the rig, its sword, and its frame-conversion globals). Run before octaslash_pose.py, which reads the
# sword's rest placement (BLADE_REST, EDGE_REST, HAND_TO_BLADE) when it loads.
import math

import bpy
from mathutils import Matrix, Quaternion, Vector

# SwordWeapon's numbers, in the right-hand bone's frame (Unity: +X down the hand to the fingertips, -Y the palm's side, +Z the thumb's).
THRUST_TILT = 50.0                                                    # ThrustTiltDegrees
ONE_HAND_AXIS = Vector((-0.056, 0.0, 0.998)).normalized()             # OneHandBladeAxisInHand
FIST_POINT = Vector((0.08, -(0.042 + 0.03), 0.0))                     # FistPoint: PalmOffsetMetres, -OneHandHandleBelowPalm
GRIP_REACH = 0.12                                                     # the rig's sword is the two-handed one: its upper grip


def look_rotation(forward, up):
    """Unity's Quaternion.LookRotation: +Z along `forward`, +Y as near `up` as it can be."""
    z = Vector(forward).normalized()
    x = Vector(up).cross(z).normalized()
    y = z.cross(x)
    return Matrix((x, y, z)).transposed().to_quaternion()


def hold_for_thrust(sword):
    """Puts the rig's sword in the thrust grip, as octaslash_rig.add_sword puts it in the two-handed hold."""
    one_hand = look_rotation((0.0, 1.0, 0.0), ONE_HAND_AXIS)                     # SwordWeapon.DefaultOneHandGripRotation
    grip_rot = Quaternion((0.0, 1.0, 0.0), math.radians(THRUST_TILT)) @ one_hand  # ThrustGripRotation
    axis = grip_rot @ Vector((0.0, 1.0, 0.0))                                   # ThrustBladeAxisInHand
    grip_pos = FIST_POINT + axis * GRIP_REACH
    arm_down = math.atan2(SHOULDER_Y - HAND_Y, HAND_X - SHOULDER_X)
    hang = Quaternion((0, 0, 1), -arm_down)
    wrist = Vector((SHOULDER_X, SHOULDER_Y, 0)).lerp(Vector((HAND_X, HAND_Y, 0)), 0.85)
    position = M.transposed() @ (wrist + hang @ grip_pos)
    rotation = M.transposed() @ (hang @ grip_rot).to_matrix() @ M
    bpy.context.view_layer.update()
    sword.matrix_world = Matrix.Translation(position) @ rotation.to_4x4()
    bpy.context.view_layer.update()
    return axis


hold_for_thrust(bpy.data.objects["Masamune"])
