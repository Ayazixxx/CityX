# Third-person pistol mechanics

What was researched, what was built from it, and -- just as important --
what was deliberately NOT built.

## What the research found

Sources are linked at the bottom. Quality varies: the Monday Night Combat
write-up is a shipped-game developer's own account; most of the rest is forum
and wiki-level summary, and I say so where a claim rests on it.

1. **The core third-person problem is parallax.** The camera sits away from the
   gun, so a bullet that follows the crosshair can pass *through* a wall corner
   the barrel is really behind (the shooter stays safe and shoots through
   geometry). Monday Night Combat's fix is three traces: a **camera trace** (what
   the crosshair is on -- it is critical that this one comes from the camera),
   a **hand trace** (body to barrel, so the barrel is not inside a wall) and a
   **weapon trace** (barrel to the camera trace's hit point). If the later
   traces are blocked, the new hit location is used. Dynamically moving the
   crosshair to compensate was tried and rejected ("causes the crosshair to jump
   around"). Muzzle flashes still come from the barrel.
2. **Aim-down-sights is a camera change plus a tighter reticle.** Over the
   shoulder, a narrower field of view, finer look control. (This game already
   had the FOV and shoulder shift from Godot.)
3. **Shoulder swap is a genuine tactical tool**, not cosmetic: peek round a
   corner without exposing the body, clear a wall your usual shoulder is against.
4. **Recoil is spring-like and recovers.** Third-person games settle the view
   back quickly after firing stops. A hobby "game feel" recipe gave a concrete
   shape for a pistol: a small kick, full recovery in roughly 0.15-0.2s, a small
   roll jolt, muzzle flash under ~70ms -- I treated the numbers as a starting
   point, not gospel. CryEngine's docs describe third-person recoil as separate
   parameters (duration, strength, kick-in, damping) from first-person.
5. **Feedback needs three channels and must never lie.** The gun (kick), the
   camera (recoil/jolt), and the reticle + audio (a hit was confirmed; a kill is
   different; the barrel is blocked; you are reloading). A crosshair-design
   article's own pitfalls: don't sacrifice core aiming for extras, and never give
   feedback that misleads about the real trajectory.
6. **Accuracy models vary, and are a design choice.** Gears has no hip-fire
   inaccuracy (ADS is what tightens spread and controls recoil). Fortnite has
   bloom that grows with moving/jumping/shooting and recovers standing/crouching
   or tap-firing. Resident Evil 4 (remake) has a reticle that *focuses* (shrinks)
   the longer you hold aim, plus sway. The Last of Us has deliberate weapon sway
   with an accessibility option to turn it off.
7. **Empty-gun handling:** clicking an empty gun auto-reloads in the modern
   third-person shooters (it is the common convention; I did not find a single
   authoritative source for it, so this one is convention, not citation).

## What was built

| From the research | What it does here |
|---|---|
| Three-trace shot | `PistolWeapon.ResolveTrace`. Camera trace first (the shot); hand trace body -> muzzle; barrel trace muzzle -> the camera hit point. Open ground: identical to before. A wall between the *gun* and the target takes the bullet. The shooter's own body no longer eats a shot (the old single `Raycast` returned the shooter's own capsule and discarded the shot). |
| Obstacle prompt | `IsBarrelBlocked` each frame -> the crosshair turns red while a shot would hit scenery instead of the target. |
| Recoil | 1.2 degrees of camera kick per shot (x0.7 aiming), settling at 10/s back to *exactly* where you were aiming -- a visual offset never written into the real aim, capped at 6 degrees, plus a 0.25-degree roll jolt. Applied **after** the shot resolves, so the first shot is always exactly on the crosshair. |
| Gun snap-back | The pistol is shoved back along its own barrel and tips its muzzle up, then settles. Derived from the muzzle's real world facing (this gun's grip has a 180-degree roll), and **measured** on the real rig by `PistolKickRigPlayTests`. |
| Hit confirmation | Four diagonal ticks on the reticle: white/quick (0.16s) on a hit, red/longer (0.32s) on a kill; a soft synthesised tick (a lower two-note for a kill). Nothing for a miss, a wall or a dry click. |
| Player-status reticle | Dims while reloading. |
| Auto-reload / dry click | An empty click starts a reload if there is reserve ammo; otherwise a dry click, throttled to 4/s. |
| Shoulder swap | **Q** swaps the camera to the other shoulder (slides across at 6/s). Works with anything in hand. |
| ADS feel | Aiming: mouse look x0.75, walk speed x0.8, and aiming ends a sprint (Fortnite's sprint puts the weapon away). |

Everything is live-tunable with `/gun` (`recoil`, `adsrecoil`, `shake`, `kick`,
`kickangle`, `recover`, `sens`, `aimspeed`, `shoulder`). `/gun` with no argument
prints the current numbers.

**Two hands (2026-09-26):** aiming now brings the free hand up onto the grip. The Mixamo
`pistol_aim_in` clip is a one-handed pose on this rig (measured: hands 61 cm apart, left
arm dangling), so the left arm is reached by IK onto a point in the gun's own space
(`PistolWeapon.SupportGripLocal`, dragged into place in the editor: CityX > Weapon Grips) -- 15 cm apart, palm
within 2 cm of the point at 0 / +40 / -40 pitch. It eases in as the gun is raised, is
free while the gun is merely carried, and follows the reload dip and the kick. Details:
[MIXAMO_WEAPONS.md](MIXAMO_WEAPONS.md).

## What was deliberately NOT built

* **Reticle bloom / random spread / weapon sway.** The research is clear these are
  real design choices, but you asked for every bullet to land *exactly* where the
  crosshair is (CS:GO-style), and a reticle that swelled with movement would be
  lying about that. So the recoil is deterministic and learnable, the reticle
  only pulses for a tenth of a second when you fire, and there is no sway.
  Say the word if you want RE4-style "focus" or Fortnite-style bloom instead --
  they are one system away, but they change the exactness you asked for.
* **Headshot multipliers.** The Godot pistol has none, and NPC bodies do not all
  have head hit zones. Hit markers are ready for it (a distinct headshot cue
  would slot into `GunShot`).
* **Cover / blind fire, aim assist.** Aim assist is designed for controller
  players (the sources say so); this is a mouse game. Cover is a whole system.
* **Shell ejection / hit-pause.** Fun, cheap to add later; not required for the
  mechanics.

## Known limitations

* The recoil numbers are a reasoned starting point, not measured against a
  reference. Feel is judged by playing; that is what `/gun` is for.
* The three-trace shot only helps where colliders exist. Anything without a
  collider (some decorative meshes) is still shoot-through, as before.
* The hit tick and kill tick are synthesised sine bursts; a real sound design pass
  would replace them (`GunFeel.FeedbackVolume` is 0.35, 0 mutes them).
* Aim speed 0.8 reuses the existing gun-walk clips, which blend on the body's
  real local velocity; if the stride looks like it slides at 0.8, `/gun aimspeed 1`
  removes the slowdown.

## Sources

* [Adventures with Guns in a Third Person Shooter -- ForrestTheWoods](https://www.forrestthewoods.com/blog/adventures_with_guns_in_a_third_person_shooter/) (Monday Night Combat's three-trace method)
* [UX Design: Methodology of keeping cross-hair design on track -- Game Developer](https://www.gamedeveloper.com/design/-ux-design-methodology-of-keeping-cross-hair-design-on-track)
* [Fortnite Bloom -- Arena FPS](https://arenafps.com/fortnite-battle-royale-weapon-bloom-mechanics/)
* [M5: game feel -- full-recovery recoil, shake, muzzle flash, hit marker (GitHub PR)](https://github.com/lk0024709-ux/Fps-game-3d-/pull/4)
* [Shooting VFX in Unity -- Trau Studios](https://traustudios.com/ideas-inspirations/shooting-vfx-unity/)
* [Aim assist -- Wikipedia](https://en.wikipedia.org/wiki/Aim_assist)
* [Procedural Weapon Animations -- CRYENGINE manual](https://docs.cryengine.com/display/SDKDOC2/Procedural+Weapon+Animations)
* [Third-person shooter -- Wikipedia](https://en.wikipedia.org/wiki/Third-person_shooter)
* [Third-Person Shooter Mechanics -- TapNJoy](https://www.tapnjoy.com/article/third-person-shooter-mechanics-aim-shoot-and-dodge-like-a-pro)
* [Resident Evil 4 discussions (reticle focus / bloom / sway)](https://steamcommunity.com/app/2050650/discussions/0/601918052458617574/)
* [The Last of Us 2's Aim and Aim Settings -- ResetEra (weapon sway as a design choice)](https://www.resetera.com/threads/the-last-of-us-2s-aim-and-aim-settings-kind-of-suck.237973/)

## Update 2026-09-26: the arm spasm and the tracer

- **"Arms spazz out a bit when shooting."** Measured with real shots (`PistolArmsSteadyPlayTests`): with the
  kick off the arms were smooth (0-0.3 degrees per frame); with it on every shot snapped the arm 5-6.6 degrees
  in one frame (both arms once the free hand followed the gun). Cause: the kick was laid on the pistol BEFORE
  the arms were solved, and the arm's barrel correction measures where the muzzle points, so it saw the
  7-degree kick as an aiming error and cancelled it by jerking the whole arm. The kick is now applied in
  `PistolWeapon.LateUpdate` (execution order after `PlayerLocomotion`), after the arms, so they aim the
  un-kicked gun and the recoil shows on the pistol. Aimed and hip-fired bursts now measure 0 degrees/second of
  arm movement. The hip-fire raise (the arm coming up for a shot) is also eased with an S-curve
  (`EasedGunAimBlend`) and takes 55ms instead of 40, so it no longer starts and stops with a twitch.
- **Tracer**: 2.2 pixels wide (was 3) and a pale warm white (1.0, 0.96, 0.72), between white and the old
  brass yellow (1.0, 0.85, 0.35).

## Update 2026-09-26 (later): the gun "pointing in other directions", and the spazzy tracers

- **Cause 1: the aim DEPTH was not continuous.** Every frame `GunFeel` traces the crosshair ray and the arm
  aimed at where it ended. What a ray ends on is not smooth -- across a facade with shot-out windows it flicks
  between the glass at 3 m and the room behind at 30 m, a lamp post is a one-frame hit at 2 m -- and from the
  shoulder (camera 4 m behind, to one side) a 3 m and a 30 m point on one ray are ~20 degrees apart. Measured
  with the crosshair steady and only the hit depth flickering (`PistolAimDepthJumpPlayTests`): a left upper arm
  turning **34 degrees in one 16 ms frame** (2,100 deg/s). Now the aim point stays ON the camera ray (the mouse
  still turns the arm with no lag) and only how far along it the arm looks is eased, in log space, with a 70 ms
  time constant (`PlayerLocomotion.GunAimDepthSmoothSeconds`); the muzzle's worst turn is ~170 deg/s. The hard
  1.5 m cut-over to a fixed point is gone too.
- **Cause 2: flying glass.** A shattered window's shards are launched back toward the shooter as solid rigid
  bodies for six seconds, and the aim trace treated each one as what the crosshair was on. Shards now carry
  `IgnoredByShots` and both bullets and the aim trace look straight through them.
- **The tracer**: its start followed the muzzle for its whole life, so every wobble of the gun was drawn as a
  wobble in the beam. It now follows only for the first 0.05 s (long enough to cover the raise for a hip-fired
  shot) and is then a fixed line in the world; it also reads the muzzle last in the frame (execution order 200,
  after the arms are solved and the kick is applied).

## Update 2026-10-14: the Glock, the AK-47, real reloads, a camera you can see the gun from

Everything here still holds; on top of it, see [FIREARMS.md](FIREARMS.md): the placeholder Beretta is replaced by a Glock made in
Blender with a slide, magazine and trigger that move; reloads are keyframed motions (tactical and empty, the magazine dropped and
seated, the slide racked) played on the body and by name on other players' copies; the fingers hold the gun like a gun (trigger
finger along the frame until it fires); the aim camera comes in over the shoulder (1.0 m to the side, 2.3 m back) so the gun is on
screen; the gun is one component with a profile, and the AK-47 is the second profile (full auto, a learnable climbing spray, a
rock-in magazine and a charging handle). The old `ADS_SHOULDER_OFFSET_X` 1.415 at 4 m is gone, as is the reload's sine dip (kept only
for a body without the motion).
