extends Node3D
## Regression check for city/building_glass.gd -- windows are uniform-
## colored and transparent (see city_chunk.gd's own GLASS_COLOR -- the only
## thing that ever renders as a window now, see build_facade_material's own
## comment), and shooting/meleeing a window (try_break_glass) cracks it
## open (collision removed, walkable immediately) on the first hit, then
## fully shatters it (visual removed, shard effect + break sound) on a
## second hit OR a player walking through the cracked opening --
## INDIVIDUALLY, per window: a real reported ask ("make the glass panels
## individual per window instead of the whole wall shattering") replaced
## the original per-ROW state machine with a per-(wall,row,COLUMN) one, so
## this file also specifically checks that breaking one window leaves its
## immediate neighbors untouched. Also covers this session's later rework
## making EVERY window on EVERY wall (door wall included) real and
## breakable -- see checks 5, 6, 9, 10 below -- after the earlier decorative-
## overlay-only treatment for upper floors/the door wall was reported as
## "still solid" (glass in front of a still-solid wall can never actually
## read as see-through, no matter how the material is tuned). Runs headless:
## pure generation + physics, no rendering needed.

const CityChunk = preload("res://city/city_chunk.gd")

var _ok := true

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

func _ready() -> void:
	call_deferred("_run_checks")

