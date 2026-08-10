extends CharacterBody3D
## Hostile grey wolf enemy -- spawned via the dev console's own "/spawn
## wolf" (see ui/dev_console.gd). A simple quadruped built from primitive
## meshes in code (see _build_model), NOT the humanoid fused-body pipeline
## character.gd/fused_body.gd use -- that whole system (SDF marching-
## tetrahedra, skeletal skinning) is built specifically around a biped rig
## and has no way to represent a four-legged animal, so this is its own
## much simpler, self-contained body instead.
##
## Always hostile, unlike npc/cpu.gd's own sparring dummy (which waits to be
## hit first) -- scans for the nearest player within DETECTION_RANGE every
## frame and, once it has a target, chases and bites it using the same
## straight-line-chase-then-attack shape cpu.gd's own punch AI already
## established, just with a bite instead of a punch and no shared humanoid
## Character body to animate. take_damage()/health match the same duck-typed
## interface every other damageable thing in this project already uses
## (player.gd's own melee/pistol raycasts, so a player can fight it back
## completely unmodified).
##
## No pathfinding (NavigationAgent3D etc.) -- straight-line chase only, the
## same simplification cpu.gd's own class comment already accepts.

const GRAVITY := 9.8
const FLOOR_MAX_ANGLE_DEGREES := 80.0
const NpcSteering = preload("res://npc/npc_steering.gd")

const MAX_HEALTH := 60.0
# The first (and so far only) enemy given a real level -- see _die()'s own
# comment on why XP_REWARD is a plain hand-set number rather than a shared
# formula: every NPC script in this project is independently copy-pasted
# (see take_damage()'s own class-comment precedent), no shared enemy base
# class exists yet to hang a level->XP formula off of, so each future
# leveled enemy sets its own ENEMY_LEVEL/XP_REWARD pair by hand the same way
# until one does.
const ENEMY_LEVEL := 1
# Half of player.gd's own XP_PER_LEVEL_BASE (100) at level 1 -- two wolf
# kills level a fresh character up once, a reasonable early-game pace for
# the first, weakest enemy in the game. Not balance-tested, easy to retune.
const XP_REWARD := 50.0
const DETECTION_RANGE := 14.0
const CHASE_SPEED := 5.5
const BITE_RANGE := 1.7
const BITE_DAMAGE := 8.0
const BITE_COOLDOWN_SECONDS := 0.9
const LUNGE_DISTANCE := 0.35
const LUNGE_SECONDS := 0.12

const BODY_COLOR := Color(0.45, 0.45, 0.48)
const DARK_COLOR := Color(0.26, 0.26, 0.29)
const TOOTH_COLOR := Color(0.96, 0.96, 0.92)
const EYE_COLOR := Color(0.9, 0.08, 0.05)

var health := MAX_HEALTH
var is_dead := false

var _target: Node3D = null
## Keeps this wolf from running face-first into a building when you're on
## the other side of it -- see npc/npc_steering.gd.
var _steering := NpcSteering.new()
var _bite_cooldown_remaining := 0.0
var _model: Node3D

@onready var _collision: CollisionShape3D = $CollisionShape3D

func _ready() -> void:
	floor_max_angle = deg_to_rad(FLOOR_MAX_ANGLE_DEGREES)
	collision_layer = 4 # same "hostile NPC" layer cpu.gd's own body uses
	collision_mask = 1 | 4 # world geometry + other bodies (matches cpu.gd)
	# Lets player.gd's own lock-on camera (press C) find this as a valid
	# target alongside the "players" group -- see its own _find_lock_on_candidates.
	add_to_group("lockable_npcs")
	_build_model()
	# After _build_model: it's what creates the collision shape this sizes
	# its probe from.
	_steering.configure_from(self)

func _physics_process(delta: float) -> void:
	if is_dead:
		return
	if not is_on_floor():
		velocity.y -= GRAVITY * delta
	else:
		velocity.y = 0.0

	_bite_cooldown_remaining = maxf(0.0, _bite_cooldown_remaining - delta)

	# Re-acquired every frame rather than trusting a stale pointer -- the
	# same "a target can go invalid/die without this ever being told"
	# reasoning cpu.gd's own take_damage-driven _target already documents,
	# just re-scanning proactively here since this wolf never waits for a
	# hit to pick a target in the first place.
	if _target == null or not is_instance_valid(_target) or bool(_target.get("is_dead")):
		_target = _find_nearest_player()

	if _target != null:
		var to_target: Vector3 = _target.global_position - global_position
		to_target.y = 0.0
		var distance := to_target.length()
		if distance > DETECTION_RANGE:
			_target = null
			velocity.x = 0.0
			velocity.z = 0.0
		elif distance > BITE_RANGE:
			# Steered, not straight at the player -- see npc/npc_steering.gd.
			# Faced along the direction it's actually travelling so it looks
			# where it's going while rounding a building, rather than
			# crabbing sideways with its head fixed on you.
			var direction := _steering.steer(self, to_target / distance, delta)
			_face(direction)
			velocity.x = direction.x * CHASE_SPEED
			velocity.z = direction.z * CHASE_SPEED
		else:
			velocity.x = 0.0
			velocity.z = 0.0
			_face(to_target)
			if _bite_cooldown_remaining <= 0.0:
				_bite()
	else:
		velocity.x = 0.0
		velocity.z = 0.0

	move_and_slide()

