# EVERYTHING FOR THE IRON SPIKED GIANT BOOMERANG'S OWN CLIPS, headless:
#   blender --background --factory-startup --python iron_spiked_moves_build.py -- [--render <out_dir>] [--export <json path>]
# Builds the body rig, loads Octaslash's pose solver, the planted feet and the punches' pose helpers, bakes the four moves (iron_spiked_moves.py), puts
# the boomerang's model on the back and in the hand as the game has it (iron_spiked_hold.py), writes a sheet of check renders per move (its moments from
# the game's camera, the front and above) and the JSON BakedMotion reads (the game's copy is Character/Resources/GiantBoomerang/IronSpikedBoomerangMotion.json).
# Prints the hold for BoomerangBuilder.
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE_ART = os.path.dirname(HERE)
OCTASLASH = os.path.join(SOURCE_ART, "Octaslash")
FIREARMS = os.path.join(SOURCE_ART, "Firearms")
REPO = os.path.dirname(os.path.dirname(SOURCE_ART))
MODEL = os.path.join(REPO, "CityX", "Assets", "CityX", "Weapons", "IronSpikedBoomerang.glb")
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

# How many moments of each move a sheet shows, and each picture's size.
MOMENTS = 7
PICTURE = 400


def run(path):
    exec(compile(open(path).read(), path, "exec"), globals())


bpy.ops.wm.read_factory_settings(use_empty=True)
run(os.path.join(OCTASLASH, "octaslash_rig.py"))
run(os.path.join(HERE, "iron_spiked_hold.py"))
RIG_OBJECT = bpy.data.objects["CityXRig"]
IN_HAND = add_boomerang(RIG_OBJECT, MODEL, "IronInHand", "hand_r", HAND_REST, IRON_HOLD)
ON_BACK = add_boomerang(RIG_OBJECT, MODEL, "IronOnBack", "spine", SPINE_REST, BACK_HANG @ BACK_MODEL)
OCTASLASH_NO_BAKE = True
run(os.path.join(OCTASLASH, "octaslash_pose.py"))
run(os.path.join(SOURCE_ART, "planted_feet.py"))
run(os.path.join(SOURCE_ART, "FistCombo", "fist_pose.py"))
run(os.path.join(HERE, "iron_spiked_moves.py"))
SPANS = bake_moves()
bpy.data.objects[SWORD].hide_render = True
print_hold()
grab_report()

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
    scene.display.shading.color_type = 'TEXTURE'
    paint = bpy.data.images.load(MODEL.replace(".glb", ".jpg"))
    steel = bpy.data.materials.new("IronSpiked")
    steel.use_nodes = True
    shader = next(n for n in steel.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    picture = steel.node_tree.nodes.new("ShaderNodeTexImage")
    picture.image = paint
    steel.node_tree.links.new(picture.outputs["Color"], shader.inputs["Base Color"])
    for ob in (IN_HAND, ON_BACK):
        ob.data.materials.clear()
        ob.data.materials.append(steel)
    floor = bpy.data.meshes.new("Floor")
    floor.from_pydata([(-12, -12, 0), (12, -12, 0), (12, 12, 0), (-12, 12, 0)], [], [(0, 1, 2, 3)])
    scene.collection.objects.link(bpy.data.objects.new("Floor", floor))
    # Blender axes: the character faces -Y, its right is -X, up is +Z. The game's camera: behind, above the head, a little to the right.
    VIEWS = (
        ("game", Vector((-0.87, 5.0, 2.9)), Vector((-0.5, -1.0, 1.5)), 50.0),
        ("front", Vector((-2.8, -6.0, 1.9)), Vector((-0.3, 0.0, 1.4)), 36.0),
        ("above", Vector((-0.4, 0.3, 9.0)), Vector((-0.4, 0.0, 1.4)), 36.0),
    )
    for name, (first, last, move) in SPANS.items():
        held_from, held_to = move["held"]
        sheet = np.zeros((PICTURE * len(VIEWS), PICTURE * MOMENTS, 4), dtype=np.float32)
        for i in range(MOMENTS):
            share = i / float(MOMENTS - 1)
            scene.frame_set(first + int(round(share * (last - first))))
            in_hand = held_from <= share < held_to
            IN_HAND.hide_render = not in_hand
            ON_BACK.hide_render = in_hand or (name != "iron_draw" and not (name == "iron_throw_quick" and share < held_from))
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
