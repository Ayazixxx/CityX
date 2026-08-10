extends Node
## Regression check for a real reported bug: getting hit (or having a block
## impacted) while a sword swing is already in progress used to leave
## _sword_swing_in_progress stuck true forever, permanently blocking every
## future swing -- reported as "swings twice then stops letting me swing,"
## traced back to the CPU landing a hit mid-swing. hit_reaction()/
## block_impact() overwrite anim_player with their own clip
## (sword_impact/sword_block_impact), so the original swing clip
## (sword_attack/sword_attack2) never reaches its own natural end and
## _on_animation_finished never fires for it -- see hit_reaction()'s own
## comment in character.gd for the fix (clearing _sword_swing_in_progress
## immediately, right when the interrupt happens, not waiting on a finish
## that will never come).

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

func _run_checks() -> void:
	for i in range(5):
		_player._physics_process(1.0 / 60.0)

	# --- hit_reaction() interrupting a swing ---
	_character.punch(false)
	if _current() != "sword_attack" or not _character._sword_swing_in_progress:
		_fail("setup didn't reach mid-swing (current='%s', in_progress=%s)" % [_current(), _character._sword_swing_in_progress])
	else:
		print("PASS: mid-swing state set up for hit_reaction test")

	_character.hit_reaction() # simulates the CPU landing a hit mid-swing
	if _current() != "sword_impact":
		_fail("hit_reaction() didn't take over the animation (current='%s')" % _current())
	if _character._sword_swing_in_progress:
		_fail("_sword_swing_in_progress is still true after hit_reaction() interrupted the swing -- THIS is the reported bug")
	else:
		print("PASS: hit_reaction() interrupting a swing clears _sword_swing_in_progress immediately")

	_character.punch(false)
	# Either sword_attack or sword_attack2 is a legitimate outcome here --
	# hit_reaction() only touches _sword_swing_in_progress (see its own
	# comment), not _sword_combo_ready, so a swing that was mid-combo before
	# getting interrupted correctly continues the combo. What actually
	# matters for this bug is that a swing is allowed AT ALL (not silently
	# rejected).
	if (_current() == "sword_attack" or _current() == "sword_attack2") and _character._sword_swing_in_progress:
		print("PASS: a fresh swing is allowed again right after being hit mid-swing (played '", _current(), "')")
	else:
		_fail("swing after being hit is still blocked (current='%s', in_progress=%s)" % [_current(), _character._sword_swing_in_progress])
	_character._on_animation_finished(_current())
	_character._sword_combo_ready = false

	# --- block_impact() interrupting a swing ---
	_character.punch(false)
	_character.block_impact() # simulates a blocked hit landing mid-swing
	if _character._sword_swing_in_progress:
		_fail("_sword_swing_in_progress is still true after block_impact() interrupted the swing")
	else:
		print("PASS: block_impact() interrupting a swing clears _sword_swing_in_progress immediately")

	_character.punch(false)
	if (_current() == "sword_attack" or _current() == "sword_attack2") and _character._sword_swing_in_progress:
		print("PASS: a fresh swing is allowed again right after a blocked hit mid-swing (played '", _current(), "')")
	else:
		_fail("swing after a blocked hit is still blocked (current='%s', in_progress=%s)" % [_current(), _character._sword_swing_in_progress])

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
