extends Node
## Regression check for player.gd's own playtime tracking (save_character_id,
## PLAYTIME_SAVE_INTERVAL_SECONDS, _flush_playtime, and the
## NOTIFICATION_WM_CLOSE_REQUEST flush-on-quit) -- see character_save.gd's
## own update_playtime(), already covered from the module side by
## test_character_save_check.gd's own _run_playtime_checks.
##
## Drives _physics_process/_notification with direct calls instead of
## waiting out PLAYTIME_SAVE_INTERVAL_SECONDS (30s) in real physics ticks --
## same "call the lifecycle method directly for a controlled, fast test"
## approach already used elsewhere in this suite (e.g. test_fly_check.gd's
## own direct set_fly_mode() calls). set_physics_process(false) right after
## add_child stops the SceneTree from ALSO auto-driving real ticks in
## parallel, which would otherwise double-count delta on top of the direct
## calls below.

const CharacterSave = preload("res://character/character_save.gd")
const PlayerScene := preload("res://player/player.tscn")

var _ok := true
var _test_path: String = "user://test_playtime_characters_%d.cfg" % randi()
var _player: CharacterBody3D

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	var id := CharacterSave.save_new_character("Playtime Player", "sphere", PackedByteArray(), _test_path)

	_player = PlayerScene.instantiate()
	add_child(_player)
	_player.set_physics_process(false)
	_player.character_save_path = _test_path
	_player.save_character_id = id

	_run_accumulation_check()
	_run_manual_flush_check(id)
	_run_auto_flush_check(id)
	_run_close_request_flush_check(id)
	_run_untracked_player_check()

	_cleanup()
	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

func _run_accumulation_check() -> void:
	_player._physics_process(1.0 / 60.0)
	_player._physics_process(1.0 / 60.0)
	if _player._playtime_since_save > 0.0:
		print("PASS: _playtime_since_save accumulates across physics frames")
	else:
		_fail("_playtime_since_save is still 0 after two physics frames")

func _run_manual_flush_check(id: int) -> void:
	var before: float = _player._playtime_since_save
	_player._flush_playtime()
	if _player._playtime_since_save == 0.0:
		print("PASS: _flush_playtime() resets _playtime_since_save")
	else:
		_fail("_playtime_since_save is still %s after _flush_playtime()" % _player._playtime_since_save)

	var saved: Dictionary = CharacterSave.load_character(id, _test_path)
	if is_equal_approx(saved["playtime_seconds"], before):
		print("PASS: _flush_playtime() persists the accumulated seconds onto the saved character")
	else:
		_fail("saved playtime_seconds=%s, expected %s" % [saved["playtime_seconds"], before])

func _run_auto_flush_check(id: int) -> void:
	var before: Dictionary = CharacterSave.load_character(id, _test_path)
	# Force _playtime_since_save right up to the auto-flush threshold so the
	# very next physics frame crosses PLAYTIME_SAVE_INTERVAL_SECONDS and
	# triggers the interval-based auto-flush inside _physics_process itself
	# -- not calling _flush_playtime() directly this time, since the point
	# is to verify _physics_process's OWN threshold check actually fires it.
	_player._playtime_since_save = _player.PLAYTIME_SAVE_INTERVAL_SECONDS - 0.5
	_player._physics_process(1.0)

	if _player._playtime_since_save == 0.0:
		print("PASS: crossing PLAYTIME_SAVE_INTERVAL_SECONDS auto-flushes and resets the counter")
	else:
		_fail("_playtime_since_save is still %s after crossing the auto-flush threshold" % _player._playtime_since_save)

	var after: Dictionary = CharacterSave.load_character(id, _test_path)
	if after["playtime_seconds"] > before["playtime_seconds"]:
		print("PASS: the auto-flush actually persisted new playtime to disk")
	else:
		_fail("saved playtime_seconds didn't increase after the auto-flush (before=%s after=%s)" % [before["playtime_seconds"], after["playtime_seconds"]])

func _run_close_request_flush_check(id: int) -> void:
	var before: Dictionary = CharacterSave.load_character(id, _test_path)
	_player._playtime_since_save = 4.0
	_player._notification(NOTIFICATION_WM_CLOSE_REQUEST)

	var after: Dictionary = CharacterSave.load_character(id, _test_path)
	if is_equal_approx(after["playtime_seconds"], before["playtime_seconds"] + 4.0):
		print("PASS: NOTIFICATION_WM_CLOSE_REQUEST flushes any remaining playtime before quitting")
	else:
		_fail("playtime after WM_CLOSE_REQUEST=%s, expected %s" % [after["playtime_seconds"], before["playtime_seconds"] + 4.0])

## save_character_id == -1 (the default -- e.g. a remote peer's own copy of
## someone else's Player, see main.gd's own comment) must never accumulate
## or write anything, since there's no local save data to track.
func _run_untracked_player_check() -> void:
	var untracked: CharacterBody3D = PlayerScene.instantiate()
	add_child(untracked)
	untracked.set_physics_process(false)
	untracked.character_save_path = _test_path

	untracked._physics_process(5.0)
	if untracked._playtime_since_save == 0.0:
		print("PASS: a player with save_character_id == -1 never accumulates playtime")
	else:
		_fail("untracked player accumulated %s seconds of playtime despite save_character_id == -1" % untracked._playtime_since_save)

	untracked._notification(NOTIFICATION_WM_CLOSE_REQUEST)
	print("PASS: WM_CLOSE_REQUEST on an untracked player doesn't crash")

	untracked.queue_free()

func _cleanup() -> void:
	var dir := DirAccess.open("user://")
	if dir != null and dir.file_exists(_test_path.trim_prefix("user://")):
		dir.remove(_test_path.trim_prefix("user://"))
