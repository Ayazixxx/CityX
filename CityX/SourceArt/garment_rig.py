# SKINNING A GARMENT ONTO THIS GAME'S BODY, shared by the garments' Blender build scripts (MidnightCoat/coat_build.py, TShirt/tshirt_build.py).
# Load it into a script's globals with exec(), after Octaslash/octaslash_rig.py has built the body ("Body") and skeleton ("CityXRig").
#   body_weights:   each point takes the weights of the nearest skin; hands go to the forearms, the head to the neck, shins and feet to the thighs;
#   share_armpits:  round each armpit the weights are smoothed and what torso and arm share goes to the half-turning shoulder bone;
#   lift_collar:    up a collar the collar bone (the neck's turn, the head's place) takes over;
#   write_garment:  the BlenderGarmentMesh JSON, in the game's frame, four bones per point.
import json

import bpy
import numpy as np

SHOULDER_JOINTS = {"l": np.array([0.35, 0.0, 1.9]), "r": np.array([-0.35, 0.0, 1.9])}   # BodyRig's (Blender +x is the body's left)

# The game's bone order (SkinnedGarment.Bones, then GarmentHelperBones.Helpers / BlenderGarmentMesh.TargetBoneNames).
TARGET_BONES = ["hips", "spine", "neck", "head", "arm_l", "forearm_l", "hand_l", "arm_r", "forearm_r", "hand_r",
                "thigh_l", "shin_l", "thigh_r", "shin_r",
                "skirt_front_l", "skirt_back_l", "skirt_front_r", "skirt_back_r", "shoulder_l", "shoulder_r", "collar"]
BONE = {b: i for i, b in enumerate(TARGET_BONES)}
# The skeleton's other bones and the garment bone each one's share goes to (a garment never follows a hand, the head or a shin).
FOLDED = {"hand_l": "forearm_l", "fingertip_l": "forearm_l", "hand_r": "forearm_r", "fingertip_r": "forearm_r", "head": "neck",
          "shin_l": "thigh_l", "foot_l": "thigh_l", "shin_r": "thigh_r", "foot_r": "thigh_r"}


def mesh_coords(me):
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    return co.reshape(-1, 3)


def group_values(obj, name):
    values = np.zeros(len(obj.data.vertices))
    group = obj.vertex_groups.get(name)
    if group is None:
        return values
    for v in obj.data.vertices:
        for g in v.groups:
            if g.group == group.index:
                values[v.index] = g.weight
    return values


def body_weights(garment, body, rig):
    """One row per point of garment, one column per TARGET_BONES: the weights of the body's skin nearest it."""
    bpy.ops.object.select_all(action="DESELECT")
    garment.select_set(True)
    bpy.context.view_layer.objects.active = garment
    transfer = garment.modifiers.new("FromBody", "DATA_TRANSFER")
    transfer.object = body
    transfer.use_vert_data = True
    transfer.data_types_verts = {"VGROUP_WEIGHTS"}
    transfer.vert_mapping = "POLYINTERP_NEAREST"
    transfer.layers_vgroup_select_src = "ALL"
    transfer.layers_vgroup_select_dst = "NAME"
    bpy.ops.object.datalayout_transfer(modifier=transfer.name)
    bpy.ops.object.modifier_apply(modifier=transfer.name)
    weights = np.zeros((len(garment.data.vertices), len(TARGET_BONES)))
    for bone in rig.data.bones:
        target = FOLDED.get(bone.name, bone.name)
        if target in BONE:
            weights[:, BONE[target]] += group_values(garment, bone.name)
    unweighted = weights.sum(1) <= 1e-6
    weights[unweighted, BONE["spine"]] = 1.0
    print("skinned: %d points took no weight (given the spine)" % unweighted.sum())
    return weights


