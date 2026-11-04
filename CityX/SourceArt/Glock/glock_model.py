# THE GLOCK, traced from a game's Glock (owner, 2026-11-03: "research how a glock looks and make it like that", "look at counter strike glock",
# "its a pretty simple design ours is all jacked up", "keep ours all black"). The first model (2026-10-14) was boxes guessed from a Glock 19's
# figures: the grip hung off the back as a long slab, the trigger guard was a loose wire, the frame under the slide a thin plate. This one is the
# Counter-Strike 2 Glock-18's SIDE SILHOUETTE, measured off the reference picture in millimetres about the web of the grip, and built as outlines
# extruded across the gun's width: the frame with its grip, hump and tang as one outline, the slide as its cross-section run along the gun with
# the nose bevelled, the trigger guard as two bars, the trigger as a curved blade. Still SEPARATE PARTS so the game animates them: the slide
# cycles, the magazine drops out, the trigger pulls.
#
# Run inside Blender (headless: blender --background --python glock_build_all.py -- --model), or exec(open(r"<this file>").read()).
# Writes CityX/Assets/CityX/Weapons/Glock.glb and Glock.points.json.
#
# AXES. Everything is authored in the gun's own Unity frame -- x the shooter's right, y up, z the muzzle -- and put into Blender with U() (the
# character faces -Y in Blender, up is +Z, its left is +X). Blender's glTF export (Y up) then glTFast's import (x mirrored) bring a point back to
# the same Unity coordinates it was authored in.
# ORIGIN. The gun's origin is the WEB OF THE HAND: the top of the backstrap where the hand's thumb/index crotch sits under the beavertail. The hold
# in the game (FirearmProfile.HoldPosition) puts this point at the hand's web, so the hand placement is one offset. The outlines below are
# measured from it directly, so nothing is shifted on export (ORIGIN_SHIFT is zero; kept for the reload script, which adds it).
# PARTS. Frame (polymer: grip, trigger guard, dust cover, levers and pins), Slide (ejection port cut, rear serrations, sights), Barrel (showing in
# the port and at the muzzle), Magazine (origin at the mag well's mouth: moving it down -gripAxis pulls it out; its baseplate), Trigger (origin at
# its pivot). PART_ORIGINS is shared with the reload (glock_reload.py reads POINTS) and FirearmBuilder (the prefab's parts, by name).

import bpy
import bmesh
import json
import math
import os
from mathutils import Vector

# The repository's root: from this file's place (SourceArt/<part>/) when run as a file, else from CITYX_REPO (code pasted into a live
# Blender has no file), else the owner's checkout.
REPO = (os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..")) if "__file__" in globals()
        else os.environ.get("CITYX_REPO", r"C:\Users\Lenovo\Downloads\the game"))
GLB = os.path.join(REPO, "CityX", "Assets", "CityX", "Weapons", "Glock.glb")

# A real pistol in metres, times this: the body is 2.6 m and its hands are wide, so a real-size pistol was a nub in them. 1.9 fits the hand:
# three fingers on the grip and the little finger under the baseplate, as a compact pistol sits in a big hand (checked in the Blender renders).
SCALE = 2.5
MM = 0.001 * SCALE   # a millimetre of the reference, in the model's metres

# THE SILHOUETTE, millimetres about the web (z forward to the muzzle, y up), measured off the Counter-Strike 2 Glock-18 picture scaled so the
# slide is a Glock 17's 186 mm long. Seen from the gun's left; x is its width.
SLIDE_REAR, SLIDE_FRONT, SLIDE_BOTTOM, SLIDE_TOP, SLIDE_WIDTH = -3.0, 183.0, 9.0, 35.0, 25.5
SLIDE_CHAMFER = 3.0                  # the top edges are eased
NOSE_LENGTH, NOSE_DROP = 5.0, 4.5    # the top front edge comes down over the last few millimetres
FRAME_WIDTH = 30.0
# The frame and grip as one outline, going round from the dust cover's front: under the slide, the frame's underside to the trigger guard's rear,
# down the front strap (the trigger guard's rear edge first), the grip's bottom, up the backstrap with its hump, the tang of the beavertail.
# The grip is the owner's own red line over the first trace (2026-11-03, "can you make it shaped more like this"): a hollow under the tang where
# the web sits, the backstrap swelling out low down, a rounded bottom, the front strap curving down from the trigger guard's join.
FRAME_OUTLINE = [(181, 9), (181, -2), (117, -5), (76, -5), (73, -28), (64.5, -30), (61, -40), (54, -55), (47, -74), (44, -86.5), (36, -86),
                 (22, -83), (8, -78), (-1, -68), (-1, -53), (7, -41), (16, -24), (13, -6), (8, 2), (-5.5, 6), (-3, 9)]
