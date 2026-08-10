extends CharacterBody3D
## Hostile armed human enemy -- spawned via the dev console's own "/spawn
## gunman". Uses the SAME playable body every player character uses (see
## CharacterScene), holds a REAL weapons/pistol.tscn on its own hand bone,
## and shoots at you from a distance -- deliberately imperfectly (see
## ACCURACY).
##
## The first enemy in this project that fights at range rather than by
## walking into you. That changes the movement shape: instead of npc/wolf.gd
## and npc/cpu.gd's own "close the distance, then attack", this one keeps a
## PREFERRED firing band (see PREFERRED_RANGE_MIN/MAX) -- it advances when
## you're too far to hit, backs off when you crowd it, and stands and
## shoots in between.
##
## The gun is genuinely the player's own pistol, not a fake: this calls
## weapons/pistol.gd's own _equip(self)/fire(), so the muzzle flash, spark
## burst, gunshot sound, hitscan, blood splatter and the character.gd gun
## moveset (set_wielding_gun -> the pistol locomotion/aim/shoot clips) all
## come along for free and stay identical to what the player experiences.
## That works because pistol.gd only ever duck-types its wielder -- it looks
## up a "Character" child with get_hand_node() and calls optional methods
## via has_method -- so a CharacterBody3D that isn't the Player satisfies it
## completely.
##
## Shots respect line of sight (see _has_line_of_fire) so it won't shoot
## through the city's own walls, but there's still no pathfinding -- it
## moves in straight lines, the same simplification cpu.gd/wolf.gd already
## accept.

const GRAVITY := 9.8
const FLOOR_MAX_ANGLE_DEGREES := 80.0
const NpcSteering = preload("res://npc/npc_steering.gd")

const CharacterScene := preload("res://character/character.tscn")
const PistolScene := preload("res://weapons/pistol.tscn")

const MAX_HEALTH := 100.0
# Sets its own ENEMY_LEVEL/XP_REWARD pair by hand, exactly as wolf.gd's own
# comment prescribes until some shared enemy base class exists to hang a
# level->XP formula off of. Rated above the wolf (level 1, 50 XP): it hits
# harder, hits from range, and takes ~5 pistol shots to drop.
const ENEMY_LEVEL := 3
const XP_REWARD := 120.0

const DETECTION_RANGE := 26.0
const MOVE_SPEED := 3.6
# The band it tries to fight from. Closer than MIN and it retreats (a
# pistol-armed enemy that lets you walk into melee is free XP); further than
# MAX and it advances. Sitting inside the band, it holds still and shoots.
const PREFERRED_RANGE_MIN := 6.0
const PREFERRED_RANGE_MAX := 14.0

const SHOT_INTERVAL_SECONDS := 1.15
# +/- jitter on every interval, so two gunmen fighting you never lock into
# a machine-gun-like shared rhythm.
const SHOT_INTERVAL_JITTER := 0.35
# A beat of "raising the gun" before the first shot of an engagement, so
# being spotted doesn't mean being instantly hit.
const AIM_DELAY_SECONDS := 0.55

# Fraction of shots that are actually aimed at you. The rest are deliberate
# misses (see MISS_SPREAD_DEGREES) -- the explicit ask for this enemy, and
# also what makes it fightable at all: a hitscan enemy that never misses is
# unavoidable damage, since there's no projectile to dodge.
const ACCURACY := 0.55
# How far wide a deliberate miss is thrown. Big enough to visibly go past
# you (and spark off a wall behind you), small enough to still read as
# being shot AT rather than random gunfire.
const MISS_SPREAD_DEGREES := 12.0
# Aim height above the target's own origin (their feet) -- centre mass.
const AIM_HEIGHT := 1.2
# Where shots leave from, on THIS body. Roughly the held gun's own height;
# firing from the origin would put every shot through the floor.
const MUZZLE_HEIGHT := 1.35

var health := MAX_HEALTH
var is_dead := false

var _character: Node3D
var _pistol: Node3D = null
var _target: Node3D = null
## Steers the advance around buildings -- see npc/npc_steering.gd.
var _steering := NpcSteering.new()
var _shot_cooldown_remaining := AIM_DELAY_SECONDS
var _was_engaging := false

func _ready() -> void:
	floor_max_angle = deg_to_rad(FLOOR_MAX_ANGLE_DEGREES)
	# The collider comes from gunman.tscn, so it already exists here.
	_steering.configure_from(self)
	collision_layer = 4 # same "hostile NPC" layer cpu.gd/wolf.gd's own bodies use
	collision_mask = 1 | 4
	# Lets player.gd's own lock-on camera (press C) treat this as a valid
	# target, same as every other hostile NPC.
	add_to_group("lockable_npcs")

	_character = CharacterScene.instantiate()
	_character.set_paintable(false)
	add_child(_character)
	# Same flip main.gd's own _place_character_on applies to the player's own
	# Character child -- the model's local +Z ("face") has to point where
	# this body's own forward (-Z) does, or it walks backwards.
	_character.rotation = Vector3(0, PI, 0)

	# Deferred by one frame rather than run inline: pistol.gd's own _equip()
	# reparents itself onto character.gd's get_hand_node(), and the hand's
	# BoneAttachment3D only exists once the Character's own _ready() has
	# finished building its skeleton.
	call_deferred("_equip_pistol")

