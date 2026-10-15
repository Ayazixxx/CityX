# Building interiors: research (2026-10-01)

Asked: research how to improve building interiors -- wide variety, never empty, a much wider range of staircases (stairwells, switchbacks,
stairs that wrap the inner walls), and fix the top floor's staircase being far too steep. Then, mid-research: **each building should be its
own procedurally generated mini dungeon** (the plan for later), and **use Fallout's buildings as inspiration**.

**Nothing here is implemented.** This is the evidence and a proposed design. The one new file in the code is a measuring tool,
`View/Editor/InteriorSurvey.cs` (menu "CityX/Survey Interiors"), so any stair rework can be judged by numbers.

---

## 1. What a building interior is today

All of it is `World/BuildingInterior.cs` (layout) and `View/CityMeshBuilder.BuildBuilding` (geometry). Ported from the Godot `city_chunk.gd`.

- One open room per floor, the full footprint. Per floor: a floor slab with one hole, **one straight flight of stairs**, and **one carved
  corner room** (40% x 42% of the plan, one doorway). That is the whole interior. No furniture, props, lights, corridors, or second room.
- The layout is a pure function of the **floor index**, not of the building: the stair corner is `floor % 4` and the room takes the other
  back corner. Every building of every kind is the same building repeated, and the lobby is laid out like any other floor.
- A **storey is two window rows = 6.4 m** (`StoreyHeightRows = 2`, `CellLayout.NominalFloorMetres = 3.2`). Real floors are about 3-4 m, so every floor
  is a double-height space.
- Stairs are a stack of solid boxes from the floor up, 0.25 m rise each. The step *depth* is just whatever is left: `run / count`, with
  `run = min(halfWidth, halfDepth) - 1` (`RunFor`). Nothing ever checks that a flight can be walked comfortably.
- `MaxFloors = 7`, and **the top floor swallows every metre the cap left over** (`FloorTop`), so its stair has to climb all of it.
- The player's controller (`slopeLimit 50`, `stepOffset 0.4`) climbs any of this as 0.25 m step-ups, so nothing ever flagged it.

### Measured, all 12,144 buildings of seed 61839025 (`InteriorSurvey`)

| | |
|---|---|
| Footprint short side | p10 9.6 m, median 14.9 m, p90 22.5 m (min 8) |
| Footprint | median 17.4 x 27.9 m; height median 37.8 m, p90 75 m, max 257 m |
| Floors | median 6, p90 7 -- **42% of buildings (5,051) hit the 7-floor cap** |
| Run the room allows | median 6.5 m, p10 3.8 m |
| **Every flight's angle** | **median 47 deg**; 85% steeper than 35 deg, 56% steeper than 45 deg, 17% steeper than 60 deg |
| Ordinary (non-top) flights | median 45 deg; 83% over 35 deg |
| **Top flight** | **median 61 deg, p90 82 deg**; 81% over 45 deg, 51% over 60 deg |
| Top flight rise | median 9.7 m, p90 35.9 m, max 218 m (874 steps) |
| Top flight by zone | Core median 77 deg, Urban 65, Residential 50, OldTown 49 |

So the report is right and understated: the top floor is a near-ladder (a 77 deg stair has a 6 cm tread), but **the ordinary flights are
steep too**. A 6.4 m rise at a comfortable 32 deg needs about 10 m of straight run; the median building allows 6.5 m. Two separate causes:

1. **Leftover height dumped on the top floor** (42% of buildings).
2. **One straight flight whose length is the room's size, not the climb's size.** There is no landing, no turn, no fold. A flight that does not fit
   simply gets steeper.

---

## 2. Stairs: what the real world and the games say

