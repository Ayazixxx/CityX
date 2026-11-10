# EVERYTHING FOR THE ONE-HANDED COMBO'S CLIPS, headless:
#   blender --background --factory-startup --python sword_combo_build.py -- [--render <out_dir>] [--export <json path>]
# Builds the body rig, loads Octaslash's pose solver, bakes the three cuts (sword_combo_pose.py), writes check renders of each cut's moments from the side, from the front and
# from the game's camera, and the JSON BakedMotion reads.
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OCTASLASH = os.path.join(os.path.dirname(HERE), "Octaslash")
FIREARMS = os.path.join(os.path.dirname(HERE), "Firearms")
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def run(path):
    exec(compile(open(path).read(), path, "exec"), globals())


bpy.ops.wm.read_factory_settings(use_empty=True)
run(os.path.join(OCTASLASH, "octaslash_rig.py"))
OCTASLASH_NO_BAKE = True
run(os.path.join(OCTASLASH, "octaslash_pose.py"))
run(os.path.join(os.path.dirname(HERE), "planted_feet.py"))
run(os.path.join(HERE, "sword_combo_pose.py"))
SPANS = bake_combo()

if "--export" in args:
    EXPORT_PATH = args[args.index("--export") + 1]
    EXPORT_SEGMENTS = ("slash", "cut", "finisher")
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
    # Blender axes: the character faces -Y, its right is -X, up is +Z. Each view looks at the body where the lunge has it.
    VIEWS = {
        "side": (Vector((-6.5, 0.0, 1.3)), Vector((0.0, 0.0, 1.15)), 34.0),
        "front": (Vector((2.6, -6.0, 1.5)), Vector((0.0, 0.0, 1.15)), 34.0),
        "game": (Vector((-0.87, 5.0, 2.7)), Vector((0.0, -1.0, 1.4)), 50.0),
    }
    for name, (first, last, total, keys) in SPANS.items():
        # The cut's moments: the start, the stride, the wind-up's end, two through the strike, the contact, the follow-through, the settle, the recovery and the end.
        marks = [keys[1][0] * 0.5, keys[1][0], keys[2][0], (keys[2][0] + keys[3][0]) * 0.5, keys[3][0] - (keys[3][0] - keys[2][0]) * 0.15, keys[3][0], keys[4][0], keys[5][0], (keys[5][0] + 1.0) * 0.5, 1.0]
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
