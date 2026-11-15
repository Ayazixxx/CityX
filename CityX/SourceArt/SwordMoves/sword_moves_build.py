# EVERYTHING FOR THE SWORD ATTACKS' CLIPS, headless:
#   blender --background --factory-startup --python sword_moves_build.py -- [--render <out_dir>] [--export <json path>] [--only <attack>]
# Builds the body rig, puts its sword in the game's hold, loads Octaslash's pose solver and the planted feet, bakes the attacks' bodies (sword_moves_pose.py) and the sword
# arm of the one-handed cuts posed so far (sword_arms_1h.py), writes check renders of each attack's moments from the side, from the front and from the game's camera with
# the trail the tip leaves, and the JSON BakedMotion reads (the game's copies are Character/Resources/Swords/SwordMotion.json, the bodies, and SwordMotion1H.json, the
# one-handed cuts with their arm).
import math
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
run(os.path.join(HERE, "sword_arms_1h.py"))
SPANS = bake_attacks()

if "--export" in args:
    # Every attack's body (SwordMotion.json), and the one-handed attacks again with the sword arm (SwordMotion1H.json, beside it).
    EXPORT_PATH = args[args.index("--export") + 1]
    EXPORT_SEGMENTS = ORDER_OF_ATTACKS
    EXPORT_BONES = EXPORTED_BONES
    run(os.path.join(OCTASLASH, "octaslash_export.py"))
    EXPORT_PATH = EXPORT_PATH.replace(".json", "1H.json")
    EXPORT_SEGMENTS = tuple(n for n in ORDER_OF_ATTACKS if n in ARMS_1H)
    EXPORT_UNTIL = {n: (ORDER_OF_ATTACKS + ("end",))[ORDER_OF_ATTACKS.index(n) + 1] for n in EXPORT_SEGMENTS}
    EXPORT_BONES = EXPORTED_BONES + ARM_BONES_1H
    run(os.path.join(OCTASLASH, "octaslash_export.py"))

if "--render" in args:
    run(os.path.join(FIREARMS, "firearm_render.py"))
    out_dir = args[args.index("--render") + 1]
    only = args[args.index("--only") + 1].split(",") if "--only" in args else None
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
        # The game camera's own eye, turned down onto the body (from the same eye a straight trail stays straight whichever way it looks).
        "game": (U(*CAMERA_EYE), U(0.0, 1.3, 0.6), 36.0),
    }
    trail_paint = bpy.data.materials.new("Trail")
    trail_paint.diffuse_color = (1.0, 0.45, 0.05, 1.0)
    for name, (first, last, move) in SPANS.items():
        if only is not None and name not in only:
            continue
        # The sword's tip from the hold's end to the follow-through's end, drawn as the trail the game leaves.
        old = bpy.data.objects.get("Trail")
        if old is not None:
            bpy.data.objects.remove(old)
        tips = []
        for f in range(first + int(move["held"] * FPS), first + int(move["followed"] * FPS) + 1):
            scene.frame_set(f)
            m = bpy.data.objects[SWORD].matrix_world
            tips.append(m.translation + m.to_quaternion() @ Vector((0.0, 0.0, GAME_ROOT_OUT + COMBAT_BLADE)))
        curve = bpy.data.curves.new("Trail", "CURVE")
        curve.dimensions = "3D"
        curve.bevel_depth = 0.012
        spline = curve.splines.new("POLY")
        spline.points.add(len(tips) - 1)
        for point, tip in zip(spline.points, tips):
            point.co = (tip.x, tip.y, tip.z, 1.0)
        curve.materials.append(trail_paint)
        scene.collection.objects.link(bpy.data.objects.new("Trail", curve))
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
