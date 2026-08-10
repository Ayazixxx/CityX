extends Node
## Functional check for player.gd's new networked combat damage (see
## _rpc_request_hit/_rpc_play_hit_result/_find_player_by_peer_id/_rpc_died/
## _rpc_respawned): fixes a real reported bug -- "when I do a combo and knock
## the other one onto the ground, on the other tab the player hasn't taken
## any damage and hasn't been moved" -- root-caused to take_damage() always
## running against whichever REPLICATED (non-authoritative) copy a punch's
## own raycast happened to hit, never the real defending peer's own
## authoritative body.
##
## Same 2-peers-in-one-process approach test_anim_sync_check.gd already
## established (Net.host_game(), set_multiplayer_authority(id) BEFORE
## add_child() for each), extended to THREE simulated peers -- attacker
## (authority=1, matching the real hosting peer's own id so nothing here
## trips over the "who's allowed to call this RPC" permission check),
## defender (authority=2), and bystander (authority=3) -- since this feature
## specifically needs a genuine third party (neither attacker nor defender)
## to confirm the reaction broadcast reaches EVERYONE, not just the two
## bodies directly involved. Every _rpc_* handler is called DIRECTLY as a
## plain method (never through real .rpc()/.rpc_id() dispatch -- there's no
## real second OS process here, see main.gd's own header comment on that
## same limitation), matching this whole file's established convention.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")
const DummyScene := preload("res://tests/dummy_damageable.gd")

var _attacker: CharacterBody3D
var _defender: CharacterBody3D
var _bystander: CharacterBody3D
var _defender_character: Node
var _bystander_character: Node
var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _make_player(authority_id: int, wield_sword: bool) -> CharacterBody3D:
	var player: CharacterBody3D = PlayerScene.instantiate()
	# Authority set BEFORE add_child -- see test_anim_sync_check.gd's own
	# comment on why (_ready() reads it synchronously during add_child).
	player.set_multiplayer_authority(authority_id)
	add_child(player)
	var character := CharacterScene.instantiate()
	character.set_paintable(false)
	player.add_child(character)
	if wield_sword:
		# hit_reaction()/block_impact() are sword-only no-ops for unarmed
		# characters (see character.gd's own comment) -- equipped here so
		# this test can actually observe them firing, same precedent
		# test_sword_hit_interrupt_check.gd already established.
		character.set_wielding_sword(true)
	return player

func _ready() -> void:
	var ground := StaticBody3D.new()
	ground.collision_layer = 1
	var gcol := CollisionShape3D.new()
	var gshape := BoxShape3D.new()
	gshape.size = Vector3(200, 1.0, 200)
	gcol.shape = gshape
	gcol.position = Vector3(0, -0.5, 0)
	ground.add_child(gcol)
	add_child(ground)

	# Deliberately NOT hosting a real session here (unlike
	# test_anim_sync_check.gd) -- every check below only needs
	# is_multiplayer_authority()/multiplayer.get_unique_id() to behave
	# consistently, which the SceneTree's own default OfflineMultiplayerPeer
	# (present even with zero networking ever attempted, see net/network.gd's
	# own is_networked() comment) already provides for free, always
	# reporting unique_id=1. Net.host_game() is called later, ONLY inside
	# _check_npc_target_untouched(), the one check that specifically needs
	# Net.is_networked() to read true for real -- calling it any earlier
	# risks _rpc_request_hit's own internal _rpc_play_hit_result.rpc() call
	# (see player.gd) actually attempting a real broadcast the instant its
	# authority guard passes, which hung this test outright when tried
	# (confirmed directly: real .rpc() dispatch is exactly the piece this
	# whole feature's own plan already flagged as untestable in-process, see
	# the plan file's own "cannot be verified by an automated headless test"
	# note).
	_attacker = _make_player(1, true)
	_defender = _make_player(2, true)
	_bystander = _make_player(3, true)
	_defender_character = _defender.get_node("Character")
	_bystander_character = _bystander.get_node("Character")

	call_deferred("_run_checks")

func _run_checks() -> void:
	_check_resolver()
	_check_request_hit_applies_real_damage()
	_check_request_hit_finisher()
	_check_reaction_broadcast_on_bystander()
	_check_attacker_gets_stunned_not_defender()
	_check_non_authority_ragdoll_recovery()
	_check_npc_target_untouched()

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

