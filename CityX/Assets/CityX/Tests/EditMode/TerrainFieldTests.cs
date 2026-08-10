using System.Collections.Generic;
using CityX.World;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// Terrain is the one thing every other piece of world geometry is written
    /// against, so the properties it promises are worth pinning down before
    /// anything stands on it.
    ///
    /// Two of these tests are load-bearing in a way the others are not.
    /// <see cref="ContinuousFieldNeverExceedsItsDerivedSlopeBound"/> checks a
    /// hand-derived piece of maths that a future octave could silently
    /// invalidate, and
    /// <see cref="NeighbouringPadsNeverDifferByMoreThanTheDerivedStep"/> is the
    /// one that decides whether the world is walkable or has unannounced cliffs
    /// in it.
    /// </summary>
    public class TerrainFieldTests
    {
        private const int Seed = 20260810;

        private static TerrainField Field()
        {
            return new TerrainField(Seed);
        }

        [Test]
        public void TerraceStepIsAWholeNumberOfFloors()
        {
            double floors = TerrainField.TerraceStep / CellLayout.NominalFloorMetres;
            Assert.AreEqual(System.Math.Round(floors), floors, 1e-6,
                "a terrace step that is not a whole number of floors puts every retaining " +
                "wall halfway through a floor slab");
        }

        [Test]
        public void EveryHeightIsAWholeTerraceStep()
        {
            TerrainField field = Field();
            for (int x = -600; x <= 600; x += 7)
            {
                for (int z = -600; z <= 600; z += 7)
                {
                    float h = field.HeightAt(x, z);
                    double steps = h / TerrainField.TerraceStep;
                    Assert.AreEqual(System.Math.Round(steps), steps, 1e-4,
                        $"({x},{z}) landed at {h}m, which is not on a terrace");
                }
            }
        }

        [Test]
        public void HeightStaysInsideItsProvenRange()
        {
            TerrainField field = Field();
            double lowest = double.MaxValue;
            double highest = double.MinValue;

            for (int x = -4000; x <= 4000; x += 13)
            {
                for (int z = -4000; z <= 4000; z += 13)
                {
                    double h = field.ContinuousHeightAt(x, z);
                    if (h < lowest) lowest = h;
                    if (h > highest) highest = h;
                }
            }

            Assert.GreaterOrEqual(lowest, TerrainField.SeaLevel,
                "ground below sea level -- the weights no longer sum to 1");
            Assert.Less(highest, TerrainField.SeaLevel + TerrainField.TotalRelief,
                "ground above the stated relief -- the weights no longer sum to 1");
            TestContext.WriteLine($"continuous relief observed: {lowest:F2}m .. {highest:F2}m " +
                                  $"of a possible 0..{TerrainField.TotalRelief}m");
        }

        /// <summary>
        /// The same coordinate must give the same answer no matter what else
        /// has been asked first.
        ///
        /// The second half is the half with teeth. A first pass could easily
        /// cache lattice rows or reuse a scratch generator between calls, and
        /// the resulting order-dependence would not show up in a straight
        /// repeat -- only when two processes happened to stream chunks in a
        /// different order, which is EXACTLY what a server and a client do.
        /// </summary>
        [Test]
        public void HeightIsIndependentOfCallOrder()
        {
            TerrainField field = Field();

            var straight = new List<float>();
            for (int i = 0; i < 200; i++)
            {
                straight.Add(field.HeightAt(i * 17, i * -23));
            }

            // Same queries, scrambled order, interleaved with unrelated ones.
            var scrambled = new float[200];
            for (int i = 199; i >= 0; i--)
            {
                field.HeightAt(i * 3.7, i * 91.3);
                scrambled[i] = field.HeightAt(i * 17, i * -23);
            }

            for (int i = 0; i < 200; i++)
            {
                Assert.AreEqual(straight[i], scrambled[i],
                    $"sample {i} changed depending on what was asked before it");
            }
        }

        [Test]
        public void DifferentSeedsGiveDifferentWorlds()
        {
            TerrainField a = new TerrainField(1);
            TerrainField b = new TerrainField(2);

            int same = 0;
            const int samples = 400;
            for (int i = 0; i < samples; i++)
            {
                if (a.HeightAt(i * 31, i * 47) == b.HeightAt(i * 31, i * 47))
                {
                    same++;
                }
            }

            // Some agreement is expected -- there are only 12 terrace levels, so
            // two unrelated worlds coincide often by chance. Near-total
            // agreement would mean the seed is not reaching the hash.
            Assert.Less(same, samples * 0.5,
                $"{same}/{samples} samples identical across two seeds -- the world seed is being ignored");
        }

        /// <summary>
        /// Checks the slope derivation in TerrainField's own comment.
        ///
        /// HONEST ABOUT WHAT THIS PROVES: sampling can only ever find slopes at
        /// least as steep as the ones it lands on, so this cannot prove the
        /// bound holds everywhere. What it does do is catch the derivation being
        /// WRONG -- which is the realistic failure, and which would show up as a
        /// measured value above the bound almost immediately. It is sampled at
        /// 1m over a 144m shortest wavelength, so the steep region of the
        /// smoothstep is resolved roughly 100x over.
        /// </summary>
        [Test]
        public void ContinuousFieldNeverExceedsItsDerivedSlopeBound()
        {
            TerrainField field = Field();
            double steepest = 0.0;
            int steepestX = 0, steepestZ = 0;

            for (int x = -900; x <= 900; x++)
            {
                for (int z = -900; z <= 900; z += 3)
                {
                    double here = field.ContinuousHeightAt(x, z);
                    double alongX = System.Math.Abs(field.ContinuousHeightAt(x + 1, z) - here);
                    double alongZ = System.Math.Abs(field.ContinuousHeightAt(x, z + 1) - here);
                    double worst = alongX > alongZ ? alongX : alongZ;
                    if (worst > steepest)
                    {
                        steepest = worst;
                        steepestX = x;
                        steepestZ = z;
                    }
                }
            }

            TestContext.WriteLine($"steepest measured: {steepest:F4} m/m at ({steepestX},{steepestZ}); " +
                                  $"derived bound {TerrainField.MaxSlope:F4} m/m");
            Assert.LessOrEqual(steepest, TerrainField.MaxSlope,
                "the ground is steeper than TerrainField.MaxSlope claims is possible -- " +
                "the derivation in that comment is stale, most likely because an octave was added");
        }

        /// <summary>
        /// The test that decides whether the world is traversable.
        ///
        /// Unlike the slope test above, this one is EXHAUSTIVE over the region
        /// it sweeps -- chunks are discrete, so every neighbour pair in the
        /// swept area is actually checked, not sampled.
        /// </summary>
        [Test]
        public void NeighbouringPadsNeverDifferByMoreThanTheDerivedStep()
        {
            TerrainField field = Field();
            var stepHistogram = new Dictionary<int, int>();
            int worst = 0;

            for (int cx = -160; cx < 160; cx++)
            {
                for (int cy = -160; cy < 160; cy++)
                {
                    int here = LevelOf(field, cx, cy);
                    int east = LevelOf(field, cx + 1, cy);
                    int north = LevelOf(field, cx, cy + 1);

                    foreach (int diff in new[] { System.Math.Abs(east - here), System.Math.Abs(north - here) })
                    {
                        stepHistogram.TryGetValue(diff, out int count);
                        stepHistogram[diff] = count + 1;
                        if (diff > worst)
                        {
                            worst = diff;
                        }
                    }
                }
            }

            int total = 0;
            foreach (var pair in stepHistogram)
            {
                total += pair.Value;
            }
            var lines = new List<string>();
            for (int step = 0; step <= worst; step++)
            {
                stepHistogram.TryGetValue(step, out int count);
                lines.Add($"{step} step(s): {count} ({100.0 * count / total:F2}%)");
            }
            TestContext.WriteLine("retaining walls between neighbouring pads --\n  " + string.Join("\n  ", lines));

            Assert.LessOrEqual(worst, TerrainField.MaxNeighbourTerraceSteps,
                $"found a {worst * TerrainField.TerraceStep}m wall between two neighbouring chunks, " +
                $"above the {TerrainField.MaxNeighbourTerraceSteps * TerrainField.TerraceStep}m the slope bound allows");
        }

        /// <summary>
        /// Guards the boring catastrophe: a hash that returns something constant
        /// makes every other test here pass while the world is a car park.
        /// </summary>
        [Test]
        public void TerrainUsesMostOfItsTerraceRange()
        {
            TerrainField field = Field();
            var levels = new HashSet<int>();

            for (int cx = -300; cx < 300; cx++)
            {
                for (int cy = -300; cy < 300; cy++)
                {
                    levels.Add(LevelOf(field, cx, cy));
                }
            }

            var sorted = new List<int>(levels);
            sorted.Sort();
            TestContext.WriteLine($"{levels.Count} of a possible {TerrainField.MaxTerraceLevel + 1} terrace " +
                                  $"levels used: [{string.Join(", ", sorted)}]");

            // Measured: 11 of 12. The top level needs the noise above 0.978,
            // which is rare enough that demanding all 12 would be demanding
            // luck. 9 leaves room for seed variation while still failing loudly
            // if the relief collapses.
            Assert.GreaterOrEqual(levels.Count, 9,
                "the world barely varies in height -- terracing is swallowing the relief, " +
                "or the noise is not varying");
        }

        /// <summary>
        /// The floating-point form of the sign trap CityLayout.PositiveMod
        /// exists for. Truncating toward zero instead of flooring would make
        /// lattice cell -0.5 and +0.5 the same cell, mirroring the entire world
        /// about the origin -- which looks plausible from spawn and is obviously
        /// broken from anywhere else.
        /// </summary>
        [Test]
        public void NegativeSpaceIsNotAMirrorOfPositiveSpace()
        {
            TerrainField field = Field();
            int mirrored = 0;
            const int samples = 500;

            for (int i = 1; i <= samples; i++)
            {
                double x = i * 1.7;
                double z = i * 2.9;
                if (field.HeightAt(-x, -z) == field.HeightAt(x, z))
                {
                    mirrored++;
                }
            }

            // With 12 terrace levels, chance agreement is common; a mirror is
            // total.
            Assert.Less(mirrored, samples * 0.6,
                $"{mirrored}/{samples} reflected coordinates have identical ground -- " +
                "the lattice is truncating toward zero instead of flooring");
        }

        [Test]
        public void PadHeightIsTheGroundAtTheChunkCentre()
        {
            TerrainField field = Field();
            for (int cx = -20; cx <= 20; cx += 3)
            {
                for (int cy = -20; cy <= 20; cy += 3)
                {
                    Assert.AreEqual(
                        field.HeightAt(CityLayout.ChunkCentreX(cx), CityLayout.ChunkCentreZ(cy)),
                        field.PadHeightFor(cx, cy),
                        $"chunk ({cx},{cy}) pad disagrees with the ground at its own centre");
                }
            }
        }

        private static int LevelOf(TerrainField field, int cx, int cy)
        {
            return (int)System.Math.Round(field.PadHeightFor(cx, cy) / TerrainField.TerraceStep);
        }
    }
}
