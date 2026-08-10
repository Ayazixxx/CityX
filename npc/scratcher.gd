extends CharacterBody3D
## The Scratcher: a fast, hostile quadruped -- spawned via "/spawn
## scratcher". Built on a Meshy-generated model (npc/scratcher/scratcher.glb)
## with its own 27-bone rig, the same "Meshy-native skeleton, no Mixamo
## retargeting needed" path npc/mutant.gd already established.
##
## Runs at exactly the player's own SPRINT speed (see CHASE_SPEED) -- an
## explicit request, and it changes what this enemy IS: you cannot outrun a
## Scratcher in a straight line. Sprinting only holds it at distance; losing
## one means breaking line of sight, climbing, or turning and fighting.
##
## ANIMATION, stated plainly because only one clip actually existed: the
## source asset ships a walk and nothing else. tests/scratch_scratcher_bake.gd
## derives the RUN from that walk (time-compressed, leg swing widened) and
## authors the ATTACK -- head rears up, then slams down -- from scratch
## against the rig's own bones. Neither is motion-captured; they're
## synthesised, and the run in particular is an exaggerated walk rather than
## a true gallop cycle. That reads well at speed but is the first thing to
## replace if a real quadruped run clip ever turns up.

const WalkAnim := preload("res://npc/scratcher/animations/scratcher_walk.tres")
const RunAnim := preload("res://npc/scratcher/animations/scratcher_run.tres")
const AttackAnim := preload("res://npc/scratcher/animations/scratcher_attack.tres")
const ModelScene := preload("res://npc/scratcher/scratcher.glb")

const GRAVITY := 9.8
const FLOOR_MAX_ANGLE_DEGREES := 80.0
const NpcSteering = preload("res://npc/npc_steering.gd")

# The model imports at roughly 3mm tall: its Armature carries a 0.01 scale
# and the rig's own bone-rest extent is 0.33 units, so a scale of 348 gives
# 1.15m -- which is what this was, before being tripled on request. At 3x
# it stands ~3.45m: taller than a person by half again over, closer to the
# mutant's own bulk than to a dog's.
const SIZE_MULTIPLIER := 3.0
const MODEL_SCALE := 348.0 * SIZE_MULTIPLIER
const TARGET_HEIGHT_METERS := 1.15 * SIZE_MULTIPLIER

# The source GLB ships with NO albedo texture (confirmed by inspecting the
# imported material directly: albedo_texture is null, albedo_color is flat
# white), so straight out of the box it renders as a featureless white
# silhouette. This tint is a stand-in, not a choice about how the creature
# ought to look -- drop a real texture in and this override should go.
# Dark and matte so the spiky mane reads as a shape against the city.
const BODY_COLOR := Color(0.36, 0.32, 0.29)
const BODY_ROUGHNESS := 0.92

# --- Glowing red eyes, seated in the model's own eye sockets. ---
const EYE_COLOR := Color(1.0, 0.06, 0.03)
const EYE_EMISSION_ENERGY := 7.0
const EYE_RADIUS_METERS := 0.07
# Where each eye sits, in the CREATURE's own space, in real metres --
# measured against the rig rather than guessed: the skull runs from the
# "head" bone (y 2.37, z 1.36) forward and DOWN to "headend", the snout tip
# (y 2.12, z 1.99), so these sit about a third along that line and just
# above it.
#
# Note the x values straddle -0.05, not 0: this rig's own head bone is
# genuinely offset ~15cm to one side of the body centreline, and eyes
# centred on the BODY would sit visibly off the face. Found by measuring
# the placement rather than by looking at it.
const EYE_POSITIONS := [
	Vector3(0.06, 2.33, 1.60),
	Vector3(-0.16, 2.33, 1.60),
]
const EYE_LIGHT_POSITION := Vector3(-0.05, 2.33, 1.70)
# A small light at the head so the glow actually falls on what's in front
# of it -- an emissive material alone lights nothing, and these are meant
# to read in a dark street.
const EYE_LIGHT_RANGE := 4.5
const EYE_LIGHT_ENERGY := 2.2

const MAX_HEALTH := 70.0
# Sets its own ENEMY_LEVEL/XP_REWARD by hand, per wolf.gd's own comment on
# why there's no shared formula yet. Rated between the wolf (1) and the
# gunman (3): it hits harder than a wolf and is far harder to escape, but
# it has to close to melee to do anything at all.
const ENEMY_LEVEL := 2
const XP_REWARD := 85.0

