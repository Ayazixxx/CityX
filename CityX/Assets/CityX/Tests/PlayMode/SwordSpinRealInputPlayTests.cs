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
            public Transform Chest;
            public Vector3 Right;
            public Vector3 Along;
            public Vector3 LeftHandOffset;
            public bool Sampled;

            /// <summary>While set, the lowest and highest the blade has pointed, as its rise (the up part of its direction).</summary>
            public bool Recording;
            public float LowestRise = float.MaxValue, HighestRise = float.MinValue;
            public float HighestAt;

            private void LateUpdate()
            {
                // The chest's own right: the body turns inside the spin's animation, its transform does not.
                Right = Chest.rotation * Vector3.right;
                Along = (Blade.BladeTipWorld - Blade.BladeBaseWorld).normalized;
                LeftHandOffset = LeftHand.position - Chest.position;
                Sampled = true;
                if (Recording)
                {
                    LowestRise = Mathf.Min(LowestRise, Along.y);
                    if (Along.y > HighestRise)
                    {
                        HighestRise = Along.y;
                        HighestAt = Time.time;
                    }
                }
            }
        }

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();   // the reader's whole combat tick is skipped without one
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
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

        private IEnumerator LoadSceneWithASword(string sword = ItemRegistry.SpikeSword)
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
            inventory.AddItem(sword, 1);
            Assert.IsTrue(inventory.RemoveFromStorage(sword, 1, out ItemInstanceData data));
            Assert.IsTrue(inventory.EquipItemId(sword, _vitals.gameObject, data), "could not equip the sword");
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
                    open = OpenRun.ClearAt(world, p);
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
            // Facing it already: a body turning toward where it runs curves round (MovementTurn), off the level line it was given.
            _vitals.transform.rotation = yaw.transform.rotation;
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
            yield return SpinsWithTheBladeLevelAlongTheArm(ItemRegistry.SpikeSword);
        }

        /// <summary>
        /// Owner, 2026-10-18: the spin's blade was "angled downward" with the combat sword (held in its own grip on a move posed for the
        /// two-handed one); every sword is held in the thrust grip the spin is posed with now.
        /// </summary>
        [UnityTest]
        public IEnumerator WithTheOneHandedSwordTheSpinsBladeIsLevelAlongTheArmToo()
        {
            yield return SpinsWithTheBladeLevelAlongTheArm(ItemRegistry.CombatSword);
        }

        private IEnumerator SpinsWithTheBladeLevelAlongTheArm(string sword)
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword(sword);

            Press(_keyboard.digit2Key);
            yield return null;
            Release(_keyboard.digit2Key);
            yield return Seconds(0.3f);
            Assert.IsTrue(_melee.IsSpinTurning, "setup: should be mid-spin");

            // MEASURED AT THE END OF THE FRAME, by a probe that runs after the locomotion's LateUpdate (where the animation is laid on the
            // body): a coroutine resumes BEFORE LateUpdate, while the bones still carry last frame's pose.
            var blade = _melee.EquippedWeapon as ISwordBlade;
            Assert.IsNotNull(blade, "the equipped sword should describe its blade");
            Animator animator = _vitals.GetComponentInChildren<Animator>();
            var probe = _vitals.gameObject.AddComponent<PoseProbe>();
            probe.Blade = blade;
            probe.LeftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            probe.Chest = animator.GetBoneTransform(HumanBodyBones.Spine);
            yield return null;
            yield return null;
            Assert.IsTrue(probe.Sampled);
            Assert.Greater(_melee.SpinPoseWeight, 0.9f, "the spin's animation does not hold the body");
            Assert.IsNotNull(_melee.SpinPoseShown, "no frame of the spin's animation is shown");
            Assert.Greater(_locomotion.BakedPoseWeight, 0.9f, "the animation was not laid on the body");
            Assert.Less(Vector3.Angle(probe.Along, probe.Right), 30.0f,
                $"the blade points {Vector3.Angle(probe.Along, probe.Right):0} degrees off straight out to the chest's right " +
                $"(spun for {_melee.SpinElapsedSeconds:0.00}s, kind {_melee.CurrentSpinKind})");
            // Owner, 2026-11-05: "hold the sword more horizontally ... mostly horizontally and very slightly tilted upward" (it rose up to 27 degrees).
            Assert.That(probe.Along.y, Is.InRange(0.0f, 0.12f), $"the blade should be nearly level, a little up, not {probe.Along.y:0.00} ({Mathf.Asin(Mathf.Clamp(probe.Along.y, -1f, 1f)) * Mathf.Rad2Deg:0} degrees)");
            Assert.Less(Vector3.Dot(probe.LeftHandOffset, probe.Right), -0.2f, "the free arm should be out the other side for balance");
#else
            yield break;
