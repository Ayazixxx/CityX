# What every sneaker script takes from Meshy's "Urban Soles" (each colour of it is the same 960k-triangle pair, face for face, with its own
# UV layout and colour): the numbers that put a shoe on our foot, and the cut that takes the grey legs off. Load it into a script's globals
# with exec(), as garment_rig.py is.
import bmesh
import bpy
import numpy as np

TEXTURE_SIZE = 1024
SCALE = 0.30                     # Meshy's units to metres on our body
HIP_X = 0.17                     # BodyRig.HipX: the feet stand under the hips
LEG_SEED_Z = -0.15               # over this is leg alone (Meshy's units); traced down from there
COLLAR_RIM = -0.34               # the collars' rim (Meshy's units): a loose piece centred over it is left of the leg...
CRUMB = 40                       # ...as is one of fewer faces than this
TREAD_CUT = 0.036                # the tread is sliced off this far over the bottom (Meshy's units): over the lugs' grooves (0.025-0.035),
                                 # under the insole (0.04)
GREY = 0.6                       # in the white pair's colour the leg is grey under this; the shoe white
REACH = 0.045                    # how far the reduced surface may lie from Meshy's when the colour is baked across (past the lugs
                                 # under the flat sole)


def only(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[-1]


def face_colours(me, pixels):
    h, w = pixels.shape[:2]
    n = len(me.polygons)
    uv = np.zeros(len(me.loops) * 2)
    me.uv_layers.active.data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 2)
    starts = np.zeros(n, dtype=int)
    me.polygons.foreach_get("loop_start", starts)
    totals = np.zeros(n, dtype=int)
    me.polygons.foreach_get("loop_total", totals)
    mean = np.add.reduceat(uv, starts) / totals[:, None]
    return pixels[np.clip((mean[:, 1] * h).astype(int), 0, h - 1), np.clip((mean[:, 0] * w).astype(int), 0, w - 1), :3]


def take_legs_off(high, grey):
    """Cuts the legs off Meshy's pair, traced down from their tops through the faces grey marks (the white pair's grey: in another colour
    the shoe has grey of its own), and drops the scraps that leaves. Returns each shoe's collar middle (x, y), "right" and "left"."""
    me = high.data
    n = len(me.polygons)
    centres = np.zeros(n * 3)
    me.polygons.foreach_get("center", centres)
    centres = centres.reshape(n, 3)
    legs = {}
    for side, mask in (("right", centres[:, 0] < 0.0), ("left", centres[:, 0] >= 0.0)):
        legs[side] = centres[mask & (centres[:, 2] > LEG_SEED_Z), :2].mean(0)
        print("%s shoe: the collar's middle at %s" % (side, legs[side].round(3)))
    # The leg traced from its top down through grey faces only: it stops where the shoe's white begins, so the cut follows the collar's own
    # edge (the grey eyelets, ringed by white, are never reached).
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    cut = np.zeros(n, dtype=bool)
    stack = list(np.nonzero(centres[:, 2] > LEG_SEED_Z)[0])
    cut[stack] = True
    while stack:
        f = bm.faces[stack.pop()]
        for e in f.edges:
            for g in e.link_faces:
                if not cut[g.index] and grey[g.index]:
                    cut[g.index] = True
                    stack.append(g.index)
    for lo in np.arange(-0.81, -0.1, 0.1):
        band = (centres[:, 2] >= lo) & (centres[:, 2] < lo + 0.1)
        print("  leg traced %.2f..%.2f: %d of %d faces" % (lo, lo + 0.1, (cut & band).sum(), band.sum()))
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in np.nonzero(cut)[0]], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    # The shoe has its own lining under the collar, so the opening needs no lid. What the trace left of the leg (paler specks in its grey, a
    # scrap of tube) is now loose from the shoe: only each shoe's own body is kept.
    bm.faces.ensure_lookup_table()
    seen = set()
    pieces = []
    for f in bm.faces:
        if f.index in seen:
            continue
        piece, stack = [], [f]
        seen.add(f.index)
        while stack:
            g = stack.pop()
            piece.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index)
                        stack.append(h)
        pieces.append(piece)
    # A scrap stands where the leg was (over the collar's rim) or is a crumb; the lining and the laces, loose now too, sit in the shoe.
    scraps = [f for piece in pieces if len(piece) < CRUMB or np.mean([g.calc_center_median().z for g in piece]) > COLLAR_RIM for f in piece]
    print("kept %d pieces (the largest %s), dropped %d faces of scraps" % (
        sum(1 for p in pieces if len(p) >= CRUMB and np.mean([g.calc_center_median().z for g in p]) <= COLLAR_RIM),
        sorted([len(p) for p in pieces], reverse=True)[:6], len(scraps)))
    bmesh.ops.delete(bm, geom=scraps, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.to_mesh(me)
    bm.free()
    print("legs off: %d of %d faces cut, %d triangles left" % (cut.sum(), n, sum(len(p.vertices) - 2 for p in me.polygons)))
    return legs


def on_our_feet(verts, axis, sole):
    """Meshy's right shoe (on -x) onto our right foot, in Blender's frame: the game's (X, Y, Z) is Blender's (-x, z, -y), so the right foot's
    ankle at X = +HIP_X is Blender x = -HIP_X, the toe (Meshy's -y) forward; the collar's middle over the ankle, the sole on the ground."""
    verts = verts.copy()
    verts[:, 0] = (verts[:, 0] - axis[0]) * SCALE - HIP_X
    verts[:, 1] = (verts[:, 1] - axis[1]) * SCALE
    verts[:, 2] = (verts[:, 2] - sole) * SCALE
    return verts
