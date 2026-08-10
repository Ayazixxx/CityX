extends CharacterBody3D
## Hostile "Crimson Claw Monster" enemy -- spawned via the dev console's own
## "/spawn crimsonclaw" (see ui/dev_console.gd). Unlike wolf.gd/sloth.gd
## (fully procedural bodies built from primitive meshes), this one's visual
## body is an IMPORTED mesh (npc/crimson_claw_monster.glb -- a decimated,
## AI-generated head-and-claws creature with no legs and no rig/skeleton at
## all), extracted from the original Meshy AI OBJ via an offline Blender
## pass. The .glb itself carries THREE separate pieces (named "Body",
## "LeftClaw", "RightClaw" -- a crude region-based split done offline, not a
## precise topological one, see the pipeline notes), which is what lets the
## claws swing independently below despite there being no real
## skeleton/rig -- each claw mesh is reparented under its own pivot Node3D
## (positioned at that claw's own approximate wrist, found the same
## bbox-based way) and just that PIVOT gets rotated for the swing.
##
## No walk-cycle animation (still explicitly out of scope) -- it HOVERS (no
## legs to stand on, so gravity/floor-snapping is skipped entirely, unlike
## every other ground-bound NPC in this project) and glides toward its
## target, same straight-line-chase-then-attack shape wolf.gd/sloth.gd
## already established. The ATTACK itself, unlike wolf.gd's own instant-
## damage-on-cooldown bite, is a real multi-phase swing (windup/swing/
## recover) that only damages the target if it's still within melee reach
## DURING the swing phase specifically (see CLAW_HIT_RADIUS/_check_claw_hit)
## -- a genuine hit check, not a bare "attack triggered = guaranteed
## damage" gate. This deliberately checks distance-to-BODY rather than
## tracking each claw's own exact swept tip position: an earlier version
## that computed the live claw-tip world position via the pivot's own
## global_transform turned out to be fragile (the composed rotation from
## this body's own look_at-driven facing, combined with the mesh's fixed
## 180-degree _facing_flip correction -- see _facing_flip's own comment --
## was hard to get exactly right, and a directly-in-front test case still
## came back several units short of the target). Checking "are you within
## reach of the CREATURE" during the animated swing window keeps the real
## gate (a target that stays out of range, or the swing whiffing because
## the target moved away mid-animation, still takes no damage) without
## depending on that fragile per-claw transform math.
##
## Same duck-typed take_damage()/health/death interface every other
## damageable thing in this project already uses, so the player can fight
## it back completely unmodified.
##
## Claw pivots (CLAW pivot/tip constants) were found by an automated bbox
## scan of the source mesh, not eyeballed -- see the offline pipeline notes.
## Eye positions (EYE_L/EYE_R) were ORIGINALLY found the same way (an
## automated vertex-concavity scan), but that scan turned out to have
## latched onto the gum line instead of the real eye socket -- confirmed
## directly from a real in-game screenshot showing the glow embedded among
## the jaw's own teeth/spikes. Re-derived instead via a targeted raycast
## scan directly against this mesh's real geometry (see EYE_L/EYE_R's own
## comment). All in the imported mesh's own RAW local space, which
## (confirmed both algebraically and by rendering markers at these exact
## coordinates against the real geometry) is identical to the exported
## .glb's own coordinate space with no axis conversion needed.

const MonsterScene := preload("res://npc/crimson_claw_monster.glb")

const AttackSound := preload("res://npc/assets/audio/claw_attack.mp3")
const DeathSound := preload("res://npc/assets/audio/claw_death.mp3")
# 50/50 picked (see _play_random_growl) both when a target is first
# noticed AND on the repeating NOTICE_GROWL_INTERVAL_SECONDS timer after
# that -- one pool, two different trigger points, per the user's own
# wording ("upon noticing... after that the 50/50 rule continues").
const NoticeSounds := [
	preload("res://npc/assets/audio/claw_noise_1.mp3"),
	preload("res://npc/assets/audio/claw_noise_2.mp3"),
]
const NOTICE_GROWL_INTERVAL_SECONDS := 5.0

const NpcSteering = preload("res://npc/npc_steering.gd")

const GRAVITY := 9.8
const HOVER_HEIGHT := 1.5 # world Y this body's own origin holds itself at -- no legs, no floor-snapping (see this file's own class comment)
const HOVER_CORRECTION_SPEED := 6.0 # how fast vertical drift (e.g. after a knockback) eases back to HOVER_HEIGHT

const MAX_HEALTH := 150.0
const DETECTION_RANGE := 16.0

