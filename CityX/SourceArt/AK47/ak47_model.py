# THE AK-47, built from boxes, cylinders, lofts and extruded profiles (owner, 2026-10-14: "add an ak47 with reload animation too. use this file
# attached as a guide but remake it yourself"). Made from scratch to the rifle's real proportions (overall 880 mm, barrel 415 mm: Wikipedia's
# AK-47 table), checked side by side with the reference only for the shape of the parts; nothing of the reference file is used.
#
# PARTS, each its own object so the game can move it: Body (receiver, dust cover, stock, pistol grip, handguards, gas tube, barrel, sights,
# trigger guard, magazine release, selector), Magazine (curved, origin at its FRONT LUG: an AK magazine hooks its front lug into the well and
# rocks back to lock, so that is what it turns about), Bolt (the carrier's charging handle on the right side: it slides back when racked and on
# every shot), Trigger (origin at its pivot).
#
# AXES AND ORIGIN: authored in the gun's Unity frame (x right, y up, z the muzzle) in real metres with the origin at the WEB of the pistol grip
# (where the hand's thumb-index crotch sits), then scaled by SCALE. Same conventions as glock_model.py, so the game's one pistol-grip hold fits.
#   blender --background --python ak47_build_all.py -- [--no-body] [--render <dir>]

import bpy
import json
import math
import os
from mathutils import Vector, Matrix

# The repository's root: from this file's place (SourceArt/<part>/) when run as a file, else from CITYX_REPO (code pasted into a live
# Blender has no file), else the owner's checkout.
REPO = (os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..")) if "__file__" in globals()
        else os.environ.get("CITYX_REPO", r"C:\Users\Lenovo\Downloads\the game"))
GLB = os.path.join(REPO, "CityX", "Assets", "CityX", "Weapons", "AK47.glb")
GUN_NAME = "AK47"
PARTS = ("Magazine", "Bolt", "Trigger")   # the parts the reload animates and the game drives

# The body is 2.6 m tall with arms to match (shoulder to wrist 0.89 m): at this size the support hand reaches the handguard with the stock in
# the shoulder pocket and the torso bladed 30 degrees, as a real shooter stands (checked in the Blender renders and in the game's rifle pose).
SCALE = 1.45

# ---- the real rifle (metres, gun frame) -------------------------------------------------------------------------------------------------
RECEIVER = dict(z0=-0.050, z1=0.200, y0=0.004, y1=0.050, half_width=0.015)
DUST_COVER = dict(z0=-0.040, z1=0.172, y0=0.050, y1=0.063, half_width=0.0135)
BORE_Y = 0.036
BARREL = dict(z0=0.200, z1=0.586, radius=0.0095)
MUZZLE_Z = 0.618
GAS_TUBE = dict(y=0.062, z0=0.250, z1=0.425, radius=0.0085)
GRIP_RAKE = 18.0
GRIP = dict(length=0.105, depth=0.036, half_width=0.014)
MAG_FRONT_LUG = Vector((0.0, 0.004, 0.140))
MAG = dict(top_z0=0.098, top_z1=0.140, inside=0.026, radius=0.40, length=0.24, half_width=0.0135, half_depth_top=0.021, half_depth_bottom=0.029)
BOLT_HANDLE = Vector((0.0155, 0.041, 0.162))
BOLT_TRAVEL = 0.12
TRIGGER_PIVOT = Vector((0.0, 0.0, 0.058))
BUTT = Vector((0.0, -0.030, -0.287))
SUPPORT = Vector((0.0, 0.006, 0.300))
EJECTION_PORT = Vector((0.016, 0.040, 0.118))

STEEL = ("AKSteel", (0.085, 0.085, 0.095), 0.85, 0.45)
DARK = ("AKDarkSteel", (0.05, 0.05, 0.055), 0.7, 0.55)
WOOD = ("AKWood", (0.40, 0.19, 0.075), 0.0, 0.55)
MAG_STEEL = ("AKMagazine", (0.11, 0.11, 0.12), 0.8, 0.5)


def grip_axis():
    r = math.radians(GRIP_RAKE)
    return Vector((0.0, -math.cos(r), -math.sin(r)))


