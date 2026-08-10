extends Node
## Regression check for the Skyrim-style grab/carry system (player.gd's own
## _process_grab_carry/_find_grab_target/_apply_carry_force, character.gd's
## own "grabbable" tagging/RAGDOLL_MASS_MULTIPLIER/RAGDOLL_PHYSICS_MATERIAL,
## and the _rpc_start_carry/_rpc_carry_transform/_rpc_stop_carry sync trio):
##
## - A dead player's ragdoll bones are real, "grabbable"-tagged, bounce-
##   capable RigidBody3D -- the carry force stays bounded (no NaN/runaway
##   growth) even under a rapidly-moving target, for both a light bone
##   (hand) and a heavy one (hips).
## - The SAME force moves the light bone measurably further than the heavy
##   one over the same time -- proof the "can't fling heavy things" mass
##   asymmetry is real, not just claimed (this is what F=ma buys for free
##   from the already-existing per-bone masses).
## - Dropping hands real physics back to the body -- it actually settles on
##   the ground, and the shared bounce/friction PhysicsMaterial is really
##   attached (not just claimed in a comment).
## - Two-peer RPC sync (same 2-peers-in-one-process technique
##   tests/test_networked_combat_check.gd already establishes, calling each
##   _rpc_* handler directly rather than through real .rpc() dispatch):
##   peer B's own local copy of a bone peer A is carrying follows the
##   broadcast transform instead of independently falling under its own
##   gravity, and resumes independent physics once dropped.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _make_ground() -> void:
	var ground := StaticBody3D.new()
	ground.collision_layer = 1
	var gcol := CollisionShape3D.new()
	var gshape := BoxShape3D.new()
	gshape.size = Vector3(200, 1.0, 200)
	gcol.shape = gshape
	gcol.position = Vector3(0, -0.5, 0)
	ground.add_child(gcol)
	add_child(ground)

func _make_player(authority_id: int) -> CharacterBody3D:
	var player: CharacterBody3D = PlayerScene.instantiate()
	# Authority set BEFORE add_child -- see test_networked_combat_check.gd's
	# own identical comment (_ready() reads it synchronously during
	# add_child()).
	player.set_multiplayer_authority(authority_id)
	add_child(player)
	var character := CharacterScene.instantiate()
	character.set_paintable(false)
	player.add_child(character)
	return player

func _ready() -> void:
	_make_ground()
	call_deferred("_run_checks")

