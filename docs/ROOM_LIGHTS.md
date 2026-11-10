# Room lights and light switches (2026-10-09)

Owner: "make it so every room has a light switch with lights in the rooms, different patterns of lights ... research lights in interiors in video
games. make it so 10% of the buildings that spawn have electricity and the others the lightswitch just does nothing. we will make a way to turn
the electricity back on later."

## What the research said
- Games light interiors with a few practical fixtures that read as the light source (an emissive face) plus a few real lights that do the lighting
  (The Level Design Book: key/fill/accent; ambient fill from dim soft point lights; CryEngine tutorial: fluorescent "practical" fittings).
- Put the lamp light in the middle of the fixture and match the emissive colour to the light's colour (80.lv, Unreal lighting guide).
- In Unity: point-light SHADOWS are the cost (six renders each) -- switchable room lights cast none; baked light cannot switch, so these are realtime;
  Forward+ (our PC renderer already uses it) removes the per-object light limit; snap or fade `Light.enabled`/intensity to switch.
- Real interiors: offices = recessed panel grids or rows of fluorescent tubes (cool white); homes and old buildings = a central fitting or pendants
  (warm); shops and lobbies = many small downlights (neutral).

## How it works
- `World/RoomLighting.cs` (pure): every room -- each floor's open hall and its corner room (the rooms `BuildingStairs.RoomFor` carves) -- gets a
  `LightPattern` by zone and seed (PanelGrid, FluorescentRows, Pendants, CentreFitting, Downlights), its fittings on a grid that keeps off the walls,
  the corner room and the stair holes (at most 16; nothing hangs below 3.2 m, the 2.6 m player's head room), 1-6 real light points (one per cell of a 1x1/2x2/3x3 split by room size), a colour per pattern,
  and a switch by the door (hall: front wall beside the entrance; corner room: inside, on the wall with its doorway), 1.45 m up.
- `PowerGrid`: one building in ten has power (`RoomLighting.PoweredShare`, rolled from the building's seed). `PowerGrid.Restore(building)` is the hook
  for the coming "electricity back on" mechanic.
- `RoomLights`: which switches are on (shared state + `Changed` event). A switch flips in every building; only a powered one lights up, so
  restoring power later lights the rooms whose switches were left on.
- Drawing: `CityMeshBuilder.RoomLights.cs` puts every fitting and switch plate into the chunk mesh (all buildings, near detail only). For powered
  buildings it also hands out a `RoomGlow` per room (the glowing faces and light points), which `CityStreamer.AddRoomLights` gives to the chunk's
  `ChunkRoomLights` as a `RoomLightGroup` per room (a plain object). Only while a room's switch is on is anything drawn or lit for it: its glow
  (`RoomGlow.mat`, URP Unlit, colour x3 so it blooms) joins its chunk's mesh of lit rooms of its colour, and `RoomLightDirector` lends it spot
  lights without shadows (since 2026-10-14, see the last update; before, every room's meshes and lights were made when its chunk streamed in).
- Interacting: `View/Interactables/LightSwitchInteractor` (on the CityStreamer, one for the scene): E or the gamepad's west button flips the switch nearest the
  player that is within 2.4 m, on their floor and in front of where they look -- found from the plan only when the key is pressed, so there is no
  component per switch. A procedural click plays either way.
- Multiplayer: `Character/RoomLightsNetwork` keeps every machine's switches the same with FishNet BROADCASTS (not RPCs on a body, which only reach
  players within 100 m): flip -> server (checks the room can exist, at most 12 flips/s per player) -> every client; a joining player asks for every
  light that is on once it is authenticated.

## Not done
- Round towers and sealed sculptures have no interior lights (no rooms of that kind).
- The switch's rocker does not move when flipped (it is part of the chunk mesh).
- Tests: `RoomLightingTests`, `RoomLightsPlayTests`, `NetworkedRoomLightsPlayTests` (real sockets, server + 2 clients + a late joiner);
  `RoomLightsCapture` (Explicit, -Graphics) renders lit rooms to `Logs/CityShots/roomlights-*.png`.

## Update: no random lit windows; a switched-on room glows from the street (2026-10-09)

Owner: "when you do /time night random windows turn yellow stop that from happening just make the lighting look good if you have a light turned on
and you're looking from outside". Every window used to be lit at night by chance (`WindowGrid.LitShareFor` per zone; glass offices floor by floor
plus 4% "desk lamps"), and tall towers wore lit crowns and panels (`AddSkylineLights`). All of that is off: the builder's lit share is 0
(`WindowPlan.IsLit` is false at 0), and the crowns are no longer built (their code was deleted on 2026-10-17; it is in git history). Shop blade signs still glow at night
(they are signs, not windows).

Instead, a powered building's rooms collect their windows while the panes are built (`CityMeshBuilder.RoomWindowGlow`: each pane goes to the room
behind it, by floor and by whether it is in that floor's corner room), as a glowing face 5 cm behind the glass in the room's light colour
(`CityStreamer.RoomWindowBrightness`), switched with the room's fittings and lights. From the street a lit room shows as its own windows lit, warm
for pendants, cooler for offices. Tests: `ExteriorDetailTests.NoWindowLightsUpThatNobodySwitchedOn`, `RoomLightingTests.ALitRoomsWindowsGlowAndOnlyItsOwn`.

## Update: the lighting pass -- lamps, bounce, pale rooms, clear glass, door lamps (2026-10-09)

Owner: "the lighting really doesnt look good both indoors and looking from outdoors during nighttime. research indoor lighting in video games as
well as city lighting at night time and implement what you learn". What the research said, and what was done with it:

- **Ceiling lights are downward spots, not point lights.** A point light 40 cm under a fitting burnt a bright hot spot into the ceiling right
  above it and left the floor dim -- the opposite of a real lit room, where the floor is bright and the ceiling is lit only by light bounced off
  it. Each lamp (`RoomLightPlan.LightPoints`, one per cell of the room's split) is now a spot just under its fittings, shining straight down
  (`RoomLighting.LampOuterAngle` 140, inner 80), as bright as its height needs (`FloorLight` x height squared). Up to 9 lamps a room (a 3x3
  split for a big hall; it was 6, and the last third of every big hall was dark); a big hall with centre fittings gets a grid of them.
- **A lamp is a fitting, not a point**: the facade shader softens a room light's falloff to 1 / (d^2 + r^2) (`CITYX_FITTING_RADIUS_SQ` 2.6), or
  the wall a metre from a lamp got thirty times the floor's light and burnt white.
- **A fake bounce instead of global illumination.** Under each lamp a second spot shines straight up
  (`BouncePoints`, `BounceOuterAngle` 170) from INSIDE the floor slab (`BounceBelowFloor` 0.08 of the 0.2 m slab): it lights the ceiling and the
  walls softly, never the floor it stands under (which faces away from it),
  in the room's colour greyed a little, about a quarter as bright as the floor (`CeilingLight` 0.24 vs `FloorLight` 0.9). First try had it 0.8 m
  down -- below the slab, inside the room underneath -- and every lit room burnt bright ovals into the ceiling below it.
- **Nothing reaches another storey.** A lamp's range stops short of the floor of the storey below, a bounce's of the ceiling of the storey above.
- **Lights know which side of a wall they are on** (`View/Lighting/CityLightLayers`). Room lights are on rendering layer 1 ("inside"), the door lamps on
  layer 2 ("outside"); the facade shader (`FacadeForwardPass.hlsl`, `CityXFragmentPBR`) skips a light on the wrong side of the surface it is
  shading, so a lit room never shines through its walls onto the pavement or the building next door. Everything else (players, props) is lit by
  both (URP's own layer test); unmarked lights (a muzzle flash) light everything, as before.
- **Shadows only where the player is.** `View/Lighting/RoomLightDirector` (made on demand by the first lit room) keeps real lights only for lit rooms within
  110 m of the camera (at most 160 room lights at once -- one fully lit tower has hundreds; farther rooms are their glow alone) and gives the 4
  lamps nearest the camera, within 30 m, soft shadows: bodies, stairs and partitions throw shadows in the room you are in. Bounces never do.
- **Pale rooms.** The inside of every building is no longer bare grey concrete (which swallowed the light): plaster walls in a colour that says
  what the building is (whites and greys for offices, cream/sage/pale blue/pink for homes, painted block for works), a white ceiling, a floor by
  zone (tiles, terracotta, pale stone, polished concrete), concrete stairs (`CityMeshBuilder.Interior.cs`, `InteriorLooks`).
- **Indoors is a flag on the surface.** `SurfaceLook` packs it into the vertex colour (wear went from 16 steps to 8 to make room); the facade
  shader dims the sky's light (ambient and sky reflection) on it by `_CityXIndoorShade` (`DayNightCycle.IndoorShadeDay` 0.65 by day,
  `IndoorShadeNight` 0.5 at night): a room is lit by its windows and lamps, not by the whole open sky.
- **Windows with a room behind them are clear glass** (`SurfaceLook.PackGlass(clear)`, the glass shader's `_ClearAlpha` 0.4 instead of 0.72), so
  from the street a lit window shows the real lit room -- its ceiling, its fittings -- instead of a flat painted panel. Glass with nothing behind it
  (a distant building's strips, the sealed top of a tower) stays dark. The old opaque glow panel is now a faint additive veil just behind the glass
  (`Shaders/CityXWindowLight.shader`, `CityStreamer.RoomWindowBrightness` 0.15), brighter towards the top, so a lit window still reads from down the
  street; it is one-sided, so it is not there when you look out from inside.
- **A lamp over every powered door** (`World/EntranceLighting`, `View/Lighting/EntranceLamp`): a warm glowing box under the canopy and a spot shining down
  on the steps and pavement, lit from dusk to dawn (`DayNightCycle.Daylight`). Pools of warm light at doorways with the street dark between them
  are what a night street is made of -- and in a city where nine buildings in ten are dead, a lit doorway is how you find the ones with power.
  Street lamps were NOT brought back (removed 2026-09-04 on request).
- **Shop blade signs glow only on powered buildings**; a dead building's sign is dark glass like its windows. So do the lit shop signs and the
  floodlit billboards on the roofs (2026-10-15, docs/WORLD_GACHA.md phase 4: the board's own paint lit by its lamps, `SurfaceLook.PackFloodlit`).
- **E works the thing you look at** (`View/Interactables/InteractionFocus`): the door and the switch beside it used to both answer one press. Now each offers
  itself with how far off the crosshair it is, and only the nearest acts; of several switches in reach, the one nearest the crosshair flips.

Tests: `NightLightingTests` (indoor flag, pale rooms, lamps and bounces per storey, layers, clear glass, dark signs, door lamps, the night's
stars/grade/indoor shade), `RoomLightsPlayTests` (spots, layers, the director's shadows and reach, the door lamp at night), `InteractionFocusTests`.

## Update: brighter rooms, lit stairwells (2026-10-10)

Owner: "some rooms aren't getting lit up enough by the lights. also the rooms with the stairwells should have lights and switches in there".
- Brighter and denser: `FloorLight` 0.9 -> 1.3, `CeilingLight` 0.24 -> 0.32, wider full-strength cone (inner 100), reach 2.6 x height; a room is
  split for lamps by `RoomLighting.SplitFor` (one per ~8 m, up to 4x4 = 16 lamps). `RoomLightDirector.MaxRoomLights` 220.
- Every enclosed stairwell is room 2 of its floor (`RoomLighting.StairwellRoom`): a switch inside beside its doorway and a WALL light over the
  doorway (the shaft's ceiling is nearly all the opening the flight climbs through). The hall's fittings keep out of the shaft. The server accepts
  rooms 0..2 (`RoomLightsNetwork.Acceptable`). Test: `RoomLightingTests.AnEnclosedStairwellHasItsOwnLightAndASwitchInside`.

## Update: nothing is made for a dark room (2026-10-14)

The room lights were the next main-thread cost of the city streaming in (PERFORMANCE.md). Measured first, with a temporary timer in
`CityStreamer.AddRoomLights` while the streamer flew through the game's own districts: every powered room was a GameObject with its glow mesh,
its window mesh and one spot light per lamp and per bounce (up to 32), all made in the frame its chunk landed: **24.4 ms** for a block of 20
rooms (294 lights: 12 ms of meshes, 10 ms of lights), **14.1 ms** for one of 30 (292 lights). Making one light costs 25-40 us (100 us the
first time in a session). And nearly all of it stayed dark: a room lights only when somebody flips its switch.

Now nothing is made for a room until its switch is on:
- **A room is a plain object** (`View/Lighting/RoomLightGroup`, no longer a MonoBehaviour): where its lamps and bounces go, their colour and brightness,
  and the faces it shows (`CityMeshBuilder.RoomGlow`, kept from the worker that built them). A chunk with powered rooms gets one component,
  `View/Lighting/ChunkRoomLights`, which follows the switches (`RoomLights.Changed`: one listener per chunk; there was one per room).
- **The lit rooms' glow and windows are one mesh per light colour per chunk** (five colours in all, `RoomLighting.ColourOf`), drawn with the same
  materials and colours as before (`RoomGlow.mat` x`RoomGlowBrightness`, the window veil x`RoomWindowBrightness`), rebuilt when a switch in the
  chunk changes, at most once a frame and before the frame is drawn (`RoomLightDirector`'s LateUpdate). A dark room draws nothing; a chunk whose
  rooms are all dark has no object for them at all.
- **The real lights are lent from a pool** (`RoomLightDirector`): a lit room in reach takes spot lights from it, put in place, coloured and
  switched on, and gives them back switched off when it goes dark, out of reach, or off screen with its chunk. No more than `MaxRoomLights`
  (220) ever exist; the pool makes at most `MaxLightsMadePerFrame` (32, the biggest room's worth) in one frame, so a player joining a session
  with many rooms lit round them gets them over a few frames, nearest first. They hang under the hidden director, never under a chunk. Which
  lit rooms get lights and which lamps cast shadows is decided as before (`LightReachMetres`, `MaxRoomLights`, `MaxShadowedLamps`), now in the
  frame a switch flips or a chunk with lit rooms comes or goes (a flipped room used to switch all its lights on at once and be trimmed within
  0.2 s).

After, same flight: the 20-room block **0.16 ms**, the 30-room one **0.06 ms**; a 29-room block attaches in 0.2 ms with every room dark, and
switching 20 of its rooms on at once (their glow drawn, 220 lights lent from the pool) takes 1.25 ms. The first powered chunk of a session pays
4.5-6 ms once in the editor (its code running for the first time, the director made).

The look is unchanged, checked by rendering: `RoomLightsCapture` (-Graphics; 62 shots of halls, corner rooms, stairwells, switches, the street,
the facade and the door, day and night, the five kinds of lighting and a dead building) was rendered with the old code and with the new, and
the images are the same to the pixel (mean difference 0.000 of 255, at most 2 on a handful of pixels). Multiplayer is unchanged too: the
switches are world state (`RoomLights`, `RoomLightsNetwork`) and every machine draws its rooms from it.

Tests (`RoomLightsPlayTests`): the flip test as before (its lights now found through the room: a dark room has none); nothing is made for a
dark room (no light but the door lamps, nothing drawn); a lit room glows in its own colour and rooms of one colour share a mesh; the pool never
outgrows the budget and is reused; a chunk that goes gives its lights back, and a room left on comes back lit; what a powered block costs
(every room dark: under 2 ms, measured 0.1-0.2; twenty rooms switched on: under 8 ms, measured 1.25).

## Update 2026-10-15: no dark rooms on cramped floors

Seed 83920571 turned up a dark hall: the top floor of a 10 x 12 m building, whose only free strip of ceiling (between the corner room and two
stair holes) was 2.3 m wide, while its fluorescent tube is 2.4 m. The last-resort fitting (`RoomLighting.Plan`, when the grid finds no free
spot) now tries the fitting as it is, then turned a quarter round, then a small 0.6 m one anywhere off the walls, on a 25 cm search grid.

## Update 2026-10-22: the light a room's walls throw back (RoomFill)

Owner: "it feels like there are still really dark spots that are close by to the light source im not sure if its working as good as it
could be". The night renders (`RoomLightsCapture`) showed where: the ceiling round every fitting, the tops of the walls beside the lamps, the
undersides of stairs and the bottom of stairwells were near black, a metre from a glowing fitting. Every light in a room shone down in a cone,
and the only stand-in for the light a real room bounces off its pale walls was the bounce spot, which lights the ceiling straight over it and
little else. The ambient occlusion was not the cause: rendered with it off, the rooms barely changed.

What games do instead of global illumination (Godot's "faking global illumination" page; Source's ambient cubes per visleaf): a soft fill with
no shadows and no shine, 10-40% of the main light. A fill LIGHT here would leak through floors and walls (room lights cast no shadows), so the
fill is per ROOM, the way Source keeps its ambient per leaf:

- **How bright** (`RoomLighting.FillFor`, pure): the lamps' light (pi times each lamp's straight-down brightness), given back by surfaces of
  reflectance 0.5 again and again (r / (1 - r)), spread over the room's floor, ceiling and walls; a small room is brighter than a big hall
  with the same lamps. Never more than `MaxFillShare` (0.6) of the light under a lamp, or a small tall room crowded with lamps is lit flat.
  Halls come out at 0.4-0.8 against the lamps' 1.3, corner rooms and stairwells up to 0.78.
- **Where** (`RoomLightPlan.FillArea`, `FloorY`, `CeilingY`, `Cuts`): the room's box, a hall's out to its window glass (the reveals), less
  the rooms carved from it (the corner room, an enclosed stairwell), which have their own switch.
- **Drawn by the facade shader** (`View/Lighting/RoomFill`, `FacadeForwardPass.hlsl` `CityXRoomFill`): `RoomLightDirector` packs the 32
  nearest lit rooms (lamps or not, so a lit room seen from down the street glows inside) into a global array; an indoor surface adds the light
  of the room it FACES -- the point 5 cm in front of it is the one tested -- so a floor belongs to the storey above it, a ceiling to the storey
  under it, and the two faces of a partition (one sheet) to the rooms either side. Diffuse only, dimmed by the screen-space occlusion (corners
  and contact shadows) but not by the indoor sky shade.
- Players and props (URP Lit) do not get it; the lamps and bounces still light them.

Found on the way: a hall's lamp hangs under the middle of the fittings in its part of the hall, and fittings either side of an enclosed
stairwell in the middle of a hall put it inside the stairwell (seed 19077151's building 7343). A lamp whose middle lands in something the
room keeps clear of (a carved room, a stair hole) now hangs under the fitting nearest it.

Tests: `RoomFillTests` (the fill covers its floor, walls, ceiling and fittings and neither the storey above nor below; a partition's faces
go to the rooms either side; a hall's fill reaches its window reveals but not its stairwell; brightness bounds; no lamp in a carved room or
over a stair hole; the shader's constants and packing), `RoomLightsPlayTests.ALitRoomsBouncedLightGoesToTheShaderAndComesOutWhenItGoesDark`.
Checked by rendering `RoomLightsCapture` before and after.

## Update 2026-11-09: the marks had stopped reaching the shaders

Found while making the sewers dark (docs/SEWERS.md): a street lamp lit the tunnel under its road although the shader skips a light
marked outside. URP keeps only the marks on rendering layers the project NAMES (Project Settings, Tags and Layers), and strips the rest
without a word; the project's layers still carried the placeholder names of an older Unity ("Light Layer 1" to "7"), which this one
does not count as named (`RenderingLayerMask.GetDefinedRenderingLayersCombinedMaskValue` gave the default layer alone). So every light
reached the shaders unmarked: a lit room shone through its walls onto the pavement and the house next door, a door lamp lit the room
behind its wall, and the soft fitting (`CityXSoftenFitting`, which knows a room light by its mark) never ran. Every test read the mark
back from the light, where it was fine.

- The layers are named for what they are: `Interior Lights`, `Exterior Lights`, `Carried Beams` (`CityLightLayers.Interior`, `Exterior`,
  `Beam`). `NightLightingTests.TheShadersReadALightsMarksByTheBitsTheyAreSetWith` fails if a mark is on a layer the project does not
  name.
- `LightMarksPlayTests` (needs `-Graphics`) renders a wall under each mark: a room's light does not light the street and a street
  light does, and the other way round for a room's wall. Only a picture shows a mark that never arrived.
- The street lamps' real lights (`StreetLampDirector`) are marked outside too: they cast no shadows.
- What it changed, rendered with the old names and the new on the same rooms (`RoomLightsCapture`, 40 night shots): rooms are 1 to
  4 % dimmer inside (the soft fitting, and no light from the next room through the wall); the pavement in front of a lit building is
  10 to 15 % darker, lit by the door lamp and the street lamps alone. A lit room no longer lays pale patches on the pavement through
  its wall. Light falling out through the WINDOWS onto the street would need the room lights to cast shadows; not built.
