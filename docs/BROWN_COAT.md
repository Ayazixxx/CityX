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
4. **The back vent** (owner, 2026-11-24, of a mark on the seat: "is that line around where the butt is a hole in the coat?"): Meshy made a vent
   up the back, an opening in the outer cloth with a second layer behind it; kept off the body (step 6) both were pushed to the same place and
   the inner one came out through the outer one, a grey patch ringed in black. Every face a ray from behind meets after the first, up the
   vent, is dropped, and the cloth there smoothed. Left: a faint dashed arc where the outer layer's opening ends (the outline draws its edge).
5. **The armpits, made as for a T-pose** (owner, 2026-11-24: "there is holes in the armpit ... fix the holes without having a stretch when arms
   move", "make it wrap around the armpits cleanly from a tpose position", "make the armplit fix line cleaner and less jagged"). Arms down,
   Meshy's sleeve underside and the coat's side were one sheet: joined it stretched into a web, cut apart it opened into a slit you saw the
   sky through. Now:
   - a point is a sleeve's when it lies nearer the arm's surface than the trunk's; stray single points go to the side round them;
   - below 1.81 m the seam between each sleeve and the coat is cut, and **each side gets its own wall** (`close_armpits`): the sleeve closed
     into a tube, the coat's side closed, the two meeting along the top of the armpit like a hinge. The cut's zigzag edge is relaxed first,
     and each wall is filled in short rungs from the top of the armpit down;
   - **smoothed with the arms out** (`relax_armpits`): the coat is put in a T-pose by its own skinning, the cloth round each armpit smoothed
     there, and put back by the same skinning undone; the two sides of the cut are glued back together where they meet with the arms down;
   - a loose flap left at the bottom of one cut is merged away (`merge_flaps`).
6. The cloth is kept 2.8 cm off the body (a shirt goes under) and 2.2 cm off the head, a ball here where Meshy's is an egg: points too near
   are moved straight out.
7. **Skinned part by part** (`part_weights`, the partitioned weight transfer clothing artists use): a sleeve's point takes the weights of the
   nearest point of its arm's skin, the coat's own point those of the trunk's, read across the skin's face; over the shoulder, where sleeve
   and coat are one cloth, the whole skin's. Each moves as the skin under it does.
8. **Above the crotch the coat hangs from the hips** (`on_the_hips`; owner, 2026-11-24: "the croatch clips through", "the coat needs to go
   over it"): the skin gave the front panels by the crotch a share of the thighs, which the skirt bones carried, and standing they drew the
   panels 7 cm back into the crotch. From 0.95 m up a point's share of a thigh goes to the hips; below, the skirt swings with the legs
   (the skirt bones, kept off them by `SkirtCloth`). Then the collar and what lies near the head on the collar bone; hood up, the hood on
   the head's own bone, so it turns with the face.
9. Each model's paint as one 1024 JPEG (`Wearables/BrownCoat.jpg`, `BrownCoatHoodUp.jpg`): Meshy lays each model out its own way.

## A shirt under it

Owner, 2026-11-24, of a T-shirt through the front panels at the stomach: "make the t shirt fit a bit tigher when brown coat is equipped", "the
top part looks good its just the stomach"; then "there is a bunch of "shards" missing from the t shirt look down the sides ... fix that without
clipping into the jacket too much"; then, of its corners mid-stride: "slightly clipping here with the white shirt going through the coat a tiny
bit". The shared rule (`UnderCoat`) draws a shirt under a coat only in the coat's open front, sized for the parka. Under the Brown Coat
(`UnderCoat.FitsClose`):
- the whole shirt in the opening is drawn (it used to be cut triangle by triangle round the panels, which left a ragged edge of "shards");
- wherever the coat's front panels lie over it (`PanelsInFront`, read off the worn coat on a 5 mm grid) it is pushed back 1.2 cm behind them,
  and within 1.5 cm of their edge, so it goes under the edge (`BehindPanels`);
- from the chest down it is drawn in to 0.8 cm off the trunk;
- it hangs from the hips, as the coat's front does: its share of the legs goes to the hips (`OffTheLegs`), so a knee no longer swings its
  corner out through a panel.

## A new colour

Meshy's two retextured models through the same script under a new name, then a line in `HoodedCoatBuilder.Colourways`, an item in
`ItemRegistry` (and `IsCoat`), a rung in `ItemRarity`, its prefab in `NetworkedPlayerWiring` and the scene builder, a `/spawn` word; rebuild
the scene and the title.

## Tests

`HoodedCoatTests` (each colour a coat of its own with both hoods and their paint, within budget, the skirt to the thighs, hood up over the
head; the chat's words; every player body has `HoodSync`), `HoodedCoatPlayTests` (hood down when put on; up and down here and on another
player's copy; told again while up; nothing to raise with no coat), `BrownCoatFitPlayTests` (on the game's posed body, standing, walking,
sprinting, arms out and forward: never sunk into the crotch, hips or seat, no open edge under the arms, no tear), `UnderCoatTests` (the shirt
whole, behind the panels, its stomach drawn in, off the legs). Pictures: `LongCoatCapture.RenderThePlayerInTheBrownCoat` and `...HoodUp`
(`browncoat-*.png`: besides the standing and running shots, the crotch close, from below as the camera comes in low, the seat close, and the
arms lifted out and forward).
