# THE BLACK JEANS, SKINNED FOR THE GAME, headless:
#   blender --background --factory-startup --python black_jeans_build.py -- BlackJeans.glb black_jeans_color.jpg <out .json> <out .jpg>
# BlackJeans.glb is the jeans fitted onto this game's body (black_jeans_fit.py, then whatever the owner adjusts in Blender on
# ../Octaslash/octaslash_rig.py's body, arms hanging: the pose every garment is bound in). Nothing here moves them: they are only skinned and
# written out.
#   1. our body and skeleton, and the jeans where the fit left them;
#   2. skinned from the body under them (../garment_rig.py): each point takes the weights of the nearest skin, so each leg follows its thigh
#      and, below the knee, its shin (trousers keep the shins a coat or a shirt folds into the thighs), and the waist the hips; the feet's share
#      goes to the shins (the hems); the waist shares the spine as the skin under it does; nothing follows an arm, a hand or the head (the
#      hands hang beside the hips): what did goes to the hips; then each point's weights averaged with its neighbours' (taken from the
#      nearest skin, they jumped across the buttocks' crease from the hips to the thigh and shin, and a bent knee tore the cloth there);
#      each leg split between its thigh and its shin by height, round the knee (the body blends the shin far up the thigh, which swung
#      the back of the thigh out of the rest when the knee bent);
#      the crotch's middle follows the hips in part (pulled half toward each leg, a stride stretched it seven times over);
#      the waistband follows the hips alone, as the shirt's hem over it does (a leg swinging back pulled the back of it out through the
#      shirt);
#   3. written for BlenderGarmentMesh, with the texture at TEXTURE_SIZE.
import os
import sys

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
OCTASLASH = os.path.join(os.path.dirname(HERE), "Octaslash")
args = sys.argv[sys.argv.index("--") + 1:]
GLB, TEXTURE, OUT_JSON, OUT_JPG = (os.path.abspath(a) for a in args[:4])

TEXTURE_SIZE = 1024   # tools/model_qa.py warns past 1024; the stitching still reads at it
JPEG_QUALITY = 90
WEIGHT_SMOOTHING = 30   # passes of neighbour averaging over the whole of them
KNEE_BLEND = 0.06   # each leg goes from its thigh to its shin over this either side of the knee (KNEE_Y, octaslash_rig.py's)
CROTCH_REACH, CROTCH_HIPS = 0.08, 0.6   # the crotch: this far either side of the middle, up to this share given to the hips
CROTCH_HEIGHTS = ((0.45, 0.55), (0.75, 0.85))   # from the first height up (wholly from the second), to the third (none past the fourth)
WAIST_FROM, WAIST_FULL = 0.86, 0.94   # the thighs' share goes to the hips from here up, wholly above WAIST_FULL (the waistband)
NOT_TROUSERS = ("neck", "head", "arm_l", "forearm_l", "hand_l", "arm_r", "forearm_r", "hand_r")


def run(path):
    exec(compile(open(path).read(), path, "exec"), globals())


# ---- 1. our body, and the jeans as fitted ----
bpy.ops.wm.read_factory_settings(use_empty=True)
run(os.path.join(OCTASLASH, "octaslash_rig.py"))
run(os.path.join(os.path.dirname(HERE), "garment_rig.py"))
body, rig = bpy.data.objects["Body"], bpy.data.objects["CityXRig"]
before = set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=GLB)
jeans = [o for o in bpy.data.objects if o not in before and o.type == "MESH"][0]
bpy.ops.object.select_all(action="DESELECT")
jeans.select_set(True)
bpy.context.view_layer.objects.active = jeans
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
mesh = jeans.data
co = mesh_coords(mesh)
print("jeans: %d points, %d triangles, from %s to %s" % (len(co), len(mesh.polygons), co.min(0).round(3), co.max(0).round(3)))

# ---- 2. skinned from the body under them ----
weights = body_weights(jeans, body, rig, TROUSERS_FOLDED)
stray = sum(weights[:, BONE[b]] for b in NOT_TROUSERS)
for b in NOT_TROUSERS:
    weights[:, BONE[b]] = 0.0
weights[:, BONE["hips"]] += stray
print("hips: %d points took over a share of an arm or the head" % (stray > 0.01).sum())
weights = smooth_weights(weights, co, mesh, WEIGHT_SMOOTHING)
# Each leg's share split between thigh and shin by height, over KNEE_BLEND either side of the knee: the body blends the shin far up the thigh
# (a third of it 12 cm above the knee), which swung the cloth on the back of the thigh, far behind the bone, out of the rest.
below = np.clip((KNEE_Y + KNEE_BLEND - co[:, 2]) / (2.0 * KNEE_BLEND), 0.0, 1.0)
below = below * below * (3.0 - 2.0 * below)
for side in ("l", "r"):
    leg = weights[:, BONE["thigh_" + side]] + weights[:, BONE["shin_" + side]]
    weights[:, BONE["thigh_" + side]] = leg * (1.0 - below)
    weights[:, BONE["shin_" + side]] = leg * below
# The crotch's middle partly follows the hips: between the legs, pulled half each way by a stride, it stretched seven times over.
middle = 1.0 - np.clip(np.abs(co[:, 0]) / CROTCH_REACH, 0.0, 1.0)
middle = middle * middle * (3.0 - 2.0 * middle)
low, high = CROTCH_HEIGHTS
height = np.clip(np.minimum(co[:, 2] - low[0], high[1] - co[:, 2]) / (low[1] - low[0]), 0.0, 1.0)
crotch = CROTCH_HIPS * middle * height
for b in ("thigh_l", "shin_l", "thigh_r", "shin_r"):
    weights[:, BONE["hips"]] += weights[:, BONE[b]] * crotch
    weights[:, BONE[b]] *= 1.0 - crotch
print("crotch: %d points follow the hips more than half" % (crotch > 0.5).sum())
# The waistband moves as the shirt's hem over it does (with the hips): a leg swinging back pulled its thigh's share out through the shirt.
band = np.clip((co[:, 2] - WAIST_FROM) / (WAIST_FULL - WAIST_FROM), 0.0, 1.0)
band = band * band * (3.0 - 2.0 * band)
for b in ("thigh_l", "thigh_r"):
    weights[:, BONE["hips"]] += weights[:, BONE[b]] * band
    weights[:, BONE[b]] *= 1.0 - band
print("waistband: %d points on the hips alone" % (band > 0.99).sum())
for side in ("l", "r"):
    print("leg %s: %d points on the thigh, %d on the shin" % (side, (weights[:, BONE["thigh_" + side]] > 0.5).sum(),
                                                             (weights[:, BONE["shin_" + side]] > 0.5).sum()))

# ---- 3. written for the game ----
write_garment(mesh, co, weights, OUT_JSON)
image = bpy.data.images.load(TEXTURE)
image.scale(TEXTURE_SIZE, TEXTURE_SIZE)
settings = bpy.context.scene.render.image_settings
settings.file_format, settings.color_mode, settings.quality = "JPEG", "RGB", JPEG_QUALITY
image.save_render(OUT_JPG, scene=bpy.context.scene)
print("WROTE %s: %d x %d, %d bytes" % (OUT_JPG, TEXTURE_SIZE, TEXTURE_SIZE, os.path.getsize(OUT_JPG)))
