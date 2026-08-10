extends Node3D
## One-off bake for just the two new car seat-transition clips -- see
## scratch_mixamo_bake.gd's own entering_car/exiting_car block for why these
## specifically need include_root_rotation=true AND include_root_translation
## =true, unlike everything else that script bakes. Kept as its own tiny
## driver (rather than re-running the full scratch_mixamo_bake.gd, which
## would needlessly re-bake ~20 already-correct clips) purely so re-baking
## just these two during iteration doesn't pay for the rest.

const RetargetScript := preload("res://tests/scratch_mixamo_retarget.gd")
const EnteringCarFbx := preload("res://character/mixamo_import/entering_car.fbx")
const ExitingCarFbx := preload("res://character/mixamo_import/exiting_car.fbx")

func _save(anim: Animation, path: String) -> void:
	var err := ResourceSaver.save(anim, path)
	if err != OK:
		print("FAIL saving ", path, ": error ", err)
	else:
		print("saved ", path, " length=", anim.length, " tracks=", anim.get_track_count())

func _ready() -> void:
	var retargeter := RetargetScript.new()
	add_child(retargeter)

	print("baking entering_car...")
	var enter_car: Animation = await retargeter.retarget(EnteringCarFbx, "mixamo_com", true, -1.0, -1.0, false, true)
	enter_car.loop_mode = Animation.LOOP_NONE
	_save(enter_car, "res://character/animations/entering_car.tres")

	print("baking exiting_car...")
	var exit_car: Animation = await retargeter.retarget(ExitingCarFbx, "mixamo_com", true, -1.0, -1.0, false, true)
	exit_car.loop_mode = Animation.LOOP_NONE
	_save(exit_car, "res://character/animations/exiting_car.tres")

	retargeter.queue_free()
	print("done.")
	get_tree().quit()
