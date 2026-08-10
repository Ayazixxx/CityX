extends "res://npc/unique_npc.gd"
## Old Guy: the first story NPC, and the front half of the tutorial.
##
## He watches you land, offers to show you the ropes, and -- if you take him
## up on it -- tells you to claim a building. Once you actually claim one he
## turns up outside it.
##
## Conversation content lives HERE rather than in npc/unique_npc.gd's own
## `lines` export because it BRANCHES and it has MEMORY: what he says
## depends on whether you've accepted, declined, or already been sent off to
## claim somewhere. The base class handles everything generic (the interact
## trigger, the box, the input gate); this file is purely his script and his
## state.
##
## TUTORIAL STATE IS SESSION-ONLY, like the claim it waits on. Quitting and
## reloading puts him back to his opening line.

## Where he is in the tutorial. Only ever moves forward, except DECLINED,
## which goes back to OFFERING so you can change your mind.
enum Stage {
	GREETING,     ## hasn't said his piece yet
	DECLINED,     ## you turned him down; talking again re-offers
	AWAITING_CLAIM, ## you accepted and he's told you to claim a building
	TRAVELLING,   ## you claimed one and he's walking over to it
	ARRIVED,      ## standing outside your building, with the ask still to come
	MOVING_IN,    ## you said yes; he's letting himself in through the door
	LIVING_INSIDE, ## he lives here now, pottering about the ground floor
	WANDERING,    ## you said no; he's off wandering the streets
}

const CityChunk = preload("res://city/city_chunk.gd")
const BuildingDoor = preload("res://city/building_door.gd")

const OPTION_ACCEPT := "accept"
const OPTION_DECLINE := "decline"
const OPTION_LET_HIM_STAY := "let_him_stay"
const OPTION_TURN_HIM_AWAY := "turn_him_away"

## How far out from the building's own front wall he stands once you claim
## it -- clear of the doorway rather than blocking it.
const ARRIVAL_DOOR_CLEARANCE := 3.5
## Dropped from here and allowed to settle, rather than placed at an exact
## height: the pavement outside a building isn't always exactly y=0 (see
## city_chunk.gd's own kerb/road overlays).
const ARRIVAL_DROP_HEIGHT := 1.0

## The longest he'll be left walking before being staged in closer instead.
## Claim a building on the far side of the city and a real walk over would
## be several minutes of nothing.
const MAX_WALK_SECONDS := 40.0
## How far out he's staged from when the real walk would be too long. At
## WALK_SPEED that's about 12 seconds -- long enough to read as him walking
## up rather than appearing, short enough not to be a wait.
const STAGING_DISTANCE := 30.0
## How many points around the destination to consider staging him at.
const STAGING_CANDIDATES := 12
## Vertical band a staging candidate is ground-probed over. Short, so a
## point over a hole or off a ledge finds nothing and is skipped.
const STAGING_PROBE_UP := 3.0
const STAGING_PROBE_DOWN := 2.0
## Height the line-of-sight and visibility checks are run at -- chest, not
## the feet, so a kerb doesn't read as a wall and a point isn't called
## visible because its ankles are.
const STAGING_EYE_HEIGHT := 1.3

## How long he stands about between strolls, once he's settled somewhere --
## indoors after moving in, or out on the street after being turned away.
const LOITER_MIN_SECONDS := 4.0
const LOITER_MAX_SECONDS := 11.0
## How far a stroll target may sit from wherever he's wandering. Indoors
## this is bounded by the room instead (see _indoor_spot).
const WANDER_RADIUS := 12.0
## Tries before giving up on finding somewhere to stroll to and just
## standing there another beat. Indoors most of the rejections are real
## (furniture-free but stair-and-partition-filled rooms), so this is worth
## more than a couple of goes.
const WANDER_ATTEMPTS := 10

## How far in front of the door he stops before walking through it, and how
## far past it he ends up. Two legs rather than one straight line to the
## middle of the room: the doorway is a narrow gap in a solid wall, and a
## direct line from where he's standing to a point inside almost never
## passes through it.
const DOOR_APPROACH_DISTANCE := 2.2
const DOOR_ENTRY_DEPTH := 3.0
## How close a door has to be to the building's own front to count as that
## building's door.
const DOOR_SEARCH_RADIUS := 12.0

