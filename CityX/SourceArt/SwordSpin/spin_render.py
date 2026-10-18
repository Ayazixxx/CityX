# CHECK RENDERS of the baked spin: the body and the sword from the side (Smash's view: the body's right, facing screen-right), from behind
# and above (the game's camera) and from straight above. Needs firearm_render.py's _workbench and _camera loaded first (spin_build_all.py).
import os

import bpy
from mathutils import Vector

SEGMENT_START = {"ground": 0, "ground_hold": 80, "air": 128}

# Blender axes: the character faces -Y, its right is -X, up is +Z.
VIEWS = {
    "side": (Vector((-7.5, 0.0, 1.4)), Vector((0.0, 0.0, 1.1)), 40.0),
    "behind": (Vector((-1.2, 6.0, 3.4)), Vector((0.0, 0.0, 1.1)), 50.0),
    "top": (Vector((0.0, 0.01, 9.0)), Vector((0.0, 0.0, 0.8)), 45.0),
}


def render_spin(out_dir, wanted, views=("side", "behind", "top")):
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    _workbench(scene, size=(560, 560))
    written = []
    for segment, frames in wanted.items():
        for f in frames:
            scene.frame_set(SEGMENT_START[segment] + f)
            bpy.context.view_layer.update()
            for view in views:
                position, target, fov = VIEWS[view]
                _camera(scene, position, target, fov)
                scene.render.filepath = os.path.join(out_dir, "%s_%03d_%s.png" % (segment, f, view))
                bpy.ops.render.render(write_still=True)
                written.append(scene.render.filepath)
    print("rendered", len(written), "images to", out_dir)
    return written