func _equip_pistol() -> void:
	if is_dead or _character == null or not is_instance_valid(_character):
		return
	var pistol := PistolScene.instantiate()
	# _equip() does get_parent().remove_child(self), so it needs a parent to
	# be removed FROM before it can reparent onto the hand.
	add_child(pistol)
	pistol.call("_equip", self)
	_pistol = pistol
	# An enemy that permanently runs dry mid-fight would just stand there,
	# which reads as broken rather than as a lull -- see pistol.gd's own
	# restock_reserve().
	if _pistol.has_method("restock_reserve"):
		_pistol.call("restock_reserve")

func _physics_process(delta: float) -> void:
	if is_dead:
		return
	if not is_on_floor():
		velocity.y -= GRAVITY * delta
	else:
		velocity.y = 0.0

	_shot_cooldown_remaining = maxf(0.0, _shot_cooldown_remaining - delta)

	# Re-acquired every frame rather than trusting a stale pointer, same
	# reasoning wolf.gd's own target handling documents.
	if _target == null or not is_instance_valid(_target) or bool(_target.get("is_dead")):
		_target = _find_nearest_player()
		if _target == null:
			_set_engaging(false)
			velocity.x = 0.0
			velocity.z = 0.0
			_set_moving(false)
			move_and_slide()
			return

	var to_target: Vector3 = _target.global_position - global_position
	to_target.y = 0.0
	var distance := to_target.length()

	if distance > DETECTION_RANGE:
		_target = null
		_set_engaging(false)
		velocity.x = 0.0
		velocity.z = 0.0
		_set_moving(false)
		move_and_slide()
		return

	# Facing stays on the TARGET, not on the direction of travel -- unlike
	# every melee NPC here. This one is a shooter: it has to keep the player
	# covered while it repositions, and its backpedal below already relies
	# on that (it walks backwards rather than turning tail).
	_face(to_target)
	var direction := to_target / maxf(distance, 0.0001)

	# Advance / retreat / hold, per the firing band this enemy fights from.
	if distance > PREFERRED_RANGE_MAX:
		# Only the ADVANCE is steered (see npc/npc_steering.gd) -- closing
		# the distance is the leg that has a building to get around.
		# Backing off is a short shuffle inside a band it's already in.
		var approach := _steering.steer(self, direction, delta)
		velocity.x = approach.x * MOVE_SPEED
		velocity.z = approach.z * MOVE_SPEED
		_set_moving(true)
	elif distance < PREFERRED_RANGE_MIN:
		# Backpedal -- still facing the target (see _face above), so the
		# model walks backwards rather than turning tail.
		velocity.x = -direction.x * MOVE_SPEED
		velocity.z = -direction.z * MOVE_SPEED
		_set_moving(true)
	else:
		velocity.x = 0.0
		velocity.z = 0.0
		_set_moving(false)

	# Fires from anywhere inside detection range as long as it actually has
	# a clear shot -- not only from inside the preferred band, so backing
	# away or closing in doesn't grant free immunity.
	var can_shoot := _has_line_of_fire()
	_set_engaging(can_shoot)
	if can_shoot and _shot_cooldown_remaining <= 0.0:
		_fire_at_target()

	move_and_slide()

## Duck-typed the same way every other NPC in this project finds a target.
func _find_nearest_player() -> Node3D:
	var nearest: Node3D = null
	var nearest_dist := DETECTION_RANGE
	for node in get_tree().get_nodes_in_group("players"):
		if not is_instance_valid(node) or bool(node.get("is_dead")):
			continue
		var dist: float = global_position.distance_to(node.global_position)
		if dist < nearest_dist:
			nearest_dist = dist
			nearest = node
	return nearest

## Whether there's actually a clear shot, rather than a wall in the way --
## the city is full of buildings, and an enemy sniping you through a
## concrete facade reads as an outright bug. Only world geometry (layer 1)
## blocks: other NPC bodies deliberately don't, so a crowd doesn't make
## everyone hold fire.
func _has_line_of_fire() -> bool:
	if _target == null or not is_instance_valid(_target):
		return false
	var from := _muzzle_origin()
	var to: Vector3 = _target.global_position + Vector3(0, AIM_HEIGHT, 0)
	var query := PhysicsRayQueryParameters3D.create(from, to)
	query.collision_mask = 1
	query.exclude = [self]
	return get_world_3d().direct_space_state.intersect_ray(query).is_empty()

func _muzzle_origin() -> Vector3:
	return global_position + Vector3(0, MUZZLE_HEIGHT, 0)

