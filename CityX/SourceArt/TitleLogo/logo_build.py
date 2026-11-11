# THE CITYX LOGO IN 3D, from the owner's drawing (logo_drawing.png), headless:
#   blender --background --factory-startup --python logo_build.py -- [--masks <dir>] [--render <dir>] [--export <fbx path>]
# Owner, 2026-11-11: the title's logo "changed to this new version ... try to make a 3d version of the logo that looks like the file called
# 3dcityxlogo.jpg" (logo_3d_reference.jpg here), "the magenta thin lined, graffiti style text in front that says CITYX to have like a neon sign
# effect"; then "maybe just try building the same thing using the original drawing".
#
# The drawing is four flat colours on a clear ground, and each becomes a part:
#   Letters  the letters' whole shape, red outline and all, as a slab with black sides; Rim, its front, red (the drawing's outline). Two
#            objects, not two materials on one: the game took a mesh's two in the other order (red sides, a black front).
#   Face     the black inside the red outline, standing on the slab's front and chiselled up to a ridge along each stroke's middle, so its
#            facets catch the light at angles, as the reference's do (a roof over the stroke: every point rises with its distance in from
#            the edge, the ridges where that distance peaks)
#   Neon     the magenta strokes as rounded ribbons floating in front of the ridges: the game lights and flickers them
#   Tag      the white "CITYX" and its arrow along the top, the same way, white
# Units: the drawing's width is LOGO_WIDTH; it faces -Y (Blender's front), up +Z, and is exported with the drawing's middle at the origin.
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.geometry import delaunay_2d_cdt

HERE = os.path.dirname(os.path.abspath(__file__))
DRAWING = os.path.join(HERE, "logo_drawing.png")
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

LOGO_WIDTH = 6.0            # metres across, whatever the game scales it to
SLAB_DEPTH = 0.22           # the letters' slab, front to back
RIM_PX = 10                 # the red outline's width in the drawing, pixels
FACE_RAISE = 0.02          # the black face's foot stands this far proud of the slab's red front: a crisp step at the outline
RIDGE_SLOPE = 0.75          # the face rises this much per unit in from its edge (about 37 degrees)
RIDGE_CAP_PX = 24           # and no higher than it is this far in: the wide parts flatten on top
GRID_PX = 3                 # where the ridges are looked for
RIDGE_CELL_PX = 12          # one ridge point kept in each such square
CREASE_DEGREES = 35         # the face's facets shade as one plane each, folding sharp only at the ridges and its foot
NEON_CLEAR = 0.03           # the neon floats this far in front of the highest ridge
TAG_CLEAR = 0.08
NEON_RADIUS_PX = 2          # the ribbons' rounded edges, in the drawing's pixels
TAG_RADIUS_PX = 3
# Slots the drawing outlines in red but leaves empty, filled black: a point inside each, and a red stroke closing it off from the space it
# opens into, in the drawing's pixels (x right, y down). Owner, 2026-11-11, of the slot left of the Y: "fill in this gap to the left of the Y
# with black it was unintentionally left empty with red outline" (its red line on the left stops short of the bar above: carried up to it).
FILLED_GAPS = [((700, 250), ((687, 229), (686, 219)))]
GAP_STROKE_PX = 4.5         # the closing stroke's half-width, as wide as the drawing's red lines


# --- masks -------------------------------------------------------------------------------------------------------------------------------------

def load_pixels(path):
    image = bpy.data.images.load(path)
    w, h = image.size
    px = np.array(image.pixels[:], dtype=np.float32).reshape(h, w, 4)
    return px, w, h


def shift(m, dy, dx):
    out = np.zeros_like(m)
    h, w = m.shape
    ys, yd = (slice(0, h - dy), slice(dy, h)) if dy >= 0 else (slice(-dy, h), slice(0, h + dy))
    xs, xd = (slice(0, w - dx), slice(dx, w)) if dx >= 0 else (slice(-dx, w), slice(0, w + dx))
    out[yd, xd] = m[ys, xs]
    return out