const DETECTION_RANGE := 30.0
# Exactly player.gd's own SPRINT_SPEED. Not "about the same" -- the point
# is that flat-out sprinting exactly matches it, so the gap between you
# never closes OR opens while you both run straight.
const CHASE_SPEED := 7.5
# Below this it drops to the walk clip -- a creature that has already caught
# you shouldn't still be galloping on the spot.
const WALK_SPEED := 2.6

# Reach scales with the body, and it HAS to: these are measured from the
# creature's own origin, so at 3x its head is already past a 2.4m range
# before it could ever swing -- it would stand inside you failing to
# attack. Damage and health are deliberately NOT scaled; only the geometry
# that stopped fitting.
const ATTACK_RANGE := 2.4 * SIZE_MULTIPLIER
const ATTACK_DAMAGE := 16.0
const ATTACK_COOLDOWN_SECONDS := 1.35
# When during the attack clip the damage actually lands: the moment the
# head finishes slamming DOWN (the clip's own slam key), not the moment the
# attack starts. Anything else would hit you while the head is still rearing
# up, which reads as being hit by nothing.
const ATTACK_HIT_TIME := 0.46
# Re-checked at the hit moment rather than trusting the range that started
# the attack -- a little longer, so stepping back slightly during the windup
# is a real dodge but doesn't make the attack impossible to land.
const ATTACK_HIT_RANGE := 3.0 * SIZE_MULTIPLIER

# Where it stops sprinting and walks the last stretch in, so the final
# stride into melee isn't a skid. Derived from ATTACK_RANGE rather than
# scaled with the body, and that distinction is the whole point: at 3x,
# scaling this the same way put the walk zone at 13.5m, which meant the
# creature crawled at WALK_SPEED for the last 13 metres and you could
# simply stroll away from the thing built to be unoutrunnable. Caught by
# its own regression test.
const WALK_ANIM_MAX_DISTANCE := ATTACK_RANGE + 2.0

const TURN_SPEED_RADIANS := 6.0

var health := MAX_HEALTH
var is_dead := false

var _model: Node3D
var _anim: AnimationPlayer
var _target: Node3D = null
## Steers this around buildings instead of into them -- see
## npc/npc_steering.gd.
var _steering := NpcSteering.new()
var _attack_cooldown_remaining := 0.0
var _attack_time_remaining := 0.0
var _attack_hit_pending := false
var _current_clip := ""

@onready var _collision: CollisionShape3D = $CollisionShape3D

func _ready() -> void:
	floor_max_angle = deg_to_rad(FLOOR_MAX_ANGLE_DEGREES)
	collision_layer = 4 # the shared "hostile NPC" layer every other NPC body uses
	collision_mask = 1 | 4
	add_to_group("lockable_npcs")

	_model = ModelScene.instantiate()
	add_child(_model)
	_model.scale = Vector3.ONE * MODEL_SCALE
	_apply_body_tint()
	_add_eyes()

	_anim = _model.get_node_or_null("AnimationPlayer")
	if _anim != null:
		# Added to the model's OWN AnimationPlayer rather than a fresh one,
		# so the baked clips' bone paths ("Armature/Skeleton3D:head") resolve
		# against exactly the root the source clip was authored against.
		var library := AnimationLibrary.new()
		library.add_animation("walk", WalkAnim)
		library.add_animation("run", RunAnim)
		library.add_animation("attack", AttackAnim)
		_anim.add_animation_library("scratcher", library)
		_play("scratcher/walk")

	var shape := CapsuleShape3D.new()
	shape.radius = 0.42
	shape.height = TARGET_HEIGHT_METERS
	_collision.shape = shape
	# Lying along the body rather than standing upright -- this is a
	# quadruped, so its footprint is long and low, not a standing cylinder.
	_collision.rotation_degrees = Vector3(90, 0, 0)
	_collision.position = Vector3(0, TARGET_HEIGHT_METERS * 0.45, 0)
	# After the shape AND its rotation are set: the steering sizes its probe
	# by measuring the capsule through its own transform, and this one is
	# laid flat (see npc_steering.gd's own configure_from).
	_steering.configure_from(self)

## material_override rather than editing the imported material in place --
## that material is a sub-resource of the .glb and is SHARED by every
## Scratcher ever spawned, so writing to it would be a global edit (and
## would persist into the imported asset). See BODY_COLOR on why this
## exists at all.
func _apply_body_tint() -> void:
	var material := StandardMaterial3D.new()
	material.albedo_color = BODY_COLOR
	material.roughness = BODY_ROUGHNESS
	for node in _all_descendants(_model):
		if node is MeshInstance3D:
			(node as MeshInstance3D).material_override = material

