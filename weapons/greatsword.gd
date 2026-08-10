extends "res://weapons/two_handed_sword.gd"
## A pick-up-able greatsword: sits planted blade-down on the ground until a
## nearby player looks toward it and presses "interact" (E), at which point
## it reparents itself onto the player's own Character hand bone and stops
## being a pickup -- see two_handed_sword.gd (this script's own base class)
## for all of that shared plumbing, plus the shared grip-pose/interact/
## manual-override system every two-handed sword in this project uses.
##
## The model is entirely procedural (a SurfaceTool blade + primitive meshes
## for grip/guard/pommel), same "no external asset files" approach every
## other visual in this project already uses (fused_body.gd, city_chunk.gd).
## It's a STYLIZED APPROXIMATION of a reference image (a dark curved
## kopis-style blade with an ornate gold hilt) -- procedural code can't
## replicate fine sculpted filigree, so this leans on silhouette (the
## crescent curve, the blade widening then tapering to a point) and a
## simple gold spine inlay to read as "ornate" instead of trying to
## reproduce the reference's lion-face engraving literally.
##
## Has a full moveset (see character.gd's own set_wielding_sword() and
## every sword_* clip it switches on), turned on the instant _equip() runs.

# This is the PRE-MODEL_SCALE radius -- the actual rendered thickness is
# this times MODEL_SCALE (0.05 * 2.0 = 0.1 world-space radius). Went
# through 3 sizes, not 1: the original 0.018 (post-scale 0.029 at the old
# MODEL_SCALE=1.6) got swallowed inside this character's own chunky
# FusedBody.ARM_R=0.09 hand capsule (no individual finger geometry to
# visually "wrap" a thin handle at all, see below); 0.045 (post-scale 0.09
# at MODEL_SCALE=2.0, tried next) overcorrected -- BOTH the grip radius AND
# the overall model scale had gone up from their original values, and
# multiplying them together landed the grip at 100% of the arm's own
# radius, reported back as looking like a thick club, not a sword handle.
# This value (post-scale 0.05, ~56% of the arm radius) is the deliberately
# re-derived middle ground: thinner than the arm (so it still reads as
# something a hand is wrapped AROUND) but thick enough not to disappear
# inside it, checking the ACTUAL POST-SCALE result against the arm radius
# directly this time instead of tuning either number in isolation.
const GRIP_RADIUS := 0.025
const POMMEL_RADIUS := 0.032
const BLADE_LENGTH := 1.05
const GUARD_ARM_LEN := 0.11
const GUARD_ARM_RADIUS := 0.013
const GUARD_BOSS_RADIUS := 0.045

const BLADE_COLOR := Color(0.04, 0.035, 0.04)
const GOLD_COLOR := Color(0.62, 0.47, 0.16)
const GRIP_COLOR := Color(0.05, 0.03, 0.02)

# (t, edge_x, width, thickness) keyframes along the blade, t=0 at the guard,
# t=1 at the tip -- see two_handed_sword.gd's own _blade_slice comment for
# what edge_x actually means. width/thickness both hit exactly 0 at t=1 so
# the last cross-section collapses to a single point.
const BLADE_PROFILE := [
	[0.0, 0.0, 0.06, 0.022],
	[0.3, -0.025, 0.075, 0.020],
	[0.6, -0.09, 0.125, 0.015],
	[0.85, -0.17, 0.16, 0.008],
	[1.0, -0.24, 0.0, 0.0],
]
const BLADE_SLICES := 28

## Grip/pommel/guard, all simple primitives in the same "boxes and
## cylinders" low-poly style the old bicycle.gd's own _build_visual used --
## see this file's own class comment on why the guard's ornate lion-face
## medallion is approximated as a plain round boss instead.
func get_item_id() -> String:
	return "greatsword"

func _build_visual() -> void:
	var blade_mat := _flat_material(BLADE_COLOR, 0.55, 0.4)
	var gold_mat := _flat_material(GOLD_COLOR, 0.9, 0.3)
	var grip_mat := _flat_material(GRIP_COLOR, 0.1, 0.85)

	var slices := _build_blade(BLADE_PROFILE, BLADE_LENGTH, BLADE_SLICES, blade_mat)

	# Gold fuller inlay tracing the spine's own centerline -- echoes the
	# reference image's engraved gold line down the blade without trying to
	# reproduce its actual filigree pattern.
	var spine_points: Array = []
	for s in slices:
		spine_points.append((s["front"] + s["back"]) / 2.0)
	for i in range(spine_points.size() - 1):
		_add_tube(spine_points[i], spine_points[i + 1], 0.006, 0.006, gold_mat)

	_add_tube(Vector3(0, -GRIP_LEN, 0), Vector3(0, -0.015, 0), GRIP_RADIUS, GRIP_RADIUS * 1.15, grip_mat)
	for i in range(1, 4):
		var ring_y: float = -GRIP_LEN * (float(i) / 4.0)
		_add_tube(Vector3(0, ring_y - 0.008, 0), Vector3(0, ring_y + 0.008, 0), GRIP_RADIUS * 1.2, GRIP_RADIUS * 1.2, gold_mat)

	var pommel := MeshInstance3D.new()
	var pommel_mesh := SphereMesh.new()
	pommel_mesh.radius = POMMEL_RADIUS
	pommel_mesh.height = POMMEL_RADIUS * 2.0
	pommel.mesh = pommel_mesh
	pommel.material_override = gold_mat
	pommel.position = Vector3(0, -GRIP_LEN - POMMEL_RADIUS * 0.6, 0)
	add_child(pommel)

	_add_tube(Vector3(0, 0, 0), Vector3(-GUARD_ARM_LEN, 0.02, 0.02), GUARD_ARM_RADIUS, GUARD_ARM_RADIUS * 0.5, gold_mat)
	_add_tube(Vector3(0, 0, 0), Vector3(GUARD_ARM_LEN, 0.02, -0.02), GUARD_ARM_RADIUS, GUARD_ARM_RADIUS * 0.5, gold_mat)

	var boss := MeshInstance3D.new()
	var boss_mesh := SphereMesh.new()
	boss_mesh.radius = GUARD_BOSS_RADIUS
	boss_mesh.height = GUARD_BOSS_RADIUS * 1.6
	boss.mesh = boss_mesh
	boss.material_override = gold_mat
	boss.position = Vector3(0, 0.0, 0.01)
	add_child(boss)
