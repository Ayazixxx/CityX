extends Node3D
## Visual sanity check of the real character.tscn (not a reimplementation)
## with the fused body integrated: rest pose, then mid-stride walking, each
## screenshotted. Not part of the game -- run directly or via the capture
## variant below.

const CharacterScene := preload("res://character/character.tscn")
const OUT_PATH_REST := "res://tests/character_visual_rest.png"
const OUT_PATH_WALK := "res://tests/character_visual_walk.png"

var _character: Node3D
var _frames := 0

func _ready() -> void:
	_character = CharacterScene.instantiate()
	add_child(_character)

	var cam := Camera3D.new()
	cam.position = Vector3(0, 1.1, 3.2)
	cam.rotation_degrees = Vector3(-5, 0, 0)
	cam.current = true
	add_child(cam)

	var light := DirectionalLight3D.new()
	light.rotation_degrees = Vector3(-45, -30, 0)
	add_child(light)

	var env_node := WorldEnvironment.new()
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color(0.12, 0.13, 0.16)
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color.WHITE
	env.ambient_light_energy = 0.7
	env_node.environment = env
	add_child(env_node)

func _process(_delta: float) -> void:
	_frames += 1
	if _frames == 10:
		var img := get_viewport().get_texture().get_image()
		img.save_png(OUT_PATH_REST)
		print("Saved rest-pose screenshot to ", OUT_PATH_REST)
		_character.set_walking(true)
		_character.anim_player.seek(0.2, true)
	elif _frames == 13:
		var img := get_viewport().get_texture().get_image()
		img.save_png(OUT_PATH_WALK)
		print("Saved walk screenshot to ", OUT_PATH_WALK)
	elif _frames > 15:
		get_tree().quit()
