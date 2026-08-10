extends CharacterBody3D
## Hostile "Mutant" enemy -- spawned via the dev console's own "/spawn
## mutant" (see ui/dev_console.gd). Unlike wolf.gd/sloth.gd (procedural
## primitive bodies) or crimson_claw_monster.gd (an imported mesh with NO
## skeleton, hand-tweened claw pivots), this one is a real imported,
## SKINNED humanoid mesh (npc/assets/mutant.glb) driven by a real
## AnimationPlayer.
##
## npc/assets/mutant.glb's own pipeline is worth recording, since it took
## two very different attempts to get right: the FIRST attempt tried
## retargeting Mixamo mocap (Mutant Walking.fbx etc.) onto this mesh's own
## Meshy-auto-rigged skeleton, which required real cross-skeleton
## retargeting math (the two rigs have different rest poses/bone-axis
## conventions) and produced repeatedly distorted results despite the
## per-bone matrix math checking out correctly in isolation -- eventually
## traced to THREE compounding bugs (a live-action mid-bake interference
## bug, a root-translation axis mismatch between the two rigs, AND the
## simple fact that this creature's own natural design IS a hunched,
## crouching stance, which had been misread as distortion for a while).
## The user then separately re-rigged and animated the SAME base mesh
## directly on Meshy's own platform (producing Meshy_AI_Skulljaw_biped_
## Animation_*_withSkin.glb, one file per clip, each carrying the full
## mesh+skeleton+ONE baked animation on Meshy's own native rig) -- since
## every one of those files shares the EXACT same skeleton (same 24 bone
## names, same rest pose) as the already-decimated base mesh built for the
## first attempt, no retargeting math was needed at all this time: each
## clip's own Action was simply reassigned directly onto the decimated
## mesh's own skeleton (literally the same bone names, so the SAME action
## data applies unmodified) and combined into one glb via Blender's
## "Export All Actions". See the offline pipeline notes for the exact
## script.
##
## The mesh's own front faces +Z in its raw coordinate space (confirmed two
## independent ways: an offline render from both +Y/-Y, and directly in
## Godot by checking the Head bone sits at a more POSITIVE Z than the Hips
## in the rest pose, matching this hunched creature's forward lean) --
## opposite of this project's own "-Z is forward" convention every
## look_at()-oriented body assumes, same situation crimson_claw_monster.gd's
## own class comment already documents for a similar Meshy export. Fixed
## the same way: a 180-degree yaw on _facing_flip re-expresses the mesh's
## own +Z-facing geometry as -Z-facing.
##
## Ground-based movement (real gravity + is_on_floor(), same shape
## wolf.gd's own chase already establishes), NOT hovering like
## crimson_claw_monster.gd -- this mesh has real legs and a real walk/run
## cycle to show.
##
## Attack: weighted 3-way choice every time an attack starts (see
## _start_attack()) -- 40% jump attack (dealing 20% more damage than the
## other two, with most of its own long animation serving as the player's
## own "brief moment to dodge or block" reaction window before the impact
## near the end), and the remaining 60% split evenly between an axe spin
## attack and a shout-and-swing (the "Shouting Angrily" clip trimmed to
## just its own first ~3 seconds, which starts with a real downward swing
## before settling into a longer shout the player never actually sees).
## Each attack's own "impact" is a single hit-radius check at a fixed
## fraction of that clip's own length (see AXE_SPIN_IMPACT_FRACTION/
## SHOUT_SWING_IMPACT_FRACTION/JUMP_ATTACK_IMPACT_FRACTION), not a
## continuous per-frame window the way crimson_claw_monster.gd's own tweened
## swing checks -- a real baked animation already has one obvious "the hit
## actually lands here" moment per clip, so a single check at that instant
## is enough (and still lets a target who steps out of range in time
## genuinely dodge, same as the crimson claw's own real hit-or-miss design).
##
## Ragdoll on death: no per-bone joints (a full skeletal ragdoll matching
## character.gd's own real one is a much bigger undertaking than anything
## else in this file), just the whole skinned mesh+skeleton reparented into
## ONE tumbling RigidBody3D -- the same "coarsest ragdoll granularity
## available" call crimson_claw_monster.gd's own class comment already
## makes for a simpler enemy, just one rigid piece instead of three since
## this mesh was never split.

