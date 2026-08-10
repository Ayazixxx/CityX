extends Node3D
## Regression check for the 4 new special building types (supermarket, bank,
## police, hospital -- see city_chunk.gd's own SPECIAL_BUILDING_PRESETS):
## the independent type-picker rolls at roughly its own configured chances,
## a special lot builds via the SAME _build_building() every generic
## building uses (with the preset's own size range/roof/facade color) plus
## a sign, and -- the critical regression
## guard given this session's own prior building-visual incidents -- a lot
## that does NOT roll special produces EXACTLY the same building it would
## have before this feature existed, because the special-type roll is drawn
## from its own independent RNG stream, never the shared per-chunk `rng`
## every other per-building choice already depends on in a fixed call order.

const CityChunk = preload("res://city/city_chunk.gd")

var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	call_deferred("_run_checks")

func _run_checks() -> void:
	var materials := CityChunk.build_materials()

	_check_picker_distribution()
	_check_special_building_geometry()
	_check_hospital_sign()
	_check_generic_lot_unaffected(materials)
	_check_full_generate_chunk_for_special_lot(materials)

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

## --- 1. _pick_special_building_type rolls each type at roughly its own
## configured "chance", and "none" (generic) fills the remainder. ---
func _check_picker_distribution() -> void:
	var trials := 20000
	var counts := {}
	for type_name in CityChunk.SPECIAL_BUILDING_PRESETS:
		counts[type_name] = 0
	counts[""] = 0

	for i in range(trials):
		var rng := RandomNumberGenerator.new()
		rng.seed = i * 92821 + 5
		var picked := CityChunk._pick_special_building_type(rng)
		counts[picked] += 1

	var dist_ok := true
	for type_name in CityChunk.SPECIAL_BUILDING_PRESETS:
		var expected: float = CityChunk.SPECIAL_BUILDING_PRESETS[type_name]["chance"]
		var actual := float(counts[type_name]) / float(trials)
		if absf(actual - expected) > 0.01:
			dist_ok = false
			_fail("special type '%s' picked at rate %s, expected ~%s" % [type_name, actual, expected])
	if dist_ok:
		print("PASS: each special building type rolls at roughly its own configured chance (", trials, " trials)")

## --- 2. Each special type builds via the real _build_building() (preset's
## own size range respected, facade material actually carries the preset's
## own color) and gets a correctly-colored sign panel. Hospital is checked
## separately below -- same plain sign now, just its own dedicated check
## guarding that its old red cross stays gone. ---
func _check_special_building_geometry() -> void:
	for type_name in CityChunk.SPECIAL_BUILDING_PRESETS:
		if type_name == "hospital":
			continue
		var preset: Dictionary = CityChunk.SPECIAL_BUILDING_PRESETS[type_name]
		var rng := RandomNumberGenerator.new()
		rng.seed = 4242
		var width: float = clampf(rng.randf_range(float(preset["width_min"]), float(preset["width_max"])), CityChunk.MIN_FOOTPRINT, CityChunk.MAX_FOOTPRINT)
		var depth: float = clampf(rng.randf_range(float(preset["depth_min"]), float(preset["depth_max"])), CityChunk.MIN_FOOTPRINT, CityChunk.MAX_FOOTPRINT)
		var height: float = rng.randf_range(float(preset["height_min"]), float(preset["height_max"]))
		var size := Vector3(width, height, depth)
		var facade_color: Color = preset["facade_color"]
		var sign_color: Color = preset["sign_color"]
		var roof: String = preset["roof"]
		var facade_mat: StandardMaterial3D = CityChunk.build_facade_material(facade_color)

		var root := Node3D.new()
		add_child(root)
		CityChunk._build_building(root, size, Vector3.ZERO, roof, CityChunk.build_materials(), facade_mat, facade_color)
		CityChunk._add_building_sign(root, size, Vector3.ZERO, sign_color)

		# root's own children are [0]=exterior body (added first inside
		# _build_building), [1]=a SEPARATE interior StaticBody3D
		# (_build_interior adds its own top-level body directly to `parent`,
		# not nested under the exterior one) -- no ground node here since
		# this test calls _build_building directly, not through
		# generate_chunk (which would add ground as child 0 first).
		var body: StaticBody3D = root.get_child(0)
		var mesh_inst: MeshInstance3D = body.get_child(0)
		var applied_mat: StandardMaterial3D = mesh_inst.material_override
		if applied_mat == null or not applied_mat.albedo_color.is_equal_approx(facade_color):
			_fail("%s: facade material color doesn't match its own preset (got %s)" % [type_name, applied_mat.albedo_color if applied_mat != null else "null"])
		else:
			print("PASS: %s builds with its own preset facade color %s" % [type_name, facade_color])

		var sign_node: MeshInstance3D = null
		for child in root.get_children():
			if child is MeshInstance3D:
				sign_node = child
		if sign_node == null:
			_fail("%s: no sign mesh was added" % type_name)
		else:
			var sign_mat: StandardMaterial3D = sign_node.material_override
			if sign_mat != null and sign_mat.albedo_color.is_equal_approx(sign_color):
				print("PASS: %s gets a sign panel in its own preset color %s" % [type_name, sign_color])
			else:
				_fail("%s: sign panel color doesn't match its own preset" % type_name)

		root.queue_free()

