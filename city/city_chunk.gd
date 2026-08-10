extends RefCounted
## Pure generation helpers for one city chunk's contents: a ground slab and
## usually one procedurally sized building, built from a seeded RNG so the
## same chunk coordinate always produces the same result on any machine or
## session -- see city.gd, which calls into this to grow/shrink the
## streamed world as the player walks, the same way Minecraft only builds
## the terrain chunks near the player instead of the whole world at once.
##
## Buildings are randomly sized (footprint and height) per chunk, but every
## one gets a walk-through front door and a stack of empty interior floors
## just inside it, connected by straight staircases, with the top floor's
## own staircase continuing up through the roof (see _build_building/
## _build_interior) -- collision is built wall-by-wall rather than as one
## solid block specifically so that door (and every stairwell opening)
## isn't just decorative (see _add_building_collision). Every wall panel
## fits its own window grid independently (see _cell_layout/_add_wall_panel)
## so a wall edge -- or a door's own edges -- never slices through the
## middle of a window. The bottom few rows of windows on each building's 3
## plain walls are real, breakable glass (see building_glass.gd) --
## transparent, shatterable by gunfire or melee, with real collision that
## opens up once broken.

const BuildingGlassScript := preload("res://city/building_glass.gd")
const BuildingDoorScript := preload("res://city/building_door.gd")

# Fallback body color for call sites that don't pick a FACADE_VARIANTS entry
# themselves (see _build_building's own facade_mat/body_color params) --
# kept equal to FACADE_VARIANTS[0]'s own "body" so an unspecified building
# still reads as the same neutral grey it always used to.
const DEFAULT_FACADE_BODY_COLOR := Color(0.74, 0.745, 0.753)

# A small, curated palette of facade BODY colors, each picked per-building
# (see generate_chunk's own _pick_facade_variant_index) instead of the
# single fixed grey every building used to share -- real skylines read as
# "mostly neutral tones with a few real standouts," not a uniform wall of
# one color OR an evenly-distributed rainbow, so "weight" biases selection
# toward the neutral entries and keeps the single saturated accent color
# genuinely rare. Windows are NOT part of this palette -- every window is
# real glass geometry (see GLASS_COLOR/build_facade_material's own comment
# on why there's no separate baked-in window color anymore), independent of
# the building's own body color either way.
const FACADE_VARIANTS := [
	{"body": DEFAULT_FACADE_BODY_COLOR, "weight": 3.0}, # light grey concrete/precast
	{"body": Color(0.88, 0.87, 0.84), "weight": 3.0}, # off-white/limestone
	{"body": Color(0.56, 0.64, 0.7), "weight": 3.0}, # blue glass curtain wall
	{"body": Color(0.27, 0.28, 0.31), "weight": 2.0}, # dark charcoal/graphite
	{"body": Color(0.72, 0.6, 0.46), "weight": 1.5}, # warm tan/sandstone
	{"body": Color(0.43, 0.12, 0.13), "weight": 0.75}, # rare deep-red/maroon standout
]

# Real, breakable glass overlay -- see building_glass.gd for the actual
# shoot/melee-to-crack, walk-through-to-shatter state machine this feeds.
# Semi-transparent so the (otherwise undetailed, see _build_interior's own
# comment) building interior is genuinely visible through an intact pane,
# not just a flat colored decal. Alpha dropped further (0.4 -> 0.18) per an
# earlier follow-up ask ("more transparent, see through both sides") --
# see build_glass_material()'s own cull_mode fix for the "both sides" half
# of that: a plain StandardMaterial3D defaults to CULL_BACK (only the
# front face of this single flat quad renders at all), so from the OTHER
# side the pane wasn't just less transparent, it didn't render as glass
# at all. Darkened + alpha raised again (per a later explicit "make them a
# bit darker" ask, once every window on every wall got a real transparent
# pane) -- 0.18 alone read as too washed-out/pale once it was the ONLY
# thing standing in for glass across a whole tall facade, rather than just
# the bottom few rows.
# A real reported bug: this dark a glass over an equally dark recess (see
# WINDOW_RECESS_COLOR below) blended to near-pure-black, indistinguishable
# from shadow or a flat opaque wall from any distance -- especially once
# real glass started covering EVERY window on every wall, not just a small
# band near the ground. Given a cool blue tint (evoking glass reflecting a
# night sky)
# and a touch more alpha/presence rather than just lightened straight to
# grey, so it still reads as GLASS specifically, not a pale wall panel.
const GLASS_COLOR := Color(0.1, 0.16, 0.24, 0.42)
# A lighter, more opaque "spiderwebbed/frosted" look for the CRACKED state
# (first hit landed, not yet walked through) -- reads as damaged glass
# rather than a second flavor of pristine glass.
const GLASS_CRACKED_COLOR := Color(0.72, 0.76, 0.78, 0.55)
# Dark, OPAQUE backing quad sat a hair behind the decorative glass overlay
# (see _add_window_glass_row) -- without it, a
# transparent GLASS_COLOR pane in front of a plain body_color wall barely
# shifts that wall's own color, so from any distance the window doesn't
# read as a window at all, just a faint smudge on an otherwise flat-
# colored building (a real reported bug: "the walls are loading a flat
# solid color with no windows"). The earlier fix removed a BAKED-TEXTURE
# rectangle in this same dark tone (see build_facade_material's own
# comment) because it was computed by a totally separate grid that could
# drift out of alignment with the real glass geometry -- this backing quad
# avoids that failure mode entirely by being emitted from the EXACT same
# _cell_layout call, in the same loop, as the glass quad it sits behind,
# so the two can never disagree about where a window actually is.
# Same cool-tint reasoning as GLASS_COLOR's own comment -- was a near-
# black neutral grey, which combined with an equally dark glass pane to
# read as a solid black rectangle rather than a window.
const WINDOW_RECESS_COLOR := Color(0.14, 0.18, 0.24)

# American-city street palette: dark asphalt road, light concrete sidewalk,
# yellow center line, white crosswalk/lane paint.
const ROAD_COLOR := Color(0.1, 0.1, 0.11)
const SIDEWALK_COLOR := Color(0.62, 0.6, 0.56)
const LINE_COLOR_YELLOW := Color(0.95, 0.75, 0.1)
const LINE_COLOR_WHITE := Color(0.92, 0.92, 0.9)

# UV-tiling reference for the wall panel/frame-piece quads' own texture
# coordinates (see _add_wall_panel/_add_windowed_wall_piece) -- 3 columns x
# 2 rows (not a square 2x2 grid) so a column can be a different WORLD size
# than a floor is tall (see NOMINAL_COLUMN_METERS/NOMINAL_FLOOR_METERS
# below), matching how a real facade's window spacing and floor height
# usually aren't the same number. No longer backed by an actual raster
# texture (see build_facade_material's own comment on why the baked-in
# window rectangle was removed) -- these UVs go nowhere now, but the
# geometry that computes them is untouched, so this stays to keep that
# math meaningful rather than ripping out a working, unrelated system.
const TEX_COLS := 3
const TEX_ROWS := 2
# How much of each cell is left as the dark facade "frame" around the
# window -- smaller than the old implicit ~22% so windows read as bold,
# modern glass panels instead of small punched openings.
const WINDOW_MARGIN_RATIO := 0.14
# Real-world-ish scale: one window "bay" (a column) is NOMINAL_COLUMN_METERS
# wide, one floor (a row) is NOMINAL_FLOOR_METERS tall -- two separate
# constants, not one shared cell size, specifically so a floor can be
# taller than a bay is wide instead of coming out perfectly square.
const NOMINAL_COLUMN_METERS := 1.6
const NOMINAL_FLOOR_METERS := 3.2

const ROOF_TYPES := ["", "", "", "shed", "antenna", "chimney", "parapet", "water_tower"] # "" (flat) weighted heavier -- most rooftops shouldn't have a prop

# Building-massing shape variation -- an attached secondary "wing" volume
# (see _maybe_add_wing/_build_wing_massing) that turns a plain rectangular
# tower into an L-shaped (or similar) footprint. Exterior-only massing: its
# own windowed facade + a single solid collision box, but no interior floors
# or stairs of its own (that would mean duplicating this whole file's
# interior/stairwell pipeline a second time for what's meant to be a small
# secondary silhouette detail, not a second real building). Deliberately
# never attaches to the door wall (see _wall_defs/DOOR wall, always index 3)
# so it can never bury the building's own entrance. Colors are NOT part of
# this -- the wing always reuses the SAME facade_mat/body_color the main
# building already picked (see FACADE_VARIANTS' own comment on why no new
# palette entries were added here), it only varies shape.
const WING_CHANCE := 0.35
const WING_MIN_WIDTH_FRACTION := 0.35
const WING_MAX_WIDTH_FRACTION := 0.65
const WING_MIN_DEPTH := 3.0
const WING_MAX_DEPTH := 6.0
const WING_MIN_HEIGHT_FRACTION := 0.35
const WING_MAX_HEIGHT_FRACTION := 0.8
const WING_MARGIN := 1.0 # keeps the wing's own footprint from reaching past the main building's own far corner

# Rare, visually distinct building types (structural/visual only -- no
# interactive loot content yet, per the user's own explicit "no loot in
# these buildings yet" ask). Each entry overrides ONLY size range/roof/
# facade color/sign color for generate_chunk's own lot-building branch --
# everything else (walls, windows, door, collision, interior floors, glass)
# still comes from the SAME, already-hardened _build_building() every
# generic building uses, completely unmodified. "chance" values are each
# checked against an independent roll (see _pick_special_building_type's
# own comment on why) and don't need to sum to 1.0 -- whatever's left over
# after all four is the (overwhelming majority) chance of an ordinary
# generic building, unchanged from before this feature existed.
const SPECIAL_BUILDING_PRESETS := {
	"supermarket": {
		"chance": 0.03,
		"width_min": 15.0, "width_max": 18.0,
		"depth_min": 11.0, "depth_max": 15.0,
		"height_min": 8.0, "height_max": 11.0,
		"roof": "",
		"facade_color": Color(0.72, 0.28, 0.12), # warm red-orange "big box" storefront
		"sign_color": Color(0.95, 0.82, 0.15), # bright yellow band
	},
	"bank": {
		"chance": 0.025,
		"width_min": 11.0, "width_max": 15.0,
		"depth_min": 11.0, "depth_max": 15.0,
		"height_min": 12.0, "height_max": 22.0,
		"roof": "parapet",
		"facade_color": Color(0.55, 0.55, 0.58), # grey stone/granite
		"sign_color": Color(0.72, 0.6, 0.28), # gold plaque
	},
	"police": {
		"chance": 0.02,
		"width_min": 10.0, "width_max": 14.0,
		"depth_min": 10.0, "depth_max": 14.0,
		"height_min": 10.0, "height_max": 16.0,
		"roof": "antenna", # a radio mast reads as "public safety building" at a glance
		"facade_color": Color(0.72, 0.73, 0.75), # light institutional grey
		"sign_color": Color(0.08, 0.18, 0.45), # dark blue
	},
	"hospital": {
		"chance": 0.015,
		"width_min": 14.0, "width_max": 18.0,
		"depth_min": 12.0, "depth_max": 16.0,
		"height_min": 24.0, "height_max": 45.0, # taller than the others -- a real hospital tower stands out on a skyline
		"roof": "",
		"facade_color": Color(0.87, 0.89, 0.91), # near-white
		"sign_color": Color(0.82, 0.08, 0.1), # was the red cross's own color; now used for its plain sign like every other special building
	},
}

## Independently-seeded pick of a rare special building type (""=none, the
## existing/default generic path). Deliberately NOT drawn from the shared
## per-chunk `rng` every other per-building choice (footprint, height, roof,
## facade) already depends on in a fixed call order -- inserting this pick
## into that shared stream would shift every LATER rng.randf()/randf_range()
## call in generate_chunk by one slot, which, because generation is fully
## deterministic from chunk_seed alone, would silently reshuffle the
## width/height/roof/facade of every ALREADY-EXISTING generic building in
## the whole city the moment this feature shipped -- not just the new
## special ones. Keeping this roll on its own independent stream keeps every
## generic building's own rng call sequence byte-for-byte identical to
## before this feature existed.
static func _pick_special_building_type(special_rng: RandomNumberGenerator) -> String:
	var roll := special_rng.randf()
	var cursor := 0.0
	for type_name in SPECIAL_BUILDING_PRESETS:
		cursor += float(SPECIAL_BUILDING_PRESETS[type_name]["chance"])
		if roll < cursor:
			return type_name
	return ""

# Small protruding platforms on random upper-floor window rows (see
# _add_balconies) -- purely decorative massing, no collision (nothing in
# this codebase has small-prop collision to build on, and a balcony nobody
# can actually stand on is a reasonable, honestly-scoped placeholder rather
# than a half-built "walkable but buggy" ledge). Capped rows/chance to keep
# a tall building's own balcony node count bounded.
# --- Vertical concrete fins ("ribbed" brutalist facades) ---
# Reference-matched: the buildings the city's look was asked to move toward
# read as deep vertical concrete ribs running the full height of a facade,
# with the windows behind them mostly lost in shadow between the fins.
#
# Deliberately built as DECORATIVE geometry layered in front of the ordinary
# windowed wall (the same way _add_balconies already layers onto a finished
# building), NOT as a genuinely solid windowless wall style. Making a wall
# really windowless would mean threading a per-wall style through
# _add_building_box AND _add_building_collision AND _build_interior AND
# building_glass.gd's own setup() -- the exact wall/glass/collision
# interaction that has already shipped two separate user-visible regressions
# (see build_glass_material's own comment history and the door-wall notes).
# Layering fins in front gets the same silhouette for none of that risk, and
# a densely ribbed wall reads as concrete anyway since the fins occlude most
# of the glass behind them at street angles.
#
# No collision of their own: a fin is a thin ledge standing proud of a wall
# that is ALREADY solid behind it, so nothing can be walked/fallen through
# that wasn't already blocked -- the only cost is that the player's own
# shoulder can visually clip a fin, which is cheap next to a per-fin
# collision shape on every ribbed building in view.
const RIB_CHANCE := 0.45
# Of the buildings that DO get ribbed, how many rib every eligible wall (a
# uniformly ribbed concrete slab) vs. only some of them -- the latter is
# what produces the asked-for "mixes of the two", a building that reads as
# ribbed concrete from one side and ordinary windows from another.
const RIB_ALL_WALLS_CHANCE := 0.5
const RIB_NOMINAL_SPACING := 1.15
# How much of each spacing cell the fin itself fills -- the rest is the gap
# the window band behind stays visible through.
const RIB_WIDTH_RATIO := 0.42
const RIB_MIN_DEPTH := 0.18
const RIB_MAX_DEPTH := 0.42
# Fins start just above the ground row so they never bracket the doorway or
# the street-level glass a player can actually walk through.
const RIB_BASE_ROWS := 1

# --- Basements (see _build_basement) ---
# A rare building has one floor BELOW ground, reached by its own stairwell
# descending through a real hole cut in the chunk's ground slab.
const BASEMENT_CHANCE := 0.12
# Which corner the descending stairwell uses (see _stairwell_for_floor's own
# corner numbering). Deliberately corner 2 (front-right) while the ground
# floor's own ASCENDING flight is corner 0 (back-left) -- diagonally
# opposite, so the down-stairs and up-stairs openings can't land on top of
# each other and fight for the same patch of floor.
const BASEMENT_STAIR_CORNER := 2

# --- Skybridges (see _maybe_add_bridges) ---
# Per adjacent building PAIR, not per building -- a chunk only ever looks at
# its +X and +Z neighbour, so this is roughly the fraction of neighbouring
# tower pairs that end up linked.
const BRIDGE_CHANCE := 0.22
# Never lower than this off the ground (a bridge at 4m reads as a carport,
# not a skybridge), and never within this much of the SHORTER tower's roof,
# so the deck always lands on real wall rather than poking out at the very
# top edge.
const BRIDGE_MIN_HEIGHT := 10.0
const BRIDGE_TOP_MARGIN := 4.0
# Below these the two buildings aren't really facing each other -- a deck
# spanning almost nothing, or clipping the corner of a building that's
# mostly offset past its neighbour, both read as broken rather than wonky.
const BRIDGE_MIN_SPAN := 3.0
const BRIDGE_MIN_OVERLAP := 2.5
const BRIDGE_MAX_WIDTH := 3.2
const BRIDGE_DECK_THICKNESS := 0.4
const BRIDGE_RAIL_HEIGHT := 1.0
const BRIDGE_RAIL_THICKNESS := 0.12
# Sunk into each facade so the deck visibly meets the wall instead of
# stopping a hair short and appearing to float in the gap.
const BRIDGE_EMBED := 0.6

const BALCONY_CHANCE_PER_ROW := 0.3
const BALCONY_MAX_ROWS := 6
const BALCONY_WIDTH := 2.2
const BALCONY_DEPTH := 1.1
const BALCONY_SLAB_THICKNESS := 0.12
const BALCONY_RAIL_HEIGHT := 0.9
const BALCONY_RAIL_THICKNESS := 0.08

# Every STREET_PERIOD-th chunk row/column (in x AND in z) is kept entirely
# clear as a through-street, in both directions -- like a real Manhattan-
# style grid, not just gaps of empty space scattered wherever a building
# happened not to get placed. The chunks between two streets form one
# walkable block; see is_street_chunk.
const STREET_PERIOD := 4

const LOT_BUILDING_CHANCE := 0.9 # a lot chunk is occasionally left as a plaza/gap instead
# Gap kept between a building's own footprint and its own lot's edges (a
# stand-in for a sidewalk) -- guarantees it can never reach into the street,
# or into a neighboring lot's own building, even at max footprint size. Also
# how wide an actual street chunk's sidewalk strip is, wherever it borders
# a lot (see _build_street_ground) -- same real-world-ish scale, so the two
# line up visually across the shared chunk boundary.
const LOT_MARGIN := 3.0
const SIDEWALK_WIDTH := LOT_MARGIN
# MAX_FOOTPRINT stays deliberately close to the lot's full available width
# (chunk_size minus margin on both sides) -- a real city block's buildings
# mostly fill their lot edge-to-edge save for a sidewalk-width gap, not
# small boxes adrift in the middle of a mostly-empty lot. MIN_FOOTPRINT is
# lower than that alone would suggest (was 12.0, tight to MAX_FOOTPRINT's
# own 18.0) specifically so a real narrow/slender tower can land next to a
# wide one -- a real skyline mixes both, not just a tight band of similarly-
# proportioned boxes.
const MIN_FOOTPRINT := 9.0
const MAX_FOOTPRINT := 18.0
const MIN_HEIGHT := 8.0
# Up to 2x the previous max (48) -- a handful of real standouts towering
# over the rest of the block, not just a tight, samey height band. Reaching
# this ceiling is deliberately rare (see generate_chunk's own height
# formula, which biases toward the lower end of this range) -- a plain
# uniform spread across MIN_HEIGHT..MAX_HEIGHT would put the AVERAGE
# building right in the middle of this range (here, ~52m), which reads as
# "the whole city got taller," not "more size variation with a few real
# standouts" -- the actual, repeatedly-requested ask.
const MAX_HEIGHT := 96.0
# Small random offset from dead-center within a lot, so a full block's row
# of buildings doesn't look mechanically identical -- capped low enough
# (see generate_chunk) that it can never eat into LOT_MARGIN, so buildings
# still read as lined up along the street rather than scattered.
const MAX_JITTER := 1.5

const GROUND_THICKNESS := 1.0
# Purely visual overlays (the road surface, lane markings) sit flush on top
# of the one collidable ground slab underneath -- just enough of a gap to
# avoid z-fighting with the sidewalk slab they sit on, kept as small as
# possible since the player's own collision floor is the SIDEWALK slab
# everywhere (the road overlay has no collision of its own, see
# _add_overlay_box) -- any thicker than that and the road's own visible top
# surface sits measurably above where the player's feet actually rest,
# which reads as the road clipping up into the player model. The previous
# value (0.06, 6cm) was picked purely to dodge z-fighting without
# considering how tall it'd read against a standing character -- confirmed
# directly as visibly clipping into the player, on top of and independent
# from the crouch-clipping bug (see character.gd's own crouch-related
# fixes).
const OVERLAY_THICKNESS := 0.01
const LINE_THICKNESS := 0.02
const LINE_WIDTH := 0.15
const DOUBLE_LINE_GAP := 0.12
const CROSSWALK_DEPTH := 4.0
const CROSSWALK_STRIPE_WIDTH := 1.0
const CROSSWALK_STRIPE_GAP := 1.0

