# THE RECORDED STAIR CLIPS, ON OUR OWN SKELETON (docs/STAIRS.md):
#   blender --background --factory-startup --python stair_clips.py -- <folder with the four Mixamo files>
#
# Reads the upper body of the owner's four Mixamo stair clips (../recorded_clip.py) and writes it beside this file, one JSON per clip. StairClipBaker (Unity) turns each into a humanoid
# clip. No legs: the ankles on a stair are StairSteps', and below the floor line the down clips' legs are ruined anyway. The hips stay at the body's height and keep only their sway
# from side to side: the climb and its bob are the game's.
import os
import sys

from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
FOLDER = sys.argv[sys.argv.index("--") + 1]
RECORDED_CLIP_FILE = os.path.join(os.path.dirname(HERE), "recorded_clip.py")
exec(compile(open(RECORDED_CLIP_FILE).read(), RECORDED_CLIP_FILE, "exec"), globals())

CLIPS = {
    "stairs_up_walk": "Ascending Stairs.fbx",
    "stairs_up_run": "Running Up Stairs.fbx",
    "stairs_down_walk": "Descending Stairs.fbx",
    "stairs_down_run": "Descending StairsRUN.fbx",
}

# The clips' ends are brought together over this many frames, so a loop has no seam in the upper body.
SEAM_FRAMES = 4

recordings = {name: read(os.path.join(FOLDER, file)) for name, file in CLIPS.items()}
load_rig()

for clip_name, clip_frames in recordings.items():
    middle = sum(entry["hip_place"].x for entry in clip_frames) / len(clip_frames)
    bake_clip(clip_name, clip_frames, [("clip", 0)], lambda entry: Vector(((entry["hip_place"].x - middle) * BODY_SCALE, 0.0, HIP_HEIGHT)), SEAM_FRAMES)
    EXPORT_PATH = os.path.join(HERE, clip_name + ".json")
    EXPORT_SEGMENTS = ("clip",)
    run(os.path.join(OCTASLASH, "octaslash_export.py"))
