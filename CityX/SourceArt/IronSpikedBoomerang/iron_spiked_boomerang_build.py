# THE IRON SPIKED GIANT BOOMERANG, headless (owner, 2026-11-19, with Meshy's "Twin Fang Crescent": "make this another giant boomerang weapon
# called "Iron Spiked Giant Boomerang" make it the roughly the same size as the other giant boomerang and shrink the y axis to roughly the same
# size as the giant boomerang as well ... also lower the face count to a reasonable amount"):
#   blender --background --factory-startup --python iron_spiked_boomerang_build.py -- <Meshy .fbx> <its colour .png>
#                                                                                     <out .glb> <out .jpg> <out .points.json>
# Meshy's model: a steel crescent with spiked blades at both ends and a leather wrap on each arm, one surface of 484,150 triangles, 2.03 m tip
# to tip and 31 cm thick (its knotted wraps), lying in Meshy's x-z plane, four 4096/2048 maps.
#   1. Laid as the game's thrown giant boomerang lies (BoomerangBuilder): flat, its face up, its middle (what it spins about) at the origin;
#      as big as the Giant Boomerang: its two arms, elbow to end, as long as the bone one's (BONE_ARMS; owner, seeing it on the back: "make it
#      the same size that the giant boomerang is now", "scale it down slightly so its the same size as the other giant boomerang i feel like it
#      a bit bigger": with the tips as far apart as the bone one's, its curled arms were a third longer), and pressed thin to about its
#      thickness (THICK: the bone one's blade with its leather wraps).
#   2. Turned over if need be so that, held by the grip with the arm running from the hand to the elbow, the other arm goes off to the hand's
#      right: on the back the crescent's hollow and its spikes then face the body's left, away from the camera over the right shoulder (owner,
#      2026-11-19: "flip it so its facing the other way so the spikes dont block your view when playing"; the bone one's other arm goes the
#      other way). The held arm lies along the bone one's, so it hangs across the back at the bone one's slant.
#   3. Cut to TRIANGLES; unwrapped, and Meshy's colour baked across onto one small map.
#   4. Written: the model (one mesh "IronSpikedBoomerang"), its map, and its points (the grip on the upper arm's wrap, the elbow, the two ends,
#      the arms' half widths) in the game's frame, for BoomerangBuilder.BuildIronSpiked.
# The game's frame (Unity: x right, y up, z ahead) goes into Blender as (-x, -z, y), as every gun here does (Firearms/firearm_parts.py U()).
import json
import os
import sys

import bpy
import numpy as np

args = sys.argv[sys.argv.index("--") + 1:]
FBX, COLOUR, OUT_GLB, OUT_JPG, OUT_POINTS = (os.path.abspath(a) for a in args[:5])

BONE_ARMS = 2.8                  # the Giant Boomerang's two arms, elbow to tip (BoomerangBuilder.GiantShape: 1.4 m each)
THICK = 0.13                     # its blade (0.11) and its leather wraps standing proud of it
TRIANGLES = 6000                 # tools/model_qa.py's weapon budget: 6k fine, 10k most
TEXTURE_SIZE = 1024
JPEG_QUALITY = 88
REACH = 0.02                     # how far the reduced model may lie from Meshy's when the colour is baked across (metres)
MATERIAL = "IronSpikedBoomerang"

# Picked on Meshy's own model (its x across, its z up, metres, seen from -y): the grip on the upper arm's wrap, between its two inner spikes
# and toward the blade (the bone one is held near an end, its length running from the hand to the elbow); the elbow where the two arms'
# middle lines meet; each arm's end.
GRIP_Z = 0.34
UPPER_ARM_Z = -0.10              # the upper arm's middle line is read here (below the wrap's spikes, above the elbow)
LOWER_ARM_X = -0.20              # the lower arm's, here (below the middle: above it the column meets the upper blade)
UPPER_END_Z = 0.70               # the upper arm's end: its points above this
LOWER_END_X = 0.55               # the lower arm's: its points right of this
SPIKES_FROM = -0.30              # the upper arm's spikes are on its inner (right) side: its middle is read from its points left of this


