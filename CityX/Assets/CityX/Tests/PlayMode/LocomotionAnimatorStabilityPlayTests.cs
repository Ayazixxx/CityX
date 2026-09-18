using System.Collections;
using System.IO;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// EXPLICIT BUG REPORT, with real screenshots this time: "the console
    /// and grapple still wildly stretches the body" -- arms twisted into
    /// an X, arms flung up and out, the torso visibly spiralled, and
    /// (separately, per the user's own follow-up) the head sometimes
    /// separating far from the body. Confirmed by the user: happens at
    /// RANDOM, but is INFLUENCED BY MOVEMENT, and snaps back to normal
    /// INSTANTLY the moment the console closes or the grapple releases.
    ///
    /// WORKING THEORY, CONFIRMED ROOT CAUSE: both console-open and
    /// GrappleSwing.Attach disable the Animator the instant they fire,
    /// whatever pose it happens to be mid-evaluation on at that exact
    /// frame. If the player is walking/running when that happens, the
    /// locomotion blend tree could be mid-TRANSITION, and Mecanim's own
    /// cross-fade between two clips with very different arm positions can
    /// produce a real, if brief, nonsensical intermediate pose that
    /// normally flashes by invisibly and only becomes visible (and stays
    /// visible) because something froze the Animator exactly then. FIXED
    /// via PlayerLocomotion.SuppressImplausibleArmSnaps -- see its own doc
    /// comment for the full story.
    ///
    /// TESTED HERE WITHOUT CONSOLE OR GRAPPLE AT ALL -- drives realistic,
    /// changing input for real, with the Animator running completely live,
    /// while a real PoseDistortionWatchdog watches the whole time.
    ///
    /// MEASURED VIA PoseDistortionWatchdog ITSELF, NOT A CUSTOM BONE-DELTA
    /// LOOP -- EXPLICIT BUG, found live: an earlier version of this test
    /// read bone rotations directly from its own coroutine right after each
    /// Tick() call, trying to time exactly when the Animator's own
    /// per-frame write and PlayerLocomotion's own LateUpdate correction had
    /// both settled (a second `yield return null`, a real-elapsed-time
    /// divisor, then a Time.frameCount-based divisor -- each an attempt to
    /// fix the previous one's measurement error). Even the last of those
    /// still produced an IMPOSSIBLE result: a measured two-real-frame
    /// rotation that exceeded the sum of the two frames' own INDIVIDUAL
    /// deltas, each independently confirmed safe via a temporary
    /// Debug.Log placed directly inside PlayerLocomotion.SuppressBoneSnap.
    /// Cross-referencing exact Euler values between that temporary log and
    /// this test's own coroutine proved the coroutine's read was
    /// inconsistently lagged -- sometimes one frame behind the most recent
    /// LateUpdate, sometimes three -- i.e. there is no fixed, predictable
    /// offset between a `[UnityTest]` coroutine's resumption point and any
    /// particular MonoBehaviour's own LateUpdate call. Reusing
    /// PoseDistortionWatchdog sidesteps this entirely: it does its own
    /// snap detection from INSIDE a real MonoBehaviour LateUpdate -- the
    /// same mechanism, and the same per-frame Time.deltaTime, that
    /// PlayerLocomotion's own correction runs on -- with no coroutine
    /// resynchronisation involved. It is also already the trusted, passing
    /// pattern PoseDistortionWatchdogPlayTests.
    /// ARealVariedSwingNeverTriggersTheWatchdog uses for the exact same
    /// kind of claim ("ordinary play never trips the watchdog"), just
    /// applied to locomotion instead of a grapple swing.
    /// </summary>
    public sealed class LocomotionAnimatorStabilityPlayTests
    {
        private GameObject _player;
        private GameObject _ground;
        private Animator _animator;
        private PlayerLocomotion _locomotion;
        private string _logPath;

        private const float StepSeconds = 1.0f / 60.0f;

        [TearDown]
        public void TearDown()
        {
            // Always reset, pass or fail -- see this class's own doc
            // comment: this project's batch PlayMode runner executes real
            // frames far faster and less evenly than any real player ever
            // sees. Time.captureDeltaTime forces every frame's
            // Time.deltaTime -- which both the Animator's own internal
            // transition timers and PoseDistortionWatchdog's own detection
            // math advance on -- to a fixed, realistic value, so play here
            // proceeds at real gameplay pace instead of whatever tiny slice
            // of wall-clock time batch mode happened to take. Left set, it
            // would leak into every later test in the same batch run, so it
            // is unconditionally cleared here.
            Time.captureDeltaTime = 0.0f;
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
            if (_logPath != null && File.Exists(_logPath)) File.Delete(_logPath);
        }

        private void SpawnPlayer()
        {
#if UNITY_EDITOR
            // A REAL FLOOR -- EXPLICIT BUG, found live: the first version of
            // this test had no ground at all, so the CharacterController
            // free-fell for the whole test (hundreds of metres down, not even
            // deterministic run to run since real gravity over hundreds of
            // frames is sensitive to exact frame timing). Real gameplay
            // always has real city floor underfoot.
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "TestGround";
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);

            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 2.0f, 0.0f), Quaternion.identity);
            _animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(_animator, "Player.prefab has no Animator");
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            Assert.IsNotNull(_locomotion, "Player.prefab has no PlayerLocomotion");
            _locomotion.Bind();

            PoseDistortionWatchdog watchdog = _player.AddComponent<PoseDistortionWatchdog>();
            watchdog.Locomotion = _locomotion;
            watchdog.Bind();

            // A clean slate for every test -- this file is a real, shared
            // append-log, not something TearDown can reset any other way
            // (same idiom as PoseDistortionWatchdogPlayTests's own SetUp).
            _logPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", "pose_distortion_watchdog.log");
            if (File.Exists(_logPath))
            {
                File.Delete(_logPath);
            }
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
#endif
        }

        /// <summary>
        /// Real, changing input driven straight into PlayerLocomotion.Tick
        /// every frame -- direction flips, sprint/crouch/aim toggles, jumps
        /// -- specifically to force the locomotion blend tree through many
        /// real transitions (walk<->run, forward<->strafe<->backward,
        /// grounded<->airborne, aiming on/off). No console, no grapple, no
        /// artificial freezing -- the Animator runs completely live the
        /// whole time, with a real PoseDistortionWatchdog watching for
        /// anything that would actually be visible on screen.
        /// </summary>
        [UnityTest]
        public IEnumerator RealisticChangingMovementNeverSnapsAnArmOrHeadBoneOnItsOwn()
        {
            SpawnPlayer();
            Time.captureDeltaTime = StepSeconds;

            var rng = new System.Random(20260910);

            // Let the body actually land on the real ground first (avoids
            // measuring both the Awake-to-first-Update snap every rig has,
            // and the brief initial drop from the spawn height).
            for (int i = 0; i < 60 && !_locomotion.IsGrounded; i++)
            {
                _locomotion.Tick(default, StepSeconds);
                yield return null;
            }
            Assert.IsTrue(_locomotion.IsGrounded, "setup failed -- never actually landed on the test ground");

            for (int i = 0; i < 900; i++)
            {
                // Real, varied, occasionally abrupt input -- direction
                // reversals and sprint/crouch/aim toggles are exactly what
                // forces a blend tree through real transitions rather than
                // sitting parked in one steady state the whole time.
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float magnitude = (float)rng.NextDouble();
                var input = new LocomotionInput
                {
                    Move = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * magnitude,
                    YawDegrees = (float)(rng.NextDouble() * 360.0),
                    Sprint = rng.NextDouble() < 0.3,
                    Crouch = rng.NextDouble() < 0.15,
                    Aiming = rng.NextDouble() < 0.2,
                    Jump = rng.NextDouble() < 0.03,
                };

                _locomotion.Tick(input, StepSeconds);
                yield return null;
            }

            Assert.IsFalse(File.Exists(_logPath),
                "PoseDistortionWatchdog caught the locomotion Animator distorting an arm/head bone during " +
                "ordinary live movement, with nothing ever disabling it -- a real controller instability that " +
                "a console/grapple freeze would only be making visible, not causing. See " +
                "CityX/Logs/pose_distortion_watchdog.log for the exact snapshot.");
        }
    }
}
