# THE HOLD THE SWORD MOVES ARE POSED WITH: the game's own (SwordWeapon.DefaultOneHandGripRotation, which the two-handed hold shares since 2026-10-28). The handle lies across
# the palm, the blade runs out past the thumb and the edge is in line with the knuckles. The rig is built with the old mocap hold (the Octaslash keeps it), so a move posed on
# it as built would point the blade somewhere else in the game.
#
# Needs octaslash_rig.py run first (the rig, its sword, and its frame-conversion globals). Run before octaslash_pose.py, which reads the sword's rest placement (BLADE_REST,
# EDGE_REST, HAND_TO_BLADE) when it loads.
import math

import bpy
from mathutils import Matrix, Quaternion, Vector

# SwordWeapon's numbers, in the right-hand bone's frame (Unity: +X down the hand to the fingertips, -Y the palm's side, +Z the thumb's).
BLADE_AXIS_IN_HAND = Vector((-0.056, 0.0, 0.998)).normalized()         # OneHandBladeAxisInHand
FIST_POINT = Vector((0.08, -(0.042 + 0.03), 0.0))                     # FistPoint: PalmOffsetMetres, -OneHandHandleBelowPalm
GRIP_DOWN = 0.10                                                      # DefaultPrimaryGripDown (the one-handed hold's is 0.09)
SUPPORT_GRIP_DOWN = 0.30                                              # DefaultSupportGripDown: the left fist, in the sword's own frame


def look_rotation(forward, up):
    """Unity's Quaternion.LookRotation: +Z along `forward`, +Y as near `up` as it can be."""
    z = Vector(forward).normalized()
    x = Vector(up).cross(z).normalized()
    y = z.cross(x)
    return Matrix((x, y, z)).transposed().to_quaternion()


def hold_as_the_game_does(sword):
    """Puts the rig's sword in the game's hold, as octaslash_rig.add_sword puts it in the mocap one."""
    grip_rot = look_rotation((0.0, 1.0, 0.0), BLADE_AXIS_IN_HAND)              # DefaultOneHandGripRotation
    grip_pos = FIST_POINT + BLADE_AXIS_IN_HAND * GRIP_DOWN                      # DefaultGripPosition
    arm_down = math.atan2(SHOULDER_Y - HAND_Y, HAND_X - SHOULDER_X)
    hang = Quaternion((0, 0, 1), -arm_down)
    wrist = Vector((SHOULDER_X, SHOULDER_Y, 0)).lerp(Vector((HAND_X, HAND_Y, 0)), 0.85)
    position = M.transposed() @ (wrist + hang @ grip_pos)
    rotation = M.transposed() @ (hang @ grip_rot).to_matrix() @ M
    bpy.context.view_layer.update()
    sword.matrix_world = Matrix.Translation(position) @ rotation.to_4x4()
    bpy.context.view_layer.update()


hold_as_the_game_does(bpy.data.objects["Masamune"])
