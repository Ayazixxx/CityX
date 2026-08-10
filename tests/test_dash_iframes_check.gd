extends Node3D
## Regression check for the new dash i-frames mechanic (see player.gd's own
## _invulnerable_remaining and DASH_INVULNERABILITY_SECONDS): a hit landing
## during a dash's own burst window whiffs entirely -- no damage at all --
## and a hit landing after that window expires lands normally again. This is
## what "dodging out of a combo" concretely means (see take_damage()'s own
## early return). Runs headless: direct take_damage()/_try_start_dash() calls
## and a single large _physics_process fast-forward (same "one big delta
## rather than many small ticks" idiom test_sword_moveset_check.gd's own
## combo-window-expiry check already established) against a real Player, no
## rendering or real-time waiting needed.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _player: CharacterBody3D
var _character: Node3D
var _ok := true

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

func _run_checks() -> void:
	for i in range(5):
		_player._physics_process(1.0 / 60.0)

	var health_before: float = _player.health
	_player._try_start_dash(Vector2(0, -1))
	if _player._invulnerable_remaining > 0.0:
		print("PASS: dashing grants a window of invulnerability")
	else:
		_fail("_try_start_dash didn't set _invulnerable_remaining")

	_player.take_damage(10.0, null, 0.0, false)
	if is_equal_approx(_player.health, health_before):
		print("PASS: a hit landing during dash i-frames deals zero damage")
	else:
		_fail("health changed despite i-frames (before=%s after=%s)" % [health_before, _player.health])

	# Fast-forward past the i-frame window entirely, same single-large-delta
	# idiom used elsewhere for timer expiry.
	_player._physics_process(_player.DASH_INVULNERABILITY_SECONDS + 0.1)
	if _player._invulnerable_remaining <= 0.0:
		print("PASS: the i-frame window actually expires over time")
	else:
		_fail("_invulnerable_remaining is still > 0 after the dash window should have expired (%s)" % _player._invulnerable_remaining)

	var health_before_2: float = _player.health
	_player.take_damage(10.0, null, 0.0, false)
	if is_equal_approx(_player.health, health_before_2 - 10.0):
		print("PASS: a hit landing after the i-frame window expires deals normal damage")
	else:
		_fail("hit after i-frames expired didn't deal normal damage (before=%s after=%s)" % [health_before_2, _player.health])

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
