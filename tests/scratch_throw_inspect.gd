extends Node3D
## One-off inspection: confirm throw.fbx uses the same Mixamo bone naming
## and find its own animation library name + length + a hand-to-shoulder
## distance scan (same technique scratch_mixamo_peak.gd's own deleted
## precedent used for heavy_punch/jab timing) to locate the windup/release
## timing. Not part of the game.

const ThrowFbx := preload("res://character/mixamo_import/throw.fbx")

func _ready() -> void:
	var inst := ThrowFbx.instantiate()
	add_child(inst)

	var skel := _find_skeleton(inst)
	var anim_player := _find_anim_player(inst)

	print("=== Skeleton bones ===")
	for i in range(skel.get_bone_count()):
		print(i, ": ", skel.get_bone_name(i))

	print("=== Animation libraries ===")
	for lib_name in anim_player.get_animation_library_list():
		var lib := anim_player.get_animation_library(lib_name)
		for anim_name in lib.get_animation_list():
			var anim: Animation = lib.get_animation(anim_name)
			print("lib='", lib_name, "' anim='", anim_name, "' length=", anim.length, " tracks=", anim.get_track_count())

	# Scan: which arm throws it -- hand-to-hip distance across the whole
	# clip, both hands (same crossed-mapping uncertainty heavy_punch's own
	# scan resolved -- Mixamo Right maps to our arm_l, confirmed elsewhere,
	# but re-verify which of Left/Right ACTUALLY moves the most here rather
	# than assuming). "mixamo_com" specifically, not "Take 001" -- every
	# other clip in this project's own pipeline uses "mixamo_com" as the
	# real, actually-animated take (confirmed directly: "Take 001" here
	# reads back as a completely static pose at every single sampled time,
	# almost certainly an unanimated rest/reference take Mixamo's own
	# exporter includes alongside the real one).
	var anim_name := "mixamo_com"

	anim_player.play(anim_name)
	anim_player.speed_scale = 0.0

	var right_hand_idx := skel.find_bone("mixamorig_RightHand")
	var left_hand_idx := skel.find_bone("mixamorig_LeftHand")
	var right_shoulder_idx := skel.find_bone("mixamorig_RightShoulder")
	var left_shoulder_idx := skel.find_bone("mixamorig_LeftShoulder")
	if right_shoulder_idx == -1:
		right_shoulder_idx = skel.find_bone("mixamorig_RightArm")
	if left_shoulder_idx == -1:
		left_shoulder_idx = skel.find_bone("mixamorig_LeftArm")
	var hips_idx := skel.find_bone("mixamorig_Hips")

	var anim: Animation = anim_player.get_animation_library(anim_player.get_animation_library_list()[0]).get_animation(anim_name)
	var length: float = anim.length
	var dt := length / 40.0
	print("=== Hand-to-shoulder distance scan (right, left) over ", length, "s ===")
	var t := 0.0
	while t <= length + 0.001:
		anim_player.seek(t, true)
		await get_tree().process_frame
		var right_hand: Vector3 = skel.get_bone_global_pose(right_hand_idx).origin
		var right_shoulder: Vector3 = skel.get_bone_global_pose(right_shoulder_idx).origin
		var left_hand: Vector3 = skel.get_bone_global_pose(left_hand_idx).origin
		var left_shoulder: Vector3 = skel.get_bone_global_pose(left_shoulder_idx).origin
		var hips: Vector3 = skel.get_bone_global_pose(hips_idx).origin
		print("t=%.3f right_dist=%.3f left_dist=%.3f hips_y=%.3f" % [t, right_hand.distance_to(right_shoulder), left_hand.distance_to(left_shoulder), hips.y])
		t += dt

	get_tree().quit()

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