def only(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[-1]


def to_blender(u):
    """The game's frame to Blender's."""
    return np.stack([-u[:, 0], -u[:, 2], u[:, 1]], axis=1)


def middle_across(points, along, at, across, keep=None):
    """The middle of the points within 2 cm of `at` along axis `along`, across axis `across`."""
    near = np.abs(points[:, along] - at) < 0.02
    if keep is not None:
        near &= keep
    values = points[near, across]
    return (values.min() + values.max()) * 0.5, (values.max() - values.min()) * 0.5


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
only(meshes)
if len(meshes) > 1:
    bpy.ops.object.join()
high = bpy.context.view_layer.objects.active
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = high.data
if me.has_custom_normals:
    bpy.ops.mesh.customdata_custom_splitnormals_clear()
for name in ("sharp_edge", "sharp_face"):
    if name in me.attributes:
        me.attributes.remove(me.attributes[name])
print("in: %d triangles" % sum(len(p.vertices) - 2 for p in me.polygons))
meshy = np.array([v.co[:] for v in me.vertices])

# The points, picked on Meshy's own (x, z): the upper arm runs up the left, the lower arm along the bottom.
upper_x, upper_half = middle_across(meshy, 2, UPPER_ARM_Z, 0)
grip_x, _ = middle_across(meshy, 2, GRIP_Z, 0, keep=meshy[:, 0] < SPIKES_FROM)
lower_z, lower_half = middle_across(meshy, 0, LOWER_ARM_X, 2, keep=meshy[:, 2] < 0.0)
picked = {
    "grip": np.array([grip_x, 0.0, GRIP_Z]),
    "elbow": np.array([upper_x, 0.0, lower_z]),
    "upper_end": np.r_[meshy[meshy[:, 2] > UPPER_END_Z][:, [0]].mean(), 0.0, meshy[meshy[:, 2] > UPPER_END_Z][:, 2].mean()],
    "lower_end": np.r_[meshy[meshy[:, 0] > LOWER_END_X][:, 0].mean(), 0.0, meshy[meshy[:, 0] > LOWER_END_X][:, [2]].mean()],
}
print("picked on Meshy's model:", {k: v.round(3).tolist() for k, v in picked.items()}, "half widths", round(upper_half, 3), round(lower_half, 3))

# 1. Its size: its arms, elbow to each end, as long as the bone one's.
arms = np.linalg.norm(picked["upper_end"] - picked["elbow"]) + np.linalg.norm(picked["lower_end"] - picked["elbow"])
scale = BONE_ARMS / arms
press = THICK / ((meshy[:, 1].max() - meshy[:, 1].min()) * scale)
print("arms %.3f m -> %.2f m (x %.3f); %.3f m thick -> %.3f m (pressed x %.3f)" % (arms, BONE_ARMS, scale, meshy[:, 1].max() - meshy[:, 1].min(),
                                                                                    THICK, press))

# The game's frame: Meshy's x across, its thin y up (the face), its z ahead; scaled, pressed, its middle (by area) at the origin.
def to_game(p):
    return np.stack([p[:, 0] * scale, p[:, 1] * scale * press, p[:, 2] * scale], axis=1)


game = to_game(meshy)
tri_area, tri_centre = [], []
for poly in me.polygons:
    v = game[list(poly.vertices)]
    tri_area.append(0.5 * np.linalg.norm(np.cross(v[1] - v[0], v[2] - v[0])))
    tri_centre.append(v.mean(0))
tri_area = np.array(tri_area)
middle = (np.array(tri_centre) * tri_area[:, None]).sum(0) / tri_area.sum()
middle[1] = 0.0
game -= middle
points = {k: to_game(v[None, :])[0] - middle for k, v in picked.items()}

# 2. Turned over if the other arm would go off to the hand's left (see the top).
toward = points["elbow"] - points["grip"]
toward /= np.linalg.norm(toward)
right = np.array([toward[2], 0.0, -toward[0]])            # Unity's cross(up, toward)
if np.dot(right, points["lower_end"] - points["grip"]) < 0.0:
    flip = np.array([-1.0, -1.0, 1.0])
    game *= flip
    points = {k: v * flip for k, v in points.items()}
    print("turned over")
me.vertices.foreach_set("co", to_blender(game).ravel())
me.update()

# 3. Cut, unwrapped, Meshy's colour baked across.
only([high])
bpy.ops.object.duplicate()
low = bpy.context.view_layer.objects.active
low.name = MATERIAL
mod = low.modifiers.new("Reduce", "DECIMATE")
mod.decimate_type = "COLLAPSE"
mod.ratio = TRIANGLES / max(1, len(low.data.polygons))
mod.use_collapse_triangulate = True
only([low])
bpy.ops.object.modifier_apply(modifier="Reduce")
print("reduced: %d triangles" % len(low.data.polygons))
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.003, area_weight=1.0, scale_to_bounds=True)
bpy.ops.uv.average_islands_scale()
bpy.ops.uv.pack_islands(rotate=True, margin=0.003)
bpy.ops.object.mode_set(mode="OBJECT")
low.data.shade_smooth()
low.data.set_sharp_from_angle(angle=np.radians(40.0))

