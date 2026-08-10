extends RefCounted
## Builds the torso+arms+legs as ONE fused, continuously-blended mesh (SDF
## capsules unioned via smooth-min, surface extracted via marching
## tetrahedra) instead of separate rigid pieces, per the "should look like
## one single unit, no square shoulders/joints" reference the character's
## body shape was built from. The head is NOT part of this -- it stays its
## own separate swappable mesh (cube/sphere/pyramid), built elsewhere in
## character.gd.
##
## Marching tetrahedra is used instead of marching cubes specifically
## because a tetrahedron's corner-sign configurations have no ambiguous
## cases (unlike marching cubes' 256-entry table), so there's much less
## surface area for a subtle transcription bug. Winding is auto-corrected
## per triangle against the scalar field's own local gradient (see
## _tet_gradient) rather than a hand-derived per-case formula -- an earlier
## attempt at hand-deriving the count==2 case's winding directly worked for
## one sample tetrahedron but did NOT generalize to the cube's other 5
## sub-tetrahedra shapes (verified: 37% wrong winding on a test sphere).
## The gradient of the field interpolated across a tet's own 4 corners
## points from inside toward outside by definition, so it's correct for
## any tetrahedron shape without needing case-by-case reasoning.
##
## Animation uses real (soft) skinning rather than rigidly moving each
## limb's whole vertex group: a vertex's bone weights are a softmin over
## its distance to each capsule segment (same k as the surface blend
## itself), so a rotated joint actually deforms the nearby geometry
## instead of tearing a rigid chunk away from the torso it was fused to at
## rest (verified: rigid one-bone weighting left a visible gap/floating
## stub at the shoulder when the arm rotated away from its rest angle).
##
## Skeleton3D bind-pose note (non-obvious, cost real debugging time): a
## bone's global_pose defaults to IDENTITY, not to whatever its "rest"
## transform was set to -- rest is apparently just a reference value, not
## folded into the pose used for skinning. So each bone's bind pose here
## is set to translate-by-minus-its-pivot (moving vertices into pivot-local
## space), and the pose's position is held CONSTANT at the pivot for the
## bone's entire lifetime -- only pose ROTATION ever changes. That
## composes to v_out = pivot + rotation*(v_in - pivot): rotate around a
## fixed pivot, using a plain rotation-only AnimationPlayer track, exactly
## like the old Node3D-pivot system's rotation:x tracks did.

const CUBE_CORNER_OFFSETS: Array[Vector3i] = [
	Vector3i(0, 0, 0), Vector3i(1, 0, 0), Vector3i(1, 1, 0), Vector3i(0, 1, 0),
	Vector3i(0, 0, 1), Vector3i(1, 0, 1), Vector3i(1, 1, 1), Vector3i(0, 1, 1),
]
const TET_DECOMPOSITION := [
	[0, 1, 2, 6], [0, 2, 3, 6], [0, 3, 7, 6], [0, 7, 4, 6], [0, 4, 5, 6], [0, 5, 1, 6],
]

const HIP_Y := 0.9
const SHOULDER_Y := 1.9
const LEG_R := 0.135
# The leg capsule's rounded bottom cap extends PAST this segment endpoint
# by the leg's own radius (that's what makes it rounded) -- so the actual
# lowest point of the rendered surface is FOOT_Y - LEG_R, not FOOT_Y
# itself. Setting FOOT_Y to exactly LEG_R makes that lowest point land at
# y=0 (the floor); leaving it at 0 put the true surface bottom at
# y=-LEG_R, sinking the feet into the floor by that amount.
const FOOT_Y := LEG_R
# Back to (near) its original reach -- shortening this to fix the
# hand-touching-hip problem made the arms read as stubby. The actual fix
# for that is horizontal clearance (HAND_X below), not vertical: a small
# safety margin above HIP_Y is enough once the hand is also pushed well
# out to the side.
const HAND_Y := 0.88
const HIP_X := 0.17
# The arm capsule's shoulder end sits close/overlapping the torso (fuses
# cleanly into a joint), but its HAND end sits notably further out
# (HAND_X > SHOULDER_X) so the capsule angles AWAY from the torso as it
# goes down. A vertical arm (constant X the whole way, as this originally
# was) stays close enough to the torso along its ENTIRE length for smin to
# fuse them the whole way down -- a "webbed/flying-squirrel" membrane
# instead of a normal armpit gap below the shoulder.
## Widened from 0.30 so the arms sit where the hooded cloak's sleeves
## already hang. The cloak (wearables/hooded_cloak.gd, baked by
## tools/meshwork/build_cloak.py) is a real modelled garment whose sleeves
## run 0.35 -> 0.51 out from centre; at 0.30 the arm started well inboard of
## its own sleeve and came out through the side partway down. The garment's
## hang is worth keeping, so the RIG moved to meet it rather than the sleeve
## being bent onto the rig.
##
## 0.35 is the largest value that doesn't open a gap at the armpit: the arm
## capsule's inner surface sits at SHOULDER_X - ARM_R = 0.26, exactly
## TORSO_R, so the two still touch and smin() still fuses them into a joint.
## Push it further and the arms detach from the torso for real -- fine under
## the cloak, but visible on a character not wearing one.
const SHOULDER_X := 0.35
# Pushed further out than before (was 0.44): with HAND_Y back down near
# hip height, horizontal clearance from the leg (leg surface reaches
# HIP_X + leg_r = 0.17 + 0.135 = 0.305) is the only thing keeping the hand
# from fusing with the hip -- needs a clear margin past that, accounting
# for BLEND_K's own blend radius on top.
const HAND_X := 0.58
# No taper at all -- straight line from armpit to hip, no waist-in curve.
# A single capsule (not two chained-and-smin'd halves, which this used to
# be) -- with the halves at identical radius there was nothing left to
# actually blend, and that redundant smin call turned out to be feeding a
# genuine double-surface defect (a small stray "flap" of duplicate,
# opposite-facing geometry) right where the shoulder blends in nearby.
# Collapsing it into one capsule removes both the redundancy and the bug.
const TORSO_R := 0.26
# Blend radius for smin -- must stay well below the thinnest blended
# feature's own radius (the arm, 0.09) or it dissolves the whole limb into
# whatever it's blending with instead of just rounding the joint (found by
# testing: k=0.11 fully absorbed the arms and merged both legs into one
# blob; k=0.06 keeps distinct limbs with a smooth, gap-free joint).
const BLEND_K := 0.06

const ARM_R := 0.09
# Where along the shoulder->hand line the elbow sits at rest (0=shoulder,
# 1=hand). Splits the arm limb into an upper-arm and forearm capsule/bone
# pair so the elbow can actually flex -- previously a single rigid arm bone
# could only pivot at the shoulder, and every attempt to fake a bend by
# rotating that one segment sideways either flared outward or swung the
# whole arm up like a stiff pole instead of folding it in.
const ELBOW_T := 0.5
# Where the wrist splits the forearm from the hand (0=shoulder, 1=hand, same
# axis ELBOW_T uses) -- the hand is the last 15% of the shoulder->hand line,
# giving it a real (if short) capsule of its own rather than being folded
# into the forearm's own single-DOF hinge.
const WRIST_T := 0.85
# Where the knee splits the leg limb into thigh/shin -- same convention as
# ELBOW_T, just for the leg's own hip->foot line.
const KNEE_T := 0.5
# Where the spine splits the torso limb into hips/chest -- same convention.
const SPINE_T := 0.5
# The neck is genuinely NEW geometry (previously the head just floated as a
# separate, non-skinned mesh with no connecting capsule at all -- see
# character.gd's own history) rather than a split of something that already
# existed, so it gets its own length/radius constants instead of a T-value
# along an existing line. Thin -- between the arm's own ARM_R and the leg's
# own LEG_R.
#
# Effectively zero length (not exactly 0 -- sdf_capsule's own distance
# formula divides by this segment's squared length, clamped to a tiny
# epsilon floor rather than guarded against outright, so a LITERAL 0 here
# would be dividing by that floor instead of a real length; a small non-zero
# value sidesteps needing to know whether that's actually safe). The head's
## own visual height (see character.gd's _define_parts, which offsets the
# head mesh up from this bone's own pivot by half the head's height) still
# depends on this, so it's kept just barely non-zero specifically so the
# head lands back at its original pre-neck height -- a real (if currently
# almost invisible) neck joint for future animations to rotate, without
# visibly taller proportions than before.
const NECK_R := 0.10
const NECK_LEN := 0.001
const NECK_Y := SHOULDER_Y + NECK_LEN

