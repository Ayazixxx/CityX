extends RefCounted
## Static-function module for saving/loading player-created characters to
## disk (user://characters.cfg), so "make a character and name it" survives
## a full game restart -- see ui/character_select.gd (lists saved
## characters, lets the player pick one to play as) and main.gd's own
## _on_creation_finished (saves a freshly-created one). Same "static
## functions on a lightweight module" pattern fused_body.gd/city_chunk.gd
## already use, and the same ConfigFile-at-user:// persistence technique
## ui/settings_panel.gd already established for audio settings -- just
## applied to character data instead.
##
## Every save is a NEW entry (see save_new_character) -- there's no "update
## an existing character" path yet, since the creator itself has no way to
## re-open an existing character for editing today (Create New is the only
## way in, per character_select.gd's own longstanding comment). Each saved
## character gets its own ConfigFile section ("character_<id>"), id being a
## simple auto-incrementing integer scanned from existing sections rather
## than tracked separately, so there's no extra "next id" state to ever get
## out of sync with what's actually on disk.

const SAVE_PATH := "user://characters.cfg"

## Every function below takes save_path as an optional final argument,
## defaulting to the real SAVE_PATH -- lets regression tests point at a
## throwaway path instead, so running the test suite can never overwrite or
## pollute an actual player's real user://characters.cfg (unlike, say,
## ui/settings_panel.gd's own tests, which accept touching the real
## settings.cfg since a volume slider isn't precious, hand-painted user
## data the way a saved character is).

## Appends a new saved-character entry (playtime starts at 0, level starts
## at 1 with no xp/points spent -- see level's own comment in
## list_characters). Returns the new character's own id.
static func save_new_character(character_name: String, head_shape: String, atlas_png: PackedByteArray, save_path: String = SAVE_PATH, hair_style: String = "none", height_scale: float = 1.0) -> int:
	var config := ConfigFile.new()
	config.load(save_path) # ignore error -- a missing file just starts empty, same as ui/settings_panel.gd's own load
	var id := _next_id(config)
	var section := "character_%d" % id
	config.set_value(section, "name", character_name)
	config.set_value(section, "head_shape", head_shape)
	config.set_value(section, "hair_style", hair_style)
	config.set_value(section, "height_scale", height_scale)
	config.set_value(section, "atlas_png", atlas_png)
	config.set_value(section, "playtime_seconds", 0.0)
	config.set_value(section, "level", 1)
	config.set_value(section, "xp", 0.0)
	config.set_value(section, "stat_points", 0)
	config.set_value(section, "skill_points", 0)
	config.set_value(section, "stat_health", 0)
	config.set_value(section, "stat_strength", 0)
	config.set_value(section, "stat_stamina", 0)
	config.set_value(section, "stat_mana", 0)
	config.set_value(section, "stat_speed", 0)
	config.set_value(section, "inventory_storage", [])
	config.set_value(section, "inventory_equipment", {})
	# The return value is CHECKED here, unlike the load above (a missing file
	# legitimately starts empty). A failed write is silent data loss of
	# hand-painted work: the game carries straight on into the world with a
	# character that looks saved, is playable for the session, and simply
	# isn't there next launch -- with nothing anywhere saying why. Cost a
	# real round trip once already while trying to work out why a
	# just-created character couldn't be found on disk.
	var err := config.save(save_path)
	if err != OK:
		push_error("character_save: FAILED to write '%s' to %s (error %d) -- this character will not survive a restart" % [character_name, save_path, err])
	return id

