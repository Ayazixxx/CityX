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
            // would actually be visible in this rig -- GrappleSwing/
            // Ragdoll both disable the Animator before taking a bone over
            // for exactly this reason.
            _animator.enabled = false;
            yield return null;

            // An instantaneous 170-degree jump in one frame -- nothing in
            // this project's own real pose systems moves a bone this far
            // in a single frame (GrappleSwing's own reach eases at 240
            // degrees per SECOND).
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
