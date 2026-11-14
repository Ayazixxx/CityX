using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The one-handed sword carried by its own clips (owner, 2026-10-17: "the sword is like spinning around make you own sword running and
    /// walking animations and make them look good"; "research how games do one handed sword running and walking animations and make ours
    /// work"), on the REAL Player.prefab with the real combat sword, standing, walking, sprinting, strafing and backing up:
    ///
    ///  * the blade holds its angle (it bobs, it does not spin): never past level, never straight down, turning only a few degrees a frame;
    ///  * it clears both legs and the ground the whole way;
    ///  * the sword arm swings, less than the free arm and against the leg on its own side;
    ///  * nothing poses over the clips while carrying, and another player's copy (given only what the network gives it) carries it the same.
    /// </summary>
    public sealed class SwordCarryPlayTests
    {
        private const float Dt = 1.0f / 60.0f;
        private GameObject _ground;
        private GameObject _player;
        private GameObject _copy;
        private SwordWeapon _sword;
        private SwordWeapon _copySword;
        private PlayerLocomotion _locomotion;
        private Animator _animator;

        [TearDown]
        public void TearDown()
        {
            if (_sword != null) Object.Destroy(_sword.gameObject);
            if (_copySword != null) Object.Destroy(_copySword.gameObject);
            if (_player != null) Object.Destroy(_player);
            if (_copy != null) Object.Destroy(_copy);
            if (_ground != null) Object.Destroy(_ground);
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
        }

        private void Build()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var swordPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/CombatSword.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");
            Assert.IsNotNull(swordPrefab, "CombatSword.prefab is missing");
            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.05f, -100.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _player.GetComponent<PlayerVitals>().InfiniteStamina = true;
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _animator = _player.GetComponentInChildren<Animator>();
            _sword = Object.Instantiate(swordPrefab, new Vector3(40.0f, 1.0f, 40.0f), Quaternion.identity).GetComponent<SwordWeapon>();
            _sword.OnEquip(_player, CityX.Items.ItemInstanceData.None);
            Assert.IsTrue(_sword.IsOneHanded, "the combat sword is not one-handed");
#endif
        }

        private IEnumerator Frame(Vector2 stick, bool sprint)
        {
            _locomotion.Tick(new LocomotionInput { Sword1HEquipped = true, Move = stick, Sprint = sprint }, Dt);
            yield return null;
        }

        /// <summary>The nearest the blade comes to a limb segment, metres (closest points of two segments, sampled along the blade).</summary>
        private static float Clearance(Vector3 bladeBase, Vector3 bladeTip, Vector3 a, Vector3 b)
        {
            float best = float.MaxValue;
            for (int i = 0; i <= 24; i++)
            {
                Vector3 p = Vector3.Lerp(bladeBase, bladeTip, i / 24.0f);
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                best = Mathf.Min(best, Vector3.Distance(p, a + ab * t));
            }
            return best;
        }

        [UnityTest]
        public IEnumerator OnTheMoveTheBladeHoldsItsAngleClearOfTheLegsAndTheGround()
        {
#if UNITY_EDITOR
            Build();
            Transform body = _player.transform;
            Transform rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform shoulder = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform[] legs =
            {
                _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg), _animator.GetBoneTransform(HumanBodyBones.RightLowerLeg), _animator.GetBoneTransform(HumanBodyBones.RightFoot),
                _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), _animator.GetBoneTransform(HumanBodyBones.LeftFoot),
            };
            // The leg's flesh round its bones, in this body's metres.
            float legRadius = BodyRig.LegRadius * Vector3.Distance(legs[0].position, legs[1].position) / (BodyRig.LegLength * BodyRig.KneeT);

            foreach ((string name, Vector2 stick, bool sprint) in new[]
            {
                ("standing", Vector2.zero, false),
                ("walking", new Vector2(0.0f, 1.0f), false),
                ("sprinting", new Vector2(0.0f, 1.0f), true),
                ("strafing right", new Vector2(1.0f, 0.0f), false),
                ("strafing left", new Vector2(-1.0f, 0.0f), false),
                ("backing up", new Vector2(0.0f, -1.0f), false),
            })
            {
                for (int i = 0; i < 90; i++) yield return Frame(stick, sprint);
                Assert.IsTrue(_animator.GetCurrentAnimatorStateInfo(0).IsName("Sword1HLocomotion"), $"{name}: the sword's own clips are not playing");
                Assert.IsFalse(_locomotion.SwordPoseActive, $"{name}: a pose is laid over the carry clips");

                Vector3 lastBlade = (_sword.BladeTipWorld - _sword.BladeBaseWorld).normalized;
                Quaternion lastSword = _sword.transform.rotation;
                float highest = -1.0f, lowest = 1.0f, worstTurn = 0.0f, worstSpin = 0.0f, nearestLeg = float.MaxValue, lowestTip = float.MaxValue;
                float rightMost = float.MinValue, rightLeast = float.MaxValue, leftMost = float.MinValue, leftLeast = float.MaxValue;
                for (int i = 0; i < 120; i++)
                {
                    yield return Frame(stick, sprint);
                    Vector3 bladeBase = _sword.BladeBaseWorld, tip = _sword.BladeTipWorld;
                    Vector3 blade = (tip - bladeBase).normalized;
                    highest = Mathf.Max(highest, blade.y);
                    lowest = Mathf.Min(lowest, blade.y);
                    worstTurn = Mathf.Max(worstTurn, Vector3.Angle(lastBlade, blade));
                    worstSpin = Mathf.Max(worstSpin, Quaternion.Angle(lastSword, _sword.transform.rotation));
                    lastBlade = blade;
                    lastSword = _sword.transform.rotation;
                    for (int leg = 0; leg < 6; leg += 3)
                    {
                        nearestLeg = Mathf.Min(nearestLeg, Clearance(bladeBase, tip, legs[leg].position, legs[leg + 1].position) - legRadius);
                        nearestLeg = Mathf.Min(nearestLeg, Clearance(bladeBase, tip, legs[leg + 1].position, legs[leg + 2].position) - legRadius);
                    }
                    lowestTip = Mathf.Min(lowestTip, tip.y - body.position.y);
                    float right = Vector3.Dot(rightHand.position - shoulder.position, body.forward);
                    float left = Vector3.Dot(leftHand.position - shoulder.position, body.forward);
                    rightMost = Mathf.Max(rightMost, right);
                    rightLeast = Mathf.Min(rightLeast, right);
                    leftMost = Mathf.Max(leftMost, left);
                    leftLeast = Mathf.Min(leftLeast, left);
                }
                Debug.Log($"CityX 1H carry {name}: blade height {Mathf.Asin(lowest) * Mathf.Rad2Deg:0}..{Mathf.Asin(highest) * Mathf.Rad2Deg:0} deg, turns at most {worstTurn:0.0} deg/frame, " +
                          $"sword turns at most {worstSpin:0.0} deg/frame, nearest a leg {nearestLeg:0.00} m, tip at least {lowestTip:0.00} m up, " +
                          $"sword hand swings {rightMost - rightLeast:0.00} m, free hand {leftMost - leftLeast:0.00} m");
                Assert.Less(highest, 0.0f, $"{name}: the blade tips up past level");
                Assert.Greater(lowest, -0.85f, $"{name}: the blade points straight at the ground");
                Assert.Less(worstTurn, 4.0f, $"{name}: the blade swings round");
                Assert.Less(worstSpin, 6.0f, $"{name}: the sword spins in the hand");
                Assert.Greater(nearestLeg, 0.0f, $"{name}: the blade goes through a leg");
                Assert.Greater(lowestTip, 0.1f, $"{name}: the blade's tip scrapes the ground");
                if (stick.y > 0.5f)
                {
                    Assert.Greater(rightMost - rightLeast, 0.05f, $"{name}: the sword arm does not swing at all");
                    Assert.Less(rightMost - rightLeast, 0.75f * (leftMost - leftLeast), $"{name}: the sword arm swings as wide as the free arm");
                }
            }
