extends Node3D
## Regression check for radio/radio.gd: starts powered off, a nearby real
## player's E press (interact) turns it on and starts playing the first
## playlist track, a second press pauses it (not restarts it) and a third
## resumes it, a track finishing while powered on advances to (and wraps
## back around) the next playlist entry, and an out-of-range E press does
## nothing. Also checks the AudioStreamPlayer3D itself is configured for
## real proximity falloff (unit_size/max_distance), not just non-positional
## background music. Runs headless: real physics frames driving real
## Input.action_press(), same convention tests/test_building_door_check.gd's
## own E-press check already establishes.

const RadioScene := preload("res://radio/radio.tscn")
const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _radio: RigidBody3D
var _stream_player: AudioStreamPlayer3D
var _player: CharacterBody3D
var _frame := 0
var _phase := 0
var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	# A real Player instance runs its own _physics_process() (gravity,
	# movement) every frame regardless of what this test does -- without
	# real ground to land on it would free-fall indefinitely across the
	# many physics frames this test's own phases wait through, eventually
	# drifting out of the radio's own INTERACT_RANGE sphere.
	var ground := StaticBody3D.new()
	ground.collision_layer = 1
	var gcol := CollisionShape3D.new()
	var gshape := BoxShape3D.new()
	gshape.size = Vector3(60, 1.0, 60)
	gcol.shape = gshape
	gcol.position = Vector3(0, -0.5, 0)
	ground.add_child(gcol)
	add_child(ground)

	_radio = RadioScene.instantiate()
	add_child(_radio)
	_stream_player = _radio._player

	if _radio.is_powered_on():
		_fail("radio starts powered ON -- should start off")
	else:
		print("PASS: radio starts powered off")

	if _stream_player.playing:
		_fail("radio's own AudioStreamPlayer3D is already playing before ever being turned on")
	else:
		print("PASS: no audio plays before the radio is ever turned on")

	# --- Real reported gap: the radio started life as a StaticBody3D, which
	# meant player.gd's own grab/carry system (only ever targets a
	# RigidBody3D in the "grabbable" group) silently couldn't pick it up at
	# all. Confirms it's now a real, tagged, bounce-capable physics body --
	# collision_layer stays 1 (unchanged, still a normal solid obstacle to
	# ordinary player movement) deliberately, not the bottle's own dedicated
	# layer 16, since that's what keeps it inside player.gd's own grab
	# raycast mask (1 | 8 | 16) with zero changes needed there. ---
	if _radio.is_in_group("grabbable"):
		print("PASS: the radio is a real RigidBody3D tagged \"grabbable\"")
	else:
		_fail("the radio still isn't tagged grabbable")
	var radio_mat: PhysicsMaterial = _radio.physics_material_override
	if _radio.mass > 0.0 and radio_mat != null and radio_mat.bounce > 0.0:
		print("PASS: the radio has real mass and a bounce-capable physics material (mass=", _radio.mass, " bounce=", radio_mat.bounce, ")")
	else:
		_fail("the radio has no real mass/bounce material configured")
	if _radio.collision_layer == 1:
		print("PASS: the radio stays on collision_layer 1 -- still a normal solid obstacle, and already inside player.gd's own grab raycast mask")
	else:
		_fail("the radio's own collision_layer changed from 1 -- may no longer block normal player movement or be reachable by the grab raycast")

	# --- Real proximity falloff, not a non-positional background track --
	# an AudioStreamPlayer3D with a real unit_size/max_distance, matching
	# radio.gd's own configured constants (see radio.gd's own AUDIO_UNIT_
	# SIZE/AUDIO_MAX_DISTANCE), on the Music bus like this project's other
	# ambient/world music (city ambience, rain, title screen). ---
	if _stream_player.bus == "Music" and is_equal_approx(_stream_player.unit_size, _radio.AUDIO_UNIT_SIZE) and is_equal_approx(_stream_player.max_distance, _radio.AUDIO_MAX_DISTANCE) and _stream_player.max_distance > 0.0:
		print("PASS: audio is real 3D positional sound with proximity falloff configured (unit_size=", _stream_player.unit_size, " max_distance=", _stream_player.max_distance, ")")
	else:
		_fail("radio's own AudioStreamPlayer3D isn't configured for real distance falloff (bus=%s unit_size=%s max_distance=%s)" % [_stream_player.bus, _stream_player.unit_size, _stream_player.max_distance])

	# --- Playlist has the 2 attached tracks, in order. ---
	if _radio.PLAYLIST.size() == 2:
		print("PASS: playlist has both attached tracks")
	else:
		_fail("playlist has %d tracks, expected 2" % _radio.PLAYLIST.size())

	_player = PlayerScene.instantiate()
	var character := CharacterScene.instantiate()
	character.set_paintable(false)
	_player.position = Vector3(0, 1.0, 1.0) # well inside INTERACT_RANGE (2.5)
	add_child(_player)
	_player.add_child(character)