def mag_slices():
    """The magazine's arc, relative to its front lug: the top inside the well, curving forward as it goes down."""
    c0 = Vector((0.0, MAG["inside"], (MAG["top_z0"] + MAG["top_z1"]) * 0.5)) - MAG_FRONT_LUG
    out = []
    steps = 10
    theta_max = MAG["length"] / MAG["radius"]
    for i in range(steps + 1):
        t = i / steps
        th = theta_max * t
        c = c0 + Vector((0.0, -MAG["radius"] * math.sin(th), MAG["radius"] * (1.0 - math.cos(th))))
        along = Vector((0.0, math.sin(th), math.cos(th)))          # across the magazine, front to back (forward is +)
        up = Vector((0.0, math.cos(th), -math.sin(th)))
        hd = MAG["half_depth_top"] + (MAG["half_depth_bottom"] - MAG["half_depth_top"]) * t
        out.append((c, along, up, MAG["half_width"], hd))
    return out


_PARTS_KIT = os.path.join(REPO, "CityX", "SourceArt", "Firearms", "firearm_parts.py")
exec(compile(open(_PARTS_KIT).read(), _PARTS_KIT, "exec"), globals())     # Part, material, U


def build_ak(root):
    g = grip_axis()
    body = Part("Body", (0.0, 0.0, 0.0), STEEL)
    r = RECEIVER
    # the receiver and its dust cover (a slimmer block on top, rounded by a narrower cap)
    body.box((0.0, (r["y0"] + r["y1"]) * 0.5, (r["z0"] + r["z1"]) * 0.5), (r["half_width"] * 2, r["y1"] - r["y0"], r["z1"] - r["z0"]))
    d = DUST_COVER
    body.box((0.0, (d["y0"] + d["y1"]) * 0.5, (d["z0"] + d["z1"]) * 0.5), (d["half_width"] * 2, d["y1"] - d["y0"], d["z1"] - d["z0"]))
    body.box((0.0, d["y1"] + 0.002, (d["z0"] + d["z1"]) * 0.5), (d["half_width"] * 1.3, 0.004, d["z1"] - d["z0"] - 0.01))
    body.box((0.0, d["y1"] - 0.004, d["z0"] - 0.006), (0.008, 0.008, 0.012), mat=DARK)                                  # the cover's latch
    # the rear sight block and leaf
    body.box((0.0, 0.034, 0.226), (0.031, 0.060, 0.052))
    body.box((0.0, 0.068, 0.232), (0.020, 0.010, 0.034), mat=DARK)
    # the barrel, the muzzle device, the front sight with its ears, the gas block and tube, the cleaning rod
    body.cylinder((0.0, BORE_Y, BARREL["z0"]), (0.0, BORE_Y, BARREL["z1"]), BARREL["radius"], 16, mat=DARK)
    body.cylinder((0.0, BORE_Y, BARREL["z1"]), (0.0, BORE_Y, MUZZLE_Z), 0.0125, 16, mat=DARK)
    body.cylinder((0.0, BORE_Y, 0.543), (0.0, BORE_Y, 0.570), 0.0135, 14)
    body.box((0.0, 0.066, 0.556), (0.010, 0.044, 0.016))
    for side in (-1, 1):
        body.box((side * 0.0085, 0.078, 0.556), (0.004, 0.034, 0.020))
    body.box((0.0, 0.049, 0.433), (0.026, 0.050, 0.026))
    body.cylinder((0.0, GAS_TUBE["y"], GAS_TUBE["z0"]), (0.0, GAS_TUBE["y"], GAS_TUBE["z1"]), GAS_TUBE["radius"], 12)
    body.cylinder((0.0, 0.017, 0.412), (0.0, 0.017, 0.548), 0.0028, 8, mat=DARK)
    # the wooden handguards: the lower one round the barrel, the upper one over the gas tube, and the steel retainer ring at the front
    body.box((0.0, 0.031, 0.330), (0.046, 0.046, 0.150), mat=WOOD)
    body.box((0.0, 0.031, 0.330), (0.040, 0.052, 0.140), mat=WOOD)
    body.box((0.0, GAS_TUBE["y"] + 0.002, 0.333), (0.032, 0.022, 0.134), mat=WOOD)
    body.box((0.0, 0.034, 0.410), (0.050, 0.060, 0.010), mat=DARK)
    # the pistol grip (raked back), the trigger guard, the magazine release paddle, the selector lever on the right
    grip_centre = g * (GRIP["length"] * 0.5) + Vector((0.0, 0.0, GRIP["depth"] * 0.5))
    body.box(tuple(grip_centre), (GRIP["half_width"] * 2, GRIP["length"], GRIP["depth"]), rake=GRIP_RAKE, mat=WOOD)
    body.box((0.0, -0.031, 0.061), (0.010, 0.005, 0.062))
    body.box((0.0, -0.013, 0.091), (0.010, 0.036, 0.005))
    body.box((0.0, -0.004, 0.0955), (0.022, 0.010, 0.006), mat=DARK)
    body.box((0.0175, 0.034, 0.128), (0.003, 0.011, 0.084), mat=DARK)
    body.box((0.0185, 0.024, 0.165), (0.004, 0.012, 0.012), mat=DARK)
    # the stock: a profile seen from the side, the comb dropping toward the butt, steel butt plate
    stock = [(-0.050, 0.044), (-0.270, 0.021), (-0.285, 0.017), (-0.285, -0.076), (-0.274, -0.081), (-0.130, -0.022), (-0.075, -0.002), (-0.050, 0.006)]
    body.profile_extrude(stock, -0.0195, 0.0195, mat=WOOD)
    body.box((0.0, -0.030, -0.2875), (0.041, 0.100, 0.005), mat=DARK)
    body.finish(root)

    # the magazine: the arc, the floor plate, the front lug
    mag = Part("Magazine", MAG_FRONT_LUG, MAG_STEEL)
    slices = mag_slices()
    mag.loft(slices)
    c, along, up, hw, hd = slices[-1]
    mag.box(tuple(c - up * 0.004), (hw * 2 + 0.006, 0.008, hd * 2 + 0.008), tilt=-math.degrees(MAG["length"] / MAG["radius"]))
    mag.box((0.0, 0.002, -0.002), (0.012, 0.006, 0.006))
    for i in (2, 5, 8):                                                                                         # the pressed ribs
        c, along, up, hw, hd = slices[i]
        mag.box(tuple(c), (hw * 2 + 0.002, 0.006, hd * 1.4), tilt=-math.degrees(MAG["length"] / MAG["radius"] * i / 10))
    mag.finish(root)

    # the bolt carrier's charging handle, sticking out of the right side; the carrier's flank showing in the ejection slot behind it
    bolt = Part("Bolt", BOLT_HANDLE, STEEL)
    bolt.box((0.0, -0.002, -0.045), (0.003, 0.012, 0.085))
    bolt.cylinder((0.0, 0.0, 0.0), (0.020, 0.0, 0.0), 0.0042, 10)
    bolt.box((0.024, 0.0, 0.0), (0.010, 0.012, 0.014), mat=DARK)
    bolt.finish(root)

    # the trigger: a curved blade under its pivot
    trig = Part("Trigger", TRIGGER_PIVOT, DARK)
    for i in range(4):
        t = (i + 0.5) / 4.0
        trig.box((0.0, -0.026 * t, -0.008 * t * t), (0.007, 0.0075, 0.005))
    trig.finish(root)


