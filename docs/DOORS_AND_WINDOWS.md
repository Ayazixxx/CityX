# Doors and windows: what the research said, and what changed

Asked (2026-09-26): "research window and window breaking mechanics and implement what you learn, also the
door opening mechanics feel bad as well, research door opening mechanics in third person games."

## What the research found

Sources actually read: the Level Design Book's chapter on doors, Liz England's "The Door Problem", and search
summaries of write-ups on *The Last of Us Part II*'s windows and door handling and on glass sound design. (Two
of the longer articles blocked the fetch, so those points come from the search summaries only.)

**Doors**
- A rotating door is easily blocked and sweeps a lot of room; it needs states beyond open/closed --
  Opening, Closing and **Blocked** (what if the player or an NPC is in the way?).
- For convenience, rotating doors should be **double-hinged, opening in both directions, away from the
  character** opening them; "err on the side of player convenience, even if it's unrealistic."
- Sound (and animation) want hooks at the start and the end of a swing.
- Third-person doors are wider than real ones so the camera and the character's kit fit. In The Last of Us
  Part II your **speed decides how you go through**: jogging bursts through, sneaking eases it open.
- Leave doors as the player left them to show where they have been; auto-close is for tense combat levels.

**Windows**
- *The Last of Us Part II* differentiates the blow: a melee strike and a thrown brick apply different forces,
  and throwables, bullets, melee and explosions were each tested to feel right. Bricks open new routes.
- Glass sound is a **sequence**, not one sample: the contact, then the crack or shatter, then the tinkle of
  pieces falling, with the pane's thickness deciding the character (thin is bright and quick, thick is low
  and drawn out).

## What was wrong here

- **Doors**: always swung INWARD, so from inside they swung into you, and the kinematic leaf then shoved you;
  E worked on any door within 2.5m (behind you, through a wall, or from across the map if an NPC was near);
  a fixed 0.5s ease you could not reverse; no sound; nothing stopped it closing on someone.
- **Windows**: only a punch or a bullet could break one (both identically: crack, then shatter on the second
  hit); a sword swung through a window did nothing; a thrown bottle bounced off; shards always flew OUTWARD
  toward whoever hit it; one sample, no tinkle.

## What changed

**Doors** (`View/BuildingDoor.cs`, `View/DoorSounds.cs`, `View/PositionalSound.cs`)
- Swings **away from whoever opens it**: from the street it opens inward, from the lobby outward
  (`BuildingDoor.SwingSignFor`). An NPC's `SetOpen` keeps the old inward swing.
- A **damped spring** instead of a timed ease: it starts quickly and settles, and E mid-swing turns it round
  smoothly.
- **Never pushes or traps anyone**: while the leaf is moving it holds still against a person AHEAD of it
  (someone on the side it is swinging away from -- the opener, pressed flush to the door -- never blocks it),
  and continues when they move. E refuses to shut it while someone stands in the doorway.
- **Walking into it opens it**, away from you; how fast you were going decides how (eased at a walk, thrown
  open at a run: 0.07s spring instead of 0.14s).
