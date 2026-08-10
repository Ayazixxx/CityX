extends SceneTree
## One-off: exports every saved character's painted atlas out of the real
## user://characters.cfg into archive/old_character_paint/ as plain PNGs, so
## the artwork survives the player-model swap as files you can actually
## look at and reuse. Read-only against the save file -- it never writes to
## characters.cfg, so running it can't damage anything.
##
## The paint itself can't be carried onto the new model (it's mapped to the
## old procedural body's 7-part x 6-face UV atlas, which the new mesh
## doesn't share), so archiving it as images is the honest way to keep it.

const CharacterSave = preload("res://character/character_save.gd")
const OUT_DIR := "res://archive/old_character_paint"

func _init() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUT_DIR))

	# Keeps Godot from importing the archived PNGs as game resources -- they
	# are a keepsake, not assets the project uses.
	var ignore := FileAccess.open(OUT_DIR + "/.gdignore", FileAccess.WRITE)
	if ignore != null:
		ignore.store_string("")
		ignore.close()

	var config := ConfigFile.new()
	var save_path := CharacterSave.SAVE_PATH
	if config.load(save_path) != OK:
		print("no save file at ", save_path, " (nothing to archive)")
		quit(0)
		return

	var exported := 0
	var skipped := 0
	for section in config.get_sections():
		var char_name: String = config.get_value(section, "name", section)
		var png: PackedByteArray = config.get_value(section, "atlas_png", PackedByteArray())
		var level: int = config.get_value(section, "level", 1)
		if png.is_empty():
			print("  %s (%s): no paint saved, skipped" % [char_name, section])
			skipped += 1
			continue
		var safe := char_name.validate_filename()
		if safe.is_empty():
			safe = section
		var out_path := "%s/%s_%s.png" % [OUT_DIR, safe, section]
		var f := FileAccess.open(out_path, FileAccess.WRITE)
		if f == null:
			print("  FAILED to write ", out_path)
			continue
		f.store_buffer(png)
		f.close()
		print("  %s (level %d) -> %s  [%d bytes]" % [char_name, level, out_path.get_file(), png.size()])
		exported += 1

	print("archived %d painted character(s), %d without paint, from %s" % [exported, skipped, save_path])
	quit(0)
