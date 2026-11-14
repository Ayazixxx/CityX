using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Pitched roofs (World/RoofShape, View/Buildings/CityMeshBuilder.Roofs.cs): only on low buildings and warehouses that stand free, walkable, closing the
    /// building from eave to ridge, with no stair up to them, no parapet and no cornice; and common enough out of the centre to be seen.
    /// </summary>
    public sealed class RoofShapeTests
    {
        private const int Seed = CitySeed.Default;

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
                    Assert.That(roof.Kind, Is.EqualTo(RoofKind.Gable).Or.EqualTo(RoofKind.Skillion).Or.EqualTo(RoofKind.Saltbox), what);
                    Assert.That(roof.Pitch, Is.InRange(3.5f, 12.5f), $"{what}: a shed's roof is a low one");
                    continue;
                }
                Assert.AreEqual(BuildingMass.LowRise, site.Params.Mass, $"{what}: a pitched roof on a tall building");
                Assert.That(roof.AlongX ? site.Params.Depth : site.Params.Width, Is.LessThanOrEqualTo(RoofShape.MaxHouseShort), $"{what}: too wide across the ridge");
                Assert.That(roof.RiseOver(site.Params.Width, site.Params.Depth), Is.LessThanOrEqualTo(RoofShape.MaxRise + 0.01f), what);
            }
            Debug.Log($"[RoofShape] {pitched} pitched roofs of {Sites.Count} buildings within 40 chunks ({sheds} sheds, {terraced} terraced): " +
                string.Join(", ", kinds));
            Assert.That(pitched, Is.GreaterThan(40), "too few pitched roofs to be seen");
            Assert.That(kinds.Count, Is.GreaterThanOrEqualTo(3), "not every kind of roof turns up");
            Assert.IsTrue(kinds.ContainsKey(RoofKind.Gable) && kinds.ContainsKey(RoofKind.Hip) && kinds.ContainsKey(RoofKind.Skillion), "no gable, hip or skillion");
        }

        [Test]
        public void TheNewKindsTurnUpAtTheirRealWorldShares()
        {
            // Phase 2b (owner, 2026-10-15: "research how often these roofs occur in real world environments and make the chance they occur
            // ingame similar"): saltboxes a few per cent of the pitched houses, skillions one in twelve in the homes and more downtown, flat roofs
            // with eaves a quarter to a third of the flat house roofs in the homes and a few mid-rise blocks downtown; never on a tower.
            int pitchedHouses = 0, saltboxHouses = 0, skillionHomes = 0, pitchedHomes = 0;
            int flatHomes = 0, eavesHomes = 0, midRiseDowntown = 0, eavesMidRise = 0, flatSheds = 0, eavesSheds = 0, pitchedSheds = 0, skillionSheds = 0, saltboxSheds = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 80))
            {
                RoofShape roof = RoofShape.For(site);
                BuildingMass mass = site.Params.Mass;
                string what = $"building {site.Id} ({site.Zone} {mass} {site.Params.Width:F0}x{site.Params.Depth:F0}x{site.Params.Height:F0})";
                if (mass == BuildingMass.Tower || mass == BuildingMass.Supertall || mass == BuildingMass.Slab)
                {
                    Assert.IsTrue(roof.HasParapet, $"{what}: a {roof.Kind} roof on a tall building");
                    continue;
                }
                bool homes = site.Zone == ZoneKind.Residential || site.Zone == ZoneKind.Outskirts;
                bool downtown = site.Zone == ZoneKind.Core || site.Zone == ZoneKind.Urban;
                if (mass == BuildingMass.Warehouse)
                {
                    if (roof.Pitched)
                    {
                        pitchedSheds++;
                        skillionSheds += roof.Kind == RoofKind.Skillion ? 1 : 0;
                        saltboxSheds += roof.Kind == RoofKind.Saltbox ? 1 : 0;
                    }
                    else
                    {
                        flatSheds++;
                        eavesSheds += roof.Kind == RoofKind.FlatEaves ? 1 : 0;
                    }
                    continue;
                }
                if (mass == BuildingMass.MidRise)
                {
                    Assert.IsFalse(roof.Pitched, $"{what}: a pitched roof on a mid-rise block");
                    if (downtown)
                    {
                        midRiseDowntown++;
                        eavesMidRise += roof.Kind == RoofKind.FlatEaves ? 1 : 0;
                    }
                    continue;
                }
                if (roof.Pitched)
                {
                    pitchedHouses++;
                    saltboxHouses += roof.Kind == RoofKind.Saltbox ? 1 : 0;
                    if (roof.Kind == RoofKind.Saltbox)
                    {
                        Assert.That(roof.Attic, Is.InRange(0.5f, RoofShape.MaxAttic + 0.01f), $"{what}: a saltbox's attic");
                        float across = roof.AlongX ? site.Params.Depth : site.Params.Width;
                        Assert.That(roof.SaltboxRidgeFromFront(across) / across, Is.InRange(RoofShape.MinSaltboxRidgeShare - 0.01f, 0.5f), $"{what}: the ridge is not forward");
                        Assert.That(roof.RiseOver(site.Params.Width, site.Params.Depth), Is.LessThanOrEqualTo(RoofShape.MaxRise + 0.02f), what);
                    }
                    if (homes)
                    {
                        pitchedHomes++;
                        skillionHomes += roof.Kind == RoofKind.Skillion ? 1 : 0;
                    }
                }
                else if (homes)
                {
                    flatHomes++;
                    eavesHomes += roof.Kind == RoofKind.FlatEaves ? 1 : 0;
                    if (roof.Kind == RoofKind.FlatEaves)
                    {
                        Assert.That(roof.Eave, Is.InRange(RoofShape.MinFlatEave, RoofShape.MaxFlatEave), $"{what}: the eaves");
                    }
                }
            }
            Debug.Log($"[RoofShape] within 80 chunks: {pitchedHouses} pitched houses ({saltboxHouses} saltboxes; in the homes {skillionHomes} skillions of {pitchedHomes}), " +
                $"{eavesHomes} of {flatHomes} flat house roofs in the homes with eaves, {eavesMidRise} of {midRiseDowntown} downtown mid-rises, " +
                $"sheds: {pitchedSheds} pitched ({skillionSheds} skillion, {saltboxSheds} saltbox), {eavesSheds} of {flatSheds} flat with eaves");
            Assert.That(pitchedHouses, Is.GreaterThan(100));
            Assert.That(saltboxHouses, Is.GreaterThanOrEqualTo(1), "no saltbox anywhere");
            Assert.That(saltboxHouses / (double)pitchedHouses, Is.LessThan(0.08), "too many saltboxes: they are a few per cent of real roofs");
            Assert.That(skillionHomes / (double)Mathf.Max(1, pitchedHomes), Is.InRange(0.03, 0.2), "the homes' shed roofs: one in twelve");
            Assert.That(flatHomes, Is.GreaterThan(20));
            Assert.That(eavesHomes / (double)flatHomes, Is.InRange(0.15, 0.45), "flat roofs with eaves in the homes: a quarter to a third");
            if (midRiseDowntown >= 60)
            {
                Assert.That(eavesMidRise / (double)midRiseDowntown, Is.InRange(0.02, 0.16), "a few downtown mid-rises get an overhang");
            }
            if (flatSheds >= 12)
            {
                Assert.That(eavesSheds / (double)flatSheds, Is.InRange(0.08, 0.45), "a quarter of the flat sheds get a gutter and overhang");
            }
            if (pitchedSheds >= 20)
            {
                Assert.That(skillionSheds + saltboxSheds, Is.GreaterThan(0), "no monopitch or lean-to shed");
            }
        }

        [Test]
        public void ASaltboxIsAStoreyTallerInFrontWithItsRidgeForwardAndItsLongSlopeBehind()
        {
            BuildingSite house = default;
            bool found = false;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 80))
            {
                if (RoofShape.For(site).Kind == RoofKind.Saltbox && site.Params.Mass != BuildingMass.Warehouse)
                {
                    house = site;
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, "no saltbox house within 80 chunks");
            RoofShape roof = RoofShape.For(house);
            float across = roof.AlongX ? house.Params.Depth : house.Params.Width;
            float half = across * 0.5f;
            float t = Mathf.Tan(roof.Pitch * Mathf.Deg2Rad);
            float back = roof.LowAtMin ? -half : half, front = -back;   // the long slope falls to the back wall
            float dir = Mathf.Sign(front - back);
            float roofY = house.Top;
            string what = $"building {house.Id} ({house.Params.Width:F0}x{house.Params.Depth:F0}x{house.Params.Height:F0}, pitch {roof.Pitch:F0}, attic {roof.Attic:F2})";
            foreach (bool far in new[] { false, true })
            {
                var structures = new MeshBuffer();
                Build(house, far, structures, out Vector3 origin);
                // Across the roof at the middle of its length, from just inside the back wall to just inside the front one.
                const int samples = 24;
                float highest = float.NegativeInfinity, highestAt = 0.0f;
                for (int i = 0; i <= samples; i++)
                {
                    float b = Mathf.Lerp(back + dir * 0.3f, front - dir * 0.3f, i / (float)samples);
                    float lx = roof.AlongX ? 0.0f : b, lz = roof.AlongX ? b : 0.0f;
                    house.LocalToWorld(lx, lz, out double wx, out double wz);
                    float top = TopAt(structures, origin, (float)wx, (float)wz, out Vector3 normal);
                    Assert.That(top, Is.GreaterThan(roofY + 0.05f), $"{what} {(far ? "far" : "near")}: nothing over b = {b:F1}");
                    if (top > highest)
                    {
                        highest = top;
                        highestAt = b;
                    }
                    if (i == 0)
                    {
                        Assert.That(top, Is.LessThan(roofY + 0.3f * t + RoofShape.Thickness / Mathf.Cos(roof.Pitch * Mathf.Deg2Rad) + 0.35f),
                            $"{what}: the long slope does not come down to the back wall's top");
                    }
                    if (i == samples)
                    {
                        Assert.That(top, Is.GreaterThan(roofY + roof.Attic - 0.05f), $"{what}: the front is not a storey taller");
                    }
                }
                float fromFront = Mathf.Abs(front - highestAt) / across;
                Assert.That(fromFront, Is.InRange(RoofShape.MinSaltboxRidgeShare - 0.06f, 0.5f), $"{what}: the ridge is {fromFront:P0} of the span from the front");
                // The slope's top is the roof's thickness over the ridge line, and near by the ridge cap stands 0.2 m over that.
                float ridge = roofY + roof.RiseOver(house.Params.Width, house.Params.Depth);
                Assert.That(highest, Is.InRange(ridge - 0.35f, ridge + 0.65f), $"{what}: the ridge's height ({highest - roofY:F2} m over the walls, {ridge - roofY:F2} expected)");
            }
            Assert.IsTrue(BuildingStairs.PlanFor(house).NoRoofStair, $"{what}: a stair up into the attic");
        }

        [Test]
        public void AFlatRoofWithEavesHasNoParapetAndStandsOutPastEveryWall()
        {
            BuildingSite house = default;
            bool found = false;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 80))
            {
                if (RoofShape.For(site).Kind == RoofKind.FlatEaves && site.Params.Mass == BuildingMass.LowRise)
                {
                    house = site;
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, "no low building with a flat roof with eaves within 80 chunks");
            RoofShape roof = RoofShape.For(house);
            string what = $"building {house.Id} ({house.Zone} {house.Params.Width:F0}x{house.Params.Depth:F0}x{house.Params.Height:F0}, eave {roof.Eave:F2})";
            Assert.IsFalse(roof.HasParapet, what);
            Assert.IsTrue(roof.IsFlat && !roof.Pitched, what);
            Assert.That(roof.Eave, Is.InRange(RoofShape.MinFlatEave, RoofShape.MaxFlatEave), what);
            WindowPlan plan = WindowPlan.For(World, house);
            Assert.IsFalse(FacadeOrnament.For(house, plan.Style, BuildingLook.For(house, plan.Style).Wall).HasCornice, $"{what}: a cornice with no parapet to cap");
            Assert.IsFalse(RoofBillboards.For(house).Exists, $"{what}: a billboard with no parapet to stand behind");
            BuildingStairs.Layout stairs = BuildingStairs.PlanFor(house);
            if (!stairs.SealedTop)
            {
                Assert.IsFalse(stairs.NoRoofStair, $"{what}: the deck is there to stand on, so the stair comes up to it");
            }
            var structures = new MeshBuffer();
            Build(house, false, structures, out Vector3 origin);
            float hw = house.Params.Width * 0.5f, hd = house.Params.Depth * 0.5f;
            float lip = house.Top + RoofShape.FlatEavesLip;
            foreach ((float lx, float lz) in new[] { (0.0f, -hd - 0.4f), (0.0f, hd + 0.4f), (-hw - 0.4f, 0.0f), (hw + 0.4f, 0.0f) })
            {
                house.LocalToWorld(lx, lz, out double wx, out double wz);
                float top = TopAt(structures, origin, (float)wx, (float)wz, out Vector3 normal);
                Assert.That(top, Is.EqualTo(lip).Within(0.03f), $"{what}: the eaves' top over ({lx:F1}, {lz:F1})");
                Assert.That(normal.y, Is.GreaterThan(0.99f), $"{what}: the eaves are not level");
            }
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
            Assert.That(seen.Count, Is.GreaterThanOrEqualTo(3), "not every kind of roof was checked");
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
