extends Node
## Entry point / screen flow: title screen (with its own Singleplayer/
## Multiplayer/Settings menu revealed on top of the same rainy city, see
## title_screen.gd -- NOT a separate screen) -> character select ->
## character creator (or straight to a previously-saved character, see
## below) -> city with a spawned player carrying the character.
##
## Character select lists every character saved via character_save.gd (a
## ConfigFile at user://characters.cfg) -- picking one and pressing Play
## goes straight to _on_character_selected, skipping the creator entirely;
## Create New goes through the creator as before, and _on_creation_finished
## saves the result the instant it's done (name/head shape/painted atlas
## are all already finalized at that point) so it shows up in the list on
## the next launch. Back returns all the way to the title screen, per how
## this was specified.
##
## Multiplayer (see title_screen.gd's own Host/Join panel, which calls into
## Net -- net/network.gd) reuses this EXACT same title -> select -> creator
## pipeline for every peer -- there's no separate "multiplayer character
## creator." Net.is_networked() is what _on_creation_finished branches on:
## false keeps the original single-player spawn (plus the pickup-able
## greatsword, see weapons/greatsword.gd -- not multiplayer-aware, so it's
## deliberately left singleplayer-only rather than risking desyncing it
## across peers);
## true announces this peer to every other connected peer over an RPC (see
## _rpc_announce_player) instead, and every peer -- including this one, via
## the RPC's own call_local -- spawns a Player+Character for each announced
## peer. City generation itself is never networked at all: it's fully
## deterministic from one fixed WORLD_SEED (see city.gd), so every peer
## just builds an identical copy locally.
##
## Position/rotation AND locomotion/combat animation state both replicate
## now (see player.tscn's own MultiplayerSynchronizer plus player.gd's own
## synced_is_walking/etc properties and its _rpc_* one-shot move wrappers) --
## a remote player visibly walks, runs, crouches, jumps, dashes, punches,
## and blocks in sync with what their own owning peer is actually doing.
##
## Known gap, not built yet: punch damage is still applied purely locally,
## with no networking at all -- fine for both peers SEEING each other swing
## and even landing "hits" that read correctly (animation-wise) on the
## attacker's own screen, but the two screens' health values aren't
## actually kept in sync with each other yet (each peer's own take_damage()
## only ever runs on whichever peer's raycast happened to detect the hit,
## not the real target's own authoritative copy).

const CharacterSave = preload("res://character/character_save.gd")

const TITLE_SCREEN_SCENE := preload("res://title_screen/title_screen.tscn")
const CHARACTER_SELECT_SCENE := preload("res://ui/character_select.tscn")
const CHARACTER_CREATOR_SCENE := preload("res://character_creator/character_creator.tscn")
const CHARACTER_SCENE := preload("res://character/character.tscn")
const CITY_SCENE := preload("res://city/city.tscn")
const PLAYER_SCENE := preload("res://player/player.tscn")
const GREATSWORD_SCENE := preload("res://weapons/greatsword.tscn")
const OLD_GUY_SCENE := preload("res://npc/old_guy.tscn")

# In front of spawn -- Y is how high the grip sits once the blade's own tip
# is planted into the ground, see below.
const GREATSWORD_SPAWN_OFFSET := Vector3(0.0, 0.93, -3.0)
# Well off to one side of where the player lands, and BEHIND them (+Z is
# back; the player's own forward is -Z). Both parts are deliberate and both
# serve the same requirement -- he must not appear anywhere in the intro
# cutscene.
#
# Distance alone can't achieve that. The cutscene's own looking_around beat
# orbits the camera (see intro_cutscene.gd's SHOT_B: spring length 10.5 at
# yaw -130), and at that depth the view is roughly 13m wide either side of
# the player, so keeping him out of frame by placement would need ~15m --
# far enough to be a hike rather than someone waiting for you. He is
# therefore not spawned at all until the cutscene hands over (see
# _spawn_old_guy's own callers), and this offset only has to handle the
# moment AFTER that: behind the player, so he isn't popping into an empty
# street directly in view when control returns. Turn around and he's there.
#
# Y stays 0: unique_npc.gd is a real CharacterBody3D and settles onto the
# ground itself.
const OLD_GUY_SPAWN_OFFSET := Vector3(9.0, 0.0, 5.0)
# How far apart networked players spawn from the exact SpawnPoint -- see
# _spawn_offset_for.
const NETWORK_SPAWN_RADIUS := 2.0

const SCREEN_FADE_SECONDS := 0.6

var _title_screen: Control
var _character_select: Control
var _creator: Node3D
var _fade_overlay: ColorRect

# Overridable so tests that exercise the real character-creation flow (e.g.
# tests/test_spawn_authority_check.gd, which drives main.tscn directly
# through title -> select -> creator -> name-confirm) can point this at a
# throwaway path instead of the real user://characters.cfg -- see
# character_select.gd's own save_path for the matching override on the
# other side of this flow (_show_character_select propagates this value to
# it, so both stay in sync automatically). Must be set before
# _on_creation_finished/_on_character_selected/_show_character_select
# actually run.
var character_save_path: String = CharacterSave.SAVE_PATH

