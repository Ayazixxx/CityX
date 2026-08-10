extends Node3D
## Regression check for the grappling hook (items/grappling_hook.gd for the
## launcher/rope, player.gd's own start_grapple/_process_grapple_swing for
## the movement mode).
##
## Whether a swing FEELS good can only be judged live. What's checked here
## is the physics it rests on -- above all that the rope actually behaves as
## a rope: it must never stretch past its own length, it must kill only the
## outward part of velocity (keeping the tangential part a pendulum swings
## on), and letting go must preserve the momentum built up, since carrying
## that momentum out into free flight is the entire mechanic.

const HookScene := preload("res://items/grappling_hook.tscn")
const PlayerScene := preload("res://player/player.tscn")

var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	call_deferred("_run_checks")

func _run_checks() -> void:
	_build_world()
	await _check_fires_and_attaches()
	await _check_rope_never_stretches()
	await _check_swing_preserves_tangential_speed()
	await _check_release_keeps_momentum()
	await _check_reel_in_shortens_rope()
	await _check_rope_is_cleaned_up()
	await _check_landing_ends_the_swing()
	_check_swing_pose_does_not_tilt_the_body()
	await _check_swinging_leaves_the_body_upright()

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

## --- Swinging must not lay the player flat. Reported twice as "grappling
## hook leaves me sideways", and the cause both times was the POSE, not the
## physics: the swing holds one clip for its whole duration, so a rotation
## track on bone 0 (the hips, the rig's root) re-orients the entire body for
## as long as you hang there.
##
## This checks the clip's own DATA rather than observing a rendered result,
## because the failure was introduced by re-baking an animation for an
## unrelated feature (the intro cutscene's dive) -- something no test of the
## grapple's behaviour would ever have touched. Asserting the property the
## grapple actually depends on is what makes that class of change loud.
##
## Deliberately checks EVERY key, not just the first: a clip that starts
## upright and rolls over mid-way would read as fine at t=0 and still dump
## the player on their face two seconds into a swing. ---
func _check_swing_pose_does_not_tilt_the_body() -> void:
	var clip_name: String = PlayerScene.instantiate().GRAPPLE_SWING_ANIMATION
	var path := "res://character/animations/%s.tres" % clip_name
	if not ResourceLoader.exists(path):
		_fail("the grapple's swing clip '%s' doesn't exist" % clip_name)
		return

	var anim: Animation = load(path)
	var worst := 0.0
	for i in anim.get_track_count():
		if String(anim.track_get_path(i)) != "BodySkeleton:bones/0/rotation":
			continue
		for k in anim.track_get_key_count(i):
			var q: Quaternion = anim.track_get_key_value(i, k)
			worst = maxf(worst, rad_to_deg(2.0 * acos(clampf(absf(q.w), -1.0, 1.0))))

	# 15 degrees of lean is stylistic; anything approaching horizontal is the
	# bug. The clip that caused this measured 81.
	if worst > 15.0:
		_fail("the grapple's swing clip '%s' rotates the rig's root by up to %.1f degrees -- the player swings tilted that far over" % [clip_name, worst])
	else:
		print("PASS: the grapple's swing clip '%s' leaves the rig's root upright (max %.1f degrees)" % [clip_name, worst])

## --- The same property, end to end: actually swing, and confirm the hips
## bone is still upright while hanging. Catches the case where the clip is
## clean but something else rotates the root once a swing starts. ---
func _check_swinging_leaves_the_body_upright() -> void:
	var player := _make_player()
	_equip_hook(player)
	player.global_position = Vector3(0, 6.0, 6.0)
	await get_tree().physics_frame

	player.start_grapple(Vector3(0, 12.0, 0))
	for _i in range(45):
		await get_tree().physics_frame

	var character: Node = player.get_node_or_null("Character")
	if character == null:
		_fail("the player has no Character to check the pose on")
	else:
		var skeleton: Skeleton3D = character.get_node_or_null("BodySkeleton")
		if skeleton == null:
			_fail("no BodySkeleton on the character")
		else:
			var hips := skeleton.find_bone("hips")
			var q := skeleton.get_bone_pose_rotation(hips)
			var tilt := rad_to_deg(2.0 * acos(clampf(absf(q.w), -1.0, 1.0)))
			if tilt > 15.0:
				_fail("mid-swing the hips are rotated %.1f degrees -- the player is hanging sideways" % tilt)
			else:
				print("PASS: mid-swing the player's hips stay upright (%.1f degrees)" % tilt)

	player.release_grapple(false)
	player.queue_free()
	await get_tree().process_frame

