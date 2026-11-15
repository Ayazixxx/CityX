using System.Collections;
using System.Reflection;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The reported fix: a dead-centre orbit camera puts anything small and
    /// close directly in front of the player -- a dropped pistol on the
    /// ground being the concrete case -- right behind the character's own
    /// silhouette. ShoulderOffset shifts the camera to one side, Fortnite/
    /// GTA-style, without changing what it looks at.
    /// </summary>
    public sealed class ThirdPersonCameraPlayTests
    {
        private GameObject _target;
        private GameObject _cameraObject;
        private ThirdPersonCamera _camera;

        [SetUp]
        public void SetUp()
        {
            _target = new GameObject("Target");
            _cameraObject = new GameObject("Camera");
            _camera = _cameraObject.AddComponent<ThirdPersonCamera>();
            _camera.Target = _target.transform;
            _camera.Yaw = 0.0f;
            _camera.Pitch = 0.0f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_target);
            Object.Destroy(_cameraObject);
        }

        [UnityTest]
        public IEnumerator TheCameraSitsToTheRightOfTheSpineRatherThanDeadCentre()
        {
            yield return null; // let LateUpdate place it once

            // Facing straight along +Z with no pitch: "to the right of the
            // spine" is a positive world-space X, and the camera should still
            // sit behind the target along Z either way.
            Assert.Greater(_cameraObject.transform.position.x, 0.05f,
                "the camera should be offset to the right of the player's own centreline");
            Assert.Less(_cameraObject.transform.position.z, _target.transform.position.z,
                "the camera should still be behind the player, offset aside notwithstanding");
        }

        [UnityTest]
        public IEnumerator TheOffsetDoesNotChangeWhereTheCameraIsActuallyLooking()
        {
            yield return null;

            // Rotation is set directly from pitch/yaw, independent of where
            // the camera's own position sits -- the offset must never leak
            // into aim, since PistolWeapon/BottleWeapon's own look-direction
            // gate reads this transform's forward directly.
            float angle = Quaternion.Angle(Quaternion.identity, _cameraObject.transform.rotation);
            Assert.Less(angle, 0.01f, "the shoulder offset should never rotate the camera off pitch/yaw");
        }

        /// <summary>
        /// THE FRAMING IS SAINTS ROW: THE THIRD'S (owner, 2026-10-23: "research the saints row the third camera pov and zoom and just copy
        /// that"): at the pitch the camera starts at, the top of the head sits at the middle of the screen, the body fills the lower half and
        /// sits left of the middle (the camera over the right shoulder), and someone as tall standing a few metres in front is seen over the
        /// head rather than behind it. Looking level at the chest, as the camera did until 2026-10-17, put the head above the middle, with
        /// whoever was in front hidden behind it.
        /// </summary>
        [UnityTest]
        public IEnumerator TheHeadSitsAtTheMiddleTheBodyFillsTheLowerHalfAndWhoeverIsAheadIsSeenOverIt()
        {
            var rig = new GameObject("FramingRig");
            Camera lens = rig.AddComponent<Camera>();
            lens.fieldOfView = ThirdPersonCamera.DefaultFieldOfView;
            ThirdPersonCamera follow = rig.AddComponent<ThirdPersonCamera>();
            follow.Target = _target.transform;
            follow.Yaw = 0.0f;
            follow.Pitch = ThirdPersonCamera.DefaultPitch;
            try
            {
                yield return null;

                const float height = 1.8f;   // what the camera assumes for a body without PlayerLocomotion
                Vector3 feet = _target.transform.position;
                Vector3 head = lens.WorldToViewportPoint(feet + Vector3.up * height);
                Vector3 soles = lens.WorldToViewportPoint(feet);
                Assert.That(head.y, Is.InRange(0.42f, 0.52f), $"the top of the head is at {head.y:0.00} of the screen's height, not at the middle");
                Assert.That(soles.y, Is.InRange(0.03f, 0.25f), $"the feet are at {soles.y:0.00} of the screen's height: the body does not fill the lower half");
                Assert.Less(head.x, 0.45f, $"the body is at {head.x:0.00} of the screen's width: not left of the middle");
                Vector3 ahead = lens.WorldToViewportPoint(feet + Vector3.forward * 4.0f + Vector3.up * height);
                Assert.Greater(ahead.y, head.y + 0.05f, "the head of someone as tall 4 m in front is hidden behind the player's own");
            }
            finally
            {
                Object.Destroy(rig);
            }
        }

        /// <summary>Fine aim keeps its own framing: in close over the right shoulder, the camera's line down from the top of the head to the shoulders.</summary>
        [UnityTest]
        public IEnumerator AimingBringsTheCameraInCloseOverTheShoulder()
        {
            yield return null;
            float restHeight = _cameraObject.transform.position.y;
            float restDistance = Vector3.Distance(_target.transform.position, _cameraObject.transform.position);

            _camera.IsAiming = true;
            for (int i = 0; i < 300; i++)   // the camera takes 0.15 s to get there
            {
                yield return null;
            }

            const float height = 1.8f;
            Vector3 expected = _target.transform.position
                + Vector3.up * (_camera.PivotHeightFraction * height + _camera.AimPivotRaise)
                + Vector3.right * _camera.AimShoulderOffset
                - Vector3.forward * _camera.AimDistance;
            Vector3 position = _cameraObject.transform.position;
            Assert.Less(Vector3.Distance(expected, position), 0.01f, $"aiming put the camera at {position}, not its over-the-shoulder place {expected}");
            Assert.Less(position.y, restHeight - 0.1f, "aiming did not bring the camera's line down from the top of the head to the shoulders");
            Assert.Less(Vector3.Distance(_target.transform.position, position), restDistance * 0.7f, "aiming did not bring the camera in close");
        }

        /// <summary>Into fine aim in Saints Row's 0.15 s, back out over half a second: the snap in reads as readiness, the ease out as relief.</summary>
        [UnityTest]
        public IEnumerator AimingSnapsInAndEasesBackOut()
        {
            Time.captureFramerate = 60;
            try
            {
                yield return null;
                _camera.IsAiming = true;
                for (int i = 0; i < 12; i++)   // 0.2 s
                {
                    yield return null;
                }
                Assert.Greater(_camera.AimCameraBlend, 0.95f, $"0.2 s into aiming the camera is only {_camera.AimCameraBlend:0.00} of the way in");
                _camera.IsAiming = false;
                for (int i = 0; i < 12; i++)
                {
                    yield return null;
                }
                Assert.Greater(_camera.AimCameraBlend, 0.2f, $"0.2 s after releasing aim the camera is already {1.0f - _camera.AimCameraBlend:P0} of the way out: no ease");
                for (int i = 0; i < 30; i++)   // 0.7 s in all
                {
                    yield return null;
                }
                Assert.Less(_camera.AimCameraBlend, 0.001f, "0.7 s after releasing aim the camera is not back");
            }
            finally
            {
                Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            }
        }

        /// <summary>
        /// A sprint widens the view a tenth over half a second (Saints Row's camera has its own sprint entry, which its first-person mod mirrors
        /// with a 1.1x field of view); aiming overrides it, and stopping restores the rest.
        /// </summary>
        [UnityTest]
        public IEnumerator SprintingWidensTheFieldOfViewATenthAndAimingOverridesIt()
        {
            var rig = new GameObject("SprintRig");
            Camera cam = rig.AddComponent<Camera>();
            cam.fieldOfView = 50.0f;
            ThirdPersonCamera follow = rig.AddComponent<ThirdPersonCamera>();
            follow.Target = _target.transform;
            Time.captureFramerate = 60;
            try
            {
                yield return null;
                follow.IsSprinting = true;
                for (int i = 0; i < 90; i++)   // 1.5 s, three times the blend
                {
                    yield return null;
                }
                Assert.AreEqual(50.0f * follow.SprintFieldOfViewScale, cam.fieldOfView, 0.5f, "sprinting should widen the field of view a tenth");
                follow.IsAiming = true;
                for (int i = 0; i < 90; i++)
                {
                    yield return null;
                }
                Assert.AreEqual(follow.AimFieldOfView, cam.fieldOfView, 0.5f, "aiming while sprinting should still zoom to the aim field of view");
                follow.IsAiming = false;
                follow.IsSprinting = false;
                for (int i = 0; i < 90; i++)
                {
                    yield return null;
                }
                Assert.AreEqual(50.0f, cam.fieldOfView, 0.5f, "stopping should restore the rest field of view");
            }
            finally
            {
                Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
                Object.Destroy(rig);
            }
        }

        /// <summary>The impact shake (a hit taken or landed): the picture jolts, the aim does not, and the camera ends up exactly where it was.</summary>
        [UnityTest]
        public IEnumerator AnImpactShakeJoltsThePictureAndSettlesBackExactlyWhereItWas()
        {
            yield return null;
            Vector3 rest = _cameraObject.transform.position;
            Quaternion aim = _cameraObject.transform.rotation;

            _camera.AddShake(0.1f, 0.25f);
            float biggest = 0.0f;
            for (int i = 0; i < 6; i++)
            {
                yield return null;
                biggest = Mathf.Max(biggest, Vector3.Distance(_cameraObject.transform.position, rest));
                Assert.Less(Quaternion.Angle(aim, _cameraObject.transform.rotation), 0.01f, "the shake turned the camera: it must only move where the picture is taken from");
            }
            Assert.Greater(biggest, 0.02f, "the shake never moved the picture");
            Assert.LessOrEqual(biggest, 0.1f + 0.001f, "the shake went past its own amplitude");

            float waited = 0.0f;
            while (waited < 0.4f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.Less(Vector3.Distance(_cameraObject.transform.position, rest), 1e-4f, "the shake did not settle back exactly");
            Assert.AreEqual(Vector3.zero, _camera.ShakeOffset);
        }

        [UnityTest]
        public IEnumerator ASmallerShakeNeverCutsABiggerOneShortAndTheLocalCameraIsKnown()
        {
            yield return null;
            Assert.AreEqual(_camera, ThirdPersonCamera.Local, "the enabled follow camera should be the one hits ask to shake");
            _camera.AddShake(0.1f, 0.3f);
            yield return null;
            _camera.AddShake(0.01f, 0.3f);   // a flurry's small jolt on top of a big one: ignored
            float biggest = 0.0f;
            Vector3 rest = _target.transform.position + Vector3.up * (_camera.PivotHeightFraction * 1.8f + _camera.FramingHeight)
                + Vector3.right * _camera.ShoulderOffset - Vector3.forward * _camera.Distance;
            for (int i = 0; i < 4; i++)
            {
                yield return null;
                biggest = Mathf.Max(biggest, Vector3.Distance(_cameraObject.transform.position, rest));
            }
            Assert.Greater(biggest, 0.02f, "the big shake was cut short by the small one");
        }

        /// <summary>Stands in for the Wraith: something the camera goes through as if it were air.</summary>
        private sealed class AirToTheCamera : MonoBehaviour, ICameraPassesThrough
        {
        }

        /// <summary>
        /// Owner, 2026-11-10: "make the ingame camera move through the wraith as if it were air, not an object that would switch the camera
        /// angle if it blocks the view". Something marked so, between the camera and the body, leaves the camera where it was; a block there
        /// still brings it in.
        /// </summary>
        [UnityTest]
        public IEnumerator TheCameraGoesThroughTheWraithButNotThroughAWall()
        {
            yield return null;
            float clear = Vector3.Distance(_cameraObject.transform.position, _target.transform.position);
            var wraith = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            wraith.transform.position = _target.transform.position - Vector3.forward * 2.0f + Vector3.up * 1.5f;
            wraith.transform.localScale = new Vector3(2.0f, 2.0f, 2.0f);
            try
            {
                wraith.AddComponent<AirToTheCamera>();
                yield return null;
                yield return null;
                Assert.AreEqual(clear, Vector3.Distance(_cameraObject.transform.position, _target.transform.position), 0.01f,
                    "the camera came in front of the Wraith");

                Object.DestroyImmediate(wraith.GetComponent<AirToTheCamera>());
                yield return null;
                yield return null;
                Assert.Less(Vector3.Distance(_cameraObject.transform.position, _target.transform.position), clear - 0.3f,
                    "setup: the same shape, unmarked, does not bring the camera in, so this shows nothing");
            }
            finally
            {
                Object.Destroy(wraith);
            }
        }

        /// <summary>Stands in for an enemy, a civilian or another player: a body that can be hurt.</summary>
        private sealed class ABody : MonoBehaviour, IDamageable
        {
            public bool IsAlive => true;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds) => default;
        }

        /// <summary>
        /// Owner, 2026-11-15: "make it so the camera ignores npcs and enemies right now it get caught on them and then the camera gets zoomed
        /// into an angle i dont want". A body between the camera and the player leaves the camera where it was.
        /// </summary>
        [UnityTest]
        public IEnumerator TheCameraGoesThroughEnemiesAndNpcs()
        {
            yield return null;
            float clear = Vector3.Distance(_cameraObject.transform.position, _target.transform.position);
            var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.transform.position = _target.transform.position - Vector3.forward * 2.0f + Vector3.up * 1.5f;
            enemy.transform.localScale = new Vector3(2.0f, 2.0f, 2.0f);
            try
            {
                enemy.AddComponent<ABody>();
                yield return null;
                yield return null;
                Assert.AreEqual(clear, Vector3.Distance(_cameraObject.transform.position, _target.transform.position), 0.01f,
                    "the camera came in front of the enemy");
            }
            finally
            {
                Object.Destroy(enemy);
            }
        }

        /// <summary>
        /// THE ACTUAL BUG this exists to catch: a wall right on top of the
        /// pivot used to clamp the camera down to its old 0.4m minimum,
        /// which combined with the camera's own 0.15m near clip plane left
        /// almost no headroom before nearby geometry (or the grapple
        /// swing's own outstretched reach pose) ended up partly inside the
        /// lens -- a live bug report described this as the character
        /// "getting distorted to lengths that shouldn't be possible" while
        /// the camera itself was the thing sitting somewhere absurd, close
        /// enough to warp or clip whatever was in view.
        /// </summary>
        [UnityTest]
        public IEnumerator NeverPullsCloserThanMinDistanceEvenAgainstAWallRightOnTopOfThePivot()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = _target.transform.position - Vector3.forward * 0.5f;
            wall.transform.localScale = new Vector3(20.0f, 20.0f, 0.5f);
            try
            {
                yield return null;

                const float fallbackHeight = 1.8f;
                Vector3 pivot = _target.transform.position +
                    Vector3.up * (_camera.PivotHeightFraction * fallbackHeight);
                float actualDistance = Vector3.Distance(_cameraObject.transform.position, pivot);
                Assert.GreaterOrEqual(actualDistance, _camera.MinDistance - 0.01f,
                    $"the camera sat {actualDistance:0.00}m from the pivot against a wall right on top " +
                    "of it -- closer than its own MinDistance, exactly the near-clip distortion this guards against");
            }
            finally
            {
                Object.Destroy(wall);
            }
        }

        /// <summary>
        /// Bug report, root-caused directly in CityMassingSceneBuilder:
        /// the real gameplay camera GameObject is first positioned by the
        /// FlyCamera editor preview (a street-level walking view, nowhere near
        /// "4.5m behind the shoulder") and ThirdPersonCamera is added
        /// afterward without ever repositioning it -- so before this fix, the
        /// camera only ever moved to its real position inside LateUpdate,
        /// leaving it parked at that leftover transform for however many
        /// frames pass before LateUpdate first runs. Ordinarily that is one
        /// invisible frame. DevConsoleView.Open() (pressed immediately on
        /// spawning in, to gear up before playing) disables this component
        /// from its own Update -- which runs BEFORE LateUpdate in the same
        /// frame -- so LateUpdate never ran even once, and the camera stayed
        /// stuck rendering from a point coincident with the player's own
        /// spawn, facing wherever the editor preview last looked: a
        /// point-blank, warped close-up of the player's own body. Testing
        /// Start() directly via reflection rather than yielding a frame,
        /// since yielding lets BOTH Start and LateUpdate run either way and
        /// would pass even without this fix -- what actually matters is that
        /// Start's own positioning already lands correctly before any
        /// Update/LateUpdate has had a chance to run at all.
        /// </summary>
        [Test]
        public void StartPositionsTheCameraImmediatelyRatherThanWaitingForLateUpdate()
        {
            // Deliberately left far from Target and facing a random direction
            // -- exactly what a leftover FlyCamera transform looks like next
            // to a player spawned somewhere else entirely.
            _cameraObject.transform.position = _target.transform.position + new Vector3(500.0f, 500.0f, 500.0f);
            _cameraObject.transform.rotation = Quaternion.Euler(4.0f, 35.0f, 0.0f);

            MethodInfo start = typeof(ThirdPersonCamera).GetMethod(
                "Start", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(start, "ThirdPersonCamera should still declare a private Start()");
            start.Invoke(_camera, null);

            float distance = Vector3.Distance(_cameraObject.transform.position, _target.transform.position);
            Assert.Less(distance, _camera.Distance + _camera.ShoulderOffset + 1.0f,
                $"Start() left the camera {distance:0.0}m from the target -- it should already be " +
                "at roughly its normal orbit distance before LateUpdate ever runs, not stuck at " +
                "whatever raw transform it was created with");
        }

        /// <summary>
        /// THE AIM-IN FEATURE ITSELF: PlayerInputReader sets IsAiming true
        /// for as long as right-click is held on an equipped gun (or a
        /// melee weapon's own WeaponAimInput.IsAiming), and this component's
        /// only job is to react to that flag by narrowing the field of view
        /// -- it never reads the mouse or an equipped item itself, so the
        /// isolated behaviour to prove here is just the zoom in and back out.
        /// </summary>
        [UnityTest]
        public IEnumerator AimingNarrowsTheFieldOfViewAndReleasingRestoresIt()
        {
            // A DEDICATED OBJECT, CAMERA ADDED FIRST -- matching
            // CityMassingSceneBuilder's own real order (Camera, then
            // ThirdPersonCamera on the same object). Start() reads
            // GetComponent<Camera>() once, on this component's own first
            // frame; adding Camera onto the shared _cameraObject from SetUp
            // AFTER ThirdPersonCamera was already on it risks that lookup
            // running before the Camera exists.
            var rig = new GameObject("AimRig");
            Camera cam = rig.AddComponent<Camera>();
            cam.fieldOfView = 60.0f;
            ThirdPersonCamera aimCamera = rig.AddComponent<ThirdPersonCamera>();
            aimCamera.Target = _target.transform;
            aimCamera.AimFieldOfView = 40.0f;
            aimCamera.AimZoomDegreesPerSecond = 90.0f;

            yield return null; // Start() captures the rest field of view here
            Assert.AreEqual(60.0f, cam.fieldOfView, 0.5f, "should start at rest, untouched");

            // MANY frames, not a handful -- batch mode runs uncapped at
            // roughly 2500fps (see PlayerLocomotionPlayTests's own comment
            // on the same gotcha), and LateUpdate steps by real
            // Time.deltaTime, so a short loop can finish while barely any
            // simulated time has actually passed. Mathf.MoveTowards clamps
            // exactly at the target once enough has accumulated, so a
            // generous frame count converges rather than overshooting.
            aimCamera.IsAiming = true;
            for (int step = 0; step < 3000; step++)
            {
                yield return null;
            }
            Assert.AreEqual(40.0f, cam.fieldOfView, 0.5f,
                "holding aim should narrow the field of view down to AimFieldOfView");

            aimCamera.IsAiming = false;
            for (int step = 0; step < 3000; step++)
            {
                yield return null;
            }
            Assert.AreEqual(60.0f, cam.fieldOfView, 0.5f,
                "releasing aim should widen the field of view back to whatever it started at");

            Object.Destroy(rig);
        }

        /// <summary>
        /// Requested: "make the camera follow the ragdoll when /sleep is
        /// used and the ragdoll is moved." Nothing moves the player's own
        /// root transform while limp (see Ragdoll's own doc comment: "only
        /// the individual Rigidbodies do"), so without this the camera
        /// would stay pinned at Target's frozen position while physics
        /// carries the actual body somewhere else entirely -- a slope
        /// slide, a push, a crash tumble.
        /// </summary>
        [UnityTest]
        public IEnumerator FollowsTheRagdollsTorsoInsteadOfTheFrozenTargetWhileLimp()
        {
            var torsoObject = new GameObject("Torso");
            torsoObject.transform.SetParent(_target.transform, false);
            Rigidbody torso = torsoObject.AddComponent<Rigidbody>();
            torsoObject.AddComponent<BoxCollider>();

            Ragdoll ragdoll = _target.AddComponent<Ragdoll>();
            ragdoll.Bodies = new[] { torso };
            ragdoll.Torso = torso;
            ragdoll.Bind();
            _camera.Ragdoll = ragdoll;

            ragdoll.Activate(Vector3.zero);

            // A limp body is interpolated between physics steps (Ragdoll.SetKinematic), and an interpolated body's transform is put back where physics says it is on the
            // next frame, undoing the move by hand below. This test stands in for physics, so it opts out.
            torso.interpolation = RigidbodyInterpolation.None;

            // Moved by hand rather than simulated -- physics owns the torso
            // now, and this stands in for wherever a real crash/push/slide
            // would have actually carried it, well away from the frozen root.
            torso.transform.position = _target.transform.position + new Vector3(6.0f, 0.0f, 9.0f);

            yield return null;

            float distanceToTorso = Vector3.Distance(
                _cameraObject.transform.position, torso.transform.position);
            float distanceToFrozenTarget = Vector3.Distance(
                _cameraObject.transform.position, _target.transform.position);
            Assert.Less(distanceToTorso, distanceToFrozenTarget,
                "the camera should have followed the ragdoll's own torso, not stayed near the frozen root");
            Assert.Less(distanceToTorso, _camera.Distance + _camera.ShoulderOffset + 1.0f,
                "the camera should be orbiting close to the torso itself, not merely closer by coincidence");
        }

        // --- third-person-shooter feel (docs/THIRD_PERSON_PISTOL.md) ---

        [UnityTest]
        public IEnumerator ARecoilKickRaisesTheViewThenSettlesBackToExactlyWhereYouWereAiming()
        {
            yield return null;
            float restPitch = _cameraObject.transform.eulerAngles.x;

            _camera.AddRecoil(3.0f, 0.0f);
            yield return null;
            float kickedPitch = Mathf.DeltaAngle(0.0f, _cameraObject.transform.eulerAngles.x);
            Assert.Less(kickedPitch, Mathf.DeltaAngle(0.0f, restPitch) - 1.0f,
                "a recoil kick did not raise the view (a raised view is a LOWER pitch angle)");

            float waited = 0.0f;
            while (waited < 1.5f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(0.0f, _camera.RecoilPitch, 0.01f, "the kick never fully settled");
            Assert.AreEqual(Mathf.DeltaAngle(0.0f, restPitch),
                Mathf.DeltaAngle(0.0f, _cameraObject.transform.eulerAngles.x), 0.05f,
                "recoil left the view somewhere other than where you were aiming");
            Assert.AreEqual(0.0f, _camera.Pitch, 0.0001f, "recoil was written into the real aim pitch");
        }

        [Test]
        public void ARapidBurstCannotKickTheCameraPastTheCap()
        {
            for (int i = 0; i < 50; i++)
            {
                _camera.AddRecoil(1.2f, 0.0f);
            }

            Assert.LessOrEqual(_camera.RecoilPitch, _camera.MaxRecoilPitchDegrees + 0.0001f);
        }

        [Test]
        public void ANegativeKickIsIgnoredSoTheViewNeverDipsDown()
        {
            _camera.AddRecoil(-5.0f, 0.0f);

            Assert.AreEqual(0.0f, _camera.RecoilPitch, 0.0001f);
        }

        [UnityTest]
        public IEnumerator SwappingShouldersMovesTheCameraToTheOtherSideOfTheSpine()
        {
            yield return null;
            Assert.Greater(_cameraObject.transform.position.x, 0.05f, "setup: should start over the right shoulder");
            Assert.AreEqual(1.0f, _camera.ShoulderSide);

            _camera.ToggleShoulder();
            float waited = 0.0f;
            while (waited < 1.0f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(-1.0f, _camera.ShoulderSide);
            Assert.Less(_cameraObject.transform.position.x, -0.05f,
                "the camera never crossed to the left of the player's centreline");
            Assert.Less(_cameraObject.transform.position.z, _target.transform.position.z,
                "swapping shoulders moved the camera off from behind the player");
        }

        [UnityTest]
        public IEnumerator SwappingBackReturnsToTheOriginalShoulder()
        {
            yield return null;
            Vector3 original = _cameraObject.transform.position;

            _camera.ToggleShoulder();
            yield return new WaitForSeconds(0.6f);
            _camera.ToggleShoulder();
            yield return new WaitForSeconds(0.6f);

            Assert.Less(Vector3.Distance(original, _cameraObject.transform.position), 0.02f);
        }
    }
}
