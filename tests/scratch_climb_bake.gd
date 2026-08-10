extends Node3D
## One-off bake for the 3 climbing clips -- same "run the established
## retargeter once, write a permanent .tres" pattern scratch_throw_bake.gd/
## scratch_mixamo_bake.gd already use. Not part of the game; run directly,
## once.

const RetargetScript := preload("res://tests/scratch_mixamo_retarget.gd")
const ClimbUpFbx := preload("res://character/mixamo_import/climbing_up_wall.fbx")
const ClimbDownFbx := preload("res://character/mixamo_import/climbing_down_wall.fbx")
const BracedHangFbx := preload("res://character/mixamo_import/braced_hang_to_crouch.fbx")

func _ready() -> void:
	var retargeter := RetargetScript.new()
	add_child(retargeter)

	# Looping hand-over-hand cycles -- same convention every other
	# locomotion cycle (walk/run/sprint) already uses: root translation
	# stays OFF (the default) since player.gd's own climbing code owns the
	# real vertical travel through world space explicitly (see its own
	# CLIMB_SPEED), the same way it already owns forward travel during a
	# walk/run instead of trusting the clip's own (here, net-zero-per-loop)
	# hip track. Only the in-place hand-over-hand motion comes from the clip.
	print("baking climb_up...")
	var climb_up: Animation = await retargeter.retarget(ClimbUpFbx, "mixamo_com")
	climb_up.loop_mode = Animation.LOOP_LINEAR
	_save(climb_up, "res://character/animations/climb_up.tres")

	print("baking climb_down...")
	var climb_down: Animation = await retargeter.retarget(ClimbDownFbx, "mixamo_com")
	climb_down.loop_mode = Animation.LOOP_LINEAR
	_save(climb_down, "res://character/animations/climb_down.tres")

	# One-shot mantle transition with REAL root motion (confirmed directly:
	# hips rises ~1.5 units and moves ~0.6 forward over the clip, see the
	# throwaway hip-trajectory scan this bake's own session used) -- same
	# include_root_translation=true treatment entering_car.fbx/exiting_car.fbx
	# already established for "the whole pelvis moves through a real 3D arc,
	# not just a vertical drop" clips. player.gd plays this once when
	# topping out a climb and waits for animation_finished (same pattern
	# throw_bottle.tres's own release already uses) before snapping the
	# real physics body up onto the ledge and resuming normal control --
	# the skeletal root motion here is what visually SELLS the mantle in
	# the meantime.
	print("baking climb_top_out...")
	var top_out: Animation = await retargeter.retarget(BracedHangFbx, "mixamo_com", false, -1.0, -1.0, false, true)
	top_out.loop_mode = Animation.LOOP_NONE
	_save(top_out, "res://character/animations/climb_top_out.tres")

	retargeter.queue_free()
	print("done.")
	get_tree().quit()

func _save(anim: Animation, path: String) -> void:
	var err := ResourceSaver.save(anim, path)
	if err != OK:
		print("FAIL saving ", path, ": error ", err)
	else:
		print("saved ", path, " length=", anim.length, " tracks=", anim.get_track_count())