# Which saved character id the NEXT spawned local player should track
# playtime onto (see player.gd's own save_character_id) -- set right before
# _enter_world_with by whichever of _on_creation_finished/
# _on_character_selected is actually running, then read back out by
# _spawn_singleplayer/_spawn_local_player once the real Player node exists.
# -1 means "don't track" (not actually reachable today -- both real entry
# points always have a real id by the time _enter_world_with runs).
var _pending_save_character_id := -1

var _city: Node3D = null
# peer_id -> {"name": String, "head_shape": String, "hair_style": String,
# "height_scale": float, "atlas_png": PackedByteArray} for every peer
# announced so far (including this one) --
# the single source of truth for "who's in this session," independent of
# whether a Player node has actually been spawned for them yet (see
# _try_spawn_player's own comment on why those two need to stay separate).
var _player_info := {}
# peer_id -> the spawned Player node, once _try_spawn_player has actually
# instantiated one -- also doubles as the idempotency guard against
# spawning the same peer twice (e.g. this peer's own call_local echo of
# its own announcement, or a redundant catch-up replay).
var _spawned_players := {}

func _ready() -> void:
	CharacterSave.migrate_legacy_save_data(character_save_path)
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	Net.peer_connected.connect(_on_peer_connected)
	Net.peer_disconnected.connect(_on_peer_disconnected)

	# A dedicated, high-numbered CanvasLayer so this always draws above
	# EVERY other layer that shows up later (each menu screen/the character
	# creator's own UI lives in its own CanvasLayer too) -- a plain Control
	# sibling would only be guaranteed on top of whatever's in the default
	# layer.
	var fade_layer := CanvasLayer.new()
	fade_layer.layer = 100
	add_child(fade_layer)
	_fade_overlay = ColorRect.new()
	_fade_overlay.color = Color(0, 0, 0, 0)
	_fade_overlay.anchor_right = 1.0
	_fade_overlay.anchor_bottom = 1.0
	_fade_overlay.mouse_filter = Control.MOUSE_FILTER_IGNORE
	fade_layer.add_child(_fade_overlay)

	_show_title_screen()

func _show_title_screen() -> void:
	_title_screen = TITLE_SCREEN_SCENE.instantiate()
	add_child(_title_screen)
	_title_screen.enter_game_pressed.connect(_on_enter_game_pressed)

## Fades to black, runs `swap` (expected to free whatever's currently shown
## and instantiate/add_child whatever comes next), then fades back in.
## Blocks input for the whole crossfade so a stray click/key can't reach
## whatever's fading out or land on the next screen before it's actually
## meant to be interactive.
##
## `pre_swap`, if given, runs FIRST -- before the fade-out tween even
## starts, with input already blocked (mouse_filter is set below, before
## anything else) but the CURRENT screen still fully visible, unlike swap
## (which only ever runs once the screen's already gone fully black). For
## slow async work (see city.gd's own initial_load_ready, awaited here by
## _enter_world_with) that should happen while whatever's already on screen
## -- the rainy title screen, in particular -- stays visible and animating,
## not hidden behind an already-opaque black overlay. That opaque-overlay
## ordering is exactly what made this transition read as a hard freeze
## before: the screen was already fully black by the time several real
## seconds of city generation started, so there was nothing on screen to
## show the game was still doing anything at all -- reported directly.
func _fade_transition(swap: Callable, pre_swap: Callable = Callable()) -> void:
	_fade_overlay.mouse_filter = Control.MOUSE_FILTER_STOP

	if pre_swap.is_valid():
		await pre_swap.call()

	var fade_out := create_tween()
	fade_out.tween_property(_fade_overlay, "color:a", 1.0, SCREEN_FADE_SECONDS)
	await fade_out.finished

	# AWAITED, so a `swap` that is itself a coroutine (loading the city, say)
	# finishes before the fade back in starts. A bare swap.call() would
	# start the work and immediately fade in over the top of it.
	# Non-coroutine swaps are unaffected -- awaiting a plain return value
	# resumes on the spot.
	await swap.call()

	var fade_in := create_tween()
	fade_in.tween_property(_fade_overlay, "color:a", 0.0, SCREEN_FADE_SECONDS)
	await fade_in.finished
	_fade_overlay.mouse_filter = Control.MOUSE_FILTER_IGNORE

## Fired once there's an actual game to walk into -- Singleplayer, a
## successful Host, or a successful Join all funnel through here (see
## title_screen.gd's own class comment). No fade-to-black and no rain
## fade-out here (explicitly asked not to) -- character select comes up
## directly on top of the SAME still-visible, still-rainy title screen, not
## a separate screen being swapped to. The title screen itself is only
## actually hidden (and its rain actually stopped, see fade_out_rain) once
## something leaves it behind for real -- _on_create_new_pressed or
## _on_character_selected, both of which DO still fade to black, per how
## this was explicitly scoped ("only... if you choose to create new").
func _on_enter_game_pressed() -> void:
	# NOT hidden entirely (see hide_foreground_ui's own comment) -- the
	# rainy city background/rain/eyes stay visible and running behind
	# character_select.gd itself, which no longer paints its own opaque
	# background. Only the logo/menu/prompt foreground is hidden, since
	# that would otherwise sit stacked behind/through the character select
	# UI.
	_title_screen.hide_foreground_ui()
	_show_character_select()

