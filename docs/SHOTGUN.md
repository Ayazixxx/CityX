# The pump shotgun (2026-11-18)

Owner: "make a shotgun with reload animations and parts to it that move like our ak47 and pistol. find a good reference to work with see if
there any free 3d shotgun models with reload and remake something similar in our game. also research how games do shotgun mechanics vs other
guns and implement what you learn".

`/equip shotgun` (or `pump`). Left mouse fires, R loads, right mouse aims. Rare (my placement, beside the AK-47).

## What the research said (sources at the bottom)

**What makes a shotgun a shotgun in games.** Not one bullet but several pellets a shot, each resolved on its own: 9 in PUBG (every shotgun)
and Counter-Strike's Nova, 10 in Team Fortress 2 and Fortnite's pump gun. Two things keep it a short-range gun, and games use both together:
the pellets spread, so fewer land the further off the target is, and each pellet loses damage with distance (the Nova: 0.7 of it every 500
units; an old Fortnite pump: all of it to about 7.7 m, 70% at 18 m). Up close the whole pattern on one body is the hardest single hit in the
game (the Nova 9 x 26, a Fortnite pump 92 of a player's 100); the price is the wait before the next shot (the Nova 68 shots a minute; a
Fortnite pump 0.7 a second).

**Fixed or random spread.** Team Fortress 2 always sends one pellet down the crosshair and, with its competitive setting, the rest in a fixed
grid; Quake III lays its pellets on rings. Random spread makes the same shot hit or miss by luck. A fixed pattern can be learnt, as the AK's
spray can.

**The reload.** A tube magazine is filled one shell at a time, about half a second each (the Nova 0.5 s a shell; filling a Fortnite pump
takes 4.4 to 5.7 s), and the loading can be broken off at any shell by firing: top up when you can, shoot when you must. Some games resolve a
whole blast on one body as a single hit rather than a hit a pellet.

**How a pump gun is really loaded** (Lucky Gunner, Shooting Illustrated): the strong hand keeps the gun up by its grip; the support hand
brings each shell up under the receiver and the thumb pushes it forward through the loading port until the latch catches it. A gun run dry
needs its action worked to chamber the first round.

**Free models with a reload.** None was both free of credit and a pump gun with a shell-by-shell reload: a rigged Remington 870 on itch.io
(CC BY 4.0, credit required, no reload listed), a rigged Remington Model 31 on Sketchfab (CC Attribution), an animated lever-action on
OpenGameArt (CC BY), paid Remington 870s with "Reload Start / Reload One / pump" clips on CGTrader. So, as with the AK-47, ours is made from
scratch and nothing was downloaded: the real gun's measurements for the shape, the real technique and those clip lists for what the reload
shows.

## What was built

### The model (`CityX/SourceArt/Shotgun/shotgun_model.py`, from scratch in Blender, headless)

A 12-gauge pump gun of the Remington 870 / Mossberg 500 family: 18.5 inch barrel, the magazine tube under it, a ribbed forend on the tube,
polymer stock on a pistol grip (so the one rifle hold fits it), times 1.45 as the AK-47. Parts: **Body**; **Pump** (the forend, its two action
bars and the bolt that shows in the ejection port: one piece, 12 cm of travel); **Shell** (one cartridge, red hull and brass head, resting
inside the loading port: the reload carries it); **Trigger**. Its points are in `Shotgun.points.json`. 4 parts, flat colours, no textures.

### How it fires (`FirearmProfile.Shotgun`, `PelletPattern`, `PistolWeapon.Fire`)