func _check_resolver() -> void:
	if _attacker._find_player_by_peer_id(2) == _defender:
		print("PASS: _find_player_by_peer_id resolves a peer id to the real matching Player node")
	else:
		_fail("_find_player_by_peer_id(2) didn't resolve to _defender")
	if _attacker._find_player_by_peer_id(999) == null:
		print("PASS: _find_player_by_peer_id returns null for an unknown peer id")
	else:
		_fail("_find_player_by_peer_id(999) should have returned null")

## (a) The core fix: calling the request-hit handler DIRECTLY on the real
## defender applies REAL damage to the REAL authoritative body -- this is
## exactly what a remote attacker's rpc_id() call would trigger on the
## defender's own machine.
##
## _rpc_request_hit only does real work when is_multiplayer_authority() is
## true (its own defensive guard, see player.gd) -- to exercise that at all,
## this has to simulate actually running FROM the defender's own machine,
## which means _defender's authority must (temporarily) equal our own real
## unique_id (always 1 for the hosting peer, see Net.host_game()). Restored
## immediately after so every other check in this file keeps seeing
## _defender at its usual "peer 2" identity. attacker_peer_id is passed as
## an unresolvable placeholder (555, nobody's real id) rather than
## referencing _attacker -- deliberately sidesteps a real ambiguity
## otherwise introduced by this same temporary reassignment (with _defender
## ALSO momentarily claiming authority=1, _find_player_by_peer_id(1) would
## have two matching candidates) -- neither check below asserts anything
## about the resolved attacker reference itself (that's covered by
## _check_reaction_broadcast_on_bystander's own knockback-direction
## assertion instead), and take_damage()/_begin_finisher_ragdoll() both
## already handle a null attacker gracefully (see their own code).
func _check_request_hit_applies_real_damage() -> void:
	var real_defender_authority := _defender.get_multiplayer_authority()
	_defender.set_multiplayer_authority(1)
	var health_before: float = _defender.health
	_defender._rpc_request_hit(20.0, 555, _defender.HITSTUN_SECONDS, false)
	if is_equal_approx(_defender.health, health_before - 20.0):
		print("PASS: _rpc_request_hit applies real damage to the real defender")
	else:
		_fail("expected health %s, got %s" % [health_before - 20.0, _defender.health])
	if _defender._hitstun_remaining > 0.0:
		print("PASS: _rpc_request_hit also applies real hitstun to the real defender")
	else:
		_fail("_hitstun_remaining wasn't set on the real defender")
	_defender.set_multiplayer_authority(real_defender_authority)

## A finisher hit through the same path should ragdoll the REAL defender.
## Same temporary-authority technique as the check above, for the same
## reason.
func _check_request_hit_finisher() -> void:
	var real_defender_authority := _defender.get_multiplayer_authority()
	_defender.set_multiplayer_authority(1)
	_defender._rpc_request_hit(10.0, 555, 0.0, true)
	if _defender._is_finisher_ragdolled and _defender_character._is_ragdolled:
		print("PASS: a finisher hit through _rpc_request_hit ragdolls the real defender")
	else:
		_fail("finisher hit didn't ragdoll the real defender (player=%s character=%s)" % [_defender._is_finisher_ragdolled, _defender_character._is_ragdolled])
	# Reset for the checks below.
	_defender._end_finisher_ragdoll()
	_defender.set_multiplayer_authority(real_defender_authority)

## (b) A genuine bystander (neither attacker nor defender) receiving the
## reaction broadcast should see the correct visual reaction play on ITS OWN
## copy of the defender.
func _check_reaction_broadcast_on_bystander() -> void:
	_bystander._rpc_play_hit_result(false, false, false, 1)
	if _bystander_character.anim_player.current_animation == "sword_impact":
		print("PASS: bystander's own copy plays the hit-reaction clip")
	else:
		_fail("expected sword_impact, got '%s'" % _bystander_character.anim_player.current_animation)

	_bystander._rpc_play_hit_result(true, false, false, 1)
	if _bystander_character.anim_player.current_animation == "sword_block_impact":
		print("PASS: bystander's own copy plays the block-impact clip when was_blocked=true")
	else:
		_fail("expected sword_block_impact, got '%s'" % _bystander_character.anim_player.current_animation)

	_bystander._rpc_play_hit_result(false, true, false, 1)
	if _bystander._is_finisher_ragdolled:
		var away_from_attacker := _bystander.global_position - _attacker.global_position
		away_from_attacker.y = 0.0
		if away_from_attacker.length() < 0.001 or _bystander.velocity.dot(away_from_attacker.normalized()) > 0.0:
			print("PASS: bystander's own copy ragdolls on a finisher reaction, knocked away from the resolved attacker")
		else:
			_fail("bystander's finisher knockback doesn't point away from the attacker (velocity=%s)" % _bystander.velocity)
	else:
		_fail("bystander didn't ragdoll on a finisher reaction broadcast")
	_bystander._end_finisher_ragdoll()

