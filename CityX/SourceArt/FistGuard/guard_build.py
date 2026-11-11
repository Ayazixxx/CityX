# THE BARE-HANDED GUARD'S CLIPS, headless:
#   blender --background --factory-startup --python guard_build.py -- [--render <out_dir>] [--export <json path>]
# Builds the body rig, loads Octaslash's pose solver, the planted feet and the punches' pose helpers, bakes the guard (guard_pose.py), writes check renders from
# the front, the side and the game's camera, and the JSON BakedMotion reads (the game's copy is Character/Resources/Fists/FistGuardMotion.json).
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
OCTASLASH_NO_BAKE = True
run(os.path.join(OCTASLASH, "octaslash_pose.py"))
run(os.path.join(SOURCE_ART, "planted_feet.py"))
run(os.path.join(SOURCE_ART, "FistCombo", "fist_pose.py"))
run(os.path.join(HERE, "guard_pose.py"))
SPANS = bake_guards()
bpy.data.objects[SWORD].hide_render = True

if "--export" in args:
    EXPORT_PATH = args[args.index("--export") + 1]
    EXPORT_SEGMENTS = ORDER_OF_GUARDS
    run(os.path.join(OCTASLASH, "octaslash_export.py"))

if "--render" in args:
    run(os.path.join(FIREARMS, "firearm_render.py"))
    out_dir = args[args.index("--render") + 1]
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    _workbench(scene, size=(360, 420))
    floor = bpy.data.meshes.new("Floor")
    floor.from_pydata([(-12, -12, 0), (12, -12, 0), (12, 12, 0), (-12, 12, 0)], [], [(0, 1, 2, 3)])
    scene.collection.objects.link(bpy.data.objects.new("Floor", floor))
    # Blender axes: the character faces -Y, its right is -X, up is +Z.
    VIEWS = {
        "front": (Vector((1.6, -6.0, 1.9)), Vector((0.0, 0.0, 1.4)), 34.0),
        "side": (Vector((-6.5, -0.3, 1.6)), Vector((0.0, -0.3, 1.4)), 34.0),
        "game": (Vector((-0.87, 5.0, 2.7)), Vector((0.0, -1.0, 1.4)), 50.0),
    }
    for name, (first, last, keys) in SPANS.items():
        shares = sorted(set([t for t, _, _ in keys] + [0.5]))
        for i, share in enumerate(shares):
            scene.frame_set(first + int(round(share * (last - first))))
            bpy.context.view_layer.update()
            for view, (position, target, fov) in VIEWS.items():
                _camera(scene, position, target, fov)
                scene.render.filepath = os.path.join(out_dir, "%s_%s_%02d.png" % (name, view, i))
                bpy.ops.render.render(write_still=True)
    print("rendered to", out_dir)
