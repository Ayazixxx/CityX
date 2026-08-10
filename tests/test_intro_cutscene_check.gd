extends Node3D
## Regression check for the intro fall cutscene (player.gd's own
## start_intro_cutscene()/_on_intro_cutscene_finished(), player/
## intro_cutscene.gd's own timeline): starting it gates normal control
## immediately, the animation sequence actually runs through falling ->
## falling_flat_impact -> getting_up -> looking_around in order, the new
## 3-beat "looking around" camera sequence (cut to Shot A, cut to Shot B,
## slow rotate back to normal cinematic framing -- see intro_cutscene.gd's
## own LOOKING_AROUND_* consts) runs through all 3 beats in order, and
## finishing restores every piece of state the cutscene temporarily
## overrode (camera framing, camera.environment, control gate). Runs
## headless: real Player+Character scenes, real physics frames driving
## real AnimationPlayer/Tween playback, same phase-based state-machine
## convention tests/test_car_enter_exit_check.gd's own animated-sequence
## check already establishes. Real clip lengths (confirmed via the actual
## bake): falling=4.5s (looped), falling_flat_impact=0.77s, getting_up=
## 7.6s, looking_around=12.67s -- the whole cutscene runs close to 40
## real seconds end to end, so this test takes a while to finish.

const PlayerScene := preload("res://player/player.tscn")
const CharacterScene := preload("res://character/character.tscn")
const IntroCutsceneScript := preload("res://player/intro_cutscene.gd")

var _player: CharacterBody3D
var _character: Node
var _frame := 0
var _phase := 0
var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	var ground := StaticBody3D.new()
	ground.collision_layer = 1
	var gcol := CollisionShape3D.new()
	var gshape := BoxShape3D.new()
	gshape.size = Vector3(400, 1.0, 400)
	gcol.shape = gshape
	gcol.position = Vector3(0, -0.5, 0)
	ground.add_child(gcol)
	add_child(ground)

	var world_env := WorldEnvironment.new()
	world_env.name = "WorldEnvironment"
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.fog_enabled = true
	world_env.environment = env
	add_child(world_env)

	_player = PlayerScene.instantiate()
	_player.position = Vector3.ZERO
	add_child(_player)
	_character = CharacterScene.instantiate()
	_character.set_paintable(false)
	_player.add_child(_character)