# The trigger guard as one band: down its front, along its rounded bottom into the grip's join, and back along the hole's edge.
GUARD_WIDTH = 12.0
GUARD_OUTLINE = [(121, -5), (121, -26), (119, -30), (115, -33), (76, -33), (69, -32), (64.5, -30), (73, -28), (113, -28), (115, -26), (116, -8), (116, -5)]
MAG_MOUTH = (25.0, -84.0)                        # z, y: the grip's bottom, its middle
MAG_RAKE_DEGREES = 22.5                          # the magazine runs parallel to the front strap, leaning back going down
MAG_WIDTH, MAG_DEPTH, MAG_HEIGHT = 23.0, 40.0, 108.0
BASEPLATE_WIDTH, BASEPLATE_DEPTH, BASEPLATE_TALL, BASEPLATE_FORWARD = 26.0, 42.0, 8.0, 0.0
BASEPLATE_TILT_DEGREES = 12.0                    # lower at the front, along the grip's bottom (owner: "tilted and aligned with the bottom")
BORE_Y = 22.5
BARREL_RADIUS, BARREL_REAR, BARREL_FRONT = 7.8, 60.0, 185.0
PORT_REAR, PORT_FRONT = 70.0, 110.0              # the ejection port, on the right side of the slide
SERRATION_COUNT, SERRATION_FIRST, SERRATION_SPACING, SERRATION_BOTTOM, SERRATION_TOP = 7, 11.0, 4.6, 12.0, 31.0
FRONT_SIGHT_Z, FRONT_SIGHT_WIDTH, FRONT_SIGHT_TALL = 175.0, 4.0, 2.2
REAR_SIGHT_Z, REAR_SIGHT_LONG, REAR_SIGHT_TALL, REAR_SIGHT_NOTCH = 12.5, 5.0, 3.0, 3.0
TRIGGER_PIVOT_MM = (0.0, 0.0, 84.0)
TRIGGER_OUTLINE = [(0, 0), (4, -3), (3, -13), (-1, -23), (-5, -27), (-8, -22), (-5, -11), (-3, -2)]   # about the pivot: a blade bowing back
TRIGGER_WIDTH = 7.0
# The levers on the left side (the shooter's left is -x) and the pins through the frame: (centre z, y), (long, tall), proud of the frame.
SLIDE_STOP = ((60.0, 7.5), (12.0, 5.0), 2.0)
TAKEDOWN_LEVER = ((100.0, 3.5), (4.0, 7.0), 1.5)
MAG_RELEASE = ((60.0, -26.0), (7.0, 6.0), 3.0)
PINS = ((84.0, 0.0), (99.0, 1.5))
PIN_RADIUS = 2.5


def grip_axis():
    """Unit vector down the grip (Unity frame): down and back by the rake."""
    r = math.radians(MAG_RAKE_DEGREES)
    return Vector((0.0, -math.cos(r), -math.sin(r)))


def grip_bottom():
    """The mag well's mouth, model metres."""
    return Vector((0.0, MAG_MOUTH[1], MAG_MOUTH[0])) * MM


def mm(x, y, z):
    return Vector((x, y, z)) * MM


# Where each part's origin sits in the gun's frame (model metres): the prefab's parts and the reload share these.
PART_ORIGINS = {
    "Slide": tuple(mm(0.0, SLIDE_BOTTOM, SLIDE_REAR)),
    "Magazine": tuple(grip_bottom()),
    "Trigger": tuple(mm(*TRIGGER_PIVOT_MM)),
    "Barrel": tuple(mm(0.0, BORE_Y, BARREL_REAR)),
}

# THE WEB POINT is the origin of the outlines above, so the exported model needs no shift (the reload script adds ORIGIN_SHIFT to its points).
WEB = Vector((0.0, 0.0, 0.0))
ORIGIN_SHIFT = -WEB

# Slide travel when racked (real: ~45 mm), the magazine's out-of-the-grip travel, the trigger's pull, all in model metres / degrees.
SLIDE_TRAVEL = 45.0 * MM
MAG_TRAVEL = (MAG_HEIGHT + 30.0) * MM
TRIGGER_PULL_DEGREES = 14.0

