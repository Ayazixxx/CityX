extends CharacterBody3D
## Third-person controller: WASD movement + mouse look, with collision
## via move_and_slide against the world's static geometry. The camera
## rides a SpringArm3D boom so it pulls in automatically instead of
## clipping through walls.
##
## Multiplayer-ready: only the peer that owns this body (its multiplayer
## authority) reads input, moves it, or captures the mouse/camera. Other
## peers see this body's position/rotation, PLUS its current locomotion
## animation state (walking/running/crouching/direction -- see
## synced_is_walking and friends below), replicated via
## MultiplayerSynchronizer; discrete one-shot moves (jump, land, dash,
## punch, heavy_punch, block, crouch transitions) replicate via a matching
## _rpc_* wrapper next to each real call site below, sent authority ->
## everyone else only (see each one's own "authority"/"call_remote" rpc
## annotation) since the authority's own machine already played the real
## call directly. With no multiplayer session active (today's only actual
## mode when Net.is_networked() is false), is_multiplayer_authority() is
## true for every node by default and every _rpc_* call is skipped (see
## each call site's own Net.is_networked() guard), so this behaves exactly
## as before.
##
## Still NOT synced, same as before: health/damage/death (see
## take_damage() -- still applied purely locally by whichever peer's own
## raycast happened to land the hit, not the actual target's own
## authoritative copy) -- a real fight still doesn't agree between two
## players' screens about who's actually still standing. That's the next
## piece, not this one.

const CharacterSave = preload("res://character/character_save.gd")

const SPEED := 4.0
const SPRINT_SPEED := 7.5
const JUMP_VELOCITY := 4.5
const MOUSE_SENSITIVITY := 0.0025
const GRAVITY := 9.8
# How often accumulated playtime gets flushed to disk (see save_character_id
# and _flush_playtime) -- frequent enough that a crash or hard kill (not a
# clean quit, see _notification's own WM_CLOSE_REQUEST flush) only ever
# loses a few seconds, infrequent enough that this isn't writing the whole
# characters.cfg file every physics frame.
const PLAYTIME_SAVE_INTERVAL_SECONDS := 30.0
# Dev-only (see set_fly_mode(), toggled via the dev console's own "/fly" --
# ui/dev_console.gd) -- vertical speed while flying; horizontal movement
# while flying reuses the ordinary SPEED/SPRINT_SPEED logic unchanged below,
# only the vertical axis (jump=up, crouch=down) is fly-specific.
const FLY_SPEED := 8.0

# Aim-down-sights zoom while a gun is equipped and right-click is held (see
# _equipped_gun/_is_aiming) -- a real camera FOV narrowing, not just a
# cosmetic overlay, which is also what actually tightens weapons/pistol.gd's
# own fire spread (see its ADS_SPREAD_DEGREES vs HIP_FIRE_SPREAD_DEGREES).
# Lerped (not snapped) toward the target each frame for a smooth zoom
# transition -- see _default_fov, captured from the camera's own configured
# value in _ready() rather than hardcoded, so this works regardless of
# whatever FOV the camera scene actually specifies.
const ADS_FOV_DEGREES := 50.0
const ADS_FOV_LERP_SPEED := 10.0

# Fortnite-style over-the-shoulder offset -- shifts the SPRING ARM sideways
# (its own local +X, which is always "camera-right" regardless of mouse
# look, since it's a child of the rotating CameraPivot) so the character's
# own body doesn't block the view, and the crosshair sits off to one side of
# the character instead of dead-center behind their head. Applied
# UNCONDITIONALLY now (originally gun-only -- see the removed comment
# history -- widened to a real ask for the whole game's own default third-
# person framing, "more over the shoulder like fortnite"). Aim is
# unaffected: the raycast direction is still whatever the crosshair/camera
# is actually pointing at (see _try_fire_gun), so shots land under the
# reticle regardless of this offset, the same way a real over-the-shoulder
# shooter camera works.
#
# MUST be applied to the SpringArm3D's own position, not Camera3D's (tried
# first, and confirmed as a real bug: SpringArm3D resets its direct
# children's local transform every frame as part of how it enforces
# spring_length/collision, silently discarding any manual position offset
# set on Camera3D before it ever rendered -- the shoulder offset never
# visibly appeared at all). Offsetting the spring arm ITSELF sidesteps that
# entirely, since the arm's own position is what SpringArm3D's collision
# logic starts from, not something it resets out from under itself.
const SHOULDER_OFFSET_X := 0.5
const SHOULDER_OFFSET_LERP_SPEED := 8.0
# A separate, more pronounced offset specifically while aiming down sights
# with a gun (real ask: the everyday SHOULDER_OFFSET_X above is the "normal"
# pov, but ADS should zoom into ITS OWN more over-the-shoulder pose, then
# settle back to SHOULDER_OFFSET_X the instant aim is released) -- measured
# via the same /camera unlock + Remote tab + /camera pose workflow the
# pistol/bottle/eyes already used, not guessed.
const ADS_SHOULDER_OFFSET_X := 1.415

# Same manual-tuning idiom as every held weapon's own set_manual_override
# (see pistol.gd/glass_bottle.gd) applied to the camera itself: while true,
# _process's own per-frame lerp toward SHOULDER_OFFSET_X AND the ADS zoom
# lerp toward ADS_FOV_DEGREES/_default_fov (both below) are skipped entirely,
# freeing _spring_arm's own Position (the shoulder offset/POV) AND Camera3D's
# own Position/rotation/fov (angle + the aim "scope" zoom level) to be
# dragged/typed directly in the Debugger panel's own Remote tab
# (Player/CameraPivot/SpringArm3D, or Camera3D one level deeper) without
# every frame snapping any of it straight back. See /camera unlock in
# ui/dev_console.gd.
var _camera_manual_override := false

func set_camera_manual_override(enabled: bool) -> void:
	_camera_manual_override = enabled

## Prints SpringArm3D's own Position (X is the shoulder offset SHOULDER_OFFSET_X
## copies from) and Camera3D's own Position/rotation/fov -- copy whichever
## axis was actually changed back into the matching const by hand, same
## "print the final numbers for a human to copy" role every other weapon's
## own print_current_pose() already fills.
func print_camera_pose() -> void:
	print("SpringArm3D position = ", _spring_arm.position, " spring_length = ", _spring_arm.spring_length)
	print("Camera3D position = ", camera.position, " rotation (degrees) = ", camera.rotation_degrees, " fov = ", camera.fov)

## Proximity voice chat. Two distinct roles run on the SAME VoicePlayer
## (AudioStreamPlayer3D, see player.tscn) node depending on which peer's
## screen this is: on the SPEAKING peer's own machine, this Player instance
## is the local/authority one, so _process_voice_capture (see there) reads
## their own real microphone and transmits it; on EVERY OTHER peer's
## machine, this exact same Player node is a non-authority remote copy sitting
## at that speaking peer's own replicated position, so _rpc_voice_chunk just
## pushes the received audio into ITS OWN VoicePlayer -- proximity/distance
## falloff (see VoicePlayer's own max_distance/unit_size in player.tscn)
## comes entirely for free from Godot's normal 3D audio attenuation, the
## same way it would for any other positional sound in the game, no extra
## distance math needed here at all.
##
## Sent as raw 16-bit PCM mono over an UNRELIABLE rpc (see
## _rpc_voice_chunk) -- real-time audio tolerates the occasional dropped
## chunk far better than the added latency reliable delivery/retries would
## cost, the same tradeoff every real voice chat system makes. This is a
## genuinely more experimental piece than anything else in this project:
## it can't be verified by ear the way visual/gameplay work can, and
## streaming raw audio over Godot's high-level RPC system (built for
## occasional state changes, not a continuous stream) is a non-standard use
## of it -- real latency/choppiness under real network conditions is
## untested.
const VOICE_SEND_INTERVAL_SECONDS := 0.05 # 20 chunks/sec
const MAX_PITCH_DEGREES := 89.0
# A building's own interior staircases (see city_chunk.gd's _add_staircase)
# are a single sloped ramp, not stepped collision -- and a compact
# footprint often forces that ramp steeper than a real-world staircase
# (up to ~70-80 degrees on the smallest buildings/tallest single flights).
# CharacterBody3D's own default (45 degrees) would read that as a wall
# instead of floor, stopping the player cold at the base of the stairs.
const FLOOR_MAX_ANGLE_DEGREES := 80.0

const CityChunk = preload("res://city/city_chunk.gd")

const MAX_HEALTH := 100.0
# 1.3x the original 100 pool -- same drain rate as before, so a full bar now
# sprints for 6.5s instead of 5s, letting sprints run noticeably
# longer/farther before exhaustion without changing how fast stamina drains
# moment to moment.
const MAX_STAMINA := 130.0
const STAMINA_DRAIN_PER_SEC := 20.0
# 1.3x the original 12/sec -- refills noticeably faster after a sprint.
const STAMINA_REGEN_PER_SEC := 15.6
# Once stamina fully runs out, sprinting is locked out until it recovers back
# up to this fraction of max -- without a threshold, hovering right at 0
# stamina (draining and regenerating by tiny amounts frame to frame) would
# flicker sprint speed on and off constantly instead of cleanly cutting out.
const STAMINA_EXHAUSTED_RECOVER_FRACTION := 0.2

# Level/stat-point system: 5 stat points every level, 1 skill point every 2
# levels (a real reported ask) -- XP comes from killing enemies (see
# add_xp(), called by whichever NPC's own _die() grants it, e.g.
# npc/wolf.gd's own XP_REWARD); quests are a stated future XP source, not
# implemented yet. xp_to_next_level (see its own var comment) is
# XP_PER_LEVEL_BASE * level -- deliberately simple/linear, a first-pass
# curve that's easy to retune later since every caller only ever goes
# through add_xp()/xp_to_next_level, never a hardcoded number.
const STAT_POINTS_PER_LEVEL := 5
const SKILL_POINT_EVERY_N_LEVELS := 2
const XP_PER_LEVEL_BASE := 100.0

# Each of the 5 stats' own effect per point spent -- Health/Stamina/Mana are
# flat/additive (applied in _recompute_stats()), Strength/Speed are
# percentage-based instead (applied at their own single usage chokepoint --
# see apply_strength() and _physics_process's own speed calc). Percentage,
# not flat, for those two specifically: a flat bonus per point would get
# wildly unbalanced at high point totals (a level-50 character could have
# 245 points sunk into one stat), where a percentage keeps growth
# proportional/self-limiting, the way it already reads in most RPGs. All 5
# are placeholder values, not balance-tested -- easy to retune, each is read
# from exactly one place.
const HEALTH_PER_STAT_POINT := 10.0
const STAMINA_PER_STAT_POINT := 10.0
const MANA_PER_STAT_POINT := 10.0
const STRENGTH_DAMAGE_PERCENT_PER_POINT := 0.02
const SPEED_PERCENT_PER_POINT := 0.01

# Double-tap Shift (the same physical key "sprint" is already bound to, see
# project.godot -- no separate "dash" action) triggers a short directional
# burst instead of the ordinary hold-to-sprint behavior. A press-release-
# press within this window counts as the double-tap; a single press (or a
# held sprint) never crosses it, since the timer only starts counting down
# from the FIRST press and gets consumed/reset the moment a second one
# lands. 0.3s is a standard double-tap window -- generous enough to catch a
# deliberate double-tap without also firing on two separate, unrelated taps.
const DASH_DOUBLE_TAP_WINDOW_SECONDS := 0.3
# Faster than even a sprint (SPRINT_SPEED) -- a dash should read as a
# distinct, snappier burst, not just "sprint but for a moment." Doubled from
# an original 12.0 (rather than doubling DASH_DURATION_SECONDS instead) so
# covering twice the distance keeps reading as a quicker, more aggressive
# lunge -- stretching the duration instead would have made it a longer
# sustained boost, a different feel than what was asked for.
const DASH_SPEED := 24.0
# How long the dash's own forced velocity overrides normal WASD-driven
# movement -- short enough to read as an instant lunge rather than a
# sustained speed boost. Covers DASH_SPEED * DASH_DURATION_SECONDS = 4.8m.
const DASH_DURATION_SECONDS := 0.2
# Flat 25% of the max bar (not whatever the CURRENT stamina happens to be) --
# same "percentage of the pool" framing MAX_HEALTH/PUNCH_DAMAGE already use
# elsewhere in this file.
const DASH_STAMINA_COST := MAX_STAMINA * 0.25

# Wall climbing (see _try_start_climb/_process_climbing) -- hold sprint+W
# against a wall to grab on, W/S to climb up/down once attached, Space to
# hop off at any time, or just run out of stamina and fall.
const CLIMB_SPEED := 2.5
# How far in front to raycast for a grabbable wall -- short and deliberate
# (roughly arm's reach), so climbing never triggers from just being
# generally near a building, only from actually walking straight into one.
const CLIMB_GRAB_CHECK_DISTANCE := 1.0
# A hit surface within this far of pure horizontal (dot with world UP) is
# rejected as "not a real wall" -- a shallow roof slope or a floor/ceiling
# shouldn't be climbable just because it happens to be in front of the
# player when the check fires.
const CLIMB_WALL_MAX_SLOPE_DOT := 0.3
# How high above the player's own current grip to check for the wall still
# being there -- reusing this same forward raycast (now from a point this
# far up) is what detects "reached the top edge" while climbing up: once
# it stops finding the wall, the climb has run out of surface to grip.
const CLIMB_TOP_CHECK_HEIGHT := 1.8
# The camera pivot's own authored height in player.tscn, for a
# default-height character -- scaled by the character's own height
# multiplier in sync_camera_to_character_height(). Duplicated as a const
# here rather than read off the node at _ready(), since the pivot has
# usually already been moved by the time anything wants the ORIGINAL value
# back.
const BASE_CAMERA_PIVOT_HEIGHT := 1.6
# Outward push (away from the wall) + upward pop applied when hopping off
# with Space -- enough to clear the wall's own collision before gravity
# takes back over, reading as a deliberate push-off rather than just
# letting go.
const CLIMB_HOP_OFF_OUTWARD_SPEED := 3.5
const CLIMB_HOP_OFF_UPWARD_SPEED := 2.0
# How far up/forward (toward the wall, i.e. onto the ledge) the real
# physics body relocates once climb_top_out's own one-shot mantle
# animation finishes -- a starting estimate, corrected immediately by a
# real downward raycast (see _complete_climb_top_out) rather than trusted
# blindly, since actual roof/ledge height varies by building.
const CLIMB_MANTLE_UP := 0.4
const CLIMB_MANTLE_FORWARD := 0.6

# Minecraft-style fall damage: falls up to SAFE_FALL_DISTANCE are free, and
# every meter past that costs a fixed fraction of max health. Two full
# staircases' worth of height (see city_chunk.gd's own NOMINAL_FLOOR_METERS
# * STORY_HEIGHT_FLOORS, the actual height one climbable interior story
# spans), not a flat meters constant, so "how far can I fall safely" always
# tracks whatever a building's own story height actually is in the
# generated city, even if that changes later -- falling off the top of a
# single flight of stairs (one story) should never hurt on its own.
#
# 8% max health per meter beyond that (steeper than Minecraft's real 1-of-20-
# HP-per-block proportion, which only worked out to ~64 damage -- survivable
# -- off a 4-story roof) guarantees jumping off any building city_chunk.gd
# would call 4 stories tall is lethal: city_chunk.gd's own num_floors is
# floor(building_height / story_height), so the SHORTEST roof that still
# counts as "4 stories" sits at exactly 4 story-heights up, and
# (4 - 2 safe stories) * story height * this rate must clear MAX_HEALTH.
const SAFE_FALL_DISTANCE := CityChunk.NOMINAL_FLOOR_METERS * CityChunk.STORY_HEIGHT_FLOORS * 2.0
const FALL_DAMAGE_PER_METER := MAX_HEALTH * 0.08

# Bedrock-style unarmed combat: click, swing, short fixed cooldown -- no
# charge-up and no reward for waiting past the cooldown before clicking
# again (unlike Java's post-1.9 attack-strength system), just "can I punch
# again yet." Slightly longer than PUNCH_ANIM_LENGTH (see character.gd) so
# one swing's own recovery finishes before the next can start, rather than
# cutting itself off mid-animation.
const PUNCH_COOLDOWN_SECONDS := 0.35
# A bit more than 2 blocks' reach -- close enough that you have to actually
# be next to whatever you're hitting, same spirit as Minecraft's own melee
# range without copying its exact 3-block number (this is a much smaller,
# denser city, not open terrain).
const PUNCH_RANGE := 2.5
# Exactly 10% of MAX_HEALTH -- every punchable target (Player, npc/cpu.gd)
# shares the same 100-point health pool, so a flat constant here already IS
# a percentage; a real weapon/skill-tree system would replace this with a
# proper source of truth later.
const PUNCH_DAMAGE := 10.0
# Roughly chest height (matches CameraPivot's own 1.6, close to eye level) --
# where the punch's own hit-detection ray starts, measured from the player's
# feet (global_position), NOT from the camera (see _punch's own comment for
# why that would break range entirely).
const PUNCH_ORIGIN_HEIGHT := 1.5
# Every 3rd punch is a left jab, the rest are right jabs (1=R, 2=R, 3=L,
# 4=R, 5=R, 6=L, ...) -- see _punch's own use of _punch_count. Combat mode's
# own timeout (when the boxing stance drops back to a normal idle after no
# punches) is owned entirely by character.gd (see its own
# COMBAT_MODE_TIMEOUT_SECONDS) rather than tracked here too -- this class
# only needs to decide WHICH hand throws each punch.
const PUNCH_LEFT_HAND_EVERY_NTH := 3
# A quick tap (press then release before this) throws the regular jab,
# exactly as before -- holding PAST this before releasing throws a heavy
# punch instead (see _physics_process's own press/hold/release tracking).
# 0.4s: long enough that an ordinary quick click (typically well under
# 150ms between press and release) never accidentally rolls a heavy punch,
# short enough that a deliberate hold doesn't feel like a laggy wait.
const HEAVY_PUNCH_HOLD_THRESHOLD_SECONDS := 0.4
# 1.5x the regular jab -- a slower, more committed haymaker (see
# character.gd's HeavyPunchAnim) in exchange for hitting harder, the
# standard risk/reward a "hold for a heavy attack" mechanic is built around.
const HEAVY_PUNCH_DAMAGE := PUNCH_DAMAGE * 1.5

# Heavier pacing than a bare jab (PUNCH_COOLDOWN_SECONDS, 0.35) -- a bottle
# swing should read as a more committed, weightier action than a quick fist
# jab, matching how HEAVY_PUNCH_DAMAGE already trades speed for damage.
const BOTTLE_MELEE_COOLDOWN_SECONDS := 0.5
# m/s along the aim direction -- RigidBody3D's own gravity gives the arc for
# free, no separate "throw angle" tuning needed.
# --- Grappling hook (see items/grappling_hook.gd, which is only the
# launcher + rope visual -- the swing itself is a movement MODE and so
# lives here, next to velocity and move_and_slide()). ---
# How far the hook can reach. Generous on purpose: the city's own street
# grid puts facades ~24m apart (see city_chunk.gd's own chunk size), and a
# hook that can't cross a street can't swing between buildings at all.
const GRAPPLE_MAX_RANGE := 45.0
# The rope can't be reeled shorter than this, so you can't wind yourself
# into the wall you're anchored to.
const GRAPPLE_MIN_ROPE_LENGTH := 2.5
const GRAPPLE_MAX_ROPE_LENGTH := 60.0
# Air control while swinging -- what lets you PUMP a swing to build height
# instead of just hanging there decaying into a pendulum at rest.
const GRAPPLE_SWING_ACCEL := 16.0
const GRAPPLE_REEL_SPEED := 9.0
const GRAPPLE_PAYOUT_SPEED := 7.0
# Bled off per constraint-frame. Small, but without ANY loss a frictionless
# pendulum swings forever and every landing overshoots.
const GRAPPLE_ROPE_DAMPING := 0.015
# Upward kick on release, so letting go at the top of an arc converts into
# real height rather than immediately dropping.
const GRAPPLE_RELEASE_BOOST := 3.5
# How long the player may stay grounded mid-swing before the rope cuts on
# its own (see _process_grapple_swing). Long enough that briefly touching
# down while skimming a rooftop keeps the swing alive, short enough that
# actually landing ends it before it reads as being stuck.
const GRAPPLE_MAX_GROUND_SECONDS := 0.35
# The pose held for the whole swing.
#
# HARD REQUIREMENT: this clip must not carry a rotation track on bone 0
# (the hips, the rig's root). A root rotation re-orients the WHOLE body,
# and since the swing holds one clip for its entire duration, whatever
# angle that track writes is the angle you swing at.
#
# This used to be "falling", which was the obvious pick (it's the closest
# thing to hanging in mid-air) and was fine until falling.tres was re-baked
# with include_root_rotation=true for the intro cutscene's own dive. That
# re-bake put an 81-degree pitch on bone 0 -- so the grapple silently
# started swinging the player flat on their face. Reported twice as
# "grappling hook leaves me sideways"; the first fix attempt (resetting the
# hips on RELEASE) could never have worked, because the clip loops and
# rewrites that bone every frame it keeps playing.
#
# "falling" is the ONLY clip in the whole library with a non-zero root
# tilt -- every other one measures 0.0 degrees -- so almost anything else
# is safe. "jump" is an upright airborne pose and doesn't loop, so it
# settles on its final frame and holds there for the rest of the swing.
# tests/test_grappling_hook_check.gd asserts the zero-tilt property
# directly, so a future re-bake can't reintroduce this quietly.
const GRAPPLE_SWING_ANIMATION := "jump"
const BOTTLE_THROW_SPEED := 16.0
# Thrown a little harder than a bottle -- a grenade is meant to be lobbed
# onto a target several metres off, and an underthrown one landing at your
# own feet with a lit fuse is a bad kind of surprise.
const GRENADE_THROW_SPEED := 18.0

# Right-click, timed, not holdable -- there's no separate "start/stop
# blocking" pair the way crouch has (start_crouch/end_crouch): a single
# press plays character.gd's one-shot block animation once, and the block
# window (see take_damage's own is_blocking() check) is exactly as long as
# that clip plays, ~0.7s (see character/animations/block.tres, baked by
# tests/scratch_mixamo_bake.gd's own BODY_BLOCK_WINDOW). Holding right-click
# past that does nothing extra -- the window doesn't extend, matching "you
# can't hold block, you have to time it."
const BLOCK_DAMAGE_MULTIPLIER := 0.25 # 75% LESS damage, i.e. only 25% gets through
# How long a successful block leaves the ATTACKER unable to move or throw
# another punch -- the actual reward for timing a block right (see
# take_damage/stun): without this, "block" would just be a worse dodge
# (still eating 25% of the hit) instead of a real punish opportunity.
const BLOCK_STUN_SECONDS := 1.0

# How long a LANDED (not blocked) unarmed hit locks the DEFENDER out of
# movement/offense -- the new Naruto-Storm-style "you're locked into taking
# the rest of the combo unless you block or dodge out" mechanic. Deliberately
# a SEPARATE variable from _stun_remaining above (which only ever punishes an
# ATTACKER who got blocked) rather than reusing it: _stun_remaining already
# gates block input itself (see _physics_process), and reusing it here would
# also lock out the very escape this mechanic is supposed to leave open.
# Scaled per-stage by combo_data.gd's own hitstun_scale (see
# character.gd's get_last_attack_combo_info()); base value tuned to roughly
# bridge one combo stage's own "window" (see combo_data.gd) so a follow-up
# hit lands right as this expires, the classic combo-timing feel.
const HITSTUN_SECONDS := 0.5
# How long a dash's own burst grants invulnerability -- exactly the burst
# window itself (not dash_backward's longer visual follow-through tail),
# matching "you dodged through/past the hit for the burst's duration," not
# "the whole visible recovery pose is also invulnerable."
const DASH_INVULNERABILITY_SECONDS := DASH_DURATION_SECONDS
# The combo finisher (5th unarmed hit, see combo_data.gd's own "finisher"
# flag): the defender ragdolls and flies back, then stands back up on their
# own -- see _begin_finisher_ragdoll/_end_finisher_ragdoll. Not the death/
# respawn ragdoll cycle (character.gd's ragdoll()/reset_ragdoll() are reused
# as-is, just orchestrated with a new timer here instead of ending in
# _die()/respawn()).
const FINISHER_RAGDOLL_SECONDS := 1.5
# Extra invulnerability held past the ragdoll's own flight, on top of it
# (see _invulnerable_remaining) -- specifically to prevent a chain-into-chain
# infinite lock: without this, the instant _end_finisher_ragdoll() hands
# control back, a still-nearby attacker could immediately land the first hit
# of a brand new combo before the defender has had any real chance to react.
const POST_FINISHER_INVULN_SECONDS := 1.0
const FINISHER_KNOCKBACK_SPEED := 9.0
const FINISHER_KNOCKBACK_UP_SPEED := 4.0
# A subtler, permanent sibling of FINISHER_KNOCKBACK_SPEED/_UP_SPEED above --
# for an ordinary (non-finisher) killing blow, see _apply_death_knockback.
# Meant to read as "reacting to the hit," not a launch, so it's well below
# the finisher's own dramatic values.
const DEATH_KNOCKBACK_SPEED := 3.5
const DEATH_KNOCKBACK_UP_SPEED := 1.5
# Caps how much of a lethal fall's own real impact speed actually reaches
# the ragdoll (see _apply_fall_damage's own comment) -- a real reported bug:
# feeding the RAW impact speed from a tall building fall (~20+ m/s) into a
# ragdoll rig that's simultaneously colliding with the ground it just
# landed on caused the whole rig to explode into a runaway spin (confirmed
# directly via a headless repro: angular velocities in the THOUSANDS of
# rad/s, bodies flying dozens of meters away, never settling -- the
# ConeTwistJoint3D chain can't resolve that much sudden shared velocity
# against a simultaneous ground collision in one physics step). Empirically
# re-tested from 6 up to 50 m/s of raw impact speed (see this session's own
# scratch repro, not committed): everything up to ~12 m/s stayed stable
# every time, instability started appearing unpredictably at 13+, so this
# sits with a comfortable safety margin below that rather than chasing the
# exact knife-edge value. Still clearly reads as "hit the ground hard" --
# just not literal terminal velocity.
const MAX_DEATH_FALL_IMPACT_SPEED := 10.0

