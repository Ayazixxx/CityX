# EVERYTHING FOR THE GLOCK, headless:
#   blender --background --python glock_build_all.py -- [--no-body] [--render <out_dir>] [--no-export]
# Builds the body rig with fingers (firearm_rig.py), the Glock (glock_model.py: writes Glock.glb and Glock.points.json), the reloads
# (glock_reload.py through firearm_pose.py), exports CityX/Assets/CityX/Character/Resources/Glock/GlockReload.json (firearm_export.py) and
# renders check frames.
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
EXPORT_PATH = os.path.join(REPO, "CityX", "Assets", "CityX", "Character", "Resources", "Glock", "GlockReload.json")


def run(path):
    exec(compile(open(path).read(), path, "exec"), globals())


run(os.path.join(FIREARMS, "firearm_rig.py"))
clear_scene()
build()                                                   # the rig (+ body)
GLOCK_STANDALONE = False
run(os.path.join(HERE, "glock_model.py"))
build(export=EXPORT)                                      # the gun, as a free object at the origin
run(os.path.join(FIREARMS, "firearm_pose.py"))
run(os.path.join(HERE, "glock_reload.py"))
count = bake(KEYS, SEGMENTS, EVENTS, "GlockReload")
print("baked %d frames" % count)
if "--debug" in args:
    scene = bpy.context.scene
    rig_ob = bpy.data.objects["CityXRig"]
    for f in (0, 18, 32, 52, 60, 120 + 82):
        scene.frame_set(f)
        bpy.context.view_layer.update()
        mag = bpy.data.objects["Magazine"]
        gun_ob = bpy.data.objects[GUN_NAME]
        mag_tilt = gun_ob.matrix_world.to_quaternion().rotation_difference(mag.matrix_world.to_quaternion()).angle
        print("frame %3d: hand_l %s  index1_r %s  gun %s  mag %s hidden=%s tilt %.0f deg  slide %s" % (
            f, tuple(round(c, 2) for c in rig_ob.pose.bones["hand_l"].head), tuple(round(c, 2) for c in rig_ob.pose.bones["index1_r"].head),
            tuple(round(c, 2) for c in gun_ob.matrix_world.translation), tuple(round(c, 2) for c in mag.matrix_world.translation),
            mag.hide_render, math.degrees(mag_tilt), tuple(round(c, 3) for c in bpy.data.objects["Slide"].location)))
if EXPORT:
    run(os.path.join(FIREARMS, "firearm_export.py"))
    export(EXPORT_PATH)
if RENDER_DIR:
    run(os.path.join(FIREARMS, "firearm_render.py"))
    frames = [f for f in args[args.index("--render") + 2:] if f.isdigit()]
    frames = [int(f) for f in frames] or [0, 8, 18, 32, 52, 60, 66, 84, 120 + 74, 120 + 82, 120 + 88]
    render_frames(RENDER_DIR, frames, "glock")
