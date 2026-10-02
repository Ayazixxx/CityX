# Building exteriors: research (2026-10-01)

Asked: "I dont like how every building is just a line of windows, they dont look realistic at all ... research the outside of buildings as well in large city
games, something like Watch Dogs' city environment or Cyberpunk 2077. I want the outside of the buildings to look like a real city not a crappy roblox game."

**Nothing here is implemented.** This is a diagnosis of why the city reads as toy-like, what the big city games do about it, and a proposed order of work. The only code added is
a counting tool, `View/Editor/ExteriorSurvey.cs` (menu "CityX/Survey Exteriors"), next to the stair one (`InteriorSurvey.cs`, see `docs/BUILDING_INTERIORS.md`).
The pictures referred to are `CityX/Logs/CityShots/plan-aerial.png`, `plan-street.png`, `plan-building-front.png`, `plan-door.png`.

---

## 1. Why it looks like Roblox (what the code and the renders actually show)

| | What there is now | What a real city has |
|---|---|---|
| **Shape** | **Every building is one axis-aligned rectangle extruded straight up.** 12,144 buildings, zero L/T/U plans, zero setbacks, zero podium-and-tower, zero stepped or tapered crowns. In `plan-aerial.png` the skyline is a field of identical boxes with flat tops. `PlanBuildings.FitRectangle` throws away the lot's real shape and keeps only the biggest inscribed rectangle. | Stepped and tapered towers, podium bases, L and courtyard plans, chamfered corners, buildings that follow an odd lot and meet their neighbours at party walls. |
| **Facade** | A window grid at one uniform pitch over every wall: `Punched` 42%, `Scattered` 23%, `Ribbon` 17%, `Curtain` 16%, `Blank` 3%. A ground floor that is the same grid plus a door. Windows are flat dark panes set into the wall. | A base, a shaft and a crown that are different from each other; a rhythm of bays; piers and spandrels; cornices, sills, lintels, mullions with depth; shopfronts; balconies and fire escapes. |
| **Material** | **One shared tiling concrete for every wall and every roof** (`CityConcrete`, smoothness 0.04). No per-building colour: the mesh buffer has no colour channel, so a glass tower and a block of flats are the same grey. No brick, stucco, stone, tile, metal panel, no stains, streaks, dirt at the base, rust, posters. | A palette per building and district, several materials, wear and grime that follow the geometry (water streaks under sills, dirt at the base). |
| **Glass** | One flat dark transparent material (alpha 0.72). No sky reflection, no rooms behind it. | Reflective glass that mirrors the sky and neighbours, and a lit room behind each window at night. |
| **Roofs** | One prop per roof, 1-2 m across (a 1.1 m shed, a 2 m mast, a 1.8 m water tank) on a 20-60 m roof: invisible from anywhere. | Roofs full of plant: HVAC boxes, lift and stair bulkheads, water tanks, masts, billboards, pipes. |
| **Street level** | No shopfronts beyond glazing, no signs, awnings, vents, utility boxes, poles, wires, scaffolding. Street lamps were removed earlier on feedback and trees are deliberately postponed. Plain road and pavement. | The ground floor is where most of the detail and the life is. |
| **Variety** | **267 distinct looks across 12,144 buildings** (mass x facade style x roof x zone). 83% are `MidRise` or `LowRise`; 82% of facades are `Punched`, `Scattered` or `Ribbon`. | A wide spread of building types and ages mixed together on each street. |
| **Landmarks** | **No landmark buildings at all in the plan city.** The supermarket, hospital, bank and police station presets rolled in the old grid are not rolled any more (`BuildingParams.SpecialType` is empty for every plan building), a gap from the port. | A few unique, recognisable buildings you can navigate by. |

(Balconies are rolled for most buildings in `BuildingDecor`, but none show in the front, door or street shots; I did not chase whether they are drawn in the plan city.)

So "a line of windows" is exactly right: in practice every building is the same recipe (box + grid + one concrete) with the window density turned up or down.

---

## 2. What the big city games do