image = bpy.data.images.load(COLOUR)
scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.samples = 4
scene.cycles.device = "CPU"
scene.render.bake.use_selected_to_active = True
scene.render.bake.cage_extrusion = REACH
scene.render.bake.max_ray_distance = REACH * 2.0
scene.render.bake.margin = 8
shown = bpy.data.materials.new("Shown")
shown.use_nodes = True
nodes = shown.node_tree.nodes
nodes.clear()
source = nodes.new("ShaderNodeTexImage")
source.image = image
emit = nodes.new("ShaderNodeEmission")
out = nodes.new("ShaderNodeOutputMaterial")
shown.node_tree.links.new(source.outputs["Color"], emit.inputs["Color"])
shown.node_tree.links.new(emit.outputs["Emission"], out.inputs["Surface"])
high.data.materials.clear()
high.data.materials.append(shown)
target = bpy.data.images.new("IronSpikedBoomerangColour", TEXTURE_SIZE, TEXTURE_SIZE, alpha=False)
catch = bpy.data.materials.new("Catch")
catch.use_nodes = True
node = catch.node_tree.nodes.new("ShaderNodeTexImage")
node.image = target
catch.node_tree.nodes.active = node
low.data.materials.clear()
low.data.materials.append(catch)
only([high, low])
bpy.ops.object.bake(type="EMIT")
settings = scene.render.image_settings
settings.file_format, settings.color_mode, settings.quality = "JPEG", "RGB", JPEG_QUALITY
target.save_render(OUT_JPG, scene=scene)
print("WROTE %s: %d bytes" % (OUT_JPG, os.path.getsize(OUT_JPG)))

# 4. The model and its points.
bpy.data.objects.remove(high)
low.data.materials.clear()
low.data.materials.append(bpy.data.materials.new(MATERIAL))
low.data.name = MATERIAL
only([low])
bpy.ops.export_scene.gltf(filepath=OUT_GLB, export_format="GLB", use_selection=True, export_materials="EXPORT",
                          export_normals=True, export_texcoords=True, export_apply=True)
print("WROTE %s: %d triangles" % (OUT_GLB, len(low.data.polygons)))
written = {k: [round(float(c), 4) for c in v] for k, v in points.items()}
written["upper_half_width"] = round(upper_half * scale, 4)
written["lower_half_width"] = round(lower_half * scale, 4)
written["thickness"] = round(float(game[:, 1].max() - game[:, 1].min()), 4)
written["extent"] = [round(float(c), 4) for c in (game.max(0) - game.min(0))]
with open(OUT_POINTS, "w") as fh:
    json.dump(written, fh, indent=1)
print("WROTE %s: %s" % (OUT_POINTS, written))
