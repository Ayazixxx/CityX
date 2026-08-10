extends "res://wearables/clothing.gd"
## Skelli Jeans -- belted black jeans, and the first thing in this project to
## occupy the LEGS slot (player/inventory.gd has had the slot since it was
## written; nothing had ever filled it).
##
## Real skinned geometry, the same treatment wearables/hooded_cloak.gd gets
## and for the same reason: trousers span hips to ankles across the hip and
## knee joints, so clothing.gd's own coarse per-bone rigid attachment would
## tear them open the moment the character took a step.
##
## The source is a Meshy generation of a whole belted figure.
## tools/meshwork/build_skelli_jeans.py separates the jeans from it -- see
## that file for the method, which is much simpler than the cloak's because
## the figure underneath is an untextured mannequin: the body is EXACTLY
## pure white and the garment is everything that isn't, so the cut follows
## the real garment boundary with no threshold to guess at.
##
## Unlike the cloak, this does NOT swap the body for stubs. A cloak covers
## the whole body and can't stop a limb poking through it, so it removes the
## body instead of fighting it. Trousers only have to clear the legs, and
## the build script already pushes them outside this rig's own leg radius
## (its CLEARANCE pass) -- so the body stays, and a shirt can be worn at the
## same time.
##
## KEEPS ITS OWN TEXTURE, unlike every other garment here. The cloak and the
## hockey mask are flat-coloured because their sources had nothing usable to
## keep; these have the denim weave and the studded skull belt, which exist
## only in the texture -- flat-shading them turns the whole garment into a
## black blob. The build script downscales the source's 4K sheet to 1024
## (it was mostly covering the body that got thrown away) and marks the
## material double-sided there, so nothing here has to override it.

const JeansModel := preload("res://wearables/assets/skelli_jeans.glb")

## Raises (or lowers) the jeans on the wearer, in metres. The build script
## already lifts them so the waistband sits level with this rig's own hip
## joint rather than at the sagged, low-slung height they were modelled at
## -- so 0.0 is NOT "as the model came", it's "waistband at the hip".
##
## This is the knob for taste from there, and it needs no re-bake: it moves
## the worn mesh only. Positive is higher.
const WAIST_LIFT := 0.0

## Lying on the ground pre-pickup, shown tipped and shrunk so a person-sized
## garment reads as a dropped item rather than as half an invisible person.
const GROUND_SCALE := 0.5
const GROUND_PITCH_DEGREES := -70.0
const GROUND_LIFT := 0.12

func get_wearable_slot() -> String:
	return "legs"

func get_item_id() -> String:
	return "skelli_jeans"

func _build_visual() -> void:
	var preview := JeansModel.instantiate()
	preview.scale = Vector3.ONE * GROUND_SCALE
	preview.rotation.x = deg_to_rad(GROUND_PITCH_DEGREES)
	preview.position.y = GROUND_LIFT
	add_child(preview)

## Pickup bookkeeping only -- all the real work is in wear_on() below, the
## same split wearables/hooded_cloak.gd uses so an NPC can be authored
## already wearing something without a ground item existing.
func _equip(player: CharacterBody3D) -> void:
	var character: Node = player.get_node_or_null("Character")
	if character == null or not character.has_method("get_bone_node"):
		return
	if not wear_on(character):
		return
	_picked_up = true
	_interact_area.monitoring = false
	queue_free()

## Puts the jeans on a character, with no pickup and no inventory involved.
##
## The mesh is already authored in this rig's own rest pose (the build
## script bakes that in), so hanging it on a real character is just a Skin
## whose bind for each bone matches the one the body's own fused mesh uses
## -- see _rebind. At rest the pose and the bind cancel and the jeans sit
## exactly where they were authored.
func wear_on(character: Node) -> bool:
	var skeleton := _character_skeleton(character)
	if skeleton == null:
		push_error("skelli_jeans: character has no Skeleton3D to bind to")
		return false

	var source := JeansModel.instantiate()
	var source_mesh := _find_mesh_instance(source)
	if source_mesh == null:
		push_error("skelli_jeans: glb has no MeshInstance3D")
		source.queue_free()
		return false

	if character.has_method("clear_worn_legs"):
		character.clear_worn_legs()

	var worn := MeshInstance3D.new()
	# No material_override: the mesh carries the imported denim/belt
	# material, which is the whole reason this garment is textured at all
	# (see this file's own class comment).
	worn.mesh = source_mesh.mesh
	worn.skin = _rebind(source_mesh.skin)
	# A MeshInstance3D deforms against the skeleton named by its own
	# `skeleton` path, and on a node built in code that path starts EMPTY --
	# not the ".." the editor shows for scene-placed ones. An empty path
	# means no skeleton at all, and the jeans would hang in their rest pose
	# while the character walked out of them. Parenting alone isn't enough.
	skeleton.add_child(worn)
	worn.skeleton = NodePath("..")
	worn.transform = Transform3D.IDENTITY
	# Applied to the WORN NODE, not baked into the mesh, so WAIST_LIFT can
	# be retuned without re-running the build script.
	worn.position.y = WAIST_LIFT
	source.queue_free()

	if character.has_method("set_worn_legs"):
		var parts: Array[Node3D] = [worn]
		character.set_worn_legs(parts)
	return true

## The character's Skeleton3D, reached through the same public accessor
## every other wearable uses -- get_bone_node() hands back a
## BoneAttachment3D, which is by definition a child of its own skeleton.
func _character_skeleton(character: Node) -> Skeleton3D:
	var attachment: Node3D = character.call("get_bone_node", "hips")
	if attachment == null:
		return null
	return attachment.get_parent() as Skeleton3D

## Rebuilds the imported skin against the character's own rig.
##
## The bind is NOT the inverse of Skeleton3D.get_bone_global_rest(). This
## project's skeleton is built by fused_body.gd's own build_skeleton(),
## which calls add_bone() and never set_bone_rest() -- so every bone's rest
## is IDENTITY and asking the skeleton for one tells you nothing. What
## actually defines the bind space is fused_body.gd's own build_skin(): a
## pure translation of -bone_origins()[bone]. The body's fused mesh is bound
## that way, and the jeans have to agree with it exactly or the two deform
## differently on the same skeleton.
##
## Bind ORDER is load-bearing: the mesh's own ARRAY_BONES indices point at
## bind slots, not at bone names, so every source bind must produce exactly
## one bind here, in the same order, even if a name fails to resolve.
func _rebind(source_skin: Skin) -> Skin:
	var origins: Dictionary = FusedBody.bone_origins()
	var skin := Skin.new()
	for i in range(source_skin.get_bind_count()):
		var bone_name := source_skin.get_bind_name(i)
		if not origins.has(bone_name):
			push_error("skelli_jeans: no bone '%s' on the character" % bone_name)
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
