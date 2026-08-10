extends Node3D
## Shared base for every pick-up-able two-handed sword (greatsword.gd,
## martyr_blade.gd, and any new one) -- extracted once a THIRD sword showed
## up, per both of those files' own "if a third weapon shows up, that's the
## point to extract a shared base" comment. Holds everything that's
## genuinely IDENTICAL across every sword: the pickup/equip/interact flow,
## the grip-pose system (including the dev-only manual-override tuning
## path), and the generic procedural-mesh helpers (tubes, flat-shaded
## triangles, a lofted blade profile). A concrete sword subclass only needs
## to override _build_visual() with its own blade profile/colors/guard-grip-
## pommel shapes -- see greatsword.gd or martyr_blade.gd for a minimal
## example of what that override looks like now.
##
## GRIP_LEN/MODEL_SCALE/GRIP_HAND_ROTATION/GRIP_HAND_POSITION/
## HEAVY_ATTACK_GRIP_ROTATION/GRIP_CENTER_T/INTERACT_RANGE/LOOK_DOT_THRESHOLD
## live HERE rather than per-subclass specifically because every sword so
## far has been an explicit "same size, same moveset" ask -- a future sword
## that genuinely needs its own size/grip would still need its own override
## (GDScript doesn't polymorphically dispatch const lookups the way it does
## methods), but nothing so far has needed that.
##
## Blade-building (_blade_profile_at/_blade_slice/_build_blade) takes the
## profile array/length/slice-count as PARAMETERS rather than reading module-
## level consts, specifically so they're reusable here in the base without
## running into that same const-shadowing problem -- each subclass just
## passes its own BLADE_PROFILE/BLADE_LENGTH/BLADE_SLICES as arguments.

const GRIP_LEN := 0.20
const INTERACT_RANGE := 2.5
# cos(35 degrees) -- how far off dead-center the player's camera can point
# and still count as "looking at" the sword (see _player_is_looking_at_me)
# -- generous enough not to demand pixel-perfect aim, tight enough that
# just being nearby facing some unrelated direction doesn't trigger it.
const LOOK_DOT_THRESHOLD := 0.82

# Overall model scale -- applied to the whole node (see _ready), so it
# scales the floor pickup and the held version identically rather than
# popping to a different size on equip. Every sword built so far shares
# this exact value ("same size" was an explicit ask each time).
const MODEL_SCALE := 2.0

# Local rotation/position once gripped in hand_r -- see _equip/
# _update_held_pose. GRIP_HAND_ROTATION/GRIP_HAND_POSITION were originally
# measured from real mocap pose data, then later manually hand-tuned
# together via the dev console's own "/sword unlock" + Godot's Remote Scene
# Tree debugger (see _manual_override's own comment), read back with
# "/sword pose". GRIP_HAND_POSITION is NOT reproducible from
# _grip_position_for(GRIP_HAND_ROTATION) -- it's well outside that formula's
# fixed 0.1-unit offset, which is why it's its own explicit constant instead
# of derived.
const GRIP_HAND_ROTATION := Vector3(1.799434, 1.654572, 2.101377)
const GRIP_HAND_POSITION := Vector3(-0.055, -0.197, 0.343)
# A real mocap pack isn't a perfectly rigid two-point grip -- hand_r's own
# hand_r-to-hand_l direction, in hand_r's own local space, measurably
# differs between idle and sword_heavy_attack's own peak strike frame, so
# this stays a SEPARATE, still mocap-measured value, switched to every
# frame based on what's actually playing (see _update_held_pose). Solved
# against sword_heavy_attack's own peak strike frame (seek(1.4, true) into
# the trimmed clip) -- verified by re-applying it and confirming the
# blade's local +Y maps to world (0,-1,0) exactly at that frame (see
# tests/test_greatsword_check.gd's own regression check for this).
const HEAVY_ATTACK_GRIP_ROTATION := Vector3(-0.07405, 0.168671, -2.315635)

