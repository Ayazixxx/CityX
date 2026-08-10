extends Node
## Regression check for character/character_save.gd (save/load a created
## character to disk so it survives a game restart, see main.gd's own
## _on_creation_finished/_on_character_selected), including its
## playtime_seconds/level fields, update_playtime(), and delete_character()
## -- plus ui/character_select.gd's own row-populate/select/portrait/delete-
## confirm wiring. Uses a
## THROWAWAY save path the whole way through (never the real
## user://characters.cfg) so running this test can never touch or overwrite
## a real player's actual saved characters -- see character_save.gd's own
## comment on why that matters here specifically (unlike e.g. settings.cfg,
## a saved character represents real hand-painted user work, not just a
## volume slider).

const CharacterSave = preload("res://character/character_save.gd")
const CharacterSelectScene := preload("res://ui/character_select.tscn")

# A random suffix so re-running this test (or running it alongside another
# instance) can never collide with a leftover file from a previous run that
# failed to clean up.
var _test_path: String = "user://test_characters_%d.cfg" % randi()
var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	_run_module_checks()
	_run_playtime_checks()
	_run_delete_checks()
	_run_migration_checks()
	await _run_ui_checks()
	_cleanup()
	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

func _run_module_checks() -> void:
	if CharacterSave.list_characters(_test_path).size() != 0:
		_fail("list_characters() on a fresh path returned non-empty")
	else:
		print("PASS: list_characters() is empty before anything is saved")

	var fake_png := PackedByteArray([1, 2, 3, 4, 5])
	var id_a := CharacterSave.save_new_character("Aiko", "sphere", fake_png, _test_path)
	if id_a >= 0:
		print("PASS: save_new_character() returns a valid id (", id_a, ")")
	else:
		_fail("save_new_character() returned a negative id")

	var list_after_one := CharacterSave.list_characters(_test_path)
	if list_after_one.size() == 1 and list_after_one[0]["name"] == "Aiko" and list_after_one[0]["head_shape"] == "sphere" and list_after_one[0]["id"] == id_a:
		print("PASS: list_characters() reflects the saved character")
	else:
		_fail("list_characters() after one save is wrong: %s" % [list_after_one])

	if list_after_one[0]["level"] == 1 and is_equal_approx(list_after_one[0]["playtime_seconds"], 0.0):
		print("PASS: a freshly saved character starts at level 1 with 0 playtime")
	else:
		_fail("fresh character's level/playtime wrong: %s" % [list_after_one[0]])

	var loaded := CharacterSave.load_character(id_a, _test_path)
	if loaded.get("name") == "Aiko" and loaded.get("head_shape") == "sphere" and loaded.get("atlas_png") == fake_png:
		print("PASS: load_character() returns the full saved data, including atlas_png bytes")
	else:
		_fail("load_character() returned wrong data: %s" % [loaded])

	var id_b := CharacterSave.save_new_character("Boro", "pyramid", PackedByteArray([9, 8, 7]), _test_path)
	if id_b != id_a:
		print("PASS: a second save gets a distinct id (", id_a, " vs ", id_b, ")")
	else:
		_fail("second save reused the same id as the first (%d)" % id_b)

	if CharacterSave.list_characters(_test_path).size() == 2:
		print("PASS: list_characters() reflects both saved characters")
	else:
		_fail("list_characters() after two saves didn't return 2 entries")

	var missing := CharacterSave.load_character(9999, _test_path)
	if missing.is_empty():
		print("PASS: load_character() on a nonexistent id returns an empty Dictionary")
	else:
		_fail("load_character() on a nonexistent id returned '%s', expected empty" % [missing])