def dilate(m, r):
    for _ in range(r):
        m = m | shift(m, 1, 0) | shift(m, -1, 0) | shift(m, 0, 1) | shift(m, 0, -1)
    return m


def erode(m, r):
    return ~dilate(~m, r)


def opening(m, r):
    return dilate(erode(m, r), r)


def closing(m, r):
    return erode(dilate(m, r), r)


def flood(free, seed):
    """The connected empty space round `seed` (x, y down)."""
    h, w = free.shape
    x0, y0 = seed
    if not free[y0, x0]:
        raise ValueError("FILLED_GAPS point %s is not in an empty space" % (seed,))
    seen = np.zeros_like(free)
    seen[y0, x0] = True
    stack = [(y0, x0)]
    while stack:
        y, x = stack.pop()
        for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
            if 0 <= yy < h and 0 <= xx < w and free[yy, xx] and not seen[yy, xx]:
                seen[yy, xx] = True
                stack.append((yy, xx))
        if len(stack) > 50000:
            raise ValueError("FILLED_GAPS point %s is in the open, not an enclosed slot" % (seed,))
    return seen


def stroke(shape, p, q, half_width):
    """The pixels within `half_width` of the segment p-q (x, y down)."""
    h, w = shape
    ys, xs = np.mgrid[0:h, 0:w]
    p, q = np.array(p, dtype=np.float64), np.array(q, dtype=np.float64)
    d = q - p
    t = np.clip(((xs + 0.5 - p[0]) * d[0] + (ys + 0.5 - p[1]) * d[1]) / max(d @ d, 1e-9), 0.0, 1.0)
    return np.hypot(xs + 0.5 - p[0] - t * d[0], ys + 0.5 - p[1] - t * d[1]) <= half_width


def masks(px):
    a = px[..., 3] > 0.5
    r, g, b = px[..., 0], px[..., 1], px[..., 2]
    red_ink = a & (r > 0.45) & (g < 0.3) & (b < 0.4)
    # The slots closed off and filled black, their red outlines kept (the owner's own paint-over showed both).
    gap = np.zeros_like(a)
    for seed, (p, q) in FILLED_GAPS:
        closer = stroke(a.shape, p, q, GAP_STROKE_PX)
        red_ink |= closer
        a = a | closer
        gap |= flood(~a, seed)
    gap = dilate(gap, 1) & ~a | gap
    a = a | gap
    r, g, b = (np.where(gap, 0.0, c) for c in (r, g, b))   # (an empty pixel's colour is anything, white too)
    # (Its edges blend into the black: a darker purple still the neon's.)
    magenta = closing(a & (r > 0.22) & (b > 0.22) & (g < 0.3) & (np.abs(r - b) < 0.3 * np.maximum(r, b) + 0.05), 1)
    white = closing(a & (np.minimum(np.minimum(r, g), b) > 0.5), 1)
    # The letters: everything drawn but the white tag (the neon over them and its blended edges included); the tag's thin black outline falls
    # away in the opening, and where the tag crosses a letter the hole it leaves is filled back (only where the letters close round it).
    body = opening(a & ~white, 4)
    tag_area = dilate(white, 4)
    letters = opening(body | (tag_area & closing(body, 14)), 2)
    # The red outline, closed across where a neon stroke crosses it; the face is the letters' inside it (the neon and the tag over the black
    # are the face's too).
    red = closing(red_ink, 5)
    face = opening(letters & ~dilate(red, 1), 3)
    return {"letters": letters, "face": face, "red": red, "neon": magenta, "tag": white}


# --- contours: the outline of a mask as closed loops of points ------------------------------------------------------------------------------------

