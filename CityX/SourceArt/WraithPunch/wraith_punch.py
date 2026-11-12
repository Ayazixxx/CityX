# THE WRAITH'S PUNCH (owner, 2026-11-11: "give the wraith a second attack where he punches the opponent"):
#   blender --background --factory-startup --python wraith_punch.py -- "<Wraith Punch.fbx>" [--render <out_dir>]
#
# The recording's upper body on our skeleton (../recorded_clip.py), as WraithPunchMotion.json for BakedMotion, as the bite is (../WraithBite): the Wraith has no Animator, it sets
# its bones itself. Its body keeps the recording's coming forward and going back, a little less far for our hips; it hovers, so its height is its own.
#
# The recording, frame by frame (30 a second, counted from 0): a stance to frame 9; the right fist drawn back and down, the body leaning back, to 30; the fist swung up and over
# from behind the head and driven down and forward, at full reach on frame 39 as the body comes 0.4 m forward; down low past it to 45; a second, smaller push of the fist out
# to 57; back to the stance by 70, which it holds to the end.
import os
import sys

from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
args = sys.argv[sys.argv.index("--") + 1:]
RECORDED_CLIP_FILE = os.path.join(os.path.dirname(HERE), "recorded_clip.py")
exec(compile(open(RECORDED_CLIP_FILE).read(), RECORDED_CLIP_FILE, "exec"), globals())

# (The recording's frames, counted from 0.) "swing": drawn back, then over and down, ending on the blow (the Wraith's WindingUp); "follow": through and back to
# the stance (Punching). The game plays the two; the stances either side are the recording's own.
SEGMENTS = [("stance", 0), ("swing", 9), ("follow", 39), ("end_stance", 70)]

recording = read(args[0])
load_rig()

start = recording[0]["hip_place"]
bake_clip("WraithPunch", recording, SEGMENTS, lambda entry: Vector(((entry["hip_place"].x - start.x) * BODY_SCALE, (entry["hip_place"].y - start.y) * BODY_SCALE, HIP_HEIGHT)))
EXPORT_PATH = os.path.join(HERE, "WraithPunchMotion.json")
EXPORT_SEGMENTS = tuple(name for name, _ in SEGMENTS)
run(os.path.join(OCTASLASH, "octaslash_export.py"))

if "--render" in args:
    run(os.path.join(os.path.dirname(HERE), "Firearms", "firearm_render.py"))
    out_dir = args[args.index("--render") + 1]
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    _workbench(scene, size=(360, 360))
    for f in range(0, 72, 3):
        scene.frame_set(f)
        bpy.context.view_layer.update()
        hips = bpy.data.objects[RIG].pose.bones["hips"].head
        _camera(scene, Vector((-5.5, hips.y - 0.4, 1.6)), Vector((0.0, hips.y - 0.4, 1.3)), 38.0)
        scene.render.filepath = os.path.join(out_dir, "punch_%03d.png" % f)
        bpy.ops.render.render(write_still=True)
