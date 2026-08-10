extends SceneTree
## One-off bake for the Scratcher's own three clips, following this
## project's established "small dedicated bake script per creature" pattern
## (see tests/scratch_intro_fall_bake.gd's own precedent).
##
## The source asset ships exactly ONE animation -- a 1-second walk. So:
##   walk   = that clip, looped, with root translation neutralised.
##   run    = DERIVED from the walk (time-compressed + leg swing amplified),
##            because no run clip exists for this model anywhere.
##   attack = AUTHORED from scratch (head rears up, then slams down), since
##            nothing like it exists in the source at all.

const OUT_DIR := "res://npc/scratcher/animations"

# The run is the walk played through in 0.55s instead of 1.0s...
const RUN_TIME_SCALE := 0.55
# ...with every leg joint's own swing away from its rest pose widened, so it
# reads as a real gallop rather than a walk on fast-forward. Applied as a
# slerp OUT from rest, which scales the rotation's own angle while keeping
# its axis -- multiplying the quaternion's components directly would skew
# the axis too and twist the legs off-plane.
const RUN_LEG_AMPLIFY := 1.45
# A little extra vertical bob on the hips, since a gallop has real flight
# time a walk doesn't.
const RUN_BOB_METERS := 0.012

# --- Attack: rear the head up, then slam it down. ---
const ATTACK_LENGTH := 0.75
const ATTACK_WINDUP_UP_DEGREES := 30.0
const ATTACK_SLAM_DOWN_DEGREES := -42.0
# Keyed asymmetrically on purpose: a slow rear-up then a fast slam is what
# makes the hit read as a strike rather than a nod.
const ATTACK_WINDUP_END := 0.34
const ATTACK_SLAM_END := 0.46

func _init() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUT_DIR))

	var walk_scene: Node = load("res://npc/scratcher/scratcher_walk.glb").instantiate()
	root.add_child(walk_scene)
	var skel: Skeleton3D = walk_scene.get_node("Armature/Skeleton3D")
	var ap: AnimationPlayer = walk_scene.get_node("AnimationPlayer")
	var source: Animation = ap.get_animation(ap.get_animation_list()[0])

	var walk := _build_walk(source, skel)
	_save(walk, "scratcher_walk.tres")

	var run := _build_run(walk, skel)
	_save(run, "scratcher_run.tres")

	var attack := _build_attack(skel)
	_save(attack, "scratcher_attack.tres")

	print("baked walk len=%.2f run len=%.2f attack len=%.2f" % [walk.length, run.length, attack.length])
	quit(0)

## The source clip, looped, with the hips' own horizontal translation
## removed. Meshy exports animate in place, but any residual forward drift
## on the root would fight npc/scratcher.gd's own velocity-driven movement
## and make the creature slide -- the same class of root-motion mismatch
## the mutant's own clips already had to be corrected for.
func _build_walk(source: Animation, skel: Skeleton3D) -> Animation:
	var out: Animation = source.duplicate(true)
	out.loop_mode = Animation.LOOP_LINEAR
	for t in range(out.get_track_count()):
		if out.track_get_type(t) != Animation.TYPE_POSITION_3D:
			continue
		var bone_name := _track_bone(out, t)
		if bone_name != "Hips":
			continue
		var idx := skel.find_bone(bone_name)
		if idx < 0:
			continue
		var rest := skel.get_bone_rest(idx).origin
		for k in range(out.track_get_key_count(t)):
			var value: Vector3 = out.track_get_key_value(t, k)
			# Vertical bob is real animation and stays; X/Z would be travel.
			out.track_set_key_value(t, k, Vector3(rest.x, value.y, rest.z))
	return out

