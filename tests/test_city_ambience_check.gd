extends Node3D
## Regression check for the outdoor city ambience (see player.gd's own
## city_ambience_player/enter_building()/exit_building()/
## _fade_city_ambience()): loops while outdoors, fades out on entering a
## building's own footprint, fades back in on leaving it -- tracked via a
## counter (not a plain bool), so overlapping building footprints can't
## cancel each other out. Also covers city_chunk.gd's own per-building
## indoor-detection Area3D (see _build_building's own comment) end-to-end
## with a real physics-driven walk-through, and title_screen.gd's own
## fade_out_stars() (the title screen's persistent background music,
## stopped once main.gd's own _enter_world_with() funnel hands off to the
## player's own ambience -- see its own class comment).

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")
const CityChunk = preload("res://city/city_chunk.gd")
const TitleScreenScene := preload("res://title_screen/title_screen.tscn")

var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

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

func _make_player() -> CharacterBody3D:
	var player: CharacterBody3D = PlayerScene.instantiate()
	add_child(player)
	var character := CharacterScene.instantiate()
	character.set_paintable(false)
	player.add_child(character)
	return player

func _ready() -> void:
	_make_ground()
	call_deferred("_run_checks")

func _run_checks() -> void:
	await _check_ambience_autostarts_and_toggles()
	await _check_real_building_area_triggers_enter_exit()
	await _check_title_screen_fade_out_stars()

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

## The player's own city_ambience_player must start playing (faded in)
## immediately on spawn, must fade out and actually STOP once every
## building it's inside has been left (a counter, not a bool, so overlap
## can't cancel a still-valid "entered"), and must fade back in once the
## count returns to 0.
func _check_ambience_autostarts_and_toggles() -> void:
	var player := _make_player()
	if player.city_ambience_player.playing:
		print("PASS: city ambience starts playing immediately on spawn")
	else:
		_fail("city ambience wasn't playing right after the player spawned")

	# Entering the FIRST building starts the fade-out.
	player.enter_building()
	if player._indoor_areas.size() != 1:
		_fail("enter_building() didn't increment the indoor counter (count=%d)" % player._indoor_areas.size())

	# A SECOND, overlapping building (e.g. a wing massing footprint touching
	# the main building's own) must not double-trigger anything weird -- the
	# real point of using a counter instead of a bool.
	player.enter_building()
	if player._indoor_areas.size() != 2:
		_fail("a second enter_building() didn't increment further (count=%d)" % player._indoor_areas.size())

	# Wait out the real fade -- must actually reach silence and stop.
	# Entering (unlike leaving) uses the slower duration.
	await get_tree().create_timer(player.CITY_AMBIENCE_SLOW_FADE_SECONDS + 0.3).timeout
	if not player.city_ambience_player.playing and is_equal_approx(player.city_ambience_player.volume_db, -80.0):
		print("PASS: city ambience fades out and stops after entering (a) building(s)")
	else:
		_fail("city ambience didn't fully fade out/stop while indoors (playing=%s volume_db=%s)" % [player.city_ambience_player.playing, player.city_ambience_player.volume_db])

	# Leaving only ONE of the two overlapping buildings must NOT restart it
	# yet -- still inside the other one.
	player.exit_building()
	if player._indoor_areas.size() != 1:
		_fail("exit_building() didn't decrement the indoor counter (count=%d)" % player._indoor_areas.size())
	if player.city_ambience_player.playing:
		_fail("ambience restarted after leaving only ONE of two overlapping buildings")
	else:
		print("PASS: leaving one of two overlapping buildings doesn't restart the ambience yet")

	# Leaving the LAST one restarts it, fading back up to the (lowered,
	# see CITY_AMBIENCE_MAX_VOLUME_DB's own comment) full outdoor volume --
	# using the FASTER fade-in duration (a real explicit ask: "make it fade
	# in quicker upon leaving indoors").
	player.exit_building()
	if player._indoor_areas.size() != 0:
		_fail("final exit_building() didn't bring the indoor counter back to 0 (count=%d)" % player._indoor_areas.size())
	await get_tree().create_timer(player.CITY_AMBIENCE_FAST_FADE_SECONDS + 0.3).timeout
	if player.city_ambience_player.playing and is_equal_approx(player.city_ambience_player.volume_db, player.CITY_AMBIENCE_MAX_VOLUME_DB):
		print("PASS: city ambience restarts and fades back up to full (lowered) volume, quickly, after leaving the last building")
	else:
		_fail("city ambience didn't restart/fade back up after leaving every building (playing=%s volume_db=%s)" % [player.city_ambience_player.playing, player.city_ambience_player.volume_db])

	# The counter can never go negative from a stray/extra exit signal.
	player.exit_building()
	if player._indoor_areas.size() == 0:
		print("PASS: an extra exit_building() call doesn't drive the counter negative")
	else:
		_fail("an extra exit_building() call left the counter at %d instead of clamping to 0" % player._indoor_areas.size())

	# Direct assertions on the two tuning values themselves (not just their
	# downstream effect above) -- both real explicit asks.
	if player.CITY_AMBIENCE_MAX_VOLUME_DB < -2.0 and player.CITY_AMBIENCE_MAX_VOLUME_DB > -5.0:
		print("PASS: the outdoor ambience's own max volume is meaningfully lowered from unity gain (", player.CITY_AMBIENCE_MAX_VOLUME_DB, " dB, ~30% quieter)")
	else:
		_fail("CITY_AMBIENCE_MAX_VOLUME_DB (%s) isn't in the expected ~-3.1 dB (30%% quieter) range" % player.CITY_AMBIENCE_MAX_VOLUME_DB)
	if player.CITY_AMBIENCE_FAST_FADE_SECONDS < player.CITY_AMBIENCE_SLOW_FADE_SECONDS:
		print("PASS: leaving a building fades the ambience back in faster than entering one fades it out")
	else:
		_fail("CITY_AMBIENCE_FAST_FADE_SECONDS (%s) isn't actually faster than CITY_AMBIENCE_SLOW_FADE_SECONDS (%s)" % [player.CITY_AMBIENCE_FAST_FADE_SECONDS, player.CITY_AMBIENCE_SLOW_FADE_SECONDS])

