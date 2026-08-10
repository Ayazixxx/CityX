extends Node3D
## Blocky humanoid character. The head is a swappable, separately rigged
## mesh (cube/sphere/pyramid); the torso/arms/legs are ONE fused,
## seamlessly-blended mesh (see fused_body.gd) animated via real skeletal
## skinning instead of separate rigid pieces. Shares one paintable texture
## atlas across all of it.
##
## Paint raycasts are done as plain CPU ray-triangle tests against this
## node's own `global_transform` (see raycast_paint), NOT via the physics
## engine. Godot's physics server does not update a body's collision
## transform synchronously with a script-side rotate/move call -- it can
## lag by a frame or two. Since the character creator rotates the model
## and paints in the same interaction, a physics-based raycast could test
## against stale, pre-rotation geometry, landing on the wrong body part
## entirely. Reading global_transform directly has no such lag.

const FusedBody = preload("res://character/fused_body.gd")
const ComboData = preload("res://character/combo_data.gd")
const LootComponent = preload("res://loot/loot_component.gd")

# Retargeted from Mixamo mocap (see tests/scratch_mixamo_retarget.gd) rather
# than hand-authored -- baked once offline into these .tres resources so the
# game loads plain pre-computed keyframe data at runtime, not something
# that re-runs retargeting on every boot (see tests/scratch_mixamo_bake.gd,
# which produced all eight of these files).
# BoxingRightJabAnim animates "arm_l"/"forearm_l"/"hand_l" (see punch()) --
# that bone pair is the one that reads as the character's own RIGHT hand,
# not left, despite the "_l" suffix: the fused body's rest-pose "right"
# (arm_r, at local +X) ends up on the PLAYER'S OWN LEFT once the character's
# whole-body PI yaw (see main.gd, needed so the model's own front faces the
# way the player actually moves) flips that +X to world -X, which third-
# person's own camera convention (behind the player, looking the same way
# they face) renders on-screen left, not right. Same crossing used when
# mapping Mixamo's own Right*/Left* bones during retargeting.
const BoxingIdleAnim := preload("res://character/animations/boxing_idle.tres")
const BoxingRightJabAnim := preload("res://character/animations/boxing_right_jab.tres")
const BoxingLeftJabAnim := preload("res://character/animations/boxing_left_jab.tres")
const WalkAnim := preload("res://character/animations/walk.tres")
const RunAnim := preload("res://character/animations/run.tres")
const StandIdleAnim := preload("res://character/animations/idle.tres")
# jump.tres is trimmed to start right at the anticipation crouch's own low
# point (see tests/scratch_mixamo_bake.gd's own JUMP_WINDOW comment) so its
# "launch" moment lines up with our physics applying JUMP_VELOCITY instantly
# -- it still runs past the game's real ~0.92s air time into a landing/
# recovery tail, cut short by land() the instant the player actually
# touches down (see land()), not by trimming the clip itself.
const JumpAnim := preload("res://character/animations/jump.tres")
const JumpSprintAnim := preload("res://character/animations/jump_sprint.tres")
# The 4 intro-cutscene clips (see player/intro_cutscene.gd) -- falling
# loops (its own real vertical descent is driven externally, by a Tween on
# global_position:y, not by root motion -- see tests/scratch_intro_fall_
# bake.gd's own comment), the other 3 are one-shots played once per beat.
const FallingAnim := preload("res://character/animations/falling.tres")
const FallingFlatImpactAnim := preload("res://character/animations/falling_flat_impact.tres")
const GettingUpAnim := preload("res://character/animations/getting_up.tres")
const LookingAroundAnim := preload("res://character/animations/looking_around.tres")
# Unlike every other baked animation, these three also carry a HIPS POSITION
# track, not just rotation (see retarget()'s own retarget_hip_height
# comment) -- a crouch is fundamentally the whole pelvis physically
# dropping, which rotation alone can't represent on this rig (bone pose
# POSITION is otherwise fixed forever after _build_fused_body).
const StandingToCrouchAnim := preload("res://character/animations/standing_to_crouch.tres")
const CrouchToIdleAnim := preload("res://character/animations/crouch_to_idle.tres")
const CrouchWalkAnim := preload("res://character/animations/crouch_walk.tres")
# A single committed haymaker, not a left/right pair like the regular jabs --
# animates "arm_l"/"forearm_l" (same crossed-side mapping as
# BoxingRightJabAnim above), regardless of which hand the regular jab
# rotation would otherwise pick.
const HeavyPunchAnim := preload("res://character/animations/heavy_punch.tres")
# Only backward (S double-tap) dashes get a dedicated clip -- forward/left/
# right all keep player.gd's own original fallback (the regular run
# animation, forced via is_dashing there), see dash()'s own comment. A
# forward dash clip (from Dashx.fbx) was tried and baked, but dropped after
# review -- the animation itself didn't look right, not a technical issue
# with retargeting it (a pre-flight ground check had confirmed it kept the
# feet comfortably above y=0 throughout, same as the backward clip below).
# Revisit with a better source clip later if one turns up.
const DashBackwardAnim := preload("res://character/animations/dash_backward.tres")
# Retargeted from a user-supplied clip (Body Block.fbx), not Mixamo's own
# library -- same retargeting pipeline either way (see
# tests/scratch_mixamo_bake.gd's own BODY_BLOCK_WINDOW comment for the trim
# reasoning: raw clip settles into a relaxed stance before/after the actual
# guard, trimmed to just the rise-to-peak window). Both hands rise roughly
# symmetrically (not a one-sided motion like the jabs), so there's no
# crossed-hand naming question to resolve the way BoxingRightJabAnim's own
# comment above had to.
const BlockAnim := preload("res://character/animations/block.tres")

# S/A/D-held directional walk clips -- only reachable now that player.gd
# locks the body's own facing to the camera direction every frame (see its
# own class comment) instead of turning to face wherever you move; without
# that, "walking backward" always looked identical to turning around and
# playing the ordinary forward walk. Real gait cycles like walk/run above
# (full clip, no trim, LOOP_LINEAR), just at their own single native mocap
# pace -- no dedicated running variant exists for these three, so
# set_walking() always uses them at walk pace regardless of the sprint key
# (see player.gd's own MOVE_DIRECTION-gated speed selection).
const WalkBackwardAnim := preload("res://character/animations/walk_backward.tres")
const WalkLeftAnim := preload("res://character/animations/walk_left.tres")
const WalkRightAnim := preload("res://character/animations/walk_right.tres")

# Two-handed-sword moveset -- a whole separate animation pack (13 clips,
# user-supplied, filenames prefixed "2hsw_", plus a 14th sprint clip added
# later), retargeted the exact same way as everything above (see
# tests/scratch_2hsw_bake.gd, which produced all of these files -- confirmed
# via a one-off inspection, since deleted, that the pack already uses the
# same mixamorig_* bone names and "mixamo_com" animation name RetargetScript/
# BONE_MAP already expect, so no new mapping work was needed). Only takes
# effect once set_wielding_sword(true) has been called (see
# weapons/greatsword.gd's own _equip()) -- see set_walking's own
# SWORD_DIRECTIONAL_ANIM and every combat method below for how these get
# selected instead of the fist equivalents. No dedicated crouch-WALKING
# variant exists in this pack (only the standing transition) -- see
# set_walking's own comment on the fallback used for that gap. A forward
# sprint DOES exist (SwordRunAnim) but backward/strafing still don't (same
# as the fist system's own WalkBackwardAnim precedent), so those three keep
# falling back to walk pace regardless of running.
const SwordIdleAnim := preload("res://character/animations/sword_idle.tres")
const SwordWalkAnim := preload("res://character/animations/sword_walk.tres")
const SwordRunAnim := preload("res://character/animations/sword_run.tres")
const SwordWalkLeftAnim := preload("res://character/animations/sword_walk_left.tres")
const SwordWalkRightAnim := preload("res://character/animations/sword_walk_right.tres")
const SwordWalkBackwardAnim := preload("res://character/animations/sword_walk_backward.tres")
const SwordStandingToCrouchAnim := preload("res://character/animations/sword_standing_to_crouch.tres")
const SwordCrouchToIdleAnim := preload("res://character/animations/sword_crouch_to_idle.tres")
const SwordJumpAnim := preload("res://character/animations/sword_jump.tres")
const SwordAttackAnim := preload("res://character/animations/sword_attack.tres")
# A combo follow-up, added later -- plays instead of SwordAttackAnim if the
# player clicks again within SWORD_COMBO_WINDOW_SECONDS of the FIRST swing
# (see punch()'s own comment for how that's tracked). Not trimmed --
# already a reasonable single-swing length (1.83s) like SwordAttackAnim's
# own 1.27s, no long dead-hold to cut the way the charged attack needed.
const SwordAttack2Anim := preload("res://character/animations/sword_attack2.tres")
# Trimmed from a raw 4.8s source down to 2.8s -- see
# tests/scratch_2hsw_bake.gd's own CHARGED_ATTACK_WINDOW comment for the
# hand-distance scan that found this clip spends its first ~1.1s in an
# essentially static "held high" charging pose before the real committed
# lunge, which our own combat model already represents through real-time
# input holding (see player.gd's own HEAVY_PUNCH_HOLD_THRESHOLD_SECONDS)
# rather than needing the animation itself to show it.
const SwordHeavyAttackAnim := preload("res://character/animations/sword_heavy_attack.tres")
const SwordBlockAnim := preload("res://character/animations/sword_block.tres")
# Played instead of the plain block hold the instant a block actually
# reduces incoming damage (see player.gd's own take_damage(), which calls
# Character.block_impact()) -- a reaction the fist system has no equivalent
# clip for at all, so this is new behavior gated entirely on
# _wielding_sword (see block_impact's own comment).
const SwordBlockImpactAnim := preload("res://character/animations/sword_block_impact.tres")
# Played the instant a hit actually LANDS (not blocked) on a sword-wielding
# player (see player.gd's own take_damage(), which calls
# Character.hit_reaction()) -- the fist system has never had any hit-
# reaction/flinch at all (see player.gd's own _stun_remaining comment on
# that gap), so this is new behavior, also gated entirely on
# _wielding_sword.
const SwordImpactAnim := preload("res://character/animations/sword_impact.tres")

# Pistol moveset -- user-supplied Mixamo clips (PistolWalk/Run/WalkLeft/
# WalkRight/WalkBackward/Jump/MovingPistolJump/ShootingPistol/PistolAimedin),
# same retargeting pipeline as everything else here (see
# tests/scratch_pistol_bake.gd, which produced all of these files). Only
# takes effect once set_wielding_gun(true) has been called (see
# weapons/pistol.gd's own _equip()) -- see set_walking's own
# GUN_DIRECTIONAL_ANIM, jump()'s own _holding_gun branch, and shoot()/
# set_aiming() for how these get selected instead of the fist equivalents.
const GunWalkAnim := preload("res://character/animations/pistol_walk.tres")
const GunRunAnim := preload("res://character/animations/pistol_run.tres")
const GunWalkLeftAnim := preload("res://character/animations/pistol_walk_left.tres")
const GunWalkRightAnim := preload("res://character/animations/pistol_walk_right.tres")
const GunWalkBackwardAnim := preload("res://character/animations/pistol_walk_backward.tres")
# Idle (standing-still) jump vs moving jump -- unlike the fist system's own
# jump/jump_sprint split (sprinting vs not), the pistol pack's own split is
# "was the player moving at all" (walking OR sprinting both count), matching
# how this was explicitly asked for. See jump()'s own is_moving parameter.
const GunJumpAnim := preload("res://character/animations/pistol_jump.tres")
const GunJumpMovingAnim := preload("res://character/animations/pistol_jump_moving.tres")
# One-shot recoil snap, trimmed hard to 0.3s (see scratch_pistol_bake.gd's
# own comment on why -- the real fire rate is far too fast for the raw
# 1.17s clip). Played on every shot fired (see shoot()), overlaid on legs
# via the SAME leg_anim_player mechanism punch/block already use, so firing
# while walking/running doesn't freeze the character's own stepping motion.
const GunShootAnim := preload("res://character/animations/pistol_shoot.tres")
# NOT a "raise the gun" transition clip -- see its own bake comment, this is
# already the settled, held aim-down-sights pose (a subtle breathing loop,
# like StandIdleAnim). The actual transition into/out of it is an ordinary
# anim_player crossfade (see set_aiming()), not baked into the clip itself.
const GunAimInAnim := preload("res://character/animations/pistol_aim_in.tres")

# weapons/glass_bottle.gd's own throw (see begin_bottle_aim()/
# release_bottle_throw()) -- unlike GunAimInAnim above, this ISN'T a
# separately-authored held pose; it's one continuous windup-into-release
# clip (see tests/scratch_throw_bake.gd's own THROW_WINDOW/WINDUP_TIME
# comment for the raw-clip timing this was trimmed from), frozen partway
# through via seek()+speed_scale=0 while aiming and let run the rest of the
# way on an actual throw -- same seek()/speed_scale mechanism ui/
# dev_console.gd's own /gun shootpose dev tool already proved works for
# freezing mid-clip, just used here as a real gameplay mechanic instead of
# a posing aid.
const ThrowBottleAnim := preload("res://character/animations/throw_bottle.tres")
# The windup's own peak (closest cocked-back point, see
# scratch_throw_bake.gd's own WINDUP_TIME) -- begin_bottle_aim() seeks here
# and freezes; release_bottle_throw() resumes playback from wherever the
# freeze left it (always this exact time, since nothing else ever seeks
# ThrowBottleAnim elsewhere).
const THROW_BOTTLE_WINDUP_TIME := 0.66

# /dance chat emote (see dance()) -- a real ~26s freestyle routine, not a
# short tight loop, played back at LOOP_LINEAR (retarget()'s own default),
# so it just repeats the whole routine from the top each time it wraps
# around rather than needing to be trimmed down to a seamless short cycle.
const DanceAnim := preload("res://character/animations/dance.tres")
# /breakdance chat emote (see breakdance()) -- a shorter (~4.6s) footwork
# routine, same LOOP_LINEAR treatment as DanceAnim.
const BreakdanceAnim := preload("res://character/animations/breakdance.tres")

# vehicles/car.gd's own seat-transition sequence -- see play_enter_car()/
# play_exit_car(). Unlike every other animation loaded here, both of these
# carry a real hips POSITION track, not just rotation (see
# tests/scratch_mixamo_retarget.gd's own include_root_translation comment) --
# a plain rotation-only retarget can't reach "the pelvis physically moves
# through a seat-entry arc," the same reasoning the crouch clips' own
# retarget_hip_height needed, generalized to all 3 axes instead of just Y.
const EnteringCarAnim := preload("res://character/animations/entering_car.tres")
const ExitingCarAnim := preload("res://character/animations/exiting_car.tres")

# player.gd's own wall-climbing state machine (see its own _is_climbing var
# and start_climb()/CLIMB_SPEED). climb_up/climb_down are looping hand-
# over-hand cycles -- rotation-only retargets (include_root_translation
# left at its default false), same as every other locomotion cycle here
# (walk/run/sprint): player.gd's own code owns the real vertical travel
# through world space explicitly, the clip only supplies the in-place
# hand-over-hand motion, same division of responsibility retarget()'s own
# class comment documents for ordinary walking. climb_top_out (see
# play_climb_top_out()) is the one-shot "reached the ledge" mantle and,
# like entering_car/exiting_car above, DOES carry a real hips position
# track -- confirmed directly (a throwaway hip-trajectory scan during
# baking): the source clip's hips rises ~1.5 units and moves ~0.6 forward
# over its own length, a real 3D arc, not a stationary loop.
const ClimbUpAnim := preload("res://character/animations/climb_up.tres")
const ClimbDownAnim := preload("res://character/animations/climb_down.tres")
const ClimbTopOutAnim := preload("res://character/animations/climb_top_out.tres")

const ATLAS_CELL := 64
const FACE_COUNT := 6 # columns a box uses in the atlas: +X,-X,+Y,-Y,+Z,-Z
const PART_NAMES := ["head", "hips", "neck", "arm_l", "arm_r", "thigh_l", "thigh_r"]
const PART_COUNT := 7 # rows in the atlas, one per body part
const VALID_HEAD_SHAPES := ["cube", "sphere", "pyramid"]
# "none" (no hair) is always valid and isn't a real imported asset -- every
# other entry maps to a .glb under character/hair/ (see HAIR_SCENES). A
# fixed enum here, same "closed string list, not a real GDScript enum"
# convention VALID_HEAD_SHAPES already established, since a save file just
## stores this as a plain string (see character_save.gd's own hair_style
# field).
const VALID_HAIR_STYLES := ["none", "embermane"]
# How far the character creator's own height slider can stretch/squash a
# character, as a plain uniform scale on this node (see set_height_scale) --
# 1.5x taller, or the same factor SHORTER (1/1.5, not 0.5, so "tallest" and
# "shortest" are the same multiple away from the default in either
# direction rather than the short end reaching twice as far as the tall
# one). Stored as a float on the save file, so a value outside this range
# from an old/hand-edited save is clamped rather than rejected.
const MIN_HEIGHT_SCALE := 1.0 / 1.5
const MAX_HEIGHT_SCALE := 1.5
const DEFAULT_HEIGHT_SCALE := 1.0
# One imported mesh per non-"none" style. Extracted from a Meshy-generated
# head+hair model (the head/face/neck removed via an offline Blender pass --
# see character/hair/ for the source pipeline notes) and pre-scaled to fit
# THIS project's own head geometry (a 0.7-unit box centered at local (0,
# 0.35, 0) relative to get_head_node(), matching wearables/wearable.gd's own
# documented head geometry).
const HAIR_SCENES := {
	"embermane": preload("res://character/hair/embermane.glb"),
}
# The exported mesh's own baked-in origin sits near its TOP with the spiky
# mass hanging BELOW it (confirmed directly, via its real AABB: y from
# -0.7196 to 0.1191, i.e. almost the entire mesh is at negative Y) -- with
# no correction at all, this placed the hair down around chest height
# instead of on the head (confirmed via a real in-game screenshot). A pure
# position offset (an earlier attempt, centering the mesh's own vertical
# midpoint on the head box's center) got the HEIGHT right but not the
# ORIENTATION: the mesh's spikes point in -Y from its own origin, i.e.
# downward once placed upright, which read in-game as hair growing
# upside-down off the back/underside of the head rather than up and out of
# the scalp (confirmed via a follow-up in-game screenshot). Confirmed via
# an offline Blender render (this project's own tooling still can't produce
# a real GPU render of the live character directly, but CAN render the raw
# mesh asset in isolation against a stand-in head shape) that rotating 180
# degrees around local X flips the spikes to point up and out correctly --
# see _build_hair()'s own rotation.x assignment. Positioning now only needs
# to place the mesh's origin (where the now-upward-pointing roots cluster)
# at the head's own TOP surface, not its center -- see _build_hair() for
# why that's computed from _parts["head"] rather than a hardcoded constant.
# Hair has no paintable-atlas UVs (unlike the body/head, see _shared_material)
# -- it gets its own single flat color for now. Color/texture/uploaded-PNG
# customization is explicit FUTURE work (see wearables/t_shirt.gd's own
# class comment on the same "just the model for now" scope), so one shared
# material is enough: a later customization pass has exactly one place to
# swap albedo_color/albedo_texture.
const HAIR_COLOR := Color(0.75, 0.55, 0.15)
# A vertex at the very edge of its own UV range (e.g. a box face's v_local=0
# or 1) maps to EXACTLY row/PART_COUNT or (row+1)/PART_COUNT -- and since
# adjacent rows share that same boundary value by construction, floating-
# point rounding right at that knife-edge can push the computed pixel
# across into the NEIGHBORING part's row (confirmed: painting the head's
# very bottom edge aliased with the torso's own far end, at the crotch,
# even though they're on opposite sides of a shared row boundary and
# nowhere near each other in 3D). Insetting every row's usable v-range by
# a couple of pixels keeps this from ever happening, regardless of float
# precision.
const ROW_V_MARGIN := 2.0 / float(ATLAS_CELL * PART_COUNT)

const HEAD_PIVOT_NAME := "HeadPivot"

# See show_speech_bubble()'s own comment for the full picture.
const SPEECH_BUBBLE_DURATION_SECONDS := 6.0
const SPEECH_BUBBLE_HEIGHT_OFFSET := 0.35
const BODY_SKELETON_NAME := "BodySkeleton"

# See _update_nameplate()'s own comment for the full picture. Sits ABOVE
# SPEECH_BUBBLE_HEIGHT_OFFSET so a name and an active chat bubble don't
# overlap when both happen to be showing at once.
const NAMEPLATE_RANGE_METERS := 6.0 # roughly 7 steps
const NAMEPLATE_LOOK_DOT_THRESHOLD := 0.85
# Alpha units/second -- a full 0->1 (or 1->0) fade takes 1/this many
# seconds, so 6.0 is a snappy ~0.17s, matching a Call of Duty-style
# nameplate popping in/out quickly rather than a slow cross-dissolve.
const NAMEPLATE_FADE_SPEED := 6.0
const NAMEPLATE_HEIGHT_OFFSET := 0.55

# Combat mode: entered the instant the first punch lands (see punch()),
# held while punches keep coming, dropped back to a normal idle after this
# long without another one -- matches how most games' combat stances decay
# rather than staying up forever after a single hit.
const COMBAT_MODE_TIMEOUT_SECONDS := 3.0

# The three crouch clips (standing_to_crouch/crouch_walk/crouch_to_idle) are
# separately-authored Mixamo assets that don't agree on how deep a "crouch"
# actually is -- confirmed directly: standing_to_crouch's own hips end up at
# roughly half the height crouch_walk holds during its own loop, a ~0.41m
# gap on this rig. Cutting between them instantly reads as the character
# suddenly popping upright the moment they start walking while crouched.
# Blending (Godot's own built-in cross-fade, via AnimationPlayer.play's
# custom_blend) smooths that mismatch into a quick transition instead of an
# instant snap.
const CROUCH_BLEND_SECONDS := 0.2
# General cross-fade for every OTHER state change in this file (walk/run,
# idle, combat_idle, punches) -- these used to snap instantly on the theory
# that their clips/poses already agree with each other closely enough that a
# blend would just add mushy latency for no reason. Confirmed directly that
# this was wrong in practice: an instant cut from, say, a punch's own
# recovery frame back to combat_idle's neutral guard reads as a visible pop/
# snap, not a clean transition -- the poses are SIMILAR, not identical, and
# even a short cross-fade over the gap reads as fluid instead. Short enough
# (0.1s) to stay snappy for combat -- this is purely a visual smoothing of
# the POSE transition, not a gameplay delay: damage/hit timing in player.gd's
# own _punch() and the JUMP_VELOCITY physics impulse both already happen
# independent of however the animation itself blends in.
const BLEND_SECONDS := 0.1

# The bones leg_anim_player is allowed to touch -- see its own @onready var
# comment. Deliberately just the legs, not "hips" too: hips is the root
# bone's own pivot (position, not rotation, is what actually moves it, and
# that's driven by CharacterBody3D movement in player.gd, not an animation
# track -- see block.tres's own comment on why every clip already excludes
# hips from its tracks entirely), so there's nothing on hips for this
# overlay to usefully own.
const LEG_OVERLAY_BONES := ["thigh_l", "shin_l", "thigh_r", "shin_r"]

# Real physics ragdoll (RigidBody3D capsules + joints, actually falling
# and colliding with the world -- a scripted tween was tried first and
# didn't hold up, see git history) rather than Godot's own PhysicalBone3D
# auto-jointing (which has its own auto-detection quirks this hand-built
# version sidesteps entirely). Every joint is a ConeTwistJoint3D (including
# elbows/knees/wrists): a single joint type that "just works" without
# needing to get a HingeJoint3D's own rotation axis oriented exactly right,
# with a narrow twist span standing in for a hinge-like bend well enough for
# a death effect.
#
# Driven directly off FusedBody.BONE_PARENT now (a real skeletal hierarchy,
# not the old rig's mostly-independent-roots one) instead of a separate,
# hand-curated RAGDOLL_PARENT chain -- the two used to need to diverge
# because the skinning system never needed a shoulder/hip parent for
# anything; now that the skeleton IS the real hierarchy, maintaining a
# second, parallel copy of it here would just be one more place for the two
# to silently drift apart. "head" is the one BONE_PARENT entry with no
# ragdoll body of its own (see _ragdoll_posable_bones -- it has no capsule/
# segment at all, see FusedBody.segment_defs, so there's nothing to give it
# a RigidBody3D shape).
const RAGDOLL_JOINT_LIMITS := {
	"spine": {"swing_degrees": 30.0, "twist_degrees": 15.0},
	"neck": {"swing_degrees": 40.0, "twist_degrees": 30.0},
	"arm_l": {"swing_degrees": 70.0, "twist_degrees": 30.0},
	"arm_r": {"swing_degrees": 70.0, "twist_degrees": 30.0},
	"forearm_l": {"swing_degrees": 80.0, "twist_degrees": 5.0},
	"forearm_r": {"swing_degrees": 80.0, "twist_degrees": 5.0},
	# Wrist/knee limits are FIRST-PASS starting values, not tuned against
	# real falling-ragdoll footage the way the rest of this table already
	# was -- see the rig-rebuild plan's own Phase 3.
	"hand_l": {"swing_degrees": 50.0, "twist_degrees": 25.0},
	"hand_r": {"swing_degrees": 50.0, "twist_degrees": 25.0},
	"thigh_l": {"swing_degrees": 50.0, "twist_degrees": 20.0},
	"thigh_r": {"swing_degrees": 50.0, "twist_degrees": 20.0},
	"shin_l": {"swing_degrees": 40.0, "twist_degrees": 10.0},
	"shin_r": {"swing_degrees": 40.0, "twist_degrees": 10.0},
}
# A dedicated collision layer so ragdoll pieces only ever collide with the
# static world (layer 1) -- never each other (an elbow and its own upper
# arm start out overlapping at the joint, which would otherwise fling them
# apart on the very first physics step) and never the player's own
# now-irrelevant standing collision capsule (temporarily disabled anyway,
# see ragdoll() itself, but belt-and-suspenders).
const RAGDOLL_COLLISION_LAYER := 8
# Bodies starting from plain rest (zero velocity) take a beat of pure
# free-fall before gravity alone produces enough visible motion to read as
# "falling" -- physically real, but from a standing start it looked like
# the character was still standing/playing some idle animation for a
# moment before finally keeling over. A mildly-stronger-than-real gravity
# scale keeps that beat short without the fall reading as unnaturally fast.
const RAGDOLL_GRAVITY_SCALE := 1.5
# Only the TORSO gets an initial spin (see ragdoll()) -- every OTHER body
# starts with zero angular velocity of its own and is dragged into motion
# purely by the joints reacting to the torso's spin and gravity, the same
# way a real ragdoll's limbs only move because something else is pulling on
# them. An earlier version gave every single body (torso, both arms, both
# forearms, both legs) its OWN independent random spin, which was the actual
# source of the reported "jank": 7 unrelated rotations kicking off on the
# same frame reads as the whole rig exploding into flailing motion, not a
# body going limp and toppling over as one coherent unit.
const RAGDOLL_TORSO_SPIN_DEGREES_PER_SEC := 90.0
# Applied per body (not just the torso) so the whole rig loses energy and
# actually comes to rest in a heap instead of jittering at the joints
# indefinitely -- angular damping is deliberately much higher than linear:
# it's rotational jitter at the joints (limbs quivering in place) that reads
# as unstable/janky, not the body's overall fall, which should stay
# unimpeded.
const RAGDOLL_LINEAR_DAMP := 0.6
const RAGDOLL_ANGULAR_DAMP := 4.0
# Real capsule volume (cylinder + one sphere's worth of end caps) times a
# flat density, so every ragdoll body's MASS is proportional to how much of
# the character it actually represents -- Godot's own default (mass = 1 for
# every RigidBody3D regardless of size) made the torso and a forearm equally
# heavy, which gave the joint solver no sense that the torso should dominate
# a limb's motion rather than the other way around. That's what made the
# rig read as flailing around its own light extremities instead of a heavy
# torso falling and dragging comparatively light limbs along with it, the
# way an actual body's weight distribution would. The density value itself
# is arbitrary (this project's capsule proportions are stylized, not
# anatomically exact, so it was never going to land on real human kg
# anyway) -- picked to land the torso in a plausible "feels heavy" range;
# only the RATIOS between body parts actually matter for how the joints
# behave.
const RAGDOLL_DENSITY := 700.0