## Self-healing migration, meant to be called once at game startup (see
## main.gd's own _ready()): if save_path has zero saved characters, checks
## the handful of OLD OS-level save locations this project has pointed
## user:// at across recent project renames/settings changes ("Slam City"
## -> "CITY X", then enabling use_custom_user_dir mid-session -- each of
## those silently moved where Godot resolves user:// to, orphaning
## whatever characters.cfg was sitting in the previous location) and
## copies whichever legacy file has the MOST saved characters into
## save_path. A no-op (cheap: one ConfigFile.load) once save_path already
## has real data, so it's safe to call unconditionally on every launch --
## this only ever fires for a player whose save data is currently
## unreachable, never overwrites an already-populated save file.
## legacy_candidates is injectable (same reasoning as save_path -- see this
## file's own class comment) so tests can point it at throwaway fixture
## paths instead of scanning a real player's actual APPDATA folder.
static func migrate_legacy_save_data(save_path: String = SAVE_PATH, legacy_candidates: Array[String] = []) -> void:
	if _count_sections(save_path) > 0:
		return
	var candidates := legacy_candidates
	if candidates.is_empty():
		var appdata := OS.get_environment("APPDATA")
		if appdata.is_empty():
			return
		candidates = [
			appdata + "/Godot/app_userdata/Slam City/characters.cfg",
			appdata + "/Godot/app_userdata/CITY X/characters.cfg",
			appdata + "/CITY X/characters.cfg",
			appdata + "/Slam City/characters.cfg",
		]
	var best_path := ""
	var best_count := 0
	for candidate in candidates:
		var count := _count_sections(candidate)
		if count > best_count:
			best_count = count
			best_path = candidate
	if best_path.is_empty():
		return
	var data := FileAccess.get_file_as_bytes(best_path)
	if data.is_empty():
		return
	var out := FileAccess.open(save_path, FileAccess.WRITE)
	if out == null:
		return
	out.store_buffer(data)
	out.close()

static func _count_sections(path: String) -> int:
	var config := ConfigFile.new()
	if config.load(path) != OK:
		return 0
	return config.get_sections().size()

## Adds additional_seconds to this character's own saved cumulative
## playtime -- called periodically (and on quit) by player.gd while playing
## AS this saved character, see its own PLAYTIME_SAVE_INTERVAL_SECONDS.
## Silently does nothing if this id no longer exists on disk (e.g. deleted
## from under a live session) rather than erroring.
static func update_playtime(id: int, additional_seconds: float, save_path: String = SAVE_PATH) -> void:
	var config := ConfigFile.new()
	if config.load(save_path) != OK:
		return
	var section := "character_%d" % id
	if not config.has_section(section):
		return
	var current: float = config.get_value(section, "playtime_seconds", 0.0)
	config.set_value(section, "playtime_seconds", current + additional_seconds)
	config.save(save_path)

## Overwrites this character's own saved level/xp/stat_points/skill_points
## and its 5 spent-stat-point allocations with the CURRENT values -- unlike
## update_playtime's own additional_seconds (a delta, since playtime only
## ever accumulates), every argument here is the live player.gd value as it
## stands right now, since level/xp/points can only ever mean one current
## state, not something to add on top of what's already saved. Called by
## player.gd alongside its own playtime flush (same
## PLAYTIME_SAVE_INTERVAL_SECONDS cadence + the same WM_CLOSE_REQUEST flush)
## rather than a separate timer, so this never needs its own accumulator.
## Silently does nothing if this id no longer exists on disk, same as
## update_playtime.
static func update_progress(id: int, level: int, xp: float, stat_points: int, skill_points: int, stat_health: int, stat_strength: int, stat_stamina: int, stat_mana: int, stat_speed: int, save_path: String = SAVE_PATH) -> void:
	var config := ConfigFile.new()
	if config.load(save_path) != OK:
		return
	var section := "character_%d" % id
	if not config.has_section(section):
		return
	config.set_value(section, "level", level)
	config.set_value(section, "xp", xp)
	config.set_value(section, "stat_points", stat_points)
	config.set_value(section, "skill_points", skill_points)
	config.set_value(section, "stat_health", stat_health)
	config.set_value(section, "stat_strength", stat_strength)
	config.set_value(section, "stat_stamina", stat_stamina)
	config.set_value(section, "stat_mana", stat_mana)
	config.set_value(section, "stat_speed", stat_speed)
	config.save(save_path)