# How long the looping outdoor city ambience (see city_ambience_player, and
# enter_building()/exit_building() below) takes to fade OUT entering a
# building, and (see _ready()'s own initial call) to fade IN the very first
# time it ever starts playing at all -- same duration title_screen.gd's own
# RAIN_FADE_OUT_SECONDS already established as this project's convention for
# this kind of slower, atmospheric transition, reused directly.
const CITY_AMBIENCE_SLOW_FADE_SECONDS := 1.5
# How long it takes to fade back IN leaving a building specifically -- a
# real explicit ask ("make it fade in quicker upon leaving indoors"):
# stepping back out onto the street should feel snappier/more immediate
# than either of the slower transitions above.
const CITY_AMBIENCE_FAST_FADE_SECONDS := 0.6
# The "fully outdoors" target volume -- NOT unity gain (0.0 dB). A real
# explicit ask ("lower the ambience volume by 30%"): 20*log10(0.7) ==
# -3.098 dB is what actually corresponds to 70% of the original LINEAR
# amplitude (a straight -30% off 0.0 dB, e.g. -0.3, would be a much smaller
# perceived change -- dB is already logarithmic, so percentages don't
# subtract directly).
const CITY_AMBIENCE_MAX_VOLUME_DB := -3.1

# How far the world's ambient light drops while indoors (see
# _fade_indoor_darkness / city/day_night_cycle.gd's own set_indoor_dim).
# Not 0.0 on purpose: pitch black with no light source at all would leave a
# player who wandered in without a flashlight stuck in a room they can't
# even find the door of. This keeps enough of a silhouette to navigate by
# while making a real light source (items/flashlight.gd) clearly worth
# carrying -- especially in the windowless basements, which have no
# daylight of their own reaching them at all.
const INDOOR_AMBIENT_DIM := 0.18
const INDOOR_DIM_FADE_SECONDS := 0.8

# Naruto-Storm-style lock-on camera (see _update_lock_on) -- how far a target
# can be and still be a valid lock-on candidate (acquisition AND while
# already locked, with a little slack -- see _update_lock_on's own *1.15).
const LOCK_ON_RANGE := 20.0
# Acquisition only (cycling through already-in-range candidates ignores
# this) -- how far off crosshair-center a candidate can be and still be
# picked by a fresh C-press, so locking on always grabs whoever you're
# actually looking toward, not just whoever's nearest regardless of facing.
const LOCK_ON_MAX_ANGLE_DEGREES := 65.0
# Same lerp-toward-target-every-frame idiom as ADS_FOV_LERP_SPEED/
# SHOULDER_OFFSET_LERP_SPEED above -- how fast the camera swings to face a
# newly (re)acquired target, not an instant snap.
const LOCK_ON_CAMERA_LERP_SPEED := 10.0
# How long a lock-on target can be line-of-sight-blocked (e.g. ducking
# behind a wall corner mid-fight) before the lock actually releases --
# forgiving brief occlusion rather than dropping lock the instant a single
# raycast happens to clip scenery.
const LOCK_ON_LOS_GRACE_SECONDS := 0.75
# _update_lock_on's own pitch is atan2(to_target.y, horiz_dist) -- against a
# TALL target (e.g. a hovering monster) at close range, horiz_dist shrinks
# toward 0 while to_target.y stays large, so that ratio spikes toward
# straight up. Since camera_pivot.rotation.x pitching up swings the
# SpringArm3D's camera end DOWN AND UNDERNEATH the pivot (it revolves around
# the pivot point, it doesn't just tilt in place), an uncapped pitch there
# put the camera on the ground looking almost straight up at the player's
# underside (confirmed via a real screenshot: buildings looming overhead,
# camera below the character's own feet). A separate, much tighter cap than
# MAX_PITCH_DEGREES (free-look's own 89-degree ceiling) keeps the lock-on
# camera in a normal, elevated viewing arc regardless of target height/
# distance.
const LOCK_ON_MAX_PITCH_DEGREES := 35.0
# Floor under horiz_dist specifically for the pitch calc above -- without
# this, closing to melee range against a tall target alone (independent of
# the cap above) already sends the raw atan2 ratio climbing steeply before
# clamping even engages, which read as the camera visibly diving as you
# closed distance rather than settling smoothly.
const LOCK_ON_MIN_PITCH_HORIZ_DIST := 2.0

# Landing a punch (not just throwing one -- a whiff stays silent) plays one
# of these at random, picked fresh each hit so spamming punches doesn't
# loop the exact same sample over and over.
const PunchImpactSounds := [
	preload("res://player/assets/audio/punch_impact_1.mp3"),
	preload("res://player/assets/audio/punch_impact_2.mp3"),
	preload("res://player/assets/audio/punch_impact_3.mp3"),
]
# Same "pick one at random per use" idea as PunchImpactSounds above -- played
# once per dash that actually fires (see _try_start_dash), not on a blocked
# double-tap (insufficient stamina/already dashing), matching how a whiffed
# punch above also stays silent.
const DashSounds := [
	preload("res://player/assets/audio/dash_1.mp3"),
	preload("res://player/assets/audio/dash_2.mp3"),
	preload("res://player/assets/audio/dash_3.mp3"),
]

const PauseMenuScene := preload("res://ui/pause_menu.tscn")
const HudScene := preload("res://ui/hud.tscn")
const InventoryPanelScene := preload("res://ui/inventory_panel.tscn")
const MapPanelScene := preload("res://ui/map_panel.tscn")
const IntroCutsceneScript := preload("res://player/intro_cutscene.gd")
const ClaimCutsceneScript := preload("res://player/claim_cutscene.gd")
const ClaimBannerScene := preload("res://ui/claim_banner.tscn")

const BuildingAcquiredAudio := preload("res://player/assets/audio/building_acquired.mp3")
## Full volume: this is the reward stinger for the whole feature, not a
## background layer. (The intro cutscene's own landing thud sits at 0.3 by
## explicit request -- that was about a specific sound being too loud, not a
## house level to match.)
const CLAIM_SOUND_VOLUME_LINEAR := 1.0

const CLAIM_SUCCESS_TEXT := "BUILDING ACQUIRED"
const CLAIM_FAILURE_TEXT := "YOU MUST BE INSIDE A BUILDING"
const CLAIM_ALREADY_OWNED_TEXT := "YOU ALREADY OWN THIS BUILDING"
const DevConsoleScene := preload("res://ui/dev_console.tscn")
const ChatBoxScene := preload("res://ui/chat_box.tscn")
const SettingsPanel = preload("res://ui/settings_panel.gd")
const ThrownBottleScene := preload("res://weapons/thrown_bottle.tscn")
const ThrownGrenadeScene := preload("res://weapons/thrown_grenade.tscn")
const BloodSplatter = preload("res://weapons/blood_splatter.gd")
const RainEffectScene := preload("res://player/rain_effect.tscn")
const ItemRegistry = preload("res://items/item_registry.gd")

signal died
signal respawned
## Fired the moment the intro cutscene hands control back (see
## _on_intro_cutscene_finished). main.gd waits on this to place anything
## that must NOT appear in the cutscene's own shots -- the fall camera and
## the looking_around orbit between them sweep most of the spawn area, so
## "far enough away not to be seen" isn't reliably achievable by placement
## alone. Never fires for a loaded character, who has no cutscene at all.
signal intro_cutscene_finished
## Fired when a building is successfully claimed (see try_claim_building),
## carrying that building's own world-space footprint centre (at ground
## level) and full size. npc/old_guy.gd waits on this to turn up outside
## whichever building you took -- the size is handed over rather than left
## for the listener to re-derive, since the claim already worked it out off
## the building's own trigger volume.
signal building_claimed(center: Vector3, size: Vector3)

@onready var camera_pivot: Node3D = $CameraPivot
@onready var _spring_arm: SpringArm3D = $CameraPivot/SpringArm3D
@onready var camera: Camera3D = $CameraPivot/SpringArm3D/Camera3D
@onready var multiplayer_sync: MultiplayerSynchronizer = $MultiplayerSynchronizer
@onready var fall_impact_player: AudioStreamPlayer = $FallImpactPlayer
@onready var walk_sound_player: AudioStreamPlayer = $WalkSoundPlayer
@onready var run_sound_player: AudioStreamPlayer = $RunSoundPlayer
@onready var punch_impact_player: AudioStreamPlayer = $PunchImpactPlayer
@onready var dash_player: AudioStreamPlayer = $DashPlayer
@onready var voice_player: AudioStreamPlayer3D = $VoicePlayer
@onready var city_ambience_player: AudioStreamPlayer = $CityAmbiencePlayer

# Current effective max -- MAX_HEALTH/MAX_STAMINA above stay fixed at their
# own original values (matching every existing test that reads them
# directly, e.g. tests/test_stats_check.gd's own _player.MAX_HEALTH
# comparisons -- a fresh, 0-stat-point character's max_health/max_stamina
# always equals MAX_HEALTH/MAX_STAMINA exactly, so none of those needed to
# change), recomputed by _recompute_stats() whenever a stat point is spent,
# or set directly in _ready() from a loaded save's own stat_health/
# stat_stamina/stat_mana (see _ready()'s own comment).
var max_health := MAX_HEALTH
var max_stamina := MAX_STAMINA
var max_mana := 0.0
var health := max_health
var stamina := max_stamina
var mana := 0.0

# Real leveling now (see add_xp()) -- was a static placeholder ("just a
# number the HUD displays") before this. xp_to_next_level is
# XP_PER_LEVEL_BASE * level (see that const's own comment), recomputed
# whenever level changes so nothing else ever needs to re-derive the curve
# itself. stat_health/stat_strength/stat_stamina/stat_mana/stat_speed are
# how many of stat_points have been permanently spent on each of the 5
# stats (see spend_stat_point()) -- persisted via character_save.gd's own
# update_progress(), loaded back by main.gd threading a saved character's
# own values onto this node before _ready() runs (see character_save.gd's
# own class comment on that ordering).
var level := 1
var xp := 0.0
var xp_to_next_level := XP_PER_LEVEL_BASE
var stat_points := 0
var skill_points := 0
var stat_health := 0
var stat_strength := 0
var stat_stamina := 0
var stat_mana := 0
var stat_speed := 0

var is_dead := false

# Which saved character (see character/character_save.gd) this session's
# playtime should accumulate onto -- set by main.gd right after instantiating
# this player, from whichever id was actually involved (a freshly created
# character's own new id, or an already-saved one just picked in
# character_select.gd). -1 (the default) means "don't track playtime at
# all" -- covers remote multiplayer peers' own Player copies on THIS
# screen, which have no local save data of their own to update.
var save_character_id := -1
# Overridable so tests can point this at a throwaway ConfigFile instead of
# the real user://characters.cfg -- same reasoning as main.gd's own
# character_save_path. Must be set (alongside save_character_id) before
# this node's first _physics_process runs.
var character_save_path: String = CharacterSave.SAVE_PATH
# Seconds accumulated since the last flush to disk -- see
# PLAYTIME_SAVE_INTERVAL_SECONDS and _flush_playtime.
var _playtime_since_save := 0.0

var _pitch := 0.0
var _character: Node = null
var _stamina_exhausted := false
var _punch_cooldown_remaining := 0.0
# Counts every punch actually thrown (not clicks -- a click during cooldown
# never reaches _punch, so it never advances this) so PUNCH_LEFT_HAND_EVERY_NTH
# lands on real swings 3, 6, 9... regardless of how spammy the clicking was.
var _punch_count := 0
# Tracks a currently-held punch press so release can tell a quick tap
# (regular jab) from a deliberate hold (heavy punch) -- see
# HEAVY_PUNCH_HOLD_THRESHOLD_SECONDS and _physics_process's own handling.
var _punch_held := false
var _punch_held_seconds := 0.0
# > 0 while stunned (see stun(), called on the ATTACKER by a defender's own
# successful block -- see take_damage) -- movement, jump, dash, punch, and
# block are all gated on this being <= 0.0 (see _physics_process), so a
# stunned player just stands there (still falls/lands normally, gravity and
# move_and_slide aren't touched) until it counts back down to zero. No
# stagger/flinch animation exists for this today, so visually a stunned
# player just holds whatever pose set_walking(false, ...) already shows for
# "standing still, not moving" -- not a dedicated reaction, just the honest
# result of every input being ignored for the duration.
var _stun_remaining := 0.0
# > 0 while the DEFENDER is locked into a landed combo hit (see
# HITSTUN_SECONDS's own comment) -- gates movement/jump/punch/gun input the
# same way _stun_remaining does, but DELIBERATELY does NOT gate block input
# or the dash-trigger (see _physics_process/_try_start_dash): those are the
# two named escapes, and gating them here would silently defeat the whole
# "unless you can block or dodge your way out of it" mechanic. Cleared to
# 0.0 the instant either escape actually fires (see character.block()'s own
# call site and _try_start_dash), not just left to tick down on its own --
# both are meant to actively cancel the lock, not merely survive it.
var _hitstun_remaining := 0.0
# > 0 while immune to take_damage() entirely (see its own early return) --
# shared by dash's i-frames (DASH_INVULNERABILITY_SECONDS) and the combo
# finisher's ragdoll-flight-plus-recovery-grace window
# (FINISHER_RAGDOLL_SECONDS + POST_FINISHER_INVULN_SECONDS). One shared
# timer rather than two separate flags since both mean the exact same thing
# to take_damage() -- "this hit whiffs, full stop" -- and maxf'ing a new
# source into whichever is already running (never a flat overwrite) means
# whichever grants the longer window always wins, the same "can't ever
# shorten a state already in progress" reasoning stun() already uses.
var _invulnerable_remaining := 0.0
# True for exactly FINISHER_RAGDOLL_SECONDS after taking a combo finisher
# hit -- see _begin_finisher_ragdoll/_end_finisher_ragdoll. While true,
# _physics_process early-returns before any of its own normal gravity/
# movement/combat handling (same "this body isn't doing anything itself
# right now, the visual rig runs its own physics" reasoning the is_dead
# early-return above already established), since character.gd's own real
# physics ragdoll is what's actually driving the visible body during this
# window.
var _is_finisher_ragdolled := false
var _finisher_ragdoll_remaining := 0.0
var _air_peak_y := 0.0
# Set once in _ready() (only for the local/owning peer, see there) -- see
# _input_blocked() for how typing suppresses WASD/
# combat input the same way _stun_remaining already does, and
# _on_chat_message_sent for what actually happens once a message is sent.
var _chat_box: Node = null
# How many cursor-driven menu panels are currently open (see
# ui/inventory_panel.gd). A COUNT rather than a bool so overlapping panels
# can't have one closing panel hand control back while another is still up
# -- there's only one such panel today, but pause_menu/dev_console can
# already layer over it.
#
# These panels deliberately do NOT pause the tree (a reported ask: pausing
# is wrong in singleplayer and outright broken in multiplayer, where it
# freezes this peer's own physics and RPC processing while everyone else
# keeps playing). So the world keeps simulating with the mouse released,
# and this counter is what stops the body from acting on raw
# Input.is_action_pressed() polling in the meantime -- see _input_blocked().
var _ui_capture_count := 0
# Was: add_child(HudScene.instantiate()) with no var capture -- kept here so
# start_intro_cutscene()/its own _on_intro_cutscene_finished() can hide/show
# it around the cutscene.
var _hud: CanvasLayer = null

# --- Voice chat state (see VOICE_SEND_INTERVAL_SECONDS's own comment) ---
var _voice_playback: AudioStreamGeneratorPlayback = null
var _voice_capture_effect: AudioEffectCapture = null
var _mic_player: AudioStreamPlayer = null
var _voice_send_timer := 0.0
# Dev-only (see set_fly_mode()) -- true while the /fly console command is
# active. Gravity and jump's own JUMP_VELOCITY logic are both bypassed
# entirely while this is on (see _physics_process), replaced with direct
# jump=up/crouch=down vertical velocity at FLY_SPEED.
var _fly_mode := false
# Dev-only, admin-gated (see ui/dev_console.gd's own _is_admin()) --
# true while /infinitehealth is active. Checked at the very top of
# take_damage(), same "early return before any state changes" position
# _invulnerable_remaining's own check already uses, so a hit whiffs
# entirely (no damage, no hit_reaction/block_impact, no hitstun) rather
# than just being reduced to 0 -- the difference matters for anything that
# reacts to BEING hit at all, not just to health actually dropping.
var _infinite_health := false
# Dev-only, admin-gated -- true while /infinitesprint is active. Checked
# in _update_stamina()'s own drain branch (see its own comment) so sprint
# never costs stamina, but regen still runs normally -- no reason to also
# suppress that, and leaving it alone means turning infinite sprint back
# off doesn't leave stamina in some artificially-frozen state.
var _infinite_stamina := false
# How many building-footprint Area3Ds (see city_chunk.gd's own
# _build_building) this player is CURRENTLY inside, not just a plain bool --
# two adjacent/overlapping footprints (e.g. a main building and its own
# wing massing) could otherwise let an unrelated "exited" from one building
# cancel a still-valid "entered" from another. Only ever touched on the
# local/authoritative copy (see enter_building()/exit_building()'s own
# is_multiplayer_authority() guard) -- a remote peer's own copy of another
# player has no reason to track or react to THEIR indoor/outdoor state.
# Every building footprint this body is currently inside, innermost LAST
# (see enter_building/exit_building). A stack rather than a plain counter
# because overlapping footprints must not cancel each other out AND because
# /claim needs to know which building is actually being stood in, not just
# that some building is. Entries may be null -- see enter_building's own
# optional parameter.
var _indoor_areas: Array[Node3D] = []
# The local, per-peer rain visual/audio effect (see player/rain_effect.gd's
# own class comment) -- only ever built for the local/authoritative player
# (see _ready()'s own instantiation), null on every remote peer's own copy.
# Toggled by ui/dev_console.gd's own "/weather rain"/"/weather clear".
var _rain_effect: Node3D = null
# Non-null while seated in (or mid-animation transitioning into/out of)
# EITHER seat of a car (see vehicles/car.gd's own _begin_enter/_begin_exit)
# -- gates ALL normal movement/gravity/combat input in _physics_process the
# same blanket way is_dead already does, since a seated PASSENGER shouldn't
# be able to walk/jump/punch any more than the driver should. Which specific
# car doesn't matter to player.gd itself -- only vehicles/car.gd reads this
# reference back (to know who to seat/unseat), player.gd only ever checks
# whether it's null.
var _seated_car: Node = null
# True for the whole intro fall cutscene (see start_intro_cutscene()/
# player/intro_cutscene.gd) -- same "own the whole frame" early-return-gate
# role _seated_car already plays for its own state.
var _in_cutscene := false
# The /claim reveal orbit (see start_claim_cutscene). Deliberately its OWN
# flag rather than reusing _in_cutscene: the two gate the same input, but
# _in_cutscene's exit path (_on_intro_cutscene_finished) restores intro-
# specific camera state, and having a second cutscene set a flag somebody
# else's teardown owns is exactly how a half-restored camera happens.
var _in_claim_cutscene := false
var _claim_cutscene: Node = null
var _claim_banner: CanvasLayer = null
# Where respawning puts this player once a building has been claimed --
# Vector3.INF until then, which respawn() reads as "use the world's own
# SpawnPoint" (see its own comment).
var _claimed_spawn_position := Vector3.INF
# The trigger area of the building already claimed, so re-typing /claim
# inside it says so instead of replaying the whole orbit.
var _claimed_building_area: Node3D = null
var _intro_cutscene: Node = null
var _cutscene_saved_spring_length := 0.0
# Set by weapons/pistol.gd's own _equip() via equip_gun() -- non-null means
# left/right-click/reload are all redirected to firing/aiming/reloading
# instead of punching/blocking (see _physics_process). No "unequip" path
# exists yet (matches every sword's own equip-only, no-drop-mechanic gap)
# EXCEPT throwing a bottle (see _equipped_bottle's own comment), which is
# the first real unequip this project has -- equip_gun()/equip_bottle() are
# what keep this and _equipped_bottle from ever going stale against each
# other (a confirmed real pre-existing gap: nothing used to clear
# _equipped_gun back to null when a sword was equipped afterward).
var _equipped_gun: Node = null
# Set by weapons/glass_bottle.gd's own _equip() via equip_bottle() --
# non-null means right-click held ("_is_aiming", same var the gun branch
# already drives) + punch triggers a throw instead of a melee swing (see
# _physics_process's own three-way combat-input branch). Cleared back to
# null the instant a throw actually fires (see _try_throw_bottle) -- the
# one weapon in this project with a real, working "unequip," since throwing
# consumes it (per explicit product answer: "you only had one physical
# bottle").
var _equipped_bottle: Node = null
# The held grenade, mirroring _equipped_bottle exactly -- see
# equip_grenade() for why each equip call clears the other two.
var _equipped_grenade: Node = null
var _equipped_grapple: Node = null

# --- Grappling-hook swing state. _grapple_active is the movement-mode gate
# (_physics_process hands the whole frame to _process_grapple_swing while
# it's true, exactly like _is_climbing already does). ---
var _grapple_active := false
var _grapple_anchor := Vector3.ZERO
var _grapple_rope_length := 0.0
# Consecutive seconds spent on the floor during the current swing -- the
# guaranteed way out of a swing that has settled onto the ground. Reset
# whenever a swing starts or ends so a previous swing's time can never
# instantly cut a fresh one.
var _grapple_ground_time := 0.0
# True while right-click is HELD with a gun equipped (continuous, unlike
# block()'s own one-shot press) -- drives both the camera's ADS zoom (see
# ADS_FOV_DEGREES) and which of the gun's own two spread constants applies
# (see weapons/pistol.gd's own fire()).
var _is_aiming := false
# Captured once in _ready() from the camera's own actual configured value
# (not hardcoded) -- what the FOV lerps back toward whenever _is_aiming is
# false.
var _default_fov := 75.0
# Last frame's is_crouching, used to edge-detect a press/release ourselves
# instead of relying on Input.is_action_just_pressed/just_released("crouch")
# -- see the crouch-handling block in _physics_process for why those lag a
# frame behind is_crouching itself and what that caused.
var _was_crouching := false
# Whatever "am I sprinting" was on the last GROUNDED frame -- carried
# through the whole jump/fall (see _physics_process) so releasing sprint
# mid-air doesn't instantly cut a sprint-jump's horizontal speed down to
# walk speed. Real momentum doesn't care what's held down after leaving
# the ground; only how fast you were going the instant you left it.
var _air_locked_sprinting := false
# Counts down from DASH_DOUBLE_TAP_WINDOW_SECONDS the instant the FIRST
# sprint-key press happens, 0 otherwise -- a second just_pressed while this
# is still > 0 is the double-tap that actually triggers a dash (see
# _physics_process's own handling). Not an absolute timestamp (e.g.
# Time.get_ticks_msec) since every other timing value in this file is
# already a plain delta-accumulated countdown (_punch_cooldown_remaining,
# _combat_mode_timer's sibling in character.gd, etc.) -- staying consistent
# with that avoids mixing two different time-tracking styles in one file.
var _sprint_tap_window_remaining := 0.0
# > 0 while a dash's own forced velocity is overriding normal WASD movement
# (see _physics_process) -- also doubles as the "already mid-dash" guard so
# a stray double-tap during an active dash can't restart/stack another one.
var _dash_time_remaining := 0.0
# The world-space direction locked in at the instant the dash was triggered
# (see _try_start_dash) -- captured once, not recomputed every frame, so
# changing WASD mid-dash can't steer a burst that's supposed to already be
# committed.
var _dash_direction := Vector3.ZERO

# --- Grab/carry (see _process_grab_carry) -- Skyrim-style physics grab:
# hold "interact" while the crosshair is on a "grabbable"-tagged RigidBody3D
# to lift it into a loose, force-driven carry; a plain tap while already
# carrying drops it. Non-null _carried_body is this state's own "owns the
# frame" flag, same role _seated_car/_is_climbing already play for theirs,
# except carrying deliberately does NOT early-return _physics_process --
# normal movement/looking stays free (see the grab/carry plan's own Design
# section 3), only combat input and starting a NEW climb are gated on it.
const GRAB_HOLD_SECONDS := 0.35
const GRAB_RANGE := 3.0
const CARRY_MIN_DISTANCE := 1.0
const CARRY_MAX_DISTANCE := 2.5
# Tuned against this project's own ragdoll mass range (hand≈5 up to
# hips≈126, see character.gd's own RAGDOLL_DENSITY comment) -- a real force,
# not a direct position/velocity override, is what makes "heavy resists,
# light flings around" fall out for free (F=ma) without any per-object
# tuning: the SAME force below moves a light bone/bottle snappily and a
# heavy torso sluggishly.
const CARRY_SPRING_K := 120.0
const CARRY_DAMPING_K := 18.0
# Hard force clamp -- same "don't let a continuous force blow up the
# physics solver" lesson MAX_DEATH_FALL_IMPACT_SPEED's own comment already
# established for a one-time velocity injection, applied here to a
# per-frame force instead.
const MAX_CARRY_FORCE := 2500.0
# If the carried body ends up further than this from its own target point
# (wedged behind geometry, e.g.), auto-drop instead of letting the spring
# force keep growing against an unmovable obstruction.
const MAX_CARRY_DISTANCE_BEFORE_AUTO_DROP := 4.0
# How often the carrying peer's own real position is broadcast to others
# while holding a networked (player-ragdoll) target -- ~15/sec, not every
# physics frame, matching this project's own "good enough, not lockstep"
# tolerance for physics sync (see _rpc_carry_transform's own comment).
const CARRY_SYNC_INTERVAL_SECONDS := 0.066

var _carried_body: RigidBody3D = null
var _carry_distance := 0.0
var _grab_hold_timer := 0.0
var _carry_sync_timer := 0.0

# --- Corpse looting (see _process_corpse_loot) -- a plain TAP of
# "interact" near a lootable corpse (loot/loot_component.gd) transfers its
# items into this player's own inventory. A SEPARATE timer from
# _grab_hold_timer above, not a reuse of it -- _grab_hold_timer resets to
# 0 the instant a hold CROSSES GRAB_HOLD_SECONDS (mid-hold, to arm a single
# grab attempt), so it can't reliably tell "a genuine quick tap" apart from
# "a long failed hold, timer since reset" at release time. Both timers
# start from the same press and add the same delta each frame though, so
# they always cross GRAB_HOLD_SECONDS on the same frame -- _loot_tap_still_
# possible flips false right then, guaranteeing a hold that actually grabs
# something never ALSO fires a loot on release for the same physical press.
var _loot_tap_timer := 0.0
var _loot_tap_still_possible := false

