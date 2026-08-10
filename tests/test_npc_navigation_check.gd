extends Node3D
## Regression check for npc/npc_steering.gd -- the local obstacle avoidance
## every moving NPC now routes its chase and wander through.
##
## The reported bug: "when an npc hostile or not is following you or heading
## to a destination, he avoids obstacles like he can walk around buildings
## instead of just walking into a wall infinitely."
##
## THE CENTRAL CHECK IS A NEGATIVE ONE MADE POSITIVE. "It moved" proves
## nothing here -- an NPC grinding along a wall is moving the whole time, at
## full speed, forever. What actually distinguishes fixed from broken is
## whether it ends up on the FAR SIDE of the obstacle. So the wall checks
## measure progress past the wall plane, not distance travelled.
##
## Everything is driven against REAL geometry and the real NPC scripts, at
## real physics frames -- no stubbing of the steering itself. A test that
## called steer() directly and asserted the vector it handed back would pass
## just as happily with the NPC still wedged in a corner, because nothing
## would have proven the returned direction is the one the body actually
## moves along.

const WolfScene := preload("res://npc/wolf.tscn")
const CivilianScene := preload("res://npc/civilian.tscn")
const PlayerScene := preload("res://player/player.tscn")
const NpcSteering = preload("res://npc/npc_steering.gd")

# Long enough for a real walk around a 10m-wide wall at wolf chase speed
# (5.5 m/s), with room to spare -- but finite, so a wedged NPC fails rather
# than hanging the suite.
const CHASE_FRAMES := 420

var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	call_deferred("_run_checks")

func _run_checks() -> void:
	_build_ground()
	await _check_open_ground_is_still_a_straight_line()
	await _check_a_wolf_gets_round_a_wall()
	await _check_it_does_not_jitter_against_a_flat_wall()
	await _check_a_wandering_civilian_clears_a_wall()
	await _check_probe_sizing_reads_a_lying_capsule_correctly()

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

## --- 1. Nothing in the way must change nothing. The steering returns the
## goal direction untouched when its probes are clear, so an NPC crossing
## open ground has to travel in the same straight line it always did --
## otherwise this "fix" would have made every chase in the game wander. ---
func _check_open_ground_is_still_a_straight_line() -> void:
	# Inside wolf.gd's own DETECTION_RANGE (14m) -- placed further out it
	# simply never acquires a target and this measures nothing at all.
	var player := _add_player(Vector3(0, 0.1, -12.0))
	# Let the player settle before the wolf looks for it: acquisition runs
	# off a live distance check, and a target still at the origin on frame
	# one reads as already within bite range.
	for _i in range(10):
		await get_tree().physics_frame

	var wolf: CharacterBody3D = WolfScene.instantiate()
	add_child(wolf)
	wolf.global_position = Vector3(0, 0.1, 0)
	for _i in range(60):
		await get_tree().physics_frame

	# Straight at the player means straight down -Z: any lateral drift is
	# the steering interfering where it has no business.
	var drift: float = absf(wolf.global_position.x)
	if drift > 0.5:
		_fail("a wolf crossing open ground drifted %.2fm sideways -- the steering is firing with nothing in the way" % drift)
	elif wolf.global_position.z > -2.0:
		_fail("a wolf on open ground barely moved (z=%.2f)" % wolf.global_position.z)
	else:
		print("PASS: on open ground the chase is still a straight line (%.2fm sideways over %.1fm closed)"
			% [drift, absf(wolf.global_position.z)])

	player.queue_free()
	wolf.queue_free()
	await get_tree().process_frame

