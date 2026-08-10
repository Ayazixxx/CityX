extends Node
## Regression check for player.gd's own gun-aiming camera fold (see
## _physics_process's own gun_aiming_now comment): a real reported ask --
## the free camera orbit that lets you look at your own character while
## genuinely idle ("standing still = free orbit, letting you see your own
## face") is WRONG while aiming down sights with a gun -- "the camera needs
## to move where you are looking" instead, so the reticle/facing stay
## glued together even with zero WASD held. Doesn't need real mouse
## capture (see test_camera_orbit_check.gd's own class comment on why that
## needs a real window) -- setting camera_pivot.rotation.y directly and
## checking whether _physics_process folds it exercises the exact same
## code path a real mouse-look would, without needing MOUSE_MODE_CAPTURED
## at all.

const PlayerScene := preload("res://player/player.tscn")

var _ok := true
var _player: CharacterBody3D

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	_player = PlayerScene.instantiate()
	add_child(_player)
	call_deferred("_run_checks")

func _run_checks() -> void:
	var dt := 1.0 / 60.0

	# --- 1. No gun equipped, not aiming: standing still leaves camera_pivot
	# free (the existing, already-tested idle behavior -- confirms this
	# check's own harness/technique reproduces it before testing the new
	# aiming case). ---
	_player.camera_pivot.rotation.y = 0.5
	var body_yaw_before := _player.rotation.y
	_player._physics_process(dt)
	if is_equal_approx(_player.rotation.y, body_yaw_before) and is_equal_approx(_player.camera_pivot.rotation.y, 0.5):
		print("PASS: with no gun equipped, standing still leaves camera_pivot free (baseline)")
	else:
		_fail("baseline idle orbit didn't hold (rotation.y=%s camera_pivot.rotation.y=%s)" % [_player.rotation.y, _player.camera_pivot.rotation.y])

	# --- 2. A gun equipped but NOT aiming (right-click not held): still
	# free-orbits like the baseline -- merely holding a gun shouldn't change
	# idle-look behavior, only actually aiming should. ---
	_player._equipped_gun = Node.new() # dummy -- just needs to be non-null, see player.gd's own _equipped_gun typing
	_player.camera_pivot.rotation.y = 0.5
	_player._physics_process(dt)
	if is_equal_approx(_player.camera_pivot.rotation.y, 0.5):
		print("PASS: holding a gun without aiming still leaves camera_pivot free")
	else:
		_fail("merely equipping a gun (not aiming) folded the camera (camera_pivot.rotation.y=%s)" % _player.camera_pivot.rotation.y)

	# --- 3. A gun equipped AND aiming (right-click held): camera_pivot's
	# accumulated yaw folds into the body EVERY frame, even with zero WASD
	# held -- the real fix. ---
	Input.action_press("block")
	_player.camera_pivot.rotation.y = 0.5
	var body_yaw_before_aim := _player.rotation.y
	_player._physics_process(dt)
	if is_equal_approx(_player.camera_pivot.rotation.y, 0.0) and is_equal_approx(_player.rotation.y, body_yaw_before_aim + 0.5):
		print("PASS: aiming down sights folds camera_pivot into the body even while standing still")
	else:
		_fail("aiming didn't fold the camera while standing still (rotation.y=%s camera_pivot.rotation.y=%s)" % [_player.rotation.y, _player.camera_pivot.rotation.y])
	Input.action_release("block")

	# --- 4. Releasing aim (right-click let go) restores the free-orbit
	# idle behavior. ---
	_player.camera_pivot.rotation.y = 0.5
	_player._physics_process(dt)
	if is_equal_approx(_player.camera_pivot.rotation.y, 0.5):
		print("PASS: releasing aim restores free camera orbit while standing still")
	else:
		_fail("camera stayed folded after releasing aim (camera_pivot.rotation.y=%s)" % _player.camera_pivot.rotation.y)

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
