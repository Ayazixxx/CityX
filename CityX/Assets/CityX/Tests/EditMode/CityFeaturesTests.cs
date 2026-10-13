using System;
using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The city's life (docs/HIGHWAYS.md, docs/CITY_LIFE.md): expressways on decks above their service roads, with exits a mile apart, a ring
    /// road round the centre and ends that come down to the ground; interchange ramps a car could drive; parking lots and garages on a few per
    /// cent of the lots; malls, very rare and far apart; pedestrian plazas among the dense streets. And the meshes they turn into.
    /// </summary>
    public sealed class CityFeaturesTests
    {
        private const int Seed = 61839025;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));
        private static PlanWorld Plan => World.Plan;

        // ---- expressways ----

        [Test]
        public void TheRoadLiftIsTheSameInThePlanAndTheMesh()
        {
            Assert.AreEqual(PlanGeometry.RoadLift, CitySurfaceBuilder.RoadLift, 1e-9);
        }

        [Test]
        public void ExpresswaysRideOnDecksAboveTheirServiceRoads()
        {
            PlanConfig cfg = Plan.Data.Config;
            int elevated = 0, clear = 0;
            foreach (EdgeGeometry g in Plan.Geometry.Edges)
            {
                if (g == null || g.Edge.Class != RoadClass.Highway || !g.Elevated) continue;
                elevated++;
                // The deck never dips under the road on the ground at either end.
                Assert.GreaterOrEqual(g.DeckA, Plan.GroundHeight(g.A.X, g.A.Y) + PlanGeometry.RoadLift - 0.02, $"edge {g.Edge.Id}: deck under the ground at A");
                Assert.GreaterOrEqual(g.DeckB, Plan.GroundHeight(g.B.X, g.B.Y) + PlanGeometry.RoadLift - 0.02, $"edge {g.Edge.Id}: deck under the ground at B");
                if (Math.Min(g.DeckA, g.DeckB) - g.MaxGround >= cfg.DeckClearance + cfg.DeckThickness - 0.01)
                {
                    clear++;
                }
            }
            Assert.Greater(elevated, 100, "hardly any expressway");
            Assert.Greater(clear / (double)elevated, 0.6, $"only {clear} of {elevated} expressway spans clear the ground by the full clearance");
        }

        [Test]
        public void AnExpresswayEndsByComingDownToTheGround()
        {
            int ends = 0;
            foreach (RoadNode node in Plan.Data.Nodes)
            {
                if (HighwayEdges(node) != 1) continue;
                ends++;
                double top = Plan.Geometry.DeckTop[node.Id];
                Assert.IsFalse(double.IsNaN(top), $"node {node.Id} ends an expressway but has no deck height");
                Assert.AreEqual(Plan.GroundHeight(node.P.X, node.P.Y) + PlanGeometry.RoadLift, top, 0.05, $"node {node.Id}: the expressway's end is not on the ground");
            }
            Assert.GreaterOrEqual(ends, 2, "no expressway ends anywhere: real highways do");
        }

        [Test]
        public void TheRingRoadCirclesTheCentre()
        {
            double radius = Plan.Data.Config.OrbitalRadius;
            Assert.Greater(radius, 0.0);
            var quadrants = new int[4];
            foreach (EdgeGeometry g in Plan.Geometry.Edges)
            {
                if (g == null || g.Edge.Class != RoadClass.Highway) continue;
                Vec2 mid = (g.A + g.B) * 0.5;
                if (Math.Abs(mid.Length - radius) > radius * 0.35) continue;
                quadrants[(mid.X >= 0.0 ? 0 : 1) + (mid.Y >= 0.0 ? 0 : 2)]++;
            }
            for (int q = 0; q < 4; q++)
            {
                Assert.GreaterOrEqual(quadrants[q], 3, $"the ring road has only {quadrants[q]} spans in quadrant {q}");
            }
        }

        // ---- interchanges ----

        [Test]
        public void ExitsAreSpacedLikeACitysAndTheirRampsCanBeDriven()
        {
            CityPlanData data = Plan.Data;
            Assert.GreaterOrEqual(data.ExitNodes.Count, 6, "too few interchanges");
            Assert.GreaterOrEqual(data.Ramps.Count, 10, "too few ramps");
            for (int i = 0; i < data.ExitNodes.Count; i++)
            {
                for (int j = i + 1; j < data.ExitNodes.Count; j++)
                {
                    double d = (data.Nodes[data.ExitNodes[i]].P - data.Nodes[data.ExitNodes[j]].P).Length;
                    Assert.GreaterOrEqual(d, Interchanges.ExitSpacing * 0.9, $"exits {data.ExitNodes[i]} and {data.ExitNodes[j]} are only {d:F0} m apart");
                }
            }
            foreach (RampGeometry r in Plan.Geometry.Ramps)
            {
                double grade = Math.Abs(r.TopStart - r.TopFoot) / r.Length;
                Assert.LessOrEqual(grade, Interchanges.MaxGrade * 1.2, $"a ramp at exit {r.Plan.ExitNode} climbs at {grade * 100.0:F1}%");
                Assert.AreEqual(Plan.GroundHeight(r.Foot.X, r.Foot.Y) + PlanGeometry.RoadLift, r.TopFoot, 0.05, "the ramp's foot is not on the ground");
                EdgeGeometry highway = Plan.Geometry.Edges[r.Plan.HighwayEdge];
                Assert.IsNotNull(highway);
                double along = Vec2.Dot(r.Plan.S - highway.A, highway.Dir);
                Assert.AreEqual(highway.DeckTopAt(along), r.TopStart, 0.05, "the ramp does not start at the deck's height");
                Assert.Greater(r.TopStart, r.TopFoot + 2.0, "a ramp that does not climb anything");
                // One straight grade: no hump at the bend.
                Assert.That(r.TopMid, Is.InRange(Math.Min(r.TopStart, r.TopFoot) - 0.01, Math.Max(r.TopStart, r.TopFoot) + 0.01));
                Assert.Greater(r.HalfWidth, 2.0);
            }
        }

        [Test]
        public void NoLotIsCutWhereARampLands()
        {
            foreach (PlanLot lot in Plan.Data.Lots)
            {
                foreach (RampPlan ramp in Plan.Data.Ramps)
                {
                    Assert.IsFalse(Interchanges.Touches(ramp, lot.Lot), $"lot {lot.Id} lies under the ramp at exit {ramp.ExitNode}");
                }
            }
        }

        [Test]
        public void TheSpawnIsNotOnAnExpressway()
        {
            Assert.IsTrue(Plan.TryFindSpawn(out double sx, out double sz));
            var p = new Vec2(sx, sz);
            foreach (EdgeGeometry g in Plan.Geometry.Edges)
            {
                if (g == null || g.Edge.Class != RoadClass.Highway) continue;
                double d = PlanMath.DistanceToSegment(p, g.A, g.B, out _);
                Assert.Greater(d, g.HalfWidth + 2.0, $"the spawn is under or on expressway edge {g.Edge.Id}");
            }
        }

        // ---- parking, malls, plazas ----

        [Test]
        public void ParkingTakesAFewPerCentOfTheLotsAndNeverTwiceInABlock()
        {
            int lots = 0, garages = 0, open = 0;
            var garageBlocks = new HashSet<int>();
            var openBlocks = new HashSet<int>();
            foreach (PlanLot lot in Plan.Data.Lots)
            {
                if (lot.Use == LotUse.Mall) continue;
                lots++;
                if (lot.Use == LotUse.ParkingGarage)
                {
                    garages++;
                    Assert.IsTrue(garageBlocks.Add(lot.BlockIndex), $"block {lot.BlockIndex} has two parking garages");
                }
                else if (lot.Use == LotUse.ParkingLot)
                {
                    open++;
                    Assert.IsTrue(openBlocks.Add(lot.BlockIndex), $"block {lot.BlockIndex} has two parking lots");
                }
            }
            double share = (garages + open) / (double)lots;
            Assert.Greater(garages, 5, "no parking garages");
            Assert.Greater(open, 5, "no parking lots");
            Assert.That(share, Is.InRange(0.005, 0.08), $"parking is {share * 100.0:F1}% of the lots: a city is a few per cent");
        }

        [Test]
        public void ParkingLotsStandAsBuildingsOfTheirOwnKind()
        {
            int garages = 0, open = 0;
            foreach (BuildingSite b in Plan.Buildings)
            {
                if (b.IsParkingGarage)
                {
                    garages++;
                    Assert.IsFalse(b.OnRoof || b.Wing || b.Shell, $"garage #{b.Id} is a wing or stacked volume");
                    Assert.GreaterOrEqual(b.Params.Height, Amenities.MinGarageLevels * Amenities.GarageFloorHeight - 0.01);
                    Assert.LessOrEqual(b.Params.Height, Amenities.MaxGarageLevels * Amenities.GarageFloorHeight + 0.01);
                }
                else if (b.IsOpenGround)
                {
                    open++;
                    Assert.Less(b.Params.Height, 1.0, $"car park #{b.Id} has a height");
                    Assert.IsFalse(b.OnRoof || b.Wing || b.Shell);
                }
            }
            Assert.Greater(garages, 5);
            Assert.Greater(open, 5);
        }

        [Test]
        public void MallsAreVeryRareAndAsFarApartAsRealOnes()
        {
            var malls = new List<BuildingSite>();
            foreach (BuildingSite b in Plan.Buildings)
            {
                if (b.IsMall) malls.Add(b);
            }
            Assert.That(malls.Count, Is.InRange(1, Amenities.MaxMalls), $"{malls.Count} malls");
            Assert.AreEqual(malls.Count, Plan.Data.MallBlocks.Count);
            for (int i = 0; i < malls.Count; i++)
            {
                BuildingSite m = malls[i];
                Assert.GreaterOrEqual(m.Params.Height, 2.0 * Amenities.MallLevelHeight - 0.01, "a mall of one level");
                Assert.GreaterOrEqual(m.Params.Width, Amenities.MallMinWidth * 0.9, $"mall #{m.Id} is only {m.Params.Width:F0} m wide");
                Assert.GreaterOrEqual(m.Params.Depth, Amenities.MallMinDepth * 0.9, $"mall #{m.Id} is only {m.Params.Depth:F0} m deep");
                Assert.IsFalse(m.OnRoof || m.Wing || m.Shell);
                for (int j = i + 1; j < malls.Count; j++)
                {
                    double dx = m.CentreX - malls[j].CentreX, dz = m.CentreZ - malls[j].CentreZ;
                    double d = Math.Sqrt(dx * dx + dz * dz);
                    Assert.GreaterOrEqual(d, Amenities.MallSeparation * 0.95, $"malls #{m.Id} and #{malls[j].Id} are {d / 1000.0:F1} km apart");
                }
            }
        }

        [Test]
        public void PlazasAreOpenBlocksWithNothingBuiltOnThem()
        {
            CityPlanData data = Plan.Data;
            Assert.GreaterOrEqual(data.PlazaBlocks.Count, 10, "too few plazas");
            var plazaIndex = new HashSet<int>(data.PlazaBlocks);
            foreach (PlanLot lot in data.Lots)
            {
                Assert.IsFalse(plazaIndex.Contains(lot.BlockIndex), $"lot {lot.Id} was cut from plaza block {lot.BlockIndex}");
            }
            foreach (int index in data.PlazaBlocks)
            {
                PlanBlock block = data.Blocks[index];
                Assert.IsTrue(block.Plaza);
                Assert.IsNotNull(block.Inner, "a plaza with no kerb ring");
                Assert.That(block.Area, Is.InRange(Amenities.PlazaMinArea * 0.99, Amenities.PlazaMaxArea * 16.0 * 1.01));
                double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
                foreach (Vec2 v in block.Inner)
                {
                    minX = Math.Min(minX, v.X); maxX = Math.Max(maxX, v.X);
                    minZ = Math.Min(minZ, v.Y); maxZ = Math.Max(maxZ, v.Y);
                }
                foreach (BuildingSite b in Plan.Buildings)
                {
                    if (b.CentreX < minX || b.CentreX > maxX || b.CentreZ < minZ || b.CentreZ > maxZ) continue;
                    Assert.IsFalse(PlanMath.PointInPolygon(new Vec2(b.CentreX, b.CentreZ), block.Inner), $"building #{b.Id} stands in plaza block {index}");
                }
            }
        }

        // ---- the meshes ----

        [Test]
        public void TheDeckChunkHasACarriagewayInTheAirWithParapetsAndAPier()
        {
            EdgeGeometry g = AHighDeckSpan();
            Vec2 mid = (g.A + g.B) * 0.5;
            int cx = PlanWorld.ChunkOf(mid.X), cy = PlanWorld.ChunkOf(mid.Y);
            MeshBuffer[] near = Surface(cx, cy, false);
            MeshBuffer[] far = Surface(cx, cy, true);
            float ground = (float)Plan.GroundHeight(mid.X, mid.Y);
            float deck = (float)g.DeckTopAt(Vec2.Dot(mid - g.A, g.Dir));
            float highest = float.MinValue;
            foreach (Vector3 v in near[0].Vertices) highest = Mathf.Max(highest, v.y);
            Assert.AreEqual(deck, highest, 0.6f, "the chunk's road surface does not reach the deck's height");
            Assert.Greater(deck - ground, Plan.Data.Config.DeckClearance - 0.5, "picked a span that is not up in the air");
            Assert.Greater(near[5].Triangles.Count, 0, "no parapet along the deck");
            Assert.AreEqual(0, far[5].Triangles.Count, "a far chunk builds parapets");
            // The slab's underside and a pier: pavement faces pointing down, and a structure between the ground and the deck.
            int down = 0;
            foreach (Vector3 n in near[1].Normals) if (n.y < -0.9f) down++;
            Assert.Greater(down, 0, "the deck has no underside");
            foreach (MeshBuffer b in near) AssertWound(b, $"deck chunk ({cx},{cy})");
            foreach (MeshBuffer b in far) AssertWound(b, $"far deck chunk ({cx},{cy})");
            int nearTriangles = 0, farTriangles = 0;
            foreach (MeshBuffer b in near) nearTriangles += b.Triangles.Count;
            foreach (MeshBuffer b in far) farTriangles += b.Triangles.Count;
            Assert.Less(farTriangles, nearTriangles, "the far deck is not lighter");
        }

        [Test]
        public void TheRaptureLeftCarsOnTheExpressway()
        {
            // Along the expressways there are jams: somewhere in the first dozen high spans a chunk has a car (its tyres carry the rubber look).
            int spans = 0;
            foreach (EdgeGeometry g in Plan.Geometry.Edges)
            {
                if (g == null || g.Edge.Class != RoadClass.Highway || !g.Elevated || g.Length < 40.0) continue;
                if (Math.Min(g.DeckA, g.DeckB) - g.MaxGround < 5.0) continue;
                if (spans++ > 40) break;
                for (double t = 12.0; t < g.Length - 12.0; t += 24.0)
                {
                    Vec2 p = g.A + g.Dir * t;
                    MeshBuffer[] near = Surface(PlanWorld.ChunkOf(p.X), PlanWorld.ChunkOf(p.Y), false);
                    if (HasLook(near[2], CityProps.Rubber)) return;
                }
            }
            Assert.Fail("no abandoned car on any of the first expressway spans");
        }

        [Test]
        public void AnExitHasItsRampAndItsSign()
        {
            Assert.Greater(Plan.Geometry.Ramps.Length, 0);
            RampGeometry r = null;
            foreach (RampGeometry candidate in Plan.Geometry.Ramps)
            {
                if (candidate.Plan.Leaving) { r = candidate; break; }
            }
            Assert.IsNotNull(r, "no off-ramp");
            // The chunk of the ramp's start: the slab leaving the deck, and the gantry over the deck (a green panel).
            Vec2 s = r.Plan.S;
            MeshBuffer[] near = Surface(PlanWorld.ChunkOf(s.X), PlanWorld.ChunkOf(s.Y), false);
            Assert.IsTrue(HasLook(near[2], CityProps.SignGreen), "no exit sign over the deck");
            // Halfway down the ramp's slope: road surface between the deck's height and the ground.
            Vec2 half = (r.Mid + r.Foot) * 0.5;
            MeshBuffer[] slope = Surface(PlanWorld.ChunkOf(half.X), PlanWorld.ChunkOf(half.Y), false);
            float ground = (float)Plan.GroundHeight(half.X, half.Y);
            float expected = (float)r.TopAt(r.LengthA + r.LengthB * 0.5);
            bool found = false;
            foreach (Vector3 v in slope[0].Vertices)
            {
                if (Mathf.Abs(v.y - expected) < 1.5f && v.y > ground + 0.5f) { found = true; break; }
            }
            Assert.IsTrue(found || expected - ground < 0.6f, "the ramp's slope is not in the chunk's road surface");
            foreach (MeshBuffer b in near) AssertWound(b, "exit chunk");
            foreach (MeshBuffer b in slope) AssertWound(b, "ramp chunk");
        }

        [Test]
        public void APlazaIsLaidInSettsWithItsBenches()
        {
            Assert.Greater(Plan.Data.PlazaBlocks.Count, 0);
            bool setts = false, bench = false;
            foreach (int index in Plan.Data.PlazaBlocks)
            {
                PlanBlock block = Plan.Data.Blocks[index];
                Vec2 c = PlanMath.Centroid(block.Inner);
                MeshBuffer[] near = Surface(PlanWorld.ChunkOf(c.X), PlanWorld.ChunkOf(c.Y), false);
                foreach (Color32 look in near[1].Colors)
                {
                    SurfaceLook.Unpack(look, out _, out int slice, out _);
                    if (slice == SurfaceLook.Setts) { setts = true; break; }
                }
                bench |= HasLook(near[2], CityProps.Wood);
                if (setts && bench) return;
            }
            Assert.IsTrue(setts, "no plaza is paved in setts");
            Assert.IsTrue(bench, "no plaza has a bench");
        }

        [Test]
        public void AGarageACarParkAndAMallBuildNearAndFar()
        {
            BuildingSite garage = default, open = default, mall = default;
            bool hasGarage = false, hasOpen = false, hasMall = false;
            foreach (BuildingSite b in Plan.Buildings)
            {
                if (!hasGarage && b.IsParkingGarage) { garage = b; hasGarage = true; }
                if (!hasOpen && b.IsOpenGround) { open = b; hasOpen = true; }
                if (!hasMall && b.IsMall) { mall = b; hasMall = true; }
            }
            Assert.IsTrue(hasGarage, "no parking garage");
            Assert.IsTrue(hasOpen, "no car park");
            Assert.IsTrue(hasMall, "no mall");
            foreach ((string name, BuildingSite site) in new[] { ("garage", garage), ("car park", open), ("mall", mall) })
            {
                MeshBuffer[] near = Site(site, false);
                MeshBuffer[] far = Site(site, true);
                int nearCount = 0, farCount = 0;
                foreach (MeshBuffer b in near) { nearCount += b.Triangles.Count; AssertWound(b, name); }
                foreach (MeshBuffer b in far) { farCount += b.Triangles.Count; AssertWound(b, name + " (far)"); }
                Assert.Greater(nearCount, 50, $"the {name} is empty");
                Assert.Less(farCount, nearCount, $"the far {name} is not lighter");
                foreach (MeshBuffer b in near)
                {
                    foreach (Vector3 v in b.Vertices)
                    {
                        Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z), $"a NaN vertex in the {name}");
                    }
                }
            }
            // The mall has an inside: vertices well within its walls, between its floor and its roof (the stores, the galleries, the food court).
            MeshBuffer[] mallNear = Site(mall, false);
            int inside = 0;
            float halfW = mall.Params.Width * 0.5f - 3.0f, halfD = mall.Params.Depth * 0.5f - 3.0f;
            foreach (Vector3 v in mallNear[0].Vertices)
            {
                if (Mathf.Abs(v.x) < halfW && Mathf.Abs(v.z) < halfD && v.y > mall.BaseHeight + 0.5f && v.y < mall.Top - 1.0f) inside++;
            }
            Assert.Greater(inside, 400, "the mall has nothing inside it");
            // The garage's decks: structure at several distinct heights between the floor and the roof.
            MeshBuffer[] garageNear = Site(garage, false);
            var heights = new HashSet<int>();
            foreach (Vector3 v in garageNear[1].Vertices)
            {
                if (v.y > garage.BaseHeight + 1.0f && v.y < garage.Top - 0.5f) heights.Add(Mathf.RoundToInt(v.y));
            }
            Assert.GreaterOrEqual(heights.Count, 3, "the garage has no decks");
        }

        [Test]
        public void TheChunksAroundAnExitAndAMallAreWoundAndFinite()
        {
            var seeds = new List<Vec2>();
            if (Plan.Geometry.Ramps.Length > 0) seeds.Add(Plan.Geometry.Ramps[0].Foot);
            foreach (BuildingSite b in Plan.Buildings)
            {
                if (b.IsMall) { seeds.Add(new Vec2(b.CentreX, b.CentreZ)); break; }
            }
            foreach (Vec2 p in seeds)
            {
                int cx = PlanWorld.ChunkOf(p.X), cy = PlanWorld.ChunkOf(p.Y);
                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        foreach (MeshBuffer b in Chunk(cx + dx, cy + dy))
                        {
                            AssertWound(b, $"chunk ({cx + dx},{cy + dy})");
                            foreach (Vector3 v in b.Vertices)
                            {
                                Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.y), $"a bad vertex in chunk ({cx + dx},{cy + dy})");
                            }
                        }
                    }
                }
            }
        }

        // ---- helpers ----

        private static int HighwayEdges(RoadNode node)
        {
            int n = 0;
            foreach (RoadEdge e in node.Edges)
            {
                if (e.Alive && e.Class == RoadClass.Highway) n++;
            }
            return n;
        }

        private static EdgeGeometry AHighDeckSpan()
        {
            EdgeGeometry best = null;
            double bestRise = 0.0;
            foreach (EdgeGeometry g in Plan.Geometry.Edges)
            {
                if (g == null || g.Edge.Class != RoadClass.Highway || !g.Elevated || g.Length < 60.0) continue;
                Vec2 mid = (g.A + g.B) * 0.5;
                double rise = g.DeckTopAt(g.Length * 0.5) - Plan.GroundHeight(mid.X, mid.Y);
                if (rise > bestRise) { bestRise = rise; best = g; }
            }
            Assert.IsNotNull(best, "no elevated expressway span");
            return best;
        }

        /// <summary>One chunk's surface only (CitySurfaceBuilder): ground, pavement, structures (the props), markings, lane dividers, guard rail.</summary>
        private static MeshBuffer[] Surface(int cx, int cy, bool far)
        {
            var buffers = new MeshBuffer[6];
            for (int i = 0; i < buffers.Length; i++) buffers[i] = new MeshBuffer();
            var origin = new Vector3((float)(cx * PlanWorld.ChunkSize), 0.0f, (float)(cy * PlanWorld.ChunkSize));
            CitySurfaceBuilder.Build(World, cx, cy, origin, buffers[0], buffers[1], buffers[2], far ? null : buffers[3], far ? null : buffers[4], far ? null : buffers[5], far);
            // Chunk-local to world, so the asserts can read heights off the plan.
            foreach (MeshBuffer b in buffers)
            {
                for (int i = 0; i < b.Vertices.Count; i++) b.Vertices[i] += origin;
            }
            return buffers;
        }

        /// <summary>One building alone, in its own frame about its centre (x, z local; y absolute): structures, concrete, lit glass, dark glass.</summary>
        private static MeshBuffer[] Site(BuildingSite site, bool far)
        {
            var buffers = new MeshBuffer[4];
            for (int i = 0; i < buffers.Length; i++) buffers[i] = new MeshBuffer();
            var origin = new Vector3((float)site.CentreX, 0.0f, (float)site.CentreZ);
            CityMeshBuilder.BuildSite(World, site, origin, buffers[0], buffers[1], buffers[2], buffers[3], null, null, null, null, far);
            // Back into the builder's unturned frame (points and normals both), so the asserts can use the site's width and depth.
            float cos = (float)site.AxisX, sin = (float)site.AxisZ;
            foreach (MeshBuffer b in buffers)
            {
                for (int i = 0; i < b.Vertices.Count; i++)
                {
                    Vector3 v = b.Vertices[i];
                    site.WorldToLocal(v.x + site.CentreX, v.z + site.CentreZ, out double lx, out double lz);
                    b.Vertices[i] = new Vector3((float)lx, v.y, (float)lz);
                    Vector3 n = b.Normals[i];
                    b.Normals[i] = new Vector3(n.x * cos + n.z * sin, n.y, -n.x * sin + n.z * cos);
                }
            }
            return buffers;
        }

        private static MeshBuffer[] Chunk(int cx, int cy)
        {
            var buffers = new MeshBuffer[5];
            for (int i = 0; i < buffers.Length; i++) buffers[i] = new MeshBuffer();
            var origin = new Vector3(cx * CityLayout.ChunkSize, 0.0f, cy * CityLayout.ChunkSize);
            CityMeshBuilder.BuildChunk(World, cx, cy, origin, buffers[0], buffers[1], buffers[2], buffers[4], buffers[3]);
            return buffers;
        }

        private static bool HasLook(MeshBuffer buffer, Color32 look)
        {
            foreach (Color32 c in buffer.Colors)
            {
                if (c.r == look.r && c.g == look.g && c.b == look.b && c.a == look.a) return true;
            }
            return false;
        }

        private static void AssertWound(MeshBuffer buffer, string what)
        {
            for (int i = 0; i + 2 < buffer.Triangles.Count; i += 3)
            {
                Vector3 a = buffer.Vertices[buffer.Triangles[i]];
                Vector3 b = buffer.Vertices[buffer.Triangles[i + 1]];
                Vector3 c = buffer.Vertices[buffer.Triangles[i + 2]];
                Vector3 n = buffer.Normals[buffer.Triangles[i]];
                Vector3 face = Vector3.Cross(b - a, c - a);
                if (face.sqrMagnitude < 1e-12f) continue;
                Assert.GreaterOrEqual(Vector3.Dot(face, n), -1e-4f * face.magnitude, $"{what}: triangle {i / 3} is wound against its normal");
            }
        }
    }
}
