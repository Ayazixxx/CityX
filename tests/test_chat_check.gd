extends Node
## Regression check for proximity text chat: character.gd's own
## show_speech_bubble() (the floating Label3D above a character's head,
## auto-hiding after SPEECH_BUBBLE_DURATION_SECONDS), ui/chat_box.gd's own
## open/type/submit/cancel flow, and player.gd's own chat_open gating (WASD/
## jump/dash/combat input must all be suppressed while typing -- a focused
## LineEdit doesn't stop Input.is_action_pressed() polling on its own, see
## player.gd's own comment on why that has to be explicit).

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")
const ChatBoxScene := preload("res://ui/chat_box.tscn")
const DevConsoleScene := preload("res://ui/dev_console.tscn")

var _ok := true
var _player: CharacterBody3D
var _character: Node3D

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
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
	_character = CharacterScene.instantiate()
	_character.set_paintable(false)
	_player.add_child(_character)

	call_deferred("_run_checks")

func _run_checks() -> void:
	_run_speech_bubble_checks()
	_run_chat_box_checks()
	_run_input_gating_checks()
	_run_console_open_suppresses_chat_check()
	_run_console_still_submits_on_real_enter_check()

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

func _run_speech_bubble_checks() -> void:
	if _character._speech_bubble == null:
		_fail("character.gd never built its own speech bubble Label3D")
		return
	if _character._speech_bubble.visible:
		_fail("speech bubble is visible before any message was ever shown")
	else:
		print("PASS: speech bubble starts hidden")

	_character.show_speech_bubble("hello world")
	if _character._speech_bubble.visible and _character._speech_bubble.text == "hello world":
		print("PASS: show_speech_bubble() shows the bubble with the right text")
	else:
		_fail("show_speech_bubble() didn't set visible/text correctly (visible=%s text='%s')" % [_character._speech_bubble.visible, _character._speech_bubble.text])

	# Not yet expired.
	_character._process(_character.SPEECH_BUBBLE_DURATION_SECONDS - 1.0)
	if _character._speech_bubble.visible:
		print("PASS: bubble stays visible before its own duration elapses")
	else:
		_fail("bubble hid too early")

	# Now past the duration.
	_character._process(2.0)
	if not _character._speech_bubble.visible:
		print("PASS: bubble auto-hides once SPEECH_BUBBLE_DURATION_SECONDS elapses")
	else:
		_fail("bubble is still visible after its own duration fully elapsed")

func _run_chat_box_checks() -> void:
	var chat := ChatBoxScene.instantiate()
	add_child(chat)

	if chat.is_open():
		_fail("chat box starts open")
	else:
		print("PASS: chat box starts closed")

	var pressed_enter := InputEventKey.new()
	# The "chat" action is bound via physical_keycode (see project.godot),
	# not keycode -- InputMap matching for a physical_keycode-bound action
	# needs the event's own physical_keycode set, not just keycode (a real,
	# confirmed test bug on the first attempt here: setting only .keycode
	# made is_action_pressed("chat") never match at all).
	pressed_enter.physical_keycode = KEY_ENTER
	pressed_enter.pressed = true
	chat._input(pressed_enter)
	if chat.is_open():
		print("PASS: pressing Enter opens the chat box")
	else:
		_fail("Enter didn't open the chat box")

	var received := [""]
	chat.message_sent.connect(func(text: String): received[0] = text)
	chat._input_field.text = "gg"
	chat._on_text_submitted("gg")
	if not chat.is_open() and received[0] == "gg":
		print("PASS: submitting text closes the box and emits message_sent with the typed text")
	else:
		_fail("submit didn't work correctly (still open=%s, received='%s')" % [chat.is_open(), received[0]])

	# Empty messages shouldn't emit at all.
	received[0] = "unset"
	var pressed_enter_2 := InputEventKey.new()
	pressed_enter_2.physical_keycode = KEY_ENTER
	pressed_enter_2.pressed = true
	chat._input(pressed_enter_2)
	chat._on_text_submitted("   ")
	if received[0] == "unset":
		print("PASS: submitting an empty/whitespace-only message doesn't emit message_sent")
	else:
		_fail("empty message still emitted message_sent")

	# Escape cancels without emitting.
	received[0] = "unset"
	var pressed_enter_3 := InputEventKey.new()
	pressed_enter_3.physical_keycode = KEY_ENTER
	pressed_enter_3.pressed = true
	chat._input(pressed_enter_3)
	var esc := InputEventKey.new()
	esc.keycode = KEY_ESCAPE
	esc.pressed = true
	chat._input(esc)
	if not chat.is_open() and received[0] == "unset":
		print("PASS: Escape closes the box without sending a message")
	else:
		_fail("Escape didn't cancel correctly (open=%s, received='%s')" % [chat.is_open(), received[0]])

	chat.queue_free()

