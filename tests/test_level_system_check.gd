extends Node
## Regression check for the new level/XP/stat-point system (player.gd's own
## add_xp()/spend_stat_point()/_recompute_stats()/apply_strength(), plus
## npc/wolf.gd's own XP_REWARD grant and character_save.gd's own
## update_progress()/load_character() persistence): confirms the XP curve
## triggers level-ups (including several from one big grant), that leveling
## awards STAT_POINTS_PER_LEVEL every level and one skill point every
## SKILL_POINT_EVERY_N_LEVELS, that spending a point on each of the 5 stats
## actually moves the derived gameplay value it's supposed to (max_health/
## max_stamina/max_mana/apply_strength()'s own multiplier), that killing a
## wolf grants the attacker real XP, and that level/xp/points round-trip
## through a save/load cycle correctly. Runs headless: pure state-machine +
## physics checks, no rendering needed.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")
const WolfScene := preload("res://npc/wolf.tscn")
const CharacterSave = preload("res://character/character_save.gd")

var _ok := true
var _player: CharacterBody3D

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
	_run_level_up_check()
	_run_multi_level_up_check()
	_run_stat_point_check()
	_run_strength_check()
	_run_speed_check()
	await _run_wolf_kill_xp_check()
	_run_persistence_check()
	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

## Baseline: a fresh player starts at level 1, 0 xp, 0 points, and its
## effective max_health/max_stamina/max_mana/SPEED-derived velocity all
## match the plain, un-boosted constants exactly -- confirms none of the
## stat-effect wiring changed default (0-point) behavior at all.
func _run_level_up_check() -> void:
	if _player.level == 1 and is_equal_approx(_player.xp, 0.0) and _player.stat_points == 0 and _player.skill_points == 0:
		print("PASS: a fresh player starts at level 1 with 0 xp/points")
	else:
		_fail("fresh player state wrong (level=%s xp=%s stat_points=%s skill_points=%s)" % [_player.level, _player.xp, _player.stat_points, _player.skill_points])
	if is_equal_approx(_player.max_health, _player.MAX_HEALTH) and is_equal_approx(_player.max_stamina, _player.MAX_STAMINA) and is_equal_approx(_player.max_mana, 0.0):
		print("PASS: a fresh player's effective max_health/max_stamina/max_mana match the base constants exactly")
	else:
		_fail("fresh player's derived stats don't match base constants (max_health=%s max_stamina=%s max_mana=%s)" % [_player.max_health, _player.max_stamina, _player.max_mana])

	# Exactly enough XP to reach level 2 (XP_PER_LEVEL_BASE * 1) -- the
	# FIRST level-up, which (SKILL_POINT_EVERY_N_LEVELS=2, level 2 % 2 == 0)
	# should also grant a skill point.
	_player.add_xp(_player.XP_PER_LEVEL_BASE)
	if _player.level == 2 and is_equal_approx(_player.xp, 0.0):
		print("PASS: exactly enough xp triggers a level-up with no leftover xp")
	else:
		_fail("level-up didn't land correctly (level=%s xp=%s)" % [_player.level, _player.xp])
	if _player.stat_points == _player.STAT_POINTS_PER_LEVEL:
		print("PASS: leveling up granted STAT_POINTS_PER_LEVEL (%d) stat points" % _player.STAT_POINTS_PER_LEVEL)
	else:
		_fail("wrong stat point award (got %d, expected %d)" % [_player.stat_points, _player.STAT_POINTS_PER_LEVEL])
	if _player.skill_points == 1:
		print("PASS: reaching level 2 (a multiple of SKILL_POINT_EVERY_N_LEVELS) granted a skill point")
	else:
		_fail("wrong skill point award at level 2 (got %d, expected 1)" % _player.skill_points)

## One big XP grant spanning several levels' worth must trigger multiple
## level-ups in a single add_xp() call, not just the first one, with
## leftover xp correctly carried into the new (higher) xp_to_next_level.
func _run_multi_level_up_check() -> void:
	var level_before: int = _player.level # 2, from the previous check
	# Level 2 needs XP_PER_LEVEL_BASE*2 to reach level 3, then *3 for level
	# 4 -- this grant clears both with a known remainder left over.
	var to_level_3: float = _player.XP_PER_LEVEL_BASE * 2.0
	var to_level_4: float = _player.XP_PER_LEVEL_BASE * 3.0
	var remainder := 17.0
	_player.add_xp(to_level_3 + to_level_4 + remainder)
	if _player.level == level_before + 2:
		print("PASS: one large xp grant triggered 2 level-ups at once (", level_before, " -> ", _player.level, ")")
	else:
		_fail("multi-level-up didn't land (level %d -> %d, expected +2)" % [level_before, _player.level])
	if is_equal_approx(_player.xp, remainder):
		print("PASS: leftover xp after multiple level-ups carried over correctly (", _player.xp, ")")
	else:
		_fail("leftover xp wrong after multi-level-up (got %s, expected %s)" % [_player.xp, remainder])

