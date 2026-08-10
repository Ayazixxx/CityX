extends Node
## Regression check for a real reported bug: "switching to martyrblade while
## holding greatsword and then blocking" stopped swinging from ever working
## again. Traced to block() (and, on inspection, jump()/dash()/start_crouch()/
## end_crouch() too -- the same gap, just not yet reported for those) all
## unconditionally overwriting anim_player mid-swing without clearing
## _sword_swing_in_progress, the same bug class tests/test_sword_hit_interrupt_check.gd
## already covers for hit_reaction()/block_impact(). This test covers the
## remaining 5 call sites that can ALSO interrupt a swing.

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
	_character.set_wielding_sword(true)

	call_deferred("_run_checks")

func _current() -> String:
	return _character.anim_player.current_animation

## Starts a fresh swing, calls whatever interrupting action, then confirms
## _sword_swing_in_progress was cleared and a fresh swing is allowed right
## after -- the same two-part check every interrupt needs.
func _check_interrupt(label: String, interrupt: Callable) -> void:
	_character._sword_combo_ready = false
	_character.punch(false)
	if _current() != "sword_attack" or not _character._sword_swing_in_progress:
		_fail("%s: setup didn't reach mid-swing (current='%s')" % [label, _current()])
		return

	interrupt.call()

	if _character._sword_swing_in_progress:
		_fail("%s: _sword_swing_in_progress is still true after the interrupt -- THIS is the reported bug" % label)
	else:
		print("PASS: ", label, " interrupting a swing clears _sword_swing_in_progress")

	_character._sword_combo_ready = false
	_character.punch(false)
	if _character._sword_swing_in_progress and (_current() == "sword_attack" or _current() == "sword_attack2"):
		print("PASS: a fresh swing is allowed again right after ", label)
	else:
		_fail("%s: swing after the interrupt is still blocked (current='%s', in_progress=%s)" % [label, _current(), _character._sword_swing_in_progress])
	# Clean slate for the next check.
	_character._on_animation_finished(_current())
	_character._is_blocking = false
	_character._is_jumping = false
	_character._is_dashing = false
	_character._is_crouch_transitioning = false

func _run_checks() -> void:
	for i in range(5):
		_player._physics_process(1.0 / 60.0)

	_check_interrupt("block()", func(): _character.block())
	_check_interrupt("jump()", func(): _character.jump())
	_check_interrupt("dash('backward')", func(): _character.dash("backward"))
	_check_interrupt("start_crouch()", func(): _character.start_crouch())
	_check_interrupt("end_crouch()", func(): _character.end_crouch())

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
