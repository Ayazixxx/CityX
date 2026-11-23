# The PHIZZ can

Owner, 2026-11-23: "make a phizz can model put the phizz logo on this can and make the can golden and the tops silver like soda cans
usually are" (a Sketchfab soda can and a picture of yellow cans with silver lids as reference; "how hard is it to just replicate it its a
pretty simple design": made here, nothing downloaded). "the can version is not meant to be used as a weapon heals only. i will have fable
build the heals and drinking element ... you will do the models".

**A model only.** It is no item yet: drinking it and its heal (80 health over 10 s, the owner's) are Fable's to build (see the PHIZZ notes in
docs/GLASS_BOTTLE.md for where full ones come from).

## What it is

- A real 12 oz can (66 mm across, 122 mm tall), at the bottles' size (2.4 times life, `BottleShape.LifeScale`), standing on its base at
  the prefab's origin: `Weapons/Cans/phizz_can.prefab`, with a capsule round it and no body.
- A **golden** body, the foot, neck, rim and lid bare **silver**; a pull tab of a duller metal (the same silver flat on the lid could not be
  told from it) riveted to the lid's middle, its finger end toward the rim; on the other side of the rivet the opening's outline stands a
  hair off the lid, as a can's mouth is pressed into its lid.
- The PHIZZ label printed front and back, its own copy (`Weapons/Cans/PhizzCanLabel.png`): the owner's two pictures
  (`SourceArt/Phizz`) laid out by `phizz_label.ps1` as for the bottle, the splash 1.1 times the bottle's and BANANA SODA the size it was
  (owner: "make the phizz part a bit bigger", "leave the banana soda the same size and make the phizz logo 1.1x bigger"). The label is
  printed 1.1 times bigger with the wording drawn 1/1.1 inside it (`-WordingScale 0.909`); its clear margins run onto the neck and foot and
  print nothing there.
- About 3,200 triangles: turned outlines (24 sides), the tab and the opening as flat rings, the print as two curved grids.

## How it is made

- `CanMesh` (`Weapons/Drinks`, pure): the outline in three parts (`Foot`, `Body`, `Top`), the `Rivet`, and the `Tab` and `Opening` as flat
  rings between two rounded outlines.
- `PhizzCanBuilder` (`Weapons/Editor`, menu CityX/Rebuild PHIZZ Can): turns, colours, prints and saves them. Rebuild:

      Unity.exe -batchmode -quit -projectPath CityX -executeMethod CityX.WeaponsEditor.PhizzCanBuilder.BuildFromMenu -logFile Logs/can.log

  The label first, if the owner's pictures or the sizes change:

      powershell -ExecutionPolicy Bypass -File CityX/SourceArt/Phizz/phizz_label.ps1 -WordingScale 0.909 -Out ../../Assets/CityX/Weapons/Cans/PhizzCanLabel.png

## Tests

`PhizzCanTests` (its parts, its size, the triangle budget, the label printed; the tab and the opening on the lid inside the rim, either side
of the rivet). `PhizzCanCapture` (Explicit, `-Graphics`): front, back, side, above, the lid close, one lying down from below:
`Logs/CityShots/can-*.png`.