# Shared by every ragdoll body (see _make_ragdoll_body) -- previously no
# ragdoll body anywhere had any physics_material_override at all, so
# nothing ever bounced; a real reported ask (see player.gd's own grab/carry
# system) for a dropped/carried body to actually bounce like real gravity
# instead of a dead thud. One shared Resource, not one per body -- it's
# stateless data, same "share one resource across many nodes" precedent
# city_chunk.gd's own materials dict already establishes.
static var RAGDOLL_PHYSICS_MATERIAL: PhysicsMaterial = _build_ragdoll_physics_material()

static func _build_ragdoll_physics_material() -> PhysicsMaterial:
	var mat := PhysicsMaterial.new()
	mat.bounce = 0.25
	mat.friction = 0.7
	return mat

# Applied on TOP of the normal volume-based mass (see RAGDOLL_DENSITY's own
# comment) for specific bones -- "neck" is the only entry: the rig has no
# separate head capsule (_ragdoll_posable_bones excludes "head" entirely),
# so grabbing "the head" really means grabbing the neck, which otherwise
# computes as the single LIGHTEST bone in the whole rig (lighter even than
# a hand) purely because it's geometrically thin. NECK_R (fused_body.gd) is
# a VISUAL/joint-placement constant shared with the character's own normal
# (non-ragdolled) mesh -- bumping it directly to fix this would also make
# every character's neck visibly thicker at all times, an unwanted side
# effect. A flat mass multiplier here changes only the ragdoll's own
# physics weight, leaving the visual/collision capsule exactly as before.
const RAGDOLL_MASS_MULTIPLIER := {
	"neck": 2.5,
}

# Every body/head mesh is on this visual layer IN ADDITION to the default
# layer 1 (so it still renders normally in the real game view) -- lets the
# HUD's portrait camera (see hud.gd's own PORTRAIT_VISUAL_LAYER) cull_mask
# down to just this layer and see ONLY the character, not whatever city
# geometry happens to share the same World3D behind them.
const PORTRAIT_VISUAL_LAYER := 1 << 19
## Set on ONE character at a time, by whoever is previewing it (see
## ui/inventory_panel.gd). PORTRAIT_VISUAL_LAYER can't do this job: every
## Character in the game carries it, including civilians, so a full-body
## preview camera culling on it catches whoever happens to be walking past
## behind the player. This layer is only ever on the character being looked
## at, so the background is guaranteed empty.
const PREVIEW_VISUAL_LAYER := 1 << 17

@onready var anim_player: AnimationPlayer = $AnimationPlayer
## A second, independent AnimationPlayer driving ONLY the 4 leg bones (see
## LEG_OVERLAY_BONES) -- see set_walking()'s own comment for why: punch/
## block/heavy_punch are full-body clips (every one carries its own baked
## leg pose, a static boxing stance), so while one of those owns anim_player
## the legs would otherwise be stuck showing that stance's fixed pose no
## matter how much WASD is held. This player plays a leg-only extract of
## walk/run (see _build_leg_only_animation) in parallel whenever moving, so
## its output simply overwrites anim_player's own (now-stale) leg pose each
## frame -- the two players never fight over anything else, since this one
## only ever has tracks for thigh_l/shin_l/thigh_r/shin_r.
@onready var leg_anim_player: AnimationPlayer = $LegAnimPlayer

var atlas_size: int = ATLAS_CELL * FACE_COUNT
var atlas_height: int = ATLAS_CELL * PART_COUNT

var atlas_image: Image
var atlas_texture: ImageTexture

# Flat triangle lists used for manual ray-triangle painting, built by
# concatenating the fused body's triangles (rest-pose positions, generated
# once) with the head's (regenerated whenever its shape changes). A
# "face_index" elsewhere in this file always means an index into
# tri_positions/tri_clamp (idx*3..idx*3+2 for tri_positions, idx directly
# for tri_clamp). tri_clamp holds, per triangle, the atlas pixel rect a
# brush stroke on that triangle must be clamped to (see _stamp) -- kept per
# triangle rather than derived from a fixed formula because the head's
# shape (and therefore its face layout) is swappable.
var tri_positions: PackedVector3Array = PackedVector3Array()
var tri_uvs: PackedVector2Array = PackedVector2Array()
var tri_clamp: Array[Dictionary] = []

# Where (in atlas UV) and on which part the last stamp of the current
# stroke landed -- lets _stamp_bridged fill the gap back to it. x < 0 means
# "no stroke in progress" (see begin_paint_stroke).
var _last_stamp_uv := Vector2(-1.0, -1.0)
var _last_stamp_clamp: Dictionary = {}

# World-space distance covered by one atlas pixel of U (circumference) and
# one atlas pixel of V (length), at the torso's own idealized straight
# cross-section -- the "normal" per-axis density every triangle's own local
# density gets compared against when stamping (see paint_at). These two are
# NOT equal (the atlas gives every part row a fixed 64px height regardless
# of the limb's actual length, while a row's WIDTH covers that limb's full
# circumference, which varies by radius) -- painting a circular brush
# stamp in flat pixel space ignores that and comes out visibly OVAL on the
# actual body, stretched along whichever axis is less pixel-dense.
var _reference_tangent_len: float
var _reference_bitangent_len: float
# A SINGLE world-per-pixel calibration (geometric mean of the two above),
# used for BOTH axes when sizing a stamp -- comparing each axis only
# against its OWN reference (tangent vs tangent, bitangent vs bitangent)
# was the first, wrong attempt at this: at an ordinary point the local and
# reference values roughly match on both axes independently, which gives
# r_u == r_v (an ISOTROPIC pixel-space stamp) -- but an isotropic pixel
# stamp is exactly what reads as OVAL on the body, since U and V don't
# cover equal world distance per pixel to begin with. Sizing both axes off
# one shared reference instead makes r_u and r_v come out DIFFERENT at an
# ordinary point (bigger on the denser axis, smaller on the sparser one),
# which is what actually cancels the atlas's own baked-in anisotropy.
var _reference_brush_len: float

var _body_tri_positions: PackedVector3Array = PackedVector3Array()
var _body_tri_uvs: PackedVector2Array = PackedVector2Array()
var _body_tri_clamp: Array[Dictionary] = []
var _head_tri_positions: PackedVector3Array = PackedVector3Array()
var _head_tri_uvs: PackedVector2Array = PackedVector2Array()
var _head_tri_clamp: Array[Dictionary] = []

var _parts := {}
var _head_pivot: BoneAttachment3D
var _speech_bubble: Label3D
var _speech_bubble_timer := 0.0
var _nameplate: Label3D
var _nameplate_alpha := 0.0
var _hand_r_pivot: BoneAttachment3D
# bone_name -> BoneAttachment3D, lazily created/cached by get_bone_node() --
# see that function's own comment for why clothing (wearables/clothing.gd)
# needs this instead of the single dedicated _head_pivot/_hand_r_pivot vars
# every earlier wearable/weapon got away with.
var _bone_attachments := {}
# The currently-worn "top" (t-shirt/hoodie, see wearables/clothing.gd)'s own
# set of per-bone piece nodes -- tracked HERE, not on the wearable instance
# itself (which queue_free()s right after equip, see clothing.gd's own
# _equip()), since the pieces end up scattered across several different
# BoneAttachment3D parents instead of sharing one, unlike every head
# wearable's single get_head_node() parent.
var _worn_top_parts: Array[Node3D] = []
## Tracked separately from the top (see set_worn_legs) so a shirt and
## trousers can be worn at once.
var _worn_legs_parts: Array[Node3D] = []
var _body_skeleton: Skeleton3D
# The fused body's own MeshInstance3D, plus the full mesh it starts with --
# kept so set_body_stubs_only() can swap between that and the stub body (see
# fused_body.gd's own stub_mesh()) without rebuilding anything.
var _body_mesh_instance: MeshInstance3D
var _full_body_mesh: ArrayMesh
var _body_stubs_only := false
# The shared baked skin, kept so set_worn_arm_pose() can swap back to it --
# it must never be written into (see fused_body_cache.gd: one Skin is shared
# by every Character in the game).
var _full_body_skin: Skin
var _worn_arm_pose := Vector3.ZERO
var _preview_layer_enabled := false
## How much of a worn arm rotation the FOREARM takes. The upper arm takes
## all of it and the hand takes none (see set_worn_arm_pose) -- this is the
## middle of that ramp, and what decides whether the arm reads as bowing
## smoothly or kinking at the elbow. Lower keeps the forearm straighter and
## puts more of the bend in the shoulder.
const ARM_POSE_FOREARM_SHARE := 0.35
var _head_shape := "sphere"
var _hair_style := "none"
var _height_scale := DEFAULT_HEIGHT_SCALE
var _hair_mesh_instance: MeshInstance3D
var _hair_material: StandardMaterial3D
var _character_name := ""
var _shared_material: StandardMaterial3D

var _is_punching := false
var _is_jumping := false
# Only true while a dedicated dash CLIP (forward/backward, see dash()) is
# playing -- a left/right dash never sets this, since those still fall back
# to player.gd's own original "force the run animation" handling with no
# clip of their own here. Blocks set_walking the same way _is_punching/
# _is_jumping already do, and gets defensively cleared wherever punch()/
# jump()/start_crouch() unconditionally take over the animation player
# anyway -- same "don't leave a one-shot's own flag stuck true forever if
# something else interrupts it" reasoning _is_crouch_transitioning already
# needed (see set_walking's own movement branch).
var _is_dashing := false
# True for exactly as long as the block clip is playing -- player.gd's own
# take_damage() checks is_blocking() to decide whether an incoming hit gets
# reduced (see block()'s own comment), so unlike _is_punching/_is_jumping
# (which only ever gate ANIMATION selection if left stuck true), a stuck
# _is_blocking would silently keep reducing damage forever, not just show
# the wrong pose -- defensively cleared wherever punch()/heavy_punch()/
# jump()/dash() unconditionally take over the animation player, same
# reasoning _is_dashing's own comment already gives for why THAT needs the
# same defensive clearing.
var _is_blocking := false
var _is_crouch_transitioning := false
# Set by weapons/greatsword.gd's own _equip() via set_wielding_sword() the
# instant a greatsword is picked up -- swaps set_walking()/punch()/
# heavy_punch()/block()/jump()/start_crouch()/end_crouch() over to the
# 2hsw_*-sourced clips (see their own preload comments above) instead of
# the fist ones, using the exact same trigger methods/state flags player.gd
# already calls -- player.gd itself needs no changes at all for this,
# since every one of those methods keeps its existing name/signature and
# just internally picks a different clip.
var _wielding_sword := false
# Set by weapons/pistol.gd's own _equip() via set_wielding_gun() -- same
# "swap set_walking()/jump() over to a different clip pack, no player.gd
# changes needed" pattern _wielding_sword's own comment already explains.
var _holding_gun := false
# Set by weapons/glass_bottle.gd's own _equip() via set_wielding_bottle() --
# a bottle is a single fixed-damage melee swing (see player.gd's own
# _try_swing_bottle(), which sources damage from the bottle's own current
# durability state), NOT a combo chain, so punch() needs a real third
# branch here rather than falling through to the unarmed chain's own
# ComboData-driven else (which would silently advance/corrupt
# _combo_stage_index/_combo_window_timer on every bottle swing).
var _wielding_bottle := false
# Set by set_aiming() (called from player.gd whenever its own _is_aiming
# edge-changes, see there) -- while true AND standing still, set_walking()
# shows GunAimInAnim instead of the normal idle/combat_idle. Has no effect
# at all unless _holding_gun is also true.
var _is_aiming := false
# True for exactly as long as GunShootAnim is playing -- same "one-shot clip
# owns anim_player until animation_finished" pattern _is_punching already
# uses (see set_walking's own early-return and leg-overlay branches, both
# extended to include this), kept as its own separate flag rather than
# reusing _is_punching so a fist punch and a gunshot can never be confused
# for each other by _on_animation_finished's own name-based dispatch.
var _is_shooting := false
# True while the /dance chat emote (see dance()) is playing -- a LOOPING
# clip (LOOP_LINEAR, no natural "finished" signal to clear this on, unlike
# _is_punching/_is_shooting/etc), so it only ever gets cleared by something
# else deliberately taking back over (movement, jumping, combat, ...) via
# the exact same defensive-clear sites every other one-shot action already
# uses -- see set_walking's own early-return/leg-overlay branches, both
# extended to include this, for how moving during a dance ends it cleanly.
var _is_dancing := false
# True while entering_car/exiting_car (see play_enter_car()/play_exit_car())
# is playing -- same "one-shot clip owns anim_player until animation_finished"
# pattern _is_punching/_is_shooting/etc already use. vehicles/car.gd waits on
# anim_player's own animation_finished signal to know when the clip is done
# before actually completing the seat/possession handoff (hide/reparent/
# collision), same as every other one-shot clip's own caller reacts to
# _on_animation_finished internally -- this flag exists purely so OTHER
# actions (movement, combat, ...) can't interrupt mid-transition, matching
# every other one-shot flag's own defensive-clear sites below.
var _is_car_transitioning := false
# True while player.gd's own climbing state machine has this body attached
# to a wall (climb_up/climb_down looping) OR playing the one-shot
# climb_top_out mantle -- same "blocks set_walking from stomping a special
# animation" role _is_car_transitioning already plays (see its own
# comment), covering both the sustained climb AND the topping-out clip
# with one flag since set_walking should stay hands-off for either.
var _is_climbing := false
# True while a bottle throw's own windup is being held (right-click aim, see
# begin_bottle_aim()) OR the release/follow-through is actually playing
# (see release_bottle_throw()) -- one flag covers both phases, unlike
# _is_punching/_is_shooting's own "clip owns anim_player until it finishes
# naturally" pattern, since the held windup phase has NO natural end of its
# own (speed_scale is 0, frozen indefinitely) -- only cancel_bottle_aim()
# (aim released without throwing) or the clip's own animation_finished
# (once release_bottle_throw() lets it play out) ever clears this. Gates
# set_walking() the same way _is_punching/_is_blocking/etc already do.
var _is_aiming_throw := false
var _in_combat_mode := false
var _combat_mode_timer := 0.0
# How long after the FIRST sword swing (sword_attack) a second click still
# counts as a combo follow-up (sword_attack2) instead of restarting the
# combo from the first swing again -- see punch()'s own comment. Ticked
# down in _physics_process the same "countdown, not an absolute timestamp"
# way _combat_mode_timer/_sprint_tap_window_remaining (player.gd) already
# do, for the same reason: every other timing value in this file already
# uses this style.
const SWORD_COMBO_WINDOW_SECONDS := 5.0
# True for exactly SWORD_COMBO_WINDOW_SECONDS after sword_attack fires --
# the next punch() call while this is still true plays sword_attack2
# instead and clears it, so a 3rd click (whether inside or outside the
# window) always restarts the combo at sword_attack, matching a plain
# 2-hit combo rather than an indefinite alternation.
var _sword_combo_ready := false
var _sword_combo_timer := 0.0
# True for exactly as long as a sword SWING clip (sword_attack/
# sword_attack2/sword_heavy_attack) is actually playing -- deliberately
# separate from _is_punching, which ALSO covers hit_reaction()/
# block_impact() (see their own comments on why those need to stay freely
# interruptible by a real attack -- getting hit shouldn't lock out
# fighting back). punch()/heavy_punch() drop the input entirely while this
# is true, rather than restarting/switching the animation -- confirmed as
# a real reported bug: player.gd's own PUNCH_COOLDOWN_SECONDS (0.35s) was
# tuned for the much shorter fist jab clips and doesn't come close to
# covering sword_attack/sword_attack2's own 1.27s/1.83s runtime, so a
# spam-click could re-trigger play() well before the current swing had
# actually finished, visibly cutting it off mid-animation.
var _sword_swing_in_progress := false
# Indexed unarmed combo chain (see combo_data.gd) -- kept entirely separate
# from _sword_combo_ready/_sword_combo_timer above rather than generalizing
# them, since sword stays on its own existing 2-hit boolean mechanic for
# this pass (see combo_data.gd's own header comment). _combo_window_timer
# ticks down the same "countdown, not an absolute timestamp" way
# _sword_combo_timer does; _combo_stage_index resets to 0 once it lapses
# with no follow-up punch. _last_combo_stage is read by player.gd's own
# get_last_attack_combo_info() this same frame, right after punch() plays
# it, to decide the actual take_damage() call for whatever just landed.
var _combo_stage_index := 0
var _combo_window_timer := 0.0
var _last_combo_stage := {}
# Cached once in _build_fused_body -- the hips bone's own rest/standing pose
# position, i.e. what it should be at any time NONE of the crouch clips are
# involved. Only standing_to_crouch/crouch_to_idle/crouch_walk carry a hips
# POSITION track at all (see their own preload comments); every other clip
# (walk/run/idle/combat_idle/punches/jump) only ever touches ROTATION
# tracks, so whatever set_walking last resolves them to just inherits
# whatever hips position was last written -- see _reset_hips_position.
var _hips_base_position := Vector3.ZERO
# The hips' own settled Y height at sword_standing_to_crouch's LAST
# keyframe -- computed once in _ready() (see _static_crouch_hip_y) directly
# from the baked clip's own data, not observed at runtime. Needed because
# the sword's own crouching+moving case (see set_walking's own
# _wielding_sword-and-crouching branch) can't rely on a one-time "already
# playing this clip" guard to keep hips correctly grounded the way every
# other held pose in this file gets away with -- see that branch's own
# comment for the full story on why.
var _sword_crouch_hip_y := 0.0

var _is_ragdolled := false
var _rest_rotation := Vector3.ZERO
var _ragdoll_rig: Node3D = null
var _ragdoll_bodies := {} # bone name -> RigidBody3D
# Captured once at ragdoll start, per non-torso bone: its rigid body's own
# orientation relative to the torso body's, and the bone's own actual pose
# rotation at that same instant -- see _physics_process for why both are
# needed to avoid a visible "pop" the instant physics takes over.
var _ragdoll_initial_relative := {} # bone name -> Basis
var _ragdoll_initial_pose := {} # bone name -> Quaternion
var _ragdoll_player_collision_layer := 0
var _ragdoll_player_collision_mask := 0

const MAX_UNDO_STEPS := 30
var _undo_stack: Array[Image] = []
var _redo_stack: Array[Image] = []
var _paintable := true

func _ready() -> void:
	var row_v_span: float = 1.0 / float(PART_COUNT) - 2.0 * ROW_V_MARGIN
	_reference_tangent_len = (TAU * FusedBody.TORSO_R) / float(atlas_size)
	_reference_bitangent_len = (FusedBody.SHOULDER_Y - FusedBody.HIP_Y) / (row_v_span * float(atlas_height))
	_reference_brush_len = sqrt(_reference_tangent_len * _reference_bitangent_len)
	_define_parts()
	_build_atlas()
	_build_fused_body()
	_build_head()
	_build_walk_animation()
	_sword_crouch_hip_y = _static_track_last_position_y(SwordStandingToCrouchAnim)
	anim_player.animation_finished.connect(_on_animation_finished)
	_build_speech_bubble()
	_build_nameplate()

func _process(delta: float) -> void:
	_update_nameplate(delta)

	if _speech_bubble_timer <= 0.0:
		return
	_speech_bubble_timer -= delta
	if _speech_bubble_timer <= 0.0:
		_speech_bubble.visible = false
		return
	# Tracks the head's own live world position every frame rather than
	# being parented under get_head_node() directly -- that BoneAttachment3D
	# (_head_pivot) gets freed and rebuilt from scratch whenever head shape
	# changes (see _build_head()'s own comment), which would silently
	# destroy a bubble parented under it. A plain child of Character itself,
	# with its GLOBAL position pushed to match the head each frame, sidesteps
	# that lifecycle entirely -- only costs a per-frame update while a
	# message is actually showing, not the common case.
	_speech_bubble.global_position = get_head_center_world() + Vector3(0, SPEECH_BUBBLE_HEIGHT_OFFSET, 0)

## Chat message shown as a floating Label3D above this character's own head
## for SPEECH_BUBBLE_DURATION_SECONDS -- see ui/chat_box.gd (the input UI)
## and player.gd's own chat-broadcast RPC, which calls this on both the
## sender's own character (locally) and every other peer's copy of it (once
## the RPC arrives). "Proximity" chat isn't enforced by network filtering --
## the message itself is broadcast to everyone the same way position/
## animation state already is -- it's proximity in the same sense the
## bubble itself is: a small 3D-space label, only actually readable up
## close, the same way a real in-person speech bubble would be.
func show_speech_bubble(text: String) -> void:
	_speech_bubble.text = text
	_speech_bubble.visible = true
	_speech_bubble_timer = SPEECH_BUBBLE_DURATION_SECONDS
	_speech_bubble.global_position = get_head_center_world() + Vector3(0, SPEECH_BUBBLE_HEIGHT_OFFSET, 0)

func _build_speech_bubble() -> void:
	_speech_bubble = Label3D.new()
	_speech_bubble.visible = false
	_speech_bubble.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	_speech_bubble.no_depth_test = true
	_speech_bubble.font_size = 48
	_speech_bubble.outline_size = 10
	_speech_bubble.modulate = Color.WHITE
	_speech_bubble.outline_modulate = Color(0, 0, 0, 1)
	_speech_bubble.pixel_size = 0.004
	add_child(_speech_bubble)

func _build_nameplate() -> void:
	_nameplate = Label3D.new()
	_nameplate.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	_nameplate.no_depth_test = true
	_nameplate.font_size = 40
	_nameplate.outline_size = 10
	_nameplate.modulate = Color(1, 1, 1, 0) # starts fully faded out
	_nameplate.outline_modulate = Color(0, 0, 0, 1)
	_nameplate.pixel_size = 0.004
	add_child(_nameplate)

## Fades this character's own name in above their head whenever the LOCAL
## viewer (whichever Player on THIS machine has is_multiplayer_authority(),
## found via the "players" group every player.gd's own _ready() already
## joins -- same group hostile NPCs already use to find every player body)
## is within NAMEPLATE_RANGE_METERS and actually looking at them, fading it
## back out the instant either stops being true -- a real, explicit ask
## ("fade in like call of duty... fade out when you look away"). Runs
## every frame for every Character (both the local player's own copy and
## every remote peer's), same as the existing speech-bubble head-tracking
## already does, but unconditionally rather than gated on a timer, since
## look-direction can change every frame regardless of chat activity.
##
## Never shows above the LOCAL viewer's own head (see the early return
## below) -- nobody needs their own name floating in front of their own
## camera.
func _update_nameplate(delta: float) -> void:
	if _nameplate == null:
		return
	var owner_player: Node = get_parent()
	var is_local_players_own_character: bool = owner_player != null and owner_player.has_method("is_multiplayer_authority") and owner_player.is_multiplayer_authority()

	var should_show := false
	if not is_local_players_own_character:
		should_show = _local_viewer_is_looking_at_me()

	var target_alpha: float = 1.0 if should_show else 0.0
	_nameplate_alpha = move_toward(_nameplate_alpha, target_alpha, NAMEPLATE_FADE_SPEED * delta)
	if _nameplate_alpha <= 0.0 and not should_show:
		return # skip the per-frame position/text update entirely once fully invisible and staying that way
	_nameplate.text = _character_name
	_nameplate.modulate.a = _nameplate_alpha
	_nameplate.global_position = get_head_center_world() + Vector3(0, NAMEPLATE_HEIGHT_OFFSET, 0)

## Finds the LOCAL machine's own authority Player (there's always at most
## one per running instance -- every OTHER Player in the "players" group is
## a remote peer's own replicated copy, see player.gd's own is_multiplayer_
## authority() gating) and checks whether IT is close enough to and looking
## at THIS character's own head. Same range/dot-product shape as every
## other "am I looking at X" check in this project (see weapons/pistol.gd's
## own _player_is_looking_at_me), just camera-eye-to-head instead of
## camera-eye-to-waist, since a nameplate sits well above ground level
## unlike a dropped weapon.
func _local_viewer_is_looking_at_me() -> bool:
	var local_player: Node3D = null
	for candidate in get_tree().get_nodes_in_group("players"):
		if candidate.has_method("is_multiplayer_authority") and candidate.is_multiplayer_authority():
			local_player = candidate
			break
	if local_player == null:
		return false
	var camera: Camera3D = local_player.get("camera")
	if camera == null:
		return false
	var head_pos := get_head_center_world()
	# Distance/direction measured from the VIEWER'S OWN BODY (matching
	# weapons/pistol.gd's own _player_is_looking_at_me convention), NOT the
	# camera -- a real bug caught here directly: this project's camera is a
	# third-person SpringArm3D that pulls back several meters BEHIND the
	# player's body, so using the camera's own position as the range origin
	# made two players standing 3m apart measure as ~7m apart (3m gap plus
	# the ~4m spring pulling the viewer's own camera backward), permanently
	# failing NAMEPLATE_RANGE_METERS. The camera's FACING direction is still
	# exactly what should decide aim, just not where the measurement starts.
	var origin: Vector3 = local_player.global_position + Vector3(0, 1.5, 0)
	var to_self := head_pos - origin
	if to_self.length() > NAMEPLATE_RANGE_METERS:
		return false
	var facing := -camera.global_transform.basis.z
	return facing.normalized().dot(to_self.normalized()) >= NAMEPLATE_LOOK_DOT_THRESHOLD

func _define_parts() -> void:
	var head_size := Vector3(0.7, 0.7, 0.7)
	# The pivot sits at the neck's own top end (a real joint position -- see
	# FusedBody.bone_origins()'s own "head" entry -- now that "head" is a
	# real Skeleton3D bone instead of a fixed-position mesh with no skeletal
	# connection at all). The head mesh itself is OFFSET upward from that
	# pivot by half its own height rather than centered on it, so it sits ON
	# TOP of the neck the way a real head does -- same "mesh offset from a
	# fixed pivot" convention every other body part already uses. Centering
	# it ON the pivot instead would make any future head-tilt/nod animation
	# read as the head orbiting in place rather than tilting from the neck.
	var head_y := FusedBody.NECK_Y
	_parts = {
		"head": {"size": head_size, "pivot": Vector3(0, head_y, 0), "offset": Vector3(0, head_size.y / 2.0, 0)},
	}

