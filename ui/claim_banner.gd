extends CanvasLayer
## Big centred announcement text -- "BUILDING ACQUIRED" when a claim lands,
## and the refusal message when one doesn't. Fades in, holds, fades out,
## then frees nothing: the node is reused for every message its owner sends
## it (see player.gd's own _show_claim_banner).
##
## Built in code rather than laid out in the .tscn, same as
## ui/inventory_panel.gd / ui/dialogue_box.gd / ui/dev_console.gd -- every
## other UI in this project constructs itself in _ready(), which keeps the
## styling next to the constants it comes from.
##
## Deliberately separate from ui/dialogue_box.gd despite both being "text on
## screen": a dialogue box is a conversation the player advances, this is a
## timed announcement they can't interact with at all. Sharing one node
## would mean one of the two carrying state it never uses.

## Emitted once a message has fully faded back out. The claim cutscene
## doesn't wait on this (the orbit is what sets the pacing), but a caller
## that wants to chain something after the text is gone can.
signal finished

const HOLD_SECONDS := 2.2
const FADE_IN_SECONDS := 0.45
const FADE_OUT_SECONDS := 0.8

const TITLE_FONT_SIZE := 54
const TITLE_COLOR := Color(0.96, 0.94, 0.85)
const TITLE_SHADOW := Color(0.0, 0.0, 0.0, 0.75)
const SHADOW_OFFSET := 3
# Sits above the middle rather than dead centre -- the centre of the screen
# is where the action is, and during a claim that's the building itself.
const VERTICAL_ANCHOR := 0.26

var _label: Label
var _tween: Tween

func _ready() -> void:
	# Above the HUD and the dialogue box (layer 5), below the pause menu --
	# an announcement should never be covered by a health bar.
	layer = 6
	_build_ui()

func _build_ui() -> void:
	_label = Label.new()
	_label.anchor_left = 0.0
	_label.anchor_right = 1.0
	_label.anchor_top = VERTICAL_ANCHOR
	_label.anchor_bottom = VERTICAL_ANCHOR
	_label.grow_horizontal = Control.GROW_DIRECTION_BOTH
	_label.grow_vertical = Control.GROW_DIRECTION_BOTH
	_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_label.add_theme_font_size_override("font_size", TITLE_FONT_SIZE)
	_label.add_theme_color_override("font_color", TITLE_COLOR)
	# A real drop shadow, not an outline: this text lands over whatever the
	# city happens to look like at that moment (a pale facade at noon, a
	# dark street at night) and has to stay readable against both.
	_label.add_theme_color_override("font_shadow_color", TITLE_SHADOW)
	_label.add_theme_constant_override("shadow_offset_x", SHADOW_OFFSET)
	_label.add_theme_constant_override("shadow_offset_y", SHADOW_OFFSET)
	# IGNORE so it never eats a click meant for the world behind it.
	_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_label.modulate.a = 0.0
	add_child(_label)

## Shows `text` for the standard fade-in/hold/fade-out cycle. Calling again
## while a message is still on screen replaces it outright rather than
## queueing -- the second message is always the more current one, and a
## queue would leave stale text appearing seconds after the fact.
func show_message(text: String) -> void:
	if _tween != null and _tween.is_valid():
		_tween.kill()
	_label.text = text
	_label.modulate.a = 0.0
	_tween = create_tween()
	_tween.tween_property(_label, "modulate:a", 1.0, FADE_IN_SECONDS)
	_tween.tween_interval(HOLD_SECONDS)
	_tween.tween_property(_label, "modulate:a", 0.0, FADE_OUT_SECONDS)
	_tween.tween_callback(func() -> void: finished.emit())

func is_showing() -> bool:
	return _label != null and _label.modulate.a > 0.0

## Total on-screen time, so a caller pacing something else against this
## doesn't have to re-add the three constants itself.
static func total_duration() -> float:
	return FADE_IN_SECONDS + HOLD_SECONDS + FADE_OUT_SECONDS
