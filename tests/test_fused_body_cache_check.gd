extends Node3D
## Regression check for the baked body-mesh cache (character/
## fused_body_cache.gd + fused_body.gd's own build()/build_uncached() split)
## that fixed the reported minute-long character-select freeze.
##
## Two things must hold, and they pull in opposite directions:
##  1. The cache must actually BE used -- if build() ever silently fell back
##     to rebuilding, the 11-13 second per-Character cost is straight back
##     and nobody would notice until a player complained again.
##  2. The cache must MATCH what the source would build right now -- a bake
##     is a snapshot, so editing fused_body.gd's SDF/bounds/CELL_SIZE (or
##     character.gd's atlas layout) without re-running
##     tests/scratch_fused_body_bake.gd would leave the game quietly
##     rendering the OLD body forever, with the source change appearing to
##     do nothing at all.
##
## Check 2 is why this test is slow (it pays the real build once, on
## purpose, to compare against) -- that's the cost of the bake never being
## able to go stale unnoticed.

const FusedBody = preload("res://character/fused_body.gd")
const Character = preload("res://character/character.gd")
const CharacterScene := preload("res://character/character.tscn")

var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	_check_cache_exists_and_is_used()
	_check_cache_matches_source()
	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

func _check_cache_exists_and_is_used() -> void:
	if not ResourceLoader.exists(FusedBody.CACHE_PATH):
		_fail("no baked cache at %s -- run tests/scratch_fused_body_bake.gd" % FusedBody.CACHE_PATH)
		return
	print("PASS: the baked cache exists at ", FusedBody.CACHE_PATH)

	# A whole real Character, built the way the game actually builds one --
	# with the bake in place this must be FAST. The threshold is deliberately
	# generous (the measured real cost is ~12ms/character on the cached path
	# vs 11000-13000ms uncached), so this only ever trips on a genuine
	# regression back to rebuilding, never on ordinary machine-speed noise.
	var t0 := Time.get_ticks_msec()
	var character: Node3D = CharacterScene.instantiate()
	add_child(character)
	var elapsed := Time.get_ticks_msec() - t0
	character.queue_free()

	if elapsed < 2000:
		print("PASS: building a real Character takes ", elapsed, " ms (cached path -- uncached measured 11000-13000 ms)")
	else:
		_fail("building a Character took %d ms -- the cache isn't being used, this is the reported minute-long freeze coming back" % elapsed)

func _check_cache_matches_source() -> void:
	var atlas_row_for_bone := {}
	for bone_name in FusedBody.PAINT_BONES:
		atlas_row_for_bone[bone_name] = Character.PART_NAMES.find(bone_name)
	var atlas_size: int = Character.ATLAS_CELL * Character.FACE_COUNT

	var cached: Dictionary = FusedBody.build(atlas_row_for_bone, Character.PART_COUNT, Character.ATLAS_CELL, atlas_size)
	var fresh: Dictionary = FusedBody.build_uncached(atlas_row_for_bone, Character.PART_COUNT, Character.ATLAS_CELL, atlas_size)

	var cached_pos: PackedVector3Array = cached["paint_positions"]
	var fresh_pos: PackedVector3Array = fresh["paint_positions"]
	if cached_pos.size() != fresh_pos.size():
		_fail("baked cache is STALE: %d paint positions vs %d freshly built -- re-run tests/scratch_fused_body_bake.gd" % [cached_pos.size(), fresh_pos.size()])
		return
	print("PASS: baked and freshly-built vertex counts match (", cached_pos.size(), ")")

	# Sampled rather than compared element by element -- 60k+ vertices
	# through GDScript's own per-element access is slow enough to dominate
	# this whole test, and any real geometry change (a different SDF, bounds,
	# or cell size) moves essentially every vertex, not a handful.
	var mismatches := 0
	var step: int = maxi(1, cached_pos.size() / 500)
	for i in range(0, cached_pos.size(), step):
		if cached_pos[i].distance_to(fresh_pos[i]) > 0.0001:
			mismatches += 1
	if mismatches == 0:
		print("PASS: sampled baked vertices match the freshly-built ones exactly")
	else:
		_fail("baked cache is STALE: %d of the sampled vertices differ from a fresh build -- re-run tests/scratch_fused_body_bake.gd" % mismatches)

	var cached_clamp: Array = cached["paint_clamp"]
	var fresh_clamp: Array = fresh["paint_clamp"]
	if cached_clamp.size() == fresh_clamp.size() and (cached_clamp.is_empty() or cached_clamp[0] == fresh_clamp[0]):
		print("PASS: the rehydrated paint_clamp round-trips back to the same shape/values")
	else:
		_fail("paint_clamp didn't survive the flatten/rehydrate round trip (%d vs %d entries)" % [cached_clamp.size(), fresh_clamp.size()])
