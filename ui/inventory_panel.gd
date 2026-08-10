extends CanvasLayer
## The character screen: Inventory, Stats and Skills as three tabs of one
## panel. "I" (the "inventory" action) opens it on whichever tab was last
## used; "L" (the "open_stats" action) opens it straight on Stats. Either
## key closes it again.
##
## Stats used to live in its own ui/stats_panel.tscn on the L key. That file
## is gone -- two separate panels meant two places for the same numbers to
## drift apart, and a reported ask put stats inside the inventory anyway.
## The L binding survives as a shortcut straight to the Stats tab rather
## than being retired with the panel.
##
## Unlike ui/pause_menu.gd and ui/dev_console.gd, this deliberately does NOT
## set get_tree().paused while open -- a reported ask, and the right call:
## pausing is merely wrong in singleplayer (the world should keep breathing
## while you rearrange your bag) but actively broken in multiplayer, where
## get_tree().paused freezes THIS peer's own physics and RPC processing
## while every other peer keeps playing. Instead the mouse is released and
## the owning player is told to ignore its own input polling
## (player.gd's own set_ui_capturing()/_input_blocked()). You are not safe
## with your inventory open -- you just can't act. process_mode stays
## ALWAYS regardless, so this still hears its own toggle key if something
## else (the pause menu) pauses the tree over the top of it.
##
## Built entirely in code (_build_ui, called from _ready()), not authored
## in a .tscn the way the other ui/ panels are -- this project has no
## precedent anywhere for a slot-grid/drag-and-drop UI (confirmed via grep
## before writing this), and hand-authoring 32+ individual Button nodes in
## scene-file text would be both extremely verbose and easy to get wrong
## with no live editor available to check it in. Procedural UI construction
## already has strong precedent elsewhere in this project (every weapon/
## wearable mesh, city_chunk.gd's buildings, character_creator.gd's own
## tool icons) -- this just applies the same idea to Controls instead of
## Node3Ds. See ui/item_slot.gd's own class comment for the matching reason
## its slots are `.new()`'d off their own script instead of a .tscn too.

const ItemSlotScript = preload("res://ui/item_slot.gd")
const ItemRegistry = preload("res://items/item_registry.gd")
const ItemIcons = preload("res://items/item_icons.gd")
const InventoryScript = preload("res://player/inventory.gd")
const Character = preload("res://character/character.gd")

const LAYER := 60
const STORAGE_COLUMNS := 8

# Tab order, and the index _open_on_stats() jumps to. Indices rather than
# names because TabContainer.current_tab is an int -- kept as consts so the
# jump can't silently start landing on the wrong page if a tab is ever
# inserted ahead of it.
const TAB_INVENTORY := 0
const TAB_STATS := 1
const TAB_SKILLS := 2

# The 5 stats, in display order, as (spend_stat_point key, label) pairs.
# The key must match player.gd's own spend_stat_point() match arms exactly
# -- a typo here would just silently no-op the "+" button, so the test
# spends one point through every key rather than trusting the strings.
const STAT_ROWS := [
	{"key": "health", "label": "Health"},
	{"key": "strength", "label": "Strength"},
	{"key": "stamina", "label": "Stamina"},
	{"key": "mana", "label": "Mana"},
	{"key": "speed", "label": "Speed"},
]
# How far back the live paper-doll camera sits from the character's own
# chest-height center -- framed for a full standing body, unlike
# ui/hud.gd's own PORTRAIT_CAMERA_DISTANCE (0.85), which only ever frames
# a tight head shot.
const PREVIEW_CAMERA_DISTANCE := 2.3

