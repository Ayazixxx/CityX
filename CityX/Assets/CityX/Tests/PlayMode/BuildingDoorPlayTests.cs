using System.Collections;
using System.Collections.Generic;
using CityX.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// How a door behaves, in real frames: which way it swings, that it never shoves or shuts on anyone, that
    /// walking into it opens it, and that it can be reversed mid-swing. A door sits on the wall plane at z = 0
    /// facing +Z (the street), hinged at x = 0, 1.4m wide and 2.4m tall -- so the leaf, opened, lies along z.
    /// </summary>
    public sealed class BuildingDoorPlayTests
    {
        private const float Width = 1.4f;
        private const float Height = 2.4f;

        private BuildingDoor _door;
        private readonly List<GameObject> _made = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Door");
            _made.Add(go);
            _door = go.AddComponent<BuildingDoor>();
            _door.Hang(new BuildingDoor.Panel(Vector3.zero, Width, Height, Vector3.forward), null, null, false);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
        }

        /// <summary>A stand-in person: a capsule with a CharacterController, its feet at <paramref name="position"/>.</summary>
        private Transform Person(Vector3 position)
        {
            var go = new GameObject("Person");
            _made.Add(go);
            go.transform.position = position;
            var controller = go.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0.0f, 0.9f, 0.0f);
            Physics.SyncTransforms();
            return go.transform;
        }

        private IEnumerator Seconds(float seconds)
        {
            float elapsed = 0.0f;
            while (elapsed < seconds)
            {
                float dt = Time.deltaTime;
                yield return null;
                elapsed += dt;
            }
        }

        /// <summary>The leaf's free end, in world space.</summary>
        private Vector3 FreeEnd() => _door.transform.TransformPoint(new Vector3(Width, 0.0f, 0.0f));

        // ---- which way it swings ----

        [Test]
        public void TheSideYouStandOnDecidesWhichWayItSwings()
        {
            Vector3 hinge = Vector3.zero;
            Assert.AreEqual(1, BuildingDoor.SwingSignFor(hinge, Vector3.forward, new Vector3(0.7f, 0.0f, 2.0f)), "from the street it opens inward");
            Assert.AreEqual(-1, BuildingDoor.SwingSignFor(hinge, Vector3.forward, new Vector3(0.7f, 0.0f, -2.0f)), "from inside it opens outward");
            Assert.AreEqual(1, BuildingDoor.SwingSignFor(hinge, Vector3.forward, new Vector3(0.7f, 0.0f, 0.0f)), "standing in the doorway it keeps the old inward swing");
        }

        [UnityTest]
        public IEnumerator FromTheStreetItSwingsInwardAwayFromYou()
        {
            Transform person = Person(new Vector3(0.7f, 0.0f, 1.5f));
            Assert.IsTrue(_door.Interact(person));
            yield return Seconds(1.3f);

            Assert.IsTrue(_door.IsOpen);
            Assert.AreEqual(BuildingDoor.OpenAngleDegrees, _door.Angle, 0.5f, "it should settle fully open");
            Assert.Less(FreeEnd().z, -0.5f, "the leaf should have swung INTO the building, away from the person outside");
        }

        [UnityTest]
        public IEnumerator FromInsideItSwingsOutwardAwayFromYouInsteadOfIntoYou()
        {
            Transform person = Person(new Vector3(0.7f, 0.0f, -1.5f));
            Assert.IsTrue(_door.Interact(person));
            yield return Seconds(1.3f);

            Assert.AreEqual(-BuildingDoor.OpenAngleDegrees, _door.Angle, 0.5f);
            Assert.Greater(FreeEnd().z, 0.5f, "the leaf should have swung OUT over the street, away from the person inside");
            Assert.Less(Vector3.Distance(person.position, new Vector3(0.7f, 0.0f, -1.5f)), 0.01f, "nobody should have been shoved");
        }

        // ---- the feel of the motion ----

        [UnityTest]
        public IEnumerator ItIsQuickAndSettlesRatherThanCreeping()
        {
            Transform person = Person(new Vector3(0.7f, 0.0f, 1.5f));
            _door.Interact(person);
            yield return Seconds(0.35f);
            Assert.Greater(_door.Angle, 45.0f, "a push should get most of the way in a third of a second");

            yield return Seconds(1.0f);
            Assert.AreEqual(BuildingDoor.OpenAngleDegrees, _door.Angle, 0.5f, "and then it should have settled on the angle, not still be drifting");
        }

        [UnityTest]
        public IEnumerator PressingAgainMidSwingTurnsItRoundSmoothly()
        {
            Transform person = Person(new Vector3(0.7f, 0.0f, 1.5f));
            _door.Interact(person);
            yield return Seconds(0.2f);
            float atReversal = _door.Angle;
            Assert.That(atReversal, Is.InRange(5.0f, 95.0f), "it should be part way when reversed");

            _door.Interact(person);   // close it again
            float highest = atReversal;
            float elapsed = 0.0f;
            while (elapsed < 1.6f)
            {
                float dt = Time.deltaTime;
                yield return null;
                elapsed += dt;
                highest = Mathf.Max(highest, _door.Angle);
            }

            Assert.IsFalse(_door.IsOpen);
            Assert.AreEqual(0.0f, _door.Angle, 0.5f, "it should end shut");
            Assert.Less(highest - atReversal, 12.0f, "reversing should not carry it much further open first");
        }

        // ---- it never pushes or traps anyone ----

        [UnityTest]
        public IEnumerator ItStopsForSomeoneInItsPathInsteadOfShovingThemAndGoesOnWhenTheyMove()
        {
            Transform opener = Person(new Vector3(0.7f, 0.0f, 1.5f));
            // Standing inside, where the leaf will swing: about 0.6m in from the wall, mid-width.
            Transform inTheWay = Person(new Vector3(0.9f, 0.0f, -0.7f));
            Vector3 startedAt = inTheWay.position;

            _door.Interact(opener);
            yield return Seconds(1.2f);

            Assert.IsTrue(_door.IsBlocked, "the door should be holding still against the person");
            Assert.Less(_door.Angle, BuildingDoor.OpenAngleDegrees - 8.0f, "it should not have swung through them");
            Assert.Less(Vector3.Distance(inTheWay.position, startedAt), 0.01f, "nobody should have been pushed");

            inTheWay.position = new Vector3(-3.0f, 0.0f, -3.0f);
            Physics.SyncTransforms();
            yield return Seconds(1.2f);

            Assert.IsFalse(_door.IsBlocked);
            Assert.AreEqual(BuildingDoor.OpenAngleDegrees, _door.Angle, 0.5f, "it should carry on once the way is clear");
        }

        [UnityTest]
        public IEnumerator SomeoneStandingRightAgainstTheDoorOnYourSideDoesNotJamIt()
        {
            // Pressed flush to the closed leaf on the street side, where it swings AWAY from them.
            Transform pressed = Person(new Vector3(0.7f, 0.0f, 0.15f));
            _door.Interact(pressed);
            yield return Seconds(1.3f);

            Assert.IsFalse(_door.IsBlocked, "a door swinging away from someone must not be stopped by them");
            Assert.AreEqual(BuildingDoor.OpenAngleDegrees, _door.Angle, 0.5f);
        }

        [UnityTest]
        public IEnumerator ItNeverShutsOnSomeoneStandingInTheDoorway()
        {
            Transform opener = Person(new Vector3(0.7f, 0.0f, 1.5f));
            _door.Interact(opener);
            yield return Seconds(1.3f);
            Assert.IsTrue(_door.IsOpen);

            Transform inDoorway = Person(new Vector3(0.7f, 0.0f, 0.0f));
            Assert.IsFalse(_door.Interact(inDoorway), "E on a door someone is standing in must do nothing");
            yield return Seconds(0.5f);
            Assert.IsTrue(_door.IsOpen, "the door must not shut on them");

            inDoorway.position = new Vector3(0.7f, 0.0f, 4.0f);
            Physics.SyncTransforms();
            Assert.IsTrue(_door.Interact(opener), "once the doorway is clear it shuts as asked");
        }

        // ---- walking into it ----

        [UnityTest]
        public IEnumerator WalkingIntoItOpensItAwayFromYou()
        {
            Transform walker = Person(new Vector3(0.7f, 0.0f, 0.5f));
            Assert.IsFalse(_door.Bump(walker, 0.5f), "drifting against it slowly does nothing");
            Assert.IsTrue(_door.Bump(walker, 2.0f), "walking into it opens it");
            yield return Seconds(1.3f);
            Assert.AreEqual(BuildingDoor.OpenAngleDegrees, _door.Angle, 0.5f);
        }

        [UnityTest]
        public IEnumerator RunningIntoItThrowsItOpenFasterThanWalking()
        {
            Transform walker = Person(new Vector3(0.7f, 0.0f, 0.5f));
            _door.Bump(walker, 2.0f);
            float walkingReached = 0.0f;
            float t = 0.0f;
            while (t < 1.0f && _door.Angle < 90.0f)
            {
                float dt = Time.deltaTime;
                yield return null;
                t += dt;
                walkingReached = t;
            }

            // A second door, thrown open at a run.
            var second = new GameObject("Door2");
            _made.Add(second);
            var fast = second.AddComponent<BuildingDoor>();
            fast.Hang(new BuildingDoor.Panel(new Vector3(20.0f, 0.0f, 0.0f), Width, Height, Vector3.forward), null, null, false);
            Transform runner = Person(new Vector3(20.7f, 0.0f, 0.5f));
            fast.Bump(runner, 6.5f);
            float runningReached = 0.0f;
            t = 0.0f;
            while (t < 1.0f && fast.Angle < 90.0f)
            {
                float dt = Time.deltaTime;
                yield return null;
                t += dt;
                runningReached = t;
            }

            Assert.Less(runningReached, walkingReached, $"a run-through ({runningReached:0.00}s) should open faster than a walk-through ({walkingReached:0.00}s)");
        }

        // ---- looking at it ----

        [Test]
        public void EOnlyCountsWhenYouAreLookingTowardTheDoor()
        {
            Vector3 eye = new Vector3(0.7f, 1.6f, 2.0f);
            Vector3 door = new Vector3(0.7f, 1.2f, 0.0f);
            Assert.IsTrue(BuildingDoor.IsLookingToward(eye, Vector3.back, door), "looking straight at it");
            Assert.IsTrue(BuildingDoor.IsLookingToward(eye, Quaternion.Euler(0, 40, 0) * Vector3.back, door), "looking a little off it");
            Assert.IsFalse(BuildingDoor.IsLookingToward(eye, Vector3.forward, door), "with your back to it");
            Assert.IsFalse(BuildingDoor.IsLookingToward(eye, Vector3.right, door), "looking along the wall");
        }

        // ---- other callers ----

        [UnityTest]
        public IEnumerator TheOldGuysOwnSetOpenStillWorksAndTellsTheStreamer()
        {
            var toggles = new List<bool>();
            _door.Toggled += toggles.Add;

            _door.SetOpen(true);
            yield return Seconds(1.3f);
            Assert.AreEqual(BuildingDoor.OpenAngleDegrees, _door.Angle, 0.5f, "an NPC's open still swings inward, as before");

            _door.SetOpen(false);
            yield return Seconds(1.3f);
            Assert.AreEqual(0.0f, _door.Angle, 0.5f);
            CollectionAssert.AreEqual(new[] { true, false }, toggles, "the streamer must hear about both");
        }

        [UnityTest]
        public IEnumerator TheHandlesTurnWithTheLeafOnBothFaces()
        {
            Rigidbody body = _door.GetComponentInChildren<Rigidbody>();
            Assert.IsNotNull(body);
            Assert.AreEqual(RigidbodyInterpolation.None, body.interpolation, "an interpolated leaf draws behind the turning root and leaves the handle floating");
            int levers = 0;
            foreach (Transform t in body.transform.GetComponentsInChildren<Transform>())
            {
                levers += t.name == "HandleLever" ? 1 : 0;
            }
            Assert.AreEqual(2, levers, "a handle on each face, all under the one moving body");

            Transform lever = body.transform.Find("HandleLever");
            Vector3 local = body.transform.InverseTransformPoint(lever.position);
            _door.SetOpen(true);
            yield return Seconds(1.0f);
            Assert.Less(Vector3.Distance(local, body.transform.InverseTransformPoint(lever.position)), 0.001f, "fixed to the leaf");
            Assert.Greater(Vector3.Distance(lever.position, _door.transform.position), 0.5f);
        }

        [UnityTest]
        public IEnumerator ADoorRestoredOpenStartsFullyOpenWithoutASound()
        {
            var go = new GameObject("Restored");
            _made.Add(go);
            var restored = go.AddComponent<BuildingDoor>();
            restored.Hang(new BuildingDoor.Panel(new Vector3(40.0f, 0.0f, 0.0f), Width, Height, Vector3.forward), null, null, true);
            yield return null;

            Assert.IsTrue(restored.IsOpen);
            Assert.AreEqual(BuildingDoor.OpenAngleDegrees, restored.Angle, 0.01f);
        }

        // ---- a pair of doors (owner, 2026-10-15: double front doors) ----

        /// <summary>
        /// A pair across a 2.4 m doorway on the wall plane z = 0 facing +Z, 30 m from the single door: the left leaf hinged at its left edge facing
        /// out, the right one hinged at its right edge facing in (as BuildingDoor.PanelsFor hangs them), linked. Returns both.
        /// </summary>
        private BuildingDoor[] Pair(bool startOpen = false)
        {
            const float leaf = 1.2f;
            var origin = new Vector3(30.0f, 0.0f, 0.0f);
            var left = new GameObject("PairLeft").AddComponent<BuildingDoor>();
            var right = new GameObject("PairRight").AddComponent<BuildingDoor>();
            _made.Add(left.gameObject);
            _made.Add(right.gameObject);
            Vector3 street = origin + new Vector3(0.0f, 0.0f, 2.0f);
            left.Hang(new BuildingDoor.Panel(origin + new Vector3(-leaf, 0.0f, 0.0f), leaf, Height, Vector3.forward), null, null, startOpen, street);
            right.Hang(new BuildingDoor.Panel(origin + new Vector3(leaf, 0.0f, -BuildingDoor.Thickness), leaf, Height, Vector3.back), null, null, startOpen, street);
            left.Partner = right;
            right.Partner = left;
            return new[] { left, right };
        }

        /// <summary>A leaf's free edge, in world space.</summary>
        private static Vector3 FreeEndOf(BuildingDoor door, float width) => door.transform.TransformPoint(new Vector3(width, 0.0f, 0.0f));

        [UnityTest]
        public IEnumerator APairOpensAsOneAwayFromWhoeverWalksIntoIt()
        {
            BuildingDoor[] pair = Pair();
            Assert.Less(Vector3.Distance(FreeEndOf(pair[0], 1.2f), FreeEndOf(pair[1], 1.2f)), 0.1f, "shut, the two leaves meet in the middle");

            // From the street: both swing in.
            Transform walker = Person(new Vector3(29.6f, 0.0f, 0.6f));
            Assert.IsTrue(pair[0].Bump(walker, 2.0f), "walking into the left leaf opens it");
            Assert.IsTrue(pair[1].IsOpen, "and its partner with it");
            yield return Seconds(1.2f);
            Assert.Less(FreeEndOf(pair[0], 1.2f).z, -0.8f, "the left leaf swung in, away from the street");
            Assert.Less(FreeEndOf(pair[1], 1.2f).z, -0.8f, "the right leaf swung in, away from the street");

            // Shut together, then opened from inside: both swing out.
            Object.Destroy(walker.gameObject);
            yield return null;
            Assert.IsTrue(pair[1].Interact(null), "E on the right leaf shuts the pair");
            yield return Seconds(1.2f);
            Assert.IsFalse(pair[0].IsOpen);
            Assert.Less(Vector3.Distance(FreeEndOf(pair[0], 1.2f), FreeEndOf(pair[1], 1.2f)), 0.1f, "shut again, they meet in the middle");
            Transform inside = Person(new Vector3(30.4f, 0.0f, -0.7f));
            Assert.IsTrue(pair[1].Bump(inside, 2.0f));
            yield return Seconds(1.2f);
            Assert.Greater(FreeEndOf(pair[0], 1.2f).z, 0.8f, "from inside the left leaf swings out");
            Assert.Greater(FreeEndOf(pair[1], 1.2f).z, 0.8f, "from inside the right leaf swings out");
        }

        [UnityTest]
        public IEnumerator APairRestoredOpenStandsSwungIntoTheBuilding()
        {
            BuildingDoor[] pair = Pair(startOpen: true);
            yield return null;
            Assert.IsTrue(pair[0].IsOpen && pair[1].IsOpen);
            Assert.Less(FreeEndOf(pair[0], 1.2f).z, -0.8f, "restored open, the left leaf stands swung in");
            Assert.Less(FreeEndOf(pair[1], 1.2f).z, -0.8f, "restored open, the right leaf stands swung in, not out over the street");
        }
    }
}
