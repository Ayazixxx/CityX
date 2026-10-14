# THE GLOCK, built from boxes and cylinders (owner, 2026-10-14: "use blender ... to make the basic glock pistol"): a Glock 19's own proportions
# scaled to this body's hands, as SEPARATE PARTS so the game can animate them -- the slide cycles, the magazine comes out, the trigger pulls.
#
# Run inside Blender (headless: blender --background --python glock_build_all.py -- --model), or exec(open(r"<this file>").read()).
# Writes CityX/Assets/CityX/Weapons/Glock.glb.
#
# AXES. Everything is authored in the gun's own Unity frame -- x the shooter's right, y up, z the muzzle -- and put into Blender with U() (the
# character faces -Y in Blender, up is +Z, its left is +X). Blender's glTF export (Y up) then glTFast's import (x mirrored) bring a point back to
# the same Unity coordinates it was authored in.
# ORIGIN. The gun's root is the WEB OF THE HAND: the top of the backstrap where the hand's thumb/index crotch sits under the beavertail. The hold in
# the game (PistolWeapon.GlockHold) puts this point at the hand's web, so the hand placement is one offset.
# PARTS. Frame (polymer: grip, trigger guard, dust cover), Slide (with the ejection port cut, rear serrations, sights), Barrel (showing in the
# port and at the muzzle), Magazine (origin at the mag well's mouth: moving it down -gripAxis pulls it out), Trigger (origin at its pivot), and the
# small levers on the frame. PART_ORIGINS is shared with glock_rig.py (the bones sit on the same points) and GlockBuilder (the prefab's parts).

import bpy
import bmesh
import json
import math
import os
from mathutils import Vector, Matrix

REPO = r"C:\Users\Lenovo\Downloads\the game"
GLB = os.path.join(REPO, "CityX", "Assets", "CityX", "Weapons", "Glock.glb")

# A Glock 19 in real metres, times this: the body is 2.6 m and its hands are wide, so a real-size pistol was a nub in them (the Beretta needed
# 2.5x). 1.9 fits the hand: three fingers on the grip and the little finger under the baseplate, as a compact pistol sits in a big hand; at
# 2.6 the grip was deeper than the palm is long and the fingers could not wrap its front strap (checked in the Blender renders).
SCALE = 1.9

# Real Glock 19 (metres).
SLIDE_LENGTH, SLIDE_WIDTH, SLIDE_HEIGHT = 0.174, 0.0255, 0.027
SLIDE_REAR_Z = -0.044                       # behind the web point
SLIDE_BOTTOM_Y = 0.013                      # above the web point: the frame's rails
BORE_Y = SLIDE_BOTTOM_Y + 0.0135
GRIP_HEIGHT, GRIP_DEPTH, GRIP_WIDTH = 0.098, 0.050, 0.031
GRIP_RAKE_DEGREES = 18.0                    # the grip leans back going down
GRIP_TOP_Z = -0.012                         # the backstrap's top, just under the beavertail
TRIGGER_GUARD_FRONT_Z, TRIGGER_GUARD_BOTTOM_Y, GUARD_BAR = 0.078, -0.044, 0.006
FRAME_FRONT_Z = 0.118                       # the dust cover ends short of the muzzle
MAG_HEIGHT, MAG_DEPTH, MAG_WIDTH = 0.108, 0.040, 0.0225
MAG_BASEPLATE = (0.034, 0.008, 0.048)       # x, y, z
BARREL_RADIUS, BARREL_FROM_Z, BARREL_TO_Z = 0.0078, 0.040, SLIDE_REAR_Z + SLIDE_LENGTH + 0.003
PORT_FROM_Z, PORT_TO_Z = 0.052, 0.100       # the ejection port on the right side of the slide
SERRATIONS = 6
TRIGGER_PIVOT = (0.0, 0.004, 0.038)         # inside the frame, above the trigger
TRIGGER_LENGTH = 0.030

