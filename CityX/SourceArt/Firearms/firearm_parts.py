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

    def finish(self, parent, shift=Vector((0, 0, 0))):
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
            p.use_smooth = False
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        obj.parent = parent
        obj.location = U(self.origin + shift)
        return obj
