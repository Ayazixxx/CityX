extends Node3D
## One-off bake: runs the (now-verified) Mixamo retargeting once for every
## mocap clip the game uses and writes the results as permanent Animation
## .tres resources under character/animations/, so the game loads pre-baked
## data at runtime instead of re-running retargeting (which needs a live,
## in-tree Skeleton3D and several awaited frames per sample -- fine for a
## one-off offline bake, not something to redo on every game boot). Not
## part of the game; run directly, once (or again if a clip changes).

const RetargetScript := preload("res://tests/scratch_mixamo_retarget.gd")
const IdleFbx := preload("res://character/mixamo_import/boxing_idle.fbx")
const RightJabFbx := preload("res://character/mixamo_import/boxing_right_jab.fbx")
const LeftJabFbx := preload("res://character/mixamo_import/boxing_left_jab.fbx")
const WalkingFbx := preload("res://character/mixamo_import/walking.fbx")
const SprintFbx := preload("res://character/mixamo_import/sprint.fbx")
const StandIdleFbx := preload("res://character/mixamo_import/idle.fbx")
const JumpFbx := preload("res://character/mixamo_import/jump.fbx")
const JumpSprintFbx := preload("res://character/mixamo_import/jump_sprint.fbx")
const StandingToCrouchFbx := preload("res://character/mixamo_import/standing_to_crouch.fbx")
const CrouchToIdleFbx := preload("res://character/mixamo_import/crouch_to_idle.fbx")
const CrouchWalkFbx := preload("res://character/mixamo_import/crouch_walk.fbx")
const HeavyPunchFbx := preload("res://character/mixamo_import/heavy_punch.fbx")
const DashBackwardFbx := preload("res://character/mixamo_import/dash_backward.fbx")
const BodyBlockFbx := preload("res://character/mixamo_import/body_block.fbx")
const WalkBackwardFbx := preload("res://character/mixamo_import/walk_backward.fbx")
const WalkLeftFbx := preload("res://character/mixamo_import/walk_left.fbx")
const WalkRightFbx := preload("res://character/mixamo_import/walk_right.fbx")
const EnteringCarFbx := preload("res://character/mixamo_import/entering_car.fbx")
const ExitingCarFbx := preload("res://character/mixamo_import/exiting_car.fbx")

# Peak-extension times found via scratch_mixamo_peak.gd's own scan (hand-to-
# shoulder distance, per clip): right jab peaks at t=0.7 of a 2.133s clip,
# left jab at t=0.433. This is a WINDOW into the source timeline (not a
# time-rescale) that both cuts the long guard-hold before/after the actual
# strike AND keeps the whole clip close to player.gd's own
# PUNCH_COOLDOWN_SECONDS (0.35s) -- an earlier wider +-0.35s window (0.7s
# total) produced a punch animation LONGER than the cooldown that gates
# throwing the next one, and since character.gd's _is_punching blocks ALL
# movement input for the animation's FULL duration (not just the cooldown
# window), that left the player unable to move for 0.7s after a single
# punch if they didn't immediately spam another -- confirmed by
# tests/test_punch_check.gd, which failed with "still marked as punching
# long after the animation should have finished" against the wider window.
const RIGHT_JAB_WINDOW := [0.52, 0.88]
const LEFT_JAB_WINDOW := [0.253, 0.613]

# jump.fbx's own hips-height scan (tests/scratch_jump_scan.gd, since deleted)
# showed a real crouch-anticipation BEFORE takeoff: hips height dips from a
# standing 1.03 down to a low of 0.60 by t=0.5, THEN rises to a jump apex of
# 1.45 around t=1.0. Our own physics applies JUMP_VELOCITY the instant jump
# is pressed -- no gradual crouch-then-spring -- so playing that anticipation
# from t=0 would show the character visibly sinking into the ground for half
# a second BEFORE the jump the physics already started, backwards from what
# actually happened. Trimming to start at the crouch's own low point (right
# as upward motion begins) lines the animation's own "launch" moment up with
# the instant takeoff our physics already applies. The back half (landing +
# recovery) is intentionally left in past the game's own ~0.92s real air
# time (2*JUMP_VELOCITY/GRAVITY) -- rather than hand-tuning a second trim
# point, player.gd calls character.land() the instant it actually detects
# touching down, which ends the jump animation state early regardless of how
# much of the clip is left (see character.gd's own land()).
const JUMP_WINDOW := [0.5, 2.6]
# jump_sprint.fbx's own scan showed a clean single arc with no separate
# anticipation crouch -- starts at a natural mid-stride low point (a running
# jump launches out of a stride, not a standing crouch), rises to a clear
# apex, and settles -- so the full clip is used untrimmed.

# heavy_punch.fbx's own hand-to-shoulder scan (both arms, since it wasn't
# yet known which one throws it) showed the RIGHT arm reaching further
# (peak dist 0.56 at t=1.3) than the left (0.51 at t=1.03) -- same crossed
# mapping as the regular jabs (Mixamo Right -> our arm_l). A wider window
# than the regular punches (0.6s vs 0.36s) on purpose: this is a slower,
# more committed haymaker, not a quick jab, and unlike jump's own landing-
# recovery tail there's no separate "end it early" hook for this one, so
# the whole windup-strike-recovery needs to already fit in the window.
const HEAVY_PUNCH_WINDOW := [0.95, 1.55]

