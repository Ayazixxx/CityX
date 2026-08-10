extends Node
## Runs once, the very first time a genuinely NEW character enters the
## world (see player.gd's own start_intro_cutscene(), main.gd's own
## is_new_character threading through _enter_world_with): falls from the
## sky playing "falling" (looped) while ruins1.mp3 plays and a dense
## height-fog camera.environment override clears as the character
## descends, lands at exactly FALL_DURATION_SECONDS and plays
## "falling_flat_impact" (one-shot), holds, plays "getting_up" (one-shot),
## then "looking_around" (one-shot, a "taking in the new world" beat --
## confirmed placement), during which the camera runs its own 3-beat
## sequence (see _start_looking_around_camera_sequence's own comment: cut
## to a front-side angle, cut to an elevated angle on the other side, then
## slowly rotate back into the normal cinematic framing), then lerps the
## camera the rest of the way down to normal gameplay framing and hands
## control back.
##
## The fall itself is Fortnite-Battle-Bus style: an OVERHEAD camera (see
## _switch_to_cinematic_cam's own comment for how the pitch swings the
## camera up above the pivot to look down) looking down at the character
## falling through the height-fog toward the city, with WASD nudging
## horizontal position slightly (see _process_fall_drift) -- capped to a
## small radius around the drop point (MAX_DRIFT_DISTANCE) so a held
## direction can't drift the landing outside city.gd's own guaranteed-
## building-free origin street chunk. Shortly before landing, the camera
## swings back down to the normal cinematic side/behind framing used for
## the rest of the cutscene (impact/getting-up/looking-around) -- "switch
## the cutscene cam" right as the fall's own drama peaks, not a hard cut.
## The whole descent is a single continuously-accelerating tween (real
## reported bug in an earlier version: a deliberate slow-down flourish in
## the final 2 seconds read as "falling fast, then slow, right before
## impact" -- wrong, removed entirely in favor of just staying fast the
## whole way down).
##
## Self-contained: instantiated via .new() and added as a child of Player
## only for this cutscene's own duration (see loot/loot_component.gd's own
## class comment for why a plain script.new() needs no .tscn), freed once
## `finished` fires. player.gd owns entry/exit bookkeeping (the
## _in_cutscene gate, HUD visibility, city ambience fade, saved camera
## state) the same way it already owns begin_car_transition()/
## end_car_transition() for the car-seat state; this file owns the
## timeline itself.
##
## Drives player.global_position directly via Tween/direct writes rather
## than physics -- player.gd's own _physics_process is fully gated
## (_in_cutscene) for this whole window, so there's no move_and_slide()/
## gravity/fall-damage to fight (a real ~300m fall through normal physics
## would be lethal, see player.gd's own _apply_fall_damage).
##
## ruins1.mp3 is reparented onto `player` itself once it starts (see
## _setup_audio) and deliberately left to keep playing after this node
## frees at handoff -- a real reported ask ("instead of fading away just
## let it play the rest of the track"), replacing an earlier version that
## faded it out over its own AUDIO_FADE_DURATION_SECONDS window. A separate
## user-supplied one-shot (GroundImpactAudio) plays at the exact moment of
## landing, see _play_ground_impact_sound.
##
## A widened city.gd chunk-streaming radius for the fall was tried here and
## REVERTED -- generating that many extra chunks mid-cutscene made the whole
## intro visibly lag ("way too laggy"), which costs more than the unloaded
## ground at frame edges it was covering up.

signal finished

const RuinsAudio := preload("res://player/assets/audio/ruins1.mp3")
# A user-supplied clip specifically for this cutscene's own dramatic landing
# -- deliberately separate from player.gd's own generic fall_impact_player
# (used for ordinary in-game fall damage landings), since this one-time
# cinematic hit is its own distinct moment, not just another regular fall.
const GroundImpactAudio := preload("res://player/assets/audio/cutscene_ground_impact.mp3")
# 30% of unity gain, a real explicit ask -- converted via linear_to_db in
# _start_impact rather than just setting volume_db to a flat -30%, since
# volume_db is already a log/dB scale (a straight -30% off 0.0 dB is a much
# smaller perceived change than "30% as loud" actually means).
const GROUND_IMPACT_VOLUME_LINEAR := 0.3

