extends StaticBody3D
## Half-Life-style breakable windows -- ONE INDIVIDUAL pane per window, not
## a shared strip covering a whole row (a real reported ask: "make the
## glass panels individual per window instead of the whole wall
## shattering" -- an earlier version deliberately traded this away for a
## much simpler per-ROW state machine, on the theory that "shooting ONE
## window cracks that whole row" was an acceptable simplification. It read
## wrong in practice: an entire wide strip visibly cracking/shattering
## together from a single hit didn't look like breaking ONE window, it
## looked like the wall itself was shattering. Fixed by tracking state per
## (wall, row, COLUMN) triple instead of just (wall, row) -- every
## individual window now cracks/shatters entirely independently of its
## neighbors, at the cost of real per-cell nodes (collision + visual, see
## setup()) instead of one shared pair per row. For a MAX_HEIGHT building
## with a wide facade this can mean on the order of a couple hundred extra
## nodes -- accepted deliberately, per this explicit ask, over the
## row-level version's own tighter node budget.
##
## Attached (via set_script, not a .tscn) to a building's exterior
## StaticBody3D by city_chunk.gd's own _build_building(), right after its
## normal wall collision/visual/interior are already built. Deliberately
## NOT a subclass instantiated from a scene: the body already exists (with
## its own facade mesh/collision children built by plain city_chunk.gd
## functions), and set_script() lets this attach real per-window behavior
## onto that SAME node in place -- same "attach via set_script directly
## onto an already-built node" pattern npc/crimson_claw_monster.gd's own
## _facing_flip/eye setup does NOT use (that one builds from scratch), but
## city_chunk.gd's caller here has no other way to add new methods to a
## plain StaticBody3D.new() after the fact. No _ready() reliance anywhere
## below -- setup() is called explicitly by the caller once, synchronously,
## so there's no ordering question about whether set_script() happening
## before or after the body enters the tree matters.
##
## Every window row on EVERY wall -- door wall included -- gets real
## breakable glass now (see city_chunk.gd's own _build_building, which hands
## `rows` as the building's full row count). The door wall's own ground row
## excludes whichever cell(s) overlap the door's own rectangle (see setup()'s
## own _door param/_is_door_cell) -- there's no real window hole there to
## fill, just the door's own already-walk-through opening, or solid wall
## immediately flanking it.
##
## One shot or melee hit cracks a window open (collision removed
## immediately -- walkable right away, matching the user's own "you can now
## just walk through it" wording) and swaps to a frosted/cracked look; a
## SECOND hit on an already-cracked window, OR a player's own body actually
## passing through the opening, fully shatters it (visual removed, a
## glass-shard burst + break sound play, matching weapons/glass_bottle.gd's
## own durability-state-machine spirit but simpler: 2 hits here, not 11).

enum CellState { INTACT, CRACKED, SHATTERED }

const GlassShatterScene := preload("res://weapons/glass_shatter.tscn")
const GlassBreakSound := preload("res://weapons/assets/audio/glass_break.mp3")
const GlassHitSound := preload("res://weapons/assets/audio/glass_hit_terrain.mp3")
# A CityChunk.WALL_THICKNESS/NOMINAL_COLUMN_METERS/WINDOW_MARGIN_RATIO
# reference, not re-declared local constants -- see city_chunk.gd's own
# _add_windowed_wall_row, which cuts the REAL hole this file's own glass
# pane has to line up with exactly, using this exact same margin math.
const CityChunk = preload("res://city/city_chunk.gd")

# All 4 wall defs (see city_chunk.gd's own _wall_defs), door wall included
# -- see setup()'s own _door param for how its ground row excludes the
# door's own opening from becoming glass.
var _walls: Array = []
var _v_row_size: float = 1.0
var _rows: int = 0
# Per-wall column layout -- unlike _v_row_size (shared by every wall, since
# every wall already uses the SAME v_layout row grid, see city_chunk.gd's
# own _build_building), each wall's own U-SPAN can differ (the two side
# walls span this building's own size.z, the back wall spans size.x), so
# column count/width is computed and cached per wall here instead of being
# one shared number.
var _cols: Array = [] # int, indexed [wall]
var _cell_width: Array = [] # float, indexed [wall]

