using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// A sprinting body's limbs must move the way the clip moves them. For a long time they did not: every few frames of a fast stride a lower leg or arm flipped through
    /// 110 to 150 degrees in a single frame and back (the pelvis "tear" and the flung arms in every sprint screenshot, from any clip). Two writers were on those bones: the
    /// Animator, and the ragdoll's kinematic rigidbodies, which had interpolation switched on and so rewrote each bone's transform every frame to smooth it between physics
    /// steps. A clip sampled directly was clean, the same clip through an Animator was not; interpolation off (or the Animator in Fixed mode) cured it.
    /// </summary>
    public sealed class LimbFlipPlayTests
    {
        /// <summary>A real fast stride turns a lower leg about 40 degrees in one 60 Hz frame at the most; the flip was 110 and up.</summary>
        private const float MostASprintingLimbMayTurnInOneFrame = 75.0f;

        [UnityTest]
        public IEnumerator ASprintingBodysLowerLimbsNeverFlipBetweenFrames()
        {
#if UNITY_EDITOR
            float previousCaptureRate = Time.captureFramerate;
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -0.5f, 0); ground.transform.localScale = new Vector3(600, 1, 600);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var player = Object.Instantiate(prefab, new Vector3(0, 0.05f, -250.0f), Quaternion.identity);
            var locomotion = player.GetComponent<PlayerLocomotion>();
            var animator = player.GetComponentInChildren<Animator>();
            HumanBodyBones[] watched =
            {
                HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
            };
            var previous = new Quaternion[watched.Length];
            float worst = 0.0f;
            HumanBodyBones worstBone = watched[0];
            int worstFrame = 0;
            const float dt = 1.0f / 60.0f;
            for (int i = 0; i < 240; i++)
            {
                locomotion.Tick(new LocomotionInput { Move = Vector2.up, Sprint = true }, dt);
                yield return null;
                for (int b = 0; b < watched.Length; b++)
                {
                    Quaternion now = animator.GetBoneTransform(watched[b]).localRotation;
                    if (i > 90)
                    {
                        float angle = Quaternion.Angle(previous[b], now);
                        if (angle > worst)
                        {
                            worst = angle;
                            worstBone = watched[b];
                            worstFrame = i;
                        }
                    }
                    previous[b] = now;
                }
            }
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            Object.Destroy(player);
            Object.Destroy(ground);
            Assert.Less(worst, MostASprintingLimbMayTurnInOneFrame,
                $"{worstBone} turned {worst:F0} degrees in a single frame (frame {worstFrame}) while sprinting: something other than the clip is writing that bone");
#else
            yield break;
#endif
        }
    }
}
