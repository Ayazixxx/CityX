using System.Collections;
using CityX.Core;
using CityX.Net;
using CityX.UI;
using FishNet.Managing;
using FishNet.Managing.Object;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Requested: opening the console should pause the player and the
    /// game in singleplayer, but must never pause anything in multiplayer --
    /// Time.timeScale governs this whole PROCESS, and in HostedWorld this
    /// same process is also the authoritative server for every other
    /// connected player, so freezing it here would silently freeze their
    /// game too. Mirrors NetworkBootstrapPlayTests's own real-socket setup,
    /// since "what GameMode does this NetworkBootstrap actually end up in"
    /// is only genuinely true once FishNet has really started -- Mode has a
    /// private setter and is only ever set by a real Begin() call.
    /// </summary>
    public sealed class DevConsolePausePlayTests
    {
        private static ushort _nextPort = 42750;

        private GameObject _host;
        private NetworkManager _networkManager;
        private NetworkBootstrap _bootstrap;
        private DefaultPrefabObjects _spawnablePrefabs;
        private GameObject _consoleObject;

        private const float ConnectTimeoutSeconds = 15.0f;

        [SetUp]
        public void SetUp()
        {
            // See NetworkBootstrapPlayTests's identical SetUp for why errors
            // are ignored only across NetworkManager construction.
            LogAssert.ignoreFailingMessages = true;
            try
            {
                _host = new GameObject("CityX Test NetworkManager");
                _host.SetActive(false);

                _networkManager = _host.AddComponent<NetworkManager>();
                _spawnablePrefabs = ScriptableObject.CreateInstance<DefaultPrefabObjects>();
                _networkManager.SpawnablePrefabs = _spawnablePrefabs;

                _bootstrap = _host.AddComponent<NetworkBootstrap>();
                _bootstrap.AutoStartOnPlay = false;

                _host.SetActive(true);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1.0f;
            if (_bootstrap != null) _bootstrap.Shutdown();
            if (_host != null) Object.DestroyImmediate(_host);
            if (_spawnablePrefabs != null) Object.DestroyImmediate(_spawnablePrefabs);
            if (_consoleObject != null) Object.DestroyImmediate(_consoleObject);
        }

        [UnityTest]
        public IEnumerator OpeningTheConsoleInAHostedWorldNeverPausesTime()
        {
            _bootstrap.Begin(GameMode.HostedWorld, ArgsOnFreshPort());
            yield return WaitUntil(() => _networkManager.IsHostStarted, "the hosted world to come up");

            _consoleObject = new GameObject("DevConsole");
            var view = _consoleObject.AddComponent<DevConsoleView>();
            view.Bind();

            Time.timeScale = 1.0f;
            view.Open();

            Assert.AreEqual(1.0f, Time.timeScale, 0.001f,
                "opening the console in a HostedWorld paused real time -- that would freeze every other connected player too");

            view.Close();
            Assert.AreEqual(1.0f, Time.timeScale, 0.001f, "closing the console left time paused after a HostedWorld open");
        }

        [UnityTest]
        public IEnumerator OpeningTheConsoleInSingleplayerReallyPausesTime()
        {
            _bootstrap.Begin(GameMode.Singleplayer, ArgsOnFreshPort());
            yield return WaitUntil(() => _networkManager.IsHostStarted, "singleplayer to come up as a host");

            _consoleObject = new GameObject("DevConsole");
            var view = _consoleObject.AddComponent<DevConsoleView>();
            view.Bind();

            Time.timeScale = 1.0f;
            view.Open();

            Assert.AreEqual(0.0f, Time.timeScale, 0.001f,
                "opening the console with a real Singleplayer NetworkBootstrap in the scene never paused time");

            view.Close();
            Assert.AreEqual(1.0f, Time.timeScale, 0.001f, "closing the console never resumed time after a real singleplayer open");
        }

        private static LaunchArgs ArgsOnFreshPort()
        {
            ushort port = _nextPort++;
            return LaunchArgs.Parse(new[] { "-port", port.ToString(), "-seed", "12345" });
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, string what)
        {
            float deadline = Time.realtimeSinceStartup + ConnectTimeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail($"timed out after {ConnectTimeoutSeconds:0.#}s waiting for {what}");
                }
                yield return null;
            }
        }
    }
}
