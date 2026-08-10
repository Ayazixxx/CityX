import bpy, bmesh, sys, os, math
from mathutils import Vector

# Strips the Meshy "Skull Belted Figure" biped down to just the jeans (belt
# included -- it's part of the garment, not the body) and bakes them into
# wearables/assets/skelli_jeans.glb.
#
# THE CUT IS BY TEXTURE COLOUR, not by geometry, and that is the whole
# reason this file is short where tools/meshwork/build_cloak.py is long.
# That source was a textured figure whose garment had to be separated from
# skin by rays and weight thresholds. This one is an UNTEXTURED MANNEQUIN
# wearing a textured garment: measured over all 83810 verts, the body is
# EXACTLY pure white (1.0, 1.0, 1.0) and nothing else is. So "remove the
# body" is literally "drop every face whose texture is pure white", which is
# both trivial and exact -- it follows the real garment boundary, including
# around the belt studs, with no threshold to tune.
#
# Corroborated independently before relying on it: every non-white vertex
# but 157 sits inside z 0.06..0.63, the waist-to-ankle band, and those 157
# are edge/anti-alias strays up on the torso. GARMENT_Z_MAX drops them.
#
# SCALE is matched to the LEG, not to the figure's height. The source is a
# stylised, stubby-legged character (1.7m tall but only 0.58m from hip to
# foot, where this project's own 2.6m rig has 0.765m), so scaling by total
# height would leave the jeans far too short for the legs they have to
# cover. See LEG_SPAN_SOURCE below.

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst, outdir = argv[0], argv[1], argv[2]
TARGET_FACES = int(argv[3]) if len(argv) > 3 else 8000

# How far a vertex's texture colour may sit from pure white and still count
# as body. TIGHT on purpose. The mannequin is EXACTLY (1,1,1), so this only
# has to absorb texture filtering at the garment's own edge -- and the belt
# is bright polished silver, which a loose tolerance reads as body and eats.
# Measured at 0.04 it chewed notches out of the buckle and studs; the belt
# is part of the garment ("Skull Belted Figure"), so it stays.
BODY_COLOUR_EPSILON = float(argv[4]) if len(argv) > 4 else 0.012
# Everything above this in SOURCE metres is body no matter what colour it
# sampled -- see the 157 strays in this file's own header.
GARMENT_Z_MAX = 0.72

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)

# Meshy ships a stray Icosphere alongside the character; keep the real mesh.
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
obj = max(meshes, key=lambda m: len(m.data.vertices))
for x in list(bpy.context.scene.objects):
    if x is not obj and x.type == 'MESH':
        bpy.data.objects.remove(x, do_unlink=True)

# --- classify every vertex by the colour of the texture on it -------------
# Sampled BEFORE the armature is dropped and the mesh is transformed, while
# the UVs and the image are still exactly as authored.
img = None
for i in bpy.data.images:
    if i.size[0] > 0 and i.name != "Render Result":
        img = i
        break
if img is None:
    raise SystemExit("no texture image in the source -- the colour cut needs one")
W, H = img.size
px = list(img.pixels)
uv = obj.data.uv_layers.active.data


def sample(u, v):
    x = min(W - 1, max(0, int(u * W)))
    y = min(H - 1, max(0, int(v * H)))
    i = (y * W + x) * 4
    return px[i], px[i + 1], px[i + 2]


acc = {}
for poly in obj.data.polygons:
    for li in poly.loop_indices:
        vi = obj.data.loops[li].vertex_index
        u, v = uv[li].uv
        r, g, b = sample(u, v)
        c = acc.setdefault(vi, [0.0, 0.0, 0.0, 0])
        c[0] += r; c[1] += g; c[2] += b; c[3] += 1

is_garment = {}
for vi, c in acc.items():
    n = max(1, c[3])
    r, g, b = c[0] / n, c[1] / n, c[2] / n
    off_white = max(abs(1.0 - r), abs(1.0 - g), abs(1.0 - b)) > BODY_COLOUR_EPSILON
    z = (obj.matrix_world @ obj.data.vertices[vi].co).z
    is_garment[vi] = off_white and z <= GARMENT_Z_MAX