func _show_character_select() -> void:
	_character_select = CHARACTER_SELECT_SCENE.instantiate()
	_character_select.save_path = character_save_path
	add_child(_character_select)
	_character_select.create_new_pressed.connect(_on_create_new_pressed)
	_character_select.back_pressed.connect(_on_character_select_back_pressed)
	_character_select.character_selected.connect(_on_character_selected)

func _on_create_new_pressed() -> void:
	# Actually leaving the title screen's rainy atmosphere behind for real
	# now (unlike just going to character select, see _on_enter_game_pressed) --
	# this is the right point to stop the rain, not up front.
	_title_screen.fade_out_rain()
	await _fade_transition(func():
		# Character select itself is the only screen the title screen shows
		# through behind (see _on_enter_game_pressed) -- hidden here, same as
		# it always was, before moving on to the creator.
		_title_screen.visible = false
		_character_select.queue_free()
		_creator = CHARACTER_CREATOR_SCENE.instantiate()
		add_child(_creator)
		_creator.creation_finished.connect(_on_creation_finished)
		_creator.creation_cancelled.connect(_on_creator_cancelled)
	)

## Mirror of _on_create_new_pressed, fired by the creator's own Back button/
## Escape (see character_creator.gd's own creation_cancelled -- already
## gated there on confirming discard of any painted work, nothing left to
## check here). Character select is re-instantiated fresh (the original
## instance was already queue_free()'d going INTO the creator) rather than
## kept alive the whole time, matching how _on_character_select_back_pressed
## already treats leaving that screen as final, not something to cache.
func _on_creator_cancelled() -> void:
	await _fade_transition(func():
		_creator.queue_free()
		_title_screen.visible = true
		_title_screen.hide_foreground_ui()
		_title_screen.fade_in_rain()
		_show_character_select()
	)

## Goes back to the title screen -- specified that way, even though it's
## one step further back than just the character-select screen alone. The
## SAME title screen instance from boot is reused (see reset_to_initial)
## rather than a fresh one being created, so its background music doesn't
## end up doubled up with a brand new instance's own. Also disconnects any
## in-progress/active network session -- backing out of character select
## mid-multiplayer-setup shouldn't leave a live connection dangling.
##
## No fade here either, same reasoning as _on_enter_game_pressed -- the
## title screen was never actually hidden or faded going INTO character
## select, so there's nothing visually discontinuous to smooth over coming
## back out of it either; reset_to_initial() already restores the initial
## "press any key" state instantly and correctly, rain and all.
func _on_character_select_back_pressed() -> void:
	Net.disconnect_game()
	_character_select.queue_free()
	_title_screen.reset_to_initial()
	_title_screen.visible = true

func _on_creation_finished(character: Node3D) -> void:
	# Saved right here, right when a freshly-created character's name/head
	# shape/painted atlas are all already finalized (see character_save.gd's
	# own class comment) -- this is the ONLY place a character ever gets
	# saved (there's no "edit an existing character" path back into the
	# creator yet), so a character made and named now genuinely survives a
	# full game restart, showing up in character_select.gd's own list next
	# time via CharacterSave.list_characters().
	_pending_save_character_id = CharacterSave.save_new_character(character.get_character_name(), character.get_head_shape(), character.get_atlas_png(), character_save_path, character.get_hair_style(), character.get_height_scale())

	character.set_paintable(false)
	character.get_parent().remove_child(character)

	# Fades creator -> black -> the fall already in progress, rather than
	# hard-cutting. Reported directly: "there's like a split second before
	# the cutscene where it shows your character at a normal pov standing on
	# the street". The root cause was in intro_cutscene.gd's own begin()
	# (see its comment -- six frames rendered before the fall started, now
	# fixed there); this fade is the second half of the ask, and covers the
	# whole creator-to-cutscene handover regardless.
	#
	# EVERYTHING happens behind the black, including the city load.
	#
	# It was briefly split -- city generation as pre_swap, with the creator
	# still on screen, to avoid a long black wait. That was wrong here and
	# reported directly: "I could see for a sec a different scene in between
	# entering character name and the beginning of the cutscene". city.tscn
	# carries its own WorldEnvironment and day/night lights, so the moment it
	# enters the tree it takes over the environment and drops its geometry
	# into the same World3D the creator's camera is looking at. There is no
	# "load it invisibly" -- an Environment applies from the moment its node
	# is in the tree, regardless of any parent's visibility.
	#
	# So: creator -> black -> (load, spawn, start the fall) -> back in on
	# the cutscene, with nothing else ever on screen. The cost is a few
	# seconds of black while the city generates, which is what was asked
	# for.
	#
	# is_new_character=true -- the ONLY call site that ever passes this,
	# since this function is the ONLY place a genuinely new (just-created,
	# never-before-saved) character enters the world.
	var swap := func() -> void:
		# Freed FIRST: the creator has to be gone before the city arrives,
		# or there's a frame where both exist in the same world.
		_creator.queue_free()
		await _load_city_async()
		_spawn_into_world(character, {}, {}, true)
	await _fade_transition(swap)

