# Meshy's sleeves made to fit this game's arm (exec after parka_lib.py and parka_rings.py). A sleeve is read ring by ring and closed where it
# was fused to the body: that reading is the guide by which the sleeve's own points are moved onto the arm and widened or drawn in to its
# girth, and the shape the missing inner side is given.
#
# Meshy's sleeves are 16 to 20 cm too long for this body. Their end is kept as Meshy drew it (the last quilted band, its rolled hem, the
# ribbed cuff below): the length comes out of the forearm instead. The left sleeve has one quilted band more than the right, of the same
# height as its others: that band is cut out seam to seam and the sleeve's end moved up to close the gap, and what is still too long is
# pressed together from the upper arm down. The right sleeve's bands are taller: it is only pressed together, which brings its seams level
# with the left's.
AROUND = 128               # angles a ring is read at
WALL_CLEAR = 0.004         # the sleeve stays this far off the body's side wall
WALL_SOFT = 0.008          # and nears it over this distance (no ridge where it starts to touch)
ARM_TOP = (0.408, 1.66)    # the body's arm: a point of its middle line (mirrored x, z) and how far it leans out per metre down
ARM_SLOPE = 0.221
STILL_RINGS = FAN_RINGS - 15   # the rings of the shoulder, which stay where Meshy has them: the fit begins below them
BACK = np.array([0.0, 1.0, 0.0])

# Meshy's quilting seams, read off the sleeves' texture: each lies on a tilted ring, SEAM_TILT rings lower at the outside and at the back
# than its middle. Per side: the band cut out (the rings of its two seams' middles), and the rolled hem (the seam above it, its lower lip).
SEAM_TILT = (2.0, 2.0)
CUT_BAND = {1: (81.55, 99.05), -1: None}
HEM = {1: (116.5, 121.5), -1: (107.5, 114.5)}
HEM_UNDER = 0.8            # the sleeve ends this many rings under its hem's lower lip, on the roll's underside
PRESS_FROM = 50.0          # the sleeve is pressed together from this ring down (below the armpit)
LEVEL_FROM, LEVEL_OVER = 45.0, 30.0   # the sleeve's tilted seams are levelled from this ring, in full this many rings further down
HEM_AT = 99.0              # so that the hem's lower lip comes to this ring: 1.05 m up, the ribbed cuff's 4 cm above its lower edge
BAND_RADIUS = 0.108        # how wide the sleeve is round its last band, on average (Meshy's left is 0.10, its right 0.12)
KNIT_RADIUS = 0.082        # the ribbed cuff: the wrist is 0.073
KNIT_DEEP = 0.0885         # and front to back
HAND_CLEAR = 0.007         # over the hand the cuff is this far off the skin (the thumb's root is 0.124 from the arm's middle at the cuff's lower edge)
KNIT_BOTTOM = 1.012        # the owner's red line: across the hand at the top of the thumb's root
KNIT_UP = 0.028            # how far up inside the hem the ribbed cuff starts
KNIT_SEGMENTS = 24         # points round the ribbed cuff
HEM_OVER = 0.002           # the hem's lip lies this far off the ribbed cuff
FLARE_RINGS = 9.0          # where the cuff is wider than the lip the sleeve eases out over it from this many rings above the hem's seam
SEAM_GAP = 0.004           # the groove left where the cut band's two seams are joined


