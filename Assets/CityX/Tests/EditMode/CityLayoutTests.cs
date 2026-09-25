using System.Collections.Generic;
using CityX.World;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The street network: irregular arterial lines, evenly spread local
    /// streets, highways and exits.
    ///
    /// NO LONGER A PARITY TEST AGAINST GODOT. This file used to compare the
    /// grid to a dump of city_chunk.gd's own (a street every fourth chunk,
    /// forever). EXPLICIT ASK behind the districts and now the irregular
    /// arterials: "not everything should be a blocky grid." A grid that
    /// still matched Godot chunk for chunk would be the very thing being
    /// asked to go away, so the golden comparison was deleted with the grid
    /// it described. What is checked instead is the set of guarantees the
    /// rest of the game leans on -- bounded gaps, blocks that are real
    /// rectangles, highways that are arterials, and answers that never
    /// depend on the order they are asked in.
    /// </summary>
    public class CityLayoutTests
    {
        [Test]
        public void TheOriginIsStillAnArterialCrossingWithAHighway()
        {
            Assert.IsTrue(CityLayout.IsArterialChunk(0, 0));
            Assert.IsTrue(CityLayout.IsHighwayChunk(0, 0));
            Assert.IsTrue(CityLayout.IsStreetChunk(0, 0));
            Assert.AreEqual(0, CityLayout.NearestArterialLine(0, xAxis: true));
            Assert.AreEqual(0, CityLayout.NearestArterialLine(0, xAxis: false));
        }

        /// <summary>
        /// Arterial lines are irregular -- that is the point -- but bounded:
        /// never closer than a district can be laid out in, never so far apart
        /// a district becomes a plain. And they exist in negative space too.
        /// </summary>
        [Test]
        public void ArterialLinesAreIrregularButAlwaysWithinTheirBounds()
        {
            foreach (bool xAxis in new[] { true, false })
            {
                // A coordinate IS an arterial line exactly when the nearest
                // line to it is itself.
                var lines = new List<int>();
                for (int c = -1500; c <= 1500; c++)
                {
                    if (CityLayout.NearestArterialLine(c, xAxis) == c)
                    {
                        lines.Add(c);
                    }
                }

                var gaps = new HashSet<int>();
                for (int i = 1; i < lines.Count; i++)
                {
                    int gap = lines[i] - lines[i - 1];
                    Assert.GreaterOrEqual(gap, CityLayout.MinDistrictChunks, $"arterials at {lines[i - 1]} and {lines[i]} are too close");
                    Assert.LessOrEqual(gap, CityLayout.MaxDistrictChunks, $"arterials at {lines[i - 1]} and {lines[i]} are too far apart");
                    gaps.Add(gap);
                }

                TestContext.WriteLine($"{(xAxis ? "X" : "Y")} axis: {lines.Count} arterials, {gaps.Count} distinct spacings");
                Assert.Greater(lines.Count, 100, "the sweep found almost no arterial lines");
                Assert.GreaterOrEqual(gaps.Count, 6, "arterial spacing is not irregular -- the districts are one repeated square again");
            }
        }

        /// <summary>
        /// A block is a genuine rectangle of buildable chunks bounded by
        /// streets, at least 2x2 and never bigger than the gap allows.
        /// </summary>
        [Test]
        public void EveryBuildableChunkBelongsToARealRectangularBlock()
        {
            for (int x = -150; x < 150; x++)
            {
                for (int y = -150; y < 150; y++)
                {
                    bool isBlock = CityLayout.TryGetBlockRect(x, y, out int minX, out int minY, out int width, out int height);
                    Assert.AreEqual(!CityLayout.IsStreetChunk(x, y), isBlock, $"({x},{y}) street/block disagreement");
                    if (!isBlock)
                    {
                        continue;
                    }

                    Assert.GreaterOrEqual(width, CityLayout.MinStreetGap - 1);
                    Assert.GreaterOrEqual(height, CityLayout.MinStreetGap - 1);
                    Assert.LessOrEqual(width, CityLayout.MaxStreetGap - 1);
                    Assert.LessOrEqual(height, CityLayout.MaxStreetGap - 1);
                    Assert.IsTrue(x >= minX && x < minX + width && y >= minY && y < minY + height, $"({x},{y}) is outside its own block");

                    // Walled by streets, and nothing inside is a street.
                    for (int i = -1; i <= width; i++)
                    {
                        Assert.IsTrue(CityLayout.IsStreetChunk(minX + i, minY - 1), $"block ({minX},{minY}) is open on its low Y edge at {minX + i}");
                        Assert.IsTrue(CityLayout.IsStreetChunk(minX + i, minY + height), $"block ({minX},{minY}) is open on its high Y edge at {minX + i}");
                    }
                    for (int j = 0; j < height; j++)
                    {
                        Assert.IsTrue(CityLayout.IsStreetChunk(minX - 1, minY + j), $"block ({minX},{minY}) is open on its low X edge at {minY + j}");
                        Assert.IsTrue(CityLayout.IsStreetChunk(minX + width, minY + j), $"block ({minX},{minY}) is open on its high X edge at {minY + j}");
                    }
                    // Corners of the interior are buildable.
                    Assert.IsFalse(CityLayout.IsStreetChunk(minX, minY));
                    Assert.IsFalse(CityLayout.IsStreetChunk(minX + width - 1, minY + height - 1));

                    // Every chunk of one block reports the same rectangle.
                    if (x == minX && y == minY)
                    {
                        CityLayout.TryGetBlockRect(minX + width - 1, minY + height - 1,
                            out int cornerX, out int cornerY, out int cornerW, out int cornerH);
                        Assert.AreEqual((minX, minY, width, height), (cornerX, cornerY, cornerW, cornerH),
                            $"opposite corners of block ({minX},{minY}) disagree about which block they are in");
                    }
                }
            }
        }

        [Test]
        public void StreetRunsAlongXAgreesWithWhichAxisMadeTheStreet()
        {
            int checkedCount = 0;
            for (int x = -120; x < 120; x++)
            {
                for (int y = -120; y < 120; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    // Only an unambiguous straight run: street on exactly two
                    // OPPOSITE sides. A T-junction or a corner has a street
                    // running each way and callers special-case those before
                    // asking (see StreetRunsAlongX's own doc comment).
                    bool north = CityLayout.IsStreetChunk(x, y + 1);
                    bool south = CityLayout.IsStreetChunk(x, y - 1);
                    bool east = CityLayout.IsStreetChunk(x + 1, y);
                    bool west = CityLayout.IsStreetChunk(x - 1, y);
                    bool northSouthOnly = north && south && !east && !west;
                    bool eastWestOnly = east && west && !north && !south;
                    if (!northSouthOnly && !eastWestOnly)
                    {
                        continue;
                    }
                    checkedCount++;
                    Assert.AreEqual(eastWestOnly, CityLayout.StreetRunsAlongX(x, y),
                        $"({x},{y}) runs {(eastWestOnly ? "east-west" : "north-south")} but StreetRunsAlongX says otherwise");
                }
            }
            Assert.Greater(checkedCount, 1000, "too few straight street chunks were checked");
        }

        /// <summary>
        /// The cache in InfoFor must never change an answer: asking in a
        /// different order, or after thrashing the cache with a distant
        /// district, gives the same street map.
        /// </summary>
        [Test]
        public void AnswersDoNotDependOnTheOrderTheyAreAskedIn()
        {
            var forward = new List<bool>();
            for (int x = -60; x < 60; x++)
            {
                for (int y = -60; y < 60; y++)
                {
                    forward.Add(CityLayout.IsStreetChunk(x, y));
                }
            }

            // Thrash: touch thousands of unrelated districts.
            for (int i = 0; i < 4000; i++)
            {
                CityLayout.IsStreetChunk(i * 31 - 60000, i * 17 + 9000);
            }

            int index = forward.Count - 1;
            for (int x = 59; x >= -60; x--)
            {
                for (int y = 59; y >= -60; y--)
                {
                    Assert.AreEqual(forward[index], CityLayout.IsStreetChunk(x, y), $"({x},{y}) changed when asked in another order");
                    index--;
                }
            }
        }

        /// <summary>
        /// A highway is chosen from the arterial lines, never a line of its
        /// own -- so it is always a real, connected street.
        /// </summary>
        [Test]
        public void HighwaysAreArterialsAndNeverCross()
        {
            var highwayColumns = new List<int>();
            for (int x = -1200; x <= 1200; x++)
            {
                bool highway = CityLayout.IsHighwayChunk(x, 5);
                Assert.AreEqual(highway, CityLayout.IsHighwayChunk(x, -777), $"x={x}: a highway depends on the row, so two could cross");
                if (highway)
                {
                    Assert.IsTrue(CityLayout.IsArterialChunk(x, 5), $"highway at x={x} is not an arterial");
                    Assert.IsTrue(CityLayout.IsStreetChunk(x, 5));
                    highwayColumns.Add(x);
                }
            }

            Assert.Greater(highwayColumns.Count, 4);
            for (int i = 1; i < highwayColumns.Count; i++)
            {
                int gap = highwayColumns[i] - highwayColumns[i - 1];
                Assert.GreaterOrEqual(gap, 4 * CityLayout.MinDistrictChunks, "two highways are too close together");
                Assert.LessOrEqual(gap, 4 * CityLayout.MaxDistrictChunks, "two highways are too far apart");
            }
        }

        [Test]
        public void StreetsRepeatIntoNegativeSpaceAndPositiveModReturnsTrueModulo()
        {
            // Both signs have arterials and local streets in the same density.
            int negative = 0, positive = 0;
            for (int c = 1; c <= 2000; c++)
            {
                if (CityLayout.IsStreetChunk(c, 7)) positive++;
                if (CityLayout.IsStreetChunk(-c, 7)) negative++;
            }
            Assert.Greater(negative, 100);
            Assert.Greater(positive, 100);
            Assert.AreEqual(positive, negative, positive * 0.25, "negative space has a very different street density than positive space");

            // These DO discriminate: C#'s % returns -1, -2, -3 here.
            Assert.AreEqual(0, CityLayout.PositiveMod(-4, 4));
            Assert.AreEqual(3, CityLayout.PositiveMod(-1, 4));
            Assert.AreEqual(2, CityLayout.PositiveMod(-2, 4));
            Assert.AreEqual(1, CityLayout.PositiveMod(-7, 4));
            Assert.AreEqual(1, CityLayout.PositiveMod(1, 4));
            Assert.AreEqual(0, CityLayout.PositiveMod(0, 4));
        }

        /// <summary>
        /// What decides a district's street spacing is what the district IS: a
        /// downtown is laid out on a tighter grid than the outskirts.
        /// </summary>
        [Test]
        public void DistrictStreetPeriodsFollowTheirZone()
        {
            var sum = new Dictionary<ZoneKind, double>();
            var count = new Dictionary<ZoneKind, int>();
            for (int cx = -2400; cx < 2400; cx += 20)
            {
                for (int cy = -2400; cy < 2400; cy += 20)
                {
                    CityLayout.GetDistrictExtent(cx, cy, out int loX, out int hiX, out int loY, out int hiY);
                    ZoneKind zone = CityZones.ZoneAt((loX + hiX) * 0.5, (loY + hiY) * 0.5);
                    sum.TryGetValue(zone, out double s);
                    count.TryGetValue(zone, out int n);
                    sum[zone] = s + CityLayout.DistrictPeriod(cx, cy);
                    count[zone] = n + 1;
                }
            }

            double Mean(ZoneKind z) => count.GetValueOrDefault(z) == 0 ? double.NaN : sum[z] / count[z];
            foreach (ZoneKind z in System.Enum.GetValues(typeof(ZoneKind)))
            {
                TestContext.WriteLine($"{z}: mean street period {Mean(z):0.0} over {count.GetValueOrDefault(z)} samples");
            }

            Assert.Less(Mean(ZoneKind.OldTown), 4.0, "an old town should be on a tight grid");
            Assert.Less(Mean(ZoneKind.Core), Mean(ZoneKind.Residential), "downtown should be on a tighter grid than the residential districts");
            Assert.Less(Mean(ZoneKind.Residential), Mean(ZoneKind.Outskirts), "the outskirts should be on the widest grid");
        }

        /// <summary>
        /// EXPLICIT BUG REPORT (SECOND ONE): "this highway is blocking this
        /// road make this impossible." A highway that touches down at an exit
        /// lands on a live cross-street, because every exit is chosen ON a Y
        /// arterial line. So the clearance floor holds EVERYWHERE, including
        /// at the exit's own centre -- there is no longer a special case that
        /// can reach a true 0.
        /// </summary>
        [Test]
        public void HighwayClearanceNeverDropsBelowTheFloorAnywhereIncludingRightAtAnExit()
        {
            const int laneKey = 0;
            const float minimumFraction = 5.0f / 14.0f;   // matches CityMeshBuilder's own real ratio

            int exitChunk = -1;
            for (int candidate = 0; candidate < 4000; candidate++)
            {
                if (CityLayout.HighwayHasExitAt(candidate, laneKey))
                {
                    exitChunk = candidate;
                    break;
                }
            }
            Assert.AreNotEqual(-1, exitChunk, "no highway exit found on this lane within a reasonable search -- test setup is broken");

            // EVERY REAL HIGHWAY EXIT COINCIDES WITH A REAL CROSS-STREET --
            // asserted so this test fails loudly if that ever stops being true
            // and the floor-everywhere reasoning no longer applies.
            Assert.IsTrue(
                CityLayout.IsStreetChunk(laneKey - 1, exitChunk) && CityLayout.IsStreetChunk(laneKey + 1, exitChunk),
                "expected every highway exit to sit on a real cross-arterial by construction");

            double exitCentre = CityLayout.SpanCentre(exitChunk, 1);

            float atExit = CityLayout.HighwayClearanceTaperAt(laneKey, exitCentre, minimumFraction);
            Assert.GreaterOrEqual(atExit, minimumFraction - 0.001f,
                "the exit's own centre -- which is always a real cross-street -- must stay at or above the floor");

            float oneChunkAway = CityLayout.HighwayClearanceTaperAt(
                laneKey, exitCentre + CityLayout.ChunkSize, minimumFraction);
            Assert.GreaterOrEqual(oneChunkAway, minimumFraction - 0.001f);

            float farAway = CityLayout.HighwayClearanceTaperAt(
                laneKey, exitCentre + CityLayout.HighwayExitTaperDistanceMetres * 2.0, minimumFraction);
            Assert.AreEqual(1.0f, farAway, 0.001f, "far from any exit should be full clearance");

            // Monotonic across the whole approach -- no dip back down partway.
            float previous = minimumFraction;
            for (double distance = 0.0; distance <= CityLayout.HighwayExitTaperDistanceMetres * 1.5;
                 distance += CityLayout.ChunkSize * 0.25)
            {
                float value = CityLayout.HighwayClearanceTaperAt(laneKey, exitCentre + distance, minimumFraction);
                Assert.GreaterOrEqual(value, minimumFraction - 0.0001f,
                    $"clearance dropped below the floor at {distance}m from the exit");
                Assert.GreaterOrEqual(value, previous - 0.0001f,
                    $"clearance taper dipped back down at {distance}m from the exit");
                previous = value;
            }
        }

        /// <summary>Every exit sits on a Y arterial line, whichever highway it is on.</summary>
        [Test]
        public void EveryHighwayExitSitsOnACrossArterial()
        {
            int exits = 0;
            for (int laneKey = -200; laneKey <= 200; laneKey++)
            {
                if (!CityLayout.IsHighwayChunk(laneKey, 0))
                {
                    continue;
                }
                for (int along = -1500; along <= 1500; along++)
                {
                    if (!CityLayout.HighwayHasExitAt(along, laneKey))
                    {
                        continue;
                    }
                    exits++;
                    Assert.IsTrue(CityLayout.NearestArterialLine(along, xAxis: false) == along,
                        $"exit at along={along} on highway {laneKey} is not on an east-west arterial");
                }
            }
            Assert.Greater(exits, 5, "no exits found on the sampled highways");
        }
    }
}
