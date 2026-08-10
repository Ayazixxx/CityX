extends Node3D
## A pick-up-able semi-automatic pistol -- same "walk up, look at it, press E"
## interact pattern every holdable prop in this project already uses (see
## weapons/two_handed_sword.gd), but NOT a subclass of it: guns are a
## fundamentally different kind of weapon (hitscan + ammo/reload state, one-
## handed, no swing/combo animation state to switch grip poses for), so this
## is its own self-contained script rather than forcing an awkward shared
## base between two genuinely different weapon categories. If a second gun
## shows up later, THAT'S the point to extract a shared "firearm" base --
## same "wait for a real second case" reasoning two_handed_sword.gd's own
## class comment already explains for swords.
##
## Deliberately copies Fortnite's classic pistol feel: semi-automatic (one
## shot per click, no full-auto), hitscan (instant raycast, not a slow
## travelling projectile), a magazine separate from a reserve pool, a timed
## reload, hip-fire bullet spread that tightens while aiming down sights
## (see player.gd's own _is_aiming/ADS handling), and a fixed fire-rate
## cooldown. No headshot multiplier/damage falloff yet -- deferred, not
## implemented incorrectly.
##
## The actual fire/reload/ammo logic all lives HERE (not in player.gd),
## called by player.gd's own _try_fire_gun()/_try_reload_gun() with just the
## camera's current origin/direction -- keeps every gun-specific number
## (damage, range, spread, fire rate, magazine size) self-contained on the
## weapon itself, the same way each sword owns its own combat constants.

## Matches the real Beretta M9's actual 15-round magazine.
const MAGAZINE_SIZE := 15
const STARTING_RESERVE_AMMO := 45
const DAMAGE := 20.0
const RANGE := 60.0
# Rounds per second -- a real Fortnite-pistol-like semi-auto rate (~5-6/s),
# enforced as a cooldown between shots rather than gating on click speed
# alone, so mashing the button faster than this simply drops the extra
# clicks (same "cooldown, not click-rate-dependent" pattern player.gd's own
# PUNCH_COOLDOWN_SECONDS already uses for fists).
const FIRE_RATE_PER_SEC := 5.5
const FIRE_COOLDOWN_SECONDS := 1.0 / FIRE_RATE_PER_SEC
const RELOAD_SECONDS := 1.5
# Half-angle of the hip-fire bullet-spread cone, in degrees -- a real shot
# lands anywhere within this cone of the aimed direction, not dead-center,
# unless aiming down sights (see ADS_SPREAD_DEGREES).
const HIP_FIRE_SPREAD_DEGREES := 3.0
# Tighter while aiming down sights (right-click, see player.gd's own
# _is_aiming) -- not zero, a pistol is never laser-accurate even ADS, but a
# real, noticeable improvement over hip-fire.
const ADS_SPREAD_DEGREES := 0.5

const INTERACT_RANGE := 2.5
const LOOK_DOT_THRESHOLD := 0.82

# The real Beretta_M9.obj model (see weapons/models/beretta_m9/ -- a
# user-supplied asset, imported via Godot's built-in OBJ importer), NOT a
# procedural build like every other weapon in this project -- see
# _build_visual's own comment for why this needed a self-normalizing scale
# instead of a flat MODEL_SCALE multiplier.
const ModelRes := preload("res://weapons/models/beretta_m9/Beretta_M9.obj")
# Real Beretta M9 overall length (~21.7cm) -- the imported mesh gets scaled
# so its own largest dimension matches this at runtime (see _build_visual),
# rather than assuming the raw OBJ's units are meters or a specific cm-to-m
# ratio, since that couldn't be confirmed just from a quick text inspection
# of the file.
const TARGET_LENGTH_METERS := 0.22
# Overrides EVERY material the imported OBJ came with -- confirmed via a
# direct dump of the imported mesh's own surfaces/materials (a scratch
# diagnostic, since deleted) that ~60 of its ~63 surfaces all share one
# material ("blinn21SG") whose .mtl source data is Kd 0.00 0.00 0.00 --
# pure black, zero diffuse reflectance. Godot's own importer warned "Ambient
# light... is ignored in PBR" for exactly this material, converting old
# Blinn-style Ka/Kd/Ks data into a StandardMaterial3D that ends up reading
# as a solid black silhouette against this game's own dark night lighting --
# a real reported bug ("it just spawns a light in the ground," i.e. the gun
# itself was invisible, only nearby unrelated scene lights were visible). No
# real texture maps exist in the source .mtl to fall back on either (checked
# directly -- no map_Kd entries at all), so a single uniform override
# (losing whatever per-part material variation the original model might
# have intended) is the correct fix here, not a per-surface material dump.
const GUNMETAL_COLOR := Color(0.16, 0.16, 0.17)
# A further gameplay-feel multiplier on TOP of the real-world normalization
# above (applied to this whole node in _ready(), same as every other
# weapon's own MODEL_SCALE) -- starts at 1.0 (true real-world size) since
# there's no prior tuning history for this real model the way the
# procedural swords had; likely needs adjusting once actually seen in-hand.
const MODEL_SCALE := 1.0

