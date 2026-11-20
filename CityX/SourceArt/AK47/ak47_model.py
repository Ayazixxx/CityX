# THE AK-47, modelled from scratch out of rounded lofts, turned parts and bevelled slabs (owner, 2026-10-14: "add an ak47 with reload animation
# too. use this file attached as a guide but remake it yourself"; 2026-11-19: "make the ak47 model look better keep it sort of cartoony ... i dont
# need it like gta 6 levels of realism id still like it to look good", and of a stylised low-poly AK on Sketchfab, looked at and not downloaded:
# "that one looks pretty good"). The rifle's real proportions (overall 880 mm, barrel 415 mm: Wikipedia's AK-47 table) with its forms a little
# fuller, as a stylised gun's are: a rounded wooden stock, grip and handguards, a stamped receiver under an arched ribbed dust cover, the rear
# sight's ramp and leaf, the gas block and tube, the front sight's ears, the cleaning rod, a rounded ribbed magazine with a round at its lips.
#
# PARTS, each its own object so the game can move it: Body, Magazine (curved, origin at its FRONT LUG: an AK magazine hooks its front lug into
# the well and rocks back to lock, so that is what it turns about), Bolt (the bright carrier seen in the slot on the right side with its charging
# handle: it slides back when racked and on every shot), Trigger (origin at its pivot).
#
# WHAT THE HANDS HOLD STAYS WHERE IT WAS (the reload and the holds were made on the first model): the grip's back strap, the lower handguard's
# box (FirearmPlayTests measures the left hand against it), the magazine's arc and size, the butt, the charging handle, the trigger's pivot.
#
# AXES AND ORIGIN: authored in the gun's Unity frame (x right, y up, z the muzzle) in real metres with the origin at the WEB of the pistol grip
# (where the hand's thumb-index crotch sits), then scaled by SCALE. Same conventions as glock_model.py, so the game's one pistol-grip hold fits.
#   blender --background --python ak47_build_all.py -- [--no-body] [--render <dir>]
#   blender --background --python ak47_model.py -- --look <dir>      (the gun alone from five sides, nothing written to the game)

import bpy
import json
import math
import os
import sys
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
DUST_COVER = dict(z0=-0.042, z1=0.174, y0=0.049, y1=0.066, half_width=0.0138)
BORE_Y = 0.036
BARREL = dict(z0=0.200, z1=0.586, radius=0.0082)
MUZZLE_Z = 0.618
GAS_TUBE = dict(y=0.062, z0=0.250, z1=0.420, radius=0.0075)
# The lower handguard's box, which the left hand's hold was made on: nothing of the handguards reaches outside it.
HANDGUARD = dict(z0=0.255, z1=0.405, y0=0.005, y1=0.057, half_width=0.023, top=0.075)
GRIP_RAKE = 18.0
GRIP = dict(length=0.105, depth=0.034)
MAG_FRONT_LUG = Vector((0.0, 0.004, 0.140))
MAG = dict(top_z0=0.098, top_z1=0.140, inside=0.026, radius=0.40, length=0.24, half_width=0.0135, half_depth_top=0.021, half_depth_bottom=0.029)
BOLT_HANDLE = Vector((0.0155, 0.041, 0.162))
BOLT_TRAVEL = 0.12
TRIGGER_PIVOT = Vector((0.0, 0.0, 0.058))
BUTT = Vector((0.0, -0.030, -0.287))
SUPPORT = Vector((0.0, 0.006, 0.300))
EJECTION_PORT = Vector((0.016, 0.040, 0.118))

# (name, colour, metallic, roughness) for Blender's own renders: the game's looks are AK47Builder.Finishes, by these names.
STEEL = ("AKSteel", (0.24, 0.255, 0.29), 0.4, 0.5)
DARK = ("AKDarkSteel", (0.07, 0.07, 0.08), 0.5, 0.55)
BRIGHT = ("AKBrightSteel", (0.60, 0.61, 0.63), 0.6, 0.4)
WOOD = ("AKWood", (0.47, 0.22, 0.08), 0.0, 0.55)
WOOD_DARK = ("AKDarkWood", (0.27, 0.12, 0.05), 0.0, 0.6)
MAG_STEEL = ("AKMagazine", (0.31, 0.32, 0.35), 0.4, 0.5)
BRASS = ("AKBrass", (0.78, 0.6, 0.22), 0.7, 0.4)

# Rounded forms are shaded smooth; an edge sharper than this stays a crisp line.
SMOOTH_DEGREES = 50.0

