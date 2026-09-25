extends Node3D
## One-off export of the Godot original's own body mesh AND head to a plain
## .obj, for Blender reference -- the Godot-side equivalent of the Unity
## port's own CityX/Export Player Body Reference (OBJ) menu item
## (PlayerBodyReferenceExporter.cs), same ask: "give me the file for the
## standard playermodel so i can shape the outfit to him in blender," this
## time "the model from the godot version" specifically -- then extended
## the same session once the first pass (body only) turned out to be
## missing the head, which matters for fitting a garment's collar/neckline.
##
## BODY: no need to run FusedBody.build_uncached() (11-13 real seconds, see
## fused_body_cache.gd's own header) -- the baked mesh already sits on disk
## at character/fused_body_cache.res, loaded here directly like any other
## Resource. This mesh has NO index buffer (see fused_body.gd's own
## build_uncached comment: "raw (non-indexed) triangle soup"), so every
## consecutive triple of ARRAY_VERTEX is already one triangle.
##
## HEAD: character.gd never bakes this to disk (it is swappable at runtime
## -- cube/sphere/pyramid, see _generate_head_verts), so there is no cache
## file to load. Replicated directly from character.gd's own
## _generate_sphere_verts/_sphere_point (the default "sphere" head shape,
## _head_shape's own default) and _define_parts' head size/pivot/offset,
## rather than instantiating a full Character scene (which would drag in
## input/camera/networking init this headless export has no use for, and
## no guarantee runs cleanly with no server/world present). Position: the
## head bone's own rest origin is FusedBody.bone_origins()["head"] ==
## Vector3(0, FusedBody.NECK_Y, 0) -- the SAME shared body-local space the
## cached body mesh already lives in -- plus the sphere's own vertical
## offset (half its own size, i.e. its radius), exactly matching
## _build_head()'s "rest_center = pivot + offset" and its mesh vertices'
## own "offset + v.pos" (the "offset" part is baked into the mesh, the
## "pivot" part normally comes from the head BONE's transform via a
## BoneAttachment3D -- reproduced here directly instead since there is no
## live skeleton in this script).
##
## Not part of the game; run directly via
## `Godot_v4.7.1-stable_win64.exe --headless --path <project> res://tests/scratch_export_body_obj.tscn`
## -- NOT the _console.exe variant, which fails to spawn its own child
## process in this environment (CreateProcess error 193, confirmed).

const FusedBody = preload("res://character/fused_body.gd")

const CachePath := "res://character/fused_body_cache.res"
# Absolute, not res://../ -- keeps this independent of how Godot resolves
# a res:// path that climbs outside the project root.
const OutputPath := "C:/Users/Lenovo/Downloads/the game/CityX/Logs/GodotPlayerBodyReference.obj"

# EXPLICIT ASK: "can you just make the godot one in unity why is there any
# difference at all?" -- the two engines run textually separate marching-
# tetrahedra implementations (fused_body.gd's own extract_mesh vs
# FusedBodyMesh.Extract), fed the SAME joint/radius numbers (confirmed by
# direct comparison) but a differently-computed bounding box for the
# sampling grid, which is enough to shift the grid's own phase relative to
# the true SDF surface and change exactly where each vertex lands along
# it. Rather than chase that discretization difference number by number,
# this dumps the RAW, UNCONVERTED Godot-native vertex/normal data (no
# X-flip/winding-swap -- that's an OBJ-specific convention, not something
# Unity's own importer needs) so a Unity-side tool can build a mesh from
# Godot's OWN exact numbers directly, guaranteeing bit-identical geometry
# instead of two independent extractions that only approximately agree.
const RawOutputPath := "C:/Users/Lenovo/Downloads/the game/CityX/Logs/GodotPlayerBodyRaw.txt"

# character.gd's own _define_parts()/_generate_sphere_verts constants for
# the default "sphere" head shape -- copied, not derived, since they live
# on Character instance state (_parts/_head_shape) this standalone script
# never instantiates.
const HeadSize := 0.7
const HeadRadius := HeadSize / 2.0
const HeadLatSegments := 8
const HeadLonSegments := 12

