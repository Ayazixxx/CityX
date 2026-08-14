using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Checks the player rig is actually wired, because the failure mode is
    /// silent.
    ///
    /// A humanoid avatar that fails to build does not throw. The character just
    /// stands in a T-pose forever, and every animation "plays" against nothing.
    /// Everything downstream -- combat, ragdoll, the whole feel of the game --
    /// gets built on top of that before anyone notices. So the rig gets checked
    /// the same way the world does.
    /// </summary>
    public class PlayerRigTests
    {
        private const string PrefabPath = "Assets/CityX/Character/Player.prefab";
        private const string ControllerPath = "Assets/CityX/Character/PlayerAnimator.controller";
        private const string AnimationDirectory = "Assets/CityX/Character/Animations";

        private static GameObject LoadPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab,
                $"no player prefab at {PrefabPath} -- run CityX/Rebuild Player Rig");
            return prefab;
        }

        [Test]
        public void ThePlayerHasAValidHumanoidAvatar()
        {
            GameObject prefab = LoadPrefab();
            var animator = prefab.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator, "the player prefab has no Animator");

            Assert.IsNotNull(animator.avatar,
                "the Animator has no avatar -- every clip will play against nothing and the " +
                "character will stand in a T-pose");
            Assert.IsTrue(animator.avatar.isValid,
                "the avatar exists but is INVALID -- Unity could not map the skeleton");
            Assert.IsTrue(animator.avatar.isHuman,
                "the avatar is not humanoid, so Mixamo clips cannot retarget onto it");
        }

        [Test]
        public void ThePlayerHasItsAnimatorControllerAndLocomotionParameters()
        {
            GameObject prefab = LoadPrefab();
            var animator = prefab.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator.runtimeAnimatorController,
                "no animator controller assigned -- nothing will ever animate");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.IsNotNull(controller, $"no controller at {ControllerPath}");

            bool speed = false, grounded = false, jump = false;
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (parameter.name == "Speed") speed = true;
                if (parameter.name == "Grounded") grounded = true;
                if (parameter.name == "Jump") jump = true;
            }

            Assert.IsTrue(speed, "no Speed parameter -- PlayerLocomotion sets it every frame");
            Assert.IsTrue(grounded, "no Grounded parameter");
            Assert.IsTrue(jump, "no Jump trigger");
        }

        /// <summary>
        /// Root motion must stay OFF. PlayerLocomotion drives position itself;
        /// with root motion on, the clip fights it and the character crawls or
        /// slides depending on which wins.
        /// </summary>
        [Test]
        public void RootMotionIsOffSoTheControllerOwnsMovement()
        {
            GameObject prefab = LoadPrefab();
            var animator = prefab.GetComponentInChildren<Animator>();
            Assert.IsFalse(animator.applyRootMotion,
                "root motion is on -- it will fight PlayerLocomotion for control of position");
        }

        [Test]
        public void ThePlayerHasACapsuleThatMatchesItsStandingHeight()
        {
            GameObject prefab = LoadPrefab();
            var controller = prefab.GetComponent<CharacterController>();
            var locomotion = prefab.GetComponent<PlayerLocomotion>();

            Assert.IsNotNull(controller, "no CharacterController on the player");
            Assert.IsNotNull(locomotion, "no PlayerLocomotion on the player");

            Assert.Greater(locomotion.StandingHeight, 0.5f, "standing height is nonsensical");
            Assert.Less(locomotion.StandingHeight, 4.0f, "standing height is nonsensical");

            // The capsule is sized in Awake, so on the raw prefab only the
            // relationship has to hold: a radius that exceeds half the height
            // makes a capsule Unity cannot build.
            Assert.Less(controller.radius * 2.0f, locomotion.StandingHeight,
                "the capsule is wider than it is tall");
        }

        /// <summary>
        /// Every stage of the attack chain must have a state to play.
        ///
        /// The failure is silent and specific: CrossFade to a state that does
        /// not exist does nothing at all, so the punch still lands and still
        /// hurts while the character stands there. It would look like the hit
        /// detection was broken rather than the animation.
        /// </summary>
        [Test]
        public void EveryChainStageHasAnAnimatorStateToPlay()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.IsNotNull(controller);

            AnimatorStateMachine root = controller.layers[0].stateMachine;
            var stateNames = new System.Collections.Generic.HashSet<string>();
            foreach (ChildAnimatorState child in root.states)
            {
                stateNames.Add(child.state.name);
            }

            foreach (ComboStage stage in ComboChain.For(ComboChain.Unarmed))
            {
                Assert.IsTrue(stateNames.Contains(stage.Animation),
                    $"the chain plays '{stage.Animation}' but the animator has no such state -- " +
                    "the punch would land with no swing");
            }
        }

        /// <summary>
        /// A swing must be able to end. Without a way out, the first punch
        /// leaves the character stuck in that state permanently.
        /// </summary>
        [Test]
        public void EverySwingStateCanReturnToLocomotion()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            AnimatorStateMachine root = controller.layers[0].stateMachine;

            foreach (ComboStage stage in ComboChain.For(ComboChain.Unarmed))
            {
                AnimatorState state = null;
                foreach (ChildAnimatorState child in root.states)
                {
                    if (child.state.name == stage.Animation)
                    {
                        state = child.state;
                        break;
                    }
                }
                if (state == null)
                {
                    continue;   // covered by the test above
                }

                Assert.Greater(state.transitions.Length, 0,
                    $"'{stage.Animation}' has no way out -- the character would be stuck mid-punch");
            }
        }

        /// <summary>
        /// Exactly one clip builds an avatar, and every other clip copies it.
        ///
        /// This is checked at the IMPORTER level rather than by looking at the
        /// finished prefab, because that is where it went wrong and the prefab
        /// does not show it. The loop that configures the dependents used to
        /// skip index 0 of a sorted list, which was correct only while the
        /// avatar source happened to sort first. Pinning the source to idle.fbx
        /// broke it: idle sorts fourth, so it was handed its OWN avatar to copy
        /// from -- a cycle Unity resolves by producing no avatar at all -- while
        /// the clip at index 0 was skipped and quietly built a second, rival
        /// avatar from a boxing stance.
        ///
        /// Nothing threw. The rig rebuilt, logged success, and wrote a prefab
        /// whose Animator had a null avatar, which reads in-game as a character
        /// standing in a T-pose while every clip "plays".
        /// </summary>
        [Test]
        public void ExactlyOneClipOwnsTheAvatarAndTheRestCopyIt()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { AnimationDirectory });
            Assert.Greater(guids.Length, 1, "expected several clips under " + AnimationDirectory);

            var owners = new System.Collections.Generic.List<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                Assert.IsNotNull(importer, $"no model importer for {path}");

                Assert.AreEqual(ModelImporterAnimationType.Human, importer.animationType,
                    $"{path} is not imported as Humanoid, so its clips cannot retarget");

                if (importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel)
                {
                    owners.Add(path);
                    continue;
                }

                Assert.AreEqual(ModelImporterAvatarSetup.CopyFromOther, importer.avatarSetup,
                    $"{path} neither builds an avatar nor copies one");
                Assert.IsNotNull(importer.sourceAvatar,
                    $"{path} is set to copy an avatar but has no source -- it will produce none");
            }

            Assert.AreEqual(1, owners.Count,
                "exactly one clip must build the avatar, but these do: " + string.Join(", ", owners));
            Assert.AreEqual($"{AnimationDirectory}/idle.fbx", owners[0],
                "the avatar must come from the neutral idle stand -- any other rest pose bends " +
                "every clip retargeted through it");
        }

        /// <summary>
        /// The blend tree's thresholds have to be the speeds the controller
        /// actually moves at. If they drift apart the character plays a walk
        /// cycle while travelling at a sprint, which is the classic moonwalk.
        /// </summary>
        /// <summary>
        /// The three punch impact samples are on the prefab.
        ///
        /// player.gd plays one at random on a LANDED hit and nothing on a
        /// whiff, which is most of what makes the sound read as impact rather
        /// than effort. Checked here because the failure mode is silence, and
        /// silence is indistinguishable from "the sound system is fine, this
        /// swing just missed".
        /// </summary>
        [Test]
        public void ThePunchImpactSoundsAreOnTheBody()
        {
            var prefab = LoadPrefab();
            var melee = prefab.GetComponent<MeleeAttacker>();
            Assert.IsNotNull(melee, "the player has no MeleeAttacker");

            Assert.IsNotNull(melee.ImpactSounds, "no impact sounds wired at all");
            Assert.AreEqual(3, melee.ImpactSounds.Length,
                "player.gd carries three punch impact samples and picks one per hit");
            foreach (AudioClip clip in melee.ImpactSounds)
            {
                Assert.IsNotNull(clip,
                    "an empty slot means the random pick can land on nothing and go silent");
            }
        }

        /// <summary>
        /// The blood burst matches weapons/blood_splatter.tscn's own numbers.
        ///
        /// Pinned because they are the difference between a spurt and a
        /// fountain, and nothing about a wrong one throws -- 14 droplets at
        /// 0.4s reads as a hit landing, the same system at 140 for 4s reads as
        /// a special effect.
        /// </summary>
        [Test]
        public void TheBloodSplatterMatchesTheGodotScene()
        {
            var prefab = LoadPrefab();
            var melee = prefab.GetComponent<MeleeAttacker>();
            Assert.IsNotNull(melee.BloodPrefab, "no blood prefab wired to the punch");

            var particles = melee.BloodPrefab.GetComponent<ParticleSystem>();
            Assert.IsNotNull(particles, "the blood prefab has no particle system");
            Assert.IsNotNull(melee.BloodPrefab.GetComponent<BloodSplatter>(),
                "nothing frees the burst, so every punch leaks a GameObject");

            ParticleSystem.MainModule main = particles.main;
            Assert.AreEqual(0.4f, main.startLifetime.constant, 0.001f, "lifetime");
            Assert.AreEqual(1.2f, main.startSpeed.constantMin, 0.001f, "initial_velocity_min");
            Assert.AreEqual(2.8f, main.startSpeed.constantMax, 0.001f, "initial_velocity_max");
            Assert.IsFalse(main.loop, "one_shot -- a looping spray is a wound, not a blow");

            // amount = 14, delivered as a single burst (explosiveness 1.0)
            // rather than a rate.
            var bursts = new ParticleSystem.Burst[particles.emission.burstCount];
            particles.emission.GetBursts(bursts);
            Assert.AreEqual(1, bursts.Length, "expected exactly one burst");
            Assert.AreEqual(14, bursts[0].maxCount, "amount");
            Assert.AreEqual(0.0f, particles.emission.rateOverTime.constant, 0.001f,
                "a steady rate would read as bleeding rather than as impact");

            Assert.AreEqual(28.0f, particles.shape.angle, 0.001f, "spread");
        }

        [Test]
        public void BlendThresholdsMatchTheControllerSpeeds()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.IsNotNull(controller);

            var prefab = LoadPrefab();
            var locomotion = prefab.GetComponent<PlayerLocomotion>();

            BlendTree tree = null;
            foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
            {
                if (child.state.motion is BlendTree found)
                {
                    tree = found;
                    break;
                }
            }
            Assert.IsNotNull(tree, "no blend tree in the controller");

            // BLENDED IN REAL METRES PER SECOND, in the body's own frame. Both
            // halves matter. If the positions stop matching the controller's
            // speeds the feet slide -- the clip's stride no longer covers the
            // ground actually travelled. If it blends on speed ALONE, as it
            // did, the tree can only say how fast and never which way, and
            // walking backwards comes out as a forward stride.
            Assert.AreEqual("MoveX", tree.blendParameter, "the tree blends on the wrong parameter");
            Assert.AreEqual("MoveZ", tree.blendParameterY, "the tree has no lateral axis");
            Assert.GreaterOrEqual(tree.children.Length, 6,
                "expected idle, walk, sprint and the three directional clips");

            // KEYED ON THE ASSET PATH, NOT THE CLIP NAME. Every clip Mixamo
            // exports is internally called "mixamo.com", so all 53 of them
            // share one name and the only thing telling walk_left from sprint
            // is which FBX it came out of.
            var positions = new Dictionary<string, Vector2>();
            foreach (ChildMotion child in tree.children)
            {
                Assert.IsNotNull(child.motion, "a blend child has no clip");
                string path = AssetDatabase.GetAssetPath(child.motion);
                positions[System.IO.Path.GetFileNameWithoutExtension(path)] = child.position;
            }

            Vector2 At(string clip)
            {
                if (positions.TryGetValue(clip, out Vector2 found))
                {
                    return found;
                }
                Assert.Fail($"no blend child from {clip}.fbx -- found {string.Join(", ", positions.Keys)}");
                return Vector2.zero;
            }

            Assert.AreEqual(Vector2.zero, At("idle"), "idle should sit at a standstill");
            Assert.AreEqual(new Vector2(0.0f, locomotion.WalkSpeed), At("walking"),
                "the walk clip is not placed at PlayerLocomotion.WalkSpeed");
            Assert.AreEqual(new Vector2(0.0f, locomotion.SprintSpeed), At("sprint"),
                "the sprint clip is not placed at PlayerLocomotion.SprintSpeed");

            // The three that were unreachable while facing followed velocity.
            Assert.AreEqual(new Vector2(0.0f, -locomotion.WalkSpeed), At("walk_backward"));
            Assert.AreEqual(new Vector2(-locomotion.WalkSpeed, 0.0f), At("walk_left"));
            Assert.AreEqual(new Vector2(locomotion.WalkSpeed, 0.0f), At("walk_right"));

            // NOTHING SIDEWAYS ABOVE WALKING PACE, which is what lets each
            // lateral axis carry one clip rather than two: PlayerLocomotion
            // refuses to sprint in any direction but forward.
            foreach (KeyValuePair<string, Vector2> pair in positions)
            {
                if (Mathf.Abs(pair.Value.x) > 0.01f)
                {
                    Assert.LessOrEqual(Mathf.Abs(pair.Value.x), locomotion.WalkSpeed + 0.01f,
                        $"{pair.Key} is placed at a sideways speed the controller never produces");
                }
            }
        }
    }
}