const START_ALTITUDE := 300.0
# User's own explicit timing: "at 15 seconds your character reaches the
# ground." A single continuously-accelerating tween across the whole
# window now (see this file's own class comment on why the earlier two-
# segment version -- accelerate then decelerate right before impact -- got
# removed: a real reported bug, "falling fast and then he starts falling
# slow right before hitting the ground").
const FALL_DURATION_SECONDS := 15.0
const IMPACT_HOLD_SECONDS := 2.5
# Camera/spring_length settle back to normal gameplay framing over this
# window, once control is actually handed back.
const HANDOFF_DURATION_SECONDS := 1.5
# Wider cinematic framing than the normal 4.0m on-foot spring_length --
# same idea as player.gd's own DRIVING_SPRING_LENGTH (7.0) for the car
# seat, just pulled back further to keep the whole falling body in frame
# against the city below. Used for impact/getting-up/looking-around, and
# whatever the fall's own OVERHEAD framing settles back down into right
# before landing (see _switch_to_cinematic_cam).
const CUTSCENE_SPRING_LENGTH := 9.0

# --- Overhead "Battle Bus" freefall camera (see _switch_to_cinematic_cam's
# own comment for the actual pitch-sign reasoning: a NEGATIVE camera_pivot
# pitch swings the spring arm's own camera end UP AND ABOVE the pivot,
# looking DOWN at it -- confirmed directly against the exact same pitch
# convention player.gd's own LOCK_ON_MAX_PITCH_DEGREES comment already
# documents for the opposite, positive-pitch case). ---
const OVERHEAD_SPRING_LENGTH := 10.0
const OVERHEAD_PITCH_DEGREES := 80.0
# How long before landing the camera starts swinging back down from
# overhead to the normal cinematic side/behind framing -- "when he is
# about to hit the ground switch the cutscene cam," a real explicit ask.
const CAM_SWITCH_LEAD_SECONDS := 2.5
const CAM_SWITCH_DURATION_SECONDS := 1.5

# Fortnite-Battle-Bus-style horizontal steering while falling -- WASD
# "moves you in that direction only slightly" (a real explicit ask), not
# full on-foot movement speed. MAX_DRIFT_DISTANCE hard-caps the TOTAL
# radius from the drop point regardless of how long a direction is held,
# rather than just capping per-frame speed and hoping -- keeps the actual
# landing spot safely inside city.gd's own guaranteed-building-free origin
# street chunk (see city.gd's own _load_chunk comment: "the origin sits at
# (0, 0), which is always a through-street chunk... so the player always
# spawns onto clear ground with no building on it") even if a player holds
# a direction the whole way down.
const FALL_DRIFT_SPEED := 4.0
const MAX_DRIFT_DISTANCE := 10.0

