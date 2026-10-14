# THE RELOAD POSER: turns a short list of KEY SPECS ("the gun is here, pointing there; the free hand is at the magazine well, fingers this way; the
# magazine is half out") into every frame of a reload on CityXRig (firearm_rig.py) with the gun following the strong hand, the free hand placed by
# IK, the fingers folded, and the gun's parts (slide, magazine, bolt) moved -- then bakes it as keyframes for firearm_export.py.
#
# WHY SPECS ARE BLENDED, NOT SOLVED POSES. Octaslash blends solved joint rotations between keys. A reload is all about CONTACT: the free hand stays
# on the magazine as both travel, the strong hand stays on the grip while the gun turns. Blending joint rotations lets the hand drift off the
# magazine between keys; blending the targets and solving every frame keeps every contact exact.
#
# Run after firearm_rig.build() and the gun model's build() (exec'd into the same globals: see glock_build_all.py). All authoring is in UNITY
# AXES (x right, y up, z forward) in the character's own frame; see firearm_rig.py for the trip to Blender.

import bpy
import math
from mathutils import Vector, Quaternion, Matrix

FPS = 60

# ---------------------------------------------------------------------------------------------------------------------------------------------
# Axes and small helpers (Unity -> Blender as firearm_rig.U; a rotation R in Unity axes is M^T R M in Blender's).

M = Matrix(((-1, 0, 0), (0, 0, 1), (0, -1, 0)))


def U(v):
    return Vector((-v[0], -v[2], v[1]))


def B(q):
    """A rotation given in Unity axes, as the same physical rotation in Blender axes."""
    return (M.transposed() @ q.to_matrix() @ M).to_quaternion()


def unity_rotation(q):
    """The inverse of B: a Blender-axes rotation in Unity axes."""
    return (M @ q.to_matrix() @ M.transposed()).to_quaternion()


FWD, UP, RIGHT = U((0, 0, 1)), U((0, 1, 0)), U((1, 0, 0))
BACK, DOWN, LEFT = -FWD, -UP, -RIGHT


def pitch(deg):
    """Tips the top forward. A BODY turn, in Blender axes (as octaslash_pose.py)."""
    return Quaternion(LEFT, math.radians(deg))


def roll(deg):
    """Rolls the right side down (clockwise seen from behind)."""
    return Quaternion(FWD, math.radians(deg))


def yaw(deg):
    """Turns to the left."""
    return Quaternion(UP, math.radians(deg))


def rot(p=0.0, y=0.0, r=0.0):
    return yaw(y) @ roll(r) @ pitch(p)


def rot_cols(x, y, z):
    """A Unity-axes rotation from where it sends the x, y and z axes (three Unity vectors; z should be the left-handed cross of x and y)."""
    return Matrix((Vector(x), Vector(y), Vector(z))).transposed().to_quaternion()


def unit(v):
    v = Vector(v)
    return v.normalized() if v.length > 1e-9 else v


def frame_of(d, r):
    d = unit(d)
    r = Vector(r) - d * Vector(r).dot(d)
    if r.length < 1e-6:
        r = UP - d * UP.dot(d)
        if r.length < 1e-6:
            r = FWD - d * FWD.dot(d)
    r.normalize()
    return Matrix((d, r, d.cross(r))).transposed()


def delta(d0, r0, d1, r1):
    """The rotation taking direction d0 (with r0 as its 'up') onto d1 (with r1). Blender axes."""
    return (frame_of(d1, r1) @ frame_of(d0, r0).inverted()).to_quaternion()


def two_bone(root, target, l1, l2, pole):
    v = target - root
    d = max(abs(l1 - l2) + 1e-4, min(l1 + l2 - 1e-4, v.length))
    along = unit(v)
    a = (l1 * l1 - l2 * l2 + d * d) / (2.0 * d)
    h = math.sqrt(max(0.0, l1 * l1 - a * a))
    side = Vector(pole) - along * Vector(pole).dot(along)
    side = unit(side) if side.length > 1e-6 else unit(UP - along * UP.dot(along))
    return root + along * a + side * h, root + along * d


