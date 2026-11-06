# Premade face parts: the plan

For whoever builds the parts behind the FACE tab. **Code** = what the code does today (paths under `CityX/Assets/CityX/`). **Plan** = proposal.

## 1. The head today
- **Code:** paint is one 768 x 1792 RGBA32 atlas, a row per part; the head is row 0, 256 texels tall with 8-texel margins (`Character/Body/PaintAtlas.cs:35-64,257`, `BodyPaint.cs:87-99`).
- **Code:** the head is its own sphere, r 0.35 m (`FusedBodyShape.cs:105`), unwrapped equirectangular over the whole row: u = longitude, v = latitude, crown at the row's low v (`HeadMesh.cs:18-21,60-62,106-124`). `PaintAtlas.cs:17-20` still says box-mapped: stale.
- **Code:** the head's frame is the body's, facing +Z (`Movement/BodyPosture.cs:67`, `Editor/FusedBodyBuilder.cs:394-397`); +X is the character's right (`BodyRig.cs:249-252`). So the face's centre is u = 0.25 mid-row, and the u = 0 seam is at the right ear: no face part crosses it.
- A face texel is about 2.9 mm across, 4.6 mm down (768 round, 240 pole to pole).

## 2. Authoring (Plan)
- One 256 x 256 RGBA PNG per part: the head seen straight from the front, edge to edge (0.70 m, 2.7 mm a pixel; the atlas keeps no more). Drawn on a template (a front render of the blank head); where it is drawn is where it sits. Lines 2 px or wider; shading in black at low opacity darkens any skin.
- Optional `<id>_tint.png`: greys multiplied by the player's colour (irises, brows, lips); the main PNG is drawn as is.
- Eyes and brows are drawn both sides; SPACING slides the picture's two halves apart.
- Pictures in `Character/Resources/FaceParts/<id>.png`, Read/Write, uncompressed, loaded by id while composing and released: the catalog references only 128 px icons, so loading it does not load every picture. Not PNG bytes decoded at runtime: ImageConversion can be stripped (`BodyPaint.cs:873-881`).
- `FacePartCatalog` (drafted) later gains per category `Paired` and `Layer` (draw order), per part a default colour. Guard test: unique ids, every picture present, readable, 256².

## 3. On the save (Plan)
- `CharacterSave.Face`: list of `{Category, Id, Colour, UpCm, Scale, SpacingCm}`; empty = no parts, which is how old saves load (`Core/CharacterSave.cs:11-19`). `Repair` clamps (up ±6 cm, size 0.75-1.25, spacing ±4 cm), alpha 1, one per kind (three extras).
- **Code:** the server copies only skin, paint and hair (`Core/CharacterService.cs:143-149`, in `Create` and `Save`): Face must go there or it is dropped. Also `UI/Title/CharacterSelect.cs:114-120,229-238`, `UI/Creator/CreatorOverlays.cs:134-136`, `PlayerRedrawStarter.cs:81-131`.

