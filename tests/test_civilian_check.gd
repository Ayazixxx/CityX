extends Node3D
## Regression check for npc/civilian.gd -- the plain white pedestrian, now
## only ever spawned deliberately (the dev console's own "/spawn civilian").
##
## THE AMBIENT CROWD IS GONE. npc/civilian_population.gd used to keep a dozen
## of these wandering around the player at all times; it was retired to
## archive/civilian_population/ at the user's request ("get rid of the random
## white npcs walking around"), and city.gd no longer creates one. Everything
## that used to check its spawn placement -- the outdoor sidewalk band, the
## derived indoor strip, distance pruning -- went with it, since none of it
## runs anymore. The archived script still holds the reasoning if the crowd
## is ever wanted back.
##
## What's left is the NPC itself, and the thing about it that would silently
## break without ever throwing an error is PASSIVITY: a civilian that quietly
## chases or punches you on sight isn't a civilian, it's a re-skinned cpu.gd.
## Checked by standing a real Player next to one and confirming nothing at
## all happens to them.

const CivilianScene := preload("res://npc/civilian.tscn")
const PlayerScene := preload("res://player/player.tscn")
const CityScene := preload("res://city/city.tscn")

# How long a real streaming city is left running with a player in it before
# declaring it crowd-free. The retired population's own spawn interval was
# 0.75s, so this is several missed spawn ticks, not one lucky frame.
const CROWD_WATCH_FRAMES := 200

var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	call_deferred("_run_checks")

func _run_checks() -> void:
	_build_ground()
	await _check_is_the_unpainted_player_model()
	await _check_wanders_on_its_own()
	await _check_ignores_a_player_standing_next_to_it()
	await _check_turns_hostile_when_attacked()
	await _check_dies_and_pays_xp()
	await _check_you_can_actually_shoot_them_in_the_head()
	await _check_nothing_spawns_a_crowd_anymore()

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

## --- Reported as "it doesn't let me shoot enemies in the head, it just
## goes straight through". The character MODEL is 2.6m tall with its head
## centred at 2.25, but every NPC's collision capsule used to span only
## 0.0-1.8 -- so from the neck up there was no collider at all and a
## hitscan shot passed through empty space.
##
## Fired as a real ray with weapons/pistol.gd's own collision_mask (1|4),
## not by inspecting the capsule's numbers: what matters is whether a shot
## AIMED at the head actually connects, which is the thing that was broken.
## Swept across the whole model height so a future resize can't leave a
## silent dead zone anywhere on the body. ---
func _check_you_can_actually_shoot_them_in_the_head() -> void:
	var civilian := _spawn_civilian(Vector3(0, 0.1, 0), 1.0)
	await get_tree().physics_frame
	await get_tree().physics_frame

	var space := get_world_3d().direct_space_state
	var body_top := 2.6 # measured off the real model, see the class comment
	var misses: Array[String] = []
	# Every 20cm from the shins to the crown. 2.25 is the head centre.
	for step in range(1, 13):
		var height := body_top * (float(step) / 13.0)
		var from := civilian.global_position + Vector3(0, height, 6.0)
		var to := civilian.global_position + Vector3(0, height, -6.0)
		var query := PhysicsRayQueryParameters3D.create(from, to)
		query.collision_mask = 1 | 4 # exactly what pistol.gd's own fire() uses
		var hit := space.intersect_ray(query)
		if hit.is_empty() or hit["collider"] != civilian:
			misses.append("%.2fm" % height)

	if not misses.is_empty():
		_fail("a shot passes straight through the body at these heights: %s (model is %.1fm tall)" % [", ".join(misses), body_top])
	else:
		print("PASS: a hitscan shot connects at every height up the body, head included")

	# And the head specifically, at its real measured centre.
	var head_from := civilian.global_position + Vector3(0, 2.25, 6.0)
	var head_to := civilian.global_position + Vector3(0, 2.25, -6.0)
	var head_query := PhysicsRayQueryParameters3D.create(head_from, head_to)
	head_query.collision_mask = 1 | 4
	var head_hit := space.intersect_ray(head_query)
	if head_hit.is_empty():
		_fail("a shot aimed straight at the head centre (y=2.25) hit nothing at all")
	elif head_hit["collider"] != civilian:
		_fail("a head shot hit %s instead of the civilian" % str(head_hit["collider"]))
	else:
		print("PASS: a shot aimed at the head centre hits the civilian")

	civilian.queue_free()
	await get_tree().process_frame

func _build_ground() -> void:
	var ground := StaticBody3D.new()
	var shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(400, 1, 400)
	shape.shape = box
	shape.position = Vector3(0, -0.5, 0)
	ground.add_child(shape)
	ground.collision_layer = 1
	add_child(ground)