# All four arrays below are indexed [wall_index][row][col] -- state is a
# plain CellState; the other three go null once that cell's own collision/
# visual/walkthrough-trigger no longer exists (removed on crack or
# shatter, see _crack_cell/_shatter_cell). _cell_area entries are left null
# until a cell actually cracks (see _spawn_walkthrough_area) -- same lazy
# "only build the trigger once it's actually needed" precedent the row-
# based version already established, kept here to avoid proactively
# doubling the per-cell node count for something most cells will never need.
var _cell_state: Array = []
var _cell_collision: Array = []
var _cell_visual: Array = []
var _cell_area: Array = []
var _glass_intact_mat: Material
var _glass_cracked_mat: Material
# The door's own {"u0","u1"} rectangle (see city_chunk.gd's own door dict) --
# only ever consulted for the door wall's row 0 (see _is_door_cell below;
# DOOR_HEIGHT_FLOORS is always 1, so the door never reaches row 1+).
var _door: Dictionary = {}

## door: city_chunk.gd's own door dict ({"u0","u1","height"}) -- the door
## wall (has_door=true in wall_defs) now gets real per-cell glass just like
## the other 3 walls, EXCEPT any row-0 cell overlapping the door's own
## [u0,u1] rectangle, which is excluded entirely (no collision/visual built,
## state starts SHATTERED so a hit there just declines -- see _is_door_cell)
## since city_chunk.gd's own _add_windowed_wall_row_excluding never cuts a
## real window hole there either (it's the door's own opening, or solid
## wall immediately flanking it).
func setup(wall_defs: Array, v_row_size: float, rows: int, glass_intact_mat: Material, glass_cracked_mat: Material, door: Dictionary) -> void:
	_walls = wall_defs
	_v_row_size = v_row_size
	_rows = rows
	_glass_intact_mat = glass_intact_mat
	_glass_cracked_mat = glass_cracked_mat
	_door = door
	_cols.resize(_walls.size())
	_cell_width.resize(_walls.size())
	_cell_state.resize(_walls.size())
	_cell_collision.resize(_walls.size())
	_cell_visual.resize(_walls.size())
	_cell_area.resize(_walls.size())
	for wi in range(_walls.size()):
		var wall: Dictionary = _walls[wi]
		# Same column layout city_chunk.gd's own _add_windowed_wall_row
		# already computes for this exact wall -- MUST match exactly (any
		# drift here would misalign this pane against the real hole cut
		# into the wall), so this uses the identical CityChunk helper
		# rather than re-deriving the math independently.
		var u_layout: Dictionary = CityChunk._cell_layout(wall["u1"] - wall["u0"], CityChunk.NOMINAL_COLUMN_METERS)
		var cols: int = u_layout["count"]
		_cols[wi] = cols
		_cell_width[wi] = u_layout["size"]

		var wall_states: Array = []
		var wall_collisions: Array = []
		var wall_visuals: Array = []
		var wall_areas: Array = []
		wall_states.resize(_rows)
		wall_collisions.resize(_rows)
		wall_visuals.resize(_rows)
		wall_areas.resize(_rows)
		for r in range(_rows):
			var row_states: Array = []
			var row_collisions: Array = []
			var row_visuals: Array = []
			var row_areas: Array = []
			row_states.resize(cols)
			row_collisions.resize(cols)
			row_visuals.resize(cols)
			row_areas.resize(cols)
			for c in range(cols):
				if _is_door_cell(wi, r, c):
					# No real window hole exists here (see this function's
					# own comment) -- SHATTERED with null collision/visual
					# reads as "already gone, nothing to hit or show,"
					# exactly matching _hit_cell's own default decline for
					# that state, without needing a separate sentinel.
					row_states[c] = CellState.SHATTERED
					row_collisions[c] = null
					row_visuals[c] = null
				else:
					row_states[c] = CellState.INTACT
					row_collisions[c] = _build_cell_collision(wi, r, c)
					row_visuals[c] = _build_cell_visual(wi, r, c)
			wall_states[r] = row_states
			wall_collisions[r] = row_collisions
			wall_visuals[r] = row_visuals
			wall_areas[r] = row_areas
		_cell_state[wi] = wall_states
		_cell_collision[wi] = wall_collisions
		_cell_visual[wi] = wall_visuals
		_cell_area[wi] = wall_areas

