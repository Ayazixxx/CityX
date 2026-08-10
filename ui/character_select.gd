extends Control
## Shown after picking Singleplayer from the main menu: a "Select Character"
## list, "Create New" to jump into the character creator, and "Back" to
## return to the title screen (see main.gd's own screen-flow wiring).
##
## Lists every character saved via character/character_save.gd (a
## ConfigFile at user://characters.cfg, written by main.gd's own
## _on_creation_finished right after a fresh character finishes creation) --
## selecting one and pressing Play emits character_selected(id) for main.gd
## to load and spawn directly, skipping the creator entirely.
##
## Terraria "Select Player"-style rows: each saved character gets its own
## clickable row showing a rendered head-and-shoulders portrait, its name,
## "Level N", and cumulative playtime (HH:MM:SS, same format Terraria's own
## menu uses) -- see character_save.gd's own playtime_seconds/level fields.
## Portraits are captured ONCE per row, up front (see _render_portraits),
## from a standalone SubViewport that mirrors ui/hud.gd's own
## _capture_portrait technique but with its own self-contained lighting
## instead of borrowing a live city World3D, since none exists yet at this
## point in the screen flow.

const CharacterSave = preload("res://character/character_save.gd")
const CharacterScene := preload("res://character/character.tscn")
const Character = preload("res://character/character.gd")

signal create_new_pressed
signal back_pressed
signal character_selected(id: int)

# Overridable so tests can point this at a throwaway ConfigFile instead of
# the real user://characters.cfg -- same "injectable path" reasoning
# character_save.gd's own functions already use, just threaded through
# here too so a full UI-level test never touches real player save data.
# Must be set BEFORE this node enters the tree (_ready() reads it once).
var save_path: String = CharacterSave.SAVE_PATH

const PORTRAIT_BG_COLOR := Color(0.1, 0.11, 0.14)
const PORTRAIT_CAMERA_DISTANCE := 0.6
const ROW_PORTRAIT_SIZE := 88.0

# Both semi-transparent (not solid) now that this screen no longer paints
# its own opaque background panel behind them -- the rainy title screen
# shows through everywhere except these rows/the buttons, see main.gd's own
# _on_enter_game_pressed. Selected is just a lighter, more opaque grey than
# normal, not a distinct color (was blue) -- asked for explicitly.
const ROW_NORMAL_BG := Color(0.08, 0.08, 0.09, 0.55)
const ROW_SELECTED_BG := Color(0.55, 0.55, 0.55, 0.45)

@onready var _rows_container: VBoxContainer = $Panel/VBox/ListScroll/RowsContainer
@onready var _play_button: Button = $Panel/VBox/PlayButton
@onready var _delete_button: Button = $Panel/VBox/DeleteButton
@onready var _create_new_button: Button = $Panel/VBox/CreateNewButton
@onready var _back_button: Button = $Panel/VBox/BackButton
@onready var _portrait_viewport: SubViewport = $PortraitViewport
@onready var _portrait_camera: Camera3D = $PortraitViewport/PortraitCamera
@onready var _confirm_delete_overlay: ColorRect = $ConfirmDeleteOverlay
@onready var _confirm_label: Label = $ConfirmDeleteOverlay/ConfirmPanel/VBox/ConfirmLabel
@onready var _confirm_delete_button: Button = $ConfirmDeleteOverlay/ConfirmPanel/VBox/ButtonRow/ConfirmDeleteButton
@onready var _confirm_cancel_button: Button = $ConfirmDeleteOverlay/ConfirmPanel/VBox/ButtonRow/ConfirmCancelButton

var _selected_id := -1
# Saved character id -> {"panel": PanelContainer, "style": StyleBoxFlat,
# "portrait": TextureRect, "name": String} -- looked up by id (not list
# index) for the same reason character_save.gd's own ids aren't guaranteed
# contiguous or in list order. "name" is kept alongside the row's own UI
# nodes purely so the delete-confirm prompt can name the character without
# a redundant disk read.
var _rows := {}

func _ready() -> void:
	_play_button.pressed.connect(_on_play_pressed)
	_delete_button.pressed.connect(_on_delete_pressed)
	_confirm_delete_button.pressed.connect(_on_confirm_delete_pressed)
	_confirm_cancel_button.pressed.connect(_on_confirm_cancel_pressed)
	_create_new_button.pressed.connect(func(): create_new_pressed.emit())
	_back_button.pressed.connect(func(): back_pressed.emit())
	_populate_character_list()