# Hand-tuned via /gun unlock + Godot's own Remote Scene Tree debugger +
# /gun pose (see that workflow's own comment history below for how this
# constant used to be a rough (0,0,0)/+90-degrees-around-X placeholder,
# before ever actually being measured against a live in-hand view).
const GRIP_HAND_ROTATION := Vector3(1.130973, 1.392773, 0.216421)
const GRIP_HAND_POSITION := Vector3(-0.03, -0.3, 0.03)
# How big the gun reads IN HAND, separate from MODEL_SCALE (which only ever
# applies to the real-world-normalized mesh before it's ever picked up --
# see _build_visual/_ready). Same live-tuning workflow as the two constants
# above (/gun unlock + Godot's own Remote Scene Tree debugger + /gun pose).
const GRIP_SCALE := Vector3(2.515, 2.515, 2.515)

# A simple PROCEDURAL reload animation -- no dedicated mocap clip exists for
# this (unlike the shoot/aim-in/locomotion poses, which are all real
# retargeted Mixamo animations on the CHARACTER's own skeleton), so instead
# this just offsets the GUN'S OWN held transform (added on top of
# GRIP_HAND_ROTATION/POSITION every frame, see _update_held_pose) for the
# duration of a reload: the gun tilts down and pulls in, as if being brought
# down to eject the old magazine and seat a new one, then eases back up to
# the normal grip the instant reload() actually finishes. A sine envelope
# (0 -> 1 -> 0 across RELOAD_SECONDS, see _update_held_pose's own dip
# amount) rather than a hard cut, so it reads as one continuous down-and-up
# motion instead of a snap into and out of a held pose. Not tuned against a
# live view the way GRIP_HAND_ROTATION/POSITION were -- reasonable
# placeholder values, functional rather than polished per the actual ask.
const RELOAD_DIP_ROTATION_OFFSET := Vector3(0.5, 0.0, 0.0)
const RELOAD_DIP_POSITION_OFFSET := Vector3(0.0, -0.15, 0.05)

@onready var _interact_area: Area3D = $InteractArea
@onready var _muzzle_flash: OmniLight3D = $MuzzleFlash
@onready var _muzzle_sparks: GPUParticles3D = $MuzzleFlash/MuzzleSparks
@onready var _shot_player: AudioStreamPlayer3D = $ShotPlayer
@onready var _reload_player: AudioStreamPlayer3D = $ReloadPlayer

const BloodSplatter = preload("res://weapons/blood_splatter.gd")

var _nearby_player: CharacterBody3D = null
var _picked_up := false
var _wielding_player: CharacterBody3D = null
# Dev-only escape hatch -- same reasoning/workflow as every sword's own
# _manual_override (see two_handed_sword.gd), just duplicated here since
# this script doesn't share that base.
var _manual_override := false

var _magazine_ammo := MAGAZINE_SIZE
var _reserve_ammo := STARTING_RESERVE_AMMO
var _fire_cooldown_remaining := 0.0
var _is_reloading := false
var _reload_time_remaining := 0.0
var _muzzle_flash_remaining := 0.0

func _ready() -> void:
	_build_visual()
	scale = Vector3.ONE * MODEL_SCALE
	_interact_area.body_entered.connect(_on_interact_area_body_entered)
	_interact_area.body_exited.connect(_on_interact_area_body_exited)
	_muzzle_flash.visible = false