def slerp(a, b, t):
    if a.dot(b) < 0.0:
        b = -b
    return a.slerp(b, t)


def lerp(a, b, t):
    return a + (b - a) * t


def ease(kind, t):
    t = max(0.0, min(1.0, t))
    if kind == "in":
        return t * t
    if kind == "out":
        return 1.0 - (1.0 - t) * (1.0 - t)
    if kind == "inout":
        return t * t * (3.0 - 2.0 * t)
    return t


# ---------------------------------------------------------------------------------------------------------------------------------------------
# THE HOLD: where a pistol grip sits in the strong hand, for every firearm (the game's FirearmProfile.HoldPosition/HoldRotation must match).
# The gun's origin is the WEB of its grip (the top of the backstrap, under the beavertail or receiver); it sits in the web of the hand, between
# the thumb and the index finger on the palm's thumb-side edge. Gun axes in the hand's: the gun's RIGHT is the back of the hand (+y), its UP the
# thumb's side (+z), its FORWARD down the fingers (+x) but dipped by HOLD_DIP: the knuckles sit a little above the bore line on a high grip.
HOLD_POS = Vector((0.10, -0.01, 0.095))
HOLD_DIP = 15.0


def hold_rotation():
    """R_hold (Unity): gun x -> hand +y; gun z -> hand +x dipped toward -z by HOLD_DIP; gun y -> the rest."""
    d = math.radians(HOLD_DIP)
    z = Vector((math.cos(d), 0.0, -math.sin(d)))
    y = Vector((math.sin(d), 0.0, math.cos(d)))
    x = Vector((0.0, 1.0, 0.0))
    return Matrix((x, y, z)).transposed().to_quaternion()


HOLD_ROT = hold_rotation()

# HandGrip's own angles (degrees): how far each finger bone folds toward the palm at a full grip, and the thumb across.
FINGER_PROXIMAL, FINGER_INTERMEDIATE, THUMB_PROXIMAL, THUMB_INTERMEDIATE = 55.0, 50.0, 35.0, 25.0

# ---------------------------------------------------------------------------------------------------------------------------------------------
# The rest: bone heads and orientations from the armature, and the gun's parts.

RIG = "CityXRig"
rig = bpy.data.objects[RIG]
BONES = {b.name: b for b in rig.data.bones}
ORDER = [b.name for b in rig.data.bones]          # created parents-first
PARENT = {n: (b.parent.name if b.parent else None) for n, b in BONES.items()}
HEAD = {n: b.head_local.copy() for n, b in BONES.items()}
TAIL = {n: b.tail_local.copy() for n, b in BONES.items()}
REST = {n: b.matrix_local.copy() for n, b in BONES.items()}
rig.location = (0, 0, 0)
for pb in rig.pose.bones:
    pb.rotation_mode = 'QUATERNION'
    pb.matrix_basis = Matrix.Identity(4)
bpy.context.view_layer.update()

HANG = {"r": B(Quaternion((0, 0, 1), -ARM_DOWN)), "l": B(Quaternion((0, 0, 1), ARM_DOWN))}   # the arms-down rest, Blender axes (ARM_DOWN: firearm_rig)
HANG_U = {"r": Quaternion((0, 0, 1), -ARM_DOWN), "l": Quaternion((0, 0, 1), ARM_DOWN)}

GUN = bpy.data.objects[GUN_NAME]                                    # the gun model's root (GUN_NAME, PARTS set by the model script)
PART = {name: bpy.data.objects[name] for name in PARTS}
for o in [GUN] + list(PART.values()):
    o.rotation_mode = 'QUATERNION'
