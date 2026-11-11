# THE BARE-HANDED GUARD, as clips. Owner, 2026-11-10: "improve upon the blocking no weapon in hand animation right now the legs dont even move and it isnt
# timed right at all. redo the hole thing like a souls like game". The old block was a borrowed full-body clip thrown for a fixed 0.7 s. The guard is held now,
# as the sword's is, and these are its upper body (the game leaves the hips and legs to the walk: BakedPose.UpperBodyOnly, so they walk under it):
#   guard_raise  0.15 s  from standing ready: both fists up before the face, forearms upright, elbows in over the ribs, the chin tucked, the left side led
#   guard_hold   1.6 s   the guard, breathing: looped while the button is held
#   guard_hit    0.35 s  a blow soaked: the forearms driven back at the face, the trunk rocked back and sunk, and up into the guard again
#   guard_parry  0.40 s  a blow caught in the guard's first instant: the lead forearm swats it out and aside, the trunk turning with it, and back
#
# Needs the rig (../Octaslash/octaslash_rig.py), octaslash_pose.py's solver (with OCTASLASH_NO_BAKE set), ../planted_feet.py and ../FistCombo/fist_pose.py (its
# key, sample and body_pose) loaded first; guard_build.py does all four. Poses are in the character's own frame (Unity axes, metres, the ground at y 0).

import bpy

GUARD_POLE_R, GUARD_POLE_L = (0.45, -1.0, 0.15), (-0.45, -1.0, 0.15)


def guard(right=(0.21, 2.02, 0.50), left=(-0.19, 2.07, 0.60), twist=16.0, hips=8.0, lean=9.0, drop=0.09, shift=(0.0, 0.0), chin=8.0, left_pole=GUARD_POLE_L):
    return key(right, left, twist=twist, hips=hips, lean=lean, drop=drop, shift=shift, chin=chin, fist_pole=GUARD_POLE_R, off_pole=left_pole)


GUARD = guard()
BREATH = guard(right=(0.21, 2.005, 0.50), left=(-0.19, 2.055, 0.60), lean=10.0, drop=0.10)
# Soaked: the forearms knocked back almost onto the brow, the weight thrown onto the back foot.
KNOCKED = guard(right=(0.20, 2.00, 0.36), left=(-0.18, 2.04, 0.40), twist=10.0, hips=5.0, lean=-5.0, drop=0.14, shift=(0.0, -0.06), chin=13.0)
# Parried: the lead forearm thrown out and across, knocking what came aside to the left; the rear fist stays home by the cheek.
SWAT = guard(right=(0.20, 2.00, 0.44), left=(-0.62, 1.92, 0.78), twist=-22.0, hips=-10.0, lean=12.0, drop=0.10, shift=(-0.02, 0.03), chin=6.0,
             left_pole=(-0.9, -0.6, -0.3))

# name: seconds, keys [(share, key, how it is eased into)]
GUARDS = (
    ("guard_raise", 0.15, [(0.0, READY, None), (1.0, GUARD, "slow_in")]),
    ("guard_hold", 1.60, [(0.0, GUARD, None), (0.5, BREATH, "smooth"), (1.0, GUARD, "smooth")]),
    ("guard_hit", 0.35, [(0.0, GUARD, None), (0.22, KNOCKED, "slow_in"), (1.0, GUARD, "smooth")]),
    ("guard_parry", 0.40, [(0.0, GUARD, None), (0.28, SWAT, "slow_in"), (0.5, SWAT, "smooth"), (1.0, GUARD, "smooth")]),
)
ORDER_OF_GUARDS = tuple(name for name, _, _ in GUARDS)


def guard_pose_at(keys, t01):
    P, hips_at, body = body_pose(sample(keys, t01), 1.0)
    stats = {"reach": 0.0}
    for leg, x in (("l", -STANCE_X), ("r", STANCE_X)):
        hip_b = U(*hips_at) + body @ (HEAD["thigh_" + leg] - HEAD["hips"])
        P[leg + "_leg"] = planted_leg(leg, to_unity(hip_b), [], (x, 0.0), 0.0, 0.0, hips_at.z, 28.0, stats)
    return P


def bake_guards():
    if rig.animation_data and rig.animation_data.action:
        old = rig.animation_data.action
        rig.animation_data.action = None
        bpy.data.actions.remove(old)
    rig.animation_data_create()
    action = bpy.data.actions.new("FistGuard")
    rig.animation_data.action = action
    scene = bpy.context.scene
    scene.timeline_markers.clear()
    previous = {}
    start = 0
    spans = {}
    for name, seconds, keys in GUARDS:
        frames = int(round(seconds * FPS))
        scene.timeline_markers.new(name, frame=start)
        # One frame past the last share, so a held or looped clip ends exactly on its last key.
        for i in range(frames + 1):
            D, pos = solve(guard_pose_at(keys, i / float(frames)))
            apply_pose(locals_of(D), pos["hips"])
            f = start + i
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
        spans[name] = (start, start + frames, keys)
        start += frames + 1
    scene.timeline_markers.new("end", frame=start)
    for fc in action.fcurves:
        for kp in fc.keyframe_points:
            kp.interpolation = 'LINEAR'
    scene.render.fps = FPS
    scene.frame_start = 0
    scene.frame_end = start - 1
    scene.frame_set(0)
    return spans
