extends CanvasLayer
## Bird's-eye city map, toggled with M. Minecraft-style: it only shows
## ground you have actually walked, filling in as you explore, and it is
## purely a plan view -- roads, lots, and building footprints drawn at their
## real relative sizes.
##
## NOTHING IS RENDERED IN 3D FOR THIS. Every chunk in this city is a pure
## function of its own seed (see city_chunk.gd's building_params_for_seed),
## so a chunk's road/lot type and its building's exact footprint can be
## DERIVED without the chunk existing as nodes. That matters: the map shows
## far more area than is loaded at any moment (city.gd only keeps a few
## chunks around the player), and a map built by looking at live geometry
## would go blank everywhere except the player's immediate surroundings --
## which is the opposite of what a map is for.
##
## Explored-ness is the ONE thing that can't be derived, since it's a record
## of what the player did -- city.gd owns it (see its own _mark_explored).
##
## KNOWN SIMPLIFICATION: what's drawn is each building's MAIN MASSING. Some
## buildings also grow an L-shaped wing (see city_chunk.gd's own
## _maybe_add_wing, ~35% of them, up to 6m deep) and most grow balconies,
## and neither shows up here -- so a winged building reads slightly smaller
## on the map than it is on the ground. Those are rolled from the chunk
## RNG's own sequence INSIDE generate_chunk rather than returned by
## building_params, so drawing them would mean exposing them as pure
## params first, the same refactor the skybridges needed. Deliberately not
## done for a first map; the main massing is what the block reads as.
##
## Follows ui/inventory_panel.gd exactly for open/close: no tree pause,
## input gated through player.set_ui_capturing() instead, mouse released
## while open and restored after.

const CityChunk = preload("res://city/city_chunk.gd")

## How much of the world fits across the map, in chunks.
##
## 16 chunks is 384m across. Picked off a render rather than guessed: the
## first attempt at 40 (960m) was technically correct and useless -- a 15m
## building came out about 10px and a long walk barely filled a corner of
## the panel. At 16 a building is a chunky ~28px block and a street reads
## as a street.
const VIEW_CHUNKS := 16.0

const PANEL_MARGIN := 60.0
const BG_COLOR := Color(0.05, 0.055, 0.07, 0.96)
const BORDER_COLOR := Color(0.55, 0.63, 1.0, 0.5)
const BORDER_WIDTH := 2.0

## Unexplored ground is simply not drawn -- the panel's own background shows
## through, which IS the fog.
const ROAD_COLOR := Color(0.17, 0.17, 0.19)
const LOT_COLOR := Color(0.30, 0.29, 0.27)
## Buildings are shaded by height between these, so the plan view still
## conveys which blocks are towers and which are low-rise. Footprint is
## drawn at true scale either way.
const BUILDING_LOW_COLOR := Color(0.52, 0.55, 0.62)
const BUILDING_TALL_COLOR := Color(0.88, 0.91, 0.98)
## Matches city_chunk.gd's own MIN_HEIGHT/MAX_HEIGHT, the range the shading
## is normalised across.
const BUILDING_MIN_HEIGHT := 8.0
const BUILDING_MAX_HEIGHT := 96.0

const PLAYER_COLOR := Color(1.0, 0.85, 0.2)
const PLAYER_MARKER_SIZE := 7.0
const CLAIMED_COLOR := Color(0.35, 1.0, 0.55)
const CLAIMED_RING_WIDTH := 2.0

## How far a single wheel notch zooms, and the range it's held inside. The
## limits are what stop the two failure modes of a free zoom on an INFINITE
## world: far enough out and every chunk is a sub-pixel speck (plus the draw
## loop's own per-chunk cull stops culling anything, so it walks every
## explored chunk in the save); far enough in and you're staring at one
## building with no idea where you are.
const ZOOM_STEP := 1.15
const ZOOM_MIN := 0.35
const ZOOM_MAX := 6.0

const TITLE_TEXT := "CITY MAP"
const HINT_TEXT := "[M] close   ·   scroll to zoom   ·   drag to pan"
const LABEL_COLOR := Color(0.72, 0.79, 1.0)
const HINT_COLOR := Color(0.55, 0.58, 0.66)
const TITLE_FONT_SIZE := 22
const HINT_FONT_SIZE := 13

var _root: Control
var _canvas: Control
var _player: Node3D = null
var _city: Node = null
var _was_mouse_captured := false