## True if cell (wi, row, col) overlaps the door's own rectangle -- only
## possible on the has_door wall's row 0 (see setup's own comment).
func _is_door_cell(wi: int, row: int, col: int) -> bool:
	var wall: Dictionary = _walls[wi]
	if not wall.get("has_door", false) or row != 0:
		return false
	var cell_width: float = _cell_width[wi]
	var col_u0: float = float(wall["u0"]) + float(col) * cell_width
	var col_u1: float = col_u0 + cell_width
	return col_u1 > float(_door["u0"]) and col_u0 < float(_door["u1"])

## The individual window's own INSET rectangle (matching the real hole
## city_chunk.gd's own _add_windowed_wall_row cuts into the wall for this
## exact cell -- same WINDOW_MARGIN_RATIO margin, not the cell's own full
## (frame-included) footprint), in this wall's own 2D panel space.
func _wall_cell_rect(wi: int, row: int, col: int) -> Dictionary:
	var wall: Dictionary = _walls[wi]
	var cell_width: float = _cell_width[wi]
	var col_u0: float = wall["u0"] + float(col) * cell_width
	var col_u1: float = col_u0 + cell_width
	var row_y0: float = float(row) * _v_row_size
	var row_y1: float = row_y0 + _v_row_size
	var margin_u: float = cell_width * CityChunk.WINDOW_MARGIN_RATIO
	var margin_v: float = _v_row_size * CityChunk.WINDOW_MARGIN_RATIO
	return {
		"u0": col_u0 + margin_u, "u1": col_u1 - margin_u,
		"y0": row_y0 + margin_v, "y1": row_y1 - margin_v,
	}

func _cell_center_local(wi: int, row: int, col: int) -> Vector3:
	var rect := _wall_cell_rect(wi, row, col)
	var to_world: Callable = _walls[wi]["to_world"]
	return to_world.call((rect["u0"] + rect["u1"]) / 2.0, (rect["y0"] + rect["y1"]) / 2.0)

func _build_cell_collision(wi: int, row: int, col: int) -> CollisionShape3D:
	var wall: Dictionary = _walls[wi]
	var rect := _wall_cell_rect(wi, row, col)
	var normal: Vector3 = wall["normal"]
	var width: float = rect["u1"] - rect["u0"]
	var height: float = rect["y1"] - rect["y0"]
	var shape := BoxShape3D.new()
	# Every wall in this project is axis-aligned (never at an arbitrary
	# yaw), so which world axis the box's own thin dimension runs along is
	# just whichever axis this wall's own normal points down -- no basis
	# rotation needed, matching _add_box_collision's own plain axis-aligned
	# convention elsewhere in city_chunk.gd.
	if absf(normal.x) > 0.5:
		shape.size = Vector3(CityChunk.WALL_THICKNESS, height, width)
	else:
		shape.size = Vector3(width, height, CityChunk.WALL_THICKNESS)
	var collision := CollisionShape3D.new()
	collision.shape = shape
	collision.position = _cell_center_local(wi, row, col)
	add_child(collision)
	return collision

func _build_cell_visual(wi: int, row: int, col: int) -> MeshInstance3D:
	var wall: Dictionary = _walls[wi]
	var rect := _wall_cell_rect(wi, row, col)
	var to_world: Callable = wall["to_world"]
	var normal: Vector3 = wall["normal"]
	# Sits a hair proud of the (opaque) frame mesh right behind it along the
	# wall's own outward normal, purely to avoid z-fighting -- not a
	# meaningful visual gap.
	var inset: Vector3 = normal * 0.02
	var corners: Array = [
		to_world.call(rect["u0"], rect["y0"]) + inset,
		to_world.call(rect["u1"], rect["y0"]) + inset,
		to_world.call(rect["u1"], rect["y1"]) + inset,
		to_world.call(rect["u0"], rect["y1"]) + inset,
	]
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	# _add_flat_quad is a generic, parameter-driven helper (no city_chunk-
	# specific state) already used for every other plain-color surface in
	# that file (roof cap, interior walls) -- reused directly here rather
	# than duplicating its winding-correction logic.
	CityChunk._add_flat_quad(st, corners, normal)
	var mesh_inst := MeshInstance3D.new()
	mesh_inst.mesh = st.commit()
	mesh_inst.material_override = _glass_intact_mat
	# Sitting proud of the wall's own opaque frame mesh (see `inset` above)
	# means this pane's own shadow -- cast_shadow defaults to ON -- lands
	# just barely offset from the wall's own shadow instead of exactly
	# overlapping it, so every single window would draw its own extra
	# hard-edged rectangular shadow seam over the wall's own shadow
	# (reported directly, back when this was per-row: "building shadow is
	# blocky with straight lines... not right, just have 1 shadow" -- now
	# with a real pane per window instead of per row, leaving this on would
	# make that same artifact far worse, not better). A semi-transparent
	# glass pane shouldn't cast a second solid shadow duplicating the
	# opaque wall right behind it anyway, so this is off entirely rather
	# than just fixed up.
	mesh_inst.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(mesh_inst)
	return mesh_inst