# Every building gets a walk-through entrance on its front wall, plus a
# stack of empty interior floors just inside it (see _build_interior) --
# deliberately undetailed (no NPCs, no furniture) for now, just enough that
# "enterable" and "multiple floors" are actually real rather than a
# decorative door cutout.
const DOOR_WIDTH := 1.6
# How many whole floor-rows tall the door is -- computed from each
# building's own ACTUAL floor-row height (see _build_building), not a
# fixed meters value, so the door's own top edge always lands exactly on
# the same row grid the rest of the wall uses. That's what keeps the
# windows directly above the door lined up with their neighbors instead of
# forming their own separately-gridded, visibly offset column.
const DOOR_HEIGHT_FLOORS := 1
# The lobby doesn't need anywhere near a tall building's own roof height --
# there's nothing above it yet (no stairs up), so nothing would be
# reachable there anyway.
# Computed from the SAME per-building floor-row size as the door itself
# (see DOOR_HEIGHT_FLOORS/_build_building), not a fixed meters value --
# a fixed constant here couldn't guarantee it always came out taller than
# a door whose own height varies per building (verified: a fixed 3.2m
# room against a door that could round up to ~4m tall on some buildings
# produced a negative-size, crashing collision box for the "above the
# door" interior wall strip). Twice the door's own floor count guarantees
## room_height > door_height always, by construction, regardless of how
# either one rounds for a given building.
const ROOM_HEIGHT_FLOORS := 2
# Walls, floors, and stairs used to all share this one flat color, lit only
# by whatever ambient/directional light happened to reach a given surface --
# fine for a single isolated wall, but a whole stairwell of same-colored
# surfaces read as a shapeless blend from some angles, with the stairs
# themselves nearly invisible against the wall right behind them (confirmed
# directly from a player screenshot: a stairwell interior where the flight
# was barely distinguishable from the walls around it). Real interiors
# don't do this either -- floor, wall, and stair materials almost always
# differ in real buildings, which is as much a readability cue as a
# material fact. Splitting into three distinct tones (still coordinated,
# not clashing) gives every surface a real color boundary to read against,
# independent of whatever the lighting happens to be doing at a given
# camera angle.
const INTERIOR_COLOR := Color(0.5, 0.47, 0.44)
const INTERIOR_FLOOR_COLOR := Color(0.36, 0.34, 0.32)
# Was a distinct warm wood tone (0.64, 0.52, 0.37) -- reverted to plain
# INTERIOR_COLOR for now because splitting the stair color off exposed a
# real z-fighting bug (stair geometry visibly flickering through the floor
# slab above it) that predates this color change and needs its own fix;
# matching the wall color just hides that seam instead of fixing it. Revisit
# once the actual geometry overlap is fixed.
const INTERIOR_STAIR_COLOR := INTERIOR_COLOR
# Nominal wall thickness for collision shapes only (the visual mesh models
## walls as zero-thickness shells, same as the rest of this file's boxes) --
# thin enough to be unnoticeable, thick enough to be a reliable collider.
const WALL_THICKNESS := 0.2

# Each walkable interior floor (see _build_interior) is this tall -- reuses
# the SAME per-building floor-row size (v_layout) the door/lobby height
# already used, just under its own name, since a "story" and the old
# single lobby's own height are now the same concept.
const STORY_HEIGHT_FLOORS := ROOM_HEIGHT_FLOORS
# A safety bound on node count, not a normal limit -- set at the actual
# natural maximum (a MAX_HEIGHT building divided into STORY_HEIGHT_FLOORS-
# tall stories never needs more than this many), so it only ever engages
# in a pathological case instead of routinely truncating. Truncating
# routinely was tried and reverted: capping well below the natural count
# dumps the whole truncated remainder onto the top floor alone (see
# _build_interior) as extra height, which produced a single flight tall
# enough that even a very generous floor_max_angle (see player.gd) couldn't
# make it climbable.
const MAX_INTERIOR_FLOORS := 7
# One straight flight per floor, tucked against the back wall (opposite
# the door) so it never blocks the entrance, AND against one of the two
# side walls (see _stairwell_for_floor) rather than centered -- centered
# read as an odd freestanding block in the middle of an otherwise empty
# room. Which side wall alternates every floor (left, right, left, ...) so
# climbing several floors in a row doesn't feel like the exact same room
# repeated. Run (see _build_building) reaches all the way to the room's
# own center rather than stopping a short distance off the back wall -- a
# short run read as a stubby, out-of-place little staircase rather than a
# real flight; the front half of the room, past the stairs, is left open
# floor same as before.
const STAIR_WIDTH := 1.8
const STAIR_MARGIN := 1.0
# Flush against the side wall a flight hugs, not some visible gap -- a gap
# there just read as a stray strip of unused floor between the stairs and
# the wall for no reason. NOT literally 0 though: that put the stair
# geometry's own outer edge exactly coplanar with the wall itself, and
# coincident geometry z-fights.
const STAIR_SIDE_MARGIN := 0.05
# Real stairs rise about this much per individual step -- a bit taller
# than a typical real-world 17-19cm residential riser, which (combined
# with the longer run above, so tread depth grows too) read as slightly
# too small/dainty against this game's own chunkier art style. Steps per
# flight are derived from this and each flight's own rise (see
# _add_staircase) rather than a fixed step count, which at a ~6.5m story
# rise used to come out to over a meter of riser height per step: giant-
# sized compared to the player's own ~1.8m height (see player.tscn's own
# CapsuleShape3D).
const STAIR_STEP_HEIGHT := 0.25

# One small separate room carved out of a back corner of every interior
# floor (see _room_corner_for_floor/_add_room_partition), so a floor reads
# as "multiple enterable rooms" instead of one single open box -- always on
# the BACK wall (opposite the exterior door, which only ever sits on the
# +Z wall, see _wall_defs) and always the back corner this floor's OWN
# staircase ISN'T using (see _stairwell_for_floor), so the two carve-outs
# can never overlap by construction, with no per-floor randomness needed at
# all. Fractions capped well under half of either footprint axis (see
# _add_room_partition's own hx/hz clamp) so the carved room can never cross
# the building's own centerline -- which is what guarantees it stays clear
# of a same-side-of-center stairwell too, not just a same-corner one.
const ROOM_WIDTH_FRACTION := 0.4
const ROOM_DEPTH_FRACTION := 0.42
const MIN_ROOM_SIZE := 2.4

## True for a chunk that's kept clear as a through-street rather than a
## buildable lot. posmod (not GDScript's %, which can return negative
## results for negative operands) so this repeats cleanly forever in every
## direction -- including negative coordinates -- not just near the origin.
static func is_street_chunk(coord: Vector2i) -> bool:
	return posmod(coord.x, STREET_PERIOD) == 0 or posmod(coord.y, STREET_PERIOD) == 0

## A flat, solid body_color material -- NO baked-in window rectangle
## anymore (an earlier version painted one directly into this texture, at
## its own independent TEX_COLS x TEX_ROWS tiling grid). That texture-space
## grid and the REAL window geometry (_add_windowed_wall_row's own punched
## holes for the breakable band, _add_window_glass_row's own decorative
## overlay quads above it -- both driven by _cell_layout against this
## building's own actual world-space dimensions) were always two
## INDEPENDENTLY computed grids that merely happened to line up when their
## math agreed; any drift between them (a panel boundary that isn't an
## exact multiple of a texture tile, floating-point rounding at a repeat
## seam, a UV origin that didn't match a given panel's own real column
## start) read as a real reported bug -- "black windows and transparent
## windows combined... walls overlapping over each other": some texture-
## painted rectangles landing under a real glass quad (transparent), others
## landing in the gap between quads (still flat black), with a visible
## seam wherever the two grids fell out of step. Removing the painted
## rectangle entirely leaves exactly ONE system that can ever render
## anything window-shaped -- the real glass geometry, sharing the exact
## same `glass_intact` material everywhere -- so there's no second grid
## left to ever drift out of alignment with it again.
static func build_facade_material(body_color: Color) -> StandardMaterial3D:
	var mat := StandardMaterial3D.new()
	mat.albedo_color = body_color
	mat.roughness = 1.0
	# Flat, stylized shading, not PBR -- roughness=1.0 alone only softens a
	# specular highlight into a broad glow, it doesn't remove it (Godot's
	# StandardMaterial3D still applies a default dielectric specular
	# response, metallic_specular=0.5, independent of roughness). That glow
	# was invisible against the old flat-color sky (nothing bright enough
	# to reflect), but became a real visible artifact -- a washed-out sheen
	# across every road, sidewalk, and facade -- the moment a real sky with
	# a sun was added (see city.tscn). Zeroing it out keeps every surface
	# reading as flat/matte the way the whole city's art style intends,
	# regardless of what's bright enough to reflect off it.
	mat.metallic_specular = 0.0
	return mat

static func _flat_material(color: Color, unshaded: bool) -> StandardMaterial3D:
	var mat := StandardMaterial3D.new()
	mat.albedo_color = color
	if unshaded:
		mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	else:
		mat.roughness = 0.95
		mat.metallic_specular = 0.0 # see build_facade_material's own comment
	return mat

## All the materials chunk generation needs, built once by city.gd and
## reused across every chunk rather than re-created per call -- including
## one real StandardMaterial3D (and its own baked window-grid texture) per
## FACADE_VARIANTS entry, so a building only ever picks an INDEX into this
## already-built array (see _pick_facade_variant_index) instead of building
## a fresh texture per building.
static func build_materials() -> Dictionary:
	var facade_variants: Array[StandardMaterial3D] = []
	for variant in FACADE_VARIANTS:
		facade_variants.append(build_facade_material(variant["body"]))
	return {
		"facade_variants": facade_variants,
		"road": _flat_material(ROAD_COLOR, false),
		"sidewalk": _flat_material(SIDEWALK_COLOR, false),
		"line_yellow": _flat_material(LINE_COLOR_YELLOW, true),
		"line_white": _flat_material(LINE_COLOR_WHITE, true),
		"interior": _flat_material(INTERIOR_COLOR, false),
		"interior_floor": _flat_material(INTERIOR_FLOOR_COLOR, false),
		"interior_stair": _flat_material(INTERIOR_STAIR_COLOR, false),
		# emission_energy_multiplier raised again (0.55 -> 2.5) -- a real
		# follow-up report: windows were STILL reading as flat black on
		# various walls even with the earlier 0.55 glow. GLASS_COLOR itself
		# (0.1, 0.16, 0.24) is deliberately dark (see its own comment on why
		# -- an explicit "make them a bit darker" ask), so at 0.55x that only
		# emits roughly (0.06, 0.09, 0.13) -- too dim to read as "lit glass"
		# against typical daylight ambient once alpha-blended over the
		# recess/interior behind it. Deliberately only touching the emission
		# multiplier here, not GLASS_COLOR/alpha themselves -- those are an
		# orthogonal knob (plain retinting already failed twice before the
		# emission approach was even introduced, see build_glass_material's
		# own comment), and boosting emission alone keeps the same dark,
		# moody base tint intact while just making the glow impossible to
		# miss.
		"glass_intact": build_glass_material(GLASS_COLOR, 0.25, 2.5),
		"glass_cracked": build_glass_material(GLASS_CRACKED_COLOR, 0.6),
		"window_recess": _flat_material(WINDOW_RECESS_COLOR, false),
	}

## Shared builder for both the intact and cracked glass looks -- just a
## transparent StandardMaterial3D, real glass shine (low roughness) for the
## intact pane, duller/frosted (higher roughness) for the cracked one.
## emission_energy (0.0 by default, i.e. no emission at all -- the cracked
## look stays plain) is a real reported bug's own fix: a purely-albedo dark
## glass tint blended into near-indistinguishability from an equally dark
## wall body color at night (color alone can only ever buy so much contrast
## once ambient light is this low) -- confirmed as still visible after two
## earlier attempts at just retinting GLASS_COLOR/WINDOW_RECESS_COLOR.
## Emitting the glass's OWN albedo color (not a separate warm "lit from
## inside" color, see title_screen_city.gd's own WINDOW_LIGHT_COLOR for
## that different, deliberately-nocturnal treatment) gives every intact
## window a real baseline glow that reads as glass regardless of the wall
## color behind it or the time of day, without implying every building is
## uniformly interior-lit the way the title screen's own skyline backdrop
## intentionally is.
static func build_glass_material(color: Color, roughness: float, emission_energy: float = 0.0) -> StandardMaterial3D:
	var mat := StandardMaterial3D.new()
	mat.albedo_color = color
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.roughness = roughness
	if emission_energy > 0.0:
		mat.emission_enabled = true
		mat.emission = color
		mat.emission_energy_multiplier = emission_energy
	# A real sheen, unlike _flat_material's own deliberately-zeroed specular
	# for matte surfaces -- this one SHOULD read as glass. Dropped from an
	# original 0.5 alongside GLASS_COLOR's own roughness bump (0.05 -> 0.25,
	# see "glass_intact" in build_materials) -- a real reported bug: with
	# roughness this low, the sun's own DirectionalLight3D specular lobe on
	# a big flat window quad is tiny and razor-sharp, so instead of a broad
	# believable sheen it rendered as a small, hard-edged, unnaturally
	# perfect bright white disc sitting on the glass ("looks like a white
	# circle"), and now that every window on every building is real glass
	# (see build_facade_material's own comment), that artifact was visible
	# everywhere at once instead of being partly hidden behind the old
	# opaque black windows. Softening both together spreads that highlight
	# into a broader, fainter gleam instead of a sharp circular hotspot,
	# while still reading as glass rather than a flat matte panel.
	mat.metallic_specular = 0.3
	# A window pane is a single flat quad (see building_glass.gd's own
	# _build_row_visual) -- StandardMaterial3D defaults to CULL_BACK (only
	# the front face renders), so without this a real reported bug follows
	# directly: the glass is only ever visible from whichever single side
	# its normal happens to face, not "see-through from both sides" the way
	# a real window is.
	mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	return mat

## Weighted-random pick of an index into FACADE_VARIANTS -- draws from the
## SAME seeded per-chunk rng every other per-building choice (footprint,
## height, roof) already uses, so a building's facade color is just as
## deterministic/reproducible per chunk coordinate as everything else about
## it (see generate_chunk's own class comment).
static func _pick_facade_variant_index(rng: RandomNumberGenerator) -> int:
	var total_weight := 0.0
	for variant in FACADE_VARIANTS:
		total_weight += float(variant["weight"])
	var roll := rng.randf() * total_weight
	var cursor := 0.0
	for i in range(FACADE_VARIANTS.size()):
		cursor += float(FACADE_VARIANTS[i]["weight"])
		if roll < cursor:
			return i
	return FACADE_VARIANTS.size() - 1 # float rounding safety net, not a normal path

## Builds one chunk's contents (ground, plus either street furniture or a
## building) as a fresh root Node3D, positioned by the caller at the
## chunk's world center. Fully deterministic from `chunk_seed` alone -- no
## two calls with the same seed ever produce different results, which is
## what lets a chunk be freed and later regenerated (or independently
## generated by another multiplayer peer) identically. `coord` must be the
## same chunk coordinate `chunk_seed` was derived from -- it's only used to
## check is_street_chunk (and its neighbors), not to perturb the RNG itself.
## Everything about a chunk's building that is decided purely by RNG rolls,
## split out from generate_chunk so a chunk can work out what its NEIGHBOUR
## built without that neighbour existing (or ever being generated) -- which
## is what makes the skybridges below possible at all: chunks generate
## independently, so both ends of a bridge have to agree on it from their
## own side, with no shared state to consult.
##
## CONSUMES `rng` in exactly the order generate_chunk used to inline, and
## generate_chunk now calls THIS rather than keeping its own copy of those
## rolls -- one source of truth on purpose. Two copies of this sequence that
## drifted apart would silently mean a bridge computed against a phantom
## building that doesn't match the real one next door, which is far harder
## to notice than a crash.
static func building_params(rng: RandomNumberGenerator, chunk_seed: int, coord: Vector2i, chunk_size: float) -> Dictionary:
	# Street chunks consume NO rolls at all (they never reach the building
	# branch) -- mirrored exactly here, or every seeded sequence downstream
	# of a street chunk would shift.
	if is_street_chunk(coord):
		return {"has_building": false}
	if rng.randf() >= LOT_BUILDING_CHANCE:
		return {"has_building": false}

	var max_footprint := minf(MAX_FOOTPRINT, chunk_size - LOT_MARGIN * 2.0)

	# See _pick_special_building_type's own comment for why this uses a
	# separate, independently-salted RNG rather than `rng` itself -- keeps
	# every generic building's own call sequence below untouched regardless
	# of whether this lot rolls special.
	var special_rng := RandomNumberGenerator.new()
	special_rng.seed = chunk_seed ^ 0x53504543
	var special_type := _pick_special_building_type(special_rng)
	var special_preset: Dictionary = SPECIAL_BUILDING_PRESETS.get(special_type, {})

	var width: float
	var depth: float
	var height: float
	var roof: String
	var facade_index := 0

	if not special_preset.is_empty():
		width = clampf(rng.randf_range(float(special_preset["width_min"]), float(special_preset["width_max"])), MIN_FOOTPRINT, max_footprint)
		depth = clampf(rng.randf_range(float(special_preset["depth_min"]), float(special_preset["depth_max"])), MIN_FOOTPRINT, max_footprint)
		height = rng.randf_range(float(special_preset["height_min"]), float(special_preset["height_max"]))
		roof = special_preset["roof"]
	else:
		width = rng.randf_range(MIN_FOOTPRINT, max_footprint)
		depth = rng.randf_range(MIN_FOOTPRINT, max_footprint)
		# Squaring a uniform [0,1] sample biases it toward 0 -- most
		# buildings land in the lower half of MIN_HEIGHT..MAX_HEIGHT
		# (median ~30m, close to the old plain-uniform range's own feel)
		# while still leaving real headroom for a rarer few to reach all
		# the way up near MAX_HEIGHT. A plain rng.randf_range(MIN_HEIGHT,
		# MAX_HEIGHT) (this line's previous form) is a FLAT distribution
		# -- half of all buildings would land above the midpoint, which
		# reads as "the whole skyline got taller," not "a few real
		# standouts among mostly similar buildings," the actual effect
		# being asked for here.
		height = MIN_HEIGHT + pow(rng.randf(), 2.0) * (MAX_HEIGHT - MIN_HEIGHT)
		roof = ROOF_TYPES[rng.randi() % ROOF_TYPES.size()]
		facade_index = _pick_facade_variant_index(rng)

	# Kept close to the lot's center (only a small jitter, individually
	# capped per axis so it can never eat into LOT_MARGIN even at max
	# footprint) rather than placed anywhere in the lot -- that's what makes
	# a whole block's buildings read as lined up along the street instead of
	# scattered at random depths within their lots.
	var slack_x: float = maxf(0.0, (chunk_size / 2.0 - LOT_MARGIN) - width / 2.0)
	var slack_z: float = maxf(0.0, (chunk_size / 2.0 - LOT_MARGIN) - depth / 2.0)
	var jitter_x := rng.randf_range(-minf(MAX_JITTER, slack_x), minf(MAX_JITTER, slack_x))
	var jitter_z := rng.randf_range(-minf(MAX_JITTER, slack_z), minf(MAX_JITTER, slack_z))

	# Its own salted RNG rather than another `rng` roll, for the same reason
	# _pick_special_building_type keeps one: adding a draw to the shared
	# sequence would reshuffle every building's own size/height/jitter city-
	# wide, so a rare optional extra gets its own independent stream instead.
	var basement_rng := RandomNumberGenerator.new()
	basement_rng.seed = chunk_seed ^ 0x42415345
	var has_basement: bool = basement_rng.randf() < BASEMENT_CHANCE

	return {
		"has_building": true,
		"size": Vector3(width, height, depth),
		"offset": Vector3(jitter_x, 0.0, jitter_z),
		"roof": roof,
		"facade_index": facade_index,
		"special_type": special_type,
		"special_preset": special_preset,
		"has_basement": has_basement,
	}

## Replays building_params for an arbitrary coord from its seed alone -- how
## a chunk inspects its neighbour's building without generating it.
static func building_params_for_seed(chunk_seed: int, coord: Vector2i, chunk_size: float) -> Dictionary:
	var rng := RandomNumberGenerator.new()
	rng.seed = chunk_seed
	return building_params(rng, chunk_seed, coord, chunk_size)

