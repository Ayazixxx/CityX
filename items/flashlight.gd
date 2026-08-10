extends RigidBody3D
## A hand-held flashlight: pick it up (interact), equip it from the
## inventory, press F (toggle_light) to switch the beam on and off.
##
## Same pickup/equip lifecycle every weapon already uses -- an InteractArea
## adds it to the inventory rather than equipping directly (see
## weapons/glass_bottle.gd's own _try_add_to_inventory for the shared
## reasoning), and Inventory.equip_from_slot() calls _equip() on a fresh
## copy, which reparents it under the character's own hand bone.
##
## The beam is a REAL SpotLight3D, not a fake cone mesh -- it genuinely
## lights the geometry it's pointed at, which is the entire point given the
## dark building interiors it exists for (see player.gd's own
## enter_building()/exit_building() indoor dimming).
##
## The light stays with the flashlight rather than being attached to the
## camera: dropped on the ground still switched on, it keeps lighting the
## room from wherever it landed. That falls out for free from it being a
## child of this node instead of the player.

const BODY_LENGTH := 0.2
const BODY_RADIUS := 0.028
const HEAD_LENGTH := 0.07
const HEAD_RADIUS := 0.042

const BODY_COLOR := Color(0.14, 0.15, 0.17)
const HEAD_COLOR := Color(0.55, 0.56, 0.6)
const LENS_ON_COLOR := Color(1.0, 0.96, 0.85)

# Aimed along the flashlight's own -Z (the direction the head points), so
# the beam follows wherever the hand is pointing once equipped.
const BEAM_RANGE := 22.0
const BEAM_ANGLE_DEGREES := 26.0
# Bright enough to genuinely read as the only light source in a pitch-dark
# basement, which is the case this was built for.
const BEAM_ENERGY := 6.0
const BEAM_COLOR := Color(1.0, 0.97, 0.88)
# A soft edge rather than a hard-cut circle -- a hard cone edge reads as a
# stage spotlight, not a torch.
const BEAM_ATTENUATION := 1.2
const BEAM_ANGLE_ATTENUATION := 0.35

const FLASHLIGHT_MASS := 0.4
## Same layer weapons/glass_bottle.gd's own BOTTLE_COLLISION_LAYER uses (and
## thrown_bottle.gd before it): "small dynamic prop", deliberately NOT the
## world-geometry layer the player's own body collides with. See _ready().
const HELD_PROP_COLLISION_LAYER := 16
const GRIP_HAND_POSITION := Vector3(0.0, 0.03, -0.04)
# Rotated so the barrel runs out along the hand's own pointing direction
# rather than sticking out sideways -- same hand-frame convention every
# other held item's own GRIP_HAND_ROTATION already uses.
const GRIP_HAND_ROTATION := Vector3(deg_to_rad(-90.0), 0.0, 0.0)

@onready var _interact_area: Area3D = $InteractArea

var _spot: SpotLight3D
var _lens_mat: StandardMaterial3D
var _nearby_player: CharacterBody3D = null
var _picked_up := false
var _is_on := false
# The wielder's own camera, captured at equip time -- while held, the beam
# is aimed down THIS rather than down the flashlight's own barrel. See
# _aim_beam_at_crosshair.
var _holder_cam: Camera3D = null

func _ready() -> void:
	_build_visual()
	_interact_area.body_entered.connect(_on_interact_area_body_entered)
	_interact_area.body_exited.connect(_on_interact_area_body_exited)

	mass = FLASHLIGHT_MASS
	# THE SMALL-DYNAMIC-PROP LAYER, and it has to be set explicitly: a
	# RigidBody3D defaults to collision_layer 1, which is the WORLD GEOMETRY
	# layer buildings and the ground use -- and player.gd's own body masks
	# exactly that. Left on the default, the flashlight in your own hand is
	# a wall as far as your own move_and_slide() is concerned: it sweeps
	# through your capsule with every stride of the run cycle and the solver
	# shoves you off it, which is the reported "the model gets caught on
	# it". weapons/glass_bottle.gd already sets 16 for this same reason.
	#
	# 16 keeps it grabbable (player.gd's own _find_grab_target casts against
	# 1|8|16) and still lets it fall onto the world, since its own
	# collision_MASK is untouched -- what changed is only what collides with
	# IT, not what it collides with.
	collision_layer = HELD_PROP_COLLISION_LAYER
	var collision := CollisionShape3D.new()
	var capsule := CapsuleShape3D.new()
	capsule.radius = BODY_RADIUS
	capsule.height = BODY_LENGTH + HEAD_LENGTH
	collision.shape = capsule
	# Lying on its side is how a dropped torch actually rests, and the
	# capsule's own long axis is Y, so it's turned to match the barrel.
	collision.rotation = Vector3(deg_to_rad(90.0), 0, 0)
	add_child(collision)
	add_to_group("grabbable")