## How far in from its own walls he'll stroll, and how much room a spot has
## to have around it to be worth strolling to.
##
## NOT the conservative front-centre strip the rest of this project uses for
## indoor placement (see civilian_population's own derivation, still quoted
## in tests/test_claim_building_check.gd). That strip is sized to be
## PROVABLY clear of the staircase and side room without looking, which is
## right for dropping someone in blind -- but on a small building it comes
## out about 2m deep, and he lands in the middle of it, so every "stroll"
## finished before it started. Measured: a real 9.5x10.4m building gave a
## 3.3x2.0m strip and 0.44m of movement in 30 seconds.
##
## So this takes the whole floor minus a wall margin, and establishes that a
## spot is actually usable by TESTING it rather than by assuming: real room
## for a body (INDOOR_CLEAR_RADIUS), and a clear run to it from where he's
## standing. Stairs and partition walls get rejected because they're
## genuinely in the way, not because of where they're expected to be.
const INDOOR_WALL_MARGIN := 1.2
const INDOOR_CLEAR_RADIUS := 0.5

var _stage: int = Stage.GREETING
## The building the player claimed, remembered so he can let himself in and
## then wander around inside it. Zero size means "no building yet".
var _home_center := Vector3.ZERO
var _home_size := Vector3.ZERO
## Remaining legs of the walk in through the front door (see MOVING_IN).
var _entry_legs: Array[Vector3] = []
## Counts down the stand-about beat between strolls.
var _loiter_timer := 0.0
## The player whose claim he's waiting on -- connected at the moment they
## accept, because by the time they actually claim a building they're long
## out of his trigger area and _nearby_player is null.
var _tutorial_player: Node3D = null

func _dialogue_nodes() -> Array:
	match _stage:
		Stage.AWAITING_CLAIM:
			# A reminder of the objective rather than the whole speech
			# again -- his own words, just the useful one.
			return [{"text": "You gotta open your chat box and type /claim once your inside the building you like"}]
		Stage.ARRIVED:
			# The payoff for the whole first quest: he approves of the
			# place, then asks to move in.
			return [
				{"text": "Ok good choice I like this building"},
				{
					"text": "By the way i'm homeless is it cool if I crash at your place for a bit",
					"options": [
						{"text": "Yes", "id": OPTION_LET_HIM_STAY},
						{"text": "No", "id": OPTION_TURN_HIM_AWAY},
					],
				},
			]
		Stage.TRAVELLING, Stage.MOVING_IN, Stage.LIVING_INSIDE, Stage.WANDERING:
			# Deliberately nothing. Catching him mid-walk, or once he's
			# moved in, or after he's wandered off, are all beats the
			# tutorial continues into in a later pass -- writing lines for
			# them now would mean inventing dialogue nobody asked for.
			return []
		Stage.DECLINED:
			# Straight back to the offer, so turning him down isn't final.
			return [_offer_node()]
		_:
			return [
				{"text": "Dude I just saw you fall from the sky"},
				_offer_node(),
			]

## The line that carries the two replies.
func _offer_node() -> Dictionary:
	return {
		"text": "I can teach you the ropes around here if you'd like",
		"options": [
			{"text": "Sure", "id": OPTION_ACCEPT},
			{"text": "I'll figure things out myself", "id": OPTION_DECLINE},
		],
	}

func _on_dialogue_option_chosen(id: String) -> void:
	if id == OPTION_ACCEPT:
		_stage = Stage.AWAITING_CLAIM
		_watch_for_claim()
		_continue_talking([
			{"text": "Alright well first you're gonna want to claim one of these buildings around here, most of them should be empty."},
			{"text": "You gotta open your chat box and type /claim once your inside the building you like"},
			{"text": "I'll meet you there, now off with you."},
		])
		return
	if id == OPTION_DECLINE:
		_stage = Stage.DECLINED
		_continue_talking([{"text": "Oh alright then"}])
		return
	if id == OPTION_LET_HIM_STAY:
		# No line for this one -- he just goes. The player said yes; what
		# happens next is him letting himself in, which the walk shows
		# better than a line of acknowledgement would.
		_begin_moving_in()
		return
	if id == OPTION_TURN_HIM_AWAY:
		_stage = Stage.WANDERING
		# The wandering itself starts when the box closes, not here -- see
		# _on_dialogue_finished. Walking off mid-sentence would pull him out
		# of earshot of his own last line.
		_continue_talking([{"text": "Oh alright then no problem, I'll see you around I'm gonna go wander around"}])

