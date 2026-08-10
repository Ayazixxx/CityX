extends RigidBody3D
## The in-flight glass bottle -- a SEPARATE Node from the equipped
## weapons/glass_bottle.gd prop, spawned fresh every throw (see player.gd's
## own _try_throw_bottle/_spawn_thrown_bottle) rather than reparenting the
## equipped one, since it needs real RigidBody3D physics (gravity-arced
## flight, a genuine collision body) the equipped prop never needs.
##
## Networked (see player.gd's own _rpc_throw_bottle): every peer spawns its
## OWN local copy of the flying bottle from the SAME broadcast spawn
## transform/velocity and simulates it independently -- there is no
## thrown-projectile precedent anywhere else in this project (guns are
## hitscan, no visible flying bullet). is_authoritative distinguishes the
## throwing peer's own ORIGINAL instance (true) from every other peer's own
## purely-cosmetic replica (false, the default) -- ONLY the authoritative
## instance is ever allowed to actually apply damage/route the networked-hit
## RPC. Every replica still plays its own local impact effects (shatter,
## blood, sound) independently -- only the DAMAGE APPLICATION itself is
## restricted, exactly mirroring how player.gd's own _punch() raycast is
## likewise only ever evaluated on the attacking player's own machine. Not
## restricting this would mean every peer's own independently-simulated
## replica could each separately detect "I hit the defender" and each fire
## off their own hit-request, applying the same throw's damage more than
## once.

const DAMAGE := 18.0
const GlassShatterScene := preload("res://weapons/glass_shatter.tscn")
const BloodSplatter = preload("res://weapons/blood_splatter.gd")
const TerrainImpactSounds := [
	preload("res://weapons/assets/audio/glass_hit_terrain.mp3"),
	preload("res://weapons/assets/audio/glass_break.mp3"),
]
const EnemyImpactSound := preload("res://weapons/assets/audio/glass_hit_enemy.mp3")

# Same bottle proportions as weapons/glass_bottle.gd's own INTACT geometry
# (a thrown bottle is always intact -- see can_throw()'s own "only while
# INTACT" gate on the equip side) -- duplicated rather than shared, since
# this is a RigidBody3D (a different node type than glass_bottle.gd's own
# Node3D) and only ever needs the ONE intact visual, not that script's full
# 3-state durability/mesh-swap machinery. _flat_material/_add_tube/_tri
# themselves are the SAME parameter-driven primitives glass_bottle.gd
# already duplicated from weapons/two_handed_sword.gd -- see its own class
# comment for why these specifically are safe/cheap to copy again here.
const BODY_HEIGHT := 0.15
const SHOULDER_HEIGHT := 0.02
const NECK_HEIGHT := 0.045
const RIM_HEIGHT := 0.015
const BODY_RADIUS := 0.032
const NECK_RADIUS := 0.011
const GLASS_COLOR := Color(0.34, 0.22, 0.08)
const GLASS_ALPHA := 0.55

var is_authoritative := false
var thrower: Node = null
var _has_impacted := false

func _ready() -> void:
	_build_visual()
	contact_monitor = true
	max_contacts_reported = 4
	continuous_cd = true
	# A genuinely free layer -- 1=world, 2=vehicles, 4=characters, 8=ragdoll
	# (confirmed: character.gd's own RAGDOLL_COLLISION_LAYER) -- so nothing
	# else in the project mistakes a flying bottle for any of those.
	collision_layer = 16
	collision_mask = 1 | 4 # world geometry + characters, matching _punch()'s own raycast mask exactly
	body_entered.connect(_on_body_entered)

## Called by player.gd's own _spawn_thrown_bottle() right after add_child()
## -- also adds a one-time collision exception so the bottle can't
## immediately re-collide with the thrower's own body the instant it spawns
## (it starts essentially inside/at the thrower's own collision shape).
func set_thrower(who: Node) -> void:
	thrower = who
	if who is CollisionObject3D:
		add_collision_exception_with(who)

