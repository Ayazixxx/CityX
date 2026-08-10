extends Node3D
## One-off bake for the 4 intro-cutscene clips (player/intro_cutscene.gd) --
## same retargeting tooling as scratch_mixamo_bake.gd. Kept as its own tiny
## driver rather than extending that ~19-clip file, same reasoning
## scratch_car_anim_bake.gd already established for its own addition:
## re-baking just these during iteration shouldn't pay for ~20 unrelated
## already-correct clips. Not part of the game; run directly, once (or
## again if a clip/flag choice changes below).

const RetargetScript := preload("res://tests/scratch_mixamo_retarget.gd")
const FallingFbx := preload("res://character/mixamo_import/falling.fbx")
const FallingFlatImpactFbx := preload("res://character/mixamo_import/falling_flat_impact.fbx")
const GettingUpFbx := preload("res://character/mixamo_import/getting_up.fbx")
const LookingAroundFbx := preload("res://character/mixamo_import/looking_around.fbx")

func _save(anim: Animation, path: String) -> void:
	var err := ResourceSaver.save(anim, path)
	if err != OK:
		print("FAIL saving ", path, ": error ", err)
	else:
		print("saved ", path, " length=", anim.length, " tracks=", anim.get_track_count())

func _ready() -> void:
	var retargeter := RetargetScript.new()
	add_child(retargeter)

	# include_root_rotation=true -- a real reported bug on the first
	# (rotation-frozen-at-hips) bake: "he kinda of falls at an angle" instead
	# of flat on his stomach. Root-caused directly against retarget()'s own
	# code (scratch_mixamo_retarget.gd): with include_root_rotation=false,
	# the "hips" bone's rotation is hard-frozen to Basis() (pure identity,
	# i.e. upright) regardless of what the source clip's own hips actually
	# do, and "spine" (hips' own child) computes its LOCAL rotation as
	# parent_global.inverse() * calibrated -- with a frozen-identity parent,
	# that reduces to just spine's own delta, losing whatever ADDITIONAL
	# pitch the source's own hips contributed to the overall dive lean. A
	# real Mixamo "Falling" clip pitches the dive mostly through the HIPS,
	# with spine only adding a smaller curve on top -- freezing hips
	# therefore under-rotates the whole pose, reading as a partial diagonal
	# lean rather than a full flat prone dive. No matching root TRANSLATION
	# concern here (see include_root_rotation's own doc comment on why THAT
	# was a real problem for boxing's weight-shifting footwork) -- an
	# airborne freefall pose has no ground-contact weight shift to go out of
	# sync with, unlike a boxing stance. The real ~300m vertical descent
	# itself is still driven externally by intro_cutscene.gd's own Tween on
	# global_position:y (see its own class comment on why root motion can't
	# do THAT: the only two clips with real root translation, entering_car/
	# exiting_car, are only proven for small pelvis arcs, not large-scale
	# world-space movement) -- this flag only fixes the ROTATION lean, not
	# position. LOOP_LINEAR since this needs to cycle for up to 15s and the
	# source clip is surely much shorter.
	print("baking falling...")
	var falling: Animation = await retargeter.retarget(FallingFbx, "mixamo_com", true)
	falling.loop_mode = Animation.LOOP_LINEAR
	_save(falling, "res://character/animations/falling.tres")

	# Rotation-only FIRST -- both of these involve a real hip-height change
	# (airborne -> lying flat -> standing), structurally closer to
	# standing_to_crouch/crouch_to_idle (which needed retarget_hip_height=
	# true -- rotation alone gave "an exact 0.0 height change... our own
	# rig's hips" per that clip's own documented bug history) than to a
	# plain in-place pose. Verify live once baked: if the character floats
	# at standing height instead of reading as grounded, re-bake with
	# retarget_hip_height=true (5th positional arg, same Y-only mechanism
	# the crouch clips use), escalating to include_root_translation=true
	# (+ likely include_root_rotation=true, 3rd/7th args) only if that's
	# still not enough, matching entering_car/exiting_car's own "real 3D
	# pelvis arc" precedent -- which would also need a matching hips reset
	# in character.gd's own _on_animation_finished (mirror its existing
	# exiting_car/climb_top_out blocks) once getting_up finishes, or the
	# body stays visibly skewed. One-shot (LOOP_NONE) either way.
	print("baking falling_flat_impact...")
	var impact: Animation = await retargeter.retarget(FallingFlatImpactFbx, "mixamo_com")
	impact.loop_mode = Animation.LOOP_NONE
	_save(impact, "res://character/animations/falling_flat_impact.tres")

	print("baking getting_up...")
	var getting_up: Animation = await retargeter.retarget(GettingUpFbx, "mixamo_com")
	getting_up.loop_mode = Animation.LOOP_NONE
	_save(getting_up, "res://character/animations/getting_up.tres")

	# A standing-pose variant (turning head/body to look around), not a
	# ground-contact transition -- plain rotation-only should already be
	# right here, same as boxing_idle/idle.
	print("baking looking_around...")
	var looking_around: Animation = await retargeter.retarget(LookingAroundFbx, "mixamo_com")
	looking_around.loop_mode = Animation.LOOP_NONE
	_save(looking_around, "res://character/animations/looking_around.tres")

	retargeter.queue_free()
	print("done.")
	get_tree().quit()
