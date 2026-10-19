# THE MIDNIGHT COAT, headless (owner, 2026-10-19: "put it in the game and fit it to the character", named "Midnight Coat"):
#   blender --background --python coat_build.py -- <fitted .glb> <out .json> <out .jpg>
# MidnightCoat.glb is Meshy's "Midnight Shearling Coat" with the mannequin cut away, reduced to 14,876 triangles and fitted BY THE OWNER, in
# Blender, onto this game's body (../Octaslash/octaslash_rig.py's, arms hanging: the pose every garment is bound in). Its texture is one 512 JPEG.
# Nothing here moves the coat: it is only skinned and written out.
#   1. our body and skeleton (octaslash_rig.py), and the coat where the owner left it;
#   2. skinned from the body under it: each point takes the weights of the nearest skin, so the sleeves follow the arms and the coat's body the
#      hips, spine and neck; the hands' share goes to the forearms (the cuffs), the head's to the neck;
#   3. the skirt (the points nearest the legs) goes to the game's skirt bones (GarmentHelperBones): the front panels to the bone in front of
#      their thigh, which follows a knee coming forward, the back ones to the bone behind, which follows a leg going back, the sides to both;
#      so a knee never comes through the coat, and the skirt does not split between the legs;
#   4. round each armpit the weights are smoothed, and where torso and arm share a point, that share goes to the half-turning shoulder bone:
#      the point turns about the shoulder instead of being pulled along the straight line between torso and arm (a pinched, stretched armpit);
#   5. up the collar, the collar bone takes over: it turns with the neck but goes where the head goes, so a head the clips lower along the
#      neck (a sprint does, 3.4 cm) does not sink into it;
#   6. written for BlenderGarmentMesh: the game's frame (x mirrored, y up), triangles, normals, UVs, four bones per vertex; the texture as is.
import json
import os
import sys

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
OCTASLASH = os.path.join(os.path.dirname(HERE), "Octaslash")
args = sys.argv[sys.argv.index("--") + 1:]
GLB, OUT_JSON, OUT_JPG = args[0], args[1], args[2]

SHOULDERS = {"l": np.array([0.35, 0.0, 1.9]), "r": np.array([-0.35, 0.0, 1.9])}   # BodyRig's shoulder joints (Blender +x is the body's left)
ARMPIT_REACH = 0.24      # how far round each shoulder joint, and at most ARMPIT_ABOVE above it, the armpit is smoothed and shared
ARMPIT_ABOVE = 0.04      # with the half-turning bone: above the shoulders is the collar's
ARMPIT_SMOOTHING = 6     # passes of neighbour averaging there
COLLAR_FROM, COLLAR_RISE, COLLAR_REACH = 1.95, 0.11, 0.42   # the collar: from the shoulders' top up, within this of the neck's axis,
SKIRT_FROM, SKIRT_FULL = 0.95, 0.72   # the skirt bones take over from the hips from the hip joints' height down to here
COLLAR_FRONT = 0.12     # starting this much lower under the chin (the head dips forward into the front in a sprint)

# The game's bone order (SkinnedGarment.Bones, then GarmentHelperBones.Helpers / BlenderGarmentMesh.TargetBoneNames).
TARGET_BONES = ["hips", "spine", "neck", "head", "arm_l", "forearm_l", "hand_l", "arm_r", "forearm_r", "hand_r",
                "thigh_l", "shin_l", "thigh_r", "shin_r",
                "skirt_front_l", "skirt_back_l", "skirt_front_r", "skirt_back_r", "shoulder_l", "shoulder_r", "collar"]
# The skeleton's other bones and the coat bone each one's share goes to (a coat never follows a hand, the head or a shin).
FOLDED = {"hand_l": "forearm_l", "fingertip_l": "forearm_l", "hand_r": "forearm_r", "fingertip_r": "forearm_r", "head": "neck",
          "shin_l": "thigh_l", "foot_l": "thigh_l", "shin_r": "thigh_r", "foot_r": "thigh_r"}
LEGS = ("thigh_l", "thigh_r")


def run(path):
    exec(compile(open(path).read(), path, "exec"), globals())


def coords(me):
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


# ---- 1. our body, and the coat as fitted ----
bpy.ops.wm.read_factory_settings(use_empty=True)
run(os.path.join(OCTASLASH, "octaslash_rig.py"))
body, head, rig = bpy.data.objects["Body"], bpy.data.objects["Head"], bpy.data.objects["CityXRig"]
before = set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=GLB)
coat = [o for o in bpy.data.objects if o not in before and o.type == "MESH"][0]
bpy.ops.object.select_all(action="DESELECT")
coat.select_set(True)
bpy.context.view_layer.objects.active = coat
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
mesh = coat.data
co = coords(mesh)
print("coat: %d vertices, %d triangles, from %s to %s" % (len(co), len(mesh.polygons), co.min(0).round(3), co.max(0).round(3)))

