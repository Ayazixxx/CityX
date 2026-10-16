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
    /// The doors onto the balconies in a real streamed city (World/BalconyDoors, View/BalconyDoorInteractor): a body the player's size walking
    /// into a shut door from its balcony opens it and walks on into the flat; a door half way up a room stays shut and holds the body on the
    /// balcony; E shuts an open door and opens it again, but not on someone standing in it.
    /// </summary>
    public sealed class BalconyDoorPlayTests
    {
        private const int Seed = 91736402;
        private readonly List<GameObject> _made = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            BalconyDoorState.Shared.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
            BalconyDoorState.Shared.Clear();
        }

        /// <summary>
        /// A free-standing apartment block near the spawn with a door of <paramref name="kind"/> that opens, and a door in it that stays shut (the
        /// stairs or a wall just inside: since 2026-10-15 every door is on a floor, there is no door half way up a room any more).
        /// </summary>
        private static bool FindDoors(CityWorld world, BalconyDoorKind kind, out BuildingSite site, out BalconyDoor opens, out BalconyDoor locked)
        {
            var doors = new List<BalconyDoor>();
            foreach (BuildingSite candidate in TestSites.NearSpawn(world, 20))
            {
                if (candidate.Contacts != null && candidate.Contacts.Length > 0)
                {
                    continue;
                }
                BalconyDoors.For(candidate, WindowPlan.For(world, candidate), doors);
                int open = -1, shut = -1;
                for (int i = 0; i < doors.Count; i++)
                {
                    if (open < 0 && doors[i].Opens && doors[i].Kind == kind) open = i;
                    if (shut < 0 && !doors[i].Opens) shut = i;
                }
                if (open >= 0 && shut >= 0)
                {
                    site = candidate;
                    opens = doors[open];
                    locked = doors[shut];
                    return true;
                }
            }
            site = default;
            opens = locked = default;
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

        /// <summary>A stand-in for the player: a character controller the player's size (PlayerRigBuilder, PlayerLocomotion), its feet at <paramref name="feet"/>.</summary>
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

        /// <summary>Walks the body along <paramref name="direction"/> at a walk for <paramref name="seconds"/>, falling as it would.</summary>
        private static IEnumerator Walk(CharacterController body, Vector3 direction, float seconds)
        {
            const float dt = 1.0f / 60.0f;
            for (float t = 0.0f; t < seconds; t += dt)
            {
                body.Move((direction * 2.5f + Vector3.down * 3.0f) * dt);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator WalkingIntoASlidingDoorFromTheBalconyOpensItAndYouWalkIntoTheFlat()
        {
            var world = new CityWorld(Seed);
            Assert.IsTrue(FindDoors(world, BalconyDoorKind.Sliding, out BuildingSite site, out BalconyDoor door, out _),
                "no apartment block near the spawn with a sliding door that opens");
            var near = new BalconyDoorInteractor.Near(site, door);
            BalconyDoors.Passage(site, door, out float from, out float to);
            Vector3 balcony = near.Corner + near.Along * ((from + to) * 0.5f) + near.Outward * 1.1f + Vector3.up * 0.05f;
            City(balcony, out BalconyDoorInteractor interactor);
            yield return null;

            CharacterController body = Body(balcony);
            interactor.PlayerBody = body;
            yield return Walk(body, -near.Outward, 2.0f);

            Assert.IsTrue(BalconyDoorState.Shared.IsOpen(site.Id, door.Unit, door.Row), "walking into it did not open it");
            Vector3 p = near.Local(body.transform.position);
            Debug.Log($"CityX balcony door: building {site.Id} flat {door.Unit} row {door.Row}, the body ended {-p.z:F2} m in from the wall's outer face, " +
                $"{p.y:F2} m above the balcony floor");
            Assert.Less(p.z, -(BuildingInterior.WallThickness + 0.5f), "the body did not get through the doorway into the flat");
            Assert.AreEqual(0.0f, p.y, 0.3f, "the body is not on the flat's floor");
        }

        [UnityTest]
        public IEnumerator ADoorWithTheStairsJustInsideStaysShutAndHoldsYouOnTheBalcony()
        {
            var world = new CityWorld(Seed);
            Assert.IsTrue(FindDoors(world, BalconyDoorKind.Sliding, out BuildingSite site, out _, out BalconyDoor locked),
                "no apartment block near the spawn with a locked door");
            var near = new BalconyDoorInteractor.Near(site, locked);
            Vector3 balcony = near.Corner + near.Along * (locked.Width * 0.5f) + near.Outward * 1.1f + Vector3.up * 0.05f;
            City(balcony, out BalconyDoorInteractor interactor);
            yield return null;

            CharacterController body = Body(balcony);
            interactor.PlayerBody = body;
            yield return Walk(body, -near.Outward, 1.5f);

            Assert.IsFalse(BalconyDoorState.Shared.IsOpen(site.Id, locked.Unit, locked.Row), "a door with the stairs or a wall just inside opened");
            Assert.IsTrue(interactor.LastWasLocked, "walking into it did not try it");
            Vector3 p = near.Local(body.transform.position);
            Assert.Greater(p.z, 0.0f, "the body went through a shut door");
        }

        [UnityTest]
        public IEnumerator EShutsAnOpenDoorButNotOnSomeoneInItAndOpensItAgain()
        {
            var world = new CityWorld(Seed);
            Assert.IsTrue(FindDoors(world, BalconyDoorKind.Double, out BuildingSite site, out BalconyDoor door, out _)
                || FindDoors(world, BalconyDoorKind.Sliding, out site, out door, out _), "no apartment block near the spawn with a door that opens");
            var near = new BalconyDoorInteractor.Near(site, door);
            Vector3 balcony = near.Corner + near.Along * (door.Width * 0.5f) + near.Outward * 1.2f;
            City(balcony, out BalconyDoorInteractor interactor);
            yield return null;
            Vector3 eye = balcony + Vector3.up * 2.3f;
            Vector3 look = (near.Handle - eye).normalized;

            Assert.IsTrue(interactor.TryUse(balcony, eye, look), "E at the door did nothing");
            Assert.IsTrue(BalconyDoorState.Shared.IsOpen(site.Id, door.Unit, door.Row), "E did not open it");
            Assert.IsTrue(interactor.TryUse(balcony, eye, look));
            Assert.IsFalse(BalconyDoorState.Shared.IsOpen(site.Id, door.Unit, door.Row), "E did not shut it");
            Assert.IsTrue(interactor.TryUse(balcony, eye, look));
            Assert.IsTrue(BalconyDoorState.Shared.IsOpen(site.Id, door.Unit, door.Row), "E did not open it again");

            // Standing in the doorway, it stays open.
            Vector3 inIt = near.Corner + near.Along * (door.Width * 0.5f) - near.Outward * 0.15f;
            Assert.IsTrue(interactor.TryUse(inIt, inIt + Vector3.up * 2.3f, (near.Handle - (inIt + Vector3.up * 2.3f)).normalized));
            Assert.IsTrue(BalconyDoorState.Shared.IsOpen(site.Id, door.Unit, door.Row), "it shut on someone standing in it");
            yield return null;
        }
    }
}
