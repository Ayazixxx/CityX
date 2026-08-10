extends Node3D
## Character creator: lets the player orbit-rotate the character (click and
## drag on the empty space around it) and paint directly onto its surface
## (click and drag on the model itself), then hands the finished character
## off. Which one happens is decided once, at the moment the mouse button
## goes down, by whether that click actually hit the character -- so a
## drag started on empty space stays a rotate even if the spinning model
## sweeps under the cursor mid-drag, and vice versa.

signal creation_finished(character_node: Node3D)
## Fired by Back/Escape once the player actually confirms leaving (or
## immediately, if there was nothing painted to lose) -- see main.gd's own
## _on_creator_cancelled, which returns to character select.
signal creation_cancelled

const DEFAULT_BRUSH_RADIUS_PX := 1
const RAY_LENGTH := 50.0
const ROTATE_SENSITIVITY := 0.01
const ERASE_COLOR := Color.WHITE
# Max screen-pixel gap allowed between two consecutive paint stamps during a
# drag -- a fast mouse drag can jump many screen pixels between motion
# events, and since the atlas isn't uniformly scaled across the body (limb
# radius/length varies, and the smoothly-blended joints deviate from the
# idealized straight-cylinder shape the UV formula assumes), that same
# screen-space jump can land stamps far apart in ATLAS space, leaving
# visible gaps instead of a continuous stroke. Re-raycasting at evenly
# spaced points along the screen-space path between events closes those
# gaps regardless of local UV distortion.
const PAINT_STEP_PX := 2.0

const ToolIconScript := preload("res://character_creator/tool_icon.gd")
const TOOL_ICON_SIZE := 64

# Scroll-to-zoom: camera.position.z is its own straight-line distance back
# from the character (see the .tscn's own starting value, 4) -- simply
# nudging it along world Z each wheel tick approximates zooming toward the
# character well enough for this simple, fixed (never-orbiting-in-depth)
# camera rig, without needing to reason about its own forward vector.
const MIN_CAMERA_DISTANCE := 1.5
const MAX_CAMERA_DISTANCE := 8.0
const ZOOM_STEP := 0.35

@onready var rotation_pivot: Node3D = $RotationPivot
@onready var character: Node3D = $RotationPivot/Character
@onready var camera: Camera3D = $Camera3D
@onready var color_picker: ColorPickerButton = $UI/ColorPickerButton
@onready var eraser_button: Button = $UI/EraserButton
@onready var mirror_button: Button = $UI/MirrorButton
@onready var undo_button: Button = $UI/UndoButton
@onready var redo_button: Button = $UI/RedoButton
@onready var brush_button: Button = $UI/BrushButton
@onready var bucket_button: Button = $UI/BucketButton
@onready var eyedropper_button: Button = $UI/EyedropperButton
@onready var brush_size_slider: HSlider = $UI/BrushSizeSlider
@onready var brush_size_value_label: Label = $UI/BrushSizeValueLabel
@onready var hair_none_button: Button = $UI/HairNoneButton
@onready var hair_embermane_button: Button = $UI/HairEmbermaneButton
@onready var enter_city_button: Button = $UI/EnterCityButton
@onready var debug_dump_atlas_button: Button = $UI/DebugDumpAtlasButton
@onready var name_entry_overlay: ColorRect = $UI/NameEntryOverlay
@onready var name_line_edit: LineEdit = $UI/NameEntryOverlay/NamePanel/VBox/NameLineEdit
@onready var name_confirm_button: Button = $UI/NameEntryOverlay/NamePanel/VBox/NameConfirmButton
@onready var back_button: Button = $UI/BackButton
@onready var confirm_back_overlay: ColorRect = $UI/ConfirmBackOverlay
@onready var confirm_discard_button: Button = $UI/ConfirmBackOverlay/ConfirmPanel/VBox/ButtonRow/ConfirmDiscardButton
@onready var confirm_cancel_button: Button = $UI/ConfirmBackOverlay/ConfirmPanel/VBox/ButtonRow/ConfirmCancelButton

var _painting := false
var _rotating := false
var _yaw := 0.0
var _pitch := 0.0
var _brush_radius_px := DEFAULT_BRUSH_RADIUS_PX
var _last_paint_screen_pos := Vector2.ZERO