func _run_checks() -> void:
	# --- 1. Windows are uniform across every facade variant, and the glass
	# overlay materials are real transparent glass. ---
	var any_variant_window := false
	for variant in CityChunk.FACADE_VARIANTS:
		if variant.has("window"):
			any_variant_window = true
	if any_variant_window:
		_fail("a FACADE_VARIANTS entry still carries its own per-variant window color")
	else:
		print("PASS: every facade variant shares one uniform window color")

	var materials := CityChunk.build_materials()
	var glass_intact: StandardMaterial3D = materials["glass_intact"]
	var glass_cracked: StandardMaterial3D = materials["glass_cracked"]
	if glass_intact.transparency == BaseMaterial3D.TRANSPARENCY_ALPHA and glass_intact.albedo_color.a < 1.0:
		print("PASS: intact glass material is real transparent glass")
	else:
		_fail("intact glass material isn't transparent")
	if glass_cracked.transparency == BaseMaterial3D.TRANSPARENCY_ALPHA:
		print("PASS: cracked glass material is also transparent")
	else:
		_fail("cracked glass material isn't transparent")

	# --- 2. A real building, built directly (same pattern
	# test_multifloor_check.gd's own direct _build_building() call already
	# establishes). Tall enough that MAX_INTERIOR_FLOORS caps its real
	# interior well short of the full height -- see _build_building's own
	# comment -- specifically so check 6 below has a real "above the real
	# decorated interior, still breakable" hollow-shell region to exercise
	# (every row is real glass now regardless of real floor count, but a
	# short test building wouldn't have any such region left to test).
	var size := Vector3(16, 60, 16)
	CityChunk._build_building(self, size, Vector3(0, 0, 0), "", materials)

	var building: Node3D = null
	for child in get_children():
		if child is StaticBody3D and child.has_method("try_break_glass"):
			building = child
			break
	if building == null:
		_fail("no building_glass-scripted StaticBody3D found after _build_building()")
		print("OVERALL: FAIL")
		get_tree().quit(1)
		return
	print("PASS: the building's exterior body has real breakable-glass behavior attached")

	# --- 2b. Every window's own visual mesh has shadow casting off -- it
	# sits proud of the wall's opaque frame mesh right behind it (see
	# building_glass.gd's own _build_cell_visual comment), so leaving the
	# engine's default cast_shadow=ON would draw an extra hard-edged
	# rectangular shadow seam over the wall's own shadow for every single
	# window (reported directly, back when this was per-row, as "building
	# shadow is blocky with straight lines... not right, just have 1
	# shadow" -- now that every INDIVIDUAL window gets its own pane instead
	# of one per row, leaving this on would multiply that same artifact by
	# however many columns a wall has). ---
	var shadow_off_ok := true
	var any_cell_checked := false
	for wi in range(building._cell_visual.size()):
		for row in range(building._cell_visual[wi].size()):
			for col in range(building._cell_visual[wi][row].size()):
				any_cell_checked = true
				var visual: MeshInstance3D = building._cell_visual[wi][row][col]
				if visual != null and visual.cast_shadow != GeometryInstance3D.SHADOW_CASTING_SETTING_OFF:
					shadow_off_ok = false
	if shadow_off_ok and any_cell_checked:
		print("PASS: every individual window's own visual mesh has shadow casting disabled")
	else:
		_fail("a window's visual mesh still casts its own shadow, doubling up on the wall's")

	# --- 3. Shooting a plain wall (wall index 0, normal +X) at row 0,
	# column 0 cracks JUST that one window: collision opens immediately
	# (walkable), state becomes CRACKED -- aimed at column 0's own real
	# center (not z=0, the wall's own midpoint) so the hit position lands
	# unambiguously in a single, known cell rather than depending on
	# floating-point rounding at a column boundary. ---
	var hx := size.x / 2.0
	var hz := size.z / 2.0
	var z_cell_layout: Dictionary = CityChunk._cell_layout(size.z, CityChunk.NOMINAL_COLUMN_METERS)
	var window_z: float = -hz + float(z_cell_layout["size"]) / 2.0 # column 0's own real center
	var row0_y := CityChunk.NOMINAL_FLOOR_METERS * 0.5 # comfortably inside row 0's own span
	var hit_pos := building.global_position + Vector3(hx, row0_y, window_z)
	var hit_normal := Vector3(1, 0, 0)
	var handled: bool = building.try_break_glass(hit_pos, hit_normal)
	if handled and building._cell_state[0][0][0] == building.CellState.CRACKED:
		print("PASS: a landed hit cracks the window open (collision removed, state=CRACKED)")
	else:
		_fail("first hit didn't crack the window (handled=%s state=%s)" % [handled, building._cell_state[0][0][0] if building._cell_state.size() > 0 else "?"])
	if building._cell_collision[0][0][0] == null:
		print("PASS: the cracked window's own collision was removed (now walkable)")
	else:
		_fail("cracked window still has solid collision blocking movement")

	# --- 3a. The SAME hit, but arriving from INSIDE the building: the ray
	# hits the pane's inner face, so its surface normal points inward --
	# the exact opposite of the wall's own outward normal. This used to be
	# declined outright (see _closest_wall_index's own comment), which is
	# what "not letting me break and jump through a window from inside a
	# building" actually was. Uses column 2 so it can't be confused with
	# the already-cracked column 0 above. ---
	var inside_window_z: float = -hz + float(z_cell_layout["size"]) * 2.5 # column 2's own center
	var inside_hit_pos := building.global_position + Vector3(hx, row0_y, inside_window_z)
	var inside_normal := Vector3(-1, 0, 0) # inward-facing: the mirror of wall 0's own +X
	var handled_inside: bool = building.try_break_glass(inside_hit_pos, inside_normal)
	if handled_inside and building._cell_state[0][0][2] == building.CellState.CRACKED:
		print("PASS: a hit from INSIDE cracks the window it actually landed on")
	else:
		_fail("a hit from inside didn't crack the window (handled=%s state=%s)" % [
			handled_inside, building._cell_state[0][0][2],
		])
	if building._cell_collision[0][0][2] == null:
		print("PASS: a window broken from inside is walkable outward")
	else:
		_fail("window broken from inside still has solid collision blocking the way out")
	# An inward normal must not be mis-attributed to the OPPOSITE wall,
	# which is the tie absf() alone would leave unresolved.
	if building._cell_state[1][0][2] == building.CellState.INTACT:
		print("PASS: the inside hit landed on the near wall, not the opposite one")
	else:
		_fail("a hit from inside cracked a window on the far wall instead")

	# --- 3b. The real point of this whole rework: the NEIGHBORING window in
	# the very same row (column 1) must be completely untouched by the hit
	# above -- this is exactly the bug being fixed ("make the glass panels
	# individual per window instead of the whole wall shattering"). ---
	if building._cell_state[0][0][1] == building.CellState.INTACT and building._cell_collision[0][0][1] != null:
		print("PASS: breaking one window leaves its immediate neighbor (same row, next column) fully intact")
	else:
		_fail("breaking column 0 also affected column 1 -- windows aren't breaking individually")

	# --- 4. A second hit on the same (already-cracked) window shatters it,
	# still without touching its neighbor. ---
	var handled2: bool = building.try_break_glass(hit_pos, hit_normal)
	if handled2 and building._cell_state[0][0][0] == building.CellState.SHATTERED:
		print("PASS: a second hit fully shatters an already-cracked window")
	else:
		_fail("second hit didn't shatter the cracked window")
	if building._cell_visual[0][0][0] == null:
		print("PASS: the shattered window's own glass visual was removed")
	else:
		_fail("shattered window still shows a glass visual")
	if building._cell_state[0][0][1] == building.CellState.INTACT:
		print("PASS: the neighboring window is still intact after the first one fully shatters")
	else:
		_fail("shattering column 0 incorrectly changed column 1's own state")

	# --- 5. The door wall now gets real breakable glass too -- everywhere
	# EXCEPT the door's own opening. This building is 16m wide (hx=8): the
	# door wall's own column grid is _cell_layout(16, 1.6) = 10 columns of
	# exactly 1.6m each, so column 8 (u in [4.8, 6.4]) sits well clear of
	# the door's own centered [-0.8, 0.8] range and must be a genuine,
	# breakable window; u=0.0 sits dead in the middle of the door's own
	# opening and must decline (see _is_door_cell). ---
	var door_wall_cell_width: float = CityChunk._cell_layout(size.x, CityChunk.NOMINAL_COLUMN_METERS)["size"]
	var door_window_u: float = -hx + 8.5 * door_wall_cell_width
	var door_window_hit := building.global_position + Vector3(door_window_u, row0_y, hz)
	var door_window_handled: bool = building.try_break_glass(door_window_hit, Vector3(0, 0, 1))
	if door_window_handled:
		print("PASS: the door wall now gets real breakable glass on its own genuine window cells")
	else:
		_fail("a hit on a genuine door-wall window (clear of the door) was incorrectly declined")

	var door_opening_hit := building.global_position + Vector3(0.0, row0_y, hz)
	var door_opening_handled: bool = building.try_break_glass(door_opening_hit, Vector3(0, 0, 1))
	if not door_opening_handled:
		print("PASS: the door's own opening is correctly excluded from the glass system (hit declined)")
	else:
		_fail("a hit on the door's own opening was incorrectly treated as breakable glass")

	# --- 6. Every row is real breakable glass now, even well above this
	# building's own real interior floor count (MAX_INTERIOR_FLOORS-capped,
	# see _build_building's own comment) -- a hit high up, in the hollow
	# shell above the real decorated interior, still cracks the window
	# instead of being declined the way the old decorative-only upper band
	# used to be. This test building is deliberately taller than that cap
	# (see check 2's own comment) so a real "above the interior" region
	# exists to hit. ---
	var high_y := size.y - 0.5 # right under the roof
	var high_hit_pos := building.global_position + Vector3(hx, high_y, window_z)
	var high_handled: bool = building.try_break_glass(high_hit_pos, Vector3(1, 0, 0))
	if high_handled:
		print("PASS: a window well above the real interior still gets real breakable glass")
	else:
		_fail("a window above the real interior was incorrectly declined -- full-height glass coverage regressed")

	# --- 7. A player's own body walking through a DIFFERENT cracked
	# window (wall index 1, normal -X, row 1, column 0 -- untouched by the
	# checks above) fully shatters it, but only once it actually passes
	# through. Aimed at column 0's own real center, same reasoning as
	# check 3. ---
	var hit_pos2 := building.global_position + Vector3(-hx, CityChunk.NOMINAL_FLOOR_METERS * 1.5, window_z)
	building.try_break_glass(hit_pos2, Vector3(-1, 0, 0))
	if building._cell_state[1][1][0] != building.CellState.CRACKED:
		_fail("setup for the walkthrough check failed -- window didn't crack")

	var player := CharacterBody3D.new()
	player.add_to_group("players")
	var pshape := CollisionShape3D.new()
	var pcapsule := CapsuleShape3D.new()
	pcapsule.radius = 0.3
	pcapsule.height = 1.6
	pshape.shape = pcapsule
	player.add_child(pshape)
	player.collision_layer = 4
	add_child(player)
	# Starts well clear of the opening, walks straight through it.
	player.global_position = building.global_position + Vector3(-hx - 3.0, CityChunk.NOMINAL_FLOOR_METERS * 1.5, window_z)

	for i in range(90):
		player.velocity = Vector3(2.0, 0, 0)
		player.move_and_slide()
		await get_tree().physics_frame

	if building._cell_state[1][1][0] == building.CellState.SHATTERED:
		print("PASS: a player walking through a cracked window fully shatters it")
	else:
		_fail("player walking through the opening didn't shatter it (state=%s)" % building._cell_state[1][1][0])

	# --- 8. Real reported bug: the exterior wall's own facade mesh was
	# never actually hollowed out for the breakable-glass band (just
	# texture-painted to LOOK like a window over solid geometry), and
	# _build_interior's own separate interior-facing wall had zero
	# awareness of the glass band at all -- so even with the glass itself
	# broken and its own collision gone, a player could get to the wall's
	# own plane (check 7 above) but no further, blocked by either the
	# still-solid exterior facade or the interior body's own always-solid
	# wall collision. Continuing the SAME walk well past both of those
	# planes (all the way to the building's own center) confirms there's
	# really nothing solid left anywhere in the broken opening -- not just
	# that this window's own front-most collision layer opened up. ---
	for i in range(180):
		player.velocity = Vector3(2.0, 0, 0)
		player.move_and_slide()
		await get_tree().physics_frame
	if player.global_position.x > -hx + 2.0:
		print("PASS: a player can walk all the way through a broken window into the real interior (ended at x=", player.global_position.x, ")")
	else:
		_fail("player got stuck at x=%s -- something behind the broken glass is still solid (exterior facade or the interior body's own separate wall collision)" % player.global_position.x)

	# --- 9. The door wall's own windows are now REAL holes on BOTH faces --
	# exterior (already proven breakable in check 5) and interior, matching
	# every other wall (see _build_interior's own rewritten has_door
	# branch, which replaced the old decorative-only overlay with a real
	# _add_flat_windowed_wall_row_excluding + collision pair). A player
	# walking through a broken door-wall window (column 8, clear of the
	# door) should end up inside, exactly like checks 7/8 already prove for
	# a plain wall -- proving the exterior and interior holes actually line
	# up rather than leaving the interior body's own separate wall solid
	# behind a broken exterior pane. ---
	building.try_break_glass(door_window_hit, Vector3(0, 0, 1)) # crack
	building.try_break_glass(door_window_hit, Vector3(0, 0, 1)) # shatter
	var door_player := CharacterBody3D.new()
	door_player.add_to_group("players")
	var door_pshape := CollisionShape3D.new()
	var door_pcapsule := CapsuleShape3D.new()
	door_pcapsule.radius = 0.3
	door_pcapsule.height = 1.6
	door_pshape.shape = door_pcapsule
	door_player.add_child(door_pshape)
	door_player.collision_layer = 4
	add_child(door_player)
	door_player.global_position = building.global_position + Vector3(door_window_u, row0_y, hz + 3.0)
	for i in range(180):
		door_player.velocity = Vector3(0, 0, -2.0)
		door_player.move_and_slide()
		await get_tree().physics_frame
	if door_player.global_position.z < building.global_position.z + hz - 2.0:
		print("PASS: a player can walk through a broken door-wall window all the way into the real interior (ended at z=", door_player.global_position.z, ")")
	else:
		_fail("player got stuck walking through a broken door-wall window (ended at z=%s) -- exterior/interior holes may not line up" % door_player.global_position.z)

	# --- 10. Rigorous zero-gap coverage check across building widths. The
	# actual root cause of BOTH earlier real-hole regressions on this exact
	# wall was a narrow leftover strip that didn't evenly divide into its
	# own independently-fitted cell grid. This rewrite uses ONE uniform,
	# un-overridden grid instead and simply classifies whole cells as
	# window-or-solid (see _add_windowed_wall_row_excluding's own comment),
	# which should make a leftover gap structurally impossible.
	#
	# Calls _add_glass_band_frame_collision_excluding directly -- the exact
	# function _add_building_collision's own door-wall branch now uses for
	# row 0 -- and inspects its own emitted CollisionShape3D children's U-
	# ranges: none may overlap the door's own [door_u0, door_u1] rectangle,
	# and together with that rectangle they must reconstruct the wall's
	# full span with no gap. Deliberately geometric, not a physics raycast
	# against a full building: a real, closed door LEAF (building_door.gd's
	# own node, also built by _build_building) legitimately occupies the
	# door's own rectangle with real collision once a full building exists,
	# which an "is this space totally empty" raycast can't tell apart from
	# a genuine wall-frame bug -- this checks the wall's own frame geometry
	# directly instead, sidestepping the door leaf entirely. Swept across
	# EVERY building width in MIN_FOOTPRINT..MAX_FOOTPRINT, not just one
	# convenient sample. ---
	var sweep_ok := true
	var widths_checked := 0
	var w := CityChunk.MIN_FOOTPRINT
	while w <= CityChunk.MAX_FOOTPRINT + 0.01:
		widths_checked += 1
		var sweep_hx := w / 2.0
		var sweep_door_u0 := -CityChunk.DOOR_WIDTH / 2.0
		var sweep_door_u1 := CityChunk.DOOR_WIDTH / 2.0

		# The frame's own collision is BATCHED into one merged shape now
		# (see city_chunk.gd's own _add_glass_frame_piece_collision -- it was
		# ~1,750 separate CollisionShape3D nodes per chunk, the single
		# biggest cost in streaming a chunk in). So the pieces are read back
		# out of the triangle buffer rather than off child nodes; each piece
		# is one quad, i.e. 6 consecutive vertices, and this wall's own quads
		# span X, so each group's min/max X is that piece's own [u0,u1].
		var faces: Array = []
		CityChunk._add_glass_band_frame_collision_excluding(faces, -sweep_hx, sweep_hx, 0.0, 3.0, func(u, y): return Vector3(u, y, 0.0), Vector3(0, 0, 1), sweep_door_u0, sweep_door_u1)

		var intervals: Array = []
		var vertex := 0
		while vertex + 5 < faces.size():
			var piece_u0: float = INF
			var piece_u1: float = -INF
			for k in range(6):
				var vx: float = (faces[vertex + k] as Vector3).x
				piece_u0 = minf(piece_u0, vx)
				piece_u1 = maxf(piece_u1, vx)
			vertex += 6
			intervals.append(Vector2(piece_u0, piece_u1))
			# A tiny epsilon tolerance (floating-point boundary touch,
			# not a real overlap) -- pieces are expected to butt right
			# up against the door's own edge exactly.
			if piece_u0 < sweep_door_u1 - 0.001 and piece_u1 > sweep_door_u0 + 0.001:
				sweep_ok = false
				_fail("width=%s: a collision piece [%s,%s] overlaps the door's own opening [%s,%s]" % [w, piece_u0, piece_u1, sweep_door_u0, sweep_door_u1])

		if not _span_fully_covered(intervals, -sweep_hx, sweep_door_u0):
			sweep_ok = false
			_fail("width=%s: gap in the door wall's own row-0 collision, left of the door" % w)
		if not _span_fully_covered(intervals, sweep_door_u1, sweep_hx):
			sweep_ok = false
			_fail("width=%s: gap in the door wall's own row-0 collision, right of the door" % w)

		w += 1.0

	if sweep_ok and widths_checked > 0:
		print("PASS: door wall row-0 collision has zero gaps outside the door's own opening, across ", widths_checked, " building widths (", CityChunk.MIN_FOOTPRINT, " to ", CityChunk.MAX_FOOTPRINT, ")")

	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

## True if the union of `intervals` (each a Vector2(start,end)) fully covers
## [span_a, span_b] with no gap. Used by check 10 above to confirm the door
## wall's own row-0 collision leaves no unaccounted strip anywhere outside
## the door's own opening.
func _span_fully_covered(intervals: Array, span_a: float, span_b: float) -> bool:
	if span_b <= span_a:
		return true
	var relevant: Array = []
	for iv in intervals:
		if iv.y > span_a and iv.x < span_b:
			relevant.append(Vector2(maxf(iv.x, span_a), minf(iv.y, span_b)))
	relevant.sort_custom(func(a, b): return a.x < b.x)
	var cursor := span_a
	for iv in relevant:
		if iv.x > cursor + 0.001:
			return false
		cursor = maxf(cursor, iv.y)
	return cursor >= span_b - 0.001
