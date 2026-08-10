extends Node
## Regression check for three character-creator paint tool bugs, all
## reported against the SAME underlying bucket_fill_at/_flood_fill:
##
## 1. Leaking through a hand-drawn ring's own diagonal-only curve segments --
##    reported as "draw a black circle, click the center, it fills the whole
##    limb." Root cause: the flood used 8-connected neighbor expansion
##    (including diagonals), which can hop straight through a wall that's
##    only diagonally connected at some point along its own curvature (true
##    of any hand-drawn circle, which has no long straight/orthogonal runs)
##    -- neither of the two background pixels on either side of that
##    diagonal corner is ever actually ON the wall, so the boundary check
##    never gets a chance to stop the flood there. Fixed by switching to
##    4-connected (orthogonal-only) expansion, which can't skip past a wall
##    pixel the way a diagonal step can.
##
## 2. STILL leaking even after fix #1, reported again with a real
##    screenshot: brown skin background, a black outline, filled with red --
##    the fill spread past the outline instead of stopping at it (though
##    not the WHOLE limb this time, since 4-connectivity was already fixed).
##    Root cause: the boundary check compared a candidate pixel's distance
##    to target_color AGAINST its distance to the NEW fill color, on the
##    assumption the drawn outline's own color sits somewhere between the
##    two -- true only by coincidence. With brown skin as target and red as
##    the new fill, black (0,0,0) is actually CLOSER to skin (~0.95 Manhattan
##    distance) than the old check required it to be closer to red by (only
##    ~1.0 away, short of skin-distance + the old 0.6 bias), so it was never
##    treated as a wall at all -- the flood just painted straight over the
##    outline and kept going. Fixed by dropping the fill-color comparison
##    entirely: a fixed tolerance from target_color ALONE now decides what's
##    still "the same region," matching how every real paint program's own
##    bucket tolerance actually works.
##
## 3. The eyedropper tool (character.gd's own sample_color_at(), wired up
##    in character_creator.gd) -- confirms it reads back exactly whatever
##    was painted at a given point.
##
## 4. Even with #1/#2 both fixed, a real hand-drawn (not synthetic-pixel)
##    closed loop at brush size 1 still left a ragged rim of unfilled white
##    "speckle" pixels right at the boundary after filling -- reported with
##    a screenshot. Root cause: _stamp()'s own anti-aliasing "taper" was a
##    flat 1.0 real atlas pixel of softening regardless of brush size --
##    at brush size 1 (radius floors at 0.5px, see paint_at's own base_r),
##    that 1px fringe extended the falloff out to 1.5px in every direction,
##    3x the intended footprint, producing a WIDE gradient band along any
##    stroke that FLOOD_FILL_TOLERANCE (a fixed, comfortably-sized-for-a-
##    NORMAL fringe distance) couldn't fully swallow. Fixed by scaling the
##    fringe down for small brushes (see _stamp's own comment) -- this test
##    paints a REAL closed loop via actual paint_at() calls (not directly
##    poking pixels the way check #1/#2 do), at brush size 1 specifically,
##    then confirms filling its interior leaves zero unfilled speckles.

const CharacterScene := preload("res://character/character.tscn")

var _ok := true
var _character: Node3D

func _fail(msg: String) -> void:
	print("FAIL: ", msg)
	_ok = false

## Not is_equal_approx() (too tight) -- atlas_image is FORMAT_RGBA8, an
## 8-bit-per-channel format, so any color written through it and read back
## is quantized to one of 256 levels per channel (~0.004 max rounding error)
## even when nothing is actually wrong. A generous 0.01-per-channel
## tolerance easily covers that quantization while still catching a real
## color mismatch (which would be off by far more than a rounding error).
func _colors_close(a: Color, b: Color, tolerance: float = 0.01) -> bool:
	return absf(a.r - b.r) <= tolerance and absf(a.g - b.g) <= tolerance and absf(a.b - b.b) <= tolerance and absf(a.a - b.a) <= tolerance

