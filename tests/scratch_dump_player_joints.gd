extends Node3D
## Prints where each joint of the real Godot character is (character space) in a few poses, to compare with the Unity port's joints.

var _character: Node3D

func _ready() -> void:
	_character = load("res://character/character.tscn").instantiate()
	add_child(_character)
	await get_tree().create_timer(0.5).timeout
	for pose in ["idle", "walk", "run"]:
		match pose:
			"idle": _character.set_walking(false)
			"walk": _character.set_walking(true, false)
			"run": _character.set_walking(true, true)
		await get_tree().create_timer(1.0).timeout
		await get_tree().process_frame
		print("== ", pose, " anim=", _character.anim_player.current_animation, " t=", snappedf(_character.anim_player.current_animation_position, 0.01))
		var skeleton: Skeleton3D = _character._body_skeleton
		for i in range(skeleton.get_bone_count()):
			var name := skeleton.get_bone_name(i)
			var o := skeleton.get_bone_global_pose(i).origin
			var q := skeleton.get_bone_pose_rotation(i)
			print("  ", name, " pos=(", snappedf(o.x, 0.001), ",", snappedf(o.y, 0.001), ",", snappedf(o.z, 0.001), ") rot=(", snappedf(q.x, 0.001), ",", snappedf(q.y, 0.001), ",", snappedf(q.z, 0.001), ",", snappedf(q.w, 0.001), ")")
	get_tree().quit(0)