## Duck-typed the same way cpu.gd's own take_damage() is: has_method checks
## (see player.gd's own melee/pistol code), not a hard Player type, so
## anything with the right shape counts as a valid target -- including
## another wolf, if that's ever spawned, though nothing pits them against
## each other today.
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

func _face(to_target: Vector3) -> void:
	if to_target.length_squared() < 0.0001:
		return
	look_at(global_position + to_target, Vector3.UP)
	rotation.x = 0.0
	rotation.z = 0.0

## A quick forward-and-back lunge (tweening the MODEL's own local position,
## not this body's real physics position) gives the bite a visible snap
## without needing any skeleton/animation system at all.
func _bite() -> void:
	_bite_cooldown_remaining = BITE_COOLDOWN_SECONDS
	if _target != null and _target.has_method("take_damage"):
		_target.take_damage(BITE_DAMAGE, self)
	var tw := create_tween()
	tw.tween_property(_model, "position:z", -LUNGE_DISTANCE, LUNGE_SECONDS * 0.4).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_OUT)
	tw.tween_property(_model, "position:z", 0.0, LUNGE_SECONDS * 0.6).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN)

## hitstun_seconds/is_finisher exist purely so player.gd's own _punch() can
## call this with the same 4-arg signature it uses on another Player -- a
## wolf has no hitstun-lock/ragdoll-finisher mechanic of its own (no
## skeletal rig to ragdoll, see this file's own class comment), so both are
## silently accepted and ignored, the same no-op duck-typing pattern
## character.gd's own hit_reaction()/block_impact() already use for unarmed
## targets.
func take_damage(amount: float, attacker: Node = null, _hitstun_seconds: float = 0.0, _is_finisher: bool = false) -> void:
	if is_dead or amount <= 0.0:
		return
	health = maxf(0.0, health - amount)
	if health <= 0.0:
		_die(attacker)

## No ragdoll rig to collapse (see this file's own class comment) -- tips
## the whole model over onto its side instead, a much simpler "dead" pose
## that still reads clearly without needing any physics bodies/joints of
## its own the way character.gd's own ragdoll() does.
##
## Grants XP_REWARD to whichever attacker actually landed the kill (a real
## reported ask: "the way you level up is by killing enemies, higher level
## enemies give you more XP") -- duck-typed via has_method("add_xp") rather
## than checking is_in_group("players") or similar, same "if it can, it
## gets it" convention take_damage()'s own attacker param already uses
## project-wide. Silently does nothing for a null attacker (e.g. this
## dying to something other than a player's own hit) or one that doesn't
## carry add_xp() at all (an NPC accidentally hitting another NPC has no
## progression of its own to grant).
func _die(attacker: Node = null) -> void:
	if attacker != null and attacker.has_method("add_xp"):
		attacker.add_xp(XP_REWARD)
	is_dead = true
	velocity = Vector3.ZERO
	_target = null
	var tw := create_tween()
	tw.tween_property(_model, "rotation:z", deg_to_rad(85), 0.4).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tw.parallel().tween_property(_model, "position:y", _model.position.y - 0.15, 0.4)

