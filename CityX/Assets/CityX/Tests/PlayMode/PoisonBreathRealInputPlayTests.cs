using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>Key 4 in the real scene: it blows a cloud out, the caster is not poisoned by it, and it has a cooldown.</summary>
    public sealed class PoisonBreathRealInputPlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            foreach (PoisonGasCloud c in Object.FindObjectsByType<PoisonGasCloud>(FindObjectsSortMode.None)) Object.Destroy(c.gameObject);
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator PressingFourBlowsACloudThatNeverPoisonsTheCaster()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            var caster = vitals.GetComponent<PoisonBreathCaster>();
            Assert.IsNotNull(caster, "the player has no poison gas -- rebuild the scene");
            Assert.IsNotNull(caster.CloudPrefab);
            var locomotion = vitals.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 400 && !locomotion.IsGrounded; i++) yield return null;
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0, Object.FindObjectsByType<PoisonGasCloud>(FindObjectsSortMode.None).Length, "setup: no cloud yet");
            float healthBefore = vitals.Health;

            Press(_keyboard.digit4Key);
            yield return null;
            Release(_keyboard.digit4Key);
            yield return null;
            Assert.IsTrue(caster.IsCasting, "4 did not start the scream");
            Assert.AreEqual(0, Object.FindObjectsByType<PoisonGasCloud>(FindObjectsSortMode.None).Length, "the gas left before the head snapped forward");
            Assert.IsTrue(vitals.GetComponent<PlayerAnimationDriver>().IsPlaying(PoisonBreathCaster.AnimationState), "the Scream animation is not playing");

            // The gas leaves the mouth when the head snaps forward.
            yield return new WaitForSeconds(caster.ReleaseSeconds + 0.1f);
            PoisonGasCloud[] clouds = Object.FindObjectsByType<PoisonGasCloud>(FindObjectsSortMode.None);
            Assert.AreEqual(1, clouds.Length, "the scream never breathed a cloud");
            Transform head = vitals.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
            Assert.Less(Vector3.Distance(clouds[0].transform.position, head.position), 2.5f, "the cloud did not come from around the mouth");

            // The cloud swells round and past the caster; the caster must be untouched.
            yield return new WaitForSeconds(2.0f);
            Assert.IsNull(vitals.GetComponent<PoisonEffect>(), "the gas poisoned the one who blew it");
            Assert.AreEqual(healthBefore, vitals.Health, 0.01f);

            Press(_keyboard.digit4Key);
            yield return null;
            Release(_keyboard.digit4Key);
            yield return null;
            Assert.AreEqual(1, Object.FindObjectsByType<PoisonGasCloud>(FindObjectsSortMode.None).Length, "a second cloud inside the cooldown");
#else
            yield break;
#endif
        }
    }
}
