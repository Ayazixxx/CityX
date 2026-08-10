extends Node
## Functional check for player.gd's new animation-sync additions: hosts a
## real session (so is_multiplayer_authority()/Net.is_networked() behave for
## real, same reasoning as the earlier scratch_mp_spawn_check.gd), spawns
## TWO Player instances -- one given OUR OWN authority (id 1, simulating the
## local/owning peer) and one given a fake remote authority (id 2,
## simulating another peer's body) -- and verifies: the local one populates
## synced_is_walking/etc from real WASD input during _physics_process; the
## "remote" one's _process applies whatever synced_* values it's holding to
## its own Character via set_walking(), exactly mirroring what real
## replication would eventually write into those properties; and each
## _rpc_* wrapper, called directly (a real second OS process isn't available
## within one, see main.gd's own header comment on that same limitation),
## correctly invokes the matching character.X() method.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _local_player: CharacterBody3D
var _remote_player: CharacterBody3D
var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	# Same ground-plane setup test_dash_check.gd already uses -- without a
	# floor to stand on, is_on_floor() stays false and _physics_process's
	# own effective_sprinting falls back to whatever _air_locked_sprinting
	# already was (default false), masking the very state this test wants
	# to check.
	var ground := StaticBody3D.new()
	ground.collision_layer = 1
	var gcol := CollisionShape3D.new()
	var gshape := BoxShape3D.new()
	gshape.size = Vector3(200, 1.0, 200)
	gcol.shape = gshape
	gcol.position = Vector3(0, -0.5, 0)
	ground.add_child(gcol)
	add_child(ground)

	var err := Net.host_game()
	if err != OK:
		_fail("Net.host_game() failed with error %d" % err)
	elif multiplayer.get_unique_id() != 1:
		_fail("hosting peer's own id should be 1")
	else:
		print("PASS: hosted a real session (own id=1)")

	_local_player = PlayerScene.instantiate()
	_local_player.set_multiplayer_authority(1)
	add_child(_local_player)
	_local_player.add_child(CharacterScene.instantiate())

	_remote_player = PlayerScene.instantiate()
	# Authority set BEFORE add_child -- see the real bug this exact ordering
	# mistake caused in main.gd (just fixed there too): _ready() runs
	# synchronously during add_child and reads is_multiplayer_authority()
	# exactly once, so setting authority afterward would make this "remote"
	# player's _ready() wrongly see itself as our own authority.
	_remote_player.set_multiplayer_authority(2)
	add_child(_remote_player)
	_remote_player.add_child(CharacterScene.instantiate())

	if not _local_player.is_multiplayer_authority():
		_fail("local player (authority=1) should be our own authority")
	else:
		print("PASS: local player recognizes itself as the authority")
	if _remote_player.is_multiplayer_authority():
		_fail("remote player (authority=2) should NOT be our own authority")
	else:
		print("PASS: remote player correctly recognizes it's NOT our own authority")

	call_deferred("_run_checks")

