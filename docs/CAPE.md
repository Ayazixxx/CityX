# The cape (2026-11-24)

Owner, 2026-11-24: "i want you to make a basic cape that we can make a bunch of different colorways of easily, make it tied around the front. and
research how to make good cape physics in video games."

Then, 2026-11-25, drawing the cape over two pictures of the body (a red band over the tops of both shoulders from in front; the back as it
was): "get rid of the yellow string and make it like my drawing, ive given two angles to how it should be", "model it based on my design".

## How it plays

- `/spawn cape` (or `redcape`, `blackcape`, `/equip ...`). Worn on the **back** (the slot the jetpack, the wings and the parachute use, so one
  of them at a time). Lying on the ground it is a folded cape, picked up with E.
- Fitted as a LEGO minifigure's cloth cape is (owner, 2026-11-25, with a picture of one: "make it fit something like this. no need to make
  the back wider just the shoulders neck part"; before that: "why is there a brighter red line? make it all the same color"):
  - a **yoke** over the whole top of each shoulder, from over its front down a little at the tip to behind it, round behind the neck under
    the head (`CapeShape.FrontEdge`, `BackEdge`, rising over the shoulder's round top), held to the body as a LEGO cape's top is;
  - a **collar** round the front of the neck under the chin, lying back on the top of the chest (it faces up as the yoke does), held too;
  - the **cloth** falling from the yoke's back edge behind the back and beside the arms to the calves, widening to the hem: the back as it
    was. Only this moves.
  - All one colour, inside and out; no cords, no rolled hem (the hem caught the light as a brighter line).
- **The front edge as the owner drew it** (2026-11-25, a black line over a picture: "follow the black line i drew and fill in the missing
  material as well as trim those triangles poking out on the sides. the middle stays as it"): the yoke's front edge runs from the collar's
  lower edge out along the front of each shoulder to its tip, with no skin between the collar and the yoke (the collar's ends tucked under
  the yoke), and the tips round back over the shoulder instead of folding down into points. The collar in the middle as it was.
- **The owner's black line** (2026-11-25, drawn over a picture: "make it a straight line across", "trim it as needed and fill material as
  needed match the black line"): seen from in front, the lower edge runs straight from each shoulder's tip down to the collar's lower edge in
  the middle (`CapeShape.FrontLineAt`: 7 cm higher at the tips), cloth above it; the collar's lower corners reach on out along it under the
  yoke, so no notch shows where they meet. Beside the neck the head sits almost on the shoulder, so the yoke routes round the neck where there is
  room and tucks a little under the head there (hidden by it); the collar lies a little nearer the body than the yoke where its ends tuck
  under it. 21 columns across, so the turn round the neck does not fold the cloth.
- **Fitted like a skin suit** (owner, 2026-11-25: "make it fit to his shoulders like a skin suit. like the same dimensions of the shoulder
  just a tiny bit bigger so its not clipping through"): when it is put on, every point of the yoke and the collar is laid 2.2 cm off the
  body's own surface under it (`CapeShape.FitToBody`, from the body mesh; the yoke smoothed so it lies as cloth, kept clear of the head), and
  follows the bones and weights of the body's skin there each frame, so the shoulders move with the arms and the yoke with them. One fit a
  body mesh, shared by every body wearing one. A body whose skin cannot be read wears it as made, on the Spine bone.
- The edges were first laid out from measures of the body: the shoulders' top is flat at 1.93 to 1.94 m out to 0.40 m, about 10 cm either side of its middle,
  and falls away at 0.43 m; the back of the neck is 0.14 m behind at 1.90 m; the head's ball comes down to 1.90 m. Everything sits 1.5 to
  5 cm off the body and clear of the head.
- Shading: the head is a big ball right over the shoulders, so with the sun high the yoke and collar are often in its shadow and read darker
  than the sunlit back, as the body under them does. That is the light, not the cloth's colour (`CapeTests.ItsOutsideFacesOut` checks the
  faces point the right way).

## Colourways

`Items/CapeColourways.All`: one line each, the cape's one colour. Red and Black to start. A new one is a new line; the scene builder makes its prefab and materials
(`Wearables/Editor/CapeBuilder`), and the item list, the console and every body's item prefabs read the same list.

## Cape physics: what games do (research, 2026-11-24)

| Source | What it says | What we took |
|---|---|---|
| Jakobsen's character physics, Müller et al. 2007, *Position Based Dynamics* | cloth as points joined by length constraints, moved by Verlet steps and corrected directly in position: stable at game frame rates | the solver: Verlet steps, links put right 6 times a step |
| Kim, Chentanez and Müller (NVIDIA), 2012, *Long Range Attachments* (made for a cape) | game cloth stretches unless it has many iterations; tie each point to the animated attachment (the shoulder) with a one-sided distance limit and it cannot stretch | every point tethered to the top of its column, no further than the cloth between them (2% give) |
| Unreal's Chaos Cloth (velocity scale, fictitious angular scale, teleport) | let only part of the character's movement into the cloth's world, and treat a teleport as a reset | part of the body's move each frame carried straight into the cloth (30%); moved over 2.5 m in a frame: back to rest |
| Batman: Arkham City (animation article) | the cape is a hybrid: simulation driven from an authored pose, keyframes for big moves, wind volumes | a pull back towards the cape's shape on the body, firm over the shoulders, faint at the hem |
| Unity Cloth / Magica Cloth (forums, docs) | body colliders as capsules and spheres on the bones; tuning is trial and error | capsules on the trunk, legs and arms and a ball for the head, the cloth kept 3.5 cm off them |
| Frostbite (Lewin, GDC 2018) | self-collision is one of the hardest parts | not done: a basic cape rarely folds into itself |

Plus air: the cloth's face pushed by its own speed through the air (and the breeze), which is what makes a runner's cape stream out and rise.

## How it is built

| Piece | What |
|---|---|
| `Items/CapeColourways` | the colourways |
| `Wearables/Capes/CapeShape` | the cape at rest on the body (a 15 x 20 grid: 5 rows of yoke, 16 of falling cloth; the collar) and the cloth made from it: links across, down, diagonal and two apart (folding), tethers to the yoke's back edge, what is held (the yoke and the collar), the pull to shape |
| `Wearables/Capes/CapeCloth` | the cloth: fixed 90 steps a second, Verlet, air push, links, tethers, capsules, floor. Pure |
| `Wearables/Capes/Cape` | on the body: follows the Spine bone after the animation and the posing (through the body skin's bind pose), the body's capsules, inertia, teleport, steps; worn on another player's copy too |
| `Wearables/Capes/CapeLook` | drawn: the yoke, cloth and collar as one sheet with two faces, in one material |
| `Wearables/Editor/CapeBuilder` | a prefab a colourway: its material and the folded cape |

Numbers (mine, in `CapeShape` and `CapeCloth`): 463 points (the yoke and collar held), 1,936 links, 8 corrections a step, keeps 98.5% of its speed a step, air push
2.2, pull to shape 0.06 at the shoulders, 0.002 where it starts to hang, 0.0004 at the hem.

## Over clothes (2026-11-25)

Owner: "make cape fit over shirt without clipping"; then, on a picture from behind, "the shoulders on the cape still clip through and i saw
some of the lower back as well".

- The yoke and the collar are fitted to the bare body as before, then lifted over what is worn (`CapeShape.FitOver`): under each held
  point, the clothes within 6 cm along the body and up to 20 cm off it are looked at, and the point is raised to 1.2 cm outside the highest.
  A lift spreads to its neighbours (never lowered), so the cloth does not crease where a seam or a collar stands out. The clothes are the
  body's combined worn mesh (`SkinnedGarmentCombiner.CombinedMesh`), which is at rest in the body's own space.
- The falling cloth is kept further out from the body by as much as the clothes stand off the back between the hips and the shoulder
  blades (`CapeShape.StandsOff`, added to the torso capsule): it went into a shirt at the small of the back.
- When the clothes change while the cape is worn (a new combined mesh), it is fitted again and its cloth starts again at rest.

## Cost and level of detail (2026-11-25)

Owner: "the cape physics look really good but would that be a risk for performance in an mmo. it doesnt need to be THAT good".

- Measured (`CapeCostBench`, Explicit, in the editor, which runs scripts slower than a built game): one cape at full detail cost 7.5 ms a
  frame at 60 fps. The cloth was made cheaper with the same behaviour: links and tethers as plain arrays in plain numbers, and the body
  pushing the cloth out twice a step (halfway through and at the end) instead of after every one of the 8 corrections. Now 2.4 ms a frame
  at full detail, 1.3 ms at the lighter one, in the editor. Not measured in a built game.
- `CapeDetail`: within 20 m of the camera the cloth runs in full; from 20 to 45 m with 3 corrections a step (the tethers still keep it
  from stretching); further, or off the screen, not at all: the cape is set at rest on the body once and goes where the body goes. With
  no game camera (tests, pictures) it always runs in full.

## Not done

- No self-collision, and the cape does not collide with what is on the back (a giant boomerang carried there) or long hair.
- No wind from the weather (a fixed light breeze for now).

## Tests

`CapeTests` (the shape at rest: the yoke from over the front of each shoulder to behind it and round behind the neck, the collar round the front of the neck meeting it, nothing in the head, every point outside the body's own surface, hem at the calves; the outside faces out; the cloth settles hanging without stretching, the body pushes it out, running it streams out behind and lifts; a capsule
pushes a point to its surface; every colourway an item on the back with its prefab and colours), `CapePlayTests` (on the real body: the top
edge on the shoulders standing, hanging behind, nothing loose deep inside the body; sprinting it streams back and lifts; moved somewhere new
it comes along at rest; another player's copy wears it, moving), `CapeCapture` (standing from four sides, the shoulders close from in front (the sun behind and in front) and from above behind, the folded one, walking,
sprinting, turning, stopping).