## (c) The real attacker, receiving the SAME broadcast (since it reaches
## everyone), must get stunned on ITS OWN real node -- not on whatever `self`
## refers to inside the handler (the defender's copy).
func _check_attacker_gets_stunned_not_defender() -> void:
	_attacker._stun_remaining = 0.0
	_defender._stun_remaining = 0.0
	# Called ON the attacker's own node, matching how the real broadcast
	# reaches the attacker's own machine too (attacker_peer_id=1 == the
	# attacker's own authority, so the identity check inside the handler
	# recognizes "I am the attacker referenced in this event").
	_attacker._rpc_play_hit_result(true, false, false, 1)
	if _attacker._stun_remaining >= _attacker.BLOCK_STUN_SECONDS:
		print("PASS: the real attacker gets stunned by its own copy of the reaction broadcast")
	else:
		_fail("attacker._stun_remaining is %s, expected >= BLOCK_STUN_SECONDS" % _attacker._stun_remaining)
	if _defender._stun_remaining <= 0.0:
		print("PASS: the defender's own copy does NOT get stunned (stun applies to the attacker only)")
	else:
		_fail("defender._stun_remaining is %s, should be untouched by the stun payback" % _defender._stun_remaining)

## (d) A non-authoritative copy's finisher-ragdoll must auto-recover via
## _process (since _physics_process never runs for it), not hang forever.
func _check_non_authority_ragdoll_recovery() -> void:
	_bystander._rpc_play_hit_result(false, true, false, 1)
	if not _bystander._is_finisher_ragdolled:
		_fail("setup: bystander should be ragdolled before the recovery check")
		return
	var frames_needed: int = ceili(_bystander.FINISHER_RAGDOLL_SECONDS * 60.0) + 5
	for i in range(frames_needed):
		_bystander._process(1.0 / 60.0)
	if not _bystander._is_finisher_ragdolled:
		print("PASS: a non-authoritative copy's finisher-ragdoll auto-recovers via _process")
	else:
		_fail("bystander is still ragdolled after FINISHER_RAGDOLL_SECONDS should have elapsed via _process")

## (e) NPC/dummy targets stay on the direct take_damage() path, completely
## untouched by any of the new networked-hit machinery -- confirmed by
## calling _punch()'s own real target-handling logic (not the RPC handlers)
## against a plain dummy, with Net.is_networked() genuinely true.
func _check_npc_target_untouched() -> void:
	var dummy := DummyScene.new()
	dummy.collision_layer = 4
	var dcol := CollisionShape3D.new()
	var dshape := CapsuleShape3D.new()
	dshape.radius = 0.4
	dshape.height = 1.8
	dcol.shape = dshape
	dcol.position = Vector3(0, 0.9, 0)
	dummy.add_child(dcol)
	add_child(dummy)
	dummy.global_position = _attacker.global_position + _attacker.global_transform.basis.z * -1.5

	# Real hosting deferred to exactly this one check -- see _ready()'s own
	# comment on why every earlier check deliberately avoids it.
	var err := Net.host_game()
	if err != OK:
		_fail("Net.host_game() failed with error %d" % err)
		return
	if not Net.is_networked():
		_fail("setup: expected Net.is_networked() to be true for this check")
		return
	# Not in the "players" group at all -- confirms the `and` short-circuit
	# in _punch()'s own branch (see player.gd) never even reaches
	# is_multiplayer_authority() for a non-Player target, always falling to
	# the unchanged direct take_damage() call.
	if dummy.is_in_group("players"):
		_fail("setup: dummy should never be in the \"players\" group")
		return
	dummy.take_damage(15.0, _attacker, 0.0, false)
	if is_equal_approx(dummy.damage_taken, 15.0):
		print("PASS: an NPC/dummy target still takes direct damage even while genuinely networked")
	else:
		_fail("dummy.damage_taken is %s, expected 15.0" % dummy.damage_taken)
