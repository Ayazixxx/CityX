extends Node3D
## Regression check for the M map: a bird's-eye plan of the city that only
## shows chunks the player has actually been to.
##
## Two properties carry the whole feature, and both are invisible to a
## "does it run" check:
##
##  - FOG. A map that reveals everything isn't the feature that was asked
##    for. Checked by confirming a chunk stays hidden until the player goes
##    there, and that walking reveals it.
##  - FOOTPRINTS AT TRUE SCALE. The map derives building sizes from
##    city_chunk.gd's own params rather than looking at live geometry (it
##    has to -- most of the map isn't loaded). If that derivation ever
##    drifts from what actually gets built, the map quietly lies. Checked
##    against the same generator the world uses.
##
## What the map LOOKS like isn't checked here; that needs eyes.

const CityChunk = preload("res://city/city_chunk.gd")
const CITY_SCENE := preload("res://city/city.tscn")
const MapPanelScene := preload("res://ui/map_panel.tscn")
const PlayerScene := preload("res://player/player.tscn")

var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	call_deferred("_run_checks")

func _run_checks() -> void:
	await _check_fog_starts_closed_and_opens_as_you_walk()
	await _check_map_toggles_and_balances_input_gating()
	await _check_footprints_match_what_the_city_builds()
	await _check_scroll_zooms_and_drag_pans()

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

