using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The sword spin (key 2, Link's Up Special) through the real input path in the real scene: charged and let go on the ground, climbing
    /// when pressed in the air, hitting in its Smash-shaped pattern, the pose of the arms, the landing lag, the once-per-airtime rule.
    /// The timeline itself is covered without any of that (SwordSpinMoveTests, MeleeCombatPlayTests).
    /// </summary>
    public sealed class SwordSpinRealInputPlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Keyboard _keyboard;

        private PlayerVitals _vitals;
        private PlayerLocomotion _locomotion;
        private MeleeAttacker _melee;
        private readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();

        private sealed class Target : MonoBehaviour, IDamageable, ILaunchable
        {
            public bool IsAlive => true;
            public int Hits;
            public int Launches;
            public Vector3 LastLaunch;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                Hits++;
                return HitResponse.Landed;
            }

            public void Launch(Vector3 velocity)
            {
                Launches++;
                LastLaunch = velocity;
            }
        }

        /// <summary>Samples the body and the sword once a frame, after everything that poses the arms has run.</summary>
        [DefaultExecutionOrder(30000)]
        private sealed class PoseProbe : MonoBehaviour
        {
            public ISwordBlade Blade;
            public Transform LeftHand;
            public Vector3 Right;
            public Vector3 Along;
            public Vector3 LeftHandOffset;
            public bool Sampled;

            private void LateUpdate()
            {
                Right = transform.right;
                Along = (Blade.BladeTipWorld - Blade.BladeBaseWorld).normalized;
                LeftHandOffset = LeftHand.position - transform.position;
                Sampled = true;
            }
        }

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();   // the reader's whole combat tick is skipped without one
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private IEnumerator LoadSceneWithASword()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            _vitals = Object.FindFirstObjectByType<PlayerVitals>();
            Assert.IsNotNull(_vitals, "no PlayerVitals in the real scene");
            _locomotion = _vitals.GetComponent<PlayerLocomotion>();
            _melee = _vitals.GetComponent<MeleeAttacker>();

            Inventory inventory = _vitals.GetComponent<Inventory>();
            inventory.AddItem(ItemRegistry.Sword, 1);
            Assert.IsTrue(inventory.RemoveFromStorage(ItemRegistry.Sword, 1, out ItemInstanceData data));
            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.Sword, _vitals.gameObject, data), "could not equip the sword");
            _vitals.InfiniteHealth = true;

            for (int i = 0; i < 400 && !_locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }
            Assert.IsTrue(_locomotion.IsGrounded, "setup failed -- the real player never settled on the ground");
#else
            yield break;
