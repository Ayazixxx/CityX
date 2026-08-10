extends Node3D
## Ambient pedestrian population -- keeps a small crowd of npc/civilian.gd
## alive around wherever the local player currently is, spawning new ones
## into chunks the city has already streamed in and freeing the ones left
## far behind. Created and fed by city.gd (see its own _ready/set_player);
## nothing else needs to know this exists.
##
## The city is infinite and streamed (see city.gd), so there's no fixed set
## of pedestrians to place once at startup -- this is the same
## "keep N of them near the player, recycle the rest" treatment the chunks
## themselves already get, applied to people.
##
## SPAWN POINTS ARE DERIVED, NOT SEARCHED. Rather than raycasting around
## hunting for somewhere a civilian would fit, both placements aim at spots
## city_chunk.gd's own generator GUARANTEES are clear:
##
##  - Outdoors: the perimeter band of any chunk. Buildings are inset by
##    LOT_MARGIN (3m) from their lot's own edge and their placement jitter
##    is individually capped so it can never eat into that margin (see
##    city_chunk.gd's own building_params) -- so the outer ~3m ring of every
##    chunk, lot or street, is always walkable ground.
##  - Indoors: the front-centre strip of a building's GROUND floor. Floor 0
##    always puts its staircase in the back-left corner and its carved-out
##    side room in the back-right (see _stairwell_for_floor /
##    _room_corner_for_floor), and a basement's stair hole is front-RIGHT
##    (BASEMENT_STAIR_CORNER) -- the front-centre, just inside the door,
##    is the one region none of those can ever reach.
##
## Every candidate is still ground-probed before use (see _ground_under), so
## an unexpected hole or an ungenerated chunk fails safe as "skip this one,
## try again next tick" rather than dropping a pedestrian into the void.
##
## Distances are all HORIZONTAL (XZ), never true 3D -- a player 300m up
## mid-intro-cutscene (see player/intro_cutscene.gd) or standing on a
## rooftop is still "at" the street below them, and should neither despawn
## the crowd nor be unable to spawn one.
##
## KNOWN GAP (multiplayer): not networked at all, same as every other NPC in
## this project. Each peer runs its own independent population with its own
## randomness, so two players in the same session see different pedestrians
## standing in different places.

const CityChunk = preload("res://city/city_chunk.gd")
const CivilianScene := preload("res://npc/civilian.tscn")

# How many LIVE civilians to keep around the player. Corpses don't count
# toward this (see _live_count) -- killing one repopulates rather than
# permanently thinning the streets.
const MAX_CIVILIANS := 14
# Roughly a third of them get placed inside a building instead of on the
# street -- "some are hanging out indoors," not most.
const INDOOR_FRACTION := 0.35

# One spawn attempt at a time on a timer, never a burst. Each civilian
# instantiates a full character.tscn (a real skeleton + rig), so a dozen in
# one frame is exactly the kind of spike the city's own chunk streaming was
# already retuned to avoid (see city.gd's own STREAM_CHUNKS_PER_FRAME).
const SPAWN_INTERVAL_SECONDS := 0.75

# Which chunks around the player's own are eligible. Kept below city.gd's
# own LOAD_RADIUS (4) so a target chunk is essentially always generated
# already -- _ground_under fails safe if it somehow isn't.
const SPAWN_CHUNK_RADIUS := 3
# Never spawn one within this distance of the player -- a pedestrian popping
# into existence in plain view reads as a glitch, not as a passer-by.
const SPAWN_MIN_DISTANCE := 22.0
# Left-behind civilians are freed out here. Inside city.gd's own
# UNLOAD_RADIUS (6 chunks, 144m) on purpose: a civilian outlives the chunk
# it was standing on (it's parented to this node, not to the chunk), so it
# has to go before the ground under it does or it just falls forever.
const DESPAWN_DISTANCE := 130.0
# Corpses go sooner than the living -- they can't walk anywhere, and
# leaving every body you make lying around the whole city accumulates.
const CORPSE_DESPAWN_DISTANCE := 70.0

# Wander radius handed to each spawned civilian (see civilian.gd's own
# home_position/wander_radius). Street pedestrians cover real ground;
# indoor ones stay put in the room they were placed in rather than
# wandering into the stairwell behind them.
const OUTDOOR_WANDER_RADIUS := 10.0
const INDOOR_WANDER_RADIUS := 2.5

