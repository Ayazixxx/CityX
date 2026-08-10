extends Node3D
## Regression check for the reflecting mirror (mirror/mirror.gd).
##
## Nothing here can confirm the mirror LOOKS right -- that needs eyes on a
## rendered frame. What it can confirm is the part that would be wrong
## silently: the reflection camera's placement math, and the two guards that
## keep the thing from either eating the framerate or collapsing into
## feedback.

const MirrorScene := preload("res://mirror/mirror.tscn")

var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	var mirror: Node3D = MirrorScene.instantiate()
	add_child(mirror)

	_check_feedback_guard(mirror)
	_check_reflection_geometry(mirror)
	_check_idle_when_unwatched(mirror)

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

## The glass must be invisible to the very camera whose output it displays,
## or the surface degenerates into an infinite recursive corridor.
func _check_feedback_guard(mirror: Node3D) -> void:
	var cam: Camera3D = mirror._reflect_cam
	if cam == null:
		_fail("no reflection camera was built")
		return
	if (cam.cull_mask & mirror.GLASS_VISUAL_LAYER) == 0:
		print("PASS: the reflection camera cannot see the glass (no feedback loop)")
	else:
		_fail("reflection camera's cull_mask still includes the glass layer -- this renders as infinite recursion")

	var glass_found := false
	for child in mirror.get_children():
		if child is MeshInstance3D and child.layers == mirror.GLASS_VISUAL_LAYER:
			glass_found = true
	if glass_found:
		print("PASS: the glass sits on its own dedicated visual layer")
	else:
		_fail("no glass MeshInstance3D on GLASS_VISUAL_LAYER was found")

## The core math: a camera at a known spot in front of the mirror must be
## reflected to the mirrored spot BEHIND it, at equal distance, and must
## still be looking back toward the glass.
func _check_reflection_geometry(mirror: Node3D) -> void:
	var cam := Camera3D.new()
	add_child(cam)
	# Mirror faces its own local +Z and sits at the origin; stand 3m out in
	# front of it, off to one side, looking back at it.
	var viewer_pos := Vector3(0.7, 1.6, 3.0)
	cam.global_position = viewer_pos
	cam.look_at(Vector3(0, mirror.MIRROR_HEIGHT / 2.0, 0), Vector3.UP)
	mirror._player_cam = cam

	mirror._process(0.016)

	var reflected: Vector3 = mirror._reflect_cam.global_position
	# Reflection across the z=0 plane: x/y unchanged, z negated.
	if is_equal_approx(reflected.x, viewer_pos.x) and is_equal_approx(reflected.y, viewer_pos.y) and is_equal_approx(reflected.z, -viewer_pos.z):
		print("PASS: the reflection camera lands exactly mirrored through the glass plane (", reflected, ")")
	else:
		_fail("reflection camera at %s, expected the mirror image of %s" % [reflected, viewer_pos])

	# It must also be facing back toward the mirror, not away from it --
	# a correctly-placed camera pointed the wrong way renders the world
	# behind the mirror and looks like the reflection simply isn't working.
	var facing: Vector3 = -mirror._reflect_cam.global_transform.basis.z
	if facing.z > 0.0:
		print("PASS: the reflection camera looks back toward the glass")
	else:
		_fail("reflection camera faces away from the mirror (forward.z=%.3f)" % facing.z)

	cam.queue_free()

## A second full scene render every frame is the most expensive thing this
## prop does -- it has to stop entirely when nobody can see it.
func _check_idle_when_unwatched(mirror: Node3D) -> void:
	var cam := Camera3D.new()
	add_child(cam)
	mirror._player_cam = cam

	# Standing well beyond ACTIVE_DISTANCE.
	cam.global_position = Vector3(0, 1.6, mirror.ACTIVE_DISTANCE + 15.0)
	mirror._process(0.016)
	if mirror._viewport.render_target_update_mode == SubViewport.UPDATE_DISABLED:
		print("PASS: rendering stops when the viewer is out of range")
	else:
		_fail("mirror kept rendering a full scene pass from beyond ACTIVE_DISTANCE")

	# Standing BEHIND the glass, where a mirror has no image at all.
	cam.global_position = Vector3(0, 1.6, -2.0)
	mirror._process(0.016)
	if mirror._viewport.render_target_update_mode == SubViewport.UPDATE_DISABLED:
		print("PASS: rendering stops when the viewer is behind the mirror")
	else:
		_fail("mirror kept rendering while the viewer stood behind it")

	# Back in front and close -- it must actually switch on again.
	cam.global_position = Vector3(0, 1.6, 3.0)
	mirror._process(0.016)
	if mirror._viewport.render_target_update_mode == SubViewport.UPDATE_ALWAYS:
		print("PASS: rendering resumes once the viewer is in front and in range")
	else:
		_fail("mirror never resumed rendering for a viewer standing right in front of it")

	cam.queue_free()
