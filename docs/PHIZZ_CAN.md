# The PHIZZ can

Owner, 2026-11-23: "make a phizz can model put the phizz logo on this can and make the can golden and the tops silver like soda cans
usually are" (a Sketchfab soda can and a picture of yellow cans with silver lids as reference; "how hard is it to just replicate it its a
pretty simple design": made here, nothing downloaded). "the can version is not meant to be used as a weapon heals only. i will have fable
build the heals and drinking element ... you will do the models".

**An item you drink** (owner, later the same day: "make a drinking animation for the can", "make it heal how we described 80 hp over 10 seconds"). Not a weapon. Full
ones are meant to come from vending machines, fridges and bars (docs/GLASS_BOTTLE.md, PHIZZ); for now `/spawn phizz` (or `phizz_can`, `soda_can`).
The bottle's drink, with liquid sloshing inside, is Fable's (owner).

## What it is

- A real 12 oz can (66 mm across, 122 mm tall), at the bottles' size (2.4 times life, `BottleShape.LifeScale`), standing on its base at
  the prefab's origin: `Weapons/Cans/phizz_can.prefab`, with a capsule round it and no body.
- A **golden** body (a yellow gold, lighter and less metal than the bottle caps: owner, "make the can a tad yellow its a bit too dark"), the foot, neck, rim and lid bare **silver**; a pull tab of a duller metal (the same silver flat on the lid could not be
  told from it) riveted to the lid's middle, its finger end toward the rim; on the other side of the rivet the opening's outline stands a
  hair off the lid, as a can's mouth is pressed into its lid.