# Paper-doll rows, in display order -- must match player/inventory.gd's
# own `equipment` dict keys exactly. "hat"/"mask" are shown as two separate
# rows (both really are independent dict keys, see wearables/wearable.gd's
# own class comment on why a hat and a mask don't evict each other) rather
# than folded into one "Head" row.
const EQUIPMENT_SLOTS := [
	{"key": "hat", "label": "Head (Hat)"},
	{"key": "mask", "label": "Head (Mask)"},
	{"key": "top", "label": "Torso"},
	{"key": "hands", "label": "Hands"},
	{"key": "legs", "label": "Legs"},
	{"key": "feet", "label": "Feet"},
	{"key": "hand_l", "label": "Left Hand"},
	{"key": "hand_r", "label": "Right Hand"},
]

@onready var _player: CharacterBody3D = get_parent()

var _dim: ColorRect
var _tabs: TabContainer
var _preview_viewport: SubViewport
var _preview_camera: Camera3D
var _equipment_slot_controls: Dictionary = {} # slot_key -> ItemSlot
var _storage_slot_controls: Array = [] # index -> ItemSlot
var _was_mouse_captured := false
var _inventory: Node

# Stats tab widgets, rebuilt-in-place on every _refresh() rather than
# recreated -- same reason the storage slots above are held as controls and
# refreshed: rebuilding the whole page on each point spent would drop focus
# mid-click.
var _level_label: Label
var _xp_label: Label
var _stat_points_label: Label
var _skill_points_label: Label
var _stat_rows: Dictionary = {} # stat key -> {"label": Label, "button": Button}

# Skills tab: just the running skill-point count for now (see _build_skills_tab).
var _skill_points_label_skills: Label

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	layer = LAYER
	_inventory = _player.get_node("Inventory")
	_build_ui()
	_dim.visible = false
	# Renders the item icons in the background, a few frames per item, so
	# they're ready long before the player first presses I -- rather than
	# stalling either startup or that first open (see items/item_icons.gd,
	# and this project's own standing "no load-time freezes" priority).
	# _open() re-refreshes anyway, so a panel opened mid-build still picks
	# up whatever has finished by then, and the refresh below catches the
	# rest.
	await ItemIcons.build_all(self)
	if is_instance_valid(self):
		_refresh()

## "I" toggles the panel on whatever tab it was left on; "L" toggles it on
## the Stats tab specifically (see this file's own class comment on why the
## old stats panel's binding survived its panel). Pressing L while already
## open on another tab SWITCHES to Stats rather than closing -- closing
## would make the key feel broken when it was really doing its job.
func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("inventory"):
		if _dim.visible:
			_close()
		else:
			_open()
	elif event.is_action_pressed("open_stats"):
		if _dim.visible and _tabs.current_tab == TAB_STATS:
			_close()
		elif _dim.visible:
			_tabs.current_tab = TAB_STATS
		else:
			_open(TAB_STATS)

func _open(tab := -1) -> void:
	# Guarded the same way _close() is: set_ui_capturing() moves a counter,
	# so an unbalanced second open would leave the player permanently unable
	# to act. Nothing calls _open() twice today; this makes sure nothing can.
	if _dim.visible:
		if tab >= 0:
			_tabs.current_tab = tab
		return
	_was_mouse_captured = Input.mouse_mode == Input.MOUSE_MODE_CAPTURED
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	# NOT get_tree().paused = true -- see this file's own class comment.
	# Telling the player to ignore its own input polling is what replaces it.
	if is_instance_valid(_player) and _player.has_method("set_ui_capturing"):
		_player.set_ui_capturing(true)
	if tab >= 0:
		_tabs.current_tab = tab
	_dim.visible = true
	_position_preview_camera()
	_refresh()

