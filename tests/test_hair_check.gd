extends Node3D
## Regression check for hair customization (character.gd's own
## VALID_HAIR_STYLES/get_hair_style()/set_hair_style()/_build_hair(), the
## character_creator.gd Hair tab, and character_save.gd's hair_style field):
## an invalid style is rejected, a valid style builds a real MeshInstance3D
## under get_head_node(), "none" removes it again, and -- the one genuinely
## tricky part -- a head SHAPE change (which frees and rebuilds
## get_head_node() itself, see character.gd's own _build_head() class
## comment) doesn't silently leave the hairstyle destroyed/orphaned, since
## _build_head() now calls _build_hair() again at its own end specifically
## to prevent that. Runs headless: a real Character scene, no rendering
## needed.

const CharacterScene := preload("res://character/character.tscn")
const CharacterCreatorScene := preload("res://character_creator/character_creator.tscn")
const CharacterSave = preload("res://character/character_save.gd")

var _character: Node3D
var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	_character = CharacterScene.instantiate()
	_character.set_paintable(false)
	add_child(_character)
	call_deferred("_run_checks")

func _run_checks() -> void:
	if _character.get_hair_style() == "none":
		print("PASS: a fresh character starts with hair_style 'none'")
	else:
		_fail("fresh character's hair_style should default to 'none', got '%s'" % _character.get_hair_style())

	_character.set_hair_style("not_a_real_style")
	if _character.get_hair_style() == "none":
		print("PASS: an invalid hair style is rejected, stays at the previous value")
	else:
		_fail("an invalid hair style was accepted (now '%s')" % _character.get_hair_style())

	_character.set_hair_style("embermane")
	var head: Node3D = _character.get_head_node()
	if _character.get_hair_style() == "embermane" and is_instance_valid(_character._hair_mesh_instance) and _character._hair_mesh_instance.mesh != null and _character._hair_mesh_instance.get_parent() == head:
		print("PASS: set_hair_style('embermane') builds a real mesh under get_head_node()")
	else:
		_fail("set_hair_style('embermane') didn't produce a mesh under the head node")

	# Real reported bug: the imported mesh's own spikes point in local -Y
	# from its baked-in origin -- placed upright with no rotation, that read
	# in-game as hair growing upside-down off the back/underside of the
	# head (confirmed via a Blender render of the raw asset against a
	# stand-in head shape, see character.gd's own HAIR_SCENES comment for
	# the full story). A 180-degree flip around local X corrects it, and the
	# mesh's origin (where the now-upward-pointing roots cluster) needs to
	# sit at the head box's own TOP surface, not its vertical center.
	var expected_top_y: float = _character._parts["head"]["size"].y
	if is_equal_approx(_character._hair_mesh_instance.rotation.x, PI):
		print("PASS: the hair mesh is flipped 180 degrees around local X so spikes point up, not down")
	else:
		_fail("hair mesh rotation.x is %s, expected PI (180 degrees)" % _character._hair_mesh_instance.rotation.x)
	if is_equal_approx(_character._hair_mesh_instance.position.y, expected_top_y):
		print("PASS: the hair mesh sits at the head box's own top surface (y=%s)" % expected_top_y)
	else:
		_fail("hair mesh position.y is %s, expected the head box's top surface (%s)" % [_character._hair_mesh_instance.position.y, expected_top_y])

	_character.set_hair_style("none")
	if _character.get_hair_style() == "none" and not is_instance_valid(_character._hair_mesh_instance):
		print("PASS: set_hair_style('none') removes the hair mesh again")
	else:
		_fail("set_hair_style('none') should have removed the hair mesh")

	# --- The tricky part: a head SHAPE change frees/rebuilds the head pivot
	# itself (see character.gd's own _build_head()) -- hair must survive
	# that by being rebuilt under the NEW pivot, not silently destroyed. ---
	_character.set_hair_style("embermane")
	var old_head_pivot: Node3D = _character.get_head_node()
	_character.set_head_shape("cube")
	var new_head_pivot: Node3D = _character.get_head_node()
	if new_head_pivot != old_head_pivot:
		print("PASS: changing head shape really does rebuild a new head pivot (sanity check on the premise)")
	else:
		_fail("changing head shape didn't rebuild the head pivot -- the premise of this check is wrong")

	if _character.get_hair_style() == "embermane" and is_instance_valid(_character._hair_mesh_instance) and _character._hair_mesh_instance.get_parent() == new_head_pivot:
		print("PASS: hair survives a head-shape change (rebuilt under the new pivot, not left orphaned)")
	else:
		_fail("hair was lost after a head-shape change (style='%s')" % _character.get_hair_style())

	if is_instance_valid(old_head_pivot):
		_fail("the OLD head pivot (and whatever hair was still attached to it) should have been freed")
	else:
		print("PASS: the old head pivot (and its stale hair copy) was actually freed, not just abandoned")

	# --- character_save.gd round-trip ---
	var test_path := "user://test_hair_check_%d.cfg" % randi()
	var id := CharacterSave.save_new_character("HairTester", "sphere", PackedByteArray(), test_path, "embermane")
	var loaded: Dictionary = CharacterSave.load_character(id, test_path)
	if loaded.get("hair_style") == "embermane":
		print("PASS: character_save.gd round-trips hair_style through save/load")
	else:
		_fail("loaded hair_style was '%s', expected 'embermane'" % loaded.get("hair_style"))
	var listed: Array[Dictionary] = CharacterSave.list_characters(test_path)
	if listed.size() == 1 and listed[0].get("hair_style") == "embermane":
		print("PASS: list_characters() also includes hair_style")
	else:
		_fail("list_characters() didn't include the right hair_style: %s" % [listed])
	var dir := DirAccess.open("user://")
	if dir != null and dir.file_exists(test_path.trim_prefix("user://")):
		dir.remove(test_path.trim_prefix("user://"))

	# A save with NO hair_style at all (an old save file predating this
	# feature) must default to "none", not error or crash.
	var legacy_path := "user://test_hair_check_legacy_%d.cfg" % randi()
	var legacy_config := ConfigFile.new()
	legacy_config.set_value("character_0", "name", "Legacy")
	legacy_config.set_value("character_0", "head_shape", "sphere")
	legacy_config.set_value("character_0", "atlas_png", PackedByteArray())
	legacy_config.set_value("character_0", "playtime_seconds", 0.0)
	legacy_config.set_value("character_0", "level", 1)
	legacy_config.save(legacy_path)
	var legacy_loaded: Dictionary = CharacterSave.load_character(0, legacy_path)
	if legacy_loaded.get("hair_style") == "none":
		print("PASS: a pre-hair-feature save file defaults hair_style to 'none' on load")
	else:
		_fail("a legacy save (no hair_style key at all) loaded hair_style='%s', expected 'none'" % legacy_loaded.get("hair_style"))
	var legacy_dir := DirAccess.open("user://")
	if legacy_dir != null and legacy_dir.file_exists(legacy_path.trim_prefix("user://")):
		legacy_dir.remove(legacy_path.trim_prefix("user://"))

	# --- character_creator.gd's Hair tab buttons ---
	var creator: Node3D = CharacterCreatorScene.instantiate()
	add_child(creator)
	await get_tree().process_frame
	var embermane_button: Button = creator.get_node("UI/HairEmbermaneButton")
	var none_button: Button = creator.get_node("UI/HairNoneButton")
	embermane_button.pressed.emit()
	if creator.character.get_hair_style() == "embermane":
		print("PASS: pressing the Embermane hair button calls set_hair_style('embermane') on the creator's character")
	else:
		_fail("pressing the Embermane hair button didn't set the creator character's hair style (got '%s')" % creator.character.get_hair_style())
	none_button.pressed.emit()
	if creator.character.get_hair_style() == "none":
		print("PASS: pressing the None hair button clears it back to 'none'")
	else:
		_fail("pressing the None hair button didn't clear the hair style")
	creator.queue_free()

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
