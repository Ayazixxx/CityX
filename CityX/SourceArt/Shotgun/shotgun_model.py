# THE PUMP SHOTGUN, built from boxes, cylinders and extruded profiles (owner, 2026-11-18: "make a shotgun with reload animations and parts to it
# that move like our ak47 and pistol ... find a good reference ... and remake something similar in our game"). Made from scratch: a 12-gauge pump
# gun of the Remington 870 / Mossberg 500 family with an 18.5 inch barrel (overall about 980 mm, barrel 470 mm, a 90 mm pump stroke, a 2.75 inch
# shell 20 mm across: Wikipedia's Remington Model 870 table and its 12-gauge page), on a pistol-grip stock so the game's one pistol-grip hold fits
# it as it fits the AK-47. No model file was used: the free ones found were looked up for what a pump gun's reload shows, not downloaded
# (docs/SHOTGUN.md).
#
# PARTS, each its own object so the game can move it: Body (receiver, barrel, magazine tube, stock, grip, trigger guard, bead), Pump (the forend
# on the magazine tube with its two action bars and the bolt that shows in the ejection port: it all slides back together when the gun is
# pumped), Shell (one cartridge, at rest inside the loading port under the receiver: the reload carries it there in the free hand and pushes it
# forward into the tube), Trigger (origin at its pivot).
#
# AXES AND ORIGIN: the gun's Unity frame (x right, y up, z the muzzle) in real metres, the origin at the WEB of the pistol grip, then scaled by
# SCALE. The same conventions as ak47_model.py.
#   blender --background --python shotgun_build_all.py -- [--no-body] [--render <dir>]

import bpy
import json
import math
import os
from mathutils import Vector, Matrix

REPO = (os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..")) if "__file__" in globals()
        else os.environ.get("CITYX_REPO", r"C:\Users\Lenovo\Downloads\the game"))
GLB = os.path.join(REPO, "CityX", "Assets", "CityX", "Weapons", "Shotgun.glb")
GUN_NAME = "Shotgun"
PARTS = ("Pump", "Shell", "Trigger")   # the parts the reload animates and the game drives

# The AK-47's scale: the support hand reaches the forend with the stock in the shoulder pocket (ak47_model.py).
SCALE = 1.45

# ---- the real gun (metres, gun frame) ---------------------------------------------------------------------------------------------------
RECEIVER = dict(z0=-0.045, z1=0.185, y0=0.004, y1=0.060, half_width=0.017)
BORE_Y = 0.047
BARREL = dict(z0=0.185, z1=0.655, radius=0.0108)
MUZZLE_Z = 0.655
TUBE = dict(y=0.020, z0=0.185, z1=0.600, radius=0.0118)
FOREND = dict(z0=0.275, z1=0.425, half_width=0.024, y0=-0.004, y1=0.036)
PUMP_ORIGIN = Vector((0.0, TUBE["y"], (FOREND["z0"] + FOREND["z1"]) * 0.5))
PUMP_TRAVEL = 0.085
EJECTION_PORT = Vector((0.0175, 0.044, 0.105))
PORT = dict(z0=0.068, z1=0.142, y0=0.034, y1=0.054)
LOADING_PORT = Vector((0.0, 0.004, 0.130))          # the middle of the opening under the receiver, ahead of the trigger guard
GRIP_RAKE = 18.0
GRIP = dict(length=0.105, depth=0.038, half_width=0.0145)
TRIGGER_PIVOT = Vector((0.0, 0.0, 0.058))
BUTT = Vector((0.0, -0.030, -0.300))
SUPPORT = Vector((0.0, FOREND["y0"], PUMP_ORIGIN.z))   # under the forend's middle
SHELL = dict(radius=0.0101, length=0.062, head=0.009)

STEEL = ("SGSteel", (0.07, 0.07, 0.078), 0.85, 0.42)
DARK = ("SGDark", (0.03, 0.03, 0.034), 0.6, 0.6)
POLYMER = ("SGPolymer", (0.045, 0.045, 0.05), 0.0, 0.7)
HULL = ("SGShellHull", (0.72, 0.07, 0.05), 0.0, 0.45)
BRASS = ("SGShellBrass", (0.78, 0.6, 0.22), 0.9, 0.35)


