# A RELOAD, EXPORTED FOR UNITY: every frame of CityXRig's action as each bone's LOCAL rotation in the game's live skeleton (fingers included),
# where the hips are, and each gun part's position and rotation in the GUN'S OWN FRAME (so the game sets the part transforms under the gun,
# whichever hand bone the gun hangs off), with the segments ("tactical", "empty") and the events ("drop", "grab", "seat", "rack") the markers name.
# BakedMotion reads it. The trip back to Unity's bones is octaslash_export.py's: Blender->Unity axes with M, the arms-down rest put back with HANG.
#
# Run after firearm_pose.bake():  exec(open(...).read()) with EXPORT_PATH set.

import bpy
import json
import math
import os
from mathutils import Matrix, Quaternion, Vector

_M = Matrix(((-1, 0, 0), (0, 0, 1), (0, -1, 0)))     # Blender -> Unity


def export(path, segment_names=("tactical", "empty")):
    scene = bpy.context.scene
    rig_ob = bpy.data.objects["CityXRig"]
    gun = bpy.data.objects[GUN_NAME]
    names = [b.name for b in rig_ob.data.bones]
    parent = {b.name: (b.parent.name if b.parent else None) for b in rig_ob.data.bones}
    rest = {b.name: b.matrix_local.to_quaternion() for b in rig_ob.data.bones}
    arm_down = math.degrees(math.atan2(1.9 - 0.88, 0.58 - 0.35))
    hang = {}
    for n in names:
        if n.endswith("_l"):
            hang[n] = Quaternion((0, 0, 1), math.radians(arm_down))
        elif n.endswith("_r"):
            hang[n] = Quaternion((0, 0, 1), math.radians(-arm_down))
    for n in ("thigh_l", "shin_l", "foot_l", "thigh_r", "shin_r", "foot_r"):
        hang.pop(n, None)                                       # the legs rest straight down in both skeletons

    markers = {m.name: m.frame for m in scene.timeline_markers}
    first = min(markers["seg_" + s] for s in segment_names)
    last = max(markers["segend_" + s] for s in segment_names)
    segments = [{"name": s, "start": markers["seg_" + s] - first, "end": markers["segend_" + s] - first} for s in segment_names]
    events = [{"name": m[3:], "frame": f - first} for m, f in markers.items() if m.startswith("ev_")]

    hips, rotations = [], []
    # Each part in ITS gun's frame: a pair's left-hand gun's parts (named with an "L") in the left-hand gun's (firearm_pose.GUN_L).
    owners = [(name, gun) for name in PARTS]
    if bpy.data.objects.get(GUN_NAME + "L") is not None:
        owners += [(name + "L", bpy.data.objects[GUN_NAME + "L"]) for name in PARTS]
    props = {name: {"name": name, "positions": [], "rotations": [], "visible": []} for name, _ in owners}
    previous = {}
    for f in range(first, last + 1):
        scene.frame_set(f)
        bpy.context.view_layer.update()
        world = {}
        for n in names:
            pb = rig_ob.pose.bones[n]
            turn = pb.matrix.to_quaternion() @ rest[n].inverted()
            unity = (_M @ turn.to_matrix() @ _M.transposed()).to_quaternion()
            world[n] = unity @ hang.get(n, Quaternion())
        for n in names:
            q = world[n] if parent[n] is None else world[parent[n]].inverted() @ world[n]
            q.normalize()
            if n in previous and previous[n].dot(q) < 0.0:
                q = -q
            previous[n] = q
            rotations.extend(round(c, 5) for c in (q.x, q.y, q.z, q.w))
        p = _M @ rig_ob.pose.bones["hips"].head
        hips.extend(round(c, 5) for c in (p.x, p.y, p.z))
        for name, owner in owners:
            part = bpy.data.objects[name]
            local = owner.matrix_world.inverted() @ part.matrix_world
            lp = _M @ local.to_translation()
            lr = (_M @ local.to_quaternion().to_matrix() @ _M.transposed()).to_quaternion()
            lr.normalize()
            key = "prop_" + name
            if key in previous and previous[key].dot(lr) < 0.0:
                lr = -lr
            previous[key] = lr
            props[name]["positions"].extend(round(c, 5) for c in (lp.x, lp.y, lp.z))
            props[name]["rotations"].extend(round(c, 5) for c in (lr.x, lr.y, lr.z, lr.w))
            props[name]["visible"].append(0 if part.hide_render else 1)

    data = {
        "fps": scene.render.fps,
        "frames": last - first + 1,
        "bones": names,
        "segments": segments,
        "cutFrames": [],
        "events": events,
        "hips": hips,
        "rotations": rotations,
        "props": [props[name] for name, _ in owners],
    }
    path = os.path.normpath(path)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w") as fh:
        json.dump(data, fh, separators=(",", ":"))
    scene.frame_set(first)
    print("wrote %s: %d frames, %d bones, %d props, %d bytes" % (path, data["frames"], len(names), len(owners), os.path.getsize(path)))
    print("segments", segments, "events", events)
    return data
