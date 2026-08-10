extends Node3D
## Regression check for claiming a building: "/claim" in chat while standing
## inside one, which plays a 360 orbit of the building, shows "BUILDING
## ACQUIRED", and moves the player's respawn point inside it.
##
## Built against a REAL generated chunk rather than a stand-in box. The
## whole feature hangs off city_chunk.gd's own indoor trigger Area3D --
## which building it reports, where its centre is, how big its shape is --
## and a hand-made Area3D would prove none of that still lines up.
##
## The orbit itself is checked for the properties that matter (a full turn,
## the building stays framed, the player's camera comes back) rather than by
## screenshot -- what it LOOKS like needs a person.

const CityChunk = preload("res://city/city_chunk.gd")
const PlayerScene := preload("res://player/player.tscn")
const ClaimCutsceneScript = preload("res://player/claim_cutscene.gd")

const CHUNK_SIZE := 24.0
const WORLD_SEED := 1337

var _ok := true
var _materials: Dictionary
var _orbit_finished := false

func _on_orbit_finished() -> void:
	_orbit_finished = true

## The claim stinger is a throwaway AudioStreamPlayer added as a child of
## the player and freed when it finishes (see player.gd's own
## _play_claim_sound).
##
## Matched by its STREAM, not by "some child is playing" -- the player owns
## several permanent audio nodes of its own (city ambience, footsteps) and
## the ambience autoplays, so a looser check reports a sound that fired
## before the claim ever happened. It did exactly that on the first run.
func _find_claim_sound(player: Node) -> AudioStreamPlayer:
	for child in player.get_children():
		if child is AudioStreamPlayer and (child as AudioStreamPlayer).stream == player.BuildingAcquiredAudio:
			return child
	return null

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	call_deferred("_run_checks")

func _run_checks() -> void:
	_materials = CityChunk.build_materials()
	await _check_claiming_outdoors_is_refused()
	await _check_claiming_inside_a_building_works()
	await _check_respawn_goes_to_the_claimed_building()
	await _check_orbit_completes_and_restores_the_camera()
	await _check_orbit_frames_the_whole_building()

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

func _chunk_seed(coord: Vector2i) -> int:
	return hash(Vector3i(coord.x, coord.y, WORLD_SEED))

## Generates real chunks until one has a building, and returns everything
## needed to stand inside it: the chunk node, the building's params, and the
## world-space point at the front-centre of its ground floor. That strip is
## the one region no staircase, side room or basement stair hole can reach:
## floor 0 always puts its staircase back-left and its carved-out side room
## back-right, and a basement's stair hole is front-RIGHT (see
## city_chunk.gd's own _stairwell_for_floor / _room_corner_for_floor /
## BASEMENT_STAIR_CORNER).
func _build_a_real_building() -> Dictionary:
	var coord := Vector2i(1, 1)
	for _attempt in 30:
		if not CityChunk.is_street_chunk(coord):
			var params: Dictionary = CityChunk.building_params_for_seed(_chunk_seed(coord), coord, CHUNK_SIZE)
			if params["has_building"]:
				var chunk: Node3D = CityChunk.generate_chunk(_chunk_seed(coord), coord, CHUNK_SIZE, _materials)
				chunk.position = Vector3(coord.x * CHUNK_SIZE, 0.0, coord.y * CHUNK_SIZE)
				add_child(chunk)
				var size: Vector3 = params["size"]
				var offset: Vector3 = params["offset"]
				var inside := chunk.position + offset + Vector3(0.0, 0.15, size.z * 0.5 - 1.8)
				return {"chunk": chunk, "size": size, "inside": inside, "center": chunk.position + offset}
		coord.x += 1
	return {}

func _make_player() -> CharacterBody3D:
	var player: CharacterBody3D = PlayerScene.instantiate()
	add_child(player)
	player.add_to_group("players")
	return player

## --- 1. Outdoors, /claim must refuse and say so, rather than silently
## claiming nothing or claiming thin air. ---
func _check_claiming_outdoors_is_refused() -> void:
	var player := _make_player()
	player.global_position = Vector3(0, 0.1, 0)
	await get_tree().physics_frame
	await get_tree().physics_frame

	var claimed: bool = player.try_claim_building()
	if claimed:
		_fail("/claim succeeded while standing outdoors")
	elif player.has_claimed_building():
		_fail("a refused claim still set a spawn point")
	elif player._in_claim_cutscene:
		_fail("a refused claim still started the orbit")
	elif _find_claim_sound(player) != null:
		_fail("a refused claim still played the acquisition sound")
	else:
		print("PASS: /claim outdoors is refused -- no spawn change, no orbit, no sound")

	player.queue_free()
	await get_tree().process_frame