PART_REST = {name: (PART[name].location.copy(), PART[name].rotation_quaternion.copy()) for name in PARTS}
PART_REST_U = {name: M @ PART[name].location for name in PARTS}        # each part's origin in the gun's frame, Unity


def child_head(D, pos, child, parent):
    return pos[parent] + D[parent] @ (HEAD[child] - HEAD[parent])


# ---------------------------------------------------------------------------------------------------------------------------------------------
# A SPEC (one key of the reload), Unity axes, the character's own frame:
#   hips (x,y,z); body, spine, neck, head: Quaternions (rot()).
#   gun: {at: the gun's origin from the CHEST CENTRE (the top of the spine), in the chest's frame; fwd, up: which way it points}
#   r_pole: which way the strong elbow points (chest frame).
#   l_hand: {at, fingers, palm, space: "chest"|"gun"|"hand_r", pole}: where the free wrist goes and which way its fingers and palm face, in the
#           chest's frame, the gun's own, or the strong hand's.
#   fingers_r / fingers_l: {index, middle, ring, little, thumb: 0 (open) .. 1 (folded); thumb_forward: degrees the thumb lies forward along the
#           frame instead of across; trigger: extra fold of the index finger (the pull)}
#   props: {part: {pos, rot (gun frame), visible, in_hand (the free hand carries it), hand (pos, rot of the part in the free hand's frame)}}
# Discrete fields (space, visible, in_hand, hand) step at a key; everything else blends between keys.

def fingers(index=1.0, middle=1.0, ring=1.0, little=1.0, thumb=1.0, thumb_forward=0.0, trigger=0.0):
    return {"index": index, "middle": middle, "ring": ring, "little": little, "thumb": thumb, "thumb_forward": thumb_forward, "trigger": trigger}


OPEN_HAND = fingers(0.15, 0.15, 0.15, 0.15, 0.1)
PISTOL_STRONG = fingers(0.12, 1.0, 1.0, 1.0, 0.25, thumb_forward=55.0)
PISTOL_SUPPORT = fingers(0.95, 0.95, 0.95, 0.9, 0.2, thumb_forward=50.0)
RIFLE_STRONG = fingers(0.12, 1.0, 1.0, 1.0, 0.35, thumb_forward=40.0)
RIFLE_SUPPORT = fingers(0.75, 0.8, 0.8, 0.75, 1.0)
MAG_HOLD = fingers(0.3, 0.85, 0.9, 0.9, 0.7, thumb_forward=20.0)


def finger_turns(spec, right):
    """The ten finger bones' local rotations (Unity, HandGrip's order: index1, index2, ... little2, thumb1, thumb2) for a spec."""
    out = []
    sign = -1.0 if right else 1.0
    for name in ("index", "middle", "ring", "little"):
        amount = spec[name] + (spec.get("trigger", 0.0) if name == "index" else 0.0)
        amount = max(0.0, min(1.0, amount))
        out.append(Quaternion((0, 0, 1), math.radians(sign * FINGER_PROXIMAL * amount)))
        out.append(Quaternion((0, 0, 1), math.radians(sign * FINGER_INTERMEDIATE * amount)))
    forward = Quaternion((0, 1, 0), math.radians((1.0 if right else -1.0) * spec.get("thumb_forward", 0.0)))
    out.append(forward @ Quaternion((1, 0, 0), math.radians(THUMB_PROXIMAL * spec["thumb"])))
    out.append(Quaternion((1, 0, 0), math.radians(THUMB_INTERMEDIATE * spec["thumb"])))
    return out


def blend_fingers(a, b, t):
    return {k: lerp(a[k], b[k], t) for k in a}


def blend_vec(a, b, t):
    return lerp(Vector(a), Vector(b), t)