## Multiplies the derived pixels-per-metre scale. 1.0 is the VIEW_CHUNKS
## framing the map was designed around.
var _zoom := 1.0
## How far the view is dragged off the player, in WORLD METRES rather than
## screen pixels. That's what keeps a pan stable across a zoom: the spot you
## dragged to stays the spot you're looking at instead of sliding, because
## the offset means the same thing at every scale.
var _pan_world := Vector2.ZERO
var _dragging := false

func _ready() -> void:
	# 45: above the HUD (40) but below the pause menu (50) and the dev
	# console (100). A full-screen map has to cover the health bars -- at
	# the inventory's own layer they punched straight through it and sat on
	# top of the map's own title, which a render caught immediately.
	layer = 45
	_player = get_parent() as Node3D
	_build_ui()
	_root.visible = false

func _build_ui() -> void:
	_root = Control.new()
	_root.anchor_right = 1.0
	_root.anchor_bottom = 1.0
	# STOP, unlike most panels here: this one is a full-screen modal, and
	# clicks landing on the world behind it would be a real surprise.
	_root.mouse_filter = Control.MOUSE_FILTER_STOP
	add_child(_root)

	var panel := PanelContainer.new()
	panel.anchor_right = 1.0
	panel.anchor_bottom = 1.0
	panel.offset_left = PANEL_MARGIN
	panel.offset_top = PANEL_MARGIN
	panel.offset_right = -PANEL_MARGIN
	panel.offset_bottom = -PANEL_MARGIN
	var style := StyleBoxFlat.new()
	style.bg_color = BG_COLOR
	style.border_color = BORDER_COLOR
	style.set_border_width_all(int(BORDER_WIDTH))
	style.set_corner_radius_all(6)
	style.set_content_margin_all(12)
	panel.add_theme_stylebox_override("panel", style)
	_root.add_child(panel)

	# The map itself is drawn by hand rather than assembled from child
	# nodes: it's hundreds of rectangles that change every time the player
	# moves, and one _draw() pass is far cheaper than churning that many
	# ColorRects.
	_canvas = Control.new()
	# STOP, not IGNORE as it was: this is what receives the wheel and the
	# drag. It's a child of the panel and children are hit-tested first, so
	# taking the events here doesn't disturb _root's own job of swallowing
	# clicks that would otherwise land on the world behind the map.
	_canvas.mouse_filter = Control.MOUSE_FILTER_STOP
	_canvas.gui_input.connect(_on_canvas_gui_input)
	# REQUIRED, not cosmetic: _draw() is not bounded by the control's own
	# rect, so without this the map keeps painting chunks straight out past
	# the panel border and over the rest of the screen. Caught in a render.
	_canvas.clip_contents = true
	_canvas.draw.connect(_draw_map)
	panel.add_child(_canvas)

	var title := Label.new()
	title.text = TITLE_TEXT
	title.add_theme_font_size_override("font_size", TITLE_FONT_SIZE)
	title.add_theme_color_override("font_color", LABEL_COLOR)
	title.position = Vector2(PANEL_MARGIN + 20.0, PANEL_MARGIN + 12.0)
	title.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_root.add_child(title)

	var hint := Label.new()
	hint.text = HINT_TEXT
	hint.add_theme_font_size_override("font_size", HINT_FONT_SIZE)
	hint.add_theme_color_override("font_color", HINT_COLOR)
	hint.anchor_left = 1.0
	hint.anchor_right = 1.0
	hint.anchor_top = 1.0
	hint.anchor_bottom = 1.0
	hint.grow_horizontal = Control.GROW_DIRECTION_BEGIN
	hint.grow_vertical = Control.GROW_DIRECTION_BEGIN
	hint.offset_left = -PANEL_MARGIN - 120.0
	hint.offset_top = -PANEL_MARGIN - 34.0
	hint.offset_right = -PANEL_MARGIN - 20.0
	hint.offset_bottom = -PANEL_MARGIN - 14.0
	hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	hint.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_root.add_child(hint)

func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("toggle_map"):
		if is_open():
			close()
		else:
			open()

