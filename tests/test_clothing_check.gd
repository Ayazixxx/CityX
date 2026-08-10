extends Node3D
## Regression check for wearables/clothing.gd (t_shirt.gd/hoodie.gd):
## walking up and pressing E no longer equips instantly -- it adds a
## ground-spawned top to the player's inventory (see player/inventory.gd's
## own class comment), and Inventory.equip_from_slot() is what actually
## calls clothing.gd's own _equip() on a fresh instance, scattering its
## several pieces across character.gd's own get_bone_node() attachments
## (spine/arm_l/arm_r/forearm_l/forearm_r/neck) instead of one single
## parent -- unlike wearables/wearable.gd's head accessories (already
## covered by test_wearable_check.gd), so this needs its own check that
## each piece actually landed on the bone it claims, that a bone's live
## pose actually carries its piece along (the whole point of per-bone
## attachment over one rigid prop), and that equipping a second top
## replaces the first one's scattered pieces (same "top" slot). Runs
## headless: real Player+Character scenes, no rendering needed.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")
const TShirtScene := preload("res://wearables/t_shirt.tscn")
const HoodieScene := preload("res://wearables/hoodie.tscn")
const DevConsoleScene := preload("res://ui/dev_console.tscn")

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

func _bone_name_for_part(part: Node3D) -> String:
	var parent := part.get_parent()
	if parent is BoneAttachment3D:
		return parent.bone_name
	return ""

