extends SceneTree
## Regression check for the sunset/sunrise glow added to
## city/day_night_cycle.gd: a warm red/orange tint that blends in on top of
## the plain day<->night lerp, keyed off how close the SUN itself is to the
## horizon (peaking at both of its own crossings -- sunrise at _time=0,
## sunset at _time=DAY_SECONDS, which is also exactly when the moon crosses
## on the opposite side, since they share one axis 180 degrees apart) and
## fading back to nothing well before/after each crossing, so the rest of
## the day/night cycle (noon, midnight) stays completely unaffected.

const CityChunk = preload("res://city/city_chunk.gd")

var _city: Node3D
var _cycle: Node
var _frame := 0

func _init():
	var CityScene: PackedScene = load("res://city/city.tscn")
	_city = CityScene.instantiate()
	get_root().add_child(_city)
	_cycle = _city.get_node("DayNightCycle")

func _process(_delta: float) -> bool:
	_frame += 1
	if _frame < 3:
		return false
	_run_checks()
	return true

func _colors_close(a: Color, b: Color, tolerance: float = 0.02) -> bool:
	return absf(a.r - b.r) <= tolerance and absf(a.g - b.g) <= tolerance and absf(a.b - b.b) <= tolerance

func _sample(t: float) -> void:
	_cycle._time = t
	_cycle._process(0.0)

