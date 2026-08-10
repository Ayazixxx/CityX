extends Node3D
## One-off bake of the 13 two-handed-sword mocap clips onto our rig, same
## process/tooling as scratch_mixamo_bake.gd (see its own header comment) --
## confirmed via scratch_2hsw_inspect_all.gd that every clip in this pack
## uses the same mixamorig_* bone names and "mixamo_com" animation name the
## existing RetargetScript/BONE_MAP already expect, so no new mapping work
## was needed, just new source files. Not part of the game; run directly.

const RetargetScript := preload("res://tests/scratch_mixamo_retarget.gd")

const IdleFbx := preload("res://character/mixamo_import/2hsw_idle.fbx")
const WalkFbx := preload("res://character/mixamo_import/2hsw_walk.fbx")
# Added after the initial 13-clip bake, once a forward sprint clip was
# supplied -- same pack/naming convention (confirmed via a one-off
# inspection, since deleted, that it also uses mixamorig_* bones and a
# "mixamo_com" clip), just a later addition.
const SprintFbx := preload("res://character/mixamo_import/2hsw_sprint.fbx")
const LeftFbx := preload("res://character/mixamo_import/2hsw_left.fbx")
const RightFbx := preload("res://character/mixamo_import/2hsw_right.fbx")
const BackwardWalkFbx := preload("res://character/mixamo_import/2hsw_backwardwalk.fbx")
const CrouchFbx := preload("res://character/mixamo_import/2hsw_crouch.fbx")
const UncrouchFbx := preload("res://character/mixamo_import/2hsw_uncrouch.fbx")
const JumpFbx := preload("res://character/mixamo_import/2hsw_jump.fbx")
const AttackFbx := preload("res://character/mixamo_import/2hsw_attack.fbx")
# A combo follow-up, added later -- see character.gd's own
# SWORD_COMBO_WINDOW_SECONDS/punch() for how it gets triggered (a second
# click within the window after the first attack).
const Attack2Fbx := preload("res://character/mixamo_import/2hsw_attack2.fbx")
const ChargedAttackFbx := preload("res://character/mixamo_import/2hsw_chargedattack.fbx")
const BlockingFbx := preload("res://character/mixamo_import/2hsw_blocking.fbx")
const BlockImpactFbx := preload("res://character/mixamo_import/2hsw_blockimpact.fbx")
const ImpactFbx := preload("res://character/mixamo_import/2hsw_impact.fbx")

# chargedattack.fbx's own hand-to-hip distance scan (tests/
# scratch_2hsw_peak_scan.gd, since deleted) showed a clear 3-phase motion
# across its raw 4.8s length: a windup raising the sword (t=0-0.5), then a
# long, essentially STATIC held-high "charging" pose (t=0.5-1.6, combined
# hand distance plateaued ~1.7-1.86 the whole time) before the real
# committed downward lunge -- hips visibly dropping ~0.4m (0.98 -> ~0.57)
# from t=1.6-2.3, held low through the strike/follow-through (t=2.3-3.6),
# then rising back to a standing recovery by t=4.3. Our own combat model
# already represents "charging" through real-time input holding (see
# player.gd's own HEAVY_PUNCH_HOLD_THRESHOLD_SECONDS) -- the animation
# itself only needs to play once input is RELEASED, so that static held
# phase is pure dead weight to trim, same "cut the long hold before/after
# the actual motion" reasoning heavy_punch.fbx's own trim already used.
# Keeps the full dramatic lunge-drop-recover sequence intact (unlike the
# quick regular jabs, a "charged" attack is supposed to read as slower and
# more committed) rather than trimming as tight as the ~0.6s regular
# combat clips.
const CHARGED_ATTACK_WINDOW := [1.5, 4.3]

