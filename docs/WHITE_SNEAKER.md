# The White Sneaker

A pair of chunky white sneakers, the first item in the shoes slot. The owner, 2026-10-25: "remove the grey leg from the shoe and lower face
count to a reasonable amount. then add it as a wearable shoe and try to fit is the best you can. Item name : White Sneaker". Spawn them with
`/spawn sneakers` (or `sneaker`, `white_sneaker`), put them on with `/equip sneakers` or by picking them up (E).

## Where they come from

- Meshy's "Urban Soles": two sneakers, each fused to a grey leg tube (960k triangles, a 4096 colour map).
- `SourceArt/WhiteSneaker/white_sneaker_build.py` (Blender, headless) makes the first cut:
  - It traces the legs off down through the grey faces, so the cut follows the collars.
  - It slices the tread flat and bakes its pattern onto the underside. The owner: the bottom "arent a priority".
  - It reduces one shoe to 7,000 triangles and mirrors it for the other, so the pair match.
  - It bakes the colour onto one 1024 map and stands each shoe on our body's foot.
- The owner then trimmed the inside by hand in Blender. That trim is kept as `WhiteSneaker_owner_trim_10k.glb` ("save and export this
  version incase i change my mind").
- Smoothed afterwards, live in Blender through the MCP:
  - The collar stitching and the front rubber, on both shoes.
  - The collar band's faces had tiny texture islands (about 5 texels each), so their colour broke into steps. They were moved onto one plain
    patch of the canvas colour.
  - A black dot in the toe rubber's texture was painted out.
- The approved version: `WhiteSneaker_smoothed_v2` (`.blend`, `.glb`, `.jpg`), 10,674 triangles for the pair. The game has its mesh as
  `Wearables/Models/WhiteSneaker.glb` (no materials) and its map as `Wearables/WhiteSneaker.jpg` (1024, 130 KB). The `.blend` files are not
  committed.

## How they are worn

Shoes are rigid, like a helmet. The body's foot is one bone, with nothing to bend a sole, so each shoe hangs on its foot bone instead of being
skinned like the jeans.

| Step | Where | What |
|---|---|---|
| Item | `ItemRegistry.WhiteSneaker` (`white_sneaker`, shoes slot) | a wearable, kept on the character and shown on the select screen |
| Prefab | `Wearables/Editor/WhiteSneakerBuilder.cs` (menu CityX > Rebuild Sneakers, and with the scene) | splits the glb by side (left foot on -x), bakes each shoe into its foot bone's frame as the body's skin is bound to it (`BodyRig.RestBindMatrix`) |
| Wearing | `Wearables/Shoes.cs` | hangs each shoe on its foot bone with no offset, the pair's root on the body |
| Body under them | `BodyUnderGarment.HiddenTriangles`, `WornBodyCover` | the feet always hidden, the ankles where the collars cover them; unioned with what other clothes hide; in shoes the jeans hide the ankles under their hems too (docs/BLACK_JEANS.md) |
| Pickup | `Wearables/GroundWearable.cs` | the E pickup, shared with the hats and helmets (`HeadWearable` derives from it too) |
| Other players | `ICosmeticMountable`, `NetworkedPlayerWiring` | a copy of the wearer puts them on its own feet the same way |

Lying on the ground, the pair stands side by side as worn.

**Size and place on the foot** (`WhiteSneakerBuilder.Size`, `Forward`, applied to the approved model when the prefab is built):
- 1.1 times the approved model (owner, 2026-10-26: "make both shoes 1.1x bigger"). Each shoe grows from the ground under its ankle, so the
  sole stays on the floor.
- Slid 2.7 cm forward. Owner: "slide it forward so the back of the heel it touching the inner wall of the shoe"; the tongue also clipped
  into the front of the ankle.
  - Standing, the shin leans forward over the foot, so the heel stood 3 cm behind the ankle.
  - At 3 cm the right ankle came out through its heel. The two legs stand a little differently.
  - The heel is a single surface, with no separate lining behind the ankle.

To refit them: open the approved `.blend`, move a shoe (the file is in the game's frame through glTF: Blender x, y, z is the game's -x, z,
-y; the body faces the game's +z, Blender's -y), export over `Wearables/Models/WhiteSneaker.glb` (selected only, glTF binary, no materials)
and rebuild the scene.

## BW Sneakers (2026-10-26)

Owner: "add these as a colorway for the shoes called "BW Sneakers"". Item `bw_sneakers`, `/spawn bwsneakers`.

- Meshy's black-and-white recolour of "Urban Soles": the same pair face for face, with its own UV layout.
- `SourceArt/WhiteSneaker/sneaker_colour_bake.py` (Blender, headless) bakes its colour onto the game's pair (`WhiteSneaker.glb`), keeping
  the owner's trim and the smoothing. Output: `bw_sneakers_color.jpg`, copied in as `Wearables/BWSneakers.jpg` (1024, 161 KB).
  - The legs come off by the white pair's grey. The recolour's own grey and black parts would read as leg.
  - The right shoe goes on our foot as the white one did and is mirrored for the left. These steps are shared with
    `white_sneaker_build.py` in `sneaker_meshy.py`; moving them there changed nothing in its output.
  - The smoothed collar band (3,057 faces, from 4 cm up to the collar) shares one texel of the layout. It takes the median of the
    recolour's faces nearest it, which is black.
- One pair of meshes for both colours. `WhiteSneakerBuilder.BuildBW` builds `BWSneakers.prefab` on them with its own map. Other players see
  them (`NetworkedPlayerWiring`).
- Known: where black meets white (the sole's top edge at the toe, the laces' edges) the line is a little ragged.

## Tests

- `WhiteSneakerTests` (EditMode):
  - the slot and name;
  - the budget and the map;
  - each shoe reaches past its foot's toes and heel, with the sole on the ground and the left on the left;
  - the feet hidden;
  - the copy's prefab;
  - the BW Sneakers: the same meshes, their own item and map.
- `WhiteSneakerPlayTests`:
  - each shoe on its own foot bone, the feet given back when taken off;
  - worn with jeans, taking the shoes off leaves the legs under the jeans hidden;
  - standing, the soles on the ground;
  - standing, the back of each ankle against its heel (no gap down its middle, nowhere out through the heel);
  - a copy wears them on its feet.
- `TitleScreenWiringTests.TheSelectScreenCanDressACharacterInEveryGarment` (the select screen needs rebuilding for each new garment).
- `WhiteSneakerCapture` (Explicit, `-Graphics`) writes `Logs/CityShots/sneaker-*.png`:
  - on the ground;
  - with bare legs (four sides, close);
  - in the Black Jeans, standing, walking and at three moments of a sprint;
  - `RenderTheBWSneakers`: the BW pair on the ground, then worn with the Khakis (`bwsneaker-*.png`).

## Known

- `tools/model_qa.py --kind shoes` warns, for the owner to judge:
  - 28 small holes, mostly on or inside the flat sole;
  - 6 spikes along the sole's bottom edge;
  - 32 floating bits, mostly inside;
  - z-fights near the tongues' tips.
- Under the cel shading the laces' small creases draw as black flecks, and the toe's shadow edge is ragged.
