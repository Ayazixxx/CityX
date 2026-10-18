# EVERYTHING FOR THE SWORD SPIN, headless:
#   blender --background --python spin_build_all.py -- [--no-export] [--render <out_dir> [--frames ground:0,4,7 air:8,48]]
# Builds the body rig (../Octaslash/octaslash_rig.py), loads Octaslash's pose solver, bakes the spin's poses (spin_pose.py), exports
# CityX/Assets/CityX/Character/Resources/SwordSpin/SwordSpinMotion.json (../Octaslash/octaslash_export.py) and renders check frames.
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
OCTASLASH_NO_BAKE = True
run(os.path.join(OCTASLASH, "octaslash_pose.py"))
run(os.path.join(HERE, "spin_pose.py"))
print("baked %d frames" % bake_spin())

if "--no-export" not in args:
    EXPORT_PATH = os.path.join(REPO, "CityX", "Assets", "CityX", "Character", "Resources", "SwordSpin", "SwordSpinMotion.json")
    EXPORT_SEGMENTS = ("ground", "ground_hold", "air")
    run(os.path.join(OCTASLASH, "octaslash_export.py"))

if "--render" in args:
    run(os.path.join(FIREARMS, "firearm_render.py"))
    run(os.path.join(HERE, "spin_render.py"))
    out_dir = args[args.index("--render") + 1]
    wanted = {"ground": [0, 2, 4, 6, 7, 9, 11, 13, 16, 20, 26, 33, 39, 44, 49, 60, 70, 79],
              "air": [0, 4, 6, 8, 10, 12, 16, 22, 30, 38, 45, 47, 49, 52, 60, 70, 80, 91]}
    if "--frames" in args:
        wanted = {}
        for spec in args[args.index("--frames") + 1:]:
            if ":" not in spec:
                break
            segment, numbers = spec.split(":", 1)
            wanted[segment] = [int(n) for n in numbers.split(",") if n]
    render_spin(out_dir, wanted)
