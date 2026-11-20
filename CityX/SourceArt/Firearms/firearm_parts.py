# THE KIT THE GUN MODELS ARE BUILT WITH (ak47_model.py; glock_model.py has its own older copy): boxes, cylinders, extruded profiles and lofts,
# authored in the gun's own UNITY frame (x the shooter's right, y up, z the muzzle) and put into Blender with U(). Each Part becomes one object
# (one mesh, one or more materials) parented to the gun's root, with its origin where the part moves about (a magazine's front lug, a bolt's
# handle, a trigger's pivot).
import bpy
import bmesh
import math
from mathutils import Vector, Matrix


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


class Part:
    """One mesh being built, in the gun's Unity frame, relative to the part's origin (the origin itself is in the gun's frame)."""

    def __init__(self, name, origin, mat):
        self.name = name
        self.origin = Vector(origin)
        self.bm = bmesh.new()
        self.mat = mat
        self.slots = [mat]
        self.slot_faces = {}

    def _slot(self, mat):
        if mat is None:
            return 0
        if mat not in self.slots:
            self.slots.append(mat)
        return self.slots.index(mat)

    def _tag(self, faces, mat):
        slot = self._slot(mat)
        if slot:
            self.slot_faces.setdefault(slot, []).extend(faces)
        return faces

    def box(self, centre, size, rake=0.0, mat=None, tilt=0.0):
        """An axis-aligned box (relative to the origin); `rake` shears it back going down (degrees); `tilt` pitches it about x (degrees, + tips
        the front down)."""
        cx, cy, cz = centre
        sx, sy, sz = size
        t = math.tan(math.radians(rake))
        rot = Matrix.Rotation(math.radians(-tilt), 3, 'X')
        verts = []
        for dx in (-1, 1):
            for dy in (-1, 1):
                for dz in (-1, 1):
                    y = dy * sy * 0.5
                    z = dz * sz * 0.5 - y * t
                    p = rot @ Vector((dx * sx * 0.5, y, z)) + Vector((cx, cy, cz))
                    verts.append(self.bm.verts.new(U(p)))
        faces = []
        for f in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)):
            faces.append(self.bm.faces.new([verts[j] for j in f]))
        return self._tag(faces, mat)

    def cylinder(self, start, end, radius, segments=14, mat=None, radius_end=None):
        a, b = Vector(start), Vector(end)
        axis = (b - a).normalized()
        side = axis.orthogonal().normalized()
        up = axis.cross(side)
        r1 = radius
        r2 = radius if radius_end is None else radius_end
        ring_a, ring_b = [], []
        for i in range(segments):
            ang = 2.0 * math.pi * i / segments
            d = side * math.cos(ang) + up * math.sin(ang)
            ring_a.append(self.bm.verts.new(U(a + d * r1)))
            ring_b.append(self.bm.verts.new(U(b + d * r2)))
        faces = []
        for i in range(segments):
            j = (i + 1) % segments
            faces.append(self.bm.faces.new((ring_a[i], ring_a[j], ring_b[j], ring_b[i])))
        faces.append(self.bm.faces.new(ring_a[::-1]))
        faces.append(self.bm.faces.new(ring_b))
        return self._tag(faces, mat)

    def profile_extrude(self, profile, x_from, x_to, mat=None):
        """A prism from a closed 2D profile in the (z, y) side view, extruded across x (from x_from to x_to): a stock, a grip seen from the side."""
        left = [self.bm.verts.new(U((x_from, y, z))) for z, y in profile]
        right = [self.bm.verts.new(U((x_to, y, z))) for z, y in profile]
        n = len(profile)
        faces = []
        for i in range(n):
            j = (i + 1) % n
            faces.append(self.bm.faces.new((left[i], left[j], right[j], right[i])))
        faces.append(self.bm.faces.new(left[::-1]))
        faces.append(self.bm.faces.new(right))
        return self._tag(faces, mat)

    def loft(self, slices, mat=None, cap=True):
        """A tube through rectangles: slices = [(centre, along, up, half_width_x, half_depth)] -- each a rectangle across x (half_width_x) and
        along `along` (half_depth), facing `up`. A curved magazine is a loft of slices round its arc."""
        rings = []
        for centre, along, up, hw, hd in slices:
            c, a = Vector(centre), Vector(along).normalized()
            x = Vector((1.0, 0.0, 0.0))
            ring = []
            for dx, dz in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                ring.append(self.bm.verts.new(U(c + x * (dx * hw) + a * (dz * hd))))
            rings.append(ring)
        faces = []
        for r0, r1 in zip(rings, rings[1:]):
            for i in range(4):
                j = (i + 1) % 4
                faces.append(self.bm.faces.new((r0[i], r0[j], r1[j], r1[i])))
        if cap:
            faces.append(self.bm.faces.new(rings[0][::-1]))
            faces.append(self.bm.faces.new(rings[-1]))
        return self._tag(faces, mat)

    def rings(self, sections, mat=None, cap=True, inlays=()):
        """A tube through any rings: sections = [(centre, across, up, ring)], each ring a list of (u, v) points laid out from the centre along
        `across` and `up` (rounded_rect, rect_ring, marked_rect); every ring the same count. A stock, a handguard, a rounded magazine.
        `inlays` = [(first span, last span, ring steps or None for all round, material)] colours faces of the tube itself: a detail drawn in
        the surface (a groove, a pressed rib, a chequered panel) with no edge standing off it for the game's outlines to speckle."""
        loops = []
        for centre, across, up, ring in sections:
            c, a, b = Vector(centre), Vector(across), Vector(up)
            loops.append([self.bm.verts.new(U(c + a * u + b * v)) for u, v in ring])
        faces = []
        n = len(loops[0])
        for r0, r1 in zip(loops, loops[1:]):
            for i in range(n):
                j = (i + 1) % n
                faces.append(self.bm.faces.new((r0[i], r0[j], r1[j], r1[i])))
        inlaid = set()
        for first, last, steps, inlay in inlays:
            chosen = [faces[span * n + i] for span in range(first, last + 1) for i in (range(n) if steps is None else steps)]
            self._tag(chosen, inlay)
            inlaid.update(chosen)
        if cap:
            # Each end closed as a fan round its middle: a ring with points in a row along a flat side (marked_rect), closed as one
            # many-sided face, is cut into triangles with no area.
            for loop, (centre, across, up, ring) in ((loops[0][::-1], sections[0]), (loops[-1], sections[-1])):
                middle = self.bm.verts.new(U(Vector(centre) + Vector(across) * (sum(u for u, v in ring) / n) + Vector(up) * (sum(v for u, v in ring) / n)))
                faces.extend(self.bm.faces.new((loop[i], loop[(i + 1) % n], middle)) for i in range(n))
        self._tag([f for f in faces if f not in inlaid], mat)
        return faces

    def rbox(self, centre, size, radius, axis='z', mat=None, corner=2, ends=None):
        """A box with its long edges rounded (`radius`) and its two ends chamfered (`ends`, the radius unless given), lying along `axis`."""
        c = Vector(centre)
        sx, sy, sz = size
        ends = radius if ends is None else ends
        if axis == 'z':
            along, across, up, length, hw, hh = Vector((0, 0, 1)), Vector((1, 0, 0)), Vector((0, 1, 0)), sz, sx * 0.5, sy * 0.5
        elif axis == 'y':
            along, across, up, length, hw, hh = Vector((0, 1, 0)), Vector((1, 0, 0)), Vector((0, 0, 1)), sy, sx * 0.5, sz * 0.5
        else:
            along, across, up, length, hw, hh = Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, 1, 0)), sx, sz * 0.5, sy * 0.5
        full = rounded_rect(hw, hh, radius, corner=corner)
        small = rounded_rect(hw - ends, hh - ends, max(radius - ends, radius * 0.4), corner=corner)
        h = length * 0.5
        return self.rings([(c - along * h, across, up, small), (c - along * (h - ends), across, up, full),
                           (c + along * (h - ends), across, up, full), (c + along * h, across, up, small)], mat=mat)

    def lathe(self, centre_xy, profile, segments=14, mat=None):
        """Turned about an axis along z through (x, y) = centre_xy: profile = [(z, radius)], capped at both ends. A barrel with its steps."""
        cx, cy = centre_xy
        loops = []
        for z, r in profile:
            loops.append([self.bm.verts.new(U((cx + r * math.cos(2.0 * math.pi * i / segments), cy + r * math.sin(2.0 * math.pi * i / segments), z)))
                          for i in range(segments)])
        faces = []
        for r0, r1 in zip(loops, loops[1:]):
            for i in range(segments):
                j = (i + 1) % segments
                faces.append(self.bm.faces.new((r0[i], r0[j], r1[j], r1[i])))
        faces.append(self.bm.faces.new(loops[0][::-1]))
        faces.append(self.bm.faces.new(loops[-1]))
        return self._tag(faces, mat)

    def slab(self, profile, half_width, bevel, x=0.0, mat=None):
        """A closed (z, y) side profile extruded across x (half_width each side of `x`), its two outlines chamfered by `bevel`: a sight base,
        a gas block, a lever."""
        inner = inset_profile(profile, bevel)
        loops = []
        for px, shape in ((x - half_width, inner), (x - half_width + bevel, profile), (x + half_width - bevel, profile), (x + half_width, inner)):
            loops.append([self.bm.verts.new(U((px, y, z))) for z, y in shape])
        faces = []
        n = len(profile)
        for r0, r1 in zip(loops, loops[1:]):
            for i in range(n):
                j = (i + 1) % n
                faces.append(self.bm.faces.new((r0[i], r0[j], r1[j], r1[i])))
        faces.append(self.bm.faces.new(loops[0][::-1]))
        faces.append(self.bm.faces.new(loops[-1]))
        return self._tag(faces, mat)

    def strap(self, path, half_width, thickness, x=0.0, mat=None):
        """A flat band following an open (z, y) path in the side view, `half_width` each side of `x` and `thickness` through: a trigger guard,
        a sight leaf, a trigger's blade."""
        points = [Vector((0.0, y, z)) for z, y in path]
        sections = []
        for i, p in enumerate(points):
            before = (p - points[i - 1]).normalized() if i > 0 else None
            after = (points[i + 1] - p).normalized() if i + 1 < len(points) else None
            tangent = ((before if before is not None else after) + (after if after is not None else before)).normalized()
            normal = Vector((0.0, tangent.z, -tangent.y))
            # at a bend the band keeps its thickness: the section is stretched by 1 / cos(half the bend)
            stretch = 1.0 / max(0.5, before.dot(tangent)) if before is not None and after is not None else 1.0
            sections.append((p + Vector((x, 0.0, 0.0)), Vector((1, 0, 0)), normal, rect_ring(half_width, thickness * 0.5 * stretch)))
        return self.rings(sections, mat=mat)

    def finish(self, parent, shift=Vector((0, 0, 0)), smooth=None):
        """`smooth` (degrees): rounded forms are shaded smooth, edges sharper than it stay crisp, and flat faces stay flat (weighted normals).
        None: every face flat, as the first guns are."""
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        for slot, faces in self.slot_faces.items():
            for f in faces:
                if f.is_valid:
                    f.material_index = slot
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for m in self.slots:
            mesh.materials.append(material(*m))
        for p in mesh.polygons:
            p.use_smooth = smooth is not None
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        obj.parent = parent
        obj.location = U(self.origin + shift)
        if smooth is not None:
            mesh.set_sharp_from_angle(angle=math.radians(smooth))
            weighted = obj.modifiers.new("WeightedNormals", 'WEIGHTED_NORMAL')
            weighted.keep_sharp = True
        return obj