## --- 2. THE one. A wall square between a wolf and its target. Before the
## steering the wolf pinned itself against the near face and stayed there
## for as long as you cared to watch; what's asserted is that it ends up
## PAST the wall, which grinding can never produce. ---
func _check_a_wolf_gets_round_a_wall() -> void:
	# 10m wide, well short of the 60m ground plane, so going round is
	# genuinely possible -- this tests avoidance, not a maze.
	var wall := _add_wall(Vector3(0, 1.5, -6.0), Vector3(10.0, 3.0, 1.0))
	# 13m out: inside wolf.gd's own DETECTION_RANGE (14m), so the chase is
	# really driven by target acquisition rather than by anything incidental.
	var player := _add_player(Vector3(0, 0.1, -13.0))
	for _i in range(10):
		await get_tree().physics_frame

	var wolf: CharacterBody3D = WolfScene.instantiate()
	add_child(wolf)
	wolf.global_position = Vector3(0, 0.1, 0)
	if wolf._find_nearest_player() == null:
		_fail("the wolf can't even see the player from 13m -- this check would prove nothing")
	for _i in range(CHASE_FRAMES):
		await get_tree().physics_frame

	var ended_at: Vector3 = wolf.global_position
	var distance_to_player: float = Vector2(ended_at.x - player.global_position.x,
		ended_at.z - player.global_position.z).length()
	if ended_at.z > -6.0:
		_fail("the wolf never got past the wall (stopped at z=%.2f, wall is at z=-6) -- it's still grinding into it"
			% ended_at.z)
	elif distance_to_player > 6.0:
		_fail("the wolf cleared the wall but never reached the player (%.1fm away)" % distance_to_player)
	else:
		print("PASS: a wolf walks around a wall and reaches a player behind it (ended %.1fm away, past z=%.1f)"
			% [distance_to_player, ended_at.z])

	player.queue_free()
	wolf.queue_free()
	wall.queue_free()
	await get_tree().process_frame

## --- 3. The failure mode local steering slips into if the committed side
## isn't held: standing square on to a flat wall, the two tangents score
## identically, so it re-picks every frame and shivers in place instead of
## going anywhere. Caught by watching for a REVERSAL in lateral direction --
## an NPC that keeps changing its mind crosses back and forth repeatedly,
## while one that committed crosses once and keeps going. ---
func _check_it_does_not_jitter_against_a_flat_wall() -> void:
	var wall := _add_wall(Vector3(0, 1.5, -6.0), Vector3(24.0, 3.0, 1.0))
	var player := _add_player(Vector3(0, 0.1, -12.0))

	var wolf: CharacterBody3D = WolfScene.instantiate()
	add_child(wolf)
	wolf.global_position = Vector3(0, 0.1, 0)
	for _i in range(30):
		await get_tree().physics_frame

	var reversals := 0
	var last_sign := 0.0
	for _i in range(180):
		await get_tree().physics_frame
		var vx: float = wolf.velocity.x
		if absf(vx) < 0.4:
			continue
		var s: float = signf(vx)
		if last_sign != 0.0 and s != last_sign:
			reversals += 1
		last_sign = s

	# A couple is fine and expected -- it re-decides when a commitment
	# lapses or the wall's end comes into view. Ten would mean shivering.
	if reversals > 4:
		_fail("the wolf changed which way it was going round %d times in 3s -- it's oscillating, not committing" % reversals)
	else:
		print("PASS: it commits to one way round a flat wall (%d direction changes in 3s)" % reversals)

	player.queue_free()
	wolf.queue_free()
	wall.queue_free()
	await get_tree().process_frame

## --- 4. Not only hostiles. The user asked for "hostile or not", and a
## civilian's WANDER leg is the passive half of that -- it used to grind
## into a wall for the whole six seconds of WANDER_TIMEOUT_SECONDS. Its
## target is set directly rather than waiting for a random roll to happen to
## pick one behind the wall. ---
func _check_a_wandering_civilian_clears_a_wall() -> void:
	var wall := _add_wall(Vector3(0, 1.5, -6.0), Vector3(8.0, 3.0, 1.0))

	var civilian: CharacterBody3D = CivilianScene.instantiate()
	civilian.home_position = Vector3(0, 0.1, 0)
	civilian.wander_radius = 20.0
	add_child(civilian)
	civilian.global_position = Vector3(0, 0.1, 0)
	await get_tree().process_frame

	# Driven straight into the wander-walk state with a target on the far
	# side, rather than idling until a random pick happens to land there.
	civilian._is_walking = true
	civilian._wander_target = Vector3(0, 0.1, -14.0)
	civilian._state_timer = 999.0

	for _i in range(CHASE_FRAMES):
		await get_tree().physics_frame

	var z: float = civilian.global_position.z
	if z > -6.0:
		_fail("a wandering civilian never got past the wall (stopped at z=%.2f)" % z)
	else:
		print("PASS: a wandering civilian walks around a wall in its way (reached z=%.1f)" % z)

	civilian.queue_free()
	wall.queue_free()
	await get_tree().process_frame

