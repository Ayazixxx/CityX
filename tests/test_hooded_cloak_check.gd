extends Node3D
## Regression check for wearables/hooded_cloak.gd -- the one "top" that is
## real SKINNED geometry instead of clothing.gd's own rigid per-bone pieces
## (see hooded_cloak.gd's own class comment for why a full-length drape can't
## use the rigid path). Everything here is about the three things that make
## the skinned path work and that nothing else in the project exercises:
##
##   1. the baked glb's own skeleton IS this project's skeleton -- same 14
##      bone names, same rest positions as character/fused_body.gd's own
##      bone_origins(), because the mesh's vertices are authored in that
##      exact pose by tools/meshwork/build_cloak.py;
##   2. the mesh actually carries bone/weight arrays through glTF (drop the
##      armature from the export and it silently becomes a rigid prop);
##   3. equipping re-binds it onto the CHARACTER's skeleton -- one mesh
##      parented to the real Skeleton3D, with every bind pose the inverse of
##      that bone's own global rest, which is what makes it land exactly
##      where it was authored and then deform with the body.
##
## Runs headless: real Player+Character scenes, no rendering needed.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")
const CloakScene := preload("res://wearables/hooded_cloak.tscn")
const CloakScript = preload("res://wearables/hooded_cloak.gd")
const CloakModel := preload("res://wearables/assets/hooded_cloak.glb")
const FusedBody = preload("res://character/fused_body.gd")

const CHARACTER_HEIGHT := 2.6

var _player: CharacterBody3D
var _character: Node
var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	_player = PlayerScene.instantiate()
	add_child(_player)
	_character = CharacterScene.instantiate()
	_character.set_paintable(false)
	_player.add_child(_character)
	call_deferred("_run_checks")

func _find_mesh(node: Node) -> MeshInstance3D:
	if node is MeshInstance3D:
		return node as MeshInstance3D
	for c in node.get_children():
		var f := _find_mesh(c)
		if f != null:
			return f
	return null

func _find_skeleton(node: Node) -> Skeleton3D:
	if node is Skeleton3D:
		return node as Skeleton3D
	for c in node.get_children():
		var f := _find_skeleton(c)
		if f != null:
			return f
	return null

