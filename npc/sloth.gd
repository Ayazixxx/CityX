extends CharacterBody3D
## Giant hostile beast enemy -- spawned via the dev console's own "/spawn
## sloth" (see ui/dev_console.gd). An ORIGINAL creature (huge, hunched,
## grey, shaggy-looking, glowing red eyes) inspired by the general shape the
## user referenced, not a reproduction of any specific copyrighted
## character's design -- no stitched-patchwork body, no facial marking, no
## companion imagery, just a big bestial silhouette built from the same
## primitive-mesh technique npc/wolf.gd already uses.
##
## Built on wolf.gd's own hostile-AI shape (scan for the nearest player,
## chase in a straight line, attack once in range, duck-typed take_damage/
## health/death) rather than inventing a new pattern -- see wolf.gd's own
## class comment for why straight-line chase (no pathfinding) and a simple
## primitive-mesh body (not the humanoid fused-body pipeline) are both the
## right call here too. The differences are all just bigger/slower/heavier
## numbers (5x the player's own ~1.9m capsule height, a much longer reach,
## a slow overhead slam instead of a quick bite) plus a bipedal/hunched
## silhouette instead of a quadruped one.

const GRAVITY := 9.8
const FLOOR_MAX_ANGLE_DEGREES := 80.0
const NpcSteering = preload("res://npc/npc_steering.gd")

const MAX_HEALTH := 500.0
const DETECTION_RANGE := 22.0
const CHASE_SPEED := 4.5 # below player.gd's own SPRINT_SPEED (7.5) -- a
	# sprinting player can outrun it, matching wolf.gd's own "not
	# inescapable" balance, just scaled for a much bigger, boss-tier threat.
const ATTACK_RANGE := 3.2 # its arms alone are longer than the player is tall
const ATTACK_DAMAGE := 24.0
const ATTACK_COOLDOWN_SECONDS := 1.7
const SLAM_DROP_DISTANCE := 0.55
const SLAM_SECONDS := 0.32

const BODY_COLOR := Color(0.42, 0.41, 0.43)
const DARK_COLOR := Color(0.24, 0.23, 0.25)
const UNDERSIDE_COLOR := Color(0.33, 0.32, 0.34)
const EYE_COLOR := Color(0.95, 0.1, 0.05)

var health := MAX_HEALTH
var is_dead := false

var _target: Node3D = null
## Keeps this thing from wedging itself against a building when you're on
## the far side of it -- see npc/npc_steering.gd.
var _steering := NpcSteering.new()
var _attack_cooldown_remaining := 0.0
var _model: Node3D

@onready var _collision: CollisionShape3D = $CollisionShape3D

func _ready() -> void:
	floor_max_angle = deg_to_rad(FLOOR_MAX_ANGLE_DEGREES)
	collision_layer = 4 # same "hostile NPC" layer wolf.gd/cpu.gd both use
	collision_mask = 1 | 4
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

	_attack_cooldown_remaining = maxf(0.0, _attack_cooldown_remaining - delta)

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
		elif distance > ATTACK_RANGE:
			# Steered around buildings rather than straight at you -- see
			# npc/npc_steering.gd. Faced along where it's actually going, so
			# a 7.7m beast rounding a block turns rather than crabs.
			var direction := _steering.steer(self, to_target / distance, delta)
			_face(direction)
			velocity.x = direction.x * CHASE_SPEED
			velocity.z = direction.z * CHASE_SPEED
		else:
			velocity.x = 0.0
			velocity.z = 0.0
			_face(to_target)
			if _attack_cooldown_remaining <= 0.0:
				_slam_attack()
	else:
		velocity.x = 0.0
		velocity.z = 0.0

	move_and_slide()

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

## A heavy overhead-slam bob (dropping and recovering the MODEL's own local
## Y, not this body's real physics position) -- same "tween the visual
## model, not the physics body" trick wolf.gd's own _bite() already uses,
## just a downward stomp instead of a forward lunge since this attack reads
## as a two-armed slam rather than a bite.
func _slam_attack() -> void:
	_attack_cooldown_remaining = ATTACK_COOLDOWN_SECONDS
	if _target != null and _target.has_method("take_damage"):
		_target.take_damage(ATTACK_DAMAGE, self)
	var tw := create_tween()
	tw.tween_property(_model, "position:y", -SLAM_DROP_DISTANCE, SLAM_SECONDS * 0.35).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_OUT)
	tw.tween_property(_model, "position:y", 0.0, SLAM_SECONDS * 0.65).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN)

## hitstun_seconds/is_finisher exist purely so player.gd's own _punch() can
## call this with the same 4-arg signature it uses on another Player -- see
## wolf.gd's own take_damage() comment for why these are silently no-ops here.
func take_damage(amount: float, attacker: Node = null, _hitstun_seconds: float = 0.0, _is_finisher: bool = false) -> void:
	if is_dead or amount <= 0.0:
		return
	health = maxf(0.0, health - amount)
	if health <= 0.0:
		_die(attacker)

