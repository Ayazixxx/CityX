# Building facades: one whole style per building

Until 2026-09-26 every building in the city glazed each cell independently at 42%, so every building wore the
same speckle. Now a building is given ONE facade style, chosen from where it stands and how big it is
(`World/WindowGrid.cs` `PlanFor`, `World/WindowPlan.cs`).

## What the research said

- **Procedural building generators pick a facade as a whole.** Split grammars (Wonka et al., *Instant
  Architecture*; Müller et al., CityEngine's CGA) split a wall into a ground floor plus repeating floor tiles and
  then choose the tile; variety comes from WHICH tile a building gets and from the ground floor differing from
  the storeys above, not from re-rolling every window.
- **Real facades come in a few families** (curtain wall = office towers, floor-to-ceiling glass; window wall =
  residential, shorter spans; punched windows = separate openings in a solid wall; ribbon/strip windows =
  punched windows joined into horizontal bands; storefront = ground-floor retail glass).
- **Window-to-wall ratio differs hugely by type:** small offices average ~19%, large offices ~54%, glass
  residential towers in Chicago run 75%+, while sheds, garages and party walls have almost none.
- **Shamus Young's procedural city:** convincing shapes matter more than window detail, and one texture for
  every building cannot suit both a glass slab and a stone-trimmed classic.

## The six styles (`FacadeStyle`)

| Style | What it is | Glass |
|---|---|---|
| Curtain | floor-to-ceiling glass on every cell of every wall, thin mullion grid, **double-width units** | 100% of cells |
| Ribbon | alternating glass bands and solid spandrel, double-width units | every other row |
| Punched | regular separate windows, shorter than the row (sill 28%, head 14%) | every 1st or 2nd column |
| Scattered | irregular openings, each building its own density (20-40%) | the old speckle |
| HighBand | one high strip of windows, blank below (sheds) | one row |
| Blank | almost nothing | ~3% |

Weights come from the building's massing (tower/supertall = mostly glass, warehouse = high band or blank, slab
leans irregular) multiplied by its zone (downtown = glass, old town = small punched windows, industrial =
blank). See `StyleWeightsByMass` and `StyleBiasFor` -- both are plain tables to retune.

## On top of the style

- **Party walls:** in the dense zones a side wall can be entirely blank (a building stood against it). Never
  the door wall, never on a glass face or a shed.
- **Storefront:** the door wall's ground floor is glazed shopfront glass in commercial zones, and always for the
  supermarket and the bank.
- **Lit at night:** a curtain wall or ribbon is lit floor by floor (whole storeys on or off, all the way round
  the building) -- random speckle over a fully glazed face reads as noise. Other styles keep per-window lights.
- **Special buildings:** supermarket = blank + shopfront, hospital = ribbon, bank/police = punched.

## Relief: base, shaft, top (`World/FacadeOrnament.cs`, `View/CityMeshBuilder.Ornament.cs`, 2026-10-09)

From the Kelly & McCabe survey of procedural cities the owner sent: the one system it calls realistic is Wonka et al.'s split grammars (Instant
Architecture, 2003) -- a facade divided into a base, a shaft and a top, floors into bays, with vertical details at the building's edges. Our walls had
the window grid and nothing proud of it, so they read flat. Every building now rolls (from its seed, by family) a set of boxes in the wall buffer:

- **Base course** over the street storey; **string courses** every 1-2 floor lines; **cornice** capping the parapet, its top flush with the coping so a climber
  can pull up over it (`docs/CLIMBING.md`), up to 0.85 m deep on masonry (a
  crown band above the roof on glass) with a smaller step under it; **piers** on every 2nd-4th column line; **corner piers**; **lintels** over windows.
- Masonry (punched) gets the full classical set, glass only a canopy and a crown, sheds pilasters. Everything sits on row/column lines, in the solid
  wall between panes, so nothing crosses glass.
- Never on a stretch another block or a neighbour stands against (`BuildingSite.Contacts`), and a band does not round a corner into one.
- Towers taller than 10 fitted floors no longer switch to flat strip glass above 64 m (`CityMeshBuilder.FullDetailRows` = 50): that switch was a
  visible seam halfway up every tall tower.
- Tests: `StreetWallTests.TheReliefStaysOnItsOwnWall`, `MostOfTheCityWearsSomeRelief`.

## Frame-rate guard

Every pane is geometry, a collider triangle and a transparent draw, so the plan is budgeted:
`FacadeVarietyTests.TheCityHangsNoMorePanesThanTheOldSpeckleDid` fails if the city ever hangs more panes than the
old scatter did (curtain and ribbon use double-width columns for exactly this reason), and a row with no window
in it is one wall panel instead of three.

## Where it is used (all read the same plan)

`CityMeshBuilder` (`AddWindowedWall`, `AddDoorRow`, `AddPane`), `GlassDamage.TryPaneAt` / `FractionWithinPane`
(shooting finds the pane the mesh actually hung), `TitleSkylineBaker`. Never call the old per-cell hash -- it no
longer exists; build a `WindowPlan` with `WindowPlan.For(world, site)` and ask it.

## Update: a tower on a podium is an attachment, not a second building

`BuildingSite.OnRoof` marks every volume that stands on another volume's roof (a tower on a podium, a stage of a stepped tower). Such a volume gets no shopfront (`WindowPlan.For`
drops the storefront), no awning/canopy/sign board/blade signs, and its ground row uses the ordinary wall look instead of the heavier street-level base, so it reads as the
building continuing upward. Its door is a plain door in the wall and is dark grey (`CityStreamer.UpperDoorMaterial`) so it differs from the brown street entrances.
Tests: `StackedVolumeTests`; guard: `BuildingOverlapTests` (no two buildings share space).
