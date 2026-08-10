extends PanelContainer
## Reusable settings panel (Master/Music volume, voice chat device selection
## + push-to-talk/open-mic, persisted via a small ConfigFile at
## user://settings.cfg) -- extracted out of pause_menu.gd so BOTH the
## in-game pause menu AND the main menu (see ui/main_menu.gd) can show the
## exact same settings without duplicating the bus-volume/device/
## persistence logic in two places. Owns its own slider/dropdown wiring and
## persistence entirely; a parent just shows/hides this and listens for
## back_pressed to know when to switch back to whatever it was showing
## before.
##
## Output/input device selection (AudioServer.output_device/input_device)
## are genuinely GLOBAL engine settings, not anything specific to one
## Player instance -- applying them here (rather than in player/voice_chat
## logic) means they're already correctly set the moment a game session
## starts, same as this panel already applies bus volumes on load. Actual
## voice CAPTURE/transmission (reading the selected input device's audio)
## lives in player.gd's own voice chat code -- this panel only owns WHICH
## devices are selected and whether push-to-talk or open-mic applies, both
## read from user://settings.cfg's own "voice" section.

signal back_pressed

const SETTINGS_PATH := "user://settings.cfg"

# A small curated list rather than DisplayServer.screen_get_size()-derived
# options -- keeps the dropdown short and predictable across every monitor
# rather than trying to enumerate "every resolution this screen supports."
const RESOLUTIONS := [
	Vector2i(1280, 720),
	Vector2i(1366, 768),
	Vector2i(1600, 900),
	Vector2i(1920, 1080),
	Vector2i(2560, 1440),
	Vector2i(3840, 2160),
]
const DEFAULT_RESOLUTION_INDEX := 3 # 1920x1080

@onready var master_slider: HSlider = $VBox/MasterSlider
@onready var music_slider: HSlider = $VBox/MusicSlider
@onready var _output_device_option: OptionButton = $VBox/OutputDeviceOption
@onready var _input_device_option: OptionButton = $VBox/InputDeviceOption
@onready var _push_to_talk_check: CheckButton = $VBox/PushToTalkCheck
@onready var _fullscreen_check: CheckButton = $VBox/FullscreenCheck
@onready var _resolution_option: OptionButton = $VBox/ResolutionOption
@onready var back_button: Button = $VBox/BackButton

func _ready() -> void:
	back_button.pressed.connect(func(): back_pressed.emit())
	master_slider.value_changed.connect(_on_master_changed)
	music_slider.value_changed.connect(_on_music_changed)
	_output_device_option.item_selected.connect(_on_output_device_selected)
	_input_device_option.item_selected.connect(_on_input_device_selected)
	_push_to_talk_check.toggled.connect(_on_push_to_talk_toggled)
	_fullscreen_check.toggled.connect(_on_fullscreen_toggled)
	_resolution_option.item_selected.connect(_on_resolution_selected)
	_populate_device_lists()
	_populate_resolution_list()
	_load_settings()

func _on_master_changed(value: float) -> void:
	_set_bus_volume("Master", value)
	_save_settings()

func _on_music_changed(value: float) -> void:
	_set_bus_volume("Music", value)
	_save_settings()

## No-op if the named bus doesn't exist (e.g. a stale/missing bus layout)
## rather than erroring -- a slider that silently does nothing is a much
## smaller problem than a crash on opening the settings panel.
func _set_bus_volume(bus_name: String, linear: float) -> void:
	var idx := AudioServer.get_bus_index(bus_name)
	if idx == -1:
		return
	AudioServer.set_bus_volume_db(idx, linear_to_db(linear))

## "Default" is always item 0 in both dropdowns -- lets a saved setting
## referencing a device that's no longer plugged in fall back cleanly
## (AudioServer.output_device/input_device both already treat the literal
## string "Default" as "whatever the OS's current default is", so this
## isn't a special case to handle separately, just the natural fallback).
func _populate_device_lists() -> void:
	_output_device_option.clear()
	for device in AudioServer.get_output_device_list():
		_output_device_option.add_item(device)
	_input_device_option.clear()
	for device in AudioServer.get_input_device_list():
		_input_device_option.add_item(device)