def contours(m):
    """Every boundary between a filled pixel and an empty one, chained into closed loops with the filled side on the left (outer ones
    anticlockwise, holes clockwise), in pixel-corner coordinates (x right, y up)."""
    h, w = m.shape
    p = np.zeros((h + 2, w + 2), dtype=bool)
    p[1:-1, 1:-1] = m
    # Where two pixels touch only at a corner, two edges leave that corner: kept as lists, and the walk takes the left one, hugging the pixel
    # it is going round (one stored per corner, the loops jumped across: long rods).
    nxt = {}

    def add(a, b):
        nxt.setdefault(a, []).append(b)

    ys, xs = np.nonzero(p)
    for y, x in zip(ys.tolist(), xs.tolist()):
        # (corner coordinates with y up: row y of the array is drawing row y from the top)
        X, Y = x - 1, h - (y - 1)
        if not p[y - 1, x]:
            add((X + 1, Y), (X, Y))           # top edge, right to left
        if not p[y + 1, x]:
            add((X, Y - 1), (X + 1, Y - 1))   # bottom edge, left to right
        if not p[y, x - 1]:
            add((X, Y), (X, Y - 1))           # left edge, downward
        if not p[y, x + 1]:
            add((X + 1, Y - 1), (X + 1, Y))   # right edge, upward

    def take(here, came):
        ends = nxt[here]
        if len(ends) > 1 and came is not None:
            dx, dy = here[0] - came[0], here[1] - came[1]
            ends.sort(key=lambda e: -(dx * (e[1] - here[1]) - dy * (e[0] - here[0])))   # the left turn first
        end = ends.pop(0)
        if not ends:
            del nxt[here]
        return end

    loops = []
    while nxt:
        start = next(iter(nxt))
        loop = [start]
        came, here = start, take(start, None)
        while here != start and here in nxt:
            loop.append(here)
            came, here = here, take(here, came)
        if len(loop) >= 8:
            loops.append(loop)
    return loops


def simplify(points, tolerance):
    """Douglas-Peucker on a closed loop."""
    pts = np.array(points, dtype=np.float64)
    if len(pts) < 4:
        return pts

    def dp(a, b):
        if b <= a + 1:
            return [a]
        seg = pts[a:b + 1]
        start, end = pts[a], pts[b]
        d = end - start
        n = np.hypot(*d)
        if n < 1e-9:
            dist = np.hypot(*(seg - start).T)
        else:
            dist = np.abs(d[0] * (seg[:, 1] - start[1]) - d[1] * (seg[:, 0] - start[0])) / n
        i = int(np.argmax(dist))
        if dist[i] <= tolerance:
            return [a]
        return dp(a, a + i) + dp(a + i, b)

    far = int(np.argmax(np.hypot(*(pts - pts[0]).T)))
    keep = dp(0, far) + dp(far, len(pts) - 1)
    return pts[keep]


def chaikin(pts, rounds):
    for _ in range(rounds):
        nxt = np.roll(pts, -1, axis=0)
        pts = np.stack([0.75 * pts + 0.25 * nxt, 0.25 * pts + 0.75 * nxt], axis=1).reshape(-1, 2)
    return pts


def smooth_loops(m, tolerance=1.1, rounds=1):
    return [chaikin(simplify(loop, tolerance), rounds) for loop in contours(m)]


def inside(points, loops):
    """Which points are inside the loops (even-odd, so a hole's inside is out)."""
    x, y = points[:, 0][:, None], points[:, 1][:, None]
    result = np.zeros(len(points), dtype=bool)
    for loop in loops:
        a, b = loop, np.roll(loop, -1, axis=0)
        for i in range(0, len(a), 256):
            ax, ay, bx, by = a[i:i + 256, 0], a[i:i + 256, 1], b[i:i + 256, 0], b[i:i + 256, 1]
            spans = (ay > y) != (by > y)
            dy = np.where(by == ay, 1e-12, by - ay)
            crossing = x < ax + (y - ay) * (bx - ax) / dy
            result ^= np.logical_xor.reduce(spans & crossing, axis=1)
    return result


# --- building ---------------------------------------------------------------------------------------------------------------------------------

def to_world(pts, w, h):
    scale = LOGO_WIDTH / w
    return (pts - np.array([w * 0.5, h * 0.5])) * scale


