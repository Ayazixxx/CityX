extends Node
## Regression check for character.gd's new nameplate fade (_update_nameplate/
## _local_viewer_is_looking_at_me): hosts a real session (so is_multiplayer_
## authority() behaves for real, same reasoning/setup test_anim_sync_check.gd
## already uses) with a LOCAL player (authority=1) and a "remote" one
## (authority=2, simulating another peer's body -- a real second OS process
## isn't available within one test, see main.gd's own header comment on that
## same limitation), and checks: looking directly at the remote player from
## close range fades their name in above their head; looking away fades it
## back out; moving out of NAMEPLATE_RANGE_METERS also fades it out even
## while still looking their way; and nobody ever sees their OWN name
## floating above their own head.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _local_player: CharacterBody3D
var _local_character: Node
var _remote_player: CharacterBody3D
var _remote_character: Node
var _ok := true
var _frame := 0
var _phase := 0

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

	var err := Net.host_game()
	if err != OK:
		_fail("Net.host_game() failed with error %d" % err)

	_local_player = PlayerScene.instantiate()
	_local_player.set_multiplayer_authority(1)
	_local_player.position = Vector3(0, 1.0, 0)
	add_child(_local_player)
	_local_character = CharacterScene.instantiate()
	_local_character.set_paintable(false)
	_local_character.set_character_name("LocalMe")
	_local_player.add_child(_local_character)

	_remote_player = PlayerScene.instantiate()
	_remote_player.set_multiplayer_authority(2)
	_remote_player.position = Vector3(0, 1.0, -3.0) # 3m straight ahead of the local player's default facing (-Z)
	add_child(_remote_player)
	_remote_character = CharacterScene.instantiate()
	_remote_character.set_paintable(false)
	_remote_character.set_character_name("RemoteFriend")
	_remote_player.add_child(_remote_character)

	if not _local_player.is_multiplayer_authority():
		_fail("local player should recognize itself as the authority")
	if _remote_player.is_multiplayer_authority():
		_fail("remote player should NOT recognize itself as the authority")

func _physics_process(_delta: float) -> void:
	_frame += 1
	match _phase:
		0: # let both settle
			if _frame >= 10:
				_phase = 1
				_frame = 0
		1: # looking directly at the remote player, close range -- should fade in
			if _frame >= 60:
				if _remote_character._nameplate_alpha > 0.9:
					print("PASS: nameplate faded in while looking directly at a nearby player (alpha=", _remote_character._nameplate_alpha, ")")
				else:
					_fail("nameplate didn't fade in (alpha=%s)" % _remote_character._nameplate_alpha)
				if _remote_character._nameplate.text == "RemoteFriend":
					print("PASS: nameplate shows the correct character name")
				else:
					_fail("nameplate text wrong (%s)" % _remote_character._nameplate.text)
				if _local_character._nameplate_alpha <= 0.0:
					print("PASS: the local player never sees their own nameplate")
				else:
					_fail("local player's own nameplate incorrectly faded in (alpha=%s)" % _local_character._nameplate_alpha)
				# Now turn the local player 180 degrees -- looking away.
				_local_player.rotation.y = PI
				_phase = 2
				_frame = 0
		2: # looking away -- should fade back out
			if _frame >= 60:
				if _remote_character._nameplate_alpha < 0.1:
					print("PASS: nameplate faded back out after looking away")
				else:
					_fail("nameplate didn't fade out after looking away (alpha=%s)" % _remote_character._nameplate_alpha)
				# Face the remote player again, but this time move far away.
				_local_player.rotation.y = 0.0
				_remote_player.position = Vector3(0, 1.0, -30.0)
				_phase = 3
				_frame = 0
		3: # looking at them again, but now out of range
			if _frame >= 60:
				if _remote_character._nameplate_alpha < 0.1:
					print("PASS: nameplate stays hidden past NAMEPLATE_RANGE_METERS even while looking at them")
				else:
					_fail("nameplate showed even though the player is out of range (alpha=%s)" % _remote_character._nameplate_alpha)
				print("OVERALL: ", "PASS" if _ok else "FAIL")
				get_tree().quit(0 if _ok else 1)