# Environment's real height-fog API is a reference height plus a falloff
# density (fog_height_min/fog_height_max, this file's own first guess,
# don't exist at all).
#
# WHAT THIS FOG CAN AND CANNOT DO, established the hard way:
# Godot's height fog only ever fills the space BELOW fog_height. Density
# scales as (fog_height - y), so it's thickest at ground level and stops
# entirely above fog_height. A NEGATIVE density does not invert that --
# it drives the term negative, which clamps to nothing, and the fog
# disappears completely (tried it; reported back as "its gotten rid of the
# fog completely").
#
# So this file's original goal, "thick haze up high, clear near the
# ground", is not achievable with height fog at all -- it's the one shape
# this API can't make. What it CAN make is a ground-level haze layer the
# character descends into, which is why the density is positive and stays
# positive.
#
# The landing/getting-up clarity that was actually asked for is handled a
# different way instead: the fade now starts at IMPACT rather than at
# handoff (see _start_impact), so the fog lifts across exactly those beats.
const FOG_HEIGHT := 80.0
# Roughly half the 0.036 that read as "can barely see the character", since
# that value is now doing its work entirely at ground level where the
# character actually is. Still a real, visible haze -- not clear air.
const FOG_HEIGHT_DENSITY := 0.018
# How long the height-fog takes to dissipate, starting from the third
# camera beat (see _rotate_to_normal_cutscene_cam, the one call site).
#
# Deliberately LONGER than LOOKING_AROUND_ROTATE_DURATION_SECONDS: at 3.0s
# against a 4.0s rotate the mist was fully gone before the camera even
# finished turning, which is the "fades too soon" that was reported. At 5.0
# it's still visibly lifting as the turn completes and control hands back.
#
# Also longer than HANDOFF_DURATION_SECONDS, which is why the tween is
# owned by `player` rather than by this node (see _start_fog_fade) -- this
# component frees itself at handoff and would otherwise kill its own fade
# mid-way. Clearing camera.environment straight to null instead of fading
# was the original reported bug here ("straight to clear ingame graphics").
const FOG_FADE_DURATION_SECONDS := 5.0

# --- "Looking around" 3-beat camera sequence (see
# _start_looking_around_camera_sequence) -- a real explicit ask: cut to a
# front-side angle right as the animation starts, cut again to an elevated
# angle on the opposite side, then slowly rotate back into the same normal
# cinematic framing _switch_to_cinematic_cam already settled into before
# impact (yaw=0, pitch=0, CUTSCENE_SPRING_LENGTH) -- picking up exactly where
# _start_handoff's own existing spring_length lerp expects to start from, so
# the two never fight each other. Durations sum to 11.0s, comfortably inside
# looking_around's own real ~12.67s runtime (see tests/
# test_intro_cutscene_check.gd's own documented clip length) with margin to
# spare, so the rotate is always fully settled before handoff begins. ---
const LOOKING_AROUND_SHOT_A_HOLD_SECONDS := 3.5
const LOOKING_AROUND_SHOT_B_HOLD_SECONDS := 3.5
const LOOKING_AROUND_ROTATE_DURATION_SECONDS := 4.0
# Front-side, slightly off-center rather than dead-on-face -- a real
# explicit ask ("the front side of him, to the side slightly").
const LOOKING_AROUND_SHOT_A_YAW_DEGREES := 150.0
const LOOKING_AROUND_SHOT_A_PITCH_DEGREES := -8.0
const LOOKING_AROUND_SHOT_A_SPRING_LENGTH := 5.5
# The opposite side from Shot A, and elevated ("up in the air") -- same
# negative-pitch-swings-the-camera-up-to-look-down convention
# OVERHEAD_PITCH_DEGREES's own comment already documents, just a much
# gentler angle than the freefall's own.
const LOOKING_AROUND_SHOT_B_YAW_DEGREES := -130.0
const LOOKING_AROUND_SHOT_B_PITCH_DEGREES := -35.0
const LOOKING_AROUND_SHOT_B_SPRING_LENGTH := 10.5

# Set by player.gd's own start_intro_cutscene() right after .new(), before
# add_child/begin().
var player: CharacterBody3D
var character: Node
var spawn_position: Vector3
var original_spring_length := 4.0

var _hold_timer := 0.0
var _holding := false
var _audio_player: AudioStreamPlayer
# True only during the actual overhead-freefall stretch (cleared the
# instant the cam-switch-to-cinematic kicks in, see
# _switch_to_cinematic_cam) -- gates _process_fall_drift so held WASD stops
# nudging position once the camera's already committed to the landing
# framing (steering while looking at a side-on cinematic shot wouldn't
# read sensibly).
var _drift_active := false
# Guards _start_fog_fade against running twice -- the real trigger is the
# third camera beat (_rotate_to_normal_cutscene_cam); _start_handoff calls
# it again only as a safety net.
var _fog_fade_started := false

