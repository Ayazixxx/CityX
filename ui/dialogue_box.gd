extends CanvasLayer
## Bottom-of-screen conversation box: a speaker name, one line at a time
## advanced with ENTER, and lines that can carry a set of player replies to
## pick between. Driven entirely by npc/unique_npc.gd and its subclasses;
## this node owns no conversation content of its own.
##
## A LINE'S WORTH OF SCRIPT is a Dictionary:
##     {"text": "..."}                       -- Enter advances
##     {"text": "...", "options": [           -- waits for a choice instead
##          {"text": "Sure", "id": "sure"},
##          {"text": "No",   "id": "no"},
##     ]}
## Branching is NOT expressed in here. A node with options ends the
## sequence and reports which id was picked (see option_chosen); whoever
## owns the conversation decides what to open next. That keeps this file
## free of jump targets/indices, which are the usual source of dialogue
## trees that silently go to the wrong place.
##
## ENTER, not the interact key. Enter is also the chat key, so this
## deliberately CONSUMES the keypress (set_input_as_handled) and
## ui/chat_box.gd carries a matching guard -- exactly the same two-sided
## treatment that file already documents for the dev console, which shares
## Enter for the same reason.
##
## Deliberately does NOT pause the game, matching ui/chat_box.gd's own
## reasoning. It DOES gate the player's movement/combat input while open
## (see npc/unique_npc.gd's set_ui_capturing calls) -- that was safe to add
## once advancing moved off the polled interact key and onto a real input
## event, which the gate doesn't touch.

## Emitted when the conversation runs off the end of its lines, or is closed
## early (walking away). NOT emitted when an option is picked -- that's
## option_chosen, and the owner is expected to open whatever comes next.
signal finished
## The id of the reply the player picked.
signal option_chosen(id: String)

const PANEL_HEIGHT := 168.0
const PANEL_MARGIN := 48.0
const PANEL_BOTTOM_MARGIN := 56.0
const CONTENT_MARGIN := 18.0

const BG_COLOR := Color(0.06, 0.07, 0.09, 0.92)
const BORDER_COLOR := Color(0.55, 0.63, 1.0, 0.55)
const SPEAKER_COLOR := Color(0.72, 0.79, 1.0)
const LINE_COLOR := Color(0.93, 0.94, 0.97)
const HINT_COLOR := Color(0.55, 0.58, 0.66)
const OPTION_COLOR := Color(0.78, 0.80, 0.86)
const OPTION_SELECTED_COLOR := Color(1.0, 0.87, 0.35)

const SPEAKER_FONT_SIZE := 20
const LINE_FONT_SIZE := 18
const OPTION_FONT_SIZE := 18
const HINT_FONT_SIZE := 13

## Prefixed to whichever reply is currently highlighted, so the selection
## reads without relying on colour alone.
const SELECTED_PREFIX := "> "
const UNSELECTED_PREFIX := "   "

var _panel: PanelContainer
var _speaker_label: Label
var _line_label: Label
var _options_box: VBoxContainer
var _hint_label: Label

var _nodes: Array = []
var _index := 0
var _option_buttons: Array[Button] = []
var _selected := 0
var _was_mouse_captured := false

func _ready() -> void:
	layer = 5
	# Found by group rather than by path -- a dialogue box lives under
	# whichever NPC is talking, so ui/chat_box.gd (a child of the PLAYER)
	# has no node path to reach it by. See its own Enter guard.
	add_to_group("open_dialogues")
	_build_ui()
	_panel.visible = false

func _build_ui() -> void:
	_panel = PanelContainer.new()
	_panel.anchor_left = 0.0
	_panel.anchor_right = 1.0
	_panel.anchor_top = 1.0
	_panel.anchor_bottom = 1.0
	_panel.offset_left = PANEL_MARGIN
	_panel.offset_right = -PANEL_MARGIN
	_panel.offset_top = -(PANEL_HEIGHT + PANEL_BOTTOM_MARGIN)
	_panel.offset_bottom = -PANEL_BOTTOM_MARGIN
	# PASS, not IGNORE: the reply buttons inside have to receive clicks,
	# but the panel itself shouldn't swallow anything aimed at the world.
	_panel.mouse_filter = Control.MOUSE_FILTER_PASS

	var style := StyleBoxFlat.new()
	style.bg_color = BG_COLOR
	style.border_color = BORDER_COLOR
	style.set_border_width_all(2)
	style.set_corner_radius_all(6)
	style.set_content_margin_all(CONTENT_MARGIN)
	_panel.add_theme_stylebox_override("panel", style)
	add_child(_panel)

	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 6)
	column.mouse_filter = Control.MOUSE_FILTER_PASS
	_panel.add_child(column)

	_speaker_label = Label.new()
	_speaker_label.add_theme_font_size_override("font_size", SPEAKER_FONT_SIZE)
	_speaker_label.add_theme_color_override("font_color", SPEAKER_COLOR)
	_speaker_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.add_child(_speaker_label)

	_line_label = Label.new()
	_line_label.add_theme_font_size_override("font_size", LINE_FONT_SIZE)
	_line_label.add_theme_color_override("font_color", LINE_COLOR)
	_line_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_line_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.add_child(_line_label)

	_options_box = VBoxContainer.new()
	_options_box.add_theme_constant_override("separation", 2)
	_options_box.mouse_filter = Control.MOUSE_FILTER_PASS
	column.add_child(_options_box)

	var spacer := Control.new()
	spacer.size_flags_vertical = Control.SIZE_EXPAND_FILL
	spacer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.add_child(spacer)

	_hint_label = Label.new()
	_hint_label.add_theme_font_size_override("font_size", HINT_FONT_SIZE)
	_hint_label.add_theme_color_override("font_color", HINT_COLOR)
	_hint_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	_hint_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.add_child(_hint_label)

