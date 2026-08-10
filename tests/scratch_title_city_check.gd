extends Node
## One-off sanity check: instantiate title_screen_city.tscn headlessly and
## confirm _ready() builds the skyline/logo/camera without errors. Not part
## of the game.

const TitleScreenCityScene := preload("res://title_screen/title_screen_city.tscn")

func _ready() -> void:
	var city := TitleScreenCityScene.instantiate()
	add_child(city)
	await get_tree().process_frame
	await get_tree().process_frame

	var skyline := city.get_node_or_null("Skyline")
	print("Skyline child count: ", skyline.get_child_count() if skyline != null else -1)
	print("Skyline position.y (expect -18): ", skyline.position.y if skyline != null else null)

	var ground := city.get_node_or_null("Ground")
	print("Ground found: ", ground != null, " position.y (expect -18): ", ground.position.y if ground != null else null)

	var logo := city.get_node_or_null("Logo3D")
	print("Logo3D child count (expect 1: textured quad): ", logo.get_child_count() if logo != null else -1)
	print("Logo3D position (expect 0,27,-12): ", logo.position if logo != null else null)

	var cam := city.get_node_or_null("Camera3D")
	print("Camera3D found: ", cam != null, " current=", cam.current if cam != null else false)

	var world_env := city.get_node_or_null("WorldEnvironment")
	print("WorldEnvironment found: ", world_env != null)

	var ok := skyline != null and ground != null and logo != null and logo.get_child_count() == 1 and cam != null and world_env != null
	print("OVERALL: PASS" if ok else "OVERALL: FAIL")
	get_tree().quit()
