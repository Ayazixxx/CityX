extends Resource
## The baked output of FusedBody.build() (see fused_body.gd), saved to disk
## once and loaded at runtime instead of being recomputed per Character.
##
## Why this exists, measured directly (tests/scratch_load_profile.gd): a
## single Character._ready() spent 11-13 REAL SECONDS inside
## _build_fused_body(), essentially all of it in FusedBody.build()'s own
## marching-tetrahedra SDF extraction + hole-filling/flap-removal/welding
## passes, all running in interpreted GDScript. Every other build step in
## _ready() combined measured under 10ms. With 5 saved characters,
## ui/character_select.gd's own portrait pass instantiates 5 of them back to
## back -- 66 real seconds of hard freeze on a screen that only needed to
## draw 5 little head-and-shoulders thumbnails (reported directly: "my game
## should [not] freeze for a whole minute when i click singleplayer").
##
## The fix is possible at all because build()'s output is a PURE FUNCTION OF
## COMPILE-TIME CONSTANTS: its sdf/bounds/cell-size inputs are all consts in
## fused_body.gd, and character.gd passes only its own consts (PART_NAMES/
## PART_COUNT/ATLAS_CELL/atlas_size) for the atlas layout. Every character in
## the game therefore gets a byte-identical body mesh -- what actually makes
## one character look different is the paint ATLAS TEXTURE (per-instance, see
## character.gd's own _build_atlas/apply_atlas_png) and the separately-built
## head (see _build_head, measured at ~5ms), neither of which is in here.
##
## Sharing the same ArrayMesh/Skin across every Character instance is normal
## Godot practice (a PackedScene does exactly this) and safe here
## specifically because nothing mutates them per instance: the per-character
## paint goes to a per-instance material_override's own texture, never to
## mesh data, and character.gd's own paint arrays are .duplicate()d into its
## working copies (see _rebuild_paint_arrays) rather than written in place.
##
## Regenerate with tests/scratch_fused_body_bake.gd whenever anything that
## feeds the body's own geometry changes (fused_body.gd's SDF/bounds/cell
## size/bone layout, or character.gd's atlas layout constants) -- otherwise
## the stale baked mesh keeps being loaded and the source change silently
## does nothing. tests/test_fused_body_cache_check.gd guards exactly that:
## it rebuilds from source and fails if the bake no longer matches.

@export var mesh: ArrayMesh
@export var skin: Skin
@export var paint_positions: PackedVector3Array
@export var paint_uvs: PackedVector2Array
## Flattened 4-ints-per-triangle (x0, x1, y0, y1) rather than the
## Array[Dictionary] build() itself returns -- a PackedInt32Array is a
## single compact buffer in the .res instead of thousands of individually
## serialized Dictionary variants. Rehydrated back into the dictionary shape
## character.gd's own paint code expects by FusedBody.build() (see
## _paint_clamp_from_flat).
@export var paint_clamp_flat: PackedInt32Array
