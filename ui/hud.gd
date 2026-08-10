extends CanvasLayer
## HUD: character portrait + level badge, health/stamina bars (WoW-style),
## and a death screen with a respawn button. Reads its stats directly from
## the parent Player node every frame (see _process) rather than via
## signals -- health/stamina change continuously (drain/regen every physics
## frame), so polling a plain value each render frame is simpler than
## wiring a signal for every tiny change; only the discrete died/respawned
## transitions use real signals.

const Character = preload("res://character/character.gd")

const HEALTH_YELLOW_FRACTION := 0.75
const HEALTH_RED_FRACTION := 0.25
const HEALTH_COLOR_GREEN := Color(0.2, 0.85, 0.2)
const HEALTH_COLOR_YELLOW := Color(0.95, 0.85, 0.15)
const HEALTH_COLOR_RED := Color(0.9, 0.15, 0.15)

# In front of the character's own face, close and level -- a head-and-a-
# little-shoulder crop like the reference mockup, not a full-body shot and
# not tilted up/down (camera height matches the head's own height, looking
# straight at it, not down at some point below it). See character.gd for
# SHOULDER_Y/head_size, which put the head center around y=2.25 and the
# shoulders around y=1.9. `+basis.z` is correct here, verified with an
# actual painted marker on the face (a blank test sphere can't tell front
# from back): a prior "fix" that negated this was wrong and showed the
# back of the head instead.
const PORTRAIT_CAMERA_DISTANCE := 0.85
# A flat, darker shade of the gold ring around the portrait (see
# StyleBoxFlat_LevelBadge/hud.tscn's own border colors) instead of
# whatever the game world's own sky/background happens to be behind the
# character -- set on the camera itself (Camera3D.environment overrides
# just what THIS camera sees) rather than swapping the whole shared
# World3D, so the character's own lighting/geometry stay exactly as they
# render in the real game.
const PORTRAIT_BG_COLOR := Color(0.26, 0.21, 0.07)

@onready var _player: CharacterBody3D = get_parent()
@onready var _health_bar: ProgressBar = $Root/StatusFrame/HealthBar
@onready var _stamina_bar: ProgressBar = $Root/StatusFrame/StaminaBar
@onready var _level_label: Label = $Root/StatusFrame/PortraitClip/LevelBadge/LevelLabel
@onready var _portrait_viewport: SubViewport = $Root/StatusFrame/PortraitClip/PortraitViewport
@onready var _portrait_camera: Camera3D = $Root/StatusFrame/PortraitClip/PortraitViewport/PortraitCamera
@onready var _portrait_image: TextureRect = $Root/StatusFrame/PortraitClip/PortraitImage
@onready var _death_screen: Control = $Root/DeathScreen
@onready var _respawn_button: Button = $Root/DeathScreen/RespawnButton
@onready var _ammo_label: Label = $Root/AmmoLabel

var _health_fill_style: StyleBoxFlat

func _ready() -> void:
	# Duplicated so recoloring this bar's fill never touches the shared
	# default StyleBoxFlat resource (which would recolor every ProgressBar
	# using it, including the stamina bar).
	_health_fill_style = _health_bar.get_theme_stylebox("fill").duplicate()
	_health_bar.add_theme_stylebox_override("fill", _health_fill_style)

	_respawn_button.pressed.connect(_on_respawn_pressed)
	_player.died.connect(_on_player_died)
	_player.respawned.connect(_on_player_respawned)

	# Shares the main game's own World3D rather than rendering an isolated
	# copy -- the portrait shows the actual character (paint job, rest
	# pose) with no duplicate geometry to keep in sync. Read off the
	# player, not the character node, deliberately: main.gd (and this
	# HUD's own test fixtures) add Character as the player's child AFTER
	# the player itself enters the tree, which is what triggers this very
	# _ready() (see player.gd's own _ready(), which instantiates this HUD)
	# -- so Character doesn't exist as a sibling yet at this point, but the
	# player itself, already in the tree, has the exact same World3D.
	_portrait_viewport.world_3d = _player.get_world_3d()
	_capture_portrait()

