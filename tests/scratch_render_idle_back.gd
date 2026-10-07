extends Node3D
## One Godot clip, sampled at even times across its length, side-on, as a strip (CityX/Logs/CityShots/godot-idle-back.png). The clip is the CLIP_NAME constant.

const OutputPath := "C:/Users/Lenovo/Downloads/the game/CityX/Logs/CityShots/godot-idle-back.png"
const ClipName := "idle"
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
	var floor_mesh := MeshInstance3D.new()
	var plane := PlaneMesh.new()
	plane.size = Vector2(40, 40)
	floor_mesh.mesh = plane
	var floor_material := StandardMaterial3D.new()
	floor_material.albedo_color = Color(0.42, 0.40, 0.38)
	floor_mesh.material_override = floor_material
	add_child(floor_mesh)
	var character: Node3D = load("res://character/character.tscn").instantiate()
	add_child(character)
	var camera := Camera3D.new()
	camera.fov = 30.0
	add_child(camera)
	camera.current = true
	camera.global_position = Vector3(2.2, 1.9, -3.4)
	camera.fov = 30.0
	camera.look_at(Vector3(0, 1.3, 0), Vector3.UP)
	await get_tree().create_timer(0.5).timeout
	var player: AnimationPlayer = character.anim_player
	var length := player.get_animation(ClipName).length
	var sheet := Image.create(Cell * Frames, Cell, false, Image.FORMAT_RGB8)
	for i in range(Frames):
		player.play(ClipName)
		player.seek(length * float(i) / float(Frames), true)
		player.pause()
		await get_tree().process_frame
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
