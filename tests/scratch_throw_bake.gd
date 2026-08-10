extends Node3D
## One-off bake for throw.fbx -- same "run the established retargeter once,
## write a permanent .tres" pattern scratch_mixamo_bake.gd already uses for
## every other mocap clip in this project (see its own class comment for
## why: retargeting needs a live, in-tree Skeleton3D and several awaited
## frames per sample, fine for an offline bake, not something to redo on
## every game boot). Not part of the game; run directly, once.

const RetargetScript := preload("res://tests/scratch_mixamo_retarget.gd")
const ThrowFbx := preload("res://character/mixamo_import/throw.fbx")

# Found via tests/scratch_throw_inspect.gd's own hand-to-shoulder distance
# scan (right hand, since Mixamo's own "Right" maps to our arm_l -- same
# crossed convention BONE_MAP's own comment documents, re-confirmed here:
# the RIGHT hand is the one that moves dramatically while the left stays
# comparatively still). Distance drops from a relaxed 0.628 down to a tight
# 0.389 by t=0.66 (the hand pulled in close to the shoulder -- a real
# cocked-back "about to throw" windup), then springs back out to ~0.63 by
# t=0.825-0.88 (the arm snapping forward -- the actual release), then
# settles into a near-static ~0.57-0.63 plateau for the rest of the raw
# 2.2s clip (an unremarkable follow-through/relaxed hold, not more real
# throw motion). Trimmed to [0.0, 1.1] -- keeps the full windup-into-
# release arc plus a brief settle, cutting the long redundant plateau tail
# the same way every other one-shot bake here already trims dead holds
# (see e.g. scratch_mixamo_bake.gd's own HEAVY_PUNCH_WINDOW comment).
const THROW_WINDOW := [0.0, 1.1]
# The windup's own peak (closest cock-back) -- see character.gd's own
# THROW_BOTTLE_WINDUP_TIME, which holds the animation frozen exactly here
# while aiming (right-click held), matching this same raw-clip timestamp
# 1:1 since the window itself already starts at t=0.
const WINDUP_TIME := 0.66

func _ready() -> void:
	var retargeter := RetargetScript.new()
	add_child(retargeter)

	print("baking throw_bottle...")
	var throw_anim: Animation = await retargeter.retarget(ThrowFbx, "mixamo_com", false, THROW_WINDOW[0], THROW_WINDOW[1])
	# One-shot -- character.gd's release_bottle_throw() plays this once (from
	# WINDUP_TIME onward, having already been held there by begin_bottle_aim())
	# and relies on animation_finished (see _on_animation_finished) to hand
	# control back to set_walking, same reasoning every other one-shot clip
	# here already documents.
	throw_anim.loop_mode = Animation.LOOP_NONE
	var err := ResourceSaver.save(throw_anim, "res://character/animations/throw_bottle.tres")
	if err != OK:
		print("FAIL saving throw_bottle.tres: error ", err)
	else:
		print("saved throw_bottle.tres length=", throw_anim.length, " tracks=", throw_anim.get_track_count())

	retargeter.queue_free()
	print("done.")
	get_tree().quit()