## The portrait is a ONE-TIME snapshot, not a live feed -- a portrait that
## kept following the character's current facing would visibly swing
## around every time the player turned, reading as the portrait itself
## "moving" rather than a still headshot. Waits a couple of frames FIRST,
## before ever looking for the character -- main.gd (and this HUD's own
## test fixtures) add Character as the player's child right after the
## player itself enters the tree, which is what triggers this very
## _ready() (see player.gd's own _ready()), so Character doesn't exist as
## a sibling yet the instant this runs. Checking immediately (as an
## earlier version of this function did) found nothing and gave up for
## good, since this only ever runs once -- waiting first instead of after
## guarantees Character has been added by the time it's checked, and
## incidentally also gives the character/SubViewport a real frame to
## render before the capture.
func _capture_portrait() -> void:
	# A SubViewport nothing displays live defaults to not bothering to
	# render at all (UPDATE_WHEN_VISIBLE, and nothing ever reads its
	# texture while it's set up) -- forcing it here is what actually makes
	# get_image() below return the rendered character instead of a blank
	# frame.
	_portrait_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS

	await get_tree().process_frame
	await get_tree().process_frame

	var character := _get_character()
	if character == null:
		return
	var head: Node3D = character.get_head_node()
	if head == null:
		return

	var portrait_env := Environment.new()
	portrait_env.background_mode = Environment.BG_COLOR
	portrait_env.background_color = PORTRAIT_BG_COLOR
	portrait_env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	portrait_env.ambient_light_color = Color.WHITE
	portrait_env.ambient_light_energy = 0.9
	_portrait_camera.environment = portrait_env
	# The Environment's background_color only fills empty sky -- sharing the
	# main game's own World3D (see _ready()'s own comment on why) means any
	# city geometry standing behind the character within the camera's frame
	# still renders normally, right over that flat color. Restricting this
	# camera to ONLY the character's own dedicated visual layer (see
	# character.gd's PORTRAIT_VISUAL_LAYER) is what actually keeps the
	# portrait to just the character with the flat color showing everywhere
	# else, instead of the city bleeding through behind them.
	_portrait_camera.cull_mask = Character.PORTRAIT_VISUAL_LAYER

	# NOT head.global_position -- that's the neck-top JOINT the head bone
	# pivots around now that "head" is a real skeleton bone (see
	# get_head_center_world's own comment), not the head mesh's own visual
	# center. Using the joint directly here aimed/framed the portrait a full
	# half-head-height too low, showing the neck's own (much thinner) gap in
	# the middle of the frame instead of the head.
	var head_pos: Vector3 = character.get_head_center_world()
	var facing := character.global_transform.basis.z.normalized()
	# Backed off in proportion to how tall this character was made (see
	# character.gd's own set_height_scale) -- a fixed distance would crop a
	# 1.5x head out of frame and leave a 0.67x one tiny in the middle of it.
	var height_scale: float = character.get_height_scale()
	# Same height as the head, looking straight at it -- level, not tilted
	# down at some lower point (that read as looking down from above).
	_portrait_camera.global_position = head_pos + facing * PORTRAIT_CAMERA_DISTANCE * height_scale
	_portrait_camera.look_at(head_pos, Vector3.UP)

	await get_tree().process_frame
	await get_tree().process_frame

	var img := _portrait_viewport.get_texture().get_image()
	_portrait_image.texture = ImageTexture.create_from_image(img)
	# Nothing else ever needs to render here again -- free it rather than
	# leave a SubViewport rendering every frame for a texture nothing else
	# reads anymore.
	_portrait_viewport.queue_free()

func _get_character() -> Node3D:
	return _player.get_node_or_null("Character")

func _process(_delta: float) -> void:
	_health_bar.max_value = _player.MAX_HEALTH
	_health_bar.value = _player.health
	_stamina_bar.max_value = _player.MAX_STAMINA
	_stamina_bar.value = _player.stamina
	_level_label.text = str(_player.level)

	var frac: float = _player.health / _player.MAX_HEALTH
	if frac <= HEALTH_RED_FRACTION:
		_health_fill_style.bg_color = HEALTH_COLOR_RED
	elif frac <= HEALTH_YELLOW_FRACTION:
		_health_fill_style.bg_color = HEALTH_COLOR_YELLOW
	else:
		_health_fill_style.bg_color = HEALTH_COLOR_GREEN

	# Only shown while a gun is actually equipped (see player.gd's own
	# _equipped_gun/equip_gun()) -- duck-typed has_method check rather than
	# a hard type reference, same pattern every other optional capability in
	# this project already uses, so this stays correct if a second gun type
	# shows up later.
	var gun: Node = _player.get("_equipped_gun")
	if gun != null and gun.has_method("get_ammo_text"):
		_ammo_label.visible = true
		_ammo_label.text = gun.get_ammo_text()
	else:
		_ammo_label.visible = false

func _on_respawn_pressed() -> void:
	_player.respawn()

func _on_player_died() -> void:
	_death_screen.visible = true

func _on_player_respawned() -> void:
	_death_screen.visible = false