# How far along the grip (0=at the guard, 1=at the pommel end) hand_r
# should sit -- see _grip_position_for. 0.5 (centered) rather than 0.0 (the
# original choice, hand_r exactly at the guard/blade-base junction): real
# feedback was that the hands didn't read as gripping a handle at all --
# with hand_r pinned right at the guard, there was no grip material
# extending toward the guard side of the hand, so it looked more like the
# hand was just touching the guard than wrapped around a handle centered
# under it.
const GRIP_CENTER_T := 0.5

## Both rotations only fix 2 of 3 rotational degrees of freedom (aligning
## the grip axis) -- the remaining "roll" (which way the blade's FLAT/edge
## faces sideways) is still whatever Quaternion(from, to)'s own shortest-
## arc choice landed on for each, not something measured (there's no bone
## data this rig carries that encodes "which way should a bladed weapon's
## edge face"). Still a known, unverified gap.
##
## Only still used for HEAVY_ATTACK_GRIP_ROTATION's own position (computed
## once into _heavy_attack_grip_position below) -- GRIP_HAND_POSITION is a
## directly hand-tuned constant instead, since it isn't reproducible from
## this formula.
func _grip_position_for(rot: Vector3) -> Vector3:
	return Basis.from_euler(rot) * Vector3(0, GRIP_LEN * GRIP_CENTER_T, 0)

# Computed once in _ready() rather than hardcoded -- HEAVY_ATTACK_GRIP_ROTATION
# hasn't been hand-tuned like GRIP_HAND_POSITION was, so this keeps its
# original formula-derived behavior unchanged.
var _heavy_attack_grip_position: Vector3

@onready var _interact_area: Area3D = $InteractArea

var _nearby_player: CharacterBody3D = null
var _picked_up := false
# Set once by _equip() -- the Character whose current animation
# _update_held_pose() checks every frame to decide which grip rotation to
# show. Not the same thing as _nearby_player (that only tracks proximity
# before pickup); this is the actual wielder, tracked after.
var _wielding_character: Node = null
# Dev-only escape hatch (see set_manual_override(), toggled via the dev
# console's own "/sword unlock"/"/sword lock" -- ui/dev_console.gd) for
# tuning GRIP_HAND_ROTATION/HEAVY_ATTACK_GRIP_ROTATION visually instead of
# blind: while true, _update_held_pose() below leaves this node's own
# position/rotation alone entirely, so Godot's own Remote Scene Tree
# debugger (Debugger panel -> Remote tab, while the game is actually
# running) can edit this node's live Transform directly and have it
# actually stick, rather than getting overwritten the very next frame.
var _manual_override := false

func _ready() -> void:
	_build_visual()
	# Scales the whole node (mesh AND InteractArea both -- see MODEL_SCALE's
	# own comment; the enlarged interact radius is a harmless side effect,
	# not something compensated for, since the real pickup gate is
	# _player_is_looking_at_me's own fixed INTERACT_RANGE, not this Area3D's
	# radius).
	scale = Vector3.ONE * MODEL_SCALE
	_heavy_attack_grip_position = _grip_position_for(HEAVY_ATTACK_GRIP_ROTATION)
	_interact_area.body_entered.connect(_on_interact_area_body_entered)
	_interact_area.body_exited.connect(_on_interact_area_body_exited)

## Overridden by every concrete sword -- builds this sword's own procedural
## mesh (blade + grip + guard + pommel). Left empty here rather than
## abstract/required since GDScript has no such enforcement mechanism
## anyway; an override that forgets to call this just gets an invisible
## (but otherwise fully functional) sword, which is an obvious enough bug to
## spot immediately.
func _build_visual() -> void:
	pass

func _process(_delta: float) -> void:
	if _picked_up:
		_update_held_pose()
		return
	if _nearby_player == null:
		return
	if not Input.is_action_just_pressed("interact"):
		return
	if not _player_is_looking_at_me(_nearby_player):
		return
	_try_add_to_inventory(_nearby_player)