X, Y, Z = Vector((1.0, 0.0, 0.0)), Vector((0.0, 1.0, 0.0)), Vector((0.0, 0.0, 1.0))


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
exec(compile(open(_PARTS_KIT).read(), _PARTS_KIT, "exec"), globals())     # Part, material, U, rounded_rect, rect_ring


def across_z(z, y0, y1, half_width, radius, radius_bottom=None, corner=3):
    """A section across the gun at z, from y0 up to y1: for a loft running along the barrel."""
    return (Vector((0.0, (y0 + y1) * 0.5, z)), X, Y, rounded_rect(half_width, (y1 - y0) * 0.5, radius, radius_bottom, corner))


# THE SMALL THINGS ARE DRAWN, NOT RAISED. The game draws a black line wherever a surface folds or steps (docs/CEL_SHADING.md), and a rib or
# a rivet standing a millimetre off the steel is smaller than a pixel: its line came out as a row of black specks along the receiver and
# the magazine. So a pressed rib, a groove, a chequered panel is a band of another colour in the surface itself (Part.rings' inlays), and a
# rivet or a pin a disc lying this far off it (far enough not to flicker against it at the distances its colour can be seen from).
FLUSH = 0.0004


def build_body(root):
    body = Part("Body", (0.0, 0.0, 0.0), STEEL)
    r, d = RECEIVER, DUST_COVER

    # THE RECEIVER: a stamped box, its edges rolled, with the magazine well's pressed dimple, the rivets of the trunnions, the pins of the
    # trigger group, and on the right the slot the bolt carrier shows in and the long selector lever (down: fire).
    body.rbox((0.0, (r["y0"] + r["y1"]) * 0.5, (r["z0"] + r["z1"]) * 0.5), (r["half_width"] * 2, r["y1"] - r["y0"], r["z1"] - r["z0"]), 0.003)
    side = r["half_width"]
    for sx in (-1, 1):
        body.box((sx * side, 0.0245, 0.119), (2.0 * FLUSH, 0.012, 0.032), mat=DARK)
        for z, y, radius in ((0.043, 0.019, 0.0017), (0.060, 0.024, 0.0017), (0.080, 0.027, 0.0017),
                             (0.186, 0.013, 0.0024), (0.186, 0.031, 0.0024), (-0.036, 0.012, 0.0024), (-0.036, 0.037, 0.0024), (0.070, 0.011, 0.0024)):
            body.cylinder((sx * (side - 0.0005), y, z), (sx * (side + FLUSH), y, z), radius, 8, mat=DARK)
    body.box((side, 0.041, 0.091), (2.0 * FLUSH, 0.011, 0.124), mat=DARK)
    # (the lever and its pivot lie wholly under the slot: the bright carrier runs back over that end of it on every shot)
    body.cylinder((side - 0.0005, 0.0298, 0.040), (side + 0.0014, 0.0298, 0.040), 0.0052, 10, mat=DARK)
    body.slab([(0.031, 0.0350), (0.031, 0.0245), (0.050, 0.0225), (0.146, 0.0105), (0.160, 0.0105), (0.160, 0.0185), (0.146, 0.0190), (0.050, 0.0350)],
              0.0004, 0.0001, x=side + 0.0004, mat=DARK)
    body.rbox((side + 0.0040, 0.0145, 0.153), (0.006, 0.0045, 0.013), 0.0012, axis='x', mat=DARK)

    # THE DUST COVER: arched, dropping to the rear, three pressed ribs across it (drawn), the recoil spring's button behind.
    cover = [(d["z0"], 0.056, 0.0020), (d["z0"] + 0.004, 0.059, 0.0), (d["z0"] + 0.016, 0.0640, 0.0), (d["z0"] + 0.034, d["y1"], 0.0)]
    ribs = []
    for z in (0.018, 0.070, 0.122):
        ribs.append(len(cover))
        cover += [(z - 0.0016, d["y1"], 0.0), (z + 0.0016, d["y1"], 0.0)]
    cover += [(d["z1"] - 0.003, d["y1"], 0.0), (d["z1"], d["y1"] - 0.002, 0.0020)]
    body.rings([across_z(z, d["y0"], top, d["half_width"] - inset, 0.0095, 0.001) for z, top, inset in cover],
               inlays=[(span, span, None, DARK) for span in ribs])
    body.rbox((0.0, 0.0525, d["z0"] - 0.004), (0.009, 0.008, 0.010), 0.002, mat=DARK)

    # THE FRONT TRUNNION AND THE REAR SIGHT: the block the barrel is pinned in, the sight's ramp over it, and the leaf with its slider and notch.
    body.slab([(0.198, 0.0060), (0.198, 0.0505), (0.254, 0.0505), (0.254, 0.0170), (0.238, 0.0060)], 0.0155, 0.0025)
    body.rbox((0.0, 0.031, 0.2525), (0.043, 0.045, 0.0060), 0.004, ends=0.0010)
    body.slab([(0.176, 0.050), (0.176, 0.0705), (0.204, 0.0725), (0.250, 0.0590), (0.253, 0.050)], 0.0130, 0.0020)
    body.strap([(0.249, 0.0625), (0.212, 0.0735), (0.184, 0.0745)], 0.0060, 0.0022, mat=DARK)
    body.rbox((0.0, 0.0735, 0.214), (0.0155, 0.0050, 0.0080), 0.0010, mat=DARK)
    for sx in (-1, 1):
        body.box((sx * 0.0042, 0.0775, 0.184), (0.0036, 0.0050, 0.0040), mat=DARK)

    # THE BARREL, turned in one piece with its muzzle nut and the bore; the cleaning rod under it.
    b = BARREL["radius"]
    body.lathe((0.0, BORE_Y), [(0.250, b), (0.586, b), (0.586, 0.0100), (0.590, 0.0116), (0.612, 0.0116), (MUZZLE_Z, 0.0104), (MUZZLE_Z, 0.0050),
                               (0.598, 0.0050)], 16, mat=DARK)
    body.cylinder((0.0, 0.0185, 0.412), (0.0, 0.0185, 0.586), 0.0026, 8, mat=DARK)
    body.cylinder((0.0, 0.0185, 0.586), (0.0, 0.0185, 0.594), 0.0038, 8, mat=DARK)

    # THE GAS SYSTEM: the tube over the barrel (seen at each end of the upper handguard) and the block that joins it to the barrel, raked forward.
    g = GAS_TUBE
    body.cylinder((0.0, g["y"], g["z0"]), (0.0, g["y"], g["z1"]), g["radius"], 14)
    body.slab([(0.419, 0.0265), (0.445, 0.0265), (0.445, 0.0460), (0.433, 0.0700), (0.419, 0.0700)], 0.0105, 0.0022)
    body.rbox((0.0, 0.0235, 0.431), (0.0080, 0.0070, 0.0140), 0.0012)

    # THE FRONT SIGHT: its band round the barrel, the tower, the two ears and the post between them.
    body.lathe((0.0, BORE_Y), [(0.540, 0.0118), (0.542, 0.0128), (0.566, 0.0128), (0.568, 0.0118)], 14)
    body.slab([(0.543, 0.040), (0.565, 0.040), (0.562, 0.0730), (0.546, 0.0730)], 0.0058, 0.0012)
    for sx in (-1, 1):
        body.slab([(0.547, 0.0700), (0.561, 0.0700), (0.563, 0.0860), (0.559, 0.0915), (0.549, 0.0915), (0.545, 0.0860)], 0.0014, 0.0005, x=sx * 0.0070)
    body.cylinder((0.0, 0.0730, 0.554), (0.0, 0.0850, 0.554), 0.0018, 8, mat=DARK)

    # THE HANDGUARDS, wood: the lower one round the barrel with its palm swell, the upper one arched over the gas tube; the steel ferrule
    # behind them, the retainer in front, and the two bands that clip the upper one to the tube.
    h = HANDGUARD
    lower = [(h["z0"] + 0.001, 0.0125, 0.0500, 0.0180), (h["z0"] + 0.007, 0.0090, 0.0525, 0.0203), (0.296, 0.0058, 0.0535, 0.0224),
             (0.346, 0.0058, 0.0535, 0.0224), (0.390, 0.0080, 0.0530, 0.0208), (h["z1"] - 0.001, 0.0105, 0.0515, 0.0192)]
    groove = (0.0395, 0.0435)                                                                              # the finger groove along each side (drawn)
    sections, steps = [], None
    for z, y0, y1, hw in lower:
        middle = (y0 + y1) * 0.5
        ring, steps = marked_rect(hw, (y1 - y0) * 0.5, 0.0045, [y - middle for y in groove], 0.0110, corner=3)
        sections.append((Vector((0.0, middle, z)), X, Y, ring))
    body.rings(sections, mat=WOOD, inlays=[(1, 3, steps(0), WOOD_DARK)])
    upper = [(0.264, 0.0725, 0.0150), (0.268, h["top"] - 0.0008, 0.0165), (0.392, h["top"] - 0.0008, 0.0165), (0.396, 0.0725, 0.0150)]
    body.rings([across_z(z, 0.0540, top, hw, 0.0120, 0.0008) for z, top, hw in upper], mat=WOOD)
    for z in (0.262, 0.398):
        body.rings([across_z(z + dz, 0.0535, h["top"], 0.0174, 0.0125, 0.0008) for dz in (-0.0035, 0.0035)])
    body.rbox((0.0, 0.031, h["z1"] + 0.0045), (0.044, 0.046, 0.0085), 0.004, ends=0.0012)

    # THE PISTOL GRIP, wood, raked back and a little fuller toward its foot, with a darker chequered panel on each side.
    a = grip_axis()
    f = Vector((0.0, a.z, -a.y))                                       # square to the grip, toward the muzzle
    grip = [(-0.020, 0.0125, 0.0340), (0.004, 0.0125, 0.0340), (0.026, 0.0130, 0.0345), (0.055, 0.0135, 0.0360), (0.086, 0.0141, 0.0382),
            (0.100, 0.0142, 0.0390), (GRIP["length"], 0.0128, 0.0360)]
    sections, steps = [], None
    for t, hw, deep in grip:
        ring, steps = marked_rect(hw, deep * 0.5, 0.0075, [-0.0080, 0.0080], corner=3)
        sections.append((a * t + f * (deep * 0.5), X, f, ring))
    body.rings(sections, mat=WOOD, inlays=[(2, 3, steps(0), WOOD_DARK)])

    # THE TRIGGER GUARD (a steel strap from the well back to the grip) and the magazine's paddle release in front of it.
    body.strap([(0.0955, 0.006), (0.0955, -0.024), (0.0895, -0.0305), (0.046, -0.0305), (0.035, -0.0285), (0.0265, -0.0225)], 0.0052, 0.0028)
    body.strap([(0.0990, 0.006), (0.0995, -0.009), (0.0965, -0.019)], 0.0058, 0.0036, mat=DARK)

    # THE STOCK, wood: rounded all the way round, the comb dropping to the butt, in a steel collar at the receiver and a steel butt plate.
    stock = [(-0.046, 0.0445, 0.0065, 0.0150), (-0.060, 0.0428, 0.0040, 0.0163), (-0.080, 0.0405, -0.0030, 0.0173),
             (-0.130, 0.0350, -0.0220, 0.0186), (-0.200, 0.0275, -0.0505, 0.0196), (-0.270, 0.0200, -0.0785, 0.0200), (-0.2815, 0.0185, -0.0800, 0.0195)]
    body.rings([across_z(z, y0, y1, hw, 0.0090, 0.0085) for z, y1, y0, hw in stock], mat=WOOD)
    body.rings([across_z(z, y0, y1, hw, 0.0080, 0.0075) for z, y1, y0, hw in
                ((-0.2815, 0.0190, -0.0808, 0.0203), (BUTT.z + 0.0015, 0.0190, -0.0808, 0.0203), (BUTT.z, 0.0170, -0.0788, 0.0185))])
    body.rbox((0.0, 0.0245, -0.0535), (0.0340, 0.0430, 0.0090), 0.0035, ends=0.0010)
    # the sling's loop under the stock, on its steel plate
    body.strap([(-0.232, -0.0636), (-0.198, -0.0500)], 0.0060, 0.0024)
    body.strap([(-0.224, -0.0601), (-0.2188, -0.0731), (-0.2008, -0.0659), (-0.206, -0.0529)], 0.0042, 0.0026, mat=DARK)
    body.finish(root, smooth=SMOOTH_DEGREES)


