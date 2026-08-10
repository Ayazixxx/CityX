extends Node
## Regression check for a real reported bug: dying mid-animation (e.g.
## mid sword-swing) left _is_punching/_sword_swing_in_progress (and every
## other one-shot animation-state flag) stuck true forever. ragdoll()'s own
## anim_player.stop() never lets the interrupted clip reach its natural end,
## so _on_animation_finished -- the only other place those flags clear --
## never fires for it, and neither ragdoll() nor reset_ragdoll() used to
## clear them either. In real play this showed up as "respawns stuck in the
## death pose" (set_walking()'s own _is_punching early-return bails out
## every single frame after respawn, before ever reaching the normal
## idle/walk logic) and "can't swing the sword after respawning" (punch()'s
## own _sword_swing_in_progress guard rejects every future swing as if one
## were still playing). Fixed by clearing every such flag in
## reset_ragdoll() itself, right alongside the rest of respawn's reset.

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

func _run_checks() -> void:
	for i in range(5):
		_player._physics_process(1.0 / 60.0)

	_character.set_wielding_sword(true)
	_character.punch(false)
	if _current() != "sword_attack" or not _character._sword_swing_in_progress:
		_fail("setup didn't actually reach a mid-swing state (current='%s', in_progress=%s)" % [_current(), _character._sword_swing_in_progress])
	else:
		print("PASS: mid-swing state set up (sword_attack playing, _sword_swing_in_progress true)")

	# Die WHILE still mid-swing -- ragdoll()'s own anim_player.stop() means
	# _on_animation_finished never fires for "sword_attack".
	_character.ragdoll()
	if not _character._is_ragdolled:
		_fail("ragdoll() didn't actually ragdoll the character")

	# Respawn.
	_character.reset_ragdoll()

	if _character._is_punching or _character._sword_swing_in_progress:
		_fail("respawn left stale flags set (_is_punching=%s, _sword_swing_in_progress=%s)" % [_character._is_punching, _character._sword_swing_in_progress])
	else:
		print("PASS: respawn cleared _is_punching/_sword_swing_in_progress")

	# set_walking() must actually show idle again, not bail out forever.
	_character.set_walking(false, false, false)
	if _current() == "sword_idle":
		print("PASS: character shows sword_idle again after respawn (not stuck in the death pose)")
	else:
		_fail("character is still stuck on '%s' after respawn instead of returning to sword_idle" % _current())

	# A fresh swing must actually be allowed again.
	_character.punch(false)
	if _current() == "sword_attack":
		print("PASS: punch() works again after respawn (plays sword_attack)")
	else:
		_fail("punch() after respawn played '%s', expected sword_attack" % _current())

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
