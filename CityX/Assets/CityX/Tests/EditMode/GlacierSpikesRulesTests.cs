using System.Collections.Generic;
using System.Linq;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Glacier Spikes with no scene (docs/GLACIER_SPIKES.md): the band widening down the line, the front's pace, the crystals' heights ramping
    /// and swelling at the end, the impact cluster round the end, the same seed growing the same field, a crystal rising, springing, settling
    /// and sinking, whom the front strikes, the line over stairs and up to a wall, the crystal's mesh, and a forwarded frost blow's chill.
    /// </summary>
    public sealed class GlacierSpikesRulesTests
    {
        private static readonly GlacierSpikesCast Line = new GlacierSpikesCast(new Vector3(3.0f, 1.0f, -2.0f), new Vector3(1.0f, 0.0f, 1.0f), 20.0f, 417);

        private static GlacierSpike[] Field(int seed)
        {
            var spikes = new GlacierSpike[GlacierSpikesRules.SpikeCount];
            GlacierSpikesLayout.Build(seed, spikes);
            return spikes;
        }

        [Test]
        public void TheBandWidensFromTheCasterToTheFarEnd()
        {
            Assert.AreEqual(GlacierSpikesRules.WidthNear, GlacierSpikesRules.HalfWidth(0.0f), 1e-4f);
            Assert.AreEqual(GlacierSpikesRules.Width, GlacierSpikesRules.HalfWidth(1.0f), 1e-4f);
            for (float s = 0.05f; s <= 1.0f; s += 0.05f)
            {
                Assert.Greater(GlacierSpikesRules.HalfWidth(s), GlacierSpikesRules.HalfWidth(s - 0.05f), $"narrower at {s:0.00}");
            }
            // Scaled from the reference's 1.8 m figure to the 2.6 m body.
            Assert.AreEqual(2.5f * 1.4f, GlacierSpikesRules.Width, 1e-4f);
            Assert.AreEqual(21.0f, GlacierSpikesRules.Range, 1e-3f);
        }

        [Test]
        public void TheFrontRunsAtItsSpeedAndArrivesWhenItSaysItWill()
        {
            Assert.AreEqual(0.0f, GlacierSpikesRules.FrontDistance(0.0f));
            float late = GlacierSpikesRules.FrontDistance(0.5f) - GlacierSpikesRules.FrontDistance(0.4f);
            Assert.AreEqual(GlacierSpikesRules.FrontSpeed * 0.1f, late, 1e-3f, "not at full speed after its ease-in");
            Assert.Less(GlacierSpikesRules.FrontDistance(0.04f), GlacierSpikesRules.FrontSpeed * 0.04f, "no ease-in");
            foreach (float distance in new[] { 0.3f, 1.0f, 5.0f, 21.0f })
            {
                Assert.AreEqual(distance, GlacierSpikesRules.FrontDistance(GlacierSpikesRules.ArrivalTime(distance)), 1e-3f, $"at {distance} m");
            }
            Assert.That(GlacierSpikesRules.ArrivalTime(GlacierSpikesRules.Range), Is.InRange(0.55f, 0.65f), "the full line is not run in about 0.6 s");
        }

        [Test]
        public void TheSameSeedGrowsTheSameFieldAndAnotherSeedAnother()
        {
            GlacierSpike[] a = Field(417);
            GlacierSpike[] b = Field(417);
            GlacierSpike[] c = Field(418);
            int differ = 0;
            for (int i = 0; i < a.Length; i++)
            {
                foreach (float age in new[] { 0.3f, 1.0f, 4.5f })
                {
                    GlacierSpikePose p = GlacierSpikesLayout.Pose(a[i], Line, age, 1.0f, float.MaxValue);
                    GlacierSpikePose q = GlacierSpikesLayout.Pose(b[i], Line, age, 1.0f, float.MaxValue);
                    Assert.AreEqual(p.Visible, q.Visible);
                    if (!p.Visible)
                    {
                        continue;
                    }
                    Assert.Less(Vector3.Distance(p.Position, q.Position), 1e-5f, $"crystal {i} stands elsewhere");
                    Assert.Less(Quaternion.Angle(p.Rotation, q.Rotation), 1e-3f);
                    Assert.Less(Vector3.Distance(p.Size, q.Size), 1e-5f);
                }
                if (Vector3.Distance(GlacierSpikesLayout.Pose(a[i], Line, 2.0f, 1.0f, float.MaxValue).Position,
                        GlacierSpikesLayout.Pose(c[i], Line, 2.0f, 1.0f, float.MaxValue).Position) > 0.05f)
                {
                    differ++;
                }
            }
            Assert.Greater(differ, a.Length / 2, "another seed grew nearly the same field");
        }

        [Test]
        public void HeightsRampDownTheLineAndTheImpactClusterSitsRoundTheEnd()
        {
            GlacierSpike[] spikes = Field(9);
            int impact = spikes.Count(s => s.Impact);
            Assert.AreEqual(Mathf.RoundToInt(GlacierSpikesRules.SpikeCount * GlacierSpikesRules.ImpactShare), impact, "about a fifth held back for the end");

            List<GlacierSpike> ground = spikes.Where(s => !s.Impact && !s.Rubble).ToList();
            float near = ground.Where(s => s.Along < 0.3f).Average(s => GlacierSpikesLayout.HeightOf(s));
            float middle = ground.Where(s => s.Along >= 0.4f && s.Along < 0.6f).Average(s => GlacierSpikesLayout.HeightOf(s));
            float far = ground.Where(s => s.Along >= 0.85f).Average(s => GlacierSpikesLayout.HeightOf(s));
            TestContext.WriteLine($"mean height near {near:0.00} m, middle {middle:0.00}, far {far:0.00}");
            Assert.Less(near, middle);
            Assert.Greater(far, middle * 1.5f, "the far end is not much taller");
            Assert.AreEqual(GlacierSpikesRules.Height * GlacierSpikesRules.Peak, GlacierSpikesLayout.HeightOf(new GlacierSpike { Along = 1.0f }), 1e-3f,
                "no swell at the end");

            var typical = new GlacierSpike { Along = 0.5f, Lateral = 0.0f };
            var flank = new GlacierSpike { Along = 0.5f, Lateral = 1.0f };
            var rubble = new GlacierSpike { Along = 0.5f, Lateral = 0.0f, Rubble = true };
            Assert.Less(GlacierSpikesLayout.HeightOf(flank), GlacierSpikesLayout.HeightOf(typical), "the flank blades are not shorter");
            Assert.AreEqual(GlacierSpikesLayout.HeightOf(typical) * GlacierSpikesRules.RubbleScale, GlacierSpikesLayout.HeightOf(rubble), 1e-4f);

            float radius = GlacierSpikesRules.ImpactRadius + 1e-3f;
            foreach (GlacierSpike s in spikes.Where(s => s.Impact))
            {
                GlacierSpikePose pose = GlacierSpikesLayout.Pose(s, Line, 3.0f, Line.Start.y, float.MaxValue);
                Vector3 fromEnd = pose.Position - Line.End;
                fromEnd.y = 0.0f;
                Assert.LessOrEqual(fromEnd.magnitude, radius, "an impact crystal stands outside the cluster round the end");
            }
            foreach (GlacierSpike s in spikes.Where(s => !s.Impact))
            {
                GlacierSpikePose pose = GlacierSpikesLayout.Pose(s, Line, 3.0f, Line.Start.y, float.MaxValue);
                Vector3 flat = pose.Position - Line.Start;
                float along = Vector3.Dot(flat, Line.Direction);
                float across = Mathf.Abs(Vector3.Dot(flat, Line.Side));
                Assert.That(along, Is.InRange(-1e-3f, Line.Length + 1e-3f), "a crystal off the line's ends");
                Assert.LessOrEqual(across, GlacierSpikesRules.HalfWidth(along / Line.Length) * (1.0f + GlacierSpikesRules.Scatter) + 1e-3f, "a crystal outside the band");
            }
        }

        [Test]
        public void ACrystalRisesSpringsPastItsHeightSettlesAndSinksAtTheEnd()
        {
            Assert.Less(GlacierSpikesRules.Emergence(-0.01f), 0.0f, "out before the front reached it");
            Assert.AreEqual(0.0f, GlacierSpikesRules.Emergence(0.0f), 1e-5f);
            Assert.Greater(GlacierSpikesRules.Emergence(GlacierSpikesRules.RiseSeconds * 0.5f), 0.9f, "outQuint: most of the way at half time");
            Assert.AreEqual(1.0f, GlacierSpikesRules.Emergence(GlacierSpikesRules.RiseSeconds), 1e-4f);
            float most = 0.0f;
            for (float t = GlacierSpikesRules.RiseSeconds; t < 0.6f; t += 0.005f)
            {
                most = Mathf.Max(most, GlacierSpikesRules.Emergence(t));
            }
            Assert.Greater(most, 1.15f, "no spring past its height");
            Assert.AreEqual(1.0f, GlacierSpikesRules.Emergence(3.0f), 0.01f, "it never settled");

            var spike = new GlacierSpike { Along = 0.5f };
            float erupts = GlacierSpikesLayout.EruptTime(spike, Line);
            Assert.AreEqual(GlacierSpikesRules.ArrivalTime(Line.Length * 0.5f), erupts, 1e-4f, "it breaks out when the front passes it");
            Assert.IsFalse(GlacierSpikesLayout.Pose(spike, Line, erupts - 0.01f, 1.0f, float.MaxValue).Visible, "seen before the front reached it");
            GlacierSpikePose rising = GlacierSpikesLayout.Pose(spike, Line, erupts + 0.03f, 1.0f, float.MaxValue);
            GlacierSpikePose standing = GlacierSpikesLayout.Pose(spike, Line, GlacierSpikesRules.SinkStartSeconds - 0.3f, 1.0f, float.MaxValue);
            Assert.Less(rising.Position.y, standing.Position.y, "it does not slide up out of the ground");
            Assert.Less(rising.Size.y, standing.Size.y, "it does not grow as it rises");
            Assert.AreEqual(standing.Position.y, GlacierSpikesLayout.Pose(spike, Line, GlacierSpikesRules.SinkStartSeconds, 1.0f, float.MaxValue).Position.y,
                0.01f, "it sank before its time (only the last of its spring is left)");
            GlacierSpikePose sunk = GlacierSpikesLayout.Pose(spike, Line, GlacierSpikesRules.EndSeconds - 0.001f, 1.0f, float.MaxValue);
            Assert.IsFalse(sunk.Visible && sunk.Position.y + sunk.Size.y > 1.0f, "it is still out of the ground at the end");
        }

        [Test]
        public void TheFrontStrikesInTheBandBehindItAndRoundTheEndOnceThere()
        {
            Vector3 start = Vector3.zero;
            Vector3 way = Vector3.forward;
            const float length = 20.0f;
            var onTheLine = new Vector3(0.5f, 0.0f, 10.0f);
            Assert.IsFalse(GlacierSpikesRules.Strikes(start, way, length, 9.0f, onTheLine, 0.0f), "struck before the front reached it");
            Assert.IsTrue(GlacierSpikesRules.Strikes(start, way, length, 10.5f, onTheLine, 0.0f));
            Assert.IsFalse(GlacierSpikesRules.Strikes(start, way, length, 20.0f, new Vector3(6.0f, 0.0f, 10.0f), 0.0f), "struck off the band");
            Assert.IsFalse(GlacierSpikesRules.Strikes(start, way, length, 20.0f, onTheLine + Vector3.up * 6.0f, 0.0f), "struck on a balcony above");
            Assert.IsFalse(GlacierSpikesRules.Strikes(start, way, length, 20.0f, new Vector3(0.0f, 0.0f, -3.0f), 0.0f), "struck behind the caster");
            var pastTheEnd = new Vector3(2.0f, 0.0f, 23.0f);
            Assert.IsFalse(GlacierSpikesRules.Strikes(start, way, length, 19.0f, pastTheEnd, 0.0f), "the end's cluster struck before the front got there");
            Assert.IsTrue(GlacierSpikesRules.Strikes(start, way, length, 20.0f, pastTheEnd, 0.0f), "the end's cluster struck nobody round it");
            Assert.IsTrue(GlacierSpikesRules.Strikes(start, way, length, 20.0f, new Vector3(-3.6f, 0.0f, 19.0f), 0.0f), "the wide far end missed its edge");
        }

        /// <summary>A world of flat ground with a stair up and an optional wall across the line, for the line to walk.</summary>
        private sealed class StairsAndWall : IGlacierGround
        {
            public float StairFrom = 5.0f;
            public float StairTo = 7.0f;
            public float StairRise = 1.2f;
            public float WallAt = float.MaxValue;
            public float GapFrom = float.MaxValue;

            private float HeightAt(float z)
            {
                if (z >= GapFrom)
                {
                    return -100.0f;
                }
                return z < StairFrom ? 0.0f : z >= StairTo ? StairRise : Mathf.Floor((z - StairFrom) / 0.4f + 1.0f) * StairRise * 0.4f / (StairTo - StairFrom);
            }

            public bool TryGround(Vector3 above, float depth, out float y)
            {
                y = HeightAt(above.z);
                return above.y - y >= 0.0f && above.y - y <= depth;
            }

            public float ClearDistance(Vector3 from, Vector3 to) =>
                Mathf.Min(from.z, to.z) <= WallAt && Mathf.Max(from.z, to.z) >= WallAt ? Mathf.Abs(WallAt - from.z) : Vector3.Distance(from, to);
        }

        [Test]
        public void TheLineFollowsStairsAndStopsAtAWallOrADrop()
        {
            var path = new GlacierSpikesPath();
            var world = new StairsAndWall();
            Assert.IsTrue(path.Walk(new Vector3(0.0f, 0.05f, 0.0f), Vector3.forward + Vector3.down, world));
            Assert.AreEqual(GlacierSpikesRules.Range, path.Length, 1e-3f, "the open line is not its full reach");
            Assert.AreEqual(GlacierSpikesRules.StartAhead, path.Start.z, 1e-4f, "it does not start just ahead of the feet");
            Assert.AreEqual(0.0f, path.HeightAt(2.0f), 1e-4f);
            Assert.AreEqual(world.StairRise, path.HeightAt(10.0f), 1e-3f, "it did not climb the stair");

            world.WallAt = 12.3f;
            path.Walk(Vector3.zero, Vector3.forward, world);
            Assert.That(path.Length, Is.InRange(12.3f - GlacierSpikesRules.StartAhead - GlacierSpikesRules.GroundStep, 12.3f - GlacierSpikesRules.StartAhead),
                "it went through the wall, or stopped short of it");

            world.WallAt = 0.5f;
            path.Walk(Vector3.zero, Vector3.forward, world);
            Assert.IsFalse(path.IsCastable, "cast into a wall at the nose");

            world.WallAt = float.MaxValue;
            world.GapFrom = 15.0f;
            path.Walk(Vector3.zero, Vector3.forward, world);
            Assert.Less(path.Length, 15.0f - GlacierSpikesRules.StartAhead, "it ran on over a drop too deep");

            Assert.IsFalse(path.Walk(new Vector3(0.0f, 30.0f, 0.0f), Vector3.forward, new StairsAndWall()), "cast from high in the air");
        }

        [Test]
        public void TheCrystalIsAClosedColumnFromTheGroundToItsTip()
        {
            IceCrystalMesh.Shape shape = IceCrystalMesh.Build(7.3f, GlacierSpikesRules.Facets, GlacierSpikesRules.Taper, GlacierSpikesRules.Roughness,
                GlacierSpikesRules.Bend);
            Assert.AreEqual(GlacierSpikesRules.Facets * 10 * 3, shape.Triangles.Count, "not the reference's 10 triangles a facet");
            float lowest = shape.Vertices.Min(v => v.y);
            float highest = shape.Vertices.Max(v => v.y);
            Assert.AreEqual(0.0f, lowest, 1e-4f, "the base is not on the ground");
            Assert.AreEqual(1.0f, highest, 1e-4f, "the tip is not at the top");
            Assert.AreEqual(shape.Vertices.Count, shape.SmoothNormals.Count);
            for (int t = 0; t < shape.Triangles.Count; t += 3)
            {
                Vector3 a = shape.Vertices[shape.Triangles[t]];
                Vector3 b = shape.Vertices[shape.Triangles[t + 1]];
                Vector3 c = shape.Vertices[shape.Triangles[t + 2]];
                Vector3 wound = Vector3.Cross(b - a, c - a);
                Assert.Greater(Vector3.Dot(wound, shape.Normals[shape.Triangles[t]]), 0.0f, "a face wound against its normal");
                Vector3 middle = (a + b + c) / 3.0f;
                Vector3 outward = middle - new Vector3(0.0f, middle.y, 0.0f);
                bool baseFace = Mathf.Max(a.y, Mathf.Max(b.y, c.y)) < 1e-4f;
                Vector3 normal = shape.Normals[shape.Triangles[t]];
                Assert.Greater(baseFace ? -normal.y : Vector3.Dot(normal, outward.normalized) + normal.y, 0.0f, "a face points into the crystal");
            }
        }

        [Test]
        public void AForwardedFrostBlowCarriesHowMuchItChills()
        {
            Assert.AreEqual(0.0f, NetworkedDamage.ChillOf(NetworkedDamage.Flags(true, false, true)));
            Assert.AreEqual(FrostRules.ChillPerTick, NetworkedDamage.ChillOf(NetworkedDamage.Flags(false, true, false, FrostRules.ChillPerTick)), 1e-5f,
                "the frost spray's tick");
            int flags = NetworkedDamage.Flags(false, false, true, GlacierSpikesRules.ChillAmount, true, true);
            Assert.AreEqual(GlacierSpikesRules.ChillAmount, NetworkedDamage.ChillOf(flags), 1e-5f, "Glacier Spikes' chill");
            Assert.AreEqual(NetworkedDamage.Flags(false, false, true, 0.0f, true, true), flags & 0xFF & ~8, "the other flags were disturbed");
        }
    }
}
