# THE MIDNIGHT COAT, headless (owner, 2026-10-19: "put it in the game and fit it to the character", named "Midnight Coat"):
#   blender --background --python coat_build.py -- <fitted .glb> <out .json> <out .jpg>
# MidnightCoat.glb is Meshy's "Midnight Shearling Coat" with the mannequin cut away, reduced to 14,876 triangles and fitted BY THE OWNER, in
# Blender, onto this game's body (../Octaslash/octaslash_rig.py's, arms hanging: the pose every garment is bound in). Its texture is one 512 JPEG.
# Nothing here moves the coat: it is only skinned and written out.
#   1. our body and skeleton (octaslash_rig.py), and the coat where the owner left it;
#   2. skinned from the body under it (../garment_rig.py, shared with the other garments): each point takes the weights of the nearest
#      skin, so the sleeves follow the arms and the coat's body the hips, spine and neck; the hands' share goes to the forearms (the cuffs),
#      the head's to the neck;
#   3. the skirt (the points nearest the legs) goes to the game's skirt bones (GarmentHelperBones): the front panels to the bone in front of
#      their thigh, which follows a knee coming forward, the back ones to the bone behind, which follows a leg going back, the sides to both;
#      so a knee never comes through the coat, and the skirt does not split between the legs;
#   4. round each armpit the weights are smoothed, and where torso and arm share a point, that share goes to the half-turning shoulder bone:
#      the point turns about the shoulder instead of being pulled along the straight line between torso and arm (a pinched, stretched armpit);
#   5. up the collar, the collar bone takes over: it turns with the neck but goes where the head goes, so a head the clips lower along the
#      neck (a sprint does, 3.4 cm) does not sink into it;
#   6. written for BlenderGarmentMesh: the game's frame (x mirrored, y up), triangles, normals, UVs, four bones per vertex; the texture as is.
import os
import sys

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
OCTASLASH = os.path.join(os.path.dirname(HERE), "Octaslash")
args = sys.argv[sys.argv.index("--") + 1:]
GLB, OUT_JSON, OUT_JPG = args[0], args[1], args[2]

ARMPIT_REACH = 0.24      # how far round each shoulder joint, and at most ARMPIT_ABOVE above it, the armpit is smoothed and shared
ARMPIT_ABOVE = 0.04      # with the half-turning bone: above the shoulders is the collar's
ARMPIT_SMOOTHING = 6     # passes of neighbour averaging there
COLLAR_FROM, COLLAR_RISE, COLLAR_REACH = 1.95, 0.11, 0.42   # the collar: from the shoulders' top up, within this of the neck's axis,
SKIRT_FROM, SKIRT_FULL = 0.95, 0.72   # the skirt bones take over from the hips from the hip joints' height down to here
COLLAR_FRONT = 0.12     # starting this much lower under the chin (the head dips forward into the front in a sprint)
LEGS = ("thigh_l", "thigh_r")


def run(path):
    exec(compile(open(path).read(), path, "exec"), globals())


# ---- 1. our body, and the coat as fitted ----
bpy.ops.wm.read_factory_settings(use_empty=True)
run(os.path.join(OCTASLASH, "octaslash_rig.py"))
run(os.path.join(os.path.dirname(HERE), "garment_rig.py"))
body, head, rig = bpy.data.objects["Body"], bpy.data.objects["Head"], bpy.data.objects["CityXRig"]
before = set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=GLB)
coat = [o for o in bpy.data.objects if o not in before and o.type == "MESH"][0]
bpy.ops.object.select_all(action="DESELECT")
coat.select_set(True)
bpy.context.view_layer.objects.active = coat
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
mesh = coat.data
co = mesh_coords(mesh)
print("coat: %d vertices, %d triangles, from %s to %s" % (len(co), len(mesh.polygons), co.min(0).round(3), co.max(0).round(3)))

# ---- 2. skinned from the body under it ----
weights = body_weights(coat, body, rig)

# ---- 3. the skirt onto the skirt bones ----
legs = sum(weights[:, BONE[b]] for b in LEGS)
for b in LEGS:
    weights[:, BONE[b]] = 0.0
# Below the hips the skirt hangs from the skirt bones however the body under it was weighted (its front took the hips' weights there).
below = np.clip((SKIRT_FROM - co[:, 2]) / (SKIRT_FROM - SKIRT_FULL), 0.0, 1.0)
below = below * below * (3.0 - 2.0 * below)
legs += weights[:, BONE["hips"]] * below
weights[:, BONE["hips"]] *= 1.0 - below
left = np.clip((co[:, 0] + 0.12) / 0.24, 0.0, 1.0)   # Blender +x is the body's left, -y its front
front = 0.5 - 0.5 * co[:, 1] / np.maximum(np.hypot(co[:, 0], co[:, 1]), 1e-6)
for side, share in (("l", left), ("r", 1.0 - left)):
    weights[:, BONE["skirt_front_" + side]] += legs * share * front
    weights[:, BONE["skirt_back_" + side]] += legs * share * (1.0 - front)
print("skirt: %d points on the skirt bones" % (legs > 0.01).sum())

# ---- 4. the armpits; 5. the collar ----
weights = share_armpits(weights, co, mesh, ARMPIT_REACH, ARMPIT_ABOVE, ARMPIT_SMOOTHING)
weights = lift_collar(weights, co, COLLAR_FROM, COLLAR_RISE, COLLAR_REACH, COLLAR_FRONT)

# ---- 6. written for the game ----
write_garment(mesh, co, weights, OUT_JSON)
image = next(n.image for m in mesh.materials for n in m.node_tree.nodes if n.type == "TEX_IMAGE" and n.image)
with open(OUT_JPG, "wb") as f:
    f.write(image.packed_file.data)
print("WROTE %s: %d x %d" % (OUT_JPG, *image.size))