const LootComponent = preload("res://loot/loot_component.gd")
# A small, authored placeholder table -- see npc/mutant.gd's own matching
# LOOT_TABLE comment for why this is real infrastructure with minimal
# content, not a fleshed-out drop table.
const LOOT_TABLE := [
	{"item_id": "scrap_metal", "count_min": 1, "count_max": 3},
]
const CHASE_SPEED := 5.0
const ATTACK_RANGE := 3.0 # a bit past CLAW_HIT_RADIUS -- the attack can trigger slightly out of real hit reach, so a target right at the edge can still duck away mid-swing and genuinely avoid it
const ATTACK_COOLDOWN_SECONDS := 1.6
const ATTACK_DAMAGE := 15.0

# Attack phases -- see _physics_process's own attack-state handling and
# _start_attack()/_update_attack().
const WINDUP_SECONDS := 0.18
const SWING_SECONDS := 0.22
const RECOVER_SECONDS := 0.28
const WINDUP_ANGLE_DEGREES := 35.0 # claws pull back/apart before swinging in
const SWING_ANGLE_DEGREES := 75.0 # then sweep this far the OTHER way, crossing in front -- a double inward rake
# How close the TARGET must be to this body during the swing phase to
# actually land a hit (see this file's own class comment on why this is a
# distance-to-body check, not a per-claw swept-tip check). Comfortably
# covers a target standing at melee range while still leaving a real gap
# below ATTACK_RANGE for someone at the very edge to back off mid-swing.
# Raised alongside the collision box's own no-longer-shrunk size (see
# _build_model's own box-shape comment) -- move_and_slide now stops this
# body's own center noticeably farther from a target's own capsule than
# the OLD, undersized box ever did (roughly box half-depth 2.0m + the
# player's own capsule radius 0.4m), so the old 2.1 was already tighter
# than the body could ever physically close to, silently failing every
# landed hit. Same "tighten to the real melee-contact distance" fix
# mutant.gd's own ATTACK_RANGE/ATTACK_HIT_RADIUS already went through.
const CLAW_HIT_RADIUS := 2.5

const MODEL_SCALE := 2.0
const BODY_COLOR := Color(0.94, 0.94, 0.92)
const EYE_COLOR := Color(1.0, 0.05, 0.0)
const EYE_LIGHT_ENERGY := 3.5
const EYE_LIGHT_RANGE := 0.6

# Hand-tuned via /crimsonclaw pose + Godot's own Remote Scene Tree debugger
# (see print_eye_pose()'s own comment for that workflow) -- position,
# SCALE, and tilt are all tracked separately per eye now (not one shared
## EYE_SCALE/EYE_TILT_DEGREES mirrored by sign the way an earlier,
# guessed-blind version assumed) since a real hands-on pass landed on two
# genuinely asymmetric eyes, not a mirror-symmetric pair -- confirmed
# directly, the measured scale and tilt for each side don't match each
# other at all. Positions replace the file's own earlier offline-scan
# values (see the removed comment history below for why those needed
# correcting more than once) with real, live-verified ones instead.
const EYE_L := Vector3(-0.479992, 0.204, 0.233995)
const EYE_R := Vector3(0.32999, 0.274, 0.263994)
const EYE_L_SCALE := Vector3(0.269994, 0.647987, 0.432)
const EYE_R_SCALE := Vector3(0.329993, 0.791986, 0.539998)
const EYE_L_TILT_DEGREES := -5.80000112900984
const EYE_R_TILT_DEGREES := 148.599890526359

# Re-derived via connected-component (mesh-island) analysis of the source
# mesh, not the ORIGINAL region/bbox-threshold split -- confirmed directly
# (a real in-game screenshot) that the old X/Z-threshold split was catching
# stray mane-spike and jaw geometry as part of the claws, so those pieces
# visibly flew off with the claw during a swing instead of just the hand.
# The two claws turned out to be genuinely separate, disconnected mesh
# islands from the head/body (confirmed via a vertex-adjacency scan: exactly
# 3 connected components in the whole source mesh), so a precise split by
# connectivity -- rather than any spatial threshold at all -- captures
# EXACTLY the claw geometry with no risk of stray inclusion. Pivot itself is
# still the same heuristic as before (the point on each claw's own bbox
# closest to the body, approximating the wrist), just computed against the
# now-correct claw-only geometry. Purely visual (see this file's own class
# comment on why the hit check itself doesn't track a per-claw tip
# position).
const CLAW_PIVOT_L := Vector3(-0.3162, -0.5586, -0.2854)
const CLAW_PIVOT_R := Vector3(0.2593, -0.6106, -0.3043)

