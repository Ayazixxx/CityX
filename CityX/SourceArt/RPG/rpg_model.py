# THE RPG, built from boxes, cylinders and cones (owner, 2026-11-19: "make a basic rpg rocket launcher that is animated. look online for a free
# rpg/rocket launcher model with reload animations and try to make some version of that"). Made from scratch after the RPG-7 (Wikipedia's RPG-7
# page: a 40 mm tube 950 mm long, wood round its middle against the heat, the rear flared into a blast cone, an optical sight on the left; the
# PG-7V rocket's 85 mm warhead stands out of the muzzle, its motor and folded fins inside the tube). The free models found (itch.io, CGTrader)
# were looked at for the shape, not downloaded: none had a reload (docs/RPG.md).
#
# PARTS, each its own object so the game can move it: Body (tube, blast cone, heat guards, trigger housing, pistol grip, sights, optic), Rocket
# (the whole round: the warhead, the motor and its folded fins; its origin at the muzzle, where it sits loaded: the reload carries it there in the
# free hand and pushes it in), Hammer (the cocking lever at the back of the trigger housing, origin at its pivot), Trigger (origin at its pivot).
#
# AXES AND ORIGIN: the gun's Unity frame (x right, y up, z the muzzle) in real metres, the origin at the WEB of the pistol grip, then scaled by
# SCALE. The same conventions as ak47_model.py and shotgun_model.py.
#   blender --background --python rpg_build_all.py -- [--no-body] [--render <dir>]

import bpy
import json
import math
import os
from mathutils import Vector, Matrix

REPO = (os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..")) if "__file__" in globals()
        else os.environ.get("CITYX_REPO", r"C:\Users\Lenovo\Downloads\the game"))
GLB = os.path.join(REPO, "CityX", "Assets", "CityX", "Weapons", "RPG.glb")
GUN_NAME = "RPG"
PARTS = ("Rocket", "Hammer", "Trigger")   # the parts the reload animates and the game drives

# The rifles' scale (ak47_model.py): a real launcher in the hands of the game's 2.6 m body.
SCALE = 1.45

# ---- the real launcher (metres, gun frame) -----------------------------------------------------------------------------------------------
TUBE_Y = 0.062                                   # the tube's axis above the grip's web
TUBE_RADIUS = 0.025
REAR_Z = -0.46                                   # the back of the blast cone
CONE = dict(z0=REAR_Z, z1=-0.24, r0=0.052, r1=0.027)
MUZZLE_Z = 0.49
REAR_GUARD = dict(z0=-0.21, z1=0.03, radius=0.036)     # the long wooden guard over the shoulder and the grip
FRONT_GUARD = dict(z0=0.11, z1=0.31, radius=0.034)     # the short one the support hand holds
HOUSING = dict(z0=-0.035, z1=0.115, y0=0.004, y1=TUBE_Y - TUBE_RADIUS + 0.004, half_width=0.014)
GRIP_RAKE = 14.0
GRIP = dict(length=0.11, depth=0.036, half_width=0.0145)
TRIGGER_PIVOT = Vector((0.0, 0.004, 0.055))
HAMMER_PIVOT = Vector((0.0, 0.026, -0.03))
HAMMER_COCK_DEGREES = 38.0
FRONT_SIGHT_Z = 0.40
REAR_SIGHT_Z = 0.07
OPTIC = dict(x=-0.058, y=TUBE_Y + 0.028, z0=-0.075, z1=0.125, radius=0.018)
SUPPORT = Vector((0.0, TUBE_Y - FRONT_GUARD["radius"], (FRONT_GUARD["z0"] + FRONT_GUARD["z1"]) * 0.5))   # under the front guard's middle
# Where the tube rests on the shoulder: on the rear guard's underside behind the grip. The game's rifle pose puts a gun's BUTT in the shoulder
# pocket; this launcher has none, so its "butt" is a point that far below and ahead of where the tube rests, which lays the tube on top of the
# shoulder beside the head when the pocket takes it (tuned against the game's pictures).
REST = Vector((0.0, TUBE_Y - REAR_GUARD["radius"], -0.15))
BUTT = REST + Vector((0.0, -0.15, 0.10))
ROCKET_ORIGIN = Vector((0.0, TUBE_Y, MUZZLE_Z))
# The round (PG-7V, in the rocket's own frame from the muzzle): the warhead out in front, the motor and fins back inside the tube.
ROCKET = dict(stalk=0.12, stalk_radius=0.021, body=0.14, warhead_radius=0.0425, ogive=0.15, nose=0.035, nose_radius=0.008,
              motor=0.33, motor_radius=0.0195, fins=0.07)

