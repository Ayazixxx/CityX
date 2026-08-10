extends Node
## Functional check for the new pick-up-able greatsword: verifies the
## procedural mesh actually builds (non-empty, sane triangle count), that
## character.gd's new get_hand_node() returns a real BoneAttachment3D bound
## to "hand_r", and that walking up to the sword, looking at it, and
## pressing "interact" actually adds it to the player's inventory (picking
## something up no longer equips it instantly -- see player/inventory.gd's
## own class comment) -- then that Inventory.equip_from_slot() reparents a
## freshly-instantiated copy onto that hand bone, the same way the future
## inventory UI will equip it.
##
## Relies on the REAL engine advancing real frames (not manually driving
## _physics_process the way some other tests in this project do) --
## Area3D's own body_entered signal needs an actual physics-server step to
## fire, which only happens on real frames the running engine itself
## processes, not from calling a node's _physics_process callback directly.

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
	var standalone := GreatswordScene.instantiate()
	add_child(standalone)
	var mesh_count := 0
	for child in standalone.get_children():
		if child is MeshInstance3D and child.mesh != null:
			mesh_count += 1
	if mesh_count == 0:
		_fail("greatsword instance has no MeshInstance3D children at all")
	else:
		print("PASS: greatsword built ", mesh_count, " mesh pieces")

	var blade_mesh: ArrayMesh = null
	for child in standalone.get_children():
		if child is MeshInstance3D and child.mesh is ArrayMesh and child.mesh.get_surface_count() > 0:
			var arrays: Array = child.mesh.surface_get_arrays(0)
			var verts: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
			if verts.size() > 100: # the blade is by far the biggest mesh piece
				blade_mesh = child.mesh
				break
	if blade_mesh == null:
		_fail("couldn't find the blade's own mesh among the built pieces")
	else:
		var arrays: Array = blade_mesh.surface_get_arrays(0)
		var verts: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
		# 28 slices -> 1 base cap tri + 28*6 ribbon tris = 169 tris = 507 verts
		# (non-indexed, 3 verts/tri, matching this project's own flat-shaded
		# SurfaceTool convention -- see _tri's own comment).
		if verts.size() == 507:
			print("PASS: blade mesh has the expected vertex count (507, 169 flat-shaded triangles)")
		else:
			_fail("blade mesh has an unexpected vertex count (%d, expected 507)" % verts.size())
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

	if not _character.has_method("get_hand_node"):
		_fail("character.gd has no get_hand_node() method")
	else:
		var hand: Node3D = _character.get_hand_node()
		if hand == null:
			_fail("get_hand_node() returned null")
		elif not (hand is BoneAttachment3D):
			_fail("get_hand_node() didn't return a BoneAttachment3D")
		elif hand.bone_name != "hand_r":
			_fail("hand attachment is bound to '%s', expected 'hand_r'" % hand.bone_name)
		else:
			print("PASS: get_hand_node() returns a real BoneAttachment3D bound to hand_r")

	_sword = GreatswordScene.instantiate()
	add_child(_sword)
	# Placed close in front of where the player will stand/look (-Z is
	# forward, matching this project's own convention, and the camera's own
	# rest orientation already points straight down -Z with no mouse input
	# needed).
	_sword.position = Vector3(0, 1.0, -1.0)