## Connected at ACCEPT time, not at claim time: the player has to be right
## in front of him to have accepted, whereas the claim itself happens
## wherever they wandered off to, with this node's own trigger area long
## since exited.
func _watch_for_claim() -> void:
	if not is_instance_valid(_nearby_player):
		return
	_tutorial_player = _nearby_player
	if not _tutorial_player.has_signal("building_claimed"):
		return
	if not _tutorial_player.building_claimed.is_connected(_on_player_claimed_building):
		# ONE_SHOT: he only turns up for the first building you take.
		_tutorial_player.building_claimed.connect(_on_player_claimed_building, CONNECT_ONE_SHOT)

## Sets off for the building the player just claimed -- he WALKS there under
## his own power (see unique_npc.gd's own walk_to) rather than appearing at
## it, so "I'll meet you there" means what it says.
##
## He heads for the FRONT of it, because that's where the way in is:
## city_chunk.gd puts every building's door on its +Z wall, so standing off
## that face is standing at the entrance rather than round the back against
## a blank wall.
##
## THE ONE COMPROMISE, and where MAX_WALK_SECONDS comes in: the city is
## infinite, so the building you claim can be arbitrarily far from wherever
## he was left standing. A genuine walk from there could be several minutes
## of the player waiting on an NPC crossing town. Past that budget he is
## staged in closer first -- but OUT OF SIGHT (see _staging_point_near), so
## what you see is still only ever him walking up.
func _on_player_claimed_building(center: Vector3, size: Vector3) -> void:
	if _stage != Stage.AWAITING_CLAIM:
		return
	_stage = Stage.TRAVELLING
	# Kept for the rest of the tutorial: letting himself in through its door
	# and pottering about its ground floor both need to know which building
	# this actually was.
	_home_center = center
	_home_size = size
	var outside := center + Vector3(0.0, ARRIVAL_DROP_HEIGHT, size.z * 0.5 + ARRIVAL_DOOR_CLEARANCE)

	if _flat_distance(global_position, outside) / WALK_SPEED > MAX_WALK_SECONDS:
		global_position = _staging_point_near(outside)
	velocity = Vector3.ZERO

	# Which way to turn once he gets there: back toward the door he's
	# standing outside of, so he reads as waiting for you to come out rather
	# than staring off down the street.
	var face := Vector3(center.x - outside.x, 0.0, center.z - outside.z)
	walk_to(outside, face)
	# ONE_SHOT: this is the only walk the tutorial ever asks of him.
	walk_finished.connect(_on_arrived_at_building, CONNECT_ONE_SHOT)

func _on_arrived_at_building() -> void:
	_stage = Stage.ARRIVED

## Turned away, so he wanders off -- started once the box has closed rather
## than the instant the reply is picked, so he delivers his last line before
## walking out of earshot of it.
func _on_dialogue_finished() -> void:
	if _stage == Stage.WANDERING and not is_walking_somewhere():
		_loiter_timer = 0.0

## Everything after "Yes": open the front door if it's shut, then walk in
## through it. TWO LEGS, not one -- see DOOR_APPROACH_DISTANCE. Without the
## approach leg he'd aim from wherever he was standing straight at a point
## inside the room, which for any door that isn't dead centre of the wall
## means aiming at solid wall and grinding along it.
func _begin_moving_in() -> void:
	_stage = Stage.MOVING_IN
	var door: Node3D = _door_near(_home_front_center())
	if door != null and door.has_method("open"):
		door.open()

	var entry := _door_entry_axis(door)
	var through: Vector3 = entry["through"]
	var inward: Vector3 = entry["inward"]
	_entry_legs = [
		through - inward * DOOR_APPROACH_DISTANCE, # stop just outside it
		through + inward * DOOR_ENTRY_DEPTH,       # and walk on through
	]
	_walk_next_entry_leg()

func _walk_next_entry_leg() -> void:
	if _entry_legs.is_empty():
		# He's in. From here on he lives here.
		_stage = Stage.LIVING_INSIDE
		_loiter_timer = randf_range(LOITER_MIN_SECONDS, LOITER_MAX_SECONDS)
		return
	var leg: Vector3 = _entry_legs.pop_front()
	walk_to(leg)
	walk_finished.connect(_walk_next_entry_leg, CONNECT_ONE_SHOT)