const MutantScene := preload("res://npc/assets/mutant.glb")

const GRAVITY := 9.8
const FLOOR_MAX_ANGLE_DEGREES := 80.0
const NpcSteering = preload("res://npc/npc_steering.gd")

const MAX_HEALTH := 120.0
const DETECTION_RANGE := 16.0

const LootComponent = preload("res://loot/loot_component.gd")
# A small, authored placeholder table (see loot/loot_component.gd's own
# class comment) -- no real item concept existed for any NPC before this
# pass (confirmed: neither this file nor wolf.gd/crimson_claw_monster.gd
# had ANY drop/loot table at all), so this is real infrastructure with
# minimal content for now, not a fleshed-out drop table.
const LOOT_TABLE := [
	{"item_id": "scrap_metal", "count_min": 1, "count_max": 3},
]
const CHASE_SPEED := 4.5
# 5x the mesh's own natural ~1.7m standing height (see _build_model()'s
# own MODEL_SCALE comment) -- matching how crimson_claw_monster.gd's own
# MODEL_SCALE scales its whole imported model in one place. Applied to the
# VISUAL model only; ATTACK_RANGE/ATTACK_HIT_RADIUS/the collision shape/the
# ragdoll collision box are all separately multiplied by this same factor
# below so a now-5x-bigger creature's actual reach/hitbox/collision keep
# matching what it visually looks like it should reach, rather than a giant
# body with a human-sized hitbox floating inside it. DETECTION_RANGE/
# CHASE_SPEED/ATTACK_DAMAGE deliberately do NOT scale with this -- same
# precedent sloth.gd's own class comment already sets (a 5x-scaled giant
# enemy elsewhere in this project that kept a comparable, not linearly
# 5x'd, detection range and actually a slightly SLOWER chase speed than the
# much smaller wolf.gd, as a "big and slow" balance choice, not a strict
# per-limb size multiplier).
const MODEL_SCALE := 5.0
# Slightly bigger than ATTACK_HIT_RADIUS (same gap crimson_claw_monster.gd's
# own ATTACK_RANGE/CLAW_HIT_RADIUS keep) -- an attack can trigger a hair
# outside real hit reach, so a target right at the edge can still back off
# mid-animation and genuinely avoid it. Both scaled by MODEL_SCALE (see its
# own comment) so the now-giant mutant's actual reach matches its visual size.
# Real reported bug: at the old 2.4/2.1 multipliers (12.0m/10.5m post-scale)
# the two were only 1.5m apart -- tiny relative to a 10.5m "hit reach," so
# once an attack started almost nothing short of actively retreating could
# avoid it, and since this creature never actually lunges during a swing
# (velocity.x/z are zeroed for the whole "attacking" state, see
# _physics_process()'s own attacking branch), a hit landing from 10.5m away
# read as damage with no real contact. Retuned to a genuinely close melee
# envelope instead: ATTACK_RANGE (6.0m) is roughly "he's right on top of
# you" for an ~8.5m-tall giant, and ATTACK_HIT_RADIUS (4.5m) leaves a real
# 1.5m/25% dodge margin during the windup rather than the old ~12%.
const ATTACK_RANGE := 1.2 * MODEL_SCALE
const ATTACK_HIT_RADIUS := 0.9 * MODEL_SCALE
const ATTACK_COOLDOWN_SECONDS := 1.8
const ATTACK_DAMAGE := 18.0
const JUMP_ATTACK_DAMAGE_MULT := 1.2 # 20% more damage than the other two