func _ready() -> void:
	_character = CharacterScene.instantiate()
	add_child(_character)
	call_deferred("_run_checks")

func _run_checks() -> void:
	_run_flood_fill_leak_check()
	_run_flood_fill_unrelated_colors_check()
	_run_sample_color_check()
	_run_brush_speckle_check()
	_run_brush_size_one_footprint_check()
	print("OVERALL: ", "PASS" if _ok else "FAIL")
	get_tree().quit(0 if _ok else 1)

## Draws a real circular RING (not a filled disc) of BLACK pixels, 30px
## radius, centered well inside one atlas cell -- a circle's own curvature
## guarantees stretches where consecutive ring pixels only touch corner-to-
## corner (diagonally), exactly the shape that let the old 8-connected flood
## leak through. Then floods from dead center with a distinct color and
## checks a point OUTSIDE the ring (near the clamp's own far corner) stayed
## completely untouched.
func _run_flood_fill_leak_check() -> void:
	var cell_x0 := 0
	var cell_x1 := 128
	var cell_y0 := 0
	var cell_y1 := 128
	var clamp := {"x0": cell_x0, "x1": cell_x1, "y0": cell_y0, "y1": cell_y1}
	var center := Vector2i(64, 64)
	var radius := 30

	# Reset this whole region to a known clean background first -- the real
	# atlas starts all-white anyway, but being explicit here keeps this test
	# correct even if that default ever changes.
	for x in range(cell_x0, cell_x1):
		for y in range(cell_y0, cell_y1):
			_character.atlas_image.set_pixel(x, y, Color.WHITE)

	# A real circle via Bresenham-ish angle stepping -- fine-grained enough
	# (720 steps around) that the ring has no gaps, but still a genuine
	# curve (not axis-aligned segments), so plenty of consecutive pixels
	# along it are only diagonally adjacent to each other.
	for i in range(720):
		var angle := TAU * float(i) / 720.0
		var px := center.x + int(round(cos(angle) * radius))
		var py := center.y + int(round(sin(angle) * radius))
		_character.atlas_image.set_pixel(px, py, Color.BLACK)

	var fill_color := Color(0.2, 0.6, 0.9, 1.0)
	var uv := Vector2(float(center.x) / _character.atlas_size, float(center.y) / _character.atlas_height)
	_character._flood_fill(uv, fill_color, clamp)

	var inside: Color = _character.atlas_image.get_pixel(center.x, center.y)
	if _colors_close(inside, fill_color):
		print("PASS: flood fill actually filled the ring's own interior")
	else:
		_fail("center pixel is %s after flooding, expected the fill color %s" % [inside, fill_color])

	# Well outside the ring (radius 30), close to the clamp's own far
	# corner -- if the old 8-connected leak bug regressed, this would have
	# been overrun too (the reported symptom: fills the WHOLE limb/cell).
	var outside: Color = _character.atlas_image.get_pixel(10, 10)
	if _colors_close(outside, Color.WHITE):
		print("PASS: flood fill did NOT leak past the ring -- outside stayed untouched")
	else:
		_fail("pixel outside the ring is %s, expected untouched white (leaked through the ring)" % outside)

