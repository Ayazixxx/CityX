extends SceneTree
## Prints the extreme thigh swing (degrees from straight down, forward positive) in the Godot run/walk/idle clips.

func _init() -> void:
	var skeleton_bones := ["hips", "spine", "neck", "head", "arm_l", "forearm_l", "hand_l", "arm_r", "forearm_r", "hand_r", "thigh_l", "shin_l", "thigh_r", "shin_r"]
	for clip_name in ["idle", "walk", "run"]:
		var anim: Animation = load("res://character/animations/%s.tres" % clip_name)
		var tracks := {}
		for t in range(anim.get_track_count()):
			var path := str(anim.track_get_path(t))
			if path.ends_with("/rotation") and path.begins_with("BodySkeleton:bones/"):
				var idx := int(path.get_slice("/", 1))
				tracks[skeleton_bones[idx]] = t
		var max_fwd := -999.0
		var min_fwd := 999.0
		var max_knee := 0.0
		var hips_track = tracks.get("hips", -1)
		var steps := 60
		for s in range(steps):
			var time := anim.length * float(s) / float(steps)
			var hips_q := Quaternion.IDENTITY
			if hips_track != -1:
				hips_q = anim.rotation_track_interpolate(hips_track, time) if anim.track_get_type(hips_track) == Animation.TYPE_ROTATION_3D else anim.value_track_interpolate(hips_track, time)
			for side in ["thigh_l", "thigh_r"]:
				var q: Quaternion = anim.value_track_interpolate(tracks[side], time)
				var dir := (hips_q * q) * Vector3(0, -1, 0)
				var fwd := rad_to_deg(atan2(dir.z, -dir.y))
				max_fwd = maxf(max_fwd, fwd)
				min_fwd = minf(min_fwd, fwd)
			for side in ["shin_l", "shin_r"]:
				var q2: Quaternion = anim.value_track_interpolate(tracks[side], time)
				max_knee = maxf(max_knee, rad_to_deg(q2.get_angle()))
		print(clip_name, " length=", snappedf(anim.length, 0.01), " thigh swing forward max=", snappedf(max_fwd, 0.1), " back max=", snappedf(min_fwd, 0.1), " knee bend max=", snappedf(max_knee, 0.1))
	quit(0)
