extends Node3D
## Regression check for the new Naruto-Storm-style unarmed combo chain (see
## character/combo_data.gd and character.gd's own chain-driven punch()) --
## drives 5 punches, each landing well within the previous stage's own
## "window" (see combo_data.gd), and asserts the exact animation sequence
## (punch_right, punch_left, punch_right, punch_left, punch_heavy),
## cumulative damage matching each stage's own damage_scale, and that the
## 5th (finisher) hit is reported to the target as such via
## get_last_attack_combo_info() -> player.gd's own _punch() ->
## take_damage()'s is_finisher param. Runs headless: pure physics +
## state-machine checks against tests/dummy_damageable.gd, no rendering
## needed (see test_combo_finisher_check.gd for the DEFENDER-side
## ragdoll+recovery mechanic this does NOT exercise, since a plain dummy
## target has no Character/ragdoll rig of its own).

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")
const DummyScene := preload("res://tests/dummy_damageable.gd")

const EXPECTED_ANIMS := ["punch_right", "punch_left", "punch_right", "punch_left", "punch_heavy"]
const EXPECTED_DAMAGE_SCALES := [1.0, 1.0, 1.0, 1.0, 1.5]

# Comfortably clears PUNCH_COOLDOWN_SECONDS (0.35s) plus a couple frames of
# input lag (see test_punch_check.gd's own comment on action_press/release's
# one-frame-late just_pressed/just_released), while staying well under any
# combo stage's own 0.9s window -- same timing this file's own sibling,
# test_punch_check.gd, already established as safe.
const FRAMES_PER_HIT := 26

var _player: CharacterBody3D
var _character: Node3D
var _dummy: CharacterBody3D
var _frame := 0
var _phase := 0 # 0 = settle, 1..5 = one per hit
var _ok := true
var _checked_this_phase := false

func _ready() -> void:
	var ground := StaticBody3D.new()
	ground.collision_layer = 1
	var gcol := CollisionShape3D.new()
	var gshape := BoxShape3D.new()
	gshape.size = Vector3(200, 1.0, 200)
	gcol.shape = gshape
	gcol.position = Vector3(0, -0.5, 0)
	ground.add_child(gcol)
	add_child(ground)

	_player = PlayerScene.instantiate()
	add_child(_player)
	_character = CharacterScene.instantiate()
	_character.set_paintable(false)
	_player.add_child(_character)

	_dummy = DummyScene.new()
	_dummy.collision_layer = 4
	var dcol := CollisionShape3D.new()
	var dshape := CapsuleShape3D.new()
	dshape.radius = 0.4
	dshape.height = 1.8
	dcol.shape = dshape
	dcol.position = Vector3(0, 0.9, 0)
	_dummy.add_child(dcol)
	add_child(_dummy)
	_dummy.global_position = Vector3(0, 0, -1.5)

func _throw_punch() -> void:
	if _frame == 1:
		Input.action_press("punch")
	if _frame == 2:
		Input.action_release("punch")

func _check_hit(hit_index: int) -> void:
	var expected_anim: String = EXPECTED_ANIMS[hit_index]
	if _character.anim_player.current_animation == expected_anim:
		print("PASS: hit #", hit_index + 1, " played ", expected_anim)
	else:
		print("FAIL: hit #", hit_index + 1, " expected ", expected_anim, ", got ", _character.anim_player.current_animation)
		_ok = false

	var expected_total := 0.0
	for i in range(hit_index + 1):
		expected_total += _player.PUNCH_DAMAGE * EXPECTED_DAMAGE_SCALES[i]
	if is_equal_approx(_dummy.damage_taken, expected_total):
		print("PASS: cumulative damage after hit #", hit_index + 1, " is ", _dummy.damage_taken)
	else:
		print("FAIL: expected cumulative damage ", expected_total, ", got ", _dummy.damage_taken)
		_ok = false

	var is_last := hit_index == EXPECTED_ANIMS.size() - 1
	if _dummy.last_is_finisher == is_last:
		print("PASS: is_finisher on hit #", hit_index + 1, " is ", is_last, " as expected")
	else:
		print("FAIL: is_finisher on hit #", hit_index + 1, " was ", _dummy.last_is_finisher, ", expected ", is_last)
		_ok = false

func _physics_process(_delta: float) -> void:
	_frame += 1
	if _phase == 0:
		if _frame >= 5: # let both bodies settle first
			_phase = 1
			_frame = 0
		return

	# _phase 1..5, one per hit. Checked shortly after throwing (while the
	# jab/heavy clip is still actually playing -- punch_right/punch_left are
	# only ~0.36s/~22 frames long, matching test_punch_check.gd's own
	# check-right-after-throwing timing), THEN the rest of FRAMES_PER_HIT is
	# just waiting out the cooldown before the next hit can fire.
	_throw_punch()
	if _frame == 6 and not _checked_this_phase:
		_checked_this_phase = true
		_check_hit(_phase - 1)
	if _frame >= FRAMES_PER_HIT:
		if _phase >= EXPECTED_ANIMS.size():
			print("OVERALL: ", "PASS" if _ok else "FAIL")
			get_tree().quit(0 if _ok else 1)
		else:
			_phase += 1
			_frame = 0
			_checked_this_phase = false
