extends "res://weapons/two_handed_sword.gd"
## A second pick-up-able two-handed sword, "Blade of the First Martyr" --
## see two_handed_sword.gd (this script's own base class) for the shared
## pickup/equip/grip-pose/manual-override plumbing every two-handed sword in
## this project uses; only the procedural visual differs here: a wide,
## mostly STRAIGHT blackened blade with dark fuller grooves (rather than
## greatsword.gd's curved kopis edge), a swept gothic crossguard with small
## horn-like tips, a segmented/ridged grip, and a crown-like clawed pommel,
## all in a dark-steel/aged-bronze palette instead of the other sword's
## black-and-gold.

const GRIP_RADIUS := 0.025
const POMMEL_RADIUS := 0.032
const BLADE_LENGTH := 1.05
const GUARD_ARM_LEN := 0.11
const GUARD_ARM_RADIUS := 0.013
const GUARD_BOSS_RADIUS := 0.045

const BLADE_COLOR := Color(0.05, 0.05, 0.055)
const GROOVE_COLOR := Color(0.015, 0.015, 0.017)
const ACCENT_COLOR := Color(0.34, 0.28, 0.19) # aged/dark bronze, not gold
const GRIP_COLOR := Color(0.045, 0.032, 0.024)

# Same (t, edge_x, width, thickness) keyframe format as two_handed_sword.gd's
# own _blade_slice comment describes -- edge_x stays at/near 0 through most
# of the length here instead of curving increasingly negative, since this
# reference is a wide, mostly STRAIGHT blade, not a curved kopis. Only
# sweeps into a real edge_x offset right at the very end, for the
# reference's angled (not symmetric) point.
const BLADE_PROFILE := [
	[0.0, 0.0, 0.075, 0.020],
	[0.5, 0.0, 0.078, 0.018],
	[0.8, -0.01, 0.07, 0.014],
	[0.93, -0.05, 0.04, 0.008],
	[1.0, -0.09, 0.0, 0.0],
]
const BLADE_SLICES := 28

## Grip/pommel/guard -- same "boxes and cylinders" low-poly primitive style
## as greatsword.gd's own _build_visual, shaped differently to read as a
## segmented/ridged grip, a swept gothic crossguard with small horn tips,
## and a crown-like clawed pommel instead of that sword's smooth taper,
## straight guard, and plain round pommel.
func get_item_id() -> String:
	return "martyr_blade"

func _build_visual() -> void:
	var blade_mat := _flat_material(BLADE_COLOR, 0.55, 0.4)
	var accent_mat := _flat_material(ACCENT_COLOR, 0.85, 0.35)
	var grip_mat := _flat_material(GRIP_COLOR, 0.1, 0.85)

	var slices := _build_blade(BLADE_PROFILE, BLADE_LENGTH, BLADE_SLICES, blade_mat)

	# Three thin, DARK (not gold-inlay) fuller grooves tracing the blade's
	# own spine at three parallel offsets between the edge and the spine's
	# own centerline -- reads as recessed grooves cut into the blade rather
	# than a raised decorative inlay, matching the reference's parallel
	# fuller lines instead of greatsword.gd's single raised gold spine.
	var groove_mat := _flat_material(GROOVE_COLOR, 0.3, 0.6)
	for groove_t in [0.3, 0.5, 0.7]:
		var groove_points: Array = []
		for s in slices:
			groove_points.append(s["edge"].lerp((s["front"] + s["back"]) / 2.0, groove_t))
		for i in range(groove_points.size() - 1):
			_add_tube(groove_points[i], groove_points[i + 1], 0.004, 0.004, groove_mat)

	# Segmented/ridged grip: 6 short tapered bands instead of one smooth
	# taper with thin rings, reading as a stacked, articulated wrap.
	const SEGMENT_COUNT := 6
	for i in range(SEGMENT_COUNT):
		var t0: float = float(i) / float(SEGMENT_COUNT)
		var t1: float = float(i + 1) / float(SEGMENT_COUNT)
		var y0: float = -GRIP_LEN * (1.0 - t0)
		var y1: float = -GRIP_LEN * (1.0 - t1)
		# Alternate slightly wider "ridge" bands and slightly narrower
		# "valley" bands along the taper for a visibly segmented profile.
		var wide: float = GRIP_RADIUS * 1.15
		var narrow: float = GRIP_RADIUS * 0.9
		var r0: float = wide if i % 2 == 0 else narrow
		var r1: float = narrow if i % 2 == 0 else wide
		_add_tube(Vector3(0, y0, 0), Vector3(0, y1 - 0.006, 0), r0, r1, grip_mat)
		_add_tube(Vector3(0, y1 - 0.006, 0), Vector3(0, y1, 0), r1 * 1.2, r1 * 1.2, accent_mat)

	# Crown-like clawed pommel: a small tapered base plus 5 outward/upward
	# prongs arranged radially, instead of a plain sphere.
	var pommel_base := Vector3(0, -GRIP_LEN - POMMEL_RADIUS * 0.4, 0)
	_add_tube(Vector3(0, -GRIP_LEN, 0), pommel_base, GRIP_RADIUS, POMMEL_RADIUS * 0.7, accent_mat)
	var core := MeshInstance3D.new()
	var core_mesh := SphereMesh.new()
	core_mesh.radius = POMMEL_RADIUS * 0.6
	core_mesh.height = POMMEL_RADIUS * 1.2
	core.mesh = core_mesh
	core.material_override = accent_mat
	core.position = pommel_base
	add_child(core)
	const PRONG_COUNT := 5
	for i in range(PRONG_COUNT):
		var angle: float = TAU * float(i) / float(PRONG_COUNT)
		var out_dir := Vector3(cos(angle), 0.35, sin(angle)).normalized()
		var prong_tip: Vector3 = pommel_base + out_dir * POMMEL_RADIUS * 1.6
		_add_tube(pommel_base, prong_tip, POMMEL_RADIUS * 0.28, 0.002, accent_mat)

	# Swept gothic crossguard: each side sweeps outward then curves back
	# UP toward the blade in a second segment, ending in a small horn-like
	# point, instead of greatsword.gd's own single straight tapered arm.
	for side in [-1.0, 1.0]:
		var mid := Vector3(side * GUARD_ARM_LEN * 0.6, 0.015, 0.025 * side)
		var tip := Vector3(side * GUARD_ARM_LEN, 0.07, 0.03 * side)
		_add_tube(Vector3(0, 0, 0), mid, GUARD_ARM_RADIUS, GUARD_ARM_RADIUS * 0.8, accent_mat)
		_add_tube(mid, tip, GUARD_ARM_RADIUS * 0.8, 0.0025, accent_mat)
		# A small stud partway along each arm, echoing the reference's
		# gem/rivet detailing.
		var stud := MeshInstance3D.new()
		var stud_mesh := SphereMesh.new()
		stud_mesh.radius = GUARD_ARM_RADIUS * 1.4
		stud_mesh.height = GUARD_ARM_RADIUS * 2.8
		stud.mesh = stud_mesh
		stud.material_override = accent_mat
		stud.position = mid
		add_child(stud)

	var boss := MeshInstance3D.new()
	var boss_mesh := SphereMesh.new()
	boss_mesh.radius = GUARD_BOSS_RADIUS
	boss_mesh.height = GUARD_BOSS_RADIUS * 1.6
	boss.mesh = boss_mesh
	boss.material_override = accent_mat
	boss.position = Vector3(0, 0.0, 0.01)
	add_child(boss)
