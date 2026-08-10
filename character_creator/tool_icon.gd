extends Control
## Draws one simple flat icon (brush/bucket/eyedropper) for the character
## creator's own tool buttons -- see character_creator.gd's own
## _build_tool_icon(), which renders one of these into a small offscreen
## SubViewport once at startup and captures the result as a Button.icon
## texture, then clears the button's text entirely. Procedural rather than
## an imported image asset, matching how this project already builds
## everything else (weapons, characters) in code rather than from external
## art.

@export var icon_type: String = "brush" # "brush" | "bucket" | "eyedropper"

const STROKE_COLOR := Color(0.9, 0.92, 1.0)
const ACCENT_COLOR := Color(0.55, 0.75, 1.0)

func _draw() -> void:
	match icon_type:
		"brush":
			_draw_brush()
		"bucket":
			_draw_bucket()
		"eyedropper":
			_draw_eyedropper()

## A diagonal handle, a metal ferrule band near the tip, and a fanned-out
## bristle tip with a small paint dab under it.
func _draw_brush() -> void:
	draw_line(Vector2(10, 54), Vector2(32, 32), STROKE_COLOR, 6.0, true)
	draw_line(Vector2(28, 36), Vector2(38, 26), ACCENT_COLOR, 7.0, true)
	var tip := PackedVector2Array([Vector2(34, 30), Vector2(52, 12), Vector2(45, 7), Vector2(28, 24)])
	draw_colored_polygon(tip, STROKE_COLOR)
	draw_circle(Vector2(50, 16), 3.0, ACCENT_COLOR)

## A trapezoid pail with a rim, a top handle arc, and a drip below.
func _draw_bucket() -> void:
	var body := PackedVector2Array([Vector2(16, 26), Vector2(48, 26), Vector2(42, 54), Vector2(22, 54)])
	draw_colored_polygon(body, STROKE_COLOR)
	draw_line(Vector2(14, 26), Vector2(50, 26), ACCENT_COLOR, 4.0, true)
	draw_arc(Vector2(32, 22), 13.0, PI + 0.3, TAU - 0.3, 24, ACCENT_COLOR, 3.0, true)
	draw_circle(Vector2(32, 59), 2.5, ACCENT_COLOR)

## A round bulb, a diagonal glass stem, a tapered tip, and a falling drop.
func _draw_eyedropper() -> void:
	draw_circle(Vector2(18, 14), 9.0, ACCENT_COLOR)
	draw_line(Vector2(23, 20), Vector2(44, 41), STROKE_COLOR, 6.0, true)
	draw_line(Vector2(44, 41), Vector2(51, 48), STROKE_COLOR, 3.0, true)
	draw_circle(Vector2(53, 55), 3.0, ACCENT_COLOR)
