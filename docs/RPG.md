# The RPG (2026-11-19)

Owner: "make a basic rpg rocket launcher that is animated. look online for a free rpg/rocket launcher model with reload animations and try
to make some version of that".

`/equip rpg` (also `rocket`, `rocketlauncher`). Very Rare (the owner's rarity list puts the RPG there).

## What the research found

- **Free models**: none with a reload. An animated RPG-7 on CGTrader (3DRT, "modern firearms animated") is not free; the free ones (itch.io,
  CGTrader, Sketchfab) are static. None was downloaded: they were looked at for the shape, and ours is made from scratch, as the shotgun was.
- **The real launcher** (Wikipedia, "RPG-7"): a 40 mm tube 950 mm long, wood round its middle against the heat, the back flared into a
  blast cone, an optical sight (PGO-7) on the left; the PG-7V round's 85 mm warhead stands out of the muzzle, its motor and folded fins
  inside the tube. The booster throws it out at 115 m/s; the rocket motor lights about 10 m out and drives it to 295 m/s; the fuze bursts it
  at about 920 m if it has met nothing.
- **Loading** (Small Arms Defense Journal, "RPG-7 system primer"): the round goes in at the muzzle, indexed and held by its gasket, the
  fuze's cap comes off, the hammer is cocked, the safety off. The back-blast has a danger area of about 30 m behind in a 70-degree cone, the
  worst of it close in; firing from a small room is discouraged.
- **Games**: Call of Duty's and Battlefield's RPG-7s fire a rocket you can watch all the way, with a slight drop, and take 3 to 4.5 s to
  reload; splash damage falls off from the middle (Quake, Team Fortress 2), and the shooter is hurt by their own blast up close.

## How it plays

| | |
|---|---|
| Left-click | Fires the loaded round toward what the crosshair is on. One round: the reload starts the moment it fires (2.3 s) |
| Right-click | Shouldered, aimed down the tube, as a rifle (the tube on the right shoulder, beside the head) |
| The rocket | Leaves slow (38 m/s), the motor lights about 7 m out with a flame and a smoke trail and drives it on to 85 m/s, nearly straight (a slight drop); it bursts on whatever it meets, or by itself after 260 m |
| The burst | The grenade's explosion (Explosion: 110 at the middle over 7.5 m, bodies thrown, gasoline lit), and a body struck square takes 70 more. The shooter is hurt by their own blast close in |
| The back-blast | Anyone within 5 m behind the launcher (a 35-degree cone) is hurt (up to 35) and thrown, the more the closer; dust kicked up behind and a roar |
| Ammunition | One loaded, four spare |

All the numbers are mine (`RocketRules`, `FirearmProfile.RPG`).

## The model

Made in Blender from boxes, cylinders and cones (`CityX/SourceArt/RPG/rpg_model.py`), at the rifles' scale: the body (tube, blast cone,
two wooden heat guards with their bands, the trigger housing and raked wooden grip, iron sights, the optic on its bracket), the round (its
olive warhead, the brass fuze nose, the motor and folded fins inside the tube), the hammer and the trigger. `RPGBuilder` makes the prefab.
A stand-in, as basic as asked.

## The reload

Baked in Blender like the other guns' (`rpg_reload.py`, `rpg_build_all.py`, into `Resources/RPG/RPGReload.json`): the launcher comes off
the shoulder to the belly, muzzle up and swung to the left; the free hand goes to a pouch at the back of the left hip, brings a round round
in front, lines its tail up with the muzzle and pushes it home (the `seat`: loaded); back to the front guard, the right thumb cocks the hammer
(the `rack`); back onto the shoulder. 2.3 s. The same frames serve both of the game's reload names (a single shot is always empty when it is
reloaded).

## How it is held

As a rifle (`PlayerLocomotion.RifleAim`): the pose puts a gun's butt in the shoulder pocket; the launcher has no butt, so its "butt"
(`FirearmProfile.ButtLocal`) is a point under and ahead of where the tube rests on the shoulder, which lays the tube on top of the shoulder
beside the head. The support hand cradles the front guard as a shotgunner holds a forend.

## Other players

The rocket is on the body's `RocketLauncher` (an `IReplicatedAbility`, wired in `PlayerAbilityWiring`): where it left and which way, then where
it burst. Another player's copy fires the same round (a copy of its own launcher's loaded round) and bursts it there, hurting no one (the
shooter's machine dealt the blast, as with grenades); a copy that meets something first bursts there itself, and the word then shows nothing
more. The shot itself (flash, sound, the round gone from the muzzle, the back-blast's dust) is the gun's usual announcement; the reload is the
usual motion by name.

## Not done

- Lock-on, guided rockets, other warheads.
- Rockets to find and pick up (the four spare are all there is, as every gun's rounds are for now).
- The model is basic.

## Tests

`RPGTests` (EditMode: the flight, the back-blast's reach, the profile, the reload motion's events, a stranger's packet), `RPGPlayTests` (on
the real body: shouldered beside the head and on the crosshair; a rocket bursting at a wall and hurting who stands by it, the next round
loaded; the back-blast behind and not beside; another player's copy). Pictures: `FirearmCapture.RenderTheRPG` (Explicit, -Graphics).