func _process(delta: float) -> void:
	if _picked_up:
		_update_held_pose()
	else:
		if _nearby_player != null and Input.is_action_just_pressed("interact") and _player_is_looking_at_me(_nearby_player):
			_try_add_to_inventory(_nearby_player)

	_fire_cooldown_remaining = maxf(0.0, _fire_cooldown_remaining - delta)

	if _is_reloading:
		_reload_time_remaining -= delta
		if _reload_time_remaining <= 0.0:
			_finish_reload()

	if _muzzle_flash_remaining > 0.0:
		_muzzle_flash_remaining -= delta
		if _muzzle_flash_remaining <= 0.0:
			_muzzle_flash.visible = false

func _update_held_pose() -> void:
	if _manual_override:
		return
	rotation = GRIP_HAND_ROTATION
	position = GRIP_HAND_POSITION
	scale = GRIP_SCALE
	if _is_reloading:
		# 0 at the very start/end of the reload, peaking at 1 exactly at its
		# midpoint -- see RELOAD_DIP_ROTATION_OFFSET's own comment. Clamped
		# since a reload triggered on the same frame RELOAD_SECONDS is 0 (a
		# degenerate case that can't really happen given reload()'s own
		# guards, but cheap to guard against here too) would otherwise send
		# progress outside 0..1.
		var progress: float = clampf(1.0 - (_reload_time_remaining / RELOAD_SECONDS), 0.0, 1.0)
		var dip: float = sin(progress * PI)
		rotation += RELOAD_DIP_ROTATION_OFFSET * dip
		position += RELOAD_DIP_POSITION_OFFSET * dip

func set_manual_override(enabled: bool) -> void:
	_manual_override = enabled

func print_current_pose() -> void:
	print("pistol position = ", position)
	print("pistol rotation (radians) = ", rotation)
	print("pistol rotation (degrees) = ", rotation * 180.0 / PI)
	print("pistol scale = ", scale)

func _player_is_looking_at_me(player: CharacterBody3D) -> bool:
	var camera: Camera3D = player.get("camera")
	if camera == null:
		return false
	var origin: Vector3 = player.global_position + Vector3(0, 1.5, 0)
	var to_self := global_position - origin
	if to_self.length() > INTERACT_RANGE:
		return false
	var facing := -camera.global_transform.basis.z
	return facing.normalized().dot(to_self.normalized()) >= LOOK_DOT_THRESHOLD

## items/item_registry.gd's own lookup key for this weapon -- see
## _try_add_to_inventory()'s own comment for how this gets used.
func get_item_id() -> String:
	return "pistol"

## Picking this up now adds it to the player's inventory instead of
## equipping it directly (a deliberate, confirmed rework -- see
## player/inventory.gd's own class comment). _equip() below is untouched
## and still does the real work; it's just called from
## Inventory.equip_from_slot() on a freshly instantiated copy now, instead
## of from here.
func _try_add_to_inventory(player: CharacterBody3D) -> void:
	var inv: Node = player.get_node_or_null("Inventory")
	if inv == null or not inv.has_method("add_item"):
		return
	if inv.call("add_item", get_item_id(), 1):
		_interact_area.monitoring = false
		queue_free()

func _equip(player: CharacterBody3D) -> void:
	var character: Node = player.get_node_or_null("Character")
	if character == null or not character.has_method("get_hand_node"):
		return
	var hand: Node3D = character.get_hand_node()
	if hand == null:
		return
	# Replace whatever's already equipped -- same "one weapon at a time"
	# reasoning every sword's own _equip() already applies (see
	# two_handed_sword.gd's own comment on the real bug this fixed).
	for existing in hand.get_children():
		existing.queue_free()
	_picked_up = true
	_wielding_player = player
	_interact_area.monitoring = false
	get_parent().remove_child(self)
	hand.add_child(self)
	rotation = GRIP_HAND_ROTATION
	position = GRIP_HAND_POSITION
	scale = GRIP_SCALE
	# Swaps character.gd's own locomotion/jump/shoot/aim clips over to the
	# pistol moveset (see set_wielding_gun's own comment) -- same pattern
	# two_handed_sword.gd's own _equip() already uses for set_wielding_sword.
	if character.has_method("set_wielding_gun"):
		character.set_wielding_gun(true)
	if player.has_method("equip_gun"):
		player.equip_gun(self)

