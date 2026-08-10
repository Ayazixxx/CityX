extends RigidBody3D
## Pick-up-able glass bottle: a one-handed melee weapon with real durability
## (Intact -> Broken -> Fully Broken, see the DurabilityState enum below)
## AND a throwable mode (see player.gd's own _try_throw_bottle/
## _try_swing_bottle) -- neither of those exists for any other weapon in
## this project.
##
## Deliberately a STANDALONE script, NOT a weapons/two_handed_sword.gd
## subclass, even though the pickup/equip/interact flow below looks a lot
## like it (and IS closely modeled on it) -- two_handed_sword.gd hardcodes
## its own grip/scale/interact constants as plain consts shared identically
## by every sword (GDScript can't let a subclass override a parent's own
## const, confirmed by wearables/wearable.gd's own override-METHOD
## workaround for the exact same constraint), and every sword so far has
## been an explicit "same size, same grip" ask. A bottle is one-handed with
## a completely different grip pose/scale -- extending would mean either
## living with wrong un-overridable sword constants or refactoring shared
## code that every existing sword also depends on. weapons/pistol.gd already
## made and documented this same call for a structurally similar situation.
## Only the genuinely reusable, parameter-driven primitives
## (_flat_material/_add_tube/_tri, credited below) are duplicated from
## two_handed_sword.gd -- the sword-specific blade-lofting helpers aren't
## needed for a cylindrical bottle shape.
##
## RigidBody3D, not the plain Node3D every other weapon uses -- per an
## explicit ask that a bottle be "grabbable like the other weapons" (see
## player.gd's own grab/carry system) with real gravity while lying
## unequipped, not just a static prop waiting to be picked up. `freeze` is
## what makes this coexist with the EXISTING equip flow below unchanged:
## true the instant it's equipped (reparented onto the hand bone, driven
## by direct position/rotation writes in _update_held_pose -- freeze stops
## the physics engine from fighting those), false while free-standing in
## the world (real physics, and what makes it a valid grab-carry target).

enum DurabilityState { INTACT, BROKEN, FULLY_BROKEN }

# Hits 2 through 11 (10 hits) while BROKEN before FULLY_BROKEN -- the 1st
# landed hit is what takes INTACT -> BROKEN in the first place, per the
# user's own "breaks... then lasts 10 more hits" wording.
const BROKEN_HITS_TO_FULLY_BROKEN := 10

# Per-durability-state melee damage -- read by player.gd's own
# _try_swing_bottle() via get_melee_damage(), NOT routed through
# character.gd's combo-chain damage_scale system at all (see
# character.gd's own punch() comment on why a bottle gets a real third
# branch instead of falling into the unarmed chain).
const INTACT_MELEE_DAMAGE := 15.0        # a solid blunt object -- above PUNCH_DAMAGE (10), near HEAVY_PUNCH_DAMAGE (15)
const BROKEN_MELEE_DAMAGE := 22.0        # a jagged edge hits harder -- above pistol.gd's own DAMAGE (20)
const FULLY_BROKEN_MELEE_DAMAGE := 0.0   # inert -- stays equipped, does nothing (per explicit user answer)

const INTERACT_RANGE := 2.5
const LOOK_DOT_THRESHOLD := 0.82 # cos(35 degrees), same as every other weapon's own pickup gate

# Bottle geometry, in real-world meters, before BOTTLE_SCALE (see its own
# comment) is applied. Local +Y is "up" (base at the origin, neck/rim at
# the top).
const BODY_HEIGHT := 0.15
const SHOULDER_HEIGHT := 0.02
const NECK_HEIGHT := 0.045
const RIM_HEIGHT := 0.015
const BODY_RADIUS := 0.032
const NECK_RADIUS := 0.011

const GLASS_COLOR := Color(0.34, 0.22, 0.08)   # amber/brown
const GLASS_ALPHA := 0.55
const DULL_GLASS_COLOR := Color(0.22, 0.19, 0.16) # fully-broken -- desaturated, no glassy sheen
const LABEL_COLOR := Color(0.86, 0.80, 0.66)   # plain cream accent -- NOT a reproduction of any real label art

