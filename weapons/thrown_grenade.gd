extends RigidBody3D
## A grenade in flight, with a lit fuse. Spawned fresh on every throw (see
## player.gd's own _try_throw_grenade/_spawn_thrown_grenade), exactly the
## way weapons/thrown_bottle.gd is -- see that file's own class comment for
## why a thrown projectile is a separate node from the equipped prop rather
## than the same one reparented.
##
## The one real behavioural difference from the thrown bottle: this does NOT
## detonate on impact. It bounces, rolls, and blows up when its FUSE runs
## out (see FUSE_SECONDS), which is what makes a grenade a grenade -- you
## can bank one around a corner, and a badly-timed throw can be thrown back
## at you by the clock rather than by an enemy.
##
## Networking mirrors thrown_bottle.gd exactly: every peer spawns its own
## local copy from the same broadcast transform/velocity and simulates it
## independently, and is_authoritative marks the throwing peer's own
## original. Only the authoritative copy applies blast damage; every replica
## still plays the full explosion visual locally. Without that split, each
## peer's own replica would independently damage everyone in range, applying
## one grenade's damage as many times as there are players.

const Explosion = preload("res://weapons/explosion.gd")

const FUSE_SECONDS := 2.4
const BLAST_RADIUS := 6.0
const BLAST_DAMAGE := 90.0

const GRENADE_MASS := 0.45
# Bouncy enough to skip off a wall, not so bouncy it comes back at you.
const BOUNCE := 0.35
const FRICTION := 0.8

const BODY_RADIUS := 0.045
const BODY_HEIGHT := 0.115
const SHELL_COLOR := Color(0.18, 0.24, 0.16)
const LEVER_COLOR := Color(0.45, 0.46, 0.48)
# The fuse indicator blinks faster as detonation approaches -- the only
# warning anyone gets, and the thing that makes a live grenade on the floor
# genuinely tense.
const BLINK_COLOR := Color(1.0, 0.25, 0.1)
const BLINK_SLOWEST_HZ := 2.0
const BLINK_FASTEST_HZ := 14.0

var is_authoritative := false
var thrower: Node = null

var _fuse_remaining := FUSE_SECONDS
var _has_exploded := false
var _blink_material: StandardMaterial3D
var _blink_phase := 0.0

func _ready() -> void:
	mass = GRENADE_MASS
	continuous_cd = true
	var physics_material := PhysicsMaterial.new()
	physics_material.bounce = BOUNCE
	physics_material.friction = FRICTION
	physics_material_override = physics_material

	var collision := CollisionShape3D.new()
	var shape := CapsuleShape3D.new()
	shape.radius = BODY_RADIUS
	shape.height = BODY_HEIGHT
	collision.shape = shape
	add_child(collision)

	_build_visual()
	# Same "a loose physics prop in the world is grabbable" tag every other
	# RigidBody3D prop carries (see player.gd's own carry system). Picking up
	# a live grenade is entirely allowed, and entirely your problem.
	add_to_group("grabbable")

func _process(delta: float) -> void:
	if _has_exploded:
		return

	_fuse_remaining -= delta

	# Blink rate ramps from BLINK_SLOWEST_HZ to BLINK_FASTEST_HZ across the
	# fuse. Driven by an accumulated PHASE rather than by sampling a sine of
	# absolute time, because the frequency itself is changing -- sampling
	# sin(t * rising_hz) makes the blink jump backwards as the rate rises,
	# instead of smoothly accelerating.
	if _blink_material != null:
		var burned: float = 1.0 - clampf(_fuse_remaining / FUSE_SECONDS, 0.0, 1.0)
		var hz: float = lerpf(BLINK_SLOWEST_HZ, BLINK_FASTEST_HZ, burned)
		_blink_phase += delta * hz * TAU
		var pulse: float = 0.5 + 0.5 * sin(_blink_phase)
		_blink_material.emission_energy_multiplier = lerpf(0.4, 6.0, pulse)

	if _fuse_remaining <= 0.0:
		_explode()

func _explode() -> void:
	if _has_exploded:
		return
	_has_exploded = true
	# Every peer plays the full visual; only the throwing peer's own copy
	# actually deals damage (see this file's own class comment). Passing
	# damage 0.0 on a replica keeps ONE code path for the effect rather than
	# a second, silently-diverging visual-only branch.
	var damage: float = BLAST_DAMAGE if is_authoritative else 0.0
	Explosion.spawn(global_position, get_tree(), thrower, BLAST_RADIUS, damage)
	queue_free()

func _build_visual() -> void:
	var shell_material := StandardMaterial3D.new()
	shell_material.albedo_color = SHELL_COLOR
	shell_material.roughness = 0.7
	shell_material.metallic = 0.3

	var body := MeshInstance3D.new()
	var body_mesh := CapsuleMesh.new()
	body_mesh.radius = BODY_RADIUS
	body_mesh.height = BODY_HEIGHT
	body.mesh = body_mesh
	body.material_override = shell_material
	add_child(body)

	var lever_material := StandardMaterial3D.new()
	lever_material.albedo_color = LEVER_COLOR
	lever_material.metallic = 0.8
	lever_material.roughness = 0.35

	# The spoon/lever down one side, and the fuse cap on top -- just enough
	# silhouette that it reads as a grenade rather than a pill at a glance.
	var lever := MeshInstance3D.new()
	var lever_mesh := BoxMesh.new()
	lever_mesh.size = Vector3(0.012, BODY_HEIGHT * 0.8, 0.02)
	lever.mesh = lever_mesh
	lever.material_override = lever_material
	lever.position = Vector3(BODY_RADIUS * 0.95, 0.0, 0.0)
	add_child(lever)

	var cap := MeshInstance3D.new()
	var cap_mesh := CylinderMesh.new()
	cap_mesh.top_radius = BODY_RADIUS * 0.42
	cap_mesh.bottom_radius = BODY_RADIUS * 0.55
	cap_mesh.height = 0.022
	cap.mesh = cap_mesh
	cap.material_override = lever_material
	cap.position = Vector3(0, BODY_HEIGHT * 0.5, 0)
	add_child(cap)

	_blink_material = StandardMaterial3D.new()
	_blink_material.albedo_color = BLINK_COLOR
	_blink_material.emission_enabled = true
	_blink_material.emission = BLINK_COLOR
	_blink_material.emission_energy_multiplier = 1.0
	var blinker := MeshInstance3D.new()
	var blink_mesh := SphereMesh.new()
	blink_mesh.radius = 0.012
	blink_mesh.height = 0.024
	blinker.mesh = blink_mesh
	blinker.material_override = _blink_material
	blinker.position = Vector3(0, BODY_HEIGHT * 0.5 + 0.014, 0)
	add_child(blinker)
