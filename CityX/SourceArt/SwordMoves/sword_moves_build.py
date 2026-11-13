# EVERYTHING FOR THE SWORD ATTACKS' CLIPS, headless:
#   blender --background --factory-startup --python sword_moves_build.py -- [--render <out_dir>] [--export <json path>] [--only <attack>]
# Builds the body rig, puts its sword in the game's hold, loads Octaslash's pose solver and the planted feet, bakes the attacks' bodies (sword_moves_pose.py: no arms, the
# game poses those), writes check renders of each attack's moments from the side, from the front and from the game's camera, and the JSON BakedMotion reads (the game's copy
# is Character/Resources/Swords/SwordMotion.json).
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE_ART = os.path.dirname(HERE)
OCTASLASH = os.path.join(SOURCE_ART, "Octaslash")
FIREARMS = os.path.join(SOURCE_ART, "Firearms")
REPO = os.path.dirname(os.path.dirname(SOURCE_ART))
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def run(path):
    exec(compile(open(path).read(), path, "exec"), globals())


bpy.ops.wm.read_factory_settings(use_empty=True)
run(os.path.join(OCTASLASH, "octaslash_rig.py"))
run(os.path.join(HERE, "sword_hold.py"))
OCTASLASH_NO_BAKE = True
run(os.path.join(OCTASLASH, "octaslash_pose.py"))
run(os.path.join(SOURCE_ART, "planted_feet.py"))
run(os.path.join(HERE, "sword_moves_pose.py"))
SPANS = bake_attacks()

if "--export" in args:
    EXPORT_PATH = args[args.index("--export") + 1]
    EXPORT_SEGMENTS = ORDER_OF_ATTACKS
    EXPORT_BONES = EXPORTED_BONES
    run(os.path.join(OCTASLASH, "octaslash_export.py"))

if "--render" in args:
    run(os.path.join(FIREARMS, "firearm_render.py"))
    out_dir = args[args.index("--render") + 1]
    only = args[args.index("--only") + 1] if "--only" in args else None
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    _workbench(scene, size=(420, 420))
    floor = bpy.data.meshes.new("Floor")
    floor.from_pydata([(-12, -12, 0), (12, -12, 0), (12, 12, 0), (-12, 12, 0)], [], [(0, 1, 2, 3)])
    scene.collection.objects.link(bpy.data.objects.new("Floor", floor))
    # Blender axes: the character faces -Y, its right is -X, up is +Z. Each view looks at the body where the step has carried it.
    VIEWS = {
        "side": (Vector((-7.5, -0.3, 1.4)), Vector((0.0, -0.3, 1.3)), 34.0),
        "front": (Vector((3.0, -7.0, 1.7)), Vector((0.0, 0.0, 1.3)), 34.0),
        "game": (Vector((-0.87, 5.0, 2.7)), Vector((0.0, -1.0, 1.4)), 50.0),
    }
    for name, (first, last, move) in SPANS.items():
        if only is not None and name != only:
            continue
        # The attack's moments: halfway up the wind-up, wound, the hold's end, three through the strike, the contact, the follow-through's end, and three on the way back.
        strike = move["contact"] - move["held"]
        back = move["seconds"] - move["settled"]
        marks = [move["wound"] * 0.5, move["wound"], move["held"], move["held"] + strike * 0.5, move["held"] + strike * 0.8, move["contact"], move["followed"],
                 move["settled"] + back * 0.3, move["settled"] + back * 0.6, move["seconds"]]
        for i, seconds in enumerate(marks):
            f = first + min(last - first, int(round(seconds * FPS)))
            scene.frame_set(f)
            bpy.context.view_layer.update()
            at = bpy.data.objects[RIG].location.copy()
            for view, (position, target, fov) in VIEWS.items():
                _camera(scene, position + at, target + at, fov)
                scene.render.filepath = os.path.join(out_dir, "%s_%s_%02d.png" % (name, view, i))
                bpy.ops.render.render(write_still=True)
    print("rendered to", out_dir)