# True while attached to a wall (see _try_start_climb/_process_climbing) --
# _physics_process short-circuits into _process_climbing every frame this
# is true, same "own the whole frame" role _seated_car/_is_finisher_ragdolled
# already play for their own states.
var _is_climbing := false
# The wall's own surface normal at the moment it was grabbed (points AWAY
# from the wall, toward the player) -- captured once, not re-raycast every
# frame, so the climb stays glued to the exact spot it was grabbed at
# rather than potentially re-snapping to a slightly different point on an
# uneven facade.
var _climb_wall_normal := Vector3.ZERO
# World-space X/Z captured the instant a climb starts (Y is irrelevant --
# only vertical movement happens while attached) -- reapplied every frame
# in _process_climbing so the body can't drift sideways off the grabbed
# spot; only global_position.y (via velocity.y + move_and_slide) actually
# changes while climbing.
var _climb_anchor_x := 0.0
var _climb_anchor_z := 0.0
# True only during climb_top_out's own one-shot mantle clip -- a separate
# flag from _is_climbing (which is already false by the time this is set,
# see _begin_climb_top_out) so _physics_process can freeze movement
# entirely while it plays, the same way _is_finisher_ragdolled already
# freezes movement during ITS OWN one-shot transition, rather than letting
# stray WASD/jump input fight the animation's own root motion mid-mantle.
var _is_climb_topping_out := false

# Mirrors synced_is_walking and friends (see _setup_replication) -- a
# remote peer's own copy has no local _is_climbing of its own (their
# _physics_process never runs, see _process's own is_multiplayer_authority
# check), so this is what lets _process() there choose the right climb
# animation instead of falling back to plain set_walking() while the
# authority's own replicated position/rotation keep moving them up the
# wall. synced_climb_direction is "up"/"down"/"hold", matching
# character.gd's own start_climb_up()/start_climb_down()/hold_climb().
var synced_is_climbing := false
var synced_climb_direction := "hold"

# Naruto-Storm-style lock-on camera state (see _update_lock_on). Non-null
# target means locked -- while locked, raw mouse-look is ignored (see
# _unhandled_input) and rotation.y is instead driven toward the target's
# bearing every physics frame (see _physics_process's own moving-yaw-fold
# branch, which this replaces while locked).
var _lock_on_target: Node3D = null
# 0 means "not locked." While locked, a plain COUNT of how many candidates
# have been visited since acquiring (starts at 1 the instant a target is
# acquired) -- NOT a position within the candidate list, since a fresh
# acquisition can land on any candidate depending on crosshair angle,
# independent of that candidate's own place in the body-forward-sorted
# cycling order (see _try_lock_on's own comment for why indexing by list
# position instead was a real bug: it could release before ever visiting
# some candidates). Each further C-press increments this; once it would
# exceed the CURRENT candidate count, the lock releases entirely instead of
# wrapping back to the first target again.
var _lock_on_visits := 0
# > 0 while a lock-on target has failed its line-of-sight raycast, ticking
# back down to 0 the instant LOS is clear again -- see _update_lock_on. Only
# an unbroken block for longer than LOCK_ON_LOS_GRACE_SECONDS actually
# releases the lock.
var _lock_on_los_blocked_remaining := 0.0

# Mirror of exactly the 4 arguments _physics_process's own character.
# set_walking(...) call passes (see there) -- replicated (see
# _setup_replication) so remote peers' own copies of this body can drive
# the SAME call on THEIR local Character instance every frame (see
# _process below), instead of set_walking() only ever being called on the
# authority's own machine. Continuous state, not an event, so this rides
# the synchronizer's own per-property diffing rather than an RPC -- unlike
# the one-shot moves below, missing one update just means next frame's
# update corrects it, no different from position/rotation's own existing
# replication.
var synced_is_walking := false
var synced_is_running := false
var synced_is_crouching := false
var synced_move_direction := "forward"

func _get_character() -> Node:
	if _character == null or not is_instance_valid(_character):
		_character = get_node_or_null("Character")
	return _character

## Re-seats the third-person camera pivot at THIS character's own eye
## height, given whatever height the player chose for them in the creator
## (see character.gd's own set_height_scale). Called by main.gd once, right
## after a character is placed on a player and its saved height applied --
## a plain method rather than something polled per-frame, since a
## character's height can't change once they're in the world.
##
## Only the pivot moves. The collision capsule deliberately keeps its
## authored 1.8m size at every height: a whole family of movement constants
## (CLIMB_TOP_CHECK_HEIGHT, PUNCH_ORIGIN_HEIGHT, the step/mantle offsets)
## are tuned against that one capsule, and rescaling it would quietly
## retune climbing and melee reach along with the model's own size.
func sync_camera_to_character_height() -> void:
	var character := _get_character()
	if character == null or not character.has_method("get_height_scale"):
		return
	camera_pivot.position.y = BASE_CAMERA_PIVOT_HEIGHT * float(character.call("get_height_scale"))

func _ready() -> void:
	floor_max_angle = deg_to_rad(FLOOR_MAX_ANGLE_DEGREES)
	# Lets any local hostile NPC (see npc/wolf.gd's own _find_nearest_player)
	# find every player body in the scene without needing a bespoke lookup
	# of its own -- added for every instance, both the local authority AND
	# every remote peer's own copy, so a wolf correctly considers all of
	# them as potential targets, not just whichever one happens to be local.
	add_to_group("players")
	_setup_replication()
	# Only the local/owning peer should capture input, drive the camera, or
	# grab the mouse -- remote peers' copies of this body are driven purely
	# by the synchronizer.
	camera.current = is_multiplayer_authority()
	_default_fov = camera.fov
	_air_peak_y = position.y
	# stat_health/stat_stamina/stat_mana/level/xp are set directly onto this
	# node by main.gd (see its own pending_progress comment) BEFORE add_child
	# -- same "set before add_child, read in _ready()" ordering
	# save_character_id already established. Recomputes the derived
	# max_health/max_stamina/max_mana from whatever those actually loaded to
	# (0 for a remote peer's own copy of another player, or a brand new
	# character -- see var stat_health's own comment) and fills current
	# health/stamina/mana all the way up, same "spawn at full" convention
	# respawn() already uses -- there's no saved "current health" to restore
	# instead, a fresh session always starts topped off.
	# Goes through _recompute_stats() rather than repeating its formula
	# inline -- this used to be a duplicate copy of the same three lines, so
	# any future term added to the real formula would have been silently
	# missing here until some later incidental recompute. The current-value
	# adjustment _recompute_stats() does is irrelevant at this point, since
	# all three are overwritten to full immediately below.
	_recompute_stats()
	health = max_health
	stamina = max_stamina
	mana = max_mana
	xp_to_next_level = XP_PER_LEVEL_BASE * level
	# Every player instance -- both the local authority AND every remote
	# peer's own copy -- needs to be ABLE to play back voice, since a
	# REMOTE peer's own Player node is what actually receives and plays
	# THEIR voice on THIS screen (see VOICE_SEND_INTERVAL_SECONDS's own
	# comment for the full picture).
	_setup_voice_playback()
	if is_multiplayer_authority():
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
		# Owns Escape/pause entirely on its own (see pause_menu.gd) -- added
		# here rather than in city/main so it only ever exists once per
		# local player, matching everything else this block already gates
		# on being the owning peer.
		add_child(PauseMenuScene.instantiate())
		_hud = HudScene.instantiate()
		add_child(_hud)
		# One panel for inventory/stats/skills now (ui/inventory_panel.gd's
		# own TabContainer) -- the separate ui/stats_panel.tscn this used to
		# also instantiate is gone, so there's a single stats UI rather than
		# two that could drift apart. "L" (the open_stats action) still works;
		# it just opens this panel on its Stats tab.
		add_child(InventoryPanelScene.instantiate())
		# Bird's-eye city map on "M" (see ui/map_panel.gd). Same
		# authority-gated slot as every other local-only UI here -- a remote
		# peer's own player body must not build a second one.
		add_child(MapPanelScene.instantiate())
		_chat_box = ChatBoxScene.instantiate()
		add_child(_chat_box)
		_chat_box.message_sent.connect(_on_chat_message_sent)
		# Dev-only debug console (see dev_console.gd's own class comment) --
		# OS.is_debug_build() is false in a real release export, so this
		# never even gets added to the tree outside development/editor runs
		# or debug exports, per how this was explicitly asked for ("not in
		# the actual game").
		if OS.is_debug_build():
			add_child(DevConsoleScene.instantiate())
		# Only the local/owning peer captures their OWN microphone.
		_setup_voice_capture()
		# The looping outdoor city ambience (see city_ambience_player) --
		# starts faded in from silence, matching title_screen.gd's own
		# fade_in_rain() convention, rather than a flat volume snap. Always
		# safe to start here unconditionally: a freshly spawned player is
		# always on the street (main.gd's own SpawnPoint is never inside a
		# building's own footprint), never already indoors, so there's no
		# "should this actually start faded OUT instead" case to handle.
		_fade_city_ambience(true, CITY_AMBIENCE_SLOW_FADE_SECONDS)
		# Local rain effect (see player/rain_effect.gd's own class comment) --
		# built once here, starts inactive; toggled on/off later by
		# ui/dev_console.gd's own "/weather rain"/"/weather clear".
		_rain_effect = RainEffectScene.instantiate()
		add_child(_rain_effect)
	else:
		set_physics_process(false)
		set_process_unhandled_input(false)

## Propagates to every node in the tree (see SceneTree's own root
## Window forwarding it down via propagate_notification), so this fires here
## directly without needing main.gd or anything else to relay it -- the one
## place a real quit (window close, Alt+F4) needs to be caught to make sure
## the last partial interval since the previous periodic flush isn't lost.
func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST:
		_flush_playtime()
		_flush_progress()
		_flush_inventory()

## Persists whatever playtime has accumulated since the last flush (see
## PLAYTIME_SAVE_INTERVAL_SECONDS) onto save_character_id's own saved
## record, then resets the counter -- a no-op if save_character_id is -1
## (nothing to track) or there's nothing accumulated yet.
func _flush_playtime() -> void:
	if save_character_id < 0 or _playtime_since_save <= 0.0:
		return
	CharacterSave.update_playtime(save_character_id, _playtime_since_save, character_save_path)
	_playtime_since_save = 0.0

## Persists the CURRENT level/xp/stat_points/skill_points/5 stat allocations
## onto save_character_id's own saved record -- unlike _flush_playtime's own
## accumulate-then-reset delta, this just overwrites with whatever's true
## right now (see character_save.gd's own update_progress() comment on why).
## Called at the exact same two trigger points as _flush_playtime
## (PLAYTIME_SAVE_INTERVAL_SECONDS's own periodic check and the
## WM_CLOSE_REQUEST notification above) rather than owning a separate timer
## of its own -- level/stat changes are rare enough that riding along on the
## playtime cadence is plenty responsive, and a crash between flushes only
## ever loses the same few seconds playtime already accepts losing.
func _flush_progress() -> void:
	if save_character_id < 0:
		return
	CharacterSave.update_progress(save_character_id, level, xp, stat_points, skill_points, stat_health, stat_strength, stat_stamina, stat_mana, stat_speed, character_save_path)

## Persists the CURRENT inventory (general storage + equipped slots) onto
## save_character_id's own saved record -- same trigger points/reasoning
## as _flush_progress() above, just riding the same already-established
## cadence instead of owning a separate timer.
func _flush_inventory() -> void:
	if save_character_id < 0:
		return
	var inv: Node = get_node_or_null("Inventory")
	if inv == null:
		return
	CharacterSave.update_inventory(save_character_id, inv.storage_slots, inv.equipment, character_save_path)

## Called whenever the player earns XP -- today, only from killing an enemy
## (see e.g. npc/wolf.gd's own _die(), which grants XP_REWARD to whichever
## attacker actually landed the kill); quests are a stated future source,
## not implemented yet. Loops (not a single if) so one big XP grant can
## trigger several level-ups at once rather than only ever advancing one
## level per call. Awards STAT_POINTS_PER_LEVEL every level, plus one skill
## point every SKILL_POINT_EVERY_N_LEVELS levels (see their own const
## comments).
func add_xp(amount: float) -> void:
	if amount <= 0.0:
		return
	xp += amount
	while xp >= xp_to_next_level:
		xp -= xp_to_next_level
		level += 1
		stat_points += STAT_POINTS_PER_LEVEL
		if level % SKILL_POINT_EVERY_N_LEVELS == 0:
			skill_points += 1
		xp_to_next_level = XP_PER_LEVEL_BASE * level

## Spends ONE available stat point on the named stat ("health"/"strength"/
## "stamina"/"mana"/"speed") -- returns false (a no-op) if there's nothing
## left to spend or the name isn't recognized, same "denied action does
## nothing" convention every other gated action in this file already uses.
## Called by the Stats tab's own "+" buttons (ui/inventory_panel.gd).
func spend_stat_point(stat_name: String) -> bool:
	if stat_points <= 0:
		return false
	match stat_name:
		"health":
			stat_health += 1
		"strength":
			stat_strength += 1
		"stamina":
			stat_stamina += 1
		"mana":
			stat_mana += 1
		"speed":
			stat_speed += 1
		_:
			return false
	stat_points -= 1
	_recompute_stats()
	return true

## True whenever something on screen owns the keyboard/mouse and this body
## must not act on its own raw Input polling -- chat's focused LineEdit, or
## any open menu panel (see _ui_capture_count). Both cases share one
## predicate because they need identical treatment: a focused Control only
## intercepts _input/_unhandled_input callbacks, NOT the
## Input.is_action_pressed() polling _physics_process does, so without this
## the player would keep running and swinging while you type or click
## around a menu.
##
## Gravity/falling/landing are deliberately NOT gated on this -- same "still
## falls normally" reasoning _stun_remaining's own comment already gives.
## You are not safe while your inventory is open; you just can't act.
func _input_blocked() -> bool:
	return (_chat_box != null and _chat_box.is_open()) or _ui_capture_count > 0

## Public read of _input_blocked, for WORLD objects that poll the "interact"
## action themselves rather than going through this body's own input
## handling -- npc/unique_npc.gd's own dialogue trigger is the first. Those
## poll Input directly (the same technique radio.gd established), which
## doesn't respect a focused chat LineEdit on its own, so pressing E to type
## the letter "e" in chat would otherwise also start a conversation.
func is_input_blocked() -> bool:
	return _input_blocked()

## Called by menu panels as they open/close (see ui/inventory_panel.gd).
## Balanced calls -- every true must be matched by a false, which is why
## _ui_capture_count is a counter and this is the only thing that moves it.
func set_ui_capturing(capturing: bool) -> void:
	_ui_capture_count = maxi(0, _ui_capture_count + (1 if capturing else -1))

## Recomputes max_health/max_stamina/max_mana from MAX_HEALTH/MAX_STAMINA
## plus whatever's currently allocated in stat_health/stat_stamina/
## stat_mana -- called by spend_stat_point() every time a point actually
## lands. Raising a max also raises the CURRENT value by the same delta (so
## spending a point feels like an immediate, real boost -- you don't have to
## go rest/heal to actually benefit from it), but this can only ever
## increase current health/stamina/mana, never lower them, since spending
## points only ever raises the max.
func _recompute_stats() -> void:
	var new_max_health: float = MAX_HEALTH + stat_health * HEALTH_PER_STAT_POINT
	var new_max_stamina: float = MAX_STAMINA + stat_stamina * STAMINA_PER_STAT_POINT
	var new_max_mana: float = stat_mana * MANA_PER_STAT_POINT
	health += new_max_health - max_health
	stamina += new_max_stamina - max_stamina
	mana += new_max_mana - max_mana
	max_health = new_max_health
	max_stamina = new_max_stamina
	max_mana = new_max_mana

## Central chokepoint for the Strength stat's own damage bonus -- called at
## every place actual damage is about to be applied (this file's own
## punch()/_try_swing_bottle(), weapons/pistol.gd's fire(), weapons/
## thrown_bottle.gd's _on_body_entered()) right before take_damage()/the
## networked hit-request RPC, rather than baking a multiplier into each
## weapon's own damage constant -- one formula here applies uniformly
## regardless of which of those call sites actually lands a hit.
func apply_strength(base_damage: float) -> float:
	return base_damage * (1.0 + stat_strength * STRENGTH_DAMAGE_PERCENT_PER_POINT)

func _setup_replication() -> void:
	var config := SceneReplicationConfig.new()
	config.add_property(NodePath(".:position"))
	config.add_property(NodePath(".:rotation"))
	config.add_property(NodePath(".:synced_is_walking"))
	config.add_property(NodePath(".:synced_is_running"))
	config.add_property(NodePath(".:synced_is_crouching"))
	config.add_property(NodePath(".:synced_move_direction"))
	config.add_property(NodePath(".:synced_is_climbing"))
	config.add_property(NodePath(".:synced_climb_direction"))
	multiplayer_sync.replication_config = config

## Remote peers' own copies of this body never run _physics_process (see
## _ready's own set_physics_process(false) for non-authority) -- this is
## the ONLY thing that still runs for them, applying whatever locomotion
## state the authority last replicated (see synced_is_walking and friends)
## to THIS peer's own local Character instance, every rendered frame.
## Skipped entirely for the authority's own copy, which already calls
## set_walking() itself from inside _physics_process -- calling it a second
## time here too would just be redundant, not wrong, but there's no reason
## to pay for it.
##
## Also owns the finisher-ragdoll countdown for a non-authoritative copy
## specifically -- _rpc_play_hit_result can call _begin_finisher_ragdoll()
## on one of these (purely for visual replay, see its own comment), but the
## normal countdown-and-auto-recover logic lives in _physics_process, which
## never runs here. Reuses _begin_finisher_ragdoll/_end_finisher_ragdoll/
## _finisher_ragdoll_remaining/_is_finisher_ragdolled verbatim (no
## duplicated logic) -- just ticked from _process's own delta instead.
## Early-returns before set_walking() while ragdolled, mirroring
## _physics_process's own early-return for the same state: set_walking()'s
## own anim_player.play() calls would otherwise fight the ragdoll's frozen
## anim_player/procedurally-driven bones for no visible benefit.
func _process(delta: float) -> void:
	if is_multiplayer_authority():
		_process_voice_capture(delta)
		return
	if _is_finisher_ragdolled:
		_finisher_ragdoll_remaining -= delta
		if _finisher_ragdoll_remaining <= 0.0:
			_end_finisher_ragdoll()
		return
	var character := _get_character()
	if character == null:
		return
	if synced_is_climbing:
		match synced_climb_direction:
			"up":
				if character.has_method("start_climb_up"):
					character.start_climb_up()
			"down":
				if character.has_method("start_climb_down"):
					character.start_climb_down()
			_:
				if character.has_method("hold_climb"):
					character.hold_climb()
		return
	character.set_walking(synced_is_walking, synced_is_running, synced_is_crouching, synced_move_direction)

func _unhandled_input(event: InputEvent) -> void:
	# While locked on (see _lock_on_target's own comment), mouse input is
	# ignored for camera purposes entirely -- _update_lock_on drives
	# rotation.y/camera_pivot every physics frame instead, a real hard lock
	# matching Naruto Storm's own combat camera, not a soft/assisted one.
	if _in_cutscene or _in_claim_cutscene:
		return
	if _lock_on_target != null:
		return
	if event is InputEventMouseMotion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		# Yaw turns camera_pivot here; _physics_process folds it back into the
		# body's own rotation.y, but ONLY while actually moving (see its own
		# comment) -- so the body's facing exactly matches the camera the
		# instant WASD is held, which is the whole precondition for S/A/D-held
		# backward/strafe animations (see character.gd's own walk_backward/
		# walk_left/walk_right) to mean anything: WASD only reads as
		# "backward" if the character's own front is reliably wherever the
		# camera is looking, not wherever it last happened to walk. While
		# standing still, camera_pivot is left free to accumulate yaw with no
		# fold at all, so the camera can orbit all the way around a
		# stationary character -- e.g. to look at your own face.
		camera_pivot.rotation.y -= event.relative.x * MOUSE_SENSITIVITY
		_pitch = clampf(_pitch - event.relative.y * MOUSE_SENSITIVITY, deg_to_rad(-MAX_PITCH_DEGREES), deg_to_rad(MAX_PITCH_DEGREES))
		camera_pivot.rotation.x = _pitch

