extends Node
## Regression check for the dev-only /fly console command (player.gd's own
## set_fly_mode()): confirms gravity is genuinely disabled while flying
## (velocity.y tracks held jump/crouch, not GRAVITY), that a fall from
## height does zero damage while flying, and that turning fly OFF restores
## normal gravity.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _ok := true
var _player: CharacterBody3D
var _character: Node3D
var _phase := 0
var _frame := 0
var _start_height := 0.0

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
		0: # settle, then start flying
			if _frame >= 5:
				if _player.is_flying():
					_fail("is_flying() true before /fly was ever used")
				_player.set_fly_mode(true)
				if not _player.is_flying():
					_fail("set_fly_mode(true) didn't set is_flying()")
				else:
					print("PASS: set_fly_mode(true) enables is_flying()")
				_player.velocity = Vector3.ZERO
				_start_height = _player.global_position.y
				_phase = 1
				_frame = 0
		1: # hold "jump" (fly up) for a bit -- no input held == no vertical
			# input in the real Input singleton during a headless test, so
			# directly exercise the vertical-velocity math instead of relying
			# on simulated key state: confirm gravity genuinely never touches
			# velocity.y while flying, by watching it stay non-negative
			# across real physics frames with nothing pulling it down.
			if _frame >= 20:
				if _player.velocity.y < -0.01:
					_fail("velocity.y went negative (%s) while flying with no downward input -- gravity is still being applied" % _player.velocity.y)
				else:
					print("PASS: gravity does not pull velocity.y negative while flying (velocity.y=", _player.velocity.y, ")")
				_phase = 2
				_frame = 0
		2: # descend from height under FLY_SPEED control (holding "crouch",
			# same as a real player flying down) -- must take no fall damage
			# at all landing from 50m up, since is_on_floor()-driven fall
			# damage is explicitly skipped while _fly_mode is on. Injecting
			# raw fall velocity instead (tried first) doesn't work: the
			# fly-mode branch overwrites velocity.y from held input every
			# single frame regardless, so it would just hover instead of
			# ever actually falling -- that's the whole point of flying, not
			# a bug, but it means a real controlled descent is what needs
			# testing here, not a simulated uncontrolled fall.
			if _frame == 1:
				_player.global_position = Vector3(0, 50.0, 0)
				_player.velocity = Vector3.ZERO
				Input.action_press("crouch")
			if _player.is_on_floor() and _frame > 5:
				Input.action_release("crouch")
				if _player.health < _player.MAX_HEALTH:
					_fail("took fall damage (health=%s) descending from 50m while flying" % _player.health)
				else:
					print("PASS: no fall damage descending from 50m under fly control")
				_phase = 3
				_frame = 0
			elif _frame >= 600:
				Input.action_release("crouch")
				_fail("never reached the ground within 600 frames")
				_phase = 3
				_frame = 0
		3: # turn fly off -- gravity must resume
			_player.velocity = Vector3.ZERO
			_player.global_position = Vector3(0, 5.0, 0)
			_player.set_fly_mode(false)
			if _player.is_flying():
				_fail("is_flying() still true after set_fly_mode(false)")
			else:
				print("PASS: set_fly_mode(false) disables is_flying()")
			_phase = 4
			_frame = 0
		4: # confirm gravity actually resumes (velocity.y goes negative again)
			if _frame >= 5:
				if _player.velocity.y < -0.01:
					print("PASS: gravity resumes after fly mode is turned off (velocity.y=", _player.velocity.y, ")")
				else:
					_fail("gravity did not resume after set_fly_mode(false) (velocity.y=%s)" % _player.velocity.y)
				print("OVERALL: ", "PASS" if _ok else "FAIL")
				get_tree().quit(0 if _ok else 1)
