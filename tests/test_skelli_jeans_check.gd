extends Node3D
## Regression check for wearables/skelli_jeans.gd and the asset
## tools/meshwork/build_skelli_jeans.py bakes for it.
##
## THE ASSERTION THAT MATTERS MOST IS THAT THE BODY IS GONE. The source was
## a whole belted figure -- head, torso, arms, legs -- and the build script
## keeps only what isn't pure white. If that cut ever regresses, what ships
## is a pair of jeans with a mannequin welded inside them, which loads
## without a single error and only shows up by looking. So the geometry is
## measured directly: nothing above the waistband, nothing below the cuffs,
## and a footprint no wider than a pair of legs.
##
## The rest guards the wiring that fails SILENTLY:
##
##  - Bind convention. This project's Skeleton3D has IDENTITY bone rests, so
##    a skin built from get_bone_global_rest() looks perfectly reasonable and
##    deforms into garbage. The binds are compared against fused_body.gd's
##    own build_skin() instead, which is the real convention.
##  - The `skeleton` NodePath. Empty by default on a code-built
##    MeshInstance3D, which means the jeans hang in their rest pose while the
##    character walks out of them -- no error anywhere.
##  - The rig constants the build script keeps its own copy of. They're
##    duplicated in a Python file this project's tests can't import, so the
##    only thing keeping them honest is comparing the baked result against
##    bone_origins() here.

const JeansScene := preload("res://wearables/skelli_jeans.tscn")
const JeansModel := preload("res://wearables/assets/skelli_jeans.glb")
const CharacterScene := preload("res://character/character.tscn")
const PlayerScene := preload("res://player/player.tscn")
const ItemRegistry = preload("res://items/item_registry.gd")
const FusedBody = preload("res://character/fused_body.gd")

# character/fused_body.gd's own rest geometry, which the jeans were baked
# against. HIP_Y is where the waistband was placed; FOOT_Y is the ankle.
const HIP_Y := 0.9
const FOOT_Y := 0.135
const LEG_R := 0.135
const HIP_X := 0.17

var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	call_deferred("_run_checks")

func _run_checks() -> void:
	await _check_the_body_is_gone()
	await _check_it_is_skinned_to_this_rig()
	await _check_it_deforms_on_a_real_character()
	await _check_it_is_a_registered_legs_item()
	await _check_it_does_not_hide_the_body()

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

## --- 1. Only jeans came through the cut. ---
func _check_the_body_is_gone() -> void:
	var inst := JeansModel.instantiate()
	add_child(inst)
	await get_tree().process_frame

	var mesh_inst := _find_mesh(inst)
	if mesh_inst == null or mesh_inst.mesh == null:
		_fail("the baked glb has no mesh at all")
		inst.queue_free()
		return

	var aabb: AABB = mesh_inst.mesh.get_aabb()
	var top: float = aabb.position.y + aabb.size.y
	var bottom: float = aabb.position.y

	# A whole figure would reach the 2.6m crown; jeans stop at the waist.
	if top > HIP_Y + 0.12:
		_fail("the mesh reaches %.2fm -- that's a torso, not a waistband (HIP_Y is %.2f)" % [top, HIP_Y])
	elif top < HIP_Y - 0.25:
		_fail("the mesh only reaches %.2fm -- the waistband is missing" % top)
	else:
		print("PASS: the mesh stops at the waist (%.2fm, HIP_Y is %.2f) -- no torso came through" % [top, HIP_Y])

	# And nothing below the ankle: no feet, no shoes, no floor.
	if bottom < FOOT_Y - 0.08:
		_fail("the mesh reaches down to %.2fm, below the ankle at %.2f -- feet came through the cut" % [bottom, FOOT_Y])
	else:
		print("PASS: the hem stops at the ankle (%.2fm) -- no feet came through" % bottom)

	# Width: two legs and the denim around them, not a figure with arms out.
	var half_width: float = maxf(absf(aabb.position.x), absf(aabb.position.x + aabb.size.x))
	if half_width > HIP_X + LEG_R + 0.25:
		_fail("the mesh is %.2fm to the side -- wide enough to still have arms on it" % half_width)
	else:
		print("PASS: the footprint is leg-width (%.2fm out), not a whole figure" % half_width)

	inst.queue_free()
	await get_tree().process_frame

## --- 2. Skinned to THIS project's bones, with THIS project's bind
## convention -- see the class comment on why the convention matters. ---
func _check_it_is_skinned_to_this_rig() -> void:
	var inst := JeansModel.instantiate()
	add_child(inst)
	await get_tree().process_frame

	var mesh_inst := _find_mesh(inst)
	var skin: Skin = mesh_inst.skin if mesh_inst != null else null
	if skin == null or skin.get_bind_count() == 0:
		_fail("the baked glb carries no skin -- the jeans would be a rigid prop")
		inst.queue_free()
		return

	var origins: Dictionary = FusedBody.bone_origins()
	var unknown: Array[String] = []
	for i in range(skin.get_bind_count()):
		var name := skin.get_bind_name(i)
		if not origins.has(name):
			unknown.append(name)
	if not unknown.is_empty():
		_fail("the skin names bones this rig doesn't have: %s" % ", ".join(unknown))
	else:
		print("PASS: all %d skin binds name real bones on this rig" % skin.get_bind_count())

	# The bones that actually carry trousers have to be among them.
	var named := {}
	for i in range(skin.get_bind_count()):
		named[skin.get_bind_name(i)] = true
	var required := ["hips", "thigh_l", "thigh_r", "shin_l", "shin_r"]
	var missing: Array[String] = []
	for bone in required:
		if not named.has(bone):
			missing.append(bone)
	if not missing.is_empty():
		_fail("nothing is weighted to %s -- the jeans wouldn't follow the legs" % ", ".join(missing))
	else:
		print("PASS: the jeans are weighted to the hips and both legs")

	inst.queue_free()
	await get_tree().process_frame