## End-to-end: a real building (built via CityChunk._build_building, same
## as test_building_glass_check.gd's own convention) must have a real
## Area3D that actually fires enter_building()/exit_building() on a real
## Player teleported through it -- physics-server overlap detection only
## picks up a teleport on the NEXT physics step (same reasoning
## test_glass_bottle_check.gd's own whiff-check comment already documents),
## so this steps real physics_frames after each teleport rather than
## checking the very same frame.
func _check_real_building_area_triggers_enter_exit() -> void:
	var materials := CityChunk.build_materials()
	var building_offset := Vector3(200, 0, 200) # well clear of the player-fixture ground/other checks above
	var size := Vector3(16, 20, 16)
	CityChunk._build_building(self, size, building_offset, "", materials)

	var indoor_area: Area3D = null
	for child in get_children():
		if child is Area3D:
			indoor_area = child
	if indoor_area == null:
		_fail("no indoor-detection Area3D found as a child of the chunk root after _build_building()")
		return

	var player := _make_player()
	player.global_position = building_offset + Vector3(0, 0.05, 30.0) # well outside
	for i in range(3):
		await get_tree().physics_frame
	if player._indoor_areas.size() != 0:
		_fail("player started with a nonzero indoor count while standing well outside any building (count=%d)" % player._indoor_areas.size())

	player.global_position = building_offset + Vector3(0, 1.0, 0) # dead center, inside the footprint
	for i in range(3):
		await get_tree().physics_frame
	if player._indoor_areas.size() == 1:
		print("PASS: a real building's own indoor-detection Area3D fires enter_building() when the player walks into its footprint")
	else:
		_fail("walking into a real building's footprint didn't increment the indoor counter (count=%d)" % player._indoor_areas.size())

	player.global_position = building_offset + Vector3(0, 0.05, 30.0) # back outside
	for i in range(3):
		await get_tree().physics_frame
	if player._indoor_areas.size() == 0:
		print("PASS: leaving a real building's footprint fires exit_building() and brings the indoor count back to 0")
	else:
		_fail("leaving a real building's footprint didn't decrement the indoor counter back to 0 (count=%d)" % player._indoor_areas.size())

## title_screen.gd's own fade_out_stars() (see main.gd's own _enter_world_with,
## which calls this once right when entering a world) must fade the
## persistent background music out and actually stop it -- same shape as
## its own pre-existing fade_out_rain(), checked directly since there's no
## full main.gd screen-flow fixture here to drive the real transition.
func _check_title_screen_fade_out_stars() -> void:
	var title_screen: Control = TitleScreenScene.instantiate()
	add_child(title_screen)
	if not title_screen.stars_player.playing:
		_fail("stars_player wasn't already playing after the title screen's own _ready() -- can't verify fade_out_stars() meaningfully")
		return
	title_screen.fade_out_stars()
	await get_tree().create_timer(title_screen.RAIN_FADE_OUT_SECONDS + 0.3).timeout
	if not title_screen.stars_player.playing and is_equal_approx(title_screen.stars_player.volume_db, -80.0):
		print("PASS: title_screen.gd's own fade_out_stars() fades the background music out and stops it")
	else:
		_fail("fade_out_stars() didn't fully fade out/stop stars_player (playing=%s volume_db=%s)" % [title_screen.stars_player.playing, title_screen.stars_player.volume_db])
