# The sleeves cut loose from the coat's side (exec after parka_lib.py, parka_rings.py, parka_sleeve.py). Meshy drew sleeve and side as one skin
# under the arm: the skin is cut along the crease between them, and each of the two holes that leaves is closed with a new patch (the
# sleeve's inner side, the coat's side wall), the two meeting along the armpit.
import bmesh
from mathutils import geometry

ARMHOLE_KEEP = 0.004       # an edge this near the armhole's plane is the shoulder's: it stays joined
CUT_FRONT, CUT_BACK = -0.155, 0.17   # the crease runs between these (y): further forward the sleeve's cap runs into the chest, uncut
TUBE_SLACK = 0.015         # a face this far outside the read sleeve is the coat's (a bulge of its front past the crease)
TUCK_REACH = 0.035         # the skin this near the cut is laid flat: on the read sleeve on its side, on the wall on the coat's
SHAVE_NARROW, SHAVE_BELOW, SHAVE_FULL = 0.11, 1.30, 1.45   # the coat's side is shaved within this of y = 0 below the elbow, widening to the crease's whole run at the armpit
ON_WALL = 0.004            # a point this near the wall lies on it
PATCH_GRID = 0.02          # how far apart a patch's own points are
PATCH_INSET = 0.012        # and how far they keep from the patch's edge


def mirrored(side, co):
    out = co.copy()
    out[:, 0] *= side
    return out


def sleeve_faces(bm, reading, world):
    """The faces of one sleeve: on the sleeve's side of both planes, no further from its middle than the read sleeve, and reached from the
    cuff through such faces. What the coat's own skin encloses of them (a speck the reading missed) is sleeve too."""
    side = reading.side
    centres = mirrored(side, np.array([world @ f.calc_center_median() for f in bm.faces]))
    wedge = in_sleeve(side, centres) & (centres[:, 2] > 0.8)
    k = reading.ring_of(centres)
    column = reading.angle_of(centres, k)
    far = np.linalg.norm(centres - reading.along(reading.centres, k), axis=1)
    read = np.linalg.norm(reading.surface(k, column) - reading.along(reading.centres, k), axis=1)
    tube = wedge & (far < read + TUBE_SLACK) & (k < reading.cuff)
    seed = int(np.where(tube & (centres[:, 2] < 1.2) & (centres[:, 0] > 0.5))[0][0])
    sleeve = flood(bm, seed, tube)
    coat_seed = int(np.argmin(np.linalg.norm(centres - np.array([0.0, 0.3, 1.2]), axis=1)))
    coat = flood(bm, coat_seed, ~sleeve)
    return sleeve | (wedge & ~coat & (k < reading.cuff))


def flood(bm, seed, allowed):
    reached = np.zeros(len(bm.faces), dtype=bool)
    reached[seed] = True
    stack = [seed]
    while stack:
        for e in bm.faces[stack.pop()].edges:
            for other in e.link_faces:
                if not reached[other.index] and allowed[other.index]:
                    reached[other.index] = True
                    stack.append(other.index)
    return reached


