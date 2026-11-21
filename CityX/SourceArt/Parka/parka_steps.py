# The remodel's steps on the working coat (exec after the other parka_*.py files). Each takes and returns the state kept between calls.
ATLAS_OLD = 0.8            # Meshy's texture keeps this much of the atlas' width; the new patches' charts take the strip beside it
CHART_RINGS = 110.0        # read rings a sleeve patch's chart is tall
FLAT_RADIUS = 0.12         # the radius a ring's columns are laid flat at, for triangulating a sleeve patch
JOIN_FADE = 0.07           # a sleeve point this far from where sleeve and coat are still one skin moves in full, one on it not at all


def start_work(source_name):
    """A fresh working copy of the coat, its texture coordinates squeezed into the atlas' old part."""
    old = bpy.data.objects.get("Coat_work")
    if old:
        bpy.data.objects.remove(old)
    source = bpy.data.objects[source_name]
    obj = source.copy()
    obj.data = source.data.copy()
    obj.name = "Coat_work"
    bpy.context.scene.collection.objects.link(obj)
    obj.hide_set(False)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    uv = bm.loops.layers.uv.active
    for f in bm.faces:
        for corner in f.loops:
            corner[uv].uv.x *= ATLAS_OLD
    return {"obj": obj, "bm": bm, "uv": uv, "world": obj.matrix_world.copy(), "part": bm.verts.layers.int.new("part"),
            "mark": bm.verts.layers.int.new("mark"), "splice": bm.verts.layers.int.new("splice"), "owner": bm.faces.layers.int.new("owner"), "patches": {}, "joined": {}}


def cut_sleeve(state, reading):
    """One sleeve cut loose along its crease and both holes closed. The sleeve's points are noted in the part layer (1 left, 2 right)."""
    bm, world, uv, part = state["bm"], state["world"], state["uv"], state["part"]
    side = reading.side
    inverse = world.inverted()
    bm.faces.ensure_lookup_table()
    bm.faces.index_update()
    sleeve = sleeve_faces(bm, reading, world)
    sleeve_chain, coat_chain = cut_loose(bm, reading, world, sleeve, state["mark"], state["owner"])
    code = 1 if side > 0 else 2
    for f in bm.faces:
        if f[state["owner"]] == code:
            for v in f.verts:
                v[part] = code
    state["joined"][side] = np.array([world @ v.co for v in bm.verts if v[state["mark"]] == 2])
    per_column = 2.0 * math.pi / AROUND * FLAT_RADIUS

    def chain_points(chain):
        return mirrored(side, np.array([world @ v.co for v in chain]))

    on_sleeve = chain_points(sleeve_chain)
    k = reading.ring_of(on_sleeve)
    flat = np.stack([reading.angle_of(on_sleeve, k) * per_column, k * RING_STEP], 1)

    def place_on_sleeve(at):
        p = mirrored(side, reading.surface(at[:, 1] / RING_STEP, at[:, 0] / per_column))
        return [inverse @ Vector(q) for q in p]

    def sleeve_chart(at):
        return [sleeve_patch_uv(side, c, r) for c, r in zip(at[:, 0] / per_column, at[:, 1] / RING_STEP)]

    fresh, faces, chord = fill_patch(bm, sleeve_chain, flat, place_on_sleeve, sleeve_chart, uv, (-side, 0, 0))
    for v in fresh:
        v[part] = code
    on_coat = chain_points(coat_chain)

    def place_on_wall(at):
        return [inverse @ Vector((side * float(wall_x(side, z, y)), y, z)) for y, z in at]

    def wall_chart(at):
        return [wall_patch_uv(side, y, z) for y, z in at]

    shared = chord if sleeve_chain[0] is coat_chain[0] and sleeve_chain[-1] is coat_chain[-1] else None
    _, wall_faces, _ = fill_patch(bm, coat_chain, on_coat[:, 1:3].copy(), place_on_wall, wall_chart, uv, (side, 0, 0), shared)
    state["patches"][side] = {"sleeve": faces, "wall": wall_faces}
    low = min(p[2] for p in on_sleeve)
    print("side %d: %d sleeve faces; cut %d points from (%.3f %.3f %.3f) to (%.3f %.3f %.3f), lowest %.3f; patches %d and %d faces, armpit %s"
          % (side, sleeve.sum(), len(sleeve_chain), *on_sleeve[0], *on_sleeve[-1], low, len(faces), len(wall_faces),
             "shared" if shared else "NOT SHARED"))


def sleeve_patch_uv(side, column, ring):
    """Where a point of a sleeve's inner patch lies on the atlas: the columns facing the body across, the rings down."""
    return (ATLAS_OLD + (1.0 - ATLAS_OLD) * (column - AROUND / 4.0) / (AROUND / 2.0), (0.0 if side > 0 else 0.3) + 0.3 * ring / CHART_RINGS)