## Fired by character_select.gd's own Play button -- loads a previously
## saved character's data straight from disk and feeds it into the exact
## same _enter_world_with path _on_creation_finished uses, skipping the
## creator entirely. set_head_shape()/apply_atlas_png() are called AFTER
## _enter_world_with(character), not right after instantiate() -- confirmed
## as a real bug (reported as "loading an old saved character enters the
## game as a plain white character"): CHARACTER_SCENE.instantiate() does NOT
## run the character's own _ready() (only actually entering the tree does,
## here via _enter_world_with -> _spawn_singleplayer/_spawn_local_player ->
## _place_character_on's own add_child), and _ready() unconditionally
## rebuilds a fresh BLANK atlas_image (see character.gd's own _build_atlas)
## regardless of anything set on the node before that point. Calling
## apply_atlas_png() (and set_head_shape(), which touches the SAME
## atlas_image) before the character ever entered the tree meant _ready()
## always ran a beat later and silently wiped it back to blank -- the freshly-
## created path (_on_creation_finished) never hit this, since that character
## is already live and already inside the tree the whole time paint was
## actually happening, so _ready() only ever runs once, before any of it.
func _on_character_selected(id: int) -> void:
	var data: Dictionary = CharacterSave.load_character(id, character_save_path)
	if data.is_empty():
		return
	_pending_save_character_id = id
	var character: Node3D = CHARACTER_SCENE.instantiate()
	character.set_character_name(data["name"])
	# City generation (see _enter_world_with -> city.gd's own
	# initial_load_ready) now runs as _fade_transition's own `pre_swap`
	# step -- BEFORE the fade-out even starts, with the title screen (rain
	# and all) still fully visible and animating the whole time, rather
	# than after it, behind an already-opaque black overlay (which is what
	# made this specific transition read as a hard freeze -- reported
	# directly). _fade_transition's own mouse_filter=STOP (set as its very
	# first action, before pre_swap even runs) already blocks every further
	# click on character_select's own Play/Back buttons for this whole
	# window, so there's no separate re-entrancy guard needed here for
	# someone clicking Play twice mid-load.
	#
	# head_shape/atlas_png are handed to _enter_world_with as pending_paint
	# rather than applied right here -- see its own class comment for why
	# (character.gd's own _ready() hasn't run yet at this point, and
	# unconditionally rebuilds a blank atlas the instant it does, wiping out
	# anything set before tree entry). Confirmed as a real bug for the
	# MULTIPLAYER case specifically: applying these here, after
	# _enter_world_with already returned, meant its own networked branch had
	# already read+broadcast this peer's (still blank) atlas_png to every
	# other peer before the real paint ever got applied locally -- the local
	# screen looked fine (this fix-up ran right after), but no correction
	# was ever re-broadcast, leaving every remote peer's own copy blank.
	#
	# level/xp/stat_points/skill_points/the 5 stat_* fields are handed along
	# too as pending_progress -- a SEPARATE dict from pending_paint (not
	# folded in despite both landing on this same character) since they
	# apply to the PLAYER node _enter_world_with spawns, not the Character
	# node pending_paint targets, and are set directly (no _ready()-wipe
	# hazard the way atlas_image has, see player.gd's own var comments).
	var pending_progress := {
		"level": data["level"],
		"xp": data["xp"],
		"stat_points": data["stat_points"],
		"skill_points": data["skill_points"],
		"stat_health": data["stat_health"],
		"stat_strength": data["stat_strength"],
		"stat_stamina": data["stat_stamina"],
		"stat_mana": data["stat_mana"],
		"stat_speed": data["stat_speed"],
		"inventory_storage": data["inventory_storage"],
		"inventory_equipment": data["inventory_equipment"],
	}
	var pre_swap := func():
		await _enter_world_with(character, {"head_shape": data["head_shape"], "atlas_png": data["atlas_png"], "hair_style": data["hair_style"], "height_scale": data["height_scale"]}, pending_progress)
	var swap := func():
		_title_screen.fade_out_rain()
		_title_screen.visible = false
		_character_select.queue_free()
	await _fade_transition(swap, pre_swap)

## Shared by both real entry points into actual gameplay -- a freshly
## created character (_on_creation_finished) and a loaded saved one
## (_on_character_selected) -- everything from here on (spawning into the
## city, singleplayer vs. multiplayer branching) doesn't care which path
## the character came from. A real coroutine now (awaits the city's own
## initial_load_ready, see city.gd's own class comment on why that used to
## be a several-second blocking call) -- every caller needs `await` at its
## own call site too, or GDScript just starts this and moves on without
## actually waiting for city generation (and the player spawn that must
## come after it) to finish.
## pending_paint is only ever non-empty for a loaded saved character (see
## _on_character_selected) -- {"head_shape": String, "atlas_png":
## PackedByteArray, "hair_style": String, "height_scale": float}. A
## freshly-created character (_on_creation_finished)
## already carries its own live, already-painted atlas_image from the
## creator, so there's nothing to reapply for that path. pending_progress is
## the same idea for level/xp/stat points (see _on_character_selected's own
## comment on why it's a separate dict) -- empty for a freshly-created
## character too, which is correct as-is: player.gd's own level/xp/stat_*
## vars already default to a brand new level-1 character.
## Split out of _enter_world_with so a caller that wants to fade THROUGH
## black can do the slow half (city generation, several seconds) while the
## previous screen is still up, and the instant half (spawning, starting the
## intro cutscene) while the screen is actually black. See
## _on_creation_finished, which needs exactly that split; every other caller
## just uses _enter_world_with and gets the original behaviour.
func _load_city_async() -> void:
	# The single point every real "start playing" path funnels through (see
	# _enter_world_with's own class comment) -- the title screen's own
	# persistent background music has no reason to keep playing once there's
	# a world to walk into.
	_title_screen.fade_out_stars()
	_city = CITY_SCENE.instantiate()
	add_child(_city)
	await _city.initial_load_ready