#else
            yield break;
#endif
        }

        /// <summary>Walking, the sword arm swings against the leg on its own side (forward as the right leg goes back), as an arm does.</summary>
        [UnityTest]
        public IEnumerator TheSwordArmSwingsAgainstTheRightLeg()
        {
#if UNITY_EDITOR
            Build();
            Transform body = _player.transform;
            Transform hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform foot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            for (int i = 0; i < 90; i++) yield return Frame(new Vector2(0.0f, 1.0f), false);
            const int Samples = 120;
            var hands = new float[Samples];
            var feet = new float[Samples];
            for (int i = 0; i < Samples; i++)
            {
                yield return Frame(new Vector2(0.0f, 1.0f), false);
                hands[i] = Vector3.Dot(hand.position - hips.position, body.forward);
                feet[i] = Vector3.Dot(foot.position - hips.position, body.forward);
            }
            float handMean = 0.0f, footMean = 0.0f;
            for (int i = 0; i < Samples; i++)
            {
                handMean += hands[i] / Samples;
                footMean += feet[i] / Samples;
            }
            float together = 0.0f, handSpread = 0.0f, footSpread = 0.0f;
            for (int i = 0; i < Samples; i++)
            {
                together += (hands[i] - handMean) * (feet[i] - footMean);
                handSpread += (hands[i] - handMean) * (hands[i] - handMean);
                footSpread += (feet[i] - footMean) * (feet[i] - footMean);
            }
            float correlation = together / Mathf.Sqrt(Mathf.Max(handSpread * footSpread, 1e-9f));
            Debug.Log($"CityX 1H carry: the sword hand against the right foot, correlation {correlation:0.00}");
            Assert.Less(correlation, -0.3f, "the sword arm swings with its own leg, not against it");
#else
            yield break;
#endif
        }

        /// <summary>
        /// Swinging from the carry and lowering back into it flows (owner, 2026-10-17: "make it flow between the animations and the attack
        /// animations smoothly"): the attack blends in from the carry clips' arm instead of snapping to its first key, and once the stance after
        /// it has lingered it lowers back into the clips over a moment, all while walking.
        /// </summary>
        [UnityTest]
        public IEnumerator AnAttackFlowsOutOfTheCarryAndBackIntoIt()
        {
#if UNITY_EDITOR
            Build();
            var combat = _player.GetComponent<SoulsSwordCombat>();
            combat.HitStopSeconds = 0.0f;
            PlayerAnimationDriver driver = _player.GetComponent<PlayerAnimationDriver>();
            Transform hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform body = _player.transform;
            Vector2 walk = new Vector2(0.0f, 1.0f);
            for (int i = 0; i < 90; i++) yield return Frame(walk, false);
            Assert.IsTrue(driver.SwordPoser.IsCarrying, "the sword is not being carried");

            Vector3 last = body.InverseTransformPoint(hand.position);
            combat.RequestLight(0.0f);
            float startJump = 0.0f;
            for (int i = 0; i < 6; i++)
            {
                combat.Tick(Dt);
                yield return Frame(walk, false);   // a swing takes nothing from a player's movement
                Vector3 now = body.InverseTransformPoint(hand.position);
                startJump = Mathf.Max(startJump, (now - last).magnitude);
                last = now;
            }
            Assert.IsTrue(driver.SwordPoser.IsStriking, "the attack did not start");

            // Through the attack and the stance after it, until the carry clips have the arm back.
            float lowerJump = 0.0f, lowerTurn = 0.0f;
            Quaternion lastTurn = hand.rotation;
            bool lowering = false;
            for (int i = 0; i < 60 * 8 && (_locomotion.SwordPoseActive || !lowering); i++)
            {
                combat.Tick(Dt);
                yield return Frame(walk, false);   // a swing takes nothing from a player's movement
                Vector3 now = body.InverseTransformPoint(hand.position);
                lowering |= !driver.SwordPoser.IsStriking && driver.SwordPoser.IsCarrying;
                if (lowering)
                {
                    lowerJump = Mathf.Max(lowerJump, (now - last).magnitude);
                    lowerTurn = Mathf.Max(lowerTurn, Quaternion.Angle(lastTurn, hand.rotation));
                }
                last = now;
                lastTurn = hand.rotation;
            }
            Debug.Log($"CityX 1H carry to attack: the hand moves at most {startJump * 100.0f:0.0} cm/frame as the attack takes over; back to the carry at most {lowerJump * 100.0f:0.0} cm/frame, {lowerTurn:0.0} deg/frame");
            Assert.IsTrue(lowering, "the stance after the attack never lowered back into the carry");
            Assert.IsFalse(_locomotion.SwordPoseActive, "the pose never handed the arm back to the carry clips");
            Assert.Less(startJump, 0.15f, "the arm snaps from the carry into the attack");
            Assert.Less(lowerJump, 0.06f, "the arm drops back into the carry with a jump");
            Assert.Less(lowerTurn, 8.0f, "the hand snaps round as it drops back into the carry");
#else
            yield break;
#endif
        }

        /// <summary>
        /// The stance held after a swing bends the arm at the elbow, low at the side, with the wrist tipped to raise the blade (owner, 2026-10-17,
        /// with a screenshot of it: "its still holding it awkardly its like bent at the wrong part of the arm", "this is the pose after
        /// swinging"): the elbow hangs below the line from the shoulder to the hand, not up by the face, and the wrist bends no further than a
        /// wrist does.
        /// </summary>
        [UnityTest]
        public IEnumerator AfterASwingTheArmBendsDownAtTheElbow()
        {
#if UNITY_EDITOR
            Build();
            var combat = _player.GetComponent<SoulsSwordCombat>();
            combat.HitStopSeconds = 0.0f;
            PlayerAnimationDriver driver = _player.GetComponent<PlayerAnimationDriver>();
            Transform shoulder = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform elbow = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Transform hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform freeShoulder = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform freeHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            float armLength = Vector3.Distance(freeShoulder.position, _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).position)
                + Vector3.Distance(_animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).position, freeHand.position);
            for (int i = 0; i < 30; i++) yield return Frame(Vector2.zero, false);
            foreach (bool guard in new[] { false, true })
            {
                if (guard)
                {
                    driver.SwordPoser.BeginGuard(true);
                }
                else
                {
                    combat.RequestLight(0.0f);
                }
                float worstRise = float.MinValue, worstWrist = 0.0f, worstElbowOverShoulder = float.MinValue, settledWrist = 0.0f;
                float freeHighest = float.MinValue, freeForemost = float.MinValue;
                int heldFrames = 0;
                for (int i = 0; i < 150; i++)
                {
                    combat.Tick(Dt);
                    yield return Frame(Vector2.zero, false);
                    bool held = guard ? driver.SwordPoser.IsGuarding : (!driver.SwordPoser.IsStriking && !driver.SwordPoser.IsCarrying);
                    if (!held || _locomotion.CurrentSwordFrame.Weight < 0.99f)
                    {
                        continue;
                    }
                    // How far the elbow sits above the straight line from the shoulder to the wrist (+ = bent upward, the wrong way).
                    Vector3 chord = hand.position - shoulder.position;
                    float t = Mathf.Clamp01(Vector3.Dot(elbow.position - shoulder.position, chord) / chord.sqrMagnitude);
                    worstRise = Mathf.Max(worstRise, elbow.position.y - (shoulder.position.y + chord.y * t));
                    worstElbowOverShoulder = Mathf.Max(worstElbowOverShoulder, elbow.position.y - shoulder.position.y);
                    settledWrist = Vector3.Angle(hand.position - elbow.position, hand.right);
                    // The free hand (owner: "make it relaxed"): how high toward its shoulder it is held, and how far out in front.
                    freeHighest = Mathf.Max(freeHighest, (freeHand.position.y - freeShoulder.position.y) / armLength);
                    freeForemost = Mathf.Max(freeForemost, Vector3.Dot(freeHand.position - freeShoulder.position, _player.transform.forward) / armLength);
                    worstWrist = Mathf.Max(worstWrist, settledWrist);
                    heldFrames++;
                }
                string name = guard ? "the guard" : "the stance after a swing";
                Debug.Log($"CityX 1H {name}: the elbow at most {worstRise:0.00} m above the shoulder-hand line and {worstElbowOverShoulder:0.00} m above the shoulder; the wrist bent at most {worstWrist:0} deg, {settledWrist:0} deg once settled ({heldFrames} frames held)");
                Assert.Greater(heldFrames, 30, $"{name}: was never held");
                Debug.Log($"CityX 1H {name}: the free hand at highest {freeHighest:0.00} arm lengths from its shoulder (below is negative), at most {freeForemost:0.00} in front of it");
                Assert.Less(freeHighest, -0.7f, $"{name}: the free hand is held up (like a shield), not hanging relaxed");
                Assert.Less(freeForemost, 0.3f, $"{name}: the free hand is held out in front, not hanging relaxed");
                Assert.Less(worstRise, 0.02f, $"{name}: the arm bends upward, the elbow raised above the line to the hand");
                Assert.Less(settledWrist, 40.0f, $"{name}: the wrist is bent further than a wrist goes");
                Assert.Less(worstWrist, 60.0f, $"{name}: the wrist folds over as the stance takes over");
                if (guard)
                {
                    driver.SwordPoser.Release();
                }
                driver.SwordPoser.Cancel();
                for (int i = 0; i < 40; i++) yield return Frame(Vector2.zero, false);
            }