def wall_patch_uv(side, y, z):
    half = (1.0 - ATLAS_OLD) * 0.5
    return (ATLAS_OLD + (0.0 if side > 0 else half) + half * (y + 0.2) / 0.4, 0.6 + 0.2 * (z - 1.0) / 0.6)


def seam_plane(reading, ring):
    """The tilted plane of one of Meshy's quilting seams, in the world: a point of it and its normal, pointing down the sleeve."""
    at = np.array([ring])
    girth = float(reading.closed[int(ring)].mean())
    normal = reading.axis - RING_STEP / girth * (SEAM_TILT[0] * reading.along(reading.outs, at)[0] + SEAM_TILT[1] * BACK)
    return (Vector(mirrored(reading.side, reading.along(reading.centres, at))[0]),
            Vector(mirrored(reading.side, normal[None, :])[0]).normalized())


def sleeve_geometry(bm, part, code):
    faces = [f for f in bm.faces if all(v[part] == code for v in f.verts)]
    geom = set(faces)
    for f in faces:
        geom.update(f.edges)
        geom.update(f.verts)
    return list(geom)


def end_at_hem(state, reading):
    """The sleeve ended just under its rolled hem's lower lip: below it Meshy's sleeve runs on into its own ribbed cuff, which the plain
    tube of knit_cuff replaces."""
    bm, world, part = state["bm"], state["world"], state["part"]
    code = 1 if reading.side > 0 else 2
    point, normal = seam_plane(reading, HEM[reading.side][1] + HEM_UNDER)
    before = len(bm.faces)
    made = bmesh.ops.bisect_plane(bm, geom=sleeve_geometry(bm, part, code), plane_co=world.inverted() @ point, plane_no=normal,
                                  clear_outer=True, dist=1e-5)
    for item in made["geom_cut"]:
        if isinstance(item, bmesh.types.BMVert):
            item[part] = code
    print("side %d: sleeve ended under its hem, %d faces fewer" % (reading.side, before - len(bm.faces)))


