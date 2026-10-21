# A BODY MOVE, exported for Unity: every frame of the CityXRig's action (octaslash_pose.py bakes Octaslash's, ../SwordSpin/spin_pose.py the
# sword spin's; a hand edit in Blender is exported the same way) written as each bone's LOCAL rotation in the game's own live skeleton, plus
# where the hips are.
#
# Run inside Blender after the bake:   exec(open(r"<this file>").read())
# Writes CityX/Assets/CityX/Character/Resources/Octaslash/OctaslashMotion.json, which BakedMotion reads. Another move sets EXPORT_PATH and
# EXPORT_SEGMENTS (its segment markers, in timeline order) before running it.
#
# THE TRIP BACK TO UNITY'S BONES.
#  * Axes: Blender (x, y, z) is Unity (-x, z, -y) (the character faces -Y in Blender, +Z in Unity, and its left is +X in Blender, -X in Unity). A rotation
#    R in Blender axes is M R M^T in Unity's, M the matrix of that swap. Same physical turn, written in the other coordinates.
#  * Rest: the Blender rig rests arms-down, the way the game's body is modelled and bound (BodyRig.RestBones); the game's live skeleton rests in a T-pose
#    with every bone's rotation the identity (BodyRigBuilder.BuildSkeleton). So a bone's turn in the game's terms is its turn from the arms-down rest
#    followed by the rest itself: for the arm chain the shoulder's hang, Euler(0, 0, +-BodyRig.ArmDownDegrees), exactly BodyRig.RestBindMatrix's rotation.
#  * Each bone's world turn W then becomes the local rotation the game sets: W(parent)^-1 W(bone).

import bpy
import json
import math
import os
from mathutils import Matrix, Quaternion, Vector

RIG = "CityXRig"
REPO = globals().get("REPO") or (
    os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..")) if "__file__" in globals()
    else os.environ.get("CITYX_REPO", r"C:\Users\Lenovo\Downloads\the game"))
OUT = globals().get("EXPORT_PATH") or globals().get("OCTASLASH_EXPORT_PATH") or os.path.join(
    REPO, "CityX", "Assets", "CityX", "Character", "Resources", "Octaslash", "OctaslashMotion.json")
SEGMENTS = tuple(globals().get("EXPORT_SEGMENTS") or ("coil_in", "hold", "dash", "recover"))

M = Matrix(((-1, 0, 0), (0, 0, 1), (0, -1, 0)))     # Blender -> Unity
ARM_DOWN = math.degrees(math.atan2(1.9 - 0.88, 0.58 - 0.35))
HANG = {}
for n in ("arm_l", "forearm_l", "hand_l", "fingertip_l"):
    HANG[n] = Quaternion((0, 0, 1), math.radians(ARM_DOWN))
for n in ("arm_r", "forearm_r", "hand_r", "fingertip_r"):
    HANG[n] = Quaternion((0, 0, 1), math.radians(-ARM_DOWN))


def export():
    scene = bpy.context.scene
    rig = bpy.data.objects[RIG]
    names = [b.name for b in rig.data.bones]
    parent = {b.name: (b.parent.name if b.parent else None) for b in rig.data.bones}
    rest = {b.name: b.matrix_local.to_quaternion() for b in rig.data.bones}
    markers = {m.name: m.frame for m in scene.timeline_markers}
    starts = [markers[s] for s in SEGMENTS]
    first, last = starts[0], markers["end"]
    segments = []
    for i, name in enumerate(SEGMENTS):
        end = starts[i + 1] if i + 1 < len(SEGMENTS) else last
        segments.append({"name": name, "start": starts[i] - first, "end": end - first})
    cuts = sorted(m.frame - first for m in scene.timeline_markers if m.name.startswith("cut"))

    hips, rotations = [], []
    previous = {}
    for f in range(first, last + 1):
        scene.frame_set(f)
        world = {}
        for n in names:
            pb = rig.pose.bones[n]
            turn = pb.matrix.to_quaternion() @ rest[n].inverted()                 # from the arms-down rest, Blender axes
            unity = (M @ turn.to_matrix() @ M.transposed()).to_quaternion()      # the same turn in Unity's axes
            world[n] = unity @ HANG.get(n, Quaternion())                          # from the T-pose
        for n in names:
            q = world[n] if parent[n] is None else world[parent[n]].inverted() @ world[n]
            q.normalize()
            if n in previous and previous[n].dot(q) < 0.0:
                q = -q                                                            # the short way round from the last frame
            previous[n] = q
            rotations.extend(round(c, 5) for c in (q.x, q.y, q.z, q.w))
        p = M @ rig.pose.bones["hips"].head                                      # armature space: the rig's own flight is not part of it
        hips.extend(round(c, 5) for c in (p.x, p.y, p.z))

    data = {
        "fps": scene.render.fps,
        "frames": last - first + 1,
        "bones": names,
        "segments": segments,
        "cutFrames": cuts,
        "hips": hips,
        "rotations": rotations,
    }
    path = os.path.normpath(OUT)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w") as fh:
        json.dump(data, fh, separators=(",", ":"))
    scene.frame_set(first)
    return path, data


_path, _data = export()
print("wrote %s: %d frames, %d bones, %d bytes" % (_path, _data["frames"], len(_data["bones"]), os.path.getsize(_path)))
print("segments", _data["segments"], "cuts", _data["cutFrames"])