## neighbour_seed_fn: Callable(Vector2i) -> int, supplied by city.gd (which
## owns the world seed) so this file can derive a neighbour's building for
## the skybridges. Omitted by callers that don't care (tests, and anything
## generating a chunk in isolation) -- bridges simply don't appear then,
## which is correct: with no way to know what's next door there's nothing to
## bridge TO.
static func generate_chunk(chunk_seed: int, coord: Vector2i, chunk_size: float, materials: Dictionary, neighbour_seed_fn: Callable = Callable()) -> Node3D:
	var rng := RandomNumberGenerator.new()
	rng.seed = chunk_seed

	var root := Node3D.new()

	if is_street_chunk(coord):
		_build_street_ground(root, coord, chunk_size, materials)
	else:
		# Params are resolved BEFORE the ground slab now (it consumes no
		# rolls of its own, so the order is free) because a basement needs a
		# real hole cut through that slab for its stairs to pass through --
		# and the slab can't know where to leave one without knowing where
		# the building, and therefore its stairwell, ended up.
		var params := building_params(rng, chunk_seed, coord, chunk_size)
		var ground_hole := {}
		if params["has_building"] and params["has_basement"]:
			var b_size: Vector3 = params["size"]
			var b_off: Vector3 = params["offset"]
			var b_run: float = minf(b_size.x / 2.0, b_size.z / 2.0) - STAIR_MARGIN
			var well := _stairwell_for_floor(BASEMENT_STAIR_CORNER, b_size.x / 2.0, b_size.z / 2.0, b_run)
			ground_hole = {
				"x0": b_off.x + well["hole_x0"], "x1": b_off.x + well["hole_x1"],
				"z0": b_off.z + well["hole_z0"], "z1": b_off.z + well["hole_z1"],
			}
		_add_ground_body(root, chunk_size, materials["sidewalk"], ground_hole)

		if params["has_building"]:
			var special_type: String = params["special_type"]
			var special_preset: Dictionary = params["special_preset"]
			var building_size: Vector3 = params["size"]
			var building_offset: Vector3 = params["offset"]
			var roof: String = params["roof"]

			var facade_mat: Material
			var facade_body_color: Color
			if not special_preset.is_empty():
				facade_body_color = special_preset["facade_color"]
				facade_mat = build_facade_material(facade_body_color)
			else:
				var facade_index: int = params["facade_index"]
				facade_mat = materials["facade_variants"][facade_index]
				facade_body_color = FACADE_VARIANTS[facade_index]["body"]

			_build_building(root, building_size, building_offset, roof, materials, facade_mat, facade_body_color, rng, params["has_basement"])
			_maybe_add_bridges(root, coord, chunk_size, params, neighbour_seed_fn, facade_mat)

			if not special_preset.is_empty():
				# Hospitals used to be special-cased into their own red cross
				# mesh here -- removed on request; they now take the same
				# plain sign every other special building gets, in their own
				# preset's sign_color.
				_add_building_sign(root, building_size, building_offset, special_preset["sign_color"])
			else:
				_maybe_add_wing(root, building_size, building_offset, rng, materials, facade_mat)

	return root

## Skybridges spanning between this chunk's building and its +X / +Z
## neighbour's -- the "random wonky bridges between buildings" the city's
## look was asked for.
##
## OWNERSHIP: each bridge is built by the LOWER-coord chunk of the pair
## only (this chunk always looks +X and +Z, never -X/-Z), so a bridge is
## emitted exactly once even though both chunks generate independently and
## neither can see the other's nodes. The neighbour's building is derived,
## not observed -- see building_params_for_seed.
##
## The pair RNG is seeded off BOTH coords rather than either chunk's own
## seed, so it can't disturb the per-chunk sequences (the same reason
## _pick_special_building_type keeps its own salted RNG) and so both sides
## of the pair would compute an identical answer if they ever both asked.
static func _maybe_add_bridges(root: Node3D, coord: Vector2i, chunk_size: float, params: Dictionary, neighbour_seed_fn: Callable, facade_mat: Material) -> void:
	if not neighbour_seed_fn.is_valid():
		return
	for axis_x in [true, false]:
		var other_coord: Vector2i = coord + (Vector2i(1, 0) if axis_x else Vector2i(0, 1))
		var other_params: Dictionary = building_params_for_seed(int(neighbour_seed_fn.call(other_coord)), other_coord, chunk_size)
		if not other_params["has_building"]:
			continue

		var pair_rng := RandomNumberGenerator.new()
		pair_rng.seed = hash(Vector4i(coord.x, coord.y, other_coord.x, other_coord.y)) ^ 0x42524447
		if pair_rng.randf() >= BRIDGE_CHANCE:
			continue

		var a_size: Vector3 = params["size"]
		var a_off: Vector3 = params["offset"]
		var b_size: Vector3 = other_params["size"]
		# The neighbour's own offset is in ITS chunk's local space; only the
		# bridged axis differs between the two chunk origins, so shifting by
		# one chunk_size along that axis puts it in ours.
		var b_off: Vector3 = other_params["offset"] + (Vector3(chunk_size, 0, 0) if axis_x else Vector3(0, 0, chunk_size))

		# Deck height has to exist on BOTH towers, and high enough off the
		# ground to read as a skybridge rather than a carport.
		var max_deck: float = minf(a_size.y, b_size.y) - BRIDGE_TOP_MARGIN
		if max_deck < BRIDGE_MIN_HEIGHT:
			continue
		var deck_y: float = pair_rng.randf_range(BRIDGE_MIN_HEIGHT, max_deck)

		# Along the shared axis: gap between the two facing walls.
		var span0: float
		var span1: float
		# Across it: the stretch of wall the two buildings actually share.
		var cross0: float
		var cross1: float
		if axis_x:
			span0 = a_off.x + a_size.x / 2.0
			span1 = b_off.x - b_size.x / 2.0
			cross0 = maxf(a_off.z - a_size.z / 2.0, b_off.z - b_size.z / 2.0)
			cross1 = minf(a_off.z + a_size.z / 2.0, b_off.z + b_size.z / 2.0)
		else:
			span0 = a_off.z + a_size.z / 2.0
			span1 = b_off.z - b_size.z / 2.0
			cross0 = maxf(a_off.x - a_size.x / 2.0, b_off.x - b_size.x / 2.0)
			cross1 = minf(a_off.x + a_size.x / 2.0, b_off.x + b_size.x / 2.0)

		if span1 - span0 < BRIDGE_MIN_SPAN or cross1 - cross0 < BRIDGE_MIN_OVERLAP:
			continue # buildings don't face each other squarely enough to link
		var width: float = minf(cross1 - cross0, BRIDGE_MAX_WIDTH)
		var cross_center: float = (cross0 + cross1) / 2.0
		# Overlaps into each facade slightly so the deck visibly meets the
		# wall instead of stopping a hair short of it and floating.
		var length: float = (span1 - span0) + BRIDGE_EMBED * 2.0
		var span_center: float = (span0 + span1) / 2.0

		var deck_size: Vector3 = Vector3(length, BRIDGE_DECK_THICKNESS, width) if axis_x else Vector3(width, BRIDGE_DECK_THICKNESS, length)
		var center: Vector3 = Vector3(span_center, deck_y, cross_center) if axis_x else Vector3(cross_center, deck_y, span_center)

		var body := StaticBody3D.new()
		body.collision_layer = 1
		root.add_child(body)
		_add_bridge_piece(body, deck_size, center, facade_mat)

		# Railings down both long edges -- the references' walkways all have
		# them, and they also stop a player who walks out onto a bridge from
		# simply strolling off the side.
		var rail_offset: float = (width - BRIDGE_RAIL_THICKNESS) / 2.0
		var rail_y: float = deck_y + BRIDGE_DECK_THICKNESS / 2.0 + BRIDGE_RAIL_HEIGHT / 2.0
		for side in [-1.0, 1.0]:
			var rail_size: Vector3 = Vector3(length, BRIDGE_RAIL_HEIGHT, BRIDGE_RAIL_THICKNESS) if axis_x else Vector3(BRIDGE_RAIL_THICKNESS, BRIDGE_RAIL_HEIGHT, length)
			var rail_center: Vector3 = center
			rail_center.y = rail_y
			if axis_x:
				rail_center.z += side * rail_offset
			else:
				rail_center.x += side * rail_offset
			_add_bridge_piece(body, rail_size, rail_center, facade_mat)

## One solid box of a bridge (deck or railing) -- visual mesh plus matching
## collision, since these are meant to be walked across, not just looked at.
static func _add_bridge_piece(body: StaticBody3D, size: Vector3, center: Vector3, mat: Material) -> void:
	var mesh_inst := MeshInstance3D.new()
	var bm := BoxMesh.new()
	bm.size = size
	mesh_inst.mesh = bm
	mesh_inst.material_override = mat
	mesh_inst.position = center
	body.add_child(mesh_inst)

	var collision := CollisionShape3D.new()
	var box_shape := BoxShape3D.new()
	box_shape.size = size
	collision.shape = box_shape
	collision.position = center
	body.add_child(collision)

## The single collidable slab every chunk stands on, always sidewalk-
## colored -- a street chunk's road surface (see _build_street_ground) is
## just a visual-only overlay flush on top of this, inset whichever edges
## border a lot so the sidewalk color shows through there, matching the
## sidewalk that same edge's neighboring lot chunk stands on.
## hole (optional, chunk-local x0/x1/z0/z1): a rectangle to leave OPEN
## through the slab, so a basement's stairwell has somewhere to descend --
## see _build_basement. Given one, the slab is built as a 4-piece frame
## around that rectangle instead of a single box; only the rare basement
## buildings pay for the extra pieces, every other chunk stays one box.
static func _add_ground_body(parent: Node3D, chunk_size: float, mat: Material, hole: Dictionary = {}) -> void:
	var body := StaticBody3D.new()
	body.collision_layer = 1
	parent.add_child(body)

	var half := chunk_size / 2.0
	var y := -GROUND_THICKNESS / 2.0
	if hole.is_empty():
		_add_ground_slab_piece(body, Vector3(chunk_size, GROUND_THICKNESS, chunk_size), Vector3(0, y, 0), mat)
		return

	# Frame pieces: two full-width bands beyond the hole on Z, then two
	# shorter bands filling the remaining X either side of it -- the same
	# 4-piece decomposition _add_floor_with_hole already uses for interior
	# slabs, just as solid boxes since this slab has real thickness.
	var x0: float = clampf(hole["x0"], -half, half)
	var x1: float = clampf(hole["x1"], -half, half)
	var z0: float = clampf(hole["z0"], -half, half)
	var z1: float = clampf(hole["z1"], -half, half)

	if z0 > -half:
		_add_ground_slab_piece(body, Vector3(chunk_size, GROUND_THICKNESS, z0 + half), Vector3(0, y, (-half + z0) / 2.0), mat)
	if z1 < half:
		_add_ground_slab_piece(body, Vector3(chunk_size, GROUND_THICKNESS, half - z1), Vector3(0, y, (z1 + half) / 2.0), mat)
	if x0 > -half:
		_add_ground_slab_piece(body, Vector3(x0 + half, GROUND_THICKNESS, z1 - z0), Vector3((-half + x0) / 2.0, y, (z0 + z1) / 2.0), mat)
	if x1 < half:
		_add_ground_slab_piece(body, Vector3(half - x1, GROUND_THICKNESS, z1 - z0), Vector3((x1 + half) / 2.0, y, (z0 + z1) / 2.0), mat)

static func _add_ground_slab_piece(body: StaticBody3D, size: Vector3, center: Vector3, mat: Material) -> void:
	if size.x <= 0.0 or size.z <= 0.0:
		return
	var mesh_inst := MeshInstance3D.new()
	var bm := BoxMesh.new()
	bm.size = size
	mesh_inst.mesh = bm
	mesh_inst.material_override = mat
	mesh_inst.position = center
	body.add_child(mesh_inst)

	var collision := CollisionShape3D.new()
	var box_shape := BoxShape3D.new()
	box_shape.size = size
	collision.shape = box_shape
	collision.position = center
	body.add_child(collision)

## A purely visual, non-colliding box laid flush on top of whatever's
## beneath it -- used for the road surface and every lane/crosswalk
## marking. No collision of its own: it's flush with the walkable ground
## slab underneath, just a different color (or a further layer) on top.
static func _add_overlay_box(parent: Node3D, size: Vector3, local_pos: Vector3, mat: Material) -> void:
	var mesh_inst := MeshInstance3D.new()
	var bm := BoxMesh.new()
	bm.size = size
	mesh_inst.mesh = bm
	mesh_inst.material_override = mat
	mesh_inst.position = local_pos
	parent.add_child(mesh_inst)

## A street chunk's ground: the collidable sidewalk-colored slab (see
## _add_ground_body), a darker road surface on top -- inset on whichever
## edges border a lot (leaving the sidewalk showing there to meet that
## lot's own sidewalk-colored ground) but running edge-to-edge wherever it
## borders more street, so the road continues seamlessly through
## intersections instead of pinching in at every chunk seam -- and lane
## markings on top of that: a double-yellow center line along a straight
## segment's own direction of travel, or crosswalk stripes on all 4 sides
## of a full intersection.
static func _build_street_ground(parent: Node3D, coord: Vector2i, chunk_size: float, materials: Dictionary) -> void:
	_add_ground_body(parent, chunk_size, materials["sidewalk"])

	var half := chunk_size / 2.0
	var is_intersection := posmod(coord.x, STREET_PERIOD) == 0 and posmod(coord.y, STREET_PERIOD) == 0

	if is_intersection:
		# A real intersection is a "plus" of road at the SAME width as the
		# streets crossing through it here, not a solid paved square -- the
		# 4 corner quadrants between the two arms are sidewalk, matching
		# the sidewalk corners every one of its 4 (always non-lot, see
		# is_street_chunk) neighbors already carries. Paving the whole
		# square left road-colored notches poking into what should have
		# been a clean sidewalk corner, and let the two crosswalks' stripes
		# overlap out past the actual road into those same corners.
		var road_half_width := half - SIDEWALK_WIDTH
		var road_width := road_half_width * 2.0
		_add_overlay_box(parent, Vector3(chunk_size, OVERLAY_THICKNESS, road_width), Vector3(0, OVERLAY_THICKNESS / 2.0, 0), materials["road"])
		_add_overlay_box(parent, Vector3(road_width, OVERLAY_THICKNESS, chunk_size), Vector3(0, OVERLAY_THICKNESS / 2.0, 0), materials["road"])
		_add_crosswalks(parent, chunk_size, road_width, materials["line_white"])
	else:
		var west_is_lot := not is_street_chunk(coord + Vector2i(-1, 0))
		var east_is_lot := not is_street_chunk(coord + Vector2i(1, 0))
		var north_is_lot := not is_street_chunk(coord + Vector2i(0, -1))
		var south_is_lot := not is_street_chunk(coord + Vector2i(0, 1))

		var x0 := -half + (SIDEWALK_WIDTH if west_is_lot else 0.0)
		var x1 := half - (SIDEWALK_WIDTH if east_is_lot else 0.0)
		var z0 := -half + (SIDEWALK_WIDTH if north_is_lot else 0.0)
		var z1 := half - (SIDEWALK_WIDTH if south_is_lot else 0.0)
		var road_size := Vector3(x1 - x0, OVERLAY_THICKNESS, z1 - z0)
		var road_center := Vector3((x0 + x1) / 2.0, OVERLAY_THICKNESS / 2.0, (z0 + z1) / 2.0)
		_add_overlay_box(parent, road_size, road_center, materials["road"])

		# Exactly one of the two axes is this street's own running
		# direction -- the other is what picked it out as a street chunk
		# in the first place (see is_street_chunk).
		var runs_along_x := posmod(coord.y, STREET_PERIOD) == 0
		_add_center_line(parent, road_size.x, road_size.z, runs_along_x, materials["line_yellow"])

## Double solid yellow line down the middle of a straight street segment,
## running the full length of its own road surface in whichever direction
## the road actually runs.
static func _add_center_line(parent: Node3D, road_x: float, road_z: float, runs_along_x: bool, mat: Material) -> void:
	var length: float = road_x if runs_along_x else road_z
	var y := OVERLAY_THICKNESS + LINE_THICKNESS / 2.0
	for line_sign in [-1.0, 1.0]:
		var offset: float = line_sign * (LINE_WIDTH + DOUBLE_LINE_GAP) / 2.0
		if runs_along_x:
			_add_overlay_box(parent, Vector3(length, LINE_THICKNESS, LINE_WIDTH), Vector3(0, y, offset), mat)
		else:
			_add_overlay_box(parent, Vector3(LINE_WIDTH, LINE_THICKNESS, length), Vector3(offset, y, 0), mat)

## Zebra-stripe crosswalks on all 4 sides of an intersection, each stripe
## running parallel to the road direction that particular side crosses
## (matching how a real crosswalk's bars line up with the traffic they let
## pedestrians cross, not with the pedestrians' own walking direction).
## Each band's own stripe repeat is kept confined to the area NOT already
## claimed by the PERPENDICULAR bands' own depth zones (chunk_size/2 -
## CROSSWALK_DEPTH from center, on the axis those bands occupy) -- using
## the full road_width for every band left each band's outermost stripe
## reaching into the corner where the perpendicular band's stripes already
## are, visibly overlapping there.
static func _add_crosswalks(parent: Node3D, chunk_size: float, road_width: float, mat: Material) -> void:
	var half := chunk_size / 2.0
	var safe_width := minf(road_width, chunk_size - 2.0 * CROSSWALK_DEPTH)
	_add_crosswalk_band(parent, Vector3(0, 0, -half + CROSSWALK_DEPTH / 2.0), safe_width, false, mat) # north
	_add_crosswalk_band(parent, Vector3(0, 0, half - CROSSWALK_DEPTH / 2.0), safe_width, false, mat) # south
	_add_crosswalk_band(parent, Vector3(-half + CROSSWALK_DEPTH / 2.0, 0, 0), safe_width, true, mat) # west
	_add_crosswalk_band(parent, Vector3(half - CROSSWALK_DEPTH / 2.0, 0, 0), safe_width, true, mat) # east

## One crosswalk's worth of parallel stripes centered at `band_center`.
## `long_axis_x` picks which axis the individual stripe bars run along
## (true = bars run along X, repeated across Z; false = the reverse) --
## the north/south crosswalks (for crossing the street that continues
## north-south) need bars running along Z, repeated across X, and vice
## versa for east/west. Each bar is only CROSSWALK_DEPTH long (how far the
## painted zone extends along the road's own direction of travel); the
## repeats span `crossing_width` (the road's own width), not each
## individual bar's own length.
static func _add_crosswalk_band(parent: Node3D, band_center: Vector3, crossing_width: float, long_axis_x: bool, mat: Material) -> void:
	var pitch := CROSSWALK_STRIPE_WIDTH + CROSSWALK_STRIPE_GAP
	var count := int(crossing_width / pitch)
	var start := -crossing_width / 2.0 + pitch / 2.0
	var y := OVERLAY_THICKNESS + LINE_THICKNESS / 2.0
	for i in range(count):
		var d := start + i * pitch
		if long_axis_x:
			_add_overlay_box(parent, Vector3(CROSSWALK_DEPTH, LINE_THICKNESS, CROSSWALK_STRIPE_WIDTH), band_center + Vector3(0, y, d), mat)
		else:
			_add_overlay_box(parent, Vector3(CROSSWALK_STRIPE_WIDTH, LINE_THICKNESS, CROSSWALK_DEPTH), band_center + Vector3(d, y, 0), mat)