def cut_band(state, reading):
    """The left sleeve's extra quilted band cut out along its two seams. The edge above is marked 1 in the splice layer and the edge below
    2: fit_sleeves moves the sleeve's end up to close the gap, join_band stitches the two."""
    bm, world, part, splice = state["bm"], state["world"], state["part"], state["splice"]
    side = reading.side
    if not CUT_BAND[side]:
        return
    code = 1 if side > 0 else 2
    inverse = world.inverted()
    planes = [seam_plane(reading, ring) for ring in CUT_BAND[side]]
    for mark, (point, normal) in enumerate(planes, 1):
        made = bmesh.ops.bisect_plane(bm, geom=sleeve_geometry(bm, part, code), plane_co=inverse @ point, plane_no=normal, dist=1e-5)
        for item in made["geom_cut"]:
            if isinstance(item, bmesh.types.BMVert):
                item[part] = code
                item[splice] = mark
    (above, above_normal), (below, below_normal) = planes
    gone = []
    for f in bm.faces:
        if all(v[part] == code for v in f.verts):
            middle = world @ f.calc_center_median()
            if (middle - above).dot(above_normal) > 0.0 and (middle - below).dot(below_normal) < 0.0:
                gone.append(f)
    bmesh.ops.delete(bm, geom=gone, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    print("side %d: quilted band between rings %.1f and %.1f cut out, %d faces" % (side, *CUT_BAND[side], len(gone)))


def fit_sleeves(state, readings):
    """Every sleeve point moved onto the body's arm."""
    bm, world, part = state["bm"], state["world"], state["part"]
    inverse = world.inverted()
    for side, reading in readings.items():
        code = 1 if side > 0 else 2
        verts = [v for v in bm.verts if v[part] == code]
        pts = mirrored(side, np.array([world @ v.co for v in verts]))
        still = smoothstep(distance_to(pts, mirrored(side, state["joined"][side])) / JOIN_FADE)
        fitted = mirrored(side, reading.fit_points(pts, reading.ring_of(pts), still))
        for v, p in zip(verts, fitted):
            v.co = inverse @ Vector(p)
        print("side %d: %d points fitted, moved up to %.3f m" % (side, len(verts), np.abs(mirrored(side, fitted) - pts).max()))


def show(state):
    state["bm"].to_mesh(state["obj"].data)
    state["obj"].data.update()


def distance_to(pts, targets):
    """How far each point is from the nearest of targets."""
    out = np.empty(len(pts))
    for i in range(0, len(pts), 2000):
        out[i:i + 2000] = np.linalg.norm(pts[i:i + 2000, None, :] - targets[None, :, :], axis=2).min(1)
    return out


def relax_side(state, side, passes=12):
    """The coat's side under one arm smoothed: where a sleeve lay pressed into it Meshy's skin is dented and creased, which shows once the
    sleeve hangs clear of it. Only the coat's own points near its wall move; its open edges keep their place."""
    bm, world, part = state["bm"], state["world"], state["part"]
    verts = [v for v in bm.verts if v[part] == 0 and not v.is_boundary]
    pts = mirrored(side, np.array([world @ v.co for v in verts]))
    wall = wall_x(side, pts[:, 2], pts[:, 1])
    near = (pts[:, 0] > wall - 0.04) & (pts[:, 0] < wall + 0.02) & (pts[:, 2] > 0.9) & (pts[:, 2] < Z_ARMPIT) & (pts[:, 1] > CUT_FRONT) & (pts[:, 1] < CUT_BACK)
    chosen = [v for v, yes in zip(verts, near) if yes]
    for _ in range(passes):
        bmesh.ops.smooth_vert(bm, verts=chosen, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    print("side %d: %d points of the coat's side smoothed" % (side, len(chosen)))


LINING_UV = (0.95, 0.9)    # the atlas' patch of plain dark cloth, for what is only seen looking up a sleeve
KNIT_ROWS = (0.962, 0.998)   # the atlas' rows of the ribbed cuff's top and of its lower edge
KNIT_REPEATS = 4           # times the atlas' piece of ribbing goes round the cuff (turned about at each, so it has no seam)
KNIT_INSET = 0.002         # the ribbing is read this far in from its piece's two ends: at the very end the atlas' next texel bleeds in as a line
RIM_IN, RIM_UP = 0.005, 0.012


def ring_coordinates(reading, pts, centre):
    """Mirrored-frame points about an arm: how far along the sleeve below centre, how far out from its line, the angle round it, and the
    two directions that angle is measured in."""
    axis = reading.axis
    out = np.array([1.0, 0.0, 0.0]) - axis * axis[0]
    out /= np.linalg.norm(out)
    side_way = np.cross(axis, out)
    rel = pts - centre
    return rel @ axis, np.hypot(rel @ out, rel @ side_way), np.arctan2(rel @ side_way, rel @ out), (out, side_way)


def join_band(state, reading):
    """The two edges the cut band left stitched together, in the fitted sleeve: the edge below takes the outline of the edge above, a
    seam's groove lower, the sleeve under it eases into that outline over 4 cm, and a strip of plain cloth closes the groove."""
    bm, world, part, splice, uv = state["bm"], state["world"], state["part"], state["splice"], state["uv"]
    side = reading.side
    if not CUT_BAND[side]:
        return
    code = 1 if side > 0 else 2
    inverse = world.inverted()

    def edge_of(mark):
        return [e for e in bm.edges if e.is_boundary and all(v[splice] == mark and v[part] == code for v in e.verts)]

    def mirrored_of(verts):
        return mirrored(side, np.array([world @ v.co for v in verts]))

    upper_verts = list({v for e in edge_of(1) for v in e.verts})
    lower_verts = list({v for e in edge_of(2) for v in e.verts})
    centre = mirrored_of(upper_verts).mean(0)
    _, far_up, turn_up, _ = ring_coordinates(reading, mirrored_of(upper_verts), centre)
    _, far_low, turn_low, _ = ring_coordinates(reading, mirrored_of(lower_verts), centre)
    bins = np.linspace(-math.pi, math.pi, 73)

    def outline(far, turn):
        which = np.clip(np.digitize(turn, bins) - 1, 0, 71)
        mean = np.array([far[which == i].mean() if (which == i).any() else np.nan for i in range(72)])
        idx = np.arange(72)
        mean[np.isnan(mean)] = np.interp(idx[np.isnan(mean)], idx[~np.isnan(mean)], mean[~np.isnan(mean)], period=72)
        return lambda t: np.interp(t, (bins[:-1] + bins[1:]) * 0.5, mean, period=2.0 * math.pi)

    above, below = outline(far_up, turn_up), outline(far_low, turn_low)
    queue = list(lower_verts)      # then the sleeve below the cut, as far as it is joined on
    seen = set(queue)
    for v in queue:
        for e in v.link_edges:
            n = e.other_vert(v)
            if n not in seen and n[part] == code:
                seen.add(n)
                queue.append(n)
    down, far, turn, (out, side_way) = ring_coordinates(reading, mirrored_of(queue), centre)
    edge_down = float(np.median(down[:len(lower_verts)]))
    ease = smoothstep(1.0 - np.clip(down - edge_down, 0.0, None) / 0.04)
    ease[:len(lower_verts)] = 1.0
    new_far = far * (1.0 + (above(turn) / below(turn) - 1.0) * ease)
    moved = centre + np.outer(down, reading.axis) + new_far[:, None] * (np.cos(turn)[:, None] * out + np.sin(turn)[:, None] * side_way)
    moved[:len(lower_verts)] += reading.axis * SEAM_GAP
    for v, p in zip(queue, mirrored(side, moved)):
        v.co = inverse @ Vector(p)
    strip = bmesh.ops.bridge_loops(bm, edges=edge_of(1) + edge_of(2))["faces"]
    for f in strip:
        f.smooth = True
        for corner, at in zip(f.loops, PLAIN_UV + PLAIN_UV[:1]):
            corner[uv].uv = at
    print("side %d: band's seams joined: %d and %d edge points, %d points below, strip of %d faces" % (
        side, len(upper_verts), len(lower_verts), len(queue), len(strip)))


def knit_cuff(state, reading):
    """The ribbed cuff (owner, 2026-11-20: "recreate the meshy cuff", a straight band with "straight lines around", wide enough to go
    round the thumb's root): a plain straight band of the outline reading.set_band worked out, from up inside the hem's rolled lip down to
    KNIT_BOTTOM, its lower edge turned in and up. The ribbing is its texture (the atlas' knit rows), not its shape. It is added to the coat
    already cut down to the game's detail: it is as plain as it can be already, and cutting down would only crumple it."""
    bm, world, uv, part = state["bm"], state["world"], state["uv"], state["part"]
    side = reading.side
    inverse = world.inverted()
    axis, around, band = reading.axis, reading.band_around, reading.band[:, None]
    bottom = reading.arm_at(KNIT_BOTTOM)
    inside_hem = reading.arm_frame(np.array([reading.hem_place()[1] - KNIT_UP / RING_STEP]))[0]
    top = bottom + axis * ((inside_hem - bottom) @ axis)     # straight above the lower edge
    rings = [top + around * band, bottom + around * band, bottom + around * (band - RIM_IN),
             bottom + around * (band - RIM_IN - 0.001) - axis * RIM_UP]
    verts = [[bm.verts.new(inverse @ Vector(p)) for p in mirrored(side, ring)] for ring in rings]
    for ring in verts:
        for v in ring:
            v[part] = 1 if side > 0 else 2
    faces = 0
    for r in range(len(rings) - 1):
        for j in range(KNIT_SEGMENTS):
            n = (j + 1) % KNIT_SEGMENTS
            corners = [(r, j), (r + 1, j), (r + 1, n), (r, n)]      # facing out of the band, on the left arm
            if side < 0:
                corners.reverse()
            face = bm.faces.new([verts[row][col] for row, col in corners])
            face.smooth = True
            faces += 1
            for corner, (row, col) in zip(face.loops, corners):
                if r >= 1:
                    corner[uv].uv = LINING_UV
                    continue
                ribs = (j + 1 if col == n else j) * KNIT_REPEATS / float(KNIT_SEGMENTS)
                corner[uv].uv = (ATLAS_OLD + KNIT_INSET + (1.0 - ATLAS_OLD - 2.0 * KNIT_INSET) * (1.0 - abs(ribs % 2.0 - 1.0)), KNIT_ROWS[row])
    for j in range(KNIT_SEGMENTS):     # the band's lower edge is a corner, not rounded over
        bm.edges.get((verts[1][j], verts[1][(j + 1) % KNIT_SEGMENTS])).smooth = False
    print("side %d: ribbed cuff of %d faces, a straight band from %.3f m down to %.3f m; %.3f m to the front, %.3f back, %.3f out" % (
        side, faces, top[2], bottom[2], band[3 * KNIT_SEGMENTS // 4, 0], band[KNIT_SEGMENTS // 4, 0], band[0, 0]))


def close_under_hem(state, reading):
    """The gap between the hem's rolled lip and the ribbed cuff closed (owner, 2026-11-20: "i can see underneath in between the cuff and
    the end of the jacket sleeve"): a ring of the coat's cloth from the lip's edge in to the cuff, all the way round, so the hem ends in
    a rounded underside and nothing shows up the sleeve."""
    bm, world, uv, part = state["bm"], state["world"], state["uv"], state["part"]
    side = reading.side
    code = 1 if side > 0 else 2
    inverse = world.inverted()
    lip = reading.hem_place()[1]
    centre = reading.arm_frame(np.array([lip]))[0]
    axis = reading.axis

    def mirrored_point(v):
        p = np.array(world @ v.co)
        p[0] *= side
        return p

    def at_lip(v):
        rel = mirrored_point(v) - centre
        return abs(rel @ axis) < 0.03 and np.linalg.norm(rel - axis * (rel @ axis)) < 0.2

    edge = [e for e in bm.edges if e.is_boundary and all(v[part] == code and at_lip(v) for v in e.verts)]
    by_vert = {}
    for e in edge:
        for v in e.verts:
            by_vert.setdefault(v, []).append(e)
    loops, seen = [], set()
    for e in edge:
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
        loops.append(loop)
    def girth(loop):
        return np.mean([np.linalg.norm((mirrored_point(v) - centre) - axis * ((mirrored_point(v) - centre) @ axis)) for e in loop for v in e.verts])

    hem = max(loops, key=len)       # the longest open edge there is the lip; any other is a slit beside it
    down = float(np.mean([(mirrored_point(v) - centre) @ axis for e in hem for v in e.verts]))
    ring = [bm.verts.new(inverse @ Vector(p)) for p in mirrored(side, centre + axis * down + reading.band_around * (reading.band[:, None] + 0.0005))]
    for v in ring:
        v[part] = code
    ring_edges = [bm.edges.new((ring[j], ring[(j + 1) % len(ring)])) for j in range(len(ring))]
    faces = bmesh.ops.bridge_loops(bm, edges=hem + ring_edges)["faces"]
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    facing = sum((f.normal * f.calc_area() for f in faces), Vector())
    if facing.dot(Vector(mirrored(side, axis[None, :])[0])) < 0.0:
        bmesh.ops.reverse_faces(bm, faces=faces)
    for f in faces:
        f.smooth = True
        for corner in f.loops:
            corner[uv].uv = LINING_UV      # in the hem's own shadow
    print("side %d: underside of the hem closed with %d faces (lip of %d edges, %.3f m round the arm on average)" % (
        side, len(faces), len(hem), girth(hem)))


def ease_fold(state, centre, radius, passes=20):
    """A fold of the coat's own skin smoothed out round a world point: in full at it, not at all radius away. (The pinch above the left
    pocket, which is what read as a dent once the panel hung straight.)"""
    bm, world, part = state["bm"], state["world"], state["part"]
    inverse = world.inverted()
    verts = [v for v in bm.verts if v[part] == 0 and not v.is_boundary and (world @ v.co - Vector(centre)).length < max(radius)]
    before = np.array([world @ v.co for v in verts])
    reach = np.linalg.norm((before - np.array(centre)) / np.array(radius), axis=1)
    keep = [v for v, r in zip(verts, reach) if r < 1.0]
    for _ in range(passes):
        bmesh.ops.smooth_vert(bm, verts=keep, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    after = np.array([world @ v.co for v in verts])
    weight = smoothstep(1.0 - reach)[:, None]
    for v, p in zip(verts, before + (after - before) * weight):
        v.co = inverse @ Vector(p)
    print("fold eased: %d points" % len(keep))


PLAIN_UV = ((0.84, 0.72), (0.86, 0.72), (0.85, 0.74))   # a spot of the atlas' plain cloth


def heal(state):
    """Every hole of the finished coat but its own openings (its front, hem and neck in one, and the two cuffs) closed with plain cloth:
    the small ones the cutting left, and Meshy's own in the hood where the fur's strands were rooted, under the fur. A sleeve's holes and
    the coat's are closed apart: a face from one to the other would tie the arm to the coat's side."""
    bm, world, uv, part = state["bm"], state["world"], state["uv"], state["part"]
    by_vert = {}
    open_edges = [e for e in bm.edges if e.is_boundary]

    def of_sleeve(e):
        return all(v[part] for v in e.link_faces[0].verts)

    for e in open_edges:
        for v in e.verts:
            by_vert.setdefault((v, of_sleeve(e)), []).append(e)
    loops, seen = [], set()
    for e in open_edges:
        if e in seen:
            continue
        loop = [e]
        seen.add(e)
        sleeve = of_sleeve(e)
        for x in loop:
            for v in x.verts:
                for y in by_vert[(v, sleeve)]:
                    if y not in seen:
                        seen.add(y)
                        loop.append(y)
        loops.append(loop)

    def length(loop):
        return sum((world @ e.verts[0].co - world @ e.verts[1].co).length for e in loop)

    def is_cuff(loop):
        z = [(world @ e.verts[0].co).z for e in loop]
        x = [abs((world @ e.verts[0].co).x) for e in loop]
        return max(z) < 1.2 and min(z) > 1.0 and min(x) > 0.42

    loops.sort(key=length, reverse=True)
    filled = []
    for loop in loops[1:]:
        if not is_cuff(loop) and plain_loops(loop):
            filled += bmesh.ops.holes_fill(bm, edges=loop, sides=0)["faces"]
    filled = bmesh.ops.triangulate(bm, faces=filled)["faces"]
    for f in filled:
        f.smooth = True
        for corner, at in zip(f.loops, PLAIN_UV):
            corner[uv].uv = at
    print("healed: %d holes closed with %d faces; left open: %d edges" % (
        len(loops) - 1, len(filled), sum(1 for e in bm.edges if e.is_boundary)))


SLIT_LENGTH = 0.35         # an open edge shorter than this all round, away from the cuffs, is a slit the cutting down left
SLIT_WIDTH = 0.004         # its two sides lie this near each other


def close_slits(bm, world):
    """The slits left in the coat cut down to the game's detail sewn shut: where a cut began or two skins touched, the two sides of an
    edge can end up apart by a hair, which opens into a hole when an arm lifts. A short open edge's points are joined where they lie on
    each other, and what is then still open of it is filled."""
    def loops():
        by_vert = {}
        edges = [e for e in bm.edges if e.is_boundary]
        for e in edges:
            for v in e.verts:
                by_vert.setdefault(v, []).append(e)
        found, seen = [], set()
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
            found.append(loop)
        return found

    def short(loop):
        z = [(world @ v.co).z for e in loop for v in e.verts]
        x = [abs((world @ v.co).x) for e in loop for v in e.verts]
        at_cuff = max(z) < 1.2 and min(z) > 0.95 and min(x) > 0.42
        return not at_cuff and sum((world @ e.verts[0].co - world @ e.verts[1].co).length for e in loop) < SLIT_LENGTH

    scale = world.to_scale()[0]
    slits = [loop for loop in loops() if short(loop)]
    for loop in slits:
        bmesh.ops.remove_doubles(bm, verts=list({v for e in loop for v in e.verts if v.is_valid}), dist=SLIT_WIDTH / scale)
    left = [loop for loop in loops() if short(loop)]
    filled = []
    for loop in left:      # a tangled one ring by ring: filled whole it gives flaps over the skin, left open it is a hole in the coat
        for ring in [loop] if plain_loops(loop) else simple_loops(loop):
            filled += bmesh.ops.holes_fill(bm, edges=ring, sides=0)["faces"]
    bmesh.ops.triangulate(bm, faces=filled)
    print("slits: %d sewn shut, %d of them then filled with %d faces; %d still open" % (
        len(slits), len(left), len(filled), sum(1 for loop in loops() if short(loop))))


SPECK = 0.06               # an opening this short all round is a pinhole: its points are drawn together
SLIVER = 0.10              # one up to this long is a sliver, and its points are joined to the ones lying this near them
SLIVER_WIDTH = 0.008
SPECK_PASSES = 4


def open_groups(bm):
    """The coat's open edges, sorted into the groups that hang together."""
    by_vert = {}
    edges = [e for e in bm.edges if e.is_boundary]
    for e in edges:
        for v in e.verts:
            by_vert.setdefault(v, []).append(e)
    groups, seen = [], set()
    for e in edges:
        if e in seen:
            continue
        group = [e]
        seen.add(e)
        for x in group:
            for v in x.verts:
                for y in by_vert[v]:
                    if y not in seen:
                        seen.add(y)
                        group.append(y)
        groups.append(group)
    return groups


def close_specks(bm, world):
    """The small holes still open in the cut-down coat closed (owner, 2026-11-20: "fix any holes"): what is left where open edges tangle, too knotted to fill as a ring. A pinhole, a centimetre across, has
    its points drawn together in its middle, and the cloth round it closes without a face added. A longer sliver has a flap of cloth
    lying along it: its points are joined to the ones a hair away, and what is then still open between two open edges and a point both
    their ends reach is filled, each face turned as the cloth beside it is (the game does not draw a face from behind)."""
    def length(group):
        return sum((world @ e.verts[0].co - world @ e.verts[1].co).length for e in group)

    closed = 0
    for _ in range(SPECK_PASSES):
        changed = False
        for group in open_groups(bm):
            if not all(e.is_valid for e in group):
                continue
            verts = list({v for e in group for v in e.verts})
            if length(group) < SPECK:
                bmesh.ops.pointmerge(bm, verts=verts, merge_co=sum((v.co for v in verts), Vector()) / len(verts))
                closed += 1
                changed = True
            elif length(group) < SLIVER:
                around = list({e.other_vert(v) for v in verts for e in v.link_edges} | set(verts))
                before = len(bm.verts)
                bmesh.ops.remove_doubles(bm, verts=around, dist=SLIVER_WIDTH / world.to_scale()[0])
                changed |= len(bm.verts) < before
        fins = [f for f in bm.faces       # a lone face standing off an edge two others already share is no hole's edge: a loose flap
                if sum(e.is_boundary for e in f.edges) == 2 and any(len(e.link_faces) > 2 for e in f.edges)]
        bmesh.ops.delete(bm, geom=fins, context="FACES")
        if not changed and not fins:
            break
    filled = 0
    for group in open_groups(bm):
        if length(group) >= SLIVER:
            continue
        for e in group:
            if not e.is_boundary:
                continue
            ends = set(e.verts)
            reach = [v for v in set(x.other_vert(e.verts[0]) for x in e.verts[0].link_edges) & set(x.other_vert(e.verts[1]) for x in e.verts[1].link_edges)
                     if v not in ends and bm.faces.get((e.verts[0], e.verts[1], v)) is None]
            if not reach:
                continue
            middle = (e.verts[0].co + e.verts[1].co) * 0.5
            third = min(reach, key=lambda v: (v.co - middle).length)
            first = e.link_loops[0].vert
            bm.faces.new((e.other_vert(first), first, third)).smooth = True
            filled += 1
    print("small holes: %d pinholes drawn shut, %d faces laid over slivers; still open: %d" % (
        closed, filled, sum(1 for g in open_groups(bm) if length(g) < SLIVER)))


BODY_CLEAR = 0.008         # the cloth lies at least this far off the skin over the shoulders
SHOULDERS_FROM = 1.75      # from this height up: lower down the coat hangs free, and the cuffs sit on the wrists as fitted
BODY_REACH = 0.05
LIFT_PASSES = 4
LEGS_BELOW = 0.95          # round the legs: below the hip joints
LEGS_WITHIN = 0.33         # and no further out than the thighs' outer side (the hands hang beside them, further out)
LEG_CLEAR = 0.02           # the lining this far off a leg's skin: a knee moves inside the coat
LEG_REACH = 0.12


def lift_over_body(bm, world, skin):
    """The cloth lifted off the shoulders where the skin came through it (owner, 2026-11-20: "looks like theres two triangular holes on
    his left shoulder", "did they holes ever get fixed im still seeing it"): no holes, the body's shoulder, 4 mm out through the cloth
    since the coat was turned back on the body. Each point of the coat nearer the skin than BODY_CLEAR, or inside it, goes out to that
    far above it. And round the legs, the lining folded into the left knee (4 cm in, the outer cloth well clear; a sprinting knee caught
    it) goes out to LEG_CLEAR off the skin. skin is the body's surface (a BVHTree) as the coat meets it."""
    inverse = world.inverted()
    turn = world.to_3x3()

    def short_of(p, facing):
        """How far a point must go out, and which way. Over the shoulders only the outer cloth moves: the lining there looks in at the
        body, and lifted as far as the cloth over it, it would come out through that cloth. Round the legs everything moves: the outer
        cloth is far off them."""
        if p.z > SHOULDERS_FROM:
            near, normal, _, _ = skin.find_nearest(p, BODY_REACH)
            clear = BODY_CLEAR
            if near is None or (turn @ facing).dot(normal) <= 0.0:
                return None
        elif p.z < LEGS_BELOW:
            near, normal, _, _ = skin.find_nearest(p, LEG_REACH)
            clear = LEG_CLEAR
            if near is None or near.z > LEGS_BELOW or abs(near.x) > LEGS_WITHIN:
                return None
        else:
            return None
        if (p - near).dot(normal) >= clear:
            return None
        return normal * (clear - (p - near).dot(normal))

    lifted = 0
    for _ in range(LIFT_PASSES):      # the points, then the faces between them: a flat face cuts the round shoulder under it
        moved = 0
        bm.normal_update()
        for v in bm.verts:
            p = world @ v.co
            out = short_of(p, v.normal)
            if out is not None:
                v.co = inverse @ (p + out)
                moved += 1
        for f in bm.faces:
            p = world @ f.calc_center_median()
            out = short_of(p, f.normal)
            if out is not None:
                for v in f.verts:
                    v.co = inverse @ (world @ v.co + out)
                moved += 1
        if not moved:
            break
        lifted += moved
    print("shoulders and legs: %d points and faces lifted off the skin" % lifted)


ODD_COLOUR = 0.10          # a face this much lighter than the faces round it shows as a stray pale triangle
SQUEEZED = 0.3             # a face this small on the map for its size on the coat has no cloth of its own there
STRETCHED = 3.5            # and one this big on the map for its size reaches across two pieces of it


def mend_map(bm, world, uv, atlas):
    """Faces with no real place on the map take their neighbour's cloth (owner, 2026-11-20: "what are all these little triangles im
    seeing?"). Three kinds: faces Meshy's own map squeezes to a single texel (a crumb of its map at the atlas' edge: a speck on the full
    coat, but cutting down grows such a face to a triangle centimetres across, all one pale colour); faces cutting down tore between two
    pieces of the map; and the ones that close holes (those two were given one flat patch of plain cloth). Such a face, and any face that
    still comes out much lighter than those round it, is laid on the map along the edge it shares with a sound neighbour, a little inside
    that neighbour, so it wears the same cloth, shade and all. Faces of the new patches' own charts are left alone."""
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])
    h, w = atlas.shape[:2]
    plain = np.sort(np.array(PLAIN_UV), 0)

    def colour(face):
        at = np.array([corner[uv].uv[:] for corner in face.loops])
        points = [at.mean(0)] + [at.mean(0) * 0.4 + c * 0.6 for c in at]
        return np.mean([atlas[min(int(q[1] * h), h - 1), min(int(q[0] * w), w - 1), :3] for q in points], 0)

    def is_plain(face):
        at = np.array([corner[uv].uv[:] for corner in face.loops])
        return len(at) == 3 and np.abs(np.sort(at, 0) - plain).max() < 1e-4

    def on_old_map(face):
        return max(corner[uv].uv.x for corner in face.loops) < ATLAS_OLD + 1e-4

    def on_map_per_metre(face):
        """How much of the map a metre of the face takes, the way it takes least and the way it takes most: a face squeezed to a line
        on the map smears one row of texels across itself."""
        a, b, c = [world @ v.co for v in face.verts]
        p, q = b - a, c - a
        x = p.normalized()
        y = p.cross(q).cross(p).normalized()
        on_coat = np.array([[p.length, q.dot(x)], [0.0, q.dot(y)]])
        at = [np.array(corner[uv].uv[:]) / np.array([ATLAS_OLD, 1.0]) for corner in face.loops]
        on_map = np.array([at[1] - at[0], at[2] - at[0]]).T
        if abs(np.linalg.det(on_coat)) < 1e-12:
            return 0.0, 0.0
        stretch = np.linalg.svd(on_map @ np.linalg.inv(on_coat), compute_uv=False)
        return float(stretch.min()), float(stretch.max())

    shade = {f: colour(f) for f in bm.faces}
    size = {f: on_map_per_metre(f) for f in bm.faces if on_old_map(f) and len(f.verts) == 3}
    usual = float(np.median([math.sqrt(least * most) for least, most in size.values()]))
    def torn(face):      # between two pieces of the map: far bigger on it than on the coat, or half on the old map and half on the strip
        us = [corner[uv].uv.x for corner in face.loops]
        return size.get(face, (usual, usual))[1] > STRETCHED * usual or (min(us) < ATLAS_OLD - 1e-4 and max(us) > ATLAS_OLD + 1e-4)

    bad = {f for f in bm.faces if is_plain(f) or torn(f) or size.get(f, (usual, usual))[0] < SQUEEZED * usual}
    squeezed = len(bad)
    for f in bm.faces:
        if f in bad or not on_old_map(f):
            continue
        around = [shade[o] for e in f.edges for o in e.link_faces if o is not f and o not in bad and on_old_map(o)]
        if around and (shade[f] - np.mean(around, 0)).min() > ODD_COLOUR:
            bad.add(f)
    found = len(bad)
    for _ in range(12):
        mended = []
        for f in bad:
            best = None
            for e in f.edges:
                for o in e.link_faces:
                    if o is not f and o not in bad and on_old_map(o) and len(o.verts) == 3:
                        if best is None or e.calc_length() > best[0].calc_length():
                            best = (e, o)
            if best is None:
                continue
            e, o = best
            theirs = {corner.vert: corner[uv].uv.copy() for corner in o.loops}
            start, along = world @ e.verts[0].co, world @ e.verts[1].co - world @ e.verts[0].co

            def beside(v):      # how far along the shared edge a point is (0 to 1), and how far from it
                to = world @ v.co - start
                t = to.dot(along) / along.length_squared
                return t, (to - along * t).length

            far, far_off = beside(next(v for v in o.verts if v not in e.verts))
            on_map = theirs[e.verts[1]] - theirs[e.verts[0]]
            off_map = (theirs[next(v for v in o.verts if v not in e.verts)] - theirs[e.verts[0]] - on_map * far) / max(far_off, 1e-9)
            for corner in f.loops:      # the neighbour's cloth as in a mirror at the shared edge, at the neighbour's own scale
                if corner.vert in e.verts:
                    corner[uv].uv = theirs[corner.vert]
                else:
                    t, off = beside(corner.vert)
                    corner[uv].uv = theirs[e.verts[0]] + on_map * min(max(t, 0.1), 0.9) + off_map * min(off, far_off * 0.9)
            mended.append(f)
        if not mended:
            break
        bad.difference_update(mended)
    print("map mended: %d faces took a neighbour's cloth (%d plain or squeezed to nothing on the map, %d only lighter than those round them), %d could not" % (
        found - len(bad), squeezed, found - squeezed, len(bad)))


def settle_faces(bm):
    """The cut-down coat's faces put in order: a face lying exactly on another goes, and every face is turned to look the same way as
    the skin it belongs to (Blender's own reckoning, outward from the coat). A face turned the other way shows as a stray pale triangle,
    lit from its wrong side, and the game does not draw a face seen from behind at all."""
    seen, doubles = {}, []
    for f in bm.faces:
        key = tuple(sorted(tuple(round(c, 4) for c in v.co) for v in f.verts))
        if key in seen:
            doubles.append(f)
        seen[key] = f
    bmesh.ops.delete(bm, geom=doubles, context="FACES")
    bm.normal_update()
    before = {f: f.normal.copy() for f in bm.faces}
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.normal_update()
    turned = sum(1 for f, n in before.items() if f.normal.dot(n) < 0.0)
    if turned > len(before) // 2:       # it reckoned the whole coat inside out: the few it left were the wrong ones
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
        turned = len(before) - turned
    print("faces settled: %d doubles taken out, %d turned round" % (len(doubles), turned))
