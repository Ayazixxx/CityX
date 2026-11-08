# CRIMSON CLAW: the owner's Meshy model with its four unwanted holes closed and its triangles cut down (owner, 2026-11-07: "there a few
# unintended holes in the crimson claw head id like you to patch up ... make it look smooth so you could never tell they were there. after
# that id like you to lower the face count of the hole thing to a reasonable amount").
#
#   "C:/Program Files/Blender Foundation/Blender 4.2/blender.exe" -b --factory-startup --python claw_patch.py -- <in.fbx> <out stem> [triangles]
#
# The model is a shell, like a hood: a wall a few centimetres thick round a hollow that is open underneath, behind the lower jaw. The holes
# go through that wall: one under each side spike, one on each side of the top of the head. Each hole is a short tube from the wall's
# outside to its inside. The tube is deleted, which leaves an opening on each face of the wall, and each opening is closed by a disc of
# rings faired into the surface round it (the disc and a band of the surface round it moved to the smoothest surface the band's edge
# allows: the squared umbrella Laplacian least, solved by conjugate gradients), so the surface carries on over where the hole was.
# The two side spikes (a horn back and a hook hanging over the hole) are never cut or moved: the disc meets them as they are (owner:
# "please try to avoid touching the two spikes on the side of the head"). --preview <file.blend> stops before cutting and colours what
# would be cut red and the spikes blue.
# Then the whole model is decimated to [triangles] (default 20000) and split into its three pieces, Body, LeftClaw and RightClaw (the
# creature's own left and right; it faces -Y here), written as <out stem>.fbx and <out stem>.glb.
import math
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

args = sys.argv[sys.argv.index("--") + 1:]
SOURCE, OUT = args[0], args[1]
TRIANGLES = int(args[2]) if len(args) > 2 else 20000
PREVIEW = args[args.index("--preview") + 1] if "--preview" in args else None   # a .blend with the faces to cut red, and stop
# The holes: a point in each (from rays through them), the rough way through the wall, and how much of it a spike over it hides from
# outside (the left spike's hook hangs well over its hole; the right one's only a little).
HOLES = [
    ("side left", Vector((-0.43, 0.05, 0.29)), Vector((-1, 0, 0)), 0.06),
    ("side right", Vector((0.41, 0.05, 0.29)), Vector((1, 0, 0)), 0.03),
    ("top left", Vector((-0.20, -0.305, 0.42)), Vector((0, 0, 1)), 0.0),
    ("top right", Vector((0.18, -0.305, 0.42)), Vector((0, 0, 1)), 0.0),
]
TUBE_RADIUS = {"side": 0.16, "top": 0.09}   # how far from a hole's line its opening is looked for
TUBE_LENGTH = 0.10                          # how far along the line, either way from the point, its tube can reach
MASK_STEP = 0.0025                          # spacing of the rays that find where a hole is open
OUTSIDE = 0.35                              # ... shot from this far out along the way through
INSIDE = 0.10                               # ... and from this far into the hollow, as far from where it is open from outside as a spike hides
OPENING_MARGIN = 0.012                      # a tube face lies, seen along the way through, this close to where the hole is open
SPIKE_GAP = 0.02                            # a side spike: what stands this far out past the wall's outside round the hole (its lip less) ...
SPIKE_REACH = 0.25                          # ... within this of the hole
SPIKES = set()                              # its corners, never cut or moved
TUBE_TURN = 0.92                            # a tube face (its rounded lip too) is turned across the way through: |n . axis| under this
BAND = 0.03                                 # metres of the surface round an opening faired with its disc

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SOURCE)
ob = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
bm = bmesh.new()
bm.from_mesh(ob.data)
bm.faces.ensure_lookup_table()
bm.verts.ensure_lookup_table()
tree = BVHTree.FromBMesh(bm)
print(f"in: {len(bm.verts)} corners, {len(bm.faces)} triangles")


def outward_axis(centre, guess):
    """The wall's outward normal round a hole: the mean normal of the faces 4-10 cm off the hole's line that face the guess's way."""
    total = Vector()
    for f in bm.faces:
        c = f.calc_center_median()
        off = c - centre
        if off.length > 0.14:
            continue
        across = (off - guess * off.dot(guess)).length
        if 0.04 < across < 0.10 and f.normal.dot(guess) > 0.3:
            total += f.normal * f.calc_area()
    return total.normalized() if total.length > 0 else guess


def joined_to_middle(cells):
    """The cells joined, side by side, to the middle one."""
    joined, stack = set(), [(0, 0)] if (0, 0) in cells else []
    while stack:
        cell = stack.pop()
        if cell in joined:
            continue
        joined.add(cell)
        for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nxt = (cell[0] + di, cell[1] + dj)
            if nxt in cells and nxt not in joined:
                stack.append(nxt)
    return joined


