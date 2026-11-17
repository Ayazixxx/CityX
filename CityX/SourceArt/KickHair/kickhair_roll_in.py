# THE KICK HAIRSTYLE'S EDGES ROLLED INTO THE HEAD (owner, 2026-11-04: "there is a slit where you can see underneath the hair and the sky shows behind it, can you make it
# roll inward into the head"). The hair is one open shell standing up to 8 cm off the head, and a surface is only drawn from outside: looking in under its edge, through the
# gap, you saw through the far side of the shell to the sky. Every open edge is carried on, curling under and in, to end inside the head, so there is no way in under it.
# The head is where the owner's fit puts it (position 0, -2.27, 0, scale 1): in the model, a sphere round (0, 2.27, 0), radius 0.35, in glTF's axes.
# Run it on KickHair-reduced.glb (kickhair_reduce.py's output), never on its own output: then CityX.UIEditor.HairAssets.RebakeKickHair.
#   blender --background --factory-startup --python kickhair_roll_in.py -- KickHair-reduced.glb ../../Assets/CityX/Character/Hair/KickHair.glb [--render <folder>]
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Hair"))
import hair_roll_in  # noqa: E402 (SourceArt/Hair: the rolling shared with the other hairstyles)

args = sys.argv[sys.argv.index("--") + 1:]
source, out = args[0], args[1]
render_folder = args[args.index("--render") + 1] if "--render" in args else None

HEAD = Vector((0.0, 0.0, 2.27))   # Blender's axes: glTF's y is its z
HEAD_RADIUS = 0.35

hair = hair_roll_in.load(source)
hair_roll_in.roll_in(hair, HEAD, HEAD_RADIUS)
hair_roll_in.export(out)

if render_folder:
    # Looking up under the edges, against a sky the same colour the slit showed, with the head in place.
    bpy.ops.mesh.primitive_uv_sphere_add(radius=HEAD_RADIUS, location=HEAD, segments=48, ring_count=32)
    head = bpy.context.object
    bpy.ops.object.shade_smooth()
    skin = bpy.data.materials.new("skin")
    skin.diffuse_color = (0.9, 0.75, 0.2, 1.0)
    head.data.materials.append(skin)
    dark = bpy.data.materials.new("hair")
    dark.diffuse_color = (0.08, 0.05, 0.03, 1.0)
    hair.data.materials.append(dark)
    world = bpy.data.worlds.new("sky")
    world.color = (0.45, 0.65, 0.95)
    bpy.context.scene.world = world
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.show_backface_culling = True
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.background_type = "WORLD"
    scene.render.resolution_x = 700
    scene.render.resolution_y = 700
    camera_data = bpy.data.cameras.new("cam")
    camera_data.lens = 70
    camera = bpy.data.objects.new("cam", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    for name, azimuth, height in (("back-low", 0.0, -0.9), ("back-left-low", 50.0, -0.8), ("left-low", 95.0, -0.7), ("front-low", 180.0, -0.7), ("back", 0.0, 0.1), ("right-low", -95.0, -0.7)):
        a = math.radians(azimuth)
        camera.location = HEAD + Vector((math.sin(a) * 2.2, math.cos(a) * 2.2, height))
        direction = HEAD - camera.location
        camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(render_folder, "kick-roll-" + name + ".png")
        bpy.ops.render.render(write_still=True)