func _physics_process(delta: float) -> void:
	# Counted before the is_dead early-return below -- cumulative "time
	# played" should keep climbing through a ragdolled/dead stretch same as
	# it does in Terraria itself, not pause the instant health hits zero.
	if save_character_id >= 0:
		_playtime_since_save += delta
		if _playtime_since_save >= PLAYTIME_SAVE_INTERVAL_SECONDS:
			_flush_playtime()
			_flush_progress()
			_flush_inventory()

	# Ragdolling (see character.gd's own ragdoll()) is a scripted visual
	# collapse, not real physics on this body -- freezing movement/gravity
	# entirely here is simplest and correct given the only damage source
	# today (fall damage) only ever kills the player at the exact moment
	# they've just landed on solid ground (see the landing check below), so
	# there's never a mid-air corpse to keep falling.
	if is_dead:
		return

	# Combo finisher ragdoll (see _begin_finisher_ragdoll) -- same "this body
	# isn't doing anything itself right now" reasoning as the is_dead
	# early-return just above: character.gd's own real physics ragdoll is
	# what's actually driving the visible body during this window, not
	# anything here. Auto-recovers back to normal control once the timer
	# runs out (see _end_finisher_ragdoll), no death/respawn involved.
	if _is_finisher_ragdolled:
		_finisher_ragdoll_remaining -= delta
		if _finisher_ragdoll_remaining <= 0.0:
			_end_finisher_ragdoll()
		return

	# The intro fall cutscene (see start_intro_cutscene()/player/intro_
	# cutscene.gd) owns this body's position entirely via its own Tween --
	# same "nothing for this body's own movement/gravity/combat handling to
	# do" reasoning the seated-car check right below already has for itself.
	if _in_cutscene:
		return

	# The claim orbit (see start_claim_cutscene) owns the VIEW, not this
	# body -- but the player standing inside a building shouldn't be able to
	# walk out from under their own reveal shot, so movement is frozen for
	# its duration the same way. Gravity is skipped along with everything
	# else, which is correct here: they were already standing on a floor
	# when they typed /claim.
	if _in_claim_cutscene:
		velocity = Vector3.ZERO
		return

	# Seated (driver or passenger, see _seated_car's own comment) -- the car
	# itself owns this body's position entirely (it's been reparented onto
	# one of the car's own seat nodes, see vehicles/car.gd's own
	# _complete_enter), so there's nothing for this body's own movement/
	# gravity/combat handling to do until it's unparented again.
	if _seated_car != null:
		return

	# True while ui/chat_box.gd's own input field is focused OR a menu panel
	# is open -- gates WASD/jump/dash/combat input the same way
	# _stun_remaining already does (see each check below). Was chat-only
	# before the inventory stopped pausing the tree; see _input_blocked() for
	# why both cases need identical treatment and what stays ungated.
	var input_blocked := _input_blocked()

	# Applies/syncs the carry force every frame regardless of anything else
	# below (see this state's own comment) -- deliberately BEFORE the
	# climbing/chat gates, since carrying should keep working (and a drop
	# should always be possible) even if, say, chat happens to be open.
	if not input_blocked:
		_process_grab_carry(delta)
		_process_corpse_loot(delta)

	# Frozen for the one-shot mantle clip's own duration -- same "the
	# animation's own root motion is what's moving the body, nothing here
	# should fight it" reasoning _is_finisher_ragdolled's own early-return
	# above already established for a different one-shot transition.
	if _is_climb_topping_out:
		return

	# Swinging replaces the entire ordinary movement frame -- gravity, WASD
	# velocity and move_and_slide() are all handled by the rope constraint
	# instead (see _process_grapple_swing). Placed BEFORE the climbing gate
	# so a swing that carries you past a wall doesn't get hijacked into a
	# wall-grab by the same held W that's pumping it.
	if _grapple_active:
		# Same reason the climbing branch below silences these: this
		# early-return skips the only call that ever stops the footstep
		# loops, so grappling away mid-sprint would leave the running loop
		# playing for the whole swing.
		_update_footstep_audio(false, false)
		_process_grapple_swing(delta)
		return

	if _is_climbing:
		# A real reported bug ("the sprint noise is playing while climbing"):
		# this early-return skips the _update_footstep_audio call further
		# down, which is the ONLY thing that ever stops those loops. Grabbing
		# a wall mid-sprint therefore left the running loop playing for the
		# whole climb, since nothing downstream ran again to turn it off.
		# Silenced here rather than only at grab time so it holds for the
		# entire climb regardless of how it was entered.
		_update_footstep_audio(false, false)
		_process_climbing(delta)
		return
	elif not input_blocked and not is_dead and _stun_remaining <= 0.0 and _hitstun_remaining <= 0.0 and _dash_time_remaining <= 0.0 and _carried_body == null:
		_try_start_climb()
		# A grab that just succeeded THIS frame must not also fall through
		# to the ordinary gravity/WASD-velocity/move_and_slide() code below
		# -- without this, the very first climbing frame would compute
		# normal ground movement (using the same held W that just triggered
		# the grab) on top of what _try_start_climb() already set up,
		# fighting it for one frame before _process_climbing ever got a
		# chance to run. The real climb-driven motion starts cleanly on the
		# NEXT call instead.
		if _is_climbing:
			return

	# Ticked down every frame regardless of anything else below -- a stunned
	# player still falls/lands normally (gravity and move_and_slide aren't
	# gated on this), only WASD/jump/dash/punch/block are (see each of
	# those below). Decremented here, before any of those checks run, so a
	# stun that expires exactly this frame already reads as "over" by the
	# time they're evaluated, same "tick down before the check" ordering
	# _sprint_tap_window_remaining's own comment already explains.
	_stun_remaining = maxf(0.0, _stun_remaining - delta)
	# Same "tick down every frame regardless" treatment as _stun_remaining
	# above -- gates a different, narrower set of input (see each var's own
	# comment for exactly which).
	_hitstun_remaining = maxf(0.0, _hitstun_remaining - delta)
	_invulnerable_remaining = maxf(0.0, _invulnerable_remaining - delta)

	var was_on_floor := is_on_floor()
	# Captured here (takeoff) rather than recomputed later -- by the time
	# effective_sprinting is known further down, was_on_floor's own branch
	# has already decided whether this is really a jump this frame.
	var jumped_this_frame := false
	if _fly_mode:
		# Gravity and the ordinary ground-jump entirely bypassed -- vertical
		# velocity is driven directly off held jump (up) / crouch (down)
		# every frame, regardless of is_on_floor(). jumped_this_frame stays
		# false here on purpose: flying up shouldn't trigger the normal jump
		# animation/RPC (see below), it's a distinct dev-only movement mode.
		var vertical_input := 0.0
		if Input.is_action_pressed("jump"):
			vertical_input += 1.0
		if Input.is_action_pressed("crouch"):
			vertical_input -= 1.0
		velocity.y = vertical_input * FLY_SPEED
	elif not was_on_floor:
		velocity.y -= GRAVITY * delta
	elif Input.is_action_just_pressed("jump") and _stun_remaining <= 0.0 and _hitstun_remaining <= 0.0 and not input_blocked:
		velocity.y = JUMP_VELOCITY
		jumped_this_frame = true

	# Tracks the highest point reached since last touching ground -- reset
	# to the current height every grounded frame (cheap and always right,
	# not just on the leaving-ground transition), and grown while airborne.
	# Comparing this to the landing height is what turns "fell off a low
	# ledge" (harmless) and "fell off a rooftop" (lethal) into different
	# outcomes instead of one fixed jump-landing check.
	if was_on_floor:
		_air_peak_y = global_position.y
	else:
		_air_peak_y = maxf(_air_peak_y, global_position.y)

	var input_dir := Vector2(
		Input.get_action_strength("move_right") - Input.get_action_strength("move_left"),
		Input.get_action_strength("move_back") - Input.get_action_strength("move_forward")
	)
	if _stun_remaining > 0.0 or _hitstun_remaining > 0.0 or input_blocked:
		input_dir = Vector2.ZERO # stunned/locked-in-a-combo/typing: no WASD-driven movement (see _stun_remaining/_hitstun_remaining's own comments)
	var is_moving := input_dir.length() > 0.01

	# While actually moving, the body's own facing exactly matches wherever
	# the camera is looking -- folded back from camera_pivot into rotation.y,
	# instantly rather than gradually catching up over several frames, since
	# the directional walk clips (walk_left/right/backward, see character.gd)
	# need the body's own transform.basis to already be camera-aligned THIS
	# frame, not several frames from now. rotation.y + camera_pivot.rotation.y
	# (the camera's actual absolute facing) is unchanged by this -- it's just
	# handing 100% of the yaw from one to the other instead of a clamped
	# fraction, so the camera itself never visibly snaps. Done BEFORE the
	# dash-trigger check below (which also reads transform.basis, via
	# _try_start_dash) so a dash fired this exact frame already uses this
	# frame's fully up-to-date facing, not last frame's.
	#
	# Gated on is_moving so that standing still leaves camera_pivot free to
	# orbit without dragging the body along -- letting the camera swing all
	# the way around to look at your own character's face. Folding this
	# unconditionally (every frame, moving or not) was tried and broke
	# exactly that: the body would instantly snap to match the camera the
	# moment you moved the mouse at all, so there was never a frame where
	# camera and body faced different directions to orbit through.
	#
	# ALSO folds while genuinely aiming down sights with a gun (real ask:
	# the free-orbit "look at yourself" behavior above is right for idle,
	# but wrong while aiming -- "the camera needs to move where you are
	# looking" instead of being able to detach from your own facing). Read
	# fresh here via Input.is_action_pressed (not _is_aiming, which isn't
	# computed until the combat-input block further down this same
	# function -- reading it here would use last frame's stale value) so a
	# held right-click keeps the reticle/facing glued together even while
	# standing perfectly still, the same way it already does the instant you
	# start moving.
	var gun_aiming_now := _equipped_gun != null and Input.is_action_pressed("block") and not is_dead and _stun_remaining <= 0.0
	#
	# While locked on (see _lock_on_target's own comment), this fold is
	# replaced entirely by _update_lock_on, which drives rotation.y toward
	# the target's own bearing every frame and keeps camera_pivot.rotation.y
	# pinned at 0.0 -- since it never drifts away from 0 while locked, there's
	# nothing to fold back on release (unlike vehicles/car.gd's own
	# end_car_transition, which DOES need an explicit fold-back because its
	# camera is free to accumulate yaw the whole time it's active): rotation.y
	# simply keeps whatever heading it last lerped to, and this ordinary
	# is_moving fold picks back up from there with no pop.
	if _lock_on_target != null:
		_update_lock_on(delta)
	elif is_moving or gun_aiming_now:
		rotation.y += camera_pivot.rotation.y
		camera_pivot.rotation.y = 0.0

	# See _sprint_tap_window_remaining's own comment for why this counts down
	# rather than tracking an absolute timestamp. Ticked down BEFORE checking
	# just_pressed this frame so a press landing exactly at the window's own
	# last valid frame still sees it as > 0 (this frame's tick already
	# happened) rather than the check racing its own countdown to zero first.
	_sprint_tap_window_remaining = maxf(0.0, _sprint_tap_window_remaining - delta)
	if Input.is_action_just_pressed("sprint") and _stun_remaining <= 0.0 and not input_blocked:
		if _sprint_tap_window_remaining > 0.0:
			_try_start_dash(input_dir)
			_sprint_tap_window_remaining = 0.0
		else:
			_sprint_tap_window_remaining = DASH_DOUBLE_TAP_WINDOW_SECONDS

	# Which of the 4 horizontal directions WASD currently requests,
	# RELATIVE TO THE BODY'S OWN FACING (see above -- always the camera
	# direction now, so this is also always relative to the camera). Drives
	# both which of character.gd's directional walk clips shows (see
	# set_walking's own call below) and whether sprint/bike-speed-pedal
	# speed is even allowed to apply (see the "elif is_moving" speed block
	# below) -- not just the animation, since a fast strafe/backward-run
	# would look physically inconsistent with a walk-paced animation
	# otherwise.
	var move_direction := _direction_from_input(input_dir) if is_moving else "forward"
	# Sprint/bike-speed-pedal speed (and the matching running footstep
	# sound, see _update_footstep_audio's own call below) only apply while
	# actually moving forward -- backward/strafing has no dedicated running
	# animation (see character.gd's own WalkBackwardAnim comment), and
	# moving/sounding faster than the walk-paced animation actually shown
	# would look and sound physically wrong, the same mismatch
	# effective_sprinting's own crouch-override comment already flags for a
	# different combination.
	var moving_forward := move_direction == "forward"

	var direction := (transform.basis * Vector3(input_dir.x, 0, input_dir.y)).normalized()
	var wants_sprint := is_moving and Input.is_action_pressed("sprint")
	# Sprint always wins over crouch, not just "sprint speed while still
	# LOOKING crouched" -- without the "and not wants_sprint" here, holding
	# both keys together kept is_crouching true, so set_walking's own
	# "crouching overrides running entirely" branch (there's no crouch-
	# sprint clip) kept playing crouch_walk while velocity was ALREADY at
	# full SPRINT_SPEED underneath it -- a real, confirmed mismatch between
	# what the animation showed and how fast the player was actually
	# moving. Held, not just-pressed -- read fresh every frame and handed to
	# set_walking below the same way running already is (see its own
	# comment), rather than character.gd storing its own copy of "am I
	# crouching" as separate state. Requiring is_moving (via wants_sprint)
	# means just tapping sprint while standing still and crouched, with no
	# WASD held, does nothing -- there's no move to actually sprint into yet.
	# The crouch key doubles as "descend" while flying (see the _fly_mode
	# branch above) -- excluded here so it doesn't ALSO trigger the normal
	# crouch pose/animation while airborne and flying downward.
	var is_crouching := Input.is_action_pressed("crouch") and not wants_sprint and not _fly_mode
	_update_stamina(delta, wants_sprint)
	var is_sprinting := wants_sprint and not _stamina_exhausted

	# While grounded this just tracks the live input every frame, so it's
	# always up to date the instant a jump leaves the ground; while
	# airborne it deliberately does NOT update, holding onto whatever it
	# was at takeoff regardless of the sprint key changing mid-air.
	if was_on_floor:
		_air_locked_sprinting = is_sprinting
	var effective_sprinting := is_sprinting if was_on_floor else _air_locked_sprinting

	if _dash_time_remaining > 0.0:
		# Overrides ordinary WASD-driven velocity entirely for the burst's
		# duration -- see _dash_direction's own comment on why this is a
		# locked-in direction, not something recomputed from live input here.
		_dash_time_remaining = maxf(0.0, _dash_time_remaining - delta)
		velocity.x = _dash_direction.x * DASH_SPEED
		velocity.z = _dash_direction.z * DASH_SPEED
		if _dash_time_remaining <= 0.0:
			# The dash_backward CLIP (see character.gd's own dash()) runs
			# well past this burst's own DASH_DURATION_SECONDS --
			# same reasoning as jump.tres's own landing/recovery tail (see
			# land()'s own comment): ending the character's dash state the
			# instant the burst itself is over, rather than waiting out
			# however long the clip's own follow-through takes, keeps the
			# visible animation in sync with the player already being free
			# to move normally again instead of still showing a committed
			# lunge recovery pose for up to another second.
			var dashing_character := _get_character()
			if dashing_character != null and dashing_character.has_method("end_dash"):
				dashing_character.end_dash()
				if Net.is_networked():
					_rpc_end_dash.rpc()
	elif is_moving:
		# SPEED/SPRINT_SPEED themselves stay fixed (matching every existing
		# test that reads them directly, e.g. tests/test_crouch_sprint_check.gd's
		# own _player.SPEED/_player.SPRINT_SPEED comparisons -- a fresh,
		# 0-speed-point character's actual velocity always matches them
		# exactly) -- the Speed stat's own bonus (see SPEED_PERCENT_PER_POINT)
		# is applied ONLY here, the single place actual velocity gets set
		# from either constant.
		var speed := (SPRINT_SPEED if (effective_sprinting and moving_forward) else SPEED) * (1.0 + stat_speed * SPEED_PERCENT_PER_POINT)
		velocity.x = direction.x * speed
		velocity.z = direction.z * speed
	else:
		velocity.x = 0.0
		velocity.z = 0.0

	# Captured immediately before move_and_slide() clips it against the
	# floor's own collision normal on landing -- see _apply_fall_damage's
	# own comment for why the real pre-impact speed has to be grabbed here,
	# not read back out afterward.
	var pre_slide_velocity_y := velocity.y
	move_and_slide()

	if is_on_floor() and not was_on_floor and not _fly_mode:
		_apply_fall_damage(_air_peak_y - global_position.y, pre_slide_velocity_y)

	# Guarded the same way as set_walking below: fall damage just above can
	# kill the player (_die() already stops both sound loops), and without
	# this check this call would immediately restart whichever one was
	# playing -- the movement/sprint keys haven't changed yet within this
	# same frame, only is_dead has.
	if not is_dead:
		_update_footstep_audio(is_moving and is_on_floor(), effective_sprinting and moving_forward)

	var character := _get_character()
	if character != null and not is_dead:
		# Ends the jump animation state the instant landing is actually
		# detected -- BEFORE set_walking, so this same frame's call already
		# sees a clear _is_jumping and can transition straight to walk/run/
		# idle instead of waiting a frame. Reuses the same is_on_floor()/
		# was_on_floor pair the fall-damage check above already computed
		# this frame (no physics step happened in between, so it's still
		# the same answer) -- safe to call even if no jump was ever thrown
		# (see land()'s own comment).
		if is_on_floor() and not was_on_floor and character.has_method("land"):
			character.land()
			if Net.is_networked():
				_rpc_land.rpc()
		# Edge-detected off is_crouching ITSELF (a plain is_action_pressed()
		# read, updated instantly -- no lag) rather than
		# Input.is_action_just_pressed/just_released("crouch") -- those lag a
		# full physics frame behind is_action_pressed() flipping (see
		# test_punch_check.gd's own comment on the same quirk for punch
		# input). That lag left a real one-frame gap on RELEASE: is_crouching
		# already reads false the instant release() is called, but
		# just_released wouldn't fire until the NEXT frame -- so for one
		# frame, set_walking() below would see crouching=false with
		# _is_crouch_transitioning still false (end_crouch() hadn't run yet)
		# and fall through to its own plain "idle" snap, THEN end_crouch()
		# the following frame would blend crouch_to_idle from THAT
		# accidental standing-tall snap instead of the actual held crouch
		# pose -- confirmed directly (a live diagnostic driving the real
		# input pipeline) as the source of a severe, real foot-through-floor
		# clip on every single uncrouch, independent of the earlier crouch/
		# walk interaction bugs already fixed above. Tracking our own
		# edge off is_crouching removes the lag entirely, and running this
		# BEFORE set_walking (not after) means start_crouch()/end_crouch()
		# already have _is_crouch_transitioning set by the time set_walking
		# runs this same frame, so its own branches correctly leave the
		# transition alone instead of racing it.
		if not is_moving:
			if is_crouching and not _was_crouching and character.has_method("start_crouch"):
				character.start_crouch()
				if Net.is_networked():
					_rpc_start_crouch.rpc()
			elif not is_crouching and _was_crouching and character.has_method("end_crouch"):
				character.end_crouch()
				if Net.is_networked():
					_rpc_end_crouch.rpc()
		_was_crouching = is_crouching
		# Forces the run animation for the burst's own duration -- without
		# this, a bare double-tap with no WASD held (a real, supported case,
		# see _try_start_dash) would fly the body across the ground while
		# set_walking still thought nothing was moving and kept showing a
		# static idle pose. Direction is forced to "forward" for the same
		# reason regardless of which way the burst actually went (left/right
		# dashes have no dedicated clip of their own -- see _try_start_dash's
		# own comment -- so this preserves the exact same forced-run fallback
		# they already used before directional walk animations existed).
		var is_dashing := _dash_time_remaining > 0.0
		synced_is_walking = is_moving or is_dashing
		synced_is_running = effective_sprinting or is_dashing
		synced_is_crouching = is_crouching
		synced_move_direction = "forward" if is_dashing else move_direction
		character.set_walking(synced_is_walking, synced_is_running, synced_is_crouching, synced_move_direction)
		if jumped_this_frame and character.has_method("jump"):
			character.jump(effective_sprinting, synced_is_walking)
			if Net.is_networked():
				_rpc_jump.rpc(effective_sprinting, synced_is_walking)

	_punch_cooldown_remaining = maxf(0.0, _punch_cooldown_remaining - delta)
	# Whole combat input block skipped entirely while typing in chat -- same
	# "one guard covers everything underneath" reasoning is_dead's own
	# early-return at the top of this function already uses, rather than
	# threading "and not input_blocked" through every individual branch below
	# (tried first, but that leaves real edge cases: e.g. _punch_held
	# already being true from BEFORE chat opened would still let
	# just_released fire a real punch while still mid-message). Also skipped
	# entirely while carrying something -- both hands are full (same
	# reasoning _seated_car/_is_climbing already apply to combat input).
	if not input_blocked and _carried_body == null:
		if _equipped_gun != null:
			# A gun completely takes over left/right-click/reload while equipped
			# -- semi-automatic (see weapons/pistol.gd's own class comment), so
			# a fresh press fires once, with none of the fist/sword system's own
			# hold-for-heavy-punch tracking (a gun has no "heavy" variant) or
			# cooldown gating here -- the gun's own fire() tracks its own
			# cooldown/ammo/reload state internally and just silently no-ops on
			# a denied shot, same "denied action does nothing" convention every
			# other gated action in this file already uses.
			if Input.is_action_just_pressed("punch") and not is_dead and _stun_remaining <= 0.0 and _hitstun_remaining <= 0.0:
				_try_fire_gun()
			# Right-click is aim-down-sights while a gun is held -- continuous
			# for as long as it's HELD (unlike block()'s own one-shot press),
			# driving both the camera's zoom and the gun's own tighter ADS
			# spread (see ADS_FOV_DEGREES/_try_fire_gun).
			_is_aiming = Input.is_action_pressed("block") and not is_dead and _stun_remaining <= 0.0
			# Reported every frame (not just on an edge) -- harmless/cheap,
			# and character.gd's own set_aiming() just remembers whatever the
			# latest value was, same "caller reports current state" pattern
			# set_walking's own running/crouching arguments already use.
			if character != null and character.has_method("set_aiming"):
				character.set_aiming(_is_aiming)
			if Input.is_action_just_pressed("reload") and not is_dead and _hitstun_remaining <= 0.0 and _equipped_gun.has_method("reload"):
				_equipped_gun.reload()
		elif _equipped_bottle != null:
			# Right-click held = aim-to-throw, reusing the SAME _is_aiming var
			# the gun branch above drives -- the camera FOV zoom (further
			# below, already weapon-agnostic) is one half of "you're aiming"
			# feedback, character.begin_bottle_aim()/cancel_bottle_aim() (see
			# their own comments) is the other: a real windup pose (arm
			# raised, cocked back), held for as long as aim stays held.
			# character.set_aiming() is deliberately NOT called here (unlike
			# the gun branch) -- it only visibly matters while _holding_gun
			# (see set_walking()'s own gate), so calling it would be a
			# harmless no-op, but omitting it keeps this branch's own intent
			# explicit.
			var was_aiming_bottle := _is_aiming
			_is_aiming = Input.is_action_pressed("block") and not is_dead and _stun_remaining <= 0.0
			if _is_aiming and not was_aiming_bottle:
				if character != null and character.has_method("begin_bottle_aim"):
					character.begin_bottle_aim()
			elif was_aiming_bottle and not _is_aiming:
				# Releasing right-click without throwing -- character.gd's
				# own cancel_bottle_aim() drops the held windup pose and
				# hands control back to set_walking(); the punch handler
				# below re-checks _is_aiming's CURRENT value fresh on the
				# next press regardless, so no other bookkeeping is needed
				# here.
				if character != null and character.has_method("cancel_bottle_aim"):
					character.cancel_bottle_aim()
			if Input.is_action_just_pressed("punch") and not is_dead and _stun_remaining <= 0.0 and _hitstun_remaining <= 0.0:
				if _is_aiming:
					if character != null and character.has_method("release_bottle_throw"):
						character.release_bottle_throw()
					_try_throw_bottle()
				elif _punch_cooldown_remaining <= 0.0:
					_try_swing_bottle(character)
		elif _equipped_grapple != null:
			# FIRING only. Releasing is handled inside
			# _process_grapple_swing() instead, because this whole block is
			# unreachable once a swing is underway -- the grapple gate near
			# the top of _physics_process early-returns well before it.
			_is_aiming = false
			if Input.is_action_just_pressed("punch") and not is_dead and _stun_remaining <= 0.0 and _hitstun_remaining <= 0.0:
				try_fire_grapple()
		elif _equipped_grenade != null:
			# The same right-click-to-aim, click-to-throw gesture the bottle
			# branch above uses, and the same character.gd windup/throw pose
			# (see weapons/grenade.gd's own _equip comment on why the throw
			# animation is shared rather than duplicated).
			#
			# Simpler than the bottle branch in one way: there's no melee
			# fallback. Clicking WITHOUT aiming does nothing at all -- a
			# grenade has no swing, and pulling the pin by accident because
			# you left-clicked is not a mechanic anyone wants.
			var was_aiming_grenade := _is_aiming
			_is_aiming = Input.is_action_pressed("block") and not is_dead and _stun_remaining <= 0.0
			if _is_aiming and not was_aiming_grenade:
				if character != null and character.has_method("begin_bottle_aim"):
					character.begin_bottle_aim()
			elif was_aiming_grenade and not _is_aiming:
				if character != null and character.has_method("cancel_bottle_aim"):
					character.cancel_bottle_aim()
			if Input.is_action_just_pressed("punch") and _is_aiming and not is_dead and _stun_remaining <= 0.0 and _hitstun_remaining <= 0.0:
				if character != null and character.has_method("release_bottle_throw"):
					character.release_bottle_throw()
				_try_throw_grenade()
		else:
			_is_aiming = false
			# Doesn't decide regular-vs-heavy on PRESS -- has to wait for release to
			# know how long the button was actually held. A plain instant-click
			# jab still feels instant in practice (an ordinary click's own press-to-
			# release gap is a couple frames at most, nowhere near
			# HEAVY_PUNCH_HOLD_THRESHOLD_SECONDS), so this doesn't add any
			# perceptible delay to the common case, it just defers the decision by
			# the length of whatever hold already happened.
			if Input.is_action_just_pressed("punch"):
				_punch_held = true
				_punch_held_seconds = 0.0
			elif _punch_held and Input.is_action_pressed("punch"):
				_punch_held_seconds += delta
			if _punch_held and Input.is_action_just_released("punch"):
				_punch_held = false
				if not is_dead and _punch_cooldown_remaining <= 0.0 and _stun_remaining <= 0.0 and _hitstun_remaining <= 0.0:
					var is_heavy := _punch_held_seconds >= HEAVY_PUNCH_HOLD_THRESHOLD_SECONDS
					_punch(character, is_heavy)

			# Right-click, timed, not holdable (see BLOCK_DAMAGE_MULTIPLIER's own
			# comment) -- unlike punch there's no press/release distinction to wait
			# on, a single press immediately plays the one-shot block animation.
			# Always safe to call character.block() again even if a block is already
			# playing: it just restarts the same clip (matching how re-pressing punch
			# mid-swing already behaves via anim_player.play()), so no extra
			# "already blocking" guard is needed here.
			#
			# Deliberately NOT gated on _hitstun_remaining (unlike every combat
			# input above) -- this is one of the two named escapes from a
			# landed combo (see _hitstun_remaining's own comment), so it has to
			# stay usable precisely while locked in. Successfully blocking
			# immediately clears the lock (see the line right after
			# character.block() below) rather than just letting it ride out.
			if Input.is_action_just_pressed("block") and not is_dead and _stun_remaining <= 0.0 and character != null and character.has_method("block"):
				character.block()
				_hitstun_remaining = 0.0
				if Net.is_networked():
					_rpc_block.rpc()

		# Naruto-Storm-style lock-on toggle/cycle (see _try_lock_on's own
		# comment) -- deliberately NOT gated on _stun_remaining/
		# _hitstun_remaining, same reasoning as block above: choosing/cycling
		# a camera target isn't itself an attack, there's no reason being
		# locked in a combo should also lock out looking at your own
		# attacker.
		if Input.is_action_just_pressed("lock_on") and not is_dead:
			_try_lock_on()

	# ADS zoom -- lerped toward whichever FOV currently applies so it reads
	# as a smooth zoom transition rather than a snap, and smoothly settles
	# back to _default_fov even if the gun gets swapped out mid-aim (since
	# this runs unconditionally, not just inside the _equipped_gun branch
	# above). Also gated on _camera_manual_override (see its own comment) --
	# without this, a manually-set Camera3D.fov (the "scope"/zoom look while
	# aiming) would get overwritten right back to ADS_FOV_DEGREES/
	# _default_fov on the very next frame, the exact same "my Remote-tab
	# edit gets stomped every frame" problem set_manual_override already
	# solves for every held weapon's own grip pose.
	if not _camera_manual_override:
		var target_fov: float = ADS_FOV_DEGREES if _is_aiming else _default_fov
		camera.fov = lerpf(camera.fov, target_fov, delta * ADS_FOV_LERP_SPEED)

		# Aiming down sights (gun equipped, right-click held) zooms into its
		# OWN more over-the-shoulder offset (ADS_SHOULDER_OFFSET_X), not the
		# everyday SHOULDER_OFFSET_X -- releasing aim (or not holding a gun
		# at all) settles back to the normal pov, same lerp either way so the
		# transition reads as a smooth push-in/pull-out rather than a snap.
		var target_shoulder_offset: float = ADS_SHOULDER_OFFSET_X if (_equipped_gun != null and _is_aiming) else SHOULDER_OFFSET_X
		_spring_arm.position.x = lerpf(_spring_arm.position.x, target_shoulder_offset, delta * SHOULDER_OFFSET_LERP_SPEED)

## Fires a directional burst in response to a detected sprint-key double-tap
## (see _physics_process's own _sprint_tap_window_remaining handling) --
## dash_input is whatever WASD was actually held at that exact instant, in
## the same (right, back) convention _physics_process's own input_dir
## already uses. No held direction (dash_input near zero, e.g. a bare
## double-tap with no other key down) defaults to Vector2(0, -1) -- the same
## value move_forward alone would produce -- so "just double-tap standing
## still" reads as a forward dash, exactly like holding W would. Silently
## does nothing (no stamina spent, no burst) if a dash is already in
## progress or there isn't enough stamina to afford one -- there's no
## separate "denied" feedback today, matching how e.g. punching during its
## own cooldown already just silently no-ops elsewhere in this file.
## Checked every physics frame while NOT already climbing (see
## _physics_process's own dispatch above) -- grabs on the instant sprint+W
## are BOTH held and a forward raycast finds a real wall within arm's
## reach. Deliberately requires sprint (not just W) so brushing up against
## a building while walking normally never accidentally starts a climb --
## matches the explicit ask ("hold shift and W... to climb").
func _try_start_climb() -> void:
	if not Input.is_action_pressed("sprint") or not Input.is_action_pressed("move_forward"):
		return
	if stamina <= 0.0:
		return
	var space_state := get_world_3d().direct_space_state
	var forward := -transform.basis.z
	var from := global_position + Vector3(0, PUNCH_ORIGIN_HEIGHT, 0)
	var to := from + forward * CLIMB_GRAB_CHECK_DISTANCE
	var query := PhysicsRayQueryParameters3D.create(from, to)
	query.collision_mask = 1 # world geometry only -- matches building walls' own collision_layer
	query.exclude = [self]
	var result := space_state.intersect_ray(query)
	if result.is_empty():
		return
	var normal: Vector3 = result["normal"]
	if absf(normal.y) > CLIMB_WALL_MAX_SLOPE_DOT:
		return # too close to horizontal to be a real climbable wall (a floor/ceiling/shallow roof)

	_is_climbing = true
	_climb_wall_normal = normal
	_climb_anchor_x = global_position.x
	_climb_anchor_z = global_position.z
	velocity = Vector3.ZERO