func _run_input_gating_checks() -> void:
	if _player._chat_box == null:
		_fail("player.gd never instantiated its own chat box (_chat_box is null)")
		return

	# Simulate opening chat directly (bypassing real Input event timing,
	# same "drive state directly" style other tests in this project already
	# use for input-adjacent checks).
	_player._chat_box._open()
	Input.action_press("move_forward")
	_player._physics_process(1.0 / 60.0)
	if _player.velocity.length() < 0.01:
		print("PASS: WASD movement is suppressed while chat is open")
	else:
		_fail("player still moved (velocity=%s) while chat was open" % _player.velocity)
	Input.action_release("move_forward")
	_player._chat_box._close()

## Regression check for a real reported bug: "when i press enter while
## console is open it opens the text chat" -- both ui/dev_console.gd's own
## command submit and ui/chat_box.gd's own open key are bound to Enter, and
## they're independent sibling _input() handlers under the same player, so
## the console consuming Enter for its own LineEdit's text_submitted signal
## never stopped chat_box's own separate _input() from ALSO treating that
## same keypress as "open chat." Fixed via an explicit sibling check in
## chat_box.gd's own _input() (see its own comment on why this can't just
## rely on Godot's _input() call order between siblings).
func _run_console_open_suppresses_chat_check() -> void:
	var console: Node = _player.get_node_or_null("DevConsole")
	if console == null:
		_fail("player.gd never instantiated its own dev console (DevConsole) -- can't verify (expected in a debug build, which this test run should be)")
		return

	console._open()
	var pressed_enter := InputEventKey.new()
	pressed_enter.physical_keycode = KEY_ENTER
	pressed_enter.pressed = true
	_player._chat_box._input(pressed_enter)
	if _player._chat_box.is_open():
		_fail("chat box opened from Enter while the dev console was open -- should have been suppressed")
	else:
		print("PASS: pressing Enter while the dev console is open does NOT also open the chat box")
	console._close()

	# Sanity check the reverse still works -- Enter opens chat normally once
	# the console is closed again, so this isn't just permanently broken.
	_player._chat_box._input(pressed_enter)
	if _player._chat_box.is_open():
		print("PASS: Enter still opens chat normally once the console is closed")
	else:
		_fail("Enter no longer opens chat at all once the console is closed -- suppression check is too broad")
	_player._chat_box._close()

## Regression check for a bug in the FIRST fix attempt at the above: having
## dev_console.gd itself call get_viewport().set_input_as_handled() for the
## "chat" action while open (to stop chat_box.gd's sibling _input() from
## seeing it) ALSO stopped Godot's own GUI dispatch from ever delivering
## that same keypress to the console's OWN focused InputField --
## text_submitted never fired, so Enter stopped submitting console commands
## at all. Reported directly: "now it doesn't let me press enter at all in
## console." set_input_as_handled()/is_input_handled() are real Viewport-
## level state -- checking it here proves exactly what matters: does OUR
## code ever mark this event handled while the console is open. It does NOT
## try to also prove Godot's own LineEdit still emits text_submitted on a
## real keypress -- that's basic, unmodified engine behavior neither fix
## touched.
##
## Uses a DEDICATED, fresh SubViewport (its own separate console+chat pair,
## not the shared _player used by every other check in this file) rather
## than reading get_viewport().is_input_handled() on the test's own shared
## root Viewport -- confirmed directly that is_input_handled() is a
## persistent flag that does NOT reset between DIRECT _input() calls made
## by hand the way a real per-frame engine dispatch cycle would reset it
## before each new input event; earlier checks in this same file (e.g.
## "pressing Enter opens the chat box") already leave it stuck true on the
## shared Viewport by the time this check would otherwise run, which made
## an earlier version of this exact check FAIL for the wrong reason (stale
## leftover state, not a real bug) even after the actual fix was correctly
## in place. A fresh SubViewport's own is_input_handled() is guaranteed
## untouched by anything else in this file.
func _run_console_still_submits_on_real_enter_check() -> void:
	var sub_viewport := SubViewport.new()
	add_child(sub_viewport)
	var fake_player := Node.new()
	sub_viewport.add_child(fake_player)
	var console := DevConsoleScene.instantiate()
	fake_player.add_child(console)
	var chat := ChatBoxScene.instantiate()
	fake_player.add_child(chat)

	console._open()
	var pressed_enter := InputEventKey.new()
	pressed_enter.keycode = KEY_ENTER
	pressed_enter.physical_keycode = KEY_ENTER
	pressed_enter.pressed = true

	console._input(pressed_enter)
	chat._input(pressed_enter)

	if sub_viewport.is_input_handled():
		_fail("dev_console.gd and/or chat_box.gd marked this Enter keypress as handled while the console was open -- that stops Godot's own GUI dispatch from ever reaching the console's focused InputField, silently breaking command submission (this is the exact bug from the first fix attempt)")
	else:
		print("PASS: neither dev_console.gd nor chat_box.gd consumes the Enter keypress meant for the console's own InputField")

	if chat.is_open():
		_fail("chat box ALSO opened from that same keypress")
	else:
		print("PASS: that same keypress did not also open the chat box")

	sub_viewport.queue_free()