## Starts a conversation. `nodes` is an Array of the Dictionaries described
## in this file's own class comment. An empty list closes out immediately
## (still emitting finished) rather than showing a blank box.
func open(speaker: String, nodes: Array) -> void:
	if nodes.is_empty():
		finished.emit()
		return
	_nodes = nodes
	_index = 0
	_speaker_label.text = speaker
	_panel.visible = true
	# Released for the WHOLE conversation, not just while replies are up:
	# flipping the cursor on and off between lines reads as a glitch, and
	# the camera is gated throughout anyway.
	_was_mouse_captured = Input.mouse_mode == Input.MOUSE_MODE_CAPTURED
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	_show_current_node()

func is_open() -> bool:
	return _panel != null and _panel.visible

## True while the player is being asked to pick a reply -- the owner uses
## this to know a conversation is paused on a choice rather than finished.
func is_choosing() -> bool:
	return is_open() and not _option_buttons.is_empty()

func close() -> void:
	if not is_open():
		return
	_panel.visible = false
	_clear_options()
	_nodes = []
	_index = 0
	if _was_mouse_captured:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	finished.emit()

func _show_current_node() -> void:
	_clear_options()
	var node: Dictionary = _nodes[_index]
	_line_label.text = str(node.get("text", ""))

	var options: Array = node.get("options", [])
	if options.is_empty():
		_hint_label.text = "[Enter] continue" if _index < _nodes.size() - 1 else "[Enter] close"
		return

	_selected = 0
	for i in options.size():
		var option: Dictionary = options[i]
		var button := Button.new()
		button.flat = true
		button.alignment = HORIZONTAL_ALIGNMENT_LEFT
		button.add_theme_font_size_override("font_size", OPTION_FONT_SIZE)
		button.focus_mode = Control.FOCUS_NONE
		# bind(i) rather than a captured loop variable: GDScript lambdas
		# capture by VALUE at creation, so this is safe either way, but
		# binding keeps the intent obvious.
		button.pressed.connect(_on_option_pressed.bind(i))
		# Hover moves the highlight too, so mouse and keyboard never
		# disagree about which reply is about to be picked.
		button.mouse_entered.connect(_on_option_hovered.bind(i))
		_options_box.add_child(button)
		_option_buttons.append(button)
	_refresh_option_visuals()
	_hint_label.text = "[Arrows / A D] choose    [Enter] select"

func _clear_options() -> void:
	for button in _option_buttons:
		if is_instance_valid(button):
			button.queue_free()
	_option_buttons.clear()

func _refresh_option_visuals() -> void:
	var options: Array = _nodes[_index].get("options", [])
	for i in _option_buttons.size():
		var button := _option_buttons[i]
		var label: String = str((options[i] as Dictionary).get("text", ""))
		var is_selected := i == _selected
		button.text = (SELECTED_PREFIX if is_selected else UNSELECTED_PREFIX) + label
		button.add_theme_color_override("font_color", OPTION_SELECTED_COLOR if is_selected else OPTION_COLOR)
		button.add_theme_color_override("font_hover_color", OPTION_SELECTED_COLOR)

func _on_option_hovered(index: int) -> void:
	if index == _selected:
		return
	_selected = index
	_refresh_option_visuals()

func _on_option_pressed(index: int) -> void:
	_selected = index
	_confirm_selection()

func _confirm_selection() -> void:
	var options: Array = _nodes[_index].get("options", [])
	if _selected < 0 or _selected >= options.size():
		return
	var id := str((options[_selected] as Dictionary).get("id", ""))
	# Torn down BEFORE the signal: whoever is listening will almost always
	# open the next stretch of conversation straight away, and that has to
	# land on a clean box rather than on top of the old one's own replies.
	_panel.visible = false
	_clear_options()
	_nodes = []
	_index = 0
	if _was_mouse_captured:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	option_chosen.emit(id)

func _advance() -> void:
	_index += 1
	if _index >= _nodes.size():
		close()
		return
	_show_current_node()

## _input, not _unhandled_input: Enter is shared with the chat box (and the
## dev console), and these presses have to be consumed before they reach
## either. ui/chat_box.gd carries the matching guard for the case where
## Godot happens to run its handler first.
func _input(event: InputEvent) -> void:
	if not is_open():
		return
	if not (event is InputEventKey) or not event.pressed or event.is_echo():
		return

	var key := event as InputEventKey
	if key.keycode == KEY_ENTER or key.keycode == KEY_KP_ENTER:
		get_viewport().set_input_as_handled()
		if is_choosing():
			_confirm_selection()
		else:
			_advance()
		return

	if not is_choosing():
		return

	# Arrow keys AND A/D. Both axes are accepted for a vertical list: two
	# replies read as either "up/down the list" or "left/right between
	# them" depending on the player, and there's no reason to be strict.
	var step := 0
	if key.keycode == KEY_UP or key.keycode == KEY_LEFT or key.keycode == KEY_A:
		step = -1
	elif key.keycode == KEY_DOWN or key.keycode == KEY_RIGHT or key.keycode == KEY_D:
		step = 1
	if step == 0:
		return
	get_viewport().set_input_as_handled()
	# wrapi, so pressing past either end rolls round rather than sticking.
	_selected = wrapi(_selected + step, 0, _option_buttons.size())
	_refresh_option_visuals()