## The instant half: everything from "the city exists" to "the player is in
## it and the intro cutscene has taken over". Deliberately contains no
## awaits at all, so a caller can run it inside a fade's own swap step and
## rely on it having fully finished by the time the fade back in starts.
func _spawn_into_world(character: Node3D, pending_paint: Dictionary, pending_progress: Dictionary, is_new_character: bool) -> void:
	if not Net.is_networked():
		_spawn_singleplayer(character, pending_paint, pending_progress, is_new_character)
		return

	var my_id := multiplayer.get_unique_id()
	_spawn_local_player(my_id, character, pending_paint, pending_progress, is_new_character)
	_player_info[my_id] = {
		"name": character.get_character_name(),
		"head_shape": character.get_head_shape(),
		"hair_style": character.get_hair_style(),
		"height_scale": character.get_height_scale(),
		"atlas_png": character.get_atlas_png(),
	}
	_rpc_announce_player.rpc(my_id, _player_info[my_id]["name"], _player_info[my_id]["head_shape"], _player_info[my_id]["atlas_png"], _player_info[my_id]["hair_style"], _player_info[my_id]["height_scale"])
	for peer_id in _player_info.keys():
		_try_spawn_player(peer_id)

func _enter_world_with(character: Node3D, pending_paint: Dictionary = {}, pending_progress: Dictionary = {}, is_new_character: bool = false) -> void:
	# The single point every real "start playing" path funnels through (see
	# this function's own class comment) -- the title screen's own
	# persistent background music (stars_player) has no reason to keep
	# playing once there's an actual world to walk into, and the
	# soon-to-be-spawned player's own outdoor city ambience (see player.gd's
	# own _ready()) is what takes over from here. Fired before city
	# generation/spawn even starts (not after) so the fade-out and the
	# loading time overlap instead of stacking, the same "start the
	# transition the moment it's truly committed to" reasoning
	# _on_create_new_pressed's own fade_out_rain() call site already uses.
	# Now just the two halves in sequence (see _load_city_async /
	# _spawn_into_world). Callers that need to fade THROUGH black call those
	# two directly instead, so the slow half can run while the previous
	# screen is still up -- see _on_creation_finished.
	#
	# _spawn_into_world carries the multiplayer announce/catch-up ordering
	# that used to live here; the ordering hazards it documents (reading
	# get_atlas_png only AFTER the character is in the tree, catching up on
	# peers who announced before our own city existed) moved with it intact.
	await _load_city_async()
	_spawn_into_world(character, pending_paint, pending_progress, is_new_character)

func _spawn_singleplayer(character: Node3D, pending_paint: Dictionary = {}, pending_progress: Dictionary = {}, is_new_character: bool = false) -> void:
	var player: CharacterBody3D = PLAYER_SCENE.instantiate()
	player.save_character_id = _pending_save_character_id
	player.character_save_path = character_save_path
	_apply_pending_progress(player, pending_progress)
	var spawn: Marker3D = _city.get_node("SpawnPoint")
	_city.add_child(player)
	player.position = spawn.position
	_city.set_player(player)

	_place_character_on(character, player)
	_apply_pending_paint(character, pending_paint)
	# AFTER _apply_pending_paint -- that's where a loaded character's own
	# saved height actually lands on the node, and the camera pivot is
	# placed off it (see player.gd's own sync_camera_to_character_height).
	player.sync_camera_to_character_height()
	_reequip_pending_inventory(player, pending_progress)

	var greatsword := GREATSWORD_SCENE.instantiate()
	_city.add_child(greatsword)
	greatsword.position = spawn.position + GREATSWORD_SPAWN_OFFSET
	# Blade planted straight down into the ground: a flip about X (see
	# greatsword.gd's own class comment -- its local +Y is "up the blade,"
	# so a PI rotation about X points that at the ground instead), plus a
	# slight tilt so it doesn't read as perfectly, artificially vertical.
	greatsword.rotation = Vector3(PI, 0.0, deg_to_rad(12))

	# Last -- character.gd's own anim_player doesn't exist until
	# _place_character_on's add_child(character) above runs its _ready().
	# player.position (not a re-derived spawn.position) so the cutscene's
	# own fall starts directly above wherever the player actually ended up.
	if is_new_character:
		player.start_intro_cutscene(player.position)
	_spawn_old_guy_when_playable(player, spawn.position, is_new_character)

