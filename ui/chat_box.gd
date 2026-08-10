extends CanvasLayer
## Proximity text chat input: "Enter" opens a text field, type a message,
## "Enter" again sends it (as a speech bubble above the sender's own head --
## see character.gd's own show_speech_bubble()) and closes the field;
## "Escape" cancels without sending. Lives as a direct child of the player
## it belongs to (see player.gd's own _ready()), same placement reasoning
## the dev console/HUD/pause menu already use.
##
## Deliberately does NOT pause the game (unlike ui/dev_console.gd) --
## chat is meant to be usable mid-fight/mid-walk, both for the typing
## player (who can keep seeing the world move) and for everyone else (whose
## own game obviously can't pause just because one peer is typing). Mouse
## capture IS released while open though (same _was_mouse_captured pattern
## dev_console.gd already uses), since typing doesn't need it and an
## accidental mouse bump shouldn't spin the camera around mid-message.
##
## player.gd's own input handling checks is_open() to suppress WASD/combat
## input while typing -- Input.is_action_pressed() polling doesn't respect
## UI focus on its own (a focused LineEdit only intercepts _input/
## _unhandled_input callbacks, not raw action polling), so that
## suppression has to be explicit, not automatic.

signal message_sent(text: String)

const MAX_MESSAGE_LENGTH := 120

@onready var _panel: Control = $Panel
@onready var _input_field: LineEdit = $Panel/InputField

var _was_mouse_captured := false

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	_panel.visible = false
	_input_field.max_length = MAX_MESSAGE_LENGTH
	_input_field.text_submitted.connect(_on_text_submitted)

func _input(event: InputEvent) -> void:
	if not _panel.visible:
		if event.is_action_pressed("chat") and not event.is_echo():
			# The dev console (see ui/dev_console.gd, a debug-only sibling
			# under the same player) is ALSO bound to Enter for submitting a
			# command -- both are independent _input() handlers reacting to
			# the exact same keypress, and Godot doesn't guarantee which
			# sibling's _input() runs first, so this can't just assume the
			# console will have already consumed it by the time this runs.
			# Checked explicitly instead (same "suppression has to be
			# explicit, not automatic" reasoning this file's own class
			# comment already gives for chat_open elsewhere) -- reported
			# directly: pressing Enter to submit a console command also
			# popped this chat box open on top of it.
			var console := get_parent().get_node_or_null("DevConsole")
			if console != null and console.has_method("is_open") and console.is_open():
				return
			# Same treatment, same reason, for an in-progress conversation:
			# ui/dialogue_box.gd advances on Enter too (see its own class
			# comment). It consumes the press itself, but sibling _input()
			# order isn't guaranteed, so pressing Enter to read the next
			# line must not also pop the chat box open behind it. The box
			# lives under whichever NPC is talking, not under the player,
			# so it's found by group rather than by node path.
			for dialogue in get_tree().get_nodes_in_group("open_dialogues"):
				if is_instance_valid(dialogue) and dialogue.has_method("is_open") and dialogue.is_open():
					return
			get_viewport().set_input_as_handled()
			_open()
		return
	# While open: Escape cancels (Enter is handled by the LineEdit's own
	# text_submitted signal instead, see _ready()).
	if event is InputEventKey and event.pressed and not event.is_echo() and event.keycode == KEY_ESCAPE:
		get_viewport().set_input_as_handled()
		_close()

func is_open() -> bool:
	return _panel.visible

func _open() -> void:
	_was_mouse_captured = Input.mouse_mode == Input.MOUSE_MODE_CAPTURED
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	_panel.visible = true
	_input_field.text = ""
	_input_field.grab_focus()

func _close() -> void:
	_panel.visible = false
	if _was_mouse_captured:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	_input_field.release_focus()

func _on_text_submitted(text: String) -> void:
	var trimmed := text.strip_edges()
	_close()
	if trimmed.is_empty():
		return
	message_sent.emit(trimmed)