def hand_frame_rotation(fingers_dir, palm_dir, right):
    """The hand's T-pose-based world rotation (Blender axes) whose fingers point along fingers_dir with the palm facing palm_dir (Blender dirs)."""
    rest_fingers = U((1, 0, 0)) if right else U((-1, 0, 0))
    rest_palm = U((0, -1, 0))
    return delta(rest_fingers, rest_palm, fingers_dir, palm_dir)


# ---------------------------------------------------------------------------------------------------------------------------------------------
# SOLVE one frame: specs A and B blended by t.

def solve(A, KB=None, t=0.0):
    if KB is None:
        KB, t = A, 0.0
    D, pos = {}, {}
    D["hips"] = slerp(A["body"], KB["body"], t)
    pos["hips"] = U(blend_vec(A["hips"], KB["hips"], t))
    for child, parent in (("spine", "hips"), ("neck", "spine"), ("head", "neck")):
        D[child] = D[parent] @ slerp(A.get(child, Quaternion()), KB.get(child, Quaternion()), t)
        pos[child] = child_head(D, pos, child, parent)
    Dc = D["spine"]
    chest = pos["neck"]

    # THE GUN, where the spec puts it (chest frame), and the strong hand on it.
    gun_at = chest + Dc @ U(blend_vec(A["gun"]["at"], KB["gun"]["at"], t))
    gun_fwd = Dc @ U(unit(blend_vec(A["gun"]["fwd"], KB["gun"]["fwd"], t)))
    gun_up = Dc @ U(unit(blend_vec(A["gun"]["up"], KB["gun"]["up"], t)))
    Wg = delta(FWD, UP, gun_fwd, gun_up)                      # the gun's own axes (Unity-mapped) turned to point its way
    Wh = Wg @ B(HOLD_ROT).inverted()                          # the strong hand's T-pose-based world rotation
    wrist_wanted = gun_at - Wh @ U(HOLD_POS)

    for side in ("r", "l"):
        a, f, h, tip = "arm_" + side, "forearm_" + side, "hand_" + side, "fingertip_" + side
        shoulder = child_head(D, pos, a, "spine")
        l1 = (HEAD[f] - HEAD[a]).length
        l2 = (HEAD[h] - HEAD[f]).length
        u_rest = unit(HEAD[f] - HEAD[a])
        if side == "r":
            target = wrist_wanted
            pole = Dc @ U(unit(blend_vec(A.get("r_pole", (0.6, -1.0, -0.2)), KB.get("r_pole", (0.6, -1.0, -0.2)), t)))
            W = Wh
        else:
            ta = l_hand_world(A["l_hand"], chest, Dc, gun_at, Wg, D, pos)
            tb = l_hand_world(KB["l_hand"], chest, Dc, gun_at, Wg, D, pos)
            target = lerp(ta[0], tb[0], t)
            W = hand_frame_rotation(unit(lerp(ta[1], tb[1], t)), unit(lerp(ta[2], tb[2], t)), False)
            pole = lerp(ta[3], tb[3], t)
        elbow, wrist = two_bone(shoulder, target, l1, l2, pole)
        upper = unit(elbow - shoulder)
        forearm = unit(wrist - elbow)
        bend = -(unit(pole - upper * pole.dot(upper)))
        Du = delta(u_rest, FWD, upper, bend)
        Df = upper.rotation_difference(forearm) @ Du
        Dh = W @ HANG[side].inverted()
        D[a], D[f], D[h], D[tip] = Du, Df, Dh, Dh
        pos[a], pos[f], pos[h] = shoulder, elbow, wrist
        pos[tip] = wrist + Dh @ (HEAD[tip] - HEAD[h])
        if side == "r":
            gun_at = wrist + Wh @ U(HOLD_POS)                 # the gun on the hand even when the hand fell short of the target

    # THE FINGERS: each bone's local turn (Unity, the hand's T-pose frame) becomes a world turn through the hang.
    for side in ("r", "l"):
        spec = blend_fingers(A["fingers_" + side], KB["fingers_" + side], t)
        turns = finger_turns(spec, side == "r")
        hang_u = HANG_U[side]
        names = []
        for digit in ("index", "middle", "ring", "little", "thumb"):
            names += [digit + "1_" + side, digit + "2_" + side]
        for name, q in zip(names, turns):
            parent = PARENT[name]
            D[name] = D[parent] @ B(hang_u @ q @ hang_u.inverted())
            pos[name] = child_head(D, pos, name, parent)

    # THE LEGS: standing (the game keeps its own legs under a reload; these are for the preview).
    for side in ("l", "r"):
        th, sn, ft = "thigh_" + side, "shin_" + side, "foot_" + side
        hip = child_head(D, pos, th, "hips")
        l1 = (HEAD[sn] - HEAD[th]).length
        l2 = (HEAD[ft] - HEAD[sn]).length
        s = -1.0 if side == "l" else 1.0
        foot = U((s * 0.03, -0.765, 0.02))
        knee_dir = U((s * 0.1, 0.2, 1.0))
        knee, ankle = two_bone(hip, hip + foot, l1, l2, knee_dir)
        thigh = unit(knee - hip)
        shin = unit(ankle - knee)
        bend = shin - thigh * shin.dot(thigh)
        if bend.length < 1e-3:
            bend = -(knee_dir - thigh * knee_dir.dot(thigh))
        Dt = delta(DOWN, BACK, thigh, bend)
        Ds = thigh.rotation_difference(shin) @ Dt
        Dft = delta(FWD, UP, FWD, -shin)
        D[th], D[sn], D[ft] = Dt, Ds, Dft
        pos[th], pos[sn], pos[ft] = hip, knee, ankle

    # THE PARTS: where each sits in the gun's frame this frame (Unity; its origin's FULL local position), carried by the free hand when the
    # spec says so. A spec's `pos` is the offset from the part's rest.
    props = {}
    Wl = D["hand_l"] @ HANG["l"]
    for name in PARTS:
        pa, pb = A["props"].get(name), KB["props"].get(name)
        if pa is None or pb is None:
            continue
        visible = pa.get("visible", 1)
        if pa.get("in_hand", 0):
            hp, hr = pa["hand"]
            world_pos = pos["hand_l"] + Wl @ U(hp)
            world_rot = Wl @ B(hr)
            local_pos = M @ (Wg.inverted() @ (world_pos - gun_at))
            local_rot = unity_rotation(Wg.inverted() @ world_rot)
        else:
            local_pos = PART_REST_U[name] + blend_vec(pa.get("pos", (0, 0, 0)), pb.get("pos", (0, 0, 0)), t)
            local_rot = slerp(pa.get("rot", Quaternion()), pb.get("rot", Quaternion()), t)
        props[name] = (Vector(local_pos), local_rot, visible)
    return {"D": D, "pos": pos, "gun": (gun_at, Wg), "props": props}


