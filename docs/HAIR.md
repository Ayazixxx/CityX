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

## Danny: working now, in every scene

`Danny.glb` (2026-09-27) imports and bakes cleanly -- same single-mesh-under-one-node
shape as Hair1, no textures, one material -- and is fully wired (`CharacterHair.Danny`,
label "DAN", its own button, `HairAssets.Danny()`). The file itself was never
re-exported (still the original commit's mesh -- confirmed via `git log`), but the
user's own hand-placed fit (dragged live in the creator, saved through the Inspector's
"Save this placement" button) turned it into a real, full-coverage hairstyle -- see
`Logs/CityShots/hair-danny-front/side/back.png` for the current look. The earlier
"a couple of gold spikes, bald scalp" report was the AUTOMATIC fit on this mesh, not
the mesh itself; a hand-tuned fit fixed it completely, no re-export needed after all.

**A second, separate bug then made it invisible in the actual game only:** the world
player's `CharacterHair` (added at scene-build time by `CityMassingSceneBuilder`, not
baked into `Player.prefab` -- see its own `HairAssets.AddTo` call) had its
`StyleMeshes` array frozen from BEFORE Danny existed (`CityMassing.unity` was last
rebuilt for the bullet-hole/knock-shatter fix, a commit after Danny's own wire-up
commit that only rebuilt the Title scene). One entry (`Hair1Mesh`) instead of two, so
`MeshFor("danny")`'s index landed past the end of the array and `CharacterHair.cs:357`
silently returned null -- no error, just nothing drawn. Looked perfect in the creator
(which `TitleScreenCapture` rebuilds fresh every time), invisible in the real game.

Fixed by rebuilding the scene (`CityX/Rebuild Massing Scene` menu item, or
`CityX.ViewEditor.CityMassingSceneBuilder.Build` via `-executeMethod`) so
`StyleMeshes` regenerates from the current `ValidStyles` list (now both `Hair1Mesh`
and `DannyMesh`). **Lesson for next time a style is added or changed:** rebuild BOTH
the Title scene AND CityMassing -- the Adding-a-style checklist below already said
this, but it is easy to do one and not the other, and the failure mode (invisible
ONLY in the real game, fine everywhere else) is a confusing one to hit by surprise.

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

## Batch added 2026-10-01: animehair, chadhair, oldmanhair, spikex, swipecut

User-supplied models in `Assets/CityX/Character/Hair/` (`AnimeHair.glb` = the user's `animehair6.glb`, `ChadHair.glb`,
`OldManHair.glb`, `SpikeX.glb`, `SwipeCut.glb`), styles `animehair/chadhair/oldmanhair/spikex/swipecut`, labels
ANI/CHAD/OLD/SPIKE/SWIPE. All on the AUTOMATIC fit (nothing hand-tuned yet) -- place them with the Inspector gizmos + Save.

- **The button row now wraps**: 8 styles in one 184-unit row was ~19 units a button. `HairButtonRow` puts at most 4 per row
  (`PerRow`), `RowOffsetY`/`Rows` place further rows, and the hint label under it moves down by the extra rows. Both the
  title creator and the in-game redraw creator use it. Past ~8 styles this wants to be a picker.
- **Multi-primitive models bald the head**: `animehair` has TWO glTF primitives (an 11-vertex stub with material 0, the real
  hair with material 1). The hair renderer has one material and so drew only the stub. `HairAssets.Bake` now MERGES all
  sub-meshes into one. Delete a style's `*Mesh.asset` to re-bake it. Danny / Hair1 were baked before this change and
  have NOT been re-baked -- Danny's original "a couple of spikes" look may have been the same cause (not checked).
- `oldmanhair` really is side tufts with a bald top (renders that way); the other four sit on the head sensibly.
- Rebuilt BOTH Title and CityMassing scenes (the world player's `StyleMeshes` is baked at scene build -- see the Danny note above).
