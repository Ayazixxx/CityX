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
                Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
                Object.Destroy(player);
                Object.Destroy(cameraObject);
                Object.Destroy(ground);
            }
#else
            yield break;
#endif
        }

        /// <summary>The fall's first stretch, while the body tips over from standing onto its back: not judged.</summary>
        private const int TippingFrames = 90;

        /// <summary>The fall's last stretch, near the ground: not judged (the camera is beside the body and the landing is its own test).</summary>
        private const float JudgedDownTo = 20.0f;

        [UnityTest]
        public IEnumerator TheBodyFallsFlatOnItsBackWithItsArmsAndLegsThrownAbout()
        {
#if UNITY_EDITOR
            // Owner, 2026-10-22: "make it so you are laying flat horizontal during the fall cutscene at the beginning instead of vertical. and
            // make some amount of flailing in the air like there is wind, so he isnt just still during the whole fall".
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                Vector3.zero, Quaternion.identity);
            var cameraObject = new GameObject("Camera", typeof(Camera));
            try
            {
                player.GetComponent<PlayerInputReader>().enabled = false;
                var thirdPersonCamera = cameraObject.AddComponent<ThirdPersonCamera>();
                thirdPersonCamera.Target = player.transform;
                thirdPersonCamera.enabled = false;
                yield return null;

                var cutscene = player.AddComponent<IntroCutscene>();
                Animator animator = player.GetComponentInChildren<Animator>();
                cutscene.Begin(player.GetComponent<CharacterController>(), thirdPersonCamera, animator, cameraObject.transform,
                    player.GetComponent<PlayerLocomotion>());
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck) ?? animator.GetBoneTransform(HumanBodyBones.Head);
                Transform leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                Transform rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                Transform foot = animator.GetBoneTransform(HumanBodyBones.RightFoot);

                int falling = 0, judged = 0, flat = 0;
                float angles = 0.0f;
                Bounds handReach = default, footReach = default;
                for (int frame = 0; frame < MaxFrames && !cutscene.HasLanded; frame++)
                {
                    yield return null;
                    if (!cutscene.IsFalling || ++falling < TippingFrames || hips.position.y < JudgedDownTo)
                    {
                        continue;
                    }
                    Vector3 front = FallFlail.Front(hips.position, neck.position, leftShoulder.position, rightShoulder.position);
                    angles += Vector3.Angle(front, Vector3.up);
                    if (Vector3.Angle(front, Vector3.up) < 30.0f)
                    {
                        flat++;
                    }
                    // Where the hand and foot are, seen from the body (its own turning does not count), in metres.
                    Quaternion unturn = Quaternion.Inverse(hips.rotation);
                    Vector3 handSeen = unturn * (hand.position - hips.position), footSeen = unturn * (foot.position - hips.position);
                    if (judged++ == 0)
                    {
                        handReach = new Bounds(handSeen, Vector3.zero);
                        footReach = new Bounds(footSeen, Vector3.zero);
                    }
                    handReach.Encapsulate(handSeen);
                    footReach.Encapsulate(footSeen);
                }

                Assert.IsTrue(cutscene.HasLanded, "the body never hit the ground");
                Assert.Greater(judged, 200, "too short a fall to judge");
                Assert.Greater(flat, judged * 0.8f, $"the body lay flat on its back for only {flat} of {judged} frames of the fall " +
                    $"(its chest {angles / Mathf.Max(1, judged):0} degrees from facing the sky on average)");
                float handMoved = handReach.size.magnitude, footMoved = footReach.size.magnitude;
                Assert.Greater(handMoved, 0.3f, $"the arms hung still in the wind (the hand moved {handMoved:0.00} m)");
                Assert.Greater(footMoved, 0.2f, $"the legs hung still in the wind (the foot moved {footMoved:0.00} m)");
            }
            finally
            {
                Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
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
