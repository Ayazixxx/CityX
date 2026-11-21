# SKINNING A GARMENT ONTO THIS GAME'S BODY, shared by the garments' Blender scripts (MidnightCoat/coat_build.py, Parka/parka_build.py,
# TShirt/tshirt_build.py, BlackJeans/black_jeans_build.py).
# Load it into a script's globals with exec(), after Octaslash/octaslash_rig.py has built the body ("Body") and skeleton ("CityXRig").
#   body_weights:   each point takes the weights of the nearest skin; hands go to the forearms, the head to the neck, shins and feet to the thighs
#                   (trousers: the feet to the shins);
#   smooth_weights: every point's weights averaged with its neighbours' (trousers: no jump across the buttocks' crease);
#   share_armpits:  round each armpit the weights are smoothed and what torso and arm share goes to the half-turning shoulder bone (the
#                   coat; the T-Shirt cuts its sleeves loose instead);
#   hang_skirt:     a long coat's skirt goes to the skirt bones, front panels and back panels apart;
#   sleeves_only:   a coat with sleeves cut loose from its body: only the sleeves follow the arms (the parka);
#   lift_collar:    up a collar the collar bone (the neck's turn, the head's place) takes over;
#   write_garment:  the BlenderGarmentMesh JSON, in the game's frame, four bones per point.
import json

import bpy
import numpy as np

SHOULDER_JOINTS = {"l": np.array([0.35, 0.0, 1.9]), "r": np.array([-0.35, 0.0, 1.9])}   # BodyRig's (Blender +x is the body's left)
HIPS_UP_TO = (1.0, 1.2)   # the body's share a coat's own cloth takes from an arm goes to the hips below the first height, the spine above the second

# The game's bone order (SkinnedGarment.Bones, then GarmentHelperBones.Helpers / BlenderGarmentMesh.TargetBoneNames).
TARGET_BONES = ["hips", "spine", "neck", "head", "arm_l", "forearm_l", "hand_l", "arm_r", "forearm_r", "hand_r",
                "thigh_l", "shin_l", "thigh_r", "shin_r",
                "skirt_front_l", "skirt_back_l", "skirt_front_r", "skirt_back_r", "shoulder_l", "shoulder_r", "collar"]
BONE = {b: i for i, b in enumerate(TARGET_BONES)}
# The skeleton's other bones and the garment bone each one's share goes to (a garment never follows a hand, the head or a shin).
FOLDED = {"hand_l": "forearm_l", "fingertip_l": "forearm_l", "hand_r": "forearm_r", "fingertip_r": "forearm_r", "head": "neck",
          "shin_l": "thigh_l", "foot_l": "thigh_l", "shin_r": "thigh_r", "foot_r": "thigh_r"}
# Trousers bend at the knee: the shins keep their own share, the feet go to the shins (the hems).
TROUSERS_FOLDED = {"hand_l": "forearm_l", "fingertip_l": "forearm_l", "hand_r": "forearm_r", "fingertip_r": "forearm_r", "head": "neck",
                   "foot_l": "shin_l", "foot_r": "shin_r"}


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


def body_weights(garment, body, rig, folded=FOLDED):
    """One row per point of garment, one column per TARGET_BONES: the weights of the body's skin nearest it, the bones in folded given to
    the bone they name (trousers keep their shins: TROUSERS_FOLDED)."""
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
        target = folded.get(bone.name, bone.name)
        if target in BONE:
            weights[:, BONE[target]] += group_values(garment, bone.name)
    unweighted = weights.sum(1) <= 1e-6
    weights[unweighted, BONE["spine"]] = 1.0
    print("skinned: %d points took no weight (given the spine)" % unweighted.sum())
    return weights


def places(weights, co, mesh):
    """The garment's points merged where they are split along UV seams (one place each): which place each point is, the places' edges, their
    weights and positions (averaged), and how many edges meet at each."""
    group = np.unique(np.round(co, 5), axis=0, return_inverse=True)[1].ravel()
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
    return group, edges, gw, gco, degree


def smooth_places(gw, edges, degree, which, passes):
    """The weights of the places in which averaged with their neighbours', passes times."""
    for _ in range(passes):
        total = np.zeros_like(gw)
        np.add.at(total, edges[:, 0], gw[edges[:, 1]])
        np.add.at(total, edges[:, 1], gw[edges[:, 0]])
        mean = total / np.maximum(degree, 1.0)[:, None]
        gw[which] = 0.5 * gw[which] + 0.5 * mean[which]


def smooth_weights(weights, co, mesh, passes):
    """Every point's weights averaged with its neighbours' over passes: no jump where the nearest skin changes from one side of a crease to the
    other (trousers at the buttocks: hips on one point, thigh and shin on the next, torn apart by a bent knee)."""
    group, edges, gw, _, degree = places(weights, co, mesh)
    smooth_places(gw, edges, degree, np.ones(len(gw), dtype=bool), passes)
    return gw[group]