func _ready() -> void:
	var retargeter := RetargetScript.new()
	add_child(retargeter)

	print("baking sword_idle...")
	var idle: Animation = await retargeter.retarget(IdleFbx, "mixamo_com")
	_save(idle, "res://character/animations/sword_idle.tres")

	print("baking sword_walk...")
	var walk: Animation = await retargeter.retarget(WalkFbx, "mixamo_com")
	_save(walk, "res://character/animations/sword_walk.tres")

	# Only a FORWARD sprint clip exists (matches the fist system's own
	# RunAnim -- backward/strafing never had a running variant either, see
	# WalkBackwardAnim's own comment), so this is only ever selected from
	# SWORD_DIRECTIONAL_ANIM's "forward" fallback, same as "run" is for the
	# unarmed system.
	print("baking sword_run...")
	var run: Animation = await retargeter.retarget(SprintFbx, "mixamo_com")
	_save(run, "res://character/animations/sword_run.tres")

	print("baking sword_walk_left...")
	var left: Animation = await retargeter.retarget(LeftFbx, "mixamo_com")
	_save(left, "res://character/animations/sword_walk_left.tres")

	print("baking sword_walk_right...")
	var right: Animation = await retargeter.retarget(RightFbx, "mixamo_com")
	_save(right, "res://character/animations/sword_walk_right.tres")

	print("baking sword_walk_backward...")
	var backward: Animation = await retargeter.retarget(BackwardWalkFbx, "mixamo_com")
	_save(backward, "res://character/animations/sword_walk_backward.tres")

	print("baking sword_standing_to_crouch...")
	var s2c: Animation = await retargeter.retarget(CrouchFbx, "mixamo_com", false, -1.0, -1.0, true)
	# One-shot -- same reasoning as the fist system's own standing_to_crouch:
	# character.gd's start_crouch() plays this once and holds on the last
	# (crouched) frame, relying on LOOP_NONE + player.gd's own edge-detected
	# crouch handling rather than looping.
	s2c.loop_mode = Animation.LOOP_NONE
	_save(s2c, "res://character/animations/sword_standing_to_crouch.tres")

	print("baking sword_crouch_to_idle...")
	var c2i: Animation = await retargeter.retarget(UncrouchFbx, "mixamo_com", false, -1.0, -1.0, true)
	c2i.loop_mode = Animation.LOOP_NONE
	_save(c2i, "res://character/animations/sword_crouch_to_idle.tres")

	print("baking sword_jump...")
	var jump: Animation = await retargeter.retarget(JumpFbx, "mixamo_com")
	jump.loop_mode = Animation.LOOP_NONE
	_save(jump, "res://character/animations/sword_jump.tres")

	print("baking sword_attack...")
	var attack: Animation = await retargeter.retarget(AttackFbx, "mixamo_com")
	attack.loop_mode = Animation.LOOP_NONE
	_save(attack, "res://character/animations/sword_attack.tres")

	print("baking sword_attack2...")
	var attack2: Animation = await retargeter.retarget(Attack2Fbx, "mixamo_com")
	attack2.loop_mode = Animation.LOOP_NONE
	_save(attack2, "res://character/animations/sword_attack2.tres")

	print("baking sword_heavy_attack...")
	var charged: Animation = await retargeter.retarget(ChargedAttackFbx, "mixamo_com", false, CHARGED_ATTACK_WINDOW[0], CHARGED_ATTACK_WINDOW[1])
	charged.loop_mode = Animation.LOOP_NONE
	_save(charged, "res://character/animations/sword_heavy_attack.tres")

	print("baking sword_block...")
	var block: Animation = await retargeter.retarget(BlockingFbx, "mixamo_com")
	block.loop_mode = Animation.LOOP_NONE
	_save(block, "res://character/animations/sword_block.tres")

	print("baking sword_block_impact...")
	var block_impact: Animation = await retargeter.retarget(BlockImpactFbx, "mixamo_com")
	block_impact.loop_mode = Animation.LOOP_NONE
	_save(block_impact, "res://character/animations/sword_block_impact.tres")

	print("baking sword_impact...")
	var impact: Animation = await retargeter.retarget(ImpactFbx, "mixamo_com")
	impact.loop_mode = Animation.LOOP_NONE
	_save(impact, "res://character/animations/sword_impact.tres")

	retargeter.queue_free()
	print("done.")
	get_tree().quit()

func _save(anim: Animation, path: String) -> void:
	var err := ResourceSaver.save(anim, path)
	if err != OK:
		print("FAIL saving ", path, ": error ", err)
	else:
		print("saved ", path, " length=", anim.length, " tracks=", anim.get_track_count())