## Real reported bug, root-caused directly: ui/hud.gd's own _capture_
## portrait() is a ONE-TIME snapshot of the character's REST pose, started
## from Player's own _ready() (which is what instantiates the HUD) and
## needing 4 awaited process frames of its own before it actually reads the
## skeleton. player.gd's own start_intro_cutscene() (called by main.gd
## right after, still inside the SAME synchronous spawn call) used to reach
## this function and start "falling" playing IMMEDIATELY -- racing ahead of
## the portrait's own delayed capture, so the portrait ended up snapshotting
## the character mid-FALL-POSE (an arm/head tilt from the falling animation)
## instead of its neutral standing rest pose, permanently baked into the
## HUD portrait forever after (it's never re-captured). This wait
## guarantees the portrait's own 4-frame capture sequence always finishes
## first, since it started strictly earlier in the same call chain.
func begin() -> void:
	# EVERYTHING POSITIONAL HAPPENS SYNCHRONOUSLY, before any await.
	#
	# This whole block used to sit AFTER the portrait wait below, which
	# meant six real frames rendered with the player standing at spawn under
	# the ordinary third-person camera before the fall began -- reported
	# directly as "a split second where it shows your character at a normal
	# pov standing on the street". main.gd now also fades through black
	# across this, but the fade is the polish; not rendering the wrong thing
	# in the first place is the fix.
	#
	# Safe to move ahead of the portrait wait because that race was only
	# ever about the POSE (see the comment above): ui/hud.gd's own portrait
	# camera positions itself off character.get_head_center_world() every
	# capture, so it follows the character to altitude perfectly happily,
	# and its cull mask already excludes the city behind them.
	player.global_position = spawn_position + Vector3(0, START_ALTITUDE, 0)
	player.velocity = Vector3.ZERO
	_setup_fog_environment()
	_setup_audio()
	player._spring_arm.spring_length = OVERHEAD_SPRING_LENGTH
	player.camera_pivot.rotation.x = deg_to_rad(-OVERHEAD_PITCH_DEGREES)
	_drift_active = true
	_start_fall_tween()

	# ONLY the falling ANIMATION waits -- that's the one thing that raced
	# the HUD portrait's own capture (see this function's own comment
	# above). Six frames is ~0.1s, during which the character is already
	# falling, just in their idle pose.
	for i in range(6):
		await get_tree().process_frame
	character.anim_player.play("falling")

## Camera3D.environment (a per-camera override of the ambient
## WorldEnvironment) rather than mutating city.tscn's own shared
## Environment directly -- that shared one is continuously re-touched every
## frame by city/day_night_cycle.gd's own day/night color blending, which
## would fight a direct edit. Already a proven technique in this exact
## codebase (ui/hud.gd's portrait camera, ui/inventory_panel.gd's own live
## preview camera). The city's own fog is purely depth-based
## (fog_height_density=0.0, unused) -- setting a real fog_height/fog_
## height_density here gives a real haze layer filling everything BELOW
## fog_height, which the character falls down into -- see
## FOG_HEIGHT_DENSITY's own comment for why "haze up high, clear at the
## ground" turned out to be the one thing this API can't express, and
## _start_fog_fade for how the fog is removed (a slow dissipate starting at
## impact, not an instant clear at handoff).
func _setup_fog_environment() -> void:
	var world_env: WorldEnvironment = player.get_parent().get_node_or_null("WorldEnvironment")
	if world_env == null or world_env.environment == null:
		return
	var env: Environment = world_env.environment.duplicate(true)
	env.fog_height = FOG_HEIGHT
	env.fog_height_density = FOG_HEIGHT_DENSITY
	player.camera.environment = env

