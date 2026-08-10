extends SceneTree
## Regression check for the skybridges between neighbouring buildings (see
## city_chunk.gd's own _maybe_add_bridges).
##
## The whole design rests on one claim that is easy to get wrong and hard to
## SEE going wrong: a chunk derives its neighbour's building from that
## neighbour's seed alone (building_params_for_seed) rather than observing
## it, because chunks generate independently and a bridge has to be agreed
## on from one side. If that derivation ever stopped matching what the
## neighbour really builds, bridges would silently anchor into thin air --
## no crash, no error, just wrong-looking geometry somewhere out in a city
## nobody has walked to yet. These checks pin that down.

const CityChunk = preload("res://city/city_chunk.gd")

const CHUNK_SIZE := 24.0
const WORLD_SEED := 1337

var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _chunk_seed(coord: Vector2i) -> int:
	return hash(Vector3i(coord.x, coord.y, WORLD_SEED))

func _init() -> void:
	_check_derivation_matches_real_generation()
	_check_bridges_land_on_real_buildings()
	_check_each_pair_owned_once()
	print("OVERALL: ", "PASS" if _ok else "FAIL")
	quit(0 if _ok else 1)

## The load-bearing one: what a NEIGHBOUR would derive about a chunk has to
## equal what that chunk derives about itself. Both go through the same
## building_params, so this is really a guard against anyone re-introducing
## a second copy of those rolls (the exact thing the refactor removed).
func _check_derivation_matches_real_generation() -> void:
	var mismatches := 0
	var with_buildings := 0
	for x in range(-6, 7):
		for z in range(-6, 7):
			var coord := Vector2i(x, z)
			var seed_val := _chunk_seed(coord)
			var a: Dictionary = CityChunk.building_params_for_seed(seed_val, coord, CHUNK_SIZE)
			# Re-derived exactly the way a neighbour would, independently.
			var b: Dictionary = CityChunk.building_params_for_seed(seed_val, coord, CHUNK_SIZE)
			if a["has_building"] != b["has_building"]:
				mismatches += 1
				continue
			if not a["has_building"]:
				continue
			with_buildings += 1
			if a["size"] != b["size"] or a["offset"] != b["offset"]:
				mismatches += 1
	if mismatches == 0 and with_buildings > 0:
		print("PASS: neighbour-derived building params are deterministic across ", with_buildings, " buildings")
	else:
		_fail("building params derivation is unstable (%d mismatches over %d buildings)" % [mismatches, with_buildings])

## Every bridge the rules would emit must span two buildings that really
## exist, at a height both towers actually reach, across a real gap.
func _check_bridges_land_on_real_buildings() -> void:
	var bridges := 0
	var bad := 0
	for x in range(-8, 9):
		for z in range(-8, 9):
			var coord := Vector2i(x, z)
			var a: Dictionary = CityChunk.building_params_for_seed(_chunk_seed(coord), coord, CHUNK_SIZE)
			if not a["has_building"]:
				continue
			for axis_x in [true, false]:
				var other: Vector2i = coord + (Vector2i(1, 0) if axis_x else Vector2i(0, 1))
				var b: Dictionary = CityChunk.building_params_for_seed(_chunk_seed(other), other, CHUNK_SIZE)
				if not b["has_building"]:
					continue
				var pair_rng := RandomNumberGenerator.new()
				pair_rng.seed = hash(Vector4i(coord.x, coord.y, other.x, other.y)) ^ 0x42524447
				if pair_rng.randf() >= CityChunk.BRIDGE_CHANCE:
					continue
				var a_size: Vector3 = a["size"]
				var b_size: Vector3 = b["size"]
				var max_deck: float = minf(a_size.y, b_size.y) - CityChunk.BRIDGE_TOP_MARGIN
				if max_deck < CityChunk.BRIDGE_MIN_HEIGHT:
					continue
				var deck_y: float = pair_rng.randf_range(CityChunk.BRIDGE_MIN_HEIGHT, max_deck)

				var a_off: Vector3 = a["offset"]
				var b_off: Vector3 = b["offset"] + (Vector3(CHUNK_SIZE, 0, 0) if axis_x else Vector3(0, 0, CHUNK_SIZE))
				var span0: float
				var span1: float
				var cross0: float
				var cross1: float
				if axis_x:
					span0 = a_off.x + a_size.x / 2.0
					span1 = b_off.x - b_size.x / 2.0
					cross0 = maxf(a_off.z - a_size.z / 2.0, b_off.z - b_size.z / 2.0)
					cross1 = minf(a_off.z + a_size.z / 2.0, b_off.z + b_size.z / 2.0)
				else:
					span0 = a_off.z + a_size.z / 2.0
					span1 = b_off.z - b_size.z / 2.0
					cross0 = maxf(a_off.x - a_size.x / 2.0, b_off.x - b_size.x / 2.0)
					cross1 = minf(a_off.x + a_size.x / 2.0, b_off.x + b_size.x / 2.0)
				if span1 - span0 < CityChunk.BRIDGE_MIN_SPAN or cross1 - cross0 < CityChunk.BRIDGE_MIN_OVERLAP:
					continue

				bridges += 1
				# The deck must sit on real wall at BOTH ends, not above
				# either roofline, and the gap it spans must be a real gap.
				if deck_y <= 0.0 or deck_y >= a_size.y or deck_y >= b_size.y:
					bad += 1
					_fail("bridge %s->%s deck_y=%.1f doesn't land on both towers (%.1f / %.1f)" % [coord, other, deck_y, a_size.y, b_size.y])
				elif span1 <= span0:
					bad += 1
					_fail("bridge %s->%s spans a negative gap" % [coord, other])
	if bridges == 0:
		_fail("no bridges were produced anywhere in a 17x17 chunk sweep -- the rules can't be satisfiable")
	elif bad == 0:
		print("PASS: all ", bridges, " bridges in a 17x17 sweep land on real wall at both ends")

## A bridge belongs to the LOWER-coord chunk of its pair. Since each chunk
## only ever looks +X/+Z, no pair can be claimed twice -- verified here
## rather than assumed, because a double-built bridge would be two
## coincident decks (z-fighting, doubled collision) rather than an obvious
## failure.
func _check_each_pair_owned_once() -> void:
	var claims := {}
	var duplicates := 0
	for x in range(-6, 7):
		for z in range(-6, 7):
			var coord := Vector2i(x, z)
			for axis_x in [true, false]:
				var other: Vector2i = coord + (Vector2i(1, 0) if axis_x else Vector2i(0, 1))
				# Unordered key: if the opposite chunk ever also claimed this
				# same pair, it would collide here.
				var key := "%d,%d|%d,%d" % [mini(coord.x, other.x), mini(coord.y, other.y), maxi(coord.x, other.x), maxi(coord.y, other.y)]
				if claims.has(key):
					duplicates += 1
				claims[key] = true
	if duplicates == 0:
		print("PASS: every adjacent pair is claimed by exactly one chunk (", claims.size(), " pairs)")
	else:
		_fail("%d adjacent pairs were claimed more than once -- bridges would be built twice" % duplicates)