def material(name, rgb, emission=0.0, roughness=0.4, metallic=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if emission > 0.0:
        bsdf.inputs["Emission Color"].default_value = (*rgb, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission
    return m


def filled_curve(name, loops, w, h, depth, bevel, front_y, mats, rounded=False):
    """Closed loops as one filled 2D curve, extruded `depth` back from `front_y`, `rounded` all round by `bevel` (a ribbon that reads as a
    tube)."""
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "2D"
    cu.fill_mode = "BOTH"
    cu.extrude = depth * 0.5
    cu.bevel_depth = bevel
    cu.bevel_resolution = 2 if rounded else 0
    for loop in loops:
        world = to_world(loop, w, h)
        sp = cu.splines.new("POLY")
        sp.points.add(len(world) - 1)
        for i, (x, y) in enumerate(world):
            sp.points[i].co = (x, y, 0.0, 1.0)
        sp.use_cyclic_u = True
    ob = bpy.data.objects.new(name, cu)
    bpy.context.scene.collection.objects.link(ob)
    ob.rotation_euler = (math.radians(90.0), 0.0, 0.0)
    ob.location = (0.0, front_y + depth * 0.5 + bevel, 0.0)
    for m in mats:
        ob.data.materials.append(m)
    return ob


def to_mesh(ob, keep=lambda face: True):
    """The curve made a mesh, applied in place, keeping the faces `keep` says; its back (never seen) dropped."""
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.convert(target="MESH")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bm.normal_update()
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.normal.y > 0.7 or not keep(f)], context="FACES")
    bm.to_mesh(ob.data)
    bm.free()
    return ob


def facing_front(face):
    return face.normal.y < -0.9