func _run_checks() -> void:
	# --- 1. the baked asset's rig is this project's rig ---
	var model := CloakModel.instantiate()
	var model_skeleton := _find_skeleton(model)
	if model_skeleton == null:
		_fail("hooded_cloak.glb has no Skeleton3D -- the export lost its armature")
		_finish()
		return
	var origins: Dictionary = FusedBody.bone_origins()
	var missing: Array[String] = []
	var misplaced: Array[String] = []
	for i in range(model_skeleton.get_bone_count()):
		var bone_name := model_skeleton.get_bone_name(i)
		if not origins.has(bone_name):
			missing.append(bone_name)
			continue
		var want: Vector3 = origins[bone_name]
		var got: Vector3 = model_skeleton.get_bone_global_rest(i).origin
		if got.distance_to(want) > 0.002:
			misplaced.append("%s want=%s got=%s" % [bone_name, want, got])
	if missing.is_empty():
		print("PASS: all %d cloak bones exist in fused_body.gd's own bone_origins()" % model_skeleton.get_bone_count())
	else:
		_fail("cloak has bones this project's rig doesn't: %s" % [missing])
	if misplaced.is_empty():
		print("PASS: every cloak bone rest matches bone_origins() to within 2mm")
	else:
		_fail("cloak bone rests don't match bone_origins(): %s" % [misplaced])

	# --- 2. the skin weights survived the glTF round trip ---
	var model_mesh := _find_mesh(model)
	if model_mesh == null or model_mesh.mesh == null:
		_fail("hooded_cloak.glb has no MeshInstance3D")
		_finish()
		return
	var arrays: Array = model_mesh.mesh.surface_get_arrays(0)
	var bones_arr = arrays[Mesh.ARRAY_BONES]
	var weights_arr = arrays[Mesh.ARRAY_WEIGHTS]
	if bones_arr != null and weights_arr != null and bones_arr.size() > 0:
		print("PASS: cloak mesh carries bone+weight arrays (%d verts)"
			% (arrays[Mesh.ARRAY_VERTEX] as PackedVector3Array).size())
	else:
		_fail("cloak mesh has no bone/weight arrays -- it would equip as a rigid prop")
	if model_mesh.skin != null and model_mesh.skin.get_bind_count() == model_skeleton.get_bone_count():
		print("PASS: cloak skin has one bind per bone (%d)" % model_mesh.skin.get_bind_count())
	else:
		_fail("cloak skin bind count doesn't match its bone count")

	# The garment is authored at the character's real height, not the source
	# figure's -- getting this wrong is a silently wearable but wrong-sized
	# cloak, so it's worth pinning.
	# The hood has to sit ABOVE the character's crown, not level with it --
	# build_cloak.py pushes the whole garment out by a fixed clearance
	# because this project's character is much chunkier for its height than
	# the source figure, and without that the head swallows the hood whole
	# and it reads as a collar. Too high and it's floating instead of worn.
	var aabb := model_mesh.mesh.get_aabb()
	var top := aabb.position.y + aabb.size.y
	if top > CHARACTER_HEIGHT + 0.04 and top < CHARACTER_HEIGHT + 0.30:
		print("PASS: hood clears the character's %.1fm crown by %.3f"
			% [CHARACTER_HEIGHT, top - CHARACTER_HEIGHT])
	else:
		_fail("cloak top is %.3f; wanted just above the %.1fm crown" % [top, CHARACTER_HEIGHT])
	if aabb.position.y > -0.05 and aabb.position.y < 0.35:
		print("PASS: cloak hem sits near the floor (y=%.3f)" % aabb.position.y)
	else:
		_fail("cloak hem is at y=%.3f, not near the floor" % aabb.position.y)

	# WHICH WAY IT FACES. This shipped backwards once: the hood's opening
	# ended up on the wearer's back. The front is +Z -- ui/hud.gd parks the
	# portrait camera at `head_pos + basis.z * distance` and has to be
	# looking at the face, which is the only unambiguous statement of the
	# convention in the project (don't infer it from wearables/hoodie.gd's
	# hood sitting at +z; a hood is behind the neck whichever way you face).
	# The opening is a hole cut in the hood, so at head height the mesh has
	# far less material straight ahead than straight behind.
	var verts: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var front_centre := 0
	var back_centre := 0
	for p in verts:
		if p.y < 2.20 or p.y > 2.50 or absf(p.x) > 0.12:
			continue
		if p.z > 0.15:
			front_centre += 1
		elif p.z < -0.15:
			back_centre += 1
	if front_centre * 2 < back_centre:
		print("PASS: hood opens toward +Z, the character's front (front=%d back=%d verts)"
			% [front_centre, back_centre])
	else:
		_fail("hood does not open toward the character's front (+Z): front=%d back=%d verts"
			% [front_centre, back_centre])
	# HOOD LOCK. Above build_cloak.py's own ramp the hood is bound to the
	# HEAD BONE ALONE. Sharing it with neck/spine (how the source rig came)
	# means a hard torso crank moves hood and head apart and the back of the
	# head comes out through the cloth -- the charged swing did exactly that.
	# Bound to one bone, the hood can only move as the head moves.
	var hood_binds: Array[String] = []
	for i in range(model_mesh.skin.get_bind_count()):
		hood_binds.append(model_mesh.skin.get_bind_name(i))
	var hb: PackedInt32Array = arrays[Mesh.ARRAY_BONES]
	var hw: PackedFloat32Array = arrays[Mesh.ARRAY_WEIGHTS]
	var infl := hb.size() / maxi(1, verts.size())
	var hood_verts := 0
	var leaked := 0
	for i in range(verts.size()):
		if verts[i].y < 2.15:
			continue
		hood_verts += 1
		for k in range(infl):
			if hw[i * infl + k] > 0.02 and hood_binds[hb[i * infl + k]] != "head":
				leaked += 1
				break
	if hood_verts > 100 and leaked == 0:
		print("PASS: all %d hood verts follow the head bone alone -- nothing can outrun the hood" % hood_verts)
	else:
		_fail("%d of %d hood verts still follow a non-head bone; a hard torso turn pushes the head through"
			% [leaked, hood_verts])

	# SLEEVE LOCK. Same idea as the hood, one limb down: a sleeve that still
	# carries spine influence swings by a different amount from the arm
	# inside it, and on a run cycle the arm comes through. Anything mostly
	# arm-driven is bound to the arm chain ALONE, so the sleeve can only move
	# as the arm moves.
	# Judged on the FOREARM AND CUFF only, not the whole sleeve. Up at the
	# shoulder the lock deliberately fades out, because cloth that switches
	# abruptly from arm-driven to body-driven shears there -- so a little
	# spine influence at the very top is the design, not a leak. Below that
	# blend there is no excuse for any.
	var arm_chain := ["arm_l", "forearm_l", "hand_l", "arm_r", "forearm_r", "hand_r"]
	var sleeve_bones := ["forearm_l", "hand_l", "forearm_r", "hand_r"]
	var sleeve_verts := 0
	var sleeve_leaked := 0
	for i in range(verts.size()):
		var best_w := -1.0
		var top_name := ""
		for k in range(infl):
			var w: float = hw[i * infl + k]
			if w > best_w:
				best_w = w
				top_name = hood_binds[hb[i * infl + k]]
		if not (top_name in sleeve_bones) or best_w < 0.75:
			continue
		sleeve_verts += 1
		for k in range(infl):
			if hw[i * infl + k] > 0.02 and not (hood_binds[hb[i * infl + k]] in arm_chain):
				sleeve_leaked += 1
				break
	if sleeve_verts > 100 and sleeve_leaked == 0:
		print("PASS: all %d sleeve verts follow the arm chain alone -- the arm can't outrun the sleeve" % sleeve_verts)
	else:
		_fail("%d of %d sleeve verts still follow a non-arm bone; the arm will clip through mid-stride"
			% [sleeve_leaked, sleeve_verts])

	# NOT tested here: whether the character's arms actually sit inside the
	# sleeves. The source figure's arms hang closer to its body than this
	# rig's did and came out through the sides, which is why fused_body.gd's
	# own SHOULDER_X was widened -- but three attempts at a cheap geometric
	# stand-in for "does the arm fit" all failed to tell a good build from a
	# bad one, so none of them is here:
	#   - sleeve centroid vs arm axis over the whole sleeve: 0.088 good vs
	#     0.105 bad, no real separation (the shoulder cap wraps the torso and
	#     drags the mean inboard either way);
	#   - cloth reaching past ARM_R in every direction around the arm: passed
	#     at BOTH shoulder widths (the sleeve is a wide bell, so there is
	#     always cloth far out whichever side of it the arm is on);
	#   - the same centroid restricted to forearm-weighted verts: 0.196 off
	#     on a build that looks correct, because the bell's flare is not
	#     symmetric about the arm.
	# Deciding it needs real containment (is the arm's surface inside the
	# garment), which means raycasting the mesh. Until that exists the fit is
	# a LOOK-AT-IT check, not a tested one. The bone-rest assertion above is
	# the part that does hold the two together: it fails the moment
	# build_cloak.py's copy of SHOULDER_X drifts from fused_body.gd's.
	var source_bind_names: Array[String] = []
	for i in range(model_mesh.skin.get_bind_count()):
		source_bind_names.append(model_mesh.skin.get_bind_name(i))
	model.queue_free()

	# --- 3. equipping re-binds onto the character's own skeleton ---
	var skeleton := _find_skeleton(_character)
	if skeleton == null:
		_fail("character has no Skeleton3D")
		_finish()
		return
	# The character's own fused body mesh already lives under this skeleton,
	# so "the cloak" is the child that wasn't there before, not just the last
	# MeshInstance3D.
	var pre_existing := {}
	for c in skeleton.get_children():
		pre_existing[c] = true
	var before := skeleton.get_child_count()
	var cloak := CloakScene.instantiate()
	add_child(cloak)
	await get_tree().process_frame
	cloak._equip(_player)
	await get_tree().process_frame

	var worn: MeshInstance3D = null
	for c in skeleton.get_children():
		if c is MeshInstance3D and not pre_existing.has(c):
			worn = c as MeshInstance3D
	if worn != null:
		print("PASS: equipping parented ONE skinned mesh to the character's Skeleton3D")
	else:
		_fail("no MeshInstance3D was added under the character's Skeleton3D (children %d -> %d)"
			% [before, skeleton.get_child_count()])
		_finish()
		return

	if worn.get_node_or_null(worn.skeleton) == skeleton:
		print("PASS: the worn mesh's skeleton path resolves to the character's skeleton")
	else:
		_fail("worn mesh's skeleton path does not resolve to the character's skeleton")

	# THE check. Skeleton3D.get_bone_global_rest() is useless here --
	# fused_body.gd's own build_skeleton() never calls set_bone_rest(), so
	# every rest is identity and "bind == inverse rest" is a tautology that
	# passes while the cloak deforms into garbage. What the bind must
	# actually equal is what the BODY's own mesh is bound with on the same
	# skeleton: fused_body.gd's own build_skin(), a pure -bone_origins()
	# translation. Compare against the real thing, not against a restatement
	# of the code under test.
	var body_skin: Skin = FusedBody.build_skin(origins)
	var body_bind_for := {}
	for i in range(body_skin.get_bind_count()):
		body_bind_for[FusedBody.BONES[i]] = body_skin.get_bind_pose(i)
	var bad: Array[String] = []
	for i in range(worn.skin.get_bind_count()):
		var bone_name := worn.skin.get_bind_name(i)
		if not body_bind_for.has(bone_name):
			bad.append("%s missing on character" % bone_name)
			continue
		var want: Transform3D = body_bind_for[bone_name]
		var got := worn.skin.get_bind_pose(i)
		if not got.is_equal_approx(want):
			bad.append("%s bind=%s body_bind=%s" % [bone_name, got.origin, want.origin])
	if bad.is_empty():
		print("PASS: every cloak bind matches the body mesh's own bind on the same bone")
	else:
		_fail("cloak binds disagree with the body's: %s" % [bad])

	# A bind of exactly identity is the specific failure this file was
	# written after: it means the mesh gets yanked by each bone's whole pose
	# instead of pivoting around that bone's joint.
	var identity_binds := 0
	for i in range(worn.skin.get_bind_count()):
		if worn.skin.get_bind_pose(i).is_equal_approx(Transform3D.IDENTITY):
			identity_binds += 1
	if identity_binds == 0:
		print("PASS: no bind is identity (an identity bind tears the mesh apart when posed)")
	else:
		_fail("%d binds are identity" % identity_binds)

	# Bind ORDER matters -- ARRAY_BONES indexes bind slots, not names.
	var order_ok := worn.skin.get_bind_count() == source_bind_names.size()
	for i in range(worn.skin.get_bind_count()):
		if worn.skin.get_bind_name(i) != source_bind_names[i]:
			order_ok = false
	if order_ok:
		print("PASS: rebinding preserved the source skin's bind order")
	else:
		_fail("rebinding reordered the binds -- vertices would follow the wrong bones")

	# --- 4. the body underneath is replaced by stubs ---
	# The cloak can't stop a limb poking through it, so it takes the body
	# away instead and leaves only what shows anyway. What matters is that
	# the MIDRIFF is gone (that's what was poking through) while the hands,
	# feet and collar survive -- dropping everything, or dropping nothing,
	# would both be wrong and both are easy to do by accident.
	var body_mi: MeshInstance3D = null
	for c in skeleton.get_children():
		if c is MeshInstance3D and pre_existing.has(c):
			body_mi = c as MeshInstance3D
	if body_mi == null:
		_fail("couldn't find the character's own body mesh under the skeleton")
	elif not _character.is_body_stubs_only():
		_fail("equipping the cloak didn't switch the body to stubs")
	else:
		print("PASS: equipping the cloak switched the body to stubs")
		# The sleeve pose is applied BY WEARING IT, not by typing /arms every
		# session -- a real complaint, so a regression back to zero has to
		# fail here rather than be rediscovered in play. Read HERE, straight
		# after the equip: section 4b below deliberately drives the pose by
		# hand and resets it, so anywhere after that measures the test's own
		# bookkeeping instead of the cloak's.
		var equipped_pose: Vector3 = _character.get_worn_arm_pose()
		if equipped_pose.is_equal_approx(CloakScript.SLEEVE_ARM_ROTATION_DEGREES) \
				and not equipped_pose.is_zero_approx():
			print("PASS: putting the cloak on applies its own sleeve pose (%s) with no console command"
				% [equipped_pose])
		else:
			_fail("wearing the cloak left the arm pose at %s, expected its own %s"
				% [equipped_pose, CloakScript.SLEEVE_ARM_ROTATION_DEGREES])

		var stub: PackedVector3Array = body_mi.mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX]
		var midriff := 0
		var hands := 0
		var feet := 0
		var collar := 0
		# Derived from the tuning constants, not hardcoded, so lowering the
		# collar to show more chest doesn't turn this into a false failure.
		var midriff_top: float = FusedBody.STUB_COLLAR_Y - 0.05
		for p in stub:
			if p.y > 1.05 and p.y < midriff_top and absf(p.x) < 0.28:
				midriff += 1
			if p.y > 0.80 and p.y < 1.20 and absf(p.x) > 0.45:
				hands += 1
			if p.y < 0.30:
				feet += 1
			if p.y > FusedBody.STUB_COLLAR_Y and absf(p.x) < FusedBody.STUB_COLLAR_R:
				collar += 1
		if midriff == 0:
			print("PASS: the stub body has no torso left to poke through the cloak")
		else:
			_fail("%d stub verts still in the midriff" % midriff)
		if hands > 0 and feet > 0 and collar > 0:
			print("PASS: hands (%d), feet (%d) and collar (%d) all survived" % [hands, feet, collar])
		else:
			_fail("stub body lost a part it should keep: hands=%d feet=%d collar=%d"
				% [hands, feet, collar])

	# --- 4b. the worn arm pose moves the ARM and not the CLOAK ---
	# The whole point. Posing the arm BONES was tried first and is wrong:
	# the cloak is skinned to those same bones, so the sleeve swings out
	# with the arm and the fit is no better than before. The pose has to
	# live in the body's own Skin, which the cloak doesn't share.
	if body_mi != null:
		var cloak_bind_before := worn.skin.get_bind_pose(worn.skin.get_bind_count() - 1)
		var body_bind_before := body_mi.skin.get_bind_pose(FusedBody.BONES.find("arm_r"))
		_character.set_worn_arm_pose(Vector3(0, 0, 20))
		await get_tree().process_frame
		var body_bind_after := body_mi.skin.get_bind_pose(FusedBody.BONES.find("arm_r"))
		var cloak_bind_after := worn.skin.get_bind_pose(worn.skin.get_bind_count() - 1)
		if not body_bind_after.is_equal_approx(body_bind_before):
			print("PASS: a worn arm pose changes the body's own arm binding")
		else:
			_fail("worn arm pose left the body's arm binding untouched -- it does nothing")

		# THE HAND MUST NOT MOVE. A held weapon hangs off the hand BONE,
		# which a skin-space pose never touches -- so if the hand MESH
		# rotates away from it, the sword ends up outside the fist. That
		# happened (rotating the whole arm rigidly) and is why the rotation
		# now tapers to zero at the hand.
		var hand_idx := FusedBody.BONES.find("hand_r")
		var hand_bind := body_mi.skin.get_bind_pose(hand_idx)
		var hand_rest_bind := Transform3D(Basis(), -(origins["hand_r"] as Vector3))
		if hand_bind.is_equal_approx(hand_rest_bind):
			print("PASS: the hand stays put under a worn arm pose, so held weapons still line up")
		else:
			_fail("the arm pose moved the hand -- a held weapon will sit outside the fist")
		if cloak_bind_after.is_equal_approx(cloak_bind_before):
			print("PASS: ...and leaves the cloak's binding alone, so the sleeve stays put")
		else:
			_fail("the arm pose moved the cloak too -- that's the bug it exists to avoid")
		# The shared baked skin is used by every character in the game; posing
		# one must not reach into it.
		if body_mi.skin != FusedBody.build_skin(origins):
			print("PASS: the posed body uses its own Skin, not the shared baked one")
		_character.set_worn_arm_pose(Vector3.ZERO)
		await get_tree().process_frame

	# --- 5. it participates in the shared "top" slot bookkeeping ---
	if _character.has_method("clear_worn_top"):
		_character.clear_worn_top()
		# clear_worn_top() queue_free()s its parts, so "gone" is a deletion
		# that has been QUEUED -- reading the child list a frame later races
		# the idle-time reap.
		var still_there := not worn.is_queued_for_deletion()
		await get_tree().process_frame
		await get_tree().process_frame
		if not still_there and not is_instance_valid(worn):
			print("PASS: clear_worn_top() removes the cloak like any other top")
		else:
			_fail("clear_worn_top() left the cloak attached")
		# Taking the cloak off has to give the body back. clear_worn_top() is
		# the only removal path, so if it doesn't restore, a character who
		# ever wore a cloak stays a pair of floating hands forever.
		if not _character.is_body_stubs_only():
			print("PASS: taking the cloak off puts the body back")
		else:
			_fail("the body stayed hidden after the cloak came off")
		# Same reasoning for the arm pose: a garment that posed the arms
		# can't leave them posed once it's gone.
		if _character.get_worn_arm_pose() == Vector3.ZERO:
			print("PASS: taking the cloak off clears its arm pose")
		else:
			_fail("arm pose survived the cloak coming off: %s" % [_character.get_worn_arm_pose()])
	_finish()

func _finish() -> void:
	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