## Builds one lot's building: the exterior shell (walls window-tiled, with
## a walk-through doorway cut into the front, see _add_building_box),
## matching wall-by-wall collision (a single solid BoxShape3D would ignore
## the visual door hole and block the player from ever walking through it,
## so collision is built per-wall instead, see _add_building_collision), a
## roof prop, and a stack of empty interior floors just inside the door,
## connected by staircases up to a roof hatch (see _build_interior).
##
## facade_mat/body_color: this building's own already-picked FACADE_VARIANTS
## entry (see generate_chunk's own _pick_facade_variant_index) -- optional
## (default null/DEFAULT_FACADE_BODY_COLOR) so tests/test_multifloor_check.gd's
## own direct call, which only cares about floor/interior geometry and never
## picked a variant, still works unchanged. body_color travels separately
## from facade_mat (rather than reading it back off the material) so
## _add_roof_detail's small roof props (e.g. the "shed" box) can match this
## SAME building's own body color without needing to inspect a
## StandardMaterial3D's own albedo_texture to recover it.
## rng: this building's own already-seeded rng (see generate_chunk), used
## ONLY for the purely-decorative balcony placement (see _add_balconies) --
## optional/null so tests/test_multifloor_check.gd's own direct call, which
## never picked an rng, still works unchanged (balconies just don't get
## added for that call, which only cares about floor/interior geometry).
static func _build_building(parent: Node3D, size: Vector3, offset: Vector3, roof: String, materials: Dictionary, facade_mat: Material = null, body_color: Color = DEFAULT_FACADE_BODY_COLOR, rng: RandomNumberGenerator = null, has_basement: bool = false) -> void:
	# Computed once here (not per-panel -- see _add_wall_panel) so every
	# panel on every wall shares the exact same floor-row grid, and the
	# door's own height is snapped to a whole number of those rows so its
	# top edge (where the "above the door" panel starts) lands exactly on
	# one of that shared grid's own lines.
	var v_layout := _cell_layout(size.y, NOMINAL_FLOOR_METERS)
	var door := {"u0": -DOOR_WIDTH / 2.0, "u1": DOOR_WIDTH / 2.0, "height": float(v_layout["size"]) * DOOR_HEIGHT_FLOORS}

	var story_height: float = float(v_layout["size"]) * STORY_HEIGHT_FLOORS
	var num_floors: int = clampi(floori(size.y / story_height), 1, MAX_INTERIOR_FLOORS)

	# EVERY window row, on EVERY wall (door wall included -- see
	# _add_windowed_wall_row_excluding), gets real geometric holes filled by
	# building_glass.gd's own real, breakable pane -- not just a band synced
	# to real interior floor count. A real reported bug with the earlier
	# "decorative overlay in front of a still-solid wall" treatment used
	# above that band: glass sitting in front of an opaque wall has nothing
	# to let light/view THROUGH, so no amount of material tuning ever made
	# it read as genuinely transparent -- it was always going to look flatly
	# solid/dark, same as the wall behind it, which is exactly what got
	# reported ("still solid windows... make them all the same level of
	# transparency"). Above num_floors' own real decorated interior, a
	# broken window now opens onto the building's hollow upper shell rather
	# than a furnished room -- less polished, but genuinely see-through and
	# shatterable, which is what was actually asked for; unlike a partial
	# fix, this can't recreate the same complaint one tier higher up.
	var glass_rows: int = int(v_layout["count"])
	var glass_band_height: float = size.y

	# Run (how far a flight extends from its own wall) is shared by every
	# floor's own stairwell (see _stairwell_for_floor) -- but WHICH wall a
	# given floor's flight is against now rotates every floor (also see
	# _stairwell_for_floor), so unlike when it was always the back wall,
	# this needs to fit the room along EITHER axis, not just Z -- derived
	# from the SMALLER of the two half-footprints so it's never long enough
	# to poke through the far wall on the tighter axis. Only the top floor's
	# own final orientation (whichever that happens to land on) is what the
	# roof's own hole needs to match.
	var hx := size.x / 2.0
	var hz := size.z / 2.0
	var stair_run: float = minf(hx, hz) - STAIR_MARGIN
	var top_stairwell := _stairwell_for_floor(num_floors - 1, hx, hz, stair_run)

	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	_add_building_box(st, size, door, v_layout, top_stairwell, glass_band_height)

	# Ground-relative (y from 0 at the floor to size.y at the roof), not
	# centered on the building's own half-height -- lines up directly with
	# _build_interior's own coordinate space, and keeps the door's own
	# "0 to door height" span the same regardless of how tall the building
	# itself is.
	var body := StaticBody3D.new()
	body.position = Vector3(offset.x, 0.0, offset.z)
	body.collision_layer = 1
	parent.add_child(body)

	var mesh_inst := MeshInstance3D.new()
	mesh_inst.mesh = st.commit()
	mesh_inst.material_override = facade_mat if facade_mat != null else materials["facade_variants"][0]
	body.add_child(mesh_inst)

	_add_building_collision(body, size, door, top_stairwell, glass_band_height, v_layout)
	_add_roof_detail(body, roof, size, body_color)
	if rng != null:
		_add_balconies(body, size, rng, v_layout, body_color)
		_maybe_add_facade_ribs(body, size, rng, v_layout, mesh_inst.material_override, door)
	_build_interior(parent, offset, size, door, v_layout, num_floors, story_height, stair_run, materials["interior"], materials["interior_floor"], materials["interior_stair"], glass_band_height, has_basement)

	# Attached via set_script (not a .tscn) directly onto the exterior
	# StaticBody3D that's already been fully built above -- see
	# building_glass.gd's own class comment for why this is safe (no
	# _ready() reliance, everything driven by the explicit setup() call
	# here). Every wall, door wall included, gets real glass now (see
	# building_glass.gd's own setup() for how it excludes the door's own
	# rectangle from its ground row).
	if glass_rows > 0:
		body.set_script(BuildingGlassScript)
		body.setup(_wall_defs(hx, hz), float(v_layout["size"]), glass_rows, materials["glass_intact"], materials["glass_cracked"], door)

	# A real, openable door filling the entranceway the walls/collision above
	# already left open -- see building_door.gd's own class comment. A new
	# top-level child of `parent` (the chunk root), same as _maybe_add_wing's
	# own placement, NOT a child of `body`: keeps it clear of the exterior
	# shell's own child-index-sensitive structure (see
	# tests/test_city_chunks_check.gd's own determinism check, which reads
	# body.get_child(1) directly) even though nothing here touches `body`
	# itself anyway.
	var door_node := Node3D.new()
	door_node.position = Vector3(offset.x + door["u0"], 0.0, offset.z + hz)
	parent.add_child(door_node)
	door_node.set_script(BuildingDoorScript)
	door_node.setup(door["height"])

	# "Am I standing inside this building" trigger, for the outdoor city
	# ambience (see player.gd's own enter_building()/exit_building()) to fade
	# out indoors and back in outdoors -- covers the building's own FULL
	# footprint/height (not just the ground floor), since a player on any
	# upper floor, reached via the stairs, is still indoors. Same top-level-
	# child-of-parent placement as door_node just above, for the identical
	# reason (see its own comment) -- not a child of `body`, whose child
	# index a test elsewhere already depends on staying stable.
	var indoor_area := Area3D.new()
	indoor_area.collision_layer = 0
	indoor_area.collision_mask = 4 # characters only, same convention building_glass.gd's own walkthrough trigger already documents
	# Extended DOWN through the basement when there is one -- otherwise the
	# trigger stops at y=0 and a player standing in the basement counts as
	# OUTDOORS, which would leave the windowless floor undimmed (see
	# player.gd's own _fade_indoor_darkness) and the street ambience audible
	# underground.
	var indoor_height: float = size.y + (story_height if has_basement else 0.0)
	var indoor_center_y: float = size.y / 2.0 - (story_height / 2.0 if has_basement else 0.0)
	indoor_area.position = Vector3(offset.x, indoor_center_y, offset.z)
	var indoor_shape := CollisionShape3D.new()
	var indoor_box := BoxShape3D.new()
	indoor_box.size = Vector3(size.x, indoor_height, size.z)
	indoor_shape.shape = indoor_box
	indoor_area.add_child(indoor_shape)
	parent.add_child(indoor_area)
	# bind() so each handler knows which building's area fired -- see their
	# own comment. Named so anything looking for "the building I'm in" has a
	# stable node to find.
	indoor_area.name = "IndoorArea"
	indoor_area.body_entered.connect(_on_building_indoor_area_body_entered.bind(indoor_area))
	indoor_area.body_exited.connect(_on_building_indoor_area_body_exited.bind(indoor_area))

## Signal handlers for the indoor-detection Area3D just above -- duck-typed
## has_method() calls, same convention every other Area3D trigger in this
## file already uses (see building_glass.gd's own try_break_glass caller),
## since whether to react to "someone is now indoors" is entirely the
## PLAYER's own concern (see player.gd's own enter_building()/
## exit_building()), not something this stateless (RefCounted, static-func-
## only) file should know how to drive itself. A hostile NPC wandering
## through (also on collision layer 4, hence the mask alone isn't enough of
## a filter -- see indoor_area's own comment) simply has neither method and
## is silently ignored.
## The Area3D itself is handed along, not just "you are indoors now": it
## carries the building's own centre and footprint in its shape, which is
## everything a caller needs to identify WHICH building this is (see
## player.gd's own /claim handling, which orbits it and sets a spawn inside
## it). Bound rather than read off the signal because body_entered only ever
## reports the body, never which area fired.
static func _on_building_indoor_area_body_entered(body: Node3D, indoor_area: Area3D) -> void:
	if body.has_method("enter_building"):
		body.enter_building(indoor_area)

static func _on_building_indoor_area_body_exited(body: Node3D, indoor_area: Area3D) -> void:
	if body.has_method("exit_building"):
		body.exit_building(indoor_area)

const SIGN_WIDTH := 3.0
const SIGN_HEIGHT := 1.1
const SIGN_BOTTOM_MARGIN := 0.6 # clearance above the door's own fixed height
const SIGN_WALL_OFFSET := 0.05 # pulled off the facade face to avoid z-fighting

## Small colored sign panel mounted flush on the door wall's own outward
## face, just above the door -- identifies a special building type (see
## SPECIAL_BUILDING_PRESETS) from outside. A NEW top-level child of
## `parent`, added AFTER _build_building returns -- same established
## pattern _maybe_add_wing already uses below, and for the same reason: it
## never touches _build_building's own tested geometry/collision at all.
## Positioned directly from `offset`/`size` (mirroring door_node's own
## identical offset math inside _build_building) rather than needing a
## handle to the exterior body, since _build_building doesn't return one.
static func _add_building_sign(parent: Node3D, size: Vector3, offset: Vector3, sign_color: Color) -> void:
	var hz := size.z / 2.0
	var v_layout := _cell_layout(size.y, NOMINAL_FLOOR_METERS)
	var door_height: float = float(v_layout["size"]) * DOOR_HEIGHT_FLOORS
	var y0 := door_height + SIGN_BOTTOM_MARGIN
	var y1 := y0 + SIGN_HEIGHT
	# Guard against a building short enough the sign would poke through its
	# own roof (only reachable right at a preset's own MIN_HEIGHT) -- simply
	# skip it rather than drawing past the roofline.
	if y1 > size.y - 0.3:
		return

	var half_width := SIGN_WIDTH / 2.0
	var z := hz + SIGN_WALL_OFFSET
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	_add_flat_quad(st, [
		Vector3(-half_width, y0, z),
		Vector3(half_width, y0, z),
		Vector3(half_width, y1, z),
		Vector3(-half_width, y1, z),
	], Vector3(0, 0, 1))

	var mesh_inst := MeshInstance3D.new()
	mesh_inst.mesh = st.commit()
	mesh_inst.material_override = _flat_material(sign_color, true) # unshaded -- a flat "signage" look, not lit like the facade
	mesh_inst.position = Vector3(offset.x, 0.0, offset.z)
	parent.add_child(mesh_inst)

## Rolls whether this building gets an attached secondary massing volume
## (see WING_CHANCE's own comment) and, if so, sizes/places it against one
## of the main building's 3 non-door walls. Called by generate_chunk AFTER
## _build_building (not from inside it) and adds its own fresh StaticBody3D
## directly to the chunk root -- appended as a NEW child after the main
## building's own exterior/interior bodies, never inserted between them, so
## it can't shift the child indices tests/test_city_chunks_check.gd's own
## determinism check relies on (get_child(1) must stay the exterior body).
static func _maybe_add_wing(root: Node3D, main_size: Vector3, main_offset: Vector3, rng: RandomNumberGenerator, materials: Dictionary, facade_mat: Material) -> void:
	if rng.randf() >= WING_CHANCE:
		return

	var hx := main_size.x / 2.0
	var hz := main_size.z / 2.0
	var wing_height: float = main_size.y * rng.randf_range(WING_MIN_HEIGHT_FRACTION, WING_MAX_HEIGHT_FRACTION)
	var wing_depth: float = rng.randf_range(WING_MIN_DEPTH, WING_MAX_DEPTH)

	# wall_choice 2 (back, -Z) protrudes straight out from the back wall;
	# 0/1 (left/right) protrude sideways -- index 3 (the door wall, +Z, see
	# _wall_defs) is deliberately never a choice here.
	var wall_choice := rng.randi() % 3
	var wing_size: Vector3
	var wing_center: Vector3
	if wall_choice == 2:
		var wing_width: float = main_size.x * rng.randf_range(WING_MIN_WIDTH_FRACTION, WING_MAX_WIDTH_FRACTION)
		var slack: float = maxf(0.0, hx - WING_MARGIN - wing_width / 2.0)
		var wing_x := rng.randf_range(-slack, slack)
		wing_size = Vector3(wing_width, wing_height, wing_depth)
		wing_center = Vector3(wing_x, 0.0, -hz - wing_depth / 2.0)
	else:
		var wing_width_z: float = main_size.z * rng.randf_range(WING_MIN_WIDTH_FRACTION, WING_MAX_WIDTH_FRACTION)
		var slack_z: float = maxf(0.0, hz - WING_MARGIN - wing_width_z / 2.0)
		var wing_z := rng.randf_range(-slack_z, slack_z)
		var side: float = -1.0 if wall_choice == 0 else 1.0
		wing_size = Vector3(wing_depth, wing_height, wing_width_z)
		wing_center = Vector3(side * (hx + wing_depth / 2.0), 0.0, wing_z)

	_build_wing_massing(root, wing_size, main_offset + wing_center, facade_mat, materials)

## The wing's own geometry: 4 window-tiled vertical faces (reusing
## _add_wall_panel directly -- it doesn't care whose building it's for) plus
## a flat roof cap, and ONE solid collision box for the whole volume, unlike
## the main building's own wall-by-wall collision -- there's no interior
## behind these walls to walk into, so there's no door/stairwell hole to
## leave open for (see _add_building_collision's own comment on why THAT
## one has to be built the harder way).
## glass_mat: real reported bug -- this massing block's own 4 walls used to
## get NO transparency treatment at all (this is a completely separate code
## path from _build_building's own wall construction, so the main
## building's own real breakable-glass windows never applied here), which
## read as "some windows are still dark, not see-through" whenever a
## building happened to grow a wing. Fixed with a purely decorative overlay
## pane per window instead -- no real holes cut and no break state at all
## (a wing has no interior to protect either way, see this function's own
## opening comment), covering the wing's own FULL height (every row) since
## -- unlike the main building -- nothing here is ever meant to be
## breakable in the first place.
## recess_mat: the dark opaque backing quad behind each glass pane -- see
## WINDOW_RECESS_COLOR's own comment on why the glass alone isn't enough
## contrast against a flat body_color wall to read as a window at all.
## A real reported bug this rewrite fixes: "these side buildings are still
## having non transparent windows." The wing used to be a SOLID box (a
## full-height _add_wall_panel) with a decorative glass pane laid on top of
## it -- the exact arrangement build_glass_material's own history already
## documents as impossible to make look transparent, because there is
## nothing behind the pane to see. No amount of material tuning fixes glass
## in front of a wall.
##
## Now built the same way the main building already is: real per-window
## holes punched through the facade (_add_glass_band_frame), real breakable
## panes filling them (building_glass.gd), and a hollow inner shell so
## looking through a window shows a dark room rather than straight out the
## far side of the building.
static func _build_wing_massing(root: Node3D, size: Vector3, center: Vector3, facade_mat: Material, materials: Dictionary) -> void:
	var hx := size.x / 2.0
	var hz := size.z / 2.0
	var v_layout := _cell_layout(size.y, NOMINAL_FLOOR_METERS)

	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var inner_st := SurfaceTool.new()
	inner_st.begin(Mesh.PRIMITIVE_TRIANGLES)

	var body := StaticBody3D.new()
	body.position = center
	body.collision_layer = 1
	root.add_child(body)

	# _wall_defs marks one wall as the DOOR wall; a wing has no doorway of
	# its own, so that flag is stripped here. Left on, building_glass.gd's
	# own _is_door_cell would try to carve a door-shaped exclusion out of
	# this wing's ground row against a door rectangle that doesn't exist.
	var walls: Array = []
	for wall_def in _wall_defs(hx, hz):
		var wall_copy: Dictionary = wall_def.duplicate()
		wall_copy.erase("has_door")
		walls.append(wall_copy)
	var wing_frame_faces: Array = []
	for wall in walls:
		var u0: float = wall["u0"]
		var u1: float = wall["u1"]
		var to_world: Callable = wall["to_world"]
		var normal: Vector3 = wall["normal"]
		# Real holes, frame/mullion geometry between them -- not a painted
		# panel. Full height, matching how every main-building wall is built.
		_add_glass_band_frame(st, u0, u1, 0.0, size.y, v_layout, to_world, normal)
		# Batched, then committed once after this loop -- see
		# _add_glass_frame_piece_collision's own comment.
		_add_glass_band_frame_collision(wing_frame_faces, u0, u1, 0.0, size.y, v_layout, to_world, normal)
		# The inward-facing copy: without it a broken/see-through window
		# looks straight through the wing and out the other side, since a
		# hole in a one-sided surface leaves nothing behind it at all.
		_add_flat_quad(inner_st, _panel_corners(u0, u1, 0.0, size.y, to_world), -normal)

	_add_flat_quad(st, [Vector3(-hx, size.y, -hz), Vector3(hx, size.y, -hz), Vector3(hx, size.y, hz), Vector3(-hx, size.y, hz)], Vector3(0, 1, 0))
	# Interior floor/ceiling so the hollow shell reads as a room rather than
	# an open-topped box seen from inside.
	_add_flat_quad(inner_st, [Vector3(-hx, 0.0, -hz), Vector3(hx, 0.0, -hz), Vector3(hx, 0.0, hz), Vector3(-hx, 0.0, hz)], Vector3(0, 1, 0))
	_add_flat_quad(inner_st, [Vector3(-hx, size.y, -hz), Vector3(hx, size.y, -hz), Vector3(hx, size.y, hz), Vector3(-hx, size.y, hz)], Vector3(0, -1, 0))

	_commit_frame_collision(body, wing_frame_faces)

	var mesh_inst := MeshInstance3D.new()
	mesh_inst.mesh = st.commit()
	mesh_inst.material_override = facade_mat
	body.add_child(mesh_inst)

	var inner_mesh_inst := MeshInstance3D.new()
	inner_mesh_inst.mesh = inner_st.commit()
	inner_mesh_inst.material_override = materials["interior"]
	body.add_child(inner_mesh_inst)

	# Roof cap collision only -- the walls' own per-window frame collision is
	# already added above, and building_glass.gd fills each opening while its
	# pane is intact, so the wing is walk-through-able exactly where (and
	# only where) a window has actually been broken. A solid box here (what
	# this used to have) would have blocked those openings invisibly.
	_add_box_collision(body, Vector3(size.x, WALL_THICKNESS, size.z), Vector3(0, size.y - WALL_THICKNESS / 2.0, 0))

	# Real, breakable panes filling the holes -- same treatment and the same
	# script the main building's own walls get. No door dict: a wing has no
	# doorway of its own, so nothing needs excluding from its ground row.
	if int(v_layout["count"]) > 0:
		body.set_script(BuildingGlassScript)
		# `walls` (the door-flag-stripped copy above), NOT a fresh
		# _wall_defs() -- handing the raw defs here was a real bug: their
		# has_door flag sent building_glass.gd's own _is_door_cell looking
		# for a door rectangle in the empty dict passed alongside it.
		body.setup(walls, float(v_layout["size"]), int(v_layout["count"]), materials["glass_intact"], materials["glass_cracked"], {})

## Small protruding platforms on random upper-floor window rows, on any of
## the building's 3 plain (non-door) walls -- purely decorative massing
## (see BALCONY_CHANCE_PER_ROW's own comment on why there's no collision),
## added onto the already-finished exterior shell via the SAME append-only
## pattern _add_roof_detail already uses, so it can't disturb the collision
## child-index test_city_chunks_check.gd relies on either (its own first
## collision shape stays at body.get_child(1) regardless of how many more
## children get appended after it).
## Vertical concrete fins layered onto a finished building -- see the RIB_*
## constants for the full reasoning (including why these are decorative
## rather than a real windowless wall style). Every fin on the whole
## building goes into ONE SurfaceTool/MeshInstance3D rather than a node per
## fin: a densely ribbed tower is 100+ fins, and this file's own generation
## cost is already the thing that dominates city streaming.
static func _maybe_add_facade_ribs(body: StaticBody3D, size: Vector3, rng: RandomNumberGenerator, v_layout: Dictionary, facade_mat: Material, door: Dictionary) -> void:
	if rng.randf() >= RIB_CHANCE:
		return
	var row_size: float = float(v_layout["size"])
	var y0: float = row_size * RIB_BASE_ROWS
	if size.y - y0 < row_size:
		return # too short to read as a ribbed facade at all

	var hx := size.x / 2.0
	var hz := size.z / 2.0
	var depth: float = rng.randf_range(RIB_MIN_DEPTH, RIB_MAX_DEPTH)
	var rib_all: bool = rng.randf() < RIB_ALL_WALLS_CHANCE

	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var any := false
	for wall in _wall_defs(hx, hz):
		# Rolled per wall (unless this building ribs all of them) -- this is
		# what makes one building read as ribbed concrete on one face and
		# ordinary windows on the next.
		if not rib_all and rng.randf() < 0.5:
			continue
		var u0: float = wall["u0"]
		var u1: float = wall["u1"]
		# The door wall keeps a clear span over its own opening so fins never
		# stand in front of the entrance the player has to walk through.
		var skip_u0: float = door["u0"] - 0.3 if wall.get("has_door", false) else 0.0
		var skip_u1: float = door["u1"] + 0.3 if wall.get("has_door", false) else 0.0
		var has_skip: bool = wall.get("has_door", false)
		var layout := _cell_layout(u1 - u0, RIB_NOMINAL_SPACING)
		var cell: float = float(layout["size"])
		for i in range(int(layout["count"])):
			var center: float = u0 + (float(i) + 0.5) * cell
			var half: float = cell * RIB_WIDTH_RATIO * 0.5
			if has_skip and center + half > skip_u0 and center - half < skip_u1:
				continue
			_add_rib(st, center - half, center + half, y0, size.y, wall["to_world"], wall["normal"], depth)
			any = true
	if not any:
		return

	var mesh_inst := MeshInstance3D.new()
	mesh_inst.mesh = st.commit()
	mesh_inst.material_override = facade_mat
	body.add_child(mesh_inst)