print("CLASSIFIED garment=%d body=%d of %d verts"
      % (sum(1 for x in is_garment.values() if x),
         sum(1 for x in is_garment.values() if not x), len(obj.data.vertices)))

# --- close speckle holes in the garment ----------------------------------
# The belt is polished silver, so its studs and buckle sample close enough
# to the mannequin's pure white to be classified as body -- which punched
# holes and notches straight through the buckle. Loosening the colour
# tolerance doesn't fix it: at 0.004 the cut instead starts swallowing
# near-white BODY verts (measured -- the garment bbox grew down to the feet
# at z=0.010 and out to x=0.321).
#
# So this is settled structurally rather than by colour. Body vertices form
# one enormous connected component (torso, head, arms, legs, all welded);
# a stud is a tiny island of "body" marooned inside the garment. Anything
# under ISLAND_MAX_VERTS is therefore a cut artefact, not body, and gets
# flipped back. Size alone separates them by three orders of magnitude, so
# there's nothing delicate about the threshold.
#
# CONFINED TO THE GARMENT'S OWN Z-BAND, and that guard is not optional. The
# jeans encircle each leg completely, which severs everything below the hem
# from the torso -- so the feet and lower shins are ALSO small "body"
# islands by pure connectivity, and reclaiming those dragged real body back
# in (measured: the mesh's bbox fell to z=-0.747, a whole leg below where
# any garment vertex sits). A stud is inside the denim; a foot is not.
ISLAND_MAX_VERTS = 400
garment_zs = sorted((obj.matrix_world @ obj.data.vertices[vi].co).z
                    for vi, g in is_garment.items() if g)
# PERCENTILES, not min/max. A handful of stray verts sample off-white down
# at the shoes and up on the torso, and taking the raw extremes let the band
# stretch to 0.000..0.717 -- wide enough to re-admit the feet this guard
# exists to exclude. The 0.2% trim cuts the strays and leaves the band on
# the denim itself.
BAND_TRIM = 0.002
BAND_LO = garment_zs[int(len(garment_zs) * BAND_TRIM)]
BAND_HI = garment_zs[int(len(garment_zs) * (1.0 - BAND_TRIM)) - 1]
print("GARMENT_BAND z %.3f .. %.3f (raw %.3f .. %.3f)"
      % (BAND_LO, BAND_HI, garment_zs[0], garment_zs[-1]))

adjacency = {}
for e in obj.data.edges:
    a, b = e.vertices
    adjacency.setdefault(a, []).append(b)
    adjacency.setdefault(b, []).append(a)

seen = set()
reclaimed = 0
islands = 0
for start in range(len(obj.data.vertices)):
    if start in seen or is_garment.get(start, False):
        continue
    stack = [start]
    seen.add(start)
    component = []
    while stack:
        vi = stack.pop()
        component.append(vi)
        for n in adjacency.get(vi, ()):  # noqa: E741
            if n not in seen and not is_garment.get(n, False):
                seen.add(n)
                stack.append(n)
    if len(component) > ISLAND_MAX_VERTS:
        continue
    zs = [(obj.matrix_world @ obj.data.vertices[vi].co).z for vi in component]
    if min(zs) < BAND_LO or max(zs) > BAND_HI:
        continue    # pokes outside the denim -- a foot, not a stud
    islands += 1
    reclaimed += len(component)
    for vi in component:
        is_garment[vi] = True
print("ISLANDS %d body islands (%d verts) reclaimed as garment detail" % (islands, reclaimed))

# --- drop the body -------------------------------------------------------
bpy.context.view_layer.objects.active = obj
for x in bpy.context.scene.objects:
    x.select_set(x is obj)
obj.modifiers.clear()
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# Keep the source rig's own joints before dropping it -- the scale and
# placement below are derived from where its legs actually were.
src_joint = {}
for a in [o for o in bpy.context.scene.objects if o.type == 'ARMATURE']:
    for b in a.data.bones:
        src_joint[b.name] = (a.matrix_world @ b.head_local).copy()
    bpy.data.objects.remove(a, do_unlink=True)