- **E only works on the door you are looking at** (within 65 degrees of the camera's view) and only for the
  local player.
- **Sound**: a latch and creak as it opens, a thud as it meets the frame, louder the harder it shut. Made in
  code (`DoorSounds`), so the game gains no audio files.
- Not done: a character reach/push animation (there is no such clip), auto-close, locked doors.

**Windows** (`World/GlassDamage.cs`, `View/GlassBreaker.cs`, `Character/SoulsSwordCombat.cs`,
`Character/PlayerLocomotion.cs`, `Weapons/ThrownBottle.cs`)
- Blows have a **strength**. A fist or a bullet is *light* (a whole pane cracks, a second blow shatters it,
  as before). A **sword swing** (the blade is traced through space each active frame, so a fast swing cannot
  skip over a pane), a **thrown bottle** (4 m/s or faster) and (running or diving into an INTACT pane no longer breaks it: removed 2026-10-05 by request; a CRACKED pane still gives way to a knock) a ~~sprint or jetpack dive at 6.5 m/s or
  faster** are *heavy*: the pane goes at once. Walking into glass does nothing.
- **Shards fly the way the blow went** (through the pane): someone outside sends glass INTO the building,
  someone inside sends it out over the street (`GlassBreaker.ShardDirection`).
- **Layered sound**: the crack or shatter with a little pitch variation (lower for a heavy blow), and on a
  shatter the same sample again a moment later, higher and quieter, as the tinkle -- no second recording.
- Not done: cut damage or a vault animation when passing through a broken window; noise that alerts NPCs;
  thick versus thin panes.

## Tests

`BuildingDoorPlayTests` (13: swing side, spring feel, reversal, blocking, flush-to-the-door, doorway, bump,
run versus walk, look-at, NPC `SetOpen`, restored-open), `GlassBlowTests` (6), `GlassBlowsPlayTests` (4: sword
through glass, wind-up breaks nothing, sprinting into an intact window breaks nothing). The look and sound of the door and the
glass are not something these can judge; they have not been seen or heard in the game.

## Update 2026-09-26 (later): every bullet makes its own hole

Reported: "only 1 bullet hole appears even if you shoot more than 1."

- A cracked pane is drawn without a solid collider (it has to be walkable at once), so the second bullet flew
  through the first bullet's hole and never found the glass. The trigger box a cracked pane's opening leaves
  (`CrackedPane`, now an `IBreakableSurface` and an `IShootableTrigger`) is the one thing standing where the
  glass is; bullets and the aim trace accept exactly those triggers and pass the shot on to `GlassBreaker`.
- A bullet is its own kind of blow (`BlowStrength.Bullet`): it adds a hole (`GlassDamage.Shoot`) and the pane
  stays cracked -- see the 2026-09-27 update below for why a bullet no longer shatters it at all. A fist still
  cracks and then shatters; a blade still takes a pane out at once. Holes are stored per pane, in order.
- The mesh cuts a real opening at every hole (`AddPaneWithHoles`: strips at each hole's edges, glass only where
  there is no hole, so overlapping holes cost nothing extra) and a crack decal at each.
- `BulletHoleDamageTests` (8), two in `CityMeshBuilderTests`, and `BulletHolesInRealWindowsPlayTests` (the real
  pistol, six shots at one real window, then the seventh). Making the pistol skip triggers again fails it with
  "bullet 2 ... flew through the first one".

## Update 2026-09-27: the visible square around a hole, and bullets no longer shatter a window

Reported: "the bullet holes have a visible square around them due to the image i used" and, separately,
"windows shattering by themself after being shot -- should only shatter when an object or player knocks into
the already shattered window."

**The square** turned out to be two real bugs, found by rendering the same pane with and without each fix and
diffing pixels, and by rendering the same pane by day and by night:
- The crack decal used the LIT shader, so it went nearly invisible at night (little ambient light for a lit
  material to catch) -- what showed through was the plain, hard-edged rectangle actually cut into the glass
  mesh (`AddPaneWithHoles`), unmasked because the decal meant to cover it had gone dark. Switched to Unlit with
  emission, so it reads the same brightness regardless of time of day (`EnsureCrackDecalMaterial`).
- Even with the decal visible, the real geometric cut was sized to EXACTLY match the decal's own see-through
  centre, leaving nothing standing between the hole's own straight edges/corners and whatever is behind the
  glass. `BulletHoleWidthFraction`/`HeightFraction` are now 60% of that measurement, so a ring of ordinary
  cracked glass -- itself still shown through the decal's transparency -- surrounds the real opening.
- `CityMassingSceneBuilder.CleanBulletHoleDecalTexture` (one-off, menu item) keeps only the largest connected
  alpha shape in `BulletHoleCrack.png` and premultiplies its RGB by alpha, so a texture-sampling emissive pass
  cannot light up a "transparent" pixel independently of its own alpha.

**Bullets never shatter a window any more.** Only something KNOCKING into a cracked pane does --
`CrackedPane.IsAKnock`: the character's own body or a genuinely simulated rigid body (never a kinematic limb
collider standing/aiming nearby, never debris) moving through the pane's plane at 1.5 m/s or more. The old rule
(any rigid body overlapping the opening's trigger box shattered it) fired on a character's own kinematic limb
colliders standing at the glass and on flying shards from a neighbouring shattered window. `GlassDamage
.MaxBulletHoles` is now the number of holes KEPT (12, oldest dropped for the newest), not a shatter threshold.

`BulletHoleDamageTests`/`BulletHolesInRealWindowsPlayTests` updated for the no-shatter-by-bullet rule;
`CrackedPaneKnockPlayTests` (7) covers the knock classification and a real streamed pane end to end.

## Research 2026-09-27: Source-style progressive glass, proposed (NOT implemented)

Asked: "research window breaking in video games. ideally it would be something like how glass breaks in
half life/ source games. research how to accomplish this in unity while working with what we've built so
far" and, separately, "not all glass will break the same the bottles and windows are gonna be separate
obviously." This section is a design only -- no code has changed yet.

**What Source actually does (`func_breakable_surf`).** A pane is a grid of small squares, each with its own
intact/broken state. A `Fragility` value decides how many break per hit. A broken square's neighbours pick a
jagged-edge texture based on which of THEIR neighbours are also broken -- the ragged look is texture-driven,
not extra geometry. Enough squares gone and whatever is left detaches and blows out ("unclean" breakoff: it
can stick to the frame or fall as a chunk, texture and physics both keyed off the same grid).

**What Unity's own Window Fracture package does instead**
(`github.com/Tiitan/GlassSystem`, made for *Receiver 2*): a real 2D fracture pattern is generated at the
impact point, clipped to the pane's outline, and each resulting polygon becomes a real extruded mesh shard.
A connectivity graph decides which shards still touch the frame (they stay) and which are cut off (they
fall). The pattern is picked by a seeded index + rotation, so it replicates over network as two integers
instead of a mesh.

**Which one fits here.** Real per-shard geometry (the Unity package's approach) is the wrong trade for this
project right now -- this session's whole 60fps pass found that DRAW CALLS, not triangle count, are the
actual cost (see [[perf_60fps_findings_2026_09_26]]), and hundreds of windows per building times dozens of
buildings in view means a geometry-per-shard system would reintroduce exactly the cost that was just cut.
Source's approach is state-driven and texture-driven, which matches how this project already draws a bullet
hole (a cut + a decal, no physics cost until the pane fully goes) -- so the proposal below is a Source-style
grid, built as an ADDITIVE layer on `GlassDamage`/`PaneKey`, not a replacement for the existing
Intact/Cracked/Shattered pipeline.

**Proposed shape**
- `PaneState` (Intact/Cracked/Shattered) stays exactly as it is and keeps deciding everything it decides
  today: the far-LOD path (`AddShellGlass`/`BuildBuildingShell`, which never needs per-bullet detail at
  range), the `CrackedPane` trigger box existing at all, the shatter sound and `GlassShardBurst` physical
  debris. The grid is new detail INSIDE the Cracked state, not a fourth state.
- A pane only gets a grid once it is Cracked (an Intact pane has no grid, same "not stored unless broken"
  convention `_broken`/`_impactPoints` already use -- see the class comment above, "ONLY BROKEN PANES ARE
  STORED"). A small fixed grid per pane (e.g. 3x3 or 4x4, picked from the pane's own aspect ratio) lives in
  a new `Dictionary<PaneKey, bool[]>`-shaped field alongside `_broken`/`_impactPoints`, behind the same
  `_gate` lock.
- A `Bullet` blow breaks the ONE cell nearest its impact UV (`FractionWithinPane` already maps a hit to a
  0..1 UV -- reuse it to pick a cell index) instead of today's "append another independent hole to a flat
  list." Repeated shots now visibly spread across the pane instead of just accumulating unrelated holes.
- A `Heavy` blow that lands on an already-Cracked pane breaks a small CLUSTER of cells around the impact
  (Source's "Fragility" idea) rather than nothing extra (today a second blow on a Cracked pane either does
  nothing or is the knock that clears it outright -- this gives it a visible, escalating in-between step).
- Once enough of a pane's cells are broken (a tunable fraction, e.g. 60%), the pane auto-promotes to
  Shattered through the EXISTING pipeline -- no new shatter/shard/sound code, just calling what `Hit`
  already calls when a knock finishes a pane. `CrackedPane.IsAKnock` is untouched and stays the primary,
  intentional way a pane clears (per the 2026-09-27 update above) -- the grid filling up is a secondary path
  for a pane that has simply been shot to pieces, not a way to bypass "only a knock shatters it" for a
  single well-aimed shot.
- Mesh side: `AddPaneWithHoles` already cuts multiple independent rectangular holes into one pane quad --
  extend it to accept the grid's list of broken CELL rectangles (fixed, grid-aligned) instead of free-form
  per-bullet UV rectangles it gets today. A broken cell whose live neighbour is still intact gets a crack
  decal along that shared edge (the same `AddCrackDecal`/`EnsureCrackDecalMaterial` recipe already built for
  the 2026-09-27 hole fix) -- this is the "jagged edge reflects neighbour state" trick, done with the
  existing decal system, not new geometry.

**Bottles stay separate -- already true today, confirmed by reading the code, and unaffected by this.**
`BottleWeapon`'s own `DurabilityState` (Intact/Broken/FullyBroken) is a melee hit-counter on the bottle prop
itself (`RegisterLandedMeleeHit`) -- it never touches `GlassDamage`/`PaneKey`/`WindowGrid` and decides only
the bottle's own damage and visual swap. `ThrownBottle.OnCollisionEnter` calls a window's `TakeBlow(...,
BlowStrength.Heavy)` exactly like a sword swing or a sprinting player -- from the window's side a thrown
bottle is just another Heavy blow and will go through the same grid-cluster logic above with no
special-casing. The bottle's own break effect (`GlassShatter`, a one-shot particle puff at the bottle's
impact point) is already a completely separate visual from the window's `GlassShardBurst` (real physical
shard debris) -- both already fire independently today. Nothing here changes any of that; the grid work is
scoped to window panes only.

**Not yet done.** No code has been written for this. Next steps when picked up: the grid dictionary and
cell-breaking logic in `GlassDamage` with focused unit tests (mirroring `GlassBlowTests`), the
`AddPaneWithHoles` extension with `CityMeshBuilderTests` coverage, then a real batch-rendered close-up
screenshot of a pane part-way through breaking before calling it done -- this is exactly the kind of visual
result a green test suite cannot judge on its own.

## Update: the fifth bullet shatters a window (2026-10-09)

Owner: "make the windows just shatter if a 5th bullet hits them". `GlassDamage.MaxBulletHoles` is 4 (was 12, with the pane never falling); the next
bullet (`ShatteringBullet`, 5) shatters it like a knock does -- shards, the break sound. Tests: `BulletHoleDamageTests.TheFifthBulletShattersTheWindow`,
`BulletHolesInRealWindowsPlayTests`.

## Update: E works the thing you look at; clear glass on real rooms (2026-10-09)

- Owner: "I keep accidentally opening and closing the door when I'm only trying to do the light switch, make it so it only interacts with the
  one you are looking at." A door (`BuildingDoor`) and the switches (`LightSwitchInteractor`) no longer act on E themselves: each OFFERS itself
  to `View/InteractionFocus` with how far off the crosshair it is, and after every Update has run only the nearest acts
  (`InteractionFocusRunner`, made on demand). Of several switches in reach, the one nearest the crosshair is the one offered. Tests:
  `InteractionFocusTests`.
- A window with a fitted-out room behind it is CLEAR glass (`SurfaceLook.PackGlass(clear)`, the glass shader's `_ClearAlpha` 0.4): the room
  shows through it, lit or dark. Glass with nothing behind it (a distant building's strips, a tower's sealed top) stays the old dark 0.72.
  See ROOM_LIGHTS.md (the lighting pass).
- Entrance canopies, sills, AC units, sign arms and parapet copings are closed boxes now (they had no underside and were see-through from below).

## Update 2026-10-15: a window breaks into its own pieces (and the answer on cost)

Owner: "research how we can improve the window fracturing. would procedurally window fracturing effect performance and be not worth it? find out
the answer to this question and implement or not while i sleep. either way improve upon the window fracturing system we have now."

**The answer: worth it, done the right way -- only for the pane that breaks, at the moment it breaks.** Measured on this PC (batch mode,
`GlassFractureTests.BreakingAPaneTakesMicroseconds`, `GlassFracturePlayTests.ABreakIsCheapAndAFullLoadOfFallingGlassIsCheapToSimulate`):

| What | Cost |
|---|---|
| Cutting a pane into its crack pattern | about 60 microseconds (35 pieces on average) |
| A whole break: pattern, 35 meshes, bodies placed | 0.3 to 0.4 ms on its frame (0.65 ms the first; the code is warmed while the city loads) |
| 160 pieces falling and tumbling at once (the cap) | 0.27 ms of physics a frame |

A 60 fps frame is 16.7 ms. The 2026-09-27 research above worried that real geometry per shard would bring back the draw calls the 60 fps
pass removed; that holds for giving every window shard geometry, not for this: an unbroken window is still part of its chunk's one merged mesh,
and a pane becomes pieces only when it breaks, for seven seconds, at most 160 pieces at a time, all with one material (the SRP batcher).

**What other games do.** Pre-fractured meshes are the cheap standard (the cost moves to authoring), but the cracks never line up with where
the blow landed; fracturing at run time costs more but puts them there. Glass, thin and brittle, breaks in radial cracks from the blow crossed
by concentric rings, and the number of pieces is what costs, so cap it and clear pieces away (Unigine's fracture documentation). Receiver 2's
Unity glass generates a 2D pattern at the impact, clips it to the pane and extrudes each piece. Pool the pieces and despawn settled debris
(general Unity destruction practice).

**What changed.**
- Before: the pane vanished and the same twelve small pre-made prisms (convex mesh colliders) popped out of the point it was hit, whatever its
  size.
- Now: `World/GlassFracture` (pure) cuts the pane's real rectangle (`View/GlassPaneFrame`: the cell less the frame's insets, a shop window's
  big glass on the entrance row, at the depth the blow landed) into 7 to 11 radial cracks from the impact crossed by three jittered rings
  (10, 30, 70 cm); a big pane's outer corners are cut across once or twice more; at most 56 pieces, covering the pane exactly.
- `View/GlassFractureBurst`: each piece a thin slab (8 mm) with a box collider and a body. The slivers round the blow are knocked through the
  way the blow went (4 m/s for a fist, 6.4 for a heavy blow, 2.4 for a bullet), the rings further out follow slower, and the big pieces on the
  frame hang on 0.1 to 0.8 s before they drop, as real glass does. Pooled (no allocation after the warm-up; waiting pieces stay switched on,
  parked far below the city -- switching a body and its collider on was most of a break's cost), never colliding with each other (layer 29),
  at most 160 alive (a new break takes the oldest), shrinking away after about 7 s. A round building's facets keep the old burst.
- The glass never gets in a body's way: a piece hanging in the frame is not solid until it drops (a body running through a window that
  has just broken passes through it), and bodies walk through pieces lying on the ground (their layer, `GlassFracture.ShardLayer`, is
  excluded from players' and passers-by's controllers). The camera ignores falling glass too. (In the first full test run a hanging piece
  held a body 0.58 m up and lying ones tripped it.)

Tests: `GlassFractureTests` (the pieces cover the pane exactly once, convex; slivers at the blow, big shards at the frame; the same seed breaks
the same way; a shop window is a few dozen pieces; the cut matches the glass the city drew for that window), `GlassFracturePlayTests` (the
pieces start in the pane, the slivers go through, the frame's pieces hang on, all land and are gone after their time; the pool makes no new
pieces for the next window and caps the total; a body runs through a window that has just broken and over its glass, unlifted; the cost above). Render: `GlassFractureCapture` (with -Graphics).

Sources: [Unigine: Fracture Body](https://developer.unigine.com/docs/latest/principles/physics/bodies/fracture/),
[Pre-fractured vs run-time fracture (Eurographics)](https://diglib.eg.org/bitstream/handle/10.2312/pt20111142/147-152.pdf),
[ShatteringTool (Unity thesis project)](https://github.com/Prommerbua/ShatteringTool),
[unity-mesh-fracture (pre-baked Voronoi cache)](https://github.com/sinanata/unity-mesh-fracture),
[How to fix destructible environment performance](https://bugnet.io/blog/how-to-fix-destructible-environment-performance),
[The Art of Destruction in Rainbow Six: Siege (GDC)](https://gdcvault.com/play/1023003/The-Art-of-Destruction-in).

## Update 2026-10-15: pairs of front doors

Owner: "make it so 15% percent of buildings have double doors as their main front entrance as opposed to them all being single doors". About one
ordinary building in seven (`BuildingInterior.HasDoubleEntrance`, 15%; not a sealed, roof-top, round or special one, nor one under 6.4 m wide) has a
2.4 m doorway (`EntranceWidth`) with two 1.2 m leaves (`BuildingDoor.PanelsFor`): the left hinged at the left jamb facing out, the right hinged at
the right jamb facing IN (a leaf hangs to the +X side of its hinge, so facing in it hangs back toward the middle), a thickness further in so both
stand in one plane. They are `Partner`s: walking into either, or E on either, opens or shuts both, each swinging away from whoever did it, one
latch sound for the pair; neither shuts on someone standing in the other's way. A pair restored open stands swung in (`Hang`'s `openFrom`).
The wall cut, the windows the doorway passes through (`GlassDamage.IsDoorCell`), the canopy, the fins, the lobby's light switch and the
entrance steps' landing all read the same width. Research: the Level Design Book (doors), Aliens vs. Predator 2's linked door pairs.
Tests: `EntranceDoorTests`, `BuildingDoorPlayTests.APair...`; `EntranceDoorCapture` renders a pair shut and open.

## Update 2026-10-15: doors onto the balconies

Owner: "make it so all balconies have a door leading between the inside of the building and the balcony, make it so the type of door varies by
building, some will have doors like the ones we built already but most will be sliding glass doors". Every flat's balcony has one: sliding glass
(60 % of buildings), a pair of glazed doors (25 %) or one solid door like the street doors (15 %). Unlike the street doors they are not objects:
there are fifty to a block of flats, so each is drawn shut or open in the chunk's own mesh and collider, and opening one rebuilds its chunk the
way a broken window does. Walking at one opens it from 1.6 m off (the rebuild takes a moment), E opens or shuts it, a hinged leaf opens into
the flat, and the door on every other balcony (half way up a two-row storey) rattles shut. Kept the same for every player by
`Character/BalconyDoorsNetwork`. The whole design, the sizes and the research: docs/WORLD_GACHA.md, "The doors onto the balconies".

## Update 2026-10-16: a window walked into breaks into its pieces too

Owner: "windows dont fracture the same way as when you shoot them vs walking into them. right now they only do the procedural fracture when you
shoot them a bunch of times but after being shattered they should do the procedural fracture when you walk into them". A knock into a cracked
pane (`View/CrackedPane`: a body walking or running into it, a thrown thing) only marked the pane broken, so it vanished with no sound and no
pieces; a shot or a blow went through `GlassBreaker`, which plays the break and cuts the glass into its own pieces (`GlassFractureBurst`).
A knock now goes through `GlassBreaker.Knock` as well: the pane is cut round where the body met it (the nearest point of the body to the
pane, carried onto the pane along the way it was moving), the pieces thrown the way it was going, a sprint or a dive (4 m/s or more) like a
heavy blow. Test: `CrackedPaneKnockPlayTests` (a body walking into a cracked window breaks it, and its pieces are in the air).