const BONES := [
	"hips", "spine", "neck", "head",
	"arm_l", "forearm_l", "hand_l",
	"arm_r", "forearm_r", "hand_r",
	"thigh_l", "shin_l",
	"thigh_r", "shin_r",
]
# Child -> parent bone name, for every bone but the root ("hips"). A real
# chain now (hips -> spine -> shoulders/neck, hips -> thighs -> shins) --
# earlier versions of this rig had only forearms as real children (skinning
# via soft distance-weighting never NEEDED a parent chain), but a real
# hierarchy is what lets ordinary forward-kinematic posing (a shoulder
# rotation actually carrying its own forearm/hand along with it) work the
# way any standard animation tool or motion file already expects.
const BONE_PARENT := {
	"spine": "hips",
	"neck": "spine",
	"head": "neck",
	"arm_l": "spine", "forearm_l": "arm_l", "hand_l": "forearm_l",
	"arm_r": "spine", "forearm_r": "arm_r", "hand_r": "forearm_r",
	"thigh_l": "hips", "shin_l": "thigh_l",
	"thigh_r": "hips", "shin_r": "thigh_r",
}
# The subset of BONES that actually own a row in the paint atlas -- matches
## PART_NAMES (minus "head") in character.gd. A bone that's a colinear
# sub-split of another paint bone's own limb (spine of hips, hand of arm,
# shin of thigh) shares that parent's row/UV axis (see PAINT_BONE_FOR)
# rather than getting its own, so paint stays continuous across the split
# instead of seaming there. "head" has no capsule at all (see bone_origins)
# so it was never eligible for a row here regardless.
const PAINT_BONES := ["hips", "neck", "arm_l", "arm_r", "thigh_l", "thigh_r"]
const PAINT_BONE_FOR := {
	"spine": "hips",
	"forearm_l": "arm_l", "hand_l": "arm_l",
	"forearm_r": "arm_r", "hand_r": "arm_r",
	"shin_l": "thigh_l",
	"shin_r": "thigh_r",
}

const CELL_SIZE := 0.045
# Must fully contain the rounded caps at the extremes of every capsule, not
# just the segment endpoints themselves -- a capsule's surface extends PAST
# its endpoint by its own radius (the rounded cap). FOOT_Y is already set
# so the leg's bottom cap lands exactly at y=0 (see FOOT_Y's own comment),
# so bounds only need a small margin below 0, not a large one -- but the
# shoulder/hand end still needs real headroom (the small negative-y margin
# here was previously large enough to hide a genuine clipping bug, so it's
# kept modest rather than exact, in case proportions change again later).
# Y max raised (2.25 -> 2.35) for the neck's own rounded cap tip, which
# extends past NECK_Y by NECK_R (2.10 + 0.10 = 2.20) -- same modest-margin
# style as the rest of this comment already established.
const BOUNDS_MIN := Vector3(-0.75, -0.05, -0.35)
const BOUNDS_MAX := Vector3(0.75, 2.35, 0.35)

static func sdf_capsule(p: Vector3, a: Vector3, b: Vector3, r: float) -> float:
	var ab := b - a
	var t := clampf((p - a).dot(ab) / maxf(ab.length_squared(), 0.0000001), 0.0, 1.0)
	return p.distance_to(a + ab * t) - r

static func smin(a: float, b: float, k: float) -> float:
	if k <= 0.0:
		return minf(a, b)
	var h := clampf(0.5 + 0.5 * (b - a) / k, 0.0, 1.0)
	return lerpf(b, a, h) - k * h * (1.0 - h)

## One entry per LIMB -- a single straight protrusion (e.g. the whole arm,
## shoulder to hand) that sdf_body() always draws as ONE capsule, regardless
## of how many BONES that limb gets split into for skinning (see
## LIMB_BONES). This is the single source of truth both sdf_body() (the
## mesh's actual SHAPE) and segment_defs()/bone_origins()/_uv_axis() (the
## SKELETON) now derive from -- previously each hardcoded the same capsule
## geometry independently with no shared source, which is exactly the kind
## of duplication that lets a change to one silently drift out of sync with
## the other.
## Limb dictionary KEYS ("torso"/"leg_l"/"leg_r") are internal lookup names
## into LIMB_BONES only -- they no longer need to match any actual BONE name
## now that torso/leg_l/leg_r have each been split into two real bones (this
## is exactly the same relationship "arm_l" the limb key already had to
## "arm_l"+"forearm_l" the bone names, just made explicit for the other
## limbs too instead of staying a 1-bone-per-limb coincidence).
static func limb_defs() -> Array:
	var shoulder_l := Vector3(-SHOULDER_X, SHOULDER_Y, 0)
	var hand_l := Vector3(-HAND_X, HAND_Y, 0)
	var shoulder_r := Vector3(SHOULDER_X, SHOULDER_Y, 0)
	var hand_r := Vector3(HAND_X, HAND_Y, 0)
	return [
		{"limb": "torso", "a": Vector3(0, HIP_Y, 0), "b": Vector3(0, SHOULDER_Y, 0), "r": TORSO_R},
		{"limb": "arm_l", "a": shoulder_l, "b": hand_l, "r": ARM_R},
		{"limb": "arm_r", "a": shoulder_r, "b": hand_r, "r": ARM_R},
		{"limb": "leg_l", "a": Vector3(-HIP_X, HIP_Y, 0), "b": Vector3(-HIP_X, FOOT_Y, 0), "r": LEG_R},
		{"limb": "leg_r", "a": Vector3(HIP_X, HIP_Y, 0), "b": Vector3(HIP_X, FOOT_Y, 0), "r": LEG_R},
		# Appended LAST, after the original 5 -- keeps the smin() blend chain
		# order for the proven prefix bit-for-bit identical to before; only
		# ever adds on top of it.
		{"limb": "neck", "a": Vector3(0, SHOULDER_Y, 0), "b": Vector3(0, NECK_Y, 0), "r": NECK_R},
	]

## Which BONES a limb splits into for skinning/animation purposes -- each
## entry is [bone_name, t0, t1] along the limb's own a->b line (0=a, 1=b).
## Splitting a limb here NEVER changes sdf_body()'s own shape (see
## limb_defs's own comment: two colinear, same-radius, endpoint-sharing
## capsules union to an identical SDF as one capsule spanning both) -- it
## only changes where segment_defs()/bone_origins() put a joint.
const LIMB_BONES := {
	"torso": [["hips", 0.0, SPINE_T], ["spine", SPINE_T, 1.0]],
	"neck": [["neck", 0.0, 1.0]],
	"arm_l": [["arm_l", 0.0, ELBOW_T], ["forearm_l", ELBOW_T, WRIST_T], ["hand_l", WRIST_T, 1.0]],
	"arm_r": [["arm_r", 0.0, ELBOW_T], ["forearm_r", ELBOW_T, WRIST_T], ["hand_r", WRIST_T, 1.0]],
	"leg_l": [["thigh_l", 0.0, KNEE_T], ["shin_l", KNEE_T, 1.0]],
	"leg_r": [["thigh_r", 0.0, KNEE_T], ["shin_r", KNEE_T, 1.0]],
}

static func sdf_body(p: Vector3) -> float:
	var d := 0.0
	var first := true
	for limb in limb_defs():
		var ld: float = sdf_capsule(p, limb["a"], limb["b"], limb["r"])
		if first:
			d = ld
			first = false
		else:
			d = smin(d, ld, BLEND_K)
	return d

