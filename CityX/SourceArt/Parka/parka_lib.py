# Shared pieces of the parka remodel (run inside the owner's Blender through exec): rays against the full-detail coat, the planes that part
# a sleeve from the body, and the rings a sleeve is re-drawn on.
import math

import bpy
import numpy as np
from mathutils import Vector

NS = bpy.app.driver_namespace

Z_ARMPIT = 1.58           # the coat's armpit: as high as Meshy's fused sleeve still has a neck to cut at
SHOULDER_TOP = (0.44, 1.90)   # where the armhole's plane leaves the shoulder, mirrored x and z

# Where the coat's side wall ends and the sleeve begins: x (mirrored) at heights, at y = 0, per side, and how much further in it is per metre
# towards the back. Read off the creases of the full-detail coat (the ends of each read ring's fused span) and, below them, the gap.
WALL = {
    1: [(0.80, 0.41), (0.88, 0.40), (0.96, 0.385), (1.04, 0.37), (1.12, 0.365), (1.22, 0.347), (1.295, 0.338), (1.34, 0.326), (1.39, 0.322),
        (1.48, 0.313), (2.2, 0.313)],
    -1: [(0.80, 0.43), (0.92, 0.418), (0.96, 0.404), (1.00, 0.394), (1.04, 0.378), (1.105, 0.372), (1.15, 0.364), (1.2, 0.356), (1.246, 0.340),
         (1.295, 0.335), (1.34, 0.330), (1.39, 0.326), (1.48, 0.309), (2.2, 0.309)],
}
WALL_TILT = -0.07


def wall_x(side, z, y=0.0):
    zs, xs = zip(*WALL[side])
    return np.interp(z, zs, xs) + WALL_TILT * y


def armhole_plane(side):
    """A point of the armhole's plane and its normal (mirrored frame, pointing into the sleeve)."""
    p = np.array([wall_x(side, Z_ARMPIT), 0.0, Z_ARMPIT])
    s = np.array([SHOULDER_TOP[0], 0.0, SHOULDER_TOP[1]])
    along = (s - p) / np.linalg.norm(s - p)
    normal = np.array([along[2], 0.0, -along[0]])   # along turned towards +x and down
    return p, normal, along


def in_sleeve(side, pts):
    """Which mirrored-frame points lie on the sleeve's side of both planes."""
    p, n, _ = armhole_plane(side)
    return ((pts - p) @ n > 0.0) & (pts[:, 0] > wall_x(side, pts[:, 2], pts[:, 1]))


class Caster:
    """Rays against an object, in world space."""

    def __init__(self, obj):
        self.obj = obj
        self.inv = obj.matrix_world.inverted()
        self.mat = obj.matrix_world.copy()
        self.scale = obj.matrix_world.to_scale()[0]

    def hits(self, origin, direction, reach):
        """Every hit along the ray up to reach: (distance, point, face index)."""
        o = self.inv @ Vector(origin)
        d = (self.inv.to_3x3() @ Vector(direction)).normalized()
        left = reach / self.scale
        gone = 0.0
        out = []
        while left > 1e-5 and len(out) < 12:
            ok, loc, _, face = self.obj.ray_cast(o, d, distance=left)
            if not ok:
                break
            step = (loc - o).length
            gone += step
            out.append((gone * self.scale, np.array(self.mat @ loc), face))
            o = loc + d * 2e-5
            left -= step + 2e-5
            gone += 2e-5
        return out
