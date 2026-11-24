# The Brown Coat (2026-11-24)

A long open-fronted cloth coat with a hood, the owner's design, worn as a top. Owner, 2026-11-23, with pictures of it on the game's body:
"make it so it is hood off by default but if you type /hoodon in the text chat not the console command chat, then it turns into the hood on
version ... make it brown but we can make multiple colorways of easily. I will personally trim each hairstyle so they fit with the hood on
version how i want them too just leave them all clipping through for now." A coat I modelled myself that day "looks bad"; so, 2026-11-24, with
Meshy's two models of it ("Hooded Wanderer", the hood down, and "Hooded Coat Character", the hood up, each the coat on a grey mannequin):
"cleanly remove separate the playermodel form the coat with these two models, also lower the face count to a reasonable amount ... use the
brown material vs the playermodel's grey as a guide. then put it into the game as a functional item called brown coat".

Item `brown_coat`, "Brown Coat", a coat (a shirt goes under it), Uncommon (mine). `/spawn browncoat`.

## The hood

- Put on, the hood is down.
- **`/hoodon`** in the text chat (Enter, not the console) puts it up; **`/hoodoff`** takes it down.
- The coat is two models, one for each; the hood going up swaps the worn one (`HoodedCoat`, an `IHooded`).
- Other players see it: `HoodSync` on the player body tells them, and tells a raised hood again every 4 s, so someone who comes near or joins
  later sees it up too.
- Hair is left as it is under the hood (the owner trims each style for it).

## The models

`SourceArt/BrownCoat/brown_coat_build.py` (Blender, headless), run on the two unzipped Meshy folders:

| | Hood down | Hood up |
|---|---|---|
| Meshy's faces | 256,158 | 337,180 |
| The coat's, cut from the mannequin | 168,550 | 258,129 |
| In the game | 9,000 triangles | 8,999 triangles |

1. **The trim, at full detail**, before anything is reduced, so its edge is as fine as Meshy's own: a face is the coat's where the paint is
   brown (red well over blue; measured brown 0.39 0.33 0.27, grey 0.63 0.63 0.63). Loose scraps left on the mannequin's side are dropped
   (32 and 20 faces), small holes in the cloth closed (25 and 16).
2. **No refitting**: Meshy's mannequin scaled to our body's 2.6 m is our body's size (arms, legs and head as wide), so the coat is only scaled
   and stood on the ground.
3. Reduced to 9,000 triangles.
4. The cloth is kept 2.8 cm off the body (a shirt goes under) and 2.2 cm off the head, a ball here where Meshy's is an egg: points too near
   are moved straight out.
5. Skinned as the coats are (`garment_rig.py`): the nearest skin; only the sleeves on the arms (told apart by where they hang); the skirt on
   the skirt bones; the armpits shared; the collar and what lies near the head on the collar bone; hood up, the hood on the head's own bone,
   so it turns with the face.
6. Each model's paint as one 1024 JPEG (`Wearables/BrownCoat.jpg`, `BrownCoatHoodUp.jpg`): Meshy lays each model out its own way.

## A new colour

Meshy's two retextured models through the same script under a new name, then a line in `HoodedCoatBuilder.Colourways`, an item in
`ItemRegistry` (and `IsCoat`), a rung in `ItemRarity`, its prefab in `NetworkedPlayerWiring` and the scene builder, a `/spawn` word; rebuild
the scene and the title.

## Tests

`HoodedCoatTests` (each colour a coat of its own with both hoods and their paint, within budget, the skirt to the thighs, hood up over the
head; the chat's words; every player body has `HoodSync`), `HoodedCoatPlayTests` (hood down when put on; up and down here and on another
player's copy; told again while up; nothing to raise with no coat). Pictures: `LongCoatCapture.RenderThePlayerInTheBrownCoat` and
`...HoodUp` (`browncoat-*.png`).
