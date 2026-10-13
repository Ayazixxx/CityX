using CityX.CharacterEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The walk and run clips are played at the pace that keeps their feet on the ground (ClipCadence, owner 2026-10-13: "without sliding feet"):
    /// the built animator's blend children carry a time scale of body speed over the clip's own measured ground speed, the idle is untouched, and
    /// a held crouch stays held.
    /// </summary>
    public sealed class ClipCadenceTests
    {
        private const string PrefabPath = "Assets/CityX/Character/Player.prefab";
        private const string ControllerPath = "Assets/CityX/Character/PlayerAnimator.controller";

        private static BlendTree Tree(AnimatorController controller, string state)
        {
            foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
            {
                if (child.state.name == state) return child.state.motion as BlendTree;
            }
            return null;
        }

        [Test]
        public void TheWalkAndSprintClipsArePacedToTheBodyAndTheIdleIsNot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.IsNotNull(prefab);
            Assert.IsNotNull(controller);
            BlendTree locomotion = Tree(controller, "Locomotion");
            Assert.IsNotNull(locomotion, "no Locomotion blend tree");

            GameObject rig = Object.Instantiate(prefab);
            try
            {
                bool sawWalk = false, sawSprint = false, sawIdle = false;
                foreach (ChildMotion child in locomotion.children)
                {
                    if (!(child.motion is AnimationClip clip)) continue;
                    float bodySpeed = child.position.magnitude;
                    if (bodySpeed < ClipCadence.MinBodySpeed)
                    {
                        sawIdle = true;
                        Assert.AreEqual(1.0f, child.timeScale, 1e-4f, "the idle was re-paced");
                        continue;
                    }
                    float clipSpeed = ClipCadence.GroundSpeed(clip, rig);
                    Assert.Greater(clipSpeed, 0.6f, $"{clip.name}: its feet hardly move on the ground (measured {clipSpeed:0.00} m/s)");
                    Assert.Less(clipSpeed, 6.0f, $"{clip.name}: an implausible ground speed ({clipSpeed:0.00} m/s)");
                    Assert.AreEqual(ClipCadence.TimeScaleFor(bodySpeed, clipSpeed), child.timeScale, 0.02f,
                        $"{clip.name} at {bodySpeed:0.0} m/s is not paced to its ground speed of {clipSpeed:0.00} m/s");
                    if (Mathf.Approximately(child.position.y, PlayerRigBuilder.WalkSpeed) && child.position.x == 0.0f)
                    {
                        sawWalk = true;
                        Assert.Greater(child.timeScale, 1.3f, "the walk clip is Mixamo-paced and must be sped up to keep its feet on the ground");
                    }
                    if (Mathf.Approximately(child.position.y, PlayerRigBuilder.SprintSpeed))
                    {
                        sawSprint = true;
                        Assert.Greater(child.timeScale, 1.0f, "the sprint clip must be sped up to keep its feet on the ground");
                    }
                }
                Assert.IsTrue(sawWalk && sawSprint && sawIdle, "the Locomotion tree lost its walk, sprint or idle");

                BlendTree crouched = Tree(controller, "Crouched");
                if (crouched != null)
                {
                    Assert.Less(crouched.children[0].timeScale, 0.02f, "the held crouch pose is no longer held");   // Unity stores the held child's 0 as 0.01
                    Assert.Greater(crouched.children[1].timeScale, 1.0f, "the crouch walk is not paced");
                }
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }
        }
    }
}