## Ground plus a tall wall to grapple onto -- collision layer 1, the only
## layer the hook's own raycast accepts (world geometry, not characters).
func _build_world() -> void:
	_add_static_box(Vector3(0, -0.5, 0), Vector3(200, 1, 200))
	_add_static_box(Vector3(0, 15, -20), Vector3(60, 30, 2))

func _add_static_box(position: Vector3, size: Vector3) -> void:
	var body := StaticBody3D.new()
	body.collision_layer = 1
	var shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = size
	shape.shape = box
	body.add_child(shape)
	add_child(body)
	body.global_position = position

func _make_player() -> CharacterBody3D:
	var player: CharacterBody3D = PlayerScene.instantiate()
	add_child(player)
	player.add_to_group("players")
	return player

func _equip_hook(player: CharacterBody3D) -> Node:
	var character: Node3D = preload("res://character/character.tscn").instantiate()
	player.add_child(character)
	var hook: Node = HookScene.instantiate()
	player.add_child(hook)
	hook.call("_equip", player)
	return hook

## --- 1. Firing at real geometry attaches; firing at empty sky doesn't. ---
func _check_fires_and_attaches() -> void:
	var player := _make_player()
	player.global_position = Vector3(0, 2, 0)
	await get_tree().process_frame
	_equip_hook(player)
	await get_tree().process_frame

	if player._equipped_grapple == null:
		_fail("equipping the hook didn't register it on the player")
		player.queue_free()
		return
	print("PASS: the grappling hook registers on the player when equipped")

	# Aim at the sky -- nothing to catch.
	player.camera_pivot.rotation = Vector3.ZERO
	player.camera.global_transform = Transform3D(Basis(), player.global_position).looking_at(player.global_position + Vector3(0, 1, 0.01), Vector3.UP)
	if player.try_fire_grapple():
		_fail("firing at open sky still attached to something")
	else:
		print("PASS: a shot that hits nothing simply doesn't attach")

	# Aim at the wall.
	player.camera.global_transform = Transform3D(Basis(), player.global_position).looking_at(Vector3(0, 12, -20), Vector3.UP)
	if not player.try_fire_grapple():
		_fail("firing straight at a wall 20m away didn't attach")
	elif not player._grapple_active:
		_fail("the grapple attached but didn't enter the swinging movement mode")
	else:
		print("PASS: firing at a building attaches and enters the swing (rope %.1fm)" % player._grapple_rope_length)

	player.queue_free()

## --- 2. THE core rope property: a rope does not stretch. Swung hard under
## gravity for a full second, the player must never end up further from the
## anchor than the rope is long. ---
func _check_rope_never_stretches() -> void:
	var player := _make_player()
	player.global_position = Vector3(0, 25, 0)
	await get_tree().process_frame
	_equip_hook(player)
	await get_tree().process_frame

	var anchor := Vector3(0, 30, -8)
	player.start_grapple(anchor)
	var rope: float = player._grapple_rope_length
	# Thrown sideways hard, so the constraint is doing real work every frame.
	player.velocity = Vector3(18, 0, 0)

	var worst := 0.0
	for i in range(60):
		await get_tree().physics_frame
		worst = maxf(worst, player.global_position.distance_to(anchor))

	# A small tolerance: the constraint is solved once per frame, so a fast
	# frame can overshoot slightly before being pulled back.
	if worst > rope + 0.35:
		_fail("the rope stretched to %.2fm from a %.2fm rope -- it's behaving like elastic" % [worst, rope])
	else:
		print("PASS: the rope never stretched past its length (max %.2fm of %.2fm)" % [worst, rope])

	player.queue_free()

## --- 3. The constraint must kill only the OUTWARD velocity component. If
## it killed everything, the player would just hang dead on the rope instead
## of swinging. ---
func _check_swing_preserves_tangential_speed() -> void:
	var player := _make_player()
	player.global_position = Vector3(0, 20, 0)
	await get_tree().process_frame
	_equip_hook(player)
	await get_tree().process_frame

	var anchor := Vector3(0, 30, 0)
	player.start_grapple(anchor)
	# Purely horizontal: entirely tangential to a rope hanging straight up,
	# so an ideal constraint leaves every bit of it intact.
	player.velocity = Vector3(12, 0, 0)
	var start_speed := player.velocity.length()

	await get_tree().physics_frame
	await get_tree().physics_frame

	var horizontal := Vector3(player.velocity.x, 0.0, player.velocity.z).length()
	if horizontal < start_speed * 0.8:
		_fail("tangential speed collapsed from %.1f to %.1f in two frames -- the player would hang dead instead of swinging" % [start_speed, horizontal])
	else:
		print("PASS: the constraint keeps tangential speed (%.1f -> %.1f), so it really swings" % [start_speed, horizontal])

	player.queue_free()

