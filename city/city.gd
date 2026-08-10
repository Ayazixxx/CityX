extends Node3D
## Continuous, procedurally generated city that streams in as the player
## walks, the same way Minecraft only builds terrain chunks near the player
## instead of the whole world up front -- see city_chunk.gd for what
## actually fills a chunk (ground + usually one building).
##
## The world is an infinite grid of CHUNK_SIZE x CHUNK_SIZE chunks, indexed
## by integer (x, z) chunk coordinates. Chunks within LOAD_RADIUS of the
## player's current chunk are kept generated; chunks farther than
## UNLOAD_RADIUS are freed. UNLOAD_RADIUS is deliberately bigger than
## LOAD_RADIUS so a chunk sitting right at the edge of the loaded area
## doesn't repeatedly load/unload as the player drifts back and forth
## across that boundary.
##
## Each chunk's contents are fully deterministic (see city_chunk.gd's
## chunk_seed derivation) -- revisiting a freed chunk regenerates exactly
## what was there before, and (once real multiplayer sessions exist) every
## peer generating the same coordinate independently would produce the
## same result too, with no need to network the city's geometry at all.
##
## Both the Sun and Moon (DayNightCycle, see day_night_cycle.gd) have
## shadow_enabled on. That was DISABLED for a while after it covered every
## flat exterior surface (road, sidewalk) in a persistent, fine-grained
## dithered noise -- tried shadow_bias, shadow_normal_bias, shadow_blur,
## directional_shadow_max_distance, and directional_shadow_mode (each
## independently), plus forcing MSAA/FXAA, none of which touched it. The
## actual cause turned out to be a project-wide rendering setting never
## looked at during any of that: rendering/lights_and_shadows/
## directional_shadow/soft_shadow_filter_quality (project.godot's own
## [rendering] section), which defaults to a fairly low PCF sample count --
## fine for a small/enclosed scene, but not for shadows spread across a
## streamed-in city this size. Raised to 4 (one below Godot's own max of
## 5, which looked identical in a direct comparison) -- confirmed directly,
## same reproduction spot, clean before/after.

const CityChunk = preload("res://city/city_chunk.gd")

const CHUNK_SIZE := 24.0
# LOAD_RADIUS*CHUNK_SIZE is the guaranteed-generated distance from the
# player -- was 3 (72m), confirmed directly as visibly too short: buildings
# noticeably popped into existence while walking toward them, well inside
# normal view distance. Bumped to 4 (96m); see city.tscn's own fog_depth_end,
# tuned to fully hide this same boundary so the edge itself is never visible
# even though it's now farther out.
const LOAD_RADIUS := 4
const UNLOAD_RADIUS := 6
# How much of LOAD_RADIUS must exist before initial_load_ready fires and
# main.gd lets the player actually start playing -- deliberately smaller
# than LOAD_RADIUS itself (see _load_initial_area_async/
# _load_remaining_area_async): with LOAD_RADIUS=4 that's 81 chunks, several
# building a full multi-floor interior, and yielding a couple at a time
# still added up to a genuinely long wait with nothing to do but watch the
# title screen's rain (reported directly: "long loading screen that
# freezes the whole screen"). Only guaranteeing this smaller radius
# (25 chunks) up front cuts that wait to roughly a third, while the
# remaining ring streams in RIGHT AFTER, in the background, while the
# player is already free to look around and move -- no perceived freeze at
# all for that part, same as how ordinary walking-triggered streaming
# already works once in the world. 2 still comfortably covers immediate
# spawn surroundings (120m across); the outer ring filling in a couple
# seconds late, before the player has any reason to already be near that
# edge, is a small, self-correcting trade for a much shorter blocking wait.
const INITIAL_LOAD_RADIUS := 2
# TODO(multiplayer): once real sessions exist, this needs to come from the
# host/lobby instead of being hardcoded, so every peer's city agrees --
# today there's only ever one local instance of the world, so a constant is
# fine.
const WORLD_SEED := 1337

# How many chunks to build per frame while spreading the STARTING area out
# over several frames (see _load_initial_area_async) instead of the single
# blocking _ready()-time call this used to be. Picked conservatively low --
# several of these 81 chunks build a full multi-floor building interior
# (walls, window-tiled facades, a staircase), real work, not tuned for the
# shortest possible total load time. The point is keeping every frame's own
# cost small enough that the engine keeps rendering/responding throughout
# the load, not minimizing wall-clock time to finish it.
const INITIAL_LOAD_CHUNKS_PER_FRAME := 2