def opening(centre, axis, reach, hidden):
    """Where the hole is open, seen along its way through: the spots a ray from outside passes the wall's middle (the plane through
    <centre>) without meeting it, and within <hidden> of those the spots a ray from the hollow does, all joined to the middle spot; as
    points across the hole (u, v, 0) for a KD tree. Also how far out the wall's outside is round the hole."""
    u = axis.orthogonal().normalized()
    v = axis.cross(u).normalized()
    steps = int(reach / MASK_STEP)
    spot = lambda i, j: centre + u * (i * MASK_STEP) + v * (j * MASK_STEP)
    cells = [(i, j) for i in range(-steps, steps + 1) for j in range(-steps, steps + 1)]
    # from outside in: open where the ray passes the wall's middle; elsewhere it meets the wall's outside
    open_outside, surface = set(), {}
    for i, j in cells:
        loc, nor, idx, dist = tree.ray_cast(spot(i, j) + axis * OUTSIDE, -axis, OUTSIDE + 0.01)
        if loc is None:
            open_outside.add((i, j))
        else:
            surface[(i, j)] = OUTSIDE - dist
    seen = joined_to_middle(open_outside)
    # the wall's outside round the hole's edge: the middle height of the spots just past where it is open
    rim = sorted(surface[(i + di, j + dj)] for (i, j) in seen for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1))
                 if (i + di, j + dj) in surface)
    outer = rim[len(rim) // 2] if rim else TUBE_LENGTH
    # from the hollow out: also open where the first thing met is past the wall's middle (a spike over a side hole hides
    # part of the hole from outside, not from in here)
    open_at = set(open_outside)
    near = KDTree(max(1, len(seen)))
    for k, (i, j) in enumerate(seen):
        near.insert(Vector((i, j, 0)), k)
    near.balance()
    for i, j in cells:
        if not seen or near.find(Vector((i, j, 0)))[2] * MASK_STEP > hidden:
            continue
        loc, nor, idx, dist = tree.ray_cast(spot(i, j) - axis * INSIDE, axis, INSIDE + OUTSIDE)
        if loc is None or dist - INSIDE > 0.01:   # past the wall's middle
            open_at.add((i, j))
    joined = joined_to_middle(open_at)
    spots = KDTree(max(1, len(joined)))
    for k, (i, j) in enumerate(joined):
        spots.insert(Vector((i * MASK_STEP, j * MASK_STEP, 0)), k)
    spots.balance()
    return u, v, spots, len(joined), outer


def tube(name, centre, axis, hidden):
    """The faces of a hole's tube: flooded from the walls a ray across the hole hits, through faces turned across the way through that
    lie, seen along it, within OPENING_MARGIN of where the hole is open (so the brow over a side hole keeps its overhang)."""
    reach = TUBE_RADIUS[name.split()[0]]
    u, v, spots, cells, outer = opening(centre, axis, reach, hidden)
    if name.startswith("side"):
        # the spike over a side hole stands out past the wall's outside: it is never cut or moved (owner, 2026-11-07: "please try to
        # avoid touching the two spikes on the side of the head")
        for vert in bm.verts:
            off = vert.co - centre
            along = off.dot(axis)
            if outer + SPIKE_GAP < along < SPIKE_REACH and (off - axis * along).length < SPIKE_REACH:
                SPIKES.add(vert)
    def fits(f):
        off = f.calc_center_median() - centre
        along = off.dot(axis)
        if abs(along) >= TUBE_LENGTH or abs(f.normal.dot(axis)) >= TUBE_TURN or any(x in SPIKES for x in f.verts):
            return False
        co, k, dist = spots.find(Vector((off.dot(u), off.dot(v), 0)))
        return dist < OPENING_MARGIN
    seeds = []
    side = axis.orthogonal().normalized()
    for k in range(8):
        d = (side * math.cos(k * math.pi / 4) + axis.cross(side) * math.sin(k * math.pi / 4)).normalized()
        loc, nor, idx, dist = tree.ray_cast(centre, d, reach)
        if idx is not None and fits(bm.faces[idx]):
            seeds.append(bm.faces[idx])
    picked = set()
    stack = list(seeds)
    while stack:
        f = stack.pop()
        if f.index in picked:
            continue
        picked.add(f.index)
        for e in f.edges:
            for g in e.link_faces:
                if g.index not in picked and fits(g):
                    stack.append(g)
    print(f"  {name}: way through {tuple(round(c, 2) for c in axis)}, open over {cells * MASK_STEP * MASK_STEP * 1e4:.0f} cm2, "
          f"outside {outer * 100:.1f} cm out, {len(seeds)} seeds, {len(picked)} tube faces, {len(SPIKES)} spike corners so far")
    return picked


doomed = set()
for name, centre, guess, hidden in HOLES:
    axis = outward_axis(centre, guess)
    doomed |= tube(name, centre, axis, hidden)
if PREVIEW:
    for f in bm.faces:
        f.material_index = 1 if f.index in doomed else 2 if all(x in SPIKES for x in f.verts) else 0
    bm.to_mesh(ob.data)
    for name, colour in (("kept", (0.8, 0.8, 0.8, 1)), ("cut", (0.9, 0.05, 0.05, 1)), ("spike", (0.1, 0.3, 0.9, 1))):
        material = bpy.data.materials.new(name)
        material.diffuse_color = colour
        ob.data.materials.append(material)
    bpy.ops.wm.save_as_mainfile(filepath=PREVIEW)
    sys.exit(0)
bmesh.ops.delete(bm, geom=[bm.faces[i] for i in doomed], context='FACES')

# Spurs left along the cut (a face with two or more open edges) go too, so each opening is one clean loop.
while True:
    spurs = [f for f in bm.faces if sum(1 for e in f.edges if e.is_boundary) >= 2]
    if not spurs:
        break
    bmesh.ops.delete(bm, geom=spurs, context='FACES')
loose = [v for v in bm.verts if not v.link_faces]
bmesh.ops.delete(bm, geom=loose, context='VERTS')
bm.verts.ensure_lookup_table()
bm.faces.ensure_lookup_table()


def openings():
    """The open edges chained into loops, each in the order that closes it facing the same way as the surface round it."""
    open_edges = {e for e in bm.edges if e.is_boundary}
    loops = []
    while open_edges:
        e = open_edges.pop()
        face = e.link_faces[0]
        # the surface runs a -> b along this edge, so the closing disc runs b -> a
        for loop in face.loops:
            if loop.edge == e:
                a, b = loop.vert, loop.link_loop_next.vert
        chain = [b, a]
        while True:
            here = chain[-1]
            nxt = [x for x in here.link_edges if x.is_boundary and x in open_edges]
            if not nxt:
                break
            x = nxt[0]
            open_edges.discard(x)
            other = x.other_vert(here)
            if other == chain[0]:
                break
            chain.append(other)
        loops.append(chain)
    return loops


def fill_and_fair(chain):
    """Closes one opening with rings toward its middle, then fairs the disc and a band round it."""
    m = len(chain)
    pts = [v.co.copy() for v in chain]
    centre = sum(pts, Vector()) / m
    edge = sum((pts[i] - pts[(i + 1) % m]).length for i in range(m)) / m
    rings = max(2, round(sum((p - centre).length for p in pts) / m / edge))
    previous = list(chain)
    new_verts = []
    for k in range(1, rings):
        t = k / rings
        ring = [bm.verts.new(pts[i].lerp(centre, t)) for i in range(m)]
        new_verts += ring
        for i in range(m):
            j = (i + 1) % m
            bm.faces.new((previous[i], previous[j], ring[j], ring[i]))
        previous = ring
    middle = bm.verts.new(centre)
    new_verts.append(middle)
    for i in range(m):
        bm.faces.new((previous[i], previous[(i + 1) % m], middle))
    # the band: the old surface within BAND of the rim, by rings of neighbours
    band, frontier = set(chain) - SPIKES, set(chain) - SPIKES
    made = set(new_verts)
    for _ in range(max(1, round(BAND / edge))):
        nxt = set()
        for v in frontier:
            for e in v.link_edges:
                o = e.other_vert(v)
                if o not in band and o not in made and o not in SPIKES:
                    nxt.add(o)
        band |= nxt
        frontier = nxt
    free = list(band) + new_verts
    fair(free)
    return len(new_verts), rings, edge


def fair(free):
    """Moves the free corners to where the squared umbrella Laplacian over them and their neighbours is least (the others held)."""
    index = {v: i for i, v in enumerate(free)}
    rows = set(free)
    for v in free:
        for e in v.link_edges:
            rows.add(e.other_vert(v))
    row_list = list(rows)
    r_idx, c_idx, w, const = [], [], [], np.zeros((len(row_list), 3))
    for r, v in enumerate(row_list):
        nbrs = [e.other_vert(v) for e in v.link_edges]
        terms = [(v, 1.0)] + [(u, -1.0 / len(nbrs)) for u in nbrs]
        for u, weight in terms:
            if u in index:
                r_idx.append(r); c_idx.append(index[u]); w.append(weight)
            else:
                const[r] += weight * np.array(u.co)
    r_idx, c_idx, w = np.array(r_idx), np.array(c_idx), np.array(w)
    n = len(free)

    def M(x):
        out = np.zeros((len(row_list), 3))
        np.add.at(out, r_idx, w[:, None] * x[c_idx])
        return out

    def Mt(y):
        out = np.zeros((n, 3))
        np.add.at(out, c_idx, w[:, None] * y[r_idx])
        return out

    x = np.array([np.array(v.co) for v in free])
    b = -Mt(const)
    res = b - Mt(M(x))
    p = res.copy()
    rs = (res * res).sum(axis=0)
    for it in range(20000):
        Ap = Mt(M(p))
        alpha = rs / np.maximum((p * Ap).sum(axis=0), 1e-30)
        x += alpha * p
        res -= alpha * Ap
        rs_new = (res * res).sum(axis=0)
        if np.sqrt(rs_new.max()) < 1e-9:
            break
        p = res + (rs_new / np.maximum(rs, 1e-30)) * p
        rs = rs_new
    for v, co in zip(free, x):
        v.co = Vector(co)


loops = openings()
print("openings: " + ", ".join(f"{len(c)} edges at ({sum(v.co.x for v in c) / len(c):.2f}, {sum(v.co.y for v in c) / len(c):.2f}, "
                               f"{sum(v.co.z for v in c) / len(c):.2f})" for c in loops))
# each hole's tube cut through leaves two openings, one on each face of the wall; one alone would close the hole on one face only
assert len(loops) == 2 * len(HOLES), f"{len(loops)} openings, not two for each of the {len(HOLES)} holes"
for chain in loops:
    centre = sum((v.co for v in chain), Vector()) / len(chain)
    made, rings, edge = fill_and_fair(chain)
    print(f"  closed {len(chain)}-edge opening at ({centre.x:.3f}, {centre.y:.3f}, {centre.z:.3f}): {rings} rings, {made} corners, edge {edge*1000:.1f} mm")

bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])
open_left = sum(1 for e in bm.edges if e.is_boundary)
broken = sum(1 for e in bm.edges if not e.is_manifold)
print(f"patched: {len(bm.faces)} triangles, {open_left} open edges, {broken} broken edges, V-E+F {len(bm.verts) - len(bm.edges) + len(bm.faces)}")
bm.to_mesh(ob.data)
bm.free()
if "--save" in args:   # the patched model before decimating, as a .blend
    bpy.ops.wm.save_as_mainfile(filepath=args[args.index("--save") + 1])

