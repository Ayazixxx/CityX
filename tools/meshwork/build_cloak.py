import bpy, bmesh, sys, os, math
from mathutils import Vector

# Strips the Meshy "Hooded Figure" biped down to just the garment and bakes
# it into wearables/assets/hooded_cloak.glb.
#
# The source is a whole hooded figure whose head, hands and feet are welded
# into ONE 73k-vert shell -- so unlike the earlier cloak (four loose shells,
# where isolating the garment was just deleting loose parts) this is a real
# geometric cut. What each cut keys off, and why:
#
#   head   -- there is no separate head shell to delete. Rays fired from
#             inside the head cross exactly one surface in every direction:
#             the face dome flows continuously into the hood. So "remove the
#             head" is really "open a face hole in the hood", cut as a cone
#             about the forward axis.
#   hands  -- welded to the sleeve at the cuff, so no plane separates them.
#             The rig's own LeftHand/RightHand weights do, and they land on
#             the hand blob plus a sliver of cuff, which is the right side to
#             err on for a sleeve that should read as open.
#   feet   -- same, via the Foot/ToeBase weights.
#
# Deleting whole faces by a threshold leaves a sawtooth rim, so every cut is
# followed by a spike-removal + boundary-relax pass. Decimation last, after
# the topology is final.

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst, outdir = argv[0], argv[1], argv[2]
HOOD_CONE_DEGREES = float(argv[3]) if len(argv) > 3 else 55.0
TARGET_FACES = int(argv[4]) if len(argv) > 4 else 12000

# The character is 2.6m sole-to-crown (character/fused_body.gd), the source
# figure 1.7m.
TARGET_HEIGHT = 2.6

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
obj = max(meshes, key=lambda m: len(m.data.vertices))
for x in list(bpy.context.scene.objects):
    if x is not obj and x.type == 'MESH':
        bpy.data.objects.remove(x, do_unlink=True)
bpy.context.view_layer.objects.active = obj
for x in bpy.context.scene.objects:
    x.select_set(x is obj)
obj.modifiers.clear()
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
# Keep the source rig's own shoulder/wrist joints before dropping it -- the
# sleeve realignment below needs the line the sleeves were actually modelled
# around, not a guess at it.
src_joint = {}
for a in [o for o in bpy.context.scene.objects if o.type == 'ARMATURE']:
    for b in a.data.bones:
        src_joint[b.name] = (a.matrix_world @ b.head_local).copy()
    bpy.data.objects.remove(a, do_unlink=True)

names = [g.name for g in obj.vertex_groups]
HAND = {"LeftHand", "RightHand"}
FOOT = {"LeftFoot", "LeftToeBase", "RightFoot", "RightToeBase"}
dom = {}
for v in obj.data.vertices:
    best, bw = None, -1.0
    for g in v.groups:
        if g.weight > bw:
            bw, best = g.weight, names[g.group]
    dom[v.index] = best

HEAD_CENTRE = Vector((0.0, -0.02, 1.47))
FORWARD = Vector((0.0, -1.0, 0.0))       # the rig's headfront sits at -y
cos_lim = math.cos(math.radians(HOOD_CONE_DEGREES))

bm = bmesh.new(); bm.from_mesh(obj.data)
bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
kill = []
for f in bm.faces:
    c = f.calc_center_median()
    idxs = [v.index for v in f.verts]
    if c.z > 1.20 and (c - HEAD_CENTRE).normalized().dot(FORWARD) > cos_lim:
        kill.append(f); continue
    if all(dom.get(i) in HAND for i in idxs) or all(dom.get(i) in FOOT for i in idxs):
        kill.append(f)
removed = len(kill)
bmesh.ops.delete(bm, geom=kill, context='FACES')