def grip_axis():
    r = math.radians(GRIP_RAKE)
    return Vector((0.0, -math.cos(r), -math.sin(r)))


_PARTS_KIT = os.path.join(REPO, "CityX", "SourceArt", "Firearms", "firearm_parts.py")
exec(compile(open(_PARTS_KIT).read(), _PARTS_KIT, "exec"), globals())     # Part, material, U


def build_shotgun(root):
    g = grip_axis()
    body = Part("Body", (0.0, 0.0, 0.0), STEEL)
    r = RECEIVER
    # the receiver: a tall flat-sided block, its top rounded by a narrower cap; the dark ejection port on the right and loading port underneath
    body.box((0.0, (r["y0"] + r["y1"]) * 0.5, (r["z0"] + r["z1"]) * 0.5), (r["half_width"] * 2, r["y1"] - r["y0"], r["z1"] - r["z0"]))
    body.box((0.0, r["y1"] + 0.003, (r["z0"] + r["z1"]) * 0.5), (r["half_width"] * 1.35, 0.006, r["z1"] - r["z0"] - 0.012))
    body.box((r["half_width"] + 0.0002, (PORT["y0"] + PORT["y1"]) * 0.5, (PORT["z0"] + PORT["z1"]) * 0.5),
             (0.001, PORT["y1"] - PORT["y0"], PORT["z1"] - PORT["z0"]), mat=DARK)
    body.box((0.0, r["y0"] - 0.0002, LOADING_PORT.z), (0.022, 0.001, 0.085), mat=DARK)
    # the barrel with its bead, the magazine tube under it with its cap, and the clamp that ties them near the muzzle
    body.cylinder((0.0, BORE_Y, BARREL["z0"]), (0.0, BORE_Y, BARREL["z1"]), BARREL["radius"], 16)
    body.cylinder((0.0, BORE_Y, BARREL["z0"]), (0.0, BORE_Y, BARREL["z0"] + 0.03), BARREL["radius"] + 0.0035, 16)
    body.cylinder((0.0, BORE_Y + BARREL["radius"], MUZZLE_Z - 0.014), (0.0, BORE_Y + BARREL["radius"] + 0.006, MUZZLE_Z - 0.014), 0.0026, 8, mat=BRASS)
    body.cylinder((0.0, TUBE["y"], TUBE["z0"]), (0.0, TUBE["y"], TUBE["z1"]), TUBE["radius"], 16)
    body.cylinder((0.0, TUBE["y"], TUBE["z1"]), (0.0, TUBE["y"], TUBE["z1"] + 0.016), TUBE["radius"] + 0.0025, 14, mat=DARK)
    body.box((0.0, (BORE_Y + TUBE["y"]) * 0.5, 0.560), (0.030, BORE_Y - TUBE["y"] + 0.03, 0.014), mat=DARK)
    # the pistol grip (raked back), the trigger guard, the safety button behind it
    grip_centre = g * (GRIP["length"] * 0.5) + Vector((0.0, 0.0, GRIP["depth"] * 0.5))
    body.box(tuple(grip_centre), (GRIP["half_width"] * 2, GRIP["length"], GRIP["depth"]), rake=GRIP_RAKE, mat=POLYMER)
    body.box((0.0, -0.031, 0.061), (0.010, 0.005, 0.062), mat=DARK)
    body.box((0.0, -0.013, 0.091), (0.010, 0.036, 0.005), mat=DARK)
    body.cylinder((-0.019, 0.012, 0.020), (0.019, 0.012, 0.020), 0.004, 8, mat=DARK)
    # the stock: a polymer profile seen from the side, the comb dropping toward the butt, a rubber recoil pad
    stock = [(-0.045, 0.052), (-0.285, 0.026), (-0.298, 0.022), (-0.298, -0.082), (-0.285, -0.086), (-0.135, -0.026), (-0.078, -0.004), (-0.045, 0.006)]
    body.profile_extrude(stock, -0.019, 0.019, mat=POLYMER)
    body.box((0.0, -0.030, -0.3035), (0.040, 0.108, 0.011), mat=DARK)
    body.finish(root)

    # the pump: the ribbed forend round the tube, the two action bars running back into the receiver, and the bolt showing in the port
    pump = Part("Pump", PUMP_ORIGIN, POLYMER)
    f = FOREND
    mid_y = (f["y0"] + f["y1"]) * 0.5 - PUMP_ORIGIN.y
    pump.box((0.0, mid_y, 0.0), (f["half_width"] * 2, f["y1"] - f["y0"], f["z1"] - f["z0"]))
    pump.box((0.0, mid_y - 0.004, 0.0), (f["half_width"] * 2 + 0.006, (f["y1"] - f["y0"]) * 0.6, f["z1"] - f["z0"] - 0.02))
    for i in range(7):                                                                                         # the grip ribs
        z = -0.054 + i * 0.018
        pump.box((0.0, mid_y - 0.004, z), (f["half_width"] * 2 + 0.011, (f["y1"] - f["y0"]) * 0.5, 0.006), mat=DARK)
    for side in (-1, 1):
        pump.box((side * 0.0145, 0.004, f["z0"] - PUMP_ORIGIN.z - 0.055), (0.003, 0.008, 0.110), mat=STEEL)
    port_mid = Vector((RECEIVER["half_width"] + 0.0008, (PORT["y0"] + PORT["y1"]) * 0.5, (PORT["z0"] + PORT["z1"]) * 0.5)) - PUMP_ORIGIN
    pump.box(tuple(port_mid), (0.0012, PORT["y1"] - PORT["y0"] - 0.003, PORT["z1"] - PORT["z0"] - 0.004), mat=STEEL)
    pump.finish(root)

    # one shell, lying along the bore inside the loading port (where the reload leaves it): the red hull forward, the brass head back
    shell = Part("Shell", LOADING_PORT + Vector((0.0, 0.012, 0.0)), HULL)
    s = SHELL
    shell.cylinder((0.0, 0.0, -s["length"] * 0.5 + s["head"]), (0.0, 0.0, s["length"] * 0.5), s["radius"], 12)
    shell.cylinder((0.0, 0.0, -s["length"] * 0.5), (0.0, 0.0, -s["length"] * 0.5 + s["head"]), s["radius"] + 0.0008, 12, mat=BRASS)
    shell.finish(root)

    # the trigger: a curved blade under its pivot
    trig = Part("Trigger", TRIGGER_PIVOT, DARK)
    for i in range(4):
        t = (i + 0.5) / 4.0
        trig.box((0.0, -0.026 * t, -0.008 * t * t), (0.007, 0.0075, 0.005))
    trig.finish(root)


