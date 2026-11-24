# The Worn Boots (2026-11-23)

Owner, with two photos of a short suede ankle boot (tan, and a darker brown): "can you make a shoe model ingame that looks like something and
looks good with the jeans. use the sneakers we built as a base for sizing but these can be a bit bulkier than the sneakers, i think the jeans
will rest on them a bit better if they are a tad bulkier it will look fashionable, not too bulky", "a bit browner like the browner image",
"doesnt have to be the exact design just make a boot a homeless person would wear ... use that as a guide though". Then, of a first paint with
a stitched patch and mottling: "get rid of that brown diamond or square thing ... just make it brown all over doesnt have to be super detailed".

Item `worn_boots`, "Worn Boots" (my name: say another and it is one word to change), Shoes, Common (mine). `/spawn boots`.

## The model

`SourceArt/WornBoots/worn_boots_build.py` (Blender, headless) makes the pair from nothing, our own design: `Wearables/Models/WornBoots.glb` and
its paint `Wearables/WornBoots.jpg` (512).

- A short ankle boot: a round bulbous toe, a thick sole with a ribbed welt, a low shaft, a rolled collar that sags a little, a pull tab at the
  heel. Each boot is one closed skin lofted from level outlines (an egg-shaped footprint drawing back from the toe as it rises into the
  shaft's oval), with an inner wall and a floor inside the collar. 2,936 triangles for the pair.
- **Sized from the sneakers as worn**: about 1.3 times as wide, a little longer, the shaft 27.5 cm up (a sneaker's collar is 13). The shaft is
  slim enough (22 by 23 cm) that the jeans' hems drape over it: wider, it stood out past the hem and the jeans cut into it.
- **Paint**, kept plain: dark brown suede all over, a little dirt climbing from the sole at the toe and heel, a stitched seam round the vamp,
  the sole, welt and collar darker. U runs round the boot from the toe, V up it in bands (sole, welt, upper, collar, inside), so a new colour
  is the colours at the top of the script.
- Built standing on the body's feet in the game's frame at its worn size, so `WornBootsBuilder` neither grows nor moves it.

## In the game

Worn as the sneakers are (`Shoes`): each boot rigid on its foot bone, the feet and the ankles it covers not drawn. `WornBootsBuilder` calls
the sneakers' pair code, now shared (`WhiteSneakerBuilder.SavePair`). Other players see them (`NetworkedPlayerWiring`).

## Tests

`WornBootsTests` (the item, Common, both boots, triangles, paint, hidden feet; on the ground, each on its foot, 1.1 to 1.4 times the sneaker's
width, taller). Pictures: `WhiteSneakerCapture.RenderTheWornBoots` with the Black Jeans (`wornboots-*.png`).
