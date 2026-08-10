extends Node3D
## A grenade blast: the visual/physical event itself, independent of
## whatever set it off (see weapons/thrown_grenade.gd, its only caller
## today). Spawned via the static spawn() below, exactly the way
## weapons/blood_splatter.gd's own spawn() is already used across this
## project -- one self-contained, self-freeing effect node dropped into the
## world at a point, with no lifecycle for the caller to manage.
##
## Four layers make up the look, all built in code rather than as authored
## assets, matching how everything else in this project is made:
##   1. a hard white-hot FLASH (OmniLight3D) that really lights the
##      surrounding geometry for a moment -- the single biggest thing
##      selling an explosion as an event rather than a decal, especially
##      inside this project's own deliberately dark building interiors;
##   2. a FIREBALL, an emissive sphere that punches out to blast radius and
##      fades;
##   3. a SHOCKWAVE, a flattened ring racing outward slightly ahead of the
##      fireball, which is what gives the blast a readable SIZE;
##   4. a debris/smoke PARTICLE burst that outlives the rest.
##
## Damage is applied ONCE, on the frame it spawns -- a single physics sphere
## query, with linear falloff from the centre. Anything with take_damage()
## takes damage (the same duck-typing every weapon in this project uses),
## and anything that's a RigidBody3D also gets a real impulse away from the
## blast, so ragdolls, bottles and loose props are physically thrown. That
## impulse is why an explosion reads as powerful even when it doesn't kill.
##
## Known simplification, stated rather than hidden: damage does NOT check
## line of sight, so a blast on the far side of a wall still hurts through
## it. Fixing that is a per-target raycast in _apply_blast(); it's left out
## of this first pass deliberately, since every current use is out in the
## open street.

const BlastRadiusDefault := 6.0

# --- Flash ---
const FLASH_ENERGY := 22.0
const FLASH_RANGE_SCALE := 2.4 # relative to blast radius
const FLASH_COLOR := Color(1.0, 0.86, 0.55)
const FLASH_SECONDS := 0.28

# --- Fireball ---
const FIREBALL_CORE_COLOR := Color(1.0, 0.95, 0.75)
const FIREBALL_EDGE_COLOR := Color(1.0, 0.42, 0.09)
const FIREBALL_GROW_SECONDS := 0.16
const FIREBALL_FADE_SECONDS := 0.34
# Starts as a bright point rather than at zero -- a sphere scaled from
# exactly 0 pops in as a single degenerate pixel for a frame.
const FIREBALL_START_SCALE := 0.12

# --- Shockwave ---
const SHOCKWAVE_COLOR := Color(1.0, 0.78, 0.45)
const SHOCKWAVE_SECONDS := 0.42
const SHOCKWAVE_THICKNESS := 0.22
# Runs out past the damage radius: the visible edge of a real blast
# outpaces its own lethal zone, and it reads better than a wave that stops
# exactly where the damage does.
const SHOCKWAVE_OVERSHOOT := 1.35

# --- Debris ---
const DEBRIS_COUNT := 48
const DEBRIS_LIFETIME := 1.4
const DEBRIS_SPEED_MIN := 6.0
const DEBRIS_SPEED_MAX := 15.0
const DEBRIS_COLOR := Color(0.22, 0.2, 0.19)

# --- Blast ---
const DEFAULT_DAMAGE := 90.0
# Fraction of full damage still dealt right at the edge of the radius --
# not zero, so grazing a blast still stings rather than being a total
# non-event.
const EDGE_DAMAGE_FRACTION := 0.25
const IMPULSE_FORCE := 14.0
# Added to every impulse's own direction so blasts throw things UP and out
# rather than skidding them flat along the ground, which looks far weaker.
const IMPULSE_UPWARD_BIAS := 0.45

var _elapsed := 0.0
var _lifetime := 0.0
var _flash: OmniLight3D
var _fireball: MeshInstance3D
var _fireball_material: StandardMaterial3D
var _shockwave: MeshInstance3D
var _shockwave_material: StandardMaterial3D
var _radius := BlastRadiusDefault

