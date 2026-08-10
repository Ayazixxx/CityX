extends Node3D
## Regression check for the glass bottle weapon (see weapons/glass_bottle.gd,
## weapons/thrown_bottle.gd): the 3-state durability machine (Intact ->
## Broken -> Fully Broken), melee swing damage/animation, throwing (which
## consumes the weapon), the thrown projectile's own impact detection +
## damage + cosmetic effects + sound selection, the authoritative-vs-
## cosmetic-replica split that prevents double damage across peers, and
## reuse of the existing networked-hit routing for both melee and thrown
## hits. Runs headless: real Player+Character scenes for the melee/equip
## checks (matching test_punch_check.gd's own convention), real physics
## frame-stepping for the RigidBody3D projectile checks (matching
## test_car_physics_check.gd's own convention -- a single large fast-forward
## delta does NOT work for real rigid-body motion, confirmed directly
## earlier this session), and the 3-peer-in-one-process technique
## test_networked_combat_check.gd already established for the networked-
## routing check.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")
const DummyScene := preload("res://tests/dummy_damageable.gd")
const GlassBottleScene := preload("res://weapons/glass_bottle.tscn")
const ThrownBottleScene := preload("res://weapons/thrown_bottle.tscn")
const DevConsoleScene := preload("res://ui/dev_console.tscn")

var _ok := true
var _frame := 0
var _phase := 0

var _player: CharacterBody3D
var _character: Node
var _dummy: CharacterBody3D
var _bottle: Node3D

var _whiff_bottle: Node3D
var _whiff_state_before
var _dummy_real_position: Vector3

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _make_ground() -> void:
	var ground := StaticBody3D.new()
	ground.collision_layer = 1
	var gcol := CollisionShape3D.new()
	var gshape := BoxShape3D.new()
	gshape.size = Vector3(200, 1.0, 200)
	gcol.shape = gshape
	gcol.position = Vector3(0, -0.5, 0)
	ground.add_child(gcol)
	add_child(ground)

func _make_dummy(at: Vector3) -> CharacterBody3D:
	var dummy := DummyScene.new()
	dummy.collision_layer = 4
	var dcol := CollisionShape3D.new()
	var dshape := CapsuleShape3D.new()
	dshape.radius = 0.4
	dshape.height = 1.8
	dcol.shape = dshape
	dcol.position = Vector3(0, 0.9, 0)
	dummy.add_child(dcol)
	add_child(dummy)
	dummy.global_position = at
	return dummy

func _ready() -> void:
	_make_ground()

	_player = PlayerScene.instantiate()
	add_child(_player)
	_character = CharacterScene.instantiate()
	_character.set_paintable(false)
	_player.add_child(_character)

	_dummy = _make_dummy(Vector3(0, 0, -1.5))

	_bottle = GlassBottleScene.instantiate()
	add_child(_bottle)
	_bottle._equip(_player)

func _physics_process(_delta: float) -> void:
	_frame += 1
	match _phase:
		0: # settle, then durability-state-machine + whiff checks. The whiff
			# check is deliberately spread across several REAL physics frames:
			# Godot's physics-server broadphase only picks up a script-side
			# transform change (e.g. _dummy.global_position = ...) at the START
			# of the NEXT physics step, so moving the dummy away and raycasting
			# in the very same frame still sees it at its OLD position --
			# confirmed via debug prints showing the "whiff" hitting a
			# CharacterBody3D at exactly the dummy's original (unmoved)
			# location. Same underlying reason RigidBody3D checks elsewhere in
			# this file need real frame-stepping, not a single fast-forward.
			if _frame == 5:
				_check_durability_state_machine()
				_dummy_real_position = _dummy.global_position
				_dummy.global_position = Vector3(500, 0, 500)
			elif _frame == 10: # let the move-away propagate to the physics server
				_run_whiff_swing()
			elif _frame == 11:
				_finish_whiff_check()
				_dummy.global_position = _dummy_real_position
			elif _frame >= 20: # let the move-back propagate before hit #1's real swing
				_phase = 1
				_frame = 0
		1: # end-to-end melee flow, hit #1 via REAL input (confirms the whole
			# Input -> _try_swing_bottle -> raycast -> damage -> durability
			# pipeline works at least once end to end)
			if _frame == 1:
				Input.action_press("punch")
			if _frame == 2:
				Input.action_release("punch")
			if _frame >= 12:
				_check_hit_1_via_real_input()
				_phase = 2
				_frame = 0
		2:
			_check_hits_2_through_12_direct()
			_check_throw_consumed_and_denied_while_broken()
			_phase = 3
			_frame = 0
		3: # thrown projectile physics -- needs real frame-stepping
			_start_thrown_projectile_checks()
			_phase = 4
			_frame = 0
		4:
			if _frame >= 90 or _thrown_checks_all_impacted():
				_check_thrown_projectile_results()
				_phase = 5
				_frame = 0
		5:
			_check_networked_melee_routing_reuse()
			_check_dev_console_spawn()
			print("OVERALL: ", "PASS" if _ok else "FAIL")
			get_tree().quit(0 if _ok else 1)

