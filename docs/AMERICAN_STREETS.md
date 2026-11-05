# American streets and sidewalks

Owner, 2026-11-05, with the traffic lights: "research real life american city streets and sidewalks ... and make ours feel like them".
The lights and STOP signs are docs/TRAFFIC_CONTROL.md. This is the street itself: its corners, its sidewalk, what stands on it.

## What an American street has that ours did not (the research)

Real sizes are multiplied by 1.3 here, as everything is (the player is 2.6 m tall).

| Thing | Real | Source |
|---|---|---|
| Sidewalk zones | frontage zone at the buildings, a clear through zone (5 ft least; 8 to 12 ft downtown), a furnishing zone at the kerb where everything stands (trees, lights, hydrants, meters, signs, bins) | [NACTO, sidewalk zones](https://nacto.org/publication/urban-street-design-guide/street-design-elements/sidewalks/sidewalk-zones/) |
| Corner (curb return) | the kerb turns the corner in an arc: 10 to 15 ft radius on city streets, up to 25 ft where trucks and buses turn | [NACTO, corner radii](https://nacto.org/publication/urban-street-design-guide/intersection-design-elements/corner-radii/) |
| Sidewalk surface | poured concrete, scored into squares about 5 ft across, a broom finish; a 6 in concrete kerb with a gutter pan about 18 in wide in the road | common US practice (from memory) |
| Curb ramps | at every crossing, with a yellow or brick-red mat of raised domes 2 ft deep at the foot | ADA standards (from memory) |
| Lanes | 10 ft, 11 ft on bus and truck routes; a parking lane 8 ft, its stalls about 22 ft long marked with white Ts | [Toronto, quoting NACTO](https://www.toronto.ca/wp-content/uploads/2018/05/986b-Lane-Widths-Guideline-Version-2.0.1-May-2018.pdf); stalls from memory |
| Street lights | cobra-head lamps on 25 to 30 ft poles with an arm over the road, 100 to 150 ft apart, staggered side to side | from memory |
| Hydrants | at most 500 ft apart, usually by a corner; the kerb painted red beside them | from memory |
| At a corner | a mailbox, newspaper boxes, a bin; street-name blades (green, white letters) on the signal or STOP post | from memory |
| Street trees | in pits with grates in the furnishing zone, 25 to 30 ft apart | from memory |
| In the road | manhole covers; storm-drain inlets in the kerb at corners | from memory |

## What is built, in this order

1. **Rounded corners.** The kerb turns each street corner in an arc; the asphalt fills the corner.
2. **Street lights** (built 2026-11-05), cobra-heads, lit at night. The owner had the first street lamps removed on 2026-09-04 ("they look bad and are
   clipping into the ground") and asked for streetlights again with the streets for drivers on 2026-10-15: these must stand ON the
   pavement and look like the real thing.
3. **The sidewalk as concrete** (the concrete built 2026-11-05): scored squares, a kerb and gutter band, yellow dome mats at the crossings, red kerb by the hydrants.
4. **What stands in the furnishing zone**: American hydrants, parking meters and stall marks, a mailbox, newspaper boxes and a bin at
   corners, dead street trees in grated pits (nothing has grown on Terraplex 9 since the rapture), manholes and drains.

Not built: street-name signs (owner, 2026-11-05: "we dont need street name signs yet btw not sure if im going to add that").

Kept from the owner: no steps along roads (`feedback_no_stepped_sidewalks`), not every space a plaza, everything walkable.

## How it is built

### Rounded corners (2026-11-05)

The plan keeps its sharp corners: blocks, lots, pavements and junction plates are all cut from them, and none of that changed (no new
world seed). What changed is what the chunks build at each corner.

- **The rule** is `World/KerbCorners.TryAt` (pure): the arc round one vertex of a block's kerb, or none. Its radius is 4 m, 6 m or 7.5 m
  by the smaller of the two roads that meet (10, 15 and 19 ft at the city's 1.3 times life). It is made smaller where it would take more
  than 30% of either kerb it joins, reach more than 7 m along one (what stands along a pavement keeps 8 m off corners), or leave less
  than 1.5 m of pavement at the corner of the lots; under 1.5 m the corner stays sharp. Only a real corner is rounded (55 to 135
  degrees), and only where both streets are driven and on the ground: a pedestrian street (laid flush), a step street and a bridge keep
  theirs.
- **The surface** is `View/Streets/CitySurfaceBuilder.Corners.cs`. Between the sharp corner and the arc the pavement and the ground
  under it are cut away (one triangle for each straight piece of the arc: the cutting takes convex pieces), the junction's own asphalt
  is laid at the road's height, and the kerb's face is stood round the arc. The straight kerbs stop where the arcs begin (`KerbInsets`).
  The far version of a chunk is cut the same, so nothing changes shape as you come near.
- **What stands at a corner** stands on the pavement the arc leaves: a signal's corner pole behind the middle of the arc
  (`CornerPole`), a STOP sign where the straight kerb begins (`SignPastArc`). Both stood in the road otherwise.
- Tests: `KerbCornerTests` (the rule's geometry on every corner round the spawn; the built street is road in the corner and pavement
  behind the arc, with the kerb round it), `TrafficHardwareTests.NoPoleOrPostStandsInTheRoadARoundedCornerGivesUp`. Pictures:
  `TrafficCapture` (`traffic-4-above.png`).
- Round the spawn (classic city, the tests' city) most kerb corners are rounded; the rest are bends, points, or beside streets that
  are walked.

### Street lights (2026-11-05)

- **The rule** is `World/StreetLights.Along` (pure): the lamps of one road, staggered from one side to the other, at most 44 m apart on
  a street, 36 m on an arterial, 32 m on a boulevard (100 to 150 ft at the city's 1.3 times life; a street shorter than half that has one
  lamp, at its middle), none within 13 m of a junction (the
  corner's arc, its lights and its signs have that stretch). The pole stands on the pavement 0.75 m in from the kerb; the arm reaches
  over the road. Only driven streets on the ground have them: not a bridge, a pedestrian street or a step street.
- **Most are dead.** The city has little power and its nights are meant to be near black: a street in three has current for its lamps
  (`StreetLights.Powered`, by the street), the rest stand dark with grey glass.
- **The lamp** is `CityProps.AddStreetLight`: a base, a pole in two tapers, an arm rising to a cobra-head with its lens looking down.
  A powered lens is drawn with the lit windows, so it glows by night by itself. Only the near version of a chunk stands its lamps.
- **The light on the road** is real light lent to the nearest lamps (`View/Lighting/StreetLampDirector`): at dusk and by night up to
  14 sodium-orange spot lights are stood under the powered lamps within 95 m of the eye, fading out toward the edge of that reach so
  one changing hands is never seen. The chunks say where their powered lamps hang as they are built (`StreetLampSites`).
- Tests: `StreetLightsTests`. Pictures: `TrafficCapture.RenderTheStreets` (`streets-3-*.png` by day, `streets-4-*.png` by night,
  `streets-5-*.png` a street without power).

### The sidewalk's concrete (2026-11-05)

The pavement was slabs 0.8 m square, each its own tone, laid by the world's own north and east: on any street not running due north or
east the joints crossed the walk at a slant, and it read as a plaza's paving, not a sidewalk. Now:

- **The surface** is poured concrete scored into flags 2 m square (5 ft at the city's 1.3 times life): one tone from flag to flag, the
  tooled joints dark with a smooth margin beside them, the broom's lines across the walk between (`FacadeTextures.Paving`; the atlases
  are baked from it by `CityX/Regenerate Facade Textures`). The same slice is the mall's floor, the plazas' and the sky hub's paving.
- **The joints run square to the street.** `World/Sidewalks.TryAlong` (pure) gives each side of a block its own frame: rows along the
  kerb, the first joint 0.2 m in from the kerb's face (so the kerb reads as a stone of its own), and the rows fitted so the last joint
  is the back of the walk. What lies behind the sidewalks is laid along the block's longest side (`Sidewalks.Behind`), and so is a
  plaza's ground.
- **How it is built**: `CitySurfaceBuilder.Sidewalks.cs`. The block's paving is still one surface cut on the ground's grid; each piece
  of it is split along the sidewalks' strips (`Split`, the same cut `Subtract` makes) and each part is given its own frame's texture
  coordinates. No geometry moved: only where the pattern lies.
- Tests: `SidewalkPavingTests` (the frame's joints on every side of every block round the spawn; the built paving carries its frame's
  coordinates on the walks and behind them; the tile is two flags by two with a joint round each). Pictures: `TrafficCapture`
  (`streets-1-sidewalk*.png`, `streets-2-corner*.png`).
- Not built yet: the gutter pan in the road, the yellow dome mats, red kerb by the hydrants.