## Scroll to zoom, hold and drag to pan.
func _on_canvas_gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		var button := event as InputEventMouseButton
		match button.button_index:
			MOUSE_BUTTON_WHEEL_UP:
				if button.pressed:
					_zoom_at(button.position, ZOOM_STEP)
			MOUSE_BUTTON_WHEEL_DOWN:
				if button.pressed:
					_zoom_at(button.position, 1.0 / ZOOM_STEP)
			MOUSE_BUTTON_LEFT:
				_dragging = button.pressed
	elif event is InputEventMouseMotion and _dragging:
		# The map moves WITH the cursor, so the view centre moves against
		# it -- drag right and the world slides right, which means looking
		# further left. Divided by the current scale so a drag covers the
		# same on-screen distance however far zoomed in you are.
		_pan_world -= (event as InputEventMouseMotion).relative / _current_scale()
		_canvas.queue_redraw()

## Zooms while keeping whatever is under the cursor pinned in place, rather
## than always zooming to the middle of the panel. Without this, zooming in
## on something off to one side walks it straight out of view and you have
## to chase it with the drag.
func _zoom_at(cursor: Vector2, factor: float) -> void:
	var before := _current_scale()
	var new_zoom: float = clampf(_zoom * factor, ZOOM_MIN, ZOOM_MAX)
	if is_equal_approx(new_zoom, _zoom):
		return
	_zoom = new_zoom
	var after := _current_scale()
	if before <= 0.0 or after <= 0.0:
		return
	# The world point under the cursor, and where it would drift to at the
	# new scale -- the difference is exactly the pan needed to cancel it.
	var offset := cursor - _canvas.size * 0.5
	_pan_world += offset / before - offset / after
	_canvas.queue_redraw()

## Pixels per world metre at the current zoom. Kept in one place because the
## drag, the zoom and the draw all have to agree on it -- they disagreed in
## an earlier pass and the map slid sideways as it zoomed.
func _current_scale() -> float:
	if _canvas == null:
		return 1.0
	var size := _canvas.size
	var chunk_size: float = _city.CHUNK_SIZE if _city != null and is_instance_valid(_city) else 24.0
	if size.x <= 0.0 or size.y <= 0.0:
		return 1.0
	return minf(size.x, size.y) / (VIEW_CHUNKS * chunk_size) * _zoom

func _process(_delta: float) -> void:
	# Only while open: redrawn every frame so the player marker tracks their
	# own movement... which it can't, since input is gated. It DOES still
	# track anything that moves them anyway (a car they're seated in), and
	# costs nothing when closed.
	if is_open():
		_canvas.queue_redraw()

func open() -> void:
	# Guarded exactly like inventory_panel.gd's own _open: set_ui_capturing
	# moves a COUNTER, so an unbalanced second open would leave the player
	# permanently unable to act.
	if is_open():
		return
	_city = _find_city()
	# Recentred on the player every time it opens, so the map can never come
	# up showing empty space you panned off to last time and left. The ZOOM
	# is kept: that reads as a preference, where a stale pan reads as a bug.
	_pan_world = Vector2.ZERO
	_dragging = false
	_was_mouse_captured = Input.mouse_mode == Input.MOUSE_MODE_CAPTURED
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	if is_instance_valid(_player) and _player.has_method("set_ui_capturing"):
		_player.set_ui_capturing(true)
	_root.visible = true
	_canvas.queue_redraw()

func close() -> void:
	if not is_open():
		return
	_root.visible = false
	# is_instance_valid, not != null -- _exit_tree can run while the player
	# is being freed out from under this panel, and a freed object still
	# compares non-null.
	if is_instance_valid(_player) and _player.has_method("set_ui_capturing"):
		_player.set_ui_capturing(false)
	if _was_mouse_captured:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED

func is_open() -> bool:
	return _root != null and _root.visible

## Balances set_ui_capturing if the player is freed with the map still open
## (disconnect, quit to title) -- same belt-and-braces the inventory panel
## carries for the same reason.
func _exit_tree() -> void:
	if is_open():
		close()

## The City is whatever parents the player (see main.gd's own spawn, which
## does _city.add_child(player)). Looked up on open rather than cached in
## _ready(), since the player isn't parented into the world yet at the point
## this panel is built.
func _find_city() -> Node:
	if not is_instance_valid(_player):
		return null
	var parent := _player.get_parent()
	if parent != null and parent.has_method("is_chunk_explored"):
		return parent
	return null

