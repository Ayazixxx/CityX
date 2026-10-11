# The map (M), the minimap, and waypoints

Asked for (2026-10-11): "research video game map systems and implement what you learn".

## What the research says

- **Getting lost is almost always frustration, not fun**, unless the game is about navigation. Players build a mental map from landmarks: Nicolas Oueijan, ["Stop Getting Lost: Make Cognitive Maps, Not Levels"](https://gdcvault.com/play/1027206/contactUs) (GDC).
- **GTA's GPS** is a shortest path over the road graph, drawn as a bold coloured line (purple) from the player to the waypoint. It optimises for lawful generic travel, not the player's vehicle ([GTA wiki: GPS](https://gta.fandom.com/wiki/GPS); a [student paper](https://informatika.stei.itb.ac.id/~rinaldi.munir/Matdis/2022-2023/Makalah2022/Makalah-Matdis-2022%20(78).pdf) on its graph search).
- **Minimaps should be selective**: show what helps right now (routes, objectives), not everything ([paper on minimaps](https://informatika.stei.itb.ac.id/~rinaldi.munir/AljabarGeometri/2024-2025/Makalah/Makalah-IF2123-Algeo-2024%20(25).pdf)). GTA VI keeps its minimap out of the way and lets it be hidden or swapped for a compass. Its map uses shaded roads and raised building shapes rather than flat linework ([GTA Boom](https://www.gtaboom.com/gta-6-minimap-c6d7)).
- **Fog of war:** reveal only what was explored. The cost is in updating it, so update it rarely ([devlog](https://jeremyedwards.itch.io/helm-of-heresy/devlog/1082149/creating-fog-of-war); [Iron Danger minimap](https://irondanger.com/blog/friday-rewind-9/)). This game already kept explored chunks.
- **Common to open-world games** (general knowledge, not from one source):
  - a player-placed waypoint;
  - its marker in the world itself, so you can navigate by looking up;
  - a minimap that turns with the camera;
  - a north indicator;
  - a legend.

## What was built

| | |
|---|---|
| **Full map (M)** | North up; explored chunks only; roads never thinner than 3.5 px; buildings shaded by height with a soft drop shadow (lit from the top left); **landmark towers** (150 m and over) marked as orange diamonds whether explored or not; the waypoint's route and pin; the player's arrow; a legend and the controls |
| **Waypoint** | **Right-click** the map to set it; right-click the pin to clear it; it clears itself within 8 m (`MapWaypoint`) |
| **GPS route** | `World/Plan/RouteFinder`: A* over the city plan's road graph (cost = road length, guess = straight distance), from the road junction nearest the player to the one nearest the pin, with the two real points at the ends. Re-routed whenever the player is 10 m from where it was last worked out |
| **Beam in the world** | `WaypointBeacon`: a 160 m beam of soft purple light standing on the ground at the pin, fading out between 60 m and 12 m away so it does not blind you on arrival |
| **Minimap** | `MiniMapView`, top right, 170 px, 85 m radius; **turned so the way the camera looks is up**; the route; the pin, held at the edge pointing the way when it is further away; landmarks; an N on the rim where north is; "WAYPOINT 230 m" under it; hidden while the full map is open |

**How it is drawn:** both maps draw through one painter (`UI/MapPainter`). A `MapFrame` turns world (x, z) into panel pixels (centred, scaled, optionally turned) and back again (`ToWorld`, for clicks). Each map is one UI mesh, rebuilt each frame it shows.

**Fix on the way:** the map's drawing object was built without its CanvasRenderer, which logged "There is no 'CanvasRenderer' attached to the "Map" game object" on every scene build. It is now created with it.

## Multiplayer

The waypoint is the player's own, like GTA's, so other players do not see it. Waypoints shared with your faction belong with factions.

## Tests

- `RouteFinderTests` (EditMode):
  - the route goes round a junction with no roads, by the shortest way;
  - in the real city every step of a route is a road;
  - the turned frame puts the heading up, and a click comes back to where it was drawn.
- `MapWaypointPlayTests` (real scene):
  - a right-click sets a waypoint at the clicked place with a street route;
  - the beam stands there;
  - the minimap hides with the full map open and comes back showing the distance;
  - a right-click on the pin clears it;
  - reaching it clears it.
- The Explicit `MapCapture` renders `Logs/CityShots/map-full.png` and `map-mini.png`.

## Later

- **Points of interest:** the four special building types already roll but are unmarked.
- **Shared and other markers:** faction-shared waypoints, and other players or faction members on the map.
- **Player stamps:** Elden Ring style markers you place yourself.
- **Hiding the minimap:** a setting to turn it off.
- **Underground and vertical world layers:** a floor switcher.