func _ready() -> void:
	_recenter_on_torso()

	enter_city_button.pressed.connect(_on_enter_city_pressed)
	eraser_button.toggled.connect(_on_eraser_toggled)
	# ColorPickerButton's own "pressed" (inherited from Button, fired on the
	# click that OPENS its popup) is used here instead of "color_changed" --
	# tested and confirmed color_changed does not reliably fire from every
	# path a real color pick can take (e.g. it never fired at all when the
	# popup's internal ColorPicker had its color set directly, which is one
	# of the ways picking a swatch/dragging the wheel updates it). Clearing
	# Eraser as soon as the picker is opened, rather than waiting for a
	# specific color to be chosen, is simpler and can't miss a real pick.
	color_picker.pressed.connect(_on_color_picker_pressed)
	undo_button.pressed.connect(_on_undo_pressed)
	redo_button.pressed.connect(_on_redo_pressed)
	brush_size_slider.value_changed.connect(_on_brush_size_changed)
	hair_none_button.pressed.connect(_on_hair_style_pressed.bind("none"))
	hair_embermane_button.pressed.connect(_on_hair_style_pressed.bind("embermane"))
	debug_dump_atlas_button.pressed.connect(_on_debug_dump_atlas_pressed)
	name_confirm_button.pressed.connect(_on_name_confirmed)
	name_line_edit.text_submitted.connect(_on_name_line_edit_submitted)
	back_button.pressed.connect(_try_go_back)
	confirm_discard_button.pressed.connect(_on_confirm_discard_pressed)
	confirm_cancel_button.pressed.connect(_on_confirm_cancel_pressed)
	_update_undo_redo_buttons()
	_on_brush_size_changed(brush_size_slider.value)
	_recenter_on_torso()
	_setup_tool_icons()

## Replaces Brush/Bucket/Eyedropper's own plain text labels with small
## procedurally-drawn icons (see tool_icon.gd) -- not awaited from _ready()
## itself, since building all three takes a couple of real frames each (see
## _build_tool_icon's own comment); the buttons just show their old text for
## a moment before the icons pop in, the same acceptable "a few frames'
## delay is fine" tradeoff this project's own portrait-capture code already
## makes elsewhere.
func _setup_tool_icons() -> void:
	_apply_tool_icon(brush_button, "brush")
	_apply_tool_icon(bucket_button, "bucket")
	_apply_tool_icon(eyedropper_button, "eyedropper")

func _apply_tool_icon(button: Button, icon_type: String) -> void:
	var texture := await _build_tool_icon(icon_type)
	button.icon = texture
	button.text = ""
	button.expand_icon = true
	button.icon_alignment = HORIZONTAL_ALIGNMENT_CENTER

## Renders one tool_icon.gd icon into a throwaway offscreen SubViewport and
## captures it as a plain ImageTexture -- procedural rather than an
## imported image asset, matching how this project already builds
## everything else (weapons, characters) in code. Needs a couple of real
## frames after adding the draw node before the SubViewport actually has
## pixels to capture (same "render target update + a frame to actually
## render" requirement ui/hud.gd's own portrait capture already documents).
func _build_tool_icon(icon_type: String) -> ImageTexture:
	var viewport := SubViewport.new()
	viewport.size = Vector2i(TOOL_ICON_SIZE, TOOL_ICON_SIZE)
	viewport.transparent_bg = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(viewport)

	var draw_node := Control.new()
	draw_node.set_script(ToolIconScript)
	draw_node.icon_type = icon_type
	draw_node.size = Vector2(TOOL_ICON_SIZE, TOOL_ICON_SIZE)
	viewport.add_child(draw_node)

	await get_tree().process_frame
	await get_tree().process_frame

	var img := viewport.get_texture().get_image()
	var texture := ImageTexture.create_from_image(img)
	viewport.queue_free()
	return texture

