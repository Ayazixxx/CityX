extends Node
## Regression check for the dev console's "/gun unlock/lock/pose/shootpose/
## unfreeze" commands (see ui/dev_console.gd's own _gun_command and
## weapons/pistol.gd's own set_manual_override/print_current_pose) -- same
## shape as test_sword_manual_pose_check.gd's own coverage of the sword
## side, plus the two commands unique to the gun: shootpose (freeze the
## gun_shoot recoil animation mid-clip so the grip can be tuned against the
## ACTUAL firing pose, not just idle) and unfreeze (undo that).

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")
const PistolScene := preload("res://weapons/pistol.tscn")
const DevConsoleScene := preload("res://ui/dev_console.tscn")

var _ok := true
var _player: CharacterBody3D
var _character: Node3D
var _gun: Node3D
var _console: CanvasLayer

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

	_console = DevConsoleScene.instantiate()
	_player.add_child(_console)

	# Console with nothing equipped yet -- should fail gracefully, not error.
	_console._run_command("/gun unlock")

	_gun = PistolScene.instantiate()
	add_child(_gun)
	_gun._equip(_player)

	call_deferred("_run_checks")

func _run_checks() -> void:
	for i in range(5):
		_player._physics_process(1.0 / 60.0)
		_gun._process(1.0 / 60.0)

	if _console._find_equipped_gun() == _gun:
		print("PASS: console finds the actually-equipped gun")
	else:
		_fail("console's _find_equipped_gun() didn't find the real equipped gun")

	_console._run_command("/gun unlock")
	if _gun._manual_override:
		print("PASS: /gun unlock sets manual_override")
	else:
		_fail("/gun unlock didn't set manual_override")

	# Simulate a manual Remote Scene Tree edit, including SCALE (the one
	# axis this whole feature was actually added for -- confirmed nothing
	# in _update_held_pose() touched .scale before this).
	var test_position := Vector3(1.23, 4.56, 7.89)
	var test_rotation := Vector3(0.1, 0.2, 0.3)
	var test_scale := Vector3(1.4, 0.8, 1.1)
	_gun.position = test_position
	_gun.rotation = test_rotation
	_gun.scale = test_scale

	# Advance a real frame -- exactly what would silently overwrite a
	# manual edit if the unlock didn't actually work.
	_gun._process(1.0 / 60.0)

	if _gun.position.is_equal_approx(test_position) and _gun.rotation.is_equal_approx(test_rotation) and _gun.scale.is_equal_approx(test_scale):
		print("PASS: manually-set Transform (including scale) survives a frame while unlocked")
	else:
		_fail("manual Transform got overwritten while supposedly unlocked (position=%s rotation=%s scale=%s)" % [_gun.position, _gun.rotation, _gun.scale])

	_console._run_command("/gun pose")
	print("PASS: /gun pose didn't error")

	_console._run_command("/gun lock")
	if not _gun._manual_override:
		print("PASS: /gun lock clears manual_override")
	else:
		_fail("/gun lock didn't clear manual_override")

	_gun._process(1.0 / 60.0)
	if _gun.rotation.is_equal_approx(_gun.GRIP_HAND_ROTATION) and _gun.position.is_equal_approx(_gun.GRIP_HAND_POSITION) and _gun.scale.is_equal_approx(_gun.GRIP_SCALE):
		print("PASS: locking hands control back to the automatic grip (position/rotation/scale all reverted)")
	else:
		_fail("locking didn't restore the automatic grip (position=%s rotation=%s scale=%s)" % [_gun.position, _gun.rotation, _gun.scale])

	# /gun shootpose -- triggers the recoil animation and freezes it
	# mid-clip so the grip can be tuned against the ACTUAL firing pose.
	_console._run_command("/gun shootpose")
	if _character._is_shooting:
		print("PASS: /gun shootpose triggers the gun_shoot animation (_is_shooting is true)")
	else:
		_fail("/gun shootpose didn't trigger the shoot animation (_is_shooting is false)")

	if is_equal_approx(_character.anim_player.speed_scale, 0.0):
		print("PASS: /gun shootpose freezes the AnimationPlayer (speed_scale=0)")
	else:
		_fail("/gun shootpose didn't freeze the AnimationPlayer (speed_scale=%s)" % _character.anim_player.speed_scale)

	if _gun._manual_override:
		print("PASS: /gun shootpose also unlocks the gun's own manual override")
	else:
		_fail("/gun shootpose didn't unlock the gun's manual override")

	# Frozen means frozen -- advancing real frames must NOT let the
	# animation keep progressing past where it was seeked to.
	var frozen_anim_pos: float = _character.anim_player.current_animation_position
	for i in range(10):
		_character._process(1.0 / 60.0)
	if is_equal_approx(_character.anim_player.current_animation_position, frozen_anim_pos):
		print("PASS: the frozen shoot animation doesn't keep advancing across real frames")
	else:
		_fail("the shoot animation kept advancing despite being frozen (was %s, now %s)" % [frozen_anim_pos, _character.anim_player.current_animation_position])

	_console._run_command("/gun unfreeze")
	if is_equal_approx(_character.anim_player.speed_scale, 1.0):
		print("PASS: /gun unfreeze restores normal animation playback (speed_scale=1)")
	else:
		_fail("/gun unfreeze didn't restore speed_scale (got %s)" % _character.anim_player.speed_scale)

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
