extends SceneTree
## Regression test for the fused-body integration: loads the real
## character.tscn (not a reimplementation) and exercises mesh build,
## painting, head shape switching, and the walk animation.
##
## Runs the checks from _process() on a later frame, not _init(): _ready()
## fires via the engine's deferred message queue, which only gets flushed
## once an actual frame passes -- running everything synchronously in
## _init() (before any frame processing) meant _ready() hadn't happened
## yet, and forcing it manually via notification() caused it to ALSO fire
## again naturally later, rebuilding everything a second time. Waiting for
## a real frame is what actually happens at runtime, so it's the right fix.

const FusedBody := preload("res://character/fused_body.gd")

var _frame := 0
var _character: Node3D

func _init():
	var CharacterScene: PackedScene = load("res://character/character.tscn")
	_character = CharacterScene.instantiate()
	get_root().add_child(_character)

func _process(_delta: float) -> bool:
	_frame += 1
	if _frame < 3:
		return false
	_run_checks()
	return false

func _run_checks() -> void:
	var character := _character
	var ok := true

	# 1. Basic build sanity.
	if character.tri_positions.size() == 0 or character.tri_positions.size() % 3 != 0:
		print("FAIL: tri_positions empty or not a multiple of 3")
		ok = false
	else:
		print("PASS: tri_positions has ", character.tri_positions.size() / 3, " triangles")

	var body_skeleton: Skeleton3D = character.get_node(character.BODY_SKELETON_NAME)
	if body_skeleton == null:
		print("FAIL: BodySkeleton node missing")
		ok = false
	elif body_skeleton.get_bone_count() != 14:
		print("FAIL: expected 14 bones, got ", body_skeleton.get_bone_count())
		ok = false
	else:
		print("PASS: BodySkeleton has 14 bones")

	var head_pivot: Node3D = character.get_head_node()
	if head_pivot == null:
		print("FAIL: head node missing")
		ok = false
	else:
		print("PASS: head node present at ", head_pivot.global_position)

	# 2. Paint raycast + stamp round trip: cast a ray straight at the hips
	# from the front (a fixed height safely inside the hips row, not
	# get_torso_center_y()'s own midpoint -- the torso limb now splits into
	# hips/spine exactly AT that midpoint, see fused_body.gd's own SPINE_T,
	# so using it here would land the ray right on the boundary between two
	# different atlas rows, an ambiguous target for what this check actually
	# wants to verify) and confirm a hit lands, then paint and confirm the
	# atlas actually changed (not just silently no-op'd).
	var hips_y: float = FusedBody.HIP_Y + 0.1
	var hit: Dictionary = character.raycast_paint(Vector3(0, hips_y, 5), Vector3(0, 0, -1), 20.0)
	if hit.is_empty():
		print("FAIL: raycast against hips found nothing")
		ok = false
	else:
		print("PASS: raycast hit hips at local_pos=", hit["local_pos"], " face_index=", hit["face_index"])
		var before: Image = character.atlas_image.duplicate()
		character.paint_at(hit["local_pos"], hit["face_index"], Color.RED, 3)
		var changed := false
		for y in range(character.atlas_height):
			for x in range(character.atlas_size):
				if before.get_pixel(x, y) != character.atlas_image.get_pixel(x, y):
					changed = true
					break
			if changed:
				break
		if not changed:
			print("FAIL: paint_at produced no atlas change")
			ok = false
		else:
			print("PASS: paint_at changed the atlas")

	# 2b. Verify the painted pixel actually landed in the HIPS's row, not
	# some other part's row (the historical atlas Y-flip bug this project
	# hit before would show up as paint landing in the wrong row).
	if not hit.is_empty():
		var hips_row: int = character.PART_NAMES.find("hips")
		var atlas_cell: int = character.ATLAS_CELL
		var y0: int = hips_row * atlas_cell
		var y1: int = y0 + atlas_cell
		var found_in_hips_row := false
		for y in range(y0, y1):
			for x in range(character.atlas_size):
				if character.atlas_image.get_pixel(x, y).is_equal_approx(Color.RED):
					found_in_hips_row = true
					break
			if found_in_hips_row:
				break
		print("PASS: paint landed in hips's atlas row" if found_in_hips_row else "FAIL: paint did NOT land in hips's atlas row")
		ok = ok and found_in_hips_row

	# 3. Head shape switching.
	character.set_head_shape("sphere")
	if character.get_head_shape() != "sphere":
		print("FAIL: head shape did not change")
		ok = false
	else:
		print("PASS: head shape changed to sphere, tri count now ", character.tri_positions.size() / 3)

	# 4. Walk animation: start walking, advance time, confirm a bone
	# actually rotated away from identity, then stop and confirm reset.
	character.set_walking(true)
	character.anim_player.seek(0.2, true)
	var thigh_r_idx := body_skeleton.find_bone("thigh_r")
	var rot_while_walking: Quaternion = body_skeleton.get_bone_pose_rotation(thigh_r_idx)
	if rot_while_walking.is_equal_approx(Quaternion.IDENTITY):
		print("FAIL: thigh_r bone didn't rotate during walk animation")
		ok = false
	else:
		print("PASS: thigh_r bone rotated during walk: ", rot_while_walking)

	# Stopping now plays a real looping standing-idle animation (retargeted
	# Mixamo mocap, see character.gd's StandIdleAnim) instead of resetting
	# every bone to a raw identity pose -- a natural idle sways the legs too
	# (confirmed directly: thigh rotation is non-identity throughout the
	# clip), so the right check here is "idle took over", not "bone is back
	# at identity".
	character.set_walking(false)
	if character.anim_player.current_animation == "idle" and character.anim_player.is_playing():
		print("PASS: stopping walking switched to the looping idle animation")
	else:
		print("FAIL: expected 'idle' to be playing after stopping, current_animation=", character.anim_player.current_animation)
		ok = false

	print("OVERALL: ", "PASS" if ok else "FAIL")
	quit(0 if ok else 1)
