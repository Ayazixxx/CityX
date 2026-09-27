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
  skip over a pane), a **thrown bottle** (4 m/s or faster) and a **sprint or jetpack dive at 6.5 m/s or
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
through glass, wind-up breaks nothing, sprint breaks, walking does not). The look and sound of the door and the
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
