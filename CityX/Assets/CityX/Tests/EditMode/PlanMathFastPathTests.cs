using System;
using CityX.World.Plan;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The plan's hottest geometry is written out in plain numbers, because the game's Mono runs Vec2's operators several times slower than the
    /// same sums (2026-11-26: the game's city took 11.6 s to plan in a built game's Mono and 6.8 s after). Written out, it must still give exactly
    /// what the Vec2 forms give, to the last bit: every player plans the city for themselves, and one road a hair off moves every road after it.
    /// </summary>
    public sealed class PlanMathFastPathTests
    {
        /// <summary>DistanceToSegment as it was written with Vec2's operators.</summary>
        private static double DistanceTheVec2Way(Vec2 p, Vec2 a, Vec2 b, out double t)
        {
            Vec2 ab = b - a;
            double len2 = ab.SqrLength;
            t = len2 < 1e-12 ? 0.0 : PlanMath.Clamp01(Vec2.Dot(p - a, ab) / len2);
            Vec2 q = a + ab * t;
            return (p - q).Length;
        }

        private static Vec2 Somewhere(Random random, double size) =>
            new Vec2((random.NextDouble() - 0.5) * size, (random.NextDouble() - 0.5) * size);

        [Test]
        public void DistanceToASegmentIsTheVec2FormsToTheLastBit()
        {
            var random = new Random(20261126);
            for (int i = 0; i < 200000; i++)
            {
                double size = i % 3 == 0 ? 12000.0 : i % 3 == 1 ? 40.0 : 0.01;
                Vec2 a = Somewhere(random, size);
                Vec2 p = i % 13 == 0 ? a : Somewhere(random, size);
                // Some segments with no length, some all but none, some running through the point.
                Vec2 b = i % 7 == 0 ? a : i % 11 == 0 ? a + new Vec2(3e-7, -1e-7) : i % 17 == 0 ? p + (p - a) : Somewhere(random, size);
                double distance = PlanMath.DistanceToSegment(p, a, b, out double t);
                double expected = DistanceTheVec2Way(p, a, b, out double expectedT);
                if (BitConverter.DoubleToInt64Bits(distance) != BitConverter.DoubleToInt64Bits(expected) ||
                    BitConverter.DoubleToInt64Bits(t) != BitConverter.DoubleToInt64Bits(expectedT))
                {
                    Assert.Fail($"p {p.X:R},{p.Y:R} a {a.X:R},{a.Y:R} b {b.X:R},{b.Y:R}: {distance:R} at {t:R}, the Vec2 form {expected:R} at {expectedT:R}");
                }
            }
        }

        /// <summary>A winding deck of uneven widths, its normals unit length as the planner makes them.</summary>
        private static ViaductPath WindingDeck(Random random)
        {
            const int samples = 80;
            var path = new ViaductPath
            {
                P = new Vec2[samples],
                N = new Vec2[samples],
                HalfLeft = new double[samples],
                HalfRight = new double[samples],
            };
            Vec2 at = Somewhere(random, 3000.0);
            Vec2 heading = new Vec2(1.0, 0.0);
            for (int i = 0; i < samples; i++)
            {
                heading = PlanMath.RotateSteps(heading, random.Next(-2, 3));
                at += heading * (2.0 + random.NextDouble() * 12.0);
                path.P[i] = at;
                path.N[i] = heading.Perp;
                path.HalfLeft[i] = 3.0 + random.NextDouble() * 9.0;
                path.HalfRight[i] = 3.0 + random.NextDouble() * 9.0;
            }
            return path;
        }

        [Test]
        public void ADeckFootprintHoldsExactlyThePointsItsPolygonDoes()
        {
            var random = new Random(48398181);
            int inside = 0, tested = 0;
            for (int deck = 0; deck < 12; deck++)
            {
                ViaductPath path = WindingDeck(random);
                for (int seg = 0; seg < path.Segments; seg++)
                {
                    foreach (double margin in new[] { 0.0, 0.5, 1.2, 7.3 })
                    {
                        Vec2[] corners = path.Footprint(seg, margin);
                        Vec2 middle = (path.P[seg] + path.P[seg + 1]) * 0.5;
                        for (int k = 0; k < 40; k++)
                        {
                            // Scattered round the segment, and exactly on its corners and the middles of its sides.
                            Vec2 p = k < 4 ? corners[k] : k < 8 ? (corners[k - 4] + corners[(k - 3) % 4]) * 0.5 : middle + Somewhere(random, 50.0);
                            bool expected = PlanMath.PointInPolygon(p, corners);
                            Assert.AreEqual(expected, path.FootprintContains(seg, margin, p), $"deck {deck}, segment {seg}, margin {margin}, point {p.X:R},{p.Y:R}");
                            tested++;
                            if (expected) inside++;
                        }
                    }
                }
            }
            Assert.Greater(inside, tested / 10, "hardly any of the points were on a deck: the test proves little");
            Assert.Less(inside, tested - tested / 10, "nearly every point was on a deck: the test proves little");
        }
    }
}
