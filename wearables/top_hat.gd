extends "res://wearables/wearable.gd"
## A classic black top hat -- brim + tapered crown + a contrasting band,
## built from plain CylinderMesh primitives, same "recognizable silhouette
## from cheap procedural primitives" style every other prop in this project
## already uses (see npc/wolf.gd, npc/sloth.gd). Local origin sits at the
## BRIM's own bottom face, so _head_position() just needs to return "the top of
## the head" (see wearable.gd's own class comment on the exact head
## geometry this is measured against) with the whole hat naturally
## extending upward from there.

const BRIM_RADIUS := 0.4
const BRIM_HEIGHT := 0.04
const CROWN_BOTTOM_RADIUS := 0.29
const CROWN_TOP_RADIUS := 0.27
const CROWN_HEIGHT := 0.32
const BAND_RADIUS := 0.295
const BAND_HEIGHT := 0.06

const HAT_COLOR := Color(0.03, 0.03, 0.035)
const BAND_COLOR := Color(0.09, 0.02, 0.02)

func get_wearable_slot() -> String:
	return "hat"

func get_item_id() -> String:
	return "top_hat"

# Sits right on top of the head (see wearable.gd's own class comment --
# head top is local (0, 0.7, 0) relative to get_head_node()'s own pivot).
func _head_position() -> Vector3:
	return Vector3(0, 0.68, 0)

func _head_rotation() -> Vector3:
	return Vector3.ZERO

func _build_visual() -> void:
	var hat_mat := StandardMaterial3D.new()
	hat_mat.albedo_color = HAT_COLOR
	hat_mat.roughness = 0.4
	hat_mat.metallic = 0.05

	var band_mat := StandardMaterial3D.new()
	band_mat.albedo_color = BAND_COLOR
	band_mat.roughness = 0.5

	var brim := MeshInstance3D.new()
	var brim_mesh := CylinderMesh.new()
	brim_mesh.top_radius = BRIM_RADIUS
	brim_mesh.bottom_radius = BRIM_RADIUS
	brim_mesh.height = BRIM_HEIGHT
	brim.mesh = brim_mesh
	brim.material_override = hat_mat
	brim.position = Vector3(0, BRIM_HEIGHT * 0.5, 0)
	add_child(brim)

	var crown := MeshInstance3D.new()
	var crown_mesh := CylinderMesh.new()
	crown_mesh.top_radius = CROWN_TOP_RADIUS
	crown_mesh.bottom_radius = CROWN_BOTTOM_RADIUS
	crown_mesh.height = CROWN_HEIGHT
	crown.mesh = crown_mesh
	crown.material_override = hat_mat
	crown.position = Vector3(0, BRIM_HEIGHT + CROWN_HEIGHT * 0.5, 0)
	add_child(crown)

	var band := MeshInstance3D.new()
	var band_mesh := CylinderMesh.new()
	band_mesh.top_radius = BAND_RADIUS
	band_mesh.bottom_radius = BAND_RADIUS
	band_mesh.height = BAND_HEIGHT
	band.mesh = band_mesh
	band.material_override = band_mat
	band.position = Vector3(0, BRIM_HEIGHT + BAND_HEIGHT * 0.5, 0)
	add_child(band)