## Owns the whole frame while attached to a wall (see _physics_process's own
## dispatch) -- W climbs up, S climbs down, neither holds the current grip
## in place, Space hops off, and running out of stamina releases the grip
## the same way hopping off does, just without the deliberate push.
func _process_climbing(delta: float) -> void:
	var character := _get_character()

	if Input.is_action_just_pressed("jump"):
		_end_climb(character, true)
		return

	# Dev-only /infinitesprint (see _infinite_stamina's own comment) --
	# climbing draws from the SAME stamina pool sprinting does, so the same
	# flag covers both rather than needing a second, differently-named
	# command for what a player would reasonably expect "infinite sprint"
	# to already cover.
	if not _infinite_stamina:
		stamina = maxf(0.0, stamina - STAMINA_DRAIN_PER_SEC * delta)
		if stamina <= 0.0:
			_end_climb(character, false)
			return

	var climbing_up := Input.is_action_pressed("move_forward")
	var climbing_down := Input.is_action_pressed("move_back")

	if climbing_up and not climbing_down:
		# Reached the top? Checked BEFORE moving this frame: a forward
		# raycast from above the player's own current grip, same technique
		# _try_start_climb used to grab on in the first place, just from a
		# higher vantage point -- once IT stops finding the wall, there's no
		# more surface left to climb, meaning this is the top edge.
		if not _climb_wall_present(global_position + Vector3(0, CLIMB_TOP_CHECK_HEIGHT, 0)):
			_begin_climb_top_out()
			return
		velocity.y = CLIMB_SPEED
		synced_is_climbing = true
		synced_climb_direction = "up"
		if character != null and character.has_method("start_climb_up"):
			character.start_climb_up()
	elif climbing_down and not climbing_up:
		velocity.y = -CLIMB_SPEED
		synced_is_climbing = true
		synced_climb_direction = "down"
		if character != null and character.has_method("start_climb_down"):
			character.start_climb_down()
	else:
		velocity.y = 0.0
		synced_is_climbing = true
		synced_climb_direction = "hold"
		if character != null and character.has_method("hold_climb"):
			character.hold_climb()

	velocity.x = 0.0
	velocity.z = 0.0
	global_position.x = _climb_anchor_x
	global_position.z = _climb_anchor_z
	move_and_slide()

	# Climbed all the way back down to solid ground -- release normally
	# (no push, same as running out of stamina) rather than leaving the
	# player stuck holding an invisible grip below floor level.
	if climbing_down and is_on_floor():
		_end_climb(character, false)

## A forward raycast from check_position, into the wall (-_climb_wall_normal),
## reused both to grab on in the first place (_try_start_climb, from the
## player's own current height) and to detect the top edge while climbing
## (_process_climbing, from above the player's own current grip).
func _climb_wall_present(check_position: Vector3) -> bool:
	var space_state := get_world_3d().direct_space_state
	var query := PhysicsRayQueryParameters3D.create(check_position, check_position - _climb_wall_normal * CLIMB_GRAB_CHECK_DISTANCE)
	query.collision_mask = 1
	query.exclude = [self]
	return not space_state.intersect_ray(query).is_empty()

## Releases the wall for any reason OTHER than reaching the top (hopping
## off with Space, or running out of stamina) -- apply_push distinguishes
## a deliberate shove away from the wall (Space) from just letting go
## (stamina exhausted, or reached the ground while climbing down), matching
## the explicit ask that those two read differently ("press space to hop
## off" vs "when it runs out you let go and fall").
func _end_climb(character: Node, apply_push: bool) -> void:
	_is_climbing = false
	synced_is_climbing = false
	if character != null and character.has_method("end_climb"):
		character.end_climb()
	if apply_push:
		velocity = _climb_wall_normal * CLIMB_HOP_OFF_OUTWARD_SPEED + Vector3(0, CLIMB_HOP_OFF_UPWARD_SPEED, 0)
	else:
		velocity = Vector3.ZERO
	_climb_wall_normal = Vector3.ZERO

## Reached the top of a climbable wall (see _process_climbing's own check) --
## freezes movement (see _physics_process's own _is_climb_topping_out
## early-return) and plays the one-shot mantle clip. character.gd's own
## climb_top_out carries real hip-position root motion (see its own
## comment) that visually sells the transition; this listens for
## anim_player's own animation_finished directly, same external-listener
## pattern vehicles/car.gd already established for entering_car/exiting_car
## (see its own _on_enter_finished), to know the exact moment to actually
## relocate the real physics body.
func _begin_climb_top_out() -> void:
	_is_climbing = false
	synced_is_climbing = false
	_is_climb_topping_out = true
	velocity = Vector3.ZERO
	var character := _get_character()
	if character != null and character.has_method("play_climb_top_out") and character.anim_player != null:
		character.play_climb_top_out()
		character.anim_player.animation_finished.connect(_on_climb_top_out_finished, CONNECT_ONE_SHOT)
	else:
		_complete_climb_top_out()

func _on_climb_top_out_finished(anim_name: StringName) -> void:
	if anim_name != "climb_top_out":
		return
	_complete_climb_top_out()

## Actually relocates the real CharacterBody3D up onto the ledge once the
## mantle clip finishes -- CLIMB_MANTLE_UP/FORWARD are a starting estimate,
## not trusted blindly (roof/ledge thickness varies by building), so a real
## downward raycast from that estimate snaps the final position onto
## whatever surface is actually there, the same "estimate then verify"
## approach _apply_fall_damage's own landing detection already relies on
## is_on_floor() for rather than a guessed height.
func _complete_climb_top_out() -> void:
	_is_climb_topping_out = false
	# Top-out triggers once the check point (CLIMB_TOP_CHECK_HEIGHT above
	# the player's own CURRENT grip, see _process_climbing's own check)
	# stops finding wall -- meaning the real wall top sits roughly that
	# far ABOVE wherever the player happened to be standing at that
	# instant, not right at their own current height. A real bug caught by
	# this file's own regression test: an earlier version only added
	# CLIMB_MANTLE_UP (a small ledge-clearance nudge) to the player's
	# CURRENT position, landing well short of the actual ledge in open
	# air -- the downward raycast below then found nothing to snap onto,
	# and gravity dropped the player straight back down the whole wall the
	# instant normal movement resumed.
	var mantle_target := global_position + Vector3(0, CLIMB_TOP_CHECK_HEIGHT + CLIMB_MANTLE_UP, 0) - _climb_wall_normal * CLIMB_MANTLE_FORWARD
	global_position = mantle_target
	var space_state := get_world_3d().direct_space_state
	var query := PhysicsRayQueryParameters3D.create(mantle_target + Vector3(0, 0.5, 0), mantle_target - Vector3(0, 3.0, 0))
	query.collision_mask = 1
	query.exclude = [self]
	var result := space_state.intersect_ray(query)
	if not result.is_empty():
		global_position.y = result["position"].y
	velocity = Vector3.ZERO
	_climb_wall_normal = Vector3.ZERO

func _try_start_dash(dash_input: Vector2) -> void:
	if _dash_time_remaining > 0.0 or stamina < DASH_STAMINA_COST:
		return
	stamina -= DASH_STAMINA_COST
	var facing_input := dash_input if dash_input.length() > 0.01 else Vector2(0, -1)
	var local_dir := Vector3(facing_input.x, 0, facing_input.y).normalized()
	_dash_direction = (transform.basis * local_dir).normalized()
	_dash_time_remaining = DASH_DURATION_SECONDS
	# i-frames for the burst's own duration -- this is what "dodges out of a
	# combo" concretely means (see take_damage's own _invulnerable_remaining
	# early-return): a hit landing during the window whiffs entirely, nothing
	# lands to continue the attacker's chain. maxf'd rather than overwritten,
	# same "never shorten a window already running" reasoning stun() uses,
	# though in practice this only ever runs from a fresh dash anyway (the
	# _dash_time_remaining guard above already blocks re-triggering mid-dash).
	_invulnerable_remaining = maxf(_invulnerable_remaining, DASH_INVULNERABILITY_SECONDS)
	# The other named escape from a landed combo (see _hitstun_remaining's
	# own comment) -- dashing actively cancels the lock rather than just
	# outlasting it, same as block() does at its own call site above.
	_hitstun_remaining = 0.0
	dash_player.stream = DashSounds[randi() % DashSounds.size()]
	dash_player.play()

	# Only backward has a dedicated DASH clip today (see character.gd's own
	# dash()) -- forward/left/right intentionally do nothing here, leaving
	# them exactly as they already worked (this same function's own
	# velocity override plus player.gd's forced run animation below).
	var character := _get_character()
	if character != null and character.has_method("dash"):
		var dash_dir := _direction_from_input(facing_input)
		character.dash(dash_dir)
		if Net.is_networked():
			_rpc_dash.rpc(dash_dir)

## Which of the 4 horizontal directions a (right, back) input vector
## represents, in the SAME convention _physics_process's own input_dir
## already uses -- shared by both the dash burst above (which direction to
## lunge) and _physics_process's own move_direction (which of character.gd's
## walk/walk_backward/walk_left/walk_right shows, and whether sprint/bike-
## speed-pedal speed is allowed -- see its own comment). Whichever axis
## dominates decides forward/backward vs left/right -- a plain single-key
## press (only one of WASD held) always lands cleanly on one of the four; a
## diagonal (e.g. W+D together) picks whichever was pressed harder/more
## directly, the same tie-breaking a real analog stick would need.
func _direction_from_input(input: Vector2) -> String:
	if absf(input.y) >= absf(input.x):
		return "backward" if input.y > 0.0 else "forward"
	else:
		return "right" if input.x > 0.0 else "left"

## Replicated one-shot moves: each mirrors a real character.X() call made
## above, on the exact same frame, from the authority's own machine (see
## each call site's own "if Net.is_networked(): _rpc_X.rpc(...)" line right
## after the real call). "authority" restricts who's even allowed to invoke
## these to this specific body's own owning peer (enforced by the engine,
## not just by convention) -- matches how set_multiplayer_authority(peer_id)
## is already assigned per spawned player in main.gd. "call_remote" (no
## call_local) since the authority's own screen already played the real
## thing directly; this just replicates that same call out to everyone
## else. All reliable -- unlike the continuous synced_is_walking state
## above, a dropped punch/jump/dash isn't "corrected next frame," it just
## never happened for that peer.
@rpc("authority", "call_remote", "reliable")
func _rpc_land() -> void:
	var character := _get_character()
	if character != null and character.has_method("land"):
		character.land()

@rpc("authority", "call_remote", "reliable")
func _rpc_jump(is_sprinting: bool, is_moving: bool = false) -> void:
	var character := _get_character()
	if character != null and character.has_method("jump"):
		character.jump(is_sprinting, is_moving)

@rpc("authority", "call_remote", "reliable")
func _rpc_start_crouch() -> void:
	var character := _get_character()
	if character != null and character.has_method("start_crouch"):
		character.start_crouch()

@rpc("authority", "call_remote", "reliable")
func _rpc_end_crouch() -> void:
	var character := _get_character()
	if character != null and character.has_method("end_crouch"):
		character.end_crouch()

@rpc("authority", "call_remote", "reliable")
func _rpc_dash(direction: String) -> void:
	var character := _get_character()
	if character != null and character.has_method("dash"):
		character.dash(direction)

@rpc("authority", "call_remote", "reliable")
func _rpc_end_dash() -> void:
	var character := _get_character()
	if character != null and character.has_method("end_dash"):
		character.end_dash()

@rpc("authority", "call_remote", "reliable")
func _rpc_punch(use_left_hand: bool) -> void:
	var character := _get_character()
	if character != null and character.has_method("punch"):
		character.punch(use_left_hand)

@rpc("authority", "call_remote", "reliable")
func _rpc_heavy_punch() -> void:
	var character := _get_character()
	if character != null and character.has_method("heavy_punch"):
		character.heavy_punch()

@rpc("authority", "call_remote", "reliable")
func _rpc_block() -> void:
	var character := _get_character()
	if character != null and character.has_method("block"):
		character.block()

## The one deliberately reversed-direction RPC in this file: every other
## @rpc here is "authority" (the real owner acts, then tells everyone else)
## -- this one is "any_peer" because the CALLER is an ATTACKER on some OTHER
## machine, who is never this body's own authority (see _punch()'s own
## rpc_id() call site, which addresses this directly at whichever peer
## actually owns the body that got hit, resolved via
## target.get_multiplayer_authority()). Runs on the REAL defending peer's
## own authoritative copy, so take_damage() finally does something
## meaningful: health/hitstun/invulnerability/blocking are all read and
## mutated on the machine where they're actually kept correct, not on some
## other peer's stale replicated copy (which is the actual root cause this
## whole RPC pair fixes -- see _punch()'s own comment).
##
## was_blocking is captured BEFORE calling take_damage() -- it reads the
## exact same character.is_blocking() take_damage() itself checks first
## thing in its own blocking branch, with no mutation in between (this is
## single-threaded GDScript), so this is provably the same value, just
## captured here so it can be included in the broadcast below.
##
## is_multiplayer_authority()'s early-return is defensive insurance -- the
## rpc_id() call site already unicasts only to the resolved owning peer, so
## this should always already be true; cheap to check anyway.
@rpc("any_peer", "call_remote", "reliable")
func _rpc_request_hit(amount: float, attacker_peer_id: int, hitstun_seconds: float, is_finisher: bool) -> void:
	if not is_multiplayer_authority():
		return
	var attacker_node := _find_player_by_peer_id(attacker_peer_id)
	var character := _get_character()
	var was_blocking: bool = character != null and character.has_method("is_blocking") and character.is_blocking()
	var was_dead := is_dead
	take_damage(amount, attacker_node, hitstun_seconds, is_finisher)
	if Net.is_networked():
		_rpc_play_hit_result.rpc(was_blocking, is_finisher, is_dead and not was_dead, attacker_peer_id)

## Broadcast by the real defending peer (see _rpc_request_hit above) once it
## knows the true outcome of a hit -- reaches every OTHER peer, including the
## real attacker's own machine, handled by two independent branches:
##
## 1. Visual replay: block_impact()/hit_reaction()/the finisher's own
##    ragdoll+knockback, applied to `self` -- on EVERY receiving machine,
##    `self` here is that peer's own local copy of the DEFENDER (the node
##    this RPC is being called on), regardless of who's receiving it, so
##    this branch alone is what makes the hit visible on the attacker's own
##    screen too. Skipped entirely when did_die is true: _rpc_died (see its
##    own comment) already calls character.ragdoll(), and replaying
##    hit_reaction()/block_impact() (which call anim_player.play()) on an
##    already-ragdolling character would just fight its own frozen
##    anim_player/procedurally-driven bones for no visible benefit.
## 2. Stun payback: deliberately NOT keyed off `self` -- attacker_peer_id ==
##    multiplayer.get_unique_id() is only true on the one machine that
##    actually threw the punch. There (and only there), it resolves ITS OWN
##    real local player (NOT the defender copy `self` refers to inside this
##    handler -- an easy mix-up, since every other line here operates on
##    self) and stuns THAT node. Runs regardless of did_die, matching
##    take_damage()'s own internal order (the block-stun payback happens
##    independent of whether the hit also turned out to be lethal).
@rpc("authority", "call_remote", "reliable")
func _rpc_play_hit_result(was_blocked: bool, is_finisher: bool, did_die: bool, attacker_peer_id: int) -> void:
	if not did_die:
		var character := _get_character()
		if was_blocked:
			if character != null and character.has_method("block_impact"):
				character.block_impact()
		else:
			if character != null and character.has_method("hit_reaction"):
				character.hit_reaction()
			if is_finisher:
				_begin_finisher_ragdoll(_find_player_by_peer_id(attacker_peer_id))
	if was_blocked and attacker_peer_id == multiplayer.get_unique_id():
		var my_own_player := _find_player_by_peer_id(attacker_peer_id)
		if my_own_player != null and my_own_player.has_method("stun"):
			my_own_player.stun(BLOCK_STUN_SECONDS)

## Fired by ui/chat_box.gd the instant a message is actually sent (empty
## messages never reach this -- see its own _on_text_submitted). Shows the
## bubble locally right away (no reason to wait on a network round-trip to
## see your own message) and broadcasts it to every other peer, same
## "authority speaks, everyone else's copy plays it back" pattern every
## other one-shot action in this file already uses.
## "/dance" and "/breakdance" (case-insensitive, surrounding whitespace
## ignored) are chat COMMANDS, not real messages -- intercepted here before
## either ever reaches show_speech_bubble()/the chat RPC, so typing one
## plays the matching emote instead of putting the literal command text up
## in a speech bubble. Not the dev console's own "/" commands (see
## ui/dev_console.gd) -- these are typed in the ordinary chat box,
## deliberately reachable in a real release build the same way the rest of
## chat already is, unlike the console.
const DANCE_COMMAND := "/dance"
const BREAKDANCE_COMMAND := "/breakdance"
const CLAIM_COMMAND := "/claim"

func _on_chat_message_sent(text: String) -> void:
	var normalized := text.strip_edges().to_lower()
	if normalized == DANCE_COMMAND:
		_try_emote("dance")
		return
	if normalized == BREAKDANCE_COMMAND:
		_try_emote("breakdance")
		return
	if normalized == CLAIM_COMMAND:
		try_claim_building()
		return
	var character := _get_character()
	if character != null and character.has_method("show_speech_bubble"):
		character.show_speech_bubble(text)
	if Net.is_networked():
		_rpc_chat_message.rpc(text)

@rpc("authority", "call_remote", "reliable")
func _rpc_chat_message(text: String) -> void:
	var character := _get_character()
	if character != null and character.has_method("show_speech_bubble"):
		character.show_speech_bubble(text)

## Plays an emote (see character.gd's own dance()/breakdance(), both of
## which just forward to the shared _play_emote(anim_name)) locally right
## away (same "authority acts now, everyone else's copy plays it back"
## pattern every other one-shot action in this file already uses, e.g.
## _rpc_punch/_rpc_block) and replicates it to every other connected peer
## over its own dedicated RPC -- deliberately NOT reusing _rpc_chat_message's
## own RPC, since remote peers must NOT show a "/dance"/"/breakdance" speech
## bubble, only the animation itself. emote_name is "dance" or "breakdance",
## matching character.gd's own method names 1:1.
func _try_emote(emote_name: String) -> void:
	var character := _get_character()
	if character != null and character.has_method(emote_name):
		character.call(emote_name)
	if Net.is_networked():
		_rpc_emote.rpc(emote_name)

@rpc("authority", "call_remote", "reliable")
func _rpc_emote(emote_name: String) -> void:
	var character := _get_character()
	if character != null and character.has_method(emote_name):
		character.call(emote_name)

## Bedrock-style unarmed attack: swing immediately (on release -- see the
## press/hold/release tracking above), then start the cooldown -- see
## PUNCH_COOLDOWN_SECONDS for why there's no reward for waiting longer than
## that before clicking again. Hit detection is a single raycast AIMED using
## the camera's own look direction (matching the crosshair, see ui/hud.tscn,
## same as Bedrock's own crosshair-based melee) but ORIGINATING near the
## player's own body, not the camera itself -- the camera sits a full
## spring_length (4m) behind the player on its boom, so a ray that both
## started AND was ranged from the camera would need PUNCH_RANGE to exceed
## that boom length just to reach the player's own position, let alone
## anything in front of them. Damages the first thing hit that exposes
## take_damage() -- today that's only ever this same take_damage() on
## another Player/npc/cpu.gd, but the check is duck-typed rather than
## hardcoded to CharacterBody3D/Player specifically so it keeps working once
## more targets show up. is_heavy swaps in HeavyPunchAnim and
## HEAVY_PUNCH_DAMAGE instead of the regular jab -- doesn't advance
## _punch_count/use_left_hand at all, since a heavy punch is a single
## committed animation, not one of the alternating left/right jabs, and
## incrementing the counter anyway would just throw off which regular punch
## lands on PUNCH_LEFT_HAND_EVERY_NTH.
func _punch(character: Node, is_heavy: bool = false) -> void:
	_punch_cooldown_remaining = PUNCH_COOLDOWN_SECONDS
	# Defaults match a committed heavy_punch() haymaker -- it's a single
	# uncombo'd move (see character.gd's own heavy_punch() comment), no
	# hitstun/finisher of its own. The chain-driven regular-punch branch
	# below overwrites these from character.gd's own get_last_attack_combo_info(),
	# whatever that particular swing actually carries (see combo_data.gd).
	var damage := HEAVY_PUNCH_DAMAGE if is_heavy else PUNCH_DAMAGE
	var hitstun := 0.0
	var is_finisher := false
	if character != null:
		if is_heavy and character.has_method("heavy_punch"):
			character.heavy_punch()
			if Net.is_networked():
				_rpc_heavy_punch.rpc()
		elif not is_heavy and character.has_method("punch"):
			_punch_count += 1
			var use_left_hand := _punch_count % PUNCH_LEFT_HAND_EVERY_NTH == 0
			character.punch(use_left_hand)
			if Net.is_networked():
				_rpc_punch.rpc(use_left_hand)
			if character.has_method("get_last_attack_combo_info"):
				var info: Dictionary = character.get_last_attack_combo_info()
				damage = PUNCH_DAMAGE * float(info.get("damage_scale", 1.0))
				hitstun = HITSTUN_SECONDS * float(info.get("hitstun_scale", 1.0))
				is_finisher = bool(info.get("finisher", false))
	damage = apply_strength(damage)

	var space_state := get_world_3d().direct_space_state
	var aim_direction := -camera.global_transform.basis.z
	var from := global_position + Vector3(0, PUNCH_ORIGIN_HEIGHT, 0)
	var to := from + aim_direction * PUNCH_RANGE
	var query := PhysicsRayQueryParameters3D.create(from, to)
	query.collision_mask = 1 | 4 # world geometry + other players' bodies
	query.exclude = [self]
	var result := space_state.intersect_ray(query)
	if result.is_empty():
		return
	var target: Object = result["collider"]
	# Checked first, and returns immediately either way -- a landed punch
	# on breakable glass (see city/building_glass.gd) still counts as a
	# genuine hit for the player's own purposes (the glass itself plays its
	# own crack/break sound), it just isn't a damage-dealing one, so it
	# shouldn't fall through to punch_impact_player's own sound below (that
	# would layer a fist-thud on top of the glass's own audio).
	if target.has_method("try_break_glass"):
		target.try_break_glass(result["position"], result["normal"])
		return
	if not target.has_method("take_damage"):
		return
	# A remote peer's OWN networked player is a real, correctly-positioned
	# CharacterBody3D on our own machine (position/rotation replicate, see
	# _setup_replication()), but it's a non-authoritative COPY -- calling
	# take_damage() on it directly would mutate health/hitstun/knockback on a
	# body whose own physics_process is disabled (see _ready()'s
	# set_physics_process(false) for non-authority), doing nothing meaningful
	# and never telling the REAL defending peer, on their own machine, that
	# they were hit at all. This was the actual root cause of a real reported
	# bug: "when I knock the other one onto the ground, on the other tab the
	# player hasn't taken any damage and hasn't been moved." Route it to the
	# real owner instead via a targeted RPC (see _rpc_request_hit's own
	# comment for what happens on the receiving end); everything else --
	# NPCs (never in the "players" group, so the `and` short-circuits before
	# ever reaching is_multiplayer_authority()), ourselves (already excluded
	# via query.exclude=[self] above), and singleplayer -- keeps calling
	# take_damage() directly exactly as before, unchanged.
	if Net.is_networked() and target.is_in_group("players") and not target.is_multiplayer_authority():
		target.rpc_id(target.get_multiplayer_authority(), "_rpc_request_hit", damage, get_multiplayer_authority(), hitstun, is_finisher)
	else:
		target.take_damage(damage, self, hitstun, is_finisher)
	# Only on an actual landed hit -- a whiff (nothing in range, or
	# hitting plain world geometry) stays silent. Picked fresh each
	# punch rather than cycling in order, so spam-clicking doesn't
	# settle into an obviously repeating pattern. Plays immediately
	# regardless of which branch above fired -- purely a local/cosmetic
	# effect on the attacker's own screen, same as always.
	punch_impact_player.stream = PunchImpactSounds[randi() % PunchImpactSounds.size()]
	punch_impact_player.play()

## A parallel function to _punch() above, NOT a modification of it -- a
## bottle is a single fixed-damage swing sourced from its own current
## durability state (weapons/glass_bottle.gd's own get_melee_damage()), not
## character.gd's combo-chain get_last_attack_combo_info(), so this
## deliberately doesn't call _punch() or share its body. Reuses _punch()'s
## own target-routing tail verbatim otherwise (same PUNCH_RANGE/
## PUNCH_ORIGIN_HEIGHT reach, same networked-hit routing) -- see its own
## comment for why that routing exists at all.
func _try_swing_bottle(character: Node) -> void:
	if _equipped_bottle == null:
		return
	var damage: float = _equipped_bottle.get_melee_damage()
	if damage <= 0.0:
		return # fully broken -- inert, same "denied action does nothing" convention every other gated action here already uses
	damage = apply_strength(damage)
	_punch_cooldown_remaining = BOTTLE_MELEE_COOLDOWN_SECONDS
	if character != null and character.has_method("punch"):
		character.punch(false)
		if Net.is_networked():
			_rpc_punch.rpc(false)

	var space_state := get_world_3d().direct_space_state
	var aim_direction := -camera.global_transform.basis.z
	var from := global_position + Vector3(0, PUNCH_ORIGIN_HEIGHT, 0)
	var to := from + aim_direction * PUNCH_RANGE
	var query := PhysicsRayQueryParameters3D.create(from, to)
	query.collision_mask = 1 | 4
	query.exclude = [self]
	var result := space_state.intersect_ray(query)
	if result.is_empty():
		return
	var target: Object = result["collider"]
	# Same "glass hit doesn't advance bottle durability" reasoning as a
	# whiff below -- see _punch()'s own identical branch for why this
	# returns immediately rather than falling through.
	if target.has_method("try_break_glass"):
		target.try_break_glass(result["position"], result["normal"])
		return
	if not target.has_method("take_damage"):
		return
	# IDENTICAL routing to _punch()'s own -- see its own comment.
	if Net.is_networked() and target.is_in_group("players") and not target.is_multiplayer_authority():
		target.rpc_id(target.get_multiplayer_authority(), "_rpc_request_hit", damage, get_multiplayer_authority(), 0.0, false)
	else:
		target.take_damage(damage, self, 0.0, false)
	# Only advances durability on an actual LANDED hit -- a whiff against
	# empty air/a wall does nothing, matching the user's own "when you hit
	# someone with it" wording.
	_equipped_bottle.register_landed_melee_hit()
	# Bleed effect applied uniformly to every landed bottle hit (intact
	# bash, broken slash, and a thrown impact alike -- see
	# weapons/thrown_bottle.gd's own impact handling) -- the original
	# request placed this specifically in the THROWING paragraph, but
	# restricting it to only thrown hits would read as an inconsistent
	# half-implementation of "a glass bottle draws blood," and the raycast
	# above already hands back exactly what BloodSplatter.spawn() needs at
	# zero extra cost either way.
	BloodSplatter.spawn(result["position"], result["normal"], get_tree())
	punch_impact_player.stream = PunchImpactSounds[randi() % PunchImpactSounds.size()]
	punch_impact_player.play()