def scale_all(root):
    """The gun was authored in real metres: everything (meshes and the parts' origins) times SCALE."""
    for ob in root.children:
        ob.data.transform(Matrix.Scale(SCALE, 4))
        ob.location = ob.location * SCALE
        ob.data.update()


POINTS = {
    "muzzle": tuple(Vector((0.0, BORE_Y, MUZZLE_Z)) * SCALE),
    "ejection_port": tuple(EJECTION_PORT * SCALE),
    "loading_port": tuple(LOADING_PORT * SCALE),
    "butt": tuple(BUTT * SCALE),
    "support": tuple(SUPPORT * SCALE),
    "pump": tuple(PUMP_ORIGIN * SCALE),
    "pump_travel": PUMP_TRAVEL * SCALE,
    "shell_rest": tuple((LOADING_PORT + Vector((0.0, 0.012, 0.0))) * SCALE),
    "shell_length": SHELL["length"] * SCALE,
    "shell_radius": SHELL["radius"] * SCALE,
    "trigger_pivot": tuple(TRIGGER_PIVOT * SCALE),
    "scale": SCALE,
}


def build(export=True):
    root = bpy.data.objects.get(GUN_NAME)
    if root is None:
        root = bpy.data.objects.new(GUN_NAME, None)
        bpy.context.scene.collection.objects.link(root)
    build_shotgun(root)
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