## Where the doorway is and which way is "in". Falls back to the building's
## own front-centre when there's no door node to be found -- a chunk that
## streamed out, or a test with no real city in it -- so moving in still
## works, it just walks at the middle of the wall instead of the opening.
func _door_entry_axis(door: Node3D) -> Dictionary:
	# Every building's door is on its +Z wall (see city_chunk.gd), so "in"
	# is -Z for all of them.
	var inward := Vector3(0.0, 0.0, -1.0)
	if door == null:
		return {"through": _home_front_center(), "inward": inward}
	# The door node sits at the opening's LEFT edge (its own u0), so the
	# middle of the gap is half a door-width along +X from it.
	var through: Vector3 = door.global_position + Vector3(CityChunk.DOOR_WIDTH * 0.5, 0.0, 0.0)
	return {"through": through, "inward": inward}

## The nearest registered door to `point`, or null. Doors hang off the chunk
## root rather than off any building (see building_door.gd's own DOOR_GROUP),
## so this matches by position rather than by parentage.
func _door_near(point: Vector3) -> Node3D:
	var nearest: Node3D = null
	var nearest_distance := DOOR_SEARCH_RADIUS
	for node in get_tree().get_nodes_in_group(BuildingDoor.DOOR_GROUP):
		if not is_instance_valid(node) or not (node is Node3D):
			continue
		var distance: float = _flat_distance((node as Node3D).global_position, point)
		if distance < nearest_distance:
			nearest_distance = distance
			nearest = node as Node3D
	return nearest

## The middle of the building's own front (+Z) wall.
func _home_front_center() -> Vector3:
	return _home_center + Vector3(0.0, 0.0, _home_size.z * 0.5)

## Somewhere to start the walk from when the real one would take longer than
## MAX_WALK_SECONDS. Wants three things at once, in priority order:
##
##  1. REAL GROUND under it, probed over a short vertical band so a point
##     over a basement stair hole or off a ledge finds nothing and is
##     skipped -- the same derived-not-searched approach the city's own
##     generator guarantees make possible.
##  2. OUT OF THE PLAYER'S VIEW. The whole point of staging is that the
##     player never sees it happen; landing one in frame would be exactly
##     the pop this change is meant to remove.
##  3. A CLEAR RUN to the destination. This is what keeps him out of
##     building interiors: a candidate inside one has a wall between it and
##     the door, so the line-of-sight cast fails and it's rejected. Local
##     steering rounds a building fine but gets trapped inside one.
##
## Each is dropped in turn rather than failing outright, because standing
## him somewhere slightly wrong beats leaving him on the far side of the
## city or dropping him into the void.
func _staging_point_near(destination: Vector3) -> Vector3:
	var camera: Camera3D = _player_camera()
	var grounded_only := Vector3.INF
	var grounded_with_sight := Vector3.INF

	for i in range(STAGING_CANDIDATES):
		var angle := TAU * float(i) / float(STAGING_CANDIDATES)
		var candidate := destination + Vector3(cos(angle), 0.0, sin(angle)) * STAGING_DISTANCE
		var spot := _ground_position_at(candidate)
		if spot == Vector3.INF:
			continue
		if grounded_only == Vector3.INF:
			grounded_only = spot
		if not _has_clear_run(spot, destination):
			continue
		if grounded_with_sight == Vector3.INF:
			grounded_with_sight = spot
		if camera == null or not camera.is_position_in_frustum(spot + Vector3.UP * STAGING_EYE_HEIGHT):
			return spot

	if grounded_with_sight != Vector3.INF:
		return grounded_with_sight
	if grounded_only != Vector3.INF:
		return grounded_only
	# Nothing anywhere round the ring had ground under it. Behind the
	# building is at least a real direction, and the walk's own timeout
	# stops him if it turns out to be unreachable.
	return destination - Vector3(0.0, 0.0, STAGING_DISTANCE)

## The local player's camera, for the visibility test. Duck-typed and fully
## optional -- a headless test has no camera at all, and staging still works
## without one (it just can't check what's on screen).
func _player_camera() -> Camera3D:
	var player: Node3D = _tutorial_player
	if not is_instance_valid(player):
		for node in get_tree().get_nodes_in_group("players"):
			player = node
			break
	if not is_instance_valid(player):
		return null
	return player.get("camera") as Camera3D

## Whether there's an unobstructed walk between two points, cast at chest
## height against world geometry only (layer 1, what city_chunk.gd puts
## every building on).
func _has_clear_run(from: Vector3, to: Vector3) -> bool:
	var a := from + Vector3.UP * STAGING_EYE_HEIGHT
	var b := Vector3(to.x, from.y + STAGING_EYE_HEIGHT, to.z)
	var query := PhysicsRayQueryParameters3D.create(a, b)
	query.collision_mask = 1
	query.exclude = [get_rid()]
	return get_world_3d().direct_space_state.intersect_ray(query).is_empty()

