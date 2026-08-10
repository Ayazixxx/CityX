extends Node
## Regression check for the pistol moveset (character.gd's own
## set_wielding_gun() and every method it redirects -- set_walking/jump/
## shoot/set_aiming): confirms walking/running/strafing while holding a gun
## shows the gun_* clips, that idle (not aiming) falls back to the SAME
## idle/combat_idle the fist system already uses (no dedicated "gun idle"
## clip exists), that jump() picks gun_jump vs gun_jump_moving off is_moving
## (not is_sprinting -- explicitly asked for), that shoot() plays gun_shoot
## and _on_animation_finished clears _is_shooting, that aiming while
## standing still shows gun_aim_in and releasing it falls back to idle, that
## shooting/aiming don't freeze the legs while moving (same leg_anim_player
## overlay mechanism sword/fist attacks already use), and that equipping a
## sword after a gun (and vice versa) is mutually exclusive rather than
## leaving both states stuck true.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _ok := true
var _player: CharacterBody3D
var _character: Node3D

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

	call_deferred("_run_checks")

func _current() -> String:
	return _character.anim_player.current_animation

func _current_leg() -> String:
	return _character.leg_anim_player.current_animation

func _run_checks() -> void:
	for i in range(5):
		_player._physics_process(1.0 / 60.0)

	_character.set_wielding_gun(true)
	if _character._holding_gun:
		print("PASS: set_wielding_gun(true) sets _holding_gun")
	else:
		_fail("_holding_gun is still false after set_wielding_gun(true)")

	# 1. Idle (not aiming) falls back to the plain fist idle -- no dedicated
	# gun-idle clip was supplied.
	_player._physics_process(1.0 / 60.0)
	if _current() == "idle" or _current() == "combat_idle":
		print("PASS: standing still while holding a gun (not aiming) falls back to idle/combat_idle")
	else:
		_fail("standing still while holding a gun shows '%s', expected idle/combat_idle" % _current())

	# 2. Walking forward shows gun_walk.
	Input.action_press("move_forward")
	_player._physics_process(1.0 / 60.0)
	Input.action_release("move_forward")
	if _current() == "gun_walk":
		print("PASS: walking forward while holding a gun shows gun_walk")
	else:
		_fail("walking forward while holding a gun shows '%s', expected gun_walk" % _current())

	# 3. Strafing left/right and walking backward show their own clips.
	Input.action_press("move_left")
	_player._physics_process(1.0 / 60.0)
	Input.action_release("move_left")
	if _current() == "gun_walk_left":
		print("PASS: strafing left while holding a gun shows gun_walk_left")
	else:
		_fail("strafing left while holding a gun shows '%s', expected gun_walk_left" % _current())

	Input.action_press("move_right")
	_player._physics_process(1.0 / 60.0)
	Input.action_release("move_right")
	if _current() == "gun_walk_right":
		print("PASS: strafing right while holding a gun shows gun_walk_right")
	else:
		_fail("strafing right while holding a gun shows '%s', expected gun_walk_right" % _current())

	Input.action_press("move_back")
	_player._physics_process(1.0 / 60.0)
	Input.action_release("move_back")
	if _current() == "gun_walk_backward":
		print("PASS: walking backward while holding a gun shows gun_walk_backward")
	else:
		_fail("walking backward while holding a gun shows '%s', expected gun_walk_backward" % _current())

	# 4. Sprinting forward shows gun_run.
	Input.action_press("move_forward")
	Input.action_press("sprint")
	for i in range(5): # let stamina/effective_sprinting actually engage
		_player._physics_process(1.0 / 60.0)
	if _current() == "gun_run":
		print("PASS: sprinting forward while holding a gun shows gun_run")
	else:
		_fail("sprinting forward while holding a gun shows '%s', expected gun_run" % _current())
	Input.action_release("move_forward")
	Input.action_release("sprint")

	# 5. jump() picks gun_jump (standing) vs gun_jump_moving (walking OR
	# sprinting) off is_moving, NOT is_sprinting -- explicitly asked for.
	_character.jump(false, false)
	if _current() == "gun_jump":
		print("PASS: jump() while standing still shows gun_jump")
	else:
		_fail("standing jump while holding a gun shows '%s', expected gun_jump" % _current())
	if _character._is_jumping:
		print("PASS: gun_jump correctly sets _is_jumping")
	else:
		_fail("gun_jump didn't set _is_jumping")
	_character._on_animation_finished("gun_jump")
	if not _character._is_jumping:
		print("PASS: _on_animation_finished('gun_jump') clears _is_jumping")
	else:
		_fail("_on_animation_finished('gun_jump') didn't clear _is_jumping")

	_character.jump(false, true) # walking, not sprinting -- still counts as moving
	if _current() == "gun_jump_moving":
		print("PASS: jump() while walking (not sprinting) shows gun_jump_moving")
	else:
		_fail("walking jump while holding a gun shows '%s', expected gun_jump_moving" % _current())
	_character.land()

	_character.jump(true, true) # sprinting -- also counts as moving
	if _current() == "gun_jump_moving":
		print("PASS: jump() while sprinting shows gun_jump_moving too")
	else:
		_fail("sprinting jump while holding a gun shows '%s', expected gun_jump_moving" % _current())
	_character.land()
	if not _character._is_jumping:
		print("PASS: land() clears _is_jumping regardless of which gun jump clip was playing")
	else:
		_fail("_is_jumping still true after land()")

	# 6. shoot() plays gun_shoot and sets _is_shooting; finishing clears it.
	_character.anim_player.play("idle")
	_character.shoot()
	if _current() == "gun_shoot":
		print("PASS: shoot() plays gun_shoot")
	else:
		_fail("shoot() played '%s', expected gun_shoot" % _current())
	if _character._is_shooting:
		print("PASS: shoot() sets _is_shooting")
	else:
		_fail("shoot() didn't set _is_shooting")
	_character._on_animation_finished("gun_shoot")
	if not _character._is_shooting:
		print("PASS: _on_animation_finished('gun_shoot') clears _is_shooting")
	else:
		_fail("_on_animation_finished('gun_shoot') didn't clear _is_shooting")

	# 6b. shoot() is a no-op while not holding a gun.
	_character.set_wielding_gun(false)
	_character.anim_player.play("idle")
	_character.shoot()
	if _current() == "idle" and not _character._is_shooting:
		print("PASS: shoot() is a no-op while not holding a gun")
	else:
		_fail("shoot() had an effect while not holding a gun (current=%s is_shooting=%s)" % [_current(), _character._is_shooting])
	_character.set_wielding_gun(true)

	# 7. Shooting while moving keeps the legs stepping (same leg-overlay
	# mechanism punch/block already use) instead of freezing them.
	Input.action_press("move_forward")
	_player._physics_process(1.0 / 60.0) # sets gun_walk, no leg overlay yet (not shooting)
	_character.shoot()
	_player._physics_process(1.0 / 60.0) # now shooting AND moving
	Input.action_release("move_forward")
	if _current() == "gun_shoot" and _current_leg() == "leg_gun_walk":
		print("PASS: shooting while walking keeps gun_shoot on the upper body with leg_gun_walk overlaying the legs")
	else:
		_fail("shooting while walking: anim='%s' leg_anim='%s', expected gun_shoot/leg_gun_walk" % [_current(), _current_leg()])
	_character._on_animation_finished("gun_shoot")

	# 8. Aiming while standing still shows gun_aim_in; releasing it falls
	# back to idle/combat_idle.
	_character.set_aiming(true)
	_player._physics_process(1.0 / 60.0)
	if _current() == "gun_aim_in":
		print("PASS: aiming while standing still and holding a gun shows gun_aim_in")
	else:
		_fail("aiming while standing still shows '%s', expected gun_aim_in" % _current())

	_character.set_aiming(false)
	_player._physics_process(1.0 / 60.0)
	if _current() == "idle" or _current() == "combat_idle":
		print("PASS: releasing aim falls back to idle/combat_idle")
	else:
		_fail("releasing aim shows '%s', expected idle/combat_idle" % _current())

	# 8b. set_aiming()/gun_aim_in have no effect while not holding a gun.
	_character.set_wielding_gun(false)
	_character.set_aiming(true)
	_character.anim_player.play("idle")
	_player._physics_process(1.0 / 60.0)
	if _current() != "gun_aim_in":
		print("PASS: set_aiming(true) has no effect while not holding a gun")
	else:
		_fail("gun_aim_in showed up while not holding a gun")
	_character.set_aiming(false)

	# 9. Mutual exclusivity: equipping the other weapon type clears this
	# one's state, rather than leaving both flags stuck true together.
	_character.set_wielding_gun(true)
	_character.set_aiming(true)
	_character.set_wielding_sword(true)
	if not _character._holding_gun and not _character._is_aiming:
		print("PASS: equipping a sword clears _holding_gun/_is_aiming from a previously-equipped gun")
	else:
		_fail("_holding_gun=%s _is_aiming=%s after equipping a sword over a gun, expected both false" % [_character._holding_gun, _character._is_aiming])

	_character.set_wielding_gun(true)
	if not _character._wielding_sword:
		print("PASS: equipping a gun clears _wielding_sword from a previously-equipped sword")
	else:
		_fail("_wielding_sword is still true after equipping a gun over a sword")

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
