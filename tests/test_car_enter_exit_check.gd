extends Node3D
## Regression check for vehicles/car.gd's animated multi-seat enter/exit
## system: walking up and pressing E seats the player in whichever of
## DriverSeat/PassengerSeat is actually closest to them (not always the
## driver seat), playing character.gd's own entering_car clip first and only
## completing the real possession handoff (hide Character, zero collision,
## reparent onto the seat) once that clip actually finishes -- not the
## instant E is pressed. Only the DRIVER's WASD ever drives the car, a
## passenger is a pure ride-along. Pressing E again while seated plays
## exiting_car and, once THAT finishes, restores the player to the world.
## Runs headless: real Player+Character scenes, real physics frames driving
## real animation playback -- no rendering needed (entering_car/exiting_car
## are baked Animation resources, sampled the same way any other AnimationPlayer
## clip would be, whether or not anything is actually being rendered).

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")
const MustangScene := preload("res://vehicles/mustang.tscn")

var _player: CharacterBody3D
var _character: Node
var _car: RigidBody3D
var _frame := 0
var _phase := 0
var _ok := true
var _hand_l_rot_right := Quaternion.IDENTITY

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	var ground := StaticBody3D.new()
	ground.collision_layer = 1
	var gcol := CollisionShape3D.new()
	var gshape := BoxShape3D.new()
	gshape.size = Vector3(400, 1.0, 400)
	gcol.shape = gshape
	gcol.position = Vector3(0, -0.5, 0)
	ground.add_child(gcol)
	add_child(ground)

	_car = MustangScene.instantiate()
	add_child(_car)
	_car.global_position = Vector3(0, 1.5, 0)

	_player = PlayerScene.instantiate()
	# Position set BEFORE add_child, not after -- confirmed directly (a
	# scratch diagnostic, since deleted) that setting global_position on a
	# physics body AFTER it's already added to the tree leaves a one-physics-
	# frame window where the PhysicsServer still has it registered at its
	# default (origin) transform. With the car ALSO sitting near the origin,
	# that one stale frame is enough for the solver to see a deep bogus
	# penetration and fire off a huge corrective impulse -- a real, very
	# confusing "car flies into the sky the instant a second physics body
	# exists anywhere in the scene, even 20m away" bug, entirely a spawn-
	# ordering artifact and nothing to do with car.gd's own physics.
	_player.position = _car.global_position + Vector3(-2.0, 0, 0)
	add_child(_player)
	_character = CharacterScene.instantiate()
	_character.set_paintable(false)
	_player.add_child(_character)
	# Standing right next to where DriverSeat will end up (left/-X side of
	# the car, see car.gd's own SEAT_X_FRACTION convention) and well within
	# INTERACT_RANGE (4m) of the car's own InteractArea.

func _tap_interact() -> void:
	Input.action_press("interact")

func _release_interact() -> void:
	Input.action_release("interact")

