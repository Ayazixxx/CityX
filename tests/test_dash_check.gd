extends Node3D
## Regression check for the double-tap-Shift dash (see player.gd's own
## _try_start_dash/_sprint_tap_window_remaining handling): a press-release-
## press of the sprint key within DASH_DOUBLE_TAP_WINDOW_SECONDS must fire a
## directional burst at DASH_SPEED, picking its direction from whichever
## WASD was actually held at that instant (A=left, D=right, S=backward,
## W/nothing=forward, all relative to the player's own facing) -- and must
## cost a flat DASH_STAMINA_COST, refusing to fire at all if stamina can't
## cover it. A single tap (no second press within the window) must NOT
## dash. Runs headless: pure physics + state-machine checks, no rendering
## needed.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _player: CharacterBody3D
var _character: Node3D
var _frame := 0
var _phase := 0
var _ok := true
var _stamina_before := 0.0

func _ready() -> void:
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

## See test_punch_check.gd's own comment on why action_press()/release()
## called from script don't flip is_action_just_pressed() until the
## FOLLOWING physics frame -- releasing one frame after pressing (rather
## than the same frame) is what lets each tap actually register as its own
## press-then-release edge instead of racing that lag.
func _tap_sprint(press_frame: int) -> void:
	if _frame == press_frame:
		Input.action_press("sprint")
	if _frame == press_frame + 1:
		Input.action_release("sprint")

func _horiz_velocity() -> Vector2:
	return Vector2(_player.velocity.x, _player.velocity.z)

## Regression check for a real reported request: "double press shift while
## pressing a+w or w+d or s+d or a+s" should dash diagonally, not snap to
## one of the 4 cardinal directions. Confirmed directly (a throwaway
## diagnostic, since deleted) that this already worked correctly BEFORE
## this check was ever added -- _try_start_dash's own _dash_direction is
## computed from the raw (right, back) input vector, not through
## _direction_from_input's 4-way reduction (that reduction only ever picks
## an ANIMATION clip, and only "backward" has a dedicated one regardless --
## see _try_start_dash's own comment). This test exists purely to LOCK IN
## that already-correct behavior with a real regression check, since it had
## no test coverage at all before. expected_y_sign/expected_x_sign are in
## the SAME (x, z) convention _horiz_velocity() returns; a genuine diagonal
## lands both axes near DASH_SPEED/sqrt(2), not one axis near DASH_SPEED
## and the other near 0 (which would mean it silently snapped to a single
## cardinal direction instead).
func _check_diagonal_dash(v: Vector2, expected_y_sign: int, expected_x_sign: int, label: String) -> void:
	var expected_component: float = _player.DASH_SPEED / sqrt(2.0)
	var expected: Vector2 = Vector2(expected_x_sign * expected_component, expected_y_sign * expected_component)
	if absf(v.x - expected.x) < 2.0 and absf(v.y - expected.y) < 2.0:
		print("PASS: ", label, " dashed diagonally (velocity ", v, ")")
	else:
		print("FAIL: ", label, " didn't dash diagonally as expected (velocity ", v, ", expected ~", expected, ")")
		_ok = false