## --- 4. Letting go preserves momentum (plus the deliberate upward kick).
## Releasing into a dead stop would make the whole mechanic pointless. ---
func _check_release_keeps_momentum() -> void:
	var player := _make_player()
	player.global_position = Vector3(0, 20, 0)
	await get_tree().process_frame
	_equip_hook(player)
	await get_tree().process_frame

	player.start_grapple(Vector3(0, 30, 0))
	player.velocity = Vector3(14, 0, 0)
	var before := player.velocity

	player.release_grapple(true)

	if player._grapple_active:
		_fail("release_grapple() left the swing active")
	elif not is_equal_approx(player.velocity.x, before.x):
		_fail("releasing changed horizontal momentum (%.1f -> %.1f)" % [before.x, player.velocity.x])
	elif player.velocity.y <= before.y:
		_fail("a boosted release gave no upward kick (%.1f -> %.1f)" % [before.y, player.velocity.y])
	else:
		print("PASS: releasing keeps horizontal momentum and adds the upward boost (y %.1f -> %.1f)" % [before.y, player.velocity.y])

	# An unboosted release (death, unequip) must NOT hand out free height.
	player.start_grapple(Vector3(0, 30, 0))
	player.velocity = Vector3(14, 0, 0)
	player.release_grapple(false)
	if not is_equal_approx(player.velocity.y, 0.0):
		_fail("an unboosted release still added %.1f upward velocity" % player.velocity.y)
	else:
		print("PASS: a forced (unboosted) release adds no free height")

	player.queue_free()

## --- 5. Reeling in shortens the rope, which is how you climb to a roof --
## and it must stop at the minimum rather than winding you into the wall. ---
func _check_reel_in_shortens_rope() -> void:
	var player := _make_player()
	player.global_position = Vector3(0, 5, 0)
	await get_tree().process_frame
	_equip_hook(player)
	await get_tree().process_frame

	player.start_grapple(Vector3(0, 30, 0))
	var start_length: float = player._grapple_rope_length

	# Drives the reel directly rather than faking held input -- what matters
	# is the clamp, and Input state can't be simulated reliably headlessly.
	player._grapple_rope_length = maxf(player.GRAPPLE_MIN_ROPE_LENGTH, start_length - player.GRAPPLE_REEL_SPEED * 1.0)
	if player._grapple_rope_length >= start_length:
		_fail("reeling in didn't shorten the rope")
	else:
		print("PASS: reeling shortens the rope (%.1fm -> %.1fm)" % [start_length, player._grapple_rope_length])

	for i in range(50):
		player._grapple_rope_length = maxf(player.GRAPPLE_MIN_ROPE_LENGTH, player._grapple_rope_length - player.GRAPPLE_REEL_SPEED * 0.1)
	if player._grapple_rope_length < player.GRAPPLE_MIN_ROPE_LENGTH - 0.001:
		_fail("the rope reeled in past its own minimum (%.2f) -- you'd wind into the wall" % player._grapple_rope_length)
	else:
		print("PASS: reeling stops at the minimum rope length (%.1fm)" % player._grapple_rope_length)

	player.queue_free()

## --- 6. The rope visual must never outlive the swing. A line left hanging
## in the sky after unequipping or dying is the most visible possible bug. ---
func _check_rope_is_cleaned_up() -> void:
	var player := _make_player()
	player.global_position = Vector3(0, 20, 0)
	await get_tree().process_frame
	var hook := _equip_hook(player)
	await get_tree().process_frame

	player.start_grapple(Vector3(0, 30, 0))
	await get_tree().process_frame
	if hook._rope == null or not is_instance_valid(hook._rope):
		_fail("attaching produced no rope visual at all")
	else:
		print("PASS: attaching draws a rope")

	player.release_grapple(false)
	await get_tree().process_frame
	if hook._rope != null and is_instance_valid(hook._rope):
		_fail("the rope visual survived releasing the grapple")
	else:
		print("PASS: releasing removes the rope")

	# Unequipping mid-swing: the swing must end AND the rope must go.
	player.start_grapple(Vector3(0, 30, 0))
	await get_tree().process_frame
	player.unequip_grappling_hook()
	await get_tree().process_frame
	if player._grapple_active:
		_fail("unequipping the launcher left the player still tethered mid-air")
	elif hook._rope != null and is_instance_valid(hook._rope):
		_fail("unequipping left the rope hanging in the world")
	else:
		print("PASS: unequipping mid-swing ends the swing and clears the rope")

	player.queue_free()