## Runs every frame once equipped -- picks HEAVY_ATTACK_GRIP_ROTATION only
## while sword_heavy_attack is actually the current animation, the ordinary
## GRIP_HAND_ROTATION otherwise (idle, walk, regular attack, block, ...),
## and recomputes position to match (see _grip_position_for -- the correct
## centering offset depends on which rotation is active). See
## GRIP_HAND_ROTATION's own comment for why this needs to switch at all
## instead of being one fixed transform.
func _update_held_pose() -> void:
	if _manual_override or _wielding_character == null:
		return
	var current_anim: String = _wielding_character.anim_player.current_animation
	if current_anim == "sword_heavy_attack":
		rotation = HEAVY_ATTACK_GRIP_ROTATION
		position = _heavy_attack_grip_position
	else:
		rotation = GRIP_HAND_ROTATION
		position = GRIP_HAND_POSITION

## Dev-only -- see _manual_override's own comment. enabled=true freezes
## this node's position/rotation exactly where they currently are (whatever
## _update_held_pose last set, or wherever a Remote Scene Tree edit left
## them) so they can be dragged/typed by hand from there; enabled=false
## hands control back to _update_held_pose, snapping back to whichever of
## GRIP_HAND_ROTATION/HEAVY_ATTACK_GRIP_ROTATION actually applies right now
## (NOT back to whatever was manually set -- manual edits are for reading
## off good numbers to hand-copy into those constants, not a persistent
## override).
func set_manual_override(enabled: bool) -> void:
	_manual_override = enabled

## Dev-only -- prints this node's own CURRENT local position/rotation
## (Vector3, radians -- paste directly into GRIP_HAND_ROTATION/
## HEAVY_ATTACK_GRIP_ROTATION) to the console, however they got there
## (manual override, or whatever _update_held_pose last set).
func print_current_pose() -> void:
	print(name, " position = ", position)
	print(name, " rotation (radians) = ", rotation)
	print(name, " rotation (degrees) = ", rotation * 180.0 / PI)

## Whether IT'S actually being looked at, not just "is the player nearby" --
## the InteractArea (see _on_interact_area_body_entered) only tracks
## proximity, matching player facing is checked separately here since the
## request was specifically "get nearby it AND look at it AND press E."
##
## Range is measured from the player's own BODY, not the camera -- same fix
## player.gd's own _punch() already needed for its aim raycast (see its own
## comment): the camera sits a full spring_length (4m) behind the player on
## its boom, so measuring range from the camera itself would need
## INTERACT_RANGE to exceed that boom length just to reach the player's own
## position, let alone anything actually in front of them. Direction still
## comes from the camera (that's genuinely where the player is aiming).
func _player_is_looking_at_me(player: CharacterBody3D) -> bool:
	var camera: Camera3D = player.get("camera")
	if camera == null:
		return false
	var origin: Vector3 = player.global_position + Vector3(0, 1.5, 0)
	var to_self := global_position - origin
	if to_self.length() > INTERACT_RANGE:
		return false
	var facing := -camera.global_transform.basis.z
	return facing.normalized().dot(to_self.normalized()) >= LOOK_DOT_THRESHOLD

## Reparents this whole prop onto the player's Character hand bone -- same
## "instant snap, no interpolation" reasoning the old bicycle.gd's own
## _mount() used (a pickup is expected to read as instant, not eased).
## Overridden by each subclass (greatsword.gd/martyr_blade.gd/runeblade.gd)
## with its own items/item_registry.gd lookup key -- same "subclass override
## METHOD, not const" reasoning wearable.gd's own get_wearable_slot()
## already documents (GDScript can't let a subclass redeclare a parent's
## const). Never instantiated directly itself, so the base return value is
## never actually used.
func get_item_id() -> String:
	return ""

## See pistol.gd's own _try_add_to_inventory() for the shared reasoning --
## picking this up now adds it to the inventory instead of equipping it
## directly; _equip() below is unchanged, just called from
## Inventory.equip_from_slot() on a freshly instantiated copy now.
func _try_add_to_inventory(player: CharacterBody3D) -> void:
	var inv: Node = player.get_node_or_null("Inventory")
	if inv == null or not inv.has_method("add_item"):
		return
	if inv.call("add_item", get_item_id(), 1):
		_interact_area.monitoring = false
		queue_free()

