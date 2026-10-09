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
  buildings it also hands out a `RoomGlow` per room (the glowing faces and light points), which `CityStreamer.AddRoomLights` turns into a
  `RoomLightGroup`: a glow mesh (`RoomGlow.mat`, URP Unlit, colour x3 so it blooms) and point lights without shadows, shown only while the switch is on.
- Interacting: `View/LightSwitchInteractor` (on the CityStreamer, one for the scene): E or the gamepad's west button flips the switch nearest the
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