func _run_checks() -> void:
	# Let the local player settle onto the ground first -- is_on_floor()
	# starts false for a just-spawned body, and effective_sprinting falls
	# back to the (default false) _air_locked_sprinting value until a real
	# grounded frame updates it (see player.gd's own comment on that carry-
	# through), which would otherwise mask exactly the state check 1 wants.
	for i in range(5):
		_local_player._physics_process(1.0 / 60.0)

	# 1. Local player: real WASD input during _physics_process should
	# populate synced_is_walking/etc, matching exactly what it hands to
	# character.set_walking() itself.
	Input.action_press("move_forward")
	Input.action_press("sprint")
	_local_player._physics_process(1.0 / 60.0)
	Input.action_release("move_forward")
	Input.action_release("sprint")

	if _local_player.synced_is_walking and _local_player.synced_is_running and _local_player.synced_move_direction == "forward":
		print("PASS: local player's synced_* fields reflect real sprint-forward input (walking=", _local_player.synced_is_walking, " running=", _local_player.synced_is_running, " dir=", _local_player.synced_move_direction, ")")
	else:
		_fail("local player's synced_* fields don't reflect real input (walking=%s running=%s dir=%s)" % [_local_player.synced_is_walking, _local_player.synced_is_running, _local_player.synced_move_direction])

	# 2. Remote player: simulate replication having already delivered new
	# synced_* values (this is exactly what MultiplayerSynchronizer would do
	# for real once actually networked with a second peer), then confirm its
	# own _process (the only thing that still runs for a non-authority body)
	# applies them to its Character via set_walking().
	var remote_character := _remote_player.get_node_or_null("Character")
	if remote_character == null:
		_fail("remote player has no Character child to check animation state on")
		print("OVERALL: ", "PASS" if _ok else "FAIL")
		get_tree().quit(0 if _ok else 1)
		return

	_remote_player.synced_is_walking = true
	_remote_player.synced_is_running = false
	_remote_player.synced_is_crouching = false
	_remote_player.synced_move_direction = "left"
	_remote_player._process(1.0 / 60.0)

	if remote_character.anim_player.current_animation == "walk_left":
		print("PASS: remote player's _process applied synced_* state (walk_left) to its own Character")
	else:
		_fail("remote player's Character didn't reflect synced_* state (got '%s', expected 'walk_left')" % remote_character.anim_player.current_animation)

	# 3. Local player's own _process should be a no-op (it already drives its
	# Character from _physics_process directly) -- confirm it doesn't also
	# stomp the local Character's animation via the remote path.
	var local_character := _local_player.get_node_or_null("Character")
	var local_anim_before: String = local_character.anim_player.current_animation if local_character != null else ""
	_local_player._process(1.0 / 60.0)
	if local_character != null and local_character.anim_player.current_animation == local_anim_before:
		print("PASS: local (authority) player's own _process is a no-op, doesn't double-drive animation")
	else:
		_fail("local player's _process changed animation state -- should be a no-op for the authority")

	# 4. Each _rpc_* wrapper, called directly (standing in for real delivery
	# over the network -- see this file's own header comment), should invoke
	# the matching character.X() method on whichever Character it's called on.
	_remote_player._rpc_jump(false)
	if remote_character._is_jumping:
		print("PASS: _rpc_jump() reached character.jump()")
	else:
		_fail("_rpc_jump() didn't set _is_jumping")

	_remote_player._rpc_land()
	if not remote_character._is_jumping:
		print("PASS: _rpc_land() reached character.land() (clears _is_jumping)")
	else:
		_fail("_rpc_land() didn't clear _is_jumping")

	_remote_player._rpc_punch(false)
	if remote_character.anim_player.current_animation == "punch_right":
		print("PASS: _rpc_punch(false) reached character.punch() (punch_right)")
	else:
		_fail("_rpc_punch(false) didn't play punch_right (got '%s')" % remote_character.anim_player.current_animation)

	_remote_player._rpc_punch(true)
	if remote_character.anim_player.current_animation == "punch_left":
		print("PASS: _rpc_punch(true) reached character.punch() (punch_left)")
	else:
		_fail("_rpc_punch(true) didn't play punch_left (got '%s')" % remote_character.anim_player.current_animation)

	_remote_player._rpc_heavy_punch()
	if remote_character.anim_player.current_animation == "punch_heavy":
		print("PASS: _rpc_heavy_punch() reached character.heavy_punch()")
	else:
		_fail("_rpc_heavy_punch() didn't play punch_heavy (got '%s')" % remote_character.anim_player.current_animation)

	_remote_player._rpc_block()
	if remote_character.anim_player.current_animation == "block":
		print("PASS: _rpc_block() reached character.block()")
	else:
		_fail("_rpc_block() didn't play block (got '%s')" % remote_character.anim_player.current_animation)

	_remote_player._rpc_dash("backward")
	if remote_character.anim_player.current_animation == "dash_backward":
		print("PASS: _rpc_dash('backward') reached character.dash()")
	else:
		_fail("_rpc_dash('backward') didn't play dash_backward (got '%s')" % remote_character.anim_player.current_animation)

	_remote_player._rpc_end_dash()
	if not remote_character._is_dashing:
		print("PASS: _rpc_end_dash() reached character.end_dash()")
	else:
		_fail("_rpc_end_dash() didn't clear _is_dashing")

	_remote_player._rpc_start_crouch()
	if remote_character.anim_player.current_animation == "standing_to_crouch":
		print("PASS: _rpc_start_crouch() reached character.start_crouch()")
	else:
		_fail("_rpc_start_crouch() didn't play standing_to_crouch (got '%s')" % remote_character.anim_player.current_animation)

	_remote_player._rpc_end_crouch()
	if remote_character.anim_player.current_animation == "crouch_to_idle":
		print("PASS: _rpc_end_crouch() reached character.end_crouch()")
	else:
		_fail("_rpc_end_crouch() didn't play crouch_to_idle (got '%s')" % remote_character.anim_player.current_animation)

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