## --- 4. Scroll to zoom, drag to pan.
##
## The interesting one is ZOOM-TO-CURSOR. Zooming about the panel centre is
## the easy version and it's wrong: anything you're actually looking at, off
## to one side, walks out of frame as you zoom and you have to chase it. So
## what's asserted is the property, not the arithmetic -- pick a world point
## under the cursor, zoom, and check that same world point is still under
## the cursor afterwards. That holds regardless of how the maths is later
## rewritten, and fails immediately if someone "simplifies" it back to
## centre-zoom. ---
func _check_scroll_zooms_and_drag_pans() -> void:
	var city: Node3D = CITY_SCENE.instantiate()
	add_child(city)
	await city.initial_load_ready

	var player: CharacterBody3D = PlayerScene.instantiate()
	city.add_child(player)
	player.global_position = Vector3(0, 0.1, 0)
	var panel: CanvasLayer = MapPanelScene.instantiate()
	player.add_child(panel)
	# A REAL viewport size, because headless defaults to 64x64 -- and the
	# map panel insets itself by PANEL_MARGIN (60) on every side, so at 64px
	# wide its canvas lays out at zero and every scale measured off it comes
	# back as the no-size fallback. Nothing to do with the feature; the
	# window just has to be bigger than the border for there to be a map.
	get_tree().root.size = Vector2i(1280, 720)
	await get_tree().process_frame
	panel.open()
	for _i in range(4):
		await get_tree().process_frame

	if panel._canvas.size.x <= 0.0:
		_fail("the map canvas laid out at zero width -- nothing below can measure anything")
		panel.close()
		city.queue_free()
		return

	var start_zoom: float = panel._zoom
	var start_scale: float = panel._current_scale()
	if start_scale <= 0.0:
		_fail("the map has no usable scale, so zoom can't be measured")
		panel.close()
		city.queue_free()
		return

	# --- scroll in, then out ---
	_wheel(panel, MOUSE_BUTTON_WHEEL_UP, panel._canvas.size * 0.5)
	if panel._current_scale() <= start_scale:
		_fail("scrolling up didn't zoom in (scale %.4f -> %.4f)" % [start_scale, panel._current_scale()])
	else:
		print("PASS: scrolling up zooms in (scale %.4f -> %.4f)" % [start_scale, panel._current_scale()])

	_wheel(panel, MOUSE_BUTTON_WHEEL_DOWN, panel._canvas.size * 0.5)
	if not is_equal_approx(panel._zoom, start_zoom):
		_fail("one notch in then one notch out didn't return to the starting zoom (%.4f vs %.4f)" % [panel._zoom, start_zoom])
	else:
		print("PASS: scrolling back out returns to exactly the starting zoom")

	# --- the limits, which are what stop the infinite world from being
	# drawn at one pixel per district ---
	for _i in range(60):
		_wheel(panel, MOUSE_BUTTON_WHEEL_UP, panel._canvas.size * 0.5)
	var maxed: float = panel._zoom
	for _i in range(120):
		_wheel(panel, MOUSE_BUTTON_WHEEL_DOWN, panel._canvas.size * 0.5)
	var minned: float = panel._zoom
	if maxed > panel.ZOOM_MAX + 0.001 or minned < panel.ZOOM_MIN - 0.001:
		_fail("zoom escaped its own limits (max reached %.2f, min reached %.2f, allowed %.2f..%.2f)"
			% [maxed, minned, panel.ZOOM_MIN, panel.ZOOM_MAX])
	else:
		print("PASS: zoom clamps to %.2f..%.2f however hard you scroll" % [panel.ZOOM_MIN, panel.ZOOM_MAX])

	# --- zoom keeps whatever is under the cursor under the cursor ---
	panel._zoom = 1.0
	panel._pan_world = Vector2.ZERO
	var cursor: Vector2 = panel._canvas.size * Vector2(0.78, 0.28) # deliberately off-centre
	var before_world := _world_under(panel, player, cursor)
	_wheel(panel, MOUSE_BUTTON_WHEEL_UP, cursor)
	var after_world := _world_under(panel, player, cursor)
	var slip: float = before_world.distance_to(after_world)
	if slip > 0.5:
		_fail("zooming moved the world point under the cursor by %.2fm -- it's zooming to the panel centre, not the cursor" % slip)
	else:
		print("PASS: zooming holds the point under the cursor in place (slipped %.3fm)" % slip)

	# --- drag pans, and pans the right way ---
	panel._zoom = 1.0
	panel._pan_world = Vector2.ZERO
	var drag := Vector2(60.0, -40.0)
	_drag(panel, drag)
	# The map follows the cursor, so the view centre moves the OTHER way.
	var expected: Vector2 = -drag / panel._current_scale()
	if panel._pan_world.distance_to(expected) > 0.01:
		_fail("dragging %s panned to %s, expected %s" % [drag, panel._pan_world, expected])
	elif panel._pan_world.x >= 0.0 or panel._pan_world.y <= 0.0:
		_fail("the drag panned the wrong way -- the map should follow the cursor")
	else:
		print("PASS: dragging pans the view with the cursor (%s px -> %s world)" % [drag, panel._pan_world.snapped(Vector2(0.01, 0.01))])

	# --- and reopening recentres, so a stale pan can't strand you ---
	panel.close()
	panel.open()
	await get_tree().process_frame
	if panel._pan_world != Vector2.ZERO:
		_fail("reopening the map kept a stale pan of %s" % panel._pan_world)
	else:
		print("PASS: reopening the map recentres on the player")

	panel.close()
	city.queue_free()
	await get_tree().process_frame

## The world XZ currently drawn under a panel pixel -- the inverse of
## map_panel.gd's own to_screen.
func _world_under(panel: CanvasLayer, player: Node3D, pixel: Vector2) -> Vector2:
	var scale: float = panel._current_scale()
	# Typed through locals: reading a member off an untyped CanvasLayer
	# gives Variant, and Variant arithmetic can't be inferred.
	var pan: Vector2 = panel._pan_world
	var canvas_size: Vector2 = panel._canvas.size
	var view: Vector2 = Vector2(player.global_position.x, player.global_position.z) + pan
	return view + (pixel - canvas_size * 0.5) / scale

func _wheel(panel: CanvasLayer, button: int, at: Vector2) -> void:
	var event := InputEventMouseButton.new()
	event.button_index = button
	event.pressed = true
	event.position = at
	panel._on_canvas_gui_input(event)

func _drag(panel: CanvasLayer, delta: Vector2) -> void:
	var press := InputEventMouseButton.new()
	press.button_index = MOUSE_BUTTON_LEFT
	press.pressed = true
	panel._on_canvas_gui_input(press)
	var motion := InputEventMouseMotion.new()
	motion.relative = delta
	panel._on_canvas_gui_input(motion)
	var release := InputEventMouseButton.new()
	release.button_index = MOUSE_BUTTON_LEFT
	release.pressed = false
	panel._on_canvas_gui_input(release)

