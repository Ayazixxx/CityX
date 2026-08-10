extends "res://weapons/two_handed_sword.gd"
## A third pick-up-able two-handed sword -- see two_handed_sword.gd (this
## script's own base class) for the shared pickup/equip/grip-pose/manual-
## override plumbing every two-handed sword in this project uses; only the
## procedural visual differs here. Approximates a reference image: a dark
## gunmetal blade etched with a GLOWING red rune line down its center and a
## matching glow tracing both edges, a crossguard with a large glowing red
## eye/gem flanked by swept wing-like flares, a dark leather grip with red
## accent bands, and a stylized dragon-head pommel with small glowing red
## eyes and fangs. The glow (StandardMaterial3D.emission, see
## _glow_material) is new to this sword specifically -- neither greatsword.gd
## nor martyr_blade.gd needed it, so it stays local here rather than added
## to the shared base until a second sword actually wants it too.
##
## The dragon head is a stylized approximation built entirely from the same
## primitives every other sword already uses (tubes + spheres) -- procedural
## code can't sculpt real scales/facial detail, so this leans on silhouette
## (a tapered snout, small fang tubes, glowing eye spheres, swept back horns)
## rather than attempting literal dragon anatomy.

const GRIP_RADIUS := 0.025
const BLADE_LENGTH := 1.05
const GUARD_WING_LEN := 0.13
const GEM_RADIUS := 0.035
const POMMEL_RADIUS := 0.032
const POMMEL_SNOUT_LEN := 0.09

const BLADE_COLOR := Color(0.045, 0.045, 0.05)
const METAL_ACCENT_COLOR := Color(0.22, 0.21, 0.24)
const GRIP_COLOR := Color(0.045, 0.028, 0.024)
const GRIP_ACCENT_COLOR := Color(0.5, 0.05, 0.04)
const GLOW_COLOR := Color(0.85, 0.08, 0.05)

# Same (t, edge_x, width, thickness) keyframe format as two_handed_sword.gd's
# own _blade_slice comment describes -- a wide, mostly straight blade with
# a lean taper to a single point, similar spirit to martyr_blade.gd's own
# profile but slightly narrower/leaner to match this reference's silhouette.
const BLADE_PROFILE := [
	[0.0, 0.0, 0.065, 0.020],
	[0.4, 0.0, 0.07, 0.018],
	[0.75, -0.02, 0.055, 0.013],
	[0.92, -0.06, 0.03, 0.007],
	[1.0, -0.1, 0.0, 0.0],
]
const BLADE_SLICES := 28

func get_item_id() -> String:
	return "runeblade"

func _glow_material(color: Color, energy: float) -> StandardMaterial3D:
	var mat := StandardMaterial3D.new()
	mat.albedo_color = color
	mat.emission_enabled = true
	mat.emission = color
	mat.emission_energy_multiplier = energy
	mat.metallic = 0.1
	mat.roughness = 0.3
	mat.metallic_specular = 0.0
	return mat

func _build_visual() -> void:
	var blade_mat := _flat_material(BLADE_COLOR, 0.6, 0.35)
	var accent_mat := _flat_material(METAL_ACCENT_COLOR, 0.75, 0.3)
	var grip_mat := _flat_material(GRIP_COLOR, 0.05, 0.85)
	var grip_accent_mat := _flat_material(GRIP_ACCENT_COLOR, 0.2, 0.6)
	var glow_mat := _glow_material(GLOW_COLOR, 4.0)

	var slices := _build_blade(BLADE_PROFILE, BLADE_LENGTH, BLADE_SLICES, blade_mat)

	# Glowing rune line down the blade's own centerline, plus a matching
	# glow tracing the cutting edge -- two parallel glow lines instead of
	# greatsword.gd's single raised gold spine, echoing the reference's
	# glowing rune-etched center AND glowing edge.
	var center_points: Array = []
	var edge_points: Array = []
	for s in slices:
		center_points.append((s["front"] + s["back"]) / 2.0)
		edge_points.append(s["edge"])
	for i in range(center_points.size() - 1):
		_add_tube(center_points[i], center_points[i + 1], 0.005, 0.005, glow_mat)
	for i in range(edge_points.size() - 1):
		_add_tube(edge_points[i], edge_points[i + 1], 0.003, 0.003, glow_mat)

	_build_grip(grip_mat, grip_accent_mat)
	_build_dragon_head_pommel(Vector3(0, -GRIP_LEN, 0), accent_mat, glow_mat)
	_build_guard(accent_mat, glow_mat)

