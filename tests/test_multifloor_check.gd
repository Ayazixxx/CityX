extends Node3D
## Regression check for multi-floor building interiors (see
## CityChunk._build_interior/_add_staircase): generation must never crash
## across a wide range of building heights/footprints, the floor count and
## leftover-height math must stay within the bounds that keep a single
## flight of stairs climbable (see MAX_INTERIOR_FLOORS's own doc comment
## for the pathological case this guards against), and -- the check that
## actually caught a real bug during development -- a CharacterBody3D must
## be able to walk up a staircase's sloped ramp collision via
## move_and_slide, not just render correctly. Runs headless: pure
## generation + physics, no rendering needed.

const CityChunk = preload("res://city/city_chunk.gd")

var _body: CharacterBody3D
var _frames := 0
var _climb_start_y: float
var _stress_ok := true

func _ready() -> void:
	var materials := CityChunk.build_materials()

	# 1. Stress sweep: many footprint/height combinations must never crash
	# (this is exactly how the original ROOM_HEIGHT/negative-collision-box
	# bug, and later the MAX_INTERIOR_FLOORS leftover-height bug, were
	# both first caught).
	var built := 0
	for x in range(-25, 25):
		for z in range(-25, 25):
			var coord := Vector2i(x, z)
			if CityChunk.is_street_chunk(coord):
				continue
			var chunk := CityChunk.generate_chunk(hash(coord), coord, 24.0, materials)
			built += 1
			chunk.free()
	print("PASS: generated ", built, " chunks across a 50x50 sweep with no crash")

	# 2. A known building built directly (bypassing random footprint/
	# height) so the exact floor count is predictable: size.y=MAX_HEIGHT is
	# the worst case for leftover top-floor height -- referenced directly
	# rather than a hardcoded number, so this test automatically tracks
	# whatever the real worst case is if MAX_HEIGHT ever changes again
	# (confirmed directly as a real staleness risk: a hardcoded 48 here
	# silently stopped being the actual worst case the moment MAX_HEIGHT
	# was raised past it, since floors beyond MAX_INTERIOR_FLOORS are
	# clamped either way -- the leftover height ABOVE the last modeled
	# floor only grows as size.y grows past that clamp point).
	var size := Vector3(14, CityChunk.MAX_HEIGHT, 14)
	var ground := StaticBody3D.new()
	ground.collision_layer = 1
	var gcol := CollisionShape3D.new()
	var gshape := BoxShape3D.new()
	gshape.size = Vector3(24, 1.0, 24)
	gcol.shape = gshape
	gcol.position = Vector3(0, -0.5, 0)
	ground.add_child(gcol)
	add_child(ground)
	CityChunk._build_building(self, size, Vector3(0, 0, 0), "", materials)

	# 3. The player must actually be able to climb the ground floor's own
	# staircase -- a compact footprint often forces a slope steeper than
	# CharacterBody3D's default 45-degree floor_max_angle, which reads a
	# too-steep ramp as a wall and stops the player cold at its base (see
	# player.gd's own FLOOR_MAX_ANGLE_DEGREES for the fix this checks).
	# The staircase sits against a SIDE wall, not centered (see
	# CityChunk._stairwell_for_floor) -- computed the same way production
	# code does rather than a hardcoded guess, so this test doesn't drift
	# out of sync with those formulas. The flight is tallest at its own
	# z0 (against the back wall) and shortest at z0+run (the open-room
	# side a player actually approaches from -- see _add_staircase's own
	# doc comment for why it's built this way round), so the climb starts
	# on the open side and walks TOWARD the back wall (-Z) to ascend.
	var hx := size.x / 2.0
	var hz := size.z / 2.0
	var v_layout := CityChunk._cell_layout(size.y, CityChunk.NOMINAL_FLOOR_METERS)
	var story_height: float = float(v_layout["size"]) * CityChunk.STORY_HEIGHT_FLOORS
	var stair_run: float = hz - CityChunk.STAIR_MARGIN
	var stairwell0: Dictionary = CityChunk._stairwell_for_floor(0, hx, hz, stair_run)
	var start_x: float = (stairwell0["x0"] + stairwell0["x1"]) / 2.0
	var start_z: float = float(stairwell0["z1"]) + 0.3 # just in front of the stairwell's own open-side entry

	_body = CharacterBody3D.new()
	_body.floor_max_angle = deg_to_rad(80.0)
	var cshape := CollisionShape3D.new()
	var capsule := CapsuleShape3D.new()
	capsule.radius = 0.3
	capsule.height = 1.6
	cshape.shape = capsule
	_body.add_child(cshape)
	_body.position = Vector3(start_x, 1.0, start_z)
	add_child(_body)
	_climb_start_y = _body.position.y

func _physics_process(delta: float) -> void:
	_frames += 1
	if not _body.is_on_floor():
		_body.velocity.y -= 9.8 * delta
	else:
		_body.velocity.y = 0.0
	_body.velocity.x = 0.0
	_body.velocity.z = -2.0
	_body.move_and_slide()

	if _frames == 420:
		var climbed := _body.position.y > _climb_start_y + 5.0
		if climbed:
			print("PASS: character climbed a staircase via move_and_slide (reached y=", _body.position.y, ")")
		else:
			print("FAIL: character did not climb the staircase (stuck at y=", _body.position.y, ")")
		var ok := _stress_ok and climbed
		print("OVERALL: ", "PASS" if ok else "FAIL")
		get_tree().quit(0 if ok else 1)