## Right-click (aim) + punch, while a bottle is equipped and still INTACT
## (see weapons/glass_bottle.gd's own can_throw()) -- throws it. Spawns the
## real flying RigidBody3D locally right away (own screen, is_authoritative
## true), then (if genuinely networked) broadcasts the same spawn transform/
## velocity so every OTHER peer spawns their own independently-simulated,
## purely cosmetic copy (see weapons/thrown_bottle.gd's own class comment
## on why only the throwing peer's own original ever applies damage).
## Consumes the weapon -- per explicit product decision ("you only had one
## physical bottle"), throwing always leaves the player bare-handed
## afterward, the one real "unequip" this project has.
##
## Throw origin/direction is body-relative (PUNCH_ORIGIN_HEIGHT off
## global_position), NOT camera-relative the way the gun's own hitscan
## raycast is -- a hitscan can start at the camera since it's instant and
## invisible, but the camera sits a full spring_length (~4m) behind the
## player on its boom (see two_handed_sword.gd's own documented camera-
## geometry gotcha), and a THROWN bottle is a real visible object -- spawning
## it at the camera would visibly fly it through the player's own body.
func _try_throw_bottle() -> void:
	if _equipped_bottle == null or not _equipped_bottle.can_throw():
		return
	var direction: Vector3 = -camera.global_transform.basis.z
	var origin: Vector3 = global_position + Vector3(0, PUNCH_ORIGIN_HEIGHT, 0) + direction * 0.5
	var spawn_xform := Transform3D(Basis(), origin)
	var velocity := direction * BOTTLE_THROW_SPEED
	_spawn_thrown_bottle(spawn_xform, velocity, true, self)
	if Net.is_networked():
		_rpc_throw_bottle.rpc(spawn_xform, velocity, get_multiplayer_authority())
	_equipped_bottle.queue_free()
	_equipped_bottle = null
	var character := _get_character()
	if character != null and character.has_method("set_wielding_bottle"):
		character.set_wielding_bottle(false)

## The grenade's own throw. Same shape as _try_throw_bottle above (same
## camera-forward direction, same origin, same "spawn locally, broadcast,
## then consume the held prop" ordering) -- see weapons/grenade.gd's own
## class comment for why the second throwable deliberately reuses the
## first's gesture rather than inventing another.
##
## One real difference: a grenade is CONSUMED, so the inventory count has to
## come down. The bottle never needed this (it's a durable single item that
## leaves the inventory entirely on equip), so this is grenade-specific
## rather than something to factor out of the bottle path.
func _try_throw_grenade() -> void:
	if _equipped_grenade == null or not _equipped_grenade.can_throw():
		return
	var direction: Vector3 = -camera.global_transform.basis.z
	var origin: Vector3 = global_position + Vector3(0, PUNCH_ORIGIN_HEIGHT, 0) + direction * 0.5
	var spawn_xform := Transform3D(Basis(), origin)
	var velocity := direction * GRENADE_THROW_SPEED
	_spawn_thrown_grenade(spawn_xform, velocity, true, self)
	if Net.is_networked():
		_rpc_throw_grenade.rpc(spawn_xform, velocity, get_multiplayer_authority())
	_equipped_grenade.queue_free()
	_equipped_grenade = null
	var character := _get_character()
	if character != null and character.has_method("set_wielding_bottle"):
		character.set_wielding_bottle(false)
	_consume_thrown_grenade()

## Spends one grenade from the inventory and, if any are left, puts the next
## one straight into the player's hand -- so throwing a stack of grenades is
## throw/throw/throw, not throw-open-inventory-re-equip each time. Running
## out simply leaves the player empty-handed.
func _consume_thrown_grenade() -> void:
	var inv: Node = get_node_or_null("Inventory")
	if inv == null:
		return
	if inv.has_method("consume_equipped_item"):
		inv.call("consume_equipped_item", "grenade", self)

func _spawn_thrown_grenade(xform: Transform3D, vel: Vector3, is_authoritative: bool, thrower: Node) -> void:
	var thrown: RigidBody3D = ThrownGrenadeScene.instantiate()
	get_tree().current_scene.add_child(thrown)
	thrown.global_transform = xform
	thrown.linear_velocity = vel
	thrown.is_authoritative = is_authoritative
	thrown.thrower = thrower

## Broadcast by the throwing peer -- every other peer spawns its own local
## replica, same "broadcast initial conditions once, simulate independently"
## pattern _rpc_throw_bottle already established. is_authoritative=false on
## every replica, so only the thrower's own copy ever deals blast damage
## (see weapons/thrown_grenade.gd's own class comment).
@rpc("any_peer", "call_remote", "reliable")
func _rpc_throw_grenade(spawn_transform: Transform3D, linear_velocity: Vector3, thrower_peer_id: int) -> void:
	_spawn_thrown_grenade(spawn_transform, linear_velocity, false, _find_player_by_peer_id(thrower_peer_id))

func _spawn_thrown_bottle(xform: Transform3D, vel: Vector3, is_authoritative: bool, thrower: Node) -> void:
	var thrown: RigidBody3D = ThrownBottleScene.instantiate()
	get_tree().current_scene.add_child(thrown)
	thrown.global_transform = xform
	thrown.linear_velocity = vel
	thrown.is_authoritative = is_authoritative
	thrown.set_thrower(thrower)

## Broadcast by the throwing peer (see _try_throw_bottle above) -- every
## OTHER peer spawns their own local, independently-simulated (NOT
## position-replicated frame-by-frame) copy of the flying bottle, same
## "broadcast initial conditions once, let each peer's own physics engine
## simulate independently" pattern _begin_finisher_ragdoll's own seeded
## knockback already established. thrower_peer_id travels alongside the
## transform/velocity so every receiving peer can resolve ITS OWN local
## copy of the thrower via the existing _find_player_by_peer_id().
@rpc("authority", "call_remote", "reliable")
func _rpc_throw_bottle(spawn_transform: Transform3D, linear_velocity: Vector3, thrower_peer_id: int) -> void:
	_spawn_thrown_bottle(spawn_transform, linear_velocity, false, _find_player_by_peer_id(thrower_peer_id))

## Loops the matching walking/running sound while actually moving on the
## ground, and stops both otherwise -- gated on is_on_floor() (not just
## is_moving) so a sprint-jump's carried-over momentum (see
## _air_locked_sprinting) doesn't also carry the footstep sound through
## mid-air, where there's no ground to be stepping on.
func _update_footstep_audio(should_play: bool, sprinting: bool) -> void:
	var should_run := should_play and sprinting
	var should_walk := should_play and not sprinting

	if should_walk and not walk_sound_player.playing:
		walk_sound_player.play()
	elif not should_walk and walk_sound_player.playing:
		walk_sound_player.stop()

	if should_run and not run_sound_player.playing:
		run_sound_player.play()
	elif not should_run and run_sound_player.playing:
		run_sound_player.stop()

## Called by city_chunk.gd's own per-building indoor-detection Area3D (see
## _build_building's own comment) the instant this body's own collision
## shape enters a building's footprint -- duck-typed has_method() call from
## a plain body_entered signal, same convention every other Area3D trigger
## in this project already uses (building_door.gd's own _nearby_player,
## building_glass.gd's own walkthrough area). Counts rather than a plain
## bool (see _indoor_building_count's own comment) so overlapping building
## footprints can't cancel each other out.
## indoor_area is the building's own trigger volume (see city_chunk.gd),
## kept in a stack rather than only counted so "which building am I in"
## has an answer -- /claim needs the real footprint to orbit and to put a
## spawn inside. Optional so anything that ever calls this without one
## (tests, a future non-city interior) still works; the count, and
## therefore the ambience/darkness fades, behave exactly as before.
func enter_building(indoor_area: Node3D = null) -> void:
	if not is_multiplayer_authority():
		return
	_indoor_areas.append(indoor_area)
	if _indoor_areas.size() == 1:
		_fade_city_ambience(false, CITY_AMBIENCE_SLOW_FADE_SECONDS)
		_fade_indoor_darkness(true)

## Companion to enter_building() -- only actually fades the ambience back IN
## once EVERY building footprint this body was inside has been left, not on
## the first exit signal alone (see _indoor_building_count's own comment).
## Fades back in noticeably FASTER than enter_building()'s own fade-out (see
## CITY_AMBIENCE_FAST_FADE_SECONDS's own comment) -- a real explicit ask.
func exit_building(indoor_area: Node3D = null) -> void:
	if not is_multiplayer_authority():
		return
	# Removes THIS building's own entry rather than just decrementing, so
	# leaving the outer of two overlapping footprints still leaves the inner
	# one as the current building. rfind so a duplicate entry (two triggers
	# for the same area) pops the most recent, matching a stack.
	var index := _indoor_areas.rfind(indoor_area)
	if index >= 0:
		_indoor_areas.remove_at(index)
	elif not _indoor_areas.is_empty():
		# Unknown area (entered before this body started tracking, or a
		# caller that passed nothing) -- still has to decrement, or the
		# count sticks above zero and the player never reads as outdoors.
		_indoor_areas.remove_at(_indoor_areas.size() - 1)
	if _indoor_areas.is_empty():
		_fade_city_ambience(true, CITY_AMBIENCE_FAST_FADE_SECONDS)
		_fade_indoor_darkness(false)

## Rides the exact same indoor/outdoor trigger the city ambience already
## uses (see enter_building above) -- stepping inside drops the world's
## ambient light toward INDOOR_AMBIENT_DIM so interiors are genuinely dark
## and a light source (see items/flashlight.gd) becomes worth carrying,
## instead of every windowless basement being as evenly lit as the street.
## Tweened rather than snapped so it reads as eyes adjusting.
func _fade_indoor_darkness(indoors: bool) -> void:
	var cycle: Node = _find_day_night_cycle()
	if cycle == null:
		return
	var target: float = INDOOR_AMBIENT_DIM if indoors else 1.0
	var tw := create_tween()
	tw.tween_method(cycle.set_indoor_dim, cycle.get("_indoor_dim"), target, INDOOR_DIM_FADE_SECONDS)

## The city's own DayNightCycle node, which owns the ambient light (see its
## set_indoor_dim). Looked up rather than held as a reference because the
## player is spawned INTO an already-built city by main.gd and has no
## direct handle on its internals -- and legitimately has none at all in
## the test scenes, hence the null-tolerant return.
func _find_day_night_cycle() -> Node:
	var city := get_parent()
	if city == null:
		return null
	var cycle := city.get_node_or_null("DayNightCycle")
	if cycle != null and cycle.has_method("set_indoor_dim"):
		return cycle
	return null

## Shared by _ready()'s own initial fade-in and enter_building()/
## exit_building() above -- true fades UP to CITY_AMBIENCE_MAX_VOLUME_DB
## (starting playback from silence first if it isn't already looping),
## false fades DOWN to silence and stops. Mirrors title_screen.gd's own
## fade_in_rain()/fade_out_rain() pair exactly (same -80.0 dB "effectively
## silent" convention, same tween-then-stop shape), just folded into one
## function since both directions share every line except which way
## volume_db moves and how long it takes (see `duration`'s own callers).
func _fade_city_ambience(audible: bool, duration: float) -> void:
	var tw := create_tween()
	if audible:
		if not city_ambience_player.playing:
			city_ambience_player.stream.loop = true
			city_ambience_player.volume_db = -80.0
			city_ambience_player.play()
		tw.tween_property(city_ambience_player, "volume_db", CITY_AMBIENCE_MAX_VOLUME_DB, duration)
	else:
		tw.tween_property(city_ambience_player, "volume_db", -80.0, duration)
		tw.tween_callback(city_ambience_player.stop)

## Drains while actually sprinting (moving + holding the sprint key),
## regenerates otherwise. Once it hits empty, sprinting is locked out (see
## is_sprinting above) until it climbs back up to
## STAMINA_EXHAUSTED_RECOVER_FRACTION of max, not just above zero -- avoids
## sprint flickering on/off frame to frame while stamina hovers near empty.
func _update_stamina(delta: float, wants_sprint: bool) -> void:
	# Dev-only /infinitesprint (see _infinite_stamina's own comment) -- skip
	# the drain branch entirely; regen below still runs normally either way.
	if wants_sprint and not _infinite_stamina and not _stamina_exhausted and stamina > 0.0:
		stamina = maxf(0.0, stamina - STAMINA_DRAIN_PER_SEC * delta)
		if stamina <= 0.0:
			_stamina_exhausted = true
	else:
		stamina = minf(max_stamina, stamina + STAMINA_REGEN_PER_SEC * delta)
		if _stamina_exhausted and stamina >= max_stamina * STAMINA_EXHAUSTED_RECOVER_FRACTION:
			_stamina_exhausted = false

## impact_velocity_y is the real pre-impact downward speed, captured by the
## caller BEFORE this same frame's move_and_slide() call clipped velocity.y
## against the floor's own collision normal (see _physics_process's own
## call site) -- by the time this function runs, self.velocity.y has
## already been reset to ~0 by that landing collision, so reading it here
## instead would always see a body that "just stopped," not one that
## actually fell.
func _apply_fall_damage(fall_distance: float, impact_velocity_y: float) -> void:
	if fall_distance <= SAFE_FALL_DISTANCE:
		return
	fall_impact_player.play()
	var damage := (fall_distance - SAFE_FALL_DISTANCE) * FALL_DAMAGE_PER_METER
	# Restores the real pre-impact fall speed ONLY on a lethal fall, right
	# before take_damage() below triggers _die() -> character.ragdoll()
	# (which reads this body's OWN velocity to seed the ragdoll's starting
	# momentum) -- a real reported complaint: a lethal fall used to ragdoll
	# as if dropped from a dead stop instead of actually carrying the fall's
	# own momentum into the corpse. Left deliberately untouched on a
	# SURVIVED fall: this body keeps moving normally afterward (nothing
	# else here resets velocity.y while grounded), so re-injecting a big
	# downward speed there would just be a stray value for ordinary movement
	# code to fight for a frame, with no ragdoll around to ever consume it.
	if damage >= health:
		# Clamped, not the raw value -- see MAX_DEATH_FALL_IMPACT_SPEED's own
		# comment for why an uncapped impact speed blows up the ragdoll.
		velocity.y = maxf(impact_velocity_y, -MAX_DEATH_FALL_IMPACT_SPEED)
	take_damage(damage)

## attacker is null for sources with no attacking Player/npc (fall damage,
## see _apply_fall_damage) -- only an actual landed punch (see _punch) passes
## one, since only that case can trigger a block-stun payback. Duck-typed
## (has_method) same as everywhere else this project checks a target/attacker
## across Player/npc/cpu.gd, rather than a hardcoded type check.
##
## hitstun_seconds/is_finisher are new, safely-defaulted params -- every
## existing caller (fall damage, NPC bites) is unaffected unless it opts in;
## only _punch()'s own chain-driven branch actually passes real values (see
## its own comment). Both are only ever consulted on a LANDED, unblocked hit
## -- a block absorbs the damage bonus AND the hitstun/finisher this hit
## would otherwise have carried, same as it already absorbs raw damage.
func take_damage(amount: float, attacker: Node = null, hitstun_seconds: float = 0.0, is_finisher: bool = false) -> void:
	if is_dead or amount <= 0.0:
		return
	# Dev-only /infinitehealth (see _infinite_health's own comment) -- same
	# "whiffs entirely" early return as the i-frame check just below, not
	# just clamped-at-1-hp, so nothing reacts as if a hit actually landed.
	if _infinite_health:
		return
	# Dash i-frames or the finisher's own ragdoll-flight-plus-recovery-grace
	# window (see _invulnerable_remaining's own comment) -- the hit whiffs
	# entirely: no damage, no hit_reaction/block_impact, no hitstun. This is
	# what "dodging out of a combo" and "can't be re-comboed the instant you
	# stand back up" both concretely mean.
	if _invulnerable_remaining > 0.0:
		return
	var character := _get_character()
	var was_blocked: bool = character != null and character.has_method("is_blocking") and character.is_blocking()
	if was_blocked:
		amount *= BLOCK_DAMAGE_MULTIPLIER
		# block_impact()/hit_reaction() are both no-ops unless the defender
		# is wielding the sword (see their own comments) -- the bare-handed
		# system has never had a block/hit reaction clip to play at all, so
		# this leaves unarmed combat completely unchanged.
		if character.has_method("block_impact"):
			character.block_impact()
		if attacker != null and attacker.has_method("stun"):
			attacker.stun(BLOCK_STUN_SECONDS)
	else:
		if character != null and character.has_method("hit_reaction"):
			character.hit_reaction()
		if is_finisher:
			_begin_finisher_ragdoll(attacker)
		elif hitstun_seconds > 0.0:
			_hitstun_remaining = maxf(_hitstun_remaining, hitstun_seconds)
	health = maxf(0.0, health - amount)
	if health <= 0.0:
		# A finisher already knocked the body back into its own ragdoll
		# flight above (_begin_finisher_ragdoll already called
		# character.ragdoll()) -- _die() below is idempotent against that
		# (ragdoll() itself no-ops once _is_ragdolled is true), so only a
		# PLAIN landed killing blow needs its own knockback applied here.
		if not was_blocked and not is_finisher:
			_apply_death_knockback(attacker)
		_die()

## Called on the ATTACKER by a defender's successful block (see take_damage
## above) -- see _stun_remaining's own comment for what actually gets gated
## by this. maxf rather than a flat overwrite so re-stunning an
## already-stunned attacker (e.g. they throw another punch the instant they
## can move again and get blocked a second time) can't ever shorten a stun
## already in progress.
func stun(duration: float) -> void:
	_stun_remaining = maxf(_stun_remaining, duration)

## Combo finisher (see combo_data.gd's own "finisher" flag, only ever true on
## a chain's last stage) -- called from take_damage() on a LANDED (unblocked)
## finishing hit. Knocks the defender away from the attacker and reuses
## character.gd's existing real physics ragdoll (the same one death/respawn
## already uses) but auto-recovers on its own timer instead of ending in
## _die()/respawn() -- see _end_finisher_ragdoll.
func _begin_finisher_ragdoll(attacker: Node) -> void:
	var character := _get_character()
	if character == null or not character.has_method("ragdoll"):
		return
	var away := -global_transform.basis.z
	if attacker != null and attacker is Node3D:
		var flat := global_position - (attacker as Node3D).global_position
		flat.y = 0.0
		if flat.length_squared() > 0.0001:
			away = flat.normalized()
	# Set on this body BEFORE ragdoll() runs so character.gd's own ragdoll
	# rig (which gives the torso an initial spin off this body's linear/
	# angular velocity, see RAGDOLL_TORSO_SPIN_DEGREES_PER_SEC) actually
	# carries this knockback into the flight, rather than launching from
	# whatever velocity happened to be left over from normal movement.
	velocity = away * FINISHER_KNOCKBACK_SPEED + Vector3.UP * FINISHER_KNOCKBACK_UP_SPEED
	character.ragdoll()
	_is_finisher_ragdolled = true
	_finisher_ragdoll_remaining = FINISHER_RAGDOLL_SECONDS
	# Spans the entire flight PLUS a post-recovery grace window in one shot --
	# this is what prevents a chain-into-chain infinite lock: without the
	# grace window, the instant _end_finisher_ragdoll() hands control back, a
	# still-nearby attacker could land the first hit of a brand new combo
	# before the defender has had any real chance to react.
	_invulnerable_remaining = maxf(_invulnerable_remaining, FINISHER_RAGDOLL_SECONDS + POST_FINISHER_INVULN_SECONDS)
	_hitstun_remaining = 0.0 # the ragdoll's own _is_finisher_ragdolled early-return already fully owns input; no ordinary hitstun needed underneath it

## Ends the finisher ragdoll once its own timer runs out (see
## _physics_process's own _is_finisher_ragdolled early-return) -- stands the
## character back up wherever they actually landed, NOT back at their
## pre-hit position. character.gd's own reset_ragdoll() resets this node's
## LOCAL position to Vector3.ZERO, which is only correct for the death/
## respawn flow because respawn() ALSO hard-teleports to a spawn point right
## after -- there's no such teleport here, so global_transform is captured
## first. Confirmed directly: character.gd's own _physics_process already
## keeps this node's global_transform in sync with the ragdoll's "hips" body
## every physics frame while ragdolled (see its own comment), so this is
## already exactly where the ragdoll actually landed, no separate accessor
## needed.
func _end_finisher_ragdoll() -> void:
	_is_finisher_ragdolled = false
	var character := _get_character()
	if character != null:
		var landing: Transform3D = character.global_transform
		global_position = landing.origin
		rotation = Vector3(0.0, landing.basis.get_euler().y, 0.0) # stand upright at whatever yaw it settled at, ignore pitch/roll
		if character.has_method("reset_ragdoll"):
			character.reset_ragdoll()

## A permanent (death, not auto-recovering) sibling of _begin_finisher_
## ragdoll's own "away from attacker" push, for an ORDINARY killing blow --
## a real reported complaint: getting finished off used to just crumple in
## place with no reaction to the hit at all, since only combo finishers ever
## applied any knockback. ADDED to whatever velocity the player already had
## (sprinting, mid-jump, falling) rather than overwriting it, so momentum
## from the hit and momentum the player already carried both show up in the
## ragdoll together -- see _die()'s own comment for why this has to run
## BEFORE _die() zeroes/reads this body's velocity, not after. No attacker
## (fall damage, see take_damage()'s own comment on why it's null there) ==
## no push added here; _apply_fall_damage's own pre-impact velocity.y
## restoration already covers that case on its own.
func _apply_death_knockback(attacker: Node) -> void:
	if attacker == null or not (attacker is Node3D):
		return
	var flat := global_position - (attacker as Node3D).global_position
	flat.y = 0.0
	if flat.length_squared() < 0.0001:
		return
	velocity += flat.normalized() * DEATH_KNOCKBACK_SPEED + Vector3.UP * DEATH_KNOCKBACK_UP_SPEED

## Resolves peer_id to its owning Player node, or null if no such player is
## currently spawned -- reuses the "players" group the exact same way
## _find_lock_on_candidates() below already does, just filtering by
## authority instead of range/angle. Needed by _rpc_request_hit (to resolve
## the attacker Node take_damage()'s own attacker param expects) and
## _rpc_play_hit_result (to resolve the real attacker's own local body for
## the block-stun payback, and the finisher-ragdoll's own knockback-
## direction attacker reference) -- see each of their own comments.
func _find_player_by_peer_id(peer_id: int) -> Node:
	for node in get_tree().get_nodes_in_group("players"):
		if node.get_multiplayer_authority() == peer_id:
			return node
	return null

## Grab/carry -- see this state's own comment where _carried_body is
## declared. Owns starting a new grab (a HELD "interact" past
## GRAB_HOLD_SECONDS while the crosshair is on something "grabbable"),
## applying the carry force every frame while already holding something,
## and a plain TAP dropping it -- matching the user's own "press E and
## untoggle" wording (no hold needed to release, only to grab).
func _process_grab_carry(delta: float) -> void:
	if _carried_body != null:
		if not is_instance_valid(_carried_body):
			_carried_body = null
			return
		_apply_carry_force(delta)
		if Input.is_action_just_pressed("interact"):
			_drop_carried_body()
		return

	# No new grab can start while dead/stunned/hitstunned -- same gating
	# every other player-initiated action in this file already applies.
	if is_dead or _stun_remaining > 0.0 or _hitstun_remaining > 0.0:
		_grab_hold_timer = 0.0
		return

	if Input.is_action_pressed("interact"):
		_grab_hold_timer += delta
		if _grab_hold_timer >= GRAB_HOLD_SECONDS:
			_grab_hold_timer = 0.0
			var target := _find_grab_target()
			if target != null:
				_start_carry(target)
	else:
		_grab_hold_timer = 0.0

## Corpse looting -- see this state's own var comment for why this needs
## its own timer instead of reusing _grab_hold_timer. Skipped entirely
## while actually carrying something (a tap there means "drop", already
## handled by _process_grab_carry) so the two systems never fight over the
## same press.
func _process_corpse_loot(delta: float) -> void:
	if _carried_body != null:
		_loot_tap_still_possible = false
		return
	if is_dead or _stun_remaining > 0.0 or _hitstun_remaining > 0.0:
		_loot_tap_still_possible = false
		return
	if Input.is_action_just_pressed("interact"):
		_loot_tap_timer = 0.0
		_loot_tap_still_possible = true
	elif Input.is_action_pressed("interact"):
		if _loot_tap_still_possible:
			_loot_tap_timer += delta
			if _loot_tap_timer >= GRAB_HOLD_SECONDS:
				_loot_tap_still_possible = false
	elif Input.is_action_just_released("interact"):
		if _loot_tap_still_possible:
			_try_loot_nearest_corpse()
		_loot_tap_still_possible = false

## "lootable_corpses" (loot/loot_component.gd's own group) mirrors the
## group-based lookup this file already uses for "players"/lockable NPCs --
## no distance sort needed since is_nearby() already gates on the SAME
## INTERACT_RANGE proximity Area3D every other interactable in this project
## uses, so at most one real corpse is ever in range to begin with in
## practice.
func _try_loot_nearest_corpse() -> void:
	for node in get_tree().get_nodes_in_group("lootable_corpses"):
		if not is_instance_valid(node):
			continue
		if node.has_method("is_nearby") and node.is_nearby(self) and node.has_method("take_all"):
			# Host-authoritative (see _rpc_request_loot's own comment) --
			# only actually mutates the corpse directly when there's no
			# networked session to arbitrate, or this peer already IS the
			# server.
			if Net.is_networked() and not Net.is_server():
				_rpc_request_loot.rpc_id(1, node.get_path())
			else:
				node.take_all(self)
			return

## Host-authoritative loot arbitration -- prevents two players double-
## looting the same corpse. A non-host peer's own tap
## (_try_loot_nearest_corpse above) sends this to the server instead of
## calling corpse.take_all() directly. Mirrors _rpc_request_hit's own
## shape ("any_peer", rpc_id targeting a specific node's own copy on a
## specific peer).
@rpc("any_peer", "call_remote", "reliable")
func _rpc_request_loot(corpse_path: NodePath) -> void:
	if not Net.is_server():
		return
	var corpse := get_node_or_null(corpse_path)
	if corpse == null or not corpse.has_method("take_all"):
		return
	var requester_peer_id := multiplayer.get_remote_sender_id()
	# Snapshot BEFORE take_all() mutates it -- see _rpc_grant_loot's own
	# comment for why resending this list back to the requester (rather
	# than computing take_all()'s own exact post-mutation delta) is the
	# simplest correct way to get the SAME items onto both copies.
	var granted: Array[Dictionary] = corpse.loot_items.duplicate(true)
	corpse.take_all(self)
	if not granted.is_empty():
		_rpc_grant_loot.rpc_id(requester_peer_id, granted)

