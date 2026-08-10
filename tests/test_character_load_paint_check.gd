extends Node
## Regression check for a real reported bug: loading a previously-saved
## character (via character_select.gd's own Play button ->
## main.gd's _on_character_selected) spawned into the world with a BLANK
## white atlas and the default head shape, discarding the saved design
## entirely -- even though the save itself was fine (see character_save.gd's
## own module tests). Root cause: CHARACTER_SCENE.instantiate() does not run
## Character's own _ready() (only actually entering the tree does), and
## _ready() unconditionally rebuilds a fresh blank atlas_image (see
## character.gd's own _build_atlas) regardless of anything set on the node
## beforehand -- set_head_shape()/apply_atlas_png() were both being called
## BEFORE the character ever entered the tree, so _ready() always ran a
## beat later and silently wiped them back out. Fixed by calling both AFTER
## _enter_world_with(character) instead of before.
##
## Drives the real title -> singleplayer -> character select -> Play flow
## through main.tscn (same technique test_spawn_authority_check.gd already
## uses), pointed at a throwaway save path pre-seeded with a real painted,
## non-default-head-shape character, so this can never touch a real
## player's actual user://characters.cfg.

const CharacterSave = preload("res://character/character_save.gd")
const MainScene := preload("res://main.tscn")
const TRANSITION_WAIT := 2.0

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
	# A real, non-blank image -- half red, half blue -- standing in for a
	# genuinely hand-painted atlas. Matches character.gd's own
	# atlas_size/atlas_height (384x448) so apply_atlas_png()'s own size
	# check accepts it, same as a real saved character's atlas would be.
	var img := Image.create(384, 448, false, Image.FORMAT_RGBA8)
	img.fill(Color.RED)
	img.fill_rect(Rect2i(0, 224, 384, 224), Color.BLUE)
	return img.save_png_to_buffer()

func _ready() -> void:
	var test_save_path: String = "user://test_load_paint_characters_%d.cfg" % randi()
	_saved_atlas_png = _make_painted_atlas_png()
	# "cube", deliberately NOT the default "sphere" (see character.gd's own
	# _head_shape default) -- a load that silently no-ops set_head_shape
	# entirely (e.g. because it ran before _ready() and crashed/was skipped)
	# would still accidentally "pass" a same-as-default check; a real shape
	# change is the only way to catch that too, not just the atlas.
	_saved_id = CharacterSave.save_new_character("PaintTester", "cube", _saved_atlas_png, test_save_path)

	_main = MainScene.instantiate()
	_main.character_save_path = test_save_path
	add_child(_main)

func _process(delta: float) -> void:
	_elapsed += delta
	match _phase:
		0:
			if _elapsed > 0.2:
				_main._title_screen.enter_game_pressed.emit()
				_phase = 1
				_phase_start_time = _elapsed
		1:
			if _elapsed - _phase_start_time > TRANSITION_WAIT:
				# Simulates pressing Play on the saved "PaintTester" row --
				# character_select.gd's own row-populate already ran during
				# its _ready() (triggered by enter_game_pressed above), so
				# the saved character is already listed by now.
				_main._character_select.character_selected.emit(_saved_id)
				_phase = 2
				_phase_start_time = _elapsed
		2:
			if _elapsed - _phase_start_time > TRANSITION_WAIT:
				_run_checks()
				print("OVERALL: ", "PASS" if _ok else "FAIL")
				get_tree().quit(0 if _ok else 1)

func _run_checks() -> void:
	if _main._city == null or _main._city._player == null:
		_fail("no player was spawned into the world after loading the saved character")
		return
	var character: Node = _main._city._player.get_node_or_null("Character")
	if character == null:
		_fail("spawned player has no Character child")
		return

	if character.get_head_shape() == "cube":
		print("PASS: the loaded character's saved head_shape ('cube') made it onto the spawned character")
	else:
		_fail("spawned character's head_shape is '%s', expected 'cube'" % character.get_head_shape())

	var atlas: Image = character.atlas_image
	if atlas == null:
		_fail("spawned character's atlas_image is null")
		return

	# Sample a handful of points that are RED in the top half of the saved
	# atlas and BLUE in the bottom half (see _make_painted_atlas_png) --
	# a blank/default atlas would be solid white everywhere, an unambiguous
	# way to tell "the saved paint survived" from "silently reset to blank".
	var top_color: Color = atlas.get_pixel(50, 50)
	var bottom_color: Color = atlas.get_pixel(50, 400)
	if top_color.is_equal_approx(Color.RED) and bottom_color.is_equal_approx(Color.BLUE):
		print("PASS: spawned character's atlas_image matches the saved paint (red top, blue bottom), not blank white")
	else:
		_fail("spawned character's atlas colors are top=%s bottom=%s, expected red/blue (got blank/white if the bug regressed)" % [top_color, bottom_color])
