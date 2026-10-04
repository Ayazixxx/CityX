# Ice shards (key 3)

Asked for: "an ability for the 3 key called Ice shards, similar to fireball but instead he fires 4 ice shards consecutively, the ice shards
get stuck in the wall they hit for 30 seconds or if they hit a character it stays for 10."

An innate spell like the fireball (key 1): nothing to equip, works with anything in the hands.

## What it does

- **Press 3**: four shards leave the chest one after another, 0.11 s apart (a volley takes a third of a second). The first leaves at the press.
- **The volley follows the crosshair.** Each shard goes where the crosshair is at its own moment (`PlayerInputReader.AimIceShards` hands the
  aim to the caster every frame while it is firing), so sweeping the view across a target spreads the four across it. Each is also nudged
  off the exact line by up to 0.8 degrees in a fixed pattern (centre, then a ring), so a volley that sticks in one spot is a cluster, not one
  shard over another.
- **Flight**: straight and fast (50 m/s, no gravity), swept with a sphere cast each step plus an overlap at the start (a shard fired from
  inside a wall sticks at once). It ignores the caster's own body. A shard that hits nothing goes after 3 s.
- **Sticking**: the tip goes in 0.22 m past a wall (0.30 m into a character), pointing the way it came with a few degrees of repeatable tilt.
  - **World** (wall, ground, door, anything that is not a character): stays **30 s**.
  - **Character** (anything with an `IDamageable`, alive or not): hurts it (8 damage, scaled by Strength) and stays **10 s**.
  - Either way the shard is parented to what it hit, so it rides along with a character, a door or a ragdoll bone, and goes with a street
    chunk that streams out. For the last 0.8 s it melts (shrinks to nothing), then it is destroyed.
- **Cooldown** 2.5 s from the start of a volley; not while a volley is going out; not while dead.
- **Cast pose**: the same raised hand as the fireball (left hand while the grapple gun holds the right), held through the volley and easing out
  over 0.4 s. The jetpack-suppression and grapple-swing paths treat it like a fireball cast.

## Look and sound

- A faceted crystal 0.9 m long (`IceShardBuilder`): hexagonal section squashed thin, flat-shaded, pointed at the front and cut off flat at the
  back; pale blue, glossy, with a cold glow so it reads in shade; an additive frost trail while it flies, which stops when it sticks.
  The prefab's origin is the tip, so where it strikes is where its origin goes.
- Two sounds made in code (`IceShardSounds`, no audio files): a quick bright "shing" per shard and a glassy "tink" when one lodges.

## Code

`IceShardCaster` (volley, cooldown, pose, aim), `IceShardProjectile` (flight, sticking, ageing), `IIceShardCaster` (so Character does not depend on
Weapons), `IceShardBuilder`, `IceShardSounds`. Tuning on the caster: `ShardCount`, `ShardIntervalSeconds`, `CooldownSeconds`, `SpreadDegrees`,
`ShardSpeed`; on the projectile the constants (`BaseDamage`, `WorldStickSeconds`, `CharacterStickSeconds`, `FlightSeconds`, embed depths).

Tests: `IceShardCasterTests` (volley timing, cooldown, aim following, pose, dead caster), `IceShardPlayTests` (sticks in a wall for 30 s, in a
character for 10, rides with it, melts, ignores the caster, fired from inside a wall), `IceShardRealInputPlayTests` (key 3 in the real scene).
`IceShardCapture` (Explicit, needs `-Graphics`) renders it to `Logs/CityShots/ice-*.png`.

## Not done

- Other players do not see the shards (like the fireball, not replicated).
- No hit-stop or flinch from a shard (NPCs have none yet); only the damage.
- Shards have no collider once stuck: they cannot be walked on or stopped by other shards.