func _draw_map() -> void:
	if _city == null or not is_instance_valid(_city) or not is_instance_valid(_player):
		return

	var size := _canvas.size
	if size.x <= 0.0 or size.y <= 0.0:
		return

	var chunk_size: float = _city.CHUNK_SIZE
	# Pixels per world metre, INCLUDING the zoom -- shared with the drag and
	# the wheel through one helper so the three can't disagree. Derived from
	# the SHORTER screen axis so the advertised VIEW_CHUNKS is what you
	# actually get across the tighter dimension rather than being cropped.
	var scale: float = _current_scale()
	var center := size * 0.5
	var player_pos: Vector3 = _player.global_position
	# The view follows the player, offset by however far it's been dragged.
	var view_x: float = player_pos.x + _pan_world.x
	var view_z: float = player_pos.z + _pan_world.y

	# World metres -> panel pixels, centred on the view. World +Z is SOUTH,
	# which is +Y (down) on screen, so this is a plain north-up plan view
	# with no flip.
	var to_screen := func(world_x: float, world_z: float) -> Vector2:
		return center + Vector2(world_x - view_x, world_z - view_z) * scale

	for coord_variant in _city.explored_chunks():
		var coord: Vector2i = coord_variant
		var origin_x: float = coord.x * chunk_size
		var origin_z: float = coord.y * chunk_size
		var top_left: Vector2 = to_screen.call(origin_x, origin_z)
		var chunk_px: float = chunk_size * scale

		# Cheap cull: chunks scrolled off the panel cost nothing.
		if top_left.x > size.x or top_left.y > size.y or top_left.x + chunk_px < 0.0 or top_left.y + chunk_px < 0.0:
			continue

		var is_street: bool = CityChunk.is_street_chunk(coord)
		_canvas.draw_rect(Rect2(top_left, Vector2(chunk_px, chunk_px)), ROAD_COLOR if is_street else LOT_COLOR)
		if is_street:
			continue

		# Derived, not observed -- see this file's own class comment.
		var params: Dictionary = CityChunk.building_params_for_seed(_city.chunk_seed_for(coord), coord, chunk_size)
		if not params["has_building"]:
			continue

		var b_size: Vector3 = params["size"]
		var b_offset: Vector3 = params["offset"]
		# Chunk-local coords are centred on the chunk, so the chunk's own
		# centre is half a chunk in from its origin.
		var b_center_x: float = origin_x + chunk_size * 0.5 + b_offset.x
		var b_center_z: float = origin_z + chunk_size * 0.5 + b_offset.z
		var b_top_left: Vector2 = to_screen.call(b_center_x - b_size.x * 0.5, b_center_z - b_size.z * 0.5)
		var tallness: float = clampf((b_size.y - BUILDING_MIN_HEIGHT) / (BUILDING_MAX_HEIGHT - BUILDING_MIN_HEIGHT), 0.0, 1.0)
		_canvas.draw_rect(
			Rect2(b_top_left, Vector2(b_size.x * scale, b_size.z * scale)),
			BUILDING_LOW_COLOR.lerp(BUILDING_TALL_COLOR, tallness)
		)

	_draw_claimed_marker(to_screen, scale)
	_draw_player_marker(center)

## A ring on the building the player owns (see player.gd's own /claim), so
## the map answers "where's my place" as well as "where have I been".
## Skipped entirely when nothing is claimed.
func _draw_claimed_marker(to_screen: Callable, scale: float) -> void:
	if not _player.has_method("has_claimed_building") or not _player.has_claimed_building():
		return
	var claimed: Vector3 = _player.get_claimed_spawn_position()
	var at: Vector2 = to_screen.call(claimed.x, claimed.z)
	_canvas.draw_arc(at, maxf(6.0, 8.0 * scale), 0.0, TAU, 24, CLAIMED_COLOR, CLAIMED_RING_WIDTH)

## A triangle at the centre pointing where the player faces -- the map is
## always centred on them, so this is the one thing that rotates.
func _draw_player_marker(center: Vector2) -> void:
	# The body's own forward is -Z. On a north-up plan view that's -Y, i.e.
	# straight up the screen at zero rotation.
	var forward_3d: Vector3 = -_player.global_transform.basis.z
	var facing := Vector2(forward_3d.x, forward_3d.z)
	if facing.length_squared() < 0.0001:
		facing = Vector2(0.0, -1.0)
	facing = facing.normalized()
	var side := Vector2(-facing.y, facing.x)
	var points := PackedVector2Array([
		center + facing * PLAYER_MARKER_SIZE,
		center - facing * PLAYER_MARKER_SIZE * 0.6 + side * PLAYER_MARKER_SIZE * 0.6,
		center - facing * PLAYER_MARKER_SIZE * 0.6 - side * PLAYER_MARKER_SIZE * 0.6,
	])
	_canvas.draw_colored_polygon(points, PLAYER_COLOR)
