# Acid Rain (key 5)

This is the acid tree's god-tier spell, built first as a test ability on a spare key (2026-10-10). There is no skill tree or god-tier system yet: like Fireball on key 1, it's simply there.

Owner's request: "make the acid rain ... in a large area but not the entire world, just whatever you think would be reasonable for a large fight, and make it do some amount of dps". Later the owner added: "make it so roofs are a full shelter for now". Melting roofs waits for building damage, which is a later system.

## What it does

- **Casting.** Press 5 and the storm is called down where the crosshair meets the world, at most 50 m away (`AcidRainCaster.Reach`). The 50 m cap keeps the cast inside the 60 m the network allows between a caster and their ability's origin. Aimed at the sky, the storm comes down on the ground at 50 m. The cooldown is 60 s.
- **The storm** (`AcidRain` holds every number):
  - It is 60 m across in radius (it was 40 m; the owner asked for a larger area on 2026-10-11) and lasts 20 s.
  - A 2 s warning comes first, while the clouds darken in.
  - Then everyone out in the open under it burns at 6 health a second, checked every half second. That takes a 100-health body down in about 17 s if it never takes cover.
  - The burn scales with the caster's Strength, like all damage.
  - It reaches 40 m below the centre (streets running downhill) and up to the clouds, 1.5 km above it (since 2026-10-15). Anything higher than the clouds is above the rain; a jetpack under them is in it.
- **Shelter.** A roof is full shelter. A ray goes up from the top of each body to the clouds (1.5 km since 2026-10-15), and any solid thing in the way shelters it: a roof, a deck or a balcony. Another body does not count. The rain drops visibly stop on roofs too, because the particles collide with the world and die.
- **The caster** is never burned. A burn is quiet: a player takes it as poison (`IPoisonable`), with no flinch and no block.
- **The look** (`AcidRainBuilder`):
  - a deck of huge, slow-churning storm-grey clouds at the cloud layer, 1.5 km up and 900 m across (`CloudLayer`, the height Self Smite's bolt comes from; owner, 2026-10-15: "make that appear from the height of the clouds"). The poison gas's puff flipbook, tinted; grey, not green, on request. At that distance the puffs are 550 to 850 m across, so some 45 cover the sky where 1,700 small ones used to: cheaper to draw;
  - a lighter underbelly;
  - green rain streaks falling at 30 m/s. They start 36 m up for anyone on the street, and 30 m above anyone higher (`AcidRain.RainStartY`, which follows this machine's camera): 1.5 km of falling drops would be a column of particles nobody could see, while started above the viewer they come down past them from the clouds' direction wherever they are;
  - **green splashes where the drops land** (owner, 2026-10-15: "make a green version of that droplet effect on the floor ... the same green as the acid rain droplets in the air"): the city rain's splash (three tiny droplets thrown up, gone in 0.3 s) where and when each drop near the camera lands, in that drop's own colour;
  - **every drop is placed by script** (`AcidRainStorm.RainDrops`, as the city rain's are): a ray straight down from where it starts finds the first thing it will hit, and it lives exactly long enough to fall onto it. The particle system's own collision, used before, let about a quarter of the drops fall through the ground at 30 m/s (measured in the test) and some through roofs;
  - a faint sickly glow;
  - the city's rain sound, lower and heavier.
  - Everything fades out over 3 s after the burning stops.

## Multiplayer

`AcidRainCaster` implements `IReplicatedAbility` with the id `acid-rain`. The owner's copy fires once with the storm's centre. Every other player's copy calls down the same storm as a cosmetic one, which looks and sounds the same but burns nothing. Damage to other players is decided on the caster's machine and reaches them through their copies, like every hit (see `docs/MULTIPLAYER_STATE.md`). Both kinds of body get the ability through `PlayerAbilityWiring`.

## Files

| Area | Files |
|---|---|
| Spell code | `Weapons/AcidRainStorm.cs` (the `AcidRain` rules and the storm) and `Weapons/AcidRainCaster.cs` |
| Builder | `Weapons/Editor/AcidRainBuilder.cs` |
| Input | `Character/Abilities/IAcidRainCaster.cs`; key 5 in `PlayerInputReader` |
| Assets | `AcidRainStorm.prefab`, `AcidRainStreak.png`, `AcidRainStreak.mat` |

Tests are in `AcidRainPlayTests`:

- burn rate and warning;
- roof shelter;
- caster and outsiders untouched;
- cosmetic burns nothing, and the storm ends;
- aim, reach and network plausibility.

The Explicit `AcidRainCapture` renders `Logs/CityShots/acid-rain-*.png`.

## Later

- **Roofs melting.** Needs building damage: the server keeps a damage list per building, applied on top of the seed's building.
- **Armour corrosion.** Needs an armour system.
- **The skill-tree and god-tier systems.** Until then it is a test key.
