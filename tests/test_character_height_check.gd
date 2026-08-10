extends Node3D
## Regression check for the character creator's own height slider: the
## uniform scale character.gd applies, its clamping, the camera pivot
## following it on a real Player, and the whole round trip through a saved
## character on disk (including an OLD save with no height field at all,
## which must still read back as the normal size).
##
## Also guards the head-shape selector's REMOVAL from the creator: the
## slider replaced it in the same pass, and character.gd's own
## set_head_shape() stayed (loading/networking still call it), so the only
## thing that actually proves the selector is gone is the creator scene
## itself no longer having those nodes.

const CharacterScene := preload("res://character/character.tscn")
const CharacterSave := preload("res://character/character_save.gd")
const PlayerScene := preload("res://player/player.tscn")
const CreatorScene := preload("res://character_creator/character_creator.tscn")

const TEST_SAVE_PATH := "user://test_character_height.cfg"

var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	call_deferred("_run_checks")

func _run_checks() -> void:
	_check_scale_applied()
	_check_clamping()
	await _check_camera_pivot_follows_height()
	_check_save_round_trip()
	_check_legacy_save_defaults_to_normal()
	_check_creator_scene_has_slider_and_no_head_shape()

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

## --- 1. set_height_scale() is a real uniform scale on the node, and
## get_height_scale() reports it back. ---
func _check_scale_applied() -> void:
	var character: Node3D = CharacterScene.instantiate()
	add_child(character)

	if not is_equal_approx(character.get_height_scale(), 1.0):
		_fail("a fresh character doesn't start at 1.0 height (got %s)" % character.get_height_scale())
	elif not character.scale.is_equal_approx(Vector3.ONE):
		_fail("a fresh character's node scale isn't identity (got %s)" % character.scale)
	else:
		print("PASS: a fresh character starts at exactly normal height")

	character.set_height_scale(1.5)
	if not character.scale.is_equal_approx(Vector3.ONE * 1.5):
		_fail("set_height_scale(1.5) didn't apply a uniform 1.5 node scale (got %s)" % character.scale)
	elif not is_equal_approx(character.get_height_scale(), 1.5):
		_fail("get_height_scale() disagrees with what was just set")
	else:
		print("PASS: the tallest setting scales the character uniformly by 1.5")

	character.set_height_scale(character.MIN_HEIGHT_SCALE)
	if not character.scale.is_equal_approx(Vector3.ONE / 1.5):
		_fail("the shortest setting isn't 1/1.5 of normal (got %s)" % character.scale)
	else:
		print("PASS: the shortest setting is the same 1.5x factor the other way (1/1.5)")

	character.queue_free()

## --- 2. Out-of-range values (a hand-edited or corrupt save) clamp instead
## of producing a character of an arbitrary size. ---
func _check_clamping() -> void:
	var character: Node3D = CharacterScene.instantiate()
	add_child(character)

	character.set_height_scale(99.0)
	if not is_equal_approx(character.get_height_scale(), character.MAX_HEIGHT_SCALE):
		_fail("an absurdly tall value wasn't clamped to MAX_HEIGHT_SCALE (got %s)" % character.get_height_scale())
	else:
		print("PASS: a too-tall value clamps to MAX_HEIGHT_SCALE")

	character.set_height_scale(0.01)
	if not is_equal_approx(character.get_height_scale(), character.MIN_HEIGHT_SCALE):
		_fail("an absurdly short value wasn't clamped to MIN_HEIGHT_SCALE (got %s)" % character.get_height_scale())
	else:
		print("PASS: a too-short value clamps to MIN_HEIGHT_SCALE")

	character.queue_free()

