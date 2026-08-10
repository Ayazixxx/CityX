extends Node3D
## One-off profiler for the reported character-select load freeze ("my game
## should [not] freeze for a whole minute when i click singleplayer while i
## wait for the list of characters to load"). Times each phase of what
## ui/character_select.gd's own _render_portraits/_capture_one_portrait
## actually does per saved character, so the fix targets whatever's really
## slow instead of a guess. Not part of the game; run directly.

const CharacterSave = preload("res://character/character_save.gd")
const CharacterScene := preload("res://character/character.tscn")

const CHARACTER_COUNT := 5

var _test_path: String = "user://scratch_load_profile_%d.cfg" % randi()

func _ms() -> int:
	return Time.get_ticks_msec()

func _ready() -> void:
	# A realistic saved-character payload: a REAL png-encoded atlas the same
	# size character.gd actually produces, not a stub, so apply_atlas_png's
	# own decode cost is measured honestly.
	var img := Image.create(384, 448, false, Image.FORMAT_RGBA8)
	img.fill(Color(0.8, 0.3, 0.3))
	var png: PackedByteArray = img.save_png_to_buffer()
	print("atlas png bytes: ", png.size())

	for i in range(CHARACTER_COUNT):
		CharacterSave.save_new_character("Prof%d" % i, "sphere", png, _test_path)

	var t0 := _ms()
	var entries: Array[Dictionary] = CharacterSave.list_characters(_test_path)
	print("list_characters(): ", _ms() - t0, " ms for ", entries.size(), " entries")

	var viewport := SubViewport.new()
	viewport.size = Vector2i(128, 128)
	add_child(viewport)
	var cam := Camera3D.new()
	cam.current = true
	viewport.add_child(cam)

	var total_instantiate := 0
	var total_add_child := 0
	var total_load := 0
	var total_head := 0
	var total_atlas := 0
	var total_frames := 0
	var total_capture := 0
	var grand0 := _ms()

	for entry in entries:
		var id: int = entry["id"]

		var a := _ms()
		var data: Dictionary = CharacterSave.load_character(id, _test_path)
		total_load += _ms() - a

		a = _ms()
		var character: Node3D = CharacterScene.instantiate()
		total_instantiate += _ms() - a

		a = _ms()
		character.set_paintable(false)
		viewport.add_child(character) # triggers _ready(): the real build work
		total_add_child += _ms() - a

		a = _ms()
		character.set_head_shape(data["head_shape"])
		total_head += _ms() - a

		a = _ms()
		character.apply_atlas_png(data["atlas_png"])
		total_atlas += _ms() - a

		a = _ms()
		await get_tree().process_frame
		await get_tree().process_frame
		total_frames += _ms() - a

		a = _ms()
		var tex := viewport.get_texture()
		if tex != null:
			var _img2 := tex.get_image()
		total_capture += _ms() - a

		character.queue_free()
		await get_tree().process_frame

	var grand := _ms() - grand0
	print("--- per-run totals over ", entries.size(), " characters ---")
	print("load_character():        ", total_load, " ms")
	print("CharacterScene.instantiate(): ", total_instantiate, " ms")
	print("add_child (runs _ready()):    ", total_add_child, " ms   <-- procedural build")
	print("set_head_shape():        ", total_head, " ms")
	print("apply_atlas_png():       ", total_atlas, " ms")
	print("awaited frames:          ", total_frames, " ms")
	print("viewport get_image():    ", total_capture, " ms")
	print("GRAND TOTAL:             ", grand, " ms")
	print("per character:           ", grand / maxi(entries.size(), 1), " ms")

	var dir := DirAccess.open("user://")
	if dir != null and dir.file_exists(_test_path.trim_prefix("user://")):
		dir.remove(_test_path.trim_prefix("user://"))
	get_tree().quit()
