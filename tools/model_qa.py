# MODEL QA: a model checked before it goes into the game (owner, 2026-10-19: "write your own QA check for imported models").
#   "C:/Program Files/Blender Foundation/Blender 4.2/blender.exe" --background --factory-startup --python tools/model_qa.py -- <model> [options]
#     <model>          .glb/.gltf/.fbx/.obj, or a Meshy .zip (the model and its maps inside are found)
#     --kind K         the item's kind, for its triangle budget: helmet, hat, top, coat, weapon, prop, character (none: no budget)
#     --height H       the size it should be, in metres, along its longest side (checked within 15%)
#     --render P.png   four views of the model with every problem ringed in red and labelled (H1, S2 ...); kept ones in green
#     --json P.json    the report as data
#     --keep H1,H2 --why "eye slits"   the owner says these are meant: written to the model's notes (<model>.qa.json beside it) and
#                      listed as kept from then on, on any later check of the same model (matched by place, within 3 cm)
#   blender ... --python tools/model_qa.py -- --selftest     builds broken models on purpose and checks each fault is caught
# The report is in plain words, each check PASS, WARN or FAIL, with where on the model (x across, y depth, z up, metres). Exit code 1 on a FAIL.
# What the checks are for (each one a fault met in this project): holes in a surface (the shearling coat's fur), spikes (the coat's collar),
# floating bits, two surfaces on top of each other (they flicker: z-fighting), faces turned inside out, broken edges, missing or squashed
# texture coordinates, maps too big for many players at once (a Meshy set is 4 x 2048: about 22 MB of video memory), and the wrong size.
import json
import math
import os
import sys
import tempfile
import zipfile

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.kdtree import KDTree

# Triangle budgets per kind: (good up to, at most). Owner's equippable budgets (2026-10-18) and this project's items.
BUDGETS = {"helmet": (5000, 8000), "hat": (4000, 6000), "top": (10000, 12000), "coat": (12000, 15000), "weapon": (6000, 10000),
           "prop": (5000, 10000), "character": (40000, 60000)}
MAP_GOOD, MAP_MOST = 1024, 2048           # a map's side: fine up to, too big past
SMALL_HOLE = 0.04                         # a hole whose rim is under this share of the model's size is a defect; bigger is an opening
HOLE_EDGES = 16                           # ... as is any rim of this few edges (a missing face or two); an opening has dozens
SPIKE_OUT = 0.5                           # a point standing out from its neighbours by this share of its own edges' length is a spike tip
WELD = 1e-5                               # points this close are one (an import splits them along UV seams)
SPIKE_LONG = 4.0                          # ... and by this many times the model's typical edge
FLOAT_SHARE = 0.01                      # a separate piece with under this share of the faces is a bit
FLOAT_GAP = 0.01                          # ... and a bit standing this far off the rest is floating
OVERLAP_GAP = 0.0005                      # two faces this close, parallel and overlapping draw over each other
SIZE_TOLERANCE = 0.15


KEEP_NEAR = 0.03                          # a note keeps a finding within this many metres of where it was noted
SHOWN = 12                                # findings listed per check (all are kept or counted)

# Each place a check finds gets a label, its letter and a number (H1, H2 ...), in the report and on the picture, so the owner can say which
# are meant: "H1 and H2 are eye slits". Those go in the model's notes (<model>.qa.json beside it) and are listed as kept from then on.
LETTERS = {"holes": "H", "spikes": "S", "floating bits": "F", "z-fighting": "Z", "turned faces": "T", "broken edges": "B",
           "zero-size faces": "D"}


def say(report, check, verdict, words, where=()):
    found = [{"id": "%s%d" % (LETTERS.get(check, "?"), i + 1), "at": [round(float(c), 3) for c in p], "kept": None}
             for i, p in enumerate(where)]
    report.append({"check": check, "verdict": verdict, "words": words, "found": found})


def notes_path(model):
    return model + ".qa.json"


