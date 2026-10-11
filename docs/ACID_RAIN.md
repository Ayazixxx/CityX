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
  - It reaches 40 m below the centre (streets running downhill) and up to the clouds, 70 m above it. Anything higher than the clouds is above the rain.
- **Shelter.** A roof is full shelter. A ray goes up from the top of each body to the clouds, and any solid thing in the way shelters it: a roof, a deck or a balcony. Another body does not count. The rain drops visibly stop on roofs too, because the particles collide with the world and die.
- **The caster** is never burned. A burn is quiet: a player takes it as poison (`IPoisonable`), with no flinch and no block.
- **The look** (`AcidRainBuilder`):
  - a disc of huge, slow-churning storm-grey clouds 70 m up (the poison gas's puff flipbook, tinted; grey, not green, on request);
  - a lighter underbelly;
  - green rain streaks falling at 30 m/s, starting about 36 m up (under the clouds, which keeps the number of drops affordable over a 60 m radius);
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
| Input | `Character/IAcidRainCaster.cs`; key 5 in `PlayerInputReader` |
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
- **Tall rooftops.** Rooftops higher than the 70 m clouds are above the storm.