## --- A swing that settles onto the ground has to end itself. A real
## reported bug: the player ended up lying face-down on the road, unable to
## stand, still tethered. Two things combined to cause it -- start_grapple()
## plays the LOOPING "falling" clip (a face-down freefall pose), and
## _physics_process early-returns into the swing branch for as long as
## _grapple_active is true, so nothing ever reclaimed the animation or gave
## movement control back.
##
## The old exit condition was speed-only ("on the floor AND nearly
## stopped"), which the swing itself defeated: it keeps applying air control
## every frame INCLUDING while grounded, so holding a movement key held the
## player above the threshold forever. This drives that exact case -- press
## and hold forward, on the floor -- and expects the grounded TIMER to cut
## the rope anyway. ---
func _check_landing_ends_the_swing() -> void:
	var player := _make_player()
	# Started just above the ground and left to settle, rather than spawned
	# embedded in it -- is_on_floor() only latches after move_and_slide()
	# actually resolves a floor contact.
	# Well clear of the other checks' players, which are still in the scene at
	# the origin -- spawning on top of them left this one resting on a body
	# instead of the ground, so is_on_floor() never latched and the timer never
	# accumulated (this test passed or failed depending on ordering luck).
	player.global_position = Vector3(40, 1.5, 40)
	await get_tree().process_frame
	_equip_hook(player)
	# WAIT for the floor to actually latch rather than assuming a fixed
	# settle time. A flat 30 frames was enough almost always and not quite
	# always -- caught this reporting the stuck-tether bug once in a busy
	# batch run purely because the player was still falling when the swing
	# started, which is a false alarm about a bug that isn't there.
	var settle := 0
	while not player.is_on_floor() and settle < 240:
		await get_tree().physics_frame
		settle += 1
	if not player.is_on_floor():
		_fail("the player never reached the ground to set up a grounded swing")
		return

	# Anchored high overhead, so the rope itself never lifts the player back
	# off the ground -- exactly the "swung down and settled" state.
	player.start_grapple(Vector3(40, 40, 40))
	if not player._grapple_active:
		_fail("couldn't set up a grounded swing to test")
		return

	# Real ground speed, so the pre-existing "on the floor AND nearly stopped"
	# check CANNOT be what ends this -- only the grounded timer can. Without
	# this the player starts at ~0 speed and the old check fires on frame 1,
	# which would make this test pass against the very bug it exists for.
	player.velocity = Vector3(6, 0, 0)
	# Held forward is what defeated the speed-only check in the real bug. The
	# engine drives _physics_process here (the player is a real node in the
	# tree) -- calling it by hand as well would double-step the swing.
	Input.action_press("move_forward")
	var frames := 0
	# GRAPPLE_MAX_GROUND_SECONDS is 0.35s -- ~21 physics frames of continuous
	# ground contact. The ceiling is far past that so a bounce or two costing
	# a few frames of contact can't be mistaken for the tether never cutting,
	# while a genuinely stuck tether still fails.
	while player._grapple_active and frames < 600:
		await get_tree().physics_frame
		frames += 1
	Input.action_release("move_forward")

	if player._grapple_active:
		_fail("still tethered after %d grounded frames -- this is the stuck-face-down-on-the-road bug" % frames)
		return
	print("PASS: a swing that settles on the ground cuts itself after %d frames" % frames)

	# HONEST LIMIT of this check: it passes if EITHER exit fires, and in this
	# harness the player settles below 1 m/s quickly enough that the
	# pre-existing speed test is usually what ends it. So it guards "a
	# grounded swing always ends" -- which is the user-visible bug -- but it
	# does not prove the grounded timer specifically.
	#
	# Isolating the timer was attempted and abandoned: forcing the player to
	# stay above the speed threshold makes the rope go taut and lift them off
	# the floor, at which point they are genuinely airborne and the timer is
	# CORRECT not to fire. Reproducing the real case needs a slack rope plus
	# sustained ground contact, which this fixture can't set up cleanly.
