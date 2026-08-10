using System.Collections;
using CityX.Core;
using CityX.Net;
using CityX.Spike;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The single most important measurement in Phase 0.
    ///
    /// Without interest management, every client receives every entity and
    /// bandwidth grows as players x entities. That is the exact wall the
    /// Godot build hit -- MultiplayerSynchronizer replicates everything to
    /// everyone -- and it is the reason this port is happening at all. If
    /// this does not work, nothing else about Unity matters.
    ///
    /// What is asserted is deliberately a RELATIONSHIP, not a magic number:
    /// a player near the middle of a 100m field with a 40m view should
    /// observe a small fraction of 200 entities, and moving that player
    /// should change WHICH entities they observe. Pinning an exact count
    /// would fail on any tuning change while proving nothing extra.
    /// </summary>
    public class InterestManagementPlayTests
    {
        private static ushort _nextPort = 41900;

        private GameObject _host;
        private NetworkManager _networkManager;
        private NetworkBootstrap _bootstrap;
        private DefaultPrefabObjects _spawnablePrefabs;
        private SpikeWorld _world;

        private const float TimeoutSeconds = 30.0f;

        /// <summary>
        /// DistanceCondition is a Timed condition -- FishNet re-evaluates it
        /// on an interval rather than every frame, which is the whole point
        /// of it being cheap. Tests must wait for at least one sweep.
        /// </summary>
        private const float ObserverSettleSeconds = 2.5f;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            try
            {
                SpikeAgent.ResetLiveCount();
                SpikePlayer.ResetLiveCount();

                _host = new GameObject("CityX Interest Host");
                _host.SetActive(false);

                _networkManager = _host.AddComponent<NetworkManager>();
                _spawnablePrefabs = ScriptableObject.CreateInstance<DefaultPrefabObjects>();
                _networkManager.SpawnablePrefabs = _spawnablePrefabs;

                _bootstrap = _host.AddComponent<NetworkBootstrap>();
                _bootstrap.AutoStartOnPlay = false;

                _world = _host.AddComponent<SpikeWorld>();

                _host.SetActive(true);
                _world.Initialize(_networkManager);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null)
            {
                _world.DespawnAll();
            }
            if (_bootstrap != null)
            {
                _bootstrap.Shutdown();
            }
            if (_host != null)
            {
                Object.DestroyImmediate(_host);
            }
            if (_spawnablePrefabs != null)
            {
                Object.DestroyImmediate(_spawnablePrefabs);
            }
            SpikeAgent.ResetLiveCount();
            SpikePlayer.ResetLiveCount();
        }

        /// <summary>
        /// THE ONE. A player must not receive the whole world.
        /// </summary>
        [UnityTest]
        public IEnumerator APlayerObservesOnlyNearbyEntitiesNotTheWholeWorld()
        {
            yield return StartHost();

            NetworkConnection connection = _networkManager.ClientManager.Connection;
            _world.SpawnPlayer(connection, Vector3.zero);
            _world.SpawnAgents(SpikeWorld.DefaultAgentCount);

            yield return new WaitForSeconds(ObserverSettleSeconds);

            int total = 0;
            int observed = 0;
            CountObserved(connection, ref total, ref observed);

            Assert.AreEqual(SpikeWorld.DefaultAgentCount, total, "not every agent spawned");
            Assert.Greater(observed, 0,
                "the player observes nothing at all -- interest management is culling everything, " +
                "which is as broken as culling nothing");
            Assert.Less(observed, total,
                "the player observes EVERY entity in the world. Interest management is not working, " +
                "and bandwidth will grow as players x entities -- the exact wall this port exists to avoid");

            Debug.Log($"[CityX.Spike] interest management: {observed}/{total} entities observed " +
                      $"({100.0f * observed / total:0.#}%) at {SpikeWorld.ObserverRadius}m view " +
                      $"on a {SpikeWorld.SpawnRadius}m field");
        }

        /// <summary>
        /// Culling must track the player, not just happen once at spawn. A
        /// static snapshot would pass the test above and still break the
        /// moment anyone walked anywhere.
        /// </summary>
        [UnityTest]
        public IEnumerator MovingThePlayerChangesWhatTheyObserve()
        {
            yield return StartHost();

            NetworkConnection connection = _networkManager.ClientManager.Connection;
            NetworkObject player = _world.SpawnPlayer(connection, Vector3.zero);
            _world.SpawnAgents(SpikeWorld.DefaultAgentCount);

            yield return new WaitForSeconds(ObserverSettleSeconds);

            var near = new System.Collections.Generic.HashSet<int>();
            CollectObservedIds(connection, near);
            Assert.Greater(near.Count, 0, "observed nothing from the starting position");

            // Move to the far edge of the field, well beyond one view radius,
            // so the observed set genuinely has to turn over.
            SpikePlayer spikePlayer = player.GetComponent<SpikePlayer>();
            spikePlayer.ServerMoveTo(new Vector3(SpikeWorld.SpawnRadius * 0.95f, 0.0f, 0.0f));

            yield return new WaitForSeconds(ObserverSettleSeconds);

            var far = new System.Collections.Generic.HashSet<int>();
            CollectObservedIds(connection, far);

            Assert.Greater(far.Count, 0, "observed nothing after moving -- culling got stuck");

            near.IntersectWith(far);
            Assert.Less(near.Count, far.Count,
                "the observed set did not change after moving across the field, so visibility " +
                "is not actually tracking the player");

            Debug.Log($"[CityX.Spike] observed set turned over on move: {far.Count} entities visible " +
                      $"at the far edge, {near.Count} of them also visible from the centre");
        }

        // --- helpers ---

        private void CountObserved(NetworkConnection connection, ref int total, ref int observed)
        {
            SpikeAgent[] agents = Object.FindObjectsByType<SpikeAgent>(FindObjectsSortMode.None);
            total = agents.Length;
            observed = 0;
            foreach (SpikeAgent agent in agents)
            {
                if (agent.NetworkObject != null && agent.NetworkObject.Observers.Contains(connection))
                {
                    observed++;
                }
            }
        }

        private void CollectObservedIds(NetworkConnection connection, System.Collections.Generic.HashSet<int> into)
        {
            SpikeAgent[] agents = Object.FindObjectsByType<SpikeAgent>(FindObjectsSortMode.None);
            foreach (SpikeAgent agent in agents)
            {
                if (agent.NetworkObject != null && agent.NetworkObject.Observers.Contains(connection))
                {
                    into.Add(agent.ObjectId);
                }
            }
        }

        private IEnumerator StartHost()
        {
            ushort port = _nextPort++;
            LaunchArgs args = LaunchArgs.Parse(new[] { "-port", port.ToString(), "-seed", "12345" });
            _bootstrap.Begin(GameMode.Singleplayer, args);

            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!_networkManager.IsHostStarted)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail($"timed out after {TimeoutSeconds:0.#}s waiting for the host to start");
                }
                yield return null;
            }
        }
    }
}
