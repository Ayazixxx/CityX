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
            Assert.AreEqual("Speed", tree.blendParameter, "the tree blends on the wrong parameter");
            Assert.GreaterOrEqual(tree.children.Length, 3, "expected idle, walk and sprint");

            float[] thresholds = new float[tree.children.Length];
            for (int i = 0; i < tree.children.Length; i++)
            {
                thresholds[i] = tree.children[i].threshold;
            }

            Assert.AreEqual(0.0f, thresholds[0], 0.01f, "the first blend child should be idle at 0");
            Assert.AreEqual(locomotion.WalkSpeed, thresholds[1], 0.01f,
                "the walk threshold does not match PlayerLocomotion.WalkSpeed");
            Assert.AreEqual(locomotion.SprintSpeed, thresholds[thresholds.Length - 1], 0.01f,
                "the top threshold does not match PlayerLocomotion.SprintSpeed");
        }
    }
}