# What the reload scripts and the game need to know, in the EXPORTED frame (origin at the web), model metres.
POINTS = {
    "muzzle": tuple(mm(0.0, BORE_Y, BARREL_FRONT) + ORIGIN_SHIFT),
    "ejection_port": tuple(mm(SLIDE_WIDTH * 0.5, BORE_Y + 4.0, (PORT_REAR + PORT_FRONT) * 0.5) + ORIGIN_SHIFT),
    "mag_mouth": tuple(grip_bottom() + ORIGIN_SHIFT),
    "mag_axis_out": tuple(grip_axis()),
    "mag_travel": MAG_TRAVEL,
    "slide_travel": SLIDE_TRAVEL,
    "slide_rear_top": tuple(mm(0.0, SLIDE_TOP, SLIDE_REAR + 20.0) + ORIGIN_SHIFT),
    "mag_release": tuple(mm(-(FRAME_WIDTH * 0.5 + MAG_RELEASE[2]), MAG_RELEASE[0][1], MAG_RELEASE[0][0]) + ORIGIN_SHIFT),
    "trigger_pivot": tuple(mm(*TRIGGER_PIVOT_MM) + ORIGIN_SHIFT),
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


# All black (owner): the polymer frame, the nitrided slide, a little lighter steel for the barrel and the pins, the magazine, the sights' white dots.
POLYMER = ("GlockPolymer", (0.055, 0.055, 0.06), 0.0, 0.85)
STEEL = ("GlockSteel", (0.10, 0.105, 0.115), 0.9, 0.42)
BARREL_STEEL = ("GlockBarrel", (0.32, 0.33, 0.35), 1.0, 0.3)
MAG_STEEL = ("GlockMag", (0.22, 0.23, 0.25), 0.9, 0.5)
DOT = ("GlockSightDot", (0.95, 0.95, 0.9), 0.0, 0.5)


class Part:
    """One mesh being built in the gun's Unity frame, model metres, relative to the part's origin."""

    def __init__(self, name, origin, mat):
        self.name = name
        self.origin = Vector(origin)
        self.bm = bmesh.new()
        self.mat = mat
        self.extra = []   # (material, [faces]) for further materials

    def box(self, centre, size, faces_out=None):
        """An axis-aligned box."""
        cx, cy, cz = centre
        sx, sy, sz = size
        verts = []
        for dx in (-1, 1):
            for dy in (-1, 1):
                for dz in (-1, 1):
                    verts.append(self.bm.verts.new(U((cx + dx * sx * 0.5, cy + dy * sy * 0.5, cz + dz * sz * 0.5))))
        faces = []
        for f in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)):
            faces.append(self.bm.faces.new([verts[j] for j in f]))
        if faces_out is not None:
            faces_out.extend(faces)
        return faces

    def cylinder(self, start, end, radius, segments=16, faces_out=None):
        """A cylinder between two points."""
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
        faces = []
        for i in range(segments):
            j = (i + 1) % segments
            faces.append(self.bm.faces.new((ring_a[i], ring_a[j], ring_b[j], ring_b[i])))
        faces.append(self.bm.faces.new(ring_a[::-1]))
        faces.append(self.bm.faces.new(ring_b))
        if faces_out is not None:
            faces_out.extend(faces)
        return faces

    def loft(self, profile_a, z_a, profile_b, z_b):
        """A prism between two closed cross-sections (x, y) of the same point count, at z_a and z_b."""
        back = [self.bm.verts.new(U((x, y, z_a))) for x, y in profile_a]
        front = [self.bm.verts.new(U((x, y, z_b))) for x, y in profile_b]
        n = len(profile_a)
        for i in range(n):
            j = (i + 1) % n
            self.bm.faces.new((back[i], back[j], front[j], front[i]))
        self.bm.faces.new(back[::-1])
        self.bm.faces.new(front)

    def profile_extrude(self, profile, z_from, z_to):
        """A prism from a closed cross-section (x, y), extruded along the gun from z_from to z_to."""
        self.loft(profile, z_from, profile, z_to)

    def side(self, outline, x_from, x_to, faces_out=None):
        """A plate from a closed SIDE outline (z, y), the silhouette seen from the gun's left, across the width from x_from to x_to."""
        left = [self.bm.verts.new(U((x_from, y, z))) for z, y in outline]
        right = [self.bm.verts.new(U((x_to, y, z))) for z, y in outline]
        n = len(outline)
        faces = []
        for i in range(n):
            j = (i + 1) % n
            faces.append(self.bm.faces.new((left[i], left[j], right[j], right[i])))
        faces.append(self.bm.faces.new(left[::-1]))
        faces.append(self.bm.faces.new(right))
        if faces_out is not None:
            faces_out.extend(faces)
        return faces

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