func _build_visual() -> void:
	var body_mat := StandardMaterial3D.new()
	body_mat.albedo_color = BODY_COLOR
	body_mat.roughness = 0.6
	var head_mat := StandardMaterial3D.new()
	head_mat.albedo_color = HEAD_COLOR
	head_mat.roughness = 0.35
	head_mat.metallic = 0.6

	var body := MeshInstance3D.new()
	var body_mesh := CylinderMesh.new()
	body_mesh.top_radius = BODY_RADIUS
	body_mesh.bottom_radius = BODY_RADIUS
	body_mesh.height = BODY_LENGTH
	body.mesh = body_mesh
	body.material_override = body_mat
	# Barrel laid along -Z (the beam direction) rather than the cylinder's
	# own default +Y.
	body.rotation = Vector3(deg_to_rad(90.0), 0, 0)
	body.position = Vector3(0, 0, BODY_LENGTH / 2.0)
	add_child(body)

	var head := MeshInstance3D.new()
	var head_mesh := CylinderMesh.new()
	head_mesh.top_radius = HEAD_RADIUS
	head_mesh.bottom_radius = BODY_RADIUS
	head_mesh.height = HEAD_LENGTH
	head.mesh = head_mesh
	head.material_override = head_mat
	head.rotation = Vector3(deg_to_rad(-90.0), 0, 0)
	head.position = Vector3(0, 0, -HEAD_LENGTH / 2.0)
	add_child(head)

	# The lens itself glows while lit, so the flashlight reads as ON from
	# the side too -- not just from whatever its beam happens to land on.
	_lens_mat = StandardMaterial3D.new()
	_lens_mat.albedo_color = LENS_ON_COLOR
	_lens_mat.emission_enabled = true
	_lens_mat.emission = LENS_ON_COLOR
	_lens_mat.emission_energy_multiplier = 0.0
	var lens := MeshInstance3D.new()
	var lens_mesh := CylinderMesh.new()
	lens_mesh.top_radius = HEAD_RADIUS * 0.92
	lens_mesh.bottom_radius = HEAD_RADIUS * 0.92
	lens_mesh.height = 0.006
	lens.mesh = lens_mesh
	lens.material_override = _lens_mat
	lens.rotation = Vector3(deg_to_rad(90.0), 0, 0)
	lens.position = Vector3(0, 0, -HEAD_LENGTH - 0.003)
	add_child(lens)

	_spot = SpotLight3D.new()
	_spot.light_color = BEAM_COLOR
	_spot.light_energy = BEAM_ENERGY
	_spot.spot_range = BEAM_RANGE
	_spot.spot_angle = BEAM_ANGLE_DEGREES
	_spot.spot_attenuation = BEAM_ATTENUATION
	_spot.spot_angle_attenuation = BEAM_ANGLE_ATTENUATION
	_spot.shadow_enabled = true
	_spot.visible = false
	# SpotLight3D shines down its own -Z, which is already the barrel's
	# direction here, so it needs no rotation of its own. Nudged just past
	# the lens so the head's own geometry doesn't sit inside the cone and
	# cast a shadow ring back down the beam.
	_spot.position = Vector3(0, 0, -HEAD_LENGTH - 0.02)
	add_child(_spot)

## items/item_registry.gd's own lookup key.
func get_item_id() -> String:
	return "flashlight"

## Whether the beam survives pickup -> inventory -> equip again, the same
## way glass_bottle.gd carries its durability across that round trip.
func _capture_instance_data() -> Dictionary:
	return {"light_on": _is_on}

func set_light_on(on: bool) -> void:
	_is_on = on
	# _ready() may not have run yet when Inventory.equip_from_slot() restores
	# saved state onto a freshly instantiated copy -- same ordering caveat
	# glass_bottle.set_durability_state documents. _apply_light_state() is
	# called again unconditionally from _ready(), so either order works.
	if _spot != null:
		_apply_light_state()

func _apply_light_state() -> void:
	_spot.visible = _is_on
	_lens_mat.emission_energy_multiplier = 2.5 if _is_on else 0.0

func _try_add_to_inventory(player: CharacterBody3D) -> void:
	var inv: Node = player.get_node_or_null("Inventory")
	if inv == null or not inv.has_method("add_item"):
		return
	if inv.call("add_item", get_item_id(), 1, _capture_instance_data()):
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
	_holder_cam = player.get("camera")
	_apply_light_state()

## Points the beam wherever the player is actually looking, rather than
## wherever the hand bone happens to be pointing.
##
## A real reported bug: parented to the hand, the barrel follows whatever
## the current animation is doing with that arm -- which for the ordinary
## idle/walk clips is hanging down at the character's side, so the beam lit
## the floor at their feet instead of what they were aiming at. The
## flashlight MODEL still rides the hand (it should visibly be held), but
## the light itself is re-aimed every frame down the camera's own forward,
## which is exactly where the crosshair points.
func _aim_beam_at_crosshair() -> void:
	if _holder_cam == null or not is_instance_valid(_holder_cam):
		return
	# Position stays wherever the lens ended up on the hand; only the
	# orientation is overridden. A SpotLight3D shines down its own -Z, and
	# so does a Camera3D, so copying the camera's basis outright aims the
	# beam along the exact view direction with no extra rotation needed.
	_spot.global_transform = Transform3D(_holder_cam.global_transform.basis, _spot.global_position)

func _process(_delta: float) -> void:
	if _picked_up:
		# Only the player actually holding it can toggle it. No ownership
		# check beyond "am I equipped" is needed: an equipped flashlight is
		# parented under its own wielder's hand, and a remote peer's copy
		# never runs this branch because their input never reaches here.
		if Input.is_action_just_pressed("toggle_light"):
			_is_on = not _is_on
			_apply_light_state()
		if _is_on:
			_aim_beam_at_crosshair()
		return

	# On the ground: interact picks it up into the inventory.
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
