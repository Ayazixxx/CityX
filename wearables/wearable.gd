extends Node3D
## Shared base for every pick-up-able head accessory (top_hat.gd,
## hockey_mask.gd, and any new one) -- unlike the swords (which started as
## ONE script and only grew a shared base once a genuine second, similar
## case showed up), this starts with two wearables at once with an explicit
## "more to come" ask, so the shared pickup/equip/slot-replacement logic is
## extracted up front instead of duplicating it twice on day one.
##
## Same "walk up, look at it, press E" InteractArea pattern every holdable
## prop in this project already uses (see weapons/pistol.gd's own class
## comment), just attached to character.gd's own get_head_node() instead of
## get_hand_node(). SLOT-based (see get_wearable_slot()) rather than
## "replace anything already worn" the way a second weapon would -- a hat
## and a mask occupy different slots and can be worn at the same time;
## equipping a second item in the SAME slot (e.g. a second hat) replaces
## the first.
##
## _head_position()/_head_rotation() are measured against character.gd's
## own fixed head geometry (_define_parts's own head_size := Vector3(0.7,
## 0.7, 0.7), offset := Vector3(0, 0.35, 0) from get_head_node()'s own
## pivot, which itself sits at the neck-top joint, not the head's visual
## center) -- every subclass MUST override both. These are METHODS, not
## consts: GDScript does not allow a subclass to redeclare a const that
## already exists in its parent (a real parse error hit here directly --
## "The member ... already exists in parent class" -- confirmed this is
## exactly why weapons/martyr_blade.gd never redeclares two_handed_sword.gd's
## own GRIP_HAND_POSITION/GRIP_HAND_ROTATION rather than just happening to
## reuse the same values).

const INTERACT_RANGE := 2.5

@onready var _interact_area: Area3D = $InteractArea

var _nearby_player: CharacterBody3D = null
var _picked_up := false
# Dev-only escape hatch for live-tuning position/rotation after equip -- same
# workflow/reasoning as every sword/gun's own _manual_override (see
# two_handed_sword.gd's own comment): unlocked, this stops _equip() (and,
# unlike a weapon's per-frame _update_held_pose, there's no other per-frame
# repositioning fighting a manual edit here at all -- see _build_seats'
# own comment on car.gd for the same "set once in _ready(), never
# touched again" reasoning) from being the last thing to move it, so it can
# be dragged directly via the Debugger panel's Remote tab instead.
var _manual_override := false

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

## Proximity-only, no look-dot-threshold -- unlike a weapon (picked up at a
## raised, planted height, see weapons/pistol.gd's own _player_is_looking_
## at_me), a wearable sits flush on the ground. A weapon-style check (eye-
## height origin, a fairly tight dot threshold) geometrically can't be
## satisfied here with a level camera: from GROUND level, the shallow
## viewing angle needed to clear the dot threshold only opens up past
## roughly 2.15m out, but by then the actual eye-to-item distance already
## exceeds a weapon's own INTERACT_RANGE -- there's no distance where both
## checks pass at once. Same fix vehicles/car.gd's own InteractArea already
## uses for the same underlying reason (a big target there, a low one
## here): proximity alone is enough.
func _process(_delta: float) -> void:
	if _picked_up:
		return
	if _nearby_player != null and Input.is_action_just_pressed("interact"):
		_try_add_to_inventory(_nearby_player)

## Overridden by each subclass -- "hat"/"mask"/etc, see this file's own
## class comment on slot-based replacement.
func get_wearable_slot() -> String:
	return "wearable"

## Overridden by each subclass (top_hat.gd/hockey_mask.gd) with its own
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

## Overridden by each subclass to build its own procedural mesh, called
## once from _ready().
func _build_visual() -> void:
	pass

## Overridden by each subclass -- see this file's own class comment on why
## these are methods, not consts.
func _head_position() -> Vector3:
	return Vector3.ZERO

func _head_rotation() -> Vector3:
	return Vector3.ZERO

func _equip(player: CharacterBody3D) -> void:
	var character: Node = player.get_node_or_null("Character")
	if character == null or not character.has_method("get_head_node"):
		return
	var head: Node3D = character.get_head_node()
	if head == null:
		return
	# Replace whatever's already worn in THIS SAME slot -- see this file's
	# own class comment on why a hat and a mask don't evict each other.
	for existing in head.get_children():
		if existing != self and existing.has_method("get_wearable_slot") and existing.get_wearable_slot() == get_wearable_slot():
			existing.queue_free()
	_picked_up = true
	_interact_area.monitoring = false
	get_parent().remove_child(self)
	head.add_child(self)
	position = _head_position()
	rotation = _head_rotation()

func set_manual_override(enabled: bool) -> void:
	_manual_override = enabled

func print_current_pose() -> void:
	print(get_wearable_slot(), " position = ", position)
	print(get_wearable_slot(), " rotation (radians) = ", rotation)
	print(get_wearable_slot(), " rotation (degrees) = ", rotation * 180.0 / PI)

func _on_interact_area_body_entered(body: Node3D) -> void:
	if body is CharacterBody3D and body.has_method("respawn"):
		_nearby_player = body

func _on_interact_area_body_exited(body: Node3D) -> void:
	if body == _nearby_player:
		_nearby_player = null