# Grip pose -- a rough placeholder (no dedicated bottle-holding animation to
# measure against), meant to be corrected the same hands-on way every other
# weapon's own grip constants already were: /bottle unlock (ui/dev_console.gd)
# + Godot's own Remote Scene Tree debugger, /bottle pose to read the result
# back out.
const GRIP_HAND_ROTATION := Vector3(deg_to_rad(-15), deg_to_rad(10), deg_to_rad(5))
const GRIP_HAND_POSITION := Vector3(0.0, -0.04, 0.06)

# The INTACT visual only -- a real imported bottle mesh (Meshy AI, offline
# decimated 180K->3.5K tris and rescaled to this file's own TARGET_HEIGHT of
# 0.23m -- see the offline pipeline notes) replacing the old fully-
# procedural tube-lofted body. BROKEN/FULLY_BROKEN stay procedural (see
# _build_broken_visual) -- no broken-state asset was provided, and the
# jagged-shard geometry there was never the part being replaced.
const BottleModelScene := preload("res://weapons/bottle_model.glb")

# 2.4 (2.0 * 1.2, per an explicit follow-up ask to make the already-2x
# bottle "1.2x bigger" still). Applied to a dedicated _visual_root
# child (see _ready()), NOT to this node's own scale -- this whole node is
# also the grip anchor (GRIP_HAND_POSITION/ROTATION are applied directly
# to ITS OWN position/rotation in _update_held_pose(), and it's what gets
# reparented straight into the hand bone in _equip()), so scaling it
# directly would also scale/distort that grip offset math. A separate
# always-2x visual child sidesteps that entirely -- every mesh either
# _build_intact_visual() or _build_broken_visual() creates goes under
# _visual_root now, not this node directly, so BOTH durability states (and
# any future rebuild) automatically render at the same doubled size with
# no separate per-state scale bookkeeping.
const BOTTLE_SCALE := 2.4

# Real physics while lying unequipped (see this file's own class comment).
# Layer 16 matches weapons/thrown_bottle.gd's own existing "small dynamic
# prop" layer -- collision_mask stays the RigidBody3D default (1, world
# only), same convention every ragdoll body in this project already uses,
# so it settles on the ground without pushing against players/NPCs.
const BOTTLE_COLLISION_LAYER := 16
const BOTTLE_MASS := 0.4 # a light glass bottle, not a heavy prop
const BOTTLE_BOUNCE := 0.25
const BOTTLE_FRICTION := 0.7

@onready var _interact_area: Area3D = $InteractArea

var _visual_root: Node3D
var _nearby_player: CharacterBody3D = null
var _picked_up := false
var _wielding_character: Node = null
# Dev-only escape hatch -- same reasoning/workflow as every sword's own
# _manual_override (see two_handed_sword.gd's own comment).
var _manual_override := false

var _state: DurabilityState = DurabilityState.INTACT
var _broken_hit_count := 0

func _ready() -> void:
	_visual_root = Node3D.new()
	# Named explicitly (Node3D.new() would otherwise get an auto-numbered
	# default like "Node3D2") so it's actually findable by hand in the
	# Debugger panel's Remote tab -- see print_current_pose()'s own comment
	# on why BOTTLE_SCALE's own manual-tuning workflow needs this SEPARATE
	# node in the first place, unlike the root's own position/rotation.
	_visual_root.name = "VisualRoot"
	_visual_root.scale = Vector3.ONE * BOTTLE_SCALE
	add_child(_visual_root)
	_rebuild_visual()
	_interact_area.body_entered.connect(_on_interact_area_body_entered)
	_interact_area.body_exited.connect(_on_interact_area_body_exited)

	# Collision/physics setup -- a single capsule roughly matching the
	# bottle's own real (BOTTLE_SCALE-applied) silhouette, base at this
	# node's own local y=0 same as the visual (see _build_intact_visual's
	# own coordinate space), NOT a precise per-mesh fit -- same "good
	# enough, not pixel-precise" call this project's other coarse ragdoll
	# collision boxes already make.
	collision_layer = BOTTLE_COLLISION_LAYER
	mass = BOTTLE_MASS
	var bounce_mat := PhysicsMaterial.new()
	bounce_mat.bounce = BOTTLE_BOUNCE
	bounce_mat.friction = BOTTLE_FRICTION
	physics_material_override = bounce_mat
	var total_height: float = (BODY_HEIGHT + SHOULDER_HEIGHT + NECK_HEIGHT + RIM_HEIGHT) * BOTTLE_SCALE
	var capsule := CapsuleShape3D.new()
	capsule.radius = BODY_RADIUS * BOTTLE_SCALE
	capsule.height = total_height
	var collision := CollisionShape3D.new()
	collision.shape = capsule
	collision.position = Vector3(0, total_height / 2.0, 0)
	add_child(collision)
	# See player.gd's own grab/carry system -- this tag is what its raycast
	# targeting checks for. Removed again the instant this is actually
	# equipped (see _equip()) -- an equipped weapon in your hand shouldn't
	# also be re-grabbable out of it.
	add_to_group("grabbable")

