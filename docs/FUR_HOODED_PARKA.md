# The Fur Hooded Parka

A long quilted parka with a fur-edged hood lying on the back, worn as a top. The owner brought Meshy's olive parka on 2026-11-20 ("fitting
this parka is proving to be quite the challenge ... il give you full creative control ... a fur hooded parka that will be functional ingame"),
then: "make the item an equipable item ingame make it blue rarity", and "im planning on making different colorways of this same model".

- The model is the **Fur Hooded Parka**; each colour is its own item, all Rare (the blue card):
  - **Green Fur Hooded Parka** (`green_fur_hooded_parka`): `/spawn parka`;
  - **Maroon Fur Hooded Parka** (`maroon_fur_hooded_parka`, 2026-11-21: "make the maroon colorway ... same blue rarity level"):
    `/spawn maroonparka`.
  Put one on with `/equip <word>` or by picking it up (E).
- 14,598 triangles (the fur's shell is 4,482 of them), one 1024 map.

## A new colour

Every colour is the same mesh with its own map. Meshy retextures the model without changing its shape or texture coordinates (the maroon
file's are the green one's to the last digit), so a new colour from Meshy goes on in the owner's Blender with `parka_colourway.py`:

- `load_map` reads Meshy's 4096 map;
- `clean_map` repaints its stray spots: on the texels only the cloth's faces cover, every pale spot (white specks, a tan blotch from the
  fur) takes the cloth round it; on the fur's, any cloth colour or dark speck takes the fur round it (owner, of the maroon: "fix all these
  blotchy white spots make it a smooth red all over", and the tan blotch "the green colorway had");