def read_notes(model):
    try:
        with open(notes_path(model)) as f:
            return json.load(f).get("keep", [])
    except (OSError, ValueError):
        return []


def apply_notes(report, notes):
    """Findings the owner said are meant are kept, with their reason; a check whose every finding is kept passes."""
    for r in report:
        for finding in r["found"]:
            for note in notes:
                if note["check"] == r["check"] and np.linalg.norm(np.array(note["at"]) - np.array(finding["at"])) < KEEP_NEAR:
                    finding["kept"] = note["why"]
        kept = [f for f in r["found"] if f["kept"]]
        if r["found"] and len(kept) == len(r["found"]):
            r["verdict"] = "PASS"


def keep(model, report, ids, why):
    """Adds the findings with these labels to the model's notes, as meant."""
    notes = read_notes(model)
    wanted = {i.strip().upper() for i in ids.split(",") if i.strip()}
    for r in report:
        for finding in r["found"]:
            if finding["id"] in wanted:
                notes.append({"check": r["check"], "at": finding["at"], "why": why, "was": finding["id"]})
                wanted.discard(finding["id"])
    with open(notes_path(model), "w") as f:
        json.dump({"keep": notes}, f, indent=1)
    if wanted:
        print("  no finding labelled " + ", ".join(sorted(wanted)))
    return notes


# ---------------------------------------------------------------- loading
def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    folder = None
    if path.lower().endswith(".zip"):
        folder = tempfile.mkdtemp(prefix="model_qa_")
        with zipfile.ZipFile(path) as z:
            for member in z.namelist():
                target = os.path.realpath(os.path.join(folder, member))
                if target.startswith(os.path.realpath(folder)):   # nothing may unpack outside the folder
                    z.extract(member, folder)
        models = [os.path.join(r, f) for r, _, fs in os.walk(folder) for f in fs if f.lower().endswith((".glb", ".gltf", ".fbx", ".obj"))]
        if not models:
            raise SystemExit("no model in the zip")
        path = sorted(models, key=lambda m: (not m.lower().endswith(".glb"), m))[0]
        folder = os.path.dirname(path)
    ext = path.lower().rsplit(".", 1)[-1]
    if ext in ("glb", "gltf"):
        bpy.ops.import_scene.gltf(filepath=path)
    elif ext == "fbx":
        bpy.ops.import_scene.fbx(filepath=path)
    elif ext == "obj":
        bpy.ops.wm.obj_import(filepath=path)
    else:
        raise SystemExit("unknown model type: " + ext)
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    images = {}
    for image in bpy.data.images:
        if image.size[0] > 0:
            images[image.name] = (image.size[0], image.size[1], image.channels)
    if folder:   # a zip: the maps shipped beside the model, used or not (and not the import's copies of the same)
        images = {}
        for f in os.listdir(folder):
            if f.lower().endswith((".png", ".jpg", ".jpeg")):
                image = bpy.data.images.load(os.path.join(folder, f), check_existing=True)
                images[f] = (image.size[0], image.size[1], image.channels)
    return path, meshes, images


def welded(meshes):
    """All the model's faces as one welded triangle mesh in world space (seam-split points joined)."""
    bm = bmesh.new()
    for o in meshes:
        part = bmesh.new()
        part.from_mesh(o.data)
        part.transform(o.matrix_world)
        tmp = bpy.data.meshes.new("qa_tmp")
        part.to_mesh(tmp)
        bm.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)
        part.free()
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=WELD)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.verts.ensure_lookup_table()
    bm.faces.ensure_lookup_table()
    bm.edges.ensure_lookup_table()
    return bm


# ---------------------------------------------------------------- checks
def check_budget(report, meshes, kind):
    triangles = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes)
    if kind in BUDGETS:
        good, most = BUDGETS[kind]
        verdict = "PASS" if triangles <= good else "WARN" if triangles <= most else "FAIL"
        say(report, "triangles", verdict, "%d triangles (a %s: fine up to %d, at most %d)" % (triangles, kind, good, most))
    else:
        say(report, "triangles", "PASS", "%d triangles (give --kind for its budget)" % triangles)
    return triangles