## items/item_registry.gd's own lookup key for this weapon.
func get_item_id() -> String:
	return "glass_bottle"

## Durability survives pickup -> inventory -> equip-again unlike every
## other item's own instance_data (empty dict) -- see player/inventory.gd's
## own add_item()/equip_from_slot() comments on why this is the one real
## exception to plain id+count stacking in this pass.
func _capture_instance_data() -> Dictionary:
	var state_name: String = ["intact", "broken", "fully_broken"][_state]
	return {"durability": state_name}

## See pistol.gd's own _try_add_to_inventory() for the shared reasoning --
## picking this up now adds it to the inventory instead of equipping it
## directly; _equip() below is unchanged, just called from
## Inventory.equip_from_slot() on a freshly instantiated copy now.
func _try_add_to_inventory(player: CharacterBody3D) -> void:
	var inv: Node = player.get_node_or_null("Inventory")
	if inv == null or not inv.has_method("add_item"):
		return
	if inv.call("add_item", get_item_id(), 1, _capture_instance_data()):
		_interact_area.monitoring = false
		queue_free()

## Called by Inventory.equip_from_slot() right after instantiate(), which
## is BEFORE this node has entered the tree (_ready() hasn't run yet, so
## _visual_root doesn't exist yet either) -- safe regardless of ordering:
## if _visual_root is still null, _ready()'s own unconditional
## _rebuild_visual() call picks up the _state set here once it does run;
## if this gets called later (e.g. a hypothetical future re-equip path),
## _visual_root already exists and gets rebuilt immediately instead.
func set_durability_state(state_name: String) -> void:
	match state_name:
		"broken":
			_state = DurabilityState.BROKEN
		"fully_broken":
			_state = DurabilityState.FULLY_BROKEN
		_:
			_state = DurabilityState.INTACT
	if _visual_root != null:
		_rebuild_visual()

func get_melee_damage() -> float:
	match _state:
		DurabilityState.INTACT:
			return INTACT_MELEE_DAMAGE
		DurabilityState.BROKEN:
			return BROKEN_MELEE_DAMAGE
		_:
			return FULLY_BROKEN_MELEE_DAMAGE

func can_throw() -> bool:
	return _state == DurabilityState.INTACT

## Called by player.gd's own _try_swing_bottle() ONLY after a melee swing
## actually lands on a valid target (has_method("take_damage")) -- a whiff
## against empty air/a wall does not advance durability, matching the
## user's own "when you hit someone with it" wording.
func register_landed_melee_hit() -> void:
	match _state:
		DurabilityState.INTACT:
			_state = DurabilityState.BROKEN
			_broken_hit_count = 0
			_rebuild_visual()
		DurabilityState.BROKEN:
			_broken_hit_count += 1
			if _broken_hit_count >= BROKEN_HITS_TO_FULLY_BROKEN:
				_state = DurabilityState.FULLY_BROKEN
				_rebuild_visual()
		DurabilityState.FULLY_BROKEN:
			pass # inert, nothing left to advance

## child.free() (immediate), not queue_free() (deferred) -- a deliberate
## choice, new territory for this codebase (no other script here rebuilds
## its own visual mid-session): a queue_free()'d old mesh would still
## render for one more frame alongside the freshly-built replacement below,
## a visible double-mesh flash the instant durability changes state.
func _rebuild_visual() -> void:
	for child in _visual_root.get_children():
		child.free()
	match _state:
		DurabilityState.INTACT:
			_build_intact_visual()
		DurabilityState.BROKEN:
			_build_broken_visual(false)
		DurabilityState.FULLY_BROKEN:
			_build_broken_visual(true)