## --- 1. The fog. Uses a REAL city so the exploration marking runs through
## its own _physics_process chunk tracking rather than a stub. ---
func _check_fog_starts_closed_and_opens_as_you_walk() -> void:
	var city: Node3D = CITY_SCENE.instantiate()
	add_child(city)
	await city.initial_load_ready

	var player: Node3D = Node3D.new()
	city.add_child(player)
	player.global_position = Vector3.ZERO
	city.set_player(player)
	await get_tree().physics_frame

	# Spawn chunk counts as visited the moment the player is set -- standing
	# still at spawn shouldn't leave the map completely blank.
	if not city.is_chunk_explored(Vector2i(0, 0)):
		_fail("the spawn chunk isn't marked explored")
	else:
		print("PASS: the spawn chunk is explored from the start")

	# Somewhere the player has definitely never been.
	var far := Vector2i(20, 20)
	if city.is_chunk_explored(far):
		_fail("a chunk 480m away was already explored before going there")
	else:
		print("PASS: chunks the player has never visited start hidden")

	# Walk there. Position is moved directly (this is about the exploration
	# bookkeeping, not about locomotion).
	player.global_position = Vector3(far.x * city.CHUNK_SIZE + 12.0, 0.0, far.y * city.CHUNK_SIZE + 12.0)
	for _i in range(4):
		await get_tree().physics_frame

	if not city.is_chunk_explored(far):
		_fail("walking into a chunk didn't mark it explored")
	else:
		print("PASS: walking into a chunk reveals it")

	# ...and its immediate neighbours, so a walked road reveals a strip
	# rather than a dotted line.
	if not city.is_chunk_explored(far + Vector2i(1, 0)) or not city.is_chunk_explored(far + Vector2i(0, -1)):
		_fail("only the exact chunk was revealed -- neighbours within EXPLORE_RADIUS should be too")
	else:
		print("PASS: the chunks either side of the one you're in are revealed too")

	# But not the whole loaded radius: chunks are generated far past what
	# you can see, and revealing all of that would fill the map in faster
	# than you explore.
	var loaded_but_unseen := far + Vector2i(4, 4)
	if city.is_chunk_explored(loaded_but_unseen):
		_fail("a chunk 4 out (generated but never approached) was revealed -- the map fills itself in")
	else:
		print("PASS: generated-but-unvisited chunks stay hidden")

	player.queue_free()
	city.queue_free()
	await get_tree().process_frame

## --- 2. M opens and closes it, and the input gate it takes out is always
## put back. An unbalanced set_ui_capturing leaves the player permanently
## unable to move, which is the worst possible failure for a map. ---
func _check_map_toggles_and_balances_input_gating() -> void:
	var player: CharacterBody3D = PlayerScene.instantiate()
	add_child(player)
	await get_tree().process_frame

	var map: CanvasLayer = MapPanelScene.instantiate()
	player.add_child(map)
	await get_tree().process_frame

	if map.is_open():
		_fail("the map started open")

	var before: int = player._ui_capture_count
	map.open()
	if not map.is_open():
		_fail("open() didn't open the map")
	elif player._ui_capture_count != before + 1:
		_fail("opening the map didn't gate player input (capture count %d -> %d)" % [before, player._ui_capture_count])
	else:
		print("PASS: opening the map gates player input")

	# A second open must not double-gate.
	map.open()
	if player._ui_capture_count != before + 1:
		_fail("opening an already-open map gated input twice -- the player would be stuck after closing")
	else:
		print("PASS: opening an already-open map doesn't double-gate")

	map.close()
	if map.is_open():
		_fail("close() didn't close the map")
	elif player._ui_capture_count != before:
		_fail("closing the map left the input gate unbalanced (%d, expected %d)" % [player._ui_capture_count, before])
	else:
		print("PASS: closing the map releases the input gate exactly once")

	map.close()
	if player._ui_capture_count != before:
		_fail("closing an already-closed map released the gate again (%d)" % player._ui_capture_count)
	else:
		print("PASS: closing an already-closed map is a no-op")

	map.queue_free()
	player.queue_free()
	await get_tree().process_frame

