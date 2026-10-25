using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>Pedestrian streets (World/PedestrianStreets, docs/OPEN_SPACE.md 8): which streets, what stands on them, and the chunk they draw.</summary>
    public sealed class PedestrianStreetTests
    {
        private const int Seed = 23481876;
        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));
        private static PlanWorld Plan => World.Plan;

        private static IEnumerable<RoadEdge> Chosen()
        {
            foreach (RoadEdge e in Plan.Data.Edges)
            {
                if (PedestrianStreets.Is(Plan, e)) yield return e;
            }
        }

        [Test]
        public void AFewStreetsInDenseCountryAreClosedToCarsAndNoneStrandsAnything()
        {
            int streets = 0, chosen = 0;
            foreach (RoadEdge e in Plan.Data.Edges)
            {
                if (e == null || !e.Alive || e.Class != RoadClass.Street || e.Bridge) continue;
                Vec2 mid = (e.A.P + e.B.P) * 0.5;
                if (Plan.Fields.Population(mid) < PedestrianStreets.MinPopulation) continue;
                double s = Plan.Fields.ScaleAt(mid.X, mid.Y);
                if (e.Length < PedestrianStreets.MinLength * s || e.Length > PedestrianStreets.MaxLength * s) continue;
                streets++;
                if (!PedestrianStreets.Is(Plan, e)) continue;
                chosen++;
                int liveA = 0, liveB = 0;
                foreach (RoadEdge x in e.A.Edges) if (x.Alive) liveA++;
                foreach (RoadEdge x in e.B.Edges) if (x.Alive) liveB++;
                Assert.GreaterOrEqual(liveA, 3, $"edge {e.Id}: closing it strands its end");
                Assert.GreaterOrEqual(liveB, 3, $"edge {e.Id}: closing it strands its end");
            }
            Assert.Greater(chosen, 5, "too few pedestrian streets");
            double share = chosen / (double)streets;
            TestContext.WriteLine($"{chosen} of {streets} short dense streets are pedestrian ({share:P1})");
            Assert.That(share, Is.InRange(0.02, 0.09));
            foreach (RoadEdge e in Plan.Data.Edges)
            {
                if (e != null && (e.Class != RoadClass.Street || e.Bridge)) Assert.IsFalse(PedestrianStreets.Is(Plan, e), $"edge {e.Id}: a {e.Class}{(e.Bridge ? " bridge" : "")} made pedestrian");
            }
        }

        [Test]
        public void TheFurnitureStandsInTheStreetWithAWalkBesideItAndBollardsAcrossBothEnds()
        {
            int checkedStreets = 0;
            foreach (RoadEdge e in Chosen())
            {
                EdgeGeometry g = Plan.Geometry.Edges[e.Id];
                if (g == null) continue;
                List<OpenPiece> pieces = PedestrianStreets.Furniture(Plan, g);
                if (pieces.Count == 0) continue;
                checkedStreets++;
                int bollards = 0;
                foreach (OpenPiece p in pieces)
                {
                    foreach (Vec2 end in new[] { p.A, p.B })
                    {
                        double along = Vec2.Dot(end - g.A, g.Dir), across = Vec2.Dot(end - g.A, g.Dir.Perp);
                        Assert.That(along, Is.InRange(g.TrimA - 1e-6, g.Length - g.TrimB + 1e-6), $"edge {e.Id}: a {p.Kind} off the ribbon's length");
                        Assert.LessOrEqual(System.Math.Abs(across) + p.HalfZ, g.HalfWidth + 1e-6, $"edge {e.Id}: a {p.Kind} over the ribbon's side");
                    }
                    if (p.Kind == OpenProp.Bollard)
                    {
                        bollards++;
                    }
                    else if (p.Kind != OpenProp.Planter)
                    {
                        double across = System.Math.Abs(Vec2.Dot(p.At - g.A, g.Dir.Perp));
                        Assert.GreaterOrEqual(g.HalfWidth - across - p.HalfZ, PedestrianStreets.WalkWidth - 1e-6, $"edge {e.Id}: a {p.Kind} leaves no walk beside it");
                    }
                }
                Assert.GreaterOrEqual(bollards, 4, $"edge {e.Id}: no bollards across its ends");
            }
            Assert.Greater(checkedStreets, 3);
        }

        [Test]
        public void ABlockEdgeAlongAPedestrianStreetKnowsIt()
        {
            int along = 0;
            foreach (RoadEdge e in Chosen())
            {
                Assert.AreSame(e, Plan.EdgeBetween(e.A.P, e.B.P), "the edge between its own nodes");
                Assert.AreSame(e, Plan.EdgeBetween(e.B.P, e.A.P), "either way round");
                Assert.IsTrue(PedestrianStreets.Along(Plan, e.A.P, e.B.P));
                along++;
                if (along >= 20) break;
            }
            Assert.Greater(along, 0);
            Assert.IsNull(Plan.EdgeBetween(new Vec2(123456.0, 654321.0), new Vec2(0.0, 0.0)), "no node there");
        }

        [Test]
        public void ThePedestrianStreetIsLaidInSettsAtThePavementsHeightWithNoPaintAndItsBollardsStand()
        {
            RoadEdge edge = null;
            EdgeGeometry g = null;
            foreach (RoadEdge e in Chosen())
            {
                g = Plan.Geometry.Edges[e.Id];
                if (g != null && g.Length - g.TrimA - g.TrimB > 20.0) { edge = e; break; }
            }
            Assert.IsNotNull(edge, "no pedestrian street with a ribbon to check");
            Vec2 mid = g.A + g.Dir * (g.Length * 0.5);
            int cx = PlanWorld.ChunkOf(mid.X), cy = PlanWorld.ChunkOf(mid.Y);
            var buffers = new MeshBuffer[6];
            for (int i = 0; i < 6; i++) buffers[i] = new MeshBuffer();
            var origin = new Vector3(cx * (float)PlanWorld.ChunkSize, 0.0f, cy * (float)PlanWorld.ChunkSize);
            CitySurfaceBuilder.Build(World, cx, cy, origin, buffers[0], buffers[1], buffers[2], buffers[3], buffers[4], buffers[5], false);

            bool Inside(Vector3 v, double margin)
            {
                var p = new Vec2(v.x + origin.x, v.z + origin.z);
                double along = Vec2.Dot(p - g.A, g.Dir), across = Vec2.Dot(p - g.A, g.Dir.Perp);
                return along > g.TrimA + margin && along < g.Length - g.TrimB - margin && System.Math.Abs(across) < g.HalfWidth - margin;
            }
            int setts = 0, other = 0, paint = 0;
            foreach (MeshBuffer paintBuffer in new[] { buffers[3], buffers[4] })
            {
                foreach (Vector3 v in paintBuffer.Vertices) if (Inside(v, 0.2)) paint++;
            }
            // Each upward face whose middle is on the street, well in from its edges (a street piece has corners only at its edges, and where a
            // chunk's border cuts it: counting corners inside the street found none when no border crossed it), with every corner at the pavement's height.
            List<Vector3> verts = buffers[0].Vertices;
            for (int i = 0; i + 2 < buffers[0].Triangles.Count; i += 3)
            {
                int ia = buffers[0].Triangles[i], ib = buffers[0].Triangles[i + 1], ic = buffers[0].Triangles[i + 2];
                Vector3 a = verts[ia], b = verts[ib], c = verts[ic];
                if (Vector3.Cross(b - a, c - a).normalized.y < 0.35f && Vector3.Cross(b - a, c - a).normalized.y > -0.35f) continue;   // a skirt
                if (!Inside((a + b + c) / 3.0f, 0.5)) continue;
                SurfaceLook.Unpack(buffers[0].Colors[ia], out _, out int slice, out _);
                if (slice == SurfaceLook.Setts) setts++; else other++;
                foreach (Vector3 v in new[] { a, b, c })
                {
                    double expected = Plan.GroundHeight(v.x + origin.x, v.z + origin.z) + CitySurfaceBuilder.RoadLift + CitySurfaceBuilder.KerbHeight;
                    Assert.AreEqual(expected, v.y + origin.y, 0.02, "the ribbon is not at the pavement's height");
                }
            }
            Assert.Greater(setts, 0, "no setts on the pedestrian street");
            Assert.AreEqual(0, other, "the pedestrian street has asphalt in it");
            Assert.AreEqual(0, paint, "the pedestrian street has lane paint or a crossing on it");
            Assert.IsTrue(HasLook(buffers[2], CityProps.DarkMetal), "no bollards (dark metal) on the street's chunk");
        }

        private static bool HasLook(MeshBuffer buffer, Color32 look)
        {
            foreach (Color32 c in buffer.Colors)
            {
                if (c.r == look.r && c.g == look.g && c.b == look.b && c.a == look.a) return true;
            }
            return false;
        }
    }
}