func _build_intact_visual() -> void:
	var glass := _bottle_glass_material(false)
	var imported: Node = BottleModelScene.instantiate()
	var mesh_source := _find_mesh_instance(imported)
	if mesh_source != null:
		var inst := MeshInstance3D.new()
		inst.mesh = mesh_source.mesh
		inst.material_override = glass
		_visual_root.add_child(inst)
	imported.free()

## Broken (dull=false) or fully-broken (dull=true, per the plan's own
## "reuse geometry, swap material only" scoping) -- the neck stops well
## short of the full height, ending in a small fixed (not runtime-random,
## for determinism/testability) fan of angular shard triangles instead of a
## smooth rim cap.
func _build_broken_visual(dull: bool) -> void:
	var glass := _bottle_glass_material(dull)
	_add_tube(Vector3(0, 0, 0), Vector3(0, BODY_HEIGHT * 0.15, 0), BODY_RADIUS * 0.85, BODY_RADIUS, glass)
	_add_tube(Vector3(0, BODY_HEIGHT * 0.15, 0), Vector3(0, BODY_HEIGHT, 0), BODY_RADIUS, BODY_RADIUS, glass)
	_add_tube(Vector3(0, BODY_HEIGHT, 0), Vector3(0, BODY_HEIGHT + SHOULDER_HEIGHT, 0), BODY_RADIUS, NECK_RADIUS, glass)
	var break_y := BODY_HEIGHT + SHOULDER_HEIGHT + NECK_HEIGHT * 0.45
	_add_tube(Vector3(0, BODY_HEIGHT + SHOULDER_HEIGHT, 0), Vector3(0, break_y, 0), NECK_RADIUS, NECK_RADIUS, glass)
	_build_jagged_rim(break_y, NECK_RADIUS, glass)
	_build_label()

## Depth-first search for the first MeshInstance3D under `node` -- the
## imported bottle glb has exactly one (a single decimated mesh, no need to
## look up by name the way the multi-piece crimson claw monster does).
static func _find_mesh_instance(node: Node) -> MeshInstance3D:
	if node is MeshInstance3D:
		return node
	for child in node.get_children():
		var found := _find_mesh_instance(child)
		if found != null:
			return found
	return null

func _bottle_glass_material(dull: bool) -> StandardMaterial3D:
	if dull:
		return _flat_material(DULL_GLASS_COLOR, 0.0, 0.75)
	var mat := _flat_material(GLASS_COLOR, 0.1, 0.15)
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.albedo_color.a = GLASS_ALPHA
	return mat

func _build_jagged_rim(rim_y: float, radius: float, mat: Material) -> void:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var shard_heights := [0.014, 0.006, 0.011, 0.004, 0.013, 0.007, 0.010, 0.005]
	var shard_count := shard_heights.size()
	for i in range(shard_count):
		var angle0 := TAU * float(i) / float(shard_count)
		var angle1 := TAU * float(i + 1) / float(shard_count)
		var base0 := Vector3(cos(angle0) * radius, rim_y, sin(angle0) * radius)
		var base1 := Vector3(cos(angle1) * radius, rim_y, sin(angle1) * radius)
		var mid_angle := (angle0 + angle1) * 0.5
		var tip := Vector3(cos(mid_angle) * radius * 0.7, rim_y + shard_heights[i], sin(mid_angle) * radius * 0.7)
		_tri(st, base0, base1, tip)
	var mesh_inst := MeshInstance3D.new()
	mesh_inst.mesh = st.commit()
	mesh_inst.material_override = mat
	_visual_root.add_child(mesh_inst)

## A plain flat accent, not a texture/decal -- an ORIGINAL shape, not a
## reproduction of any real bottle's actual label artwork.
func _build_label() -> void:
	var label := MeshInstance3D.new()
	var box := BoxMesh.new()
	box.size = Vector3(BODY_RADIUS * 1.3, BODY_HEIGHT * 0.45, 0.002)
	label.mesh = box
	label.material_override = _flat_material(LABEL_COLOR, 0.0, 0.9)
	label.position = Vector3(0, BODY_HEIGHT * 0.5, BODY_RADIUS * 0.98)
	_visual_root.add_child(label)