## --- 5. The probe has to be sized from the collider THROUGH ITS OWN
## TRANSFORM. wolf.gd and scratcher.gd lay their capsule flat (they're
## quadrupeds), so the shape's `height` is nose-to-tail LENGTH, not how tall
## the animal is -- reading it naively aims a wolf's probe over its own head
## at nothing, and the avoidance silently never fires. This is exactly the
## kind of thing that looks fine in code and does nothing in game, so it's
## asserted numerically rather than trusted. ---
func _check_probe_sizing_reads_a_lying_capsule_correctly() -> void:
	var body := CharacterBody3D.new()
	var col := CollisionShape3D.new()
	var capsule := CapsuleShape3D.new()
	capsule.radius = 0.28
	capsule.height = 1.45
	col.shape = capsule
	# Exactly what wolf.gd does to its own collider.
	col.rotation_degrees = Vector3(90, 0, 0)
	col.position = Vector3(0, 0.55, 0)
	body.add_child(col)
	add_child(body)

	var steering = NpcSteering.new()
	steering.configure_from(body)

	# Laid flat, the capsule's top is its centre plus its RADIUS (0.55 +
	# 0.28 = 0.83), not plus half its height (which would be 1.27 -- above
	# the animal entirely).
	if steering.probe_height > 0.83:
		_fail("the probe sits at %.2fm on a 0.83m-tall wolf -- it's casting over its own head, so nothing is ever detected"
			% steering.probe_height)
	elif steering.probe_height < 0.15:
		_fail("the probe sits at %.2fm -- down in the kerb, which would read every road edge as a wall"
			% steering.probe_height)
	else:
		print("PASS: a flat-laid capsule is measured through its own transform (probe at %.2fm on a 0.83m body)"
			% steering.probe_height)

	# And an upright one still measures the tall way, so the fix didn't
	# simply swap one wrong reading for another.
	var upright := CharacterBody3D.new()
	var ucol := CollisionShape3D.new()
	var ucapsule := CapsuleShape3D.new()
	ucapsule.radius = 0.4
	ucapsule.height = 2.6
	ucol.shape = ucapsule
	ucol.position = Vector3(0, 1.3, 0)
	upright.add_child(ucol)
	add_child(upright)

	var upright_steering = NpcSteering.new()
	upright_steering.configure_from(upright)
	if upright_steering.probe_height < 0.8:
		_fail("an upright 2.6m body probes at only %.2fm" % upright_steering.probe_height)
	else:
		print("PASS: an upright 2.6m body still probes at chest height (%.2fm)" % upright_steering.probe_height)

	body.queue_free()
	upright.queue_free()
	await get_tree().process_frame

func _add_player(at: Vector3) -> CharacterBody3D:
	var player: CharacterBody3D = PlayerScene.instantiate()
	add_child(player)
	player.global_position = at
	player.add_to_group("players")
	return player

func _add_wall(center: Vector3, size: Vector3) -> StaticBody3D:
	var wall := StaticBody3D.new()
	# Layer 1: exactly what city_chunk.gd puts every building on, which is
	# the only layer the steering probes for.
	wall.collision_layer = 1
	var shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = size
	shape.shape = box
	wall.add_child(shape)
	add_child(wall)
	wall.global_position = center
	return wall

func _build_ground() -> void:
	var ground := StaticBody3D.new()
	var shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(60, 1, 60)
	shape.shape = box
	shape.position = Vector3(0, -0.5, 0)
	ground.add_child(shape)
	ground.collision_layer = 1
	add_child(ground)
