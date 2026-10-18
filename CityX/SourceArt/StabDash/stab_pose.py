# THE STAB DASH (Sword tree), posed: the Mii Swordfighter's grounded Power Thrust (Smash Ultimate) on CityX's own skeleton.
#
# Needs the rig (../Octaslash/octaslash_rig.py) and octaslash_pose.py's solver loaded first, with OCTASLASH_NO_BAKE set; stab_build_all.py
# does both, bakes, exports and renders check frames.
#
# THE MOVE, read frame by frame from SmashWiki's hitbox GIF (one GIF frame per game frame: GIF frame g is the game's frame g + 1, and the
# clip's frame f is the game's frame f, so the hits land on StabDashMove's frames; UltimateFrameData: hitboxes 12-30, interruptible at 59):
#   0       the sword stance
#   1-4     sinking into a wide, deep squat, side-on to where it will go, the sword hand drawn back past the right shoulder with the blade
#           pointing ahead and down beside the body, the free arm reaching ahead at chest height (GIF frames 0-3)
#   4-9     the squat deepening, the weight going back (GIF frames 3-8)
#   9-12    the release: the body unwinding to face ahead, the sword hand coming forward past the shoulder (frame 10, solved halfway) and
#           through at the hip with the blade level (frame 11), then the arm thrust out straight, the back leg driving (frame 12, the first and
#           strongest hit)
#   12-31   the lunge, held while the body slides (the game moves it): very low, the torso pitched far forward, the sword arm and blade dead
#           level ahead, the back leg stretched out behind, the free arm raised as a fencer's rear arm: the upper arm out to the side and back,
#           the forearm up (GIF frames 11-30; the GIF's fist cocked up behind the head bent the elbow backwards on this forward-pitched body)
#   31-41   the slide dying, the head lifting a little (GIF frames 30-33); held
#   41-59   recovering: the back leg drawn in, the body rising, the blade brought back up into the stance (not in the GIF, which ends at 34)
#
# Poses are written in the character's own frame (Unity axes: x right, y up, z forward; the stab goes along +z). The game moves the body along
# the stab and turns it to face it; the clip only lunges and slides its hips a little within the body.

import bpy
import math
from mathutils import Vector, Quaternion, Matrix

STAB = (0, 59)

ANKLE = 0.135


def squat_legs(hips_y, front=0.45, back=0.42):
    """The wind-up's stance: wide and deep, side-on, the left foot ahead and the right behind, both flat, the back knee out to the right."""
    drop = ANKLE - hips_y
    return {
        "l_leg": {"foot": (-0.05, drop, front), "knee": (-0.45, 0.3, 1.0), "toe": (-0.15, 0.0, 1.0), "space": "root"},
        "r_leg": {"foot": (0.10, drop, -back), "knee": (1.0, 0.3, -0.25), "toe": (1.0, 0.0, 0.25), "space": "root"},
    }


def lunge_legs(hips_y, front=0.55, back=(0.14, -0.28, -0.70)):
    """The thrust's lunge: the left foot far ahead and flat under a deeply bent knee, the right leg stretched out behind on the ball of the
    foot, its knee to the ground."""
    drop = ANKLE - hips_y
    return {
        "l_leg": {"foot": (-0.12, drop, front), "knee": (-0.1, 0.25, 1.0), "toe": (0.0, 0.0, 1.0), "space": "root"},
        "r_leg": {"foot": back, "knee": (0.05, -1.0, 0.3), "toe": (0.0, -0.85, 0.5), "space": "root"},
    }


def stance():
    """Frame 0, and again on frame 59: the two-handed sword stance the game's idle holds (Octaslash's settle), so taking hold and handing back
    are short."""
    return pose_settle()


def drawn():
    """Frame 4: sunk into the squat and turned side-on, the sword hand drawn back past the right shoulder, the blade pointing ahead and down
    out beside the body; the free arm reaching ahead (GIF frame 3)."""
    hy = 0.58
    P = {"hips": (0.0, hy, -0.08), "body": rot(p=4, y=-28), "spine": rot(p=2, y=-8), "neck": rot(p=-4, y=18), "head": rot(p=-4, y=16),
         "sword": {"hand": (0.20, -0.25, -0.12), "blade": (0.10, -0.18, 0.98), "space": "root"},
         "free": {"hand": (0.08, -0.30, 0.66), "pole": (-0.7, -0.7, 0.0), "space": "root"}}
    P.update(squat_legs(hy, front=0.44, back=0.40))
    return P


