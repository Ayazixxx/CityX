extends Node
## Regression check for death-ragdoll momentum (see player.gd's own
## _die()/_apply_death_knockback()/_apply_fall_damage()): a killing blow
## must seed the ragdoll rig with the player's own real velocity at the
## moment of death, not zero -- _die() used to zero player.velocity BEFORE
## character.ragdoll() ever read it, so every death ragdolled from a dead
## stop regardless of how fast the player was actually moving. A plain
## (non-finisher, non-blocked) killing blow must also add a push away from
## the attacker (previously only combo finishers ever applied any
## knockback), and a lethal fall must restore the real pre-impact downward
## speed -- move_and_slide() has already clipped it to ~0 by the time
## _physics_process's own landing check would otherwise read it. ALSO
## covers a real follow-up bug found live: an UNCLAMPED fall-impact speed
## (a tall building drop) fed into the ragdoll rig while it's simultaneously
## colliding with the ground it just landed on caused the whole rig to
## explode into a runaway spin that never settled -- see
## MAX_DEATH_FALL_IMPACT_SPEED's own comment in player.gd. Most checks run
## synchronously calling take_damage()/_apply_fall_damage() directly for
## determinism (matching test_ragdoll_check.gd's own convention of reading
## character._ragdoll_bodies directly rather than a real fall/hit); the
## explosion check specifically needs real engine physics ticks to actually
## watch the rig evolve (RigidBody3D/ConeTwistJoint3D are simulated by the
## physics server, not by calling a script method), so it runs through this
## script's own _physics_process the same way test_ragdoll_check.gd's own
## multi-frame NaN/distance/angular-velocity watch does.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _ok := true
var _explosion_check_character: Node
var _explosion_check_frame := 0
var _explosion_check_running := false

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

func _make_player_pair() -> Dictionary:
	var player: CharacterBody3D = PlayerScene.instantiate()
	add_child(player)
	var character := CharacterScene.instantiate()
	character.set_paintable(false)
	player.add_child(character)
	return {"player": player, "character": character}

func _settle(player: CharacterBody3D) -> void:
	for i in range(10):
		player._physics_process(1.0 / 60.0)

func _hips_velocity(character: Node) -> Vector3:
	var body: RigidBody3D = character._ragdoll_bodies.get("hips")
	if body == null or not is_instance_valid(body):
		return Vector3.ZERO
	return body.linear_velocity

func _ready() -> void:
	_make_ground()
	call_deferred("_run_checks")

func _run_checks() -> void:
	_check_moving_death_carries_momentum()
	_check_plain_lethal_hit_knocks_back()
	_check_lethal_fall_restores_impact_velocity()
	_check_die_still_zeroes_player_velocity_afterward()
	_start_tall_fall_explosion_check()
	# Deliberately does NOT print OVERALL/quit here -- the explosion check
	# just kicked off needs real engine physics ticks to actually watch the
	# ragdoll rig over time (see this script's own class comment), so
	# _physics_process below owns finishing the test once that completes.

## A lethal hit landing while the player already had real horizontal
## velocity (e.g. mid-sprint) -- the ragdoll's own "hips" body must start
## with a real nonzero speed roughly matching it, not zero.
func _check_moving_death_carries_momentum() -> void:
	var pair := _make_player_pair()
	var player: CharacterBody3D = pair["player"]
	var character: Node = pair["character"]
	_settle(player)
	player.health = player.max_health
	player.velocity = Vector3(5.0, 0.0, 0.0)
	player.take_damage(player.max_health) # no attacker -- pure "already moving" case
	var hips_speed: float = _hips_velocity(character).length()
	if hips_speed > 2.0:
		print("PASS: a killing blow while moving seeds the ragdoll with real momentum (hips speed=", hips_speed, ")")
	else:
		_fail("ragdoll started with near-zero momentum despite the player moving at death (hips speed=%s)" % hips_speed)
	player.queue_free()

## A plain (non-finisher, non-blocked) killing blow from a known attacker
## position must push the ragdoll away from the attacker (see
## _apply_death_knockback) -- a real reported complaint: getting finished
## off used to just crumple in place with zero reaction to the hit itself.
func _check_plain_lethal_hit_knocks_back() -> void:
	var pair := _make_player_pair()
	var player: CharacterBody3D = pair["player"]
	var character: Node = pair["character"]
	_settle(player)
	player.global_position = Vector3(0, 0, 0)
	player.health = player.max_health
	player.velocity = Vector3.ZERO
	var attacker := Node3D.new()
	add_child(attacker)
	attacker.global_position = Vector3(0, 0, -3.0) # attacker in front (-Z) -- push should be away, +Z
	player.take_damage(player.max_health, attacker)
	var hips_vel := _hips_velocity(character)
	if hips_vel.z > 1.0:
		print("PASS: a plain killing blow pushes the ragdoll away from the attacker (hips velocity=", hips_vel, ")")
	else:
		_fail("no away-from-attacker push on a plain killing blow (hips velocity=%s)" % hips_vel)
	player.queue_free()
	attacker.queue_free()

