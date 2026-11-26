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
    was. Only this swings.
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
  is skinned to the bones and weights of the body's skin there, so the shoulders move with the arms and the yoke with them. One fit a body
  mesh, shared by every body wearing one. A body whose skin cannot be read wears it as made, on the Spine bone.
- The edges were first laid out from measures of the body: the shoulders' top is flat at 1.93 to 1.94 m out to 0.40 m, about 10 cm either side of its middle,
  and falls away at 0.43 m; the back of the neck is 0.14 m behind at 1.90 m; the head's ball comes down to 1.90 m. Everything sits 1.5 to
  5 cm off the body and clear of the head.
- Shading: the head is a big ball right over the shoulders, so with the sun high the yoke and collar are often in its shadow and read darker
  than the sunlit back, as the body under them does. That is the light, not the cloth's colour (`CapeTests.ItsOutsideFacesOut` checks the
  faces point the right way).

## Colourways

`Items/CapeColourways.All`: one line each, the cape's one colour. Red and Black to start. A new one is a new line; the scene builder makes its prefab and materials
(`Wearables/Editor/CapeBuilder`), and the item list, the console and every body's item prefabs read the same list.

## Cape physics: what games do (research, 2026-11-24; the cloth built from it was replaced on 2026-11-25, below)

| Source | What it says | What we took |
|---|---|---|
| Jakobsen's character physics, Müller et al. 2007, *Position Based Dynamics* | cloth as points joined by length constraints, moved by Verlet steps and corrected directly in position: stable at game frame rates | the solver: Verlet steps, links put right 6 times a step |
| Kim, Chentanez and Müller (NVIDIA), 2012, *Long Range Attachments* (made for a cape) | game cloth stretches unless it has many iterations; tie each point to the animated attachment (the shoulder) with a one-sided distance limit and it cannot stretch | every point tethered to the top of its column, no further than the cloth between them (2% give) |
| Unreal's Chaos Cloth (velocity scale, fictitious angular scale, teleport) | let only part of the character's movement into the cloth's world, and treat a teleport as a reset | part of the body's move each frame carried straight into the cloth (30%); moved over 2.5 m in a frame: back to rest |
| Batman: Arkham City (animation article) | the cape is a hybrid: simulation driven from an authored pose, keyframes for big moves, wind volumes | a pull back towards the cape's shape on the body, firm over the shoulders, faint at the hem |
| Unity Cloth / Magica Cloth (forums, docs) | body colliders as capsules and spheres on the bones; tuning is trial and error | capsules on the trunk, legs and arms and a ball for the head, the cloth kept 3.5 cm off them |
| Frostbite (Lewin, GDC 2018) | self-collision is one of the hardest parts | not done: a basic cape rarely folds into itself |

Plus air: the cloth's face pushed by its own speed through the air (and the breeze), which is what makes a runner's cape stream out and rise.

## How it is built (since 2026-11-25: no cloth)

Owner, 2026-11-25: "work to get cape physics that are easier on performance. it really doesnt need to be anything crazy world of warcraft cape
physics are like nothing it just fits over the clothing and looks good. maybe the back flies up a bit when you jump or something. something
that will not tank performance in large crowds of people remember there is a hub".

The cloth simulation is gone. The cape is a **skinned mesh**, bent by the graphics card as the body and its clothes are:

- the yoke and the collar on the body's own bones (each point the bones and weights of the body's nearest point), so they follow the
  shoulders and arms exactly;
- the hanging part on a chain of 4 bones down its middle, hung from the Spine; its first rows follow the yoke's back edge as they leave it
  (fading to the chain over the first 15% of its length), so the two never come apart;
- the chain swung by two springs (`CapeSwing`): **lift** back and up, about 4 degrees for each metre a second forward (a walk about 16,
  a sprint about 30) and 6 for each metre a second up or down (a jump about 27, a long drop up to 70); **sway** to the side away from a turn
  or a sidestep, with a light breeze. 1.6 swings a second, a little overshoot. It never swings in towards the body (3 degrees at most).
  The lift and sway are shared equally down the chain, so the cape curves as it lifts.