func _build_visual() -> void:
	var glass := _flat_material(GLASS_COLOR, 0.1, 0.15)
	glass.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	glass.albedo_color.a = GLASS_ALPHA
	_add_tube(Vector3(0, 0, 0), Vector3(0, BODY_HEIGHT * 0.15, 0), BODY_RADIUS * 0.85, BODY_RADIUS, glass)
	_add_tube(Vector3(0, BODY_HEIGHT * 0.15, 0), Vector3(0, BODY_HEIGHT, 0), BODY_RADIUS, BODY_RADIUS, glass)
	_add_tube(Vector3(0, BODY_HEIGHT, 0), Vector3(0, BODY_HEIGHT + SHOULDER_HEIGHT, 0), BODY_RADIUS, NECK_RADIUS, glass)
	var neck_top_y := BODY_HEIGHT + SHOULDER_HEIGHT + NECK_HEIGHT
	_add_tube(Vector3(0, BODY_HEIGHT + SHOULDER_HEIGHT, 0), Vector3(0, neck_top_y, 0), NECK_RADIUS, NECK_RADIUS, glass)
	_add_tube(Vector3(0, neck_top_y, 0), Vector3(0, neck_top_y + RIM_HEIGHT, 0), NECK_RADIUS, NECK_RADIUS * 1.2, glass)
	var col := CollisionShape3D.new()
	var shape := CapsuleShape3D.new()
	shape.radius = BODY_RADIUS
	shape.height = neck_top_y + RIM_HEIGHT
	col.shape = shape
	# CapsuleShape3D's own default axis is already local Y, matching the
	# bottle's own "up" (base at y=0, neck/rim at the top) -- no rotation
	# needed, just centered vertically along the shape's own length.
	col.position = Vector3(0, (neck_top_y + RIM_HEIGHT) * 0.5, 0)
	add_child(col)

func _play_impact_sound(stream: AudioStream, at_position: Vector3) -> void:
	# A temporary, self-freeing 3D one-shot player -- same "spawn a
	# standalone effect, let it finish, queue_free itself" idiom
	# blood_splatter.gd's own particle system already uses, applied to
	# audio: this node must outlive this script's own queue_free() below --
	# a child AudioStreamPlayer3D would otherwise be cut off mid-playback
	# the instant its parent frees.
	var player := AudioStreamPlayer3D.new()
	player.stream = stream
	get_tree().current_scene.add_child(player)
	player.global_position = at_position
	player.play()
	player.finished.connect(player.queue_free)

func _on_body_entered(body: Node) -> void:
	if _has_impacted:
		return
	_has_impacted = true

	var shatter: GPUParticles3D = GlassShatterScene.instantiate()
	get_tree().current_scene.add_child(shatter)
	shatter.global_position = global_position

	var hit_character: bool = body.has_method("take_damage")
	_play_impact_sound(
		EnemyImpactSound if hit_character else TerrainImpactSounds[randi() % TerrainImpactSounds.size()],
		global_position
	)

	if not is_authoritative:
		queue_free() # cosmetic replica -- no damage, no RPC, ever (see this file's own class comment)
		return

	if hit_character:
		BloodSplatter.spawn(global_position, -linear_velocity.normalized(), get_tree())
		# Strength stat bonus (see player.gd's own apply_strength()) --
		# duck-typed since thrower is just typed as Node here.
		var actual_damage := DAMAGE
		if thrower != null and thrower.has_method("apply_strength"):
			actual_damage = thrower.apply_strength(DAMAGE)
		# IDENTICAL routing to player.gd's own _punch()/_try_swing_bottle()
		# -- only the throwing peer's own ORIGINAL copy (is_authoritative)
		# ever reaches this branch, so this can't double-fire from every
		# peer's own independently-simulated replica.
		if Net.is_networked() and body.is_in_group("players") and not body.is_multiplayer_authority():
			body.rpc_id(body.get_multiplayer_authority(), "_rpc_request_hit", actual_damage, thrower.get_multiplayer_authority(), 0.0, false)
		else:
			body.take_damage(actual_damage, thrower, 0.0, false)
	queue_free()

# --- Duplicated from weapons/two_handed_sword.gd via weapons/glass_bottle.gd
# (both credit the original source in their own class comments) --
# genuinely parameter-driven, no state baked in. ---

func _flat_material(color: Color, metallic: float, roughness: float) -> StandardMaterial3D:
	var mat := StandardMaterial3D.new()
	mat.albedo_color = color
	mat.metallic = metallic
	mat.roughness = roughness
	mat.metallic_specular = 0.0
	return mat

func _add_tube(a: Vector3, b: Vector3, radius_a: float, radius_b: float, mat: Material) -> void:
	var mesh_inst := MeshInstance3D.new()
	var cm := CylinderMesh.new()
	cm.top_radius = radius_b
	cm.bottom_radius = radius_a
	var length: float = a.distance_to(b)
	cm.height = maxf(length, 0.001)
	mesh_inst.mesh = cm
	mesh_inst.material_override = mat
	var mid := (a + b) / 2.0
	var y_axis: Vector3 = (b - a).normalized() if length > 0.001 else Vector3.UP
	var helper := Vector3.FORWARD if absf(y_axis.dot(Vector3.FORWARD)) < 0.95 else Vector3.RIGHT
	var x_axis := y_axis.cross(helper).normalized()
	var z_axis := x_axis.cross(y_axis).normalized()
	mesh_inst.transform = Transform3D(Basis(x_axis, y_axis, z_axis), mid)
	add_child(mesh_inst)
