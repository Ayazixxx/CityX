# CITYX'S BODY IN BLENDER, ready to animate: the game's own skeleton (BodyRig), the real body mesh skinned to it the way the game skins it, and the
# sword in the right hand exactly where the game puts it. octaslash_pose.py animates it; octaslash_export.py sends the animation back to Unity.
#
# Run inside Blender on an empty scene:   exec(open(r"<this file>").read())
# Needs PlayerModel.obj at the repo root (Unity: CityX > Export Player Model, PlayerModelExporter).
#
# AXES. Blender (x, y, z) = Unity (-x, -z, y): the character faces -Y (Blender's front view), its left is +X (Blender's .L side), up is +Z.
# REST. Arms hanging (BodyRig.RestBones), which is how the game's body is modelled and bound; the game's live skeleton rests in a T-pose instead,
# and the exporter puts the difference (the shoulder's hang) back.

import bpy
import bmesh
import math
import os
from mathutils import Vector, Quaternion, Matrix

REPO = r"C:\Users\Lenovo\Downloads\the game"
OBJ = os.path.join(REPO, "PlayerModel.obj")

# BodyRig / FusedBodyShape's own numbers
HIP_Y, SHOULDER_Y, HIP_X, SHOULDER_X, HAND_X, HAND_Y, FOOT_Y = 0.9, 1.9, 0.17, 0.35, 0.58, 0.88, 0.135
HEAD_CENTRE_Y = SHOULDER_Y + 0.001 + 0.35
TOP = HEAD_CENTRE_Y + 0.35
KNEE_Y = HIP_Y - (HIP_Y - FOOT_Y) * 0.5
TORSO_R, ARM_R, LEG_R, NECK_R, BLEND_R, LIMB_WIDEN = 0.26, 0.09, 0.135, 0.10, 0.06, 2.5

# SwordWeapon's default hold, in the right hand bone's frame (Unity), and SwordBuilder's blade length
SWORD_AXIS = Vector((0.84, -0.25, 0.48)).normalized()
SWORD_ROLL = 270.0
PALM = Vector((0.949, 0.141, -0.283)).normalized() * 0.08
PRIMARY_GRIP_DOWN = 0.12
BLADE_LENGTH = 2.1

M = Matrix(((-1, 0, 0), (0, 0, 1), (0, -1, 0)))     # Blender -> Unity


def U(x, y, z):
    return Vector((-x, -z, y))


def arm_chain(side):
    s = -1.0 if side == "l" else 1.0
    shoulder = Vector((s * SHOULDER_X, SHOULDER_Y, 0.0))
    hand = Vector((s * HAND_X, HAND_Y, 0.0))
    elbow, wrist = shoulder.lerp(hand, 0.5), shoulder.lerp(hand, 0.85)
    tip = hand + (hand - shoulder).normalized() * 0.05
    return [("arm_" + side, shoulder, elbow, "spine", False), ("forearm_" + side, elbow, wrist, "arm_" + side, True),
            ("hand_" + side, wrist, hand, "forearm_" + side, True), ("fingertip_" + side, hand, tip, "hand_" + side, True)]


def leg_chain(side):
    s = -1.0 if side == "l" else 1.0
    hip, knee, ankle = Vector((s * HIP_X, HIP_Y, 0)), Vector((s * HIP_X, KNEE_Y, 0)), Vector((s * HIP_X, FOOT_Y, 0))
    return [("thigh_" + side, hip, knee, "hips", False), ("shin_" + side, knee, ankle, "thigh_" + side, True),
            ("foot_" + side, ankle, Vector((s * HIP_X, FOOT_Y, 0.2)), "shin_" + side, True)]


