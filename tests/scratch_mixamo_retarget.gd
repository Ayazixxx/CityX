extends Node
## Retargets a Mixamo FBX animation onto our own 14-bone rig.
##
## Mixamo's own rest pose has a complex, non-identity, per-bone-oriented
## basis at every joint (e.g. the shoulder's rest basis is an 82/158/-100
## degree rotation -- confirmed by direct inspection) -- completely unlike
## our own rig, where every bone's bind pose is a plain identity basis (see
## FusedBody.build_skin). Copying Mixamo's raw local rotation values onto
## our bones directly would therefore be meaningless -- they're expressed in
## a totally different per-bone coordinate frame.
##
## Standard fix: measure each bone's rotation as a DELTA from ITS OWN rest
## pose, in GLOBAL (world) space (not local -- local delta would still be
## expressed in the source's own bone-local axes, which is exactly what
## differs between the two rigs). A global delta is a physical, frame-
## independent fact ("the upper arm rotated this much in world space") that
## can be reapplied to a DIFFERENT rig's own rest pose and still mean the
## same thing.
##
## That reapplication needs a REFERENCE for the target rig's own "rest" to
## apply the delta on top of -- and this is where a real bug lived (found
## via a numeric elbow-bend-angle check against the source: ~140 degrees on
## the first working version vs ~69 degrees on the source for the same
## frame, a genuine discrepancy, not just an Euler-angle reading artifact).
## Our own target rig's LOCAL bind rotation is identity for every bone (a
## skinning-convenience choice, see FusedBody.build_skin) -- but that is
## NOT the same claim as "our rig's rest SHAPE is a T-pose like Mixamo's".
## It isn't: our rig's identity-rotation configuration is arms-hanging-DOWN
## (a normal standing idle), entirely different from Mixamo's T-pose
## (arms-out-HORIZONTAL). A world-space delta measured relative to a
## horizontal-arm rest does not mean the same thing when blindly reapplied
## on top of a hanging-arm rest. See _compute_calibration for the fix:
## sight along each bone's own rest direction toward its child in BOTH
## rigs, and use the shortest-arc rotation between those two directions
## (Q_align) as the target's own T_rest_global, instead of assuming a bare
## Identity. From there, converting to a local pose for OUR Animation track
## just needs OUR OWN parent chain (walked top-down, using already-computed
## target_global_pose values for each bone's parent).

const FusedBody := preload("res://character/fused_body.gd")
const BODY_SKELETON_NAME := "BodySkeleton"

# target bone name -> source (mixamo) bone name. Crossed L/R deliberately --
# verified directly (see the throwaway side-check run before this script):
# "Boxingrightjab.fbx" actually moves mixamorig_RightHand the most, and our
# OWN "arm_l"/"forearm_l"/"hand_l" are the bones that read as the
# character's own right hand (established earlier this session, and still
# true after the rig rebuild) -- so Mixamo's Right* maps to our own *_l.
const BONE_MAP := {
	"hips": "mixamorig_Hips",
	"spine": "mixamorig_Spine2",
	"neck": "mixamorig_Neck",
	"head": "mixamorig_Head",
	"arm_l": "mixamorig_RightArm",
	"forearm_l": "mixamorig_RightForeArm",
	"hand_l": "mixamorig_RightHand",
	"arm_r": "mixamorig_LeftArm",
	"forearm_r": "mixamorig_LeftForeArm",
	"hand_r": "mixamorig_LeftHand",
	"thigh_l": "mixamorig_RightUpLeg",
	"shin_l": "mixamorig_RightLeg",
	"thigh_r": "mixamorig_LeftUpLeg",
	"shin_r": "mixamorig_LeftLeg",
}
# Walked in this order (root first, each bone after its own parent) so a
# bone's parent's target_global is always already computed by the time the
# bone itself is processed -- FusedBody.BONES is already in exactly this
# order (see build_skeleton, which adds bones in this order and relies on
# the same property), reused directly here rather than re-listing it so the
# two definitions can never drift apart.
const BONE_ORDER := FusedBody.BONES

# Which bone to sight along, per bone, to calibrate its rest ORIENTATION
# (see the big comment on CALIBRATION below) -- our own bind rotations are
# all identity (a skinning-convenience choice, see build_skin), but the
# character's actual VISUAL rest shape is arms-hanging-DOWN, not Mixamo's
# T-pose arms-out-HORIZONTAL -- two totally different real-world
# configurations that happen to both be labeled "identity"/"rest". A bone
# with no entry here has no child of its own in the retargeted chain
# (hand_l/r, shin_l/r) and reuses its own parent's calibration instead (see
# _compute_calibration).
const AIM_CHILD := {
	"hips": "spine", "spine": "neck", "neck": "head",
	"arm_l": "forearm_l", "forearm_l": "hand_l",
	"arm_r": "forearm_r", "forearm_r": "hand_r",
	"thigh_l": "shin_l", "thigh_r": "shin_r",
}