## Drops one explosion into the world at `position` and returns it. `source`
## is whoever caused it (a thrown grenade's own thrower) -- forwarded as the
## `attacker` on every take_damage() call so kills credit the right player
## and grant them XP, and excluded from its own blast so you can't gib
## yourself with a grenade you're standing next to... except you CAN: only
## the projectile itself is excluded, not the thrower. Standing in your own
## blast hurts, which is the point of a grenade having a fuse.
static func spawn(position: Vector3, tree: SceneTree, source: Node = null, radius: float = BlastRadiusDefault, damage: float = DEFAULT_DAMAGE) -> Node3D:
	if tree == null or tree.current_scene == null:
		return null
	var explosion := new()
	explosion._radius = radius
	tree.current_scene.add_child(explosion)
	explosion.global_position = position
	explosion._apply_blast(source, damage)
	return explosion

func _ready() -> void:
	_lifetime = maxf(maxf(FLASH_SECONDS, FIREBALL_GROW_SECONDS + FIREBALL_FADE_SECONDS), maxf(SHOCKWAVE_SECONDS, DEBRIS_LIFETIME))
	_build_flash()
	_build_fireball()
	_build_shockwave()
	_build_debris()

func _process(delta: float) -> void:
	_elapsed += delta

	# Flash: an instant spike that decays fast, on a curve rather than
	# linearly -- a linear fade reads as a light being dimmed, not as a
	# detonation.
	if _flash != null:
		var flash_t: float = clampf(_elapsed / FLASH_SECONDS, 0.0, 1.0)
		_flash.light_energy = FLASH_ENERGY * pow(1.0 - flash_t, 3.0)
		_flash.visible = flash_t < 1.0

	# Fireball: punch out to full radius, then fade in place.
	if _fireball != null:
		if _elapsed <= FIREBALL_GROW_SECONDS:
			var grow_t: float = _elapsed / FIREBALL_GROW_SECONDS
			# Eased out, so it's most of its size almost immediately.
			var eased: float = 1.0 - pow(1.0 - grow_t, 3.0)
			var s: float = lerpf(FIREBALL_START_SCALE, 1.0, eased)
			_fireball.scale = Vector3.ONE * s
		else:
			var fade_t: float = clampf((_elapsed - FIREBALL_GROW_SECONDS) / FIREBALL_FADE_SECONDS, 0.0, 1.0)
			_fireball.scale = Vector3.ONE * (1.0 + fade_t * 0.25)
			_fireball_material.albedo_color.a = 1.0 - fade_t
			_fireball_material.emission_energy_multiplier = 4.0 * (1.0 - fade_t)
			_fireball.visible = fade_t < 1.0

	# Shockwave: expands past the damage radius while thinning out.
	if _shockwave != null:
		var wave_t: float = clampf(_elapsed / SHOCKWAVE_SECONDS, 0.0, 1.0)
		var eased_wave: float = 1.0 - pow(1.0 - wave_t, 2.0)
		_shockwave.scale = Vector3.ONE * maxf(eased_wave * SHOCKWAVE_OVERSHOOT, 0.001)
		_shockwave_material.albedo_color.a = (1.0 - wave_t) * 0.7
		_shockwave.visible = wave_t < 1.0

	if _elapsed >= _lifetime:
		queue_free()

func _build_flash() -> void:
	_flash = OmniLight3D.new()
	_flash.light_color = FLASH_COLOR
	_flash.light_energy = FLASH_ENERGY
	_flash.omni_range = _radius * FLASH_RANGE_SCALE
	# No shadows: a one-frame-bright light casting real shadows is both
	# expensive and visually noisy at this duration, and nothing about the
	# effect depends on them.
	_flash.shadow_enabled = false
	add_child(_flash)

func _build_fireball() -> void:
	_fireball_material = StandardMaterial3D.new()
	_fireball_material.albedo_color = FIREBALL_EDGE_COLOR
	_fireball_material.emission_enabled = true
	_fireball_material.emission = FIREBALL_CORE_COLOR
	_fireball_material.emission_energy_multiplier = 4.0
	_fireball_material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	# Unshaded: a fireball is its own light source, so scene lighting has no
	# business darkening one side of it.
	_fireball_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	# Both faces, so standing INSIDE the blast still shows the fireball
	# rather than looking through its back faces into nothing.
	_fireball_material.cull_mode = BaseMaterial3D.CULL_DISABLED

	var mesh := SphereMesh.new()
	mesh.radius = _radius * 0.62
	mesh.height = _radius * 1.24
	mesh.radial_segments = 16
	mesh.rings = 10

	_fireball = MeshInstance3D.new()
	_fireball.mesh = mesh
	_fireball.material_override = _fireball_material
	_fireball.scale = Vector3.ONE * FIREBALL_START_SCALE
	add_child(_fireball)

