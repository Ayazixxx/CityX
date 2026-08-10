extends CanvasLayer
## Escape opens/closes this pause menu. While open, the game is fully
## paused (get_tree().paused = true) and the mouse cursor is released so
## the player can click its buttons/sliders -- this node's own
## process_mode is ALWAYS specifically so it keeps receiving input (to
## notice Escape again, or a button press) while everything else in the
## tree is frozen.
##
## Settings live in ui/settings_panel.gd now (shared with the main menu's
## own Settings button, see ui/main_menu.gd) -- this file just shows/hides
## it and listens for its back_pressed signal, it doesn't own any of the
## slider/persistence logic itself anymore. master_slider/music_slider/
## back_button stay exposed here (reaching into the instanced
## SettingsPanel's own internal nodes via the same paths as before) purely
## so tests/test_pause_menu_check.gd's existing direct node access keeps
## working unchanged.

@onready var dim: ColorRect = $Dim
@onready var pause_panel: Control = $Dim/PausePanel
@onready var settings_panel: Control = $Dim/SettingsPanel
@onready var resume_button: Button = $Dim/PausePanel/VBox/ResumeButton
@onready var settings_button: Button = $Dim/PausePanel/VBox/SettingsButton
@onready var back_button: Button = $Dim/SettingsPanel/VBox/BackButton
@onready var master_slider: HSlider = $Dim/SettingsPanel/VBox/MasterSlider
@onready var music_slider: HSlider = $Dim/SettingsPanel/VBox/MusicSlider

# Restored to whatever it was right before opening (visible while paused,
# so the player can see the cursor over the menu) rather than always
# force-capturing on close -- if the mouse was already free for some other
# reason, resuming shouldn't yank it back.
var _was_mouse_captured := false

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	resume_button.pressed.connect(_on_resume_pressed)
	settings_button.pressed.connect(_on_settings_pressed)
	settings_panel.back_pressed.connect(_on_back_pressed)

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE:
		if dim.visible:
			_close()
		else:
			_open()

func _open() -> void:
	_was_mouse_captured = Input.mouse_mode == Input.MOUSE_MODE_CAPTURED
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	get_tree().paused = true
	dim.visible = true
	pause_panel.visible = true
	settings_panel.visible = false

func _close() -> void:
	dim.visible = false
	get_tree().paused = false
	if _was_mouse_captured:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED

func _on_resume_pressed() -> void:
	_close()

func _on_settings_pressed() -> void:
	pause_panel.visible = false
	settings_panel.visible = true

func _on_back_pressed() -> void:
	settings_panel.visible = false
	pause_panel.visible = true