const SAMPLE_RATE := 30.0 # Hz

func _find_skeleton(node: Node) -> Skeleton3D:
	if node is Skeleton3D:
		return node
	for child in node.get_children():
		var found := _find_skeleton(child)
		if found != null:
			return found
	return null

func _find_anim_player(node: Node) -> AnimationPlayer:
	if node is AnimationPlayer:
		return node
	for child in node.get_children():
		var found := _find_anim_player(child)
		if found != null:
			return found
	return null

## Global REST transform (basis AND origin -- origin matters here, unlike
## the old basis-only version, since calibration below needs real rest
## POSITIONS to measure a direction) for every bone in a Skeleton3D,
## computed once by composing get_bone_rest() up each bone's own parent
## chain (Skeleton3D doesn't cache/expose this directly the way it does for
## the live POSE via get_bone_global_pose, so it's computed by hand here).
func _compute_rest_globals(skel: Skeleton3D) -> Dictionary:
	var globals := {}
	for i in range(skel.get_bone_count()):
		var chain := []
		var cur := i
		while cur >= 0:
			chain.append(cur)
			cur = skel.get_bone_parent(cur)
		chain.reverse()
		var xform := Transform3D()
		for bone_idx in chain:
			xform = xform * skel.get_bone_rest(bone_idx)
		globals[i] = xform
	return globals

## The core fix for a bug found the hard way (see the big module comment):
## naively treating our own target rig's rest as "globally identity" silently
## assumes our rest SHAPE (arms hanging down) is the same real-world
## configuration as Mixamo's rest SHAPE (arms out in a T-pose) -- it isn't,
## so a world-space delta measured against a horizontal-arm rest doesn't
## mean the same thing when reapplied on top of a hanging-arm rest (verified
## empirically: the elbow bend angle measured from real 3D joint positions
## came out ~140 degrees on our retargeted result vs ~69 degrees on the
## source for the exact same clip/bone -- a real, large discrepancy, not
## just an artifact of reading Euler angles).
##
## Fix: for each bone, sight along its own rest direction toward its child
## (AIM_CHILD) in BOTH rigs -- source in real global rest space (Mixamo's
## rig has real translational rest offsets), target using bone_origins()
## directly (valid in world space since target's local rest rotation truly
## is identity) -- and compute the shortest-arc rotation between the two
## directions. That rotation (Q_align) is what our target bone's rest would
## need to ALREADY be, if reinterpreted as pointing the same real-world
## direction Mixamo's bone points at rest, and is used as T_rest_global in
## place of a bare Identity everywhere below. Bones with no child of their
## own in the chain (hand_l/r, shin_l/r) reuse their own parent's alignment
## (a leaf bone can't calibrate against a child it doesn't have -- its own
## sub-segment is colinear with its parent's by construction anyway, see
## fused_body.gd's LIMB_BONES).
func _compute_calibration(src_skel: Skeleton3D, rest_globals: Dictionary) -> Dictionary:
	var target_origins: Dictionary = FusedBody.bone_origins()
	var align := {}
	for bone_name in BONE_ORDER:
		if not AIM_CHILD.has(bone_name):
			continue
		var child_name: String = AIM_CHILD[bone_name]
		var target_dir: Vector3 = (target_origins[child_name] - target_origins[bone_name]).normalized()

		var src_idx := src_skel.find_bone(BONE_MAP[bone_name])
		var src_child_idx := src_skel.find_bone(BONE_MAP[child_name])
		var src_pos: Vector3 = rest_globals[src_idx].origin
		var src_child_pos: Vector3 = rest_globals[src_child_idx].origin
		var source_dir: Vector3 = (src_child_pos - src_pos).normalized()

		align[bone_name] = Quaternion(target_dir, source_dir)

	# Leaf bones: inherit the parent's alignment (no child to calibrate
	# against of their own).
	for bone_name in BONE_ORDER:
		if align.has(bone_name):
			continue
		var parent_name: String = FusedBody.BONE_PARENT.get(bone_name, "")
		align[bone_name] = align.get(parent_name, Quaternion.IDENTITY)
	return align