func _ready() -> void:
	var cache: Resource = load(CachePath)
	if cache == null or cache.mesh == null:
		print("FAIL: no cached body mesh at ", CachePath)
		get_tree().quit(1)
		return

	var mesh: ArrayMesh = cache.mesh
	var arrays: Array = mesh.surface_get_arrays(0)
	var positions: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var normals: PackedVector3Array = arrays[Mesh.ARRAY_NORMAL]
	var uvs: PackedVector2Array = arrays[Mesh.ARRAY_TEX_UV]
	var body_vertex_count := positions.size()

	_append_head(positions, normals, uvs)

	var lines := PackedStringArray()
	lines.append("# Godot fused_body + head reference -- arms-down rest pose")
	lines.append("# body: fused_body_cache.res, head: replicated sphere shape from character.gd")

	# X-flip for the same Godot(right-handed-ish, -Z forward)->OBJ handedness
	# reason the Unity exporter flips X -- keeps this reference consistent
	# with PlayerBodyReferenceExporter.cs's own output convention.
	for v in positions:
		lines.append("v %.6f %.6f %.6f" % [-v.x, v.y, v.z])
	var has_normals := normals.size() == positions.size()
	if has_normals:
		for n in normals:
			lines.append("vn %.6f %.6f %.6f" % [-n.x, n.y, n.z])
	var has_uvs := uvs.size() == positions.size()
	if has_uvs:
		for uv in uvs:
			lines.append("vt %.6f %.6f" % [uv.x, uv.y])

	# Body: raw triangle soup, no index buffer -- consecutive triples are
	# faces. The X-flip above mirrors winding too, so corners b/c are
	# swapped to keep faces pointing the right way (same fix the Unity
	# exporter uses).
	var i := 0
	while i + 2 < body_vertex_count:
		var a := i + 1
		var b := i + 2
		var c := i + 3
		lines.append("f %s %s %s" % [_corner(a, has_uvs, has_normals), _corner(c, has_uvs, has_normals), _corner(b, has_uvs, has_normals)])
		i += 3

	# Head: also raw triangle soup (appended the same way, see _append_head).
	while i + 2 < positions.size():
		var a := i + 1
		var b := i + 2
		var c := i + 3
		lines.append("f %s %s %s" % [_corner(a, has_uvs, has_normals), _corner(c, has_uvs, has_normals), _corner(b, has_uvs, has_normals)])
		i += 3

	DirAccess.make_dir_recursive_absolute(OutputPath.get_base_dir())
	var f := FileAccess.open(OutputPath, FileAccess.WRITE)
	if f == null:
		print("FAIL: could not open ", OutputPath, " for writing, error ", FileAccess.get_open_error())
		get_tree().quit(1)
		return
	f.store_string("\n".join(lines) + "\n")
	f.close()

	_write_raw_dump(positions, normals, body_vertex_count)

	print("wrote Godot player body+head reference to ", OutputPath, " (",
		positions.size(), " vertices: ", body_vertex_count, " body + ",
		positions.size() - body_vertex_count, " head, arms-down rest pose).")
	get_tree().quit(0)

## Godot's own exact numbers, completely unconverted -- see RawOutputPath's
## own doc comment for why. A Unity-side importer applies whatever
## conversion it needs and can verify the result by rendering it, rather
## than trusting a conversion baked in here sight-unseen.
func _write_raw_dump(positions: PackedVector3Array, normals: PackedVector3Array, body_vertex_count: int) -> void:
	var raw := PackedStringArray()
	raw.append(str(body_vertex_count))
	raw.append(str(positions.size() - body_vertex_count))
	for v in positions:
		raw.append("%.8f %.8f %.8f" % [v.x, v.y, v.z])
	for n in normals:
		raw.append("%.8f %.8f %.8f" % [n.x, n.y, n.z])

	var f := FileAccess.open(RawOutputPath, FileAccess.WRITE)
	if f == null:
		print("FAIL: could not open ", RawOutputPath, " for writing, error ", FileAccess.get_open_error())
		return
	f.store_string("\n".join(raw) + "\n")
	f.close()

## Appends the head sphere's vertices/normals/uvs (packed arrays, in place)
## at the same rest-pose position character.gd's own _build_head() puts it:
## the head bone's rest origin (FusedBody.bone_origins()["head"]) plus the
## sphere's own radius upward. Mirrors _generate_sphere_verts/_sphere_point
## exactly, including its per-triangle winding fix.
func _append_head(positions: PackedVector3Array, normals: PackedVector3Array, uvs: PackedVector2Array) -> void:
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
					positions.append(center + v["pos"])
					normals.append(v["normal"])
					uvs.append(v["uv"])

func _sphere_point(lat_i: int, lon_i: int) -> Dictionary:
	var theta := float(lat_i) / float(HeadLatSegments) * PI
	var phi := float(lon_i) / float(HeadLonSegments) * TAU
	var sin_t := sin(theta)
	var pos := Vector3(HeadRadius * sin_t * cos(phi), HeadRadius * cos(theta), HeadRadius * sin_t * sin(phi))
	var normal := pos.normalized() if pos.length_squared() > 0.0001 else Vector3.UP
	# No real atlas row exists here (this script never builds one) -- a
	# plain 0..1 equirectangular UV is enough for a geometry-only reference
	# with no texture applied.
	var uv := Vector2(float(lon_i) / float(HeadLonSegments), float(lat_i) / float(HeadLatSegments))
	return {"pos": pos, "normal": normal, "uv": uv}

func _corner(index: int, has_uvs: bool, has_normals: bool) -> String:
	if has_uvs and has_normals:
		return "%d/%d/%d" % [index, index, index]
	if has_uvs:
		return "%d/%d" % [index, index]
	if has_normals:
		return "%d//%d" % [index, index]
	return str(index)