# Death ragdoll -- no skeleton at all (see this file's own class comment),
# so "ragdoll" here means detaching the 3 already-separate mesh pieces
# (Body/LeftClaw/RightClaw) into their own independent RigidBody3D debris,
# each keeping its current world pose/orientation at the moment of death
# (see _spawn_ragdoll_piece), rather than the old single scripted tip-and-
# sink tween -- see _die()'s own comment for why that read as clipping
# through the floor. Same ragdoll collision layer character.gd's own real
# skeletal ragdoll already established (RAGDOLL_COLLISION_LAYER=8) --
# world-only collision mask, so debris pieces settle on the ground without
# pushing the player or each other around.
const RAGDOLL_COLLISION_LAYER := 8
const RAGDOLL_GRAVITY_SCALE := 1.0
const RAGDOLL_LINEAR_DAMP := 0.4
const RAGDOLL_ANGULAR_DAMP := 0.4
# A small random toss/tumble rather than a limp drop straight down -- reads
# as a genuine collapse, not a puppet with its strings cut standing dead
# still for a frame first.
const RAGDOLL_TOSS_SPEED := 1.8
const EYE_FADE_SECONDS := 1.5

var health := MAX_HEALTH
var is_dead := false

var _target: Node3D = null
## Steers this around buildings instead of into them -- see
## npc/npc_steering.gd.
var _steering := NpcSteering.new()
var _attack_cooldown_remaining := 0.0
# "idle", "windup", "swing", "recover" -- see _start_attack()/_update_attack().
var _attack_state := "idle"
var _attack_phase_timer := 0.0
var _attack_hit_landed := false
# Alternates every completed attack (see _update_attack's own "recover"
# case) -- which hand throws the NEXT attack. _active_claw_pivot is set once
# per attack, at _start_attack() time, and read by every phase after that so
# windup/swing/recover always animate the SAME single hand for that whole
# attack rather than re-deriving "which hand" mid-sequence.
var _next_attack_hand_is_left := true
var _active_claw_pivot: Node3D = null

# Notice/idle growl -- see _play_random_growl()'s own comment.
var _had_target_last_frame := false
var _growl_timer_remaining := 0.0

var _model: Node3D
# The mesh's own front faces +Z in its raw coordinate space (confirmed via
# the same offline render analysis that located the eyes) -- opposite of
# this project's own "-Z is forward" convention every look_at()-oriented
# body already assumes (see wearables/hockey_mask.gd's own class comment).
# A 180-degree yaw on this one child container re-expresses the mesh's own
# +Z-facing geometry (AND the eye/claw-pivot positions, which ride along
# for free since they're parented under the same flip) as -Z-facing,
# without needing to hand-negate any coordinate.
var _facing_flip: Node3D
var _left_claw_pivot: Node3D
var _right_claw_pivot: Node3D
# Stored so _die()'s own ragdoll/fade logic can reach them directly instead
# of re-searching the tree -- the body mesh and both eye meshes/lights all
# need to be reparented onto (or, for the eyes, ride along with) the new
# "head" ragdoll piece at death (see _spawn_ragdoll_pieces/_fade_eye).
var _body_inst: MeshInstance3D
var _eye_mesh_l: MeshInstance3D
var _eye_mesh_r: MeshInstance3D
var _eye_light_l: OmniLight3D
var _eye_light_r: OmniLight3D

var _growl_player: AudioStreamPlayer3D
var _attack_player: AudioStreamPlayer3D
var _death_player: AudioStreamPlayer3D

@onready var _collision: CollisionShape3D = $CollisionShape3D

func _ready() -> void:
	collision_layer = 4 # same "hostile NPC" layer wolf.gd/sloth.gd/cpu.gd all use
	collision_mask = 1 | 4
	# Lets player.gd's own lock-on camera (press C) find this as a valid
	# target alongside the "players" group -- see its own _find_lock_on_candidates.
	add_to_group("lockable_npcs")
	_build_model()
	# After _build_model: crimson_claw_monster.tscn ships an EMPTY
	# CollisionShape3D and the box is assigned in there, so configuring any
	# earlier would measure nothing.
	_steering.configure_from(self)
	_build_audio_players()
	global_position.y = HOVER_HEIGHT