#endif
        }

        /// <summary>
        /// Owner, 2026-11-05: "hold the sword more horizontally when doing the spin right now its angled too upward, I want it to be mostly
        /// horizontally and very slightly tilted upward": every frame its blows can land on, ground and air, the blade near level.
        /// </summary>
        [UnityTest]
        public IEnumerator ThroughEveryBlowTheBladeIsNearlyLevel([Values(false, true)] bool inTheAir)
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword();
            if (inTheAir)
            {
                yield return JumpToHeight(_vitals.transform.position.y, 0.6f);
            }
            Animator animator = _vitals.GetComponentInChildren<Animator>();
            var probe = _vitals.gameObject.AddComponent<PoseProbe>();
            probe.Blade = _melee.EquippedWeapon as ISwordBlade;
            probe.LeftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            probe.Chest = animator.GetBoneTransform(HumanBodyBones.Spine);
            SpinPulse[] pulses = SwordSpinMove.Pulses(inTheAir ? SpinKind.Air : SpinKind.Ground);

            Press(_keyboard.digit2Key);
            yield return null;
            Release(_keyboard.digit2Key);
            float started = Time.time;
            int blowsBeforeTheThrow = pulses[pulses.Length - 1].Final ? pulses.Length - 1 : pulses.Length;
            float until = pulses[blowsBeforeTheThrow - 1].Time + SwordSpinMove.Frames(2.0f);
            while (Time.time - started < pulses[0].Time)
            {
                yield return null;
            }
            probe.Recording = true;
            while (Time.time - started < until)
            {
                yield return null;
            }
            probe.Recording = false;
            float highest = Mathf.Asin(Mathf.Clamp(probe.HighestRise, -1f, 1f)) * Mathf.Rad2Deg;
            float lowest = Mathf.Asin(Mathf.Clamp(probe.LowestRise, -1f, 1f)) * Mathf.Rad2Deg;
            Debug.Log($"CityX spin blade ({(inTheAir ? "air" : "ground")}): {lowest:0.0} to {highest:0.0} degrees, highest {probe.HighestAt - started:0.00} s in");
            Assert.That(highest, Is.LessThanOrEqualTo(10.0f), $"the blade rose to {highest:0} degrees, {probe.HighestAt - started:0.00} s into the spin");
            // A moment's dip as the arm swings out into the first turn, never a blade pointing down.
            Assert.That(lowest, Is.GreaterThanOrEqualTo(-6.0f), $"the blade dipped to {lowest:0} degrees");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheSpinTrailsLightFromTheBladeThatFadesAfter()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword();

            Press(_keyboard.digit2Key);
            yield return null;
            Release(_keyboard.digit2Key);
            yield return Seconds(0.3f);

            BladeTrail trail = _melee.SpinSlash;
            Assert.IsNotNull(trail, "no light trails from the blade -- is the slash material on the player's MeleeAttacker?");
            Assert.IsTrue(trail.IsEmitting && trail.IsShowing, "the light is not trailing from the blade mid-spin");
            Assert.Greater(trail.RibbonBounds.extents.magnitude, 1.0f, "the light is not a sweep round the body");

            yield return Seconds(1.2f);
            Assert.IsFalse(trail.IsShowing, "the light never died away");
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

        /// <summary>
        /// Owner, 2026-11-05: four blows, four damage numbers, the target carried along with the spin (Smash's Toon Link), and on the ground
        /// the fourth a plain blow: every blow draws the target in toward the spinner, none throws it up.
        /// </summary>
        [UnityTest]
        public IEnumerator TheGroundSpinHitsFourTimesDrawingTheTargetInAndThrowsNothing()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword();
            Vector3 at = _vitals.transform.position;
            Target beside = Dummy(at + new Vector3(2.0f, 1.2f, 0.0f));

            Press(_keyboard.digit2Key);
            yield return null;
            Release(_keyboard.digit2Key);
            yield return Seconds(1.6f);

            Assert.AreEqual(4, beside.Hits, "the ground spin should land four blows");
            Assert.AreEqual(4, beside.Launches, "each blow should draw the target along");
            Assert.Less(beside.LastLaunch.x, 0.0f, "the blows push the target away instead of drawing it in");
            Assert.AreEqual(0.0f, beside.LastLaunch.y, 1e-4f, "a ground spin's blow threw the target up");
#else
            yield break;
#endif
        }

        /// <summary>A body drawn by a spin's blow moves that far toward the spinner, through its own controller (walls still stop it).</summary>
        [UnityTest]
        public IEnumerator APullDrawsABodyAlong()
        {
#if UNITY_EDITOR
            yield return LoadSceneWithASword();
            Vector3 before = _vitals.transform.position;
            _vitals.TriggerPull(Vector3.right, 1.5f, 0.3f, 0.0f);
            yield return Seconds(0.6f);
            float moved = _vitals.transform.position.x - before.x;
            Assert.That(moved, Is.InRange(1.0f, 2.0f), $"pulled 1.5 m, the body moved {moved:0.00} m");
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
            Assert.GreaterOrEqual(above.Launches, 1, "the last blow did not throw it");
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