# body_block.fbx's own hand-to-head-distance scan (both hands, plus hips
# height): a raw 3.43s clip that starts and settles back into a relaxed
# stance (hands ~0.3-0.4m from head) -- the actual guard only rises during
# t=0.75-1.5ish, both hands closing to their nearest point around t=1.3-1.4
# (~0.24-0.28m) while hips visibly dip too (0.92 down to 0.84), reading as a
# genuine "brace/duck behind raised hands" block rather than a plain idle.
# Trimmed to just that rise-to-peak window, same "cut the long hold before/
# after the actual defensive motion" reasoning as the punch windows above --
# ends right at the tightest guard point rather than including the
# recovery back to a relaxed stance, since (like every other one-shot here)
# it holds on its own last frame once done, and player.gd's own block
# window is exactly this clip's length (see character.gd's block()) -- a
# longer trim would mean either a slower-to-raise guard or a block window
# that outlasts the visible "hands are actually up" pose.
const BODY_BLOCK_WINDOW := [0.75, 1.45]

func _ready() -> void:
	var retargeter := RetargetScript.new()
	add_child(retargeter)

	print("baking boxing_idle...")
	var idle: Animation = await retargeter.retarget(IdleFbx, "mixamo_com")
	_save(idle, "res://character/animations/boxing_idle.tres")

	print("baking boxing_right_jab...")
	var rjab: Animation = await retargeter.retarget(RightJabFbx, "mixamo_com", false, RIGHT_JAB_WINDOW[0], RIGHT_JAB_WINDOW[1])
	# One-shot, not looping -- character.gd's punch() plays this once per
	# swing and relies on the animation_finished signal (see
	# _on_animation_finished) to know when to hand control back to
	# set_walking. LOOP_LINEAR (retarget()'s own default, right for the held
	# idle stance) would never fire that signal at all, permanently stuck
	# with _is_punching true and the player unable to move or punch again.
	rjab.loop_mode = Animation.LOOP_NONE
	_save(rjab, "res://character/animations/boxing_right_jab.tres")

	print("baking boxing_left_jab...")
	var ljab: Animation = await retargeter.retarget(LeftJabFbx, "mixamo_com", false, LEFT_JAB_WINDOW[0], LEFT_JAB_WINDOW[1])
	ljab.loop_mode = Animation.LOOP_NONE
	_save(ljab, "res://character/animations/boxing_left_jab.tres")

	# Full clip, no trim/rescale -- both are already single natural gait
	# cycles (Mixamo's own "mixamo_com" trim), and unlike the punches
	# there's no cooldown/lockout constraint forcing a shorter window; a
	# real walk/run cycle should just play at its own real-world cadence.
	# loop_mode defaults to LOOP_LINEAR in retarget() already, which is
	# exactly right here (continuously cycling while walking/running).
	print("baking walk...")
	var walk: Animation = await retargeter.retarget(WalkingFbx, "mixamo_com")
	_save(walk, "res://character/animations/walk.tres")

	print("baking run...")
	var run: Animation = await retargeter.retarget(SprintFbx, "mixamo_com")
	_save(run, "res://character/animations/run.tres")

	# A subtle, held sway/breathing loop for normal (non-combat) standing --
	# same full-clip, no-trim treatment as walk/run above.
	print("baking idle...")
	var idle_stand: Animation = await retargeter.retarget(StandIdleFbx, "mixamo_com")
	_save(idle_stand, "res://character/animations/idle.tres")

	print("baking jump...")
	var jump: Animation = await retargeter.retarget(JumpFbx, "mixamo_com", false, JUMP_WINDOW[0], JUMP_WINDOW[1])
	# One-shot, not looping -- same reasoning as the punches: character.gd's
	# jump()/land() rely on _is_jumping being clearable (via land() or this
	# animation naturally finishing), and a looping clip would never let
	# set_walking take back over.
	jump.loop_mode = Animation.LOOP_NONE
	_save(jump, "res://character/animations/jump.tres")

	print("baking jump_sprint...")
	var jump_sprint: Animation = await retargeter.retarget(JumpSprintFbx, "mixamo_com")
	jump_sprint.loop_mode = Animation.LOOP_NONE
	_save(jump_sprint, "res://character/animations/jump_sprint.tres")

	# All three crouch clips use retarget_hip_height=true -- unlike every
	# other animation here, a crouch is fundamentally about the whole pelvis
	# physically dropping, a TRANSLATION our rotation-only retargeting can't
	# capture at all (confirmed directly: our own rig's hips showed an exact
	# 0.0 height change with rotation alone, vs the source's own ~46% drop).
	# See retarget()'s own comment on retarget_hip_height for the fix.
	print("baking standing_to_crouch...")
	var s2c: Animation = await retargeter.retarget(StandingToCrouchFbx, "mixamo_com", false, -1.0, -1.0, true)
	# One-shot -- character.gd's start_crouch() plays this once and holds on
	# the last (crouched) frame once it finishes, same LOOP_NONE reasoning
	# as the punches/jump above.
	s2c.loop_mode = Animation.LOOP_NONE
	_save(s2c, "res://character/animations/standing_to_crouch.tres")

	print("baking crouch_to_idle...")
	var c2i: Animation = await retargeter.retarget(CrouchToIdleFbx, "mixamo_com", false, -1.0, -1.0, true)
	c2i.loop_mode = Animation.LOOP_NONE
	_save(c2i, "res://character/animations/crouch_to_idle.tres")

	print("baking crouch_walk...")
	var cwalk: Animation = await retargeter.retarget(CrouchWalkFbx, "mixamo_com", false, -1.0, -1.0, true)
	# Looping, like walk/run -- loop_mode defaults to LOOP_LINEAR already.
	_save(cwalk, "res://character/animations/crouch_walk.tres")

	print("baking heavy_punch...")
	var heavy: Animation = await retargeter.retarget(HeavyPunchFbx, "mixamo_com", false, HEAVY_PUNCH_WINDOW[0], HEAVY_PUNCH_WINDOW[1])
	heavy.loop_mode = Animation.LOOP_NONE
	_save(heavy, "res://character/animations/heavy_punch.tres")

	# Plain rotation-only retargeting (no retarget_hip_height) -- unlike the
	# crouch clips, a pre-flight ground check (sampling the lowest leg point
	# across the whole clip on our own rig, since deleted) showed the dash
	# lunge keeps the feet comfortably ABOVE y=0 throughout (min +0.05),
	# never below, so there's no clipping to correct for. A forward
	# counterpart (Dashx.fbx) was also retargeted this same way and
	# confirmed equally clean, but got dropped after review -- the animation
	# itself just didn't look right, not a retargeting problem.
	print("baking dash_backward...")
	var dash_back: Animation = await retargeter.retarget(DashBackwardFbx, "mixamo_com")
	# One-shot -- character.gd's dash() plays this once per burst and relies
	# on animation_finished (see _on_animation_finished) to hand control back
	# to set_walking, same reasoning as the punches/jump.
	dash_back.loop_mode = Animation.LOOP_NONE
	_save(dash_back, "res://character/animations/dash_backward.tres")

	print("baking block...")
	var block: Animation = await retargeter.retarget(BodyBlockFbx, "mixamo_com", false, BODY_BLOCK_WINDOW[0], BODY_BLOCK_WINDOW[1])
	# One-shot -- character.gd's block() plays this once per right-click and
	# relies on animation_finished (see _on_animation_finished) to hand
	# control back to set_walking, same reasoning as the punches/jump/dash.
	block.loop_mode = Animation.LOOP_NONE
	_save(block, "res://character/animations/block.tres")

	# Full clip, no trim, LOOP_LINEAR -- same treatment as walk/run above:
	# real gait cycles at their own native mocap cadence, not one-shot
	# actions. Used for player.gd's own S/A/D-held movement once the
	# character's facing is locked to the camera direction instead of
	# turning to face movement (see player.gd's own class comment) --
	# without that decoupling these would never be reachable, since the
	# old turn-to-face system meant "moving backward" always looked
	# identical to turning around and walking forward.
	print("baking walk_backward...")
	var walk_back: Animation = await retargeter.retarget(WalkBackwardFbx, "mixamo_com")
	_save(walk_back, "res://character/animations/walk_backward.tres")

	print("baking walk_left...")
	var walk_left: Animation = await retargeter.retarget(WalkLeftFbx, "mixamo_com")
	_save(walk_left, "res://character/animations/walk_left.tres")

	print("baking walk_right...")
	var walk_right: Animation = await retargeter.retarget(WalkRightFbx, "mixamo_com")
	_save(walk_right, "res://character/animations/walk_right.tres")

	# Both car seat-transition clips use include_root_rotation=true AND
	# include_root_translation=true, unlike everything else baked above --
	# a real Blender-side inspection of the raw FBX (not just our own rig)
	# showed the source's own hips bone moving ~1.9 units across TWO axes
	# over each clip's length, not just dropping in height the way a crouch
	# does -- this is a real "walk-and-sit"/"stand-and-step-away" pelvis
	# arc, not a pose reachable by joint rotation alone against a fixed
	# root. See retarget()'s own comment on include_root_translation for
	# the mechanism (a scaled copy of the source's own hips delta-from-rest,
	# generalizing retarget_hip_height's existing Y-only crouch trick to all
	# 3 axes). One-shot (LOOP_NONE) -- car.gd plays each once per seat
	# transition and relies on animation_finished to know when to complete
	# the actual possession handoff (hide/reparent/collision), same
	# reasoning as every other one-shot clip above.
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

func _save(anim: Animation, path: String) -> void:
	var err := ResourceSaver.save(anim, path)
	if err != OK:
		print("FAIL saving ", path, ": error ", err)
	else:
		print("saved ", path, " length=", anim.length, " tracks=", anim.get_track_count())
