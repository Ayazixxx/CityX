extends Control
## Title screen: a rainy, hand-drawn city at night with something watching
## from two of its windows. Click, or press any key, to reveal the menu
## (Singleplayer/Multiplayer/Settings) -- right on top of this same rainy
## city, not a separate screen swap, so the rain/lightning/eyes keep going
## underneath it the whole time. Settings just shows the shared panel (see
## ui/settings_panel.gd, also used by the in-game pause menu) without
## leaving this screen either. Multiplayer shows its own Host/Join sub-
## panel -- both paths call into Net (see net/network.gd) directly-by-IP,
## no lobby list/matchmaking (that's what Net's own class comment already
## flags as future work once this is Steam-ready).
##
## enter_game_pressed fires once there's an actual game to walk into --
## Singleplayer emits it immediately, Host once Net.host_game() succeeds,
## Join once Net.connected_to_server actually fires (not just once
## join_game() is CALLED -- that only starts a connection attempt, see
## _on_join_pressed's own comment). main.gd doesn't care which of the three
## it was; it just checks Net.is_networked() once character creation
## finishes to decide whether to spawn a local-only session or announce
## itself to other peers (see main.gd's own _on_creation_finished).
##
## main.gd keeps this ONE instance alive for the whole game rather than
## freeing and recreating it every time the player backs out to it (see
## reset_to_initial) -- it just gets hidden/shown. That's what lets
## StarsPlayer (the persistent background music) just stay put as this
## node's own child and keep playing regardless, instead of needing to be
## reparented out from under a soon-to-be-freed title screen the way an
## earlier version of this had to.

signal enter_game_pressed

const RAIN_FADE_OUT_SECONDS := 1.5

const LOGO_FLICKER_PERIOD := 6.5
const PROMPT_PULSE_PERIOD := 2.2
const LIGHTNING_MIN := 4.0
const LIGHTNING_MAX := 13.0
const STRIKE_GAP_MIN := 0.09
const STRIKE_GAP_MAX := 0.21

@onready var background: TextureRect = $Background
@onready var logo: TextureRect = $Logo
@onready var _city_subviewport: SubViewport = $City3DViewport/SubViewport
@onready var _city_backdrop: Node3D = $City3DViewport/SubViewport/TitleScreenCity
@onready var eye_layer: Control = $EyeLayer
@onready var prompt_label: Label = $PromptLabel
@onready var flash_overlay: ColorRect = $FlashOverlay
@onready var rain_player: AudioStreamPlayer = $RainPlayer
@onready var stars_player: AudioStreamPlayer = $StarsPlayer
@onready var menu_panel: Control = $MenuPanel
@onready var settings_panel: Control = $SettingsPanel
@onready var multiplayer_panel: Control = $MultiplayerPanel
@onready var _singleplayer_button: Button = $MenuPanel/SingleplayerButton
@onready var _multiplayer_button: Button = $MenuPanel/MultiplayerButton
@onready var _settings_button: Button = $MenuPanel/SettingsButton
@onready var _host_button: Button = $MultiplayerPanel/HostRow/HostButton
@onready var _host_port_field: LineEdit = $MultiplayerPanel/HostRow/HostPortField
@onready var _join_address_field: LineEdit = $MultiplayerPanel/JoinRow/JoinAddressField
@onready var _join_port_field: LineEdit = $MultiplayerPanel/JoinRow/JoinPortField
@onready var _join_button: Button = $MultiplayerPanel/JoinRow/JoinButton
@onready var _multiplayer_status_label: Label = $MultiplayerPanel/StatusLabel
@onready var _multiplayer_back_button: Button = $MultiplayerPanel/BackButton

var _flicker_t := 0.0
var _pulse_t := 0.0
var _lightning_timer := 0.0
var _rng := RandomNumberGenerator.new()
# Any key/click reveals the menu -- without this guard, holding a key down
# (auto-repeat) or hitting several keys in the same moment would re-trigger
# the reveal repeatedly. Renamed from the old "have we started" meaning --
# this screen doesn't itself go anywhere anymore, it just reveals its own
# menu once.
var _menu_revealed := false

