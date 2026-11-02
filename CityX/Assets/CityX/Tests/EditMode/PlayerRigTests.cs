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
                // The unarmed strikes are PROCEDURAL (FistStrikes / FistPoseDriver): no animator state, so the legs keep walking under them.
                // What must be true instead is that the strike exists, or the punch would land with no swing.
                Assert.IsTrue(FistStrikes.Has(stage.Animation) || stateNames.Contains(stage.Animation),
                    $"the chain plays '{stage.Animation}' but there is neither a procedural strike nor an animator state for it -- " +
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
        /// The blend tree's thresholds have to be the speeds the controller
        /// actually moves at. If they drift apart the character plays a walk
        /// cycle while travelling at a sprint, which is the classic moonwalk.
        /// </summary>
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
            //
            // "_stride" STRIPPED OFF THE END: the walk/sprint/directional
            // children are PlayerRigBuilder's own widened-swing duplicates
            // (ExaggerateStride), named "<clip>_stride.anim" rather than
            // "<clip>.fbx" -- a different asset, but still the thing standing
            // in for that clip in the tree, so it should still answer to the
            // clip's own name here.
            // A clip may sit at more than one sample (the run clip is both the jog at walking speed and the sprint: ClipCadence).
            var positions = new Dictionary<string, List<Vector2>>();
            foreach (ChildMotion child in tree.children)
            {
                Assert.IsNotNull(child.motion, "a blend child has no clip");
                string path = AssetDatabase.GetAssetPath(child.motion);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (name.EndsWith("_stride"))
                {
                    name = name.Substring(0, name.Length - "_stride".Length);
                }
                if (name.EndsWith("_paced"))
                {
                    name = name.Substring(0, name.Length - "_paced".Length);
                }
                // The locomotion clips are the Godot character's own baked motion (godot_walk.anim ...), which answers to the Mixamo clip it was made from.
                switch (name)
                {
                    case "godot_walk": name = "walking"; break;
                    case "godot_run": name = "sprint"; break;
                    default:
                        if (name.StartsWith("godot_")) name = name.Substring("godot_".Length);
                        break;
                }
                if (!positions.TryGetValue(name, out List<Vector2> list))
                {
                    positions[name] = list = new List<Vector2>();
                }
                list.Add(child.position);
            }

            List<Vector2> AllAt(string clip)
            {
                if (positions.TryGetValue(clip, out List<Vector2> found))
                {
                    return found;
                }
                Assert.Fail($"no blend child from {clip}.fbx -- found {string.Join(", ", positions.Keys)}");
                return new List<Vector2>();
            }

            Vector2 At(string clip)
            {
                List<Vector2> all = AllAt(clip);
                Assert.AreEqual(1, all.Count, $"{clip} sits at more than one sample");
                return all[0];
            }

            Assert.AreEqual(Vector2.zero, At("idle"), "idle should sit at a standstill");
            // The walk clip at walking speed and the run at sprinting speed (the jog at walking speed was tried and refused, 2026-10-13: "he shouldn't
            // be pumping his arms like that when he is just walking"; ClipCadence.GaitSwapTree).
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
            foreach (KeyValuePair<string, List<Vector2>> pair in positions)
            {
                foreach (Vector2 at in pair.Value)
                {
                    if (Mathf.Abs(at.x) > 0.01f)
                    {
                        Assert.LessOrEqual(Mathf.Abs(at.x), locomotion.WalkSpeed + 0.01f,
                            $"{pair.Key} is placed at a sideways speed the controller never produces");
                    }
                }
            }
        }

        /// <summary>
        /// A gun aimed and the two-handed sword side-step with the plain side-steps (docs/MOVEMENT.md): their own, the Mixamo pistol and
        /// great-sword strafes, squatted bow-legged (owner, 2026-11-01: "when you move sideways with some weapons you looks like your squatting").
        /// </summary>
        [Test]
        public void TheGunAndTheTwoHandedSwordSideStepAsThePlainWalkDoes()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.IsNotNull(controller);
            BlendTree plain = TreeOf(controller, "Locomotion");
            foreach (string name in new[] { "GunLocomotion", "SwordLocomotion" })
            {
                BlendTree tree = TreeOf(controller, name);
                foreach (float side in new[] { -1.0f, 1.0f })
                {
                    Assert.AreEqual(SideStep(plain, side), SideStep(tree, side), $"{name}'s {(side < 0 ? "left" : "right")} side-step is not the plain one");
                }
            }
        }

        private static BlendTree TreeOf(AnimatorController controller, string state)
        {
            foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
            {
                if (child.state.name == state && child.state.motion is BlendTree tree)
                {
                    return tree;
                }
            }
            Assert.Fail($"no {state} blend tree");
            return null;
        }

        /// <summary>The clip at the straight side-step sample (MoveZ 0) on the side <paramref name="side"/> points.</summary>
        private static Motion SideStep(BlendTree tree, float side)
        {
            foreach (ChildMotion child in tree.children)
            {
                if (Mathf.Abs(child.position.y) < 0.01f && child.position.x * side > 0.1f && !(child.motion is BlendTree))
                {
                    return child.motion;
                }
            }
            return null;
        }

        /// <summary>
        /// The two-handed pistol walk only while aiming (owner, 2026-11-01: "i only want him only the pistol with two hands like that when holding
        /// right click ... have normal walk leave the arm normal just carrying it with 1 hand"): the way from the plain walk into the gun's walk
        /// needs Aiming, and the gun's walk goes back to the plain one when the aim is let go.
        /// </summary>
        [Test]
        public void ThePistolWalkPlaysOnlyWhileAiming()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.IsNotNull(controller);
            AnimatorState locomotion = null, gun = null;
            foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
            {
                if (child.state.name == "Locomotion") locomotion = child.state;
                if (child.state.name == "GunLocomotion") gun = child.state;
            }
            Assert.IsNotNull(locomotion);
            Assert.IsNotNull(gun);
            bool anyIn = false;
            foreach (AnimatorStateTransition transition in locomotion.transitions)
            {
                if (transition.destinationState != gun)
                {
                    continue;
                }
                anyIn = true;
                Assert.IsTrue(System.Array.Exists(transition.conditions, c => c.parameter == "Aiming" && c.mode == AnimatorConditionMode.If),
                    "the plain walk goes into the two-handed pistol walk without aiming");
            }
            Assert.IsTrue(anyIn, "nothing leads into the pistol walk");
            Assert.IsTrue(System.Array.Exists(gun.transitions, t => t.destinationState == locomotion
                && System.Array.Exists(t.conditions, c => c.parameter == "Aiming" && c.mode == AnimatorConditionMode.IfNot)),
                "letting go of the aim does not leave the pistol walk");
        }
    }
}