## Server -> the one specific requesting peer only -- applies the same
## items the server's own take_all() call above just granted onto ITS OWN
## copy, to the requester's own LOCAL Inventory too. General inventory
## contents are never synced/replicated in this project (see
## player/inventory.gd's own class comment on multiplayer scope) -- this
## explicit hand-back is the only way the requester's own screen actually
## receives what they looted. Defensively checks the sender really is the
## server -- an "any_peer" RPC any client could otherwise call on
## themselves to grant free loot.
@rpc("any_peer", "call_remote", "reliable")
func _rpc_grant_loot(items: Array[Dictionary]) -> void:
	if multiplayer.get_remote_sender_id() != 1:
		return
	var inv: Node = get_node_or_null("Inventory")
	if inv == null:
		return
	for entry in items:
		inv.call("add_item", entry.get("item_id", ""), entry.get("count", 1))

## Camera-forward raycast, same one-off inline PhysicsRayQueryParameters3D
## idiom every other raycast in this file already uses (punch target, climb
## check, lock-on line-of-sight) -- no shared helper existed to extend.
## mask 1|8|16: world (so a wall between the camera and a target correctly
## occludes it, same as any other line-of-sight check) + ragdoll bones +
## small dynamic props (glass_bottle.gd's own new layer, matching
## thrown_bottle.gd's existing use of 16). Only ever returns something
## explicitly tagged "grabbable" -- a raw world/wall hit never counts.
func _find_grab_target() -> RigidBody3D:
	var space_state := get_world_3d().direct_space_state
	var from: Vector3 = camera.global_position
	var to: Vector3 = from + (-camera.global_transform.basis.z) * GRAB_RANGE
	var query := PhysicsRayQueryParameters3D.create(from, to)
	query.collision_mask = 1 | 8 | 16
	var result := space_state.intersect_ray(query)
	if result.is_empty():
		return null
	var collider: Object = result.get("collider")
	if collider is RigidBody3D and (collider as RigidBody3D).is_in_group("grabbable"):
		return collider
	return null

func _start_carry(body: RigidBody3D) -> void:
	_carried_body = body
	_carry_distance = clampf(camera.global_position.distance_to(body.global_position), CARRY_MIN_DISTANCE, CARRY_MAX_DISTANCE)
	_carry_sync_timer = 0.0
	# Only a player's own ragdoll bone carries this metadata (see
	# character.gd's own _make_ragdoll_body) -- mutant/crimson-claw corpses
	# and the bottle are local-only for now (see the grab/carry plan's own
	# Design section 6 on why: NPCs and /spawn-ed props aren't networked at
	# all today, so there's nothing to sync FOR on other peers' screens).
	if Net.is_networked() and body.has_meta("ragdoll_owner_peer_id"):
		var owner_peer_id: int = body.get_meta("ragdoll_owner_peer_id")
		var bone_name: String = body.get_meta("ragdoll_bone_name")
		_rpc_start_carry.rpc(owner_peer_id, bone_name)

## Bounded PD spring pulling _carried_body toward a point in front of the
## camera -- a real force via apply_central_force(), not a direct position/
## velocity override, so F=ma naturally makes heavy bones (hips) sluggish
## and light ones (hands, the bottle) snappy off the SAME already-existing
## per-bone masses (see CARRY_SPRING_K's own comment). Auto-drops if the
## target is unreachable (wedged behind geometry) instead of letting the
## force keep growing against an obstruction.
func _apply_carry_force(delta: float) -> void:
	var target: Vector3 = camera.global_position + (-camera.global_transform.basis.z) * _carry_distance
	var error: Vector3 = target - _carried_body.global_position
	if error.length() > MAX_CARRY_DISTANCE_BEFORE_AUTO_DROP:
		_drop_carried_body()
		return
	var force: Vector3 = (error * CARRY_SPRING_K) - (_carried_body.linear_velocity * CARRY_DAMPING_K)
	force = force.limit_length(MAX_CARRY_FORCE)
	_carried_body.apply_central_force(force)

	if Net.is_networked() and _carried_body.has_meta("ragdoll_owner_peer_id"):
		_carry_sync_timer += delta
		if _carry_sync_timer >= CARRY_SYNC_INTERVAL_SECONDS:
			_carry_sync_timer = 0.0
			var owner_peer_id: int = _carried_body.get_meta("ragdoll_owner_peer_id")
			var bone_name: String = _carried_body.get_meta("ragdoll_bone_name")
			_rpc_carry_transform.rpc(owner_peer_id, bone_name, _carried_body.global_transform, _carried_body.linear_velocity)

func _drop_carried_body() -> void:
	if _carried_body == null:
		return
	if Net.is_networked() and _carried_body.has_meta("ragdoll_owner_peer_id"):
		var owner_peer_id: int = _carried_body.get_meta("ragdoll_owner_peer_id")
		var bone_name: String = _carried_body.get_meta("ragdoll_bone_name")
		_rpc_stop_carry.rpc(owner_peer_id, bone_name, _carried_body.global_transform, _carried_body.linear_velocity)
	_carried_body = null

## Broadcast from the CARRYING peer's own Player node (this RPC's own
## "authority") -- resolved by every OTHER peer via the dead player's own
## peer_id (already-networked, see _rpc_died), not this node's own
## identity, since the carried bone lives on a DIFFERENT player's Character.
## freeze=true stops this peer's own local physics simulation of just that
## one bone from fighting the incoming synced transforms below.
@rpc("authority", "call_remote", "reliable")
func _rpc_start_carry(owner_peer_id: int, bone_name: String) -> void:
	var body := _resolve_ragdoll_body(owner_peer_id, bone_name)
	if body != null:
		body.freeze = true

## Throttled (see CARRY_SYNC_INTERVAL_SECONDS), unreliable -- same
## "high-frequency, next-one-supersedes-this-one" precedent
## _rpc_voice_chunk already establishes, applied to a transform snapshot
## instead of an audio chunk. This is the "one peer simulates, everyone
## else follows" pattern, not lockstep-identical physics -- matching this
## project's own already-accepted tolerance for "good enough" ragdoll sync.
@rpc("authority", "call_remote", "unreliable")
func _rpc_carry_transform(owner_peer_id: int, bone_name: String, xform: Transform3D, vel: Vector3) -> void:
	var body := _resolve_ragdoll_body(owner_peer_id, bone_name)
	if body != null:
		body.global_transform = xform
		body.linear_velocity = vel

## Unfreezes the bone locally and hands it the last known transform/velocity
## as its own new starting point -- every peer then continues independent
## local physics simulation from there (same "authoritative snapshot, then
## independent continuation" shape _rpc_play_hit_result already uses).
@rpc("authority", "call_remote", "reliable")
func _rpc_stop_carry(owner_peer_id: int, bone_name: String, xform: Transform3D, vel: Vector3) -> void:
	var body := _resolve_ragdoll_body(owner_peer_id, bone_name)
	if body != null:
		body.global_transform = xform
		body.linear_velocity = vel
		body.freeze = false

func _resolve_ragdoll_body(owner_peer_id: int, bone_name: String) -> RigidBody3D:
	var owner_player := _find_player_by_peer_id(owner_peer_id)
	if owner_player == null:
		return null
	var character: Node = owner_player.get_node_or_null("Character")
	if character == null or not character.has_method("get_ragdoll_body"):
		return null
	return character.get_ragdoll_body(bone_name)

## Every other member of the "players" group (player.gd's own _ready()) plus
## every member of the new "lockable_npcs" group (wolf.gd/sloth.gd's own
## _ready()) within LOCK_ON_RANGE, alive -- the full pool _try_lock_on()
## picks from. Sorted by signed horizontal angle off THIS body's own current
## forward (not the camera's) so cycling through them (see _try_lock_on) has
## a stable, predictable left-to-right visiting order rather than jumping
## around based on whatever "nearest to crosshair" happens to resolve to
## each press.
func _find_lock_on_candidates() -> Array:
	var candidates: Array = []
	for node in get_tree().get_nodes_in_group("players") + get_tree().get_nodes_in_group("lockable_npcs"):
		if node == self or not (node is Node3D):
			continue
		if bool(node.get("is_dead")):
			continue
		if global_position.distance_to((node as Node3D).global_position) > LOCK_ON_RANGE:
			continue
		candidates.append(node)
	var forward_flat := Vector3(-transform.basis.z.x, 0.0, -transform.basis.z.z)
	candidates.sort_custom(func(a: Node3D, b: Node3D) -> bool:
		var angle_a := forward_flat.signed_angle_to(_flat_to(a.global_position), Vector3.UP)
		var angle_b := forward_flat.signed_angle_to(_flat_to(b.global_position), Vector3.UP)
		return angle_a < angle_b
	)
	return candidates

func _flat_to(target_position: Vector3) -> Vector3:
	var flat := target_position - global_position
	flat.y = 0.0
	return flat

## Naruto-Storm-style lock-on camera, toggled/cycled by a single "lock_on"
## (C) press (see _physics_process's own call site) -- see _lock_on_target's
## own comment for the overall mechanics.
##
## Not locked: acquires whichever in-range candidate is closest to the
## CAMERA's own crosshair (within LOCK_ON_MAX_ANGLE_DEGREES), so a fresh
## lock-on always grabs whoever you're actually looking toward.
##
## Already locked: cycles to the NEXT candidate in the (freshly recomputed,
## body-forward-relative) list instead -- crosshair angle is deliberately
## ignored here, so repeated presses visit every in-range target in a
## stable order regardless of where the camera happens to be pointed mid-
## fight. _lock_on_visits (a plain COUNT of presses since acquiring, not a
## list position) is what actually decides when to release -- NOT "does the
## acquired target's own index in the sorted list overrun," which would
## release early whenever acquisition happened to land on some candidate
## other than the list's first entry (confirmed as a real bug this way: a
## fresh acquisition can land on ANY candidate depending on crosshair angle,
## completely independent of that candidate's own position in the body-
## forward-sorted cycling order -- e.g. with 2 candidates where the acquired
## one sits at sorted-index 1, "index+1 >= size" would release on the very
## NEXT press without ever visiting the other candidate at all). Counting
## presses instead guarantees every OTHER candidate gets visited exactly
## once before releasing, regardless of which one was acquired first -- e.g.
## with exactly 2 candidates: 1st press locks A, 2nd press advances to B,
## 3rd press (visits would exceed the candidate count) releases entirely
## instead of wrapping back to A. A single candidate releases on the very
## next press, the same rule applied with N=1.
func _try_lock_on() -> void:
	var candidates := _find_lock_on_candidates()
	if _lock_on_target == null:
		if candidates.is_empty():
			return
		var best: Node3D = null
		var best_angle := INF
		var cam_forward := -camera.global_transform.basis.z
		for node in candidates:
			var to_node := (node as Node3D).global_position - global_position
			var angle := rad_to_deg(cam_forward.angle_to(to_node))
			if angle < best_angle:
				best_angle = angle
				best = node
		if best == null or best_angle > LOCK_ON_MAX_ANGLE_DEGREES:
			return
		_lock_on_target = best
		_lock_on_visits = 1
		_lock_on_los_blocked_remaining = 0.0
	else:
		_lock_on_visits += 1
		if _lock_on_visits > candidates.size():
			_release_lock_on()
			return
		var current_index := candidates.find(_lock_on_target)
		# The current target may no longer be in the freshly recomputed list
		# at all (e.g. it just left range/died this same instant) -- fall
		# back to the front of the list rather than indexing with -1.
		var next_index: int = (maxi(current_index, 0) + 1) % candidates.size()
		_lock_on_target = candidates[next_index]
		_lock_on_los_blocked_remaining = 0.0

func _release_lock_on() -> void:
	_lock_on_target = null
	_lock_on_visits = 0
	_lock_on_los_blocked_remaining = 0.0

## Drives the actual camera-lock behavior every physics frame while locked
## (see _physics_process's own call site, which replaces the ordinary
## moving-yaw-fold branch with this while _lock_on_target != null) -- swings
## rotation.y (the body itself, not just camera_pivot) toward the target's
## own bearing using the same lerp-toward-target-every-frame idiom
## ADS_FOV_LERP_SPEED/SHOULDER_OFFSET_LERP_SPEED already use elsewhere in
## this file, and pins camera_pivot.rotation.y at 0.0 so there's nothing left
## to fold back once the lock releases (see _lock_on_target's own comment).
##
## Free side effect worth knowing: WASD (transform.basis-relative),
## move_direction (_direction_from_input), and dash direction all already
## key off rotation.y -- driving it toward the target automatically turns
## WASD into Storm-style strafe-around-the-target movement with zero changes
## to any movement code.
func _update_lock_on(delta: float) -> void:
	if not is_instance_valid(_lock_on_target) or bool(_lock_on_target.get("is_dead")):
		_release_lock_on()
		return
	var to_target := _lock_on_target.global_position - global_position
	var flat := Vector3(to_target.x, 0.0, to_target.z)
	if flat.length() > LOCK_ON_RANGE * 1.15:
		_release_lock_on()
		return

	var space_state := get_world_3d().direct_space_state
	var eye := global_position + Vector3(0, PUNCH_ORIGIN_HEIGHT, 0)
	var query := PhysicsRayQueryParameters3D.create(eye, _lock_on_target.global_position)
	query.collision_mask = 1 # world geometry only -- other bodies shouldn't block a lock
	query.exclude = [self]
	var result := space_state.intersect_ray(query)
	if not result.is_empty():
		_lock_on_los_blocked_remaining += delta
		if _lock_on_los_blocked_remaining > LOCK_ON_LOS_GRACE_SECONDS:
			_release_lock_on()
			return
	else:
		_lock_on_los_blocked_remaining = 0.0

	var desired_yaw := atan2(-flat.x, -flat.z)
	rotation.y = lerp_angle(rotation.y, desired_yaw, delta * LOCK_ON_CAMERA_LERP_SPEED)
	camera_pivot.rotation.y = 0.0

	var horiz_dist := maxf(flat.length(), LOCK_ON_MIN_PITCH_HORIZ_DIST)
	var desired_pitch := clampf(atan2(to_target.y, horiz_dist), deg_to_rad(-LOCK_ON_MAX_PITCH_DEGREES), deg_to_rad(LOCK_ON_MAX_PITCH_DEGREES))
	camera_pivot.rotation.x = lerp_angle(camera_pivot.rotation.x, desired_pitch, delta * LOCK_ON_CAMERA_LERP_SPEED)
	_pitch = camera_pivot.rotation.x

func _die() -> void:
	is_dead = true
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	# Dying mid-swing has to drop the rope: _physics_process's own is_dead
	# early-return sits ABOVE the grapple gate, so the swing would never run
	# again to clean itself up and the rope would hang there permanently.
	if _grapple_active:
		release_grapple(false)
	# Can't still be carrying something once dead -- drops it the same way a
	# manual E-press would (real gravity/physics take over from wherever it
	# was).
	if _carried_body != null:
		_drop_carried_body()
	# A finisher's own ragdoll flight is superseded entirely by death -- the
	# is_dead early-return in _physics_process sits ABOVE the
	# _is_finisher_ragdolled check, so this state would otherwise just sit
	# unread until respawn() clears it anyway, but clearing it here too keeps
	# it from ever being stale even for that one frame.
	_is_finisher_ragdolled = false
	_finisher_ragdoll_remaining = 0.0
	# Otherwise a walk/run loop already playing at the moment of death would
	# keep looping forever -- _physics_process (where it would normally get
	# stopped) never runs again once is_dead is true.
	walk_sound_player.stop()
	run_sound_player.stop()
	var character := _get_character()
	if character != null and character.has_method("ragdoll"):
		character.ragdoll()
	# Zeroed AFTER ragdoll() above, not before -- character.gd's own
	# ragdoll() reads THIS body's own velocity the instant it's called to
	# seed the ragdoll rig's own starting momentum (see its own comment,
	# "carries over whatever momentum the player already had"). Zeroing
	# first (a real bug this used to have) meant every death ragdolled from
	# a dead stop regardless of how fast the player was actually moving,
	# falling, or getting knocked back (see _apply_death_knockback above)
	# the instant before. Safe to zero afterward regardless -- is_dead's own
	# early-return in _physics_process means nothing ever reads this body's
	# velocity for movement again.
	velocity = Vector3.ZERO
	died.emit()
	# Broadcasts from HERE, not from a specific damage-source call site, so
	# EVERY lethal path (fall damage, an NPC bite, and the new networked-hit
	# path via _rpc_request_hit) gets networked death-visibility for free --
	# _die() is the one funnel all of them already go through.
	if Net.is_networked():
		_rpc_died.rpc()

## Replays a death's own visual state (ragdoll, is_dead) on every OTHER
## peer's copy of whoever actually died -- a deliberately narrower sibling
## of _die(), not a call to _die() itself: _die()'s own
## Input.mouse_mode = MOUSE_MODE_VISIBLE line (further up) has no
## is_multiplayer_authority() guard, which is harmless today only because
## _die() itself never runs on a non-authoritative copy -- this handler
## exists specifically so that stays true. died.emit() is also deliberately
## skipped: a non-authoritative copy never has a HUD child to begin with
## (see _ready()'s own is_multiplayer_authority()-gated HUD instantiation),
## so nothing would be listening for it anyway.
##
## Side benefit, not a separate fix: is_dead is a plain non-replicated var,
## so setting it true here also makes a networked player's real death
## visible to _find_lock_on_candidates()/_update_lock_on()'s own
## node.get("is_dead") checks for every other peer, for free.
@rpc("authority", "call_remote", "reliable")
func _rpc_died() -> void:
	is_dead = true
	_is_finisher_ragdolled = false
	_finisher_ragdoll_remaining = 0.0
	walk_sound_player.stop()
	run_sound_player.stop()
	var character := _get_character()
	if character != null and character.has_method("ragdoll"):
		character.ragdoll()
	# Same ordering fix as _die()'s own -- see its comment. This replica's
	# own velocity (whatever this peer's local copy of the CharacterBody3D
	# last had it at) is still worth preserving into the ragdoll rather than
	# zeroing it first for the same "carries over momentum" reason, even
	# though it isn't the authoritative source of that momentum.
	velocity = Vector3.ZERO

## Resets health/stamina/position and un-ragdolls the character -- called
## by the death screen's Respawn button (see ui/hud.gd).
func respawn() -> void:
	health = max_health
	stamina = max_stamina
	mana = max_mana
	_stamina_exhausted = false
	is_dead = false
	velocity = Vector3.ZERO
	# Same "explicitly clear everything so the next frame doesn't get stuck"
	# hygiene character.gd's own reset_ragdoll() already documents for ITS
	# combat flags -- a respawn mid-hitstun/mid-invuln/mid-finisher-flight
	# (e.g. fall damage finishing off an already-comboed player) shouldn't
	# leave any of this lingering into the fresh life.
	_hitstun_remaining = 0.0
	_invulnerable_remaining = 0.0
	_is_finisher_ragdolled = false
	_finisher_ragdoll_remaining = 0.0
	_release_lock_on()

	# A claimed building overrides the world's own SpawnPoint -- that's the
	# whole point of claiming one (see try_claim_building). global_position,
	# not position: the claim recorded a world-space point, and this body
	# isn't always parented directly to the city (a car reparents it, see
	# vehicles/car.gd), so assigning it to the LOCAL position would put a
	# respawn wherever that offset happened to land.
	if has_claimed_building():
		global_position = _claimed_spawn_position
	else:
		var spawn := get_parent().get_node_or_null("SpawnPoint")
		position = spawn.position if spawn != null else Vector3.ZERO
	rotation = Vector3.ZERO
	camera_pivot.rotation = Vector3.ZERO
	_pitch = 0.0
	_air_peak_y = position.y

	if is_multiplayer_authority():
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED

	var character := _get_character()
	if character != null and character.has_method("reset_ragdoll"):
		character.reset_ragdoll()
	respawned.emit()
	# respawn() is only ever triggered by the HUD's own Respawn button, which
	# (per _die()'s own HUD-existence gating) only ever exists on the real
	# dead player's own machine -- always a safe, correctly-authoritative
	# call site, same as every other one-shot action's own broadcast.
	if Net.is_networked():
		_rpc_respawned.rpc()

## Replays a respawn's own visual state on every OTHER peer's copy --
## position/rotation need no RPC at all (respawn() already writes them
## directly above, and both are already in _setup_replication()'s own
## continuous sync, so the teleport-to-spawn propagates automatically); this
## only needs to cover the two things that AREN'T replicated: is_dead and
## the ragdoll rig's own visual reset.
@rpc("authority", "call_remote", "reliable")
func _rpc_respawned() -> void:
	is_dead = false
	_is_finisher_ragdolled = false
	_finisher_ragdoll_remaining = 0.0
	var character := _get_character()
	if character != null and character.has_method("reset_ragdoll"):
		character.reset_ragdoll()

## Dev-only, toggled by the dev console's own "/fly" command (see
## ui/dev_console.gd) -- see _fly_mode's own comment for what it actually
## changes in _physics_process. velocity.y is reset on EVERY toggle (not
## just turning it off) so neither direction leaves a stale vertical speed
## behind: turning fly ON while already falling would otherwise keep that
## fall speed for one frame before the new jump/crouch-driven logic
## overwrites it, and turning it OFF mid-ascent would otherwise resume real
## gravity from whatever FLY_SPEED happened to be, not a clean 0.
func set_fly_mode(enabled: bool) -> void:
	_fly_mode = enabled
	velocity.y = 0.0

func is_flying() -> bool:
	return _fly_mode

## Dev-only, toggled by the dev console's own "/infinitehealth" command --
## see _infinite_health's own comment for what it actually changes
## (take_damage's own early return). No other state to reset on toggle
## (unlike set_fly_mode's own velocity.y clear) since this doesn't touch
## movement at all.
func set_infinite_health(enabled: bool) -> void:
	_infinite_health = enabled

func is_infinite_health() -> bool:
	return _infinite_health

## Dev-only, toggled by the dev console's own "/infinitesprint" command --
## see _infinite_stamina's own comment for what it actually changes
## (_update_stamina's own drain branch, plus _process_climbing's own).
func set_infinite_stamina(enabled: bool) -> void:
	_infinite_stamina = enabled

func is_infinite_stamina() -> bool:
	return _infinite_stamina

## Dev-only, toggled by the dev console's own "/weather rain"/"/weather
## clear" commands -- see player/rain_effect.gd's own class comment for what
## this actually turns on/off. _rain_effect is null on a non-authoritative
## copy (see _ready()'s own instantiation), so this is a no-op there --
## there's nothing for a remote peer to locally toggle for another player.
func set_rain_active(active: bool) -> void:
	if _rain_effect != null:
		_rain_effect.set_active(active)

func is_rain_active() -> bool:
	return _rain_effect != null and _rain_effect.is_active()

## Zoomed-out spring_length while seated in ANY car seat -- a real, explicit
## ask ("zoom the driving pov out a bit"), since a car reads much bigger
## on-screen than the player's own body the default on-foot 4.0 was tuned
## against. Applies to the passenger seat too, not just the driver's --
## both are inside the same car needing the same wider view, and there's no
## separate passenger-specific camera concern to justify splitting this.
const DRIVING_SPRING_LENGTH := 7.0
var _saved_spring_length := 0.0

## Called by vehicles/car.gd's own _begin_enter() the instant a seat
## transition starts (before the entering_car animation even finishes) --
## deliberately not deferred to _complete_enter, so the player can't walk
## away mid-animation. car itself isn't used by player.gd at all (see
## _seated_car's own comment); it's only a Node reference so is_seated()-
## style checks elsewhere have something non-null to test.
func begin_car_transition(car: Node) -> void:
	_seated_car = car
	_saved_spring_length = _spring_arm.spring_length
	_spring_arm.spring_length = DRIVING_SPRING_LENGTH
	# A locked-on combat camera has no business fighting the car's own camera
	# swap -- release it the same way entering any other input-freezing state
	# (respawn) already does.
	_release_lock_on()

## Called by vehicles/car.gd's own _complete_exit(), once the player is
## already back in the world at a safe position -- see its own ordering
## comment for why this is the very last thing that happens, not the first.
##
## Also folds camera_pivot's own accumulated rotation back into a sane
## state -- a real reported bug ("stuck in a weird position at an angle...
## lets me walk around but at an angle"): mouse-look (_unhandled_input)
## isn't gated on _seated_car at all (looking around while driving is
## normal), so camera_pivot.rotation.y/.x (and _pitch, which
## _unhandled_input keeps re-driving .x from every mouse-motion event) keep
## accumulating for the WHOLE drive with nothing ever folding or resetting
## them the way normal on-foot movement already does every time is_moving
## (see _physics_process's own "rotation.y += camera_pivot.rotation.y"
## fold) -- car.gd's own _complete_exit() already set THIS body's own
## rotation.y to a correct upright car-facing yaw by this point, but
## camera_pivot itself still holds whatever extra look-yaw/pitch was left
## over from driving, which is what actually read as "stuck at an angle"
## even though the body transform underneath it was fine. Folding yaw in
## (not just zeroing it) keeps the same "your view doesn't jarringly snap"
## continuity the walking fold already provides; pitch resets to level,
## matching respawn()'s own precedent (camera_pivot.rotation = Vector3.ZERO
## + _pitch = 0.0) for the same "neutral, looking-straight-ahead" default.
func end_car_transition() -> void:
	_seated_car = null
	_spring_arm.spring_length = _saved_spring_length
	rotation.y += camera_pivot.rotation.y
	camera_pivot.rotation = Vector3.ZERO
	_pitch = 0.0

## The "/claim" chat command (see _on_chat_message_sent). Claims whatever
## building this player is currently standing inside: plays the reveal orbit
## (player/claim_cutscene.gd), shows "BUILDING ACQUIRED", and moves this
## player's respawn point to where they were standing when they claimed it.
##
## Returns whether a claim actually happened, so callers/tests don't have to
## infer it from side effects.
##
## The spawn point is the player's OWN position at the moment of claiming,
## not a derived point inside the footprint. They are demonstrably standing
## somewhere valid -- that's what being inside the trigger means -- so there
## is no geometry to solve and no way to land them in a wall.
##
## KNOWN GAP (multiplayer): local only, like every other NPC/world system in
## this project. Two peers can each claim the same building and neither will
## know. Real ownership needs a networked authority to arbitrate it, which
## doesn't exist yet.
func try_claim_building() -> bool:
	if not is_multiplayer_authority():
		return false
	# Mid-orbit, or dead: neither is a moment to start another one.
	if _in_claim_cutscene or _in_cutscene or is_dead:
		return false

	var area := current_building_area()
	if area == null:
		_show_claim_banner(CLAIM_FAILURE_TEXT)
		return false
	if _claimed_building_area != null and _claimed_building_area == area:
		_show_claim_banner(CLAIM_ALREADY_OWNED_TEXT)
		return false

	var extents := _building_extents(area)
	_claimed_building_area = area
	_claimed_spawn_position = global_position
	_show_claim_banner(CLAIM_SUCCESS_TEXT)
	_play_claim_sound()
	_start_claim_cutscene(extents["center"], extents["size"])
	building_claimed.emit(extents["center"], extents["size"])
	return true

