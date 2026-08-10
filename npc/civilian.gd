extends CharacterBody3D
## City pedestrian -- the plain, unpainted player model (see CharacterScene)
## wandering around wherever it was dropped.
##
## ONLY EVER SPAWNED DELIBERATELY, via the dev console's own "/spawn
## civilian". npc/civilian_population.gd used to keep a dozen of these
## streaming in and out around the player; it was retired to
## archive/civilian_population/ (the crowd wasn't wanted), so nothing places
## one automatically anymore.
##
## Deliberately WHITE with no paint of its own: character.gd's own
## _build_atlas() already fills a fresh atlas with Color.WHITE and nothing
## here ever paints over it, so "just make them white playermodels for now"
## needs no colour code at all -- a civilian is literally a Character that
## was never painted.
##
## Behaviour is npc/cpu.gd's own passive-until-hit shape (see its class
## comment) with a wander layer on top: idle for a beat, pick a nearby
## point, walk to it, idle again. The instant anything actually damages one
## (see take_damage) it turns PERMANENTLY hostile toward whoever did it and
## fights back with the same character.gd punch()/set_walking() calls the
## player itself drives -- it never calms back down, matching cpu.gd.
##
## No pathfinding (there's no navmesh anywhere in this project), but not a
## straight line either: both the wander walk and the chase go through
## npc/npc_steering.gd, which probes ahead and rounds an obstacle rather
## than grinding into it. Enough for the city's own convex buildings; a
## wander target genuinely unreachable still gives up on its own walk timer
## (see WANDER_TIMEOUT_SECONDS).
##
## NOT in the "lockable_npcs" group while passive, unlike every other NPC in
## this project -- deliberate. Any bystander standing around is something
## pressing C could grab instead of whatever's actually attacking you. One
## turns lockable the moment it turns hostile (see _become_hostile), which is
## exactly when locking onto it is worth doing.

const GRAVITY := 9.8
const FLOOR_MAX_ANGLE_DEGREES := 80.0
const NpcSteering = preload("res://npc/npc_steering.gd")
const CharacterScene := preload("res://character/character.tscn")

const MAX_HEALTH := 100.0
# A civilian is the weakest thing in the city -- rated below the wolf
# (level 1, 50 XP) since it doesn't fight back until provoked. Same
# hand-set ENEMY_LEVEL/XP_REWARD pair wolf.gd's own comment prescribes
# until some shared enemy base class exists to hang a formula off of.
const ENEMY_LEVEL := 1
const XP_REWARD := 20.0

# Ambient strolling pace -- deliberately below player.gd's own walk SPEED
# (4.0) so pedestrians read as ambling rather than marching, and so walking
# past one always overtakes it.
const WANDER_SPEED := 1.5
# Once provoked they move at the player's own walk speed, same as cpu.gd --
# fast enough to be a real chase, slow enough that sprinting escapes.
const CHASE_SPEED := 4.0

# Same numbers as player.gd's own PUNCH_RANGE/PUNCH_DAMAGE/
# PUNCH_COOLDOWN_SECONDS, for the same reason cpu.gd copies them (no raycast
# needed -- the chase logic has already confirmed the target is in range).
const PUNCH_RANGE := 2.5
const PUNCH_DAMAGE := 10.0
const PUNCH_COOLDOWN_SECONDS := 0.35

# How far a wander target may sit from home_position. Overridable per
# civilian (`wander_radius` below) -- one dropped in the street can have a
# wide radius so it actually covers ground, one dropped indoors a tight one
# so it stays in the room rather than wandering into a stairwell.
const DEFAULT_WANDER_RADIUS := 9.0
const IDLE_MIN_SECONDS := 1.2
const IDLE_MAX_SECONDS := 4.5
# A walk leg gives up after this long even if it never arrived -- the only
# thing that unsticks a civilian that walked into a wall, since there's no
# pathfinding to route it around one.
const WANDER_TIMEOUT_SECONDS := 6.0
const ARRIVE_DISTANCE := 0.6
# How many candidate points to try before giving up and idling another beat.
const WANDER_TARGET_ATTEMPTS := 6
# A candidate point is only accepted if there's real ground under it within
# this vertical band of home_position -- keeps a civilian from setting off
# toward a point over a basement stairwell hole, off a roof edge, or through
# a floor onto a lower storey.
const GROUND_PROBE_UP := 1.5
const GROUND_PROBE_DOWN := 2.0

## Where this civilian wanders around -- set by whoever spawns it, BEFORE
## add_child (defaults to wherever it lands if left at the sentinel below).
var home_position := Vector3.INF
var wander_radius := DEFAULT_WANDER_RADIUS

