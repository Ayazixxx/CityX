extends Node3D
## Regression check for npc/scratcher.gd and its three baked clips.
##
## Two of those clips don't exist in the source asset at all -- the run is
## derived from the walk and the attack is authored from scratch (see
## tests/scratch_scratcher_bake.gd) -- so most of what's checked here is
## that those syntheses actually produced usable animation rather than
## silently-empty or malformed tracks, which is exactly the failure mode
## you'd never notice until you saw the creature standing frozen in-game.

const ScratcherScene := preload("res://npc/scratcher.tscn")
const PlayerScene := preload("res://player/player.tscn")
const WalkAnim := preload("res://npc/scratcher/animations/scratcher_walk.tres")
const RunAnim := preload("res://npc/scratcher/animations/scratcher_run.tres")
const AttackAnim := preload("res://npc/scratcher/animations/scratcher_attack.tres")
const Scratcher = preload("res://npc/scratcher.gd")
const Player = preload("res://player/player.gd")

var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	call_deferred("_run_checks")

func _run_checks() -> void:
	_build_ground()
	_check_matches_sprint_speed()
	_check_clips_are_real()
	_check_attack_moves_head_up_then_down()
	await _check_chases_and_animates()
	await _check_attacks_in_range()
	await _check_dies_and_grants_xp()

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

func _build_ground() -> void:
	var ground := StaticBody3D.new()
	ground.collision_layer = 1
	var shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(400, 1, 400)
	shape.shape = box
	shape.position = Vector3(0, -0.5, 0)
	ground.add_child(shape)
	add_child(ground)

## --- 1. THE explicit request: it runs exactly as fast as a sprinting
## player. Compared against player.gd's own constant, so the two can't
## drift apart later without this failing. ---
func _check_matches_sprint_speed() -> void:
	if not is_equal_approx(Scratcher.CHASE_SPEED, Player.SPRINT_SPEED):
		_fail("Scratcher chase speed %.2f != player SPRINT_SPEED %.2f -- you could outrun (or never escape) it" % [Scratcher.CHASE_SPEED, Player.SPRINT_SPEED])
	else:
		print("PASS: the Scratcher runs at exactly the player's sprint speed (%.1f m/s)" % Scratcher.CHASE_SPEED)

## --- 2. All three clips carry real animation. The run and attack are
## SYNTHESISED, so "did the bake actually produce anything" is a real
## question, not a formality. ---
func _check_clips_are_real() -> void:
	if WalkAnim.loop_mode != Animation.LOOP_LINEAR or RunAnim.loop_mode != Animation.LOOP_LINEAR:
		_fail("walk/run aren't looping -- locomotion would play once and freeze")
	elif AttackAnim.loop_mode != Animation.LOOP_NONE:
		_fail("the attack loops -- it would head-slam forever")
	else:
		print("PASS: walk/run loop, attack is a one-shot")

	if RunAnim.length >= WalkAnim.length:
		_fail("the run (%.2fs) isn't faster than the walk (%.2fs)" % [RunAnim.length, WalkAnim.length])
	else:
		print("PASS: the derived run is a faster cycle than the walk (%.2fs vs %.2fs)" % [RunAnim.length, WalkAnim.length])

	# The run must be a real re-pose, not just the walk at a different
	# speed -- the leg swing was widened, so at least one leg bone's own
	# keys have to genuinely differ from the walk's.
	var differing_leg_tracks := 0
	for t in range(RunAnim.get_track_count()):
		if RunAnim.track_get_type(t) != Animation.TYPE_ROTATION_3D:
			continue
		var bone := str(RunAnim.track_get_path(t).get_concatenated_subnames())
		if not bone.contains("leg"):
			continue
		for wt in range(WalkAnim.get_track_count()):
			if str(WalkAnim.track_get_path(wt)) != str(RunAnim.track_get_path(t)):
				continue
			var count: int = mini(RunAnim.track_get_key_count(t), WalkAnim.track_get_key_count(wt))
			for k in range(count):
				var a: Quaternion = RunAnim.track_get_key_value(t, k)
				var b: Quaternion = WalkAnim.track_get_key_value(wt, k)
				if absf(a.angle_to(b)) > deg_to_rad(1.0):
					differing_leg_tracks += 1
					break
			break
	if differing_leg_tracks == 0:
		_fail("the run's leg poses are identical to the walk's -- it's just the walk sped up")
	else:
		print("PASS: the run genuinely re-poses the legs (%d leg tracks widened)" % differing_leg_tracks)

