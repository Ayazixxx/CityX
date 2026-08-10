extends Node
## Regression check for corpse looting (loot/loot_component.gd,
## player.gd's own _process_corpse_loot/_try_loot_nearest_corpse) and its
## tap-vs-hold coexistence with the already-shipped grab/carry system,
## which owns E on the exact same ragdoll bones (see player.gd's own
## class comment on _loot_tap_timer for the full reasoning). Handlers are
## called directly with controlled delta values, same established
## convention tests/test_grab_check.gd already uses ("plain input plumbing,
## already covered") rather than driving real raycasts/timers through
## accumulated real-time frames.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")
const MutantScene := preload("res://npc/mutant.tscn")

var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _make_ground() -> void:
	var ground := StaticBody3D.new()
	ground.collision_layer = 1
	var gcol := CollisionShape3D.new()
	var gshape := BoxShape3D.new()
	gshape.size = Vector3(200, 1.0, 200)
	gcol.shape = gshape
	gcol.position = Vector3(0, -0.5, 0)
	ground.add_child(gcol)
	add_child(ground)

func _make_player(authority_id: int) -> CharacterBody3D:
	var player: CharacterBody3D = PlayerScene.instantiate()
	player.set_multiplayer_authority(authority_id)
	add_child(player)
	var character := CharacterScene.instantiate()
	character.set_paintable(false)
	player.add_child(character)
	return player

func _ready() -> void:
	_make_ground()
	call_deferred("_run_checks")

func _run_checks() -> void:
	await _run_tap_loots_check()
	await _run_hold_never_loots_check()
	await _run_player_corpse_strip_check()
	await _run_npc_loot_table_check()
	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

## A quick tap (release well before GRAB_HOLD_SECONDS) with nothing
## currently carried loots a nearby corpse.
func _run_tap_loots_check() -> void:
	var victim := _make_player(2)
	victim.global_position = Vector3(20, 5.0, 20)
	var victim_character: Node = victim.get_node("Character")
	victim.get_node("Inventory").equip_item_id("top_hat", victim)
	victim_character.ragdoll()

	var looter := _make_player(1)
	looter.global_position = victim.global_position

	var corpse := _find_corpse_near(victim.global_position)
	if corpse == null:
		_fail("setup failed -- no lootable_corpses group member found near the victim after ragdoll()")
		return

	# action_press() doesn't flip is_action_just_pressed() until the
	# FOLLOWING frame (same quirk tests/test_greatsword_check.gd's own
	# comment already documents) -- awaited here since _process_corpse_loot
	# is being called directly rather than through the real
	# _physics_process() cadence that would normally supply that framing.
	Input.action_press("interact")
	await get_tree().physics_frame
	looter._process_corpse_loot(1.0 / 60.0)
	Input.action_release("interact")
	await get_tree().physics_frame
	looter._process_corpse_loot(1.0 / 60.0)

	var inv: Node = looter.get_node("Inventory")
	var found := false
	for slot in inv.storage_slots:
		if slot.get("item_id", "") == "top_hat":
			found = true
			break
	if found:
		print("PASS: a quick tap near a corpse loots it into the looter's own inventory")
	else:
		_fail("a quick tap didn't loot the corpse (storage_slots=%s)" % [inv.storage_slots])

## While _carried_body is already set (a hold successfully grabbed
## something), _process_corpse_loot's own top-of-function guard must block
## looting entirely, even on an otherwise-tap-shaped press/release --
## proves a hold that grabs something never ALSO loots on the same press.
func _run_hold_never_loots_check() -> void:
	var victim := _make_player(4)
	victim.global_position = Vector3(30, 5.0, 30)
	var victim_character: Node = victim.get_node("Character")
	victim.get_node("Inventory").equip_item_id("hockey_mask", victim)
	victim_character.ragdoll()

	var looter := _make_player(3)
	looter.global_position = victim.global_position
	var some_bone: RigidBody3D = victim_character.get_ragdoll_body("hand_r")
	looter._start_carry(some_bone)

	Input.action_press("interact")
	await get_tree().physics_frame
	looter._process_corpse_loot(1.0 / 60.0)
	Input.action_release("interact")
	await get_tree().physics_frame
	looter._process_corpse_loot(1.0 / 60.0)
	looter._drop_carried_body()

	var inv: Node = looter.get_node("Inventory")
	var found := false
	for slot in inv.storage_slots:
		if slot.get("item_id", "") == "hockey_mask":
			found = true
			break
	if not found:
		print("PASS: while carrying (a hold already grabbed something), a press/release near a corpse does NOT also loot it")
	else:
		_fail("looting fired even while _carried_body was set -- grab and loot collided on the same press")

	# A hold long enough to cross GRAB_HOLD_SECONDS -- even if it never
	# actually grabs anything (no _carried_body set at all here), the loot
	# tap must ALSO have been cancelled by the same elapsed hold, matching
	# real gameplay: holding E hoping to grab something that turns out not
	# to be grabbable shouldn't accidentally loot instead. 3 separate calls,
	# matching _process_corpse_loot's own 3-branch shape: one that lands on
	# the "just pressed" branch (arms the tap), one that lands on the
	# "still held" branch with enough delta to cross the threshold (cancels
	# it), and one that lands on "just released" (must find it cancelled).
	var corpse2 := _find_corpse_near(victim.global_position)
	Input.action_press("interact")
	await get_tree().physics_frame
	looter._process_corpse_loot(1.0 / 60.0) # lands on the "just pressed" branch -- arms the tap
	# A second real frame so is_action_just_pressed() clears and
	# is_action_pressed() alone remains true -- otherwise this next call
	# would ALSO land on the "just pressed" branch (which ignores its own
	# delta argument entirely) instead of the "still held" branch that
	# actually accumulates delta toward GRAB_HOLD_SECONDS.
	await get_tree().physics_frame
	looter._process_corpse_loot(looter.GRAB_HOLD_SECONDS + 0.05) # lands on "still held" -- crosses the threshold, cancels the tap
	Input.action_release("interact")
	await get_tree().physics_frame
	looter._process_corpse_loot(1.0 / 60.0) # lands on "just released" -- must find the tap already cancelled
	var still_has_items: bool = corpse2 != null and not corpse2.loot_items.is_empty()
	if still_has_items:
		print("PASS: a hold past GRAB_HOLD_SECONDS never loots, even if it didn't end up grabbing anything either")
	else:
		_fail("a long hold incorrectly looted the corpse despite crossing GRAB_HOLD_SECONDS")