# Sawtooth cleanup: a face left hanging by two or more of its own edges is a
# spike on the new rim, not part of the garment.
for _ in range(4):
    spikes = [f for f in bm.faces
              if sum(1 for e in f.edges if len(e.link_faces) == 1) >= 2]
    if not spikes:
        break
    bmesh.ops.delete(bm, geom=spikes, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')

# Relax the rim itself so the remaining stair-stepping reads as a hem.
rim = [v for v in bm.verts if any(len(e.link_faces) == 1 for e in v.link_edges)]
for _ in range(6):
    bmesh.ops.smooth_vert(bm, verts=rim, factor=0.55,
                          use_axis_x=True, use_axis_y=True, use_axis_z=True)
print("CUT removed=%d faces=%d verts=%d rim_verts=%d"
      % (removed, len(bm.faces), len(bm.verts), len(rim)))
bm.to_mesh(obj.data); bm.free()
obj.data.update()

before = len(obj.data.polygons)
if TARGET_FACES < before:
    d = obj.modifiers.new("dec", 'DECIMATE')
    d.decimate_type = 'COLLAPSE'
    d.ratio = TARGET_FACES / float(before)
    bpy.ops.object.modifier_apply(modifier=d.name)
print("DECIMATE %d -> %d faces" % (before, len(obj.data.polygons)))

bpy.ops.object.shade_smooth()

# The rig's bones are named for a Mixamo-style biped; rename the weight
# groups to this project's own bone names (character/fused_body.gd) so the
# garment can later be driven straight off the character's skeleton. Several
# source bones collapse onto one of ours -- our rig has no shoulder or toe
# bone, and one spine instead of three.
# NOTE the side swap: the source's Left* bones sit at +x and land at Godot
# +x, where THIS rig's *_r bones live (arm_r is at +SHOULDER_X). The bone
# names are positions, not anatomy -- what has to line up is which side of
# the body the geometry ends up on, so Left* maps to *_r here. Turning the
# garment 180 degrees instead would pair Left* with *_l, but it would also
# face the cloak backwards; see the export block below.
REMAP = {
    "Hips": "hips",
    "Spine": "spine", "Spine01": "spine", "Spine02": "spine",
    "LeftShoulder": "spine", "RightShoulder": "spine",
    "neck": "neck", "Head": "head", "head_end": "head", "headfront": "head",
    "LeftArm": "arm_r", "LeftForeArm": "forearm_r", "LeftHand": "hand_r",
    "RightArm": "arm_l", "RightForeArm": "forearm_l", "RightHand": "hand_l",
    "LeftUpLeg": "thigh_r", "LeftLeg": "shin_r",
    "LeftFoot": "shin_r", "LeftToeBase": "shin_r",
    "RightUpLeg": "thigh_l", "RightLeg": "shin_l",
    "RightFoot": "shin_l", "RightToeBase": "shin_l",
}
merged = {}
for v in obj.data.vertices:
    acc = {}
    for g in v.groups:
        tgt = REMAP.get(names[g.group])
        if tgt:
            acc[tgt] = acc.get(tgt, 0.0) + g.weight
    merged[v.index] = acc
obj.vertex_groups.clear()
new_groups = {}
for tgt in sorted(set(REMAP.values())):
    new_groups[tgt] = obj.vertex_groups.new(name=tgt)
for vi, acc in merged.items():
    total = sum(acc.values()) or 1.0
    for tgt, w in acc.items():
        new_groups[tgt].add([vi], w / total, 'REPLACE')
print("VGROUPS %s" % [g.name for g in obj.vertex_groups])

# This project's own rest skeleton (character/fused_body.gd), in Godot units.
# These MUST match that file -- they set the rest the exported armature is
# built at, and the cloak binds onto the character's skeleton by bone name.
# tests/test_hooded_cloak_check.gd compares them against bone_origins() and
# fails on drift, which is the only thing keeping this copy honest.
HIP_Y, SHOULDER_Y, FOOT_Y = 0.9, 1.9, 0.135
HIP_X, SHOULDER_X, HAND_X, HAND_Y = 0.17, 0.35, 0.58, 0.88
NECK_Y_GAME = SHOULDER_Y + 0.001

lo = Vector((1e9,)*3); hi = Vector((-1e9,)*3)
for v in obj.data.vertices:
    for i in range(3):
        lo[i] = min(lo[i], v.co[i]); hi[i] = max(hi[i], v.co[i])
scale = TARGET_HEIGHT / 1.700   # the SOURCE figure's full height, not the
                                # stripped garment's (the hem stops short of
                                # the sole once the feet are gone)
print("STRIPPED_BBOX lo=(%.3f %.3f %.3f) hi=(%.3f %.3f %.3f) scale=%.4f"
      % (lo.x, lo.y, lo.z, hi.x, hi.y, hi.z, scale))

# Bake the game-space scale into the vertices. NO yaw: this project's
# characters face Godot +Z, not -Z. The authority on that is ui/hud.gd,
# which parks the portrait camera at `head_pos + basis.z * distance` and
# has to be looking at the face -- so +z is the front. (Don't reason from
# wearables/hoodie.gd's hood sitting at +z; a hood is behind the neck either
# way, and that's what sent the first build out backwards.)
#
# The source figure faces Blender -Y, and glTF export maps Blender -Y to
# Godot +Z, so it already faces front with no rotation at all. Turning it
# 180 degrees to "fix" the facing is what put the hood opening on the
# wearer's back.
me = obj.data
for v in me.vertices:
    v.co = v.co * scale
me.update()

# The sleeves are left exactly where they were modelled. An earlier pass
# rotated them onto this rig's own shoulder->wrist line to stop the
# character's arms coming through the sides, but the garment's hang is the
# part worth keeping -- so the RIG moved to meet the sleeve instead (see
# character/fused_body.gd's own SHOULDER_X, widened for exactly this).
# Report the two lines so a future drift between them is visible here.
WRIST_T = 0.85
for side, sx, src_side in (("r", 1.0, "Left"), ("l", -1.0, "Right")):
    a0 = src_joint[src_side + "Arm"] * scale
    a1 = src_joint[src_side + "Hand"] * scale
    b0 = Vector((sx * SHOULDER_X, 0.0, SHOULDER_Y))
    b1 = Vector((sx * (SHOULDER_X + (HAND_X - SHOULDER_X) * WRIST_T),
                 0.0,
                 SHOULDER_Y + (HAND_Y - SHOULDER_Y) * WRIST_T))
    print("SLEEVE %s sleeve=(%.3f %.3f)->(%.3f %.3f) rig_arm=(%.3f %.3f)->(%.3f %.3f) mismatch=%.1f deg"
          % (side, a0.x, a0.z, a1.x, a1.z, b0.x, b0.z, b1.x, b1.z,
             math.degrees((a1 - a0).angle((b1 - b0)))))

# CLEARANCE. Scaling the source figure to 2.6m makes it the right HEIGHT but
# not the right BUILD: this project's character is far chunkier for its
# height than the human proportions Meshy generated. Its torso capsule is
# TORSO_R=0.26 from the spine axis and its head is a 0.7-wide ball centred at
# NECK_Y + 0.35, while the scaled garment's chest sits nearer 0.23 out and
# its hood cavity is about 0.34 -- so worn as-is the body pushes straight
# through the chest and the head swallows the hood entirely, leaving what
# looks like a collar.
#
# The fix is a fixed-DISTANCE push outward, not a scale factor. A scale big
# enough to clear the chest would balloon the sleeves and hem with it; a
# fixed offset adds the same clearance everywhere and barely changes the
# silhouette where the garment is already wide.
BODY_CLEARANCE = 0.12         # away from the body's own vertical axis
HOOD_CLEARANCE = 0.16         # radially away from the head's centre
HEAD_CENTRE_Z = NECK_Y_GAME + 0.35    # character.gd's own head offset
HOOD_BLEND_LO, HOOD_BLEND_HI = 1.95, 2.15

def smoothstep(lo, hi, x):
    t = min(1.0, max(0.0, (x - lo) / (hi - lo)))
    return t * t * (3.0 - 2.0 * t)

head_centre = Vector((0.0, 0.0, HEAD_CENTRE_Z))
for v in me.vertices:
    p = v.co.copy()
    t = smoothstep(HOOD_BLEND_LO, HOOD_BLEND_HI, p.z)
    axial = Vector((p.x, p.y, 0.0))
    dir_body = axial.normalized() if axial.length > 1e-5 else Vector((0.0, 1.0, 0.0))
    radial = p - head_centre
    dir_head = radial.normalized() if radial.length > 1e-5 else Vector((0.0, 0.0, 1.0))
    v.co = p + dir_body * (BODY_CLEARANCE * (1.0 - t)) + dir_head * (HOOD_CLEARANCE * t)
me.update()
print("CLEARANCE body=%.2f hood=%.2f about head_z=%.3f"
      % (BODY_CLEARANCE, HOOD_CLEARANCE, HEAD_CENTRE_Z))

# HOOD LOCK. The hood came out of the source rig sharing its weights between
# head, neck and spine -- so on any animation that cranks the torso hard (the
# charged swing is the one that showed it) the hood follows the spine while
# the head follows the head bone, they move apart, and the back of the head
# comes out through the hood.
#
# Above HOOD_LOCK_HI the hood is bound to the HEAD BONE ALONE, so it can only
# ever move exactly as the head moves and the head can't overtake it, at any
# pose. Ramped in from HOOD_LOCK_LO instead of switched, or the collar would
# shear where locked cloth meets cloth that still follows the shoulders.
#
# This is a WEIGHTING change: no vertex moves, nothing is resized. That was
# the constraint -- the fit is already right, only the following was wrong.
HOOD_LOCK_LO, HOOD_LOCK_HI = 1.95, 2.10
head_group = obj.vertex_groups["head"]
other_groups = [g for g in obj.vertex_groups if g.name != "head"]
locked = 0
for v in me.vertices:
    t = smoothstep(HOOD_LOCK_LO, HOOD_LOCK_HI, v.co.z)
    if t <= 0.001:
        continue
    acc = merged.get(v.index, {})
    total = sum(acc.values()) or 1.0
    head_w = acc.get("head", 0.0) / total
    new_head_w = head_w + (1.0 - head_w) * t
    head_group.add([v.index], new_head_w, 'REPLACE')
    # Everything else shrinks by whatever share the head just took, so the
    # vertex still sums to 1 and doesn't quietly lose influence.
    shrink = (1.0 - new_head_w) / max(1e-6, 1.0 - head_w)
    for g in other_groups:
        w = acc.get(g.name, 0.0) / total
        if w > 0.0:
            g.add([v.index], w * shrink, 'REPLACE')
    locked += 1
print("HOOD_LOCK %d verts bound toward head-only above z=%.2f" % (locked, HOOD_LOCK_LO))

# SLEEVE LOCK -- the same problem as the hood, one limb down. The sleeves
# came off the source rig carrying some spine/shoulder influence, while the
# body's arm is driven by the arm chain alone. Shared bones, different
# blends, so on a run cycle the sleeve and the arm inside it swing by
# slightly different amounts and the arm comes through.
#
# Anything already mostly arm-driven gets bound to the arm chain ONLY, so
# the sleeve can move exactly as the arm moves and nothing else can pull it.
# Ramped on the vertex's existing arm weight rather than a height, so it
# fades out naturally where the sleeve meets the shoulder instead of
# needing a hand-picked cut line.
ARM_CHAINS = (("arm_l", "forearm_l", "hand_l"), ("arm_r", "forearm_r", "hand_r"))
SLEEVE_LOCK_LO, SLEEVE_LOCK_HI = 0.20, 0.50
groups_by_name = {g.name: g for g in obj.vertex_groups}
sleeve_locked = 0
for v in me.vertices:
    acc = merged.get(v.index, {})
    total = sum(acc.values()) or 1.0
    shares = [sum(acc.get(b, 0.0) for b in chain) / total for chain in ARM_CHAINS]
    best = 0 if shares[0] >= shares[1] else 1
    arm_share = shares[best]
    t = smoothstep(SLEEVE_LOCK_LO, SLEEVE_LOCK_HI, arm_share)
    if t <= 0.001 or arm_share <= 1e-6:
        continue
    chain = ARM_CHAINS[best]
    # Everything off the chain fades by t; the chain takes up the slack,
    # keeping its own internal proportions so the elbow still bends where
    # it did.
    off_chain_after = (1.0 - arm_share) * (1.0 - t)
    chain_after = 1.0 - off_chain_after
    chain_scale = chain_after / arm_share
    for name, g in groups_by_name.items():
        w = acc.get(name, 0.0) / total
        if w <= 0.0:
            continue
        g.add([v.index], w * (chain_scale if name in chain else (1.0 - t)), 'REPLACE')
    sleeve_locked += 1
print("SLEEVE_LOCK %d verts bound toward arm-chain-only" % sleeve_locked)

# An armature whose bones ARE this project's own bones, at this project's own
# rest positions (character/fused_body.gd bone_origins()). glTF only carries
# skin weights when there is a skin to carry them, so without this the
# vertex groups above would be dropped on export and the garment would be a
# rigid prop again -- which is the whole thing the rig exists to avoid.
# Godot re-binds these by NAME onto the character's real skeleton, so only
# the names and the rest positions matter here, not the bone roll/tails.
#
# Positions are Godot-space (gx, gy, gz) converted to the Blender space the
# vertices are now in: Blender (gx, -gz, gy). Every rest joint sits at gz=0.
NECK_Y = NECK_Y_GAME

def lerp3(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t)

REST = {"hips": (0, HIP_Y, 0), "spine": (0, (HIP_Y + SHOULDER_Y) * 0.5, 0),
        "neck": (0, SHOULDER_Y, 0), "head": (0, NECK_Y, 0)}
for side, sx in (("l", -1.0), ("r", 1.0)):
    sh = (sx * SHOULDER_X, SHOULDER_Y, 0.0)
    hd = (sx * HAND_X, HAND_Y, 0.0)
    REST["arm_" + side] = sh
    REST["forearm_" + side] = lerp3(sh, hd, 0.5)     # ELBOW_T
    REST["hand_" + side] = lerp3(sh, hd, 0.85)       # WRIST_T
    hip = (sx * HIP_X, HIP_Y, 0.0)
    foot = (sx * HIP_X, FOOT_Y, 0.0)
    REST["thigh_" + side] = hip
    REST["shin_" + side] = lerp3(hip, foot, 0.5)     # KNEE_T
PARENT = {"spine": "hips", "neck": "spine", "head": "neck",
          "arm_l": "spine", "forearm_l": "arm_l", "hand_l": "forearm_l",
          "arm_r": "spine", "forearm_r": "arm_r", "hand_r": "forearm_r",
          "thigh_l": "hips", "shin_l": "thigh_l",
          "thigh_r": "hips", "shin_r": "thigh_r"}

arm_data = bpy.data.armatures.new("CloakRig")
arm_obj = bpy.data.objects.new("CloakRig", arm_data)
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

os.makedirs(os.path.dirname(dst), exist_ok=True)
for x in bpy.context.scene.objects:
    x.select_set(x in (obj, arm_obj))
bpy.context.view_layer.objects.active = obj
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB', use_selection=True,
                          export_yup=True, export_apply=False,
                          export_skins=True, export_all_influences=False)