## No ragdoll rig (see this file's own class comment -- primitive-mesh body,
## same as wolf.gd) -- tips the whole model over the same way wolf.gd's own
## _die() does, just a bigger fall distance to match the bigger body.
func _die(_attacker: Node = null) -> void:
	is_dead = true
	velocity = Vector3.ZERO
	_target = null
	var tw := create_tween()
	tw.tween_property(_model, "rotation:z", deg_to_rad(80), 0.7).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tw.parallel().tween_property(_model, "position:y", _model.position.y - 0.6, 0.7)

## Every visual part built directly in code, same reasoning as wolf.gd's own
## _build_model() -- named/commented constants instead of opaque hand-placed
## Transform3D values in the .tscn. A hunched bipedal silhouette (long,
## ground-reaching arms; short, bent-looking legs; a heavy forward-leaning
## spine) reads clearly as a big, dangerous beast at a glance without
## needing actual fur simulation or a rig -- roughly 9.5m tall overall,
## ~5x the player's own ~1.9m collision height (see player.tscn's own
## CapsuleShape3D: radius 0.4, height 1.8).
func _build_model() -> void:
	_model = Node3D.new()
	add_child(_model)

	# Every limb/torso segment below is placed by its own two REAL 3D
	# endpoints (via _add_capsule_between), not by hand-picked Euler
	# rotation degrees -- confirmed directly as the right call after the
	# first attempt (rotation-based, like npc/wolf.gd's own horizontal-body
	# convention) produced a real, visible bug: the legs came out nearly
	# HORIZONTAL instead of vertical (a rotation meant for a quadruped's
	# horizontal torso, copy-pasted onto what needed to be vertical leg
	# columns), and the arms fell well short of the separately-placed fist
	# spheres, leaving a visible gap, because the rotation/length math for a
	# steep-but-not-quite-vertical limb is easy to get subtly wrong by hand.
	# Endpoint-based placement can't have either bug: the capsule always
	# spans exactly the two points given, however steep or shallow.

	# Pelvis -- a rounded mass roughly where the legs plant from.
	const PELVIS_POS := Vector3(0, 2.7, -0.15)
	_add_sphere(_model, PELVIS_POS, 1.05, BODY_COLOR)

	# Spine -- leans forward and up from the pelvis (the "hunch"), NOT a
	# straight vertical torso, which is what actually sells a hunched-over
	# beast silhouette instead of just a big standing humanoid.
	const SHOULDER_POS := Vector3(0, 6.5, -2.05)
	_add_capsule_between(_model, PELVIS_POS, SHOULDER_POS, 1.05, BODY_COLOR)

	# Shoulder hump -- a rounded mass at the top of the spine, reads as a
	# muscular/shaggy hump and hides the spine/shoulder joint. Sized to
	# clear the head (see below) rather than swallow it -- confirmed
	# directly via a real screenshot that the first attempt's bigger hump
	# (radius 1.35, head only 0.3m past its own edge) buried the entire head
	# behind it from any front-facing camera angle.
	_add_sphere(_model, SHOULDER_POS, 1.1, BODY_COLOR)

	# Head -- positioned well clear of the shoulder hump's own front edge
	# (hump front is at SHOULDER_POS.z - 1.1 = -3.15; head center at -3.85,
	# half-depth 0.7, so its own front face reaches -4.55 -- comfortably
	# past the hump with plenty of the head's own volume visibly clear of
	# it, unlike the first attempt).
	const HEAD_POS := Vector3(0, 6.05, -3.85)
	_add_box(_model, HEAD_POS, Vector3(1.35, 1.2, 1.4), BODY_COLOR)
	_add_box(_model, HEAD_POS + Vector3(0, -0.15, -0.85), Vector3(0.85, 0.7, 0.7), UNDERSIDE_COLOR) # muzzle

	# Ears -- small relative to the huge head, tucked close (reads as
	# animalistic, not humanoid).
	_add_ear(HEAD_POS + Vector3(-0.62, 0.68, 0.35))
	_add_ear(HEAD_POS + Vector3(0.62, 0.68, 0.35))

	# Eyes -- glowing red, unshaded, deep-set under a heavy brow so they
	# read as menacing points of light in an otherwise dark grey silhouette.
	_add_eye(HEAD_POS + Vector3(-0.38, 0.15, -0.75))
	_add_eye(HEAD_POS + Vector3(0.38, 0.15, -0.75))

	# Arms -- from each shoulder down to a fist near the ground,
	# knuckle-dragging like a real great ape's (and well forward of the
	# feet), which is what makes the hunch/reach actually believable
	# instead of just a tall humanoid with short arms. The fist sphere is
	# placed at the SAME point the capsule actually ends at, so there's
	# never a gap between them regardless of how steep this limb is.
	const ARM_RADIUS := 0.42
	for side_x in [-1.55, 1.55]:
		var shoulder := SHOULDER_POS + Vector3(side_x, -0.3, 0.15)
		var fist := Vector3(side_x * 1.2, 0.8, -2.7)
		_add_capsule_between(_model, shoulder, fist, ARM_RADIUS, DARK_COLOR)
		_add_sphere(_model, fist, 0.55, DARK_COLOR)

	# Legs -- short, thick, and nearly vertical (a slight forward bend),
	# planting from the pelvis down to a foot roughly under/ahead of it --
	# the whole weight is carried low and forward, not standing tall on
	# straight legs the way a humanoid would.
	const LEG_RADIUS := 0.62
	for side_x in [-0.75, 0.75]:
		var hip := PELVIS_POS + Vector3(side_x * 0.9, -0.3, 0.05)
		var foot := Vector3(side_x, 0.0, 0.55)
		_add_capsule_between(_model, hip, foot, LEG_RADIUS, DARK_COLOR)

	# Collision shape -- a single tall capsule standing UPRIGHT (identity
	# rotation -- CapsuleShape3D's own local Y is already "up" by default),
	# roughly covering the hunched silhouette's own vertical footprint (feet
	# at y=0 up to the head's own top, HEAD_POS.y + half its own height =
	# 6.05 + 0.6 = 6.65), same simplification wolf.gd's own single capsule
	# already uses rather than a compound shape per limb. NOT
	# wolf.gd's own rotation_degrees=(90,0,0) -- that rotates a capsule's
	# long axis to lie flat along Z, which is correct for wolf.gd's own
	# horizontal quadruped body but is flat-out wrong for this creature's
	# vertical bipedal-ish stance: copying it here turned the sloth's own
	# real physics volume into a wide horizontal disc floating around chest
	# height, barely (if at all) touching the ground and covering almost
	# none of the actual standing silhouette -- a real, confirmed bug (this
	# creature was effectively floating/broken-collision from the moment it
	# spawned), found while investigating a separate reported freeze.
	var shape := CapsuleShape3D.new()
	shape.radius = 1.6
	shape.height = 7.7
	_collision.shape = shape
	_collision.position = Vector3(0, shape.height / 2.0, -1.4)