## Every bone segment, generated by walking limb_defs() x LIMB_BONES and
## lerping each bone's own [t0,t1] span along its limb's a->b line.
static func segment_defs() -> Array:
	var out := []
	for limb in limb_defs():
		var a: Vector3 = limb["a"]
		var b: Vector3 = limb["b"]
		for bone_entry in LIMB_BONES[limb["limb"]]:
			var bone_name: String = bone_entry[0]
			var t0: float = bone_entry[1]
			var t1: float = bone_entry[2]
			out.append({"a": a.lerp(b, t0), "b": a.lerp(b, t1), "r": limb["r"], "bone": bone_name})
	return out

## PAINT_BONES (character-facing, "hips"/"thigh_l"/...) -> limb_defs() key
## (internal, "torso"/"leg_l"/...) -- these diverged once torso/leg_l/leg_r
## each split into two real bones with new names, so a paint bone can no
## longer double as its own limb key the way arm_l/arm_r still do.
const PAINT_BONE_TO_LIMB := {
	"hips": "torso", "neck": "neck", "arm_l": "arm_l", "arm_r": "arm_r",
	"thigh_l": "leg_l", "thigh_r": "leg_r",
}

## Whole-LIMB axis per paint bone, used only for UV parameterization -- spans
## the limb's FULL range (ignoring any internal bone split, e.g. the elbow)
## so the paint UV stays continuous across that internal seam. bone_name is
## a PAINT bone (see character.gd's PAINT_BONE_FOR, which redirects any
## sub-bone like forearm_l to its own limb's paint bone before this is ever
## called) -- mapped to its limb via PAINT_BONE_TO_LIMB.
static func _uv_axis(bone_name: String) -> Dictionary:
	var limb_name: String = PAINT_BONE_TO_LIMB.get(bone_name, bone_name)
	for limb in limb_defs():
		if limb["limb"] == limb_name:
			return {"a": limb["a"], "b": limb["b"]}
	push_error("_uv_axis: no limb for paint bone " + bone_name)
	return {"a": Vector3.ZERO, "b": Vector3.ZERO}

## Bone pivots -- the joint each bone rotates around: hip/shoulder for a
## limb's own root bone, and each subsequent bone's own [t0] split point
## (the elbow, for a forearm) for the rest. "head" is the one exception --
## it has no capsule/segment of its own (see segment_defs, which never
## produces a "head" entry) so it isn't reachable through any limb's own
## LIMB_BONES table; its pivot is simply the neck's own top end, i.e. where
## a real neck joint would actually be.
static func bone_origins() -> Dictionary:
	var out := {}
	for limb in limb_defs():
		var a: Vector3 = limb["a"]
		var b: Vector3 = limb["b"]
		for bone_entry in LIMB_BONES[limb["limb"]]:
			var bone_name: String = bone_entry[0]
			var t0: float = bone_entry[1]
			out[bone_name] = a.lerp(b, t0)
	out["head"] = Vector3(0, NECK_Y, 0)
	return out

static func _edge_point(pa: Vector3, va: float, pb: Vector3, vb: float) -> Vector3:
	var t: float = va / (va - vb)
	return pa.lerp(pb, t)

## Two adjacent grid cells that share an edge each compute that edge's
## surface-crossing point independently -- and `pa.lerp(pb, t)` vs
## `pb.lerp(pa, 1-t)` are mathematically identical but NOT bit-identical in
## floating point, since one cell might reference the shared corners in
## the opposite order from its neighbor. That mismatch leaves a hairline
## crack (the two triangles meeting at that edge don't actually share a
## vertex), which read as a fine grid-spaced pattern of see-through lines
## across the whole body. Fixed by caching each edge's point keyed by a
## CANONICAL (sorted) pair of grid coordinates, always computed in the same
## corner order regardless of which cell/tetrahedron asks first -- so the
## two cells sharing this edge get the exact same cached Vector3.
static func _edge_point_cached(pa: Vector3, va: float, ka: Vector3i, pb: Vector3, vb: float, kb: Vector3i, cache: Dictionary) -> Vector3:
	var swap := _grid_key_less(kb, ka)
	var key := PackedInt32Array([ka.x, ka.y, ka.z, kb.x, kb.y, kb.z]) if not swap else PackedInt32Array([kb.x, kb.y, kb.z, ka.x, ka.y, ka.z])
	var cache_key: String = str(key)
	if cache.has(cache_key):
		return cache[cache_key]
	var result: Vector3 = _edge_point(pb, vb, pa, va) if swap else _edge_point(pa, va, pb, vb)
	cache[cache_key] = result
	return result

static func _grid_key_less(a: Vector3i, b: Vector3i) -> bool:
	if a.x != b.x:
		return a.x < b.x
	if a.y != b.y:
		return a.y < b.y
	return a.z < b.z

static func _tet_gradient(p: Array, v: Array) -> Vector3:
	var e1: Vector3 = p[1] - p[0]
	var e2: Vector3 = p[2] - p[0]
	var e3: Vector3 = p[3] - p[0]
	var d := Vector3(v[1] - v[0], v[2] - v[0], v[3] - v[0])
	var denom: float = e1.dot(e2.cross(e3))
	if absf(denom) < 0.0000001:
		return Vector3.ZERO
	return (e2.cross(e3) * d.x + e3.cross(e1) * d.y + e1.cross(e2) * d.z) / denom

static func _process_tet(p: Array, v: Array, k: Array, out: PackedVector3Array, edge_cache: Dictionary) -> void:
	var inside := []
	for val in v:
		inside.append(val < 0.0)
	var count := 0
	for b in inside:
		if b:
			count += 1
	if count == 0 or count == 4:
		return

	var gradient := _tet_gradient(p, v)

	var tris := []
	if count == 1 or count == 3:
		var solo := -1
		for i in range(4):
			if inside[i] == (count == 1):
				solo = i
				break
		var others := []
		for i in range(4):
			if i != solo:
				others.append(i)
		var e0 := _edge_point_cached(p[solo], v[solo], k[solo], p[others[0]], v[others[0]], k[others[0]], edge_cache)
		var e1 := _edge_point_cached(p[solo], v[solo], k[solo], p[others[1]], v[others[1]], k[others[1]], edge_cache)
		var e2 := _edge_point_cached(p[solo], v[solo], k[solo], p[others[2]], v[others[2]], k[others[2]], edge_cache)
		tris.append([e0, e1, e2])
	else:
		var ins := []
		var outs := []
		for i in range(4):
			if inside[i]:
				ins.append(i)
			else:
				outs.append(i)
		var q0 := _edge_point_cached(p[ins[0]], v[ins[0]], k[ins[0]], p[outs[0]], v[outs[0]], k[outs[0]], edge_cache)
		var q1 := _edge_point_cached(p[ins[0]], v[ins[0]], k[ins[0]], p[outs[1]], v[outs[1]], k[outs[1]], edge_cache)
		var q2 := _edge_point_cached(p[ins[1]], v[ins[1]], k[ins[1]], p[outs[1]], v[outs[1]], k[outs[1]], edge_cache)
		var q3 := _edge_point_cached(p[ins[1]], v[ins[1]], k[ins[1]], p[outs[0]], v[outs[0]], k[outs[0]], edge_cache)
		tris.append([q0, q1, q2])
		tris.append([q0, q2, q3])

	for tri in tris:
		var a: Vector3 = tri[0]
		var b: Vector3 = tri[1]
		var c: Vector3 = tri[2]
		var geo_normal := (b - a).cross(c - a)
		if geo_normal.length_squared() > 0.0000001 and gradient.length_squared() > 0.0000001:
			# Godot's actual front-face convention (empirically verified
			# with a plain two-triangle test scene, opposite of the naive
			## assumption): a triangle renders visible when its winding-
			# implied geo_normal points AWAY from the outward/gradient
			# direction, i.e. geo_normal.dot(gradient) must end up
			# NEGATIVE. This condition had the sign backward for this
			# whole marching-tetrahedra module (every other shape
			# generator in this project -- box/sphere/pyramid/capsule --
			# already used the correct sign); it silently produced a mesh
			# that mostly still looked right (a fully closed, globally
			# self-consistent shell renders the same whichever absolute
			# side is chosen), but let isolated components land on the
			# WRONG absolute side relative to their neighbors, which is
			# what caused paint to silently apply to a triangle that
			# turned out to be invisible from the very angle it was
			# clicked from.
			if geo_normal.dot(gradient) > 0.0:
				var tmp := b
				b = tri[2]
				c = tmp
		out.append(a); out.append(b); out.append(c)