func _build_atlas() -> void:
	atlas_image = Image.create(atlas_size, atlas_height, false, Image.FORMAT_RGBA8)
	atlas_image.fill(Color.WHITE)
	atlas_texture = ImageTexture.create_from_image(atlas_image)

	_shared_material = StandardMaterial3D.new()
	_shared_material.albedo_texture = atlas_texture
	_shared_material.roughness = 1.0
	_shared_material.metallic = 0.0
	# Flat, stylized shading, not PBR -- roughness alone doesn't remove
	# StandardMaterial3D's own default dielectric specular response
	# (metallic_specular=0.5 unless zeroed), which reads as a visible sheen
	# under a bright environment. Invisible against the city's old flat-
	# color sky; became a real artifact the moment a real sky/sun was added
	# (see city.tscn) -- fixed there for road/sidewalk/facade materials,
	# same fix needed here so the character doesn't develop the same sheen
	# under direct sun from some angle later.
	_shared_material.metallic_specular = 0.0
	# LINEAR, not NEAREST -- the atlas itself is fairly low-res (ATLAS_CELL is
	# only 64px per row) and gets magnified a lot on a close-up character, so
	# NEAREST showed every texel as a hard-edged block, reading as a jagged
	# staircase instead of a smooth stroke even though _stamp() already
	# anti-aliases the brush's own edges WITHIN the atlas (confirmed
	# directly: a diagonal test stroke rendered as visibly blocky steps under
	# NEAREST, smooth under LINEAR). Safe against bleeding across atlas row
	# boundaries (a real risk with LINEAR, which blends across texel edges)
	# because ROW_V_MARGIN already insets every row by more than a single
	# texel specifically to leave room for this -- see its own comment.
	#
	# Deliberately no mipmaps (plain LINEAR, not LINEAR_WITH_MIPMAPS): tried
	# mipmapping earlier as a guess at fixing brush marks looking elongated
	# near the head's pole and the near-zero-length neck, and it turned out
	# to be the actual CAUSE of that, not a fix for it -- confirmed directly
	# by isolating a single brush-1 tap at the exact triangle producing the
	# bug and swapping only the texture filter mode: NEAREST and plain
	# LINEAR both rendered it as a small round dot, while LINEAR_WITH_
	# MIPMAPS (and even LINEAR_WITH_MIPMAPS_ANISOTROPIC, tried as a milder
	# alternative) rendered the SAME atlas data as an obviously elongated
	# vertical streak. Near a UV pole, the screen-space derivative of U
	# (circumference) is extreme even when V (pole-axis) is fine, which
	# pushes automatic mip selection to a coarser level than V actually
	# needs -- blurring the mark away in U while leaving V comparatively
	# sharp, which reads as "stretched" regardless of how carefully the
	# stamp's own pixel radii are computed. The atlas is a fixed, fairly
	# small 64px-per-row texture that's always viewed close-up in the
	# character creator, so mipmapping's own usual benefit (avoiding
	# minification shimmer at a distance) isn't worth this trade here.
	_shared_material.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR

## Builds the fused torso+arms+legs mesh ONCE (mesh generation costs
## ~100-500ms -- fine as a one-time cost at character creation, nowhere
## near fast enough to redo per-frame). Sets up the Skeleton3D, binds the
## mesh to it, and records rest-pose triangle data for CPU paint
## raycasting. Never called again afterward (there's currently no feature
## that changes body proportions at runtime -- only the head shape can
## change, handled separately by _build_head).
func _build_fused_body() -> void:
	var atlas_row_for_bone := {}
	for bone_name in FusedBody.PAINT_BONES:
		atlas_row_for_bone[bone_name] = PART_NAMES.find(bone_name)

	var built: Dictionary = FusedBody.build(atlas_row_for_bone, PART_COUNT, ATLAS_CELL, atlas_size)

	_body_tri_positions = built["paint_positions"]
	_body_tri_uvs = built["paint_uvs"]
	_body_tri_clamp = built["paint_clamp"]
	_rebuild_paint_arrays()

	_body_skeleton = FusedBody.build_skeleton()
	_body_skeleton.name = BODY_SKELETON_NAME
	add_child(_body_skeleton)

	var origins := FusedBody.bone_origins()
	# "hips" is the root bone (no parent, see the loop below), so its own
	# local pose position IS its world-space rest origin, unadjusted.
	_hips_base_position = origins["hips"]
	for i in range(FusedBody.BONES.size()):
		var bone_name: String = FusedBody.BONES[i]
		# A child bone's pose position is relative to its PARENT's pose (the
		# Skeleton composes them automatically) -- the elbow's absolute
		# origin needs the shoulder's own origin subtracted out, or the
		# forearm would double-count the shoulder's offset and end up way
		# out past the actual elbow position.
		var parent_name: String = FusedBody.BONE_PARENT.get(bone_name, "")
		var local_origin: Vector3 = origins[bone_name]
		if parent_name != "":
			local_origin -= origins[parent_name]
		_body_skeleton.set_bone_pose_position(i, local_origin)

	var mesh_inst := MeshInstance3D.new()
	mesh_inst.mesh = built["mesh"]
	mesh_inst.skin = built["skin"]
	_body_mesh_instance = mesh_inst
	_full_body_mesh = built["mesh"]
	_full_body_skin = built["skin"]
	mesh_inst.material_override = _shared_material
	mesh_inst.layers = 1 | PORTRAIT_VISUAL_LAYER
	_body_skeleton.add_child(mesh_inst)
	mesh_inst.skeleton = mesh_inst.get_path_to(_body_skeleton)

	# A BoneAttachment3D bound to "hand_r", same reasoning as _head_pivot
	# (see _build_head's own comment) -- lets anything grabbed (see
	# get_hand_node(), first used by weapons/greatsword.gd's own equip())
	# follow the hand's live animated pose every frame for free, with no
	# per-frame code needed here. Built once here (unlike _head_pivot, this
	# never needs rebuilding -- the hand bone itself never changes shape).
	_hand_r_pivot = BoneAttachment3D.new()
	_hand_r_pivot.name = "HandRPivot"
	_body_skeleton.add_child(_hand_r_pivot)
	_hand_r_pivot.bone_name = "hand_r"

## Builds (or, if called again after a head shape change, rebuilds) just
## the head's mesh, pivot node, and paint bookkeeping. Safe to call more
## than once: the old pivot node is freed first. free() (not queue_free())
## removes it immediately -- queue_free() defers removal to the end of the
## frame, so an immediately-added same-named replacement would still find
## the old one present and get auto-renamed (e.g. "HeadPivot2"), silently
## breaking anything resolving it by name.
func _build_head() -> void:
	if is_instance_valid(_head_pivot):
		_head_pivot.free()

	var part: Dictionary = _parts["head"]
	var row: int = PART_NAMES.find("head")
	var verts: Array = _generate_head_verts(part["size"], row)

	_head_tri_positions = PackedVector3Array()
	_head_tri_uvs = PackedVector2Array()
	_head_tri_clamp = []
	var rest_center: Vector3 = part["pivot"] + part["offset"]
	for i in range(verts.size()):
		var v: Dictionary = verts[i]
		_head_tri_positions.append(rest_center + v["pos"])
		_head_tri_uvs.append(v["uv"])
		# One clamp entry per TRIANGLE (paint_at indexes this by
		# face_index, a triangle index), not per vertex -- verts has 3
		# entries per triangle all sharing the same clamp rect, so only
		# take it every 3rd vertex.
		if i % 3 == 0:
			_head_tri_clamp.append(v["clamp"])
	_rebuild_paint_arrays()

	# A BoneAttachment3D bound to the skeleton's own "head" bone (a real
	# child of "neck" now, see FusedBody.BONE_PARENT) instead of a plain
	# Node3D at a fixed position under Character directly -- this is what
	# lets a future animation actually move the head (nod, react to a hit,
	# look around) for free: the attachment follows the bone's live pose
	# every frame with no extra per-frame code needed here at all. Must be a
	# CHILD OF THE SKELETON (a BoneAttachment3D requirement), not of
	# Character directly like the old Node3D was -- callers that need this
	# node should go through get_head_node() rather than looking it up by
	# name/path, since its location in the tree changed.
	_head_pivot = BoneAttachment3D.new()
	_head_pivot.name = HEAD_PIVOT_NAME
	_body_skeleton.add_child(_head_pivot)
	_head_pivot.bone_name = "head"

	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	for v in verts:
		st.set_normal(v["normal"])
		st.set_uv(v["uv"])
		st.add_vertex(part["offset"] + v["pos"])

	var mesh_inst := MeshInstance3D.new()
	mesh_inst.mesh = st.commit()
	mesh_inst.material_override = _shared_material
	mesh_inst.layers = 1 | PORTRAIT_VISUAL_LAYER
	_head_pivot.add_child(mesh_inst)

	# _head_pivot was just freed and rebuilt from scratch above -- any hair
	# previously parented under the OLD pivot died with it, so hair needs
	# rebuilding here too, every time the head does (not just when hair
	# itself changes). See set_hair_style()'s own comment for why hair
	# can't just be a wearables/wearable.gd-style pickup: that pattern
	# assumes its parent (get_head_node()) stays alive across a head-shape
	# change, which isn't true here.
	_build_hair()

## Rebuilds (or removes) the hair mesh under _head_pivot, matching the
## currently-set _hair_style. Safe to call whenever _head_pivot itself was
## just rebuilt (see _build_head()'s own call site) as well as from
## set_hair_style() directly. free() (not queue_free()), same reasoning
## _build_head()'s own class comment gives for _head_pivot.
func _build_hair() -> void:
	if is_instance_valid(_hair_mesh_instance):
		_hair_mesh_instance.free()
		_hair_mesh_instance = null
	if _hair_style == "none" or not HAIR_SCENES.has(_hair_style) or not is_instance_valid(_head_pivot):
		return

	var imported: Node = HAIR_SCENES[_hair_style].instantiate()
	var source_mesh: MeshInstance3D = _find_mesh_instance(imported)
	if source_mesh == null:
		imported.free()
		return

	if _hair_material == null:
		_hair_material = StandardMaterial3D.new()
		_hair_material.albedo_color = HAIR_COLOR
		_hair_material.roughness = 0.6

	_hair_mesh_instance = MeshInstance3D.new()
	_hair_mesh_instance.mesh = source_mesh.mesh
	_hair_mesh_instance.material_override = _hair_material
	_hair_mesh_instance.layers = 1 | PORTRAIT_VISUAL_LAYER
	# See HAIR_SCENES' own comment on the mesh's baked-in orientation --
	# flips the spikes to point up and out of the scalp instead of down off
	# the back of the head.
	_hair_mesh_instance.rotation.x = PI
	# The head box's own top surface, not its center -- offset.y is already
	# size.y/2.0 (see _define_parts()), so offset.y + size.y/2.0 == size.y
	# exactly. Computed from _parts rather than a hardcoded constant so this
	# stays correct if the head box's own size ever changes, same reasoning
	# get_torso_center_y() already applies to a different part of this file.
	var head_top_y: float = _parts["head"]["size"].y
	_hair_mesh_instance.position = Vector3(0, head_top_y, 0)
	_head_pivot.add_child(_hair_mesh_instance)
	# Only the mesh resource itself is reused above -- the rest of the
	# imported scene (its own root node, any wrapper transforms glTF import
	# added) isn't needed once that reference is taken.
	imported.free()

## Depth-first search for the first MeshInstance3D in an imported glTF
## scene's own node hierarchy -- Godot's glTF importer can wrap a single
## mesh in intermediate nodes depending on how the source file was
## exported, so this doesn't assume any fixed depth/path.
static func _find_mesh_instance(node: Node) -> MeshInstance3D:
	if node is MeshInstance3D:
		return node
	for child in node.get_children():
		var found := _find_mesh_instance(child)
		if found != null:
			return found
	return null

## Concatenates the body's (fixed) and head's (swappable) rest-pose
## triangle data into the flat arrays raycast_paint/paint_at actually use.
## face_index (a triangle index) stays valid across a head rebuild as long
## as the body's triangles always come first, in the same order.
func _rebuild_paint_arrays() -> void:
	tri_positions = _body_tri_positions.duplicate()
	tri_positions.append_array(_head_tri_positions)
	tri_uvs = _body_tri_uvs.duplicate()
	tri_uvs.append_array(_head_tri_uvs)
	tri_clamp = _body_tri_clamp.duplicate()
	tri_clamp.append_array(_head_tri_clamp)

## Returns 36 {pos, uv, normal, clamp} vertex entries (12 triangles)
## describing one box's 6 flat faces, positions relative to the box's own
## center. Only used for the cube head option now -- torso/arms/legs are one
## fused, smoothly-blended mesh with no flat faces at all (see
## fused_body.gd), per the reference drawing this was built from.
func _generate_box_verts(size: Vector3, atlas_row: int) -> Array:
	var hx := size.x / 2.0
	var hy := size.y / 2.0
	var hz := size.z / 2.0

	var faces := [
		{"normal": Vector3(1, 0, 0), "corners": [Vector3(hx, -hy, -hz), Vector3(hx, -hy, hz), Vector3(hx, hy, hz), Vector3(hx, hy, -hz)]},
		{"normal": Vector3(-1, 0, 0), "corners": [Vector3(-hx, -hy, -hz), Vector3(-hx, -hy, hz), Vector3(-hx, hy, hz), Vector3(-hx, hy, -hz)]},
		{"normal": Vector3(0, 1, 0), "corners": [Vector3(-hx, hy, -hz), Vector3(hx, hy, -hz), Vector3(hx, hy, hz), Vector3(-hx, hy, hz)]},
		{"normal": Vector3(0, -1, 0), "corners": [Vector3(-hx, -hy, -hz), Vector3(hx, -hy, -hz), Vector3(hx, -hy, hz), Vector3(-hx, -hy, hz)]},
		{"normal": Vector3(0, 0, 1), "corners": [Vector3(-hx, -hy, hz), Vector3(hx, -hy, hz), Vector3(hx, hy, hz), Vector3(-hx, hy, hz)]},
		{"normal": Vector3(0, 0, -1), "corners": [Vector3(-hx, -hy, -hz), Vector3(hx, -hy, -hz), Vector3(hx, hy, -hz), Vector3(-hx, hy, -hz)]},
	]

	var row_y0 := atlas_row * ATLAS_CELL
	var row_y1 := row_y0 + ATLAS_CELL

	var out := []
	for col in range(faces.size()):
		var face: Dictionary = faces[col]
		var normal: Vector3 = face["normal"]
		var corners: Array = face["corners"]

		var u0 := float(col) / float(FACE_COUNT)
		var u1 := float(col + 1) / float(FACE_COUNT)
		var v0 := float(atlas_row) / float(PART_COUNT) + ROW_V_MARGIN
		var v1 := float(atlas_row + 1) / float(PART_COUNT) - ROW_V_MARGIN
		var uvs := [Vector2(u0, v1), Vector2(u1, v1), Vector2(u1, v0), Vector2(u0, v0)]
		var clamp := {"x0": col * ATLAS_CELL, "x1": (col + 1) * ATLAS_CELL, "y0": row_y0, "y1": row_y1}

		var quad := []
		for i in range(4):
			quad.append({"pos": corners[i], "uv": uvs[i]})

		var geometric_normal: Vector3 = (quad[1]["pos"] - quad[0]["pos"]).cross(quad[2]["pos"] - quad[0]["pos"]).normalized()
		if geometric_normal.dot(normal) > 0.0:
			quad.reverse()

		for tri in [[0, 1, 2], [0, 2, 3]]:
			for idx in tri:
				out.append({"pos": quad[idx]["pos"], "uv": quad[idx]["uv"], "normal": normal, "clamp": clamp})

	return out

## Dispatches to whichever mesh generator matches the currently selected
## head shape. All three share the same {pos, uv, normal, clamp} vertex
## format as _generate_box_verts, so paint_at/raycast_paint don't need to
## know or care which shape produced a given triangle.
func _generate_head_verts(size: Vector3, atlas_row: int) -> Array:
	match _head_shape:
		"sphere":
			return _generate_sphere_verts(size.y / 2.0, atlas_row)
		"pyramid":
			return _generate_pyramid_verts(size, atlas_row)
		_:
			return _generate_box_verts(size, atlas_row)

## A sphere has no discrete faces, so it gets the WHOLE row's width for a
## single continuous equirectangular UV wrap instead of being split into
## per-face columns like a box.
func _generate_sphere_verts(radius: float, atlas_row: int) -> Array:
	const LAT_SEGMENTS := 8
	const LON_SEGMENTS := 12

	var v0 := float(atlas_row) / float(PART_COUNT) + ROW_V_MARGIN
	var v1 := float(atlas_row + 1) / float(PART_COUNT) - ROW_V_MARGIN
	var clamp := {"x0": 0, "x1": atlas_size, "y0": atlas_row * ATLAS_CELL, "y1": (atlas_row + 1) * ATLAS_CELL}

	var out := []
	for lat_i in range(LAT_SEGMENTS):
		for lon_i in range(LON_SEGMENTS):
			var p00 := _sphere_point(lat_i, lon_i, LAT_SEGMENTS, LON_SEGMENTS, radius)
			var p01 := _sphere_point(lat_i, lon_i + 1, LAT_SEGMENTS, LON_SEGMENTS, radius)
			var p10 := _sphere_point(lat_i + 1, lon_i, LAT_SEGMENTS, LON_SEGMENTS, radius)
			var p11 := _sphere_point(lat_i + 1, lon_i + 1, LAT_SEGMENTS, LON_SEGMENTS, radius)

			var quad := []
			for c in [p00, p01, p11, p10]:
				var uv := Vector2(c["u"], v0 + c["v_local"] * (v1 - v0))
				quad.append({"pos": c["pos"], "normal": c["normal"], "uv": uv})

			# Decide winding separately for EACH of the two triangles that
			# make up this quad, rather than once for the whole quad using
			# vertices 0,1,2. At the poles, lon_i and lon_i+1 collapse to
			# the same point, making triangle (0,1,2) degenerate (zero
			# area) there -- checking winding via THAT triangle gives a
			# zero cross product, so the check got silently skipped, and
			# the OTHER (real, non-degenerate) triangle covering that pole
			# wedge could end up with the wrong winding -- i.e. facing
			# inward and invisible, a hole right at the top of the sphere.
			for tri in [[0, 1, 2], [0, 2, 3]]:
				var tv := [quad[tri[0]], quad[tri[1]], quad[tri[2]]]
				var geo_normal: Vector3 = (tv[1]["pos"] - tv[0]["pos"]).cross(tv[2]["pos"] - tv[0]["pos"])
				if geo_normal.length_squared() > 0.0000001:
					var desired_normal: Vector3 = ((tv[0]["normal"] + tv[1]["normal"] + tv[2]["normal"]) / 3.0).normalized()
					geo_normal = geo_normal.normalized()
					if geo_normal.dot(desired_normal) > 0.0:
						tv.reverse()
				for v in tv:
					out.append({"pos": v["pos"], "uv": v["uv"], "normal": v["normal"], "clamp": clamp})

	return out

func _sphere_point(lat_i: int, lon_i: int, lat_segments: int, lon_segments: int, radius: float) -> Dictionary:
	var theta := float(lat_i) / float(lat_segments) * PI
	var phi := float(lon_i) / float(lon_segments) * TAU
	var sin_t := sin(theta)
	var pos := Vector3(radius * sin_t * cos(phi), radius * cos(theta), radius * sin_t * sin(phi))
	var normal := pos.normalized() if pos.length_squared() > 0.0001 else Vector3.UP
	return {"pos": pos, "normal": normal, "u": float(lon_i) / float(lon_segments), "v_local": float(lat_i) / float(lat_segments)}

## Square-based pyramid: 4 triangular side faces, each given its own
## quarter of the row's width, plus the base (bottom) face -- skipping the
## base seemed safe since it faces straight down into the neck, but it's
## visible as a hole letting the background show through whenever the
## character is viewed from below or at a steep enough angle while rotating.
func _generate_pyramid_verts(size: Vector3, atlas_row: int) -> Array:
	var hx := size.x / 2.0
	var hy := size.y / 2.0
	var hz := size.z / 2.0
	var apex := Vector3(0, hy, 0)
	var base := [Vector3(-hx, -hy, -hz), Vector3(hx, -hy, -hz), Vector3(hx, -hy, hz), Vector3(-hx, -hy, hz)]

	var v0 := float(atlas_row) / float(PART_COUNT) + ROW_V_MARGIN
	var v1 := float(atlas_row + 1) / float(PART_COUNT) - ROW_V_MARGIN
	var cell_w := atlas_size / 4.0
	var row_y0 := atlas_row * ATLAS_CELL
	var row_y1 := (atlas_row + 1) * ATLAS_CELL

	var out := []
	for i in range(4):
		var c0: Vector3 = base[i]
		var c1: Vector3 = base[(i + 1) % 4]
		var u0 := float(i) / 4.0
		var u1 := float(i + 1) / 4.0
		var u_mid := (u0 + u1) / 2.0

		var positions := [c0, c1, apex]
		var uvs := [Vector2(u0, v0), Vector2(u1, v0), Vector2(u_mid, v1)]

		# Direction from the shape's local origin to the face centroid is a
		# reasonable stand-in for "outward" on a convex shape like this,
		# used only to decide whether this naive winding needs flipping.
		var center: Vector3 = (c0 + c1 + apex) / 3.0
		var desired_normal := center.normalized()
		var geo_normal: Vector3 = (positions[1] - positions[0]).cross(positions[2] - positions[0])
		if geo_normal.length_squared() > 0.0000001:
			geo_normal = geo_normal.normalized()
			if geo_normal.dot(desired_normal) > 0.0:
				positions.reverse()
				uvs.reverse()

		var clamp := {"x0": int(i * cell_w), "x1": int((i + 1) * cell_w), "y0": row_y0, "y1": row_y1}
		for k in range(3):
			out.append({"pos": positions[k], "uv": uvs[k], "normal": desired_normal, "clamp": clamp})

	# The base is never intentionally painted (it faces straight down into
	# the neck), so exact UV placement doesn't matter -- just map the whole
	# quad to a single point near the row's corner so it reads as a plain,
	# unpainted patch instead of stretching whatever's at column 0.
	var base_normal := Vector3(0, -1, 0)
	var base_uv := Vector2(0.01, v0 + 0.01 * (v1 - v0))
	var base_clamp := {"x0": 0, "x1": int(cell_w), "y0": row_y0, "y1": row_y1}
	var base_quad := [base[0], base[1], base[2], base[3]]
	var base_geo_normal: Vector3 = (base_quad[1] - base_quad[0]).cross(base_quad[2] - base_quad[0])
	if base_geo_normal.length_squared() > 0.0000001:
		base_geo_normal = base_geo_normal.normalized()
		if base_geo_normal.dot(base_normal) > 0.0:
			base_quad.reverse()
	for tri in [[0, 1, 2], [0, 2, 3]]:
		for idx in tri:
			out.append({"pos": base_quad[idx], "uv": base_uv, "normal": base_normal, "clamp": base_clamp})

	return out

func _build_walk_animation() -> void:
	var lib := AnimationLibrary.new()
	lib.add_animation("walk", WalkAnim)
	lib.add_animation("run", RunAnim)
	lib.add_animation("idle", StandIdleAnim)
	lib.add_animation("combat_idle", BoxingIdleAnim)
	lib.add_animation("punch_right", BoxingRightJabAnim)
	lib.add_animation("punch_left", BoxingLeftJabAnim)
	lib.add_animation("jump", JumpAnim)
	lib.add_animation("jump_sprint", JumpSprintAnim)
	lib.add_animation("standing_to_crouch", StandingToCrouchAnim)
	lib.add_animation("crouch_to_idle", CrouchToIdleAnim)
	lib.add_animation("crouch_walk", CrouchWalkAnim)
	lib.add_animation("punch_heavy", HeavyPunchAnim)
	lib.add_animation("dash_backward", DashBackwardAnim)
	lib.add_animation("block", BlockAnim)
	lib.add_animation("walk_backward", WalkBackwardAnim)
	lib.add_animation("walk_left", WalkLeftAnim)
	lib.add_animation("walk_right", WalkRightAnim)
	lib.add_animation("sword_idle", SwordIdleAnim)
	lib.add_animation("sword_walk", SwordWalkAnim)
	lib.add_animation("sword_run", SwordRunAnim)
	lib.add_animation("sword_walk_left", SwordWalkLeftAnim)
	lib.add_animation("sword_walk_right", SwordWalkRightAnim)
	lib.add_animation("sword_walk_backward", SwordWalkBackwardAnim)
	lib.add_animation("sword_standing_to_crouch", SwordStandingToCrouchAnim)
	lib.add_animation("sword_crouch_to_idle", SwordCrouchToIdleAnim)
	lib.add_animation("sword_jump", SwordJumpAnim)
	lib.add_animation("sword_attack", SwordAttackAnim)
	lib.add_animation("sword_attack2", SwordAttack2Anim)
	lib.add_animation("sword_heavy_attack", SwordHeavyAttackAnim)
	lib.add_animation("sword_block", SwordBlockAnim)
	lib.add_animation("sword_block_impact", SwordBlockImpactAnim)
	lib.add_animation("sword_impact", SwordImpactAnim)
	lib.add_animation("gun_walk", GunWalkAnim)
	lib.add_animation("gun_run", GunRunAnim)
	lib.add_animation("gun_walk_left", GunWalkLeftAnim)
	lib.add_animation("gun_walk_right", GunWalkRightAnim)
	lib.add_animation("gun_walk_backward", GunWalkBackwardAnim)
	lib.add_animation("gun_jump", GunJumpAnim)
	lib.add_animation("gun_jump_moving", GunJumpMovingAnim)
	lib.add_animation("gun_shoot", GunShootAnim)
	lib.add_animation("gun_aim_in", GunAimInAnim)
	lib.add_animation("throw_bottle", ThrowBottleAnim)
	lib.add_animation("dance", DanceAnim)
	lib.add_animation("breakdance", BreakdanceAnim)
	lib.add_animation("entering_car", EnteringCarAnim)
	lib.add_animation("exiting_car", ExitingCarAnim)
	lib.add_animation("climb_up", ClimbUpAnim)
	lib.add_animation("climb_down", ClimbDownAnim)
	lib.add_animation("climb_top_out", ClimbTopOutAnim)
	lib.add_animation("falling", FallingAnim)
	lib.add_animation("falling_flat_impact", FallingFlatImpactAnim)
	lib.add_animation("getting_up", GettingUpAnim)
	lib.add_animation("looking_around", LookingAroundAnim)
	anim_player.add_animation_library("", lib)

	# See leg_anim_player's own @onready var comment -- these are the SAME
	# walk/run/directional clips, just stripped down to their leg tracks
	# only, so this second player can drive legs independently of whatever
	# full-body one-shot anim_player itself is playing.
	var leg_lib := AnimationLibrary.new()
	leg_lib.add_animation("leg_walk", _build_leg_only_animation(WalkAnim))
	leg_lib.add_animation("leg_run", _build_leg_only_animation(RunAnim))
	leg_lib.add_animation("leg_walk_backward", _build_leg_only_animation(WalkBackwardAnim))
	leg_lib.add_animation("leg_walk_left", _build_leg_only_animation(WalkLeftAnim))
	leg_lib.add_animation("leg_walk_right", _build_leg_only_animation(WalkRightAnim))
	leg_lib.add_animation("leg_sword_walk", _build_leg_only_animation(SwordWalkAnim))
	leg_lib.add_animation("leg_sword_run", _build_leg_only_animation(SwordRunAnim))
	leg_lib.add_animation("leg_sword_walk_backward", _build_leg_only_animation(SwordWalkBackwardAnim))
	leg_lib.add_animation("leg_sword_walk_left", _build_leg_only_animation(SwordWalkLeftAnim))
	leg_lib.add_animation("leg_sword_walk_right", _build_leg_only_animation(SwordWalkRightAnim))
	# Borrowed from the FIST system's own CrouchWalkAnim, not a sword-pack
	# clip -- see set_walking's own crouching+_wielding_sword leg-overlay
	# branch for why: there's no dedicated sword crouch-walk source clip
	# (see SwordStandingToCrouchAnim's own comment), but CrouchWalkAnim is
	# already a genuinely crouched (not standing) gait with its own correct
	# grounding, unlike the standing walk/run clips this same leg-overlay
	# mechanism otherwise uses (which is exactly why crouching is normally
	# excluded from it at all).
	# keep_hips_position=true here ONLY -- see _build_leg_only_animation's
	# own comment on why this one specifically needs it: CrouchWalkAnim's
	# own leg rotations were grounded (see retarget()'s own
	# _grounded_hip_offset) assuming ITS OWN hips position track is also
	# being applied every frame, not the STATIC held hip height
	# sword_standing_to_crouch happens to leave things at. Confirmed the
	# hard way: leg-only (rotation-only) worked perfectly for every
	# standing walk/run overlay above, but produced ~30cm foot-through-
	# floor clipping for this one the first time it was tried, exactly
	# because the two clips' own hip heights don't match and only one of
	# them (the wrong one) was being kept.
	leg_lib.add_animation("leg_crouch_walk", _build_leg_only_animation(CrouchWalkAnim, true))
	# Same idea as the sword pack's own leg overlays above -- lets
	# GunShootAnim/GunAimInAnim (both full-body clips) own anim_player's
	# upper body/arms while the legs keep stepping through whichever
	# directional gun-walk/run clip actually matches the held movement key
	# (see set_walking's own _holding_gun leg-overlay branch).
	leg_lib.add_animation("leg_gun_walk", _build_leg_only_animation(GunWalkAnim))
	leg_lib.add_animation("leg_gun_run", _build_leg_only_animation(GunRunAnim))
	leg_lib.add_animation("leg_gun_walk_backward", _build_leg_only_animation(GunWalkBackwardAnim))
	leg_lib.add_animation("leg_gun_walk_left", _build_leg_only_animation(GunWalkLeftAnim))
	leg_lib.add_animation("leg_gun_walk_right", _build_leg_only_animation(GunWalkRightAnim))
	leg_anim_player.add_animation_library("", leg_lib)