func _on_interact_area_body_entered(body: Node3D) -> void:
	if body is CharacterBody3D and body.has_method("respawn"):
		_nearby_player = body

func _on_interact_area_body_exited(body: Node3D) -> void:
	if body == _nearby_player:
		_nearby_player = null

## Called by player.gd's own _try_fire_gun() with the camera's current
## world-space origin/direction -- returns false (a dry/empty click) without
## firing if on cooldown, mid-reload, or the magazine is empty. Applies
## HIP_FIRE_SPREAD_DEGREES/ADS_SPREAD_DEGREES as a random cone deviation
## around the aimed direction (aiming controls which cone, not whether one
## applies at all -- even ADS isn't perfectly laser-accurate), then does a
## single instant hitscan raycast and damages whatever it hits, same
## collision_mask (world geometry + other players' bodies) and
## has_method("take_damage") duck-typing player.gd's own melee _punch()
## raycast already uses.
func fire(origin: Vector3, direction: Vector3, aiming: bool) -> bool:
	if _is_reloading or _fire_cooldown_remaining > 0.0 or _magazine_ammo <= 0:
		return false
	_fire_cooldown_remaining = FIRE_COOLDOWN_SECONDS
	_magazine_ammo -= 1

	_muzzle_flash.visible = true
	_muzzle_flash_remaining = 0.05
	# restart(), not just emitting=true -- a one_shot GPUParticles3D that's
	# already mid-burst from the previous shot (very possible at this gun's
	# real fire rate, see FIRE_COOLDOWN_SECONDS) would otherwise just keep
	# playing that same burst uninterrupted, silently swallowing this one --
	# same "already-playing same clip doesn't restart" issue player.gd's own
	# shoot animation hit, restart() forces a fresh burst from frame 0 every
	# time regardless of what it was already doing.
	_muzzle_sparks.restart()
	_shot_player.play()

	var spread_degrees: float = ADS_SPREAD_DEGREES if aiming else HIP_FIRE_SPREAD_DEGREES
	var spread_dir := _apply_spread(direction, spread_degrees)

	var space_state := get_world_3d().direct_space_state
	var to: Vector3 = origin + spread_dir * RANGE
	var query := PhysicsRayQueryParameters3D.create(origin, to)
	query.collision_mask = 1 | 4 # world geometry + other players' bodies
	if _wielding_player != null:
		query.exclude = [_wielding_player]
	var result := space_state.intersect_ray(query)
	if not result.is_empty():
		var target: Object = result["collider"]
		# Checked before take_damage -- a breakable-glass building (see
		# city/building_glass.gd) has neither has_method today, but this
		# keeps the "world geometry vs. a real body" ordering explicit
		# rather than relying on which duck-typed method happens to exist.
		if target.has_method("try_break_glass"):
			target.try_break_glass(result["position"], result["normal"])
		elif target.has_method("take_damage"):
			# Strength stat bonus (see player.gd's own apply_strength()) --
			# duck-typed since _wielding_player is just typed as
			# CharacterBody3D here, not the Player script specifically.
			var actual_damage := DAMAGE
			if _wielding_player != null and _wielding_player.has_method("apply_strength"):
				actual_damage = _wielding_player.apply_strength(DAMAGE)
			target.take_damage(actual_damage, _wielding_player)
			# Only on an actual character hit, not a shot that just hit a
			# wall/the ground -- world geometry has no take_damage() to
			# duck-type against, so this naturally only fires for a real
			# body hit (a player or the CPU dummy), never a random wall.
			BloodSplatter.spawn(result["position"], result["normal"], get_tree())
	return true