func _equip(player: CharacterBody3D) -> void:
	var character: Node = player.get_node_or_null("Character")
	if character == null or not character.has_method("get_hand_node"):
		return
	var hand: Node3D = character.get_hand_node()
	if hand == null:
		return
	# Replace whatever's already equipped, rather than stacking -- picking up
	# a second weapon while already carrying one used to leave both visibly
	# parented under the same hand attachment at once (a real reported bug:
	# "it lets you carry both sword at once"). The hand attachment only ever
	# holds an equipped weapon (nothing else gets parented there), so
	# freeing every existing child before attaching the new one is safe.
	for existing in hand.get_children():
		existing.queue_free()
	_picked_up = true
	_wielding_character = character
	_interact_area.monitoring = false
	get_parent().remove_child(self)
	hand.add_child(self)
	# Set once here so there's no one-frame pop showing at the wrong
	# transform before _process's own _update_held_pose catches up next
	# frame -- same values that call would compute anyway (not wielding
	# sword_heavy_attack yet, so the ordinary grip rotation).
	rotation = GRIP_HAND_ROTATION
	position = GRIP_HAND_POSITION
	# Swaps character.gd's own locomotion/combat clips over to the sword
	# moveset (see set_wielding_sword's own comment) -- everything else
	# (player.gd's punch/block/jump/crouch input handling) stays completely
	# unchanged, since character.gd's own methods internally redirect.
	if character.has_method("set_wielding_sword"):
		character.set_wielding_sword(true)

## has_method("respawn") duck-types "is this a real Player" -- npc/cpu.gd
## shares the same collision_layer (4) but has no respawn() of its own, so
## a hostile CPU wandering into range never gets treated as a would-be
## wielder (same filter the old bicycle.gd's own InteractArea used).
func _on_interact_area_body_entered(body: Node3D) -> void:
	if body is CharacterBody3D and body.has_method("respawn"):
		_nearby_player = body

func _on_interact_area_body_exited(body: Node3D) -> void:
	if body == _nearby_player:
		_nearby_player = null

func _flat_material(color: Color, metallic: float, roughness: float) -> StandardMaterial3D:
	var mat := StandardMaterial3D.new()
	mat.albedo_color = color
	mat.metallic = metallic
	mat.roughness = roughness
	# See city_chunk.gd's own build_facade_material comment -- same fix:
	# StandardMaterial3D's default dielectric specular response reads as a
	# visible sheen under a real sky/sun regardless of roughness otherwise.
	mat.metallic_specular = 0.0
	return mat

## A cylinder spanning two arbitrary world points, with independently
## settable end radii (unlike the old bicycle.gd's own _add_tube, which
## only ever needed a uniform radius) -- lets grip/guard/pommel pieces taper
## instead of reading as plain uniform rods.
func _add_tube(a: Vector3, b: Vector3, radius_a: float, radius_b: float, mat: Material) -> void:
	var mesh_inst := MeshInstance3D.new()
	var cm := CylinderMesh.new()
	cm.top_radius = radius_b
	cm.bottom_radius = radius_a
	var length: float = a.distance_to(b)
	cm.height = maxf(length, 0.001)
	mesh_inst.mesh = cm
	mesh_inst.material_override = mat
	var mid := (a + b) / 2.0
	var y_axis: Vector3 = (b - a).normalized() if length > 0.001 else Vector3.UP
	var helper := Vector3.FORWARD if absf(y_axis.dot(Vector3.FORWARD)) < 0.95 else Vector3.RIGHT
	var x_axis := y_axis.cross(helper).normalized()
	var z_axis := x_axis.cross(y_axis).normalized()
	mesh_inst.transform = Transform3D(Basis(x_axis, y_axis, z_axis), mid)
	add_child(mesh_inst)

## Flat-shaded triangle (one normal for all 3 vertices, computed fresh per
## triangle rather than smoothed/shared) -- matches this project's existing
## low-poly stylized look (see the old bicycle.gd's own class comment) and
## is simpler than vertex-normal averaging for a faceted blade like this.
func _tri(st: SurfaceTool, a: Vector3, b: Vector3, c: Vector3) -> void:
	var n := (b - a).cross(c - a).normalized()
	st.set_normal(n)
	st.add_vertex(a)
	st.set_normal(n)
	st.add_vertex(b)
	st.set_normal(n)
	st.add_vertex(c)