## Reproduces the exact real-world scenario from the second bug report:
## brown SKIN background, a solid BLACK outline (a closed blob shape, not
## just a circle this time -- an arbitrary hand-drawn line), filled with
## RED. With the old fill-color-comparison heuristic this leaked straight
## through the outline; with a fixed target_color-only tolerance it must
## not.
func _run_flood_fill_unrelated_colors_check() -> void:
	var cell_x0 := 0
	var cell_x1 := 128
	var cell_y0 := 128
	var cell_y1 := 256
	var clamp := {"x0": cell_x0, "x1": cell_x1, "y0": cell_y0, "y1": cell_y1}
	var skin := Color(0.45, 0.3, 0.2, 1.0)

	for x in range(cell_x0, cell_x1):
		for y in range(cell_y0, cell_y1):
			_character.atlas_image.set_pixel(x, y, skin)

	# A closed, slightly irregular blob (not a perfect circle) -- same
	# "real hand-drawn shape, not a clean geometric one" spirit as the
	# reported screenshot's own outline.
	var center := Vector2i(64, 192)
	var base_radius := 28
	for i in range(500):
		var angle := TAU * float(i) / 500.0
		var wobble := sin(angle * 5.0) * 3.0
		var r := base_radius + wobble
		var px := center.x + int(round(cos(angle) * r))
		var py := center.y + int(round(sin(angle) * r))
		_character.atlas_image.set_pixel(px, py, Color.BLACK)

	var fill_color := Color(1.0, 0.0, 0.0, 1.0)
	var uv := Vector2(float(center.x) / _character.atlas_size, float(center.y) / _character.atlas_height)
	_character._flood_fill(uv, fill_color, clamp)

	var inside: Color = _character.atlas_image.get_pixel(center.x, center.y)
	if _colors_close(inside, fill_color):
		print("PASS: flood fill (skin background, black outline, red fill) filled the interior")
	else:
		_fail("center pixel is %s after flooding, expected the fill color %s" % [inside, fill_color])

	var outside: Color = _character.atlas_image.get_pixel(10, 138)
	if _colors_close(outside, skin):
		print("PASS: flood fill (skin/black/red) did NOT leak past the outline -- outside stayed the original skin tone")
	else:
		_fail("pixel outside the outline is %s, expected untouched skin tone %s (leaked through)" % [outside, skin])

## Paints a small patch a known color, then confirms sample_color_at()
## reads back that exact color at the same point.
func _run_sample_color_check() -> void:
	var test_color := Color(0.85, 0.15, 0.35, 1.0)
	# arm_l's own row -- any real, in-range face works; using the same
	# raycast-derived local_pos/face_index paint_at/bucket_fill_at already
	# rely on, via a real hit against the live mesh.
	var hit: Dictionary = _character.raycast_paint(Vector3(0, 1.2, 2.0), Vector3(0, 0, -1), 5.0)
	if hit.is_empty():
		_fail("raycast_paint found no hit to test sample_color_at against -- can't verify")
		return

	_character.paint_at(hit["local_pos"], hit["face_index"], test_color, 3)
	var sampled: Color = _character.sample_color_at(hit["local_pos"], hit["face_index"])
	if _colors_close(sampled, test_color):
		print("PASS: sample_color_at() reads back exactly what was just painted")
	else:
		_fail("sample_color_at() returned %s, expected the just-painted %s" % [sampled, test_color])