func _ready() -> void:
	_rng.randomize()
	_lightning_timer = _rng.randf_range(LIGHTNING_MIN, LIGHTNING_MAX)
	resized.connect(_layout_art)
	resized.connect(_layout_city_viewport)
	_layout_art()
	_layout_city_viewport()

	rain_player.stream.loop = true
	stars_player.stream.loop = true
	rain_player.play()
	stars_player.play()

	_singleplayer_button.pressed.connect(func(): enter_game_pressed.emit())
	_multiplayer_button.pressed.connect(_on_multiplayer_pressed)
	_settings_button.pressed.connect(_on_settings_pressed)
	settings_panel.back_pressed.connect(_on_settings_back_pressed)

	_host_button.pressed.connect(_on_host_pressed)
	_join_button.pressed.connect(_on_join_pressed)
	_multiplayer_back_button.pressed.connect(_on_multiplayer_back_pressed)

func _on_multiplayer_pressed() -> void:
	menu_panel.visible = false
	multiplayer_panel.visible = true
	_multiplayer_status_label.text = ""

func _on_settings_pressed() -> void:
	menu_panel.visible = false
	settings_panel.visible = true

func _on_settings_back_pressed() -> void:
	settings_panel.visible = false
	menu_panel.visible = true

## Straight to hosting on the default port (see Net.DEFAULT_PORT) -- no
## separate port field, matching this whole panel's "direct-IP, no
## matchmaking" scope (see this file's own class comment). ENetMultiplayerPeer
## fails synchronously (create_server returns an error code immediately,
## e.g. the port's already in use) so, unlike joining, there's no
## "connecting..." wait state to show here -- it's either an instant error
## or already succeeded.
## Both rows now carry their own port box, left blank for the usual case
## (see _parse_port -- blank means Net.DEFAULT_PORT).
##
## They're SEPARATE ports on purpose, not one shared field. When a host is
## behind a router, the port a joiner has to dial is the router's own
## EXTERNAL port, which is frequently not the port the host is actually
## bound to internally (a forward of external 30000 -> internal 7777 is a
## perfectly ordinary rule, and some routers assign the external one for
## you). With a single hardcoded 7777 on both ends that setup was simply
## unreachable, with nothing in the UI to suggest why.
func _on_host_pressed() -> void:
	var port := _parse_port(_host_port_field)
	if port < 0:
		_multiplayer_status_label.text = "Host port must be a number between 1 and 65535."
		return
	var err := Net.host_game(port)
	if err != OK:
		_multiplayer_status_label.text = "Couldn't host on port %d (error %d) -- is another instance already running?" % [port, err]
		return
	_multiplayer_status_label.text = "Hosting on port %d." % port
	enter_game_pressed.emit()

## Blank -> Net.DEFAULT_PORT, so the common case needs no typing at all.
## Returns -1 for anything that isn't a valid port, which the callers turn
## into a message rather than silently falling back to the default -- a
## typo'd port that quietly became 7777 would be a genuinely confusing
## thing to debug from the other end of a "couldn't connect".
func _parse_port(field: LineEdit) -> int:
	var text := field.text.strip_edges()
	if text.is_empty():
		return Net.DEFAULT_PORT
	if not text.is_valid_int():
		return -1
	var value := int(text)
	if value < 1 or value > 65535:
		return -1
	return value

## Unlike hosting, join_game() only STARTS a connection attempt --
## ENetMultiplayerPeer resolves/connects asynchronously, so success or
## failure only becomes known later via Net's own connected_to_server/
## connection_failed signals (see network.gd). CONNECT_ONE_SHOT on both so
## repeated Join presses don't stack up duplicate listeners, each firing
## once for exactly the attempt that triggered it.
func _on_join_pressed() -> void:
	var address := _join_address_field.text.strip_edges()
	if address.is_empty():
		_multiplayer_status_label.text = "Enter an IP address to join."
		return

	var port := _parse_port(_join_port_field)
	if port < 0:
		_multiplayer_status_label.text = "Port must be a number between 1 and 65535."
		return

	# A pasted "1.2.3.4:30000" is how an address usually gets shared, so it's
	# accepted directly and the port taken from it. Guarded on there being
	# exactly ONE colon: an IPv6 address is full of them, and splitting one
	# of those would mangle the address into nonsense.
	if address.count(":") == 1:
		var parts := address.split(":")
		var suffix := parts[1].strip_edges()
		if suffix.is_valid_int() and int(suffix) >= 1 and int(suffix) <= 65535:
			address = parts[0].strip_edges()
			port = int(suffix)

	var err := Net.join_game(address, port)
	if err != OK:
		_multiplayer_status_label.text = "Couldn't start connecting (error %d)." % err
		return
	_multiplayer_status_label.text = "Connecting to %s:%d..." % [address, port]
	Net.connected_to_server.connect(_on_connected_to_server, CONNECT_ONE_SHOT)
	Net.connection_failed.connect(_on_connection_failed, CONNECT_ONE_SHOT)