var health := MAX_HEALTH
var is_dead := false

var _character: Node3D
# Set the instant a hit with a valid attacker lands (see take_damage) and
# never cleared -- once provoked, a civilian stays hostile for good. _target
# re-latches onto whoever hit it MOST RECENTLY, so a second attacker steals
# aggro, same as cpu.gd.
var _is_hostile := false
var _target: Node3D = null
var _punch_cooldown_remaining := 0.0

# Passive wander state. _wander_target is only meaningful while _is_walking.
var _is_walking := false
var _wander_target := Vector3.ZERO
var _state_timer := 0.0
## Rounds buildings instead of walking into them -- see npc/npc_steering.gd.
var _steering := NpcSteering.new()

func _ready() -> void:
	floor_max_angle = deg_to_rad(FLOOR_MAX_ANGLE_DEGREES)
	# The collider comes from civilian.tscn, so it already exists here.
	_steering.configure_from(self)
	collision_layer = 4 # same "NPC body" layer cpu.gd/gunman.gd's own bodies use
	# 4 as well as 1, so pedestrians bump off each other and off the player
	# instead of standing inside one another on a crowded sidewalk.
	collision_mask = 1 | 4

	_character = CharacterScene.instantiate()
	_character.set_paintable(false)
	add_child(_character)
	# Same flip main.gd's own _place_character_on applies to the player's own
	# Character child -- the model's local +Z ("face") has to point where
	# this body's own forward (-Z) does, or it walks backwards.
	_character.rotation = Vector3(0, PI, 0)

	if home_position == Vector3.INF:
		home_position = global_position
	# Staggered rather than starting every civilian at the same instant, so a
	# freshly-spawned crowd doesn't all step off on the same frame.
	_state_timer = randf_range(0.0, IDLE_MAX_SECONDS)

func _physics_process(delta: float) -> void:
	if is_dead:
		return
	if not is_on_floor():
		velocity.y -= GRAVITY * delta
	else:
		velocity.y = 0.0

	_punch_cooldown_remaining = maxf(0.0, _punch_cooldown_remaining - delta)

	# A target can go invalid (freed) or die without a fresh take_damage call
	# ever saying so -- rechecked every frame rather than trusting whatever
	# _target pointed at when it was set, same as cpu.gd.
	var target_alive := _is_hostile and _target != null and is_instance_valid(_target) and not bool(_target.get("is_dead"))
	if target_alive:
		_process_hostile(delta)
	else:
		_process_wander(delta)

	move_and_slide()

## Chase-and-punch, identical in shape to cpu.gd's own hostile branch.
func _process_hostile(delta: float) -> void:
	var to_target: Vector3 = _target.global_position - global_position
	to_target.y = 0.0
	var distance := to_target.length()
	if distance > PUNCH_RANGE:
		# Steered around whatever's between it and you, and faced along
		# where it's actually walking -- see npc/npc_steering.gd.
		var direction := _steering.steer(self, to_target / maxf(distance, 0.0001), delta)
		_face(direction)
		velocity.x = direction.x * CHASE_SPEED
		velocity.z = direction.z * CHASE_SPEED
		_set_walking(true)
	else:
		velocity.x = 0.0
		velocity.z = 0.0
		_set_walking(false)
		if _punch_cooldown_remaining <= 0.0:
			_throw_punch()

## Idle <-> walk, on a timer. Both legs run off the same _state_timer: it
## counts down whichever state is current, and hitting zero flips to the
## other one (a walk leg also ends early on arrival, see ARRIVE_DISTANCE).
func _process_wander(delta: float) -> void:
	_state_timer -= delta

	if _is_walking:
		var to_target := _wander_target - global_position
		to_target.y = 0.0
		if to_target.length() <= ARRIVE_DISTANCE or _state_timer <= 0.0:
			_begin_idle()
		else:
			# Steered too, not only the chase: a wander target picked on the
			# far side of a wall used to be a six-second grind against it
			# (WANDER_TIMEOUT_SECONDS) rather than a walk round.
			var direction := _steering.steer(self, to_target.normalized(), delta)
			_face(direction)
			velocity.x = direction.x * WANDER_SPEED
			velocity.z = direction.z * WANDER_SPEED
			return
	elif _state_timer <= 0.0:
		_begin_walk()
		return

	velocity.x = 0.0
	velocity.z = 0.0

func _begin_idle() -> void:
	_is_walking = false
	_state_timer = randf_range(IDLE_MIN_SECONDS, IDLE_MAX_SECONDS)
	velocity.x = 0.0
	velocity.z = 0.0
	_set_walking(false)

