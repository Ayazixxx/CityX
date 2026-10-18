# CHECK RENDERS of the baked stab: the body and the sword from the side (Smash's view: facing screen-right), from behind and above (the
# game's camera) and from the front. Needs firearm_render.py's _workbench and _camera loaded first (stab_build_all.py).
import os

import bpy
from mathutils import Vector

# Blender axes: the character faces -Y, its right is -X, up is +Z.
VIEWS = {
    "side": (Vector((-7.5, -1.0, 1.2)), Vector((0.0, -1.0, 1.0)), 42.0),
    "behind": (Vector((-1.2, 6.0, 3.4)), Vector((0.0, -1.0, 1.1)), 50.0),
    "front": (Vector((2.5, -7.0, 1.6)), Vector((0.0, 0.0, 1.0)), 45.0),
    "frontleft": (Vector((4.5, -5.0, 1.4)), Vector((0.0, -0.8, 0.9)), 40.0),
}


def render_stab(out_dir, frames, views=("side", "behind", "front", "frontleft")):
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    _workbench(scene, size=(640, 480))
    written = []
    for f in frames:
        scene.frame_set(f)
        bpy.context.view_layer.update()
        for view in views:
            position, target, fov = VIEWS[view]
            _camera(scene, position, target, fov)
            scene.render.filepath = os.path.join(out_dir, "stab_%03d_%s.png" % (f, view))
            bpy.ops.render.render(write_still=True)
            written.append(scene.render.filepath)
    print("rendered", len(written), "images to", out_dir)
    return written
