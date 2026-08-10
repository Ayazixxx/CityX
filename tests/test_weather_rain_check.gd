extends Node
## Regression check for the admin-gated dev console command /weather
## <rain|clear> (see player/rain_effect.gd's own class comment, player.gd's
## own set_rain_active()/is_rain_active(), and ui/dev_console.gd's own
## _weather_command()): confirms rain starts inactive on spawn, that
## "/weather rain" via the real console input path actually starts the
## particle emitter and looping rain sound (not just flips a bool),
## "/weather clear" stops both again, an unrecognized sub-verb logs usage
## instead of crashing, and -- same admin-gating this project's own
## /infinitehealth and /infinitesprint already establish (see
## tests/test_infinite_cheats_check.gd, whose exact Net.join_game()/
## host_game() technique this mirrors) -- the command is denied to a
## networked, non-host client while still allowed to a real host or a plain
## singleplayer session.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _ok := true
var _player: CharacterBody3D
var _console: CanvasLayer
var _input_field: LineEdit
var _output: RichTextLabel

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

func _ready() -> void:
	if not OS.is_debug_build():
		_fail("OS.is_debug_build() is false in this test run -- can't verify the console at all")
		print("OVERALL: FAIL")
		get_tree().quit(1)
		return

	_make_ground()

	_player = PlayerScene.instantiate()
	add_child(_player)
	var character := CharacterScene.instantiate()
	character.set_paintable(false)
	_player.add_child(character)

	_console = _player.get_node_or_null("DevConsole")
	if _console == null:
		_fail("no DevConsole child found on the player (OS.is_debug_build() was true)")
		print("OVERALL: FAIL")
		get_tree().quit(1)
		return
	_input_field = _console.get_node("Panel/VBox/InputField")
	_output = _console.get_node("Panel/VBox/Output")

	call_deferred("_run_checks")

func _settle(body: CharacterBody3D) -> void:
	for i in range(10):
		body._physics_process(1.0 / 60.0)

func _submit(command: String) -> void:
	_input_field.text = command
	_input_field.emit_signal("text_submitted", command)

func _log_contains(needle: String) -> bool:
	return _output.get_parsed_text().contains(needle)

func _run_checks() -> void:
	_settle(_player)

	_check_rain_starts_inactive()
	_check_weather_rain_starts_rain()
	_check_weather_clear_stops_rain()
	_check_unknown_subcommand_logs_usage()
	_check_admin_denied_as_networked_client()
	_check_admin_allowed_as_host()

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

func _check_rain_starts_inactive() -> void:
	if _player._rain_effect == null:
		_fail("player._rain_effect was never instantiated for the local/authoritative player")
		return
	if not _player.is_rain_active() and not _player._rain_effect._particles.emitting and not _player._rain_effect._rain_player.playing:
		print("PASS: rain starts inactive (no particles, no sound) on spawn")
	else:
		_fail("rain wasn't inactive on spawn (is_rain_active=%s emitting=%s playing=%s)" % [_player.is_rain_active(), _player._rain_effect._particles.emitting, _player._rain_effect._rain_player.playing])

func _check_weather_rain_starts_rain() -> void:
	_submit("/weather rain")
	if _player.is_rain_active() and _player._rain_effect._particles.emitting and _player._rain_effect._particles.visible and _player._rain_effect._rain_player.playing and _log_contains("weather: rain"):
		print("PASS: /weather rain starts the particle emitter and the looping rain sound, and logs it")
	else:
		_fail("/weather rain didn't fully start rain (is_rain_active=%s emitting=%s visible=%s playing=%s)" % [_player.is_rain_active(), _player._rain_effect._particles.emitting, _player._rain_effect._particles.visible, _player._rain_effect._rain_player.playing])

func _check_weather_clear_stops_rain() -> void:
	_submit("/weather clear")
	if not _player.is_rain_active() and not _player._rain_effect._particles.emitting and not _player._rain_effect._particles.visible and not _player._rain_effect._rain_player.playing and _log_contains("weather: clear"):
		print("PASS: /weather clear stops the particle emitter and the rain sound, and logs it")
	else:
		_fail("/weather clear didn't fully stop rain (is_rain_active=%s emitting=%s visible=%s playing=%s)" % [_player.is_rain_active(), _player._rain_effect._particles.emitting, _player._rain_effect._particles.visible, _player._rain_effect._rain_player.playing])

func _check_unknown_subcommand_logs_usage() -> void:
	var before: bool = _player.is_rain_active()
	_submit("/weather ridiculous")
	if _player.is_rain_active() == before and _log_contains("usage: /weather"):
		print("PASS: an unrecognized /weather sub-verb logs usage instead of changing anything")
	else:
		_fail("/weather ridiculous either changed rain state or didn't log a usage message (is_rain_active changed from %s)" % before)

## Same technique as tests/test_infinite_cheats_check.gd's own identical
## check -- a freshly created client peer already reads is_networked=true,
## is_server=false before any real handshake completes, which is enough to
## exercise the "networked but not the host" denial path.
func _check_admin_denied_as_networked_client() -> void:
	var err := Net.join_game("127.0.0.1", 54341)
	if err != OK:
		_fail("Net.join_game() itself failed (err=%s) -- can't verify the networked-non-host denial" % err)
		return

	var before: bool = _player.is_rain_active()
	_submit("/weather rain")
	if _player.is_rain_active() == before and _log_contains("admin only"):
		print("PASS: /weather rain is denied to a non-host client and logs why")
	else:
		_fail("/weather rain wasn't denied to a non-host client (is_rain_active changed from %s, or no denial logged)" % before)

	Net.disconnect_game()

func _check_admin_allowed_as_host() -> void:
	var err := Net.host_game(54342)
	if err != OK:
		_fail("Net.host_game() itself failed (err=%s) -- can't verify host-admin allowance" % err)
		return

	var before: bool = _player.is_rain_active()
	_submit("/weather rain")
	if _player.is_rain_active() != before:
		print("PASS: /weather rain is allowed while hosting")
	else:
		_fail("/weather rain was denied while hosting")

	Net.disconnect_game()