# 40% jump attack; the remaining 60% splits evenly (30%/30%) between axe
# spin and shout-swing -- see this file's own class comment.
const JUMP_ATTACK_CHANCE := 0.4

# Animation clip names, baked directly onto this mesh's own skeleton (see
# this file's own class comment) -- no library/path prefix needed, every
# clip lives in the AnimationPlayer's own default "" library.
const ANIM_IDLE := "idle"
const ANIM_RUN := "run"
const ANIM_AXE_SPIN := "axe_spin_attack"
const ANIM_SHOUT_SWING := "shout_swing"
const ANIM_JUMP_ATTACK := "jump_attack"

# How far into each attack clip (as a fraction of its own length) the hit
# actually lands -- picked per clip from watching each one's own actual
# motion (see the offline pipeline notes), not one shared guess, since the
# 3 clips have very different shapes and lengths (2.5s / 3.0s / 5.9s).
const AXE_SPIN_IMPACT_FRACTION := 0.5
# "at the beginning of shouting angrily it plays a swing downward" -- lands
# early in the (already-trimmed-to-3s) clip.
const SHOUT_SWING_IMPACT_FRACTION := 0.3
# The leap/strike itself is the climax right near the END of this long
# animation -- everything before it is the player's own "brief moment...
# to dodge or block" reaction window (see this file's own class comment).
const JUMP_ATTACK_IMPACT_FRACTION := 0.85

# Same ragdoll collision layer/damping character.gd's own real skeletal
# ragdoll and crimson_claw_monster.gd's own mesh-piece ragdoll both already
# use -- world-only collision mask, so the corpse settles without pushing
# the player or other bodies around.
const RAGDOLL_COLLISION_LAYER := 8
const RAGDOLL_GRAVITY_SCALE := 1.0
const RAGDOLL_LINEAR_DAMP := 0.4
const RAGDOLL_ANGULAR_DAMP := 0.4
const RAGDOLL_TOSS_SPEED := 1.6

# Meta key marking an Animation resource as already vertically-grounded
# (see _ground_animations()) -- these resources are shared/cached across
# every mutant instance (confirmed by precedent: _set_loop() already
# mutates the same shared resources on every spawn and relies on that
# mutation being idempotent), so without this guard a second mutant
# spawning would re-apply (and double) the same Y shift on top of the
# first spawn's already-corrected keyframes.
const ANIM_GROUND_META := &"mutant_ground_offset_applied"
# Same idempotency role as ANIM_GROUND_META, for _flatten_horizontal_root_motion()'s
# own X/Z fix instead.
const ANIM_FLATTEN_META := &"mutant_horizontal_flatten_applied"

var health := MAX_HEALTH
var is_dead := false

var _target: Node3D = null
## Steers this around buildings instead of into them -- see
## npc/npc_steering.gd.
var _steering := NpcSteering.new()
var _attack_cooldown_remaining := 0.0
# "idle" | "attacking" -- see _start_attack()/_update_attack().
var _attack_state := "idle"
var _attack_damage := 0.0
var _attack_timer := 0.0
var _attack_duration := 0.0
var _attack_impact_time := 0.0
var _attack_hit_landed := false

var _model: Node3D
var _facing_flip: Node3D
var _anim_player: AnimationPlayer

@onready var _collision: CollisionShape3D = $CollisionShape3D

func _ready() -> void:
	floor_max_angle = deg_to_rad(FLOOR_MAX_ANGLE_DEGREES)
	collision_layer = 4 # same "hostile NPC" layer wolf.gd/sloth.gd/crimson_claw_monster.gd all use
	collision_mask = 1 | 4
	# Lets player.gd's own lock-on camera (press C) find this as a valid
	# target alongside the "players" group -- see its own _find_lock_on_candidates.
	add_to_group("lockable_npcs")
	_build_model()
	# After _build_model: mutant.tscn ships an EMPTY CollisionShape3D and the
	# box is assigned in there, so configuring any earlier would measure
	# nothing and silently leave this 5x creature probing at human scale.
	_steering.configure_from(self)

