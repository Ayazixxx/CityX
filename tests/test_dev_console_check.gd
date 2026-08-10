extends Node
## Functional check for the dev console: confirms it's actually present
## (OS.is_debug_build() should be true under this headless test run, same
## as any editor/debug run), that "/spawn sword" and "/spawn mustang" via
## the real UI submit path spawn a greatsword/car as a sibling of the
## player positioned in front of them, and that opening/closing it
## correctly pauses/unpauses the tree and toggles mouse mode -- the same
## pattern pause_menu.gd already uses.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _ok := true
var _player: CharacterBody3D
var _console: CanvasLayer

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	if not OS.is_debug_build():
		_fail("OS.is_debug_build() is false in this test run -- can't verify the console at all")
		print("OVERALL: FAIL")
		get_tree().quit(1)
		return

	var ground := StaticBody3D.new()
	ground.collision_layer = 1
	var gcol := CollisionShape3D.new()
	var gshape := BoxShape3D.new()
	gshape.size = Vector3(200, 1.0, 200)
	gcol.shape = gshape
	gcol.position = Vector3(0, -0.5, 0)
	ground.add_child(gcol)
	add_child(ground)

	_player = PlayerScene.instantiate()
	add_child(_player)
	var character := CharacterScene.instantiate()
	character.set_paintable(false)
	_player.add_child(character)

	_console = _player.get_node_or_null("DevConsole")
	if _console == null:
		_fail("no DevConsole child found on the player (OS.is_debug_build() was true)")
	else:
		print("PASS: dev console is present on an authority player in a debug build")

	call_deferred("_run_checks")

func _run_checks() -> void:
	if _console == null:
		print("OVERALL: FAIL")
		get_tree().quit(1)
		return

	var panel: Control = _console.get_node("Panel")
	if panel.visible:
		_fail("console starts open -- should start closed")
	else:
		print("PASS: console starts closed")

	# Open via the real toggle path (a synthetic key event routed through
	# _input, exactly like a real "`" press would be) rather than calling
	# _open() directly, so this also proves the input wiring itself works.
	var ev := InputEventKey.new()
	ev.physical_keycode = KEY_QUOTELEFT
	ev.pressed = true
	_console._input(ev)

	if panel.visible and get_tree().paused and Input.mouse_mode == Input.MOUSE_MODE_VISIBLE:
		print("PASS: toggling open shows the panel, pauses the tree, releases the mouse")
	else:
		_fail("toggling open didn't work correctly (visible=%s paused=%s mouse_mode=%s)" % [panel.visible, get_tree().paused, Input.mouse_mode])

	var input_field: LineEdit = _console.get_node("Panel/VBox/InputField")
	var player_pos_before: Vector3 = _player.global_position
	input_field.text = "/spawn sword"
	input_field.emit_signal("text_submitted", "/spawn sword")

	var sword: Node3D = null
	for child in _player.get_parent().get_children():
		if child.get_script() != null and str(child.get_script().resource_path).contains("greatsword"):
			sword = child
			break
	if sword == null:
		_fail("/spawn sword didn't actually spawn a greatsword as a sibling of the player")
	else:
		var dist := player_pos_before.distance_to(sword.global_position)
		if dist > 0.5 and dist < 5.0:
			print("PASS: /spawn sword placed a greatsword a reasonable distance in front of the player (", dist, "m)")
		else:
			_fail("spawned sword is an unreasonable distance from the player (%sm)" % dist)

	if input_field.text == "":
		print("PASS: input field cleared after submitting")
	else:
		_fail("input field wasn't cleared after submitting")

	input_field.text = "/spawn mustang"
	input_field.emit_signal("text_submitted", "/spawn mustang")

	var car: Node3D = null
	for child in _player.get_parent().get_children():
		if child.get_script() != null and str(child.get_script().resource_path).contains("vehicles/car"):
			car = child
			break
	if car == null:
		_fail("/spawn mustang didn't actually spawn a car as a sibling of the player")
	else:
		var dist := player_pos_before.distance_to(car.global_position)
		if dist > 3.0 and dist < 10.0:
			print("PASS: /spawn mustang placed a car a reasonable distance in front of the player (", dist, "m)")
		else:
			_fail("spawned car is an unreasonable distance from the player (%sm)" % dist)

	# Close it again -- same toggle path.
	_console._input(ev)
	if not panel.visible and not get_tree().paused:
		print("PASS: toggling closed hides the panel and unpauses the tree")
	else:
		_fail("toggling closed didn't work correctly (visible=%s paused=%s)" % [panel.visible, get_tree().paused])

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