def l_hand_world(spec, chest, Dc, gun_at, Wg, D, pos):
    """The free hand's target (position, fingers direction, palm direction, pole) in Blender world space for one key's spec."""
    space = spec.get("space", "chest")
    if space == "gun":
        origin, R = gun_at, Wg
    elif space == "hand_r":
        origin, R = pos["hand_r"], D["hand_r"] @ HANG["r"]
    else:
        origin, R = chest, Dc
    at = origin + R @ U(spec["at"])
    fingers_dir = R @ U(unit(spec["fingers"]))
    palm_dir = R @ U(unit(spec["palm"]))
    pole = Dc @ U(unit(spec.get("pole", (-0.7, -1.0, -0.3))))
    return at, fingers_dir, palm_dir, pole


def hand_holding(part_pos, part_rot, hand_offset, right=False):
    """An l_hand spec (gun space) that puts the free hand where it holds a part whose origin sits at part_pos (full local position) with
    part_rot in the gun's frame, the part at hand_offset (pos, rot) in the hand's own frame (Unity)."""
    hp, hr = hand_offset
    hand_rot = part_rot @ hr.inverted()
    hand_pos = Vector(part_pos) - hand_rot @ Vector(hp)
    fingers_dir = hand_rot @ Vector((1.0 if right else -1.0, 0.0, 0.0))
    palm_dir = hand_rot @ Vector((0.0, -1.0, 0.0))
    return {"at": tuple(hand_pos), "fingers": tuple(fingers_dir), "palm": tuple(palm_dir), "space": "gun"}


