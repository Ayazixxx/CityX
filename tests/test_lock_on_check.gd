extends Node3D
## Regression check for the Naruto-Storm-style lock-on camera (see
## player.gd's own _try_lock_on/_update_lock_on/_release_lock_on): pressing
## "lock_on" (C) acquires whichever in-range candidate is nearest the
## camera's own crosshair; pressing it again while already locked cycles to
## the next candidate; once every candidate has been visited once, the NEXT
## press releases the lock entirely instead of wrapping back to the first
## target again (the refined rule from user feedback -- with exactly 2
## candidates: 1st press locks A, 2nd press advances to B, 3rd press
## releases). Also covers the 3 auto-release conditions (range, death) and
## the camera mechanics themselves (rotation.y lerps toward the target
## rather than snapping, camera_pivot.rotation.y stays pinned at 0 while
## locked).
##
## _try_lock_on()/_update_lock_on() are called DIRECTLY here (same "poke
## internal methods directly" convention test_car_exit_upright_check.gd's
## own _complete_exit() call already uses) rather than driving the real
## Input.action_press("lock_on") pipeline -- this sidesteps the documented
## one-frame just_pressed lag entirely and lets every check run
## synchronously in _ready(), matching test_combo_window_expiry_check.gd's
## own "no real per-frame waiting needed" style. Lock-on candidates are
## plain Node3D stand-ins (an inner LockTarget class, just a global_position
## + is_dead, same minimal-stand-in spirit as tests/dummy_damageable.gd)
## manually added to the "players" group -- not real Player scenes, to keep
## this test lightweight and avoid multiple real Player cameras/replication
## setup fighting each other. Runs fully headless: no real per-frame waiting,
## no mouse capture needed (unlike test_camera_orbit_check.gd, since this
## never touches raw mouse input at all).

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

class LockTarget:
	extends Node3D
	var is_dead := false

var _player: CharacterBody3D
var _character: Node3D
var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _make_target(pos: Vector3) -> LockTarget:
	var target := LockTarget.new()
	add_child(target)
	target.add_to_group("players")
	target.global_position = pos
	return target

func _ready() -> void:
	_player = PlayerScene.instantiate()
	add_child(_player)
	_character = CharacterScene.instantiate()
	_character.set_paintable(false)
	_player.add_child(_character)
	call_deferred("_run_checks")

func _run_checks() -> void:
	# --- Acquisition: nearest-to-crosshair wins ---
	# Player faces -Z at rotation 0 (see main.gd's own facing convention).
	# target_a sits dead ahead (camera angle ~0); target_b is well off to the
	# side but still within LOCK_ON_RANGE -- both are valid range-wise
	# candidates, but only target_a should win a FRESH acquisition.
	var target_a := _make_target(Vector3(0, 0, -5))
	var target_b := _make_target(Vector3(6, 0, -5))

	_player._try_lock_on()
	if _player._lock_on_target == target_a:
		print("PASS: a fresh lock-on acquires the candidate nearest the crosshair")
	else:
		_fail("fresh lock-on acquired %s, expected target_a (dead ahead)" % [_player._lock_on_target])

	# --- Cycling then releasing: N candidates -> N+1 presses releases ---
	var first_target: Node3D = _player._lock_on_target
	_player._try_lock_on() # 2nd press: should cycle to the OTHER candidate
	if _player._lock_on_target != null and _player._lock_on_target != first_target:
		print("PASS: a 2nd press cycles to the other candidate")
	else:
		_fail("2nd press didn't cycle to the other candidate (target=%s, first_target=%s)" % [_player._lock_on_target, first_target])

	_player._try_lock_on() # 3rd press: every candidate visited once -> release
	if _player._lock_on_target == null:
		print("PASS: cycling through every candidate once then releases, rather than wrapping back to the first target")
	else:
		_fail("3rd press should have released the lock, still locked onto %s" % [_player._lock_on_target])

	# --- Auto-release: target moves out of range ---
	_player._try_lock_on()
	if _player._lock_on_target == null:
		_fail("setup: couldn't re-acquire a target for the range check")
	target_a.global_position = Vector3(0, 0, -(_player.LOCK_ON_RANGE * 2.0))
	target_b.global_position = Vector3(0, 0, -(_player.LOCK_ON_RANGE * 2.0))
	if _player._lock_on_target != null:
		_player._update_lock_on(1.0 / 60.0)
	if _player._lock_on_target == null:
		print("PASS: the lock releases once the target moves out of range")
	else:
		_fail("lock is still held even though the target moved far out of range")

	# Reset targets back into range for the next checks.
	target_a.global_position = Vector3(0, 0, -5)
	target_b.global_position = Vector3(6, 0, -5)

	# --- Auto-release: target dies ---
	_player._try_lock_on()
	if _player._lock_on_target == null:
		_fail("setup: couldn't re-acquire a target for the death check")
	var locked_target: Node = _player._lock_on_target
	locked_target.is_dead = true
	_player._update_lock_on(1.0 / 60.0)
	if _player._lock_on_target == null:
		print("PASS: the lock releases once the target dies")
	else:
		_fail("lock is still held even though the target died")
	locked_target.is_dead = false

	# --- Camera mechanics: rotation.y lerps toward the target (not a snap), camera_pivot.rotation.y stays pinned at 0 ---
	_player._release_lock_on()
	_player.rotation.y = 0.0
	_player.camera_pivot.rotation.y = 0.5 # simulate leftover mouse-look yaw from before locking on
	# A target well off to the side (not dead ahead) so there's a real yaw
	# difference for the lerp to visibly close only PART of.
	var side_target := _make_target(Vector3(10, 0, 0))
	_player._lock_on_target = side_target
	_player._lock_on_visits = 1

	var yaw_before := _player.rotation.y
	_player._update_lock_on(1.0 / 60.0)
	if is_equal_approx(_player.camera_pivot.rotation.y, 0.0):
		print("PASS: camera_pivot.rotation.y is pinned at 0 while locked on")
	else:
		_fail("camera_pivot.rotation.y wasn't pinned at 0 while locked on (%s)" % _player.camera_pivot.rotation.y)

	if not is_equal_approx(_player.rotation.y, yaw_before) and absf(angle_difference(_player.rotation.y, PI / 2.0)) > 0.01:
		print("PASS: rotation.y moved toward the target but didn't snap there in a single frame (lerp, not snap)")
	else:
		_fail("rotation.y either didn't move or snapped instantly (yaw=%s)" % _player.rotation.y)

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
