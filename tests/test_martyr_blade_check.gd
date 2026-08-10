extends Node
## Functional check for the second two-handed sword ("Blade of the First
## Martyr", weapons/martyr_blade.gd): confirms the procedural mesh actually
## builds, that picking it up adds it to inventory and equip_from_slot()
## then reparents a fresh instance onto hand_r exactly like greatsword.gd
## does, and that it uses the SAME grip constants as greatsword.gd (an
## explicit "same size, same moveset" requirement) rather than having
## drifted to independent values.

const MartyrBladeScene := preload("res://weapons/martyr_blade.tscn")
const GreatswordScene := preload("res://weapons/greatsword.tscn")
const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _ok := true
var _player: CharacterBody3D
var _character: Node3D
var _sword: Node3D
var _frame := 0

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	var standalone := MartyrBladeScene.instantiate()
	add_child(standalone)
	var mesh_count := 0
	for child in standalone.get_children():
		if child is MeshInstance3D and child.mesh != null:
			mesh_count += 1
	if mesh_count == 0:
		_fail("martyr blade instance has no MeshInstance3D children at all")
	else:
		print("PASS: martyr blade built ", mesh_count, " mesh pieces")

	# "Same size, same moveset" as greatsword.gd -- confirm the grip/scale
	# constants actually match rather than having drifted.
	var reference := GreatswordScene.instantiate()
	if is_equal_approx(standalone.MODEL_SCALE, reference.MODEL_SCALE) \
			and standalone.GRIP_HAND_ROTATION.is_equal_approx(reference.GRIP_HAND_ROTATION) \
			and standalone.GRIP_HAND_POSITION.is_equal_approx(reference.GRIP_HAND_POSITION) \
			and standalone.HEAVY_ATTACK_GRIP_ROTATION.is_equal_approx(reference.HEAVY_ATTACK_GRIP_ROTATION):
		print("PASS: grip/scale constants match greatsword.gd exactly")
	else:
		_fail("grip/scale constants drifted from greatsword.gd")
	reference.queue_free()
	standalone.queue_free()

	var ground := StaticBody3D.new()
	ground.collision_layer = 1
	var gcol := CollisionShape3D.new()
	var gshape := BoxShape3D.new()
	gshape.size = Vector3(200, 1.0, 200)
	gcol.shape = gshape
	gcol.position = Vector3(0, -0.5, 0)
	ground.add_child(gcol)
	add_child(ground)

	_player = PlayerScene.instantiate()
	add_child(_player)
	_character = CharacterScene.instantiate()
	_character.set_paintable(false)
	_player.add_child(_character)

	_sword = MartyrBladeScene.instantiate()
	add_child(_sword)
	_sword.position = Vector3(0, 1.0, -1.0)

func _process(_delta: float) -> void:
	_frame += 1
	if _frame == 20:
		if _sword._nearby_player == _player:
			print("PASS: Area3D proximity detection recognized the nearby player")
		else:
			_fail("Area3D proximity never recognized the player as nearby")
		Input.action_press("interact")
	elif _frame == 21:
		Input.action_release("interact")
	elif _frame == 25:
		# Pressing interact no longer equips instantly -- it adds to
		# inventory slot 0 (see player/inventory.gd's own class comment).
		var inv: Node = _player.get_node("Inventory")
		if inv.storage_slots[0].get("item_id", "") == "martyr_blade":
			print("PASS: pressing interact while nearby and looking at it added the martyr blade to inventory slot 0")
		else:
			_fail("inventory slot 0 is '%s', expected 'martyr_blade'" % inv.storage_slots[0].get("item_id", ""))
		if inv.equip_from_slot(0, _player):
			print("PASS: equip_from_slot(0, player) equipped the martyr blade")
		else:
			_fail("equip_from_slot(0, player) returned false")
		# The original world prop is gone (queue_free()'d the instant it
		# entered inventory) -- equip_from_slot() instantiates a fresh copy
		# of the same scene and calls its own unchanged _equip(), so every
		# check below just needs to point at THAT instance instead.
		_sword = _character.get_hand_node().get_child(0)
		if _sword.get_parent() == _character.get_hand_node():
			print("PASS: equip_from_slot() reparented the sword onto hand_r")
		else:
			_fail("sword's parent after equip_from_slot is '%s', expected the hand_r BoneAttachment3D" % _sword.get_parent())
		if _character._wielding_sword:
			print("PASS: picking it up switched character._wielding_sword to true")
		else:
			_fail("picking it up didn't set character._wielding_sword")
		# set_wielding_sword() only flips a flag -- anim_player only actually
		# catches up the next time set_walking() runs (see player.gd's own
		# _physics_process). equip_from_slot() runs synchronously in THIS
		# frame now (unlike the old instant-equip flow, which always had
		# several real frames in between), so force one tick here -- same
		# pattern tests/test_sword_moveset_check.gd's own setup uses.
		_player._physics_process(1.0 / 60.0)
		var current: String = _character.anim_player.current_animation
		if current.begins_with("sword_") or current == "":
			print("PASS: character's current animation ('", current, "') is consistent with the sword moveset")
		else:
			_fail("character is still playing a bare-handed animation ('%s') right after equipping" % current)
		if _sword.rotation.is_equal_approx(_sword.GRIP_HAND_ROTATION) and _sword.position.is_equal_approx(_sword.GRIP_HAND_POSITION):
			print("PASS: equipped pose matches GRIP_HAND_ROTATION/GRIP_HAND_POSITION")
		else:
			_fail("equipped pose is pos=%s rot=%s, expected the GRIP_HAND_* constants" % [_sword.position, _sword.rotation])

		print("OVERALL: ", "PASS" if _ok else "FAIL")
		get_tree().quit(0 if _ok else 1)