## Three persistent, reused players (not the disposable self-freeing
## pattern weapons/thrown_bottle.gd's own one-shot impact sound uses) --
## this monster is long-lived, so a fresh AudioStreamPlayer3D per bark would
## just leak nodes. Separate players per sound ROLE (not one shared player)
## so an attack bark can't cut off an in-flight notice growl or vice versa --
## same "one dedicated player per concurrent sound role" convention
## player.tscn's own WalkSoundPlayer/RunSoundPlayer/PunchImpactPlayer/
## DashPlayer split already establishes.
func _build_audio_players() -> void:
	_growl_player = AudioStreamPlayer3D.new()
	add_child(_growl_player)
	_attack_player = AudioStreamPlayer3D.new()
	add_child(_attack_player)
	_death_player = AudioStreamPlayer3D.new()
	add_child(_death_player)

func _physics_process(delta: float) -> void:
	if is_dead:
		return

	# No gravity/floor-snapping (see this file's own class comment) -- just
	# ease any vertical drift back to the hover height every frame.
	velocity.y = (HOVER_HEIGHT - global_position.y) * HOVER_CORRECTION_SPEED

	_attack_cooldown_remaining = maxf(0.0, _attack_cooldown_remaining - delta)

	# Re-acquired every frame rather than trusting a stale pointer -- same
	# reasoning wolf.gd's own _physics_process already documents.
	if _target == null or not is_instance_valid(_target) or bool(_target.get("is_dead")):
		_target = _find_nearest_player()

	# Notice growl (immediate, on the null->target transition) then a
	# repeating 50/50 growl every NOTICE_GROWL_INTERVAL_SECONDS for as long
	# as a target stays acquired -- placed here, BEFORE the attack-state
	# early return below, so the timer keeps ticking (and can still fire)
	# through an attack instead of pausing for its duration.
	var has_target_now := _target != null
	if has_target_now and not _had_target_last_frame:
		_play_random_growl()
		_growl_timer_remaining = NOTICE_GROWL_INTERVAL_SECONDS
	elif has_target_now:
		_growl_timer_remaining -= delta
		if _growl_timer_remaining <= 0.0:
			_play_random_growl()
			_growl_timer_remaining = NOTICE_GROWL_INTERVAL_SECONDS
	_had_target_last_frame = has_target_now

	if _attack_state != "idle":
		_update_attack(delta)
		# Still allowed to drift toward HOVER_HEIGHT and turn to face the
		# target while mid-swing, but no new movement/attack decisions --
		# same "committed to the current action" shape wolf.gd's own bite
		# (implicitly, via its cooldown) already has, just explicit here
		# since this attack spans several real frames instead of one.
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

## Picks one of NoticeSounds at random and plays it on _growl_player --
## called both on first noticing a target and on the repeating
## NOTICE_GROWL_INTERVAL_SECONDS timer after that (see _physics_process's
## own growl-timer block), so a fresh 50/50 roll happens independently each
## time rather than committing to one sound for the whole encounter.
func _play_random_growl() -> void:
	_growl_player.stream = NoticeSounds[randi() % NoticeSounds.size()]
	_growl_player.play()

## Begins the windup phase -- ONLY the current hand's own claw pivot
## (_next_attack_hand_is_left picks which one, see this var's own comment)
## tweens out to WINDUP_ANGLE_DEGREES; the other stays at rest. Sign
## convention (left pulls to -WINDUP, right to +WINDUP) matches exactly
## what the old always-both-hands version used for each side, just applied
## to whichever ONE pivot is active this attack. _update_attack() carries
## the state machine through swing/recover on its own each frame, still
## only ever touching _active_claw_pivot.
func _start_attack() -> void:
	_attack_cooldown_remaining = ATTACK_COOLDOWN_SECONDS
	_attack_state = "windup"
	_attack_phase_timer = WINDUP_SECONDS
	_attack_hit_landed = false
	_active_claw_pivot = _left_claw_pivot if _next_attack_hand_is_left else _right_claw_pivot
	var windup_sign := -1.0 if _next_attack_hand_is_left else 1.0
	_attack_player.stream = AttackSound
	_attack_player.play()
	var tw := create_tween()
	tw.tween_property(_active_claw_pivot, "rotation:y", deg_to_rad(WINDUP_ANGLE_DEGREES * windup_sign), WINDUP_SECONDS).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_OUT)

