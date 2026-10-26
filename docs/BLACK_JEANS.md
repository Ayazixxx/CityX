# The Black Jeans

Black denim jeans, worn on the legs. The owner, 2026-10-24: "add them as "Black Jeans" and try to fit it to the player the best you can il
make adjustments if needed". Spawn them with `/spawn jeans` (or `black_jeans`), put them on with `/equip jeans` or by picking them up (E).

## Where they come from

- Meshy's "Blank Avatar in Jeans" (one 363k-triangle surface, body and jeans fused). **The owner cut the body away** in Blender, reduced the
  jeans to 11,335 triangles and smoothed the creases by hand: `CityX/SourceArt/BlackJeans/BlackJeansCut.glb`, still where Meshy's figure stood.
  Reducing further (7.9k) wiped out the stitching and was undone.
- The texture is Meshy's black denim, baked from Meshy's textured model onto the owner's cut (its own layout; the faces that caught the body
  under the jeans, the waistband's inside and the hems' edges, repainted denim): `black_jeans_color.jpg`, 2048, 232 KB. The game takes it at
  1024 (77 KB): `tools/model_qa.py` warns past 1024, and the stitching still reads.

## Other colours (2026-10-25)

Owner: "get the textures from these two and add them as wearable jeans options ingame. item names "Blue Jeans" and "Bred Jeans"", then "do
the same process with this one too call it maroon jeans", and "keep them at a reasonable file size while trying to maintain the design".

- Meshy's retextures of the same "Blank Avatar in Jeans" (same figure, same place, each with its own UV layout).
  `SourceArt/BlackJeans/jeans_colour_bake.py` bakes each one's colour onto the owner's cut (Cycles, selected to active, 3 cm reach). Faces
  that caught the mannequin's grey skin (60% of their texels pale and colourless: the waistband's inside, the hems' edges) are painted the
  denim's median colour, and white stitching inside a face is kept. The pieces are grown 12 texels into the empty map, and the rest is filled
  flat (seams stay clean in the 1024 copy, and flat costs the file nothing).
- `blue_jeans_color.jpg`, `bred_jeans_color.jpg`, `maroon_jeans_color.jpg` (2048), then `black_jeans_build.py` for the game's 1024 copy
  (its JSON comes out byte for byte the Black Jeans' mesh, so only the picture is kept): BlueJeans.jpg 297 KB (the fine twill weave),
  BredJeans.jpg 140 KB, MaroonJeans.jpg 55 KB. In video memory each is the same compressed 1024 map as the Black Jeans'.
- One garment, `Jeans` (was `BlackJeans`, renamed with its GUID), with its item id serialized like the T-shirt's. `JeansBuilder` (was
  `BlackJeansBuilder`, menu CityX > Rebuild Jeans) builds the four prefabs on the one mesh: items `black_jeans`, `blue_jeans`, `bred_jeans`,
  `maroon_jeans`. `/spawn bluejeans`, `bredjeans`, `maroonjeans`. Other players see them (`NetworkedPlayerWiring`).
- Tests: `BlackJeansTests.EveryColourIsTheSamePairWithItsOwnItemAndTexture`. `BlackJeansCapture.RenderEveryColour` writes
  `jeans-<blue|bred|maroon|khakis>-<front|behind|threequarter>.png`.
- **Khakis** (2026-10-26). Owner: "add these as a colorway for the jeans and call the items "Khakis"". Meshy's tan cotton retexture of the
  same figure, through the same two steps: `khakis_color.jpg` (2048), then `Khakis.jpg` (1024, 74 KB). Item `khakis`, `/spawn khakis`.

## How they are built

| Step | Where | What |
|---|---|---|
| Fit | `SourceArt/BlackJeans/black_jeans_fit.py` (Blender, headless) | the cut onto this game's body: `BlackJeans.glb`, the file to adjust in Blender |
| Skinning | `SourceArt/BlackJeans/black_jeans_build.py`, shared steps in `SourceArt/garment_rig.py` | the fit is not moved; weights from the body, reworked (below) |
| Mesh | `Wearables/Editor/BlenderGarmentMesh.cs` | the JSON into a skinned mesh bound to the arms-down skeleton |
| Body under them | `Wearables/Editor/BodyUnderGarment.cs` | which of the body's triangles the jeans hide |
| Item | `BlackJeansBuilder`, `BlackJeans`, `ItemRegistry.BlackJeans` (legs slot) | the prefab, built with the scene; `NetworkedPlayerWiring` lists it, so other players see it |

To adjust them: in Blender, the collection "Black Jeans on the game body" holds the game's body and the jeans on it. Move the jeans there,
export them over `BlackJeans.glb` (selected only, glTF binary), then:

```
"C:/Program Files/Blender Foundation/Blender 4.2/blender.exe" --background --factory-startup --python CityX/SourceArt/BlackJeans/black_jeans_build.py -- CityX/SourceArt/BlackJeans/BlackJeans.glb CityX/SourceArt/BlackJeans/black_jeans_color.jpg CityX/Assets/CityX/Wearables/Models/BlackJeans.json CityX/Assets/CityX/Wearables/BlackJeans.jpg
```

and rebuild (`CityX > Rebuild Black Jeans`, or the scene). `black_jeans_fit.py` is only run again to start over from the cut: it overwrites
`BlackJeans.glb`.

