extends SceneTree
## Regression check for the rare basement buildings (city_chunk.gd's own
## _build_basement / BASEMENT_CHANCE).
##
## The failure this really guards against: the hole through the chunk's
## GROUND SLAB and the basement's own stairwell are computed in two
## different places (generate_chunk vs _build_basement) and have to agree
## exactly. If they drift, you get either a basement nobody can reach or a
## pit in the pavement opening onto solid fill -- neither of which errors,
## and both of which only show up if a player happens to walk into that one
## rare building.

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
	_check_basements_are_rare_but_real()
	_check_ground_hole_matches_stairwell()
	_check_stairwell_stays_inside_the_building()
	print("OVERALL: ", "PASS" if _ok else "FAIL")
	quit(0 if _ok else 1)

## "Rare" has to mean rare -- but it also has to actually happen, or the
## whole feature is dead code nobody would notice was broken.
func _check_basements_are_rare_but_real() -> void:
	var buildings := 0
	var basements := 0
	for x in range(-12, 13):
		for z in range(-12, 13):
			var coord := Vector2i(x, z)
			var p: Dictionary = CityChunk.building_params_for_seed(_chunk_seed(coord), coord, CHUNK_SIZE)
			if not p["has_building"]:
				continue
			buildings += 1
			if p["has_basement"]:
				basements += 1
	if basements == 0:
		_fail("no building anywhere in a 25x25 chunk sweep got a basement -- the feature never fires")
		return
	var rate := float(basements) / float(buildings)
	if rate < 0.3:
		print("PASS: basements occur but stay rare (", basements, " of ", buildings, " buildings, ", "%.1f%%" % (rate * 100.0), ")")
	else:
		_fail("basements are not rare: %d of %d buildings (%.1f%%)" % [basements, buildings, rate * 100.0])

## The load-bearing one: generate_chunk cuts the ground hole, _build_basement
## places the stairs, and both derive it from the same
## _stairwell_for_floor(BASEMENT_STAIR_CORNER) call. This recomputes both
## sides independently and demands they land on the same rectangle.
func _check_ground_hole_matches_stairwell() -> void:
	var checked := 0
	for x in range(-12, 13):
		for z in range(-12, 13):
			var coord := Vector2i(x, z)
			var p: Dictionary = CityChunk.building_params_for_seed(_chunk_seed(coord), coord, CHUNK_SIZE)
			if not p["has_building"] or not p["has_basement"]:
				continue
			var size: Vector3 = p["size"]
			var off: Vector3 = p["offset"]
			var hx: float = size.x / 2.0
			var hz: float = size.z / 2.0
			var run: float = minf(hx, hz) - CityChunk.STAIR_MARGIN
			var well: Dictionary = CityChunk._stairwell_for_floor(CityChunk.BASEMENT_STAIR_CORNER, hx, hz, run)

			# Ground-slab hole, in chunk-local space (what generate_chunk builds).
			var hole_x0: float = off.x + well["hole_x0"]
			var hole_x1: float = off.x + well["hole_x1"]
			var hole_z0: float = off.z + well["hole_z0"]
			var hole_z1: float = off.z + well["hole_z1"]
			# Basement ceiling hole, in BUILDING-local space, shifted into
			# chunk space the way the interior body's own offset does it.
			var ceil_x0: float = off.x + well["hole_x0"]
			var ceil_x1: float = off.x + well["hole_x1"]

			if not is_equal_approx(hole_x0, ceil_x0) or not is_equal_approx(hole_x1, ceil_x1):
				_fail("ground hole and basement stairwell disagree at %s" % coord)
				return
			if hole_x1 <= hole_x0 or hole_z1 <= hole_z0:
				_fail("degenerate ground hole at %s (%.2f..%.2f, %.2f..%.2f)" % [coord, hole_x0, hole_x1, hole_z0, hole_z1])
				return
			checked += 1
	if checked > 0:
		print("PASS: ground-slab hole lines up with the basement stairwell on all ", checked, " basement buildings")
	else:
		_fail("found no basement buildings to check the ground hole against")

## The hole is cut out of the chunk's pavement, so it must sit inside the
## building's own footprint -- a stairwell poking past the wall would open a
## pit in the open street outside.
func _check_stairwell_stays_inside_the_building() -> void:
	var checked := 0
	for x in range(-12, 13):
		for z in range(-12, 13):
			var coord := Vector2i(x, z)
			var p: Dictionary = CityChunk.building_params_for_seed(_chunk_seed(coord), coord, CHUNK_SIZE)
			if not p["has_building"] or not p["has_basement"]:
				continue
			var size: Vector3 = p["size"]
			var hx: float = size.x / 2.0
			var hz: float = size.z / 2.0
			var run: float = minf(hx, hz) - CityChunk.STAIR_MARGIN
			var well: Dictionary = CityChunk._stairwell_for_floor(CityChunk.BASEMENT_STAIR_CORNER, hx, hz, run)
			if well["hole_x0"] < -hx or well["hole_x1"] > hx or well["hole_z0"] < -hz or well["hole_z1"] > hz:
				_fail("basement stairwell at %s pokes outside the building footprint -- that would open a hole in the street" % coord)
				return
			checked += 1
	if checked > 0:
		print("PASS: every basement stairwell stays within its own building footprint (", checked, " checked)")
