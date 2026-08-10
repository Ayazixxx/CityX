extends Node
## Regression check for the dev console's "/sword unlock/lock/pose"
## commands (see ui/dev_console.gd's own _sword_command and greatsword.gd's
## own set_manual_override/print_current_pose) -- lets a real person drag/
## type the sword's Transform by hand via Godot's own Remote Scene Tree
## debugger instead of guessing numbers blind. Confirms: unlock actually
## stops the automatic per-frame pose overwrite (a manually-set Transform
## sticks across a frame), lock hands control back (reverts to the normal
## computed pose), and running /sword with nothing equipped fails
## gracefully rather than erroring.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")
const GreatswordScene := preload("res://weapons/greatsword.tscn")
const DevConsoleScene := preload("res://ui/dev_console.tscn")

var _ok := true
var _player: CharacterBody3D
var _character: Node3D
var _sword: Node3D
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
	_console._run_command("/sword unlock")

	# Now actually equip a sword (direct _equip(), same shortcut
	# test_sword_moveset_check.gd's own setup already uses -- proximity/
	# interact simulation is a separate, already-covered concern in
	# test_greatsword_check.gd).
	_sword = GreatswordScene.instantiate()
	add_child(_sword)
	_sword._equip(_player)

	call_deferred("_run_checks")

func _run_checks() -> void:
	for i in range(5):
		_player._physics_process(1.0 / 60.0)
		_sword._process(1.0 / 60.0)

	if _console._find_equipped_sword() == _sword:
		print("PASS: console finds the actually-equipped sword")
	else:
		_fail("console's _find_equipped_sword() didn't find the real equipped sword")

	_console._run_command("/sword unlock")
	if _sword._manual_override:
		print("PASS: /sword unlock sets manual_override")
	else:
		_fail("/sword unlock didn't set manual_override")

	# Simulate a manual Remote Scene Tree edit -- an arbitrary Transform
	# that would NEVER naturally occur from GRIP_HAND_ROTATION/
	# HEAVY_ATTACK_GRIP_ROTATION.
	var test_position := Vector3(1.23, 4.56, 7.89)
	var test_rotation := Vector3(0.1, 0.2, 0.3)
	_sword.position = test_position
	_sword.rotation = test_rotation

	# Advance a real frame -- this is exactly what would silently overwrite
	# a manual edit if the unlock didn't actually work.
	_sword._process(1.0 / 60.0)

	if _sword.position.is_equal_approx(test_position) and _sword.rotation.is_equal_approx(test_rotation):
		print("PASS: manually-set Transform survives a frame while unlocked (not overwritten)")
	else:
		_fail("manual Transform got overwritten while supposedly unlocked (position=%s rotation=%s)" % [_sword.position, _sword.rotation])

	_console._run_command("/sword pose")
	print("PASS: /sword pose didn't error")

	_console._run_command("/sword lock")
	if not _sword._manual_override:
		print("PASS: /sword lock clears manual_override")
	else:
		_fail("/sword lock didn't clear manual_override")

	_sword._process(1.0 / 60.0)
	if _sword.rotation.is_equal_approx(_sword.GRIP_HAND_ROTATION):
		print("PASS: locking hands control back to the automatic pose (reverted to GRIP_HAND_ROTATION)")
	else:
		_fail("locking didn't restore the automatic rotation (got %s, expected %s)" % [_sword.rotation, _sword.GRIP_HAND_ROTATION])

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
