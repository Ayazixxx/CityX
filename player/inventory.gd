extends Node
## Player inventory: 32 general storage slots + a fixed paper-doll
## equipment dict. A direct child of Player (see player.tscn's own
## "Inventory" node), not built at runtime -- main.gd's own
## _apply_pending_progress needs it to already exist so loaded save data
## can be written onto it before add_child(player) runs, the same timing
## every other loaded stat (stat_health etc.) already relies on.
##
## Doesn't know how to equip/unequip anything itself -- equip_from_slot()/
## unequip_to_slot() just instantiate/free the SAME weapon/wearable scenes
## that already know how to equip themselves (see items/item_registry.gd's
## own class comment), calling their existing _equip(player) unchanged.
## Unlike equipping, UNequipping has no prior precedent anywhere in this
## codebase (every existing weapon/wearable only ever adds to a slot) --
## see character.gd's own clear_wielded_weapon()/clear_head_slot(), new
## alongside this file.

const ItemRegistry = preload("res://items/item_registry.gd")

const STORAGE_SLOT_COUNT := 32

# Each entry: {} (empty) or {"item_id": String, "count": int, "instance_data": Dictionary}.
var storage_slots: Array[Dictionary] = []

# slot_key -> item_id ("" if empty). "hand_l" is never populated by
# equip_from_slot in this pass -- no registered item's own equip_slot is
# "hand_l" yet (see item_registry.gd's own class comment on why: nothing
# to put there today) -- kept here so the UI has a stable slot to show as
# reserved/future rather than omitting it entirely.
var equipment: Dictionary = {
	"hat": "", "mask": "", "top": "", "hands": "", "legs": "", "feet": "",
	"hand_l": "", "hand_r": "",
}

func _ready() -> void:
	# Only fills in the default empty grid if nothing was already placed
	# here -- a freshly spawned (never-saved) character sees storage_slots
	# still at its own script-default [], while a loaded character already
	# had main.gd write real (or legitimately all-empty, same shape) data
	# onto this BEFORE _ready() ever runs -- see this file's own class
	# comment on why that ordering matters.
	if storage_slots.is_empty():
		storage_slots.resize(STORAGE_SLOT_COUNT)
		for i in range(STORAGE_SLOT_COUNT):
			storage_slots[i] = {}

## Denied (inventory has no room) does nothing at all -- not even a partial
## stack -- matching this project's existing "denied action does nothing"
## convention (see player.gd's own spend_stat_point()). Checks room via a
## dry run before mutating anything, so a failed add never leaves a partial
## trace behind.
func add_item(item_id: String, count: int, instance_data: Dictionary = {}) -> bool:
	var item := ItemRegistry.get_item(item_id)
	if item.is_empty() or count <= 0:
		return false
	var stack_size: int = item.get("stack_size", 1)
	# instance_data (e.g. a bottle's own durability) makes an item unique --
	# never silently merged into an unrelated stack of the "same" item id.
	var stackable: bool = stack_size > 1 and instance_data.is_empty()

	var remaining := count
	if stackable:
		for slot in storage_slots:
			if remaining <= 0:
				break
			if slot.get("item_id", "") == item_id:
				remaining -= maxi(0, stack_size - int(slot.get("count", 0)))
	for slot in storage_slots:
		if remaining <= 0:
			break
		if slot.is_empty():
			remaining -= stack_size
	if remaining > 0:
		return false

	remaining = count
	if stackable:
		for slot in storage_slots:
			if remaining <= 0:
				break
			if slot.get("item_id", "") == item_id:
				var room: int = stack_size - int(slot.get("count", 0))
				var added: int = mini(room, remaining)
				if added > 0:
					slot["count"] = int(slot.get("count", 0)) + added
					remaining -= added
	for i in range(storage_slots.size()):
		if remaining <= 0:
			break
		if storage_slots[i].is_empty():
			var to_add: int = mini(remaining, stack_size)
			storage_slots[i] = {"item_id": item_id, "count": to_add, "instance_data": instance_data}
			remaining -= to_add
	return true

## Instantiates the storage slot's registered scene and calls its existing
## _equip(player) -- the exact same equip logic every weapon/wearable
## already had before the inventory system, just invoked from here instead
## of the item's own _process(). Whatever was already in that paper-doll
## slot is unequipped back to storage first (real "replace," not orphaning
## the old instance). LOOT_ONLY items (no "scene") aren't equippable and
## fail here -- they only ever exist as a storage-slot count.
func equip_from_slot(slot_index: int, player: CharacterBody3D) -> bool:
	if slot_index < 0 or slot_index >= storage_slots.size():
		return false
	var slot: Dictionary = storage_slots[slot_index]
	if slot.is_empty():
		return false
	if not equip_item_id(slot["item_id"], player, slot.get("instance_data", {})):
		return false
	# Takes ONE off the stack, not the whole slot. Every equippable in this
	# project used to be stack_size 1, so clearing the slot outright was
	# indistinguishable from decrementing it -- until grenades (see
	# items/item_registry.gd) became the first stackable equippable, at
	# which point equipping from a stack of 5 silently destroyed the other
	# 4. Caught by the grenade's own regression test.
	var count: int = int(slot.get("count", 1))
	if count <= 1:
		storage_slots[slot_index] = {}
	else:
		slot["count"] = count - 1
		storage_slots[slot_index] = slot
	return true

