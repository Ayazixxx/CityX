extends Node
## Regression check for the pause menu: Escape should open it (pausing the
## tree, releasing the mouse), Escape again (or Resume) should close it and
## restore whatever the mouse mode was before, Settings should navigate to
## the volume sliders, and moving a slider should actually change the
## corresponding AudioServer bus's volume and persist across a reload.
##
## Needs a real window (not --headless): MOUSE_MODE_CAPTURED silently
## no-ops without a display server.

const PauseMenuScene := preload("res://ui/pause_menu.tscn")

var _menu: CanvasLayer
var _frame := 0

func _ready() -> void:
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	_menu = PauseMenuScene.instantiate()
	add_child(_menu)

func _key_event(keycode: int) -> void:
	var ev := InputEventKey.new()
	ev.keycode = keycode
	ev.pressed = true
	_menu._unhandled_input(ev)

func _process(_delta: float) -> void:
	_frame += 1
	if _frame < 3:
		return
	_run_checks()
	get_tree().quit()

func _run_checks() -> void:
	var ok := true
	var dim: ColorRect = _menu.dim

	# 1. Standing check: menu starts closed, tree unpaused.
	if dim.visible or get_tree().paused:
		print("FAIL: pause menu should start closed and unpaused")
		ok = false
	else:
		print("PASS: pause menu starts closed, game unpaused")

	# 2. Escape opens it: pauses the tree, releases the mouse.
	_key_event(KEY_ESCAPE)
	if dim.visible and get_tree().paused and Input.mouse_mode == Input.MOUSE_MODE_VISIBLE:
		print("PASS: Escape opens the menu, pauses the tree, releases the mouse")
	else:
		print("FAIL: Escape didn't open/pause/release correctly (visible=", dim.visible, " paused=", get_tree().paused, " mouse_mode=", Input.mouse_mode, ")")
		ok = false

	# 3. Settings button navigates to the settings panel.
	_menu.settings_button.pressed.emit()
	if _menu.settings_panel.visible and not _menu.pause_panel.visible:
		print("PASS: Settings button shows the settings panel")
	else:
		print("FAIL: Settings navigation didn't switch panels")
		ok = false

	# 4. Moving the master slider actually changes the Master bus's volume.
	var master_idx := AudioServer.get_bus_index("Master")
	_menu.master_slider.value = 0.5
	var expected_db := linear_to_db(0.5)
	var actual_db := AudioServer.get_bus_volume_db(master_idx)
	if is_equal_approx(actual_db, expected_db):
		print("PASS: master slider actually changes the Master bus volume (", actual_db, " dB)")
	else:
		print("FAIL: master bus volume didn't match slider (expected ", expected_db, ", got ", actual_db, ")")
		ok = false

	# 5. The Music bus exists (from default_bus_layout.tres) and responds
	# to the music slider too.
	var music_idx := AudioServer.get_bus_index("Music")
	if music_idx == -1:
		print("FAIL: no 'Music' bus exists -- default_bus_layout.tres isn't wired up")
		ok = false
	else:
		_menu.music_slider.value = 0.25
		var music_db := AudioServer.get_bus_volume_db(music_idx)
		if is_equal_approx(music_db, linear_to_db(0.25)):
			print("PASS: music slider changes the Music bus volume (", music_db, " dB)")
		else:
			print("FAIL: music bus volume didn't match slider (got ", music_db, ")")
			ok = false

	# 6. Back button returns to the main pause panel.
	_menu.back_button.pressed.emit()
	if _menu.pause_panel.visible and not _menu.settings_panel.visible:
		print("PASS: Back button returns to the pause panel")
	else:
		print("FAIL: Back button didn't return to the pause panel")
		ok = false

	# 7. Escape again (closing from the main panel) unpauses and restores
	# the mouse to captured (since it was captured before opening).
	_key_event(KEY_ESCAPE)
	if not dim.visible and not get_tree().paused and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		print("PASS: Escape closes the menu, unpauses, restores captured mouse")
	else:
		print("FAIL: closing didn't restore state correctly (visible=", dim.visible, " paused=", get_tree().paused, " mouse_mode=", Input.mouse_mode, ")")
		ok = false

	# 8. Settings persist: a fresh menu instance loads the same values back.
	var config := ConfigFile.new()
	var loaded_ok := config.load("user://settings.cfg") == OK
	var saved_master: float = config.get_value("audio", "master_volume", -1.0)
	var saved_music: float = config.get_value("audio", "music_volume", -1.0)
	if loaded_ok and is_equal_approx(saved_master, 0.5) and is_equal_approx(saved_music, 0.25):
		print("PASS: settings persisted to user://settings.cfg (master=", saved_master, " music=", saved_music, ")")
	else:
		print("FAIL: settings didn't persist correctly (loaded=", loaded_ok, " master=", saved_master, " music=", saved_music, ")")
		ok = false

	print("OVERALL: ", "PASS" if ok else "FAIL")
