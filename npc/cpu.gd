extends CharacterBody3D
## Stationary sparring-dummy NPC for testing combat: same playmodel as the
## player (see CharacterScene), same take_damage()/health/ragdoll shape
## player.gd's own _punch() already duck-types against, so the player's
## existing punch code works against it completely unmodified. Sits idle and
## takes no action until it's actually hit -- see take_damage's own _target/
## _is_hostile handling -- at which point it permanently turns hostile
## toward whoever hit it: chases them down and throws jabs back using the
## same character.gd punch()/set_walking() calls the player itself drives.
## No pathfinding (NavigationAgent3D etc.) -- straight-line chase only, fine
## for a single open sparring target, not meant to navigate real city
## obstacles yet.

const GRAVITY := 9.8
const FLOOR_MAX_ANGLE_DEGREES := 80.0
const CharacterScene := preload("res://character/character.tscn")

const NpcSteering = preload("res://npc/npc_steering.gd")

const MAX_HEALTH := 100.0

# Matches the player's own walk SPEED (see player.gd) -- lets a player who's
# just walking, not sprinting, actually outrun this once hostile, rather
# than an unescapable chase.
const CHASE_SPEED := 4.0
# Same numbers as player.gd's own PUNCH_RANGE/PUNCH_DAMAGE/
# PUNCH_COOLDOWN_SECONDS -- no raycast needed here (unlike the player's own
# _punch, which has to aim a ray from an arbitrary camera direction), since
# a punch only ever fires once _target is already confirmed within
# PUNCH_RANGE by the chase logic below.
const PUNCH_RANGE := 2.5
const PUNCH_DAMAGE := 10.0
const PUNCH_COOLDOWN_SECONDS := 0.35

var health := MAX_HEALTH
var is_dead := false

var _character: Node3D
# Set the instant a hit with a valid attacker lands (see take_damage) and
# never cleared back to false -- once this dummy has been hit, it stays
# hostile for good, it doesn't calm back down. _target re-latches onto
# whoever hit it MOST RECENTLY, so a second attacker landing a hit steals
# aggro rather than the dummy staying fixated on the first.
var _is_hostile := false
var _target: Node3D = null
var _punch_cooldown_remaining := 0.0
## Steers a chase around whatever's in the way -- see npc/npc_steering.gd.
var _steering := NpcSteering.new()

func _ready() -> void:
	floor_max_angle = deg_to_rad(FLOOR_MAX_ANGLE_DEGREES)
	# The collider comes from cpu.tscn, so it already exists here -- unlike
	# the NPCs that build theirs in code and have to configure after that.
	_steering.configure_from(self)
	_character = CharacterScene.instantiate()
	_character.set_paintable(false)
	add_child(_character)
	# Same relative flip main.gd applies to the player's own Character child
	# -- the model's local +Z ("face") needs to point the same way this
	# body's own forward (local -Z) does, or it renders back-first.
	_character.rotation = Vector3(0, PI, 0)

func _physics_process(delta: float) -> void:
	if is_dead:
		return
	if not is_on_floor():
		velocity.y -= GRAVITY * delta
	else:
		velocity.y = 0.0

	_punch_cooldown_remaining = maxf(0.0, _punch_cooldown_remaining - delta)

	# A target can go invalid (freed) or die without a fresh take_damage call
	# ever telling this dummy -- re-checked fresh every frame rather than
	# trusting whatever _target pointed at when it was first set.
	var target_alive := _is_hostile and _target != null and is_instance_valid(_target) and not bool(_target.get("is_dead"))
	if target_alive:
		var to_target: Vector3 = _target.global_position - global_position
		to_target.y = 0.0
		var distance := to_target.length()
		if distance > PUNCH_RANGE:
			# Steered around whatever's in the way rather than straight at
			# the target -- see npc/npc_steering.gd. Faced along the
			# direction actually travelled so it looks where it's going.
			var direction := _steering.steer(self, to_target / distance, delta)
			_face(direction)
			velocity.x = direction.x * CHASE_SPEED
			velocity.z = direction.z * CHASE_SPEED
			if _character.has_method("set_walking"):
				_character.set_walking(true, false, false)
		else:
			velocity.x = 0.0
			velocity.z = 0.0
			_face(to_target)
			if _character.has_method("set_walking"):
				_character.set_walking(false, false, false)
			if _punch_cooldown_remaining <= 0.0:
				_throw_punch()
	else:
		velocity.x = 0.0
		velocity.z = 0.0

	move_and_slide()

## Turns this body to face along a horizontal (Y-zeroed) direction, using
## look_at's own math rather than hand-deriving an atan2 formula -- pitch/
## roll are stripped straight back out afterward so a target standing on a
## rooftop above this dummy can't tip it face-up.
func _face(to_target: Vector3) -> void:
	if to_target.length_squared() < 0.0001:
		return
	look_at(global_position + to_target, Vector3.UP)
	rotation.x = 0.0
	rotation.z = 0.0

func _throw_punch() -> void:
	_punch_cooldown_remaining = PUNCH_COOLDOWN_SECONDS
	if _character.has_method("punch"):
		_character.punch(false)
	if _target != null and _target.has_method("take_damage"):
		_target.take_damage(PUNCH_DAMAGE, self)

## attacker is duck-typed (has_method checks, same as player.gd's own
## take_damage) rather than typed Player specifically, so anything else that
## ever calls take_damage(amount, attacker) against this dummy -- another
## npc, say -- would correctly draw aggro too, not just the Player.
##
## hitstun_seconds/is_finisher exist purely so player.gd's own _punch() can
## call this with the same 4-arg signature it uses on another Player -- this
## sparring dummy has no hitstun-lock/ragdoll-finisher mechanic of its own,
## so both are silently accepted and ignored, same as wolf.gd/sloth.gd.
func take_damage(amount: float, attacker: Node = null, _hitstun_seconds: float = 0.0, _is_finisher: bool = false) -> void:
	if is_dead or amount <= 0.0:
		return
	if attacker != null and is_instance_valid(attacker):
		_is_hostile = true
		_target = attacker
	health = maxf(0.0, health - amount)
	if health <= 0.0:
		_die()

func _die() -> void:
	is_dead = true
	velocity = Vector3.ZERO
	if _character != null and _character.has_method("ragdoll"):
		_character.ragdoll()