func _physics_process(_delta: float) -> void:
	_frame += 1
	match _phase:
		0: # let both bodies settle, then start the cutscene directly (same
			# "call the trigger function directly" convention every other
			# scripted-sequence test in this project already uses).
			if _frame >= 10:
				_player.start_intro_cutscene(Vector3.ZERO)
				# _in_cutscene gates control synchronously, immediately --
				# unlike the falling animation/fog below, which player/
				# intro_cutscene.gd's own begin() now deliberately delays a
				# few frames (see its own class comment: a real reported bug
				# where the HUD's own delayed portrait capture lost a race
				# against "falling" starting immediately, snapshotting the
				# character mid-fall-pose instead of its neutral rest pose).
				if _player._in_cutscene:
					print("PASS: start_intro_cutscene() gates control immediately")
				else:
					_fail("_in_cutscene wasn't set immediately by start_intro_cutscene()")

				# NO await before this: the player must ALREADY be at
				# altitude the instant start_intro_cutscene returns, so no
				# frame can ever render them standing at spawn under the
				# ordinary camera.
				#
				# That's a real reported bug -- "a split second where it
				# shows your character at a normal pov standing on the
				# street". begin() used to wait six frames (for the HUD
				# portrait's own capture) BEFORE moving the player, so six
				# frames rendered exactly that.
				var altitude: float = _player.global_position.y
				if altitude < IntroCutsceneScript.START_ALTITUDE * 0.9:
					_fail("the player was still at y=%.1f right after the cutscene started -- frames will render them standing on the street" % altitude)
				else:
					print("PASS: the player is at altitude (%.0fm) before a single frame renders" % altitude)
				_phase = 1
				_frame = 0
		1: # wait out begin()'s own deliberate startup delay until "falling"
			# and the fog environment override/overhead battle-bus camera
			# actually kick in.
			if _character.anim_player.current_animation == "falling":
				print("PASS: the falling animation starts (after begin()'s own deliberate delay)")
				if _player.camera.environment != null:
					print("PASS: a fog camera.environment override is applied during the fall")
				else:
					_fail("camera.environment wasn't overridden for the fall's own height-fog reveal")
				if is_equal_approx(_player._spring_arm.spring_length, IntroCutsceneScript.OVERHEAD_SPRING_LENGTH) and _player.camera_pivot.rotation.x < -1.0:
					print("PASS: the camera swings to an overhead battle-bus view for the fall")
				else:
					_fail("camera wasn't set to the overhead fall view (spring_length=%s rotation.x=%s)" % [_player._spring_arm.spring_length, _player.camera_pivot.rotation.x])
				Input.action_press("move_forward")
				_phase = 2
				_frame = 0
			elif _frame > 30:
				_fail("falling animation never started within 30 frames of begin()'s own startup delay")
				Input.action_press("move_forward")
				_phase = 2
				_frame = 0
		2: # held move_forward should nudge the player slightly in -Z (a real
			# explicit ask: Fortnite-battle-bus-style steering, "WASD moves
			# you in that direction only slightly") -- not zero drift, but
			# also nowhere near full on-foot movement speed, and player.gd's
			# own physics/velocity is still fully gated the whole time.
			if _frame >= 20:
				Input.action_release("move_forward")
				var drifted_forward := _player.global_position.z < -0.01
				var stayed_small := _player.global_position.z > -3.0
				if _player.velocity.length() < 0.1 and drifted_forward and stayed_small and absf(_player.global_position.x) < 0.1:
					print("PASS: held move_forward nudges the player slightly (pos=%s) with no real physics velocity" % _player.global_position)
				else:
					_fail("drift during the fall wasn't the expected slight nudge (velocity=%s pos=%s)" % [_player.velocity, _player.global_position])
				_phase = 3
				_frame = 0
		3: # run out the fall (15s @ 60fps = 900 frames) until impact starts.
			if _character.anim_player.current_animation == "falling_flat_impact":
				if is_equal_approx(_player.global_position.y, 0.0):
					print("PASS: the fall lands at the spawn's own ground height and starts falling_flat_impact")
				else:
					_fail("landed at the wrong height (y=%s)" % _player.global_position.y)
				if is_equal_approx(_player._spring_arm.spring_length, IntroCutsceneScript.CUTSCENE_SPRING_LENGTH) and is_equal_approx(_player.camera_pivot.rotation.x, 0.0):
					print("PASS: the camera switched back to the cinematic framing before landing")
				else:
					_fail("camera didn't switch back to cinematic framing before impact (spring_length=%s rotation.x=%s)" % [_player._spring_arm.spring_length, _player.camera_pivot.rotation.x])
				_phase = 4
				_frame = 0
			elif _frame > 960:
				_fail("falling never transitioned to falling_flat_impact after 960 frames")
				_phase = 4
				_frame = 0
		4: # run out the impact clip + IMPACT_HOLD_SECONDS until getting_up starts.
			if _character.anim_player.current_animation == "getting_up":
				print("PASS: falling_flat_impact -> (hold) -> getting_up")
				_phase = 5
				_frame = 0
			elif _frame > 300:
				_fail("falling_flat_impact/hold never transitioned to getting_up after 300 frames")
				_phase = 5
				_frame = 0
		5: # run out getting_up until looking_around starts -- Shot A of the new
			# 3-beat camera sequence should already be cut to on this exact
			# frame (a real explicit ask: "cut to the front side of him, to
			# the side slightly" right as looking_around begins).
			if _character.anim_player.current_animation == "looking_around":
				print("PASS: getting_up -> looking_around")
				if is_equal_approx(_player.camera_pivot.rotation.y, deg_to_rad(IntroCutsceneScript.LOOKING_AROUND_SHOT_A_YAW_DEGREES)) \
						and is_equal_approx(_player.camera_pivot.rotation.x, deg_to_rad(IntroCutsceneScript.LOOKING_AROUND_SHOT_A_PITCH_DEGREES)) \
						and is_equal_approx(_player._spring_arm.spring_length, IntroCutsceneScript.LOOKING_AROUND_SHOT_A_SPRING_LENGTH):
					print("PASS: the camera cuts to the front-side Shot A framing right as looking_around starts")
				else:
					_fail("camera wasn't cut to Shot A (rotation=%s spring_length=%s)" % [_player.camera_pivot.rotation, _player._spring_arm.spring_length])
				_phase = 6
				_frame = 0
			elif _frame > 600:
				_fail("getting_up never transitioned to looking_around after 600 frames")
				_phase = 6
				_frame = 0
		6: # wait out LOOKING_AROUND_SHOT_A_HOLD_SECONDS until the camera cuts
			# to Shot B (elevated, "up in the air," on the opposite side).
			if is_equal_approx(_player.camera_pivot.rotation.y, deg_to_rad(IntroCutsceneScript.LOOKING_AROUND_SHOT_B_YAW_DEGREES)):
				if is_equal_approx(_player.camera_pivot.rotation.x, deg_to_rad(IntroCutsceneScript.LOOKING_AROUND_SHOT_B_PITCH_DEGREES)) \
						and is_equal_approx(_player._spring_arm.spring_length, IntroCutsceneScript.LOOKING_AROUND_SHOT_B_SPRING_LENGTH):
					print("PASS: the camera cuts to the elevated Shot B framing on the opposite side")
				else:
					_fail("Shot B's yaw matched but pitch/spring_length didn't (rotation=%s spring_length=%s)" % [_player.camera_pivot.rotation, _player._spring_arm.spring_length])
				_phase = 7
				_frame = 0
			elif _frame > 300:
				_fail("camera never cut to Shot B after 300 frames")
				_phase = 7
				_frame = 0
		7: # wait out LOOKING_AROUND_SHOT_B_HOLD_SECONDS + LOOKING_AROUND_
			# ROTATE_DURATION_SECONDS until the camera slowly rotates back
			# into the same normal cinematic framing used everywhere else in
			# this cutscene -- "rotate around him slowly into place of the
			# normal gameplay pov camera position," a real explicit ask.
			if is_equal_approx(_player.camera_pivot.rotation.y, 0.0) and is_equal_approx(_player.camera_pivot.rotation.x, 0.0) \
					and is_equal_approx(_player._spring_arm.spring_length, IntroCutsceneScript.CUTSCENE_SPRING_LENGTH):
				print("PASS: the camera slowly rotates back into the normal cinematic framing before looking_around ends")
				_phase = 8
				_frame = 0
			elif _frame > 600:
				_fail("camera never rotated back to normal cinematic framing after 600 frames")
				_phase = 8
				_frame = 0
		8: # run out looking_around + HANDOFF_DURATION_SECONDS until control
			# actually comes back. camera.environment is deliberately NOT
			# checked here -- it's still mid-fade at this exact instant (see
			# phase 9 below), a real explicit ask ("make the fog slowly fade
			# away instead of just going straight to clear").
			if not _player._in_cutscene:
				print("PASS: the cutscene finishes and clears _in_cutscene")
				if is_equal_approx(_player._spring_arm.spring_length, 4.0):
					print("PASS: spring_length restored to the normal on-foot value")
				else:
					_fail("spring_length wasn't restored (got %s)" % _player._spring_arm.spring_length)
				if _player.camera_pivot.rotation.is_equal_approx(Vector3.ZERO) and is_equal_approx(_player._pitch, 0.0):
					print("PASS: camera_pivot/_pitch reset to neutral, looking-straight-ahead")
				else:
					_fail("camera wasn't reset to neutral (rotation=%s pitch=%s)" % [_player.camera_pivot.rotation, _player._pitch])
				if _player._intro_cutscene == null:
					print("PASS: the cutscene component itself is freed")
				else:
					_fail("_intro_cutscene wasn't cleared/freed")
				# The fog fade now starts on the THIRD camera beat (the slow turn
				# back onto him), not at handoff, so that
				# the haze holds through the fall, landing and both held shots (a
				# direct ask: "make it last up until the camera turns to him...
				# only start fading once the camera starts turning"). That beat
				# plus the fade runs past the end of looking_around, so by
				# the time control hands back the fog is legitimately gone.
				# Either state is correct here -- what would be WRONG is the
				# fog still sitting at full density, which is what this now
				# actually guards.
				var fog_env: Environment = _player.camera.environment
				if fog_env == null:
					print("PASS: the fog finished lifting before control handed back (fade starts on the 3rd camera beat)")
				elif absf(fog_env.fog_height_density) < absf(IntroCutsceneScript.FOG_HEIGHT_DENSITY):
					print("PASS: the fog is partway through lifting at handoff (density %.4f of %.4f)" % [fog_env.fog_height_density, IntroCutsceneScript.FOG_HEIGHT_DENSITY])
				else:
					_fail("the fog never started lifting -- still at full density (%.4f) when control handed back" % fog_env.fog_height_density)
				_phase = 9
				_frame = 0
			elif _frame > 1200:
				_fail("looking_around/handoff never finished handing control back after 1200 frames")
				print("OVERALL: FAIL")
				get_tree().quit(1)
		9: # run out FOG_FADE_DURATION_SECONDS (see _start_fog_fade, bound to
			# `player` so it survives this cutscene node's own freeing at
			# handoff) until the fog actually finishes dissipating and
			# camera.environment clears back to null on its own.
			if _player.camera.environment == null:
				print("PASS: camera.environment clears once the fog finishes slowly fading out")
				print("OVERALL: ", "PASS" if _ok else "FAIL")
				get_tree().quit(0 if _ok else 1)
			elif _frame > 300:
				_fail("the fog never finished fading and clearing camera.environment after 300 frames")
				print("OVERALL: FAIL")
				get_tree().quit(1)
