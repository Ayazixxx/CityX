extends Node3D
## Regression check for vehicles/car.gd's Stage 1 bare-physics core (see the
## approved vehicle plan's "Core physics algorithm" section): forward
## engine force actually moves the car, drag/rolling resistance decay speed
## once throttle's released, steering curves the car toward the side
## actually pressed (this is the one the plan's own comment explicitly
## warned not to just assume the sign of -- verified here empirically
## against real simulated frames, not derived on paper), the handbrake
## drastically weakens lateral grip (the spec's own "heart of it" mechanic)
## while normal grip cancels a lateral slide fast, and an airborne car (wheel
## rays not touching ground) doesn't respond to throttle. Runs headless:
## pure RigidBody3D physics, no rendering needed.

const MustangScene := preload("res://vehicles/mustang.tscn")

var _car: RigidBody3D
var _frame := 0
var _phase := 0
var _ok := true
var _speed_before := 0.0
var _lateral_before := 0.0

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
	# Real driving input is now gated on an actual seated driver (see
	# car.gd's own _integrate_forces comment on the "works like an RC car"
	# fix) -- this test is exercising the raw PHYSICS response to WASD, not
	# the interact/possession system itself, so a bare placeholder body
	# (added to the tree only so it gets cleaned up at teardown like
	# everything else here, never actually driven through the real enter
	# sequence) is enough to satisfy the null-check that gate reads.
	var dummy_driver := CharacterBody3D.new()
	add_child(dummy_driver)
	_car._driver = dummy_driver

func _horiz(v: Vector3) -> Vector2:
	return Vector2(v.x, v.z)

func _reset_car(pos: Vector3, yaw: float) -> void:
	_car.global_position = pos
	_car.rotation = Vector3(0, yaw, 0)
	_car.linear_velocity = Vector3.ZERO
	_car.angular_velocity = Vector3.ZERO

func _physics_process(_delta: float) -> void:
	_frame += 1
	match _phase:
		0: # let the dropped car land and settle
			if _frame >= 90:
				_phase = 1
				_frame = 0
		1: # holding forward should build real forward speed and move -Z
			if _frame == 1:
				Input.action_press("move_forward")
			if _frame >= 90:
				Input.action_release("move_forward")
				var speed: float = _horiz(_car.linear_velocity).length()
				if speed > 5.0 and _car.global_position.z < -3.0:
					print("PASS: throttle built forward speed and moved the car (speed ", speed, ", z ", _car.global_position.z, ")")
				else:
					print("FAIL: throttle didn't move the car as expected (speed ", speed, ", z ", _car.global_position.z, ")")
					_ok = false
				_speed_before = speed
				_phase = 2
				_frame = 0
		2: # releasing throttle should decay speed via drag/rolling resistance
			if _frame >= 60:
				var speed: float = _horiz(_car.linear_velocity).length()
				if speed < _speed_before - 1.0:
					print("PASS: releasing throttle decayed speed (", _speed_before, " -> ", speed, ")")
				else:
					print("FAIL: speed didn't decay after releasing throttle (", _speed_before, " -> ", speed, ")")
					_ok = false
				_reset_car(Vector3(0, 1.5, 0), 0.0)
				_phase = 3
				_frame = 0
		3: # settle after reset
			if _frame >= 20:
				_phase = 4
				_frame = 0
		4: # forward + steer right (D) should curve the car toward world +X
			# (it starts facing -Z, so curving "right" bends its path toward +X)
			if _frame == 1:
				Input.action_press("move_forward")
				Input.action_press("move_right")
			if _frame >= 120:
				Input.action_release("move_forward")
				Input.action_release("move_right")
				var x: float = _car.global_position.x
				if x > 1.5:
					print("PASS: steering right curved the car toward +X (x=", x, ")")
				else:
					print("FAIL: steering right didn't curve the car as expected (x=", x, ")")
					_ok = false
				_reset_car(Vector3(0, 1.5, 0), 0.0)
				_phase = 5
				_frame = 0
		5: # settle after reset
			if _frame >= 20:
				_phase = 6
				_frame = 0
		6: # forward + steer left (A) should curve the car toward world -X
			if _frame == 1:
				Input.action_press("move_forward")
				Input.action_press("move_left")
			if _frame >= 120:
				Input.action_release("move_forward")
				Input.action_release("move_left")
				var x: float = _car.global_position.x
				if x < -1.5:
					print("PASS: steering left curved the car toward -X (x=", x, ")")
				else:
					print("FAIL: steering left didn't curve the car as expected (x=", x, ")")
					_ok = false
				_reset_car(Vector3(0, 1.5, 0), 0.0)
				_phase = 7
				_frame = 0
		7: # settle, then inject a big lateral slide with no input held --
			# normal grip should cancel most of it quickly.
			if _frame >= 20:
				_car.linear_velocity = Vector3(15.0, _car.linear_velocity.y, 0.0)
				_lateral_before = 15.0
				_phase = 8
				_frame = 0
		8:
			if _frame >= 15:
				var lateral: float = absf(_car.linear_velocity.x)
				if lateral < _lateral_before * 0.5:
					print("PASS: normal grip cancelled most of an injected slide (", _lateral_before, " -> ", lateral, ")")
				else:
					print("FAIL: normal grip didn't cancel the slide as expected (", _lateral_before, " -> ", lateral, ")")
					_ok = false
				_reset_car(Vector3(0, 1.5, 0), 0.0)
				_phase = 9
				_frame = 0
		9: # hold the handbrake (jump) first and let _effective_grip actually
			# ramp down to handbrakeGripRear BEFORE injecting the slide --
			# otherwise the first several ticks would still be cancelling at
			# the leftover normal-grip strength from the previous phase,
			# muddying the comparison.
			if _frame == 1:
				Input.action_press("jump")
			if _frame >= 40:
				_car.linear_velocity = Vector3(15.0, _car.linear_velocity.y, 0.0)
				_lateral_before = 15.0
				_phase = 10
				_frame = 0
		10:
			if _frame >= 15:
				Input.action_release("jump")
				var lateral: float = absf(_car.linear_velocity.x)
				if lateral > _lateral_before * 0.5:
					print("PASS: handbrake kept most of the slide (", _lateral_before, " -> ", lateral, ")")
				else:
					print("FAIL: handbrake grip cancelled the slide too fast (", _lateral_before, " -> ", lateral, ")")
					_ok = false
				_phase = 11
				_frame = 0
		11: # airborne: no ground under the wheel rays at all -- throttle
			# should not build forward speed even though gravity still pulls
			# the car down.
			if _frame == 1:
				_reset_car(Vector3(0, 60.0, 0), 0.0)
				Input.action_press("move_forward")
			if _frame >= 30:
				Input.action_release("move_forward")
				var forward_speed: float = absf(_horiz(_car.linear_velocity).length())
				if forward_speed < 2.0:
					print("PASS: airborne car didn't respond to throttle (horizontal speed ", forward_speed, ")")
				else:
					print("FAIL: airborne car built horizontal speed from throttle (", forward_speed, ")")
					_ok = false
				print("OVERALL: ", "PASS" if _ok else "FAIL")
				get_tree().quit(0 if _ok else 1)
