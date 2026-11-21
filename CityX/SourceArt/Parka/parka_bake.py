# The parka's game map (exec after the other parka_*.py files): the finished coat laid out afresh on the map and its colour carried over.
# Meshy's own layout is thousands of crumbs, cloth and fur side by side: cut from 4096 to the game's 1024, each crumb's edge takes its
# neighbours' colour and the coat is covered in pale slivers (owner, 2026-11-20: "what are all these little triangles im seeing?", and of the
# full-size map against the cut one: "the first was better"). Laid out in whole pieces with room between them, nothing bleeds, and the faces
# nobody sees (the lining deep inside) give their room on the map to the ones that are seen.
from mathutils.bvhtree import BVHTree

GAME_MAP = 1024
PIECE_ANGLE = 70.0         # a piece's faces look within this many degrees of its first one
SMALL_PIECE = 12           # a piece of fewer faces joins a neighbour
JOIN_PASSES = 6
PIECE_GAP = 0.004          # room between two pieces, of the map's width: four texels of the game's map
KNIT_BAND = 0.955          # the pieces are kept below this row: the ribbed cuffs keep Meshy's-map rows above it, which they repeat round the wrist
SIGHT_RAYS = 24
SIGHT_REACH = 4.0
UNSEEN = 1                 # a face this many of its rays or fewer get out from is one nobody sees
UNSEEN_SIZE = 0.4          # an unseen piece's size on the map, against a seen one's
SETTLE_PASSES = 3


def is_knit(uvs):
    return uvs[:, 0].min() >= ATLAS_OLD - 1e-4 and uvs[:, 1].min() >= KNIT_FROM - 1e-4


def unseen_faces(obj, others):
    """Which of the object's faces nobody can see: from the face, almost no ray gets out past the coat itself and the body in it."""
    points, faces = [], []
    for o in [obj] + others:
        first = len(points)
        points += [o.matrix_world @ v.co for v in o.data.vertices]
        faces += [[first + i for i in p.vertices] for p in o.data.polygons]
    tree = BVHTree.FromPolygons(points, faces)
    i = np.arange(SIGHT_RAYS) + 0.5
    up = np.sqrt(1.0 - i / SIGHT_RAYS)                   # more rays near the face's own direction, as its light comes
    flat = np.sqrt(1.0 - up * up)
    turn = i * math.pi * (3.0 - math.sqrt(5.0))
    rotation = obj.matrix_world.to_3x3()
    unseen = np.zeros(len(obj.data.polygons), dtype=bool)
    for p in obj.data.polygons:
        normal = (rotation @ p.normal).normalized()
        side = normal.orthogonal().normalized()
        other = normal.cross(side)
        start = obj.matrix_world @ p.center + normal * 0.003
        out = 0
        for a, b, c in zip(flat * np.cos(turn), flat * np.sin(turn), up):
            if tree.ray_cast(start, side * float(a) + other * float(b) + normal * float(c), SIGHT_REACH)[0] is None:
                out += 1
                if out > UNSEEN:
                    break
        unseen[p.index] = out <= UNSEEN
    return unseen


def settle(bm, flags):
    """A face goes with most of its neighbours, so the seen and the unseen part along lines and not face by face."""
    for _ in range(SETTLE_PASSES):
        new = flags.copy()
        for f in bm.faces:
            around = [g.index for e in f.edges for g in e.link_faces if g is not f]
            if around and sum(flags[i] == flags[f.index] for i in around) * 2 < len(around):
                new[f.index] = not flags[f.index]
        flags = new
    return flags


