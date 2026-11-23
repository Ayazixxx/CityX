# Fingerless gloves (2026-11-23)

Owner: "generate some fingerless gloves. just black and brown for now but make it so we can make more colorways easily". Worn in the gloves
slot. `/equip gloves` (black), `/equip blackgloves`, `/equip browngloves`, or any colourway's id; `/spawn` the same. Common (no rarity set).

## How they are made

Not a model: the gloves are **the wearer's own hands**, made when a pair is put on (`GloveShell`, called by `FingerlessGloves.GlovesFor`,
once per body mesh and shared by every body that wears a pair):
- every point of the hands lifted 3.5 mm off the skin along its normal, with the body's own bone weights and bind poses, so the leather bends
  with every finger (a fist, a grip on a weapon) exactly as the hand under it does;
- cut off 70% of the way along each finger's first bone (55% on the thumb) and 8.5 cm up the forearm. On a hand a point is measured
  against the cut of the digit it is nearest, so each opening is one clean ring; the palm itself is never cut (the thumb's cut once opened a
  hole in its web, owner: "looks like there is a tear where skin shows");
- the cuff's last 1.8 cm stands 1.8 mm prouder;
- a wall from each opening's edge down under the skin, facing out of it: the leather has a thickness and the black line runs round it.

The finger bones are found by name (`BodyRig.Bones`): they are not in the humanoid avatar. Under a garment that hides some of the body, the
gloves are still made from the whole body (`WornBodyCover.Whole`), so a long sleeve never takes a cuff away.

Drawn by their own `SkinnedMeshRenderer` ("FingerlessGloves") beside the body's skin, with its bones; not under the body's paint.

## A new colourway

One line in `Items/FingerlessGloveColourways.All` (id, name, console alias, colour), then **CityX/Rebuild Fingerless Gloves** (or the scene
rebuild, which runs it) and `TitleScreenBuilder.Build`. The item, its prefab and material (`Wearables/FingerlessGloves/<id>.*`), the console
name and the lists every body draws items from (`NetworkedPlayerWiring`, the scene's inventory, the select screen) all come from that table.
An id is what a save stores: never change one once shipped. Black is a very dark grey: pure black loses the fingers' shape to the outline.

## On the ground

One shared model (`FingerlessGlovesOnGround.asset`, `FingerlessGlovesBuilder`): the gloves made on the player's hands, laid side by side,
palms down, fingers ahead.

## Other players

`ICosmeticMountable`: a copy of the wearer makes the same gloves on its own hands.

## Tests

`FingerlessGlovesTests` (bound like the body, only the arms' and hands' bones, where the cuts are, close on the skin, every colourway an item
with its prefab and colour), `FingerlessGlovesPlayTests` (worn and taken off, the body's bones, whole under a long-sleeved shirt),
`NetworkedPlayerStateSyncPlayTests.WornClothesAndAHelmetShowOnAnotherPlayersCopyAndGoWhenTakenOff` (on another player's copy). Pictures:
`FingerlessGlovesCapture` (`gloves-*.png`: standing, the back and palm of a hand, fists, the pairs on the ground).

## Not done

- No armour value yet (gloves are an armour slot in docs/ARMOUR.md: forearms).
- No texture (seams, stitching): one flat colour per colourway.