func _on_connected_to_server() -> void:
	enter_game_pressed.emit()

func _on_connection_failed() -> void:
	_multiplayer_status_label.text = "Couldn't connect -- check the address and that they're actually hosting."

## Also disconnects -- covers the case where Back is pressed while a join
## attempt is still pending ("Connecting..." showing), not just backing out
## of an idle panel with nothing in progress.
func _on_multiplayer_back_pressed() -> void:
	Net.disconnect_game()
	multiplayer_panel.visible = false
	menu_panel.visible = true

## Called by main.gd right when Singleplayer is pressed, before this whole
## screen gets hidden -- fades the rain out and stops it (rather than
## leaving it silently looping forever underneath, wasted work) without
## touching stars_player at all, which just keeps playing as background
## music through every later screen.
func fade_out_rain() -> void:
	var tw := create_tween()
	tw.tween_property(rain_player, "volume_db", -80.0, RAIN_FADE_OUT_SECONDS)
	tw.tween_callback(rain_player.stop)

## Called once by main.gd's own _enter_world_with() -- the single funnel
## both real "actually start playing" paths (a freshly created character
## and a loaded saved one) go through -- so the persistent background music
## (see this file's own class comment on why stars_player, unlike
## rain_player, normally keeps playing through every earlier screen) finally
## stops right as the newly-spawned player's own outdoor city ambience (see
## player.gd's own _fade_city_ambience()) fades in and takes over. Same
## fade-then-stop shape as fade_out_rain() above, reusing its exact duration
## rather than inventing a second "how long does an ambience transition
## take" number.
func fade_out_stars() -> void:
	var tw := create_tween()
	tw.tween_property(stars_player, "volume_db", -80.0, RAIN_FADE_OUT_SECONDS)
	tw.tween_callback(stars_player.stop)

## Companion to fade_out_rain(), for backing OUT of the character creator
## (see main.gd's own _on_creator_cancelled) -- without this the rain would
## stay silently stopped for the rest of the session even though character
## select is shown back on top of this same rainy screen.
func fade_in_rain() -> void:
	rain_player.volume_db = -80.0
	rain_player.play()
	var tw := create_tween()
	tw.tween_property(rain_player, "volume_db", 0.0, RAIN_FADE_OUT_SECONDS)

## Called by main.gd when the player backs all the way out to the title
## screen from character select -- puts this same (never-freed) instance
## back into its exact original "press any key" state rather than staying
## on whatever menu/settings sub-panel happened to be showing last time.
func reset_to_initial() -> void:
	_menu_revealed = false
	_city_backdrop.set_logo_visible(true)
	prompt_label.visible = true
	prompt_label.modulate.a = 0.15
	menu_panel.visible = false
	settings_panel.visible = false
	multiplayer_panel.visible = false
	_multiplayer_status_label.text = ""
	rain_player.volume_db = 0.0
	rain_player.play()

func _layout_art() -> void:
	_layout_cover_bottom(background)
	_layout_cover_bottom(logo)

## Keeps the 3D backdrop's SubViewport rendering at the Control's actual
## pixel size (rather than a fixed resolution) so City3DViewport's own
## stretch=true just does a cheap 1:1 blit instead of visibly distorting the
## skyline/logo whenever the window is resized to an aspect ratio other than
## the SubViewport's default.
func _layout_city_viewport() -> void:
	if size.x <= 0.0 or size.y <= 0.0:
		return
	_city_subviewport.size = Vector2i(size)

## Matches CSS `background-size: cover; background-position: center bottom`:
## scale up (preserving aspect) until the image covers the whole rect, then
## anchor it so the image's bottom edge sits on the rect's bottom edge and
## it's centered horizontally.
func _layout_cover_bottom(rect: TextureRect) -> void:
	if rect.texture == null or size.x <= 0.0 or size.y <= 0.0:
		return
	var tex_size := rect.texture.get_size()
	var scale_factor := maxf(size.x / tex_size.x, size.y / tex_size.y)
	var disp := Vector2(tex_size) * scale_factor
	var offset := Vector2((size.x - disp.x) / 2.0, size.y - disp.y)
	rect.position = offset
	rect.size = disp

