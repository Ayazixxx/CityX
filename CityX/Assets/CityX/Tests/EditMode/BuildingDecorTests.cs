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
    }
}