# Emitted once the starting INITIAL_LOAD_RADIUS area around the origin has
# finished generating (see _load_initial_area_async) -- main.gd awaits this
# before actually spawning a player into the city, so they never end up
# standing over a still-empty lot mid-load. Deliberately NOT the same thing
# as the full LOAD_RADIUS being ready (see full_load_ready below) -- that's
# the whole point of the split.
signal initial_load_ready
# Emitted once the REST of the area, out to the full LOAD_RADIUS, has also
# finished generating (see _load_remaining_area_async) -- fires well after
# initial_load_ready, invisibly in the background while the player's
# already playing. Nothing currently needs to await this one directly the
# way main.gd awaits initial_load_ready (there's no "and now do X" that
# depends on the outer ring specifically being done); it exists mainly so
# tests can reliably wait for the WHOLE load to settle instead of guessing
# a frame count (see test_city_chunks_check.gd's own comment on why
# guessing was already confirmed unreliable for the inner-radius case).
signal full_load_ready

# How many newly-in-range chunks may actually be GENERATED per frame while
# streaming (see _update_chunks/_drain_pending_chunks) -- a real reported
# bug: "as i run around the game starts freezing up like every 10 seconds
# i think its freezing when a new chunk loads in". Crossing a single chunk
# boundary brings a whole new ROW of (2*LOAD_RADIUS+1) = 9 chunks into
# range at once, and _update_chunks used to generate every one of them
# SYNCHRONOUSLY inside the same _physics_process tick -- 9 buildings' worth
# of real procedural geometry (walls, per-window glass holes, interiors,
# staircases) in one frame, which is exactly the visible hitch, repeating
# every time the player crosses into a new chunk.
#
# 1 per frame turns that single 9-chunk spike into 9 ordinary frames (~0.15s
# to fully catch up at 60fps) -- imperceptible, and the chunks stream in
# well ahead of the player either way since LOAD_RADIUS (4 chunks, 96m) is
# far beyond where they can walk in that time. This is the same
# yield-between-batches technique _load_initial_area_async already uses for
# the startup load, just applied to the ongoing streaming path that was
# still fully synchronous.
const STREAM_CHUNKS_PER_FRAME := 1

var _materials: Dictionary
var _player: Node3D = null
var _loaded_chunks := {} # Vector2i(chunk x, chunk z) -> Node3D chunk root
var _last_player_chunk := Vector2i.ZERO
# Coords that are in range but not generated yet, nearest-first (see
# _update_chunks) -- drained a few per frame by _drain_pending_chunks.
var _pending_chunks: Array[Vector2i] = []

func _ready() -> void:
	_materials = CityChunk.build_materials()
	_load_initial_area_async()

## Loads only INITIAL_LOAD_RADIUS (not the full LOAD_RADIUS) around the
## origin before firing initial_load_ready, spread across multiple frames
## instead of one big blocking call -- see INITIAL_LOAD_RADIUS's own
## comment for why this is deliberately smaller than the streaming radius.
## Real, reported bug this originally fixed: generating the FULL LOAD_RADIUS
## (81 chunks, several building a full multi-floor building interior)
## synchronously in one _ready() call took upward of ten real seconds with
## the engine completely unable to render a single frame in between
## (nothing animates, no input processes -- reads exactly like a crash, not
## a loading screen). Yielding via `await get_tree().process_frame` between
## small batches lets the engine keep rendering/responding throughout -- see
## main.gd's own _enter_world_with, which now awaits initial_load_ready
## BEFORE even starting its own screen fade, specifically so the previous
## screen (rain and all) stays fully visible and animating for the whole
## loading window instead of a frozen-looking black one. Reported again
## later even with that fix in place ("long loading screen that freezes the
## whole screen") -- 81 chunks' worth of yielded-but-still-real generation
## work still added up to a long wait; shrinking what's actually BLOCKING
## down to INITIAL_LOAD_RADIUS (see _load_remaining_area_async for the rest)
## is the actual fix for the wait being long, not just non-frozen-looking.
func _load_initial_area_async() -> void:
	var since_last_yield := 0
	for dz in range(-INITIAL_LOAD_RADIUS, INITIAL_LOAD_RADIUS + 1):
		for dx in range(-INITIAL_LOAD_RADIUS, INITIAL_LOAD_RADIUS + 1):
			var coord := Vector2i(dx, dz)
			if not _loaded_chunks.has(coord):
				_load_chunk(coord)
				since_last_yield += 1
				if since_last_yield >= INITIAL_LOAD_CHUNKS_PER_FRAME:
					since_last_yield = 0
					await get_tree().process_frame
	_last_player_chunk = Vector2i.ZERO
	initial_load_ready.emit()
	_load_remaining_area_async()

