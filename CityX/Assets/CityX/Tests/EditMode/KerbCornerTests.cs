using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The kerb going round each street corner in an arc (World/KerbCorners, docs/AMERICAN_STREETS.md): the rule's own geometry on the real
    /// city's corners, and the street the chunks build from it (road in the corner, pavement behind the arc, the kerb round it).
    /// </summary>
    public sealed class KerbCornerTests
    {
        private const int Seed = 48398181;
        private const int ReachChunks = 6;

        private static CityWorld _world;

        private static CityWorld World => _world ??= new CityWorld(Seed);

        private struct Rounded
        {
            public PlanBlock Block;
            public int Vertex;
            public KerbArc Arc;
        }

        /// <summary>Every rounded corner of the blocks within a few chunks of the spawn, and how many kerb corners those blocks have in all.</summary>
        private static List<Rounded> NearTheSpawn(out int corners)
        {
            PlanWorld plan = World.Plan;
            plan.TryFindSpawn(out double sx, out double sz);
            int cx = PlanWorld.ChunkOf(sx), cy = PlanWorld.ChunkOf(sz);
            var seen = new HashSet<int>();
            var found = new List<Rounded>();
            corners = 0;
            for (int dy = -ReachChunks; dy <= ReachChunks; dy++)
            {
                for (int dx = -ReachChunks; dx <= ReachChunks; dx++)
                {
                    foreach (int index in plan.BlocksIn(cx + dx, cy + dy))
                    {
                        if (!seen.Add(index))
                        {
                            continue;
                        }
                        PlanBlock block = plan.Data.Blocks[index];
                        for (int v = 0; block.Inner != null && v < block.Inner.Count; v++)
                        {
                            corners++;
                            if (KerbCorners.TryAt(plan, block, v, out KerbArc arc))
                            {
                                found.Add(new Rounded { Block = block, Vertex = v, Arc = arc });
                            }
                        }
                    }
                }
            }
            return found;
        }

        private static double OffTheLine(Vec2 p, Vec2 a, Vec2 b) => System.Math.Abs(Vec2.Cross((b - a).Normalized, p - a));

        [Test]
        public void MostStreetCornersAreRounded()
        {
            List<Rounded> rounded = NearTheSpawn(out int corners);
            TestContext.WriteLine($"{rounded.Count} of {corners} kerb corners round the spawn are rounded");
            Assert.Greater(rounded.Count, 40, "hardly a corner near the spawn is rounded");
            Assert.Greater(rounded.Count, corners / 3, "most of the kerb's corners are still sharp");
        }

        [Test]
        public void AnArcLeavesBothKerbsAtATangentAndKeepsWithinItsLimits()
        {
            foreach (Rounded r in NearTheSpawn(out _))
            {
                KerbArc arc = r.Arc;
                PlanBlock block = r.Block;
                int n = block.Inner.Count;
                Vec2 corner = block.Inner[r.Vertex], before = block.Inner[(r.Vertex + n - 1) % n], after = block.Inner[(r.Vertex + 1) % n];
                string where = $"the corner at {corner.X:0},{corner.Y:0}";

                Assert.Less((arc.Corner - corner).Length, 1e-9, where);
                Assert.Less(OffTheLine(arc.From, before, corner), 1e-6, $"{where}: the arc does not start on the kerb coming in");
                Assert.Less(OffTheLine(arc.To, corner, after), 1e-6, $"{where}: the arc does not end on the kerb going out");
                Assert.AreEqual((arc.From - corner).Length, (arc.To - corner).Length, 1e-6, $"{where}: the arc takes more of one kerb than the other");
                Assert.AreEqual(arc.Radius, (arc.From - arc.Centre).Length, 1e-6, where);
                Assert.AreEqual(arc.Radius, (arc.To - arc.Centre).Length, 1e-6, where);
                Assert.AreEqual(0.0, Vec2.Dot((arc.From - arc.Centre).Normalized, (corner - arc.From).Normalized), 1e-6, $"{where}: the arc meets the kerb at an angle, not a tangent");

                Assert.GreaterOrEqual(arc.Radius, KerbCorners.LeastRadius - 1e-9, where);
                Assert.LessOrEqual(arc.Radius, KerbCorners.BoulevardRadius + 1e-9, where);
                Assert.LessOrEqual(arc.TangentLength, KerbCorners.LongestTangent + 1e-6, $"{where}: the arc reaches where things stand along the pavement");
                Assert.LessOrEqual(arc.TangentLength, System.Math.Min((before - corner).Length, (after - corner).Length) * KerbCorners.MostOfAKerb + 1e-6,
                    $"{where}: the arc takes too much of a short kerb");
                Assert.LessOrEqual(arc.SetBack + KerbCorners.PavementLeft, (block.Outer[r.Vertex] - corner).Length + 1e-6,
                    $"{where}: the arc leaves too little pavement at the corner of the lots");

                Assert.Less((arc.PointAt(0.0) - arc.From).Length, 1e-6, where);
                Assert.Less((arc.PointAt(1.0) - arc.To).Length, 1e-6, where);
                Assert.Less((arc.PointAt(0.5) - arc.Apex).Length, 1e-6, $"{where}: the arc goes the long way round");
                Assert.IsTrue(arc.GivesUp(corner + arc.Inward * 0.05), $"{where}: the sharp corner itself is not given up to the road");
                Assert.IsFalse(arc.GivesUp(arc.Apex + arc.Inward * 0.2), $"{where}: the pavement behind the arc is given up to the road");
                Assert.IsFalse(arc.GivesUp(arc.From + (arc.From - corner).Normalized * 0.2 + arc.Inward * 0.2), $"{where}: the pavement along the straight kerb is given up");

                Assert.IsTrue(KerbCorners.TryAt(World.Plan, corner, out KerbArc again), $"{where}: not found by its place");
                Assert.Less((again.From - arc.From).Length + (again.To - arc.To).Length, 1e-9, $"{where}: found by its place, it is another arc");
            }
        }

        [Test]
        public void AStreetThatIsWalkedOrClimbedKeepsItsSharpCorners()
        {
            PlanWorld plan = World.Plan;
            foreach (Rounded r in NearTheSpawn(out _))
            {
                int n = r.Block.Poly.Count;
                foreach (RoadEdge road in new[]
                         {
                             plan.EdgeBetween(r.Block.Poly[(r.Vertex + n - 1) % n], r.Block.Poly[r.Vertex]),
                             plan.EdgeBetween(r.Block.Poly[r.Vertex], r.Block.Poly[(r.Vertex + 1) % n]),
                         })
                {
                    Assert.IsNotNull(road);
                    Assert.IsFalse(PedestrianStreets.Is(plan, road), "a pedestrian street, laid flush with the pavement, has a rounded kerb");
                    Assert.IsFalse(StepStreets.Is(plan, road), "a step street has a rounded kerb");
                    Assert.IsFalse(road.Bridge, "a bridge has a rounded kerb");
                }
            }
        }

        /// <summary>The height of the highest face of a buffer that looks up, over a point of the world; NaN where there is none.</summary>
        private static float SurfaceAt(MeshBuffer buffer, Vector3 origin, Vec2 at)
        {
            float best = float.NaN;
            var p = new Vector2((float)at.X - origin.x, (float)at.Y - origin.z);
            for (int t = 0; t + 2 < buffer.Triangles.Count; t += 3)
            {
                Vector3 a = buffer.Vertices[buffer.Triangles[t]], b = buffer.Vertices[buffer.Triangles[t + 1]], c = buffer.Vertices[buffer.Triangles[t + 2]];
                if (Vector3.Cross(b - a, c - a).normalized.y < 0.5f)
                {
                    continue;
                }
                var a2 = new Vector2(a.x, a.z);
                var b2 = new Vector2(b.x, b.z);
                var c2 = new Vector2(c.x, c.z);
                float d = (b2.y - c2.y) * (a2.x - c2.x) + (c2.x - b2.x) * (a2.y - c2.y);
                if (Mathf.Abs(d) < 1e-9f)
                {
                    continue;
                }
                float u = ((b2.y - c2.y) * (p.x - c2.x) + (c2.x - b2.x) * (p.y - c2.y)) / d;
                float v = ((c2.y - a2.y) * (p.x - c2.x) + (a2.x - c2.x) * (p.y - c2.y)) / d;
                float w = 1.0f - u - v;
                if (u < -1e-4f || v < -1e-4f || w < -1e-4f)
                {
                    continue;
                }
                float y = u * a.y + v * b.y + w * c.y;
                if (float.IsNaN(best) || y > best)
                {
                    best = y;
                }
            }
            return best;
        }

        [Test]
        public void TheBuiltStreetIsRoadInTheCornerAndPavementBehindTheArcWithTheKerbRoundIt()
        {
            PlanWorld plan = World.Plan;
            var ground = new MeshBuffer();
            var pavement = new MeshBuffer();
            var built = new MeshBuffer();
            var markings = new MeshBuffer();
            var dividers = new MeshBuffer();
            var rail = new MeshBuffer();
            int looked = 0;
            foreach (Rounded r in NearTheSpawn(out _))
            {
                KerbArc arc = r.Arc;
                // Points well inside one chunk, so one chunk's build holds them all.
                Vec2 inTheCorner = arc.Corner + arc.Inward * (arc.SetBack * 0.5);
                Vec2 behindTheArc = arc.Apex + arc.Inward * 0.4;
                Vec2 onTheArc = arc.PointAt(1.0 / arc.Pieces);
                int cx = PlanWorld.ChunkOf(inTheCorner.X), cy = PlanWorld.ChunkOf(inTheCorner.Y);
                bool oneChunk = true;
                foreach (Vec2 point in new[] { behindTheArc, onTheArc, arc.Corner, arc.From, arc.To })
                {
                    oneChunk &= PlanWorld.ChunkOf(point.X) == cx && PlanWorld.ChunkOf(point.Y) == cy;
                }
                if (!oneChunk)
                {
                    continue;
                }
                foreach (MeshBuffer buffer in new[] { ground, pavement, built, markings, dividers, rail })
                {
                    buffer.Reset();
                }
                var origin = new Vector3(cx * (float)PlanWorld.ChunkSize, 0.0f, cy * (float)PlanWorld.ChunkSize);
                CitySurfaceBuilder.Build(World, cx, cy, origin, ground, pavement, built, markings, dividers, rail, false);
                string where = $"the corner at {arc.Corner.X:0},{arc.Corner.Y:0}";

                float road = (float)plan.GroundHeight(inTheCorner.X, inTheCorner.Y) + CitySurfaceBuilder.RoadLift;
                Assert.AreEqual(road, SurfaceAt(ground, origin, inTheCorner), 0.03f, $"{where}: no road in the corner the arc gives up");
                float over = SurfaceAt(pavement, origin, inTheCorner);
                Assert.IsTrue(float.IsNaN(over) || over < road + 0.03f, $"{where}: the pavement still lies over the corner ({over - road:0.00} m above the road)");

                float walk = (float)plan.GroundHeight(behindTheArc.X, behindTheArc.Y) + CitySurfaceBuilder.RoadLift + CitySurfaceBuilder.KerbHeight;
                Assert.AreEqual(walk, SurfaceAt(pavement, origin, behindTheArc), 0.03f, $"{where}: no pavement behind the arc");

                // The kerb stands round the arc: its foot and its top at the arc's middle, and none left at the sharp corner.
                bool foot = false, top = false, sharp = false;
                float arcRoad = (float)plan.GroundHeight(onTheArc.X, onTheArc.Y) + CitySurfaceBuilder.RoadLift;
                float cornerRoad = (float)plan.GroundHeight(arc.Corner.X, arc.Corner.Y) + CitySurfaceBuilder.RoadLift;
                for (int i = 0; i < pavement.Vertices.Count; i++)
                {
                    Vector3 v = pavement.Vertices[i] + origin;
                    if (Mathf.Abs(pavement.Normals[i].y) > 0.1f)
                    {
                        continue;   // a face that looks up or down is paving, not kerb
                    }
                    var flat = new Vec2(v.x, v.z);
                    if ((flat - onTheArc).Length < 0.03)
                    {
                        foot |= Mathf.Abs(v.y - arcRoad) < 0.02f;
                        top |= Mathf.Abs(v.y - (arcRoad + CitySurfaceBuilder.KerbHeight)) < 0.02f;
                    }
                    sharp |= (flat - arc.Corner).Length < 0.03 && Mathf.Abs(v.y - (cornerRoad + CitySurfaceBuilder.KerbHeight)) < 0.02f;
                }
                Assert.IsTrue(foot && top, $"{where}: no kerb stands round the arc");
                Assert.IsFalse(sharp, $"{where}: the kerb still comes to its sharp corner");
                if (++looked >= 12)
                {
                    break;
                }
            }
            Assert.GreaterOrEqual(looked, 6, "hardly any rounded corner lay inside one chunk to be looked at");
        }
    }
}
