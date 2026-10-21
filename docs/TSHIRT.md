# The T-Shirt

A plain white short-sleeved tee, worn as a top. The owner asked on 2026-10-19: "put this in as a wearable top called T-Shirt", with the
body separated from the shirt, "the lump and wrinks at the front" flattened "so we can cleanly put a various textures and logos on here for a
bunch of different shirts", and fewer faces. Then: "the fabric should wrap around the bicep tighter". Then, unhappy with the neckline of the
first cut, the owner cut the shirt out by hand: "i have my own open i selected more precisely please lower the face count and fill the missing
space on the collar from the meshy models head being removed", with the flattening and the snug sleeves as before; then "make it rounded
inward and evenly dont make it raise around the shoulders", "i want there to be a visible collar though", and "the shirt stretches around
the armpits please detatch the sleeve from the body", then "it still stretches around the armpits. also the shoulder stitch line is jagged
can you make it straight", with research asked first into how games keep clothes from stretching at the armpit (see Underarms). On
2026-10-20: "make the back collar a little less low" (to a line drawn just under the head) "but again leave the front as it is"; "the sleeve
is clipping through the arm a bit here ... make the parts of the body being covered invisible"; "it like rolls in at the side on both
sleeves, make it shaped like a real shirt"; and, of the front over the crotch, "get rid of this weird boner thing just flatten it out like
the back side".

Spawn it with `/spawn tshirt`, put it on with `/equip tshirt` (also `shirt`, `t_shirt`) or by picking it up (E).

## Where it comes from

Meshy's "Blank Slate Character" (`Meshy_AI_Blank_Slate_Character_1020040122_generate.fbx`, in the owner's Downloads, 5 MB, not kept in the repo)
is a ball-headed mannequin in a tee: one fused 180,738-triangle surface with no UVs, 1.9 m tall. **The owner cut the shirt out of it by hand**
in Blender: `CityX/SourceArt/TShirt/TShirtCut.glb`, 8,550 triangles, still where Meshy's figure stood. Scaled as the whole figure was (feet to
our body's 2.6 m, its head over ours), it lies within 1-3 cm of our body everywhere, so nothing had to be moved.

`CityX/SourceArt/TShirt/tshirt_build.py` (Blender, headless) makes everything from it:

| Step | What |
|---|---|
| Neck | Meshy's own collar is cut away round the neck: in front along an oval (0.28 m to the sides, 0.2 m ahead); behind along a flatter curve, (x / 0.28)^3 + (y / 0.18)^2 = 1, as on a real tee, whose back neckline sits higher than its front: 2 cm higher than the oval across the middle, its corners where the oval's were. A smaller oval behind (0.16) brought the corners up against the head, which dips 3.4 cm in a sprint and sank 3.7 cm into them. Where the mannequin's neck had been joined to Meshy's collar, it was notched and folded over itself, and it stood up at the shoulders (owner: "collar looks like shit ... make it rounded inward and evenly dont make it raise around the shoulders", then "i want there to be a visible collar though"). |
| Collar | After the reduction, a crew neck's band is sewn round the oval, the same all the way round: its heights put on a smooth curve, then one cross-section swept along it (a groove at the seam, the rib raised 5 mm off the cloth and 2.6 cm wide, its edge rounding over and down into the neck). It leans up toward the neck by 40 degrees in front and behind and only 20 at the shoulders, so it never stands up beside the head, and nowhere sits closer than 8 mm to the body. |
| Sleeves | Pulled in toward each arm: they stood 4-8 cm off it like a bell. The smooth looseness is cut to 30%; the folds and the cuff's roll keep 80%. |
| Front and back | How far the cloth stands off the body is evened out (smoothed 200 times), never closer than 1.5 cm: no lumps or wrinkles under a design, and the chest no longer shows through. On the back's middle this goes up to the collar (lower, a fold of Meshy's collar dented the band raised across it). Below the belly (1.25 m, wholly by 1.15 m) the front then hangs straight down from its own line, as a shirt does: kept a set distance off the body, it came out over the crotch in a point 4 cm proud. Where the body stands out past the hanging line, the cloth would span it in a broad swell like the back's over the seat; with this body none is needed. Cloth still inside the body is pushed out in small rounds spread over its neighbours. Meshy's folds stay at the sides, the armpits and the hem. |
| Reduced | to 3,800 triangles; a point standing out of the surface is put back level with its neighbours (moved only out of the surface: moved to their middle, a point of a long thin triangle once folded the front into a pit). With the collar, the remade underarms and the seams: 7,281 in all, inside the budget for a shirt (3-8k, at most 12k). |
| Underarms | Owner: "it still stretches around the armpits". Meshy's mannequin stood arms down and its shirt never went between the arm and the ribs: from the armpit down past the cuff's top it ran from the chest straight across to the front of the arm, round the arm's outside and back across to the shoulder blade, like a bat's wing. The sleeve had no inside and the shirt no side there, and any arm swing stretched that web (16x in a sprint). Games model clothes arms out, with a gap under the arm, and lay clean game meshes over sculpted ones (retopology); the same is done here, on each side. The armhole is a flat plane through the armpit's crease (1.76 m, where the arm's skin meets the ribs') and the shoulder's top, so the seam over the shoulder is straight ("the shoulder stitch line is jagged"); under the arm it is the crease. The sleeve is made again as a tube from the armhole to the cuff, each point as far off the arm's axis as Meshy's sleeve was there (read with a ray), and where Meshy had none, inside the arm, 2 cm off the skin; its edge is rolled under at the cuff. Its rings have a point at least every 3 cm round the arm: they took their places from the armhole's, and the crease under the arm, short and pushed out of the armpit, has few, so a quarter of every ring was one flat face that cut into the arm and turned the cuff in at the side ("it like rolls in at the side on both sleeves"). The seam over the shoulder is the longest run of the hole's rim on the armhole's plane (a stray point on it once ran alone). The shirt's side under the arm, from the crease down to 1.30 m and 40 degrees round either way, is made again as new cloth carrying on the cloth round it. Standing, there is now a gap under each arm, as in a real tee. |
| Unwrapped | Four pieces at fixed places on the texture, cut like a real tee's sewing pattern along smooth seam lines: down the sides and over the shoulders, round each armhole (a set-in sleeve with a rounded cap), under each sleeve. Every piece is upright, not mirrored, and at one scale (479 texture pixels per metre on a 1024 map). |
| Skinned | From the body under it (`SourceArt/garment_rig.py`, shared with the Midnight Coat). The shirt's body follows the torso and never an arm; the hem the hips, never a leg. Each sleeve follows its arm as the arm's own skin does as far down it (near the cuff partly the forearm: the elbow bends there); taken from the skin nearest each point, the rounded sleeve's inside under the armpit took the ribs' scraps of forearm and was drawn out 2.7x by the bending elbow. The armhole's seam follows the half-turning shoulder bone; the sleeve goes over to its arm smoothly along 30 cm of it, the body back to the torso along 20 cm (over 5 cm the cloth at the seam was drawn out 6x), fading out before the collar. The collar's sides follow the neck by 60% (on the torso alone, the head sank 1.8 cm into it in a sprint). In a sprint the cloth is now drawn out 2.1x at worst, at the back of the armpit, and only 38 of its triangles' sides past 2x; the body's own skin there is drawn out up to 12x. The head dips 1.0 cm into the collar at worst. |

To rebuild after changing the cut or the script:

```
"C:/Program Files/Blender Foundation/Blender 4.2/blender.exe" --background --factory-startup --python CityX/SourceArt/TShirt/tshirt_build.py -- CityX/SourceArt/TShirt/TShirtCut.glb CityX/Assets/CityX/Wearables/Models/TShirt.json
```

then rebuild the scene (`CityMassingSceneBuilder`), which runs `TShirtBuilder`. A third argument (a folder) also writes preview renders and a
`tshirt.glb` for the model check there. `tools/model_qa.py` passes it on every check.

## Making a new shirt design

`CityX/SourceArt/TShirt/tshirt_uv_template.png` (1024 x 1024) is the layout every design is drawn on:

| Part of the texture | Piece |
|---|---|
| top left (u 0-0.5, v 0.4-1) | the front, seen from in front: the wearer's left on the image's right |
| top right (u 0.5-1, v 0.4-1) | the back, seen from behind |
| bottom left | the right sleeve |
| bottom right | the left sleeve |