## --- 3. Hospital gets the SAME plain sign every other special building
## gets. It used to be special-cased into a red plus-shaped cross mesh;
## that was removed on request, and this now guards the removal -- a
## re-introduced cross would show up here as an extra mesh under root.
func _check_hospital_sign() -> void:
	var preset: Dictionary = CityChunk.SPECIAL_BUILDING_PRESETS["hospital"]
	var size := Vector3(16, 30, 14)

	var root := Node3D.new()
	add_child(root)
	var facade_mat := CityChunk.build_facade_material(preset["facade_color"])
	CityChunk._build_building(root, size, Vector3.ZERO, preset["roof"], CityChunk.build_materials(), facade_mat, preset["facade_color"])
	CityChunk._add_building_sign(root, size, Vector3.ZERO, preset["sign_color"])

	var sign_nodes: Array = []
	for child in root.get_children():
		# The exterior/interior bodies' own meshes are nested under their
		# own StaticBody3D, not direct children of root -- so any
		# MeshInstance3D found directly under root here is signage.
		if child is MeshInstance3D:
			sign_nodes.append(child)
	if sign_nodes.size() != 1:
		_fail("hospital: expected exactly 1 signage mesh, found %d (a red cross coming back would add a second)" % sign_nodes.size())
	else:
		var sign_mat: StandardMaterial3D = sign_nodes[0].material_override
		if sign_mat != null and sign_mat.albedo_color.is_equal_approx(preset["sign_color"]):
			print("PASS: hospital gets the ordinary plain sign in its own sign_color (", sign_mat.albedo_color, "), no cross")
		else:
			_fail("hospital: sign color doesn't match the preset's own sign_color")

	root.queue_free()

## --- 4. THE critical regression guard: a lot whose independent special-
## type roll comes back "" (generic) must produce a building byte-for-byte
## identical to what the ORIGINAL (pre-this-feature) rng call sequence
## would have produced -- proving the new special_rng draw never perturbed
## the shared `rng` stream every generic building's own width/depth/height/
## roof/facade already depended on. ---
func _check_generic_lot_unaffected(materials: Dictionary) -> void:
	var chunk_size := 24.0
	var coord := Vector2i(5, -3)
	var found_seed := -1

	for candidate in range(200000):
		var probe_rng := RandomNumberGenerator.new()
		probe_rng.seed = candidate
		if probe_rng.randf() >= CityChunk.LOT_BUILDING_CHANCE:
			continue
		var probe_special_rng := RandomNumberGenerator.new()
		probe_special_rng.seed = candidate ^ 0x53504543
		if CityChunk._pick_special_building_type(probe_special_rng) != "":
			continue
		found_seed = candidate
		break

	if found_seed == -1:
		_fail("couldn't find a generic (non-special) lot seed to test against -- test setup issue")
		return

	# Replay the EXACT original (pre-feature) rng sequence by hand: the
	# first call is the same LOT_BUILDING_CHANCE check generate_chunk
	# itself still makes first, then width/depth/height/roof/facade_index
	# in that same original order, all off ONE fresh rng seeded identically
	# to generate_chunk's own internal `rng`.
	var replay_rng := RandomNumberGenerator.new()
	replay_rng.seed = found_seed
	replay_rng.randf() # LOT_BUILDING_CHANCE roll
	var max_footprint := minf(CityChunk.MAX_FOOTPRINT, chunk_size - CityChunk.LOT_MARGIN * 2.0)
	var expected_width := replay_rng.randf_range(CityChunk.MIN_FOOTPRINT, max_footprint)
	var expected_depth := replay_rng.randf_range(CityChunk.MIN_FOOTPRINT, max_footprint)
	var expected_height := CityChunk.MIN_HEIGHT + pow(replay_rng.randf(), 2.0) * (CityChunk.MAX_HEIGHT - CityChunk.MIN_HEIGHT)
	var expected_roof: String = CityChunk.ROOF_TYPES[replay_rng.randi() % CityChunk.ROOF_TYPES.size()]
	var expected_facade_index := CityChunk._pick_facade_variant_index(replay_rng)
	var expected_color: Color = CityChunk.FACADE_VARIANTS[expected_facade_index]["body"]
	var expected_slack_x: float = maxf(0.0, (chunk_size / 2.0 - CityChunk.LOT_MARGIN) - expected_width / 2.0)
	var expected_slack_z: float = maxf(0.0, (chunk_size / 2.0 - CityChunk.LOT_MARGIN) - expected_depth / 2.0)
	var expected_jitter_x := replay_rng.randf_range(-minf(CityChunk.MAX_JITTER, expected_slack_x), minf(CityChunk.MAX_JITTER, expected_slack_x))
	var expected_jitter_z := replay_rng.randf_range(-minf(CityChunk.MAX_JITTER, expected_slack_z), minf(CityChunk.MAX_JITTER, expected_slack_z))

	var chunk := CityChunk.generate_chunk(found_seed, coord, chunk_size, materials)
	if chunk.get_child_count() < 2:
		_fail("generic lot (seed=%s) produced no building at all" % found_seed)
		chunk.free()
		return

	var body: StaticBody3D = chunk.get_child(1)
	var mesh_inst: MeshInstance3D = body.get_child(0)
	var actual_aabb: AABB = mesh_inst.mesh.get_aabb()
	var actual_mat: StandardMaterial3D = mesh_inst.material_override
	var actual_offset := Vector2(body.position.x, body.position.z)
	var expected_offset := Vector2(expected_jitter_x, expected_jitter_z)

	var size_ok := absf(actual_aabb.size.x - expected_width) < 0.05 and absf(actual_aabb.size.z - expected_depth) < 0.05 and absf(actual_aabb.size.y - expected_height) < 0.05
	var color_ok := actual_mat != null and actual_mat.albedo_color.is_equal_approx(expected_color)
	var offset_ok := actual_offset.is_equal_approx(expected_offset)

	if size_ok and color_ok and offset_ok:
		print("PASS: a generic (non-special) lot still produces the exact same building the original rng sequence would have (seed=", found_seed, ", size=", actual_aabb.size, ", roof='", expected_roof, "')")
	else:
		_fail("generic lot changed vs the original rng sequence (seed=%s): size_ok=%s (got %s expected ~[%s,%s,%s]) color_ok=%s offset_ok=%s (got %s expected %s)" % [found_seed, size_ok, actual_aabb.size, expected_width, expected_height, expected_depth, color_ok, offset_ok, actual_offset, expected_offset])

	chunk.free()