func _close() -> void:
	if not _dim.visible:
		return
	_dim.visible = false
	# is_instance_valid, not != null: _exit_tree() can run while the player
	# is being freed out from under this panel, and a freed object still
	# compares non-null.
	if is_instance_valid(_player) and _player.has_method("set_ui_capturing"):
		_player.set_ui_capturing(false)
	# The paper-doll preview is UPDATE_ALWAYS while open (see
	# _position_preview_camera) -- that has to be switched back off here now
	# that closing the panel no longer unpauses the tree along with it.
	# Otherwise a 240x240 viewport keeps re-rendering the character every
	# frame for the rest of the session with nobody looking at it.
	if _preview_viewport != null:
		_preview_viewport.render_target_update_mode = SubViewport.UPDATE_DISABLED
	# Off the preview layer again. Nothing renders that layer while the panel
	# is shut, so leaving it on would be harmless today -- but "only the
	# character being looked at carries this" is the property that keeps the
	# background empty, and it stops being true the moment one is left set.
	if is_instance_valid(_player):
		var character: Node = _player.get_node_or_null("Character")
		if character != null and character.has_method("set_preview_layer_enabled"):
			character.set_preview_layer_enabled(false)
	if _was_mouse_captured:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED

## The player node is freed with this panel still open (disconnect, quit to
## title) if nothing balances the set_ui_capturing(true) above. Nothing
## reads the counter after the player is gone, so this is belt-and-braces
## rather than a live bug -- but leaving an unbalanced counter around is
## exactly the kind of thing that becomes one the moment panels get reused.
func _exit_tree() -> void:
	if _dim != null and _dim.visible:
		_close()

func _build_ui() -> void:
	_dim = ColorRect.new()
	_dim.name = "Dim"
	_dim.color = Color(0, 0, 0, 0.55)
	_dim.anchor_right = 1.0
	_dim.anchor_bottom = 1.0
	_dim.mouse_filter = Control.MOUSE_FILTER_STOP
	add_child(_dim)

	var panel := PanelContainer.new()
	# The default theme panel is translucent enough that the live 3D world
	# reads straight through the text -- tolerable for a grid of item icons,
	# not for the paragraphs of skill descriptions this now holds. These are
	# the exact colors the retired ui/stats_panel.tscn used, so moving stats
	# in here isn't a legibility downgrade from the panel it replaced.
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.1, 0.11, 0.14, 0.95)
	style.border_color = Color(0.55, 0.63, 1.0, 0.6)
	style.set_border_width_all(2)
	style.set_corner_radius_all(6)
	panel.add_theme_stylebox_override("panel", style)
	panel.anchor_left = 0.5
	panel.anchor_top = 0.5
	panel.anchor_right = 0.5
	panel.anchor_bottom = 0.5
	# Wider/taller than the pre-tabs layout: the Skills tab lays three
	# branch columns side by side, which the old 860x600 box clipped.
	panel.offset_left = -470.0
	panel.offset_top = -330.0
	panel.offset_right = 470.0
	panel.offset_bottom = 330.0
	_dim.add_child(panel)

	var margin := MarginContainer.new()
	for side in ["left", "top", "right", "bottom"]:
		margin.add_theme_constant_override("margin_%s" % side, 16)
	panel.add_child(margin)

	var vbox := VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 10)
	margin.add_child(vbox)

	# A TabContainer names its tabs after its direct children's node names,
	# so each page's own `name` IS the tab caption -- hence the explicit
	# .name assignments in the three builders below rather than a separate
	# set_tab_title() pass.
	_tabs = TabContainer.new()
	_tabs.size_flags_vertical = Control.SIZE_EXPAND_FILL
	vbox.add_child(_tabs)
	_tabs.add_child(_build_inventory_tab())
	_tabs.add_child(_build_stats_tab())
	_tabs.add_child(_build_skills_tab())

	var close_button := Button.new()
	close_button.text = "Close"
	close_button.pressed.connect(_close)
	vbox.add_child(close_button)

func _build_inventory_tab() -> Control:
	var hbox := HBoxContainer.new()
	hbox.name = "Inventory"
	hbox.add_theme_constant_override("separation", 24)
	hbox.add_child(_build_paper_doll())
	hbox.add_child(_build_storage_grid())
	return hbox