func _run_playtime_checks() -> void:
	var id := CharacterSave.save_new_character("Playtime Tester", "cube", PackedByteArray(), _test_path)

	CharacterSave.update_playtime(id, 12.5, _test_path)
	var after_one: Dictionary = CharacterSave.load_character(id, _test_path)
	if is_equal_approx(after_one["playtime_seconds"], 12.5):
		print("PASS: update_playtime() sets playtime on a fresh character")
	else:
		_fail("playtime after one update_playtime() call was %s, expected 12.5" % after_one["playtime_seconds"])

	CharacterSave.update_playtime(id, 7.5, _test_path)
	var after_two: Dictionary = CharacterSave.load_character(id, _test_path)
	if is_equal_approx(after_two["playtime_seconds"], 20.0):
		print("PASS: update_playtime() accumulates onto existing playtime rather than overwriting it")
	else:
		_fail("playtime after two update_playtime() calls was %s, expected 20.0" % after_two["playtime_seconds"])

	# Must not throw/crash on an id that doesn't exist (e.g. a stale player
	# session whose character got deleted out from under it) -- see
	# update_playtime()'s own comment on why this is a silent no-op.
	CharacterSave.update_playtime(9999, 5.0, _test_path)
	print("PASS: update_playtime() on a nonexistent id doesn't crash")

## Adds then removes its own throwaway character ("DeleteMe") so the net
## effect on _test_path's other 3 entries (Aiko, Boro, Playtime Tester --
## still relied on by _run_ui_checks below) is zero.
func _run_delete_checks() -> void:
	var id_a := CharacterSave.save_new_character("DeleteMe", "sphere", PackedByteArray(), _test_path)
	var before: int = CharacterSave.list_characters(_test_path).size()

	CharacterSave.delete_character(id_a, _test_path)
	var after: Array[Dictionary] = CharacterSave.list_characters(_test_path)
	if after.size() == before - 1 and not after.any(func(e): return e["id"] == id_a):
		print("PASS: delete_character() removes the saved character from disk")
	else:
		_fail("delete_character() didn't remove the character, size went from %d to %d" % [before, after.size()])

	if CharacterSave.load_character(id_a, _test_path).is_empty():
		print("PASS: load_character() on a deleted id returns empty, same as a nonexistent one")
	else:
		_fail("load_character() still returned data for a deleted id")

	# Must not crash on an id that was never saved (e.g. a stale UI row list
	# vs. disk, or a double-fired confirm button) -- same silent no-op
	# contract as update_playtime/update_progress/update_inventory.
	CharacterSave.delete_character(9999, _test_path)
	print("PASS: delete_character() on a nonexistent id doesn't crash")

## Covers migrate_legacy_save_data() (see character_save.gd's own comment
## on why it exists -- a real incident this session where renaming the
## project, then separately enabling use_custom_user_dir, each silently
## moved where Godot resolves user:// to and orphaned a real player's
## characters.cfg in the previous location).
func _run_migration_checks() -> void:
	var target_path := "user://test_migrate_target_%d.cfg" % randi()
	var legacy_small := "user://test_migrate_legacy_small_%d.cfg" % randi()
	var legacy_big := "user://test_migrate_legacy_big_%d.cfg" % randi()

	CharacterSave.save_new_character("Solo", "sphere", PackedByteArray(), legacy_small)

	CharacterSave.save_new_character("First", "cube", PackedByteArray(), legacy_big)
	CharacterSave.save_new_character("Second", "sphere", PackedByteArray(), legacy_big)
	CharacterSave.save_new_character("Third", "cube", PackedByteArray(), legacy_big)

	CharacterSave.migrate_legacy_save_data(target_path, [legacy_small, legacy_big])
	var migrated := CharacterSave.list_characters(target_path)
	var migrated_names: Array = migrated.map(func(e): return e["name"])
	if migrated.size() == 3 and migrated_names.has("First") and migrated_names.has("Second") and migrated_names.has("Third"):
		print("PASS: migrate_legacy_save_data() picks the legacy candidate with the MOST characters, not just the first one")
	else:
		_fail("migration picked wrong candidate, got: %s" % [migrated_names])

	# Re-running migration against a target that already has data must be a
	# no-op -- a real player's already-working save file should never get
	# silently clobbered by this self-healing check running again on a
	# later launch.
	CharacterSave.save_new_character("AlreadyHere", "cube", PackedByteArray(), target_path)
	CharacterSave.migrate_legacy_save_data(target_path, [legacy_small, legacy_big])
	var after_second_call := CharacterSave.list_characters(target_path)
	if after_second_call.size() == 4:
		print("PASS: migrate_legacy_save_data() is a no-op once the target already has real data")
	else:
		_fail("migration ran again against a non-empty target, size is now %d (expected 4)" % after_second_call.size())

	# A target with no matching legacy candidates at all (or none of them
	# exist) must not crash and must leave the target untouched/empty.
	var empty_target := "user://test_migrate_empty_%d.cfg" % randi()
	CharacterSave.migrate_legacy_save_data(empty_target, ["user://test_migrate_nonexistent_%d.cfg" % randi()])
	if CharacterSave.list_characters(empty_target).is_empty():
		print("PASS: migrate_legacy_save_data() with no valid candidates leaves the target empty, doesn't crash")
	else:
		_fail("migration somehow produced data from a nonexistent candidate")

	for path in [target_path, legacy_small, legacy_big, empty_target]:
		var dir := DirAccess.open("user://")
		if dir != null and dir.file_exists(path.trim_prefix("user://")):
			dir.remove(path.trim_prefix("user://"))

