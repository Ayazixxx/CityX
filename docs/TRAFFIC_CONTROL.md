# Traffic lights and STOP signs

Owner, 2026-11-05: "lets improve our city, i think its a good time to add realistically placed traffic lights and stop signs throughout the city. research how stop signs vs traffic lights are utilized in real life. and realistically time the traffic lights like how they are in american cities." Built by Fable 5.1, as the owner asked on 2026-10-16.

## What American cities do (researched 2026-11-05)

The rulebook is the federal [MUTCD](https://mutcd.fhwa.dot.gov/htm/2009/part2/part2b.htm). It decides by traffic counts:

- **Lights** need heavy traffic on both roads: about [500 to 600 vehicles an hour on the main road and 150 to 200 on the side road, for 8 hours of the day](https://mutcd.fhwa.dot.gov/htm/2009/part4/part4c.htm) (Warrant 1), or many walkers, or crashes. Meeting a warrant "shall not in itself require" lights.
- **STOP on the side street** where it meets a through street, [above about 6,000 vehicles a day on the through street](https://mutcd.fhwa.dot.gov/htm/2009/part2/part2b.htm) (2B.06). The main street never stops.
- **All-way stop** where the two streets carry [about the same traffic](https://intrans.iastate.edu/app/uploads/2019/11/TSIS_faq20l.pdf): at least 300 vehicles an hour on one and 200 on the other for 8 hours (2B.07). An ALL WAY plate hangs under each sign.
- **Nothing** on the quietest corners: the left car yields to the right (2B.04).
- **Spacing**: lights on a main road are kept apart, [a quarter to half a mile where they can be](https://epg.modot.org/index.php/940.6_Traffic_Signal_Spacing), and a coordinated line of them [not under 1,000 ft](https://mutcd.fhwa.dot.gov/htm/2009/part4/part4c.htm). Dense downtowns go down to a block or two.
- **Timing**: [60 to 90 s cycles on fixed timers are the urban ideal](https://nacto.org/publication/urban-street-design-guide/intersection-design-elements/traffic-signals/signal-cycle-lengths/) (NACTO). Yellow [3 to 6 s, longer on faster roads; an all-red after it](https://mutcd.fhwa.dot.gov/htm/2009/part4/part4d.htm) (4D.26). Lights down one road are offset so a car at the road's speed meets green after green.
- **Walk signals**: [7 s of WALK, then a flashing hand long enough to cross at 3.5 ft/s](https://mutcd.fhwa.dot.gov/htm/2009/part4/part4e.htm), mounted 7 to 10 ft up (4E.05, 4E.06).
- **Late-night flashing**: yellow to the main road, red to the side street, [50 to 60 flashes a minute](https://mutcd.fhwa.dot.gov/htm/2009/part4/part4d.htm) (4D.28, 4D.30).
- **Hardware**: red on top; [two or more heads facing each approach, 40 to 180 ft beyond its stop line](https://mutcd.fhwa.dot.gov/htm/2009/part4/part4d.htm) (so on the far side), 15 ft or more over the road or 8 to 19 ft on a pole (4D.11 to 4D.16). A STOP sign stands [on the right, before the crossing](https://mutcd.fhwa.dot.gov/htm/2009/part2/part2b.htm) (2B.10), 30 in across, 36 in facing more than one lane.

From memory, not checked: mounting a sign 7 ft up where people walk (2A.18), and the stop line 4 ft before the crossing (3B.16).

## Which junction gets what (`World/TrafficPlan.cs`, pure)

The game has no traffic counts. A road's class (street, arterial, boulevard) and how built-up the place is (`PlanFields.Population`) stand for them. The thresholds are mine.

| Junction | What it gets |
|---|---|
| Two main roads (arterial or boulevard) meet | Lights, always |
| A side street crosses a main road | Lights where they can stand far enough from other lights: 400 m on a quiet stretch (1,000 ft at the city's 1.3x) down to 170 m in the core. Otherwise STOP on the side street |
| A side street ends at a main road | STOP on the side street (lights only in the core, spaced) |
| A street ends at another street | STOP on the one that ends |
| Two side streets cross, core (population 0.80 or more) | Lights 150 m or more from other lights, otherwise an all-way stop |
| Two side streets cross, busy (0.65 or more) | All-way stop |
| Two side streets cross, elsewhere | STOP on one street, which one alternating from block to block (the "basket weave") |
| Population under 0.30, side streets only | Nothing |

In this city (seed 95998845) 254 of 1,834 junctions have lights (14%), 160 are all-way stops, 1,287 have a STOP on the side street and 133 have nothing (`tools/cityplan traffic` counts them and draws the map). American cities light roughly a tenth to a quarter of theirs. The spawn stands at lights.

## How the lights are timed (`World/SignalTiming.cs`, pure)

Fixed time: the main street's green, yellow, all-red, then the side street's.

| | |
|---|---|
| Cycle | 90 s on main roads, 60 s where two side streets cross |
| Yellow | 3 s street, 3.5 s arterial, 4 s boulevard |
| All-red | 1 to 2 s, by the width of the road being cleared |
| Greens | what is left, shared by class (a boulevard gets more than a street), neither shorter than 7 s of WALK plus the time to cross the other road at 1.39 m/s (3.5 ft/s at 1.3x) |
| WALK | with the green alongside; the hand flashes for the crossing time up to that green's end |
| Green wave | each junction's cycle is offset by its distance down the main road, measured along the road round its bends, at 20.3 m/s on a boulevard, 17.4 on an arterial, 14.5 on a street (35, 30 and 25 mph at 1.3x). One way down the road |
| Late night | where a side street meets a bigger road or another side street, the lights flash once a second while the sun is well down: yellow to the main street, red to the side |

The lights at the spawn: 90 s; main road 50 s green, 3.5 yellow, 1 all-red; side street 31 s green, 3 yellow, 1.5 all-red.

## What stands on the street (`View/Streets`)

| Piece | Where |
|---|---|
| STOP sign | on the right of its approach at the stop line, 1 m across (1.2 m on a main road), its bottom 2.8 m up, lettered; an ALL WAY plate under it at an all-way stop. `TrafficProps.AddStopSign` |
| Stop line | white, half a metre wide, across the incoming half of the road, 1.6 m before the zebra. `CitySurfaceBuilder.StopLine` |
| Zebra | at lights, on every leg. Not across the street that runs free at a side-street stop. Elsewhere as before (most corners). `CitySurfaceBuilder.HasCrossing` |
| Poles | one at each kerb corner that carries something, 0.9 m behind the kerb; where a corner is missing (a street ending at another, roads meeting at a slant) a pole of its own on the far pavement or at the crossing's end. Nothing stands in a road, on Old Guy's land, or over a drop (a bridge's end): that piece is left out. `CitySurfaceBuilder.Traffic.cs` |
| Heads | on the FAR side from the traffic they face: one on the pole at each far corner; on a main road two more over the lanes on a mast arm, with backplates, their bottoms 6.1 m up. Red over yellow over green, visors |
| Walk signals | at each end of every crossing, facing along it to those waiting at the other end, 3 m up: a white walking figure, an orange hand |
| Lamps | all of a chunk's in one mesh and one draw (`SignalLampBuffer`), lit by the shader `CityX/SignalLamp` from the clock, with a soft glow that is plainer by night |

Everything is drawn in boxes, as the city's other street furniture is. It is in the near version of a chunk only.

## Every player sees the same light

A lamp is a pure function of the plan (which is the seed's) and one clock (`World/SignalClock.cs`). In a session the clock is the session's own time (`NetworkedSignalClock`: FishNet's tick, which every machine keeps in step with the server's). Alone it is this machine's. Nothing is sent per light.

Late-night flashing follows each machine's own sky: the time of day is not yet shared between players, so two players can disagree about whether it is night at all. That is a gap of the day and night cycle, not of the lights.

## Not built

- Nothing drives, and civilians do not wait for the walk signal: the lights are the street's, not yet anyone's rule.
- Where an expressway's ramps meet the streets. At a bridge's end the pieces that would stand over the trench are left out.
- Turn arrows, countdown numbers, push buttons, street-name signs on the mast arms.
- The exact switch into and out of flashing (4D.29, 4D.31): the lights just change.

## Tests

`TrafficPlanTests` (EditMode, the real city: which junction gets what, spacing, the weave, cycles, yellows, never two greens, WALK times, the green wave, the clock), `TrafficHardwareTests` (EditMode, the chunks: two or more heads on the far side of every approach, red on top, walk signals at both ends of each crossing, signs on the right facing their traffic, nothing standing in the road, stop lines before crossings), `TrafficPlayTests` (the real scene has its lamps on the shared clock; two machines over real sockets read the same clock). Pictures: `TrafficCapture` (`Logs/CityShots/traffic-*.png`), taken in the game's own city: the tests run on the classic one, whose roads are narrower. `tools/cityplan traffic [seed] [out.png]` counts and maps the junctions with no Unity, in a second: the way to try a threshold.
