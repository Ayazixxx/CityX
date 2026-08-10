extends Node3D
## Scratch: renders the real, baked Old Guy to a PNG so his paint can be
## LOOKED AT rather than just asserted about. The regression suite proves a
## design reaches the atlas; only an image proves it's the right design.

const OldGuyScene := preload("res://npc/old_guy.tscn")

func _ready() -> void:
	call_deferred("_run")

func _run() -> void:
	var light := DirectionalLight3D.new()
	light.rotation = Vector3(deg_to_rad(-35), deg_to_rad(35), 0)
	light.light_energy = 1.4
	add_child(light)

	var env := WorldEnvironment.new()
	var e := Environment.new()
	e.background_mode = Environment.BG_COLOR
	e.background_color = Color(0.18, 0.19, 0.22)
	e.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	e.ambient_light_color = Color(0.6, 0.62, 0.68)
	e.ambient_light_energy = 1.0
	env.environment = e
	add_child(env)

	var old_guy: CharacterBody3D = OldGuyScene.instantiate()
	add_child(old_guy)
	old_guy.global_position = Vector3.ZERO

	var cam := Camera3D.new()
	add_child(cam)
	# Camera on -Z, because that is the direction the FACE points: the body's
	# own forward is -Z, and unique_npc.gd rotates the Character child by PI
	# so the model's +Z face lines up with it. Sitting on +Z (the obvious
	# guess) renders the back of his head.
	cam.look_at_from_position(Vector3(0, 1.15, -2.6), Vector3(0, 1.0, 0), Vector3.UP)
	cam.current = true

	# Several frames: the skeleton, the design and the idle clip all land
	# across different points in the first couple of ticks.
	for _i in range(12):
		await get_tree().process_frame

	var img := get_viewport().get_texture().get_image()
	img.save_png("res://old_guy_render.png")
	print("wrote res://old_guy_render.png (%dx%d)" % [img.get_width(), img.get_height()])
	get_tree().quit(0)