## Every visual part, built directly in code rather than hand-placed in the
## .tscn -- matches how this project already builds weapons/other props
## procedurally instead of hand-tuning a long list of exact Transform3D
## values, and keeps every measurement as a named, commented constant
## instead of opaque numbers in scene-file text.
func _build_model() -> void:
	_model = Node3D.new()
	add_child(_model)

	const BODY_LEN := 0.95
	const BODY_RADIUS := 0.22
	const BODY_Y := 0.42
	_add_capsule(_model, Vector3(0, BODY_Y, 0), Vector3(90, 0, 0), BODY_RADIUS, BODY_LEN, BODY_COLOR)

	# Head + snout, both offset forward (+Z, this body's own "front" per
	# _face()'s own look_at, which points -Z at the target -- wait, forward
	# is -Z for a look_at-oriented CharacterBody3D, so the head sits at -Z).
	const HEAD_Z := -0.62
	const HEAD_Y := 0.5
	_add_box(_model, Vector3(0, HEAD_Y, HEAD_Z), Vector3(0.28, 0.26, 0.34), BODY_COLOR)
	const SNOUT_Z := HEAD_Z - 0.28
	_add_box(_model, Vector3(0, HEAD_Y - 0.04, SNOUT_Z), Vector3(0.16, 0.16, 0.26), DARK_COLOR)

	# Ears -- two small pyramids on top of the head.
	_add_ear(Vector3(-0.09, HEAD_Y + 0.16, HEAD_Z + 0.05))
	_add_ear(Vector3(0.09, HEAD_Y + 0.16, HEAD_Z + 0.05))

	# Sharp teeth -- small pale cones just under the snout's front tip,
	# pointing down, reading as a row of fangs.
	var teeth_z := SNOUT_Z - 0.1
	for i in range(4):
		var tx: float = lerpf(-0.06, 0.06, float(i) / 3.0)
		_add_tooth(Vector3(tx, HEAD_Y - 0.11, teeth_z))

	# Eyes -- small glowing red spheres, unshaded so they read as menacing
	# regardless of scene lighting.
	_add_eye(Vector3(-0.09, HEAD_Y + 0.04, HEAD_Z - 0.05))
	_add_eye(Vector3(0.09, HEAD_Y + 0.04, HEAD_Z - 0.05))

	# Legs -- 4 thin capsules at each corner.
	const LEG_RADIUS := 0.06
	const LEG_LEN := 0.4
	const LEG_Y := 0.22
	const LEG_X := 0.16
	for side_x in [-LEG_X, LEG_X]:
		for leg_z in [-0.3, 0.32]:
			_add_capsule(_model, Vector3(side_x, LEG_Y, leg_z), Vector3(0, 0, 0), LEG_RADIUS, LEG_LEN, DARK_COLOR)

	# Tail -- a thin capsule angled up and back from the rear.
	_add_capsule(_model, Vector3(0, 0.55, 0.58), Vector3(-35, 0, 0), 0.06, 0.4, BODY_COLOR)

	# Collision shape, sized to roughly cover the body+legs footprint.
	var shape := CapsuleShape3D.new()
	shape.radius = BODY_RADIUS + 0.06
	shape.height = BODY_LEN + 0.5
	_collision.shape = shape
	_collision.rotation_degrees = Vector3(90, 0, 0)
	_collision.position = Vector3(0, BODY_Y, 0)

func _add_capsule(parent: Node3D, pos: Vector3, rot_degrees: Vector3, radius: float, height: float, color: Color) -> MeshInstance3D:
	var mesh := CapsuleMesh.new()
	mesh.radius = radius
	mesh.height = height
	var mat := StandardMaterial3D.new()
	mat.albedo_color = color
	mesh.material = mat
	var inst := MeshInstance3D.new()
	inst.mesh = mesh
	inst.position = pos
	inst.rotation_degrees = rot_degrees
	parent.add_child(inst)
	return inst

func _add_box(parent: Node3D, pos: Vector3, size: Vector3, color: Color) -> MeshInstance3D:
	var mesh := BoxMesh.new()
	mesh.size = size
	var mat := StandardMaterial3D.new()
	mat.albedo_color = color
	mesh.material = mat
	var inst := MeshInstance3D.new()
	inst.mesh = mesh
	inst.position = pos
	parent.add_child(inst)
	return inst

func _add_ear(pos: Vector3) -> void:
	var mesh := PrismMesh.new()
	mesh.size = Vector3(0.09, 0.14, 0.05)
	var mat := StandardMaterial3D.new()
	mat.albedo_color = DARK_COLOR
	mesh.material = mat
	var inst := MeshInstance3D.new()
	inst.mesh = mesh
	inst.position = pos
	_model.add_child(inst)

func _add_tooth(pos: Vector3) -> void:
	var mesh := PrismMesh.new()
	mesh.size = Vector3(0.025, 0.05, 0.025)
	var mat := StandardMaterial3D.new()
	mat.albedo_color = TOOTH_COLOR
	mesh.material = mat
	var inst := MeshInstance3D.new()
	inst.mesh = mesh
	inst.position = pos
	inst.rotation_degrees = Vector3(180, 0, 0) # point downward, tip first
	_model.add_child(inst)

func _add_eye(pos: Vector3) -> void:
	var mesh := SphereMesh.new()
	mesh.radius = 0.035
	mesh.height = 0.07
	var mat := StandardMaterial3D.new()
	mat.albedo_color = EYE_COLOR
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mesh.material = mat
	var inst := MeshInstance3D.new()
	inst.mesh = mesh
	inst.position = pos
	_model.add_child(inst)