def rect_ring(half_width, half_height):
    """A rectangle as a ring of (u, v) points."""
    return [(half_width, -half_height), (half_width, half_height), (-half_width, half_height), (-half_width, -half_height)]


def rounded_rect(half_width, half_height, radius, radius_bottom=None, corner=2):
    """A rectangle with rounded corners as a ring of (u, v) points: `radius` at the top corners, `radius_bottom` at the bottom ones (the
    same unless given), `corner` steps round each. An arched cover is a big top radius; a round belly a big bottom one."""
    top = max(1e-5, min(radius, half_width, half_height))
    bottom = top if radius_bottom is None else max(1e-5, min(radius_bottom, half_width, half_height))
    ring = []
    for cx, cy, r, start in ((1, -1, bottom, -90.0), (1, 1, top, 0.0), (-1, 1, top, 90.0), (-1, -1, bottom, 180.0)):
        for i in range(corner + 1):
            a = math.radians(start + 90.0 * i / corner)
            ring.append((cx * (half_width - r) + r * math.cos(a), cy * (half_height - r) + r * math.sin(a)))
    return ring


def marked_rect(half_width, half_height, radius, marks, radius_bottom=None, corner=2):
    """rounded_rect with extra points on its two flat sides at the heights `marks` (v, rising): the ring, and steps(k) = the two ring steps
    (right side, left side) between mark k and the next, for Part.rings' inlays: a band drawn along each side of a tube."""
    base = rounded_rect(half_width, half_height, radius, radius_bottom, corner)
    c = corner + 1
    ring = base[:c] + [(half_width, m) for m in marks] + base[c:3 * c] + [(-half_width, m) for m in reversed(marks)] + base[3 * c:]
    count = len(marks)
    return ring, lambda k: [c + k, 3 * c + count + (count - 2 - k)]


def inset_profile(profile, distance):
    """A closed 2D outline moved inward by `distance` (each corner along its mitre)."""
    n = len(profile)
    area = sum(profile[i][0] * profile[(i + 1) % n][1] - profile[(i + 1) % n][0] * profile[i][1] for i in range(n))
    sign = 1.0 if area > 0.0 else -1.0
    out = []
    for i in range(n):
        p0, p1, p2 = Vector(profile[i - 1]), Vector(profile[i]), Vector(profile[(i + 1) % n])
        e1, e2 = (p1 - p0).normalized(), (p2 - p1).normalized()
        n1, n2 = Vector((-e1.y, e1.x)) * sign, Vector((-e2.y, e2.x)) * sign
        mitre = (n1 + n2) / max(0.3, 1.0 + n1.dot(n2))
        out.append(tuple(p1 + mitre * distance))
    return out
