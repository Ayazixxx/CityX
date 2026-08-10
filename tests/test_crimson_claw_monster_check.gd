extends Node
## Regression check for the hostile Crimson Claw Monster (npc/crimson_claw_
## monster.gd, spawned via the dev console's own "/spawn crimsonclaw"):
## confirms the imported-mesh model actually builds (no crash, has a real
## mesh, has two glowing red eyes at the confirmed socket positions), that
## it finds a nearby real Player within DETECTION_RANGE and chases it, that
## it attacks (damages the player) once in range and respects its own
## cooldown, that it HOVERS (no gravity/floor-snapping, unlike wolf.gd/
## sloth.gd) rather than needing ground to stand on, and that take_damage()/
## health/death work the same duck-typed way every other damageable thing in
## this project already does.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")
const MonsterScene := preload("res://npc/crimson_claw_monster.tscn")

var _ok := true
var _player: CharacterBody3D
var _monster: CharacterBody3D

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	# Deliberately NO ground plane -- the whole point of this creature is
	# that it hovers and needs no floor to stand on (see its own class
	# comment); a passing test here with no ground at all is itself part of
	# the check.
	_player = PlayerScene.instantiate()
	add_child(_player)
	var character := CharacterScene.instantiate()
	character.set_paintable(false)
	_player.add_child(character)
	# Same determinism reasoning as _monster's own set_physics_process(false)
	# (see _run_model_build_check's own comment) -- this test repositions
	# the player directly and expects it to STAY there across several real
	# awaited frames (needed for the monster's own attack tween); the
	# player's normal automatic gravity/movement processing would otherwise
	# be free to nudge it (e.g. settling onto the ground plane added after
	# the hover check) in between those manual repositions.
	_player.set_physics_process(false)

	call_deferred("_run_checks")

func _run_checks() -> void:
	_run_model_build_check()
	await _run_hover_check()
	# The hover check deliberately runs with NO ground (see this file's own
	# _ready() comment) -- but the PLAYER (unlike the hovering monster) is
	# subject to normal gravity, and free-falls for the rest of this test
	# without one. Added here, after the hover check, so it can't mask that
	# check's own "needs no floor at all" premise.
	var ground := StaticBody3D.new()
	ground.collision_layer = 1
	var gcol := CollisionShape3D.new()
	var gshape := BoxShape3D.new()
	gshape.size = Vector3(200, 1.0, 200)
	gcol.shape = gshape
	gcol.position = Vector3(0, -0.5, 0)
	ground.add_child(gcol)
	add_child(ground)
	await get_tree().physics_frame

	_run_detection_and_chase_check()
	await _run_attack_and_damage_check()
	await _run_attack_miss_check()
	_run_death_check()
	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

func _first_mesh_child(node: Node) -> MeshInstance3D:
	for child in node.get_children():
		if child is MeshInstance3D and child.mesh != null:
			return child
	return null

## Drives the monster's own multi-phase attack state machine (windup ->
## swing -> recover -> idle) through real frames with a real delta, exactly
## as _physics_process would -- the attack no longer lands damage on the
## single frame it's triggered (see crimson_claw_monster.gd's own class
## comment on why), so a test that only calls _physics_process() once (the
## old version of this check) can no longer observe it landing at all.
func _drive_attack_to_completion() -> void:
	var delta := 1.0 / 60.0
	var max_frames := 200 # generous upper bound -- real total duration is WINDUP+SWING+RECOVER seconds / delta
	var frames := 0
	while _monster._attack_state != "idle" or frames == 0:
		_monster._physics_process(delta)
		frames += 1
		if frames > max_frames:
			_fail("attack state machine never returned to 'idle' within %d frames (stuck at '%s')" % [max_frames, _monster._attack_state])
			return
		await get_tree().physics_frame

