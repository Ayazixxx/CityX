# EVERYTHING FOR THE STAB DASH, headless:
#   blender --background --python stab_build_all.py -- [--no-export] [--render <out_dir> [--frames 0,4,9,12,20] [--views side,frontleft]]
# Builds the body rig (../Octaslash/octaslash_rig.py), puts its sword in the thrust grip (stab_grip.py), loads Octaslash's pose solver,
# bakes the stab's poses (stab_pose.py), exports
# CityX/Assets/CityX/Character/Resources/StabDash/StabDashMotion.json (../Octaslash/octaslash_export.py) and renders check frames.
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
OCTASLASH = os.path.join(os.path.dirname(HERE), "Octaslash")
FIREARMS = os.path.join(os.path.dirname(HERE), "Firearms")
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def run(path):
    exec(compile(open(path).read(), path, "exec"), globals())


bpy.ops.wm.read_factory_settings(use_empty=True)
run(os.path.join(OCTASLASH, "octaslash_rig.py"))
run(os.path.join(HERE, "stab_grip.py"))          # the sword in the thrust grip the game holds it in during the move
SOLVER_TWIST_WEIGHT = 1.0                        # and the hand not wrung round its forearm to lay the blade
OCTASLASH_NO_BAKE = True
run(os.path.join(OCTASLASH, "octaslash_pose.py"))
run(os.path.join(HERE, "stab_pose.py"))
print("baked %d frames" % bake_stab())

if "--no-export" not in args:
    EXPORT_PATH = os.path.join(REPO, "CityX", "Assets", "CityX", "Character", "Resources", "StabDash", "StabDashMotion.json")
    EXPORT_SEGMENTS = ("stab",)
    run(os.path.join(OCTASLASH, "octaslash_export.py"))

if "--render" in args:
    run(os.path.join(FIREARMS, "firearm_render.py"))
    run(os.path.join(HERE, "stab_render.py"))
    out_dir = args[args.index("--render") + 1]
    wanted = [0, 4, 9, 11, 12, 14, 20, 31, 35, 41, 46, 51, 55, 59]
    if "--frames" in args:
        wanted = [int(n) for n in args[args.index("--frames") + 1].split(",") if n]
    views = ("side", "behind", "front", "frontleft")
    if "--views" in args:
        views = tuple(args[args.index("--views") + 1].split(","))
    render_stab(out_dir, wanted, views)
