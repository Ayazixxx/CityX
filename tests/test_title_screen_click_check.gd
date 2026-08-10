extends Node
## Checks that the title screen's "press any key" idle state can ALSO be
## dismissed with a plain mouse click, which is what its own prompt label
## now advertises.
##
## The click path lives in _unhandled_input, but a Control only ever sees
## unhandled input for a mouse event that no Control consumed first --
## and mouse_filter defaults to STOP. Every child layer already opted out
## with MOUSE_FILTER_IGNORE; the root TitleScreen itself did not, so it
## silently ate every click while keyboard (which doesn't go through
## mouse filtering at all) kept working. This pushes a REAL
## InputEventMouseButton through the viewport rather than calling
## _unhandled_input directly, so the filter chain is actually exercised
## -- calling the handler by hand would have passed even while broken.

const TitleScreenScene := preload("res://title_screen/title_screen.tscn")

var _ok := true

func _fail(msg: String) -> void:
	_ok = false
	print("FAIL: ", msg)

func _ready() -> void:
	await _run()
	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

func _run() -> void:
	var ts: Control = TitleScreenScene.instantiate()
	add_child(ts)
	await get_tree().process_frame
	await get_tree().process_frame

	_check_prompt_text(ts)
	_check_mouse_filters(ts)
	await _check_click_reveals(ts)
	await _check_second_click_is_ignored(ts)

	ts.queue_free()

## The prompt deliberately only mentions the key, even though clicking works
## too -- a reported ask ("remove the 'or click' from the title screen text
## ... not the way clicking works too, just the text"). So this checks the
## wording is still the any-key prompt and does NOT assert anything about
## the word "click"; the click PATH is covered behaviorally below, which is
## what actually matters.
func _check_prompt_text(ts: Control) -> void:
	var prompt: Label = ts.get_node("PromptLabel")
	if not prompt.text.to_upper().contains("KEY"):
		_fail("PromptLabel should advertise any-key, got: " + prompt.text)
	elif _ok:
		print("PASS: prompt reads \"", prompt.text, "\"")

## Any full-screen Control stacked over the idle screen has to be
## transparent to the mouse, root included -- one STOP anywhere in the
## stack is enough to swallow the click before _unhandled_input runs.
func _check_mouse_filters(ts: Control) -> void:
	if ts.mouse_filter != Control.MOUSE_FILTER_IGNORE:
		_fail("root TitleScreen mouse_filter should be IGNORE, got " + str(ts.mouse_filter))
	var overlay_names := [
		"City3DViewport", "Background", "RainLayer", "Vignette",
		"Logo", "EyeLayer", "PromptLabel", "FlashOverlay",
	]
	for overlay_name in overlay_names:
		var overlay: Control = ts.get_node(overlay_name)
		if overlay.mouse_filter != Control.MOUSE_FILTER_IGNORE:
			_fail(overlay_name + " mouse_filter should be IGNORE, got " + str(overlay.mouse_filter))
	if _ok:
		print("PASS: root + all ", overlay_names.size(), " idle-screen layers are mouse-transparent")

func _check_click_reveals(ts: Control) -> void:
	var prompt: Label = ts.get_node("PromptLabel")
	var menu: Control = ts.get_node("MenuPanel")
	if menu.visible:
		_fail("MenuPanel should start hidden")
	if not prompt.visible:
		_fail("PromptLabel should start visible")

	# Aim at the control's own real rect -- the headless root viewport is
	# only 64x64, so a hard-coded screen-centre coordinate would land
	# outside it and fall through to _unhandled_input no matter what the
	# mouse filters say, quietly passing this test even while broken.
	await _click_at(ts.get_global_rect().get_center())

	if not menu.visible:
		_fail("a left click should have revealed MenuPanel")
	if prompt.visible:
		_fail("PromptLabel should be hidden once the menu is revealed")
	if not bool(ts.get("_menu_revealed")):
		_fail("_menu_revealed should be true after a click")
	if _ok:
		print("PASS: left click reveals the menu")

## Once the menu is up, clicks are the menu's own business -- a stray
## click on the backdrop must not re-run the reveal or fight the buttons.
func _check_second_click_is_ignored(ts: Control) -> void:
	var menu: Control = ts.get_node("MenuPanel")
	await _click_at(ts.get_global_rect().get_center())
	if not menu.visible:
		_fail("a second click should have left the menu revealed")
	elif _ok:
		print("PASS: clicking again is a no-op while the menu is up")

func _click_at(position: Vector2) -> void:
	var press := InputEventMouseButton.new()
	press.button_index = MOUSE_BUTTON_LEFT
	press.pressed = true
	press.position = position
	press.global_position = position
	get_viewport().push_input(press)
	await get_tree().process_frame

	var release := InputEventMouseButton.new()
	release.button_index = MOUSE_BUTTON_LEFT
	release.pressed = false
	release.position = position
	release.global_position = position
	get_viewport().push_input(release)
	await get_tree().process_frame
