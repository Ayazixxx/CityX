# CITYX'S BODY IN BLENDER WITH FINGERS AND A GUN, ready to animate a reload (owner, 2026-10-14: "realistic reload animations ... fingers in place as
# they would a real gun"). The same skeleton as octaslash_rig.py (BodyRig's bones, arms-down rest, the real body mesh skinned the way the game skins
# it) plus the twenty finger bones the game added on 2026-10-13 (BodyRig.AddFingers: two per digit, at FusedBodyShape.Digits' joints), and a gun
# built by its own model script (glock_model.py, ak47_model.py) as a free object the pose solver moves with the hand.
#
# Run inside Blender (headless: see glock_build_all.py), on an EMPTY scene. Needs PlayerModel.obj at the repo root.
#
# AXES. Blender (x, y, z) = Unity (-x, -z, y): the character faces -Y in Blender, its left is +X, up is +Z. Everything the pose scripts author is in
# Unity axes and converted with U().
# REST. Arms hanging (BodyRig.RestBones). The game's live skeleton rests in a T-pose; firearm_export.py puts the hang back (HANG).
# THE HAND'S FRAME (Unity, T-pose; HandGrip): +X runs down the arm to the fingertips (the LEFT hand's the other way), -Y is the palm's side, +Z
# the thumb's; the finger bones' rest rotations are the identity like every other bone's.

import bpy
import bmesh
import math
import os
from mathutils import Vector, Quaternion, Matrix

REPO = r"C:\Users\Lenovo\Downloads\the game"
OBJ = os.path.join(REPO, "PlayerModel.obj")
SKIP_BODY = os.environ.get("FIREARM_NO_BODY") == "1"     # the export needs only the armature; the renders want the body

# BodyRig / FusedBodyShape's own numbers
HIP_Y, SHOULDER_Y, HIP_X, SHOULDER_X, HAND_X, HAND_Y, FOOT_Y = 0.9, 1.9, 0.17, 0.35, 0.58, 0.88, 0.135
HEAD_CENTRE_Y = SHOULDER_Y + 0.001 + 0.35
TOP = HEAD_CENTRE_Y + 0.35
KNEE_Y = HIP_Y - (HIP_Y - FOOT_Y) * 0.5
TORSO_R, ARM_R, LEG_R, NECK_R, BLEND_R, LIMB_WIDEN = 0.26, 0.09, 0.135, 0.10, 0.06, 2.5
ARM_DOWN = math.atan2(SHOULDER_Y - HAND_Y, HAND_X - SHOULDER_X)      # BodyRig.ArmDownDegrees, radians

# FusedBodyShape's hand (metres at the reference height), in the hand's own frame.
PALM_LENGTH, PALM_HALF_WIDTH = 0.16, 0.11
FINGER_RADIUS, FINGER_SPACING = 0.025, 0.058
FINGER_LENGTHS = (0.088, 0.098, 0.09, 0.072)
FINGER_CURL, FINGER_TIP_CURL, KNUCKLE_SHARE = 20.0, 50.0, 0.55
THUMB_RADIUS, THUMB_LENGTH, THUMB_TIP_LENGTH = 0.027, 0.06, 0.052
DIGIT_NAMES = ("index", "middle", "ring", "little", "thumb")

M = Matrix(((-1, 0, 0), (0, 0, 1), (0, -1, 0)))     # Blender -> Unity


def U(x, y=None, z=None):
    """Unity (x right, y up, z forward) -> Blender."""
    if y is None:
        x, y, z = x
    return Vector((-x, -z, y))


def digits():
    """FusedBodyShape.Digits: (name, root, knuckle, tip, radius) in the hand's frame."""
    c1 = Vector((math.cos(math.radians(FINGER_CURL)), -math.sin(math.radians(FINGER_CURL)), 0.0))
    c2 = Vector((math.cos(math.radians(FINGER_TIP_CURL)), -math.sin(math.radians(FINGER_TIP_CURL)), 0.0))
    out = []
    for i in range(4):
        root = Vector((PALM_LENGTH - 0.01, 0.0, (1.5 - i) * FINGER_SPACING))
        knuckle = root + c1 * (FINGER_LENGTHS[i] * KNUCKLE_SHARE)
        tip = knuckle + c2 * (FINGER_LENGTHS[i] * (1.0 - KNUCKLE_SHARE))
        out.append((DIGIT_NAMES[i], root, knuckle, tip, FINGER_RADIUS))
    root = Vector((0.045, 0.0, PALM_HALF_WIDTH - 0.01))
    knuckle = root + Vector((0.45, -0.25, 0.8)).normalized() * THUMB_LENGTH
    tip = knuckle + Vector((0.6, -0.6, 0.45)).normalized() * THUMB_TIP_LENGTH
    out.append(("thumb", root, knuckle, tip, THUMB_RADIUS))
    return out


