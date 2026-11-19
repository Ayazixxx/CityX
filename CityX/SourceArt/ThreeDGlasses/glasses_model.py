# THE 3D GLASSES, built from flat outlines (owner, 2026-11-19: "make a 3D Glasses model and equippable item. make it shaped like the black one
# but white ingame. make the blue and red part's transparent", with two pictures of paper anaglyph glasses, the second a black pair). Made from
# scratch after the black pair's shape: a flat front with two rounded lens windows and a rounded notch for the nose, and arms that stand full
# height at the hinge, sweep up into a band and hook down at the end.
#
# PARTS: Frame (the front and both arms, one object), LensRed (before the LEFT eye) and LensBlue (before the RIGHT eye), flat sheets set in the
# middle of the front's thickness.
#
# AXES AND ORIGIN: the Head bone's Unity frame (x the wearer's right, y up, z the way the face looks) in metres, the origin the head's centre
# (the Head bone's pivot), as the hats are made. The head is a ball FusedBodyShape.HeadRadius (0.35 m) round on a 2.6 m body, so the glasses
# are a cartoon's: narrower than the ball, set just off its front at the eyes' height, the arms splaying out round it to behind its widest.
#   "C:/Program Files/Blender Foundation/Blender 4.2/blender.exe" --background --factory-startup --python CityX/SourceArt/ThreeDGlasses/glasses_model.py

import bpy
import bmesh
import math
import os
from mathutils import Matrix, Vector

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
GLB = os.path.join(REPO, "CityX", "Assets", "CityX", "Wearables", "Models", "ThreeDGlasses.glb")

HEAD_RADIUS = 0.35
EYES_Y = 0.04                    # the lenses' middle, above the head's centre
FRONT_GAP = 0.012                # from the ball's front to the back of the front

FRONT = dict(width=0.58, height=0.20, corner=0.03, thickness=0.016)
LENS = dict(width=0.21, height=0.12, corner=0.028, apart=0.29, lift=0.012)   # each window; their middles `apart`; `lift` up the front
LENS_OVERLAP = 0.006             # how far a lens sheet runs into the frame round its window, out of sight in its thickness
NOSE = dict(bottom=0.05, top=0.026, height=0.042)                             # half-widths at the front's lower edge and at the top
ARM = dict(thickness=0.012, band=0.045, sweep=0.12, out_to=0.37, back_to=-0.05, hook_radius=0.06, hook_degrees=70.0, tail=0.05)
BEVEL = 0.0025                   # the edges rounded a little, as the black pair's are

WHITE = ("GlassesWhite", (0.95, 0.95, 0.94, 1.0))
RED = ("GlassesRed", (1.0, 0.1, 0.1, 0.5))
BLUE = ("GlassesBlue", (0.1, 0.3, 1.0, 0.5))

# Unity (x right, y up, z forward) -> Blender (the character faces -Y, its left is +X, up is +Z): the firearms' U().
TO_BLENDER = Matrix(((-1.0, 0.0, 0.0, 0.0), (0.0, 0.0, -1.0, 0.0), (0.0, 1.0, 0.0, 0.0), (0.0, 0.0, 0.0, 1.0)))


def arc(cx, cy, radius, from_deg, to_deg, steps):
    return [(cx + radius * math.cos(math.radians(from_deg + (to_deg - from_deg) * i / steps)),
             cy + radius * math.sin(math.radians(from_deg + (to_deg - from_deg) * i / steps))) for i in range(steps + 1)]


def rounded_rect(cx, cy, w, h, r, steps=6):
    x0, x1, y0, y1 = cx - w / 2, cx + w / 2, cy - h / 2, cy + h / 2
    return (arc(x1 - r, y0 + r, r, 270, 360, steps) + arc(x1 - r, y1 - r, r, 0, 90, steps)
            + arc(x0 + r, y1 - r, r, 90, 180, steps) + arc(x0 + r, y0 + r, r, 180, 270, steps))


def front_outline():
    """The front, round the outside: its rounded corners, the nose notch rising out of the lower edge (narrowing to its top)."""
    w, h, r = FRONT["width"], FRONT["height"], FRONT["corner"]
    points = arc(-w / 2 + r, -h / 2 + r, r, 180, 270, 6)
    for i in range(0, 17):
        a = math.pi * (1.0 - i / 16.0)
        half = NOSE["top"] + (NOSE["bottom"] - NOSE["top"]) * (1.0 - math.sin(a))
        points.append((math.cos(a) * half, -h / 2 + NOSE["height"] * math.sin(a)))
    points += arc(w / 2 - r, -h / 2 + r, r, 270, 360, 6)
    points += arc(w / 2 - r, h / 2 - r, r, 0, 90, 6)
    points += arc(-w / 2 + r, h / 2 - r, r, 90, 180, 6)
    return points