## Fills the ring between INITIAL_LOAD_RADIUS and the full LOAD_RADIUS,
## started right after initial_load_ready fires -- by this point main.gd
## has already spawned the player and let them loose (see its own
## _enter_world_with), so this whole thing runs invisibly in the
## background while they're already free to look around and move, same
## yielded-per-frame technique as the initial load so it still can't stall
## a frame either. Ordinary walking-triggered streaming (_update_chunks,
## called from _physics_process) requires no changes to cooperate with
## this: it already skips any coord _loaded_chunks already has, so if the
## player wanders out to the edge before this finishes, the two simply
## don't double-load the same chunk.
func _load_remaining_area_async() -> void:
	var since_last_yield := 0
	for dz in range(-LOAD_RADIUS, LOAD_RADIUS + 1):
		for dx in range(-LOAD_RADIUS, LOAD_RADIUS + 1):
			var coord := Vector2i(dx, dz)
			if not _loaded_chunks.has(coord):
				_load_chunk(coord)
				since_last_yield += 1
				if since_last_yield >= INITIAL_LOAD_CHUNKS_PER_FRAME:
					since_last_yield = 0
					await get_tree().process_frame
	full_load_ready.emit()

## Called by main.gd once the player's been added as this city's child --
## city.tscn/city.gd have no way to know about the player on their own,
## since main.gd instantiates them in that order (city first, then player).
func set_player(player: Node3D) -> void:
	_player = player
	# The spawn chunk has to be marked HERE, not left to _physics_process:
	# that only marks on a chunk CHANGE, and the player spawns into
	# _last_player_chunk's own starting value (0,0), so standing still at
	# spawn would never register as having been anywhere.
	_mark_explored(_world_to_chunk(player.global_position))

func _physics_process(_delta: float) -> void:
	if _player == null:
		return
	# global_position, not position -- a real reported bug ("world stopped
	# generating while driving"): _player.position is relative to whatever
	# node CURRENTLY parents the player, which is this City directly while
	# on foot but a car's own DriverSeat/PassengerSeat while driving (see
	# vehicles/car.gd's own _complete_enter, which reparents the whole
	# Player node and resets its LOCAL position to Vector3.ZERO). Once
	# seated, .position stays pinned at that same (0,0,0) local offset
	# forever regardless of where the car actually drives -- chunk == the
	# same starting chunk every single tick, so _update_chunks() never runs
	# again and the streamed-in world just stops growing the moment you get
	# in and drive away from it. global_position always reflects the
	# player's real world location no matter what currently parents them.
	var chunk := _world_to_chunk(_player.global_position)
	if chunk != _last_player_chunk:
		_mark_explored(chunk)
		_update_chunks(chunk)
	_drain_pending_chunks()

## Generates at most STREAM_CHUNKS_PER_FRAME of the queued in-range chunks
## (see that constant for the hitch this spreads out). Coords that fell back
## out of range while queued, or that something else already generated, are
## dropped rather than built.
func _drain_pending_chunks() -> void:
	var built := 0
	while built < STREAM_CHUNKS_PER_FRAME and not _pending_chunks.is_empty():
		var coord: Vector2i = _pending_chunks.pop_front()
		if _loaded_chunks.has(coord):
			continue
		var d: Vector2i = coord - _last_player_chunk
		if maxi(absi(d.x), absi(d.y)) > LOAD_RADIUS:
			continue
		_load_chunk(coord)
		built += 1

func _world_to_chunk(world_pos: Vector3) -> Vector2i:
	return Vector2i(floori(world_pos.x / CHUNK_SIZE), floori(world_pos.z / CHUNK_SIZE))