func _physics_process(delta: float) -> void:
	if is_dead:
		return

	if not is_on_floor():
		velocity.y -= GRAVITY * delta
	else:
		velocity.y = 0.0

	_attack_cooldown_remaining = maxf(0.0, _attack_cooldown_remaining - delta)

	# Re-acquired every frame rather than trusting a stale pointer -- same
	# reasoning wolf.gd's own _physics_process already documents.
	if _target == null or not is_instance_valid(_target) or bool(_target.get("is_dead")):
		_target = _find_nearest_player()

	if _attack_state == "attacking":
		_update_attack(delta)
		# Still allowed to turn and face the target mid-attack, but no new
		# movement/attack decisions -- same "committed to the current
		# action" shape crimson_claw_monster.gd's own multi-phase swing
		# already establishes.
		if _target != null:
			var to_target_facing: Vector3 = _target.global_position - global_position
			to_target_facing.y = 0.0
			_face(to_target_facing)
		velocity.x = 0.0
		velocity.z = 0.0
		move_and_slide()
		return

	if _target != null:
		var to_target: Vector3 = _target.global_position - global_position
		to_target.y = 0.0
		var distance := to_target.length()
		if distance > DETECTION_RANGE:
			_target = null
			velocity.x = 0.0
			velocity.z = 0.0
		elif distance > ATTACK_RANGE:
			# Steered around buildings rather than into them, and faced
			# along the direction actually travelled -- see
			# npc/npc_steering.gd.
			var direction := _steering.steer(self, to_target / distance, delta)
			_face(direction)
			velocity.x = direction.x * CHASE_SPEED
			velocity.z = direction.z * CHASE_SPEED
		else:
			velocity.x = 0.0
			velocity.z = 0.0
			_face(to_target)
			if _attack_cooldown_remaining <= 0.0:
				_start_attack()
	else:
		velocity.x = 0.0
		velocity.z = 0.0

	move_and_slide()
	_update_locomotion_animation()

## Same duck-typed target-acquisition wolf.gd's own _find_nearest_player()
## already establishes.
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

## Plays idle/run depending on whether this frame's own move_and_slide()
## actually produced horizontal motion -- skipped entirely while attacking
## (that state owns _anim_player until its own clip finishes, see
## _update_attack()).
func _update_locomotion_animation() -> void:
	# Real bug this guard fixes: _physics_process() calls this every frame
	# AFTER its own movement/attack branch, including the exact frame an
	# attack just started on (the "attacking" early-return at the top of
	# _physics_process only skips this call on SUBSEQUENT frames, once
	# _attack_state is already set -- not on the frame it FIRST becomes
	# "attacking" later in that same call). Without this check, that first
	# frame's own velocity.x/z == 0 (just zeroed by the attack branch) read
	# as "not moving" and immediately stomped the attack clip _start_attack()
	# had just started back to idle.
	if _attack_state == "attacking":
		return
	var moving := Vector2(velocity.x, velocity.z).length() > 0.1
	var target_anim := ANIM_RUN if moving else ANIM_IDLE
	if _anim_player.current_animation != target_anim:
		_anim_player.play(target_anim, 0.2)

