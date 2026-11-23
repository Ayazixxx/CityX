# EVERYTHING FOR THE CHAIN'S CLIPS, headless:
#   blender --background --factory-startup --python chain_build.py -- [--sheets <out_dir>] [--export <json path>] [--only <move>,<move>]
# Builds the body rig, loads Octaslash's pose solver, the planted feet and the punches' keys, bakes the chain's blows (chain_pose.py), writes a sheet of each blow's moments
# from the side, from the front and from behind (where the game's camera is), and the JSON BakedMotion reads (the game's copy is Character/Resources/Chain/ChainMotion.json).
import os
import sys

import bpy
import numpy
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
run(os.path.join(SOURCE_ART, "FistCombo", "fist_pose.py"))
run(os.path.join(HERE, "chain_pose.py"))
SPANS = bake_chain()
# The rig comes with a sword in its hand; the chain is the game's to hang there.
bpy.data.objects[SWORD].hide_render = True

if "--export" in args:
    EXPORT_PATH = args[args.index("--export") + 1]
    EXPORT_SEGMENTS = ORDER_OF_CHAIN_MOVES
    run(os.path.join(OCTASLASH, "octaslash_export.py"))

if "--sheets" in args:
    run(os.path.join(FIREARMS, "firearm_render.py"))
    out_dir = args[args.index("--sheets") + 1]
    only = args[args.index("--only") + 1].split(",") if "--only" in args else None
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    CELL = 300
    _workbench(scene, size=(CELL, CELL))
    floor = bpy.data.meshes.new("Floor")
    floor.from_pydata([(-12, -12, 0), (12, -12, 0), (12, 12, 0), (-12, 12, 0)], [], [(0, 1, 2, 3)])
    scene.collection.objects.link(bpy.data.objects.new("Floor", floor))
    # Blender axes: the character faces -Y, its right is -X, up is +Z. Each view looks at the body where the step has carried it.
    VIEWS = (
        ("side", Vector((-7.0, -0.3, 1.5)), Vector((0.0, -0.3, 1.35)), 34.0),
        ("front", Vector((2.6, -6.6, 1.7)), Vector((0.0, 0.0, 1.35)), 34.0),
        ("behind", Vector((-0.87, 6.2, 2.9)), Vector((0.0, -0.8, 1.4)), 36.0),
    )
    MOMENTS = 12
    shot = os.path.join(out_dir, "_cell.png")
    for name, (first, last, move) in SPANS.items():
        if only is not None and name not in only:
            continue
        # Twelve moments: the keys' own and the strike between them, then evenly through what is left.
        strike = move["contact"] - move["held"]
        marks = [0.0, move["wound"] * 0.5, move["wound"], move["held"], move["held"] + strike * 0.35, move["held"] + strike * 0.7, move["contact"],
                 (move["contact"] + move["followed"]) * 0.5, move["followed"], move["settled"],
                 move["settled"] + (move["seconds"] - move["settled"]) * 0.55, move["seconds"]]
        sheet = numpy.zeros((CELL * len(VIEWS), CELL * MOMENTS, 4), dtype=numpy.float32)
        for column, seconds in enumerate(marks[:MOMENTS]):
            scene.frame_set(first + min(last - first, int(round(seconds * FPS))))
            bpy.context.view_layer.update()
            at = bpy.data.objects[RIG].location.copy()
            for row, (view, position, target, fov) in enumerate(VIEWS):
                _camera(scene, position + at, target + at, fov)
                scene.render.filepath = shot
                bpy.ops.render.render(write_still=True)
                image = bpy.data.images.load(shot)
                pixels = numpy.array(image.pixels[:], dtype=numpy.float32).reshape(CELL, CELL, 4)
                bpy.data.images.remove(image)
                top = (len(VIEWS) - 1 - row) * CELL
                sheet[top:top + CELL, column * CELL:(column + 1) * CELL] = pixels
        out = bpy.data.images.new(name, CELL * MOMENTS, CELL * len(VIEWS), alpha=True)
        out.pixels = sheet.ravel()
        out.filepath_raw = os.path.join(out_dir, name + ".png")
        out.file_format = 'PNG'
        out.save()
        print("sheet", out.filepath_raw, "moments (s):", " ".join("%.2f" % s for s in marks[:MOMENTS]))
    if os.path.exists(shot):
        os.remove(shot)
