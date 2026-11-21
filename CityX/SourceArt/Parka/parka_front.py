# The left front panel made to hang like the right one (exec after parka_lib.py and parka_sleeve.py). Meshy drew the wearer's left panel with its
# edge flap standing straight out forward like a fin, its edge drifting out to the side towards the hem, and dented above the pocket; the right
# panel's edge lies flat and hangs straight. Both panels are traced level by level from above (the right one mirrored): the left one's trace
# runs along its shell and on out along the fin, and every point of the left panel is carried from its place by that trace to the same place
# by the right panel's, keeping how far it stands off the trace. So the fin folds flat into the panel's line, the edge comes in under the
# right one's mirror image, the dent fills, and the pockets, zip and stitching keep their size.
FRONT_FROM = -0.15         # the panels are traced from this y forward (the side seam's line); behind it nothing moves
LEVELS = np.arange(0.46, 1.80, 0.04)
TRACE_POINTS = 80
MIDDLE = np.array([0.0, -0.10])     # the traces' angles are measured about this point (x, y)
BLEND_IN = 0.10            # along the first of the trace the panel eases from its own shape into the right one's
TOP_FADE = (1.50, 1.72)    # above this the collar and hood begin: the change fades out
FOOT_CLEAR = 0.03          # the shell's points this near the fin's line are the fin's own face
CORNER_PASSES = 14         # the fin's foot is a sharp corner in the trace: rounded, so the points inside the corner are not torn between its two sides
LEVEL_PASSES = 4           # the traces are smoothed up and down the coat: a pocket flap or a fold at one level does not kink the panel
NEAR_TRACE = 0.16          # a point further than this from the left trace is not the panel's (the back's inside, seen through the open front)


def level_points(pts, z, thick=0.004):
    return pts[np.abs(pts[:, 2] - z) < thick][:, :2]


def angle_of_point(p):
    rel = p - MIDDLE
    return math.atan2(-rel[1], rel[0])     # 0 = out to the side, a quarter turn = straight ahead


def shell_trace(points, to_angle):
    """The outermost point at each angle about MIDDLE, from the side seam forward to to_angle: the panel's shell (facings and pocket bags lie
    inside it)."""
    rel = points - MIDDLE
    angle = np.arctan2(-rel[:, 1], rel[:, 0])
    radius = np.linalg.norm(rel, axis=1)
    start = math.atan2(-(FRONT_FROM - MIDDLE[1]), 0.4)
    trace = []
    for a in np.arange(start, to_angle, math.radians(2.0)):
        inside = np.abs(angle - a) < math.radians(1.5)
        if inside.sum() > 2:
            trace.append(points[np.argmax(np.where(inside, radius, -1.0))])
    return np.array(trace)


def smooth_line(line, passes):
    line = line.copy()
    for _ in range(passes):
        line[1:-1] = 0.25 * line[:-2] + 0.5 * line[1:-1] + 0.25 * line[2:]
    return line


def steady(marks):
    """Marks found level by level (one row each) with the odd one out replaced by its neighbours' middle, then smoothed."""
    out = marks.copy()
    for i in range(len(marks)):
        out[i] = np.median(marks[max(0, i - 2):i + 3], axis=0)
    return smooth_line(out, 2)


def resample(line, count=TRACE_POINTS):
    at = np.concatenate([[0.0], np.cumsum(np.linalg.norm(np.diff(line, axis=0), axis=1))])
    want = np.linspace(0.0, at[-1], count)
    return np.stack([np.interp(want, at, line[:, 0]), np.interp(want, at, line[:, 1])], 1)


def length_of(line):
    return float(np.linalg.norm(np.diff(line, axis=0), axis=1).sum())


def fin_marks(points):
    """The fin at one level of the left panel: its foot (where it stands out of the shell) and its tip."""
    front = points[(points[:, 0] > 0.08) & (points[:, 1] < -0.2)]
    tip = front[np.argmin(front[:, 1])]
    near = front[np.linalg.norm(front - tip, axis=1) < 0.06]
    along = np.linalg.svd(near - near.mean(0), full_matrices=False)[2][0]
    if along[1] > 0.0:
        along = -along        # towards the tip (forward)
    across = np.array([-along[1], along[0]])
    back = (tip - points) @ along
    off = (points - tip) @ across
    for t in np.arange(0.01, 0.25, 0.005):
        band = (np.abs(back - t) < 0.006) & (np.abs(off) < 0.12)
        if band.sum() > 3 and np.abs(off[band]).max() > 0.035:
            return tip - along * t, tip
    return tip - along * 0.1, tip


def edge_mark(points):
    """The right panel's front edge at one level (the panel mirrored to the left): its point nearest the coat's middle line."""
    front = points[(points[:, 1] < -0.25) & (points[:, 0] > 0.0)]
    return front[np.argmin(front[:, 0])]


def shell_and_fin(points, foot, tip):
    """The left panel's trace at one level: its shell from the side seam, on in a straight line to where that line meets the fin's, then out
    along the fin to its tip. (The shell's own last points before the fin are the fin's face, and would put a kink in the corner.)"""
    fin = (tip - foot) / np.linalg.norm(tip - foot)
    shell = smooth_line(shell_trace(points, angle_of_point(foot) - math.radians(2.0)), 3)
    off_fin = np.abs((shell - tip) @ np.array([-fin[1], fin[0]]))
    shell = shell[off_fin > FOOT_CLEAR]
    heading = shell[-1] - shell[-4]
    heading /= np.linalg.norm(heading)
    # shell[-1] + a * heading = tip - b * fin
    a, b = np.linalg.solve(np.stack([heading, fin], 1), tip - shell[-1])
    corner = shell[-1] + heading * a if 0.0 < a < 0.12 and 0.0 < b < 0.3 else foot
    return np.concatenate([shell, corner[None, :], tip[None, :]])


