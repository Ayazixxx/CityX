extends RigidBody3D
## A grappling hook: aim at a building, click to fire, and swing. The item
## itself is only the launcher and the rope VISUAL -- the actual swing
## physics live in player.gd (see its own start_grapple/_process_grapple_
## swing), because they replace the player's whole movement mode for as long
## as the rope is attached, and that has to happen inside the same
## _physics_process that owns velocity and move_and_slide().
##
## Same pickup/equip lifecycle as every other held item (see
## items/flashlight.gd): InteractArea -> inventory -> equip_from_slot()
## reparents a fresh copy onto the character's own hand bone.
##
## The rope is a single stretched cylinder re-oriented every frame between
## the muzzle and the anchor, rather than a real segmented/simulated rope.
## A rope that visibly sags would need its own solver and a chain of
## segments to draw; a taut line is both what a grappling hook under load
## actually looks like and what honestly represents the physics underneath
## (player.gd's own constraint IS a rigid distance, not a spring).

const BODY_LENGTH := 0.22
const BODY_RADIUS := 0.035
const BARREL_LENGTH := 0.16
const BARREL_RADIUS := 0.018
const BODY_COLOR := Color(0.2, 0.22, 0.26)
const BARREL_COLOR := Color(0.5, 0.52, 0.56)
const HOOK_COLOR := Color(0.62, 0.64, 0.68)

const ROPE_RADIUS := 0.018
const ROPE_COLOR := Color(0.14, 0.13, 0.12)
# The flying hook head parked at the anchor while attached -- purely
# cosmetic, so the rope visibly ends in something rather than at a point in
# mid-air.
const HOOK_HEAD_SIZE := 0.09

const HOOK_MASS := 1.1
const GRIP_HAND_POSITION := Vector3(0.0, 0.03, -0.05)
# Same hand-frame convention every other held item uses -- see
# items/flashlight.gd's own GRIP_HAND_ROTATION.
const GRIP_HAND_ROTATION := Vector3(deg_to_rad(-90.0), 0.0, 0.0)

@onready var _interact_area: Area3D = $InteractArea

var _nearby_player: CharacterBody3D = null
var _picked_up := false
var _holder: CharacterBody3D = null
# Rope + hook head live in the WORLD, not under this node: both span from
# the hand to an anchor tens of metres away, and a child of a bone that's
# being animated (and may carry its own scale) is the wrong space to
# express that in. Freed whenever the rope detaches.
var _rope: MeshInstance3D = null
var _hook_head: MeshInstance3D = null
var _muzzle: Node3D

func _ready() -> void:
	_build_visual()
	_interact_area.body_entered.connect(_on_interact_area_body_entered)
	_interact_area.body_exited.connect(_on_interact_area_body_exited)

	mass = HOOK_MASS
	var collision := CollisionShape3D.new()
	var capsule := CapsuleShape3D.new()
	capsule.radius = BODY_RADIUS
	capsule.height = BODY_LENGTH + BARREL_LENGTH
	collision.shape = capsule
	collision.rotation = Vector3(deg_to_rad(90.0), 0, 0)
	add_child(collision)
	add_to_group("grabbable")

func _build_visual() -> void:
	var body_material := StandardMaterial3D.new()
	body_material.albedo_color = BODY_COLOR
	body_material.roughness = 0.55
	body_material.metallic = 0.4

	var body := MeshInstance3D.new()
	var body_mesh := BoxMesh.new()
	body_mesh.size = Vector3(BODY_RADIUS * 2.0, BODY_RADIUS * 2.4, BODY_LENGTH)
	body.mesh = body_mesh
	body.material_override = body_material
	add_child(body)

	var barrel_material := StandardMaterial3D.new()
	barrel_material.albedo_color = BARREL_COLOR
	barrel_material.metallic = 0.8
	barrel_material.roughness = 0.3

	var barrel := MeshInstance3D.new()
	var barrel_mesh := CylinderMesh.new()
	barrel_mesh.top_radius = BARREL_RADIUS
	barrel_mesh.bottom_radius = BARREL_RADIUS
	barrel_mesh.height = BARREL_LENGTH
	barrel.mesh = barrel_mesh
	barrel.material_override = barrel_material
	# Laid along -Z (the aiming direction) rather than the cylinder's own +Y.
	barrel.rotation = Vector3(deg_to_rad(90.0), 0, 0)
	barrel.position = Vector3(0, 0, -(BODY_LENGTH * 0.5 + BARREL_LENGTH * 0.5))
	add_child(barrel)

	# Where the rope is drawn FROM. A plain marker rather than geometry, so
	# the rope's own origin tracks the barrel tip through every animation
	# the hand happens to be playing.
	_muzzle = Node3D.new()
	_muzzle.position = Vector3(0, 0, -(BODY_LENGTH * 0.5 + BARREL_LENGTH))
	add_child(_muzzle)

