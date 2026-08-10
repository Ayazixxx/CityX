extends RefCounted
## Closed table of every item the inventory system (player/inventory.gd)
## knows how to store/equip -- same "static Dictionary + a lookup func"
## idiom as character/combo_data.gd's own CHAINS/chain_for(), just for item
## data instead of combo data.
##
## Every equippable entry's "scene" is the SAME weapon/wearable .tscn that
## already existed before the inventory system -- picking one up no longer
## calls its own _equip() directly (see each script's own _try_add_to_
## inventory()), but equip_from_slot() on the inventory still does, on a
## freshly instantiated copy of this same scene. This registry is purely an
## identity/lookup layer bolted on top of already-working equip code, not a
## reimplementation of it.
##
## LOOT_ONLY entries (currently just "scrap_metal") have no "scene" and no
## "equip_slot" -- they're never a world object or an equippable, only ever
## a count sitting in someone's inventory (see loot/loot_component.gd).

enum Category { WEAPON_HAND, WEARABLE_HEAD, WEARABLE_TORSO, WEARABLE_LEGS, LOOT_ONLY }

const ITEMS := {
	"pistol": {
		"display_name": "Beretta M9",
		"category": Category.WEAPON_HAND,
		"equip_slot": "hand_r",
		"scene": preload("res://weapons/pistol.tscn"),
		"stack_size": 1,
	},
	"glass_bottle": {
		"display_name": "Glass Bottle",
		"category": Category.WEAPON_HAND,
		"equip_slot": "hand_r",
		"scene": preload("res://weapons/glass_bottle.tscn"),
		# Carries mutable durability across pickup/equip -- see
		# player/inventory.gd's own instance_data handling, NOT stack_size
		# (this stays 1, a single physical bottle, matching its pre-
		# inventory "you only ever had one" precedent).
		"stack_size": 1,
	},
	"greatsword": {
		"display_name": "Greatsword",
		"category": Category.WEAPON_HAND,
		"equip_slot": "hand_r",
		"scene": preload("res://weapons/greatsword.tscn"),
		"stack_size": 1,
	},
	"martyr_blade": {
		"display_name": "Martyr Blade",
		"category": Category.WEAPON_HAND,
		"equip_slot": "hand_r",
		"scene": preload("res://weapons/martyr_blade.tscn"),
		"stack_size": 1,
	},
	"runeblade": {
		"display_name": "Runeblade",
		"category": Category.WEAPON_HAND,
		"equip_slot": "hand_r",
		"scene": preload("res://weapons/runeblade.tscn"),
		"stack_size": 1,
	},
	"top_hat": {
		"display_name": "Top Hat",
		"category": Category.WEARABLE_HEAD,
		"equip_slot": "hat",
		"scene": preload("res://wearables/top_hat.tscn"),
		"stack_size": 1,
	},
	"hockey_mask": {
		"display_name": "Hockey Mask",
		"category": Category.WEARABLE_HEAD,
		"equip_slot": "mask",
		"scene": preload("res://wearables/hockey_mask.tscn"),
		"stack_size": 1,
	},
	"t_shirt": {
		"display_name": "T-Shirt",
		"category": Category.WEARABLE_TORSO,
		"equip_slot": "top",
		"scene": preload("res://wearables/t_shirt.tscn"),
		"stack_size": 1,
	},
	"hoodie": {
		"display_name": "Hoodie",
		"category": Category.WEARABLE_TORSO,
		"equip_slot": "top",
		"scene": preload("res://wearables/hoodie.tscn"),
		"stack_size": 1,
	},
	"hooded_cloak": {
		"display_name": "Hooded Cloak",
		"category": Category.WEARABLE_TORSO,
		"equip_slot": "top",
		"scene": preload("res://wearables/hooded_cloak.tscn"),
		"stack_size": 1,
	},
	"skelli_jeans": {
		"display_name": "Skelli Jeans",
		"category": Category.WEARABLE_LEGS,
		"equip_slot": "legs",
		"scene": preload("res://wearables/skelli_jeans.tscn"),
		"stack_size": 1,
	},
	"flashlight": {
		"display_name": "Flashlight",
		"category": Category.WEAPON_HAND,
		"equip_slot": "hand_r",
		"scene": preload("res://items/flashlight.tscn"),
		# Carries its on/off state across pickup -> inventory -> equip, the
		# same way glass_bottle carries durability (see player/inventory.gd's
		# own instance_data handling) -- stowing a lit torch and pulling it
		# back out dark would read as a bug.
		"stack_size": 1,
	},
	"grappling_hook": {
		"display_name": "Grappling Hook",
		"category": Category.WEAPON_HAND,
		"equip_slot": "hand_r",
		"scene": preload("res://items/grappling_hook.tscn"),
		"stack_size": 1,
	},
	"grenade": {
		"display_name": "Frag Grenade",
		"category": Category.WEAPON_HAND,
		"equip_slot": "hand_r",
		"scene": preload("res://weapons/grenade.tscn"),
		# The first genuinely CONSUMABLE equippable -- every other entry
		# here is a durable item you keep. Stacks because it's ammunition,
		# and player/inventory.gd's own consume_equipped_item() spends one
		# per throw and re-arms you from the stack.
		"stack_size": 10,
	},
	"scrap_metal": {
		"display_name": "Scrap Metal",
		"category": Category.LOOT_ONLY,
		"stack_size": 20,
	},
}

static func get_item(id: String) -> Dictionary:
	return ITEMS.get(id, {})

## Whether an item of this id can be dropped onto equip_slot -- used by
## ui/item_slot.gd's own _can_drop_data() to gate paper-doll drops (e.g. a
## sword can't be dropped onto the "feet" slot).
static func fits_slot(item_id: String, slot_key: String) -> bool:
	var item := get_item(item_id)
	return not item.is_empty() and item.get("equip_slot", "") == slot_key