func _update_attack(delta: float) -> void:
	_attack_phase_timer -= delta
	if _attack_phase_timer > 0.0:
		if _attack_state == "swing":
			_check_claw_hit()
		return

	# Same left/right sign convention _start_attack()'s own windup uses,
	# just for the swing's OPPOSITE-direction sweep (see WINDUP/
	# SWING_ANGLE_DEGREES's own comment on why it crosses inward).
	var swing_sign := 1.0 if _next_attack_hand_is_left else -1.0
	match _attack_state:
		"windup":
			_attack_state = "swing"
			_attack_phase_timer = SWING_SECONDS
			var tw := create_tween()
			tw.tween_property(_active_claw_pivot, "rotation:y", deg_to_rad(SWING_ANGLE_DEGREES * swing_sign), SWING_SECONDS).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
		"swing":
			_attack_state = "recover"
			_attack_phase_timer = RECOVER_SECONDS
			var tw := create_tween()
			tw.tween_property(_active_claw_pivot, "rotation:y", 0.0, RECOVER_SECONDS).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
		"recover":
			_attack_state = "idle"
			# The NEXT attack swings the other hand -- see _next_attack_hand_is_left's own comment.
			_next_attack_hand_is_left = not _next_attack_hand_is_left

## Real hit check, not a guaranteed on-trigger hit: each frame during the
## swing phase, checks whether the target is still within CLAW_HIT_RADIUS of
## this body (see this file's own class comment on why that's a
## distance-to-body check, not a per-claw swept-tip check). Horizontal-only,
## same as the ATTACK_RANGE/chase distance check above (to_target.y = 0.0) --
## this body hovers at a fixed height regardless of the target's own height,
## so the vertical gap to a grounded target has nothing to do with claw
## reach and must not gate the hit the same way it doesn't gate triggering
## the attack in the first place. Only ever lands ONE hit per attack
## (_attack_hit_landed), so staying close for several frames in a row can't
## multi-hit off a single swing.
func _check_claw_hit() -> void:
	if _attack_hit_landed or _target == null or not is_instance_valid(_target):
		return
	var to_target: Vector3 = _target.global_position - global_position
	to_target.y = 0.0
	if to_target.length() <= CLAW_HIT_RADIUS:
		_attack_hit_landed = true
		if _target.has_method("take_damage"):
			_target.take_damage(ATTACK_DAMAGE, self)

## hitstun_seconds/is_finisher exist purely so player.gd's own _punch() can
## call this with the same 4-arg signature it uses on another Player -- same
## no-op duck-typing wolf.gd's own take_damage() already documents.
func take_damage(amount: float, attacker: Node = null, _hitstun_seconds: float = 0.0, _is_finisher: bool = false) -> void:
	if is_dead or amount <= 0.0:
		return
	health = maxf(0.0, health - amount)
	if health <= 0.0:
		_die(attacker)

## Replaces the old single scripted tip-and-sink tween (which read as the
## whole rigid model just turning sideways and clipping through the floor,
## per direct user feedback) with a real, if crude, ragdoll: the 3
## already-separate mesh pieces (see this file's own class comment on the
## body/claws being genuinely disconnected mesh islands) each become their
## own independent RigidBody3D debris, keeping whatever pose they were
## actually in at the moment of death (mid-swing included) and tumbling
## under real gravity/physics instead of following one fixed scripted
## path. No skeleton to collapse (see this file's own class comment) --
## this is the coarsest ragdoll granularity available, one rigid piece per
## mesh island, not per-bone.
func _die(_attacker: Node = null) -> void:
	is_dead = true
	velocity = Vector3.ZERO
	_target = null
	_attack_state = "idle"
	_collision.disabled = true # the ragdoll pieces carry their own collision now; the original hover-height hitbox shouldn't keep blocking movement where the corpse used to float
	_death_player.stream = DeathSound
	_death_player.play()
	_spawn_ragdoll_pieces()

## Detaches Body (+ both eyes, so they visually ride along with the head
## instead of floating behind at the old, now-abandoned model origin) and
## both claw pivots into 3 independent RigidBody3D debris pieces --
## Node.reparent()'s own keep_global_transform (the default) is what makes
## this simple: each piece keeps its exact current world pose without any
## manual transform math, mid-swing pose included.
func _spawn_ragdoll_pieces() -> void:
	var head_body := _make_ragdoll_body(_body_inst.global_transform)
	_body_inst.reparent(head_body, true)
	_eye_mesh_l.reparent(head_body, true)
	_eye_mesh_r.reparent(head_body, true)
	_eye_light_l.reparent(head_body, true)
	_eye_light_r.reparent(head_body, true)
	_add_ragdoll_collision(head_body, Vector3(1.3, 1.0, 1.4) * MODEL_SCALE * 0.5)
	_fade_eye(_eye_mesh_l, _eye_light_l)
	_fade_eye(_eye_mesh_r, _eye_light_r)

	# See loot/loot_component.gd's own class comment -- a real reported ask
	# ("press E to loot a enemy... corpse"). Attached to head_body (the
	# main "Body" piece) rather than a claw, matching where a player would
	# naturally aim/interact with the corpse as a whole.
	var loot: Node3D = LootComponent.new()
	var loot_items: Array[Dictionary] = []
	for entry in LOOT_TABLE:
		loot_items.append({"item_id": entry["item_id"], "count": randi_range(entry["count_min"], entry["count_max"])})
	loot.loot_items = loot_items
	head_body.add_child(loot)

	var left_body := _make_ragdoll_body(_left_claw_pivot.global_transform)
	_left_claw_pivot.reparent(left_body, true)
	_add_ragdoll_collision(left_body, Vector3.ONE * MODEL_SCALE * 0.4)

	var right_body := _make_ragdoll_body(_right_claw_pivot.global_transform)
	_right_claw_pivot.reparent(right_body, true)
	_add_ragdoll_collision(right_body, Vector3.ONE * MODEL_SCALE * 0.4)

	# The original CharacterBody3D (now visually empty -- every mesh/light
	# it had just got reparented away) stays around, invisible and non-
	# colliding, rather than being freed outright: take_damage()/_die()'s
	# own is_dead guard already makes it fully inert, and freeing self from
	# inside its own method call stack is needless risk for zero benefit.
	_model.visible = false

