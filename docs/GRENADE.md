# Frag grenade and explosions

## 2026-10-29: built to the owner's ask

> "after that work on explosions try making a grenade that looks really good research video game grenades and make it look AAA game level"

`/equip grenade` gives you a stack of them. Hold right-click to aim and click to throw, or just click: the grenade leaves the hand with its
fuse lit and the next one comes into it.

## What games do (the research)

- **The grenade** (the Godot game had one, `weapons/grenade.gd`, `weapons/thrown_grenade.gd`; ported here): a real rigid body that bounces
  off walls and rolls, a fuse that starts at the throw (2.4 s: you can bank one round a corner, and a bad throw is punished by the clock),
  a blinking fuse light that quickens as it burns down -- the only warning anyone gets.
- **An explosion is layers, all short-lived and all at once** (Battlefront II's, taken apart frame by frame by Simon Schreibt; the Godot
  game's own explosion had four of them): a **flash** that lights the surroundings for a few frames and falls off on a curve (a linear fade
  reads as a light being dimmed, not a detonation); a **fireball** of fire puffs bursting out fast and slowing, white-hot to orange, then
  quickly down to dark smoke (some puffs still burning while others are smoke; lingering in deep red, the ball read as one flat red blob);
  a **shockwave** ring outrunning the damage (the visible edge of a blast outruns its lethal one); **sparks** flung out and falling;
  **debris** chunks tumbling with spin and bouncing; a **smoke** column lit from inside while young, drifting away over seconds; a **scorch** left on the ground; **camera shake** by distance; a crack, a thump and a rumble.
- **The blast**: damage falling off from the middle (the Godot game: 25% left at the rim), an impulse that throws bodies and loose things
  away and UP (thrown flat along the ground reads weak), walls shielding (the Godot game left this out: here a wall between you and it
  saves you).

Sources: the Godot game's `weapons/explosion.gd`; Simon Schreibt, "Star Wars Battlefront II: explosion" breakdown (simonschreibt.de); the
fire's own research (docs/FIRE.md).

## What it is (`GrenadeRules` for the numbers)

- **In the hand** (`GrenadeItem`, `Grenade.prefab` from `GrenadeBuilder`): an olive egg with a grey fuse cap and spoon, 1.3 times life.
  A stack of 10 (`ItemRegistry`).
- **In flight** (`ThrownGrenade`, `ThrownGrenade.prefab`): 0.45 kg, thrown at 17 m/s lifted 8 degrees (a lob), bounce 0.35, friction 0.8,
  never touching its thrower for its first 0.3 s; the fuse light blinking from 2 to 14 times a second with a little red light.
- **The blast** (`Explosion.Blast`): 7.5 m; 110 damage at the middle (a kill), a quarter at the rim, none past it; nobody a wall stands in
  front of; bodies it does not kill thrown down, away and up (11 m/s, 6 up, by how near); loose things pushed out to 1.4 times its reach;
  gasoline in it lit; enough to kill outright bursts a body into gore (docs/DISMEMBERMENT.md: a blast has no edge); the thrower is paid
  the XP.
- **The look** (`Explosion`, every machine): the flash (a light of 130 falling off over 0.35 s on a cubic, and a white-hot core for 0.12 s),
  the fireball (36 fire puffs, 7-13 m/s out, slowing to a twelfth, 2-3.5 m and growing, white-hot to orange to smoke-dark over 0.5-1.2 s each), the
  shockwave ring (to 1.6 times the reach in 0.22 s) and dust rolling out over the ground behind it (`DustBurst`, the giant's), 70 sparks
  (12-28 m/s, falling and bouncing), 28 chunks of debris tumbling and bouncing, the fire's own tongues licking up and its smoke column
  (`FireScene.Explode`: lit orange from inside while young, gone over 4-7 s), a scorch on the ground (`FireMarks.Scorch`, faded after a
  minute as the fire's are), the camera of a player near it shaking, and a boom made in code (`ExplosionSounds`).

Its pictures, materials and sounds are made ahead, by the first body to come in (`GrenadeThrower` calls `Explosion.Prewarm` while the game
loads): made at the first blast, they stopped the game for about 50 ms just as the grenade went off (measured in the editor, where the
first blast cost 220 ms in all before this and the fire's noise, docs/FIRE.md, were moved off it; the warm blast takes about 1 ms).

## Other players (docs/MULTIPLAYER_STATE.md)

`GrenadeThrower` (an `IReplicatedAbility` on the body, "grenade"): **Thrown** (where from, which way, how fast: their copy of the grenade
flies the same way, a copy that hurts no one) and **Exploded** (where: their copy goes off there, or by itself half a second past its fuse
if the word is late). The blast is dealt on the thrower's machine and reaches other players through their copies, as every blow does.

## Tests

`GrenadeRulesTests` (the falloff, the blink, the lob), `GrenadePlayTests` (a body coming in makes the explosion ahead; thrown, it bounces and goes off on time, hurting the body in the
open and throwing it down, sparing the one behind a wall; the explosion's layers and its end; another player's copy); pictures
`GrenadeCapture` (`grenade-*.png`: frame by frame, day and night).