STEEL = ("RPGSteel", (0.11, 0.115, 0.1), 0.7, 0.5)
DARK = ("RPGDark", (0.03, 0.03, 0.032), 0.5, 0.6)
WOOD = ("RPGWood", (0.33, 0.17, 0.07), 0.0, 0.62)
GLASS = ("RPGGlass", (0.05, 0.08, 0.1), 0.9, 0.1)
WARHEAD = ("RPGWarhead", (0.22, 0.27, 0.16), 0.2, 0.5)
MOTOR = ("RPGMotor", (0.2, 0.2, 0.2), 0.6, 0.45)
BRASS = ("RPGBrass", (0.75, 0.58, 0.22), 0.9, 0.35)


def grip_axis():
    r = math.radians(GRIP_RAKE)
    return Vector((0.0, -math.cos(r), -math.sin(r)))


_PARTS_KIT = os.path.join(REPO, "CityX", "SourceArt", "Firearms", "firearm_parts.py")
exec(compile(open(_PARTS_KIT).read(), _PARTS_KIT, "exec"), globals())     # Part, material, U


def build_rpg(root):
    g = grip_axis()
    body = Part("Body", (0.0, 0.0, 0.0), STEEL)
    # the tube, the blast cone flaring out behind, a ring at the muzzle
    body.cylinder((0.0, TUBE_Y, CONE["z1"]), (0.0, TUBE_Y, MUZZLE_Z), TUBE_RADIUS, 20)
    body.cylinder((0.0, TUBE_Y, CONE["z0"]), (0.0, TUBE_Y, CONE["z1"]), CONE["r0"], 20, radius_end=CONE["r1"])
    body.cylinder((0.0, TUBE_Y, CONE["z0"]), (0.0, TUBE_Y, CONE["z0"] + 0.012), CONE["r0"] + 0.003, 20, mat=DARK)
    body.cylinder((0.0, TUBE_Y, MUZZLE_Z - 0.02), (0.0, TUBE_Y, MUZZLE_Z), TUBE_RADIUS + 0.004, 20, mat=DARK)
    # the wooden heat guards, each held by two dark bands
    for guard in (REAR_GUARD, FRONT_GUARD):
        body.cylinder((0.0, TUBE_Y, guard["z0"]), (0.0, TUBE_Y, guard["z1"]), guard["radius"], 18, mat=WOOD)
        for z in (guard["z0"] + 0.012, guard["z1"] - 0.012):
            body.cylinder((0.0, TUBE_Y, z - 0.006), (0.0, TUBE_Y, z + 0.006), guard["radius"] + 0.002, 18, mat=DARK)
    # the trigger housing under the tube, the guard, the raked pistol grip
    h = HOUSING
    body.box((0.0, (h["y0"] + h["y1"]) * 0.5, (h["z0"] + h["z1"]) * 0.5), (h["half_width"] * 2, h["y1"] - h["y0"], h["z1"] - h["z0"]), mat=DARK)
    body.box((0.0, -0.036, 0.062), (0.009, 0.005, 0.060), mat=DARK)
    body.box((0.0, -0.016, 0.091), (0.009, 0.040, 0.005), mat=DARK)
    grip_centre = g * (GRIP["length"] * 0.5) + Vector((0.0, 0.0, GRIP["depth"] * 0.5))
    body.box(tuple(grip_centre), (GRIP["half_width"] * 2, GRIP["length"], GRIP["depth"]), rake=GRIP_RAKE, mat=WOOD)
    # the iron sights on top: a post at the front, a notch over the grip
    body.box((0.0, TUBE_Y + TUBE_RADIUS + 0.018, FRONT_SIGHT_Z), (0.004, 0.036, 0.008), mat=DARK)
    body.box((0.0, TUBE_Y + TUBE_RADIUS + 0.006, FRONT_SIGHT_Z), (0.016, 0.012, 0.016), mat=DARK)
    body.box((0.0, TUBE_Y + TUBE_RADIUS + 0.014, REAR_SIGHT_Z), (0.022, 0.028, 0.010), mat=DARK)
    # the optic on its bracket on the left: a tube with a rubber eyecup behind and a lens in front
    o = OPTIC
    body.box(((o["x"] + 0.0) * 0.5 - 0.004, o["y"] - 0.012, (o["z0"] + o["z1"]) * 0.5), (abs(o["x"]) - 0.01, 0.016, 0.07), mat=DARK)
    body.cylinder((o["x"], o["y"], o["z0"]), (o["x"], o["y"], o["z1"]), o["radius"], 14, mat=DARK)
    body.cylinder((o["x"], o["y"], o["z0"] - 0.035), (o["x"], o["y"], o["z0"]), o["radius"] + 0.006, 14, mat=DARK, radius_end=o["radius"])
    body.cylinder((o["x"], o["y"], o["z1"]), (o["x"], o["y"], o["z1"] + 0.004), o["radius"] - 0.003, 14, mat=GLASS)
    body.finish(root)

    # the round: warhead (body, ogive, the fuze's nose) ahead of the muzzle, the stalk into it, the motor and the folded fins inside the tube
    rocket = Part("Rocket", ROCKET_ORIGIN, WARHEAD)
    r = ROCKET
    z = 0.0
    rocket.cylinder((0.0, 0.0, z), (0.0, 0.0, z + r["stalk"]), r["stalk_radius"], 14, mat=MOTOR)
    z += r["stalk"]
    rocket.cylinder((0.0, 0.0, z), (0.0, 0.0, z + 0.03), r["stalk_radius"], 18, radius_end=r["warhead_radius"])
    z += 0.03
    rocket.cylinder((0.0, 0.0, z), (0.0, 0.0, z + r["body"]), r["warhead_radius"], 18)
    z += r["body"]
    rocket.cylinder((0.0, 0.0, z), (0.0, 0.0, z + r["ogive"]), r["warhead_radius"], 18, radius_end=r["nose_radius"] + 0.006)
    z += r["ogive"]
    rocket.cylinder((0.0, 0.0, z), (0.0, 0.0, z + r["nose"]), r["nose_radius"], 10, mat=BRASS)
    rocket.cylinder((0.0, 0.0, -r["motor"]), (0.0, 0.0, 0.0), r["motor_radius"], 14, mat=MOTOR)
    for i in range(4):                                                                   # the fins, folded flat along the motor's tail
        a = math.radians(45.0 + 90.0 * i)
        offset = Vector((math.cos(a), math.sin(a), 0.0)) * (r["motor_radius"] + 0.001)
        rocket.box(tuple(offset + Vector((0.0, 0.0, -r["motor"] + r["fins"] * 0.5))), (0.003, 0.003, r["fins"]), mat=DARK)
    rocket.finish(root)

    # the hammer: a short lever standing up out of the back of the housing (cocked: swung back)
    hammer = Part("Hammer", HAMMER_PIVOT, STEEL)
    hammer.box((0.0, 0.010, -0.004), (0.008, 0.022, 0.008))
    hammer.box((0.0, 0.020, -0.010), (0.010, 0.006, 0.014), mat=DARK)
    hammer.finish(root)

    # the trigger: a curved blade under its pivot
    trig = Part("Trigger", TRIGGER_PIVOT, DARK)
    for i in range(4):
        t = (i + 0.5) / 4.0
        trig.box((0.0, -0.026 * t, -0.008 * t * t), (0.007, 0.0075, 0.005))
    trig.finish(root)