## One shot, aimed at centre mass -- or deliberately thrown wide, per
## ACCURACY. The miss is applied as a real angular offset to the FIRED
## direction rather than by suppressing damage on a hit, so a miss behaves
## like a miss all the way through: the bullet actually travels somewhere
## else, sparks off whatever it does hit, and can't be told apart from a
## genuine near-miss.
func _fire_at_target() -> void:
	_shot_cooldown_remaining = SHOT_INTERVAL_SECONDS + randf_range(-SHOT_INTERVAL_JITTER, SHOT_INTERVAL_JITTER)
	if _pistol == null or not is_instance_valid(_pistol) or not _pistol.has_method("fire"):
		return

	var origin := _muzzle_origin()
	var aim_point: Vector3 = _target.global_position + Vector3(0, AIM_HEIGHT, 0)
	var direction := (aim_point - origin).normalized()
	if randf() > ACCURACY:
		direction = _throw_wide(direction)

	# aiming=true so the pistol applies its own TIGHT spread cone on top --
	# this enemy's own hit/miss decision above is what should govern
	# accuracy, not a second random cone piled on it.
	var fired: bool = _pistol.call("fire", origin, direction, true)
	if fired:
		# The same recoil-snap clip player.gd's own _try_fire_gun() plays.
		if _character.has_method("shoot"):
			_character.shoot()
	elif _pistol.has_method("reload"):
		# Dry or empty: reload, and pause about as long as that takes so it
		# visibly stops shooting for a beat instead of dry-firing every frame.
		_pistol.call("reload")
		if _pistol.has_method("restock_reserve"):
			_pistol.call("restock_reserve")
		_shot_cooldown_remaining = maxf(_shot_cooldown_remaining, 1.6)

## Rotates direction by a random angle in a cone of MISS_SPREAD_DEGREES,
## with a guaranteed MINIMUM deviation -- a "miss" that randomly came out at
## 0.2 degrees would still hit, which is exactly the bug this avoids. Same
## two-perpendicular-axes construction weapons/pistol.gd's own
## _apply_spread() uses.
func _throw_wide(direction: Vector3) -> Vector3:
	var helper := Vector3.UP if absf(direction.dot(Vector3.UP)) < 0.95 else Vector3.RIGHT
	var right := direction.cross(helper).normalized()
	var up := right.cross(direction).normalized()
	var min_rad := deg_to_rad(MISS_SPREAD_DEGREES * 0.45)
	var max_rad := deg_to_rad(MISS_SPREAD_DEGREES)
	var angle := randf_range(min_rad, max_rad)
	# A random direction around the aim axis, so misses go left/right/high/
	# low rather than always to one side.
	var roll := randf_range(0.0, TAU)
	var axis := (right * cos(roll) + up * sin(roll)).normalized()
	return direction.rotated(axis, angle).normalized()

## Drives the shared character.gd locomotion the player itself uses -- with
## a gun equipped, set_walking() routes to the pistol moveset (see
## set_wielding_gun).
func _set_moving(moving: bool) -> void:
	if _character != null and _character.has_method("set_walking"):
		_character.set_walking(moving, false, false)

## Raises/lowers the gun. Edge-triggered rather than set every frame,
## matching how player.gd only tells the character about its own aim state
## when that state actually changes.
func _set_engaging(engaging: bool) -> void:
	if engaging == _was_engaging:
		return
	_was_engaging = engaging
	if _character != null and _character.has_method("set_aiming"):
		_character.set_aiming(engaging)

func _face(to_target: Vector3) -> void:
	if to_target.length_squared() < 0.0001:
		return
	look_at(global_position + to_target, Vector3.UP)
	rotation.x = 0.0
	rotation.z = 0.0

## Same duck-typed 4-arg signature every damageable thing in this project
## accepts (see wolf.gd's own comment) -- hitstun/finisher are ignored, this
## enemy has no hitstun-lock mechanic of its own.
func take_damage(amount: float, attacker: Node = null, _hitstun_seconds: float = 0.0, _is_finisher: bool = false) -> void:
	if is_dead or amount <= 0.0:
		return
	# Being shot at from out of nowhere re-aggros onto whoever did it, even
	# if they're behind this gunman or outside its own detection sweep.
	if attacker != null and is_instance_valid(attacker) and attacker.is_in_group("players"):
		_target = attacker
	health = maxf(0.0, health - amount)
	if health <= 0.0:
		_die(attacker)

## Unlike wolf.gd (no skeleton to collapse), this one has the full player
## rig, so it gets the same REAL physics ragdoll a dying player does.
func _die(attacker: Node = null) -> void:
	if attacker != null and attacker.has_method("add_xp"):
		attacker.add_xp(XP_REWARD)
	is_dead = true
	velocity = Vector3.ZERO
	_target = null
	# The gun leaves with them -- it's parented to the hand bone, which the
	# ragdoll's own rig doesn't carry over.
	if _pistol != null and is_instance_valid(_pistol):
		_pistol.queue_free()
		_pistol = null
	if _character != null and _character.has_method("ragdoll"):
		_character.ragdoll()
