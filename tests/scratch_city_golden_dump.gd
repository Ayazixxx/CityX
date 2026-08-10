extends SceneTree
## Dumps the city generator's DETERMINISTIC output to a golden JSON file, so
## the Unity port can be verified against the Godot original rather than
## against my own reading of it.
##
## WHY THIS MATTERS MORE THAN IT LOOKS.
##
## PORT_PLAN's biggest carried asset is "the world is a seed, not a level":
## server and client generate byte-identical cities, so no geometry is ever
## sent over the network. That guarantee rests entirely on the random number
## generator producing the same sequence in both processes.
##
## In Unity that is NOT free, and the danger is specific: a regional shard
## runs on LINUX while players run WINDOWS. System.Random's algorithm is not
## contractually stable across .NET versions or platforms, and
## UnityEngine.Random is global mutable state shared by every system in the
## process. Either one would produce a server and a client that quietly
## disagree about where the buildings are.
##
## So the port must carry its own explicit PRNG. Reimplementing Godot's PCG32
## exactly costs no more than inventing a different one, and buys this: the
## Godot build becomes an ORACLE. Every number below is a fact about how the
## world is supposed to generate, and the C# port either reproduces it or is
## wrong.
##
## Run:
##   ./tools/Godot_v4.7.1-stable_win64_console.exe --headless --path . \
##       -s tests/scratch_city_golden_dump.gd

const CityChunk = preload("res://city/city_chunk.gd")

const OUTPUT_PATH := "res://CityX/Assets/CityX/Tests/Resources/CityX.Golden/city_golden.json"

## Seeds chosen to cover the awkward cases, not just easy ones: zero, small,
## large, and the 32-bit boundary where a sloppy port would wrap differently.
const RNG_SEEDS := [0, 1, 42, 12345, 2147483647, 4294967295]

const SEQUENCE_LENGTH := 12
const CHUNK_SIZE := 24.0

func _init() -> void:
	var golden := {}

	golden["chunk_size"] = CHUNK_SIZE
	golden["street_period"] = CityChunk.STREET_PERIOD

	# --- 1. Raw RNG sequences. If these do not match, nothing else can. ---
	var rng_cases := []
	for seed_value in RNG_SEEDS:
		var rng := RandomNumberGenerator.new()
		rng.seed = seed_value

		var randf_values := []
		for i in range(SEQUENCE_LENGTH):
			randf_values.append(rng.randf())

		var rng2 := RandomNumberGenerator.new()
		rng2.seed = seed_value
		var randi_values := []
		for i in range(SEQUENCE_LENGTH):
			randi_values.append(rng2.randi())

		var rng3 := RandomNumberGenerator.new()
		rng3.seed = seed_value
		var range_values := []
		for i in range(SEQUENCE_LENGTH):
			range_values.append(rng3.randf_range(9.0, 18.0))

		rng_cases.append({
			"seed": seed_value,
			"randf": randf_values,
			"randi": randi_values,
			"randf_range_9_18": range_values,
		})
	golden["rng"] = rng_cases

	# --- 2. Street layout. Pure integer maths, but posmod on negatives is a
	# --- classic porting trap: C#'s % returns negative for negative operands
	# --- exactly like GDScript's, so a naive port breaks the whole west/south
	# --- half of the world and nowhere else. ---
	var street_cases := []
	for x in range(-9, 10):
		for y in range(-9, 10):
			street_cases.append({
				"x": x,
				"y": y,
				"is_street": CityChunk.is_street_chunk(Vector2i(x, y)),
			})
	golden["street_chunks"] = street_cases

	# --- 3. building_params: the actual world. ---
	var param_cases := []
	for seed_value in RNG_SEEDS:
		for x in range(-4, 5):
			for y in range(-4, 5):
				var coord := Vector2i(x, y)
				# Mirrors city.gd's own per-chunk seeding shape closely enough
				# to exercise the same code; the exact derivation lives there.
				var chunk_seed: int = seed_value ^ (x * 73856093) ^ (y * 19349663)
				var params: Dictionary = CityChunk.building_params_for_seed(chunk_seed, coord, CHUNK_SIZE)

				var entry := {
					"world_seed": seed_value,
					"x": x,
					"y": y,
					"chunk_seed": chunk_seed,
					"has_building": params.get("has_building", false),
				}
				if params.get("has_building", false):
					var size: Vector3 = params["size"]
					var offset: Vector3 = params["offset"]
					entry["width"] = size.x
					entry["height"] = size.y
					entry["depth"] = size.z
					entry["offset_x"] = offset.x
					entry["offset_z"] = offset.z
					entry["roof"] = params.get("roof", "")
					entry["facade_index"] = params.get("facade_index", 0)
					entry["special_type"] = params.get("special_type", "")
					entry["has_basement"] = params.get("has_basement", false)
				param_cases.append(entry)
	golden["building_params"] = param_cases

	# --- write ---
	var dir_path := OUTPUT_PATH.get_base_dir()
	var abs_dir := ProjectSettings.globalize_path(dir_path)
	DirAccess.make_dir_recursive_absolute(abs_dir)

	var file := FileAccess.open(OUTPUT_PATH, FileAccess.WRITE)
	if file == null:
		print("FAIL: could not open ", OUTPUT_PATH, " (", FileAccess.get_open_error(), ")")
		quit(1)
		return
	file.store_string(JSON.stringify(golden, "  "))
	file.close()

	var buildings := 0
	for entry in param_cases:
		if entry["has_building"]:
			buildings += 1

	print("PASS: wrote golden data to ", OUTPUT_PATH)
	print("  rng cases:        ", rng_cases.size(), " seeds x ", SEQUENCE_LENGTH, " draws x 3 kinds")
	print("  street chunks:    ", street_cases.size(), " coords")
	print("  building params:  ", param_cases.size(), " lots, ", buildings, " with buildings")
	print("OVERALL: PASS")
	quit(0)
