extends Node3D
## Regression check for wearables/wearable.gd (top_hat.gd/hockey_mask.gd):
## walking up and pressing E no longer equips instantly -- it adds a
## ground-spawned item to the player's inventory (see player/inventory.gd's
## own class comment), and Inventory.equip_from_slot() is what actually
## parents a fresh instance onto character.gd's own get_head_node(). A hat
## and a mask can be worn at the SAME time (different slots, see
## get_wearable_slot()), but equipping a second item in the SAME slot
## replaces the first one (same "one item per slot" rule a weapon's own
## hand slot already enforces). Runs headless: real Player+Character
## scenes, no rendering needed (the look-detection dot-product check only
## needs a real Camera3D transform, not an actual rendered frame).

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")
const TopHatScene := preload("res://wearables/top_hat.tscn")
const HockeyMaskScene := preload("res://wearables/hockey_mask.tscn")

var _player: CharacterBody3D
var _character: Node
var _ok := true

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
	var head: Node3D = _character.get_head_node()

	# --- Top hat: spawn, detect proximity, press E, confirm it's equipped
	# onto the head slot "hat". ---
	var hat := TopHatScene.instantiate()
	add_child(hat)
	hat.global_position = _player.global_position + Vector3(0, 0.05, -1.0)
	await get_tree().physics_frame
	await get_tree().physics_frame
	await get_tree().physics_frame

	if hat._nearby_player == _player:
		print("PASS: top hat's InteractArea detected the nearby player")
	else:
		_fail("top hat's InteractArea never detected the nearby player")

	Input.action_press("interact")
	await get_tree().physics_frame
	Input.action_release("interact")
	await get_tree().physics_frame
	# wearable.gd's own equip check runs in _process (a render frame), not
	# _physics_process -- waiting only on physics_frame signals raced it on
	# the very first attempt (confirmed directly: a later check further
	# down, at a point with more elapsed frames, showed the hat HAD equipped
	# by then after all). An explicit process_frame wait here guarantees at
	# least one real _process() tick has run before checking the result.
	await get_tree().process_frame
	await get_tree().process_frame

	# Pressing E no longer equips instantly -- it adds to inventory slot 0
	# (see player/inventory.gd's own class comment).
	var inv: Node = _player.get_node("Inventory")
	if inv.storage_slots[0].get("item_id", "") == "top_hat":
		print("PASS: pressing E added the top hat to inventory slot 0")
	else:
		_fail("inventory slot 0 is '%s', expected 'top_hat'" % inv.storage_slots[0].get("item_id", ""))

	if inv.equip_from_slot(0, _player):
		print("PASS: equip_from_slot(0, player) equipped the top hat")
	else:
		_fail("equip_from_slot(0, player) returned false")

	# The original world prop is gone (queue_free()'d the instant it entered
	# inventory) -- equip_from_slot() instantiates a fresh copy of the same
	# scene and calls its own unchanged _equip(); re-point `hat` at whatever
	# now sits in the head's "hat" slot instead of the freed original node.
	hat = null
	for child in head.get_children():
		if child.has_method("get_wearable_slot") and child.get_wearable_slot() == "hat":
			hat = child
			break

	if hat != null and hat.get_parent() == head and hat.get_wearable_slot() == "hat":
		print("PASS: equip_from_slot() equipped the top hat onto the head")
	else:
		_fail("top hat wasn't equipped onto the head (hat=%s)" % hat)

	# --- Hockey mask: a DIFFERENT slot, should be wearable AT THE SAME TIME
	# as the hat, not replace it. ---
	var mask := HockeyMaskScene.instantiate()
	add_child(mask)
	mask.global_position = _player.global_position + Vector3(0, 0.05, -1.0)
	await get_tree().physics_frame
	await get_tree().physics_frame
	await get_tree().physics_frame

	Input.action_press("interact")
	await get_tree().physics_frame
	Input.action_release("interact")
	await get_tree().physics_frame
	# wearable.gd's own equip check runs in _process (a render frame), not
	# _physics_process -- waiting only on physics_frame signals raced it on
	# the very first attempt (confirmed directly: a later check further
	# down, at a point with more elapsed frames, showed the hat HAD equipped
	# by then after all). An explicit process_frame wait here guarantees at
	# least one real _process() tick has run before checking the result.
	await get_tree().process_frame
	await get_tree().process_frame

	# Same inventory-then-equip flow as the top hat above.
	if inv.storage_slots[0].get("item_id", "") == "hockey_mask":
		print("PASS: pressing E added the hockey mask to inventory slot 0")
	else:
		_fail("inventory slot 0 is '%s', expected 'hockey_mask'" % inv.storage_slots[0].get("item_id", ""))

	if inv.equip_from_slot(0, _player):
		print("PASS: equip_from_slot(0, player) equipped the hockey mask")
	else:
		_fail("equip_from_slot(0, player) returned false")

	# Re-point `mask` at whatever now sits in the head's "mask" slot -- the
	# original world prop was freed the instant it entered inventory.
	mask = null
	for child in head.get_children():
		if child.has_method("get_wearable_slot") and child.get_wearable_slot() == "mask":
			mask = child
			break

	if mask != null and mask.get_parent() == head:
		print("PASS: equip_from_slot() equipped the hockey mask onto the head too")
	else:
		_fail("hockey mask wasn't equipped onto the head (mask=%s)" % mask)

	if is_instance_valid(hat) and hat.get_parent() == head:
		print("PASS: equipping the mask did NOT remove the already-worn hat (different slot)")
	else:
		_fail("equipping the mask incorrectly removed the hat")

	# --- A second hat should REPLACE the first one (same slot). ---
	var hat2 := TopHatScene.instantiate()
	add_child(hat2)
	hat2.global_position = _player.global_position + Vector3(0, 0.05, -1.0)
	await get_tree().physics_frame
	await get_tree().physics_frame
	await get_tree().physics_frame

	Input.action_press("interact")
	await get_tree().physics_frame
	Input.action_release("interact")
	await get_tree().physics_frame
	# wearable.gd's own equip check runs in _process (a render frame), not
	# _physics_process -- waiting only on physics_frame signals raced it on
	# the very first attempt (confirmed directly: a later check further
	# down, at a point with more elapsed frames, showed the hat HAD equipped
	# by then after all). An explicit process_frame wait here guarantees at
	# least one real _process() tick has run before checking the result.
	await get_tree().process_frame
	await get_tree().process_frame

	# Same inventory-then-equip flow again -- picking up hat2 adds a second
	# "top_hat" to inventory slot 0 (the first one refilled it the instant
	# equip_from_slot() emptied it above), then equip_from_slot() replaces
	# whatever's currently worn in the "hat" slot (the first hat's own
	# equipped instance) with a fresh instance of this pickup.
	if inv.storage_slots[0].get("item_id", "") == "top_hat":
		print("PASS: pressing E added the second top hat to inventory slot 0")
	else:
		_fail("inventory slot 0 is '%s', expected 'top_hat'" % inv.storage_slots[0].get("item_id", ""))

	var first_hat_instance: Node3D = hat
	if inv.equip_from_slot(0, _player):
		print("PASS: equip_from_slot(0, player) equipped the second top hat")
	else:
		_fail("equip_from_slot(0, player) returned false")

	# equip_from_slot() -> unequip_to_slot() -> character.clear_head_slot()
	# frees the first hat via queue_free() -- DEFERRED, not immediate -- so
	# right after the call above both the old and new hat are still
	# momentarily children of head at once. A process_frame wait lets that
	# deferred free actually resolve before searching head's children below,
	# so the search unambiguously lands on the new instance, not the stale
	# about-to-be-freed one.
	await get_tree().process_frame

	hat2 = null
	for child in head.get_children():
		if child.has_method("get_wearable_slot") and child.get_wearable_slot() == "hat":
			hat2 = child
			break

	if hat2 != null and is_instance_valid(hat2) and hat2.get_parent() == head:
		print("PASS: a second hat was equipped")
	else:
		_fail("second hat wasn't equipped")

	if not is_instance_valid(first_hat_instance) or first_hat_instance.get_parent() != head:
		print("PASS: equipping a second hat replaced the first one (same slot)")
	else:
		_fail("first hat is still worn after equipping a second one (same slot should replace)")

	if is_instance_valid(mask) and mask.get_parent() == head:
		print("PASS: the mask is still worn after the hat got replaced (different slot, untouched)")
	else:
		_fail("the mask was incorrectly removed when a new hat was equipped")

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
