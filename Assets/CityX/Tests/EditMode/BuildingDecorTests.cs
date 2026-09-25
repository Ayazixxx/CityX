using System.Collections.Generic;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Where balconies land.
    ///
    /// Every one of these is a property city_chunk.gd's own _add_balconies
    /// arrived at rather than started with: never on the ground row, never on
    /// the wall the entrance is in, never hanging off a corner, and rare
    /// enough that a facade reads as having a few rather than being a grid of
    /// them.
    /// </summary>
    public class BuildingDecorTests
    {
        private const float Width = 20.0f;
        private const float Depth = 16.0f;
        private const float Height = 40.0f;

        [Test]
        public void BalconiesHangOffARealWallAndStayOnIt()
        {
            int sampled = 0;

            for (int seed = 0; seed < 400; seed++)
            {
                foreach (BuildingDecor.Balcony balcony in
                         BuildingDecor.Balconies(seed, Width, Depth, Height))
                {
                    sampled++;

                    // Axis-aligned and outward, which is what lets every box be
                    // built without a rotation.
                    Assert.AreEqual(1.0f, balcony.Normal.magnitude, 0.001f);
                    Assert.AreEqual(0.0f, balcony.Normal.y, 0.001f, "a balcony on the roof");

                    // NEVER THE DOOR WALL. That is the +Z face, and a balcony
                    // over the entrance hides the one part of the facade that
                    // has to stay readable.
                    Assert.Less(balcony.Normal.z, 0.5f, "a balcony hangs over the entrance");

                    // Off the wall by half its own depth, so the slab starts at
                    // the facade rather than inside it.
                    float standoff = BuildingDecor.BalconyDepth * 0.5f;
                    if (balcony.WidthAlongX)
                    {
                        Assert.AreEqual(-Depth * 0.5f - standoff, balcony.Centre.z, 0.001f);
                        // On the wall, not round the corner.
                        float overhang = Mathf.Abs(balcony.Centre.x)
                                         + BuildingDecor.BalconyWidth * 0.5f;
                        Assert.LessOrEqual(overhang, Width * 0.5f,
                            "a balcony runs off the end of its own wall");
                    }
                    else
                    {
                        Assert.AreEqual((Width * 0.5f + standoff) * balcony.Normal.x,
                            balcony.Centre.x, 0.001f);
                        float overhang = Mathf.Abs(balcony.Centre.z)
                                         + BuildingDecor.BalconyWidth * 0.5f;
                        Assert.LessOrEqual(overhang, Depth * 0.5f,
                            "a balcony runs off the end of its own wall");
                    }

                    // Never at pavement level -- row 0 is skipped, so the
                    // lowest possible is one window row up.
                    Assert.Greater(balcony.Centre.y, CellLayout.NominalFloorMetres * 0.5f,
                        "a balcony at street level reads as a loading dock");
                }
            }

            TestContext.WriteLine($"{sampled} balconies over 400 buildings");
            Assert.Greater(sampled, 100, "not enough balconies sampled to mean anything");
        }

        /// <summary>
        /// Rare per row, and capped. Without the cap a 300m tower grows a
        /// column of balconies nobody can see from the street and everybody
        /// pays for.
        /// </summary>
        [Test]
        public void BalconiesAreOccasionalRatherThanEveryFloor()
        {
            int total = 0;
            int mostOnOne = 0;
            for (int seed = 0; seed < 400; seed++)
            {
                int count = BuildingDecor.Balconies(seed, Width, Depth, Height).Count;
                total += count;
                mostOnOne = Mathf.Max(mostOnOne, count);
            }

            float mean = total / 400.0f;
            TestContext.WriteLine($"mean {mean:0.00} per building, most {mostOnOne}");

            // Six eligible rows at 30% each.
            Assert.AreEqual(BuildingDecor.BalconyMaxRows * BuildingDecor.BalconyChancePerRow,
                mean, 0.25, "the per-row chance drifted");
            Assert.LessOrEqual(mostOnOne, BuildingDecor.BalconyMaxRows,
                "more balconies than there are rows allowed to carry one");

            // A very tall building gets no more than a short one, because the
            // cap is rows and not height.
            Assert.AreEqual(
                BuildingDecor.Balconies(7, Width, Depth, 300.0f).Count,
                BuildingDecor.Balconies(7, Width, Depth, 300.0f).Count);
        }

        /// <summary>
        /// Same seed, same balconies -- and a different seed puts them
        /// somewhere else. Chunks are built independently and can be rebuilt at
        /// any time, so a balcony that moved on reload would be a building that
        /// changed while you were looking away.
        /// </summary>
        [Test]
        public void TheSameSeedAlwaysGivesTheSameBalconies()
        {
            List<BuildingDecor.Balcony> first = BuildingDecor.Balconies(99, Width, Depth, Height);
            List<BuildingDecor.Balcony> again = BuildingDecor.Balconies(99, Width, Depth, Height);

            Assert.AreEqual(first.Count, again.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].Centre, again[i].Centre);
                Assert.AreEqual(first[i].Normal, again[i].Normal);
            }

            int differences = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                if (BuildingDecor.Balconies(seed, Width, Depth, Height).Count !=
                    BuildingDecor.Balconies(seed + 1000, Width, Depth, Height).Count)
                {
                    differences++;
                }
            }
            Assert.Greater(differences, 40, "different seeds produced suspiciously similar facades");
        }

        /// <summary>
        /// A building too narrow to carry one gets none rather than a balcony
        /// wider than the wall it hangs off.
        /// </summary>
        [Test]
        public void ABuildingTooNarrowGetsNone()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                Assert.IsEmpty(BuildingDecor.Balconies(seed, 3.0f, 3.0f, Height),
                    "a 3m building grew a 2.2m balcony with clearance it does not have");
            }

            // And a building with no room above the ground floor gets none,
            // because row 0 is the only row there is.
            Assert.IsEmpty(BuildingDecor.Balconies(0, Width, Depth, 3.0f));
        }

        // --- ribs ---

        private const float DoorU0 = -0.8f;
        private const float DoorU1 = 0.8f;

        private static BuildingDecor.RibbedFacade RibsFor(int seed, float height = Height)
        {
            return BuildingDecor.Ribs(seed, Width, Depth, height, DoorU0, DoorU1);
        }

        /// <summary>
        /// A fin stands on its own wall, in its own slot, clear of its
        /// neighbours. Overlapping fins would be invisible at a cost, and a fin
        /// running off the end of a wall would hang in mid air at the corner.
        /// </summary>
        [Test]
        public void EachFinSitsInsideItsOwnCellOnItsOwnWall()
        {
            int sampled = 0;

            for (int seed = 0; seed < 300; seed++)
            {
                BuildingDecor.RibbedFacade ribbed = RibsFor(seed);

                var spansByWall = new Dictionary<Vector3, List<BuildingDecor.Rib>>();
                foreach (BuildingDecor.Rib rib in ribbed.Fins)
                {
                    sampled++;

                    Assert.AreEqual(1.0f, rib.Normal.magnitude, 0.001f);
                    Assert.AreEqual(0.0f, rib.Normal.y, 0.001f, "a fin lying on the roof");
                    Assert.Less(rib.U0, rib.U1, "a fin with no width");

                    // On the wall it claims, not round the corner.
                    float span = Mathf.Abs(rib.Normal.x) > 0.5f ? Depth : Width;
                    Assert.GreaterOrEqual(rib.U0, -span * 0.5f - 0.001f);
                    Assert.LessOrEqual(rib.U1, span * 0.5f + 0.001f);

                    if (!spansByWall.TryGetValue(rib.Normal, out List<BuildingDecor.Rib> onWall))
                    {
                        onWall = new List<BuildingDecor.Rib>();
                        spansByWall[rib.Normal] = onWall;
                    }
                    foreach (BuildingDecor.Rib other in onWall)
                    {
                        Assert.IsTrue(rib.U1 <= other.U0 + 0.001f || rib.U0 >= other.U1 - 0.001f,
                            $"two fins overlap on the same wall: " +
                            $"[{rib.U0},{rib.U1}] and [{other.U0},{other.U1}]");
                    }
                    onWall.Add(rib);
                }

                if (ribbed.Fins.Count > 0)
                {
                    Assert.GreaterOrEqual(ribbed.Depth, BuildingDecor.RibMinDepth);
                    Assert.LessOrEqual(ribbed.Depth, BuildingDecor.RibMaxDepth);
                }
            }

            TestContext.WriteLine($"{sampled} fins over 300 buildings");
            Assert.Greater(sampled, 1000, "not enough fins sampled to mean anything");
        }

        /// <summary>
        /// NOTHING STANDS IN THE DOORWAY. Every other rule here is about how the
        /// facade looks; this one is about whether the building can be walked
        /// into, which is the only way a decorative fin can break something
        /// real.
        /// </summary>
        [Test]
        public void NoFinStandsInFrontOfTheDoor()
        {
            int doorWallFins = 0;

            for (int seed = 0; seed < 400; seed++)
            {
                foreach (BuildingDecor.Rib rib in RibsFor(seed).Fins)
                {
                    if (rib.Normal.z < 0.5f)
                    {
                        continue;   // not the door wall
                    }
                    doorWallFins++;

                    bool clear = rib.U1 <= DoorU0 - BuildingDecor.RibDoorClearance ||
                                 rib.U0 >= DoorU1 + BuildingDecor.RibDoorClearance;
                    Assert.IsTrue(clear,
                        $"a fin spans [{rib.U0},{rib.U1}], across the doorway at [{DoorU0},{DoorU1}]");
                }
            }

            Assert.Greater(doorWallFins, 100, "the door wall was never ribbed, so this proved nothing");
        }

        /// <summary>
        /// Ribbed at roughly the stated rate, and when a building is ribbed it
        /// is either all four walls or some of them -- the mix is what stops
        /// the city reading as one repeated building.
        /// </summary>
        [Test]
        public void SomeBuildingsAreRibbedAndSomeWallsAreNot()
        {
            int ribbed = 0;
            var wallCounts = new Dictionary<int, int>();

            for (int seed = 0; seed < 800; seed++)
            {
                BuildingDecor.RibbedFacade facade = RibsFor(seed);
                if (facade.Fins.Count == 0)
                {
                    continue;
                }
                ribbed++;

                var walls = new HashSet<Vector3>();
                foreach (BuildingDecor.Rib rib in facade.Fins)
                {
                    walls.Add(rib.Normal);
                }
                wallCounts.TryGetValue(walls.Count, out int seen);
                wallCounts[walls.Count] = seen + 1;
            }

            float share = ribbed / 800.0f;
            TestContext.WriteLine($"{share:0.00} ribbed; walls per ribbed building: " +
                                  string.Join(", ", wallCounts));

            Assert.AreEqual(BuildingDecor.RibChance, share, 0.06,
                "the ribbed share drifted");
            wallCounts.TryGetValue(4, out int allFour);
            Assert.Greater(allFour, 0, "no building ribs every wall");
            Assert.Greater(wallCounts.Count, 1,
                "every ribbed building ribs the same number of walls");
        }

        /// <summary>
        /// Fins start one window row up and run to the roof. The bottom row is
        /// the one holding the entrance and the street-level glass, and a fin
        /// bracketing either is the one place this decoration would be in the
        /// way rather than on the wall.
        /// </summary>
        [Test]
        public void FinsClearTheGroundRowAndAShortBuildingGetsNone()
        {
            int checked_ = 0;
            for (int seed = 0; seed < 300; seed++)
            {
                BuildingDecor.RibbedFacade facade = RibsFor(seed);
                if (facade.Fins.Count == 0)
                {
                    continue;
                }
                checked_++;

                CellLayout rows = CellLayout.For(Height, CellLayout.NominalFloorMetres);
                Assert.AreEqual(rows.Size * BuildingDecor.RibBaseRows, facade.BaseY, 0.001f);
                Assert.Greater(facade.BaseY, 0.0f);
            }
            Assert.Greater(checked_, 50);

            // One row of building is all door and all glass, and there is
            // nothing above it for a fin to run up.
            for (int seed = 0; seed < 100; seed++)
            {
                Assert.IsEmpty(BuildingDecor.Ribs(seed, Width, Depth, 3.2f, DoorU0, DoorU1).Fins,
                    "a one-storey building grew fins over its own doorway");
            }
        }

        /// <summary>
        /// Same seed, same fins. Chunks rebuild whenever streaming decides to,
        /// and a facade that re-rolled on reload is a building that changed
        /// while you were looking away.
        /// </summary>
        [Test]
        public void TheSameSeedAlwaysGivesTheSameFins()
        {
            BuildingDecor.RibbedFacade first = RibsFor(31);
            BuildingDecor.RibbedFacade again = RibsFor(31);

            Assert.AreEqual(first.BaseY, again.BaseY);
            Assert.AreEqual(first.Depth, again.Depth);
            Assert.AreEqual(first.Fins.Count, again.Fins.Count);
            for (int i = 0; i < first.Fins.Count; i++)
            {
                Assert.AreEqual(first.Fins[i].Normal, again.Fins[i].Normal);
                Assert.AreEqual(first.Fins[i].U0, again.Fins[i].U0);
            }

            // And ribs are rolled on their own stream, so adding or removing
            // them cannot move a balcony -- the same independence every other
            // seeded decision on a building relies on.
            int differences = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                if (RibsFor(seed).Fins.Count != RibsFor(seed + 1000).Fins.Count)
                {
                    differences++;
                }
            }
            Assert.Greater(differences, 40, "different seeds produced suspiciously similar facades");
        }

        // --- wings ---

        private const float Roomy = 8.0f;

        /// <summary>
        /// Every wing a building grows sits on a real wall, stays within it,
        /// and never on the entrance side -- checked over every wing in the
        /// list now that a roomy building can grow more than one (see
        /// BuildingDecor.WingsFor's own doc comment).
        /// </summary>
        [Test]
        public void AWingSitsOnOneWallAndStaysWithinIt()
        {
            int grown = 0;
            int buildingsWithAWing = 0;

            for (int seed = 0; seed < 500; seed++)
            {
                List<BuildingDecor.Wing> wings = BuildingDecor.WingsFor(
                    seed, Width, Depth, Height, Roomy, Roomy, Roomy);
                if (wings.Count == 0)
                {
                    continue;
                }
                buildingsWithAWing++;

                foreach (BuildingDecor.Wing wing in wings)
                {
                    grown++;

                    Assert.AreEqual(1.0f, wing.Normal.magnitude, 0.001f);
                    Assert.AreEqual(0.0f, wing.Normal.y, 0.001f);
                    Assert.Less(wing.Normal.z, 0.5f, "a wing grew across the entrance");

                    float span = Mathf.Abs(wing.Normal.x) > 0.5f ? Depth : Width;
                    Assert.Greater(wing.Width, 0.0f);
                    Assert.LessOrEqual(wing.Width, span * BuildingDecor.WingMaxWidthFraction + 0.001f);

                    // Inside the wall it grows from, with the stated margin left at
                    // the corner -- unless the wing is so wide there is none to
                    // leave, in which case it centres rather than overhanging.
                    float reach = Mathf.Abs(wing.Along) + wing.Width * 0.5f;
                    Assert.LessOrEqual(reach, span * 0.5f + 0.001f,
                        "a wing runs off the end of the wall it grew from");

                    Assert.GreaterOrEqual(wing.Depth, BuildingDecor.WingLeastUsefulDepth);
                    Assert.LessOrEqual(wing.Depth, BuildingDecor.WingMaxDepth + 0.001f);

                    Assert.GreaterOrEqual(wing.Height, Height * BuildingDecor.WingMinHeightFraction - 0.001f);
                    Assert.LessOrEqual(wing.Height, Height * BuildingDecor.WingMaxHeightFraction + 0.001f);
                }
            }

            double buildingShare = buildingsWithAWing / 500.0;
            // Each of the 3 walls is an independent WingChance roll, so at
            // least one surviving is 1-(1-WingChance)^3, not WingChance itself.
            double expectedBuildingShare = 1.0 - System.Math.Pow(1.0 - BuildingDecor.WingChance, 3);
            TestContext.WriteLine($"{buildingShare:0.00} of roomy buildings grew at least one wing, " +
                $"{grown} wings total");
            Assert.AreEqual(expectedBuildingShare, buildingShare, 0.06, "the wing share drifted");
            Assert.Greater(grown, buildingsWithAWing,
                "no roomy building grew more than one wing -- the multi-wall roll is not actually independent");
        }

        /// <summary>
        /// THE DEPARTURE FROM GODOT, TESTED AS SUCH. city_chunk.gd protrudes
        /// three to six metres with no check, which is safe there because a
        /// building sits alone in the middle of its chunk. Here a building
        /// fills its lot and its neighbour is under a metre away, so the depth
        /// is clamped to the room the wall actually has -- and a wall with no
        /// room grows nothing at all rather than a stub.
        /// </summary>
        [Test]
        public void AWingNeverProtrudesFurtherThanThereIsRoom()
        {
            int refused = 0;
            int clamped = 0;

            for (int seed = 0; seed < 500; seed++)
            {
                // A landlocked building: a party wall's worth of room on every
                // side, which is not enough for a wing on any of them.
                Assert.IsEmpty(
                    BuildingDecor.WingsFor(seed, Width, Depth, Height, 0.8f, 0.8f, 0.8f),
                    "a wing grew into the building next door");
                refused++;

                // One street-facing wall and two party walls. Whatever grows
                // must be on the wall with the room, and must fit in it.
                List<BuildingDecor.Wing> wings =
                    BuildingDecor.WingsFor(seed, Width, Depth, Height, 0.8f, 4.0f, 0.8f);
                if (wings.Count == 0)
                {
                    continue;
                }
                clamped++;
                foreach (BuildingDecor.Wing wing in wings)
                {
                    Assert.AreEqual(Vector3.right, wing.Normal,
                        "a wing grew off a wall with no room beside walls that had none");
                    Assert.LessOrEqual(wing.Depth, 4.0f + 0.001f);
                }
            }

            TestContext.WriteLine($"{refused} landlocked refused, {clamped} clamped to one wall");
            Assert.Greater(clamped, 20, "no wing ever landed on the one wall with room");
        }

        /// <summary>
        /// Same seed, same wing -- and picking the wall does not depend on how
        /// much room the walls have, only whether the pick survives. A building
        /// whose wing moved when a neighbour was demolished would be a building
        /// rebuilding itself for reasons nothing can see.
        /// </summary>
        [Test]
        public void TheSameSeedAlwaysGrowsTheSameWing()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                List<BuildingDecor.Wing> first =
                    BuildingDecor.WingsFor(seed, Width, Depth, Height, Roomy, Roomy, Roomy);
                List<BuildingDecor.Wing> again =
                    BuildingDecor.WingsFor(seed, Width, Depth, Height, Roomy, Roomy, Roomy);

                Assert.AreEqual(first.Count, again.Count);
                for (int i = 0; i < first.Count; i++)
                {
                    Assert.AreEqual(first[i].Normal, again[i].Normal);
                    Assert.AreEqual(first[i].Along, again[i].Along);
                    Assert.AreEqual(first[i].Width, again[i].Width);
                }

                // Tighter clearances can only shrink or refuse each wall's own
                // wing, never move it to another wall -- matched up by which
                // wall each one is on, since a tighter roll can drop some
                // walls' wings without dropping others'.
                List<BuildingDecor.Wing> tight =
                    BuildingDecor.WingsFor(seed, Width, Depth, Height, 2.0f, 2.0f, 2.0f);
                foreach (BuildingDecor.Wing tightWing in tight)
                {
                    BuildingDecor.Wing roomyWing = first.Find(w => w.Normal == tightWing.Normal);
                    Assert.IsTrue(roomyWing.Exists,
                        "a tighter clearance grew a wing on a wall the roomy version did not");
                    Assert.AreEqual(roomyWing.Along, tightWing.Along);
                    Assert.LessOrEqual(tightWing.Depth, roomyWing.Depth + 0.001f);
                }
            }
        }

        // --- entrance variety ---

        [Test]
        public void ASmallMismatchIsFlushWithNoStairs()
        {
            BuildingDecor.EntranceStyle flat = BuildingDecor.EntranceStyleFor(0.0f);
            Assert.IsFalse(flat.NeedsStairs);
            Assert.AreEqual(0.0f, flat.Rise);

            BuildingDecor.EntranceStyle tiny = BuildingDecor.EntranceStyleFor(
                BuildingDecor.EntranceFlushMaxMismatch * 0.5f);
            Assert.IsFalse(tiny.NeedsStairs, "a mismatch well inside the flush threshold rolled stairs anyway");
        }

        [Test]
        public void ANegativeMismatchIsAlsoFlush()
        {
            // The door standing BELOW the real ground outside it is not a
            // shape this system has any style for -- clamped to zero rather
            // than producing a negative rise that would build a nonsensical
            // upside-down flight.
            BuildingDecor.EntranceStyle style = BuildingDecor.EntranceStyleFor(-5.0f);
            Assert.IsFalse(style.NeedsStairs);
            Assert.AreEqual(0.0f, style.Rise);
        }

        [Test]
        public void AMismatchPastTheFlushThresholdGetsStairsSizedToIt()
        {
            float mismatch = BuildingDecor.EntranceFlushMaxMismatch + 2.0f;
            BuildingDecor.EntranceStyle style = BuildingDecor.EntranceStyleFor(mismatch);
            Assert.IsTrue(style.NeedsStairs);
            Assert.AreEqual(mismatch, style.Rise, 0.001f);
        }

        /// <summary>
        /// EXPLICIT BUG REPORT reasoning, not a made-up number: a mismatch
        /// bigger than a single stoop could sanely bridge must not send the
        /// stairs running out over the sidewalk or into the street forever --
        /// see EntranceMaxRise's own doc comment for why this is capped the
        /// same way CityMeshBuilder.SkirtDepth caps a retaining wall's own
        /// worst case.
        /// </summary>
        [Test]
        public void AHugeMismatchIsCappedRatherThanBuildingAnEndlessStaircase()
        {
            BuildingDecor.EntranceStyle style = BuildingDecor.EntranceStyleFor(500.0f);
            Assert.IsTrue(style.NeedsStairs);
            Assert.AreEqual(BuildingDecor.EntranceMaxRise, style.Rise, 0.001f);
        }

        // --- entrance approach: sloped terrain vs. hard edge ---

        /// <summary>Same seed, same answer -- a rebuild must not flip a building's own approach style.</summary>
        [Test]
        public void EntranceApproachIsSlopedIsDeterministic()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                bool first = BuildingDecor.EntranceApproachIsSloped(seed);
                bool second = BuildingDecor.EntranceApproachIsSloped(seed);
                Assert.AreEqual(first, second, $"seed {seed} answered differently on a second call");
            }
        }

        /// <summary>EXPLICIT ASK: "lets do 60%... the other 40%" -- a real split across many buildings, not always one answer.</summary>
        [Test]
        public void EntranceApproachIsSlopedSplitsRoughlySixtyForty()
        {
            int sloped = 0;
            const int trials = 2000;
            for (int seed = 0; seed < trials; seed++)
            {
                if (BuildingDecor.EntranceApproachIsSloped(seed))
                {
                    sloped++;
                }
            }

            float fraction = sloped / (float)trials;
            Assert.AreEqual(BuildingDecor.EntranceApproachSlopedChance, fraction, 0.05,
                $"{sloped}/{trials} rolled sloped ({fraction:P1}) -- too far from the stated 60% chance");
        }

        /// <summary>
        /// Independent of every OTHER per-building roll this codebase
        /// already makes from the same building seed (balconies, ribs,
        /// wings) -- EntranceApproachStream has to be its own salt, or
        /// adding/removing one of those would silently flip a building's
        /// own entrance style too.
        /// </summary>
        [Test]
        public void EntranceApproachDoesNotCorrelateWithBalconyPlacement()
        {
            int agree = 0;
            const int trials = 500;
            for (int seed = 0; seed < trials; seed++)
            {
                bool sloped = BuildingDecor.EntranceApproachIsSloped(seed);
                bool hasBalcony = BuildingDecor.Balconies(seed, Width, Depth, Height).Count > 0;
                if (sloped == hasBalcony)
                {
                    agree++;
                }
            }

            // Two independent ~coin-flip-ish booleans agree close to half
            // the time purely by chance; a shared stream/seed bug would
            // instead push this toward 0% or 100%.
            float agreement = agree / (float)trials;
            Assert.Greater(agreement, 0.3f, "entrance approach and balcony placement look correlated");
            Assert.Less(agreement, 0.7f, "entrance approach and balcony placement look correlated");
        }
    }
}
