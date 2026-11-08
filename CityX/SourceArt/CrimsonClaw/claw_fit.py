"""Fit a held body into the Crimson Claw's -X fist (Unity model coords; docs/CRIMSON_CLAW.md, the fit). blender -b -P claw_fit.py -- <CrimsonClaw.glb> <sizes, e.g. 2.2,2.4,2.6>"""
import bpy, sys, math, time, bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

args = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=args[0])
ob = bpy.data.objects['RightClaw']
bm = bmesh.new(); bm.from_mesh(ob.data); bm.transform(ob.matrix_world)
# to Unity model coords: (x, y, z) -> (-x, z, -y)
for v in bm.verts:
    x, y, z = v.co
    v.co = Vector((-x, z, -y))
bm.normal_update()
tree = BVHTree.FromBMesh(bm)
V = np.array([v.co[:] for v in bm.verts])
wrist = np.array([(V[:, 0].min() + V[:, 0].max()) / 2, (V[:, 1].min() + V[:, 1].max()) / 2, V[:, 2].min()])
print("wrist", wrist.round(3), "bounds", V.min(0).round(3), V.max(0).round(3))

# inside grid in the claw's pivot frame (model units, origin at the wrist)
G = 0.012
lo = V.min(0) - wrist - 0.02; hi = V.max(0) - wrist + 0.02
nx, ny, nz = [int(math.ceil((hi[i] - lo[i]) / G)) + 1 for i in range(3)]
t0 = time.time()
inside = np.zeros((nx, ny, nz), bool)
d = Vector((0.577, 0.5771, 0.5773))
for i in range(nx):
    for j in range(ny):
        for k in range(nz):
            p = Vector((lo[0] + i * G, lo[1] + j * G, lo[2] + k * G)) + Vector(wrist)
            n = 0; o = p
            while True:
                loc, nor, idx, dist = tree.ray_cast(o, d, 5)
                if loc is None: break
                n += 1; o = loc + d * 1e-4
            inside[i, j, k] = n % 2 == 1
print("grid", inside.shape, inside.sum(), "inside cells", round(time.time() - t0, 1), "s")

def lookup(P):
    idx = np.round((P - lo) / G).astype(int)
    ok = np.all((idx >= 0) & (idx < np.array([nx, ny, nz])), axis=1)
    out = np.zeros(len(P), bool)
    ii = idx[ok]
    out[ok] = inside[ii[:, 0], ii[:, 1], ii[:, 2]]
    return out

# the player's body in metres, feet at the origin, y up, z its front, x its right
def capsule(a, b, r, step=0.05):
    a, b = np.array(a, float), np.array(b, float)
    lo_ = np.minimum(a, b) - r; hi_ = np.maximum(a, b) + r
    g = np.mgrid[lo_[0]:hi_[0]:step, lo_[1]:hi_[1]:step, lo_[2]:hi_[2]:step].reshape(3, -1).T
    ab = b - a; t = np.clip(((g - a) @ ab) / max(ab @ ab, 1e-9), 0, 1)
    dist = np.linalg.norm(g - (a + t[:, None] * ab), axis=1)
    return g[dist <= r]
grip = np.vstack([capsule((0, 1.0, 0), (0, 1.64, 0), 0.26), capsule((-0.17, 0.9, 0), (0.17, 0.9, 0), 0.15),
                  capsule((-0.35, 1.84, 0), (0.35, 1.84, 0), 0.12),
                  capsule((-0.35, 1.84, 0), (-0.52, 0.95, 0), 0.09), capsule((0.35, 1.84, 0), (0.52, 0.95, 0), 0.09)])
outside = np.vstack([capsule((0, 2.25, 0), (0, 2.25, 0), 0.35), capsule((-0.17, 0.9, 0), (-0.17, 0.14, 0), 0.135),
                     capsule((0.17, 0.9, 0), (0.17, 0.14, 0), 0.135)])
mid = np.array([0, 1.4, 0])
ring = []
for h in np.arange(1.0, 1.75, 0.1):
    for a in range(16):
        for r in (0.32, 0.38, 0.45):
            ring.append((a, (r * math.sin(a * math.pi / 8), h, r * math.cos(a * math.pi / 8))))
ring_sector = np.array([s for s, _ in ring]); ring_p = np.array([p for _, p in ring])
print("samples", len(grip), len(outside), len(ring_p))

def rot(pitch, yaw, roll):
    x, y, z = map(math.radians, (pitch, yaw, roll))
    Rx = np.array([[1, 0, 0], [0, math.cos(x), -math.sin(x)], [0, math.sin(x), math.cos(x)]])
    Ry = np.array([[math.cos(y), 0, math.sin(y)], [0, 1, 0], [-math.sin(y), 0, math.cos(y)]])
    Rz = np.array([[math.cos(z), -math.sin(z), 0], [math.sin(z), math.cos(z), 0], [0, 0, 1]])
    return Ry @ Rx @ Rz   # Unity's Quaternion.Euler

def score(p, pitch, roll, yaw, psi, S):
    # body upright in the creature's frame, turned psi; the claw turned (pitch, yaw, roll) about its wrist. In the claw's rest frame the body
    # is turned by R_claw^-1 * Yaw(psi), its middle at p.
    Rb = rot(pitch, yaw, roll).T @ rot(0, psi, 0)
    f = lambda B: p + ((B - mid) / S) @ Rb.T
    g_in = lookup(f(grip)).mean()
    o_in = lookup(f(outside)).mean()
    cov = lookup(f(ring_p))
    sectors = len(set(ring_sector[cov].tolist())) / 16.0
    return sectors - 3.0 * g_in - 8.0 * o_in, sectors, g_in, o_in

best = []
hollow = np.array([0.07, 0.02, 0.48])
scales = [float(s) for s in args[1].split(",")]
t0 = time.time()
for S in scales:
    for pitch in range(-30, 31, 15):
        for roll in range(-30, 31, 15):
            for yaw in (-20, 0, 20):
                for psi in range(0, 360, 30):
                    for dx in np.arange(-0.12, 0.121, 0.04):
                        for dy in np.arange(-0.12, 0.121, 0.04):
                            for dz in np.arange(-0.12, 0.121, 0.04):
                                p = hollow + np.array([dx, dy, dz])
                                s = score(p, pitch, roll, yaw, psi, S)
                                best.append((s[0], s[1], s[2], s[3], S, pitch, roll, yaw, psi, tuple(np.round(p, 3))))
    print("scale", S, "done", round(time.time() - t0), "s")
best.sort(key=lambda b: -b[0])
def show(tag, b):
    print(tag, "score", round(b[0], 3), "wrap", round(b[1], 2), "body-in-claw", round(b[2], 3), "head/legs-in-claw", round(b[3], 3),
          "S", b[4], "pitch", b[5], "roll", b[6], "yaw", b[7], "psi", b[8], "p", b[9])
for b in best[:8]:
    show("B", b)
for S in scales:
    bs = [b for b in best if b[4] == S]
    for b in bs[:4]:
        show("BEST@" + str(S), b)
