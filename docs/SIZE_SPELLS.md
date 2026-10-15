# Grow and Shrink (the Wizard tree)

Owner, 2026-10-15: *"two more spells. one that makes you 10x as big and one that makes you 10x as small ... i was going to make one just
called "Wizard" and lets just put it in there for now"*; *"the 10x as big and the 10x as small skill lasts for 30 seconds each"*; icons are
the spell's first letter (G, S) until the owner draws them.

## How it plays

- **Grow** (key **0** on the default bar): ten times your size for 30 s. **Shrink** (drag it onto a slot from C, Player, Skills, Wizard): a
  tenth of your size for 30 s.
- The change takes 1.2 s, with a swirl of violet motes spiralling up (growing) or down (shrinking) round the body and a rising or falling chime.
- **The same spell again ends it early.** The other spell goes straight to the other size, for a fresh 30 s. Cooldown 1 s between casts.
- **Growing needs room**: under a roof or a bridge you grow until your head touches it, and carry on once you are out from under it.
- Death and respawn put you back at your own size.

## How a resized body moves

Gravity is the same for a giant as for anyone, so a real body's natural pace goes with the **square root** of its size (the Froude number:
why an elephant walks faster than a mouse, but nowhere near as many body-lengths a second). So:

| | Giant (x10) | Tiny (x0.1) |
|---|---|---|
| Height | 26 m | 26 cm |
| Walk / sprint | 12.6 / 23.7 m/s (x3.16) | 1.3 / 2.4 m/s (x0.32) |
| Steps | 3.16 times as slow (heavy) | 3.16 times as quick (scurrying) |
| Jump | 10 times as high, 3.16 times as long in the air | a tenth as high, a quick hop |
| Kerbs it steps onto | up to a car (2.3 m) at once, up to 6.5 m with the step-up | 4 cm |
| Fall damage | measured in its own lengths: 20 m is a hop | as anyone's |
| Camera | 10 times as far back | 10 times as close, near plane brought in |

The walk and run animations play at `StrideRate` = 1/sqrt(size) (an Animator float on the walking states, `PlayerRigBuilder.AddStrideRate`), so
the feet keep to the ground instead of skating; the Animator is told the body's speed in its own terms (a giant's 12.6 m/s is a walk).

While resized, **climbing, vaulting, wall-stick and foot placement are off**: their probes are in metres for a 2.6 m body.

## How it is built

| What | Where |
|---|---|
| The size over time, the 30 s, the change by ratio (log space), room to grow, speed and stride (pure) | `Character/BodySize.cs` |
| The spell on the body: casts, applies the scale, room check, swirl, sound | `Character/BodySizeAbility.cs` |
| Speed, jump, dash, animator speeds, step heights x size | `PlayerLocomotion` (`BodyScale`, `SizeSpeedFactor`, `IsResized`), `PlayerLocomotion.StepUp.cs` |
| Camera distances x size | `ThirdPersonCamera` (reads the body's scale) |
| Fall damage in a giant's lengths | `PlayerVitals.FallScale` |
| The tree and the bar | `SkillCatalog` (Wizard, `grow`, `shrink`, Grow on 0) |

**The size is the body's transform scale.** The model, the capsule, the ragdoll and whatever it holds scale with it. Unity's CharacterController
scales its height and radius with the transform but NOT its step offset or skin width (world metres), and refuses a step offset taller than its
own unscaled height: the step is scaled by hand and capped at 2.3 m (the scaled step-up takes anything taller), and the skin goes with the size
(a tiny body floated on 3 cm of it).

## Multiplayer

- **The size itself travels with the body's NetworkTransform**, which syncs scale (a late joiner sees a giant as a giant).
- The cast (swirl, chime, raised hand) is an ability event (`body-size`: kinds 0 grow, 1 shrink, 2 back to normal). A copy never sets its own
  scale; it paces its own stride from the scale it is given.
- Tested over real sockets: `NetworkedAbilitySyncPlayTests.GrowOnOnePlayersBodyIsShownOnAnothersCopyAndSoIsTheWayBack`.

## Open

- Melee reach, spell sizes and damage do not change with size yet (a giant's punch reaches as far as anyone's).
- NPCs aim at a giant's normal chest height.
- A giant can be grown into a street too narrow for it (it stops growing at the walls), but walking into one it simply cannot pass.