def cut_loose(bm, reading, world, sleeve, mark, owner):
    """Parts the sleeve's skin from the coat's everywhere but round the shoulder (the armhole's plane, and the crease's two ends where the
    sleeve's cap runs into chest and back), where they stay one skin. Returns the cut's points on the sleeve and on the coat, each in order
    from the front of the armpit round the bottom of the crease to its back, their two ends shared; any other place the two skins met is
    left as a small hole in each, closed flat. mark is a vertex layer: 2 is left on the points still joined. owner is a face layer the
    sleeve's faces are noted in (1 left, 2 right; Blender's own operators wipe face tags, and parting makes the faces anew)."""
    side = reading.side
    plane_point, plane_normal, _ = armhole_plane(side)
    for v in bm.verts:
        v[mark] = 0
    joined, parted = [], []
    for e in bm.edges:
        if len(e.link_faces) != 2 or sleeve[e.link_faces[0].index] == sleeve[e.link_faces[1].index]:
            continue
        mid = (world @ e.verts[0].co + world @ e.verts[1].co) * 0.5
        m = np.array([mid.x * side, mid.y, mid.z])
        stays = ((m - plane_point) @ plane_normal <= ARMHOLE_KEEP or not CUT_FRONT < m[1] < CUT_BACK
                 or reading.ring_of(m[None, :])[0] < STILL_RINGS)
        (joined if stays else parted).append(e)
    for e in joined:
        for v in e.verts:
            v[mark] = 2
    for e in parted:
        for v in e.verts:
            if v[mark] == 0:
                v[mark] = 1
    code = 1 if side > 0 else 2
    for f in bm.faces:
        if sleeve[f.index]:
            f[owner] = code
    bmesh.ops.split(bm, geom=[f for f in bm.faces if f[owner] == code])
    bmesh.ops.remove_doubles(bm, verts=[v for v in bm.verts if v[mark] == 2], dist=1e-7)
    bm.verts.ensure_lookup_table()
    bm.edges.ensure_lookup_table()
    bm.faces.ensure_lookup_table()
    shave_coat(bm, side, world, mark, owner)
    bm.verts.ensure_lookup_table()
    bm.edges.ensure_lookup_table()
    bm.faces.ensure_lookup_table()
    chains = {}
    strays = 0
    for is_sleeve in (True, False):
        edges = [e for e in bm.edges if e.is_boundary and e.verts[0][mark] and e.verts[1][mark] and (e.link_faces[0][owner] == code) == is_sleeve]
        tuck_crease(bm, reading, world, {v for e in edges for v in e.verts}, is_sleeve)
        chains[is_sleeve] = ordered_chain(edges, world, mark)
        on_chain = set(chains[is_sleeve])
        stray = [e for e in edges if e.verts[0] not in on_chain or e.verts[1] not in on_chain]
        for loop in plain_loops(stray):     # each skin's own holes closed apart: a face from one skin to the other would tie the arm to the coat
            bmesh.ops.triangulate(bm, faces=bmesh.ops.holes_fill(bm, edges=loop, sides=0)["faces"])
            strays += len(loop)
    if strays:
        print("side %d: %d stray edges where the skins also met, closed" % (side, strays))
    return chains[True], chains[False]


def shave_coat(bm, side, world, mark, owner):
    """Under the arm the coat is cut back to its side wall: Meshy's crease leaves rags of skin on it past the wall (the right forearm lay
    against the coat to the cuff), which the sleeve no longer covers once it is moved. Faces wholly past the wall go, a face across it has
    its points past it laid on it; the points of the edge this leaves are marked (1) as the cut's."""
    inverse = world.inverted()
    coat = [v for v in bm.verts if v[mark] != 2 and v.link_faces and all(f[owner] == 0 for f in v.link_faces)]
    pts = mirrored(side, np.array([world @ v.co for v in coat]))
    reach = SHAVE_NARROW + (np.clip((pts[:, 2] - SHAVE_BELOW) / (SHAVE_FULL - SHAVE_BELOW), 0.0, 1.0))[:, None] * (
        np.array([[-CUT_FRONT, CUT_BACK]]) - SHAVE_NARROW)
    window = (pts[:, 1] > -reach[:, 0]) & (pts[:, 1] < reach[:, 1]) & (pts[:, 2] > 0.85)
    past = set(v for v, yes in zip(coat, in_sleeve(side, pts) & window) if yes)
    gone = [f for f in bm.faces if f[owner] == 0 and all(v in past for v in f.verts)]
    bmesh.ops.delete(bm, geom=gone, context="FACES")
    laid = 0
    for v in past:
        if v.is_valid and v.link_faces:
            w = world @ v.co
            w.x = side * float(wall_x(side, w.z, w.y))
            v.co = inverse @ w
            v[mark] = 1
            laid += 1
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    for e in bm.edges:
        if e.is_boundary and e.link_faces[0][owner] == 0 and (e.verts[0][mark] or e.verts[1][mark]):
            for v in e.verts:
                if not v[mark]:
                    w = world @ v.co
                    if abs(w.x * side - wall_x(side, w.z, w.y)) < ON_WALL * 3:
                        v[mark] = 1
    print("side %d: the coat's side shaved to its wall, %d faces off and %d points laid on it" % (side, len(gone), laid))


