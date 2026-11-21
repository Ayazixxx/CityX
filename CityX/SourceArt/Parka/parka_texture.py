# The parka's one texture (exec after the other parka_*.py files): Meshy's colour map squeezed into the left of the atlas, and beside it the
# cloth of the new patches. A sleeve's inner patch is painted from the sleeve itself: each texel is the colour Meshy's sleeve has at that ring
# and angle, and where Meshy has no sleeve there (the fused side) the colour of the cloth as far the other side of the fused span's edge, so
# the quilting's seams run on round the arm.
CHART_STEP = 2             # the charts are painted at one texel in this many and spread: they are cloth seen only with an arm raised
LINING_DARKER = 0.45       # the cuff's turned-in rim, against the sleeve's cloth
KNIT_FROM = 0.96           # the atlas' rows from here up are the ribbed cuff's
RIBS = 34                  # ribs across the atlas' piece of ribbing: four pieces go round the cuff, a rib every 4.5 mm
RIB_GROOVE = 0.78          # how dark the groove between two ribs is, against a rib: fine and quiet, as Meshy's are
KNIT_DARKER = 0.92         # the ribbing against the coat's cloth


class Colours:
    """The colour Meshy's map gives any point of the full-detail coat's surface."""

    def __init__(self, obj, pixels):
        mesh = obj.data
        co = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
        mesh.vertices.foreach_get("co", co)
        corner = np.empty(len(mesh.loops), dtype=np.int32)
        mesh.loops.foreach_get("vertex_index", corner)
        uv = np.empty(len(mesh.loops) * 2, dtype=np.float32)
        mesh.uv_layers.active.data.foreach_get("uv", uv)
        self.corners = co.reshape(-1, 3)[corner].reshape(-1, 3, 3)
        self.uv = uv.reshape(-1, 3, 2)
        self.inverse = np.array(obj.matrix_world.inverted())
        self.pixels = pixels

    def at(self, face, point):
        """The colour at a world point of a face: the map read at the point's own place on it, between the face's corners."""
        p = self.inverse[:3, :3] @ point + self.inverse[:3, 3]
        a, b, c = self.corners[face]
        u, v, w = b - a, c - a, p - a
        uu, uv, vv, wu, wv = u @ u, u @ v, v @ v, w @ u, w @ v
        det = uu * vv - uv * uv
        s = (vv * wu - uv * wv) / det if det > 1e-20 else 1.0 / 3.0
        t = (uu * wv - uv * wu) / det if det > 1e-20 else 1.0 / 3.0
        at = self.uv[face][0] * (1.0 - s - t) + self.uv[face][1] * s + self.uv[face][2] * t
        h, width = self.pixels.shape[:2]
        return self.pixels[min(max(int(at[1] * h), 0), h - 1), min(max(int(at[0] * width), 0), width - 1), :3]


def wall_colour(side, caster, colours, origin, direction):
    """The colour of the sleeve's wall along a ray from inside it (mirrored frame), or None where there is none."""
    hit = first_wall(side, caster, origin, direction)
    if not hit:
        return None
    return colours.at(hit[2], np.array([hit[1][0] * side, hit[1][1], hit[1][2]]))


def sleeve_chart(reading, caster, colours, width, height):
    """The chart of one sleeve's inner patch, height rows (rings 0 to CHART_RINGS) by width columns (the half turn facing the body)."""
    side = reading.side
    chart = np.zeros((height, width, 3), dtype=np.float32)
    known = np.zeros((height, width), dtype=bool)
    columns = AROUND / 4.0 + (np.arange(width) + 0.5) / width * (AROUND / 2.0)
    for row in range(height):
        k = (row + 0.5) / height * CHART_RINGS
        i = min(int(k), len(reading.frames) - 2)
        f = k - i
        centre = reading.centres[i] * (1 - f) + reading.centres[i + 1] * f
        out = reading.outs[i] * (1 - f) + reading.outs[i + 1] * f

        def colour_at(column):
            t = 2.0 * math.pi * column / AROUND
            return wall_colour(side, caster, colours, centre, math.cos(t) * out + math.sin(t) * BACK)

        got = [colour_at(c) for c in columns]
        for j, c in enumerate(got):
            if c is not None:
                chart[row, j] = c
                known[row, j] = True
        missing = np.where(~known[row])[0]
        if len(missing) and len(missing) < width:
            lo, hi = missing.min(), missing.max()
            middle = (lo + hi) * 0.5
            per = (AROUND / 2.0) / width
            for j in missing:
                mirrored_column = columns[lo] - (j - lo + 1) * per if j <= middle else columns[hi] + (hi - j + 1) * per
                c = colour_at(mirrored_column)
                if c is not None:
                    chart[row, j] = c
                    known[row, j] = True
    return fill_unknown(chart, known)


