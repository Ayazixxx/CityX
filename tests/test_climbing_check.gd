extends Node3D
## Regression check for the new wall-climbing mechanic (see player.gd's own
## _try_start_climb/_process_climbing/CLIMB_* consts): hold sprint+W against
## a wall to grab on, W/S to climb up/down once attached, stamina drains at
## the same rate as sprinting, Space hops off with an outward push at any
## time, running out of stamina releases the grip WITHOUT a push, and
## reaching the top of a climbable wall plays the one-shot climb_top_out
## mantle and relocates the real physics body onto the ledge. Runs headless
## against real flat wall StaticBody3D fixtures (not a full CityChunk
## building -- the climb logic only cares about real collision at layer 1
## with a roughly-vertical normal, matching test_dash_iframes_check.gd's
## own "simple fixture, not the full city" precedent) and a real Player,
## driving _physics_process manually for deterministic deltas, same idiom
## every other player-fixture test here already uses.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

const TALL_WALL_HEIGHT := 30.0
const SHORT_WALL_HEIGHT := 4.0

var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _make_wall(height: float, x_offset: float, with_roof: bool = false) -> StaticBody3D:
	var wall := StaticBody3D.new()
	wall.collision_layer = 1
	wall.position = Vector3(x_offset, height / 2.0, -1.0)
	var col := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	shape.size = Vector3(10.0, height, 1.0)
	col.shape = shape
	wall.add_child(col)
	add_child(wall)

	if with_roof:
		# A real climbable building has a real roof at the top of its own
		# wall (see city_chunk.gd's own _add_roof_detail) for the mantle to
		# actually land ON -- without this, this bare test fixture's own
		# wall is just a vertical slab floating with nothing on top, so
		# normal gravity (which resumes the instant the mantle finishes)
		# would pull the player straight back down to the distant ground
		# plane instead of settling near the wall's own height, which isn't
		# what this check is actually testing.
		# Flush with the wall's own near face (z=-0.5) and extending BACK
		# from there (away from the climbing player), not past it -- a real
		# reported bug in an earlier version of this fixture: with the
		# roof's own near edge past z=-0.5, its underside physically
		# overlapped the column the player climbs straight up through,
		# wedging them against it (a real move_and_slide collision) well
		# before the top-of-wall raycast check ever got a chance to fire,
		# which read as "climbing just stalls partway up and never tops
		# out" -- not a real climbing-code bug, a fixture geometry one.
		var roof := StaticBody3D.new()
		roof.collision_layer = 1
		roof.position = Vector3(x_offset, height + 0.25, -3.5)
		var rcol := CollisionShape3D.new()
		var rshape := BoxShape3D.new()
		rshape.size = Vector3(10.0, 0.5, 6.0)
		rcol.shape = rshape
		roof.add_child(rcol)
		add_child(roof)
	return wall

func _make_ground() -> void:
	var ground := StaticBody3D.new()
	ground.collision_layer = 1
	var gcol := CollisionShape3D.new()
	var gshape := BoxShape3D.new()
	gshape.size = Vector3(200, 1.0, 200)
	gcol.shape = gshape
	gcol.position = Vector3(0, -0.5, 0)
	ground.add_child(gcol)
	add_child(ground)

func _make_player(x_offset: float) -> CharacterBody3D:
	var player: CharacterBody3D = PlayerScene.instantiate()
	add_child(player)
	var character := CharacterScene.instantiate()
	character.set_paintable(false)
	player.add_child(character)
	# 0.7m clear of the tall/short walls' own near face (z=-0.5), well
	# within CLIMB_GRAB_CHECK_DISTANCE (1.0) but not touching -- facing
	# rotation.y=0 (default forward, -Z) points straight at them.
	player.global_position = Vector3(x_offset, 0.05, 0.2)
	return player

func _ready() -> void:
	_make_ground()
	_make_wall(TALL_WALL_HEIGHT, 0.0)
	_make_wall(SHORT_WALL_HEIGHT, 20.0, true)
	call_deferred("_run_checks")

func _run_checks() -> void:
	await _check_grab_and_climb_up()
	await _check_hop_off()
	await _check_climb_down_to_ground()
	await _check_stamina_exhaustion_releases()
	await _check_top_out()

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