## Piecewise-linear lookup into a (t, edge_x, width, thickness) profile
## array at parameter t (0=guard, 1=tip) -- a small hand-tunable keyframe
## table, same "explicit data over dense closed-form math" style this
## project already favors elsewhere (e.g. character.gd's own
## DIRECTIONAL_ANIM/STANCE_* tables). Takes the profile as a parameter
## (not a module-level const) so every subclass's own differently-shaped
## blade can reuse this same lookup -- see this file's own class comment.
func _blade_profile_at(profile: Array, t: float) -> Dictionary:
	var lo: Array = profile[0]
	var hi: Array = profile[profile.size() - 1]
	for i in range(profile.size() - 1):
		if t >= profile[i][0] and t <= profile[i + 1][0]:
			lo = profile[i]
			hi = profile[i + 1]
			break
	var span: float = hi[0] - lo[0]
	var f: float = (t - lo[0]) / span if span > 0.0001 else 0.0
	return {
		"edge_x": lerpf(lo[1], hi[1], f),
		"width": lerpf(lo[2], hi[2], f),
		"thickness": lerpf(lo[3], hi[3], f),
	}

## One blade cross-section at parameter t: a thin edge point plus two spine
## points (front/back, straddling the blade's own thickness) -- see
## _build_blade's own comment on how these three points per slice
## triangulate into a proper wedge-profile blade. edge_x is the
## cross-section's own CENTERLINE offset (what drives the blade's curve
## toward the tip), not the edge's own position -- edge and spine sit
## symmetrically half a width to either side of it, so the blade's base
## (t=0) straddles local X=0 the same way the guard/grip/pommel below are
## built symmetric about X=0 (a real reported bug when this instead put the
## edge AT edge_x with the full width trailing off to one side, visibly
## off-center from the hilt).
func _blade_slice(profile: Array, blade_length: float, t: float) -> Dictionary:
	var p := _blade_profile_at(profile, t)
	var y: float = t * blade_length
	var center_x: float = p["edge_x"]
	var half_width: float = p["width"] / 2.0
	return {
		"edge": Vector3(center_x - half_width, y, 0.0),
		"front": Vector3(center_x + half_width, y, p["thickness"] / 2.0),
		"back": Vector3(center_x + half_width, y, -p["thickness"] / 2.0),
	}

## Builds the blade as one continuous mesh: each cross-section has 3 points
## (edge/front/back, see _blade_slice), and consecutive slices connect into
## 3 ribbons -- front bevel (edge to front), back bevel (edge to back), and
## the spine (front to back, the blade's own thin flat backbone) -- giving
## a real wedge-profile blade (thin cutting edge, thicker spine) rather than
## a flat, zero-thickness cutout. Slices converge to a single point at the
## tip (width/thickness both hit 0 there, by convention every profile so
## far uses), so no special tip-capping is needed -- only the base (where it
## meets the guard) gets an explicit one-triangle cap. Returns the computed
## slices array so callers can reuse it for their own decorative elements
## (a spine inlay, fuller grooves, rune lines, ...) without recomputing.
func _build_blade(profile: Array, blade_length: float, slice_count: int, mat: Material) -> Array:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)

	var slices: Array = []
	for i in range(slice_count + 1):
		slices.append(_blade_slice(profile, blade_length, float(i) / float(slice_count)))

	_tri(st, slices[0]["edge"], slices[0]["back"], slices[0]["front"])

	for i in range(slice_count):
		var s0: Dictionary = slices[i]
		var s1: Dictionary = slices[i + 1]
		_tri(st, s0["edge"], s0["front"], s1["front"])
		_tri(st, s0["edge"], s1["front"], s1["edge"])
		_tri(st, s0["edge"], s1["back"], s0["back"])
		_tri(st, s0["edge"], s1["edge"], s1["back"])
		_tri(st, s0["front"], s0["back"], s1["back"])
		_tri(st, s0["front"], s1["back"], s1["front"])

	var mesh_inst := MeshInstance3D.new()
	mesh_inst.mesh = st.commit()
	mesh_inst.material_override = mat
	add_child(mesh_inst)

	return slices