## The 5 stats (Health/Strength/Stamina/Mana/Speed) with a "+" per row that
## spends one available stat point, plus the level/XP/points header the old
## ui/stats_panel.tscn used to own. Rows come from STAT_ROWS rather than
## being hand-written five times over, so the labels and the
## spend_stat_point() keys can't drift apart.
func _build_stats_tab() -> Control:
	var vbox := VBoxContainer.new()
	vbox.name = "Stats"
	vbox.add_theme_constant_override("separation", 8)

	_level_label = Label.new()
	_level_label.add_theme_font_size_override("font_size", 18)
	vbox.add_child(_level_label)

	_xp_label = Label.new()
	vbox.add_child(_xp_label)

	_stat_points_label = Label.new()
	vbox.add_child(_stat_points_label)

	_skill_points_label = Label.new()
	vbox.add_child(_skill_points_label)

	vbox.add_child(HSeparator.new())

	for entry in STAT_ROWS:
		var row := HBoxContainer.new()
		row.add_theme_constant_override("separation", 8)

		var name_label := Label.new()
		name_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		name_label.custom_minimum_size = Vector2(300, 0)
		row.add_child(name_label)

		var plus := Button.new()
		plus.text = "+"
		plus.custom_minimum_size = Vector2(36, 0)
		plus.pressed.connect(_on_spend_stat_pressed.bind(entry["key"]))
		row.add_child(plus)

		_stat_rows[entry["key"]] = {"label": name_label, "button": plus}
		vbox.add_child(row)

	return vbox

## Deliberately empty for now. The tab itself is real (a reported ask -- the
## character screen should have a place for skills), but its CONTENTS are
## explicitly being designed later rather than filled with placeholder
## skills: an earlier pass invented three branches of passive nodes and
## they were removed on request, since guessed-at skills are worse than
## none when the real tree is meant to carry fighting styles and
## specializations. All this shows today is the skill points piling up,
## waiting for something to spend them on.
func _build_skills_tab() -> Control:
	var vbox := VBoxContainer.new()
	vbox.name = "Skills"
	vbox.add_theme_constant_override("separation", 12)
	vbox.alignment = BoxContainer.ALIGNMENT_CENTER

	_skill_points_label_skills = Label.new()
	_skill_points_label_skills.add_theme_font_size_override("font_size", 18)
	_skill_points_label_skills.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	vbox.add_child(_skill_points_label_skills)

	var placeholder := Label.new()
	placeholder.text = "Coming soon."
	placeholder.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	placeholder.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	placeholder.add_theme_color_override("font_color", Color(0.66, 0.68, 0.74))
	vbox.add_child(placeholder)

	return vbox

func _build_paper_doll() -> Control:
	var doll_vbox := VBoxContainer.new()
	doll_vbox.custom_minimum_size = Vector2(280, 0)
	doll_vbox.add_theme_constant_override("separation", 6)

	doll_vbox.add_child(_build_preview())

	for entry in EQUIPMENT_SLOTS:
		var row := HBoxContainer.new()
		row.add_theme_constant_override("separation", 8)

		var label := Label.new()
		label.text = entry["label"]
		label.custom_minimum_size = Vector2(110, 0)
		row.add_child(label)

		var slot: Button = ItemSlotScript.new()
		slot.source = "equipment"
		slot.index_or_key = entry["key"]
		slot.panel = self
		row.add_child(slot)
		_equipment_slot_controls[entry["key"]] = slot

		doll_vbox.add_child(row)

	return doll_vbox

func _build_preview() -> Control:
	var clip := SubViewportContainer.new()
	clip.custom_minimum_size = Vector2(240, 240)
	clip.stretch = true

	_preview_viewport = SubViewport.new()
	_preview_viewport.size = Vector2i(240, 240)
	clip.add_child(_preview_viewport)

	_preview_camera = Camera3D.new()
	_preview_viewport.add_child(_preview_camera)

	return clip