def close_fused_side(frames, radius, valid):
    """The rings' radii with the side fused to the body filled in: each ring's open span (round the angle facing the body) is closed by a
    smooth arc as round as the sleeve's outside (fit_points then keeps it off the body's wall). Returns the radii and the span per ring (lo,
    hi as indices, -1 where the ring was whole)."""
    n, around = radius.shape
    half = around // 2
    radius = radius.copy()
    spans = np.full((n, 2), -1)
    for k in range(n):
        bad = np.where(~valid[k])[0]
        inner = bad[(bad > around // 4) & (bad < 3 * around // 4)]
        if len(inner) > 2:
            spans[k] = (max(inner.min() - 3, around // 4), min(inner.max() + 3, 3 * around // 4))
    # a ring's span follows its neighbours' (no jagged edge where a fold was hit)
    for column in (0, 1):
        s = spans[:, column].astype(float)
        for k in np.where(s >= 0)[0]:
            near = s[max(0, k - 3):k + 4]
            spans[k, column] = int(np.median(near[near >= 0]))
    mids = np.array([np.concatenate([radius[k, :around // 12], radius[k, -around // 12:]]).mean() for k in range(n)])
    mids = np.convolve(np.pad(mids, 4, mode="edge"), np.ones(9) / 9.0, mode="valid")
    idx = np.arange(around)
    for k in range(n):
        lo, hi = spans[k]
        ok = valid[k].copy()
        if lo >= 0:
            ok[lo:hi + 1] = True
        if not ok.all():    # stray misses elsewhere: from their neighbours
            radius[k, ~ok] = np.interp(idx[~ok], idx[ok], radius[k, ok], period=around)
        if lo < 0:
            continue
        r_lo, r_hi = radius[k, lo - 1], radius[k, hi + 1]
        for j in range(lo, hi + 1):
            if j <= half:
                t = (j - lo + 1) / (half - lo + 1)
                radius[k, j] = r_lo + (mids[k] - r_lo) * (3 * t * t - 2 * t * t * t)
            else:
                t = (hi + 1 - j) / (hi + 1 - half)
                radius[k, j] = r_hi + (mids[k] - r_hi) * (3 * t * t - 2 * t * t * t)
    return soften_closure(radius, spans), spans


def soften_closure(radius, spans):
    """The closed side and the creases at its edges blurred into each other: Meshy's sleeve ends there in a crease against the body, which
    read ring by ring is a ragged edge."""
    n, around = radius.shape
    mask = np.zeros((n, around))
    idx = np.arange(around)
    for k in range(n):
        lo, hi = spans[k]
        if lo >= 0:
            mask[k] = np.clip(1.0 - np.maximum(lo - idx, idx - hi) / 10.0, 0.0, 1.0)
    for _ in range(6):   # the last closed rings fade out down the sleeve, not at a step
        mask[1:] = np.maximum(mask[1:], mask[:-1] * 0.75)
    blurred = radius.copy()
    kernel = np.array([1.0, 4.0, 6.0, 4.0, 1.0]) / 16.0
    for _ in range(4):
        wrapped = np.concatenate([blurred[:, -2:], blurred, blurred[:, :2]], 1)
        blurred = sum(kernel[i] * wrapped[:, i:i + around] for i in range(5))
        padded = np.pad(blurred, ((2, 2), (0, 0)), mode="edge")
        blurred = sum(kernel[i] * padded[i:i + n] for i in range(5))
    return radius + mask * (blurred - radius)


def smoothstep(x):
    x = np.clip(x, 0.0, 1.0)
    return x * x * (3.0 - 2.0 * x)


class Reading:
    """One sleeve as read: its rings, their closed radii, and where any point lies among them."""

    def __init__(self, side, caster):
        self.side = side
        self.frames = ring_frames(side, caster, 0.80)
        self.radius, self.valid, self.face, self.hit = sample_rings(side, caster, self.frames, AROUND)
        self.closed, self.spans = close_fused_side(self.frames, self.radius, self.valid)
        self.centres = np.array([f[0] for f in self.frames])
        self.outs = np.array([f[1] for f in self.frames])
        angles = 2.0 * math.pi * np.arange(AROUND) / AROUND
        points = self.centres[:, None, :] + self.closed[:, :, None] * (
            np.cos(angles)[None, :, None] * self.outs[:, None, :] + np.sin(angles)[None, :, None] * BACK)
        middles = points.mean(axis=1)
        for axis in range(3):
            middles[:, axis] = np.convolve(np.pad(middles[:, axis], 4, mode="edge"), np.ones(9) / 9.0, mode="valid")
        self.middles = middles
        p, _, along = armhole_plane(side)
        self.hinge = p - along * HINGE_BELOW
        self.fan_from = math.atan2(along[2], along[0])
        self.fan_to = math.atan2(self.outs[FAN_RINGS][2], self.outs[FAN_RINGS][0])
        self.axis = self.frames[-1][2]
        self.cuff = next(k for k in range(60, len(self.frames)) if self.valid[k].mean() < 0.8)   # Meshy's sleeve ends before this ring

    def ring_of(self, pts):
        """Which ring (a fraction) each mirrored-frame point lies on: by its angle about the hinge in the fan, by how far down the sleeve
        below it."""
        v = pts - self.hinge
        angle = np.arctan2(v[:, 2], v[:, 0])
        fan = (self.fan_from - angle) / (self.fan_from - self.fan_to) * FAN_RINGS
        down = FAN_RINGS + ((pts - self.centres[FAN_RINGS]) @ self.axis) / RING_STEP
        return np.where(angle >= self.fan_to, fan, down)

    def along(self, table, k):
        """A per-ring table at fractional rings."""
        k = np.clip(k, 0.0, len(table) - 1.001)
        i = k.astype(int)
        f = (k - i)[:, None]
        return table[i] * (1.0 - f) + table[i + 1] * f

    def angle_of(self, pts, k):
        """Each point's angle round its ring, in read columns (0 = out, a quarter of AROUND = the back)."""
        offset = pts - self.along(self.centres, k)
        out = self.along(self.outs, k)
        return (np.arctan2(offset @ BACK, np.einsum("ij,ij->i", offset, out)) % (2.0 * math.pi)) * AROUND / (2.0 * math.pi)

    def surface(self, k, column):
        """The closed sleeve's surface at fractional ring and column, mirrored frame."""
        ki = np.clip(k, 0.0, len(self.frames) - 1.001)
        i = ki.astype(int)
        f = ki - i
        c0 = np.floor(column).astype(int) % AROUND
        g = column - np.floor(column)
        c1 = (c0 + 1) % AROUND
        r = (self.closed[i, c0] * (1 - g) + self.closed[i, c1] * g) * (1 - f) + (self.closed[i + 1, c0] * (1 - g) + self.closed[i + 1, c1] * g) * f
        t = column * 2.0 * math.pi / AROUND
        out = self.along(self.outs, ki)
        return self.along(self.centres, ki) + r[:, None] * (np.cos(t)[:, None] * out + np.sin(t)[:, None] * BACK)

    def tilted(self, pts, k):
        """Each point's ring counted along Meshy's tilted seams: a seam's points all have its middle's ring."""
        turn = self.angle_of(pts, k) * 2.0 * math.pi / AROUND
        return k - SEAM_TILT[0] * np.cos(turn) - SEAM_TILT[1] * np.sin(turn)

    def cut_rings(self):
        """How many rings the cut band takes out, and what the rest is pressed together by, so the hem comes to HEM_AT."""
        cut = CUT_BAND[self.side]
        out = cut[1] - cut[0] if cut else 0.0
        return out, (HEM_AT - PRESS_FROM) / (HEM[self.side][1] - out - PRESS_FROM)

    def place_of(self, pts, k):
        """The ring each point of the sleeve is put at: its own, less the cut band if it is below it, levelled, and pressed together
        below the armpit. Levelled: Meshy's quilting seams and hem lie on tilted rings (its sleeve leaned), so on this body's arm the hem
        rode up at the front; from LEVEL_FROM down a point is put at its seam's ring, not its own, in full LEVEL_OVER rings further on, so
        seams, hem and cuff all lie square to the arm (owner, 2026-11-20: the hem's edge "clean rounded off all around")."""
        out, press = self.cut_rings()
        tilted = self.tilted(pts, k)
        place = k.copy()
        if out:
            place[tilted > (CUT_BAND[self.side][0] + CUT_BAND[self.side][1]) * 0.5] -= out
        place -= smoothstep((k - LEVEL_FROM) / LEVEL_OVER) * (k - tilted)
        return np.where(place > PRESS_FROM, PRESS_FROM + (place - PRESS_FROM) * press, place)

    def hem_place(self):
        """The rings the hem's upper seam and lower lip are put at."""
        out, press = self.cut_rings()
        return tuple(PRESS_FROM + (h - out - PRESS_FROM) * press for h in HEM[self.side])

    def arm_frame(self, place):
        """The arm's middle at the rings place (mirrored frame)."""
        z = self.along(self.middles, place)[:, 2]
        return np.stack([ARM_TOP[0] + ARM_SLOPE * (ARM_TOP[1] - z) + 0.004, np.full(len(z), 0.004), z], 1)

    def arm_at(self, z):
        """The arm's middle at a height (mirrored frame)."""
        return np.array([ARM_TOP[0] + ARM_SLOPE * (ARM_TOP[1] - z) + 0.004, 0.004, z])

    def set_band(self, body):
        """Works out the ribbed cuff's outline, the same from under the hem to its lower edge: snug round the wrist, or the hand's own
        outline at the cuff's lower edge and HAND_CLEAR where that is wider (the thumb's root), rounded off so the band goes round the
        lump in a curve, not a point. body: the body's points, mirrored frame."""
        out = np.array([1.0, 0.0, 0.0]) - self.axis * self.axis[0]
        out /= np.linalg.norm(out)
        self.band_turn = 2.0 * math.pi * np.arange(KNIT_SEGMENTS) / KNIT_SEGMENTS
        self.band_around = np.cos(self.band_turn)[:, None] * out + np.sin(self.band_turn)[:, None] * BACK
        snug = KNIT_RADIUS + (KNIT_DEEP - KNIT_RADIUS) * np.abs(self.band_around @ BACK)
        hand = body[body[:, 0] > 0.38]
        wide = np.maximum(snug, hand_outline(hand, self.arm_at(KNIT_BOTTOM), self.axis, self.band_around) + HAND_CLEAR)
        eased = wide
        for _ in range(2):
            eased = 0.25 * np.roll(eased, 1) + 0.5 * eased + 0.25 * np.roll(eased, -1)
        self.band = np.maximum(eased, wide - 0.004)

    def band_at(self, turn):
        """The ribbed cuff's outline at angles round the arm (0 = out, a quarter turn = the back)."""
        return np.interp(turn % (2.0 * math.pi), self.band_turn, self.band, period=2.0 * math.pi)

    def round_arm(self, pts, place):
        """Fitted points about the arm's middle at their ring: how far out, the angle round, and the direction out (rows)."""
        offset = pts - self.arm_frame(place)
        offset -= np.outer(offset @ self.axis, self.axis)
        far = np.linalg.norm(offset, axis=1)
        return far, np.arctan2(offset @ BACK, offset @ self.band_around[0]), offset / np.maximum(far, 1e-9)[:, None]

    def fit_points(self, pts, k, still=None):
        """Mirrored-frame points of the sleeve moved onto the body's arm: each goes to the ring place_of gives it, centred on the arm's
        middle line, the sleeve brought to BAND_RADIUS round its last band, and its end eased out over the ribbed cuff that comes out of
        it. The shoulder stays where Meshy has it. still (0 to 1 per point) holds a point back: 0 where the sleeve is still one skin with
        the coat."""
        place = self.place_of(pts, k)
        weight = smoothstep((place - STILL_RINGS) / 30.0)[:, None]
        if still is not None:
            weight = weight * still[:, None]
        middle = self.along(self.middles, k)
        centre = middle + weight * (self.arm_frame(place) - middle)
        seam = HEM[self.side][0]
        own = float(self.closed[int(seam) - 16:int(seam) - 2].mean())
        girth = 1.0 + (BAND_RADIUS / own - 1.0) * smoothstep((place - STILL_RINGS) / (HEM_AT - 12.0 - STILL_RINGS))[:, None]
        fitted = self.over_cuff(centre + (pts - middle) * (1.0 + weight * (girth - 1.0)), place)
        wall = wall_x(self.side, fitted[:, 2], fitted[:, 1]) + WALL_CLEAR
        clear = wall + WALL_SOFT * np.logaddexp(0.0, (fitted[:, 0] - wall) / WALL_SOFT)
        fitted[:, 0] += (clear - fitted[:, 0]) * (1.0 if still is None else still)
        return fitted

    def over_cuff(self, fitted, place):
        """The sleeve's end eased outward where the ribbed cuff under it is wider than the hem's rolled lip (at the thumb's root): the whole
        hem moves out by what its lip lacks at that angle, less and less up the last band, so the roll keeps its shape and still lies over
        the cuff all the way round, as Meshy's does."""
        seam, lip = self.hem_place()
        far, turn, out = self.round_arm(fitted, place)
        at_lip = place > lip - 1.0
        if not at_lip.any():
            return fitted
        which = np.round(turn / (2.0 * math.pi) * KNIT_SEGMENTS).astype(int) % KNIT_SEGMENTS
        inside = np.array([far[at_lip & (which == i)].min() if (at_lip & (which == i)).any() else np.nan for i in range(KNIT_SEGMENTS)])
        idx = np.arange(KNIT_SEGMENTS)
        inside[np.isnan(inside)] = np.interp(idx[np.isnan(inside)], idx[~np.isnan(inside)], inside[~np.isnan(inside)], period=KNIT_SEGMENTS)
        lacks = np.maximum(self.band + HEM_OVER - inside, 0.0)
        lacks = np.maximum(lacks, 0.25 * np.roll(lacks, 1) + 0.5 * lacks + 0.25 * np.roll(lacks, -1))
        ease = smoothstep((place - (seam - FLARE_RINGS)) / FLARE_RINGS)
        fitted = fitted + out * (np.interp(turn % (2.0 * math.pi), self.band_turn, lacks, period=2.0 * math.pi) * ease)[:, None]
        far = self.round_arm(fitted, place)[0]
        short = np.maximum(self.band_at(turn) + HEM_OVER * 0.5 - far, 0.0) * smoothstep((place - (seam - 2.0)) / 2.0)
        return fitted + out * short[:, None]


def hand_outline(body, centre, axis, around, slab=0.006):
    """How far the body's skin reaches from centre in each of the directions around (rows), within slab of the plane through centre square
    to axis: the hand's outline there. body is the body's points, in the frame of the rest."""
    rel = body - centre
    near = rel[np.abs(rel @ axis) < slab]
    near = near[np.linalg.norm(near, axis=1) < 0.2]
    flat = near - np.outer(near @ axis, axis)
    far = np.linalg.norm(flat, axis=1)
    cosine = (flat @ around.T) / np.maximum(far, 1e-9)[:, None]
    reach = np.where(cosine > math.cos(math.pi / len(around)), far[:, None], 0.0)
    return reach.max(0) if len(near) else np.zeros(len(around))
