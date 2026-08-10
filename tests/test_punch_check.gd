extends Node3D
## Regression check for Bedrock-style unarmed combat (see player.gd's own
## _punch() and character.gd's punch()/enter_combat_mode()/
## exit_combat_mode()): a punch enters combat mode, throws whichever hit the
## unarmed combo chain is currently on (see combo_data.gd -- punch_right,
## punch_left, punch_right, punch_left, punch_heavy, in that order, NOT the
## old flat PUNCH_LEFT_HAND_EVERY_NTH alternation this replaced), damages
## whatever's hit within range, is gated by a cooldown so spam-clicking
## doesn't spam damage, and combat mode itself drops back to a normal idle
## after a few seconds of no punching.
## Runs headless: pure physics + state-machine checks, no rendering needed.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")
const DummyScene := preload("res://tests/dummy_damageable.gd")

var _player: CharacterBody3D
var _character: Node3D
var _dummy: CharacterBody3D
var _frame := 0
var _phase := 0
var _ok := true
var _health_after_punch := 0.0
var _punches_landed := 0

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

	# Directly in front of the player (which faces -Z at the origin, see
	# main.gd's own facing convention) and within PUNCH_RANGE, at the same
	# capsule height/shape as the real player's own collision so the
	# camera-height raycast actually intersects it.
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

## Input.action_press()/action_release(), called from script (as opposed to
## a real hardware event), don't flip is_action_just_pressed()/
## is_action_just_released() until the FOLLOWING physics frame -- confirmed
## directly (temporary per-frame debug logging in player.gd's own input
## handling): is_action_pressed() itself goes true the same frame
## action_press() is called, but just_pressed only appears one frame later,
## and the same one-frame lag applies again to just_released after
## action_release(). player.gd's own regular-vs-heavy-punch tracking (added
## alongside the heavy punch feature) relies on just_pressed/just_released
## specifically, not is_action_pressed(), so checking results too soon after
## calling these (the previous version checked only 1 frame after release)
## raced against that lag and saw the punch as having never fired at all.
## Releasing later than pressing (rather than the same frame) keeps this a
## clean "quick tap" well under HEAVY_PUNCH_HOLD_THRESHOLD_SECONDS.
func _throw_punch() -> void:
	if _frame == 1:
		Input.action_press("punch")
	if _frame == 2:
		Input.action_release("punch")

func _cooldown_frames() -> int:
	return ceili(_player.PUNCH_COOLDOWN_SECONDS * 60.0) + 5

func _physics_process(_delta: float) -> void:
	_frame += 1
	match _phase:
		0: # let everything settle onto the ground for a few frames
			if _frame >= 5:
				_phase = 1
				_frame = 0
		1: # punch #1 should be a RIGHT jab, damage the dummy, and enter
			# combat mode
			_throw_punch()
			if _frame >= 6:
				_punches_landed += 1
				_health_after_punch = _dummy.health
				if _dummy.damage_taken == _player.PUNCH_DAMAGE:
					print("PASS: punch #1 damaged the target for PUNCH_DAMAGE (", _player.PUNCH_DAMAGE, ")")
				else:
					print("FAIL: expected ", _player.PUNCH_DAMAGE, " damage, dummy took ", _dummy.damage_taken)
					_ok = false
				if _character.anim_player.current_animation == "punch_right" and _character._is_punching:
					print("PASS: punch #1 threw the RIGHT-hand jab")
				else:
					print("FAIL: punch #1 wasn't the right-hand jab (current_animation=", _character.anim_player.current_animation, ")")
					_ok = false
				if _character._in_combat_mode:
					print("PASS: combat mode entered on the first punch")
				else:
					print("FAIL: combat mode was not entered")
					_ok = false
				_phase = 2
				_frame = 0
		2: # spam-clicking again immediately (still on cooldown) must NOT
			# deal a second hit
			_throw_punch()
			if _frame >= 6:
				if _dummy.health == _health_after_punch:
					print("PASS: punching again during cooldown dealt no extra damage")
				else:
					print("FAIL: cooldown did not block a second punch (health ", _health_after_punch, " -> ", _dummy.health, ")")
					_ok = false
				_phase = 3
				_frame = 0
		3: # wait out the cooldown (and the swing animation finishing, which
			# should hand control back to character.set_walking on its own)
			if _frame >= _cooldown_frames():
				if not _character._is_punching:
					print("PASS: punch animation finished and released control")
				else:
					print("FAIL: still marked as punching long after the animation should have finished")
					_ok = false
				_phase = 4
				_frame = 0
		4: # punch #2 should be the chain's own 2nd stage: punch_left (see
			# combo_data.gd's own "unarmed" chain -- R, L, R, L, Heavy)
			_throw_punch()
			if _frame >= 6:
				_punches_landed += 1
				var expected: float = _player.PUNCH_DAMAGE * float(_punches_landed)
				if _dummy.damage_taken == expected:
					print("PASS: punch #2 landed (total damage ", _dummy.damage_taken, ")")
				else:
					print("FAIL: expected total damage ", expected, ", got ", _dummy.damage_taken)
					_ok = false
				if _character.anim_player.current_animation == "punch_left":
					print("PASS: punch #2 advanced the combo chain to punch_left")
				else:
					print("FAIL: punch #2 should be the chain's 2nd stage, punch_left (current_animation=", _character.anim_player.current_animation, ")")
					_ok = false
				_phase = 5
				_frame = 0
		5:
			if _frame >= _cooldown_frames():
				_phase = 6
				_frame = 0
		6: # punch #3 should be the chain's own 3rd stage: punch_right again
			_throw_punch()
			if _frame >= 6:
				_punches_landed += 1
				var expected: float = _player.PUNCH_DAMAGE * float(_punches_landed)
				if _dummy.damage_taken == expected:
					print("PASS: punch #3 landed (total damage ", _dummy.damage_taken, ")")
				else:
					print("FAIL: expected total damage ", expected, ", got ", _dummy.damage_taken)
					_ok = false
				if _character.anim_player.current_animation == "punch_right":
					print("PASS: punch #3 advanced the combo chain to punch_right (R, L, R, L, Heavy chain confirmed)")
				else:
					print("FAIL: punch #3 should be the chain's 3rd stage, punch_right (current_animation=", _character.anim_player.current_animation, ")")
					_ok = false
				_phase = 7
				_frame = 0
		7: # wait past COMBAT_MODE_TIMEOUT_SECONDS with no more punching --
			# combat mode should drop back to a normal idle on its own
			if _frame == 1:
				Input.action_release("punch")
			var timeout_frames: int = ceili(_character.COMBAT_MODE_TIMEOUT_SECONDS * 60.0) + 10
			if _frame >= timeout_frames:
				if not _character._in_combat_mode:
					print("PASS: combat mode timed out back to a normal idle")
				else:
					print("FAIL: combat mode never timed out")
					_ok = false
				print("OVERALL: ", "PASS" if _ok else "FAIL")
				get_tree().quit(0 if _ok else 1)
