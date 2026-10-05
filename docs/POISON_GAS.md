# Poison gas (key 4)

Asked for: research good-looking smoke/steam in Unity, then a key-4 ability "poison gas": the character blows out a cloud of purple gas that poisons
what it touches. Lasts 10 s for now (details to be tuned together), drain rate chosen by me, never affects the user.

## What it does
- **Press 4**: a cloud is blown out of the chest down the crosshair at 11 m/s, slows to a drift in about a second (stops at walls and the ground, not at
  people), swells from 0.6 m to a 3 m radius over 1.2 s, hangs, and thins away by 7 s (harmless for its last 1.5 s). Cooldown 6 s.
- **Poison**: everything with an `IDamageable` inside the cloud (checked 5 times a second) is poisoned: **3 health per second for 10 seconds = 30 of
  a body's 100**, in half-second ticks of 1.5 (`PoisonEffect`). Strength scales the rate like any damage. It is a quick-to-tune pair of constants
  (`PoisonEffect.DefaultDamagePerSecond`, `DefaultSeconds`).
  - Standing in the gas tops the clock back up; being poisoned again **restarts** it, never stacks, so you stay poisoned until 10 s after you leave.
  - **Never affects the caster** (their colliders are skipped).
  - A player takes it quietly (`IPoisonable.TakePoison`: no flinch, no block, no i-frames); anything else takes each tick as a zero-stun hit.
    Poison can kill. Victims show a few rising purple bubbles that go when it ends.
- **Multiplayer from the start** (`IReplicatedAbility`, id `poison-gas`): other players see the cloud (a cosmetic copy that poisons nothing), the hiss and
  the raised hand. Wired for the host's body and every joining player's body through `PlayerAbilityWiring`.

## The smoke (research -> what was built)
Sources: Unity's Texture Sheet Animation and noise module docs, particle-smoke tutorials. What makes particle smoke read as smoke:
- many LARGE, SOFT puffs with a ragged fractal-noise silhouette, not a clean disc: a procedural 2x2 flipbook of four fbm puffs, one picked at random per puff;
- alpha rises fast and falls slowly, size grows as it ages, each puff turns slowly;
- the **noise module** (low frequency, high quality, scrolling) so the bank rolls instead of puffing out evenly;
- **world-space** simulation so the cloud hangs where it was blown instead of following the mouth;
- alpha-blended, not additive (smoke occludes; additive reads as fire or magic), plus a thin layer of small brighter wisps and a faint purple light.
Not done: soft-particle depth fade at geometry (the legacy particle shader has none; URP's own particle shader rendered blank in batch captures here), and
the cloud does not yet get pushed by wind or by moving bodies.

## Files
`PoisonGasCaster`, `PoisonGasCloud`, `PoisonGasSounds`, `PoisonGasBuilder` (Weapons), `PoisonEffect`, `IPoisonable`, `IPoisonGasCaster` (Character).
Tests: `PoisonGasPlayTests`, `PoisonGasRealInputPlayTests`, the replication tests, and the network test. `PoisonGasCapture` (Explicit, -Graphics) renders it to `Logs/CityShots/gas-*.png`.