## Draws a real closed ring on the TORSO's own front surface using actual
## paint_at() calls at brush size 1 (going through the real _stamp() anti-
## aliasing code, not directly poking pixels the way the earlier checks in
## this file do), fills its interior, then scans that interior for any
## leftover "speckle" pixels -- background-colored pixels that should have
## either been painted over by the ring itself or caught by the fill.
func _run_brush_speckle_check() -> void:
	var center_hit: Dictionary = _character.raycast_paint(Vector3(0, 1.4, 2.0), Vector3(0, 0, -1), 5.0)
	if center_hit.is_empty():
		_fail("no raycast hit on the torso to test brush speckling against")
		return
	var target_center: Vector3 = center_hit["local_pos"]

	# Reset the whole torso/hips row to a known skin tone first.
	var clamp: Dictionary = _character.tri_clamp[center_hit["face_index"]]
	var skin := Color(0.45, 0.3, 0.2, 1.0)
	for x in range(clamp["x0"], clamp["x1"]):
		for y in range(clamp["y0"], clamp["y1"]):
			_character.atlas_image.set_pixel(x, y, skin)

	# Sweep ray origins around target_center, in a small circle facing it --
	# same technique test_mirror_paint_check.gd/the earlier head-ring check
	# already use -- tracing a real, brush-painted (not synthetic) closed
	# ring directly onto the torso's own curved surface. begin_paint_stroke()
	# first resets _last_stamp_uv/_last_stamp_clamp -- without it, this
	# reused shared _character instance's own paint state is left over from
	# an EARLIER check in this same file (e.g. _run_sample_color_check's own
	# single paint_at() call on the arm), so the very first ring stamp here
	# would get bridged (see paint_at's own call into _stamp_bridged) all
	# the way back to that unrelated point instead of starting a fresh
	# stroke -- the real cause of this check's own first failed run, not a
	# genuine remaining fringe-width bug.
	_character.begin_paint_stroke()
	const RING_RADIUS := 0.12
	# Track where each real ring stamp actually landed in atlas-pixel space
	# (same uv->px conversion paint_at itself uses internally) so the scan
	# region below can be sized from the ring's OWN measured footprint
	# instead of a guessed constant -- see the comment on stamp_min/stamp_max
	# below for why a guessed constant was the real bug here.
	var stamp_min := Vector2i(999999, 999999)
	var stamp_max := Vector2i(-999999, -999999)
	for i in range(240):
		var angle := TAU * float(i) / 240.0
		var origin: Vector3 = target_center + Vector3(cos(angle), sin(angle), 0) * RING_RADIUS + Vector3(0, 0, 1.0)
		var direction: Vector3 = (target_center + Vector3(cos(angle), sin(angle), 0) * RING_RADIUS - origin).normalized()
		var hit: Dictionary = _character.raycast_paint(origin, direction, 3.0)
		if hit.is_empty():
			continue
		_character.paint_at(hit["local_pos"], hit["face_index"], Color.BLACK, 1)

		var hidx: int = hit["face_index"] * 3
		var huv: Vector2 = _character._barycentric_uv(
			hit["local_pos"],
			_character.tri_positions[hidx], _character.tri_positions[hidx + 1], _character.tri_positions[hidx + 2],
			_character.tri_uvs[hidx], _character.tri_uvs[hidx + 1], _character.tri_uvs[hidx + 2]
		)
		huv.x = fposmod(huv.x, 1.0)
		var hpx := Vector2i(int(huv.x * _character.atlas_size), int(huv.y * _character.atlas_height))
		stamp_min = Vector2i(mini(stamp_min.x, hpx.x), mini(stamp_min.y, hpx.y))
		stamp_max = Vector2i(maxi(stamp_max.x, hpx.x), maxi(stamp_max.y, hpx.y))

	var fill_color := Color(0.1, 0.85, 0.2, 1.0)
	_character.bucket_fill_at(target_center, center_hit["face_index"], fill_color)

	# Scan an ELLIPSE inset from the ring's own measured bounding box (well
	# inside it), not a plain rectangle. The ring's on-atlas footprint is a
	# strongly anisotropic ellipse, because the torso atlas row packs its
	# full circumference into the same fixed row height as its length --
	# measured directly at 31.5px half-width (circumference/U axis) vs only
	# 4.0px half-height (length/V axis), an ~8:1 ratio. A plain rectangular
	# inset (this check's own second version) still assumes the ring's
	# vertical extent is constant across its full width, which is only true
	# away from the ellipse's own "poles" (its far left/right ends) -- right
	# near a pole the ring's true vertical extent tapers toward zero exactly
	# like any ellipse's does, so a rectangle inset by a few px can still
	# reach past the ring's real (tapered) edge there into genuinely
	# untouched background just outside it, at the SAME x column a taller
	# part of the rectangle assumed was still safely interior. An ellipse
	# inset (scaling both axes by the same margin, same shape as the ring
	# itself) tapers its own scan boundary right along with the ring's real
	# one, so it can't make that mistake at any point around the ring
	# regardless of how anisotropic the body region is.
	const SCAN_MARGIN_PX := 3.0
	var bbox_cx: float = (stamp_min.x + stamp_max.x) / 2.0
	var bbox_cy: float = (stamp_min.y + stamp_max.y) / 2.0
	var ellipse_a: float = (stamp_max.x - stamp_min.x) / 2.0 - SCAN_MARGIN_PX
	var ellipse_b: float = (stamp_max.y - stamp_min.y) / 2.0 - SCAN_MARGIN_PX
	if ellipse_a < 0.5 or ellipse_b < 0.5:
		_fail("ring's own measured footprint (x=[%d,%d] y=[%d,%d]) is too small for a %spx-inset interior scan -- can't verify" % [stamp_min.x, stamp_max.x, stamp_min.y, stamp_max.y, SCAN_MARGIN_PX])
		return

	# A leftover "speckle" is a pixel that's neither close to the fill color
	# nor close to black -- i.e. still sitting somewhere in "no man's land"
	# between the ring's own outline and the fill, whether that's the
	# original untouched skin tone (a genuine gap the ring never reached) or
	# some other half-painted color (a too-wide, too-faint gradient band the
	# fill's own FLOOD_FILL_TOLERANCE can't swallow -- the ORIGINAL reported
	# form of this bug, back when _stamp's taper was a flat, oversized 1px
	# regardless of brush size: confirmed directly that checking ONLY
	# against skin (a check briefly tried while narrowing this down) stops
	# catching that wide-gradient-band failure mode entirely, since a wide
	# soft taper smears color broadly enough that almost nothing is left at
	# the *exact* original skin value even though the result is still an
	# unacceptably fuzzy, half-painted ring). The black tolerance here is
	# deliberately looser (0.25, vs 0.15 elsewhere in this file) than a
	# "fully painted" pixel needs to be -- a single real antialiased edge
	# pixel immediately touching solid black on multiple sides (confirmed by
	# hand at brush size 1, ~55% coverage) is expected softening the fill
	# itself already treats as part of the wall (too far from target_color
	# for FLOOD_FILL_TOLERANCE to have painted over), not a genuine leftover
	# gap -- 0.25 comfortably clears that single-pixel case while still
	# easily catching a genuinely untouched skin pixel (off by 0.2-0.45 per
	# channel) or a true wide no-man's-land band (which spans many pixels,
	# not just one right at a hard edge).
	var speckle_count := 0
	var scanned := 0
	for px in range(stamp_min.x, stamp_max.x + 1):
		for py in range(stamp_min.y, stamp_max.y + 1):
			var nx: float = (float(px) - bbox_cx) / ellipse_a
			var ny: float = (float(py) - bbox_cy) / ellipse_b
			if nx * nx + ny * ny > 1.0:
				continue
			if px < clamp["x0"] or px >= clamp["x1"] or py < clamp["y0"] or py >= clamp["y1"]:
				continue
			scanned += 1
			var pixel: Color = _character.atlas_image.get_pixel(px, py)
			if not _colors_close(pixel, fill_color, 0.05) and not _colors_close(pixel, Color.BLACK, 0.25):
				speckle_count += 1
	if speckle_count == 0:
		print("PASS: no unfilled speckle pixels remain inside a real brush-painted (size 1) closed ring after filling (scanned ", scanned, " pixels)")
	else:
		_fail("%d unfilled speckle pixel(s) remain inside the ring after filling (out of %d scanned)" % [speckle_count, scanned])