## --- 3. On a real Player, the third-person camera pivot rides the
## character's own height (main.gd calls this right after applying a saved
## height), while the collision capsule deliberately does NOT change. ---
func _check_camera_pivot_follows_height() -> void:
	var player: CharacterBody3D = PlayerScene.instantiate()
	add_child(player)
	await get_tree().process_frame

	var character: Node3D = CharacterScene.instantiate()
	player.add_child(character)
	await get_tree().process_frame

	var capsule_before: float = (player.get_node("CollisionShape3D").shape as CapsuleShape3D).height

	character.set_height_scale(1.5)
	player.sync_camera_to_character_height()
	# Explicitly typed: `player` is declared as the base CharacterBody3D, so
	# a const read off it resolves to Variant and can't be inferred from.
	var expected: float = float(player.BASE_CAMERA_PIVOT_HEIGHT) * 1.5
	if not is_equal_approx(player.camera_pivot.position.y, expected):
		_fail("camera pivot didn't follow a 1.5x character (expected %s, got %s)" % [expected, player.camera_pivot.position.y])
	else:
		print("PASS: the camera pivot rises to ", expected, " for a 1.5x character")

	character.set_height_scale(character.MIN_HEIGHT_SCALE)
	player.sync_camera_to_character_height()
	expected = player.BASE_CAMERA_PIVOT_HEIGHT / 1.5
	if not is_equal_approx(player.camera_pivot.position.y, expected):
		_fail("camera pivot didn't follow the shortest character (expected %s, got %s)" % [expected, player.camera_pivot.position.y])
	else:
		print("PASS: the camera pivot drops to ", expected, " for the shortest character")

	# Deliberate, documented behaviour (see sync_camera_to_character_height's
	# own comment) -- a whole family of movement constants is tuned against
	# this one capsule, so a height change must never quietly resize it.
	var capsule_after: float = (player.get_node("CollisionShape3D").shape as CapsuleShape3D).height
	if not is_equal_approx(capsule_before, capsule_after):
		_fail("the collision capsule changed size with character height (%s -> %s)" % [capsule_before, capsule_after])
	else:
		print("PASS: the collision capsule keeps its authored size at every height")

	player.queue_free()

## --- 4. A chosen height survives save -> disk -> load. ---
func _check_save_round_trip() -> void:
	DirAccess.remove_absolute(ProjectSettings.globalize_path(TEST_SAVE_PATH))
	var id := CharacterSave.save_new_character("Tall Guy", "sphere", PackedByteArray(), TEST_SAVE_PATH, "none", 1.42)
	var data: Dictionary = CharacterSave.load_character(id, TEST_SAVE_PATH)
	if data.is_empty():
		_fail("the saved character couldn't be loaded back at all")
	elif not is_equal_approx(float(data["height_scale"]), 1.42):
		_fail("saved height didn't survive the round trip (got %s)" % data["height_scale"])
	else:
		print("PASS: a chosen height survives save -> load")

	var listed: Array[Dictionary] = CharacterSave.list_characters(TEST_SAVE_PATH)
	if listed.is_empty() or not is_equal_approx(float(listed[0]["height_scale"]), 1.42):
		_fail("list_characters() doesn't report the saved height (character_select's portraits need it)")
	else:
		print("PASS: list_characters() reports the saved height too")

	DirAccess.remove_absolute(ProjectSettings.globalize_path(TEST_SAVE_PATH))

## --- 5. THE compatibility guard: a character saved before this feature
## existed has no height_scale key at all, and must load as normal height
## rather than erroring or coming back as 0 (a zero-scale, invisible
## character). ---
func _check_legacy_save_defaults_to_normal() -> void:
	DirAccess.remove_absolute(ProjectSettings.globalize_path(TEST_SAVE_PATH))
	var config := ConfigFile.new()
	config.set_value("character_0", "name", "Old Save")
	config.set_value("character_0", "head_shape", "sphere")
	config.set_value("character_0", "atlas_png", PackedByteArray())
	config.save(TEST_SAVE_PATH)

	var data: Dictionary = CharacterSave.load_character(0, TEST_SAVE_PATH)
	if not is_equal_approx(float(data.get("height_scale", -1.0)), 1.0):
		_fail("a pre-height-slider save didn't default to normal height (got %s)" % data.get("height_scale", "<missing>"))
	else:
		print("PASS: a save written before the height slider existed loads as normal height")

	DirAccess.remove_absolute(ProjectSettings.globalize_path(TEST_SAVE_PATH))

## --- 6. The creator scene has lost BOTH the height slider and the
## head-shape selector. ---
##
## The slider was pulled on request ("get rid of the height slider from the
## character creator for now") while the underlying height_scale API on
## character.gd, and its save field, deliberately stayed -- every check
## above still exercises those. So this asserts the UI is gone, not the
## feature: a saved character made while the slider existed must still load
## back at its chosen height, which section 5 covers.
func _check_creator_scene_has_slider_and_no_head_shape() -> void:
	var state := CreatorScene.get_state()
	var names := []
	for i in range(state.get_node_count()):
		names.append(str(state.get_node_name(i)))

	var height_nodes := []
	for node_name in names:
		if node_name.begins_with("Height"):
			height_nodes.append(node_name)
	if not height_nodes.is_empty():
		_fail("the height slider is still in the creator scene: %s" % [height_nodes])
	else:
		print("PASS: the height slider is gone from the creator scene")

	var leftovers := []
	for node_name in names:
		if "HeadShape" in node_name or "HeadSphere" in node_name:
			leftovers.append(node_name)
	if not leftovers.is_empty():
		_fail("the head-shape selector is still in the creator scene: %s" % [leftovers])
	else:
		print("PASS: the head-shape selector is gone from the creator scene")