## Spending a point on each of the 5 stats actually moves the right
## derived value, and spending fails cleanly (false, no state change) once
## points run out or the stat name is bogus.
func _run_stat_point_check() -> void:
	var points_before: int = _player.stat_points # from the 2 level-ups above
	var max_health_before: float = _player.max_health
	var health_before: float = _player.health
	if _player.spend_stat_point("health") and _player.stat_health == 1:
		print("PASS: spend_stat_point('health') succeeds and increments stat_health")
	else:
		_fail("spend_stat_point('health') didn't land correctly")
	if is_equal_approx(_player.max_health, max_health_before + _player.HEALTH_PER_STAT_POINT):
		print("PASS: spending a health point raised max_health by HEALTH_PER_STAT_POINT")
	else:
		_fail("max_health didn't rise correctly (got %s, expected %s)" % [_player.max_health, max_health_before + _player.HEALTH_PER_STAT_POINT])
	if is_equal_approx(_player.health, health_before + _player.HEALTH_PER_STAT_POINT):
		print("PASS: spending a health point also raised CURRENT health by the same delta")
	else:
		_fail("current health didn't rise alongside max_health (got %s, expected %s)" % [_player.health, health_before + _player.HEALTH_PER_STAT_POINT])
	if _player.stat_points == points_before - 1:
		print("PASS: spending a point consumed exactly one stat_points")
	else:
		_fail("stat_points didn't decrement correctly (got %d, expected %d)" % [_player.stat_points, points_before - 1])

	var max_stamina_before: float = _player.max_stamina
	_player.spend_stat_point("stamina")
	if is_equal_approx(_player.max_stamina, max_stamina_before + _player.STAMINA_PER_STAT_POINT):
		print("PASS: spending a stamina point raised max_stamina by STAMINA_PER_STAT_POINT")
	else:
		_fail("max_stamina didn't rise correctly")

	var max_mana_before: float = _player.max_mana
	_player.spend_stat_point("mana")
	if is_equal_approx(_player.max_mana, max_mana_before + _player.MANA_PER_STAT_POINT):
		print("PASS: spending a mana point raised max_mana by MANA_PER_STAT_POINT")
	else:
		_fail("max_mana didn't rise correctly")

	_player.spend_stat_point("strength")
	_player.spend_stat_point("speed")
	if _player.stat_strength == 1 and _player.stat_speed == 1:
		print("PASS: spending a point on strength/speed incremented their own stat_* counters")
	else:
		_fail("stat_strength/stat_speed didn't increment (strength=%d speed=%d)" % [_player.stat_strength, _player.stat_speed])

	if not _player.spend_stat_point("not_a_real_stat"):
		print("PASS: spending on a bogus stat name fails cleanly (returns false)")
	else:
		_fail("spending on a bogus stat name incorrectly succeeded")

	var remaining: int = _player.stat_points
	_player.stat_points = 0
	if not _player.spend_stat_point("health"):
		print("PASS: spending with 0 stat_points available fails cleanly")
	else:
		_fail("spending with 0 stat_points available incorrectly succeeded")
	_player.stat_points = remaining # restore for later checks

## Strength's own damage multiplier (apply_strength()) -- 0 points is a
## pure passthrough, spent points scale up by exactly
## STRENGTH_DAMAGE_PERCENT_PER_POINT per point.
func _run_strength_check() -> void:
	# stat_strength is 1 from _run_stat_point_check() above.
	var expected: float = 100.0 * (1.0 + _player.stat_strength * _player.STRENGTH_DAMAGE_PERCENT_PER_POINT)
	var actual: float = _player.apply_strength(100.0)
	if is_equal_approx(actual, expected):
		print("PASS: apply_strength(100) with stat_strength=", _player.stat_strength, " returns ", actual, " (expected ", expected, ")")
	else:
		_fail("apply_strength() gave %s, expected %s" % [actual, expected])

