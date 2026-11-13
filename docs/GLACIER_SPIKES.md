# Glacier Spikes

Owner, 2026-11-12, sharing the "Frost Lance" of an MIT-licensed Three.js spell sandbox
([achrefelouafi/LinearAbiltyCastingThreeJS](https://github.com/achrefelouafi/LinearAbiltyCastingThreeJS)): "make the ice crystals erupting
from the ground line spell a hotbar spell and when you press it you look which direction you want to cast it in and then click to cast. similar
to the octoslash". The owner named it **Glacier Spikes**. It is the **Cryomancer**'s first-row left star, after Ice Shards
(`SkillCatalog.GlacierSpikes`, `glacier-spikes`). Its bar icon is the letter **G** until the owner draws one.

## How it plays

| | |
|---|---|
| Its key | **Arms** it. The body turns to where the camera looks (as the Giant Boomerang's aim does: `LocomotionInput.ThrowAiming`, 900 degrees a second) and walks on facing that way. An **arrow on the ground** shows the line: the band the spikes will fill, from just ahead of the feet to the line's end, with a pointed tip past the end (League of Legends' skillshot arrow, which the reference has too): pale cyan, its edges bright, laid over the ground (each 0.5 m of it level across, on the highest of the ground under its middle and its edges, so a kerb or a step beside the line never hides part of it). Only the caster sees it |
| Left click | **Casts** it. The click is only the spell's: it is held back from everything else until let go, so it never also punches, swings, shoots or charges a staff |
| The key again, or right click | Puts it away; nothing is spent |
| It stays armed | until one of those, or until the body is out of its player's hands: a menu, a stagger, a fall, a climb, a ladder, death (the reader must aim it every frame; a frame it does not, it is put away) |
| The line (mine) | Along the ground, the camera's flat direction, from 0.9 m ahead of the feet, **always to its full reach: 21 m** (the reference's 15 m, scaled). The ground is looked at every 0.5 m: the line goes up kerbs and stairs (up to 0.9 m a step) and down drops (up to 2.5 m), and ends at a wall (a ray at knee height) or a deeper drop. Small props (a parking meter, a lamp post, a hydrant: anything under 1.2 m across both ways) and bodies do not stop it: the ice flows past them. Higher than 4 m above any ground, or with a wall at the nose, there is nothing to cast along and the click does nothing (it stays armed) |
| The front | Races down the line at 36 m/s (the reference's 26, scaled), easing up to speed over 0.08 s: the whole line in about 0.6 s |
| The spikes | 190 crystals tear out of the ground behind the front, in a band 0.8 m either side at the start widening to 3.5 m at the end, taller down the line (0.7 m to 4.3 m, swelling to 6.3 m at the end), shorter at the band's flanks, some knocked down to ankle-high rubble; about a fifth are thrown up round the far end, within 4.4 m of it. Each springs past its height and settles. A crystal beside a wall stays 25 cm off it; each stands on the ground under it |
| Who is struck | Everyone the front passes in the band (their feet within 0.5 m of it, and from 1.5 m below the ground to 3.5 m above it), and everyone round the far end once it gets there: **once each**. Never the caster. The spikes themselves have no colliders yet |
| After | The field stands 4.2 s from the cast, then sinks into the ground over a second |
| Cast pose | The raised hand every spell uses (`PlayerLocomotion.SetCastPose`), 0.3 s and easing out over 0.4 s (quicker with Cast Speed). No new arm patches |

## The numbers (mine)

`GlacierSpikesRules`. Mana and damage set against the Fireball (10 mana, 25 damage) and the Arcane Orb (20 mana): one strong spell's worth.

| | |
|---|---|
| Mana | **20** (18 through the Magic Wand) |
| Cooldown | **6 s** from the cast (shorter with Cooldown Reduction) |
| Damage | **25** to each one struck, magic, before Spell Power (the Fireball's); Armor Penetration and magic resistance apply; the striker is named, so its numbers show and a kill pays its XP |
| Chill | **half a full chill** (`Chill`, `FrostRules`): the body walks at three quarters of its pace, wearing off in about a second and a half; a second frost blow on top of it (another cast, the Frost Staff) freezes it |
| Hitstun | 0.5 s, as a spell's |

## The look (ported from the reference)

The reference's `IceAbility` (spawn and spike update), `IceMaterial` and crystal geometry, ported to C# and HLSL. Its lengths are made 1.4
times longer (its figure is 1.8 m, ours 2.6 m); its times are kept.

- **The field** is a pure function of the cast: the line's start, its way, its length and a seed (`GlacierSpikesLayout`). Each crystal is a
  record of dice: how far down the line (`pow((i + rand) / n, 0.85)`), how far across (clumped `sign * |x|^1.35`, plus scatter), and jitters
  for its height, width, lean and turn; a share are rubble; 22% go to the cluster at the end (spread `sqrt(rand)` round it). A crystal breaks
  out when the front passes it (staggered up to 0.09 s), rising out of the ground (outQuint over 0.17 s, sliding up 0.85 of its height and
  growing from 0.86 of its size), then springing past and back (`1 + 0.26 sin(14 t) exp(-t / 0.55)`). It leans away from the caster and
  outward, more down the line. At the end the whole field sinks (inCubic). Two changes of mine: a leaning crystal's foot is sunk by half the
  width its lean lifts off the ground, so no gap shows under its high side; and no crystal is narrower than a tenth of its height (the
  reference's thinnest, ringed in black cel lines, read as poles in the first pictures).
- **The crystals** (`IceCrystalMesh`): a 7-sided column on a unit footprint, five uneven rings and a pointed tip, tapering to 0.69 of its
  width, its spine bent to one side; three seeds, so three shapes. Flat shaded. Built once, in code.
- **The ice** (`CityX/Ice`, `View/Shaders/CityXIce.shader`): opaque, lit by the game's cel light like every surface (shadows, lamps, the dark
  under the ground), but made to read as translucent as the reference's is: the deep tint (#3e737a) where a face looks straight at the eye and
  the pale ice (#8adaff) where it turns away, a glowing rim (#f2feff), cracks (ridged noise) and veins of light inside (#638797), a frost line
  at the foot, glints that come and go, and a glow from within for 0.45 s after each crystal breaks the ground. One term is mine: the faces
  turned away from the eye give back a little of the ice colour (`_BodyGlow`), since the cel light flattens the environment reflections the
  reference's cyan came from. Its black cel lines trace each crystal's outline but not every facet (the depth-normals pass writes a smooth
  normal).
- **Round it** (`GlacierSpikesEffects`): ice chips thrown up where each crystal breaks the ground, mist rolling and glitter rising behind the
  front, frost (#f0f9ff, edged #79b6dd) left along the band; at the end a burst of mist and glitter, a ring racing out over the floor (#5fd0ff),
  a frost patch, a flash of cold light (#7fd4ff) and a small shake. The light runs with the front, then stays at the end until the field sinks.
  The mist is thin (the first pictures' was a white wall) and none rises within 3 m of the line's start, where it filled the caster's own view;
  it fades where it meets the street (soft particles). The mist and chips are shaded down at night (they are no lamps), the glitter to a third,
  so no specks shine in the dark; the ice's own glow is not.
- **Cost**: the crystals are three instanced draws (one per shape, `Graphics.RenderMeshInstanced`) and their shadows, about 13,000 triangles;
  the field places them each frame with no garbage.

## The pieces

| | |
|---|---|
| `GlacierSpikesRules` | the numbers, the front's run, a crystal's rise and sink, whom the front strikes (pure) |
| `GlacierSpikesLayout` | the crystals' dice from a seed, and where each stands at a moment (pure) |
| `GlacierSpikesPath`, `IGlacierGround`, `GlacierSpikesGround` | the ground along the line (pure, the world asked through an interface; the real world's rays ignore bodies) |
| `IceCrystalMesh` | the crystal's mesh (pure) |
| `GlacierSpikesField` | one cast as it is seen: lays the crystals on the ground, draws them instanced |
| `GlacierSpikesEffects` | chips, mist, glitter, frost, ring, light, shake |
| `GlacierSpikesArrow`, `GlacierGroundStrip` | the arrow while armed, and the strip of ground both it and the frost are drawn on |
| `GlacierSpikesAbility` | arming, aiming, casting, the cooldown and mana, the strikes; `IReplicatedAbility` |
| `PlayerInputReader.Spells.cs` | its key, and its clicks while armed (taken before `TickCombat`'s weapons) |
| `CityX/Ice` and its material (`Character/Resources/GlacierIce.mat`, `IceMaterialBuilder`, rebuilt with the scene) | the ice |

## Other players (docs/MULTIPLAYER_STATE.md)

`GlacierSpikesAbility` is an `IReplicatedAbility` (`glacier-spikes`), on both kinds of body (`PlayerAbilityWiring`). One event, the cast:
Origin the line's start, Direction its way, A its length, B its seed (0 to 999). Every other player's copy of the caster grows the same field
from it, on its own ground, with the raised hand, and strikes nobody; the caster's machine strikes, and a blow on another player reaches them
through their copy as every blow does. Arming and the arrow are the caster's own; the turn of the body comes with its transform.

A frost blow relayed to another player used to carry only "it chills" and was given one spray tick's chill (an eighth). It now carries how
much, in eighths (`NetworkedDamage.Flags`, `ChillOf`), so a player struck by Glacier Spikes on another machine is chilled by half, as on this one.

## Tests

- `GlacierSpikesRulesTests` (EditMode): the band widening, the front's pace and arrival, the same seed growing the same field and another seed
  another, heights ramping and swelling at the end with shorter flanks and rubble, the impact cluster round the end and every crystal in its
  band, a crystal rising, springing, settling and sinking on time, whom the front strikes (behind it, in the band, on the ground, round the end
  once there, never behind the caster), the line up a stair and stopping at a wall, at a drop and with a wall at the nose, the crystal's mesh
  (closed, base to tip, faces outward), and a relayed frost blow's chill.
- `SkillTreeTests.GlacierSpikesIsTheCryomancersFirstRowLeftStarAfterIceShards` and the skill list.
- `GlacierSpikesPlayTests` (the real keys in the real city): its key arms it, the body turns to where the camera looks, the arrow is on the
  ground down the open street; a click casts it, pays 20 mana, starts the 6 s cooldown and throws no punch, and the key does not arm it again
  while it cools down; a body on the line is struck and chilled by half and a dummy on it is struck exactly once with the chill, one beside the
  band is untouched; the key again and a right click put it away, nothing spent; and on a flat floor another player's copy, told of the cast,
  grows the very same field and strikes nobody.
- `NetworkedAbilitySyncPlayTests.GlacierSpikesOnOnePlayersBodyGrowTheSameFieldOnAnothersCopy` (over real sockets), and the replication
  guard tests.

## Pictures

`GlacierSpikesCapture` (Explicit, `-Graphics`) writes `Logs/CityShots/glacier-*.png` in the real city, from the game's own camera behind the
player (`-game`) and from the side (`-side`): armed with the arrow, the front halfway, the impact, the whole field, close on the crystals, from
above, sinking; and all of it again at midnight (`glacier-night-*`).

## Not yet

- **An own Blender cast clip** for the cast (it uses the raised hand every spell uses).
- **Colliders on the spikes**: they cannot be stood on, nor block anything.
- **Sound**: none yet.
- Shattering the field early, or a frozen body shattering on the spikes.
- The line bending round corners: it runs straight, and a wall ends it.
- Aiming on a controller (the key and the clicks are the keyboard's and the mouse's, as the other spells' are for now).

## Credit

The look and its numbers are ported from "Frost Lance" in achrefelouafi's
[LinearAbiltyCastingThreeJS](https://github.com/achrefelouafi/LinearAbiltyCastingThreeJS) (`src/abilities/IceAbility.js`,
`src/materials/IceMaterial.js`, `src/assets/ProceduralGeometry.js`), "Copyright (c) 2026 mohamedachrefelouafi", MIT licence: its spike
layout and timeline, its crystal geometry and its ice shader's colours and terms. No file or asset was copied in; the port is C# and HLSL
written for this game, and the source files that carry it (`GlacierSpikesLayout`, `IceCrystalMesh`, `CityXIce.shader`) name it.
