extends Node3D
## Regression check for items/flashlight.gd.
##
## THE BUG THIS EXISTS FOR: "holding a flashlight messes up the running
## animation, it's like the model is getting caught on it."
##
## It wasn't the animation. A RigidBody3D defaults to collision_layer 1 --
## the WORLD GEOMETRY layer that buildings and the ground use -- and the
## flashlight never set its own. player.gd's own body masks exactly layer 1,
## so once the flashlight was reparented into the hand bone, the player's
## own move_and_slide() treated the torch in its own fist as a wall: it
## sweeps through the player capsule with every stride of the run cycle and
## the solver shoves the player off it.
##
## Every other held RigidBody3D in this project (weapons/glass_bottle.gd,
## weapons/thrown_bottle.gd) already sets layer 16, "small dynamic prop", for
## exactly this reason. This checks the INVARIANT rather than the symptom:
## nothing you can hold may sit on a layer your own body collides with. That
## catches the same mistake on the next held item, which "does the run
## animation look right" never would.

const FlashlightScene := preload("res://items/flashlight.tscn")
const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	call_deferred("_run_checks")

func _run_checks() -> void:
	_build_ground()
	await _check_it_is_not_on_a_layer_the_player_collides_with()
	await _check_it_is_still_grabbable_and_still_falls()
	await _check_holding_it_does_not_shove_the_player()

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

## --- 1. THE invariant. Stated as a bitmask relationship rather than as a
## magic number, so it keeps holding if either side is ever re-layered. ---
func _check_it_is_not_on_a_layer_the_player_collides_with() -> void:
	var player: CharacterBody3D = PlayerScene.instantiate()
	add_child(player)
	var torch: RigidBody3D = FlashlightScene.instantiate()
	add_child(torch)
	await get_tree().process_frame

	var overlap: int = torch.collision_layer & player.collision_mask
	if overlap != 0:
		_fail("the flashlight sits on layer(s) 0x%x that the player's own body masks -- held in the hand it acts as a wall and shoves the player around"
			% overlap)
	else:
		print("PASS: the flashlight's layer (%d) shares nothing with the player body's mask (%d), so it can't collide with its own holder"
			% [torch.collision_layer, player.collision_mask])

	player.queue_free()
	torch.queue_free()
	await get_tree().process_frame

## --- 2. ...but the layer change must not have cost it anything. It still
## has to be pick-up-able by the grab raycast, and still has to fall onto
## the world rather than hanging in the air. ---
func _check_it_is_still_grabbable_and_still_falls() -> void:
	var torch: RigidBody3D = FlashlightScene.instantiate()
	add_child(torch)
	torch.global_position = Vector3(0, 4.0, 0)
	await get_tree().process_frame

	# player.gd's own _find_grab_target casts against 1|8|16.
	const GRAB_MASK := 1 | 8 | 16
	if torch.collision_layer & GRAB_MASK == 0:
		_fail("the flashlight is invisible to the grab raycast (layer %d vs mask %d)" % [torch.collision_layer, GRAB_MASK])
	elif not torch.is_in_group("grabbable"):
		_fail("the flashlight isn't tagged grabbable")
	else:
		print("PASS: still on a layer the grab raycast sees, and still tagged grabbable")

	# collision_MASK is what it collides WITH -- untouched, so it still
	# lands on the ground instead of floating.
	if torch.collision_mask & 1 == 0:
		_fail("the flashlight no longer collides with world geometry -- it would fall through the floor")
	else:
		var start_y := torch.global_position.y
		for _i in range(180):
			await get_tree().physics_frame
		var settled := torch.global_position.y
		if settled >= start_y - 0.5:
			_fail("the flashlight didn't fall (y %.2f -> %.2f)" % [start_y, settled])
		elif settled < -1.0:
			_fail("the flashlight fell through the ground (y %.2f)" % settled)
		else:
			print("PASS: it still falls and comes to rest on the ground (y %.2f -> %.2f)" % [start_y, settled])

	torch.queue_free()
	await get_tree().process_frame

## --- 3. An end-to-end smoke check of the equip path: the torch really
## reparents into the hand, the run clip really drives, and the player is
## still standing where it was left.
##
## STATED PLAINLY: this does NOT catch the layer bug. It was run against the
## broken version to find out, and reported the same 0.000m drift either way
## -- a CharacterBody3D given no input never moves, so move_and_slide() never
## resolves against the torch and the shove never happens. Check 1 above is
## the one with teeth; this one only proves the equip path is intact. Kept
## deliberately labelled rather than dressed up as a behavioural proof it
## isn't. ---
func _check_holding_it_does_not_shove_the_player() -> void:
	var player: CharacterBody3D = PlayerScene.instantiate()
	add_child(player)
	player.global_position = Vector3(0, 0.2, 0)
	player.add_to_group("players")
	# player.tscn ships WITHOUT a Character -- main.gd's own
	# _place_character_on adds it at spawn time. The flashlight's _equip
	# needs one to find a hand bone, so the test has to build the same
	# arrangement the real spawn path does.
	var character: Node3D = CharacterScene.instantiate()
	character.set_paintable(false)
	player.add_child(character)
	await get_tree().process_frame
	await get_tree().process_frame

	var torch: RigidBody3D = FlashlightScene.instantiate()
	add_child(torch)
	await get_tree().process_frame
	torch._equip(player)
	await get_tree().process_frame

	if torch.get_parent() == self:
		_fail("the flashlight was never reparented into the hand -- this check would prove nothing")
		player.queue_free()
		torch.queue_free()
		return
	# Let it settle onto the ground first, so the drift measured below is
	# the torch and not the initial drop.
	for _i in range(30):
		await get_tree().physics_frame

	var start: Vector3 = player.global_position
	for _i in range(180):
		# Re-asserted every frame: the run clip is what swings the hand the
		# torch is parented to, and that swing is what used to do the
		# shoving. A still character would never reproduce it.
		if character.has_method("set_walking"):
			character.set_walking(true, true, false)
		await get_tree().physics_frame

	var drift := Vector2(player.global_position.x - start.x, player.global_position.z - start.z).length()
	if drift > 0.25:
		_fail("holding the flashlight pushed the player %.2fm with no input" % drift)
	else:
		print("PASS: the equip path is intact -- torch in the hand, run clip driving, player still put (%.3fm)" % drift)

	player.queue_free()
	await get_tree().process_frame

func _build_ground() -> void:
	var ground := StaticBody3D.new()
	var shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(80, 1, 80)
	shape.shape = box
	shape.position = Vector3(0, -0.5, 0)
	ground.add_child(shape)
	ground.collision_layer = 1
	add_child(ground)
