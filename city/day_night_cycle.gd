extends Node
## Minecraft-style day/night cycle: a real-time clock drives the sun and
## moon rotating across the sky, always exactly opposite each other (when
## one sets, the other rises), plus a matching shift in ambient light and
## sky/fog color between day and night presets. 20 real minutes of day, 14
## of night (a 34-minute full cycle) -- deliberately uneven, not a plain
## 50/50 split, per the actual request this was built from.
##
## Both Sun and Moon are separate DirectionalLight3D children (see
## city.tscn) rather than one light that reskins itself -- confirmed
## directly that Godot's ProceduralSkyMaterial renders an independent disc
## per light, so two real lights read as two real celestial bodies (the
## moon visibly rising as the sun sets), not a single dot changing color.
##
## Both share one rotation axis (AZIMUTH_DEGREES) so they trace the same
## great-circle arc through the sky, permanently 180 degrees apart --
## whichever one is currently above the horizon lights the world; the
## other fades out as it dips below, rather than both contributing at once
## like a physically-wrong double-noon.

const DAY_SECONDS := 20.0 * 60.0
const NIGHT_SECONDS := 14.0 * 60.0
const CYCLE_SECONDS := DAY_SECONDS + NIGHT_SECONDS

# Matches the original fixed light's own azimuth (city.tscn, before this
# cycle replaced it) so noon shadows still fall the same general direction
# the world was already tuned to look right under.
const AZIMUTH_DEGREES := -35.0

# Daylight is WARM, not blue. The original sun here was (0.82, 0.86, 1.0) --
# a blue lamp -- and paired with a blue ambient and a navy sky it produced
# the flat, cold, "cheap engine default" look this pass exists to fix. Real
# midday sun is roughly 5500-6000K, which lands slightly warm of white; the
# BLUE in an outdoor scene belongs to the sky filling the shadows, which is
# what AMBIENT_DAY_COLOR is for. Warm key against cool fill is most of what
# reads as "real lighting".
const SUN_COLOR := Color(1.0, 0.955, 0.885, 1)
# Bright enough to make real contrast against the ambient below. Values
# above 1.0 are meaningful now that the Environment tonemaps (AgX): the
# renderer works in HDR and the curve brings it back down, which is what
# gives highlights their roll-off instead of clipping flat to white.
const SUN_ENERGY := 2.4
# The real sun subtends about 0.53 degrees. This was 6.0 -- eleven times too
# wide -- which asks the shadow filter for an enormous soft penumbra and
# gets back visible speckle instead, the "grainy shadows" this was reported
# as. It was always grainy; it only became VISIBLE once the ambient came
# down and shadows stopped being washed out. Slightly wider than life, which
# keeps edges from looking razor-cut without costing cleanliness.
const SUN_ANGULAR_DISTANCE := 1.0
# Real moonlight is roughly a millionth of sunlight -- far too dim to see
# anything by in a game. Picked instead so night is clearly darker and
# cooler than day without going unplayably black.
const MOON_COLOR := Color(0.62, 0.72, 1.0, 1)
const MOON_ENERGY := 0.13
# Same reasoning as SUN_ANGULAR_DISTANCE. The moon is about the same
# apparent size as the sun; a touch wider here only because its light is dim
# enough that a soft edge never gets noisy.
const MOON_ANGULAR_DISTANCE := 1.5

# The SKY's own colour, because that's physically what ambient light
# outdoors IS -- the blue dome filling everything the sun doesn't reach
# directly. Energy is a fraction of what it was (0.7): high uniform ambient
# floods every crevice and erases the shading gradient that makes a surface
# read as solid, which is exactly the Roblox-ish flatness. The sun does the
# lighting now; this only fills.
const AMBIENT_DAY_COLOR := Color(0.45, 0.58, 0.85, 1)
const AMBIENT_DAY_ENERGY := 0.3
const AMBIENT_NIGHT_COLOR := Color(0.12, 0.15, 0.28, 1)
const AMBIENT_NIGHT_ENERGY := 0.09

# A real daytime sky: saturated blue overhead, pale and desaturated toward
# the horizon (that's aerial perspective -- more atmosphere between you and
# the horizon scatters more light back). The old values were a dark navy
# dome that lit nothing and read as dusk at noon.
const SKY_TOP_DAY := Color(0.22, 0.42, 0.82, 1)
const SKY_HORIZON_DAY := Color(0.64, 0.76, 0.91, 1)
const SKY_TOP_NIGHT := Color(0.015, 0.02, 0.05, 1)
const SKY_HORIZON_NIGHT := Color(0.04, 0.05, 0.1, 1)
const GROUND_BOTTOM_DAY := Color(0.16, 0.16, 0.18, 1)
const GROUND_BOTTOM_NIGHT := Color(0.015, 0.015, 0.03, 1)
# The sky dome's own brightness, separate from its colour. Dropped hard at
# night so the night sky doesn't sit at daytime luminance under the tonemap
# curve -- and so its reflections (the Environment reflects the sky) don't
# light the city like an overcast afternoon at midnight.
const SKY_ENERGY_DAY := 1.0
const SKY_ENERGY_NIGHT := 0.15