## Hand-deriving winding purely per-tetrahedron (the gradient check in
## _process_tet) turned out to be necessary but NOT sufficient: two
## triangles from DIFFERENT tets that legitimately share an edge can each
## independently pass their own local gradient check yet still traverse
## that shared edge in the SAME direction instead of opposite (verified:
## even a single unblended capsule, no smin involved at all, showed ~12%
## of directed edges with no matching reverse partner) -- a real
## topological inconsistency, not a floating-point crack, and it shows up
## as backface-culled gaps (holes letting the background through) rather
## than a shading artifact. Fixed the robust way instead of re-deriving
## the tetrahedra case logic a third time: propagate a consistent
## orientation across the whole mesh via shared-edge adjacency (any two
## triangles sharing an edge must traverse it in opposite directions),
## then flip the WHOLE result once at the end if that consistent
## orientation turned out to be inside-out, checked against the field's
## own gradient.
static func _make_orientation_consistent(tris: PackedVector3Array, sdf_func: Callable) -> PackedVector3Array:
	var num_tris := tris.size() / 3
	if num_tris == 0:
		return tris

	var edge_map := {}
	for ti in range(num_tris):
		for e in range(3):
			var a: Vector3 = tris[ti * 3 + e]
			var b: Vector3 = tris[ti * 3 + (e + 1) % 3]
			var key: String = str(a) + "|" + str(b) if str(a) < str(b) else str(b) + "|" + str(a)
			if not edge_map.has(key):
				edge_map[key] = []
			edge_map[key].append({"tri": ti, "a": a, "b": b})

	var flip := {}
	var component := {}
	var visited := {}
	var num_components := 0
	for start in range(num_tris):
		if visited.has(start):
			continue
		var this_component := num_components
		num_components += 1
		flip[start] = false
		component[start] = this_component
		visited[start] = true
		var queue := [start]
		while not queue.is_empty():
			var cur: int = queue.pop_front()
			var cur_flip: bool = flip[cur]
			for e in range(3):
				var a: Vector3 = tris[cur * 3 + e]
				var b: Vector3 = tris[cur * 3 + (e + 1) % 3]
				if cur_flip:
					var tmp := a; a = b; b = tmp
				var key: String = str(a) + "|" + str(b) if str(a) < str(b) else str(b) + "|" + str(a)
				for entry in edge_map[key]:
					var other_ti: int = entry["tri"]
					if other_ti == cur or visited.has(other_ti):
						continue
					var other_a: Vector3 = entry["a"]
					var other_b: Vector3 = entry["b"]
					# Consistent orientation needs the neighbor to traverse
					# this shared edge as b->a (opposite of this
					# triangle's effective a->b). Figure out whether the
					# neighbor needs flipping to achieve that.
					var other_flip: bool = not (other_a.is_equal_approx(b) and other_b.is_equal_approx(a))
					visited[other_ti] = true
					flip[other_ti] = other_flip
					component[other_ti] = this_component
					queue.append(other_ti)

	var out := PackedVector3Array()
	out.resize(tris.size())
	for ti in range(num_tris):
		var a: Vector3 = tris[ti * 3]
		var b: Vector3 = tris[ti * 3 + 1]
		var c: Vector3 = tris[ti * 3 + 2]
		if flip.get(ti, false):
			var tmp := b; b = c; c = tmp
		out[ti * 3] = a
		out[ti * 3 + 1] = b
		out[ti * 3 + 2] = c

	# Now consistent WITHIN each connected component -- but two different
	# components could each independently be internally consistent while
	# disagreeing with each other about which way is "outward" (this was
	# the actual bug behind a severe localized inside-out patch: a single
	# GLOBAL vote/flip decision, dominated by triangle count from the
	# large main surface, could flip a smaller separate component the
	# WRONG way). So the final "is this inside-out" check and flip is done
	# PER COMPONENT, using only that component's own triangles.
	var eps := 0.001
	var component_votes := {}
	for ti in range(num_tris):
		var comp: int = component[ti]
		if not component_votes.has(comp):
			component_votes[comp] = 0
		var a: Vector3 = out[ti * 3]
		var b: Vector3 = out[ti * 3 + 1]
		var c: Vector3 = out[ti * 3 + 2]
		var geo_normal := (b - a).cross(c - a)
		if geo_normal.length_squared() < 0.0000001:
			continue
		var centroid := (a + b + c) / 3.0
		var gx: float = sdf_func.call(centroid + Vector3(eps, 0, 0)) - sdf_func.call(centroid - Vector3(eps, 0, 0))
		var gy: float = sdf_func.call(centroid + Vector3(0, eps, 0)) - sdf_func.call(centroid - Vector3(0, eps, 0))
		var gz: float = sdf_func.call(centroid + Vector3(0, 0, eps)) - sdf_func.call(centroid - Vector3(0, 0, eps))
		var grad := Vector3(gx, gy, gz)
		if grad.length_squared() < 0.0000001:
			continue
		# Same corrected sign as _process_tet: visible/correct means
		# geo_normal.dot(grad) is NEGATIVE, not positive.
		if geo_normal.dot(grad) < 0.0:
			component_votes[comp] += 1
		else:
			component_votes[comp] -= 1

	var final := PackedVector3Array()
	final.resize(out.size())
	for ti in range(num_tris):
		var comp: int = component[ti]
		var needs_flip: bool = component_votes.get(comp, 0) < 0
		var a: Vector3 = out[ti * 3]
		var b: Vector3 = out[ti * 3 + 1]
		var c: Vector3 = out[ti * 3 + 2]
		if needs_flip:
			var tmp := b; b = c; c = tmp
		final[ti * 3] = a
		final[ti * 3 + 1] = b
		final[ti * 3 + 2] = c

	return final

## Raw grid-sample-and-tetrahedralize pass, before any of the cleanup
## steps (weld/orientation/flap-removal/hole-patching) run.
static func _extract_raw(sdf_func: Callable, bounds_min: Vector3, bounds_max: Vector3, cell_size: float) -> PackedVector3Array:
	var nx := int(ceil((bounds_max.x - bounds_min.x) / cell_size))
	var ny := int(ceil((bounds_max.y - bounds_min.y) / cell_size))
	var nz := int(ceil((bounds_max.z - bounds_min.z) / cell_size))

	var value_cache := {}
	var edge_cache := {}
	var out := PackedVector3Array()

	for cx in range(nx):
		for cy in range(ny):
			for cz in range(nz):
				var corner_pos := []
				var corner_val := []
				var corner_key: Array[Vector3i] = []
				for offset: Vector3i in CUBE_CORNER_OFFSETS:
					var gx: int = cx + offset.x
					var gy: int = cy + offset.y
					var gz: int = cz + offset.z
					var key := Vector3i(gx, gy, gz)
					var val: float
					if value_cache.has(key):
						val = value_cache[key]
					else:
						val = sdf_func.call(bounds_min + Vector3(gx, gy, gz) * cell_size)
						# A grid corner landing at (or extremely near) exactly
						# zero makes "inside = val < 0.0" an unstable,
						# knife-edge classification -- tets sharing that
						# corner can disagree on which side it's on due to
						# float noise, breaking the local topology around it
						# (this is what was causing the residual hairline
						# crack right at a capsule's rounded cap tip, a
						# genuine curvature singularity where several grid
						# samples land very close to the surface). Nudging
						# any near-zero value a small fixed amount away from
						# zero makes the classification deterministic.
						if absf(val) < 0.0001:
							val = 0.0001 if val >= 0.0 else -0.0001
						value_cache[key] = val
					corner_pos.append(bounds_min + Vector3(gx, gy, gz) * cell_size)
					corner_val.append(val)
					corner_key.append(key)

				for tet in TET_DECOMPOSITION:
					var tp := [corner_pos[tet[0]], corner_pos[tet[1]], corner_pos[tet[2]], corner_pos[tet[3]]]
					var tv := [corner_val[tet[0]], corner_val[tet[1]], corner_val[tet[2]], corner_val[tet[3]]]
					var tk := [corner_key[tet[0]], corner_key[tet[1]], corner_key[tet[2]], corner_key[tet[3]]]
					_process_tet(tp, tv, tk, out, edge_cache)

	return out