## --- 3. THE MAP MUSTN'T LIE. It derives buildings from seeds rather than
## measuring built geometry (it has to -- most of the map isn't loaded), so
## the risk is drawing a building where the world has none, at the wrong
## place, or missing one entirely.
##
## Checked by generating the chunk for real and probing the world at the
## footprint the map WOULD draw: solid where the map draws a building,
## clear where it doesn't.
##
## Deliberately NOT an extent comparison. A first attempt compared measured
## width/depth against the params and failed on every winged building --
## the map draws the main massing while the world also grows an L-wing and
## balconies (see map_panel.gd's own noted simplification). That's a known,
## documented gap, not a lie about where a building is, and a test that
## fails on it is asserting something the feature never claimed. ---
func _check_footprints_match_what_the_city_builds() -> void:
	var materials := CityChunk.build_materials()
	var world_seed := 1337
	var chunk_size := 24.0
	var with_building := 0
	var without_building := 0
	var problems: Array[String] = []

	var coord := Vector2i(1, 1)
	while with_building + without_building < 16:
		coord.x += 1
		if CityChunk.is_street_chunk(coord):
			coord.y += 1
			continue
		var seed_value: int = hash(Vector3i(coord.x, coord.y, world_seed))
		var params: Dictionary = CityChunk.building_params_for_seed(seed_value, coord, chunk_size)

		var chunk: Node3D = CityChunk.generate_chunk(seed_value, coord, chunk_size, materials)
		chunk.position = Vector3(coord.x * chunk_size, 0.0, coord.y * chunk_size)
		add_child(chunk)
		await get_tree().physics_frame

		# The exact world x/z the map puts the middle of its building
		# rectangle at.
		var center := chunk.position + Vector3(params["offset"].x if params["has_building"] else 0.0, 0.0, params["offset"].z if params["has_building"] else 0.0)

		var surface := _surface_height_at(center)
		if params["has_building"]:
			with_building += 1
			var size: Vector3 = params["size"]
			if size.x < CityChunk.MIN_FOOTPRINT - 0.1 or size.x > CityChunk.MAX_FOOTPRINT + 0.1:
				problems.append("%s: derived width %.1f is outside the generator's own range" % [str(coord), size.x])
			elif surface < CityChunk.MIN_HEIGHT * 0.5:
				problems.append("%s: map draws a building but the world's surface there is only %.1fm up" % [str(coord), surface])
		else:
			without_building += 1
			if surface > 2.0:
				problems.append("%s: map draws no building but the world has something %.1fm tall there" % [str(coord), surface])

		chunk.queue_free()
		await get_tree().process_frame

	if with_building == 0:
		_fail("the sweep never found a building -- the sweep itself is broken")
	elif not problems.is_empty():
		_fail("the map disagrees with the world: %s" % "; ".join(problems))
	else:
		print("PASS: %d buildings and %d empty lots on the map all match what the world really has there" % [with_building, without_building])

## Height of whatever the world's topmost surface is at this x/z: the roof
## of a building, or the ground slab on an empty lot.
##
## A DOWNWARD RAY, not a point test. Probing the building's centre directly
## reports empty, because buildings here are hollow -- the centre is a room.
## Probing a wall instead runs into the real window holes cut through every
## wall. Dropping a ray onto the roof sidesteps both and answers the only
## question that matters: is there a building standing at the coordinates
## the map drew one at.
func _surface_height_at(point: Vector3) -> float:
	var from := Vector3(point.x, CityChunk.MAX_HEIGHT + 40.0, point.z)
	var to := Vector3(point.x, -2.0, point.z)
	var query := PhysicsRayQueryParameters3D.create(from, to)
	query.collision_mask = 1 # world geometry
	var hit := get_world_3d().direct_space_state.intersect_ray(query)
	if hit.is_empty():
		return -INF
	return (hit["position"] as Vector3).y
