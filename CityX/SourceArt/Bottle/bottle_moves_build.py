# EVERYTHING FOR THE BOTTLE'S CLIPS, headless:
#   blender --background --factory-startup --python bottle_moves_build.py -- [--render <out_dir>] [--export <json path>]
# Builds the body rig, loads Octaslash's pose solver, the planted feet and the punches' pose helpers, bakes the two moves (bottle_moves.py), puts a
# bottle in the right hand as the game holds it (BottleWeapon.HoldPosition, HoldRotation), writes a sheet of check renders per move (its moments
# from the game's camera, the front and the side) and the JSON BakedMotion reads (the game's copy is Character/Resources/Bottle/BottleMotion.json).
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE_ART = os.path.dirname(HERE)
OCTASLASH = os.path.join(SOURCE_ART, "Octaslash")
FIREARMS = os.path.join(SOURCE_ART, "Firearms")
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

# How many moments of each move a sheet shows, and each picture's size.
MOMENTS = 7
PICTURE = 400


def run(path):
    exec(compile(open(path).read(), path, "exec"), globals())


def add_bottle(rig):
    """A plain bottle on the right hand as the game holds one: in its own frame the fist's middle on the neck is the origin and it runs out along +y
    (the game's axes) to its base; in the hand (SwordWeapon.FistPoint, DefaultOneHandGripRotation) that is out past the thumb."""
    # (radius, how far along from the fist), the game's metres: the mouth behind the fist, the neck through it, the shoulder, the body, the base.
    profile = ((0.0, -0.13), (0.035, -0.13), (0.032, -0.02), (0.033, 0.06), (0.075, 0.17), (0.088, 0.22), (0.088, 0.40), (0.0, 0.42))
    sides = 14
    verts, faces = [], []
    for r, along in profile:
        for s in range(sides):
            a = s * 2.0 * math.pi / sides
            verts.append(TO_BLENDER.to_3x3() @ Vector((r * math.cos(a), along, r * math.sin(a))))
    for i in range(len(profile) - 1):
        for s in range(sides):
            a, b = i * sides + s, i * sides + (s + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    mesh = bpy.data.meshes.new("Bottle")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    ob = bpy.data.objects.new("BottleInHand", mesh)
    bpy.context.scene.collection.objects.link(ob)
    glass = bpy.data.materials.new("BottleGlass")
    glass.diffuse_color = (0.36, 0.20, 0.07, 1.0)
    mesh.materials.append(glass)
    ob.parent = rig
    ob.parent_type = "BONE"
    ob.parent_bone = "hand_r"
    bpy.context.view_layer.update()
    palm_half_thickness = 0.042                                  # FusedBodyShape.PalmHalfThickness
    fist = (0.08, -(palm_half_thickness + 0.03), 0.0)            # SwordWeapon.FistPoint
    axis = Vector((-0.056, 0.0, 0.998)).normalized()             # SwordWeapon.OneHandBladeAxisInHand
    hold = u_trs(fist, u_look((0.0, 1.0, 0.0), axis))            # Quaternion.LookRotation(Vector3.up, axis)
    ob.matrix_world = in_blender(HAND_REST @ hold)
    return ob


bpy.ops.wm.read_factory_settings(use_empty=True)
run(os.path.join(OCTASLASH, "octaslash_rig.py"))
run(os.path.join(SOURCE_ART, "game_frames.py"))
RIG_OBJECT = bpy.data.objects["CityXRig"]
IN_HAND = add_bottle(RIG_OBJECT)
OCTASLASH_NO_BAKE = True
run(os.path.join(OCTASLASH, "octaslash_pose.py"))
run(os.path.join(SOURCE_ART, "planted_feet.py"))
run(os.path.join(SOURCE_ART, "FistCombo", "fist_pose.py"))
run(os.path.join(SOURCE_ART, "wrist_turn.py"))
run(os.path.join(SOURCE_ART, "head_carry.py"))
run(os.path.join(HERE, "bottle_moves.py"))
SPANS = bake_moves()
bpy.data.objects[SWORD].hide_render = True

if "--export" in args:
    EXPORT_PATH = args[args.index("--export") + 1]
    EXPORT_SEGMENTS = ORDER_OF_MOVES
    run(os.path.join(OCTASLASH, "octaslash_export.py"))

if "--render" in args:
    run(os.path.join(FIREARMS, "firearm_render.py"))
    out_dir = args[args.index("--render") + 1]
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    _workbench(scene, size=(PICTURE, PICTURE))
    floor = bpy.data.meshes.new("Floor")
    floor.from_pydata([(-12, -12, 0), (12, -12, 0), (12, 12, 0), (-12, 12, 0)], [], [(0, 1, 2, 3)])
    scene.collection.objects.link(bpy.data.objects.new("Floor", floor))
    # Blender axes: the character faces -Y, its right is -X, up is +Z. The game's camera: behind, above the head, a little to the right.
    VIEWS = (
        ("game", Vector((-0.87, 5.0, 2.9)), Vector((-0.5, -1.0, 1.5)), 50.0),
        ("front", Vector((-2.4, -6.0, 1.9)), Vector((-0.3, 0.0, 1.5)), 36.0),
        ("side", Vector((-7.0, -0.4, 1.8)), Vector((0.0, -0.2, 1.5)), 36.0),
    )
    for name, (first, last, move) in SPANS.items():
        held_from, held_to = move["held"]
        sheet = np.zeros((PICTURE * len(VIEWS), PICTURE * MOMENTS, 4), dtype=np.float32)
        for i in range(MOMENTS):
            share = i / float(MOMENTS - 1)
            scene.frame_set(first + int(round(share * (last - first))))
            IN_HAND.hide_render = not (held_from <= share < held_to)
            bpy.context.view_layer.update()
            for row, (view, position, target, fov) in enumerate(VIEWS):
                _camera(scene, position, target, fov)
                path = os.path.join(out_dir, "_shot.png")
                scene.render.filepath = path
                bpy.ops.render.render(write_still=True)
                shot = bpy.data.images.load(path)
                pixels = np.array(shot.pixels[:], dtype=np.float32).reshape(PICTURE, PICTURE, 4)
                bpy.data.images.remove(shot)
                top = (len(VIEWS) - 1 - row) * PICTURE          # image rows run bottom-up
                sheet[top:top + PICTURE, i * PICTURE:(i + 1) * PICTURE] = pixels
        out = bpy.data.images.new(name, PICTURE * MOMENTS, PICTURE * len(VIEWS), alpha=True)
        out.pixels = sheet.ravel()
        out.filepath_raw = os.path.join(out_dir, name + ".png")
        out.file_format = 'PNG'
        out.save()
    os.remove(os.path.join(out_dir, "_shot.png"))
    print("rendered to", out_dir)
