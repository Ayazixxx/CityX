extends Node3D
## Regression check for the combo finisher mechanic (see player.gd's own
## _begin_finisher_ragdoll/_end_finisher_ragdoll): a landed hit with
## is_finisher=true knocks the defender away from the attacker and ragdolls
## them via character.gd's existing real physics ragdoll (the same one
## death/respawn already uses), then auto-recovers on its own timer --
## standing back up wherever the ragdoll actually landed, NOT snapped back
## to the pre-hit position (see character.gd's own reset_ragdoll() comment
## on why a naive reuse would get this wrong), and staying invulnerable for
## a grace window afterward so a still-nearby attacker can't immediately
## start a new combo. take_damage() is called directly with is_finisher=true
## (isolating the mechanic, same approach test_hitstun_lock_check.gd already
## uses) rather than driving a real 5-hit combo end to end (see
## test_unarmed_combo_check.gd for that).
##
## Runs headless, but needs REAL elapsed physics frames (not a single large
## fast-forward delta like the pure-GDScript-timer tests use) for the
## ragdoll's own knockback position check: the ragdoll's "hips" RigidBody3D
## only actually moves via the engine's own PhysicsServer stepping in
## lockstep with real frames -- a manually-invoked _physics_process(huge_dt)
## call runs the GDScript logic with that delta but does NOT itself advance
## real rigid-body simulation, confirmed directly (a single big-delta call
## left the body at exactly its pre-hit position, 0 velocity integration
## having actually happened).

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _player: CharacterBody3D
var _character: Node3D
var _attacker: Node3D
var _attacker_pos := Vector3(0, 0, 3.0)
var _frame := 0
var _phase := 0
var _ok := true
var _health_mid_ragdoll := 0.0

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

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
	_player.position = Vector3.ZERO
	add_child(_player)
	_character = CharacterScene.instantiate()
	_character.set_paintable(false)
	_player.add_child(_character)

	# A plain Node3D standing in for the attacker -- only its global_position
	# is read (see _begin_finisher_ragdoll's own knockback-direction math),
	# no real Player needed for that.
	_attacker = Node3D.new()
	add_child(_attacker)
	_attacker.global_position = _attacker_pos

func _physics_process(_delta: float) -> void:
	_frame += 1
	match _phase:
		0: # let the body settle onto the ground
			if _frame >= 5:
				_phase = 1
				_frame = 0
		1: # a finisher hit ragdolls + knocks back + grants invulnerability
			if _frame == 1:
				_player.take_damage(10.0, _attacker, 0.0, true)

				if _player._is_finisher_ragdolled and _character._is_ragdolled:
					print("PASS: a finisher hit ragdolls the defender")
				else:
					_fail("finisher hit didn't ragdoll (player._is_finisher_ragdolled=%s character._is_ragdolled=%s)" % [_player._is_finisher_ragdolled, _character._is_ragdolled])

				var away_from_attacker := _player.global_position - _attacker_pos
				away_from_attacker.y = 0.0
				if away_from_attacker.length() > 0.001 and _player.velocity.dot(away_from_attacker.normalized()) > 0.0:
					print("PASS: knockback velocity points away from the attacker")
				else:
					_fail("knockback velocity doesn't point away from the attacker (velocity=%s)" % _player.velocity)

				if _player._invulnerable_remaining > 0.0:
					print("PASS: the defender is invulnerable during the ragdoll flight")
				else:
					_fail("_invulnerable_remaining wasn't set by the finisher")

				# A second hit landing mid-ragdoll must whiff -- this IS the
				# "can't be re-comboed the instant you're launched" guarantee.
				_health_mid_ragdoll = _player.health
				_player.take_damage(10.0, _attacker, 0.0, false)
				if is_equal_approx(_player.health, _health_mid_ragdoll):
					print("PASS: a second hit landing mid-ragdoll deals zero damage")
				else:
					_fail("health changed from a hit landing mid-ragdoll (before=%s after=%s)" % [_health_mid_ragdoll, _player.health])

			# Real elapsed frames for FINISHER_RAGDOLL_SECONDS plus slack --
			# the ragdoll's own rigid bodies need actual physics steps to
			# carry the knockback, not a single fast-forwarded delta.
			var wait_frames: int = ceili(_player.FINISHER_RAGDOLL_SECONDS * 60.0) + 15
			if _frame >= wait_frames:
				_phase = 2
				_frame = 0
		2: # auto-recovers on its own, standing up where it actually landed
			if not _player._is_finisher_ragdolled and not _character._is_ragdolled:
				print("PASS: the finisher ragdoll auto-recovers once its own timer runs out")
			else:
				_fail("still ragdolled after FINISHER_RAGDOLL_SECONDS should have elapsed")

			if _player._invulnerable_remaining > 0.0:
				print("PASS: a post-recovery grace window is still active immediately after standing back up")
			else:
				_fail("no post-recovery invulnerability grace window remains")

			if _player.global_position.distance_to(Vector3.ZERO) > 0.5:
				print("PASS: the defender stands back up at their actual landing position, not snapped back to where they were hit")
			else:
				_fail("defender ended up back near the pre-hit position (%s) instead of where the knockback actually carried them" % _player.global_position)

			print("OVERALL: ", "PASS" if _ok else "FAIL")
			get_tree().quit(0 if _ok else 1)
