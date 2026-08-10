extends Node
## Regression check for the giant hostile sloth/beast enemy (npc/sloth.gd,
## spawned via the dev console's own "/spawn sloth"): same shape as
## tests/test_wolf_check.gd (model build, player-group detection/chase,
## attack+damage+cooldown, death stops attacking) plus one check specific to
## this enemy's own explicit requirement -- "make it like 5x bigger than the
## player" -- verifying its own collision capsule is actually in that
## ballpark relative to player.tscn's own CapsuleShape3D (radius 0.4, height
## 1.8, i.e. a ~2.6m top-to-bottom extent including the rounded caps).

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")
const SlothScene := preload("res://npc/sloth.tscn")

var _ok := true
var _player: CharacterBody3D
var _sloth: CharacterBody3D

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
	_run_model_build_and_scale_check()
	_run_detection_and_chase_check()
	_run_attack_and_damage_check()
	_run_death_check()
	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

func _run_model_build_and_scale_check() -> void:
	if not _player.is_in_group("players"):
		_fail("player.gd's own _ready() didn't add it to the 'players' group")
	else:
		print("PASS: player is in the 'players' group")

	_sloth = SlothScene.instantiate()
	add_child(_sloth)
	var mesh_count := 0
	for child in _sloth._model.get_children():
		if child is MeshInstance3D and child.mesh != null:
			mesh_count += 1
	if mesh_count >= 10:
		print("PASS: sloth model built ", mesh_count, " mesh pieces (hips/spine/hump/head/ears/eyes/arms/fists/legs)")
	else:
		_fail("sloth model only built %d mesh pieces, expected at least 10" % mesh_count)

	if _sloth.health == _sloth.MAX_HEALTH and not _sloth.is_dead:
		print("PASS: sloth starts at full health, alive")
	else:
		_fail("sloth didn't start correctly (health=%s is_dead=%s)" % [_sloth.health, _sloth.is_dead])

	# "5x bigger than the player" -- player.tscn's own CapsuleShape3D is
	# radius 0.4, height 1.8, a ~2.6m top-to-bottom extent (height + 2*radius
	# for the rounded caps). The sloth's own collision capsule should be in
	# the ballpark of 5x that (~13m), generously bounded (3x-8x) rather than
	# pinned to an exact multiplier this check would be too brittle against
	# any future hand-tuning of the model's own proportions.
	var player_shape: CapsuleShape3D = _player.get_node("CollisionShape3D").shape
	var player_extent: float = player_shape.height + player_shape.radius * 2.0
	var sloth_shape: CapsuleShape3D = _sloth._collision.shape
	var sloth_extent: float = sloth_shape.height + sloth_shape.radius * 2.0
	var ratio := sloth_extent / player_extent
	if ratio >= 3.0 and ratio <= 8.0:
		print("PASS: sloth's own collision extent (", sloth_extent, "m) is ~", ratio, "x the player's (", player_extent, "m) -- in the ballpark of 5x bigger")
	else:
		_fail("sloth's collision extent is %sx the player's (sloth=%sm, player=%sm) -- expected roughly 5x (3x-8x), not this" % [ratio, sloth_extent, player_extent])

func _run_detection_and_chase_check() -> void:
	_player.global_position = Vector3(0, 0, 0)
	_sloth.global_position = Vector3(0, 0, -15.0) # within DETECTION_RANGE (22)

	_sloth._physics_process(1.0 / 60.0)
	if _sloth._target == _player:
		print("PASS: sloth detects and targets a real player within DETECTION_RANGE")
	else:
		_fail("sloth didn't target the player (target=%s)" % _sloth._target)

	if _sloth.velocity.length() > 0.1:
		print("PASS: sloth starts moving toward the target when out of attack range")
	else:
		_fail("sloth isn't moving despite a target being out of attack range (velocity=%s)" % _sloth.velocity)

	_sloth.global_position = Vector3(0, 0, -80.0)
	_sloth.velocity = Vector3.ZERO
	_sloth._physics_process(1.0 / 60.0)
	if _sloth._target == null:
		print("PASS: sloth drops its target once it's out of DETECTION_RANGE")
	else:
		_fail("sloth kept a target far outside DETECTION_RANGE")

func _run_attack_and_damage_check() -> void:
	_sloth.global_position = Vector3(0, 0, -2.0) # within ATTACK_RANGE (3.2)
	_sloth._attack_cooldown_remaining = 0.0
	var starting_health: float = _player.health
	_sloth._physics_process(1.0 / 60.0)
	if _player.health < starting_health:
		print("PASS: sloth slams and damages the player once in range")
	else:
		_fail("player health didn't drop after being in attack range (still %s)" % _player.health)

	var health_after_first_hit: float = _player.health
	_sloth._physics_process(1.0 / 60.0)
	if is_equal_approx(_player.health, health_after_first_hit):
		print("PASS: attack cooldown blocks an immediate second slam")
	else:
		_fail("player took damage again before the attack cooldown elapsed")

func _run_death_check() -> void:
	_sloth.take_damage(_sloth.MAX_HEALTH * 2.0, _player)
	if _sloth.is_dead and is_equal_approx(_sloth.health, 0.0):
		print("PASS: enough damage kills the sloth (is_dead=true, health=0)")
	else:
		_fail("sloth didn't die correctly (is_dead=%s health=%s)" % [_sloth.is_dead, _sloth.health])

	_sloth.global_position = Vector3(0, 0, -2.0)
	var player_health_before: float = _player.health
	_sloth._physics_process(1.0 / 60.0)
	if is_equal_approx(_player.health, player_health_before):
		print("PASS: a dead sloth no longer attacks")
	else:
		_fail("a dead sloth still damaged the player")