## Duplicates source (walk/run) and strips every track except the 4 leg
## bones' (see LEG_OVERLAY_BONES) -- keeps the kept tracks' keys/
## interpolation completely untouched, just removes everything else, so
## leg_anim_player's output for those 4 bones is byte-identical to what
## anim_player itself would show if it were playing this same clip.
## keep_hips_position additionally keeps the hips POSITION track (never
## true for the plain standing walk/run overlays, which have no such track
## to begin with -- see leg_crouch_walk's own comment for the one case that
## actually needs it) -- identified by path suffix rather than going
## through _track_bone_name, since that helper only ever recognizes
## ".../rotation" tracks by design (see its own comment) and would return ""
## for a position track regardless.
func _build_leg_only_animation(source: Animation, keep_hips_position: bool = false) -> Animation:
	var result: Animation = source.duplicate()
	for i in range(result.get_track_count() - 1, -1, -1):
		if keep_hips_position and String(result.track_get_path(i)).ends_with("/position"):
			continue
		if not LEG_OVERLAY_BONES.has(_track_bone_name(result.track_get_path(i))):
			result.remove_track(i)
	return result

## Every bone track in this file is a Value track path shaped
## "BodySkeleton:bones/<idx>/rotation" (see tests/scratch_mixamo_retarget.gd,
## which originally wrote these) -- pulls the bone index back out of that
## path and resolves it to a name via the real skeleton, rather than
## string-matching bone names directly (skeleton bone order/indices are the
## only thing actually guaranteed to agree with the animation's own baked
## paths). Returns "" for anything that isn't shaped like a bone rotation
## track at all (there aren't any today, but this stays honest either way).
func _track_bone_name(track_path: NodePath) -> String:
	var subnames: String = String(track_path).get_slice(":", 1)
	var parts := subnames.split("/")
	if parts.size() != 3 or parts[0] != "bones" or parts[2] != "rotation":
		return ""
	return _body_skeleton.get_bone_name(int(parts[1]))

## Bone name for each of the 4 horizontal movement directions set_walking's
## own `direction` argument accepts, to the full-body clip AND the leg-only
## overlay clip that shows it -- "forward" (or any other value) falls back
## to the ordinary walk/run pair, the only two of these five with a real
## running variant (see WalkBackwardAnim's own comment on why: no dedicated
## running clip exists for backward/strafing, so those three always play at
## walk pace regardless of running).
const DIRECTIONAL_ANIM := {
	"backward": {"anim": "walk_backward", "leg_anim": "leg_walk_backward"},
	"left": {"anim": "walk_left", "leg_anim": "leg_walk_left"},
	"right": {"anim": "walk_right", "leg_anim": "leg_walk_right"},
}
## Same idea as DIRECTIONAL_ANIM, sword-equipped equivalent -- see
## set_walking's own _wielding_sword branches for where this gets picked
## instead.
const SWORD_DIRECTIONAL_ANIM := {
	"backward": {"anim": "sword_walk_backward", "leg_anim": "leg_sword_walk_backward"},
	"left": {"anim": "sword_walk_left", "leg_anim": "leg_sword_walk_left"},
	"right": {"anim": "sword_walk_right", "leg_anim": "leg_sword_walk_right"},
}
## Same idea as DIRECTIONAL_ANIM, gun-equipped equivalent -- see
## set_walking's own _holding_gun branches for where this gets picked
## instead.
const GUN_DIRECTIONAL_ANIM := {
	"backward": {"anim": "gun_walk_backward", "leg_anim": "leg_gun_walk_backward"},
	"left": {"anim": "gun_walk_left", "leg_anim": "leg_gun_walk_left"},
	"right": {"anim": "gun_walk_right", "leg_anim": "leg_gun_walk_right"},
}

## Starts/stops the walk or run cycle (running only takes effect while
## enabled is also true -- sprinting in place while not moving isn't a
## thing). Has no effect until the character leaves rest pose is safe:
## painting is only expected while at rest, and callers are expected to
## only enable walking once painting is done (see Character.set_paintable /
## main.gd's transition into the room).
## crouching is read fresh every frame from player.gd's own Input state, NOT
## stored here -- unlike _is_jumping/_is_crouch_transitioning (genuine state
## this node owns), "am I holding the crouch key" is the caller's input to
## report each frame, the same way "running" already is.
## direction ("forward"/"backward"/"left"/"right") picks which of the 4
## horizontal directional clips shows -- only meaningful now that player.gd
## locks the body's own facing to the camera every frame instead of turning
## to face wherever you move (see its own class comment); WASD is always
## relative to that fixed facing, so "backward" means "camera looking one
## way, character visibly walking the other," not just "moving away from
## spawn."
func set_walking(enabled: bool, running: bool = false, crouching: bool = false, direction: String = "forward") -> void:
	# Movement always cancels a dance in progress -- unlike every OTHER
	# one-shot flag in this file, nothing else ever clears _is_dancing on
	# its own (a dance has no natural "finished" signal, see its own
	# comment), so set_walking() -- the only place that ever actually knows
	# "the player just started moving" -- has to be the one to end it,
	# right here, before the early-return below would otherwise keep
	# protecting it forever.
	if enabled and _is_dancing:
		_is_dancing = false

	# Run BEFORE the early-return below, not after -- a punch/block is
	# exactly the case that early-return exists for (see its own comment),
	# but that's also exactly when the legs need this overlay the most: see
	# leg_anim_player's own @onready var comment for why anim_player's own
	# legs are frozen on a static boxing-stance pose for as long as one of
	# those clips owns it. Only engages during an actual punch/block, not
	# unconditionally on every walk/run frame -- when neither is active,
	# anim_player's own walk/run/idle already drives the legs correctly on
	# its own, and leaving this off then avoids two players redundantly
	# fighting over identical output. Excluded during crouching entirely:
	# every leg_* overlay clip is extracted from a STANDING clip, so
	# overlaying one during a crouch (a fundamentally different, bent-knee
	# leg pose) would read as the legs snapping up to standing height while
	# the rest of the body stayed low, a worse glitch than the frozen legs
	# this exists to fix.
	if enabled and not crouching and (_is_punching or _is_blocking or _is_shooting or _is_aiming_throw):
		var directional_table: Dictionary
		# A forward sword/gun sprint leg-overlay clip exists for both packs
		# now (leg_sword_run/leg_gun_run), same as the fist system's own
		# leg_run -- none of the three movesets has a directional (strafe/
		# backward) running variant though, see WalkBackwardAnim's own
		# comment on why those 3 fall back to walk pace regardless of
		# running.
		var fallback_leg: String
		if _wielding_sword:
			directional_table = SWORD_DIRECTIONAL_ANIM
			fallback_leg = "leg_sword_run" if running else "leg_sword_walk"
		elif _holding_gun:
			directional_table = GUN_DIRECTIONAL_ANIM
			fallback_leg = "leg_gun_run" if running else "leg_gun_walk"
		else:
			directional_table = DIRECTIONAL_ANIM
			fallback_leg = "leg_run" if running else "leg_walk"
		var leg_anim_name: String = directional_table[direction]["leg_anim"] if directional_table.has(direction) else fallback_leg
		leg_anim_player.play(leg_anim_name, BLEND_SECONDS)
	elif enabled and crouching and _wielding_sword:
		# No dedicated sword crouch-walk clip exists (see
		# SwordStandingToCrouchAnim's own comment) -- without this, moving
		# while crouched+sworded left the legs completely frozen on
		# whatever static pose the crouching short-circuit below holds
		# (correctly grounded for STANDING STILL, but with zero stepping
		# motion while the whole body kept sliding forward via normal
		# movement) -- confirmed as the real cause of a reported "legs clip
		# through the floor" while crouch-walking: not an actual Y-axis
		# grounding bug (measured directly, the static pose itself stays
		# within a couple centimeters of true ground level throughout), but
		# feet visibly gliding across the ground with no walk cycle at all,
		# which reads the same way to a player watching it. leg_crouch_walk
		# (see its own comment) gives the legs real, correctly-grounded
		# stepping motion while the upper body/arms/sword stay on the
		# static sworded crouch pose underneath.
		leg_anim_player.play("leg_crouch_walk", BLEND_SECONDS)
	else:
		leg_anim_player.stop()

	# A punch (see punch()), a jump (see jump()), or a block (see block())
	# owns the arm/animation player until its own one-shot clip finishes
	# (see _on_animation_finished) or, for a jump, until land() ends it
	# early -- without this, still holding a movement key would make THIS
	# get called again the very next frame and immediately stomp that
	# animation with walk/run, cutting it off after a single frame. A
	# dedicated dash clip (see dash()) owns it the same way -- a left/right
	# dash never sets _is_dashing at all, so this only ever holds for
	# forward/backward.
	if _is_punching or _is_jumping or _is_dashing or _is_blocking or _is_shooting or _is_dancing or _is_car_transitioning or _is_aiming_throw or _is_climbing:
		return

	# No dedicated crouch-WALKING clip exists for the sword pack (only the
	# standing_to_crouch/crouch_to_idle transitions, see SwordStandingToCrouchAnim's
	# own comment) -- rather than either faking a walk cycle that doesn't
	# exist or reusing the fist crouch_walk clip (which would show the wrong
	# weapon-less arm pose), this just holds the same static crouched pose
	# regardless of whether WASD is also held, short-circuiting BEFORE the
	# enabled/crouching branching below ever distinguishes the two. Once
	# already settled on this pose (current_animation already matches,
	# whether still mid-transition, fully finished, or paused on its last
	# frame), every subsequent frame does nothing further -- exactly the
	# "hold the last one-shot frame" behavior every other one-shot clip in
	# this file already relies on.
	if _wielding_sword and crouching:
		if enabled:
			# Moving: legs+hips-bob are driven by the leg-overlay branch
			# above (leg_crouch_walk, which carries its OWN correctly-
			# grounded hips-position track -- see its own comment) -- this
			# just needs the upper body/arms/sword showing the crouch pose
			# underneath it.
			if anim_player.current_animation != "sword_standing_to_crouch":
				anim_player.play("sword_standing_to_crouch", CROUCH_BLEND_SECONDS)
			return
		if _is_crouch_transitioning:
			# start_crouch() (called by player.gd on the crouch-key press
			# edge, BEFORE set_walking runs this same frame) already has
			# sword_standing_to_crouch playing naturally from frame 0 -- let
			# its OWN hips-position track drive hips through the actual
			# crouching-DOWN motion untouched here. Forcing the settled
			# target hip height (see below) while this is still legitimately
			# mid-transition was a real, confirmed bug: hips would get
			# yanked down to the fully-crouched height on the very FIRST
			# frame, while the legs were still barely bent (frame 0 of a
			# ~0.67s animation) -- an anatomically nonsensical combination
			# (very low hips, nearly-straight knees) that read as severe,
			# timing-dependent foot-through-floor clipping (confirmed
			# directly: reproduced up to ~40cm on some runs, depending on
			# exactly which frame the flaky sampling caught it on).
			return
		# Standing still, crouched, and NOT mid-transition (i.e. genuinely
		# settled, or arriving here from crouch-WALKING's own leg overlay
		# stopping): unconditionally (re-)assert BOTH the pose AND its own
		# correct hip height directly, every frame -- NOT a one-time "only
		# if not already showing this clip" guard the way every other held
		# pose in this file gets away with. Needed specifically because
		# crouch-walking's own leg overlay (leg_crouch_walk) drives hips
		# through its own continuously bobbing cycle while moving, and when
		# movement stops, hips is left at whatever arbitrary point in that
		# bob cycle it happened to be at -- NOT this pose's own settled
		# value. A one-time guard checked against either current_animation
		# (resets to "" once this LOOP_NONE clip finishes playing on its
		# own, so it would never even match again) or a "still at true
		# standing base" distance check (never true again either, since
		# crouching moves hips away from base) would both silently leave
		# hips stuck at that wrong bob-cycle value forever -- confirmed
		# directly, and it read exactly like ~8cm foot-through-floor
		# clipping once the player eventually stood back up from there,
		# inheriting the same wrong height. Forcing the skeleton's own hips
		# POSITION directly (not relying on animation blending to
		# eventually settle there) sidesteps needing to reason about blend
		# timing at all -- same direct-poke approach _reset_hips_position()
		# already uses for the standing case.
		if anim_player.current_animation != "sword_standing_to_crouch" or not anim_player.is_playing():
			anim_player.play("sword_standing_to_crouch", CROUCH_BLEND_SECONDS)
			anim_player.seek(SwordStandingToCrouchAnim.length, true)
		_body_skeleton.set_bone_pose_position(_body_skeleton.find_bone("hips"), Vector3(_hips_base_position.x, _sword_crouch_hip_y, _hips_base_position.z))
		return

	# Godot's own cross-fade blends FROM its internal record of the outgoing
	# animation's own last output, not from whatever _reset_hips_position()
	# just poked directly into the skeleton -- confirmed directly: blending
	# out of a crouch clip into a hips-position-reset walk/combat_idle/idle
	# still visibly dragged hips back down toward the crouch clip's own low
	# value for the blend's duration before settling, reproducing the exact
	# foot-through-floor clip _reset_hips_position was written to fix. Since
	# only the three crouch clips ever carry a hips-position track (see
	# _reset_hips_position's own comment), this is the one handoff where a
	# blend and a hips reset actively fight each other -- cut instantly here
	# instead, same as every transition in this file did before BLEND_SECONDS
	# existed.
	#
	# Detected by checking the hips bone's OWN current pose position against
	# its rest value, not by name-matching anim_player.current_animation --
	# confirmed directly that current_animation resets to "" the instant a
	# LOOP_NONE clip (every crouch clip) finishes playing on its own, rather
	# than staying set to that clip's name the way every current_animation
	# guard elsewhere in this file assumes. Checking the hips position itself
	# instead measures the ACTUAL condition that matters (is hips displaced
	# from rest right now) and keeps working whether the crouch clip is still
	# actively playing, was JUST paused (see the "settle to static crouch"
	# branch below), or already finished on its own.
	var leaving_crouch_anim := _body_skeleton.get_bone_pose_position(_body_skeleton.find_bone("hips")).distance_to(_hips_base_position) > 0.01
	if enabled:
		# Movement always overrides a crouch transition in progress, rather
		# than making the player wait out standing_to_crouch's own ~0.93s
		# runtime before WASD does anything -- clearing the flag here (not
		# just falling through to the "enabled" branch below) means an
		# interrupted transition doesn't leave _is_crouch_transitioning
		# stuck true forever (nothing else would ever clear it, since the
		# animation never gets to finish naturally once overridden).
		_is_crouch_transitioning = false
		var anim_name: String
		var blend := BLEND_SECONDS
		if crouching:
			# Crouch overrides running entirely -- there's no "crouch-sprint"
			# clip, and trying to sprint while crouched wouldn't make sense
			# anatomically either. Blended (see CROUCH_BLEND_SECONDS) since
			# crouch_walk's own held height doesn't match whatever crouch
			# pose came before it.
			anim_name = "crouch_walk"
			blend = CROUCH_BLEND_SECONDS
		else:
			# Backward/left/right (see DIRECTIONAL_ANIM) always play at walk
			# pace regardless of running -- no dedicated running clip exists
			# for those three, see WalkBackwardAnim's own comment. Anything
			# else (including plain "forward") falls back to the ordinary
			# walk/run pair. _wielding_sword redirects the whole lookup to
			# SWORD_DIRECTIONAL_ANIM/sword_walk/sword_run instead -- a
			# forward sword sprint clip exists (SwordRunAnim), same as the
			# fist system's own "run", so "forward" picks between
			# sword_walk/sword_run the same way plain walk/run already does.
			if _wielding_sword:
				anim_name = SWORD_DIRECTIONAL_ANIM[direction]["anim"] if SWORD_DIRECTIONAL_ANIM.has(direction) else ("sword_run" if running else "sword_walk")
			elif _holding_gun:
				anim_name = GUN_DIRECTIONAL_ANIM[direction]["anim"] if GUN_DIRECTIONAL_ANIM.has(direction) else ("gun_run" if running else "gun_walk")
			else:
				anim_name = DIRECTIONAL_ANIM[direction]["anim"] if DIRECTIONAL_ANIM.has(direction) else ("run" if running else "walk")
			# None of walk/run/walk_backward/walk_left/walk_right carry a
			# hips-position track of their own (see _hips_base_position's
			# own comment), so if the LAST thing playing was crouch-related,
			# hips could still be sitting at whatever that clip left it --
			# confirmed as a real bug via crouch_to_idle specifically (see
			# its own preload comment).
			_reset_hips_position()
			if leaving_crouch_anim:
				blend = 0.0
		if anim_player.current_animation != anim_name or not anim_player.is_playing():
			anim_player.play(anim_name, blend)
	elif _is_crouch_transitioning:
		# Standing still while standing_to_crouch/crouch_to_idle is still
		# playing -- let it finish undisturbed rather than snapping straight
		# to idle/crouch-hold mid-transition.
		pass
	elif crouching:
		# Standing still while crouched. Usually deliberately does nothing:
		# there's no dedicated "held crouch idle" clip, and
		# start_crouch()'s own standing_to_crouch animation already holds
		# on its last (crouched) frame once it finishes (same behavior
		# every one-shot clip in this file already relies on), so leaving
		# the animation player alone is exactly right there. The ONE real
		# exception is stopping while mid-crouch-walk: "crouch_walk" is
		# still the current_animation at that point, and since nothing
		# here ever calls play() again, Godot just keeps looping/advancing
		# it on its own forever -- confirmed directly as a real bug (the
		# character visibly kept cycling its walk gait while standing
		# completely still). Snapping to standing_to_crouch's own final
		# (fully crouched, correctly grounded) frame settles it into a
		# real static pose instead, the same instant-snap-not-eased-
		# transition convention this file already uses elsewhere (e.g.
		# reset_ragdoll).
		if anim_player.current_animation == "crouch_walk":
			anim_player.play("standing_to_crouch")
			anim_player.seek(StandingToCrouchAnim.length, true)
			anim_player.pause()
	elif _holding_gun and _is_aiming:
		# Standing still while aiming down sights always holds gun_aim_in,
		# regardless of _in_combat_mode -- same "only one stance exists for
		# this state" reasoning _wielding_sword's own sword_idle branch below
		# already uses. Moving away from standing still (the "enabled"
		# branch above) or releasing aim (see set_aiming()) both fall
		# through to a DIFFERENT branch next frame, so this only ever holds
		# while genuinely standing still and aiming.
		_reset_hips_position() # see _wielding_sword's own comment on why this needs to be every frame, not just once
		if anim_player.current_animation != "gun_aim_in" or not anim_player.is_playing():
			anim_player.play("gun_aim_in", 0.0 if leaving_crouch_anim else BLEND_SECONDS)
	elif _wielding_sword:
		# Standing still while wielding the sword always holds sword_idle,
		# regardless of _in_combat_mode -- the 2hsw pack only has the one
		# idle clip (no separate "combat stance" variant the way the fist
		# system's own combat_idle/BoxingIdleAnim is), so there's nothing to
		# distinguish here.
		#
		# _reset_hips_position() is called EVERY frame here, not just once
		# on transition (unlike every sibling branch in this chain) --
		# confirmed the hard way that a one-time call isn't reliable
		# straight out of sword_crouch_to_idle specifically: it correctly
		# set hips to standing height for exactly one frame, then Godot's
		# own internal animation-blend bookkeeping silently dragged it back
		# down to a stale mid-crouch value the very next frame, with
		# nothing in this file touching hips again to explain it (sword_idle
		# itself has no hips track at all -- see its own preload comment).
		# Re-asserting the direct poke every frame, the same defensive fix
		# already needed for the crouching+_wielding_sword branch above (see
		# its own comment for the fuller story), sidesteps needing to
		# understand that blend timing at all.
		_reset_hips_position()
		if anim_player.current_animation != "sword_idle" or not anim_player.is_playing():
			anim_player.play("sword_idle", 0.0 if leaving_crouch_anim else BLEND_SECONDS)
	elif _in_combat_mode:
		# Standing still in combat mode holds a looping boxing-idle animation
		# (retargeted Mixamo mocap, see BoxingIdleAnim) instead of dropping to
		# a plain standing rest -- movement still overrides it (the branch
		# above), same as normal idle already gets overridden by walk/run.
		# Same not-already-playing guard as walk/run above, needed for the
		# same reason: calling play() every single idle frame would restart
		# the clip from frame 0 every time, which never actually looks like
		# it's playing at all.
		if anim_player.current_animation != "combat_idle" or not anim_player.is_playing():
			_reset_hips_position() # see the walk/run branch's own comment
			anim_player.play("combat_idle", 0.0 if leaving_crouch_anim else BLEND_SECONDS)
	else:
		# Standing still, out of combat, holds a looping standing-idle
		# animation (retargeted Mixamo mocap, see StandIdleAnim) instead of
		# a plain static rest pose -- same not-already-playing guard as the
		# branches above.
		if anim_player.current_animation != "idle" or not anim_player.is_playing():
			_reset_hips_position() # see the walk/run branch's own comment
			anim_player.play("idle", 0.0 if leaving_crouch_anim else BLEND_SECONDS)

## Snaps the hips bone's pose POSITION back to its rest/standing value.
## Needed because only the three crouch clips (standing_to_crouch/
## crouch_to_idle/crouch_walk) carry a hips position track at all -- every
## other animation this file plays (walk/run/idle/combat_idle/punches/jump)
## only ever keys ROTATION tracks, so Godot leaves hips position exactly
## where the last clip that DID touch it left it. Confirmed as a real bug,
## not just a theoretical gap: crouch_to_idle's own baked last frame doesn't
## fully return hips to the standing rest height (its source mocap's own
## legs aren't perfectly straight at that exact instant, so the FK-grounding
## bake correctly left hips still ~0.135m low to keep that leg's own foot
## from floating) -- once "idle" then takes over with its own straight-leg
## rotations and NO hips track of its own to correct the mismatch, the
## combination of "hips still low" + "legs now straight" pushes the foot
## straight through the floor, and it stays that way indefinitely (idle
## loops forever without ever touching hips again). Called once, right when
## a non-crouch animation is ABOUT to start playing (see set_walking's own
## walk/run/combat_idle/idle branches) -- never while an actual crouch clip
## is active, which would stomp its own legitimately-lowered hips.
func _reset_hips_position() -> void:
	_body_skeleton.set_bone_pose_position(_body_skeleton.find_bone("hips"), _hips_base_position)

## Clears the hips bone's own ROTATION back to identity -- the same fix
## _on_animation_finished already applies for exiting_car (see its own
## comment there for the full explanation), exposed publicly because the
## THIRD clip in this game carrying a real hips rotation track, "falling"
## (see player/intro_cutscene.gd), is LOOPED and therefore never fires
## animation_finished at all, so it can't be handled the same way from in
## here. A real reported bug without this: "the character is stuck sideways
## even once the game loads" -- once the intro fall's own dive lean is
## written into the hips bone, every subsequent clip (impact, getting_up,
## looking_around, and all ordinary walk/run/idle) is rotation-only with NO
## hips track, so nothing ever moves that bone again and the whole body --
## hips being the root every other bone hangs off -- stays permanently
## skewed. Called by intro_cutscene.gd the instant the fall ends.
func reset_hips_rotation() -> void:
	_body_skeleton.set_bone_pose_rotation(_body_skeleton.find_bone("hips"), Quaternion.IDENTITY)

## Reads a baked animation's own position track (there's ever only one --
## the hips, see retarget()'s own retarget_hip_height) at its LAST keyframe,
## directly from the resource's own data -- not by playing/seeking/
## observing at runtime. See _sword_crouch_hip_y's own comment for why this
## is needed at all.
func _static_track_last_position_y(anim: Animation) -> float:
	for i in range(anim.get_track_count()):
		if String(anim.track_get_path(i)).ends_with("/position"):
			var key_count := anim.track_get_key_count(i)
			if key_count > 0:
				return (anim.track_get_key_value(i, key_count - 1) as Vector3).y
	return _hips_base_position.y

## Enters the boxing stance and (re)starts its own timeout clock (see
## COMBAT_MODE_TIMEOUT_SECONDS/_physics_process) -- called from punch() on
## EVERY swing, not just the first, so landing hits back-to-back keeps
## pushing the timeout out rather than it counting down from the first
## punch alone. Cheap/idempotent to call repeatedly.
func enter_combat_mode() -> void:
	_in_combat_mode = true
	_combat_mode_timer = COMBAT_MODE_TIMEOUT_SECONDS

## Drops back to a normal standing idle -- called by _physics_process once
## combat mode's own timeout elapses with no new punches. Only actually
## changes the pose if nothing else (a punch still finishing, or walk/run
## already playing -- Player's own _physics_process runs before this one
## each frame, since Character is its child, so a walk that just started
## THIS SAME frame is already playing by the time this runs) currently owns
## it -- clearing _in_combat_mode is still correct either way; set_walking's
## own idle branch would take over next time it runs regardless (this frame
## or the next), but starting "idle" here too avoids even a single frame of
## whatever pose combat_idle/a punch left the bones in.
func exit_combat_mode() -> void:
	_in_combat_mode = false
	# "walk"/"run" plus every DIRECTIONAL_ANIM/SWORD_DIRECTIONAL_ANIM clip
	# name -- without the directional ones here too, combat mode timing out
	# while actively walking backward/strafing would incorrectly snap to
	# idle for a frame (this ran before set_walking's own next call could
	# correct it), exactly the kind of one-frame pop this early-return
	# already exists to prevent for plain forward walk/run.
	var moving_anim_names := ["walk", "run", "sword_walk", "sword_run"]
	for entry in DIRECTIONAL_ANIM.values():
		moving_anim_names.append(entry["anim"])
	for entry in SWORD_DIRECTIONAL_ANIM.values():
		moving_anim_names.append(entry["anim"])
	if _is_punching or _is_aiming_throw or anim_player.current_animation in moving_anim_names:
		return
	# _wielding_sword always shows sword_idle regardless of combat mode (see
	# set_walking's own comment on why there's no separate sword combat-idle
	# clip) -- since that's already what's showing (this function only ever
	# runs to correct a "combat_idle" hold, never anything else), this is
	# almost always a no-op for the sword case, but still needs to redirect
	# the actual clip NAME here too, or a lagging call could wrongly force
	# the bare-handed "idle" clip over top of a correctly-showing sword_idle.
	var idle_name := "sword_idle" if _wielding_sword else "idle"
	if anim_player.current_animation != idle_name or not anim_player.is_playing():
		# Same hips-reset + no-blend-from-crouch handling as set_walking's own
		# idle branch -- this path is rarer (combat mode timing out while
		# ALSO mid-crouch) but not impossible, and would otherwise reproduce
		# the exact same foot-through-floor clip.
		var leaving_crouch_anim := anim_player.current_animation in ["standing_to_crouch", "crouch_to_idle", "crouch_walk", "sword_standing_to_crouch", "sword_crouch_to_idle"]
		_reset_hips_position()
		anim_player.play(idle_name, 0.0 if leaving_crouch_anim else BLEND_SECONDS)

