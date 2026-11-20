# WHERE THE IRON SPIKED GIANT BOOMERANG IS, ON THE BACK AND IN THE HAND, IN BLENDER (docs/BOOMERANG.md), for its moves' check renders and for the hold
# itself: the hold is worked out here and printed for BoomerangBuilder (IronSpikedHoldPosition, IronSpikedHoldRotation).
#
# Everything is written in the game's terms (../game_frames.py) and only carried into Blender's axes at the end. Needs the rig (../Octaslash/octaslash_rig.py)
# and ../game_frames.py loaded first.
#
# THE HOLD (owner, 2026-11-20: "i want it to grab the boomerang by the rope and maybe have the fingers clipped into the rope strategically so it looks
# like its got a grip on the rope", "an above grip like how sasuke holds the demon wind shuriken"): the palm laid on top of a wrap, sunk a little into
# it, the fingers folded down into the leather (GiantBoomerangItem's grip), the wrap's arm running across the hand.
import math

import bpy
from mathutils import Matrix, Quaternion, Vector

# IronSpikedGiantBoomerang.prefab: where the owner hung it from the spine, its model in that frame on the back, and its model in the item's own frame
# (the grip of old at the origin).
BACK_HANG = u_trs((-0.92, 0.15, -0.36), u_euler(516.5, -90.0, 90.0))
BACK_MODEL = u_trs((-0.21655567, 0.0, 0.8869269), uq(0.0, -0.89133155, 0.0, -0.45335218))
ITEM_MODEL = u_trs((-0.29373237, 0.0, 0.42650568), uq(-0.99855137, 0.0, -0.053807065, 0.0))

# The model in its thrown frame (iron_spiked_boomerang_build.py's points): the middles of its two wraps on its faces, its elbow.
HALF_THICK = 0.041            # the leather under the palm lies this far from its middle plane (its knots stand out to 0.065)
WRAPS = {"upper": Vector((0.30, 0.0, 0.30)), "lower": Vector((-0.12, 0.0, -0.46))}
ELBOW = Vector((0.3434, 0.0, -0.4437))

# The palm in the hand bone's frame (+x to the fingertips, -y the palm's side, +z the thumb's): its middle, on its skin (FusedBodyShape).
PALM_MIDDLE = Vector((0.10, -0.042, 0.0))


def on_the_back():
    """The model in the body's frame as it hangs on the back."""
    return SPINE_REST @ BACK_HANG @ BACK_MODEL


def hold(wrap, face, across_degrees, sink, slide=(0.0, 0.0)):
    """The model in the hand bone's frame: the palm on `wrap`'s middle on the model's `face` (+1 its +y face, -1 the other), sunk `sink` into it, the
    wrap's arm (toward the elbow) lying `across_degrees` round from the fingers toward the thumb, the palm slid `slide` (along the fingers, toward the
    thumb) over the wrap."""
    grip = WRAPS[wrap] + Vector((0.0, face * HALF_THICK, 0.0))
    arm = (ELBOW - WRAPS[wrap]).normalized()
    normal = Vector((0.0, face, 0.0))
    side = normal.cross(arm)
    a = math.radians(across_degrees)
    arm_h = Vector((math.cos(a), 0.0, math.sin(a)))
    normal_h = Vector((0.0, 1.0, 0.0))
    side_h = normal_h.cross(arm_h)
    model = Matrix((arm, normal, side)).transposed()        # columns
    hand = Matrix((arm_h, normal_h, side_h)).transposed()
    turn = hand @ model.inverted()
    at = PALM_MIDDLE + Vector((slide[0], sink, slide[1])) - turn @ grip
    return Matrix.Translation(at) @ turn.to_4x4()


def add_boomerang(rig, glb, name, bone, rest, local):
    """The model on a bone of the rig (at rest): `local` its place in that bone's frame."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=glb)
    ob = next(o for o in bpy.data.objects if o not in before and o.type == "MESH")
    ob.name = name
    ob.parent = rig
    ob.parent_type = "BONE"
    ob.parent_bone = bone
    bpy.context.view_layer.update()
    ob.matrix_world = in_blender(rest @ local)
    return ob


# The hold: the palm on the wrap of the arm that hangs behind the right shoulder blade, on the face that lies against the back (the hand goes in
# between), the fingers along that arm toward the boomerang's elbow. Held out sideways palm down, its other arm then trails behind.
IRON_HOLD = hold("lower", -1.0, HOLD_ACROSS, HOLD_SINK) if "HOLD_ACROSS" in globals() else hold("lower", -1.0, 40.0, 0.015, slide=(-0.084, -0.071))


def print_hold():
    """The item in the hand bone's frame (its model stays where the prefab has it in the item), for BoomerangBuilder."""
    item = IRON_HOLD @ ITEM_MODEL.inverted()
    q = item.to_quaternion()
    p = item.to_translation()
    print("HOLD position (%.4ff, %.4ff, %.4ff) rotation xyzw (%.5ff, %.5ff, %.5ff, %.5ff)" % (p.x, p.y, p.z, q.x, q.y, q.z, q.w))
    for name, at in (("other wrap", WRAPS["upper"]), ("elbow", ELBOW), ("held end", Vector((-1.0094, 0.0, -0.4116))), ("other end", Vector((-0.0762, 0.0, 0.9408)))):
        print("   in the hand, %s at %s" % (name, [round(c, 2) for c in (IRON_HOLD @ at)]))
