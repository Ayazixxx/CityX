extends Control
## Animated rain: drifting streak drops with occasional splash rings where
## they hit the bottom. Direct port of the title screen mockup's canvas
## rain effect (same drop/splash parameters), redrawn every frame.

const DROP_COUNT := 260
const DROP_COLOR := Color(200.0 / 255.0, 210.0 / 255.0, 255.0 / 255.0)
const SPLASH_COLOR := Color(210.0 / 255.0, 220.0 / 255.0, 255.0 / 255.0)

var _drops: Array = []
var _splashes: Array = []
var _rng := RandomNumberGenerator.new()

func _ready() -> void:
	_rng.randomize()
	for i in range(DROP_COUNT):
		_drops.append(_make_drop())

func _make_drop() -> Dictionary:
	return {
		"x": _rng.randf() * maxf(size.x, 1.0),
		"y": _rng.randf() * maxf(size.y, 1.0),
		"len": 10.0 + _rng.randf() * 20.0,
		"speed": 7.0 + _rng.randf() * 9.0,
		"drift": 1.2 + _rng.randf() * 0.8,
		"opacity": 0.15 + _rng.randf() * 0.35,
		"width": 2.0 if _rng.randf() < 0.15 else 1.0,
	}

func _process(_delta: float) -> void:
	queue_redraw()

func _draw() -> void:
	for d in _drops:
		var col := DROP_COLOR
		col.a = d["opacity"]
		var from := Vector2(d["x"], d["y"])
		var to := Vector2(d["x"] - d["drift"] * (d["len"] / 6.0), d["y"] + d["len"])
		draw_line(from, to, col, d["width"])

		d["x"] -= d["drift"]
		d["y"] += d["speed"]

		if d["y"] > size.y - 40.0 and d["y"] < size.y:
			_maybe_splash(d)

		if d["y"] > size.y or d["x"] < -20.0:
			d["x"] = _rng.randf() * maxf(size.x, 1.0)
			d["y"] = -20.0

	for i in range(_splashes.size() - 1, -1, -1):
		var s: Dictionary = _splashes[i]
		var col := SPLASH_COLOR
		col.a = s["opacity"]
		draw_set_transform(Vector2(s["x"], s["y"]), 0.0, Vector2(1.0, 0.35))
		draw_arc(Vector2.ZERO, s["r"], 0.0, TAU, 24, col, 1.0)
		draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)

		s["r"] += 0.4
		s["opacity"] -= 0.04
		if s["opacity"] <= 0.0 or s["r"] > s["maxR"]:
			_splashes.remove_at(i)

func _maybe_splash(d: Dictionary) -> void:
	if _rng.randf() < 0.02:
		_splashes.append({
			"x": d["x"],
			"y": size.y - _rng.randf() * 6.0,
			"r": 1.0,
			"maxR": 4.0 + _rng.randf() * 4.0,
			"opacity": 0.5,
		})