func _select_device_item(option: OptionButton, device_name: String) -> void:
	for i in range(option.item_count):
		if option.get_item_text(i) == device_name:
			option.select(i)
			return
	if option.item_count > 0:
		option.select(0)

func _on_output_device_selected(index: int) -> void:
	AudioServer.output_device = _output_device_option.get_item_text(index)
	_save_settings()

func _on_input_device_selected(index: int) -> void:
	AudioServer.input_device = _input_device_option.get_item_text(index)
	_save_settings()

func _on_push_to_talk_toggled(_pressed: bool) -> void:
	_save_settings()

func _populate_resolution_list() -> void:
	_resolution_option.clear()
	for res in RESOLUTIONS:
		_resolution_option.add_item("%dx%d" % [res.x, res.y])

func _on_fullscreen_toggled(pressed: bool) -> void:
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_FULLSCREEN if pressed else DisplayServer.WINDOW_MODE_WINDOWED)
	_resolution_option.disabled = pressed
	if not pressed:
		_apply_resolution(_resolution_option.selected)
	_save_settings()

func _on_resolution_selected(index: int) -> void:
	_apply_resolution(index)
	_save_settings()

func _apply_resolution(index: int) -> void:
	if index < 0 or index >= RESOLUTIONS.size():
		return
	DisplayServer.window_set_size(RESOLUTIONS[index])
	var screen_size := DisplayServer.screen_get_size()
	var window_size := DisplayServer.window_get_size()
	DisplayServer.window_set_position((screen_size - window_size) / 2)

func _load_settings() -> void:
	var config := ConfigFile.new()
	var master := 1.0
	var music := 1.0
	var output_device := "Default"
	var input_device := "Default"
	var push_to_talk := true
	var fullscreen := true
	var resolution_index := DEFAULT_RESOLUTION_INDEX
	if config.load(SETTINGS_PATH) == OK:
		master = config.get_value("audio", "master_volume", 1.0)
		music = config.get_value("audio", "music_volume", 1.0)
		output_device = config.get_value("voice", "output_device", "Default")
		input_device = config.get_value("voice", "input_device", "Default")
		push_to_talk = config.get_value("voice", "push_to_talk", true)
		fullscreen = config.get_value("display", "fullscreen", true)
		resolution_index = config.get_value("display", "resolution_index", DEFAULT_RESOLUTION_INDEX)
	master_slider.value = master
	music_slider.value = music
	_set_bus_volume("Master", master)
	_set_bus_volume("Music", music)

	_select_device_item(_output_device_option, output_device)
	AudioServer.output_device = output_device
	_select_device_item(_input_device_option, input_device)
	AudioServer.input_device = input_device
	_push_to_talk_check.button_pressed = push_to_talk

	resolution_index = clampi(resolution_index, 0, RESOLUTIONS.size() - 1)
	_resolution_option.select(resolution_index)
	_resolution_option.disabled = fullscreen
	_fullscreen_check.button_pressed = fullscreen
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_FULLSCREEN if fullscreen else DisplayServer.WINDOW_MODE_WINDOWED)
	if not fullscreen:
		_apply_resolution(resolution_index)

func _save_settings() -> void:
	var config := ConfigFile.new()
	config.set_value("audio", "master_volume", master_slider.value)
	config.set_value("audio", "music_volume", music_slider.value)
	config.set_value("voice", "output_device", _output_device_option.get_item_text(_output_device_option.selected) if _output_device_option.selected >= 0 else "Default")
	config.set_value("voice", "input_device", _input_device_option.get_item_text(_input_device_option.selected) if _input_device_option.selected >= 0 else "Default")
	config.set_value("voice", "push_to_talk", _push_to_talk_check.button_pressed)
	config.set_value("display", "fullscreen", _fullscreen_check.button_pressed)
	config.set_value("display", "resolution_index", _resolution_option.selected)
	config.save(SETTINGS_PATH)

## Read directly by player.gd's own voice chat code -- true means only
## transmit while "voice_talk" (hold V) is held, false means transmit
## continuously (open mic) whenever there's anything to send.
static func is_push_to_talk() -> bool:
	var config := ConfigFile.new()
	if config.load(SETTINGS_PATH) != OK:
		return true
	return config.get_value("voice", "push_to_talk", true)
