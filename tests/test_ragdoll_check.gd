extends Node3D
## Regression check for the ragdoll physics rig on the 14-bone standard
## humanoid skeleton (see the rig-rebuild plan) -- specifically a safety net
## for two things that changed together: the ragdoll chain now goes up to 3
## joints deep from the root (hand_l is hips->spine->arm_l->forearm_l->
## hand_l) instead of the old rig's max of 2, and the mesh-follow pose
## composition was changed from "always relative to the root" to "relative
## to each bone's own immediate parent" to fix a bug that would otherwise
## compound at every level of a deep chain (see character.gd's own
## _physics_process comment). Runs headless: pure physics + state checks,
## no rendering needed -- see the separate visual check for how it actually
## looks.

const CharacterScene := preload("res://character/character.tscn")
const PlayerScene := preload("res://player/player.tscn")

var _player: CharacterBody3D
var _character: Node3D
var _frame := 0
var _ok := true

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

func _physics_process(_delta: float) -> void:
	_frame += 1
	if _frame == 3:
		_player.velocity = Vector3(0, 0, -2.0)
		_character.ragdoll()

	if _frame == 4:
		# Every posable bone (see character.gd's own _ragdoll_posable_bones)
		# plus "hips" must actually have gotten a real RigidBody3D -- a
		# missing one here means segment_defs()/BONE_PARENT drifted out of
		# sync with each other.
		var expected: Array = _character._ragdoll_posable_bones()
		expected.append("hips")
		for bone_name in expected:
			if not _character._ragdoll_bodies.has(bone_name):
				print("FAIL: no ragdoll body for ", bone_name)
				_ok = false
		if _character._ragdoll_bodies.size() != expected.size():
			print("FAIL: expected ", expected.size(), " ragdoll bodies, got ", _character._ragdoll_bodies.size())
			_ok = false
		else:
			print("PASS: every posable bone (", expected.size(), ") got a ragdoll body")

	if _frame in [60, 90, 120, 150, 180, 200]:
		var m := 0.0
		for bone_name in _character._ragdoll_bodies:
			var b: RigidBody3D = _character._ragdoll_bodies[bone_name]
			if is_instance_valid(b):
				m = maxf(m, b.angular_velocity.length())
		print("frame ", _frame, " max angular velocity: ", m)

	if _frame >= 5 and _frame <= 200:
		# Watched every frame across the whole fall, not just at the end --
		# a NaN or a body rocketing away can appear mid-simulation and then
		# get masked by damping/settling before a single end-of-test check
		# would catch it.
		for bone_name in _character._ragdoll_bodies:
			var body: RigidBody3D = _character._ragdoll_bodies[bone_name]
			if not is_instance_valid(body):
				continue
			var pos := body.global_position
			if is_nan(pos.x) or is_nan(pos.y) or is_nan(pos.z):
				print("FAIL: ", bone_name, " position went NaN at frame ", _frame)
				_ok = false
			# A body meaningfully far from the others is exactly what
			# "flailing"/explosion looks like -- the character started at
			# the origin, so nothing should ever wander more than a few
			# body-lengths away in any direction.
			elif pos.distance_to(Vector3(0, 1, 0)) > 15.0:
				print("FAIL: ", bone_name, " flew to ", pos, " at frame ", _frame)
				_ok = false
			var angvel := body.angular_velocity
			if is_nan(angvel.x) or is_nan(angvel.y) or is_nan(angvel.z):
				print("FAIL: ", bone_name, " angular_velocity went NaN at frame ", _frame)
				_ok = false

	if _frame == 201:
		if _ok:
			print("PASS: no NaNs and no body flew away across the whole fall")

		# By now the fall (character starts ~1m off the ground with a
		# little push) should have long since landed and be settling --
		# angular velocity should have decayed close to zero, not still be
		# spinning hard, confirming the rig actually comes to rest instead
		# of jittering/exploding indefinitely.
		var max_angvel := 0.0
		for bone_name in _character._ragdoll_bodies:
			var body: RigidBody3D = _character._ragdoll_bodies[bone_name]
			if is_instance_valid(body):
				max_angvel = maxf(max_angvel, body.angular_velocity.length())
		if max_angvel < 2.0:
			print("PASS: ragdoll settled (max angular velocity ", max_angvel, " rad/s)")
		else:
			print("FAIL: ragdoll still spinning hard after settling time (max angular velocity ", max_angvel, " rad/s)")
			_ok = false

		print("OVERALL: ", "PASS" if _ok else "FAIL")
		get_tree().quit(0 if _ok else 1)
