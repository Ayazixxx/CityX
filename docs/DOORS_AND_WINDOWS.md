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