func _process(_delta: float) -> void:
	if _picked_up:
		_update_held_pose()
		return
	if _nearby_player == null:
		return
	if not Input.is_action_just_pressed("interact"):
		return
	if not _player_is_looking_at_me(_nearby_player):
		return
	_try_add_to_inventory(_nearby_player)

## No per-animation pose switching the way the sword's own heavy-attack
## grip needs (see two_handed_sword.gd's own _update_held_pose comment) --
## a bottle has exactly one fixed swing, so exactly one grip pose.
func _update_held_pose() -> void:
	if _manual_override:
		return
	rotation = GRIP_HAND_ROTATION
	position = GRIP_HAND_POSITION

func set_manual_override(enabled: bool) -> void:
	_manual_override = enabled

## Unlike weapons/pistol.gd's own print_current_pose() (position/rotation/
## scale all live on the SAME node, since GRIP_SCALE is applied directly
## via _update_held_pose()), this bottle's own SCALE lives on a separate
## child (VisualRoot, see _ready()'s own comment on why: this root is ALSO
## the grip anchor _update_held_pose() drives every frame, so scaling it
## directly would distort the grip offset math too) -- printed here
## alongside position/rotation so both halves of "place and resize the
## bottle" read back from one single command instead of needing to unlock/
## inspect two different nodes separately.
func print_current_pose() -> void:
	print(name, " position = ", position)
	print(name, " rotation (radians) = ", rotation)
	print(name, " rotation (degrees) = ", rotation * 180.0 / PI)
	print(name, "/VisualRoot scale = ", _visual_root.scale, " (this is what BOTTLE_SCALE ends up as -- edit VisualRoot's own Scale in the Remote tab, not this node's)")

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

func _equip(player: CharacterBody3D) -> void:
	var character: Node = player.get_node_or_null("Character")
	if character == null or not character.has_method("get_hand_node"):
		return
	var hand: Node3D = character.get_hand_node()
	if hand == null:
		return
	# Replace whatever's already equipped -- same "one weapon at a time"
	# reasoning every other weapon's own _equip() already applies.
	for existing in hand.get_children():
		existing.queue_free()
	_picked_up = true
	_wielding_character = character
	_interact_area.monitoring = false
	# Stops real physics simulation the instant it's equipped -- see this
	# file's own class comment on why freeze is what lets this coexist with
	# _update_held_pose()'s own direct position/rotation writes below (and
	# every frame after) without the physics engine fighting them. Also no
	# longer a valid grab-carry target once it's in your own hand (see
	# player.gd's own grab/carry system) -- remove_from_group is a safe
	# no-op if grab/carry already had it (this bottle was mid-carry, then
	# equipped instead of dropped -- can't happen via normal input today
	# since grab-carry and weapon-equip both key off "interact," but this
	# keeps the state consistent regardless).
	freeze = true
	linear_velocity = Vector3.ZERO
	angular_velocity = Vector3.ZERO
	remove_from_group("grabbable")
	get_parent().remove_child(self)
	hand.add_child(self)
	rotation = GRIP_HAND_ROTATION
	position = GRIP_HAND_POSITION
	if character.has_method("set_wielding_bottle"):
		character.set_wielding_bottle(true)
	if player.has_method("equip_bottle"):
		player.equip_bottle(self)

func _on_interact_area_body_entered(body: Node3D) -> void:
	if body is CharacterBody3D and body.has_method("respawn"):
		_nearby_player = body

func _on_interact_area_body_exited(body: Node3D) -> void:
	if body == _nearby_player:
		_nearby_player = null

# --- Duplicated from weapons/two_handed_sword.gd (credited, see this
# file's own class comment for why this is a standalone copy rather than a
# subclass) -- genuinely parameter-driven, no sword-specific state baked in. ---

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
	_visual_root.add_child(mesh_inst)

func _tri(st: SurfaceTool, a: Vector3, b: Vector3, c: Vector3) -> void:
	var n := (b - a).cross(c - a).normalized()
	st.set_normal(n)
	st.add_vertex(a)
	st.set_normal(n)
	st.add_vertex(b)
	st.set_normal(n)
	st.add_vertex(c)
