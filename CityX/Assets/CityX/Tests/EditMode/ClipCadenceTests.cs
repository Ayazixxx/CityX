using CityX.Character;
using CityX.CharacterEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The gaits (ClipCadence, owner 2026-10-13): the built animator plays the run clip as a jog at walking speed (within 20% of its own pace), the walk
    /// clip at its own slow pace, the sprint no more than 20% sped up with the rest taken by the stride (GaitStrides) and the foot locks; the idle is
    /// untouched and a held crouch stays held.
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
        public void TheSplitKeepsThePlayRateAndStrideWithinTheirLimits()
        {
            ClipCadence.Split(4.0f / 4.48f, ClipCadence.MaxRateChange, out float rate, out float stride);
            Assert.AreEqual(0.893f, rate, 0.005f, "a jog 11% slower than the run clip is within the rate limit");
            Assert.AreEqual(1.0f, stride, 0.005f);
            ClipCadence.Split(7.5f / 4.48f, ClipCadence.MaxRateChange, out rate, out stride);
            Assert.AreEqual(ClipCadence.MaxRateChange, rate, 1e-4f, "the sprint takes the whole rate allowance");
            Assert.AreEqual(ClipCadence.MaxStrideChange, stride, 1e-4f, "and the whole stride allowance");
            ClipCadence.Split(4.0f / 1.38f, ClipCadence.MaxSideRateChange, out rate, out stride);
            Assert.AreEqual(ClipCadence.MaxSideRateChange, rate, 1e-4f, "a strafe may be re-paced further");
        }

        [Test]
        public void TheBuiltAnimatorJogsAtWalkingSpeedAndKeepsTheWalkForASlowPush()
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
                bool sawJog = false, sawSprint = false, sawIdle = false, sawSlowWalk = false;
                AnimationClip jogClip = null, sprintClip = null;
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
                    Assert.Greater(clipSpeed, 0.5f, $"{clip.name}: its feet hardly move on the ground ({clipSpeed:0.00} m/s)");
                    bool straight = Mathf.Abs(child.position.x) < 0.01f && child.position.y > 0.0f;
                    float maxRate = straight ? ClipCadence.MaxRateChange : ClipCadence.MaxSideRateChange;
                    Assert.LessOrEqual(child.timeScale, maxRate + 1e-3f, $"{clip.name} at {bodySpeed:0.0} m/s is re-paced past the limit ({child.timeScale:0.00})");
                    Assert.GreaterOrEqual(child.timeScale, 1.0f / maxRate - 1e-3f, $"{clip.name} at {bodySpeed:0.0} m/s is slowed past the limit");
                    if (straight && Mathf.Approximately(child.position.y, PlayerRigBuilder.WalkSpeed))
                    {
                        sawJog = true;
                        jogClip = clip;
                        Assert.AreEqual(4.0f / clipSpeed, child.timeScale, 0.03f, "the jog's pace is not matched to walking speed");
                        Assert.Less(child.timeScale, 1.0f, "4 m/s should play the run clip slowed a little, not a walk sped up");
                    }
                    if (straight && Mathf.Approximately(child.position.y, PlayerRigBuilder.SprintSpeed))
                    {
                        sawSprint = true;
                        sprintClip = clip;
                        Assert.AreEqual(ClipCadence.MaxRateChange, child.timeScale, 1e-3f, "the sprint should take the whole rate allowance");
                    }
                    if (straight && bodySpeed < PlayerRigBuilder.WalkSpeed - 0.5f)
                    {
                        sawSlowWalk = true;
                        Assert.AreEqual(1.0f, child.timeScale, 1e-3f, "the walk at its own pace should play at its own pace");
                        Assert.AreEqual(clipSpeed, bodySpeed, 0.05f, "the walk's sample should sit at the walk's own ground speed");
                    }
                }
                Assert.IsTrue(sawIdle, "the idle is gone");
                Assert.IsTrue(sawJog, "no clip at walking speed");
                Assert.IsTrue(sawSprint, "no clip at sprinting speed");
                Assert.IsTrue(sawSlowWalk, "the walk clip lost its slow sample");
                Assert.AreSame(jogClip, sprintClip, "the jog and the sprint should be the same run clip at two paces");

                GaitStrides gaits = AssetDatabase.LoadAssetAtPath<GaitStrides>(GaitStrides.AssetPath);
                Assert.IsNotNull(gaits, "no gait strides asset was written");
                Assert.AreEqual(ClipCadence.MaxStrideChange, gaits.StrideFor("Locomotion", new Vector2(0.0f, PlayerRigBuilder.SprintSpeed)), 1e-3f, "the sprint should stride longer");
                Assert.AreEqual(1.0f, gaits.StrideFor("Locomotion", new Vector2(0.0f, PlayerRigBuilder.WalkSpeed)), 0.02f, "the jog needs no longer stride");
                Assert.AreEqual(1.0f, gaits.StrideFor("Locomotion", Vector2.zero), 1e-3f, "standing still has no stride");

                BlendTree crouched = Tree(controller, "Crouched");
                if (crouched != null)
                {
                    Assert.Less(crouched.children[0].timeScale, 0.02f, "the held crouch pose is no longer held");   // Unity stores the held child's 0 as 0.01
                }
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }
        }
    }
}