## Two glowing eyes parented to the head bone, so they track the skull
## through every animation -- including the attack's own slam, where eyes
## left behind on the body would visibly detach from the head.
##
## A BoneAttachment3D follows the bone's POSE, and its children sit in that
## bone's own local space, whose axes on this rig bear no relation to the
## model's. So EYE_OFFSETS is authored in readable model axes and converted
## here through the head bone's own rest basis, which is exactly the
## transform between those two spaces. Same reasoning the attack clip's own
## pitch axis needed (see tests/scratch_scratcher_bake.gd).
func _add_eyes() -> void:
	var skeleton: Skeleton3D = _model.get_node_or_null("Armature/Skeleton3D")
	if skeleton == null:
		return
	var head_idx := skeleton.find_bone("head")
	if head_idx < 0:
		return

	var attachment := BoneAttachment3D.new()
	attachment.bone_name = "head"
	attachment.bone_idx = head_idx
	skeleton.add_child(attachment)

	var material := StandardMaterial3D.new()
	material.albedo_color = EYE_COLOR
	material.emission_enabled = true
	material.emission = EYE_COLOR
	material.emission_energy_multiplier = EYE_EMISSION_ENERGY
	# Unshaded, so an eye stays lit even with the skull between it and every
	# light in the scene -- the whole point is that they glow in the dark.
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED

	# The full transform from the creature's own space down to the head
	# bone, built explicitly from the node chain rather than read off
	# global_transform -- global would only be right while the creature
	# happens to be sitting at the origin, which it is during _ready() and
	# is NOT a moment later once the spawner positions it.
	var armature: Node3D = _model.get_node("Armature")
	var chain: Transform3D = _model.transform * armature.transform * skeleton.transform * skeleton.get_bone_global_rest(head_idx)
	var to_bone_local := chain.affine_inverse()
	# Everything under the attachment inherits the model's own huge scale,
	# so sizes authored in metres have to be divided back out.
	var chain_scale: float = maxf(chain.basis.get_scale().x, 0.0001)

	for position in EYE_POSITIONS:
		var eye := MeshInstance3D.new()
		var mesh := SphereMesh.new()
		mesh.radius = EYE_RADIUS_METERS / chain_scale
		mesh.height = mesh.radius * 2.0
		mesh.radial_segments = 8
		mesh.rings = 6
		eye.mesh = mesh
		eye.material_override = material
		eye.position = to_bone_local * (position as Vector3)
		attachment.add_child(eye)

	var glow := OmniLight3D.new()
	glow.light_color = EYE_COLOR
	glow.light_energy = EYE_LIGHT_ENERGY
	# omni_range is in world metres too, so it gets the same treatment --
	# left unscaled it would light the entire city.
	glow.omni_range = EYE_LIGHT_RANGE / chain_scale
	glow.shadow_enabled = false
	glow.position = to_bone_local * EYE_LIGHT_POSITION
	attachment.add_child(glow)

func _all_descendants(node: Node) -> Array:
	var out := [node]
	for child in node.get_children():
		out.append_array(_all_descendants(child))
	return out

func _physics_process(delta: float) -> void:
	if is_dead:
		return
	if not is_on_floor():
		velocity.y -= GRAVITY * delta
	else:
		velocity.y = 0.0

	_attack_cooldown_remaining = maxf(0.0, _attack_cooldown_remaining - delta)
	_tick_attack(delta)

	if _target == null or not is_instance_valid(_target) or bool(_target.get("is_dead")):
		_target = _find_nearest_player()

	if _target == null:
		velocity.x = 0.0
		velocity.z = 0.0
		if _attack_time_remaining <= 0.0:
			_play("scratcher/walk")
		move_and_slide()
		return

	var to_target: Vector3 = _target.global_position - global_position
	to_target.y = 0.0
	var distance := to_target.length()

	if distance > DETECTION_RANGE:
		_target = null
		velocity.x = 0.0
		velocity.z = 0.0
		move_and_slide()
		return

	_face(to_target, delta)

	# Mid-attack: planted. A lunging head-slam that also slides forward
	# would look like the whole creature is skating.
	if _attack_time_remaining > 0.0:
		velocity.x = 0.0
		velocity.z = 0.0
		move_and_slide()
		return

	if distance <= ATTACK_RANGE:
		velocity.x = 0.0
		velocity.z = 0.0
		if _attack_cooldown_remaining <= 0.0:
			_start_attack()
	else:
		# Steered around buildings rather than straight at you -- see
		# npc/npc_steering.gd.
		var direction := _steering.steer(self, to_target / maxf(distance, 0.0001), delta)
		# Full sprint speed at range; drops to a walk once it's right on
		# top of you, so the last stride into melee isn't a skid.
		var speed: float = CHASE_SPEED if distance > WALK_ANIM_MAX_DISTANCE else WALK_SPEED
		velocity.x = direction.x * speed
		velocity.z = direction.z * speed
		_play("scratcher/run" if distance > WALK_ANIM_MAX_DISTANCE else "scratcher/walk")

	move_and_slide()