## Perturbs direction within a cone of half-angle spread_degrees -- two
## independent random small-angle rotations around an arbitrary pair of axes
## perpendicular to direction, which for a small cone angle reads as a
## uniform-enough random deviation without needing real spherical-cap
## sampling math.
func _apply_spread(direction: Vector3, spread_degrees: float) -> Vector3:
	if spread_degrees <= 0.0:
		return direction
	var helper := Vector3.UP if absf(direction.dot(Vector3.UP)) < 0.95 else Vector3.RIGHT
	var right := direction.cross(helper).normalized()
	var up := right.cross(direction).normalized()
	var max_rad := deg_to_rad(spread_degrees)
	var yaw := randf_range(-max_rad, max_rad)
	var pitch := randf_range(-max_rad, max_rad)
	var result := direction.rotated(up, yaw)
	result = result.rotated(right, pitch)
	return result.normalized()

## Called by player.gd's own _try_reload_gun() (bound to the "reload"
## action) -- no-op if already reloading, the magazine's already full, or
## there's no reserve ammo left to reload FROM (matching the plain "nothing
## happens" convention this project already uses for other denied actions,
## e.g. punching during its own cooldown).
func reload() -> void:
	if _is_reloading or _magazine_ammo >= MAGAZINE_SIZE or _reserve_ammo <= 0:
		return
	_is_reloading = true
	_reload_time_remaining = RELOAD_SECONDS
	_reload_player.play()

func _finish_reload() -> void:
	_is_reloading = false
	var needed: int = MAGAZINE_SIZE - _magazine_ammo
	var drawn: int = mini(needed, _reserve_ammo)
	_magazine_ammo += drawn
	_reserve_ammo -= drawn

## Tops this gun's own reserve back up. Only NPC wielders call it (see
## npc/gunman.gd) -- an armed enemy that permanently runs dry after its
## starting 60 rounds would just stand in front of you clicking, which reads
## as a broken enemy rather than as a lull in a firefight. The player's own
## pistol never calls this, so scarce ammo stays a real constraint for them.
func restock_reserve(rounds: int = STARTING_RESERVE_AMMO) -> void:
	_reserve_ammo = maxi(_reserve_ammo, rounds)

## Read by ui/hud.gd's own ammo counter.
func get_ammo_text() -> String:
	if _is_reloading:
		return "RELOADING..."
	return "%d / %d" % [_magazine_ammo, _reserve_ammo]

## Loads the real Beretta_M9.obj (see ModelRes's own comment) instead of
## building a procedural mesh. Godot's OBJ importer resolves this specific
## file directly to an ArrayMesh (confirmed via a quick load+inspect
## diagnostic, since deleted -- a `load()`+print, not the earlier
## screenshot-render approach that kept timing out under CPU contention),
## not a PackedScene, so this loads it as a plain Mesh rather than carrying
## unreachable PackedScene-handling code (GDScript's own static analyzer
## actually rejects an `is PackedScene` check here once ModelRes's type is
## inferred as ArrayMesh from the preload -- confirmed as a real compile
## error, not just unnecessary).
##
## Re-centers the mesh on its own bounding-box middle and normalizes its
## scale so its largest dimension matches TARGET_LENGTH_METERS -- the raw
## OBJ's own units/whether it includes any stray non-gun geometry couldn't
## be confirmed from a quick text inspection alone (a naive vertex-bounds
## scan suggested an implausibly large ~57cm span on one axis), so deriving
## scale from the ACTUAL loaded mesh at runtime is more robust than trusting
## an assumed cm-to-m conversion.
func _build_visual() -> void:
	var mesh_inst := MeshInstance3D.new()
	mesh_inst.mesh = ModelRes
	add_child(mesh_inst)

	var aabb: AABB = mesh_inst.get_aabb()
	var largest_dim: float = maxf(aabb.size.x, maxf(aabb.size.y, aabb.size.z))
	var normalize_scale: float = TARGET_LENGTH_METERS / largest_dim if largest_dim > 0.0001 else 1.0
	mesh_inst.scale = Vector3.ONE * normalize_scale
	mesh_inst.position = -aabb.get_center() * normalize_scale

	var gunmetal := StandardMaterial3D.new()
	gunmetal.albedo_color = GUNMETAL_COLOR
	gunmetal.metallic = 0.75
	gunmetal.roughness = 0.35
	gunmetal.metallic_specular = 0.0
	mesh_inst.material_override = gunmetal

	$MuzzleFlash.position = Vector3(0, 0, TARGET_LENGTH_METERS * 0.5)