func _physics_process(_delta: float) -> void:
	_frame += 1
	match _phase:
		0: # let both bodies settle and the InteractArea detect proximity
			if _frame >= 10:
				if _car._nearby_player == _player:
					print("PASS: InteractArea detected the nearby player")
				else:
					_fail("InteractArea never detected the nearby player")
				_phase = 1
				_frame = 0
		1: # press E -- should start entering_car, not an instant teleport
			if _frame == 1:
				_tap_interact()
			if _frame == 2:
				_release_interact()
			if _frame >= 5:
				if _car._transitioning and _character._is_car_transitioning and _character.anim_player.current_animation == "entering_car":
					print("PASS: pressing E near the closer (driver) side started entering_car")
				else:
					_fail("entering_car didn't start as expected (transitioning=%s anim=%s)" % [_car._transitioning, _character.anim_player.current_animation])
				if _car._driver == null:
					print("PASS: not yet possessed as driver mid-animation")
				else:
					_fail("driver was assigned before the entering animation even finished")
				_phase = 2
				_frame = 0
		2: # WASD must do nothing while mid-transition, even though nobody's
			# officially "driving" is ambiguous -- the real assertion is that
			# the car doesn't move from held throttle during this window.
			if _frame == 1:
				Input.action_press("move_forward")
			if _frame >= 20:
				Input.action_release("move_forward")
				if _car.linear_velocity.length() < 1.0:
					print("PASS: car didn't respond to throttle while entering mid-animation")
				else:
					_fail("car moved from throttle while still mid-entry animation (velocity %s)" % _car.linear_velocity)
				_phase = 3
				_frame = 0
		3: # run out the rest of entering_car's real length (~5.47s @ 60fps)
			# until animation_finished actually fires and completes the
			# handoff.
			if not _character.anim_player.is_playing() or _car._driver != null:
				if _car._driver == _player:
					print("PASS: driver possession completed once entering_car finished")
				else:
					_fail("driver was never assigned after entering_car finished")
				if _character.visible:
					print("PASS: Character stays visible, seated in the driver's seat")
				else:
					_fail("Character was hidden after being seated (should stay visible)")
				if _player.collision_layer == 0 and _player.collision_mask == 0:
					print("PASS: player collision zeroed while seated")
				else:
					_fail("player collision wasn't zeroed while seated")
				if _player.get_parent() == _car._driver_seat:
					print("PASS: player reparented onto DriverSeat")
				else:
					_fail("player wasn't reparented onto DriverSeat (parent=%s)" % _player.get_parent())
				_phase = 4
				_frame = 0
			elif _frame > 400:
				_fail("entering_car never finished after 400 frames")
				_phase = 4
				_frame = 0

		4: # now that a real driver is seated, WASD should actually drive.
			if _frame == 1:
				if is_equal_approx(_player._spring_arm.spring_length, _player.DRIVING_SPRING_LENGTH):
					print("PASS: camera zoomed out to DRIVING_SPRING_LENGTH while seated")
				else:
					_fail("camera didn't zoom out while seated (spring_length=%s)" % _player._spring_arm.spring_length)
				Input.action_press("move_forward")
				Input.action_press("move_right")
			if _frame == 30:
				var hand_l_idx: int = _character.FusedBody.BONES.find("hand_l")
				_hand_l_rot_right = _character._body_skeleton.get_bone_pose_rotation(hand_l_idx)
				Input.action_release("move_right")
				Input.action_press("move_left")
			if _frame >= 60:
				var hand_l_idx: int = _character.FusedBody.BONES.find("hand_l")
				var hand_l_rot_left: Quaternion = _character._body_skeleton.get_bone_pose_rotation(hand_l_idx)
				if not hand_l_rot_left.is_equal_approx(_hand_l_rot_right):
					print("PASS: driver's hand pose actually changes with steering direction")
				else:
					_fail("driver's hand pose didn't change between steering left and right")
				Input.action_release("move_forward")
				Input.action_release("move_left")
				if _car.linear_velocity.length() > 3.0:
					print("PASS: seated driver's WASD actually drives the car")
				else:
					_fail("car didn't respond to the seated driver's throttle (velocity %s)" % _car.linear_velocity)
				_phase = 5
				_frame = 0
		5: # press E again to exit -- should start exiting_car, not an
			# instant pop-out.
			if _frame == 1:
				_car.linear_velocity = Vector3.ZERO
				_car.angular_velocity = Vector3.ZERO
				_tap_interact()
			if _frame == 2:
				_release_interact()
			if _frame >= 5:
				if _car._transitioning and _character.anim_player.current_animation == "exiting_car":
					print("PASS: pressing E while seated started exiting_car")
				else:
					_fail("exiting_car didn't start as expected")
				if _car._driver == _player:
					print("PASS: still the driver mid-exit-animation (still parented in the seat)")
				else:
					_fail("driver was cleared before the exit animation even finished")
				_phase = 6
				_frame = 0
		6: # run out exiting_car's real length (~5.8s @ 60fps) until it
			# actually completes the reverse handoff.
			if not _character.anim_player.is_playing() or _player.get_parent() != _car._driver_seat:
				if _car._driver == null:
					print("PASS: driver cleared once exiting_car finished")
				else:
					_fail("driver was never cleared after exiting_car finished")
				if _character.visible:
					print("PASS: Character visible again after exiting")
				else:
					_fail("Character is still hidden after exiting")
				if _player.collision_layer != 0 and _player.collision_mask != 0:
					print("PASS: player collision restored after exiting")
				else:
					_fail("player collision wasn't restored after exiting")
				if _player.get_parent() != _car._driver_seat and is_instance_valid(_player.get_parent()):
					print("PASS: player reparented back into the world")
				else:
					_fail("player wasn't reparented back into the world")
				# A real reported bug: "stuck looking left, the model, not
				# the camera" -- exiting_car is the only animation with a
				# real hips ROTATION track (see character.gd's own
				# _on_animation_finished comment), and every other
				# animation (walk/run/idle) never touches that bone at all,
				# so whatever angle exiting_car's own last frame left hips
				# rotated to would otherwise persist forever once nothing
				# else ever resets it.
				var hips_idx: int = _character.FusedBody.BONES.find("hips")
				var hips_rot: Quaternion = _character._body_skeleton.get_bone_pose_rotation(hips_idx)
				if hips_rot.is_equal_approx(Quaternion.IDENTITY):
					print("PASS: hips bone reset to identity after exiting -- body isn't stuck skewed")
				else:
					_fail("hips bone is NOT reset after exiting (rotation=%s) -- body reads as stuck at an angle" % hips_rot)
				_phase = 7
				_frame = 0
			elif _frame > 450:
				_fail("exiting_car never finished after 450 frames")
				_phase = 7
				_frame = 0

		7: # reposition near the PASSENGER (right/+X) side and confirm the
			# nearest-seat logic actually seats them there, not the driver
			# seat, and that a passenger never gets driving control.
			if _frame == 1:
				_player.global_position = _car.global_position + Vector3(2.5, 0, 0)
				_player.velocity = Vector3.ZERO
			if _frame >= 10:
				if _car._nearby_player == _player:
					_tap_interact()
					_phase = 8
					_frame = 0
				elif _frame > 60:
					_fail("InteractArea never redetected the player on the passenger side")
					_phase = 10
					_frame = 0
		8:
			if _frame == 2:
				_release_interact()
			if _frame >= 5:
				if _character.anim_player.current_animation == "entering_car":
					print("PASS: pressing E near the closer (passenger) side started entering_car again")
				else:
					_fail("entering_car didn't start for the passenger-side entry")
				_phase = 9
				_frame = 0
		9:
			if not _character.anim_player.is_playing() or _car._passenger != null:
				if _car._passenger == _player and _car._driver == null:
					print("PASS: seated as PASSENGER, not driver, when closer to that side")
				else:
					_fail("wasn't seated as passenger as expected (driver=%s passenger=%s)" % [_car._driver, _car._passenger])
				_phase = 10
				_frame = 0
			elif _frame > 400:
				_fail("entering_car (passenger) never finished after 400 frames")
				_phase = 10
				_frame = 0
		10:
			if _frame == 1:
				Input.action_press("move_forward")
			if _frame >= 30:
				Input.action_release("move_forward")
				if _car.linear_velocity.length() < 1.0:
					print("PASS: a passenger's WASD does not drive the car")
				else:
					_fail("car moved from a passenger's throttle input (velocity %s)" % _car.linear_velocity)
				print("OVERALL: ", "PASS" if _ok else "FAIL")
				get_tree().quit(0 if _ok else 1)