## TEMPORARY debug aid: dumps the current paint atlas to disk so it can be
## inspected directly while tracking down a reported paint-mapping bug. A
## keyboard shortcut (F9, then Ctrl+Alt+D) was tried first but F9 got
## silently intercepted by OEM laptop hotkey software before it ever
## reached the game -- an on-screen button can't be hijacked that way.
## Safe to remove (along with the button in the .tscn) once resolved.
func _on_debug_dump_atlas_pressed() -> void:
	var path := "user://atlas_debug.png"
	var err: Error = character.atlas_image.save_png(path)
	if err == OK:
		print("Saved atlas to: ", ProjectSettings.globalize_path(path))
	else:
		print("Failed to save atlas debug image: ", err)

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE:
		_handle_escape()
		return
	if event is InputEventMouseButton and event.pressed and (event.button_index == MOUSE_BUTTON_WHEEL_UP or event.button_index == MOUSE_BUTTON_WHEEL_DOWN):
		var step := -ZOOM_STEP if event.button_index == MOUSE_BUTTON_WHEEL_UP else ZOOM_STEP
		camera.position.z = clampf(camera.position.z + step, MIN_CAMERA_DISTANCE, MAX_CAMERA_DISTANCE)
		return
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if event.pressed:
			var hit := _raycast_character(event.position)
			if hit.is_empty():
				_rotating = true
			elif eyedropper_button.button_pressed:
				# Not an edit -- no undo snapshot, no _painting -- so mouse
				# motion afterward (still held down) doesn't drag-paint or
				# repeatedly re-sample.
				_pick_color_at_hit(hit)
			else:
				character.push_undo_snapshot()
				_update_undo_redo_buttons()
				if bucket_button.button_pressed:
					# Bucket is a single click action, not a drag stroke --
					# leave _painting false so mouse motion afterward (still
					# held down) doesn't repeatedly re-flood the same region.
					_bucket_fill_at_hit(hit)
				else:
					_painting = true
					_last_paint_screen_pos = event.position
					character.begin_paint_stroke()
					_paint_at_hit(hit)
		else:
			_painting = false
			_rotating = false
	elif event is InputEventMouseMotion:
		if _painting:
			_try_paint_at(event.position)
		elif _rotating:
			# Absolute accumulated angles, not incremental rotate_x/rotate_y
			# calls -- composing local rotations frame-to-frame would drift
			# off-axis (introduce roll) over a long drag. Setting rotation
			# fresh from two independent angles each time keeps yaw and
			# pitch clean no matter how much the player spins it around.
			_yaw -= event.relative.x * ROTATE_SENSITIVITY
			_pitch -= event.relative.y * ROTATE_SENSITIVITY
			rotation_pivot.rotation = Vector3(_pitch, _yaw, 0.0)

func _raycast_character(screen_pos: Vector2) -> Dictionary:
	var from := camera.project_ray_origin(screen_pos)
	var dir := camera.project_ray_normal(screen_pos)
	return character.raycast_paint(from, dir, RAY_LENGTH)

## Re-raycasts and stamps at evenly spaced points along the screen-space
## path from the last painted position to this one (see PAINT_STEP_PX),
## instead of just at screen_pos itself -- keeps a fast drag from leaving
## gaps between stamps, especially where local UV distortion makes the same
## screen-space gap map to a much bigger atlas-space jump.
func _try_paint_at(screen_pos: Vector2) -> void:
	var dist := _last_paint_screen_pos.distance_to(screen_pos)
	var steps := maxi(1, int(dist / PAINT_STEP_PX))
	for i in range(1, steps + 1):
		var t := float(i) / float(steps)
		var hit := _raycast_character(_last_paint_screen_pos.lerp(screen_pos, t))
		if hit.is_empty():
			continue
		_paint_at_hit(hit)
	_last_paint_screen_pos = screen_pos

func _paint_at_hit(hit: Dictionary) -> void:
	var color := ERASE_COLOR if eraser_button.button_pressed else color_picker.color
	character.paint_at(hit["local_pos"], hit["face_index"], color, _brush_radius_px)
	if mirror_button.button_pressed:
		_paint_at_mirror(hit["local_pos"], color)

func _bucket_fill_at_hit(hit: Dictionary) -> void:
	var color := ERASE_COLOR if eraser_button.button_pressed else color_picker.color
	character.bucket_fill_at(hit["local_pos"], hit["face_index"], color)
	if mirror_button.button_pressed:
		var mirrored_pos: Vector3 = character.mirror_local_point(hit["local_pos"])
		var mirrored_face: int = character.find_face_for_point(mirrored_pos)
		if mirrored_face >= 0:
			character.bucket_fill_at(mirrored_pos, mirrored_face, color)

## Mirror mode: every stroke/fill also lands at the anatomically
## corresponding point on the OTHER side of the body -- see character.gd's
## own mirror_local_point (the rig is perfectly symmetric across local
## X=0) and find_face_for_point (relocates that mirrored 3D point on the
## mesh surface, without needing to know this mesh's own UV formula at
## all). find_face_for_point() can legitimately return -1 right at the
## exact sagittal-plane centerline (e.g. brush size 1 dead-center on the
## nose/spine, where the mirrored point IS the original point and may not
## cleanly re-locate) -- silently skipping the mirror stamp there is
## correct, not a bug: there's nothing meaningfully different to paint on
## "the other side" of a point that's already centered on the mirror line.
func _paint_at_mirror(local_pos: Vector3, color: Color) -> void:
	var mirrored_pos: Vector3 = character.mirror_local_point(local_pos)
	var mirrored_face: int = character.find_face_for_point(mirrored_pos)
	if mirrored_face >= 0:
		character.paint_at(mirrored_pos, mirrored_face, color, _brush_radius_px)