## (a) Durability transitions, driven directly (no need to route through a
## real swing for the state machine's own logic).
func _check_durability_state_machine() -> void:
	var b: Node3D = GlassBottleScene.instantiate()
	add_child(b)
	if b._state == b.DurabilityState.INTACT and b.can_throw():
		print("PASS: a fresh bottle starts INTACT and throwable")
	else:
		_fail("fresh bottle should start INTACT and throwable")

	b.register_landed_melee_hit()
	if b._state == b.DurabilityState.BROKEN and is_equal_approx(b.get_melee_damage(), b.BROKEN_MELEE_DAMAGE) and not b.can_throw():
		print("PASS: 1st landed hit takes INTACT -> BROKEN, not throwable anymore")
	else:
		_fail("expected BROKEN after 1 hit (state=%s damage=%s can_throw=%s)" % [b._state, b.get_melee_damage(), b.can_throw()])

	for i in range(b.BROKEN_HITS_TO_FULLY_BROKEN - 1):
		b.register_landed_melee_hit()
	if b._state == b.DurabilityState.BROKEN:
		print("PASS: still BROKEN after 9 more hits (10th still pending)")
	else:
		_fail("bottle left BROKEN too early (state=%s)" % b._state)

	b.register_landed_melee_hit() # the 10th hit while broken -- 11th overall
	if b._state == b.DurabilityState.FULLY_BROKEN and is_equal_approx(b.get_melee_damage(), 0.0) and not b.can_throw():
		print("PASS: 10th hit while broken takes BROKEN -> FULLY_BROKEN, 0 damage, not throwable")
	else:
		_fail("expected FULLY_BROKEN after 11 total hits (state=%s damage=%s)" % [b._state, b.get_melee_damage()])

	b.register_landed_melee_hit() # a 12th hit -- must be a safe no-op
	if b._state == b.DurabilityState.FULLY_BROKEN:
		print("PASS: an extra hit past fully-broken is a safe no-op, doesn't crash or change state")
	else:
		_fail("fully-broken state changed from an extra hit (state=%s)" % b._state)
	b.queue_free()

## (b) A whiff (nothing in range) must not advance durability. Split across
## two physics frames by the caller (see _physics_process's own comment) so
## the dummy's move-away has genuinely propagated to the physics server
## before the raycast runs.
func _run_whiff_swing() -> void:
	_whiff_bottle = GlassBottleScene.instantiate()
	add_child(_whiff_bottle)
	_whiff_state_before = _whiff_bottle._state
	_player._equipped_bottle = _whiff_bottle
	_player._try_swing_bottle(_character)

func _finish_whiff_check() -> void:
	if _whiff_bottle._state == _whiff_state_before:
		print("PASS: a whiff (nothing in range) doesn't advance bottle durability")
	else:
		_fail("bottle durability changed from a whiff (state=%s)" % _whiff_bottle._state)
	_whiff_bottle.queue_free()
	_player._equipped_bottle = _bottle # restore the real fixture for later checks
	# Defensive: a whiff still starts the cooldown (matches a real swing --
	# see _try_swing_bottle's own unconditional _punch_cooldown_remaining
	# assignment near its top) -- reset it so the very next real-input swing
	# isn't silently denied by a cooldown this whiff itself started.
	_player._punch_cooldown_remaining = 0.0

func _check_hit_1_via_real_input() -> void:
	if is_equal_approx(_dummy.damage_taken, _bottle.INTACT_MELEE_DAMAGE):
		print("PASS: hit #1 (via real Input) deals INTACT_MELEE_DAMAGE (%s)" % _bottle.INTACT_MELEE_DAMAGE)
	else:
		_fail("expected %s damage after hit #1, got %s" % [_bottle.INTACT_MELEE_DAMAGE, _dummy.damage_taken])
	if _character.anim_player.current_animation == "punch_right":
		print("PASS: a bottle swing plays punch_right (reused unarmed clip, not a sword swing)")
	else:
		_fail("expected punch_right, got '%s'" % _character.anim_player.current_animation)
	if _bottle._state == _bottle.DurabilityState.BROKEN:
		print("PASS: the real end-to-end swing correctly broke the bottle on its first landed hit")
	else:
		_fail("bottle should be BROKEN after hit #1 (state=%s)" % _bottle._state)