# Decimate the whole model to the budget. Meshy's stored normals go first: the decimation scrambles them, and the .glb came out with a
# hard edge on every triangle; without them the surface is shaded smooth from its own shape.
bpy.context.view_layer.objects.active = ob
ob.select_set(True)
if ob.data.has_custom_normals:
    bpy.ops.mesh.customdata_custom_splitnormals_clear()
for name in ("sharp_edge", "sharp_face"):   # the FBX's smoothing groups, read in as hard edges
    if name in ob.data.attributes:
        ob.data.attributes.remove(ob.data.attributes[name])
for p in ob.data.polygons:
    p.use_smooth = True
mod = ob.modifiers.new("decimate", 'DECIMATE')
mod.decimate_type = 'COLLAPSE'
mod.ratio = TRIANGLES / len(ob.data.polygons)
bpy.ops.object.modifier_apply(modifier=mod.name)
print(f"decimated: {len(ob.data.polygons)} triangles")

# Its three pieces, named.
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.separate(type='LOOSE')
bpy.ops.object.mode_set(mode='OBJECT')
pieces = sorted([o for o in bpy.context.scene.objects if o.type == 'MESH'], key=lambda o: len(o.data.polygons), reverse=True)
body, claws = pieces[0], pieces[1:3]
body.name = body.data.name = "Body"
for claw in claws:
    x = sum((claw.matrix_world @ v.co).x for v in claw.data.vertices) / len(claw.data.vertices)
    # facing -Y, the creature's left hand is on +X
    claw.name = claw.data.name = "LeftClaw" if x > 0 else "RightClaw"
for o in pieces:
    for p in o.data.polygons:
        p.use_smooth = True
    print(f"  {o.name}: {len(o.data.polygons)} triangles")
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=OUT + ".fbx", use_selection=True, apply_scale_options='FBX_SCALE_ALL')
bpy.ops.export_scene.gltf(filepath=OUT + ".glb", export_format='GLB', use_selection=True)
print("written", OUT + ".fbx", OUT + ".glb")