## Escape dismisses the delete-confirm prompt first if it's open (same
## "top overlay wins" precedent character_creator.gd's own _handle_escape
## already established for its discard-confirm prompt), otherwise it's the
## normal "leave this screen" shortcut.
func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE:
		if _confirm_delete_overlay.visible:
			_on_confirm_cancel_pressed()
		else:
			back_pressed.emit()

func _populate_character_list() -> void:
	for child in _rows_container.get_children():
		child.queue_free()
	_rows.clear()
	_selected_id = -1
	_play_button.disabled = true
	_delete_button.disabled = true

	var entries: Array[Dictionary] = CharacterSave.list_characters(save_path)
	for entry in entries:
		_add_row(entry)
	_render_portraits(entries)

func _add_row(entry: Dictionary) -> void:
	var id: int = entry["id"]

	var style := StyleBoxFlat.new()
	style.bg_color = ROW_NORMAL_BG
	style.corner_radius_top_left = 6
	style.corner_radius_top_right = 6
	style.corner_radius_bottom_right = 6
	style.corner_radius_bottom_left = 6
	style.content_margin_left = 16.0
	style.content_margin_top = 14.0
	style.content_margin_right = 16.0
	style.content_margin_bottom = 14.0

	var panel := PanelContainer.new()
	panel.add_theme_stylebox_override("panel", style)
	panel.mouse_filter = Control.MOUSE_FILTER_STOP
	panel.gui_input.connect(_on_row_gui_input.bind(id))

	var hbox := HBoxContainer.new()
	hbox.add_theme_constant_override("separation", 18)
	panel.add_child(hbox)

	var portrait := TextureRect.new()
	portrait.custom_minimum_size = Vector2(ROW_PORTRAIT_SIZE, ROW_PORTRAIT_SIZE)
	portrait.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	portrait.stretch_mode = TextureRect.STRETCH_SCALE
	hbox.add_child(portrait)

	var vbox := VBoxContainer.new()
	vbox.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	hbox.add_child(vbox)

	var name_label := Label.new()
	name_label.text = String(entry["name"])
	name_label.add_theme_font_size_override("font_size", 22)
	name_label.add_theme_color_override("font_color", Color(0.9, 0.92, 1.0))
	name_label.add_theme_color_override("font_shadow_color", Color(0.05, 0.05, 0.07, 0.9))
	name_label.add_theme_constant_override("shadow_outline_size", 6)
	vbox.add_child(name_label)

	var stats_label := Label.new()
	stats_label.text = "Level %d   %s" % [int(entry["level"]), _format_playtime(entry["playtime_seconds"])]
	stats_label.add_theme_font_size_override("font_size", 17)
	stats_label.add_theme_color_override("font_color", Color(0.75, 0.78, 0.86))
	stats_label.add_theme_color_override("font_shadow_color", Color(0.05, 0.05, 0.07, 0.9))
	stats_label.add_theme_constant_override("shadow_outline_size", 6)
	vbox.add_child(stats_label)

	_rows_container.add_child(panel)
	_rows[id] = {"panel": panel, "style": style, "portrait": portrait, "name": String(entry["name"])}

func _on_row_gui_input(event: InputEvent, id: int) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		_select_row(id)

func _select_row(id: int) -> void:
	if _rows.has(_selected_id):
		_rows[_selected_id]["style"].bg_color = ROW_NORMAL_BG
	_selected_id = id
	if _rows.has(id):
		_rows[id]["style"].bg_color = ROW_SELECTED_BG
	_play_button.disabled = false
	_delete_button.disabled = false

func _on_play_pressed() -> void:
	if _selected_id < 0:
		return
	character_selected.emit(_selected_id)

## Opens the "are you sure" prompt rather than deleting immediately --
## a saved character (hours of playtime, a hand-painted design) is exactly
## the kind of destructive-and-irreversible action character_creator.gd's
## own confirm-discard prompt was already built for, just for delete instead
## of discard-on-back.
func _on_delete_pressed() -> void:
	if _selected_id < 0 or not _rows.has(_selected_id):
		return
	var character_name: String = _rows[_selected_id]["name"]
	_confirm_label.text = "Permanently delete \"%s\"?\nThis cannot be undone." % character_name
	_confirm_delete_overlay.visible = true

func _on_confirm_cancel_pressed() -> void:
	_confirm_delete_overlay.visible = false

func _on_confirm_delete_pressed() -> void:
	_confirm_delete_overlay.visible = false
	if _selected_id < 0:
		return
	CharacterSave.delete_character(_selected_id, save_path)
	_populate_character_list()