## Bedrock-style unarmed attack: a quick, non-looping arm jab (a retargeted
## Mixamo clip, see BoxingRightJabAnim/BoxingLeftJabAnim) that hands control
## back to set_walking the instant it finishes (see _on_animation_finished).
## use_left_hand picks which registered animation plays (see player.gd's own
## punch-count logic for when that's true -- every 3rd punch). Blended (see
## BLEND_SECONDS) FROM whatever pose was actually showing (walking mid-
## stride, plain idle, another punch's own recovery) straight into the
## swing's own first frame -- this used to instant-snap to a hand-authored
## guard stance FIRST specifically to soften that same pop, but now that the
## swing itself blends in smoothly, that intermediate snap-to-stance step
## was just ANOTHER unblended pop of its own (confirmed as part of the same
## "punches snap suddenly" report), so it was removed rather than kept
## alongside real blending.
func punch(use_left_hand: bool = false) -> void:
	# Sword only -- see _sword_swing_in_progress's own comment. Dropped
	# here, before enter_combat_mode()/etc run at all, so a spam-click
	# during an already-playing swing has zero effect rather than
	# restarting/switching it.
	if _wielding_sword and _sword_swing_in_progress:
		return
	enter_combat_mode()
	_is_punching = true
	_is_dashing = false # see _is_dashing's own comment on why this needs clearing whenever something else unconditionally takes over
	_is_blocking = false # see _is_blocking's own comment -- same reasoning, higher stakes (a stuck block reduces damage forever, not just the wrong pose)
	_is_dancing = false # see _is_dancing's own comment -- a dance has no input-level exclusion with punching the way shooting does, so this needs an explicit clear here too
	_is_aiming_throw = false # see jump()'s own comment -- reachable if right-click release and a punch tap land on the exact same frame
	if _wielding_sword:
		_sword_swing_in_progress = true
		# Single swing type, no left/right alternation the way the bare-
		# handed jabs have (a two-handed sword swing isn't a "which hand"
		# question) -- use_left_hand is simply ignored here.
		#
		# Combo: a click within SWORD_COMBO_WINDOW_SECONDS of the FIRST
		# swing (sword_attack) plays the follow-up (sword_attack2) instead
		# and consumes the window, so a 3rd click always restarts at
		# sword_attack again -- a plain 2-hit combo, not an indefinite
		# alternation. _sword_combo_ready only gets set true here, on
		# sword_attack itself -- landing on sword_attack2 does NOT chain
		# into a 3rd swing (there isn't one).
		if _sword_combo_ready:
			anim_player.play("sword_attack2", BLEND_SECONDS)
			_sword_combo_ready = false
		else:
			anim_player.play("sword_attack", BLEND_SECONDS)
			_sword_combo_ready = true
			_sword_combo_timer = SWORD_COMBO_WINDOW_SECONDS
	elif _wielding_bottle:
		# A single fixed swing, deliberately NOT routed through
		# ComboData.chain_for("unarmed") below -- a bottle has its own flat,
		# per-durability-state damage (see player.gd's own
		# _try_swing_bottle(), which sources damage from
		# _equipped_bottle.get_melee_damage()), not a 5-stage chain, so
		# touching _combo_stage_index/_combo_window_timer here would just
		# silently corrupt the UNARMED chain's own state for whenever the
		# bottle eventually breaks fully/gets thrown and punch() falls back
		# to bare fists again. use_left_hand is ignored, same reasoning
		# _wielding_sword's own branch already gives -- a one-handed bottle
		# has no obvious "which hand" motivation the way bare-fist boxing
		# does, and no heavy variant exists for it either.
		anim_player.play("punch_right", BLEND_SECONDS)
	else:
		# Chain-driven (see combo_data.gd) rather than the old flat left/
		# right alternation -- use_left_hand is now unused here (which hand
		# plays is baked into the chain's own stage order), left as a
		# parameter still since player.gd's caller/RPC signature still
		# passes it and nothing else currently depends on removing it.
		var chain := ComboData.chain_for("unarmed")
		if _combo_window_timer <= 0.0:
			_combo_stage_index = 0
		var stage: Dictionary = chain[_combo_stage_index]
		_last_combo_stage = stage
		anim_player.play(stage["anim"], BLEND_SECONDS)
		if stage.get("finisher", false):
			_combo_stage_index = 0
			_combo_window_timer = 0.0
		else:
			_combo_stage_index = (_combo_stage_index + 1) % chain.size()
			_combo_window_timer = float(stage.get("window", 0.0))

## Thrown after player.gd detects a hold-then-release on the punch input
## (see its own HEAVY_PUNCH_HOLD_THRESHOLD_SECONDS) instead of a quick tap --
## a single committed haymaker (HeavyPunchAnim), not a left/right pair, so
## there's no use_left_hand parameter here the way punch() has one. Same
## _is_punching handling as a regular punch otherwise (see punch()'s own
## comment on why this blends in rather than snapping to a guard stance
## first).
func heavy_punch() -> void:
	if _wielding_sword and _sword_swing_in_progress: # see punch()'s own comment
		return
	enter_combat_mode()
	_is_punching = true
	_is_dashing = false # see punch()'s own comment
	_is_blocking = false # see punch()'s own comment
	_is_dancing = false # see punch()'s own comment
	# A committed haymaker abandons any in-progress unarmed chain -- same
	# "something else takes over" reasoning already used above for
	# _is_dashing/_is_blocking (see punch()'s own comment).
	_combo_stage_index = 0
	_combo_window_timer = 0.0
	if _wielding_sword:
		_sword_swing_in_progress = true
	anim_player.play("sword_heavy_attack" if _wielding_sword else "punch_heavy", BLEND_SECONDS)

## Plays a jump animation the instant the player leaves the ground (called
## from player.gd right when it applies JUMP_VELOCITY, not on some later
## airborne check) -- is_sprinting picks jump_sprint (a clean single running-
## leap arc) vs jump (a standing jump with its own crouch-launch, trimmed to
## start right at that launch -- see JumpAnim's own comment) matching
## whichever gait the player was actually in at takeoff. Overrides walk/run/
## idle the same way punch does (see set_walking's _is_jumping guard).
## is_moving is a SEPARATE axis from is_sprinting, only consulted while
## _holding_gun -- the pistol pack's own jump split is "was moving at all"
## (walking OR sprinting both count as gun_jump_moving), not "was sprinting"
## specifically, matching how this was explicitly asked for. Every other
## moveset (fist, sword) ignores it entirely and keeps their existing
## is_sprinting-only split.
func jump(is_sprinting: bool = false, is_moving: bool = false) -> void:
	_is_jumping = true
	_is_dashing = false # see punch()'s own comment
	_is_blocking = false # see punch()'s own comment
	_is_shooting = false # see punch()'s own comment
	_is_dancing = false # see punch()'s own comment
	_is_aiming_throw = false # see begin_bottle_aim()'s own comment -- jumping mid-aim is real/reachable (right-click aim + spacebar are on separate input paths), and this unconditionally overwrites anim_player below regardless
	# See hit_reaction()'s own comment on why this needs clearing too, not
	# just _is_punching/_is_dashing/_is_blocking above -- jumping mid-swing
	# is a real, confirmed way to reach the same "stuck, can never swing
	# again" bug those fixed, since this unconditionally overwrites
	# anim_player (below) regardless of whether a swing is in progress.
	_sword_swing_in_progress = false
	_combo_stage_index = 0 # see heavy_punch()'s own comment
	_combo_window_timer = 0.0
	if _wielding_sword:
		# Only one jump clip in the sword pack (no separate sprint-jump
		# variant) -- used regardless of is_sprinting.
		anim_player.play("sword_jump")
	elif _holding_gun:
		anim_player.play("gun_jump_moving" if is_moving else "gun_jump")
	else:
		anim_player.play("jump_sprint" if is_sprinting else "jump")

## Ends the jump animation state the instant the player actually touches
## back down (called from player.gd's own landing check), regardless of how
## much of the clip is left -- jump.tres in particular runs well past the
## game's real ~0.92s air time into a landing/recovery tail (see its own
## comment on why that wasn't trimmed out), and without this the character
## would keep playing that unrelated recovery pose for another second-plus
## after already landing and potentially already moving again. Safe to call
## unconditionally on every landing (falling off a ledge without ever
## jumping, for instance) -- a no-op if _is_jumping is already false.
func land() -> void:
	_is_jumping = false

## Plays a dedicated dash clip -- called from player.gd's own _try_start_dash
## the instant a double-tap dash actually fires, with direction being
## whichever of "forward"/"backward"/"left"/"right" it classified the dash
## as (see its own comment on that classification). Only backward has a real
## clip to play (DashBackwardAnim) -- forward/left/right are deliberately
## left alone here -- player.gd's own dash handling already forces the
## regular run animation for the burst's duration regardless of what this
## method does (see its own is_dashing handling), so doing nothing for those
## three directions is enough to keep them exactly as they were. (A forward
## clip, DashForwardAnim, was tried and dropped -- see DashBackwardAnim's own
## comment.) Blended in (see BLEND_SECONDS) from whatever was showing, same
## as a punch.
func dash(direction: String) -> void:
	if direction == "backward":
		_is_dashing = true
		_is_blocking = false # see punch()'s own comment
		_is_shooting = false # see punch()'s own comment
		_is_dancing = false # see punch()'s own comment
		_is_aiming_throw = false # see jump()'s own comment
		_sword_swing_in_progress = false # see jump()'s own comment
		_combo_stage_index = 0 # see heavy_punch()'s own comment
		_combo_window_timer = 0.0
		anim_player.play("dash_backward", BLEND_SECONDS)

## Ends the dash animation state the instant player.gd's own physics burst
## is over (called from there, see its own comment) -- the dash_backward
## clip runs well past DASH_DURATION_SECONDS into its own follow-through,
## same "cut the visible recovery tail short once the real effect has
## already ended" reasoning as land(). Safe to call unconditionally (a
## forward/left/right dash never set _is_dashing in the first place, so this
## is just a no-op then, same as land() when nothing was jumping).
func end_dash() -> void:
	_is_dashing = false

## Plays the block animation once (BlockAnim, retargeted from a user-
## supplied clip -- see its own comment) -- called from player.gd the
## instant right-click is pressed. Deliberately has no "hold to keep
## blocking" concept: unlike start_crouch()/end_crouch()'s separate press/
## release pair, there's only this one entry point, and the block window
## itself is exactly as long as this one-shot clip plays (see is_blocking(),
## which player.gd's own take_damage() checks) -- once BLOCK finishes and
## _on_animation_finished clears _is_blocking, set_walking takes back over
## automatically, the same handoff every other one-shot here already uses.
## Enters combat mode the same as a punch -- blocking only makes sense
## mid-fight, and standing still afterward should hold the boxing stance
## rather than drop back to a relaxed idle.
func block() -> void:
	enter_combat_mode()
	_is_blocking = true
	_is_punching = false # see punch()'s own comment on why _is_dashing/_is_blocking get defensively cleared -- same reasoning applies in reverse here
	_is_jumping = false
	_is_dashing = false
	_is_dancing = false # see punch()'s own comment
	# Confirmed as a real reported bug: blocking mid-swing cleared
	# _is_punching above (correctly abandoning that swing's "owns the
	# animation player" flag) but left _sword_swing_in_progress -- a
	# SEPARATE flag, see hit_reaction()'s own comment -- still true forever,
	# since the swing clip this overwrites (below) never reaches its own
	# natural end to fire _on_animation_finished. Permanently blocked every
	# future swing until the fix here.
	_sword_swing_in_progress = false
	_combo_stage_index = 0 # see heavy_punch()'s own comment
	_combo_window_timer = 0.0
	anim_player.play("sword_block" if _wielding_sword else "block", BLEND_SECONDS)

## Whether the block animation is CURRENTLY playing -- true for exactly the
## clip's own ~0.7s runtime, false before and after. player.gd's own
## take_damage() calls this on the defender to decide whether an incoming
## hit gets reduced (see its own BLOCK_DAMAGE_MULTIPLIER).
func is_blocking() -> bool:
	return _is_blocking

## Starts (or re-holds) the bottle throw windup -- called from player.gd
## the instant right-click is first pressed while a bottle is equipped (see
## its own aim-edge handling, not called every frame aim stays held).
## Plays ThrowBottleAnim then immediately freezes it at
## THROW_BOTTLE_WINDUP_TIME (seek + speed_scale=0, the same mechanism ui/
## dev_console.gd's own /gun shootpose dev tool already uses to freeze
## mid-clip for posing, just reused here as a real gameplay mechanic) --
## the arm stays raised and cocked back for as long as aim is held, with no
## natural "finished" signal of its own (unlike every other one-shot clip
## in this file) until either cancel_bottle_aim() or release_bottle_throw()
## ends it.
func begin_bottle_aim() -> void:
	enter_combat_mode()
	_is_aiming_throw = true
	_is_punching = false # see punch()'s own comment on why sibling one-shot flags get defensively cleared
	_is_blocking = false
	_is_jumping = false
	_is_dashing = false
	_is_dancing = false
	_sword_swing_in_progress = false # see block()'s own comment
	_combo_stage_index = 0 # see heavy_punch()'s own comment
	_combo_window_timer = 0.0
	anim_player.play("throw_bottle", BLEND_SECONDS)
	anim_player.seek(THROW_BOTTLE_WINDUP_TIME, true)
	anim_player.speed_scale = 0.0

## Cancels a held windup WITHOUT throwing -- player.gd calls this the
## instant right-click is released before a throw actually fired (see its
## own aim-edge handling). Restores speed_scale (left at 0.0 by
## begin_bottle_aim(), otherwise a LATER real throw's own release would
## silently replay frozen) and hands control straight back to set_walking()
## -- there's no animation_finished to wait for here, unlike every other
## one-shot clip's own cancel path, since the clip was never allowed to
## actually finish playing.
func cancel_bottle_aim() -> void:
	if not _is_aiming_throw:
		return
	_is_aiming_throw = false
	anim_player.speed_scale = 1.0

## Lets a held windup actually play out -- player.gd's own _try_throw_bottle()
## calls this the instant a throw fires (punch pressed while aiming), right
## alongside the real projectile spawn. Simply un-freezes playback
## (speed_scale back to 1.0) from wherever begin_bottle_aim() already left
## it seeked to (always THROW_BOTTLE_WINDUP_TIME -- nothing else ever seeks
## this clip) -- _on_animation_finished clears _is_aiming_throw once the
## release/follow-through actually finishes playing, the same handoff every
## other one-shot clip here already uses.
func release_bottle_throw() -> void:
	if not _is_aiming_throw:
		return
	anim_player.speed_scale = 1.0

## What player.gd's own _punch() needs, right after calling punch(), to
## decide the DEFENDER's take_damage() call -- how much damage/hitstun this
## particular swing carries and whether it was the chain's own finisher.
## Sword works off _sword_combo_ready directly rather than needing its own
## _last_combo_stage-style state: it's unconditionally true right after
## sword_attack and false right after sword_attack2 (see punch()'s own
## comment), so "the swing that just played was the finisher" is reliably
## `not _sword_combo_ready` immediately after any punch() call while
## wielding a sword -- sword_attack2 is always the last swing in its own
## 2-hit combo.
func get_last_attack_combo_info() -> Dictionary:
	if _wielding_sword:
		return {"damage_scale": 1.0, "hitstun_scale": 1.0, "finisher": not _sword_combo_ready}
	return {
		"damage_scale": float(_last_combo_stage.get("damage_scale", 1.0)),
		"hitstun_scale": float(_last_combo_stage.get("hitstun_scale", 1.0)),
		"finisher": bool(_last_combo_stage.get("finisher", false)),
	}

## Plays the standing-to-crouch transition once (see start_crouch's own
## comment on why this and end_crouch block set_walking entirely rather
## than just the idle branch) -- called from player.gd the instant the
## crouch key is first pressed (just_pressed), not held continuously.
## Firing this while the player is ALREADY moving is fine: set_walking's own
## "crouching" branch plays crouch_walk instead the very same/next frame,
## so this transition only actually gets seen through to completion while
## standing still, exactly as intended.
func start_crouch() -> void:
	_is_crouch_transitioning = true
	_is_dashing = false # see punch()'s own comment
	_is_shooting = false # see punch()'s own comment
	_is_dancing = false # see punch()'s own comment
	_is_aiming_throw = false # see jump()'s own comment
	_sword_swing_in_progress = false # see jump()'s own comment
	_combo_stage_index = 0 # see heavy_punch()'s own comment
	_combo_window_timer = 0.0
	anim_player.play("sword_standing_to_crouch" if _wielding_sword else "standing_to_crouch", CROUCH_BLEND_SECONDS)

## Plays the crouch-to-standing transition once -- called from player.gd
## the instant the crouch key is released (just_released). Needs its own
## blocking flag (not just relying on set_walking's "crouching" branch doing
## nothing) because the moment the key is released, player.gd's own
## crouching bool flips to false THAT SAME FRAME -- without
## _is_crouch_transitioning, set_walking would immediately fall through to
## the normal idle/combat_idle branch and stomp this animation before it
## ever got to play.
func end_crouch() -> void:
	_is_crouch_transitioning = true
	_is_shooting = false # see punch()'s own comment
	_is_dancing = false # see punch()'s own comment
	_is_aiming_throw = false # see jump()'s own comment
	_sword_swing_in_progress = false # see jump()'s own comment
	_combo_stage_index = 0 # see heavy_punch()'s own comment
	_combo_window_timer = 0.0
	anim_player.play("sword_crouch_to_idle" if _wielding_sword else "crouch_to_idle", CROUCH_BLEND_SECONDS)

## Played the instant a hit actually lands (not blocked) on a sword-
## wielding character (see player.gd's own take_damage(), which calls this
## on the defender) -- a reaction the fist system has never had (see
## player.gd's own _stun_remaining comment on that pre-existing gap), so
## this only does anything at all while _wielding_sword, leaving unarmed
## take_damage() completely unchanged. Reuses _is_punching as its "owns the
## animation player" flag rather than inventing a new one -- semantically
## this IS "some one-shot upper-body clip owns anim_player right now," the
## exact condition _is_punching already gates set_walking's own early
## return on (see there), and player.gd's own punch-trigger has no gate on
## character state at all (only its own cooldown timer), so a punch/attack
## thrown right after taking a hit already correctly interrupts this either
## way -- no risk of getting stuck.
##
## _sword_swing_in_progress (a SEPARATE flag from _is_punching, added later
## for the spam-click fix -- see punch()'s own comment) DOES need clearing
## here too, though: getting hit while a swing is already in progress
## overwrites anim_player with "sword_impact" via the play() call below,
## which means the ORIGINAL swing clip ("sword_attack"/"sword_attack2")
## never reaches its own natural end -- _on_animation_finished never fires
## for it, so nothing would ever clear _sword_swing_in_progress, permanently
## blocking every future swing. Confirmed as a real reported bug ("swings
## twice then stops letting me swing," traced back to getting hit by the
## CPU mid-swing). The swing genuinely IS over the moment it gets
## interrupted like this, so clearing it right here (not waiting on
## sword_impact's own finish) is the correct fix, not a workaround.
func hit_reaction() -> void:
	if not _wielding_sword:
		return
	_is_punching = true
	_sword_swing_in_progress = false
	anim_player.play("sword_impact", BLEND_SECONDS)

## Played instead of the plain block hold the instant a block actually
## reduces incoming damage (see player.gd's own take_damage(), which calls
## this on the defender when is_blocking() was true) -- same _wielding_sword
## gate, _is_punching reuse, and _sword_swing_in_progress fix as
## hit_reaction() above, for the same reasons.
func block_impact() -> void:
	if not _wielding_sword:
		return
	_is_punching = true
	_sword_swing_in_progress = false
	anim_player.play("sword_block_impact", BLEND_SECONDS)

func _on_animation_finished(anim_name: StringName) -> void:
	if anim_name == "punch_right" or anim_name == "punch_left" or anim_name == "punch_heavy" or anim_name == "sword_attack" or anim_name == "sword_attack2" or anim_name == "sword_heavy_attack" or anim_name == "sword_impact" or anim_name == "sword_block_impact":
		_is_punching = false
	if anim_name == "sword_attack" or anim_name == "sword_attack2" or anim_name == "sword_heavy_attack":
		_sword_swing_in_progress = false
	elif anim_name == "jump" or anim_name == "jump_sprint" or anim_name == "sword_jump" or anim_name == "gun_jump" or anim_name == "gun_jump_moving":
		_is_jumping = false
	elif anim_name == "standing_to_crouch" or anim_name == "crouch_to_idle" or anim_name == "sword_standing_to_crouch" or anim_name == "sword_crouch_to_idle":
		_is_crouch_transitioning = false
	elif anim_name == "dash_backward":
		_is_dashing = false
	elif anim_name == "block" or anim_name == "sword_block":
		_is_blocking = false
	elif anim_name == "gun_shoot":
		_is_shooting = false
	elif anim_name == "throw_bottle":
		_is_aiming_throw = false
	elif anim_name == "entering_car" or anim_name == "exiting_car":
		_is_car_transitioning = false
		# A real reported bug: "the character is stuck looking left, the
		# model, not the camera" -- entering_car/exiting_car are the ONLY
		# two animations in this whole game with a real HIPS rotation track
		# (see play_enter_car's own comment on why -- the seat-transition
		# motion genuinely turns the whole body, unlike every other clip
		# here, which all deliberately skip the hips bone entirely so this
		# node's own Node3D rotation is the sole source of truth for body
		# facing, see retarget()'s own "our game's own walk/run/punch
		# animations already follow this convention" comment). Once
		# exiting_car leaves the hips bone rotated to whatever angle its
		# own final frame happened to end on, NOTHING resets it -- walk/
		# run/idle/etc never touch that bone at all, so AnimationPlayer has
		# no reason to move it away from wherever the last clip left it,
		# permanently skewing the whole visible body (hips is the root of
		# every other bone) by that leftover amount even though this node's
		# own rotation is already correct. Only exiting_car resets it, not
		# entering_car -- the natural seated lean/twist entering_car ends
		# on is exactly what should keep showing while seated (see
		# play_enter_car's own comment), it's only once you're back to
		# walking around the world that hips needs to return to the
		# identity every other animation already assumes.
		if anim_name == "exiting_car":
			var hips_idx: int = FusedBody.BONES.find("hips")
			_body_skeleton.set_bone_pose_rotation(hips_idx, Quaternion.IDENTITY)
	elif anim_name == "climb_top_out":
		# player.gd's own animation_finished listener (see play_climb_top_out's
		# own comment) is what actually snaps the real physics body onto the
		# ledge -- this just releases set_walking() to take back over once
		# that's done, same "clear the flag, let the next set_walking call
		# pick the right clip" pattern end_climb() already uses.
		_is_climbing = false
		# Same class of bug exiting_car's own hips-ROTATION reset above fixes,
		# for hips POSITION instead: this clip carries a real hips position
		# track (see ClimbTopOutAnim's own comment), and NO other animation
		# in this game ever touches that track (ordinary walk/run/idle/etc
		# are rotation-only), so without an explicit reset here the hips --
		# and therefore the whole body, since it's the root bone -- would
		# stay permanently offset at wherever the clip's own final frame left
		# it. FusedBody.bone_origins()["hips"] is the SAME base value
		# retarget()'s own hips-position tracks are always written relative
		# to (see its own target_hips_base_pos), so this is a real "back to
		# rest," not a guessed number.
		var hips_pos_idx: int = FusedBody.BONES.find("hips")
		_body_skeleton.set_bone_pose_position(hips_pos_idx, FusedBody.bone_origins()["hips"])

## Every bone that gets its own ragdoll RigidBody3D -- every
## FusedBody.BONE_PARENT key (i.e. every non-root bone) except "head",
## which has no capsule/segment of its own (see FusedBody.segment_defs) and
## so has nothing to build a rigid body FROM. "hips" (the root) has a body
## too, but isn't in this list -- it isn't "posable" the way every other
## bone is (nothing computes a parent-relative delta for it); its own body
## instead directly drives this whole node's root transform every frame
## (see _physics_process). Computed fresh each call rather than cached -- a
## dictionary's .keys() plus one .erase() is cheap enough that caching would
## just be one more piece of state to keep in sync for no real benefit.
func _ragdoll_posable_bones() -> Array:
	var bones := FusedBody.BONE_PARENT.keys()
	bones.erase("head")
	return bones

