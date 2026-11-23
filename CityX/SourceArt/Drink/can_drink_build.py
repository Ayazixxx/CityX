# THE CAN DRINK, headless:
#   blender --background --factory-startup --python can_drink_build.py -- [--render <out_dir>] [--export <json path>]
# Builds the body rig, loads Octaslash's pose solver, the planted feet, the punches' pose helpers, the wrist turn and the head carry, puts a can in the
# right hand as the game holds it (PhizzCan.HoldPosition, HoldRotation: keep CAN_HOLD in step), bakes can_drink.py, writes a sheet of check renders
# (moments from the game's camera, the front and the side) and the JSON BakedMotion reads (the game's copy is Character/Resources/Drink/CanDrinkMotion.json).
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

MOMENTS = 12
PICTURE = 360


def run(path):
    exec(compile(open(path).read(), path, "exec"), globals())


bpy.ops.wm.read_factory_settings(use_empty=True)
run(os.path.join(OCTASLASH, "octaslash_rig.py"))
run(os.path.join(SOURCE_ART, "game_frames.py"))

# The can in the right hand bone's frame, as the game holds it (PhizzCan.HoldPosition, HoldRotation): its base at the origin of its own frame, +Y up through
# its lid; in the fist its axis runs along the thumb's side (+Z), the fist round its middle, its side on the palm, its hole (its -X) toward the wrist.
CAN_HOLD = u_trs((0.10, -0.121, -0.148), u_look((0.0, -1.0, 0.0), (0.0, 0.0, 1.0)))


def add_can(rig):
    """A plain can on the right hand as the game holds one, its hole marked dark on the lid."""
    life = 2.4
    radius, height, rim = 0.033 * life, 0.1222 * life, 0.0262 * life
    sides = 20
    verts, faces = [], []
    for r, y in ((0.0, 0.0), (radius, 0.0), (radius, height * 0.9), (rim, height), (0.0, height)):
        for s in range(sides):
            a = s * 2.0 * math.pi / sides
            verts.append(TO_BLENDER.to_3x3() @ Vector((r * math.cos(a), y, r * math.sin(a))))
    for i in range(4):
        for s in range(sides):
            a, b = i * sides + s, i * sides + (s + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    hole = len(verts)
    for x, z in ((-0.024 * life, -0.006 * life), (-0.010 * life, -0.006 * life), (-0.010 * life, 0.006 * life), (-0.024 * life, 0.006 * life)):
        verts.append(TO_BLENDER.to_3x3() @ Vector((x, height + 0.002, z)))
    faces.append((hole, hole + 1, hole + 2, hole + 3))
    mesh = bpy.data.meshes.new("Can")
    mesh.from_pydata(verts, [], faces)
    gold = bpy.data.materials.new("CanGold")
    gold.diffuse_color = (0.95, 0.79, 0.22, 1.0)
    dark = bpy.data.materials.new("CanHole")
    dark.diffuse_color = (0.02, 0.02, 0.02, 1.0)
    mesh.materials.append(gold)
    mesh.materials.append(dark)
    mesh.polygons[-1].material_index = 1
    mesh.update()
    ob = bpy.data.objects.new("CanInHand", mesh)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = rig
    ob.parent_type = "BONE"
    ob.parent_bone = "hand_r"
    bpy.context.view_layer.update()
    ob.matrix_world = in_blender(HAND_REST @ CAN_HOLD)
    return ob


RIG_OBJECT = bpy.data.objects["CityXRig"]
IN_HAND = add_can(RIG_OBJECT)
OCTASLASH_NO_BAKE = True
run(os.path.join(OCTASLASH, "octaslash_pose.py"))
run(os.path.join(SOURCE_ART, "planted_feet.py"))
run(os.path.join(SOURCE_ART, "FistCombo", "fist_pose.py"))
run(os.path.join(SOURCE_ART, "wrist_turn.py"))
run(os.path.join(SOURCE_ART, "head_carry.py"))
run(os.path.join(HERE, "can_drink.py"))
FRAMES = bake_drink()
if SWORD in bpy.data.objects:
    bpy.data.objects[SWORD].hide_render = True

if "--export" in args:
    EXPORT_PATH = args[args.index("--export") + 1]
    EXPORT_SEGMENTS = ("can_drink",)
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
    # Blender axes: the character faces -Y, its right is -X, up is +Z.
    VIEWS = (
        ("game", Vector((-0.87, 5.0, 2.9)), Vector((-0.5, -1.0, 1.5)), 50.0),
        ("front", Vector((-2.0, -5.0, 2.2)), Vector((-0.2, 0.0, 1.8)), 32.0),
        ("side", Vector((-6.0, -0.6, 2.1)), Vector((0.0, -0.2, 1.8)), 30.0),
        ("face", Vector((-1.4, -1.9, 2.4)), Vector((-0.1, -0.2, 2.1)), 34.0),
    )
    # Hand-picked moments (seconds): the reach, the tab, the sip, the gulp, the drop.
    SECONDS_SHOWN = (0.0, 0.32, 0.44, 0.75, 1.36, 1.6, 1.95, 2.25, 2.55, 2.9, 3.28, 3.49)
    sheet = np.zeros((PICTURE * len(VIEWS), PICTURE * len(SECONDS_SHOWN), 4), dtype=np.float32)
    for i, seconds in enumerate(SECONDS_SHOWN):
        scene.frame_set(min(FRAMES - 1, int(round(seconds * FPS))))
        IN_HAND.hide_render = seconds > 3.26
        bpy.context.view_layer.update()
        for row, (view, position, target, fov) in enumerate(VIEWS):
            _camera(scene, position, target, fov)
            path = os.path.join(out_dir, "_shot.png")
            scene.render.filepath = path
            bpy.ops.render.render(write_still=True)
            shot = bpy.data.images.load(path)
            pixels = np.array(shot.pixels[:], dtype=np.float32).reshape(PICTURE, PICTURE, 4)
            bpy.data.images.remove(shot)
            top = (len(VIEWS) - 1 - row) * PICTURE
            sheet[top:top + PICTURE, i * PICTURE:(i + 1) * PICTURE] = pixels
    out = bpy.data.images.new("can_drink", PICTURE * len(SECONDS_SHOWN), PICTURE * len(VIEWS), alpha=True)
    out.pixels = sheet.ravel()
    out.filepath_raw = os.path.join(out_dir, "can_drink.png")
    out.file_format = 'PNG'
    out.save()
    os.remove(os.path.join(out_dir, "_shot.png"))
    print("rendered to", out_dir)
