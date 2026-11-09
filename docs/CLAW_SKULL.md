# The Claw Skull

Owner, 2026-11-08: "can you give me the model file for the crimson claw as it is now with no claws just the head. im planning on making the head a
helmet you can get as a lucky drop"; then, with a picture crossing out the back, "the back of it is too long for it to be functional ingame ...
can you shape it to his head while still maintaining the same design with spikes in the back just not so far back"; then "make it an item ingame
"Claw Skull" and wearable as a helmet".

## The item

`claw_skull`, "Claw Skull", helmet slot. **Very Rare** and **Medium armour** are my placements (it is not on the owner's rarity list). Hair goes
bald under it, as under the Knight Helmet. `/equip clawskull` or `/spawn claw_skull`. It is not dropped by anything yet: the owner has it in mind as
a lucky drop from the Crimson Claw, chance not chosen.

## The model

`Wearables/Models/ClawSkull.glb`, from the owner's fit in Blender on a body standing at the origin (the head's centre at 2.25 m, as `BodyRig.HeadCentreY`).
`ClawSkullBuilder` keeps that placement and takes the head's centre off it. To refit it, move or scale it in Blender and export over the same file,
then rebuild the scene.

**The back was drawn in, not cut**, so every spike is still there. From a pivot 0.15 m behind the head's centre, what is behind it was squeezed
towards the head by an amount set at each height so the back follows the owner's red line: nothing moves above 2.44 m, and the furthest reach
went from 0.70 m to 0.44 m behind the centre. The squeeze eases in over the first 15%, so there is no crease, and is even after that. The lowest spikes
stay further back than the line (0.43 m, against its 0.2 to 0.3 m) on purpose: squeezed to about 0.37 m, the rear row packed so tight that the game's black
outlines merged into a blob when seen from the side, and they have to stay outside the body's back at 0.26 m anyway.

Cut from 17,370 triangles to 4,999 (the budget's aim for a helmet is 2,000 to 5,000), shaded smooth with hard edges past 45 degrees. It has texture coordinates, generated,
for a texture one day; it wears the Crimson Claw's own material today (`CrimsonClawBody.mat`, flat colour), so it adds no map and always matches the Claw.

## Model check

`tools/model_qa.py --kind helmet`: triangles, spikes, broken edges, turned faces and floating bits pass. Warnings, not yet looked at by the owner: 14 small holes
(12 inside it, two at the mouth, H3 and H12) and one face lying on another, inside it.

## Tests

`ClawSkullTests`: the helmet slot, the budget, around the head and facing forward, the back drawn in, the Claw's own colour, rarity and armour, shown on other
players' copies (`NetworkedPlayerWiring.ItemPrefabPaths`, `ICosmeticMountable`), the console's names. Pictures: `KnightHelmetCapture.RenderThePlayerInTheClawSkull` (`clawskull-*.png`).