static func extract_mesh(sdf_func: Callable, bounds_min: Vector3, bounds_max: Vector3, cell_size: float) -> PackedVector3Array:
	var raw := _extract_raw(sdf_func, bounds_min, bounds_max, cell_size)
	var welded := _weld_near_duplicate_vertices(raw)
	var consistent := _make_orientation_consistent(welded, sdf_func)
	var deflapped := _remove_duplicate_flaps(consistent)
	return _fill_holes(deflapped)

static func _directed_edge_key(a: Vector3, b: Vector3) -> String:
	return str(a) + "->" + str(b)

## Marching tetrahedra can, in rare tet configurations, leave a small hole
## in the surface -- an edge used by only one triangle instead of the two
## a closed, watertight mesh needs. Confirmed this is NOT a resolution
## problem (a specific reported armpit hole was checked against cell sizes
## from the normal 0.045 down to 0.02 -- more than 5x the triangle count,
## 6x the build time -- and the exact same hole was still there; it even
## briefly "fixed itself" at two resolutions in between before reopening,
## which is what a coincidental grid-alignment artifact looks like, not a
## real fix), so chasing a smaller CELL_SIZE is both expensive and
## unreliable. Same approach this file already takes for the other
## fragile per-configuration defect class it has (see
## _remove_duplicate_flaps's own reasoning): detect the actual topological
## symptom directly and patch it, instead of hunting the exact geometric
## conditions that avoid it.
static func _fill_holes(tris: PackedVector3Array) -> PackedVector3Array:
	var num_tris := tris.size() / 3
	if num_tris == 0:
		return tris

	# This mesh is already orientation-consistent (see
	# _make_orientation_consistent, which ran before this) -- every proper
	# edge is used by exactly two triangles, once in each direction (a->b
	# by one, b->a by its neighbor across that edge). An edge used a->b
	# with no matching b->a anywhere is a genuine boundary/hole edge.
	var directed_count := {}
	for ti in range(num_tris):
		for e in range(3):
			var a: Vector3 = tris[ti * 3 + e]
			var b: Vector3 = tris[ti * 3 + (e + 1) % 3]
			var key := _directed_edge_key(a, b)
			directed_count[key] = directed_count.get(key, 0) + 1

	# next_boundary[a's key] = the b's boundary edges lead to from a --
	# walking these traces a hole's own loop in the SAME direction its
	# (one-sided) existing triangles already use, so the fan triangles
	# below reverse each step, facing the opposite (correct, outward) way.
	var next_boundary := {}
	var boundary_edges := []
	for ti in range(num_tris):
		for e in range(3):
			var a: Vector3 = tris[ti * 3 + e]
			var b: Vector3 = tris[ti * 3 + (e + 1) % 3]
			if directed_count.has(_directed_edge_key(b, a)):
				continue
			var a_key := str(a)
			if not next_boundary.has(a_key):
				next_boundary[a_key] = []
			next_boundary[a_key].append(b)
			boundary_edges.append([a, b])

	if boundary_edges.is_empty():
		return tris

	var visited := {}
	var out := tris.duplicate()
	for start_pair in boundary_edges:
		var start_a: Vector3 = start_pair[0]
		var start_b: Vector3 = start_pair[1]
		var start_step_key := str(start_a) + "=>" + str(start_b)
		if visited.has(start_step_key):
			continue

		var loop: Array = [start_a]
		var current: Vector3 = start_b
		visited[start_step_key] = true
		var closed := false
		# A real hole here is always small (a handful of edges) -- this cap
		# just stops a malformed/non-loop boundary graph from spinning
		# forever instead of being left unpatched.
		for _guard in range(256):
			if current.is_equal_approx(start_a):
				closed = true
				break
			var current_key := str(current)
			if not next_boundary.has(current_key):
				break
			var advanced := false
			for next_v in next_boundary[current_key]:
				var step_key := current_key + "=>" + str(next_v)
				if visited.has(step_key):
					continue
				visited[step_key] = true
				loop.append(current)
				current = next_v
				advanced = true
				break
			if not advanced:
				break

		if not closed or loop.size() < 3:
			continue # couldn't cleanly close this one -- leave it rather than risk patching it wrong

		var centroid := Vector3.ZERO
		for v in loop:
			centroid += v
		centroid /= loop.size()

		# Each fan triangle MUST use v1->v0 (not v0->v1) at its base edge --
		# that's the one direction that pairs with the existing, one-sided
		# triangle already using v0->v1, which is what actually closes the
		# hole. Flipping a triangle to "fix" its winding reverses ALL three
		# of its directed edges at once, including this one -- tried
		# checking each fan triangle's normal against the field's own
		# gradient and flipping when it disagreed (matching how every other
		# triangle in this file gets its winding verified), and that
		# silently broke the v1->v0 pairing on whichever triangles it
		# flipped, reopening holes instead of closing them (verified:
		# boundary-edge count went from 0 back up to 16). A handful of fan
		# triangles ending up normal-backwards on a non-convex loop is a
		# far smaller defect than that -- confirmed by direct inspection
		# (screenshots from 3 different angles) that it isn't visible at
		# all -- so this intentionally does NOT do a per-triangle winding
		# check; watertight-but-occasionally-backward beats correctly-wound
		# but full of holes again.
		for i in range(loop.size()):
			var v0: Vector3 = loop[i]
			var v1: Vector3 = loop[(i + 1) % loop.size()]
			out.append(centroid); out.append(v1); out.append(v0)

	return out