## 4. Drawing it (Plan)
- A pure `FaceLayer` makes a head-row overlay (768 x 256): per texel, invert HeadMesh (texel -> longitude, latitude -> sphere point), keep it if it faces forward (z > 0.25), project straight back onto the picture (picture right = character's left, as `Wearables/Editor/ShirtPrintBaker.cs:112` notes), apply up/size/spacing, average four bilinear taps, blend parts by `Layer`. Only texels under each part's opaque bounds.
- Not BodyPaint's texel map: built per `Bind` from the creator's posed body, near 100 MB on the way (`BodyPaint.cs:72-77,408-534`); worn bodies never have one. HeadMesh inverts exactly, and only row 0 is touched, so nothing bleeds onto the neck.
- Reuse: `Wearables/ShirtPrintLayout.cs:26-57,88-129` already lays a picture over cloth with a premultiplied bilinear sample, but privately, in Wearables, which Character cannot reference (Wearables -> Character -> Core): move the sampler to Core.
- Not a shader decal: sharper and animatable (blinks, talking), but it changes CityX/Lit and its outline for everything, adds a texture a body, and the eyedropper and bucket would not see it. The saved choices do not care, so it can come later.

## 5. Editable (Plan)
- Paint stays paint: `_pixels`, `Encode`, the 24-snapshot undo (`BodyPaint.cs:783-837`) unchanged. `BodyPaint.SetFace(layer)`: `Apply` (`:160-164`) uploads paint, then row 0 blended (`SetPixels32(0, 0, 768, 256, ...)`). Brush and bucket paint under parts; swapping a part never touches paint. `SampleAt` reads `_pixels` (`:248-261`): make the eyedropper read the blend.
- A pick is not on the paint undo stack, like hair; NONE removes it. `HasEdits` is paint-only (`CharacterCreator.cs:712`): count a changed face so BACK still asks.

## 6. Worn bodies
- **Code:** four places decode paint to wear it (`new BodyPaint`/`Decode`/`ShrinkForWearing`): `Player/CharacterIdentity.cs:210-219`, `Network/NetworkedPlayerIdentity.cs:245-255`, `UI/Title/CharacterSelect.cs:680-690`, `Weapons/Editor/SwordsmanBuilder.cs:93-104`. **Plan:** one shared helper first, then add the face once.
- **Code:** no paint = no atlas, only a tint (`CharacterIdentity.cs:225`): a face-only character needs a skin-filled atlas (1.4 MB compressed).
- **Code:** `ShrinkForWearing` compresses (`BodyPaint.cs:1007-1015`): 4x4 blocks are ~1.1 x 1.8 cm on the face. Check thin lines in a capture.

## 7. Other players
- **Code:** the look goes once when the body appears: ServerRpc -> ObserversRpc(BufferLast) with name, skin, hair, hair colour, paint bytes (`NetworkedPlayerIdentity.cs:183-191,220-227`), paint up to ~200 KB. The relay checks nothing.
- **Plan:** add `byte[] face` from a pure codec (like `Network/RagdollPoseCodec`): ~30 B a part, a whole face under 200 B. Receivers clamp, skip unknown ids (as `CharacterHair.cs:49-54` does for styles) and compose locally, so better art for a part reaches everyone wearing it. A face-only player sends ~0.1% of a painted one.
- **Code, a gap today:** ReDraw's SAVE applies and saves locally (`PlayerRedrawStarter.cs:117-131`); nothing re-sends, so others keep the old paint and hair. Re-send on SAVE (BufferLast replaces it).

## 8. The FACE tab (Plan)
- `FacePage` (drafted) builds each kind's tiles from `FacePartCatalog.Current`. Next: NONE first; a press calls `CharacterCreator.SetFacePart(kind, id)`, rebuilds the layer, applies; the worn tile framed gold, polled like `UI/Creator/CreatorControls.cs:119-128`.
- Baked into Title.unity, tile Buttons need a ButtonAction (`Tests/EditMode/TitleScreenWiringTests.cs:127-152`): append `CreatorFacePart` at the end (`UI/ButtonAction.cs:82-94`), Value = part index, plus a kind-key field.
- Under the grid (now the note): the part's colour (one picker for hair and face, not a third beside `CreatorControls`/`HairColourControls`) and UP, SIZE, SPACING sliders, hidden with nothing picked (as `HairColourControls.cs:71-81` hides while bald).
- Adding a part = catalog entry + picture + rerun `TitleScreenBuilder.Build` (ReDraw builds at runtime). A guard test compares baked tiles with the catalog.

## 9. Order and tests
1 shared wearing helper; 2 `FaceLayer` + EditMode tests (a centred dot lands at u = 0.25; picture-left lands on +X; nothing outside row 0 changes); 3 save, `CopyLook`, `Repair`; 4 creator overlay + capture; 5 tiles and sliders; 6 codec, RPC, ReDraw re-send, `NetworkedPlayerStateSyncPlayTests`: a copy wears the face; 7 docs.
