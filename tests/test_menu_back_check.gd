extends Node
## Regression check for "Escape/Back to go back" across the menu screens
## that got it added: character_creator.gd's own gated Back/Escape (with
## its discard-confirm prompt if the player has actually painted anything,
## see character.gd's own can_undo()), character_select.gd's ESC, and
## title_screen.gd's ESC stepping back one panel at a time.
##
## Node-based (not a plain --script SceneTree), same as
## test_spawn_authority_check.gd -- title_screen.gd references the Net
## autoload, and that identifier fails to resolve at compile time when its
## script is first loaded from a bare SceneTree entry point (confirmed via
## an isolated probe script loading title_screen.tscn alone); going through
## a real scene tree root sidesteps it, matching this project's established
## "Node-based vs SceneTree-based" test split.

var _ok := true
var _frame := 0

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _pass(msg: String) -> void:
	print("PASS: ", msg)

func _process(_delta: float) -> void:
	_frame += 1
	if _frame < 5:
		return
	_run_checks()
	get_tree().quit(0 if _ok else 1)
	set_process(false)

func _run_checks() -> void:
	_check_creator_no_paint()
	_check_creator_with_paint()
	_check_creator_escape_routing()
	_check_character_select_escape()
	_check_title_screen_escape()
	print("OVERALL: ", "PASS" if _ok else "FAIL")

## Nothing painted yet -- Back should leave immediately, no confirm prompt.
func _check_creator_no_paint() -> void:
	var creator: Node3D = load("res://character_creator/character_creator.tscn").instantiate()
	add_child(creator)
	# A plain local bool doesn't work here -- GDScript lambdas capture outer
	# locals BY VALUE, so `func(): cancelled = true` would silently mutate a
	# throwaway copy (confirmed via a direct probe: the lambda body itself
	# ran, but the outer `cancelled` never changed). A one-element array is
	# captured by reference (it's the array's underlying data, not the
	# array variable, that's shared), which is this project's own existing
	# workaround where it needs a lambda to mutate outer state.
	var cancelled := [false]
	creator.creation_cancelled.connect(func(): cancelled[0] = true)

	creator._try_go_back()
	if not cancelled[0]:
		_fail("Back with nothing painted did not cancel immediately")
	elif creator.get_node("UI/ConfirmBackOverlay").visible:
		_fail("confirm overlay shown despite nothing being painted")
	else:
		_pass("Back with nothing painted skips the confirm prompt")

	creator.queue_free()

## At least one paint edit landed -- Back should show the confirm prompt
## instead of leaving immediately; Cancel keeps you in the creator, Discard
## actually leaves.
func _check_creator_with_paint() -> void:
	var creator: Node3D = load("res://character_creator/character_creator.tscn").instantiate()
	add_child(creator)
	var character: Node3D = creator.get_node("RotationPivot/Character")
	character.push_undo_snapshot()

	var cancelled := [false]
	creator.creation_cancelled.connect(func(): cancelled[0] = true)
	var overlay: ColorRect = creator.get_node("UI/ConfirmBackOverlay")

	creator._try_go_back()
	if cancelled[0]:
		_fail("Back with paint present left immediately instead of confirming")
	elif not overlay.visible:
		_fail("confirm overlay did not open despite paint being present")
	else:
		_pass("Back with paint present opens the confirm prompt")

	creator._on_confirm_cancel_pressed()
	if cancelled[0] or overlay.visible:
		_fail("Cancel on the confirm prompt did not just close it")
	else:
		_pass("Cancel on the confirm prompt stays in the creator")

	creator._try_go_back()
	creator._on_confirm_discard_pressed()
	if not cancelled[0]:
		_fail("Discard on the confirm prompt did not fire creation_cancelled")
	elif overlay.visible:
		_fail("confirm overlay still visible after Discard")
	else:
		_pass("Discard on the confirm prompt fires creation_cancelled")

	creator.queue_free()

## Escape's meaning depends on what's already on top: dismiss the confirm
## prompt if it's open, close the name-entry prompt (not leave outright) if
## THAT's open, otherwise behave like Back.
func _check_creator_escape_routing() -> void:
	var creator: Node3D = load("res://character_creator/character_creator.tscn").instantiate()
	add_child(creator)
	var character: Node3D = creator.get_node("RotationPivot/Character")
	var confirm_overlay: ColorRect = creator.get_node("UI/ConfirmBackOverlay")
	var name_overlay: ColorRect = creator.get_node("UI/NameEntryOverlay")
	var cancelled := [false]
	creator.creation_cancelled.connect(func(): cancelled[0] = true)

	name_overlay.visible = true
	creator._handle_escape()
	if name_overlay.visible or cancelled[0]:
		_fail("Escape during name entry did not just close that prompt")
	else:
		_pass("Escape during name entry closes it without leaving")

	character.push_undo_snapshot()
	creator._handle_escape()
	if not confirm_overlay.visible:
		_fail("Escape with paint present did not open the confirm prompt")
	else:
		creator._handle_escape()
		if confirm_overlay.visible or cancelled[0]:
			_fail("Escape while confirm prompt is open did not dismiss it")
		else:
			_pass("Escape while confirm prompt is open dismisses it (same as Cancel)")

	creator.queue_free()

func _check_character_select_escape() -> void:
	var select: Control = load("res://ui/character_select.tscn").instantiate()
	add_child(select)
	var went_back := [false]
	select.back_pressed.connect(func(): went_back[0] = true)

	var esc := InputEventKey.new()
	esc.keycode = KEY_ESCAPE
	esc.pressed = true
	select._unhandled_input(esc)

	if not went_back[0]:
		_fail("Escape on character select did not emit back_pressed")
	else:
		_pass("Escape on character select emits back_pressed")

	select.queue_free()

## Escape steps back one level: Multiplayer/Settings sub-panel -> root menu
## -> "press any key" idle state.
func _check_title_screen_escape() -> void:
	var title: Control = load("res://title_screen/title_screen.tscn").instantiate()
	add_child(title)

	title._reveal_menu()
	title.get_node("MultiplayerPanel").visible = true
	title.get_node("MenuPanel").visible = false
	title._on_escape_pressed()
	if title.get_node("MultiplayerPanel").visible or not title.get_node("MenuPanel").visible:
		_fail("Escape did not back out of the multiplayer panel to the root menu")
	else:
		_pass("Escape backs out of the multiplayer panel to the root menu")

	title._on_escape_pressed()
	if title.get_node("MenuPanel").visible or title.get_node("PromptLabel").visible == false:
		_fail("Escape did not back out of the root menu to the idle state")
	else:
		_pass("Escape backs out of the root menu to the idle 'press any key' state")

	title.queue_free()
