# THE GET-UP CLIPS, headless:
#   blender --background --factory-startup --python getup_build.py -- [--render <out_dir>] [--export <json path>]
# Builds the body rig, loads Octaslash's pose solver, bakes both get-ups (getup_pose.py), writes check renders of each from the side, from the
# front and from above-behind, and the JSON BakedMotion reads (the game's copy is Character/Resources/GetUp/GetUpMotion.json).
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
run(os.path.join(HERE, "getup_pose.py"))
GETUP_VERBOSE = "--verbose" in args
SPANS = bake_getups()
bpy.data.objects[SWORD].hide_render = True

if "--export" in args:
    EXPORT_PATH = args[args.index("--export") + 1]
    EXPORT_SEGMENTS = tuple(name for name, _ in CLIPS)
    run(os.path.join(OCTASLASH, "octaslash_export.py"))

if "--render" in args:
    run(os.path.join(FIREARMS, "firearm_render.py"))
    out_dir = args[args.index("--render") + 1]
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    _workbench(scene, size=(360, 300))
    floor = bpy.data.meshes.new("Floor")
    floor.from_pydata([(-12, -12, 0), (12, -12, 0), (12, 12, 0), (-12, 12, 0)], [], [(0, 1, 2, 3)])
    scene.collection.objects.link(bpy.data.objects.new("Floor", floor))
    # Blender axes: the character faces -Y, its right is -X, up is +Z.
    VIEWS = {
        "side": (Vector((-6.0, -0.2, 1.4)), Vector((0.0, -0.2, 0.7)), 38.0),
        "front": (Vector((3.2, -5.0, 2.0)), Vector((0.0, 0.0, 0.7)), 38.0),
        "back": (Vector((-1.5, 5.5, 2.8)), Vector((0.0, 0.0, 0.7)), 38.0),
    }
    step = int(args[args.index("--every") + 1]) if "--every" in args else 12
    only = args[args.index("--only") + 1] if "--only" in args else None
    upto = int(args[args.index("--upto") + 1]) if "--upto" in args else 100000
    if "--close" in args:
        _workbench(scene, size=(400, 300))
        VIEWS = {
            "side": (Vector((-4.2, -0.1, 1.2)), Vector((0.0, -0.1, 0.55)), 45.0),
            "roll": (Vector((3.6, 0.6, 2.2)), Vector((0.0, 0.1, 0.3)), 45.0),
            "high": (Vector((0.6, -3.6, 3.4)), Vector((0.0, 0.0, 0.4)), 45.0),
            "left": (Vector((5.4, 1.2, 2.2)), Vector((0.0, 0.0, 0.9)), 40.0),
        }
    for name, (first, last, keys) in SPANS.items():
        if only is not None and name != only:
            continue
        last = min(last, first + upto)
        frames = list(range(first, last + 1, step))
        if frames[-1] != last:
            frames.append(last)
        for i, f in enumerate(frames):
            scene.frame_set(f)
            bpy.context.view_layer.update()
            for view, (position, target, fov) in VIEWS.items():
                _camera(scene, position, target, fov)
                scene.render.filepath = os.path.join(out_dir, "%s_%s_%02d.png" % (name, view, i))
                bpy.ops.render.render(write_still=True)
    print("rendered to", out_dir)

if "--skin" in args:
    # The skinned body itself, every other frame: how far its lowest point goes under the floor, and on which bone (GetUpFloorPlayTests
    # measures the same in the game).
    import numpy as np
    scene = bpy.context.scene
    parts = [bpy.data.objects["Body"], bpy.data.objects["Head"]]
    for name, (first, last, keys) in SPANS.items():
        worst = (9.0, "", 0.0)
        for f in range(first, last + 1, 2):
            scene.frame_set(f)
            depsgraph = bpy.context.evaluated_depsgraph_get()
            low = (9.0, "")
            for part in parts:
                evaluated = part.evaluated_get(depsgraph)
                mesh = evaluated.to_mesh()
                co = np.empty(len(mesh.vertices) * 3)
                mesh.vertices.foreach_get("co", co)
                z = co[2::3]
                i = int(np.argmin(z))
                if z[i] < low[0]:
                    groups = part.data.vertices[i].groups
                    bone = part.vertex_groups[max(groups, key=lambda g: g.weight).group].name if len(groups) else part.name
                    low = (float(z[i]), bone)
                evaluated.to_mesh_clear()
            seconds = (f - first) / float(FPS)
            if low[0] < -0.02:
                print("  skin %s %.2f s: %.3f under, %s" % (name, seconds, -low[0], low[1]))
            if low[0] < worst[0]:
                worst = (low[0], low[1], seconds)
        print("SKIN %-12s lowest %.3f (%s at %.2f s)" % (name, worst[0], worst[1], worst[2]))