def build_magazine(root):
    """The curved steel magazine: rounded, two long ribs pressed along each side (drawn), a floor plate, the lug it rocks on, and a round at
    its lips (seen when it is out of the rifle)."""
    mag = Part("Magazine", MAG_FRONT_LUG, MAG_STEEL)
    slices = mag_slices()
    sections, steps = [], None
    for c, along, up, hw, hd in slices:
        ring, steps = marked_rect(hw, hd, 0.0050, [hd * -0.42 - 0.0015, hd * -0.42 + 0.0015, hd * 0.42 - 0.0015, hd * 0.42 + 0.0015])
        sections.append((c, X, along, ring))
    mag.rings(sections, inlays=[(1, 8, steps(0) + steps(2), DARK)])
    c, along, up, hw, hd = slices[-1]
    floor = rounded_rect(hw + 0.0022, hd + 0.0030, 0.0050, corner=2)
    mag.rings([(c + up * 0.0010, X, along, floor), (c - up * 0.0045, X, along, floor),
               (c - up * 0.0060, X, along, rounded_rect(hw + 0.0008, hd + 0.0016, 0.0045, corner=2))], mat=DARK)
    mag.rbox((0.0, 0.002, -0.002), (0.012, 0.006, 0.006), 0.001)
    c, along, up, hw, hd = slices[0]
    mag.cylinder(tuple(c + up * 0.0015 - Z * 0.017), tuple(c + up * 0.0015 + Z * 0.010), 0.0052, 10, mat=BRASS)
    mag.cylinder(tuple(c + up * 0.0015 + Z * 0.010), tuple(c + up * 0.0015 + Z * 0.024), 0.0040, 10, mat=BRASS, radius_end=0.0014)
    mag.finish(root, smooth=SMOOTH_DEGREES)