## Overwrites this character's own saved inventory (both general storage
## and equipped-slot state) with the CURRENT live values -- same
## "overwrite with current live state, not a delta" shape as
## update_progress() (there's no natural "delta" for inventory contents the
## way playtime has one). ConfigFile.set_value already trusts native
## Variant serialization for Array/Dictionary values (same as this file's
## own atlas_png: PackedByteArray field), so storage_slots/equipment need
## no JSON-stringifying. Silently does nothing if this id no longer exists
## on disk, same as update_playtime/update_progress.
static func update_inventory(id: int, storage_slots: Array, equipment: Dictionary, save_path: String = SAVE_PATH) -> void:
	var config := ConfigFile.new()
	if config.load(save_path) != OK:
		return
	var section := "character_%d" % id
	if not config.has_section(section):
		return
	config.set_value(section, "inventory_storage", storage_slots)
	config.set_value(section, "inventory_equipment", equipment)
	config.save(save_path)

## Every saved character's own id/name/head_shape/playtime/level, in save
## order -- NOT atlas_png, xp, or the 5 spent-stat-point fields (see
## load_character for those), since character_select.gd's own list only
## needs enough to render a one-time portrait + level badge, no reason to
## load every single one just to show a list of rows.
static func list_characters(save_path: String = SAVE_PATH) -> Array[Dictionary]:
	var config := ConfigFile.new()
	if config.load(save_path) != OK:
		return []
	var out: Array[Dictionary] = []
	for section in config.get_sections():
		if not section.begins_with("character_"):
			continue
		var id := int(section.trim_prefix("character_"))
		out.append({
			"id": id,
			"name": config.get_value(section, "name", ""),
			"head_shape": config.get_value(section, "head_shape", "sphere"),
			"hair_style": config.get_value(section, "hair_style", "none"),
			# Defaulted (here and in load_character) rather than required:
			# every character saved before the creator had a height slider
			# has no such key at all, and reads back as the normal size.
			"height_scale": config.get_value(section, "height_scale", 1.0),
			"playtime_seconds": config.get_value(section, "playtime_seconds", 0.0),
			"level": config.get_value(section, "level", 1),
		})
	return out

## The full saved data for one character (including its atlas_png) -- an
## empty Dictionary if that id doesn't exist (e.g. the save file got deleted
## out from under a stale character_select.gd list; callers should treat
## that as "nothing to load" rather than crashing).
static func load_character(id: int, save_path: String = SAVE_PATH) -> Dictionary:
	var config := ConfigFile.new()
	if config.load(save_path) != OK:
		return {}
	var section := "character_%d" % id
	if not config.has_section(section):
		return {}
	return {
		"name": config.get_value(section, "name", ""),
		"head_shape": config.get_value(section, "head_shape", "sphere"),
		"hair_style": config.get_value(section, "hair_style", "none"),
		"height_scale": config.get_value(section, "height_scale", 1.0),
		"atlas_png": config.get_value(section, "atlas_png", PackedByteArray()),
		"playtime_seconds": config.get_value(section, "playtime_seconds", 0.0),
		"level": config.get_value(section, "level", 1),
		"xp": config.get_value(section, "xp", 0.0),
		"stat_points": config.get_value(section, "stat_points", 0),
		"skill_points": config.get_value(section, "skill_points", 0),
		"stat_health": config.get_value(section, "stat_health", 0),
		"stat_strength": config.get_value(section, "stat_strength", 0),
		"stat_stamina": config.get_value(section, "stat_stamina", 0),
		"stat_mana": config.get_value(section, "stat_mana", 0),
		"stat_speed": config.get_value(section, "stat_speed", 0),
		"inventory_storage": config.get_value(section, "inventory_storage", []),
		"inventory_equipment": config.get_value(section, "inventory_equipment", {}),
	}

## Permanently removes one saved character's own section -- used by
## character_select.gd's own delete flow (behind its own "are you sure"
## confirmation, this function itself trusts the caller and does not ask
## again). Silently does nothing if this id no longer exists on disk, same
## as update_playtime/update_progress/update_inventory.
static func delete_character(id: int, save_path: String = SAVE_PATH) -> void:
	var config := ConfigFile.new()
	if config.load(save_path) != OK:
		return
	var section := "character_%d" % id
	if not config.has_section(section):
		return
	config.erase_section(section)
	config.save(save_path)

static func _next_id(config: ConfigFile) -> int:
	var max_id := -1
	for section in config.get_sections():
		if section.begins_with("character_"):
			max_id = maxi(max_id, int(section.trim_prefix("character_")))
	return max_id + 1