## Spawns THIS peer's own player directly (not through _try_spawn_player's
## generic remote-player path, since we already have the real, already-
## painted Character instance from the creator in hand -- no need to
## round-trip it through PNG encode/decode against ourselves) and hands it
## to city.set_player() so chunk streaming follows OUR OWN position, not
## just whichever peer happens to get spawned first (see _try_spawn_player,
## which deliberately never calls set_player -- only this, our own, local
## player should ever drive chunk streaming on our own screen).
func _spawn_local_player(my_id: int, character: Node3D, pending_paint: Dictionary = {}, pending_progress: Dictionary = {}, is_new_character: bool = false) -> void:
	var player: CharacterBody3D = PLAYER_SCENE.instantiate()
	player.save_character_id = _pending_save_character_id
	player.character_save_path = character_save_path
	_apply_pending_progress(player, pending_progress)
	var spawn: Marker3D = _city.get_node("SpawnPoint")
	# Authority MUST be set before add_child -- _ready() runs synchronously
	# during add_child (this node's parent, _city, is already inside the
	# tree), and _ready() reads is_multiplayer_authority() exactly once to
	# decide whether to capture input/mouse, show a HUD+pause menu, and set
	# this body's camera current -- all one-time setup, never re-evaluated
	# later. Setting authority AFTER add_child (the original order here)
	# meant _ready() always saw the DEFAULT authority (always our own peer
	# id) regardless of which peer this player actually belongs to -- for
	# _spawn_local_player specifically my_id already equals our own id, so
	# this particular case was silently correct either way, but
	# _try_spawn_player's exact same ordering below was not (see there).
	player.position = spawn.position + _spawn_offset_for(my_id)
	player.set_multiplayer_authority(my_id)
	_city.add_child(player)
	_city.set_player(player)
	_place_character_on(character, player)
	_apply_pending_paint(character, pending_paint)
	player.sync_camera_to_character_height()
	_reequip_pending_inventory(player, pending_progress)
	_spawned_players[my_id] = player

	if is_new_character:
		player.start_intro_cutscene(player.position)
	_spawn_old_guy_when_playable(player, spawn.position, is_new_character)

## The first hand-authored talking NPC (see npc/unique_npc.gd) -- sitting a
## few metres off the landing point, watching where you come down, with one
## line about it.
##
## Called from the two LOCAL-player spawn paths only (_spawn_singleplayer /
## _spawn_local_player), never from _try_spawn_player -- that one runs once
## per REMOTE peer, so putting it there would stack a duplicate Old Guy on
## the same spot for every other player in the session. NPCs aren't
## networked in this project at all, so each peer independently spawning its
## own single copy is both consistent with that and the correct result on
## screen.
##
## Places Old Guy at the right MOMENT: immediately for a loaded character
## (no cutscene to stay out of), and only once the intro cutscene has handed
## control back for a new one.
##
## Deferring rather than hiding-then-showing, and rather than just moving
## him further away: he must not appear in any cutscene shot, and the
## cutscene's own camera covers too much ground for placement alone to
## guarantee that (see OLD_GUY_SPAWN_OFFSET). Not existing yet is the only
## version of "not visible" with no edge cases -- no frame where a
## half-faded body is caught by the orbit, nothing to get wrong.
##
## CONNECT_ONE_SHOT because this must fire exactly once. The signal itself
## only ever fires once per spawn today, but a second Old Guy standing
## inside the first is the kind of thing that would go unnoticed for a long
## time.
func _spawn_old_guy_when_playable(player: CharacterBody3D, spawn_position: Vector3, is_new_character: bool) -> void:
	if not is_new_character:
		_spawn_old_guy(spawn_position)
		return
	player.intro_cutscene_finished.connect(
		func() -> void: _spawn_old_guy(spawn_position),
		CONNECT_ONE_SHOT
	)

## Placed on every entry into the world, not only behind is_new_character
## the way the intro cutscene is: he's a fixture of the spawn area, and an
## NPC who exists exactly once and then is gone forever the next time you
## load that save would read as a bug rather than as a scripted moment.
func _spawn_old_guy(spawn_position: Vector3) -> void:
	# Guarded because the deferred path above can fire a whole cutscene
	# later, by which point the world may be gone (quit to menu mid-fall).
	if _city == null or not is_instance_valid(_city):
		return
	var old_guy: CharacterBody3D = OLD_GUY_SCENE.instantiate()
	_city.add_child(old_guy)
	old_guy.global_position = spawn_position + OLD_GUY_SPAWN_OFFSET
	# Turned to face the landing point. look_at aims the body's own -Z,
	# which is the same direction the Character child's face points once
	# unique_npc.gd applies its own PI flip -- so this really is "looking at
	# where the player lands," not away from it. The target is pulled to his
	# own height first so a spawn point at a different elevation can't tip
	# him over backwards; x/z are re-zeroed afterward for the same reason
	# every _face() helper in npc/ does it.
	var face_target := Vector3(spawn_position.x, old_guy.global_position.y, spawn_position.z)
	old_guy.look_at(face_target, Vector3.UP)
	old_guy.rotation.x = 0.0
	old_guy.rotation.z = 0.0