func _run_model_build_check() -> void:
	if not _player.is_in_group("players"):
		_fail("player.gd's own _ready() didn't add it to the 'players' group")
	else:
		print("PASS: player is in the 'players' group")

	_monster = MonsterScene.instantiate()
	add_child(_monster)
	# This test drives the monster's own _physics_process() MANUALLY
	# throughout (for deterministic, single-step control) -- but later
	# checks also need to `await get_tree().physics_frame` for real (so the
	# attack's own Tween-driven claw swing can actually animate), and once
	# a real frame passes, Godot's own engine ALSO calls _physics_process
	# automatically on any node in the tree that hasn't disabled it
	# (confirmed as a real bug this way: one stray automatic call, firing
	# while the monster and player each still sat at their leftover
	# positions from an earlier check, silently started a real attack
	# sequence that then blocked the NEXT check's own manually-driven
	# scenario). Disabling it here makes every state change in this test
	# come from an explicit, visible call site.
	_monster.set_physics_process(false)

	var mesh_count := 0
	var eye_count := 0
	for child in _monster._facing_flip.get_children():
		if child is MeshInstance3D and child.mesh != null:
			mesh_count += 1
			if child.mesh is SphereMesh and child.mesh.material != null and child.mesh.material.shading_mode == BaseMaterial3D.SHADING_MODE_UNSHADED:
				eye_count += 1
	if mesh_count >= 3:
		print("PASS: monster model built ", mesh_count, " mesh pieces (imported body + 2 eyes)")
	else:
		_fail("monster model only built %d mesh pieces, expected at least 3" % mesh_count)
	if eye_count == 2:
		print("PASS: exactly 2 glowing unshaded eye meshes built")
	else:
		_fail("expected 2 unshaded eye meshes, found %d" % eye_count)

	var left_claw_mesh := _first_mesh_child(_monster._left_claw_pivot)
	var right_claw_mesh := _first_mesh_child(_monster._right_claw_pivot)
	if left_claw_mesh != null and right_claw_mesh != null:
		print("PASS: both claw pivots have a real mesh piece parented under them")
	else:
		_fail("a claw pivot is missing its own mesh (left=%s right=%s)" % [left_claw_mesh, right_claw_mesh])

	if _monster.health == _monster.MAX_HEALTH and not _monster.is_dead:
		print("PASS: monster starts at full health, alive")
	else:
		_fail("monster didn't start correctly (health=%s is_dead=%s)" % [_monster.health, _monster.is_dead])

## Confirms the monster holds itself at HOVER_HEIGHT with no ground present
## at all -- real physics frames, not a single fast-forward delta (matches
## this project's own established convention for anything relying on real
## per-frame movement, see test_car_physics_check.gd's own comment).
func _run_hover_check() -> void:
	_monster.global_position = Vector3(50, -20, 50) # displaced well away from HOVER_HEIGHT, nowhere near any ground
	for i in range(150):
		_monster._physics_process(1.0 / 60.0)
		await get_tree().physics_frame
	# An exponential ease (velocity.y proportional to remaining distance)
	# never reaches the EXACT target, just asymptotically close -- a plain
	# is_equal_approx() (CMP_EPSILON = 1e-5) is tighter than real easing
	# actually converges to in a bounded number of frames. A real, generous
	# tolerance instead. Iteration count and tolerance both got looser here
	# after _monster.set_physics_process(false) was added (see
	# _run_model_build_check's own comment) -- convergence used to look
	# faster than this, but that was actually the engine's own automatic
	# per-frame call double-processing every step alongside this loop's own
	# manual one, not real single-step convergence.
	if absf(_monster.global_position.y - _monster.HOVER_HEIGHT) < 0.05:
		print("PASS: the monster eases back to its own HOVER_HEIGHT with no ground present at all")
	else:
		_fail("monster didn't hover to HOVER_HEIGHT (y=%s, expected %s)" % [_monster.global_position.y, _monster.HOVER_HEIGHT])
	_monster.global_position = Vector3(0, _monster.HOVER_HEIGHT, 0)

func _run_detection_and_chase_check() -> void:
	_player.global_position = Vector3(0, 0, 0)
	_monster.global_position = Vector3(0, _monster.HOVER_HEIGHT, -10.0) # within DETECTION_RANGE (16)

	_monster._physics_process(1.0 / 60.0)
	if _monster._target == _player:
		print("PASS: monster detects and targets a real player within DETECTION_RANGE")
	else:
		_fail("monster didn't target the player (target=%s)" % _monster._target)

	var horizontal_speed := Vector2(_monster.velocity.x, _monster.velocity.z).length()
	if horizontal_speed > 0.1:
		print("PASS: monster starts moving toward the target when out of attack range")
	else:
		_fail("monster isn't moving despite a target being out of attack range (velocity=%s)" % _monster.velocity)

	# Out of detection range entirely -- should drop the target and stop.
	_monster.global_position = Vector3(0, _monster.HOVER_HEIGHT, -50.0)
	_monster.velocity = Vector3.ZERO
	_monster._physics_process(1.0 / 60.0)
	if _monster._target == null:
		print("PASS: monster drops its target once it's out of DETECTION_RANGE")
	else:
		_fail("monster kept a target far outside DETECTION_RANGE")

