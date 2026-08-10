extends Node
## Regression check for camera/body facing: standing still should leave
## camera_pivot free to orbit around the character with no fold at all (so
## the camera can swing all the way around to look at your own face), while
## actually moving should instantly fold that same yaw into the body's own
## rotation.y (see player.gd's own class comment) so S/A/D-held backward/
## strafe animations mean anything -- the body's own front has to reliably
## match wherever the camera looks the moment WASD is held, not wherever it
## last happened to walk.
##
## Needs a real window (not --headless): MOUSE_MODE_CAPTURED silently
## no-ops without a display server, and _unhandled_input's mouse-look
## handling is gated on that mode actually being captured.

const PlayerScene := preload("res://player/player.tscn")

var _player: CharacterBody3D
var _frame := 0

func _ready() -> void:
	_player = PlayerScene.instantiate()
	add_child(_player)

func _mouse_motion(dx: float) -> void:
	var ev := InputEventMouseMotion.new()
	ev.relative = Vector2(dx, 0)
	_player._unhandled_input(ev)

func _process(_delta: float) -> void:
	_frame += 1
	if _frame < 3:
		return
	_run_checks()
	get_tree().quit()

func _run_checks() -> void:
	var ok := true
	var camera_pivot: Node3D = _player.camera_pivot
	var dt := 1.0 / 60.0

	print("mouse_mode: ", Input.mouse_mode)

	# 1. Standing still: looking around should NOT turn the body -- camera_pivot
	# keeps the yaw indefinitely, across as many physics steps as it takes to
	# orbit all the way around and look at your own character.
	_mouse_motion(300.0)
	var pre_step_offset := camera_pivot.rotation.y
	if is_equal_approx(pre_step_offset, 0.0):
		print("FAIL: camera_pivot did not rotate from mouse-look")
		ok = false
	else:
		print("PASS: camera_pivot rotated from mouse-look (", pre_step_offset, ")")

	var body_yaw_before := _player.rotation.y
	_player._physics_process(dt)

	if not is_equal_approx(camera_pivot.rotation.y, pre_step_offset):
		print("FAIL: camera_pivot's yaw changed from a physics step while idle (", pre_step_offset, " -> ", camera_pivot.rotation.y, ") -- should stay free, not fold")
		ok = false
	else:
		print("PASS: camera_pivot keeps its own yaw across a physics step while idle (free orbit)")

	if not is_equal_approx(_player.rotation.y, body_yaw_before):
		print("FAIL: body rotated while standing still (", body_yaw_before, " -> ", _player.rotation.y, ") -- should stay put so you can orbit to look at it")
		ok = false
	else:
		print("PASS: body stays put while standing still, letting the camera orbit around it")

	# 2. Moving: the SAME accumulated camera_pivot yaw should now fold
	# instantly into the body the moment WASD is held.
	var absolute_yaw_before_move := _player.rotation.y + camera_pivot.rotation.y
	Input.action_press("move_forward")
	_player._physics_process(dt)

	if not is_equal_approx(camera_pivot.rotation.y, 0.0):
		print("FAIL: camera_pivot still held yaw after a physics step while moving (", camera_pivot.rotation.y, ") -- should be fully folded into the body")
		ok = false
	elif not is_equal_approx(_player.rotation.y, absolute_yaw_before_move):
		print("FAIL: body's facing while moving (", _player.rotation.y, ") doesn't match the pre-fold absolute yaw (", absolute_yaw_before_move, ")")
		ok = false
	else:
		print("PASS: starting to move instantly folds the orbited camera yaw into the body")

	# 3. Still moving: further mouse-look should keep folding instantly too,
	# same as before this fix -- only the idle case changed.
	_mouse_motion(150.0)
	var offset_before_second_fold := camera_pivot.rotation.y
	var absolute_yaw_before_2 := _player.rotation.y + camera_pivot.rotation.y
	_player._physics_process(dt)

	if is_equal_approx(offset_before_second_fold, 0.0):
		print("FAIL: second mouse-look produced no camera_pivot offset to test folding while moving")
		ok = false
	elif not is_equal_approx(camera_pivot.rotation.y, 0.0):
		print("FAIL: camera_pivot still held yaw after a physics step while moving (", camera_pivot.rotation.y, ")")
		ok = false
	elif not is_equal_approx(_player.rotation.y, absolute_yaw_before_2):
		print("FAIL: body's facing while moving (", _player.rotation.y, ") doesn't match the pre-fold absolute yaw (", absolute_yaw_before_2, ")")
		ok = false
	else:
		print("PASS: mouse-look continues folding into the body instantly for as long as WASD is held")

	Input.action_release("move_forward")
	print("OVERALL: ", "PASS" if ok else "FAIL")
