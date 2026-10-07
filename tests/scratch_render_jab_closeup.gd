extends Node3D
## The real Godot character throwing a jab, close up from the front-right, a frame every 0.04 s (CityX/Logs/CityShots/godot-jab-closeup.png).

const OutputPath := "C:/Users/Lenovo/Downloads/the game/CityX/Logs/CityShots/godot-jab-closeup.png"
const Cell := 300
const Frames := 10

func _ready() -> void:
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-40, 30, 0)
	sun.light_energy = 1.2
	add_child(sun)
	var env := WorldEnvironment.new()
	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color(0.45, 0.55, 0.70)
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color(0.55, 0.55, 0.58)
	env.environment = environment
	add_child(env)
	var character: Node3D = load("res://character/character.tscn").instantiate()
	add_child(character)
	var camera := Camera3D.new()
	camera.fov = 26.0
	add_child(camera)
	camera.current = true
	camera.global_position = Vector3(2.6, 1.9, 3.0)
	camera.look_at(Vector3(0, 1.6, 0.3), Vector3.UP)
	await get_tree().create_timer(0.6).timeout
	character.set_walking(false)
	character.enter_combat_mode()
	await get_tree().create_timer(0.4).timeout
	var sheet := Image.create(Cell * Frames, Cell, false, Image.FORMAT_RGB8)
	character.punch(false)
	for i in range(Frames):
		await get_tree().create_timer(0.045).timeout
		await get_tree().process_frame
		var image := get_viewport().get_texture().get_image()
		image.convert(Image.FORMAT_RGB8)
		var side := mini(image.get_width(), image.get_height())
		var crop := image.get_region(Rect2i((image.get_width() - side) / 2, (image.get_height() - side) / 2, side, side))
		crop.resize(Cell, Cell, Image.INTERPOLATE_BILINEAR)
		sheet.blit_rect(crop, Rect2i(0, 0, Cell, Cell), Vector2i(i * Cell, 0))
	sheet.save_png(OutputPath)
	print("wrote ", OutputPath)
	get_tree().quit(0)