func _setup_audio() -> void:
	_audio_player = AudioStreamPlayer.new()
	_audio_player.stream = RuinsAudio
	_audio_player.bus = "Music"
	# Reparented onto `player` (NOT left as this node's own child) so it
	# keeps playing after this cutscene component frees itself at handoff --
	# see this file's own class comment on why the track now plays out to
	# its own natural end instead of fading. Self-frees once the track
	# actually finishes, so it doesn't linger as a dead node forever after.
	player.add_child(_audio_player)
	_audio_player.play()
	_audio_player.finished.connect(_audio_player.queue_free)

func _start_fall_tween() -> void:
	var tw := create_tween()
	tw.tween_property(player, "global_position:y", spawn_position.y, FALL_DURATION_SECONDS)\
		.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
	tw.tween_callback(_start_impact)

	# Scheduled on its own separate Tween (just an interval + callback, no
	# property to animate) so it fires independent of the position tween's
	# own step count/trans/ease above.
	var cam_tw := create_tween()
	cam_tw.tween_interval(maxf(0.0, FALL_DURATION_SECONDS - CAM_SWITCH_LEAD_SECONDS))
	cam_tw.tween_callback(_switch_to_cinematic_cam)

## Swings the camera back down from the overhead freefall view to the
## normal cinematic side/behind framing every other beat of this cutscene
## already uses -- "when he is about to hit the ground switch the cutscene
## cam," a real explicit ask. A NEGATIVE camera_pivot pitch (see
## OVERHEAD_PITCH_DEGREES above) swings the spring arm's camera end UP AND
## ABOVE the pivot to look down at it; lerping pitch back to 0.0 (level)
## swings it back down behind the character at normal height, exactly the
## same geometry player.gd's own LOCK_ON_MAX_PITCH_DEGREES comment already
## documents for the mirror-image (positive-pitch) case.
func _switch_to_cinematic_cam() -> void:
	_drift_active = false
	var tw := create_tween()
	tw.set_parallel(true)
	tw.tween_property(player._spring_arm, "spring_length", CUTSCENE_SPRING_LENGTH, CAM_SWITCH_DURATION_SECONDS)
	tw.tween_property(player.camera_pivot, "rotation:x", 0.0, CAM_SWITCH_DURATION_SECONDS)

## Fortnite-Battle-Bus-style horizontal steering -- see FALL_DRIFT_SPEED/
## MAX_DRIFT_DISTANCE's own comments. World-axis-relative (not camera- or
## player-facing-relative): the overhead camera never rotates with mouse
## look during this window (player.gd's own _unhandled_input is fully
## gated by _in_cutscene), so there's no live facing to steer relative to
## -- W is always world -Z, D is always world +X, matching how the
## overhead view itself never rotates either.
func _process_fall_drift(delta: float) -> void:
	var input_dir := Vector2(
		Input.get_action_strength("move_right") - Input.get_action_strength("move_left"),
		Input.get_action_strength("move_back") - Input.get_action_strength("move_forward")
	)
	if input_dir.length() < 0.01:
		return
	var moved: Vector3 = player.global_position + Vector3(input_dir.x, 0.0, input_dir.y).normalized() * FALL_DRIFT_SPEED * delta
	var offset := Vector2(moved.x - spawn_position.x, moved.z - spawn_position.z)
	if offset.length() > MAX_DRIFT_DISTANCE:
		offset = offset.normalized() * MAX_DRIFT_DISTANCE
		moved.x = spawn_position.x + offset.x
		moved.z = spawn_position.z + offset.y
	player.global_position.x = moved.x
	player.global_position.z = moved.z

func _start_impact() -> void:
	_drift_active = false
	# "falling" is the only LOOPED clip in this game carrying a real hips
	# ROTATION track (its dive lean -- see tests/scratch_intro_fall_bake.gd's
	# own include_root_rotation comment), and being looped it never fires
	# animation_finished, so character.gd can't self-clear it the way it
	# already does for exiting_car. Cleared right here, the instant the fall
	# is over -- a real reported bug otherwise ("the character is stuck
	# sideways even once the game loads"): nothing else in the game ever
	# writes that bone again, so the dive lean would stay applied to the
	# whole body forever, through impact/getting_up/looking_around and all
	# ordinary gameplay after it.
	character.reset_hips_rotation()
	character.anim_player.animation_finished.connect(_on_impact_finished, CONNECT_ONE_SHOT)
	character.anim_player.play("falling_flat_impact")
	_play_ground_impact_sound()