## --- 2. THE feature: standing inside a real generated building, /claim
## takes it. Uses the actual indoor trigger, so this also proves
## city_chunk.gd is still handing the area to enter_building(). ---
func _check_claiming_inside_a_building_works() -> void:
	var building := _build_a_real_building()
	if building.is_empty():
		_fail("couldn't generate any chunk with a building -- the test setup is broken")
		return

	var player := _make_player()
	player.global_position = building["inside"]
	# Several frames: the Area3D has to actually register the overlap.
	for _i in range(6):
		await get_tree().physics_frame

	if player.current_building_area() == null:
		_fail("standing at the front-centre of a real building's ground floor, the player doesn't register as indoors")
		player.queue_free()
		building["chunk"].queue_free()
		return
	print("PASS: standing inside a real building registers which building it is")

	var claimed: bool = player.try_claim_building()
	if not claimed:
		_fail("/claim inside a building was refused")
	elif not player.has_claimed_building():
		_fail("a successful claim didn't set a spawn point")
	elif not player._in_claim_cutscene:
		_fail("a successful claim didn't start the reveal orbit")
	else:
		print("PASS: /claim inside a building succeeds, sets a spawn and starts the orbit")

	# The spawn must be where they actually stood, not the building's centre
	# or the world origin.
	var spawn: Vector3 = player.get_claimed_spawn_position()
	if spawn.distance_to(building["inside"]) > 1.0:
		_fail("the claimed spawn landed %.1fm from where the player was standing" % spawn.distance_to(building["inside"]))
	else:
		print("PASS: the claimed spawn is where the player was standing when they claimed it")

	# The acquisition stinger has to actually fire, and only on success --
	# a reward sound that plays on a refused claim is worse than none.
	var sfx := _find_claim_sound(player)
	if sfx == null:
		_fail("claiming a building never played building_acquired.mp3")
	elif not sfx.playing:
		_fail("the acquisition sound was created but isn't playing")
	else:
		print("PASS: claiming plays the BUILDING ACQUIRED sound")

	# Re-claiming the same building is a no-op, not a second orbit.
	var again: bool = player.try_claim_building()
	if again:
		_fail("claiming the same building twice succeeded again")
	else:
		print("PASS: re-claiming the same building is refused")

	player.queue_free()
	building["chunk"].queue_free()
	await get_tree().process_frame

## --- 3. "It sets your spawn to in there" -- the part that outlives the
## flourish. respawn() must use the claimed point INSTEAD of SpawnPoint. ---
func _check_respawn_goes_to_the_claimed_building() -> void:
	var building := _build_a_real_building()
	if building.is_empty():
		_fail("couldn't generate a building for the respawn check")
		return

	# A real SpawnPoint elsewhere, so "went to the claim" and "went to the
	# default" are genuinely distinguishable.
	var spawn_marker := Marker3D.new()
	spawn_marker.name = "SpawnPoint"
	spawn_marker.position = Vector3(0, 0.1, 0)
	add_child(spawn_marker)

	var player := _make_player()
	player.global_position = building["inside"]
	for _i in range(6):
		await get_tree().physics_frame

	if not player.try_claim_building():
		_fail("couldn't claim the building for the respawn check")
		player.queue_free()
		building["chunk"].queue_free()
		spawn_marker.queue_free()
		return

	var claimed_at: Vector3 = player.get_claimed_spawn_position()
	# Somewhere else entirely, then die and come back.
	player.global_position = Vector3(200, 0.1, 200)
	player.respawn()
	await get_tree().physics_frame

	var landed := player.global_position
	if landed.distance_to(Vector3(0, 0.1, 0)) < 2.0:
		_fail("respawn went to the world SpawnPoint, ignoring the claimed building")
	elif landed.distance_to(claimed_at) > 1.5:
		_fail("respawn landed %.1fm from the claimed building" % landed.distance_to(claimed_at))
	else:
		print("PASS: respawning puts the player back inside the building they claimed")

	player.queue_free()
	building["chunk"].queue_free()
	spawn_marker.queue_free()
	await get_tree().process_frame

