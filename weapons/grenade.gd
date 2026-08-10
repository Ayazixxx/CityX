extends RigidBody3D
## A grenade sitting in the world / held in hand: pick it up (interact), it
## goes to the inventory, equip it, then hold right-click to aim and click
## to throw. The actual flying, fuse-burning, exploding object is a separate
## node (see weapons/thrown_grenade.gd) spawned on release.
##
## Deliberately the SAME lifecycle and the SAME two-button aim/throw gesture
## the glass bottle already uses (see weapons/glass_bottle.gd and player.gd's
## own _equipped_bottle branch) rather than a new one -- this is the second
## throwable in the project, and a player who has thrown a bottle already
## knows how to throw this.
##
## Unlike the bottle, there is no melee use: swinging a live grenade at
## someone is not a mechanic, so player.gd's own grenade branch only wires
## the throw.
##
## Stacks in the inventory (see items/item_registry.gd's own stack_size) --
## it's ammunition, not a durable weapon, and it's consumed on throw.

const BODY_RADIUS := 0.045
const BODY_HEIGHT := 0.115
const SHELL_COLOR := Color(0.18, 0.24, 0.16)
const LEVER_COLOR := Color(0.45, 0.46, 0.48)

const GRENADE_MASS := 0.45
const GRIP_HAND_POSITION := Vector3(0.0, 0.02, -0.03)
# Same hand-frame convention every other held item's own GRIP_HAND_ROTATION
# uses -- see items/flashlight.gd.
const GRIP_HAND_ROTATION := Vector3(deg_to_rad(-90.0), 0.0, 0.0)

@onready var _interact_area: Area3D = $InteractArea

var _nearby_player: CharacterBody3D = null
var _picked_up := false

func _ready() -> void:
	_build_visual()
	_interact_area.body_entered.connect(_on_interact_area_body_entered)
	_interact_area.body_exited.connect(_on_interact_area_body_exited)

	mass = GRENADE_MASS
	var collision := CollisionShape3D.new()
	var shape := CapsuleShape3D.new()
	shape.radius = BODY_RADIUS
	shape.height = BODY_HEIGHT
	collision.shape = shape
	add_child(collision)
	add_to_group("grabbable")

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

	# No blinking fuse light here, unlike the thrown version -- the pin is
	# still in. It only starts counting down once it's actually thrown.

## items/item_registry.gd's own lookup key.
func get_item_id() -> String:
	return "grenade"

## Always true -- unlike glass_bottle.gd's own can_throw() (which refuses
## once the bottle is broken), a grenade has no damaged state to gate on.
## Kept so player.gd's throw path can stay the same shape for both.
func can_throw() -> bool:
	return true

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
	_interact_area.monitoring = false
	freeze = true
	linear_velocity = Vector3.ZERO
	angular_velocity = Vector3.ZERO
	remove_from_group("grabbable")
	get_parent().remove_child(self)
	hand.add_child(self)
	rotation = GRIP_HAND_ROTATION
	position = GRIP_HAND_POSITION
	# Reuses the bottle's own throwing moveset (windup pose on aim, throw
	# animation on release) rather than adding a grenade-specific one --
	# character.gd's own begin_bottle_aim/release_bottle_throw are a generic
	# overhand throw, and nothing in them is bottle-shaped.
	if character.has_method("set_wielding_bottle"):
		character.set_wielding_bottle(true)
	if player.has_method("equip_grenade"):
		player.equip_grenade(self)

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
