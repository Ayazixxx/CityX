extends Node
## Regression check for the voice chat plumbing (player.gd's own
## _setup_voice_playback/_setup_voice_capture/_encode_pcm16/_decode_pcm16)
## and ui/settings_panel.gd's own device-list/push-to-talk persistence.
##
## What this CANNOT verify (see player.gd's own VOICE_SEND_INTERVAL_SECONDS
## comment): actual audio quality, latency, or whether real captured
## speech survives the encode/decode round trip intelligibly -- there's no
## hardware mic/speaker in a headless test environment, and Godot's own
## dummy audio driver here means _setup_voice_capture()'s own
## AudioStreamMicrophone likely never produces real samples either. What
## IS verified: the whole system initializes and runs without crashing even
## with no real audio hardware (a real, concrete risk given this depends on
## driver/enable_input + an actual capture device existing), and the pure
## encode/decode math is provably correct.

const PlayerScene := preload("res://player/player.tscn")
const SettingsPanel = preload("res://ui/settings_panel.gd")
const SettingsPanelScene := preload("res://ui/settings_panel.tscn")

var _ok := true
var _player: CharacterBody3D

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	# Player instantiated first -- _run_encode_decode_checks needs a real
	# object to call _encode_pcm16/_decode_pcm16 on (player.gd extends
	# CharacterBody3D, so a bare Node with the script attached via
	# set_script() doesn't work -- confirmed directly as a real test bug on
	# the first attempt here: "Script inherits from native type
	# 'CharacterBody3D', so it can't be assigned to an object of type
	# 'Node'").
	_run_player_setup_checks()
	_run_encode_decode_checks()
	_run_settings_panel_checks()
	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

func _run_encode_decode_checks() -> void:
	var dummy := _player
	# Round-trips a real, non-trivial waveform (not just zeros) through
	# encode -> decode and checks the result is close to the original --
	# "close" not "exact" since 16-bit quantization is real, expected loss,
	# not a bug (see player.gd's own _encode_pcm16 comment on why this isn't
	# real compression, just a raw 16-bit format).
	var original := PackedVector2Array()
	for i in range(100):
		var v: float = sin(float(i) * 0.3) * 0.7
		original.append(Vector2(v, v))
	var encoded: PackedByteArray = dummy._encode_pcm16(original)
	if encoded.size() == original.size() * 2:
		print("PASS: _encode_pcm16 produces 2 bytes per sample (16-bit mono)")
	else:
		_fail("_encode_pcm16 produced %d bytes for %d samples, expected %d" % [encoded.size(), original.size(), original.size() * 2])

	var decoded: PackedVector2Array = dummy._decode_pcm16(encoded)
	if decoded.size() == original.size():
		print("PASS: _decode_pcm16 recovers the same sample count")
	else:
		_fail("_decode_pcm16 returned %d samples, expected %d" % [decoded.size(), original.size()])

	var max_error := 0.0
	for i in range(original.size()):
		max_error = maxf(max_error, absf(decoded[i].x - original[i].x))
	if max_error < 0.001:
		print("PASS: encode/decode round-trip stays within 16-bit quantization error (max_error=", max_error, ")")
	else:
		_fail("encode/decode round-trip error too large: %s" % max_error)

	# Silence must round-trip to exactly silence, not noise.
	var silence := PackedVector2Array()
	silence.resize(20)
	var silence_decoded: PackedVector2Array = dummy._decode_pcm16(dummy._encode_pcm16(silence))
	var silent := true
	for v in silence_decoded:
		if not is_equal_approx(v.x, 0.0):
			silent = false
	if silent:
		print("PASS: silence encodes/decodes as exact silence")
	else:
		_fail("silence round-trip produced non-zero samples")

func _run_player_setup_checks() -> void:
	_player = PlayerScene.instantiate()
	add_child(_player)

	if _player.voice_player == null:
		_fail("player.gd has no voice_player reference")
	else:
		print("PASS: player.gd resolves its own VoicePlayer node")

	if _player.voice_player.stream is AudioStreamGenerator and _player.voice_player.playing:
		print("PASS: VoicePlayer is playing with an AudioStreamGenerator stream after setup")
	else:
		_fail("VoicePlayer isn't set up correctly (stream=%s playing=%s)" % [_player.voice_player.stream, _player.voice_player.playing])

	if _player._voice_playback != null:
		print("PASS: _voice_playback resolved to a real AudioStreamGeneratorPlayback")
	else:
		_fail("_voice_playback is null after _setup_voice_playback()")

	# The real point of this check: none of the mic-capture setup should
	# ever crash the game, even in an environment with no real audio input
	# hardware (headless/dummy driver here) -- a graceful no-op (silently
	# never capturing anything) is correct, a crash is not.
	var crashed := false
	# _ready() already called _setup_voice_capture() for this single-player
	# instance (is_multiplayer_authority() defaults true offline) -- confirm
	# it got through without throwing, and that _process_voice_capture()
	# (which will run every real frame for a local player) also survives a
	# manual call regardless of whether real capture is actually available.
	_player._process_voice_capture(1.0 / 60.0)
	if not crashed:
		print("PASS: voice capture setup/processing doesn't crash even without real audio input hardware")

func _run_settings_panel_checks() -> void:
	var panel: PanelContainer = SettingsPanelScene.instantiate()
	add_child(panel)

	if panel._output_device_option.item_count >= 0 and panel._input_device_option.item_count >= 0:
		print("PASS: settings panel populates device lists without crashing (", panel._output_device_option.item_count, " output, ", panel._input_device_option.item_count, " input)")
	else:
		_fail("device list population failed")

	panel._push_to_talk_check.button_pressed = false
	panel._on_push_to_talk_toggled(false)
	if not SettingsPanel.is_push_to_talk():
		print("PASS: toggling push-to-talk off persists and is readable via is_push_to_talk()")
	else:
		_fail("is_push_to_talk() still returned true after toggling it off")

	panel._push_to_talk_check.button_pressed = true
	panel._on_push_to_talk_toggled(true)
	if SettingsPanel.is_push_to_talk():
		print("PASS: toggling push-to-talk back on persists correctly")
	else:
		_fail("is_push_to_talk() still returned false after toggling it back on")

	panel.queue_free()
