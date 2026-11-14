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
    /// A footbridge's doors are opened the way the player opens them (owner, 2026-10-16: "doesnt let me open sliding glass doors bridges connecting
    /// buildings"): through the door interactor, with E and by walking into them, on whatever wall the bridge meets. Until then the interactor only
    /// looked for doors along a building's back wall (the flats' balcony doors), so a bridge door on any other wall could not be opened at all; the
    /// bridge walk test never went through one.
    /// </summary>
    public sealed class FootbridgeDoorPlayTests
    {
        private const int Seed = CitySeed.Default;
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _made.Clear();
            BalconyDoorState.Shared.Clear();
        }

        /// <summary>A bridge door near the spawn on a wall other than the back one (where the old search never looked), on a free-standing block.</summary>
        private static bool FindDoor(CityWorld world, out BuildingSite site, out BalconyDoor door)
        {
            var doors = new List<BalconyDoor>();
            foreach (BuildingSite s in TestSites.NearSpawn(world, 30))
            {
                if (Footbridges.For(world, s).Length == 0 || s.Contacts != null && s.Contacts.Length > 0) continue;
                doors.Clear();
                Footbridges.DoorsOf(world, s, WindowPlan.For(world, s), doors);
                foreach (BalconyDoor d in doors)
                {
                    if (d.Wall == ApartmentFront.BalconyWall || !d.Opens) continue;
                    site = s;
                    door = d;
                    return true;
                }
            }
            site = default;
            door = default;
            return false;
        }

        private CityStreamer City(Vector3 around, out BalconyDoorInteractor interactor)
        {
            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 2;
            streamer.GeneratorThreads = 0;   // a door opened rebuilds its chunk at once, not in the background
            streamer.SpawnPreStreamBudget = 0;
            interactor = go.AddComponent<BalconyDoorInteractor>();
            go.SetActive(true);
            streamer.StreamAround(around, int.MaxValue);
            Physics.SyncTransforms();
            return streamer;
        }

        private CharacterController Body(Vector3 feet)
        {
            var go = new GameObject("Body");
            _made.Add(go);
            go.transform.position = feet;
            var controller = go.AddComponent<CharacterController>();
            controller.height = BodyRig.StandingHeight;
            controller.radius = FusedBodyShape.TorsoRadius;
            controller.skinWidth = 0.03f;
            controller.center = new Vector3(0.0f, BodyRig.StandingHeight * 0.5f, 0.0f);
            controller.stepOffset = 0.3f;
            controller.minMoveDistance = 0.0f;
            controller.slopeLimit = 50.0f;
            Physics.SyncTransforms();
            return controller;
        }

        [UnityTest]
        public IEnumerator EOpensAndShutsABridgeDoorOnAFrontOrSideWall()
        {
            var world = new CityWorld(Seed);
            Assert.IsTrue(FindDoor(world, out BuildingSite site, out BalconyDoor door), "no footbridge door near the spawn on a front or side wall");
            var near = new BalconyDoorInteractor.Near(site, door);
            Vector3 deck = near.Corner + near.Along * (door.Width * 0.5f) + near.Outward * 1.3f + Vector3.up * 0.05f;
            City(deck, out BalconyDoorInteractor interactor);
            yield return null;
            Vector3 eye = deck + Vector3.up * 2.3f;
            Vector3 look = (near.Handle - eye).normalized;
            Debug.Log($"CityX bridge door: building {site.Id}, the {door.Wall} wall, {door.Kind} {door.Width:F2} m wide, row {door.Row}");

            Assert.IsTrue(interactor.TryUse(deck, eye, look), $"E at the bridge door on the {door.Wall} wall did nothing");
            Assert.IsTrue(BalconyDoorState.Shared.IsOpen(site.Id, door.Unit, door.Row), "E did not open it");
            Assert.IsTrue(interactor.TryUse(deck, eye, look));
            Assert.IsFalse(BalconyDoorState.Shared.IsOpen(site.Id, door.Unit, door.Row), "E did not shut it");
        }

        [UnityTest]
        public IEnumerator WalkingDownTheDeckIntoABridgeDoorOpensItAndYouWalkIntoTheBuilding()
        {
            var world = new CityWorld(Seed);
            Assert.IsTrue(FindDoor(world, out BuildingSite site, out BalconyDoor door), "no footbridge door near the spawn on a front or side wall");
            var near = new BalconyDoorInteractor.Near(site, door);
            BalconyDoors.Passage(site, door, out float from, out float to);
            Vector3 deck = near.Corner + near.Along * ((from + to) * 0.5f) + near.Outward * 2.6f + Vector3.up * 0.05f;
            City(deck, out BalconyDoorInteractor interactor);
            yield return null;

            CharacterController body = Body(deck);
            interactor.PlayerBody = body;
            const float dt = 1.0f / 60.0f;
            for (float t = 0.0f; t < 3.0f; t += dt)
            {
                body.Move((-near.Outward * 2.5f + Vector3.down * 3.0f) * dt);
                yield return null;
            }

            Assert.IsTrue(BalconyDoorState.Shared.IsOpen(site.Id, door.Unit, door.Row), "walking into the bridge door did not open it");
            Vector3 p = near.Local(body.transform.position);
            Debug.Log($"CityX bridge door walk: building {site.Id}, the {door.Wall} wall, the body ended {-p.z:F2} m in from the wall's face, {p.y:F2} m over the floor");
            Assert.Less(p.z, -(BuildingInterior.WallThickness + 0.5f), "the body did not get through the doorway into the building");
            Assert.AreEqual(0.0f, p.y, 0.3f, "the body is not on the building's floor");
        }
    }
}