## Called by pistol.gd's own fire() and player.gd's own _punch()/
## _try_swing_bottle() -- same duck-typed has_method("try_break_glass")
## pattern every other hit-routing branch in this project already uses
## (take_damage, etc.). Returns true if the hit actually landed on
## breakable glass (a frame/mullion hit, or a hit on the door wall's own
## excluded door-cell, returns false so the caller's own normal wall-hit
## handling still applies there).
func try_break_glass(hit_position: Vector3, hit_normal: Vector3) -> bool:
	var local: Vector3 = to_local(hit_position)
	var wi := _closest_wall_index(hit_normal, local)
	if wi < 0:
		return false
	var row := floori(local.y / _v_row_size)
	if row < 0 or row >= _rows:
		return false
	var wall: Dictionary = _walls[wi]
	var normal: Vector3 = wall["normal"]
	var along_x := absf(normal.x) < 0.5 # true: this wall's own "u" runs along world X (the back wall); false: along world Z (left/right walls)
	var local_u: float = local.x if along_x else local.z
	var col := floori((local_u - float(wall["u0"])) / _cell_width[wi])
	if col < 0 or col >= _cols[wi]:
		return false
	return _hit_cell(wi, row, col)

## hit_normal arrives in WORLD space (straight from a PhysicsRayQueryParameters3D
## result) -- every city building is axis-aligned with no rotation of its
## own (confirmed: city.gd never rotates a chunk's root), so a plain
## (unscaled) basis inverse correctly maps it into this body's own local
## space, same as any other direction vector. local_position is the hit
## point already in this body's own local space (see try_break_glass).
##
## Matched on the wall's own AXIS (absolute dot), not its signed direction.
## A real reported bug: "not letting me break and jump through a window from
## inside a building." Hitting a pane from OUTSIDE returns a surface normal
## pointing outward, which lines up with that wall's own outward normal
## (dot ~ +1); hitting the SAME pane from INSIDE returns the opposite normal
## (dot ~ -1), so the correct wall scored WORST and a perpendicular wall
## (dot ~ 0) won instead -- the row/column lookup then landed off that
## wrong wall's grid and the hit was simply declined. Every window in the
## game was therefore unbreakable from indoors.
##
## Taking absf() alone isn't enough: it makes a wall and the one directly
## opposite it (+X vs -X) tie at 1.0. The tie is broken by which plane the
## hit point actually lies on -- every wall sits at its own fixed offset
## along its own normal (see city_chunk.gd's own _wall_defs), and the
## building's local origin is its center, so the nearest plane is
## unambiguously the wall that was hit.
func _closest_wall_index(hit_normal: Vector3, local_position: Vector3) -> int:
	var local_normal: Vector3 = global_transform.basis.inverse() * hit_normal
	var best_i := -1
	var best_distance := INF
	for i in range(_walls.size()):
		var wall: Dictionary = _walls[i]
		var normal: Vector3 = wall["normal"]
		# A hit on the roof (not in _walls at all) has no wall axis to match,
		# so it's rejected here rather than being misattributed to whichever
		# wall happened to be least wrong.
		if absf(local_normal.dot(normal)) < 0.5:
			continue
		var to_world: Callable = wall["to_world"]
		var plane_offset: float = (to_world.call(0.0, 0.0) as Vector3).dot(normal)
		var distance: float = absf(local_position.dot(normal) - plane_offset)
		if distance < best_distance:
			best_distance = distance
			best_i = i
	return best_i