def tuck_crease(bm, reading, world, edge, is_sleeve):
    """Lays the skin beside the cut flat. Meshy's crease is a deep fold, and cut along its bottom each half is left standing as a ragged
    flange towards the other: the sleeve's is drawn in to the read sleeve's closed surface, the coat's back to the side wall, in full at
    the cut and less with distance."""
    side = reading.side
    inverse = world.inverted()
    near = set(edge)
    for _ in range(12):   # a few rings of neighbours: all that can be within reach at this mesh's edge length
        near |= {e.other_vert(v) for v in list(near) for e in v.link_edges}
    near = list(near)
    pts = mirrored(side, np.array([world @ v.co for v in near]))
    cut = mirrored(side, np.array([world @ v.co for v in edge]))
    pull = np.empty(len(pts))
    for i in range(0, len(pts), 2000):
        pull[i:i + 2000] = np.linalg.norm(pts[i:i + 2000, None, :] - cut[None, :, :], axis=2).min(1)
    pull = smoothstep(1.0 - pull / TUCK_REACH)
    if is_sleeve:
        k = reading.ring_of(pts)
        centre = reading.along(reading.centres, k)
        read = reading.surface(k, reading.angle_of(pts, k))
        over = np.linalg.norm(pts - centre, axis=1) > np.linalg.norm(read - centre, axis=1)
        moved = np.where((over * pull)[:, None] > 0, pts + (read - pts) * (over * pull)[:, None], pts)
    else:
        wall = wall_x(side, pts[:, 2], pts[:, 1])
        moved = pts.copy()
        moved[:, 0] -= np.maximum(pts[:, 0] - wall, 0.0) * pull
    for v, p in zip(near, mirrored(side, moved)):
        v.co = inverse @ Vector(p)