## Picks the next wander target and steps off toward it. Falls back to
## another idle beat if no candidate point has real ground under it (see
## _ground_position_at) -- better a civilian that stands still a moment
## longer than one that walks off a rooftop or into a basement hole.
func _begin_walk() -> void:
	for _attempt in WANDER_TARGET_ATTEMPTS:
		var angle := randf_range(0.0, TAU)
		var radius := randf_range(wander_radius * 0.35, wander_radius)
		var candidate := home_position + Vector3(cos(angle), 0.0, sin(angle)) * radius
		var grounded := _ground_position_at(candidate)
		if grounded == Vector3.INF:
			continue
		_wander_target = grounded
		_is_walking = true
		_state_timer = WANDER_TIMEOUT_SECONDS
		_set_walking(true)
		return
	_begin_idle()

## The real floor position under `point` (whose own Y is ignored, probed
## from home_position's own height instead), or Vector3.INF if there's no
## walkable ground within the probe band. World geometry only (layer 1) --
## another pedestrian standing on the spot shouldn't invalidate it.
func _ground_position_at(point: Vector3) -> Vector3:
	var from := Vector3(point.x, home_position.y + GROUND_PROBE_UP, point.z)
	var to := Vector3(point.x, home_position.y - GROUND_PROBE_DOWN, point.z)
	var query := PhysicsRayQueryParameters3D.create(from, to)
	query.collision_mask = 1
	query.exclude = [self]
	var hit := get_world_3d().direct_space_state.intersect_ray(query)
	if hit.is_empty():
		return Vector3.INF
	return hit["position"]

func _throw_punch() -> void:
	_punch_cooldown_remaining = PUNCH_COOLDOWN_SECONDS
	if _character != null and _character.has_method("punch"):
		_character.punch(false)
	if _target != null and is_instance_valid(_target) and _target.has_method("take_damage"):
		_target.take_damage(PUNCH_DAMAGE, self)

## Drives the shared character.gd locomotion the player itself uses. Always
## the plain walk clip, never the run one -- even while chasing, since
## CHASE_SPEED is the player's own WALK speed, not its sprint.
func _set_walking(moving: bool) -> void:
	if _character != null and _character.has_method("set_walking"):
		_character.set_walking(moving, false, false)

## Turns this body to face along a horizontal (Y-zeroed) direction, using
## look_at's own math rather than a hand-derived atan2 -- pitch/roll are
## stripped straight back out afterward so a target standing above can't tip
## a pedestrian face-up. Same helper cpu.gd/gunman.gd both carry.
func _face(to_target: Vector3) -> void:
	if to_target.length_squared() < 0.0001:
		return
	look_at(global_position + to_target, Vector3.UP)
	rotation.x = 0.0
	rotation.z = 0.0

## Same duck-typed 4-arg signature every damageable thing in this project
## accepts (see wolf.gd's own comment) -- hitstun/finisher are ignored, a
## civilian has no hitstun-lock mechanic of its own.
##
## This is the whole "attack them and they turn hostile" hook: ANY attacker
## flips it, not just a Player, so a stray gunman's bullet or another NPC's
## punch draws aggro too rather than being silently absorbed.
func take_damage(amount: float, attacker: Node = null, _hitstun_seconds: float = 0.0, _is_finisher: bool = false) -> void:
	if is_dead or amount <= 0.0:
		return
	if attacker != null and is_instance_valid(attacker):
		_become_hostile(attacker)
	health = maxf(0.0, health - amount)
	if health <= 0.0:
		_die(attacker)

## Joins "lockable_npcs" here rather than in _ready() -- see this file's own
## class comment for why a passive pedestrian deliberately isn't a lock-on
## target. add_to_group is idempotent, so re-provoking an already-hostile
## civilian doesn't double-add it.
func _become_hostile(attacker: Node) -> void:
	_is_hostile = true
	_target = attacker
	_is_walking = false
	add_to_group("lockable_npcs")

func _die(attacker: Node = null) -> void:
	if attacker != null and is_instance_valid(attacker) and attacker.has_method("add_xp"):
		attacker.add_xp(XP_REWARD)
	is_dead = true
	velocity = Vector3.ZERO
	_target = null
	# Nothing should keep locking onto a corpse.
	remove_from_group("lockable_npcs")
	# Same real physics ragdoll a dying player gets -- a civilian carries the
	# full player rig, so there's a skeleton to actually collapse.
	if _character != null and _character.has_method("ragdoll"):
		_character.ragdoll()
