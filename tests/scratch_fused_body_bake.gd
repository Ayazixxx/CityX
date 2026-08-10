extends Node3D
## Bakes FusedBody.build_uncached()'s output to disk as the shipped
## fused_body_cache.res -- see character/fused_body_cache.gd's own class
## comment for the measurement that motivated this (11-13 real seconds per
## Character, recomputing a byte-identical result every time).
##
## Re-run this whenever anything feeding the body's own geometry changes:
## fused_body.gd's SDF/bounds/CELL_SIZE/bone layout, or character.gd's atlas
## layout constants (PART_NAMES/PART_COUNT/ATLAS_CELL/FACE_COUNT). Not part
## of the game; run directly.

const FusedBody = preload("res://character/fused_body.gd")
const Character = preload("res://character/character.gd")
const FusedBodyCache = preload("res://character/fused_body_cache.gd")

func _ready() -> void:
	# Exactly what character.gd's own _build_fused_body() passes -- kept in
	# sync by deriving it from the SAME constants rather than re-listing
	# numbers here, so a change to either can't silently bake a mismatch.
	var atlas_row_for_bone := {}
	for bone_name in FusedBody.PAINT_BONES:
		atlas_row_for_bone[bone_name] = Character.PART_NAMES.find(bone_name)
	var atlas_size: int = Character.ATLAS_CELL * Character.FACE_COUNT

	print("building (this is the slow path being baked away)...")
	var t0 := Time.get_ticks_msec()
	var built: Dictionary = FusedBody.build_uncached(atlas_row_for_bone, Character.PART_COUNT, Character.ATLAS_CELL, atlas_size)
	print("build_uncached() took ", Time.get_ticks_msec() - t0, " ms")

	var cache := FusedBodyCache.new()
	cache.mesh = built["mesh"]
	cache.skin = built["skin"]
	cache.paint_positions = built["paint_positions"]
	cache.paint_uvs = built["paint_uvs"]
	cache.paint_clamp_flat = FusedBody.paint_clamp_to_flat(built["paint_clamp"])

	var err := ResourceSaver.save(cache, FusedBody.CACHE_PATH)
	if err != OK:
		print("FAIL saving ", FusedBody.CACHE_PATH, ": error ", err)
	else:
		print("saved ", FusedBody.CACHE_PATH)
		print("  surfaces=", cache.mesh.get_surface_count(), " paint_positions=", cache.paint_positions.size(), " paint_clamp_flat=", cache.paint_clamp_flat.size())
	get_tree().quit()
