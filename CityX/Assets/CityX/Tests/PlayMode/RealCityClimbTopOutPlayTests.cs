using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// THE REPORTED BUG, on the real city: climbing a real building's outside wall mantled at the floor of an inner storey and carried the player through
    /// the outer wall to the inside. Climbs a real streamed facade at several spots; wherever the climb ends in a mantle it must end ON the building's roof (up
    /// and over the top), never at an interior floor part-way up, and never with the body inside geometry.
    /// </summary>
    public sealed class RealCityClimbTopOutPlayTests
    {
        private const int Seed = 48398181;
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
        }

        [UnityTest]
        public IEnumerator ClimbingARealFacadeNeverTopsOutAtAnInteriorFloor()
        {
#if UNITY_EDITOR
            var world = new CityWorld(Seed);
            BuildingSite site = default;
            bool found = false;
            foreach (BuildingSite candidate in TestSites.NearSpawn(world))
            {
                // A plain box with a roof to reach: not round, not sculpture, not under a sealed block, and no shop awning, apartment walkway
                // or balconies across the wall climbed (a climber cannot get past an overhang, and a stack of balcony slabs is no wall to grab:
                // this would become a test of awnings or balconies; seed 91736402's first candidate was a block of flats).
                WindowPlan facade = WindowPlan.For(world, candidate);
                // ...and an ordinary building: a parking garage's front is open spandrel bands with air above each (seed 91736402's first pick:
                // nothing to grab at four spots of six, a 1.4 m spandrel at the others).
                bool plain = !candidate.Shell && !candidate.OnRoof && candidate.Sides < 3 && !candidate.RoofSealed
                    && !facade.Storefront
                    && !ApartmentFront.For(candidate, facade).Any
                    && !candidate.IsParkingGarage && !candidate.IsOpenGround && !candidate.IsMall && !candidate.IsMallAnchor;
                // ...no taller than its real window rows: past CityMeshBuilder.FullDetailRows (100 m) a tower's wall is a flat panel with nothing to
                // grab (seed 46283197's first pick, a 122 m curtain-wall tower, stalled every climb 100 m up).
                if (plain && candidate.Params.Width > 10.0f && candidate.Params.Height > 12.0f && candidate.Params.Height <= 60.0f)
                {
                    site = candidate;
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, "no tall building to test against");
            Debug.Log($"CityX facade climb building {site.Id}: {site.Params.Width:F1} x {site.Params.Depth:F1} x {site.Params.Height:F1} m, {site.Params.Mass}, " +
                $"{WindowPlan.For(world, site).Style}, zone {site.Zone}, special '{site.Params.SpecialType}', contacts {(site.Contacts?.Length ?? 0)}, " +
                $"roof {RoofShape.For(site).Kind}, wing {site.Wing}, base {site.BaseHeight:F2}, podium {site.PodiumBase:F2}");

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
            streamer.StreamAround(TestSites.At(site, site.CentreX, floorY, site.MaxZ + 0.6f), int.MaxValue);
            yield return null;
            yield return null;
            Physics.SyncTransforms();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            float roofY = floorY + site.Params.Height;
            int climbs = 0;
            int mantles = 0;
            for (int i = 0; i < 6; i++)
            {
                float x = (float)site.MinX + 2.0f + i * (float)(site.MaxX - site.MinX - 4.0) / 5.0f;
                Vector3 at = TestSites.At(site, x, floorY, site.MaxZ + 0.7f);
                GameObject player = Object.Instantiate(prefab, at, Quaternion.LookRotation(inward, Vector3.up));
                _made.Add(player);
                var climbing = player.GetComponent<PlayerClimbing>();
                var vitals = player.GetComponent<PlayerVitals>();
                vitals.InfiniteStamina = true;
                yield return null;
                yield return null;
                if (!climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f))
                {
                    bool wallThere = Physics.Raycast(at + Vector3.up * 1.3f, inward, out RaycastHit probe, 3.0f, ~0, QueryTriggerInteraction.Ignore);
                    Debug.Log($"CityX climb spot {i}: no grab at {at}; a ray at chest height {(wallThere ? $"hits {probe.collider.name} {probe.distance:F2} m away (normal {probe.normal})" : "hits nothing within 3 m")}; " +
                        $"grounded={player.GetComponent<CharacterController>().isGrounded} y={player.transform.position.y:F2} floor={floorY:F2}");
                    continue;
                }
                climbs++;

                bool mantled = false, entered = false;
                float mantleStartY = 0.0f;
                const float dt = 1.0f / 60.0f;
                var vaulting = player.GetComponent<PlayerVaulting>();
                for (float t = 0.0f; t < 40.0f; t += dt)
                {
                    if (climbing.IsClimbing) climbing.Tick(false, true, false, vitals, dt, false);
                    else if (climbing.IsToppingOut)
                    {
                        if (!mantled) mantleStartY = player.transform.position.y;
                        mantled = true;
                        climbing.TickMantle(dt);
                    }
                    else if (vaulting != null && vaulting.IsVaulting)
                    {
                        // An opening in reach on the way up (a broken window, a footbridge's door since 2026-10-15) is dived through from the climb,
                        // which hands the body to the vault: driven here as the locomotion drives it in the game, to its end.
                        entered = true;
                        vaulting.Tick(dt);
                    }
                    else break;
                    yield return null;
                }
                Debug.Log($"CityX climb spot {i}: mantled={mantled} entered an opening={entered} climbing={climbing.IsClimbing} y={player.transform.position.y:F1} roof={roofY:F1} start={floorY:F1} stamina={vitals.Stamina:F1} grounded={player.GetComponent<CharacterController>().isGrounded}");
                if (mantled)
                {
                    mantles++;
                    Assert.Greater(player.transform.position.y, roofY - 1.0f,
                        $"spot {i}: topped out at y={player.transform.position.y:F1} (began mantle at {mantleStartY:F1}), well below the roof at {roofY:F1}: an interior floor");
                }
                if (!LedgeProbe.HasRoom(player.transform.position, BodyRig.StandingHeight, 0.3f, player.transform))
                {
                    var names = new List<string>();
                    Vector3 p = player.transform.position;
                    foreach (Collider c in Physics.OverlapCapsule(p + Vector3.up * 0.36f, p + Vector3.up * (BodyRig.StandingHeight - 0.3f), 0.27f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (!c.transform.IsChildOf(player.transform)) names.Add($"{c.name} ({c.GetType().Name})");
                    }
                    Assert.Fail($"spot {i}: the body is inside geometry at {p}: {string.Join(", ", names)}");
                }
                Assert.IsTrue(player.GetComponent<CharacterController>().enabled, $"spot {i}: controller left off");
            }
            Debug.Log($"CityX facade climbs: {climbs} grabbed, {mantles} topped out");
            Assert.Greater(climbs, 0, "could not grab the facade anywhere");
            Assert.Greater(mantles, 0, "no facade climb reached the roof");
#else
            yield break;
#endif
        }
    }
}