def cocked():
    """Frames 6-9: the squat at its deepest, the weight back, the sword hand far back at shoulder height and the blade along the body's side,
    ahead and down; the free fist out ahead at chest height, aiming (GIF frames 5-8)."""
    hy = 0.47
    P = {"hips": (0.0, hy, -0.16), "body": rot(p=2, y=-36), "spine": rot(p=2, y=-8), "neck": rot(p=-4, y=24), "head": rot(p=-4, y=20),
         "sword": {"hand": (0.20, -0.20, -0.30), "blade": (0.10, -0.22, 0.97), "space": "root"},
         "free": {"hand": (0.05, -0.20, 0.72), "pole": (-0.7, -0.7, 0.0), "space": "root"}}
    P.update(squat_legs(hy, front=0.50, back=0.46))
    return P


def launch():
    """Frame 11: unwinding to face ahead and moving off, the sword hand coming through ahead of the hip with the blade level, the free arm
    swinging back past the left hip, the back leg pushing (GIF frame 10)."""
    hy = 0.52
    P = {"hips": (0.0, hy, -0.10), "body": rot(p=20, y=-10), "spine": rot(p=6), "neck": rot(p=-16, y=6), "head": rot(p=-12, y=4),
         "sword": {"hand": (0.04, -0.30, 0.52), "blade": (0.0, -0.03, 1.0), "space": "root"},
         "free": {"hand": (-0.18, -0.40, -0.40), "pole": (-0.9, -0.3, 0.1), "space": "root"}}
    P.update(lunge_legs(hy, front=0.48, back=(0.14, -0.34, -0.58)))
    return P


def thrust():
    """Frame 12, the first and strongest hit: the sword arm thrust out straight ahead at shoulder height, the blade on its line, the right
    shoulder driven forward; the free arm flung back; the back leg straightening behind (GIF frame 11)."""
    hy = 0.46
    P = {"hips": (0.0, hy, -0.12), "body": rot(p=42, y=10), "spine": rot(p=6, y=6), "neck": rot(p=-28, y=-8), "head": rot(p=-22, y=-6),
         "sword": {"hand": (-0.08, -0.26, 0.84), "blade": (0.0, 0.0, 1.0), "space": "root"},
         "free": {"hand": (-0.40, 0.10, -0.50), "pole": (-0.5, -0.8, -0.3), "space": "root"}}
    P.update(lunge_legs(hy, front=0.58, back=(0.14, -0.28, -0.68)))
    return P


def slide(sink=0.0):
    """Frames 13-31: the lunge, held while the body slides: lower still, the torso pitched far forward, the head up looking ahead, the sword
    arm and blade dead level; the free arm cocked up behind the shoulders, the back leg stretched out behind (GIF frames 12-30)."""
    hy = 0.41 - sink
    P = {"hips": (0.0, hy, -0.16), "body": rot(p=52, y=12), "spine": rot(p=8, y=6), "neck": rot(p=-36, y=-10), "head": rot(p=-26, y=-8),
         "sword": {"hand": (-0.06, -0.30, 0.83), "blade": (0.0, 0.0, 1.0), "space": "root"},
         "free": {"hand": (-0.37, 0.36, -0.25), "pole": (-0.5, -0.8, -0.4), "space": "root"}}
    P.update(lunge_legs(hy, front=0.62, back=(0.14, -0.25, -0.70)))
    return P


def slide_end(arm_down=False):
    """Frame 35: the slide over, still in the lunge, the body and head lifted a little (GIF frames 30-33); by frame 41 (`arm_down`) the free
    arm has come down behind, ready to rise."""
    hy = 0.44
    P = {"hips": (0.0, hy, -0.14), "body": rot(p=44, y=10), "spine": rot(p=6, y=5), "neck": rot(p=-30, y=-8), "head": rot(p=-22, y=-6),
         "sword": {"hand": (-0.06, -0.30, 0.83), "blade": (0.0, -0.03, 1.0), "space": "root"},
         "free": {"hand": (-0.38, 0.28, -0.28), "pole": (-0.5, -0.8, -0.4), "space": "root"}}
    if arm_down:
        P["free"] = {"hand": (-0.20, -0.45, -0.30), "pole": (-0.4, -0.7, -0.4), "space": "root"}
    P.update(lunge_legs(hy, front=0.60, back=(0.14, -0.27, -0.69)))
    return P