func _run_checks() -> void:
	var ok := true

	# Sunset crossing: sun_pitch=-180 at _time=DAY_SECONDS exactly (see
	# _sun_pitch_degrees) -- the glow should be at (or extremely near) full
	# strength, tinting the horizon and the sun's own light color warm.
	_sample(_cycle.DAY_SECONDS)
	var sunset_horizon: Color = _cycle._sky_mat.sky_horizon_color
	if _colors_close(sunset_horizon, _cycle.SUNSET_COLOR, 0.05):
		print("PASS: sky horizon is warm red/orange right at the sunset crossing (", sunset_horizon, ")")
	else:
		print("FAIL: sky horizon at the sunset crossing is ", sunset_horizon, ", expected close to SUNSET_COLOR ", _cycle.SUNSET_COLOR)
		ok = false

	var sunset_sun_color: Color = _cycle._sun.light_color
	if _colors_close(sunset_sun_color, _cycle.SUNSET_SUN_COLOR, 0.05):
		print("PASS: the sun's own direct light color warms up at the sunset crossing (", sunset_sun_color, ")")
	else:
		print("FAIL: sun light color at the sunset crossing is ", sunset_sun_color, ", expected close to SUNSET_SUN_COLOR ", _cycle.SUNSET_SUN_COLOR)
		ok = false

	# Sunrise crossing: sun_pitch=0 at _time=0 -- same physical moment as
	# dusk (sun right on the horizon), so the glow must appear here too,
	# not just once per cycle.
	_sample(0.0)
	var sunrise_horizon: Color = _cycle._sky_mat.sky_horizon_color
	if _colors_close(sunrise_horizon, _cycle.SUNSET_COLOR, 0.05):
		print("PASS: sky horizon is ALSO warm red/orange right at the sunrise crossing (", sunrise_horizon, ")")
	else:
		print("FAIL: sky horizon at the sunrise crossing is ", sunrise_horizon, ", expected close to SUNSET_COLOR ", _cycle.SUNSET_COLOR)
		ok = false

	# Noon: sun straight overhead (elevation=1), nowhere near the horizon --
	# the glow must be fully faded out, leaving the plain day colors alone.
	_sample(_cycle.DAY_SECONDS / 2.0)
	var noon_horizon: Color = _cycle._sky_mat.sky_horizon_color
	var noon_sun_color: Color = _cycle._sun.light_color
	if _colors_close(noon_horizon, _cycle.SKY_HORIZON_DAY, 0.02) and _colors_close(noon_sun_color, _cycle.SUN_COLOR, 0.02):
		print("PASS: noon shows the plain day colors, unaffected by the sunset glow")
	else:
		print("FAIL: noon colors drifted from the plain day preset (horizon=", noon_horizon, " sun_color=", noon_sun_color, ")")
		ok = false

	# Real reported bug: light_energy=0 doesn't stop a DirectionalLight3D
	# from also casting its own shadow, so Sun and Moon both having
	# shadow_enabled on permanently rendered two overlapping shadow
	# directions at once ("should just be 1 shadow"). At noon the sun is
	# the only one actually above the horizon -- only ITS shadow should be
	# on.
	if _cycle._sun.shadow_enabled and not _cycle._moon.shadow_enabled:
		print("PASS: only the sun casts a shadow at noon (moon's own shadow is off)")
	else:
		print("FAIL: shadow_enabled wrong at noon (sun=", _cycle._sun.shadow_enabled, " moon=", _cycle._moon.shadow_enabled, ")")
		ok = false

	# Midnight: sun straight opposite (moon overhead), also nowhere near
	# the horizon -- same requirement on the night side.
	_sample(_cycle.DAY_SECONDS + _cycle.NIGHT_SECONDS / 2.0)
	var midnight_horizon: Color = _cycle._sky_mat.sky_horizon_color
	if _colors_close(midnight_horizon, _cycle.SKY_HORIZON_NIGHT, 0.02):
		print("PASS: midnight shows the plain night colors, unaffected by the sunset glow")
	else:
		print("FAIL: midnight horizon color drifted from the plain night preset (", midnight_horizon, ")")
		ok = false

	# Same "only the light actually above the horizon casts a shadow" check,
	# mirrored on the night side.
	if _cycle._moon.shadow_enabled and not _cycle._sun.shadow_enabled:
		print("PASS: only the moon casts a shadow at midnight (sun's own shadow is off)")
	else:
		print("FAIL: shadow_enabled wrong at midnight (sun=", _cycle._sun.shadow_enabled, " moon=", _cycle._moon.shadow_enabled, ")")
		ok = false

	# fog_light_color must still track sky_horizon_color exactly even with
	# the new sunset blend added -- this is a load-bearing existing
	# invariant (see day_night_cycle.gd's own comment on why) that the new
	# code touches the same variable through, not a separate path.
	_sample(_cycle.DAY_SECONDS)
	var fog_color: Color = _cycle._world_env.environment.fog_light_color
	var horizon_color: Color = _cycle._sky_mat.sky_horizon_color
	if _colors_close(fog_color, horizon_color, 0.001):
		print("PASS: fog_light_color still exactly matches sky_horizon_color during the sunset glow")
	else:
		print("FAIL: fog_light_color (", fog_color, ") no longer matches sky_horizon_color (", horizon_color, ") during the sunset glow")
		ok = false

	# Real reported bug: buildings (large enough to span multiple shadow
	# cascades) showed a hard straight-line seam cutting through their own
	# shadow at the cascade boundary, while the player's own small nearby
	# shadow (never spans one) looked fine -- directional_shadow_blend_splits
	# defaults to false, which is exactly this "hard cut between cascades"
	# behavior. Both Sun and Moon need it on, not just whichever is
	# currently lit, since the other becomes live the instant it crosses
	# the horizon.
	if _cycle._sun.directional_shadow_blend_splits and _cycle._moon.directional_shadow_blend_splits:
		print("PASS: both sun and moon blend shadow cascade splits (no hard seam across large objects)")
	else:
		print("FAIL: directional_shadow_blend_splits not enabled on both lights (sun=", _cycle._sun.directional_shadow_blend_splits, " moon=", _cycle._moon.directional_shadow_blend_splits, ")")
		ok = false

	# --- the filmic lighting setup itself ---------------------------------
	# Reported as "the lighting looks cheap / Roblox-ish / like a
	# single-prompt game". The diagnosis was that the Environment was running
	# on engine DEFAULTS -- a Linear tonemapper, no glow, and an ambient
	# almost as strong as the sun -- which is literally the ungraded look.
	# Each of these is one property, and losing any of them silently puts
	# that look straight back.
	var env: Environment = _cycle._world_env.environment
	if env.tonemap_mode == Environment.TONE_MAPPER_LINEAR:
		print("FAIL: tonemap is back to Linear -- that's raw ungraded output, the flat look")
		ok = false
	else:
		print("PASS: a filmic tonemapper is applied (mode ", env.tonemap_mode, "), not Linear")

	# Glow was tried and REMOVED on request -- it read as a haze over
	# everything rather than as light. Asserted off so it can't drift back
	# in: this is a taste call that was already made, not an oversight.
	if env.glow_enabled:
		print("FAIL: glow is back on -- it was removed on request for hazing the image")
		ok = false
	else:
		print("PASS: glow stays off, as asked")

	# SSIL likewise: its indirect-light estimate was a source of the reported
	# grain in shadowed areas, and its contribution to a flat-coloured city
	# was small enough not to be worth the noise.
	if env.ssil_enabled:
		print("FAIL: SSIL is back on -- it was removed for graining up the shadows")
		ok = false
	else:
		print("PASS: SSIL stays off, so shadowed areas stay clean")

	# The real sun is ~0.53 degrees across. Anything much wider asks the
	# shadow filter for a huge penumbra and gets speckle back -- which is
	# exactly what the grain turned out to be.
	if _cycle.SUN_ANGULAR_DISTANCE > 2.0:
		print("FAIL: the sun is %.1f degrees wide -- that penumbra will be grainy" % _cycle.SUN_ANGULAR_DISTANCE)
		ok = false
	else:
		print("PASS: the sun is %.1f degrees across, close to life, so its shadows stay clean" % _cycle.SUN_ANGULAR_DISTANCE)

	# CONTRAST RATIO is the real measurement behind "it looks flat". Sun
	# versus ambient at noon: high uniform ambient fills every shadow and
	# erases the shading gradient that makes a surface read as solid. It
	# used to be 1.0 sun against 0.7 ambient -- nearly 1:1.
	_sample(_cycle.DAY_SECONDS * 0.5)
	var sun_energy: float = _cycle._sun.light_energy
	var ambient_energy: float = env.ambient_light_energy
	var ratio: float = sun_energy / maxf(ambient_energy, 0.0001)
	if ratio < 4.0:
		print("FAIL: noon sun/ambient ratio is only %.1f:1 -- too flat, the ambient is washing out the shading" % ratio)
		ok = false
	else:
		print("PASS: noon sun outweighs ambient %.1f:1, so surfaces get real shading" % ratio)

	# The sun must be WARM. A blue sun (it was 0.82, 0.86, 1.0) under a blue
	# ambient is the cold, lifeless cast the whole pass was about.
	var noon_sun: Color = _cycle._sun.light_color
	if noon_sun.r < noon_sun.b:
		print("FAIL: the noon sun is blue (", noon_sun, ") -- daylight is warm, the sky is what's blue")
		ok = false
	else:
		print("PASS: the noon sun is warm (", noon_sun, "), with the blue left to the sky/ambient")

	# GOLDEN HOUR HAS TO BE LIT. The sun's energy used to fall off linearly
	# with elevation, so at the exact moment the sky turned orange the sun
	# had almost nothing left and the "sunset" was a coloured backdrop behind
	# an unlit city. Sampled a little before the crossing, where the warm
	# light should be at its most useful.
	_sample(_cycle.DAY_SECONDS * 0.94)
	var golden_energy: float = _cycle._sun.light_energy
	var golden_fraction: float = golden_energy / _cycle.SUN_ENERGY
	if golden_fraction < 0.25:
		print("FAIL: the sun is down to %.0f%% of full during golden hour -- nothing is actually lit by it" % (golden_fraction * 100.0))
		ok = false
	else:
		print("PASS: the sun still carries %.0f%% of its energy through golden hour, so it really lights the city" % (golden_fraction * 100.0))

	# And the fog has to scatter that light, or the glowing sky sits behind a
	# stubbornly neutral grey wall.
	_sample(_cycle.DAY_SECONDS)
	if env.fog_sun_scatter <= _cycle.FOG_SUN_SCATTER_BASE:
		print("FAIL: fog_sun_scatter doesn't rise at sunset (", env.fog_sun_scatter, ") -- the haze won't catch the warm light")
		ok = false
	else:
		print("PASS: the fog scatters the sun's own colour at sunset (scatter %.2f)" % env.fog_sun_scatter)

	print("OVERALL: ", "PASS" if ok else "FAIL")
	quit(0 if ok else 1)
