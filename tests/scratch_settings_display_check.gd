extends Node
## One-off sanity check: instantiate settings_panel.tscn headlessly, toggle
## fullscreen/resolution, and confirm the DisplayServer calls don't error
## (headless mode stubs DisplayServer, so this just proves no crash) and
## that user://settings.cfg round-trips the new "display" section
## correctly. Not part of the game.

const SettingsPanelScene := preload("res://ui/settings_panel.tscn")
const SETTINGS_PATH := "user://settings.cfg"

func _ready() -> void:
	var panel := SettingsPanelScene.instantiate()
	add_child(panel)
	await get_tree().process_frame

	var fullscreen_check: CheckButton = panel.get_node("VBox/FullscreenCheck")
	var resolution_option: OptionButton = panel.get_node("VBox/ResolutionOption")

	print("ResolutionOption item count (expect 6): ", resolution_option.item_count)
	print("FullscreenCheck default (expect true): ", fullscreen_check.button_pressed)
	print("ResolutionOption disabled while fullscreen (expect true): ", resolution_option.disabled)

	# Uncheck fullscreen -> resolution dropdown should re-enable.
	fullscreen_check.button_pressed = false
	fullscreen_check.toggled.emit(false)
	await get_tree().process_frame
	print("ResolutionOption disabled after unchecking fullscreen (expect false): ", resolution_option.disabled)

	# Pick a specific resolution (index 1 = 1366x768) and confirm it saved.
	resolution_option.select(1)
	resolution_option.item_selected.emit(1)
	await get_tree().process_frame

	var config := ConfigFile.new()
	var load_ok := config.load(SETTINGS_PATH) == OK
	var saved_fullscreen: bool = config.get_value("display", "fullscreen", true) if load_ok else true
	var saved_res_index: int = config.get_value("display", "resolution_index", -1) if load_ok else -1
	print("Saved fullscreen (expect false): ", saved_fullscreen)
	print("Saved resolution_index (expect 1): ", saved_res_index)

	# Re-toggle fullscreen back on and confirm the dropdown disables + saves again.
	fullscreen_check.button_pressed = true
	fullscreen_check.toggled.emit(true)
	await get_tree().process_frame
	config = ConfigFile.new()
	config.load(SETTINGS_PATH)
	var saved_fullscreen_2: bool = config.get_value("display", "fullscreen", false)
	print("ResolutionOption disabled after re-checking fullscreen (expect true): ", resolution_option.disabled)
	print("Saved fullscreen after re-check (expect true): ", saved_fullscreen_2)

	# A SECOND panel instance loading the same config should pick up the
	# saved state (mirrors how pause_menu's own SettingsPanel and the main
	# menu's own SettingsPanel are two separate instances sharing one file).
	var panel2 := SettingsPanelScene.instantiate()
	add_child(panel2)
	await get_tree().process_frame
	var resolution_option_2: OptionButton = panel2.get_node("VBox/ResolutionOption")
	print("Second instance loads saved resolution_index selection (expect 1): ", resolution_option_2.selected)

	var ok := resolution_option.item_count == 6 and not saved_fullscreen and saved_res_index == 1 and saved_fullscreen_2 and resolution_option_2.selected == 1
	print("OVERALL: PASS" if ok else "OVERALL: FAIL")
	get_tree().quit()
