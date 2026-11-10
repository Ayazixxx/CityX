# Character creator: painting the body

The creator (title screen → create) paints a texture, the **paint atlas**, that the body wears. `CharacterCreator` takes the mouse, `BodyPaint` owns the atlas and the brush, and `PaintAtlas` decides where each body part lives in the atlas.

## The brush is measured on the body (2026-10-10)

The owner reported that "the brush sizes dont scale up naturally": size 1 felt "very choppy ... a line or ovel instead of a pixel or circle", and "the 2nd brush size is way bigger". There were three causes:

- **Size 1 was one texel.** The atlas is stretched unevenly over the body: a limb's row maps the angle around the limb to u and the position along it to v. One texel on an arm was about 1.5 mm around but 8 mm along, so a size-1 dot was a sliver.
- **Size 2 jumped.** From size 2 the stamp was an ellipse corrected per triangle for that stretch, with a floor of 2.75 texels on the short axis. The long axis was multiplied by the stretch, up to about 12 times.
- **Slanted lines were stairs.** At Godot's 64 texels per row, a torso texel was about 4 mm around and 16 mm up, whatever the brush did.

### What it does now

**Texel map** (`BodyPaint.EnsureTexelMap`). Every triangle is rasterised into the atlas through its UVs, so each texel knows where it is drawn on the body and which way it faces there. A texel can be drawn in several places and keeps all of them. At a joint, the hidden rounded end of one tube shares texels with the visible tube over it. When only one place was kept, a stroke down the arm broke off at the elbow. Texels just outside any triangle borrow a neighbour's place, for two passes and only within their own row, so bilinear filtering never blends unpainted colour in at a part's edge. The map is built once per `Bind`.

**Stamp** (`StampSphere`). A stamp paints every texel within the brush's radius of the hit point that faces the brush: the dot of the normals must be at least `FacingCutoff` (-0.2), so a big brush on the front of an arm stays off its back. If no texel is that close, the nearest one is painted, so a tap always leaves a mark. Paint crosses from one part's row to another's by itself, for example over the shoulder.

**Sizes.** The radius is `BrushStepMetres` (7 mm) times the size: size 1 is a 1.4 cm dot on the 2.6 m body, and size 15 is about 21 cm. Every step adds the same amount.

**Strokes.** The creator raycasts every 2 screen pixels along the drag. It lays stamps along the body every 0.3 brush radii (`StrokeSpacingMetres`). If two hits are more than 5 cm apart, the cursor jumped from one part to another behind it, and the two are not joined.

**Mirror.** The mirror tool stamps the same sphere at x mirrored, with the normal mirrored too (`PaintMirroredAt`).

### Finer atlas

`PaintAtlas.ColumnDetail` is 2 and `RowDetail` is 4: 768 × 1792 texels, eight times Godot's 384 × 448.