## Places a capsule spanning exactly the two given world-local points --
## used for every limb/spine segment instead of hand-picked Euler rotation
## degrees (see this file's own _build_model comment for why: rotation math
## for a steep-but-not-vertical limb is easy to get subtly wrong by hand,
## and a mismatch between a rotation-placed limb and a separately-positioned
## endpoint like a fist is exactly the kind of gap that's hard to spot
## without a real screenshot). The capsule's own local +Y is its long axis
## by default, so this aligns +Y to the from->to direction directly rather
## than guessing what Euler angles would produce that same alignment.
func _add_capsule_between(parent: Node3D, from: Vector3, to: Vector3, radius: float, color: Color) -> MeshInstance3D:
	var diff := to - from
	var length := diff.length()
	var mesh := CapsuleMesh.new()
	mesh.radius = radius
	mesh.height = maxf(length, radius * 2.0 + 0.01) # never shorter than its own caps
	var mat := StandardMaterial3D.new()
	mat.albedo_color = color
	mesh.material = mat
	var inst := MeshInstance3D.new()
	inst.mesh = mesh
	inst.position = (from + to) / 2.0

	var y_axis := diff.normalized()
	var reference := Vector3.RIGHT if absf(y_axis.dot(Vector3.UP)) > 0.99 else Vector3.UP
	var x_axis := reference.cross(y_axis).normalized()
	var z_axis := x_axis.cross(y_axis).normalized()
	inst.transform.basis = Basis(x_axis, y_axis, z_axis)

	parent.add_child(inst)
	return inst

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

func _add_sphere(parent: Node3D, pos: Vector3, radius: float, color: Color) -> MeshInstance3D:
	var mesh := SphereMesh.new()
	mesh.radius = radius
	mesh.height = radius * 2.0
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
	mesh.size = Vector3(0.32, 0.42, 0.18)
	var mat := StandardMaterial3D.new()
	mat.albedo_color = DARK_COLOR
	mesh.material = mat
	var inst := MeshInstance3D.new()
	inst.mesh = mesh
	inst.position = pos
	_model.add_child(inst)

func _add_eye(pos: Vector3) -> void:
	var mesh := SphereMesh.new()
	mesh.radius = 0.11
	mesh.height = 0.22
	var mat := StandardMaterial3D.new()
	mat.albedo_color = EYE_COLOR
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mesh.material = mat
	var inst := MeshInstance3D.new()
	inst.mesh = mesh
	inst.position = pos
	_model.add_child(inst)
