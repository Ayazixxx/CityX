# CHECK RENDERS of a baked reload: the body with the gun, from the front-right and from over the shoulder, at the frames asked for.
# exec'd after the bake; call render_frames(out_dir, frames, prefix).
import bpy
import os
from mathutils import Vector


def _workbench(scene, size=(720, 900)):
    try:
        scene.render.engine = 'BLENDER_WORKBENCH'
    except TypeError as e:
        print("engine", e)
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'MATERIAL'
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.world = scene.world or bpy.data.worlds.new("World")
    scene.world.color = (0.55, 0.58, 0.62)


def _camera(scene, position, target, fov_degrees):
    cam = bpy.data.objects.get("CheckCam")
    if cam is None:
        cam = bpy.data.objects.new("CheckCam", bpy.data.cameras.new("CheckCam"))
        scene.collection.objects.link(cam)
    cam.location = Vector(position)
    cam.rotation_euler = (Vector(target) - Vector(position)).to_track_quat('-Z', 'Y').to_euler()
    cam.data.type = 'PERSP'
    cam.data.angle = __import__("math").radians(fov_degrees)
    cam.data.clip_start = 0.01
    scene.camera = cam


def render_frames(out_dir, frames, prefix="reload", views=("front", "close", "right", "left")):
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    _workbench(scene)
    rig_ob = bpy.data.objects["CityXRig"]
    gun = bpy.data.objects[GUN_NAME]
    written = []
    for f in frames:
        scene.frame_set(f)
        bpy.context.view_layer.update()
        chest = rig_ob.matrix_world @ rig_ob.pose.bones["spine"].tail
        gun_at = gun.matrix_world.to_translation()
        # Blender axes: the character faces -Y; its right is -X.
        shots = {
            "front": (chest + Vector((-1.6, -3.2, 0.2)), chest + Vector((0.0, 0.0, -0.35)), 42.0),
            "shoulder": (chest + Vector((-0.9, 2.2, 0.9)), gun_at, 55.0),
            "close": (gun_at + Vector((-1.3, -1.1, 0.5)), gun_at, 30.0),
            "right": (gun_at + Vector((-1.5, 0.1, 0.25)), gun_at, 28.0),
            "left": (gun_at + Vector((1.4, -0.5, 0.1)), gun_at, 30.0),
        }
        for view in views:
            position, target, fov = shots[view]
            _camera(scene, position, target, fov)
            scene.render.filepath = os.path.join(out_dir, "%s_%03d_%s.png" % (prefix, f, view))
            bpy.ops.render.render(write_still=True)
            written.append(scene.render.filepath)
    print("rendered", len(written), "images to", out_dir)
    return written
