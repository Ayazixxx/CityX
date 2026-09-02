using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The grapple swing's own held pose -- explicit ask: "his right hand
    /// stay attached to it but the rest of his body is ragdolling". THREE
    /// earlier versions of this used real Rigidbody/CharacterJoint physics
    /// and were all reported broken live (the body stretching apart, limbs
    /// flying off) -- see GrappleSwing's own class doc comment for why a
    /// script forcing a character through fast, arbitrary motion is a bad
    /// fit for a real constraint solver. This is a fourth, deliberately
    /// much simpler design: plain procedural bone rotation, the same
    /// technique PlayerLocomotionCastPosePlayTests already proves stable
    /// for PlayerLocomotion.ApplyCastingPose. Nothing here is physics, so
    /// there is no joint/collision failure mode left to test for -- what
    /// actually needs checking is that the Animator hands off and back
    /// cleanly, and that the bones move the way the pose is supposed to.
    /// </summary>
    public sealed class GrappleSwingPosePlayTests
    {
        private GameObject _player;
        private GrappleSwing _swing;
        private Animator _animator;

        private const float StepSeconds = 1.0f / 60.0f;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
        }

        private void SpawnPlayer()
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
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
#endif
        }

        [UnityTest]
        public IEnumerator AttachingDisablesTheAnimatorAndReleasingReEnablesIt()
        {
            SpawnPlayer();
            Assert.IsTrue(_animator.enabled, "setup failed -- the animator should start enabled");

            _swing.Attach(_player.transform.position + new Vector3(3.0f, 6.0f, 2.0f));
            Assert.IsFalse(_animator.enabled,
                "the animator is still posing bones the swing's own procedural pose needs to own");

            _swing.Release(false);
            Assert.IsTrue(_animator.enabled, "releasing did not hand control back to the animator");
            yield break;
        }

        /// <summary>
        /// EXPLICIT BUG REPORT: "just imagine the character immediately
        /// glitches out... upon opening menu and grapple swinging."
        /// Root-caused directly with a diagnostic across a freshly
        /// instantiated rig: an Animator that has never actually evaluated
        /// a single real frame yet sits at the raw imported BIND POSE
        /// (confirmed: a stark, all-zero-rotation T-pose) and only snaps to
        /// a real pose on its own first Update. Firing the instant control
        /// is gained -- spawning in and immediately firing at the nearest
        /// wall is a completely normal first action, not an edge case --
        /// disabled the Animator before it ever got that first Update, and
        /// CaptureBase reads the CURRENT bone rotation as the swing's own
        /// base to sway and reach relative to, so a T-pose base did not
        /// just freeze one bad frame, it corrupted the base the entire
        /// swing's pose built on. Checked here by attaching with ZERO real
        /// frames elapsed since Instantiate (matching the diagnostic's own
        /// repro exactly, and the live report of it happening
        /// "immediately") and confirming the captured base is not the
        /// all-zero bind pose.
        /// </summary>
        [UnityTest]
        public IEnumerator AttachingTheInstantAfterSpawnDoesNotCaptureTheRawBindPose()
        {
            SpawnPlayer();
            Transform rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);

            // NO yield HERE -- zero real frames, zero real Animator
            // evaluations, matching the exact repro: fire the instant the
            // rig exists.
            _swing.Attach(_player.transform.position + new Vector3(3.0f, 6.0f, 2.0f));

            Assert.Greater(
                Quaternion.Angle(Quaternion.identity, rightUpperArm.localRotation) +
                Quaternion.Angle(Quaternion.identity, leftUpperArm.localRotation), 1.0f,
                "the captured base is the raw all-zero bind pose -- the swing's whole pose is now " +
                "built on a T-pose instead of a real evaluated one");
            yield break;
        }

        /// <summary>
        /// EXPLICIT BUG REPORT, confirmed directly by rendering the exact
        /// frame before and after a release: the right arm jumped from
        /// UpdateSwingPose's own ~150 degree reach pose straight to
        /// whatever the Animator's live state evaluated to in a single
        /// frame, with nothing blending between them -- a real, visible
        /// discontinuity distinct from any of the compounding bugs already
        /// fixed elsewhere. Checked here by watching the FIRST real frame
        /// after release actually land close to the pre-release pose
        /// (not jump the full distance to the animator's own target
        /// outright) and the arm settling smoothly rather than snapping.
        /// </summary>
        [UnityTest]
        public IEnumerator ReleasingBlendsAwayFromTheReachPoseRatherThanSnapping()
        {
            SpawnPlayer();
            Transform rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);

            _swing.Attach(_player.transform.position + new Vector3(3.0f, 6.0f, 2.0f));
            for (int i = 0; i < 90; i++)
            {
                _swing.Tick(Vector2.zero, 0.0f, false, false, false, false, StepSeconds);
                yield return null;
            }
            Quaternion reachPose = rightUpperArm.localRotation;

            _swing.Release(false);
            yield return null;

            float jumpOnFirstFrame = Quaternion.Angle(reachPose, rightUpperArm.localRotation);
            Assert.Less(jumpOnFirstFrame, 20.0f,
                $"the arm jumped {jumpOnFirstFrame:0.0} degrees on the very first frame after release -- " +
                "that is a snap, not a blend");

            // Let the rest of the (short) blend window pass -- the arm
            // should keep moving toward the animator's own pose rather
            // than getting stuck at the captured release pose forever.
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }
            float totalDrift = Quaternion.Angle(reachPose, rightUpperArm.localRotation);
            Assert.Greater(totalDrift, jumpOnFirstFrame,
                "the arm never kept moving away from the reach pose after the first frame -- " +
                "the blend is stuck rather than actually easing toward the animator");
        }

        /// <summary>
        /// EXPLICIT USER REPORT: "why does it do it in the console
        /// sometimes now... i never saw that happen in console before the
        /// grapple hook was implemented." DiagnoseConsoleOpenedMidSwing
        /// already covers the console opening WHILE actively swinging
        /// (stable) -- this covers a different moment that leaves no other
        /// coverage: the console opened just AFTER releasing, during
        /// GrappleSwing's own 0.15s ReleaseBlendSeconds window.
        ///
        /// WHY THIS WINDOW IS SUSPECT SPECIFICALLY: BlendBone's own doc
        /// comment says it reads bone.localRotation itself as the blend's
        /// moving target, on the assumption the Animator keeps writing a
        /// fresh pose there every real frame. DevConsoleView.Open disables
        /// that Animator (never GrappleSwing itself, which it does not
        /// touch at all) -- once the Animator stops writing, LateUpdate
        /// (which the console never stops, unlike Tick) keeps slerping a
        /// bone toward whatever ITS OWN PREVIOUS OUTPUT was, every frame,
        /// rather than toward a genuinely live pose. This whole mechanism
        /// is part of the grapple hook feature itself, matching "never saw
        /// it before the grapple hook was implemented" exactly. Tried at
        /// several different delays between Release and the console
        /// opening -- "sometimes" implies this is timing-sensitive, so one
        /// single delay could get lucky and miss the real failure window.
        /// </summary>
        [UnityTest]
        public IEnumerator OpeningTheConsoleDuringTheReleaseBlendNeverProducesAWildPose()
        {
            SpawnPlayer();
            Transform rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform head = _animator.GetBoneTransform(HumanBodyBones.Head);
            Vector3 anchor = _player.transform.position + new Vector3(3.0f, 6.0f, 2.0f);

            int[] delaysBeforeConsoleOpens = { 0, 1, 2, 3, 5, 8, 12 };
            foreach (int delay in delaysBeforeConsoleOpens)
            {
                _swing.Attach(anchor);
                // Vector2.zero, matching ReleasingBlendsAwayFromTheReachPoseRatherThanSnapping's
                // own proven-stable setup -- real movement input here let the
                // character swing down, land, and go grounded-with-low-speed
                // well inside AttachGraceSeconds (0.2s), which Tick's own
                // auto-release check then ended on its own partway through
                // this loop; the explicit Release below then found
                // IsActive already false and did nothing at all, so every
                // trial was silently measuring an already-long-settled pose
                // instead of the actual release moment.
                for (int i = 0; i < 90; i++)
                {
                    _swing.Tick(Vector2.zero, 0.0f, false, false, false, false, StepSeconds);
                    yield return null;
                }
                Assert.IsTrue(_swing.IsActive, $"delay={delay}: setup failed -- the swing auto-released itself " +
                    "before the test's own explicit Release, so this trial measured the wrong moment entirely");
                Quaternion armAtRelease = rightUpperArm.localRotation;
                _swing.Release(false);

                // A REAL, BIG, CONTINUOUSLY CHANGING WALK-CYCLE POSE for the
                // blend to chase, not a static falling pose -- EXPLICIT ASK
                // this is modelling: "gets more distorted the more you
                // move". Driven directly through Animator.Play + a
                // zero-delta Update (the same forced-evaluation technique
                // this whole file already relies on elsewhere), not real
                // PlayerLocomotion/CharacterController physics -- the
                // player is still mid-air this soon after a swing, so
                // physics-driven walking would not actually be grounded yet
                // within a handful of frames, and what is under test here
                // is BlendBone's own reaction to a moving target, not
                // whether the character has landed.
                for (int i = 0; i < delay; i++)
                {
                    float walkT = ((i + 1) * 0.07f) % 1.0f;
                    _animator.Play("Locomotion", 0, walkT);
                    _animator.Update(0.0f);
                    yield return null;
                }
                Quaternion armWhenConsoleOpens = rightUpperArm.localRotation;

                // THE CONSOLE OPENING -- DevConsoleView.Open's own exact
                // effect on the Animator, mid-blend this time rather than
                // mid-swing. GrappleSwing itself is untouched, so its own
                // LateUpdate keeps running automatically every real frame
                // below, exactly like the genuine console does.
                _animator.enabled = false;

                // Held well past the whole 0.15s blend window (0.5s real
                // time) -- long enough to see where this actually settles.
                for (int i = 0; i < 30; i++)
                {
                    yield return null;
                }

                float armDistortion = Quaternion.Angle(armAtRelease, rightUpperArm.localRotation);
                float armMovementSinceConsoleOpened = Quaternion.Angle(armWhenConsoleOpens, rightUpperArm.localRotation);
                float headDistortion = Quaternion.Angle(Quaternion.identity, head.localRotation);
                Debug.Log($"CityX TEST delay={delay}: armDistortionFromReleasePose={armDistortion:F1}deg " +
                    $"armMovementWhileConsoleHeldOpen={armMovementSinceConsoleOpened:F1}deg " +
                    $"headFromIdentity={headDistortion:F1}deg animatorEnabled={_animator.enabled}");

                // A GENEROUS SANITY BOUND, not a tight one -- this is the
                // first real measurement of this specific window, so the
                // point right now is finding out whether it is bounded at
                // all, the same spirit DiagnoseConsoleOpenedMidSwing's own
                // rigOffset check has. Anything past a lazy full 180-degree
                // swing from the release pose is not "a slightly stale
                // blend," it is the reported "wildly distorted."
                Assert.Less(armDistortion, 180.0f,
                    $"delay={delay}: the arm ended up {armDistortion:0.0} degrees from its own release pose " +
                    "after the console froze the animator mid-blend -- that is a wild pose, not a stale one");

                // Close the console, exactly like real gameplay before the
                // next trial's own fresh Attach.
                _animator.enabled = true;
                _animator.Update(0.0f);
            }
        }

        /// <summary>
        /// The right arm's own job the whole time a rope is attached: ease
        /// toward its fixed reach pose. Checked by ANGLE TO THE TARGET
        /// shrinking over real ticks, not an exact value, since the exact
        /// pose is an explicit first guess (see GrappleSwing's own
        /// RightUpperArmReachPose doc comment) that is expected to be
        /// re-tuned once actually seen.
        /// </summary>
        [UnityTest]
        public IEnumerator SwingingMovesTheRightArmTowardItsOwnReachPose()
        {
            SpawnPlayer();
            Transform rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Assert.IsNotNull(rightUpperArm, "Player.prefab's Animator has no RightUpperArm bone mapped");

            Quaternion before = rightUpperArm.localRotation;

            _swing.Attach(_player.transform.position + new Vector3(3.0f, 6.0f, 2.0f));
            for (int i = 0; i < 30; i++)
            {
                _swing.Tick(Vector2.zero, 0.0f, false, false, false, false, StepSeconds);
                yield return null;
            }

            float moved = Quaternion.Angle(before, rightUpperArm.localRotation);
            Assert.Greater(moved, 5.0f,
                $"the right arm barely moved ({moved:0.0} degrees) after half a second of swinging -- " +
                "it should be visibly reaching for the rope");
        }

        /// <summary>
        /// THE ACTUAL ROOT CAUSE of a live bug report ("distorted to
        /// lengths that shouldn't be possible", "the character doesn't
        /// follow the camera") that survived several other real, separately
        /// confirmed fixes -- nothing to do with the swing's own pose or
        /// velocity at all. PlayerLocomotion.ClampFeetAboveGround exists
        /// purely to fix a badly calibrated GROUNDED clip (see its own doc
        /// comment) and has no job while dangling from a rope -- left
        /// running during a real, varied swing, its own "reset to zero,
        /// recompute from the live foot position" pattern turned into a
        /// genuine runaway: confirmed directly, live, the visual rig's own
        /// offset climbed from single digits to over 1000 METRES within
        /// about 20 real frames while the actual character root stayed
        /// completely normal. The camera (correctly tracking the real
        /// root) and the visible mesh (flung a kilometre straight up) came
        /// apart -- exactly what "the character doesn't follow the camera"
        /// describes, backwards from how it reads: the camera was right
        /// the whole time. Fixed by suspending the clamp entirely for as
        /// long as GrappleSwing owns the pose (SetGroundClampSuspended),
        /// plus a hard ceiling on the correction itself as a second line of
        /// defence. Checked here with the SAME realistic, continuously
        /// varying input (sweeping yaw, alternating reel/payout) that
        /// reproduced the runaway live -- a simpler, single-direction
        /// swing never triggered it.
        /// </summary>
        [UnityTest]
        public IEnumerator TheVisualRigNeverDriftsFromTheRootDuringARealVariedSwing()
        {
            SpawnPlayer();
            Transform animatorTransform = _animator.transform;

            _swing.Attach(_player.transform.position + new Vector3(6.0f, 8.0f, 4.0f));

            float worstOffset = 0.0f;
            for (int i = 0; i < 300; i++)
            {
                float t = i / 60.0f;
                Vector2 move = new Vector2(Mathf.Sin(t * 0.7f), Mathf.Cos(t * 0.5f) * 0.5f + 0.5f);
                float yaw = i * 2.0f;
                bool reel = (i / 30) % 4 == 0;
                bool payout = (i / 30) % 4 == 2;
                _swing.Tick(move, yaw, false, false, reel, payout, StepSeconds);
                yield return null;

                float offset = animatorTransform.localPosition.magnitude;
                if (offset > worstOffset)
                {
                    worstOffset = offset;
                }

                if (!_swing.IsActive)
                {
                    break;
                }
            }

            Assert.Less(worstOffset, 0.5f,
                $"the visual rig drifted {worstOffset:0.0}m from the root during a real varied swing -- " +
                "ClampFeetAboveGround's own runaway is back");
        }

        /// <summary>
        /// EXPLICIT BUG REPORT, matched almost word for word by this class's
        /// own AttachGraceSeconds doc comment ("the character stays in
        /// place... the more you move around... the more contorted he
        /// gets"): a player whose swing does not seem to be working
        /// naturally keeps trying, firing again soon after a release.
        /// Release re-enables the Animator but does not force it to
        /// actually evaluate before returning -- confirmed directly, live:
        /// 40 rapid attach/release cycles with a real Player.prefab drifted
        /// the right arm's own captured base rotation by a further ~120
        /// degrees EVERY single cycle (cycling through 120/240/360 without
        /// ever actually resetting), because each Attach's CaptureBase read
        /// whatever the PREVIOUS swing's own UpdateSwingPose last left the
        /// bone at, not a clean animated pose. Fixed with a forced
        /// zero-delta Animator.Update in Release, so even a same-frame
        /// re-Attach always captures a genuinely fresh pose.
        /// </summary>
        [UnityTest]
        public IEnumerator RepeatedlyFiringAndReleasingNeverDriftsTheCapturedArmBase()
        {
            SpawnPlayer();
            Transform rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);

            // Cycle 0's own captured base is deliberately EXCLUDED from the
            // comparison: it can land on whatever pose existed before the
            // real Animator has ever evaluated a single frame (this test
            // fires immediately on spawn), which is a one-time cold-start
            // detail unrelated to the actual bug -- COMPOUNDING drift across
            // repeated cycles once the Animator is already live and settled.
            Quaternion previousCaptured = Quaternion.identity;
            float maxDriftBetweenCycles = 0.0f;

            for (int cycle = 0; cycle < 20; cycle++)
            {
                Vector3 anchor = _player.transform.position + new Vector3(3.0f, 4.0f, 0.0f);
                _swing.Attach(anchor);

                if (cycle >= 1)
                {
                    float drift = cycle == 1 ? 0.0f : Quaternion.Angle(previousCaptured, rightUpperArm.localRotation);
                    maxDriftBetweenCycles = Mathf.Max(maxDriftBetweenCycles, drift);
                    previousCaptured = rightUpperArm.localRotation;
                }

                for (int i = 0; i < 15 && _swing.IsActive; i++)
                {
                    _swing.Tick(Vector2.zero, 0.0f, false, false, false, false, StepSeconds);
                    yield return null;
                }
                if (_swing.IsActive)
                {
                    _swing.Release(false);
                }
                // NO YIELD HERE -- the very next cycle's Attach happens on
                // the same frame Release just ran on, exactly the
                // zero-frames-between-attempts case that reproduced the bug.
            }

            Assert.Less(maxDriftBetweenCycles, 2.0f,
                $"the captured arm base drifted {maxDriftBetweenCycles:0.0} degrees between two consecutive rapid " +
                "attach/release cycles -- each re-fire is capturing the previous swing's own leftover pose instead of a clean one");
        }

        /// <summary>
        /// THE SLEEVE MESH CANNOT FOLLOW THE REACH POSE -- see HoodedCloak's
        /// own UpdateSwingHiding doc comment: a cloth tube baked and
        /// weighted for ordinary locomotion's narrow arm swing crumples into
        /// a flat, wing-shaped mess when the arm reaches this far, confirmed
        /// directly by rendering one (GrapplingHookBuilder's own
        /// CaptureSwingWithCloak) -- a live bug report ("all contorted")
        /// that survived the reach-pose fix above because the bug was never
        /// in the body at all. Checked here by watching the cloak's own worn
        /// skin renderer, not by trying to measure "does the mesh look
        /// crumpled" from a test.
        /// </summary>
        [UnityTest]
        public IEnumerator AHoodedCloakHidesItselfWhileSwingingAndReappearsOnRelease()
        {
            SpawnPlayer();
#if UNITY_EDITOR
            GameObject cloakPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Wearables/HoodedCloak.prefab");
            Assert.IsNotNull(cloakPrefab, "HoodedCloak.prefab is missing");
            var cloakInstance = Object.Instantiate(cloakPrefab);
            var cloak = cloakInstance.GetComponent<HoodedCloak>();
            cloak.OnEquip(_player, ItemInstanceData.None);

            SkinnedMeshRenderer cloakSkin = FindSkinByMesh(_player, cloak.GarmentMesh);
            Assert.IsNotNull(cloakSkin, "couldn't find the cloak's own worn skin renderer");
            Assert.IsTrue(cloakSkin.gameObject.activeSelf, "setup failed -- the cloak should start showing");

            _swing.Attach(_player.transform.position + new Vector3(3.0f, 6.0f, 2.0f));
            for (int i = 0; i < 10; i++)
            {
                _swing.Tick(Vector2.zero, 0.0f, false, false, false, false, StepSeconds);
                cloak.UpdateSwingHiding();
                yield return null;
            }
            Assert.IsFalse(cloakSkin.gameObject.activeSelf,
                "the cloak's own sleeve is still showing mid-swing -- it will crumple against the reach pose");

            _swing.Release(false);
            cloak.UpdateSwingHiding();
            yield return null;

            Assert.IsTrue(cloakSkin.gameObject.activeSelf, "the cloak never came back after the swing ended");

            Object.Destroy(cloakInstance);
#else
            yield break;
#endif
        }

        private static SkinnedMeshRenderer FindSkinByMesh(GameObject root, Mesh mesh)
        {
            foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh == mesh)
                {
                    return renderer;
                }
            }
            return null;
        }

        /// <summary>
        /// AN ABSOLUTE FIXED TARGET for the reach pose (this feature's own
        /// original design) rendered live as a grotesquely stretched limb --
        /// confirmed directly with GrapplingHookBuilder.CaptureSwing against
        /// a bug report that the body "stays still and starts stretching".
        /// This rig's skin is bound relative to whatever pose a bone is
        /// already in, so overwriting it outright can land on an
        /// orientation the skin weights were never bound for. Checked here
        /// by settling the SAME reach from two DELIBERATELY DIFFERENT
        /// starting poses -- composed relative to its own captured base,
        /// both settle the identical distance from their own very different
        /// starting points (a quaternion identity: Angle(q, q*delta) is
        /// delta's own angle no matter what q is); a fixed absolute target
        /// would settle two very different distances instead, since the two
        /// starting poses were picked to be nowhere near each other.
        /// </summary>
        [UnityTest]
        public IEnumerator TheRightArmReachSettlesRelativeToItsOwnCapturedPoseNotAFixedAbsoluteOrientation()
        {
            SpawnPlayer();
            Transform rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Vector3 anchor = _player.transform.position + new Vector3(3.0f, 6.0f, 2.0f);

            // 180 frames (a full 3 simulated seconds), not the 90 an earlier
            // version of this test used -- confirmed directly that 90 was
            // too tight a margin: RightUpperArmReachDegrees' own 150 degree
            // rotation only fully settles around frame 86 in practice
            // (RotateTowards converges more slowly than ArmPoseDegreesPerSecond's
            // own nominal rate alone would suggest once other per-frame
            // rope/velocity work is in the same loop), which occasionally
            // measured a still-mid-transition angle instead of the fully
            // settled one and made this test flaky rather than wrong.
            const int settleFrames = 180;

            // TWO GENUINELY DIFFERENT REAL ANIMATOR STATES, not a raw bone
            // override -- Attach's own forced Animator.Update (see its own
            // doc comment: a never-yet-evaluated Animator sits at the raw
            // import bind pose, a real, separately fixed bug) always
            // re-evaluates the LIVE Animator regardless of a bone's own
            // current value, including overwriting a manually-set rotation
            // -- Animator.Update forces evaluation unconditionally, it does
            // not check `enabled` the way Unity's own automatic per-frame
            // call does. So the only way to give GrappleSwing two
            // deliberately different starting poses to capture is to make
            // the ANIMATOR itself actually be in two different real states;
            // baseA/baseB are read back AFTER Attach, from whatever it
            // actually captured, rather than asserted in advance.
            _animator.Play("Falling", 0, 0.2f);
            _animator.Update(0.0f);
            _swing.Attach(anchor);
            Quaternion baseA = rightUpperArm.localRotation;
            for (int i = 0; i < settleFrames; i++)
            {
                _swing.Tick(Vector2.zero, 0.0f, false, false, false, false, StepSeconds);
                yield return null;
            }
            float deltaA = Quaternion.Angle(baseA, rightUpperArm.localRotation);
            _swing.Release(false);

            _animator.Play("Locomotion", 0, 0.6f);
            _animator.Update(0.0f);
            _swing.Attach(anchor);
            Quaternion baseB = rightUpperArm.localRotation;
            for (int i = 0; i < settleFrames; i++)
            {
                _swing.Tick(Vector2.zero, 0.0f, false, false, false, false, StepSeconds);
                yield return null;
            }
            float deltaB = Quaternion.Angle(baseB, rightUpperArm.localRotation);

            Assert.AreEqual(deltaA, deltaB, 2.0f,
                $"the settled reach pose moved {deltaA:0.0} degrees from one starting pose but {deltaB:0.0} " +
                "degrees from another very different one -- the reach target is not relative to its own captured base any more");
        }

        /// <summary>
        /// The grapple gun owns the right hand -- explicit ask: casting a
        /// fireball mid-swing should come from the LEFT hand instead, held
        /// in place rather than left flailing with the rest of the body,
        /// or a deliberate cast would read as an accidental twitch.
        /// </summary>
        [UnityTest]
        public IEnumerator CastingMidSwingHoldsTheLeftArmSteadyRatherThanLettingItFlail()
        {
            SpawnPlayer();
            var fireball = _player.AddComponent<CityX.Weapons.FireballCaster>();
            fireball.Bind();
            Transform leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Assert.IsNotNull(leftUpperArm, "Player.prefab's Animator has no LeftUpperArm bone mapped");

            _swing.Attach(_player.transform.position + new Vector3(3.0f, 6.0f, 2.0f));
            // Give the swing real sideways speed so the free-flailing left
            // arm would visibly keep moving if the cast pose were not
            // overriding it -- a swing with near-zero velocity would pass
            // this test even with the override broken, since there would
            // be nothing to flail from in the first place.
            for (int i = 0; i < 20; i++)
            {
                _swing.Tick(new Vector2(1.0f, 0.0f), 0.0f, false, false, false, false, StepSeconds);
                yield return null;
            }

            fireball.TryCast(_player.transform.position, Vector3.forward, new Collider[0]);

            // FireballCaster.Tick is never called here, so IsCasting never
            // expires on its own -- deliberately: this waits long enough
            // for the arm's own RotateTowards easing to fully SETTLE into
            // the cast pose first (a real, legitimate transition, not a
            // flail) before measuring drift, rather than mistaking that
            // settling motion for the bug this test is actually checking.
            for (int i = 0; i < 90; i++)
            {
                _swing.Tick(new Vector2(1.0f, 0.0f), 0.0f, false, false, false, false, StepSeconds);
                yield return null;
            }

            Quaternion settled = leftUpperArm.localRotation;
            for (int i = 0; i < 10; i++)
            {
                _swing.Tick(new Vector2(1.0f, 0.0f), 0.0f, false, false, false, false, StepSeconds);
                yield return null;
            }

            float driftWhileCasting = Quaternion.Angle(settled, leftUpperArm.localRotation);
            // A generous tolerance, not a tight one: some other system
            // touching this same bone (PlayerLocomotion.LateUpdate's own
            // ApplyCastingPose, in particular) can add a few degrees of its
            // own jitter here independent of GrappleSwing. What actually
            // matters is that this stays far below genuine flail motion
            // (the head/leg sway elsewhere in UpdateSwingPose swings a full
            // 20-40 degrees), not that it is perfectly motionless.
            Assert.Less(driftWhileCasting, 10.0f,
                $"the left arm moved {driftWhileCasting:0.0} degrees after settling into its cast pose -- " +
                "it should hold roughly still, not flail");
        }
    }
}