def scale_all(root):
    """The launcher was authored in real metres: everything (meshes and the parts' origins) times SCALE."""
    for ob in root.children:
        ob.data.transform(Matrix.Scale(SCALE, 4))
        ob.location = ob.location * SCALE
        ob.data.update()


ROCKET_TIP_Z = ROCKET["stalk"] + 0.03 + ROCKET["body"] + ROCKET["ogive"] + ROCKET["nose"]
POINTS = {
    "muzzle": tuple(Vector((0.0, TUBE_Y, MUZZLE_Z)) * SCALE),
    "rear": tuple(Vector((0.0, TUBE_Y, REAR_Z)) * SCALE),
    "butt": tuple(BUTT * SCALE),
    "rest": tuple(REST * SCALE),
    "support": tuple(SUPPORT * SCALE),
    "rocket": tuple(ROCKET_ORIGIN * SCALE),
    "rocket_tip": tuple((ROCKET_ORIGIN + Vector((0.0, 0.0, ROCKET_TIP_Z))) * SCALE),
    "rocket_tail": tuple((ROCKET_ORIGIN + Vector((0.0, 0.0, -ROCKET["motor"]))) * SCALE),
    "rocket_length": (ROCKET_TIP_Z + ROCKET["motor"]) * SCALE,
    "warhead_radius": ROCKET["warhead_radius"] * SCALE,
    "hammer_pivot": tuple(HAMMER_PIVOT * SCALE),
    "hammer_cock_degrees": HAMMER_COCK_DEGREES,
    "trigger_pivot": tuple(TRIGGER_PIVOT * SCALE),
    "scale": SCALE,
}


def build(export=True):
    root = bpy.data.objects.get(GUN_NAME)
    if root is None:
        root = bpy.data.objects.new(GUN_NAME, None)
        bpy.context.scene.collection.objects.link(root)
    build_rpg(root)
    scale_all(root)
    if export:
        bpy.ops.object.select_all(action='DESELECT')
        root.select_set(True)
        for child in root.children_recursive:
            child.select_set(True)
        os.makedirs(os.path.dirname(GLB), exist_ok=True)
        bpy.ops.export_scene.gltf(filepath=GLB, export_format='GLB', use_selection=True, export_apply=True,
                                  export_yup=True, export_materials='EXPORT', export_animations=False, export_skins=False)
        with open(GLB.replace(".glb", ".points.json"), "w") as fh:
            json.dump(POINTS, fh, indent=1)
        print("wrote", GLB, "and its points")
    return root