def pieces_of(bm, kind):
    """The faces sorted into the pieces they are laid out as: each grown from a big face across the edges two faces share, as far as the
    faces are of one kind and look within PIECE_ANGLE of the first, so a piece lies nearly flat and never wraps round on itself. A piece
    of only a few faces joins the neighbour it shares most edge with: thousands of crumbs is what Meshy's map was."""
    piece = np.full(len(bm.faces), -1, dtype=np.int64)
    limit = math.cos(math.radians(PIECE_ANGLE))

    def beside(f):
        for e in f.edges:
            if len(e.link_faces) == 2:
                g = e.link_faces[0] if e.link_faces[1] is f else e.link_faces[1]
                if kind[g.index] == kind[f.index]:
                    yield e, g

    count = 0
    for seed in sorted(bm.faces, key=lambda f: -f.calc_area()):
        if piece[seed.index] >= 0:
            continue
        piece[seed.index] = count
        grown = [seed]
        for f in grown:
            for _, g in beside(f):
                if piece[g.index] < 0 and g.normal.dot(seed.normal) > limit:
                    piece[g.index] = count
                    grown.append(g)
        count += 1
    for _ in range(JOIN_PASSES):
        size = np.bincount(piece, minlength=count)
        shared = {}
        for f in bm.faces:
            if size[piece[f.index]] < SMALL_PIECE:
                for e, g in beside(f):
                    if piece[g.index] != piece[f.index]:
                        key = (piece[f.index], piece[g.index])
                        shared[key] = shared.get(key, 0.0) + e.calc_length()
        best = {}
        for (small, other), length in shared.items():
            if small not in best or length > best[small][1]:
                best[small] = (other, length)
        if not best:
            break
        joins = {}

        def joined(p):
            while p in joins:
                p = joins[p]
            return p

        for small, (other, _) in best.items():
            if joined(other) != small:        # two small pieces that chose each other become one, not a ring
                joins[small] = other
        piece = np.array([joined(p) for p in range(count)])[piece]
    for _ in range(SETTLE_PASSES):       # a face reaching into another piece like a tooth goes over to it: ragged pieces pack badly
        for f in bm.faces:
            around = [piece[g.index] for _, g in beside(f)]
            if len(around) > 1 and around.count(piece[f.index]) < 2:
                most = max(set(around), key=around.count)
                if around.count(most) > 1:
                    piece[f.index] = most
    return piece


