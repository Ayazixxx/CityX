extends Node
## One-off sanity check: instantiate the REAL title_screen.tscn headlessly
## and confirm the new City3DViewport wiring (SubViewport sizing, logo
## flicker/visibility redirect) works without errors. Not part of the game.

const TitleScreenScene := preload("res://title_screen/title_screen.tscn")

func _ready() -> void:
	var ts := TitleScreenScene.instantiate()
	add_child(ts)
	ts.size = Vector2(1280, 720)
	await get_tree().process_frame
	await get_tree().process_frame

	var viewport: SubViewport = ts.get_node("City3DViewport/SubViewport")
	print("SubViewport size after layout: ", viewport.size)

	var backdrop := ts.get_node("City3DViewport/SubViewport/TitleScreenCity")
	print("Backdrop found: ", backdrop != null)

	var background: TextureRect = ts.get_node("Background")
	var logo: TextureRect = ts.get_node("Logo")
	print("Background.visible (should be false): ", background.visible)
	print("Logo.visible (should be false): ", logo.visible)

	# Drive a couple frames of _process to exercise the flicker redirect.
	await get_tree().process_frame
	await get_tree().process_frame

	ts.hide_foreground_ui()
	await get_tree().process_frame
	print("hide_foreground_ui() ran without error")

	ts.reset_to_initial()
	await get_tree().process_frame
	print("reset_to_initial() ran without error")

	var ok: bool = viewport.size.x == 1280 and viewport.size.y == 720 and backdrop != null and not background.visible and not logo.visible
	print("OVERALL: PASS" if ok else "OVERALL: FAIL")
	get_tree().quit()