# Sunset/sunrise glow -- a warm red/orange blended IN ON TOP of the plain
# day<->night lerp above, not a third fixed preset colors get lerped
# between. The plain day/night lerp alone means dusk just fades the sky's
# already-blueish day color straight down to the night color with no warm
# color ever appearing -- true of a sunset in real life too (the sun's low
# angle scatters red/orange light far more than its own blue), so this
# needs its own separate contribution keyed off how close the sun is to the
# horizon, not off the same day/night blend value.
const SUNSET_COLOR := Color(1.0, 0.4, 0.16, 1)
# How much weaker the glow reaches the ZENITH (top of the sky) / ambient
# light than the horizon itself -- a real sunset's color is concentrated in
# a band near the horizon, with the upper sky staying comparatively cool;
# tinting literally everything the same solid orange would read as a color
# filter over the whole world, not a horizon glow.
const SUNSET_TOP_WEIGHT := 0.45
const SUNSET_AMBIENT_WEIGHT := 0.5
# Also warms the SUN's own direct light color (not just the sky backdrop) --
# this is what actually makes lit surfaces (building facades, the ground)
# look sunset-lit rather than just a colored sky dome behind normally-lit
# geometry. Slightly less saturated than SUNSET_COLOR so directly-lit
# surfaces don't blow out to pure orange.
const SUNSET_SUN_COLOR := Color(1.0, 0.55, 0.32, 1)
# How far above/below the horizon (in the same sin(elevation) units
# daylight's own smoothstep already uses, so 1.0 would be straight
# overhead) the glow is still visible before fully fading out -- ~17
# degrees of elevation on either side of the horizon, matching how a real
# sunset's dramatic color is a relatively brief window right around
# sun-touching-the-horizon, not something that lingers through most of the
# afternoon/morning.
const SUNSET_ELEVATION_HALF_WIDTH := 0.3

# GOLDEN HOUR. The sun's brightness used to fall off linearly with its own
# elevation, which meant that at the exact moment the sky went orange the
# sun had almost no energy left -- the sunset was a coloured backdrop behind
# an unlit world. An exponent below 1 holds the light up while the sun is
# low and then drops it quickly through the horizon, which is much closer to
# how real sunlight behaves (the atmosphere barely dims it until the very
# last few degrees). This is what makes the low warm light actually LAND on
# building faces.
const SUN_ELEVATION_FALLOFF_EXP := 0.55

# How much the fog scatters the sun's own colour back at you. At altitude
# this is subtle; when the sun is sitting on the horizon it's the entire
# reason a hazy sunset glows rather than sitting behind a flat grey wall.
const FOG_SUN_SCATTER_BASE := 0.2
const FOG_SUN_SCATTER_SUNSET := 0.75

# Volumetric fog carries the light shafts. Thicker at dawn/dusk (when real
# air is coolest and mistiest, and when low sun rakes through it) and
# thinner at noon, so the shafts show up when they're worth having instead
# of hazing over the whole day.
const VOLUMETRIC_DENSITY_NOON := 0.008
const VOLUMETRIC_DENSITY_SUNSET := 0.022
const VOLUMETRIC_DENSITY_NIGHT := 0.014
const VOLUMETRIC_ALBEDO_DAY := Color(0.85, 0.88, 0.95, 1)
const VOLUMETRIC_ALBEDO_SUNSET := Color(1.0, 0.72, 0.5, 1)

@onready var _sun: DirectionalLight3D = $Sun
@onready var _moon: DirectionalLight3D = $Moon
@onready var _world_env: WorldEnvironment = get_parent().get_node("WorldEnvironment")

var _time := DAY_SECONDS * 0.25 # start mid-morning, not at the exact sunrise instant
var _sky_mat: ProceduralSkyMaterial
# Multiplier folded into the ambient energy every frame (see _process) --
# driven by player.gd's own enter_building()/exit_building(). See
# set_indoor_dim for why it lives here rather than being written straight
# onto the Environment from outside.
var _indoor_dim := 1.0

