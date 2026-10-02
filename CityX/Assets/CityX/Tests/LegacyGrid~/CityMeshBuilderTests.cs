using System.Collections.Generic;
using System.Linq;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The geometry itself.
    ///
    /// Screenshots are the point of this phase, but a screenshot is a bad way
    /// to establish whether a retaining wall was generated at all -- buildings
    /// hide most of the ground, so "I cannot see a problem" and "there is no
    /// problem" look identical. These check the things a picture is bad at,
    /// and leave the picture to answer whether it looks right.
    /// </summary>
    public class CityMeshBuilderTests
    {
        private const int Seed = 20260810;

        private static void Build(int chunkX, int chunkY, out MeshBuffer ground, out MeshBuffer structures)
        {
            Build(chunkX, chunkY, out ground, out _, out structures);
        }

        private static void Build(int chunkX, int chunkY,
            out MeshBuffer ground, out MeshBuffer pavement, out MeshBuffer structures)
        {
            Build(chunkX, chunkY, out ground, out pavement, out structures, out _, out _);
        }

        private static void Build(int chunkX, int chunkY, out MeshBuffer ground,
            out MeshBuffer pavement, out MeshBuffer structures,
            out MeshBuffer litGlass, out MeshBuffer darkGlass)
        {
            CityWorld world = new CityWorld(Seed);
            SkybridgeLayout bridges = new SkybridgeLayout(Seed);
            ground = new MeshBuffer();
            pavement = new MeshBuffer();
            structures = new MeshBuffer();
            litGlass = new MeshBuffer();
            darkGlass = new MeshBuffer();
            CityMeshBuilder.BuildChunk(world, bridges, chunkX, chunkY, Vector3.zero,
                ground, pavement, structures, litGlass, darkGlass);
        }

        private static void BuildStreetEdge(int chunkX, int chunkY,
            out MeshBuffer pavement, out MeshBuffer guardRail)
        {
            BuildStreetEdge(chunkX, chunkY, out pavement, out guardRail, out _);
        }

        private static void BuildStreetEdge(int chunkX, int chunkY,
            out MeshBuffer pavement, out MeshBuffer guardRail, out MeshBuffer ground)
        {
            CityWorld world = new CityWorld(Seed);
            SkybridgeLayout bridges = new SkybridgeLayout(Seed);
            ground = new MeshBuffer();
            pavement = new MeshBuffer();
            var structures = new MeshBuffer();
            guardRail = new MeshBuffer();
            CityMeshBuilder.BuildChunk(world, bridges, chunkX, chunkY, Vector3.zero,
                ground, pavement, structures, guardRail: guardRail);
        }

        private static bool HasVertexNear(MeshBuffer buffer, float y, float tolerance = 0.05f)
        {
            foreach (Vector3 v in buffer.Vertices)
            {
                if (Mathf.Abs(v.y - y) <= tolerance)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasVertexAbove(MeshBuffer buffer, float y)
        {
            foreach (Vector3 v in buffer.Vertices)
            {
                if (v.y >= y)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Mirrors CityMeshBuilder's own private FloorDiv -- integer / rounds toward zero, not floor, and chunk coordinates can be negative.</summary>
        private static int FloorDivForTest(int a, int b)
        {
            int q = a / b;
            if (a % b != 0 && (a < 0) != (b < 0))
            {
                q--;
            }
            return q;
        }

        /// <summary>
        /// True if some vertex near height y has a genuinely downward-
        /// facing normal -- proof a face is meant to be seen from
        /// underneath, not just from above.
        /// </summary>
        private static bool HasDownwardNormalNear(MeshBuffer buffer, float y, float yTolerance)
        {
            for (int i = 0; i < buffer.Vertices.Count; i++)
            {
                if (Mathf.Abs(buffer.Vertices[i].y - y) <= yTolerance && buffer.Normals[i].y < -0.1f)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The height of the deck at world Z, found among the real vertices
        /// sitting there -- the HIGHEST one within a generous window of
        /// approxY, not merely the first one encountered.
        ///
        /// EXPLICIT BUG, found live: the ordinary tilted street's own edge
        /// sits at that exact same Z too (chunk corners are shared exactly,
        /// by design), and its real height is often still within a window
        /// wide enough to also catch the deck (a span's own flanking ends
        /// are only guaranteed at least ElevatedBridgeMinDip below the
        /// deck, not the whole window's radius). Returning whichever
        /// vertex happened to be inserted first made this compare the
        /// ORDINARY STREET's height in one chunk against the DECK's height
        /// in its neighbour -- two genuinely different surfaces that were
        /// never meant to match at all -- and reported that as "a gap."
        /// The deck is elevated BY DEFINITION, so the true deck vertex
        /// among any candidates at the same Z is always the highest one.
        /// </summary>
        private static float HeightOfVertexNearZ(MeshBuffer buffer, float z, float approxY)
        {
            const float zTolerance = 0.05f;
            const float yWindow = 15.0f;
            float best = float.NaN;
            foreach (Vector3 v in buffer.Vertices)
            {
                if (Mathf.Abs(v.z - z) <= zTolerance && Mathf.Abs(v.y - approxY) <= yWindow)
                {
                    if (float.IsNaN(best) || v.y > best)
                    {
                        best = v.y;
                    }
                }
            }
            return best;
        }

        /// <summary>
        /// Same as <see cref="HasVertexNear"/>, but also confined to an X/Z
        /// footprint -- needed for anything checking a BUILDING chunk's own
        /// pavement/flatConcrete buffer specifically, since (see
        /// PavementRunsAlongStreetsAndStopsAtJunctions's own doc comment)
        /// that buffer also carries the building's own roof and interior
        /// floors/stairs at a wide spread of heights, which a bare height
        /// check cannot tell apart from the thing actually being tested.
        /// </summary>
        private static bool HasVertexNearInRegion(MeshBuffer buffer, float y, Rect xzRegion,
            float tolerance = 0.05f)
        {
            foreach (Vector3 v in buffer.Vertices)
            {
                if (Mathf.Abs(v.y - y) <= tolerance && xzRegion.Contains(new Vector2(v.x, v.z)))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Pavement runs along streets and stops at junctions -- EXCEPT the
        /// four small corner squares AddSidewalkCorners now deliberately
        /// fills wherever a junction's own diagonal neighbour is a building
        /// (see that method's own doc comment: without them, the sidewalk
        /// coming from one arm and the sidewalk coming from the other arm
        /// never actually meet, leaving a bare notch of road right at the
        /// corner -- a real reported bug). The CROSSING ITSELF -- everywhere
        /// outside those four corner squares -- must still never carry
        /// pavement, which is the actual invariant worth pinning: it is not
        /// written as a special case anywhere, it falls out of the street
        /// grid, and it would silently stop being true if the street period
        /// ever changed.
        ///
        /// ONLY STREET CHUNKS ARE INSPECTED, deliberately. The flat-concrete
        /// buffer also carries roofs and walkway decks, so a buildable chunk
        /// having something in it proves nothing either way -- asserting on it
        /// would just be asserting that the chunk has a building.
        /// </summary>
        [Test]
        public void PavementRunsAlongStreetsAndStopsAtJunctions()
        {
            int junctions = 0;
            int runs = 0;
            float chunk = CityLayout.ChunkSize;
            float sw = CityMeshBuilder.SidewalkWidth;

            for (int x = -24; x <= 24; x++)
            {
                for (int y = -24; y <= 24; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    Build(x, y, out _, out MeshBuffer flatConcrete, out _);

                    bool junction = CityLayout.IsStreetChunk(x - 1, y) && CityLayout.IsStreetChunk(x + 1, y) &&
                                    CityLayout.IsStreetChunk(x, y - 1) && CityLayout.IsStreetChunk(x, y + 1);
                    if (junction)
                    {
                        junctions++;
                        float x0 = x * chunk, z0 = y * chunk, x1 = x0 + chunk, z1 = z0 + chunk;
                        const float margin = 0.01f;
                        Rect nw = Rect.MinMaxRect(x0 - margin, z0 - margin, x0 + sw + margin, z0 + sw + margin);
                        Rect ne = Rect.MinMaxRect(x1 - sw - margin, z0 - margin, x1 + margin, z0 + sw + margin);
                        Rect sw2 = Rect.MinMaxRect(x0 - margin, z1 - sw - margin, x0 + sw + margin, z1 + margin);
                        Rect se = Rect.MinMaxRect(x1 - sw - margin, z1 - sw - margin, x1 + margin, z1 + margin);
                        foreach (Vector3 v in flatConcrete.Vertices)
                        {
                            Vector2 xz = new Vector2(v.x, v.z);
                            bool inCornerSquare =
                                nw.Contains(xz) || ne.Contains(xz) || sw2.Contains(xz) || se.Contains(xz);
                            Assert.IsTrue(inCornerSquare,
                                $"({x},{y}) is a junction -- pavement at ({v.x:0.00},{v.z:0.00}) sits outside " +
                                "any real corner square and would block the crossing");
                        }
                    }
                    else
                    {
                        runs++;
                        Assert.IsFalse(flatConcrete.IsEmpty,
                            $"({x},{y}) is a street beside buildings but has no pavement");
                    }
                }
            }

            TestContext.WriteLine($"{runs} street runs with pavement, {junctions} junctions without");
            Assert.Greater(junctions, 20, "not enough junctions sampled");
            Assert.Greater(runs, 100, "not enough street runs sampled");
        }


        /// <summary>
        /// No building wall may carry the roof. Directly guards the thing that
        /// looked worst in the first textured render -- window texture printed
        /// across the top of every building.
        ///
        /// THE RULE IS "NOT OVER THE FOOTPRINT", not "nothing facing up", and
        /// the difference is a fin's top cap. Facade ribs stand proud of the
        /// wall and are capped, so they put a few upward-facing slivers in this
        /// buffer, entirely OUTSIDE the plan of the building. Loosening this to
        /// tolerate small upward faces anywhere would have let a real roof
        /// through the day one got split into small triangles; testing where
        /// they are instead separates the two exactly.
        /// </summary>
        [Test]
        public void BuildingRoofsAreNotPartOfTheFacade()
        {
            CityWorld world = new CityWorld(Seed);
            int checkedCount = 0;

            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }
                    checkedCount++;

                    Build(x, y, out _, out _, out MeshBuffer structures);
                    for (int t = 0; t < structures.Triangles.Count; t += 3)
                    {
                        int a = structures.Triangles[t];
                        if (structures.Normals[a].y < 0.5f)
                        {
                            continue;
                        }

                        Vector3 centre = (structures.Vertices[a] +
                                          structures.Vertices[structures.Triangles[t + 1]] +
                                          structures.Vertices[structures.Triangles[t + 2]]) / 3.0f;
                        bool overThePlan =
                            centre.x > site.MinX + 0.001 && centre.x < site.MaxX - 0.001 &&
                            centre.z > site.MinZ + 0.001 && centre.z < site.MaxZ - 0.001;
                        Assert.IsFalse(overThePlan,
                            $"({x},{y}) has an upward-facing facade surface at {centre} inside its own " +
                            "footprint -- a roof got the window texture");
                    }
                }
            }

            Assert.Greater(checkedCount, 100, "not enough buildings sampled");
        }

        [Test]
        public void EveryChunkGetsGroundAtItsOwnPadHeight()
        {
            CityWorld world = new CityWorld(Seed);
            for (int x = -6; x <= 6; x++)
            {
                for (int y = -6; y <= 6; y++)
                {
                    Build(x, y, out MeshBuffer ground, out _);
                    Assert.IsFalse(ground.IsEmpty, $"chunk ({x},{y}) produced no ground at all");

                    if (CityLayout.IsStreetChunk(x, y))
                    {
                        // A street's own ground now tilts to the smooth
                        // continuous field instead of sitting flat at the
                        // quantised pad -- see BuildGround's own "hillside
                        // streets" comment. What still has to hold: the
                        // ground actually reaches each of the chunk's own
                        // four corner heights from that same field, the
                        // same four corners AddTiltedStreetGround builds
                        // its quad from.
                        float chunk = CityLayout.ChunkSize;
                        float minX = x * chunk;
                        float minZ = y * chunk;
                        float[] corners =
                        {
                            (float)world.Terrain.ContinuousHeightAt(minX, minZ),
                            (float)world.Terrain.ContinuousHeightAt(minX + chunk, minZ),
                            (float)world.Terrain.ContinuousHeightAt(minX + chunk, minZ + chunk),
                            (float)world.Terrain.ContinuousHeightAt(minX, minZ + chunk),
                        };
                        foreach (float corner in corners)
                        {
                            Assert.IsTrue(HasVertexNear(ground, corner),
                                $"street chunk ({x},{y}) has no ground vertex near its own corner " +
                                $"height of {corner}m");
                        }
                        continue;
                    }

                    float pad = world.Terrain.PadHeightFor(x, y);
                    int atPad = 0;
                    foreach (Vector3 v in ground.Vertices)
                    {
                        if (Mathf.Abs(v.y - pad) < 0.001f)
                        {
                            atPad++;
                        }
                    }
                    Assert.GreaterOrEqual(atPad, 4,
                        $"chunk ({x},{y}) has no surface sitting at its pad height of {pad}m");
                }
            }
        }

        /// <summary>
        /// The one a picture genuinely cannot answer. A missing retaining wall
        /// is a slot of daylight along a terrace edge, visible only if you
        /// happen to be standing at the right spot on the low side.
        ///
        /// Checks all 8 neighbours, not just the 4 cardinal ones. A diagonal
        /// neighbour is never bounded by the cardinal slope limit the same
        /// way a direct one is (it can compound two independent cardinal
        /// drops), so a corner can need a wall even when neither flanking
        /// edge does -- see AddCornerSkirtIfLower's own doc comment.
        /// </summary>
        [Test]
        public void ATerraceEdgeAlwaysGetsARetainingWall()
        {
            CityWorld world = new CityWorld(Seed);
            int edgesFound = 0;

            // WIDENED FROM -30..30: TerrainField.RegionFlatnessFactor now
            // clusters large flat regions among hilly ones (EXPLICIT ASK,
            // "make the steepness and flat parts come in clusters"), each
            // spanning many chunks (RegionWavelength ~2700m, well over 100
            // chunks) -- the origin can land inside one of those flat
            // clusters by chance, and the old -30..30 sweep (1464m) could
            // sit entirely inside it, finding too few real terrace edges to
            // mean anything. -60..60 (2880m) comfortably exceeds one full
            // RegionWavelength, so it always crosses at least one
            // flat-to-hilly transition regardless of where the origin
            // happens to fall.
            // A chunk's real height at a given world point -- ContinuousHeightAt
            // if it is a street (the same tilted surface its own ground
            // actually sits on), PadHeightFor if it is a building (flat, so
            // that already IS its real height). Exactly
            // CityMeshBuilder.NeighbourGroundHeightAt, duplicated rather than
            // exposed, since this is also what "this chunk's own real edge
            // height" needs now -- see that method's own doc comment for the
            // gap this closes: two QUANTISED pads used to be compared
            // instead of this, which could agree ("no drop") while the
            // street's real, continuously-tilted surface still sat away
            // from the building's flat one by however much the tilt had
            // carried it.
            float RealHeightAt(int cx, int cy, float worldX, float worldZ) =>
                CityLayout.IsStreetChunk(cx, cy)
                    ? (float)world.Terrain.ContinuousHeightAt(worldX, worldZ)
                    : world.Terrain.PadHeightFor(cx, cy);

            float chunkSize = CityLayout.ChunkSize;

            for (int x = -60; x <= 60; x++)
            {
                for (int y = -60; y <= 60; y++)
                {
                    float pad = world.Terrain.PadHeightFor(x, y);
                    float x0 = x * chunkSize, z0 = y * chunkSize;
                    float x1 = x0 + chunkSize, z1 = z0 + chunkSize;

                    // The exact same corner each cardinal edge's own gate
                    // uses (AddSkirtIfLower's top0) and each diagonal's own
                    // corner post uses (AddCornerSkirtIfLower's corner) --
                    // see their call sites in BuildGround. A street-vs-street
                    // pair needs no separate exclusion here the way
                    // BuildGround's own westToStreet/etc. skip it: both
                    // sides evaluate ContinuousHeightAt at the literal same
                    // world point, so they always come out exactly equal,
                    // which already fails the strict "<" eligibility check
                    // below on its own.
                    var checks = new (int dx, int dy, float px, float pz)[]
                    {
                        (-1, 0, x0, z0),
                        (1, 0, x1, z1),
                        (0, -1, x1, z0),
                        (0, 1, x0, z1),
                        (-1, -1, x0, z0),
                        (1, -1, x1, z0),
                        (-1, 1, x0, z1),
                        (1, 1, x1, z1),
                    };

                    float lowestEligibleNeighbour = float.MaxValue;
                    bool anyEligible = false;
                    foreach (var (dx, dy, px, pz) in checks)
                    {
                        float thisHeight = RealHeightAt(x, y, px, pz);
                        float neighbourHeight = RealHeightAt(x + dx, y + dy, px, pz);
                        if (neighbourHeight < thisHeight)
                        {
                            anyEligible = true;
                            lowestEligibleNeighbour = Mathf.Min(lowestEligibleNeighbour, neighbourHeight);
                        }
                    }

                    Build(x, y, out MeshBuffer ground, out _);

                    float lowestVertex = float.MaxValue;
                    foreach (Vector3 v in ground.Vertices)
                    {
                        if (v.y < lowestVertex)
                        {
                            lowestVertex = v.y;
                        }
                    }

                    // A street chunk's own ground is tilted to the smooth
                    // continuous field now (see BuildGround's "hillside
                    // streets" comment), so with no skirt built anywhere its
                    // own natural floor is whichever of its own four corners
                    // sits lowest, not the flat quantised pad a building
                    // sits on.
                    float ownFloor = pad;
                    if (CityLayout.IsStreetChunk(x, y))
                    {
                        ownFloor = Mathf.Min(
                            Mathf.Min((float)world.Terrain.ContinuousHeightAt(x0, z0),
                                      (float)world.Terrain.ContinuousHeightAt(x1, z0)),
                            Mathf.Min((float)world.Terrain.ContinuousHeightAt(x1, z1),
                                      (float)world.Terrain.ContinuousHeightAt(x0, z1)));
                    }

                    if (anyEligible)
                    {
                        edgesFound++;
                        Assert.LessOrEqual(lowestVertex, lowestEligibleNeighbour + 0.001f,
                            $"chunk ({x},{y})'s geometry stops at {lowestVertex}m, short of the " +
                            $"{lowestEligibleNeighbour}m real ground beside it -- there is a gap you can see through");
                    }
                    else
                    {
                        Assert.AreEqual(ownFloor, lowestVertex, 0.001f,
                            $"chunk ({x},{y}) has no lower neighbour but still built a skirt down to " +
                            $"{lowestVertex}m -- that is a wall buried inside solid ground");
                    }
                }
            }

            TestContext.WriteLine($"{edgesFound} terrace edges checked");
            Assert.Greater(edgesFound, 100, "not enough terrace edges in the sweep to mean anything");
        }

        /// <summary>
        /// EXPLICIT BUG REPORT, WITH SCREENSHOTS: "we got to start bridging
        /// this gaps between the roads and the buildings there is just
        /// empty space." The bug this proves is fixed: AddSkirtIfLower used
        /// to gate on two QUANTISED pads agreeing -- a street chunk and the
        /// building lot next to it can sit on the exact same terrace level
        /// while the street's own ground, now continuously tilted (see
        /// BuildGround's "hillside streets" comment), still sits away from
        /// the building's flat one by however much that tilt carried it.
        /// The old gate never saw that: same pad meant "no wall," full
        /// stop, regardless of what the real, rendered surfaces actually
        /// did. Sweeps for a real occurrence of exactly that -- a street
        /// chunk whose west neighbour is a building on the identical
        /// quantised pad, but whose real edge height at the shared corner
        /// differs from it by a real margin -- and confirms the built
        /// ground now reaches all the way down to close it.
        /// </summary>
        [Test]
        public void ASameQuantisedPadStreetAndBuildingStillGetAWallWhenTheirRealHeightsDiffer()
        {
            CityWorld world = new CityWorld(Seed);
            float chunkSize = CityLayout.ChunkSize;
            int found = 0;

            for (int x = -60; x <= 60; x++)
            {
                for (int y = -60; y <= 60; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    int nx = x - 1, ny = y;
                    if (CityLayout.IsStreetChunk(nx, ny))
                    {
                        continue;
                    }

                    float pad = world.Terrain.PadHeightFor(x, y);
                    float neighbourPad = world.Terrain.PadHeightFor(nx, ny);
                    float realHeight = (float)world.Terrain.ContinuousHeightAt(x * chunkSize, y * chunkSize);

                    // The OLD gate (neighbourPad >= pad -> no wall) says
                    // this edge is flush; the NEW one (neighbourPad >=
                    // realHeight -> no wall) says it genuinely is not, by a
                    // margin too big to be float noise.
                    bool oldGateSaysFlush = neighbourPad >= pad - 0.0001f;
                    bool newGateSaysGap = neighbourPad < realHeight - 0.05f;
                    if (!oldGateSaysFlush || !newGateSaysGap)
                    {
                        continue;
                    }

                    found++;
                    Build(x, y, out MeshBuffer ground, out _);
                    float lowestVertex = ground.Vertices.Min(v => v.y);
                    Assert.LessOrEqual(lowestVertex, neighbourPad + 0.001f,
                        $"chunk ({x},{y}) and its west neighbour both sit on the {pad}m pad, but the " +
                        $"street's own real edge height there is {realHeight}m against the building's " +
                        $"{neighbourPad}m -- built ground stops at {lowestVertex}m, which does not reach " +
                        "down far enough to close that gap");
                }
            }

            Assert.Greater(found, 0,
                "never found a same-pad, different-real-height case in this sweep -- widen it, or the " +
                "gap this fix closes cannot be shown to actually occur");
        }

        /// <summary>
        /// Wherever a street's terraced edge drops toward a BUILDING lot
        /// with a real sidewalk to carry it, there is a real flight of steps
        /// -- not just the sheer retaining wall -- reaching all the way from
        /// this chunk's own pad down to the lower one's.
        ///
        /// STREET-VS-STREET IS DELIBERATELY EXCLUDED FROM THIS SWEEP. Two
        /// neighbouring street chunks now always meet through the same
        /// continuous hillside tilt (see BuildGround's own "hillside
        /// streets" comment), so BuildGround never even calls
        /// AddStreetEdgeFeatures for that pair any more -- there is no drop
        /// left there for a stair or ramp to cover. What is left eligible
        /// for either treatment is exactly a street's edge against a
        /// BUILDING lot (see AddStreetEdgeFeatures's own doc comment).
        /// </summary>
        [Test]
        public void AStreetTerraceDropWithASidewalkGetsARealStaircaseOrASlopedRamp()
        {
            CityWorld world = new CityWorld(Seed);
            int stairCases = 0;
            int rampCases = 0;

            for (int x = -30; x <= 30; x++)
            {
                for (int y = -30; y <= 30; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    float pad = world.Terrain.PadHeightFor(x, y);
                    bool chunkAlreadySloped = false;

                    foreach ((int nx, int ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                    {
                        // Not eligible at all any more -- BuildGround's own
                        // westToStreet/etc. gating skips this pair entirely.
                        if (CityLayout.IsStreetChunk(nx, ny))
                        {
                            continue;
                        }

                        float neighbourPad = world.Terrain.PadHeightFor(nx, ny);
                        if (neighbourPad >= pad)
                        {
                            continue;
                        }

                        // Mirrors AddStreetEdgeFeatures's own roll exactly --
                        // seeded off the NEIGHBOUR's coordinate, and gated the
                        // same "at most one sloped edge per chunk" way, so the
                        // test knows in advance which of the two treatments
                        // this edge is going to get.
                        bool sloped = !chunkAlreadySloped &&
                            new DeterministicRandom(world.ChunkSeed(nx, ny)).NextDouble() <
                                CityMeshBuilder.SlopedStreetChance;
                        if (sloped)
                        {
                            chunkAlreadySloped = true;
                        }

                        BuildStreetEdge(x, y, out MeshBuffer pavement, out _, out MeshBuffer ground);

                        if (sloped)
                        {
                            rampCases++;
                            // Wide tolerance deliberately: the ramp starts
                            // flush with this street chunk's own tilted
                            // edge (ContinuousHeightAt), not this quantised
                            // pad -- see AddSlopedStreetRamp's own "top0.y,
                            // not pad" comment. TerraceStep is the
                            // worst-case gap between the two by
                            // construction (PadHeightFor floors
                            // ContinuousHeightAt to the nearest step).
                            Assert.IsTrue(HasVertexNear(ground, pad, tolerance: TerrainField.TerraceStep),
                                $"chunk ({x},{y}) drops to ({nx},{ny}) with a sloped ramp, but no vertex " +
                                $"reaches anywhere near its own pad at {pad}m");
                            Assert.IsTrue(HasVertexNear(ground, neighbourPad, tolerance: 0.6f),
                                $"chunk ({x},{y}) drops to ({nx},{ny}), but the ramp does not reach down " +
                                $"anywhere near {neighbourPad}m");
                            continue;
                        }

                        // No sidewalk-existence gate needed any more -- the
                        // neighbour here is always a BUILDING (street-vs-
                        // street is excluded above), which is exactly the
                        // condition AddSidewalks itself builds THIS edge's
                        // own sidewalk under, so it is always real. See
                        // AddStreetEdgeFeatures's own doc comment.
                        stairCases++;
                        // Wide tolerance deliberately: the stair's own top
                        // starts from wherever this street chunk's tilted
                        // sidewalk actually sits at that point
                        // (ContinuousHeightAt), not this quantised pad --
                        // see AddStreetEdgeFeatures's own "wallPoint.y, not
                        // pad" comment. TerraceStep is the worst-case gap
                        // between the two by construction (PadHeightFor
                        // floors ContinuousHeightAt to the nearest step).
                        Assert.IsTrue(HasVertexNear(pavement, pad, tolerance: TerrainField.TerraceStep),
                            $"chunk ({x},{y}) drops to ({nx},{ny}) with a sidewalk to carry a stair, " +
                            $"but no step reaches anywhere near its own pad at {pad}m");
                        // Same 0.6f the ramp check above allows: the chosen
                        // slice's own rise is clamped to at least one step
                        // (BuildingInterior.StairStepHeight) rather than
                        // exactly wallPoint.y - neighbourPad whenever that
                        // could go non-positive, so the flight can overshoot
                        // slightly past neighbourPad rather than landing on
                        // it to the millimetre -- see AddStreetEdgeFeatures's
                        // own "still clamped, as a last resort" comment.
                        Assert.IsTrue(HasVertexNear(pavement, neighbourPad, tolerance: 0.6f),
                            $"chunk ({x},{y}) drops to ({nx},{ny}), but no step reaches down to " +
                            $"{neighbourPad}m -- the flight stops short of the lower level");
                    }
                }
            }

            TestContext.WriteLine($"{stairCases} staircases, {rampCases} sloped ramps checked");
            Assert.Greater(stairCases, 0, "no street-vs-building staircase drop in the sweep");
            Assert.Greater(rampCases, 0, "no street-vs-building sloped ramp drop in the sweep");
        }

        /// <summary>
        /// EXPLICIT BUG REPORT, with a screenshot: "make the sidewalks
        /// sloped with the roads" -- a staircase was reported floating in
        /// mid-air over a sloped street. AddSidewalks used to build a plain
        /// flat MeshBuffer.AddBox at the chunk's own quantised PadHeightFor,
        /// while the real street ground next to it (and the stairs/guard-
        /// rails sitting on the sidewalk) already tilted to the continuous
        /// field -- confirms directly that each sidewalk strip's own outer
        /// corner now sits at the real ContinuousHeightAt (plus KerbHeight),
        /// not the quantised pad, the same "own corner height" pattern
        /// EveryChunkGetsGroundAtItsOwnPadHeight already checks for the
        /// street ground itself.
        /// </summary>
        [Test]
        public void SidewalksFollowTheRealTiltedGroundNotTheQuantisedPad()
        {
            CityWorld world = new CityWorld(Seed);
            float chunk = CityLayout.ChunkSize;
            int checkedSides = 0;

            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }

                    Build(x, y, out _, out MeshBuffer pavement, out _);

                    float x0 = x * chunk;
                    float z0 = y * chunk;
                    float x1 = x0 + chunk;
                    float z1 = z0 + chunk;

                    void CheckCorner(float cx, float cz)
                    {
                        float expected =
                            (float)world.Terrain.ContinuousHeightAt(cx, cz) + CityMeshBuilder.KerbHeight;
                        Assert.IsTrue(HasVertexNear(pavement, expected, tolerance: 0.05f),
                            $"sidewalk at chunk ({x},{y})'s corner ({cx},{cz}) has no vertex near its own " +
                            $"real tilted height of {expected:0.00}m -- still sitting at a flat/quantised height");
                    }

                    if (!CityLayout.IsStreetChunk(x - 1, y))
                    {
                        checkedSides++;
                        CheckCorner(x0, z0);
                        CheckCorner(x0, z1);
                    }
                    if (!CityLayout.IsStreetChunk(x + 1, y))
                    {
                        checkedSides++;
                        CheckCorner(x1, z0);
                        CheckCorner(x1, z1);
                    }
                    if (!CityLayout.IsStreetChunk(x, y - 1))
                    {
                        checkedSides++;
                        CheckCorner(x0, z0);
                        CheckCorner(x1, z0);
                    }
                    if (!CityLayout.IsStreetChunk(x, y + 1))
                    {
                        checkedSides++;
                        CheckCorner(x0, z1);
                        CheckCorner(x1, z1);
                    }
                }
            }

            Assert.Greater(checkedSides, 0, "no sidewalk-bearing street edge in the sweep");
        }

        /// <summary>
        /// EXPLICIT BUG REPORT, with a screenshot: a bare square notch of
        /// road left uncovered right at a street corner, between the two
        /// sidewalk strips that should meet there. See AddSidewalkCorners'
        /// own doc comment for the full root-cause explanation -- in short,
        /// a chunk's four edge strips only cover sides that border a
        /// BUILDING, so at a real turn (two ADJACENT sides are street, not
        /// building) neither matching strip ever reaches the small square
        /// where an arm's sidewalk should meet the other arm's.
        ///
        /// Checks the corner square's OUTER point specifically (the actual
        /// chunk corner, e.g. (x0,z0) for the NW case) -- AddTiltedSidewalkBox
        /// samples that exact point through the same topAt closure the rest
        /// of this chunk's sidewalks use, so it has to land at precisely the
        /// real ContinuousHeightAt there (plus KerbHeight), the same
        /// invariant SidewalksFollowTheRealTiltedGroundNotTheQuantisedPad
        /// already checks for the ordinary edge strips.
        /// </summary>
        [Test]
        public void SidewalkCornersFillTheGapAtARealStreetTurn()
        {
            CityWorld world = new CityWorld(Seed);
            float chunk = CityLayout.ChunkSize;
            int casesFound = 0;

            for (int x = -40; x <= 40; x++)
            {
                for (int y = -40; y <= 40; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }

                    float x0 = x * chunk;
                    float z0 = y * chunk;
                    float x1 = x0 + chunk;
                    float z1 = z0 + chunk;
                    float sw = CityMeshBuilder.SidewalkWidth;

                    var corners = new[]
                    {
                        (west: x - 1, northSouth: y - 1, diag: (x - 1, y - 1), outerX: x0, outerZ: z0),
                        (west: x + 1, northSouth: y - 1, diag: (x + 1, y - 1), outerX: x1, outerZ: z0),
                        (west: x - 1, northSouth: y + 1, diag: (x - 1, y + 1), outerX: x0, outerZ: z1),
                        (west: x + 1, northSouth: y + 1, diag: (x + 1, y + 1), outerX: x1, outerZ: z1),
                    };

                    foreach (var c in corners)
                    {
                        if (!CityLayout.IsStreetChunk(c.west, y) || !CityLayout.IsStreetChunk(x, c.northSouth)
                            || CityLayout.IsStreetChunk(c.diag.Item1, c.diag.Item2))
                        {
                            continue;
                        }

                        Build(x, y, out _, out MeshBuffer pavement, out _);
                        float expected =
                            (float)world.Terrain.ContinuousHeightAt(c.outerX, c.outerZ) + CityMeshBuilder.KerbHeight;
                        float minX = Mathf.Min(c.outerX, c.outerX + (c.outerX == x0 ? sw : -sw)) - 0.01f;
                        float maxX = Mathf.Max(c.outerX, c.outerX + (c.outerX == x0 ? sw : -sw)) + 0.01f;
                        float minZ = Mathf.Min(c.outerZ, c.outerZ + (c.outerZ == z0 ? sw : -sw)) - 0.01f;
                        float maxZ = Mathf.Max(c.outerZ, c.outerZ + (c.outerZ == z0 ? sw : -sw)) + 0.01f;
                        Rect region = Rect.MinMaxRect(minX, minZ, maxX, maxZ);

                        casesFound++;
                        Assert.IsTrue(HasVertexNearInRegion(pavement, expected, region, tolerance: 0.05f),
                            $"turn at chunk ({x},{y})'s corner ({c.outerX},{c.outerZ}) has no sidewalk vertex " +
                            "near its own real tilted height -- the corner gap was not filled");
                    }
                }
            }

            TestContext.WriteLine($"{casesFound} street-turn corners checked");
            Assert.Greater(casesFound, 0, "no real street-turn corner found in the sweep");
        }

        /// <summary>
        /// EXPLICIT ASK: "and curve" -- confirms two neighbouring curving
        /// street chunks meet EXACTLY at their shared boundary, the same
        /// "sampling the same pure function of world position from either
        /// side always agrees" guarantee AddCurvingStreetGround's own doc
        /// comment claims. AddCurvingStreetGround is the FIRST thing
        /// BuildGround ever writes to a fresh `ground` buffer for a street
        /// chunk, so a freshly-built chunk's own first (segments * 4)
        /// vertices are always exactly its ribbon's own corners, in the
        /// exact per-segment order [near0, near1, far1, far0] -- no other
        /// geometry mixed in yet to filter out.
        /// </summary>
        [Test]
        public void AdjacentCurvingChunksShareTheExactSameGroundAtTheirBoundary()
        {
            CityWorld world = new CityWorld(Seed);
            int segments = Mathf.RoundToInt(CityLayout.ChunkSize / CityMeshBuilder.StreetCurveSegmentLength);
            bool found = false;

            for (int x = -40; x <= 40 && !found; x++)
            {
                for (int y = -40; y <= 40 && !found; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y) || !CityLayout.IsStreetChunk(x, y + 1))
                    {
                        continue;
                    }
                    // Both genuinely north-south and neither a junction --
                    // StreetRunsAlongX's own doc comment says it is only
                    // meaningful for a real, non-junction street chunk.
                    if (CityLayout.StreetRunsAlongX(x, y) || CityLayout.StreetRunsAlongX(x, y + 1))
                    {
                        continue;
                    }

                    Build(x, y, out MeshBuffer groundHere, out _);
                    Build(x, y + 1, out MeshBuffer groundNext, out _);
                    if (groundHere.Vertices.Count < segments * 4 || groundNext.Vertices.Count < segments * 4)
                    {
                        continue;
                    }

                    found = true;
                    Vector3 hereNear1 = groundHere.Vertices[(segments - 1) * 4 + 1];
                    Vector3 hereFar1 = groundHere.Vertices[(segments - 1) * 4 + 2];
                    Vector3 nextNear0 = groundNext.Vertices[0];
                    Vector3 nextFar0 = groundNext.Vertices[3];

                    Assert.AreEqual(hereNear1.x, nextNear0.x, 0.001f, "west edge X disagrees across the boundary");
                    Assert.AreEqual(hereNear1.y, nextNear0.y, 0.001f, "west edge height disagrees across the boundary");
                    Assert.AreEqual(hereFar1.x, nextFar0.x, 0.001f, "east edge X disagrees across the boundary");
                    Assert.AreEqual(hereFar1.y, nextFar0.y, 0.001f, "east edge height disagrees across the boundary");
                }
            }

            Assert.IsTrue(found, "no adjacent pair of plain north-south street chunks found in the sweep");
        }

        /// <summary>
        /// EXPLICIT ASK: "and curve" -- the curve has to reach EXACTLY zero
        /// approaching a junction (StreetCurveTaperDistanceMetres's own doc
        /// comment) so cross streets still meet at clean right angles, not
        /// a lateral jump where the curve suddenly stops.
        /// </summary>
        [Test]
        public void TheCurveReachesZeroRightAtAJunction()
        {
            CityWorld world = new CityWorld(Seed);
            int segments = Mathf.RoundToInt(CityLayout.ChunkSize / CityMeshBuilder.StreetCurveSegmentLength);
            bool found = false;

            for (int x = -40; x <= 40 && !found; x++)
            {
                for (int y = -40; y <= 40 && !found; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    bool junction = CityLayout.IsStreetChunk(x - 1, y) && CityLayout.IsStreetChunk(x + 1, y) &&
                                     CityLayout.IsStreetChunk(x, y - 1) && CityLayout.IsStreetChunk(x, y + 1);
                    if (!junction)
                    {
                        continue;
                    }

                    Build(x, y, out MeshBuffer ground, out _);
                    if (ground.Vertices.Count < segments * 4)
                    {
                        continue;
                    }

                    found = true;
                    float chunk = CityLayout.ChunkSize;
                    float expectedX0 = x * chunk;
                    float expectedX1 = expectedX0 + chunk;
                    float expectedZ0 = y * chunk;
                    float expectedZ1 = expectedZ0 + chunk;

                    // ONLY the chunk's own two OUTER edges -- vertices[0]/
                    // [3] are segment 0's own a0 edge (the chunk's real
                    // near boundary), and vertices[(segments-1)*4+1]/[+2]
                    // are the LAST segment's own a1 edge (the chunk's real
                    // far boundary). vertices[1]/[2] are NOT chunk-boundary
                    // points -- EXPLICIT BUG, found live: they are segment
                    // 0's own a1 edge, only StreetCurveSegmentLength (1.5m)
                    // into the chunk, and asserting THOSE sit on the plain
                    // grid line is simply the wrong claim, whether or not
                    // the curve is actually behaving correctly.
                    //
                    // A junction's own ribbon runs along whichever axis
                    // StreetRunsAlongX happens to report for it, but every
                    // one of these real boundary corners has to sit at the
                    // plain, un-offset grid position regardless -- a
                    // junction sits at the intersection of BOTH lanes' own
                    // taper zones.
                    foreach (Vector3 v in new[]
                             {
                                 ground.Vertices[0], ground.Vertices[3],
                                 ground.Vertices[(segments - 1) * 4 + 1], ground.Vertices[(segments - 1) * 4 + 2],
                             })
                    {
                        bool onXBoundary = Mathf.Abs(v.x - expectedX0) < 0.01f || Mathf.Abs(v.x - expectedX1) < 0.01f;
                        bool onZBoundary = Mathf.Abs(v.z - expectedZ0) < 0.01f || Mathf.Abs(v.z - expectedZ1) < 0.01f;
                        Assert.IsTrue(onXBoundary && onZBoundary,
                            $"junction ({x},{y}) has a ground corner at ({v.x:0.000},{v.z:0.000}) that has " +
                            "drifted off the plain grid position -- the curve did not reach zero at the junction");
                    }
                }
            }

            Assert.IsTrue(found, "no junction chunk found in the sweep");
        }

        /// <summary>
        /// A junction is street on all four sides, and a street chunk never
        /// gets AddStreetEdgeFeatures called against ANOTHER street
        /// neighbour any more -- BuildGround's own westToStreet/etc. gating
        /// routes every such edge through the continuous hillside tilt
        /// instead (see its "hillside streets" comment), seamlessly, with
        /// nothing left to carve a stair into even where the two chunks'
        /// own quantised pads disagree. So a junction never gets a stair on
        /// any of its four sides, correctly, rather than one floating over
        /// open roadway.
        /// </summary>
        [Test]
        public void AJunctionTerraceDropGetsNoStaircase()
        {
            CityWorld world = new CityWorld(Seed);
            int casesFound = 0;

            // A junction is no longer every 4th coordinate on both axes --
            // CityLayout.LocalStreetPeriodFor makes street spacing vary by
            // neighbourhood, so this sweeps every coordinate in range and
            // filters for the real thing, the same "street on all four
            // sides" definition AddSidewalks itself relies on.
            for (int x = -60; x <= 60; x++)
            {
                for (int y = -60; y <= 60; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    bool junction = CityLayout.IsStreetChunk(x - 1, y) &&
                                    CityLayout.IsStreetChunk(x + 1, y) &&
                                    CityLayout.IsStreetChunk(x, y - 1) &&
                                    CityLayout.IsStreetChunk(x, y + 1);
                    if (!junction)
                    {
                        continue;
                    }

                    float pad = world.Terrain.PadHeightFor(x, y);
                    foreach ((int nx, int ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                    {
                        float neighbourPad = world.Terrain.PadHeightFor(nx, ny);
                        if (neighbourPad >= pad)
                        {
                            continue;
                        }

                        BuildStreetEdge(x, y, out MeshBuffer pavement, out _);
                        casesFound++;
                        Assert.IsFalse(HasVertexNear(pavement, neighbourPad),
                            $"junction ({x},{y}) drops to ({nx},{ny}) with no sidewalk on either side, " +
                            $"but pavement still reaches {neighbourPad}m -- a stair over open roadway");
                    }
                }
            }

            TestContext.WriteLine($"{casesFound} junction drops checked");
            Assert.Greater(casesFound, 0, "no junction terrace drop in the sweep");
        }

        /// <summary>
        /// The short guardrail is there wherever a street drops toward a
        /// BUILDING lot's own lower edge and did NOT roll sloped -- a real
        /// ledge, worth fencing, when it is not a hillside. EXPLICIT ASK:
        /// "make it like 90% slopes 10% hard cutoffs with fencing and
        /// stairs" widened the sloped roll to cover a building neighbour
        /// too (see AddStreetEdgeFeatures's own doc comment), so a
        /// guardrail is no longer unconditional here the way it used to
        /// be -- this mirrors the same roll AStreetTerraceDropWithA
        /// SidewalkGetsARealStaircaseOrASlopedRamp already has to, in the
        /// same per-chunk edge order, so it knows in advance which drops
        /// are still expected to fence.
        /// </summary>
        [Test]
        public void AStreetTerraceDropAgainstABuildingGetsAGuardRailUnlessItRolledSloped()
        {
            CityWorld world = new CityWorld(Seed);
            int casesFound = 0;
            int slopedCases = 0;
            float chunkSize = CityLayout.ChunkSize;

            for (int x = -30; x <= 30; x++)
            {
                for (int y = -30; y <= 30; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    float x0 = x * chunkSize, z0 = y * chunkSize;
                    float x1 = x0 + chunkSize, z1 = z0 + chunkSize;
                    bool chunkAlreadySloped = false;
                    bool anyBuildingDrop = false;
                    bool expectRail = false;

                    // top0 for each cardinal edge, exactly as BuildGround's own
                    // four AddSkirtIfLower/AddStreetEdgeFeatures call sites
                    // pass it -- see AddStreetEdgeFeatures's own "real heights,
                    // not quantised pads" gate this now has to mirror (the
                    // Phase 2 gap fix): a street chunk's REAL edge here is
                    // ContinuousHeightAt at that exact corner, which can differ
                    // from PadHeightFor even when the quantised pads agree.
                    var edges = new (int nx, int ny, float px, float pz)[]
                    {
                        (x - 1, y, x0, z0),
                        (x + 1, y, x1, z1),
                        (x, y - 1, x1, z0),
                        (x, y + 1, x0, z1),
                    };

                    foreach (var (nx, ny, px, pz) in edges)
                    {
                        float top0Y = (float)world.Terrain.ContinuousHeightAt(px, pz);
                        float neighbourHeight = CityLayout.IsStreetChunk(nx, ny)
                            ? (float)world.Terrain.ContinuousHeightAt(px, pz)
                            : world.Terrain.PadHeightFor(nx, ny);
                        if (neighbourHeight >= top0Y)
                        {
                            continue;
                        }

                        bool sloped = !chunkAlreadySloped &&
                            new DeterministicRandom(world.ChunkSeed(nx, ny)).NextDouble() <
                                CityMeshBuilder.SlopedStreetChance;
                        if (sloped)
                        {
                            chunkAlreadySloped = true;
                        }

                        if (!CityLayout.IsStreetChunk(nx, ny))
                        {
                            anyBuildingDrop = true;
                            if (sloped)
                            {
                                slopedCases++;
                            }
                            else
                            {
                                expectRail = true;
                            }
                        }
                    }

                    if (!anyBuildingDrop)
                    {
                        continue;
                    }
                    casesFound++;

                    if (!expectRail)
                    {
                        continue;
                    }
                    BuildStreetEdge(x, y, out _, out MeshBuffer guardRail);
                    Assert.IsFalse(guardRail.IsEmpty,
                        $"street chunk ({x},{y}) drops to a building lot and did not roll sloped, " +
                        "but built no guardrail");
                }
            }

            TestContext.WriteLine($"{casesFound} street-vs-building drops checked, " +
                $"{slopedCases} rolled sloped instead of a guardrail");
            Assert.Greater(casesFound, 0, "no street-vs-building terrace drop in the sweep");
            Assert.Greater(casesFound - slopedCases, 0,
                "every street-vs-building drop rolled sloped -- nothing exercised the guardrail case");
        }

        /// <summary>
        /// EXPLICIT ASK: "if there is a drop that would cause fall damage
        /// like a hard corner make a fence around it and have a staircase."
        /// Confirmed root cause before this fix: AddStreetEdgeFeatures
        /// bailed immediately for any non-street chunk, so
        /// AddSkirtIfLower's bare retaining wall was the ONLY thing a
        /// BUILDING chunk's own edge against a lower neighbour (another lot
        /// on a different terrace, OR a lower street) ever got -- a real,
        /// fall-damage-worthy drop with no fence and no way down. Mirrors
        /// AStreetTerraceDropAgainstABuildingGetsAGuardRailUnlessItRolledSloped's
        /// own roll simulation, but sweeping BUILDING chunks as "this" one
        /// instead of street chunks.
        /// </summary>
        [Test]
        public void ABuildingTerraceDropAgainstAnyLowerNeighbourGetsAGuardRailUnlessItRolledSloped()
        {
            CityWorld world = new CityWorld(Seed);
            float chunkSize = CityLayout.ChunkSize;
            int casesFound = 0;
            int slopedCases = 0;

            for (int x = -30; x <= 30; x++)
            {
                for (int y = -30; y <= 30; y++)
                {
                    if (CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    float pad = world.Terrain.PadHeightFor(x, y);
                    float x0 = x * chunkSize, z0 = y * chunkSize;
                    float x1 = x0 + chunkSize, z1 = z0 + chunkSize;
                    bool chunkAlreadySloped = false;
                    bool anyDrop = false;
                    bool expectRail = false;

                    // (neighbourX, neighbourY, gateCornerX, gateCornerZ) --
                    // the exact corner AddStreetEdgeFeatures/AddSkirtIfLower
                    // gate each cardinal edge off (top0), matching their own
                    // call sites in BuildGround.
                    var edges = new (int nx, int ny, float px, float pz)[]
                    {
                        (x - 1, y, x0, z0),
                        (x + 1, y, x1, z1),
                        (x, y - 1, x1, z0),
                        (x, y + 1, x0, z1),
                    };

                    BuildingSite site = world.BuildingAt(x, y);

                    foreach (var (nx, ny, px, pz) in edges)
                    {
                        float neighbourHeight = CityLayout.IsStreetChunk(nx, ny)
                            ? (float)world.Terrain.ContinuousHeightAt(px, pz)
                            : world.Terrain.PadHeightFor(nx, ny);
                        if (neighbourHeight >= pad)
                        {
                            continue;
                        }

                        // An edge between two chunks of the SAME building runs
                        // through the building, not along a terrace, and is
                        // deliberately left bare (see BuildGround's own
                        // "westInternal" comment).
                        if (site.Exists)
                        {
                            BuildingSite other = world.BuildingAt(nx, ny);
                            if (other.Exists && other.AnchorX == site.AnchorX && other.AnchorY == site.AnchorY)
                            {
                                continue;
                            }
                        }

                        // The ORIGINAL street-ramp roll (SlopedStreetChance)
                        // is vestigial here and always effectively false in
                        // real behaviour -- AddSlopedStreetRamp itself is
                        // gated on CityLayout.IsStreetChunk(chunkX, chunkY),
                        // which this whole sweep has already excluded (see
                        // the continue above). The one REAL way a building
                        // chunk's own edge skips the guardrail is the
                        // door-facing (north, py == z1) edge specifically,
                        // when its neighbour is a real street and
                        // BuildingDecor.EntranceApproachIsSloped rolls true
                        // -- see AddStreetEdgeFeatures's own matching gate.
                        bool isDoorFacingEdge = ny == y + 1;
                        // ANY chunk of the building on the door-facing row, not
                        // just the anchor: AddStreetEdgeFeatures reads the
                        // building's roll off BuildingAt, which follows a
                        // covered chunk back to its anchor.
                        bool sloped = isDoorFacingEdge && CityLayout.IsStreetChunk(nx, ny) &&
                            site.Exists &&
                            BuildingDecor.EntranceApproachIsSloped(world.ChunkSeed(site.AnchorX, site.AnchorY));

                        anyDrop = true;
                        if (sloped)
                        {
                            slopedCases++;
                        }
                        else
                        {
                            expectRail = true;
                        }
                    }

                    if (!anyDrop)
                    {
                        continue;
                    }
                    casesFound++;

                    if (!expectRail)
                    {
                        continue;
                    }
                    BuildStreetEdge(x, y, out _, out MeshBuffer guardRail);
                    Assert.IsFalse(guardRail.IsEmpty,
                        $"building chunk ({x},{y}) drops to a lower neighbour and did not roll sloped, " +
                        "but built no guardrail");
                }
            }

            TestContext.WriteLine($"{casesFound} building terrace drops checked, " +
                $"{slopedCases} rolled sloped instead of a guardrail");
            Assert.Greater(casesFound, 0, "no building terrace drop in the sweep");
            Assert.Greater(casesFound - slopedCases, 0,
                "every building terrace drop rolled sloped -- nothing exercised the guardrail case");
        }

        /// <summary>
        /// EXPLICIT ASK: "the windows/entrances should never be cut off
        /// always have like a stairway entrance or some transition to
        /// flatness." Sweeps for a real multi-chunk building whose door row
        /// sits on a lower pad than whichever row set BaseHeight (see
        /// BuildBuilding's own doorRowPad comment) -- which would otherwise
        /// leave its own threshold floating over open air -- and confirms
        /// real stair geometry now reaches all the way down to the real
        /// ground outside it.
        ///
        /// WIDE SWEEP, DELIBERATELY: this is a compound rarity -- a
        /// multi-chunk building (TowerChance is the largest single share at
        /// 0.45, but still well under half of all lots) whose own internal
        /// terrace mismatch (confirmed separately to often be zero even
        /// among multi-chunk buildings, since a 48m span rarely crosses a
        /// whole 6.4m TerraceStep) ALSO happens to put the low pad
        /// specifically under the door's own row rather than the other one.
        /// A +/-40 chunk sweep found zero genuine cases; +/-150 reliably
        /// does, the same "the origin can land inside one region cluster by
        /// chance" reasoning ATerraceEdgeAlwaysGetsARetainingWall's own
        /// widened sweep already documents.
        /// </summary>
        [Test]
        public void ADoorOnASlopedLotGetsRealStairsReachingTheGroundOutsideIt()
        {
            CityWorld world = new CityWorld(Seed);
            int found = 0;

            for (int x = -150; x <= 150 && found == 0; x++)
            {
                for (int y = -150; y <= 150 && found == 0; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }

                    // Mirrors BuildBuilding's own calculation exactly: the
                    // highest pad along the door's own row (MaxZ), which is
                    // routinely a different, lower row than whichever one
                    // set BaseHeight for a multi-chunk building.
                    float doorRowPad = float.MinValue;
                    for (int dx = 0; dx < site.Role.SpanX; dx++)
                    {
                        doorRowPad = Mathf.Max(doorRowPad,
                            world.Terrain.PadHeightFor(x + dx, y + site.Role.SpanY - 1));
                    }
                    float mismatch = site.BaseHeight - doorRowPad;
                    if (mismatch <= BuildingDecor.EntranceFlushMaxMismatch + 0.5f)
                    {
                        continue;   // want a real, unambiguous case, not a borderline one
                    }

                    found++;
                    Build(x, y, out MeshBuffer ground, out MeshBuffer pavement, out MeshBuffer structures);

                    float expectedFloor = site.BaseHeight - Mathf.Min(mismatch, BuildingDecor.EntranceMaxRise);
                    Assert.IsTrue(HasVertexNear(pavement, expectedFloor, tolerance: 0.05f),
                        $"building at ({x},{y}) has a {mismatch:0.0}m door mismatch but no stair geometry " +
                        $"reaches down to {expectedFloor:0.0}m -- the door is left floating");
                }
            }

            Assert.Greater(found, 0, "never found a building with a real door mismatch worth checking");
        }

        /// <summary>
        /// EXPLICIT ASK: "make it transitions from door to street more
        /// smoothly lets do 60% have the terrain slope into the entrance
        /// sometimes with some sort of staircase that could be built into
        /// the terrain if needed but if its flat enough then no
        /// staircase... the other 40%... hard edges and fencing and
        /// staircases." Sweeps real buildings whose door-facing edge meets
        /// a real, lower street, mirrors BuildingDecor.EntranceApproachIsSloped
        /// (the same roll AddStreetEdgeFeatures itself makes) and the same
        /// grade math AddSlopedEntranceApproach uses, and confirms each of
        /// the three real outcomes it should produce:
        /// - not sloped (40%): the ordinary guardrail, unchanged.
        /// - sloped and flat enough: a ramp reaching both real ends, with
        ///   no vertex at all near the midpoint height -- a plain quad has
        ///   none, only individual stair-step boxes would add one.
        /// - sloped and too steep: the same ramp, PLUS a vertex near the
        ///   midpoint height, proving real embedded stair geometry exists.
        /// </summary>
        [Test]
        public void ADoorFacingEdgeGetsTheApproachItsOwnRollAndGradeSay()
        {
            CityWorld world = new CityWorld(Seed);
            float chunkSize = CityLayout.ChunkSize;
            int hardEdgeCases = 0;
            int flatRampCases = 0;
            int steppedRampCases = 0;

            for (int x = -40; x <= 40; x++)
            {
                for (int y = -40; y <= 40; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }
                    if (!CityLayout.IsStreetChunk(x, y + 1))
                    {
                        continue;   // door does not face a real street here
                    }

                    float pad = world.Terrain.PadHeightFor(x, y);
                    float x0 = x * chunkSize;
                    float x1 = x0 + chunkSize;
                    float z1 = y * chunkSize + chunkSize;
                    float neighbourHeight = (float)world.Terrain.ContinuousHeightAt(x0, z1);
                    if (neighbourHeight >= pad)
                    {
                        continue;   // flush -- nothing to build either way
                    }

                    bool sloped = BuildingDecor.EntranceApproachIsSloped(world.ChunkSeed(x, y));
                    BuildStreetEdge(x, y, out MeshBuffer pavement, out MeshBuffer guardRail);

                    if (!sloped)
                    {
                        hardEdgeCases++;
                        Assert.IsFalse(guardRail.IsEmpty,
                            $"({x},{y}) rolled the hard-edge approach but built no guardrail");
                        continue;
                    }

                    // Confined to the real margin strip -- this chunk's
                    // full width, from the building's own true wall out to
                    // the chunk boundary -- rather than scanning the whole
                    // pavement buffer for a height. That buffer also
                    // carries this SAME building's own roof and interior
                    // floors/stairs (see PavementRunsAlongStreetsAndStops
                    // AtJunctions's own doc comment), spread across a wide
                    // range of heights that a bare height check cannot
                    // tell apart from the entrance approach actually being
                    // tested here -- confirmed live, a real interior floor
                    // slab at a real building nearly triggered a false
                    // "stairs where there should be none" failure.
                    // Padded a hair past the exact boundary -- the ramp's
                    // own low edge sits AT z1 exactly (see
                    // AddSlopedEntranceApproach's own low0/low1, built from
                    // top0/top1's real x/z unchanged), and Rect.Contains
                    // excludes a point sitting exactly on its max edge.
                    const float regionPad = 0.1f;
                    var approachRegion = Rect.MinMaxRect(
                        x0 - regionPad, (float)site.MaxZ - regionPad, x1 + regionPad, z1 + regionPad);

                    // Sloped -- the ramp must reach both real ends
                    // regardless of grade (same wide tolerances the sibling
                    // street-ramp/stair sweep above already establishes).
                    Assert.IsTrue(HasVertexNearInRegion(pavement, pad, approachRegion, TerrainField.TerraceStep),
                        $"({x},{y})'s sloped approach never reaches its own pad at {pad}m");
                    Assert.IsTrue(HasVertexNearInRegion(pavement, neighbourHeight, approachRegion, 0.6f),
                        $"({x},{y})'s sloped approach never reaches down to {neighbourHeight}m");

                    // Mirrors AddSlopedEntranceApproach's own grade math
                    // exactly: run is the REAL measured margin (site.MaxZ
                    // to the chunk boundary), not an assumed constant --
                    // see that function's own "measured, not assumed"
                    // comment for the bug this guards against.
                    float rise = pad - neighbourHeight;
                    float run = z1 - (float)site.MaxZ;
                    float worstAngleDeg = Mathf.Atan2(Mathf.Max(rise, 0.0f), run) * Mathf.Rad2Deg;
                    float midHeight = neighbourHeight + rise * 0.5f;

                    if (worstAngleDeg <= CityMeshBuilder.EntranceRampMaxAngleDeg)
                    {
                        flatRampCases++;
                        // Only meaningful once the midpoint sits clearly
                        // apart from both real endpoints -- for a tiny
                        // rise (this case is the near-flush end of "flat
                        // enough"), the midpoint can land within the SAME
                        // tolerance window as the endpoints themselves,
                        // which the plain ramp quad's own two corner
                        // vertices already legitimately satisfy. That is a
                        // property of how close together the two heights
                        // are, not evidence of stair geometry.
                        if (rise > 0.5f)
                        {
                            Assert.IsFalse(HasVertexNearInRegion(pavement, midHeight, approachRegion, 0.1f),
                                $"({x},{y})'s approach is flat enough ({worstAngleDeg:0.0} deg) for a " +
                                "plain ramp, but contains stair-like geometry at its own midpoint height");
                        }
                    }
                    else
                    {
                        steppedRampCases++;
                        Assert.IsTrue(HasVertexNearInRegion(pavement, midHeight, approachRegion, 0.3f),
                            $"({x},{y})'s approach is steep ({worstAngleDeg:0.0} deg) but has no embedded " +
                            "stair geometry near its own midpoint height");
                    }
                }
            }

            TestContext.WriteLine($"{hardEdgeCases} hard-edge, {flatRampCases} flat ramp, " +
                $"{steppedRampCases} stepped ramp");
            Assert.Greater(hardEdgeCases + flatRampCases + steppedRampCases, 0,
                "no door-facing street edge drop found in the sweep");
        }

        /// <summary>
        /// EXPLICIT ASK: "make some areas that the street generates so
        /// high that there is open space under the street at entrance
        /// level and theres essentially a second street under the street
        /// like a bridge." Mirrors CityMeshBuilder.TryGetElevatedBridge-
        /// DeckHeight's own detection exactly (a real terrain dip between
        /// two flanking high points, gated by the same rare roll) to find
        /// a genuine qualifying span, then confirms the real chunk built
        /// there has BOTH the ordinary lower street (the "second street,"
        /// left completely alone) AND a real elevated deck with genuine
        /// headroom above it.
        /// </summary>
        [Test]
        public void AGenuineTerrainDipOccasionallyGetsAnElevatedBridgeDeckWithTheRealStreetStillUnderneath()
        {
            CityWorld world = new CityWorld(Seed);
            const int half = CityMeshBuilder.ElevatedBridgeHalfLengthChunks;
            // Mirrors CityMeshBuilder's own private ElevatedBridgeBlockChunks
            // (half * 4) exactly -- see that constant's own doc comment for
            // why a centre may only be chosen from the middle of its block,
            // never the block's own edge margin: a candidate whose full
            // [centre-half, centre+half] reach would cross into the
            // neighbouring block is never picked by the real code, so this
            // sweep must skip those positions too or it will "find" a dip
            // the real code deliberately never builds a deck for.
            const int block = half * 4;
            int found = 0;

            for (int x = -60; x <= 60 && found == 0; x++)
            {
                for (int centreY = -300; centreY <= 300 && found == 0; centreY++)
                {
                    if (!CityLayout.IsStreetChunk(x, centreY) || CityLayout.StreetRunsAlongX(x, centreY))
                    {
                        continue;
                    }

                    int blockStart = FloorDivForTest(centreY, block) * block;
                    if (centreY < blockStart + half || centreY > blockStart + block - 1 - half)
                    {
                        continue;
                    }

                    // Mirrors CityMeshBuilder.IsElevatedBridgeSpanCentre
                    // exactly: only the two flanking endpoints have to sit
                    // meaningfully higher than the centre, then the same
                    // rare roll.
                    double columnX = CityLayout.ChunkCentreX(x);
                    double heightCentre = world.Terrain.ContinuousHeightAt(columnX, CityLayout.ChunkCentreZ(centreY));
                    double heightBefore =
                        world.Terrain.ContinuousHeightAt(columnX, CityLayout.ChunkCentreZ(centreY - half));
                    double heightAfter =
                        world.Terrain.ContinuousHeightAt(columnX, CityLayout.ChunkCentreZ(centreY + half));

                    if (heightBefore - heightCentre < CityMeshBuilder.ElevatedBridgeMinDip ||
                        heightAfter - heightCentre < CityMeshBuilder.ElevatedBridgeMinDip)
                    {
                        continue;
                    }

                    var roll = new DeterministicRandom(world.ChunkSeed(x, centreY));
                    if (roll.NextDouble() >= CityMeshBuilder.ElevatedBridgeChance)
                    {
                        continue;
                    }

                    found++;
                    // BuildStreetEdge, not Build -- the bridge feature is
                    // gated on `guardRail != null` (CityMeshBuilder.cs's
                    // own BuildGround), and Build's own overload never
                    // supplies one (defaults to null), which would silently
                    // skip the whole feature regardless of a genuine dip.
                    BuildStreetEdge(x, centreY, out MeshBuffer pavement, out MeshBuffer guardRail,
                        out MeshBuffer ground);

                    // NOT necessarily interpolated from THIS exact
                    // centreY's own before/after -- CityMeshBuilder's real
                    // search may land on a different, overlapping centre
                    // within its own search window and use that one
                    // instead (both are genuine qualifying valleys; which
                    // one wins is an implementation detail neither side
                    // needs to agree on, now that IsElevatedBridgeSpanCentre
                    // no longer requires a single STRICT local minimum).
                    // What must hold regardless: the deck never dips below
                    // (this chunk's own real street height + MinDip)
                    // anywhere, since it is a straight line between two
                    // points that are both at least that high by
                    // construction.
                    float minPossibleDeckY = (float)heightCentre + (float)CityMeshBuilder.ElevatedBridgeMinDip;
                    Assert.IsTrue(HasVertexAbove(ground, minPossibleDeckY - 0.5f),
                        $"({x},{centreY}) should have an elevated deck reaching at least " +
                        $"{minPossibleDeckY:0.0}m but none found");

                    // The real, lower street must still be there too --
                    // "the second street under the bridge," unchanged.
                    float realStreetY = (float)heightCentre;
                    Assert.IsTrue(HasVertexNear(ground, realStreetY, 0.6f),
                        $"({x},{centreY}) lost its own real street surface under the new deck");

                    // EXPLICIT BUG REPORT, with screenshots: "also is
                    // invisible from the bottom." A quad built with only an
                    // upward normal is exactly what back-face culling would
                    // hide from underneath -- confirms a genuinely
                    // downward-facing normal exists somewhere near the
                    // deck's own height, not just an upward one.
                    Assert.IsTrue(HasDownwardNormalNear(ground, minPossibleDeckY, 6.0f),
                        $"({x},{centreY})'s deck has no downward-facing geometry -- " +
                        "invisible from underneath");

                    // EXPLICIT BUG REPORT, with screenshots: "gaps in it
                    // where it doesnt tilt properly and larger gap." The
                    // immediately preceding chunk, still well inside this
                    // same span, must compute the IDENTICAL height at their
                    // shared edge -- confirmed by construction
                    // (DeckHeightAtWorldZ interpolates by the real world Z,
                    // so two chunks sharing an edge's exact world position
                    // always agree, the same reason the real street below
                    // never needed stitching either).
                    //
                    // ONLY WHEN THE NEIGHBOUR IS ITSELF A STRAIGHT COLUMN
                    // CHUNK. A cross street's junction chunk (the street
                    // runs along X there) never carries a deck -- the span
                    // simply ends at it -- so there is no second deck to
                    // meet. Junctions used to sit at fixed spacing and this
                    // sweep never landed beside one; irregular arterials
                    // put one anywhere.
                    BuildStreetEdge(x, centreY - 1, out _, out _, out MeshBuffer neighbourGround);
                    float sharedEdgeZ = centreY * CityLayout.ChunkSize;
                    float sharedEdgeHeightHere = HeightOfVertexNearZ(ground, sharedEdgeZ, minPossibleDeckY);
                    float sharedEdgeHeightNeighbour =
                        HeightOfVertexNearZ(neighbourGround, sharedEdgeZ, minPossibleDeckY);
                    bool neighbourIsAColumnChunk = CityLayout.IsStreetChunk(x, centreY - 1) &&
                                                   !CityLayout.StreetRunsAlongX(x, centreY - 1);
                    if (neighbourIsAColumnChunk && !float.IsNaN(sharedEdgeHeightHere) && !float.IsNaN(sharedEdgeHeightNeighbour))
                    {
                        Assert.AreEqual(sharedEdgeHeightHere, sharedEdgeHeightNeighbour, 0.05f,
                            $"({x},{centreY})'s deck does not meet its own neighbour's deck at their " +
                            "shared edge -- a real gap between two chunks of the same bridge");
                    }
                }
            }

            TestContext.WriteLine($"{found} elevated bridge span(s) found in the sweep");
            Assert.Greater(found, 0, "never found a genuinely qualifying dip in the sweep");
        }

        /// <summary>
        /// EXPLICIT BUG REPORT, WITH SCREENSHOT: "the highway is going into
        /// the street underground make that impossible." The highway
        /// deck's vertical profile was changed to a smoothed, 900m-averaged
        /// reference (TerrainField.SmoothedHeightAt, see
        /// HighwayVerticalSmoothingWindowMetres's own doc comment) so it
        /// undulates less often -- but a wide average has no lower bound
        /// relative to the REAL ground at any one point, so over a local
        /// rise the smoothed reference could sit well below the actual
        /// terrain there, letting the deck's computed height drop below the
        /// real street surface. Fixed by taking Max(smoothed, real) as the
        /// ground reference everywhere (DeckCornerAt's own doc comment).
        ///
        /// Builds several real chunks along the x=0 highway line (a
        /// structural fact of CityLayout.IsHighwayChunk, true for every
        /// world seed) and checks every clearly-elevated ground vertex
        /// (more than 3m above the real local ground at its own x/z --
        /// comfortably separating deck geometry from the ordinary street
        /// surface/markings sharing the same buffer) sits at or above the
        /// worst-case floor clearance above that same real local ground.
        /// This is the exact invariant CityLayout.HighwayClearanceTaperAt's
        /// own "no true touchdown anywhere, ever" fix already established
        /// for the taper multiplier -- this test catches the SAME class of
        /// bug reappearing through a different mechanism (the ground
        /// reference itself, not the multiplier).
        /// </summary>
        [Test]
        public void TheHighwayDeckNeverDipsBelowTheFloorClearanceAboveTheRealLocalGround()
        {
            CityWorld world = new CityWorld(Seed);
            const int highwayX = 0;
            float minimumClearance = CityMeshBuilder.HighwayClearance * CityMeshBuilder.HighwayMinimumClearanceFraction;
            int checkedVertices = 0;

            for (int y = 0; y <= 40; y++)
            {
                Assert.IsTrue(CityLayout.IsHighwayChunk(highwayX, y),
                    $"test assumption broken -- chunk ({highwayX},{y}) is no longer a highway chunk");

                // BuildStreetEdge, not Build -- AddHighwayDeck (like the
                // elevated bridge deck) is only called when guardRail !=
                // null (BuildChunk's own BuildGround), and Build's overload
                // never supplies one, which would silently skip the whole
                // feature (same gotcha the elevated bridge test above
                // already documents).
                BuildStreetEdge(highwayX, y, out _, out _, out MeshBuffer ground);
                foreach (Vector3 v in ground.Vertices)
                {
                    double realGround = world.Terrain.ContinuousHeightAt(v.x, v.z);
                    float heightAboveRealGround = (float)(v.y - realGround);
                    if (heightAboveRealGround <= 3.0f)
                    {
                        continue; // ordinary street-level surface, not the deck
                    }

                    checkedVertices++;
                    Assert.GreaterOrEqual(heightAboveRealGround, minimumClearance - 0.1f,
                        $"chunk ({highwayX},{y}): a deck vertex at world ({v.x:F1},{v.z:F1}) sits only " +
                        $"{heightAboveRealGround:F2}m above the real local ground -- below the " +
                        $"{minimumClearance:F2}m floor, i.e. it has dipped into the street");
                }
            }

            Assert.Greater(checkedVertices, 0, "never found an elevated deck vertex in the swept stretch");
        }

        /// <summary>Same chunk, same world -- the stair has to pick the same side every time, not flip on a rebuild.</summary>
        [Test]
        public void StreetEdgeFeaturesAreDeterministic()
        {
            CityWorld world = new CityWorld(Seed);

            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    float pad = world.Terrain.PadHeightFor(x, y);
                    bool anyDrop = false;
                    foreach ((int nx, int ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                    {
                        if (world.Terrain.PadHeightFor(nx, ny) < pad)
                        {
                            anyDrop = true;
                        }
                    }
                    if (!anyDrop)
                    {
                        continue;
                    }

                    BuildStreetEdge(x, y, out MeshBuffer pavementA, out MeshBuffer guardRailA, out MeshBuffer groundA);
                    BuildStreetEdge(x, y, out MeshBuffer pavementB, out MeshBuffer guardRailB, out MeshBuffer groundB);

                    CollectionAssert.AreEqual(pavementA.Vertices, pavementB.Vertices,
                        $"chunk ({x},{y}) built a different staircase on a second, identical rebuild");
                    CollectionAssert.AreEqual(guardRailA.Vertices, guardRailB.Vertices,
                        $"chunk ({x},{y}) built a different guardrail on a second, identical rebuild");
                    CollectionAssert.AreEqual(groundA.Vertices, groundB.Vertices,
                        $"chunk ({x},{y}) built a different sloped ramp on a second, identical rebuild");
                    return;
                }
            }

            Assert.Fail("no street terrace drop found to check determinism against");
        }

        /// <summary>
        /// EXPLICIT ASK: "never so steep that you can walk or run up it."
        /// Player/Civilian/OldGuy's CharacterController.slopeLimit is 50
        /// degrees (see PlayerRigBuilder.cs, CivilianBuilder.cs,
        /// OldGuyBuilder.cs) -- a tripwire against ever tuning
        /// SlopedStreetMinAngleDeg down to or below that by accident.
        /// </summary>
        [Test]
        public void SlopedStreetRampsAreAlwaysSteeperThanACharacterCanClimb()
        {
            const float CharacterControllerSlopeLimitDeg = 50.0f;
            Assert.Greater(CityMeshBuilder.SlopedStreetMinAngleDeg, CharacterControllerSlopeLimitDeg);
        }

        [Test]
        public void ABuildingsBoxMatchesItsFootprintAndHeight()
        {
            CityWorld world = new CityWorld(Seed);
            int checkedCount = 0;

            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }

                    Build(x, y, out _, out MeshBuffer structures);
                    Assert.IsFalse(structures.IsEmpty, $"({x},{y}) has a building but produced no geometry");
                    checkedCount++;

                    Bounds bounds = BoundsOf(structures);

                    // Facade ribs stand proud of the wall they are on, so the
                    // west and south faces reach exactly one fin depth further
                    // out -- and exactly zero further when that wall was not
                    // ribbed. Both are pinned rather than allowing a range,
                    // because "somewhere within half a metre of the wall" would
                    // also pass for a fin built on the wrong wall entirely.
                    BuildingDecor.RibbedFacade ribbed = BuildingDecor.Ribs(
                        world.ChunkSeed(x, y), site.Params.Width, site.Params.Depth,
                        site.Params.Height,
                        -BuildingInterior.DoorWidth * 0.5f, BuildingInterior.DoorWidth * 0.5f);
                    float westFins = 0.0f;
                    float southFins = 0.0f;
                    foreach (BuildingDecor.Rib rib in ribbed.Fins)
                    {
                        if (rib.Normal == Vector3.left)
                        {
                            westFins = ribbed.Depth;
                        }
                        if (rib.Normal == Vector3.back)
                        {
                            southFins = ribbed.Depth;
                        }
                    }

                    // A wing is a second volume attached to one wall, so on the
                    // wall it grew from the facade reaches out to the wing's
                    // own far face instead -- and a building can now grow more
                    // than one, so take whichever reaches furthest on each axis.
                    double westFace = site.MinX - westFins;
                    double southFace = site.MinZ - southFins;
                    foreach (Bounds wing in CityMeshBuilder.WingBoxesFor(world, site))
                    {
                        westFace = System.Math.Min(westFace, wing.min.x);
                        southFace = System.Math.Min(southFace, wing.min.z);
                    }

                    // Bridges extend past the building, so only the lower
                    // corner and the roof can be compared exactly.
                    Assert.AreEqual(westFace, bounds.min.x, 0.01, $"({x},{y}) west face");
                    Assert.AreEqual(southFace, bounds.min.z, 0.01, $"({x},{y}) south face");
                    Assert.AreEqual(site.PodiumBase, bounds.min.y, 0.01, $"({x},{y}) podium base");
                    Assert.GreaterOrEqual(bounds.max.y, site.Top - 0.01f, $"({x},{y}) roof");
                }
            }

            Assert.Greater(checkedCount, 100, "not enough buildings sampled");
        }

        /// <summary>
        /// NO WING EVER REACHES INTO ANOTHER BUILDING. This is the one thing
        /// the wing's clearance clamp exists for, and the only place it can
        /// actually be checked: BuildingDecor is handed clearances as numbers
        /// and has no way of knowing whether they were measured correctly, so
        /// a clamp that worked on paper and a lot rectangle read one chunk out
        /// would both pass every test in that file.
        ///
        /// Real ground truth, over every anchored building in a wide sweep,
        /// against every other building's real footprint.
        /// </summary>
        [Test]
        public void NoWingGrowsThroughTheBuildingNextDoor()
        {
            CityWorld world = new CityWorld(Seed);
            var footprints = new List<(int X, int Y, Rect Plan)>();

            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }
                    footprints.Add((x, y, Rect.MinMaxRect(
                        (float)site.MinX, (float)site.MinZ, (float)site.MaxX, (float)site.MaxZ)));
                }
            }

            int wings = 0;
            foreach ((int x, int y, Rect _) in footprints)
            {
                BuildingSite site = world.BuildingAt(x, y);
                foreach (Bounds box in CityMeshBuilder.WingBoxesFor(world, site))
                {
                    wings++;

                    Rect plan = Rect.MinMaxRect(box.min.x, box.min.z, box.max.x, box.max.z);
                    foreach ((int otherX, int otherY, Rect other) in footprints)
                    {
                        if (otherX == x && otherY == y)
                        {
                            continue;
                        }
                        Assert.IsFalse(plan.Overlaps(other),
                            $"({x},{y})'s wing at {plan} runs into ({otherX},{otherY}) at {other}");
                    }
                }
            }

            TestContext.WriteLine($"{wings} wings over {footprints.Count} buildings");
            Assert.Greater(wings, 20, "no building in the sweep grew a wing, so this proved nothing");
        }

        /// <summary>
        /// A multi-chunk building must be built once, by its anchor. Building it
        /// from every chunk it covers would stack four identical towers in the
        /// same place -- invisible from outside, and quadruple the triangles.
        /// </summary>
        [Test]
        public void MultiChunkBuildingsAreBuiltOnlyByTheirAnchor()
        {
            CityWorld world = new CityWorld(Seed);
            int covered = 0;

            for (int x = -30; x <= 30; x++)
            {
                for (int y = -30; y <= 30; y++)
                {
                    if (world.RoleAt(x, y).Kind != LotKind.Consumed)
                    {
                        continue;
                    }
                    covered++;
                    Build(x, y, out _, out MeshBuffer structures);
                    Assert.IsTrue(structures.IsEmpty,
                        $"covered chunk ({x},{y}) built its own copy of a neighbour's building");
                }
            }

            Assert.Greater(covered, 50, "no covered chunks in the sweep");
        }

        [Test]
        public void NoGeometryContainsNaNOrInfinity()
        {
            for (int x = -12; x <= 12; x++)
            {
                for (int y = -12; y <= 12; y++)
                {
                    Build(x, y, out MeshBuffer ground, out MeshBuffer structures);
                    foreach (MeshBuffer buffer in new[] { ground, structures })
                    {
                        foreach (Vector3 v in buffer.Vertices)
                        {
                            Assert.IsTrue(IsFinite(v), $"({x},{y}) has a vertex at {v}");
                        }
                        foreach (Vector3 n in buffer.Normals)
                        {
                            Assert.IsTrue(IsFinite(n), $"({x},{y}) has a normal of {n}");
                        }
                    }
                }
            }
        }

        [Test]
        public void EveryTriangleIndexIsInRange()
        {
            for (int x = -12; x <= 12; x++)
            {
                for (int y = -12; y <= 12; y++)
                {
                    Build(x, y, out MeshBuffer ground, out MeshBuffer structures);
                    foreach (MeshBuffer buffer in new[] { ground, structures })
                    {
                        Assert.AreEqual(0, buffer.Triangles.Count % 3, $"({x},{y}) has a partial triangle");
                        Assert.AreEqual(buffer.Vertices.Count, buffer.Normals.Count, $"({x},{y}) normals");
                        Assert.AreEqual(buffer.Vertices.Count, buffer.Uvs.Count, $"({x},{y}) uvs");
                        foreach (int index in buffer.Triangles)
                        {
                            Assert.GreaterOrEqual(index, 0, $"({x},{y}) negative index");
                            Assert.Less(index, buffer.Vertices.Count, $"({x},{y}) index past the end");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// A walkway deck has to actually reach both buildings. An inverted box
        /// -- which is what you get if a neighbour's footprint starts before
        /// this one ends -- renders as a hole rather than as an error.
        /// </summary>
        [Test]
        public void WalkwayDecksHavePositiveExtentAndSitAtTheirStatedHeight()
        {
            CityWorld world = new CityWorld(Seed);
            SkybridgeLayout bridges = new SkybridgeLayout(Seed);
            int decks = 0;

            for (int x = -40; x <= 40; x++)
            {
                for (int y = -40; y <= 40; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }

                    foreach (BuildingSite neighbour in new[]
                             {
                                 SkybridgeLayout.EastNeighbour(world, site),
                                 SkybridgeLayout.NorthNeighbour(world, site),
                             })
                    {
                        if (!neighbour.Exists)
                        {
                            continue;
                        }
                        foreach (Skybridge bridge in bridges.BridgesBetween(site, neighbour))
                        {
                            decks++;
                            // The gap the deck has to span, measured face to face.
                            double gap = neighbour.MinX > site.MaxX
                                ? neighbour.MinX - site.MaxX
                                : neighbour.MinZ - site.MaxZ;
                            Assert.Greater(gap, 0.0,
                                $"({x},{y}) is bridged to a neighbour whose footprint overlaps it, so the " +
                                "deck would be inside out");
                            Assert.Less(gap, CityLayout.ChunkSize * 2.0,
                                $"({x},{y}) has a {gap:0.0}m walkway -- that is further than a neighbour " +
                                "should ever be");
                        }
                    }
                }
            }

            TestContext.WriteLine($"{decks} walkway decks checked");
            Assert.Greater(decks, 30, "not enough walkways sampled");
        }

        /// <summary>
        /// EVERY FACE IS WOUND TO FACE THE WAY IT SAYS IT DOES.
        ///
        /// This one exists because it was already false. Five of AddBox's six
        /// faces were wound backwards -- every wall, kerb and walkway deck in
        /// the city -- and it went unnoticed for exactly the reason a test is
        /// needed here: an inside-out closed box still fills the same
        /// silhouette, so the city looked like a city. What it actually did
        /// was light each surface by a normal pointing away from the camera
        /// and leave the FAR wall's depth in the buffer, which is invisible
        /// until something is put inside the building -- and then the stairs
        /// draw in front of the facade.
        ///
        /// The convention was settled by rendering a quad each way round with
        /// back-face culling on rather than by reasoning about handedness: the
        /// visible one is the one whose Cross(b - a, c - a) points at the
        /// camera, along the outward normal.
        /// </summary>
        [Test]
        public void EveryFaceIsWoundTheWayItsNormalClaims()
        {
            int triangles = 0;

            for (int x = -8; x <= 8; x++)
            {
                for (int y = -8; y <= 8; y++)
                {
                    Build(x, y, out MeshBuffer ground, out MeshBuffer pavement, out MeshBuffer structures);
                    foreach (MeshBuffer buffer in new[] { ground, pavement, structures })
                    {
                        for (int t = 0; t + 2 < buffer.Triangles.Count; t += 3)
                        {
                            Vector3 a = buffer.Vertices[buffer.Triangles[t]];
                            Vector3 b = buffer.Vertices[buffer.Triangles[t + 1]];
                            Vector3 c = buffer.Vertices[buffer.Triangles[t + 2]];
                            Vector3 stated = buffer.Normals[buffer.Triangles[t]];

                            float facing = Vector3.Dot(Vector3.Cross(b - a, c - a), stated);
                            Assert.Greater(facing, 0.0f,
                                $"chunk ({x},{y}) has a triangle claiming to face {stated} that is "
                                + "wound the other way -- it renders from behind and writes the "
                                + "wrong surface's depth");
                            triangles++;
                        }
                    }
                }
            }

            TestContext.WriteLine($"{triangles} triangles checked");
            Assert.Greater(triangles, 10000, "not enough geometry in the sweep to mean anything");
        }

        /// <summary>
        /// THE DOOR IS A REAL HOLE, not a painted-on rectangle.
        ///
        /// IT USED TO MEASURE THE WHOLE WALL against a solid rectangle less a
        /// doorway, and that stopped meaning anything the moment the wall grew
        /// real windows -- it failed with 619m2 against an expected 816m2,
        /// which is the windows, not a missing door. Rewriting it to subtract
        /// the window area too would have made the test recompute the layout it
        /// is supposed to be checking.
        ///
        /// So it measures the one rectangle the claim is actually about: how
        /// much wall stands in the doorway. The answer has to be none, whatever
        /// the rest of the wall is doing, and the second assertion keeps that
        /// from passing because the wall was never built at all.
        /// </summary>
        [Test]
        public void TheFrontWallHasADoorwayMissingFromIt()
        {
            CityWorld world = new CityWorld(Seed);
            int checkedCount = 0;

            for (int x = -12; x <= 12; x++)
            {
                for (int y = -12; y <= 12; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }
                    checkedCount++;

                    Build(x, y, out _, out _, out MeshBuffer structures);

                    float centreX = (float)site.CentreX;
                    float half = BuildingInterior.DoorWidth * 0.5f;
                    var doorway = Rect.MinMaxRect(
                        centreX - half, site.BaseHeight,
                        centreX + half, site.BaseHeight + BuildingInterior.DoorHeight(site.Params.Height));

                    float blocking = AreaInUprightRect(structures, (float)site.MaxZ, doorway);
                    Assert.AreEqual(0.0f, blocking, 0.001f,
                        $"({x},{y}) has {blocking:0.00}m2 of wall standing in its own doorway");

                    float wall = AreaInPlane(structures, (float)site.MaxZ, 2, Rect.zero, Rect.zero);
                    Assert.Greater(wall, site.Params.Width * 2.0f,
                        $"({x},{y})'s front wall is barely there, so an empty doorway proves nothing");
                }
            }

            Assert.Greater(checkedCount, 40, "not enough buildings sampled");
        }

        /// <summary>
        /// A WINDOW IS A HOLE THROUGH THE WALL WITH GLASS IN IT.
        ///
        /// Three claims, and each one fails differently if it is wrong. The
        /// pane count has to match what WindowGrid says is glazed, or the
        /// geometry and the layout have drifted apart. No wall may stand inside
        /// a pane's own rectangle, or the opening was never cut and the glass
        /// is a sticker on concrete -- which is exactly the bug the Godot build
        /// reported as "you cant see the outside or inside through them" while
        /// the glass material itself was perfectly transparent. And the same
        /// has to hold for the INWARD wall, or a window is a recess rather than
        /// a way of seeing into a room.
        /// </summary>
        [Test]
        public void EveryGlazedCellIsARealOpeningWithAPaneInIt()
        {
            CityWorld world = new CityWorld(Seed);
            int panes = 0;
            int buildings = 0;

            for (int x = -8; x <= 8; x++)
            {
                for (int y = -8; y <= 8; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }
                    buildings++;

                    Build(x, y, out _, out MeshBuffer concrete, out MeshBuffer facade,
                        out MeshBuffer lit, out MeshBuffer dark);

                    // FOUR triangles a pane now (front + back, see
                    // AddDoubleSidedFace's own doc comment), not two -- 12
                    // indices, not 6.
                    int built = (lit.Triangles.Count + dark.Triangles.Count) / 12;
                    Assert.AreEqual(ExpectedPanes(world, site), built,
                        $"({x},{y}) glazed {ExpectedPanes(world, site)} cells but hung {built} panes");
                    panes += built;

                    // The wall at maximum Z, checked pane by pane. One wall is
                    // enough to catch a mismatch and keeps the sweep quick.
                    WindowPlan plan = WindowPlan.For(world, site);
                    CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                    CellLayout columns = plan.ColumnsFor(site.Params.Width);
                    float inset = WindowGrid.PaneInset;

                    for (int row = 1; row < rows.Count; row++)
                    {
                        for (int column = 0; column < columns.Count; column++)
                        {
                            if (!plan.IsGlazed(WindowGrid.Wall.North, column, row))
                            {
                                continue;
                            }

                            float cellX = (float)site.MinX + column * columns.Size;
                            float cellY = site.BaseHeight + row * rows.Size;
                            plan.Insets(WindowGrid.TopEdgeHasSlab(row, rows.Size, site.Params.Height),
                                out float insetBottom, out float insetTop);
                            var pane = Rect.MinMaxRect(
                                cellX + columns.Size * inset,
                                cellY + rows.Size * insetBottom,
                                cellX + columns.Size * (1.0f - inset),
                                cellY + rows.Size * (1.0f - insetTop));

                            float blocked = AreaInUprightRect(facade, (float)site.MaxZ, pane);
                            Assert.AreEqual(0.0f, blocked, 0.001f,
                                $"({x},{y}) has {blocked:0.000}m2 of facade across the window at " +
                                $"column {column}, row {row} -- the opening was never cut");

                            // A SKYBRIDGE CAN LEGITIMATELY STAND IN FRONT OF ONE OF THESE.
                            // BuildBridges lands a deck at a real floor boundary on the
                            // north wall (BuildSpan's own "EveryBridgeLandsOnAWholeFloor"
                            // contract), flush against this building's exterior at
                            // that exact Z -- and its box goes into the SAME buffer
                            // (flatConcrete, here "concrete") because a deck is
                            // concrete, not facade. Neither system knows about the
                            // other, so wherever a bridge's own floor happens to be a
                            // glazed row's floor too, the deck's flush end-cap sits
                            // right where this test is looking, on the inward side
                            // only -- BuildBridges never touches structures, which is
                            // why blocked (above) never sees it. Confirmed directly:
                            // dumping the triangles at (-6,-2) col 7 row 9 gave a box
                            // exactly BridgeWidth (4.0m) by BridgeThickness (0.6m),
                            // flush at site.MaxZ. That is a real deck landing on a
                            // real window, not a missed cut -- so the tolerance below
                            // is sized to one whole end-cap (4.0 * 0.6 = 2.4m2) rather
                            // than to the one site that happened to surface it, which
                            // is why a second, different building hit the identical
                            // number before this was understood. Anything past that
                            // is still a real regression: a whole uncut pane runs to
                            // several square metres at this building's own row height.
                            float inside = AreaInUprightRect(concrete, (float)site.MaxZ, pane);
                            Assert.AreEqual(0.0f, inside,
                                CityMeshBuilder.BridgeWidth * CityMeshBuilder.BridgeThickness + 0.01f,
                                $"({x},{y}) cut the outward wall at column {column}, row {row} but " +
                                "left the inward one solid -- you cannot see into the room");
                        }
                    }
                }
            }

            TestContext.WriteLine($"{panes} panes over {buildings} buildings");
            Assert.Greater(buildings, 20, "not enough buildings sampled");
            Assert.Greater(panes, 1000, "not enough glass sampled to mean anything");
        }

        /// <summary>
        /// THE DOOR FILLS THE HOLE THE WALL BUILDER CUT, exactly.
        ///
        /// Two numbers arrived at independently -- the opening comes from
        /// AddWallAround, the leaf from BuildingDoor -- and they have to agree
        /// or it shows immediately: a leaf narrower than its opening leaves a
        /// gap you can walk through with the door shut, and a wider one clips
        /// into the wall as it swings.
        ///
        /// It also hinges on the LEFT edge, which is what makes it swing inward
        /// clear of the opening rather than sweeping across it.
        /// </summary>
        [Test]
        public void EveryDoorLeafFillsItsOwnDoorway()
        {
            CityWorld world = new CityWorld(Seed);
            int checkedCount = 0;

            for (int x = -12; x <= 12; x++)
            {
                for (int y = -12; y <= 12; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }
                    checkedCount++;

                    BuildingDoor.Panel panel = BuildingDoor.PanelFor(site);

                    Assert.AreEqual(BuildingInterior.DoorWidth, panel.Width, 0.001f);
                    Assert.AreEqual(BuildingInterior.DoorHeight(site.Params.Height),
                        panel.Height, 0.001f, "the leaf is not the height of its own opening");

                    // Hinged on the opening's left edge, standing in the
                    // entrance wall, on the lobby floor.
                    float half = BuildingInterior.DoorWidth * 0.5f;
                    Assert.AreEqual((float)site.CentreX - half, panel.Hinge.x, 0.001f,
                        "the door does not hinge on the left edge of its opening");
                    Assert.AreEqual((float)site.MaxZ, panel.Hinge.z, 0.001f,
                        "the door is not in the entrance wall");
                    Assert.AreEqual(site.BaseHeight, panel.Hinge.y, 0.001f,
                        "the door does not stand on the lobby floor");

                    // The leaf reaches exactly across, no further.
                    Assert.AreEqual((float)site.CentreX + half, panel.Hinge.x + panel.Width, 0.001f);
                    Assert.LessOrEqual(panel.Hinge.y + panel.Height, site.Top,
                        "the door is taller than the building");
                }
            }

            Assert.Greater(checkedCount, 40, "not enough buildings sampled");
        }

        /// <summary>
        /// LANE PAINT GOES ON ROADS AND STAYS ON THEM.
        ///
        /// Two failures worth catching. Paint on a lot is paint on somebody's
        /// floor. And paint that reaches past the road onto the pavement is the
        /// giveaway that a crossing was sized against the chunk rather than
        /// against the street it crosses -- which is the bug Godot's own
        /// _add_crosswalks comment records fixing, where every band's outermost
        /// stripe overlapped the perpendicular band's.
        /// </summary>
        [Test]
        public void LanePaintOnlyEverLandsOnRoad()
        {
            CityWorld world = new CityWorld(Seed);
            SkybridgeLayout bridges = new SkybridgeLayout(Seed);
            int junctions = 0;
            int runs = 0;

            for (int x = -6; x <= 6; x++)
            {
                for (int y = -6; y <= 6; y++)
                {
                    var markings = new MeshBuffer();
                    CityMeshBuilder.BuildChunk(world, bridges, x, y, Vector3.zero,
                        new MeshBuffer(), new MeshBuffer(), new MeshBuffer(),
                        null, null, null, null, null, markings);

                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        Assert.IsTrue(markings.IsEmpty, $"({x},{y}) is a lot with lane paint on it");
                        continue;
                    }

                    // A car-free pedestrian zone gets no driving-lane paint
                    // at all -- see CityLayout.IsPedestrianZoneChunk's own
                    // doc comment ("nobody paints a driving lane down the
                    // middle of a street nobody is allowed to drive on"),
                    // the one legitimate exception to every other street
                    // chunk having some marking.
                    if (CityLayout.IsPedestrianZoneChunk(x, y))
                    {
                        continue;
                    }

                    Assert.IsFalse(markings.IsEmpty, $"({x},{y}) is a street with no markings");

                    bool junction = CityLayout.IsStreetChunk(x - 1, y) && CityLayout.IsStreetChunk(x + 1, y) &&
                                    CityLayout.IsStreetChunk(x, y - 1) && CityLayout.IsStreetChunk(x, y + 1);
                    if (junction)
                    {
                        junctions++;
                    }
                    else
                    {
                        runs++;
                    }

                    float chunk = CityLayout.ChunkSize;
                    float minX = x * chunk;
                    float minZ = y * chunk;

                    // Paint now tilts with its street (see BuildGround's own
                    // "hillside streets" comment), and EVERY marking -- a
                    // junction's crossings included -- samples
                    // ContinuousHeightAt DIRECTLY at its own real position
                    // rather than a coarse bilinear interpolation of the
                    // chunk's own 4 corners. EXPLICIT BUG REPORT (with a
                    // screenshot): "some decals... are still not sloping
                    // with the roads" -- the road surface under a junction's
                    // own crossings already samples the real terrain this
                    // finely (AddCurvingStreetGround, unconditional for
                    // every street chunk), so paint using the coarser
                    // bilinear approximation could visibly diverge from it
                    // wherever the real terrain isn't perfectly flat between
                    // the chunk's 4 corners.
                    foreach (Vector3 v in markings.Vertices)
                    {
                        double expectedY = world.Terrain.ContinuousHeightAt(v.x, v.z) + CityMeshBuilder.MarkingLift;
                        Assert.AreEqual(expectedY, v.y, 0.01,
                            $"({x},{y}) paint at {v} is not lying on the road");
                        // A curving straight run's own paint can wobble up
                        // to StreetCurveAmplitudeMetres sideways within its
                        // corridor -- generous bounds instead of the exact
                        // chunk edge for that case; a junction's crossings
                        // are unaffected and keep the exact bound.
                        float lateralSlack = junction ? 0.001f : CityMeshBuilder.StreetCurveAmplitudeMetres + 0.1f;
                        Assert.GreaterOrEqual(v.x, minX - lateralSlack);
                        Assert.LessOrEqual(v.x, minX + chunk + lateralSlack);
                        Assert.GreaterOrEqual(v.z, minZ - lateralSlack);
                        Assert.LessOrEqual(v.z, minZ + chunk + lateralSlack);

                        // The crossings are sized against the street they cross,
                        // so nothing may reach out onto a pavement.
                        if (!junction)
                        {
                            continue;
                        }
                        bool acrossX = v.x > minX + CityMeshBuilder.SidewalkWidth - 0.001f &&
                                       v.x < minX + chunk - CityMeshBuilder.SidewalkWidth + 0.001f;
                        bool acrossZ = v.z > minZ + CityMeshBuilder.SidewalkWidth - 0.001f &&
                                       v.z < minZ + chunk - CityMeshBuilder.SidewalkWidth + 0.001f;
                        Assert.IsTrue(acrossX || acrossZ,
                            $"({x},{y}) has a crossing stripe at {v} out in the corner");
                    }
                }
            }

            TestContext.WriteLine($"{runs} straight runs, {junctions} junctions");
            Assert.Greater(junctions, 3, "not enough junctions sampled");
            Assert.Greater(runs, 20, "not enough straight street sampled");
        }

        /// <summary>
        /// A BROKEN WINDOW STAYS BROKEN, AND THE HOLE STAYS A HOLE.
        ///
        /// The dangerous failure here is not that the glass fails to change --
        /// that is visible instantly. It is that the OPENING gets rebuilt along
        /// with it: the wall is cut around each glazed cell, so a builder that
        /// consulted damage in the wrong place would reinstate solid concrete
        /// where a shattered window used to be, and the window a player just
        /// smashed would become a wall they cannot walk through.
        /// </summary>
        [Test]
        public void BreakingAWindowChangesTheGlassAndNotTheHole()
        {
            CityWorld world = new CityWorld(Seed);
            SkybridgeLayout bridges = new SkybridgeLayout(Seed);

            (int x, int y) = FirstBuilding(world);
            BuildingSite site = world.BuildingAt(x, y);
            var damage = new GlassDamage();

            int wallBefore = BuildWith(world, bridges, x, y, damage,
                out int litBefore, out int darkBefore, out int crackedBefore);
            Assert.AreEqual(0, crackedBefore, "an untouched city has cracked glass in it");
            Assert.Greater(litBefore + darkBefore, 0, "this building has no glass to break");

            // Find a window that is really there, and hit it.
            PaneKey key = AnyPaneOn(world, site);
            Assert.AreEqual(PaneState.Cracked, damage.Hit(key));

            // One quad, which is six indices -- these are index counts, not
            // triangle counts, and the difference cost a confused test run.
            const int Quad = 6;

            // A PANE IS TWO QUADS NOW, FRONT AND BACK -- EXPLICIT BUG REPORT:
            // "when shooting windows from inside the building the are not
            // breaking." AddDoubleSidedFace adds a back-facing copy of every
            // plain pane so the shared MeshCollider has a triangle to hit
            // from either side (queriesHitBackfaces is off project-wide, see
            // AddDoubleSidedFace's own doc comment) -- doubling the index
            // count this test already tracked per pane.
            const int Pane = Quad * 2;

            var openings = new List<CityMeshBuilder.CrackedOpening>();
            int wallCracked = BuildWith(world, bridges, x, y, damage,
                out int litCracked, out int darkCracked, out int crackedNow, openings);

            // The opening a body can climb through. One per cracked pane, at
            // the pane, and with real depth -- a trigger with no thickness is
            // stepped over between two physics frames at a run.
            Assert.AreEqual(1, openings.Count, "a cracked pane got no opening, or several");
            Assert.AreEqual(key, openings[0].Key);
            Assert.GreaterOrEqual(openings[0].Size.x, CityMeshBuilder.CrackedOpeningDepth - 0.001f);
            Assert.GreaterOrEqual(openings[0].Size.z, CityMeshBuilder.CrackedOpeningDepth - 0.001f);
            Assert.Greater(openings[0].Size.y, 0.5f, "the opening is not window-shaped");
            Assert.AreEqual(Pane, crackedNow,
                "the cracked pane is not in the cracked buffer, or took something with it");
            Assert.AreEqual(litBefore + darkBefore - Pane, litCracked + darkCracked,
                "the pane left the intact buffers as exactly one quad");
            Assert.AreEqual(wallBefore, wallCracked,
                "cracking a window changed the wall around it");

            Assert.AreEqual(PaneState.Shattered, damage.Hit(key));

            int wallGone = BuildWith(world, bridges, x, y, damage,
                out int litGone, out int darkGone, out int crackedGone);
            Assert.AreEqual(0, crackedGone, "a shattered pane is still drawn");
            Assert.AreEqual(litBefore + darkBefore - Pane, litGone + darkGone);
            Assert.AreEqual(wallBefore, wallGone,
                "the wall grew back over a window somebody smashed");
        }

        /// <summary>
        /// EXPLICIT ASK: "when you shoot the glass make it shatter but also
        /// have a hole in it where you shot." A cracked pane with a KNOWN
        /// impact point (GlassDamage.Hit's own optional second argument)
        /// frames a real opening -- four quads, not one -- unlike the case
        /// above, where an impact-less Hit still frosts the whole rectangle
        /// flat because nothing ever recorded where to put a hole.
        /// </summary>
        [Test]
        public void AShotThatCracksAWindowLeavesARealHoleAtTheImpactPoint()
        {
            CityWorld world = new CityWorld(Seed);
            SkybridgeLayout bridges = new SkybridgeLayout(Seed);

            (int x, int y) = FirstBuilding(world);
            BuildingSite site = world.BuildingAt(x, y);
            var damage = new GlassDamage();

            PaneKey key = AnyPaneOn(world, site);
            Assert.AreEqual(PaneState.Cracked, damage.Hit(key, new Vector2(0.5f, 0.5f)));

            // One quad, which is six indices, framed into four -- see the
            // test above for why this is index count, not triangle count.
            const int Quad = 6;

            var decals = new MeshBuffer();
            BuildWith(world, bridges, x, y, damage,
                out int lit, out int dark, out int cracked, crackDecals: decals);

            // EACH OF THE FOUR FRAME PANELS IS ALSO DOUBLE-SIDED NOW, same
            // reason (and same AddDoubleSidedFace) as the plain pane in the
            // test above -- Quad * 4 * 2, not Quad * 4.
            Assert.AreEqual(Quad * 4 * 2, cracked,
                "a shot pane with a known impact point should frame a hole, not draw one flat quad");

            // EXPLICIT ASK: replace the plain square hole with a real crack-
            // texture decal (BulletHoleCrack.png) on top of it -- one quad,
            // in its own buffer so it can sit proud of the base glass
            // without the two systems' alpha blending z-fighting.
            Assert.AreEqual(Quad, decals.Triangles.Count,
                "a shot pane with a known impact point should get exactly one crack decal quad");
        }

        /// <summary>
        /// EXPLICIT BUG REPORT: "only 1 bullet hole appears even if you shoot more than 1. each bullet hole should
        /// appear." Every bullet in a pane gets its own opening and its own crack pattern: N shots, N decals, and
        /// each new hole takes the same amount of glass out of the pane.
        /// </summary>
        [Test]
        public void EveryBulletInAPaneLeavesItsOwnHoleAndItsOwnCrackDecal()
        {
            CityWorld world = new CityWorld(Seed);
            SkybridgeLayout bridges = new SkybridgeLayout(Seed);

            (int x, int y) = FirstBuilding(world);
            BuildingSite site = world.BuildingAt(x, y);
            PaneKey key = AnyPaneOn(world, site);

            Vector2[] shots = { new Vector2(0.25f, 0.3f), new Vector2(0.6f, 0.7f), new Vector2(0.4f, 0.5f), new Vector2(0.75f, 0.25f) };

            var areas = new List<float>();
            var decalQuads = new List<int>();

            var plain = new GlassDamage();
            plain.Set(key, PaneState.Cracked);   // cracked, no known impact: the whole pane, no hole
            areas.Add(CrackedArea(world, bridges, x, y, plain, out _));
            decalQuads.Add(0);

            var damage = new GlassDamage();
            foreach (Vector2 shot in shots)
            {
                damage.Shoot(key, shot, out bool changed);
                Assert.IsTrue(changed);
                areas.Add(CrackedArea(world, bridges, x, y, damage, out int decals));
                decalQuads.Add(decals);
            }

            for (int i = 1; i < areas.Count; i++)
            {
                Assert.AreEqual(i, decalQuads[i], $"after {i} bullets the window shows {decalQuads[i]} crack decals");
                Assert.Less(areas[i], areas[i - 1] - 0.0005f, $"bullet {i} took no glass out of the pane");
            }

            // Each hole is the same size, so each one removes the same area (holes here do not overlap).
            float firstHole = areas[0] - areas[1];
            Assert.Greater(firstHole, 0.0f);
            for (int i = 2; i < areas.Count; i++)
            {
                Assert.AreEqual(firstHole, areas[i - 1] - areas[i], firstHole * 0.05f, $"bullet {i}'s hole is a different size to the first");
            }
        }

        [Test]
        public void TwoBulletsInTheSameSpotStillLeaveAWellFormedPane()
        {
            CityWorld world = new CityWorld(Seed);
            SkybridgeLayout bridges = new SkybridgeLayout(Seed);
            (int x, int y) = FirstBuilding(world);
            PaneKey key = AnyPaneOn(world, world.BuildingAt(x, y));

            var damage = new GlassDamage();
            damage.Shoot(key, new Vector2(0.5f, 0.5f), out _);
            float one = CrackedArea(world, bridges, x, y, damage, out _);
            damage.Shoot(key, new Vector2(0.5f, 0.5f), out _);   // exactly on top of the first
            float two = CrackedArea(world, bridges, x, y, damage, out int decals);

            Assert.AreEqual(one, two, one * 0.001f, "overlapping holes must not cut the glass twice");
            Assert.AreEqual(2, decals);
        }

        /// <summary>The glass area (both faces) of every cracked pane in a chunk, and how many crack decals it has.</summary>
        private static float CrackedArea(CityWorld world, SkybridgeLayout bridges, int x, int y, GlassDamage damage, out int decalQuads)
        {
            var ground = new MeshBuffer();
            var pavement = new MeshBuffer();
            var structures = new MeshBuffer();
            var cracked = new MeshBuffer();
            var decals = new MeshBuffer();
            CityMeshBuilder.BuildChunk(world, bridges, x, y, Vector3.zero,
                ground, pavement, structures, new MeshBuffer(), new MeshBuffer(), cracked, damage, null,
                crackDecals: decals);

            float area = 0.0f;
            for (int t = 0; t + 2 < cracked.Triangles.Count; t += 3)
            {
                Vector3 a = cracked.Vertices[cracked.Triangles[t]];
                Vector3 b = cracked.Vertices[cracked.Triangles[t + 1]];
                Vector3 c = cracked.Vertices[cracked.Triangles[t + 2]];
                area += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            }
            decalQuads = decals.Triangles.Count / 6;
            return area;
        }

        /// <summary>Builds one chunk against a damage record and counts triangles.</summary>
        private static int BuildWith(CityWorld world, SkybridgeLayout bridges, int x, int y,
            GlassDamage damage, out int lit, out int dark, out int cracked,
            List<CityMeshBuilder.CrackedOpening> openings = null, MeshBuffer crackDecals = null)
        {
            var ground = new MeshBuffer();
            var pavement = new MeshBuffer();
            var structures = new MeshBuffer();
            var litGlass = new MeshBuffer();
            var darkGlass = new MeshBuffer();
            var crackedGlass = new MeshBuffer();

            CityMeshBuilder.BuildChunk(world, bridges, x, y, Vector3.zero,
                ground, pavement, structures, litGlass, darkGlass, crackedGlass, damage, openings,
                crackDecals: crackDecals);

            lit = litGlass.Triangles.Count;
            dark = darkGlass.Triangles.Count;
            cracked = crackedGlass.Triangles.Count;
            return structures.Triangles.Count;
        }

        private static (int, int) FirstBuilding(CityWorld world)
        {
            for (int x = -8; x <= 8; x++)
            {
                for (int y = -8; y <= 8; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (site.Exists && site.AnchorX == x && site.AnchorY == y &&
                        site.Params.Width > 14.0f && HasWindowOnDoorWall(world, site))
                    {
                        return (x, y);
                    }
                }
            }
            Assert.Fail("no building near the origin");
            return (0, 0);
        }

        /// <summary>Whether the door wall has a window above the ground floor -- a blank shed or a party-wall building may not.</summary>
        private static bool HasWindowOnDoorWall(CityWorld world, BuildingSite site)
        {
            WindowPlan plan = WindowPlan.For(world, site);
            CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
            CellLayout columns = plan.ColumnsFor(site.Params.Width);
            for (int row = 1; row < rows.Count; row++)
            {
                for (int column = 0; column < columns.Count; column++)
                {
                    if (plan.IsGlazed(WindowGrid.Wall.North, column, row))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static PaneKey AnyPaneOn(CityWorld world, BuildingSite site)
        {
            WindowPlan plan = WindowPlan.For(world, site);
            CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
            CellLayout columns = plan.ColumnsFor(site.Params.Width);

            for (int row = 1; row < rows.Count; row++)
            {
                for (int column = 0; column < columns.Count; column++)
                {
                    if (plan.IsGlazed(WindowGrid.Wall.North, column, row))
                    {
                        return new PaneKey(site.AnchorX, site.AnchorY,
                            WindowGrid.Wall.North, column, row);
                    }
                }
            }
            Assert.Fail("this building's front wall has no windows at all");
            return default;
        }

        /// <summary>How many cells WindowGrid glazes on a building, doorway aside.</summary>
        private static int ExpectedPanes(CityWorld world, BuildingSite site)
        {
            CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
            WindowPlan plan = WindowPlan.For(world, site);
            float centreX = (float)site.CentreX;
            float doorU0 = centreX - BuildingInterior.DoorWidth * 0.5f;
            float doorU1 = centreX + BuildingInterior.DoorWidth * 0.5f;

            int total = 0;
            foreach ((WindowGrid.Wall wall, float span) in new[]
                     {
                         (WindowGrid.Wall.West, site.Params.Depth),
                         (WindowGrid.Wall.East, site.Params.Depth),
                         (WindowGrid.Wall.South, site.Params.Width),
                         (WindowGrid.Wall.North, site.Params.Width),
                     })
            {
                CellLayout columns = plan.ColumnsFor(span);
                for (int row = 0; row < rows.Count; row++)
                {
                    for (int column = 0; column < columns.Count; column++)
                    {
                        if (!plan.IsGlazed(wall, column, row))
                        {
                            continue;
                        }
                        // The entrance row of the door wall loses whatever
                        // cells the opening passes through.
                        if (wall == WindowGrid.Wall.North && row == 0)
                        {
                            float cellU0 = (float)site.MinX + column * columns.Size;
                            if (cellU0 + columns.Size > doorU0 && cellU0 < doorU1)
                            {
                                continue;
                            }
                        }
                        total++;
                    }
                }
            }
            return total;
        }

        /// <summary>
        /// Area of geometry standing in an upright rectangle on a wall at fixed
        /// Z. <paramref name="area"/> is in X and Y, unlike
        /// <see cref="AreaInPlane"/>'s plan rectangles.
        ///
        /// Faces BOTH ways, because the outward and inward copies of a wall sit
        /// in the same plane with opposite normals, and asking about one of
        /// them means not filtering the other out by accident.
        /// </summary>
        private static float AreaInUprightRect(MeshBuffer buffer, float atZ, Rect area)
        {
            float total = 0.0f;
            for (int t = 0; t + 2 < buffer.Triangles.Count; t += 3)
            {
                Vector3 a = buffer.Vertices[buffer.Triangles[t]];
                Vector3 b = buffer.Vertices[buffer.Triangles[t + 1]];
                Vector3 c = buffer.Vertices[buffer.Triangles[t + 2]];
                if (Mathf.Abs(a.z - atZ) > 0.001f || Mathf.Abs(b.z - atZ) > 0.001f ||
                    Mathf.Abs(c.z - atZ) > 0.001f)
                {
                    continue;
                }

                Vector3 centroid = (a + b + c) / 3.0f;
                if (centroid.x <= area.xMin || centroid.x >= area.xMax ||
                    centroid.y <= area.yMin || centroid.y >= area.yMax)
                {
                    continue;
                }
                total += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            }
            return total;
        }

        /// <summary>
        /// NO GROUND UNDER A BUILDING. A building's lobby floor sits at
        /// BaseHeight and on level ground so does the chunk's own surface, so
        /// leaving both means two coplanar upward faces at the same height --
        /// which is not a floor, it is a z-fight you only notice once you can
        /// walk in and stand on it.
        /// </summary>
        [Test]
        public void TheGroundIsCutAwayUnderEveryBuilding()
        {
            CityWorld world = new CityWorld(Seed);
            int checkedCount = 0;

            for (int x = -14; x <= 14; x++)
            {
                for (int y = -14; y <= 14; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists)
                    {
                        continue;
                    }
                    checkedCount++;

                    Build(x, y, out MeshBuffer ground, out _);
                    Rect plan = Rect.MinMaxRect((float)site.MinX, (float)site.MinZ,
                        (float)site.MaxX, (float)site.MaxZ);

                    for (int t = 0; t + 2 < ground.Triangles.Count; t += 3)
                    {
                        // Only the walkable surface. Retaining walls are
                        // vertical and a 2x2 building covers its own internal
                        // chunk edges, so a skirt buried under one is normal.
                        if (ground.Normals[ground.Triangles[t]].y < 0.5f)
                        {
                            continue;
                        }
                        Vector3 a = ground.Vertices[ground.Triangles[t]];
                        Vector3 b = ground.Vertices[ground.Triangles[t + 1]];
                        Vector3 c = ground.Vertices[ground.Triangles[t + 2]];
                        Vector3 centroid = (a + b + c) / 3.0f;
                        Assert.IsFalse(Covers(plan, centroid),
                            $"chunk ({x},{y}) still has ground at {centroid} inside the footprint "
                            + $"of the building at ({site.AnchorX},{site.AnchorY})");
                    }
                }
            }

            Assert.Greater(checkedCount, 200, "not enough built-on chunks sampled");
        }

        /// <summary>
        /// A FLOOR TO STAND ON AT EVERY STOREY, and the roof opened for the
        /// top flight. Without the opening the last staircase climbs into a
        /// solid ceiling and the roof is unreachable.
        /// </summary>
        [Test]
        public void EveryStoreyGetsAFloorAndTheRoofGetsItsStairwell()
        {
            CityWorld world = new CityWorld(Seed);
            int checkedCount = 0;

            for (int x = -12; x <= 12; x++)
            {
                for (int y = -12; y <= 12; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }
                    checkedCount++;

                    Build(x, y, out _, out MeshBuffer concrete, out _);

                    float height = site.Params.Height;
                    float storey = BuildingInterior.StoreyHeight(height);
                    int floors = BuildingInterior.FloorCountFor(height);
                    float footprint = site.Params.Width * site.Params.Depth;

                    var plan = Rect.MinMaxRect((float)site.MinX, (float)site.MinZ,
                        (float)site.MaxX, (float)site.MaxZ);

                    float run = BuildingInterior.RunFor(
                        site.Params.Width * 0.5f, site.Params.Depth * 0.5f);

                    // The lobby, whole except where a basement is reached
                    // through it. ABasementIsARoomWithAHoleInTheLobbyAboveIt
                    // owns that opening; here it is only subtracted so the
                    // floor's own area still adds up.
                    Rect cellar = Rect.zero;
                    if (site.Params.HasBasement)
                    {
                        Rect local = BuildingInterior.StairwellFor(
                            BuildingInterior.BasementStairCorner,
                            site.Params.Width * 0.5f, site.Params.Depth * 0.5f, run).Hole;
                        cellar = new Rect(local.x + (float)site.CentreX,
                            local.y + (float)site.CentreZ, local.width, local.height);
                    }
                    Assert.AreEqual(footprint - cellar.width * cellar.height,
                        AreaInPlane(concrete, site.BaseHeight, 1, plan, cellar), 0.05f,
                        $"({x},{y}) has no lobby floor -- you walk in the door and fall down "
                        + "the inside of the podium");

                    for (int floor = 0; floor < floors; floor++)
                    {
                        float top = site.BaseHeight
                                    + BuildingInterior.FloorTop(floor, floors, height, storey);
                        Rect local = BuildingInterior.StairwellFor(
                            floor, site.Params.Width * 0.5f, site.Params.Depth * 0.5f, run).Hole;
                        var hole = new Rect(
                            local.x + (float)site.CentreX, local.y + (float)site.CentreZ,
                            local.width, local.height);

                        // THE OPENING IS EXCLUDED, not just subtracted. The
                        // topmost step's tread lands flush with the storey
                        // above -- that is the landing you step off onto --
                        // so it lies in this exact plane, inside the hole.
                        // Measuring the slab means measuring outside it.
                        Assert.AreEqual(footprint - hole.width * hole.height,
                            AreaInPlane(concrete, top, 1, plan, hole), 0.05f,
                            $"({x},{y}) floor {floor}'s ceiling is not the footprint with its "
                            + "stairwell taken out of it");
                    }
                }
            }

            Assert.Greater(checkedCount, 40, "not enough buildings sampled");
        }

        /// <summary>
        /// A BASEMENT IS A REAL ROOM WITH A WAY IN, and a building without one
        /// still has an unbroken lobby floor. The second half matters as much
        /// as the first: a hole cut in every lobby would be a pit in nine
        /// buildings out of ten.
        /// </summary>
        [Test]
        public void ABasementIsARoomWithAHoleInTheLobbyAboveIt()
        {
            CityWorld world = new CityWorld(Seed);
            int cellars = 0;
            int plain = 0;

            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }

                    Build(x, y, out _, out MeshBuffer concrete, out _);
                    var plan = Rect.MinMaxRect((float)site.MinX, (float)site.MinZ,
                        (float)site.MaxX, (float)site.MaxZ);
                    float footprint = site.Params.Width * site.Params.Depth;
                    float storey = BuildingInterior.StoreyHeight(site.Params.Height);
                    float run = BuildingInterior.RunFor(
                        site.Params.Width * 0.5f, site.Params.Depth * 0.5f);
                    Rect hole = BuildingInterior.StairwellFor(BuildingInterior.BasementStairCorner,
                        site.Params.Width * 0.5f, site.Params.Depth * 0.5f, run).Hole;
                    float opening = hole.width * hole.height;

                    if (!site.Params.HasBasement)
                    {
                        plain++;
                        Assert.AreEqual(footprint,
                            AreaInPlane(concrete, site.BaseHeight, 1, plan, Rect.zero), 0.05f,
                            $"({x},{y}) has no basement but its lobby floor is not solid");
                        Assert.IsFalse(HasSurfaceBelow(concrete, plan, site.BaseHeight - 0.5f),
                            $"({x},{y}) has no basement but built geometry under its lobby");
                        continue;
                    }

                    cellars++;
                    var world_hole = new Rect(hole.x + (float)site.CentreX,
                        hole.y + (float)site.CentreZ, hole.width, hole.height);
                    Assert.AreEqual(footprint - opening,
                        AreaInPlane(concrete, site.BaseHeight, 1, plan, world_hole), 0.05f,
                        $"({x},{y}) has a basement but no way down into it");

                    // And a floor at the bottom of it, or the way down is a pit.
                    Assert.AreEqual(footprint,
                        AreaInPlane(concrete, site.BaseHeight - storey, 1, plan, Rect.zero), 0.05f,
                        $"({x},{y})'s basement has no floor");
                }
            }

            TestContext.WriteLine($"{cellars} basements, {plain} buildings without one");
            Assert.Greater(cellars, 5, "no basements in the sweep, so this proved nothing");
            Assert.Greater(plain, cellars * 4, "basements are supposed to be rare");
        }

        /// <summary>
        /// Is there any geometry under this footprint below a height?
        ///
        /// STRICTLY INSIDE the footprint: a vertex ON its edge belongs to the
        /// wall, not to anything under the lobby. An entrance staircase that
        /// descends to a street below a multi-chunk building's raised floor
        /// has its lowest riser exactly on the wall plane -- a building whose
        /// door row sits on a lower terrace is normal (see BuildBuilding's own
        /// doorRowPad), and this check only exists to catch geometry built
        /// UNDER the floor.
        /// </summary>
        private static bool HasSurfaceBelow(MeshBuffer buffer, Rect plan, float below)
        {
            const float wallInset = 0.05f;
            plan = Rect.MinMaxRect(plan.xMin + wallInset, plan.yMin + wallInset,
                plan.xMax - wallInset, plan.yMax - wallInset);
            for (int i = 0; i < buffer.Vertices.Count; i++)
            {
                Vector3 v = buffer.Vertices[i];
                if (v.y < below && Covers(plan, v))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// EVERY ROOF TYPE THE GENERATOR ROLLS ACTUALLY BUILDS SOMETHING.
        ///
        /// The roof was picked, stored and asserted against its own table for
        /// a long time while nothing read it, so every rooftop in the city was
        /// a bare plane and no test noticed -- the data was perfect and unused.
        /// This checks the other end: a building whose roof is not flat has
        /// geometry standing above its own roofline, and a flat one does not.
        /// </summary>
        [Test]
        public void EveryRoofTypeBuildsSomethingOnTheRoof()
        {
            CityWorld world = new CityWorld(Seed);
            var seen = new Dictionary<string, int>();

            // Roofs are picked per ZONE (a downtown bristles with antennas, an
            // industrial estate with chimneys and sheds), so a window near the
            // origin sees only some of the table. These windows span the
            // zones: the downtown at spawn plus residential, industrial and
            // outskirts ground.
            foreach (var (windowX, windowY) in new[] { (0, 0), (140, 140), (-140, 140), (140, -140), (-280, -140), (280, 140) })
            for (int x = windowX - 12; x <= windowX + 12; x++)
            {
                for (int y = windowY - 12; y <= windowY + 12; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }

                    Build(x, y, out _, out MeshBuffer concrete, out _);

                    // WIDENED BY A HAIR, because a parapet is a rim wall flush
                    // with the roof edge: every one of its vertices sits
                    // exactly ON the footprint boundary in at least one axis,
                    // and strict containment throws all of them away. The
                    // nearest neighbouring building is metres off, so this
                    // cannot pick up anything but this roof.
                    const float edge = 0.01f;
                    var plan = Rect.MinMaxRect((float)site.MinX - edge, (float)site.MinZ - edge,
                        (float)site.MaxX + edge, (float)site.MaxZ + edge);

                    // Anything standing proud of the roof, inside the
                    // footprint. Skybridges land below the roofline and other
                    // buildings are elsewhere, so this is the props alone.
                    bool anythingUpThere = false;
                    foreach (Vector3 v in concrete.Vertices)
                    {
                        if (v.y > site.Top + 0.01f && Covers(plan, v))
                        {
                            anythingUpThere = true;
                            break;
                        }
                    }

                    string roof = string.IsNullOrEmpty(site.Params.Roof) ? "flat" : site.Params.Roof;
                    seen.TryGetValue(roof, out int count);
                    seen[roof] = count + 1;

                    if (roof == "flat")
                    {
                        Assert.IsFalse(anythingUpThere,
                            $"({x},{y}) has a flat roof but something is standing on it");
                    }
                    else
                    {
                        Assert.IsTrue(anythingUpThere,
                            $"({x},{y}) rolled a '{roof}' roof and nothing was built for it");
                    }
                }
            }

            TestContext.WriteLine(string.Join(", ",
                seen.Select(pair => $"{pair.Key} {pair.Value}")));

            // Every type in the table has to actually turn up, or this could
            // pass while one of them silently built nothing.
            foreach (string type in CityChunkGenerator.RoofTypes)
            {
                string roof = string.IsNullOrEmpty(type) ? "flat" : type;
                Assert.IsTrue(seen.ContainsKey(roof),
                    $"no building in the sweep rolled a '{roof}' roof, so it went unchecked");
            }
        }

        /// <summary>
        /// What the staircases actually cost, pinned rather than guessed at.
        ///
        /// THE TOP FLOOR SWALLOWS WHATEVER THE FLOOR CAP LEFT OVER, which is
        /// city_chunk.gd's own rule and is carried across unchanged. Nearly two
        /// thirds of this city hits that cap, so nearly two thirds of its
        /// buildings have a top floor several times taller than any other, and
        /// a flight of steps that climbs all of it at a real 0.25m riser. That
        /// is where almost all of this number comes from. It is pinned here so
        /// that changing the rule -- which is a design call, not a porting one
        /// -- shows up as a number moving rather than as a vague feeling that
        /// the city got heavier.
        /// </summary>
        [Test]
        public void TheStairsAreAKnownCostRatherThanASurprise()
        {
            CityWorld world = new CityWorld(Seed);
            long steps = 0;
            int buildings = 0;
            int capped = 0;
            int worst = 0;

            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }
                    buildings++;

                    float height = site.Params.Height;
                    float storey = BuildingInterior.StoreyHeight(height);
                    int floors = BuildingInterior.FloorCountFor(height);
                    if (floors == BuildingInterior.MaxFloors)
                    {
                        capped++;
                    }

                    int here = 0;
                    for (int floor = 0; floor < floors; floor++)
                    {
                        here += BuildingInterior.StepsFor(
                            BuildingInterior.FloorTop(floor, floors, height, storey) - floor * storey);
                    }
                    steps += here;
                    worst = Mathf.Max(worst, here);
                }
            }

            float mean = steps / (float)buildings;
            TestContext.WriteLine($"{buildings} buildings, {capped} at the floor cap");
            TestContext.WriteLine($"{steps} steps, {mean:0} per building, worst {worst}");

            Assert.Greater(capped / (float)buildings, 0.5f,
                "most of this city used to hit the floor cap; if that stopped being true the "
                + "top-floor rule is worth another look");
            Assert.Less(mean, 400.0f,
                $"a building averages {mean:0} steps, which is {mean * 10:0} triangles of "
                + "staircase each -- something made the top floor taller still");
        }

        /// <summary>
        /// Area of every face lying in one axis plane and facing along it.
        /// <paramref name="axis"/> is 0 for X, 1 for Y, 2 for Z.
        ///
        /// Both rectangles are in X and Z, and an empty one means no filter.
        /// <paramref name="within"/> keeps the measurement to one building --
        /// skybridges land on whole storeys, so a neighbour's walkway deck can
        /// sit in the exact plane of this building's floor and be counted as
        /// floor. <paramref name="exclude"/> drops the stairwell opening.
        /// </summary>
        private static float AreaInPlane(MeshBuffer buffer, float at, int axis,
            Rect within, Rect exclude)
        {
            float total = 0.0f;
            for (int t = 0; t + 2 < buffer.Triangles.Count; t += 3)
            {
                Vector3 a = buffer.Vertices[buffer.Triangles[t]];
                Vector3 b = buffer.Vertices[buffer.Triangles[t + 1]];
                Vector3 c = buffer.Vertices[buffer.Triangles[t + 2]];
                if (Mathf.Abs(a[axis] - at) > 0.001f ||
                    Mathf.Abs(b[axis] - at) > 0.001f ||
                    Mathf.Abs(c[axis] - at) > 0.001f)
                {
                    continue;
                }

                // Only the side that faces out of the plane, or a slab's own
                // underside would be counted along with its top.
                if (buffer.Normals[buffer.Triangles[t]][axis] <= 0.0f)
                {
                    continue;
                }

                Vector3 centroid = (a + b + c) / 3.0f;
                if (Covers(exclude, centroid) || (within.width > 0.0f && !Covers(within, centroid)))
                {
                    continue;
                }
                total += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            }
            return total;
        }

        private static bool Covers(Rect area, Vector3 point)
        {
            return area.width > 0.0f &&
                   point.x > area.xMin && point.x < area.xMax &&
                   point.z > area.yMin && point.z < area.yMax;
        }

        private static Bounds BoundsOf(MeshBuffer buffer)
        {
            Bounds bounds = new Bounds(buffer.Vertices[0], Vector3.zero);
            foreach (Vector3 v in buffer.Vertices)
            {
                bounds.Encapsulate(v);
            }
            return bounds;
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
                   !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }
    }
}