bm = bmesh.new()
bm.from_mesh(obj.data)
bm.verts.ensure_lookup_table()
# EVERY vertex of a face must be garment. "any vertex" would keep a ring of
# body faces bridging the waistband and the hem, which is exactly the fringe
# that has to come off.
doomed = [f for f in bm.faces if not all(is_garment.get(v.index, False) for v in f.verts)]
bmesh.ops.delete(bm, geom=doomed, context='FACES')
loose = [v for v in bm.verts if not v.link_faces]
bmesh.ops.delete(bm, geom=loose, context='VERTS')
bm.to_mesh(obj.data)
bm.free()
obj.data.update()
print("CUT to %d verts / %d faces" % (len(obj.data.vertices), len(obj.data.polygons)))

lo = Vector((1e9,) * 3); hi = Vector((-1e9,) * 3)
for v in obj.data.vertices:
    for i in range(3):
        lo[i] = min(lo[i], v.co[i]); hi[i] = max(hi[i], v.co[i])
print("GARMENT_BBOX lo=(%.3f %.3f %.3f) hi=(%.3f %.3f %.3f)" % (lo.x, lo.y, lo.z, hi.x, hi.y, hi.z))

# --- decimate ------------------------------------------------------------
if len(obj.data.polygons) > TARGET_FACES:
    dec = obj.modifiers.new("Decimate", 'DECIMATE')
    dec.ratio = TARGET_FACES / float(len(obj.data.polygons))
    bpy.ops.object.modifier_apply(modifier=dec.name)
    print("DECIMATED to %d faces" % len(obj.data.polygons))

# --- remap the source rig onto this project's own bone names -------------
# SIDES ARE SWAPPED, exactly as in build_cloak.py and for the same reason:
# the source figure already faces Godot +Z with no rotation (its own
# `headfront` bone points Blender -Y, which glTF export maps to Godot +Z),
# and a figure facing +Z has its own left at +X -- while this project's rig
# puts _l at -X. Pairing Left* with *_l would mirror the garment.
names = [g.name for g in obj.vertex_groups]
REMAP = {
    "Hips": "hips",
    "Spine": "spine", "Spine01": "spine", "Spine02": "spine",
    "LeftUpLeg": "thigh_r", "LeftLeg": "shin_r",
    "LeftFoot": "shin_r", "LeftToeBase": "shin_r",
    "RightUpLeg": "thigh_l", "RightLeg": "shin_l",
    "RightFoot": "shin_l", "RightToeBase": "shin_l",
}
merged = {}
for v in obj.data.vertices:
    a = {}
    for g in v.groups:
        tgt = REMAP.get(names[g.group])
        if tgt:
            a[tgt] = a.get(tgt, 0.0) + g.weight
    merged[v.index] = a
obj.vertex_groups.clear()
new_groups = {}
for tgt in sorted(set(REMAP.values())):
    new_groups[tgt] = obj.vertex_groups.new(name=tgt)
unweighted = 0
for vi, a in merged.items():
    total = sum(a.values())
    if total <= 0.0:
        # A vertex weighted only to bones this garment doesn't use (an arm,
        # say, catching the belt) would otherwise export with no influence
        # at all and sit frozen at the origin. Pin it to the hips.
        new_groups["hips"].add([vi], 1.0, 'REPLACE')
        unweighted += 1
        continue
    for tgt, w in a.items():
        new_groups[tgt].add([vi], w / total, 'REPLACE')
print("VGROUPS %s (%d verts fell back to hips)" % ([g.name for g in obj.vertex_groups], unweighted))

# --- scale and place -----------------------------------------------------
# This project's own rest skeleton (character/fused_body.gd), in Godot
# units. These MUST match that file; tests/test_skelli_jeans_check.gd
# compares them against bone_origins() and fails on drift, which is the only
# thing keeping this copy honest.
HIP_Y, FOOT_Y, HIP_X = 0.9, 0.135, 0.17
LEG_R = 0.135

# Scaled by the LEG, not by the figure's height -- see this file's header.
LEG_SPAN_SOURCE = src_joint["Hips"].z - src_joint["LeftFoot"].z
LEG_SPAN_GAME = HIP_Y - FOOT_Y
scale = LEG_SPAN_GAME / LEG_SPAN_SOURCE
print("SCALE %.4f (source hip->foot %.3f, game %.3f)" % (scale, LEG_SPAN_SOURCE, LEG_SPAN_GAME))

