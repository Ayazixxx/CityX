extends Node3D
## Renders the REAL Godot character (its own scene, its own baked animations) in several poses from several angles, as one contact sheet, to compare with
## the Unity port's body (CityX/Logs/CityShots/godot-character-sheet.png). Needs a real window (do NOT run with --headless) and the non-console Godot exe.

const OutputPath := "C:/Users/Lenovo/Downloads/the game/CityX/Logs/CityShots/godot-character-sheet.png"
const Cell := 300
const Poses := ["idle", "walk", "run", "guard", "jab"]
const Views := ["front", "side", "three-quarter"]

var _character: Node3D
var _camera: Camera3D

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
	environment.ambient_light_energy = 1.0
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

	_character = load("res://character/character.tscn").instantiate()
	add_child(_character)
	_camera = Camera3D.new()
	_camera.fov = 30.0
	add_child(_camera)
	_camera.current = true

	var sheet := Image.create(Cell * Views.size(), Cell * Poses.size(), false, Image.FORMAT_RGB8)
	await get_tree().create_timer(0.6).timeout
	for row in range(Poses.size()):
		_pose(Poses[row])
		await get_tree().create_timer(0.55 if Poses[row] != "jab" else 0.12).timeout
		for col in range(Views.size()):
			_aim(Views[col])
			await get_tree().process_frame
			await get_tree().process_frame
			var image := get_viewport().get_texture().get_image()
			image.convert(Image.FORMAT_RGB8)
			var side := mini(image.get_width(), image.get_height())
			var crop := image.get_region(Rect2i((image.get_width() - side) / 2, (image.get_height() - side) / 2, side, side))
			crop.resize(Cell, Cell, Image.INTERPOLATE_BILINEAR)
			sheet.blit_rect(crop, Rect2i(0, 0, Cell, Cell), Vector2i(col * Cell, row * Cell))
	DirAccess.make_dir_recursive_absolute(OutputPath.get_base_dir())
	sheet.save_png(OutputPath)
	print("wrote ", OutputPath)
	get_tree().quit(0)

func _pose(name: String) -> void:
	match name:
		"idle":
			_character.set_walking(false)
		"walk":
			_character.set_walking(true, false)
		"run":
			_character.set_walking(true, true)
		"guard":
			_character.set_walking(false)
			_character.enter_combat_mode()
		"jab":
			_character.set_walking(false)
			_character.punch(false)

func _aim(view: String) -> void:
	var target := Vector3(0, 1.1, 0)
	match view:
		"front":
			_camera.global_position = Vector3(0, 1.4, 7.0)
		"side":
			_camera.global_position = Vector3(7.0, 1.4, 0)
		_:
			_camera.global_position = Vector3(4.6, 1.8, 5.2)
	_camera.look_at(target, Vector3.UP)