## Directly verifies the other half of the original report -- "size 1 should
## be 1 pixel, it seems a little too large" -- which the ring-fill speckle
## check above can't actually catch: an oversized taper makes the BRUSH
## bigger (more pixels touched per stamp), not fewer pixels filled, so a
## "leftover unfilled speckle" count stays at zero either way (confirmed
## directly: reverting _stamp's aa_fringe_px scaling back to the old flat
## 1.0px taper still passes the ring check above, since the wider taper
## paints MORE of the ring's own interior black rather than leaving gaps --
## it just makes the ring itself fatter). This check instead measures a
## single brush-size-1 stamp's own real footprint directly.
##
## NOT asserting "zero neighbors touched" -- confirmed directly (via a
## scratch check computing paint_at's own _local_uv_tangent_lengths for this
## exact torso point) that this specific spot has real, legitimate local UV
## anisotropy (~6.5:1, tangent lengths 0.00425 vs 0.0278 world-units-per-
## atlas-pixel), which paint_at's own anisotropic correction (see its own
## extensive comment) deliberately inflates ONE axis's radius to ~1px to
## compensate -- exactly the behavior that earlier fixed diamond-shaped
## brush artifacts on the head/neck. That correction can legitimately touch
## 1-2 immediate neighbor pixels along the more-compressed axis alone, which
## is a correctly-sized ~1-world-pixel brush, not an oversized one. What
## must NOT happen -- and is what actually distinguishes a real oversized
## blob (confirmed directly: the old flat 1.0px taper touches 14 pixels in
## this same 5x5 window, including ones 2 full atlas pixels away) -- is any
## touched pixel reaching a full 2 atlas pixels from center in EITHER axis
## (Chebyshev distance >= 2), or the total touched count climbing into
## "most of the scanned window" territory instead of staying a small,
## compact handful.
func _run_brush_size_one_footprint_check() -> void:
	var hit: Dictionary = _character.raycast_paint(Vector3(0, 1.4, 2.0), Vector3(0, 0, -1), 5.0)
	if hit.is_empty():
		_fail("no raycast hit on the torso to test brush size 1's footprint against")
		return

	var clamp: Dictionary = _character.tri_clamp[hit["face_index"]]
	var skin := Color(0.45, 0.3, 0.2, 1.0)
	for x in range(clamp["x0"], clamp["x1"]):
		for y in range(clamp["y0"], clamp["y1"]):
			_character.atlas_image.set_pixel(x, y, skin)

	_character.begin_paint_stroke()
	_character.paint_at(hit["local_pos"], hit["face_index"], Color.BLACK, 1)

	var idx: int = hit["face_index"] * 3
	var uv: Vector2 = _character._barycentric_uv(
		hit["local_pos"],
		_character.tri_positions[idx], _character.tri_positions[idx + 1], _character.tri_positions[idx + 2],
		_character.tri_uvs[idx], _character.tri_uvs[idx + 1], _character.tri_uvs[idx + 2]
	)
	uv.x = fposmod(uv.x, 1.0)
	var cx := int(uv.x * _character.atlas_size)
	var cy := int(uv.y * _character.atlas_height)

	var center_pixel: Color = _character.atlas_image.get_pixel(cx, cy)
	if _colors_close(center_pixel, Color.BLACK, 0.05):
		print("PASS: brush size 1's own center pixel is fully painted")
	else:
		_fail("brush size 1's center pixel is %s, expected solid black" % center_pixel)

	var touched_neighbors := 0
	var far_touched := 0
	for dx in range(-2, 3):
		for dy in range(-2, 3):
			if dx == 0 and dy == 0:
				continue
			var px := cx + dx
			var py := cy + dy
			if px < clamp["x0"] or px >= clamp["x1"] or py < clamp["y0"] or py >= clamp["y1"]:
				continue
			var pixel: Color = _character.atlas_image.get_pixel(px, py)
			if not _colors_close(pixel, skin, 0.02):
				touched_neighbors += 1
				if maxi(absi(dx), absi(dy)) >= 2:
					far_touched += 1

	const MAX_NEAR_NEIGHBORS := 4
	if far_touched > 0:
		_fail("brush size 1 touched %d pixel(s) a full 2 atlas pixels from its own center -- expected the falloff to stay within 1 pixel regardless of local UV anisotropy" % far_touched)
	elif touched_neighbors > MAX_NEAR_NEIGHBORS:
		_fail("brush size 1 touched %d neighboring pixels around its own center -- expected at most %d (a compact anisotropic footprint), not a soft/oversized blob" % [touched_neighbors, MAX_NEAR_NEIGHBORS])
	else:
		print("PASS: brush size 1 stays a compact ~1px footprint (", touched_neighbors, " neighbor(s) touched, all within 1 atlas pixel of center)")
