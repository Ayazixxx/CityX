# The cape (2026-11-24)

Owner, 2026-11-24: "i want you to make a basic cape that we can make a bunch of different colorways of easily, make it tied around the front. and
research how to make good cape physics in video games."

Then, 2026-11-25, drawing the cape over two pictures of the body (a red band over the tops of both shoulders from in front; the back as it
was): "get rid of the yellow string and make it like my drawing, ive given two angles to how it should be", "model it based on my design".

## How it plays

- `/spawn cape` (or `redcape`, `blackcape`, `/equip ...`). Worn on the **back** (the slot the jetpack, the wings and the parachute use, so one
  of them at a time). Lying on the ground it is a folded cape, picked up with E.
- As the owner drew it: its top edge lies **on top of the shoulders**, from one shoulder's tip along the shoulder in to under the head,
  round behind the neck, and out to the other tip, finished in a rolled hem (`CapeShape.EdgeRadius`, 2.2 cm) that shows from in front as a
  band over each shoulder. No cords, no knot. From the top edge the cloth goes back over the shoulders in a quarter circle and falls behind
  to the calves, widening to the hem.
- The top edge was measured off the body (`CapeShape.TopEdge`): the shoulders' top is flat at 1.93 to 1.94 m out to 0.40 m and falls away
  at 0.43 m; the back of the neck is 0.14 m behind at 1.90 m; the head's ball comes down to 1.90 m. The edge sits 2 to 4 cm off all of it.
- Standing it hangs behind; walking it sways; sprinting it streams out behind and lifts; turning it swings round; a light breeze stirs it.
  Other players see it the same: their game runs the cloth from your body's moves (nothing extra is sent, only which cape you wear).

## Colourways

`Items/CapeColourways.All`: one line each, the outside (and the hem over the shoulders) and the lining (the inside, seen when it swings out).
Red (darker red lining) and Black (charcoal lining) to start. A new one is a new line; the scene builder makes its prefab and materials
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
| `Wearables/Capes/CapeShape` | the cape at rest on the body (a 15 x 18 grid, its top edge on the shoulders) and the cloth made from it: links across, down, diagonal and two apart (folding), tethers, what is held (the top edge), the pull to shape |
| `Wearables/Capes/CapeCloth` | the cloth: fixed 90 steps a second, Verlet, air push, links, tethers, capsules, floor. Pure |
| `Wearables/Capes/Cape` | on the body: follows the Spine bone after the animation and the posing (through the body skin's bind pose), the body's capsules, inertia, teleport, steps; worn on another player's copy too |
| `Wearables/Capes/CapeLook` | drawn: the sheet with an outside and a lining, and the rolled hem along the top edge |
| `Wearables/Editor/CapeBuilder` | a prefab a colourway: its two materials and the folded cape |

Numbers (mine, in `CapeShape` and `CapeCloth`): 270 points, about 1,400 links, 8 corrections a step, keeps 98.5% of its speed a step, air push
2.2, pull to shape 0.06 at the shoulders, 0.002 where it starts to hang, 0.0004 at the hem.

## Not done

- No self-collision, and the cape does not collide with what is on the back (a giant boomerang carried there) or long hair.
- No wind from the weather (a fixed light breeze for now).

## Tests

`CapeTests` (the shape at rest: the top edge on the shoulders' tips and over the shoulders where it shows from in front, behind the neck under
the head, clear of the head, every point outside the body's own surface, hem at the calves; the cloth settles hanging without stretching, the body pushes it out, running it streams out behind and lifts; a capsule
pushes a point to its surface; every colourway an item on the back with its prefab and colours), `CapePlayTests` (on the real body: the top
edge on the shoulders standing, hanging behind, nothing loose deep inside the body; sprinting it streams back and lifts; moved somewhere new
it comes along at rest; another player's copy wears it, moving), `CapeCapture` (standing from four sides, the shoulders close from in front and from above behind, the folded one, walking,
sprinting, turning, stopping).
