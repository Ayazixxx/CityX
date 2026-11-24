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
4. **The sleeves are cut loose under the arm** (owner, 2026-11-24: "fix the armpit stretch"): Meshy joins each sleeve to the coat's side from
   the armpit to the elbow, and a lifted arm drew the join into a web. The faces joining them are given to the coat's side whole (their
   sleeve points made again as the coat's own), so the side has no hole and the sleeve swings free; under a raised arm a little of the
   sleeve's inside shows. The cloth just under the armpit stays with the body (`ARMS_FROM` 1.58 to 1.72).
5. The cloth is kept 2.8 cm off the body (a shirt goes under) and 2.2 cm off the head, a ball here where Meshy's is an egg: points too near
   are moved straight out.
6. Skinned as the coats are (`garment_rig.py`): the nearest skin; only the sleeves on the arms (told apart by where they hang); the skirt on
   the skirt bones; the armpits shared; the collar and what lies near the head on the collar bone; hood up, the hood on the head's own bone,
   so it turns with the face.
7. Each model's paint as one 1024 JPEG (`Wearables/BrownCoat.jpg`, `BrownCoatHoodUp.jpg`): Meshy lays each model out its own way.

## A shirt under it

Owner, 2026-11-24, of a T-shirt coming through the front panels at the stomach: "make the t shirt fit a bit tigher when brown coat is
equipped", "the top part looks good its just the stomach", then "fix the bottom part". The shared rule (`UnderCoat`) draws a shirt under a coat
only in the coat's open front, sized for the parka; the Brown Coat's gap is narrower and its panels lie closer. So under it
(`UnderCoat.FitsClose`) the shirt is drawn only where this coat's own front panels do not cover it (`CoveredInFront`, read off the worn coat on a
5 mm grid, with a 1.5 cm tuck under their edges), and from the chest down it is drawn in to 0.8 cm off the trunk; the chest is left as it was.

## A new colour

Meshy's two retextured models through the same script under a new name, then a line in `HoodedCoatBuilder.Colourways`, an item in
`ItemRegistry` (and `IsCoat`), a rung in `ItemRarity`, its prefab in `NetworkedPlayerWiring` and the scene builder, a `/spawn` word; rebuild
the scene and the title.

## Tests

`HoodedCoatTests` (each colour a coat of its own with both hoods and their paint, within budget, the skirt to the thighs, hood up over the
head; the chat's words; every player body has `HoodSync`), `HoodedCoatPlayTests` (hood down when put on; up and down here and on another
player's copy; told again while up; nothing to raise with no coat). Pictures: `LongCoatCapture.RenderThePlayerInTheBrownCoat` and
`...HoodUp` (`browncoat-*.png`).
