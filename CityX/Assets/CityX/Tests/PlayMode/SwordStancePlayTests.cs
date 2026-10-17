using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The sword held and swung on the REAL Player.prefab with the real swords (owner, 2026-10-16: "my character holds and swings the sword
    /// awkwardly ... make the character is not turned in any awkward way and make sure his feet dont lock up during combat animations while
    /// moving", and "use the spikesword as guide ... the side with the spikes" should hit):
    ///
    ///  * swinging while walking, the feet STEP and stay put between steps instead of sliding along the ground (SwordFootwork);
    ///  * the spike sword's spiked side (its edge, local -X) leads every cut, measured on the rig, not just on paper;
    ///  * the body stays upright through a swing and a guard (no stoop from the sword idle clip's spine);
    ///  * the sword is carried low at the side, not held out at the shoulders;
    ///  * another player's copy, given only the attack's name and its body's motion (what the network sends), carries and steps the same way.
    /// </summary>
    public sealed class SwordStancePlayTests
    {
        private const float Dt = 1.0f / 60.0f;
        private GameObject _ground;
        private GameObject _player;
        private GameObject _copy;
        private SwordWeapon _sword;
        private SwordWeapon _copySword;
        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private SoulsSwordCombat _combat;
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

        private void Build(string swordPath)
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var swordPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(swordPath);
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");
            Assert.IsNotNull(swordPrefab, swordPath + " is missing");
            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.05f, -20.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _vitals = _player.GetComponent<PlayerVitals>();
            _vitals.InfiniteStamina = true;
            _animator = _player.GetComponentInChildren<Animator>();
            _sword = Object.Instantiate(swordPrefab, new Vector3(40.0f, 1.0f, 40.0f), Quaternion.identity).GetComponent<SwordWeapon>();
            _sword.OnEquip(_player, CityX.Items.ItemInstanceData.None);
            _combat = _player.GetComponent<SoulsSwordCombat>();
            _combat.HitStopSeconds = 0.0f;
#endif
        }

        /// <summary>One frame of the owner's body, the way the input reader drives it: a committed swing slows the walk.</summary>
        private IEnumerator Frame(Vector2 stick)
        {
            Vector2 move = _combat.IsCommitted ? stick * _combat.CommittedMoveSpeedFraction : stick;
            _combat.Tick(Dt);
            _vitals.TickHitstun(Dt);
            _locomotion.Tick(new LocomotionInput { SwordEquipped = !_combat.IsOneHanded, Sword1HEquipped = _combat.IsOneHanded, Move = move }, Dt);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SwingingWhileWalkingTheFeetStepAndDoNotSlide()
        {
#if UNITY_EDITOR
            foreach (string swordPath in new[] { "Assets/CityX/Weapons/SpikeSword.prefab", "Assets/CityX/Weapons/CombatSword.prefab" })
            {
                Build(swordPath);
                for (int i = 0; i < 40; i++) yield return Frame(new Vector2(0.0f, 1.0f));

                Transform[] feet = { _animator.GetBoneTransform(HumanBodyBones.LeftFoot), _animator.GetBoneTransform(HumanBodyBones.RightFoot) };
                Vector3[] last = { feet[0].position, feet[1].position };
                float[] slid = new float[2];
                int stepsBefore = _locomotion.SwordFootwork.StepsTaken;
                float footworkFrames = 0.0f, frames = 0.0f;
                float nextClick = 0.0f;
                for (float t = 0.0f; t < 3.0f; t += Dt)
                {
                    if (t >= nextClick && t < 2.6f)
                    {
                        _combat.RequestLight(0.0f);
                        nextClick += 0.3f;
                    }
                    yield return Frame(new Vector2(0.0f, 1.0f));
                    SwordFootwork footwork = _locomotion.SwordFootwork;
                    frames++;
                    if (footwork.Weight > 0.99f)
                    {
                        footworkFrames++;
                    }
                    for (int f = 0; f < 2; f++)
                    {
                        Vector3 p = feet[f].position;
                        // A planted foot (the stepper says it is down, and it has the feet) must stay where it is.
                        if (footwork.Weight > 0.99f && !footwork.Of(f).Stepping)
                        {
                            slid[f] += new Vector2(p.x - last[f].x, p.z - last[f].z).magnitude;
                        }
                        last[f] = p;
                    }
                }
                int steps = _locomotion.SwordFootwork.StepsTaken - stepsBefore;
                string label = System.IO.Path.GetFileNameWithoutExtension(swordPath);
                Assert.Greater(footworkFrames / frames, 0.7f, $"{label}: the feet were the stepper's only {footworkFrames / frames:P0} of the swinging");
                Assert.Greater(steps / 3.0f, 2.0f, $"{label}: only {steps} steps in three seconds of swinging on the move: the feet lock up");
                // Before (2026-10-16) each foot slid about 3.4 m along the ground in these three seconds.
                Assert.Less(slid[0] + slid[1], 0.2f, $"{label}: the planted feet slid {slid[0]:0.00} + {slid[1]:0.00} m");
                TearDown();
                yield return null;
            }
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheSpikedEdgeLeadsEveryCutOnTheRig()
        {
#if UNITY_EDITOR
            foreach (string swordPath in new[] { "Assets/CityX/Weapons/SpikeSword.prefab", "Assets/CityX/Weapons/CombatSword.prefab" })
            {
                Build(swordPath);
                for (int i = 0; i < 30; i++) yield return Frame(Vector2.zero);
                var attacks = new System.Collections.Generic.List<(SoulsAttack attack, bool heavy, int chain)>
                {
                    (_combat.LightAttack(0), false, 0), (_combat.LightAttack(1), false, 1), (_combat.HeavyAttack, true, 0),
                };
                if (!_combat.IsOneHanded)
                {
                    attacks.Add((_combat.LightAttack(2), false, 2));   // the one-handed third is the thrust: it leads with its point
                }
                foreach ((SoulsAttack attack, bool heavy, int chain) in attacks)
                {
                    // Chain up to it, then measure it.
                    for (int earlier = 0; earlier < chain; earlier++)
                    {
                        _combat.RequestLight(0.0f);
                        SoulsAttack before = _combat.LightAttack(earlier);
                        for (float t = 0.0f; t < before.Startup + before.Active + before.Recovery * before.ChainAt + 0.02f; t += Dt)
                        {
                            yield return Frame(Vector2.zero);
                        }
                    }
                    if (heavy) _combat.RequestHeavy(0.0f);
                    else _combat.RequestLight(0.0f);
                    yield return Frame(Vector2.zero);   // a chained request starts on the next tick
                    Assert.AreEqual(attack.Animation, _combat.CurrentAttack?.Animation);

                    float sum = 0.0f;
                    int samples = 0;
                    Vector3 lastMid = Vector3.Lerp(_sword.BladeBaseWorld, _sword.BladeTipWorld, 0.6f);
                    while (_combat.IsCommitted)
                    {
                        yield return Frame(Vector2.zero);
                        Vector3 mid = Vector3.Lerp(_sword.BladeBaseWorld, _sword.BladeTipWorld, 0.6f);
                        Vector3 motion = mid - lastMid;
                        lastMid = mid;
                        if (_combat.Phase != SoulsPhase.Active || ReferenceEquals(_combat.CurrentAttack, null) || _combat.CurrentAttack.Animation != attack.Animation)
                        {
                            continue;
                        }
                        Vector3 blade = (_sword.BladeTipWorld - _sword.BladeBaseWorld).normalized;
                        Vector3 across = motion - blade * Vector3.Dot(motion, blade);
                        if (across.magnitude < 0.005f)
                        {
                            continue;
                        }
                        sum += Vector3.Dot(_sword.EdgeDirectionWorld, across.normalized);
                        samples++;
                    }
                    Assert.Greater(samples, 3, $"{attack.Animation}: hardly any live frames measured");
                    Assert.Greater(sum / samples, 0.6f, $"{attack.Animation}: the spiked edge does not lead the cut on the rig (on average {sum / samples:0.00})");
                    for (int i = 0; i < 120; i++) yield return Frame(Vector2.zero);
                }
                TearDown();
                yield return null;
            }
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheBodyStaysUprightThroughTheSwingsAndTheGuard()
        {
#if UNITY_EDITOR
            Build("Assets/CityX/Weapons/Sword.prefab");
            for (int i = 0; i < 40; i++) yield return Frame(Vector2.zero);
            Transform hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform neck = _animator.GetBoneTransform(HumanBodyBones.Neck);
            float worst = 0.0f;
            string worstAt = "";
            System.Action<string> measure = label =>
            {
                float stoop = Vector3.Angle(neck.position - hips.position, Vector3.up);
                float authored = Mathf.Max(0.0f, _locomotion.CurrentSwordFrame.LeanDegrees) * _locomotion.CurrentSwordFrame.Weight;
                if (stoop - authored > worst)
                {
                    worst = stoop - authored;
                    worstAt = label;
                }
            };
            for (int a = 0; a < 3; a++)
            {
                _combat.RequestLight(0.0f);
                for (float t = 0.0f; t < 0.6f; t += Dt)
                {
                    yield return Frame(Vector2.zero);
                    measure($"light {a + 1}");
                }
            }
            for (int i = 0; i < 60; i++) yield return Frame(Vector2.zero);
            for (int i = 0; i < 40; i++)
            {
                _combat.SetBlockHeld(true);
                yield return Frame(Vector2.zero);
                measure("guard");
            }
            // The sword idle's own spine used to be followed through a swing: about 35 degrees of stoop on top of the cut's lean.
            Assert.Less(worst, 12.0f, $"the body stoops {worst:0} degrees beyond the cut's own lean ({worstAt})");
#else
            yield break;
#endif
        }

        /// <summary>
        /// Adjusting the hold by hand in the editor (owner, 2026-10-16: "i will likely need to adjust the grip and positioning of the sword
        /// manually"): while the held sword is selected the hand holds still and the sword turns IN it, as the gizmo turns it; deselected,
        /// the stance points the blade where it wants with the new hold.
        /// </summary>
        [UnityTest]
        public IEnumerator WhileTheHoldIsBeingEditedTheSwordTurnsInTheStillHand()
        {
#if UNITY_EDITOR
            Build("Assets/CityX/Weapons/SpikeSword.prefab");
            for (int i = 0; i < 60; i++) yield return Frame(Vector2.zero);
            Transform hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            WeaponGripSettings.HoldBeingEdited = true;
            try
            {
                yield return Frame(Vector2.zero);   // the hold as it was is kept from here
                Quaternion handBefore = hand.rotation;
                Vector3 edgeBefore = -_sword.transform.right;
                _sword.transform.localRotation = _sword.transform.localRotation * Quaternion.AngleAxis(30.0f, Vector3.up);   // the gizmo: spun about the blade
                for (int i = 0; i < 10; i++) yield return Frame(Vector2.zero);
                Assert.Less(Quaternion.Angle(handBefore, hand.rotation), 3.0f, "the hand turned round the sword while its hold was being edited");
                Assert.Greater(Vector3.Angle(edgeBefore, -_sword.transform.right), 20.0f, "the sword did not turn in the hand");
            }
            finally
            {
                WeaponGripSettings.HoldBeingEdited = false;
            }
            for (int i = 0; i < 10; i++) yield return Frame(Vector2.zero);
            SwordFrame frame = _locomotion.CurrentSwordFrame;
            Transform body = _player.transform;
            Vector3 asked = (body.right * frame.Blade.x + body.up * frame.Blade.y + body.forward * frame.Blade.z).normalized;
            Assert.Less(Vector3.Angle(asked, _sword.transform.up), 25.0f, "with the hold edited, the stance no longer points the blade where it wants");   // (the held stances give a little of the blade's angle to a straight wrist)
#else
            yield break;
#endif
        }

        /// <summary>
        /// A one-handed sword is carried with the forearm running straight into the hand (owner, 2026-10-16, with the combat sword: "angle the
        /// arm properly so theres no that weird bend"): the elbow is placed for it and the hand meets it the rest of the way.
        /// </summary>
        [UnityTest]
        public IEnumerator AOneHandedSwordIsCarriedWithAStraightWrist()
        {
#if UNITY_EDITOR
            Build("Assets/CityX/Weapons/CombatSword.prefab");
            for (int i = 0; i < 60; i++) yield return Frame(Vector2.zero);
            Transform hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform elbow = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            float wristBend = Vector3.Angle(hand.position - elbow.position, hand.right);
            Assert.Less(wristBend, 38.0f, $"the wrist is bent {wristBend:0} degrees carrying the sword");   // (tipped 30 on purpose, point down)
            Assert.Less((-_sword.transform.right).y, -0.3f, "the edge does not face down");
            Vector3 blade = (_sword.BladeTipWorld - _sword.BladeBaseWorld).normalized;
            Assert.Less(blade.y, -0.3f, "the carried blade does not point down");
#else
            yield break;
#endif
        }

        /// <summary>The one-handed sword moves with its own clips (owner, 2026-10-17: the 1H sword runs and side-steps) and goes back to the plain set without it.</summary>
        [UnityTest]
        public IEnumerator AOneHandedSwordRunsWithItsOwnClips()
        {
#if UNITY_EDITOR
            Build("Assets/CityX/Weapons/CombatSword.prefab");
            for (int i = 0; i < 40; i++) yield return Frame(new Vector2(1.0f, 0.0f));
            Assert.IsTrue(_animator.GetCurrentAnimatorStateInfo(0).IsName("Sword1HLocomotion"), "the one-handed sword is not moving with its own clips");
            for (int i = 0; i < 40; i++)
            {
                _locomotion.Tick(new LocomotionInput { Move = new Vector2(1.0f, 0.0f) }, Dt);
                yield return null;
            }
            Assert.IsTrue(_animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"), "putting the sword away did not hand the legs back to the plain walk");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheSwordIsCarriedLowAtTheSideAndTheCopyCarriesItTheSameWay()
        {
#if UNITY_EDITOR
            Build("Assets/CityX/Weapons/SpikeSword.prefab");
            for (int i = 0; i < 60; i++) yield return Frame(Vector2.zero);
            PlayerAnimationDriver driver = _player.GetComponent<PlayerAnimationDriver>();
            Assert.IsTrue(driver.SwordPoser.IsCarrying, "the sword is not being carried");
            Transform shoulder = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            Assert.Greater(shoulder.position.y - hand.position.y, 0.4f, "the sword hand is up at the shoulders, not low at the side");
            Vector3 blade = (_sword.BladeTipWorld - _sword.BladeBaseWorld).normalized;
            Assert.Less(blade.y, -0.2f, "the carried blade does not point down");

            // ANOTHER PLAYER'S COPY: the same body with the sword mounted as a cosmetic and nothing ticking it (the network moves it).
            var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _copy = Object.Instantiate(playerPrefab, new Vector3(6.0f, 0.05f, -20.0f), Quaternion.identity);
            _copy.GetComponent<PlayerInputReader>().enabled = false;
            var swordPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/SpikeSword.prefab");
            _copySword = Object.Instantiate(swordPrefab, new Vector3(40.0f, 1.0f, 44.0f), Quaternion.identity).GetComponent<SwordWeapon>();
            _copySword.MountCosmetic(_copy);
            PlayerAnimationDriver copyDriver = _copy.GetComponent<PlayerAnimationDriver>();
            PlayerLocomotion copyLocomotion = _copy.GetComponent<PlayerLocomotion>();
            Animator copyAnimator = _copy.GetComponentInChildren<Animator>();
            for (int i = 0; i < 40; i++)
            {
                copyAnimator.SetBool("Grounded", true);   // what NetworkedAnimatorSync hands a copy every tick
                yield return null;
            }
            Assert.IsTrue(copyDriver.SwordPoser.IsCarrying, "another player's copy does not carry the sword");
            Transform copyShoulder = copyAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform copyHand = copyAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            Assert.AreEqual(shoulder.position.y - hand.position.y, copyShoulder.position.y - copyHand.position.y, 0.05f,
                "the copy holds the sword differently from the owner");

            // The owner swings on the move: the copy is told the attack's name and moved as the network moves it.
            copyDriver.PlayRemoteState(SwordStrikes.Sweep2HName);
            Assert.IsTrue(copyDriver.SwordPoser.IsStriking, "the copy did not take up the swing");
            int stepsBefore = copyLocomotion.SwordFootwork.StepsTaken;
            float most = 0.0f;
            for (float t = 0.0f; t < SwordStrikes.Seconds(SwordStrikeKind.Sweep2H); t += Dt)
            {
                _copy.transform.position += Vector3.forward * (1.6f * Dt);
                copyAnimator.SetBool("Grounded", true);
                copyAnimator.SetFloat("Speed", 1.6f);
                copyAnimator.SetFloat("MoveZ", 1.6f);
                yield return null;
                most = Mathf.Max(most, copyLocomotion.SwordFootwork.Weight);
            }
            Assert.Greater(most, 0.99f, "the copy's feet never took the stance");
            Assert.GreaterOrEqual(copyLocomotion.SwordFootwork.StepsTaken - stepsBefore, 2, "the copy's feet did not step as it moved through the swing");
#else
            yield break;
#endif
        }
    }
}
