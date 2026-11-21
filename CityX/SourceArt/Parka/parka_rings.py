# The rings a sleeve of Meshy's coat is read on (exec after parka_lib.py): a fan of planes turning about a line under the armpit, from the
# armhole's plane to the plane square to the sleeve, then planes square to the sleeve down to the cuff.
AXIS_SLOPE = 0.19          # how far the sleeve leans out per metre down (Meshy's, both sides)
HINGE_BELOW = 0.10         # the fan's hinge, this far under the armpit along the armhole's plane (inside the body, so no two rings cross in the sleeve)
FAN_RINGS = 40
RING_STEP = 0.008
RING_REACH = 0.26          # no sleeve wall is further from its ring's middle
DOUBLE_WALL = 0.03         # a second wall this close behind the first is the same wall's outside (the cuff's lining, a fold)


def ring_frames(side, caster, length):
    """The source rings of one sleeve, mirrored frame: each (centre, out, normal). out points away from the body in the ring's plane; the
    ring's other direction is +y (the back)."""
    p, _, along = armhole_plane(side)
    hinge = p - along * HINGE_BELOW
    axis = np.array([AXIS_SLOPE, 0.0, -1.0])
    axis /= np.linalg.norm(axis)
    square = np.array([-axis[2], 0.0, axis[0]])      # out, square to the sleeve
    a0 = math.atan2(along[2], along[0])
    a1 = math.atan2(square[2], square[0])
    frames = []
    for j in range(FAN_RINGS + 1):
        a = a0 + (a1 - a0) * j / FAN_RINGS
        out = np.array([math.cos(a), 0.0, math.sin(a)])
        wall = (wall_x(side, hinge[2]) - hinge[0]) / out[0]
        far = outer_hit(side, caster, hinge + out * (wall + 0.05), out)
        centre = hinge + out * (wall + (wall + 0.05 + far)) * 0.5
        frames.append((centre, out, np.array([out[2], 0.0, -out[0]])))
    centre = frames[-1][0]
    for i in range(1, int(length / RING_STEP) + 1):
        frames.append((centre + axis * RING_STEP * i, square, axis))
    return frames


def world(side, p):
    return (p[0] * side, p[1], p[2])


def first_wall(side, caster, origin, direction, reach=RING_REACH):
    """The sleeve's wall along a ray from inside it, mirrored frame: (distance, point, face) of its outside, or None where the ray leaves
    through the fused side into the body."""
    hits = caster.hits(world(side, origin), world(side, direction), reach + DOUBLE_WALL)
    pick = None
    for distance, where, face in hits:
        point = np.array([where[0] * side, where[1], where[2]])
        if not in_sleeve(side, point[None, :])[0] or (pick and distance - pick[0] > DOUBLE_WALL):
            break
        pick = (distance, point, face)
    return pick if pick and pick[0] <= reach else None


def outer_hit(side, caster, origin, direction):
    hit = first_wall(side, caster, origin, direction, 0.5)
    return hit[0] if hit else 0.2


def sample_rings(side, caster, frames, around):
    """Radius, validity, hit face and hit point of every ring at around angles (0 = out, a quarter turn = the back)."""
    n = len(frames)
    radius = np.zeros((n, around))
    valid = np.zeros((n, around), dtype=bool)
    face = np.full((n, around), -1, dtype=np.int64)
    point = np.zeros((n, around, 3))
    back = np.array([0.0, 1.0, 0.0])
    for k, (centre, out, _) in enumerate(frames):
        for j in range(around):
            t = 2.0 * math.pi * j / around
            hit = first_wall(side, caster, centre, math.cos(t) * out + math.sin(t) * back)
            if hit:
                radius[k, j], point[k, j], face[k, j] = hit[0], hit[1], hit[2]
                valid[k, j] = True
    return radius, valid, face, point