## One fin: a rectangular concrete prism standing proud of its wall plane.
## Only the 3 faces a street-level viewer can actually see (the outer face
## and the two sides) plus a top cap are emitted -- the back face is flush
## against the wall it sits on and the underside is only visible from
## directly below, so both are skipped rather than doubling the fin count's
## own triangle cost for surfaces nothing looks at.
static func _add_rib(st: SurfaceTool, u0: float, u1: float, y0: float, y1: float, to_world: Callable, normal: Vector3, depth: float) -> void:
	var out: Vector3 = normal * depth
	var u_dir: Vector3 = (to_world.call(u1, y0) - to_world.call(u0, y0)).normalized()

	var base := _panel_corners(u0, u1, y0, y1, to_world)
	var outer := []
	for c in base:
		outer.append(c + out)

	_add_flat_quad(st, outer, normal) # outer face
	# Sides: base[0]/base[3] are the u0 edge (bottom/top), base[1]/base[2] the u1 edge.
	_add_flat_quad(st, [base[0], outer[0], outer[3], base[3]], -u_dir)
	_add_flat_quad(st, [base[1], outer[1], outer[2], base[2]], u_dir)
	_add_flat_quad(st, [base[3], outer[3], outer[2], base[2]], Vector3(0, 1, 0)) # top cap

static func _add_balconies(body: StaticBody3D, size: Vector3, rng: RandomNumberGenerator, v_layout: Dictionary, body_color: Color) -> void:
	var hx := size.x / 2.0
	var hz := size.z / 2.0
	var row_size: float = float(v_layout["size"])
	var max_row: int = mini(int(v_layout["count"]) - 1, BALCONY_MAX_ROWS)
	if max_row < 1:
		return
	var eligible_walls: Array = _wall_defs(hx, hz).slice(0, 3) # excludes the door wall (index 3)

	var rail_mat := StandardMaterial3D.new()
	rail_mat.albedo_color = body_color
	rail_mat.roughness = 0.9
	rail_mat.metallic_specular = 0.0

	for row in range(1, max_row + 1): # row 0 (ground) skipped -- a "balcony" at sidewalk level doesn't read as one
		if rng.randf() >= BALCONY_CHANCE_PER_ROW:
			continue
		var wall: Dictionary = eligible_walls[rng.randi() % eligible_walls.size()]
		var normal: Vector3 = wall["normal"]
		var u0: float = wall["u0"]
		var u1: float = wall["u1"]
		var to_world: Callable = wall["to_world"]
		var half_w := BALCONY_WIDTH / 2.0
		if (u1 - u0) < BALCONY_WIDTH + 1.0:
			continue
		var u: float = rng.randf_range(u0 + half_w + 0.5, u1 - half_w - 0.5)
		var y: float = float(row) * row_size + 0.05 # just above that row's own floor line

		# The wall's own normal is always axis-aligned (see _wall_defs --
		# every wall is a plain +/-X or +/-Z plane, never diagonal), so
		# which world axis the wall's own "u" (width) direction runs along
		# is fully determined by the normal -- along_x true means the BACK
		# wall (normal.x == 0, u maps to world X, see _build_wing_massing's
		# own identical walls array); false means a LEFT/RIGHT wall (u maps
		# to world Z). That lets every box here be built axis-aligned with
		# no basis rotation needed at all.
		var along_x := absf(normal.x) < 0.5
		var slab_size: Vector3 = (Vector3(BALCONY_WIDTH, BALCONY_SLAB_THICKNESS, BALCONY_DEPTH) if along_x
			else Vector3(BALCONY_DEPTH, BALCONY_SLAB_THICKNESS, BALCONY_WIDTH))
		var center: Vector3 = to_world.call(u, y) + normal * (BALCONY_DEPTH / 2.0)
		_add_balcony_box(body, slab_size, center, rail_mat)

		var outer_rail_size: Vector3 = (Vector3(BALCONY_WIDTH, BALCONY_RAIL_HEIGHT, BALCONY_RAIL_THICKNESS) if along_x
			else Vector3(BALCONY_RAIL_THICKNESS, BALCONY_RAIL_HEIGHT, BALCONY_WIDTH))
		var outer_rail_pos: Vector3 = center + normal * (BALCONY_DEPTH / 2.0 - BALCONY_RAIL_THICKNESS / 2.0) + Vector3(0, BALCONY_RAIL_HEIGHT / 2.0, 0)
		_add_balcony_box(body, outer_rail_size, outer_rail_pos, rail_mat)

		var side_rail_size: Vector3 = (Vector3(BALCONY_RAIL_THICKNESS, BALCONY_RAIL_HEIGHT, BALCONY_DEPTH) if along_x
			else Vector3(BALCONY_DEPTH, BALCONY_RAIL_HEIGHT, BALCONY_RAIL_THICKNESS))
		var side_offset: Vector3 = Vector3(half_w, 0, 0) if along_x else Vector3(0, 0, half_w)
		_add_balcony_box(body, side_rail_size, center + side_offset + Vector3(0, BALCONY_RAIL_HEIGHT / 2.0, 0), rail_mat)
		_add_balcony_box(body, side_rail_size, center - side_offset + Vector3(0, BALCONY_RAIL_HEIGHT / 2.0, 0), rail_mat)

static func _add_balcony_box(body: StaticBody3D, size: Vector3, pos: Vector3, mat: Material) -> void:
	var mesh_inst := MeshInstance3D.new()
	var bm := BoxMesh.new()
	bm.size = size
	mesh_inst.mesh = bm
	mesh_inst.material_override = mat
	mesh_inst.position = pos
	body.add_child(mesh_inst)

## A single solid BoxShape3D would ignore the door hole cut into the
## visual mesh and block the player from ever walking through it -- so
## collision is built wall-by-wall instead (a thin box per side, plus the
## roof), with the front wall split into the same 3 pieces (left/right/
## above) _add_building_box's visual mesh uses around the door, leaving
## the door's own rectangle uncollided. The roof cap is likewise framed
## around the stairwell opening (see _add_collision_frame) instead of one
## solid box, so the top floor's staircase can actually reach the roof
## instead of bonking an invisible ceiling there.
## glass_band_height: how much of every wall (the door wall included, via
## its own row-0 exclusion -- see _add_glass_band_frame_collision_excluding)
## is handed off to building_glass.gd's own per-row collision instead (see
## its own class comment) -- 0.0 disables breakable glass for this building
## entirely and this function behaves exactly as it always did.
static func _add_building_collision(body: StaticBody3D, size: Vector3, door: Dictionary, stairwell: Dictionary, glass_band_height: float, v_layout: Dictionary) -> void:
	var hx := size.x / 2.0
	var hz := size.z / 2.0

	# Guarded (not just clamped to a degenerate size) the same way
	# ROOM_HEIGHT_FLOORS's own comment already flags for a similar
	# situation -- a size.y <= glass_band_height box here would be zero or
	# negative height, confirmed elsewhere in this file as a real crash.
	if size.y > glass_band_height:
		var upper_height := size.y - glass_band_height
		var upper_center_y := glass_band_height + upper_height / 2.0
		_add_box_collision(body, Vector3(WALL_THICKNESS, upper_height, size.z), Vector3(hx, upper_center_y, 0))
		_add_box_collision(body, Vector3(WALL_THICKNESS, upper_height, size.z), Vector3(-hx, upper_center_y, 0))
		_add_box_collision(body, Vector3(size.x, upper_height, WALL_THICKNESS), Vector3(0, upper_center_y, -hz))
	_add_collision_frame(body, -hx, hx, -hz, hz, stairwell["hole_x0"], stairwell["hole_x1"], stairwell["hole_z0"], stairwell["hole_z1"], size.y, WALL_THICKNESS) # roof cap

	# Real, PERMANENT collision for the frame/mullion pieces around each
	# window cell in the breakable-glass band (see _add_glass_band_frame's
	# own comment on the matching visual) -- building_glass.gd's own row
	# collision (see its own setup()) only ever blocks the FULL row while
	# INTACT/CRACKED and is removed entirely once that row SHATTERS, so
	# without this, a shattered row's own still-visible frame geometry
	# would let a player walk straight through it -- the frame was never a
	# real obstacle on its own, only the row-wide box standing in for it.
	var door_u0: float = door["u0"]
	var door_u1: float = door["u1"]
	var row_size: float = v_layout["size"]
	# One batch for the whole exterior shell -- every wall's own frame
	# pieces merge into a single collision node (see
	# _add_glass_frame_piece_collision's own comment on why).
	var frame_faces: Array = []
	for wall in _wall_defs(hx, hz):
		if wall.get("has_door", false):
			# Same row-0-only door awareness as _add_building_box's own
			# identical branch -- see _add_glass_band_frame_collision_
			# excluding's own comment. Rows above the door (DOOR_HEIGHT_
			# FLOORS is always 1) get the exact same real-band collision
			# every other wall's own full span already gets below.
			_add_glass_band_frame_collision_excluding(frame_faces, wall["u0"], wall["u1"], 0.0, row_size, wall["to_world"], wall["normal"], door_u0, door_u1)
			if size.y > row_size:
				_add_glass_band_frame_collision(frame_faces, wall["u0"], wall["u1"], row_size, size.y, v_layout, wall["to_world"], wall["normal"])
		else:
			_add_glass_band_frame_collision(frame_faces, wall["u0"], wall["u1"], 0.0, minf(glass_band_height, size.y), v_layout, wall["to_world"], wall["normal"])
	_commit_frame_collision(body, frame_faces)

static func _add_box_collision(body: StaticBody3D, size: Vector3, local_pos: Vector3) -> void:
	var collision := CollisionShape3D.new()
	var box_shape := BoxShape3D.new()
	box_shape.size = size
	collision.shape = box_shape
	collision.position = local_pos
	body.add_child(collision)

## Collision counterpart to _add_glass_band_frame -- mirrors its exact same
## per-cell/margin math (any drift between the two would mean collision
## either pokes into a real window opening or leaves a gap next to a
## visibly solid frame piece) but emits box shapes instead of textured
## quads, so it's reusable for the interior wall's own flat-colored frame
## too (see _build_interior's own call site), not just the exterior
## facade's textured one. region_y0/region_y1: the vertical span to cover,
## NOT assumed to start at y=0 -- the exterior shell's own glass band
## always does (see its own call site), but the interior wall's own
## per-FLOOR copy (see _build_interior) needs whatever portion of the
## glass band overlaps THAT floor's own floor_bottom..floor_top span,
## which floors above the ground floor don't start at 0.
static func _add_glass_band_frame_collision(faces: Array, u0: float, u1: float, region_y0: float, region_y1: float, v_layout: Dictionary, to_world: Callable, normal: Vector3) -> void:
	var row_size: float = v_layout["size"]
	var rows := int(round((region_y1 - region_y0) / row_size))
	var u_layout := _cell_layout(u1 - u0, NOMINAL_COLUMN_METERS)
	var cell_width: float = u_layout["size"]
	var cell_count: int = int(u_layout["count"])
	var margin_u := cell_width * WINDOW_MARGIN_RATIO
	var along_x := absf(normal.x) < 0.5 # true: this wall's own "u" runs along world X (the back wall); false: along world Z (left/right walls) -- see _add_balconies' own identical check
	for r in range(rows):
		var y0 := region_y0 + float(r) * row_size
		var y1 := y0 + row_size
		var margin_v := row_size * WINDOW_MARGIN_RATIO
		for i in range(cell_count):
			var col_u0 := u0 + float(i) * cell_width
			var col_u1 := col_u0 + cell_width
			var win_u0 := col_u0 + margin_u
			var win_u1 := col_u1 - margin_u
			var win_y0 := y0 + margin_v
			var win_y1 := y1 - margin_v
			_add_glass_frame_piece_collision(faces, col_u0, win_u0, y0, y1, to_world, along_x)
			_add_glass_frame_piece_collision(faces, win_u1, col_u1, y0, y1, to_world, along_x)
			_add_glass_frame_piece_collision(faces, win_u0, win_u1, y0, win_y0, to_world, along_x)
			_add_glass_frame_piece_collision(faces, win_u0, win_u1, win_y1, y1, to_world, along_x)
		# _cell_layout always evenly divides u1-u0 -- no leftover strip is
		# possible here (see _add_flat_windowed_wall_row's own identical note).

## Collision counterpart to _add_windowed_wall_row_excluding -- see its own
## comment for why whole-cell exclusion against ONE uniform, un-overridden
## grid replaces the door wall's own earlier narrower-segment approach, and
## for why the excluded range itself (the door's own opening) gets NO
## collision at all rather than a solid box spanning the whole cell -- the
## door must stay a real walkthrough opening, not a wall with a smaller
## door-shaped hole punched through it.
## Only ever called for the door wall's own single ground row (DOOR_HEIGHT_
## FLOORS is always 1, see _build_building's own door dict), so y0/y1
## always span exactly one row -- unlike _add_glass_band_frame_collision
## above, this doesn't loop multiple rows.
static func _add_glass_band_frame_collision_excluding(faces: Array, u0: float, u1: float, y0: float, y1: float, to_world: Callable, normal: Vector3, exclude_u0: float, exclude_u1: float) -> void:
	var u_layout := _cell_layout(u1 - u0, NOMINAL_COLUMN_METERS)
	var cell_width: float = u_layout["size"]
	var cell_count: int = int(u_layout["count"])
	var margin_u := cell_width * WINDOW_MARGIN_RATIO
	var margin_v := (y1 - y0) * WINDOW_MARGIN_RATIO
	var along_x := absf(normal.x) < 0.5
	for i in range(cell_count):
		var col_u0 := u0 + float(i) * cell_width
		var col_u1 := col_u0 + cell_width
		if col_u1 > exclude_u0 and col_u0 < exclude_u1:
			if col_u0 < exclude_u0:
				_add_glass_frame_piece_collision(faces, col_u0, exclude_u0, y0, y1, to_world, along_x)
			if col_u1 > exclude_u1:
				_add_glass_frame_piece_collision(faces, exclude_u1, col_u1, y0, y1, to_world, along_x)
			continue
		var win_u0 := col_u0 + margin_u
		var win_u1 := col_u1 - margin_u
		var win_y0 := y0 + margin_v
		var win_y1 := y1 - margin_v
		_add_glass_frame_piece_collision(faces, col_u0, win_u0, y0, y1, to_world, along_x)
		_add_glass_frame_piece_collision(faces, win_u1, col_u1, y0, y1, to_world, along_x)
		_add_glass_frame_piece_collision(faces, win_u0, win_u1, y0, win_y0, to_world, along_x)
		_add_glass_frame_piece_collision(faces, win_u0, win_u1, win_y1, y1, to_world, along_x)

## Appends ONE frame piece's own triangles to a batch, rather than adding a
## CollisionShape3D node for it.
##
## This is the hot path of the whole city: a building's window frame is
## emitted per cell, per row, per wall, for the exterior shell AND for every
## interior floor's own copy, which measured at ~1,750 collision nodes per
## chunk -- and node allocation plus the per-node physics-server registration
## that follows was, by direct measurement, the single largest cost in
## generating a chunk (~4,000 CollisionShape3D per chunk, 97% of all its
## nodes, ~63ms in _build_building alone). That is what made a sprinting
## player stutter every few seconds as a new row of chunks streamed in.
##
## The pieces are static, purely-blocking geometry that nothing ever looks
## up individually, so they lose nothing by being merged into a single
## ConcavePolygonShape3D per body (see _commit_frame_collision). The
## breakable window panes are deliberately NOT merged -- building_glass.gd
## frees an individual cell's own collision node when that pane shatters, so
## those have to stay separate nodes.
static func _add_glass_frame_piece_collision(faces: Array, piece_u0: float, piece_u1: float, y0: float, y1: float, to_world: Callable, along_x: bool) -> void:
	if piece_u1 <= piece_u0 or y1 <= y0:
		return
	var u_size := piece_u1 - piece_u0
	var height := y1 - y0
	var center: Vector3 = to_world.call((piece_u0 + piece_u1) / 2.0, (y0 + y1) / 2.0)
	_append_frame_quad(faces, u_size, height, center, along_x)

## One frame piece as a single flat QUAD (2 triangles) on its own mid-plane,
## appended to `faces`. A plain Array (not a PackedVector3Array) because
## Array is a reference type in GDScript, so callers accumulate into the
## same buffer without threading it back through every return value.
##
## A quad, not the box's full 12 triangles, and that choice is the whole
## point of the merge: cooking a ConcavePolygonShape3D builds a BVH over
## its triangles, and that cook -- not node creation -- turned out to
## dominate once the boxes were merged (measured: solid boxes made a chunk
## SLOWER, 32ms -> 48ms, despite cutting node count ninefold). Six times
## fewer triangles brings the cook back down while keeping the node saving.
##
## Losing the piece's own thickness is safe here: these frames are
## WALL_THICKNESS slabs sitting inside a wall plane, and a player can only
## ever approach one face-on. The shape is given backface_collision (see
## _commit_frame_collision) so it blocks from inside the building too.
## `along_x` is the WALL's own orientation, passed in rather than inferred
## from which dimension happens to be smallest. A real bug that inference
## caused, caught by test_building_glass_check's own door-wall coverage
## sweep: a narrow mullion between two windows can legitimately be thinner
## than WALL_THICKNESS, so "smallest axis" picked the wrong plane and
## collapsed that piece to zero width, leaving a hole in the wall a player
## could walk through.
static func _append_frame_quad(faces: Array, u_size: float, height: float, center: Vector3, along_x: bool) -> void:
	var hu := u_size / 2.0
	var hv := height / 2.0
	if along_x:
		# Spans world X (this wall's own "u") and Y, flat in Z.
		faces.append(Vector3(center.x - hu, center.y - hv, center.z))
		faces.append(Vector3(center.x + hu, center.y - hv, center.z))
		faces.append(Vector3(center.x + hu, center.y + hv, center.z))
		faces.append(Vector3(center.x - hu, center.y - hv, center.z))
		faces.append(Vector3(center.x + hu, center.y + hv, center.z))
		faces.append(Vector3(center.x - hu, center.y + hv, center.z))
	else:
		# Spans world Z and Y, flat in X.
		faces.append(Vector3(center.x, center.y - hv, center.z - hu))
		faces.append(Vector3(center.x, center.y - hv, center.z + hu))
		faces.append(Vector3(center.x, center.y + hv, center.z + hu))
		faces.append(Vector3(center.x, center.y - hv, center.z - hu))
		faces.append(Vector3(center.x, center.y + hv, center.z + hu))
		faces.append(Vector3(center.x, center.y + hv, center.z - hu))

## Turns a batch of accumulated triangles into ONE collision node. A no-op
## for an empty batch, so callers don't need to guard (a building with no
## glass band, or a wall span too short to fit a single cell, legitimately
## produces nothing).
static func _commit_frame_collision(body: StaticBody3D, faces: Array) -> void:
	if faces.is_empty():
		return
	var collision := CollisionShape3D.new()
	var shape := ConcavePolygonShape3D.new()
	# Required, since the pieces are single-sided quads now (see
	# _append_box_faces) -- without it a frame would block from the street
	# but not from inside the room behind it.
	shape.backface_collision = true
	shape.set_faces(PackedVector3Array(faces))
	collision.shape = shape
	body.add_child(collision)

## One row's own worth of purely decorative glass overlay quads -- same
## per-cell/margin layout _add_windowed_wall_row uses for the breakable
## band's own real holes, just emitting a flat transparent quad at each
## window's own inset rectangle instead of leaving a gap there. ALSO emits a
## dark opaque backing quad (see WINDOW_RECESS_COLOR's own comment) a hair
## further back at the SAME rect, into a separate `recess_st` -- without it
## the transparent glass pane alone barely tints the plain body_color wall
## behind it, so the window doesn't read as a window at all from any
## distance. Both quads come from this one loop's own single u_layout/
## margin computation, so they're geometrically incapable of disagreeing
## with each other the way the old baked-texture rectangle and this overlay
## used to (see WINDOW_RECESS_COLOR's own comment).
## Only used for wing massing volumes now (_build_wing_massing) -- a wing
## has no real interior behind its own facade, so its own windows stay
## purely decorative rather than real holes (see _build_wing_massing's own
## class comment).
static func _add_window_glass_row(recess_st: SurfaceTool, glass_st: SurfaceTool, u0: float, u1: float, y0: float, y1: float, to_world: Callable, normal: Vector3) -> void:
	var u_layout := _cell_layout(u1 - u0, NOMINAL_COLUMN_METERS)
	var cell_width: float = u_layout["size"]
	var cell_count: int = int(u_layout["count"])
	var row_height := y1 - y0
	var margin_u := cell_width * WINDOW_MARGIN_RATIO
	var margin_v := row_height * WINDOW_MARGIN_RATIO
	var recess_inset: Vector3 = normal * 0.01 # behind the glass, in front of the (unmodified) wall
	var glass_inset: Vector3 = normal * 0.02 # same "sits a hair proud of the wall" convention building_glass.gd's own row visual already uses
	for i in range(cell_count):
		var col_u0 := u0 + float(i) * cell_width
		var win_u0 := col_u0 + margin_u
		var win_u1 := col_u0 + cell_width - margin_u
		var win_y0 := y0 + margin_v
		var win_y1 := y1 - margin_v
		var recess_corners: Array = [
			to_world.call(win_u0, win_y0) + recess_inset,
			to_world.call(win_u1, win_y0) + recess_inset,
			to_world.call(win_u1, win_y1) + recess_inset,
			to_world.call(win_u0, win_y1) + recess_inset,
		]
		_add_flat_quad(recess_st, recess_corners, normal)
		var glass_corners: Array = [
			to_world.call(win_u0, win_y0) + glass_inset,
			to_world.call(win_u1, win_y0) + glass_inset,
			to_world.call(win_u1, win_y1) + glass_inset,
			to_world.call(win_u0, win_y1) + glass_inset,
		]
		_add_flat_quad(glass_st, glass_corners, normal)

