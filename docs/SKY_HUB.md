# The sky hub

Owner, 2026-11-02: "lets make the hub. start with a giant circular sky island as high as the clouds. with a giant spiral staircase leading up to it that
is the size of the circumference of the round island and spirals around as it leads between the island and floor. make the bottom of the staircase lead
to a flat bit on the floor not clipping into any buildings or roads."

What the hub is for, and the guards, the safe zone and the rest still to come: `GAME_DESIGN.md`, "The sky hub". This is the shape only: nothing on the island
yet, no safe zone, no guards.

## The numbers (`World/Plan/SkyHubPlan`)

| | |
|---|---|
| Island | round, 600 m across (`IslandRadius` 300), its top 1500 m over the stair's foot: `CloudLayer.HeightMetres`, where the game's clouds are (a test holds them together) |
| Stair | 30 m wide, just outside the island's edge all the way, so it is the size of the island's circumference; 1.5 turns round; 6667 steps of 22.5 cm (the height of the city's and Old Guy's), 44 cm deep; 27 degrees |
| The climb | 2970 m along. At a walk (4 m/s) about 12.5 minutes, at a sprint (7.5 m/s) about 6.5 if stamina lasted. Fewer turns would be steeper and shorter: 1 turn is 37 degrees, past the 33 the city's stairs keep to. `StairTurns` changes it |
| Landing | 40 x 38 m of flat paving: 24 m before the first step, then 16 m on under the stair, 4 m clear each side, standing a small step up from the pavement |
| Walls | waist high (1.3 m on a 2.6 m body) along both sides of the stair and round the island, open only where the stair comes up. A body can jump them: nothing stops that |

The stair has no supports: nothing comes down from it to the city. It is the sky hub; that is the magic. Say if it should have piers.

## Where it stands

A pure function of the world seed (`SkyHubPlan.Find`, run from `PlanWorld`), so every machine puts it in the same place and nothing is sent.

- **The landing** is the nearest to the spawn that is a lot inside one block's lot line (so on no road), its long side on a street's lot line (so a body walks onto it from the pavement), flat to within 0.5 m, dry, clear of the expressways and Old Guy's lot. Blocks that are a plaza, a mall, enclose another block or have a stairway through them are passed over. The stair runs along the street, curving either way.
- **The spawn and Old Guy's lot stay outside the ring**, 15 m past the stair's outer edge, so the fall from the sky never meets the stair or the island.
- **The ground and the expressways:** past the landing the stair's underside stays at least 6 m over the ground under its whole width (a hill fails the spot), and no expressway runs under it for its first 120 m (64 m up).
- **Buildings:** a generated building comes out, its whole lot, if it stands within 2 m of the landing or its top comes within 10 m of the stair's underside anywhere the stair passes over it (`SkyHubPlan.Overlaps`, in `PlanWorld.WithoutLotsUnder`). Where the stair is high enough it simply passes over them. Footbridges keep off the landing and the stair's first 190 m (`BlocksABridge`).

World seed 81565117 -> 95998845 for it (`tools/cityplan tutorialseeds` now wants the house, the hub and enough hills; `tools/cityplan tutoriallot <seed>` prints where each lands): 88% of the driveway in sun, 19% of the city on a hill (a flat spawn can come with a flat city, and 52736359, tried first, had too few: `HillTests`), the house 24 m from the spawn junction, the hub's landing 51 m from the spawn, the island's middle 349 m away. Other seeds tried (52736359, 63286984, 78228768, 45823786) each failed a few of the generator's own tests, one case in thousands and nothing to do with the hub: a bridge bay under half a bay wide, 14 stripes of road paint 2 cm off the road, a wing door opening into a building, a blank wall with nothing against it, a road running through a basement, a garage mouth off flush with the road. They are real corner cases in the generator, still there for whichever seed meets them. This seed has one the suite now steps round (`GarageDriveInPlayTests`): garage 3317, on a steep hillside, has an opening with no road in front and the paving outside a metre over its deck, so a body is too tall to walk in by it; its other opening works. The classic city the tests use finds a hub or does not; the tests that care pin the game's own scale.

## How it is built (`View/Buildings/SkyHubBuilder`, `SkyHubSite`)

`CityStreamer.Rebuild` stands it (`SkyHubSite.PlaceOn`) when the world is made, built whole and kept, as Old Guy's house is: not streamed by chunk. 24 ms at load (cooking included), 36,000 triangles in 56 pieces: the stair in lengths of 128 steps, each drawn, culled and collided alone. Everything solid collides, steps as real steps, as the city's stairs do; the rock too, for whoever flies into it.

The rock's rings stay level, craggy only in how far out each point stands, and each face looks the way its corners run: with uneven heights, or a guess at each face's way, rings folded and faces turned away, and the sky showed through (seen in the first renders).

## Seen in the renders

`SkyHubCapture` (`Logs/CityShots/skyhub-*.png`). At this height the game's distance haze reaches the island: from the street its underside is a pale disc with the stair's ring round it, and the rock's shape is mostly lost until you are up there. From the stair and the island the city below goes into haze the same way.

The black-line pass draws some of the lower steps' edges as broken dashes when seen from just below. Not chased yet.

## Tests

`SkyHubPlanTests` (cloud height, the stair's size and steps, the landing flat and off the roads, no building, hill or expressway in the way, spawn and house outside the ring, every machine the same), `SkyHubPlayTests` (a real body walks off the landing and climbs at the stair's slope, walks from the top onto the island, the walls keep a walking body on, the rock's faces look outward, every piece collides).
