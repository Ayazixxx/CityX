extends Node3D
## Regression check for the new Naruto-Storm-style hit-lock mechanic (see
## player.gd's own _hitstun_remaining): landing an unblocked hit locks the
## DEFENDER out of movement/punch input for HITSTUN_SECONDS (scaled per
## combo stage, see combo_data.gd), UNLESS they block or dash out of it --
## those two escapes are deliberately left ungated and each actively zeroes
## _hitstun_remaining the instant they fire (see take_damage()/block-input/
## _try_start_dash's own comments). take_damage() is called directly to
## APPLY the hitstun (isolating the DEFENDER-side mechanic, same "poke the
## real method directly rather than staging a full second attacking Player"
## approach test_sword_hit_interrupt_check.gd already uses), but the escapes
## themselves are driven through the REAL Input.action_press()/
## _physics_process() pipeline via this node's own auto-invoked
## _physics_process callback (matching test_punch_check.gd's own phase
## pattern) -- manually calling player.gd's _physics_process() in a tight
## synchronous loop does NOT work for just_pressed-gated input (block/punch):
## is_action_pressed() updates immediately, but is_action_just_pressed()
## only flips on a REAL engine frame boundary, which requires the SceneTree's
## own automatic per-frame callback, not a manually-driven one. Runs
## headless: pure physics + input-polling checks, no rendering needed.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _player: CharacterBody3D
var _character: Node3D
var _frame := 0
var _phase := 0
var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

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

func _physics_process(_delta: float) -> void:
	_frame += 1
	match _phase:
		0: # let the body settle onto the ground
			if _frame >= 5:
				_phase = 1
				_frame = 0
		1: # a landed, unblocked hit sets the hitstun lock
			if _frame == 1:
				_player.take_damage(10.0, null, _player.HITSTUN_SECONDS, false)
				if _player._hitstun_remaining > 0.0:
					print("PASS: a landed hit sets _hitstun_remaining > 0")
				else:
					_fail("_hitstun_remaining wasn't set by a landed hit")
				Input.action_press("move_forward")
			if _frame >= 3:
				if is_equal_approx(_player.velocity.x, 0.0) and is_equal_approx(_player.velocity.z, 0.0):
					print("PASS: WASD produces no movement while locked in a combo")
				else:
					_fail("player moved while hitstun-locked (velocity=%s)" % _player.velocity)
				Input.action_release("move_forward")
				_phase = 2
				_frame = 0
		2: # punch input is rejected while locked
			if _frame == 1:
				Input.action_press("punch")
			if _frame == 2:
				Input.action_release("punch")
			if _frame >= 6:
				if not _character._is_punching:
					print("PASS: punch input is rejected while locked in a combo")
				else:
					_fail("a punch went through while hitstun-locked")
				_phase = 3
				_frame = 0
		3: # block is one of the two named escapes -- still usable, and clears the lock
			if _frame == 1:
				Input.action_press("block")
			if _frame >= 3:
				if _character.is_blocking() and _player._hitstun_remaining <= 0.0:
					print("PASS: blocking still works while hitstun-locked, and clears the lock")
				else:
					_fail("block didn't go through / didn't clear the lock (is_blocking=%s hitstun_remaining=%s)" % [_character.is_blocking(), _player._hitstun_remaining])
				Input.action_release("block")
				_character._on_animation_finished("block")
				_phase = 4
				_frame = 0
		4: # dash is the other named escape -- still usable, clears the lock, and grants i-frames
			if _frame == 1:
				_player.take_damage(10.0, null, _player.HITSTUN_SECONDS, false)
				if _player._hitstun_remaining <= 0.0:
					_fail("setup didn't re-apply hitstun before the dash check")
				_player._try_start_dash(Vector2(0, -1))
			if _frame >= 2:
				if _player._hitstun_remaining <= 0.0 and _player._invulnerable_remaining > 0.0:
					print("PASS: dashing clears the hitstun lock and grants invulnerability")
				else:
					_fail("dash didn't clear hitstun / grant invulnerability (hitstun_remaining=%s invulnerable_remaining=%s)" % [_player._hitstun_remaining, _player._invulnerable_remaining])
				print("OVERALL: ", "PASS" if _ok else "FAIL")
				get_tree().quit(0 if _ok else 1)
