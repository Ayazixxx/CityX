# The 3D Glasses

White paper cinema glasses with a red lens and a blue lens, worn on the face. The owner, 2026-11-19, with two pictures of anaglyph glasses
(a white pair and a black pair): "make a 3D Glasses model and equippable item. make it shaped like the black one but white ingame. make the
blue and red part's transparent". Spawn them with `/spawn glasses` (or `3dglasses`, `3d_glasses`), put them on with `/equip glasses` or by
picking them up (E); `/unequip mask` takes them off.

## The model

- Made from scratch in Blender by `SourceArt/ThreeDGlasses/glasses_model.py` (headless, a few seconds), after the black pair's shape:
  - a flat front with rounded corners, two rounded lens windows and a rounded notch for the nose;
  - arms full height at the hinge whose lower edge sweeps up into a band along the top, the band bending down at the end and finishing
    round;
  - two flat lens sheets set in the middle of the front's thickness, a little larger than their windows.
- Made in the Head bone's frame with the head's centre at the origin, as the hats are. The head is a 0.35 m ball on the 2.6 m body, so these
  are a cartoon's glasses: the front 58 cm wide (narrower than the ball), 1.2 cm off its front at the eyes' height (4 cm above its middle),
  the arms splaying out round it to just behind its widest.
- Red before the LEFT eye, blue before the RIGHT, as real ones are and as the owner's pictures show. Each lens window 21 by 12 cm, 8 cm
  apart (owner, 2026-11-19, after the first pictures: "make a slightly larger gap between the lenses ... maybe make the lenses slightly
  larger as well"; they were 19 by 11, 6 apart).
- 2,340 triangles in all. Written to `Wearables/Models/ThreeDGlasses.glb`.

## In the game

| Step | Where | What |
|---|---|---|
| Item | `ItemRegistry.ThreeDGlasses` (`3d_glasses`, the **mask** slot) | a wearable on the face, worn beside any hat or helmet; kept on the character and shown on the character list; Common |
| Prefab | `Wearables/Editor/ThreeDGlassesBuilder.cs` (menu CityX > Rebuild 3D Glasses, and with the scene) | the Frame, LensRed and LensBlue parts as the glb has them |
| Look | the same builder | the frame white card (`CityX/Lit`, matte); the lenses unlit red and blue film at 40% cover, seen through (`SeeThroughMaterial`), in the transparent queue so the cel look's black lines leave them alone; the lenses cast no shadow |
| Wearing | `Wearables/ThreeDGlasses.cs` (`HeadWearable`) | rigid on the head at its pivot; the hair stays |
| Other players | `ICosmeticMountable`, `NetworkedPlayerWiring` | a copy of the wearer wears them the same way |

The mask slot was in the inventory and the HUD's list since 2026-10-15 with nothing to put in it; these are its first item.

**Hair:** rendered with every hairstyle (`ThreeDGlassesCapture`, `glasses-hair-*.png`): the front stays in front of every one, and the fuller
styles cover the arms. Nothing to hide.

## Tests

- `ThreeDGlassesTests` (EditMode): the slot (the face, not a hat's) and the name; the budget; the frame white and solid, the lenses red and
  blue, seen through and drawn after the solid world; red before the left eye and blue before the right, both in front of the face at the
  eyes' height; nothing inside the head, the arms reaching round it; worn on the Head bone with the hair kept; the copy's prefab.
- `ThreeDGlassesCapture` (Explicit, `-Graphics`): front, close, three-quarters, side, from above behind, the whole player, the front lit, one
  shot per hairstyle, with the Farmer Hat over them, and a loose pair with dark bars behind it to see through the lenses.
