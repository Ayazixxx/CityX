extends Node
## Regression check for a real reported bug (an in-game screenshot showing
## both swords' blades visibly off-center from their own hilt): the blade's
## cross-section at its base (where it meets the guard) must straddle local
## X=0 symmetrically, matching the guard/grip/pommel, which are all built
## symmetric about X=0 (see each weapon's own _build_visual). Checks both
## weapons since they share the same underlying _blade_slice formula and
## the same fix.

const GreatswordScene := preload("res://weapons/greatsword.tscn")
const MartyrBladeScene := preload("res://weapons/martyr_blade.tscn")

var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _check(name: String, sword: Node3D) -> void:
	var base: Dictionary = sword._blade_slice(sword.BLADE_PROFILE, sword.BLADE_LENGTH, 0.0)
	var edge: Vector3 = base["edge"]
	var front: Vector3 = base["front"]
	var center_x: float = (edge.x + front.x) / 2.0
	if is_equal_approx(center_x, 0.0) or absf(center_x) < 0.001:
		print("PASS: ", name, "'s blade base is centered on the hilt axis (center_x=", center_x, ")")
	else:
		_fail("%s's blade base center_x is %s, expected ~0 (centered on the hilt)" % [name, center_x])
	# Also confirm it's not degenerately centered by both edge and front
	# collapsing to the same point (a trivially "centered" but wrong case).
	if absf(edge.x - front.x) < 0.001:
		_fail("%s's blade base has zero width (edge.x == front.x == %s)" % [name, edge.x])
	else:
		print("PASS: ", name, "'s blade base has real width (edge.x=", edge.x, ", front.x=", front.x, ")")

func _ready() -> void:
	var greatsword := GreatswordScene.instantiate()
	add_child(greatsword)
	_check("greatsword", greatsword)

	var martyr := MartyrBladeScene.instantiate()
	add_child(martyr)
	_check("martyr blade", martyr)

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