def scaled(outline):
    return [(z * MM, y * MM) for z, y in outline]


def slide_profile(port=False, top=SLIDE_TOP):
    """The slide's cross-section (x, y) in the slide's own frame (its rear bottom centre): a box with eased top corners; with the port, the
    right top corner is cut away down to the bore."""
    w, c = SLIDE_WIDTH * 0.5 * MM, SLIDE_CHAMFER * MM
    h = (top - SLIDE_BOTTOM) * MM
    bore = (BORE_Y - SLIDE_BOTTOM) * MM
    pts = [(-w, 0.0), (w, 0.0)]
    if port:
        pts += [(w, bore - 2.0 * MM), (4.0 * MM, bore - 2.0 * MM), (4.0 * MM, h)]
    else:
        pts += [(w, h - c), (w - c, h)]
    pts += [(-w + c, h), (-w, h - c)]
    return pts


def build_glock(parent):
    # ---- FRAME: the traced outline across the frame's width; the trigger guard's two bars; the levers on the left; the pins through it.
    frame = Part("Frame", (0.0, 0.0, 0.0), POLYMER)
    half = FRAME_WIDTH * 0.5 * MM
    frame.side(scaled(FRAME_OUTLINE), -half, half)
    frame.side(scaled(GUARD_OUTLINE), -GUARD_WIDTH * 0.5 * MM, GUARD_WIDTH * 0.5 * MM)
    for (cz, cy), (long, tall), proud in (SLIDE_STOP, TAKEDOWN_LEVER, MAG_RELEASE):
        frame.box(tuple(mm(-(FRAME_WIDTH * 0.5 + proud * 0.5), cy, cz)), tuple(mm(proud + 0.5, tall, long)))
    pins = []
    for cz, cy in PINS:
        frame.cylinder(mm(-(FRAME_WIDTH * 0.5 + 0.5), cy, cz), mm(FRAME_WIDTH * 0.5 + 0.5, cy, cz), PIN_RADIUS * MM, 12, faces_out=pins)
    frame.extra.append((STEEL, pins))
    frame_obj = frame.finish(parent)

    # ---- SLIDE: its cross-section run along the gun in three lengths (rear, the port, front), the nose bevelled, rear serrations, both sights.
    # Origin: its rear bottom centre.
    slide = Part("Slide", PART_ORIGINS["Slide"], STEEL)
    port_rear, port_front = (PORT_REAR - SLIDE_REAR) * MM, (PORT_FRONT - SLIDE_REAR) * MM
    nose_start, nose_end = (SLIDE_FRONT - NOSE_LENGTH - SLIDE_REAR) * MM, (SLIDE_FRONT - SLIDE_REAR) * MM
    slide.profile_extrude(slide_profile(), 0.0, port_rear)
    slide.profile_extrude(slide_profile(port=True), port_rear, port_front)
    slide.profile_extrude(slide_profile(), port_front, nose_start)
    slide.loft(slide_profile(), nose_start, slide_profile(top=SLIDE_TOP - NOSE_DROP), nose_end)
    height = (SLIDE_TOP - SLIDE_BOTTOM) * MM
    for i in range(SERRATION_COUNT):
        z = (SERRATION_FIRST + i * SERRATION_SPACING - SLIDE_REAR) * MM
        for sign in (-1, 1):
            slide.box((sign * (SLIDE_WIDTH * 0.5 + 0.2) * MM, (SERRATION_BOTTOM + SERRATION_TOP) * 0.5 * MM - SLIDE_BOTTOM * MM, z),
                      (0.6 * MM, (SERRATION_TOP - SERRATION_BOTTOM) * MM, 1.6 * MM))
    dots = []
    slide.box((0.0, height + FRONT_SIGHT_TALL * 0.5 * MM, (FRONT_SIGHT_Z - SLIDE_REAR) * MM), (FRONT_SIGHT_WIDTH * MM, FRONT_SIGHT_TALL * MM, 4.0 * MM))
    slide.box((0.0, height + (FRONT_SIGHT_TALL + 0.8) * MM, (FRONT_SIGHT_Z - 2.3 - SLIDE_REAR) * MM), (2.0 * MM, 2.0 * MM, 0.8 * MM), faces_out=dots)
    rear_z = (REAR_SIGHT_Z - SLIDE_REAR) * MM
    for sign in (-1, 1):
        x = sign * (REAR_SIGHT_NOTCH * 0.5 + (SLIDE_WIDTH * 0.5 - REAR_SIGHT_NOTCH * 0.5) * 0.5) * MM
        slide.box((x, height + REAR_SIGHT_TALL * 0.5 * MM, rear_z), ((SLIDE_WIDTH * 0.5 - REAR_SIGHT_NOTCH * 0.5) * MM, REAR_SIGHT_TALL * MM, REAR_SIGHT_LONG * MM))
        slide.box((sign * 5.0 * MM, height + REAR_SIGHT_TALL * 0.55 * MM, rear_z - (REAR_SIGHT_LONG * 0.5 + 0.3) * MM), (2.0 * MM, 2.0 * MM, 0.8 * MM), faces_out=dots)
    slide.extra.append((DOT, dots))
    slide_obj = slide.finish(parent)

    # ---- BARREL: a cylinder on the bore from the chamber to the muzzle, a smaller nose for the bore's mouth. Origin: the chamber end.
    barrel = Part("Barrel", PART_ORIGINS["Barrel"], BARREL_STEEL)
    length = (BARREL_FRONT - BARREL_REAR) * MM
    barrel.cylinder((0.0, 0.0, 0.0), (0.0, 0.0, length), BARREL_RADIUS * MM, 20)
    barrel.cylinder((0.0, 0.0, length - 2.0 * MM), (0.0, 0.0, length + 0.5 * MM), BARREL_RADIUS * 0.6 * MM, 20)
    barrel.finish(parent)

    # ---- MAGAZINE: the body up the grip's inside along the rake, and the baseplate under the grip. Origin: the mag well's mouth; +gripAxis is OUT.
    mag = Part("Magazine", PART_ORIGINS["Magazine"], MAG_STEEL)
    r = math.radians(MAG_RAKE_DEGREES)
    up = (math.sin(r), math.cos(r))          # (z, y) up the magazine
    across = (math.cos(r), -math.sin(r))     # (z, y) toward the front strap
    d = MAG_DEPTH * 0.5
    body = [(-across[0] * d, -across[1] * d), (across[0] * d, across[1] * d),
            (across[0] * d + up[0] * MAG_HEIGHT, across[1] * d + up[1] * MAG_HEIGHT), (-across[0] * d + up[0] * MAG_HEIGHT, -across[1] * d + up[1] * MAG_HEIGHT)]
    mag.side(scaled(body), -MAG_WIDTH * 0.5 * MM, MAG_WIDTH * 0.5 * MM)
    plate = []
    t = math.radians(BASEPLATE_TILT_DEGREES)
    along = (math.cos(t), -math.sin(t))       # (z, y) along the plate toward the front, falling with the grip's bottom
    down = (-math.sin(t), -math.cos(t))
    bd = BASEPLATE_DEPTH * 0.5
    corners = []
    for a, d in ((BASEPLATE_FORWARD - bd, -3.0), (BASEPLATE_FORWARD + bd, -3.0), (BASEPLATE_FORWARD + bd, BASEPLATE_TALL), (BASEPLATE_FORWARD - bd, BASEPLATE_TALL)):
        corners.append((along[0] * a + down[0] * d, along[1] * a + down[1] * d))   # the top edge sits up into the grip, so no gap shows under its rounded back
    mag.side(scaled(corners), -BASEPLATE_WIDTH * 0.5 * MM, BASEPLATE_WIDTH * 0.5 * MM, faces_out=plate)
    mag.extra.append((POLYMER, plate))
    mag.finish(parent)

    # ---- TRIGGER: a curved blade hanging from its pivot inside the frame. Origin: the pivot; it turns about +X.
    trig = Part("Trigger", PART_ORIGINS["Trigger"], POLYMER)
    trig.side(scaled(TRIGGER_OUTLINE), -TRIGGER_WIDTH * 0.5 * MM, TRIGGER_WIDTH * 0.5 * MM)
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
