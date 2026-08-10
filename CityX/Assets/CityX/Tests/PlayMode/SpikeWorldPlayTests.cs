using System.Collections;
using CityX.Core;
using CityX.Net;
using CityX.Spike;
using FishNet.Managing;
using FishNet.Managing.Object;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// PORT_PLAN Phase 0, the entity half: can this stack carry the number of
    /// server-simulated entities the design implies?
    ///
    /// This file establishes that 200 entities SPAWN and are SIMULATED. It
    /// does not yet establish that they are affordable at 30 clients, which
    /// needs bandwidth and tick-time measurement across separate processes.
    /// Getting the correctness right first means those later numbers are
    /// measuring something real.
    /// </summary>
    public class SpikeWorldPlayTests
    {
        private static ushort _nextPort = 41850;

        private GameObject _host;
        private NetworkManager _networkManager;
        private NetworkBootstrap _bootstrap;
        private DefaultPrefabObjects _spawnablePrefabs;
        private SpikeWorld _world;

        private const float TimeoutSeconds = 30.0f;

        [SetUp]
        public void SetUp()
        {
            // See NetworkBootstrapPlayTests for why construction-time errors
            // are ignored: NetworkManager.OnValidate logs during AddComponent
            // itself, before any field can be assigned.
            LogAssert.ignoreFailingMessages = true;
            try
            {
                SpikeAgent.ResetLiveCount();

                _host = new GameObject("CityX Spike Host");
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
        }

        /// <summary>
        /// The headline number. 200 server-simulated entities, spawned and
        /// live, which is roughly what dozens of players plus faction NPCs
        /// plus summons implies.
        /// </summary>
        [UnityTest]
        public IEnumerator TwoHundredAgentsSpawnAndAreSimulatedByTheServer()
        {
            yield return StartHost();

            _world.SpawnAgents(SpikeWorld.DefaultAgentCount);
            yield return null;

            Assert.AreEqual(SpikeWorld.DefaultAgentCount, _world.SpawnedCount,
                "not every agent was spawned");
            Assert.AreEqual(SpikeWorld.DefaultAgentCount, SpikeAgent.LiveCount,
                "some agents spawned but never started simulating on the server");
        }

        /// <summary>
        /// Spawned is not the same as moving. An entity that sits still costs
        /// almost no replication traffic, so a spike full of statues would
        /// measure nothing and look great doing it.
        /// </summary>
        [UnityTest]
        public IEnumerator AgentsActuallyMoveOnTheServer()
        {
            yield return StartHost();

            const int sampleCount = 20;
            _world.SpawnAgents(sampleCount);
            yield return null;

            SpikeAgent[] agents = Object.FindObjectsByType<SpikeAgent>(FindObjectsSortMode.None);
            Assert.AreEqual(sampleCount, agents.Length, "wrong number of agents in the scene");

            Vector3[] before = new Vector3[agents.Length];
            for (int i = 0; i < agents.Length; i++)
            {
                before[i] = agents[i].transform.position;
            }

            yield return new WaitForSeconds(1.0f);

            int moved = 0;
            for (int i = 0; i < agents.Length; i++)
            {
                if ((agents[i].transform.position - before[i]).sqrMagnitude > 0.0001f)
                {
                    moved++;
                }
            }

            Assert.AreEqual(agents.Length, moved,
                $"only {moved}/{agents.Length} agents moved in a second -- the tick loop is not driving them");
        }

        /// <summary>
        /// Despawning has to release the entities AND their server-side tick
        /// subscriptions. A leak here would quietly inflate every later
        /// performance measurement.
        /// </summary>
        [UnityTest]
        public IEnumerator DespawnReleasesEveryAgent()
        {
            yield return StartHost();

            _world.SpawnAgents(50);
            yield return null;
            Assert.AreEqual(50, SpikeAgent.LiveCount, "setup failed -- agents did not start");

            _world.DespawnAll();
            yield return null;
            yield return null;

            Assert.AreEqual(0, _world.SpawnedCount, "the world still thinks it owns agents");
            Assert.AreEqual(0, SpikeAgent.LiveCount,
                "agents despawned but their server tick subscriptions leaked");
        }

        // --- helpers ---

        private IEnumerator StartHost()
        {
            ushort port = _nextPort++;
            LaunchArgs args = LaunchArgs.Parse(new[] { "-port", port.ToString(), "-seed", "12345" });
            _bootstrap.Begin(GameMode.Singleplayer, args);

            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!_networkManager.IsServerStarted)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail($"timed out after {TimeoutSeconds:0.#}s waiting for the server to start");
                }
                yield return null;
            }
        }
    }
}
