# THE GAME'S FRAMES IN BLENDER: a place written as the game has it (Unity: x right, y up, z ahead; a quaternion as x, y, z, w; Euler as Quaternion.Euler has it), and the bones a
# held or carried thing rides at the rig's rest, so a script can say where a weapon is on the body in the game's own numbers (a prefab's, a component's constants) and only carry
# it into Blender's axes at the end. Loaded with exec after the rig (Octaslash/octaslash_rig.py), like the rest of this folder: IronSpikedBoomerang/iron_spiked_hold.py,
# wrist_turn.py.
import math

from mathutils import Matrix, Quaternion, Vector

TO_BLENDER = Matrix(((-1, 0, 0), (0, 0, -1), (0, 1, 0))).to_4x4()      # Unity (x, y, z) is Blender (-x, -z, y)


def uq(x, y, z, w):
    return Quaternion((w, x, y, z))


def u_euler(x, y, z):
    """Quaternion.Euler: about z, then x, then y."""
    return (Quaternion((0, 1, 0), math.radians(y)) @ Quaternion((1, 0, 0), math.radians(x)) @ Quaternion((0, 0, 1), math.radians(z)))


def u_look(along, face):
    """Quaternion.LookRotation: z along `along`, y as near `face` as that leaves it."""
    z = Vector(along).normalized()
    x = Vector(face).cross(z).normalized()
    return Matrix((x, z.cross(x), z)).transposed().to_quaternion()


def u_trs(position, rotation):
    return Matrix.Translation(Vector(position)) @ rotation.to_matrix().to_4x4()


def in_blender(unity):
    return TO_BLENDER @ unity @ TO_BLENDER.inverted()


# The bones a carried thing rides, at the rig's arms-down rest (BodyRig).
SPINE_REST = u_trs((0.0, 1.4, 0.0), Quaternion())
_ARM_DOWN = math.atan2(1.9 - 0.88, 0.58 - 0.35)
_WRIST = Vector((0.35, 1.9, 0.0)).lerp(Vector((0.58, 0.88, 0.0)), 0.85)
HAND_REST = u_trs(_WRIST, Quaternion((0, 0, 1), -_ARM_DOWN))
