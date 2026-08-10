extends Node3D
## Regression check for crouch foot-grounding, driven through the REAL
## Player + Character + input pipeline (not an isolated AnimationPlayer
## harness) -- exactly the gap that let two real, severe bugs slip past
## earlier isolated-clip verification this session:
## 1) set_walking()'s own per-frame fallback racing end_crouch() on release
##    (fixed via same-frame is_crouching edge detection in player.gd, run
##    BEFORE set_walking rather than after).
## 2) crouch_to_idle's own baked last frame leaving hips ~0.135m short of
##    full standing height, silently inherited forever by "idle" (which has
##    no hips-position track of its own to correct it) -- fixed via
##    character.gd's _reset_hips_position(), called whenever a non-crouch
##    animation is about to start.
## CLIP_THRESHOLD allows the much smaller (~1-7cm), brief transient dip
## inherent to linearly cross-fading two independently-authored poses
## during CROUCH_BLEND_SECONDS itself (an accepted, pre-existing tradeoff --
## see that constant's own comment) while still failing hard on anything
## resembling the two bugs above (11-44cm, and sustained rather than
## momentary). Runs headless: pure physics + animation checks, no rendering
## needed.

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
		1: # press crouch, stand still, let standing_to_crouch finish
			if _frame == 2:
				Input.action_press("crouch")
			_track("standing still, crouch pressed")
			if _frame >= 90:
				_phase = 2
				_frame = 0
		2: # start walking while crouched (CROUCH_BLEND_SECONDS blend into
			# crouch_walk)
			if _frame == 1:
				Input.action_press("move_forward")
			_track("starting to crouch-walk")
			if _frame >= 180: # 3 real seconds -- several full walk cycles
				_phase = 3
				_frame = 0
		3: # stop moving while still crouching (settle-to-static-crouch)
			if _frame == 1:
				Input.action_release("move_forward")
			_track("stopping crouch-walk")
			if _frame >= 60:
				_phase = 4
				_frame = 0
		4: # release crouch entirely (crouch_to_idle -> idle)
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
				print("PASS: feet stayed within the accepted ground tolerance throughout the full crouch/crouch-walk/uncrouch cycle")
			print("OVERALL: ", "PASS" if _ok else "FAIL")
			get_tree().quit(0 if _ok else 1)
