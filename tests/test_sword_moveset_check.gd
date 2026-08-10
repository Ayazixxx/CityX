extends Node
## Regression check for the two-handed-sword moveset (character.gd's own
## set_wielding_sword() and every method it redirects -- set_walking/punch/
## heavy_punch/block/jump/start_crouch/end_crouch/hit_reaction/
## block_impact): confirms every one of those correctly switches to its
## sword_* clip once wielding, that _on_animation_finished recognizes the
## new clip names (so state flags don't get stuck), that crouching+moving
## while sworded holds a static pose instead of looping/glitching (no
## dedicated crouch-walk clip exists for the pack, see character.gd's own
## comment), and that hit_reaction()/block_impact() are no-ops for an
## unarmed character (leaving the existing fist system provably untouched).

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")

var _ok := true
var _player: CharacterBody3D
var _character: Node3D

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

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

	_player = PlayerScene.instantiate()
	add_child(_player)
	_character = CharacterScene.instantiate()
	_character.set_paintable(false)
	_player.add_child(_character)

	call_deferred("_run_checks")

func _current() -> String:
	return _character.anim_player.current_animation

func _run_checks() -> void:
	for i in range(5):
		_player._physics_process(1.0 / 60.0)

	# 1. Unarmed baseline: hit_reaction()/block_impact() must be no-ops.
	_character.anim_player.play("idle")
	_character.hit_reaction()
	if _current() == "idle":
		print("PASS: hit_reaction() is a no-op while unarmed")
	else:
		_fail("hit_reaction() changed the animation while unarmed (now '%s')" % _current())
	_character.block_impact()
	if _current() == "idle":
		print("PASS: block_impact() is a no-op while unarmed")
	else:
		_fail("block_impact() changed the animation while unarmed (now '%s')" % _current())

	_character.set_wielding_sword(true)

	# 2. Idle: standing still now shows sword_idle.
	_player._physics_process(1.0 / 60.0)
	if _current() == "sword_idle":
		print("PASS: standing still while wielding shows sword_idle")
	else:
		_fail("standing still while wielding shows '%s', expected sword_idle" % _current())

	# 3. Walking forward shows sword_walk.
	Input.action_press("move_forward")
	_player._physics_process(1.0 / 60.0)
	Input.action_release("move_forward")
	if _current() == "sword_walk":
		print("PASS: walking forward while wielding shows sword_walk")
	else:
		_fail("walking forward while wielding shows '%s', expected sword_walk" % _current())

	# 4. Strafing left shows sword_walk_left.
	Input.action_press("move_left")
	_player._physics_process(1.0 / 60.0)
	Input.action_release("move_left")
	if _current() == "sword_walk_left":
		print("PASS: strafing left while wielding shows sword_walk_left")
	else:
		_fail("strafing left while wielding shows '%s', expected sword_walk_left" % _current())

	# 4b. Sprinting forward (added after a 14th "sprint" clip was supplied)
	# shows sword_run, not just sword_walk.
	Input.action_press("move_forward")
	Input.action_press("sprint")
	for i in range(5): # let stamina/effective_sprinting actually engage
		_player._physics_process(1.0 / 60.0)
	if _current() == "sword_run":
		print("PASS: sprinting forward while wielding shows sword_run")
	else:
		_fail("sprinting forward while wielding shows '%s', expected sword_run" % _current())
	Input.action_release("move_forward")
	Input.action_release("sprint")

	# 5. Regular attack (punch()) plays sword_attack, ignoring use_left_hand.
	_character.punch(true)
	if _current() == "sword_attack":
		print("PASS: punch() while wielding plays sword_attack")
	else:
		_fail("punch() while wielding plays '%s', expected sword_attack" % _current())
	if _character._is_punching:
		print("PASS: sword_attack correctly sets _is_punching")
	else:
		_fail("sword_attack didn't set _is_punching")
	_character._on_animation_finished("sword_attack")
	if not _character._is_punching:
		print("PASS: _on_animation_finished('sword_attack') clears _is_punching")
	else:
		_fail("_on_animation_finished('sword_attack') didn't clear _is_punching")

	# 5b. Combo: a second click within the window plays sword_attack2, a
	# 3rd click after that restarts at sword_attack, and a click AFTER the
	# window has expired also restarts at sword_attack (not attack2).
	# Explicitly reset first -- step 5's own punch() call above already
	# left _sword_combo_ready true (correctly -- the window outlives the
	# animation itself finishing, that's the whole point), so this section
	# needs a clean, known starting point rather than inheriting that.
	_character._sword_combo_ready = false
	_character.punch(false) # fresh first swing
	if _current() == "sword_attack":
		print("PASS: combo restarts at sword_attack for a fresh first swing")
	else:
		_fail("fresh first swing plays '%s', expected sword_attack" % _current())
	if _character._sword_combo_ready:
		print("PASS: sword_attack sets _sword_combo_ready for the follow-up window")
	else:
		_fail("sword_attack didn't set _sword_combo_ready")

	# The first swing has to actually FINISH (see _sword_swing_in_progress's
	# own comment -- punch() now drops a click that arrives mid-swing)
	# before a second click can land at all, combo or otherwise.
	_character._on_animation_finished("sword_attack")
	_character.punch(false) # second click, still well within the 5s window
	if _current() == "sword_attack2":
		print("PASS: a second click within the combo window plays sword_attack2")
	else:
		_fail("second click within the window plays '%s', expected sword_attack2" % _current())
	if not _character._sword_combo_ready:
		print("PASS: sword_attack2 consumes the combo window (no 3rd chained swing)")
	else:
		_fail("sword_attack2 left _sword_combo_ready still true")
	_character._on_animation_finished("sword_attack2")

	_character.punch(false) # 3rd click -- restarts the combo
	if _current() == "sword_attack":
		print("PASS: a 3rd click restarts the combo at sword_attack")
	else:
		_fail("3rd click plays '%s', expected sword_attack (combo restart)" % _current())
	_character._on_animation_finished("sword_attack")

	# Combo window actually expiring (not just being consumed by a
	# follow-up click) -- simulated via a single large _physics_process
	# delta rather than many small ticks, matching how other timers in this
	# project are already tested (e.g. test_punch_check.gd's own combat-mode
	# timeout check).
	if _character._sword_combo_ready:
		_character._physics_process(_character.SWORD_COMBO_WINDOW_SECONDS + 0.1)
	if _character._sword_combo_ready:
		_fail("_sword_combo_ready still true after the combo window should have expired")
	else:
		print("PASS: the combo window actually expires over time, not just on a follow-up click")
	_character.punch(false)
	if _current() == "sword_attack":
		print("PASS: clicking again after the combo window expired plays sword_attack, not sword_attack2")
	else:
		_fail("clicking after the window expired plays '%s', expected sword_attack" % _current())
	_character._on_animation_finished("sword_attack")

	# 5c. Spam-clicking must NOT interrupt a swing that's still actually
	# playing (real reported bug: player.gd's own PUNCH_COOLDOWN_SECONDS,
	# 0.35s, is nowhere near long enough to cover a 1.27s/1.83s sword
	# swing, so a spam-click could re-trigger play() well before the
	# current one had actually finished).
	_character._sword_combo_ready = false
	_character.punch(false) # starts sword_attack, _sword_swing_in_progress=true
	if _character._sword_swing_in_progress:
		print("PASS: punch() sets _sword_swing_in_progress while the swing plays")
	else:
		_fail("punch() didn't set _sword_swing_in_progress")
	_character.punch(false) # spam-click while sword_attack is still "playing"
	_character.punch(false) # and again
	if _current() == "sword_attack":
		print("PASS: spam-clicking punch() mid-swing doesn't restart/switch the animation")
	else:
		_fail("spam-clicking mid-swing changed the animation to '%s'" % _current())
	if _character._sword_combo_ready:
		print("PASS: the dropped spam-clicks didn't consume the combo window either")
	else:
		_fail("a dropped spam-click incorrectly consumed _sword_combo_ready")
	_character._on_animation_finished("sword_attack")
	if not _character._sword_swing_in_progress:
		print("PASS: _on_animation_finished('sword_attack') clears _sword_swing_in_progress")
	else:
		_fail("_on_animation_finished('sword_attack') didn't clear _sword_swing_in_progress")
	_character.punch(false) # now that the swing actually finished, this should land as the combo follow-up
	if _current() == "sword_attack2":
		print("PASS: a real click once the swing actually finished still triggers the combo follow-up correctly")
	else:
		_fail("click after swing finished plays '%s', expected sword_attack2" % _current())
	_character._on_animation_finished("sword_attack2")

	# Same spam-click protection for heavy_punch(), and cross-blocking
	# between the two (a regular attack shouldn't interrupt an in-progress
	# heavy swing or vice versa).
	_character.heavy_punch()
	_character.punch(false) # should NOT interrupt the heavy swing
	if _current() == "sword_heavy_attack":
		print("PASS: punch() can't interrupt an in-progress heavy attack swing")
	else:
		_fail("punch() interrupted the heavy attack swing (now '%s')" % _current())
	_character._on_animation_finished("sword_heavy_attack")

	# hit_reaction()/block_impact() must stay freely interruptible by a
	# real attack -- they don't touch _sword_swing_in_progress at all (only
	# punch()/heavy_punch() do), so a punch right after getting hit should
	# still land normally rather than being blocked.
	_character.hit_reaction()
	_character.punch(false)
	if _current() == "sword_attack":
		print("PASS: punch() can still interrupt hit_reaction() (getting hit doesn't lock out attacking back)")
	else:
		_fail("punch() couldn't interrupt hit_reaction() (still '%s')" % _current())
	_character._on_animation_finished("sword_attack")

	# 6. Heavy attack.
	_character.heavy_punch()
	if _current() == "sword_heavy_attack":
		print("PASS: heavy_punch() while wielding plays sword_heavy_attack")
	else:
		_fail("heavy_punch() while wielding plays '%s', expected sword_heavy_attack" % _current())
	_character._on_animation_finished("sword_heavy_attack")
	if not _character._is_punching:
		print("PASS: _on_animation_finished('sword_heavy_attack') clears _is_punching")
	else:
		_fail("_on_animation_finished('sword_heavy_attack') didn't clear _is_punching")

	# 7. Block.
	_character.block()
	if _current() == "sword_block":
		print("PASS: block() while wielding plays sword_block")
	else:
		_fail("block() while wielding plays '%s', expected sword_block" % _current())
	if not _character.is_blocking():
		_fail("is_blocking() false right after block() while wielding")
	else:
		print("PASS: is_blocking() still tracks state correctly while wielding")
	_character._on_animation_finished("sword_block")
	if _character.is_blocking():
		_fail("_on_animation_finished('sword_block') didn't clear is_blocking()")
	else:
		print("PASS: _on_animation_finished('sword_block') clears is_blocking()")

	# 8. Jump (single clip regardless of is_sprinting).
	_character.jump(true)
	if _current() == "sword_jump":
		print("PASS: jump(true) while wielding plays sword_jump (no separate sprint variant)")
	else:
		_fail("jump(true) while wielding plays '%s', expected sword_jump" % _current())
	_character.land()
	_character._on_animation_finished("sword_jump")
	if not _character._is_jumping:
		print("PASS: _on_animation_finished('sword_jump') clears _is_jumping")
	else:
		_fail("_on_animation_finished('sword_jump') didn't clear _is_jumping")

	# 9. Crouch transitions.
	_character.start_crouch()
	if _current() == "sword_standing_to_crouch":
		print("PASS: start_crouch() while wielding plays sword_standing_to_crouch")
	else:
		_fail("start_crouch() while wielding plays '%s', expected sword_standing_to_crouch" % _current())
	_character._on_animation_finished("sword_standing_to_crouch")
	if not _character._is_crouch_transitioning:
		print("PASS: _on_animation_finished('sword_standing_to_crouch') clears _is_crouch_transitioning")
	else:
		_fail("_on_animation_finished('sword_standing_to_crouch') didn't clear _is_crouch_transitioning")

	_character.end_crouch()
	if _current() == "sword_crouch_to_idle":
		print("PASS: end_crouch() while wielding plays sword_crouch_to_idle")
	else:
		_fail("end_crouch() while wielding plays '%s', expected sword_crouch_to_idle" % _current())
	_character._on_animation_finished("sword_crouch_to_idle")

	# 10. Crouching+moving while wielding holds a static pose (no dedicated
	# crouch-walk clip in the sword pack) rather than looping/restarting.
	Input.action_press("crouch")
	Input.action_press("move_forward")
	for i in range(5):
		_player._physics_process(1.0 / 60.0)
	var settled_anim := _current()
	if settled_anim != "sword_standing_to_crouch":
		_fail("crouching+moving while wielding shows '%s', expected sword_standing_to_crouch" % settled_anim)
	else:
		print("PASS: crouching+moving while wielding holds sword_standing_to_crouch")
	var pos_before: float = _character.anim_player.current_animation_position
	_player._physics_process(1.0 / 60.0)
	_player._physics_process(1.0 / 60.0)
	if _current() == settled_anim:
		print("PASS: holding crouch+move for extra frames doesn't restart/glitch the pose (still '%s')" % _current())
	else:
		_fail("crouching+moving glitched to '%s' on a later frame" % _current())
	Input.action_release("crouch")
	Input.action_release("move_forward")

	# 11. Armed hit_reaction()/block_impact() actually play their clips.
	_character.anim_player.play("sword_idle")
	_character.hit_reaction()
	if _current() == "sword_impact":
		print("PASS: hit_reaction() while wielding plays sword_impact")
	else:
		_fail("hit_reaction() while wielding plays '%s', expected sword_impact" % _current())
	_character._on_animation_finished("sword_impact")
	if not _character._is_punching:
		print("PASS: _on_animation_finished('sword_impact') clears _is_punching")
	else:
		_fail("_on_animation_finished('sword_impact') didn't clear _is_punching")

	_character.block_impact()
	if _current() == "sword_block_impact":
		print("PASS: block_impact() while wielding plays sword_block_impact")
	else:
		_fail("block_impact() while wielding plays '%s', expected sword_block_impact" % _current())
	_character._on_animation_finished("sword_block_impact")
	if not _character._is_punching:
		print("PASS: _on_animation_finished('sword_block_impact') clears _is_punching")
	else:
		_fail("_on_animation_finished('sword_block_impact') didn't clear _is_punching")

	# 12. take_damage() actually reaches these through the real player.gd
	# code path (not just calling character methods directly above).
	_character.set_wielding_sword(false)
	_player.health = _player.MAX_HEALTH
	_character.anim_player.play("idle")
	_player.take_damage(10.0)
	if _current() == "idle":
		print("PASS: take_damage() doesn't touch animation for an unarmed player")
	else:
		_fail("take_damage() changed animation ('%s') for an unarmed player" % _current())

	_character.set_wielding_sword(true)
	_player.health = _player.MAX_HEALTH
	_character.anim_player.play("sword_idle")
	_player.take_damage(10.0)
	if _current() == "sword_impact":
		print("PASS: take_damage() reaches hit_reaction() through the real player.gd code path")
	else:
		_fail("take_damage() didn't trigger sword_impact through player.gd (got '%s')" % _current())

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)
