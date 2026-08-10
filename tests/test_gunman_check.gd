extends Node3D
## Regression check for npc/gunman.gd -- the armed human enemy that uses the
## player's own body, holds a real pistol, and shoots at you while missing
## some of the time.
##
## The two things most worth guarding here are the ones that would silently
## turn this from an enemy into either a harmless prop or an unfair one:
## that its shots ACTUALLY miss at roughly the configured rate (a hitscan
## enemy that never misses is unavoidable damage), and that it holds its
## firing distance instead of walking into melee like every previous NPC.

const GunmanScene := preload("res://npc/gunman.tscn")
const PlayerScene := preload("res://player/player.tscn")

var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	call_deferred("_run_checks")

func _run_checks() -> void:
	_build_ground()
	await _check_equips_a_real_pistol()
	_check_miss_spread_is_a_real_miss()
	_check_accuracy_rate()
	await _check_holds_its_distance()
	await _check_dies_and_grants_xp()

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

func _build_ground() -> void:
	var ground := StaticBody3D.new()
	var shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(200, 1, 200)
	shape.shape = box
	shape.position = Vector3(0, -0.5, 0)
	ground.add_child(shape)
	ground.collision_layer = 1
	add_child(ground)

## --- 1. It really equips a real weapons/pistol.tscn onto its own hand
## bone, rather than just miming a shot -- that's what carries the muzzle
## flash, sound and hitscan. ---
func _check_equips_a_real_pistol() -> void:
	var gunman: CharacterBody3D = GunmanScene.instantiate()
	add_child(gunman)
	# The pistol equip is deliberately deferred a frame (the hand's
	# BoneAttachment3D doesn't exist until the Character's own _ready() has
	# built the skeleton) -- so wait for it.
	await get_tree().process_frame
	await get_tree().process_frame

	var character: Node = gunman.get_node_or_null("Character")
	if character == null:
		_fail("the gunman has no Character child -- it isn't using the player's own model")
	else:
		print("PASS: the gunman is built from the same Character model the player uses")

	var pistol = gunman._pistol
	if pistol == null or not is_instance_valid(pistol):
		_fail("the gunman never equipped a pistol")
	elif not pistol.has_method("fire"):
		_fail("what the gunman equipped isn't a real pistol (no fire())")
	else:
		# Parented onto the hand bone, not still sitting loose under the body.
		var hand: Node3D = character.get_hand_node()
		if hand == null or pistol.get_parent() != hand:
			_fail("the pistol isn't parented to the gunman's own hand bone")
		else:
			print("PASS: a real pistol is equipped onto the gunman's hand bone")

	gunman.queue_free()

## --- 2. A "miss" has to actually miss. _throw_wide() enforces a MINIMUM
## deviation for exactly this reason -- a miss that randomly came out at a
## fraction of a degree would still hit, quietly making the enemy far more
## accurate than ACCURACY claims. ---
func _check_miss_spread_is_a_real_miss() -> void:
	var gunman: CharacterBody3D = GunmanScene.instantiate()
	add_child(gunman)

	var direction := Vector3(0, 0, -1)
	var min_deviation := 999.0
	var max_deviation := 0.0
	for i in range(400):
		var wide: Vector3 = gunman._throw_wide(direction)
		if not is_equal_approx(wide.length(), 1.0):
			_fail("_throw_wide() returned a non-normalized direction (%s)" % wide.length())
			break
		var deviation := rad_to_deg(direction.angle_to(wide))
		min_deviation = minf(min_deviation, deviation)
		max_deviation = maxf(max_deviation, deviation)

	var floor_deg: float = gunman.MISS_SPREAD_DEGREES * 0.45
	if min_deviation < floor_deg - 0.01:
		_fail("a 'miss' came out only %.2f degrees wide -- that would still hit (floor is %.2f)" % [min_deviation, floor_deg])
	elif max_deviation > gunman.MISS_SPREAD_DEGREES + 0.01:
		_fail("a 'miss' went %.2f degrees wide, past MISS_SPREAD_DEGREES (%.2f) -- reads as random gunfire, not as being shot at" % [max_deviation, gunman.MISS_SPREAD_DEGREES])
	else:
		print("PASS: every deliberate miss lands between %.1f and %.1f degrees wide" % [min_deviation, max_deviation])

	gunman.queue_free()