def lay_out(obj, others):
    """The object's faces laid out afresh in its first set of texture coordinates. Returns each face's corners as they were on the old map."""
    mesh = obj.data
    old = np.empty(len(mesh.loops) * 2, dtype=np.float32)
    mesh.uv_layers[0].data.foreach_get("uv", old)
    old = old.reshape(-1, 3, 2)
    knit = np.array([is_knit(uvs) for uvs in old])
    unseen = unseen_faces(obj, others)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    mesh.uv_layers.active = mesh.uv_layers[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bm = bmesh.from_edit_mesh(mesh)
    bm.faces.ensure_lookup_table()
    bm.normal_update()
    unseen = settle(bm, unseen) & ~knit
    piece = pieces_of(bm, np.where(knit, 2, unseen.astype(int)))
    for e in bm.edges:
        e.seam = len(e.link_faces) != 2 or piece[e.link_faces[0].index] != piece[e.link_faces[1].index]
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        f.select = not knit[f.index]
        for corner in f.loops:
            corner[uv].select = corner[uv].select_edge = f.select
    bm.select_flush_mode()
    bmesh.update_edit_mesh(mesh)
    bpy.ops.uv.unwrap(method="ANGLE_BASED", fill_holes=True, margin=PIECE_GAP)
    bpy.ops.uv.average_islands_scale()
    small = set(np.unique(piece[unseen]).tolist())
    for group in small:
        corners = [corner for i in np.where(piece == group)[0] for corner in bm.faces[i].loops]
        middle = sum((corner[uv].uv for corner in corners), Vector((0.0, 0.0))) / len(corners)
        for corner in corners:
            corner[uv].uv = middle + (corner[uv].uv - middle) * UNSEEN_SIZE
    bmesh.update_edit_mesh(mesh)
    bpy.ops.uv.pack_islands(rotate=True, scale=True, shape_method="CONCAVE", margin_method="FRACTION", margin=PIECE_GAP)
    for f in bm.faces:
        for i, corner in enumerate(f.loops):
            if knit[f.index]:
                corner[uv].uv = old[f.index][i]
            else:
                corner[uv].uv.y *= KNIT_BAND
    for e in bm.edges:
        e.seam = False
    bmesh.update_edit_mesh(mesh)
    bpy.ops.object.mode_set(mode="OBJECT")
    print("laid out in %d pieces: %d faces nobody sees at %.1f of the size, %d of the cuffs' ribbing kept in place" % (
        len(np.unique(piece[~knit])), unseen.sum(), UNSEEN_SIZE, knit.sum()))
    return old


def carry_colour(obj, old, atlas):
    """The game's map: each texel of the new layout takes the colour the old map has at the same place on the coat. Painted at the old
    map's size and cut down, every texel the mean of the texels in it that lie on the coat; the rest take their nearest neighbour's colour,
    so nothing but the coat's own cloth can bleed into a piece's edge at any distance."""
    mesh = obj.data
    h, w = atlas.shape[:2]
    new = np.empty(len(mesh.loops) * 2, dtype=np.float32)
    mesh.uv_layers[0].data.foreach_get("uv", new)
    new = new.reshape(-1, 3, 2).astype(np.float64) * (w, h) - 0.5
    was = old.astype(np.float64) * (w, h) - 0.5
    out = np.zeros((h, w, 3), dtype=np.float32)
    painted = np.zeros((h, w), dtype=bool)
    for a, b in zip(new, was):
        x0, y0 = np.maximum(np.floor(a.min(0)).astype(int), 0)
        x1, y1 = np.minimum(np.ceil(a.max(0)).astype(int), (w - 1, h - 1))
        e1, e2 = a[1] - a[0], a[2] - a[0]
        det = e1[0] * e2[1] - e1[1] * e2[0]
        if abs(det) < 1e-9 or x1 < x0 or y1 < y0:
            continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
        px, py = xs - a[0][0], ys - a[0][1]
        s = (px * e2[1] - py * e2[0]) / det
        t = (py * e1[0] - px * e1[1]) / det
        inside = (s >= -1e-3) & (t >= -1e-3) & (s + t <= 1.0 + 1e-3)
        if not inside.any():
            continue
        s, t = s[inside], t[inside]
        at = b[0] * (1.0 - s - t)[:, None] + b[1] * s[:, None] + b[2] * t[:, None]
        ax = np.clip(at[:, 0], 0.0, w - 1.001)
        ay = np.clip(at[:, 1], 0.0, h - 1.001)
        ix, iy = ax.astype(int), ay.astype(int)
        fx, fy = (ax - ix)[:, None], (ay - iy)[:, None]
        colour = (atlas[iy, ix, :3] * (1 - fx) + atlas[iy, ix + 1, :3] * fx) * (1 - fy) + (atlas[iy + 1, ix, :3] * (1 - fx) + atlas[iy + 1, ix + 1, :3] * fx) * fy
        out[ys[inside], xs[inside]] = colour
        painted[ys[inside], xs[inside]] = True
    knit = int(h * KNIT_FROM)
    old_columns = int(w * ATLAS_OLD)
    out[knit:, old_columns:] = atlas[knit:, old_columns:, :3]
    painted[knit:, old_columns:] = True
    step = w // GAME_MAP
    count = painted.reshape(GAME_MAP, step, GAME_MAP, step).sum((1, 3))
    total = (out * painted[..., None]).reshape(GAME_MAP, step, GAME_MAP, step, 3).sum((1, 3))
    small = total / np.maximum(count, 1)[..., None]
    print("map carried over: %.0f%% of it is the coat's" % (100.0 * (count > 0).mean()))
    return fill_unknown(small.astype(np.float32), count > 0)