func _spawn_civilian(at: Vector3, wander_radius: float = 9.0) -> CharacterBody3D:
	var civilian: CharacterBody3D = CivilianScene.instantiate()
	civilian.home_position = at
	civilian.wander_radius = wander_radius
	add_child(civilian)
	civilian.global_position = at
	return civilian

## --- 1. It's literally the player's own body, never painted -- that's the
## entire implementation of "just make them white playermodels for now," so
## a stray default colour landing on character.gd's atlas would break it
## with no other symptom. ---
func _check_is_the_unpainted_player_model() -> void:
	var civilian := _spawn_civilian(Vector3(0, 0.1, 0))
	await get_tree().process_frame

	var character: Node = civilian.get_node_or_null("Character")
	if character == null:
		_fail("the civilian has no Character child -- it isn't using the player's own model")
		civilian.queue_free()
		return
	print("PASS: the civilian is built from the same Character model the player uses")

	var atlas: Image = character.atlas_image
	if atlas == null:
		_fail("the civilian's Character has no atlas image at all")
	else:
		# Sampled at several points rather than one: a default paint job
		# covering only, say, the head would still leave the first pixel
		# white.
		var samples := [Vector2i(1, 1), Vector2i(atlas.get_width() / 2, atlas.get_height() / 2), Vector2i(atlas.get_width() - 2, atlas.get_height() - 2)]
		var non_white := Color.WHITE
		var found := false
		for p in samples:
			var c: Color = atlas.get_pixelv(p)
			if not c.is_equal_approx(Color.WHITE):
				non_white = c
				found = true
				break
		if found:
			_fail("a civilian spawned already painted (%s) -- they're meant to be plain white" % str(non_white))
		else:
			print("PASS: a civilian spawns plain white, with no paint applied")

	civilian.queue_free()
	await get_tree().process_frame

## --- 2. It actually goes somewhere. A pedestrian that stands rooted to its
## spawn point forever is the failure mode a wander state machine slips into
## most easily (a target it can never reach, a timer that never fires). ---
func _check_wanders_on_its_own() -> void:
	var civilian := _spawn_civilian(Vector3(0, 0.1, 0))
	await get_tree().process_frame
	# Skips the randomized start stagger (see civilian.gd's own _ready) so
	# this doesn't depend on which idle duration it happened to roll.
	civilian._state_timer = 0.0

	var start := civilian.global_position
	for _i in range(90):
		await get_tree().physics_frame
	var moved := Vector2(civilian.global_position.x - start.x, civilian.global_position.z - start.z).length()
	if moved < 0.3:
		_fail("a civilian moved only %.2fm in 90 physics frames -- it isn't wandering at all" % moved)
	else:
		print("PASS: a civilian wanders off on its own (%.2fm in 90 frames)" % moved)

	civilian.queue_free()
	await get_tree().process_frame

## --- 3. THE defining property: unprovoked, it does nothing to you. Not a
## chase, not a punch. A real Player is used (not a stub) so this exercises
## the same take_damage path a real hit would. ---
func _check_ignores_a_player_standing_next_to_it() -> void:
	var player: CharacterBody3D = PlayerScene.instantiate()
	add_child(player)
	player.global_position = Vector3(0, 0.1, 0)
	player.add_to_group("players")
	await get_tree().process_frame

	# A deliberately tiny wander radius: this check is about whether the
	# civilian TARGETS the player, and a wide random stroll that happens to
	# drift closer would muddy a pure distance assertion.
	var civilian := _spawn_civilian(Vector3(0, 0.1, 8.0), 1.0)
	await get_tree().process_frame

	var health_before: float = player.health
	for _i in range(120):
		await get_tree().physics_frame

	var distance := Vector2(civilian.global_position.x - player.global_position.x, civilian.global_position.z - player.global_position.z).length()
	if civilian._is_hostile:
		_fail("a civilian turned hostile with nobody ever touching it")
	elif distance < 5.0:
		_fail("an unprovoked civilian closed from 8m to %.1fm -- it's chasing the player" % distance)
	elif player.health < health_before:
		_fail("an unprovoked civilian damaged the player (%.0f -> %.0f)" % [health_before, player.health])
	else:
		print("PASS: an unprovoked civilian ignores a player standing 8m away (ended %.1fm, player untouched)" % distance)

	# Passive civilians deliberately stay OUT of lock-on (see civilian.gd's
	# own class comment) so a crowd doesn't steal the reticle from whatever's
	# actually attacking you.
	if civilian.is_in_group("lockable_npcs"):
		_fail("a passive civilian is lock-on targetable -- a crowd would hijack lock-on from real threats")
	else:
		print("PASS: a passive civilian isn't a lock-on target")

	player.queue_free()
	civilian.queue_free()
	await get_tree().process_frame