## A lethal fall (move_and_slide() has already clipped velocity.y to ~0 by
## the time this runs, see _apply_fall_damage's own comment) must still
## restore the real pre-impact downward speed into the ragdoll -- calling
## _apply_fall_damage directly with a contrived fall distance + impact
## speed, matching exactly what _physics_process's own landing check passes.
## The raw -20.0 here is deliberately well past MAX_DEATH_FALL_IMPACT_SPEED
## (10.0) so this also confirms the clamp is actually being applied, not
## just that SOME downward speed made it through.
func _check_lethal_fall_restores_impact_velocity() -> void:
	var pair := _make_player_pair()
	var player: CharacterBody3D = pair["player"]
	var character: Node = pair["character"]
	_settle(player)
	player.health = player.max_health
	player.velocity = Vector3.ZERO # as if move_and_slide() already clipped it on landing
	var lethal_fall_distance: float = player.SAFE_FALL_DISTANCE + (player.max_health / player.FALL_DAMAGE_PER_METER) + 1.0
	player._apply_fall_damage(lethal_fall_distance, -20.0)
	var hips_vel := _hips_velocity(character)
	if hips_vel.y < -5.0 and hips_vel.y >= -player.MAX_DEATH_FALL_IMPACT_SPEED - 0.01:
		print("PASS: a lethal fall restores real downward momentum into the ragdoll, clamped to MAX_DEATH_FALL_IMPACT_SPEED (hips velocity.y=", hips_vel.y, ")")
	else:
		_fail("lethal fall didn't carry the expected (clamped) downward momentum into the ragdoll (hips velocity.y=%s, expected in [-%s, -5.0))" % [hips_vel.y, player.MAX_DEATH_FALL_IMPACT_SPEED])
	player.queue_free()

## _die() still ends with player.velocity == Vector3.ZERO once the ragdoll
## has already captured the real momentum (see its own comment for why the
## zero has to come AFTER, not before) -- nothing downstream should ever see
## a dead player still "moving."
func _check_die_still_zeroes_player_velocity_afterward() -> void:
	var pair := _make_player_pair()
	var player: CharacterBody3D = pair["player"]
	_settle(player)
	player.health = player.max_health
	player.velocity = Vector3(3.0, -2.0, 1.0)
	player.take_damage(player.max_health)
	if player.velocity == Vector3.ZERO:
		print("PASS: player.velocity is zeroed after death completes")
	else:
		_fail("player.velocity wasn't zeroed after death (velocity=%s)" % player.velocity)
	player.queue_free()

## A real reported bug: dying from a fall off a TALL building (raw impact
## speed far beyond MAX_DEATH_FALL_IMPACT_SPEED) sent the ragdoll rig into a
## runaway spin that never settled -- confirmed directly via a headless
## repro before the clamp existed (angular velocities in the thousands of
## rad/s, bodies flying dozens of meters away). -50.0 here is deliberately
## an extreme "skyscraper" fall speed, well past the clamp, specifically to
## prove the fix holds even at the far end, not just near the clamp's own
## boundary. Kicks off the fall; _physics_process below watches the rig
## across real physics frames the same way test_ragdoll_check.gd's own
## multi-frame NaN/distance/angular-velocity watch does, since the ragdoll
## rig is simulated by the physics server, not advanced by calling a script
## method directly.
func _start_tall_fall_explosion_check() -> void:
	var pair := _make_player_pair()
	var player: CharacterBody3D = pair["player"]
	var character: Node = pair["character"]
	_settle(player)
	player.health = player.max_health
	player.velocity = Vector3.ZERO
	var lethal_fall_distance: float = player.SAFE_FALL_DISTANCE + (player.max_health / player.FALL_DAMAGE_PER_METER) + 1.0
	player._apply_fall_damage(lethal_fall_distance, -50.0)
	_explosion_check_character = character
	_explosion_check_running = true

func _physics_process(_delta: float) -> void:
	if not _explosion_check_running:
		return
	_explosion_check_frame += 1
	var any_nan := false
	var maxdist := 0.0
	var maxangvel := 0.0
	for bone_name in _explosion_check_character._ragdoll_bodies:
		var body: RigidBody3D = _explosion_check_character._ragdoll_bodies[bone_name]
		if not is_instance_valid(body):
			continue
		var pos := body.global_position
		if is_nan(pos.x) or is_nan(pos.y) or is_nan(pos.z):
			any_nan = true
		maxdist = maxf(maxdist, pos.distance_to(Vector3(0, 1, 0)))
		var angvel := body.angular_velocity
		if is_nan(angvel.x) or is_nan(angvel.y) or is_nan(angvel.z):
			any_nan = true
		maxangvel = maxf(maxangvel, angvel.length())
	if any_nan:
		_fail("ragdoll went NaN at frame %d after a clamped-but-still-hard fall" % _explosion_check_frame)
		_explosion_check_running = false
	# Same "flew away" distance threshold test_ragdoll_check.gd's own watch
	# already uses -- the character starts at the origin, so nothing should
	# ever wander more than a few body-lengths away in any direction.
	elif maxdist > 15.0:
		_fail("ragdoll exploded -- a body flew to %sm away at frame %d after a clamped fall" % [maxdist, _explosion_check_frame])
		_explosion_check_running = false
	elif _explosion_check_frame >= 180:
		_explosion_check_running = false
		if maxangvel < 2.0:
			print("PASS: a hard (clamped) fall death settles normally instead of exploding (max angular velocity=", maxangvel, " rad/s)")
		else:
			_fail("ragdoll still spinning hard after settling time following a clamped fall (max angular velocity=%s rad/s)" % maxangvel)

	if not _explosion_check_running:
		print("OVERALL: ", "PASS" if _ok else "FAIL")
		get_tree().quit(0 if _ok else 1)