# ---- 2. skinned from the body under it ----
transfer = coat.modifiers.new("FromBody", "DATA_TRANSFER")
transfer.object = body
transfer.use_vert_data = True
transfer.data_types_verts = {"VGROUP_WEIGHTS"}
transfer.vert_mapping = "POLYINTERP_NEAREST"
transfer.layers_vgroup_select_src = "ALL"
transfer.layers_vgroup_select_dst = "NAME"
bpy.ops.object.datalayout_transfer(modifier=transfer.name)
bpy.ops.object.modifier_apply(modifier=transfer.name)
index = {b: i for i, b in enumerate(TARGET_BONES)}
weights = np.zeros((len(co), len(TARGET_BONES)))
for bone in rig.data.bones:
    target = FOLDED.get(bone.name, bone.name)
    if target in index:
        weights[:, index[target]] += group_values(coat, bone.name)
unweighted = weights.sum(1) <= 1e-6
weights[unweighted, index["spine"]] = 1.0
print("skinned: %d points took no weight (given the spine)" % unweighted.sum())

# ---- 3. the skirt onto the skirt bones ----
legs = sum(weights[:, index[b]] for b in LEGS)
for b in LEGS:
    weights[:, index[b]] = 0.0
# Below the hips the skirt hangs from the skirt bones however the body under it was weighted (its front took the hips' weights there).
below = np.clip((SKIRT_FROM - co[:, 2]) / (SKIRT_FROM - SKIRT_FULL), 0.0, 1.0)
below = below * below * (3.0 - 2.0 * below)
legs += weights[:, index["hips"]] * below
weights[:, index["hips"]] *= 1.0 - below
left = np.clip((co[:, 0] + 0.12) / 0.24, 0.0, 1.0)   # Blender +x is the body's left, -y its front
front = 0.5 - 0.5 * co[:, 1] / np.maximum(np.hypot(co[:, 0], co[:, 1]), 1e-6)
for side, share in (("l", left), ("r", 1.0 - left)):
    weights[:, index["skirt_front_" + side]] += legs * share * front
    weights[:, index["skirt_back_" + side]] += legs * share * (1.0 - front)
print("skirt: %d points on the skirt bones" % (legs > 0.01).sum())

# ---- 4. the armpits ----
group = np.unique(np.round(co, 5), axis=0, return_inverse=True)[1].ravel()   # the import splits points along UV seams: one group per place
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
for side, joint in SHOULDERS.items():
    near = (np.linalg.norm(gco - joint, axis=1) < ARMPIT_REACH) & (gco[:, 2] < joint[2] + ARMPIT_ABOVE)
    for _ in range(ARMPIT_SMOOTHING):
        total = np.zeros_like(gw)
        np.add.at(total, edges[:, 0], gw[edges[:, 1]])
        np.add.at(total, edges[:, 1], gw[edges[:, 0]])
        mean = total / np.maximum(degree, 1.0)[:, None]
        gw[near] = 0.5 * gw[near] + 0.5 * mean[near]
    shared = np.where(near, np.minimum(gw[:, index["spine"]], gw[:, index["arm_" + side]]), 0.0)
    gw[:, index["spine"]] -= shared
    gw[:, index["arm_" + side]] -= shared
    gw[:, index["shoulder_" + side]] += 2.0 * shared
    print("armpit %s: %d points smoothed, %d share the half-turning bone" % (side, near.sum(), (shared > 0.01).sum()))
weights = gw[group]

# ---- 5. the collar ----
radius = np.hypot(co[:, 0], co[:, 1])
ahead = np.clip(-co[:, 1] / np.maximum(radius, 1e-6), 0.0, 1.0)   # 1 straight in front (-y), 0 at the sides and behind
rise = np.clip((co[:, 2] - (COLLAR_FROM - COLLAR_FRONT * ahead * ahead)) / COLLAR_RISE, 0.0, 1.0)
rise = rise * rise * (3.0 - 2.0 * rise) * (radius < COLLAR_REACH)
weights *= (1.0 - rise)[:, None]
weights[:, index["collar"]] += rise
print("collar: %d points on the collar bone" % (rise > 0.5).sum())
top = np.argsort(-weights, axis=1)[:, :4]
top_w = np.take_along_axis(weights, top, 1)
top_w /= np.maximum(top_w.sum(1, keepdims=True), 1e-6)

# ---- 6. written for the game ----
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
with open(OUT_JSON, "w") as f:
    json.dump(data, f, separators=(",", ":"))
image = next(n.image for m in mesh.materials for n in m.node_tree.nodes if n.type == "TEX_IMAGE" and n.image)
with open(OUT_JPG, "wb") as f:
    f.write(image.packed_file.data)
print("WROTE %s: %d vertices, %d triangles; %s: %d x %d" % (OUT_JSON, len(positions), len(triangles) // 3, OUT_JPG, *image.size))