def check_maps(report, images):
    if not images:
        say(report, "textures", "PASS", "no texture maps")
        return
    total = 0.0
    big = []
    for name, (w, h, channels) in sorted(images.items()):
        total += w * h * (1.0 if channels == 4 else 0.5) * 4.0 / 3.0   # compressed in the game (DXT1/DXT5), with its mips
        if max(w, h) > MAP_GOOD:
            big.append("%s %dx%d" % (name, w, h))
    mb = total / 1e6
    words = "%d maps, about %.1f MB of video memory in the game" % (len(images), mb)
    if any(max(w, h) > MAP_MOST for w, h, _ in images.values()):
        say(report, "textures", "FAIL", words + "; too big: " + ", ".join(big) + " (bake down to 512-1024)")
    elif big:
        say(report, "textures", "WARN", words + "; larger than 1024: " + ", ".join(big) + " (bake down to 512-1024 for an item)")
    else:
        say(report, "textures", "PASS", words)


def check_surface(report, bm, size):
    edges = bm.edges
    lengths = np.array([e.calc_length() for e in edges]) if len(edges) else np.zeros(1)
    typical = float(np.median(lengths)) if len(lengths) else 0.0

    # broken edges: more than two faces on one edge
    broken = [e for e in edges if len(e.link_faces) > 2]
    say(report, "broken edges", "PASS" if not broken else "WARN",
        "none" if not broken else "%d edges shared by more than two faces" % len(broken),
        [(e.verts[0].co + e.verts[1].co) / 2 for e in broken])

    # holes: rims of open edges, walked into loops
    rim = [e for e in edges if e.is_boundary]
    seen, loops = set(), []
    adjacency = {}
    for e in rim:
        for v in e.verts:
            adjacency.setdefault(v.index, []).append(e)
    for start in rim:
        if start.index in seen:
            continue
        loop, e, v = [], start, start.verts[0]
        while e is not None and e.index not in seen:
            seen.add(e.index)
            loop.append(e)
            v = e.other_vert(v)
            following = [x for x in adjacency[v.index] if x.index not in seen]
            e = following[0] if following else None
        loops.append(loop)
    small = []
    for loop in loops:
        perimeter = sum(e.calc_length() for e in loop)
        if perimeter < SMALL_HOLE * size or len(loop) <= HOLE_EDGES:
            centre = sum((e.verts[0].co for e in loop), Vector()) / len(loop)
            small.append((perimeter, centre))
    openings = len(loops) - len(small)
    if small:
        say(report, "holes", "WARN", "%d small holes in the surface: a fault, unless meant (eye slits) (and %d openings, such as sleeves or a collar)" %
            (len(small), openings), [c for _, c in sorted(small, key=lambda x: -x[0])])
    else:
        say(report, "holes", "PASS", "no small holes (%d openings, such as sleeves or a collar)" % openings)

    # spikes: a point standing far out from the points round it (long thin triangles alone are no fault: a blade is made of them)
    spikes = []
    for v in bm.verts:
        if len(v.link_edges) < 3 or v.is_boundary:
            continue
        ring = [e.other_vert(v).co for e in v.link_edges]
        out = (v.co - sum(ring, Vector()) / len(ring)).length
        reach = sum(e.calc_length() for e in v.link_edges) / len(v.link_edges)
        if out > SPIKE_LONG * typical and out > SPIKE_OUT * reach:
            spikes.append(v.co.copy())
    say(report, "spikes", "PASS" if not spikes else "WARN",
        "none" if not spikes else "%d points sticking out of the surface like spikes" % len(spikes), spikes)

    # zero-size faces
    flat = [f.calc_center_median() for f in bm.faces if f.calc_area() < 1e-12]
    say(report, "zero-size faces", "PASS" if not flat else "WARN", "none" if not flat else "%d faces with no area" % len(flat), flat)

    # faces turned the wrong way: neighbours whose shared edge runs the same way in both
    flipped = []
    for e in edges:
        if len(e.link_faces) != 2:
            continue
        a, b = e.link_faces
        la = next(l for l in a.loops if l.edge == e)
        lb = next(l for l in b.loops if l.edge == e)
        if la.vert == lb.vert:
            flipped.append((e.verts[0].co + e.verts[1].co) / 2)
    say(report, "turned faces", "PASS" if not flipped else "WARN",
        "none" if not flipped else "%d edges between faces turned opposite ways (one side would show inside out)" % len(flipped), flipped)
    return typical