| | |
|---|---|
| Pellets | 9 a shot, 12 damage each (108 for a whole pattern; the AK-47 is 34 a round, the Glock 20) |
| The pattern | Fixed (`PelletPattern.Offset`): one pellet down the crosshair, 2 on a ring at 0.45 of the cone, 6 on its edge. The cone is 3.2 degrees to its edge from the hip, 0.8 of that aiming: a body-wide pattern at about 8 m |
| Distance | Each pellet keeps all its damage to 10 m and falls in a straight line to 35% at 30 m (`FirearmProfile.DamageShareAt`, from the muzzle); it reaches 45 m |
| One blow | Whatever pellets land on one body are added up and land as ONE blow: one damage number, one roll against a block or armour, one wound |
| Each pellet | Is its own three-trace hitscan and its own tracer; makes its own hole in a window and goes on through it at 90% (docs/FIREARMS.md) |
| The hold | The left hand cradles the forend, palm up under it, the fingers up its far side, the thumb along the near one (owner, with a photo: "the left hand is clipping through", "rotate the left hand so its holding that reload part from the bottom"): `CradleFingersLocal`, `CradlePalmLocal`, the reload's own hand on the forend (`on_forend`), so a reload starts and ends where the hold is. The AK-47 keeps its old hand (its fingers also stand up past the top of its handguard: not asked) |
| The pump | The forend is pulled back 0.2 s after the shot and shoved home over 0.36 s (`ActionDelaySeconds`, `ActionCycleSeconds`, `ActionBackAt`), the support hand riding it; the spent shell (red, not brass) flies as it reaches the rear, with the rack's sound. The next shot 0.85 s after the last: about 70 a minute |
| The kick | 4.5 degrees up a shot (the AK's first is 0.55), a heavy shove back |
| Ammunition | 6 in the tube, 24 in reserve |
| The other spread | The Glock's and AK's running, jumping and spraying cone moves the whole pattern (Accuracy narrows it), as it moves their one bullet |

The damage, the cone and the falloff are mine: the owner has not set them.

### The reload (`shotgun_reload.py`, `GunReload`'s shell stages, `PistolWeapon.PlayShellStage`)

Not one motion but four pieces the gun strings together, each ending in the pose the next begins in:

| Piece (name / segment) | Seconds | What |
|---|---|---|
| `gun_shells_start` / `start` | 0.40 | The support hand leaves the forend for the shells at the belly; the gun drawn in and rolled so the loading port turns toward that hand |
| `gun_shell` / `shell` | 0.57 | A shell taken, brought up under the receiver across the fingers, laid at the port and thumbed forward into the tube (`seat`: one more round); the hand back for the next. Once for every shell |
| `gun_shells_end` / `end` | 0.40 | The hand back to the forend, the gun back in the shoulder |
| `gun_shells_end_empty` / `endempty` | 0.67 | The same for a gun that had been run dry: the forend pulled back and shoved home first (`rack`) |

- R starts it; it goes on until the tube is full or the reserve is empty. Filling an empty tube takes 4.5 s.
- **A shot breaks it off** as soon as there is a round in the gun (from the first moment when it was not dry).
- Each piece is announced by name as it starts (`PlayLocalState`), so another player's copy plays the same pieces. Only the first eases in
  from the aim and only the last eases out to it (`GunReload.Stage`); a copy holds a piece's last frame for up to 0.3 s for the next one's
  name to arrive (`GunReloadDriver.HoldForNextSeconds`).
- The sounds are the synthesised clicks (a shell going in is the magazine's click; the pump is the rack).
- A body with no motion (a bare test rig) runs the same pieces on timers (`ShellStartSeconds`...).

### Other players

The shot's flash, crack and tracers (every pellet of the same fixed pattern: nothing is sent but the shot's name), the pump worked a moment
after with the copy's hand on it and the shell thrown (`FirearmEffects.WorkAction`, `Later`: a copy's gun has its scripts switched off), and
the loading piece by piece. The damage goes through `NetworkedDamage` as every blow does. The riot shield counts it as a long gun.

## Rebuild

```
"C:/Program Files/Blender Foundation/Blender 4.2/blender.exe" --background --python CityX/SourceArt/Shotgun/shotgun_build_all.py -- --no-body
```

