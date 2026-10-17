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
    /// <summary>The ice shards spell from the real key in the real scene: 3 starts a volley of four shards, a third of a second long, and nothing else gets in its way.</summary>
    public sealed class IceShardRealInputPlayTests : InputTestFixture
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
            foreach (IceShardProjectile shard in Object.FindObjectsByType<IceShardProjectile>(FindObjectsSortMode.None))
            {
                Object.Destroy(shard.gameObject);
            }
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private static IEnumerator Seconds(float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Time.deltaTime)
            {
                yield return null;
            }
        }

        private static int ShardsInTheWorld()
        {
            return Object.FindObjectsByType<IceShardProjectile>(FindObjectsSortMode.None).Length;
        }

        [UnityTest]
        public IEnumerator PressingThreeFiresFourShardsOneAfterAnother()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            Assert.IsNotNull(vitals, "no player in the scene");
            var caster = vitals.GetComponent<IceShardCaster>();
            Assert.IsNotNull(caster, "the player has no ice shards spell -- rebuild the scene");
            Assert.IsNotNull(caster.ShardPrefab, "the spell has no shard prefab");
            var locomotion = vitals.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 400 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            yield return Seconds(0.3f);
            Assert.AreEqual(0, ShardsInTheWorld(), "setup: nothing should be flying yet");

            Press(_keyboard.digit3Key);
            yield return null;
            Release(_keyboard.digit3Key);
            yield return null;
            Assert.IsTrue(caster.IsVolleying || caster.ShotsFired == 4, "3 did not start a volley");
            Assert.GreaterOrEqual(ShardsInTheWorld(), 1, "the first shard should leave at once");
            Assert.Less(ShardsInTheWorld(), 4, "all four left in the same instant");

            yield return Seconds(0.6f);
            Assert.AreEqual(4, caster.ShotsFired, "a volley is four shards");
            Assert.AreEqual(4, ShardsInTheWorld(), "four shards, each still in the air or stuck");
            Assert.IsFalse(caster.IsVolleying);

            // Pressing again straight away does nothing: the cooldown.
            Press(_keyboard.digit3Key);
            yield return null;
            Release(_keyboard.digit3Key);
            yield return Seconds(0.3f);
            Assert.AreEqual(4, ShardsInTheWorld(), "a second volley inside the cooldown");
#else
            yield break;
#endif
        }
    }
}