def fill_unknown(chart, known):
    """Texels nothing was found for take their nearest painted neighbour's colour, row by row then column by column."""
    chart = chart.copy()
    known = known.copy()
    for _ in range(max(chart.shape)):
        if known.all():
            break
        for shift, axis in ((1, 1), (-1, 1), (1, 0), (-1, 0)):
            src_known = np.roll(known, shift, axis)
            src = np.roll(chart, shift, axis)
            take = ~known & src_known
            chart[take] = src[take]
            known |= take
    return chart


def spread(chart, width, height):
    ys = np.clip(((np.arange(height) + 0.5) / height * chart.shape[0]).astype(int), 0, chart.shape[0] - 1)
    xs = np.clip(((np.arange(width) + 0.5) / width * chart.shape[1]).astype(int), 0, chart.shape[1] - 1)
    return chart[ys][:, xs]


def paint_atlas(pixels, readings, caster, coat):
    """The atlas' texels (rows from the bottom, RGBA): the old map in its ATLAS_OLD of the width, the sleeves' inner patches, the side walls'
    plain cloth and the cuffs' dark lining in the strip."""
    h, w = pixels.shape[:2]
    old = int(w * ATLAS_OLD)
    xs = (np.arange(old) + 0.5) / old * w - 0.5
    x0 = np.clip(np.floor(xs).astype(int), 0, w - 1)
    x1 = np.clip(x0 + 1, 0, w - 1)
    fx = (xs - x0)[None, :, None]
    atlas = np.zeros((h, w, 4), dtype=np.float32)
    atlas[..., 3] = 1.0
    atlas[:, :old] = pixels[:, x0] * (1 - fx) + pixels[:, x1] * fx
    colours = Colours(coat, pixels)
    strip = w - old
    rows = int(h * 0.3)
    cloth = []
    for side, first_row in ((1, 0), (-1, rows)):
        chart = sleeve_chart(readings[side], caster, colours, strip // CHART_STEP, rows // CHART_STEP)
        atlas[first_row:first_row + rows, old:, :3] = spread(chart, strip, rows)
        cloth.append(np.median(chart.reshape(-1, 3), axis=0))
    cloth = np.mean(cloth, axis=0)
    grain = np.random.default_rng(7).normal(0.0, 0.004, (h - 2 * rows, strip, 1)).astype(np.float32)
    atlas[2 * rows:, old:, :3] = np.clip(cloth + grain, 0.0, 1.0)
    lining = slice(int(h * 0.85), int(h * 0.95)), slice(old + strip // 2, w)
    atlas[lining[0], lining[1], :3] = cloth * LINING_DARKER
    knit = int(h * KNIT_FROM)
    atlas[knit:, old:, :3] = knit_ribs(strip, h - knit, cloth)
    return atlas


def knit_ribs(width, height, cloth):
    """The ribbed cuff's cloth, drawn (owner, 2026-11-20: "reapply the ribbed texture it looks all pixelated or redraw it", "straight lines
    around"): straight ribs up and down, wide and light, with a narrow dark groove between each two, in the coat's own colour a little
    darker. RIBS of them across the piece, a whole number, so the piece can be turned about at its ends and go round the cuff unbroken."""
    across = (np.arange(width) + 0.5) / width
    rib = np.abs(np.sin(math.pi * RIBS * across)) ** 0.6
    shade = RIB_GROOVE + (1.0 - RIB_GROOVE) * rib
    grain = np.random.default_rng(11).normal(0.0, 0.006, (height, width, 1)).astype(np.float32)
    return np.clip(cloth * KNIT_DARKER * shade[None, :, None] + grain, 0.0, 1.0)


def box_blur(image, radius):
    """The mean of each texel's square of neighbours, radius texels each way."""
    out = image.astype(np.float32)
    for axis in (0, 1):
        total = np.cumsum(np.concatenate([np.zeros_like(np.take(out, [0], axis)), out], axis), axis, dtype=np.float64)
        n = out.shape[axis]
        hi = np.clip(np.arange(n) + radius + 1, 0, n)
        lo = np.clip(np.arange(n) - radius, 0, n)
        out = ((np.take(total, hi, axis) - np.take(total, lo, axis)) / (hi - lo).reshape([-1 if a == axis else 1 for a in range(out.ndim)])).astype(np.float32)
    return out