## --- 4. The orbit runs a full turn and hands the view back. A cutscene
## that ends without restoring the player's camera leaves them looking at
## the building forever, which is worse than no cutscene. ---
func _check_orbit_completes_and_restores_the_camera() -> void:
	var player := _make_player()
	player.global_position = Vector3(0, 0.1, 0)
	await get_tree().physics_frame

	var cutscene: Node = ClaimCutsceneScript.new()
	cutscene.player = player
	cutscene.building_center = Vector3(0, 0, 0)
	cutscene.building_size = Vector3(14, 30, 14)
	add_child(cutscene)
	cutscene.begin()
	await get_tree().process_frame

	if player.camera.current:
		_fail("the orbit never took over from the player's own camera")

	# Sample the camera's angle around the building as the orbit runs, and
	# confirm it really goes all the way round rather than drifting a few
	# degrees. Summed as absolute deltas so a full turn is unambiguous.
	var swept := 0.0
	var previous := _camera_angle(cutscene)
	# A METHOD, not a lambda assigning to a local: GDScript lambdas capture
	# by value, so `finished = true` inside one never reaches the outer
	# variable and the wait always times out.
	_orbit_finished = false
	cutscene.finished.connect(_on_orbit_finished)
	# Generous frame budget: headless runs at roughly 7ms a frame, not 16, so
	# a 6-second orbit needs ~850 frames rather than the ~360 a 60fps guess
	# would suggest. Sized well past that so this can't fail on timing alone.
	for _i in range(1600):
		await get_tree().process_frame
		if _orbit_finished:
			break
		var now := _camera_angle(cutscene)
		var delta: float = wrapf(now - previous, -PI, PI)
		swept += absf(delta)
		previous = now

	if not _orbit_finished:
		_fail("the orbit never finished (ran %.2fs of its %.1fs, swept %.0f degrees)" % [cutscene._elapsed, cutscene.ORBIT_SECONDS, rad_to_deg(swept)])
	elif swept < TAU * 0.9:
		_fail("the orbit only swept %.0f degrees, not a full circle" % rad_to_deg(swept))
	else:
		print("PASS: the claim orbit sweeps a full %.0f degrees and ends" % rad_to_deg(swept))

	await get_tree().process_frame
	if not player.camera.current:
		_fail("the orbit finished without giving the player their camera back")
	else:
		print("PASS: the player's own camera is restored when the orbit ends")

	cutscene.queue_free()
	player.queue_free()
	await get_tree().process_frame

func _camera_angle(cutscene: Node) -> float:
	var cam: Camera3D = cutscene._camera
	if cam == null or not is_instance_valid(cam):
		return 0.0
	var offset: Vector3 = cam.global_position - cutscene.building_center
	return atan2(offset.z, offset.x)

## --- 5. Framing has to hold across the whole range of buildings this city
## actually generates (8m to 96m tall, see city_chunk.gd) -- a fixed radius
## would clip through a tower or lose a shop in the distance. ---
func _check_orbit_frames_the_whole_building() -> void:
	var player := _make_player()
	await get_tree().physics_frame

	var cases := [Vector3(9, 8, 9), Vector3(14, 30, 14), Vector3(18, 96, 18)]
	var failures: Array[String] = []
	for size in cases:
		var cutscene: Node = ClaimCutsceneScript.new()
		cutscene.player = player
		cutscene.building_center = Vector3.ZERO
		cutscene.building_size = size
		add_child(cutscene)
		cutscene.begin()
		await get_tree().process_frame

		var cam: Camera3D = cutscene._camera
		var horizontal := Vector2(cam.global_position.x, cam.global_position.z).length()
		var half_diagonal := Vector2(size.x, size.z).length() * 0.5

		# The real test is the bounding sphere: everything within `radius`
		# of the building's middle has to fall inside the frustum, from
		# whatever angle and elevation the camera happens to be at. That's
		# distance-to-CENTRE, not horizontal radius -- checking the latter
		# is what let a camera riding high above a 48m tower pass while
		# actually cropping its roof (seen directly in a render).
		var focus := Vector3(0.0, size.y * 0.5, 0.0)
		var sphere_radius := Vector2(half_diagonal, size.y * 0.5).length()
		var needed: float = sphere_radius / sin(deg_to_rad(cam.fov) * 0.5)
		var actual := cam.global_position.distance_to(focus)

		if horizontal <= half_diagonal:
			failures.append("%dm tall: camera is inside the %.1fm footprint" % [int(size.y), half_diagonal])
		elif actual < needed * 0.99:
			failures.append("%dm tall: camera %.1fm from centre, needs %.1fm to fit it in frame" % [int(size.y), actual, needed])

		cutscene.queue_free()
		await get_tree().process_frame

	if not failures.is_empty():
		_fail("the orbit doesn't frame these buildings: %s" % "; ".join(failures))
	else:
		print("PASS: the orbit frames buildings across the full 8m-96m height range")

	player.queue_free()
	await get_tree().process_frame
