extends Node3D
## Shared base for torso-slot clothing (wearables/t_shirt.gd,
## wearables/hoodie.gd) -- built as SEVERAL small rigid pieces, each
## eventually parented onto its own character.gd get_bone_node() (spine,
## arm_l/r, forearm_l/r, neck), rather than the ONE prop under ONE
## BoneAttachment3D every wearables/wearable.gd head accessory uses. A shirt
## needs to loosely follow the torso AND both arms, which rotate
## independently of each other and of the torso (a punch swings arm_l/r
## roughly 90 degrees on its own) -- one rigid prop under a single bone
## can't do that. This is coarse, per-BONE rigid attachment, NOT true
## per-vertex skinning like the body's own fused mesh (see
## character/fused_body.gd) -- a visible seam at the shoulder mid-swing is
## an accepted limitation of this first "just the model" pass (color/
## texture/uploaded-PNG customization is explicit FUTURE work too); real
## soft-skinned cloth would need the same marching-tetrahedra machinery the
## body itself uses, well beyond this scope.
##
## Same pickup pattern as wearable.gd (walk up, look, E) -- duplicated
## rather than subclassed, since wearable.gd's own _equip() hardcodes a
## single parent (get_head_node()) that doesn't fit a multi-bone prop, and
## proximity-only detection for the same geometric reason wearable.gd's own
## class comment already gives (a ground-level item, a level camera, no
## angle where both a distance AND a look-dot check pass at once).
##
## Every piece is authored in the SAME "rest-pose" coordinate convention
## character/fused_body.gd's own limb_defs()/bone_origins() use (e.g.
## shoulder height SHOULDER_Y) via _add_piece()'s rest_transform, so a piece
## drawn to visually line up with the standing character's own proportions
## lines up automatically while still lying on the ground, pre-equip.
## _equip() converts that into each target bone's own local space by
## subtracting FusedBody.bone_origins()[bone] from the transform's origin
## (a BoneAttachment3D's own origin sits exactly at that bone's rest pivot,
## see fused_body.gd's own class comment on bind pose), then reparents --
## the same mechanism weapons already rely on via get_hand_node(), just
## applied to several bones instead of one.

const FusedBody = preload("res://character/fused_body.gd")
const INTERACT_RANGE := 2.5

@onready var _interact_area: Area3D = $InteractArea

var _nearby_player: CharacterBody3D = null
var _picked_up := false

# Array[Dictionary] of {"node": MeshInstance3D, "bone": String, "transform": Transform3D} --
# "transform" is the piece's own REST-POSE transform (see this file's own
# class comment), kept alongside "node" so _equip() can re-derive each
# piece's bone-local transform without re-deriving the mesh itself.
var _pieces: Array[Dictionary] = []

func _ready() -> void:
	_build_visual()
	_interact_area.collision_layer = 0
	_interact_area.collision_mask = 4
	var shape := CollisionShape3D.new()
	var sphere := SphereShape3D.new()
	sphere.radius = INTERACT_RANGE
	shape.shape = sphere
	_interact_area.add_child(shape)
	_interact_area.body_entered.connect(_on_interact_area_body_entered)
	_interact_area.body_exited.connect(_on_interact_area_body_exited)

func _process(_delta: float) -> void:
	if _picked_up:
		return
	if _nearby_player != null and Input.is_action_just_pressed("interact"):
		_try_add_to_inventory(_nearby_player)

## Overridden by each subclass -- "top" (see this file's own class comment:
## a t-shirt and hoodie occupy the SAME slot, since this is a single-layer
## "just the model" pass with no separate under/over-shirt layering yet).
func get_wearable_slot() -> String:
	return "top"

## Overridden by each subclass (t_shirt.gd/hoodie.gd) with its own
## items/item_registry.gd lookup key -- same "subclass override METHOD, not
## const" reasoning as get_wearable_slot() above.
func get_item_id() -> String:
	return ""

## See weapons/pistol.gd's own _try_add_to_inventory() for the shared
## reasoning -- picking this up now adds it to the inventory instead of
## equipping it directly; _equip() below is unchanged, just called from
## Inventory.equip_from_slot() on a freshly instantiated copy now.
func _try_add_to_inventory(player: CharacterBody3D) -> void:
	var inv: Node = player.get_node_or_null("Inventory")
	if inv == null or not inv.has_method("add_item"):
		return
	if inv.call("add_item", get_item_id(), 1):
		_interact_area.monitoring = false
		queue_free()

## Overridden by each subclass to build its own procedural pieces via
## _add_piece()/_add_tube(), called once from _ready().
func _build_visual() -> void:
	pass

## rest_transform: see this file's own class comment -- authored in
## rest-pose world-like coordinates, NOT yet bone-local.
func _add_piece(mesh_inst: MeshInstance3D, bone: String, rest_transform: Transform3D) -> void:
	mesh_inst.transform = rest_transform
	add_child(mesh_inst)
	_pieces.append({"node": mesh_inst, "bone": bone, "transform": rest_transform})

## Same parameter-driven tube primitive weapons/two_handed_sword.gd's own
## _add_tube first established (credited there, re-duplicated since by
## weapons/glass_bottle.gd and weapons/thrown_bottle.gd -- see their own
## class comments) -- genuinely no state baked in, just two rest-pose
## points. Builds AND registers the piece in one call via _add_piece().
func _add_tube(a: Vector3, b: Vector3, radius_a: float, radius_b: float, mat: Material, bone: String) -> MeshInstance3D:
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
	_add_piece(mesh_inst, bone, Transform3D(Basis(x_axis, y_axis, z_axis), mid))
	return mesh_inst

func _equip(player: CharacterBody3D) -> void:
	var character: Node = player.get_node_or_null("Character")
	if character == null or not character.has_method("get_bone_node"):
		return
	# Replace whatever top was worn before -- see character.gd's own
	# clear_worn_top() class comment for why this lives there instead of
	# the "look at my future siblings under one shared parent" trick
	# wearable.gd's own _equip() uses.
	if character.has_method("clear_worn_top"):
		character.clear_worn_top()
	_picked_up = true
	_interact_area.monitoring = false
	var origins: Dictionary = FusedBody.bone_origins()
	var equipped_parts: Array[Node3D] = []
	for piece in _pieces:
		var node: MeshInstance3D = piece["node"]
		var bone: String = piece["bone"]
		var attachment: Node3D = character.get_bone_node(bone)
		remove_child(node)
		attachment.add_child(node)
		var pivot: Vector3 = origins.get(bone, Vector3.ZERO)
		var rest_transform: Transform3D = piece["transform"]
		node.transform = Transform3D(rest_transform.basis, rest_transform.origin - pivot)
		equipped_parts.append(node)
	if character.has_method("set_worn_top"):
		character.set_worn_top(equipped_parts)
	# self isn't needed anymore -- every visual piece has been reparented
	# onto its own bone attachment above, so nothing is left under self but
	# the (now-disabled) InteractArea.
	queue_free()

func _on_interact_area_body_entered(body: Node3D) -> void:
	if body is CharacterBody3D and body.has_method("respawn"):
		_nearby_player = body

func _on_interact_area_body_exited(body: Node3D) -> void:
	if body == _nearby_player:
		_nearby_player = null