## Scales the world's ambient light, for darkening building interiors (see
## player.gd's own enter_building()). Routed through this node rather than
## written onto the Environment directly for two reasons: _process() here
## rewrites ambient_light_energy every frame from the day/night blend, so a
## direct external write would be overwritten within one frame; and the
## other obvious channel -- a per-camera Camera3D.environment override -- is
## already spoken for by player/intro_cutscene.gd's own fog, and only one
## override can be active at a time.
##
## Ambient specifically, not the sun: sunlight is directional and already
## blocked by a building's own walls/roof, so it isn't what makes interiors
## read flat and evenly lit. Godot's ambient light is unoccluded -- it
## reaches inside a sealed basement exactly as brightly as it lights the
## open street, which is precisely why somewhere with no windows at all
## still looked fully lit before this.
func set_indoor_dim(factor: float) -> void:
	_indoor_dim = clampf(factor, 0.0, 1.0)

func _ready() -> void:
	_sun.light_color = SUN_COLOR
	_sun.light_angular_distance = SUN_ANGULAR_DISTANCE
	_moon.light_color = MOON_COLOR
	_moon.light_angular_distance = MOON_ANGULAR_DISTANCE
	# DirectionalLight3D shadows split into 4 cascades by distance from the
	# camera (defaults: 10%/20%/50%/100% of directional_shadow_max_distance,
	# i.e. ~10m/20m/50m/100m here) so nearby objects get high-resolution
	# shadow detail without needing that same resolution out to the full
	# view distance. blend_splits defaults to OFF, which hard-cuts between
	# cascades -- invisible for anything small enough to stay within one
	# cascade (confirmed: the player's own nearby shadow "looks fine," per
	# direct report), but a building can easily span tens of meters and
	# cross a cascade boundary itself, showing up as a straight-line seam
	# cutting through what should be one continuous shadow. Turning blending
	# on cross-fades adjacent cascades instead of hard-switching between them.
	_sun.directional_shadow_blend_splits = true
	_moon.directional_shadow_blend_splits = true
	_sky_mat = _world_env.environment.sky.sky_material

