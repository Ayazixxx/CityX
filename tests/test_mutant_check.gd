extends Node
## Regression check for the hostile mutant enemy (npc/mutant.gd, spawned via
## the dev console's own "/spawn mutant"): confirms the imported skinned
## model + AnimationPlayer actually build with every expected clip present,
## that it finds a nearby real Player within DETECTION_RANGE and chases it,
## that its weighted 3-way attack choice (40% jump attack / 30% axe spin /
## 30% shout-swing) lands roughly on target over many rolls, that damage
## only actually applies at each attack's own impact instant (not
## immediately on trigger) and respects ATTACK_HIT_RADIUS, that the jump
## attack deals 20% more damage than the other two, and that death ragdolls
## (the whole model reparented into a real tumbling RigidBody3D) rather than
## just vanishing.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")
const MutantScene := preload("res://npc/mutant.tscn")

var _ok := true
var _player: CharacterBody3D
var _mutant: CharacterBody3D

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
	_run_attack_distribution_check()
	_run_attack_timing_and_damage_check()
	_run_death_and_ragdoll_check()
	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

func _run_model_build_check() -> void:
	if not _player.is_in_group("players"):
		_fail("player.gd's own _ready() didn't add it to the 'players' group")
	else:
		print("PASS: player is in the 'players' group")

	_mutant = MutantScene.instantiate()
	add_child(_mutant)

	if _mutant._anim_player == null:
		_fail("mutant's own _build_model() didn't find an AnimationPlayer under the imported mesh")
	else:
		print("PASS: mutant model built with a real AnimationPlayer")

	var expected_anims := [_mutant.ANIM_IDLE, _mutant.ANIM_RUN, _mutant.ANIM_AXE_SPIN, _mutant.ANIM_SHOUT_SWING, _mutant.ANIM_JUMP_ATTACK]
	var missing := []
	for anim_name in expected_anims:
		if not _mutant._anim_player.has_animation(anim_name):
			missing.append(anim_name)
	if missing.is_empty():
		print("PASS: all 5 expected animation clips are present (idle/run/axe_spin_attack/shout_swing/jump_attack)")
	else:
		_fail("missing animation clips: %s" % [missing])

	_run_root_motion_check()
	_run_ground_level_check()

	if _mutant.health == _mutant.MAX_HEALTH and not _mutant.is_dead:
		print("PASS: mutant starts at full health, alive")
	else:
		_fail("mutant didn't start correctly (health=%s is_dead=%s)" % [_mutant.health, _mutant.is_dead])

	if _mutant._anim_player.current_animation == _mutant.ANIM_IDLE:
		print("PASS: mutant starts playing its idle animation")
	else:
		_fail("mutant's starting animation was '%s', expected idle" % _mutant._anim_player.current_animation)

