# EVERYTHING FOR THE AK-47, headless:
#   blender --background --python ak47_build_all.py -- [--no-body] [--render <out_dir> [frames...]] [--no-export] [--debug]
# Builds the body rig with fingers (Firearms/firearm_rig.py), the AK-47 (ak47_model.py: writes AK47.glb and AK47.points.json), the reloads
# (ak47_reload.py through Firearms/firearm_pose.py), exports CityX/Assets/CityX/Character/Resources/AK47/AK47Reload.json and renders check frames.
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
FIREARMS = os.path.join(os.path.dirname(HERE), "Firearms")
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
if "--no-body" in args:
    os.environ["FIREARM_NO_BODY"] = "1"
RENDER_DIR = args[args.index("--render") + 1] if "--render" in args else None
EXPORT = "--no-export" not in args
EXPORT_PATH = os.path.join(REPO, "CityX", "Assets", "CityX", "Character", "Resources", "AK47", "AK47Reload.json")


def run(path):
    exec(compile(open(path).read(), path, "exec"), globals())


run(os.path.join(FIREARMS, "firearm_rig.py"))
clear_scene()
build()                                                   # the rig (+ body, or the capsule stand-in)
run(os.path.join(HERE, "ak47_model.py"))
build(export=EXPORT)                                      # the gun, a free object at the origin
run(os.path.join(FIREARMS, "firearm_pose.py"))
run(os.path.join(HERE, "ak47_reload.py"))
count = bake(KEYS, SEGMENTS, EVENTS, "AK47Reload")
print("baked %d frames" % count)
if "--debug" in args:
    scene = bpy.context.scene
    rig_ob = bpy.data.objects["CityXRig"]
    for f in (0, 12, 22, 32, 58, 84, 92, 150 + 112, 150 + 120):
        scene.frame_set(f)
        bpy.context.view_layer.update()
        gun_ob = bpy.data.objects[GUN_NAME]
        hand = rig_ob.matrix_world @ rig_ob.pose.bones["hand_l"].head
        reach = (rig_ob.pose.bones["hand_l"].head - rig_ob.pose.bones["arm_l"].head).length
        print("frame %3d: left wrist %s (%.2f m from its shoulder), gun %s, mag hidden %s, bolt z %.3f" % (
            f, tuple(round(c, 2) for c in hand), reach, tuple(round(c, 2) for c in gun_ob.matrix_world.translation),
            bpy.data.objects["Magazine"].hide_render, bpy.data.objects["Bolt"].location.y))
if EXPORT:
    run(os.path.join(FIREARMS, "firearm_export.py"))
    export(EXPORT_PATH)
if RENDER_DIR:
    run(os.path.join(FIREARMS, "firearm_render.py"))
    frames = [int(f) for f in args[args.index("--render") + 2:] if f.isdigit()] or [0, 12, 22, 38, 58, 84, 92, 150 + 112, 150 + 120]
    render_frames(RENDER_DIR, frames, "ak")