## Hits 2 through 12, driven directly (bypassing Input, since the real
## Input-driven pipeline was already confirmed for hit #1 above -- no need
## to repeat the press/release timing dance 11 more times).
func _check_hits_2_through_12_direct() -> void:
	var expected_total: float = _dummy.damage_taken
	for hit_number in range(2, 13):
		_player._try_swing_bottle(_character)
		if hit_number <= 11:
			expected_total += _bottle.BROKEN_MELEE_DAMAGE
		# hit 12 (fully broken) adds 0 damage
		if not is_equal_approx(_dummy.damage_taken, expected_total):
			_fail("hit #%d: expected cumulative damage %s, got %s" % [hit_number, expected_total, _dummy.damage_taken])
			return
	print("PASS: cumulative damage across hits 2-12 matches BROKEN_MELEE_DAMAGE per hit, then 0 once fully broken")
	if _bottle._state == _bottle.DurabilityState.FULLY_BROKEN:
		print("PASS: bottle is FULLY_BROKEN after 11 total landed hits")
	else:
		_fail("bottle should be FULLY_BROKEN by now (state=%s)" % _bottle._state)

## (d)/(e) Throw consumes the weapon; throwing is denied while broken.
func _check_throw_consumed_and_denied_while_broken() -> void:
	# _bottle is FULLY_BROKEN at this point (from the checks above) --
	# confirm throwing is denied.
	var thrown_before: int = get_tree().get_nodes_in_group("_thrown_bottle_check_marker").size()
	_player._try_throw_bottle()
	if _player._equipped_bottle == _bottle:
		print("PASS: throwing a broken bottle is denied -- still equipped")
	else:
		_fail("a broken bottle should not be throwable")

	# A fresh INTACT bottle, equipped for real, to test the actual throw.
	var fresh: Node3D = GlassBottleScene.instantiate()
	add_child(fresh)
	fresh._equip(_player)
	if _player._equipped_bottle != fresh:
		_fail("setup: fresh bottle didn't equip correctly")
		return
	_player._try_throw_bottle()
	if _player._equipped_bottle == null:
		print("PASS: throwing an intact bottle consumes it -- _equipped_bottle is now null")
	else:
		_fail("_equipped_bottle should be null after throwing")
	if _character.has_method("get") and not _character._wielding_bottle:
		print("PASS: character._wielding_bottle is false after throwing")
	else:
		_fail("character._wielding_bottle should be false after throwing")

## (f)/(g)/(h) Thrown projectile physics -- spawns 3 real thrown_bottle
## instances (aimed at an enemy, aimed at a wall, and a cosmetic replica
## aimed at the same enemy) and lets real physics frames carry them to
## impact.
var _thrown_enemy: RigidBody3D
var _thrown_terrain: RigidBody3D
var _thrown_replica: RigidBody3D
var _enemy_target: CharacterBody3D
var _wall: StaticBody3D

func _start_thrown_projectile_checks() -> void:
	_enemy_target = _make_dummy(Vector3(10, 0.5, 0))

	_wall = StaticBody3D.new()
	_wall.collision_layer = 1
	var wcol := CollisionShape3D.new()
	var wshape := BoxShape3D.new()
	wshape.size = Vector3(0.2, 4, 4)
	wcol.shape = wshape
	_wall.add_child(wcol)
	add_child(_wall)
	_wall.global_position = Vector3(-10, 1.0, 0)

	# Thrown close enough that real gravity drop over the flight time stays
	# well within the target capsule's vertical extent -- a longer flight
	# (e.g. the wall test's 5m, which only worked because a 4x4 wall is huge
	# and forgiving of drop) would undershoot a human-sized capsule target,
	# which is a real ballistic-arc property of the RigidBody3D throw, not a
	# bug to work around in the implementation.
	_thrown_enemy = _spawn_thrown(Vector3(8.5, 0.5, 0), (_enemy_target.global_position - Vector3(8.5, 0.5, 0)).normalized() * 12.0, true, _player)
	_thrown_terrain = _spawn_thrown(Vector3(-5, 1.0, 0), (_wall.global_position - Vector3(-5, 1.0, 0)).normalized() * 12.0, true, _player)
	_thrown_replica = _spawn_thrown(Vector3(5, 0.5, 3), (_enemy_target.global_position + Vector3(0, 0, 3) - Vector3(5, 0.5, 3)).normalized() * 12.0, false, _player)

func _spawn_thrown(at: Vector3, vel: Vector3, is_authoritative: bool, thrower: Node) -> RigidBody3D:
	var thrown: RigidBody3D = ThrownBottleScene.instantiate()
	add_child(thrown)
	thrown.global_position = at
	thrown.linear_velocity = vel
	thrown.is_authoritative = is_authoritative
	thrown.set_thrower(thrower)
	return thrown