## Retargets ONE animation onto our rig, returns a new Animation resource
## with one rotation-only TYPE_VALUE track per BONE_ORDER entry, sampled at
## SAMPLE_RATE across the source's own full length (no rescaling -- keeps
## the original mocap timing so the FIRST look can judge whether that
## timing is even usable before touching it).
## include_root_rotation=false (the default) freezes "hips" at identity
## instead of taking Mixamo's own root sway -- our game's own walk/run/punch
## animations already follow this convention (root position/facing is owned
## entirely by player.gd's movement code, never by an animation track), and
## mixing in Mixamo's real hip rotation WITHOUT the matching hip
## TRANSLATION it was recorded against (root motion isn't retargeted here
## at all) reads as a physically-inconsistent lean/twist rather than a
## boxer's weight shift -- confirmed visually: the first render with hips
## rotation included looked like a warped, off-balance floating pose, not a
## stance. Freezing hips only changes what children are measured RELATIVE
## TO (their own independently-measured global deltas are unchanged), so
## the rest of the body still reflects the source animation faithfully.
## trim_start/trim_end (both -1.0 by default, meaning "use the whole clip")
## window the OUTPUT to just [trim_start, trim_end] of the source's own
## timeline, remapped so the output animation starts at t=0 -- needed
## because raw mocap punches spend most of their multi-second length just
## holding the guard before/after the actual strike, and our combat system
## blocks movement/other punches for a punch animation's FULL duration (see
## character.gd's _is_punching) -- playing a 2-second clip at native speed
## would freeze the player for 2 real seconds on every single throw, wrecking
## the existing fast/spammable combat feel. Trimming to just the windup-
## strike-recovery window (found via scratch_mixamo_peak.gd's own extension
## scan) keeps the source's own natural relative timing intact -- this is a
## WINDOW, not a time-rescale.
##
## Given this SAMPLE's already-computed GLOBAL bases for thigh_l/shin_l/
## thigh_r/shin_r (target_global, computed with hips frozen at identity --
## true for every crouch clip, which never sets include_root_rotation), for
## each leg walks the SAME forward-kinematics composition Skeleton3D would
## do internally: thigh's global origin is just its own rest position
## (hips contributes no rotation), shin's global origin is thigh's origin
## plus thigh's OWN rotation applied to the rest-space thigh->shin offset,
## and the "foot" (no real bone -- just the rest-space point straight below
## shin at FusedBody.FOOT_Y, matching how the leg's own rest capsule is a
## single straight line down to that exact height) is shin's origin plus
## the COMBINED thigh*shin rotation applied to the rest-space shin->foot
## offset. Returns how far to move the hip DOWN so the more-extended leg's
## own foot lands at exactly y=0 (matching FusedBody.FOOT_Y's own "lowest
## point sits at world y=0" calibration) -- negative if that leg's bend
## already lifts the foot above 0 (the normal crouch case), positive in the
## unlikely event a leg's own bend would already dip below 0 even at full
## hip height (guards against clipping either direction rather than just
## assuming crouch bends only ever lift the foot).
func _grounded_hip_offset(target_global: Dictionary) -> float:
	var origins: Dictionary = FusedBody.bone_origins()
	var lowest_foot_y := INF
	for side in ["l", "r"]:
		var thigh_name: String = "thigh_" + side
		var shin_name: String = "shin_" + side
		var thigh_origin: Vector3 = origins[thigh_name]
		var shin_origin: Vector3 = origins[shin_name]
		var thigh_basis: Basis = target_global[thigh_name]
		var shin_basis: Basis = target_global[shin_name]

		var shin_world: Vector3 = thigh_origin + thigh_basis * (shin_origin - thigh_origin)
		# shin_basis (target_global["shin_*"]) is ALREADY shin's own GLOBAL
		# (world-space) basis -- it's a direct per-bone measurement (see
		# retarget()'s own big comment on how target_global is built), not a
		# parent-relative local one that still needs thigh's rotation
		# composed onto it. Multiplying by thigh_basis here was a real bug:
		# it applied thigh's own rotation a SECOND time on top of shin's
		# already-global basis, badly overestimating how far the bent knee
		# lifts the foot and therefore how far to drop the hip -- confirmed
		# directly by the resulting overcorrection clipping the feet even
		# further below ground than before this whole fix existed.
		# ground_ref_y is the true ground-touching point, FOOT_Y - LEG_R (the
		# capsule's rounded cap extends past FOOT_Y by its own radius) -- see
		# FusedBody.FOOT_Y's own comment.
		var ground_ref_y: float = FusedBody.FOOT_Y - FusedBody.LEG_R
		var foot_local_offset := Vector3(0, ground_ref_y - shin_origin.y, 0)
		var foot_world_y: float = shin_world.y + (shin_basis * foot_local_offset).y

		lowest_foot_y = minf(lowest_foot_y, foot_world_y)
	return -lowest_foot_y