### Numbers worth designing to
- IBC (commercial): riser 4-7 in (0.10-0.18 m), tread at least 11 in (0.28 m), width at least 44 in (1.12 m), headroom at least 80 in (2.03 m),
  **at most 12 ft (3.66 m) of rise between floors or landings**, landing at least as wide as the stair. IRC (houses): riser up to 7.75 in, tread
  at least 10 in, 36 in wide, 12 ft 7 in. ([code summary](https://www.buildingcode.blog/blog/stairwaycoderequirements), [IBC reference](https://buildingcodetrainer.com/commercial-stair-codes-explained/))
- Comfort: Blondel's rule, 2 x riser + tread = 63-65 cm, which lands at **30-35 deg**. ([2R+T guide](https://www.stair-calculator.com/guides/stair-comfort-formula.html))
  The level-design metrics reference says the same: "modern stairs should follow a 30-35 degree slope", real-life 7 in x 11 in
  ([Level Design Book](https://book.leveldesignbook.com/process/blockout/metrics)).
- Spiral (IBC 1011.10): at least 26 in (0.66 m) clear width, riser up to 9.5 in, tread at least 7.5 in measured 12 in from the narrow edge
  ([IBC 1011.10](https://codes.iccsafe.org/s/IBC2018/chapter-10-means-of-egress/IBC2018-Ch10-Sec1011.10)).
- Scissor stairs (two interlocked flights in one shaft) count as **one** exit and cannot serve as two remote exits in a high-rise
  ([UpCodes](https://up.codes/d/scissor-stair)).
- Game controllers: Unity's manual suggests a step offset of 0.1-0.4 m for a 2 m character; set it below the riser and every step becomes a wall.

**The 3.66 m rule is the useful one.** Our storey is 6.4 m, so a real stair there must break into two flights with a landing. That is what
makes a switchback or an L the *natural* answer rather than a cosmetic option.

### Proposed dimensions (mine, derived from the above, not quoted from a source)
Riser 0.175 m, tread 0.29 m -> 31 deg, 2R+T = 0.64 m. For a 1.2 m wide flight:

| Rise to climb | Risers | Straight run | Footprint |
|---|---|---|---|
| 3.2 m (one row) | 18 | 4.9 m | straight 1.2 x 6.1 m (with a landing) |
| 6.4 m (our storey), U-stair, one mid landing | 2 x 18 | 4.9 m per flight | **2.55 x 6.1 m** |
| 6.4 m, straight with a landing | 36 | 10.2 m + 1.2 m landing | 1.2 x 11.4 m |
| 6.4 m, L-stair (quarter turn at the landing) | 2 x 18 | 4.9 m per flight | about 6.1 x 6.1 m, wrapping a corner |

A U-stair is 2.55 m wide, so it fits inside even the smallest footprint (short side 8 m), though it leaves little else on that floor; it is also the
standard fire-stair shape for exactly this reason (a lot of rise in a small square). The current width, `StairWidth = 1.8 m`, is fine for a game
(the metrics reference says a hallway should be at least twice the player's width) and can stay; widen the figures above accordingly.

### The stair kit (what "a wide variety" means concretely)

| Type | What it is | Where it belongs |
|---|---|---|
| **Straight** | One flight, optionally two with a landing | Walk-ups, rowhouses, shop-to-upstairs, along a party wall |
| **L / quarter-turn** | Two flights at 90 deg around a landing | Corner stair halls, mid-rise residential |
| **U / switchback** | Two parallel flights, half-turn landing | The workhorse: fire stairs, stair cores, anything with a 6 m storey |
| **Scissor** | Two U-stairs interleaved in one shaft | Big slabs and towers (one shaft, two routes) |
| **Open-well / wrap-around** | Flights run along the inside walls round a central void, landings at corners | Lobbies, libraries, department stores, hotels, atria: the "stairs that go around the inner walls" |
| **Winder** | Turns on pie-shaped treads | Cramped old houses (OldTown) |
| **Spiral / helical** | Round stair around a pole (spiral) or open well (helical) | Landmarks, small roof/loft access, "special" buildings |
| **Stair core** | The stair inside an enclosed concrete shaft, door on every landing | Towers: central core (about 85% of 500 buildings in one survey), side core, split core ([CTBUH](https://global.ctbuh.org/resources/papers/download/4186-offset-cores-trends-drivers-and-frequency-in-tall-buildings.pdf), [overview](https://cove.is/glossary/building-core)) |
| **Ladder / alternating tread / catwalk stair** | Steep on purpose, steel | Industrial, plant rooms, roof hatches: the only place a 55-70 deg stair belongs |
| **Collapsed / broken** | A stair or floor that is gone | Ruined buildings: Fallout's collapsed floors (see section 4) |

Stairs are also a *building* decision, not a floor one: a building should have a **main** stair (the one you see from the lobby), usually a **second remote**
stair at the other end (code wants two exits; it also makes a loop, see section 5), a **basement** stair, and a **roof access** (a small bulkhead stair or a
ladder), instead of one stair repeated up the same corner.

### Fixing the top floor, cheapest first
1. **Make the stair fit the climb.** Needed run = rise / tan(31 deg). If the room allows it, one straight flight; if not, fold it: landing + U, then L, then
   spiral as the last resort. Never let the angle exceed about 36 deg except on a ladder-type stair. This alone fixes the ordinary flights (85% of flights).
2. **Stop dumping the leftover on the top floor.** Spread it, or (better) have more floors: with one-row storeys the median building has 11 floors, p90 23.
3. **Give the roof its own access** (bulkhead stair or ladder) instead of the last flight climbing 200 m.
4. Replace the stack of boxes with an extruded stair profile (a sloped soffit plus risers/treads): about 4 triangles a step instead of 12, and it
   looks like a stair, not a heap. Keep it solid for the collider, which is right for a shared world.

---

## 3. Making interiors varied and not empty

### What the games do
- **Skyrim / Fallout 4 build interiors from kits, not unique art.** Skyrim: two kit artists, seven kits, 400+ interior cells
  ([PCGamesN](https://www.pcgamesn.com/fallout-4/bethesda-open-world-design), [Game Developer](https://www.gamedeveloper.com/design/skyrim-s-modular-approach-to-level-design)).
  Fallout 4 (GDC 2016, Burgess and Purkeypile, [slides](https://www.slideshare.net/slideshow/gdc-2016-modular-level-design-of-fallout-4/59770460)): standard
  256 and 512 unit footprints, "pick a pivot and stick with it", 14+ kit families consolidated by asking what problem each solves, and a unified
  **Building Kit across Retail, Institutional and Office** built from standard sub-kits (rooms, halls, stairs, railings), with wall/floor/ceiling split into
  separate pieces so they can be destroyed and varied.
- **Repeated *detail* is noticed far more than repeated *architecture*.** The fix is layers on the same bones: kit-bashing, dressing, lighting, unique accent
  pieces, asymmetry, and different occupants in the same room shape.
- **Clutter works in clusters that imply a use or a story**, not scattered evenly; clarity beats density. ([Level Design Book](https://book.leveldesignbook.com/process/env-art/storytelling))
- Academic floor-plan generators agree on a pipeline: a **program** (which rooms, what areas) -> a **layout** (BSP / squarified treemap / constrained growth /
  stochastic optimisation) -> a **connectivity graph** (doors between neighbours, a corridor where two rooms do not touch) -> **furnishing** in two passes, wall
  pieces first, then free-standing ([Marson & Musse, squarified treemaps](https://onlinelibrary.wiley.com/doi/10.1155/2010/624817),
  [Merrell et al., residential layouts](https://www.semanticscholar.org/paper/Computer-generated-residential-building-layouts-Merrell-Schkufza/84d5c57ea523bdb456902d9eb53666c25eb9eb3f),
  [Yu et al., Make It Home](https://web.cs.ucla.edu/~dt/papers/siggraph11/siggraph11.pdf)). Furniture is placed by a cost function (accessibility, visibility,
  against-wall, pair rules), not a random scatter.
- **Why few games enter every building:** designers say players tire by the tenth building, and every door being openable means a lot of rooms that serve no
  purpose ([discussion](https://www.resetera.com/threads/can-city-based-open-worlds-ever-solve-the-i-want-to-go-inside-every-building-issue.1516459/)).
  Cyberpunk deliberately hand-builds. That is the risk for this project, and it is the strongest argument for the mini-dungeon framing:
  **each building needs a reason to be entered** (a purpose, something to find, something to get past), not just rooms.

### What that means for us
Variety needs three independent axes, all driven by the building's seed:

1. **Archetype** (what the building *is*), chosen from zone, footprint, height and the special-building roll: tower, slab office, apartment block with a double-loaded corridor,
   walk-up with a stair hall, shop-and-flat, rowhouse, warehouse/factory floor with a mezzanine, hotel, civic hall with an open-well stair, parking structure, ...
2. **Programme and plan**: which spaces, how big, where the core goes, a corridor or not, where the doors are.
3. **State and dressing**: intact, neglected, ruined; then room-function dressing in clusters.

The existing facade grid (1.6 m columns, 3.2 m rows) is the right structural grid for it: partitions should land on column boundaries so a wall never
meets a window mid-pane, which is the same trick the facade already uses for slabs.

Footprints are small enough that this matters: 10% of buildings are under 9.6 m on the short side. Those get a two- or three-space archetype (a shop and a stair, a flat), not a
mini office floor.

---

## 4. Fallout's buildings as a model

Fallout's buildings are the right reference because they are **small dungeons with a programme, a gate, a vertical route and a reward**, built from a few kits. Case studies
from the sources above ([Mass Fusion](https://fallout.wiki/wiki/Mass_Fusion_Building), [Hubris Comics](https://fallout.wiki/wiki/Hubris_Comics_(Fallout_4)),
[Corvega](https://fallout.wiki/wiki/Corvega_Assembly_Plant), [Boston Public Library](https://fallout.wiki/wiki/Boston_Public_Library)):

| Building | What it teaches |
|---|---|
| **Hubris Comics** (retail + offices + studio) | Public front, private back: shop and storeroom on the ground floor, stairs at the back, a stair at *each end* to the executive floor, a studio wing with a ladder to the roof, loot in a locked display case behind the counter. A small building with a programme, two vertical routes and a roof ladder. |
| **Mass Fusion** (office tower) | **Ruin as the vertical connector**: bridges, "busted wall" and *collapsed roofs* are how you climb between floors; a keycard elevator for the shortcut; a basement reached by its own elevator; a hole in the roof as the last way up. Damage creates routes. |
| **Corvega Assembly Plant** (industrial) | Four zones round a central hub (admin, basement, manufacturing, assembly), reached by elevator *or* stairwell; a locked cage mid-way; the boss at the far end. |
| **Boston Public Library** (civic) | A locked front door that is also a trap, a courtyard with a ramp, an open stair up to a reading room, terminals on the balcony. Entry is an event. |

What to copy:
- **Each building is a short level**: an entrance, a middle, an end worth reaching (loot, a terminal, a view, a faction thing).
- **More than one way up**: a main stair, a back stair, a roof ladder, an elevator shaft, and in ruins a collapsed floor.
- **A gate and a key elsewhere** (locked door, keycard, a password on a terminal): a loop, not a corridor.
- **Damage as a variety engine**: collapsed floor panels, a hole in the roof, rubble, a blocked stair. One layout reads as many different places.
- **Programme-led dressing**: a comic shop has racks and a counter, a studio has a booth.
- **Kits with a fixed footprint and pivot**, and split wall/floor/ceiling so a ruin pass can remove pieces.

What not to copy: Fallout buildings are **separate interior cells behind a loading door**, so the inside need not match the outside. Here the interior sits inside the
visible footprint (you can see in, climb in through a window, shoot the glass), which is the game's identity and the right call for a shared world. See the open question below.

---

## 5. The mini-dungeon design

Dungeon generators have the same lessons ([Liapis, constructive methods](https://antoniosliapis.com/articles/pcgbook_dungeons.php)):
BSP guarantees non-overlapping rooms and gives gated clusters; templates give predictable pacing; **loops and vertical connections do not appear by themselves**, they need
explicit rules. Two directly useful techniques:

- **Cyclic dungeon generation** ([Dormans / Unexplored](https://www.gamedeveloper.com/design/unexplored-s-secret-cyclic-dungeon-generation-)): generate an abstract graph by rewriting
  *cycles* (lock-and-key, shortcut, secret), nest cycles inside cycles, then turn the graph into space. No dead-end branching feel.
- **Accretion + add loops** (Brogue, [Nystrom's rooms-and-mazes](https://journal.stuffwithstuff.com/2014/12/21/rooms-and-mazes/)): grow a connected tree of rooms, then deliberately
  re-open some doors between neighbours so there are loops.

### Proposed pipeline (pure functions of `site.Seed`, same shape as the city plan)
1. **Archetype** from zone / footprint / height / special roll.
2. **Programme**: a list of spaces with target areas and a public-to-private gradient.
3. **Floor plans**: place the **core** (stairs + shaft) first (central / side / split, per archetype), then BSP or treemap the rest, on the facade's 1.6 m grid, with corridors where needed.
   Towers get a **typical floor** repeated (real towers do; it is also cheap), plus a unique lobby, podium, and top.
4. **Connectivity graph**, including vertical edges: main stair, remote stair, roof access, basement, elevator shaft. Add loops on purpose (second stair at the far end, a service door).
5. **State pass**: intact / neglected / ruined by zone. Ruins delete pieces (a floor panel, a stair flight) and add rubble, but the graph is checked so the player is never stranded
   unless a way round exists.
6. **Gates**: locked doors and a key elsewhere, by building tier.
7. **Dressing**: room-function clusters (furniture, shelving, machines, bedding), wall pieces first then free-standing, by a cost function.
8. **Content markers**: loot slots, spawn slots, terminal and container anchors. Deterministic *positions* only; the actual loot and enemies come from the server and the live-state
   overlay (what is looted, which doors are open, which glass is broken), exactly how doors and glass already work.
9. **Geometry**: the existing `MeshBuffer` writers, in the building's own frame, turned into place by `SetFrame`. That part of the architecture already fits.

### Things the research turned up that will bite
- **Storey height is coupled to the facade.** `WindowGrid.TopEdgeHasSlab` (window insets), `WindowPlan.IsLit` (`row >> 1`, one decision per two-row storey) and
  `PlayerVitals` (fall-damage thresholds) all assume a two-row storey and the "top floor swallows the rest" rule. Moving to 1-row storeys for offices and flats (with 2-row for
  lobbies, retail, warehouses) means generalising those three places; they are not hard, but they are a hidden dependency.
- **Cost.** A median 38 m building at one-row storeys has 11 floors; a 257 m tower has 80. Only the lowest-detail shell exists beyond 8 chunks, but everything inside that radius is meshed into the chunk. The
  typical-floor repeat, a per-floor budget, and possibly generating an interior only when the player is near its building will be needed. Chunk build currently costs 1.2 ms on average, 17 ms worst; interiors are
  the next big contributor, and **should be measured before and after** (the `InteriorSurvey` and the bench hook are the tools).
- **Determinism for an MMO.** Interior layout must be integer-hash / double-arithmetic only, like the city plan, so every client builds the same rooms.
- **Navigation.** NPC pathing is street-level today; interior layouts should emit a navigable graph (rooms, doors, stair edges) as a free by-product.
- **Purpose.** If every building is enterable, a building that is just rooms will bore players by the tenth one. Archetype, gate, and a worthwhile end matter more than the room count.

### Open question for you
**Seamless or instanced?** Fallout puts each interior behind a loading door so it can be any size and any state per visit. This project has seamless interiors you can see into and climb into through windows. I
recommend staying seamless (the layout is deterministic from the building's seed, and all mutable state is a small shared overlay), and keeping an instanced "deep" level behind a basement or vault door as a later option
for a *big* dungeon. Say if you want it the other way.

---

## 6. Suggested order (when you say go)
1. **Stairs first**, because it is a bounded fix with a measurable goal: pick stair types by footprint and rise, enforce riser/tread/landing rules, fold when the run does not fit, stop the top-floor dump,
   add roof access. Target from `InteriorSurvey`: at least 90% of flights between 28 and 36 deg, none over 40 deg except ladder-type stairs, a spread of stair types.
2. **Floor-plan generator**: core, rooms, corridors, doors on the facade grid; archetypes.
3. **Room function and dressing**: the kit, clusters, wall pieces first.
4. **State, loops, gates**: ruin pass, remote stairs, locks and keys.
5. **Content markers** for loot and encounters, once the server side is ready.
Each step ships with tests and a before/after `InteriorSurvey` and chunk-cost number.

## Sources
Stairs: [code summary](https://www.buildingcode.blog/blog/stairwaycoderequirements), [IBC commercial](https://buildingcodetrainer.com/commercial-stair-codes-explained/),
[2R+T](https://www.stair-calculator.com/guides/stair-comfort-formula.html), [IBC 1011.10 spiral](https://codes.iccsafe.org/s/IBC2018/chapter-10-means-of-egress/IBC2018-Ch10-Sec1011.10),
[scissor stairs](https://up.codes/d/scissor-stair), [stair types](https://www.dimensions.com/collection/stair-types), [building cores](https://cove.is/glossary/building-core),
[offset cores (CTBUH)](https://global.ctbuh.org/resources/papers/download/4186-offset-cores-trends-drivers-and-frequency-in-tall-buildings.pdf).
Games: [Fallout 4 modular level design, GDC 2016](https://www.slideshare.net/slideshow/gdc-2016-modular-level-design-of-fallout-4/59770460),
[Skyrim modular level design](https://www.gamedeveloper.com/design/skyrim-s-modular-approach-to-level-design),
[Bethesda kitbashing](https://www.pcgamesn.com/fallout-4/bethesda-open-world-design), [Level Design Book: metrics](https://book.leveldesignbook.com/process/blockout/metrics),
[environment storytelling](https://book.leveldesignbook.com/process/env-art/storytelling).
Generation: [squarified treemaps](https://onlinelibrary.wiley.com/doi/10.1155/2010/624817), [Merrell 2010](https://www.semanticscholar.org/paper/Computer-generated-residential-building-layouts-Merrell-Schkufza/84d5c57ea523bdb456902d9eb53666c25eb9eb3f),
[Make It Home](https://web.cs.ucla.edu/~dt/papers/siggraph11/siggraph11.pdf), [cyclic dungeon generation](https://www.gamedeveloper.com/design/unexplored-s-secret-cyclic-dungeon-generation-),
[Nystrom](https://journal.stuffwithstuff.com/2014/12/21/rooms-and-mazes/), [Liapis](https://antoniosliapis.com/articles/pcgbook_dungeons.php).
Fallout buildings: [Mass Fusion](https://fallout.wiki/wiki/Mass_Fusion_Building), [Hubris Comics](https://fallout.wiki/wiki/Hubris_Comics_(Fallout_4)),
[Corvega](https://fallout.wiki/wiki/Corvega_Assembly_Plant), [Boston Public Library](https://fallout.wiki/wiki/Boston_Public_Library).

Not read in full (the fetch returned binary or was blocked): the Chalmers thesis on indoor generation, the Diva "hierarchical procedural decoration" thesis, and the Marson & Musse paper itself
(its abstract and summary were read). Treat their specifics as unverified. The proposed stair dimensions in section 2 are my own derivation from the cited rules.

---

## Implemented 2026-10-02: the stair kit (`World/BuildingStairs.cs`)

Asked: "there are still very steep staircases at the top floors of some buildings ... research stairwells, switchback stairs, staircases with
landings, L shaped stairs ... make a wide variety of them appear and eliminate the super steep ones." The research above (section 2) already covered
the codes (Blondel, the 3.66 m rule), Fallout's buildings and the kit; this is the build.

- **No steep stairs, by construction.** The tread is fixed (0.36 m, a 1.3x-real tread for a 2.6 m player) and the risers are derived from the
  climb (at most 0.235 m), so every flight is under 34 degrees (measured on 11,200 stairs: steepest 33.1; before: median 47, top floor median 61).
  A flight that does not fit the room is folded or another kind is tried; it is never made steeper.
- **Kinds:** straight, straight with a landing, L (quarter turn), switchback (2, 3 or 4 flights: U, Z, double-U), and a wrap round an open well.
  Each is built in its own frame and tried in the 8 orientations at the corners, mid-walls (and the centre for a wrap), starting from a point that
  varies per building.
- **Stair cores.** Tall and downtown buildings (65% of towers/slabs, 35% downtown, 8% others) get a walled-in U with a doorway, the same shaft on
  every floor, down to the basement. Everyone else's stair changes kind and place every few floors.
- **Headroom and openings.** A step within 3.1 m of the ceiling needs the opening above it; the opening is the bounding box of exactly those
  steps (a straight stair opens only over its upper half). Steps are stringers (the box hangs 0.55 m under its tread, so the underside is a stepped
  soffit) which lets the next floor's flight stand over the opening below. Entry and exit cells (1.2 m) are kept free at both ends.
- **The top floor no longer swallows the height.** `MaxFloors` is 10; a building that hit the cap and is a whole storey taller is sealed above
  its last fitted-out ceiling (no roof stair; you have the jetpack). Under the cap the top floor swallows less than a storey, as before.
- **Basement** uses the same planner (the old one was as steep as the top floor), and the carved corner room dodges the stairs
  (`BuildingStairs.RoomFor`).
- Not done: railings, stair doors with a latch that opens, spiral/helical stairs, the roof bulkhead, ladders for plant rooms, collapsed flights.

Tests: `BuildingStairsTests` (angle, every floor has a stair, in the room with end cells, opening where the ceiling is close, floors line up,
all kinds appear, core is one shaft, sealed tall buildings). `BuildingInteriorPlayTests.TheStairsCarryABodyUpAStorey` now walks the planned route
(any kind) with the real controller. `PlanCityCapture.CaptureStairs` renders one interior shot per kind (Logs/CityShots/stair-*.png).

### Fix 2026-10-02 (later): the head hits the ceiling in a stairwell

Reported: "my head is hitting the roof in this stairwell and it doesn't let me go up. make sure there's always enough room that a player can fit."

The first stair kit left 2.9 m between a step and the slab above. The player is 2.6 m tall with a half-metre capsule that steps up onto the next
tread (0.25 m higher, half a metre ahead), so on a landing under the slab the head had nowhere to go: a walking test with the real controller
stopped at 3.4 m of a 6.4 m climb. Now:
- a step needs 3.4 m (plus the slab's 0.2 m) of clear height or it gets the opening: `BuildingStairs.HeadClear` is 3.9 m below the ceiling;
- a stair that stands over another stair's opening (stringers hang 0.55 m under their treads) must leave `StackClear` 3.7 m above the one below,
  checked with the body's footprint rather than the tread's;
- `BuildingStairsTests.ABodyAlwaysFitsOnEveryStairWithRoomForTheNextStep` sweeps every stair in the city (a 0.5 m radius body on every step, with the
  highest neighbouring step under it, under the slab and the next floor's stair) and `BuildingInteriorPlayTests.EveryKindOfStairCarriesABodyUpAStorey`
  walks the real controller up one building of each kind (and three floors of a core). With the old numbers the second one fails; with these both pass.

### Fix 2026-10-15: every floor can be walked into from its stairs

Found through the balcony doors (a door that opened onto a floor with no way to the stairs). Three causes:

- **A wrapping flight boxed in its own landing.** It climbed on round the spot where the flight from below arrived, and with the opening below
  that spot was shut in: you climbed past the floor without being able to step off it. The planner (`CanReach`) checked only that a body
  could walk from where it arrives to where the next flight starts. Now, from where it arrives, a body must also reach at least half of the
  floor's free ground (`BuildingStairs.Flood`, on `WalkPath`'s own grid and body). Failing that for every placement, the middle of the
  floor (`HallPoint`); failing that, the old rule: a stair rather than none. The flight up to a top storey with no stair of its own is held
  to the same for that storey (`Context.LastFlight`), and so is every wrap for the floor it climbs to (it can shut its own arrival in
  before the next floor's stair is planned, `Context.FloorAbove`).
- **A corner room walled in a stair core's doorway.** Its inner wall could stand 47 cm in front of the core's door. The room now keeps clear of
  the core's door lobby (`Clashes`, `Lobby`), going to the other back corner or not being built.
- **A core across a narrow floor** left the far side unreachable (41 %). Cores are held to the same rule; a building whose core fails it
  gets ordinary stairs.

`BuildingStairsTests.WhereTheStairsArriveYouCanWalkOffIntoTheFloor` floods from every arrival (its own flood, not the planner's): 0 of 7102
floors shut in on seed 52817364 (0 of 8262 on 83920571), from 17 near the spawn before. The checks cost about 40 % more planning time (once per building, on the streaming
threads); the streaming cost tests still pass.
