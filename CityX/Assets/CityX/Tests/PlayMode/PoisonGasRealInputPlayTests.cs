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
    public sealed class PoisonGasRealInputPlayTests : InputTestFixture
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
        public IEnumerator TearDown()
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
            var caster = vitals.GetComponent<PoisonGasCaster>();
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
            Assert.AreEqual(1, Object.FindObjectsByType<PoisonGasCloud>(FindObjectsSortMode.None).Length, "4 did not blow a cloud");

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