## Rolls the weighted 3-way attack choice (see JUMP_ATTACK_CHANCE's own
## comment), plays that clip once, and records this attack's own damage/
## impact timing for _update_attack() to act on as the clip actually plays
## out.
func _start_attack() -> void:
	_attack_cooldown_remaining = ATTACK_COOLDOWN_SECONDS
	_attack_state = "attacking"
	_attack_timer = 0.0
	_attack_hit_landed = false

	var anim_name: String
	var impact_fraction: float
	var roll := randf()
	if roll < JUMP_ATTACK_CHANCE:
		anim_name = ANIM_JUMP_ATTACK
		_attack_damage = ATTACK_DAMAGE * JUMP_ATTACK_DAMAGE_MULT
		impact_fraction = JUMP_ATTACK_IMPACT_FRACTION
	elif roll < JUMP_ATTACK_CHANCE + (1.0 - JUMP_ATTACK_CHANCE) * 0.5:
		anim_name = ANIM_AXE_SPIN
		_attack_damage = ATTACK_DAMAGE
		impact_fraction = AXE_SPIN_IMPACT_FRACTION
	else:
		anim_name = ANIM_SHOUT_SWING
		_attack_damage = ATTACK_DAMAGE
		impact_fraction = SHOUT_SWING_IMPACT_FRACTION

	_anim_player.play(anim_name, 0.15)
	_attack_duration = _anim_player.current_animation_length
	_attack_impact_time = _attack_duration * impact_fraction

func _update_attack(delta: float) -> void:
	_attack_timer += delta
	if not _attack_hit_landed and _attack_timer >= _attack_impact_time:
		_check_attack_hit()
	if _attack_timer >= _attack_duration:
		_attack_state = "idle"

## A single check at this attack's own impact instant, not a continuous
## per-frame window (see this file's own class comment on why one check is
## enough for a real baked animation's own single obvious impact moment).
## Only ever lands ONE hit per attack (_attack_hit_landed is set here
## regardless of whether the check actually connects), so a target that
## drifts back into range a moment later this same attack can't get hit
## twice.
func _check_attack_hit() -> void:
	_attack_hit_landed = true
	if _target == null or not is_instance_valid(_target):
		return
	var to_target: Vector3 = _target.global_position - global_position
	to_target.y = 0.0
	if to_target.length() <= ATTACK_HIT_RADIUS and _target.has_method("take_damage"):
		_target.take_damage(_attack_damage, self)

## hitstun_seconds/is_finisher exist purely so player.gd's own _punch() can
## call this with the same 4-arg signature it uses on another Player -- same
## no-op duck-typing wolf.gd/crimson_claw_monster.gd's own take_damage()
## already documents.
func take_damage(amount: float, attacker: Node = null, _hitstun_seconds: float = 0.0, _is_finisher: bool = false) -> void:
	if is_dead or amount <= 0.0:
		return
	health = maxf(0.0, health - amount)
	if health <= 0.0:
		_die(attacker)

func _die(_attacker: Node = null) -> void:
	is_dead = true
	velocity = Vector3.ZERO
	_target = null
	_attack_state = "idle"
	_collision.disabled = true
	_anim_player.stop() # freezes on whatever pose was showing at the moment of death
	_spawn_ragdoll()