func _place_character_on(character: Node3D, player: CharacterBody3D) -> void:
	character.position = Vector3.ZERO
	# The character's local +Z ("face", the side shown to the player in the
	# creator, since that camera sits in front of the character looking
	# toward -Z) needs to point the same way the player moves (local -Z).
	# Left at zero rotation, the face would point at the third-person
	# camera (which sits behind the player) instead of away from it --
	# i.e. the character would appear to walk backwards, face-on to camera.
	character.rotation = Vector3(0, PI, 0)
	player.add_child(character)

## Applies a loaded saved character's head_shape/atlas_png/hair_style --
## must run AFTER `character` is already inside the scene tree (see
## _place_character_on's own add_child call right before every call site of
## this), since character.gd's own _ready() unconditionally rebuilds a blank
## atlas the instant it runs, wiping out anything set before tree entry.
## No-op for an empty dict (a freshly-created character, which already
## carries its own live, already-painted atlas_image and hair from the
## creator -- nothing to reapply). set_head_shape() before
## apply_atlas_png(), matching character.gd's own set_head_shape comment
## (changing head shape clears/rebuilds that part of the atlas);
## set_hair_style() after both -- set_head_shape() already rebuilds the head
## (and, via character.gd's own _build_head(), the hair mesh along with it,
## but back to "none" since _hair_style hasn't been set from the save data
## yet at that point), so the real saved style still needs applying here.
## Re-equips whatever this saved character had on when they last saved --
## genuinely new logic, no prior precedent (nothing re-equipped anything on
## load before the inventory system existed; a freshly instantiated
## Character always started bare). Must run AFTER _place_character_on
## (same reason _apply_pending_paint does, see its own comment) --
## equip_item_id() ultimately calls into an item's own _equip(player),
## which looks up player.get_node("Character") and does nothing if that's
## not there yet. No-op for an empty dict (a freshly-created character,
## nothing saved to re-equip).
func _reequip_pending_inventory(player: CharacterBody3D, pending_progress: Dictionary) -> void:
	if pending_progress.is_empty():
		return
	var inv: Node = player.get_node("Inventory")
	var equipment: Dictionary = pending_progress["inventory_equipment"]
	for slot_key in equipment:
		inv.equip_item_id(equipment[slot_key], player)

func _apply_pending_paint(character: Node3D, pending_paint: Dictionary) -> void:
	if pending_paint.is_empty():
		return
	character.set_head_shape(pending_paint["head_shape"])
	character.apply_atlas_png(pending_paint["atlas_png"])
	if pending_paint.has("hair_style"):
		character.set_hair_style(pending_paint["hair_style"])
	# .get() with a default rather than a required key: a save written
	# before the creator had a height slider has no height of its own, and
	# character_save.gd already reads those back as the normal size.
	character.set_height_scale(pending_paint.get("height_scale", 1.0))

## Applies a loaded saved character's own level/xp/stat_points/skill_points/
## 5 stat_* fields directly onto `player` -- unlike _apply_pending_paint's
## own atlas_image (which needs the character already inside the tree, see
## its own comment), player.gd's own _ready() reads these back out itself
## (see its own comment there), so this just needs to run BEFORE that node
## enters the tree -- called right after PLAYER_SCENE.instantiate(), same
## spot save_character_id/character_save_path already get set. A no-op for
## a freshly-created character (empty pending_progress, see
## _enter_world_with's own comment), leaving player.gd's own level=1/xp=0/
## stat_*=0 defaults untouched.
func _apply_pending_progress(player: CharacterBody3D, pending_progress: Dictionary) -> void:
	if pending_progress.is_empty():
		return
	player.level = pending_progress["level"]
	player.xp = pending_progress["xp"]
	player.stat_points = pending_progress["stat_points"]
	player.skill_points = pending_progress["skill_points"]
	player.stat_health = pending_progress["stat_health"]
	player.stat_strength = pending_progress["stat_strength"]
	player.stat_stamina = pending_progress["stat_stamina"]
	player.stat_mana = pending_progress["stat_mana"]
	player.stat_speed = pending_progress["stat_speed"]

	# Same "set before add_child, read in _ready()" ordering every field
	# above already relies on -- the Inventory node is a real static child
	# of player.tscn (not built at runtime), so it already exists here even
	# though its own _ready() hasn't fired yet. Inventory._ready()'s own
	# "only fill in the default empty grid if nothing's here yet" guard
	# means it won't stomp this. Array.assign() (not a plain =) is what
	# actually satisfies storage_slots' own Array[Dictionary] typing --
	# ConfigFile deserializes a saved array back as a plain untyped Array,
	# which a typed Array[Dictionary] field can't be assigned directly.
	var inv: Node = player.get_node("Inventory")
	var storage: Array[Dictionary] = []
	storage.assign(pending_progress["inventory_storage"])
	inv.storage_slots = storage
	# Equipped-slot re-equip is NOT done here -- unlike storage_slots (just
	# data on the Inventory node), equipping needs a real Character node to
	# attach to, and Character isn't parented onto `player` yet at this
	# point (that happens later, in _place_character_on -- same reason
	# _apply_pending_paint below has its own separate call site instead of
	# living in here). See _reequip_pending_inventory().

