# THE HOLD THE SWORD MOVES ARE POSED WITH: the game's one-handed hold as the owner tuned it in the game (Character/Resources/WeaponGrips.asset,
# 2026-10-17: "he actually grips it properly"). The handle is closed in the fingers right under the guard, the blade runs straight out past the thumb
# across the forearm, and the edge is in line with the knuckles. A grip leaning the blade toward the fingertips ran the handle back along the forearm
# (owner, 2026-11-15: "your grip is like slipping down the arm", with a sketch of the blade standing up out of the fist). The rig is built with the old
# mocap hold (the Octaslash keeps it), so a move posed on it as built would point the blade somewhere else in the game.
#
# Needs octaslash_rig.py run first (the rig, its sword, and its frame-conversion globals). Run before octaslash_pose.py, which reads the sword's rest
# placement (BLADE_REST, EDGE_REST, HAND_TO_BLADE) when it loads.
import math

import bpy
from mathutils import Matrix, Quaternion, Vector

# In the right-hand bone's frame (Unity: +X down the hand to the fingertips, -Y the palm's side, +Z the thumb's). The tuned hold's blade, and the way
# the rig's sword's -X side (its edge, SwordWeapon's convention) faces: the knuckles. The combat sword, edged on +X, is held turned half round its blade.
BLADE_AXIS_IN_HAND = Vector((-0.056, -0.077, 0.995)).normalized()     # Quaternion.Euler(WeaponGrips.OneHandEuler) * up
BACK_OF_BLADE_IN_HAND = Vector((-0.0074, 0.9962, 0.0767)).normalized()  # the sword's own +Z, toward the back of the hand
FIST_POINT = Vector((0.08, -(0.042 + 0.03), 0.0))                     # FistPoint: PalmOffsetMetres, -OneHandHandleBelowPalm
GRIP_DOWN = 0.09                                                      # OneHandGripDown: the hand just below the guard
SUPPORT_GRIP_DOWN = 0.30                                              # DefaultSupportGripDown: the left fist, in the sword's own frame


def look_rotation(forward, up):
    """Unity's Quaternion.LookRotation: +Z along `forward`, +Y as near `up` as it can be."""
    z = Vector(forward).normalized()
    x = Vector(up).cross(z).normalized()
    y = z.cross(x)
    return Matrix((x, y, z)).transposed().to_quaternion()


def hold_as_the_game_does(sword):
    """Puts the rig's sword in the game's hold, as octaslash_rig.add_sword puts it in the mocap one."""
    grip_rot = look_rotation(BACK_OF_BLADE_IN_HAND, BLADE_AXIS_IN_HAND)
    grip_pos = FIST_POINT + BLADE_AXIS_IN_HAND * GRIP_DOWN
    arm_down = math.atan2(SHOULDER_Y - HAND_Y, HAND_X - SHOULDER_X)
    hang = Quaternion((0, 0, 1), -arm_down)
    wrist = Vector((SHOULDER_X, SHOULDER_Y, 0)).lerp(Vector((HAND_X, HAND_Y, 0)), 0.85)
    position = M.transposed() @ (wrist + hang @ grip_pos)
    rotation = M.transposed() @ (hang @ grip_rot).to_matrix() @ M
    bpy.context.view_layer.update()
    sword.matrix_world = Matrix.Translation(position) @ rotation.to_4x4()
    bpy.context.view_layer.update()


hold_as_the_game_does(bpy.data.objects["Masamune"])