## items/item_registry.gd's own lookup key.
func get_item_id() -> String:
	return "grappling_hook"

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
	for existing in hand.get_children():
		existing.queue_free()
	_picked_up = true
	_holder = player
	_interact_area.monitoring = false
	freeze = true
	linear_velocity = Vector3.ZERO
	angular_velocity = Vector3.ZERO
	remove_from_group("grabbable")
	get_parent().remove_child(self)
	hand.add_child(self)
	rotation = GRIP_HAND_ROTATION
	position = GRIP_HAND_POSITION
	if player.has_method("equip_grappling_hook"):
		player.equip_grappling_hook(self)

## Fires at whatever the crosshair is pointing at. Returns the anchor point,
## or a null-ish result the caller can test -- player.gd owns the decision
## about whether a shot that hit nothing should do anything.
##
## World geometry ONLY (collision mask 1): buildings, bridges, the ground.
## Deliberately NOT other characters -- a rope anchored to a person who then
## walks away is a physics problem with no good answer, and "grapple onto an
## enemy" is a different mechanic from "swing between buildings".
func find_anchor(from: Vector3, direction: Vector3, max_range: float) -> Dictionary:
	var query := PhysicsRayQueryParameters3D.create(from, from + direction * max_range)
	query.collision_mask = 1
	if _holder != null and is_instance_valid(_holder):
		query.exclude = [_holder]
	return get_world_3d().direct_space_state.intersect_ray(query)

## Builds the rope + hook-head visuals and shows them. Called by player.gd
## the moment a grapple actually attaches.
func show_rope(anchor: Vector3) -> void:
	_clear_rope()
	var scene_root := get_tree().current_scene
	if scene_root == null:
		return

	var rope_material := StandardMaterial3D.new()
	rope_material.albedo_color = ROPE_COLOR
	rope_material.roughness = 0.9

	_rope = MeshInstance3D.new()
	var rope_mesh := CylinderMesh.new()
	rope_mesh.top_radius = ROPE_RADIUS
	rope_mesh.bottom_radius = ROPE_RADIUS
	# Unit height, then scaled to the real span every frame in
	# update_rope() -- avoids rebuilding the mesh resource each time the
	# rope length changes (which it does constantly while reeling).
	rope_mesh.height = 1.0
	rope_mesh.radial_segments = 6
	_rope.mesh = rope_mesh
	_rope.material_override = rope_material
	scene_root.add_child(_rope)

	var hook_material := StandardMaterial3D.new()
	hook_material.albedo_color = HOOK_COLOR
	hook_material.metallic = 0.8
	hook_material.roughness = 0.35

	_hook_head = MeshInstance3D.new()
	var head_mesh := BoxMesh.new()
	head_mesh.size = Vector3(HOOK_HEAD_SIZE, HOOK_HEAD_SIZE, HOOK_HEAD_SIZE)
	_hook_head.mesh = head_mesh
	_hook_head.material_override = hook_material
	scene_root.add_child(_hook_head)
	_hook_head.global_position = anchor

	update_rope(anchor)

## Re-stretches the rope between the muzzle and the anchor. Called every
## frame by player.gd while swinging, since BOTH ends move -- the player
## swings, and the hand the muzzle rides is being animated.
func update_rope(anchor: Vector3) -> void:
	if _rope == null or not is_instance_valid(_rope):
		return
	var from: Vector3 = _muzzle.global_position if _muzzle != null else global_position
	var span := anchor - from
	var length := span.length()
	if length < 0.01:
		_rope.visible = false
		return
	_rope.visible = true
	# A CylinderMesh runs along its own +Y, so the rope is built by pointing
	# +Y down the span. looked_at() gives a basis whose -Z faces the target,
	# so the extra -90 degree X rotation swings +Y onto that -Z.
	var mid := from + span * 0.5
	var basis := Basis()
	var up := span / length
	var helper := Vector3.UP if absf(up.dot(Vector3.UP)) < 0.95 else Vector3.RIGHT
	var right := helper.cross(up).normalized()
	var forward := up.cross(right).normalized()
	basis = Basis(right, up, forward)
	_rope.global_transform = Transform3D(basis, mid)
	_rope.scale = Vector3(1.0, length, 1.0)

	if _hook_head != null and is_instance_valid(_hook_head):
		_hook_head.global_position = anchor

func hide_rope() -> void:
	_clear_rope()

func _clear_rope() -> void:
	if _rope != null and is_instance_valid(_rope):
		_rope.queue_free()
	_rope = null
	if _hook_head != null and is_instance_valid(_hook_head):
		_hook_head.queue_free()
	_hook_head = null

## The rope must never outlive the launcher -- unequipping or dropping mid-
## swing would otherwise leave a line hanging in the sky forever.
func _exit_tree() -> void:
	_clear_rope()

func _process(_delta: float) -> void:
	if _picked_up:
		return
	if _nearby_player == null:
		return
	if Input.is_action_just_pressed("interact"):
		_try_add_to_inventory(_nearby_player)

func _on_interact_area_body_entered(body: Node3D) -> void:
	if body is CharacterBody3D and body.is_in_group("players"):
		_nearby_player = body

func _on_interact_area_body_exited(body: Node3D) -> void:
	if body == _nearby_player:
		_nearby_player = null