#else
            yield break;
#endif
        }

        /// <summary>Another player's copy, given only what NetworkedAnimatorSync gives it (the flags and the motion), holds the sword as the owner does.</summary>
        [UnityTest]
        public IEnumerator AnotherPlayersCopyCarriesItTheSameWay()
        {
#if UNITY_EDITOR
            Build();
            for (int i = 0; i < 60; i++) yield return Frame(Vector2.zero, false);
            var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var swordPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/CombatSword.prefab");
            _copy = Object.Instantiate(playerPrefab, new Vector3(8.0f, 0.05f, -100.0f), Quaternion.identity);
            _copy.GetComponent<PlayerInputReader>().enabled = false;
            _copySword = Object.Instantiate(swordPrefab, new Vector3(40.0f, 1.0f, 44.0f), Quaternion.identity).GetComponent<SwordWeapon>();
            _copySword.MountCosmetic(_copy);
            Animator copyAnimator = _copy.GetComponentInChildren<Animator>();
            for (int i = 0; i < 60; i++)
            {
                copyAnimator.SetBool("Grounded", true);   // what NetworkedAnimatorSync hands a copy every tick
                copyAnimator.SetBool("Sword1HEquipped", true);
                yield return Frame(Vector2.zero, false);
            }
            Assert.IsTrue(copyAnimator.GetCurrentAnimatorStateInfo(0).IsName("Sword1HLocomotion"), "the copy does not play the sword's carry");
            Assert.IsFalse(_copy.GetComponent<PlayerLocomotion>().SwordPoseActive, "a pose is laid over the copy's carry");
            Vector3 blade = _player.transform.InverseTransformDirection(_sword.BladeTipWorld - _sword.BladeBaseWorld).normalized;
            Vector3 copyBlade = _copy.transform.InverseTransformDirection(_copySword.BladeTipWorld - _copySword.BladeBaseWorld).normalized;
            Assert.Less(Vector3.Angle(blade, copyBlade), 8.0f, "the copy points the sword another way from the owner");
            Vector3 hand = _player.transform.InverseTransformPoint(_animator.GetBoneTransform(HumanBodyBones.RightHand).position);
            Vector3 copyHand = _copy.transform.InverseTransformPoint(copyAnimator.GetBoneTransform(HumanBodyBones.RightHand).position);
            Assert.Less(Vector3.Distance(hand, copyHand), 0.06f, "the copy holds the sword somewhere else");
#else
            yield break;
#endif
        }
    }
}