print("EXPORTED", dst)

# --- render the finished garment -----------------------------------------
mat = bpy.data.materials.new("M"); mat.use_backface_culling = True; mat.use_nodes = True
b = mat.node_tree.nodes["Principled BSDF"]
b.inputs["Base Color"].default_value = (0.62, 0.62, 0.65, 1)
b.inputs["Roughness"].default_value = 0.7
me.materials.clear(); me.materials.append(mat)
world = bpy.data.worlds.new("W"); bpy.context.scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (1, 0, 0, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 1.3
sc = bpy.context.scene
sc.render.engine = 'BLENDER_EEVEE_NEXT'; sc.render.resolution_x = 800; sc.render.resolution_y = 1000
cd = bpy.data.cameras.new("C"); cd.type = 'ORTHO'
cam = bpy.data.objects.new("C", cd); sc.collection.objects.link(cam); sc.camera = cam
sun = bpy.data.objects.new("S", bpy.data.lights.new("S", type='SUN')); sun.data.energy = 3.5
sc.collection.objects.link(sun)
lo = Vector((1e9,)*3); hi = Vector((-1e9,)*3)
for v in me.vertices:
    for i in range(3):
        lo[i] = min(lo[i], v.co[i]); hi[i] = max(hi[i], v.co[i])
cen = (lo + hi) * 0.5; span = max(hi - lo)
os.makedirs(outdir, exist_ok=True)
for name, yaw, pitch, zoom, target in [
        ("final_front", 180, 0, span * 1.05, cen),
        ("final_back", 0, 0, span * 1.05, cen),
        ("final_side", 90, 0, span * 1.05, cen),
        ("final_hood", 180, 12, span * 0.34, Vector((cen.x, cen.y, hi.z - span * 0.13))),
        ("final_hem", 180, 12, span * 0.30, Vector((cen.x, cen.y, lo.z + span * 0.10)))]:
    y = math.radians(yaw); p = math.radians(pitch); d = span * 3.0
    dv = Vector((math.sin(y) * math.cos(p), -math.cos(y) * math.cos(p), math.sin(p)))
    cam.location = target + dv * d
    cam.rotation_euler = (math.radians(90 - pitch), 0, y)
    cd.ortho_scale = zoom
    sun.location = cam.location + Vector((0, 0, span))
    sun.rotation_euler = (math.radians(50 - pitch), 0, y)
    sc.render.filepath = os.path.join(outdir, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("RENDER", name)
