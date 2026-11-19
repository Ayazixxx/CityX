# A PAIR OF GLOCKS' RELOAD, headless (Dual Pistols: docs/DUAL_PISTOLS.md):
#   blender --background --python glock_pair_build_all.py -- [--no-body] [--render <out_dir> [frames...]] [--no-export]
# Builds the body rig (firearm_rig.py), the Glock (glock_model.py, not exported again: Glock.glb is glock_build_all.py's) and its copy for the
# left hand (every object of it named with an "L"), bakes glock_pair_reload.py's keys through firearm_pose.py, exports
# CityX/Assets/CityX/Character/Resources/Glock/GlockPairReload.json (firearm_export.py: the left gun's parts as SlideL, MagazineL, TriggerL)
# and renders check frames.
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
EXPORT_PATH = os.path.join(REPO, "CityX", "Assets", "CityX", "Character", "Resources", "Glock", "GlockPairReload.json")


def run(path):
    exec(compile(open(path).read(), path, "exec"), globals())


def copy_for_left_hand(source, parent=None, suffix="L"):
    """The gun's object and everything under it again, each named with `suffix` (the meshes shared)."""
    dup = source.copy()
    dup.name = source.name + suffix
    bpy.context.scene.collection.objects.link(dup)
    dup.parent = parent
    if parent is not None:
        dup.matrix_parent_inverse = source.matrix_parent_inverse.copy()
    for child in source.children:
        copy_for_left_hand(child, dup, suffix)
    return dup


run(os.path.join(FIREARMS, "firearm_rig.py"))
clear_scene()
build()                                                   # the rig (+ body)
GLOCK_STANDALONE = False
run(os.path.join(HERE, "glock_model.py"))
build(export=False)                                       # the gun, as a free object at the origin
copy_for_left_hand(bpy.data.objects[GUN_NAME])
run(os.path.join(FIREARMS, "firearm_pose.py"))
run(os.path.join(HERE, "glock_pair_reload.py"))
count = bake(KEYS, SEGMENTS, EVENTS, "GlockPairReload")
print("baked %d frames" % count)
if EXPORT:
    run(os.path.join(FIREARMS, "firearm_export.py"))
    export(EXPORT_PATH)
if RENDER_DIR:
    run(os.path.join(FIREARMS, "firearm_render.py"))
    frames = [f for f in args[args.index("--render") + 2:] if f.isdigit()]
    frames = [int(f) for f in frames] or [0, 10, 14, 36, 44, 50, 56, 90, 120 + 66, 120 + 74, 120 + 76]
    render_frames(RENDER_DIR, frames, "pair", views=("front", "shoulder"))