## --- 4. The whole point of the feature: hit one and it fights back. ---
func _check_turns_hostile_when_attacked() -> void:
	var player: CharacterBody3D = PlayerScene.instantiate()
	add_child(player)
	player.global_position = Vector3(0, 0.1, 0)
	player.add_to_group("players")
	await get_tree().process_frame

	var civilian := _spawn_civilian(Vector3(0, 0.1, 10.0), 1.0)
	await get_tree().process_frame

	var start_distance := Vector2(civilian.global_position.x - player.global_position.x, civilian.global_position.z - player.global_position.z).length()
	var health_before: float = player.health
	civilian.take_damage(5.0, player)

	if not civilian._is_hostile:
		_fail("punching a civilian didn't make it hostile")
	elif not civilian.is_in_group("lockable_npcs"):
		_fail("a provoked civilian never became a lock-on target")
	else:
		print("PASS: attacking a civilian turns it hostile and lock-on targetable")

	for _i in range(180):
		await get_tree().physics_frame

	var end_distance := Vector2(civilian.global_position.x - player.global_position.x, civilian.global_position.z - player.global_position.z).length()
	if end_distance >= start_distance - 0.5:
		_fail("a provoked civilian didn't close in (%.1fm -> %.1fm)" % [start_distance, end_distance])
	else:
		print("PASS: a provoked civilian chases the player down (%.1fm -> %.1fm)" % [start_distance, end_distance])

	if player.health >= health_before:
		_fail("a provoked civilian caught up but never actually hit back (player still at %.0f health)" % player.health)
	else:
		print("PASS: a provoked civilian punches back (player %.0f -> %.0f)" % [health_before, player.health])

	player.queue_free()
	civilian.queue_free()
	await get_tree().process_frame

## --- 5. Dies like every other NPC here: ragdolls, pays XP, and stops being
## a lock-on target (nothing should keep the reticle on a corpse). ---
func _check_dies_and_pays_xp() -> void:
	var player: CharacterBody3D = PlayerScene.instantiate()
	add_child(player)
	player.add_to_group("players")
	await get_tree().process_frame

	var civilian := _spawn_civilian(Vector3(0, 0.1, 6.0), 1.0)
	await get_tree().process_frame

	var xp_before: float = player.xp
	civilian.take_damage(civilian.MAX_HEALTH * 2.0, player)
	if not civilian.is_dead:
		_fail("a civilian survived damage well past its own MAX_HEALTH")
	elif player.xp <= xp_before and player.level <= 1:
		_fail("killing a civilian granted no XP at all")
	elif civilian.is_in_group("lockable_npcs"):
		_fail("a dead civilian is still a lock-on target")
	else:
		print("PASS: a civilian dies, pays out XP (level %d, xp %.0f) and drops off lock-on" % [player.level, player.xp])

	player.queue_free()
	civilian.queue_free()
	await get_tree().process_frame


## --- The removal itself, asserted behaviourally rather than by grepping
## city.gd: stand up a REAL streaming city with a real player in it, let it
## run past several of the retired spawner's own tick intervals, and confirm
## not one civilian ever appears.
##
## Worth a real city rather than a stub. The thing being guarded against is
## the population getting re-wired in by accident -- and the only place that
## could happen is city.gd's own _ready/set_player, which a stub wouldn't
## execute at all. ---
func _check_nothing_spawns_a_crowd_anymore() -> void:
	var city: Node3D = CityScene.instantiate()
	add_child(city)
	await city.initial_load_ready

	var player: CharacterBody3D = PlayerScene.instantiate()
	city.add_child(player)
	player.global_position = Vector3(0, 0.1, 0)
	player.add_to_group("players")
	city.set_player(player)

	for _i in range(CROWD_WATCH_FRAMES):
		await get_tree().physics_frame

	# Counted by SCRIPT, not by node name -- a re-wired spawner would very
	# likely name its children something else entirely.
	var found := _count_civilians(city, player)
	if city.get_node_or_null("CivilianPopulation") != null:
		_fail("the city still builds a CivilianPopulation child")
	elif found > 0:
		_fail("%d civilian(s) appeared on their own in %d frames of a real city -- the ambient crowd is back" % [found, CROWD_WATCH_FRAMES])
	else:
		print("PASS: a real city ran %d frames with a player in it and spawned no pedestrians at all" % CROWD_WATCH_FRAMES)

	player.queue_free()
	city.queue_free()
	await get_tree().process_frame

## Every node under `root` running npc/civilian.gd, skipping the player (its
## own Character is the same model but is not a civilian).
func _count_civilians(root: Node, player: Node) -> int:
	var count := 0
	if root != player and root.get_script() != null \
			and str(root.get_script().resource_path).ends_with("npc/civilian.gd"):
		count += 1
	for child in root.get_children():
		count += _count_civilians(child, player)
	return count