## Real reported bug: the raw Meshy source clips carry baked root motion on
## the Hips bone that this project never applies to the real CharacterBody3D
## (movement is 100% code-driven, see mutant.gd's own class comment) -- the
## jump_attack clip in particular had a huge one-way horizontal drift (the
## mesh visually lunging several meters forward independent of the actual
## collision body, then snapping back when the clip ended), reported
## directly as "he grows and then shrinks back down". Fixed at the source
## (see the offline pipeline notes) by holding the Hips position track's
## two horizontal components constant per clip while leaving the vertical
## component (the actual jump/leap motion) untouched. This check catches a
## regression on either half of that fix: a re-baked clip that reintroduces
## real horizontal drift, or one that overcorrects and flattens the
## vertical motion too (which would silently kill the jump's own visual
## point).
func _run_root_motion_check() -> void:
	var lib: AnimationLibrary = _mutant._anim_player.get_animation_library("")
	for anim_name in [_mutant.ANIM_AXE_SPIN, _mutant.ANIM_SHOUT_SWING, _mutant.ANIM_JUMP_ATTACK]:
		var anim: Animation = lib.get_animation(anim_name)
		var hips_track := -1
		for i in range(anim.get_track_count()):
			var path := str(anim.track_get_path(i))
			if anim.track_get_type(i) == Animation.TYPE_POSITION_3D and "Hips" in path:
				hips_track = i
				break
		if hips_track < 0:
			_fail("%s has no Hips position track to check" % anim_name)
			continue
		var n_keys := anim.track_get_key_count(hips_track)
		var min_v: Vector3 = anim.track_get_key_value(hips_track, 0)
		var max_v: Vector3 = min_v
		for k in range(n_keys):
			var v: Vector3 = anim.track_get_key_value(hips_track, k)
			min_v.x = minf(min_v.x, v.x); max_v.x = maxf(max_v.x, v.x)
			min_v.y = minf(min_v.y, v.y); max_v.y = maxf(max_v.y, v.y)
			min_v.z = minf(min_v.z, v.z); max_v.z = maxf(max_v.z, v.z)
		var horizontal_span := maxf(max_v.x - min_v.x, max_v.z - min_v.z)
		if horizontal_span < 20.0:
			print("PASS: %s's Hips bone has no significant baked horizontal drift (span=%.1f)" % [anim_name, horizontal_span])
		else:
			_fail("%s's Hips bone still drifts %.1f horizontally -- the mesh will visually slide independent of the real collision body" % [anim_name, horizontal_span])

	# jump_attack specifically needs to KEEP a real, large vertical range --
	# this is what actually reads as a jump; the fix above must not have
	# flattened it along with the horizontal drift.
	var jump_anim: Animation = lib.get_animation(_mutant.ANIM_JUMP_ATTACK)
	for i in range(jump_anim.get_track_count()):
		var path := str(jump_anim.track_get_path(i))
		if jump_anim.track_get_type(i) == Animation.TYPE_POSITION_3D and "Hips" in path:
			var n_keys := jump_anim.track_get_key_count(i)
			var min_y: float = jump_anim.track_get_key_value(i, 0).y
			var max_y: float = min_y
			for k in range(n_keys):
				var y: float = jump_anim.track_get_key_value(i, k).y
				min_y = minf(min_y, y)
				max_y = maxf(max_y, y)
			if max_y - min_y > 50.0:
				print("PASS: jump_attack's Hips bone still has real vertical motion (span=%.1f) -- the leap itself wasn't flattened by the drift fix" % (max_y - min_y))
			else:
				_fail("jump_attack's Hips vertical span is only %.1f -- the horizontal-drift fix may have also flattened the actual jump" % (max_y - min_y))
			break

## Real reported bug: Meshy baked each clip's own Hips bone with a
## DIFFERENT baseline height (confirmed directly via a track dump: idle's
## own lowest point sits at a noticeably lower raw Y than run/axe_spin/
## shout_swing's own lowest points) even though every clip nominally shares
## the same skeleton/rest pose -- since this CharacterBody3D never applies
## root motion, nothing else compensated for that, so switching clips
## visibly shifted the model up or down relative to the actual, correctly-
## grounded collision floor, reported directly as "some of the mutant's
## animations leave him hovering above the ground slightly". Fixed by
## _ground_animations() (called from _build_model()), which shifts each
## clip's own Hips Y keyframes by a constant per-clip delta so every clip's
## OWN lowest point lands at idle's own lowest point. This check catches a
## regression on that fix specifically -- every clip's min_y should now
## match idle's, within floating-point tolerance.
func _run_ground_level_check() -> void:
	var lib: AnimationLibrary = _mutant._anim_player.get_animation_library("")
	var idle_anim: Animation = lib.get_animation(_mutant.ANIM_IDLE)
	var idle_track := _hips_position_track(idle_anim)
	if idle_track < 0:
		_fail("idle has no Hips position track to check for ground-level consistency")
		return
	var idle_min := INF
	for k in range(idle_anim.track_get_key_count(idle_track)):
		idle_min = minf(idle_min, idle_anim.track_get_key_value(idle_track, k).y)

	var all_grounded := true
	for anim_name in [_mutant.ANIM_IDLE, _mutant.ANIM_RUN, _mutant.ANIM_AXE_SPIN, _mutant.ANIM_SHOUT_SWING, _mutant.ANIM_JUMP_ATTACK]:
		var anim: Animation = lib.get_animation(anim_name)
		var track := _hips_position_track(anim)
		if track < 0:
			_fail("%s has no Hips position track to check for ground-level consistency" % anim_name)
			all_grounded = false
			continue
		var clip_min := INF
		for k in range(anim.track_get_key_count(track)):
			clip_min = minf(clip_min, anim.track_get_key_value(track, k).y)
		if not is_equal_approx(clip_min, idle_min) and absf(clip_min - idle_min) > 0.05:
			_fail("%s's Hips lowest point (%.2f) doesn't match idle's own (%.2f) -- would visibly hover/sink relative to idle" % [anim_name, clip_min, idle_min])
			all_grounded = false
	if all_grounded:
		print("PASS: every clip's own lowest Hips point is grounded to match idle's (no visible hover/sink switching clips)")

static func _hips_position_track(anim: Animation) -> int:
	for i in range(anim.get_track_count()):
		if anim.track_get_type(i) == Animation.TYPE_POSITION_3D and str(anim.track_get_path(i)).findn("hips") != -1:
			return i
	return -1