## A stack of `num_floors` empty rooms just inside a building's front door,
## each `story_height` tall (except the top one, see below), all sharing
## the exterior shell's own X/Z footprint. Floor 0 is the original ground
## lobby -- door cut into its own front wall, floor is just the chunk's
## existing ground (already collidable, see _add_ground_body), no separate
## floor slab needed. Every floor above it is a plain box (no door -- you
## reach it by the stairs, not another entrance), separated from the floor
## below by one shared slab with a stairwell-sized hole (see
## _add_floor_with_hole/_add_collision_frame): built once per gap, it
## doubles as that floor's own ceiling AND the next floor's own floor, so
## there's no duplicate coincident geometry between them. The TOP floor's
## own ceiling is the exterior roof's own already-holed cap (see
## _add_building_box/_add_building_collision) rather than another slab
## here, and absorbs whatever height is left over past the last even
## `story_height` line (num_floors is rounded DOWN from size.y /
## story_height) so its own staircase lands exactly at the roof hole with
## no dead gap above it. Every floor gets its own straight staircase up to
## the next level (see _add_staircase/_stairwell_for_floor), against one
## of the two side walls (alternating floor to floor) rather than
## centered, inside the same stairwell footprint the holes directly above
## and below that SAME floor share (the floor above's own flight, going up
## further still, gets its own -- opposite-side -- footprint). Plain flat-
## colored walls, no windows: deliberately undetailed, a placeholder
## rather than a finished interior -- no furniture, no railings around the
## stairwell holes.
## glass_band_height: see _add_building_box's own comment -- the SAME
## bottom band, on the SAME 3 non-door walls, needs to be left as a real
## opening on the interior side too (both the visual quad AND its own
## separate collision box, see below), or a player could see/shoot straight
## through the now-open exterior facade only to find this file's OWN
## interior wall still standing there solid, undoing the whole fix. Floors
## (measured in story_height units) and glass rows (measured in the
## smaller v_layout row units) don't share a grid, so glass_band_height can
## land partway through a floor's own span -- clipped per floor via
## maxf(floor_bottom, glass_band_height) rather than an all-or-nothing
## per-floor toggle, the same "partial band" shape _add_building_collision
## already established for the exterior shell.
static func _build_interior(parent: Node3D, offset: Vector3, size: Vector3, door: Dictionary, v_layout: Dictionary, num_floors: int, story_height: float, stair_run: float, wall_mat: Material, floor_mat: Material, stair_mat: Material, glass_band_height: float, has_basement: bool = false) -> void:
	var hx := size.x / 2.0
	var hz := size.z / 2.0
	var door_u0: float = door["u0"]
	var door_u1: float = door["u1"]
	var door_height: float = door["height"]
	var row_size: float = v_layout["size"]

	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	# Floor/ceiling slabs are a separate mesh (own material) from the walls
	# -- see INTERIOR_FLOOR_COLOR's own comment for why they read better as
	# a visibly different tone rather than sharing the wall material.
	var floor_st := SurfaceTool.new()
	floor_st.begin(Mesh.PRIMITIVE_TRIANGLES)

	var body := StaticBody3D.new()
	body.position = Vector3(offset.x, 0.0, offset.z)
	body.collision_layer = 1
	parent.add_child(body)

	for floor_idx in range(num_floors):
		var is_ground := floor_idx == 0
		var is_top := floor_idx == num_floors - 1
		var floor_bottom: float = floor_idx * story_height
		var floor_top: float = size.y if is_top else floor_bottom + story_height
		var floor_height := floor_top - floor_bottom
		# This floor's OWN flight (climbing up to floor_top) -- rotates to a
		# different wall than the floor below's, so arriving at the top of
		# one flight leaves the next flight's start just around the corner
		# rather than clear across the room (see _stairwell_for_floor's own
		# comment for the full reasoning).
		var stairwell := _stairwell_for_floor(floor_idx, hx, hz, stair_run)
		var h_x0: float = stairwell["hole_x0"]
		var h_x1: float = stairwell["hole_x1"]
		var h_z0: float = stairwell["hole_z0"]
		var h_z1: float = stairwell["hole_z1"]

		# Same 4 walls as the exterior shell, at the same positions, but
		# wound INWARD (visible from inside looking out, not outside
		# looking in) -- achieved simply by using each wall's inward-
		# pointing normal instead of its outward one; _emit_quad's own
		# winding correction (via _add_flat_quad) handles the rest.
		var walls := [
			{"normal": Vector3(-1, 0, 0), "u0": -hz, "u1": hz, "to_world": func(u, y): return Vector3(hx, y, u)},
			{"normal": Vector3(1, 0, 0), "u0": -hz, "u1": hz, "to_world": func(u, y): return Vector3(-hx, y, u)},
			{"normal": Vector3(0, 0, 1), "u0": -hx, "u1": hx, "to_world": func(u, y): return Vector3(u, y, -hz)},
			{"normal": Vector3(0, 0, -1), "u0": -hx, "u1": hx, "to_world": func(u, y): return Vector3(u, y, hz), "has_door": is_ground},
		]

		# One batch per FLOOR (each floor has its own body), committed after
		# this wall loop -- see _add_glass_frame_piece_collision's own
		# comment on why these merge.
		var floor_frame_faces: Array = []
		for wall in walls:
			var normal: Vector3 = wall["normal"]
			var u0: float = wall["u0"]
			var u1: float = wall["u1"]
			var to_world: Callable = wall["to_world"]
			if wall.get("has_door", false):
				# Row 0 (the door's own row -- DOOR_HEIGHT_FLOORS is always
				# 1) needs door-aware treatment; every row above it on this
				# SAME interior-facing wall is just an ordinary real glass-
				# band row, identical to the `else` branch below -- see
				# _add_windowed_wall_row_excluding's own comment for why
				# whole-cell exclusion against this wall's own natural grid
				# replaces the earlier narrower-segment decorative-only
				# treatment (which read as a blank slab from inside, a real
				# reported bug, since it was never see-through either).
				_add_flat_windowed_wall_row_excluding(st, u0, u1, 0.0, row_size, to_world, normal, door_u0, door_u1)
				_add_glass_band_frame_collision_excluding(floor_frame_faces, u0, u1, 0.0, row_size, to_world, normal, door_u0, door_u1)
				if floor_top > row_size:
					_add_flat_glass_band_frame(st, u0, u1, row_size, floor_top, row_size, to_world, normal)
					_add_glass_band_frame_collision(floor_frame_faces, u0, u1, row_size, floor_top, v_layout, to_world, normal)
			else:
				# Above the glass band: an ordinary solid flat quad, same as
				# always. wall_y0 is where that solid portion starts (either
				# this floor's own bottom, if the glass band doesn't reach
				# this floor at all, or the glass band's own top, if it does).
				var wall_y0: float = maxf(floor_bottom, glass_band_height)
				if floor_top > wall_y0:
					_add_flat_quad(st, _panel_corners(u0, u1, wall_y0, floor_top, to_world), normal)
				# The glass band's own overlap with THIS floor, if any --
				# real per-window holes (see _add_flat_glass_band_frame's own
				# comment), mirroring _add_building_box's own exterior-wall
				# treatment so both sides of a broken window read the same
				# way, not a plain skipped gap the way an earlier version
				# left it (reading as one giant opening instead of a normal
				# wall with individual windows -- a real reported bug).
				var glass_region_top: float = minf(floor_top, glass_band_height)
				if glass_region_top > floor_bottom:
					_add_flat_glass_band_frame(st, u0, u1, floor_bottom, glass_region_top, v_layout["size"], to_world, normal)
					_add_glass_band_frame_collision(floor_frame_faces, u0, u1, floor_bottom, glass_region_top, v_layout, to_world, normal)
				# The solid portion's own collision -- to_world.call(0.0, ...)
				# reads this wall's own correct world position directly
				# (this array's own "u" doesn't map to world axes the same
				# simple way for every entry -- see _wall_defs' own inward-
				# facing normals -- so asking to_world for it here avoids
				# re-deriving that sign per wall by hand).
				var solid_height := floor_top - wall_y0
				if solid_height > 0.0:
					var solid_center_y := wall_y0 + solid_height / 2.0
					var center: Vector3 = to_world.call(0.0, solid_center_y)
					if absf(normal.x) > 0.5:
						_add_box_collision(body, Vector3(WALL_THICKNESS, solid_height, size.z), center)
					else:
						_add_box_collision(body, Vector3(size.x, solid_height, WALL_THICKNESS), center)

		_commit_frame_collision(body, floor_frame_faces)

		# Carves one small separate, enterable room out of this floor's own
		# back corner (see ROOM_WIDTH_FRACTION's own comment) -- every floor
		# gets one, not just a chosen few, so "multiple rooms" reads as a
		# real property of the building rather than a rare occasional extra.
		_add_room_partition(st, body, hx, hz, floor_idx, floor_bottom, floor_top, door_height)

		# No separate front-wall collision block here anymore -- the
		# per-wall loop above already builds full, correct collision for
		# EVERY wall at every floor_idx, has_door or not (real per-window
		# frame collision via _add_glass_band_frame_collision/_excluding,
		# with building_glass.gd's own per-cell collision filling each
		# opening while intact). A leftover legacy block used to ALSO add a
		# full solid box here spanning this same wall's entire span -- once
		# glass_band_height covers the full building height, that would
		# have silently blocked every real window on this wall with
		# invisible solid collision, undoing the whole point of this fix.

		# The slab between this floor and the next -- doubles as this
		# floor's own ceiling and the next floor's own floor, so it's only
		# built once per gap. Given REAL thickness (WALL_THICKNESS, matching
		# _add_collision_frame's own long-established thickness for this
		# exact slab) rather than drawing both faces at the same coincident
		# y -- confirmed directly (a real screenshot) that two zero-
		# thickness faces sharing one y let the staircase FROM THE FLOOR
		# ABOVE visibly bleed/z-fight through what should have read as a
		# solid ceiling. The top face stays at floor_top (the walkable
		# surface everything else already measures from); the bottom face
		# sits WALL_THICKNESS below it, with a thin collar closing the
		# gap around the hole's own inner edge (see _add_floor_hole_collar)
		# so the opening reads as a hole cut through solid material instead
		# of a gap between two floating sheets. The top floor instead
		# reuses the exterior roof's own already-holed cap (at size.y, see
		# _add_building_box) for its OUTWARD-facing top side, so only the
		# underside + collar are built here for it.
		if not is_top:
			_add_floor_with_hole(floor_st, -hx, hx, -hz, hz, h_x0, h_x1, h_z0, h_z1, floor_top - WALL_THICKNESS, Vector3(0, -1, 0)) # this floor's ceiling, seen from below
			_add_floor_with_hole(floor_st, -hx, hx, -hz, hz, h_x0, h_x1, h_z0, h_z1, floor_top, Vector3(0, 1, 0)) # next floor's floor, seen from above
			_add_floor_hole_collar(floor_st, h_x0, h_x1, h_z0, h_z1, floor_top - WALL_THICKNESS, floor_top)
			_add_collision_frame(body, -hx, hx, -hz, hz, h_x0, h_x1, h_z0, h_z1, floor_top, WALL_THICKNESS)
		else:
			_add_floor_with_hole(floor_st, -hx, hx, -hz, hz, h_x0, h_x1, h_z0, h_z1, floor_top - WALL_THICKNESS, Vector3(0, -1, 0)) # roof cap's underside, seen from below
			_add_floor_hole_collar(floor_st, h_x0, h_x1, h_z0, h_z1, floor_top - WALL_THICKNESS, floor_top)

		_add_staircase(body, stairwell["wall_point"], stairwell["run_dir"], stairwell["width_dir"], stair_run, floor_bottom, floor_height, STAIR_WIDTH, stair_mat)

	if has_basement:
		_build_basement(st, floor_st, body, hx, hz, story_height, stair_run, stair_mat)

	var mesh_inst := MeshInstance3D.new()
	mesh_inst.mesh = st.commit()
	mesh_inst.material_override = wall_mat
	body.add_child(mesh_inst)

	var floor_mesh_inst := MeshInstance3D.new()
	floor_mesh_inst.mesh = floor_st.commit()
	floor_mesh_inst.material_override = floor_mat
	body.add_child(floor_mesh_inst)

## One floor BELOW ground level, reached by a stairwell descending through a
## real hole in the chunk's own ground slab (cut by _add_ground_body, which
## is handed the matching rectangle back in generate_chunk -- both sides
## derive it from the same _stairwell_for_floor(BASEMENT_STAIR_CORNER) call,
## so the hole and the stairs can't drift apart).
##
## Deliberately windowless: it's underground, so this skips the glass-band
## treatment every above-ground floor gets and just builds 4 solid walls.
## That also keeps it entirely clear of building_glass.gd, which reasons in
## whole window rows measured up from y=0 and has no concept of a negative
## floor.
static func _build_basement(st: SurfaceTool, floor_st: SurfaceTool, body: StaticBody3D, hx: float, hz: float, story_height: float, stair_run: float, stair_mat: Material) -> void:
	var floor_y := -story_height
	var well := _stairwell_for_floor(BASEMENT_STAIR_CORNER, hx, hz, stair_run)
	var h_x0: float = well["hole_x0"]
	var h_x1: float = well["hole_x1"]
	var h_z0: float = well["hole_z0"]
	var h_z1: float = well["hole_z1"]

	# 4 solid walls, wound inward (same inward-normal convention the
	# above-ground interior walls use).
	var walls := [
		{"normal": Vector3(-1, 0, 0), "u0": -hz, "u1": hz, "to_world": func(u, y): return Vector3(hx, y, u)},
		{"normal": Vector3(1, 0, 0), "u0": -hz, "u1": hz, "to_world": func(u, y): return Vector3(-hx, y, u)},
		{"normal": Vector3(0, 0, 1), "u0": -hx, "u1": hx, "to_world": func(u, y): return Vector3(u, y, -hz)},
		{"normal": Vector3(0, 0, -1), "u0": -hx, "u1": hx, "to_world": func(u, y): return Vector3(u, y, hz)},
	]
	for wall in walls:
		var normal: Vector3 = wall["normal"]
		var to_world: Callable = wall["to_world"]
		_add_flat_quad(st, _panel_corners(wall["u0"], wall["u1"], floor_y, 0.0, to_world), normal)
		var center: Vector3 = to_world.call(0.0, floor_y + story_height / 2.0)
		if absf(normal.x) > 0.5:
			_add_box_collision(body, Vector3(WALL_THICKNESS, story_height, hz * 2.0), center)
		else:
			_add_box_collision(body, Vector3(hx * 2.0, story_height, WALL_THICKNESS), center)

	# The basement's own floor -- solid, no hole: nothing goes below this.
	_add_floor_with_hole(floor_st, -hx, hx, -hz, hz, 0.0, 0.0, 0.0, 0.0, floor_y, Vector3(0, 1, 0))
	_add_box_collision(body, Vector3(hx * 2.0, WALL_THICKNESS, hz * 2.0), Vector3(0, floor_y - WALL_THICKNESS / 2.0, 0))

	# Its ceiling, which is the ground floor's own floor -- holed for the
	# stairwell, with the same underside + collar + collision-frame
	# treatment every other inter-floor slab gets (see _build_interior's own
	# comment on why the slab has real thickness rather than one flat face).
	_add_floor_with_hole(floor_st, -hx, hx, -hz, hz, h_x0, h_x1, h_z0, h_z1, -WALL_THICKNESS, Vector3(0, -1, 0))
	_add_floor_hole_collar(floor_st, h_x0, h_x1, h_z0, h_z1, -WALL_THICKNESS, 0.0)
	_add_collision_frame(body, -hx, hx, -hz, hz, h_x0, h_x1, h_z0, h_z1, 0.0, WALL_THICKNESS)

	# Rises from the basement floor back up to ground level -- the same
	# flight the player walks DOWN to get here in the first place.
	_add_staircase(body, well["wall_point"], well["run_dir"], well["width_dir"], stair_run, floor_y, story_height, STAIR_WIDTH, stair_mat)

## Flat horizontal rectangle (an interior floor/ceiling slab) with a
## rectangular hole cut out of it for a staircase to pass through -- the
## same "cut a rectangle into an up-to-4-piece frame" idea _add_building_box
## already uses for a door, just applied to a horizontal surface. Any hole
## edge that already lines up with the slab's own edge just yields a
## zero-width (skipped) piece on that side.
static func _add_floor_with_hole(st: SurfaceTool, x0: float, x1: float, z0: float, z1: float, hole_x0: float, hole_x1: float, hole_z0: float, hole_z1: float, y: float, normal: Vector3) -> void:
	if hole_x0 > x0:
		_add_flat_quad(st, [Vector3(x0, y, z0), Vector3(hole_x0, y, z0), Vector3(hole_x0, y, z1), Vector3(x0, y, z1)], normal)
	if x1 > hole_x1:
		_add_flat_quad(st, [Vector3(hole_x1, y, z0), Vector3(x1, y, z0), Vector3(x1, y, z1), Vector3(hole_x1, y, z1)], normal)
	if hole_z0 > z0:
		_add_flat_quad(st, [Vector3(hole_x0, y, z0), Vector3(hole_x1, y, z0), Vector3(hole_x1, y, hole_z0), Vector3(hole_x0, y, hole_z0)], normal)
	if z1 > hole_z1:
		_add_flat_quad(st, [Vector3(hole_x0, y, hole_z1), Vector3(hole_x1, y, hole_z1), Vector3(hole_x1, y, z1), Vector3(hole_x0, y, z1)], normal)

## Fills the vertical gap around a floor/ceiling slab's own stairwell hole
## with 4 thin quads (one per hole edge), connecting the slab's bottom face
## (y_bottom) up to its top face (y_top) -- see _build_interior's own
## comment on why the slab needs real thickness at all. Without this, a
## thick slab's hole would show as an open-sided gap (daylight visible
## between the two flat faces) instead of reading as a hole cut cleanly
## through solid material. Each face's normal points INWARD (toward the
## hole's own center) -- that's the side actually visible to someone
## standing in the stairwell opening looking at its own edges; the outward
## side faces into solid slab material and is never seen.
static func _add_floor_hole_collar(st: SurfaceTool, hole_x0: float, hole_x1: float, hole_z0: float, hole_z1: float, y_bottom: float, y_top: float) -> void:
	_add_flat_quad(st, [Vector3(hole_x0, y_bottom, hole_z0), Vector3(hole_x0, y_bottom, hole_z1), Vector3(hole_x0, y_top, hole_z1), Vector3(hole_x0, y_top, hole_z0)], Vector3(1, 0, 0))
	_add_flat_quad(st, [Vector3(hole_x1, y_bottom, hole_z1), Vector3(hole_x1, y_bottom, hole_z0), Vector3(hole_x1, y_top, hole_z0), Vector3(hole_x1, y_top, hole_z1)], Vector3(-1, 0, 0))
	_add_flat_quad(st, [Vector3(hole_x1, y_bottom, hole_z0), Vector3(hole_x0, y_bottom, hole_z0), Vector3(hole_x0, y_top, hole_z0), Vector3(hole_x1, y_top, hole_z0)], Vector3(0, 0, 1))
	_add_flat_quad(st, [Vector3(hole_x0, y_bottom, hole_z1), Vector3(hole_x1, y_bottom, hole_z1), Vector3(hole_x1, y_top, hole_z1), Vector3(hole_x0, y_top, hole_z1)], Vector3(0, 0, -1))

