# Hairstyles: choosing one, colouring it, placing it, adding one

## What there is now

- **Styles** (`CharacterHair.ValidStyles`): `none`, `hair1` (the bob cut) and
  `danny` (added 2026-09-27 from a user-supplied `Danny.glb` -- SEE ITS OWN
  WARNING BELOW: the model does not actually look like hair). The
  earlier `embermane` and `animehair0` were removed as outdated (2026-09-26),
  models and all. A character saved wearing either still carries the name;
  `SetStyle` ignores a name that is not in the list, so they load **bald** until
  they pick again. A save file stores the string, so a style's name never
  changes once shipped.
- **The selector**: the creator has one button per style (BALD / BOB), built from
  `ValidStyles`, so a new style gets its button with no builder edit. The buttons
  share one row and narrow to fit the 200-unit column (`HairButtonRow`); past
  five or six styles that wants to become a picker. The same list is wired onto
  the character list's carousel heads, the player in the world and the in-game
  redraw creator (`HairAssets.StyleMeshes`, which walks `ValidStyles`, so the
  lists cannot drift out of order).
- **The colour**: any colour. A card down the **right** of the creator (the left
  column is full) with a saturation/value square, a hue strip, a preview and 12
  hair-coloured swatches (`HairColourPanel`, `HairColourControls`). It only
  appears while the character has hair. The colour is kept while bald and worn by
  whichever style comes next.
- **The automatic fit**: `CharacterHair.FitToHead` scales the mesh so it is
  `WidthOverHead` (1.12x) the head's width, stands its top `CrownLift` above the
  crown and centres it on the head. It only needs the mesh's bounds.

## How a colour is kept and shown

- `CharacterSave.HairColour` holds it (default `DefaultHairColour`, the golden
  blond every head wore before). A save from before hair had a colour has no
  such field and loads as the default; a see-through colour is repaired to the
  default, black is a perfectly good colour and is left alone.
- `CharacterHair.SetColour` dyes the worn mesh **through a MaterialPropertyBlock
  per renderer**, so the one shared `Hair.mat` is never touched and every head
  has its own colour. (The cost is that those few renderers do not batch, which
  is nothing for a head.)
- It is applied everywhere hair is shown: the creator, the character list's
  pedestal bodies, the world player (`CharacterIdentity.Apply`), the ReDraw
  creator (which opens on the character's own colour and saves a new one back)
  and other players over the network (`NetworkedPlayerIdentity` sends it with
  the style).

## Placing a style by hand (no console)

1. Press **Play** (any scene with a head: the title's creator, or the world).
2. In the Hierarchy select the character (the creator's body, or the player)
   and look at its **Character Hair** component.
3. Pick the style in the **Style** drop-down there if it is not already worn.
4. Press **Select the hair**. The worn hair (an object called `Hair` under the
   head bone) is selected and the Scene view frames it.
5. Drag it with the Move / Rotate / Scale tools, or type numbers into its
   Transform. Every change is live on the head.
6. Press **Save this placement**. The numbers are written into
   `Assets/CityX/Character/Resources/HairFits.asset`, which survives leaving Play
   Mode and is read by every head wearing the style: creator, character list,
   player, redraw creator.

**Go back to the automatic fit** deletes the saved entry. The **Now** line in the
Inspector shows the current numbers if you want to paste them somewhere.

Nothing is applied until Save: while dragging you are moving one head's hair,
not the shared fit.

## What the Hair1 model is

`Hair1.glb` is a full **bob cut**: one mesh of about 800 vertices, no UVs, one
plain material, a node that scales it 1.41x and lifts it 1.3 m. That node
transform is **baked into the vertices once** (`HairAssets.Hair1` writes
`Hair1Mesh.asset`): a glTF with one node imports as that node itself, so a fit
measured on the raw vertices lands the hair the wrong size and floating. Delete
`Hair1Mesh.asset` to re-bake from a changed model.

It covers the whole head: a straight fringe across the forehead, the sides down
past the ears, and the back to the nape. It faces +Z (fringe to the front), so its
yaw is 0, and the automatic fit sits it well without hand tuning.

## Danny: wired in, waiting on a corrected export

`Danny.glb` (2026-09-27) imports and bakes cleanly -- same single-mesh-under-one-node
shape as Hair1, no textures, one material -- and is fully wired (`CharacterHair.Danny`,
label "DAN", its own button, `HairAssets.Danny()`), so it is mechanically a real
hairstyle option today. But **rendered on a real head the current file is not a full
hairstyle**: a couple of small gold spike shapes sit near the ears and the top of the
scalp is bald -- see `Logs/CityShots/hair-danny-*.png` (close-ups) and
`hair-danny-wide-*.png` (the whole worn object, pulled back).

Diagnosed with the user directly (their own Blender outliner screenshot): their scene
has TWO mesh objects for this style (`Mesh_0.001` and `Mesh_0.002`), and only one of
them made it into `Danny.glb` -- whatever export step was used dropped the second
piece. Separately, the exported mesh (37,478 vertices) is roughly 25x denser than
Blender's own reported 2,899 faces, almost certainly because a Subdivision Surface
modifier's RENDER level is set higher than its VIEWPORT level (the status bar shows
viewport; glTF export bakes using render). Told the user to select both mesh objects
(Limit to: Selected Objects) and match the two subdivision levels before re-exporting.

**When the corrected file arrives:** replace `Assets/CityX/Character/Hair/Danny.glb`,
delete `DannyMesh.asset` to force a re-bake, rebuild the Title scene, and look again
before trusting the automatic fit -- a style this dense is also worth weighing against
the "keep asset size small" standing practice once its real shape is confirmed.

## Adding another style

1. Put the model in `Assets/CityX/Character/Hair/`.
2. Add its name to `CharacterHair.ValidStyles`, give it a short label in
   `LabelFor` (it has to fit a narrow button), and a yaw in `YawFor` if the
   model does not face +Z.
3. Add a case for it in `HairAssets.MeshFor`. A model file is baked with
   `BakedFromModel` (see `Hair1()`).
4. Rebuild the scenes (Title, and CityMassing). Place it as above if the
   automatic fit is not right. Colour needs nothing: it works on any style.

## Tests

- `CharacterHairFitTests` (EditMode): the fit's maths, saved-vs-automatic
  precedence, the baked mesh's size and placement, every style having a label
  and a mesh, the button row fitting the column, removed styles loading bald,
  and the colour (default, any colour, worn mesh only, kept while bald, opaque).
- `CharacterSaveTests` (EditMode): hair colour round trip, old saves, repair.
- `TitleScreenWiringTests` (EditMode): every colour swatch is wired to a picker.
- `CharacterCreatorPlayTests` (PlayMode): Hair1 on the real head, each button
  selecting its style, the colour card appearing only with hair, every swatch
  dyeing the hair, the square reaching an arbitrary colour, saving a fit.
- `CharacterSelectCarouselPlayTests`, `PlayerRedrawStarterPlayTests` (PlayMode):
  a saved colour on the pedestal body; ReDraw opening on it and saving a new one.
- `TitleScreenCapture.CaptureHair1` writes front / side / back head close-ups to
  `Logs/CityShots/`; the `creator-hair` shot wears blue hair with the card open.
