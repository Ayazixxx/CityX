extends Control
## Glowing red eyes hidden in two of the background buildings' windows.
## Coordinates are in the background artwork's native pixel space
## (1365x768) and get mapped through the same "cover, center-bottom"
## transform used to display that artwork, so they always land exactly on
## the windows regardless of screen size. Each pair blinks independently
## on its own random rhythm; trigger_surge() flares all of them brighter
## for a moment (used by lightning strikes).

const NAT_SIZE := Vector2(1365.0, 768.0)
const EYE_SIZE := 2.5
# Matches the original CSS radial-gradient exactly: solid color from center
# out to 70% radius (fading from the lighter red to the darker one), then
# only the outer 30% ring fades to transparent. No soft/blurred halo --
# kept tight and flat on purpose, per spec ("no glow/bloom, kept subtle").
const LIGHT_RED := Color(209.0 / 255.0, 41.0 / 255.0, 26.0 / 255.0) # #d1291a
const DARK_RED := Color(143.0 / 255.0, 17.0 / 255.0, 8.0 / 255.0) # #8f1108
const SURGE_TINT := Color(1.6, 1.0, 1.0) # brighter/redder tint during lightning

var _pairs: Array = []
var _eye_tex: ImageTexture
var _surge_timer := 0.0
var _rng := RandomNumberGenerator.new()

func _ready() -> void:
	_rng.randomize()
	_eye_tex = _make_eye_texture(16)

	_pairs = [
		{"dots": [Vector2(411, 659), Vector2(421, 658)]},
		{"dots": [Vector2(990, 707), Vector2(1000, 707)]},
	]
	for pair in _pairs:
		var nodes := []
		for dot in pair["dots"]:
			var node := TextureRect.new()
			node.texture = _eye_tex
			# Without this, TextureRect's default EXPAND_KEEP_SIZE makes its
			# minimum size match the texture's native pixels (16x16), which
			# silently overrides the tiny manual size set in _layout() back
			# up to 16x16 -- shifting the visual center away from the
			# intended window position.
			node.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
			node.stretch_mode = TextureRect.STRETCH_SCALE
			node.mouse_filter = Control.MOUSE_FILTER_IGNORE
			node.modulate = Color(1.0, 1.0, 1.0, 1.0)
			add_child(node)
			nodes.append(node)
		pair["nodes"] = nodes
		pair["state"] = "waiting"
		pair["timer"] = _rng.randf_range(2.5, 8.5)
		pair["blinks_left"] = 1

	resized.connect(_layout)
	_layout()

func _make_eye_texture(px: int) -> ImageTexture:
	var img := Image.create(px, px, false, Image.FORMAT_RGBA8)
	var center := Vector2(px / 2.0, px / 2.0)
	var max_r := px / 2.0
	var inner_r := max_r * 0.7
	for y in range(px):
		for x in range(px):
			var d := Vector2(x + 0.5, y + 0.5).distance_to(center)
			if d <= inner_r:
				var t := d / inner_r
				var col := LIGHT_RED.lerp(DARK_RED, t)
				img.set_pixel(x, y, Color(col.r, col.g, col.b, 1.0))
			elif d <= max_r:
				var t2 := (d - inner_r) / (max_r - inner_r)
				img.set_pixel(x, y, Color(DARK_RED.r, DARK_RED.g, DARK_RED.b, 1.0 - t2))
			else:
				img.set_pixel(x, y, Color(0.0, 0.0, 0.0, 0.0))
	return ImageTexture.create_from_image(img)

func _layout() -> void:
	if size.x <= 0 or size.y <= 0:
		return
	var scale_factor := maxf(size.x / NAT_SIZE.x, size.y / NAT_SIZE.y)
	var disp := NAT_SIZE * scale_factor
	var offset := Vector2((size.x - disp.x) / 2.0, size.y - disp.y)

	for pair in _pairs:
		for i in range(pair["dots"].size()):
			var dot: Vector2 = pair["dots"][i]
			var node: TextureRect = pair["nodes"][i]
			var sz := maxf(2.0, EYE_SIZE * scale_factor)
			var pos := offset + dot * scale_factor - Vector2(sz, sz) / 2.0
			node.position = pos
			node.size = Vector2(sz, sz)
			node.pivot_offset = Vector2(sz, sz) / 2.0

func trigger_surge() -> void:
	_surge_timer = 0.26

func _process(delta: float) -> void:
	for pair in _pairs:
		pair["timer"] -= delta
		if pair["timer"] > 0.0:
			continue
		match pair["state"]:
			"waiting":
				pair["blinks_left"] = 2 if _rng.randf() < 0.25 else 1
				_set_closed(pair, true)
				pair["state"] = "closed"
				pair["timer"] = 0.13 + _rng.randf() * 0.08
			"closed":
				_set_closed(pair, false)
				pair["blinks_left"] -= 1
				if pair["blinks_left"] > 0:
					pair["state"] = "open_gap"
					pair["timer"] = 0.16
				else:
					pair["state"] = "waiting"
					pair["timer"] = _rng.randf_range(2.5, 8.5)
			"open_gap":
				_set_closed(pair, true)
				pair["state"] = "closed"
				pair["timer"] = 0.13 + _rng.randf() * 0.08

	if _surge_timer > 0.0:
		_surge_timer -= delta
	var tint := SURGE_TINT if _surge_timer > 0.0 else Color(1.0, 1.0, 1.0)
	for pair in _pairs:
		for node in pair["nodes"]:
			var a: float = node.modulate.a
			node.modulate = Color(tint.r, tint.g, tint.b, a)

func _set_closed(pair: Dictionary, closed: bool) -> void:
	for node in pair["nodes"]:
		node.modulate.a = 0.0 if closed else 1.0
		node.scale = Vector2(1.0, 0.05) if closed else Vector2.ONE