def finger_local(p, right):
    """BodyRig.FingerLocal: a hand-frame point in the hand BONE's axes (the left hand's x runs the other way)."""
    return Vector((p.x if right else -p.x, p.y, p.z))


def hang(side):
    """The arm chain's arms-down rest turn from the T-pose (Unity axes): BodyRig.RestBindMatrix's rotation."""
    return Quaternion((0, 0, 1), -ARM_DOWN if side == "r" else ARM_DOWN)


def arm_chain(side):
    s = -1.0 if side == "l" else 1.0
    shoulder = Vector((s * SHOULDER_X, SHOULDER_Y, 0.0))
    hand = Vector((s * HAND_X, HAND_Y, 0.0))
    elbow, wrist = shoulder.lerp(hand, 0.5), shoulder.lerp(hand, 0.85)
    tip = hand + (hand - shoulder).normalized() * 0.05
    return [("arm_" + side, shoulder, elbow, "spine", False), ("forearm_" + side, elbow, wrist, "arm_" + side, True),
            ("hand_" + side, wrist, hand, "forearm_" + side, True), ("fingertip_" + side, hand, tip, "hand_" + side, True)]


def finger_bones(side):
    """The ten finger bones of one hand at the arms-down rest: BodyRig's T-pose joints, hung with the arm."""
    right = side == "r"
    s = 1.0 if right else -1.0
    shoulder = Vector((s * SHOULDER_X, SHOULDER_Y, 0.0))
    arm_length = (Vector((HAND_X, HAND_Y, 0.0)) - Vector((SHOULDER_X, SHOULDER_Y, 0.0))).length   # BodyRig.ArmLength: shoulder to fingertip
    wrist_t = Vector((s * (SHOULDER_X + arm_length * 0.85), SHOULDER_Y, 0.0))                     # the T-pose wrist (BodyRig WristT 0.85)
    h = hang(side)

    def rest(p):
        return shoulder + h @ (wrist_t - shoulder + finger_local(p, right))
    out = []
    for name, root, knuckle, tip, _ in digits():
        out.append((name + "1_" + side, rest(root), rest(knuckle), "hand_" + side, False))
        out.append((name + "2_" + side, rest(knuckle), rest(tip), name + "1_" + side, False))
    return out


def leg_chain(side):
    s = -1.0 if side == "l" else 1.0
    hip, knee, ankle = Vector((s * HIP_X, HIP_Y, 0)), Vector((s * HIP_X, KNEE_Y, 0)), Vector((s * HIP_X, FOOT_Y, 0))
    return [("thigh_" + side, hip, knee, "hips", False), ("shin_" + side, knee, ankle, "thigh_" + side, True),
            ("foot_" + side, ankle, Vector((s * HIP_X, FOOT_Y, 0.2)), "shin_" + side, True)]


BONES = [("hips", Vector((0, HIP_Y, 0)), Vector((0, (HIP_Y + SHOULDER_Y) / 2, 0)), None, False),
         ("spine", Vector((0, (HIP_Y + SHOULDER_Y) / 2, 0)), Vector((0, SHOULDER_Y, 0)), "hips", True),
         ("neck", Vector((0, SHOULDER_Y, 0)), Vector((0, HEAD_CENTRE_Y, 0)), "spine", True),
         ("head", Vector((0, HEAD_CENTRE_Y, 0)), Vector((0, TOP, 0)), "neck", True)] \
    + arm_chain("l") + arm_chain("r") + leg_chain("l") + leg_chain("r") + finger_bones("r") + finger_bones("l")


def clear_scene():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)