## A user-supplied one-shot for the actual moment of landing -- see
## GroundImpactAudio/GROUND_IMPACT_VOLUME_LINEAR's own comments. A plain
## throwaway AudioStreamPlayer, same "new() + add_child() + play(), no
## .tscn needed" idiom PunchImpactSounds's own random-pick playback in
## player.gd already uses for one-shot SFX -- self-frees once it finishes,
## same as _setup_audio's own ruins1.mp3 player.
func _play_ground_impact_sound() -> void:
	var sfx := AudioStreamPlayer.new()
	sfx.stream = GroundImpactAudio
	sfx.volume_db = linear_to_db(GROUND_IMPACT_VOLUME_LINEAR)
	# "Master" -- this project has no dedicated SFX bus (see default_bus_
	# layout.tres: only "Music"/"Record" exist beyond it), matching every
	# other impact one-shot's own bus (player.tscn's own FallImpactPlayer/
	# PunchImpactPlayer, both "Master").
	sfx.bus = "Master"
	player.add_child(sfx)
	sfx.play()
	sfx.finished.connect(sfx.queue_free)

func _on_impact_finished(_anim_name: StringName) -> void:
	_hold_timer = IMPACT_HOLD_SECONDS
	_holding = true

func _process(delta: float) -> void:
	if _drift_active:
		_process_fall_drift(delta)
	if not _holding:
		return
	_hold_timer -= delta
	if _hold_timer <= 0.0:
		_holding = false
		_start_getting_up()

func _start_getting_up() -> void:
	character.anim_player.animation_finished.connect(_on_getting_up_finished, CONNECT_ONE_SHOT)
	character.anim_player.play("getting_up")

## "Looking Around" always plays here -- confirmed placement: a "taking in
## the new world" beat between standing up and gameplay starting.
func _on_getting_up_finished(_anim_name: StringName) -> void:
	character.anim_player.animation_finished.connect(_on_looking_around_finished, CONNECT_ONE_SHOT)
	character.anim_player.play("looking_around")
	_start_looking_around_camera_sequence()

## See this file's own LOOKING_AROUND_* consts for the full reasoning. An
## instant CUT to Shot A right as the animation starts, held, then an
## instant CUT to Shot B, held, then a slow tween rotating/pulling back to
## the same normal cinematic framing used everywhere else in this cutscene.
func _start_looking_around_camera_sequence() -> void:
	player.camera_pivot.rotation.y = deg_to_rad(LOOKING_AROUND_SHOT_A_YAW_DEGREES)
	player.camera_pivot.rotation.x = deg_to_rad(LOOKING_AROUND_SHOT_A_PITCH_DEGREES)
	player._spring_arm.spring_length = LOOKING_AROUND_SHOT_A_SPRING_LENGTH

	var tw := create_tween()
	tw.tween_interval(LOOKING_AROUND_SHOT_A_HOLD_SECONDS)
	tw.tween_callback(_cut_to_looking_around_shot_b)
	tw.tween_interval(LOOKING_AROUND_SHOT_B_HOLD_SECONDS)
	tw.tween_callback(_rotate_to_normal_cutscene_cam)

func _cut_to_looking_around_shot_b() -> void:
	player.camera_pivot.rotation.y = deg_to_rad(LOOKING_AROUND_SHOT_B_YAW_DEGREES)
	player.camera_pivot.rotation.x = deg_to_rad(LOOKING_AROUND_SHOT_B_PITCH_DEGREES)
	player._spring_arm.spring_length = LOOKING_AROUND_SHOT_B_SPRING_LENGTH