- A torso texel is now about 2 × 4 mm, and the face about 5 × 3 mm.
- The UVs are fractions of the atlas and did not change. `RowMargin` is still two Godot texels.
- Paint saved at Godot's size (`LegacyWidth` × `LegacyHeight`) still loads in `Decode` and `Adopt`: each old texel becomes a 2 × 4 block.
- The Old Guy keeps his baked skin at Godot's size. He is only worn, never painted.
- A body that is only worn (another player's, or a character on the select screen) compresses its atlas on the GPU (`ShrinkForWearing`): about 1.4 MB instead of 5.5 MB.
- Saved paint is gzip of raw RGBA. A busy body stays well under 200 KB (`BodyPaintTests.ABusilyPaintedBodyStillSavesSmall`). FishNet splits large messages on the reliable channel.
- The pixels are bytes (`Color32`). Undo keeps 24 whole-atlas snapshots, about 5.5 MB each.

## The click target is the body as shown

The creator shows the body in a T-pose, but the mesh is modelled arms-down (`BodyRig.RestBindMatrix`). The collider and the brush used to be the mesh at rest, so a click on an outstretched arm went through it. `CharacterCreator.Initialise` now bakes the skinned mesh as it stands and uses that for both. The body holds still in the creator (`Disarm`).

## Brush ring and size keys

A ring the size of the brush follows the cursor on the body (`ShowBrushRing`). It is dark over light paint and light over dark paint, and sits on the body's layer because the creator's camera sees nothing else. Paint tools such as Meccha Chameleon's paint mode show the brush this way. `[` and `]` step the size, and the slider follows (`CreatorControls.Update`).

## Zooming toward the mouse (2026-11-05)

Owner: "scrolling in and out only zooms into the center of the playermodel but can you make it zoom into wherever you mouse is hovering? so you can zoom into the head or feet or anywhere you want to focus". A notch of the wheel moves the camera 15% of the way along the line through what the mouse is over (`CreatorZoom`, `CharacterCreator.ZoomAt`), which leaves that point where it is on the screen. Off the body it uses the upright plane through the body's middle. The camera now comes as close as 0.9 m from the body's middle (was 1.5 m, where the whole body nearly fitted the view), and 45 cm from what it is zooming at. The further in it is, the further off the middle it may be; back at its starting distance it is back in the middle, so zooming out always brings the whole body back. `Zoom` stays as a straight zoom for controllers later. ReDraw has the same zoom, being the same creator. Tests: `CreatorZoomTests`, `CharacterCreatorPlayTests.ScrollingZoomsTowardWhatTheMouseIsOverAndOutBringsTheWholeBodyBack`.

## Tests

- `BodyPaintTests`: round on a 7:1 stretched surface, size 1 is a dot, equal size steps, an unbroken stroke, no paint through to the back, hidden shared texels, mirror, old saves, and save size.
- `CharacterCreatorPlayTests`: a click on an outstretched arm paints the arm, and the ring has the brush's size. The explicit `BrushCapture` renders `Logs/CityShots/brush-sizes.png`; look at it after any brush change.

## The bucket fills the torso's neck too (2026-10-15)

Owner: "when you do paint bucket fill on torso during character creator with a color, once you load into the game there is like a white
sliver around the neck that visibly wasnt reached by the paint bucket, but is apart of the torso ... make the paint bucket color the whole
torso unless being clicked in a contained area". The torso (the Hips row) and the neck (the Neck row) are one surface on the body but two
rows of the paint atlas, and the fill stopped at its row. `BodyPaint.BucketFillAt` now also crosses between the parts of a group
(`BucketGroups`: torso and neck) over the body itself: from a texel at the edge of its row's painted area to the other part's texels within
1.2 cm of it on the surface, facing the same way, still only through the colour that was clicked -- so an outline drawn round an area keeps
the fill inside, and the arms, legs and head stay fills of their own. Tests: `BodyPaintBucketTests`.

## ReDraw's tools did nothing (2026-10-17)

Owner, with a screenshot of the in-game ReDraw: "the redraw menu is broken, none of the tools work properly", "particularly the eye dropper
tool i noticed wasnt working". ReDraw's panel is built during play (`RedrawCreatorRuntime`), and `ButtonAction`, `CreatorControls` and
`HairColourControls` wire themselves in `Awake` -- which fires the instant a component is added to an active object, before the builder has
said what the button does or which slider is which. So every button woke up as "does nothing" and never listened: the brush/bucket/eyedropper
buttons (stuck on the brush, which is why painting alone worked), the swatches, mirror, eraser, undo, redo, the hue and brush sliders and the
hairstyles. The title screen's creator never had it: it is built in the editor and saved, so its components wake up wired. The overlay is now
built inactive and switched on at the end, as its stage already was. And the eyedropper set the creator's colour behind the panel's back, so
the square, the hue and the preview kept the old colour and the pick looked like it had failed: `CreatorControls` now adopts whatever colour
the creator has. Tests: `RedrawRealInputPlayTests` (a mouse on the real buttons, swatches, sliders, hairstyles, the eyedropper on a painted
spot, the bucket, and the HUD) -- `PlayerRedrawStarterPlayTests` calls the creator directly, which is how this went unnoticed.

## One kit for both creators (2026-10-17)

The title screen's creator (`CharacterCreatorBuilder`, baked in the editor) and ReDraw's (`RedrawCreatorRuntime`, built during play) had
about 500 lines in common, copied by hand, and had drifted (the bug above is what the drift looked like). Both are now built from
`UI/Creator/CreatorKit`: the tool column (`BuildToolColumn`, given the heading, the overlays the tool buttons carry and the two action buttons),
the discard prompt, the prompt parts and the stage's lights. Each creator keeps only what really differs: which body it clones and how,
where its hair and its layer come from, ENTER THE CITY/BACK against SAVE/CANCEL, and the title screen's name prompt. The title scene the
builder writes came out identical, object for object. See docs/CODE_STYLE.md.


## The character list: five on the row, a bin on the card (2026-10-26)

Owner: "make it display 5 character (assuming you have made 5) and the back 2 on the right and left are dark just dark and shadowy figures,
also make it so the direction it scrolls through the characters when you press the buttons on the right and left are inverted. move the
delete button to just a little trash icon that is in the top right of that little panel".

- **Five pedestals** (`CharacterSelect.SlotCount`, `CentreSlot`), laid out by `CharacterSelectBuilder` left to right as the camera sees them
  (it looks back down -Z, so +X is the screen's left: the old "left" anchor stood on the right of the screen). The furthest pair stand further
  out (4.5 m) and back (1.4 m), smaller; their bodies are drawn all in `UI/SelectSilhouette.mat`, an unlit near-black, every colour and
  property block cleared (`CharacterSelect.InShadow`). They only stand with five or more characters, so nobody is on the row twice
  (`CharacterSelect.CharacterFor`).
- **The arrows the other way round:** the next character now stands on the right, so the right arrow brings it in from the right and the
  row slides left (it came in from the left); the count still goes up with the right arrow.
- **Delete is a bin** in the stat card's top-right corner (`MakeTrashButton`, drawn from rectangles, red under the pointer), still asking
  first; PLAY and CREATE NEW close up in the middle.

Tests: `CharacterSelectCarouselPlayTests.FiveStandOnTheRowTheBackPairInShadowAndTheNextOneOnTheRight` (five filled with
six saved, the back pair in shadow, the next one on the right and brought in by the right arrow), `TheBackPairOnlyStandsWithFiveSoNobodyIsOnTheRowTwice`,
`TitleScreenWiringTests.TheCharacterListDeletesFromABinInTheCardsCornerAndStandsFive` (no DELETE button, the bin's place and action, five
anchors in screen order, the silhouette material). Picture: `CharacterSelectCapture` (`Logs/CityShots/select-*.png`, the menu on its own in
`select-*-menu.png`).

## Tabs: DRAW, HAIR, FACE (2026-11-05)

The first tab was called PAINT until 2026-11-10 (owner: "make the paint tab called "DRAW" instead"); the code still calls it `Paint`. The
title screen's creator has no heading over the tabs since that day (owner: "get rid of the text that says create above paint hair and face");
ReDraw keeps its REDRAW.

Owner: "try to improve upon the UI of the character creation, I think later im going to have a bunch of premade mouths, eyes, eyebrows, etc., for people who dont want to draw. so improve upon that menu start planning for that, make a hair tab on the menus too and make it so it generate an icon of a plain white playermodel wearing the hairstyle instead of selecting a button with the name of the hairstyle".

- **Three tabs** under the heading of the left column, now 232 units wide (`CreatorTabs`, `ButtonAction.Kind.CreatorTab`). One page shows at a time. A page is hidden by its CanvasGroup, never switched off, so every button on it stays wired. The action bar (ENTER THE CITY / BACK, ReDraw's SAVE / CANCEL) stays under every tab. Both creators get the same tabs, as both are built from `CreatorKit`.
- **PAINT** holds the paint tools as before, 38 units lower (`CreatorKit.PaintPageDrop`).
- **HAIR** shows each hairstyle as a picture of a plain white head wearing it, four to a row in a grid that scrolls past four rows (`HairPage`, `CreatorPageKit`). The worn style's tile is framed in gold (`HairStyleTiles`). The hair colour (square, swatches, hue, preview) sits under the styles and hides while bald. The right-hand hair cards are gone (`HairColourPanel` and `HairButtonRow` deleted).
- **The pictures** are baked by CityX/Bake Hair Icons (`HairIconBaker`, batch: `CityX.UIEditor.HairIconBaker.RebuildAndBake`) into `UI/Resources/HairIcons/{style}.png`. It uses the creator's own camera, lights and cel lines, one framing for every style fitted to the largest, a 3/4 view from the key light's side, brown hair and 128 px. A new hairstyle needs a rebake and a title rebuild; until then its tile shows its short name.
- **FACE** is the page for the premade parts to come (`FacePage`, `FacePartChips`): chips for EYES, BROWS, MOUTH, NOSE and EXTRAS, each with a grid of parts. None exist yet, so each grid says so, and a note points to the PAINT tab. Parts are data in `FacePartCatalog` (`Character/Resources/FacePartCatalog.asset` when made). Adding one shows it in ReDraw at once, and in the title screen's creator after a title rebuild.
- **Paint only on PAINT** (`CharacterCreator.PaintEnabled`): on HAIR and FACE a drag on the body turns it and no brush ring shows, so looking at a hairstyle from every side never leaves a stray dot.
- **The wheel over the panel scrolls it** and no longer zooms the body.

### Premade face parts: the plan

The full plan, with what the code does now and what is proposed, is `docs/FACE_PARTS_PLAN.md`. In short: a part is a front-view picture of the whole head, saved on the character as an id, a colour and a small up, size and spacing. It is drawn as a layer over the paint when the head's texture is uploaded, so the paint stays separate and a part can be swapped. Other players build it from the ids, under 200 bytes a face. Before it can work: the server must keep the face when it saves a character, a ReDraw must reach other players (today none does), and the four places that dress a worn body's paint should share one helper.

Tests: `CreatorTabsPlayTests` (each tab its own page, painting only on PAINT, the ring, the wheel over the grid), `CharacterCreatorPlayTests` (each hair tile its own style, on screen and uncovered, the gold frame, the colour card, the swatches), `RedrawRealInputPlayTests` (ReDraw's tabs and tiles by mouse), `TitleScreenWiringTests` (tabs and pages wired, each tool on its own page), `HairIconTests`. Pictures: `TitleScreenCapture.RebuildAndCaptureSelect` (`creator.png`, `creator-hair.png`, `creator-face.png`).

## Made on a conveyor belt, alive (2026-11-10)

The title screen's creator now stands on a conveyor belt in the sky, and making a character starts on the empty belt and ends with the
body carried off it: docs/BELT_INTRO.md. Two things changed in the creator itself. The body keeps its whole ragdoll (it is the one
that falls), and it can be **alive** (idling) or **holding still** (`CreatorLife`): the brush is built on the pose it
holds still in, and alive the ray finds it as it stands and carries the hit back to that pose (`CharacterCreator.LivePaint`, docs/BELT_INTRO.md).
Only the T-pose button or T lifts its arms. ReDraw is as it was: holding still, no belt.