def build_bolt(root):
    """The bolt carrier's bright flank in the slot on the right side, and its charging handle: a stem and a flared knob."""
    bolt = Part("Bolt", BOLT_HANDLE, BRIGHT)
    bolt.box((0.0001, 0.0, -0.037), (0.0008, 0.0090, 0.076))
    bolt.cylinder((0.0, 0.0, 0.0), (0.016, 0.0, 0.0), 0.0040, 10)
    bolt.cylinder((0.016, 0.0, 0.0), (0.026, 0.0, 0.0), 0.0042, 10, radius_end=0.0060)
    bolt.finish(root, smooth=SMOOTH_DEGREES)


def build_trigger(root):
    """The trigger: a blade curving back under its pivot."""
    trig = Part("Trigger", TRIGGER_PIVOT, DARK)
    trig.strap([(0.0015, 0.003), (0.0005, -0.008), (-0.0030, -0.0170), (-0.0085, -0.0255)], 0.0035, 0.0050)
    trig.finish(root, smooth=SMOOTH_DEGREES)


def build_ak(root):
    build_body(root)
    build_magazine(root)
    build_bolt(root)
    build_trigger(root)


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


def look(folder):
    """The gun alone from five sides (Workbench, the materials' flat colours, outlined), and its triangle count: for looking at the model
    while it is being shaped. Nothing is written to the game."""
    scene = bpy.context.scene
    for ob in list(scene.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    root = build(export=False)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    total = 0
    for ob in root.children:
        mesh = ob.evaluated_get(depsgraph).to_mesh()
        mesh.calc_loop_triangles()
        print("%-10s %5d triangles, %d materials" % (ob.name, len(mesh.loop_triangles), len(ob.data.materials)))
        total += len(mesh.loop_triangles)
    print("AK-47: %d triangles" % total)
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'MATERIAL'
    scene.display.shading.show_object_outline = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = 'BOTH'
    scene.display.render_aa = '16'
    scene.render.film_transparent = False
    scene.world = scene.world or bpy.data.worlds.new("World")
    scene.world.color = (0.55, 0.62, 0.72)
    scene.render.resolution_x, scene.render.resolution_y = 1800, 760
    camera = bpy.data.objects.new("LookCamera", bpy.data.cameras.new("LookCamera"))
    scene.collection.objects.link(camera)
    scene.camera = camera
    middle = U(Vector((0.0, 0.0, 0.165)) * SCALE)
    # (name, where the camera stands in the gun's Unity frame, orthographic width in metres or None for a 30 degree lens)
    for name, at, width in (("right", (3.0, 0.0, 0.165 * SCALE), 1.42), ("left", (-3.0, 0.0, 0.165 * SCALE), 1.42),
                            ("top", (0.0, 3.0, 0.165 * SCALE), 1.42), ("front-right", (1.2, 0.55, 1.75), None), ("rear-left", (-1.3, 0.6, -1.1), None)):
        camera.location = U(Vector(at))
        camera.data.type = 'ORTHO' if width else 'PERSP'
        if width:
            camera.data.ortho_scale = width
        else:
            camera.data.lens = 60.0
        # In Blender the gun's up is +Z; seen from straight above, the muzzle is put to the right of the picture.
        direction = middle - camera.location
        camera.rotation_euler = direction.to_track_quat('-Z', 'Y' if name != "top" else 'X').to_euler()
        scene.render.filepath = os.path.join(folder, "ak47-%s.png" % name)
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__" and "--look" in sys.argv:
    look(sys.argv[sys.argv.index("--look") + 1])