- The PHIZZ label printed front and back, its own copy (`Weapons/Cans/PhizzCanLabel.png`): the owner's two pictures
  (`SourceArt/Phizz`) laid out by `phizz_label.ps1` as for the bottle, the splash about 1.2 times the bottle's and BANANA SODA the size it is there
  (owner: "make the phizz part a bit bigger", "leave the banana soda the same size and make the phizz logo 1.1x bigger", "make the logo
  1.1x bigger"). Each copy goes exactly half way round, the splash's drips meeting at the sides (any further and the two would overlap), so
  the can's label has no margin (`-Margin 0`) and its wording is drawn smaller inside it (`-WordingScale 0.834`); its clear top and bottom
  run onto the neck and foot and print nothing there.
- About 3,200 triangles: turned outlines (24 sides), the tab and the opening as flat rings, the print as two curved grids.

## How it is made

- `CanMesh` (`Weapons/Drinks`, pure): the outline in three parts (`Foot`, `Body`, `Top`), the `Rivet`, and the `Tab` and `Opening` as flat
  rings between two rounded outlines.
- `PhizzCanBuilder` (`Weapons/Editor`, menu CityX/Rebuild PHIZZ Can): turns, colours, prints and saves them. Rebuild:

      Unity.exe -batchmode -quit -projectPath CityX -executeMethod CityX.WeaponsEditor.PhizzCanBuilder.BuildFromMenu -logFile Logs/can.log

  The label first, if the owner's pictures or the sizes change:

      powershell -ExecutionPolicy Bypass -File CityX/SourceArt/Phizz/phizz_label.ps1 -WordingScale 0.834 -Margin 0 -Out ../../Assets/CityX/Weapons/Cans/PhizzCanLabel.png

## Tests

`PhizzCanTests` (its parts, its size, the triangle budget, the label printed; the tab and the opening on the lid inside the rim, either side
of the rivet). `PhizzCanCapture` (Explicit, `-Graphics`): front, back, side, above, the lid close, one lying down from below:
`Logs/CityShots/can-*.png`.

## Drinking it (`PhizzCan`, `Drinker`)

Owner: "he holds it in his right hand, and when you initiate the drinking. he reaches over with his left hand and does an opening the tab animation
and a black hole appears where the drinking hole is. then it plays the drinking animation attached drops the can", "from click to completion last 3.5
seconds", "thumb wrapped around for grip no not clipping into the can, maybe a very slight amount of the finger tips can clip through".

| | |
|---|---|
| Carried | upright in the right fist, raised as the bottle is (the same carry pose, `IRaisedCarry`), its hole toward the body |
| The grip | the palm on the can, the fingers bent at the knuckles and hardly at the tips (curled evenly, their tips hit a can this wide a third of the way to a fist), the thumb turned 30 degrees away and closed onto the can's near side: thumb 3 mm off it, fingertips 8 mm in (`CanGripPlayTests`) |
| Left-click | drinks. 0 to 0.6 s: the can comes in before the chest, the left fingertip gets under the tab and flicks it up (0.32 to 0.44 s), the left hand turned knuckles up over the lid, the index finger and the thumb pinching the tab (owner, with a photo of a can being opened; the fingers are the slingshot's pinch, `Drinker.TabHand` through `PlayerLocomotion.SetLeftFingers`; the clip turns the hand after the solver and puts the index fingertip a fingertip's thickness above the tab, following it as it swings up; nothing more than a millimetre into the can, the fingertip within 5 mm of the tab: `CanDrinkPlayTests`), the hole shows black. Then the owner's Mixamo "Drinking", mirrored to the right hand, 1.65 times as fast: a sip with the head bowed (1.36 s: **the heal begins**, 80 health over 10 s), the can tipped up with the head back (about 2.1 to 2.4 s), down again. 3.22 s: the hand lets the empty can drop. 3.5 s: done, the hand empty |
| The heal | `PlayerVitals.AddHealOverTime`: even over the 10 s, a blow does not pause it (the body's own regeneration waits after a blow; a drink does not), two drinks heal side by side, lost on death (`HealOverTime`) |
| The sound | your soda-can-open sound (`Weapons/Audio/soda_can_open.mp3`) plays from the can the moment the tab cracks the lid and the hole shows, about 0.38 s in, on every machine |
| The empty can | falls from the hand, rolls, lies for 30 s and shrinks away (`DroppedCan`); litter, on each machine its own |
| Moving | the drink is from the waist up and the legs walk on, as the bottle's moves |

**The clip** (`CityX/SourceArt/Drink/can_drink.py`, built by `can_drink_build.py`): key poses on the punches' solver. The can's rim is put to the
mouth (low on the head's front: faces are painted, so where a drawn mouth sits) at each moment the recording's cup is at its mouth, the hand fitted
to the can from the head (`wrist_turn.fit_hand` with `bone="head"`; its turn now searched out when the angles read off it miss, as they did for the
can tipped up). Keep `Drinker`'s moments and `PhizzCan.HoldPosition`/`HoldRotation` in step with it. Rebuild:

    blender --background --factory-startup --python CityX/SourceArt/Drink/can_drink_build.py -- --render <folder> --export CityX/Assets/CityX/Character/Resources/Drink/DrinkMotion.json

**Other players** (`Drinker`, an `IReplicatedAbility` "drink", on every body: `PlayerAbilityWiring`): told when a drink begins, their copy of
the body drinks its copy of the can the same way (the tab, the hole, the drop) and heals no one. The can in the hand is an `ICosmeticMountable`
(`NetworkedPlayerWiring`).

Tests: `PhizzCanTests` (the heal's numbers and stacking, the clip's 3.5 s and its moments in order), `CanGripPlayTests`, `CanDrinkPlayTests` (a
click to the end: the tab and hole, the heal at the sip, the drop, the hand empty, the copy). `PhizzCanCapture.RenderTheDrink` (Explicit,
`-Graphics`): `can-drink-front-*.png`, `can-drink-game-*.png`.

## The full PHIZZ bottle (`FullBottle`, `phizz_bottle`)

Owner, 2026-11-23: "make the glass bottle full of piss yellow liquid and when you drink its, it visually lowers until empty"; it is the PHIZZ soda bottle;
"100 health over 10 seconds"; drunk empty it stays in the hand as the weapon; drinks "stack infinitely".

- **The model** (`FullBottleBuilder`): the soda bottle's own glass, label and gold cap (`BottleShape`, `BottleMesh`), standing on its base as a drink is held.
- **The drink in it** (`View/Shaders/CityXLiquid.hlsl`, the `_CITYX_LIQUID` keyword on CityX/Lit): the glass is opaque (see-through glass gets no black line),
  so the drink is the glass coloured yellow below a level, a little lighter just under it (its surface). The level is a height in the world, set per
  bottle each frame (`FullBottle.Level`: up from the lowest point inside it toward the fill line, as much as is left), so it stays level as the bottle tips
  and goes down from the first sip to the end of the gulp (`Drinker.DrunkBySeconds`).
- **Held** like the can, round the straight of its body below the shoulder (higher, the thumb went over the shoulder into the glass), its thumb turned
  round its side (`FullBottle.ThumbTurn`; `CanGripPlayTests` measures both drinks). Carried lower than the can (`can_drink.py`'s `LOW_CARRY`): it is as
  long as an arm, and held at the chest its neck stood before the face. Every carried thing names the clip it is carried in (`IRaisedCarry.CarryClip`).
- **Drunk** (`bottle_drink`, the same 3.5 s): the left hand pinches the cap and pulls it off (it falls), the bottle to the mouth, the drink goes down,
  100 health over 10 s from the first sip. The clip ends in the bottle weapon's carry and the hand then holds the empty **soda bottle**, the weapon.
- **Stacks** (`DrinkStack`, both drinks, `ItemRegistry.EndlessStack`): one in the hand, the rest in a bag slot; when the one in the hand is gone the next
  comes out of the bag (as grenades do). The empty bottle goes in the bag while there are more to drink, stays in the hand after the last (set down at
  the feet if the bag is full). `/spawn` and `/equip` hand out 6.