## The real floor under `point`, or INF if there isn't one in the probe
## band. Ignores `point`'s own Y -- it's a candidate on the XZ plane.
func _ground_position_at(point: Vector3) -> Vector3:
	var from := Vector3(point.x, point.y + STAGING_PROBE_UP, point.z)
	var to := Vector3(point.x, point.y - STAGING_PROBE_DOWN, point.z)
	var query := PhysicsRayQueryParameters3D.create(from, to)
	query.collision_mask = 1
	query.exclude = [get_rid()]
	var hit := get_world_3d().direct_space_state.intersect_ray(query)
	if hit.is_empty():
		return Vector3.INF
	# Dropped from just above and allowed to settle, same as ARRIVAL_DROP_HEIGHT.
	return (hit["position"] as Vector3) + Vector3(0.0, 0.15, 0.0)

func _flat_distance(a: Vector3, b: Vector3) -> float:
	return Vector2(a.x - b.x, a.z - b.z).length()

## Drives the settled-in life: standing about most of the time, strolling
## somewhere new now and then. Shared by both endings -- indoors once he's
## moved in, out on the street once he's been turned away -- because it's
## the same behaviour with a different set of places to stroll to.
func _physics_process(delta: float) -> void:
	super(delta)
	if _stage != Stage.LIVING_INSIDE and _stage != Stage.WANDERING:
		return
	# The walk itself is the base class's job; this only decides when to
	# start the next one.
	if is_walking_somewhere():
		return
	_loiter_timer -= delta
	if _loiter_timer > 0.0:
		return
	_loiter_timer = randf_range(LOITER_MIN_SECONDS, LOITER_MAX_SECONDS)
	var spot := _indoor_spot() if _stage == Stage.LIVING_INSIDE else _outdoor_spot()
	if spot != Vector3.INF:
		walk_to(spot)

## Somewhere to stroll to on his own ground floor -- inside the front-centre
## strip described at INDOOR_HALF_WIDTH_FRACTION, ground-probed so a spot
## over a basement stair hole is skipped. INF when nothing suitable came up,
## which simply means another beat of standing still.
func _indoor_spot() -> Vector3:
	if _home_size == Vector3.ZERO:
		return Vector3.INF
	var hx: float = maxf(_home_size.x * 0.5 - INDOOR_WALL_MARGIN, 0.5)
	var hz: float = maxf(_home_size.z * 0.5 - INDOOR_WALL_MARGIN, 0.5)
	for _attempt in range(WANDER_ATTEMPTS):
		var candidate := _home_center + Vector3(randf_range(-hx, hx), 0.0, randf_range(-hz, hz))
		var spot := _ground_position_at(candidate)
		if spot == Vector3.INF:
			continue
		# Real room for a body, and a real way to get there. Between them
		# these reject the staircase, the partition wall and anything behind
		# them -- by testing, not by assuming where they'll be.
		if not _is_clear(spot):
			continue
		if not _has_clear_run(global_position, spot):
			continue
		return spot
	return Vector3.INF

## Whether a body-sized sphere fits at `spot`, clear of world geometry.
## Cast at chest height rather than at the feet, so standing on a kerb or a
## floor seam doesn't read as being embedded in it.
func _is_clear(spot: Vector3) -> bool:
	var sphere := SphereShape3D.new()
	sphere.radius = INDOOR_CLEAR_RADIUS
	var query := PhysicsShapeQueryParameters3D.new()
	query.shape = sphere
	query.transform = Transform3D(Basis(), spot + Vector3.UP * STAGING_EYE_HEIGHT)
	query.collision_mask = 1
	query.exclude = [get_rid()]
	return get_world_3d().direct_space_state.intersect_shape(query, 1).is_empty()

## Somewhere to stroll to out on the street, for the turned-away ending.
## Ground-probed the same way, so he doesn't set off toward a rooftop edge
## or a hole.
func _outdoor_spot() -> Vector3:
	for _attempt in range(WANDER_ATTEMPTS):
		var angle := randf() * TAU
		var candidate := global_position + Vector3(cos(angle), 0.0, sin(angle)) * randf_range(3.0, WANDER_RADIUS)
		var spot := _ground_position_at(candidate)
		if spot != Vector3.INF:
			return spot
	return Vector3.INF

## Reset alongside the stage so a fresh conversation can re-arm it. Purely
## defensive -- nothing un-accepts the tutorial today.
func tutorial_stage() -> int:
	return _stage