## Collision counterpart to _add_floor_with_hole: up to 4 thin box shapes
## framing a rectangular hole in a horizontal slab, instead of one solid
## box that would ignore the hole and block the stairwell.
##
## `y` is the slab's own WALKABLE (top) surface -- matching
## _add_floor_with_hole's own `y`, which is where the visual mesh is drawn
## -- not the box's center. Centering each box AT y (as an earlier version
## did) put its top surface at y + thickness/2, floating every floor
## (roofs included) half the wall thickness above the visible floor mesh:
## confirmed directly, both by dropping a physics body onto a known roof
## (settled 5-10cm above the visual surface) and in a real screenshot of
## the player floating above a rooftop. Shifting the box down by
## thickness/2 here (instead of at every individual call site) puts the
## top flush with `y` once, for every caller.
static func _add_collision_frame(body: StaticBody3D, x0: float, x1: float, z0: float, z1: float, hole_x0: float, hole_x1: float, hole_z0: float, hole_z1: float, y: float, thickness: float) -> void:
	var center_y := y - thickness / 2.0
	if hole_x0 > x0:
		_add_box_collision(body, Vector3(hole_x0 - x0, thickness, z1 - z0), Vector3((x0 + hole_x0) / 2.0, center_y, (z0 + z1) / 2.0))
	if x1 > hole_x1:
		_add_box_collision(body, Vector3(x1 - hole_x1, thickness, z1 - z0), Vector3((hole_x1 + x1) / 2.0, center_y, (z0 + z1) / 2.0))
	if hole_z0 > z0:
		_add_box_collision(body, Vector3(hole_x1 - hole_x0, thickness, hole_z0 - z0), Vector3((hole_x0 + hole_x1) / 2.0, center_y, (z0 + hole_z0) / 2.0))
	if z1 > hole_z1:
		_add_box_collision(body, Vector3(hole_x1 - hole_x0, thickness, z1 - hole_z1), Vector3((hole_x0 + hole_x1) / 2.0, center_y, (hole_z1 + z1) / 2.0))

## The X/Z footprint of one floor's own staircase, and the matching hole
## cut around it -- hugs a CORNER (flush against both the wall it climbs
## toward AND one of the two side walls meeting there, same as the
## original single-wall version always did), rotating through all 4
## corners (back-left, back-right, front-right, front-left, back-left,
## ...) one floor at a time, alternating which AXIS it climbs toward (Z,
## then X, then Z, then X) so it's genuinely a different WALL each floor,
## not just a different corner of the same one.
##
## Used to just alternate LEFT/RIGHT while always staying against the back
## wall (opposite the door) -- but that meant a floor's own flight always
## started against the SAME back wall the floor below's flight had just
## climbed up to meet, then ran all the way across the room to its own
## open end on the OPPOSITE side: arriving at the top of one flight left
## the next flight's start clear across the room, confirmed directly as a
## real, disorienting "where do I even go next" problem in a real player
## screenshot. A first attempt at fixing this centered every flight in the
## middle of its wall instead of hugging a corner, which turned out to be
## an overcorrection -- also confirmed directly, from a second screenshot
## showing the stairwell opening floating in the middle of the floor with
## no wall anywhere near it. Hugging a rotating CORNER instead keeps every
## flight flush against a real wall (like the original always was) while
## still climbing toward a different wall each floor, so the next flight's
## start lands along the SAME wall the previous one topped out at (a walk
## along one wall to get there, not a diagonal crossing of the open room).
##
## The hole matches the stairs' own footprint exactly (hole_x0/x1/z0/z1 are
## just x0/x1/z0/z1) -- a wider hole was tried, to make the opening read as
## more than just the stairs' own silhouette, but it left visible gaps of
## open floor hanging between the stairs and the hole's own edges, which
## looked like an unintentional, disconnected pit rather than a clean
## stairwell cutout.
static func _stairwell_for_floor(floor_idx: int, hx: float, hz: float, run: float) -> Dictionary:
	var corner := floor_idx % 4
	var half_width := STAIR_WIDTH / 2.0
	var side_x: float
	var side_z: float
	var near_axis_is_z: bool
	match corner:
		0: side_x = -1.0; side_z = -1.0; near_axis_is_z = true   # back-left, climbs toward the back wall
		1: side_x = 1.0; side_z = -1.0; near_axis_is_z = false   # back-right, climbs toward the right wall
		2: side_x = 1.0; side_z = 1.0; near_axis_is_z = true     # front-right, climbs toward the front wall
		_: side_x = -1.0; side_z = 1.0; near_axis_is_z = false   # front-left, climbs toward the left wall

	# near_wall_dir points from the room's own center straight at the wall
	# this floor's flight climbs toward; hug_dir points at the (different)
	# side wall it stays flush against for its whole run. run_dir (the
	# direction the flight actually extends) is the OPPOSITE of
	# near_wall_dir, by construction -- away from that wall, into the room.
	var near_wall_dir: Vector3
	var hug_dir: Vector3
	var near_extent: float
	var hug_extent: float
	if near_axis_is_z:
		near_wall_dir = Vector3(0, 0, side_z)
		hug_dir = Vector3(side_x, 0, 0)
		near_extent = hz
		hug_extent = hx
	else:
		near_wall_dir = Vector3(side_x, 0, 0)
		hug_dir = Vector3(0, 0, side_z)
		near_extent = hx
		hug_extent = hz

	var run_dir := -near_wall_dir
	var width_dir := hug_dir
	var wall_point := near_wall_dir * (near_extent - STAIR_MARGIN) + hug_dir * (hug_extent - STAIR_SIDE_MARGIN - half_width)
	var far_point := wall_point + run_dir * run
	var corner_a := wall_point - width_dir * half_width
	var corner_b := far_point + width_dir * half_width
	var x0 := minf(corner_a.x, corner_b.x)
	var x1 := maxf(corner_a.x, corner_b.x)
	var z0 := minf(corner_a.z, corner_b.z)
	var z1 := maxf(corner_a.z, corner_b.z)

	return {
		"x0": x0, "x1": x1, "z0": z0, "z1": z1,
		"hole_x0": x0, "hole_x1": x1, "hole_z0": z0, "hole_z1": z1,
		"wall_point": wall_point, "run_dir": run_dir, "width_dir": width_dir,
	}

## Which of the 2 BACK corners (0=back-left, 1=back-right -- see
## _stairwell_for_floor's own corner numbering) this floor's carved-out room
## uses: whichever one this floor's OWN staircase corner (floor_idx % 4)
## ISN'T, so the two can never land on the same corner. When the stairwell
## is on a FRONT corner (2 or 3, the door-wall side) instead, both back
## corners are free -- alternates by floor_idx purely for visual variety
## between floors, not because either one is unsafe.
static func _room_corner_for_floor(floor_idx: int) -> int:
	var stair_corner := floor_idx % 4
	if stair_corner == 0:
		return 1
	if stair_corner == 1:
		return 0
	return 0 if floor_idx % 2 == 0 else 1

## Splits one back corner of this floor into a small separate, enterable
## room with its own doorway -- see ROOM_WIDTH_FRACTION's own comment for
## why this is always on the BACK wall (never the door wall) and always the
## corner this floor's own staircase isn't using. Reuses the exact same
## "up to 3 pieces around a door-height opening" shape _add_building_box's
## own door already uses, both for the visual quads (here doubled, front
## and back, since -- unlike every other interior wall in this file -- both
## sides of this one are real walkable space, not a wall with open air
## behind it) and for collision.
static func _add_room_partition(st: SurfaceTool, body: StaticBody3D, hx: float, hz: float, floor_idx: int, floor_bottom: float, floor_top: float, door_height: float) -> void:
	if hx < MIN_ROOM_SIZE + 0.5 or hz < MIN_ROOM_SIZE + 0.5:
		return # building too small to carve out a separate room without leaving the rest of the floor a sliver

	# Capped at half of either footprint axis (minus a small safety margin)
	# so the carved room can never cross the building's own centerline --
	# which is what structurally guarantees it stays clear of a stairwell
	# hugging the OPPOSITE back corner too, not just a same-corner one (see
	# _room_corner_for_floor's own comment).
	var room_w: float = clampf(2.0 * hx * ROOM_WIDTH_FRACTION, MIN_ROOM_SIZE, hx - 0.5)
	var room_d: float = clampf(2.0 * hz * ROOM_DEPTH_FRACTION, MIN_ROOM_SIZE, hz - 0.5)

	var corner := _room_corner_for_floor(floor_idx)
	var room_x0: float = -hx if corner == 0 else hx - room_w
	var room_x1: float = -hx + room_w if corner == 0 else hx
	var room_z0 := -hz
	var room_z1 := -hz + room_d

	# Wall A: the room's own doorway wall, at whichever X edge sits AWAY
	# from the exterior corner it hugs.
	var wall_a_x: float = room_x1 if corner == 0 else room_x0
	var door_z0 := room_z0 + (room_d - DOOR_WIDTH) / 2.0
	var door_z1 := door_z0 + DOOR_WIDTH
	for n in [Vector3(1, 0, 0), Vector3(-1, 0, 0)]:
		if door_z0 > room_z0:
			_add_flat_quad(st, [Vector3(wall_a_x, floor_bottom, room_z0), Vector3(wall_a_x, floor_bottom, door_z0), Vector3(wall_a_x, floor_top, door_z0), Vector3(wall_a_x, floor_top, room_z0)], n)
		if room_z1 > door_z1:
			_add_flat_quad(st, [Vector3(wall_a_x, floor_bottom, door_z1), Vector3(wall_a_x, floor_bottom, room_z1), Vector3(wall_a_x, floor_top, room_z1), Vector3(wall_a_x, floor_top, door_z1)], n)
		_add_flat_quad(st, [Vector3(wall_a_x, floor_bottom + door_height, door_z0), Vector3(wall_a_x, floor_bottom + door_height, door_z1), Vector3(wall_a_x, floor_top, door_z1), Vector3(wall_a_x, floor_top, door_z0)], n)

	# Wall B: the room's other inward-facing side -- solid, no opening (one
	# doorway is enough for a small corner room).
	for n in [Vector3(0, 0, 1), Vector3(0, 0, -1)]:
		_add_flat_quad(st, [Vector3(room_x0, floor_bottom, room_z1), Vector3(room_x1, floor_bottom, room_z1), Vector3(room_x1, floor_top, room_z1), Vector3(room_x0, floor_top, room_z1)], n)

	var floor_height := floor_top - floor_bottom
	if door_z0 > room_z0:
		_add_box_collision(body, Vector3(WALL_THICKNESS, floor_height, door_z0 - room_z0), Vector3(wall_a_x, (floor_bottom + floor_top) / 2.0, (room_z0 + door_z0) / 2.0))
	if room_z1 > door_z1:
		_add_box_collision(body, Vector3(WALL_THICKNESS, floor_height, room_z1 - door_z1), Vector3(wall_a_x, (floor_bottom + floor_top) / 2.0, (door_z1 + room_z1) / 2.0))
	var above_door_height := floor_top - (floor_bottom + door_height)
	_add_box_collision(body, Vector3(WALL_THICKNESS, above_door_height, door_z1 - door_z0), Vector3(wall_a_x, floor_top - above_door_height / 2.0, (door_z0 + door_z1) / 2.0))
	_add_box_collision(body, Vector3(room_x1 - room_x0, floor_height, WALL_THICKNESS), Vector3((room_x0 + room_x1) / 2.0, (floor_bottom + floor_top) / 2.0, room_z1))

## One straight flight of stairs inside a floor's own stairwell footprint --
## TALLEST at the wall end (where it meets the hole leading to the next
## floor), down to a short first step on the open-room side a player
## actually approaches from. (Built the other way around -- tall end facing
## the open room -- a player walking toward the stairs meets a shoulder-
## height wall of stair blocks first, with the actual low, climbable step
## hidden behind it against the wall: reads as the staircase facing
## backwards, into the wall, instead of rising to meet the player.)
## Visually a stack of solid boxes -- each step's own riser reaches all the
## way down to this floor's own floor (not just its own tread depth), so
## there's no gap underneath to see through -- but collision is a single
## continuous sloped ramp rather than individual step boxes, so
## move_and_slide climbs it smoothly instead of catching on every step's
## own front edge.
##
## `wall_point` (the flight's own tall/near-wall end center), `run_dir`
## (unit vector the flight extends along, away from that wall) and
## `width_dir` (unit vector along the flight's own width, i.e. the side
## wall it hugs) all come directly from _stairwell_for_floor -- taken as
## ready-made vectors rather than re-deriving them here from some more
## compact encoding (an angle, a side index, ...): an earlier version DID
## re-derive them from a "quarter_turns" int using its own separate copy of
## the same rotation formula, which drifted out of sync with
## _stairwell_for_floor's own version of that formula for 2 of the 4
## cases, confirmed directly as stairs jutting outside the building's own
## exterior walls in a real screenshot. Taking the exact same vectors
## _stairwell_for_floor already computed makes that class of mismatch
## structurally impossible, not just less likely.
##
## Step count is derived from `rise` and STAIR_STEP_HEIGHT rather than a
## fixed number, so a short flight and a tall one both get realistically-
## sized individual steps instead of the same step count stretched or
## squashed to fit -- which could mean dozens of steps for a tall flight
## (up to ~65 for the tallest possible top-floor rise), so they're drawn
## as one MultiMeshInstance3D (one instanced draw call) instead of one
## MeshInstance3D per step, to keep a single staircase from ever costing
## more scene-tree nodes than the old fixed-6-steps version did.
static func _add_staircase(body: StaticBody3D, wall_point: Vector3, run_dir: Vector3, width_dir: Vector3, run: float, y0: float, rise: float, width: float, mat: Material) -> void:
	var step_count := maxi(2, roundi(rise / STAIR_STEP_HEIGHT))
	var step_run := run / float(step_count)
	# -width_dir, not width_dir: (width_dir, UP, run_dir) is consistently a
	# LEFT-handed basis (determinant -1) for all 4 of _stairwell_for_floor's
	# corners, since hug_dir there is chosen to point at whichever side
	# wall that corner actually hugs, not to keep this basis right-handed.
	# A box mesh's own geometry is symmetric under mirroring its local
	# width axis (scaling by `width` doesn't care which way that axis
	# points), so flipping it here to force a proper (determinant +1)
	# rotation is free -- it doesn't change the box's rendered shape or
	# position -- while avoiding a real risk a reflected transform would
	# otherwise carry: Godot's backface culling reads winding order after
	# the full transform, so every face of a mirrored box would flip which
	# side reads as "front," which could cull the faces that should
	# actually be visible.
	var box_basis := Basis(-width_dir, Vector3.UP, run_dir)

	var multimesh := MultiMesh.new()
	multimesh.transform_format = MultiMesh.TRANSFORM_3D
	var unit_box := BoxMesh.new()
	unit_box.size = Vector3.ONE
	multimesh.mesh = unit_box
	multimesh.instance_count = step_count
	for i in range(step_count):
		var step_height := rise * float(step_count - i) / float(step_count)
		var t := step_run * i + step_run / 2.0
		var pos := wall_point + run_dir * t
		pos.y = y0 + step_height / 2.0
		var xform := Transform3D(box_basis * Basis().scaled(Vector3(width, step_height, step_run)), pos)
		multimesh.set_instance_transform(i, xform)

	var mmi := MultiMeshInstance3D.new()
	mmi.multimesh = multimesh
	mmi.material_override = mat
	body.add_child(mmi)

	var ramp_length := Vector2(run, rise).length()
	var collision := CollisionShape3D.new()
	var box_shape := BoxShape3D.new()
	box_shape.size = Vector3(width, WALL_THICKNESS, ramp_length)
	collision.shape = box_shape
	var ramp_center := wall_point + run_dir * (run / 2.0)
	ramp_center.y = y0 + rise / 2.0
	collision.position = ramp_center
	collision.basis = box_basis * Basis(Vector3.RIGHT, atan2(rise, run))
	body.add_child(collision)

## How many whole window cells fit across a dimension, and the actual
## per-cell size that results -- always evenly divides `dimension`
## (remainder 0), so an edge always lands exactly on a cell boundary
## instead of slicing through the middle of a window. Snaps to the
## nearest whole count for `nominal`-sized cells, so the real cell size
## only ever drifts a little from the nominal target.
static func _cell_layout(dimension: float, nominal: float) -> Dictionary:
	var count := maxi(1, roundi(dimension / nominal))
	return {"count": count, "size": dimension / float(count)}

## Builds one building's exterior shell into `st`: 4 walls (window-tiled,
## with a walk-through doorway cut into the front wall -- see
## _add_wall_panel for how the (up to) 3 pieces a door leaves behind still
## get a clean, uncut, ROW-ALIGNED window grid) and a flat roof cap with a
## stairwell-sized hole in it (see _add_floor_with_hole) so the top
## interior floor's own staircase can actually reach the roof. Ground-
## relative (y from 0 at the floor to size.y at the roof), not centered on
## the building's own half-height, to match _build_interior's own
## coordinate space directly. Winding is auto-corrected per panel the same
## way Character's box builder does -- see character.gd for why the sign
## of that check matters.
##
## `v_layout` (the floor-row grid) is computed ONCE by the caller (see
## _build_building) and shared by every panel on every wall, rather than
## each panel fitting its own -- the door's own height is already snapped
## to a whole number of this SAME grid's rows (see DOOR_HEIGHT_FLOORS), so
## sharing it here is what keeps the windows directly above the door at
## the same height as their neighbors instead of floating in their own,
## separately-gridded column. `stairwell` is the TOP interior floor's own
## footprint (see _stairwell_for_floor/_build_interior), so the roof's own
## hole lines up exactly above that floor's own ceiling hole regardless of
## which side wall it happens to have landed on.
## The 4 wall definitions (normal/u0/u1/to_world, in this building's own
## local space, "u" running along each wall's own horizontal extent and "y"
## shared vertical height 0..size.y) -- shared between _add_building_box
## (the visual mesh) and building_glass.gd's own setup() (breakable-glass
## hit-testing/geometry), pulled out into one place specifically so the two
## can never drift out of sync with each other. Only the door wall
## (has_door=true) carries a door split -- building_glass.gd's own setup()
## excludes just the row-0 cells overlapping the door's own rectangle from
## that one wall (see its own class comment), not the whole wall.
static func _wall_defs(hx: float, hz: float) -> Array:
	return [
		{"normal": Vector3(1, 0, 0), "u0": -hz, "u1": hz, "to_world": func(u, y): return Vector3(hx, y, u)},
		{"normal": Vector3(-1, 0, 0), "u0": -hz, "u1": hz, "to_world": func(u, y): return Vector3(-hx, y, u)},
		{"normal": Vector3(0, 0, -1), "u0": -hx, "u1": hx, "to_world": func(u, y): return Vector3(u, y, -hz)},
		{"normal": Vector3(0, 0, 1), "u0": -hx, "u1": hx, "to_world": func(u, y): return Vector3(u, y, hz), "has_door": true},
	]

## glass_band_height: the bottom slice of the 3 plain (non-door) walls
## handed off to building_glass.gd's own real glass pane instead (see its
## own class comment) -- skipped ENTIRELY here (a real geometric gap, not
## just textured to look like a window) rather than drawn as ordinary solid
## wall underneath it. A solid wall behind the glass was a real reported
## bug: since the wall's own window-grid texture already painted a dark
## rectangle right where a real window sits, an opaque surface directly
## behind the (correctly transparent) glass pane meant "you cant see the
## outside or inside through them" even though the glass material itself
## was genuinely see-through -- there was simply always solid geometry
## immediately behind it. That same coincident-plane stacking (glass pane,
## painted-but-solid facade wall, and _build_interior's own separate
## interior-facing wall, all at virtually the same Y/Z position) is also
## what read as the jagged diagonal z-fighting artifact reported directly
## from a screenshot. 0.0 disables this (a wall too short to have any
## breakable rows) and every wall draws full-height exactly as before.
static func _add_building_box(st: SurfaceTool, size: Vector3, door: Dictionary, v_layout: Dictionary, stairwell: Dictionary, glass_band_height: float) -> void:
	var hx := size.x / 2.0
	var hz := size.z / 2.0

	# Each wall is described in its own 2D "panel space": u_local runs
	# along the wall's horizontal extent, y_local is height (shared by
	# every wall, 0 at the floor to size.y at the roof). to_world converts
	# a wall's own (u_local, y_local) into this building's local 3D space
	# -- u_local's own direction/handedness doesn't need to be consistent
	# between walls, since winding is auto-corrected per panel regardless.
	var walls := _wall_defs(hx, hz)

	for wall in walls:
		var normal: Vector3 = wall["normal"]
		var u0: float = wall["u0"]
		var u1: float = wall["u1"]
		var to_world: Callable = wall["to_world"]
		if wall.get("has_door", false):
			var door_u0: float = door["u0"]
			var door_u1: float = door["u1"]
			var row_size: float = v_layout["size"]
			# The door only ever occupies row 0 (DOOR_HEIGHT_FLOORS is always
			# 1, see _build_building's own door dict) -- so only that one row
			# needs door-aware handling; every row above it is windowed
			# exactly like any other wall, full span, no door interaction.
			# See _add_windowed_wall_row_excluding's own comment for why
			# whole-cell exclusion against this wall's own natural grid
			# replaces the earlier narrower-segment approach that shipped a
			# real gap/seam regression twice.
			_add_windowed_wall_row_excluding(st, u0, u1, 0.0, row_size, to_world, normal, v_layout, door_u0, door_u1)
			if size.y > row_size:
				_add_glass_band_frame(st, u0, u1, row_size, size.y, v_layout, to_world, normal)
		else:
			if size.y > glass_band_height:
				_add_wall_panel(st, u0, u1, glass_band_height, size.y, to_world, normal, v_layout)
			# The breakable-glass band itself: real per-WINDOW holes (see
			# _add_glass_band_frame's own comment), not the plain textured
			# panel above -- building_glass.gd's own pane fills each hole.
			_add_glass_band_frame(st, u0, u1, 0.0, minf(glass_band_height, size.y), v_layout, to_world, normal)

	_add_floor_with_hole(st, -hx, hx, -hz, hz, stairwell["hole_x0"], stairwell["hole_x1"], stairwell["hole_z0"], stairwell["hole_z1"], size.y, Vector3(0, 1, 0))