## Speed's own bonus is applied at the single point actual velocity gets
## set in _physics_process -- rather than reaching into that private
## calculation, this drives a real physics frame with move_forward held and
## confirms the resulting horizontal speed matches SPEED scaled by the same
## formula.
func _run_speed_check() -> void:
	_player.global_position = Vector3(0, 0, 0)
	_player.velocity = Vector3.ZERO
	Input.action_press("move_forward")
	_player._physics_process(1.0 / 60.0)
	Input.action_release("move_forward")
	var horiz_speed := Vector2(_player.velocity.x, _player.velocity.z).length()
	var expected: float = _player.SPEED * (1.0 + _player.stat_speed * _player.SPEED_PERCENT_PER_POINT)
	if is_equal_approx(horiz_speed, expected):
		print("PASS: walking speed with stat_speed=", _player.stat_speed, " matches SPEED * (1 + stat_speed * SPEED_PERCENT_PER_POINT) (", horiz_speed, ")")
	else:
		_fail("walking speed didn't reflect the Speed stat (got %s, expected %s)" % [horiz_speed, expected])

## Real reported ask: "the way you level up is by killing enemies, higher
## level enemies give you more XP... just start off by making the wolf a
## level 1 enemy." Kills a real wolf with _player as the attacker and
## confirms XP_REWARD actually landed on the player.
func _run_wolf_kill_xp_check() -> void:
	var wolf: CharacterBody3D = WolfScene.instantiate()
	add_child(wolf)
	await get_tree().physics_frame

	var xp_before: float = _player.xp
	var level_before: int = _player.level
	wolf.take_damage(wolf.MAX_HEALTH * 2.0, _player)
	if not wolf.is_dead:
		_fail("setup for the wolf-kill-xp check failed -- wolf didn't die")
		return
	# add_xp()'s own level-up loop can leave xp non-comparable directly if a
	## level-up landed exactly on the kill -- check the total XP EARNED
	# instead (accounting for however many xp_to_next_level thresholds were
	# crossed), not a raw xp delta.
	var levels_gained: int = _player.level - level_before
	var xp_earned: float = _player.xp - xp_before
	for i in range(levels_gained):
		xp_earned += _player.XP_PER_LEVEL_BASE * float(level_before + i)
	if is_equal_approx(xp_earned, wolf.XP_REWARD):
		print("PASS: killing a wolf granted the attacker XP_REWARD (", wolf.XP_REWARD, ") xp")
	else:
		_fail("wolf kill granted %s xp, expected %s (ENEMY_LEVEL=%d)" % [xp_earned, wolf.XP_REWARD, wolf.ENEMY_LEVEL])

## level/xp/stat_points/skill_points/the 5 stat_* fields all round-trip
## through character_save.gd's own save_new_character -> update_progress ->
## load_character cycle intact -- a throwaway save path, never the real
## user://characters.cfg (same convention every other save/load test in
## this project already follows).
func _run_persistence_check() -> void:
	var test_save_path: String = "user://test_level_system_characters_%d.cfg" % randi()
	var id := CharacterSave.save_new_character("LevelTester", "sphere", PackedByteArray(), test_save_path)

	CharacterSave.update_progress(id, 7, 42.5, 3, 2, 5, 4, 3, 2, 1, test_save_path)
	var data: Dictionary = CharacterSave.load_character(id, test_save_path)
	if data["level"] == 7 and is_equal_approx(data["xp"], 42.5) and data["stat_points"] == 3 and data["skill_points"] == 2 and data["stat_health"] == 5 and data["stat_strength"] == 4 and data["stat_stamina"] == 3 and data["stat_mana"] == 2 and data["stat_speed"] == 1:
		print("PASS: level/xp/stat_points/skill_points/5 stat allocations all round-trip through save/load intact")
	else:
		_fail("loaded progress data doesn't match what was saved: %s" % data)

	# _flush_progress() itself (the real code path player.gd actually calls)
	# also works end to end, not just CharacterSave's own functions in
	# isolation.
	_player.save_character_id = id
	_player.character_save_path = test_save_path
	_player.level = 9
	_player.xp = 3.0
	_player._flush_progress()
	var flushed: Dictionary = CharacterSave.load_character(id, test_save_path)
	if flushed["level"] == 9 and is_equal_approx(flushed["xp"], 3.0):
		print("PASS: player.gd's own _flush_progress() persists live level/xp correctly")
	else:
		_fail("_flush_progress() didn't persist correctly: %s" % flushed)