func _rotate_to_normal_cutscene_cam() -> void:
	# THE fog cue. The haze holds at full density through the whole fall,
	# the landing, getting up, and both held camera shots -- and only starts
	# lifting here, on the third beat, as the camera begins its slow turn
	# back onto him. Asked for exactly that way ("make it last up until the
	# camera turns to him... only start fading once the camera starts
	# turning to him after the 2nd camera cut").
	#
	# The fade is deliberately longer than this rotate, so the air is still
	# visibly clearing as gameplay control comes back rather than being
	# already done by the time the turn lands.
	_start_fog_fade()
	var tw := create_tween()
	tw.set_parallel(true)
	tw.tween_property(player.camera_pivot, "rotation:y", 0.0, LOOKING_AROUND_ROTATE_DURATION_SECONDS)
	tw.tween_property(player.camera_pivot, "rotation:x", 0.0, LOOKING_AROUND_ROTATE_DURATION_SECONDS)
	tw.tween_property(player._spring_arm, "spring_length", CUTSCENE_SPRING_LENGTH, LOOKING_AROUND_ROTATE_DURATION_SECONDS)

func _on_looking_around_finished(_anim_name: StringName) -> void:
	_start_handoff()

## Camera lerps back to normal gameplay -- this IS the "camera moves into
## place of the normal gameplay pov" beat. No explicit "play idle" call
## needed once control resumes: character.set_walking(...) already runs
## unconditionally every _physics_process frame for the local player, which
## naturally reclaims anim_player from whatever one-shot last held it,
## exactly like every other one-shot action (punch/jump/dash) already
## relies on. ruins1.mp3 is deliberately left alone here (see _setup_audio's
## own comment) -- no fade, no stop, it just keeps playing on its own.
func _start_handoff() -> void:
	var tw := create_tween()
	tw.tween_property(player._spring_arm, "spring_length", original_spring_length, HANDOFF_DURATION_SECONDS)
	tw.tween_callback(_finish)
	# The fog fade starts on the THIRD camera beat (see
	# _rotate_to_normal_cutscene_cam), not here.
	# Called again here purely as a safety net for the case where the third
	# camera beat somehow never ran -- _start_fog_fade is idempotent.
	_start_fog_fade()

## Slowly dissipates the fall's own height-fog rather than snapping it away
## instantly (see FOG_FADE_DURATION_SECONDS's own comment) -- a real
## reported bug ("straight to clear ingame graphics" instead of an actual
## fade). Bound to `player`, not `self` -- create_tween()'s tween is tied to
## whichever node's method calls it, and this cutscene node is queue_free()d
## by player.gd's own _on_intro_cutscene_finished() the instant `finished`
## fires (see _finish(), at HANDOFF_DURATION_SECONDS -- shorter than this
## fade's own, deliberately longer, duration), which would otherwise kill
## this tween mid-fade. Reparenting the AUDIO player onto `player` for the
## exact same survive-past-this-node's-own-freeing reason already
## established the pattern (see _setup_audio's own comment).
func _start_fog_fade() -> void:
	# Idempotent: called at impact, and again at handoff as a safety net.
	# A second fade tween on the same property would fight the first and
	# make the mist visibly stutter.
	if _fog_fade_started:
		return
	_fog_fade_started = true
	var env: Environment = player.camera.environment
	if env == null:
		return
	var tw := player.create_tween()
	tw.tween_property(env, "fog_height_density", 0.0, FOG_FADE_DURATION_SECONDS)
	# player.gd's own clear_camera_fog_override(), NOT a method here on this
	# script -- see that function's own comment for the real bug this
	# sidesteps: a bare method reference is always bound to the script
	# instance it's written in, regardless of which node's create_tween()
	# built the Tween, so a callback still written here would have stayed
	# bound to THIS node even though the tween itself outlives it.
	tw.tween_callback(Callable(player, "clear_camera_fog_override"))

func _finish() -> void:
	finished.emit()