## retarget_hip_height=true ADDITIONALLY tracks the hips bone's own POSITION
## (not just rotation) -- found necessary specifically for crouch clips.
## Every other animation this rig plays (walk/run/idle/punch/jump) keeps the
## character at essentially the same standing height throughout, achieved
## purely through joint ROTATION (bent knees, swinging arms), which is why
## rotation-only retargeting has been correct everywhere else. A crouch is
## different: it's fundamentally about the whole pelvis physically dropping
## closer to the ground, a TRANSLATION, not a rotation -- confirmed directly
## by measuring our own target rig's hip height before/after a rotation-only
## retargeted "standing to crouch" clip and getting an exact 0.0 drop, while
## the SOURCE's own hips dropped ~46% of standing height over the same clip.
##
## The first version of this fix just copied the SOURCE's own absolute hip
## Y delta onto our own rig's fixed hip position -- this seemed right (the
## elbow-bend-angle calibration fix used exactly this kind of "copy a
## measured delta" approach successfully) but broke on the very next real
## playtest: releasing crouch made the legs visibly clip through the floor.
## Root cause: FusedBody.FOOT_Y is calibrated so a straight leg's own lowest
## point lands at EXACTLY world y=0 -- moving hips down by Mixamo's own
## absolute amount, independently of what OUR OWN (differently-proportioned)
## thigh/shin bend actually does to OUR OWN leg's vertical reach, has no
## reason to keep OUR feet at y=0 too; whatever gap is left between "how
## much our own bent leg's own reach shortened" and "how much the hip
## dropped" shows up as the foot poking through or floating above the
## ground.
##
## Fixed properly this time: instead of copying Mixamo's own hip number at
## all, compute how far OUR OWN target rig's already-retargeted thigh/shin
## rotations shorten the leg's own vertical reach (real forward kinematics
## using OUR OWN bone_origins(), not Mixamo's proportions), and lower the
## hip by EXACTLY that much so the foot lands at y=0 by construction, no
## matter how our own leg proportions compare to Mixamo's. See
## _grounded_hip_offset.
func retarget(source_scene: PackedScene, source_anim_name: String, include_root_rotation: bool = false, trim_start: float = -1.0, trim_end: float = -1.0, retarget_hip_height: bool = false, include_root_translation: bool = false) -> Animation:
	var inst := source_scene.instantiate()
	add_child(inst)
	var src_skel := _find_skeleton(inst)
	var src_anim_player := _find_anim_player(inst)
	var src_anim: Animation = src_anim_player.get_animation_library("").get_animation(source_anim_name)
	var rest_globals := _compute_rest_globals(src_skel)
	var align := _compute_calibration(src_skel, rest_globals)
	var target_hips_base_pos: Vector3 = FusedBody.bone_origins()["hips"]

	# include_root_translation is the general-purpose sibling of
	# retarget_hip_height: that flag only ever tracks a Y-only grounded-foot
	# offset (right for a crouch, which is fundamentally "pelvis drops
	# straight down while the feet stay planted" -- see _grounded_hip_offset's
	# own comment), but a real seat-transition clip (see entering_car.fbx/
	# exiting_car.fbx) moves the whole pelvis through a real 3D arc (walking
	# up, swinging down and back into the seat), not a pure vertical drop --
	# confirmed directly (a Blender-side inspection, since the clip's own
	# hips bone moves ~1.9 source units on its OTHER two axes too, not just
	# height). Fix: carry the SOURCE's own hips position delta (measured from
	# ITS OWN rest, for consistency with how every rotation delta here is
	# already computed relative to rest, not frame 0) straight onto our
	# target's own base hip position, uniformly scaled by the two rigs'
	# relative standing hip heights so a taller/shorter source doesn't move
	# our own differently-proportioned rig too far or too little.
	var src_hips_idx := src_skel.find_bone(BONE_MAP["hips"])
	var src_hips_rest_pos: Vector3 = rest_globals[src_hips_idx].origin
	var root_translation_scale: float = target_hips_base_pos.y / src_hips_rest_pos.y if absf(src_hips_rest_pos.y) > 0.0001 else 1.0

	var window_start: float = maxf(trim_start, 0.0) if trim_start >= 0.0 else 0.0
	var window_end: float = minf(trim_end, src_anim.length) if trim_end >= 0.0 else src_anim.length

	# seek() only does anything once an animation is actually assigned --
	# without this, every seek() below silently no-ops and every sample
	# reads back the skeleton's own rest pose (confirmed the hard way: the
	# very first run of this script produced dead-identity rotations at
	# every single sample time). speed_scale=0 stops play() from also
	# auto-advancing playback during the awaited frames below -- position
	# is driven purely by the explicit seek() each iteration instead.
	src_anim_player.play(source_anim_name)
	src_anim_player.speed_scale = 0.0

	var out := Animation.new()
	out.length = window_end - window_start
	out.loop_mode = Animation.LOOP_LINEAR

	var tracks := {}
	for bone_name in BONE_ORDER:
		if bone_name == "hips" and not include_root_rotation:
			continue
		var bone_idx: int = FusedBody.BONES.find(bone_name)
		var idx := out.add_track(Animation.TYPE_VALUE)
		out.track_set_path(idx, NodePath(BODY_SKELETON_NAME + ":bones/" + str(bone_idx) + "/rotation"))
		tracks[bone_name] = idx

	var hips_pos_track := -1
	if retarget_hip_height or include_root_translation:
		var hips_bone_idx: int = FusedBody.BONES.find("hips")
		hips_pos_track = out.add_track(Animation.TYPE_VALUE)
		out.track_set_path(hips_pos_track, NodePath(BODY_SKELETON_NAME + ":bones/" + str(hips_bone_idx) + "/position"))

	var t := 0.0
	var dt := 1.0 / SAMPLE_RATE
	while t <= (window_end - window_start) + 0.0001:
		var sample_t: float = minf(window_start + t, window_end)
		src_anim_player.seek(sample_t, true)
		# seek() with update=true should apply immediately, but the skeleton's
		# own cached global pose matrices are only guaranteed fresh after a
		# process/physics frame actually runs -- without this, early samples
		# read stale (rest-pose) global poses.
		await get_tree().process_frame

		var target_global := {}
		for bone_name in BONE_ORDER:
			var src_bone_name: String = BONE_MAP[bone_name]
			var src_idx := src_skel.find_bone(src_bone_name)
			var pose_global: Basis = src_skel.get_bone_global_pose(src_idx).basis
			var rest_global: Basis = rest_globals[src_idx].basis
			var delta_global: Basis = pose_global * rest_global.inverse()
			# T_pose_global = delta_global * T_rest_global, using the
			# CALIBRATED rest reference (see _compute_calibration) instead of
			# a bare identity -- this is the piece that was missing before.
			var calibrated: Basis = delta_global * Basis(align[bone_name])
			if bone_name == "hips" and not include_root_rotation:
				calibrated = Basis()
			target_global[bone_name] = calibrated

			if bone_name == "hips" and not include_root_rotation:
				continue

			var parent_name: String = FusedBody.BONE_PARENT.get(bone_name, "")
			var local_rot: Quaternion
			if parent_name == "":
				local_rot = calibrated.get_rotation_quaternion()
			else:
				var parent_global: Basis = target_global[parent_name]
				# Both sides must be the CALIBRATED global (this bone's own
				# Q_align included, not just its parent's) -- using the raw
				# delta_global here silently dropped this bone's own
				# calibration term, a real bug caught by re-deriving the
				# formula against this exact line, not by the numeric check
				# (which happened to still land close for the elbow -- see
				# git history/session notes if this ever needs re-auditing).
				local_rot = (parent_global.inverse() * calibrated).get_rotation_quaternion()
			out.track_insert_key(tracks[bone_name], t, local_rot)

		if retarget_hip_height:
			var hip_offset_y := _grounded_hip_offset(target_global)
			out.track_insert_key(hips_pos_track, t, target_hips_base_pos + Vector3(0, hip_offset_y, 0))
		elif include_root_translation:
			var src_hips_idx2 := src_skel.find_bone(BONE_MAP["hips"])
			var src_hips_pose_pos: Vector3 = src_skel.get_bone_global_pose(src_hips_idx2).origin
			var source_delta: Vector3 = src_hips_pose_pos - src_hips_rest_pos
			out.track_insert_key(hips_pos_track, t, target_hips_base_pos + source_delta * root_translation_scale)

		if t >= (window_end - window_start):
			break
		t += dt

	inst.queue_free()
	return out