func _build_run(walk: Animation, skel: Skeleton3D) -> Animation:
	var out: Animation = walk.duplicate(true)
	out.loop_mode = Animation.LOOP_LINEAR
	out.length = walk.length * RUN_TIME_SCALE

	for t in range(out.get_track_count()):
		var bone_name := _track_bone(out, t)
		var idx := skel.find_bone(bone_name)
		var is_leg := bone_name.contains("leg")
		var track_type := out.track_get_type(t)
		for k in range(out.track_get_key_count(t)):
			out.track_set_key_time(t, k, out.track_get_key_time(t, k) * RUN_TIME_SCALE)
			if idx < 0:
				continue
			if track_type == Animation.TYPE_ROTATION_3D and is_leg:
				var rest_rotation := skel.get_bone_rest(idx).basis.get_rotation_quaternion()
				var key: Quaternion = out.track_get_key_value(t, k)
				# Widen the swing by slerping PAST the animated pose,
				# starting from rest -- t>1 extrapolates along the same arc.
				out.track_set_key_value(t, k, rest_rotation.slerp(key, RUN_LEG_AMPLIFY).normalized())
			elif track_type == Animation.TYPE_POSITION_3D and bone_name == "Hips":
				var value: Vector3 = out.track_get_key_value(t, k)
				var phase := float(k) / maxf(1.0, float(out.track_get_key_count(t) - 1))
				# Two bounces per stride -- a quadruped's own gallop pushes
				# off twice per cycle, not once.
				out.track_set_key_value(t, k, value + Vector3(0, sin(phase * TAU * 2.0) * RUN_BOB_METERS, 0))
	return out

## Authored from nothing: the head rears up and then slams down, which is
## the attack that was asked for. Rotation is applied about the skeleton's
## own X axis expressed in the PARENT bone's space -- an animation rotation
## track stores a bone's LOCAL rotation, so a raw Quaternion(Vector3.RIGHT,
## angle) would pitch the head about whatever arbitrary direction the
## rig's own local X happens to point, not about the world horizontal.
func _build_attack(skel: Skeleton3D) -> Animation:
	var out := Animation.new()
	out.length = ATTACK_LENGTH
	out.loop_mode = Animation.LOOP_NONE

	var head_idx := skel.find_bone("head")
	var chest_idx := skel.find_bone("chest")
	if head_idx < 0:
		push_error("no 'head' bone -- can't author the attack")
		return out

	# The parent's own rest basis converts a skeleton-space axis into the
	# space this bone's local rotation is expressed in.
	var parent_basis := Basis()
	if chest_idx >= 0:
		parent_basis = skel.get_bone_global_rest(chest_idx).basis
	var pitch_axis: Vector3 = (parent_basis.inverse() * Vector3.RIGHT).normalized()
	var head_rest := skel.get_bone_rest(head_idx).basis.get_rotation_quaternion()

	var track := out.add_track(Animation.TYPE_ROTATION_3D)
	out.track_set_path(track, NodePath("Armature/Skeleton3D:head"))
	out.track_set_interpolation_type(track, Animation.INTERPOLATION_LINEAR)

	# rest -> reared up -> slammed down -> settle back to rest.
	var keys := [
		{"time": 0.0, "degrees": 0.0},
		{"time": ATTACK_WINDUP_END, "degrees": ATTACK_WINDUP_UP_DEGREES},
		{"time": ATTACK_SLAM_END, "degrees": ATTACK_SLAM_DOWN_DEGREES},
		{"time": ATTACK_LENGTH, "degrees": 0.0},
	]
	for key in keys:
		var swing := Quaternion(pitch_axis, deg_to_rad(float(key["degrees"])))
		out.rotation_track_insert_key(track, float(key["time"]), (swing * head_rest).normalized())

	# The chest follows a fraction of the head's own arc, so the whole
	# front end drops into the slam instead of just the skull swivelling.
	if chest_idx >= 0:
		var chest_rest := skel.get_bone_rest(chest_idx).basis.get_rotation_quaternion()
		var chest_parent_idx := skel.get_bone_parent(chest_idx)
		var chest_parent_basis := Basis()
		if chest_parent_idx >= 0:
			chest_parent_basis = skel.get_bone_global_rest(chest_parent_idx).basis
		var chest_axis: Vector3 = (chest_parent_basis.inverse() * Vector3.RIGHT).normalized()
		var chest_track := out.add_track(Animation.TYPE_ROTATION_3D)
		out.track_set_path(chest_track, NodePath("Armature/Skeleton3D:chest"))
		out.track_set_interpolation_type(chest_track, Animation.INTERPOLATION_LINEAR)
		for key in keys:
			var swing := Quaternion(chest_axis, deg_to_rad(float(key["degrees"]) * 0.35))
			out.rotation_track_insert_key(chest_track, float(key["time"]), (swing * chest_rest).normalized())

	return out

func _track_bone(anim: Animation, track: int) -> String:
	var path := anim.track_get_path(track)
	var sub := path.get_concatenated_subnames()
	return sub

func _save(anim: Animation, file_name: String) -> void:
	var path := OUT_DIR + "/" + file_name
	var err := ResourceSaver.save(anim, path)
	if err != OK:
		push_error("failed to save %s (%d)" % [path, err])