BONES = [("hips", Vector((0, HIP_Y, 0)), Vector((0, (HIP_Y + SHOULDER_Y) / 2, 0)), None, False),
         ("spine", Vector((0, (HIP_Y + SHOULDER_Y) / 2, 0)), Vector((0, SHOULDER_Y, 0)), "hips", True),
         ("neck", Vector((0, SHOULDER_Y, 0)), Vector((0, HEAD_CENTRE_Y, 0)), "spine", True),
         ("head", Vector((0, HEAD_CENTRE_Y, 0)), Vector((0, TOP, 0)), "neck", True)] \
    + arm_chain("l") + arm_chain("r") + leg_chain("l") + leg_chain("r")


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
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)   # the OBJ is a triangle soup
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
        b.head, b.tail, b.roll = U(*head), U(*tail), 0.0
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
    """FusedBodyShape.WeightsAt: every capsule claims exp(-(d - nearest) / k), summed per bone, the four heaviest kept; k is wider within a limb."""
    segs = [(U(0, HIP_Y, 0), U(0, 1.4, 0), TORSO_R, "hips", 0), (U(0, 1.4, 0), U(0, SHOULDER_Y, 0), TORSO_R, "spine", 0),
            (U(-SHOULDER_X, SHOULDER_Y, 0), U(SHOULDER_X, SHOULDER_Y, 0), ARM_R, "spine", 0),
            (U(0, SHOULDER_Y, 0), U(0, SHOULDER_Y + 0.001, 0), NECK_R, "neck", 0)]
    for side, limb_arm, limb_leg in (("l", 1, 3), ("r", 2, 4)):
        for name, h, t, _, _ in arm_chain(side)[:3]:
            segs.append((U(*h), U(*t), ARM_R, name, limb_arm))
        for name, h, t, _, _ in leg_chain(side)[:2]:
            segs.append((U(*h), U(*t), LEG_R, name, limb_leg))
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


def add_sword(rig):
    """A plain long blade where SwordWeapon holds the game's: its grip in the right hand bone's frame, the blade along the sword's own +Y."""
    arm_down = math.atan2(SHOULDER_Y - HAND_Y, HAND_X - SHOULDER_X)
    hang = Quaternion((0, 0, 1), -arm_down)                                   # the right arm chain's arms-down rest (Unity)
    wrist = Vector((SHOULDER_X, SHOULDER_Y, 0)).lerp(Vector((HAND_X, HAND_Y, 0)), 0.85)
    grip_rot = Vector((0, 1, 0)).rotation_difference(SWORD_AXIS) @ Quaternion((0, 1, 0), math.radians(SWORD_ROLL))
    grip_pos = PALM + SWORD_AXIS * PRIMARY_GRIP_DOWN
    position = M.transposed() @ (wrist + hang @ grip_pos)
    rotation = M.transposed() @ (hang @ grip_rot).to_matrix() @ M

    mesh = bpy.data.meshes.new("Masamune")
    bm = bmesh.new()

    def v(x, y, z):
        return bm.verts.new(M.transposed() @ Vector((x, y, z)))
    rows = []
    for i in range(15):
        t = i / 14.0
        taper = 1.0 if t < 0.93 else max(0.05, (1.0 - t) / 0.07)
        bend = -0.06 * t * t
        rows.append((v(bend - 0.03 * taper, t * BLADE_LENGTH, 0), v(bend + 0.012 * taper, t * BLADE_LENGTH, 0.006 * taper),
                     v(bend + 0.012 * taper, t * BLADE_LENGTH, -0.006 * taper)))
    for (e0, p0, n0), (e1, p1, n1) in zip(rows, rows[1:]):
        bm.faces.new((e0, e1, p1, p0))
        bm.faces.new((e0, n0, n1, e1))
        bm.faces.new((p0, p1, n1, n0))
    for cx, cy, cz, sx, sy, sz in ((0, -0.01, 0, 0.07, 0.012, 0.07), (0, -0.25, 0, 0.022, 0.24, 0.022)):
        corners = [v(cx + dx * sx, cy + dy * sy, cz + dz * sz) for dx in (-1, 1) for dy in (-1, 1) for dz in (-1, 1)]
        for f in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)):
            bm.faces.new([corners[j] for j in f])
    bm.to_mesh(mesh)
    bm.free()
    sword = bpy.data.objects.new("Masamune", mesh)
    bpy.context.scene.collection.objects.link(sword)
    sword.parent = rig
    sword.parent_type = 'BONE'
    sword.parent_bone = "hand_r"
    bpy.context.view_layer.update()
    sword.matrix_world = Matrix.Translation(position) @ rotation.to_4x4()
    return sword


def material(name, rgb, metal, rough):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    m.diffuse_color = (*rgb, 1.0)
    return m


def build():
    body, head = import_body()
    rig = build_rig()
    skin(rig, body, head)
    sword = add_sword(rig)
    skin_mat = material("CityXSkin", (0.80, 0.72, 0.66), 0.0, 0.6)
    for o in (body, head):
        o.data.materials.clear()
        o.data.materials.append(skin_mat)
    sword.data.materials.append(material("Steel", (0.85, 0.87, 0.9), 1.0, 0.18))
    bpy.context.scene.render.fps = 60
    return rig


build()
print("CityXRig built")
