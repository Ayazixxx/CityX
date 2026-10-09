using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The small things that make an outside read as a real building: the parapet and plant on a roof, what hangs on a facade, the lights of a
    /// skyline, and the one rule they all obey -- the player is 2.6 m tall, so nothing is hung where a body walks.
    /// </summary>
    public sealed class ExteriorDetailTests
    {
        private const int Seed = 87650022;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        private static IEnumerable<BuildingSite> Sample(int count, System.Func<BuildingSite, bool> keep)
        {
            int n = 0;
            foreach (BuildingSite s in TestSites.NearSpawn(World, 14))
            {
                if (keep(s))
                {
                    yield return s;
                    if (++n >= count)
                    {
                        yield break;
                    }
                }
            }
        }

        private static void Build(BuildingSite site, bool far, MeshBuffer structures, MeshBuffer concrete, MeshBuffer lit, MeshBuffer dark, out Vector3 origin)
        {
            origin = new Vector3(site.OwnerChunkX * CityLayout.ChunkSize, 0, site.OwnerChunkY * CityLayout.ChunkSize);
            CityMeshBuilder.BuildSite(World, site, origin, structures, concrete, lit, dark, new MeshBuffer(), null, null, new MeshBuffer(), far);
        }

        [Test]
        public void EveryRoofEndsInAParapet()
        {
            int checkedRoofs = 0;
            foreach (BuildingSite site in Sample(40, s => s.Params.Height > 15.0f))
            {
                var structures = new MeshBuffer();
                Build(site, false, structures, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), out _);
                float roofY = site.BaseHeight + site.Params.Height;
                float top = float.MinValue;
                foreach (Vector3 v in structures.Vertices)
                {
                    top = Mathf.Max(top, v.y);
                }
                Assert.GreaterOrEqual(top, roofY + 1.1f, $"building {site.Id}: nothing stands above its roof, so there is no parapet");
                checkedRoofs++;
            }
            Assert.Greater(checkedRoofs, 20, "need a real sample");
        }

        [Test]
        public void NothingIsHungWhereATallBodyWalks()
        {
            // Outside the building, between knee and head height of a 2.6 m player, the wall buffer holds only the wall's own thickness and
            // detail a few centimetres proud of it: no awning, sign, sill or unit juts far enough out to catch a body walking by.
            int checkedBuildings = 0;
            foreach (BuildingSite site in Sample(60, s => s.Params.Height > 12.0f))
            {
                var structures = new MeshBuffer();
                Build(site, false, structures, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), out Vector3 origin);
                float low = site.BaseHeight + 0.7f;
                float high = site.BaseHeight + 2.6f;
                foreach (Vector3 v in structures.Vertices)
                {
                    if (v.y < low || v.y > high)
                    {
                        continue;
                    }
                    site.WorldToLocal(origin.x + v.x, origin.z + v.z, out double lx, out double lz);
                    double out_ = System.Math.Max(System.Math.Abs(lx) - site.Params.Width * 0.5, System.Math.Abs(lz) - site.Params.Depth * 0.5);
                    Assert.Less(out_, 0.32, $"building {site.Id} ({site.Zone} {site.Params.Mass} {site.Params.Width:0.0}x{site.Params.Depth:0.0}x{site.Params.Height:0.0}, storefront {WindowPlan.For(World, site).Storefront}, ac {BuildingLook.For(site, WindowPlan.For(World, site).Style).AcDensity:0.00}): something juts {out_:0.00} m out at {v.y - site.BaseHeight:0.00} m up, in a walking body's way");
                }
                checkedBuildings++;
            }
            Assert.Greater(checkedBuildings, 30, "need a real sample");
        }

        [Test]
        public void AirConditionersHangOnHomesAndNotDowntownOrOnGlass()
        {
            foreach (ZoneKind zone in new[] { ZoneKind.Core, ZoneKind.Industrial })
            {
                for (int i = 0; i < 200; i++)
                {
                    Assert.AreEqual(0.0f, BuildingLook.For(i, zone, BuildingMass.MidRise, FacadeStyle.Punched).AcDensity, $"{zone}");
                }
            }
            for (int i = 0; i < 200; i++)
            {
                Assert.AreEqual(0.0f, BuildingLook.For(i, ZoneKind.Residential, BuildingMass.MidRise, FacadeStyle.Curtain).AcDensity, "a glass tower");
            }
            int homesWithUnits = 0;
            for (int i = 0; i < 200; i++)
            {
                homesWithUnits += BuildingLook.For(i, ZoneKind.OldTown, BuildingMass.MidRise, FacadeStyle.Punched).AcDensity > 0.0f ? 1 : 0;
            }
            Assert.Greater(homesWithUnits, 60, "most old-town homes have some");
        }

        [Test]
        public void SomeTallTowersWearALitCrownAndSomeDowntownOnesALitPanel()
        {
            int tall = 0, crowns = 0, panels = 0, downtown = 0;
            for (int id = 0; id < World.BuildingCount; id++)
            {
                BuildingSite site = World.Building(id);
                if (site.Params.Height < 70.0f)
                {
                    continue;
                }
                CityMeshBuilder.SkylineLightsOf(site, out bool crown, out bool panel);
                bool isTall = site.Params.Mass == BuildingMass.Tower || site.Params.Mass == BuildingMass.Supertall || site.Params.Height >= 110.0f;
                tall += isTall ? 1 : 0;
                crowns += crown ? 1 : 0;
                if (site.Zone == ZoneKind.Core || site.Zone == ZoneKind.Urban)
                {
                    downtown++;
                    panels += panel ? 1 : 0;
                }
            }
            Assert.Greater(tall, 50, "need towers");
            Assert.Greater(crowns, tall * 0.2, "a fair few crowns");
            Assert.Less(crowns, tall * 0.65, "but not all");
            Assert.Greater(panels, 0, "some panels");
            Assert.Less(panels, downtown * 0.3, "but few");
        }

        [Test]
        public void ALitCrownIsInTheLitGlassSoItComesOnAtDusk()
        {
            BuildingSite withCrown = default;
            for (int id = 0; id < World.BuildingCount && !withCrown.Exists; id++)
            {
                BuildingSite site = World.Building(id);
                CityMeshBuilder.SkylineLightsOf(site, out bool crown, out _);
                if (crown)
                {
                    withCrown = site;
                }
            }
            Assert.IsTrue(withCrown.Exists, "a tower with a crown");
            var lit = new MeshBuffer();
            Build(withCrown, true, new MeshBuffer(), new MeshBuffer(), lit, new MeshBuffer(), out _);
            float roofY = withCrown.BaseHeight + withCrown.Params.Height;
            int near = 0;
            foreach (Vector3 v in lit.Vertices)
            {
                near += v.y > roofY - 4.2f && v.y < roofY - 1.9f ? 1 : 0;
            }
            Assert.Greater(near, 16, "four walls of crown quads at the top of the tower, in the lit glass");
        }
    }
}