def import_body():
    before = set(bpy.data.objects)
    bpy.ops.wm.obj_import(filepath=OBJ, forward_axis='NEGATIVE_Z', up_axis='Y')
    made = [o for o in bpy.data.objects if o not in before]
    for o in made:
        bpy.ops.object.select_all(action='DESELECT')
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bm.to_mesh(o.data)
        bm.free()
        for p in o.data.polygons:
            p.use_smooth = True
    return bpy.data.objects["Body"], bpy.data.objects["Head"]


def build_rig():
    data = bpy.data.armatures.new("CityXRig")
    rig = bpy.data.objects.new("CityXRig", data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    for name, head, tail, parent, connect in BONES:
        b = data.edit_bones.new(name)
        b.head, b.tail, b.roll = U(head), U(tail), 0.0
    for name, head, tail, parent, connect in BONES:
        if parent:
            data.edit_bones[name].parent = data.edit_bones[parent]
            data.edit_bones[name].use_connect = connect
    bpy.ops.object.mode_set(mode='OBJECT')
    data.display_type = 'STICK'
    rig.show_in_front = True
    for pb in rig.pose.bones:
        pb.rotation_mode = 'QUATERNION'
    return rig


def segment_distance(p, a, b, r):
    ba = b - a
    h = 0.0 if ba.length_squared < 1e-12 else max(0.0, min(1.0, (p - a).dot(ba) / ba.length_squared))
    return (p - a - ba * h).length - r


def skin(rig, body, head):
    """FusedBodyShape.WeightsAt, with the fingers: every capsule claims exp(-(d - nearest) / k), the four heaviest kept. A finger's two
    capsules are their own bones, so the hands can fold in the check renders."""
    segs = [(U(0, HIP_Y, 0), U(0, 1.4, 0), TORSO_R, "hips", 0), (U(0, 1.4, 0), U(0, SHOULDER_Y, 0), TORSO_R, "spine", 0),
            (U(-SHOULDER_X, SHOULDER_Y, 0), U(SHOULDER_X, SHOULDER_Y, 0), ARM_R, "spine", 0),
            (U(0, SHOULDER_Y, 0), U(0, SHOULDER_Y + 0.001, 0), NECK_R, "neck", 0)]
    for side, limb_arm, limb_leg in (("l", 1, 3), ("r", 2, 4)):
        for name, h, t, _, _ in arm_chain(side)[:3]:
            segs.append((U(h), U(t), ARM_R, name, limb_arm))
        for name, h, t, _, _ in leg_chain(side)[:2]:
            segs.append((U(h), U(t), LEG_R, name, limb_leg))
        for name, h, t, _, _ in finger_bones(side):
            radius = THUMB_RADIUS if name.startswith("thumb") else FINGER_RADIUS
            segs.append((U(h), U(t), radius, name, 0))
    for ob in (body, head):
        ob.vertex_groups.clear()
        for b in rig.data.bones:
            ob.vertex_groups.new(name=b.name)
        ob.modifiers.new("Rig", 'ARMATURE').object = rig
        ob.parent = rig
    groups = {g.name: g for g in body.vertex_groups}
    for v in body.data.vertices:
        ds = [segment_distance(v.co, a, b, r) for a, b, r, _, _ in segs]
        nearest = min(ds)
        limb = segs[ds.index(nearest)][4]
        share = {}
        for i, (a, b, r, bone, l) in enumerate(segs):
            k = BLEND_R * LIMB_WIDEN if (limb != 0 and l == limb) else BLEND_R
            share[bone] = share.get(bone, 0.0) + math.exp(-(ds[i] - nearest) / k)
        ranked = sorted(share.items(), key=lambda kv: -kv[1])
        kept = [(bn, w) for bn, w in ranked[:4] if w >= ranked[0][1] * 0.005]
        total = sum(w for _, w in kept)
        for bn, w in kept:
            groups[bn].add([v.index], w / total, 'REPLACE')
    head.vertex_groups["head"].add([v.index for v in head.data.vertices], 1.0, 'REPLACE')


def material(name, rgb, metal, rough):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    m.diffuse_color = (*rgb, 1.0)
    return m


def build_proxy(rig):
    """A stand-in body for quick check renders: a capsule per bone (a box for each palm), rigidly on its bone. Nothing like the real skin, but it
    shows the hold and every finger in seconds instead of the minute the real body takes to skin."""
    skin_mat = material("CityXProxySkin", (0.80, 0.70, 0.62), 0.0, 0.7)
    radii = {"hips": 0.24, "spine": 0.24, "neck": 0.09, "head": 0.33, "arm": 0.085, "forearm": 0.075, "hand": 0.0, "fingertip": 0.0,
             "thigh": 0.13, "shin": 0.11, "foot": 0.06}
    bpy.context.view_layer.update()
    for b in rig.data.bones:
        kind = b.name.split("_")[0]
        if kind[:-1] in DIGIT_NAMES and kind[-1] in "12":
            radius = THUMB_RADIUS * 0.9 if kind.startswith("thumb") else FINGER_RADIUS * 0.9
        elif kind in radii:
            radius = radii[kind]
        else:
            continue
        head, tail = b.head_local.copy(), b.tail_local.copy()
        if kind == "hand":
            # the palm: a box from the wrist, PALM_LENGTH down the arm, PALM_HALF_WIDTH to each side (the thumb's side is +Z of the hand), flat
            along = (tail - head).normalized()
            side = "r" if b.name.endswith("_r") else "l"
            right = side == "r"
            h = hang(side)
            # the hand's own axes at the arms-down rest, in Blender: x down the arm (the left hand's the other way), y the back, z the thumb's side
            ax = U(h @ Vector((1.0 if right else -1.0, 0.0, 0.0)))
            ay = U(h @ Vector((0.0, 1.0, 0.0)))
            az = U(h @ Vector((0.0, 0.0, 1.0)))
            bm = bmesh.new()
            verts = []
            for dx in (0.0, PALM_LENGTH):
                for dy in (-0.035, 0.035):
                    for dz in (-PALM_HALF_WIDTH, PALM_HALF_WIDTH):
                        verts.append(bm.verts.new(head + ax * dx + ay * dy + az * dz))
            for f in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)):
                bm.faces.new([verts[j] for j in f])
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        elif kind == "head":
            bm = bmesh.new()
            bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=radius)
            centre = (head + tail) * 0.5
            for v in bm.verts:
                v.co += centre
        elif kind == "fingertip":
            continue
        else:
            bm = bmesh.new()
            axis = tail - head
            length = axis.length
            if length < 1e-4:
                continue
            axis.normalize()
            side = axis.orthogonal().normalized()
            up = axis.cross(side)
            rings = []
            for k, along in enumerate((-radius * 0.6 if kind not in ("hips", "spine") else 0.0, length + (radius * 0.6 if kind not in ("hips", "spine") else 0.0))):
                ring = []
                for i in range(10):
                    ang = 2.0 * math.pi * i / 10
                    ring.append(bm.verts.new(head + axis * along + (side * math.cos(ang) + up * math.sin(ang)) * radius))
                rings.append(ring)
            for i in range(10):
                j = (i + 1) % 10
                bm.faces.new((rings[0][i], rings[0][j], rings[1][j], rings[1][i]))
            bm.faces.new(rings[0][::-1])
            bm.faces.new(rings[1])
        mesh = bpy.data.meshes.new("proxy_" + b.name)
        bm.to_mesh(mesh)
        bm.free()
        mesh.materials.append(skin_mat)
        ob = bpy.data.objects.new("proxy_" + b.name, mesh)
        bpy.context.scene.collection.objects.link(ob)
        ob.parent = rig
        ob.parent_type = 'BONE'
        ob.parent_bone = b.name
        bpy.context.view_layer.update()
        ob.matrix_world = Matrix.Identity(4)


def build(with_body=not SKIP_BODY):
    """The rig (and the skinned body unless skipped, a capsule stand-in then). The gun is added by the model script's build(): a free object at
    the origin, moved by the pose."""
    rig = build_rig()
    if with_body:
        body, head = import_body()
        skin(rig, body, head)
        skin_mat = material("CityXSkin", (0.80, 0.72, 0.66), 0.0, 0.6)
        for o in (body, head):
            o.data.materials.clear()
            o.data.materials.append(skin_mat)
    else:
        build_proxy(rig)
    bpy.context.scene.render.fps = 60
    return rig
