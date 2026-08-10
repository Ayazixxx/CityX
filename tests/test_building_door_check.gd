extends Node3D
## Regression check for city/building_door.gd: a closed door really blocks
## the entranceway (not just a decorative panel), an open door doesn't, and
## a real player standing nearby pressing "interact" (E) is what actually
## toggles it. Runs headless: real building generation + real physics
## frames driving a real Tween -- no rendering needed.
##
## Movement/blocking is driven by a plain, fully test-controlled
## CharacterBody3D (same convention tests/test_multifloor_check.gd and
## tests/test_building_glass_check.gd's own walkthrough checks already use)
## rather than a real player/player.gd instance -- a real Player already
## runs its OWN autonomous _physics_process() every frame (reading WASD,
## applying its own gravity), which would fight any velocity this script
## also tried to set directly. The E-press path specifically DOES need a
## real player (building_door.gd's own InteractArea only accepts a body
## with has_method("respawn")), so that part uses one separately, following
## tests/test_car_enter_exit_check.gd's own Input.action_press("interact")
## pattern.

const CityChunk = preload("res://city/city_chunk.gd")
const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _door: Node3D
var _size: Vector3
var _hz: float
var _walker: CharacterBody3D
var _player: CharacterBody3D
var _frame := 0
var _phase := 0
var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	var materials := CityChunk.build_materials()
	_size = Vector3(16, 20, 16)
	_hz = _size.z / 2.0

	var ground := StaticBody3D.new()
	ground.collision_layer = 1
	var gcol := CollisionShape3D.new()
	var gshape := BoxShape3D.new()
	gshape.size = Vector3(60, 1.0, 60)
	gcol.shape = gshape
	gcol.position = Vector3(0, -0.5, 0)
	ground.add_child(gcol)
	add_child(ground)

	CityChunk._build_building(self, _size, Vector3(0, 0, 0), "", materials)

	for child in get_children():
		if child.get_script() != null and str(child.get_script().resource_path).contains("building_door"):
			_door = child
			break
	if _door == null:
		_fail("no building_door.gd node found under the built building")
		call_deferred("_finish")
		return

	_walker = CharacterBody3D.new()
	var wshape := CollisionShape3D.new()
	var wcapsule := CapsuleShape3D.new()
	wcapsule.radius = 0.3
	wcapsule.height = 1.6
	wshape.shape = wcapsule
	_walker.add_child(wshape)
	_walker.collision_layer = 4
	# Doorway's own world X center is 0 (u0/u1 straddle 0, see
	# city_chunk.gd's own door dict) -- starts well outside, facing the
	# door.
	_walker.position = Vector3(0, 1.0, _hz + 3.0)
	add_child(_walker)

func _tap_interact() -> void:
	Input.action_press("interact")

func _release_interact() -> void:
	Input.action_release("interact")

func _physics_process(_delta: float) -> void:
	if _door == null:
		return
	_frame += 1
	match _phase:
		0: # a CLOSED door really blocks the entranceway
			_walker.velocity = Vector3(0, 0, -2.0)
			_walker.move_and_slide()
			if _frame >= 90:
				if _walker.global_position.z > _hz - 0.5:
					print("PASS: a closed door blocks the entranceway (walker stopped at z=", _walker.global_position.z, ")")
				else:
					_fail("walker passed straight through a CLOSED door (z=%s) -- its collision isn't blocking anything" % _walker.global_position.z)
				_phase = 1
				_frame = 0
		1: # open it directly (bypassing the interact path -- that's checked
			# separately below with a real player) and let the swing tween finish
			if _frame == 1:
				_door._toggle()
			if _frame >= 45: # OPEN_SECONDS (0.5s) worth of physics frames, plus margin
				if _door.is_open():
					print("PASS: _toggle() opens the door")
				else:
					_fail("door didn't report open after _toggle()")
				_phase = 2
				_frame = 0
		2: # an OPEN door does NOT block the same entranceway
			_walker.velocity = Vector3(0, 0, -2.0)
			_walker.move_and_slide()
			if _frame >= 90:
				if _walker.global_position.z < _hz - 1.0:
					print("PASS: an open door lets a body walk through into the building (z=", _walker.global_position.z, ")")
				else:
					_fail("walker is still stuck outside an OPEN door (z=%s)" % _walker.global_position.z)
				_phase = 3
				_frame = 0
		3: # closing it again re-blocks the same spot
			if _frame == 1:
				_door._toggle()
			if _frame >= 45:
				if not _door.is_open():
					print("PASS: _toggle() closes the door again")
				else:
					_fail("door didn't report closed after a second _toggle()")
				_spawn_edge_walker()
				_phase = 4
				_frame = 0
		4: # real reported bug: the panel's own collision was centered ON the
			# hinge instead of offset out from it, so it only ever covered
			# roughly the LEFT half of the doorway -- a body approaching the
			# RIGHT (free-swinging) edge would walk straight through a
			# "closed" door. Same closed-door-blocks check as phase 0, but
			# aimed at the doorway's own far edge instead of dead center.
			_edge_walker.velocity = Vector3(0, 0, -2.0)
			_edge_walker.move_and_slide()
			if _frame >= 90:
				if _edge_walker.global_position.z > _hz - 0.5:
					print("PASS: a closed door blocks the doorway's own far edge too, not just the center (z=", _edge_walker.global_position.z, ")")
				else:
					_fail("walker passed through the closed door near its free edge (z=%s) -- the panel's own collision doesn't reach that far" % _edge_walker.global_position.z)
				_spawn_real_player()
				_phase = 5
				_frame = 0
		5: # a real player standing nearby is what the InteractArea should pick up
			if _frame >= 10:
				if _door._nearby_player == _player:
					print("PASS: InteractArea detected the nearby real player")
				else:
					_fail("InteractArea never detected the nearby real player")
				_phase = 6
				_frame = 0
		6: # pressing E (real input, matching tests/test_car_enter_exit_check.gd's
			# own convention) is what actually toggles it, not just proximity
			if _frame == 1:
				_tap_interact()
			if _frame == 2:
				_release_interact()
			if _frame >= 45:
				if _door.is_open():
					print("PASS: pressing E on a nearby door opens it")
				else:
					_fail("door didn't open after a real interact press")
				_finish()

## A second walker, positioned near the doorway's own free (non-hinge)
## edge -- roughly CityChunk.DOOR_WIDTH/2 - 0.2 off-center, still
## comfortably inside the real doorway's own width, but on the exact side
## an off-centered door panel would fail to cover.
var _edge_walker: CharacterBody3D
func _spawn_edge_walker() -> void:
	_edge_walker = CharacterBody3D.new()
	var wshape := CollisionShape3D.new()
	var wcapsule := CapsuleShape3D.new()
	wcapsule.radius = 0.15
	wcapsule.height = 1.6
	wshape.shape = wcapsule
	_edge_walker.add_child(wshape)
	_edge_walker.collision_layer = 4
	var edge_x: float = CityChunk.DOOR_WIDTH / 2.0 - 0.2
	_edge_walker.position = Vector3(edge_x, 1.0, _hz + 3.0)
	add_child(_edge_walker)

## Spawned only once the manual-toggle checks above are done, at the same
## spot the plain walker already proved is right next to the door -- a real
## Player+Character (see this file's own class comment for why the earlier
## movement checks deliberately don't use one).
func _spawn_real_player() -> void:
	_player = PlayerScene.instantiate()
	var character := CharacterScene.instantiate()
	character.set_paintable(false)
	_player.position = Vector3(0, 1.0, _hz + 1.5)
	add_child(_player)
	_player.add_child(character)

func _finish() -> void:
	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
