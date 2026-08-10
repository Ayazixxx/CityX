extends Node3D
## One-off inspection: confirm the 3 new climbing FBX clips use the same
## Mixamo bone naming / "mixamo_com" take convention as every other clip
## in this project's pipeline, and report length + whether the hips bone
## actually translates (root motion) so retarget() calls downstream know
## whether include_root_translation is needed. Not part of the game.

const ClimbUpFbx := preload("res://character/mixamo_import/climbing_up_wall.fbx")
const ClimbDownFbx := preload("res://character/mixamo_import/climbing_down_wall.fbx")
const BracedHangFbx := preload("res://character/mixamo_import/braced_hang_to_crouch.fbx")

func _ready() -> void:
	for entry in [["climbing_up_wall", ClimbUpFbx], ["climbing_down_wall", ClimbDownFbx], ["braced_hang_to_crouch", BracedHangFbx]]:
		await _inspect(entry[0], entry[1])
	get_tree().quit()

func _inspect(label: String, scene: PackedScene) -> void:
	print("=== ", label, " ===")
	var inst := scene.instantiate()
	add_child(inst)

	var skel := _find_skeleton(inst)
	var anim_player := _find_anim_player(inst)
	if skel == null or anim_player == null:
		print("  MISSING skeleton or anim player!")
		inst.queue_free()
		return

	print("  bone count: ", skel.get_bone_count(), " sample bones: ", skel.get_bone_name(0), ", ", skel.get_bone_name(skel.get_bone_count() / 2))
	var hips_idx := skel.find_bone("mixamorig_Hips")
	print("  has mixamorig_Hips: ", hips_idx != -1)

	for lib_name in anim_player.get_animation_library_list():
		var lib := anim_player.get_animation_library(lib_name)
		for anim_name in lib.get_animation_list():
			var anim: Animation = lib.get_animation(anim_name)
			print("  lib='", lib_name, "' anim='", anim_name, "' length=", anim.length, " tracks=", anim.get_track_count())

	# Root motion check: sample hips position at t=0 and t=length, see if it moved.
	if hips_idx != -1:
		var anim_name: String = anim_player.get_animation_library_list()[0]
		var lib := anim_player.get_animation_library(anim_name)
		var first_anim_name: String = lib.get_animation_list()[0]
		anim_player.play(first_anim_name)
		anim_player.speed_scale = 0.0
		anim_player.seek(0.0, true)
		await get_tree().process_frame
		var hips_start: Vector3 = skel.get_bone_global_pose(hips_idx).origin
		var anim: Animation = lib.get_animation(first_anim_name)
		anim_player.seek(anim.length, true)
		await get_tree().process_frame
		var hips_end: Vector3 = skel.get_bone_global_pose(hips_idx).origin
		print("  hips start=", hips_start, " end=", hips_end, " delta=", hips_end - hips_start)

	inst.queue_free()

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