def rise():
    """Frame 51: the back leg drawn in under the body, rising, the blade brought back up toward the stance, the free hand coming to the grip."""
    hy = 0.74
    P = {"hips": (0.0, hy, -0.04), "body": rot(p=12, y=-4), "spine": rot(p=4), "neck": rot(p=-6), "head": rot(p=-4),
         "sword": {"hand": (-0.05, -0.55, 0.45), "blade": (0.05, 0.35, 0.94), "space": "root"},
         "free": {"hand": (0.30, -0.65, 0.35), "pole": (-0.6, -0.6, -0.5), "space": "root"}}
    drop = ANKLE - hy
    P["l_leg"] = {"foot": (-0.08, drop, 0.25), "knee": (-0.1, 0.25, 1.0), "toe": (-0.05, 0.0, 1.0), "space": "root"}
    P["r_leg"] = {"foot": (0.08, drop + 0.02, -0.30), "knee": (0.1, 0.1, 1.0), "toe": (0.05, -0.3, 1.0), "space": "root"}
    return P


# ---------------------------------------------------------------------------------------------------------------------------------------------
# THE TIMELINE

def tween(a, b, t):
    """A pose part way from pose `a` to pose `b`, before solving: places and directions interpolated, turns slerped. Solved as it is, each limb
    takes a path between the two; the bones of two solved poses slerped instead turned the short way round, the blade passing straight up."""
    def lerp(x, y):
        return tuple(x[i] + (y[i] - x[i]) * t for i in range(3))
    P = {"hips": lerp(a["hips"], b["hips"])}
    for k in ("body", "spine", "neck", "head"):
        qa, qb = a.get(k, Quaternion()), b.get(k, Quaternion())
        if qa.dot(qb) < 0.0:
            qb = -qb
        P[k] = qa.slerp(qb, t)
    for arm in ("sword", "free"):
        A, B = a[arm], b[arm]
        part = {"hand": lerp(A["hand"], B["hand"]), "space": A.get("space", "chest")}
        for k in ("blade", "pole"):
            if k in A and k in B:
                part[k] = tuple(unit(Vector(lerp(A[k], B[k]))))
        P[arm] = part
    for leg in ("l_leg", "r_leg"):
        A, B = a[leg], b[leg]
        P[leg] = {"foot": lerp(A["foot"], B["foot"]), "knee": lerp(A["knee"], B["knee"]), "toe": lerp(A["toe"], B["toe"]),
                  "space": A.get("space", "root")}
    return P

def stab_frames():
    s, s_p = chained(stance(), None, "stance")
    d, d_p = chained(drawn(), s_p, "drawn")
    c, c_p = chained(cocked(), d_p, "cocked")
    through, through_p = chained(tween(cocked(), launch(), 0.5), c_p, "coming through")
    l, l_p = chained(launch(), through_p, "launch")
    t, t_p = chained(thrust(), l_p, "thrust")
    sl, sl_p = chained(slide(), t_p, "slide")
    low, low_p = chained(slide(sink=0.02), sl_p, "slide low")
    end, end_p = chained(slide_end(), low_p, "slide end")
    down, down_p = chained(slide_end(arm_down=True), end_p, "arm down")
    up, _ = chained(rise(), down_p, "rise")
    keys = [(0, s, "inout"), (4, d, "inout"), (9, c, "linear"), (10, through, "linear"), (11, l, "linear"), (12, t, "out"), (14, sl, "inout"), (20, low, "inout"),
            (31, low, "inout"), (35, end, "inout"), (45, down, "inout"), (53, up, "inout"), (STAB[1], s, "linear")]
    return {f: keyed(keys, f) for f in range(STAB[0], STAB[1] + 1)}


# ---------------------------------------------------------------------------------------------------------------------------------------------
# BAKE onto the armature: one key a frame, linear between; markers name the segment for the exporter.

def bake_stab():
    frames = stab_frames()
    if rig.animation_data and rig.animation_data.action:
        old = rig.animation_data.action
        rig.animation_data.action = None
        bpy.data.actions.remove(old)
    rig.animation_data_create()
    action = bpy.data.actions.new("StabDash")
    rig.animation_data.action = action
    rig.location = (0, 0, 0)
    previous = {}
    for f in sorted(frames):
        L, hips = frames[f]
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
    scene.timeline_markers.new("stab", frame=STAB[0])
    scene.timeline_markers.new("end", frame=STAB[1])
    scene.render.fps = FPS
    scene.frame_start = STAB[0]
    scene.frame_end = STAB[1]
    scene.frame_set(STAB[0])
    return len(frames)