## A permanent, continuously-updating viewport (UPDATE_ALWAYS, never
## queue_free()'d) -- unlike ui/hud.gd's own portrait, which is a
## deliberate one-shot snapshot (see its own class comment), this panel
## stays open long enough that newly-equipped gear needs to show up live.
## Sharing the real game's own World3D (same technique hud.gd already
## established) means that "show newly equipped gear" falls out for free
## with zero extra sync code -- it's the SAME live Character node, just
## viewed from a second camera.
func _position_preview_camera() -> void:
	_preview_viewport.world_3d = _player.get_world_3d()
	_preview_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	var character: Node = _player.get_node_or_null("Character")
	if character == null:
		return
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color(0.10, 0.10, 0.13)
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color.WHITE
	env.ambient_light_energy = 0.9
	_preview_camera.environment = env
	# Culls on PREVIEW_VISUAL_LAYER, which only this character carries and
	# only while the panel is open. The portrait layer can't be used here:
	# EVERY Character has it, civilians included, so a full-body shot at this
	# distance picked up whoever was walking past behind the player.
	if character.has_method("set_preview_layer_enabled"):
		character.set_preview_layer_enabled(true)
	# Same cull_mask trick hud.gd's own portrait camera uses -- keeps the
	# preview to just the character, not the city bleeding through behind
	# them (this viewport shares the real game World3D, see above).
	_preview_camera.cull_mask = Character.PREVIEW_VISUAL_LAYER
	var center: Vector3 = character.global_position + Vector3(0, 0.95, 0)
	var facing: Vector3 = character.global_transform.basis.z.normalized()
	_preview_camera.global_position = center + facing * PREVIEW_CAMERA_DISTANCE + Vector3(0, 0.15, 0)
	_preview_camera.look_at(center, Vector3.UP)

func _build_storage_grid() -> Control:
	var vbox := VBoxContainer.new()
	vbox.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	vbox.add_theme_constant_override("separation", 6)

	var label := Label.new()
	label.text = "Storage"
	vbox.add_child(label)

	var grid := GridContainer.new()
	grid.columns = STORAGE_COLUMNS
	grid.add_theme_constant_override("h_separation", 4)
	grid.add_theme_constant_override("v_separation", 4)
	vbox.add_child(grid)

	for i in range(InventoryScript.STORAGE_SLOT_COUNT):
		var slot: Button = ItemSlotScript.new()
		slot.source = "storage"
		slot.index_or_key = i
		slot.panel = self
		grid.add_child(slot)
		_storage_slot_controls.append(slot)

	return vbox

## Click-to-equip/unequip -- always available regardless of drag skill, see
## this file's own class comment. A click on a storage slot holding a
## non-equippable item (e.g. scrap_metal, items/item_registry.gd's own
## LOOT_ONLY category) is a harmless no-op -- nothing TO equip it into.
## Goes through _player's own equip_from_slot()/unequip_to_slot() wrappers,
## NOT straight to _inventory -- those wrappers are what fire the
## multiplayer equip-visibility RPC (see player.gd's own class comment on
## them), so calling _inventory directly here would silently skip it.
func slot_activated(source: String, index_or_key) -> void:
	if source == "storage":
		var slot: Dictionary = _inventory.storage_slots[index_or_key]
		if slot.is_empty():
			return
		var item := ItemRegistry.get_item(slot.get("item_id", ""))
		if item.get("equip_slot", "") == "":
			return
		_player.equip_from_slot(index_or_key)
	else:
		_player.unequip_to_slot(index_or_key)
	_refresh()

