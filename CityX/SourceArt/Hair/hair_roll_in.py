# A HAIRSTYLE'S OPEN EDGES ROLLED INTO THE HEAD (owner, 2026-11-04, of the kick hair: "there is a slit where you can see underneath the hair and the sky shows
# behind it, can you make it roll inward into the head"; 2026-11-16, the same for the IDK hair). A hair model is an open shell, drawn from outside only: where
# its open edge stands off the head, a look in under it goes out through the far side. Every open edge is carried on, curling under and in, to end inside the
# head, so there is no way in under it. Used by KickHair/kickhair_roll_in.py and IdkNeedsWork/idk_roll_in.py.
# The head is a sphere round `head` (Blender's axes) of `radius`: where the owner's fit puts the game's head (FusedBodyShape.HeadRadius) in the model.
import bmesh
import bpy
from mathutils import Vector


def _open_loops(mesh):
    seen = set()
    loops = []
    for start in mesh.edges:
        if start in seen or not start.is_boundary:
            continue
        group, stack = [], [start]
        seen.add(start)
        while stack:
            edge = stack.pop()
            group.append(edge)
            for vert in edge.verts:
                for other in vert.link_edges:
                    if other.is_boundary and other not in seen:
                        seen.add(other)
                        stack.append(other)
        loops.append(group)
    return loops


def _out_of_the_surface(vert):
    """The way the surface was going as it reached this open edge: out of its faces, past the edge, along the surface."""
    direction = Vector((0.0, 0.0, 0.0))
    for edge in vert.link_edges:
        if edge.is_boundary:
            face = edge.link_faces[0]
            along = (edge.other_vert(vert).co - vert.co).normalized()
            across = face.normal.cross(along).normalized()
            if across.dot(face.calc_center_median() - vert.co) > 0.0:
                across = -across
            direction += across
    return direction.normalized() if direction.length > 1e-6 else Vector((0.0, 0.0, 0.0))


def toward_the_centre(head, depth):
    """Where an open edge at a point ends: under the head's surface, straight in toward its centre."""
    def end(point):
        from_head = point - head
        return head + from_head.normalized() * depth if from_head.length > 1e-6 else head.copy()
    return end


def roll_in(hair, head, radius, inside=0.05, curl_out=0.15, smallest_loop=8, curl_weld=0.02, buried_weld=0.05, ends=None, curl_at=None):
    """
    Every open edge of the mesh object `hair` curled under and ended `inside` metres under the head's surface.
    curl_out: how far the edge carries on the way it was going before it turns in, as a share of its way to the head (more, and thin lock tips go fat).
    smallest_loop: an opening of fewer edges is a pinhole, filled. curl_weld: the open edge is very finely cut and only the edge itself draws the locks'
    outline, so past it corners are merged to save triangles; buried_weld: the part buried in the head is nobody's to see.
    ends: where the edge at a point ends, inside the head (toward_the_centre by default).
    curl_at: given an edge point and where its curl would turn in, where it should instead (the curl then ends where `ends` says from there).
    """
    end_in_head = ends if ends is not None else toward_the_centre(head, radius - inside)

    mesh = bmesh.new()
    mesh.from_mesh(hair.data)
    before = sum(len(f.verts) - 2 for f in mesh.faces)
    outward_before = sum(1 for f in mesh.faces if f.normal.dot(f.calc_center_median() - head) > 0.0)
    print("before: %d triangles, %d of %d faces facing away from the head" % (before, outward_before, len(mesh.faces)))

    pinholes = [loop for loop in _open_loops(mesh) if len(loop) < smallest_loop]
    for loop in pinholes:
        bmesh.ops.holes_fill(mesh, edges=loop, sides=0)
    print("filled %d pinholes" % len(pinholes))

    rim = [e for e in mesh.edges if e.is_boundary]
    rim_verts = {v for e in rim for v in e.verts}
    start = {v: v.co.copy() for v in rim_verts}
    carry = {v: _out_of_the_surface(v) for v in rim_verts}
    print("rolling %d open edges" % len(rim))

    result = bmesh.ops.extrude_edge_only(mesh, edges=rim)
    curl = [g for g in result["geom"] if isinstance(g, bmesh.types.BMVert)]
    # The extruded corner knows the rim corner it came from by its one edge back to it.
    for vert in curl:
        back = next(e.other_vert(vert) for e in vert.link_edges if e.other_vert(vert) in start)
        p = start[back]
        way = (end_in_head(p) - p).length
        # Partway along the curve from the edge to the head, carried on past the edge first.
        control = p + carry[back] * way * curl_out
        t = 0.45
        vert.co = (1.0 - t) * (1.0 - t) * p + 2.0 * (1.0 - t) * t * control + t * t * end_in_head(p)
        if curl_at is not None:
            vert.co = curl_at(p, vert.co.copy())
    bmesh.ops.remove_doubles(mesh, verts=curl, dist=curl_weld)

    result = bmesh.ops.extrude_edge_only(mesh, edges=[e for e in mesh.edges if e.is_boundary])
    buried = [g for g in result["geom"] if isinstance(g, bmesh.types.BMVert)]
    fresh = set(buried)
    for vert in buried:
        back = next(e.other_vert(vert) for e in vert.link_edges if e.other_vert(vert) not in fresh)
        vert.co = end_in_head(back.co)
    bmesh.ops.remove_doubles(mesh, verts=buried, dist=buried_weld)
    bmesh.ops.dissolve_degenerate(mesh, dist=1e-5, edges=mesh.edges[:])
    # Merging can leave a short open edge on the curl: pulled into the head with the rest.
    for vert in {v for e in mesh.edges if e.is_boundary for v in e.verts}:
        if (vert.co - head).length > radius - 0.02:
            vert.co = end_in_head(vert.co)
    bmesh.ops.triangulate(mesh, faces=[f for f in mesh.faces if len(f.verts) > 3])
    mesh.to_mesh(hair.data)
    mesh.free()
    hair.data.validate()
    hair.data.update()
    # Validating can drop a bad face and open an edge of its own: the last pass after that.
    mesh = bmesh.new()
    mesh.from_mesh(hair.data)
    for vert in {v for e in mesh.edges if e.is_boundary for v in e.verts}:
        if (vert.co - head).length > radius - 0.02:
            vert.co = end_in_head(vert.co)
    mesh.to_mesh(hair.data)
    mesh.free()
    hair.data.update()
    for polygon in hair.data.polygons:
        polygon.use_smooth = True
    report(hair, head, radius)


def report(hair, head, radius):
    check = bmesh.new()
    check.from_mesh(hair.data)
    left = [e for e in check.edges if e.is_boundary]
    outside = [e for e in left if max((v.co - head).length for v in e.verts) > radius]
    print("after: %d triangles; %d open edges left, %d of them outside the head" % (sum(len(f.verts) - 2 for f in check.faces), len(left), len(outside)))
    check.free()


def load(source):
    """The model alone in an empty scene, its transforms applied."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=source)
    hair = next(o for o in bpy.context.scene.objects if o.type == "MESH")
    bpy.context.view_layer.objects.active = hair
    hair.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return hair


def export(out):
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_materials="NONE", export_texcoords=False, export_normals=True, use_selection=False)
    print("wrote " + out)
