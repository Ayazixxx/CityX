# EVERYTHING FOR THE PARACHUTE'S POSES, headless:
#   blender --background --factory-startup --python parachute_build.py -- [--render <out_dir>] [--export]
# Builds the body rig, loads Octaslash's pose solver, bakes the poses (parachute_pose.py), writes check renders of each (the skydive ones laid face down as the game lays
# them) and the JSON BakedMotion reads (Character/Resources/Parachute/ParachuteMotion.json).
import math
import os
import sys

import bpy
from mathutils import Euler, Vector

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
OCTASLASH_NO_BAKE = True
run(os.path.join(OCTASLASH, "octaslash_pose.py"))
run(os.path.join(HERE, "parachute_pose.py"))
SPANS = bake_moves()
bpy.data.objects[SWORD].hide_render = True

if "--export" in args:
    EXPORT_PATH = os.path.join(REPO, "CityX", "Assets", "CityX", "Character", "Resources", "Parachute", "ParachuteMotion.json")
    EXPORT_SEGMENTS = ORDER_OF_MOVES
    run(os.path.join(OCTASLASH, "octaslash_export.py"))

if "--render" in args:
    run(os.path.join(FIREARMS, "firearm_render.py"))
    out_dir = args[args.index("--render") + 1]
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    _workbench(scene, size=(360, 360))
    rig_ob = bpy.data.objects[RIG]
    # Blender axes: the character faces -Y, its right is -X, up is +Z. Face down, it is pitched forward about its middle, as the game lays it (ParachuteRider.FreefallPitch).
    VIEWS = {
        "side": (Vector((-6.5, 0.0, 1.5)), Vector((0.0, 0.0, 1.45)), 34.0),
        "front": (Vector((2.6, -6.0, 1.6)), Vector((0.0, 0.0, 1.45)), 34.0),
        "behind": (Vector((-0.87, 5.0, 2.7)), Vector((0.0, -1.0, 1.4)), 50.0),
        "below": (Vector((-1.5, -1.0, -4.5)), Vector((0.0, 0.0, 1.3)), 50.0),
    }
    for name, (first, last) in SPANS.items():
        face_down = name.startswith("skydive") or name == "chute_pull"
        for f in sorted({first, (first + last) // 2, last}):
            scene.frame_set(f)
            rig_ob.rotation_euler = Euler((math.radians(85.0) if face_down else 0.0, 0.0, 0.0))
            rig_ob.location = Vector((0.0, 1.39, 1.28)) if face_down else Vector((0.0, 0.0, 0.0))
            bpy.context.view_layer.update()
            for view, (position, target, fov) in VIEWS.items():
                _camera(scene, position, target, fov)
                scene.render.filepath = os.path.join(out_dir, "%s_%03d_%s.png" % (name, f - first, view))
                bpy.ops.render.render(write_still=True)
    print("rendered to", out_dir)
