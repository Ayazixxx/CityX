using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The city's sound is flat, so every body playing it adds a whole layer: spawning swordsmen turned the wind up.
    /// The real prefabs, because what goes wrong is which bodies carry it.
    /// </summary>
    public sealed class CityAmbienceBodiesPlayTests
    {
        private GameObject _body;
        private Scene _scene;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_body != null) Object.Destroy(_body);
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        public static int LayersOfTheCity(GameObject body)
        {
            AudioClip city = body.GetComponent<PlayerAudio>().CityAmbience;
            Assert.IsNotNull(city, $"{body.name} has no city sound to test with");
            int layers = 0;
            foreach (AudioSource source in body.GetComponents<AudioSource>())
            {
                if (source.clip == city && source.isPlaying)
                {
                    layers++;
                }
            }
            return layers;
        }

        private GameObject Spawn(string prefab)
        {
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Character/{prefab}.prefab");
            Assert.IsNotNull(asset, $"{prefab}.prefab is missing");
            _body = Object.Instantiate(asset);
            return _body;
#else
            Assert.Ignore("needs the editor's asset database");
            return null;
#endif
        }

        [Test]
        public void ThePlayersOwnBodyPlaysTheCityOnce()
        {
            Assert.AreEqual(1, LayersOfTheCity(Spawn("Player")));
        }

        [UnityTest]
        public IEnumerator TheRealScenesPlayerPlaysTheCityOnceInSingleplayer()
        {
#if UNITY_EDITOR
            // The real scene renders, and a headless run cannot make its render textures.
            LogAssert.ignoreFailingMessages = true;
            const string scenePath = "Assets/CityX/Scenes/CityMassing.unity";
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;

            _scene = SceneManager.GetSceneByPath(scenePath);
            Assert.IsTrue(_scene.IsValid() && _scene.isLoaded, "the real scene never finished loading");

            var identity = Object.FindFirstObjectByType<NetworkedPlayerIdentity>();
            Assert.IsNotNull(identity, "the real scene's player has no NetworkedPlayerIdentity");
            Assert.AreEqual(1, LayersOfTheCity(identity.gameObject));
#else
            yield break;
#endif
        }

        [TestCase("Swordsman")]
        [TestCase("Shoota")]
        [TestCase("Giant")]
        public void AnEnemyAddsNoLayerOfTheCity(string enemy)
        {
            Assert.AreEqual(0, LayersOfTheCity(Spawn(enemy)));
        }

        [Test]
        public void SomebodyElsesCopyFallsSilentAndComesBackIfItBecomesOurs()
        {
            GameObject body = Spawn("Player");
            PlayerAudio audio = body.GetComponent<PlayerAudio>();

            audio.SetOwnBody(false);
            Assert.AreEqual(0, LayersOfTheCity(body));

            audio.SetOwnBody(true);
            Assert.AreEqual(1, LayersOfTheCity(body));
        }
    }
}
