extends Node
## Regression check for the admin-gated dev console commands /infinitehealth
## and /infinitesprint (see player.gd's own _infinite_health/
## _infinite_stamina and ui/dev_console.gd's own _is_admin()/
## _toggle_infinite_health()/_toggle_infinite_sprint()): confirms both
## toggle correctly through the real console command path, that infinite
## health actually blocks take_damage() (a real early return, not a clamp),
## that infinite sprint blocks stamina drain during both plain sprinting AND
## climbing (the same stamina pool, see player.gd's own _process_climbing
## comment), and that both commands are denied to a networked, non-host
## client (see net/network.gd's own is_networked()/is_server()) while still
## allowed to a real host or a plain singleplayer session. Runs headless,
## driving _physics_process manually for deterministic deltas -- same idiom
## test_climbing_check.gd's own fixtures already use.

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

## A tall climbable wall, well clear of the ground fixture -- matches
## test_climbing_check.gd's own _make_wall(TALL_WALL_HEIGHT, ...) fixture,
## just inlined here since this check only ever needs the one wall.
func _make_wall(height: float, x_offset: float) -> void:
	var wall := StaticBody3D.new()
	wall.collision_layer = 1
	wall.position = Vector3(x_offset, height / 2.0, -1.0)
	var col := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	shape.size = Vector3(10.0, height, 1.0)
	col.shape = shape
	wall.add_child(col)
	add_child(wall)

func _ready() -> void:
	if not OS.is_debug_build():
		_fail("OS.is_debug_build() is false in this test run -- can't verify the console at all")
		print("OVERALL: FAIL")
		get_tree().quit(1)
		return

	_make_ground()
	_make_wall(30.0, 20.0)

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

## Real console input path, same as test_dev_console_check.gd's own
## /spawn sword check -- goes through the actual text_submitted signal, not
## a direct _run_command() call, so this also proves the input wiring works.
func _submit(command: String) -> void:
	_input_field.text = command
	_input_field.emit_signal("text_submitted", command)

func _log_contains(needle: String) -> bool:
	return _output.get_parsed_text().contains(needle)

func _run_checks() -> void:
	_settle(_player)

	_check_admin_default_singleplayer()
	_check_infinite_health_toggle_and_block()
	_check_infinite_sprint_toggle_and_block()
	_check_infinite_sprint_blocks_climbing()
	_check_admin_denied_as_networked_client()
	_check_admin_allowed_as_host()

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

func _check_admin_default_singleplayer() -> void:
	if Net.is_networked():
		_fail("Net.is_networked() is true before any networking was ever touched -- test fixture assumption broken")
		return
	if _console._is_admin():
		print("PASS: _is_admin() is true in a plain singleplayer session (not networked)")
	else:
		_fail("_is_admin() is false in a plain singleplayer session")

func _check_infinite_health_toggle_and_block() -> void:
	if _player.is_infinite_health():
		_fail("is_infinite_health() true before /infinitehealth was ever used")
		return

	_submit("/infinitehealth")
	if _player.is_infinite_health() and _log_contains("infinite health ON"):
		print("PASS: /infinitehealth turns infinite health on and logs it")
	else:
		_fail("/infinitehealth didn't turn on infinite health (is_infinite_health=%s)" % _player.is_infinite_health())

	var health_before: float = _player.health
	_player.take_damage(50.0)
	if _player.health == health_before:
		print("PASS: take_damage() is a real no-op while infinite health is on (health stayed ", _player.health, ")")
	else:
		_fail("took damage while infinite health was on (before=%s after=%s)" % [health_before, _player.health])

	_submit("/infinitehealth")
	if not _player.is_infinite_health() and _log_contains("infinite health off"):
		print("PASS: /infinitehealth toggles back off and logs it")
	else:
		_fail("/infinitehealth didn't toggle back off")

	health_before = _player.health
	_player.take_damage(15.0)
	if _player.health == health_before - 15.0:
		print("PASS: take_damage() applies normally again after infinite health is off (", health_before, " -> ", _player.health, ")")
	else:
		_fail("take_damage() didn't apply normally after toggling infinite health off (before=%s after=%s)" % [health_before, _player.health])

	# Heal back up so the sprint/climb/admin checks below aren't tripped up
	# by leftover damage from this check -- take_damage() has no heal path,
	# so restore the var directly (a test-only shortcut, not an API real
	# gameplay code would ever use).
	_player.health = _player.max_health