## Collapses the character to the ground on death using REAL physics: a
## small rig of RigidBody3D capsules (one per posable bone, sized/placed
## from FusedBody.segment_defs()'s own geometry) connected by
## ConeTwistJoint3D joints mirroring FusedBody.BONE_PARENT, added to the
## scene root -- NOT parented under this Character node, since they need to
## exist in plain world space independent of whatever transform this node's
## own root gets set to each frame (see _physics_process). Idempotent --
## calling this again while already ragdolled does nothing, so a stray
## second death signal can't rebuild the rig mid-fall.
func ragdoll() -> void:
	if _is_ragdolled:
		return
	_is_ragdolled = true
	_rest_rotation = rotation

	# AnimationPlayer.stop() snaps every bone it was driving straight back to
	# its rest (identity) pose THE INSTANT it's called (verified directly: a
	# mid-swing ~0.52 rad leg rotation reset to exactly 0 the moment stop()
	# ran) -- called with no other precaution, that's an instant, visible pop
	# to a plain standing pose (arms/legs suddenly straightening out) before
	# any physics has even started moving anything, which is exactly what
	# read as the character "teleporting" or playing some kind of animation
	# right at the moment of death. Capturing each animated bone's live pose
	# first and re-applying it immediately after stop() keeps the mesh
	# visually frozen exactly where it was the instant death happened, so the
	# only thing that moves from here on is physics.
	var posable_bones := _ragdoll_posable_bones()
	var live_pose := {}
	for bone_name in posable_bones:
		live_pose[bone_name] = _body_skeleton.get_bone_pose_rotation(_body_skeleton.find_bone(bone_name))
	anim_player.stop()
	for bone_name in posable_bones:
		_body_skeleton.set_bone_pose_rotation(_body_skeleton.find_bone(bone_name), live_pose[bone_name])

	var segments := {}
	for seg in FusedBody.segment_defs():
		segments[seg["bone"]] = seg
	var origins := FusedBody.bone_origins()

	_ragdoll_rig = Node3D.new()
	get_tree().root.add_child(_ragdoll_rig)

	# Carries over whatever momentum the player already had (so getting
	# killed mid-stride or mid-fall doesn't look like it teleports to a
	# dead stop first) -- applied to every body. The random toppling spin
	# (see RAGDOLL_TORSO_SPIN_DEGREES_PER_SEC's own comment for why only
	# one body gets it) goes on the root ("hips") alone.
	var player := get_parent()
	var initial_velocity := Vector3.ZERO
	if player is CharacterBody3D:
		initial_velocity = (player as CharacterBody3D).velocity
	var root_spin := Vector3(
		randf_range(-1.0, 1.0), randf_range(-1.0, 1.0), randf_range(-1.0, 1.0)
	).normalized() * deg_to_rad(RAGDOLL_TORSO_SPIN_DEGREES_PER_SEC)

	# Every bone WITH a segment gets a body -- "head" has none (see
	# _ragdoll_posable_bones's own comment), so this iterates segments.keys()
	# (== "hips" + every posable bone), not FusedBody.BONES directly, which
	# would include "head" and KeyError against segments.
	for bone_name in segments.keys():
		var seg: Dictionary = segments[bone_name]
		var world_a: Vector3 = global_transform * (seg["a"] as Vector3)
		var world_b: Vector3 = global_transform * (seg["b"] as Vector3)
		var angular_velocity := root_spin if bone_name == "hips" else Vector3.ZERO
		# Radius scaled by hand; the capsule's own LENGTH already is, since
		# world_a/world_b came through global_transform (which carries this
		# character's own height scale -- see set_height_scale). Without
		# this, a 1.5x character's corpse would be long-limbed but
		# pencil-thin, and a short one's stubby but barrel-wide.
		var body := _make_ragdoll_body(world_a, world_b, float(seg["r"]) * _height_scale, initial_velocity, angular_velocity)
		_ragdoll_rig.add_child(body)
		_ragdoll_bodies[bone_name] = body
		# See player.gd's own grab/carry system -- "grabbable" is the tag its
		# raycast targeting checks for, the metadata below is what lets a
		# receiving peer resolve WHICH player's WHICH bone a carry RPC refers
		# to (see player.gd's own _resolve_ragdoll_body/_rpc_start_carry).
		# Only meaningful for a real player (get_multiplayer_authority()
		# reads 1 -- an arbitrary but harmless default -- on a non-Player
		# parent, but nothing ever looks this metadata up unless a grab RPC
		# was actually sent for it, which player.gd's own _start_carry only
		# does while Net.is_networked()).
		body.add_to_group("grabbable")
		body.physics_material_override = RAGDOLL_PHYSICS_MATERIAL
		body.mass *= RAGDOLL_MASS_MULTIPLIER.get(bone_name, 1.0)
		if player != null:
			body.set_meta("ragdoll_owner_peer_id", player.get_multiplayer_authority())
			body.set_meta("ragdoll_bone_name", bone_name)

	# A dead player becomes lootable the instant they ragdoll -- see
	# loot/loot_component.gd's own class comment. Snapshots ONLY the
	# equipped-slot items (get_equipped_item_ids(), not general storage --
	# nobody but the owner needs to see inside someone's bag, matching this
	# project's own already-confirmed multiplayer visibility scope for
	# inventories), parented onto the hips capsule so its own lifecycle is
	# automatically tied to reset_ragdoll()'s own later
	# _ragdoll_rig.queue_free() -- no separate cleanup code needed.
	var inventory: Node = player.get_node_or_null("Inventory") if player != null else null
	if inventory != null and inventory.has_method("get_equipped_item_ids"):
		var equipped: Dictionary = inventory.get_equipped_item_ids()
		if not equipped.is_empty():
			var loot: Node3D = LootComponent.new()
			var loot_items: Array[Dictionary] = []
			for slot_key in equipped:
				loot_items.append({"item_id": equipped[slot_key], "count": 1, "source_slot": slot_key})
			loot.loot_items = loot_items
			# Lets take_all() actually strip THIS corpse's own worn gear as
			# each item is taken -- see loot_component.gd's own
			# _strip_source_slot() for what that does and why it's safe.
			loot.strip_target = player
			_ragdoll_bodies["hips"].add_child(loot)

	for bone_name in posable_bones:
		var parent_name: String = FusedBody.BONE_PARENT[bone_name]
		var attach_world: Vector3 = global_transform * (origins[bone_name] as Vector3)
		var limits: Dictionary = RAGDOLL_JOINT_LIMITS[bone_name]
		_add_ragdoll_joint(_ragdoll_bodies[parent_name], _ragdoll_bodies[bone_name], attach_world, limits)

	# Captured AFTER every body/joint exists, so the very first physics
	# frame's "how much has this rotated since the start" delta (see
	# _physics_process) comes out as an identity rotation -- without this,
	# the arbitrary (if consistent) basis _make_ragdoll_body constructs for
	# each capsule would otherwise show up as an instant, visible pop in the
	# bone's pose the moment physics starts moving it. Relative to each
	# bone's own IMMEDIATE PARENT, not always the root -- see
	# _physics_process's own comment for why using the root regardless of
	# how deep a bone sits in the chain was a real (if previously subtle,
	# one-level-deep) bug.
	for bone_name in posable_bones:
		var body: RigidBody3D = _ragdoll_bodies[bone_name]
		var parent_name: String = FusedBody.BONE_PARENT[bone_name]
		var parent_basis_at_start: Basis = _ragdoll_bodies[parent_name].global_transform.basis
		_ragdoll_initial_relative[bone_name] = parent_basis_at_start.inverse() * body.global_transform.basis
		_ragdoll_initial_pose[bone_name] = _body_skeleton.get_bone_pose_rotation(_body_skeleton.find_bone(bone_name))

	# The player's own standing collision capsule would otherwise still be
	# sitting right where the ragdoll starts falling from.
	if player is CollisionObject3D:
		_ragdoll_player_collision_layer = player.collision_layer
		_ragdoll_player_collision_mask = player.collision_mask
		player.collision_layer = 0
		player.collision_mask = 0

## One capsule-shaped RigidBody3D spanning world_a to world_b -- built with
## an arbitrary (but consistent) orthonormal basis whose Y axis runs along
## the segment (a capsule's own X/Z choice doesn't matter visually, it's
## rotationally symmetric); a fallback cross vector avoids a degenerate
## basis on the rare bone whose segment happens to run parallel to the
## first choice. Starts already moving (see RAGDOLL_GRAVITY_SCALE's own
## comment for why) rather than from plain rest, and its mass is sized off
## its own actual volume (see RAGDOLL_DENSITY) rather than Godot's flat
## mass=1 default.
func _make_ragdoll_body(world_a: Vector3, world_b: Vector3, radius: float, initial_velocity: Vector3, initial_angular_velocity: Vector3) -> RigidBody3D:
	var body := RigidBody3D.new()
	body.collision_layer = RAGDOLL_COLLISION_LAYER
	body.collision_mask = 1
	body.gravity_scale = RAGDOLL_GRAVITY_SCALE
	body.linear_damp = RAGDOLL_LINEAR_DAMP
	body.angular_damp = RAGDOLL_ANGULAR_DAMP
	# Godot's default auto-sleep (can_sleep=true) puts a RigidBody3D to sleep
	# once its velocity stays under a threshold for a moment -- confirmed
	# directly (a scratch diagnostic watching every ragdoll body's own
	# .sleeping) that the WHOLE rig (one physics island) falls asleep
	# together well within a normal death's timeframe, and a sleeping body
	# stops being simulated entirely, gravity included. If a limb happened
	# to be resting against a joint's swing limit rather than fully
	# drooped when that threshold was crossed, it stays visibly stuck there
	# forever -- exactly the reported "arm frozen mid-air" even though the
	# rest of the body had already settled. Disabling sleep keeps every
	# body live for the ragdoll's whole lifetime so it keeps responding to
	# gravity/collisions instead of ever locking into whatever pose it
	# happened to be in at the moment it went quiet.
	body.can_sleep = false
	body.linear_velocity = initial_velocity
	body.angular_velocity = initial_angular_velocity
	var length: float = world_a.distance_to(world_b)
	var mid: Vector3 = (world_a + world_b) / 2.0
	var y_axis: Vector3 = (world_b - world_a).normalized() if length > 0.001 else Vector3.UP
	var helper: Vector3 = Vector3.FORWARD if absf(y_axis.dot(Vector3.FORWARD)) < 0.95 else Vector3.RIGHT
	var x_axis: Vector3 = y_axis.cross(helper).normalized()
	var z_axis: Vector3 = x_axis.cross(y_axis).normalized()
	body.global_transform = Transform3D(Basis(x_axis, y_axis, z_axis), mid)

	var shape := CollisionShape3D.new()
	var capsule := CapsuleShape3D.new()
	capsule.radius = radius
	capsule.height = maxf(length, radius * 2.0 + 0.01)
	shape.shape = capsule
	body.add_child(shape)

	var cylinder_volume: float = PI * radius * radius * length
	var cap_volume: float = (4.0 / 3.0) * PI * radius * radius * radius
	body.mass = maxf((cylinder_volume + cap_volume) * RAGDOLL_DENSITY, 0.01)
	return body

## Read by player.gd's own grab/carry RPC handlers (_resolve_ragdoll_body)
## to reach a SPECIFIC bone on ANOTHER player's ragdoll -- null both before
## ragdoll() has ever run and for "head" (never has its own body, see
## _ragdoll_posable_bones's own comment).
func get_ragdoll_body(bone_name: String) -> RigidBody3D:
	return _ragdoll_bodies.get(bone_name)

func _add_ragdoll_joint(parent_body: RigidBody3D, child_body: RigidBody3D, attach_world: Vector3, limits: Dictionary) -> void:
	var joint := ConeTwistJoint3D.new()
	_ragdoll_rig.add_child(joint)
	joint.global_position = attach_world
	joint.node_a = joint.get_path_to(parent_body)
	joint.node_b = joint.get_path_to(child_body)
	joint.set_param(ConeTwistJoint3D.PARAM_SWING_SPAN, deg_to_rad(limits["swing_degrees"]))
	joint.set_param(ConeTwistJoint3D.PARAM_TWIST_SPAN, deg_to_rad(limits["twist_degrees"]))

## Un-ragdolls back to a normal standing pose for respawn: frees the whole
## physics rig, restores this node's own transform and the player's
## collision, and resets every bone pose to identity -- an instant snap
## rather than a tween, matching the position/rotation reset also
## happening instantly elsewhere at respawn (see player.gd's own
## respawn()).
func reset_ragdoll() -> void:
	if not _is_ragdolled:
		return
	_is_ragdolled = false
	if is_instance_valid(_ragdoll_rig):
		_ragdoll_rig.queue_free()
	_ragdoll_rig = null
	_ragdoll_bodies.clear()
	_ragdoll_initial_relative.clear()
	_ragdoll_initial_pose.clear()

	position = Vector3.ZERO
	rotation = _rest_rotation
	for bone_name in _ragdoll_posable_bones():
		_body_skeleton.set_bone_pose_rotation(_body_skeleton.find_bone(bone_name), Quaternion.IDENTITY)

	# Dying mid-animation (e.g. mid-swing, mid-punch, mid-dash) leaves
	# whichever one-shot flag was covering that clip stuck true forever:
	# ragdoll()'s own anim_player.stop() never lets the clip reach its
	# natural end, so _on_animation_finished (the only other place these
	# clear) never fires for it. Left uncleared, this was a real, confirmed
	# bug -- set_walking()'s own early-return on _is_punching/_is_jumping/
	# _is_dashing/_is_blocking (see its own comment) would then bail out
	# every single frame after respawn, forever, before ever reaching the
	# normal idle/walk logic (reading as "stuck in the death pose"), and
	# punch()/heavy_punch()'s own _sword_swing_in_progress guard would
	# likewise reject every future swing as if one were still playing.
	# Explicitly clearing everything here, right alongside the rest of
	# respawn's reset, is what actually lets the very next set_walking()
	# call (player.gd's respawn() runs before this returns) show idle again.
	_is_punching = false
	_is_jumping = false
	_is_dashing = false
	_is_blocking = false
	_is_shooting = false
	_is_dancing = false
	_is_crouch_transitioning = false
	_in_combat_mode = false
	_combat_mode_timer = 0.0
	_sword_combo_ready = false
	_sword_combo_timer = 0.0
	_sword_swing_in_progress = false
	_combo_stage_index = 0
	_combo_window_timer = 0.0
	anim_player.stop()
	leg_anim_player.stop()

	var player := get_parent()
	if player is CollisionObject3D:
		player.collision_layer = _ragdoll_player_collision_layer
		player.collision_mask = _ragdoll_player_collision_mask

## Every physics step while ragdolled: this node's own root transform
## follows the "hips" body (translated so the hips bone's own PIVOT -- not
## this node's local origin -- lines up with that body's actual simulated
## position), and every OTHER bone's pose is set to its own rotation
## relative to its own IMMEDIATE PARENT's current body -- reusing the exact
## same "rotation only, position stays at the bone's fixed pivot" mechanism
## every animation in this file already uses (set_bone_pose_rotation, never
## position), just fed live physics results each frame instead of a baked
## keyframe curve.
## This was chosen over driving the skeleton from each bone's own absolute
## physics transform because the skinning bind pose assumes that
## fixed-pivot convention (see this file's own class comment) -- feeding it
## a raw, freely-translating world transform per bone would fight that
## assumption and visibly distort the mesh.
##
## Relative to the IMMEDIATE PARENT, not always "hips" -- an earlier version
## (back when only forearms were ever real children of anything) always
## measured every bone's delta against the root directly, which happened to
## be correct for every bone THEN since none were more than one level deep.
## Once the rig grew real multi-level chains (a hand three joints from root,
## a shin two), reusing that same always-relative-to-root logic would have
## silently double-applied each ancestor's own rotation on top of its
## descendants' (Skeleton3D already composes a child's LOCAL pose on top of
## its parent's GLOBAL pose automatically -- feeding it an already-root-
## relative value as if it were parent-relative composes the parent's own
## rotation into the result a second time). Measuring against the parent's
## own CURRENT basis instead keeps each bone's local pose exactly what
## Skeleton3D's own composition expects.
func _physics_process(delta: float) -> void:
	if _in_combat_mode:
		_combat_mode_timer -= delta
		if _combat_mode_timer <= 0.0:
			exit_combat_mode()

	if _sword_combo_ready:
		_sword_combo_timer -= delta
		if _sword_combo_timer <= 0.0:
			_sword_combo_ready = false

	if _combo_window_timer > 0.0:
		_combo_window_timer -= delta
		if _combo_window_timer <= 0.0:
			_combo_stage_index = 0

	if not _is_ragdolled:
		return
	var hips_body: RigidBody3D = _ragdoll_bodies.get("hips")
	if hips_body == null or not is_instance_valid(hips_body):
		return

	var hips_pivot: Vector3 = FusedBody.bone_origins()["hips"]
	var hips_basis_now: Basis = hips_body.global_transform.basis
	global_transform = Transform3D(hips_basis_now, hips_body.global_position - hips_basis_now * hips_pivot)

	for bone_name in _ragdoll_posable_bones():
		var body: RigidBody3D = _ragdoll_bodies.get(bone_name)
		if body == null or not is_instance_valid(body):
			continue
		var parent_name: String = FusedBody.BONE_PARENT[bone_name]
		var parent_body: RigidBody3D = _ragdoll_bodies.get(parent_name)
		if parent_body == null or not is_instance_valid(parent_body):
			continue
		var parent_basis_now: Basis = parent_body.global_transform.basis
		var current_relative: Basis = parent_basis_now.inverse() * body.global_transform.basis
		var pose_delta: Basis = (_ragdoll_initial_relative[bone_name] as Basis).inverse() * current_relative
		var pose_rotation: Quaternion = (_ragdoll_initial_pose[bone_name] as Quaternion) * pose_delta.get_rotation_quaternion()
		_body_skeleton.set_bone_pose_rotation(_body_skeleton.find_bone(bone_name), pose_rotation)

## Enables/disables paint raycasting. Disabled once the character leaves
## the creator scene (painting has no purpose in the room, and this also
## avoids doing the raycast work every frame for nothing).
func set_paintable(enabled: bool) -> void:
	_paintable = enabled

func get_head_shape() -> String:
	return _head_shape

func get_hair_style() -> String:
	return _hair_style

## The head's own BoneAttachment3D -- lives under BodySkeleton now (see
## _build_head), not directly under Character, so callers (e.g. hud.gd's
## portrait camera) should go through this instead of a direct node-path
## lookup.
func get_head_node() -> Node3D:
	return _head_pivot

## The right hand's own BoneAttachment3D -- see _build_fused_body's own
## comment. Whatever gets parented under this (e.g. a picked-up weapon,
## see weapons/greatsword.gd's own _equip()) automatically follows "hand_r"'s
## live animated pose every frame, no extra code needed on either side.
func get_hand_node() -> Node3D:
	return _hand_r_pivot

## Generic BoneAttachment3D lookup for ANY body bone (spine, arm_l, arm_r,
## forearm_l, forearm_r, neck, ...), lazily created and cached -- unlike
## get_head_node()/get_hand_node() (each backed by its own dedicated var,
## fine when only ever ONE bone's worth of attachment point is needed),
## clothing (wearables/clothing.gd) needs several DIFFERENT bones at once
## (a torso panel on "spine", sleeves on "arm_l"/"arm_r", etc.) so a rigid
## prop attached to any ONE of them can't cover the rest -- see clothing.gd's
## own class comment for why several small per-bone pieces are used instead
## of one single-parent prop. Safe to cache indefinitely: _body_skeleton
## (unlike the head, which rebuilds on a shape change) is built once and
## never rebuilt.
func get_bone_node(bone_name: String) -> Node3D:
	if _bone_attachments.has(bone_name) and is_instance_valid(_bone_attachments[bone_name]):
		return _bone_attachments[bone_name]
	var attachment := BoneAttachment3D.new()
	attachment.name = bone_name.capitalize().replace(" ", "") + "Pivot"
	_body_skeleton.add_child(attachment)
	attachment.bone_name = bone_name
	_bone_attachments[bone_name] = attachment
	return attachment


## Called by weapons/greatsword.gd's own _equip() right after it reparents
## itself onto get_hand_node() -- see _wielding_sword's own comment for what
## this actually switches over.
func set_wielding_sword(enabled: bool) -> void:
	_wielding_sword = enabled
	# No "drop sword" mechanic exists yet to actually reach this with
	# enabled=false today, but resetting both here is cheap and correct
	# regardless -- neither a stale "ready for the follow-up swing" flag
	# nor a stale "currently mid-swing" lock has any business surviving a
	# weapon change.
	_sword_combo_ready = false
	_sword_swing_in_progress = false
	_combo_stage_index = 0 # see heavy_punch()'s own comment -- same reasoning, other direction (unarmed chain abandoned on switching TO a sword)
	_combo_window_timer = 0.0
	_is_dancing = false # see punch()'s own comment -- equipping a weapon should end a dance in progress the same way any other action does
	# Mutually exclusive with holding a gun -- weapons/two_handed_sword.gd's
	# own _equip() already frees whatever model was previously parented onto
	# get_hand_node() before attaching itself (see its own "replace, don't
	# stack" comment), but that only handles the VISUAL swap; nothing there
	# has any reason to know about guns specifically. Clearing _holding_gun
	# (and its own _is_aiming/_is_shooting) HERE, the one place both
	# movesets' state actually lives, keeps a sword-then-gun-then-sword
	# swap sequence from ever leaving a stale _holding_gun=true behind --
	# confirmed as a real gap while wiring this up: set_walking's own
	# "_holding_gun and _is_aiming" branch is checked BEFORE _wielding_sword,
	# so a stuck _holding_gun would have shown the gun's aim pose on an
	# equipped SWORD the moment _is_aiming also happened to still read true.
	if enabled:
		_holding_gun = false
		_is_aiming = false
		_is_shooting = false
		_wielding_bottle = false # see set_wielding_bottle's own comment -- same 3-way mutual exclusion, this direction (bottle abandoned on switching TO a sword)

## Called by weapons/pistol.gd's own _equip() right after it reparents
## itself onto get_hand_node() -- see _holding_gun's own comment for what
## this actually switches over. Same "no drop mechanic yet, but reset is
## cheap and correct regardless" reasoning as set_wielding_sword().
func set_wielding_gun(enabled: bool) -> void:
	_holding_gun = enabled
	_is_aiming = false
	_is_shooting = false
	_is_dancing = false # see punch()'s own comment -- equipping a weapon should end a dance in progress the same way any other action does
	# See set_wielding_sword's own comment on why this mirrors it exactly --
	# same mutual-exclusivity fix, other direction (gun after sword).
	if enabled:
		_wielding_sword = false
		_sword_combo_ready = false
		_sword_swing_in_progress = false
		_wielding_bottle = false # see set_wielding_bottle's own comment -- same 3-way mutual exclusion, this direction (bottle abandoned on switching TO a gun)

## Called by weapons/glass_bottle.gd's own _equip() right after it reparents
## itself onto get_hand_node() -- see _wielding_bottle's own comment for
## what this actually switches over. Same "no drop mechanic yet, but reset
## is cheap and correct regardless" reasoning set_wielding_sword()/
## set_wielding_gun() already establish, extended to a real 3-way mutual
## exclusion (sword/gun/bottle) rather than just the 2-way pair those two
## already had -- equipping a bottle must also abandon any in-progress
## sword swing/combo or gun aim/shooting state, exactly like switching
## between sword and gun already does to each other.
func set_wielding_bottle(enabled: bool) -> void:
	_wielding_bottle = enabled
	_is_dancing = false # see punch()'s own comment
	if enabled:
		_wielding_sword = false
		_sword_combo_ready = false
		_sword_swing_in_progress = false
		_combo_stage_index = 0 # see heavy_punch()'s own comment -- unarmed chain abandoned on switching TO a bottle
		_combo_window_timer = 0.0
		_holding_gun = false
		_is_aiming = false
		_is_shooting = false

## Called by wearables/clothing.gd's own _equip() BEFORE it attaches a new
## top's pieces -- frees whatever the previously-worn t-shirt/hoodie left
## behind. Can't reuse the head-wearable "look at get_head_node()'s existing
## children and free whichever matches this slot" trick (see wearable.gd's
## own _equip()): clothing pieces are scattered across SEVERAL different
## BoneAttachment3D parents (spine/arm_l/arm_r/...), not one shared parent,
## so this project tracks them here instead.
func clear_worn_top() -> void:
	for part in _worn_top_parts:
		if is_instance_valid(part):
			part.queue_free()
	_worn_top_parts.clear()
	# Whatever came off, the body comes back. This is the ONLY path a top is
	# ever removed by (see this function's own callers), so restoring here
	# means a garment that hid the body can't leave it hidden -- including
	# when it's replaced by a different top that doesn't hide anything.
	set_body_stubs_only(false)
	set_worn_arm_pose(Vector3.ZERO)

## Swaps the fused body for the stub body -- hands, feet and a collar only,
## see fused_body.gd's own stub_mesh() for what survives and how to tune it.
##
## For garments that cover the whole body. A cloak can't stop a limb poking
## through it (rigid cloth over a mesh that moves independently), so the
## covering garment removes the body underneath instead of fighting it, and
## puts back only the parts that were showing anyway.
##
## The skin is untouched, so the stubs keep deforming with the same bones,
## and the paint atlas is untouched too, so they keep the character's own
## colours -- this only changes which triangles get drawn.
func set_body_stubs_only(enabled: bool) -> void:
	if _body_mesh_instance == null or _full_body_mesh == null:
		return
	if _body_stubs_only == enabled:
		return
	_body_stubs_only = enabled
	_body_mesh_instance.mesh = FusedBody.stub_mesh(_full_body_mesh) if enabled else _full_body_mesh

func is_body_stubs_only() -> bool:
	return _body_stubs_only

## Swings the arms out/forward, for sitting them inside a garment's sleeves.
## Zero for both puts them back. clear_worn_top() resets this, so a garment
## can't leave the arms stuck in its own pose after it comes off.
##
## This moves the BODY'S GEOMETRY, not the bones. Rotating the arm bones was
## the obvious approach and it doesn't work here: the garment is skinned to
## those same bones, so the sleeve swings out with the arm and the arm is
## exactly as badly fitted as before. Instead the body's own Skin gets a
## rotation about the shoulder folded into its arm binds -- the skeleton is
## untouched, so anything else bound to it (the cloak, and its sleeves)
## stays exactly where it was.
##
## The HANDS DON'T MOVE. The rotation is full on the upper arm, partial on
## the forearm and zero on the hand, so the arm bows out into the sleeve
## while the hand stays where it was. That isn't cosmetic: a held weapon
## hangs off the hand BONE, which this never touches, so any rotation that
## carried the hand mesh with it would leave the sword floating outside the
## fist. The fused mesh's own smooth weights turn the taper into a bend
## rather than a break at the elbow.
func set_worn_arm_pose(rotation_degrees: Vector3) -> void:
	if _body_mesh_instance == null or _full_body_skin == null:
		return
	_worn_arm_pose = rotation_degrees
	if rotation_degrees.is_zero_approx():
		# Back to the SHARED baked skin -- see fused_body_cache.gd on why
		# every character normally points at the same one.
		_body_mesh_instance.skin = _full_body_skin
		return

	var origins: Dictionary = FusedBody.bone_origins()
	var skin := Skin.new()
	for i in range(FusedBody.BONES.size()):
		var bone_name: String = FusedBody.BONES[i]
		var bind := Transform3D(Basis(), -(origins[bone_name] as Vector3))
		var side := 0.0
		if bone_name.ends_with("_l"):
			side = -1.0
		elif bone_name.ends_with("_r"):
			side = 1.0
		# TAPERED along the arm, not applied whole. Rotating the entire arm
		# rigidly about the shoulder carries the HAND with it, and the hand
		# is where held weapons live -- a sword sits at the hand BONE, which
		# never moves, so the hand mesh swinging away from it puts the blade
		# outside the fist. Full turn at the shoulder, none at the hand: the
		# arm bows out into the sleeve while the hand stays exactly put.
		var share := 0.0
		if bone_name.begins_with("arm_"):
			share = 1.0
		elif bone_name.begins_with("forearm_"):
			share = ARM_POSE_FOREARM_SHARE
		elif bone_name.begins_with("hand_"):
			share = 0.0
		if side != 0.0 and share > 0.0:
			bind = bind * _arm_pose_offset(side, rotation_degrees * share)
		skin.add_bind(i, bind)
	# A fresh Skin per character, never the shared one: writing into that
	# would pose every character in the game, cloaked or not.
	_body_mesh_instance.skin = skin

## Free rotation about that side's SHOULDER, in the body's own rest space.
## All three axes, in degrees:
##   x  swings the arm FORWARD (negative swings it back)
##   y  twists it around the vertical
##   z  SPREADS it out from the body
##
## y and z mirror per side so both arms stay symmetric -- +z has to push the
## left arm left and the right arm right, which is opposite signs in world
## terms. x doesn't mirror: forward is forward for both.
func _arm_pose_offset(side: float, degrees: Vector3) -> Transform3D:
	var shoulder := Vector3(side * FusedBody.SHOULDER_X, FusedBody.SHOULDER_Y, 0.0)
	var basis := Basis(Quaternion(Vector3(0, 0, 1), deg_to_rad(degrees.z) * side)
		* Quaternion(Vector3(0, 1, 0), deg_to_rad(degrees.y) * side)
		* Quaternion(Vector3(1, 0, 0), deg_to_rad(degrees.x)))
	return Transform3D(Basis(), shoulder) * Transform3D(basis, Vector3.ZERO) * Transform3D(Basis(), -shoulder)

func get_worn_arm_pose() -> Vector3:
	return _worn_arm_pose

## Called by wearables/clothing.gd's own _equip() right after it reparents
## every piece onto its own get_bone_node() -- see clear_worn_top()'s own
## comment for why these need tracking here instead of under one shared
## parent.
func set_worn_top(parts: Array[Node3D]) -> void:
	_worn_top_parts = parts
	# Clothing is built in code, so its meshes default to layer 1 only and
	# are invisible to any camera that culls on a layer -- which is why worn
	# tops didn't show in the HUD portrait or the inventory preview. Put them
	# on the same layers the body itself uses, plus the preview layer if this
	# character is currently being previewed.
	for part in parts:
		_apply_visual_layers(part, 1 | PORTRAIT_VISUAL_LAYER
			| (PREVIEW_VISUAL_LAYER if _preview_layer_enabled else 0))