## Runs the attack clip's own timeline, landing the damage at the slam
## rather than on the frame the attack began (see ATTACK_HIT_TIME).
func _tick_attack(delta: float) -> void:
	if _attack_time_remaining <= 0.0:
		return
	var previous := _attack_time_remaining
	_attack_time_remaining = maxf(0.0, _attack_time_remaining - delta)
	var elapsed := AttackAnim.length - _attack_time_remaining
	var previous_elapsed := AttackAnim.length - previous
	if _attack_hit_pending and previous_elapsed < ATTACK_HIT_TIME and elapsed >= ATTACK_HIT_TIME:
		_attack_hit_pending = false
		_land_attack()

func _start_attack() -> void:
	_attack_cooldown_remaining = ATTACK_COOLDOWN_SECONDS + AttackAnim.length
	_attack_time_remaining = AttackAnim.length
	_attack_hit_pending = true
	_play("scratcher/attack", true)

func _land_attack() -> void:
	if _target == null or not is_instance_valid(_target) or bool(_target.get("is_dead")):
		return
	var to_target: Vector3 = _target.global_position - global_position
	to_target.y = 0.0
	if to_target.length() > ATTACK_HIT_RANGE:
		return # stepped out of it during the windup -- a real dodge
	if _target.has_method("take_damage"):
		_target.take_damage(ATTACK_DAMAGE, self)

## Same duck-typed player search every other NPC in this project uses.
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

## Turned toward the target over time rather than snapped (unlike wolf.gd's
## own instant look_at) -- at sprint speed an instant 180 reads as the model
## popping, and a real turning circle is also the only thing that makes
## juking one worthwhile.
func _face(to_target: Vector3, delta: float) -> void:
	if to_target.length_squared() < 0.0001:
		return
	var desired := atan2(to_target.x, to_target.z)
	rotation.y = lerp_angle(rotation.y, desired, clampf(TURN_SPEED_RADIANS * delta, 0.0, 1.0))
	rotation.x = 0.0
	rotation.z = 0.0

## `restart` forces a clip that's already current to play again from frame 0
## -- AnimationPlayer.play() on the already-playing animation is a no-op,
## which would silently swallow every attack after the first (the exact
## issue character.gd's own shoot() documents).
func _play(clip: String, restart := false) -> void:
	if _anim == null:
		return
	if _current_clip == clip and not restart:
		return
	_current_clip = clip
	if restart:
		_anim.stop()
	_anim.play(clip)

## Same duck-typed 4-arg signature every damageable thing here accepts;
## hitstun/finisher are ignored, as with every other NPC.
func take_damage(amount: float, attacker: Node = null, _hitstun_seconds: float = 0.0, _is_finisher: bool = false) -> void:
	if is_dead or amount <= 0.0:
		return
	if attacker != null and is_instance_valid(attacker) and attacker.is_in_group("players"):
		_target = attacker
	health = maxf(0.0, health - amount)
	if health <= 0.0:
		_die(attacker)

## No ragdoll rig (this is its own Meshy skeleton, not the player's fused
## body with its physics bones), so it tips onto its side the same way
## wolf.gd's own corpse does.
func _die(attacker: Node = null) -> void:
	if attacker != null and attacker.has_method("add_xp"):
		attacker.add_xp(XP_REWARD)
	is_dead = true
	velocity = Vector3.ZERO
	_target = null
	if _anim != null:
		_anim.stop()
	var tween := create_tween()
	tween.tween_property(_model, "rotation:z", deg_to_rad(82), 0.45).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tween.parallel().tween_property(_model, "position:y", _model.position.y - 0.12, 0.45)