## Same collision_layer/mask (RAGDOLL_COLLISION_LAYER, world-only) and
## damping character.gd's own real skeletal ragdoll already established
## (see this file's own class comment) -- a small random toss/tumble rather
## than a limp drop so the collapse reads as a real hit, not a puppet
## quietly folding.
##
## Deliberately strips SCALE out of `at_global_transform` before applying it
## here (position/rotation only) -- confirmed directly as a real reported
## bug otherwise: RigidBody3D's own transform is what the physics engine
## actively re-integrates every physics step from its internal position/
## rotation state, and it does NOT preserve an arbitrary scaled basis
## sitting in that transform, so a body created with MODEL_SCALE (2x) baked
## into ITS OWN global_transform.basis rendered at the right size for
## exactly one frame, then visibly snapped down to 1x the instant physics
## started simulating it. The caller's OWN reparent(body, true) call is what
## re-derives and bakes that missing 2x scale into the reparented MESH's own
## local transform instead (a plain child Node3D the physics step never
## touches), which is what actually survives simulation.
func _make_ragdoll_body(at_global_transform: Transform3D) -> RigidBody3D:
	var body := RigidBody3D.new()
	body.collision_layer = RAGDOLL_COLLISION_LAYER
	body.collision_mask = 1
	body.gravity_scale = RAGDOLL_GRAVITY_SCALE
	body.linear_damp = RAGDOLL_LINEAR_DAMP
	body.angular_damp = RAGDOLL_ANGULAR_DAMP
	get_tree().current_scene.add_child(body)
	body.global_transform = Transform3D(Basis(at_global_transform.basis.get_rotation_quaternion()), at_global_transform.origin)
	body.linear_velocity = Vector3(randf_range(-1.0, 1.0), randf_range(0.6, 1.4), randf_range(-1.0, 1.0)) * RAGDOLL_TOSS_SPEED
	body.angular_velocity = Vector3(randf_range(-3.0, 3.0), randf_range(-3.0, 3.0), randf_range(-3.0, 3.0))
	# See player.gd's own grab/carry system (Skyrim-style hold-E-to-lift) --
	# "grabbable" is the tag its raycast targeting checks for. Local-only
	# (no owner-peer metadata the way character.gd's own per-bone ragdoll
	# gets) -- NPCs aren't networked at all today, so there's nothing to
	# sync a carry FOR on another peer's screen yet.
	body.add_to_group("grabbable")
	var bounce_mat := PhysicsMaterial.new()
	bounce_mat.bounce = 0.25
	bounce_mat.friction = 0.7
	body.physics_material_override = bounce_mat
	return body

## A single box centered on the piece's own origin -- not a precise per-
## mesh AABB fit (this is a few seconds of tumbling debris, not something
## the player interacts with, so exact collision-to-mesh alignment isn't
## worth chasing; same "good enough, not pixel-precise" call this file's
## own CLAW_HIT_RADIUS distance-to-body check already made for a similar
## reason -- see its own comment).
func _add_ragdoll_collision(body: RigidBody3D, box_size: Vector3) -> void:
	var shape := BoxShape3D.new()
	shape.size = box_size
	var collision := CollisionShape3D.new()
	collision.shape = shape
	body.add_child(collision)