- `colour_parka` paints it into the atlas (the sleeves' patches, lining and ribbing from its own cloth), carries it onto the game's
  layout and saves `Wearables/<Name>.jpg`.

It needs the session state the green one left (which texels are cloth and which fur, the readings, the parka's places on the atlas), kept
in `parka_work_backup.blend`'s session only while that Blender is open. A colour painted by hand instead is a recoloured copy of
`GreenFurHoodedParka.jpg` (the fur is the pale pieces, the ribbed cuffs the strip at the top right). Then add:

1. an id and a definition in `ItemRegistry`, and its rung in `ItemRarity`;
2. a `Save("<Name>", ..., bakeMesh: false)` line in `FurHoodedParkaBuilder` (and in its `BuildAll`), called from
   `CityMassingSceneBuilder` into the inventory's list;
3. its prefab in `NetworkedPlayerWiring` (other players see it), a `/spawn` word in `DevConsoleView.Spawning`, a fixture in `LongCoatTests`
   and `LongCoatPlayTests`, a render in `LongCoatCapture`;
4. rebuild the scene and the title screen (`TitleScreenBuilder.Build`: the select screen must be able to dress a character in it).

## Where the model comes from

Meshy's parka did not fit: the sleeves were fused to the coat's sides, too long and too wide at the wrist, and the left front hung open at
an angle. It was remodelled in the owner's live Blender (through the Blender MCP) with the scripts in `CityX/SourceArt/Parka/`:

| Script | What |
|---|---|
| `parka_lib.py`, `parka_rings.py` | rays against the full-detail coat; each sleeve read as rings square to its axis |
| `parka_sleeve.py`, `parka_cut.py`, `parka_steps.py` | the sleeves cut loose from the coat's sides and patched, fitted to the arm, a band cut out of the left one, the hem levelled, the ribbed cuff (a plain band, its ribs drawn on the map) down to the top of the thumb, holes closed |
| `parka_front.py` | the left front panel re-hung straight, a mirror of the right |
| `parka_texture.py` | Meshy's map with the new patches' cloth beside it; the hood's tan blotch and the fur's green strand were repainted |
| `parka_bake.py` | the game's map: the finished coat laid out afresh in whole pieces and its colour carried over |
| `parka_remodel.py` | the driver: `build()`, `finish()`, `export()` to `Parka.glb` |
| `parka_colourway.py` | another colour from Meshy onto the same parka (below) |
| `parka_skin.py`, `parka_build.py` | the skinning, headless, to `Wearables/Models/FurHoodedParka.json` |
| `parka_pose_check.py` | pictures of it with its arms raised, headless |

What the owner asked for along the way, and what answers it:

- **Sleeves kept, not rebuilt** ("i think the sleeves were good ... just refitting and trimming and maybe the cuff rebuilt"): Meshy's own
  sleeves, cut loose and moved onto the arm.
- **The cuff** as Meshy's (last quilted band, rolled hem, ribbed cuff coming out from under it), its lower edge at the top of the thumb's
  root and wide enough to go round it, "straight lines around"; nothing to see up the sleeve between cuff and hem.
- **The left front** straight and a mirror of the right, the dent above its pocket gone.
- **The underarm** "less stretched more natural ... like a jacket is stitched": the patches under the arm are even webs, and the weights
  there are a long smooth blend onto the half-turning shoulder bones.
- **The owner's own placement** of the coat on the body is kept (they turned it back 4 degrees).
- **"What are all these little triangles"** and **"fix any holes"**: below.

## The game's map is laid out afresh

Meshy's map is thousands of crumbs, cloth and fur side by side. At 4096 that works. Cut to the game's 1024, every crumb's edge takes its
neighbours' colour, and the coat was covered in pale slivers; cutting the model down had also grown faces Meshy's map squeezes to one texel
into pale triangles centimetres across. So `parka_bake.py`:

- sorts the finished coat's faces into about 500 pieces that each lie nearly flat (grown from a big face while the faces look within 70
  degrees of it), flattens them (Blender's unwrap) and packs them with four texels between;
- gives the faces nobody sees (the lining deep inside: almost no ray from them gets out past the coat and the body) 0.4 of the size, so the
  cloth that is seen gets the room;
- paints each new texel with the colour the old map has at the same place on the coat, at 4096, and cuts that down to 1024 counting only
  the texels that lie on the coat; the rest take their nearest neighbour's colour, so nothing bleeds at any distance;
- leaves the ribbed cuffs on their own strip, which they repeat four times round the wrist.

Before that, `mend_map` gives every face with no real cloth of its own on Meshy's map (squeezed to a point or a line, torn across two
pieces, or added to close a hole) its neighbour's cloth, mirrored at the edge they share.

## Holes

Cutting the model down leaves pinholes where open edges tangle. `close_slits` sews and fills the plain ones; `close_specks` draws the
rest shut (a pinhole's points meet in its middle; a sliver's are joined to the ones a hair away) and drops loose flaps.

The "two triangular holes on his left shoulder" the owner saw were not holes: the body's shoulder came 4 mm out through the cloth there
once the coat was turned back on the body. `lift_over_body` lifts the outer cloth over the shoulders to 8 mm off the skin; rays from
twelve views round both shoulders now meet the coat before the skin. The finished coat
has no edge shared by three faces and no opening but its own (front, hem, neck, cuffs, and Meshy's holes in the hood under the fur).

## In the game

Built like the Midnight Coat (`docs/MIDNIGHT_COAT.md`): `BlenderGarmentMesh` makes the skinned mesh, `LongCoatBody` says which of the body's
triangles a long open-fronted coat hides, `GarmentHelperBones` moves the skirt, the armpits and the collar. The differences:

- **Only the sleeves follow the arms.** `Parka.glb` says which points are a sleeve's in its second set of texture coordinates (u = 1);
  the coat's own cloth beside them gives its arms' share to the spine (`garment_rig.sleeves_only`). The nearest skin alone gave the coat's
  sides to the forearms hanging beside them.
- **A wider armpit blend** (0.50 m round the shoulder joint, 24 passes): the parka's armpit hangs 38 cm under the joint.
- **The fur ring and the hood** go to the collar bone, as the Midnight Coat's collar does, and the hair is tucked in under the ring.

To rebuild after a change in Blender (`export()` writes `Parka.glb`):

```
"C:/Program Files/Blender Foundation/Blender 4.2/blender.exe" --background --factory-startup --python CityX/SourceArt/Parka/parka_build.py -- CityX/SourceArt/Parka/Parka.glb CityX/Assets/CityX/Wearables/Models/FurHoodedParka.json CityX/Assets/CityX/Wearables/GreenFurHoodedParka.jpg
```

then rebuild the scene (`CityMassingSceneBuilder.Build`).

## A shirt under it (2026-11-21)

Owner: "make it so i can wear a shirt underneath the parka. make it so they both go into the top slot in inventory and can stack. that is how
we will do it for testing purposes I will come up with a way for it to look good with ui later."

- The top slot has a second place, under the coat (`EquipSlots.TorsoUnder`), as a weapon slot has one for a second pistol. The rule is
  `TopLayers` (pure): a coat goes in the top place, over the shirt worn, which moves under it; a shirt goes under the coat worn, or in the top
  place with no coat on. One shirt and one coat at most: a second of either kind takes the place of the first, which goes to storage.
- Coats are `ItemRegistry.IsCoat`: the Midnight Coat and the parkas. Everything else worn as a top is a shirt.
- The menus show both in the top slot, "Green Fur Hooded Parka + T-Shirt". A click on it takes the coat off; the shirt stays on.
- Both are saved, told to other players (their copies wear both) and put back on from a save in either order.
- Under a coat a shirt is drawn only in the coat's open front (`UnderCoat.FrontOnly`, in `SkinnedGarmentCombiner`; the same opening
  `LongCoatBody` shows the body in). The rest the coat covers, and that is where it came out through: a long-sleeved shirt's sleeves through
  the parka's fitted upper sleeves, and (owner: "im seeing the shirt clipping through around the shoulders. make the shirt sleeves
  invisible underneath the parka") both shirts' shoulders by the collar. Measured on the parka at rest, every point of either shirt out
  through it lies outside the opening. `maroonparka-shirt-*.png` shows the red striped T-shirt under the maroon parka.
  `LongCoatCapture.RenderThePlayerInTheGreenParkaOverALongSleevedShirt` renders `greenparka-shirt-*.png`.

## The hair behind the head (2026-11-21)

Owner: "there are small bits of the scalp visible on the back of the head when wearing parka. you can have the hair clip into the hood
slightly to hide this", then "just dont have hair poking through the other side of the fur". The hair is drawn in under a coat's collar from
its rim down (docs/HAIR.md, "Tucked into a high collar"). The rim was the collar's highest point behind the neck: on the parka the fur's tips
beside the head, 2.165 m up, while straight behind the head the fur only reaches 2.13 m. So behind the head the hair was drawn in above the fur
and the scalp showed. The parka reads its rim straight behind the head (`FurHoodedParka.CollarRimHalfWidth`, 8 cm either side of the middle):
the hair stays on the head down to the fur's top there and goes into the fur below it. The fur is 13 to 16 cm thick behind the head, so the
hair going into it stays inside it.

## Melee swings (2026-11-21)

Owner: "when swinging with pretty much any melee weapon the knee clips through the jacket". The skirt is kept out of the legs as the Midnight
Coat's is (docs/MIDNIGHT_COAT.md, "Kept out of the legs in melee swings"): `SkirtCloth` pushes the skirt bones off the legs each frame, a step out to
the side carries both of a leg's skirt bones, and the waist hangs from them from 1.15 m down. One thing of the parka's own rig changed with it:

- **Its lower sides follow the hips, not the chest** (`garment_rig.sleeves_only`): the coat's own cloth beside the hands, given the arms' share
  by the nearest skin, handed it to the spine, so in a swing it twisted with the upper body and the thigh went through it. Below the waist it
  goes to the hips now (and so to the skirt bones).

Through all 11 melee weapons a leg now comes through the parka's outer cloth on 3 single frames in all (`CoatLegsPlayTests`);
`CoatLegsCapture` renders `Logs/CityShots/coat-legs-sheet.png` (the fists, the combat sword, the bo staff and the sledgehammer, from the game's
camera and from the side).

## Tests

- `LongCoatTests` (EditMode, every long coat): a top with its own name and a blue card; within 15k triangles and one small map; never on
  the head, hands or shins; below the hips, nine in ten points on the skirt bones; hides part of the body; hangs from the shoulders to below the hips.
- `LongCoatPlayTests` (PlayMode, every long coat): the body under it is hidden and given back; through a sprint the head does not sink into the
  collar and no knee comes through the front.
- `TopLayersTests` (EditMode): a shirt and a coat stack in either order; a second of either kind takes its own kind's place; the coat
  comes off first; both saved, told to other players and put back on.
- `UnderCoatTests` (EditMode): a shirt under a coat shows its chest and nothing round the shoulders or on the arms.
- `CoatLegsPlayTests` (PlayMode): the real scene and player in the parka, every melee weapon swung by the mouse; no leg through the outer cloth
  for more than three frames running. `SkirtClearanceTests` (EditMode): the push itself.
- `GarmentCollarTests.TheParkasRimIsTheFursTopStraightBehindTheHead`; `HairPushCapture.RenderThePushOverTheParka` renders
  `Logs/CityShots/hair-push-parka-sheet.png`.
- `LongCoatCapture` (PlayMode, Explicit, `-Graphics`): standing, the cuff, walking and four moments of a sprint, to
  `Logs/CityShots/greenparka-*.png` and `maroonparka-*.png`.

## Known limits

- The remodel's first steps (the 250,000-face working copy, the hood's repainted blotch, the front panels' traces) were done by hand in
  the owner's Blender session and are kept in `SourceArt/Parka/parka_work_backup.blend` (368 MB, not committed): `build()` starts from them.
- The quilting's stitches are softer at 1024 than on Meshy's 4096 map.
- The model check (`tools/model_qa.py`) still warns of small holes: they are in the fur's shell and Meshy's own in the hood under it.