# How far in from a chunk's own edge an outdoor spawn lands -- inside the
# guaranteed-clear perimeter band described in this file's class comment.
const SIDEWALK_INSET_MIN := 0.9
const SIDEWALK_INSET_MAX := 2.2
# Keeps an outdoor spawn off the exact chunk corner, where two chunks' own
# bands meet and two civilians could land nearly on top of each other.
const EDGE_CORNER_MARGIN := 2.5

# Fraction of a building's own half-footprint the indoor strip spans. Both
# are derived in this file's class comment -- 0.35 of the half-width clears
# the ground floor's back-left staircase and any front-right basement hole
# for every footprint city_chunk.gd can produce, and the depth band keeps
# the civilian in the front half without standing in the doorway itself.
const INDOOR_HALF_WIDTH_FRACTION := 0.35
const INDOOR_DEPTH_NEAR_FRACTION := 0.3
const INDOOR_DOOR_CLEARANCE := 1.6

# Vertical band a candidate point is probed over. Generous upward (a
# candidate's own nominal Y is street level, and a sidewalk sits fractionally
# above it), short downward so a point over a basement stair hole or an open
# lot edge finds nothing and is rejected.
const GROUND_PROBE_UP := 3.0
const GROUND_PROBE_DOWN := 1.5

## Set by city.gd right after construction -- used for CHUNK_SIZE and to
## resolve a chunk's own deterministic seed (see chunk_seed_for). Duck-typed
## rather than a preload, since city.gd already preloads THIS file and the
## two would otherwise form a cycle.
var city: Node3D = null

var _player: Node3D = null
var _spawn_cooldown := 0.0
var _civilians: Array[Node3D] = []

## Called by city.gd's own set_player -- the population follows whoever
## chunk streaming follows, i.e. this peer's own local player, not some
## other peer's replicated one.
func set_player(player: Node3D) -> void:
	_player = player

func _physics_process(delta: float) -> void:
	if _player == null or not is_instance_valid(_player) or city == null:
		return

	_prune()

	_spawn_cooldown -= delta
	if _spawn_cooldown > 0.0:
		return
	_spawn_cooldown = SPAWN_INTERVAL_SECONDS
	if _live_count() >= MAX_CIVILIANS:
		return
	_try_spawn_one()

## Drops freed nodes, then anything the player has walked away from --
## corpses on a shorter leash than the living (see CORPSE_DESPAWN_DISTANCE).
func _prune() -> void:
	var kept: Array[Node3D] = []
	for civilian in _civilians:
		if not is_instance_valid(civilian):
			continue
		var limit: float = CORPSE_DESPAWN_DISTANCE if bool(civilian.get("is_dead")) else DESPAWN_DISTANCE
		if _flat_distance(civilian.global_position, _player.global_position) > limit:
			civilian.queue_free()
			continue
		kept.append(civilian)
	_civilians = kept

func _live_count() -> int:
	var count := 0
	for civilian in _civilians:
		if is_instance_valid(civilian) and not bool(civilian.get("is_dead")):
			count += 1
	return count

## One attempt only -- a failure (no eligible chunk, no clear ground, too
## close to the player) just waits for the next tick rather than looping,
## which keeps a bad neighbourhood from turning into a busy spin.
func _try_spawn_one() -> void:
	var picked: Variant = _pick_chunk()
	if picked == null:
		return
	var coord: Vector2i = picked
	var indoors := randf() < INDOOR_FRACTION
	var spot: Vector3 = _indoor_spot(coord) if indoors else _outdoor_spot(coord)
	# An indoor pick lands on a chunk with no building often enough (street
	# chunks, and lots that rolled empty) to be worth one retry outdoors
	# rather than wasting the whole tick.
	if spot == Vector3.INF and indoors:
		indoors = false
		spot = _outdoor_spot(coord)
	if spot == Vector3.INF:
		return
	if _flat_distance(spot, _player.global_position) < SPAWN_MIN_DISTANCE:
		return
	_add_civilian(spot, INDOOR_WANDER_RADIUS if indoors else OUTDOOR_WANDER_RADIUS)

func _add_civilian(spot: Vector3, wander_radius: float) -> void:
	var civilian: Node3D = CivilianScene.instantiate()
	# Both set BEFORE add_child -- civilian.gd's own _ready() reads
	# home_position exactly once and falls back to wherever it landed if
	# it's still the sentinel.
	civilian.home_position = spot
	civilian.wander_radius = wander_radius
	add_child(civilian)
	civilian.global_position = spot
	# Facing is arbitrary but shouldn't be identical for everyone -- a whole
	# street of pedestrians all pointing due north reads as spawned-in props.
	civilian.rotation.y = randf_range(0.0, TAU)
	_civilians.append(civilian)