## Deterministic per-peer offset (a point on a small circle around
## SpawnPoint) so multiple networked players don't spawn exactly stacked
## on top of each other -- peer_id itself is an arbitrary server-assigned
## int, not a small sequential index, so it's used directly as an angle in
## radians rather than needing a separately-tracked "which number joiner is
## this" counter. Two peer ids landing close enough in radians to still
## visibly overlap is possible in principle but very unlikely at the scale
## this is actually used at (a handful of friends, not hundreds of peers).
func _spawn_offset_for(peer_id: int) -> Vector3:
	var angle := float(peer_id)
	return Vector3(cos(angle), 0.0, sin(angle)) * NETWORK_SPAWN_RADIUS

## Broadcast (see the .rpc() call in _on_creation_finished) whenever a peer
## finishes character creation, and re-sent targeted at just one peer (see
## _on_peer_connected) to catch a late joiner up on everyone who announced
## themselves before that peer connected -- an ordinary broadcast .rpc()
## call only ever reaches whoever's ALREADY connected at the moment it's
## sent, it isn't retroactively replayed to later joiners by Godot itself.
## Records the announcement in _player_info UNCONDITIONALLY, even if this
## peer's own _city doesn't exist yet (still mid-character-creation) --
## _on_creation_finished's own catch-up loop is what actually spawns
## anything recorded here that arrived too early to spawn immediately.
@rpc("any_peer", "call_local", "reliable")
func _rpc_announce_player(peer_id: int, character_name: String, head_shape: String, atlas_png: PackedByteArray, hair_style: String = "none", height_scale: float = 1.0) -> void:
	_player_info[peer_id] = {"name": character_name, "head_shape": head_shape, "atlas_png": atlas_png, "hair_style": hair_style, "height_scale": height_scale}
	_try_spawn_player(peer_id)

## Spawns a Player+Character for `peer_id` if (a) we actually know about
## them yet (_player_info) and (b) we have our own city to spawn them into
## and (c) we haven't already (idempotency, both against this peer's own
## call_local echo of its own announcement and against a redundant catch-up
## replay). Never calls city.set_player() -- see _spawn_local_player's own
## comment for why only our own local player should ever do that.
func _try_spawn_player(peer_id: int) -> void:
	if _city == null or _spawned_players.has(peer_id) or not _player_info.has(peer_id):
		return
	var info: Dictionary = _player_info[peer_id]

	var player: CharacterBody3D = PLAYER_SCENE.instantiate()
	var spawn: Marker3D = _city.get_node("SpawnPoint")
	# Authority set BEFORE add_child -- see _spawn_local_player's own comment
	# on why. This was the call site where the ordering bug actually bit:
	# peer_id here is always someone ELSE's id, so every remote player's
	# _ready() was seeing "I'm the authority" (our own default id) at the
	# exact moment it decided to capture input/mouse, spawn a phantom HUD +
	# pause menu, steal the active camera (camera.current = true) from our
	# own real player, and -- worst of all -- never actually disable its own
	# physics_process/unhandled_input, so it kept reading and acting on our
	# local WASD/mouse input right alongside our own real player, fighting
	# the position replication coming from that peer's own machine.
	player.position = spawn.position + _spawn_offset_for(peer_id)
	player.set_multiplayer_authority(peer_id)
	_city.add_child(player)
	_spawned_players[peer_id] = player

	var character := CHARACTER_SCENE.instantiate()
	character.set_paintable(false)
	character.set_character_name(info["name"])
	_place_character_on(character, player)
	# AFTER _place_character_on (which add_child()s it), not before -- same
	# "_ready() hasn't run yet, so it silently rebuilds a blank atlas right
	# back on top of this" bug _on_character_selected's own comment explains,
	# just for a remote peer's own painted design instead of a loaded save.
	# set_head_shape() before apply_atlas_png() -- same ordering
	# _apply_pending_paint's own comment already establishes (changing head
	# shape clears/rebuilds that part of the atlas).
	character.set_head_shape(info["head_shape"])
	character.apply_atlas_png(info["atlas_png"])
	character.set_hair_style(info.get("hair_style", "none"))
	character.set_height_scale(info.get("height_scale", 1.0))
	player.sync_camera_to_character_height()

## Every peer (not just the server) replays its own already-known
## _player_info to a newly-connected peer -- redundant when more than one
## peer already knows about the same players (harmless, _try_spawn_player
## on the receiving end is idempotent), but correct regardless of whether
## peer_connected fires only on the server or on every peer.
func _on_peer_connected(id: int) -> void:
	for peer_id in _player_info.keys():
		var info: Dictionary = _player_info[peer_id]
		_rpc_announce_player.rpc_id(id, peer_id, info["name"], info["head_shape"], info["atlas_png"], info.get("hair_style", "none"), info.get("height_scale", 1.0))

func _on_peer_disconnected(id: int) -> void:
	if _spawned_players.has(id):
		var player: Node = _spawned_players[id]
		if is_instance_valid(player):
			player.queue_free()
		_spawned_players.erase(id)
	_player_info.erase(id)
