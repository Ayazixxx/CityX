extends "res://wearables/clothing.gd"
## The hooded cloak -- a full-length hooded robe, and the first piece of
## clothing in this project that is REAL SKINNED GEOMETRY rather than either
## a hand-assembled set of tubes (wearables/hoodie.gd, wearables/t_shirt.gd)
## or a rigid prop bolted to one bone (wearables/hockey_mask.gd).
##
## That distinction is the whole point of this file. Everything else in the
## "top" slot is attached with clothing.gd's own _add_piece(), i.e. COARSE
## PER-BONE RIGID attachment -- a sleeve is a stiff tube parented to one
## bone, and clothing.gd's own class comment already admits the visible seam
## that leaves at the shoulder mid-swing. A full-length cloak makes that
## unwearable: it spans hips to head in one continuous drape, so any rigid
## split through it tears open the moment the character moves.
##
## The source is a Meshy generation of a whole hooded figure, RIGGED to a
## Mixamo-style biped. tools/meshwork/build_cloak.py strips the head, hands
## and feet off it (see that file for what each cut keys off and why -- the
## figure is one welded 73k-vert shell, so none of it was as simple as
## dropping loose parts), collapses the source rig's weights onto THIS
## project's own 14 bone names, bakes the mesh into the character's own rest
## pose at its real 2.6m height, and exports assets/hooded_cloak.glb with a
## matching armature so glTF carries the skin weights through.
##
## Equipping therefore does NOT go through clothing.gd's own _equip(): there
## are no rigid pieces to re-parent. Instead the imported mesh is re-bound
## onto the character's OWN Skeleton3D with a fresh Skin, so the cloak
## deforms with every bone the body itself uses -- one mesh, no seams.
##
## No material of its own in the source (no usable UVs -- see the build
## script), so it gets a flat colour here, the same way
## wearables/hockey_mask.gd treats its own imported glb.

const CloakModel := preload("res://wearables/assets/hooded_cloak.glb")

const CLOAK_COLOR := Color(0.30, 0.26, 0.22)

## How the arms sit WHILE THIS IS WORN -- a rotation about each shoulder, in
## degrees: x swings them forward, y twists, z spreads them out. Tune it live
## with the dev console's own "/arms <forward> <twist> <spread>", then paste
## the triple here.
##
## 15 degrees of SPREAD, arrived at by tuning it live and confirmed as the
## value that looks right -- so it's applied automatically on equip now
## rather than being typed into the console every session.
##
## Spread only: forward and twist stay at zero. And it's a POSE, not the
## rig -- character.gd's own set_worn_arm_pose() folds it into the BODY's
## skin alone, so the cloak doesn't swing out with the arms (which would
## defeat the point), and the hands don't move (which would leave a held
## sword floating outside the fist). clear_worn_top() puts it back to zero,
## so taking the cloak off restores the normal stance.
const SLEEVE_ARM_ROTATION_DEGREES := Vector3(0.0, 0.0, 15.0)
## Lying on the ground pre-pickup, the garment is shown in its rest pose --
## which is a person-height standing cloak. Tipping and shrinking it reads as
## a dropped item instead of an invisible person standing there.
const GROUND_SCALE := 0.45
const GROUND_PITCH_DEGREES := -78.0
const GROUND_LIFT := 0.15

func get_wearable_slot() -> String:
	return "top"

func get_item_id() -> String:
	return "hooded_cloak"

func _build_visual() -> void:
	var preview := CloakModel.instantiate()
	_tint(preview)
	preview.scale = Vector3.ONE * GROUND_SCALE
	preview.rotation.x = deg_to_rad(GROUND_PITCH_DEGREES)
	preview.position.y = GROUND_LIFT
	add_child(preview)

## Overrides clothing.gd's own piece-by-piece re-parenting wholesale -- see
## this file's own class comment. All the actual dressing is in wear_on()
## below; what's left here is the pickup bookkeeping that only applies when
## the cloak came off the ground or out of an inventory.
func _equip(player: CharacterBody3D) -> void:
	var character: Node = player.get_node_or_null("Character")
	if character == null or not character.has_method("get_bone_node"):
		return
	if not wear_on(character):
		return
	_picked_up = true
	_interact_area.monitoring = false
	queue_free()

