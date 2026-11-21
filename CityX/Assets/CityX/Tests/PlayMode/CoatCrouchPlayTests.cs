using System.Collections;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A long coat's skirt standing and crouch walking (owner, 2026-11-21, of the Midnight Coat after its skirt was first kept out of the legs:
    /// "there is skin visible around the crotch", "the bottom of the coat starts spazzing out when holding control and control walking"). The
    /// real scene and player, Ctrl and W held as a player would. Standing, the skirt bones stay where the legs put them (pushed off legs the coat
    /// was cut round, its front opened); crouch walking, they turn no faster than the walk turns them (pushed off folded legs, a panel flipped
    /// half round in a frame).
    /// </summary>
    [TestFixture(ItemRegistry.MidnightCoat)]
    [TestFixture(ItemRegistry.GreenFurHoodedParka)]
    public sealed class CoatCrouchPlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        /// <summary>The most a skirt bone may sit off its hip joint standing still, metres: a touch of a push, no opening of the front.</summary>
        private const float StandingShift = 0.025f;

        /// <summary>The most a skirt bone may turn in a frame crouch walking, degrees: the walk's own swing, eased (without the push, about 10).</summary>
        private const float CrouchWalkTurn = 15.0f;

        private readonly string _coat;
        private Scene _scene;
        private Keyboard _keyboard;

        public CoatCrouchPlayTests(string coatItemId)
        {
            _coat = coatItemId;
        }

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator TheSkirtStaysPutStandingAndSteadyCrouchWalking()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            PlayerInputReader input = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                if (input == null) input = root.GetComponentInChildren<PlayerInputReader>(true);
            }
            Assert.IsNotNull(input, "no player in the real scene");
            GameObject player = input.gameObject;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 600 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            Time.captureFramerate = 60;
            Assert.IsTrue(player.GetComponent<Inventory>().EquipItemId(_coat, player), $"the {_coat} could not be put on");
            yield return null;
            Animator body = player.GetComponentInChildren<Animator>();
            Transform hips = body.GetBoneTransform(HumanBodyBones.Hips);
            Transform[] skirt =
            {
                TransformSearch.FindDeep(player.transform, "skirt_front_l"), TransformSearch.FindDeep(player.transform, "skirt_back_l"),
                TransformSearch.FindDeep(player.transform, "skirt_front_r"), TransformSearch.FindDeep(player.transform, "skirt_back_r"),
            };
            Transform[] thighs = { body.GetBoneTransform(HumanBodyBones.LeftUpperLeg), body.GetBoneTransform(HumanBodyBones.RightUpperLeg) };
            Assert.IsNotNull(skirt[0], "the coat has no skirt bones");

            float standingShift = 0.0f;
            for (int f = 0; f < 40; f++)
            {
                yield return null;
                for (int h = 0; h < 4; h++)
                {
                    standingShift = Mathf.Max(standingShift, Vector3.Distance(skirt[h].position, thighs[h / 2].position));
                }
            }

            Press(_keyboard.leftCtrlKey);
            yield return Frames(40);
            Press(_keyboard.wKey);
            var last = new Quaternion[4];
            float fastest = 0.0f;
            for (int f = 0; f < 150; f++)
            {
                yield return null;
                for (int h = 0; h < 4; h++)
                {
                    Quaternion local = Quaternion.Inverse(hips.rotation) * skirt[h].rotation;
                    if (f > 0)
                    {
                        fastest = Mathf.Max(fastest, Quaternion.Angle(last[h], local));
                    }
                    last[h] = local;
                }
            }
            Release(_keyboard.wKey);
            Release(_keyboard.leftCtrlKey);

            Debug.Log($"{_coat}: standing, the skirt bones at most {standingShift * 100.0f:0.0} cm off the hip joints; crouch walking, at most {fastest:0.0} degrees in a frame");
            Assert.Less(standingShift, StandingShift, "standing still, the skirt is pushed off the legs it was cut round (its front opens)");
            Assert.Less(fastest, CrouchWalkTurn, "crouch walking, the skirt jumps from frame to frame");
#else
            Assert.Ignore("needs the editor to load the scene.");
            yield break;
#endif
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }
    }
}