func _process(delta: float) -> void:
	_flicker_t = fmod(_flicker_t + delta, LOGO_FLICKER_PERIOD)
	_city_backdrop.set_logo_flicker(_flicker_opacity(_flicker_t / LOGO_FLICKER_PERIOD * 100.0))

	if prompt_label.visible:
		_pulse_t += delta
		var phase := fmod(_pulse_t, PROMPT_PULSE_PERIOD) / PROMPT_PULSE_PERIOD
		prompt_label.modulate.a = lerpf(0.15, 0.9, (sin(phase * TAU - PI / 2.0) + 1.0) / 2.0)

	_lightning_timer -= delta
	if _lightning_timer <= 0.0:
		_lightning_timer = _rng.randf_range(LIGHTNING_MIN, LIGHTNING_MAX)
		_trigger_lightning()

## Mirrors the CSS @keyframes flicker (a 6.5s loop) on the logo: mostly at
## full opacity, with a couple of quick dips/flares like a failing sign.
func _flicker_opacity(pct: float) -> float:
	if pct < 92.0:
		return 1.0
	elif pct < 93.0:
		return lerpf(1.0, 0.8, pct - 92.0)
	elif pct < 94.0:
		return lerpf(0.8, 1.0, pct - 93.0)
	elif pct < 95.0:
		return lerpf(1.0, 0.55, pct - 94.0)
	elif pct < 96.0:
		return lerpf(0.55, 1.0, pct - 95.0)
	else:
		return 1.0

func _trigger_lightning() -> void:
	var strikes := 2 if _rng.randf() < 0.35 else 1
	for i in range(strikes):
		_flash_once()
		eye_layer.trigger_surge()
		if i < strikes - 1:
			await get_tree().create_timer(_rng.randf_range(STRIKE_GAP_MIN, STRIKE_GAP_MAX)).timeout

func _flash_once() -> void:
	flash_overlay.modulate.a = _rng.randf_range(0.55, 0.9)
	var tw := create_tween()
	tw.tween_property(flash_overlay, "modulate:a", 0.0, 0.22).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_OUT)

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE:
		if _menu_revealed:
			_on_escape_pressed()
		return
	if _menu_revealed:
		return
	if event is InputEventMouseButton and event.pressed:
		_reveal_menu()
	elif event is InputEventKey and event.pressed:
		_reveal_menu()

func _reveal_menu() -> void:
	_menu_revealed = true
	prompt_label.visible = false
	menu_panel.visible = true

## Escape steps back one level at a time: out of whichever sub-panel is
## currently showing (reusing its own Back button's exact handler, not a
## duplicate of that logic) and, from the root Singleplayer/Multiplayer/
## Settings menu itself, all the way back to the original "press any key"
## idle state (the reverse of _reveal_menu).
func _on_escape_pressed() -> void:
	if multiplayer_panel.visible:
		_on_multiplayer_back_pressed()
	elif settings_panel.visible:
		_on_settings_back_pressed()
	elif menu_panel.visible:
		_menu_revealed = false
		menu_panel.visible = false
		prompt_label.visible = true

## Hides everything EXCEPT the pure ambient background layer -- called by
## main.gd right as character_select.gd goes up on top of this same (still
## visible) screen, so the rainy city/lightning/eyes keep running behind
## character select instead of this whole screen disappearing, per how that
## was explicitly asked for. The "CITY X" logo and PromptLabel are title-
## screen BRANDING, not ambient atmosphere -- left up, either would sit
## directly behind/through character select's own now-transparent panel and
## clutter it, so both get hidden here too, not just the 3 menu/settings/
## multiplayer sub-panels. Doesn't touch visibility of this node itself --
## main.gd is still the one that hides this whole screen later, once
## character select hands off to the creator or straight into the game
## world.
func hide_foreground_ui() -> void:
	menu_panel.visible = false
	settings_panel.visible = false
	multiplayer_panel.visible = false
	_city_backdrop.set_logo_visible(false)
	prompt_label.visible = false