me = obj.data
for v in me.vertices:
    v.co = v.co * scale

# Lift so the WAISTBAND lands at the rig's own hip rather than wherever the
# source figure wore it. On the source these sit visibly below the hip
# joint -- the "sagged awfully low" the model was flagged with -- and
# carrying that offset across would bake the sag in permanently. Starting
# level with the hip is the neutral place to tune from; wearables/
# skelli_jeans.gd's own WAIST_LIFT moves it from there without a re-bake.
top_z = max(v.co.z for v in me.vertices)
lift = HIP_Y - top_z
for v in me.vertices:
    v.co.z += lift
me.update()
print("LIFTED %.3f so the waistband sits at HIP_Y=%.2f" % (lift, HIP_Y))

# --- clearance: the rig's own legs must fit inside ------------------------
# The source figure's legs are not this rig's legs. Where the garment sits
# closer to a leg axis than the leg's own radius, the character's shin comes
# straight through the denim -- the same failure the cloak had against the
# chest, solved the same way: a fixed-distance push outward, applied only
# where it's actually needed, so the garment's own hang is otherwise
# untouched.
CLEARANCE = LEG_R + 0.02
pushed = 0
worst = 0.0
for v in me.vertices:
    # Which leg axis this vertex belongs to, by side. Above the crotch both
    # legs merge into one seat, so only push below it.
    if v.co.z > HIP_Y - 0.18:
        continue
    axis_x = HIP_X if v.co.x > 0.0 else -HIP_X
    dx = v.co.x - axis_x
    dy = v.co.y                     # Blender -Y is Godot +Z; the leg axis is at y=0
    r = math.hypot(dx, dy)
    worst = max(worst, CLEARANCE - r)
    if r < CLEARANCE and r > 1e-6:
        k = CLEARANCE / r
        v.co.x = axis_x + dx * k
        v.co.y = dy * k
        pushed += 1
me.update()
print("CLEARANCE pushed %d verts out to %.3f (deepest intrusion was %.3f)" % (pushed, CLEARANCE, worst))

lo = Vector((1e9,) * 3); hi = Vector((-1e9,) * 3)
for v in me.vertices:
    for i in range(3):
        lo[i] = min(lo[i], v.co[i]); hi[i] = max(hi[i], v.co[i])
print("FINAL_BBOX lo=(%.3f %.3f %.3f) hi=(%.3f %.3f %.3f)" % (lo.x, lo.y, lo.z, hi.x, hi.y, hi.z))

# --- an armature at this project's own rest ------------------------------
# glTF only carries skin weights when there is a skin to carry them, so
# without this the vertex groups above would be dropped on export and the
# jeans would be a rigid prop. Godot re-binds these by NAME onto the
# character's real skeleton, so only the names and rest positions matter.
# Positions are Godot-space converted to the Blender space the vertices are
# now in: Blender (gx, -gz, gy). Every rest joint sits at gz=0.
SHOULDER_Y, HAND_X, HAND_Y, SHOULDER_X = 1.9, 0.58, 0.88, 0.35
NECK_Y = SHOULDER_Y + 0.001


def lerp3(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t)


REST = {"hips": (0, HIP_Y, 0), "spine": (0, (HIP_Y + SHOULDER_Y) * 0.5, 0),
        "neck": (0, SHOULDER_Y, 0), "head": (0, NECK_Y, 0)}
for side, sx in (("l", -1.0), ("r", 1.0)):
    sh = (sx * SHOULDER_X, SHOULDER_Y, 0.0)
    hd = (sx * HAND_X, HAND_Y, 0.0)
    REST["arm_" + side] = sh
    REST["forearm_" + side] = lerp3(sh, hd, 0.5)
    REST["hand_" + side] = lerp3(sh, hd, 0.85)
    hip = (sx * HIP_X, HIP_Y, 0.0)
    foot = (sx * HIP_X, FOOT_Y, 0.0)
    REST["thigh_" + side] = hip
    REST["shin_" + side] = lerp3(hip, foot, 0.5)
PARENT = {"spine": "hips", "neck": "spine", "head": "neck",
          "arm_l": "spine", "forearm_l": "arm_l", "hand_l": "forearm_l",
          "arm_r": "spine", "forearm_r": "arm_r", "hand_r": "forearm_r",
          "thigh_l": "hips", "shin_l": "thigh_l",
          "thigh_r": "hips", "shin_r": "thigh_r"}

