extends Node3D
## Regression check for the dedicated backward dash animation (see
## character.gd's own dash()/end_dash()): a backward dash (S held) must
## play "dash_backward", while forward/left/right dashes must NOT get a
## dedicated clip -- they keep player.gd's own original fallback (forced
## "run"). A forward clip (dash_forward, from Dashx.fbx) was tried and
## wired in, then dropped after review -- the animation itself didn't look
## right, so forward dashes were reverted to the same fallback left/right
## always used. Also checks that the dash animation state doesn't outlive
## the physics burst itself (see end_dash()'s own comment on why it cuts
## the clip's own longer recovery tail short, same as land() already does
## for jump). Runs headless: pure physics + animation state-machine checks,
## no rendering needed.

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

func _tap_sprint(press_frame: int) -> void:
	if _frame == press_frame:
		Input.action_press("sprint")
	if _frame == press_frame + 1:
		Input.action_release("sprint")

func _physics_process(_delta: float) -> void:
	_frame += 1
	match _phase:
		0: # settle
			if _frame >= 5:
				_phase = 1
				_frame = 0
		1: # bare double-tap, no WASD held -- forward dash, no dedicated clip
			_tap_sprint(2)
			_tap_sprint(5)
			if _frame == 8:
				if not _character._is_dashing and _character.anim_player.current_animation == "run":
					print("PASS: bare double-tap (forward) dash used the plain run fallback")
				else:
					print("FAIL: bare double-tap dash didn't use the run fallback (anim=", _character.anim_player.current_animation, " is_dashing=", _character._is_dashing, ")")
					_ok = false
			if _frame == 20:
				if _character.anim_player.current_animation == "idle":
					print("PASS: settled back to idle once the burst ended and no WASD was held")
				else:
					print("FAIL: didn't settle back to idle (anim=", _character.anim_player.current_animation, ")")
					_ok = false
				_player.stamina = _player.MAX_STAMINA
				Input.action_press("move_back")
				_phase = 2
				_frame = 0
		2: # holding S (move_back) -- should play dash_backward
			_tap_sprint(3)
			_tap_sprint(6)
			if _frame == 9:
				if _character.anim_player.current_animation == "dash_backward" and _character._is_dashing:
					print("PASS: holding S dash played dash_backward")
				else:
					print("FAIL: holding S dash didn't play dash_backward (anim=", _character.anim_player.current_animation, " is_dashing=", _character._is_dashing, ")")
					_ok = false
			if _frame == 20:
				# "walk_backward" not "run" here is correct/expected -- the
				# sprint key was already released as part of the double-tap
				# gesture itself, so once the burst ends, still holding S
				# alone moves at plain walk pace, not sprint; and it's
				# "walk_backward" specifically (not the old generic "walk"),
				# now that the body's own facing stays locked to the camera
				# and S always means backward relative to it (see player.gd's
				# own class comment) rather than turning around to walk
				# "forward" away from the camera.
				if not _character._is_dashing and _character.anim_player.current_animation == "walk_backward":
					print("PASS: dash state ended with the burst (didn't wait out the full clip) and settled to walk_backward")
				else:
					print("FAIL: dash state outlived the burst (anim=", _character.anim_player.current_animation, " is_dashing=", _character._is_dashing, ")")
					_ok = false
			# Well past dash_backward's own 0.6s clip length before starting
			# the next phase -- otherwise the previous dash's own still-
			# active animation is mistaken for the next one.
			if _frame >= 60:
				Input.action_release("move_back")
				_player.velocity = Vector3.ZERO
				_player.stamina = _player.MAX_STAMINA
				Input.action_press("move_left")
				_phase = 3
				_frame = 0
		3: # holding A (move_left) -- must NOT get a dedicated clip
			_tap_sprint(3)
			_tap_sprint(6)
			if _frame == 9:
				if not _character._is_dashing and _character.anim_player.current_animation != "dash_backward":
					print("PASS: holding A dash left used no dedicated clip (anim=", _character.anim_player.current_animation, ")")
				else:
					print("FAIL: holding A dash unexpectedly used a dedicated clip (anim=", _character.anim_player.current_animation, " is_dashing=", _character._is_dashing, ")")
					_ok = false
			if _frame >= 20:
				Input.action_release("move_left")
				_player.velocity = Vector3.ZERO
				_player.stamina = _player.MAX_STAMINA
				Input.action_press("move_right")
				_phase = 4
				_frame = 0
		4: # holding D (move_right) -- must NOT get a dedicated clip
			_tap_sprint(3)
			_tap_sprint(6)
			if _frame == 9:
				if not _character._is_dashing and _character.anim_player.current_animation != "dash_backward":
					print("PASS: holding D dash right used no dedicated clip (anim=", _character.anim_player.current_animation, ")")
				else:
					print("FAIL: holding D dash unexpectedly used a dedicated clip (anim=", _character.anim_player.current_animation, " is_dashing=", _character._is_dashing, ")")
					_ok = false
				Input.action_release("move_right")
				print("OVERALL: ", "PASS" if _ok else "FAIL")
				get_tree().quit(0 if _ok else 1)