# Where each part's origin sits in the gun's frame (Unity metres at SCALE): the bones in glock_rig.py and the prefab's parts share these.
PART_ORIGINS = {
    "Slide": (0.0, SLIDE_BOTTOM_Y * SCALE, SLIDE_REAR_Z * SCALE),
    "Magazine": None,       # the mag well's mouth: filled in below from the grip's rake
    "Trigger": tuple(c * SCALE for c in TRIGGER_PIVOT),
    "Barrel": (0.0, BORE_Y * SCALE, BARREL_FROM_Z * SCALE),
}


def grip_axis():
    """Unit vector down the grip (Unity frame): down and back by the rake."""
    r = math.radians(GRIP_RAKE_DEGREES)
    return Vector((0.0, -math.cos(r), -math.sin(r)))


def grip_bottom():
    """The mag well's mouth, Unity metres at SCALE."""
    top = Vector((0.0, 0.0, GRIP_TOP_Z - GRIP_DEPTH * 0.5))
    return (top + grip_axis() * GRIP_HEIGHT) * SCALE


PART_ORIGINS["Magazine"] = tuple(grip_bottom())

# THE WEB POINT: the top of the backstrap under the beavertail, where the hand's thumb-index crotch sits. The exported model's ORIGIN is moved
# here (ORIGIN_SHIFT) so that the game's one pistol-grip hold (FirearmProfile.HoldPosition) puts every gun's web in the hand's web.
WEB = Vector((0.0, -0.004, -0.060)) * SCALE
ORIGIN_SHIFT = -WEB

# Slide travel when racked (real: ~45 mm), the magazine's out-of-the-grip travel, the trigger's pull, all at SCALE / in degrees.
SLIDE_TRAVEL = 0.045 * SCALE
MAG_TRAVEL = (MAG_HEIGHT + 0.03) * SCALE
TRIGGER_PULL_DEGREES = 14.0

# What the reload scripts and the game need to know, in the EXPORTED frame (origin at the web), Unity metres.
POINTS = {
    "muzzle": tuple(Vector((0.0, BORE_Y, BARREL_TO_Z)) * SCALE + ORIGIN_SHIFT),
    "ejection_port": tuple(Vector((SLIDE_WIDTH * 0.5, BORE_Y + 0.004, (PORT_FROM_Z + PORT_TO_Z) * 0.5)) * SCALE + ORIGIN_SHIFT),
    "mag_mouth": tuple(grip_bottom() + ORIGIN_SHIFT),
    "mag_axis_out": tuple(grip_axis()),
    "mag_travel": MAG_TRAVEL,
    "slide_travel": SLIDE_TRAVEL,
    "slide_rear_top": tuple(Vector((0.0, SLIDE_BOTTOM_Y + SLIDE_HEIGHT, SLIDE_REAR_Z + 0.02)) * SCALE + ORIGIN_SHIFT),
    "mag_release": tuple(Vector((-GRIP_WIDTH * 0.5 - 0.004, -0.018, -0.004)) * SCALE + ORIGIN_SHIFT),
    "trigger_pivot": tuple(Vector(TRIGGER_PIVOT) * SCALE + ORIGIN_SHIFT),
    "trigger_pull_degrees": TRIGGER_PULL_DEGREES,
    "grip_bottom": tuple(grip_bottom() + ORIGIN_SHIFT),
    "scale": SCALE,
}
GUN_NAME = "Glock"
PARTS = ("Slide", "Magazine", "Trigger")   # the parts the reload animates and the game drives


def U(v):
    """Unity (x right, y up, z forward) -> Blender (the character faces -Y, its left is +X, up is +Z)."""
    return Vector((-v[0], -v[2], v[1]))


def material(name, rgb, metal, rough):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    m.diffuse_color = (*rgb, 1.0)
    return m


POLYMER = ("GlockPolymer", (0.055, 0.055, 0.06), 0.0, 0.85)
STEEL = ("GlockSteel", (0.10, 0.105, 0.115), 0.9, 0.42)
BARREL_STEEL = ("GlockBarrel", (0.32, 0.33, 0.35), 1.0, 0.3)
MAG_STEEL = ("GlockMag", (0.22, 0.23, 0.25), 0.9, 0.5)
DOT = ("GlockSightDot", (0.95, 0.95, 0.9), 0.0, 0.5)


