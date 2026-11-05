# Damage numbers

Owner, 2026-10-24: "whats a way we include the damage numbers you are doing on screen without it being in the way and annoying", then
"try to recreate it how fortnite does it, make it turn more red based on the amount of the enemies healthbar you are taking away with that
1 attack. so if your going against someone much weaker thank you yours will be mostly red and if its vise versa then yours will be mostly
white or maybe you will get some red if you do some big damage somehow like a headshot or crit or something".

## What it does

- **Fortnite's numbers.** Every blow you land shows its damage at the spot it hit. The number pops out 1.6x its size and settles in 0.12 s,
  rises and drifts aside, stays solid for 0.45 s and is gone at 0.7 s. Bold, with a dark edge so a white number reads on a bright wall.
  Each hit its own number, scattered a little so they do not pile up.
- **Red by the share of the bar.** The colour is the share of the target's whole health bar that one blow took (`DamageDealt.ShareOfBar`):
  white under 3%, full red from half the bar, smooth between. The size grows with it (24 to 34 design pixels) and a heavy blow is 1.25x
  bigger still. The same cut of 22 is red on a 40-health opponent, pink on a 100-health one and white on a 1000-health one, and a big blow on
  anyone shows red. (The game has no headshots or crits yet; when it does, their bigger damage turns them red by itself.)
- **Only yours, only on your screen.** The numbers are for whoever dealt them, so nothing is sent to other players; each sees their own. A
  hit forwarded to another player's machine is not shown there.
- **Not in the way.** Under the HUD, gone within a second, smaller with distance (to 0.65x at 60 m) and none past 60 m, at most 24 at once
  (the oldest goes). Blocked and parried blows show nothing.
- **A burn adds up.** Hits on one target within 0.08 s of each other add into one number, for up to half a second: a flamethrower (a little
  every frame) or the hits of one moment show one growing number; an automatic's bullets, a tenth of a second apart, each get their own, as
  in Fortnite.

## How

- `Character/Combat/DamageDealt.cs`: the report of a blow that landed (striker, target, point, amount, share of the bar, kind, heavy) and
  `IHealthBar` (the player, civilians, the Crimson Claw, the sparring dummy). `DamageContext.Strike` raises `DamageDealt.Landed` when the
  striker is named. Every weapon and spell names its wielder: the fists and the sword (`MeleeAttacker`, `SoulsSwordCombat`), the guns and
  the flamethrower, the thrown bottle, the fireball, ice shards, the sword wave, acid rain and Self Smite (the projectiles carry their caster).
  Poison does not show numbers.
- A blow that kills pays its striker what the kill is worth (`IXpReward`), in the same place (`DamageContext.Strike`), once. Each weapon
  used to pay for itself, and most spells never did: a kill by a speck, a boomerang, a sword wave, acid rain, a beam, a stream or fire
  paid nothing (2026-11-05). A hit forwarded from another player's machine names no striker; that kill is credited by `EnemyNetwork`.
- `UI/Hud/DamageNumberLook.cs`: the colour, size, pop, rise, fade and distance scale, as plain functions.
- `UI/Hud/DamageNumbers.cs`: the view. Its own overlay canvas (sorting 30, under the HUD's 40; a canvas under the HUD's would take its
  sorting and fades), scaled like the HUD, labels pooled. `PlayerHudBuilder.AddToScene` adds it to the scene. It shows the blows of the
  body this machine's input drives (or `Player`, if set).

## Tests and renders

- `DamageNumberLookTests` (EditMode): white to red by the share, monotonic; weak opponent red, strong white; size; pop, rise and fade
  within a second; distance; whole numbers.
- `DamageNumbersPlayTests`: your blow shows at the spot it hit and someone else's (or a forwarded one) does not; red on a weak opponent,
  white on a strong one; a burn's ticks add into one number while an automatic's bullets each get one; gone within a second; a blocked blow
  shows nothing; a real sword cut names its wielder.
- `DamageNumbersCapture` (Explicit, `-Graphics`): three swordsmen of 40, 100 and 1000 health take the same cut, then a heavy lands on the
  middle one, through the game camera in the real city: `Logs/CityShots/damage-numbers-1..3.png`.

## Sources

- GameWith, [Fortnite damage number colours](https://gamewith.net/fortnite/article/show/182) (white health, blue shield, yellow headshot)
- WoW combat text addons: [Splatter Combat Text](https://www.curseforge.com/wow/addons/splattercombattext),
  [nugsCombatText](https://www.curseforge.com/wow/addons/nugscombattext) (merging hits, crits bigger)
- [Neverwinter's combat text changes](https://forum.arcgames.com/neverwinter/roletracker/jump/1266779) (smaller numbers, bigger for big hits)
- bugnet, [fixing overlapping damage numbers](https://bugnet.io/blog/how-to-fix-floating-damage-numbers-overlapping-into-an-unreadable-blob)
