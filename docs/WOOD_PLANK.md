# The wood plank (2026-11-23)

Owner: "I want to make a wood plank weapon, just a wood plank like a 2x4 (maybe not that exact length ingame but one that looks like it) that is
common rarity level, its like one of the lowest level 2 handed weapons (and may have some other use like fireplace or something later idk). make
it about the length of the metal pole maybe a tad less. find a free wood texture online that would work well for 1 wood plank", "probably will
look a lot longer than a 2x4". Then, of the texture found: "make your own version of that same design i dont want ours to be too realistic i want
it kinda cartoony to fit the artstyle", "use that one you found as a guide though", and with two drawings of a cartoon plank: "make it textured
more like this (without the face)", "and add the chip at the top of it".

`/equip plank` (also `woodplank`, `wood_plank`, `2x4`). Common (the owner's).

## How it plays

Two-handed and blunt, fought as the baseball bat is: the greatsword's swings, guard, dash and two-handed hold, the bat's blow (the lowest a
two-handed weapon has, a punch's). Blunt. No other use yet (a fireplace is the owner's maybe).

## The board

`WoodPlankShape`, made in code:

- **2.0 m** long on the 2.6 m body, a tad short of the bo staff's 2.2 m pole; **10 by 4.5 cm** across, a 2x4's look (3.5 by 1.5 inches is 2.3 to 1)
  at a size the hands close on. Mine.
- Flat faced (the game's black line draws its edges), **a V chip** in its top end through its thickness, 62% across, 1.6 cm wide at the end,
  3.5 cm deep (the drawing's).
- In the greatsword's frame as the bat: the lower hand 11 cm above the bottom end, the upper hand the support grip's 20 cm above that. Its width
  is across the swing's leading side (it swings a broad face first) and its thickness the way the right palm lies on it.

## The wood

`SourceArt/WoodPlank/plank_texture.py` (Blender, headless) draws `Weapons/WoodPlank.png`, 1024 by 128: the drawing's look, without its face.
Its upper board is for the broad faces, bright yellow wood with short streaks of orange and paler yellow along it, four dark cracks and darker
edges; its lower board is for the narrow sides, the ends and the chip, a reddish brown with darker streaks. Both tile along the plank
(seamless left to right). The first texture found was Poly Haven's "Hinoki Planks" (CC0), pale pine boards; it was the guide for a second, drawn
pine, before the owner chose the cartoon plank's yellow. Nothing of it is in the game.

## The hands

A fist closed round a board went into the wood (the owner's rule: no fingers through a weapon), so the plank shapes the hands itself
(`WoodPlankGrip`, an `IHandGripStyle`): the right palm flat on a broad face, its fingers over the far edge, its thumb lifted off the wood; the left
hand, which comes at the board from the other side turned onto its edge, is put on that edge (`SwordWeapon.SupportPalmOffset`, 4.8 cm out past
it) with its fingers curled onto the broad face. Measured on the skin, carried: the right fingers 4 mm into the wood, the palms 5 mm, the left
fingers 3 mm off it, the thumbs clear (`WoodPlankGripPlayTests`; a fingertip's 1 cm is allowed, as on the can).

## Other players

A `SwordWeapon` in `NetworkedPlayerWiring`: held and swung on every machine as the bat is; the grip shape is on the prefab, so copies hold it the
same way.

## Tests

`WoodPlankTests` (the item, Common, two-handed, blunt, the bat's blow; the length against the pole, the 2x4's proportions, the chip, the faces
wound outward; the wood tiling along it, its two boards apart), `WoodPlankGripPlayTests` (the hands on the board). Pictures:
`SwordStrikeCapture.RenderTheWoodPlankAttacks` (Explicit, -Graphics): `strike-plank-*.png`.
