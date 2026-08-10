extends Node
## Regression check for the authority-before-add_child ordering fix in
## main.gd's _spawn_local_player/_try_spawn_player: a remotely-spawned
## player's _ready() must see its REAL (non-our-own) authority at the exact
## moment it runs, not our own default id, since _ready() only ever runs
## once and one-time-decides input capture, HUD/pause-menu spawning, camera
## activation, and (critically) whether physics_process/unhandled_input get
## disabled for a non-authority body. The original ordering (add_child
## THEN set_multiplayer_authority) got the FINAL authority id right (what
## the old scratch_mp_spawn_check.gd checked, and passed) while still
## getting every one of those _ready()-time decisions wrong -- this test
## checks the actual consequences, not just the id.

const CharacterSave = preload("res://character/character_save.gd")
const MainScene := preload("res://main.tscn")
const TRANSITION_WAIT := 2.0

var _main: Node
var _elapsed := 0.0
var _phase := 0
var _phase_start_time := 0.0
var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	_main = MainScene.instantiate()
	# This test drives the real character-creation flow (see phase 2 below)
	# all the way through _on_creation_finished, which now saves to disk
	# (see main.gd's own character_save_path) -- pointed at a throwaway
	# path so running this test can never write a fake "AuthorityChecker"
	# entry into a real player's actual user://characters.cfg.
	_main.character_save_path = "user://test_spawn_authority_characters_%d.cfg" % randi()
	add_child(_main)

func _process(delta: float) -> void:
	_elapsed += delta
	match _phase:
		0:
			if _elapsed > 0.2:
				Net.host_game()
				_main._title_screen.enter_game_pressed.emit()
				_phase = 1
				_phase_start_time = _elapsed
		1:
			if _elapsed - _phase_start_time > TRANSITION_WAIT:
				_main._character_select.create_new_pressed.emit()
				_phase = 2
				_phase_start_time = _elapsed
		2:
			if _elapsed - _phase_start_time > TRANSITION_WAIT:
				_main._creator._on_enter_city_pressed()
				var line_edit: LineEdit = _main._creator.get_node("UI/NameEntryOverlay/NamePanel/VBox/NameLineEdit")
				line_edit.text = "AuthorityChecker"
				_main._creator._on_name_confirmed()
				_phase = 3
				_phase_start_time = _elapsed
		3:
			if _elapsed - _phase_start_time > TRANSITION_WAIT:
				# Simulate a remote peer (id 777) announcing itself, exactly
				# like a real _rpc_announce_player call arriving from another
				# machine would -- exercises the exact _try_spawn_player path
				# that had the ordering bug.
				_main._rpc_announce_player(777, "RemoteFriend", "sphere", PackedByteArray())
				var remote_player: Node = _main._spawned_players.get(777)
				if remote_player == null or not is_instance_valid(remote_player):
					_fail("remote player (777) wasn't spawned at all")
				else:
					if remote_player.is_multiplayer_authority():
						_fail("remote player (777) incorrectly reads as OUR OWN authority")
					else:
						print("PASS: remote player (777) correctly does NOT read as our own authority")

					if not remote_player.is_physics_processing():
						print("PASS: remote player's physics_process is disabled (no fighting with replicated position)")
					else:
						_fail("remote player's physics_process is still ENABLED -- it'll read/act on our own local input")

					if not remote_player.is_processing_unhandled_input():
						print("PASS: remote player's unhandled_input is disabled")
					else:
						_fail("remote player's unhandled_input is still enabled")

					var remote_camera: Camera3D = remote_player.camera
					if remote_camera != null and not remote_camera.current:
						print("PASS: remote player's camera did NOT steal the active viewport camera")
					else:
						_fail("remote player's camera incorrectly became the active camera")

					var hud_count := 0
					var pause_count := 0
					for child in remote_player.get_children():
						if child.name.begins_with("Hud") or child.get_script() != null and str(child.get_script().resource_path).contains("hud"):
							hud_count += 1
						if str(child.name).to_lower().contains("pause"):
							pause_count += 1
					if hud_count == 0 and pause_count == 0:
						print("PASS: remote player got no phantom HUD/pause-menu children")
					else:
						_fail("remote player spawned phantom UI children (hud=%d pause=%d)" % [hud_count, pause_count])

				# Sanity check: our OWN local player must still correctly be
				# our own authority, with everything a real local player needs.
				var my_id: int = multiplayer.get_unique_id()
				var my_player: Node = _main._spawned_players.get(my_id)
				if my_player != null and my_player.is_multiplayer_authority() and my_player.is_physics_processing():
					print("PASS: our own local player is still correctly the authority with physics_process enabled")
				else:
					_fail("our own local player regressed -- authority=%s physics_processing=%s" % [my_player.is_multiplayer_authority() if my_player != null else "null", my_player.is_physics_processing() if my_player != null else "null"])

				# Real end-to-end check of main.gd's own _pending_save_character_id
				# plumbing (_on_creation_finished -> _spawn_local_player): the
				# freshly-created "AuthorityChecker" character's own saved id
				# should have made it all the way onto the actual spawned
				# Player node, not been left at the -1 default.
				if my_player != null:
					var saved := CharacterSave.list_characters(_main.character_save_path)
					var expected_id := -1
					for entry in saved:
						if entry["name"] == "AuthorityChecker":
							expected_id = entry["id"]
					if expected_id >= 0 and my_player.save_character_id == expected_id:
						print("PASS: our own local player's save_character_id matches the freshly-created character's real saved id")
					else:
						_fail("my_player.save_character_id=%d, expected the saved 'AuthorityChecker' id (%d)" % [my_player.save_character_id, expected_id])

					if remote_player != null and is_instance_valid(remote_player) and remote_player.save_character_id == -1:
						print("PASS: a remote peer's own Player copy has no local save_character_id (stays -1, no local save to track)")
					else:
						_fail("remote player's save_character_id was %s, expected -1" % [remote_player.save_character_id if remote_player != null and is_instance_valid(remote_player) else "n/a"])

				print("OVERALL: ", "PASS" if _ok else "FAIL")
				get_tree().quit(0 if _ok else 1)