## Detaches the WHOLE model (mesh+skeleton, under _model) into one
## independent tumbling RigidBody3D -- see this file's own class comment on
## why this is the coarsest ragdoll granularity, not a per-bone one.
## Node.reparent()'s own keep_global_transform (the default) is what keeps
## the model in its exact current world pose (mid-attack pose included)
## with no manual transform math needed.
func _spawn_ragdoll() -> void:
	var body := RigidBody3D.new()
	body.collision_layer = RAGDOLL_COLLISION_LAYER
	body.collision_mask = 1
	body.gravity_scale = RAGDOLL_GRAVITY_SCALE
	body.linear_damp = RAGDOLL_LINEAR_DAMP
	body.angular_damp = RAGDOLL_ANGULAR_DAMP
	get_tree().current_scene.add_child(body)
	# Scale stripped from the body's own transform before applying it here --
	# same reasoning crimson_claw_monster.gd's own _make_ragdoll_body already
	# documents (a RigidBody3D discards any scale baked into its OWN
	# transform every physics step); this body is never scaled today, but
	# matching the same defensive pattern costs nothing and stays correct if
	# that ever changes.
	body.global_transform = Transform3D(Basis(global_transform.basis.get_rotation_quaternion()), global_transform.origin)
	body.linear_velocity = Vector3(randf_range(-1.0, 1.0), randf_range(0.6, 1.4), randf_range(-1.0, 1.0)) * RAGDOLL_TOSS_SPEED
	body.angular_velocity = Vector3(randf_range(-3.0, 3.0), randf_range(-3.0, 3.0), randf_range(-3.0, 3.0))
	# See player.gd's own grab/carry system (Skyrim-style hold-E-to-lift) --
	# "grabbable" is the tag its raycast targeting checks for. Local-only
	# (no owner-peer metadata the way character.gd's own per-bone ragdoll
	# gets, see its own comment) -- NPCs aren't networked at all today, so
	# there's nothing to sync a carry FOR on another peer's screen yet.
	body.add_to_group("grabbable")
	var bounce_mat := PhysicsMaterial.new()
	bounce_mat.bounce = 0.25
	bounce_mat.friction = 0.7
	body.physics_material_override = bounce_mat

	# _model already carries MODEL_SCALE baked into its own local scale (see
	# _build_model()) -- reparenting it under this scale-free body (see the
	# scale-stripping above) is what BAKES that same compensating scale into
	# _model's own local transform post-reparent, same mechanism
	# crimson_claw_monster.gd's own _make_ragdoll_body comment documents,
	# so the corpse keeps rendering at full 5x size instead of visibly
	# snapping down to 1x the instant physics starts simulating this body.
	_model.reparent(body, true)

	# Scaled by MODEL_SCALE same as the live collision shape below (see its
	# own comment) -- this box is a child of the new scale-free `body`, not
	# of the already-5x-scaled _model, so it needs the multiplication
	# applied explicitly rather than inheriting it for free.
	var shape := BoxShape3D.new()
	shape.size = Vector3(0.8, 1.7, 0.9) * MODEL_SCALE # roughly the mesh's own standing AABB
	var collision := CollisionShape3D.new()
	collision.shape = shape
	collision.position = Vector3(0, 0.85 * MODEL_SCALE, 0) # mesh's own local origin sits at the feet, not centered
	body.add_child(collision)

	# See loot/loot_component.gd's own class comment -- a real reported ask
	# ("press E to loot a enemy... corpse"). No inventory/equipment concept
	# exists for NPCs (never did), so this is purely LOOT_TABLE's own
	# authored placeholder content, not a snapshot of anything the mutant
	# was "wearing."
	var loot: Node3D = LootComponent.new()
	var loot_items: Array[Dictionary] = []
	for entry in LOOT_TABLE:
		loot_items.append({"item_id": entry["item_id"], "count": randi_range(entry["count_min"], entry["count_max"])})
	loot.loot_items = loot_items
	body.add_child(loot)

func _build_model() -> void:
	_model = Node3D.new()
	add_child(_model)
	# 5x the mesh's own natural size (see MODEL_SCALE's own comment) -- same
	# "scale the whole imported model in one place" pattern
	# crimson_claw_monster.gd's own _build_model() already uses.
	_model.scale = Vector3.ONE * MODEL_SCALE

	# See this file's own class comment on why the mesh's own +Z-facing
	# geometry needs this 180-degree correction.
	_facing_flip = Node3D.new()
	_facing_flip.rotation_degrees.y = 180.0
	_model.add_child(_facing_flip)

	var imported: Node3D = MutantScene.instantiate()
	_facing_flip.add_child(imported)

	_anim_player = _find_animation_player(imported)
	_ground_animations()
	_flatten_horizontal_root_motion()
	_set_loop(ANIM_IDLE, true)
	_set_loop(ANIM_RUN, true)
	_anim_player.play(ANIM_IDLE)

	# The mesh's own local origin sits at the feet (confirmed directly via
	# its own AABB: Y from ~0 to ~1.7), matching every other ground-walking
	# CharacterBody3D's own convention (wolf.gd's own capsule likewise
	# starts from the ground up) -- roughly its own standing AABB footprint,
	# same "close enough, not pixel-matched to the mesh" call wolf.gd/
	# crimson_claw_monster.gd's own collision shapes already make. This
	# shape is a child of the CharacterBody3D directly, not of the already-
	# 5x-scaled _model, so MODEL_SCALE is applied explicitly here too --
	# without it, a now-giant visual body would still only have a
	# human-sized hitbox floating inside it.
	var shape := BoxShape3D.new()
	shape.size = Vector3(0.75, 1.7, 0.85) * MODEL_SCALE
	_collision.shape = shape
	_collision.position = Vector3(0, 0.85 * MODEL_SCALE, 0)