## Sequential smin-chaining of several capsules can, in a tight junction
## where more than one blend interacts (verified: the arm-to-torso
## shoulder blend), produce a genuine small extra fold of surface -- a
## thin "flap" sitting almost exactly on top of the real surface but
## facing the opposite way. It's not a floating-point crack (the two
## sheets are clearly separate, deliberately-generated surface, not a
## sliver gap) and not an orientation-propagation bug (each sheet is
## internally consistent, they're just two genuinely separate surfaces
## occupying nearly the same space) -- confirmed by nudging SHOULDER_X and
## watching the defect's location and triangle count change accordingly,
## rather than staying fixed to particular grid cells. Rather than
## chasing the exact geometric conditions that avoid it (fragile -- any
## future proportion tweak could reintroduce it, in a different spot),
## detect and strip out these near-coincident opposite-facing pairs
## directly. This is what was causing painting to silently land on a
## hidden "inner" duplicate surface instead of the visible outer one: the
## paint raycast has no backface culling and no preference for "the real
## one," so whichever of the two nearly-coincident triangles was
## marginally closer along the ray won, unpredictably.
static func _remove_duplicate_flaps(tris: PackedVector3Array) -> PackedVector3Array:
	var num_tris := tris.size() / 3
	if num_tris == 0:
		return tris

	const DIST_THRESHOLD := 0.025
	const DOT_THRESHOLD := -0.4
	var cell_size := DIST_THRESHOLD * 1.5

	var centroids := PackedVector3Array()
	var normals := PackedVector3Array()
	var buckets := {}
	for i in range(num_tris):
		var a: Vector3 = tris[i * 3]
		var b: Vector3 = tris[i * 3 + 1]
		var c: Vector3 = tris[i * 3 + 2]
		var centroid := (a + b + c) / 3.0
		var n := (b - a).cross(c - a)
		centroids.append(centroid)
		normals.append(n.normalized() if n.length_squared() > 0.0000001 else Vector3.ZERO)
		var key := Vector3i(roundi(centroid.x / cell_size), roundi(centroid.y / cell_size), roundi(centroid.z / cell_size))
		if not buckets.has(key):
			buckets[key] = []
		buckets[key].append(i)

	var removed := {}
	for i in range(num_tris):
		if removed.has(i):
			continue
		var c: Vector3 = centroids[i]
		var n: Vector3 = normals[i]
		if n == Vector3.ZERO:
			continue
		var base_key := Vector3i(roundi(c.x / cell_size), roundi(c.y / cell_size), roundi(c.z / cell_size))
		for dx in range(-1, 2):
			for dy in range(-1, 2):
				for dz in range(-1, 2):
					var key := base_key + Vector3i(dx, dy, dz)
					if not buckets.has(key):
						continue
					for j in buckets[key]:
						if j <= i or removed.has(j):
							continue
						var on: Vector3 = normals[j]
						if on == Vector3.ZERO:
							continue
						if c.distance_to(centroids[j]) < DIST_THRESHOLD and n.dot(on) < DOT_THRESHOLD:
							removed[i] = true
							removed[j] = true

	if removed.is_empty():
		return tris

	var out := PackedVector3Array()
	for i in range(num_tris):
		if removed.has(i):
			continue
		out.append(tris[i * 3])
		out.append(tris[i * 3 + 1])
		out.append(tris[i * 3 + 2])
	return out

## Snaps vertices that are extremely close (but not bit-identical) to a
## single shared position. The grid-key edge cache handles the common case
## of two cells sharing a grid EDGE, but a capsule's rounded cap tip is a
## genuine curvature singularity where several *different* edges (from
## different, non-adjacent tets) can independently interpolate to
## near-identical -- but not bit-identical -- points, leaving a hairline
## gap the edge cache doesn't catch. Rounding to a fixed precision (well
## below the mesh's own feature size) unifies those into one exact vertex.
static func _weld_near_duplicate_vertices(tris: PackedVector3Array) -> PackedVector3Array:
	const PRECISION := 100000.0
	var canonical := {}
	var out := PackedVector3Array()
	out.resize(tris.size())
	for i in range(tris.size()):
		var v: Vector3 = tris[i]
		var key := Vector3i(roundi(v.x * PRECISION), roundi(v.y * PRECISION), roundi(v.z * PRECISION))
		if not canonical.has(key):
			canonical[key] = v
		out[i] = canonical[key]
	return out

static func closest_bone(p: Vector3, segments: Array) -> String:
	var best_d := INF
	var best_bone := ""
	for seg in segments:
		var d: float = sdf_capsule(p, seg["a"], seg["b"], seg["r"])
		if d < best_d:
			best_d = d
			best_bone = seg["bone"]
	return best_bone

static func build_skeleton() -> Skeleton3D:
	var skeleton := Skeleton3D.new()
	for bone_name in BONES:
		skeleton.add_bone(bone_name)
	for bone_name in BONE_PARENT:
		skeleton.set_bone_parent(skeleton.find_bone(bone_name), skeleton.find_bone(BONE_PARENT[bone_name]))
	return skeleton

## --- Stub body (worn under a full-length garment) ---------------------
##
## A cloak can't stop the body poking through it, because the body is a
## single fused surface and the garment is rigid cloth over the top of it --
## any pose that swings a limb wider than the cloth eventually shows skin.
## The fix that actually holds is to have no body there to poke through:
## while a covering garment is worn, character.gd swaps the fused mesh for
## this filtered copy, which keeps ONLY the parts that show anyway -- the
## hands, the feet, and a collar under the chin.
##
## TUNE THESE to change how much sticks out. The two limb ones are a
## fraction along that limb's own rest line, 0 at the body end and 1 at the
## tip, so a SMALLER number means MORE limb visible:
##   STUB_HAND_T  0.62 -> forearm + hand, from about y=1.27 down
##   STUB_FOOT_T  0.80 -> foot + a little ankle (hip->foot line)
## and the collar is a height plus a radius about the body's centre line:
##   STUB_COLLAR_Y 1.72 -> how far down the chest is kept. Lower shows more
##     chest but the back starts clipping out through the cloak on a jump,
##     which is what pulled this back up from 1.60.
##   STUB_COLLAR_R 0.32 -> how wide a slice of CHEST. 0.32 is the ceiling,
##     not a preference: this rule only ever sees points already within
##     TORSO_R + BLEND_K of the torso capsule, so a bigger number changes
##     nothing at all (0.46 was tried, moved 146 verts, looked identical).
##     Widening the collar can't reach the shoulders -- that geometry
##     belongs to the arm limb, not the torso.
const STUB_HAND_T := 0.62
const STUB_FOOT_T := 0.80
const STUB_COLLAR_Y := 1.72
const STUB_COLLAR_R := 0.32

static var _cached_stub_mesh: ArrayMesh = null

## Whether a point survives into the stub body. A point can sit near more
## than one limb (the shoulder belongs to both torso and arm), so this is a
## union: any limb whose rule says "keep" keeps it.
static func is_stub_point(p: Vector3) -> bool:
	for limb in limb_defs():
		var key: String = limb["limb"]
		var a: Vector3 = limb["a"]
		var b: Vector3 = limb["b"]
		var ab: Vector3 = b - a
		var denom: float = ab.length_squared()
		var t := 0.0
		if denom > 1e-9:
			t = clampf((p - a).dot(ab) / denom, 0.0, 1.0)
		# BLEND_K's worth of slack because smin() rounds each joint outward
		# past the raw capsule -- without it the fused surface right at a
		# wrist reads as belonging to no limb at all and gets dropped.
		if p.distance_to(a + ab * t) > float(limb["r"]) + BLEND_K:
			continue
		match key:
			"torso", "neck":
				if p.y >= STUB_COLLAR_Y and Vector2(p.x, p.z).length() <= STUB_COLLAR_R:
					return true
			"arm_l", "arm_r":
				# Nothing but the far end. Keeping the top of the upper arm
				# as well was tried, to fill the hollow you can see down the
				# neck opening -- but the outer edge of those shoulder caps
				# pokes through the cloak as a visible sliver, and the hollow
				# is only visible from a much less common angle. Not worth
				# the trade.
				if t >= STUB_HAND_T:
					return true
			_:
				if t >= STUB_FOOT_T:
					return true
	return false