def check_pieces(report, bm, size):
    faces = len(bm.faces)
    piece = [-1] * faces
    pieces = []
    for f in bm.faces:
        if piece[f.index] >= 0:
            continue
        stack, members = [f], []
        piece[f.index] = len(pieces)
        while stack:
            g = stack.pop()
            members.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if piece[h.index] < 0:
                        piece[h.index] = len(pieces)
                        stack.append(h)
        pieces.append(members)
    if len(pieces) <= 1:
        say(report, "floating bits", "PASS", "one piece")
        return
    largest = max(pieces, key=len)
    big_points = {v.index for f in largest for v in f.verts}
    tree = KDTree(len(big_points))
    for v in bm.verts:
        if v.index in big_points:
            tree.insert(v.co, v.index)
    tree.balance()
    floating = []
    for p in pieces:
        if p is largest or len(p) > FLOAT_SHARE * faces:
            continue
        gap = min(tree.find(v.co)[2] for f in p for v in f.verts)
        if gap > FLOAT_GAP:
            floating.append(sum((f.calc_center_median() for f in p), Vector()) / len(p))
    say(report, "floating bits", "PASS" if not floating else "WARN",
        ("%d pieces, none of them a stray bit" % len(pieces)) if not floating else
        "%d small pieces floating off the model (of %d pieces)" % (len(floating), len(pieces)), floating)


def check_overlaps(report, bm):
    centres = [f.calc_center_median() for f in bm.faces]
    tree = KDTree(len(centres))
    for i, c in enumerate(centres):
        tree.insert(c, i)
    tree.balance()
    pairs = []
    for f in bm.faces:
        n = f.normal
        size = math.sqrt(max(f.calc_area(), 1e-12))
        for _, j, distance in tree.find_range(centres[f.index], max(size * 0.5, OVERLAP_GAP)):
            if j <= f.index:
                continue
            g = bm.faces[j]
            if abs(n.dot(g.normal)) > 0.99 and abs((centres[j] - centres[f.index]).dot(n)) < OVERLAP_GAP and \
                    not set(f.verts) & set(g.verts):
                pairs.append(centres[f.index])
    say(report, "z-fighting", "PASS" if not pairs else "WARN",
        "no surfaces on top of each other" if not pairs else "%d faces lying on top of other faces (they would flicker)" % len(pairs),
        pairs)


def check_uvs(report, meshes):
    missing = [o.name for o in meshes if not o.data.uv_layers]
    if missing:
        say(report, "texture coordinates", "FAIL", "no texture coordinates on " + ", ".join(missing) + " (it cannot wear a texture)")
        return
    squashed = total = outside = 0
    for o in meshes:
        me = o.data
        uv = np.empty(len(me.loops) * 2)
        me.uv_layers.active.data.foreach_get("uv", uv)
        uv = uv.reshape(-1, 2)
        outside += int(((uv < -0.001) | (uv > 1.001)).any(1).sum())
        for p in me.polygons:
            corners = uv[p.loop_start:p.loop_start + p.loop_total]
            if len(corners) < 3:
                continue
            area = 0.5 * abs(np.cross(corners[1] - corners[0], corners[2] - corners[0]))
            total += 1
            squashed += area < 1e-9 and p.area > 1e-8
    share = squashed / max(total, 1)
    verdict = "PASS" if share < 0.01 and outside == 0 else "WARN"
    say(report, "texture coordinates", verdict, "%.1f%% of faces squashed to nothing on the texture; %d corners outside it" % (100 * share, outside))