## The fit (`black_jeans_fit.py`)

1. **Scaled** as the T-shirt's Meshy figure was (same 1.9 m mannequin): feet to our 2.6 m body's height. The jeans then lie within a few cm.
2. **The legs straightened.** Meshy's lower legs splay forward (2.6 cm at the ankle). Each leg's middle is moved, height by height, onto ours
   (straight down from the hip joint), fully below 0.50 m, not at all above 0.62 m. The body's own cross-section was not used: at the ankle it
   takes in the foot and moved the hems 8 cm forward.
3. **Nothing of the body comes through.** What lies inside the body or within 1.2 cm of it is pushed out a little at a time, each push spread
   over its neighbours. Each push is also given to the cloth lying just outside it: the waistband's lining pushed alone met its outer layer
   (faces on faces, which flicker).
4. **The waistband tucked in.** A shirt's hem hangs over the waistband from 0.92 m up, 1.2-2 cm off the body. The waistband stood out to
   4.6 cm (the belt loops) and came through the shirt. From 0.89 m up (wholly from 0.94 m) how far it stands out past 1 cm is halved, its
   layers kept in order. Cut to 0.3, a loop met the band (faces on faces).
5. Shaded smooth with the seams, pockets and loops crisp (edges folded over 30 degrees).

## The skinning (`black_jeans_build.py`)

Each point first takes the weights of the nearest skin (`garment_rig.body_weights`). Trousers keep the shins, which a coat or a shirt folds into
the thighs (`TROUSERS_FOLDED`); the feet go to the shins. Then:

- **No arm, hand or head.** The hands hang beside the hips; their small shares (6% at most) go to the hips. The waist keeps the spine's
  share, as the skin under it does.
- **Smoothed** over the whole mesh (30 passes). Taken from the nearest skin, the weights jumped across the buttocks' crease (hips on one point,
  thigh and shin on the next): a bent knee tore the cloth there, an edge stretched 24 times its length.
- **Thigh and shin split by height**, over 6 cm either side of the knee. The body blends the shin far up the thigh (a third of it 12 cm
  above the knee). On the back of the thigh, far behind the bone, that swung the cloth out of the rest when the knee bent.
- **The crotch's middle follows the hips** by up to 60%. Between the legs, pulled half toward each leg, a stride stretched it 7 times over.
- **The waistband follows the hips alone** (from 0.86 m, wholly from 0.94 m), as the shirt's hem over it does. A leg swinging back pulled the
  back of the waistband out through the shirt.

## The body under them

- **Always hidden:** the thighs and the shins. Hidden only where covered, a calf behind a bent knee came through.
- **Hidden where covered:** the hips, up to the waistband.
- **Always drawn:** the top 4 cm under the waistband (hidden, its cut edge showed above the band as a row of dark teeth) and everything
  below 4.5 cm over the hems' lowest point, 0.106 m (hidden, a stride lifting a hem showed a gap down to the foot's jagged cut edge). Until
  2026-10-25 it was everything below 0.15 m, which poked out past the hems in some strides (owner: "some ankle still poking through"); 2 cm
  over the hem was "a tad too low", a gap and the jagged edge showed.

## Wearing two garments at once

Each garment used to swap the body's mesh for its own cut copy and put back the one it found. With a shirt and the jeans on, the second cut
the first's copy, which its mask was not made for, so it cut nothing. Taking the shirt off first then put back the whole body under the jeans.

`WornBodyCover` (on the body's skin) now keeps every worn garment's mask. The body is drawn from the whole mesh without all of them at once
(`BodyCover.Cut` with a list), and taking any off gives back exactly what the rest no longer cover. The coat and the shirts go through it
too.

## Tests

- `BlackJeansTests` (EditMode): the legs slot and the name; within 12k triangles with a map of at most 1024; every weight adds up, nothing
  follows an arm, a hand, the neck or the head, and each shin carries a tenth of the points; from above the hips to the ankles; hides the body.
- `BodyCoverTests` (EditMode): two garments both cut theirs, the same set in any order cuts the same mesh, a stale mask cuts nothing.
- `BlackJeansPlayTests`: the legs hidden and given back; worn with the T-shirt, either taken off first leaves the other's cut; through a sprint
  no edge of 1 cm or more stretches past 5 times its length (about 4 at the seat when a knee comes up high, as in any skinned trousers; the
  tears fixed above were 7 to 24) and the hems stay within 22 cm of the ankles (about 17).
- `BlackJeansCapture` (Explicit, `-Graphics`): standing, the waist and hems close up, into a sprint (the legs widest apart), the walk and the
  sprint, then without the shirt. `Logs/CityShots/blackjeans-*.png`.
- `tools/model_qa.py --kind trousers` on `BlackJeans.glb`: WARN for the triangle count (fine up to 8k, at most 12k) and the 2048 map in the
  Blender file (the game's is 1024); no holes, spikes, z-fighting or loose pieces.

## Known

- At the widest point of a stride, from the front, a jagged strip of ankle skin can show under the planted leg's hem. The hem follows the shin,
  the ankle skin partly follows the foot, and garments have no foot bone. Clothes following the feet would fix it (it would also be needed for
  shoes).
