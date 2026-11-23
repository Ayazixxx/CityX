# Beards (2026-11-22)

Owner: beards are 3D like the hairstyles, not drawn ("the drawn ones i have tried have came out bad"), "not super big so they clip into
clothing ... not much farther out then the head". Picked on the creator's FACE tab, BEARD chip. Worn in the hair's colour.

## What there is

| Name (saved) | Tile | State |
|---|---|---|
| `none` | NONE | clean-shaven, the default |
| `full_beard` | FULL | the full beard with CRISP lines along the cheek and under the jaw |
| `spiky_beard` | SPIKY | the same beard with a spiked cheek line (small, soft) and a spiked hem (bigger, like hair spikes) |
| `moustache` | STACHE | the older make (locks). **Paused** (owner: "pause on the circle beard and moustache. i may end up remove those completely"): untouched, still offered |
| `circle_beard` | CIRCLE | as the moustache |

A save stores the name (`CharacterSave.Beard`), so a name never changes once shipped; an old save is clean-shaven.

## The look the owner settled (full beards)

After locks ("cheap ... just adding a bunch of lumps to show texture") and strands ("i dont even think it needs to be strands"):
- ONE smooth skin, "everything blend together well ... no visible creases or lines" on it. What reads is its **outline**.
- "like hair spikes not roblox boxy": the spiky one's shape is all in its edge. The cheek spikes are the small, less sharp ones ("i think the
  top spikes looked better here less sharp spikes"); the hem's are bolder.
- **One black line round the edge**, as the hair has ("how can we get the black line on the edge of the beard like the hair above it"), and
  one only ("now there is two beard lines").
- At most 1.3 cm off the head, nothing below the head's lower curve: clear of the fur parka's collar (`BeardCapture`).

## How they are made

`CityX/SourceArt/Beards/beard_build.py`, headless Blender, all in code:

    blender --background --factory-startup --python beard_build.py -- [--only <name>] [--render <dir>] [--export Assets/CityX/Character/Beards]

then **CityX/Bake Beards** in Unity (`BeardAssets.Bake`, or `-executeMethod CityX.UIEditor.BeardAssets.Bake`), which writes
`Character/Resources/Beards/<name>.asset`. The head is a ball of 0.35 m with the face drawn on it, so a beard is a shape on the face's map
(degrees round from the front, degrees up) lifted off the ball; made in the head bone's own frame, it goes on with no fit.

A full beard (`build_shell`): the shape's edge is traced on a fine map with every corner kept, the skin is triangulated inside it and lifted
by its thickness (9 mm on the cheeks, 13 at the chin, thinner over the lip and at the spikes' points), and a skirt runs from the rim down
under the head's surface.

**The line.** `CityXOutline` inks where neighbouring pixels' normals are more than about 45 degrees apart. So:
- the skin's thickness changes slowly and it is shaded smooth: no line on it;
- the skirt is GIVEN normals lying flat along the head, a quarter turn from the beard on one side and the face on the other: a line all the
  way round, at any slope;
- the skin comes down to a LOW rim (`RIM_HEIGHT` 3 mm, over `RIM_BEVEL`), so the skirt is about 3 mm wide and tall: a pixel or two from
  any side. A tall skirt was inked along its top and its foot (two lines, worst from the side); a sliver of one was not found at all.

Spikes: `CHEEK_SPIKES`, `HEM_SPIKES` (from, to, depth, where the point is, in degrees), `CHEEK_POINTS_ROUNDED`.

## In the game

- `CharacterHair.Beard.cs`: `ValidBeards`, `SetBeard`, the mesh from Resources by name, a child `Beard` of the head bone with the hair's
  material and colour. Every body with hair can wear one with nothing wired.
- Creator: `FacePage` (BEARD chip, a tile a beard, `BeardTiles` frames the worn one), `ButtonAction.Kind.CreatorBeard`,
  `CharacterCreator.SetBeard`. The creator is baked into the title scene: a new beard needs `TitleScreenBuilder.Build`.
- Carried with the look everywhere the hairstyle is: `CharacterIdentity`, the belt's lineup, ReDraw, `CharacterService.CopyLook`.
- **Other players** see it: it travels with the appearance (`NetworkedPlayerIdentity`, checked by `AppearanceIsSane`).

## Adding one

A shape function and a line in `SHELLS` and `ORDER` (`beard_build.py`), export, a constant + `ValidBeards` + `BeardLabelFor`
(`CharacterHair.Beard.cs`), Bake Beards, `TitleScreenBuilder.Build`.

## Tests

`BeardTests` (every beard has its mesh and stays within 3 cm of the head and off the collar; a full beard is smooth skin plus a hard rim
and nothing else; put on, dyed, shaved; saved; the FACE tab's tiles), `RelayChecksTests` (the server passes only real beards on),
`NetworkedPlayerStateSyncPlayTests.HairstyleBeardAndColourShowOnAnotherPlayersCopy`. Pictures: `BeardCapture` (`beard-*.png`), and
`BeltIntroCapture`'s `face-beards` shot of the creator.

## Not done

- Tiles show names, not pictures (the hairstyles' are baked by `HairIconBaker`).
- A helmet does not hide a beard (`HeadWearable` covers hair only).
- The mouth's opening is where a mouth usually is drawn; a face drawn elsewhere will not line up.
- The moustache and the circle beard: paused, maybe to be removed.
