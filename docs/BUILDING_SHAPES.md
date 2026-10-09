# Building shapes

Asked 2026-10-08, after seeing a procedural "gigacity" and Unity's Megacity: "all sorts of different buildings shapes and sizes in my city some normal some wonky some round some square some L shaped you named it". No flying cars.

## What the references taught

- **Silhouette beats surface detail.** The cities that read as real from the air have towers on podiums, setbacks, blocks hanging over the street, and stacks of mismatched blocks, not just taller boxes. (Same lesson as `docs/FACADES.md`, Shamus Young's procedural city.)
- **Megacity / Megacity Metro** (Unity, Unity Companion License, URP + ECS, multi-gigabyte Git LFS repo) is hand-made modular sci-fi art: building = stacked modules plus thousands of props (signs, AC units, pipes, lights). Its assets are licensed for Unity projects only and are a different art style, so nothing was imported; the lesson taken is "many shapes and a dense, busy skyline".
- **Gigacity** (Ryan Sael, three.js/WebGPU, "100% code by Opus 5.5"): all procedural, no models or textures; tall stacked towers with overhanging blocks, heavy aerial haze, thousands of lit windows. The author states its code and look are his own; nothing was copied, only general techniques noted (haze, weathering, lit rooms) for a later look pass.

## How a shape is made (`World/Plan/Massing.cs` `Compose`)

Every building is still made of boxes, because the whole building generator (shootable windows, doors, stairs, rooms) works on a box. A lot's rectangle (width along the street, depth back) becomes one of:

| Form | What it is | Who gets it |
|---|---|---|
| Box | one box | most buildings |
| Stepped | tower on a podium and/or setbacks (the older `Split`) | towers, tall mid-rise |
| L / T / U | a bar on the street plus one or two **wings** going back, turned so their doors face the yard | low and mid-rise, ~half of them |
| Staggered | two blocks side by side, one set back and a different height | low and mid-rise |
| Twisted | a base you can enter, then 3-16 **sealed** stages each turned 4-9 degrees further, shrunk to stay in the lot | towers, tall mid-rise |
| Wonky | a base, then a pile of sealed blocks each turned up to 12 degrees and pushed off centre | same |
| Cantilever | a narrow shaft, and one big sealed block across it or slid off it, hanging out over the yard | same |
| Round | a 6, 8, 12, 16 or 20-sided tower you can enter, stood toward the street; half get one or two narrower sealed round crowns | same |

Rules, all tested in `BuildingShapeTests`:
- Every volume stays inside the lot rectangle (so nothing reaches a neighbour); every volume above the ground has its centre over the one it stands on.
- Blocks at street level touch but never overlap; they share one floor height so a wing meets the bar at the same level.
- **Contacts** (`BuildingSite.Contacts`, `PlanBuildings.ContactsBetween`): where one block stands against another, that stretch of wall, up to the other block's roof, has no windows (`WindowPlan.WithContacts`), balconies, fins or skyline lights.
- A wing's door never faces another block; it opens onto open ground (its yard, or the street for a staggered pair). Wings have no shopfront.
- **Sealed blocks** (`BuildingSite.Shell`): the turned, slid or overhanging blocks of sculpted towers are built as their outside only at every distance (no door, rooms, stairs or breakable panes), with a drawn underside. The base under them is a normal building you can enter.
- Deterministic: the turn angles come from literal tangent tables (no sin/cos), like the rest of the plan.

- A roof with a sealed block on it (`BuildingSite.RoofSealed`) has no stair up to it (`BuildingStairs.Layout.RoofCovered`): there would be nowhere to come out.

## Round towers (`World/RoundFootprint.cs`, `View/CityMeshBuilder.Round.cs`)

`BuildingSite.Sides` > 0: the outline is a regular polygon whose corners lie on the circle `Width` across (`Width == Depth`), side 0 facing the front with the door.
- **Every side is a short flat wall built by the ordinary wall code.** The buffers' frame is turned to each side (building turn, then side turn, out at the
  apothem), so `AddWindowedWall`, `AddDoorRow`, glass, frames, shopfronts and cracked panes work unchanged. A side's windows are window "wall"
  `WindowGrid.PolygonSide(i)` (side 0 = North, side i = 8 + i), so panes are keyed and shot like any other (`GlassDamage.TryRoundPaneAt`, `RoundAlong`).
- Floors: the middle square (`InnerHalf` = 0.70 x apothem, fits 6-20 sides) with the stair openings, plus a ring of quads cut along rays through every
  corner of square and polygon. Open plan (no partitions). Stairs are planned in the middle square. No basements.
- `cos(pi/N)` comes from literals (`BuildingSite.CosPiOver`) so the plan stays trig-free; drawing and hit-testing use Math.Cos/Sin.
- Approximations left on purpose: `BuildingAt`, the indoor trigger, rain cover and the map still treat a round building as its square (2R).

## Layout fix found by the new seed

`CityPlan.MarkEnclosing`: a block with another block (a stand-alone loop of road) inside its outline is left as open ground with its pavement, instead of one
huge plot laid over the island's buildings (seed 87650022, classic scale: a 432 m plot over six houses). `tools/cityplan -- overlaps [seed]` checks for
overlapping buildings and nested lots in seconds (run with `CITYX_CITY_SCALE=classic` to match the tests).

`BuildingShapeCapture` (Explicit, -Graphics) renders one of each near the spawn, in the city and alone ("studio" shots high above it), to
`Logs/CityShots/shape-*.png`.

## Next

- A filler for the open blocks (park, plaza) left by `MarkEnclosing`.
- Look pass learned from the references: height haze for depth, stronger weathering streaks, more lit rooms and signs at night.