func _run_detection_and_chase_check() -> void:
	_player.global_position = Vector3(0, 0, 0)
	# Comfortably within DETECTION_RANGE but outside ATTACK_RANGE (both
	# read directly off the mutant itself, not hardcoded, since
	# ATTACK_RANGE scales with MODEL_SCALE -- a fixed distance here would
	# silently drift into "already in attack range" territory if that scale
	# ever changes again, exactly what happened the first time this test
	# was written against the pre-5x-scale ATTACK_RANGE).
	var chase_distance: float = (_mutant.ATTACK_RANGE + _mutant.DETECTION_RANGE) / 2.0
	_mutant.global_position = Vector3(0, 0, -chase_distance)

	_mutant._physics_process(1.0 / 60.0)
	if _mutant._target == _player:
		print("PASS: mutant detects and targets a real player within DETECTION_RANGE")
	else:
		_fail("mutant didn't target the player (target=%s)" % _mutant._target)

	if Vector2(_mutant.velocity.x, _mutant.velocity.z).length() > 0.1:
		print("PASS: mutant starts moving toward the target when out of attack range")
	else:
		_fail("mutant isn't moving despite a target being out of attack range (velocity=%s)" % _mutant.velocity)

	if _mutant._anim_player.current_animation == _mutant.ANIM_RUN:
		print("PASS: mutant plays its run animation while chasing")
	else:
		_fail("mutant's animation while chasing was '%s', expected run" % _mutant._anim_player.current_animation)

	# Out of detection range entirely -- should drop the target and stop.
	_mutant.global_position = Vector3(0, 0, -50.0)
	_mutant.velocity = Vector3.ZERO
	_mutant._physics_process(1.0 / 60.0)
	if _mutant._target == null:
		print("PASS: mutant drops its target once it's out of DETECTION_RANGE")
	else:
		_fail("mutant kept a target far outside DETECTION_RANGE")

## Forces _start_attack() directly (not through the full physics/cooldown
## flow, which _run_attack_timing_and_damage_check() already exercises) a
## large number of times, tallying which of the 3 clips got picked each
## time -- a statistical check on JUMP_ATTACK_CHANCE's own 40/30/30 split,
## same "roll many times, check the proportions land close to the target"
## shape any RNG-driven pick in this codebase needs (a single roll can't
## prove a distribution).
func _run_attack_distribution_check() -> void:
	const TRIALS := 400
	var counts := {"jump_attack": 0, "axe_spin_attack": 0, "shout_swing": 0}
	for i in range(TRIALS):
		_mutant._attack_state = "idle" # reset so _start_attack() doesn't assert/short-circuit
		_mutant._start_attack()
		var anim: String = _mutant._anim_player.current_animation
		if counts.has(anim):
			counts[anim] += 1
		else:
			_fail("_start_attack() played an unexpected animation: '%s'" % anim)
			return

	var jump_frac := float(counts["jump_attack"]) / TRIALS
	var axe_frac := float(counts["axe_spin_attack"]) / TRIALS
	var shout_frac := float(counts["shout_swing"]) / TRIALS
	print("attack distribution over %d trials: jump=%.2f axe_spin=%.2f shout_swing=%.2f" % [TRIALS, jump_frac, axe_frac, shout_frac])

	# Generous tolerance (+-0.08) -- this is a statistical check on a real
	# RNG, not an exact-count assertion.
	if is_equal_approx(snappedf(jump_frac, 0.01), 0.4) or absf(jump_frac - 0.4) < 0.08:
		print("PASS: jump attack lands close to its own 40% share")
	else:
		_fail("jump attack landed at %.2f, expected close to 0.40" % jump_frac)
	if absf(axe_frac - 0.3) < 0.08:
		print("PASS: axe spin attack lands close to its own 30% share")
	else:
		_fail("axe spin attack landed at %.2f, expected close to 0.30" % axe_frac)
	if absf(shout_frac - 0.3) < 0.08:
		print("PASS: shout-swing attack lands close to its own 30% share")
	else:
		_fail("shout-swing attack landed at %.2f, expected close to 0.30" % shout_frac)