func _run_ui_checks() -> void:
	var select: Control = CharacterSelectScene.instantiate()
	select.save_path = _test_path
	add_child(select) # triggers _ready(), which populates rows from _test_path (already has 3 entries: Aiko, Boro, Playtime Tester)

	var play_button: Button = select.get_node("Panel/VBox/PlayButton")

	if select._rows.size() == 3:
		print("PASS: character_select.gd builds one row per saved character")
	else:
		_fail("character_select.gd built %d rows, expected 3" % select._rows.size())

	if play_button.disabled:
		print("PASS: Play button starts disabled with nothing selected")
	else:
		_fail("Play button wasn't disabled before any selection")

	var first_id: int = select._rows.keys()[0]
	var first_panel: PanelContainer = select._rows[first_id]["panel"]
	var click := InputEventMouseButton.new()
	click.button_index = MOUSE_BUTTON_LEFT
	click.pressed = true
	first_panel.gui_input.emit(click)

	if not play_button.disabled:
		print("PASS: clicking a row enables the Play button")
	else:
		_fail("Play button is still disabled after clicking a row")

	if select._selected_id == first_id:
		print("PASS: clicking a row records it as the selection")
	else:
		_fail("_selected_id is %d after clicking row %d" % [select._selected_id, first_id])

	# A single-element Array, not a plain int, as the mutable "box" a lambda
	# writes into -- confirmed directly (via a since-removed debug print)
	# that GDScript lambdas capture enclosing local variables BY VALUE, not
	# by reference: `func(id): emitted_id = id` compiled and ran correctly
	# (the lambda really did receive the right id), but the assignment only
	# ever mutated the lambda's own private copy, never the outer variable.
	# An Array's CONTENTS, unlike a plain int binding, are a shared
	# reference, so mutating emitted_id_box[0] from inside the lambda
	# actually is visible out here.
	var emitted_id_box := [-1]
	select.character_selected.connect(func(id: int): emitted_id_box[0] = id)
	play_button.pressed.emit()
	if emitted_id_box[0] == first_id:
		print("PASS: pressing Play emits character_selected with the correct saved character's id")
	else:
		_fail("character_selected fired with id=%d, expected %d" % [emitted_id_box[0], first_id])

	# Portrait rendering (see _capture_one_portrait) needs several real
	# frames per row to actually produce pixels -- waiting generously here
	# rather than trying to synchronize exactly on the last await inside it.
	# Whether a row actually ENDS UP with a real texture assigned depends on
	# a working rendering backend being available -- true when this is run
	# normally, but NOT under --headless, which forces Godot's dummy
	# renderer (SubViewport.get_texture().get_image() can't produce real
	# pixels there, same fundamental "no real backend in this environment"
	# limitation test_voice_chat_check.gd already documents for the
	# headless/dummy audio driver). So the bar here is the same as that
	# file's: the whole capture pipeline (instantiate throwaway Character,
	# pose it, snap the viewport, free it, repeat per row) must run start to
	# finish without crashing or hanging, for every saved character,
	# regardless of whether real pixels come out the other end.
	for i in range(10):
		await get_tree().process_frame
	print("PASS: portrait rendering runs to completion for every row without crashing or hanging")

	await _run_delete_ui_checks(select, first_id)

	select.queue_free()