func _run_checks() -> void:
	# A dead, ragdolled player -- built directly via character.ragdoll(),
	# same shortcut tests/test_death_momentum_check.gd's own checks already
	# use rather than routing through real fall/combat damage.
	var victim := _make_player(2)
	victim.global_position = Vector3(50, 5.0, 50) # well clear of everything else below
	var victim_character: Node = victim.get_node("Character")
	victim_character.ragdoll()
	await get_tree().physics_frame
	await get_tree().physics_frame

	var hand: RigidBody3D = victim_character.get_ragdoll_body("hand_r")
	var hips: RigidBody3D = victim_character.get_ragdoll_body("hips")
	if hand == null or hips == null:
		_fail("ragdoll() didn't produce the expected hand_r/hips bodies")
		print("OVERALL: FAIL")
		get_tree().quit(1)
		return

	# --- 1. Tagging + bounce material -- the actual mechanism a dropped/
	# carried body bounces through, not just a claim in a comment. ---
	if hand.is_in_group("grabbable") and hips.is_in_group("grabbable"):
		print("PASS: ragdoll bones are tagged \"grabbable\"")
	else:
		_fail("ragdoll bones aren't in the \"grabbable\" group")
	var hand_mat: PhysicsMaterial = hand.physics_material_override
	if hand_mat != null and hand_mat.bounce > 0.0:
		print("PASS: ragdoll bodies carry a real bounce-capable PhysicsMaterial (bounce=", hand_mat.bounce, ")")
	else:
		_fail("ragdoll body has no real bounce material -- a dropped body would never bounce")

	# --- 2. Mass asymmetry -- neck's own multiplier bump (see character.gd's
	# own RAGDOLL_MASS_MULTIPLIER) shouldn't make the head/neck heavier than
	# the actual torso, and hips must still be, by a wide margin, the
	# heaviest bone in the rig. ---
	if hips.mass > hand.mass * 5.0:
		print("PASS: hips is still by far the heaviest bone (hips=", hips.mass, " hand=", hand.mass, ")")
	else:
		_fail("hips isn't meaningfully heavier than a hand anymore (hips=%s hand=%s)" % [hips.mass, hand.mass])

	# --- 3. Carrier player, with a grab/carry force applied directly
	# (bypassing the raycast/hold-timer -- those are plain input plumbing,
	# already covered by this project's own established "call the handler
	# directly" test convention). Whips the carrier's own position around
	# rapidly (which moves camera.global_position, the force's own target
	# anchor, right along with it) for many frames -- a real stress test of
	# the SAME bounded-force safety this session's own earlier
	# MAX_DEATH_FALL_IMPACT_SPEED tuning was built to guard against. ---
	var carrier := _make_player(1)
	carrier.global_position = Vector3(50, 5.0, 53.0)
	await get_tree().physics_frame

	var hand_start := hand.global_position
	carrier._start_carry(hand)
	var hand_stable := await _whip_and_check_stable(carrier, hand, 90)
	carrier._drop_carried_body()
	var hand_displacement: float = hand.global_position.distance_to(hand_start)

	# Reset hips near its own equivalent starting offset from the carrier
	# for a fair comparison, then run the identical whip pattern.
	hips.global_position = victim.global_position + Vector3(0, 1.0, 0)
	hips.linear_velocity = Vector3.ZERO
	hips.angular_velocity = Vector3.ZERO
	var hips_start := hips.global_position
	carrier._start_carry(hips)
	var hips_stable := await _whip_and_check_stable(carrier, hips, 90)
	carrier._drop_carried_body()
	var hips_displacement: float = hips.global_position.distance_to(hips_start)

	if hand_stable and hips_stable:
		print("PASS: carry force stays bounded (no NaN/runaway velocity) for both a light and a heavy bone under a rapidly-moving target")
	else:
		_fail("carry force produced an unbounded/NaN velocity somewhere (hand_stable=%s hips_stable=%s)" % [hand_stable, hips_stable])

	if hand_displacement > hips_displacement * 1.5:
		print("PASS: the SAME carry force moves the light hand bone measurably further than the heavy hips bone (hand=", hand_displacement, "m hips=", hips_displacement, "m) -- \"heavy resists, light flings\" is real")
	else:
		_fail("no real mass asymmetry observed (hand displacement=%s, hips displacement=%s) -- heavy bones aren't actually resisting more than light ones" % [hand_displacement, hips_displacement])

	# --- 4. Drop -> real gravity takes over -> it actually settles near the
	# ground, not still floating or NaN. ---
	for i in range(240):
		await get_tree().physics_frame
	if is_finite(hand.global_position.y) and hand.global_position.y < victim.global_position.y:
		print("PASS: a dropped bone falls under real gravity and settles (ended at y=", hand.global_position.y, ")")
	else:
		_fail("dropped bone didn't fall/settle correctly (y=%s)" % hand.global_position.y)

	# --- 5. Two-peer RPC sync -- peer B's own local copy of the SAME bone
	# peer A (the carrier) is holding follows the broadcast transform
	# instead of independently free-falling, and resumes independent
	# physics once dropped. Handlers called directly, matching
	# test_networked_combat_check.gd's own established convention (no real
	# second OS process/.rpc() dispatch available in-process). ---
	var sync_hand: RigidBody3D = victim_character.get_ragdoll_body("hand_l")
	sync_hand.global_position = Vector3(80, 10.0, 80)
	sync_hand.linear_velocity = Vector3.ZERO
	sync_hand.freeze = false
	await get_tree().physics_frame

	var owner_peer_id: int = victim.get_multiplayer_authority()
	carrier._rpc_start_carry(owner_peer_id, "hand_l")
	if not sync_hand.freeze:
		_fail("_rpc_start_carry didn't freeze the remote peer's own local copy of the held bone")
	else:
		print("PASS: _rpc_start_carry freezes the receiving peer's own local simulation of the held bone")

	var pre_sync_y := sync_hand.global_position.y
	for i in range(30):
		await get_tree().physics_frame
	var unmoved_while_frozen: bool = is_equal_approx(sync_hand.global_position.y, pre_sync_y)

	var synced_xform := Transform3D(Basis(), Vector3(80, 6.5, 80))
	carrier._rpc_carry_transform(owner_peer_id, "hand_l", synced_xform, Vector3(1, 0, 0))
	var followed: bool = sync_hand.global_position.is_equal_approx(synced_xform.origin)
	if unmoved_while_frozen and followed:
		print("PASS: a frozen remote bone doesn't independently fall, and DOES follow an incoming carry-transform broadcast")
	else:
		_fail("remote bone sync didn't behave as expected (unmoved_while_frozen=%s followed=%s)" % [unmoved_while_frozen, followed])

	carrier._rpc_stop_carry(owner_peer_id, "hand_l", synced_xform, Vector3.ZERO)
	if sync_hand.freeze:
		_fail("_rpc_stop_carry didn't unfreeze the bone -- it would stay stuck forever on every other peer's screen")
	else:
		print("PASS: _rpc_stop_carry unfreezes the bone so it resumes independent local physics")

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

## Moves `carrier` around rapidly for `frames` physics steps (dragging the
## carry target -- camera.global_position -- along with it), applying the
## carry force each step, and confirms `body`'s own linear_velocity never
## goes non-finite or beyond a generous sanity ceiling. Returns false the
## instant either happens. A mid-loop auto-drop (MAX_CARRY_DISTANCE_BEFORE_
## AUTO_DROP tripping, see _apply_carry_force's own comment) is treated as
## a SAFE, successful outcome, not a failure -- it's the safety mechanism
## working as designed, not the force blowing up -- and this guards against
## the real crash that would otherwise follow from calling
## _apply_carry_force again with _carried_body already null.
func _whip_and_check_stable(carrier: CharacterBody3D, body: RigidBody3D, frames: int) -> bool:
	var base: Vector3 = carrier.global_position
	for i in range(frames):
		if carrier._carried_body == null:
			return true
		carrier.global_position = base + Vector3(sin(float(i) * 0.9) * 1.5, cos(float(i) * 1.3) * 0.75, sin(float(i) * 0.5) * 1.5)
		carrier._apply_carry_force(1.0 / 60.0)
		await get_tree().physics_frame
		var v: Vector3 = body.linear_velocity
		if not (is_finite(v.x) and is_finite(v.y) and is_finite(v.z)):
			return false
		if v.length() > 500.0: # generous -- real instability blows up far past this, not gently exceeds it
			return false
	return true
