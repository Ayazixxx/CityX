# Poison breath (key 4)

Asked for: research good-looking smoke/steam in Unity, then a key-4 ability "poison breath": the character blows out a cloud of purple gas that poisons
what it touches. Lasts 10 s for now (details to be tuned together), drain rate chosen by me, never affects the user.

## What it does
- **Press 4**: a cloud is blown out of the chest down the crosshair at 11 m/s, slows to a drift in about a second (stops at walls and the ground, not at
  people), swells from 0.6 m to a 3 m radius over 1.2 s, hangs, and thins away by 7 s (harmless for its last 1.5 s). Cooldown 6 s.
- **Poison**: everything with an `IDamageable` inside the cloud (checked 5 times a second) is poisoned: **3 health per second for 10 seconds = 30 of
  a body's 100**, in half-second ticks of 1.5 (`PoisonEffect`). Strength scales the rate like any damage. It is a quick-to-tune pair of constants
  (`PoisonEffect.DefaultDamagePerSecond`, `DefaultSeconds`).
  - Standing in the gas tops the clock back up; being poisoned again **restarts** it, never stacks, so you stay poisoned until 10 s after you leave.
    The ticks keep their beat through a new dose (2026-11-05): a dose used to start the half-second tick over, and with the cloud dosing
    five times a second none ever landed while a body stood in the gas, only once it had left
    (`PoisonGasPlayTests.StandingInTheCloudHurtsFromTheStartNotOnlyAfterLeavingIt`).
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
`PoisonBreathCaster`, `PoisonGasCloud`, `PoisonGasSounds`, `PoisonGasBuilder` (Weapons), `PoisonEffect`, `IPoisonable`, `IPoisonBreathCaster` (Character).
Tests: `PoisonGasPlayTests`, `PoisonBreathRealInputPlayTests`, the replication tests, and the network test. `PoisonBreathCapture` (Explicit, -Graphics) renders it to `Logs/CityShots/gas-*.png`.

## Update: it is a scream (renamed from "poison gas")
- The ability is **poison breath**. Pressing 4 plays the **Scream** animation (`Animations/poison_breath_scream.fbx`, 2.8 s, played at 1.5x): the
  head tilts back to breathe in, then snaps forward about 1.05 s of clip time in, and the gas is breathed out of the **mouth** at that moment
  (the head bone moved out along the way the head faces; `PoisonBreathCaster.MouthOffset`), aimed at where the crosshair is then.
- **Only the torso and head play the scream** (layer `ScreamTorso`, mask Body+Head), so the hips and legs keep walking. The **arms** play it on their own
  layer (`ScreamArms`) which sits BELOW the sword's upper-body layer, so a sword swing or guard keeps the arms, and the gun-aim and cast poses (applied
  after the Animator) do too: the arms do the scream's arm motion only when nothing else is using them.
- Replicated with the rest of the animations (both states go through the timed-state sync); the gas itself rides the `poison-breath` ability event.