## --- 5. A full generate_chunk() call on a lot that DOES roll special
## produces the expected end-to-end structure: the same core exterior body
## (mesh + collision + door + indoor area) every generic building gets, a
## sign, and no wing. ---
func _check_full_generate_chunk_for_special_lot(materials: Dictionary) -> void:
	var chunk_size := 24.0
	var coord := Vector2i(1, 1) # not on the street grid (STREET_PERIOD=4)
	var found_seed := -1
	var found_type := ""

	for candidate in range(200000):
		var probe_rng := RandomNumberGenerator.new()
		probe_rng.seed = candidate
		if probe_rng.randf() >= CityChunk.LOT_BUILDING_CHANCE:
			continue
		var probe_special_rng := RandomNumberGenerator.new()
		probe_special_rng.seed = candidate ^ 0x53504543
		var picked := CityChunk._pick_special_building_type(probe_special_rng)
		if picked != "":
			found_seed = candidate
			found_type = picked
			break

	if found_seed == -1:
		_fail("couldn't find a special-rolling lot seed to test against -- test setup issue")
		return

	var chunk := CityChunk.generate_chunk(found_seed, coord, chunk_size, materials)

	# generate_chunk's own ground slab (_add_ground_body) is ALSO a
	# StaticBody3D directly under the chunk root, plus _build_building's own
	# 2 (the exterior shell AND a separate one _build_interior adds for the
	# interior floors -- see _check_special_building_geometry's own
	# comment) = 3 total. A wing (_maybe_add_wing) would add a 4th --
	# special buildings must never get one (see generate_chunk's own
	# special-type branch, which calls the sign helper instead of
	# _maybe_add_wing).
	var static_body_count := 0
	var root_mesh_count := 0
	var root_mesh: MeshInstance3D = null
	for child in chunk.get_children():
		if child is StaticBody3D:
			static_body_count += 1
		elif child is MeshInstance3D:
			root_mesh_count += 1
			root_mesh = child

	if static_body_count == 3:
		print("PASS: special building ('", found_type, "') gets exactly ground+exterior+interior bodies, no wing")
	else:
		_fail("special building ('%s', seed=%s) has %s StaticBody3D children directly under the chunk root, expected 3 (ground+exterior+interior, no wing)" % [found_type, found_seed, static_body_count])

	# Every special type -- hospital included, since its red cross was
	# removed -- now uses its own preset sign_color for one plain sign.
	var expected_color: Color = CityChunk.SPECIAL_BUILDING_PRESETS[found_type]["sign_color"]
	if root_mesh_count == 1 and root_mesh.material_override != null and root_mesh.material_override.albedo_color.is_equal_approx(expected_color):
		print("PASS: special building ('", found_type, "') got exactly one sign mesh in the expected color")
	else:
		_fail("special building ('%s') sign mesh missing or wrong color (found %s root-level MeshInstance3D nodes)" % [found_type, root_mesh_count])

	var body: StaticBody3D = chunk.get_child(1)
	if body.get_child_count() < 2 or not (body.get_child(0) is MeshInstance3D):
		_fail("special building ('%s') exterior body is missing its own mesh -- core _build_building output looks broken" % found_type)
	else:
		print("PASS: special building ('", found_type, "') exterior body still has a real mesh from the unmodified _build_building()")

	chunk.free()