## Grip wrapped in alternating dark-leather/red-accent bands -- same
## "alternating tapered tube segments" technique martyr_blade.gd's own
## segmented grip uses, just with a red accent color instead of bronze.
func _build_grip(grip_mat: Material, accent_mat: Material) -> void:
	const SEGMENT_COUNT := 6
	for i in range(SEGMENT_COUNT):
		var t0: float = float(i) / float(SEGMENT_COUNT)
		var t1: float = float(i + 1) / float(SEGMENT_COUNT)
		var y0: float = -GRIP_LEN * (1.0 - t0)
		var y1: float = -GRIP_LEN * (1.0 - t1)
		_add_tube(Vector3(0, y0, 0), Vector3(0, y1 - 0.006, 0), GRIP_RADIUS, GRIP_RADIUS, grip_mat)
		_add_tube(Vector3(0, y1 - 0.006, 0), Vector3(0, y1, 0), GRIP_RADIUS * 1.15, GRIP_RADIUS * 1.15, accent_mat)

## Stylized dragon head at the pommel end: a tapered snout, 2 small fang
## tubes, 2 glowing eye spheres, and 2 small back-swept horns -- all built
## from the same tube/sphere primitives every other sword already uses (see
## this file's own class comment on why this is a silhouette approximation,
## not literal anatomy).
func _build_dragon_head_pommel(grip_end: Vector3, mat: Material, glow_mat: Material) -> void:
	var snout_tip: Vector3 = grip_end + Vector3(0, -POMMEL_SNOUT_LEN, 0)
	_add_tube(grip_end, snout_tip, POMMEL_RADIUS, POMMEL_RADIUS * 0.35, mat)

	for side in [-1.0, 1.0]:
		var fang_root: Vector3 = snout_tip + Vector3(side * POMMEL_RADIUS * 0.25, POMMEL_RADIUS * 0.15, 0)
		var fang_tip: Vector3 = fang_root + Vector3(0, -POMMEL_RADIUS * 0.55, 0)
		_add_tube(fang_root, fang_tip, POMMEL_RADIUS * 0.12, 0.001, mat)

		var eye := MeshInstance3D.new()
		var eye_mesh := SphereMesh.new()
		eye_mesh.radius = POMMEL_RADIUS * 0.16
		eye_mesh.height = POMMEL_RADIUS * 0.32
		eye.mesh = eye_mesh
		eye.material_override = glow_mat
		eye.position = grip_end + Vector3(side * POMMEL_RADIUS * 0.35, -POMMEL_RADIUS * 0.15, POMMEL_RADIUS * 0.32)
		add_child(eye)

		var horn_root: Vector3 = grip_end + Vector3(side * POMMEL_RADIUS * 0.3, POMMEL_RADIUS * 0.3, -POMMEL_RADIUS * 0.15)
		var horn_tip: Vector3 = horn_root + Vector3(side * POMMEL_RADIUS * 0.35, POMMEL_RADIUS * 0.7, -POMMEL_RADIUS * 0.3)
		_add_tube(horn_root, horn_tip, POMMEL_RADIUS * 0.1, 0.001, mat)

## Crossguard: a large glowing red gem at the center flanked by swept,
## flat wing-like flares (a small hand-built 2-triangle membrane per side,
## same _tri technique wings.gd's own membrane uses) instead of round tube
## arms -- reads as more angular/bat-wing-like, matching this reference's
## guard shape.
func _build_guard(mat: Material, glow_mat: Material) -> void:
	for side in [-1.0, 1.0]:
		var st := SurfaceTool.new()
		st.begin(Mesh.PRIMITIVE_TRIANGLES)
		var root := Vector3(side * 0.015, 0.0, 0.0)
		var tip := Vector3(side * GUARD_WING_LEN, 0.045, -0.02)
		var notch := Vector3(side * GUARD_WING_LEN * 0.55, 0.0, 0.05)
		var lower := Vector3(side * 0.025, -0.03, 0.06)
		_tri(st, root, tip, notch)
		_tri(st, root, notch, lower)
		var mesh_inst := MeshInstance3D.new()
		mesh_inst.mesh = st.commit()
		mesh_inst.material_override = mat
		add_child(mesh_inst)

	var gem := MeshInstance3D.new()
	var gem_mesh := SphereMesh.new()
	gem_mesh.radius = GEM_RADIUS
	gem_mesh.height = GEM_RADIUS * 1.6
	gem.mesh = gem_mesh
	gem.material_override = glow_mat
	gem.position = Vector3(0, 0.0, 0.02)
	add_child(gem)