## The filtered copy of `source`, cached per process. Pure function of
## `source` plus the constants above -- same reasoning as the disk cache
## (see fused_body_cache.gd): every character gets the identical result, so
## building it once and sharing it is both safe and the only affordable
## option. Kept out of the .res bake deliberately; it costs one pass over a
## mesh that is already in memory, and only the first time a covering
## garment is actually worn, so it never lands on the load path.
static func stub_mesh(source: ArrayMesh) -> ArrayMesh:
	if _cached_stub_mesh != null:
		return _cached_stub_mesh
	var arrays: Array = source.surface_get_arrays(0)
	var verts: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var normals: PackedVector3Array = arrays[Mesh.ARRAY_NORMAL]
	var uvs: PackedVector2Array = arrays[Mesh.ARRAY_TEX_UV]
	var bones: PackedInt32Array = arrays[Mesh.ARRAY_BONES]
	var weights: PackedFloat32Array = arrays[Mesh.ARRAY_WEIGHTS]
	var influences: int = bones.size() / maxi(1, verts.size())

	var out_v := PackedVector3Array()
	var out_n := PackedVector3Array()
	var out_uv := PackedVector2Array()
	var out_b := PackedInt32Array()
	var out_w := PackedFloat32Array()
	# The fused mesh is raw triangle soup (see build_uncached) -- no index
	# buffer to keep consistent, so a triangle is just three consecutive
	# vertices and dropping one is dropping three entries.
	var tri_count: int = verts.size() / 3
	for tri in range(tri_count):
		var i0: int = tri * 3
		var centroid: Vector3 = (verts[i0] + verts[i0 + 1] + verts[i0 + 2]) / 3.0
		if not is_stub_point(centroid):
			continue
		for k in range(3):
			var vi: int = i0 + k
			out_v.append(verts[vi])
			out_n.append(normals[vi])
			out_uv.append(uvs[vi])
			for j in range(influences):
				out_b.append(bones[vi * influences + j])
				out_w.append(weights[vi * influences + j])

	var out := Array()
	out.resize(Mesh.ARRAY_MAX)
	out[Mesh.ARRAY_VERTEX] = out_v
	out[Mesh.ARRAY_NORMAL] = out_n
	out[Mesh.ARRAY_TEX_UV] = out_uv
	out[Mesh.ARRAY_BONES] = out_b
	out[Mesh.ARRAY_WEIGHTS] = out_w
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, out)
	_cached_stub_mesh = mesh
	print("FusedBody.stub_mesh: kept %d of %d triangles" % [out_v.size() / 3, tri_count])
	return _cached_stub_mesh

static func build_skin(origins: Dictionary) -> Skin:
	var skin := Skin.new()
	for i in range(BONES.size()):
		skin.add_bind(i, Transform3D(Basis(), -origins[BONES[i]]))
	return skin

## Builds the fused mesh (with soft skin weights) plus the parallel
## {pos, uv, clamp} arrays character.gd's CPU raycast/paint system needs
## (see raycast_paint/paint_at) -- positions here are already in the
## character's own local space, no separate pivot+offset composition
## required (unlike the old per-part pivot-node system).
##
## A triangle whose 3 vertices' u (theta-based, wrapping at 0/1 -- the
## atlas-x seam running along the back of the torso/each limb) straddle
## that wrap point -- e.g. two vertices near u=0.99 and one near u=0.01 --
## would otherwise get barycentric-interpolated LINEARLY across that gap,
## sweeping through u~0.5 for any point in between, which is a completely
## unrelated part of the atlas row. That's what caused paint near an
## armpit (close to the wrap seam on the arm/torso's own circumference) to
## also scatter across a trail of unrelated positions elsewhere in the
## same row. Since this mesh is raw (non-indexed) triangle soup -- every
## triangle owns its own 3 vertex copies, never shared with any other
## triangle -- it's safe to "unwrap" a vertex's u value locally for just
## this one triangle (treat 0.01 as 1.01) so interpolation stays
## continuous; painting wraps it back into [0,1) afterward (see
## paint_at's fposmod call in character.gd).
static func _fix_theta_seam(tri_uv: Array) -> void:
	var us := [tri_uv[0].x, tri_uv[1].x, tri_uv[2].x]
	var max_u: float = maxf(us[0], maxf(us[1], us[2]))
	var min_u: float = minf(us[0], minf(us[1], us[2]))
	if max_u - min_u <= 0.5:
		return # normal spread within one triangle, not a seam crossing
	for i in range(3):
		if us[i] < 0.5:
			tri_uv[i] = Vector2(us[i] + 1.0, tri_uv[i].y)

## atlas_row_for_bone: {bone_name: int}: which PART_NAMES row each bone
## paints into. part_count/atlas_cell/atlas_size: same meaning as in
## character.gd, passed in rather than duplicated here.
## The whole point of the cache below: build_uncached() measured at 11-13
## REAL SECONDS per call (tests/scratch_load_profile.gd), and it's a pure
## function of compile-time constants, so every one of those calls after the
## first recomputes a byte-identical result. See fused_body_cache.gd's own
## class comment for the full measurement and why sharing one result across
## every Character instance is safe.
##
## Two layers: an in-memory static (one build per process at worst) in front
## of a disk-baked .res (zero builds in the normal case -- the shipped
## bake). Deliberately READ-ONLY here: writing the bake is
## tests/scratch_fused_body_bake.gd's job, not something the game does at
## runtime (res:// isn't writable in an exported build anyway).
const CACHE_PATH := "res://character/fused_body_cache.res"

static var _cached_build: Dictionary = {}

static func build(atlas_row_for_bone: Dictionary, part_count: int, atlas_cell: int, atlas_size: int) -> Dictionary:
	if not _cached_build.is_empty():
		return _cached_build
	var from_disk := _load_cached_build()
	if not from_disk.is_empty():
		_cached_build = from_disk
		return _cached_build
	# No bake on disk (or a stale/unreadable one) -- fall back to really
	# building it, then at least keep it in memory so this process only ever
	# pays the cost once rather than once per Character.
	_cached_build = build_uncached(atlas_row_for_bone, part_count, atlas_cell, atlas_size)
	return _cached_build

static func _load_cached_build() -> Dictionary:
	if not ResourceLoader.exists(CACHE_PATH):
		return {}
	var cache: Resource = load(CACHE_PATH)
	if cache == null or cache.mesh == null or cache.skin == null:
		return {}
	return {
		"mesh": cache.mesh,
		"skin": cache.skin,
		"paint_positions": cache.paint_positions,
		"paint_uvs": cache.paint_uvs,
		"paint_clamp": _paint_clamp_from_flat(cache.paint_clamp_flat),
	}

## Rehydrates the flattened 4-ints-per-triangle clamp buffer (see
## fused_body_cache.gd's own paint_clamp_flat comment) back into the
## Array[Dictionary] shape character.gd's own paint code already expects.
static func _paint_clamp_from_flat(flat: PackedInt32Array) -> Array[Dictionary]:
	var out: Array[Dictionary] = []
	var count := flat.size() / 4
	for i in range(count):
		var b := i * 4
		out.append({"x0": flat[b], "x1": flat[b + 1], "y0": flat[b + 2], "y1": flat[b + 3]})
	return out

## Flattens build()'s own paint_clamp for the bake -- the inverse of
## _paint_clamp_from_flat above, used only by
## tests/scratch_fused_body_bake.gd.
static func paint_clamp_to_flat(paint_clamp: Array) -> PackedInt32Array:
	var flat := PackedInt32Array()
	for entry in paint_clamp:
		flat.append(entry["x0"])
		flat.append(entry["x1"])
		flat.append(entry["y0"])
		flat.append(entry["y1"])
	return flat