## --- 3. The attack does what was asked for: head UP, then DOWN. Checked
## as a real pitch angle over time rather than by trusting the keys, since
## the whole clip was authored by hand against this rig. ---
func _check_attack_moves_head_up_then_down() -> void:
	var head_track := -1
	for t in range(AttackAnim.get_track_count()):
		if str(AttackAnim.track_get_path(t).get_concatenated_subnames()) == "head":
			head_track = t
			break
	if head_track < 0:
		_fail("the attack has no 'head' track at all -- nothing would move")
		return

	var count := AttackAnim.track_get_key_count(head_track)
	if count < 3:
		_fail("the attack's head track has only %d keys -- can't express up-then-down" % count)
		return

	# Measured against the clip's own first key (its rest pose): the swing
	# has to go one way, then decisively past rest the other way.
	var rest: Quaternion = AttackAnim.track_get_key_value(head_track, 0)
	var up_swing := 0.0
	var down_swing := 0.0
	var up_time := -1.0
	var down_time := -1.0
	for k in range(count):
		var key: Quaternion = AttackAnim.track_get_key_value(head_track, k)
		var delta := rest.inverse() * key
		# Signed about the swing's own axis: which side of rest it's on.
		var angle: float = delta.get_angle()
		if angle > PI:
			angle -= TAU
		var signed: float = angle * signf(delta.get_axis().x if absf(delta.get_axis().x) > 0.001 else 1.0)
		if signed > up_swing:
			up_swing = signed
			up_time = AttackAnim.track_get_key_time(head_track, k)
		if signed < down_swing:
			down_swing = signed
			down_time = AttackAnim.track_get_key_time(head_track, k)

	if up_swing <= deg_to_rad(5.0) or down_swing >= -deg_to_rad(5.0):
		_fail("the attack doesn't swing the head both ways (up %.1f deg, down %.1f deg)" % [rad_to_deg(up_swing), rad_to_deg(down_swing)])
	elif up_time >= down_time:
		_fail("the head goes DOWN before it goes UP (up at %.2fs, down at %.2fs)" % [up_time, down_time])
	else:
		print("PASS: the attack rears the head up %.0f deg at %.2fs, then slams it %.0f deg down at %.2fs" % [rad_to_deg(up_swing), up_time, absf(rad_to_deg(down_swing)), down_time])

	if Scratcher.ATTACK_HIT_TIME > AttackAnim.length:
		_fail("the damage lands after the clip ends -- it would never hit")
	elif Scratcher.ATTACK_HIT_TIME < up_time:
		_fail("the damage lands while the head is still rearing up -- you'd be hit by nothing")
	else:
		print("PASS: damage lands at %.2fs, on the slam rather than the windup" % Scratcher.ATTACK_HIT_TIME)

## --- 4. It closes the distance, and it uses the run clip to do it. ---
func _check_chases_and_animates() -> void:
	var player: CharacterBody3D = PlayerScene.instantiate()
	add_child(player)
	player.global_position = Vector3.ZERO
	player.add_to_group("players")
	await get_tree().process_frame

	var scratcher: CharacterBody3D = ScratcherScene.instantiate()
	add_child(scratcher)
	scratcher.global_position = Vector3(0, 0, 18)
	await get_tree().process_frame

	var start := scratcher.global_position.distance_to(player.global_position)
	for i in range(30):
		await get_tree().physics_frame
	var ended := scratcher.global_position.distance_to(player.global_position)

	if ended >= start - 0.5:
		_fail("the Scratcher didn't close the distance (%.1fm -> %.1fm)" % [start, ended])
	else:
		print("PASS: it hunts the player down (%.1fm -> %.1fm)" % [start, ended])

	if scratcher._current_clip != "scratcher/run":
		_fail("chasing at range doesn't play the run clip (playing '%s')" % scratcher._current_clip)
	else:
		print("PASS: it plays the run clip while closing at range")

	player.queue_free()
	scratcher.queue_free()

## --- 5. In range, it attacks and the damage actually lands. ---
func _check_attacks_in_range() -> void:
	var player: CharacterBody3D = PlayerScene.instantiate()
	add_child(player)
	player.global_position = Vector3.ZERO
	player.add_to_group("players")
	await get_tree().process_frame
	var health_before: float = player.health

	var scratcher: CharacterBody3D = ScratcherScene.instantiate()
	add_child(scratcher)
	scratcher.global_position = Vector3(0, 0, 1.8) # inside ATTACK_RANGE
	await get_tree().process_frame

	# Long enough for the windup AND the slam to land.
	for i in range(90):
		await get_tree().physics_frame

	if player.health >= health_before:
		_fail("standing point-blank, the Scratcher never damaged the player (health %.0f)" % player.health)
	else:
		print("PASS: the head-slam lands real damage (%.0f -> %.0f health)" % [health_before, player.health])

	player.queue_free()
	scratcher.queue_free()

## --- 6. Dies like every other enemy and pays out its XP. ---
func _check_dies_and_grants_xp() -> void:
	var player: CharacterBody3D = PlayerScene.instantiate()
	add_child(player)
	player.add_to_group("players")
	await get_tree().process_frame

	var scratcher: CharacterBody3D = ScratcherScene.instantiate()
	add_child(scratcher)
	scratcher.global_position = Vector3(0, 0, 12)
	await get_tree().process_frame

	var xp_before: float = player.xp
	scratcher.take_damage(Scratcher.MAX_HEALTH * 2.0, player)
	if not scratcher.is_dead:
		_fail("it survived damage well past its own MAX_HEALTH")
	elif player.xp <= xp_before and player.level <= 1:
		_fail("killing a Scratcher granted no XP")
	else:
		print("PASS: it dies and pays out XP (level %d, xp %.0f)" % [player.level, player.xp])

	player.queue_free()
	scratcher.queue_free()
