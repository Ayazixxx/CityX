using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The tops of the tall towers (World/TowerTop, View/Buildings/CityMeshBuilder.TowerTops.cs): only a tower's topmost box volume with its flat parapet roof
    /// gets one, about as often as real ones do (a fifth of the towers, more than half of the supertalls), standing on the roof inside its edges
    /// and off the stair's hatch, drawn to their height near and far, and as a stub in the skyline tier.
    /// </summary>
    public sealed class TowerTopTests
    {
        private const int Seed = 81565117;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        private static List<BuildingSite> _sites;
        private static List<BuildingSite> Sites => _sites ?? (_sites = new List<BuildingSite>(TestSites.NearSpawn(World, 80)));

        private static void Build(BuildingSite site, bool far, MeshBuffer structures, out Vector3 origin)
        {
            origin = new Vector3(site.OwnerChunkX * CityLayout.ChunkSize, 0, site.OwnerChunkY * CityLayout.ChunkSize);
            CityMeshBuilder.BuildSite(World, site, origin, structures, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), new MeshBuffer(),
                null, null, new MeshBuffer(), far);
        }

        private static Rect Foot(in TowerTop top)
        {
            return Rect.MinMaxRect(top.X - top.Size * 0.5f, top.Z - top.Size * 0.5f, top.X + top.Size * 0.5f, top.Z + top.Size * 0.5f);
        }

        /// <summary>The hatch a top must keep off, grown by the margin, as TowerTop reads it; zero when the roof has no stair up to it.</summary>
        private static Rect Hatch(BuildingSite site)
        {
            BuildingStairs.Layout stairs = BuildingStairs.PlanFor(site);
            StairPlan roofStair = stairs.NoRoofStair || stairs.FloorCount == 0 ? null : stairs.Floors[stairs.FloorCount - 1];
            if (roofStair == null)
            {
                return Rect.zero;
            }
            Rect h = roofStair.Hole;
            return Rect.MinMaxRect(h.xMin - TowerTop.HatchMargin, h.yMin - TowerTop.HatchMargin, h.xMax + TowerTop.HatchMargin, h.yMax + TowerTop.HatchMargin);
        }

        [Test]
        public void OnlyFreeStandingTowersWithFlatRoofsGetATopAboutAsOftenAsRealOnes()
        {
            int towers = 0, toppedTowers = 0, supertalls = 0, toppedSupertalls = 0;
            var kinds = new Dictionary<TowerTopKind, int>();
            foreach (BuildingSite site in Sites)
            {
                bool tower = site.Params.Mass == BuildingMass.Tower, supertall = site.Params.Mass == BuildingMass.Supertall;
                TowerTop top = TowerTop.For(site);
                // Counted per building: its topmost volume (the one with nothing standing on it), which is the only one that can carry a top.
                bool topmost = !site.HasRoofObstacle && !site.RoofSealed;
                if (tower && topmost) towers++;
                if (supertall && topmost) supertalls++;
                if (!top.Exists)
                {
                    continue;
                }
                string what = $"building {site.Id} ({site.Zone} {site.Params.Mass} {site.Params.Width:F0}x{site.Params.Depth:F0}x{site.Params.Height:F0}): a {top.Kind}";
                Assert.IsTrue(tower || supertall, $"{what} on something that is not a tower");
                Assert.IsTrue(RoofShape.For(site).HasParapet, $"{what} on a roof with no parapet");
                Assert.IsFalse(site.Params.IsSpecial || site.Wing || site.HasRoofObstacle || site.RoofSealed,
                    $"{what} on a volume that is not a tower's topmost one");
                float h = site.Reach;
                Assert.That(h, Is.GreaterThanOrEqualTo(TowerTop.LeastReach), $"{what} on a tower reaching only {h:F0} m");
                Assert.That(top.Height, Is.InRange(0.04f * h, 0.27f * h), $"{what} {top.Height:F1} m tall on a tower reaching {h:F0} m");
                if (top.Kind == TowerTopKind.Mast)
                {
                    Assert.That(top.Height, Is.GreaterThanOrEqualTo(TowerTop.LeastMast), what);
                }
                kinds[top.Kind] = kinds.TryGetValue(top.Kind, out int n) ? n + 1 : 1;
                if (tower) toppedTowers++;
                if (supertall) toppedSupertalls++;
            }
            Debug.Log($"[TowerTop] within 80 chunks: {toppedTowers} of {towers} towers and {toppedSupertalls} of {supertalls} supertalls topped: " +
                string.Join(", ", kinds));
            Assert.That(towers, Is.GreaterThan(30), "too few towers to count");
            double share = toppedTowers / (double)towers;
            Assert.That(share, Is.InRange(0.1, 0.36), $"{share:P0} of the towers topped: real cities top about a fifth of theirs");
            if (supertalls >= 12)
            {
                // A supertall is rare (half a dozen within 80 chunks at the classic scale: too few to judge a share by), so only a real sample is held to it.
                Assert.That(toppedSupertalls / (double)supertalls, Is.InRange(0.3, 0.85), "the supertalls: better than an even chance");
            }
            Assert.That(kinds.Count, Is.GreaterThanOrEqualTo(3), "too few kinds of top turn up");
        }

        [Test]
        public void ATopStandsOnTheRoofInsideItsEdgesAndOffTheStairsHatch()
        {
            int checkedTops = 0;
            foreach (BuildingSite site in Sites)
            {
                TowerTop top = TowerTop.For(site);
                if (!top.Exists)
                {
                    continue;
                }
                checkedTops++;
                string what = $"building {site.Id}: a {top.Kind} {top.Size:F1} m across at ({top.X:F1}, {top.Z:F1}) on a {site.Params.Width:F0}x{site.Params.Depth:F0} roof";
                if (top.Kind == TowerTopKind.Fins)
                {
                    Assert.AreEqual(0.0f, top.Size, what);
                    continue;
                }
                Assert.That(top.Size, Is.GreaterThanOrEqualTo(TowerTop.LeastBase - 0.01f), what);
                // The roof's edges: a box's walls, or the square inside a round tower's parapet.
                float hw = site.Sides >= 3 ? RoundFootprint.Of(site).InnerHalf : site.Params.Width * 0.5f;
                float hd = site.Sides >= 3 ? hw : site.Params.Depth * 0.5f;
                Assert.That(Mathf.Abs(top.X) + top.Size * 0.5f, Is.LessThanOrEqualTo(hw - TowerTop.EdgeMargin + 0.01f), $"{what}: over the edge");
                Assert.That(Mathf.Abs(top.Z) + top.Size * 0.5f, Is.LessThanOrEqualTo(hd - TowerTop.EdgeMargin + 0.01f), $"{what}: over the edge");
                Rect hatch = Hatch(site);
                if (hatch.width > 0.0f)
                {
                    Assert.IsFalse(hatch.Overlaps(Foot(top)), $"{what}: on the stair's hatch {hatch}");
                }
            }
            Assert.That(checkedTops, Is.GreaterThan(5));
        }

        [Test]
        public void TheMeshCarriesTheTopToItsHeightNearFarAndInTheSkyline()
        {
            var seen = new HashSet<TowerTopKind>();
            int built = 0;
            foreach (BuildingSite site in Sites)
            {
                TowerTop top = TowerTop.For(site);
                if (!top.Exists || seen.Contains(top.Kind))
                {
                    continue;
                }
                seen.Add(top.Kind);
                float hw = site.Params.Width * 0.5f + 0.01f, hd = site.Params.Depth * 0.5f + 0.01f;
                string what = $"building {site.Id} ({site.Params.Mass} {site.Params.Height:F0} m): a {top.Kind} {top.Height:F1} m tall";
                foreach (bool far in new[] { false, true })
                {
                    var structures = new MeshBuffer();
                    Build(site, far, structures, out Vector3 origin);
                    float highest = HighestInside(structures, origin, site, hw, hd);
                    Assert.That(highest, Is.EqualTo(site.Top + top.Height).Within(0.8f), $"{what}, {(far ? "far" : "near")}: the mesh reaches {highest - site.Top:F1} m over the roof");
                }
                // The skyline tier: the group's boxes, built as if the viewer were far away, carry a stub of it.
                Vector2Int group = SkylineStreamer.GroupOf(new Vector3((float)site.CentreX, 0.0f, (float)site.CentreZ));
                int n = SkylineStreamer.GroupChunks;
                var skyline = new MeshBuffer();
                var skylineOrigin = new Vector3(group.x * SkylineStreamer.GroupSize, 0.0f, group.y * SkylineStreamer.GroupSize);
                CityMeshBuilder.BuildSkyline(World, group.x * n, group.y * n, n, skylineOrigin, new MeshBuffer(), skyline, new Vector2Int(1000000, 1000000),
                    SkylineStreamer.ExcludeRadiusChunks);
                float skylineHighest = HighestInside(skyline, skylineOrigin, site, hw, hd);
                Assert.That(skylineHighest, Is.GreaterThan(site.Top + top.Height * 0.5f), $"{what}: the skyline box reaches only {skylineHighest - site.Top:F1} m over the roof");
                Assert.That(skylineHighest, Is.LessThanOrEqualTo(site.Top + top.Height + 0.01f), $"{what}: the skyline stub overshoots the top");
                built++;
            }
            Assert.That(built, Is.GreaterThanOrEqualTo(3), "too few kinds of top to check");
        }

        /// <summary>The highest vertex of <paramref name="buffer"/> (in chunk space from <paramref name="origin"/>) within the building's footprint.</summary>
        private static float HighestInside(MeshBuffer buffer, Vector3 origin, BuildingSite site, float hw, float hd)
        {
            float highest = float.NegativeInfinity;
            foreach (Vector3 v in buffer.Vertices)
            {
                Vector3 p = v + origin;
                double dx = p.x - site.CentreX, dz = p.z - site.CentreZ;
                double u = dx * site.AxisX + dz * site.AxisZ;
                double w = -dx * site.AxisZ + dz * site.AxisX;
                if (System.Math.Abs(u) <= hw && System.Math.Abs(w) <= hd)
                {
                    highest = Mathf.Max(highest, p.y);
                }
            }
            return highest;
        }
    }
}
