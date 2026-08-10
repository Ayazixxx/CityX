extends "res://wearables/clothing.gd"
## A plain pullover hoodie -- a longer/looser torso tube than t_shirt.gd, a
## kangaroo pocket, a hood draped against the upper back/neck, two hanging
## drawstrings, and full-length sleeves. The sleeves are split into an
## upper-arm piece (bone "arm_l"/"arm_r") and a forearm/cuff piece (bone
## "forearm_l"/"forearm_r") -- a single rigid piece can't follow both the
## shoulder's AND the elbow's own independent rotation, same reasoning
## clothing.gd's own class comment gives for splitting per-bone at all, just
## needed here and not on t_shirt.gd's own short sleeve because this one
## actually reaches past the elbow. An original basic silhouette, no
## reference photo involved. Same "just the model for now" scope as
## t_shirt.gd -- one shared _hoodie_material, no color/texture/PNG
## customization yet.

const TOP_RADIUS := 0.335
const BOTTOM_RADIUS := 0.34
const HEM_Y := 1.15

const HOOD_RADIUS := 0.24
const HOOD_HEIGHT_SCALE := 1.7

const POCKET_SIZE := Vector3(0.28, 0.16, 0.09)
const POCKET_Y := 1.24

const UPPER_SLEEVE_RADIUS := 0.15
const ELBOW_SLEEVE_RADIUS := 0.135
# Short of FusedBody.WRIST_T (0.85) so the cuff piece doesn't overlap the
# hand bone's own region -- see FusedBody.LIMB_BONES, forearm_l/r spans
# ELBOW_T..WRIST_T along the shoulder->hand line.
const CUFF_T := 0.78
const CUFF_RADIUS := 0.115

const DRAWSTRING_RADIUS := 0.012
const DRAWSTRING_LENGTH := 0.14

const HOODIE_COLOR := Color(0.22, 0.24, 0.28)
const DRAWSTRING_COLOR := Color(0.85, 0.83, 0.78)

var _hoodie_material: StandardMaterial3D

func get_wearable_slot() -> String:
	return "top"

func get_item_id() -> String:
	return "hoodie"

func _build_visual() -> void:
	_hoodie_material = StandardMaterial3D.new()
	_hoodie_material.albedo_color = HOODIE_COLOR
	_hoodie_material.roughness = 0.9
	_hoodie_material.metallic = 0.0

	_add_tube(
		Vector3(0, FusedBody.SHOULDER_Y, 0), Vector3(0, HEM_Y, 0),
		TOP_RADIUS, BOTTOM_RADIUS, _hoodie_material, "spine"
	)

	var pocket := MeshInstance3D.new()
	var pocket_mesh := BoxMesh.new()
	pocket_mesh.size = POCKET_SIZE
	pocket.mesh = pocket_mesh
	pocket.material_override = _hoodie_material
	var pocket_z := -(BOTTOM_RADIUS + POCKET_SIZE.z * 0.3)
	_add_piece(pocket, "spine", Transform3D(Basis(), Vector3(0, POCKET_Y, pocket_z)))

	# Draped against the upper back/neck (positive Z = behind, matching the
	# character's own -Z forward facing -- see wearables/hockey_mask.gd's
	# own class comment on this project's front/back sign convention),
	# resting rather than "up" over the head -- a raised hood would need its
	# own separate pose/animation state this first pass doesn't add.
	var hood := MeshInstance3D.new()
	var hood_mesh := SphereMesh.new()
	hood_mesh.radius = HOOD_RADIUS
	hood_mesh.height = HOOD_RADIUS * HOOD_HEIGHT_SCALE
	hood.mesh = hood_mesh
	hood.material_override = _hoodie_material
	_add_piece(hood, "neck", Transform3D(Basis(), Vector3(0, FusedBody.SHOULDER_Y + 0.12, 0.10)))

	var drawstring_mat := StandardMaterial3D.new()
	drawstring_mat.albedo_color = DRAWSTRING_COLOR
	drawstring_mat.roughness = 0.6
	for side in [-1.0, 1.0]:
		var top := Vector3(side * 0.05, FusedBody.SHOULDER_Y - 0.02, -TOP_RADIUS * 0.85)
		var bottom := top + Vector3(side * 0.01, -DRAWSTRING_LENGTH, 0)
		_add_tube(top, bottom, DRAWSTRING_RADIUS, DRAWSTRING_RADIUS, drawstring_mat, "spine")

	for side in [-1.0, 1.0]:
		var bone_suffix := "l" if side < 0.0 else "r"
		var shoulder := Vector3(side * FusedBody.SHOULDER_X, FusedBody.SHOULDER_Y, 0)
		var hand := Vector3(side * FusedBody.HAND_X, FusedBody.HAND_Y, 0)
		var elbow := shoulder.lerp(hand, FusedBody.ELBOW_T)
		var cuff := shoulder.lerp(hand, CUFF_T)
		_add_tube(shoulder, elbow, UPPER_SLEEVE_RADIUS, ELBOW_SLEEVE_RADIUS, _hoodie_material, "arm_" + bone_suffix)
		_add_tube(elbow, cuff, ELBOW_SLEEVE_RADIUS, CUFF_RADIUS, _hoodie_material, "forearm_" + bone_suffix)
