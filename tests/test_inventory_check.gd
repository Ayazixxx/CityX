extends Node3D
## Regression check for player/inventory.gd + items/item_registry.gd +
## ui/inventory_panel.gd: the 32-slot storage cap, stacking, equip/unequip
## round-tripping through a real Character node, the "inventory" key
## toggling the panel, and save/load persistence
## (character_save.gd's own update_inventory()/load_character()). Runs
## headless: pure state-machine checks plus one real Input-driven UI
## toggle, no rendering needed.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")
const CharacterSave = preload("res://character/character_save.gd")

var _ok := true
var _player: CharacterBody3D
var _character: Node

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	_player = PlayerScene.instantiate()
	add_child(_player)
	_character = CharacterScene.instantiate()
	_character.set_paintable(false)
	_player.add_child(_character)
	call_deferred("_run_checks")

func _run_checks() -> void:
	# Runs BEFORE _run_slot_cap_check() deliberately -- that check swaps in
	# a fresh Inventory node (see its own comment), which would otherwise
	# leave InventoryPanel's own cached _inventory reference (set once in
	# its _ready(), before this test ever runs) pointing at a freed object.
	await _run_panel_toggle_check()
	_run_slot_cap_check()
	_run_stacking_check()
	await _run_equip_unequip_check()
	_run_persistence_check()
	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

## 32 non-stackable items (pistol, stack_size=1) each take their own slot;
## the 33rd has nowhere to go and must fail cleanly with zero partial
## trace, matching this project's own "denied action does nothing"
## convention.
func _run_slot_cap_check() -> void:
	var inv: Node = _player.get_node("Inventory")
	for i in range(32):
		if not inv.add_item("pistol", 1):
			_fail("add_item('pistol') #%d unexpectedly failed before the inventory was full" % i)
			return
	var occupied := 0
	for slot in inv.storage_slots:
		if not slot.is_empty():
			occupied += 1
	if occupied == 32:
		print("PASS: 32 non-stackable items filled all 32 storage slots")
	else:
		_fail("expected all 32 slots occupied, got %d" % occupied)

	if inv.add_item("pistol", 1):
		_fail("a 33rd add_item() succeeded despite a full inventory")
	else:
		print("PASS: a 33rd add_item() on a full inventory fails cleanly")

	# Reset for the checks below -- a fresh, empty Inventory in place of the
	# one just filled to capacity above.
	var old_inv := _player.get_node("Inventory")
	_player.remove_child(old_inv)
	old_inv.queue_free()
	var fresh_inv := preload("res://player/inventory.gd").new()
	fresh_inv.name = "Inventory"
	_player.add_child(fresh_inv)

## scrap_metal (stack_size=20) stacks into ONE slot across multiple
## add_item() calls, rather than spreading across several.
func _run_stacking_check() -> void:
	var inv: Node = _player.get_node("Inventory")
	inv.add_item("scrap_metal", 5)
	inv.add_item("scrap_metal", 3)
	var occupied_slots := 0
	var total_count := 0
	for slot in inv.storage_slots:
		if not slot.is_empty() and slot.get("item_id", "") == "scrap_metal":
			occupied_slots += 1
			total_count += slot.get("count", 0)
	if occupied_slots == 1 and total_count == 8:
		print("PASS: two add_item('scrap_metal') calls stacked into one slot (count=8)")
	else:
		_fail("scrap_metal stacking wrong (occupied_slots=%d total_count=%d, expected 1/8)" % [occupied_slots, total_count])

## equip_from_slot() parents a fresh instance onto get_hand_node(); calling
## it again on the SAME slot_key while something's already equipped there
## replaces it via unequip_to_slot() first (verified by round-tripping
## PISTOL out to storage and confirming get_hand_node() is empty again).
func _run_equip_unequip_check() -> void:
	var inv: Node = _player.get_node("Inventory")
	inv.add_item("pistol", 1)
	var pistol_slot_index := -1
	for i in range(inv.storage_slots.size()):
		if inv.storage_slots[i].get("item_id", "") == "pistol":
			pistol_slot_index = i
			break
	if pistol_slot_index == -1:
		_fail("setup failed -- couldn't find the pistol in storage after add_item")
		return

	if not inv.equip_from_slot(pistol_slot_index, _player):
		_fail("equip_from_slot() returned false for a valid pistol slot")
		return
	await get_tree().process_frame

	var hand: Node3D = _character.get_hand_node()
	if hand.get_child_count() > 0 and inv.equipment.get("hand_r", "") == "pistol":
		print("PASS: equip_from_slot() parented a real pistol instance onto get_hand_node() and recorded equipment['hand_r']")
	else:
		_fail("pistol wasn't equipped onto the hand (hand children=%d equipment.hand_r=%s)" % [hand.get_child_count(), inv.equipment.get("hand_r", "")])

	if not inv.unequip_to_slot("hand_r", _player):
		_fail("unequip_to_slot('hand_r') returned false")
		return
	await get_tree().process_frame

	if hand.get_child_count() == 0 and inv.equipment.get("hand_r", "") == "":
		print("PASS: unequip_to_slot() cleared the hand and the equipment dict")
	else:
		_fail("unequip_to_slot() didn't fully clear hand_r (hand children=%d equipment.hand_r=%s)" % [hand.get_child_count(), inv.equipment.get("hand_r", "")])

	var found_in_storage := false
	for slot in inv.storage_slots:
		if slot.get("item_id", "") == "pistol":
			found_in_storage = true
			break
	if found_in_storage:
		print("PASS: the unequipped pistol landed back in general storage")
	else:
		_fail("the unequipped pistol is missing from storage entirely")

