# CHECK RENDERS of the Glock (and, when the rig is in the scene, of the reload): Workbench renders from a few sides, written to the folder given
# after "--" (default: next to this script). Headless:  blender --background --python glock_render.py -- <out_dir> [model|reload]
import bpy
import math
import os
import sys
from mathutils import Vector

HERE = (os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals()
        else os.path.join(os.environ.get("CITYX_REPO", r"C:\Users\Lenovo\Downloads\the game"), "CityX", "SourceArt", "Glock"))
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = args[0] if args else HERE
WHAT = args[1] if len(args) > 1 else "model"
os.makedirs(OUT, exist_ok=True)


def clear_scene():
    """Blender's startup file has a cube, a light and a camera at the origin: a 2 m cube in front of every check camera."""
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)


def workbench(scene, size=(900, 700)):
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
    scene.render.film_transparent = False
    scene.world = scene.world or bpy.data.worlds.new("World")
    scene.world.color = (0.55, 0.58, 0.62)


def camera_at(scene, position, target, ortho_scale=None, fov_degrees=35.0):
    cam = bpy.data.objects.get("CheckCam")
    if cam is None:
        cam = bpy.data.objects.new("CheckCam", bpy.data.cameras.new("CheckCam"))
        scene.collection.objects.link(cam)
    cam.location = Vector(position)
    direction = Vector(target) - Vector(position)
    cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    if ortho_scale is not None:
        cam.data.type = 'ORTHO'
        cam.data.ortho_scale = ortho_scale
    else:
        cam.data.type = 'PERSP'
        cam.data.angle = math.radians(fov_degrees)
    cam.data.clip_start = 0.01
    scene.camera = cam
    return cam


def render(scene, name):
    scene.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("rendered", scene.render.filepath)


def render_model():
    scene = bpy.context.scene
    workbench(scene)
    root = bpy.data.objects["Glock"]
    centre = Vector((0.0, -0.17, -0.04))   # Blender axes: the gun points -Y; the middle of the slide and grip
    size = 0.5
    # Blender axes: the shooter's right is -X, the muzzle -Y, up +Z.
    camera_at(scene, centre + Vector((-2.0, 0.0, 0.0)), centre, ortho_scale=size)   # from the shooter's right
    render(scene, "glock_right")
    camera_at(scene, centre + Vector((2.0, 0.0, 0.0)), centre, ortho_scale=size)    # from the shooter's left
    render(scene, "glock_left")
    camera_at(scene, centre + Vector((0.0, 0.0, 2.0)), centre, ortho_scale=size)    # from above
    render(scene, "glock_top")
    camera_at(scene, centre + Vector((-1.2, -1.4, 0.9)), centre, fov_degrees=28.0)  # three-quarter from the front right
    render(scene, "glock_three_quarter")


def render_reload(frames, prefix="reload"):
    """With the rig in the scene: the body from the front-right and from the camera's own shoulder view at the given frames."""
    scene = bpy.context.scene
    workbench(scene, size=(800, 900))
    rig = bpy.data.objects["CityXRig"]
    for f in frames:
        scene.frame_set(f)
        bpy.context.view_layer.update()
        chest = rig.matrix_world @ rig.pose.bones["spine"].tail
        camera_at(scene, chest + Vector((-2.4, -3.0, 0.4)), chest + Vector((0.0, 0.0, -0.2)), fov_degrees=40.0)
        render(scene, "%s_%03d_front" % (prefix, f))
        camera_at(scene, chest + Vector((-1.0, 2.6, 1.0)), chest + Vector((0.0, -0.4, -0.3)), fov_degrees=50.0)
        render(scene, "%s_%03d_behind" % (prefix, f))


if WHAT == "model":
    clear_scene()
    GLOCK_STANDALONE = False
    exec(compile(open(os.path.join(HERE, "glock_model.py")).read(), "glock_model.py", "exec"))
    build(export=False)
    render_model()
