extends "res://wearables/clothing.gd"
## A plain crew-neck short-sleeve t-shirt -- an original basic silhouette
## (a tapered torso tube, a short capped sleeve on each shoulder, and an
## inset collar ring), no reference photo involved. Deliberately basic --
## see clothing.gd's own class comment for why (color/texture/uploaded-PNG
## customization is explicit FUTURE work, not built here): every piece
## shares ONE cached StandardMaterial3D (_shirt_material) so a later
## customization pass has exactly one place to swap albedo_color/
## albedo_texture, instead of hunting down N separate per-piece materials.

const TOP_RADIUS := 0.37
const BOTTOM_RADIUS := 0.315
# Longer hem -- matches the length of a painted-on tee design, closer to hip
# height than the original short-cropped cut.
const HEM_Y := 1.02
const COLLAR_RADIUS := 0.205
const COLLAR_INSET_Y := 0.02
# The torso tube's own top point sits a bit ABOVE FusedBody.SHOULDER_Y
# (rather than exactly at it) so it rises up over the shoulder/deltoid
# instead of ending in a flat disc right at the shoulder joint's own
# height -- that flat-top cap was what left a bare-skin gap between the
# torso panel and the sleeve in the first pass.
const SHOULDER_RISE := 0.06
# How far down the shoulder->hand line the short sleeve reaches (0=shoulder,
# 1=hand) -- stays well inside arm_l/r's own bone span (see
# FusedBody.ELBOW_T) so the whole sleeve follows ONE bone's rotation, no
# elbow split needed the way hoodie.gd's own long sleeve does.
const SLEEVE_T := 0.28
# The sleeve's own shoulder-end point is pulled INWARD/UP from the bare
# shoulder joint toward the raised torso top (see SLEEVE_START_BLEND) and
# given a bigger radius here -- together these make the sleeve's own cap
# overlap the torso's raised shoulder area instead of just touching it at a
# single point, closing the gap SHOULDER_RISE alone doesn't fully cover.
const SLEEVE_START_BLEND := 0.35
const SLEEVE_SHOULDER_RADIUS := 0.17
const SLEEVE_HEM_RADIUS := 0.14

const SHIRT_COLOR := Color(0.86, 0.86, 0.88)

var _shirt_material: StandardMaterial3D

func get_wearable_slot() -> String:
	return "top"

func get_item_id() -> String:
	return "t_shirt"

func _build_visual() -> void:
	_shirt_material = StandardMaterial3D.new()
	_shirt_material.albedo_color = SHIRT_COLOR
	_shirt_material.roughness = 0.85
	_shirt_material.metallic = 0.0

	var torso_top := Vector3(0, FusedBody.SHOULDER_Y + SHOULDER_RISE, 0)
	_add_tube(
		torso_top, Vector3(0, HEM_Y, 0),
		TOP_RADIUS, BOTTOM_RADIUS, _shirt_material, "spine"
	)
	# Collar ring -- narrower than the torso's own top radius so it reads as
	# an inset crew neck rather than just the torso tube's own open top edge.
	_add_tube(
		torso_top + Vector3(0, COLLAR_INSET_Y, 0), torso_top + Vector3(0, -COLLAR_INSET_Y, 0),
		COLLAR_RADIUS, COLLAR_RADIUS, _shirt_material, "spine"
	)

	for side in [-1.0, 1.0]:
		var bone_suffix := "l" if side < 0.0 else "r"
		var shoulder := Vector3(side * FusedBody.SHOULDER_X, FusedBody.SHOULDER_Y, 0)
		var hand := Vector3(side * FusedBody.HAND_X, FusedBody.HAND_Y, 0)
		var sleeve_start := shoulder.lerp(torso_top, SLEEVE_START_BLEND)
		var sleeve_end := shoulder.lerp(hand, SLEEVE_T)
		_add_tube(sleeve_start, sleeve_end, SLEEVE_SHOULDER_RADIUS, SLEEVE_HEM_RADIUS, _shirt_material, "arm_" + bone_suffix)