## Settles a fresh player onto the ground for a few frames (same reasoning
## test_dash_iframes_check.gd's own "5 physics ticks before asserting
## anything" warmup uses) so is_on_floor()/gravity are in a sane state
## before the climb-specific checks below start driving input.
func _settle(player: CharacterBody3D) -> void:
	for i in range(10):
		player._physics_process(1.0 / 60.0)

func _check_grab_and_climb_up() -> void:
	var player := _make_player(0.0)
	_settle(player)

	if player._is_climbing:
		_fail("player started climbing with no input at all")
		return

	Input.action_press("sprint")
	Input.action_press("move_forward")
	player._physics_process(1.0 / 60.0)
	Input.action_release("sprint")

	if not player._is_climbing:
		_fail("holding sprint+W against a real wall didn't start a climb")
		return
	print("PASS: holding sprint+W against a wall grabs on (_is_climbing=true)")

	var stamina_before: float = player.stamina
	var y_before: float = player.global_position.y
	# W alone (no sprint) continues climbing once already attached -- per
	# the explicit ask ("once your on press S to go down"), only the
	# INITIAL grab needs sprint held.
	for i in range(30):
		player._physics_process(1.0 / 60.0)

	if player.global_position.y > y_before + 0.5:
		print("PASS: holding W while attached climbs upward (y ", y_before, " -> ", player.global_position.y, ")")
	else:
		_fail("climbing up didn't meaningfully raise the player's own height (before=%s after=%s)" % [y_before, player.global_position.y])

	var expected_drain: float = player.STAMINA_DRAIN_PER_SEC * (30.0 / 60.0)
	if stamina_before - player.stamina > expected_drain * 0.5:
		print("PASS: climbing drains stamina (before=", stamina_before, " after=", player.stamina, ", expected~", expected_drain, ")")
	else:
		_fail("stamina didn't drain while climbing (before=%s after=%s)" % [stamina_before, player.stamina])

	if is_equal_approx(player.global_position.x, 0.0) and player.global_position.z < 0.5:
		print("PASS: player stayed glued to the wall's own grab spot (no horizontal drift)")
	else:
		_fail("player drifted horizontally while climbing (pos=%s)" % player.global_position)

	Input.action_release("move_forward")
	player.queue_free()

## Space's own WIRING (a single `if Input.is_action_just_pressed("jump"):
## _end_climb(character, true)` at the very top of _process_climbing) is
## simple enough to trust by inspection -- what actually needs checking is
## _end_climb()'s own push math. Calling it directly, rather than
## simulating a real Space press through Input, sidesteps the documented
## one-frame just_pressed lag entirely (is_action_just_pressed() only
## reflects a press that happened on a real ENGINE physics step, not one
## manually invoked via _physics_process() -- see test_punch_check.gd's
## own comment on this exact quirk) -- same "call the internal method
## directly instead of fighting real Input timing" choice
## test_lock_on_check.gd's own class comment already documents making for
## this identical class of fragility.
func _check_hop_off() -> void:
	var player := _make_player(0.0)
	_settle(player)
	Input.action_press("sprint")
	Input.action_press("move_forward")
	player._physics_process(1.0 / 60.0)
	Input.action_release("sprint")
	Input.action_release("move_forward")
	if not player._is_climbing:
		_fail("setup for hop-off check failed -- never grabbed the wall")
		player.queue_free()
		return

	player._end_climb(player._get_character(), true)

	if player._is_climbing:
		_fail("_end_climb(push=true) didn't clear _is_climbing")
	else:
		print("PASS: hopping off ends the climb")

	if player.velocity.z > 0.5 and player.velocity.y > 0.5:
		print("PASS: hopping off applies an outward + upward push (velocity=", player.velocity, ")")
	else:
		_fail("hopping off didn't apply a real outward/upward push (velocity=%s)" % player.velocity)

	player.queue_free()