static func _find_animation_player(node: Node) -> AnimationPlayer:
	if node is AnimationPlayer:
		return node
	for child in node.get_children():
		var found := _find_animation_player(child)
		if found != null:
			return found
	return null

## idle/run loop (a locomotion cycle plays forever until the state machine
## picks something else); every attack clip stays one-shot (LOOP_NONE, the
## default straight out of import) so _update_attack() can rely on it
## actually finishing after _attack_duration seconds.
func _set_loop(anim_name: String, enabled: bool) -> void:
	for lib_name in _anim_player.get_animation_library_list():
		var lib := _anim_player.get_animation_library(lib_name)
		if lib.has_animation(anim_name):
			var anim := lib.get_animation(anim_name)
			anim.loop_mode = Animation.LOOP_LINEAR if enabled else Animation.LOOP_NONE

## Real reported bug: Meshy baked each clip's own Hips bone with a
## DIFFERENT baseline height, even though every clip nominally shares the
## same skeleton/rest pose -- confirmed directly by dumping each clip's own
## Hips position-track keyframes (idle's own lowest point sits at raw
## Y~68.9; run~74.7; axe_spin_attack~72.2; shout_swing~79.4 -- up to ~10.5
## raw units apart, which becomes ~52cm once divided by the glTF import's
## own /100 unit conversion and multiplied back up by MODEL_SCALE=5).
## Since this CharacterBody3D never applies root motion (see this file's
## own class comment -- movement is 100% code-driven), nothing else
## compensates for that mismatch: switching between clips visibly shifted
## the whole model up or down relative to the actual, correctly-grounded,
## physics-driven collision floor underneath it -- reported directly as
## "some of the mutant's animations leave him hovering above the ground
## slightly". Fixed by shifting each clip's own Hips Y keyframes by a
## constant per-clip delta so that clip's OWN lowest point (its most
## solidly-grounded instant) lands exactly at idle's own lowest point -- a
## constant shift changes nothing about a clip's internal motion SHAPE (a
## run's own stride bob, jump_attack's own real leap height above its own
## crouch), only its baseline, so this is independent of (and doesn't
## disturb) the earlier horizontal-drift fix on these same clips.
func _ground_animations() -> void:
	var hips_track_path := ""
	for lib_name in _anim_player.get_animation_library_list():
		var lib := _anim_player.get_animation_library(lib_name)
		for anim_name in lib.get_animation_list():
			var anim: Animation = lib.get_animation(anim_name)
			for i in range(anim.get_track_count()):
				if anim.track_get_type(i) == Animation.TYPE_POSITION_3D and str(anim.track_get_path(i)).findn("hips") != -1:
					hips_track_path = str(anim.track_get_path(i))
					break
			if hips_track_path != "":
				break
		if hips_track_path != "":
			break
	if hips_track_path == "":
		return # no Hips position track found at all -- nothing to reground

	var idle_min := _clip_hips_min_y(hips_track_path, ANIM_IDLE)
	if is_nan(idle_min):
		return

	for lib_name in _anim_player.get_animation_library_list():
		var lib := _anim_player.get_animation_library(lib_name)
		for anim_name in lib.get_animation_list():
			var anim: Animation = lib.get_animation(anim_name)
			if anim.has_meta(ANIM_GROUND_META):
				continue
			anim.set_meta(ANIM_GROUND_META, true)
			var track_idx := _find_hips_track(anim, hips_track_path)
			if track_idx == -1:
				continue
			var key_count := anim.track_get_key_count(track_idx)
			if key_count == 0:
				continue
			var clip_min := INF
			for k in range(key_count):
				clip_min = minf(clip_min, (anim.track_get_key_value(track_idx, k) as Vector3).y)
			var delta := idle_min - clip_min
			if absf(delta) < 0.01:
				continue
			for k in range(key_count):
				var v: Vector3 = anim.track_get_key_value(track_idx, k)
				v.y += delta
				anim.track_set_key_value(track_idx, k, v)

