extends SceneTree
## Bakes one character drawn in the game's own creator out of the real
## user://characters.cfg and into the project as an npc/designs/*.tres (see
## npc/npc_design.gd) -- the one manual step between "I drew a guy" and "he's
## standing in the world."
##
## Read-only against the save file: it never writes to characters.cfg, so
## running it cannot damage a real character. Re-running it for the same
## name just overwrites that design's own .tres with whatever is currently
## saved, which is how you'd pick up a repaint.
##
## Usage (name match is case/space-insensitive, so "old guy" finds "Old Guy"):
##   godot --headless --path . --script tests/scratch_bake_npc_design.gd -- "Old Guy"
## With no argument it lists what's actually in the save file and exits,
## which is also what it does when the name doesn't match anything -- a
## silent "baked nothing" would be far worse than a printed list.
##
## An optional SECOND argument overrides which save file to read:
##   ... -- "Old Guy" res://characters_real.cfg
## That exists because user:// is not always reachable from wherever this
## script is being run. Confirmed the hard way: the Claude desktop app runs
## its tooling inside a Windows MSIX container, which virtualizes %APPDATA%
## copy-on-write -- once anything in the container had written
## characters.cfg, every subsequent read from inside it was silently
## redirected to a frozen private copy while still REPORTING the real path,
## so the file looked days out of date and was missing characters that were
## definitely saved. Pointing this at a hand-copied file placed somewhere
## outside the virtualized tree (the project folder is not redirected) is
## the reliable way through that.

const CharacterSave = preload("res://character/character_save.gd")
const NpcDesignScript = preload("res://npc/npc_design.gd")
const OUT_DIR := "res://npc/designs"

func _init() -> void:
	var wanted := ""
	var args := OS.get_cmdline_user_args()
	if args.size() > 0:
		wanted = args[0]
	var save_path := CharacterSave.SAVE_PATH
	if args.size() > 1:
		save_path = args[1]

	print("reading: ", save_path)
	var characters := CharacterSave.list_characters(save_path)
	if characters.is_empty():
		print("no saved characters at ", save_path, " -- nothing to bake")
		quit(1)
		return

	if wanted == "":
		print("saved characters:")
		for entry in characters:
			print("  [%d] %s" % [entry["id"], entry["name"]])
		print("pass one of those names as an argument to bake it")
		quit(1)
		return

	var match_id := -1
	var match_name := ""
	for entry in characters:
		if _slug(str(entry["name"])) == _slug(wanted):
			match_id = int(entry["id"])
			match_name = str(entry["name"])
			break

	if match_id < 0:
		print("no saved character named '", wanted, "'. saved characters:")
		for entry in characters:
			print("  [%d] %s" % [entry["id"], entry["name"]])
		quit(1)
		return

	var data := CharacterSave.load_character(match_id, save_path)
	var atlas_png: PackedByteArray = data["atlas_png"]
	if atlas_png.is_empty():
		print("'", match_name, "' has no painted atlas saved -- refusing to bake an empty design")
		quit(1)
		return

	var design: Resource = NpcDesignScript.new()
	design.display_name = match_name
	design.atlas_png = atlas_png
	design.head_shape = data["head_shape"]
	design.hair_style = data["hair_style"]
	design.height_scale = data["height_scale"]

	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUT_DIR))
	var out_path := "%s/%s.tres" % [OUT_DIR, _slug(match_name)]
	var err := ResourceSaver.save(design, out_path)
	if err != OK:
		print("failed to write ", out_path, " (error ", err, ")")
		quit(1)
		return

	print("baked '%s' -> %s" % [match_name, out_path])
	print("  head_shape:   ", design.head_shape)
	print("  hair_style:   ", design.hair_style)
	print("  height_scale: ", design.height_scale)
	print("  atlas_png:    %d bytes" % atlas_png.size())
	quit(0)

## "Old Guy" -> "old_guy". Used both for matching the requested name against
## what's saved (so capitalisation/spacing in the argument doesn't matter)
## and for the output filename.
func _slug(text: String) -> String:
	var out := ""
	for c in text.strip_edges().to_lower():
		if c == " " or c == "-":
			out += "_"
		elif (c >= "a" and c <= "z") or (c >= "0" and c <= "9") or c == "_":
			out += c
	return out