## Delete button + "are you sure" confirm-overlay flow -- Cancel must leave
## the character on disk untouched, confirming must actually remove it (both
## from disk AND the row list), matching character_creator.gd's own
## confirm-discard overlay precedent character_select.gd's delete prompt was
## modeled on.
func _run_delete_ui_checks(select: Control, first_id: int) -> void:
	var delete_button: Button = select.get_node("Panel/VBox/DeleteButton")
	var confirm_overlay: ColorRect = select.get_node("ConfirmDeleteOverlay")
	var confirm_delete_button: Button = select.get_node("ConfirmDeleteOverlay/ConfirmPanel/VBox/ButtonRow/ConfirmDeleteButton")
	var confirm_cancel_button: Button = select.get_node("ConfirmDeleteOverlay/ConfirmPanel/VBox/ButtonRow/ConfirmCancelButton")

	if not delete_button.disabled:
		print("PASS: Delete button is enabled once a row is selected (still selected from the Play check above)")
	else:
		_fail("Delete button should be enabled once a row is selected")

	if not confirm_overlay.visible:
		print("PASS: the confirm-delete overlay starts hidden")
	else:
		_fail("confirm-delete overlay shouldn't be visible before Delete is pressed")

	delete_button.pressed.emit()
	if confirm_overlay.visible:
		print("PASS: pressing Delete opens the are-you-sure confirmation instead of deleting immediately")
	else:
		_fail("confirm-delete overlay didn't open after pressing Delete")

	confirm_cancel_button.pressed.emit()
	var after_cancel: Array[Dictionary] = CharacterSave.list_characters(_test_path)
	if not confirm_overlay.visible and after_cancel.size() == 3:
		print("PASS: Cancel dismisses the prompt without deleting anything")
	else:
		_fail("Cancel either left the overlay open or deleted the character anyway (size=%d)" % after_cancel.size())

	delete_button.pressed.emit()
	confirm_delete_button.pressed.emit()
	var after_confirm: Array[Dictionary] = CharacterSave.list_characters(_test_path)
	if after_confirm.size() == 2 and not after_confirm.any(func(e): return e["id"] == first_id):
		print("PASS: confirming Delete removes the character from disk")
	else:
		_fail("character wasn't actually deleted from disk after confirming, size=%d" % after_confirm.size())

	if not confirm_overlay.visible:
		print("PASS: confirming Delete also closes the overlay")
	else:
		_fail("confirm-delete overlay stayed open after confirming")

	if select._rows.size() == 2 and not select._rows.has(first_id):
		print("PASS: the deleted character's row is gone after the list refreshes")
	else:
		_fail("row list wasn't refreshed correctly after delete, has %d rows" % select._rows.size())

	if delete_button.disabled and select._selected_id == -1:
		print("PASS: Delete button/selection reset after the list refreshes post-delete")
	else:
		_fail("Delete button or _selected_id wasn't reset after the post-delete refresh")

	# The refresh above kicked off a fresh portrait-render pass for the
	# remaining 2 rows -- let it run to completion before this scene frees
	# itself, same reasoning as the earlier portrait-render wait.
	for i in range(10):
		await get_tree().process_frame

func _cleanup() -> void:
	var dir := DirAccess.open("user://")
	if dir != null and dir.file_exists(_test_path.trim_prefix("user://")):
		dir.remove(_test_path.trim_prefix("user://"))
