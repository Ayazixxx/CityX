# A RECORDED MIXAMO CLIP'S UPPER BODY, ON OUR OWN SKELETON. Loaded by a move's script with exec, like the rest of this folder (StairClips/stair_clips.py, WraithBite/wraith_bite.py):
#   recordings = {name: read(file) ...}     # first: each read starts Blender's scene afresh
#   load_rig()                              # then the game's skeleton and its pose solver
#   bake_clip(name, frames, markers)        # then one action a clip, which octaslash_export.py writes out
#
# The turns are copied as turns: the hips, chest, neck and head from their own bones; each arm by which way its two bones point, on our longer arms. Imported as a humanoid FBX instead,
# Unity's retargeting raised the arms from about 70 degrees below level to about 25. No legs. The hips' place comes from the recording, by `hip_motion`.
import os

import bpy
from mathutils import Quaternion, Vector

SOURCE_ART = os.path.dirname(os.path.abspath(globals().get("RECORDED_CLIP_FILE", __file__ if "__file__" in globals() else ".")))
OCTASLASH = os.path.join(SOURCE_ART, "Octaslash")

HIP_HEIGHT = 0.9

# Our hips move a little less than a taller body's.
BODY_SCALE = 0.85

FPS = 30


def run(path):
    exec(compile(open(path).read(), path, "exec"), globals())


def read(path):
    """What the recording's upper body does, frame by frame, in world axes (both skeletons face -Y with their left at +X)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    scene = bpy.context.scene
    arm = next(o for o in scene.objects if o.type == "ARMATURE")
    action = arm.animation_data.action
    first, last = int(action.frame_range[0]), int(action.frame_range[1])
    world = arm.matrix_world.to_quaternion()

    def pose_bone(name):
        return arm.pose.bones["mixamorig:" + name]

    def rest(name):
        return world @ arm.data.bones["mixamorig:" + name].matrix_local.to_quaternion()

    rests = {n: rest(n) for n in ("Hips", "Spine2", "Neck", "Head")}

    def turn(name):
        return (world @ pose_bone(name).matrix.to_quaternion()) @ rests[name].inverted()

    frames = []
    for frame in range(first, last + 1):
        scene.frame_set(frame)
        m = arm.matrix_world
        entry = {"hips": turn("Hips"), "chest": turn("Spine2"), "neck": turn("Neck"), "head": turn("Head"), "hip_place": m @ pose_bone("Hips").head}
        for side, name in (("l", "Left"), ("r", "Right")):
            shoulder = m @ pose_bone(name + "Arm").head
            elbow = m @ pose_bone(name + "ForeArm").head
            wrist = m @ pose_bone(name + "Hand").head
            entry["upper_" + side] = (elbow - shoulder).normalized()
            entry["fore_" + side] = (wrist - elbow).normalized()
        frames.append(entry)
    return frames


def load_rig():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    run(os.path.join(OCTASLASH, "octaslash_rig.py"))
    globals()["OCTASLASH_NO_BAKE"] = True
    run(os.path.join(OCTASLASH, "octaslash_pose.py"))


def arm_turns(D, side, upper, fore, last_out):
    """The arm's bones pointing where the recording's do: what solve() does for an arm, from directions instead of a hand target. The hand runs straight on from the forearm."""
    a, f, h, t = "arm_" + side, "forearm_" + side, "hand_" + side, "fingertip_" + side
    l1 = (HEAD[f] - HEAD[a]).length
    l2 = (HEAD[h] - HEAD[f]).length
    elbow = upper * l1
    wrist = elbow + fore * l2
    reach = unit(wrist)
    out = elbow - reach * elbow.dot(reach)
    # A nearly straight arm has no elbow to speak of: it keeps the way it last stuck out.
    out = unit(out) if out.length > 0.02 else last_out
    bend = -(out - upper * out.dot(upper))
    Du = delta(unit(HEAD[f] - HEAD[a]), FWD, upper, bend)
    Df = upper.rotation_difference(fore) @ Du
    D[a], D[f], D[h], D[t] = Du, Df, Df, Df
    return out


def bake_clip(name, frames, markers, hip_motion, seam_frames=0):
    """Bakes the frames onto the rig as one action. `markers` name its segments for the exporter; `hip_motion(entry)` is where the hips go; `seam_frames` brings the ends together for a loop."""
    poses = []
    # Arms hang a little out to their own sides and back, until the recording says otherwise.
    last_out = {"l": unit(LEFT + BACK), "r": unit(RIGHT + BACK)}
    for entry in frames:
        D = {n: Quaternion() for n in ORDER}
        D["hips"] = entry["hips"]
        D["spine"] = entry["chest"]
        D["neck"] = entry["neck"]
        D["head"] = entry["head"]
        for side in ("l", "r"):
            last_out[side] = arm_turns(D, side, entry["upper_" + side], entry["fore_" + side], last_out[side])
        poses.append((locals_of(D), hip_motion(entry)))
    count = len(poses)
    for i in range(seam_frames):
        at = count - 1 - i
        poses[at] = blend(poses[at], poses[0], 1.0 - i / float(seam_frames))

    if rig.animation_data and rig.animation_data.action:
        old = rig.animation_data.action
        rig.animation_data.action = None
        bpy.data.actions.remove(old)
    rig.animation_data_create()
    action = bpy.data.actions.new(name)
    rig.animation_data.action = action
    rig.location = (0, 0, 0)
    previous = {}
    for f, (L, hips) in enumerate(poses):
        apply_pose(L, hips)
        for n in ORDER:
            pb = rig.pose.bones[n]
            q = pb.rotation_quaternion.copy()
            if n in previous and previous[n].dot(q) < 0.0:
                q = -q
            pb.rotation_quaternion = q
            previous[n] = q
            pb.keyframe_insert("rotation_quaternion", frame=f, group=n)
            if PARENT[n] is None:
                pb.keyframe_insert("location", frame=f, group=n)
    for fc in action.fcurves:
        for kp in fc.keyframe_points:
            kp.interpolation = 'LINEAR'
    scene = bpy.context.scene
    scene.timeline_markers.clear()
    for marker, frame in markers:
        scene.timeline_markers.new(marker, frame=frame)
    scene.timeline_markers.new("end", frame=count - 1)
    scene.render.fps = FPS
    scene.frame_start = 0
    scene.frame_end = count - 1
    scene.frame_set(0)
