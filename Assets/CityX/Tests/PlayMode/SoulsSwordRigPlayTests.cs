using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The soulslike sword on the REAL Player.prefab and the real Sword.prefab:
    /// the parts a bare GameObject cannot show -- that the Animator actually
    /// plays the swing at the fitted speed, that the fitted speed and clip
    /// offset are what the network is told, that the blade and the reach volume
    /// connect with a target standing where a swing would reach, and that the
    /// body is handed back to the sword idle at normal speed afterwards.
    ///
    /// Driven frame by frame with real Time.deltaTime, because the Animator's
    /// own clock is real time (see PlayerAimingPlayTests on the same point).
    /// </summary>
    public sealed class SoulsSwordRigPlayTests
    {
        private GameObject _ground;
        private GameObject _player;
        private GameObject _sword;
        private GameObject _dummyObject;
        private SparringDummy _dummy;
        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private SoulsSwordCombat _combat;
        private Animator _animator;
        private PlayerAnimationDriver _driver;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_sword != null) Object.Destroy(_sword);
            if (_dummyObject != null) Object.Destroy(_dummyObject);
            if (_ground != null) Object.Destroy(_ground);
        }

        private void Build()
        {
#if UNITY_EDITOR
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);

            var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var swordPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Sword.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");
            Assert.IsNotNull(swordPrefab, "Sword.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.1f, 0.0f), Quaternion.identity);
            // The prefab's own input reader would tick the combat a second
            // time per frame -- this test is the only driver.
            var reader = _player.GetComponent<PlayerInputReader>();
            if (reader != null)
            {
                reader.enabled = false;
            }
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _vitals = _player.GetComponent<PlayerVitals>();
            _animator = _player.GetComponentInChildren<Animator>();
            _driver = _player.GetComponent<PlayerAnimationDriver>();

            _sword = Object.Instantiate(swordPrefab, new Vector3(20.0f, 0.5f, 20.0f), Quaternion.identity);
            _sword.GetComponent<SwordWeapon>().OnEquip(_player, CityX.Items.ItemInstanceData.None);
            _combat = _player.GetComponent<SoulsSwordCombat>();
            Assert.IsNotNull(_combat, "equipping the sword did not add the soulslike combat");

            _dummyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _dummyObject.transform.position = new Vector3(0.0f, 1.0f, 1.6f);
            _dummy = _dummyObject.AddComponent<SparringDummy>();
            _dummy.Reset();
            Physics.SyncTransforms();
#endif
        }

        private void StepOnce(float dt)
        {
            var idle = new LocomotionInput { SwordEquipped = true };
            _combat.Tick(dt);
            _vitals.TickStamina(false, dt);
            _locomotion.Tick(idle, dt);
        }

        private IEnumerator Frame(float seconds)
        {
            float elapsed = 0.0f;
            while (elapsed < seconds)
            {
                float dt = Time.deltaTime;
                StepOnce(dt);
                elapsed += dt;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ALightSwingPlaysItsClipAtTheFittedSpeedAndHitsATargetInFront()
        {
#if UNITY_EDITOR
            Build();
            yield return Frame(0.5f);   // settle into the sword idle

            SoulsAttack light = _combat.Light1;
            string playedState = null;
            float playedSpeed = 0.0f;
            float playedStart = -1.0f;
            _driver.TimedStatePlayed += (s, speed, start) =>
            {
                if (playedState == null)
                {
                    playedState = s;
                    playedSpeed = speed;
                    playedStart = start;
                }
            };

            _combat.RequestLight(0.0f);
            yield return Frame(0.05f);

            Assert.AreEqual(light.Animation, playedState, "the announced state is not the swing's clip");
            Assert.AreEqual(light.AnimatorSpeed, playedSpeed, 0.001f, "the announced speed is not the fitted one");
            Assert.AreEqual(light.StartSeconds, playedStart, 0.001f, "the announced clip offset is not the slice start");
            // The swing plays on the UPPER-BODY layer with its own speed multiplier, so
            // the legs (Animator.speed) keep their own pace under it.
            Assert.AreEqual(light.AnimatorSpeed, _animator.GetFloat("UpperSpeed"), 0.001f, "the swing is not playing at the fitted speed");
            Assert.AreEqual(1.0f, _animator.speed, 0.001f, "the swing sped up the WHOLE Animator, legs included");
            Assert.IsTrue(_driver.IsPlaying(light.Animation), "the Animator never entered the swing state");
            Assert.Greater(_driver.LayerFor(light.Animation), 0, "the swing is on the base layer -- it would freeze the legs");

            yield return Frame(light.Total + 0.3f);

            Assert.AreEqual(1, _dummy.TimesHit, "a swing at a target standing in front never connected");
            Assert.AreEqual(SoulsPhase.Idle, _combat.Phase);
            Assert.AreEqual(1.0f, _animator.speed, 0.001f, "the swing left the Animator running at a fitted speed");
            Assert.IsTrue(_driver.IsPlaying(SoulsSwordCombat.IdleStateName),
                "the arms were not handed back to the walk cycle");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheStrikeLandsInsideTheActiveWindowNotBeforeIt()
        {
#if UNITY_EDITOR
            Build();
            yield return Frame(0.5f);

            SoulsAttack light = _combat.Light1;
            _combat.RequestLight(0.0f);

            float elapsed = 0.0f;
            float hitAt = -1.0f;
            while (elapsed < light.Total + 0.3f)
            {
                // One tick per rendered frame, counted with the very dt it was
                // ticked with -- Frame() above can tick twice in one frame.
                float dt = Time.deltaTime;
                StepOnce(dt);
                elapsed += dt;
                yield return null;
                if (hitAt < 0.0f && _dummy.TimesHit > 0)
                {
                    hitAt = elapsed;
                }
            }

            Assert.Greater(hitAt, 0.0f, "never hit");
            Assert.GreaterOrEqual(hitAt, light.Startup - 0.05f, "hit before the wind-up finished");
            Assert.LessOrEqual(hitAt, light.Startup + light.Active + 0.1f, "hit after the active window closed");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheHeavyIsPlayedFasterThanItsOwnClipAndHandedBackAtNormalSpeed()
        {
#if UNITY_EDITOR
            Build();
            yield return Frame(0.5f);
            _vitals.InfiniteStamina = true;

            _combat.RequestHeavy(0.0f);
            yield return Frame(0.05f);
            Assert.Greater(_animator.GetFloat("UpperSpeed"), 1.2f, "the 4.8s heavy clip is not being sped up to fit its timing");
            Assert.AreEqual(1.0f, _animator.speed, 0.001f, "the heavy sped up the legs as well");

            yield return Frame(_combat.Heavy.Total + 0.4f);
            Assert.AreEqual(SoulsPhase.Idle, _combat.Phase);
            Assert.AreEqual(1.0f, _animator.speed, 0.001f);
            Assert.AreEqual(1, _dummy.TimesHit);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AHeldGuardStaysInTheGuardPoseAcrossSeveralClipLengths()
        {
#if UNITY_EDITOR
            Build();
            yield return Frame(0.5f);

            // 2.0s is four times the 0.5s guard clip: without the hold loop
            // the pose would drop back to idle after the first pass.
            float end = Time.time + 2.0f;
            int framesInGuard = 0;
            int frames = 0;
            var idle = new LocomotionInput { SwordEquipped = true };
            while (Time.time < end)
            {
                float dt = Time.deltaTime;
                _combat.SetBlockHeld(true);
                _combat.Tick(dt);
                _vitals.TickHitstun(dt);
                _locomotion.Tick(idle, dt);
                frames++;
                if (_driver.IsPlaying(SoulsSwordCombat.BlockStateName))
                {
                    framesInGuard++;
                }
                yield return null;
            }

            Assert.IsTrue(_vitals.IsBlocking);
            Assert.Greater(framesInGuard / (float)frames, 0.9f,
                "the guard pose kept dropping out while the button was held");

            _combat.SetBlockHeld(false);
            yield return Frame(0.4f);
            Assert.IsFalse(_vitals.IsBlocking);
            Assert.IsTrue(_driver.IsPlaying(SoulsSwordCombat.IdleStateName), "releasing the guard did not lower it");
#else
            yield break;
#endif
        }

        // --- THE LEGS KEEP WALKING UNDER THE SWORD ------------------------------

        private sealed class Travel
        {
            public float Feet;
            public float Hand;
            public float HipsHeightRange;
        }

        /// <summary>
        /// Steps the body walking forward for `seconds` (with `perFrame` run
        /// each frame first) and returns how far the feet and the right hand
        /// travelled, in the body's own space, plus how much the hips bobbed.
        /// </summary>
        private IEnumerator MeasureWalking(float seconds, System.Action<float> perFrame, Travel result)
        {
            var walk = new LocomotionInput { SwordEquipped = true, Move = new Vector2(0.0f, 1.0f) };
            Transform left = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform right = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform hips = _animator.GetBoneTransform(HumanBodyBones.Hips);

            Vector3 lastLeft = _player.transform.InverseTransformPoint(left.position);
            Vector3 lastRight = _player.transform.InverseTransformPoint(right.position);
            Vector3 lastHand = _player.transform.InverseTransformPoint(hand.position);
            float minHips = float.MaxValue;
            float maxHips = float.MinValue;

            float elapsed = 0.0f;
            while (elapsed < seconds)
            {
                float dt = Time.deltaTime;
                perFrame?.Invoke(dt);
                _combat.Tick(dt);
                _vitals.TickStamina(false, dt);
                _locomotion.Tick(walk, dt);
                yield return null;
                elapsed += dt;

                Vector3 l = _player.transform.InverseTransformPoint(left.position);
                Vector3 r = _player.transform.InverseTransformPoint(right.position);
                Vector3 h = _player.transform.InverseTransformPoint(hand.position);
                result.Feet += (l - lastLeft).magnitude + (r - lastRight).magnitude;
                result.Hand += (h - lastHand).magnitude;
                lastLeft = l;
                lastRight = r;
                lastHand = h;
                float hipsY = _player.transform.InverseTransformPoint(hips.position).y;
                minHips = Mathf.Min(minHips, hipsY);
                maxHips = Mathf.Max(maxHips, hipsY);
            }
            result.HipsHeightRange = maxHips - minHips;
        }

        /// <summary>
        /// EXPLICIT ASK: "make the legs walk and not freeze up during sword use
        /// too." The swings are full-body Mixamo clips; on the base layer they
        /// overwrote the legs with a planted lunge. They play on a masked
        /// upper-body layer now, so while you walk AND swing the feet must keep
        /// stepping while the hand swings.
        /// </summary>
        [UnityTest]
        public IEnumerator TheLegsKeepWalkingWhileTheSwordSwings()
        {
#if UNITY_EDITOR
            Build();
            _vitals.InfiniteStamina = true;
            yield return Frame(0.5f);

            // Baseline: walking with nothing swung.
            var plain = new Travel();
            yield return MeasureWalking(1.0f, null, plain);
            Assert.Greater(plain.Feet, 0.8f, $"setup: plain walking barely moved the feet ({plain.Feet:0.00})");

            // The same second of walking, with a heavy in the middle of it.
            var swinging = new Travel();
            bool thrown = false;
            yield return MeasureWalking(1.0f, dt =>
            {
                if (!thrown)
                {
                    thrown = true;
                    _combat.RequestHeavy(_player.transform.eulerAngles.y);
                }
            }, swinging);

            Assert.IsTrue(_combat.IsCommitted || swinging.Hand > 0.0f, "setup: the swing never started");
            Assert.Greater(swinging.Feet, plain.Feet * 0.6f,
                $"the legs froze during the swing: the feet travelled {swinging.Feet:0.00} against {plain.Feet:0.00} walking");
            Assert.Greater(swinging.Hand, plain.Hand * 2.0f,
                $"the sword hand did not actually swing ({swinging.Hand:0.00} against {plain.Hand:0.00} walking)");
            Assert.Less(swinging.HipsHeightRange, plain.HipsHeightRange + 0.25f,
                $"the swing dragged the hips about ({swinging.HipsHeightRange:0.00}m of height change against {plain.HipsHeightRange:0.00}m walking) -- the mask is letting the pelvis follow the clip");
#else
            yield break;
#endif
        }

        /// <summary>
        /// THE CONTROL FOR THE TEST ABOVE: the same swing played the OLD way, on
        /// the base layer, must show frozen legs by the very same measurement.
        /// If it does not, "the legs keep walking" above would be passing
        /// against a yardstick that cannot tell a frozen leg from a walking one.
        /// </summary>
        [UnityTest]
        public IEnumerator TheSameSwingOnTheBaseLayerFreezesTheLegsByTheSameMeasurement()
        {
#if UNITY_EDITOR
            Build();
            _vitals.InfiniteStamina = true;
            yield return Frame(0.5f);

            var plain = new Travel();
            yield return MeasureWalking(1.0f, null, plain);

            var oldWay = new Travel();
            bool thrown = false;
            yield return MeasureWalking(1.0f, dt =>
            {
                if (!thrown)
                {
                    thrown = true;
                    _animator.Update(0.0f);
                    _animator.CrossFadeInFixedTime("sword_charged_attack", 0.06f, 0);   // layer 0: full body
                }
            }, oldWay);

            Assert.Less(oldWay.Feet, plain.Feet * 0.6f,
                $"a full-body swing did NOT freeze the legs by this measurement ({oldWay.Feet:0.00} against {plain.Feet:0.00}) -- the yardstick cannot tell");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheLegsKeepWalkingWhileTheGuardIsUp()
        {
#if UNITY_EDITOR
            Build();
            _vitals.InfiniteStamina = true;
            yield return Frame(0.5f);

            var plain = new Travel();
            yield return MeasureWalking(1.0f, null, plain);

            var guarding = new Travel();
            yield return MeasureWalking(1.0f, dt => _combat.SetBlockHeld(true), guarding);

            Assert.IsTrue(_driver.IsPlaying(SoulsSwordCombat.BlockStateName), "setup: the guard never came up");
            Assert.Greater(guarding.Feet, plain.Feet * 0.6f,
                $"the legs froze under the raised guard: the feet travelled {guarding.Feet:0.00} against {plain.Feet:0.00}");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ARemoteCopyClampsAGarbledSpeedAndIgnoresAnUnknownState()
        {
#if UNITY_EDITOR
            Build();
            yield return Frame(0.2f);

            // The sword swings are UPPER-BODY states: their speed is the layer's
            // own UpperSpeed multiplier, and Animator.speed stays 1.
            _driver.PlayRemoteState("sword_attack", 999.0f, 0.0f);
            Assert.AreEqual(PlayerAnimationDriver.MaxRemoteSpeed, _animator.GetFloat("UpperSpeed"), 0.001f);

            _driver.PlayRemoteState("sword_attack", -5.0f, 0.0f);
            Assert.AreEqual(PlayerAnimationDriver.MinRemoteSpeed, _animator.GetFloat("UpperSpeed"), 0.001f);

            _animator.SetFloat("UpperSpeed", 1.0f);
            _driver.PlayRemoteState("NoSuchState", 3.0f, 0.0f);
            Assert.AreEqual(1.0f, _animator.GetFloat("UpperSpeed"), 0.001f, "an unknown state still changed the playback speed");
            Assert.AreEqual(1.0f, _animator.speed, 0.001f);

            _driver.PlayRemoteState("sword_attack", float.NaN, 0.0f);
            Assert.AreEqual(1.0f, _animator.GetFloat("UpperSpeed"), 0.001f, "a NaN speed got through");
#else
            yield break;
#endif
        }
    }
}