def share_armpits(weights, co, mesh, reach, above, passes):
    """Within reach of each shoulder joint and no more than above it: weights smoothed over passes, the torso and arm's shared part moved to the
    shoulder bone (the point turns about the shoulder instead of being pulled along the line between torso and arm: a pinched armpit)."""
    group = np.unique(np.round(co, 5), axis=0, return_inverse=True)[1].ravel()   # points split along UV seams: one group per place
    edges = np.empty(len(mesh.edges) * 2, dtype=np.int64)
    mesh.edges.foreach_get("vertices", edges)
    edges = group[edges.reshape(-1, 2)]
    edges = edges[edges[:, 0] != edges[:, 1]]
    groups = group.max() + 1
    gw = np.zeros((groups, len(TARGET_BONES)))
    np.add.at(gw, group, weights)
    gw /= np.bincount(group, minlength=groups)[:, None]
    gco = np.zeros((groups, 3))
    np.add.at(gco, group, co)
    gco /= np.bincount(group, minlength=groups)[:, None]
    degree = np.bincount(edges.ravel(), minlength=groups).astype(float)
    for side, joint in SHOULDER_JOINTS.items():
        near = (np.linalg.norm(gco - joint, axis=1) < reach) & (gco[:, 2] < joint[2] + above)
        for _ in range(passes):
            total = np.zeros_like(gw)
            np.add.at(total, edges[:, 0], gw[edges[:, 1]])
            np.add.at(total, edges[:, 1], gw[edges[:, 0]])
            mean = total / np.maximum(degree, 1.0)[:, None]
            gw[near] = 0.5 * gw[near] + 0.5 * mean[near]
        shared = np.where(near, np.minimum(gw[:, BONE["spine"]], gw[:, BONE["arm_" + side]]), 0.0)
        gw[:, BONE["spine"]] -= shared
        gw[:, BONE["arm_" + side]] -= shared
        gw[:, BONE["shoulder_" + side]] += 2.0 * shared
        print("armpit %s: %d points smoothed, %d share the half-turning bone" % (side, near.sum(), (shared > 0.01).sum()))
    return gw[group]


def lift_collar(weights, co, start, rise, reach, front):
    """From start up (front lower in front, under the chin), within reach of the neck's axis, the collar bone takes over over rise: it turns
    with the neck but goes where the head goes, so a head that dips along the neck (a sprint does, 3.4 cm) does not sink into it."""
    radius = np.hypot(co[:, 0], co[:, 1])
    ahead = np.clip(-co[:, 1] / np.maximum(radius, 1e-6), 0.0, 1.0)   # 1 straight in front (-y), 0 at the sides and behind
    up = np.clip((co[:, 2] - (start - front * ahead * ahead)) / rise, 0.0, 1.0)
    up = up * up * (3.0 - 2.0 * up) * (radius < reach)
    weights = weights * (1.0 - up)[:, None]
    weights[:, BONE["collar"]] += up
    print("collar: %d points on the collar bone" % (up > 0.5).sum())
    return weights


def write_garment(mesh, co, weights, path):
    """The BlenderGarmentMesh JSON: the game's frame (x mirrored, y up), triangles, normals, UVs, the four largest bone weights per point."""
    top = np.argsort(-weights, axis=1)[:, :4]
    top_w = np.take_along_axis(weights, top, 1)
    top_w /= np.maximum(top_w.sum(1, keepdims=True), 1e-6)
    loops = len(mesh.loops)
    loop_vertex = np.empty(loops, dtype=np.int64)
    mesh.loops.foreach_get("vertex_index", loop_vertex)
    loop_uv = np.empty(loops * 2, dtype=np.float32)
    mesh.uv_layers.active.data.foreach_get("uv", loop_uv)
    loop_uv = loop_uv.reshape(-1, 2)
    loop_normal = np.array([c.vector[:] for c in mesh.corner_normals], dtype=np.float32)
    keys = np.concatenate([loop_vertex[:, None].astype(np.float64), np.round(loop_uv, 5), np.round(loop_normal, 3)], 1)
    unique, first, corner_to_vertex = np.unique(keys, axis=0, return_index=True, return_inverse=True)
    corner_to_vertex = corner_to_vertex.ravel()
    src = loop_vertex[first]
    game = lambda v: np.stack([-v[:, 0], v[:, 2], -v[:, 1]], 1)   # Blender -> Unity
    positions = game(co[src])
    normals = game(loop_normal[first].astype(np.float64))
    triangles = []
    for poly in mesh.polygons:
        a, b, c = (corner_to_vertex[poly.loop_start + i] for i in range(3))
        triangles += [int(a), int(c), int(b)]   # the mirror turns the winding round
    data = {
        "positions": [round(float(x), 5) for x in positions.ravel()],
        "normals": [round(float(x), 4) for x in normals.ravel()],
        "uvs": [round(float(x), 5) for x in loop_uv[first].ravel()],
        "triangles": triangles,
        "boneIndices": [int(x) for x in top[src].ravel()],
        "boneWeights": [round(float(x), 4) for x in top_w[src].ravel()],
    }
    with open(path, "w") as f:
        json.dump(data, f, separators=(",", ":"))
    print("WROTE %s: %d vertices, %d triangles" % (path, len(positions), len(triangles) // 3))