## Real reported bug this fixes: an earlier version of the breakable-glass
## band simply omitted the WHOLE row's own wall geometry for the entire
## width of the wall, leaving one single wall-wide opening -- correct for
## making the glass see-through, but it also erased every mullion/frame
## between individual windows, reading as "the whole wall is one big glass
## panel" instead of a normal textured wall with individual windows (only
## the actual glass band looked wrong this way -- the untouched rows above
## it, still drawn by the ordinary _add_wall_panel, never had this problem).
## Punches a real hole at each individual window CELL's own inset
## rectangle instead (same WINDOW_MARGIN_RATIO margin build_facade_
## material's own texture already uses, just applied to real geometry
## here) -- solid, textured frame/mullion geometry fills the rest of each
## cell, so the band reads as a normal window-tiled wall again, with only
## the actual window openings left for building_glass.gd's own real,
## breakable pane to fill. Iterates whole v_layout rows (region_y1 -
## region_y0 is always an exact multiple of v_layout's own row size, see
## glass_rows' own comment in _build_building) so every row gets the
## identical treatment. region_y0/region_y1: see
## _add_glass_band_frame_collision's own identical parameter for why this
## doesn't just assume the band starts at y=0.
static func _add_glass_band_frame(st: SurfaceTool, u0: float, u1: float, region_y0: float, region_y1: float, v_layout: Dictionary, to_world: Callable, normal: Vector3) -> void:
	var row_size: float = v_layout["size"]
	var rows := int(round((region_y1 - region_y0) / row_size))
	for r in range(rows):
		var y0 := region_y0 + float(r) * row_size
		_add_windowed_wall_row(st, u0, u1, y0, y0 + row_size, to_world, normal, v_layout)

## Interior counterpart to _add_glass_band_frame -- same per-cell hole
## layout, but flat-colored (interior walls have no window texture at all,
## see _build_interior's own class comment) instead of UV-mapped against
## the facade texture.
static func _add_flat_glass_band_frame(st: SurfaceTool, u0: float, u1: float, region_y0: float, region_y1: float, row_size: float, to_world: Callable, normal: Vector3) -> void:
	var rows := int(round((region_y1 - region_y0) / row_size))
	for r in range(rows):
		var y0 := region_y0 + float(r) * row_size
		_add_flat_windowed_wall_row(st, u0, u1, y0, y0 + row_size, to_world, normal)

## Flat-colored counterpart to _add_windowed_wall_row -- same per-cell hole
## layout, emitted via _add_flat_quad instead of the facade-textured quad.
static func _add_flat_windowed_wall_row(st: SurfaceTool, u0: float, u1: float, y0: float, y1: float, to_world: Callable, normal: Vector3) -> void:
	var u_layout := _cell_layout(u1 - u0, NOMINAL_COLUMN_METERS)
	var cell_width: float = u_layout["size"]
	for i in range(int(u_layout["count"])):
		var col_u0 := u0 + float(i) * cell_width
		_add_flat_window_cell(st, col_u0, col_u0 + cell_width, y0, y1, to_world, normal)
	# _cell_layout always evenly divides u1-u0 by construction (count*size
	# == the full span, zero remainder) -- no leftover strip is possible
	# here the way the door wall's own now-removed cell_width_override path
	# used to leave one (see _add_windowed_wall_row_excluding's own comment
	# on how that whole class of bug is avoided now).

## One cell's own flat-colored frame pieces around its inset window hole.
static func _add_flat_window_cell(st: SurfaceTool, col_u0: float, col_u1: float, y0: float, y1: float, to_world: Callable, normal: Vector3) -> void:
	var cell_width := col_u1 - col_u0
	var row_height := y1 - y0
	var margin_u := cell_width * WINDOW_MARGIN_RATIO
	var margin_v := row_height * WINDOW_MARGIN_RATIO
	var win_u0 := col_u0 + margin_u
	var win_u1 := col_u1 - margin_u
	var win_y0 := y0 + margin_v
	var win_y1 := y1 - margin_v
	if win_u0 > col_u0:
		_add_flat_quad(st, _panel_corners(col_u0, win_u0, y0, y1, to_world), normal)
	if col_u1 > win_u1:
		_add_flat_quad(st, _panel_corners(win_u1, col_u1, y0, y1, to_world), normal)
	if win_y0 > y0:
		_add_flat_quad(st, _panel_corners(win_u0, win_u1, y0, win_y0, to_world), normal)
	if y1 > win_y1:
		_add_flat_quad(st, _panel_corners(win_u0, win_u1, win_y1, y1, to_world), normal)

## Flat-colored counterpart to _add_windowed_wall_row_excluding -- see its
## own comment. Used for the door wall's own INTERIOR-facing ground row.
static func _add_flat_windowed_wall_row_excluding(st: SurfaceTool, u0: float, u1: float, y0: float, y1: float, to_world: Callable, normal: Vector3, exclude_u0: float, exclude_u1: float) -> void:
	var u_layout := _cell_layout(u1 - u0, NOMINAL_COLUMN_METERS)
	var cell_width: float = u_layout["size"]
	for i in range(int(u_layout["count"])):
		var col_u0 := u0 + float(i) * cell_width
		var col_u1 := col_u0 + cell_width
		if col_u1 > exclude_u0 and col_u0 < exclude_u1:
			# This cell overlaps the door -- solid on whichever side(s)
			# aren't the door's own opening; [exclude_u0, exclude_u1] itself
			# gets NO geometry at all (the real walkthrough opening).
			if col_u0 < exclude_u0:
				_add_flat_quad(st, _panel_corners(col_u0, exclude_u0, y0, y1, to_world), normal)
			if col_u1 > exclude_u1:
				_add_flat_quad(st, _panel_corners(exclude_u1, col_u1, y0, y1, to_world), normal)
		else:
			_add_flat_window_cell(st, col_u0, col_u1, y0, y1, to_world, normal)

## One row's own worth of window cells, each cell missing its own inset
## window rectangle (a real hole) but otherwise framed by up to 4 solid
## textured pieces -- same "pieces around a rectangular cutout" shape
## _add_floor_with_hole already establishes for a horizontal slab, just
## applied to a vertical wall row instead.
static func _add_windowed_wall_row(st: SurfaceTool, u0: float, u1: float, y0: float, y1: float, to_world: Callable, normal: Vector3, v_layout: Dictionary) -> void:
	var u_layout := _cell_layout(u1 - u0, NOMINAL_COLUMN_METERS)
	var cell_width: float = u_layout["size"]
	for i in range(int(u_layout["count"])):
		var col_u0 := u0 + float(i) * cell_width
		_add_window_cell(st, col_u0, col_u0 + cell_width, y0, y1, u0, u_layout, v_layout, to_world, normal)
	# _cell_layout always evenly divides u1-u0 by construction -- see
	# _add_flat_windowed_wall_row's own identical note.

## One cell's own textured frame pieces around its inset window hole --
## factored out of _add_windowed_wall_row so _add_windowed_wall_row_excluding
## (below) can build the identical frame/margin treatment for the cells it
## doesn't exclude, without duplicating the margin math.
static func _add_window_cell(st: SurfaceTool, col_u0: float, col_u1: float, y0: float, y1: float, panel_u0: float, u_layout: Dictionary, v_layout: Dictionary, to_world: Callable, normal: Vector3) -> void:
	var cell_width := col_u1 - col_u0
	var row_height := y1 - y0
	var margin_u := cell_width * WINDOW_MARGIN_RATIO
	var margin_v := row_height * WINDOW_MARGIN_RATIO
	var win_u0 := col_u0 + margin_u
	var win_u1 := col_u1 - margin_u
	var win_y0 := y0 + margin_v
	var win_y1 := y1 - margin_v
	if win_u0 > col_u0:
		_add_windowed_wall_piece(st, col_u0, win_u0, y0, y1, panel_u0, u_layout, v_layout, to_world, normal)
	if col_u1 > win_u1:
		_add_windowed_wall_piece(st, win_u1, col_u1, y0, y1, panel_u0, u_layout, v_layout, to_world, normal)
	if win_y0 > y0:
		_add_windowed_wall_piece(st, win_u0, win_u1, y0, win_y0, panel_u0, u_layout, v_layout, to_world, normal)
	if y1 > win_y1:
		_add_windowed_wall_piece(st, win_u0, win_u1, win_y1, y1, panel_u0, u_layout, v_layout, to_world, normal)

## Same per-cell "frame + real hole" treatment _add_windowed_wall_row builds,
## but any cell whose own [col_u0, col_u1] span overlaps [exclude_u0,
## exclude_u1] renders SOLID on whichever side(s) of it aren't the excluded
## range (still texture-aligned via the same shared u_layout/panel_u0
## reference _add_windowed_wall_piece already uses for frame pieces, so it
## doesn't read as a different-looking patch) instead of a window --
## [exclude_u0, exclude_u1] itself gets NO geometry at all in that cell,
## a true opening. Used ONLY for the door wall's own ground row, where the
## excluded range is the door's own opening, handled by building_door.gd.
##
## Deliberately uses the SAME natural, un-overridden _cell_layout the rest
## of this wall (and every other wall) already uses -- NOT a cell width
## independently fitted to just the door-flanking remainder. That's what
## two earlier real-hole attempts on this exact wall did, and both shipped
## a visible gap/seam: a narrow remainder span forced to fit a DIFFERENT
## cell width than the rest of the building rarely divides evenly, leaving
## a leftover fractional strip that needed its own separate cover-up patch.
## Classifying whole cells from ONE uniform grid instead is gap-free by
## construction outside the door itself: every cell across the full u0..u1
## span is either a window or (door-adjacent) solid, the two together
## always sum to exactly u1-u0 minus the door's own exact width -- no
## leftover strip to patch. A window-cell immediately beside the door may
## end up replaced by a solid one up to a whole column wider than the
## door's own literal DOOR_WIDTH (whenever the door's own centered edges
## don't land exactly on a natural cell boundary) -- a slightly wider blank
## recess around the entrance, not a gap or a mismatched-size seam.
static func _add_windowed_wall_row_excluding(st: SurfaceTool, u0: float, u1: float, y0: float, y1: float, to_world: Callable, normal: Vector3, v_layout: Dictionary, exclude_u0: float, exclude_u1: float) -> void:
	var u_layout := _cell_layout(u1 - u0, NOMINAL_COLUMN_METERS)
	var cell_width: float = u_layout["size"]
	for i in range(int(u_layout["count"])):
		var col_u0 := u0 + float(i) * cell_width
		var col_u1 := col_u0 + cell_width
		if col_u1 > exclude_u0 and col_u0 < exclude_u1:
			if col_u0 < exclude_u0:
				_add_windowed_wall_piece(st, col_u0, exclude_u0, y0, y1, u0, u_layout, v_layout, to_world, normal)
			if col_u1 > exclude_u1:
				_add_windowed_wall_piece(st, exclude_u1, col_u1, y0, y1, u0, u_layout, v_layout, to_world, normal)
		else:
			_add_window_cell(st, col_u0, col_u1, y0, y1, u0, u_layout, v_layout, to_world, normal)

## One frame piece's own quad -- UV-mapped by the EXACT same formula
## _add_wall_panel's own quad uses (same panel_u0/u_layout/v_layout
## reference, not values re-derived for just this smaller piece) so the
## texture lines up seamlessly with the untouched panel directly above/
## beside it, with no visible seam at the boundary.
static func _add_windowed_wall_piece(st: SurfaceTool, u0: float, u1: float, y0: float, y1: float, panel_u0: float, u_layout: Dictionary, v_layout: Dictionary, to_world: Callable, normal: Vector3) -> void:
	var corners_2d := [Vector2(u0, y0), Vector2(u1, y0), Vector2(u1, y1), Vector2(u0, y1)]
	var quad := []
	for c2 in corners_2d:
		var u: float = (c2.x - panel_u0) / (float(TEX_COLS) * float(u_layout["size"]))
		var v: float = c2.y / (float(TEX_ROWS) * float(v_layout["size"]))
		quad.append({"pos": to_world.call(c2.x, c2.y), "uv": Vector2(u, v)})
	_emit_quad(st, quad, normal)

## Emits one flat wall panel -- a rectangle in the wall's own 2D "panel
## space" (u0..u1 along the wall, y0..y1 vertically). Fits its OWN
## independently-sized window grid ALONG U (see _cell_layout) so this
## panel's own left/right edges always land on a column boundary -- but
## uses the SHARED `v_layout` every panel on this wall was given (not one
## fitted to just this panel's own y0..y1 span) for V, measured from the
## wall's own absolute bottom (y=0), not this panel's own y0. A panel
## above a door doesn't start at y=0, so fitting ITS OWN row grid to just
## its own (shorter) height would tile windows at a different spacing --
## and starting rows fresh from its own y0 would offset them vertically
## too -- either way reading as a visibly separate, misaligned column
## right above the door instead of a continuation of the same floors on
## either side of it.
static func _add_wall_panel(st: SurfaceTool, u0: float, u1: float, y0: float, y1: float, to_world: Callable, normal: Vector3, v_layout: Dictionary) -> void:
	var u_layout := _cell_layout(u1 - u0, NOMINAL_COLUMN_METERS)

	var corners_2d := [Vector2(u0, y0), Vector2(u1, y0), Vector2(u1, y1), Vector2(u0, y1)]
	var quad := []
	for c2 in corners_2d:
		var u: float = (c2.x - u0) / (float(TEX_COLS) * float(u_layout["size"]))
		var v: float = c2.y / (float(TEX_ROWS) * float(v_layout["size"]))
		quad.append({"pos": to_world.call(c2.x, c2.y), "uv": Vector2(u, v)})

	_emit_quad(st, quad, normal)

## World-space corners of a flat (u0..u1, y0..y1) rectangle in some wall's
## own panel space -- shared helper for the plain, windowless surfaces
## (roof, interior walls/ceiling) that just need the positions, not a
## window-tiled UV layout.
static func _panel_corners(u0: float, u1: float, y0: float, y1: float, to_world: Callable) -> Array:
	return [to_world.call(u0, y0), to_world.call(u1, y0), to_world.call(u1, y1), to_world.call(u0, y1)]

## A single-color flat quad with no window tiling -- used for the roof cap
## and every interior surface, which just sample a flat, windowless corner
## of whichever material they're given.
static func _add_flat_quad(st: SurfaceTool, corners: Array, normal: Vector3) -> void:
	var quad := []
	for c in corners:
		quad.append({"pos": c, "uv": Vector2(0.03, 0.03)})
	_emit_quad(st, quad, normal)

## Shared tail end for both panel types: auto-corrects winding against
## `normal` (same approach Character's box builder uses -- see
## character.gd for why the sign of this check matters), then emits the
## quad as 2 triangles.
static func _emit_quad(st: SurfaceTool, quad: Array, normal: Vector3) -> void:
	var geo_normal: Vector3 = (quad[1]["pos"] - quad[0]["pos"]).cross(quad[2]["pos"] - quad[0]["pos"]).normalized()
	if geo_normal.dot(normal) > 0.0:
		quad.reverse()

	for tri in [[0, 1, 2], [0, 2, 3]]:
		for idx in tri:
			st.set_normal(normal)
			st.set_uv(quad[idx]["uv"])
			st.add_vertex(quad[idx]["pos"])

static func _add_roof_detail(parent: Node3D, roof: String, size: Vector3, body_color: Color) -> void:
	var top_y := size.y
	match roof:
		"shed":
			var shed := MeshInstance3D.new()
			var bm := BoxMesh.new()
			bm.size = Vector3(1.1, 0.6, 1.1)
			shed.mesh = bm
			var mat := StandardMaterial3D.new()
			# Matches THIS building's own picked facade color (not a fixed
			# constant) -- a rooftop utility shed is ordinarily built from the
			# same facade material as the rest of the building.
			mat.albedo_color = body_color
			shed.material_override = mat
			shed.position = Vector3(0, top_y + 0.3, 0)
			parent.add_child(shed)
		"antenna":
			_add_antenna(parent, Vector3(0, top_y, 0), 2.0)
		"chimney":
			var chim := MeshInstance3D.new()
			var bm := BoxMesh.new()
			bm.size = Vector3(0.9, 1.2, 0.9)
			chim.mesh = bm
			var mat := StandardMaterial3D.new()
			mat.albedo_color = Color(0.85, 0.87, 0.92)
			chim.material_override = mat
			chim.position = Vector3(0, top_y + 0.6, 0)
			parent.add_child(chim)
			_add_antenna(parent, Vector3(0.3, top_y + 1.2, 0), 1.5)
		"parapet":
			_add_parapet(parent, size, body_color)
		"water_tower":
			_add_water_tower(parent, Vector3(0, top_y, 0))

## A thin rim wall running around all 4 edges of the flat roof cap --
## matches THIS building's own facade color (like "shed" already does)
## rather than a fixed constant, since it reads as part of the same
## building's own roofline, not a separate prop sitting on top of it.
static func _add_parapet(parent: Node3D, size: Vector3, body_color: Color) -> void:
	var top_y := size.y
	var hx := size.x / 2.0
	var hz := size.z / 2.0
	var h := 0.5
	var t := 0.15
	var mat := StandardMaterial3D.new()
	mat.albedo_color = body_color
	mat.roughness = 0.9
	mat.metallic_specular = 0.0 # see build_facade_material's own comment
	var edges := [
		[Vector3(size.x, h, t), Vector3(0, top_y + h / 2.0, -hz + t / 2.0)],
		[Vector3(size.x, h, t), Vector3(0, top_y + h / 2.0, hz - t / 2.0)],
		[Vector3(t, h, size.z), Vector3(-hx + t / 2.0, top_y + h / 2.0, 0)],
		[Vector3(t, h, size.z), Vector3(hx - t / 2.0, top_y + h / 2.0, 0)],
	]
	for edge in edges:
		var mesh_inst := MeshInstance3D.new()
		var bm := BoxMesh.new()
		bm.size = edge[0]
		mesh_inst.mesh = bm
		mesh_inst.material_override = mat
		mesh_inst.position = edge[1]
		parent.add_child(mesh_inst)

## A rooftop water tank on 4 thin legs -- a fixed weathered-tank color (not
## a FACADE_VARIANTS entry), same precedent as the antenna/chimney's own
## fixed accent colors right above.
static func _add_water_tower(parent: Node3D, base_pos: Vector3) -> void:
	var leg_height := 1.4
	var tank_radius := 0.9
	var tank_height := 1.3
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.35, 0.28, 0.22)
	mat.roughness = 0.85
	mat.metallic_specular = 0.0

	for dx in [-0.55, 0.55]:
		for dz in [-0.55, 0.55]:
			var leg := MeshInstance3D.new()
			var cm := CylinderMesh.new()
			cm.top_radius = 0.05
			cm.bottom_radius = 0.05
			cm.height = leg_height
			leg.mesh = cm
			leg.material_override = mat
			leg.position = base_pos + Vector3(dx, leg_height / 2.0, dz)
			parent.add_child(leg)

	var tank := MeshInstance3D.new()
	var tank_mesh := CylinderMesh.new()
	tank_mesh.top_radius = tank_radius
	tank_mesh.bottom_radius = tank_radius
	tank_mesh.height = tank_height
	tank.mesh = tank_mesh
	tank.material_override = mat
	tank.position = base_pos + Vector3(0, leg_height + tank_height / 2.0, 0)
	parent.add_child(tank)

static func _add_antenna(parent: Node3D, base_pos: Vector3, height: float) -> void:
	var spike := MeshInstance3D.new()
	var cm := CylinderMesh.new()
	cm.top_radius = 0.03
	cm.bottom_radius = 0.05
	cm.height = height
	spike.mesh = cm
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.05, 0.05, 0.05)
	spike.material_override = mat
	spike.position = base_pos + Vector3(0, height / 2.0, 0)
	parent.add_child(spike)
