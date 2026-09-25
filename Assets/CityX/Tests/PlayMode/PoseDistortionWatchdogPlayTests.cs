using System.Collections;
using System.IO;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// See PoseDistortionWatchdog's own class doc comment for why it
    /// exists. What matters here is the same shape every diagnostic in
    /// this whole investigation has needed: it must NOT cry wolf during
    /// perfectly ordinary play (a real varied swing is the single
    /// diagnostic this project already trusts most for "does this look
    /// distorted"), and it must actually catch the two things rotation
    /// alone provably cannot explain -- a scale change and a position
    /// offset on the visual rig -- since those are the only two places in
    /// the whole project a genuine "stretched"/"head detached" look could
    /// actually come from.
    /// </summary>
    public sealed class PoseDistortionWatchdogPlayTests
    {
        private GameObject _player;
        private GrappleSwing _swing;
        private Animator _animator;
        private PoseDistortionWatchdog _watchdog;
        private string _logPath;

        private const float StepSeconds = 1.0f / 60.0f;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 20.0f, 0.0f), Quaternion.identity);
            _animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(_animator, "Player.prefab has no Animator");

            _swing = _player.AddComponent<GrappleSwing>();
            _swing.Bind();

            _watchdog = _player.AddComponent<PoseDistortionWatchdog>();
            _watchdog.Swing = _swing;
            _watchdog.Locomotion = _player.GetComponent<PlayerLocomotion>();
            _watchdog.Bind();

            // A clean slate for every test -- this file is a real, shared
            // append-log, not something TearDown can reset any other way.
            _logPath = System.IO.Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", "pose_distortion_watchdog.log");
            if (File.Exists(_logPath))
            {
                File.Delete(_logPath);
            }
