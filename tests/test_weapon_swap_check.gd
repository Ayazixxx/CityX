extends Node
## Regression check for a real reported bug: picking up a second weapon
## while already carrying one left BOTH parented under the same hand
## attachment at once ("it lets you carry both sword at once"). Confirms
## equipping a second weapon frees the first (not just leaves it dangling
## alongside), for both same-type (greatsword -> greatsword) and
## cross-type (greatsword -> martyr blade, and back) swaps.

const GreatswordScene := preload("res://weapons/greatsword.tscn")
const MartyrBladeScene := preload("res://weapons/martyr_blade.tscn")
const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _ok := true
var _player: CharacterBody3D
var _character: Node3D
var _hand: Node3D

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	_player = PlayerScene.instantiate()
	add_child(_player)
	_character = CharacterScene.instantiate()
	_character.set_paintable(false)
	_player.add_child(_character)
	_hand = _character.get_hand_node()

	call_deferred("_run_checks")

func _run_checks() -> void:
	# Same-type swap: greatsword -> a second greatsword.
	var sword_a := GreatswordScene.instantiate()
	add_child(sword_a)
	sword_a._equip(_player)
	await get_tree().process_frame # let queue_free() from the FIRST equip (none yet) settle -- harmless no-op here, keeps timing consistent with the swaps below

	var sword_b := GreatswordScene.instantiate()
	add_child(sword_b)
	sword_b._equip(_player)
	await get_tree().process_frame # let sword_a's queue_free() actually take effect

	if _hand.get_child_count() == 1 and _hand.get_children()[0] == sword_b:
		print("PASS: equipping a second greatsword replaces the first (same-type swap)")
	else:
		_fail("hand has %d children after a same-type swap (expected exactly 1, sword_b)" % _hand.get_child_count())

	# Cross-type swap: greatsword -> martyr blade.
	var martyr := MartyrBladeScene.instantiate()
	add_child(martyr)
	martyr._equip(_player)
	await get_tree().process_frame

	if _hand.get_child_count() == 1 and _hand.get_children()[0] == martyr:
		print("PASS: equipping the martyr blade replaces an equipped greatsword (cross-type swap)")
	else:
		_fail("hand has %d children after a cross-type swap (expected exactly 1, martyr)" % _hand.get_child_count())

	# Swap back: martyr blade -> greatsword.
	var sword_c := GreatswordScene.instantiate()
	add_child(sword_c)
	sword_c._equip(_player)
	await get_tree().process_frame

	if _hand.get_child_count() == 1 and _hand.get_children()[0] == sword_c:
		print("PASS: equipping a greatsword replaces an equipped martyr blade (swap back)")
	else:
		_fail("hand has %d children after swapping back (expected exactly 1, sword_c)" % _hand.get_child_count())

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
