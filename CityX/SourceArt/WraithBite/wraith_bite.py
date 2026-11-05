# THE WRAITH'S NECK BITE (owner, 2026-11-05: "sink his teeth into your neck using the wraith neck bite.fbx animation"):
#   blender --background --factory-startup --python wraith_bite.py -- "<Wraith Neck Bite.fbx>" [--render <out_dir>]
#
# The recording's upper body on our skeleton (../recorded_clip.py), as WraithBiteMotion.json for BakedMotion: the Wraith has no Animator, it sets its bones itself. Its body stays the
# recording's in how far it lunges forward and comes back, a little less far for our hips; it hovers, so its height is its own.
#
# The recording, frame by frame: a stance; a lunge forward with both hands coming up to about shoulder height, there by frame 27; the head down biting, the body working forward a
# little, to frame 84; thrown back to 107; back to the stance. Its hands close 0.8 m ahead of where it starts.
import os
import sys

from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
args = sys.argv[sys.argv.index("--") + 1:]
RECORDED_CLIP_FILE = os.path.join(os.path.dirname(HERE), "recorded_clip.py")
exec(compile(open(RECORDED_CLIP_FILE).read(), RECORDED_CLIP_FILE, "exec"), globals())

# (The recording's frames, counted from 0.)
SEGMENTS = [("stance", 0), ("lunge", 12), ("bite", 27), ("thrown", 84), ("recover", 107)]

recording = read(args[0])
load_rig()

start = recording[0]["hip_place"]
bake_clip("WraithBite", recording, SEGMENTS, lambda entry: Vector(((entry["hip_place"].x - start.x) * BODY_SCALE, (entry["hip_place"].y - start.y) * BODY_SCALE, HIP_HEIGHT)))
EXPORT_PATH = os.path.join(HERE, "WraithBiteMotion.json")
EXPORT_SEGMENTS = tuple(name for name, _ in SEGMENTS)
run(os.path.join(OCTASLASH, "octaslash_export.py"))

if "--render" in args:
    run(os.path.join(os.path.dirname(HERE), "Firearms", "firearm_render.py"))
    out_dir = args[args.index("--render") + 1]
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    _workbench(scene, size=(420, 420))
    for f in (0, 12, 20, 27, 45, 70, 84, 95, 107, 125):
        scene.frame_set(f)
        bpy.context.view_layer.update()
        hips = bpy.data.objects[RIG].pose.bones["hips"].head
        _camera(scene, Vector((-5.5, hips.y - 0.4, 1.6)), Vector((0.0, hips.y - 0.4, 1.3)), 38.0)
        scene.render.filepath = os.path.join(out_dir, "bite_%03d.png" % f)
        bpy.ops.render.render(write_still=True)