func _process(_delta: float) -> void:
	_frame += 1
	if _frame == 20:
		# By now the physics server has had plenty of real frames to detect
		# the Area3D overlap and fire body_entered.
		if _sword._nearby_player == _player:
			print("PASS: Area3D proximity detection recognized the nearby player")
		else:
			_fail("Area3D proximity never recognized the player as nearby")
		# action_press() from script doesn't flip is_action_just_pressed()
		# until the FOLLOWING frame (see test_dash_check.gd's own comment on
		# this exact quirk) -- pressed here, checked a few frames later.
		Input.action_press("interact")
	elif _frame == 21:
		Input.action_release("interact")
	elif _frame == 25:
		# Pressing interact no longer equips instantly -- it adds to
		# inventory slot 0 (see player/inventory.gd's own class comment).
		var inv: Node = _player.get_node("Inventory")
		if inv.storage_slots[0].get("item_id", "") == "greatsword":
			print("PASS: pressing interact while nearby and looking at it added the greatsword to inventory slot 0")
		else:
			_fail("inventory slot 0 is '%s', expected 'greatsword'" % inv.storage_slots[0].get("item_id", ""))

		if inv.equip_from_slot(0, _player):
			print("PASS: equip_from_slot(0, player) equipped the greatsword")
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

		if is_equal_approx(_sword.scale.x, _sword.MODEL_SCALE):
			print("PASS: sword is scaled to MODEL_SCALE (", _sword.MODEL_SCALE, "x)")
		else:
			_fail("sword scale is %s, expected %s" % [_sword.scale, _sword.MODEL_SCALE])

		# character.set_wielding_sword() (called from the fresh instance's own
		# _equip(), just above) only flips the _wielding_sword flag -- it
		# doesn't touch anim_player itself, that only happens the next time
		# set_walking() actually runs (see player.gd's own _physics_process).
		# Under the OLD instant-equip flow there were always several real
		# frames between the world prop's own equip and this check, plenty of
		# time for that to happen naturally; equip_from_slot() now runs
		# synchronously in THIS SAME frame, so a manual tick is needed here --
		# same "force one physics tick after a wielding-state change" pattern
		# tests/test_sword_moveset_check.gd's own setup already establishes.
		_player._physics_process(1.0 / 60.0)

		# The character should now visibly be in the sword moveset -- confirm
		# by checking the currently-playing animation is a sword_* clip, not
		# still a bare-handed one.
		var current: String = _character.anim_player.current_animation
		if current.begins_with("sword_") or current == "":
			print("PASS: character's current animation ('", current, "') is consistent with the sword moveset")
		else:
			_fail("character is still playing a bare-handed animation ('%s') right after equipping" % current)

		# Still idle at this point (never attacked yet) -- should be using
		# the ordinary grip rotation, NOT the attack-specific one. Real
		# feedback was specifically that idle needed to go back to this
		# rotation once a separate attack-only rotation was introduced (see
		# greatsword.gd's own GRIP_HAND_ROTATION comment for the history).
		if _sword.rotation.is_equal_approx(_sword.GRIP_HAND_ROTATION):
			print("PASS: idle (before ever attacking) uses the ordinary GRIP_HAND_ROTATION, not the attack-specific one")
		else:
			_fail("idle rotation is %s, expected GRIP_HAND_ROTATION %s" % [_sword.rotation, _sword.GRIP_HAND_ROTATION])

		# Trigger the charged attack -- see frame 35's own comment for why
		# the actual seek+check is deferred another 10 frames.
		_character.heavy_punch()
	elif _frame == 35:
		# heavy_punch() plays with a BLEND_SECONDS (0.1s = 6 frames) cross-
		# fade -- seeking to the target frame immediately (tried first) caught
		# the pose still mid-blend from whatever was showing before, not the
		# pure sword_heavy_attack pose alone, and failed. Waiting 10 real
		# frames (well past the blend) before seeking means the seek lands
		# on a clean, blend-free pose. GRIP_HAND_ROTATION was specifically
		# solved against this exact frame (see its own comment), so this is
		# the real regression guard for that fix: the ACTUAL sword object's
		# own world-space blade direction, through its full transform chain
		# (hand_r bone pose -> BoneAttachment3D -> the sword's own local
		# rotation), not just a standalone recomputation of the same math.
		_character.anim_player.seek(1.4, true)
	elif _frame == 36:
		# One frame later so the skeleton's cached global pose is fresh
		# after the seek (same seek-then-wait-a-frame reasoning
		# tests/scratch_mixamo_retarget.gd's own retarget() already
		# documents).
		var blade_world_dir: Vector3 = _sword.global_transform.basis.y.normalized()
		var straight_down := blade_world_dir.dot(Vector3.DOWN)
		if straight_down > 0.97: # within ~14 degrees of dead straight down
			print("PASS: the sword's blade points straight down at the charged attack's own peak strike frame (dot=", straight_down, ")")
		else:
			_fail("sword blade direction at the charged attack's peak is %s (dot with straight-down = %s), expected close to straight down" % [blade_world_dir, straight_down])

		if _sword.rotation.is_equal_approx(_sword.HEAVY_ATTACK_GRIP_ROTATION):
			print("PASS: sword's own local rotation switched to HEAVY_ATTACK_GRIP_ROTATION during the swing")
		else:
			_fail("sword rotation during the swing is %s, expected HEAVY_ATTACK_GRIP_ROTATION %s" % [_sword.rotation, _sword.HEAVY_ATTACK_GRIP_ROTATION])

		# Back to idle once the swing finishes -- should switch back to the
		# ordinary grip rotation, not stay stuck on the attack one.
		_character.anim_player.play("sword_idle")
	elif _frame == 37:
		if _sword.rotation.is_equal_approx(_sword.GRIP_HAND_ROTATION):
			print("PASS: sword's rotation switches back to GRIP_HAND_ROTATION once back at sword_idle")
		else:
			_fail("sword rotation back at idle is %s, expected GRIP_HAND_ROTATION %s (didn't switch back)" % [_sword.rotation, _sword.GRIP_HAND_ROTATION])

		print("OVERALL: ", "PASS" if _ok else "FAIL")
		get_tree().quit(0 if _ok else 1)