## The original horizontal-drift fix (see this file's own class comment on
## the offline pipeline that baked it directly into mutant.glb) held axe_spin_
## attack/shout_swing's own Hips X/Z essentially flat (confirmed directly,
## a regression test measuring their own span comes back at ~2-3 raw units,
## negligible skinning wiggle) but left jump_attack with a real, un-flattened
## ~27.7-unit horizontal span -- a real regression this test previously
## couldn't catch (this whole test script failed to even PARSE for an
## unrelated reason until that got fixed, so this had never actually been
## re-verified since the original offline fix). Rather than re-touch the
## Blender pipeline a second time, the SAME hold-constant idea is applied
## here at runtime instead (this file already proved the technique safe and
## correct for the Y axis, see _ground_animations() above): every attack
## clip's own Hips X/Z is pinned to its own FIRST keyframe's value for the
## whole clip, leaving Y (the real jump height, already verified elsewhere)
## completely untouched.
func _flatten_horizontal_root_motion() -> void:
	var attack_anims := [ANIM_AXE_SPIN, ANIM_SHOUT_SWING, ANIM_JUMP_ATTACK]
	for lib_name in _anim_player.get_animation_library_list():
		var lib := _anim_player.get_animation_library(lib_name)
		for anim_name in attack_anims:
			if not lib.has_animation(anim_name):
				continue
			var anim: Animation = lib.get_animation(anim_name)
			if anim.has_meta(ANIM_FLATTEN_META):
				continue
			anim.set_meta(ANIM_FLATTEN_META, true)
			for i in range(anim.get_track_count()):
				if anim.track_get_type(i) != Animation.TYPE_POSITION_3D or str(anim.track_get_path(i)).findn("hips") == -1:
					continue
				var key_count := anim.track_get_key_count(i)
				if key_count == 0:
					continue
				var first: Vector3 = anim.track_get_key_value(i, 0)
				for k in range(key_count):
					var v: Vector3 = anim.track_get_key_value(i, k)
					v.x = first.x
					v.z = first.z
					anim.track_set_key_value(i, k, v)

func _clip_hips_min_y(track_path: String, anim_name: String) -> float:
	for lib_name in _anim_player.get_animation_library_list():
		var lib := _anim_player.get_animation_library(lib_name)
		if lib.has_animation(anim_name):
			var anim := lib.get_animation(anim_name)
			var track_idx := _find_hips_track(anim, track_path)
			if track_idx == -1:
				return NAN
			var key_count := anim.track_get_key_count(track_idx)
			if key_count == 0:
				return NAN
			var m := INF
			for k in range(key_count):
				m = minf(m, (anim.track_get_key_value(track_idx, k) as Vector3).y)
			return m
	return NAN

static func _find_hips_track(anim: Animation, track_path: String) -> int:
	for i in range(anim.get_track_count()):
		if anim.track_get_type(i) == Animation.TYPE_POSITION_3D and str(anim.track_get_path(i)) == track_path:
			return i
	return -1
