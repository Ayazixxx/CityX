using System.Collections;
using CityX.Character;
using CityX.Core;
using CityX.Net;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// THE ACTUAL DELIVERABLE, end to end: "get to a point i can play with
    /// my friend and have our character skins and animations synced up."
    /// Every earlier multiplayer test either drives NetworkBootstrap in
    /// isolation (NetworkBootstrapPlayTests) or spawns a generic spike
    /// entity (SpikeWorldPlayTests/ThirtyClientLoadPlayTests) -- nothing
    /// exercises the REAL path a hosting player and a joining friend
    /// actually take: NetworkBootstrap.Begin(HostedWorld) on the real
    /// CityMassing.unity scene, then a second, genuinely separate
    /// NetworkManager connecting in as the friend, then
    /// NetworkedPlayerSpawner reacting to that connection for real.
    ///
    /// TWO REAL NetworkManagers, ONE PROCESS, LOOPBACK -- same technique
    /// ThirtyClientLoadPlayTests already established for a server + many
    /// clients; here it is a server-with-local-client (the host) plus one
    /// more, external client (the friend).
    /// </summary>
    public sealed class NetworkedPlayerSpawnPlayTests
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private static ushort _nextPort = 43600;
        private const float ConnectTimeoutSeconds = 20.0f;

        private GameObject _hostGo;
        private NetworkManager _hostManager;
        private NetworkBootstrap _hostBootstrap;
        private DefaultPrefabObjects _hostPrefabs;

        private GameObject _friendGo;
        private NetworkManager _friendManager;
        private DefaultPrefabObjects _friendPrefabs;

        private Scene _scene;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = true;

            if (_friendManager != null && _friendManager.ClientManager.Started)
            {
                _friendManager.ClientManager.StopConnection();
            }
            if (_hostBootstrap != null)
            {
                _hostBootstrap.Shutdown();
            }

            // The friend's spawned clone (GetPooledInstantiated) lands in
            // whatever scene Unity currently considers "active" -- never
            // explicitly set to _scene here, so it is NOT necessarily part
            // of it and UnloadSceneAsync below would not touch it. Left
            // alone, it leaks into every later PlayMode test in the same
            // batch run (confirmed: it broke unrelated Parachute/PlayerSpawn
            // real-scene tests that run alphabetically after this one).
            // Swept up explicitly, by type, since nothing else in the
            // scene is expected to carry this component.
            foreach (NetworkedPlayerIdentity identity in
                     Object.FindObjectsByType<NetworkedPlayerIdentity>(FindObjectsSortMode.None))
            {
                if (identity != null)
                {
                    Object.DestroyImmediate(identity.gameObject);
                }
            }

            if (_scene.IsValid() && _scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            if (_friendGo != null)
            {
                Object.DestroyImmediate(_friendGo);
            }
            if (_hostGo != null)
            {
                Object.DestroyImmediate(_hostGo);
            }
            if (_friendPrefabs != null)
            {
                Object.DestroyImmediate(_friendPrefabs);
            }
            if (_hostPrefabs != null)
            {
                Object.DestroyImmediate(_hostPrefabs);
            }

            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator AFriendJoiningAHostedWorldGetsASpawnedCloneWhileTheHostKeepsTheirOwnBody()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            ushort port = _nextPort++;

            GameObject networkedPlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/NetworkedPlayer.prefab");
            Assert.IsNotNull(networkedPlayerPrefab, "no NetworkedPlayer.prefab -- run CityX/Rebuild Massing Scene first");

            // --- the host: NetworkBootstrap + NetworkManager, real HostedWorld mode ---
            _hostGo = new GameObject("Host NetworkManager");
            _hostGo.SetActive(false);
            _hostManager = _hostGo.AddComponent<NetworkManager>();
            _hostGo.AddComponent<Tugboat>();
            _hostPrefabs = ScriptableObject.CreateInstance<DefaultPrefabObjects>();
            _hostPrefabs.AddObjects(new System.Collections.Generic.List<FishNet.Object.NetworkObject>
            {
                networkedPlayerPrefab.GetComponent<FishNet.Object.NetworkObject>()
            }, checkForDuplicates: false);
            _hostManager.SpawnablePrefabs = _hostPrefabs;
            _hostBootstrap = _hostGo.AddComponent<NetworkBootstrap>();
            _hostBootstrap.AutoStartOnPlay = false;
            AllowMultipleNetworkManagers(_hostManager);
            _hostGo.SetActive(true);

            bool hostStarted = _hostBootstrap.Begin(GameMode.HostedWorld,
                LaunchArgs.For(GameMode.HostedWorld, "127.0.0.1", port));
            Assert.IsTrue(hostStarted, "the hosted world failed to come up");

            yield return WaitUntil(() => _hostManager.IsHostStarted, "the host to come up");

            // --- the real scene, loaded additively exactly the way the
            // --- title screen's own scene transition would load it, so the
            // --- baked player + NetworkedPlayerSpawner exist for real ---
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;

            _scene = SceneManager.GetSceneByPath(ScenePath);
            Assert.IsTrue(_scene.IsValid() && _scene.isLoaded, "the real scene never finished loading");

            var hostIdentity = Object.FindFirstObjectByType<NetworkedPlayerIdentity>();
            Assert.IsNotNull(hostIdentity, "the baked scene player has no NetworkedPlayerIdentity");

            // The host's own connection is claimed by NetworkedPlayerSpawner
            // as soon as its own Start() runs -- give it a few frames rather
            // than assuming same-frame ordering against the scene load.
            yield return WaitUntil(() => hostIdentity.GetComponent<FishNet.Object.NetworkObject>().IsOwner,
                "the host to own their own baked body");

            var hostInput = hostIdentity.GetComponent<PlayerInputReader>();
            Assert.IsNotNull(hostInput, "the baked player has no PlayerInputReader");
            Assert.IsTrue(hostInput.enabled,
                "the host's own PlayerInputReader was disabled -- NetworkedPlayerIdentity.OnStartClient " +
                "must have run before NetworkedPlayerSpawner handed the host ownership of their own body, " +
                "so it saw IsOwner == false and switched off the host's own controls");

            // --- the friend: a second, genuinely separate NetworkManager,
            // --- connecting in exactly the way a real joining player's own
            // --- process would (see TitleMultiplayerView.Join) ---
            _friendGo = new GameObject("Friend NetworkManager");
            _friendGo.SetActive(false);
            _friendManager = _friendGo.AddComponent<NetworkManager>();
            _friendGo.AddComponent<Tugboat>();
            _friendPrefabs = ScriptableObject.CreateInstance<DefaultPrefabObjects>();
            _friendManager.SpawnablePrefabs = _friendPrefabs;
            AllowMultipleNetworkManagers(_friendManager);
            _friendGo.SetActive(true);

            _friendManager.ClientManager.StartConnection("127.0.0.1", port);

            yield return WaitUntil(() => _hostManager.ServerManager.Clients.Count >= 2,
                "the friend to connect to the host");

            // NetworkedPlayerSpawner's own OnRemoteConnectionState handler
            // and the spawn's own replication down to observers both take a
            // few real frames -- poll instead of guessing a fixed count.
            NetworkedPlayerIdentity[] identities = null;
            yield return WaitUntil(() =>
            {
                identities = Object.FindObjectsByType<NetworkedPlayerIdentity>(FindObjectsSortMode.None);
                return identities.Length >= 2;
            }, "the friend's own clone to be spawned");

            Assert.AreEqual(2, identities.Length,
                "expected exactly two players -- the host's own baked body plus one spawned clone for the " +
                "joining friend -- but found " + identities.Length);

            NetworkedPlayerIdentity clone = identities[0] == hostIdentity ? identities[1] : identities[0];
            Assert.AreNotSame(hostIdentity, clone, "the friend's clone is the same object as the host's own body");
            Assert.IsFalse(clone.GetComponent<FishNet.Object.NetworkObject>().IsSceneObject,
                "the friend's player should be a spawned clone, not the baked scene object");

            var cloneTransform = clone.GetComponent<FishNet.Component.Transforming.NetworkTransform>();
            Assert.IsNotNull(cloneTransform, "the friend's spawned clone has no NetworkTransform to sync position");

            // The city's flat sound: on the host's screen only the host's own body may play it, or each player who joins turns it up.
            // The friend is out of the host's view radius here, so the host's client never starts this copy: it must be silent without being told.
            yield return WaitUntil(() => CityAmbienceBodiesPlayTests.LayersOfTheCity(clone.gameObject) == 0,
                "the friend's copy to stop playing the city on the host's machine");
            // IsOwner is true on the server's side as soon as ownership is given; the client's callback that starts the sound arrives a little later.
            yield return WaitUntil(() => CityAmbienceBodiesPlayTests.LayersOfTheCity(hostIdentity.gameObject) == 1,
                "the host's own body to play the city once");
#else
            yield break;
#endif
        }

        /// <summary>
        /// Real play never has two NetworkManagers sharing one process --
        /// each side of a connection is its own machine, or at least its own
        /// process -- so FishNet's own default persistence (DestroyNewest)
        /// is correct there and is left alone everywhere else in this
        /// project. A single PlayMode test process is the one place that
        /// isn't true (see ThirtyClientLoadPlayTests' own SpikeNetworkManager
        /// prefab, which is pre-configured with AllowMultiple in its own
        /// Inspector for the exact same reason): without this, the second
        /// NetworkManager constructed here destroys itself the instant it
        /// sees the first one already exists.
        /// </summary>
        private static void AllowMultipleNetworkManagers(NetworkManager manager)
        {
#if UNITY_EDITOR
            var so = new SerializedObject(manager);
            SerializedProperty persistence = so.FindProperty("_persistence");
            if (persistence != null)
            {
                persistence.enumValueIndex = (int)NetworkManager.PersistenceType.AllowMultiple;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
#endif
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
