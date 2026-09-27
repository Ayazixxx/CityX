using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Climbing on purpose. Reported: "i always find myself climbing when not intending, like when i'm running up
    /// stairs or trying to jump through a window, or even sometimes when running through a doorway."
    ///
    /// Two halves. <see cref="GrabIntent"/> is the ask (Space held a moment, a tap is a jump); <see cref="WallProbe"/>
    /// is whether what is in front is really a wall (a wide, tall, flat face -- not a jamb, a window frame or a stair).
    /// The fixtures are real colliders in front of a player at the origin facing +Z, each built the way the real
    /// thing is shaped: a doorway is two piers with a gap, a window is a wall with a hole in it, stairs are risers.
    /// </summary>
    public sealed class WallGrabIntentTests
    {
        private GameObject _player;
        private PlayerClimbing _climbing;
        private PlayerVitals _vitals;
        private readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Player");
            _player.AddComponent<CharacterController>();
            _vitals = _player.AddComponent<PlayerVitals>();
            _climbing = _player.AddComponent<PlayerClimbing>();
            _climbing.Bind();
            _player.transform.position = Vector3.zero;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_player);
            foreach (GameObject go in _made)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _made.Clear();
        }

        private GameObject Box(string name, Vector3 centre, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.position = centre;
            go.AddComponent<BoxCollider>().size = size;
            _made.Add(go);
            return go;
        }

        /// <summary>A wall face at z = <paramref name="faceZ"/>, thick 1m, of the given width and height.</summary>
        private void Wall(float faceZ, float width, float height, float centreX = 0.0f)
        {
            Box("Wall", new Vector3(centreX, height * 0.5f, faceZ + 0.5f), new Vector3(width, height, 1.0f));
        }

        private bool Probe()
        {
            Physics.SyncTransforms();
            return WallProbe.TryFind(_player.transform.position, Vector3.forward, BodyRig.StandingHeight, out _);
        }

        // ---- the ask ----

        [Test]
        public void ATapIsAJumpAndNotAGrab()
        {
            var intent = new GrabIntent();
            intent.Update(true, 0.08f);   // a quick tap
            Assert.IsFalse(intent.Ready);
            intent.Update(false, 0.016f);
            Assert.IsFalse(intent.Ready);
        }

        [Test]
        public void HoldingTheKeyLongEnoughIsAGrab()
        {
            var intent = new GrabIntent();
            float held = 0.0f;
            while (held < GrabIntent.HoldSeconds - 0.001f)
            {
                intent.Update(true, 0.016f);
                held += 0.016f;
            }
            intent.Update(true, 0.016f);
            Assert.IsTrue(intent.Ready);
        }

        [Test]
        public void OneHoldOnlyGrabsOnceAndThenNeedsLettingGoOf()
        {
            GrabIntent intent = GrabIntent.AlreadyHeld();
            Assert.IsTrue(intent.Ready);

            intent.Consume();
            Assert.IsFalse(intent.Ready);
            intent.Update(true, 5.0f);
            Assert.IsFalse(intent.Ready, "holding on after the grab must not queue another");

            intent.Update(false, 0.016f);
            intent.Update(true, GrabIntent.HoldSeconds + 0.01f);
            Assert.IsTrue(intent.Ready, "a fresh hold after letting go is a new ask");
        }

        [Test]
        public void ACooldownStopsAHopOffBeingUndoneByTheNextFrame()
        {
            var intent = new GrabIntent();
            intent.BeginCooldown();
            intent.Update(false, 0.016f);
            intent.Update(true, GrabIntent.HoldSeconds + 0.01f);
            Assert.IsFalse(intent.Ready, "grabbed again straight after letting go");

            intent.Update(false, GrabIntent.CooldownSeconds);
            intent.Update(true, GrabIntent.HoldSeconds + 0.01f);
            Assert.IsTrue(intent.Ready, "the cooldown never ends");
        }

        [Test]
        public void RunningIntoAWallWithoutAskingDoesNotClimb()
        {
            Wall(0.2f, 30.0f, 30.0f);
            Physics.SyncTransforms();

            Assert.IsFalse(_climbing.TryStart(new GrabIntent(), forwardHeld: true, stamina: 100.0f));
            Assert.IsFalse(_climbing.IsClimbing);
        }

        // ---- what counts as a wall ----

        [Test]
        public void ABigFlatWallCounts()
        {
            Wall(0.5f, 12.0f, 20.0f);
            Assert.IsTrue(Probe());
        }

        [Test]
        public void ADoorwayDoesNot()
        {
            // Two piers with a 1.4m gap between them, the player walking through the gap.
            Box("PierLeft", new Vector3(-2.2f, 10.0f, 0.7f), new Vector3(3.0f, 20.0f, 1.0f));
            Box("PierRight", new Vector3(2.2f, 10.0f, 0.7f), new Vector3(3.0f, 20.0f, 1.0f));
            Physics.SyncTransforms();
            // The gap is 1.4m; the rays sit 0.35m either side of the centre, inside it: nothing to hit, so no wall.
            Assert.IsFalse(WallProbe.TryFind(_player.transform.position, Vector3.forward, BodyRig.StandingHeight, out _),
                "a doorway was taken for a wall");
        }

        [Test]
        public void TheJambBesideADoorwayDoesNotEither()
        {
            // Walking through a doorway and brushing its side: the player is beside the gap, facing the pier corner.
            Box("Pier", new Vector3(0.6f, 10.0f, 0.7f), new Vector3(0.6f, 20.0f, 1.0f));   // 0.6m wide: a pier, not a wall
            Physics.SyncTransforms();
            Assert.IsFalse(WallProbe.TryFind(_player.transform.position, Vector3.forward, BodyRig.StandingHeight, out _),
                "a narrow jamb was taken for a wall");
        }

        [Test]
        public void AWindowOpeningDoesNot()
        {
            // A wall with a window cut out of it at knee-to-above-head height, the player facing the opening.
            Box("Sill", new Vector3(0.0f, 0.4f, 0.7f), new Vector3(10.0f, 0.8f, 1.0f));
            Box("Header", new Vector3(0.0f, 5.0f, 0.7f), new Vector3(10.0f, 6.4f, 1.0f));   // from 1.8m up
            Physics.SyncTransforms();
            Assert.IsFalse(WallProbe.TryFind(_player.transform.position, Vector3.forward, BodyRig.StandingHeight, out _),
                "a window opening was taken for a wall");
        }

        [Test]
        public void TheRisersOfAStairDoNot()
        {
            // Risers no higher than a stair step, then open air above: a chest-high ray finds nothing.
            for (int i = 0; i < 4; i++)
            {
                Box("Riser", new Vector3(0.0f, 0.2f + 0.4f * i, 0.5f + 0.35f * i), new Vector3(3.0f, 0.4f, 0.7f));
            }
            Physics.SyncTransforms();
            Assert.IsFalse(WallProbe.TryFind(_player.transform.position, Vector3.forward, BodyRig.StandingHeight, out _),
                "a flight of stairs was taken for a wall");
        }

        [Test]
        public void ALowFenceDoesNot()
        {
            Wall(0.5f, 12.0f, 1.0f);
            Assert.IsFalse(Probe());
        }

        [Test]
        public void ACornerDoesNot()
        {
            // An OUTSIDE corner: two walls meeting at 90 degrees in an edge that points at the player, who is
            // facing that edge. (Corner edge at x=0, z=0.6; each wall runs back and away at 45 degrees.)
            GameObject left = Box("WallLeft", new Vector3(-1.41f, 10.0f, 0.6f + 1.41f), new Vector3(4.0f, 20.0f, 0.4f));
            left.transform.rotation = Quaternion.Euler(0.0f, -135.0f, 0.0f);
            GameObject right = Box("WallRight", new Vector3(1.41f, 10.0f, 0.6f + 1.41f), new Vector3(4.0f, 20.0f, 0.4f));
            right.transform.rotation = Quaternion.Euler(0.0f, -45.0f, 0.0f);
            Physics.SyncTransforms();
            Assert.IsFalse(WallProbe.TryFind(_player.transform.position, Vector3.forward, BodyRig.StandingHeight, out _),
                "an outside corner was taken for one flat wall");
        }

        [Test]
        public void ADoorIsNotAWall()
        {
            GameObject door = Box("Door", new Vector3(0.0f, 5.0f, 0.7f), new Vector3(10.0f, 10.0f, 0.4f));
            door.AddComponent<FakeDoor>();
            Physics.SyncTransforms();
            Assert.IsFalse(WallProbe.TryFind(_player.transform.position, Vector3.forward, BodyRig.StandingHeight, out _));
        }

        [Test]
        public void AWallNotBeingFacedDoesNotCount()
        {
            Wall(0.5f, 12.0f, 20.0f);
            Physics.SyncTransforms();
            // The same wall, but the player stands at an angle to it: a wall 60 degrees off the facing is not one they are at.
            _player.transform.rotation = Quaternion.Euler(0.0f, 70.0f, 0.0f);
            Assert.IsFalse(WallProbe.TryFind(_player.transform.position, _player.transform.forward, BodyRig.StandingHeight, out _));
        }

        [Test]
        public void AClimbNeverStartsAtAWindowFrameEvenWithTheKeyHeld()
        {
            Box("Sill", new Vector3(0.0f, 0.4f, 0.7f), new Vector3(10.0f, 0.8f, 1.0f));
            Box("Header", new Vector3(0.0f, 5.0f, 0.7f), new Vector3(10.0f, 6.4f, 1.0f));
            Physics.SyncTransforms();

            Assert.IsFalse(_climbing.TryStart(GrabIntent.AlreadyHeld(), forwardHeld: true, stamina: 100.0f),
                "jumping through a window with Space held grabbed the frame");
        }

        [Test]
        public void GrabbingDrawsTheBodyOntoTheWallAndTurnsItToFaceIt()
        {
            _player.transform.position = new Vector3(0.0f, 0.0f, -0.4f);
            _player.transform.rotation = Quaternion.Euler(0.0f, 20.0f, 0.0f);   // grabbed a little off square
            Wall(0.3f, 12.0f, 30.0f);
            Physics.SyncTransforms();
            Assert.IsTrue(_climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f));

            for (int i = 0; i < 40; i++)
            {
                _climbing.Tick(false, false, false, _vitals, 1.0f / 60.0f);
            }

            float facing = Vector3.Angle(_player.transform.forward, Vector3.forward);
            Assert.Less(facing, 3.0f, "the body is not squared up to the wall it grabbed");
        }

        /// <summary>Stands in for a real door: anything that is an IOpenableDoor is not a wall.</summary>
        private sealed class FakeDoor : MonoBehaviour, CityX.World.IOpenableDoor
        {
            public Vector3 Position => transform.position;
            public void SetOpen(bool open) { }
        }
    }
}
