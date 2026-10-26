using System.Collections;
using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The real Wraith prefab in play (owner, 2026-10-26: "make him float around"): it hovers at its height over the ground, bobbing; it floats
    /// about and stays near where it was born; it looks at the player near it; killed, it fades and is gone; and /spawn wraith through the real
    /// console puts one in the city.
    /// </summary>
    public sealed class WraithPlayTests
    {
        private const string PrefabPath = "Assets/CityX/Character/Wraith.prefab";
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        private GameObject _ground;
        private GameObject _wraith;
        private GameObject _player;
        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "TestGround";
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureFramerate = 0;
            Object.Destroy(_ground);
            if (_wraith != null) Object.Destroy(_wraith);
            if (_player != null) Object.Destroy(_player);
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private Wraith Spawn(Vector3 at)
        {
#if UNITY_EDITOR
            _wraith = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), at, Quaternion.identity);
            return _wraith.GetComponent<Wraith>();
#else
            return null;
#endif
        }

        [UnityTest]
        public IEnumerator ItHoversAtItsHeightOverTheGroundBobbing()
        {
            Wraith wraith = Spawn(new Vector3(0.0f, 3.0f, 0.0f));   // dropped in high: it eases down onto its height
            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }
            float lowest = float.MaxValue, highest = float.MinValue;
            for (int i = 0; i < 60 * 4; i++)
            {
                float y = wraith.transform.position.y;
                lowest = Mathf.Min(lowest, y);
                highest = Mathf.Max(highest, y);
                yield return null;
            }
            float slack = 0.06f;
            Assert.GreaterOrEqual(lowest, WraithDrift.HoverHeight - WraithDrift.BobAmplitude - slack, "it sank below its hover height");
            Assert.LessOrEqual(highest, WraithDrift.HoverHeight + WraithDrift.BobAmplitude + slack, "it rose above its hover height");
            Assert.Greater(highest - lowest, WraithDrift.BobAmplitude, "it hangs dead still: no bob");
        }

        [UnityTest]
        public IEnumerator ItFloatsAboutAndStaysNearHome()
        {
            Wraith wraith = Spawn(new Vector3(0.0f, WraithDrift.HoverHeight, 0.0f));
            Vector3 home = wraith.transform.position;
            Vector3 last = home;
            float travelled = 0.0f, furthest = 0.0f;
            bool lingered = false, drifted = false;
            for (int i = 0; i < 60 * 20; i++)
            {
                yield return null;
                Vector3 now = wraith.transform.position;
                Vector3 step = now - last;
                step.y = 0.0f;
                travelled += step.magnitude;
                Vector3 fromHome = now - home;
                fromHome.y = 0.0f;
                furthest = Mathf.Max(furthest, fromHome.magnitude);
                last = now;
                lingered |= wraith.IsLingering;
                drifted |= wraith.Speed > WraithDrift.CruiseSpeed * 0.9f;
            }
            Assert.Greater(travelled, 6.0f, "in twenty seconds it hardly moved");
            Assert.LessOrEqual(furthest, WraithDrift.HomeRadius + 3.0f, "it drifted away from home");
            Assert.IsTrue(drifted, "it never got up to speed");
            Assert.IsTrue(lingered, "it never lingered");
        }

        [UnityTest]
        public IEnumerator ItTurnsItsHeadToThePlayerNearIt()
        {
#if UNITY_EDITOR
            Wraith wraith = Spawn(new Vector3(0.0f, WraithDrift.HoverHeight, 0.0f));
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(4.0f, 0.05f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            yield return null;
            int looked = 0;
            const int frames = 60 * 4;
            for (int i = 0; i < frames; i++)
            {
                // Kept ahead of it and off to its right, wherever it drifts: within its neck's turn, but not where its body faces.
                _player.transform.position = wraith.transform.position + wraith.transform.forward * 4.0f + wraith.transform.right * 1.5f
                    - Vector3.up * WraithDrift.HoverHeight;
                yield return null;
                yield return null;
                Vector3 eye = _player.transform.position + Vector3.up * Wraith.PlayerEyeHeight;
                Vector3 toEye = eye - wraith.Head.position;
                float headOff = Vector3.Angle(wraith.Head.forward, toEye);
                float bodyOff = Vector3.Angle(wraith.transform.forward, toEye);
                looked += headOff < 6.0f && bodyOff > 12.0f ? 1 : 0;
            }
            Assert.Greater(looked, frames * 0.8f, $"it looked at the player in only {looked} of {frames} frames");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator KilledItFadesAwayAndIsGone()
        {
            Wraith wraith = Spawn(new Vector3(0.0f, WraithDrift.HoverHeight, 0.0f));
            yield return null;
            ((IDamageable)wraith).ApplyHit(Wraith.MaxHealth, Vector3.zero, 0.0f);
            Assert.IsFalse(wraith.IsAlive);
            yield return null;
            yield return null;
            Assert.Greater(wraith.FadeProgress, 0.0f, "it does not fade");
            Assert.Less(wraith.FadeProgress, 1.0f, "it vanished at once");
            float waited = 0.0f;
            while (_wraith != null && waited < Wraith.FadeSeconds + 0.5f)
            {
                waited += 1.0f / 60.0f;
                yield return null;
            }
            Assert.IsTrue(_wraith == null, "faded away, it is still there");
        }

        [UnityTest]
        public IEnumerator SpawnWraithThroughTheRealConsolePutsOneInTheCity()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Object.Destroy(_ground);
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(_scene);

            var console = Object.FindFirstObjectByType<DevConsoleView>();
            Assert.IsNotNull(console);
            Assert.IsNotNull(console.WraithPrefab, "no Wraith prefab wired to the real console (DevConsoleBuilder)");
            console.Submit("/spawn wraith");
            yield return null;
            var wraith = Object.FindFirstObjectByType<Wraith>();
            Assert.IsNotNull(wraith, "/spawn wraith made no Wraith");
            for (int i = 0; i < 60 * 3; i++)
            {
                yield return null;
            }
            Assert.IsTrue(wraith != null && wraith.IsAlive, "the Wraith died on its own in the city");
            Object.Destroy(wraith.gameObject);
#else
            yield break;
#endif
        }
    }
}
