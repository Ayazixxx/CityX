extends Node3D
## Regression check for a real reported bug: "when you get out of the car
## the character is stuck at an angle." vehicles/car.gd's own _complete_exit
## used to copy just the car's raw rotation.y onto the exiting player,
## leaving their rotation.x/z at whatever the reparent-to-world left them
## at (0, since car.gd resets the player's LOCAL rotation to zero while
## seated) -- fine ONLY if the car itself happened to be perfectly level
## (pure yaw, no pitch/roll) at that exact instant, which a RigidBody3D
## driving over any real ground essentially never is. This test drives the
## point home by TILTING the car (real pitch AND roll, not just yaw) before
## calling _complete_exit() directly (skipping the ~5.8s exiting_car
## animation wait -- this test is only exercising the rotation math itself,
## not the whole seat-transition sequence test_car_enter_exit_check.gd
## already covers end to end), then asserting the player ends up perfectly
## UPRIGHT (rotation.x and .z both ~0) with a yaw matching the car's own
## flattened forward direction.

const MustangScene := preload("res://vehicles/mustang.tscn")
const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _ok := true

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

	var car := MustangScene.instantiate()
	add_child(car)
	car.global_position = Vector3(0, 5.0, 0) # high up, away from the ground -- rotation is set by hand below, no need to let it physically settle first

	# Spawned well away from the car (not co-located) -- both are physics
	# bodies with overlapping layer/mask (car's own mask includes layer 4,
	# characters), and starting them stacked on the exact same point risks
	# the same kind of bogus-deep-penetration explosion a previous car bug
	# already hit (see vehicles/mustang position-before-add_child fix).
	# Reparented onto the seat (with collision zeroed, matching the real
	# _complete_enter()) before any of that ever matters anyway.
	var player := PlayerScene.instantiate()
	player.position = Vector3(20, 5.0, 20)
	add_child(player)
	var character := CharacterScene.instantiate()
	character.set_paintable(false)
	player.add_child(character)

	# Manually replicate what _complete_enter() does (seat the player) --
	# skips the whole entering_car animation wait, since this test is only
	# exercising _complete_exit()'s own rotation math.
	player.get_parent().remove_child(player)
	car._driver_seat.add_child(player)
	player.position = Vector3.ZERO
	player.rotation = Vector3.ZERO
	player.collision_layer = 0
	player.collision_mask = 0
	car._driver = player

	# A real pitch AND roll AND yaw, not just facing some direction level --
	# this is the exact condition ("car isn't perfectly level") that
	# triggered the reported bug.
	car.rotation = Vector3(0.9, 1.1, 0.6)

	# Simulates having looked around with the mouse WHILE driving -- see
	# end_car_transition()'s own comment: _unhandled_input's own mouse-look
	# isn't gated on _seated_car at all, so camera_pivot.rotation.x/y (and
	# _pitch, which keeps re-driving .x) accumulate freely for the whole
	# drive with nothing resetting them, unlike normal on-foot movement's
	# own per-move fold. This is the actual "stuck at an angle" the user
	# saw -- the BODY's own rotation was already correct by itself (see the
	# checks below), it was the CAMERA still carrying leftover look-yaw/
	# pitch from driving that read as "walking around at an angle."
	player.camera_pivot.rotation = Vector3(0.4, 0.3, 0.0)
	player._pitch = 0.3

	call_deferred("_run_check", car, player)

func _run_check(car: RigidBody3D, player: CharacterBody3D) -> void:
	var expected_forward_flat := Vector3(-car.global_transform.basis.z.x, 0.0, -car.global_transform.basis.z.z)
	var expected_yaw: float = Vector3.FORWARD.signed_angle_to(expected_forward_flat, Vector3.UP)
	var camera_yaw_before: float = player.camera_pivot.rotation.y

	car._complete_exit(player, car._driver_seat)

	if absf(player.rotation.x) < 0.01 and absf(player.rotation.z) < 0.01:
		print("PASS: player stands perfectly upright after exiting a tilted car (rotation=", player.rotation, ")")
	else:
		_fail("player is NOT upright after exiting a tilted car (rotation=%s)" % player.rotation)

	var expected_final_yaw: float = expected_yaw + camera_yaw_before
	if absf(angle_difference(player.rotation.y, expected_final_yaw)) < 0.01:
		print("PASS: player's yaw matches the car's own flattened forward direction, folding in leftover look-yaw (yaw=", player.rotation.y, ")")
	else:
		_fail("player's yaw (%s) doesn't match the expected folded yaw (%s)" % [player.rotation.y, expected_final_yaw])

	if player.camera_pivot.rotation.is_equal_approx(Vector3.ZERO):
		print("PASS: camera_pivot's own leftover rotation (both yaw and pitch) was reset after exiting")
	else:
		_fail("camera_pivot still has leftover rotation after exiting (%s)" % player.camera_pivot.rotation)

	if absf(player._pitch) < 0.001:
		print("PASS: _pitch was reset to a neutral level view after exiting")
	else:
		_fail("_pitch wasn't reset after exiting (%s)" % player._pitch)

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