The grey grid is 10 cm of shirt per square, and the crosses mark the middle of the chest and of the back at the usual logo height. Paint
anywhere inside the outlines, and a little over them so the seams do not show a white line. `tshirt_test_design.png` is a check design (a dark
grid and a red disc on each cross): `TShirtCapture` wears it, and it lands round and centred on the chest and the back.

A new shirt is then one texture plus one item, with the same mesh.

## Printed shirts: the Blood Raven T-Shirt

The owner, 2026-10-20: "put this logo on the shirt and call it "Blood Raven T-Shirt", let me adjust the logo positioning too in unity".
`/equip bloodraven` (also `blood_raven`) or `/spawn bloodraven`.

**Placing the logo:** menu **CityX > Place the Blood Raven Logo** selects `Wearables/Prints/BloodRavenPrint.asset`. Its Inspector:

| Field | What it does |
|---|---|
| Logo | the picture (`Prints/BloodRaven.png`, transparent background) |
| Side | front or back |
| Right Cm / Up Cm | where its centre is, in cm from the middle of the chest (or the back), as you look at that side; negative is left / down |
| Width Cm | how wide it is printed (34 cm to start; the height follows the picture) |
| Rotation Degrees | turned anticlockwise as you look at it |

A moment after a number stops changing, the shirt is printed again (`Prints/BloodRavenPrintShirt.png`), in Play mode too, on the shirt being
worn. The values are saved in the asset; commit it with the printed PNG.

**How it works:** `ShirtPrintBaker` reads off the shirt's mesh where the middle of each side (1.62 m) lies on the texture and how far a metre
of cloth goes on it (a straight-line fit of the points round the middle), and `ShirtPrintLayout` lays the picture over the cotton there,
blended by its transparency, at 2048 texels (about 960 a metre, so the 34 cm logo is about 330 texels wide). The printed shirt is the
T-Shirt's prefab with its own item (`TShirt.SetItemId`) and a white material carrying the printed texture (the cotton's colour is in the
texture). Another printed shirt is a new print asset, item and three lines in `TShirtBuilder`, like `BuildBloodRaven`.

## In the game

| Part | Where |
|---|---|
| Item | `ItemRegistry.TShirt` (`t_shirt`, "T-Shirt") and `ItemRegistry.BloodRavenTShirt` (`blood_raven_t_shirt`, "Blood Raven T-Shirt"), Torso slot; `TShirt : SkinnedGarment`, its item set by its builder |
| Prefab | `TShirtBuilder` (`CityX/Rebuild T-Shirt`, `CityX/Rebuild Blood Raven T-Shirt`), built with the scene: plain off-white cotton, matte; the printed shirt the same with its printed texture |
| Body under it | the torso and upper arms always hidden (`BodyUnderGarment`), except the skin the shirt leaves bare: inside the crew neck (in front just inside the band, behind out to its seam, drawn whole along a clean line) and the upper arm past each cuff, read off the cuff's own edge on the mesh (`SleeveCuff`) and drawn 1.5 cm up inside it, behind the rolled lip; the hips hidden where the shirt covers them. Hidden only where covered, the upper arms came through at the back of the armholes; shown below one height, the arm came through the back of each slanted cuff ("the sleeve is clipping through the arm"); cut off right at the lip, the skin's ragged edge showed up the sleeve. |
| Other players | not shown on copies, like all clothing (`docs/MULTIPLAYER_STATE.md`) |
| Tests | `TShirtTests` (registry, budget, weights, shape, each sleeve on its arm and the body on no arm, no seam ever opens, no arm skin drawn inside a sleeve but just inside a cuff, each sleeve round the arm with no flat side, a design's front on the chest and back on the back, upright and not mirrored), `TShirtPlayTests` (the body hidden and given back; the head never sinks into the collar in a sprint; the cloth never drawn out past 2.3x, and few edges past 2x, in a sprint), `BloodRavenTShirtTests` (its own item, the T-Shirt's mesh and hiding, the logo where its print says and only there; the printing maths: placement, shape, turn, transparency), `TShirtCapture` (renders, plain, with the test design and the Blood Raven shirt, a close-up of the chest, with close-ups of the collar from in front and from behind and above, a sleeve and the back of an armhole) |
