# Dismemberment

Owner, 2026-10-29: "i want to add dismemberment to the game. this is a pretty good garrys mod dismemberment mod you can use as a guide
https://steamcommunity.com/sharedfiles/filedetails/?id=2963761869 . make it so overkill damage causes dismemberment, make it so decapitation
or limbs being cut off only happens when being hit with a sharp object like sword or something, otherwise they kind of just explode into
gore."

## What was looked at

- **The guide**: the Workshop page is "Dismemberment Gore Reconstructed" (Steam would not serve its description to a fetch; its discussion
  page confirms the name). Mods of its kind cut the hit body part off on a killing blow and spray gore; blunt and explosive deaths gib.
- **How it is done on skinned bodies** (Unity Asset Store dismemberment tools, Unreal's skeletal mesh partitioning, Babylon.js and Assimp
  notes): split the mesh by bone weights (each vertex goes with the bone that moves it most), freeze the cut-off part in its current pose
  (bake it) so it can fly as a plain mesh, and cover the hole with a cap at the joint.

## The rules (`DismemberRules`, pure)

Then, the same day: "i want it so only overkill damage dismembers people who are alive. so if your killing blow does way more damage then
the amount of health they had left then it dismembers. but also if you just keep hitting or shooting the dead body then it will occur then
as well".

- **A living body** comes apart only on a **killing blow** that does way more than it had left (`OverkillNeeded`): at least as much again
  (double what was left), and at least 5, or 5% of its whole health if that is more. A body on 10 needs a blow of 20; one on 30, a blow of
  60. So about half the kills by any weapon (the health left before a killing blow is anything up to the blow), more when a big blow lands
  on a body already low. A bare kill leaves the body whole. A tick of fire, poison or acid (under 5 a tick) never takes anything, and
  neither does a fall.
- **A corpse** takes every blow on it (`PlayerVitals.CorpseStruck`, `Civilian`; it takes no damage and pays no XP): each adds to what the
  part it lands on has taken, and the part comes off once that reaches what it can take (`Toughness`): the head 20% of the body's whole
  health, an arm or leg part 15%, the torso 40% (on 100: two club blows to the head, one sword cut or pistol shot to a limb, two shots to
  burst the chest). A killing blow too small to take anything leaves its excess on the part it hit. Swings, the soulslike sword, arrows and
  the sword's wave reach a corpse while it has something left to lose (`Dismemberer.TakesCorpseHits`; guns always did); one with nothing
  left is passed over, as before.
- Parts come off one at a time, as many as are hit: an arm off at the shoulder takes its forearm, the torso takes all that is left. A blow
  where a part was lands on the nearest part still on.
- An **edged** blow (the combat sword and the spike sword, and everything they do: the swings, the soulslike attacks, Octaslash, the stab
  dash, the sword spin, the crescent wave; `DamageContext.Edged`, opened where a blade strikes; `IEquippableWeapon.IsEdged`, from
  `ItemRegistry.HasEdge`) **cuts** the part it hit clean off; anything else (fists, the bat, the crowbar, a bottle, bullets, arrows, spells)
  **bursts** it into gore. A **bloodless** blow (the water jet, fire) takes nothing.
- **Which part**: the bone segment nearest where the blow landed (the head, an upper or lower arm, an upper or lower leg, the torso). A cut
  on the torso cuts at the nearest joint instead (the neck takes the head, a shoulder the arm, a hip the leg): a body is not cut in half. A
  burst on the torso takes the whole body. A blow with no point of its own lands on the chest.

## The look (`Dismemberer`, `MeshSevering`, `Gore`, `StumpBleed`, `Debris`)

