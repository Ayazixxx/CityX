# EVERYTHING FOR THE PUNCHES' CLIPS, headless:
#   blender --background --factory-startup --python fist_build.py -- [--render <out_dir>] [--export <json path>]
# Builds the body rig, loads Octaslash's pose solver and the planted feet, bakes the four punches (fist_pose.py), writes check renders of each punch's moments from the side, from
# the front and from the game's camera, and the JSON BakedMotion reads (the game's copy is Character/Resources/Fists/FistMotion.json).
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
run(os.path.join(HERE, "fist_pose.py"))
SPANS = bake_punches()
# The rig comes with a sword in its hand; a punch is thrown without one.
bpy.data.objects[SWORD].hide_render = True

if "--export" in args:
    EXPORT_PATH = args[args.index("--export") + 1]
    EXPORT_SEGMENTS = ORDER_OF_PUNCHES
    run(os.path.join(OCTASLASH, "octaslash_export.py"))

if "--render" in args:
    run(os.path.join(FIREARMS, "firearm_render.py"))
    out_dir = args[args.index("--render") + 1]
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    _workbench(scene, size=(420, 420))
    floor = bpy.data.meshes.new("Floor")
    floor.from_pydata([(-12, -12, 0), (12, -12, 0), (12, 12, 0), (-12, 12, 0)], [], [(0, 1, 2, 3)])
    scene.collection.objects.link(bpy.data.objects.new("Floor", floor))
    # Blender axes: the character faces -Y, its right is -X, up is +Z. Each view looks at the body where the step has carried it.
    VIEWS = {
        "side": (Vector((-6.5, -0.3, 1.3)), Vector((0.0, -0.3, 1.25)), 34.0),
        "front": (Vector((2.6, -6.0, 1.5)), Vector((0.0, 0.0, 1.25)), 34.0),
        "game": (Vector((-0.87, 5.0, 2.7)), Vector((0.0, -1.0, 1.4)), 50.0),
    }
    for name, (first, last, punch) in SPANS.items():
        # The blow's moments: every key, and two more through the strike, where the hips, the shoulders and the fist go one after another.
        times = [t for t, _, _ in punch["keys"]]
        strike = next(i for i, (_, k, _) in enumerate(punch["keys"]) if k["staged"])
        t0, t1 = times[strike - 1], times[strike]
        marks = sorted(set(times[:-1] + [t0 + (t1 - t0) * 0.45, t0 + (t1 - t0) * 0.8, 0.93]))
        for i, share in enumerate(marks):
            f = first + min(last - first, int(round(share * (last - first + 1))))
            scene.frame_set(f)
            bpy.context.view_layer.update()
            at = bpy.data.objects[RIG].location.copy()
            for view, (position, target, fov) in VIEWS.items():
                _camera(scene, position + at, target + at, fov)
                scene.render.filepath = os.path.join(out_dir, "%s_%s_%02d.png" % (name, view, i))
                bpy.ops.render.render(write_still=True)
    print("rendered to", out_dir)