def arm_outline(length):
    """An arm in its own plane (u back from the hinge, v up from the eyes' height): full height at the hinge, the lower edge sweeping up into
    the band along the top, the band bending down at the end and finishing round."""
    h, band = FRONT["height"], ARM["band"]
    top, band_low = h / 2, h / 2 - band
    middle = top - band / 2
    straight = length - ARM["hook_radius"] * math.sin(math.radians(ARM["hook_degrees"]))
    centre = (straight, middle - ARM["hook_radius"])
    inner, outer = ARM["hook_radius"] - band / 2, ARM["hook_radius"] + band / 2
    end_deg = 90.0 - ARM["hook_degrees"]
    tangent = (math.sin(math.radians(end_deg)), -math.cos(math.radians(end_deg)))

    points = [(0.0, -h / 2)]
    for i in range(1, 13):
        s = i / 12.0
        points.append((ARM["sweep"] * s, -h / 2 + (band_low + h / 2) * (s * s * (3.0 - 2.0 * s))))
    points.append((straight, band_low))
    points += arc(centre[0], centre[1], inner, 90.0, end_deg, 10)[1:]
    inner_end = points[-1]
    tail_in = (inner_end[0] + tangent[0] * ARM["tail"], inner_end[1] + tangent[1] * ARM["tail"])
    points.append(tail_in)
    tip = (tail_in[0] + math.cos(math.radians(end_deg)) * band / 2, tail_in[1] + math.sin(math.radians(end_deg)) * band / 2)
    # Round the end: a half circle from the inner edge's end over to the outer edge's, round the far side (past the tail's direction).
    tip_deg = end_deg + 180.0
    points += arc(tip[0], tip[1], band / 2, tip_deg, tip_deg + 180.0, 8)[1:]
    points += arc(centre[0], centre[1], outer, end_deg, 90.0, 10)
    points.append((0.0, top))
    return points


def flat(name, loops, depth, bevel):
    """A mesh of 2D outlines (the first round the outside, any after it holes in it), `depth` thick about z = 0."""
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '2D'
    curve.fill_mode = 'BOTH'
    curve.extrude = max(depth / 2 - bevel, 0.0)
    curve.bevel_depth = bevel if depth > 0 else 0.0
    curve.bevel_resolution = 1
    for loop in loops:
        spline = curve.splines.new('POLY')
        spline.points.add(len(loop) - 1)
        for i, (x, y) in enumerate(loop):
            spline.points[i].co = (x, y, 0.0, 1.0)
        spline.use_cyclic_u = True
    holder = bpy.data.objects.new(name + "Curve", curve)
    bpy.context.scene.collection.objects.link(holder)
    bpy.context.view_layer.update()
    mesh = bpy.data.meshes.new_from_object(holder.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(holder)
    bpy.data.curves.remove(curve)
    return mesh


def material(spec):
    name, rgba = spec
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = rgba
    bsdf.inputs["Alpha"].default_value = rgba[3]
    m.diffuse_color = rgba
    return m


def place(mesh, unity_matrix):
    """Puts a mesh made in its own plane into the head's Unity frame, then into Blender's axes, its faces turned outward again."""
    mesh.transform(TO_BLENDER @ unity_matrix)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()


def unity(origin, right, up, out):
    """The matrix taking a part's own (x right, y up, z out of its face) into the head's Unity frame."""
    return Matrix(((right[0], up[0], out[0], origin[0]), (right[1], up[1], out[1], origin[1]),
                   (right[2], up[2], out[2], origin[2]), (0.0, 0.0, 0.0, 1.0)))


def build():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob)
    front_back = math.sqrt(HEAD_RADIUS ** 2 - EYES_Y ** 2) + FRONT_GAP
    front_middle = front_back + FRONT["thickness"] / 2
    windows = [rounded_rect(side * LENS["apart"] / 2, LENS["lift"], LENS["width"], LENS["height"], LENS["corner"]) for side in (-1, 1)]

    front = flat("Front", [front_outline()] + windows, FRONT["thickness"], BEVEL)
    place(front, unity((0.0, EYES_Y, front_middle), (1, 0, 0), (0, 1, 0), (0, 0, 1)))
    parts = [front]
    for side in (-1, 1):
        hinge = Vector((side * (FRONT["width"] / 2 - ARM["thickness"] / 2), EYES_Y, front_back))
        end = Vector((side * ARM["out_to"], EYES_Y, ARM["back_to"]))
        back = (end - hinge).normalized()
        # The arm's own x runs back from the hinge, its y up; its thickness is either side of its plane, so either side is out.
        arm = flat("Arm", [arm_outline((end - hinge).length)], ARM["thickness"], BEVEL)
        place(arm, unity(hinge, back, (0, 1, 0), back.cross(Vector((0.0, 1.0, 0.0)))))
        parts.append(arm)

    frame_mesh = bpy.data.meshes.new("Frame")
    bm = bmesh.new()
    for part in parts:
        bm.from_mesh(part)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bm.to_mesh(frame_mesh)
    bm.free()
    frame = bpy.data.objects.new("Frame", frame_mesh)
    bpy.context.scene.collection.objects.link(frame)
    frame.data.materials.append(material(WHITE))

    for name, side, spec in (("LensRed", -1, RED), ("LensBlue", 1, BLUE)):
        sheet = flat(name, [rounded_rect(0.0, 0.0, LENS["width"] + 2 * LENS_OVERLAP, LENS["height"] + 2 * LENS_OVERLAP,
                                         LENS["corner"] + LENS_OVERLAP)], 0.0, 0.0)
        place(sheet, unity((side * LENS["apart"] / 2, EYES_Y + LENS["lift"], front_middle), (1, 0, 0), (0, 1, 0), (0, 0, 1)))
        lens = bpy.data.objects.new(name, sheet)
        bpy.context.scene.collection.objects.link(lens)
        lens.data.materials.append(material(spec))

    bpy.ops.object.select_all(action='DESELECT')
    for ob in bpy.context.scene.collection.objects:
        ob.select_set(True)
    os.makedirs(os.path.dirname(GLB), exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=GLB, export_format='GLB', use_selection=True, export_apply=True, export_yup=True,
                              export_materials='EXPORT', export_animations=False, export_skins=False)
    triangles = sum(sum(len(p.vertices) - 2 for p in ob.data.polygons) for ob in bpy.context.scene.collection.objects)
    print("wrote", GLB, "-", triangles, "triangles")


build()