## The shared equip mechanics behind equip_from_slot() above, factored out
## so main.gd's own load-time re-equip (see _apply_pending_progress()) can
## equip straight from a saved item_id without needing a fake storage slot
## to pull it out of first -- a saved character's equipment dict already
## names the item directly, there's no storage index to look it up by.
func equip_item_id(item_id: String, player: CharacterBody3D, instance_data: Dictionary = {}) -> bool:
	var item := ItemRegistry.get_item(item_id)
	var scene: PackedScene = item.get("scene")
	var slot_key: String = item.get("equip_slot", "")
	if scene == null or slot_key == "":
		return false
	if equipment.get(slot_key, "") != "":
		unequip_to_slot(slot_key, player)
	var instance: Node = scene.instantiate()
	if instance_data.has("durability") and instance.has_method("set_durability_state"):
		instance.call("set_durability_state", instance_data["durability"])
	# Same "restore this instance's own carried state onto a freshly built
	# copy" step as durability just above -- see items/flashlight.gd's own
	# set_light_on for why calling it this early (before _ready()) is safe.
	if instance_data.has("light_on") and instance.has_method("set_light_on"):
		instance.call("set_light_on", instance_data["light_on"])
	# Parented onto the player FIRST, not straight onto the target hand/head
	# bone -- every weapon/wearable's own _equip() (unchanged, see this
	# file's own class comment) was written assuming it's already inside the
	# SceneTree by the time it runs, same as it always was back when it only
	# ever got called on an already-world-placed pickup prop: its @onready
	# vars (e.g. a weapon's own _interact_area) only populate once _ready()
	# fires, which only happens once a node actually enters the tree, and
	# _equip() itself does its own get_parent().remove_child(self) before
	# reparenting onto the real target bone -- calling _equip() on a
	# same-frame scene.instantiate() with no parent at all crashed there
	# (confirmed directly: "Invalid assignment of property... on a base
	# object of type 'Nil'" at _interact_area.monitoring = false, the item
	# never actually reaching hand.add_child()/head.add_child()). This is a
	# real, if momentary, parent for exactly that hand-off, not a leftover.
	player.add_child(instance)
	if instance.has_method("_equip"):
		instance.call("_equip", player)
	equipment[slot_key] = item_id
	return true

## Spends one unit of a CONSUMABLE equipped item, then immediately re-equips
## the next one if the player still has any. Built for the grenade (see
## player.gd's own _try_throw_grenade), the first item in this project that
## is used UP rather than kept: every previous equippable is durable, so
## equipping simply moved it out of storage and there was never a count to
## decrement.
##
## The equipment slot is cleared first (the thrown instance is already gone
## by the time this runs -- player.gd frees it as part of the throw), so
## equip_item_id() below isn't fighting an occupied slot. Returns true if
## another one was actually drawn, false if that was the last one and the
## player is now empty-handed.
func consume_equipped_item(item_id: String, player: CharacterBody3D) -> bool:
	var item := ItemRegistry.get_item(item_id)
	var slot_key: String = item.get("equip_slot", "")
	if slot_key != "" and equipment.get(slot_key, "") == item_id:
		equipment[slot_key] = ""

	for i in range(storage_slots.size()):
		var slot: Dictionary = storage_slots[i]
		if slot.is_empty() or slot.get("item_id", "") != item_id:
			continue
		var count: int = int(slot.get("count", 1))
		if count <= 1:
			storage_slots[i] = {}
		else:
			slot["count"] = count - 1
			storage_slots[i] = slot
		return equip_item_id(item_id, player, slot.get("instance_data", {}))
	return false

## The reverse of equip_from_slot() -- frees the live weapon/wearable
## instance via character.gd's own clear_wielded_weapon()/clear_worn_top()/
## clear_head_slot() (new, see character.gd) and returns the item to
## storage. Fails (leaves it equipped) if storage has no room, rather than
## destroying an item the player has nowhere to put.
func unequip_to_slot(slot_key: String, player: CharacterBody3D) -> bool:
	var item_id: String = equipment.get(slot_key, "")
	if item_id == "":
		return false
	if not add_item(item_id, 1):
		return false
	var character: Node = player.get_node_or_null("Character")
	if character == null:
		return false
	match slot_key:
		"hand_r":
			if character.has_method("clear_wielded_weapon"):
				character.call("clear_wielded_weapon")
			# character.clear_wielded_weapon() only frees the actual scene
			# instance/resets ITS OWN wielding flags -- player.gd separately
			# tracks _equipped_gun/_equipped_bottle for its own combat-input
			# routing (see player.gd's own equip_gun()/equip_bottle()), so
			# those need clearing here too or a stale reference would keep
			# _physics_process reading the wrong weapon branch.
			if player.has_method("unequip_gun"):
				player.call("unequip_gun")
			if player.has_method("unequip_bottle"):
				player.call("unequip_bottle")
		"top":
			if character.has_method("clear_worn_top"):
				character.call("clear_worn_top")
		"legs":
			if character.has_method("clear_worn_legs"):
				character.call("clear_worn_legs")
		"hat", "mask":
			if character.has_method("clear_head_slot"):
				character.call("clear_head_slot", slot_key)
		_:
			pass # hands/feet/hand_l: no wearable content exists yet, nothing to clear
	equipment[slot_key] = ""
	return true

## Equipped-slot -> item_id only (no counts, no instance_data) -- exactly
## what a corpse-loot snapshot and the multiplayer equip-visibility RPCs
## need (see loot/loot_component.gd, player.gd's own _rpc_equip_item).
## General storage contents are never included -- nobody but the owning
## peer needs to see inside their own bag (see this project's own already-
## confirmed multiplayer visibility scope).
func get_equipped_item_ids() -> Dictionary:
	var out := {}
	for slot_key in equipment:
		var item_id: String = equipment[slot_key]
		if item_id != "":
			out[slot_key] = item_id
	return out