## Tweens the eye's own glow down to nothing over EYE_FADE_SECONDS instead
## of it just vanishing (freed) or staying lit on a now-dead, ragdolled
## head -- both the emissive mesh material (mesh.material was built fresh
## per-eye in _add_eye(), never shared, so mutating it directly here can't
## bleed into any other instance) and the real OmniLight3D fade together.
func _fade_eye(mesh_inst: MeshInstance3D, light: OmniLight3D) -> void:
	var mat: StandardMaterial3D = mesh_inst.mesh.material
	var tw := create_tween()
	tw.set_parallel(true)
	tw.tween_property(mat, "emission_energy_multiplier", 0.0, EYE_FADE_SECONDS)
	tw.tween_property(mat, "albedo_color", Color(0.05, 0.02, 0.02, 1.0), EYE_FADE_SECONDS)
	tw.tween_property(light, "light_energy", 0.0, EYE_FADE_SECONDS)

func _build_model() -> void:
	_model = Node3D.new()
	_model.name = "Model"
	add_child(_model)
	_model.scale = Vector3.ONE * MODEL_SCALE

	_facing_flip = Node3D.new()
	_facing_flip.name = "FacingFlip"
	_facing_flip.rotation_degrees.y = 180.0
	_model.add_child(_facing_flip)

	var imported: Node = MonsterScene.instantiate()
	var body_mat := StandardMaterial3D.new()
	body_mat.albedo_color = BODY_COLOR
	body_mat.roughness = 0.65

	var body_source := _find_mesh_instance_named(imported, "Body")
	if body_source != null:
		_body_inst = MeshInstance3D.new()
		_body_inst.mesh = body_source.mesh
		_body_inst.material_override = body_mat
		_facing_flip.add_child(_body_inst)

	_left_claw_pivot = _build_claw_pivot(imported, "LeftClaw", CLAW_PIVOT_L, body_mat)
	_right_claw_pivot = _build_claw_pivot(imported, "RightClaw", CLAW_PIVOT_R, body_mat)
	imported.free()

	var eye_l := _add_eye(EYE_L, EYE_L_SCALE, EYE_L_TILT_DEGREES, "EyeMeshL", "EyeLightL")
	_eye_mesh_l = eye_l["mesh"]
	_eye_light_l = eye_l["light"]
	var eye_r := _add_eye(EYE_R, EYE_R_SCALE, EYE_R_TILT_DEGREES, "EyeMeshR", "EyeLightR")
	_eye_mesh_r = eye_r["mesh"]
	_eye_light_r = eye_r["light"]

	# A simple box approximation of the model's own head+claws footprint
	# (unscaled bbox roughly 1.69 x 1.12 x 1.90, see this file's own class
	# comment on the offline analysis that measured it) -- same "close
	# enough, not pixel-matched to the mesh" convention wolf.gd/sloth.gd's
	# own capsule collision shapes already use. Deliberately NOT shrunk
	# below the real bbox (an earlier version multiplied this by an
	# additional 0.55 "fits through tighter gaps" fudge factor) -- a real
	# reported bug, confirmed via screenshot: with the hitbox roughly half
	# the model's own real footprint, the visual body/claws stuck visibly
	# through walls well before move_and_slide's own collision response
	# ever kicked in. Matching the real bbox 1:1 means the hitbox now stops
	# at the same boundary the mesh itself does, same "close enough" spirit
	# every other NPC's own collision shape already follows (see
	# mutant.gd's own "roughly the mesh's own standing AABB" comment).
	var shape := BoxShape3D.new()
	shape.size = Vector3(1.8, 1.3, 2.0) * MODEL_SCALE
	_collision.shape = shape

## Builds a pivot Node3D at `pivot_pos` (that claw's own approximate wrist,
## see CLAW_PIVOT_L/R's own comment) and reparents the named claw mesh
## under it, offsetting the mesh's own local position by -pivot_pos so it
## renders in EXACTLY its original spot at rest -- rotating the returned
## pivot then swings the claw around that wrist point instead of around the
## whole model's own origin.
func _build_claw_pivot(imported: Node, mesh_name: String, pivot_pos: Vector3, mat: Material) -> Node3D:
	var pivot := Node3D.new()
	pivot.position = pivot_pos
	_facing_flip.add_child(pivot)

	var source := _find_mesh_instance_named(imported, mesh_name)
	if source != null:
		var inst := MeshInstance3D.new()
		inst.mesh = source.mesh
		inst.material_override = mat
		inst.position = -pivot_pos
		pivot.add_child(inst)
	return pivot