class Part:
    """One mesh being built: boxes and cylinders in the gun's Unity frame, relative to the part's origin."""

    def __init__(self, name, origin, mat):
        self.name = name
        self.origin = Vector(origin)
        self.bm = bmesh.new()
        self.mat = mat
        self.extra = []   # (material, [faces]) for a second material

    def box(self, centre, size, rake=0.0, faces_out=None):
        """An axis-aligned box (Unity frame, metres at SCALE, relative to the origin); `rake` shears it back going down (degrees)."""
        cx, cy, cz = centre
        sx, sy, sz = size
        t = math.tan(math.radians(rake))
        verts = []
        for dx in (-1, 1):
            for dy in (-1, 1):
                for dz in (-1, 1):
                    y = cy + dy * sy * 0.5
                    z = cz + dz * sz * 0.5 - (y - cy) * t   # higher is further forward
                    verts.append(self.bm.verts.new(U((cx + dx * sx * 0.5, y, z))))
        faces = []
        for f in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)):
            try:
                faces.append(self.bm.faces.new([verts[j] for j in f]))
            except ValueError:
                pass
        if faces_out is not None:
            faces_out.extend(faces)
        return faces

    def cylinder(self, start, end, radius, segments=16):
        """A cylinder between two points (Unity frame)."""
        a, b = Vector(start), Vector(end)
        axis = (b - a).normalized()
        side = axis.orthogonal().normalized()
        up = axis.cross(side)
        ring_a, ring_b = [], []
        for i in range(segments):
            ang = 2.0 * math.pi * i / segments
            off = (side * math.cos(ang) + up * math.sin(ang)) * radius
            ring_a.append(self.bm.verts.new(U(a + off)))
            ring_b.append(self.bm.verts.new(U(b + off)))
        for i in range(segments):
            j = (i + 1) % segments
            self.bm.faces.new((ring_a[i], ring_a[j], ring_b[j], ring_b[i]))
        self.bm.faces.new(ring_a[::-1])
        self.bm.faces.new(ring_b)

    def profile_extrude(self, profile, z_from, z_to):
        """A prism from a closed 2D profile (x, y) in the gun's frame, extruded from z_from to z_to."""
        back = [self.bm.verts.new(U((x, y, z_from))) for x, y in profile]
        front = [self.bm.verts.new(U((x, y, z_to))) for x, y in profile]
        n = len(profile)
        for i in range(n):
            j = (i + 1) % n
            self.bm.faces.new((back[i], back[j], front[j], front[i]))
        self.bm.faces.new(back[::-1])
        self.bm.faces.new(front)

    def finish(self, parent):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        for slot, (mat, faces) in enumerate(self.extra, start=1):
            for f in faces:
                if f.is_valid:
                    f.material_index = slot
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        mesh.materials.append(material(*self.mat))
        for mat, faces in self.extra:
            mesh.materials.append(material(*mat))
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        obj.parent = parent
        obj.location = U(self.origin + ORIGIN_SHIFT)
        for p in mesh.polygons:
            p.use_smooth = False
        return obj


def slide_profile(port=False):
    """The slide's cross-section (x, y): a box with chamfered top corners; with the port, the right top corner is cut away down to the bore."""
    w, h, c = SLIDE_WIDTH * 0.5 * SCALE, SLIDE_HEIGHT * SCALE, 0.004 * SCALE
    bottom = SLIDE_BOTTOM_Y * SCALE
    top = bottom + h
    pts = [(-w, bottom), (w, bottom)]
    if port:
        pts += [(w, BORE_Y * SCALE - 0.002 * SCALE), (0.004 * SCALE, BORE_Y * SCALE - 0.002 * SCALE), (0.004 * SCALE, top)]
    else:
        pts += [(w, top - c), (w - c, top)]
    pts += [(-w + c, top), (-w, top - c)]
    return pts