#endif
        }

        /// <summary>
        /// Points W (the movement's yaw source; the camera stays as it was) along the flattest open direction from where the body stands:
        /// 30 m with no building within 3 m and the least ground rising ahead (level or downhill).
        /// </summary>
        private IEnumerator FaceALevelRun()
        {
            CityWorld world = CurrentWorld.Instance;
            Assert.IsNotNull(world, "setup: no world");
            Vector3 from = _vitals.transform.position;
            float bestRise = float.MaxValue;
            Vector3 heading = Vector3.forward;
            for (int a = 0; a < 72; a++)
            {
                var dir = new Vector3(Mathf.Sin(a * 5.0f * Mathf.Deg2Rad), 0.0f, Mathf.Cos(a * 5.0f * Mathf.Deg2Rad));
                double h0 = world.Plan.GroundHeight(from.x, from.z);
                float rise = 0.0f;
                bool open = true;
                for (float run = 2.0f; run <= 30.0f && open; run += 2.0f)
                {
                    Vector3 p = from + dir * run;
                    rise = Mathf.Max(rise, (float)(world.Plan.GroundHeight(p.x, p.z) - h0));   // only ground coming UP to meet the jump matters
                    open = world.Plan.BuildingAt(p.x, p.z, 3.0) < 0;
                }
                if (open && rise < bestRise)
                {
                    bestRise = rise;
                    heading = dir;
                }
            }
            Assert.Less(bestRise, 0.4f, "setup: no level or downhill open direction from the spawn");
            var yaw = new GameObject("TestYaw");
            yaw.transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
            _vitals.GetComponent<PlayerInputReader>().YawSource = yaw.transform;
            yield return null;
        }

        private IEnumerator Seconds(float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Time.deltaTime)
            {
                yield return null;
            }
        }

        /// <summary>Taps Space and waits until the body is off the ground and at least <paramref name="height"/> metres above where it stood.</summary>
        private IEnumerator JumpToHeight(float groundY, float height)
        {
            Press(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.spaceKey);
            for (int i = 0; i < 2000 && _vitals.transform.position.y - groundY < height; i++)
            {
                yield return null;
            }
            Assert.Greater(_vitals.transform.position.y - groundY, height, "setup failed -- the jump never got that high");
        }

        private Target Dummy(Vector3 position)
        {
            var go = new GameObject("Target");
            go.transform.position = position;
            var body = go.AddComponent<CapsuleCollider>();
            body.height = 1.8f;
            body.radius = 0.4f;
            go.AddComponent<Target>();
            _made.Add(go);
            Physics.SyncTransforms();
            return go.GetComponent<Target>();
        }

        [UnityTest]
        public IEnumerator HoldingTwoOnTheGroundChargesAndLettingGoSpins()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword();

            Press(_keyboard.digit2Key);
            yield return null;
            yield return null;
            Assert.IsTrue(_melee.IsChargingSpin, "holding 2 on the ground should charge the spin");
            Assert.IsFalse(_melee.IsSpinTurning);

            yield return Seconds(0.3f);
            Assert.IsTrue(_melee.IsChargingSpin, "still holding it");

            Release(_keyboard.digit2Key);
            yield return null;
            yield return null;
            Assert.IsTrue(_melee.IsSpinTurning, "letting go of 2 should start the spin");
            Assert.AreEqual(SpinKind.Ground, _melee.CurrentSpinKind);

            float elapsed = 0.0f;
            while (_melee.IsSpinAttacking && elapsed < 3.0f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.IsFalse(_melee.IsSpinAttacking, "the spin never handed the body back");
            Assert.AreEqual(SwordSpinMove.GroundCommitSeconds, elapsed, 0.2f, "a ground spin holds the body for 74 frames");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheSpinHoldsTheSwordArmOutToTheSideWithTheBladeAlongIt()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword();

            Press(_keyboard.digit2Key);
            yield return null;
            Release(_keyboard.digit2Key);
            yield return Seconds(0.3f);
            Assert.IsTrue(_melee.IsSpinTurning, "setup: should be mid-spin");

            // MEASURED AT THE END OF THE FRAME, by a probe that runs after the locomotion's LateUpdate (where the arms are placed). A
            // coroutine resumes after Update and BEFORE LateUpdate: by then the spin has already turned the body this frame but the arms
            // still carry last frame's pose, so on a slow frame the two differ by a frame's worth of turning (900 degrees a second).
            var blade = _melee.EquippedWeapon as ISwordBlade;
            Assert.IsNotNull(blade, "the equipped sword should describe its blade");
            var probe = _vitals.gameObject.AddComponent<PoseProbe>();
            probe.Blade = blade;
            probe.LeftHand = _vitals.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.LeftHand);
            yield return null;
            yield return null;
            Assert.IsTrue(probe.Sampled);
            Assert.Less(Vector3.Angle(probe.Along, probe.Right), 20.0f,
                $"the blade points {Vector3.Angle(probe.Along, probe.Right):0} degrees off straight out to the right " +
                $"(pose blend {_locomotion.SwordSpinPoseBlend:0.00}, spun for {_melee.SpinElapsedSeconds:0.00}s, kind {_melee.CurrentSpinKind})");
            Assert.Less(Mathf.Abs(probe.Along.y), 0.2f, "the blade should be level");
            Assert.Less(Vector3.Dot(probe.LeftHandOffset, probe.Right), -0.2f, "the free arm should be out the other side for balance");
            Assert.Greater(_locomotion.SwordSpinPoseBlend, 0.9f);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheSpinDrawsAGlowingTrailThatFadesAfter()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword();

            Press(_keyboard.digit2Key);
            yield return null;
            Release(_keyboard.digit2Key);
            yield return Seconds(0.3f);

            Transform made = _vitals.transform.Find("SwordSpinTrail");
            Assert.IsNotNull(made, "no swoosh drawn -- is the trail prefab on the player's MeleeAttacker?");
            var trail = made.GetComponent<SwordSpinTrail>();
            Assert.Greater(trail.Intensity, 0.5f, "the swoosh is not showing mid-spin");

            yield return Seconds(1.2f);
            Assert.Less(trail.Intensity, 0.05f, "the swoosh never died away");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator PressingTwoInTheAirClimbsFarHigherThanAJump()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword();

            float groundY = _vitals.transform.position.y;
            yield return JumpToHeight(groundY, 0.6f);

            Press(_keyboard.digit2Key);
            yield return null;
            Release(_keyboard.digit2Key);
            yield return null;
            Assert.IsTrue(_melee.IsSpinTurning, "2 in the air should start the spin at once");
            Assert.AreEqual(SpinKind.Air, _melee.CurrentSpinKind);

            float peak = 0.0f;
            for (float t = 0.0f; t < 3.0f; t += Time.deltaTime)
            {
                peak = Mathf.Max(peak, _vitals.transform.position.y - groundY);
                yield return null;
            }
            // A plain jump peaks at about a metre. The spin starts climbing at 7 m/s under 0.55 gravity: about 4 m more.
            Assert.Greater(peak, 3.5f, $"the aerial spin only reached {peak:0.0} m above the ground");
            Assert.Less(peak, 7.0f, $"the aerial spin went {peak:0.0} m up -- far past a recovery move");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheAirSpinHitsSeveralTimesAndItsLastBlowThrowsTargetsUp()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword();

            float groundY = _vitals.transform.position.y;
            yield return JumpToHeight(groundY, 0.6f);
            Vector3 at = _vitals.transform.position;
            // One beside the body (reached by the first blows as it climbs away) and one high above (reached by the last).
            Target beside = Dummy(at + new Vector3(1.5f, 1.2f, 0.0f));
            Target above = Dummy(at + new Vector3(0.0f, 5.6f, 0.0f));
            Assert.IsNotNull(beside);

            Press(_keyboard.digit2Key);
            yield return null;
            Release(_keyboard.digit2Key);
            yield return Seconds(1.6f);

            Assert.GreaterOrEqual(beside.Hits, 3, $"the target beside the body was hit {beside.Hits} time(s): a multi-hit spin hits more than that");
            Assert.GreaterOrEqual(above.Hits, 1, "the target at the top of the climb was never reached");
            Assert.AreEqual(1, above.Launches, "the last blow should throw it, once");
            Assert.Greater(above.LastLaunch.y, 5.0f, "thrown upward");