## The legs-slot counterpart to set_worn_top(). A SEPARATE list on purpose:
## trousers and a shirt are worn at the same time, so routing both through
## set_worn_top() would make putting on one strip off the other.
##
## Unlike the top slot this never hides the body -- see
## wearables/skelli_jeans.gd on why trousers don't need the stub-body
## treatment a full-length cloak does.
func set_worn_legs(parts: Array[Node3D]) -> void:
	_worn_legs_parts = parts
	for part in parts:
		_apply_visual_layers(part, 1 | PORTRAIT_VISUAL_LAYER
			| (PREVIEW_VISUAL_LAYER if _preview_layer_enabled else 0))

func clear_worn_legs() -> void:
	for part in _worn_legs_parts:
		if is_instance_valid(part):
			part.queue_free()
	_worn_legs_parts.clear()

## Adds this character to (or removes it from) PREVIEW_VISUAL_LAYER -- every
## VisualInstance3D under it, so worn clothing and the head come along
## without needing to be listed. Called by whatever is previewing it.
func set_preview_layer_enabled(enabled: bool) -> void:
	_preview_layer_enabled = enabled
	_toggle_preview_layer(self, enabled)

func _toggle_preview_layer(node: Node, enabled: bool) -> void:
	# The nameplate and speech bubble are Label3Ds, so a blanket
	# VisualInstance3D walk sweeps them in and floats them across the
	# paper-doll shot. The preview wants the character, not the world-space
	# UI stuck to them.
	if node is Label3D:
		return
	if node is VisualInstance3D:
		var vis := node as VisualInstance3D
		vis.layers = (vis.layers | PREVIEW_VISUAL_LAYER) if enabled \
			else (vis.layers & ~PREVIEW_VISUAL_LAYER)
	for child in node.get_children():
		_toggle_preview_layer(child, enabled)

func _apply_visual_layers(node: Node, layers: int) -> void:
	if node is VisualInstance3D:
		(node as VisualInstance3D).layers |= layers
	for child in node.get_children():
		_apply_visual_layers(child, layers)

## New (see player/inventory.gd's own unequip_to_slot()) -- frees whatever
## is currently parented under get_hand_node() and resets all three
## wielding flags together, the one thing set_wielding_sword()/_gun()/
## _bottle() never had to do on their own (each only ever turns ONE flag on
## while clearing the other two -- no code path before the inventory system
## ever needed "wielding nothing at all").
func clear_wielded_weapon() -> void:
	var hand := get_hand_node()
	if hand != null:
		for child in hand.get_children():
			child.queue_free()
	_wielding_sword = false
	_sword_combo_ready = false
	_sword_swing_in_progress = false
	_holding_gun = false
	_is_aiming = false
	_is_shooting = false
	_wielding_bottle = false

## New (see player/inventory.gd's own unequip_to_slot()) -- frees whichever
## child of get_head_node() currently occupies `slot` ("hat"/"mask"), the
## standalone-callable version of the "replace" side effect
## wearables/wearable.gd's own _equip() already does inline for itself.
func clear_head_slot(slot: String) -> void:
	var head := get_head_node()
	if head == null:
		return
	for child in head.get_children():
		if child.has_method("get_wearable_slot") and child.get_wearable_slot() == slot:
			child.queue_free()

## Called by player.gd whenever its OWN _is_aiming (right-click held with a
## gun equipped) edge-changes -- NOT read directly from player.gd every
## frame, since set_walking() already runs every physics frame regardless
## and just needs to know the current value, the same "caller reports state,
## this node just remembers it" pattern crouching's own comment on
## set_walking already explains. Has no visible effect at all unless
## _holding_gun is also true (see set_walking's own _holding_gun and
## _is_aiming branch) -- harmless to call regardless of whether a gun is
## actually equipped, so player.gd doesn't need its own duplicate guard.
func set_aiming(enabled: bool) -> void:
	_is_aiming = enabled

## Plays the pistol's recoil-snap clip once per shot -- called by player.gd's
## own _try_fire_gun() right alongside the actual weapons/pistol.gd fire()
## call, every time a shot successfully goes off (not on every click --
## fire() itself already gates on ammo/cooldown/reload, see its own
## comment, and a click that didn't actually fire has no recoil to show).
## No-op while not _holding_gun -- defensive, matches set_aiming()'s own
## "harmless to call regardless" reasoning.
##
## Deliberately does NOT call enter_combat_mode()'s sibling gate the fist
## punches use to pick combat_idle vs idle -- see set_walking's own
## _holding_gun branch, which falls through to the SAME idle/combat_idle
## logic already used unarmed, so leaving this alone is exactly right; a
## gunshot doesn't need any additional bookkeeping on top of what
## _is_shooting already gives it.
##
## GunShootAnim is trimmed to 0.3s (see its own preload comment) -- LONGER
## than weapons/pistol.gd's own FIRE_COOLDOWN_SECONDS gap at the pistol's
## real fire rate (~0.18s), meaning back-to-back rapid shots routinely
## retrigger this before the previous play already finished, unlike every
## other one-shot clip in this file (punch/block/etc either alternate clip
## names or have a cooldown comfortably longer than their own clip).
## Confirmed directly (a throwaway scratch check, since deleted):
## AnimationPlayer.play() silently does NOT restart a clip that's already
## the current, still-playing animation -- calling play("gun_shoot") again
## mid-clip would just let the FIRST shot's recoil keep animating,
## invisibly swallowing every shot after it. stop() first forces each shot
## to restart at frame 0 for a fresh, crisp snap every time, at the cost of
## skipping BLEND_SECONDS' own crossfade for back-to-back shots specifically
## -- an instant cut reads as sharper recoil anyway, not a downgrade.
func shoot() -> void:
	if not _holding_gun:
		return
	_is_shooting = true
	_is_dashing = false # see punch()'s own comment
	_is_blocking = false # see punch()'s own comment
	_is_dancing = false # see punch()'s own comment
	anim_player.stop()
	anim_player.play("gun_shoot", BLEND_SECONDS)

## Plays the /dance chat emote -- see player.gd's own _on_chat_message_sent,
## which intercepts a literal "/dance" chat message before it ever becomes a
## speech bubble, calling this instead. A real ~26s freestyle routine (see
## DanceAnim's own preload comment), looped via LOOP_LINEAR until something
## else takes over (see _is_dancing's own comment for the full list of what
## ends it -- movement, jumping, combat, changing weapons, ...).
func dance() -> void:
	_play_emote("dance")

## Same idea as dance(), a second /breakdance chat emote (see player.gd's
## own _on_chat_message_sent) -- a shorter (~4.6s) footwork routine, also
## looped via LOOP_LINEAR. Shares _is_dancing (not a separate flag) since
## only one emote can ever be playing at a time and both are cancelled by
## exactly the same things -- see _play_emote's own comment.
func breakdance() -> void:
	_play_emote("breakdance")

## Plays the walk-up-and-sit-down clip -- called by vehicles/car.gd right
## after it decides which seat the player is entering (see its own interact
## handling), BEFORE any of the actual possession handoff (hiding this
## character, reparenting the player, zeroing collision) happens. car.gd
## itself listens for anim_player's own animation_finished("entering_car")
## to know the exact moment to complete that handoff -- this function only
## ever starts the clip, it doesn't know or care what happens once it ends.
func play_enter_car() -> void:
	_is_car_transitioning = true
	_is_punching = false # see block()'s own comment on why this needs clearing
	_is_dashing = false # see punch()'s own comment
	_is_blocking = false # see punch()'s own comment
	_is_shooting = false # see punch()'s own comment
	_is_dancing = false # see punch()'s own comment
	_is_aiming_throw = false # see jump()'s own comment
	anim_player.stop()
	anim_player.play("entering_car")

## Mirrors play_enter_car() for the reverse clip -- called by car.gd once it
## decides to let the player out (see its own interact-while-seated
## handling), while the player is still parented under the car's own seat
## (see car.gd's own exit ordering comment for why the reparent-back-to-
## world happens AFTER this clip finishes, not before).
func play_exit_car() -> void:
	_is_car_transitioning = true
	_is_punching = false
	_is_dashing = false
	_is_blocking = false
	_is_shooting = false
	_is_dancing = false
	anim_player.stop()
	anim_player.play("exiting_car")

# --- Wall climbing (see player.gd's own climb state machine) ---

## Called every physics frame player.gd has the player attached to a wall
## and moving up (W held) -- same "called every frame, only actually
## restarts if it wasn't already showing" pattern set_walking() itself
## uses for walk/run, so holding W doesn't restart climb_up from frame
## zero on every single tick. speed_scale is reset to 1.0 here since
## hold_climb() (below) can leave it paused at 0.
func start_climb_up() -> void:
	_is_climbing = true
	_is_punching = false # see block()'s own comment on why this needs clearing
	_is_dashing = false # see punch()'s own comment
	_is_blocking = false # see punch()'s own comment
	_is_shooting = false # see punch()'s own comment
	_is_dancing = false # see punch()'s own comment
	_is_aiming_throw = false # see jump()'s own comment
	anim_player.speed_scale = 1.0
	if anim_player.current_animation != "climb_up":
		anim_player.play("climb_up", BLEND_SECONDS)

## Mirrors start_climb_up() for the downward loop (S held while attached).
func start_climb_down() -> void:
	_is_climbing = true
	_is_punching = false
	_is_dashing = false
	_is_blocking = false
	_is_shooting = false
	_is_dancing = false
	_is_aiming_throw = false
	anim_player.speed_scale = 1.0
	if anim_player.current_animation != "climb_down":
		anim_player.play("climb_down", BLEND_SECONDS)

## Attached to the wall but neither W nor S held -- freezes whichever climb
## loop was already showing (rather than switching to a dedicated held
## pose that doesn't exist) so it reads as "gripping the wall, not
## climbing" instead of visibly cycling in place. start_climb_up()/
## start_climb_down() both reset speed_scale back to 1.0 the moment real
## movement resumes.
func hold_climb() -> void:
	_is_climbing = true
	if anim_player.current_animation != "climb_up" and anim_player.current_animation != "climb_down":
		anim_player.play("climb_up", BLEND_SECONDS)
	anim_player.speed_scale = 0.0

## Called when player.gd's own climb state ends for any reason OTHER than
## reaching the top (hopping off with Space, or letting go once stamina
## runs out) -- clears _is_climbing so set_walking() takes back over the
## very next call, and resets speed_scale in case hold_climb() left it
## paused. No explicit anim_player.play() here: player.gd's own next
## set_walking() call (driven by normal falling/landing state) picks the
## right clip on its own, same as landing from an ordinary jump already
## does without this function's help.
func end_climb() -> void:
	_is_climbing = false
	anim_player.speed_scale = 1.0

## Plays the one-shot ledge-mantle clip once the top of a climbable wall is
## reached (see player.gd's own top-of-climb detection) -- a real hips
## POSITION track (see ClimbTopOutAnim's own comment) carries the visual
## rise from hanging to standing on the ledge purely through skeletal root
## motion, the same way entering_car's own seat-transition arc does,
## WITHOUT this node's own root Node3D moving during the clip. player.gd
## listens for anim_player's own animation_finished("climb_top_out")
## directly (same external-listener pattern vehicles/car.gd already
## established for entering_car/exiting_car) to know the exact moment to
## snap the real physics body up onto the ledge and resume normal control.
func play_climb_top_out() -> void:
	_is_climbing = true
	_is_punching = false
	_is_dashing = false
	_is_blocking = false
	_is_shooting = false
	_is_dancing = false
	_is_aiming_throw = false
	anim_player.speed_scale = 1.0
	anim_player.stop()
	anim_player.play("climb_top_out")

# --- Steering-wheel hand overlay (see vehicles/car.gd's own driver-input
# handling, which calls set_driving_wheel_angle every physics tick while a
# real driver is seated). entering_car is LOOP_NONE (see play_enter_car's
# own comment) -- once it finishes, anim_player just holds its own last
# frame indefinitely (nothing else calls .play() on it while
# player.gd's own _seated_car gate blocks the usual set_walking() calls),
# which is already a reasonable seated/hands-near-wheel pose on its own.
# This layers a small ADDITIVE roll on top of THAT frozen pose for each
# hand bone, directly on the skeleton (bypassing anim_player entirely,
# same "reach past the AnimationPlayer for a live procedural tweak"
# approach dev_console.gd's own /gun shootpose already uses), scaled by how
# far the wheel is currently turned -- gives a real, continuously
# responsive "hands turning the wheel" read without needing a dedicated
# recorded animation for every possible steering angle. The base pose is
# captured ONCE per drive (see _driving_wheel_base_captured) rather than
# re-read every call, so repeatedly calling this with a changing angle
# rotates FROM that one fixed reference each time instead of compounding
# drift onto whatever the previous call already left the bone at.
const DRIVING_HAND_TURN_DEGREES := 30.0
var _driving_wheel_base_hand_l := Quaternion.IDENTITY
var _driving_wheel_base_hand_r := Quaternion.IDENTITY
var _driving_wheel_base_captured := false

## angle/max_angle are in the SAME units car.gd's own _current_wheel_angle
## already uses (radians, signed) -- fraction = angle/max_angle so full
## lock either direction maps to +-DRIVING_HAND_TURN_DEGREES regardless of
## this particular car's own turnRate tuning. Rotation axis/sign is a
## first-pass guess (no reference "hands on a wheel" pose exists to
## measure against, unlike the sword/gun grips' own mocap-measured
## constants) -- meant to be corrected the same hands-on way those were,
## once actually seen turning in-game.
func set_driving_wheel_angle(angle: float, max_angle: float) -> void:
	var hand_l_idx: int = FusedBody.BONES.find("hand_l")
	var hand_r_idx: int = FusedBody.BONES.find("hand_r")
	if not _driving_wheel_base_captured:
		_driving_wheel_base_hand_l = _body_skeleton.get_bone_pose_rotation(hand_l_idx)
		_driving_wheel_base_hand_r = _body_skeleton.get_bone_pose_rotation(hand_r_idx)
		_driving_wheel_base_captured = true
	var fraction: float = clampf(angle / max_angle, -1.0, 1.0) if max_angle > 0.0001 else 0.0
	var offset := Quaternion(Vector3.RIGHT, deg_to_rad(DRIVING_HAND_TURN_DEGREES) * fraction)
	_body_skeleton.set_bone_pose_rotation(hand_l_idx, _driving_wheel_base_hand_l * offset)
	_body_skeleton.set_bone_pose_rotation(hand_r_idx, _driving_wheel_base_hand_r * offset)

## Called once when a drive ends (see vehicles/car.gd's own _complete_exit)
## so the NEXT drive re-captures a fresh base pose instead of layering onto
## whatever roll offset the last drive happened to leave behind.
func clear_driving_wheel_pose() -> void:
	_driving_wheel_base_captured = false

## Shared by dance()/breakdance() -- both are "loop an emote clip until
## something else takes over" in exactly the same way, just a different
## clip name. No-op while airborne -- dancing/breakdancing mid-jump doesn't
## read as anything sensible, and jump()'s own animation already owns
## anim_player at that point anyway. Clears every other one-shot flag the
## same defensive way block()/jump() already do, in case an emote command
## gets typed while one of those was still mid-swing -- otherwise this
## would visually cut straight to the emote (play() below doesn't check any
## of them) while silently leaving the interrupted one stuck "in progress"
## forever, the exact bug class this file has already had to fix several
## times over.
func _play_emote(anim_name: String) -> void:
	if _is_jumping:
		return
	_is_dancing = true
	_is_punching = false # see block()'s own comment on why this needs clearing
	_is_dashing = false # see punch()'s own comment
	_is_blocking = false # see punch()'s own comment
	_is_shooting = false # see punch()'s own comment
	_is_aiming_throw = false # see jump()'s own comment
	_sword_swing_in_progress = false # see jump()'s own comment
	anim_player.play(anim_name, BLEND_SECONDS)

## The head MESH's own current visual center in world space -- NOT the same
## thing as get_head_node().global_position, which is the neck-top JOINT
## the head bone pivots around (see FusedBody.bone_origins()'s own "head"
## entry). The head's mesh itself sits OFFSET from that pivot (see
## _define_parts's own offset comment, half the head's height, so it sits
## ON TOP of the neck rather than centered on the joint) -- callers that
## want to frame/aim at the actual visible head (e.g. hud.gd's portrait
## camera) need this, not the raw joint position.
func get_head_center_world() -> Vector3:
	var offset: Vector3 = _parts["head"]["offset"]
	return _head_pivot.global_transform * offset

func get_character_name() -> String:
	return _character_name

func set_character_name(new_name: String) -> void:
	_character_name = new_name

## Vertical midpoint of the torso, for the character creator to orbit
## around (see character_creator.gd) -- the torso no longer has its own
## pivot node to read a position from now that it's part of the fused body.
func get_torso_center_y() -> float:
	return (FusedBody.HIP_Y + FusedBody.SHOULDER_Y) / 2.0

## Swaps the head's mesh shape (see VALID_HEAD_SHAPES) and rebuilds just the
## head to match (the fused body is unaffected -- see _build_head). The
## head's own atlas row is cleared back to white first, since old paint was
## mapped to the PREVIOUS shape's UV layout and would look like random noise
## under the new one; the other parts' rows (and their existing paint) are
## untouched. This is deliberately NOT part of the undo/redo history --
## undo/redo only tracks pixel edits, and mixing a mesh-topology change into
## that stack risks restoring paint pixels that don't match whatever shape
## is current.
func set_head_shape(shape: String) -> void:
	if shape == _head_shape or shape not in VALID_HEAD_SHAPES:
		return
	_head_shape = shape
	_clear_head_atlas_row()
	_build_head()

## Swaps the hairstyle (see VALID_HAIR_STYLES, "none" = bald) and rebuilds
## just the hair mesh (see _build_hair()) -- unlike set_head_shape(), no
## atlas/paint bookkeeping is involved: hair has no paintable UVs of its own
## yet (see HAIR_COLOR's own comment on why customization is future work).
func set_hair_style(style: String) -> void:
	if style == _hair_style or style not in VALID_HAIR_STYLES:
		return
	_hair_style = style
	_build_hair()

func get_height_scale() -> float:
	return _height_scale

## How tall this character is, as a uniform multiple of the rig's own
## authored size (see MIN/MAX_HEIGHT_SCALE) -- the character creator's own
## height slider, persisted per saved character.
##
## Deliberately a UNIFORM scale on this node rather than a Y-only stretch:
## the body is one baked, skinned mesh (see fused_body.gd) whose bone
## origins, hand/head attachment points and paint UV mapping all assume the
## rig's own proportions, so squashing one axis alone would distort the
## whole model (and every held item and hairstyle riding a bone with it)
## rather than reading as a shorter person. Nothing else in this file ever
## writes `scale`, so owning it outright here is safe.
##
## Two things measured off the rig have to follow the scale to stay
## correct: the ragdoll's own capsule radii (see _build_ragdoll, which
## already gets scaled LENGTHS for free by transforming its endpoints
## through global_transform) and the third-person camera pivot's own height
## (player.gd's own sync_camera_to_character_height, called by whoever
## applies this).
func set_height_scale(height_scale: float) -> void:
	_height_scale = clampf(height_scale, MIN_HEIGHT_SCALE, MAX_HEIGHT_SCALE)
	scale = Vector3.ONE * _height_scale

func _clear_head_atlas_row() -> void:
	var head_row := PART_NAMES.find("head")
	var y0 := head_row * ATLAS_CELL
	var y1 := y0 + ATLAS_CELL
	for y in range(y0, y1):
		for x in range(atlas_size):
			atlas_image.set_pixel(x, y, Color.WHITE)
	_flush_atlas_texture()

## Pushes the CPU-side atlas_image's current pixel data to the GPU-side
## atlas_texture, regenerating mipmaps first. TEXTURE_FILTER_LINEAR_WITH_
## MIPMAPS (see _build_atlas) needs an actually up to date mip chain, not
## just the base level -- without this, a minified/anisotropically-scaled
## view of a freshly painted stroke would sample STALE (blank) mip data
## instead of what was just painted, which reads as exactly the kind of
## aliasing artifact that made a single, genuinely round brush stamp (see
## paint_at's own anisotropic correction) look like a stretched vertical
## sliver from a normal viewing distance, while the SAME stamp looked
## properly round zoomed in close (where mip level 0 alone gets sampled) --
## confirmed directly by comparing both views of the identical paint data.
func _flush_atlas_texture() -> void:
	atlas_texture.update(atlas_image)

## Casts a ray (in world space) against this character's own triangle data
## and returns {"local_pos": Vector3, "face_index": int} for the closest
## hit, or {} if nothing was hit. Done as plain CPU math against the node's
## current global_transform -- see the class comment for why this doesn't
## use the physics engine.
func raycast_paint(world_origin: Vector3, world_dir: Vector3, max_distance: float) -> Dictionary:
	if not _paintable:
		return {}

	var inv := global_transform.affine_inverse()
	var local_origin: Vector3 = inv * world_origin
	var local_dir: Vector3 = (inv.basis * world_dir).normalized()

	var closest_t := max_distance
	var closest_face := -1
	var num_tris := tri_positions.size() / 3
	for i in range(num_tris):
		var idx := i * 3
		var t = _ray_triangle_intersect(local_origin, local_dir, tri_positions[idx], tri_positions[idx + 1], tri_positions[idx + 2])
		if t != null and t < closest_t:
			closest_t = t
			closest_face = i

	if closest_face < 0:
		return {}
	return {"local_pos": local_origin + local_dir * closest_t, "face_index": closest_face}

## Standard Moeller-Trumbore ray-triangle intersection, in whatever space
## the inputs are already in. Returns the hit distance along ray_dir (which
## must be normalized), or null if the ray misses. Naturally double-sided
## (no backface culling), which is what we want here.
func _ray_triangle_intersect(ray_origin: Vector3, ray_dir: Vector3, v0: Vector3, v1: Vector3, v2: Vector3) -> Variant:
	const EPSILON := 0.0000001
	var edge1 := v1 - v0
	var edge2 := v2 - v0
	var h := ray_dir.cross(edge2)
	var a := edge1.dot(h)
	if absf(a) < EPSILON:
		return null
	var f := 1.0 / a
	var s := ray_origin - v0
	var u := f * s.dot(h)
	if u < 0.0 or u > 1.0:
		return null
	var q := s.cross(edge1)
	var v := f * ray_dir.dot(q)
	if v < 0.0 or u + v > 1.0:
		return null
	var t := f * edge2.dot(q)
	if t > EPSILON:
		return t
	return null

## Mirrors a LOCAL-space point across the character's own left-right
## (sagittal) plane -- the rig is built perfectly symmetric across local
## X=0 (see FusedBody.limb_defs()'s own arm_l/arm_r and leg_l/leg_r pairs,
## which differ ONLY by negated X, everything else identical), so negating
## X alone maps any point on one side of the body to the anatomically
## corresponding point on the other, regardless of which limb it's on.
func mirror_local_point(local_pos: Vector3) -> Vector3:
	return Vector3(-local_pos.x, local_pos.y, local_pos.z)

## Finds which face a point already known to lie ON (or extremely near) the
## mesh surface belongs to, by testing each triangle's own barycentric
## projection of the point rather than a ray intersection -- used by
## character_creator.gd's own mirror-paint mode to locate the correct face/
## UV for a MIRRORED point (see mirror_local_point) without needing to
## understand this mesh's own UV formula at all: mirroring the 3D position
## and re-locating it on the surface naturally lands on the anatomically
## mirrored spot, whatever UV that spot happens to have (torso/head/neck
## paint straight back onto themselves at the reflected point; arm_l/thigh_l
## points land on arm_r/thigh_r and vice versa). Returns -1 if the point
## isn't actually on (or very near) any triangle -- defensive, shouldn't
## happen for a point mirrored from a real hit on this same symmetric mesh.
func find_face_for_point(point: Vector3) -> int:
	const EPS := 0.02
	var best_face := -1
	var best_dist := INF
	var num_tris := tri_positions.size() / 3
	for i in range(num_tris):
		var idx := i * 3
		var a: Vector3 = tri_positions[idx]
		var b: Vector3 = tri_positions[idx + 1]
		var c: Vector3 = tri_positions[idx + 2]
		var w := _barycentric_weights(point, a, b, c)
		if w.x < -EPS or w.y < -EPS or w.z < -EPS:
			continue
		var plane_point: Vector3 = a * w.x + b * w.y + c * w.z
		var dist := point.distance_squared_to(plane_point)
		if dist < best_dist:
			best_dist = dist
			best_face = i
	return best_face

## Same core barycentric-weight math _barycentric_uv already uses (see its
## own comment), just returning the raw (u,v,w) weights themselves instead
## of applying them to a pair of UVs -- find_face_for_point's own "is this
## point actually inside this triangle" containment test needs the weights
## directly, not an interpolated UV.
func _barycentric_weights(p: Vector3, a: Vector3, b: Vector3, c: Vector3) -> Vector3:
	var v0 := b - a
	var v1 := c - a
	var v2 := p - a
	var d00 := v0.dot(v0)
	var d01 := v0.dot(v1)
	var d11 := v1.dot(v1)
	var d20 := v2.dot(v0)
	var d21 := v2.dot(v1)
	var denom := d00 * d11 - d01 * d01
	if absf(denom) < 0.000001:
		return Vector3(1, 0, 0)
	var v := (d11 * d20 - d01 * d21) / denom
	var w := (d00 * d21 - d01 * d20) / denom
	var u := 1.0 - v - w
	return Vector3(u, v, w)

