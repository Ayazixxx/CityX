extends Node
## Regression check for the hostile wolf enemy (npc/wolf.gd, spawned via the
## dev console's own "/spawn wolf"): confirms the procedural model actually
## builds (no crash, has a real mesh), that it finds a nearby real Player
## (added to the "players" group by player.gd's own _ready()) within
## DETECTION_RANGE and chases it, that it bites (damages the player) once
## in range and respects its own cooldown, and that take_damage()/health/
## death work the same duck-typed way every other damageable thing in this
## project already does.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")
const WolfScene := preload("res://npc/wolf.tscn")

var _ok := true
var _player: CharacterBody3D
var _wolf: CharacterBody3D

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
	var character := CharacterScene.instantiate()
	character.set_paintable(false)
	_player.add_child(character)

	call_deferred("_run_checks")

func _run_checks() -> void:
	_run_model_build_check()
	_run_detection_and_chase_check()
	_run_bite_and_damage_check()
	_run_death_check()
	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

func _run_model_build_check() -> void:
	if not _player.is_in_group("players"):
		_fail("player.gd's own _ready() didn't add it to the 'players' group")
	else:
		print("PASS: player is in the 'players' group")

	_wolf = WolfScene.instantiate()
	add_child(_wolf)
	var mesh_count := 0
	for child in _wolf._model.get_children():
		if child is MeshInstance3D and child.mesh != null:
			mesh_count += 1
	if mesh_count >= 10:
		print("PASS: wolf model built ", mesh_count, " mesh pieces (body/head/ears/teeth/eyes/legs/tail)")
	else:
		_fail("wolf model only built %d mesh pieces, expected at least 10" % mesh_count)

	if _wolf.health == _wolf.MAX_HEALTH and not _wolf.is_dead:
		print("PASS: wolf starts at full health, alive")
	else:
		_fail("wolf didn't start correctly (health=%s is_dead=%s)" % [_wolf.health, _wolf.is_dead])

func _run_detection_and_chase_check() -> void:
	_player.global_position = Vector3(0, 0, 0)
	_wolf.global_position = Vector3(0, 0, -8.0) # within DETECTION_RANGE (14)

	_wolf._physics_process(1.0 / 60.0)
	if _wolf._target == _player:
		print("PASS: wolf detects and targets a real player within DETECTION_RANGE")
	else:
		_fail("wolf didn't target the player (target=%s)" % _wolf._target)

	if _wolf.velocity.length() > 0.1:
		print("PASS: wolf starts moving toward the target when out of bite range")
	else:
		_fail("wolf isn't moving despite a target being out of bite range (velocity=%s)" % _wolf.velocity)

	# Out of detection range entirely -- should drop the target and stop.
	_wolf.global_position = Vector3(0, 0, -50.0)
	_wolf.velocity = Vector3.ZERO
	_wolf._physics_process(1.0 / 60.0)
	if _wolf._target == null:
		print("PASS: wolf drops its target once it's out of DETECTION_RANGE")
	else:
		_fail("wolf kept a target far outside DETECTION_RANGE")

func _run_bite_and_damage_check() -> void:
	_wolf.global_position = Vector3(0, 0, -1.0) # within BITE_RANGE (1.7)
	_wolf._bite_cooldown_remaining = 0.0
	var starting_health: float = _player.health
	_wolf._physics_process(1.0 / 60.0)
	if _player.health < starting_health:
		print("PASS: wolf bites and damages the player once in range")
	else:
		_fail("player health didn't drop after being in bite range (still %s)" % _player.health)

	# Cooldown blocks an immediate second bite.
	var health_after_first_bite: float = _player.health
	_wolf._physics_process(1.0 / 60.0)
	if is_equal_approx(_player.health, health_after_first_bite):
		print("PASS: bite cooldown blocks an immediate second bite")
	else:
		_fail("player took damage again before the bite cooldown elapsed")

func _run_death_check() -> void:
	_wolf.take_damage(_wolf.MAX_HEALTH * 2.0, _player)
	if _wolf.is_dead and is_equal_approx(_wolf.health, 0.0):
		print("PASS: enough damage kills the wolf (is_dead=true, health=0)")
	else:
		_fail("wolf didn't die correctly (is_dead=%s health=%s)" % [_wolf.is_dead, _wolf.health])

	# A dead wolf must stop acting entirely.
	_wolf.global_position = Vector3(0, 0, -1.0)
	var player_health_before: float = _player.health
	_wolf._physics_process(1.0 / 60.0)
	if is_equal_approx(_player.health, player_health_before):
		print("PASS: a dead wolf no longer bites")
	else:
		_fail("a dead wolf still damaged the player")