def drop_loose(bm, world):
    """Deletes everything not connected to the coat's one big skin: stubs of fur strands round the hood, the cuffs' linings, what is left
    of a sleeve's end below its new cuff."""
    bm.faces.ensure_lookup_table()
    bm.faces.index_update()
    seed = min(bm.faces, key=lambda f: (world @ f.calc_center_median() - Vector((0.0, 0.3, 1.2))).length).index
    keep = flood(bm, seed, np.ones(len(bm.faces), dtype=bool))
    gone = [f for f in bm.faces if not keep[f.index]]
    bmesh.ops.delete(bm, geom=gone, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    return len(gone)


def plain_loops(edges):
    """The edges sorted into open loops, and only the plain ones: every point of a plain loop has exactly two of its edges. A tangled one
    (edges crossing at a point, a loop pinched shut) is left open: filled, it gives faces lying over the skin and turned the wrong way,
    which show as stray pale triangles, where the tangle itself is a slit no wider than a hair."""
    by_vert = {}
    for e in edges:
        for v in e.verts:
            by_vert.setdefault(v, []).append(e)
    loops, seen = [], set()
    for e in edges:
        if e in seen:
            continue
        loop = [e]
        seen.add(e)
        for x in loop:
            for v in x.verts:
                for y in by_vert[v]:
                    if y not in seen:
                        seen.add(y)
                        loop.append(y)
        if all(len(by_vert[v]) == 2 for x in loop for v in x.verts):
            loops.append(loop)
    return loops

def simple_loops(edges):
    """A tangle of open edges sorted into its plain rings: two holes that meet at a point are a figure of eight, and each of its rings is
    a hole that can be filled like any other. Edges that end nowhere (a slit along an edge three faces share) belong to no ring."""
    by_vert = {}
    for e in edges:
        for v in e.verts:
            by_vert.setdefault(v, []).append(e)
    left, rings = set(edges), []
    while left:
        e = next(iter(left))
        path, verts = [], [e.verts[0]]
        while e is not None:
            left.discard(e)
            path.append(e)
            v = e.other_vert(verts[-1])
            if v in verts:
                i = verts.index(v)
                rings.append(path[i:])
                del path[i:]
                del verts[i + 1:]
            else:
                verts.append(v)
            e = next((x for x in by_vert[verts[-1]] if x in left), None)
    return [ring for ring in rings if len(ring) > 2]



def ordered_chain(edges, world, mark):
    """The shortest run from the front end of the cut to its back end, as points in order, through the largest connected set of the edges.
    The ends are its points still joined (mark 2) furthest forward and back; a loop the run passes, or a separate place the skins met, is
    left out."""
    links = {}
    for e in edges:
        for a, b in ((e.verts[0], e.verts[1]), (e.verts[1], e.verts[0])):
            links.setdefault(a, []).append(b)
    largest, seen = [], set()
    for start in links:
        if start in seen:
            continue
        group = [start]
        seen.add(start)
        for v in group:
            for n in links[v]:
                if n not in seen:
                    seen.add(n)
                    group.append(n)
        if len(group) > len(largest):
            largest = group
    ends = [v for v in largest if v[mark] == 2] or largest
    front = min(ends, key=lambda v: (world @ v.co).y)
    back = max(ends, key=lambda v: (world @ v.co).y)
    came = {front: None}
    queue = [front]
    for v in queue:
        if v is back:
            break
        for n in links[v]:
            if n not in came:
                came[n] = v
                queue.append(n)
    if back not in came:
        raise RuntimeError("the cut does not run from its front end to its back end: %d of %d points reached" % (len(came), len(links)))
    run = [back]
    while came[run[-1]] is not None:
        run.append(came[run[-1]])
    return run[::-1]


def fill_patch(bm, chain, flat, place, uv_of, uv_layer, facing, chord=None):
    """Closes the hole whose edge is chain (its ends joined straight across by the chord) with a patch: the chain's points, the chord's and
    a grid of new ones inside, triangulated in the flat coordinates flat gives the chain (metres), the new points put where place(flat)
    says, every corner given uv_of(flat), the faces turned to look along facing. Pass the chord of the patch that shares it, back end
    first as returned. Returns the new inside points, the faces and the chord's points."""
    edge = np.array(flat)
    count = len(chain)
    if chord is None:
        steps = int((chain[0].co - chain[-1].co).length / PATCH_GRID)
        chord = [bm.verts.new(chain[-1].co.lerp(chain[0].co, (i + 1) / (steps + 1))) for i in range(steps)]
    across = [edge[-1] + (edge[0] - edge[-1]) * (i + 1) / (len(chord) + 1) for i in range(len(chord))]
    outline = np.concatenate([edge, np.array(across).reshape(-1, 2)])
    given = list(chain) + list(chord)
    lo, hi = outline.min(0), outline.max(0)
    inside = []
    for x in np.arange(lo[0] + PATCH_GRID * 0.5, hi[0], PATCH_GRID):
        for y in np.arange(lo[1] + PATCH_GRID * 0.5, hi[1], PATCH_GRID):
            p = np.array([x, y])
            if within(p, outline) and np.linalg.norm(outline - p, axis=1).min() > PATCH_INSET:
                inside.append(p)
    points = np.concatenate([outline, np.array(inside).reshape(-1, 2)])
    loop = [(i, (i + 1) % len(outline)) for i in range(len(outline))]
    out_points, _, out_faces, from_points, _, _ = geometry.delaunay_2d_cdt([Vector(p) for p in points], loop, [], 1, 1e-6)
    out_flat = np.array([[p.x, p.y] for p in out_points])
    new_co = place(out_flat)
    uvs = uv_of(out_flat)
    verts, fresh = [], []
    for i in range(len(out_points)):
        source = [s for s in from_points[i] if s < len(given)]
        if source:
            verts.append(given[source[0]])
        else:
            verts.append(bm.verts.new(new_co[i]))
            fresh.append(verts[-1])
    faces = []
    for tri in out_faces:
        if len({verts[i] for i in tri}) < 3:
            continue
        try:
            face = bm.faces.new([verts[i] for i in tri])
        except ValueError:
            continue
        face.smooth = True
        for corner, i in zip(face.loops, tri):
            corner[uv_layer].uv = uvs[i]
        faces.append(face)
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    mean = Vector((0.0, 0.0, 0.0))
    for face in faces:
        mean += face.normal * face.calc_area()
    if mean.dot(Vector(facing)) < 0.0:
        bmesh.ops.reverse_faces(bm, faces=faces)
    return fresh, faces, chord


def within(p, polygon):
    x, y = p
    inside = False
    j = len(polygon) - 1
    for i in range(len(polygon)):
        xi, yi = polygon[i]
        xj, yj = polygon[j]
        if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi) + xi:
            inside = not inside
        j = i
    return inside