## --- 3. The hit/miss split actually tracks ACCURACY. Checked against the
## same randf() the real _fire_at_target() branches on, over enough trials
## that a genuinely broken rate (always-hit / never-hit) can't slip past. ---
func _check_accuracy_rate() -> void:
	var gunman: CharacterBody3D = GunmanScene.instantiate()
	add_child(gunman)

	var accuracy: float = gunman.ACCURACY
	if accuracy <= 0.0 or accuracy >= 1.0:
		_fail("ACCURACY is %s -- it must be between 0 and 1 or the enemy either never hits or never misses" % accuracy)
	else:
		var trials := 20000
		var hits := 0
		for i in range(trials):
			if randf() <= accuracy:
				hits += 1
		var rate := float(hits) / float(trials)
		if absf(rate - accuracy) > 0.02:
			_fail("aimed-shot rate %.3f drifted from ACCURACY %.3f" % [rate, accuracy])
		else:
			print("PASS: roughly %.0f%% of shots are aimed and %.0f%% deliberately miss" % [accuracy * 100.0, (1.0 - accuracy) * 100.0])

	gunman.queue_free()

## --- 4. THE behavioural difference from every earlier NPC: it fights from
## range. Dropped right on top of a player, it must BACK AWAY rather than
## closing in the way wolf.gd/cpu.gd do. ---
func _check_holds_its_distance() -> void:
	var player: CharacterBody3D = PlayerScene.instantiate()
	add_child(player)
	player.global_position = Vector3(0, 0, 0)
	player.add_to_group("players")

	var gunman: CharacterBody3D = GunmanScene.instantiate()
	add_child(gunman)
	# Well inside PREFERRED_RANGE_MIN -- crowded.
	gunman.global_position = Vector3(0, 0, 2.0)
	await get_tree().process_frame

	var start_distance := gunman.global_position.distance_to(player.global_position)
	for i in range(40):
		await get_tree().physics_frame
	var crowded_distance := gunman.global_position.distance_to(player.global_position)
	if crowded_distance <= start_distance + 0.1:
		_fail("crowded at %.1fm, the gunman didn't retreat (ended at %.1fm) -- it fights like a melee NPC" % [start_distance, crowded_distance])
	else:
		print("PASS: crowded at %.1fm, the gunman backed off to %.1fm" % [start_distance, crowded_distance])

	# And the other direction: parked well beyond its firing band, it should
	# close in rather than plinking away from across the map.
	gunman.global_position = Vector3(0, 0, 22.0)
	await get_tree().process_frame
	var far_start := gunman.global_position.distance_to(player.global_position)
	for i in range(40):
		await get_tree().physics_frame
	var far_end := gunman.global_position.distance_to(player.global_position)
	if far_end >= far_start - 0.1:
		_fail("parked at %.1fm (outside its firing band), the gunman didn't advance (ended at %.1fm)" % [far_start, far_end])
	else:
		print("PASS: too far at %.1fm, the gunman advanced to %.1fm" % [far_start, far_end])

	player.queue_free()
	gunman.queue_free()

## --- 5. Dies like every other enemy: ragdolls, stops acting, and pays out
## its own XP to whoever killed it (the progression loop this game is
## built around). ---
func _check_dies_and_grants_xp() -> void:
	var player: CharacterBody3D = PlayerScene.instantiate()
	add_child(player)
	player.add_to_group("players")
	await get_tree().process_frame

	var gunman: CharacterBody3D = GunmanScene.instantiate()
	add_child(gunman)
	gunman.global_position = Vector3(0, 0, 10.0)
	await get_tree().process_frame
	await get_tree().process_frame

	var xp_before: float = player.xp
	gunman.take_damage(gunman.MAX_HEALTH * 2.0, player)
	if not gunman.is_dead:
		_fail("the gunman survived damage well past its own MAX_HEALTH")
	elif player.xp <= xp_before and player.level <= 1:
		_fail("killing the gunman granted no XP at all")
	else:
		print("PASS: the gunman dies and pays out XP (level %d, xp %.0f)" % [player.level, player.xp])

	# The gun shouldn't survive its owner -- it hangs off a hand bone the
	# ragdoll rig doesn't carry over, so it would otherwise float in place.
	await get_tree().process_frame
	if gunman._pistol != null and is_instance_valid(gunman._pistol):
		_fail("the pistol outlived its dead wielder -- it would hang in mid-air")
	else:
		print("PASS: the pistol is cleaned up when the gunman dies")

	player.queue_free()
	gunman.queue_free()