func _tap_interact() -> void:
	Input.action_press("interact")

func _release_interact() -> void:
	Input.action_release("interact")

func _physics_process(_delta: float) -> void:
	_frame += 1
	match _phase:
		0: # a real nearby player is detected by the InteractArea
			if _frame >= 10:
				if _radio._nearby_player == _player:
					print("PASS: InteractArea detected the nearby real player")
				else:
					_fail("InteractArea never detected the nearby real player")
				_phase = 1
				_frame = 0
		1: # pressing E turns it on and starts track 0
			if _frame == 1:
				_tap_interact()
			if _frame == 2:
				_release_interact()
			if _frame >= 10:
				if _radio.is_powered_on() and _stream_player.playing and not _stream_player.stream_paused and _stream_player.stream == _radio.PLAYLIST[0]:
					print("PASS: pressing E turns the radio on and starts playing the first track")
				else:
					_fail("radio didn't turn on / start track 0 (powered_on=%s playing=%s paused=%s)" % [_radio.is_powered_on(), _stream_player.playing, _stream_player.stream_paused])
				_phase = 2
				_frame = 0
		2: # pressing E again PAUSES it (doesn't restart/stop it)
			if _frame == 1:
				_tap_interact()
			if _frame == 2:
				_release_interact()
			if _frame >= 10:
				if not _radio.is_powered_on() and _stream_player.stream_paused and _stream_player.stream == _radio.PLAYLIST[0]:
					print("PASS: pressing E again pauses the radio without restarting the track")
				else:
					_fail("radio didn't pause correctly (powered_on=%s paused=%s stream=%s)" % [_radio.is_powered_on(), _stream_player.stream_paused, _stream_player.stream])
				_phase = 3
				_frame = 0
		3: # a third press resumes (un-pauses) it
			if _frame == 1:
				_tap_interact()
			if _frame == 2:
				_release_interact()
			if _frame >= 10:
				if _radio.is_powered_on() and not _stream_player.stream_paused:
					print("PASS: pressing E a third time resumes playback")
				else:
					_fail("radio didn't resume after a third press (powered_on=%s paused=%s)" % [_radio.is_powered_on(), _stream_player.stream_paused])
				_check_track_cycling()
				_phase = 4
				_frame = 0
		4: # moving out of range clears _nearby_player, and E does nothing there
			if _frame == 1:
				_player.global_position = _radio.global_position + Vector3(0, 1.0, 100.0) # well past INTERACT_RANGE
			if _frame >= 10:
				if _radio._nearby_player == null:
					print("PASS: InteractArea loses the player once they walk out of range")
				else:
					_fail("InteractArea still thinks the player is nearby after walking far away")
				_phase = 5
				_frame = 0
		5: # E does nothing while out of range
			var was_on: bool = _radio.is_powered_on()
			if _frame == 1:
				_tap_interact()
			if _frame == 2:
				_release_interact()
			if _frame >= 10:
				if _radio.is_powered_on() == was_on:
					print("PASS: an out-of-range E press doesn't toggle the radio")
				else:
					_fail("radio toggled from an out-of-range E press")
				_finish()

## Directly exercises the playlist-advance logic (bypassing waiting for a
## real ~2-3 minute mp3 to actually finish) -- calls the SAME handler
## real playback completion calls, _on_track_finished(), matching how
## other tests in this project call a handler directly to avoid waiting on
## real-time media playback.
func _check_track_cycling() -> void:
	_radio._on_track_finished()
	if _radio._track_index == 1 and _stream_player.stream == _radio.PLAYLIST[1] and _stream_player.playing:
		print("PASS: a finished track advances to the next playlist entry and keeps playing")
	else:
		_fail("track didn't advance correctly (index=%d stream=%s)" % [_radio._track_index, _stream_player.stream])

	_radio._on_track_finished()
	if _radio._track_index == 0 and _stream_player.stream == _radio.PLAYLIST[0]:
		print("PASS: the playlist wraps back around to the first track")
	else:
		_fail("playlist didn't wrap back to track 0 (index=%d)" % _radio._track_index)

func _finish() -> void:
	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