## The player stays put, well within CLAW_HIT_RADIUS of where the claws
## actually swing to -- the attack's own real hit check should land.
func _run_attack_and_damage_check() -> void:
	_player.global_position = Vector3(0, 0, 0)
	# -1.9, not something smaller like -0.5: the monster's own BoxShape3D
	# collision plus the player's capsule radius mean move_and_slide()'s own
	# depenetration shoves the two apart EVERY frame regardless of velocity
	# once they're too close, before the hit check can ever land (confirmed
	# via the DEBUG_CLAW prints below: self position converged to a fixed
	# floor no matter what Z was actually set to). Real gameplay never hits
	# this, since the chase logic already stops advancing at ATTACK_RANGE
	# (3.0), well outside that collision-contact distance -- this was purely
	# a too-close test fixture. -1.9 comfortably lands the hit check within
	# CLAW_HIT_RADIUS (2.5, raised alongside the collision box's own
	# no-longer-shrunk size -- see CLAW_HIT_RADIUS's own comment) while
	# staying clear of that collision-contact floor.
	_monster.global_position = Vector3(0, _monster.HOVER_HEIGHT, -1.9)
	_monster.velocity = Vector3.ZERO
	_monster._attack_cooldown_remaining = 0.0
	# A directly-set global_position isn't necessarily visible to the physics
	# server's own broadphase/move_and_slide() until the NEXT physics step --
	# same root cause as the glass bottle's own whiff-check bug diagnosed
	# earlier this session (tests/test_glass_bottle_check.gd). Confirmed
	# directly here too: without this, the monster's OWN reported position
	# during the swing consistently drifted to ~Z=-1.5 regardless of what
	# Z was actually just set to, as if move_and_slide() were still working
	# from a stale pre-reposition transform on its first call. One real
	# awaited physics frame (safe now that _monster.set_physics_process(false)
	# means nothing auto-advances the state machine during it) lets that
	# settle before the manually-driven attack sequence begins.
	await get_tree().physics_frame
	var starting_health: float = _player.health

	await _drive_attack_to_completion()

	if _player.health < starting_health:
		print("PASS: monster's claw swing lands and damages the player standing in range")
	else:
		_fail("player health didn't drop after a full attack sequence in range (still %s)" % _player.health)
	if _monster._attack_state == "idle":
		print("PASS: the attack state machine returns to 'idle' once the sequence finishes")
	else:
		_fail("attack state machine didn't return to idle (state='%s')" % _monster._attack_state)

	# Cooldown blocks an immediate second attack.
	var health_after_first_attack: float = _player.health
	_monster._physics_process(1.0 / 60.0)
	if is_equal_approx(_player.health, health_after_first_attack):
		print("PASS: attack cooldown blocks an immediate second attack")
	else:
		_fail("player took damage again before the attack cooldown elapsed")

## Same setup, but the player is standing OUTSIDE CLAW_HIT_RADIUS (still
## within the wider ATTACK_RANGE that triggers the attack in the first
## place) -- confirms the attack is a real distance-to-body hit check, not a
## guaranteed hit just for triggering: this exact scenario (triggered, but
## whiffed) couldn't happen at all under the OLD range-only-gated design.
func _run_attack_miss_check() -> void:
	# 2.75, not the old 2.3 -- CLAW_HIT_RADIUS/ATTACK_RANGE were both raised
	# alongside the collision box's own no-longer-shrunk size (see
	# CLAW_HIT_RADIUS's own comment), so 2.3 now lands inside the new 2.5
	# hit radius instead of outside it. 2.75 sits between the new
	# CLAW_HIT_RADIUS (2.5) and ATTACK_RANGE (3.0), with margin, while
	# staying clear of the box+capsule collision-contact floor (~2.4, see
	# _run_attack_and_damage_check's own comment) so move_and_slide's own
	# depenetration doesn't nudge the distance during the stand-still swing.
	_player.global_position = Vector3(2.75, 0, 0) # within ATTACK_RANGE, outside CLAW_HIT_RADIUS
	_monster.global_position = Vector3(0, _monster.HOVER_HEIGHT, 0)
	_monster.velocity = Vector3.ZERO
	_monster._attack_cooldown_remaining = 0.0
	# Same physics-server transform-lag fix as _run_attack_and_damage_check()
	# above -- a directly-set global_position needs one real physics step
	# before move_and_slide() reliably reflects it.
	await get_tree().physics_frame
	var starting_health: float = _player.health

	await _drive_attack_to_completion()

	if is_equal_approx(_player.health, starting_health):
		print("PASS: an attack that triggers but never brings the monster close enough deals no damage (a real miss)")
	else:
		_fail("player took damage despite standing outside CLAW_HIT_RADIUS (health %s -> %s)" % [starting_health, _player.health])

func _run_death_check() -> void:
	_monster.take_damage(_monster.MAX_HEALTH * 2.0, _player)
	if _monster.is_dead and is_equal_approx(_monster.health, 0.0):
		print("PASS: enough damage kills the monster (is_dead=true, health=0)")
	else:
		_fail("monster didn't die correctly (is_dead=%s health=%s)" % [_monster.is_dead, _monster.health])

	# A dead monster must stop acting entirely.
	_monster.global_position = Vector3(0, _monster.HOVER_HEIGHT, -1.0)
	var player_health_before: float = _player.health
	_monster._physics_process(1.0 / 60.0)
	if is_equal_approx(_player.health, player_health_before):
		print("PASS: a dead monster no longer attacks")
	else:
		_fail("a dead monster still damaged the player")