## A random already-generated chunk within SPAWN_CHUNK_RADIUS of the
## player's own. Null (not a Vector2i) when nothing qualifies -- Vector2i has
## no "none" value, and (0,0) is a perfectly real chunk.
func _pick_chunk() -> Variant:
	var chunk_size: float = city.CHUNK_SIZE
	var center := Vector2i(floori(_player.global_position.x / chunk_size), floori(_player.global_position.z / chunk_size))
	var candidates: Array[Vector2i] = []
	for dz in range(-SPAWN_CHUNK_RADIUS, SPAWN_CHUNK_RADIUS + 1):
		for dx in range(-SPAWN_CHUNK_RADIUS, SPAWN_CHUNK_RADIUS + 1):
			var coord := center + Vector2i(dx, dz)
			if city.is_chunk_loaded(coord):
				candidates.append(coord)
	if candidates.is_empty():
		return null
	return candidates[randi() % candidates.size()]

## A point on the guaranteed-clear perimeter band of `coord` (see this
## file's class comment), ground-probed. Vector3.INF if there's nothing
## solid under it.
func _outdoor_spot(coord: Vector2i) -> Vector3:
	var chunk_size: float = city.CHUNK_SIZE
	var half := chunk_size / 2.0
	var inset := randf_range(SIDEWALK_INSET_MIN, SIDEWALK_INSET_MAX)
	var along := randf_range(-half + EDGE_CORNER_MARGIN, half - EDGE_CORNER_MARGIN)
	var local: Vector3
	match randi() % 4:
		0: local = Vector3(along, 0.0, -half + inset)
		1: local = Vector3(along, 0.0, half - inset)
		2: local = Vector3(-half + inset, 0.0, along)
		_: local = Vector3(half - inset, 0.0, along)
	return _ground_under(_chunk_origin(coord) + local)

## A point on the front-centre strip of `coord`'s own building's GROUND
## floor -- just inside the front door, the one region no staircase, side
## room or basement hole can reach (see this file's class comment).
## Vector3.INF for a street chunk or a lot that rolled no building.
func _indoor_spot(coord: Vector2i) -> Vector3:
	if CityChunk.is_street_chunk(coord):
		return Vector3.INF
	var chunk_size: float = city.CHUNK_SIZE
	var params: Dictionary = CityChunk.building_params_for_seed(city.chunk_seed_for(coord), coord, chunk_size)
	if not params["has_building"]:
		return Vector3.INF

	var size: Vector3 = params["size"]
	var offset: Vector3 = params["offset"]
	var hx: float = size.x / 2.0
	var hz: float = size.z / 2.0
	# The door is on the +Z wall (see city_chunk.gd's own _build_building),
	# so "front" is +Z in the building's own local space.
	var near_z: float = hz * INDOOR_DEPTH_NEAR_FRACTION
	var far_z: float = hz - INDOOR_DOOR_CLEARANCE
	if far_z <= near_z:
		return Vector3.INF
	var local := Vector3(
		offset.x + randf_range(-hx * INDOOR_HALF_WIDTH_FRACTION, hx * INDOOR_HALF_WIDTH_FRACTION),
		0.0,
		offset.z + randf_range(near_z, far_z)
	)
	return _ground_under(_chunk_origin(coord) + local)

func _chunk_origin(coord: Vector2i) -> Vector3:
	var chunk_size: float = city.CHUNK_SIZE
	return Vector3(coord.x * chunk_size, 0.0, coord.y * chunk_size)

## The real floor position under `point` (its own Y ignored -- probed around
## street level, which is y=0 for every chunk's own ground slab AND for a
## building's ground floor, since the slab IS that floor). Vector3.INF when
## nothing solid is there: an ungenerated chunk, a basement stair hole, or
## anything else unexpected all fail safe through here.
func _ground_under(point: Vector3) -> Vector3:
	var from := Vector3(point.x, GROUND_PROBE_UP, point.z)
	var to := Vector3(point.x, -GROUND_PROBE_DOWN, point.z)
	var query := PhysicsRayQueryParameters3D.create(from, to)
	query.collision_mask = 1 # world geometry only
	var hit := get_world_3d().direct_space_state.intersect_ray(query)
	if hit.is_empty():
		return Vector3.INF
	var pos: Vector3 = hit["position"]
	# A hair above the surface so the capsule doesn't start the frame
	# already penetrating it.
	return pos + Vector3(0.0, 0.05, 0.0)

func _flat_distance(a: Vector3, b: Vector3) -> float:
	return Vector2(a.x - b.x, a.z - b.z).length()