def build_glock(parent):
    s = SCALE
    g = grip_axis()
    # ---- FRAME: dust cover and rails under the slide, the block above the grip with the beavertail, the raked grip, the trigger guard.
    frame = Part("Frame", (0, 0, 0), POLYMER)
    rail_h = SLIDE_BOTTOM_Y
    frame.box((0.0, rail_h * 0.5 * s, (FRAME_FRONT_Z + 0.005) * 0.5 * s), (SLIDE_WIDTH * 0.9 * s, rail_h * s, (FRAME_FRONT_Z - 0.005) * s))
    # the rear block (holds the slide's rails, behind the trigger) and the beavertail tang
    frame.box((0.0, 0.0, -0.016 * s), (GRIP_WIDTH * s, 0.026 * s, 0.070 * s))
    frame.box((0.0, 0.003 * s, -0.060 * s), (GRIP_WIDTH * 0.8 * s, 0.012 * s, 0.022 * s))
    # the grip: a raked box from the top of the backstrap down, finger-groove bumps on the front strap
    grip_top = Vector((0.0, 0.0, GRIP_TOP_Z - GRIP_DEPTH * 0.5))
    grip_centre = (grip_top + g * (GRIP_HEIGHT * 0.5)) * s
    frame.box(tuple(grip_centre), (GRIP_WIDTH * s, GRIP_HEIGHT * s, GRIP_DEPTH * s), rake=GRIP_RAKE_DEGREES)
    for i in range(3):
        along = GRIP_HEIGHT * (0.30 + 0.2 * i)
        c = (grip_top + g * along + Vector((0.0, 0.0, GRIP_DEPTH * 0.5))) * s
        frame.box(tuple(c), (GRIP_WIDTH * 0.9 * s, 0.012 * s, 0.006 * s), rake=GRIP_RAKE_DEGREES)
    # the trigger guard: a bottom bar and a front bar (the frame is its top)
    guard_z0 = GRIP_TOP_Z + 0.010
    frame.box((0.0, TRIGGER_GUARD_BOTTOM_Y * s, (guard_z0 + TRIGGER_GUARD_FRONT_Z) * 0.5 * s),
              (0.012 * s, GUARD_BAR * s, (TRIGGER_GUARD_FRONT_Z - guard_z0 + GUARD_BAR) * s))
    frame.box((0.0, (TRIGGER_GUARD_BOTTOM_Y + 0.0) * 0.5 * s, TRIGGER_GUARD_FRONT_Z * s), (0.012 * s, (-TRIGGER_GUARD_BOTTOM_Y + 0.004) * s, GUARD_BAR * s))
    # the mag release (left side) and the slide stop lever (left side) are frame details
    frame.box((-(GRIP_WIDTH * 0.5 + 0.002) * s, -0.018 * s, -0.004 * s), (0.005 * s, 0.012 * s, 0.009 * s))
    frame.box((-(SLIDE_WIDTH * 0.5 + 0.0015) * s, (SLIDE_BOTTOM_Y + 0.004) * s, 0.012 * s), (0.003 * s, 0.006 * s, 0.040 * s))
    frame_obj = frame.finish(parent)

    # ---- SLIDE: three extrusions (rear, port, front), chamfered top, rear serrations, both sights. Origin: its rear bottom centre.
    slide = Part("Slide", PART_ORIGINS["Slide"], STEEL)
    o = Vector(PART_ORIGINS["Slide"])

    def rel(profile):
        return [(x, y - o.y) for x, y in profile]
    slide.profile_extrude(rel(slide_profile()), 0.0, (PORT_FROM_Z - SLIDE_REAR_Z) * s)
    slide.profile_extrude(rel(slide_profile(port=True)), (PORT_FROM_Z - SLIDE_REAR_Z) * s, (PORT_TO_Z - SLIDE_REAR_Z) * s)
    slide.profile_extrude(rel(slide_profile()), (PORT_TO_Z - SLIDE_REAR_Z) * s, SLIDE_LENGTH * s)
    for i in range(SERRATIONS):
        z = (0.012 + i * 0.0055) * s
        for side in (-1, 1):
            slide.box((side * (SLIDE_WIDTH * 0.5 + 0.0004) * s, SLIDE_HEIGHT * 0.5 * s, z), (0.0012 * s, SLIDE_HEIGHT * 0.75 * s, 0.0016 * s))
    # the sights (a second material for the white dots)
    dots = []
    slide.box((0.0, (SLIDE_HEIGHT + 0.003) * s, (SLIDE_LENGTH - 0.012) * s), (0.004 * s, 0.006 * s, 0.006 * s))
    slide.box((0.0, (SLIDE_HEIGHT + 0.0062) * s, (SLIDE_LENGTH - 0.0095) * s), (0.0025 * s, 0.0025 * s, 0.0012 * s), faces_out=dots)
    for side in (-1, 1):
        slide.box((side * 0.0062 * s, (SLIDE_HEIGHT + 0.0035) * s, 0.010 * s), (0.0075 * s, 0.007 * s, 0.009 * s))
        slide.box((side * 0.0062 * s, (SLIDE_HEIGHT + 0.0035) * s, 0.0052 * s), (0.0025 * s, 0.0025 * s, 0.0012 * s), faces_out=dots)
    slide.extra.append((DOT, dots))
    slide_obj = slide.finish(parent)

    # ---- BARREL: a cylinder on the bore from the chamber to just past the slide's nose. Origin: the chamber end.
    barrel = Part("Barrel", PART_ORIGINS["Barrel"], BARREL_STEEL)
    barrel.cylinder((0.0, 0.0, 0.0), (0.0, 0.0, (BARREL_TO_Z - BARREL_FROM_Z) * s), BARREL_RADIUS * s, 20)
    barrel.cylinder((0.0, 0.0, (BARREL_TO_Z - BARREL_FROM_Z) * s - 0.002 * s), (0.0, 0.0, (BARREL_TO_Z - BARREL_FROM_Z) * s + 0.0005 * s),
                    BARREL_RADIUS * 0.6 * s, 20)   # the bore's mouth (a dark inset would need a hole; a smaller nose reads as one)
    barrel.finish(parent)

    # ---- MAGAZINE: the body up the grip's inside and the baseplate below it. Origin: the mag well's mouth; +gripAxis is OUT.
    mag = Part("Magazine", PART_ORIGINS["Magazine"], MAG_STEEL)
    body_centre = -g * (MAG_HEIGHT * 0.5 - 0.004) * s
    mag.box(tuple(body_centre), (MAG_WIDTH * s, MAG_HEIGHT * s, MAG_DEPTH * s), rake=GRIP_RAKE_DEGREES)
    plate_centre = g * (MAG_BASEPLATE[1] * 0.5 + 0.001) * s
    mag.box(tuple(plate_centre), tuple(c * s for c in MAG_BASEPLATE), rake=GRIP_RAKE_DEGREES)
    mag.finish(parent)

    # ---- TRIGGER: a curved blade hanging from its pivot inside the frame, with the safety tab. Origin: the pivot; it turns about +X.
    trig = Part("Trigger", PART_ORIGINS["Trigger"], POLYMER)
    for i in range(4):
        t = (i + 0.5) / 4.0
        y = -TRIGGER_LENGTH * t
        z = -0.010 * t * t          # curving back toward the shooter
        trig.box((0.0, y * s, z * s), (0.007 * s, (TRIGGER_LENGTH / 4.0 + 0.001) * s, 0.0055 * s))
    trig.box((0.0, -TRIGGER_LENGTH * 0.55 * s, -0.0045 * s), (0.0025 * s, 0.012 * s, 0.003 * s))   # the safety blade
    trig.finish(parent)
    return frame_obj, slide_obj


def build(export=True):
    root = bpy.data.objects.get(GUN_NAME)
    if root is None:
        root = bpy.data.objects.new(GUN_NAME, None)
        bpy.context.scene.collection.objects.link(root)
    build_glock(root)
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


if globals().get("GLOCK_STANDALONE", __name__ == "__main__"):
    build(export=True)
    print("Glock built")
