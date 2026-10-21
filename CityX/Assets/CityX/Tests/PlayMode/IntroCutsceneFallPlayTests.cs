using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The intro played out on the real player over real ground (owner, 2026-10-20: "make it so you are ragdolling falling from the sky ...
    /// there is a jump cut in the cutscene mid fall right before the impact ... make it so your feet are on the ground when you take over
    /// control"): the body falls limp, lands as a ragdoll, gets up, and is handed back standing on the ground; the camera never jumps on the way.
    /// </summary>
    public sealed class IntroCutsceneFallPlayTests
    {
        /// <summary>The most the camera may move in one frame at 60 fps: the fall's top speed (about 35 m/s) is 0.6 m a frame.</summary>
        private const float MaxCameraStepPerFrame = 1.5f;

        /// <summary>How far over or under the ground the feet may be when control is handed back.</summary>
        private const float FeetTolerance = 0.05f;

        private const int MaxFrames = 60 * 60;

        [UnityTest]
        public IEnumerator TheBodyFallsLimpLandsGetsUpAndIsHandedBackStandingWithNoCameraJump()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                Vector3.zero, Quaternion.identity);
            var cameraObject = new GameObject("Camera", typeof(Camera));
            try
            {
                player.GetComponent<PlayerInputReader>().enabled = false;   // as IntroCutsceneStarter does
                var thirdPersonCamera = cameraObject.AddComponent<ThirdPersonCamera>();
                thirdPersonCamera.Target = player.transform;
                thirdPersonCamera.enabled = false;
                yield return null;

                var cutscene = player.AddComponent<IntroCutscene>();
                bool finished = false;
                cutscene.Finished += () => finished = true;
                cutscene.Begin(player.GetComponent<CharacterController>(), thirdPersonCamera, player.GetComponentInChildren<Animator>(),
                    cameraObject.transform, player.GetComponent<PlayerLocomotion>());
                var ragdoll = player.GetComponent<Ragdoll>();

                bool fellLimp = false;
                float worstCameraStep = 0.0f;
                int worstFrame = 0;
                Vector3 lastCamera = cameraObject.transform.position;
                int frame = 0;
                for (; frame < MaxFrames && !finished; frame++)
                {
                    yield return null;
                    fellLimp |= cutscene.IsFalling && ragdoll.IsLimp;
                    float step = Vector3.Distance(lastCamera, cameraObject.transform.position);
                    if (step > worstCameraStep)
                    {
                        worstCameraStep = step;
                        worstFrame = frame;
                    }
                    lastCamera = cameraObject.transform.position;
                }

                Assert.IsTrue(fellLimp, "the body never fell as a ragdoll");
                Assert.IsTrue(cutscene.HasLanded, "the body never hit the ground");
                Assert.IsTrue(finished, $"the intro had not finished after {MaxFrames / 60} s");
                Assert.IsFalse(ragdoll.IsLimp, "control was handed back while the body was still limp");
                Assert.Less(worstCameraStep, MaxCameraStepPerFrame, $"the camera jumped {worstCameraStep:0.00} m on frame {worstFrame}");
                Assert.AreEqual(0.0f, player.transform.position.y, FeetTolerance, "the feet are not on the ground at hand-off");
            }
            finally
            {
                Time.captureFramerate = 0;
                Object.Destroy(player);
                Object.Destroy(cameraObject);
                Object.Destroy(ground);
            }
#else
            yield break;
#endif
        }
    }
}
