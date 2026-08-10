extends Node3D
## Attached to a corpse (a player's ragdoll hips capsule, or an NPC's
## single coarse ragdoll body -- see character.gd's own ragdoll() and
## npc/mutant.gd's/npc/crimson_claw_monster.gd's own _spawn_ragdoll()) to
## make it lootable: a quick TAP of "interact" while nearby transfers every
## item in loot_items into the looter's own inventory
## (player/inventory.gd's own add_item()) and marks itself looted so a
## second tap on an emptied corpse is a harmless no-op.
##
## Same "own Area3D proximity + something else polls interact" split
## radio/radio.gd's own class comment already documents for a DIFFERENT
## reason (there, radio.gd polls its own _process()) -- here, the PLAYER
## polls instead (see player.gd's own _process_corpse_loot()/
## _try_loot_nearest_corpse()), not this node itself, since looting has to
## coexist with the SAME "interact" press the grab/carry system already
## owns on these exact bodies (a ragdoll bone is independently tagged
## "grabbable"). See player.gd's own _process_corpse_loot() for the
## tap-vs-hold split that makes both work without colliding: a quick tap
## loots, a sustained hold still grabs/carries the bone under the
## crosshair, completely unchanged.
##
## Local-only -- not networked in this pass (see player/inventory.gd's own
## class comment on multiplayer scope: only what's visually EQUIPPED gets
## RPC'd across peers). Taking from a corpse is a local inventory mutation;
## the taking peer's own equip RPCs already cover making anything they
## equip FROM the loot visible to other peers.

const INTERACT_RANGE := 2.5

## [{"item_id": String, "count": int, "source_slot": String}, ...] -- set
## once by whoever constructs this (character.gd's own ragdoll(), or an
## NPC's own _spawn_ragdoll()-equivalent) right after instancing it.
## "source_slot" is only ever set for a PLAYER corpse's own equipped items
## (see _strip_source_slot()) -- an NPC's authored loot table entries have
## no equipment slot to strip, so they simply omit it.
var loot_items: Array[Dictionary] = []

## The dead player's own CharacterBody3D, if this corpse IS a player (null
## for an NPC corpse -- nothing on an NPC to strip, they never had a real
## Character/Inventory of their own).
var strip_target: Node = null

var _nearby_player: Node = null
var _looted := false

func _ready() -> void:
	add_to_group("lootable_corpses")
	var area := Area3D.new()
	area.collision_layer = 0
	area.collision_mask = 4
	var shape := CollisionShape3D.new()
	var sphere := SphereShape3D.new()
	sphere.radius = INTERACT_RANGE
	shape.shape = sphere
	area.add_child(shape)
	add_child(area)
	area.body_entered.connect(_on_body_entered)
	area.body_exited.connect(_on_body_exited)

func _on_body_entered(body: Node3D) -> void:
	if body is CharacterBody3D and body.has_method("respawn"):
		_nearby_player = body

func _on_body_exited(body: Node3D) -> void:
	if body == _nearby_player:
		_nearby_player = null

func is_nearby(player: Node) -> bool:
	return _nearby_player == player

## Transfers every still-fitting item into looter's own Inventory. An item
## that doesn't fit (looter's own inventory full) stays in loot_items for a
## later attempt instead of being destroyed -- matches this project's own
## "denied action does nothing" convention (see player/inventory.gd's own
## add_item()).
func take_all(looter: Node) -> void:
	if _looted:
		return
	var inv: Node = looter.get_node_or_null("Inventory")
	if inv == null or not inv.has_method("add_item"):
		return
	var still_remaining: Array[Dictionary] = []
	for entry in loot_items:
		if inv.call("add_item", entry.get("item_id", ""), entry.get("count", 1)):
			_strip_source_slot(entry.get("source_slot", ""))
			continue
		still_remaining.append(entry)
	loot_items = still_remaining
	if loot_items.is_empty():
		_looted = true

## Visually strips the dead PLAYER's own corpse of whatever was just taken
## (no-op for an NPC corpse -- strip_target is null there, nothing to
## strip). The live weapon/wearable instance is still a real child of the
## dead character's own hand/head/bone node at this point -- ragdolling
## never reparents equipped gear away (see character.gd's own ragdoll()
## class comment) -- so freeing it here is what makes the corpse actually
## look stripped, not just an abstract inventory-count change on the
## looter's side.
func _strip_source_slot(slot_key: String) -> void:
	if slot_key == "" or strip_target == null:
		return
	var character: Node = strip_target.get_node_or_null("Character")
	var inv: Node = strip_target.get_node_or_null("Inventory")
	if character == null:
		return
	match slot_key:
		"hand_r":
			if character.has_method("clear_wielded_weapon"):
				character.call("clear_wielded_weapon")
			if strip_target.has_method("unequip_gun"):
				strip_target.call("unequip_gun")
			if strip_target.has_method("unequip_bottle"):
				strip_target.call("unequip_bottle")
		"top":
			if character.has_method("clear_worn_top"):
				character.call("clear_worn_top")
		"hat", "mask":
			if character.has_method("clear_head_slot"):
				character.call("clear_head_slot", slot_key)
		_:
			pass # hands/legs/feet/hand_l: no wearable content exists yet, nothing to clear
	if inv != null:
		inv.equipment[slot_key] = ""
