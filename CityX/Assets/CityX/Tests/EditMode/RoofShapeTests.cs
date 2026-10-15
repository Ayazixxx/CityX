using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Pitched roofs (World/RoofShape, View/CityMeshBuilder.Roofs.cs): only on low buildings and warehouses that stand free, walkable, closing the
    /// building from eave to ridge, with no stair up to them, no parapet and no cornice; and common enough out of the centre to be seen.
    /// </summary>
    public sealed class RoofShapeTests
    {
        private const int Seed = 61537940;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        private static List<BuildingSite> _sites;
        private static List<BuildingSite> Sites => _sites ?? (_sites = new List<BuildingSite>(TestSites.NearSpawn(World, 40)));

        private static void Build(BuildingSite site, bool far, MeshBuffer structures, out Vector3 origin)
        {
            origin = new Vector3(site.OwnerChunkX * CityLayout.ChunkSize, 0, site.OwnerChunkY * CityLayout.ChunkSize);
            CityMeshBuilder.BuildSite(World, site, origin, structures, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), new MeshBuffer(),
                null, null, new MeshBuffer(), far);
        }

        [Test]
        public void OnlyLowBuildingsAndWarehousesThatStandFreeArePitched()
        {
            int pitched = 0, sheds = 0, terraced = 0;
            var kinds = new Dictionary<RoofKind, int>();
            foreach (BuildingSite site in Sites)
            {
                RoofShape roof = RoofShape.For(site);
                if (!roof.Pitched)
                {
                    continue;
                }
                pitched++;
                kinds[roof.Kind] = kinds.TryGetValue(roof.Kind, out int n) ? n + 1 : 1;
                string what = $"building {site.Id} ({site.Zone} {site.Params.Mass} {site.Params.Width:F0}x{site.Params.Depth:F0}x{site.Params.Height:F0})";
                Assert.IsFalse(site.Params.IsSpecial || site.Shell || site.OnRoof || site.Wing || site.Sides >= 3, $"{what}: not an ordinary block");
                Assert.IsFalse(site.HasRoofObstacle || site.RoofSealed, $"{what}: something stands on this roof");
                // Neighbours only against the gable's ends (the walls the ridge runs into), with the roof flush there: a row house.
                bool againstEnds = false, againstSides = false;
                foreach (WallContact contact in site.Contacts ?? new WallContact[0])
                {
                    bool end = contact.Wall == WindowGrid.Wall.West || contact.Wall == WindowGrid.Wall.East;
                    againstEnds |= end;
                    againstSides |= !end;
                }
                Assert.IsFalse(againstEnds && againstSides, $"{what}: something stands against it both ways round, under its eaves");
                if (againstEnds || againstSides)
                {
                    terraced++;
                    Assert.AreEqual(RoofKind.Gable, roof.Kind, $"{what}: a terraced house's roof is a gable");
                    Assert.AreEqual(againstEnds, roof.AlongX, $"{what}: the ridge does not run into the walls its neighbours stand against");
                    Assert.AreEqual(0.0f, roof.Verge, $"{what}: a verge over the neighbour's roof");
                }
                if (site.Params.Mass == BuildingMass.Warehouse)
                {
                    sheds++;
                    Assert.AreEqual(RoofKind.Gable, roof.Kind, what);
                    Assert.That(roof.Pitch, Is.InRange(5.0f, 10.5f), $"{what}: a shed's roof is a low one");
                    continue;
                }
                Assert.AreEqual(BuildingMass.LowRise, site.Params.Mass, $"{what}: a pitched roof on a tall building");
                Assert.That(roof.AlongX ? site.Params.Depth : site.Params.Width, Is.LessThanOrEqualTo(RoofShape.MaxHouseShort), $"{what}: too wide across the ridge");
                Assert.That(roof.RiseOver(site.Params.Width, site.Params.Depth), Is.LessThanOrEqualTo(RoofShape.MaxRise + 0.01f), what);
            }
            Debug.Log($"[RoofShape] {pitched} pitched roofs of {Sites.Count} buildings within 40 chunks ({sheds} sheds, {terraced} terraced): " +
                string.Join(", ", kinds));
            Assert.That(pitched, Is.GreaterThan(40), "too few pitched roofs to be seen");
            Assert.That(kinds.Count, Is.EqualTo(3), "not every kind of roof turns up");
        }

        [Test]
        public void EveryPitchedRoofCanBeWalkedUp()
        {
            foreach (BuildingSite site in Sites)
            {
                RoofShape roof = RoofShape.For(site);
                if (roof.Pitched)
                {
                    Assert.That(roof.Pitch, Is.InRange(5.0f, RoofShape.MaxPitch), $"building {site.Id}: a {roof.Kind} at {roof.Pitch:F1} degrees");
                    Assert.That(roof.Pitch, Is.LessThan(44.0f), "steeper than the controller's slope limit");
                }
            }
        }

        [Test]
        public void APitchedRoofHasNoStairUpToItNoParapetAndNoCornice()
        {
            int checkedRoofs = 0;
            foreach (BuildingSite site in Sites)
            {
                RoofShape roof = RoofShape.For(site);
                if (!roof.Pitched)
                {
                    continue;
                }
                Assert.IsTrue(BuildingStairs.PlanFor(site).NoRoofStair, $"building {site.Id}: a stair up into the attic");
                WindowPlan plan = WindowPlan.For(World, site);
                Assert.IsFalse(FacadeOrnament.For(site, plan.Style, BuildingLook.For(site, plan.Style).Wall).HasCornice,
                    $"building {site.Id}: a cornice with no parapet to cap");
                Assert.IsFalse(ApartmentFront.For(site, plan).Any, $"building {site.Id}: a house with balconies on every floor");
                if (++checkedRoofs >= 120)
                {
                    break;
                }
            }
            Assert.That(checkedRoofs, Is.GreaterThan(20));
        }

        [Test]
        public void TheRoofCoversTheWholeBuildingNearAndFar()
        {
            // Looked down on, every point of the footprint is under a slope of the roof (no gap at a hip, a ridge or an end), and the roof rises as
            // high as its pitch says.
            int built = 0;
            var seen = new HashSet<RoofKind>();
            foreach (BuildingSite site in Sites)
            {
                RoofShape roof = RoofShape.For(site);
                if (!roof.Pitched || (seen.Contains(roof.Kind) && built >= 6))
                {
                    continue;
                }
                seen.Add(roof.Kind);
                foreach (bool far in new[] { false, true })
                {
                    var structures = new MeshBuffer();
                    Build(site, far, structures, out Vector3 origin);
                    float roofY = site.Top;
                    float rise = roof.RiseOver(site.Params.Width, site.Params.Depth);
                    float hw = site.Params.Width * 0.5f, hd = site.Params.Depth * 0.5f;
                    float highest = float.NegativeInfinity;
                    for (int i = 0; i < 5; i++)
                    {
                        for (int j = 0; j < 5; j++)
                        {
                            float lx = Mathf.Lerp(-hw + 0.3f, hw - 0.3f, i / 4.0f);
                            float lz = Mathf.Lerp(-hd + 0.3f, hd - 0.3f, j / 4.0f);
                            site.LocalToWorld(lx, lz, out double wx, out double wz);
                            float top = TopAt(structures, origin, (float)wx, (float)wz, out Vector3 normal);
                            Assert.That(top, Is.GreaterThan(roofY + 0.05f),
                                $"building {site.Id} ({roof.Kind}, {(far ? "far" : "near")}): nothing over ({lx:F1}, {lz:F1}) higher than the walls' top");
                            Assert.That(normal.y, Is.GreaterThan(Mathf.Cos(roof.Pitch * Mathf.Deg2Rad) - 0.02f),
                                $"building {site.Id} ({roof.Kind}): the surface over ({lx:F1}, {lz:F1}) is not the roof's slope");
                            highest = Mathf.Max(highest, top);
                        }
                    }
                    Assert.That(highest, Is.GreaterThan(roofY + rise - 0.3f),
                        $"building {site.Id} ({roof.Kind}): the roof never reaches the {rise:F2} m its pitch rises to");
                }
                built++;
            }
            Assert.That(seen.Count, Is.EqualTo(3), "not every kind of roof was checked");
        }

        /// <summary>The height of the highest upward-facing triangle over a point, and its normal (the roof seen from above).</summary>
        private static float TopAt(MeshBuffer buffer, Vector3 origin, float x, float z, out Vector3 normal)
        {
            float best = float.NegativeInfinity;
            normal = Vector3.zero;
            List<int> tris = buffer.Triangles;
            for (int t = 0; t + 2 < tris.Count; t += 3)
            {
                Vector3 a = buffer.Vertices[tris[t]] + origin, b = buffer.Vertices[tris[t + 1]] + origin, c = buffer.Vertices[tris[t + 2]] + origin;
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.y <= 1e-6f)
                {
                    continue;   // only faces looking up
                }
                // Barycentric in plan.
                float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(d) < 1e-9f)
                {
                    continue;
                }
                float wa = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
                float wb = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
                float wc = 1.0f - wa - wb;
                if (wa < -1e-4f || wb < -1e-4f || wc < -1e-4f)
                {
                    continue;
                }
                float y = wa * a.y + wb * b.y + wc * c.y;
                if (y > best)
                {
                    best = y;
                    normal = n.normalized;
                }
            }
            return best;
        }
    }
}
