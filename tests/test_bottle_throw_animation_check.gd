extends Node3D
## Regression check for the glass bottle's throw ANIMATION (see
## character.gd's own begin_bottle_aim()/cancel_bottle_aim()/
## release_bottle_throw(), wired from player.gd's own aim-edge handling):
## holding right-click while a bottle is equipped raises the arm into a
## held windup pose (ThrowBottleAnim frozen at THROW_BOTTLE_WINDUP_TIME),
## releasing right-click WITHOUT throwing drops it back to normal, and
## tapping punch while aiming lets the windup play out through the actual
## throw. tests/test_glass_bottle_check.gd already covers the MECHANICAL
## consequences of throwing (projectile spawn, damage, weapon consumption)
## by calling _try_throw_bottle() directly -- this file is specifically
## about the ANIMATION/input side, so it drives aim/throw via real
## Input.action_press/release the same way tests/test_car_enter_exit_check.gd
## and tests/test_building_door_check.gd already do for their own held-
## input mechanics. Runs headless: real Player+Character+bottle, real
## physics frames -- no rendering needed.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")
const GlassBottleScene := preload("res://weapons/glass_bottle.tscn")

var _ok := true
var _frame := 0
var _phase := 0

var _player: CharacterBody3D
var _character: Node
var _bottle: Node3D

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

	_bottle = GlassBottleScene.instantiate()
	add_child(_bottle)
	_bottle._equip(_player)

func _press_aim() -> void:
	Input.action_press("block")

func _release_aim() -> void:
	Input.action_release("block")

func _tap_punch() -> void:
	Input.action_press("punch")

func _release_punch() -> void:
	Input.action_release("punch")

func _physics_process(_delta: float) -> void:
	_frame += 1
	match _phase:
		0: # let everything settle
			if _frame >= 5:
				_phase = 1
				_frame = 0
		1: # hold right-click -- should raise into the held windup pose
			if _frame == 1:
				_press_aim()
			if _frame >= 10:
				if _character._is_aiming_throw:
					print("PASS: holding right-click enters the bottle throw windup")
				else:
					_fail("_is_aiming_throw is false after holding right-click")
				if _character.anim_player.current_animation == "throw_bottle":
					print("PASS: the windup plays the real retargeted throw_bottle clip")
				else:
					_fail("current_animation is '%s', expected 'throw_bottle'" % _character.anim_player.current_animation)
				if is_equal_approx(_character.anim_player.current_animation_position, _character.THROW_BOTTLE_WINDUP_TIME):
					print("PASS: the windup is held frozen at THROW_BOTTLE_WINDUP_TIME (%.2fs)" % _character.THROW_BOTTLE_WINDUP_TIME)
				else:
					_fail("held at t=%.3f, expected THROW_BOTTLE_WINDUP_TIME=%.3f" % [_character.anim_player.current_animation_position, _character.THROW_BOTTLE_WINDUP_TIME])
				if is_equal_approx(_character.anim_player.speed_scale, 0.0):
					print("PASS: playback is frozen (speed_scale=0) while the windup is held")
				else:
					_fail("speed_scale is %s while holding aim, expected 0.0" % _character.anim_player.speed_scale)
				_phase = 2
				_frame = 0
		2: # release right-click WITHOUT throwing -- should cancel cleanly
			if _frame == 1:
				_release_aim()
			if _frame >= 10:
				if not _character._is_aiming_throw:
					print("PASS: releasing right-click without throwing cancels the windup")
				else:
					_fail("_is_aiming_throw is still true after releasing aim without throwing")
				if is_equal_approx(_character.anim_player.speed_scale, 1.0):
					print("PASS: playback speed is restored after cancelling the windup")
				else:
					_fail("speed_scale is %s after cancelling, expected 1.0" % _character.anim_player.speed_scale)
				_phase = 3
				_frame = 0
		3: # re-aim, then tap punch -- the windup should play through to a
			# real throw, ending back at idle once the clip finishes
			if _frame == 1:
				_press_aim()
			if _frame >= 10:
				if not _character._is_aiming_throw:
					_fail("re-aiming after a cancel didn't re-enter the windup")
				_phase = 4
				_frame = 0
		4:
			if _frame == 1:
				_tap_punch()
			if _frame == 2:
				_release_punch()
			if _frame >= 5:
				if is_equal_approx(_character.anim_player.speed_scale, 1.0):
					print("PASS: throwing while aiming un-freezes playback (speed_scale back to 1.0)")
				else:
					_fail("speed_scale is %s right after throwing, expected 1.0" % _character.anim_player.speed_scale)
				var thrown := _find_thrown_bottle()
				if thrown != null:
					print("PASS: a real thrown_bottle was spawned by the actual throw input")
				else:
					_fail("no thrown_bottle sibling found after throwing via real input")
				_phase = 5
				_frame = 0
		5: # let the release/follow-through actually finish playing
			if _frame >= 90: # well past THROW clip's own remaining ~0.44s (1.1 - 0.66) at real speed
				if not _character._is_aiming_throw:
					print("PASS: _is_aiming_throw clears once the release finishes playing (animation_finished)")
				else:
					_fail("_is_aiming_throw is still true long after the throw clip should have finished")
				_finish()

func _find_thrown_bottle() -> Node:
	for child in get_children():
		if child.get_script() != null and str(child.get_script().resource_path).contains("thrown_bottle"):
			return child
	return null

func _finish() -> void:
	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
