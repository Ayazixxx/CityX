extends SceneTree
## Regression check for the character-creator name-entry step: the overlay
## should stay hidden until "Enter City" is pressed, reject an empty/
## whitespace-only name, and only fire creation_finished (with the name
## applied to the character) once a real name is confirmed.

var _creator: Node3D
var _frame := 0
var _got_finished_character: Node3D = null

func _init():
	var CreatorScene: PackedScene = load("res://character_creator/character_creator.tscn")
	_creator = CreatorScene.instantiate()
	get_root().add_child(_creator)
	_creator.creation_finished.connect(func(character): _got_finished_character = character)

func _process(_delta: float) -> bool:
	_frame += 1
	if _frame < 3:
		return false
	_run_checks()
	return false

func _run_checks() -> void:
	var ok := true
	var overlay: ColorRect = _creator.get_node("UI/NameEntryOverlay")
	var line_edit: LineEdit = _creator.get_node("UI/NameEntryOverlay/NamePanel/VBox/NameLineEdit")

	if overlay.visible:
		print("FAIL: name overlay visible before Enter City was pressed")
		ok = false
	else:
		print("PASS: name overlay starts hidden")

	_creator._on_enter_city_pressed()
	if not overlay.visible:
		print("FAIL: name overlay did not open on Enter City")
		ok = false
	else:
		print("PASS: name overlay opens on Enter City")

	line_edit.text = "   "
	_creator._on_name_confirmed()
	if _got_finished_character != null:
		print("FAIL: creation_finished fired with a blank/whitespace name")
		ok = false
	elif not overlay.visible:
		print("FAIL: overlay closed despite a blank name being rejected")
		ok = false
	else:
		print("PASS: blank name rejected, overlay stays open")

	line_edit.text = "  Voss  "
	_creator._on_name_confirmed()
	if _got_finished_character == null:
		print("FAIL: creation_finished did not fire for a valid name")
		ok = false
	elif _got_finished_character.get_character_name() != "Voss":
		print("FAIL: character name mismatch, got '", _got_finished_character.get_character_name(), "'")
		ok = false
	else:
		print("PASS: creation_finished fired with trimmed name 'Voss'")

	if overlay.visible:
		print("FAIL: overlay still visible after successful confirm")
		ok = false
	else:
		print("PASS: overlay hides after successful confirm")

	print("OVERALL: ", "PASS" if ok else "FAIL")
	quit(0 if ok else 1)