## "inventory" (the "I" key) toggles the panel's own dim/pause state, same
## pattern test_pause_menu_check.gd's own _key_event() already establishes:
## Input.action_press() alone never reaches _unhandled_input() (that only
## fires for a real InputEvent flowing through the Viewport's own input
## pipeline, which action_press() doesn't synthesize) -- calling
## _unhandled_input() directly with a real InputEventKey is what actually
## exercises the same code path a real keypress would. physical_keycode
## (not keycode) is what matches, since project.godot's own "inventory"
## action is bound via physical_keycode=73 (see the "open_stats" action's
## own identical format this was copied from).
func _run_panel_toggle_check() -> void:
	var panel: CanvasLayer = _player.get_node_or_null("InventoryPanel")
	if panel == null:
		_fail("no InventoryPanel child found on the player -- was it instantiated in _ready()?")
		return
	var dim: ColorRect = panel.get_node("Dim")

	var ev := InputEventKey.new()
	ev.physical_keycode = KEY_I
	ev.pressed = true
	panel._unhandled_input(ev)

	# Deliberately asserts the tree is NOT paused: this panel used to set
	# get_tree().paused, which is wrong in singleplayer and outright breaks
	# multiplayer (it freezes only this peer). What replaced it is the
	# player's own UI-capture gate, checked right below.
	if dim.visible and not get_tree().paused:
		print("PASS: pressing 'inventory' opens the panel WITHOUT pausing the tree")
	else:
		_fail("pressing 'inventory' didn't open the panel correctly (visible=%s paused=%s)" % [dim.visible, get_tree().paused])

	if _player._input_blocked():
		print("PASS: the open panel blocks the player's own input polling")
	else:
		_fail("panel is open but the player isn't treating its input as blocked")

	panel._unhandled_input(ev)

	if not dim.visible and not get_tree().paused:
		print("PASS: pressing 'inventory' again closes the panel")
	else:
		_fail("pressing 'inventory' a second time didn't close the panel correctly (visible=%s paused=%s)" % [dim.visible, get_tree().paused])

	if not _player._input_blocked():
		print("PASS: closing the panel hands input back to the player")
	else:
		_fail("panel closed but the player is still treating its input as blocked")

## storage_slots/equipment round-trip through character_save.gd's own
## save_new_character -> update_inventory -> load_character cycle intact --
## a throwaway save path, never the real user://characters.cfg (same
## convention every other save/load test in this project follows).
func _run_persistence_check() -> void:
	var test_save_path: String = "user://test_inventory_characters_%d.cfg" % randi()
	var id := CharacterSave.save_new_character("InventoryTester", "sphere", PackedByteArray(), test_save_path)

	var storage: Array = [{"item_id": "scrap_metal", "count": 4, "instance_data": {}}]
	for i in range(31):
		storage.append({})
	var equipment := {"hat": "top_hat", "mask": "", "top": "", "hands": "", "legs": "", "feet": "", "hand_l": "", "hand_r": "pistol"}
	CharacterSave.update_inventory(id, storage, equipment, test_save_path)

	var data: Dictionary = CharacterSave.load_character(id, test_save_path)
	var loaded_storage: Array = data.get("inventory_storage", [])
	var loaded_equipment: Dictionary = data.get("inventory_equipment", {})
	if loaded_storage.size() == 32 and loaded_storage[0].get("item_id", "") == "scrap_metal" and loaded_storage[0].get("count", 0) == 4:
		print("PASS: saved inventory_storage round-trips through save/load intact")
	else:
		_fail("loaded inventory_storage doesn't match what was saved: %s" % loaded_storage)
	if loaded_equipment.get("hat", "") == "top_hat" and loaded_equipment.get("hand_r", "") == "pistol":
		print("PASS: saved inventory_equipment round-trips through save/load intact")
	else:
		_fail("loaded inventory_equipment doesn't match what was saved: %s" % loaded_equipment)