func _hit_cell(wi: int, row: int, col: int) -> bool:
	match _cell_state[wi][row][col]:
		CellState.INTACT:
			_crack_cell(wi, row, col)
			return true
		CellState.CRACKED:
			_shatter_cell(wi, row, col)
			return true
		_:
			return false

## First landed hit -- collision opens immediately (matching "you can now
## just walk through it"), visual swaps to a cracked/frosted look, and a
## trigger-only Area3D starts watching for a player actually walking
## through the new opening (see _on_walkthrough_body_entered). Only THIS
## one window is affected -- its neighbors, even in the same row, are
## entirely separate cells with their own independent state.
func _crack_cell(wi: int, row: int, col: int) -> void:
	_cell_state[wi][row][col] = CellState.CRACKED
	var collision: CollisionShape3D = _cell_collision[wi][row][col]
	if collision != null:
		collision.queue_free()
		_cell_collision[wi][row][col] = null
	var visual: MeshInstance3D = _cell_visual[wi][row][col]
	if visual != null:
		visual.material_override = _glass_cracked_mat
	_play_glass_sound(GlassHitSound, to_global(_cell_center_local(wi, row, col)))
	_spawn_walkthrough_area(wi, row, col)

## Second hit on an already-cracked window, OR a player walking through it --
## the glass is fully gone now: visual removed, a real shard burst + break
## sound play. Collision was already open since _crack_cell, nothing to
## change there.
func _shatter_cell(wi: int, row: int, col: int) -> void:
	_cell_state[wi][row][col] = CellState.SHATTERED
	var visual: MeshInstance3D = _cell_visual[wi][row][col]
	if visual != null:
		visual.queue_free()
		_cell_visual[wi][row][col] = null
	var area: Area3D = _cell_area[wi][row][col]
	if area != null:
		area.queue_free()
		_cell_area[wi][row][col] = null
	var world_pos: Vector3 = to_global(_cell_center_local(wi, row, col))
	var shatter: GPUParticles3D = GlassShatterScene.instantiate()
	get_tree().current_scene.add_child(shatter)
	shatter.global_position = world_pos
	_play_glass_sound(GlassBreakSound, world_pos)

func _spawn_walkthrough_area(wi: int, row: int, col: int) -> void:
	var wall: Dictionary = _walls[wi]
	var rect := _wall_cell_rect(wi, row, col)
	var normal: Vector3 = wall["normal"]
	var width: float = rect["u1"] - rect["u0"]
	var height: float = rect["y1"] - rect["y0"]
	var area := Area3D.new()
	area.collision_layer = 0
	area.collision_mask = 4 # characters only (players AND hostile NPCs alike can trigger it -- see _on_walkthrough_body_entered for the players-only gate on what actually SHATTERS it)
	area.position = _cell_center_local(wi, row, col)
	var shape := BoxShape3D.new()
	# A bit of real depth (not paper-thin) so a body actually passing
	# THROUGH the opening triggers it, not just brushing past outside.
	if absf(normal.x) > 0.5:
		shape.size = Vector3(1.0, height, width)
	else:
		shape.size = Vector3(width, height, 1.0)
	var collision := CollisionShape3D.new()
	collision.shape = shape
	area.add_child(collision)
	add_child(area)
	area.body_entered.connect(_on_walkthrough_body_entered.bind(wi, row, col))
	_cell_area[wi][row][col] = area

## Only a real PLAYER walking through finishes off a cracked window (matching
## the user's own "you walk through it and it will shatter" wording) -- a
## hostile NPC wandering through wouldn't read as a deliberate player
## action the same way.
func _on_walkthrough_body_entered(body: Node3D, wi: int, row: int, col: int) -> void:
	if _cell_state[wi][row][col] != CellState.CRACKED:
		return
	if not body.is_in_group("players"):
		return
	_shatter_cell(wi, row, col)

## Temporary, self-freeing 3D one-shot player -- same idiom
## weapons/thrown_bottle.gd's own _play_impact_sound already establishes
## (a child AudioStreamPlayer3D would otherwise cut off mid-playback the
## instant a cell's own node tree gets torn down around it, if it were
## parented under something this function frees).
func _play_glass_sound(stream: AudioStream, at_position: Vector3) -> void:
	var player := AudioStreamPlayer3D.new()
	player.stream = stream
	get_tree().current_scene.add_child(player)
	player.global_position = at_position
	player.play()
	player.finished.connect(player.queue_free)