## A dead PLAYER's own equipped gear becomes the corpse's loot_items, AND
## taking it visually strips the corpse (frees the live weapon/wearable
## instance still parented on the dead character's own hand/head node --
## ragdolling never reparents equipped gear away).
func _run_player_corpse_strip_check() -> void:
	var victim := _make_player(6)
	victim.global_position = Vector3(40, 5.0, 40)
	var victim_character: Node = victim.get_node("Character")
	victim.get_node("Inventory").equip_item_id("greatsword", victim)
	await get_tree().process_frame
	var hand: Node3D = victim_character.get_hand_node()
	if hand.get_child_count() == 0:
		_fail("setup failed -- greatsword wasn't equipped onto the victim before ragdolling")
		return

	victim_character.ragdoll()
	var corpse := _find_corpse_near(victim.global_position)
	if corpse == null:
		_fail("setup failed -- no lootable_corpses member found for the player corpse")
		return
	if corpse.loot_items.size() != 1 or corpse.loot_items[0].get("item_id", "") != "greatsword":
		_fail("player corpse's own loot_items don't match what was equipped: %s" % [corpse.loot_items])
		return
	print("PASS: a dead player's own loot_items snapshot exactly what they had equipped")

	var looter := _make_player(5)
	looter.global_position = victim.global_position
	corpse.take_all(looter)
	await get_tree().process_frame

	var looter_inv: Node = looter.get_node("Inventory")
	var got_it := false
	for slot in looter_inv.storage_slots:
		if slot.get("item_id", "") == "greatsword":
			got_it = true
			break
	if got_it:
		print("PASS: take_all() transferred the greatsword into the looter's own inventory")
	else:
		_fail("looter's inventory doesn't contain the greatsword after take_all()")

	if hand.get_child_count() == 0:
		print("PASS: taking the greatsword visually stripped it off the dead player's own corpse")
	else:
		_fail("the dead player's own hand still has a child after being looted -- corpse wasn't stripped")

## Mutant/crimson-claw corpses roll npc/mutant.gd's own LOOT_TABLE into a
## real LootComponent -- no player Inventory/equipment involved at all.
func _run_npc_loot_table_check() -> void:
	var mutant: CharacterBody3D = MutantScene.instantiate()
	mutant.global_position = Vector3(60, 2.0, 60)
	add_child(mutant)
	await get_tree().physics_frame
	mutant.call("_spawn_ragdoll")
	await get_tree().physics_frame

	var corpse := _find_corpse_near(mutant.global_position)
	if corpse == null:
		_fail("no lootable_corpses member found for the mutant corpse")
		return
	if corpse.loot_items.size() == 1 and corpse.loot_items[0].get("item_id", "") == "scrap_metal":
		var count: int = corpse.loot_items[0].get("count", 0)
		if count >= 1 and count <= 3:
			print("PASS: a mutant corpse rolled its own LOOT_TABLE into a real LootComponent (scrap_metal x", count, ")")
		else:
			_fail("mutant loot count out of LOOT_TABLE's own authored range (got %d, expected 1-3)" % count)
	else:
		_fail("mutant corpse's own loot_items don't match LOOT_TABLE: %s" % [corpse.loot_items])

func _find_corpse_near(pos: Vector3) -> Node:
	for node in get_tree().get_nodes_in_group("lootable_corpses"):
		if is_instance_valid(node) and node.is_inside_tree() and node.global_position.distance_to(pos) < 3.0:
			return node
	return null