## Returns {"mesh": MeshInstance3D, "light": OmniLight3D} -- _build_model()
## stores both into _eye_mesh_l/r + _eye_light_l/r so _die()'s own ragdoll/
## fade logic can reach them directly later (see those vars' own comment).
## scale/tilt_degrees are per-eye now (see EYE_L_SCALE/EYE_R_SCALE's own
## comment on why they're no longer one shared constant), and mesh_name/
## light_name are passed explicitly rather than inferred from tilt_degrees'
## sign -- real measured data landed with BOTH eyes' own tilt keeping the
## same sign the old mirror-symmetry assumption expected (a coincidence,
## not something to rely on going forward).
func _add_eye(pos: Vector3, scale: Vector3, tilt_degrees: float, mesh_name: String, light_name: String) -> Dictionary:
	# A unit sphere (radius 1), non-uniformly SCALED on the instance itself
	# rather than an elongated SphereMesh -- scaling the instance keeps the
	# mesh resource itself shareable/simple and is exactly equivalent for a
	# primitive this basic.
	var mesh := SphereMesh.new()
	mesh.radius = 1.0
	mesh.height = 2.0
	var mat := StandardMaterial3D.new()
	mat.albedo_color = EYE_COLOR
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.emission_enabled = true
	mat.emission = EYE_COLOR
	mat.emission_energy_multiplier = 3.0
	mesh.material = mat
	var inst := MeshInstance3D.new()
	inst.mesh = mesh
	inst.position = pos
	inst.scale = scale
	inst.rotation.z = deg_to_rad(tilt_degrees)
	# Named explicitly (Node3D.new() would otherwise get an auto-numbered
	# default like "MeshInstance3D2") so they're actually findable by eye in
	# the Debugger panel's Remote tab -- see print_eye_pose()'s own comment
	# on the manual-tuning workflow this is for.
	inst.name = mesh_name
	_facing_flip.add_child(inst)

	# The unshaded emissive sphere alone reads as a flat glowing disc from
	# most angles -- a real light at the same spot is what actually
	# illuminates the surrounding eye socket/skull interior, giving the
	# "glowing from within" look instead of just a bright dot floating in
	# the dark (the original, literal reading of "just a red dot" this
	# whole change is fixing).
	var light := OmniLight3D.new()
	light.light_color = EYE_COLOR
	light.light_energy = EYE_LIGHT_ENERGY
	light.omni_range = EYE_LIGHT_RANGE
	light.position = pos
	light.name = light_name
	_facing_flip.add_child(light)

	return {"mesh": inst, "light": light}

## EYE_L/EYE_R and their per-side scale/tilt constants have already been
## wrong more than once purely from guessing coordinates blind with no way
## to see the live render in this environment. The eye meshes/lights are
## built ONCE in _add_eye() and never re-touched on any later frame (unlike
## a held weapon's own grip pose, which gets reapplied every frame -- see
## two_handed_sword.gd's own set_manual_override comment) -- so there's
## nothing to "unlock" here at all: once a crimson claw is spawned,
## EyeMeshL/EyeMeshR/EyeLightL/EyeLightR (see _add_eye's own naming) are
## already freely draggable by hand via the Debugger panel's own Remote tab
## while the game is running FROM the editor. This just prints back
## whatever position/scale/rotation they were left at, to copy into
## EYE_L/EYE_R/EYE_L_SCALE/EYE_R_SCALE/EYE_L_TILT_DEGREES/EYE_R_TILT_DEGREES
## by hand -- same "print the final numbers for a human to copy" role
## car.gd's own /car pose command already fills for DriverSeat/PassengerSeat.
func print_eye_pose() -> void:
	print("EyeMeshL position = ", _eye_mesh_l.position, " scale = ", _eye_mesh_l.scale, " tilt (degrees) = ", _eye_mesh_l.rotation.z * 180.0 / PI)
	print("EyeMeshR position = ", _eye_mesh_r.position, " scale = ", _eye_mesh_r.scale, " tilt (degrees) = ", _eye_mesh_r.rotation.z * 180.0 / PI)
	print("EyeLightL position = ", _eye_light_l.position)
	print("EyeLightR position = ", _eye_light_r.position)

## Depth-first search for the first MeshInstance3D under `node` whose OWN
## name matches `target_name` -- the split pipeline names each of the 3
## exported pieces ("Body"/"LeftClaw"/"RightClaw", see this file's own class
## comment), and Godot's glTF importer preserves those as real node names,
## so this can look each one up directly instead of guessing by order.
static func _find_mesh_instance_named(node: Node, target_name: String) -> MeshInstance3D:
	if node is MeshInstance3D and node.name == target_name:
		return node
	for child in node.get_children():
		var found := _find_mesh_instance_named(child, target_name)
		if found != null:
			return found
	return null