#else
            yield break;
#endif
        }

        /// <summary>
        /// REPORTED: "when jumping in the air and doing spin attack the character just kinda stops mid air after that attack, can you make
        /// the momentum of the movement keep flowing." Sprint, jump, let go of every key and spin: the body has to keep its speed through
        /// the spin AND after it, until it lands (where the ordinary ground rules take over again).
        /// </summary>
        [UnityTest]
        public IEnumerator TheBodyKeepsFlowingThroughTheAirSpinAndAfterIt()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword();
            // On level ground: the spawn of a city seed can face a hill (seed 61839025's rises 16% ahead), where the jump lands early on the slope,
            // the released keys brake the body on the ground, and the test would measure the hill, not the spin.
            yield return FaceALevelRun();

            float groundY = _vitals.transform.position.y;
            Vector3 startAt = _vitals.transform.position;
            // (One key event per frame: two queued in the same frame overwrite each other in the test device.)
            Press(_keyboard.wKey);
            yield return null;
            Press(_keyboard.leftShiftKey);
            yield return Seconds(0.8f);
            Assert.Greater(_locomotion.CurrentSpeed, 6.0f,
                $"setup: the body should be at a sprint (W down {_keyboard.wKey.isPressed}, moved {(_vitals.transform.position - startAt).magnitude:0.00} m, " +
                $"stamina {_vitals.Stamina:0.0}, sprinting {_locomotion.IsSprinting}, swinging {_melee.IsSwinging})");

            groundY = _vitals.transform.position.y;   // the sprint may have run down a slope: the jump is from here
            yield return JumpToHeight(groundY, 0.6f);
            Press(_keyboard.digit2Key);
            yield return null;
            Release(_keyboard.digit2Key);
            yield return null;
            Assert.IsTrue(_melee.IsSpinTurning, "setup: the aerial spin should be under way");
            float flowing = _locomotion.CurrentSpeed;
            Assert.Greater(flowing, 5.5f, "the spin should start with the sprint's momentum, not slow to a half-pace walk");

            // Now let go of every key: nothing asks the body to move any more.
            Release(_keyboard.wKey);
            yield return null;
            Release(_keyboard.leftShiftKey);
            yield return null;

            yield return Seconds(0.6f);
            Vector3 body = _vitals.transform.position;
            Vector3 ahead = new Vector3(_vitals.transform.forward.x, 0.0f, _vitals.transform.forward.z).normalized;
            string blocker = Physics.Raycast(body + Vector3.up * 1.3f, ahead, out RaycastHit block, 3.0f, ~0, QueryTriggerInteraction.Ignore)
                ? $"{block.collider.name} {block.distance:0.00} m ahead" : "nothing ahead";
            foreach (Collider near in Physics.OverlapCapsule(body + Vector3.up * 0.2f, body + Vector3.up * 2.4f, 0.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!near.transform.IsChildOf(_vitals.transform)) blocker += $"; touching {near.name} ({near.GetType().Name}) at {near.ClosestPoint(body + Vector3.up)}";
            }
            Assert.Greater(_locomotion.CurrentSpeed, flowing * 0.9f,
                $"the body braked while it was spinning (from {startAt} to {body}, {body.y - groundY:0.00} m up, grounded {_locomotion.IsGrounded}, {blocker})");

            float elapsed = 0.0f;
            while (_melee.IsSpinAttacking && elapsed < 3.0f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.IsFalse(_locomotion.IsGrounded, "setup: the spin should end with the body still in the air");
            Assert.Greater(_locomotion.CurrentSpeed, flowing * 0.85f, "the body stopped dead when the spin handed it back");

            yield return Seconds(0.15f);
            if (!_locomotion.IsGrounded)
            {
                Assert.Greater(_locomotion.CurrentSpeed, flowing * 0.75f, "the body lost its momentum in the air after the spin");
            }

            // Landed: the ordinary rules take it back, and with no key held it comes to rest.
            for (int i = 0; i < 4000 && !_locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            yield return Seconds(0.4f);
            Assert.Less(_locomotion.CurrentSpeed, 1.0f, "the momentum carried on after landing");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheAirSpinCanBeUsedOncePerTimeInTheAir()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword();
            _melee.SpinAttackCooldownSeconds = 0.0f;   // so only the once-per-airtime rule can refuse

            float groundY = _vitals.transform.position.y;
            yield return JumpToHeight(groundY, 0.6f);
            Press(_keyboard.digit2Key);
            yield return null;
            Release(_keyboard.digit2Key);
            yield return null;
            Assert.IsTrue(_melee.IsSpinTurning);

            float elapsed = 0.0f;
            while (_melee.IsSpinAttacking && elapsed < 3.0f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.IsFalse(_locomotion.IsGrounded, "setup: the spin should end with the body still in the air");

            Press(_keyboard.digit2Key);
            yield return null;
            Release(_keyboard.digit2Key);
            yield return null;
            Assert.IsFalse(_melee.IsSpinAttacking, "a second aerial spin in the same time off the ground");

            // Back on the ground it is available again.
            for (int i = 0; i < 4000 && !_locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            yield return Seconds(0.1f);
            Press(_keyboard.digit2Key);
            yield return null;
            Release(_keyboard.digit2Key);
            yield return null;
            yield return null;
            Assert.IsTrue(_melee.IsSpinAttacking, "the spin should work again once the body is back on the ground");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ComingDownBeforeTheAirSpinIsOverCostsThirtyFramesOfLanding()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword();

            // A low ceiling cuts the climb short, so the body is down again well before the spin's 91 frames are up.
            float groundY = _vitals.transform.position.y;
            var ceiling = new GameObject("Ceiling");
            ceiling.transform.position = _vitals.transform.position + new Vector3(0.0f, 4.5f, 0.0f);
            ceiling.AddComponent<BoxCollider>().size = new Vector3(20.0f, 1.0f, 20.0f);
            _made.Add(ceiling);
            Physics.SyncTransforms();

            yield return JumpToHeight(groundY, 0.6f);
            Press(_keyboard.digit2Key);
            yield return null;
            Release(_keyboard.digit2Key);
            yield return null;
            Assert.IsTrue(_melee.IsSpinTurning);

            // Until it is down.
            float airTime = 0.0f;
            while (_melee.IsSpinTurning && airTime < 3.0f)
            {
                airTime += Time.deltaTime;
                yield return null;
            }
            Assert.Less(airTime, SwordSpinMove.AirCommitSeconds - 0.1f, "setup: the body should be back on the ground before the spin is over");
            Assert.IsTrue(_melee.IsSpinAttacking, "landing mid-spin should leave the body held for the landing lag");

            float lag = 0.0f;
            while (_melee.IsSpinAttacking && lag < 2.0f)
            {
                lag += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(SwordSpinMove.AirLandingLagSeconds, lag, 0.15f, "landing lag is 30 frames");
#else
            yield break;
#endif
        }
    }
}