## Real reported behavior spec: the jump attack must deal 20% more damage
## than the other two, and damage must only apply at each attack's own
## impact instant (not the moment the animation starts), respecting
## ATTACK_HIT_RADIUS.
func _run_attack_timing_and_damage_check() -> void:
	_mutant.global_position = Vector3(0, 0, -1.0) # comfortably within ATTACK_HIT_RADIUS regardless of MODEL_SCALE
	_player.global_position = Vector3(0, 0, 0)
	_mutant._target = _player
	_mutant._attack_cooldown_remaining = 0.0

	# Force a jump attack specifically (retry until the roll lands on it --
	# a real roll, not a hand-picked value, so this still exercises the
	# actual RNG-driven _start_attack() path).
	var attempts := 0
	while _mutant._anim_player.current_animation != _mutant.ANIM_JUMP_ATTACK and attempts < 200:
		_mutant._attack_state = "idle"
		_mutant._start_attack()
		attempts += 1
	if _mutant._anim_player.current_animation != _mutant.ANIM_JUMP_ATTACK:
		_fail("couldn't roll a jump attack in 200 attempts -- distribution check above should already have failed first")
		return

	if not is_equal_approx(_mutant._attack_damage, _mutant.ATTACK_DAMAGE * _mutant.JUMP_ATTACK_DAMAGE_MULT):
		_fail("jump attack damage is %s, expected ATTACK_DAMAGE * 1.2 = %s" % [_mutant._attack_damage, _mutant.ATTACK_DAMAGE * _mutant.JUMP_ATTACK_DAMAGE_MULT])
	else:
		print("PASS: jump attack deals 20% more damage than the base attack damage")

	var starting_health: float = _player.health
	# Well before the impact instant -- damage must NOT have applied yet.
	_mutant._update_attack(0.01)
	if is_equal_approx(_player.health, starting_health):
		print("PASS: no damage applies before the attack's own impact instant")
	else:
		_fail("player took damage before the jump attack's own impact instant")

	# Advance time past the recorded impact instant.
	_mutant._update_attack(_mutant._attack_impact_time)
	if _player.health < starting_health:
		print("PASS: damage applies once the attack reaches its own impact instant")
	else:
		_fail("player didn't take damage at/after the jump attack's own impact instant")

	# Only one hit per attack, even if more time passes mid-attack.
	var health_after_impact: float = _player.health
	_mutant._update_attack(0.05)
	if is_equal_approx(_player.health, health_after_impact):
		print("PASS: an attack only lands one hit, even with more frames before it ends")
	else:
		_fail("player took damage a second time from the same attack")

	# Let the attack fully finish -- state returns to idle.
	_mutant._update_attack(_mutant._attack_duration)
	if _mutant._attack_state == "idle":
		print("PASS: attack state returns to idle once the clip's own duration has elapsed")
	else:
		_fail("attack state is still '%s' after its own duration elapsed" % _mutant._attack_state)

	# A target outside ATTACK_HIT_RADIUS at the impact instant takes no damage.
	_mutant._attack_state = "idle"
	_mutant._start_attack()
	_player.global_position = Vector3(0, 0, -20.0) # well outside ATTACK_HIT_RADIUS
	var health_before_whiff: float = _player.health
	_mutant._update_attack(_mutant._attack_impact_time + 0.001)
	if is_equal_approx(_player.health, health_before_whiff):
		print("PASS: a target outside ATTACK_HIT_RADIUS at the impact instant takes no damage (a real miss)")
	else:
		_fail("player took damage while well outside ATTACK_HIT_RADIUS")
	_player.global_position = Vector3(0, 0, 0)
	_mutant._attack_state = "idle"

func _run_death_and_ragdoll_check() -> void:
	var model_ref: Node3D = _mutant._model
	_mutant.take_damage(_mutant.MAX_HEALTH * 2.0, _player)
	if _mutant.is_dead and is_equal_approx(_mutant.health, 0.0):
		print("PASS: enough damage kills the mutant (is_dead=true, health=0)")
	else:
		_fail("mutant didn't die correctly (is_dead=%s health=%s)" % [_mutant.is_dead, _mutant.health])

	if is_instance_valid(model_ref) and model_ref.get_parent() is RigidBody3D:
		print("PASS: the whole model was reparented into a real RigidBody3D on death (ragdoll)")
	else:
		_fail("model wasn't reparented into a RigidBody3D on death")

	if _mutant._collision.disabled:
		print("PASS: the original hitbox is disabled once dead (the ragdoll body carries its own collision now)")
	else:
		_fail("original hitbox is still enabled after death")

	# A dead mutant must stop acting entirely.
	_mutant.global_position = Vector3(0, 0, -1.0)
	_player.global_position = Vector3(0, 0, 0)
	var player_health_before: float = _player.health
	_mutant._physics_process(1.0 / 60.0)
	if is_equal_approx(_player.health, player_health_before):
		print("PASS: a dead mutant no longer attacks")
	else:
		_fail("a dead mutant still damaged the player")