# ---------------------------------------------------------------------------------------------------------------------------------------------
# APPLY a solved frame to the scene, and BAKE a timeline.

def apply(pose):
    D, pos = pose["D"], pose["pos"]
    X = {}
    for n in ORDER:
        Xn = Matrix.Translation(pos[n]) @ (D[n] @ REST[n].to_quaternion()).to_matrix().to_4x4()
        X[n] = Xn
        p = PARENT[n]
        if p is None:
            basis = REST[n].inverted() @ Xn
        else:
            basis = (X[p] @ REST[p].inverted() @ REST[n]).inverted() @ Xn
        pb = rig.pose.bones[n]
        pb.matrix_basis = basis
        if p is not None:
            pb.location = (0.0, 0.0, 0.0)
    gun_at, Wg = pose["gun"]
    GUN.location = gun_at
    GUN.rotation_quaternion = Wg
    for name, (local_pos, local_rot, visible) in pose["props"].items():
        part = PART[name]
        rest_pos, rest_rot = PART_REST[name]
        part.location = U(local_pos)
        part.rotation_quaternion = B(local_rot) @ rest_rot
        part.hide_render = not visible
        part.hide_viewport = not visible


def keyed(keys, frame):
    """keys: [(frame, spec, ease into the next)] -> the solved pose at frame."""
    for (f0, s0, kind), (f1, s1, _) in zip(keys, keys[1:]):
        if f0 <= frame <= f1:
            return solve(s0, s1, ease(kind, (frame - f0) / float(max(1, f1 - f0))))
    return solve(keys[-1][1])


def bake(keys, segments, events, action_name="Reload"):
    """Keys every frame from the first key to the last; markers name the segments ({name: (start, end)}) and the events ({name: frame})."""
    if rig.animation_data and rig.animation_data.action:
        old = rig.animation_data.action
        rig.animation_data.action = None
        bpy.data.actions.remove(old)
    rig.animation_data_create()
    rig.animation_data.action = bpy.data.actions.new(action_name)
    first, last = keys[0][0], keys[-1][0]
    previous = {}
    for f in range(first, last + 1):
        apply(keyed(keys, f))
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
        GUN.keyframe_insert("location", frame=f)
        GUN.keyframe_insert("rotation_quaternion", frame=f)
        for name in PARTS:
            part = PART[name]
            part.keyframe_insert("location", frame=f)
            part.keyframe_insert("rotation_quaternion", frame=f)
            part.keyframe_insert("hide_render", frame=f)
            part.keyframe_insert("hide_viewport", frame=f)
    for ob in [rig, GUN] + list(PART.values()):
        if ob.animation_data and ob.animation_data.action:
            for fc in ob.animation_data.action.fcurves:
                for kp in fc.keyframe_points:
                    kp.interpolation = 'LINEAR' if fc.data_path not in ("hide_render", "hide_viewport") else 'CONSTANT'
    scene = bpy.context.scene
    scene.timeline_markers.clear()
    for name, (a, b) in segments.items():
        scene.timeline_markers.new("seg_" + name, frame=a)
        scene.timeline_markers.new("segend_" + name, frame=b)
    for name, f in events.items():
        scene.timeline_markers.new("ev_" + name, frame=f)
    scene.render.fps = FPS
    scene.frame_start = first
    scene.frame_end = last
    scene.frame_set(first)
    return last - first + 1