## HH:MM:SS, matching Terraria's own "time played" display on this exact menu.
static func _format_playtime(total_seconds: float) -> String:
	var total := int(total_seconds)
	var hours := total / 3600
	var minutes := (total % 3600) / 60
	var seconds := total % 60
	return "%02d:%02d:%02d" % [hours, minutes, seconds]

## Captures one portrait per row, one at a time (see _capture_one_portrait --
## rendering is inherently async, needing a couple of real frames per
## capture, so there's no benefit to trying to overlap them). Reuses a
## single SubViewport/Camera/Light (see the .tscn) across every character
## rather than spinning up one per row.
func _render_portraits(entries: Array[Dictionary]) -> void:
	# A real reported bug -- "long loading screen right when clicking
	# Singleplayer/Host, while character select loads": _populate_character_list()
	# calls this WITHOUT awaiting it, on the theory that portrait capture
	# is "async" and therefore can't block anything. That's only true AFTER
	# this function's own first await -- and the per-entry loop below does
	# real, expensive work (instantiate a full Character, decode/apply its
	# saved atlas PNG, see _capture_one_portrait) BEFORE ever reaching one,
	# on its very first iteration. That synchronous prefix still runs as
	# part of the SAME call stack as _ready(), so the first character's own
	# setup cost blocked the whole screen -- rows, buttons, AND the still-
	# supposedly-visible rainy title screen behind it -- from rendering AT
	# ALL until it finished (measured directly against a real save file:
	# over 11 real seconds). A bare await here yields back to the caller
	# immediately, so _ready() actually returns (the screen appears) before
	# any of this function's own real work starts -- same "don't let slow
	# async work run behind an already-drawn frame" principle
	# main.gd's own _fade_transition already documents fixing for city
	# loading, just never applied here too.
	await get_tree().process_frame
	_portrait_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	var portrait_env := Environment.new()
	portrait_env.background_mode = Environment.BG_COLOR
	portrait_env.background_color = PORTRAIT_BG_COLOR
	portrait_env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	portrait_env.ambient_light_color = Color.WHITE
	portrait_env.ambient_light_energy = 0.9
	_portrait_camera.environment = portrait_env
	# Restricts the camera to just the character's own dedicated visual layer
	# (see character.gd's PORTRAIT_VISUAL_LAYER) -- not strictly needed here
	# the way it is in hud.gd (this SubViewport has nothing else in it to
	# exclude), kept anyway for consistency with the one other place this
	# exact technique is used.
	_portrait_camera.cull_mask = Character.PORTRAIT_VISUAL_LAYER

	for entry in entries:
		var id: int = entry["id"]
		if not _rows.has(id):
			continue
		var data: Dictionary = CharacterSave.load_character(id, save_path)
		if data.is_empty():
			continue
		var texture: ImageTexture = await _capture_one_portrait(data["head_shape"], data["atlas_png"], data["height_scale"])
		if texture != null and _rows.has(id):
			_rows[id]["portrait"].texture = texture

## Builds a throwaway Character instance inside the shared portrait
## SubViewport, poses it with the saved head shape/paint, snaps one frame,
## then frees it -- see ui/hud.gd's own _capture_portrait for the live-world
## version of this same technique (head framing via get_head_center_world(),
## NOT the raw neck-joint position, for the same reason explained there).
func _capture_one_portrait(head_shape: String, atlas_png: PackedByteArray, height_scale: float = 1.0) -> ImageTexture:
	var character: Node3D = CharacterScene.instantiate()
	character.set_paintable(false)
	_portrait_viewport.add_child(character)
	character.set_head_shape(head_shape)
	character.apply_atlas_png(atlas_png)
	character.set_height_scale(height_scale)

	await get_tree().process_frame
	await get_tree().process_frame

	var head: Node3D = character.get_head_node()
	if head == null:
		character.queue_free()
		return null

	var head_pos: Vector3 = character.get_head_center_world()
	var facing := character.global_transform.basis.z.normalized()
	# Distance scaled with the character's own chosen height, so every row's
	# portrait frames its head the same way -- see hud.gd's own copy of this.
	_portrait_camera.global_position = head_pos + facing * PORTRAIT_CAMERA_DISTANCE * height_scale
	_portrait_camera.look_at(head_pos, Vector3.UP)

	await get_tree().process_frame
	await get_tree().process_frame

	var img := _portrait_viewport.get_texture().get_image()
	var texture := ImageTexture.create_from_image(img)

	character.queue_free()
	return texture