## Called by ui/item_slot.gd's own _can_drop_data() -- storage<->storage
## (swap/move) is always allowed; anything targeting an equipment slot must
## actually fit that specific slot (items/item_registry.gd's own
## fits_slot()), e.g. a hat can't land on "hand_r".
func can_drop_onto(target_source: String, target_index_or_key, data: Dictionary) -> bool:
	var from_source: String = data.get("source", "")
	var from_index_or_key = data.get("index_or_key")
	if from_source == target_source and from_index_or_key == target_index_or_key:
		return false
	if target_source == "equipment":
		var item_id := _item_id_at(from_source, from_index_or_key)
		return ItemRegistry.fits_slot(item_id, target_index_or_key)
	return true

func drop_onto(target_source: String, target_index_or_key, data: Dictionary) -> void:
	var from_source: String = data.get("source", "")
	var from_index_or_key = data.get("index_or_key")
	if from_source == "storage" and target_source == "equipment":
		_player.equip_from_slot(from_index_or_key)
	elif from_source == "equipment" and target_source == "storage":
		_player.unequip_to_slot(from_index_or_key)
	elif from_source == "storage" and target_source == "storage":
		_swap_storage_slots(from_index_or_key, target_index_or_key)
	# equipment -> equipment (e.g. dragging a worn hat straight onto the
	# mask row) isn't a supported gesture -- unequip then equip separately.
	_refresh()

func _swap_storage_slots(a: int, b: int) -> void:
	var tmp: Dictionary = _inventory.storage_slots[a]
	_inventory.storage_slots[a] = _inventory.storage_slots[b]
	_inventory.storage_slots[b] = tmp

func _item_id_at(source: String, index_or_key) -> String:
	if source == "storage":
		return _inventory.storage_slots[index_or_key].get("item_id", "")
	return _inventory.equipment.get(index_or_key, "")

## Goes through the PLAYER's own spend_stat_point(), which re-checks
## affordability itself -- this panel only ever reflects the result, it
## never decides it. A rejected press is a silent no-op, so the refresh
## below simply redraws the unchanged state.
func _on_spend_stat_pressed(stat_name: String) -> void:
	if _player != null and _player.has_method("spend_stat_point"):
		_player.spend_stat_point(stat_name)
	_refresh()

func _refresh() -> void:
	for slot_key in _equipment_slot_controls:
		var control: Button = _equipment_slot_controls[slot_key]
		control.refresh(_inventory.equipment.get(slot_key, ""), 1)
	for i in range(_storage_slot_controls.size()):
		var control: Button = _storage_slot_controls[i]
		var entry: Dictionary = _inventory.storage_slots[i]
		control.refresh(entry.get("item_id", ""), entry.get("count", 1))
	_refresh_stats()
	_refresh_skills()

func _refresh_stats() -> void:
	if _player == null:
		return
	_level_label.text = "Level %d" % _player.level
	_xp_label.text = "XP: %d / %d" % [int(_player.xp), int(_player.xp_to_next_level)]
	_stat_points_label.text = "Stat Points Available: %d" % _player.stat_points
	_skill_points_label.text = "Skill Points Available: %d" % _player.skill_points

	# Health/Stamina/Mana show the effective max they currently produce
	# alongside the raw point count; Strength/Speed have no such value on
	# this node at all (they're percentages applied at their own usage
	# chokepoint, see player.gd's apply_strength() and its speed calc), so
	# those rows show the point count only.
	var derived := {
		"health": _player.max_health,
		"stamina": _player.max_stamina,
		"mana": _player.max_mana,
	}
	for entry in STAT_ROWS:
		var key: String = entry["key"]
		var row: Dictionary = _stat_rows[key]
		var points: int = _player.get("stat_%s" % key)
		var label: Label = row["label"]
		if derived.has(key):
			label.text = "%s -- %d pts (%.0f)" % [entry["label"], points, derived[key]]
		else:
			label.text = "%s -- %d pts" % [entry["label"], points]
		var button: Button = row["button"]
		button.disabled = _player.stat_points <= 0

func _refresh_skills() -> void:
	if _player == null:
		return
	_skill_points_label_skills.text = "Skill Points Available: %d" % _player.skill_points