def chiselled_face(name, face_mask, w, h, mat):
    """The black face: its outline at FACE_RAISE in front of the slab, rising in to ridges along each stroke's middle, with a short wall
    down to the slab all round its edge."""
    loops = smooth_loops(face_mask, tolerance=1.5)
    # The edge, sampled densely, for the distance in from it.
    edge = []
    for loop in loops:
        nxt = np.roll(loop, -1, axis=0)
        for p, q in zip(loop, nxt):
            k = max(1, int(np.hypot(*(q - p)) / 1.5))
            edge.append(p + (q - p) * (np.arange(k)[:, None] / k))
    edge = np.concatenate(edge)

    # The distance in from the edge on a grid over the face, and the ridges: where it peaks across some direction (a sharp fold, not the
    # level run along a slope).
    rows, cols = np.mgrid[GRID_PX // 2:h:GRID_PX, GRID_PX // 2:w:GRID_PX]
    on_face = face_mask[rows, cols]
    grid = np.stack([cols + 0.5, h - rows - 0.5], axis=-1)
    dist = np.zeros(on_face.shape)
    flat = grid[on_face]
    near = np.empty(len(flat))
    for i in range(0, len(flat), 1024):
        chunk = flat[i:i + 1024]
        near[i:i + 1024] = np.sqrt(((chunk[:, None, :] - edge[None, :, :]) ** 2).sum(-1)).min(axis=1)
    dist[on_face] = near
    ridge = np.zeros(on_face.shape, dtype=bool)
    for dy, dx in ((0, 1), (1, 0), (1, 1), (1, -1)):
        ahead, behind = np.roll(dist, (-dy, -dx), axis=(0, 1)), np.roll(dist, (dy, dx), axis=(0, 1))
        fold = 2.0 * dist - ahead - behind
        ridge |= (dist >= ahead) & (dist >= behind) & (fold > 0.5 * GRID_PX)
    ridge &= on_face & (dist > 4.0)
    # One point a square, the highest in it.
    picks = {}
    for r, c in zip(*np.nonzero(ridge)):
        u, v = grid[r, c]
        key = (int(u // RIDGE_CELL_PX), int(v // RIDGE_CELL_PX))
        if key not in picks or dist[r, c] > picks[key][1]:
            picks[key] = ((u, v), dist[r, c])
    ridge_pts = np.array([p for p, _ in picks.values()])
    ridge_px = np.array([d for _, d in picks.values()])
    px_size = LOGO_WIDTH / w
    ridge_h = RIDGE_SLOPE * np.minimum(ridge_px, RIDGE_CAP_PX) * px_size

    # Triangulated between the outline (constrained) and the ridge points; what falls outside the face (round it, in its holes) is dropped.
    outline = np.concatenate(loops)
    starts = np.cumsum([0] + [len(l) for l in loops])
    edges = []
    for li, loop in enumerate(loops):
        s = starts[li]
        n = len(loop)
        edges += [(s + i, s + (i + 1) % n) for i in range(n)]
    coords = [Vector((float(u), float(v))) for u, v in np.concatenate([outline, ridge_pts])]
    heights = np.concatenate([np.zeros(len(outline)), ridge_h])
    out_verts, _, out_faces, out_orig, _, _ = delaunay_2d_cdt(coords, edges, [], 0, 1e-6)
    out_xy = np.array([(p.x, p.y) for p in out_verts])
    out_h = np.array([heights[o[0]] if o else 0.0 for o in out_orig])
    tris = [f for f in out_faces if len(f) == 3]
    centres = np.array([out_xy[list(f)].mean(axis=0) for f in tris])
    keep = inside(centres, loops)
    tris = [f for f, k in zip(tris, keep) if k]

    world = to_world(out_xy, w, h)
    bm = bmesh.new()
    top = [bm.verts.new((x, -FACE_RAISE - z, y)) for (x, y), z in zip(world, out_h)]
    for f in tris:
        a, b, c = f
        # Anticlockwise across the drawing faces the front (-Y).
        p, q, r = out_xy[a], out_xy[b], out_xy[c]
        if (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0]) < 0.0:
            b, c = c, b
        bm.faces.new((top[a], top[b], top[c]))
    # The wall from the outline down into the slab's front.
    out_of = {}
    for i, originals in enumerate(out_orig):
        for k in originals:
            out_of.setdefault(k, i)
    for li, loop in enumerate(loops):
        s = starts[li]
        n = len(loop)
        rim = [top[out_of[s + i]] for i in range(n)]
        foot = [bm.verts.new((v.co.x, 0.005, v.co.z)) for v in rim]
        for i in range(n):
            j = (i + 1) % n
            wall = bm.faces.new((rim[i], foot[i], foot[j], rim[j]))
            # The filled side is on the loop's left, so outward is its right.
            along = rim[j].co - rim[i].co
            wall.normal_update()
            if wall.normal.x * along.z - wall.normal.z * along.x < 0.0:
                wall.normal_flip()
    bm.normal_update()
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    ob = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(ob)
    mesh.materials.append(mat)
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(CREASE_DEGREES))
    print("face: %d ridge points, highest %.3f m" % (len(ridge_pts), ridge_h.max()))
    return ob, float(ridge_h.max())


def save_mask(m, path):
    h, w = m.shape
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=False)
    rgba = np.repeat(np.flipud(m)[..., None].astype(np.float32), 4, axis=2)
    rgba[..., 3] = 1.0
    img.pixels = rgba.ravel()
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    px, w, h = load_pixels(DRAWING)
    px = np.flipud(px)   # row 0 at the top, as the drawing is read
    m = masks(px)
    if "--masks" in args:
        out = args[args.index("--masks") + 1]
        os.makedirs(out, exist_ok=True)
        for k, v in m.items():
            save_mask(v, os.path.join(out, k + ".png"))

    black = material("LogoBlack", (0.015, 0.015, 0.018), roughness=0.4)
    red = material("LogoRed", (0.62, 0.02, 0.05), roughness=0.35)
    neon = material("Neon", (0.85, 0.05, 0.9), emission=1.5, roughness=0.1)
    tag = material("Tag", (0.92, 0.92, 0.94), roughness=0.15, metallic=0.6)

    letter_loops = smooth_loops(m["letters"])
    slab = to_mesh(filled_curve("Letters", letter_loops, w, h, SLAB_DEPTH, 0.0, 0.0, [black]), keep=lambda f: not facing_front(f))
    rim = to_mesh(filled_curve("Rim", letter_loops, w, h, SLAB_DEPTH, 0.0, 0.0, [red]), keep=facing_front)
    face, highest = chiselled_face("Face", m["face"], w, h, black)
    # The neon and the tag: each stroke's own outline, drawn in by the rounding and rounded back out by it. (Traced down to centre lines and
    # made round tubes, the junctions split them into pieces and the short ones dropped out: dashed neon.)
    px_size = LOGO_WIDTH / w
    neon_front = -FACE_RAISE - highest - NEON_CLEAR
    tag_front = -FACE_RAISE - highest - TAG_CLEAR
    neon_ob = to_mesh(filled_curve("Neon", smooth_loops(erode(m["neon"], NEON_RADIUS_PX)), w, h, 0.004, NEON_RADIUS_PX * px_size,
                                   neon_front, [neon], rounded=True))
    tag_ob = to_mesh(filled_curve("Tag", smooth_loops(erode(m["tag"], TAG_RADIUS_PX)), w, h, 0.004, TAG_RADIUS_PX * px_size,
                                  tag_front, [tag], rounded=True))
    made = [slab, rim, face, neon_ob, tag_ob]
    for o in (neon_ob, tag_ob):
        for poly in o.data.polygons:
            poly.use_smooth = True
    for o in made:
        print("%-8s %6d triangles" % (o.name, sum(len(p.vertices) - 2 for p in o.data.polygons)))
    return made