func _check_climb_down_to_ground() -> void:
	var player := _make_player(0.0)
	_settle(player)
	Input.action_press("sprint")
	Input.action_press("move_forward")
	player._physics_process(1.0 / 60.0)
	Input.action_release("sprint")
	if not player._is_climbing:
		_fail("setup for climb-down check failed -- never grabbed the wall")
		player.queue_free()
		return
	# Climb up a bit first so there's real height to descend from.
	for i in range(20):
		player._physics_process(1.0 / 60.0)
	Input.action_release("move_forward")
	Input.action_press("move_back")
	# Enough real ticks to walk the climb speed back down to y=0 and detect
	# is_on_floor() -- generous upper bound, loop breaks the instant it
	# actually happens rather than relying on an exact frame count.
	var reached_ground := false
	for i in range(300):
		player._physics_process(1.0 / 60.0)
		if not player._is_climbing:
			reached_ground = true
			break
	Input.action_release("move_back")

	if reached_ground and is_equal_approx(player.velocity.length(), 0.0):
		print("PASS: climbing down to the ground releases cleanly (no push, velocity=", player.velocity, ")")
	else:
		_fail("climbing down never reached the ground / released cleanly (_is_climbing=%s velocity=%s pos=%s)" % [player._is_climbing, player.velocity, player.global_position])

	player.queue_free()

func _check_stamina_exhaustion_releases() -> void:
	var player := _make_player(0.0)
	_settle(player)
	Input.action_press("sprint")
	Input.action_press("move_forward")
	player._physics_process(1.0 / 60.0)
	Input.action_release("sprint")
	if not player._is_climbing:
		_fail("setup for stamina-exhaustion check failed -- never grabbed the wall")
		player.queue_free()
		return

	player.stamina = 0.05 # about to run out
	player._physics_process(1.0 / 60.0)

	if not player._is_climbing:
		print("PASS: running out of stamina releases the grip automatically")
	else:
		_fail("player kept climbing after stamina hit 0")

	if is_equal_approx(player.velocity.length(), 0.0):
		print("PASS: letting go from stamina exhaustion applies no deliberate push (velocity=", player.velocity, ")")
	else:
		_fail("stamina-exhausted release incorrectly applied a hop-off-style push (velocity=%s)" % player.velocity)

	Input.action_release("move_forward")
	player.queue_free()

## Grabs the SHORT wall and climbs straight to its own top edge -- confirms
## _is_climb_topping_out engages, the real one-shot climb_top_out clip
## actually plays, and once it finishes naturally (real animation_finished,
## not a directly-invoked handler -- same "wait for is_playing() to go
## false" idiom test_car_enter_exit_check.gd's own entering_car check
## already established) the real physics body ends up relocated near the
## top of the wall, not left hanging at its old climbing height.
func _check_top_out() -> void:
	var player := _make_player(20.0)
	_settle(player)
	Input.action_press("sprint")
	Input.action_press("move_forward")
	player._physics_process(1.0 / 60.0)
	Input.action_release("sprint")
	if not player._is_climbing:
		_fail("setup for top-out check failed -- never grabbed the short wall")
		player.queue_free()
		return

	var topped_out := false
	for i in range(200):
		player._physics_process(1.0 / 60.0)
		if player._is_climb_topping_out:
			topped_out = true
			break
	Input.action_release("move_forward")

	if topped_out:
		print("PASS: reaching the top of a climbable wall triggers the mantle (_is_climb_topping_out=true)")
	else:
		_fail("never reached the top of the short wall (pos=%s _is_climbing=%s)" % [player.global_position, player._is_climbing])
		player.queue_free()
		return

	var character := player.get_node("Character")
	if character.anim_player.current_animation != "climb_top_out":
		_fail("topping out didn't actually play the climb_top_out clip (playing '%s' instead)" % character.anim_player.current_animation)

	# Let the real ~1.13s one-shot clip finish naturally via the engine's
	# own automatic AnimationPlayer playback (this test's manual
	# _physics_process() calls above don't drive that -- it advances on its
	# own every real process frame regardless).
	var waited := 0
	while character.anim_player.is_playing() and waited < 300:
		await get_tree().process_frame
		waited += 1

	if not player._is_climb_topping_out and not player._is_climbing:
		print("PASS: the mantle finishing naturally hands control back (both climb flags clear)")
	else:
		_fail("climb flags didn't clear after climb_top_out finished (_is_climb_topping_out=%s _is_climbing=%s)" % [player._is_climb_topping_out, player._is_climbing])

	if player.global_position.y > SHORT_WALL_HEIGHT - 1.0:
		print("PASS: the real physics body ended up relocated near the top of the wall (y=", player.global_position.y, ", wall height=", SHORT_WALL_HEIGHT, ")")
	else:
		_fail("player wasn't relocated onto the ledge after topping out (y=%s, expected near %s)" % [player.global_position.y, SHORT_WALL_HEIGHT])

	player.queue_free()
