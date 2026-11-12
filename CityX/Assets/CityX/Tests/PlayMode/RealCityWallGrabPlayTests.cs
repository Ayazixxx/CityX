using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The wall probe against the city's own geometry. Reported: climbing kicked in "running up a stairs, trying
    /// to jump through a window, running through a doorway". The synthetic fixtures (WallGrabIntentTests) say the
    /// probe rejects a doorway-shaped gap; this checks it against the REAL streamed chunk: the plain wall of a
    /// building is a wall, and the real doorway in the same wall is not.
    /// </summary>
    public sealed class RealCityWallGrabPlayTests
    {
        private const int Seed = 48398181;
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
        }

        [UnityTest]
        public IEnumerator ARealBuildingsWallIsAWallAndItsRealDoorwayIsNot()
        {
#if UNITY_EDITOR
            var world = new CityWorld(Seed);
            BuildingSite site = default;
            bool found = false;
            foreach (BuildingSite candidate in TestSites.NearSpawn(world))
            {
                // A door at street level: a raised one has its entrance stairs along this wall (EntranceStairs), which is not plain wall.
                // And a wall with wall in it: a curtain wall is glass from floor to ceiling, and a shop front is glass at the street, so neither is a
                // plain wall to grab at a standing body's height (seed 63428175's first pick was a curtain-wall office: 3 spots of 75).
                WindowPlan plan = WindowPlan.For(world, candidate);
                if (candidate.Params.Width > 10.0f && candidate.Params.Height > 8.0f && !candidate.OnRoof && !candidate.Params.IsSpecial   // a garage or mall has open entrances, not a doorway
                    && !BuildingDecor.EntranceStyleFor(candidate.BaseHeight - candidate.DoorGroundHeight).NeedsStairs
                    && plan.Style != FacadeStyle.Curtain && !plan.Storefront)
                {
                    site = candidate;
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, "no building to test against");
            WindowPlan facade = WindowPlan.For(world, site);
            Debug.Log($"CityX wall probe building {site.Id}: {site.Params.Width:F1} x {site.Params.Depth:F1} x {site.Params.Height:F1} m, {site.Params.Mass}, " +
                $"{facade.Style}, storefront {facade.Storefront}, apartment front {ApartmentFront.For(site, facade).Any}, zone {site.Zone}, base {site.BaseHeight:F2}, " +
                $"door ground {site.DoorGroundHeight:F2}, contacts {(site.Contacts?.Length ?? 0)}");

            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 2;
            streamer.GeneratorThreads = 0;
            streamer.SpawnPreStreamBudget = 0;
            go.SetActive(true);

            float floorY = site.BaseHeight;
            Vector3 inward = -CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North);
            Vector3 standAt = TestSites.At(site, site.CentreX, floorY, site.MaxZ + 0.6f);
            streamer.StreamAround(standAt, int.MaxValue);
            yield return null;
            yield return null;
            Physics.SyncTransforms();

            float height = BodyRig.StandingHeight;
            var doorU0 = (float)site.CentreX - BuildingInterior.DoorWidth * 0.5f;
            var doorU1 = (float)site.CentreX + BuildingInterior.DoorWidth * 0.5f;

            int wallSpots = 0;
            int wallProbes = 0;
            int doorwaySpots = 0;
            int doorwayProbes = 0;
            for (float x = (float)site.MinX + 1.0f; x <= (float)site.MaxX - 1.0f; x += 0.25f)
            {
                Vector3 at = TestSites.At(site, x, floorY, site.MaxZ + 0.6f);
                bool isWall = WallProbe.TryFind(at, inward, height, out _);
                bool inDoorway = x > doorU0 + 0.5f && x < doorU1 - 0.5f;
                bool nearDoorway = x > doorU0 - 0.8f && x < doorU1 + 0.8f;
                if (inDoorway)
                {
                    doorwayProbes++;
                    if (isWall) doorwaySpots++;
                }
                else if (!nearDoorway)
                {
                    wallProbes++;
                    if (isWall) wallSpots++;
                }
            }

            Debug.Log($"CityX wall probe on a real facade: {wallSpots}/{wallProbes} plain-wall spots accepted, {doorwaySpots}/{doorwayProbes} doorway spots accepted");
            Assert.Greater(wallProbes, 6);
            Assert.Greater(doorwayProbes, 1);
            Assert.AreEqual(0, doorwaySpots, "the real doorway was taken for a wall: running through a door would still start a climb");
            Assert.GreaterOrEqual(wallSpots / (float)wallProbes, 0.8f,
                "most of a real building's plain wall was refused: nobody could climb a building");
#else
            yield break;
#endif
        }
    }
}