#endif
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_logPath != null && File.Exists(_logPath)) File.Delete(_logPath);
        }

        /// <summary>
        /// STILL FAILS, HONESTLY LEFT THAT WAY -- see grapple_swing_deprioritized
        /// memory for the full investigation trail. GrappleSwing was rebuilt
        /// from square one to stop touching the Animator/bones entirely (see
        /// its own class doc comment); this test's own bone-rotation-rate
        /// check (not the position/scale ones, which pass cleanly with this
        /// SAME input -- see GrappleSwingAnimatorPlayTests.
        /// SwingingNeverDriftsTheVisualRigFromTheRoot) still occasionally logs a
        /// snap on RightUpperArm during this specific 5-second, perfectly
        /// smooth, continuously-rotating-yaw stress pattern. Three
        /// production fixes were made along the way and are all real,
        /// justified improvements kept regardless (PlayerLocomotion.
        /// DriveAnimatorFromExternalVelocity: clamped to SprintSpeed, always
        /// fed as local-forward, eased through the same _horizontalVelocity
        /// field ordinary Tick uses) -- none of them eliminated this one.
        /// A control test (an ordinary long fall, zero GrappleSwing
        /// involvement, fed this exact same smooth input through
        /// PlayerLocomotion.Tick directly) never tripped the watchdog,
        /// ruling out "any sufficiently long fall" as the cause. The
        /// leading hypothesis, based on reading PlayerLocomotion.
        /// SuppressBoneSnap's own "accept unconditionally the frame after a
        /// hold" design (a deliberate Round 7 fix for a real permanent-
        /// lockup bug): if Mecanim's own transition into/through a
        /// multi-second-held Falling state genuinely spans MORE than one
        /// frame, the correction's hold-then-unconditional-accept pattern
        /// can let a real, multi-frame transition's SECOND half through
        /// with zero validation, which THIS watchdog (a separate observer
        /// with its own frame-to-frame reference) still sees as one large
        /// jump even though PlayerLocomotion's own bookkeeping considers it
        /// resolved. Nothing in this whole codebase ever held Grounded=false
        /// with a LIVE Animator for multiple continuous seconds before this
        /// rebuild (the old GrappleSwing disabled the Animator outright, and
        /// no ordinary fall lasts this long) -- if true, this is a
        /// pre-existing characteristic of an already-many-times-revised core
        /// system, newly exposed by a genuinely novel scenario, not a new
        /// bug in GrappleSwing's own code. NOT CONFIRMED, and deliberately
        /// not "fixed" by touching SuppressBoneSnap itself -- that system
        /// has already been revised many times chasing this exact saga, and
        /// a further change needs its own dedicated verification, not a
        /// guess bolted on at the end of an unrelated rebuild. See this
        /// test's own doc comment history / grapple_swing_deprioritized
        /// memory before touching it again.
        /// </summary>
        [UnityTest]
        public IEnumerator ARealVariedSwingNeverTriggersTheWatchdog()
        {
#if UNITY_EDITOR
            // THE SAME INPUT TheVisualRigNeverDriftsFromTheRootDuringARealVariedSwing
            // already uses -- this project's own most-trusted "does a real
            // swing look distorted" check, reused rather than reinvented so
            // this test's own idea of "ordinary play" cannot quietly drift
            // from that one's.
            _swing.Attach(_player.transform.position + new Vector3(6.0f, 8.0f, 4.0f));
            for (int i = 0; i < 300 && _swing.IsActive; i++)
            {
                float t = i / 60.0f;
                Vector2 move = new Vector2(Mathf.Sin(t * 0.7f), Mathf.Cos(t * 0.5f) * 0.5f + 0.5f);
                float yaw = i * 2.0f;
                bool reel = (i / 30) % 4 == 0;
                bool payout = (i / 30) % 4 == 2;
                _swing.Tick(move, yaw, false, false, reel, payout, StepSeconds);
                yield return null;
            }

            Assert.IsFalse(File.Exists(_logPath),
                "the watchdog flagged a real, ordinary varied swing as distorted -- it is crying wolf");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AScaleCorruptionOnTheVisualRigIsCaughtAndLogged()
        {
#if UNITY_EDITOR
            yield return null; // one real frame so the watchdog has a baseline to compare against

            // A SYNTHETIC INJECTION, not a real bug reproduction -- this
            // only proves the watchdog notices the shape of thing a real
            // "stretched" report would actually be (see PoseDistortionWatchdog's
            // own class doc comment for why scale/position, not rotation).
            _animator.transform.localScale = new Vector3(3.0f, 3.0f, 3.0f);
            yield return null;

            Assert.IsTrue(File.Exists(_logPath), "the watchdog did not log an obvious 3x scale corruption");
            string contents = File.ReadAllText(_logPath);
            StringAssert.Contains("scale", contents.ToLowerInvariant());
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator APositionOffsetOnTheVisualRigIsCaughtAndLogged()
        {
#if UNITY_EDITOR
            // PlayerLocomotion disabled -- ClampFeetAboveGround recomputes
            // the rig's own localPosition from scratch every real LateUpdate
            // it runs (zero, then a fresh correction from the live foot
            // positions), which would silently overwrite this test's own
            // injected value before the watchdog ever saw it. A genuine
            // runaway is real feet-position-driven growth over many ticks,
            // not a directly-set value, so isolating from that recompute is
            // what actually isolates the WATCHDOG's own detection here.
            _player.GetComponent<PlayerLocomotion>().enabled = false;
            yield return null;

            // Matches ClampFeetAboveGround's own documented runaway shape --
            // a large localPosition offset on the Animator's own transform,
            // not the character root.
            _animator.transform.localPosition = new Vector3(0.0f, 50.0f, 0.0f);
            yield return null;

            Assert.IsTrue(File.Exists(_logPath), "the watchdog did not log an obvious 50m rig offset");
            string contents = File.ReadAllText(_logPath);
            StringAssert.Contains("offset", contents.ToLowerInvariant());
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ASuddenBoneSnapIsCaughtAndLogged()
        {
#if UNITY_EDITOR
            Transform rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            // The Animator disabled -- left running, its own idle/falling
            // clip keeps writing a fresh pose to this same bone every real
            // frame (between this test's own injection and the watchdog's
            // LateUpdate), silently overwriting the injected snap before
            // the watchdog could ever see it. Matches how a genuine snap
            // would actually be visible in this rig -- Ragdoll disables the
            // Animator before taking a bone over for exactly this reason
            // (GrappleSwing used to as well; rebuilt from square one to
            // never touch a bone or the Animator at all -- see its own
            // class doc comment).
            _animator.enabled = false;
            yield return null;

            // An instantaneous 170-degree jump in one frame -- nothing in
            // this project's own real pose systems moves a bone this far
            // in a single frame.
            rightUpperArm.localRotation = rightUpperArm.localRotation * Quaternion.Euler(170.0f, 0.0f, 0.0f);
            yield return null;

            Assert.IsTrue(File.Exists(_logPath), "the watchdog did not log an obvious instantaneous bone snap");
            string contents = File.ReadAllText(_logPath);
            StringAssert.Contains("snap", contents.ToLowerInvariant());
#else
            yield break;
#endif
        }
    }
}