def check_size(report, bm, height):
    co = np.array([v.co[:] for v in bm.verts])
    low, high = co.min(0), co.max(0)
    extent = high - low
    words = "%.2f x %.2f x %.2f m (across, deep, tall), standing from z %.2f to %.2f" % (*extent, low[2], high[2])
    if height:
        longest = float(extent.max())
        off = abs(longest - height) / height
        say(report, "size", "PASS" if off <= SIZE_TOLERANCE else "FAIL",
            words + "; its longest side is %.2f m, %s %.2f m" % (longest, "close to the asked" if off <= SIZE_TOLERANCE else "not the asked", height))
    else:
        say(report, "size", "PASS", words + " (give --height to check it)")
    return float(extent.max())


# ---------------------------------------------------------------- the picture
# A 3x5 pixel font for the labels: just the letters and digits they use.
GLYPHS = {"H": "101101111101101", "S": "111100111001111", "F": "111100110100100", "Z": "111001010100111", "T": "111010010010010",
          "B": "110101110101110", "D": "110101101101110", "0": "111101101101111", "1": "010110010010111", "2": "111001111100111",
          "3": "111001111001111", "4": "101101111001001", "5": "111100111001111", "6": "111100111101111", "7": "111001001001001",
          "8": "111101111101111", "9": "111101111001111", "?": "111001011000010"}