## Puts this cloak onto a character, with no pickup, no inventory and no
## ground item anywhere in it. _equip() above is the PLAYER'S route in and is
## now nothing but the pickup bookkeeping around this call; an NPC authored
## already wearing one comes in here directly (npc/unique_npc.gd's own
## `worn_top` export), which instantiates this scene purely to reach this
## method and frees it again immediately.
##
## Deliberately touches NOTHING on self -- not _picked_up, not the @onready
## _interact_area, nothing that _ready() sets up -- precisely so it is safe
## to call on an instance that was never added to the tree and therefore
## never ran _ready() at all. Returns false if the character couldn't wear
## it, so the caller knows not to treat it as consumed.
##
## The mesh's vertices are already authored in the character's rest pose
## (build_cloak.py bakes that in), so the ONLY thing needed to hang it on a
## real character is a Skin whose bind for each bone matches the one the
## body's own fused mesh uses (see _rebind below). At rest every bone's pose
## equals its rest, the two cancel, and the cloak sits exactly where it was
## authored.
func wear_on(character: Node) -> bool:
	var skeleton := _character_skeleton(character)
	if skeleton == null:
		push_error("hooded_cloak: character has no Skeleton3D to bind to")
		return false

	var source := CloakModel.instantiate()
	var source_mesh := _find_mesh_instance(source)
	if source_mesh == null:
		push_error("hooded_cloak: glb has no MeshInstance3D")
		source.queue_free()
		return false

	if character.has_method("clear_worn_top"):
		character.clear_worn_top()

	var worn := MeshInstance3D.new()
	worn.mesh = source_mesh.mesh
	worn.skin = _rebind(source_mesh.skin, skeleton)
	var mat := StandardMaterial3D.new()
	mat.albedo_color = CLOAK_COLOR
	mat.roughness = 0.95
	mat.metallic = 0.0
	# An open hood and open sleeves mean the inside faces the camera whenever
	# you can see into them; single-sided they would read as holes.
	mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	worn.material_override = mat
	# A MeshInstance3D deforms against the skeleton named by its own
	# `skeleton` path. On a node built in code that path starts EMPTY (not
	# the ".." the editor shows for scene-placed ones), and an empty path
	# means no skeleton at all -- the cloak would hang in its rest pose and
	# never move. Parenting it to the Skeleton3D is not enough on its own.
	skeleton.add_child(worn)
	worn.skeleton = NodePath("..")
	worn.transform = Transform3D.IDENTITY
	source.queue_free()

	if character.has_method("set_worn_top"):
		var parts: Array[Node3D] = [worn]
		character.set_worn_top(parts)
	# This garment covers the whole body, so the body underneath comes off:
	# rigid cloth over a mesh that moves on its own will always let a limb
	# through somewhere, and no amount of fitting fixes that. What's left is
	# hands, feet and a collar -- the parts that show through the cloak
	# anyway. character.gd's own clear_worn_top() puts the body back.
	if character.has_method("set_body_stubs_only"):
		character.set_body_stubs_only(true)
	# Holds the arms inside the sleeves while this is worn. A POSE, not the
	# rig -- it applies only to whoever is wearing the cloak and needs no
	# body re-bake, unlike fused_body.gd's own SHOULDER_X. Tune it live with
	# the dev console's own "/arms <forward> <twist> <spread>" and paste the numbers
	# you land on into SLEEVE_ARM_ROTATION_DEGREES at the top of this file.
	if character.has_method("set_worn_arm_pose"):
		character.set_worn_arm_pose(SLEEVE_ARM_ROTATION_DEGREES)
	return true

## The character's Skeleton3D, reached through the same public accessor every
## other wearable already uses -- get_bone_node() hands back a
## BoneAttachment3D, and a BoneAttachment3D is by definition a child of the
## skeleton it is bound to (see character.gd's own _head_pivot comment).
func _character_skeleton(character: Node) -> Skeleton3D:
	var attachment: Node3D = character.call("get_bone_node", "spine")
	if attachment == null:
		return null
	return attachment.get_parent() as Skeleton3D

## Rebuilds the imported skin against the character's own rig.
##
## The bind is NOT the inverse of Skeleton3D.get_bone_global_rest(). This
## project's skeleton is built by fused_body.gd's own build_skeleton(), which
## calls add_bone() and never set_bone_rest() -- so every bone's rest is
## IDENTITY, and asking the skeleton for a rest tells you nothing. What
## actually defines the bind space is fused_body.gd's own build_skin():
## a pure translation of -bone_origins()[bone], no rotation, which moves a
## vertex into the frame of the joint that bone pivots around. The body's own
## fused mesh is bound that way, and the cloak has to agree with it exactly
## or the two deform differently on the same skeleton.
##
## Bind ORDER is load-bearing: the mesh's own ARRAY_BONES indices point at
## bind slots, not at bone names, so every source bind must produce exactly
## one bind here, in the same order, even if a name fails to resolve.
func _rebind(source_skin: Skin, _skeleton: Skeleton3D) -> Skin:
	var origins: Dictionary = FusedBody.bone_origins()
	var skin := Skin.new()
	for i in range(source_skin.get_bind_count()):
		var bone_name := source_skin.get_bind_name(i)
		if not origins.has(bone_name):
			push_error("hooded_cloak: no bone '%s' on the character" % bone_name)
			skin.add_named_bind(bone_name, source_skin.get_bind_pose(i))
			continue
		var origin: Vector3 = origins[bone_name]
		skin.add_named_bind(bone_name, Transform3D(Basis(), -origin))
	return skin

func _find_mesh_instance(node: Node) -> MeshInstance3D:
	if node is MeshInstance3D:
		return node as MeshInstance3D
	for child in node.get_children():
		var found := _find_mesh_instance(child)
		if found != null:
			return found
	return null

func _tint(node: Node) -> void:
	if node is MeshInstance3D:
		var mat := StandardMaterial3D.new()
		mat.albedo_color = CLOAK_COLOR
		mat.roughness = 0.95
		mat.cull_mode = BaseMaterial3D.CULL_DISABLED
		(node as MeshInstance3D).material_override = mat
	for child in node.get_children():
		_tint(child)