Cost: 0.3 microseconds a frame for one cape on the processor (`CapeCostBench`, Explicit, in the editor), against 7.5 ms for the cloth it
replaced; the mesh (926 points, two faces each) bent on the graphics card, as a small piece of clothing is. A body moved over 2.5 m in a frame
gets its cape hanging straight again. It does not collide with anything: like most games' capes, a leg can pass through its hem now and then.

| Piece | What |
|---|---|
| `Items/CapeColourways` | the colourways |
| `Wearables/Capes/CapeShape` | the cape at rest on the body (a 21 x 21 grid: 5 rows of yoke, 16 hanging; the collar), fitted to the body and over the clothes. Pure |
| `Wearables/Capes/CapeSkin` | the mesh: its faces, each point's bones and weights, the chain's places and bind poses. Pure |
| `Wearables/Capes/CapeSwing` | the two springs. Pure |
| `Wearables/Capes/Cape` | on the body: fits it, makes the chain and the mesh, swings the chain after the animation and the posing; refits when the clothes change; worn on another player's copy too |
| `Wearables/Editor/CapeBuilder` | a prefab a colourway: its material and the folded cape |

## Over clothes (2026-11-25)

Owner: "make cape fit over shirt without clipping"; then, on a picture from behind, "the shoulders on the cape still clip through and i saw
some of the lower back as well".

- The yoke and the collar are fitted to the bare body as before, then lifted over what is worn (`CapeShape.FitOver`): under each held
  point, the clothes within 6 cm along the body and up to 20 cm off it are looked at, and the point is raised to 1.2 cm outside the highest.
  A lift spreads to its neighbours (never lowered), so the cloth does not crease where a seam or a collar stands out. The clothes are the
  body's combined worn mesh (`SkinnedGarmentCombiner.CombinedMesh`), which is at rest in the body's own space.
- The hanging part is kept outside the clothes too (`CapeShape.ClearHanging`): any point of it nearer the clothes' surface than 1.2 cm, or
  under it, is moved out to that, the move spread to the points round it. It went into a shirt at the small of the back.
- When the clothes change while the cape is worn (a new combined mesh), it is fitted again.

## Cost before (2026-11-25)

The cloth cost 7.5 ms a frame for one cape in the editor (`CapeCostBench` then); made cheaper (arrays, fewer collisions: 2.4 ms) and given
distance levels, it was still too much for a hub, so it was replaced by the skinned cape above.

## Not done

- It collides with nothing (the legs can pass through the hem now and then), nor with what is on the back or long hair.
- No wind from the weather (a fixed light breeze for now).

## Tests

`CapeTests` (the shape at rest: the yoke from over the front of each shoulder to behind it and round behind the neck, the collar round the
front of the neck meeting it, nothing in the head, every point outside the body's own surface, hem at the calves; fitted like a skin suit and
not folded; over a T-shirt and a long-sleeved one, the yoke, the collar and the hanging part outside it; the outside faces out; the swing:
straight standing, lifting walking and more sprinting, flying up in a jump and a drop, trailing a turn and a sidestep, never into the body,
steady at any frame; the skinning: four bones at most adding to one, the yoke on the body's bones, the hem on the chain, every point back on
itself at rest; every colourway an item on the back with its prefab and colours), `CapePlayTests` (on the real body, as drawn: the top edge
on the shoulders standing, hanging behind, nothing deep in the torso; sprinting it lifts out behind; jumping its back flies up; over a
T-shirt put on before or after it nothing of the shirt comes through; moved somewhere new it comes along hanging straight; another player's
copy wears it, moving), `CapeCapture` (over a T-shirt: standing from four sides, the shoulders close from in front and from above behind,
the lower back, the folded one, walking, sprinting, turning, stopping, jumping).
