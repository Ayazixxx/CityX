extends "res://wearables/wearable.gd"
## A weathered goalie-style hockey mask -- an imported model (Meshy AI
## "Weathered Hockey Mask" generate output, user-supplied), REPLACING an
## earlier fully-procedural version of this same wearable (plain primitive
## meshes layered into a mask silhouette). The source .obj was extreme for
## a real-time worn prop (183,500 verts / 367,195 faces, ~31MB, no UVs, no
## material) -- decimated via a headless Blender pass (see this repo's own
## scratchpad, not checked in -- ratio chosen to land near 6000 triangles)
## down to 5999 faces / ~18k exported vertices before being re-exported as
## res://wearables/hockey_mask_model.glb, the asset this file actually
## loads. No texture data survived the decimation either way (there was
## none to begin with -- the source's own mtllib reference pointed at a
## temp.mtl that was never actually alongside the .obj), so the "weathered"
## look here is a plain matte grey-white StandardMaterial3D, not a bake.
##
## Orientation: Godot's own glTF importer already applies the standard
## Blender-Zup -> glTF/Godot-Yup correction on load (confirmed directly --
## the imported MeshInstance3D's own global_transform carries a real
## rotation, and the resulting WORLD-space bounding box reads as a
## sensible upright mask: ~1.55 wide, ~1.9 tall, ~0.81 deep). Which
## direction the mask's own FRONT (convex, detailed) face points after
## that, though, couldn't be verified visually in this environment (no
## live render available) -- MODEL_YAW_DEGREES is the single knob to flip
## it 180 degrees if it turns out backwards in-game, and
## ui/dev_console.gd's own existing "/wearable unlock" + "/wearable pose"
## commands (already wired for any currently-worn hat/mask, see
## wearable.gd's own set_manual_override/print_current_pose) work for
## live-dragging this exact model to a corrected pose without needing a
## code change at all.
##
## Local origin sits at the model's own natural (roughly centered, not
## back-aligned the way the old procedural dome was) pivot -- MODEL_SCALE
## and the position/rotation _head_position()/_head_rotation() return
## account for that directly rather than assuming a back-center origin.

const MaskModel := preload("res://wearables/hockey_mask_model.glb")

# Raw model is ~1.55 x 1.9 x 0.81 (Blender units, treated as meters) --
# scaled down to a believable worn-mask width (~0.62), matching the OLD
# procedural mask's own final size (BASE_RADIUS 0.34 * OVAL_X/Y) closely
# enough that no other tuning (interact range, etc.) needed revisiting.
const MODEL_SCALE := 0.4
# See this file's own class comment -- 0.0 is a real guess (the exporter's
# own axis handling made this the more likely of the two options), not a
# confirmed-correct value. Flip to 180.0 if the mask shows up facing away
# from the camera in-game.
const MODEL_YAW_DEGREES := 0.0

const MASK_COLOR := Color(0.64, 0.62, 0.56) # weathered/dirtied off-white, duller than a fresh MASK_COLOR would be

func get_wearable_slot() -> String:
	return "mask"

func get_item_id() -> String:
	return "hockey_mask"

# Front of the face. POSITIVE Z, not negative -- a real reported bug (twice
# now, independently, on both this model AND the old fully-procedural mask
# it replaced, which used the SAME -Z-is-front assumption wearable.gd's own
# class comment documents): with a negative Z offset here, the mask
# consistently rendered on the BACK of the head instead of the face. Both
# masks agreeing on the same wrong side, using the same sign convention,
# points at the convention itself (this project's own head bone's actual
# local orientation) rather than either mask's own individual math -- see
# wearable.gd's own class comment, which may need revisiting if a THIRD
# wearable ever needs a real forward/back offset and hits the same issue.
# NOTE: this flips the sign based directly on the user's own two matching
# reports, not an independently re-confirmed measurement (no live render
# available in this environment) -- if it's now on the wrong side the OTHER
# way, the fix is the same one line, back to a negative Z.
func _head_position() -> Vector3:
	return Vector3(0, 0.30, 0.36)

func _head_rotation() -> Vector3:
	return Vector3(0, deg_to_rad(MODEL_YAW_DEGREES), 0)

func _build_visual() -> void:
	var model := MaskModel.instantiate()
	model.scale = Vector3.ONE * MODEL_SCALE
	add_child(model)

	var mat := StandardMaterial3D.new()
	mat.albedo_color = MASK_COLOR
	mat.roughness = 0.85
	mat.metallic = 0.0
	_apply_material_recursive(model, mat)

## The imported glb has no material/UVs of its own (see this file's own
## class comment on why) -- walks every MeshInstance3D in the imported
## scene and overrides its material, rather than assuming there's exactly
## one (the decimate/export pass was run once, headlessly, without a live
## check of exactly how many mesh nodes glTF export split the geometry
## into).
func _apply_material_recursive(node: Node, mat: Material) -> void:
	if node is MeshInstance3D:
		node.material_override = mat
	for child in node.get_children():
		_apply_material_recursive(child, mat)