def scale_all(root):
    """The rifle was authored in real metres: everything (meshes and the parts' origins) times SCALE."""
    for ob in root.children:
        ob.data.transform(Matrix.Scale(SCALE, 4))
        ob.location = ob.location * SCALE
        ob.data.update()


POINTS = {
    "muzzle": tuple(Vector((0.0, BORE_Y, MUZZLE_Z)) * SCALE),
    "ejection_port": tuple(EJECTION_PORT * SCALE),
    "mag_front_lug": tuple(MAG_FRONT_LUG * SCALE),
    "mag_out": (0.0, -0.95, 0.31),
    "butt": tuple(BUTT * SCALE),
    "support": tuple(SUPPORT * SCALE),
    "bolt_handle": tuple(BOLT_HANDLE * SCALE),
    "bolt_travel": BOLT_TRAVEL * SCALE,
    "mag_release": tuple(Vector((0.0, -0.004, 0.0955)) * SCALE),
    "trigger_pivot": tuple(TRIGGER_PIVOT * SCALE),
    "scale": SCALE,
}


def build(export=True):
    root = bpy.data.objects.get(GUN_NAME)
    if root is None:
        root = bpy.data.objects.new(GUN_NAME, None)
        bpy.context.scene.collection.objects.link(root)
    build_ak(root)
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