Add `--render <folder> [frames...]` for check images, then **CityX > Rebuild Firearms** (or the scene build). The rifle stance the AK-47's
and the shotgun's reloads share is `CityX/SourceArt/Firearms/firearm_rifle_stance.py`.

## Not done

- **The shot's sound** is the pistol's clip pitched down to 0.6; a shotgun recording should replace it. The pump and the shells are the old
  synthesised clicks.
- **No knock-back or stagger** from a close blast (a bullet interrupts nothing in this game). Up close it is only damage.
- **Balance is untested by play**: guns already beat a lone melee enemy before he arrives (docs/PLAYTEST_BOT.md), and this one hits hardest.
- The model is plain (boxes and cylinders). The shells in the reserve are not shown on the body (a belt or a side saddle).
- Enemies do not use it.

## Tests

`ShotgunTests` (the pattern, the falloff, the profile and its pump's timing, the pieces' names, the baked motion's four pieces joining in
the same pose with the shell in the hand between grab and seat, the built prefab), `ShotgunPlayTests` (up close the whole pattern as one
blow, far off fewer and softer; the pump after the shot with the hand on it and the shell flying as it comes back; four shells loaded one at
a time, each piece announced, the arms never letting go between; a dry gun pumped at the end; a shot breaking the loading off; another
player's copy pumping and loading by name). Pictures: `FirearmCapture.RenderTheShotgun` (Explicit, -Graphics) writes
`Logs/CityShots/firearm-shotgun-*.png`.

## Sources

- [Shotguns -- PUBG Wiki](https://pubg.wiki.gg/wiki/Shotguns) (9 pellets a round)
- [Shotgun -- Team Fortress 2 Wiki](https://wiki.teamfortress.com/wiki/Shotgun) (10 pellets, one always down the crosshair, fixed spread, per-pellet falloff)
- [Shotgun (Q3) -- Quake Wiki mirror](https://breezewiki.discard.no/quake/wiki/Shotgun_(Q3)) (pellets on rings)
- [Nova -- csdb.gg](https://csdb.gg/weapon/nova) and [Nova -- SteamAnalyst](https://www.steamanalyst.com/weapons/stats/nova) (9 x 26, 68 RPM, about 0.5 s a shell)
- [Pump Shotgun -- Fortnite Wiki](https://fortnite.fandom.com/wiki/Pump_Shotgun) and [Fortnite Pump Shotgun stats -- Inverse](https://inverse.com/article/51282-fortnite-pump-shotgun-stats-rarity-and-damage) (10 pellets, 5 shells, 0.7 shots a second, 4.4-5.7 s to reload)
- [Warframe forum: shotgun mechanics and falloff](https://forums.warframe.com/topic/327104-opinion-a-quick-spheel-on-shotgun-mechanicsfalloff/) (a blast resolved as one hit; spread against falloff)
- [How to reload a defensive shotgun -- Lucky Gunner](https://www.luckygunner.com/lounge/reload-defensive-shotgun/) and [How to Reload a Shotgun: Tips From the Pros -- Shooting Illustrated](https://www.shootingillustrated.com/articles/2011/3/24/how-to-reload-a-shotgun-tips-from-the-pros) (the support hand feeds the tube from underneath; an empty gun is chambered first)
- [Remington Pump-Action Shotgun -- itch.io](https://gintoki1234.itch.io/remington-pump-action-shotgun), [Remington Model 31, rigged -- Sketchfab](https://sketchfab.com/3d-models/shotgun-remington-model-31-in-3-types-rigged-d95413fcdc9844aa9a09c413dfa4b293), [Animated shotgun -- OpenGameArt](https://opengameart.org/content/animated-shotgun), [3DRT Remington 870 -- CGTrader](https://www.cgtrader.com/3d-models/military/gun/3drt-modern-firearms-hd-remington-870) (the free and paid models looked up; none downloaded)