## --- 3. Worn on a real character: bound with the body's own convention,
## and actually pointed at the skeleton. ---
func _check_it_deforms_on_a_real_character() -> void:
	var character: Node3D = CharacterScene.instantiate()
	character.set_paintable(false)
	add_child(character)
	await get_tree().process_frame

	var jeans: Node3D = JeansScene.instantiate()
	if not jeans.wear_on(character):
		_fail("wear_on() refused a real character")
		jeans.free()
		character.queue_free()
		return

	var worn: MeshInstance3D = character._worn_legs_parts[0] if not character._worn_legs_parts.is_empty() else null
	if worn == null:
		_fail("nothing was recorded in the legs slot")
		jeans.free()
		character.queue_free()
		return

	if not (worn.get_parent() is Skeleton3D):
		_fail("the jeans hang off a %s, not the Skeleton3D" % worn.get_parent().get_class())
	elif worn.skeleton.is_empty():
		_fail("the jeans' skeleton path is empty -- they'd stay in their rest pose while the character moves")
	else:
		print("PASS: the jeans hang on the character's own Skeleton3D and are pointed at it")

	# THE bind check: compared against the convention fused_body.gd actually
	# uses, not against the skeleton's own (identity, therefore meaningless)
	# rests. Getting this wrong still loads, and still animates -- wrongly.
	var origins: Dictionary = FusedBody.bone_origins()
	var wrong: Array[String] = []
	var identity_binds := 0
	for i in range(worn.skin.get_bind_count()):
		var name := worn.skin.get_bind_name(i)
		if not origins.has(name):
			continue
		var expected := Transform3D(Basis(), -(origins[name] as Vector3))
		if not worn.skin.get_bind_pose(i).is_equal_approx(expected):
			wrong.append(name)
		if worn.skin.get_bind_pose(i).is_equal_approx(Transform3D.IDENTITY):
			identity_binds += 1
	if not wrong.is_empty():
		_fail("these binds don't match fused_body.gd's own build_skin(): %s" % ", ".join(wrong))
	elif identity_binds > 0:
		_fail("%d binds are identity -- the rebind didn't happen" % identity_binds)
	else:
		print("PASS: every bind matches the body's own build_skin() convention")

	jeans.free()
	character.queue_free()
	await get_tree().process_frame

## --- 4. It's a real item called Skelli Jeans, in the legs slot. ---
func _check_it_is_a_registered_legs_item() -> void:
	var item: Dictionary = ItemRegistry.ITEMS.get("skelli_jeans", {})
	if item.is_empty():
		_fail("skelli_jeans isn't in the item registry")
	elif str(item.get("display_name", "")) != "Skelli Jeans":
		_fail("the item is called '%s'" % str(item.get("display_name", "")))
	elif str(item.get("equip_slot", "")) != "legs":
		_fail("the item equips to '%s', not the legs slot" % str(item.get("equip_slot", "")))
	elif not ItemRegistry.fits_slot("skelli_jeans", "legs"):
		_fail("the registry won't let Skelli Jeans drop onto the legs paper-doll slot")
	elif ItemRegistry.fits_slot("skelli_jeans", "top"):
		_fail("Skelli Jeans can be dropped onto the TOP slot -- they'd fight the cloak for it")
	else:
		print("PASS: 'Skelli Jeans' is registered to the legs slot and only that slot")
	await get_tree().process_frame

## --- 5. Trousers must NOT hide the body the way the cloak does. That's a
## real distinction with a real failure mode: reusing the cloak's stub-body
## call here would leave the wearer with no torso, arms or head. ---
func _check_it_does_not_hide_the_body() -> void:
	var character: Node3D = CharacterScene.instantiate()
	character.set_paintable(false)
	add_child(character)
	await get_tree().process_frame

	var jeans: Node3D = JeansScene.instantiate()
	jeans.wear_on(character)
	await get_tree().process_frame

	if character.is_body_stubs_only():
		_fail("putting trousers on stripped the body to stubs -- that's the cloak's behaviour, not trousers'")
	else:
		print("PASS: the body stays visible under the jeans, so a top can be worn too")

	# And taking them off removes them without touching anything else.
	character.clear_worn_legs()
	await get_tree().process_frame
	if not character._worn_legs_parts.is_empty():
		_fail("clear_worn_legs() left parts behind")
	else:
		print("PASS: clear_worn_legs() takes them off cleanly")

	jeans.free()
	character.queue_free()
	await get_tree().process_frame

func _find_mesh(node: Node) -> MeshInstance3D:
	if node is MeshInstance3D:
		return node as MeshInstance3D
	for child in node.get_children():
		var found := _find_mesh(child)
		if found != null:
			return found
	return null