## Picks up whatever color is already painted at the clicked point and loads
## it into the color picker, ready to paint with immediately -- switches
## back to Brush right away (same ButtonGroup as Eyedropper, so this just
## un-presses Eyedropper) rather than staying in pick mode, matching how a
## one-shot eyedropper conventionally behaves in other paint tools: click,
## pick, and you're straight back to painting with that color.
func _pick_color_at_hit(hit: Dictionary) -> void:
	color_picker.color = character.sample_color_at(hit["local_pos"], hit["face_index"])
	eraser_button.button_pressed = false
	brush_button.button_pressed = true

func _on_eraser_toggled(_pressed: bool) -> void:
	pass # just flips which color _paint_at_hit/_bucket_fill_at_hit use next

## Opening the color picker is a clear signal the player wants to paint with
## a chosen color -- without this, Eraser staying silently toggled on (easy
## to forget, since it's a separate button from the color picker) would keep
## forcing every stroke to white no matter what color got picked afterward,
## making the picker look "stuck" on white.
func _on_color_picker_pressed() -> void:
	eraser_button.button_pressed = false

func _on_brush_size_changed(value: float) -> void:
	_brush_radius_px = int(value)
	brush_size_value_label.text = str(_brush_radius_px)

## Orbit around the torso, not the feet: put the pivot at the character's
## own torso height (read via get_torso_center_y() rather than hardcoding a
## duplicate number here) and shift the character down under it by the same
## amount, so its feet stay planted at the same spot and only the rotation
## center moves.
##
## Still multiplies by get_height_scale() even though the creator no longer
## exposes a height slider -- character.gd keeps that API, and a saved
## character created while the slider existed can still load back at a
## non-1.0 scale, which this has to stay correct for.
func _recenter_on_torso() -> void:
	var torso_height: float = character.get_torso_center_y() * character.get_height_scale()
	rotation_pivot.position.y = torso_height
	character.position.y = -torso_height

func _on_hair_style_pressed(style: String) -> void:
	character.set_hair_style(style)

func _on_undo_pressed() -> void:
	character.undo()
	_update_undo_redo_buttons()

func _on_redo_pressed() -> void:
	character.redo()
	_update_undo_redo_buttons()

func _update_undo_redo_buttons() -> void:
	undo_button.disabled = not character.can_undo()
	redo_button.disabled = not character.can_redo()

## "Enter City" doesn't finish creation directly anymore -- it opens the
## name prompt first, and creation_finished only fires once a name's
## actually been confirmed (see _on_name_confirmed).
func _on_enter_city_pressed() -> void:
	name_entry_overlay.visible = true
	name_line_edit.text = ""
	name_line_edit.grab_focus()

func _on_name_line_edit_submitted(_text: String) -> void:
	_on_name_confirmed()

func _on_name_confirmed() -> void:
	var typed_name := name_line_edit.text.strip_edges()
	if typed_name.is_empty():
		name_line_edit.grab_focus()
		return
	character.set_character_name(typed_name)
	name_entry_overlay.visible = false
	creation_finished.emit(character)

## Escape's meaning depends on what's already showing on top: dismiss the
## confirm-discard prompt (same as its own Cancel button) or the name-entry
## prompt (back to editing, not all the way out) if either is open, otherwise
## treat it the same as pressing Back.
func _handle_escape() -> void:
	if confirm_back_overlay.visible:
		_on_confirm_cancel_pressed()
	elif name_entry_overlay.visible:
		name_entry_overlay.visible = false
	else:
		_try_go_back()

## Shared by both the Back button and Escape. Nothing is ever auto-saved
## from the creator (see main.gd's own class comment -- a character is only
## ever saved once creation_finished actually fires), so leaving after
## painting something would silently throw it away -- can_undo() being true
## means at least one paint/bucket edit landed (see character.gd's own
## push_undo_snapshot, called right before every edit), which is what gates
## the confirmation rather than tracking a separate "dirty" flag.
func _try_go_back() -> void:
	if character.can_undo():
		confirm_back_overlay.visible = true
	else:
		creation_cancelled.emit()

func _on_confirm_discard_pressed() -> void:
	confirm_back_overlay.visible = false
	creation_cancelled.emit()

func _on_confirm_cancel_pressed() -> void:
	confirm_back_overlay.visible = false