- **A cut**: every skinned mesh on the body (the skin, the clothes) loses the triangles the cut-off bones carry, along the line where those
  bones take over (`MeshSevering`; the body keeps every vertex and its weights, so it still follows the skeleton). Those triangles, baked in
  the pose the body is in, fly off as one piece (a rigid body with a box round it), with what rides the bones (the skull, hair, a hat) as
  still copies. Something held in a cut-off hand stays where it is: the death drops it as it always did. The body keeps a stump at the cut
  (a wet red cap the width of the cut, with a nub of bone) that spurts blood for 3.5 s; the piece has one too. The cap is 6% wider than the
  limb there and its dark ring of blood 10% wider again (`Gore.CapOverLimb`, `CollarOverCap`): the limb's radius is the mean distance of the
  cut's corners from its middle measured across the way the part came off, since the cut zigzags along the limb between rows of corners
  (counted straight, with a 20% margin on top, the cap came out 1.3 to 1.7 times the limb's width; owner, 2026-11-07: "way larger than the
  circumference of the limb"). Close up, the skin's torn edge shows round the cap, most at the shoulder. The head is a mesh of its own
  sitting on the shoulders (this body has no neck to cut), so taking it off cuts no skin: its stump goes on the skin where the head met it,
  the top of the shoulders (the first render had it floating at the head's pivot, a quarter of a metre up).
- **A burst**: the part's triangles and riders are gone, and a spray of chunks (lumpy flesh and dark flesh, every fourth a shard of bone)
  is thrown out of its middle with blood; the head throws 9 (13 cm), a limb 7 (12 cm), the whole body 24 (16 cm). The chunks never knock into
  the body they came out of. A whole body that bursts keeps its hidden ragdoll on the floor, so what follows the body (the death camera) finds it there.
- The cut-off bones' ragdoll bodies stay on their joints (they must be whole again on revival) with no collider and almost no weight, so
  the invisible limb neither props the body up nor drags it. They are always the end of a chain, where a light link is stable.
- Every piece is matte, and no tissue is paler than 0.5 (`Gore.Palest`). At first near-white eyes, teeth and bone came out twice the bloom's
  threshold in the noon sun, and small on a dark body the bloom made each a dot of glowing light (owner, 2026-11-07): capped at the skin's
  0.7. At night, under a room's lamps, small pale bits on dark red meat (eyes, teeth, bone, a chunk's rim of fat) still lay about as points
  of light (2026-11-08: "there are still little dots of shining light coming from some of the dismembered parts ive asked for it to be fixed
  several times"): capped at 0.5, eye whites 0.42, and a burst head no longer throws loose teeth (the jaw keeps its own). `GoreNightCapture`
  renders hacked bodies at night under a flashlight and lamps through the game's grade and counts the small bright patches; the earlier
  checks were by day with no grade, where no such dot can show.
- A copy keeps the look of what it copies, each material's own property block included (`MaterialBlocks`): the hair's dye is on its
  material's block, and the cut-off head and a burst head's hair chunks came out in the hair material's gold (owner, 2026-11-08: "when my
  player got decapitated the hair color changed on the decapitated head"). Hair a hat or helmet covers (switched off under it) is not
  copied: it poked through the hat on the flying head.
- Chunks and cut-off parts lie 60 s, then shrink away over 3 s, taking the meshes made for them (`Debris`). Revived, the body is whole
  (`Restore`).

## Armour (2026-11-06)

A part under armour takes more to come off (three times under heavy armour), and what is worn on a part that comes off falls to the ground as the item itself (docs/ARMOUR.md).

## Other players

Decided on the machine where the body is (the owner's; a civilian's, the machine it lives on). `Dismemberer` is an `IReplicatedAbility`:
what went (cut or burst), which part, where and which way; every copy is taken apart the same way, and made whole again when the owner is
revived (a copy never raises its own Died or Revived). A hit forwarded from another player's copy carries whether it was edged, whether it
was bloodless and where it landed (`NetworkedDamage`), and a blow on the copy of a corpse is forwarded too while the corpse has something
left to lose, so hacking at another player's body takes it apart on every screen. Across the network the part is picked from the copy's
pose, a moment behind a body still falling: a blow then can land on the part next to the one aimed at.

## Tests

`DismemberRulesTests` (EditMode: only a killing blow of double what was left; edge cuts, else bursts, water takes nothing; what each part
of a corpse takes and what it takes with it; the nearest part still on; an edge on the torso cuts at the nearest joint still on; a skinned
strip cut where its bones take over; a tube's cut as wide as the tube however long its triangles; the blow read from the striker's scope). `DismembermentPlayTests` (a blade's overkill cuts the forearm
off and it flies, with a stump; a blunt one bursts the head; a bare kill or water takes nothing; 50 on a body on 30 takes nothing, 70 takes
the head; a corpse hacked at part by part, head, forearm, arm, then the rest; a civilian too; a chest burst takes the body, and revived it is
whole; a copy told of a cut is cut the same way and made whole with its owner; a cut-off forearm's, arm's, shin's and leg's cap
as wide as the limb at its open edge). `NetworkedPlayerStateSyncPlayTests` (over real sockets: a
sword's overkill on A takes the same head off on B's copy, B hacking at the corpse takes the forearm off on both, both whole after A is
revived). Pictures: `DismembermentCapture` (Logs/CityShots/dismember-*.png, with a corpse hacked at, and both ends of each cut close up:
`dismember-*-piece-end.png`, `-body-end.png`).

## Not yet

Cutting a body in half at the waist; cutting up a part already cut off (the pieces on the ground are not struck); gore decals on the
stump's skin; a sound of its own; gibbing on a long fall; spells other than the sword's wave on a corpse; dismembering an NPC that does not
use the fused body.

## Chunks made of the body (2026-11-05)

Owner: "try to improve upon the dismemberment "chunks" that explode from the body upon destruction, try to make them more human like". Research (Half-Life, Quake, TF2, Fallout, Left 4 Dead 2, Dead Island 2, MadWorld) said: recognisable pieces, each the person's own skin or clothes outside, red meat round a white bone at the cut, a ragged edge rather than a clean one.

- **The body's own surface breaks up** (`GoreShatter`): a burst part's skin, clothes, skull and hair are cut into pieces of a size, each closed behind with a line of fat, the meat round its torn edge and the inside. Clothes over skin get a back of their own.
- **What was inside comes out** (`GoreAnatomy`, `GoreMeshes`): a head throws its eyes, jaw, teeth and skull shards; a trunk its ribs, spine, heart, lungs, liver and guts; a limb its own bone. A whole body also throws its head and the end of each limb whole, each with a stump (`Dismemberer.Burst.cs`).
- **They fly, land and rest** (`GoreChunk`): a few leave a smear where they land, they come to rest and stop being simulated. At most `Debris.MaxLive` pieces are out at once, the oldest going first.
- **The same on every screen**: the burst's seed goes in the replicated event. It is kept within what the server accepts (`Dismemberer.SeedRange` = `NetworkedAbilitySync.MaxSpeedOrDuration`): a larger one made the server refuse the cut, and other players never saw a head come off.
- **No strobing light**: a cut-off part's bones stay on the body, weightless and without colliders, so it can be whole again when revived. They spun at up to 46 turns a second and swept any light riding them, a flashlight's beam or a burning blade, round like a strobe (owner: "a bunch of flashing lights ... like a flashlight type brightness"). They are now held from spinning (`CutBodyMostSpin`, `CutBodyAngularDamping`), the hidden ragdoll a whole burst leaves too, and every light on a part that is gone is off until revival.

Tests: `GoreShatterTests`, `GoreAnatomyTests` (closed pieces, sizes, no broken corner or normal), `DismembermentPlayTests.Pieces.cs` (each part's pieces, the same seed the same pieces, landing and resting, every piece where numbers are, no light shining and no bone spinning after a burst). Pictures: `DismembermentCapture` (`dismember-*-burst*.png`).