MADE = build()

if "--export" in args:
    path = args[args.index("--export") + 1]
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in MADE:
        o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_unit_scale=True, bake_space_transform=True, mesh_smooth_type="FACE",
                             axis_forward="-Z", axis_up="Y", add_leaf_bones=False)
    print("wrote", path)

if "--render" in args:
    out = args[args.index("--render") + 1]
    os.makedirs(out, exist_ok=True)
    scene = bpy.context.scene
    try:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError:
        scene.render.engine = "BLENDER_EEVEE"
    scene.view_settings.view_transform = "Standard"
    scene.render.resolution_x, scene.render.resolution_y = 1365, 768
    scene.world = bpy.data.worlds.new("World")
    scene.world.use_nodes = True
    bg = next(n for n in scene.world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (0.35, 0.35, 0.34, 1.0)
    bg.inputs["Strength"].default_value = 0.6
    wall = bpy.data.meshes.new("Wall")
    wall.from_pydata([(-20, 1.0, -12), (20, 1.0, -12), (20, 1.0, 12), (-20, 1.0, 12)], [], [(0, 1, 2, 3)])
    wall_ob = bpy.data.objects.new("Wall", wall)
    scene.collection.objects.link(wall_ob)
    wall_ob.data.materials.append(material("Concrete", (0.55, 0.55, 0.53), roughness=0.9))
    for name, rot, energy in (("Key", (math.radians(50), 0, math.radians(-30)), 3.0), ("Fill", (math.radians(60), 0, math.radians(40)), 1.2)):
        light = bpy.data.lights.new(name, "SUN")
        light.energy = energy
        lo = bpy.data.objects.new(name, light)
        lo.rotation_euler = rot
        scene.collection.objects.link(lo)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    for view, position in (("front", Vector((0.0, -9.0, 0.0))), ("angle", Vector((-3.5, -8.0, 1.5)))):
        cam.location = position
        cam.rotation_euler = (Vector((0.0, 0.0, 0.0)) - position).to_track_quat("-Z", "Y").to_euler()
        cam.data.lens = 50 if view == "front" else 45
        scene.render.filepath = os.path.join(out, "logo_%s.png" % view)
        bpy.ops.render.render(write_still=True)
    print("rendered to", out)