def share_armpits(weights, co, mesh, reach, above, passes):
    """Within reach of each shoulder joint and no more than above it: weights smoothed over passes, the torso and arm's shared part moved to the
    shoulder bone (the point turns about the shoulder instead of being pulled along the line between torso and arm: a pinched armpit)."""
    group, edges, gw, gco, degree = places(weights, co, mesh)
    for side, joint in SHOULDER_JOINTS.items():
        near = (np.linalg.norm(gco - joint, axis=1) < reach) & (gco[:, 2] < joint[2] + above)
        smooth_places(gw, edges, degree, near, passes)
        shared = np.where(near, np.minimum(gw[:, BONE["spine"]], gw[:, BONE["arm_" + side]]), 0.0)
        gw[:, BONE["spine"]] -= shared
        gw[:, BONE["arm_" + side]] -= shared
        gw[:, BONE["shoulder_" + side]] += 2.0 * shared
        print("armpit %s: %d points smoothed, %d share the half-turning bone" % (side, near.sum(), (shared > 0.01).sum()))
    return gw[group]


def hang_skirt(weights, co, start, full, legs=("thigh_l", "thigh_r")):
    """A long coat's skirt onto the game's skirt bones (GarmentHelperBones): what the nearest skin gave to the legs, and from start down
    to full the hips' share too (the body's own weights give a coat's front panels mostly to the hips, and a knee went through them). The
    front panels go to the bone in front of their thigh, which follows a knee coming forward, the back ones to the bone behind, the sides
    to both: a knee never comes through the coat, and the skirt does not split between the legs."""
    swing = sum(weights[:, BONE[b]] for b in legs)
    for b in legs:
        weights[:, BONE[b]] = 0.0
    below = np.clip((start - co[:, 2]) / (start - full), 0.0, 1.0)
    below = below * below * (3.0 - 2.0 * below)
    swing += weights[:, BONE["hips"]] * below
    weights[:, BONE["hips"]] *= 1.0 - below
    left = np.clip((co[:, 0] + 0.12) / 0.24, 0.0, 1.0)   # Blender +x is the body's left, -y its front
    front = 0.5 - 0.5 * co[:, 1] / np.maximum(np.hypot(co[:, 0], co[:, 1]), 1e-6)
    for side, share in (("l", left), ("r", 1.0 - left)):
        weights[:, BONE["skirt_front_" + side]] += swing * share * front
        weights[:, BONE["skirt_back_" + side]] += swing * share * (1.0 - front)
    print("skirt: %d points on the skirt bones" % (swing > 0.01).sum())
    return weights


def sleeves_only(weights, co, mesh, arms_from):
    """A coat whose sleeves are cut loose from its body says which points are a sleeve's in its second set of texture coordinates (u = 1).
    The rest is the coat's own cloth and does not follow an arm: its sides hang as near the forearms as the hips, and the nearest skin alone
    would give them to the arms. Below arms_from[0] it gives its arms' share to the body: the spine at the chest, the hips from the waist
    down (so a long coat's sides then hang with the skirt, hang_skirt, and not with the chest: given to the spine, they twisted with a
    melee swing and the thigh went through them); from arms_from[1] up it keeps it, round the shoulder, where sleeve and coat are one skin."""
    flag = np.zeros(len(mesh.loops) * 2, dtype=np.float32)
    mesh.uv_layers[1].data.foreach_get("uv", flag)
    corner = np.empty(len(mesh.loops), dtype=np.int64)
    mesh.loops.foreach_get("vertex_index", corner)
    sleeve = np.zeros(len(co))
    sleeve[corner] = flag.reshape(-1, 2)[:, 0]
    keep = np.clip((co[:, 2] - arms_from[0]) / (arms_from[1] - arms_from[0]), 0.0, 1.0)
    keep = np.where(sleeve > 0.5, 1.0, keep * keep * (3.0 - 2.0 * keep))
    hips = np.clip((HIPS_UP_TO[1] - co[:, 2]) / (HIPS_UP_TO[1] - HIPS_UP_TO[0]), 0.0, 1.0)
    hips = hips * hips * (3.0 - 2.0 * hips)
    handed_over = 0.0
    for bone in ("arm_l", "forearm_l", "arm_r", "forearm_r"):
        given = weights[:, BONE[bone]] * (1.0 - keep)
        weights[:, BONE[bone]] -= given
        weights[:, BONE["hips"]] += given * hips
        weights[:, BONE["spine"]] += given * (1.0 - hips)
        handed_over += given.sum()
    print("sleeves: %d points are a sleeve's; the coat's own cloth gave %.0f points' worth of arm to the body" % ((sleeve > 0.5).sum(), handed_over))
    return weights


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
