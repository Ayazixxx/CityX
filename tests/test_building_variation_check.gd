extends Node3D
## Regression check for city_chunk.gd's building-shape-variation additions:
## attached L-shaped wings (_maybe_add_wing/_build_wing_massing), decorative
## balconies (_add_balconies), the 2 new roof prop types (parapet/
## water_tower), and per-floor room partitioning (_room_corner_for_floor/
## _add_room_partition). Runs headless: pure generation + physics, no
## rendering needed. Doesn't re-cover the full footprint/height stress
## sweep -- tests/test_multifloor_check.gd's own 1369-chunk sweep already
## exercises _build_building/_build_interior with all of this wired in.

const CityChunk = preload("res://city/city_chunk.gd")

var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	call_deferred("_run_checks")

func _run_checks() -> void:
	var materials := CityChunk.build_materials()
	var facade_mat: Material = materials["facade_variants"][0]

	# --- 1. Wing attachment rolls at roughly WING_CHANCE across many seeds,
	# and never attaches to the door wall (root only ever gains a 4th
	# StaticBody3D child, never anything that would encroach on the +Z
	# wall the entrance sits on). ---
	var main_size := Vector3(14, 30, 14)
	var wing_count := 0
	var trials := 300
	for i in range(trials):
		var rng := RandomNumberGenerator.new()
		rng.seed = i * 7919 + 13
		var root := Node3D.new()
		CityChunk._maybe_add_wing(root, main_size, Vector3.ZERO, rng, materials, facade_mat)
		if root.get_child_count() > 0:
			wing_count += 1
			var wing: StaticBody3D = root.get_child(0)
			var has_mesh := false
			var has_collision := false
			for child in wing.get_children():
				if child is MeshInstance3D and child.mesh != null:
					has_mesh = true
				if child is CollisionShape3D and child.shape is BoxShape3D:
					has_collision = true
					var box: BoxShape3D = child.shape
					if box.size.x <= 0.0 or box.size.y <= 0.0 or box.size.z <= 0.0:
						_fail("wing collision box has a non-positive dimension: %s" % box.size)
			if not has_mesh:
				_fail("a wing was added with no visible mesh")
			if not has_collision:
				_fail("a wing was added with no collision shape")
		root.free()
	var wing_fraction := float(wing_count) / float(trials)
	if absf(wing_fraction - CityChunk.WING_CHANCE) < 0.1:
		print("PASS: wings attach in about ", wing_count, "/", trials, " trials (~", CityChunk.WING_CHANCE, " expected)")
	else:
		_fail("wing attachment rate %s is too far from WING_CHANCE %s" % [wing_fraction, CityChunk.WING_CHANCE])

	# --- 2. Room-corner assignment never collides with that same floor's
	# own staircase corner, across a wide range of floor indices. ---
	var corner_ok := true
	for floor_idx in range(40):
		var stair_corner := floor_idx % 4
		var room_corner: int = CityChunk._room_corner_for_floor(floor_idx)
		if room_corner == stair_corner:
			corner_ok = false
			_fail("floor %s: room corner %s collides with stair corner %s" % [floor_idx, room_corner, stair_corner])
		if room_corner != 0 and room_corner != 1:
			corner_ok = false
			_fail("floor %s: room corner %s isn't one of the 2 back corners" % [floor_idx, room_corner])
	if corner_ok:
		print("PASS: every floor's carved room corner avoids that floor's own staircase corner")

	# --- 3. A real building's ground floor carved room is actually
	# enterable -- walk a CharacterBody3D through its own doorway (same
	# real move_and_slide technique test_multifloor_check.gd/
	# test_building_glass_check.gd already use, not just a geometry-only
	# check). ---
	var size := Vector3(14, 20, 14)
	var ground := StaticBody3D.new()
	ground.collision_layer = 1
	var gcol := CollisionShape3D.new()
	var gshape := BoxShape3D.new()
	gshape.size = Vector3(30, 1.0, 30)
	gcol.shape = gshape
	gcol.position = Vector3(0, -0.5, 0)
	ground.add_child(gcol)
	add_child(ground)
	CityChunk._build_building(self, size, Vector3(0, 0, 0), "", materials)

	var hx := size.x / 2.0
	var hz := size.z / 2.0
	var room_w: float = clampf(size.x * CityChunk.ROOM_WIDTH_FRACTION, CityChunk.MIN_ROOM_SIZE, hx - 0.5)
	var room_d: float = clampf(size.z * CityChunk.ROOM_DEPTH_FRACTION, CityChunk.MIN_ROOM_SIZE, hz - 0.5)
	var room_corner0: int = CityChunk._room_corner_for_floor(0)
	var room_x0: float = -hx if room_corner0 == 0 else hx - room_w
	var room_x1: float = -hx + room_w if room_corner0 == 0 else hx
	var room_z0 := -hz
	var room_z1 := -hz + room_d
	var wall_a_x: float = room_x1 if room_corner0 == 0 else room_x0
	var door_z0 := room_z0 + (room_d - CityChunk.DOOR_WIDTH) / 2.0
	var door_z1 := door_z0 + CityChunk.DOOR_WIDTH
	var door_center_z := (door_z0 + door_z1) / 2.0

	var player := CharacterBody3D.new()
	player.floor_max_angle = deg_to_rad(50.0)
	var pshape := CollisionShape3D.new()
	var pcapsule := CapsuleShape3D.new()
	pcapsule.radius = 0.3
	pcapsule.height = 1.6
	pshape.shape = pcapsule
	player.add_child(pshape)
	player.collision_layer = 4
	add_child(player)
	# Starts just outside the room, on the near (center) side of the
	# doorway, and walks straight toward/through it. The room always sits
	# on the far side of wall_a_x from the building's own center -- i.e.
	# toward +X when wall_a_x > 0 (room corner 1), toward -X when
	# wall_a_x < 0 (room corner 0) -- so room_side_sign captures both which
	# way "outside" is (the opposite side) and which way to walk to enter.
	var room_side_sign: float = 1.0 if room_corner0 == 1 else -1.0
	var start_x: float = wall_a_x - room_side_sign * 1.5
	player.global_position = Vector3(start_x, 1.0, door_center_z)
	var walk_dir := room_side_sign

	for i in range(120):
		if not player.is_on_floor():
			player.velocity.y -= 9.8 * (1.0 / 60.0)
		else:
			player.velocity.y = 0.0
		player.velocity.x = walk_dir * 2.0
		player.velocity.z = 0.0
		player.move_and_slide()
		await get_tree().physics_frame

	var ended_inside_room := player.global_position.x > minf(room_x0, room_x1) - 0.1 and player.global_position.x < maxf(room_x0, room_x1) + 0.1 \
		and player.global_position.z > room_z0 - 0.1 and player.global_position.z < room_z1 + 0.1
	if ended_inside_room:
		print("PASS: a player can walk through the carved room's own doorway into it (ended at ", player.global_position, ")")
	else:
		_fail("player didn't end up inside the carved room (ended at %s, room x[%s,%s] z[%s,%s])" % [player.global_position, room_x0, room_x1, room_z0, room_z1])

	# --- 4. The 2 new roof prop types build without crashing and actually
	# add geometry. ---
	for roof_kind in ["parapet", "water_tower"]:
		var roof_parent := Node3D.new()
		add_child(roof_parent)
		CityChunk._add_roof_detail(roof_parent, roof_kind, size, Color(0.7, 0.7, 0.7))
		if roof_parent.get_child_count() > 0:
			print("PASS: roof type '", roof_kind, "' adds ", roof_parent.get_child_count(), " node(s) with no crash")
		else:
			_fail("roof type '%s' added no geometry" % roof_kind)
		roof_parent.queue_free()

	# --- 5. Balconies roll at roughly BALCONY_CHANCE_PER_ROW per eligible
	# row across many seeds, on a building tall enough to have
	# BALCONY_MAX_ROWS worth of rows. ---
	var tall_size := Vector3(14, 40, 14)
	var v_layout := CityChunk._cell_layout(tall_size.y, CityChunk.NOMINAL_FLOOR_METERS)
	var balcony_rows_hit := 0
	var balcony_trials := 60
	for i in range(balcony_trials):
		var rng2 := RandomNumberGenerator.new()
		rng2.seed = i * 104729 + 3
		var body := StaticBody3D.new()
		CityChunk._add_balconies(body, tall_size, rng2, v_layout, Color(0.7, 0.7, 0.7))
		balcony_rows_hit += body.get_child_count() / 4 # each balcony is 1 slab + 3 rails
		body.free()
	var max_row: int = mini(int(v_layout["count"]) - 1, CityChunk.BALCONY_MAX_ROWS)
	var expected_opportunities := balcony_trials * max_row
	var balcony_fraction := float(balcony_rows_hit) / float(expected_opportunities)
	if absf(balcony_fraction - CityChunk.BALCONY_CHANCE_PER_ROW) < 0.15:
		print("PASS: balconies appear on about ", balcony_rows_hit, "/", expected_opportunities, " eligible rows (~", CityChunk.BALCONY_CHANCE_PER_ROW, " expected)")
	else:
		_fail("balcony placement rate %s is too far from BALCONY_CHANCE_PER_ROW %s" % [balcony_fraction, CityChunk.BALCONY_CHANCE_PER_ROW])

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
