extends Node3D
## Renders the SAME body+head mesh scratch_export_body_obj.gd exports,
## using GODOT'S OWN rendering with NO coordinate conversion at all --
## checking whether the visible gap found when that same data was
## reimported into Unity (GodotBodyImporter.cs) is a real defect already
## present in Godot's own source data/winding, or something introduced by
## the X-flip/winding-swap conversion on the way into Unity. Needs a real
## window/graphics device -- do NOT run with --headless (matches Unity's
## own "needs a real graphics device" requirement for CharacterCapture).

const FusedBody = preload("res://character/fused_body.gd")
const CachePath := "res://character/fused_body_cache.res"
const OutputPath := "C:/Users/Lenovo/Downloads/the game/CityX/Logs/CityShots/godot-native-seam-check.png"

const HeadSize := 0.7
const HeadRadius := HeadSize / 2.0
const HeadLatSegments := 8
const HeadLonSegments := 12

func _ready() -> void:
	var cache: Resource = load(CachePath)
	var mesh: ArrayMesh = cache.mesh
	var arrays: Array = mesh.surface_get_arrays(0)
	var positions: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var normals: PackedVector3Array = arrays[Mesh.ARRAY_NORMAL]

	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	for i in range(positions.size()):
		st.set_normal(normals[i])
		st.add_vertex(positions[i])
	_append_head(st)
	var combined_mesh := st.commit()

	var mesh_instance := MeshInstance3D.new()
	mesh_instance.mesh = combined_mesh
	var material := StandardMaterial3D.new()
	material.albedo_color = Color(0.85, 0.55, 0.35)
	mesh_instance.material_override = material
	add_child(mesh_instance)

	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-40, -150, 0)
	sun.light_energy = 1.2
	add_child(sun)

	var env := WorldEnvironment.new()
	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color(0.10, 0.11, 0.15)
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color(0.3, 0.3, 0.32)
	environment.ambient_light_energy = 1.0
	env.environment = environment
	add_child(env)

	var camera := Camera3D.new()
	camera.fov = 32.0
	add_child(camera)
	camera.current = true
	camera.global_transform.origin = Vector3(0.6, 1.2, 4.5)
	camera.look_at(Vector3(0.0, 1.1, 0.0), Vector3.UP)

	# Two frames so the render pipeline actually produces a real frame
	# before we grab it.
	await get_tree().process_frame
	await get_tree().process_frame

	var image := get_viewport().get_texture().get_image()
	DirAccess.make_dir_recursive_absolute(OutputPath.get_base_dir())
	image.save_png(OutputPath)
	print("wrote ", OutputPath)
	get_tree().quit(0)

func _append_head(st: SurfaceTool) -> void:
	var center := Vector3(0, FusedBody.NECK_Y, 0) + Vector3(0, HeadRadius, 0)
	for lat_i in range(HeadLatSegments):
		for lon_i in range(HeadLonSegments):
			var p00 := _sphere_point(lat_i, lon_i)
			var p01 := _sphere_point(lat_i, lon_i + 1)
			var p10 := _sphere_point(lat_i + 1, lon_i)
			var p11 := _sphere_point(lat_i + 1, lon_i + 1)
			var quad := [p00, p01, p11, p10]
			for tri in [[0, 1, 2], [0, 2, 3]]:
				var tv := [quad[tri[0]], quad[tri[1]], quad[tri[2]]]
				var geo_normal: Vector3 = (tv[1]["pos"] - tv[0]["pos"]).cross(tv[2]["pos"] - tv[0]["pos"])
				if geo_normal.length_squared() > 0.0000001:
					var desired_normal: Vector3 = ((tv[0]["normal"] + tv[1]["normal"] + tv[2]["normal"]) / 3.0).normalized()
					if geo_normal.normalized().dot(desired_normal) > 0.0:
						tv.reverse()
				for v in tv:
					st.set_normal(v["normal"])
					st.add_vertex(center + v["pos"])

func _sphere_point(lat_i: int, lon_i: int) -> Dictionary:
	var theta := float(lat_i) / float(HeadLatSegments) * PI
	var phi := float(lon_i) / float(HeadLonSegments) * TAU
	var sin_t := sin(theta)
	var pos := Vector3(HeadRadius * sin_t * cos(phi), HeadRadius * cos(theta), HeadRadius * sin_t * sin(phi))
	var normal := pos.normalized() if pos.length_squared() > 0.0001 else Vector3.UP
	return {"pos": pos, "normal": normal}
