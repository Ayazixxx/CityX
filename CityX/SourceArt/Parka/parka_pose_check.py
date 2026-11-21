# THE PARKA WITH ITS ARMS RAISED, headless: does it move as a coat should once it is skinned the way the game skins a coat?
#   blender --background --python parka_pose_check.py -- <Parka.glb> <folder for the pictures>
# The body and skeleton are the game's (../Octaslash/octaslash_rig.py), the weights the ones the game's parka gets (parka_skin.py). The game's half-turning shoulder bones are built here as it has them (a bone at
# each shoulder that takes half the upper arm's turn); its collar bone is stood in for by the neck and its skirt bones by the hips: enough to
# see whether a sleeve tears from the coat's side, drags it along, leaves a hole under the arm or creases there. Pictures: standing, both arms out to the side, one arm forward, and the armpit from below.
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE_ART = os.path.dirname(HERE)
GLB, OUT = sys.argv[sys.argv.index("--") + 1:][:2]
STAND_INS = {"collar": ("neck", "neck"),
             "skirt_front_l": ("hips", "hips"), "skirt_back_l": ("hips", "hips"), "skirt_front_r": ("hips", "hips"),
             "skirt_back_r": ("hips", "hips")}


def run(path):
    exec(compile(open(path).read(), path, "exec"), globals())


bpy.ops.wm.read_factory_settings(use_empty=True)
run(os.path.join(SOURCE_ART, "Octaslash", "octaslash_rig.py"))
run(os.path.join(SOURCE_ART, "garment_rig.py"))
run(os.path.join(HERE, "parka_skin.py"))
body, head, rig = bpy.data.objects["Body"], bpy.data.objects["Head"], bpy.data.objects["CityXRig"]
before = set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=GLB)
coat = [o for o in bpy.data.objects if o not in before and o.type == "MESH"][0]
bpy.ops.object.select_all(action="DESELECT")
coat.select_set(True)
bpy.context.view_layer.objects.active = coat
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
co, weights = skin_parka(coat, body, rig)
for helper, (first, second) in STAND_INS.items():
    for bone in (first, second):
        weights[:, BONE[bone]] += 0.5 * weights[:, BONE[helper]]
    weights[:, BONE[helper]] = 0.0
# the half-turning shoulder bones, as GarmentHelperBones makes them
bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="EDIT")
for side in "lr":
    arm = rig.data.edit_bones["arm_" + side]
    helper = rig.data.edit_bones.new("shoulder_" + side)
    helper.head, helper.tail, helper.roll, helper.parent = arm.head, arm.tail, arm.roll, arm.parent
bpy.ops.object.mode_set(mode="POSE")
for side in "lr":
    half = rig.pose.bones["shoulder_" + side].constraints.new("COPY_ROTATION")
    half.target, half.subtarget, half.influence = rig, "arm_" + side, 0.5
    half.target_space = half.owner_space = "LOCAL"
bpy.ops.object.mode_set(mode="OBJECT")
coat.vertex_groups.clear()
for name, column in BONE.items():
    group = coat.vertex_groups.new(name=name)
    for i in np.nonzero(weights[:, column] > 1e-4)[0]:
        group.add([int(i)], float(weights[i, column]), "REPLACE")
coat.modifiers.new("Rig", "ARMATURE").object = rig
arms = weights[:, [BONE[b] for b in ("arm_l", "forearm_l", "arm_r", "forearm_r")]].sum(1)
print("sleeves: %d of %d points follow an arm by more than half" % ((arms > 0.5).sum(), len(co)))


def turn(bone, axis, degrees):
    """Turns a bone about its own head, round a world axis."""
    pose = rig.pose.bones[bone]
    head = rig.matrix_world @ pose.head
    spin = Matrix.Translation(head) @ Matrix.Rotation(math.radians(degrees), 4, Vector(axis)) @ Matrix.Translation(-head)
    pose.matrix = rig.matrix_world.inverted() @ spin @ rig.matrix_world @ pose.matrix
    bpy.context.view_layer.update()


def picture(name, eye, target, lens=45):
    camera = bpy.data.objects.get("CheckCamera")
    if camera is None:
        camera = bpy.data.objects.new("CheckCamera", bpy.data.cameras.new("CheckCamera"))
        bpy.context.scene.collection.objects.link(camera)
        bpy.context.scene.camera = camera
    camera.location = Vector(eye)
    camera.rotation_euler = (Vector(target) - Vector(eye)).to_track_quat("-Z", "Y").to_euler()
    camera.data.lens = lens
    camera.data.clip_start = 0.02
    bpy.context.scene.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)


scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "TEXTURE"
scene.display.shading.show_backface_culling = True     # a hole shows the backdrop
scene.world = bpy.data.worlds.new("Backdrop")
scene.world.color = (1.0, 0.0, 1.0)
scene.render.film_transparent = False
scene.render.resolution_x, scene.render.resolution_y = 900, 1100
for o in bpy.data.objects:
    if o.type == "MESH" and o is not coat and o.name not in ("Body", "Head"):
        o.hide_render = True

picture("parka_stand_front", (0.0, -5.2, 1.35), (0.0, 0.0, 1.3))
picture("parka_stand_side", (5.2, -0.6, 1.35), (0.0, 0.0, 1.3))
turn("arm_l", (0.0, 1.0, 0.0), -70.0)       # out to the side
turn("arm_r", (0.0, 1.0, 0.0), 70.0)
picture("parka_arms_out_front", (0.0, -5.6, 1.5), (0.0, 0.0, 1.4))
picture("parka_arms_out_back", (0.0, 5.6, 1.5), (0.0, 0.0, 1.4))
picture("parka_armpit_left", (1.3, -1.6, 0.9), (0.42, 0.0, 1.6), 50)
picture("parka_armpit_right", (-1.3, -1.6, 0.9), (-0.42, 0.0, 1.6), 50)
for hidden in (True, False):       # and without the body in the way: any hole under an arm shows the backdrop
    body.hide_render = head.hide_render = hidden
    if hidden:
        picture("parka_armpit_left_alone", (1.3, -1.6, 0.9), (0.42, 0.0, 1.6), 50)
        picture("parka_armpit_right_alone", (-1.3, -1.6, 0.9), (-0.42, 0.0, 1.6), 50)
        picture("parka_armpit_left_behind", (1.3, 1.6, 0.9), (0.42, 0.0, 1.6), 50)
        picture("parka_armpit_right_behind", (-1.3, 1.6, 0.9), (-0.42, 0.0, 1.6), 50)
turn("arm_l", (0.0, 1.0, 0.0), 70.0)
turn("arm_r", (0.0, 1.0, 0.0), -70.0)
turn("arm_l", (1.0, 0.0, 0.0), 75.0)        # forward
turn("forearm_r", (1.0, 0.0, 0.0), 80.0)    # the other elbow bent
picture("parka_arm_forward_side", (5.2, -1.2, 1.5), (0.0, -0.2, 1.35))
picture("parka_arm_forward_front", (1.6, -5.0, 1.6), (0.0, 0.0, 1.35))
print("PICTURES in", OUT)