def panel_traces(coat_points):
    """Both panels' traces at every level, each TRACE_POINTS points evenly along its length and smoothed up and down the coat, and how much
    longer the right one is than the left at each level."""
    left = coat_points[(coat_points[:, 0] > 0.0) & (coat_points[:, 1] < FRONT_FROM + 0.02)]
    right = coat_points[(coat_points[:, 0] < 0.0) & (coat_points[:, 1] < FRONT_FROM + 0.02)].copy()
    right[:, 0] *= -1.0
    marks = [fin_marks(level_points(left, z)) for z in LEVELS]
    feet, tips = steady(np.array([m[0] for m in marks])), steady(np.array([m[1] for m in marks]))
    edges = steady(np.array([edge_mark(level_points(right, z)) for z in LEVELS]))
    lefts, rights = [], []
    for i, z in enumerate(LEVELS):
        lefts.append(resample(shell_and_fin(level_points(left, z), feet[i], tips[i])))
        shell = smooth_line(shell_trace(level_points(right, z), angle_of_point(edges[i]) - math.radians(1.0)), 3)
        rights.append(resample(np.concatenate([shell, edges[i][None, :]])))
    lefts, rights = np.array(lefts), np.array(rights)
    for table, corner in ((lefts, CORNER_PASSES), (rights, 2)):
        for i in range(len(table)):
            table[i] = np.median(table[max(0, i - 1):i + 2], axis=0)
        for _ in range(LEVEL_PASSES):
            table[1:-1] = 0.25 * table[:-2] + 0.5 * table[1:-1] + 0.25 * table[2:]
        for i in range(len(table)):
            table[i] = resample(smooth_line(table[i], corner))
    stretch = np.array([length_of(r) / length_of(l) for l, r in zip(lefts, rights)])
    return lefts, rights, np.clip(smooth_line(stretch[:, None], 4)[:, 0], 0.85, 1.15)


def along_trace(trace, pts):
    """Each point's place by a trace: how far along it its nearest point is, how far off it to the trace's left, and how far from it."""
    seg = np.diff(trace, axis=0)
    length = np.linalg.norm(seg, axis=1)
    start = np.concatenate([[0.0], np.cumsum(length)])
    unit = seg / length[:, None]
    rel = pts[:, None, :] - trace[None, :-1, :]
    t = np.clip(np.einsum("ijk,jk->ij", rel, unit), 0.0, length[None, :])
    gap = rel - t[:, :, None] * unit[None, :, :]
    j = np.argmin(np.linalg.norm(gap, axis=2), axis=1)
    rows = np.arange(len(pts))
    normal = np.stack([-unit[j, 1], unit[j, 0]], 1)
    return start[j] + t[rows, j], np.einsum("ij,ij->i", gap[rows, j], normal), np.linalg.norm(gap[rows, j], axis=1)


def on_trace(trace, along, off):
    """The point that far along a trace (on past its end in a straight line) and that far off it."""
    seg = np.diff(trace, axis=0)
    length = np.linalg.norm(seg, axis=1)
    start = np.concatenate([[0.0], np.cumsum(length)])
    j = np.clip(np.searchsorted(start, along, side="right") - 1, 0, len(seg) - 1)
    unit = seg[j] / length[j][:, None]
    normal = np.stack([-unit[:, 1], unit[:, 0]], 1)
    return trace[j] + unit * (along - start[j])[:, None] + normal * off[:, None]


def straighten(pts, lefts, rights, stretch):
    """World points of the coat moved as the left front panel is re-hung: those of the panel go from their place by the left trace to the
    same place by the right panel's trace (mirrored), easing in from the side seam and out towards the collar. A point between two levels
    takes both levels' answers in proportion. Returns the moved points."""
    out = pts.copy()
    step = LEVELS[1] - LEVELS[0]
    level = np.clip((pts[:, 2] - LEVELS[0]) / step, 0.0, len(LEVELS) - 1.001)
    panel = (pts[:, 0] > 0.0) & (pts[:, 1] < FRONT_FROM + 0.03) & (pts[:, 2] > LEVELS[0] - 0.12)
    for i in range(len(LEVELS) - 1):
        chosen = np.where(panel & (level.astype(int) == i))[0]
        if not len(chosen):
            continue
        p = pts[chosen, :2]
        share = (level[chosen] - i)[:, None]
        target = np.zeros_like(p)
        weight = np.zeros(len(chosen))
        for j, part in ((i, 1.0 - share), (i + 1, share)):
            along, off, gap = along_trace(lefts[j], p)
            target += on_trace(rights[j], along * stretch[j], off) * part
            near = 1.0 - smoothstep((gap - NEAR_TRACE * 0.6) / (NEAR_TRACE * 0.4))
            weight += smoothstep(along / BLEND_IN) * near * part[:, 0]
        weight *= 1.0 - smoothstep((pts[chosen, 2] - TOP_FADE[0]) / (TOP_FADE[1] - TOP_FADE[0]))
        out[chosen, :2] += (target - p) * weight[:, None]
    return out