arm_data = bpy.data.armatures.new("JeansRig")
arm_obj = bpy.data.objects.new("JeansRig", arm_data)
bpy.context.scene.collection.objects.link(arm_obj)
bpy.context.view_layer.objects.active = arm_obj
bpy.ops.object.mode_set(mode='EDIT')
eb = {}
for name, g in REST.items():
    b = arm_data.edit_bones.new(name)
    b.head = Vector((g[0], -g[2], g[1]))
    b.tail = b.head + Vector((0.0, 0.0, 0.08))
    eb[name] = b
for child, parent in PARENT.items():
    eb[child].parent = eb[parent]
bpy.ops.object.mode_set(mode='OBJECT')

obj.parent = arm_obj
mod = obj.modifiers.new("Armature", 'ARMATURE')
mod.object = arm_obj
print("RIG bones=%d" % len(arm_data.bones))

# --- keep the texture, but not at 4K -------------------------------------
# THE TEXTURE IS THE POINT of this garment -- the denim weave and the skull
# belt studs only exist in it, and exporting untextured (the way
# wearables/hooded_cloak.gd's source had to be, having no usable UVs) would
# reduce these to a black blob. But the source sheet is 4096x4096 and mostly
# covers the body that was just thrown away: exporting it as-is produced an
# 18MB glb for an 8000-face garment.
#
# Downscaling is the whole fix. The garment's own UV islands are a fraction
# of the sheet, so at 1024 the denim still reads at the size a pair of
# trousers occupies on screen.
TEXTURE_SIZE = 1024
for i in bpy.data.images:
    if i.size[0] > TEXTURE_SIZE:
        print("TEXTURE %dx%d -> %dx%d" % (i.size[0], i.size[1], TEXTURE_SIZE, TEXTURE_SIZE))
        i.scale(TEXTURE_SIZE, TEXTURE_SIZE)

# Double-sided at the source, so Godot imports it that way and the wearable
# needs no material override of its own: the waist and both cuffs are open,
# and single-sided they read as holes whenever you can see into them.
for m in obj.data.materials:
    if m is not None:
        m.use_backface_culling = False

os.makedirs(os.path.dirname(dst), exist_ok=True)
for x in bpy.context.scene.objects:
    x.select_set(x in (obj, arm_obj))
bpy.context.view_layer.objects.active = obj
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB', use_selection=True,
                          export_yup=True, export_apply=False,
                          export_skins=True, export_all_influences=False)
print("EXPORTED %s (%.1f MB)" % (dst, os.path.getsize(dst) / (1024.0 * 1024.0)))

# --- render the finished garment, with the rig's own legs drawn in --------
# The legs are the point of the render: they're what the clearance pass
# exists to keep inside the denim, and a jeans render on its own can't show
# whether that worked. The garment keeps its OWN material here rather than
# being flat-shaded, so the render also shows whether the texture survived.
legmat = bpy.data.materials.new("L"); legmat.use_nodes = True
legmat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.85, 0.5, 0.35, 1)
for sx in (-1.0, 1.0):
    bpy.ops.mesh.primitive_cylinder_add(radius=LEG_R, depth=HIP_Y - FOOT_Y,
                                        location=(sx * HIP_X, 0.0, (HIP_Y + FOOT_Y) * 0.5))
    leg = bpy.context.active_object
    leg.data.materials.append(legmat)

scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE_NEXT'
scene.render.resolution_x, scene.render.resolution_y = 520, 760
w = bpy.data.worlds.new("W"); scene.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[1].default_value = 2.2
cam_data = bpy.data.cameras.new("C"); cam_data.type = 'ORTHO'; cam_data.ortho_scale = 1.5
cam = bpy.data.objects.new("C", cam_data)
scene.collection.objects.link(cam); scene.camera = cam
for name, loc in (("front", (0, -3, 0.55)), ("side", (3, 0, 0.55))):
    cam.location = loc
    d = Vector((0, 0, 0.55)) - Vector(loc)
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = os.path.join(outdir, "jeans_" + name + ".png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", scene.render.filepath)