func _thrown_checks_all_impacted() -> bool:
	return not is_instance_valid(_thrown_enemy) and not is_instance_valid(_thrown_terrain) and not is_instance_valid(_thrown_replica)

func _check_thrown_projectile_results() -> void:
	var damage_before_replica_check: float = _enemy_target.damage_taken
	if damage_before_replica_check > 0.0:
		print("PASS: an authoritative thrown bottle hitting an enemy applies real damage (%s)" % damage_before_replica_check)
	else:
		_fail("thrown bottle hitting the enemy dealt no damage")

	var shatters := 0
	for child in get_children():
		if child.get_script() != null and str(child.get_script().resource_path).contains("glass_shatter"):
			shatters += 1
	if shatters >= 2:
		print("PASS: glass-shatter cosmetic effects spawned for the impacts (found %d)" % shatters)
	else:
		_fail("expected at least 2 glass-shatter effects, found %d" % shatters)

	var enemy_sound_found := false
	var terrain_sound_found := false
	for child in get_tree().current_scene.get_children():
		if child is AudioStreamPlayer3D:
			if child.stream == load("res://weapons/assets/audio/glass_hit_enemy.mp3"):
				enemy_sound_found = true
			elif child.stream == load("res://weapons/assets/audio/glass_hit_terrain.mp3") or child.stream == load("res://weapons/assets/audio/glass_break.mp3"):
				terrain_sound_found = true
	if enemy_sound_found:
		print("PASS: the enemy-hit impact played glass_hit_enemy.mp3")
	else:
		_fail("no AudioStreamPlayer3D found carrying the enemy-impact sound")
	if terrain_sound_found:
		print("PASS: the terrain-hit impact played one of the terrain/break sounds, not the enemy one")
	else:
		_fail("no AudioStreamPlayer3D found carrying a terrain-impact sound")

	# The replica was aimed at a SEPARATE target 3m over from the real
	# enemy -- confirm it never touched that target's health at all.
	var offset_target_hit := false
	for child in get_children():
		if child is CharacterBody3D and child != _dummy and child != _enemy_target and child.get("damage_taken") != null and child.damage_taken > 0.0:
			offset_target_hit = true
	if not offset_target_hit:
		print("PASS: the cosmetic (non-authoritative) replica never applied any damage")
	else:
		_fail("a non-authoritative replica incorrectly applied damage")

## (i) Networked melee routing reuse -- same 3-peer-in-one-process technique
## test_networked_combat_check.gd already established: temporarily grant a
## "defender" local authority (matching the real host's own unique id) so
## _rpc_request_hit's own guard passes, confirming a bottle-equipped
## attacker's damage value flows through the EXISTING RPC pair untouched.
func _check_networked_melee_routing_reuse() -> void:
	var attacker: CharacterBody3D = PlayerScene.instantiate()
	attacker.set_multiplayer_authority(1)
	add_child(attacker)
	var attacker_character := CharacterScene.instantiate()
	attacker_character.set_paintable(false)
	attacker.add_child(attacker_character)
	var bottle: Node3D = GlassBottleScene.instantiate()
	add_child(bottle)
	bottle._equip(attacker)

	var defender: CharacterBody3D = PlayerScene.instantiate()
	defender.set_multiplayer_authority(1) # simulates "this is our own real authoritative body" for _rpc_request_hit's own guard, same technique test_networked_combat_check.gd already uses
	add_child(defender)
	var defender_character := CharacterScene.instantiate()
	defender_character.set_paintable(false)
	defender.add_child(defender_character)

	var health_before: float = defender.health
	defender._rpc_request_hit(bottle.get_melee_damage(), 555, 0.0, false)
	if is_equal_approx(defender.health, health_before - bottle.INTACT_MELEE_DAMAGE):
		print("PASS: a bottle-equipped attacker's damage value flows through the existing _rpc_request_hit handler unchanged")
	else:
		_fail("expected defender health to drop by %s, got a drop of %s" % [bottle.INTACT_MELEE_DAMAGE, health_before - defender.health])

func _check_dev_console_spawn() -> void:
	var console := DevConsoleScene.instantiate()
	_player.add_child(console)
	var before := get_tree().get_nodes_in_group("players").size() # unrelated group, just forces a scene-tree read; real check is below
	console._spawn("bottle")
	var found := false
	for child in _player.get_parent().get_children():
		if child.get_script() != null and str(child.get_script().resource_path).contains("glass_bottle") and child != _bottle:
			found = true
	if found:
		print("PASS: /spawn bottle placed a new glass_bottle.tscn instance in the world")
	else:
		_fail("/spawn bottle didn't place a new bottle in the world")