func _physics_process(_delta: float) -> void:
	_frame += 1
	match _phase:
		0: # let everything settle onto the ground for a few frames
			if _frame >= 5:
				_phase = 1
				_frame = 0
		1: # bare double-tap, no WASD held -- should dash FORWARD (matches
			# W's own direction) by default.
			_tap_sprint(2)
			_tap_sprint(5)
			if _frame == 1:
				_stamina_before = _player.stamina
			if _frame >= 10:
				var v := _horiz_velocity()
				if v.y < -_player.DASH_SPEED + 1.0 and absf(v.x) < 1.0:
					print("PASS: bare double-tap dashed forward (velocity ", v, ")")
				else:
					print("FAIL: bare double-tap didn't dash forward as expected (velocity ", v, ")")
					_ok = false
				var drained: float = _stamina_before - _player.stamina
				# Tolerance wider than a flat epsilon: passive stamina regen
				# (STAMINA_REGEN_PER_SEC) keeps running every frame between
				# the dash actually firing and this check a few frames later,
				# quietly clawing a bit of it back -- not a bug, just this
				# check not being on the exact trigger frame.
				if absf(drained - _player.DASH_STAMINA_COST) < 2.0:
					print("PASS: dash cost DASH_STAMINA_COST (~", drained, ")")
				else:
					print("FAIL: dash stamina cost wrong (drained ", drained, ", expected ~", _player.DASH_STAMINA_COST, ")")
					_ok = false
				_phase = 2
				_frame = 0
		2: # let the burst fully finish before starting the next check
			if _frame >= 20:
				_player.velocity = Vector3.ZERO
				_player.stamina = _player.MAX_STAMINA
				Input.action_press("move_left")
				_phase = 3
				_frame = 0
		3: # holding A (move_left) during the tap should dash LEFT
			_tap_sprint(3)
			_tap_sprint(6)
			if _frame >= 11:
				var v := _horiz_velocity()
				if v.x < -_player.DASH_SPEED + 1.0 and absf(v.y) < 1.0:
					print("PASS: holding A dashed left (velocity ", v, ")")
				else:
					print("FAIL: holding A didn't dash left as expected (velocity ", v, ")")
					_ok = false
				_phase = 4
				_frame = 0
		4:
			if _frame >= 20:
				Input.action_release("move_left")
				_player.velocity = Vector3.ZERO
				_player.stamina = _player.MAX_STAMINA
				Input.action_press("move_right")
				_phase = 5
				_frame = 0
		5: # holding D (move_right) during the tap should dash RIGHT
			_tap_sprint(3)
			_tap_sprint(6)
			if _frame >= 11:
				var v := _horiz_velocity()
				if v.x > _player.DASH_SPEED - 1.0 and absf(v.y) < 1.0:
					print("PASS: holding D dashed right (velocity ", v, ")")
				else:
					print("FAIL: holding D didn't dash right as expected (velocity ", v, ")")
					_ok = false
				_phase = 6
				_frame = 0
		6:
			if _frame >= 20:
				Input.action_release("move_right")
				_player.velocity = Vector3.ZERO
				_player.stamina = _player.MAX_STAMINA
				Input.action_press("move_back")
				_phase = 7
				_frame = 0
		7: # holding S (move_back) during the tap should dash BACKWARD
			_tap_sprint(3)
			_tap_sprint(6)
			if _frame >= 11:
				var v := _horiz_velocity()
				if v.y > _player.DASH_SPEED - 1.0 and absf(v.x) < 1.0:
					print("PASS: holding S dashed backward (velocity ", v, ")")
				else:
					print("FAIL: holding S didn't dash backward as expected (velocity ", v, ")")
					_ok = false
				_phase = 8
				_frame = 0
		8:
			if _frame >= 20:
				Input.action_release("move_back")
				_player.velocity = Vector3.ZERO
				_player.stamina = _player.MAX_STAMINA
				Input.action_press("move_forward")
				Input.action_press("move_right")
				_phase = 9
				_frame = 0
		9: # holding W+D (forward-right diagonal) should dash at 45 degrees,
			# not snap to pure forward or pure right -- the actual case
			# reported ("double press shift while pressing a+w or w+d...").
			_tap_sprint(3)
			_tap_sprint(6)
			if _frame >= 11:
				_check_diagonal_dash(_horiz_velocity(), -1, 1, "W+D (forward-right)")
				_phase = 10
				_frame = 0
		10:
			if _frame >= 20:
				Input.action_release("move_forward")
				Input.action_release("move_right")
				_player.velocity = Vector3.ZERO
				_player.stamina = _player.MAX_STAMINA
				Input.action_press("move_forward")
				Input.action_press("move_left")
				_phase = 11
				_frame = 0
		11: # holding W+A (forward-left diagonal)
			_tap_sprint(3)
			_tap_sprint(6)
			if _frame >= 11:
				_check_diagonal_dash(_horiz_velocity(), -1, -1, "W+A (forward-left)")
				_phase = 12
				_frame = 0
		12:
			if _frame >= 20:
				Input.action_release("move_forward")
				Input.action_release("move_left")
				_player.velocity = Vector3.ZERO
				_player.stamina = _player.MAX_STAMINA
				Input.action_press("move_back")
				Input.action_press("move_right")
				_phase = 13
				_frame = 0
		13: # holding S+D (backward-right diagonal)
			_tap_sprint(3)
			_tap_sprint(6)
			if _frame >= 11:
				_check_diagonal_dash(_horiz_velocity(), 1, 1, "S+D (backward-right)")
				_phase = 14
				_frame = 0
		14:
			if _frame >= 20:
				Input.action_release("move_back")
				Input.action_release("move_right")
				_player.velocity = Vector3.ZERO
				_player.stamina = _player.MAX_STAMINA
				Input.action_press("move_back")
				Input.action_press("move_left")
				_phase = 15
				_frame = 0
		15: # holding S+A (backward-left diagonal)
			_tap_sprint(3)
			_tap_sprint(6)
			if _frame >= 11:
				_check_diagonal_dash(_horiz_velocity(), 1, -1, "S+A (backward-left)")
				_phase = 16
				_frame = 0
		16:
			if _frame >= 20:
				Input.action_release("move_back")
				Input.action_release("move_left")
				_player.velocity = Vector3.ZERO
				_player.stamina = 10.0 # well under DASH_STAMINA_COST
				_phase = 17
				_frame = 0
		17: # not enough stamina -- the double-tap must NOT dash at all
			var stamina_before_tap: float = _player.stamina
			_tap_sprint(2)
			_tap_sprint(5)
			if _frame >= 10:
				var v := _horiz_velocity()
				if v.length() < 0.5:
					print("PASS: insufficient stamina blocked the dash (velocity ", v, ")")
				else:
					print("FAIL: dashed despite insufficient stamina (velocity ", v, ")")
					_ok = false
				if _player.stamina >= stamina_before_tap - 0.1:
					print("PASS: no stamina was spent on the blocked dash")
				else:
					print("FAIL: stamina was spent even though the dash was blocked (", stamina_before_tap, " -> ", _player.stamina, ")")
					_ok = false
				_player.stamina = _player.MAX_STAMINA
				_phase = 18
				_frame = 0
		18: # a single tap (no second press within the window) must NOT dash
			# -- holding it afterward is just an ordinary sprint.
			if _frame == 2:
				Input.action_press("move_forward")
				Input.action_press("sprint")
			if _frame >= 30:
				var speed := _horiz_velocity().length()
				if speed < _player.DASH_SPEED - 1.0 and speed > _player.SPEED:
					print("PASS: a single tap+hold sprinted normally, no dash (speed ", speed, ")")
				else:
					print("FAIL: single tap behaved like a dash (speed ", speed, ")")
					_ok = false
				Input.action_release("sprint")
				Input.action_release("move_forward")
				print("OVERALL: ", "PASS" if _ok else "FAIL")
				get_tree().quit(0 if _ok else 1)
