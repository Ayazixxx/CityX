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

`BoomerangFlightTests` (the path, pure), `BoomerangPlayTests` (on a real body: out and back; a hit going out turns it back and it hits who it passes coming home; each pass unlocked is one more body going out; a wall turns it back with nobody behind it hurt; home through a wall, hitting who it passes; pushed away from the thrower; one out at a time; a swap and back while it is out; another player's copy), `ItemCardTests`. Pictures: `BoomerangCapture` (`Logs/CityShots/boomerang-*.png`).

# The Giant Boomerang

Owner, 2026-11-12: "make a giant version of the boomerang like the girl from inuyasha" (two pictures of the anime's bone boomerang), then, asked: **throw only** (no swings), **heavy and slow**, and of its size and carry, "make it about as tall as the player ... find out a good way to carry the large boomerang, maybe on the back?" ("there are going to be large greatswords, guns, etc."). While it was being built: "make an animation when you hold right click where it grabs the boomerang and does like an aiming it pose and aims in. also make it so you can press click and it throws it right away". `/equip giantboomerang`. Rare (my placement).

Our own design, not the anime's prop: a wide, thick bone-white blade 2.6 m tip to tip, leather wrapped at its grip near one end, round its elbow and near its other tip (`BoomerangBuilder.BuildGiant`, a stand-in until the owner's model).

## How it plays

- **On the back** (`Items/BackCarry`, the shared mount for big weapons): slung at a slant, the grip up behind the right shoulder, the length down across the back. It rides the spine, so it leans and turns with the trunk.
- **Hold right-click**: the right hand goes up over the shoulder, takes it off the back, and the body comes side-on into the aim (the left arm out at what it is thrown at, the boomerang cocked back flat behind the shoulder); the camera aims in as with any thrown weapon, and the body turns to where the camera looks (`LocomotionInput.ThrowAiming`). Let go without clicking and it is put back (the draw played backward).
- **Click while aiming**: the aimed throw, the hips, shoulders and arm unwinding and the arm sweeping it round flat; it leaves the hand 0.18 s after the click.
- **Click without aiming**: the quick throw straight off the back, over the shoulder for it and round through the aim in one go; it leaves the hand 0.41 s after the click.
- **In the air** (`BoomerangProfile.Giant`, `GiantBoomerangRules`, my numbers but those the owner chose): flat and spinning twice a second, 18 m/s out for 1.1 s, about 25 m in all (twice the wooden one's), 30 damage (three times), back at up to 24 m/s. Going out it turns back off the first body it hits (a body within 1.1 m of its path; since 2026-11-18, below: it used to cut on through everyone) or off a wall (met by its 0.3 m thick blade, so the ground under it never does). Every body it hits is thrown down limp, away from the thrower. **A guard stops it** (owner, 2026-11-12: "make it so the boomerang is blockable and when you block it, it doesnt ragdoll you"): a blow a held guard, a timed block, a shield or a barrier stops throws nobody down, and going out the guard turns it back as a wall does. Coming back it goes through walls and hits everyone it passes, as the wooden one does. It is caught (below).
- One boomerang out at a time, of either kind.
- **While it is out the hands are bare** (owner, 2026-11-12: "after throwing the boomerang, when you are unarmed, make it so you can punch when its mid air", "just make that apart of the base game"; both boomerangs): a click punches with the fists (`IEquippableWeapon.InHand` is false, the melee holds no weapon); right-click still counts toward holding the aim when it is caught. Caught, the hands hold it again. `BoomerangPunchRealInputPlayTests`.

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

Tests: `BoomerangFlightTests` (its range and numbers), `GiantBoomerangPlayTests` (on the back, drawn into the hand and put back, the quick throw, the hand out for it before it is home, caught in the hand and put back on the back, caught with right-click held and held in the aim, the aimed throw leaving sooner, turned back off the first body it hits, or the second with one pass; with every pass through two people, knocking them down, then turned back by a wall with nobody behind it hurt, another player's copy). Pictures: `GiantBoomerangCapture` (`Logs/CityShots/giantrang-*.png`).

## Hair under it on the back

Owner, 2026-11-12: "the hairstyles clip through the boomerang when its worn on the back ... is there a way to make any amount of hair that passes through it invisible on the other side". Slung on the back, the boomerang shows its board to `Items/BackClip` (its frame and its outline, a V from tip to elbow to tip, worked out by `BoomerangBuilder.BuildGiant`); before each frame the boards go to the shader as globals.

First it was **cut**: whatever of the hair was past the board's inner face was left out. Every way of drawing that line either left spikes poking out at the board's edges or showed a bald patch under it (the owner: "theres clearly a fucking baldspot"). Since the same day it is **pressed**, not cut (`View/Shaders/CityXHairPush.hlsl`, the hair's `_CITYX_HAIR` keyword, the same pass that keeps it out of the shirt, `docs/HAIR.md`). At first everything from the board's inner face out was moved flat to just under the inner face, 12 cm in for hair at its outer face: beside the board that pulled the hair into the neck and head and showed skin between them (owner: "without the push looks better there are clear bald spots in between the hair and boomerang on dreads and kick hairstyle"). Then only what came out of its outer face, up to 5 cm out, was put back just inside that face: the dreads' tips hanging farther out were left on the face as lone spikes. The owner then set the rule: "I just dont want the scalp visible from any angles and no dreads poking through and make it go back to normal as he pulls the boomerang away", "they can clip through slightly around the edges of the boomerang ... it gives the visual illusion that the hair is just wrapping around the boomerang slightly, but there should be no 'lone spikes' poking through".

So now whatever of the hair is out past the board's middle inside its outline, however far out, is laid on that middle, inside the board (its faces thin to its edges, so only the middle is inside it everywhere); within 4 cm past its edges partly, easing off, so the hair there seems to wrap round them. Hair on the body's side of the board's middle is never moved, so none goes into the head or neck and no scalp shows from any side. Nor is hair pressed into a head, and hair within 5 cm of the scalp less and less, none on it: just after it is put back on the back the head is still leaning into the board, and pressing pulled the hair off the scalp for a frame or two (owner: "there is a frame where the bald scalp is viewable ... i would much rather have a frame where the boomerang clips through the hair"). With 12 cm no scalp showed on any style but too much hair came through the board in those frames ("surely we can have less hair poking through"); with 5 cm the Kick hair shows none and the Dreads a speck (58 pixels, one frame), the Anime hair some (set aside by the owner for now). The heads are spheres `HairPush` sends with the body pieces. Then the hair is pushed out of the body. All passes, shadows included. In the hand or in the air nothing is pressed (`GiantBoomerangItem.LateUpdate` shows the board only while it is settled on the back). Up to eight boards at once; any big weapon on the back can show its own: its frame (middle at 0) and its outline, a V of two arms narrowing from the elbow to the tips (`BackClip.Show`).

Pictures: `BackClipCapture` (`backclip-*.png`, four long styles with and without, from behind, both sides, at the head and from above). It measures each picture again with the hair drawn magenta and with the board alone, and fails if, pushed, more than 10 pixels of **lone** hair are on the board's face (more than 8 pixels in from its outline, and joined by no run of hair to the hair off it: hair joined to it lies over the edge or hangs in front, which the owner allows), or more than 20 pixels of skin show in patches (5 pixels across) where the unpushed hair covered all round them. A second test throws it, catches it and puts it back, and measures every frame from the hand going out for it until it has lain on the back half a second, from the game's camera and three close views, for scalp showing where the hair covered it (Kick and Dreads; plain Kick frames every third frame, `backclip-catch-kickhair-*.png`). The numbers go to `backclip-measures.txt` and `backclip-catch-measures.txt`; a picture with any is saved again marked (`backclip-*-marked.png`, lone hair red, skin green).

## Striking a wall, the floor or a thing (2026-11-17)

Owner: "make it so the boomerang, both large and small one, when thrown against a wall or the floor or any type of object there is some sort of
bang noise and some impact fx". Whatever turns either boomerang back that is not a person (a wall, the floor, a car, a crate) now bangs and
bursts where it struck (`ThrownBoomerang.Bang`): a bang made in code (`BoomerangSounds.Bang`: a sharp crack over a thump from 110 Hz down to 55,
gone in a third of a second; the giant one plays it at 0.62 pitch, deeper), and `Character/Combat/ImpactBurst` laid on the surface whichever
way it faces: a white flash star, a ring snapping out flat on it, dark chips thrown out of it that fall, and grey dust drifting off (the giant
one's 1.8 times the size). A person struck keeps the thud it had. Other players: the `TurnedBack` event now carries where it struck (Origin)
and which way that faced (Direction), and their copy bangs and bursts there too. The giant one still flies through loose physics objects
(it is only turned back by fixed walls), so those do not bang.
Tests: `BoomerangPlayTests.AWallOrTheFloorItStrikesBangsAndBurstsWhereItStruckOnEveryScreen`, `...TheBangCracksAtOnceAndIsGoneInAThirdOfASecond`,
`...TheBurstsChipsFlyOutOfTheSurfaceWithinItsCone`, `GiantBoomerangPlayTests.WithEveryPassItCutsThroughPeople...` (the burst on the wall, none on the
people). Picture: `ImpactBurstCapture` (Explicit, -Graphics).

## Going out, the first body turns it back (2026-11-18)

Owner: "make it so the boomerangs including the giant one, return to the thrower on the first enemy it hits instead of traveling through a bunch
of them. the ability to continue after hitting someone will be something you can unlock in skills or possibly stats, and it may be either you
unlock it and it works as it does now where it travels through people but maybe it starts with just being able to continue after 1 person hit and
you can upgrade it further". Then, of a first go that also stopped it hitting on the way home: "it want it to hit people on the way back".

- **Now** (`BoomerangBlows`, pure): going out, the giant one turns back off the first body it hits, the nearest (it used to cut on through
  everyone until a wall), as the wooden one always did. Coming home both hit everyone they pass, as before. A blow a guard stopped turns it back too.
- **The upgrade to come** is one number on the thrower, `BoomerangThrower.PassesThrough`: how many bodies a throw goes on through going out
  after its first, before it turns back (0 now; `BoomerangBlows.Unlimited` is how the giant one flew before this day). Both kinds read it.
  Nothing sets it yet: whether it is a skill or a stat, and its steps, are the owner's to decide.
- Other players: nothing new is sent. The thrower's machine decides hits, and a turn back off a person was already told (`TurnedBack`, A 1).

Tests: `BoomerangBlowsTests`, `BoomerangPlayTests.TheFirstBodyItHitsTurnsItBackAndItHitsWhoItPassesComingHome`,
`...EachPassUnlockedTakesItOnThroughOneMoreBodyGoingOut`, `GiantBoomerangPlayTests.GoingOutItTurnsBackOffTheLastBodyItMayHit`.