## The real, uncached build -- see build() above for why nothing in the game
## should call this directly anymore.
static func build_uncached(atlas_row_for_bone: Dictionary, part_count: int, atlas_cell: int, atlas_size: int) -> Dictionary:
	var tris := extract_mesh(sdf_body, BOUNDS_MIN, BOUNDS_MAX, CELL_SIZE)
	var segments := segment_defs()
	var origins := bone_origins()

	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)

	var paint_positions := PackedVector3Array()
	var paint_uvs := PackedVector2Array()
	var paint_clamp: Array[Dictionary] = []

	var eps := 0.02
	var tri_count := tris.size() / 3
	for ti in range(tri_count):
		# Computed for all 3 vertices FIRST (not emitted per-vertex
		# independently) so the seam fix below can see the whole triangle
		# at once -- see the note above _fix_theta_seam for why that's
		# necessary.
		var tri_uv := []
		var tri_normal := []
		var tri_bones := []
		var tri_weights := []
		var tri_row := []
		var tri_clamp_rect := []

		# The dominant bone (and therefore the atlas ROW and UV axis) is
		# decided ONCE per triangle, from its centroid -- not independently
		# per vertex. Every smin blend junction (armpit: torso/arm, hip:
		# torso/leg) has a knife-edge where two bones' blend weights are
		# nearly tied; a tiny triangle straddling that edge could have one
		# vertex "won" by torso and another by the neighboring limb, putting
		# its 3 corners in TWO DIFFERENT atlas rows. The GPU linearly
		# interpolates UV across a triangle, so the interior of such a
		# triangle sweeps through every row in between (e.g. the arm rows,
		# sitting numerically between torso's and a leg's row) -- sampling
		# whatever happened to be painted there. That's what caused a paint
		# stroke near the armpit (which also touched the arm's row) to show
		# up as a thin line traced along the torso/leg boundary at the hip,
		# nowhere near where the click actually landed. Sharing one dominant
		# bone across all 3 corners guarantees a triangle never spans more
		# than one row.
		var centroid := (tris[ti * 3] + tris[ti * 3 + 1] + tris[ti * 3 + 2]) / 3.0
		var centroid_bone_weight := {}
		var centroid_d_min := INF
		for seg in segments:
			centroid_d_min = minf(centroid_d_min, sdf_capsule(centroid, seg["a"], seg["b"], seg["r"]))
		for seg in segments:
			var cd: float = sdf_capsule(centroid, seg["a"], seg["b"], seg["r"])
			var cw: float = exp(-(cd - centroid_d_min) / BLEND_K)
			centroid_bone_weight[seg["bone"]] = centroid_bone_weight.get(seg["bone"], 0.0) + cw
		var dom_bone: String = BONES[0]
		var best_w := -1.0
		for bone_name in BONES:
			var w: float = centroid_bone_weight.get(bone_name, 0.0)
			if w > best_w:
				best_w = w
				dom_bone = bone_name
		# A forearm shares its parent upper-arm's row/UV axis (see
		# PAINT_BONE_FOR) rather than getting its own, so paint stays
		# continuous across the elbow instead of seaming there.
		var paint_bone: String = PAINT_BONE_FOR.get(dom_bone, dom_bone)
		var axis := _uv_axis(paint_bone)
		var axis_a: Vector3 = axis["a"]
		var axis_b: Vector3 = axis["b"]
		var row: int = atlas_row_for_bone[paint_bone]

		for k in range(3):
			var v: Vector3 = tris[ti * 3 + k]
			var gx: float = sdf_body(v + Vector3(eps, 0, 0)) - sdf_body(v - Vector3(eps, 0, 0))
			var gy: float = sdf_body(v + Vector3(0, eps, 0)) - sdf_body(v - Vector3(0, eps, 0))
			var gz: float = sdf_body(v + Vector3(0, 0, eps)) - sdf_body(v - Vector3(0, 0, eps))
			var normal := Vector3(gx, gy, gz).normalized()

			var bone_weight := {}
			var d_min := INF
			for seg in segments:
				d_min = minf(d_min, sdf_capsule(v, seg["a"], seg["b"], seg["r"]))
			for seg in segments:
				var d: float = sdf_capsule(v, seg["a"], seg["b"], seg["r"])
				var w: float = exp(-(d - d_min) / BLEND_K)
				var bone_name: String = seg["bone"]
				bone_weight[bone_name] = bone_weight.get(bone_name, 0.0) + w

			var names: Array = bone_weight.keys()
			names.sort_custom(func(a, b): return bone_weight[a] > bone_weight[b])
			names = names.slice(0, 4)
			var total := 0.0
			for n in names:
				total += bone_weight[n]
			var bones_arr := [0, 0, 0, 0]
			var weights_arr := [0.0, 0.0, 0.0, 0.0]
			for i in range(names.size()):
				bones_arr[i] = BONES.find(names[i])
				weights_arr[i] = bone_weight[names[i]] / total

			var ab := axis_b - axis_a
			var raw_t := (v - axis_a).dot(ab) / maxf(ab.length_squared(), 0.0000001)
			# radial/theta (the U/circumference axis) only ever needs the
			# NEAREST point ON the segment itself, same as before -- clamped
			# to [0,1] regardless of how the V axis below handles overflow.
			var clamped_t := clampf(raw_t, 0.0, 1.0)
			var radial := v - (axis_a + ab * clamped_t)
			var theta := atan2(radial.z, radial.x)
			var u := theta / TAU + 0.5

			# A capsule's own rounded end caps genuinely extend PAST its a/b
			# endpoints (a capsule isn't a flat-ended cylinder) -- hard-
			# clamping t to [0,1] collapsed EVERY vertex out there onto the
			# exact same V coordinate regardless of how far past the endpoint
			# it actually was. Confirmed directly as a real bug, not just
			# theoretical: at the torso's own top cap (which blends into the
			# neck/head right at the collar -- a natural, common place to
			# paint), this produced literal ZERO-AREA triangles (all 3
			# corners sharing one exact UV.y), which is exactly the kind of
			# input character.gd's own paint_at() anisotropic correction
			# can't do anything sensible with (a real tangent/bitangent can't
			# be solved from a degenerate triangle) -- it was falling back to
			# a generic reference shape there instead of this cap's own
			# actual local geometry, and the surrounding non-degenerate
			# triangles right at that same knife-edge boundary were
			# themselves extremely compressed, since a whole real cap's
			# worth of surface was fighting to fit into the last sliver
			# before the clamp. Smoothly asymptoting into a small reserved
			# slice at each end of the row instead (never reaching it
			# exactly, so it can't create a NEW degenerate case out at the
			# far end either) spreads real, distinct 3D points across real,
			# distinct V values there.
			const CAP_T_FRACTION := 0.2
			const CAP_OVERFLOW_SCALE := 0.22
			var t: float
			if raw_t > 1.0:
				var overflow_norm: float = 1.0 - exp(-(raw_t - 1.0) / CAP_OVERFLOW_SCALE)
				t = (1.0 - CAP_T_FRACTION) + overflow_norm * CAP_T_FRACTION
			elif raw_t < 0.0:
				var overflow_norm: float = 1.0 - exp(raw_t / CAP_OVERFLOW_SCALE)
				t = CAP_T_FRACTION * (1.0 - overflow_norm)
			else:
				t = CAP_T_FRACTION + raw_t * (1.0 - 2.0 * CAP_T_FRACTION)

			# Padded a few pixels in from the row's true edges: t=0 or t=1
			# (the far ends of a limb/torso) would otherwise map to EXACTLY
			# row/part_count or (row+1)/part_count -- and since adjacent
			# rows share that same boundary value by construction, floating-
			# point rounding at that exact knife-edge can push a vertex's
			# computed pixel across into the NEIGHBORING part's row (this is
			# what caused paint on the head's bottom edge to alias with the
			## torso's own t=0 end at the crotch, despite them being on
			# opposite sides of a shared row boundary). Insetting the usable
			# v-range keeps every vertex's UV safely inside its own row
			# regardless of float precision.
			var row_margin: float = 2.0 / float(atlas_cell * part_count)
			var v0 := float(row) / float(part_count) + row_margin
			var v1 := float(row + 1) / float(part_count) - row_margin

			tri_uv.append(Vector2(u, v0 + t * (v1 - v0)))
			tri_normal.append(normal)
			tri_bones.append(bones_arr)
			tri_weights.append(weights_arr)
			tri_row.append(row)
			tri_clamp_rect.append({"x0": 0, "x1": atlas_size, "y0": row * atlas_cell, "y1": (row + 1) * atlas_cell})

		_fix_theta_seam(tri_uv)

		for k in range(3):
			st.set_normal(tri_normal[k])
			st.set_uv(tri_uv[k])
			st.set_bones(tri_bones[k])
			st.set_weights(tri_weights[k])
			st.add_vertex(tris[ti * 3 + k])

			paint_positions.append(tris[ti * 3 + k])
			paint_uvs.append(tri_uv[k])
			if k == 0:
				paint_clamp.append(tri_clamp_rect[k])

	var mesh := st.commit()
	return {
		"mesh": mesh,
		"skin": build_skin(origins),
		"paint_positions": paint_positions,
		"paint_uvs": paint_uvs,
		"paint_clamp": paint_clamp,
	}
