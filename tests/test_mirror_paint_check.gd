extends Node
## Regression check for the character creator's new Mirror mode:
## character.gd's own mirror_local_point() (negate local X -- the rig is
## perfectly symmetric across X=0, see FusedBody.limb_defs()'s own
## arm_l/arm_r and leg_l/leg_r pairs) and find_face_for_point() (relocates
## an arbitrary 3D point already known to be on the mesh surface, used to
## find the mirrored point's own face/UV without needing to understand this
## mesh's own UV formula at all), plus character_creator.gd's own
## _paint_at_hit/_bucket_fill_at_hit actually painting both sides when
## Mirror is toggled on.

const CharacterScene := preload("res://character/character.tscn")
const CreatorScene := preload("res://character_creator/character_creator.tscn")

var _ok := true
var _character: Node3D

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _colors_close(a: Color, b: Color, tolerance: float = 0.01) -> bool:
	return absf(a.r - b.r) <= tolerance and absf(a.g - b.g) <= tolerance and absf(a.b - b.b) <= tolerance

func _ready() -> void:
	_character = CharacterScene.instantiate()
	add_child(_character)
	call_deferred("_run_checks")

func _run_checks() -> void:
	_run_mirror_point_check()
	await _run_find_face_and_paint_check()
	await _run_creator_mirror_toggle_check()
	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

func _run_mirror_point_check() -> void:
	var p := Vector3(0.3, 1.5, 0.1)
	var mirrored: Vector3 = _character.mirror_local_point(p)
	if mirrored.is_equal_approx(Vector3(-0.3, 1.5, 0.1)):
		print("PASS: mirror_local_point() negates X only, leaves Y/Z untouched")
	else:
		_fail("mirror_local_point(%s) returned %s, expected (-0.3, 1.5, 0.1)" % [p, mirrored])

## Hits the arm on one side, mirrors the hit point, and confirms
## find_face_for_point() lands on a REAL, different face whose own position
## is actually on the opposite side (negative-vs-positive X), then confirms
## painting there via character.gd's own paint_at()/sample_color_at()
## round-trips correctly.
func _run_find_face_and_paint_check() -> void:
	var hit: Dictionary = _character.raycast_paint(Vector3(2.0, 1.2, 0.0), Vector3(-1, 0, 0), 5.0)
	if hit.is_empty():
		_fail("raycast_paint found no hit on the arm to test mirroring against")
		return
	var original_x: float = hit["local_pos"].x
	var mirrored_pos: Vector3 = _character.mirror_local_point(hit["local_pos"])
	var mirrored_face: int = _character.find_face_for_point(mirrored_pos)
	if mirrored_face < 0:
		_fail("find_face_for_point() couldn't locate the mirrored point at all")
		return
	if sign(original_x) != 0.0 and sign(mirrored_pos.x) == sign(original_x):
		_fail("mirrored point (%s) is on the SAME side as the original (%s), expected the opposite" % [mirrored_pos, hit["local_pos"]])
	else:
		print("PASS: find_face_for_point() locates a real face on the opposite side of the body")

	var test_color := Color(0.1, 0.8, 0.4, 1.0)
	_character.paint_at(mirrored_pos, mirrored_face, test_color, 3)
	var sampled: Color = _character.sample_color_at(mirrored_pos, mirrored_face)
	if _colors_close(sampled, test_color):
		print("PASS: painting at the mirrored face/point actually lands correctly")
	else:
		_fail("sample_color_at() at the mirrored point returned %s, expected %s" % [sampled, test_color])

## Drives the real character_creator.gd scene: toggles Mirror on, paints a
## single stroke on one arm, and confirms the OTHER arm picked up the same
## color too -- the actual end-to-end path a player would use.
func _run_creator_mirror_toggle_check() -> void:
	var creator: Node3D = CreatorScene.instantiate()
	add_child(creator)
	await get_tree().process_frame
	await get_tree().process_frame

	creator.mirror_button.button_pressed = true
	creator.color_picker.color = Color(0.9, 0.2, 0.6, 1.0)

	var hit: Dictionary = creator.character.raycast_paint(Vector3(2.0, 1.2, 0.0), Vector3(-1, 0, 0), 5.0)
	if hit.is_empty():
		_fail("creator's own character had no arm hit to test the real Mirror toggle against")
		creator.queue_free()
		return
	creator._paint_at_hit(hit)

	var mirrored_pos: Vector3 = creator.character.mirror_local_point(hit["local_pos"])
	var mirrored_face: int = creator.character.find_face_for_point(mirrored_pos)
	if mirrored_face < 0:
		_fail("couldn't locate the mirrored face after a real _paint_at_hit call")
		creator.queue_free()
		return
	var mirrored_color: Color = creator.character.sample_color_at(mirrored_pos, mirrored_face)
	if _colors_close(mirrored_color, Color(0.9, 0.2, 0.6, 1.0)):
		print("PASS: with Mirror toggled on, painting one arm through the real creator also paints the other arm")
	else:
		_fail("mirrored side wasn't painted through the real creator (got %s)" % mirrored_color)

	# Sanity: with Mirror OFF, only the clicked side gets painted.
	creator.mirror_button.button_pressed = false
	var hit2: Dictionary = creator.character.raycast_paint(Vector3(2.0, 1.3, 0.0), Vector3(-1, 0, 0), 5.0)
	if not hit2.is_empty():
		creator.color_picker.color = Color(0.1, 0.1, 0.9, 1.0)
		var before: Color = creator.character.sample_color_at(creator.character.mirror_local_point(hit2["local_pos"]), creator.character.find_face_for_point(creator.character.mirror_local_point(hit2["local_pos"])))
		creator._paint_at_hit(hit2)
		var after: Color = creator.character.sample_color_at(creator.character.mirror_local_point(hit2["local_pos"]), creator.character.find_face_for_point(creator.character.mirror_local_point(hit2["local_pos"])))
		if _colors_close(before, after):
			print("PASS: with Mirror toggled off, the opposite side is left untouched")
		else:
			_fail("opposite side changed (%s -> %s) even with Mirror off" % [before, after])

	creator.queue_free()
