# EVERYTHING FOR THE RPG, headless:
#   blender --background --python rpg_build_all.py -- [--no-body] [--render <out_dir> [frames...]] [--no-export]
# Builds the body rig with fingers (Firearms/firearm_rig.py), the launcher (rpg_model.py: writes RPG.glb and RPG.points.json), the reload
# (rpg_reload.py through Firearms/firearm_pose.py), exports CityX/Assets/CityX/Character/Resources/RPG/RPGReload.json and renders check frames.
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
EXPORT_PATH = os.path.join(REPO, "CityX", "Assets", "CityX", "Character", "Resources", "RPG", "RPGReload.json")


def run(path):
    exec(compile(open(path).read(), path, "exec"), globals())


run(os.path.join(FIREARMS, "firearm_rig.py"))
clear_scene()
build()                                                   # the rig (+ body, or the capsule stand-in)
run(os.path.join(HERE, "rpg_model.py"))
build(export=EXPORT)                                      # the launcher, a free object at the origin
run(os.path.join(FIREARMS, "firearm_pose.py"))
run(os.path.join(HERE, "rpg_reload.py"))
count = bake(KEYS, SEGMENTS, EVENTS, "RPGReload")
print("baked %d frames" % count)
if EXPORT:
    run(os.path.join(FIREARMS, "firearm_export.py"))
    export(EXPORT_PATH, SEGMENT_NAMES)
if RENDER_DIR:
    run(os.path.join(FIREARMS, "firearm_render.py"))
    frames = [int(f) for f in args[args.index("--render") + 2:] if f.isdigit()] or [0, 16, 38, 62, 84, 100, 112, 140]
    render_frames(RENDER_DIR, frames, "rpg", views=("front", "shoulder"))