func _run_checks() -> void:
	# --- get_bone_node() itself: same bone name returns the SAME cached
	# attachment, different bones return different ones. ---
	var spine_a: Node3D = _character.get_bone_node("spine")
	var spine_b: Node3D = _character.get_bone_node("spine")
	var arm_l: Node3D = _character.get_bone_node("arm_l")
	if spine_a == spine_b:
		print("PASS: get_bone_node() caches and returns the same attachment for the same bone")
	else:
		_fail("get_bone_node() returned a different attachment for the same bone name twice")
	if spine_a != arm_l and spine_a is BoneAttachment3D and arm_l is BoneAttachment3D:
		print("PASS: get_bone_node() returns distinct BoneAttachment3D nodes for different bones")
	else:
		_fail("get_bone_node() didn't return distinct attachments for distinct bones")

	# --- T-shirt: spawn, detect proximity, press E, confirm its 4 pieces
	# landed on the right bones. ---
	var shirt := TShirtScene.instantiate()
	add_child(shirt)
	shirt.global_position = _player.global_position + Vector3(0, 0.05, -1.0)
	await get_tree().physics_frame
	await get_tree().physics_frame
	await get_tree().physics_frame

	if shirt._nearby_player == _player:
		print("PASS: t-shirt's InteractArea detected the nearby player")
	else:
		_fail("t-shirt's InteractArea never detected the nearby player")

	Input.action_press("interact")
	await get_tree().physics_frame
	Input.action_release("interact")
	await get_tree().physics_frame
	# clothing.gd's own equip check runs in _process (a render frame, same
	# convention wearable.gd already established -- see
	# test_wearable_check.gd's own comment on why an explicit process_frame
	# wait is needed here, not just physics_frame).
	await get_tree().process_frame
	await get_tree().process_frame

	# Pressing E no longer equips instantly -- it adds to inventory slot 0
	# (see player/inventory.gd's own class comment); equip_from_slot() is
	# what actually calls clothing.gd's own _equip() on a fresh instance,
	# scattering its pieces across the character's own bones.
	var inv: Node = _player.get_node("Inventory")
	if inv.storage_slots[0].get("item_id", "") == "t_shirt":
		print("PASS: pressing E added the t-shirt to inventory slot 0")
	else:
		_fail("inventory slot 0 is '%s', expected 't_shirt'" % inv.storage_slots[0].get("item_id", ""))

	if inv.equip_from_slot(0, _player):
		print("PASS: equip_from_slot(0, player) equipped the t-shirt")
	else:
		_fail("equip_from_slot(0, player) returned false")

	var shirt_parts: Array = _character._worn_top_parts.duplicate()
	if shirt_parts.size() == 4:
		print("PASS: equipping the t-shirt produced its 4 pieces (torso, collar, 2 sleeves)")
	else:
		_fail("expected 4 worn-top parts after equipping the t-shirt, got %d" % shirt_parts.size())

	var shirt_bones := []
	for part in shirt_parts:
		shirt_bones.append(_bone_name_for_part(part))
	shirt_bones.sort()
	if shirt_bones == ["arm_l", "arm_r", "spine", "spine"]:
		print("PASS: the t-shirt's pieces landed on the correct bones (spine x2, arm_l, arm_r)")
	else:
		_fail("t-shirt pieces landed on the wrong bones: %s" % [shirt_bones])

	if not is_instance_valid(shirt) or shirt.get_parent() == null:
		print("PASS: the original t-shirt pickup node was freed after equipping (pieces live under the bones now)")
	else:
		_fail("the original t-shirt node is still alive/parented after equipping")

	# --- Bone-following: rotating "arm_l" for real must actually carry the
	# equipped sleeve piece along with it -- this is the entire point of
	# per-bone attachment instead of one rigid prop under a single bone (see
	# clothing.gd's own class comment). ---
	var sleeve_l: Node3D = null
	for part in shirt_parts:
		if _bone_name_for_part(part) == "arm_l":
			sleeve_l = part
			break
	if sleeve_l == null:
		_fail("couldn't find the t-shirt's left sleeve piece to test bone-following")
	else:
		# The character's own idle AnimationPlayer drives "BodySkeleton:
		# bones/idx/rotation" tracks every frame (see character.gd's own
		# comment on that track path) -- left running, it would stomp a
		# direct set_bone_pose_rotation() call right back to the idle clip's
		# own current keyframe before the next await even resolves. Stopped
		# here so the manual pose actually sticks long enough to check.
		_character.anim_player.stop()
		await get_tree().physics_frame
		var pos_before: Vector3 = sleeve_l.global_position
		var skeleton: Skeleton3D = _character.get_node("BodySkeleton")
		var bone_idx := skeleton.find_bone("arm_l")
		# Re-applied each frame (not just set once) -- BoneAttachment3D's own
		# transform refresh isn't guaranteed to land inside the SAME
		# physics_frame the pose was changed in (same "equip lands in
		# _process, not _physics_process" timing gap test_wearable_check.gd's
		# own comment already flags elsewhere in this project), so re-setting
		# it right up to the moment of the final read sidesteps needing to
		# know exactly which callback actually does the refresh.
		for _i in range(3):
			skeleton.set_bone_pose_rotation(bone_idx, Quaternion(Vector3(0, 0, 1), deg_to_rad(70)))
			await get_tree().physics_frame
			await get_tree().process_frame
		var pos_after: Vector3 = sleeve_l.global_position
		if pos_after.distance_to(pos_before) > 0.05:
			print("PASS: rotating the arm_l bone carried the equipped sleeve piece along with it")
		else:
			_fail("rotating arm_l didn't move the equipped sleeve piece (before=%s after=%s)" % [pos_before, pos_after])
		skeleton.set_bone_pose_rotation(bone_idx, Quaternion.IDENTITY)
		await get_tree().physics_frame

	# --- Hoodie: equipping a SECOND top (same "top" slot) must replace the
	# t-shirt's pieces, not add to them. ---
	var hoodie := HoodieScene.instantiate()
	add_child(hoodie)
	hoodie.global_position = _player.global_position + Vector3(0, 0.05, -1.0)
	await get_tree().physics_frame
	await get_tree().physics_frame
	await get_tree().physics_frame

	Input.action_press("interact")
	await get_tree().physics_frame
	Input.action_release("interact")
	await get_tree().physics_frame
	await get_tree().process_frame
	await get_tree().process_frame

	# Same inventory-then-equip flow as the t-shirt above.
	if inv.storage_slots[0].get("item_id", "") == "hoodie":
		print("PASS: pressing E added the hoodie to inventory slot 0")
	else:
		_fail("inventory slot 0 is '%s', expected 'hoodie'" % inv.storage_slots[0].get("item_id", ""))

	if inv.equip_from_slot(0, _player):
		print("PASS: equip_from_slot(0, player) equipped the hoodie")
	else:
		_fail("equip_from_slot(0, player) returned false")

	# equip_from_slot() -> clothing.gd's own _equip() -> character.gd's own
	# clear_worn_top() frees the t-shirt's pieces via queue_free() --
	# DEFERRED, not immediate -- so they're still momentarily valid right
	# after the call above. A process_frame wait lets that resolve before
	# checking is_instance_valid() below.
	await get_tree().process_frame

	var still_valid_shirt_parts := 0
	for part in shirt_parts:
		if is_instance_valid(part):
			still_valid_shirt_parts += 1
	if still_valid_shirt_parts == 0:
		print("PASS: equipping the hoodie freed all of the t-shirt's own pieces (same slot, replaced)")
	else:
		_fail("%d t-shirt pieces are still alive after equipping the hoodie" % still_valid_shirt_parts)

	var hoodie_parts: Array = _character._worn_top_parts.duplicate()
	if hoodie_parts.size() == 9:
		print("PASS: equipping the hoodie produced its 9 pieces (torso, pocket, hood, 2 drawstrings, 4 sleeve segments)")
	else:
		_fail("expected 9 worn-top parts after equipping the hoodie, got %d" % hoodie_parts.size())

	var hoodie_bones := []
	for part in hoodie_parts:
		hoodie_bones.append(_bone_name_for_part(part))
	hoodie_bones.sort()
	var expected_hoodie_bones := ["arm_l", "arm_r", "forearm_l", "forearm_r", "neck", "spine", "spine", "spine", "spine"]
	if hoodie_bones == expected_hoodie_bones:
		print("PASS: the hoodie's pieces (including the split upper-arm/forearm sleeves) landed on the correct bones")
	else:
		_fail("hoodie pieces landed on the wrong bones: %s" % [hoodie_bones])

	# --- /spawn shirt and /spawn hoodie via the real dev console. ---
	var console := DevConsoleScene.instantiate()
	_player.add_child(console)
	var world := _player.get_parent()
	var shirts_before := 0
	var hoodies_before := 0
	for child in world.get_children():
		if child.get_script() != null:
			var path: String = str(child.get_script().resource_path)
			if path.contains("t_shirt"):
				shirts_before += 1
			elif path.contains("hoodie"):
				hoodies_before += 1
	console._spawn("shirt")
	console._spawn("hoodie")
	var shirts_after := 0
	var hoodies_after := 0
	for child in world.get_children():
		if child.get_script() != null:
			var path: String = str(child.get_script().resource_path)
			if path.contains("t_shirt"):
				shirts_after += 1
			elif path.contains("hoodie"):
				hoodies_after += 1
	if shirts_after == shirts_before + 1:
		print("PASS: /spawn shirt placed a new t_shirt.tscn instance in the world")
	else:
		_fail("/spawn shirt didn't place a new t-shirt in the world")
	if hoodies_after == hoodies_before + 1:
		print("PASS: /spawn hoodie placed a new hoodie.tscn instance in the world")
	else:
		_fail("/spawn hoodie didn't place a new hoodie in the world")

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
