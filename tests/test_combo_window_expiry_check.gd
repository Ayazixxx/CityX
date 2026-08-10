extends Node3D
## Regression check for the new unarmed combo chain's own "window" (see
## character/combo_data.gd and character.gd's own _combo_stage_index/
## _combo_window_timer): a follow-up punch() call within a stage's own
## window advances to the next stage, but letting the window actually
## expire resets the chain back to stage 0 -- same "single large
## _physics_process delta rather than many small ticks" fast-forward idiom
## test_sword_moveset_check.gd's own combo-window-expiry check already
## established for the sibling sword mechanic. Runs headless: a bare
## Character with no Player/animation-timing involved at all, just the
## chain's own index/timer state.

const CharacterScene := preload("res://character/character.tscn")

var _character: Node3D
var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _current() -> String:
	return _character.anim_player.current_animation

func _ready() -> void:
	_character = CharacterScene.instantiate()
	_character.set_paintable(false)
	add_child(_character)

	# A follow-up punch well within the window advances the chain.
	_character.punch(false)
	if _current() == "punch_right":
		print("PASS: 1st punch plays punch_right (stage 0)")
	else:
		_fail("1st punch plays '%s', expected punch_right" % _current())
	_character._on_animation_finished("punch_right")

	_character.punch(false)
	if _current() == "punch_left":
		print("PASS: a follow-up punch within the window advances to stage 1 (punch_left)")
	else:
		_fail("follow-up punch plays '%s', expected punch_left" % _current())
	_character._on_animation_finished("punch_left")

	# The window actually expiring (not just being consumed by a follow-up
	# click) resets the chain back to stage 0.
	if _character._combo_window_timer > 0.0:
		_character._physics_process(0.9 + 0.1) # ComboData's own unarmed "window" is 0.9s
	if _character._combo_stage_index == 0 and _character._combo_window_timer <= 0.0:
		print("PASS: the combo window actually expires over time, resetting to stage 0")
	else:
		_fail("combo state didn't reset after the window expired (stage_index=%s window_timer=%s)" % [_character._combo_stage_index, _character._combo_window_timer])

	_character.punch(false)
	if _current() == "punch_right":
		print("PASS: punching again after the window expired restarts at punch_right, not punch_left")
	else:
		_fail("punching after the window expired plays '%s', expected punch_right" % _current())
	_character._on_animation_finished("punch_right")

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