func _process(delta: float) -> void:
	_time = fmod(_time + delta, CYCLE_SECONDS)

	var sun_pitch := _sun_pitch_degrees()
	var moon_pitch := sun_pitch - 180.0
	_sun.rotation_degrees = Vector3(sun_pitch, AZIMUTH_DEGREES, 0)
	_moon.rotation_degrees = Vector3(moon_pitch, AZIMUTH_DEGREES, 0)

	# sin(elevation) of a body whose rotation.x is `pitch` (with this same
	# axis/order) works out to -sin(pitch) -- confirmed numerically against
	# 8 sample angles spanning the full circle before relying on it, same
	# way the corner-hugging staircase rotation was verified earlier rather
	# than assumed. Computed once here (both the day/night blend and the
	# sunset glow below key off this same value) rather than re-deriving it
	# separately in three places.
	var sun_elev_sine := -sin(deg_to_rad(sun_pitch))
	var moon_elev_sine := -sin(deg_to_rad(moon_pitch))
	# Clamping to >=0 fades each light out the instant it dips below the
	# horizon instead of lighting the world from underneath. The exponent is
	# what keeps the golden hour actually lit -- see
	# SUN_ELEVATION_FALLOFF_EXP.
	_sun.light_energy = SUN_ENERGY * pow(clampf(sun_elev_sine, 0.0, 1.0), SUN_ELEVATION_FALLOFF_EXP)
	_moon.light_energy = MOON_ENERGY * pow(clampf(moon_elev_sine, 0.0, 1.0), SUN_ELEVATION_FALLOFF_EXP)
	# light_energy=0 does NOT stop a DirectionalLight3D from casting its own
	# shadow -- a real, easy-to-miss Godot gotcha (the shadow pass runs
	# regardless of energy) that was producing exactly the reported "should
	# just be 1 shadow" bug: Sun and Moon both had shadow_enabled on
	# permanently, so anywhere both happened to be even slightly above the
	# horizon at once (or one at 0 energy but still geometrically "up")
	# rendered two overlapping shadow directions blended together instead of
	# one clean one. Toggling shadow_enabled off for whichever light is
	# actually below the horizon this frame means only the one body that's
	# visibly lighting the world ever casts a shadow.
	_sun.shadow_enabled = sun_elev_sine > 0.0
	_moon.shadow_enabled = moon_elev_sine > 0.0

	# A smoothed day/night blend (0=full night, 1=full day) for ambient and
	# sky color -- smoothstep across a band straddling the horizon so dusk
	# and dawn shift gradually, instead of the world's whole ambient color
	# snapping the instant the sun's own point-sample light_energy hits 0.
	var daylight: float = smoothstep(-0.15, 0.15, sun_elev_sine)

	# How close the SUN itself is to the horizon (0 = well clear of it in
	# either direction, 1 = sitting right on it) -- peaks at BOTH of the
	# sun's own horizon crossings (sunrise AND sunset are the same physical
	# moment, just opposite direction), which is also exactly when the moon
	# crosses the horizon on the opposite side (they share one axis, always
	# 180 degrees apart -- see this file's own class comment), so a single
	# sun-relative value already covers both bodies without needing a
	# separate moon-side glow term.
	var sunset_amount := 1.0 - smoothstep(0.0, SUNSET_ELEVATION_HALF_WIDTH, absf(sun_elev_sine))

	_sun.light_color = SUN_COLOR.lerp(SUNSET_SUN_COLOR, sunset_amount)

	var ambient_color := AMBIENT_NIGHT_COLOR.lerp(AMBIENT_DAY_COLOR, daylight)
	ambient_color = ambient_color.lerp(SUNSET_COLOR, sunset_amount * SUNSET_AMBIENT_WEIGHT)
	_world_env.environment.ambient_light_color = ambient_color
	# _indoor_dim (see set_indoor_dim) folds in HERE rather than anywhere
	# else because this line runs every single frame and would otherwise
	# stomp any ambient change made from outside a moment later.
	_world_env.environment.ambient_light_energy = lerpf(AMBIENT_NIGHT_ENERGY, AMBIENT_DAY_ENERGY, daylight) * _indoor_dim

	# The glow is strongest right at the horizon and weaker toward the
	# zenith (SUNSET_TOP_WEIGHT) -- a real sunset's color is concentrated in
	# a band near the horizon, not a flat tint over the whole sky dome.
	var sky_horizon := SKY_HORIZON_NIGHT.lerp(SKY_HORIZON_DAY, daylight)
	sky_horizon = sky_horizon.lerp(SUNSET_COLOR, sunset_amount)
	var sky_top := SKY_TOP_NIGHT.lerp(SKY_TOP_DAY, daylight)
	sky_top = sky_top.lerp(SUNSET_COLOR, sunset_amount * SUNSET_TOP_WEIGHT)
	_sky_mat.sky_top_color = sky_top
	_sky_mat.sky_horizon_color = sky_horizon
	_sky_mat.ground_horizon_color = sky_horizon
	var ground_bottom := GROUND_BOTTOM_NIGHT.lerp(GROUND_BOTTOM_DAY, daylight)
	_sky_mat.ground_bottom_color = ground_bottom.lerp(SUNSET_COLOR, sunset_amount * SUNSET_TOP_WEIGHT)
	# fog_light_color must keep matching sky_horizon_color exactly -- that's
	# the whole trick hiding the streamed world's own loaded-chunk edge
	# (see city.gd's LOAD_RADIUS comment); letting them drift apart here
	# would make that boundary visible again every dawn and dusk. The
	# sunset tint is already baked into sky_horizon above, so this still
	# just mirrors it rather than needing its own separate blend.
	_world_env.environment.fog_light_color = sky_horizon

	# The sky's own brightness, and the reflections taken off it.
	_sky_mat.sky_energy_multiplier = lerpf(SKY_ENERGY_NIGHT, SKY_ENERGY_DAY, daylight)
	_sky_mat.ground_energy_multiplier = lerpf(SKY_ENERGY_NIGHT, SKY_ENERGY_DAY * 0.6, daylight)

	# Scattering the sun's own colour through the fog. This is what turns a
	# flat grey distance wall into a glowing hazy horizon at dusk -- without
	# it the sky can be as orange as it likes and the fog in front of it
	# stays stubbornly neutral.
	_world_env.environment.fog_sun_scatter = lerpf(FOG_SUN_SCATTER_BASE, FOG_SUN_SCATTER_SUNSET, sunset_amount)

	# Volumetric fog: density and tint. Thicker and warmer through dusk, so
	# low sun rakes visible shafts between the buildings.
	var volumetric_density := lerpf(VOLUMETRIC_DENSITY_NIGHT, VOLUMETRIC_DENSITY_NOON, daylight)
	_world_env.environment.volumetric_fog_density = lerpf(
		volumetric_density, VOLUMETRIC_DENSITY_SUNSET, sunset_amount)
	_world_env.environment.volumetric_fog_albedo = VOLUMETRIC_ALBEDO_DAY.lerp(
		VOLUMETRIC_ALBEDO_SUNSET, sunset_amount)

func _sun_pitch_degrees() -> float:
	if _time < DAY_SECONDS:
		return -180.0 * (_time / DAY_SECONDS)
	else:
		return -180.0 - 180.0 * ((_time - DAY_SECONDS) / NIGHT_SECONDS)
