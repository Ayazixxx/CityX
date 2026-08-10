extends Node3D
## Regression check for sword-wielding crouch foot-grounding -- same "lowest
## leg point over a real crouch/crouch-move/uncrouch cycle" methodology as
## test_crouch_ground_check.gd (see its own header comment), but with the
## sword equipped, guarding two real bugs found the hard way (reported as
## "legs clip through the floor" while crouch-walking with the sword):
## 1) The sword pack has no dedicated crouch-WALKING clip (only the
##    standing_to_crouch/crouch_to_idle transitions) -- the original
##    static-hold approach left legs completely frozen while the whole body
##    kept sliding forward via normal movement, which read the same as
##    clipping to a player watching it even though the pose itself (a
##    single, correctly-grounded static frame) never actually went below
##    ground. Fixed by overlaying leg_crouch_walk (borrowed from the FIST
##    system's own CrouchWalkAnim, see its own comment) for real stepping
##    motion while the upper body/arms/sword stay on the static crouch pose.
## 2) Once crouch-walking's own leg overlay (which carries its OWN
##    correctly-grounded hips-position track) left hips at some arbitrary
##    point in its bob cycle when movement stopped, a one-time "snap to the
##    static pose" guard (checked against being back at the ORIGINAL
##    standing base) never re-triggered, since hips never returns to that
##    exact base while still crouched -- silently leaving hips stuck at the
##    wrong height. A SEPARATE, similar bug also showed up transitioning
##    all the way back to standing: a single _reset_hips_position() call
##    correctly set hips for exactly one frame, then Godot's own internal
##    animation-blend bookkeeping silently dragged it back down to a stale
##    value the very next frame, with nothing else in character.gd touching
##    hips to explain it. Both fixed by directly re-asserting the correct
##    hips height EVERY frame (not a one-time call) in the relevant
##    set_walking() branches, sidestepping needing to reason about Godot's
##    own blend timing at all.
## CLIP_THRESHOLD matches test_crouch_ground_check.gd's own -- see its
## comment for what it allows vs. catches.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")
const FusedBody := preload("res://character/fused_body.gd")

const CLIP_THRESHOLD := -0.08

var _player: CharacterBody3D
var _character: Node3D
var _skeleton: Skeleton3D
var _frame := 0
var _phase := 0
var _ok := true
var _global_min_y := INF
var _global_min_desc := ""
var _origins: Dictionary
var _ground_ref_y: float

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
	_character.set_wielding_sword(true)
	_skeleton = _character.get_node("BodySkeleton")
	_origins = FusedBody.bone_origins()
	_ground_ref_y = FusedBody.FOOT_Y - FusedBody.LEG_R

func _lowest_leg_point() -> float:
	var lowest := INF
	for side in ["l", "r"]:
		var shin_idx: int = _skeleton.find_bone("shin_" + side)
		var shin_origin: Vector3 = _origins["shin_" + side]
		var local_offset := Vector3(0, _ground_ref_y - shin_origin.y, 0)
		var bone_pose: Transform3D = _skeleton.get_bone_global_pose(shin_idx)
		var world_point: Vector3 = _skeleton.global_transform * (bone_pose * local_offset)
		lowest = minf(lowest, world_point.y)
	return lowest

func _track(label: String) -> void:
	var y := _lowest_leg_point()
	if y < _global_min_y:
		_global_min_y = y
		_global_min_desc = label + " (frame " + str(_frame) + ")"

func _physics_process(_delta: float) -> void:
	_frame += 1
	match _phase:
		0: # settle
			if _frame >= 10:
				_phase = 1
				_frame = 0
		1: # press crouch, stand still, let sword_standing_to_crouch finish
			if _frame == 2:
				Input.action_press("crouch")
			_track("standing still, crouch pressed")
			if _frame >= 90:
				_phase = 2
				_frame = 0
		2: # start walking while crouched -- exercises leg_crouch_walk
			if _frame == 1:
				Input.action_press("move_forward")
			_track("starting to crouch-walk")
			if _frame >= 180: # 3 real seconds -- several full walk cycles
				_phase = 3
				_frame = 0
		3: # stop moving while still crouching (settle back to static pose)
			if _frame == 1:
				Input.action_release("move_forward")
			_track("stopping crouch-walk")
			if _frame >= 60:
				_phase = 4
				_frame = 0
		4: # release crouch entirely (sword_crouch_to_idle -> sword_idle)
			if _frame == 1:
				Input.action_release("crouch")
			_track("uncrouching")
			if _frame >= 90:
				_phase = 5
				_frame = 0
		5:
			print("GLOBAL MIN leg height: ", _global_min_y, " at ", _global_min_desc)
			if _global_min_y < CLIP_THRESHOLD:
				print("FAIL: feet clipped through the floor beyond the accepted blend-transient tolerance (", CLIP_THRESHOLD, ")")
				_ok = false
			else:
				print("PASS: feet stayed within the accepted ground tolerance throughout the full sword crouch/crouch-walk/uncrouch cycle")
			print("OVERALL: ", "PASS" if _ok else "FAIL")
			get_tree().quit(0 if _ok else 1)