## Newly-in-range chunks are QUEUED here rather than generated on the spot
## (see STREAM_CHUNKS_PER_FRAME for the reported hitch that fixes) --
## unloading stays immediate, since freeing a node is cheap next to building
## one. Sorted nearest-first so whatever the player is walking toward
## generates before the far edge of the ring they're walking away from.
func _update_chunks(center: Vector2i) -> void:
	_last_player_chunk = center

	var newly_in_range: Array[Vector2i] = []
	for dz in range(-LOAD_RADIUS, LOAD_RADIUS + 1):
		for dx in range(-LOAD_RADIUS, LOAD_RADIUS + 1):
			var coord := center + Vector2i(dx, dz)
			if not _loaded_chunks.has(coord) and not _pending_chunks.has(coord):
				newly_in_range.append(coord)
	newly_in_range.sort_custom(func(a: Vector2i, b: Vector2i) -> bool:
		var da: Vector2i = a - center
		var db: Vector2i = b - center
		return maxi(absi(da.x), absi(da.y)) < maxi(absi(db.x), absi(db.y))
	)
	_pending_chunks.append_array(newly_in_range)

	var to_unload := []
	for coord in _loaded_chunks.keys():
		var d: Vector2i = coord - center
		if maxi(absi(d.x), absi(d.y)) > UNLOAD_RADIUS:
			to_unload.append(coord)
	for coord in to_unload:
		_unload_chunk(coord)

func _load_chunk(coord: Vector2i) -> void:
	# The origin sits at (0, 0), which is always a through-street chunk (see
	# CityChunk.is_street_chunk -- 0 is a multiple of every STREET_PERIOD),
	# so the player always spawns onto clear ground with no building on it.
	# _chunk_seed is handed over so a chunk can derive what its NEIGHBOUR
	# built (see city_chunk.gd's own building_params/_maybe_add_bridges) --
	# the world seed lives here, not there, and skybridges need both ends of
	# a pair to agree without either chunk being able to see the other.
	var chunk := CityChunk.generate_chunk(_chunk_seed(coord), coord, CHUNK_SIZE, _materials, _chunk_seed)
	chunk.name = "Chunk_%d_%d" % [coord.x, coord.y]
	chunk.position = Vector3(coord.x * CHUNK_SIZE, 0.0, coord.y * CHUNK_SIZE)
	add_child(chunk)
	_loaded_chunks[coord] = chunk

func _unload_chunk(coord: Vector2i) -> void:
	var chunk: Node3D = _loaded_chunks[coord]
	chunk.queue_free()
	_loaded_chunks.erase(coord)


## How far around the player's own chunk counts as "been there" for the map
## (see ui/map_panel.gd). 1 means the 3x3 block around them: you fill in the
## street you're on and the lots either side of it, not just the single
## 24m square your feet are in, which is what makes walking down a road
## reveal a road-shaped strip rather than a dotted line.
##
## Deliberately much smaller than LOAD_RADIUS. Chunks are generated well
## beyond what you can actually see (4 chunks, 96m, through fog and around
## corners); revealing everything that was GENERATED would fill the map in
## faster than you explore and defeat the point.
const EXPLORE_RADIUS := 1

# Every chunk coord the player has been to (see EXPLORE_RADIUS). Used as a
# set -- the values are ignored.
#
# NOT PERSISTED: this resets when you quit, same as a claimed building.
# Saving it means adding a field to character_save.gd's own schema.
var _explored_chunks := {}

## Marks `center` and everything within EXPLORE_RADIUS of it as seen.
## Called only when the player actually crosses into a new chunk (see
## _physics_process) rather than every frame -- the result is identical and
## it avoids rewriting the same handful of dictionary keys 60 times a second
## while standing still.
func _mark_explored(center: Vector2i) -> void:
	for dz in range(-EXPLORE_RADIUS, EXPLORE_RADIUS + 1):
		for dx in range(-EXPLORE_RADIUS, EXPLORE_RADIUS + 1):
			_explored_chunks[center + Vector2i(dx, dz)] = true

## Whether the player has been to `coord` yet -- the map draws nothing for
## coords this returns false for.
func is_chunk_explored(coord: Vector2i) -> bool:
	return _explored_chunks.has(coord)

## Every chunk the player has been to. Returned as a fresh Array so a caller
## iterating it can't be tripped up by the player crossing a chunk boundary
## mid-loop.
func explored_chunks() -> Array:
	return _explored_chunks.keys()

## Public read of _chunk_seed for anything OUTSIDE this file that needs to
## derive what a chunk contains without generating it -- today that's
## ui/map_panel.gd replaying building_params_for_seed to draw a building's
## own footprint on the map. The world seed still lives only here.
func chunk_seed_for(coord: Vector2i) -> int:
	return _chunk_seed(coord)

## Deterministic per-chunk seed: the same world seed + coordinate always
## hashes to the same number, which is what makes regenerating a freed
## chunk (or, eventually, another peer generating it independently)
## reproduce it exactly instead of drawing fresh randomness.
func _chunk_seed(coord: Vector2i) -> int:
	return hash(Vector3i(coord.x, coord.y, WORLD_SEED))
