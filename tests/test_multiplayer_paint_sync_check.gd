extends Node
## Regression check for a real reported bug: "I tried multiplayer but the
## skins aren't loading for anyone other than the [local] player." Root
## cause (confirmed directly): main.gd's own _enter_world_with used to read
## character.get_atlas_png() for its _player_info/broadcast payload BEFORE
## the character had actually entered the scene tree -- fine for a freshly-
## created character (already painted live, well before _enter_world_with
## ever runs) but broken for a LOADED saved character, whose real
## head_shape/atlas_png used to only get applied back in
## _on_character_selected, entirely AFTER _enter_world_with (and its
## broadcast) had already returned. The local screen looked fine (that
## fix-up ran right after, locally), but the broadcast payload sent to every
## other peer was still the character's pre-tree-entry (blank) atlas, and
## head_shape was never included in the broadcast at all, for ANY
## character. Fixed by threading a `pending_paint` dict through
## _enter_world_with/_spawn_local_player, applied right after tree entry but
## BEFORE the atlas/head_shape get read for _player_info -- see main.gd's
## own class comments on _enter_world_with/_apply_pending_paint.
##
## Drives the real title -> host -> character select -> Play flow through
## main.tscn (same technique test_spawn_authority_check.gd already uses),
## with a saved character pre-seeded with a real painted, non-default-head-
## shape design, so a regression here can never be masked by "it happened to
## already match the default." Hosts on a throwaway high port (not
## Net.DEFAULT_PORT) so this test can never collide with a real game session
## (or another leftover test process) already holding 7777.

const CharacterSave = preload("res://character/character_save.gd")
const MainScene := preload("res://main.tscn")
const TRANSITION_WAIT := 2.0
const TEST_PORT := 58217

var _main: Node
var _elapsed := 0.0
var _phase := 0
var _phase_start_time := 0.0
var _ok := true
var _saved_id := -1
var _saved_atlas_png: PackedByteArray

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _make_painted_atlas_png() -> PackedByteArray:
	# Matches character.gd's own atlas_size/atlas_height (384x448) so
	# apply_atlas_png()'s own size check accepts it, same as
	# test_character_load_paint_check.gd's own stand-in atlas.
	var img := Image.create(384, 448, false, Image.FORMAT_RGBA8)
	img.fill(Color.RED)
	img.fill_rect(Rect2i(0, 224, 384, 224), Color.BLUE)
	return img.save_png_to_buffer()

func _ready() -> void:
	var test_save_path: String = "user://test_mp_paint_sync_characters_%d.cfg" % randi()
	_saved_atlas_png = _make_painted_atlas_png()
	# "cube", deliberately not the default "sphere" -- see
	# test_character_load_paint_check.gd's own comment on why a same-as-
	# default shape wouldn't actually catch a silently-skipped sync.
	_saved_id = CharacterSave.save_new_character("MPPaintTester", "cube", _saved_atlas_png, test_save_path)

	_main = MainScene.instantiate()
	_main.character_save_path = test_save_path
	add_child(_main)

func _process(delta: float) -> void:
	_elapsed += delta
	match _phase:
		0:
			if _elapsed > 0.2:
				var err := Net.host_game(TEST_PORT)
				if err != OK:
					_fail("Net.host_game(%d) failed with error %s -- can't exercise the networked path at all" % [TEST_PORT, err])
					print("OVERALL: ", "PASS" if _ok else "FAIL")
					get_tree().quit(0 if _ok else 1)
					return
				_main._title_screen.enter_game_pressed.emit()
				_phase = 1
				_phase_start_time = _elapsed
		1:
			if _elapsed - _phase_start_time > TRANSITION_WAIT:
				# Simulates pressing Play on the saved "MPPaintTester" row.
				_main._character_select.character_selected.emit(_saved_id)
				_phase = 2
				_phase_start_time = _elapsed
		2:
			if _elapsed - _phase_start_time > TRANSITION_WAIT:
				_run_checks()
				Net.disconnect_game()
				print("OVERALL: ", "PASS" if _ok else "FAIL")
				get_tree().quit(0 if _ok else 1)

func _run_checks() -> void:
	var my_id := multiplayer.get_unique_id()

	# --- The actual broadcast payload -- this is what every OTHER peer
	# would have received. Blank/default here means remote peers get a
	# blank/default character, which was the exact reported bug. ---
	if not _main._player_info.has(my_id):
		_fail("_player_info has no entry for our own peer id -- the networked branch of _enter_world_with never ran")
		return
	var info: Dictionary = _main._player_info[my_id]

	if info.get("head_shape", "") == "cube":
		print("PASS: the broadcast payload's own head_shape is the real saved 'cube', not the default")
	else:
		_fail("broadcast head_shape is '%s', expected 'cube' -- remote peers would see the default head shape" % info.get("head_shape", "<missing>"))

	var broadcast_png: PackedByteArray = info.get("atlas_png", PackedByteArray())
	if broadcast_png.is_empty():
		_fail("broadcast atlas_png is EMPTY -- remote peers would receive nothing to apply at all (this IS the reported bug)")
	else:
		var decoded := Image.new()
		var err := decoded.load_png_from_buffer(broadcast_png)
		if err != OK:
			_fail("broadcast atlas_png doesn't even decode as a valid PNG (error %s)" % err)
		else:
			var top_color: Color = decoded.get_pixel(50, 50)
			var bottom_color: Color = decoded.get_pixel(50, 400)
			if top_color.is_equal_approx(Color.RED) and bottom_color.is_equal_approx(Color.BLUE):
				print("PASS: the broadcast payload's own atlas_png matches the real saved paint (red top, blue bottom), not blank")
			else:
				_fail("broadcast atlas colors are top=%s bottom=%s, expected red/blue (blank/white if the bug regressed)" % [top_color, bottom_color])

	# --- Our own local screen should ALSO look right (this part already
	# worked before the fix -- confirming the fix didn't break it). ---
	if _main._city == null or not _main._spawned_players.has(my_id):
		_fail("our own local player was never spawned")
		return
	var player: Node = _main._spawned_players[my_id]
	var character: Node = player.get_node_or_null("Character")
	if character == null:
		_fail("our own local player has no Character child")
		return
	if character.get_head_shape() == "cube":
		print("PASS: our own local character's head_shape is also correctly 'cube'")
	else:
		_fail("our own local character's head_shape is '%s', expected 'cube'" % character.get_head_shape())
	var atlas: Image = character.atlas_image
	if atlas != null and atlas.get_pixel(50, 50).is_equal_approx(Color.RED) and atlas.get_pixel(50, 400).is_equal_approx(Color.BLUE):
		print("PASS: our own local character's atlas_image also matches the real saved paint")
	else:
		_fail("our own local character's atlas_image doesn't match the saved paint")