def draw_label(tile, x, y, text, colour, scale=4):
    """Writes text with its top-left at pixel (x, y) (y counted from the bottom, as Blender's pixels are), outlined in black."""
    h, w = tile.shape[:2]
    for outline, paint, grow in ((True, (0, 0, 0, 1), 1), (False, colour, 0)):
        for n, char in enumerate(text):
            bits = GLYPHS.get(char, GLYPHS["?"])
            for i, bit in enumerate(bits):
                if bit != "1":
                    continue
                px, py = x + (n * 4 + i % 3) * scale, y - (i // 3) * scale
                tile[max(py - scale + 1 - grow, 0):min(py + 1 + grow, h), max(px - grow, 0):min(px + scale + grow, w)] = paint


def draw_ring(tile, x, y, radius, colour, width=3):
    h, w = tile.shape[:2]
    yy, xx = np.ogrid[:h, :w]
    distance = np.sqrt((xx - x) ** 2 + (yy - y) ** 2)
    tile[(distance > radius - width) & (distance < radius + width)] = (0, 0, 0, 1)
    tile[(distance > radius - width + 1) & (distance < radius + width - 1)] = colour


TILE = 500
# The six views of the picture, left to right, top row then bottom: from the front (-y), the right, the back, the left, above and below
# (below: the inside of a helmet or hat).
VIEWS = (("front", (0, -1, 0.15)), ("right", (1, 0, 0.15)), ("back", (0, 1, 0.15)), ("left", (-1, 0, 0.15)),
         ("above", (0.6, -0.6, 0.8)), ("below", (0.3, -0.5, -0.8)))


def render(meshes, report, path):
    """The six views with each finding ringed and labelled where it can be seen; returns the labels no view can see."""
    from bpy_extras.object_utils import world_to_camera_view
    for o in meshes:
        o.hide_render = False
    marks = [(f["id"], Vector(f["at"]), f["kept"]) for r in report for f in r["found"][:SHOWN] + [x for x in r["found"][SHOWN:] if x["kept"]]
             if r["verdict"] != "PASS" or f["kept"]]
    co = np.array([(o.matrix_world @ v.co)[:] for o in meshes for v in o.data.vertices])
    centre, size = (co.min(0) + co.max(0)) / 2, float(np.ptp(co, 0).max())
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.render.resolution_x = scene.render.resolution_y = TILE
    camera = bpy.data.objects.new("QACam", bpy.data.cameras.new("QACam"))
    scene.collection.objects.link(camera)
    scene.camera = camera
    sheet = bpy.data.images.new("QASheet", TILE * 3, TILE * 2)
    pixels = np.zeros((TILE * 2, TILE * 3, 4), dtype=np.float32)
    seen = set()
    for i, (name, direction) in enumerate(VIEWS):
        d = Vector(direction).normalized()
        camera.location = Vector(centre) + d * size * 2.2
        camera.rotation_euler = (Vector(centre) - camera.location).to_track_quat("-Z", "Y").to_euler()
        bpy.context.view_layer.update()
        shot = os.path.join(tempfile.gettempdir(), "model_qa_%s.png" % name)
        scene.render.filepath = shot
        bpy.ops.render.render(write_still=True)
        tile = np.array(bpy.data.images.load(shot).pixels[:], dtype=np.float32).reshape(TILE, TILE, 4)
        depsgraph = bpy.context.evaluated_depsgraph_get()
        placed = []
        for label, at, kept in marks:
            x, y, depth = world_to_camera_view(scene, camera, at)
            if depth <= 0 or not (0 <= x <= 1 and 0 <= y <= 1):
                continue
            towards = at - camera.location
            hit, where, *_ = scene.ray_cast(depsgraph, camera.location, towards.normalized(), distance=towards.length)
            if hit and (where - at).length > 0.015 * size:   # behind the model from here: another view shows it
                continue
            seen.add(label)
            colour = (0.2, 0.9, 0.3, 1) if kept else (1, 0.15, 0.15, 1)
            draw_ring(tile, x * TILE, y * TILE, 14, colour)
            lx, ly = int(x * TILE) + 16, int(y * TILE) + 26
            while any(abs(lx - px) < 16 * len(label) and abs(ly - py) < 24 for px, py in placed):   # labels never on top of each other
                ly -= 24
            placed.append((lx, ly))
            draw_label(tile, lx, ly, label, colour)
        row, column = 1 - i // 3, i % 3
        pixels[row * TILE:(row + 1) * TILE, column * TILE:(column + 1) * TILE] = tile
    sheet.pixels.foreach_set(pixels.ravel())
    sheet.filepath_raw = path
    sheet.file_format = "PNG"
    sheet.save()
    return {label for label, _, _ in marks} - seen


# ---------------------------------------------------------------- the run
def qa(path, kind=None, height=None, render_path=None, json_path=None, keep_ids=None, why=None):
    model = path
    path, meshes, images = load(path)
    report = []
    bm = welded(meshes)
    check_budget(report, meshes, kind)
    check_maps(report, images)
    size = check_size(report, bm, height)
    check_surface(report, bm, size)
    check_pieces(report, bm, size)
    check_overlaps(report, bm)
    check_uvs(report, meshes)
    bm.free()
    notes = keep(model, report, keep_ids, why or "meant") if keep_ids else read_notes(model)
    apply_notes(report, notes)
    hidden = render(meshes, report, render_path) if render_path else set()
    print("\nMODEL QA: %s\n" % os.path.basename(path))
    for r in report:
        open_ = [f for f in r["found"] if not f["kept"]]
        kept = [f for f in r["found"] if f["kept"]]
        print("  %-5s %-20s %s" % (r["verdict"], r["check"], r["words"]))
        for f in open_[:SHOWN]:
            print("        %-4s at %s%s" % (f["id"], f["at"], "  (inside the model: no view of the picture shows it)" if f["id"] in hidden else ""))
        if len(open_) > SHOWN:
            print("        and %d more" % (len(open_) - SHOWN))
        for f in kept:
            print("        %-4s kept (%s)" % (f["id"], f["kept"]))
    if notes:
        print("\n  notes: %s (%d kept)" % (notes_path(model), len(notes)))
    worst = "FAIL" if any(r["verdict"] == "FAIL" for r in report) else "WARN" if any(r["verdict"] == "WARN" for r in report) else "PASS"
    print("\n  OVERALL: %s\n" % worst)
    if json_path:
        with open(json_path, "w") as f:
            json.dump({"model": path, "overall": worst, "checks": report}, f, indent=1)
    if render_path:
        print("  picture: %s (front, right, back / left, above, below; problems ringed in red with their labels, kept ones in green)" %
              render_path)
    return worst, report


def selftest():
    """Broken models made on purpose: each fault must be caught, and a clean model must pass."""
    folder = tempfile.mkdtemp(prefix="model_qa_selftest_")

    def save(name, build):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        build()
        path = os.path.join(folder, name + ".glb")
        bpy.ops.export_scene.gltf(filepath=path, export_format="GLB")
        return path

    def sphere():
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.5, location=(0, 0, 0.5), segments=48, ring_count=24)
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.uv.smart_project()
        bpy.ops.object.mode_set(mode="OBJECT")
        return bpy.context.active_object

    def holed():
        o = sphere()
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bm.faces.ensure_lookup_table()
        bmesh.ops.delete(bm, geom=[bm.faces[300]], context="FACES")
        bm.to_mesh(o.data)

    def flipped():
        o = sphere()
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bm.faces.ensure_lookup_table()
        bm.faces[300].normal_flip()
        bm.to_mesh(o.data)

    def spiked():
        o = sphere()
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bm.verts.ensure_lookup_table()
        v = bm.verts[400]
        v.co = v.co + v.co.normalized() * 0.6
        bm.to_mesh(o.data)

    def floating():
        sphere()
        bpy.ops.mesh.primitive_cube_add(size=0.02, location=(0.0, 0.0, 1.3))
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.uv.smart_project()
        bpy.ops.object.mode_set(mode="OBJECT")

    def doubled():
        sphere()
        bpy.ops.mesh.primitive_plane_add(size=0.2, location=(0, 0, 1.2))
        bpy.ops.mesh.primitive_plane_add(size=0.18, location=(0.01, 0, 1.2))
        for o in bpy.data.objects:
            bpy.context.view_layer.objects.active = o
            bpy.ops.object.mode_set(mode="EDIT")
            bpy.ops.uv.smart_project()
            bpy.ops.object.mode_set(mode="OBJECT")

    cases = [("clean", sphere, None), ("hole", holed, "holes"), ("turned", flipped, "turned faces"), ("spike", spiked, "spikes"),
             ("floating", floating, "floating bits"), ("z-fighting", doubled, "z-fighting")]
    failures = []
    for name, build, fault in cases:
        _, report = qa(save(name, build), kind="prop", height=1.0)
        flagged = {r["check"] for r in report if r["verdict"] != "PASS"}
        if fault is None and flagged:
            failures.append("the clean model was flagged for: " + ", ".join(sorted(flagged)))
        elif fault is not None and fault not in flagged:
            failures.append("the %s model was not caught (flagged: %s)" % (name, ", ".join(sorted(flagged)) or "nothing"))
    # A hole the owner says is meant: kept by its note on the next check, and only that one.
    holed_path = os.path.join(folder, "hole.glb")
    qa(holed_path, kind="prop", height=1.0, keep_ids="H1", why="an eye slit")
    _, report = qa(holed_path, kind="prop", height=1.0)
    holes = next(r for r in report if r["check"] == "holes")
    if holes["verdict"] != "PASS" or holes["found"][0]["kept"] != "an eye slit":
        failures.append("a hole noted as meant was still flagged")
    print("SELFTEST: " + ("PASS" if not failures else "FAIL\n  " + "\n  ".join(failures)))
    return not failures


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--selftest" in args:
        sys.exit(0 if selftest() else 1)

    def option(name, cast=str):
        return cast(args[args.index(name) + 1]) if name in args else None

    worst, _ = qa(args[0], kind=option("--kind"), height=option("--height", float), render_path=option("--render"), json_path=option("--json"),
                  keep_ids=option("--keep"), why=option("--why"))
    sys.exit(1 if worst == "FAIL" else 0)