### Cyberpunk 2077
- **Night City was built almost entirely by hand**: "from every building and advert to every trash container and every piece of paper on the sidewalk". CD Projekt tested procedural tools and
  found them inadequate for the building work; the one procedural tool of note was a road spline system that placed roads, bridges and overpasses, and artists adjusted that by hand
  ([80.lv interview](https://80.lv/articles/interview-how-cyberpunk-2077-s-night-city-was-built-almost-entirely-by-hand)).
- They still built from **modular architecture sets** (exterior and interior), for example by Mateusz Walus, Krzysztof Olborski and Maciej Caputa
  ([Walus](https://mateuszwalus.artstation.com/projects/oAdBy4), [Olborski](https://krzysztofolborski.artstation.com/projects/WmWPDQ)).
- **Texture density was about twice The Witcher 3's**, with decals used heavily to keep detail up close ([same interview](https://80.lv/articles/interview-how-cyberpunk-2077-s-night-city-was-built-almost-entirely-by-hand)).
- **Districts have an identity**: four visual styles layered through history (80s-inspired entropy, 60s kitsch, corporate neo-militarism, pseudo-natural neo-kitsch), colour-coded by district, and a
  "rule of contrast" to avoid monotony. With Phantom Liberty the team added clearer guidance on grouping colours and shapes and leaving **negative space** so the eye can rest, because early areas were noisy.
- **Layers**: primary architecture, signage, holographic overlays, weather, crowds, vehicles, verticality; plus "visual waypoints" (a few huge neon signs or features visible from far away)
  ([overview](https://medium.com/beyond-the-game/cyberpunk-2077-night-city-open-world-41cf865243fa)).
- Artist breakdown: clean base textures; **blend 2-3 materials with masks and vertex paint**; weathering, damage and graffiti are applied last as decals and vertex paint, not baked into the base
  texture; emissive screens and signs do double duty as atmosphere and as composition ([production techniques](https://80.lv/articles/cyberpunk-environment-production-techniques)).

### Watch Dogs 2 and Legion
- Watch Dogs 2 represented **36 architectural styles** for San Francisco, compressed the geography ("best fit") rather than copying it, and kept the verticality because it suited the parkour
  ([Game Developer](https://www.gamedeveloper.com/design/using-san-francisco-as-the-framework-for-i-watch-dogs-2-i-)).
- Watch Dogs Legion (London) leaned on **modular kits**: two industrial kits of 250+ modules between them let artists assemble many different industrial buildings, with interior kits
  (walls, windows, doorways) for the inside ([Daniel Luka's kits](https://www.artstation.com/artwork/48kakl)). I could not find a source on a procedural building tool for either game, so none is claimed.

### The Division (Manhattan)
- Start from **Google Street View of the real street** and keep checking it. In Snowdrop, the building tool takes **a footprint and a list of kits** per building. Blockout concentrates on
  scale (widths, floor counts, sidewalk distance), then window shapes and wall colours from the kit library ([80.lv](https://80.lv/articles/division-pt-1)).
- **Street dressing has a fixed order**: light poles, signals and signage, then road markings, manholes, tarmac patches. The team made **hundreds of fictional shop signs and brands**, each with
  colour schemes and product-placement rules, so shopfronts read as a real commercial street.
- Composition: tall dark buildings at the sides to make a vertical image, cranes forming strong shapes to lead the eye.

### Marvel's Spider-Man
- Manhattan was authored **procedurally in Houdini, offline (not at runtime)**: ground, buildings, traffic, vignettes, props, impostors, audio, lighting ([GDC 2019](https://www.gdcvault.com/play/1026415/Procedurally-Crafting-Manhattan-for-Marvel); I could only read the abstract).
- **Windows show furnished rooms with no geometry**: interior mapping, a shader that ray-casts to floor, ceiling and wall planes behind the glass and reads them from a cube-texture atlas of rooms
  ([technique](https://80.lv/articles/interior-mapping-rendering-real-rooms-without-geometry), [reports](https://www.resetera.com/threads/spider-man-ps4s-interior-mapping-shader-makes-the-city-pop.74071/)).
  Cost is nearly independent of room count; limits: furniture is flat, no lighting interaction, and repetition has to be managed with room atlases and differing room depths.

### The academic baseline
- **CGA shape grammar** (Mueller, Wonka et al. 2006, the language behind CityEngine): buildings are *mass models* (volumetric shapes of any orientation) then split, repeat and component-split into
  facades and roofs, with stochastic rule choice and context queries (occlusion, snapping) so details stay consistent on rotated, overlapping volumes
  ([paper](https://dl.acm.org/doi/10.1145/1141911.1141931)). The Houdini building tools in games do the same thing: mass shapes -> boolean -> facades -> typed (window, door, wall) -> assets placed
  and scaled to fit ([80.lv](https://80.lv/articles/006sdf-environment-art-procedural-workflow-in-houdini)).
- **Tripartite composition**: base (two or three storeys), shaft, cap (one to four), with a shared style so the three read as one building
  ([Chicago School](https://www.architecture.org/online-resources/architecture-encyclopedia/tripartite-division)).
- **Setbacks are why real skylines look the way they do**: New York's 1916 zoning forced towers to step back as they rose, producing the wedding-cake and podium-and-tower forms
  ([Building the Skyline](https://buildingtheskyline.org/revisiting-1916-i/)).
- **Material variety on a budget**: a clean tiling texture plus a dirty version blended by vertex colour or a mask, trim sheets for repeated detail, decals for one-offs, per-pixel tinting
  ([polycount threads](https://polycount.com/discussion/77922/tiling-building-textures-variation)).

### What the games have in common
None of them get realism from a smarter window grid. They get it from **layers of different-scale detail on varied shapes, a material and wear pass that varies per building, a busy
ground floor with signage, and a deliberate identity per district**. Cyberpunk does it by hand; we cannot, so we have to *generate each layer*.

---

## 3. What would change the look most (ranked by return, with the reason)

1. **Massing from the lot, not a box.** The biggest effect at skyline and aerial distance, and it is the thing the Subversion-style plan was meant to enable. Use the real lot polygon:
   build a building as one to three turned rectangles (L, T, U, courtyard), a podium with a smaller tower, stepped setbacks, chamfered corner buildings, party walls against neighbours,
   stepped or tapered crowns. The architecture already allows it: `MeshBuffer.SetFrame` turns each rectangle, and the old grid city had wings before the port removed them.
2. **A different ground floor.** One to two storeys of the building are the base: taller, with a shopfront or lobby, a sign band, awnings, a canopy, steps, roll-down shutters, vents and
   utility boxes. This is where a player's eye is most of the time.
3. **Facade composition instead of a grid.** Base / shaft / crown. Bays with a rhythm (not one pitch), piers and spandrels, cornice lines every few floors, sills and lintels, mullions with
   depth, parapet with coping, balconies and fire escapes at a rhythm. Different style families per district, as today but richer.
4. **Material, tint and wear per building.** A palette (brick, plaster, concrete panel, metal cladding, stone, curtain-wall tint), a separate roof material, a roughness spread, dirt at the base,
   streaks under sills and ledges, baked-looking ambient occlusion, and a **second, dirtier texture blended in by a per-building or per-vertex mask**. Cheap to run and the largest single
   change per unit of effort on every wall in the city.
5. **Glass that looks like glass.** An opaque reflective glass at distance (sky reflection, fresnel, a lit or dark interior-mapped room behind each window), real transparent glass only inside the
   full-detail radius where windows can break. This looks better *and* shrinks the transparent pass (about 1,750 transparent draws in the last census).
6. **Rooftops at a believable scale.** HVAC units, bulkheads, water tanks, billboards, masts and helipads sized to the roof, on a roof material that is not the wall.
7. **Street dressing in a fixed order** (The Division): poles, signs, signals, then markings, manholes, patches; hydrants, bins, bollards, utility boxes, wires in older districts, scaffolding and
   hoardings at random. Trees stay out until you ask.
8. **District identity and landmarks.** Four to six looks (Cyberpunk uses four; Watch Dogs 2 uses 36 styles for one city), a few hero buildings and large visible signs as waypoints. The old special
   buildings (supermarket, hospital, bank, police) should come back as *architecture*, not just a roll.
9. **Atmosphere** multiplies everything above: wet roads in rain reflecting neon, colour grade, depth haze, varied window colours at night. The rain and post-processing exist; this is tuning.

---

## 4. How it fits this codebase and the frame rate

The hard constraint from `docs/PERFORMANCE.md` is **draw calls, not triangles** (about 8 us per draw). So every item above must arrive *without more materials, objects or transparent draws*.

- **One wall material, with the variation carried in the data.** Add a per-vertex channel to `MeshBuffer` (a `Color32` is 4 bytes per vertex) or, cheaper, one float in a spare UV carrying a **building id** that
  indexes a small per-building table (tint, material slice, wear, window style, lights-on). A **texture array** of wall materials (brick, plaster, panel, metal, stone) plus a shared dirt mask gives
  the variety with one sampler. Unity URP exposes only 16 samplers, so arrays and packing matter ([notes](https://medium.com/@maglucen/making-vertex-painted-terrain-work-in-unity-urp-without-breaking-your-sampler-budget-4836361d6d66)).
  This needs a custom (Shader Graph or hand-written) lit shader instead of stock URP Lit.
- **Facade UVs are known** (every quad is generated), so world-metre UVs give consistent scale with no triplanar cost.
- **Detail by distance.** Near: real geometry (cornice profiles, sills, mullions, reveals, awnings) in the existing buffers. Far (`BuildBuildingShell`): the same material and tint, detail faked in the
  texture and normal map, as the shell already merges glass strips. The far version must keep the new silhouette (setbacks, crowns), because the skyline is exactly what far chunks show.
- **Opaque far glass** with a sky-reflection and interior-mapped rooms; transparent glass only near.
- **Decals** (URP decal feature) for stains, posters and graffiti near the player; vertex/mask wear far away.
- **Signs and brands**: one emissive atlas, all signs in the existing submeshes, no extra draws. A brand table per zone, as The Division did.
- **Deterministic**: every choice from `site.Seed` by integer hash, as `WindowPlan` does, so every client builds the same city.
- **Small assets** (standing preference): generate the wall textures procedurally in an editor script, 512 px, block-compressed, rather than importing large images.
- **Measure** with `ExteriorSurvey` (variety), the street/aerial captures (look, same cameras before and after), and the `PerfBuild` bench (draws, fps).

### Couplings to check before building
- **Massing and interiors are one decision.** A building made of several volumes (podium plus tower) needs the mini-dungeon interior generator (`docs/BUILDING_INTERIORS.md`) to take a list of volumes,
  not one rectangle. `BuildingSite` and `PaneKey` are one rectangle each today.
- **Windows, doors, glass breaking, bullet holes, the climb-in-through-a-window behaviour and the interior volume** all key off the single-rectangle site. Compound buildings can be several sites with a shared
  family id and still use all of that, but it must be designed once, not patched in.
- `WindowGrid.TopEdgeHasSlab`, the lit-window floors (`row >> 1`) and fall damage assume the current two-row storey.

---

## 5. Suggested order (when you say go)

1. **Material and light pass** (highest return, smallest risk): per-building tint and a wall texture array, a distinct roof material, roughness and dirt/streak wear, reflective far glass, bigger rooftop
   plant. No geometry change; compare the same four shots.
2. **Ground floor and facade composition**: base / shaft / crown, bays with rhythm, cornices, sills, mullion depth, shopfronts with a sign atlas and awnings.
3. **Massing from the lot**: compound volumes, podium plus tower, setbacks, crowns, corner buildings, party walls; the far shell updated to match. Decide the interior coupling first.
4. **Street dressing** in The Division's order.
5. **Districts and landmarks**: palettes and kits per zone, hero buildings, waypoint signs, the old special buildings back.
6. **Atmosphere**: wet roads and neon in rain, grade, night window colour variety, interior-mapped windows.
Each step ships with before/after captures from fixed cameras, a fresh `ExteriorSurvey`, and a bench run.

## Open questions for you
1. **Target look.** Cyberpunk is a stylised, neon-heavy, hand-authored city; Watch Dogs and The Division aim at a grounded real city. The game's own art direction is Chongqing (dense terraced
   brutalist concrete, walkway lattice). My recommendation is a grounded dense Asian megacity: weathered concrete, tile, mixed-material podiums, dense signage, neon at street level, with a
   Cyberpunk-style district identity on top. Say if you want it more stylised or more realistic.
2. **Whether buildings may become several volumes** (podium plus tower, L plans), which is the single biggest silhouette change but must be designed together with the mini-dungeon interiors.
3. **Textures:** procedural, small, generated in the editor (my default), or do you want to supply or buy facade texture sets?

## Not read / caveats
Spider-Man's and Watch Dogs' GDC building talks were not accessible (only abstracts), GTA V was not researched, and the Cyberpunk and Division details come from developer interviews and artist
posts, not first-hand. The ranking in section 3 is my judgement from these sources and from looking at the renders, not a measured result.

---

## Progress (2026-10-01, after the go-ahead)

Direction given: **dense megacity, but not so crowded it overwhelms**; "proceed and use your own discretion"; generate wall textures (the user may
replace them with their own). Built so far, in the order of section 5:

1. **Material and light pass.** One facade shader (`View/Shaders/CityXFacade.shader`, URP Lit's passes with the surface replaced) reads each vertex's *look*
   (`View/SurfaceLook.cs`: tint, one of eight wall materials, wear) and draws it from texture arrays. `MeshBuffer` carries a `Look` and a `UvOffset` per building.
   `World/BuildingLook.cs` decides a building's walls, ground floor, roof, trim, frames and glass tint from its seed, zone and facade style (restrained palettes,
   district wear). The eight materials (`View/FacadeTextures.cs`: concrete, brick, plaster, metal, stone, tile, glazed spandrel, roof) are drawn procedurally to
   `View/Textures/FacadeAlbedo.png` and `FacadeNormal.png` (4 x 2 grids of 512 px tiles, imported as texture arrays). **Replace the two PNGs with your own set on the same
   grid and slice order and they are used as they are** (`CityX/Generate Facade Textures` never overwrites existing ones; `Regenerate` does). The roof is its own material, in
   the wall buffer, so a building is still one draw. The rain's wetness reaches the walls through a global (`_CityXWetness`).
2. **Ground floor and facade depth.** Walls are 0.3 m thick: windows are recesses with reveals and a sill, the glass sits back inside them, with frames and mullions as
   geometry (a broken window keeps its frame). Doors are a real 2.4 m with a lintel (they were the whole 3.2 m row). Shops get glazing from a stall riser to a sign band, a sign
   board and (usually) awnings; other entrances get a canopy. New window glass shader (`CityXGlass.shader`): a tint per building, one of several kinds of light per lit window, blinds drawn
   part-way down.
3. **Massing.** A building is often a stack of volumes (`World/Plan/Massing.cs`): a tower on a podium, a tower that steps in as it climbs. Each volume is its own `BuildingSite` standing
   on the roof of the one below, so windows, doors, glass and interiors all work unchanged; the podium's roof hatch is the way up, kept clear of the tower. Stacked volumes are found by
   height (`CityWorld.TryBuildingAt(Vector3)`).

Tools: `CityX/Generate Facade Textures`, `Capture Facade Swatches` (a sheet of the eight materials, clean and worn), `Capture Plan City`, `Survey Exteriors`.
4. **Roofs, accessories, banding.** Every roof has a parapet with coping and real rooftop plant (air handlers, vents, skylights, ducts, a stair head, tanks, masts), kept off the
   roof hatch and off the volume standing on it. Homes get air conditioners under some windows. Some buildings have the strips above and below the windows in another
   material from the piers between them (`BuildingLook.BandMaterial`).
5. **The ground.** Roads, pavements, kerbs, bare land and the hardstanding round buildings are the same facade shader and the same single draw as the walls (the chunk mesh's
   ground, pavement and wall submeshes are one). Four more slices of the texture array (now 12, a 4 x 3 grid): paving slabs, road asphalt (cracks, patched repairs),
   bare earth, granite setts. Roads differ by class and wear, pavements by block, the land inside a block is concrete apron, tarmac or setts and only an unbuilt block is sometimes bare earth.
   Zebra crossings at the mouth of most side streets; a hydrant, bin or utility cabinet now and then along a pavement (**no lamp posts**).
6. **Signs.** Vertical lit blade signs on about half the shopfronts (some two) and a few downtown buildings, in the lit-glass buffer so they come on at dusk like the windows.

**Scale rule learned the hard way:** the player is 2.6 m tall (`BodyRig.StandingHeight`), so everything a body passes through is about 1.3x real: doors are the whole 3.2 m
ground row, an awning's lowest edge is above 2.7 m, the bars across a broken window are left out so it can still be walked through.

**Measured (2560 x 1600, RTX 3070 Laptop, D3D12, release player, `CityX/Builds/Bench`):** standing 184 fps, sprinting through the streaming city **179 fps**, worst frame 41 ms,
0.1% of frames under 60 fps; about 5,900 submesh draws loaded, 1,800 in view (406 transparent). Ample room left for more detail.

**Still to do:** skybridges between towers (the old Chongqing walkway look), the special buildings as landmarks, graffiti/posters/stains as decals, wet-road reflections (needs
screen-space reflections or probes), street lamps (only if you want them back), tree-less public squares, and the mini-dungeon interiors.

### Fix 2026-10-02: road paint lies on the road

Reported: the right foot sinks into the zebra crossing ("the street decals should not be on a higher level than the street"). Paint floated 3 cm over
the road so it would not z-fight, and a body on it sank 3 cm. It is now 1 cm up and drawn with `CityX/Paint` (URP Unlit plus a depth offset, also in the
depth passes; units grow with distance like z-fighting does, so up close, at the feet, it is a fraction of a millimetre). RoadLine and Crosswalk use it.