## The acquisition stinger. Same throwaway-player shape intro_cutscene.gd's
## own _play_ground_impact_sound uses: build one, play it, free it when it
## finishes.
##
## Parented to THIS body rather than to the claim cutscene, deliberately --
## the cutscene is queue_free()'d the moment its 6-second orbit ends, and a
## sound living on it would be cut off mid-note if the clip runs longer than
## the orbit does.
##
## "Master", not "Music": this project has no dedicated SFX bus (see
## default_bus_layout.tres -- only Music/Record exist beyond Master), and
## every other one-shot here already plays on Master. Putting it on Music
## would make the pause menu's music slider silence it.
func _play_claim_sound() -> void:
	var sfx := AudioStreamPlayer.new()
	sfx.stream = BuildingAcquiredAudio
	sfx.volume_db = linear_to_db(CLAIM_SOUND_VOLUME_LINEAR)
	sfx.bus = "Master"
	add_child(sfx)
	sfx.play()
	sfx.finished.connect(sfx.queue_free)

## The building this body is currently standing in, or null outdoors.
## Innermost-last (see _indoor_areas): with two overlapping footprints, the
## one entered most recently is the one being stood in.
func current_building_area() -> Node3D:
	for i in range(_indoor_areas.size() - 1, -1, -1):
		var area: Node3D = _indoor_areas[i]
		if area != null and is_instance_valid(area):
			return area
	return null

## Pulls a building's real world-space centre and full size straight off its
## indoor trigger's own BoxShape3D (see city_chunk.gd, which sizes that box
## to the building itself). Falls back to something sane rather than
## erroring if the shape isn't the expected kind -- a wrong-sized orbit is
## recoverable, a crash mid-claim isn't.
func _building_extents(area: Node3D) -> Dictionary:
	var center: Vector3 = area.global_position
	var size := Vector3(12.0, 20.0, 12.0)
	for child in area.get_children():
		if child is CollisionShape3D and (child as CollisionShape3D).shape is BoxShape3D:
			size = ((child as CollisionShape3D).shape as BoxShape3D).size
			break
	# The trigger box is centred on the building's own mid-height, but the
	# orbit wants the footprint centre at GROUND level to circle around.
	return {"center": Vector3(center.x, center.y - size.y * 0.5, center.z), "size": size}

func _show_claim_banner(text: String) -> void:
	if _claim_banner == null or not is_instance_valid(_claim_banner):
		_claim_banner = ClaimBannerScene.instantiate()
		add_child(_claim_banner)
	_claim_banner.show_message(text)

func _start_claim_cutscene(center: Vector3, size: Vector3) -> void:
	_in_claim_cutscene = true
	if _hud != null:
		_hud.visible = false
	_claim_cutscene = ClaimCutsceneScript.new()
	_claim_cutscene.player = self
	_claim_cutscene.building_center = center
	_claim_cutscene.building_size = size
	_claim_cutscene.finished.connect(_on_claim_cutscene_finished)
	add_child(_claim_cutscene)
	_claim_cutscene.begin()

func _on_claim_cutscene_finished() -> void:
	_in_claim_cutscene = false
	if _hud != null:
		_hud.visible = true
	if _claim_cutscene != null and is_instance_valid(_claim_cutscene):
		_claim_cutscene.queue_free()
	_claim_cutscene = null

## Whether this player has claimed a building at all yet -- what makes
## respawn() use the claimed spawn instead of the world's own SpawnPoint.
func has_claimed_building() -> bool:
	return _claimed_spawn_position != Vector3.INF

func get_claimed_spawn_position() -> Vector3:
	return _claimed_spawn_position

## Called by main.gd's own _spawn_singleplayer()/_spawn_local_player(), only
## for a genuinely NEW (just-created, never-before-saved) character -- see
## player/intro_cutscene.gd's own class comment for the actual timeline.
## Mirrors begin_car_transition()'s own split: THIS function owns entry/
## exit bookkeeping (the gate, HUD, city ambience, saved camera state), the
## cutscene component itself owns the timeline.
func start_intro_cutscene(spawn_position: Vector3) -> void:
	if not is_multiplayer_authority():
		return
	_in_cutscene = true
	if _hud != null:
		_hud.visible = false
	_fade_city_ambience(false, CITY_AMBIENCE_SLOW_FADE_SECONDS)
	_cutscene_saved_spring_length = _spring_arm.spring_length
	_intro_cutscene = IntroCutsceneScript.new()
	_intro_cutscene.player = self
	_intro_cutscene.character = _get_character()
	_intro_cutscene.spawn_position = spawn_position
	_intro_cutscene.original_spring_length = _cutscene_saved_spring_length
	_intro_cutscene.finished.connect(_on_intro_cutscene_finished)
	add_child(_intro_cutscene)
	_intro_cutscene.begin()

## Same "neutral, looking-straight-ahead" camera reset respawn()/
## end_car_transition() already use.
func _on_intro_cutscene_finished() -> void:
	_in_cutscene = false
	_spring_arm.spring_length = _cutscene_saved_spring_length
	camera_pivot.rotation = Vector3.ZERO
	_pitch = 0.0
	velocity = Vector3.ZERO
	_air_peak_y = global_position.y
	if _hud != null:
		_hud.visible = true
	_fade_city_ambience(true, CITY_AMBIENCE_SLOW_FADE_SECONDS)
	_intro_cutscene.queue_free()
	_intro_cutscene = null
	intro_cutscene_finished.emit()

## Called by player/intro_cutscene.gd's own fog-fade tween's final callback
## (see its own _start_fog_fade comment) -- deliberately a method on PLAYER,
## not intro_cutscene.gd itself, even though intro_cutscene.gd is what sets
## the override up. A real root-caused bug: that fade tween is bound to
## `player` specifically so it survives past this node's own queue_free()
## above (which happens at HANDOFF_DURATION_SECONDS, well before the fade's
## own longer FOG_FADE_DURATION_SECONDS finishes) -- but a bare method
## reference used as a Callable (e.g. `tween_callback(_some_method)`) is
## ALWAYS bound to whichever script instance it's written in, regardless of
## which node's create_tween() built the Tween itself. A callback still
## bound to intro_cutscene.gd's own instance silently never fired once that
## instance was freed (confirmed directly: the tween's own step_finished
## signal still reported the step as complete, but the callback's body never
## ran) -- moving the actual clearing logic here, onto the long-lived player
## node itself, sidesteps that entirely.
func clear_camera_fog_override() -> void:
	camera.environment = null

## Called by weapons/pistol.gd's own _equip() right after it reparents
## itself onto the player's hand -- see its own class comment on why guns
## redirect input here in player.gd instead of through character.gd's
## set_wielding_sword()-style moveset switching: there's no dedicated gun-
## holding animation yet, so equipping a gun only changes what left/right-
## click/reload DO (see _physics_process), not which animations play -- the
## character keeps its ordinary unarmed idle/walk/run.
func equip_gun(gun: Node) -> void:
	_equipped_gun = gun
	_equipped_grenade = null
	# Confirmed real pre-existing gap, fixed alongside the new bottle
	# feature (which made it much easier to actually hit -- throwing
	# routinely returns the player to bare-handed, making "equip bottle,
	# throw, later grab a gun" a common sequence): nothing used to clear
	# _equipped_bottle back to null here, so a stale reference to an
	# already-replaced bottle could linger and keep this combat-input block
	# reading the WRONG branch.
	_equipped_bottle = null

## Called by weapons/glass_bottle.gd's own _equip() right after it reparents
## itself onto the player's hand -- same "own equip call clears the OTHER
## equipped-weapon reference" fix equip_gun() above now also has, this
## direction (bottle after gun).
func equip_bottle(bottle: Node) -> void:
	_equipped_bottle = bottle
	_equipped_gun = null
	_equipped_grenade = null

## Called by player/inventory.gd's own unequip_to_slot(), right after
## character.clear_wielded_weapon() has already freed the actual gun scene
## instance it was pointing at -- clears the now-dangling reference so
## _physics_process's own "_equipped_gun != null" combat-input branches
## stop treating the player as still holding one.
func unequip_gun() -> void:
	_equipped_gun = null

## Same as unequip_gun(), the bottle side.
func unequip_bottle() -> void:
	_equipped_bottle = null

## Called by weapons/grenade.gd's own _equip(). Clears the other two
## equipped-weapon references for the same reason equip_gun()/equip_bottle()
## already clear each other's: _physics_process picks its combat-input
## branch by testing these in order, so a stale one silently routes input to
## a weapon that isn't in your hand any more.
func equip_grenade(grenade: Node) -> void:
	_equipped_grenade = grenade
	_equipped_gun = null
	_equipped_bottle = null

## Same as unequip_gun(), the grenade side.
func unequip_grenade() -> void:
	_equipped_grenade = null

## Called by items/grappling_hook.gd's own _equip().
func equip_grappling_hook(hook: Node) -> void:
	_equipped_grapple = hook
	_equipped_gun = null
	_equipped_bottle = null
	_equipped_grenade = null

## Same as unequip_gun(), the grapple side. Releases an in-progress swing
## first -- putting the launcher away mid-air must not leave the player
## permanently tethered to a rope whose launcher no longer exists.
func unequip_grappling_hook() -> void:
	if _grapple_active:
		release_grapple(false)
	_equipped_grapple = null

## Fires the hook at whatever the crosshair is on. No-op (a miss) if nothing
## solid is in range -- deliberately silent, matching how every other denied
## action in this file just doesn't happen.
func try_fire_grapple() -> bool:
	if _equipped_grapple == null or not is_instance_valid(_equipped_grapple):
		return false
	if not _equipped_grapple.has_method("find_anchor"):
		return false
	var origin: Vector3 = camera.global_position
	var direction: Vector3 = -camera.global_transform.basis.z
	var hit: Dictionary = _equipped_grapple.call("find_anchor", origin, direction, GRAPPLE_MAX_RANGE)
	if hit.is_empty():
		return false
	start_grapple(hit["position"])
	return true

## Attaches the rope and hands movement over to _process_grapple_swing().
##
## The rope's length is whatever the distance to the anchor already is at
## the moment it catches, NOT some fixed value -- that's what makes a
## grapple fired at a distant rooftop swing you in a wide arc, while one
## fired at the wall beside you barely moves you. Clamped only so a rope
## can't start shorter than it can ever be reeled to.
func start_grapple(anchor: Vector3) -> void:
	_grapple_anchor = anchor
	_grapple_rope_length = clampf(global_position.distance_to(anchor), GRAPPLE_MIN_ROPE_LENGTH, GRAPPLE_MAX_ROPE_LENGTH)
	_grapple_active = true
	_grapple_ground_time = 0.0
	# A swing and a wall-climb are mutually exclusive movement modes, and
	# _try_start_climb() can fire on the same held W that's pumping a swing.
	_is_climbing = false
	_release_lock_on()
	if _equipped_grapple != null and _equipped_grapple.has_method("show_rope"):
		_equipped_grapple.call("show_rope", anchor)
	var character := _get_character()
	if character != null:
		# Cleared going IN as well as coming out: whatever ran last (the
		# intro cutscene's own dive, most likely) may have left bone 0
		# tilted, and the swing clip deliberately has no root track of its
		# own to correct it (see GRAPPLE_SWING_ANIMATION).
		if character.has_method("reset_hips_rotation"):
			character.reset_hips_rotation()
		if character.anim_player != null:
			# Nothing else drives the animation while swinging --
			# _physics_process early-returns into _process_grapple_swing
			# before ever reaching set_walking().
			character.anim_player.play(GRAPPLE_SWING_ANIMATION)

## Lets go. `boosted` adds the upward kick that makes releasing at the top
## of an arc worth timing; a release forced by something else (unequipping,
## dying, the launcher going away) passes false and just drops the rope.
##
## Velocity is deliberately left untouched otherwise -- carrying the swing's
## own momentum out into free flight IS the mechanic.
func release_grapple(boosted: bool = true) -> void:
	if not _grapple_active:
		return
	_grapple_active = false
	_grapple_ground_time = 0.0
	if boosted:
		velocity.y += GRAPPLE_RELEASE_BOOST
	if _equipped_grapple != null and is_instance_valid(_equipped_grapple) and _equipped_grapple.has_method("hide_rope"):
		_equipped_grapple.call("hide_rope")
	var character := _get_character()
	if character != null and character.has_method("reset_hips_rotation"):
		# Belt and braces. The swing clip itself no longer tilts the root
		# (see GRAPPLE_SWING_ANIMATION), so there is normally nothing to
		# undo -- but bone 0 keeps whatever value was last WRITTEN to it,
		# and a clip with no track for that bone will never restore it. If
		# anything upstream leaves the hips rotated, this is what stops the
		# player standing up sideways afterwards.
		character.reset_hips_rotation()

## The whole movement frame while swinging: gravity, air control, reel/pay
## out, then the rope constraint.
##
## The constraint is a rigid distance, solved the standard way rather than
## as a spring: once the player is further from the anchor than the rope is
## long, the component of velocity pointing AWAY from the anchor is removed
## (leaving only the tangential part, which is what a pendulum swings on)
## and the position is snapped back onto the sphere. A spring would need
## tuning to avoid either sagging or oscillating, and would still let the
## rope visibly stretch.
func _process_grapple_swing(delta: float) -> void:
	velocity.y -= GRAVITY * delta

	# Air control, camera-relative, same basis the ordinary ground movement
	# uses -- pushing "forward" pumps the swing the way you'd expect from
	# where you're looking, not from where the rope happens to be.
	var input_blocked := _input_blocked()
	if not input_blocked and not is_dead:
		var input_dir := Input.get_vector("move_left", "move_right", "move_forward", "move_backward")
		if input_dir.length_squared() > 0.0:
			var basis := camera.global_transform.basis
			var forward := -basis.z
			forward.y = 0.0
			forward = forward.normalized()
			var right := basis.x
			right.y = 0.0
			right = right.normalized()
			var push := (right * input_dir.x + forward * input_dir.y).normalized()
			velocity += push * GRAPPLE_SWING_ACCEL * delta

		# Reel in to climb the rope (this is how you get onto a roof);
		# pay out to drop lower without letting go.
		if Input.is_action_pressed("sprint"):
			_grapple_rope_length = maxf(GRAPPLE_MIN_ROPE_LENGTH, _grapple_rope_length - GRAPPLE_REEL_SPEED * delta)
		elif Input.is_action_pressed("crouch"):
			_grapple_rope_length = minf(GRAPPLE_MAX_ROPE_LENGTH, _grapple_rope_length + GRAPPLE_PAYOUT_SPEED * delta)

		# Both release inputs are handled HERE, not in _physics_process's own
		# equipped-weapon input block -- while _grapple_active is true that
		# block is unreachable, since the grapple gate early-returns long
		# before it. Space gives the boosted release (the one worth timing);
		# clicking again just lets go.
		if Input.is_action_just_pressed("jump"):
			release_grapple(true)
			move_and_slide()
			return
		if Input.is_action_just_pressed("punch"):
			release_grapple(false)
			move_and_slide()
			return

	var to_anchor := _grapple_anchor - global_position
	var distance := to_anchor.length()
	if distance > _grapple_rope_length and distance > 0.001:
		var direction := to_anchor / distance
		# Velocity's own outward component, i.e. the part trying to stretch
		# the rope. Cancelled; everything perpendicular survives untouched.
		var outward := velocity.dot(-direction)
		if outward > 0.0:
			velocity += direction * outward
		velocity *= 1.0 - GRAPPLE_ROPE_DAMPING
		global_position = _grapple_anchor - direction * _grapple_rope_length

	if _equipped_grapple != null and is_instance_valid(_equipped_grapple) and _equipped_grapple.has_method("update_rope"):
		_equipped_grapple.call("update_rope", _grapple_anchor)

	move_and_slide()

	# Landing on your feet ends the swing on its own, rather than leaving the
	# player tethered and shuffling around on the ground.
	#
	# TWO ways out, because the speed test alone wasn't enough. It only fires
	# once you're nearly stopped -- but this same function keeps applying air
	# control every frame, INCLUDING while grounded, so simply holding a
	# movement key kept the player above the threshold indefinitely. The
	# result was being stuck face-down on the road in the looped "falling"
	# pose (see start_grapple) with no way to stand up, since _physics_process
	# early-returns into this branch for as long as _grapple_active is true.
	#
	# The grounded TIMER is the guaranteed escape; the speed test stays
	# because it ends a spent swing instantly instead of after a delay. The
	# timer is what preserves the original intent the speed test was reaching
	# for -- a fast skim across a rooftop only touches down briefly, so it
	# never accumulates enough grounded time to cut the rope.
	if is_on_floor():
		_grapple_ground_time += delta
	else:
		_grapple_ground_time = 0.0
	if _grapple_ground_time >= GRAPPLE_MAX_GROUND_SECONDS:
		release_grapple(false)
	elif is_on_floor() and Vector3(velocity.x, 0.0, velocity.z).length() < 1.0:
		release_grapple(false)

## Thin wrapper around Inventory.equip_from_slot() -- the ONE place that
## call is ever made from a live (not save-load) equip action, so the
## multiplayer broadcast below can never be forgotten by a caller.
## ui/inventory_panel.gd calls THIS, not the Inventory node directly, for
## exactly that reason. See player/inventory.gd's own class comment on
## multiplayer scope: only what's visually EQUIPPED gets RPC'd, general
## storage never does.
func equip_from_slot(slot_index: int) -> bool:
	var inv: Node = get_node_or_null("Inventory")
	if inv == null:
		return false
	var slot: Dictionary = {}
	if slot_index >= 0 and slot_index < inv.storage_slots.size():
		slot = inv.storage_slots[slot_index]
	var item_id: String = slot.get("item_id", "")
	if not inv.equip_from_slot(slot_index, self):
		return false
	var slot_key: String = ItemRegistry.get_item(item_id).get("equip_slot", "")
	if Net.is_networked() and slot_key != "":
		_rpc_equip_item.rpc(slot_key, item_id)
	return true

## Same reasoning as equip_from_slot() above, the unequip side.
func unequip_to_slot(slot_key: String) -> bool:
	var inv: Node = get_node_or_null("Inventory")
	if inv == null:
		return false
	if not inv.unequip_to_slot(slot_key, self):
		return false
	if Net.is_networked():
		_rpc_unequip_item.rpc(slot_key)
	return true

## Mirrors _rpc_died's own shape -- instantiates the same registered scene
## equip_from_slot() itself used and calls its unchanged _equip(self) on
## THIS peer's own replicated copy, so a remote player visually sees what
## you just equipped. NOT broadcast for the initial gear a loaded character
## re-equips at spawn (see main.gd's own _reequip_pending_inventory) or for
## a corpse getting visually stripped by looting (see loot/loot_component.gd)
## -- both are known, accepted gaps for this first pass, not oversights;
## only LIVE equip/unequip actions via this file's own wrappers above sync.
@rpc("authority", "call_remote", "reliable")
func _rpc_equip_item(_slot_key: String, item_id: String) -> void:
	var item := ItemRegistry.get_item(item_id)
	var scene: PackedScene = item.get("scene")
	if scene == null:
		return
	var instance: Node = scene.instantiate()
	# Same "must already be in the tree before _equip() runs" reasoning
	# player/inventory.gd's own equip_item_id() documents -- an item's own
	# _equip() assumes it's already inside the SceneTree (its @onready vars,
	# its own get_parent().remove_child() before reparenting onto the real
	# hand/head bone).
	add_child(instance)
	if instance.has_method("_equip"):
		instance.call("_equip", self)

@rpc("authority", "call_remote", "reliable")
func _rpc_unequip_item(slot_key: String) -> void:
	var character := _get_character()
	if character == null:
		return
	match slot_key:
		"hand_r":
			if character.has_method("clear_wielded_weapon"):
				character.call("clear_wielded_weapon")
		"top":
			if character.has_method("clear_worn_top"):
				character.call("clear_worn_top")
		"hat", "mask":
			if character.has_method("clear_head_slot"):
				character.call("clear_head_slot", slot_key)
		_:
			pass # hands/legs/feet/hand_l: no wearable content exists yet, nothing to clear

## Fires the currently equipped gun along the camera's own aim direction --
## origin/direction come from here (not the gun itself) since the CAMERA is
## genuinely where the player is aiming, same reasoning _punch()'s own melee
## raycast already uses for its direction (though that one's origin is the
## player's body, not the camera -- see its own comment; a gun's origin
## being the camera itself is fine, unlike melee range, since RANGE is
## nowhere near short enough for the camera-boom-length problem that fix
## was for).
func _try_fire_gun() -> void:
	if _equipped_gun == null:
		return
	var origin: Vector3 = camera.global_position
	var direction: Vector3 = -camera.global_transform.basis.z
	var fired: bool = _equipped_gun.fire(origin, direction, _is_aiming)
	# Only on an actual fire, not every click -- fire() itself already gates
	# on ammo/cooldown/reload (see its own comment) and silently no-ops on a
	# denied shot, which has no recoil to show.
	if fired:
		var character := _get_character()
		if character != null and character.has_method("shoot"):
			character.shoot()

## Sets up VoicePlayer (see player.tscn) to receive pushed audio buffers --
## runs for EVERY player instance, not just the local authority (see
## VOICE_SEND_INTERVAL_SECONDS's own comment on why). play() has to run
## before get_stream_playback() returns a real AudioStreamGeneratorPlayback
## rather than null.
func _setup_voice_playback() -> void:
	var gen := AudioStreamGenerator.new()
	gen.mix_rate = AudioServer.get_mix_rate()
	gen.buffer_length = 0.3
	voice_player.stream = gen
	voice_player.play()
	_voice_playback = voice_player.get_stream_playback()

## Only called for the local/owning peer (see _ready()) -- finds the
## AudioEffectCapture on the "Record" bus (see default_bus_layout.tres) and
## starts a microphone stream feeding it. That bus is muted (see its own
## bus/2/mute in default_bus_layout.tres) so this doesn't also audibly loop
## the raw mic input back out this player's own speakers -- muting a bus
## only silences its own final output, the AudioEffectCapture tap upstream
## of that still works.
func _setup_voice_capture() -> void:
	var bus_idx := AudioServer.get_bus_index("Record")
	if bus_idx == -1:
		return
	for i in range(AudioServer.get_bus_effect_count(bus_idx)):
		var effect := AudioServer.get_bus_effect(bus_idx, i)
		if effect is AudioEffectCapture:
			_voice_capture_effect = effect
			break
	if _voice_capture_effect == null:
		return
	_mic_player = AudioStreamPlayer.new()
	_mic_player.stream = AudioStreamMicrophone.new()
	_mic_player.bus = "Record"
	add_child(_mic_player)
	_mic_player.play()

## Called every _process frame (only reached for the local authority, see
## _process's own early-return-for-remote-peers structure) -- gates on
## push-to-talk/open-mic (see ui/settings_panel.gd's own is_push_to_talk()),
## then batches whatever's been captured since the last send at a fixed
## rate (VOICE_SEND_INTERVAL_SECONDS) rather than sending every single
## frame, keeping packet rate reasonable regardless of actual framerate.
func _process_voice_capture(delta: float) -> void:
	if _voice_capture_effect == null:
		return
	var talking: bool = Input.is_action_pressed("voice_talk") if SettingsPanel.is_push_to_talk() else true
	if not talking:
		# Discards anything captured while not transmitting -- otherwise a
		# later push-to-talk press would dump a backlog of stale/silent
		# audio all at once instead of starting clean.
		_voice_capture_effect.clear_buffer()
		return
	_voice_send_timer -= delta
	if _voice_send_timer > 0.0:
		return
	_voice_send_timer = VOICE_SEND_INTERVAL_SECONDS
	var available := _voice_capture_effect.get_frames_available()
	if available <= 0:
		return
	var samples := _voice_capture_effect.get_buffer(available)
	if Net.is_networked():
		_rpc_voice_chunk.rpc(_encode_pcm16(samples))

## Received by every OTHER peer (see @rpc's own "call_remote" -- the
## sender doesn't hear their own voice echoed back) -- pushes the decoded
## audio into THIS Player node's own VoicePlayer. Since this exact node is
## the receiving peer's own local copy of the SPEAKING peer (sitting at
## their replicated position), Godot's normal 3D audio distance attenuation
## on VoicePlayer is what actually makes this read as proximity chat, with
## no distance math needed here.
@rpc("authority", "call_remote", "unreliable")
func _rpc_voice_chunk(pcm16_bytes: PackedByteArray) -> void:
	if _voice_playback == null:
		return
	var samples := _decode_pcm16(pcm16_bytes)
	var to_push: int = mini(samples.size(), _voice_playback.get_frames_available())
	if to_push > 0:
		_voice_playback.push_buffer(samples.slice(0, to_push))

## Mono downmix (stereo capture -> mono, halving bandwidth -- voice doesn't
## need stereo) packed as signed 16-bit PCM, the same simple, universally-
## supported raw format most lightweight voice codecs use as their
## uncompressed baseline. No real compression (Opus etc.) -- deferred, not
## implemented incorrectly; see this file's own VOICE_SEND_INTERVAL_SECONDS
## comment on why this whole system is more experimental than the rest of
## this project.
func _encode_pcm16(samples: PackedVector2Array) -> PackedByteArray:
	var bytes := PackedByteArray()
	bytes.resize(samples.size() * 2)
	for i in range(samples.size()):
		var mono: float = (samples[i].x + samples[i].y) * 0.5
		var int_sample: int = int(clampf(mono, -1.0, 1.0) * 32767.0)
		bytes.encode_s16(i * 2, int_sample)
	return bytes

func _decode_pcm16(bytes: PackedByteArray) -> PackedVector2Array:
	var count: int = bytes.size() / 2
	var samples := PackedVector2Array()
	samples.resize(count)
	for i in range(count):
		var int_sample := bytes.decode_s16(i * 2)
		var f: float = float(int_sample) / 32767.0
		samples[i] = Vector2(f, f)
	return samples
