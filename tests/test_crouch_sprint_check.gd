extends Node3D
## Regression check: sprint must always cancel crouch, not stack with it.
## Before this fix, holding crouch+sprint+forward left is_crouching true
## (since it only ever read the crouch key), so set_walking's own "crouch
## overrides running" animation branch kept showing crouch_walk while the
## ACTUAL velocity was already at full SPRINT_SPEED underneath it -- a real
## mismatch between what the animation showed and how fast the player
## actually moved. Runs headless: pure physics + animation state-machine
## checks, no rendering needed.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _player: CharacterBody3D
var _character: Node3D
var _frame := 0
var _phase := 0
var _ok := true

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

func _horiz_speed() -> float:
	return Vector2(_player.velocity.x, _player.velocity.z).length()

func _physics_process(_delta: float) -> void:
	_frame += 1
	match _phase:
		0: # settle
			if _frame >= 5:
				_phase = 1
				_frame = 0
		1: # start crouch-walking (no sprint yet)
			if _frame == 1:
				Input.action_press("move_forward")
				Input.action_press("crouch")
			if _frame == 30:
				if _character.anim_player.current_animation == "crouch_walk" and absf(_horiz_speed() - _player.SPEED) < 0.5:
					print("PASS: crouch-walking alone plays crouch_walk at plain walk speed")
				else:
					print("FAIL: crouch-walk setup wrong (anim=", _character.anim_player.current_animation, " speed=", _horiz_speed(), ")")
					_ok = false
				Input.action_press("sprint")
				_phase = 2
				_frame = 0
		2: # now hold sprint too, while still holding crouch+forward
			if _frame == 20:
				if absf(_horiz_speed() - _player.SPRINT_SPEED) < 0.5:
					print("PASS: sprint+crouch moves at full SPRINT_SPEED (", _horiz_speed(), ")")
				else:
					print("FAIL: sprint+crouch didn't reach sprint speed (", _horiz_speed(), ", expected ~", _player.SPRINT_SPEED, ")")
					_ok = false
				if _character.anim_player.current_animation == "run":
					print("PASS: animation switched to run, left crouch_walk behind")
				else:
					print("FAIL: animation still not run (anim=", _character.anim_player.current_animation, ")")
					_ok = false
				Input.action_release("sprint")
				Input.action_release("crouch")
				Input.action_release("move_forward")
				print("OVERALL: ", "PASS" if _ok else "FAIL")
				get_tree().quit(0 if _ok else 1)