## local_pos: hit position in this node's local space. face_index: from
## raycast_paint().
func paint_at(local_pos: Vector3, face_index: int, color: Color, brush_radius_px: int) -> void:
	var idx := face_index * 3
	if idx < 0 or idx + 2 >= tri_positions.size():
		return

	var a: Vector3 = tri_positions[idx]
	var b: Vector3 = tri_positions[idx + 1]
	var c: Vector3 = tri_positions[idx + 2]
	var uva: Vector2 = tri_uvs[idx]
	var uvb: Vector2 = tri_uvs[idx + 1]
	var uvc: Vector2 = tri_uvs[idx + 2]

	var uv := _barycentric_uv(local_pos, a, b, c, uva, uvb, uvc)
	# A fused-body triangle straddling the theta wrap seam stores u values
	# "unwrapped" past [0,1) on purpose (see FusedBody._fix_theta_seam) so
	# interpolation across it stays continuous -- wrap it back into [0,1)
	# here before turning it into a pixel column, or a barycentric result
	# like u=1.02 would compute a pixel column past the atlas's right edge
	# and simply fail to paint instead of landing at the correct column.
	uv.x = fposmod(uv.x, 1.0)

	# A brush stamp that's a circle in flat ATLAS PIXEL space comes out
	# visibly OVAL on the actual 3D surface, because the atlas doesn't
	# scale U (circumference) and V (length) by the same amount -- every
	# part row gets a fixed 64px height regardless of that limb's actual
	# length, while the row's WIDTH covers its full circumference, which
	# varies with radius. Stretching the stamp into the matching ellipse in
	# atlas-pixel space (using this triangle's own local UV gradient,
	# compared against the torso's own reference) is what makes it land as
	# an actual circle on the body instead.
	#
	# base_r floors at 0.5 (not 0) specifically so brush size 1 (base_r would
	# otherwise be exactly 0) still gets a real, if small, anisotropic
	# correction instead of skipping this entire calculation -- an earlier
	# version hardcoded brush 1 to an isotropic (0.5, 0.5) unconditionally,
	# which meant it never compensated for local UV distortion at all.
	# Confirmed as a real bug on the sphere head specifically: near the top
	# (where an equirectangular UV wrap is most compressed -- the same
	# distortion a Mercator map shows near the poles), brush 1 dots rendered
	# as visible diamonds/lenses instead of circles, precisely because
	# nothing was correcting for that projection there. brush_radius_px=2
	# still maps to base_r=1.0 exactly as before (maxf is a no-op once
	# brush_radius_px-1 already exceeds 0.5), so this doesn't change
	# anything for larger brushes.
	var base_r: float = maxf(float(brush_radius_px) - 1.0, 0.5)
	var tangent_lens := _local_uv_tangent_lengths(a, b, c, uva, uvb, uvc)
	var raw_r_u: float = base_r * _reference_brush_len / tangent_lens.x
	var raw_r_v: float = base_r * _reference_brush_len / tangent_lens.y
	# Cap how far the two axes are allowed to diverge from each other.
	# Directly measured several body regions' own raw (uncapped) ratios to
	# pick this rather than guessing: torso ~3.9:1 (its own natural
	# proportions -- this cap must clear it, or torso paint flattens into an
	# oval, confirmed directly as a real regression at an earlier, lower
	# value of 1.8), the neck/shoulder junction ~6:1, the sphere head near
	# its own pole ~8.4:1 (both of these driven by FusedBody's own
	# near-zero-length NECK_LEN and the equirectangular head wrap
	# respectively -- genuine extreme local anisotropy, not measurement
	# noise), and the arm ~12.6:1. Fully uncapping (tried directly) clears
	# the torso correctly but lets the neck/head cases stretch into
	# visibly-elongated ellipses instead of reading as a brush at all. 4.0
	# clears the torso with essentially no capping (3.9 < 4.0) while
	# meaningfully reining in the more extreme spots -- not a perfect circle
	# there (impossible without more fundamental atlas-layout changes, since
	# those regions are GENUINELY compressed far more than the atlas's own
	# fixed-height rows account for), but a much rounder result than either
	# the old 1.8 cap or no cap at all. Purely a safety net against a truly
	# degenerate triangle producing an unbounded ellipse beyond this.
	# The 3.0 cap above exists because this correction is a LOCAL LINEAR
	# (tangent-plane) approximation of what's actually a non-linear
	# compression (worst right at a pole or the near-zero-length neck) --
	# extrapolated across a BIG brush's whole footprint, that approximation
	# increasingly diverges from the real compression the farther out you
	# get from the one triangle it was measured on, so capping protects
	# against a big brush overcorrecting into a huge, obviously-wrong
	# sliver. But that divergence only matters when the footprint is big
	# enough to reach past where the local approximation stays accurate --
	# at brush size 1-2 (base_r near its 0.5 floor), the WHOLE stamp is
	# under 1 atlas pixel, well inside that radius, so the raw (uncapped)
	# correction is essentially exact. Verified directly against the real
	# reproduced head-pole tap (raw ratio 3.48:1, raw_r_u=0.805,
	# raw_r_v=0.231): multiplying each by its own axis's world-per-pixel
	# distance gives 0.004211 vs 0.004206 -- equal to within 0.1%, i.e.
	# genuinely round in world space, not the elongated diamond the flat
	# 3.0 cap was producing by throwing that correction away. So loosen the
	# cap toward "effectively uncapped" as base_r shrinks toward its floor,
	# and only bring it down to the original 3.0 once base_r is large
	# enough (>=3.0, brush size ~4+) that the big-brush overcorrection risk
	# the cap was actually guarding against becomes real again.
	var max_aspect_ratio: float = lerp(30.0, 3.0, clampf((base_r - 0.5) / 2.5, 0.0, 1.0))
	var r_u: float = raw_r_u
	var r_v: float = raw_r_v
	if raw_r_u > raw_r_v * max_aspect_ratio:
		r_u = raw_r_v * max_aspect_ratio
	elif raw_r_v > raw_r_u * max_aspect_ratio:
		r_v = raw_r_u * max_aspect_ratio
	# A RATIO cap alone still lets the two axes drift arbitrarily far apart
	# in ABSOLUTE atlas pixels once the brush itself is big -- confirmed
	# directly at the slider's own max size (15): even fully inside the 3:1
	# ratio cap, the larger axis came out ~9 atlas pixels bigger than the
	# smaller one at a region with elevated local anisotropy, clearly
	# reading as an oversized, obviously-elongated blob rather than a big
	# circle. Also cap the raw DIFFERENCE between the two axes, independent
	# of ratio, so a big brush stays proportioned instead of scaling its own
	# distortion right along with its size.
	const MAX_AXIS_DIFF_PX := 4.0
	if r_u - r_v > MAX_AXIS_DIFF_PX:
		r_u = r_v + MAX_AXIS_DIFF_PX
	elif r_v - r_u > MAX_AXIS_DIFF_PX:
		r_v = r_u + MAX_AXIS_DIFF_PX
	r_u = clampf(r_u, 0.5, base_r * 6.0)
	r_v = clampf(r_v, 0.5, base_r * 6.0)

	_stamp_bridged(uv, color, r_u, r_v, tri_clamp[face_index])

## World-space distance covered by one atlas pixel of U and one atlas pixel
## of V, for this specific triangle -- the standard tangent/bitangent
## derivation (same math used for normal-mapping): solves for the world-
## space directions T (per-U) and B (per-V) such that
## edge1 = uvEdge1.x*T + uvEdge1.y*B and edge2 = uvEdge2.x*T + uvEdge2.y*B,
## then returns their lengths. Falls back to the reference (torso) lengths
## for a degenerate triangle (near-zero UV area, or a near-zero edge in one
## direction) rather than dividing by ~0.
func _local_uv_tangent_lengths(a: Vector3, b: Vector3, c: Vector3, uva: Vector2, uvb: Vector2, uvc: Vector2) -> Vector2:
	var edge1 := b - a
	var edge2 := c - a
	var atlas_px := Vector2(atlas_size, atlas_height)
	var uv_edge1 := (uvb - uva) * atlas_px
	var uv_edge2 := (uvc - uva) * atlas_px
	var det := uv_edge1.x * uv_edge2.y - uv_edge2.x * uv_edge1.y
	if absf(det) < 0.000001:
		return Vector2(_reference_tangent_len, _reference_bitangent_len)

	var tangent: Vector3 = (edge1 * uv_edge2.y - edge2 * uv_edge1.y) / det
	var bitangent: Vector3 = (edge2 * uv_edge1.x - edge1 * uv_edge2.x) / det
	var t_len := tangent.length()
	var b_len := bitangent.length()
	if t_len < 0.000001:
		t_len = _reference_tangent_len
	if b_len < 0.000001:
		b_len = _reference_bitangent_len
	return Vector2(t_len, b_len)

## Paint-bucket tool: flood-fills the contiguous same-color region touching
## the clicked point with `color`, same hit data as paint_at.
func bucket_fill_at(local_pos: Vector3, face_index: int, color: Color) -> void:
	var idx := face_index * 3
	if idx < 0 or idx + 2 >= tri_positions.size():
		return

	var uv := _barycentric_uv(
		local_pos,
		tri_positions[idx], tri_positions[idx + 1], tri_positions[idx + 2],
		tri_uvs[idx], tri_uvs[idx + 1], tri_uvs[idx + 2]
	)
	uv.x = fposmod(uv.x, 1.0)

	_flood_fill(uv, color, tri_clamp[face_index])

## Reads back whatever color is currently painted at a raycast hit --
## character_creator.gd's own eyedropper tool uses this to let the player
## pick up an already-painted color instead of hand-matching it in the
## color picker. Reuses the exact same local_pos -> UV -> atlas-pixel
## mapping bucket_fill_at/paint_at already use, so "the color you click" and
## "the color that's actually there" can never disagree.
func sample_color_at(local_pos: Vector3, face_index: int) -> Color:
	var idx := face_index * 3
	if idx < 0 or idx + 2 >= tri_positions.size():
		return Color.WHITE

	var uv := _barycentric_uv(
		local_pos,
		tri_positions[idx], tri_positions[idx + 1], tri_positions[idx + 2],
		tri_uvs[idx], tri_uvs[idx + 1], tri_uvs[idx + 2]
	)
	uv.x = fposmod(uv.x, 1.0)

	var px := clampi(int(uv.x * atlas_size), 0, atlas_size - 1)
	var py := clampi(int(uv.y * atlas_height), 0, atlas_height - 1)
	return atlas_image.get_pixel(px, py)

func _barycentric_uv(p: Vector3, a: Vector3, b: Vector3, c: Vector3, uva: Vector2, uvb: Vector2, uvc: Vector2) -> Vector2:
	var v0 := b - a
	var v1 := c - a
	var v2 := p - a
	var d00 := v0.dot(v0)
	var d01 := v0.dot(v1)
	var d11 := v1.dot(v1)
	var d20 := v2.dot(v0)
	var d21 := v2.dot(v1)
	var denom := d00 * d11 - d01 * d01
	if absf(denom) < 0.000001:
		return uva
	var v := (d11 * d20 - d01 * d21) / denom
	var w := (d00 * d21 - d01 * d20) / denom
	var u := 1.0 - v - w
	return uva * u + uvb * v + uvc * w

## Call once when a new stroke starts (mouse button just went down), so the
## first stamp of that stroke doesn't get bridged (see _stamp_bridged) back
## to wherever an earlier, separate stroke last painted.
func begin_paint_stroke() -> void:
	_last_stamp_uv = Vector2(-1.0, -1.0)
	_last_stamp_clamp = {}

## Stamps at uv, and if this continues the same stroke on the same body part
## as the last stamp, first fills in a line of intermediate stamps back to
## it. Consecutive raycast hits along a drag can land much farther apart in
## ATLAS space than the on-screen mouse movement suggests: the body mesh is
## a fairly low-poly SDF extraction, and on a thin limb like the arm each
## facet covers a bigger slice of the circumference, so the exact hit point
## can hop between neighboring facets (jumping several atlas pixels around
## the U/circumference axis) even for a one-pixel screen movement. Left
## unbridged, that reads as a row of disconnected dabs instead of a flowing
## stroke -- capping the stamp's own aspect ratio (see paint_at) alone can't
## fix it, since the gap is between stamps, not within one.
func _stamp_bridged(uv: Vector2, color: Color, radius_u_px: float, radius_v_px: float, clamp: Dictionary) -> void:
	var stamp_uv := uv
	if _last_stamp_uv.x >= 0.0 and _last_stamp_clamp == clamp:
		# U wraps at the theta seam (0/1) -- go the short way around rather
		# than the long way, or a stroke crossing the seam would bridge
		# almost all the way around the limb instead of across the seam.
		var du := fposmod(uv.x - _last_stamp_uv.x + 0.5, 1.0) - 0.5
		var dv := uv.y - _last_stamp_uv.y

		# Low-pass filter (EMA) the raw hit toward last_stamp_uv rather than
		# jumping straight to it, weighted per-axis by that axis's own
		# radius. Real raycasts against the fused body's marching-tetrahedra
		# surface aren't perfectly smooth even along a dead-straight screen-
		# space drag -- consecutive hits land on different facets whose own
		# tiny normal variation shifts the hit point by roughly 1-2 atlas
		# pixels off the intended axis (confirmed directly: sweeping real
		# camera rays across the torso at a fixed screen height, holding
		# that axis constant, still produced a +/-1-2px oscillating sequence
		# like 83,82,84,83,83,82,83,82 -- pure sensor noise, not the user
		# moving). A brush with real overlap between consecutive stamps
		# (radius several pixels) absorbs that invisibly; brush size 1
		# (radius as low as 0.5px) has essentially none, so the same noise
		# reads as a jagged zigzag line instead of a straight one. Weighting
		# by radius means a small brush (least overlap margin to spare)
		# keeps only a small fraction of each new sample (heavy smoothing),
		# while a large brush (which doesn't need the help) keeps nearly all
		# of it, so this doesn't add perceptible lag once brushes are big
		# enough to already tolerate the noise on their own. Verified by
		# hand against the measured noise sequence above: alpha=0.25 (this
		# formula's value at brush size 1's 0.5px radius) collapses that
		# whole 82-84 oscillation down to a flat, constant row.
		var alpha_u: float = clampf(radius_u_px / 2.0, 0.15, 1.0)
		var alpha_v: float = clampf(radius_v_px / 2.0, 0.15, 1.0)
		stamp_uv = Vector2(fposmod(_last_stamp_uv.x + du * alpha_u, 1.0), _last_stamp_uv.y + dv * alpha_v)

		var bridge_du := fposmod(stamp_uv.x - _last_stamp_uv.x + 0.5, 1.0) - 0.5
		var bridge_dv := stamp_uv.y - _last_stamp_uv.y
		var dist_px := Vector2(bridge_du * atlas_size, bridge_dv * atlas_height).length()
		# Step spacing, and how many steps that spacing implies:
		#
		# step_px floors at 0.5 (not the old 1.0) -- at brush size 1
		# (min_radius=0.5), _stamp()'s own tightened AA fringe (see its own
		# comment) now means a stamp only ever paints the SINGLE integer
		# atlas pixel it lands on, with no soft reach into its neighbors at
		# all (confirmed by hand: at radius 0.5/taper 0.4, even the adjacent
		# pixel one whole atlas-pixel away already falls outside the 1.0+taper
		# falloff radius). A flat 1.0px step spacing relied on the OLD flat
		# 1px taper's oversized, always-overlapping fringe to invisibly paper
		# over any rounding gap between consecutive stamps' own truncated
		# integer pixel -- crisping that fringe up removed the insurance
		# without anything replacing it, so two real, isolated single-pixel
		# dots exactly 1.0-ish px apart could land on two DIFFERENT integer
		# pixels with a genuinely untouched pixel between them and no third
		# stamp ever placed there. Confirmed directly as a real, reported
		# case: a real brush-painted ring at size 1, scanned right at its own
		# boundary, showed several pixels still the untouched background
		# color, not just a partial AA blend. Halving the floor to 0.5
		# doubles the sampling density along the path specifically for small
		# brushes (larger brushes already had real overlap and don't need
		# this), closing that rounding gap.
		#
		# steps now rounds UP (ceil), not down (int/floor). The old
		# int(dist_px / step_px) throws away the fractional remainder --
		# e.g. dist_px=1.05, step_px=1.0 gives steps=1, and range(1, 1) is
		# EMPTY, placing zero intermediate stamps for a gap that's still a
		# full pixel wide. Rounding up guarantees at least enough steps to
		# actually span the measured distance instead of silently
		# undercounting by up to one whole step on every bridge.
		var step_px: float = maxf(0.5, minf(radius_u_px, radius_v_px) * 0.6)
		var steps := maxi(1, int(ceil(dist_px / step_px)))
		for i in range(1, steps):
			var t := float(i) / float(steps)
			var bridge_uv := Vector2(fposmod(_last_stamp_uv.x + bridge_du * t, 1.0), _last_stamp_uv.y + bridge_dv * t)
			_stamp(bridge_uv, color, radius_u_px, radius_v_px, clamp)

	_stamp(stamp_uv, color, radius_u_px, radius_v_px, clamp)
	_last_stamp_uv = stamp_uv
	_last_stamp_clamp = clamp

## Stamps a filled ellipse of `color` into the atlas, clamped hard to the
## triangle's own atlas-cell bounds. Without this clamp, a brush stroke
## near a face's edge would bleed into the neighboring cell in the atlas,
## which is a *different* face (possibly on a different body part) --
## painting one spot would visibly "leak" onto unrelated geometry.
##
## radius_u_px/radius_v_px are the FINAL per-axis pixel radii (already
## anisotropy-corrected by the caller, see paint_at) -- 0.5 for both means
## "just the single center pixel". A hard elliptical inside/outside test
## reads as a diamond, not a circle, at small pixel radii (there aren't
## enough distinct rows for a binary decision to show curvature), so pixels
## fully inside get full coverage while one extra ring gets blended toward
## the brush color in proportion to how much of the ideal ellipse actually
## covers them -- the same anti-aliasing trick a real paint tool's brush
## uses to read as round even when tiny.
func _stamp(uv: Vector2, color: Color, radius_u_px: float, radius_v_px: float, clamp: Dictionary) -> void:
	var cell_x0: int = clamp["x0"]
	var cell_x1: int = clamp["x1"]
	# Godot's 3D texture sampling maps UV v=0 to the TOP row of the source
	# image (same order as Image pixel rows, no vertical flip) -- so the
	# pixel band for a given atlas row must NOT be inverted here. An
	# earlier version of this code assumed a flip that didn't match the
	# renderer's actual convention, which silently wrote every stroke into
	# the mirror-opposite row of the shared atlas -- i.e. paint landing on
	# a completely different body part than the one clicked.
	var cell_y0: int = clamp["y0"]
	var cell_y1: int = clamp["y1"]

	var px := int(uv.x * atlas_size)
	var py := int(uv.y * atlas_height)
	# One pixel's worth of softening, expressed in the ellipse's own
	# normalized distance units (where 1.0 = right at the boundary) -- taken
	# from the SMALLER of the two radii (minf), not the larger, so the more
	# tightly constrained axis always gets a proper, generously-sized ~1px
	# soft edge of its own. Using maxf here was a real bug, not just a style
	# choice: on an anisotropic stamp (any triangle where the atlas's U and V
	# axes don't scale the same, e.g. the torso) the SMALLER axis's edge got
	# tapered by the LARGER axis's own (much tighter, since taper shrinks as
	# radius grows) value instead of its own -- confirmed directly as the
	# cause of a real regression where brush size 2 (whose smaller axis
	# lands barely above the 0.5px floor, same ballpark as size 1's own flat
	# 0.5/0.5) painted a visibly SMALLER footprint than brush size 1 (9
	# touched atlas pixels vs 5), the opposite of what a bigger brush should
	# do.
	# AA fringe width in real atlas pixels -- NOT a flat 1.0px regardless of
	# brush size (an earlier version of this line, "1.0 / min_radius"
	# directly). Confirmed as a real reported bug: at brush size 1
	# (min_radius floors at 0.5, see paint_at's own base_r), a flat 1px
	# fringe extends the falloff all the way out to 1.5px in every
	# direction -- fully 3x the intended "1 pixel" footprint -- reading as
	# a soft, oversized blob instead of a crisp dot. Worse, that same
	# softness compounds along a hand-drawn outline into a wide gradient
	# band that bucket_fill_at's own FLOOD_FILL_TOLERANCE (a fixed distance
	# from the background color) can't fully swallow, leaving a ragged rim
	# of unfilled white speckle pixels right at the boundary -- reported
	# directly, with a screenshot showing exactly that speckled edge after
	# filling a closed outline.
	#
	# Scales from a crisp ~0.2px fringe at brush size 1-2 (min_radius near
	# its own 0.5 floor) up to the original full 1.0px once the brush is
	# big enough (min_radius >= 3.0, brush size ~4+) that a 1px fringe is
	# proportionally small rather than dominating the whole stamp -- same
	# "lerp based on how close min_radius is to its own floor" shape
	# paint_at's own max_aspect_ratio already uses, for consistency. The
	# existing 4x4 supersampled coverage sampling above (see its own
	# comment) already handles round-vs-diamond shape correctly regardless
	# of fringe width, so narrowing the fringe doesn't reintroduce that
	# earlier bug -- it only affects how QUICKLY coverage falls from 1.0 to
	# 0.0, not the shape of the falloff region.
	var min_radius: float = minf(radius_u_px, radius_v_px)
	var aa_fringe_px: float = lerp(0.2, 1.0, clampf((min_radius - 0.5) / 2.5, 0.0, 1.0))
	var taper: float = aa_fringe_px / min_radius
	# Bound the search loop off the ACTUAL falloff edge (radius + taper's own
	# extra reach), not just the raw radius -- with taper now potentially
	# larger than either radius alone (when the two axes are very different),
	# the old maxf(radius)+1 bound could clip part of the real falloff on the
	# bigger axis before it finished fading to zero.
	var max_r := int(ceil((1.0 + taper) * maxf(radius_u_px, radius_v_px))) + 1
	# Boundary pixels (partial coverage) get refined with a 4x4 sub-pixel
	# supersample average instead of a single sample at the pixel's exact
	# center. A center-point sample measures raw distance from a candidate
	# pixel's own center to the stamp's center, which is a plain grid fact:
	# the 4 orthogonal neighbor pixels are always closer to the stamp center
	# than the 4 diagonal ones. At a large brush that bias is spread across
	# dozens of boundary pixels around the rim and averages out visually, but
	# at brush size 1 (radius ~0.5px) the ENTIRE stamp is just those 9
	# pixels, all boundary -- so the bias alone becomes the whole visible
	# shape: a bright plus/cross (orthogonal neighbors ~0.5 coverage) with
	# barely-visible diagonal corners (~0.09), which reads as a diamond, not
	# a circle, and tiles into a jagged zigzag when dragged into a line.
	# Averaging a 4x4 grid of sub-samples across each candidate pixel's own
	# area (not just its center point) approximates that pixel's actual
	# fractional coverage by the stamp, which is far less biased toward the
	# 4 grid-axis directions -- confirmed by hand-computing brush size 1's
	# own numbers: the diagonal-to-orthogonal coverage ratio roughly doubles
	# (17% -> 31%), enough to read as round instead of diamond. Skipped for
	# fully-covered interior pixels (coverage already 1.0) since those need
	# no refinement, which keeps big-brush stamps just as cheap as before.
	const SS := 4
	for dy in range(-max_r, max_r + 1):
		for dx in range(-max_r, max_r + 1):
			var nx := float(dx) / radius_u_px
			var ny := float(dy) / radius_v_px
			var dist := sqrt(nx * nx + ny * ny)
			var coverage := clampf((1.0 + taper - dist) / taper, 0.0, 1.0)
			if coverage <= 0.0:
				continue
			if coverage < 1.0:
				var total := 0.0
				for sy in range(SS):
					for sx in range(SS):
						var ox: float = (float(sx) + 0.5) / SS - 0.5
						var oy: float = (float(sy) + 0.5) / SS - 0.5
						var snx := (float(dx) + ox) / radius_u_px
						var sny := (float(dy) + oy) / radius_v_px
						var sdist := sqrt(snx * snx + sny * sny)
						total += clampf((1.0 + taper - sdist) / taper, 0.0, 1.0)
				coverage = total / float(SS * SS)
				if coverage <= 0.0:
					continue
			var x := px + dx
			var y := py + dy
			if x < cell_x0 or x >= cell_x1 or y < cell_y0 or y >= cell_y1:
				continue
			var existing := atlas_image.get_pixel(x, y)
			atlas_image.set_pixel(x, y, existing.lerp(color, coverage))
	_flush_atlas_texture()

func _color_dist(a: Color, b: Color) -> float:
	return absf(a.r - b.r) + absf(a.g - b.g) + absf(a.b - b.b)

## 4-connected (orthogonal-only) flood fill -- see the neighbor-expansion
## loop's own comment below for why. Hard-clamped to the same atlas-cell
## rect _stamp uses -- without that clamp, a bucket click on a still-all-
## white atlas would fill the ENTIRE shared atlas (every body part starts
## out the same white), not just the clicked part.
func _flood_fill(uv: Vector2, color: Color, clamp: Dictionary) -> void:
	var cell_x0: int = clamp["x0"]
	var cell_x1: int = clamp["x1"]
	var cell_y0: int = clamp["y0"]
	var cell_y1: int = clamp["y1"]

	var start_x := int(uv.x * atlas_size)
	var start_y := int(uv.y * atlas_height)
	if start_x < cell_x0 or start_x >= cell_x1 or start_y < cell_y0 or start_y >= cell_y1:
		return

	var target_color := atlas_image.get_pixel(start_x, start_y)
	if target_color.is_equal_approx(color):
		return

	var stack := [Vector2i(start_x, start_y)]
	var visited := {}
	while not stack.is_empty():
		var p: Vector2i = stack.pop_back()
		if visited.has(p):
			continue
		if p.x < cell_x0 or p.x >= cell_x1 or p.y < cell_y0 or p.y >= cell_y1:
			continue
		var pixel := atlas_image.get_pixel(p.x, p.y)
		# Whether a pixel counts as still-background (fillable) is decided
		# by a fixed tolerance from target_color ALONE -- not, as an earlier
		# version of this did, by comparing distance-to-target against
		# distance-to-the-NEW-fill-color. That comparison is a real,
		# confirmed bug: it implicitly assumes the drawn outline's own color
		# sits somewhere between target_color and the new fill color, which
		# is only true by coincidence. Reported directly: a black outline
		# drawn over brown skin, filled with red, leaked through the outline
		# and painted the ENTIRE limb -- because black (0,0,0) happens to be
		# CLOSER to brown skin (~0.95 away) than red is far enough beyond it
		# (only ~1.0 away, not more than 0.95 + the old bias of 0.6), so the
		# old check never treated the black outline as a wall at all and
		# just filled straight over it, then kept flooding through. The new
		# fill color has NOTHING to do with which pixels are still part of
		# the SAME region the player clicked in -- only target_color does,
		# same as every real paint program's own "magic wand"/bucket
		# tolerance already works.
		#
		# FLOOD_FILL_TOLERANCE is deliberately generous (out of a max
		# Manhattan distance of 3.0) to swallow the anti-aliased edge a
		# brush stroke's own overlapping stamps produce (see _stamp) without
		# a visible unfilled rim, while staying comfortably below the gap
		# between typical background/fill colors and a deliberately chosen,
		# clearly-different outline color -- an outline only reads as a
		# visible line at all because it WAS chosen to look clearly
		# different from what it's drawn over.
		const FLOOD_FILL_TOLERANCE := 0.35
		if _color_dist(pixel, target_color) > FLOOD_FILL_TOLERANCE:
			continue
		visited[p] = true
		atlas_image.set_pixel(p.x, p.y, color)
		# 4-connected (orthogonal only), NOT 8-connected -- confirmed as a
		# real reported bug ("draw a black circle, click the center, it
		# fills the whole limb"): a hand-drawn curve is only diagonally
		# connected at plenty of points along its own curvature (a circle
		# has no straight runs), and an 8-connected flood can cut straight
		# through a diagonal-only wall corner by hopping between the two
		# background pixels on either side of it -- neither of which is
		# ever actually ON the drawn line, so the boundary check above never
		# gets a chance to stop it there. A solid painted interior (many
		# overlapping round brush stamps) is already fully 4-connected
		# everywhere that matters, so dropping the 4 diagonal directions
		# costs nothing there while making it topologically impossible to
		# leak through a closed line -- a straight 4-connected path can't
		# skip past a wall pixel the way a diagonal step can.
		for offset in [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]:
			stack.append(p + offset)
	_flush_atlas_texture()

## Encodes the painted atlas as PNG bytes -- small enough to ship over an
## RPC once a networked lobby exists, so a player's paint job can be sent
## to other peers instead of everyone needing to repaint locally.
func get_atlas_png() -> PackedByteArray:
	return atlas_image.save_png_to_buffer()

## Replaces the atlas with PNG bytes from get_atlas_png() (e.g. received
## from another peer over the network). Silently ignores malformed data.
func apply_atlas_png(png_bytes: PackedByteArray) -> void:
	var img := Image.new()
	if img.load_png_from_buffer(png_bytes) != OK:
		return
	if img.get_size() != Vector2i(atlas_size, atlas_height):
		return
	atlas_image = img
	_flush_atlas_texture()

## Call once, right before a paint stroke begins (i.e. on mouse-down, before
## any stamping), to record what the atlas looked like beforehand. Starting
## a new stroke always clears the redo stack -- once you paint something
## new, the old "future" no longer applies.
func push_undo_snapshot() -> void:
	_undo_stack.append(atlas_image.duplicate())
	_redo_stack.clear()
	if _undo_stack.size() > MAX_UNDO_STEPS:
		_undo_stack.pop_front()

func can_undo() -> bool:
	return not _undo_stack.is_empty()

func can_redo() -> bool:
	return not _redo_stack.is_empty()

func undo() -> void:
	if _undo_stack.is_empty():
		return
	_redo_stack.append(atlas_image.duplicate())
	atlas_image = _undo_stack.pop_back()
	_flush_atlas_texture()

func redo() -> void:
	if _redo_stack.is_empty():
		return
	_undo_stack.append(atlas_image.duplicate())
	atlas_image = _redo_stack.pop_back()
	_flush_atlas_texture()
