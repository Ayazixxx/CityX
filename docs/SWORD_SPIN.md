# The sword spin (key 2): Link's Up Special

Asked for: "research link's up special from smash bros and make the 2 key sword spin more like that ... make it so you can use it on
ground or if you jump and use it then it goes upward like smash bros ... research all the details about it like how long it lasts."

## What Link's Spin Attack is (Smash Ultimate, 60 fps)

Sources: SmashWiki "Link (SSBU) / Up special" and "Spin Attack", UltimateFrameData (Link), KuroganeHammer (Smash 4).

**Grounded.** One slow, undulating spin (the speed swells and dips as it turns).
- Hitboxes frames 7-39. Damage falls with how late it connects: clean (7-8) 14%, mid (9-13) 12%, late (14-23) 9%, latest (24-39) 7%.
- **Chargeable**, ground only (Brawl onward): hold for up to 60 frames; damage x (1 + 0.6 x charge/60), so up to 1.6x (22.4% clean).
- Animation 94 frames (91 uncharged); interruptible at frame 77 (74 uncharged). No cooldown: the lag is the cost.

**Aerial.** A fast multi-hit spin that climbs.
- Eight blows, alternating front and back: front 4% at frames 8, 16, 22, 31; back 2% at 12, 19, 26, 38; a last blow at 47-49 (4%) with high
  upward knockback.
- Rises: initial vertical speed 2.4 (Smash units) under 0.55 gravity, air speed x0.5 while it turns; "moderate" height.
- Animation 91 frames. **Landing before it ends costs 30 frames of landing lag.** Link can use it once per time in the air (it is a recovery).
- Smash 4 buffed the height and the momentum kept from before; Ultimate made every version harder to escape.

## What the game does

All of the timeline is in [SwordSpinMove.cs](../CityX/Assets/CityX/Character/SwordSpinMove.cs) as frame counts at 60 fps, so it can be
checked against the above (`SwordSpinMoveTests`).

| | Ground | Air |
|---|---|---|
| Press | **hold 2**: sword drawn back, charging up to 1 s; **release** to spin (a quick tap spins at once) | 2 spins at once |
| Turn | 2 revolutions over 50 frames, speed 55%-145% of average (undulating) | 3 revolutions over 49 frames, fast then easing |
| Blows | each target once, 14/12/9/7% of the sword's damage by when it connects (x charge) | 8 blows 4%/2%, then a 12% blow (Smash: 4%) |
| Movement | rooted, no jumping out of it | **climbs**: starts at 7 m/s under 0.55 gravity (about 4 m, gravity easing back over 0.4 s after the last blow); **keeps the momentum it had** (sprint, jump, spin: the body keeps flowing) until it lands, the keys only steer it (14 m/s^2, half strength during the spin) |
| Holds the body | 74 frames | 91 frames; landing before then = 30 frames of landing lag |
| Reach | 2.8 m from the chest (the 2.1 m blade and the arm holding it out) | same |
| After | 0.4 s before another spin | once per time off the ground |

- **The last aerial blow launches** whatever can be launched (`ILaunchable`: pedestrians so far) up at 9 m/s and 3 m/s away. Smash's last hit
  is about knockback, not damage; in this game the damage had to be worth something too, hence 12% not 4%.
- **Bloodrage and Strength** scale every blow as they scale any melee hit.
- The spin used to be 1.2 s of a fixed 720 deg/s turn with the ordinary swing clip restarted every 0.3 s, a 4 s cooldown, and no rise.

### The look

- **The pose** is placed after the Animator in `PlayerLocomotion.ApplySwordSpinPose`: the sword arm straight out to the right with the blade
  carrying on the line of the arm (the hand is turned about the wrist to lay it there), rolled so the **cutting edge leads**; the free arm
  out the other side for balance; the support hand lets go of the handle. A charge draws the blade back 70 degrees. It blends in and out in
  about a seventh of a second. No clip does this (the swing clips are all two-handed chops).
- **The turn** is an exact function of time (`SwordSpinMove.AngleAt`) ending on whole revolutions, so the body finishes facing where it
  started. It turns counter-clockwise seen from above, so the sword sweeps forward across the front.
- **The swoosh** (`SwordSpinTrail`, built by `SwordSpinTrailBuilder`): three flat arcs of white light at shoulder height trailing behind the
  blade, sharing the sword wave's streak texture. It fades in quickly and dies away over about a fifth of a second when the body stops turning.
- `SwordSpinCapture` (an Explicit PlayMode test, needs `-Graphics`) renders the spin from the side and from above to `Logs/CityShots/spin-*.png`.

### Not done

- Other players do not see the arms or the swoosh: the pose and the trail are local (the animator state names that are replicated do not
  include them). The body's own turn and rise are ordinary movement and do replicate.
- Only `Civilian` is launchable. Crimson Claws and dummies take the damage and stay put.
- No sound yet, and no hit-stop on the blows.

## Tuning

Public fields on `MeleeAttacker` ("Sword Spin Attack"): `SpinReachMetres`, `SpinAttackCooldownSeconds`, `SpinAttackDamageScale`,
`AirSpinRiseSpeed`, `AirSpinGravityScale`, `AirSpinMoveFraction`, `AirSpinLaunchUp`, `AirSpinLaunchAway`. The timeline's numbers (frames,
revolutions, damage table) are constants in `SwordSpinMove`.
