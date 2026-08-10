extends Button
## One inventory slot -- either a general storage slot (source="storage",
## index_or_key is an int index into player/inventory.gd's own
## storage_slots) or a paper-doll equipment slot (source="equipment",
## index_or_key is a slot_key string like "hand_r", matching Inventory's
## own equipment dict keys).
##
## Shows the item's ICON -- a render of that item's own real 3D model, see
## items/item_icons.gd -- with the stack count as a small corner badge and
## the display name moved to the tooltip. The display name is still drawn as
## button TEXT, but only as a fallback for when an icon genuinely isn't
## available (a headless/software run, where offscreen viewport capture
## returns nothing) -- so a slot is never blank, and never both at once.
##
## Two parallel, both-real ways to move an item: a plain click on a
## STORAGE slot holding an equippable item auto-equips it into its own
## registered slot (a click on an EQUIPPED slot unequips it back to
## storage) -- always available, no precision needed. Real Control drag-
## and-drop (_get_drag_data/_can_drop_data/_drop_data, Godot's own built-in
## mechanism -- confirmed via grep that no drag-and-drop precedent existed
## anywhere in this project before this file) is ALSO wired, for swapping
## two specific storage slots or choosing between two same-slot-type items.
##
## Instantiated directly via `preload("res://ui/item_slot.gd").new()` (see
## ui/inventory_panel.gd's own class comment on why it's built in code, not
## a .tscn) -- valid, ordinary GDScript: a script extending a Godot
## built-in class can always be instantiated straight off its own script
## resource, exactly like instantiating a scene, no .tscn required.

const ItemRegistry = preload("res://items/item_registry.gd")
const ItemIcons = preload("res://items/item_icons.gd")

# Square, icon-sized, and the SAME for both sources now that neither shows
# a name inline -- an equipment slot used to be a wide 150x32 strip because
# it had to fit "Beretta M9" as text. Its paper-doll row label ("Right
# Hand") still sits beside it, see ui/inventory_panel.gd.
const SLOT_SIZE := 48
const COUNT_BADGE_FONT_SIZE := 12

var source := "storage" # "storage" or "equipment"
var index_or_key # int for storage, String (slot_key) for equipment
# What's actually in this slot ("" when empty). The single source of truth
# for emptiness -- `text` used to serve that purpose, but it's now only a
# fallback that's usually blank precisely BECAUSE the slot is full.
var item_id := ""

var _count_badge: Label
# The owning InventoryPanel -- a CanvasLayer, NOT a Control (a real bug
# hit here directly: typing this as Control threw "Invalid assignment...
# on a base object of type Button" the instant inventory_panel.gd tried to
# set slot.panel = self, since CanvasLayer doesn't extend Control at all).
# Untyped Node keeps this duck-typed via has_method() below instead.
var panel: Node

func _ready() -> void:
	pressed.connect(_on_pressed)
	custom_minimum_size = Vector2(SLOT_SIZE, SLOT_SIZE)
	# Scales the icon to the button instead of drawing it at its native
	# 128px (TextureRect/Button both default to native size -- the same
	# default that caused ui/hud.gd's own long-standing "bars overlap the
	# portrait" bug).
	expand_icon = true
	icon_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_build_count_badge()

## A stack count drawn in the slot's own bottom-right corner, the way an
## inventory conventionally shows one -- Button can't do icon + corner text
## on its own (its `text` sits BESIDE the icon, which would squeeze the icon
## out of a 48px square), so the count is its own overlay child.
func _build_count_badge() -> void:
	_count_badge = Label.new()
	_count_badge.name = "CountBadge"
	_count_badge.anchor_left = 0.0
	_count_badge.anchor_top = 0.0
	_count_badge.anchor_right = 1.0
	_count_badge.anchor_bottom = 1.0
	_count_badge.offset_right = -3.0
	_count_badge.offset_bottom = -1.0
	_count_badge.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	_count_badge.vertical_alignment = VERTICAL_ALIGNMENT_BOTTOM
	_count_badge.add_theme_font_size_override("font_size", COUNT_BADGE_FONT_SIZE)
	# Outlined so it stays readable over a light-colored icon underneath it.
	_count_badge.add_theme_color_override("font_outline_color", Color.BLACK)
	_count_badge.add_theme_constant_override("outline_size", 4)
	# Must not eat the click/drag gestures of the button it's drawn over.
	_count_badge.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_count_badge)

func refresh(new_item_id: String, count: int) -> void:
	item_id = new_item_id
	if item_id == "":
		icon = null
		text = "--" if source == "equipment" else ""
		tooltip_text = ""
		if _count_badge != null:
			_count_badge.text = ""
		return
	var item := ItemRegistry.get_item(item_id)
	var display: String = item.get("display_name", item_id)
	var item_icon := ItemIcons.get_icon(item_id)
	icon = item_icon
	# Name text ONLY when there's no icon to show instead -- see this file's
	# own class comment. The name is always on the tooltip either way.
	text = "" if item_icon != null else display
	tooltip_text = display
	if _count_badge != null:
		_count_badge.text = "" if count <= 1 else "x%d" % count

func _on_pressed() -> void:
	if panel != null and panel.has_method("slot_activated"):
		panel.slot_activated(source, index_or_key)

## Emptiness is decided by item_id, NOT by the button's own text the way it
## used to be -- a slot showing an icon now has EMPTY text, so the old
## `if text == ""` check would have refused to drag every filled slot.
func _get_drag_data(_at_position: Vector2) -> Variant:
	if item_id == "":
		return null
	set_drag_preview(_build_drag_preview())
	return {"source": source, "index_or_key": index_or_key}

func _build_drag_preview() -> Control:
	if icon != null:
		var preview := TextureRect.new()
		preview.texture = icon
		preview.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		preview.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		preview.custom_minimum_size = Vector2(SLOT_SIZE, SLOT_SIZE)
		preview.size = Vector2(SLOT_SIZE, SLOT_SIZE)
		return preview
	var label := Label.new()
	label.text = ItemRegistry.get_item(item_id).get("display_name", item_id)
	return label

func _can_drop_data(_at_position: Vector2, data: Variant) -> bool:
	if panel == null or not (data is Dictionary):
		return false
	return panel.can_drop_onto(source, index_or_key, data)

func _drop_data(_at_position: Vector2, data: Variant) -> void:
	if panel != null:
		panel.drop_onto(source, index_or_key, data)