def remesh_patch(bm, faces, world, uv_layer, grid):
    """A patch of faces drawn again as an even web: its edge stays as it is, and inside it points grid apart, each settled midway between
    its neighbours (in place and on the map), so the web spans its edge like a stretched cloth. Cutting the coat down to the game's detail
    leaves an underarm patch a jumble of long thin faces, which crease into spikes when the arm lifts: an even web stretches smoothly
    (what riggers ask of an armpit's edge loops). Returns the new faces, or None if the patch's edge is not one plain loop."""
    faces = set(faces)
    if not faces:
        return None
    edge = [e for f in faces for e in f.edges if sum(1 for other in e.link_faces if other in faces) == 1]
    links = {}
    for e in set(edge):
        for a, b in ((e.verts[0], e.verts[1]), (e.verts[1], e.verts[0])):
            links.setdefault(a, []).append(b)
    if not links or any(len(n) != 2 for n in links.values()):
        return None
    loop = [next(iter(links))]
    while True:
        nxt = [v for v in links[loop[-1]] if v not in loop[-2:]]
        if not nxt or nxt[0] is loop[0]:
            break
        loop.append(nxt[0])
    if len(loop) != len(links):
        return None
    on_map = {}
    facing = Vector((0.0, 0.0, 0.0))
    for f in faces:
        facing += f.normal * f.calc_area()
        for corner in f.loops:
            on_map.setdefault(corner.vert, corner[uv_layer].uv.copy())
    inverse = world.inverted()
    pts = np.array([world @ v.co for v in loop])
    middle = pts.mean(0)
    axes = np.linalg.svd(pts - middle, full_matrices=False)[2]
    flat = np.stack([(pts - middle) @ axes[0], (pts - middle) @ axes[1]], 1)
    lo, hi = flat.min(0), flat.max(0)
    inside = [np.array([x, y]) for x in np.arange(lo[0] + grid * 0.5, hi[0], grid) for y in np.arange(lo[1] + grid * 0.5, hi[1], grid)
              if within(np.array([x, y]), flat) and np.linalg.norm(flat - np.array([x, y]), axis=1).min() > grid * 0.6]
    points = np.concatenate([flat, np.array(inside).reshape(-1, 2)])
    ring = [(i, (i + 1) % len(loop)) for i in range(len(loop))]
    out_points, _, out_faces, from_points, _, _ = geometry.delaunay_2d_cdt([Vector(p) for p in points], ring, [], 1, 1e-6)
    old_inside = {v for f in faces for v in f.verts} - set(loop)
    bmesh.ops.delete(bm, geom=list(faces), context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in old_inside if v.is_valid and not v.link_faces], context="VERTS")
    verts, free = [], []
    for i, p in enumerate(out_points):
        source = [s for s in from_points[i] if s < len(loop)]
        if source:
            verts.append(loop[source[0]])
        else:
            verts.append(bm.verts.new(inverse @ Vector(middle + axes[0] * p.x + axes[1] * p.y)))
            free.append(verts[-1])
            on_map[verts[-1]] = Vector((0.0, 0.0))
    new_faces = []
    for tri in out_faces:
        if len({verts[i] for i in tri}) == 3:
            try:
                new_faces.append(bm.faces.new([verts[i] for i in tri]))
            except ValueError:
                pass
    neighbours = {v: [e.other_vert(v) for e in v.link_edges] for v in free}
    for _ in range(80):
        for v in free:
            v.co = sum((n.co for n in neighbours[v]), Vector()) / len(neighbours[v])
            on_map[v] = sum((on_map[n] for n in neighbours[v]), Vector((0.0, 0.0))) / len(neighbours[v])
    bmesh.ops.recalc_face_normals(bm, faces=new_faces)
    turned = sum((f.normal * f.calc_area() for f in new_faces), Vector())
    if turned.dot(facing) < 0.0:
        bmesh.ops.reverse_faces(bm, faces=new_faces)
    for f in new_faces:
        f.smooth = True
        for corner in f.loops:
            corner[uv_layer].uv = on_map[corner.vert]
    return new_faces
