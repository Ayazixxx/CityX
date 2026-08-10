extends Node
## Regression check for a real reported bug: "swings twice then stops letting
## me swing" (no death/ragdoll involved at all). tests/test_sword_moveset_check.gd
## already covers the combo/spam-click logic, but drives it by manually
## calling _on_animation_finished() itself rather than ever letting a real
## clip actually run out through real engine time -- if the bug only shows up
## under genuine real-time AnimationPlayer playback, that test would stay
## green while the real game stays broken. This one instead awaits the real
## animation_finished SIGNAL directly (the exact same mechanism character.gd's
## own _on_animation_finished relies on), which sidesteps headless/fixed-fps
## frame-budget guessing entirely -- await just naturally lets however many
## real engine frames it actually takes go by.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _ok := true
var _player: CharacterBody3D
var _character: Node3D

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _current() -> String:
	return _character.anim_player.current_animation

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
	_character.set_wielding_sword(true)

	for i in range(5):
		await get_tree().process_frame

	_character.punch(false)
	if _current() == "sword_attack" and _character._sword_swing_in_progress:
		print("PASS: first real swing plays sword_attack")
	else:
		_fail("first swing didn't start correctly (current='%s', in_progress=%s)" % [_current(), _character._sword_swing_in_progress])

	var timed_out: bool = await _wait_for_finish_or_timeout("sword_attack", 10.0)
	if timed_out:
		_fail("sword_attack never fired animation_finished within 10 real seconds")
	elif _character._sword_swing_in_progress:
		_fail("sword_attack finished but _sword_swing_in_progress is still true (animation_finished didn't clear it)")
	else:
		print("PASS: sword_attack finished naturally and cleared _sword_swing_in_progress")

	_character.punch(false)
	if _current() == "sword_attack2" and _character._sword_swing_in_progress:
		print("PASS: second real swing (within combo window) plays sword_attack2")
	else:
		_fail("second swing didn't start correctly (current='%s', in_progress=%s)" % [_current(), _character._sword_swing_in_progress])

	timed_out = await _wait_for_finish_or_timeout("sword_attack2", 10.0)
	if timed_out:
		_fail("sword_attack2 never fired animation_finished within 10 real seconds -- THIS is the reported bug: the follow-up swing never lets go of _sword_swing_in_progress, so no further swing is ever allowed again")
	elif _character._sword_swing_in_progress:
		_fail("sword_attack2 finished but _sword_swing_in_progress is still true (animation_finished didn't clear it) -- THIS is the reported bug")
	else:
		print("PASS: sword_attack2 finished naturally and cleared _sword_swing_in_progress")

	_character.punch(false)
	if _current() == "sword_attack" and _character._sword_swing_in_progress and _character.anim_player.is_playing():
		print("PASS: third real swing is allowed and plays sword_attack (not stuck)")
	else:
		_fail("third swing was rejected/wrong -- current='%s', in_progress=%s, is_playing=%s (this is the reported bug: stops letting you swing)" % [_current(), _character._sword_swing_in_progress, _character.anim_player.is_playing()])

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

## Awaits animation_finished for the specific clip name we just started (a
## stray finished signal for something else shouldn't count), OR a wall-clock
## timeout as a safety net so a genuinely-broken case (the reported bug) ends
## the test instead of hanging forever. Returns true if it timed out.
func _wait_for_finish_or_timeout(anim_name: String, timeout_seconds: float) -> bool:
	var finished := false
	var on_finished := func(name: StringName) -> void:
		if String(name) == anim_name:
			finished = true
	_character.anim_player.animation_finished.connect(on_finished)
	var elapsed := 0.0
	while not finished and elapsed < timeout_seconds:
		await get_tree().process_frame
		elapsed += get_process_delta_time()
	_character.anim_player.animation_finished.disconnect(on_finished)
	return not finished