func _check_infinite_sprint_toggle_and_block() -> void:
	if _player.is_infinite_stamina():
		_fail("is_infinite_stamina() true before /infinitesprint was ever used")
		return

	_submit("/infinitesprint")
	if _player.is_infinite_stamina() and _log_contains("infinite sprint ON"):
		print("PASS: /infinitesprint turns infinite sprint on and logs it")
	else:
		_fail("/infinitesprint didn't turn on infinite sprint")

	_player.stamina = _player.max_stamina
	var stamina_before: float = _player.stamina
	Input.action_press("sprint")
	Input.action_press("move_forward")
	for i in range(90):
		_player._physics_process(1.0 / 60.0)
	Input.action_release("sprint")
	Input.action_release("move_forward")
	_player.velocity = Vector3.ZERO
	if _player.stamina >= stamina_before - 0.01:
		print("PASS: sprinting for 1.5s doesn't drain stamina while infinite sprint is on (", stamina_before, " -> ", _player.stamina, ")")
	else:
		_fail("stamina drained while infinite sprint was on (before=%s after=%s)" % [stamina_before, _player.stamina])

	_submit("/infinitesprint")
	if not _player.is_infinite_stamina() and _log_contains("infinite sprint off"):
		print("PASS: /infinitesprint toggles back off and logs it")
	else:
		_fail("/infinitesprint didn't toggle back off")

	_player.stamina = _player.max_stamina
	stamina_before = _player.stamina
	Input.action_press("sprint")
	Input.action_press("move_forward")
	for i in range(60):
		_player._physics_process(1.0 / 60.0)
	Input.action_release("sprint")
	Input.action_release("move_forward")
	_player.velocity = Vector3.ZERO
	if _player.stamina < stamina_before - 1.0:
		print("PASS: stamina drains normally again after toggling infinite sprint off (", stamina_before, " -> ", _player.stamina, ")")
	else:
		_fail("stamina didn't drain after toggling infinite sprint off (before=%s after=%s)" % [stamina_before, _player.stamina])

## A fresh player fixture positioned at the climbable wall (see
## _make_wall(30.0, 20.0) in _ready()) rather than reusing _player -- climbing
## repositions/reorients the body in a way the plain-sprint check above
## doesn't need to account for, and a fresh fixture keeps this check's own
## pass/fail independent of whatever state that one left behind.
func _check_infinite_sprint_blocks_climbing() -> void:
	var climber: CharacterBody3D = PlayerScene.instantiate()
	add_child(climber)
	var character := CharacterScene.instantiate()
	character.set_paintable(false)
	climber.add_child(character)
	# 0.7m clear of the wall's own near face (z=-0.5), matching
	# test_climbing_check.gd's own _make_player() placement convention.
	climber.global_position = Vector3(20.0, 0.05, 0.2)
	_settle(climber)

	climber.set_infinite_stamina(true)
	Input.action_press("sprint")
	Input.action_press("move_forward")
	climber._physics_process(1.0 / 60.0)
	Input.action_release("sprint")

	if not climber._is_climbing:
		_fail("fresh climber fixture didn't grab the wall -- can't verify infinite sprint during climbing")
		Input.action_release("move_forward")
		climber.queue_free()
		return

	var stamina_before: float = climber.stamina
	for i in range(30):
		climber._physics_process(1.0 / 60.0)
	if climber.stamina >= stamina_before - 0.01:
		print("PASS: climbing doesn't drain stamina while infinite sprint is on (", stamina_before, " -> ", climber.stamina, ")")
	else:
		_fail("stamina drained while climbing with infinite sprint on (before=%s after=%s)" % [stamina_before, climber.stamina])

	Input.action_release("move_forward")
	climber.queue_free()

## Net.join_game() against an unreachable local port -- the connection never
## actually needs to complete: is_networked()/is_server() only look at the
## multiplayer_peer's own presence/role (confirmed via a direct scratch
## probe: a freshly created client peer already reads is_networked=true,
## is_server=false before any real handshake happens), so this is enough to
## exercise the "networked but not the host" denial path without a second
## real peer.
func _check_admin_denied_as_networked_client() -> void:
	var err := Net.join_game("127.0.0.1", 54329)
	if err != OK:
		_fail("Net.join_game() itself failed (err=%s) -- can't verify the networked-non-host denial" % err)
		return

	if _console._is_admin():
		_fail("_is_admin() is true while networked as a non-host client")
	else:
		print("PASS: _is_admin() is false while networked as a plain (non-host) client")

	var before: bool = _player.is_infinite_health()
	_submit("/infinitehealth")
	if _player.is_infinite_health() == before and _log_contains("admin only"):
		print("PASS: /infinitehealth is denied to a non-host client and logs why")
	else:
		_fail("/infinitehealth wasn't denied to a non-host client (is_infinite_health changed from %s, or no denial logged)" % before)

	Net.disconnect_game()

func _check_admin_allowed_as_host() -> void:
	var err := Net.host_game(54330)
	if err != OK:
		_fail("Net.host_game() itself failed (err=%s) -- can't verify host-admin allowance" % err)
		return

	if _console._is_admin():
		print("PASS: _is_admin() is true while hosting")
	else:
		_fail("_is_admin() is false while hosting -- the host should always be admin")

	var before: bool = _player.is_infinite_health()
	_submit("/infinitehealth")
	if _player.is_infinite_health() != before:
		print("PASS: /infinitehealth is allowed while hosting")
	else:
		_fail("/infinitehealth was denied while hosting")

	Net.disconnect_game()