func _build_shockwave() -> void:
	_shockwave_material = StandardMaterial3D.new()
	_shockwave_material.albedo_color = SHOCKWAVE_COLOR
	_shockwave_material.emission_enabled = true
	_shockwave_material.emission = SHOCKWAVE_COLOR
	_shockwave_material.emission_energy_multiplier = 2.0
	_shockwave_material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_shockwave_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_shockwave_material.cull_mode = BaseMaterial3D.CULL_DISABLED

	var mesh := TorusMesh.new()
	mesh.inner_radius = _radius - SHOCKWAVE_THICKNESS
	mesh.outer_radius = _radius
	mesh.rings = 24

	_shockwave = MeshInstance3D.new()
	_shockwave.mesh = mesh
	_shockwave.material_override = _shockwave_material
	# Sits just off the ground -- a ring exactly at blast height would be
	# half-buried in whatever surface the grenade landed on.
	_shockwave.position = Vector3(0, 0.1, 0)
	_shockwave.scale = Vector3.ONE * 0.001
	add_child(_shockwave)

func _build_debris() -> void:
	var particles := GPUParticles3D.new()
	particles.amount = DEBRIS_COUNT
	particles.lifetime = DEBRIS_LIFETIME
	particles.one_shot = true
	particles.explosiveness = 1.0 # the whole burst leaves at once, not as a stream
	particles.emitting = true

	var material := ParticleProcessMaterial.new()
	material.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_SPHERE
	material.emission_sphere_radius = _radius * 0.2
	material.direction = Vector3(0, 1, 0)
	material.spread = 180.0
	material.initial_velocity_min = DEBRIS_SPEED_MIN
	material.initial_velocity_max = DEBRIS_SPEED_MAX
	material.gravity = Vector3(0, -9.8, 0)
	material.damping_min = 1.0
	material.damping_max = 3.0
	material.scale_min = 0.4
	material.scale_max = 1.2
	particles.process_material = material

	var mesh := BoxMesh.new()
	mesh.size = Vector3(0.09, 0.09, 0.09)
	var debris_material := StandardMaterial3D.new()
	debris_material.albedo_color = DEBRIS_COLOR
	debris_material.roughness = 0.9
	mesh.material = debris_material
	particles.draw_pass_1 = mesh

	add_child(particles)

## One sphere query at spawn time -- everything caught takes falloff damage
## and, if it's a rigid body, a real impulse away from the blast.
##
## The mask is deliberately every layer: rather than hard-coding which of
## this project's layers hold players, NPCs and ragdoll bones (three
## different answers that have each moved before), it catches everything and
## then filters by what the node can actually DO -- static world geometry
## has neither take_damage() nor a RigidBody3D to push, so it drops out on
## its own. Same duck-typing every weapon here already relies on.
func _apply_blast(source: Node, damage: float) -> void:
	var space := get_world_3d().direct_space_state
	var shape := SphereShape3D.new()
	shape.radius = _radius
	var query := PhysicsShapeQueryParameters3D.new()
	query.shape = shape
	query.transform = Transform3D(Basis(), global_position)
	query.collision_mask = 0xFFFFFFFF
	query.collide_with_bodies = true
	query.collide_with_areas = false
	# Godot caps results per query; well above any plausible crowd inside
	# one blast, but named rather than left at the silent default of 32.
	var hits := space.intersect_shape(query, 64)

	# One body can report several shapes (a ragdoll is a dozen separate
	# capsules under one rig, and hitting each for full damage would delete
	# anything wearing one) -- so each collider is only ever processed once.
	var seen := {}
	for hit in hits:
		var body = hit.get("collider")
		if body == null or not is_instance_valid(body) or seen.has(body.get_instance_id()):
			continue
		seen[body.get_instance_id()] = true

		var to_body: Vector3 = body.global_position - global_position
		var distance := to_body.length()
		var falloff: float = 1.0 - clampf(distance / _radius, 0.0, 1.0)
		# Lerped down to EDGE_DAMAGE_FRACTION rather than to zero.
		var scaled_damage: float = damage * lerpf(EDGE_DAMAGE_FRACTION, 1.0, falloff)

		if body.has_method("take_damage"):
			body.take_damage(scaled_damage, source)

		if body is RigidBody3D:
			var direction := to_body.normalized() if distance > 0.01 else Vector3.UP
			direction = (direction + Vector3.UP * IMPULSE_UPWARD_BIAS).normalized()
			(body as RigidBody3D).apply_impulse(direction * IMPULSE_FORCE * maxf(falloff, 0.2))
