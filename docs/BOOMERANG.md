# The Wooden Boomerang

Owner, 2026-11-05: "make a boomerang weapon that functions exactly like the terraria boomerang but in 3d". `/equip boomerang`. Uncommon, as the rarity list in `GAME_DESIGN.md` has it.

## Terraria's rules, and ours

From the [Wooden Boomerang](https://terraria.wiki.gg/wiki/Wooden_Boomerang) and [Boomerangs](https://terraria.wiki.gg/wiki/Boomerangs) pages of the Terraria wiki. A Terraria player stands 3 tiles tall and ours 2.6 m, so a tile is about 0.87 m (`BoomerangRules`).

| Rule | Terraria | Here |
|---|---|---|
| Damage | 10, of a player's 100 | 10 of 100, plus Strength as a thrown bottle has it |
| Speed out | velocity 6.5 | 20 m/s, no gravity |
| How long out | 0.5 s, then it slows and turns back | the same |
| Range | about 13.6 tiles | 12 m |
| Speed back | up to 46 mph | up to 29 m/s, steering for wherever the thrower now is |
| Going out it meets something | turns straight back (not on the wiki: from memory of the game's code) | the same, a person or a wall |
| Coming back | pierces without limit, passes through blocks | the same; the same body again only after 0.33 s |
| Knockback | always away from the thrower, whichever way it flies | the hit's "from" is the thrower, so the stagger pushes that way |
| How many | "most boomerangs cannot be fired again before returning" | one out; clicks do nothing till it is caught |
| Use time | 20 ticks | a throw at most every 1/3 s |
| Lost | after a minute, or 187.5 tiles off | a minute, or 163 m |

3D's own additions, all mine: it is aimed at what the crosshair is on, since the camera looks past the shoulder. Going out, it cracks a window it hits, as a fist does, and turns back. Its disc is tipped, so its spin reads from behind, from the side and from above. It is 70 cm tip to tip, well over life size, so that it reads in the air at its range.

Terraria's melee speed bonus and its slowing in liquids are not in.

## The pieces (`Weapons/Thrown`)

| Piece | What |
|---|---|
| `BoomerangRules` | the numbers |
| `BoomerangFlight` | the path, pure |
| `ThrownBoomerang` | one in the air: the path, a sphere swept along it each frame for hits, the spin, the whirr |
| `BoomerangThrower` | on every body: whether it is out (it outlives a swap to another weapon), the throw, and word to other players |
| `BoomerangItem` | in the hand; hidden while it is out. A plain click throws it, as a grenade's does |
| `BoomerangBuilder` | the stand-in model, a carved V with red tips, drawn in code |
| `PlayerLocomotion.Throw.cs` | the arm's swing: code-posed over the Animator, a first go, not a Blender clip |


## Other players

`BoomerangThrower` is an `IReplicatedAbility`: the throw (their copy swings its arm and flies its own), what turned it back, and the catch. The copy hurts no one: the thrower's machine decides hits, and they reach other players' bodies as every blow does.

## Tests

`BoomerangFlightTests` (the path, pure), `BoomerangPlayTests` (on a real body: out and back; a hit going out turns it back and it can hit again coming home; a wall turns it back with nobody behind it hurt; home through a wall, hitting who it passes; pushed away from the thrower; one out at a time; a swap and back while it is out; another player's copy), `ItemCardTests`. Pictures: `BoomerangCapture` (`Logs/CityShots/boomerang-*.png`).

# The Giant Boomerang

Owner, 2026-11-12: "make a giant version of the boomerang like the girl from inuyasha" (two pictures of the anime's bone boomerang), then, asked: **throw only** (no swings), **heavy and slow**, and of its size and carry, "make it about as tall as the player ... find out a good way to carry the large boomerang, maybe on the back?" ("there are going to be large greatswords, guns, etc."). While it was being built: "make an animation when you hold right click where it grabs the boomerang and does like an aiming it pose and aims in. also make it so you can press click and it throws it right away". `/equip giantboomerang`. Rare (my placement).

Our own design, not the anime's prop: a wide, thick bone-white blade 2.6 m tip to tip, leather wrapped at its grip near one end, round its elbow and near its other tip (`BoomerangBuilder.BuildGiant`, a stand-in until the owner's model).

## How it plays

- **On the back** (`Items/BackCarry`, the shared mount for big weapons): slung at a slant, the grip up behind the right shoulder, the length down across the back. It rides the spine, so it leans and turns with the trunk.
- **Hold right-click**: the right hand goes up over the shoulder, takes it off the back, and the body comes side-on into the aim (the left arm out at what it is thrown at, the boomerang cocked back flat behind the shoulder); the camera aims in as with any thrown weapon, and the body turns to where the camera looks (`LocomotionInput.ThrowAiming`). Let go without clicking and it is put back (the draw played backward).
- **Click while aiming**: the aimed throw, the hips, shoulders and arm unwinding and the arm sweeping it round flat; it leaves the hand 0.18 s after the click.
- **Click without aiming**: the quick throw straight off the back, over the shoulder for it and round through the aim in one go; it leaves the hand 0.41 s after the click.
- **In the air** (`BoomerangProfile.Giant`, `GiantBoomerangRules`, my numbers but those the owner chose): flat and spinning twice a second, 18 m/s out for 1.1 s, about 25 m in all (twice the wooden one's), 30 damage (three times), back at up to 24 m/s. Going out it cuts on through the people it meets (a body within 1.1 m of its path); only a wall turns it back (met by its 0.3 m thick blade, so the ground under it never does). Every body it hits is thrown down limp, away from the thrower. **A guard stops it** (owner, 2026-11-12: "make it so the boomerang is blockable and when you block it, it doesnt ragdoll you"): a blow a held guard, a timed block, a shield or a barrier stops throws nobody down, and going out the guard turns it back as a wall does. Coming back it goes through walls and hits everyone it passes, as the wooden one does. It is caught (below).
- One boomerang out at a time, of either kind.

## The moves

Four full-body clips made in Blender (`CityX/SourceArt/GiantBoomerang`, the punches' solver and planted feet): `giant_draw` (its last frame is the aim, held), `giant_throw_aimed`, `giant_throw_quick` and `giant_catch` (its last frame is the aim too). Rebuild: `blender --background --factory-startup --python CityX/SourceArt/GiantBoomerang/giant_boomerang_build.py -- --render <folder> --export CityX/Assets/CityX/Character/Resources/GiantBoomerang/GiantBoomerangMotion.json`. The grab and release moments are `GiantBoomerangMoves`' constants: keep them in step with the script.

`GiantBoomerangPoseDriver` plays them after the Animator. The draw, the aim and the aimed throw are on the upper body, so the legs walk on under them; a quick throw from standing is the whole body with its step. The grip in the hand was measured from the aim's hand on the real body (`GiantBoomerangCapture`), so in the aim it lies flat out behind; taken off the back or put back, it glides over 0.12 s instead of jumping.

## The catch

Owner, 2026-11-12: "on the return of the boomerang, instead of it just snapping into place on his back, he actually catches it and puts it back or just holds it in his hand if you holding right click".

- It steers for the catching hand, not the chest. A moment before it gets there (`GiantBoomerangMoves.CatchSeconds`, 0.18 s, `ThrownBoomerang.ComingIn`) the hand goes out for it, ahead and to the right at shoulder height, and waits there if it is late. It is caught with its middle a metre from the hand (where it is held near its end), or as soon as it starts away again from within 2.5 m (it passed the hand).
- Caught, it glides over 0.12 s from where it was flying, spinning, into the grip; the arm gives with its weight and comes round into the aim.
- Then: right-click held, the aim is held, ready to throw again (right-click pressed while it was still out counts: it is remembered, and nothing is drawn while it is out); not held, it is put back on the back (the draw played backward).
- A throw cannot start during the catch (0.45 s). Owner, 2026-11-12: "id like it a bit faster": the catch clip went from 0.6 s to 0.45 s and putting it back on the back plays 1.4 times as fast as the draw (`PutAwayPace`). Lost (a minute, or 163 m), it is simply back on the back.
- Other players: each screen catches its own copy as it comes home; the owner's word that it was caught (`Caught`, A 1) makes a late copy caught, not lost.

## Other players

`BoomerangThrower` tells them of the aim (`GiantAim`), of the throw starting (`GiantThrowStarted`) and of the release (`Thrown` with `N` 1): their copy of the body draws, aims and throws it with the same clips, and the giant one flies there, hurting no one.

## Not yet

- The middle of the throw's swing is quick and has not been looked at closely frame by frame; the boomerang tips up for a few frames on its way round.
- No strap across the chest on the body.

Tests: `BoomerangFlightTests` (its range and numbers), `GiantBoomerangPlayTests` (on the back, drawn into the hand and put back, the quick throw, the hand out for it before it is home, caught in the hand and put back on the back, caught with right-click held and held in the aim, the aimed throw leaving sooner, through two people and knocking them down then turned back by a wall with nobody behind it hurt, another player's copy). Pictures: `GiantBoomerangCapture` (`Logs/CityShots/giantrang-*.png`).
