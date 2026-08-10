using System.Collections;
using CityX.Spike;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// PORT_PLAN Phase 0's numeric exit criteria, measured rather than
    /// asserted from theory:
    ///
    ///   "Per-client bandwidth stays flat as total entity count grows
    ///    (proves AoI works)"
    ///
    /// A REAL server and a REAL remote client, in one process but talking
    /// over a loopback socket, so serialization and byte counts are genuine.
    /// Host mode would not do: its local client short-circuits the transport,
    /// and the bandwidth numbers would be fiction.
    ///
    /// The experiment: measure bytes-per-second arriving at one client with N
    /// entities in the world, then again with 4N. If area-of-interest works,
    /// the client's traffic barely moves, because the extra entities are
    /// spawned into extra WORLD rather than into the player's neighbourhood
    /// (see SpikeWorld.FieldRadiusFor -- that distinction is what makes this
    /// a measurement of culling rather than of density).
    /// </summary>
    public class ScaleMeasurementPlayTests
    {
        private static ushort _nextPort = 42000;

        private GameObject _serverGo;
        private GameObject _clientGo;
        private NetworkManager _server;
        private NetworkManager _client;
        private SpikeWorld _world;
        private SpikeMetrics _metrics;

        private const float TimeoutSeconds = 30.0f;

        /// <summary>Long enough for many ticks and several observer sweeps.</summary>
        private const float MeasureSeconds = 4.0f;

        /// <summary>Let spawning, observer evaluation and JIT settle before measuring.</summary>
        private const float SettleSeconds = 3.0f;

        private const int BaselineEntities = 50;
        private const int ScaledEntities = 200;

        [SetUp]
        public void SetUp()
        {
            SpikeAgent.ResetLiveCount();
            SpikePlayer.ResetLiveCount();
            _metrics = new SpikeMetrics();

            GameObject prefab = Resources.Load<GameObject>("CityX.Spike/SpikeNetworkManager");
            Assert.IsNotNull(prefab,
                "missing generated SpikeNetworkManager prefab -- run SpikeAssetBuilder.BuildAll");

            _serverGo = Object.Instantiate(prefab);
            _serverGo.name = "Spike Server";
            _server = _serverGo.GetComponent<NetworkManager>();

            _clientGo = Object.Instantiate(prefab);
            _clientGo.name = "Spike Client";
            _client = _clientGo.GetComponent<NetworkManager>();

            _world = _serverGo.AddComponent<SpikeWorld>();
            _world.Initialize(_server);
        }

        [TearDown]
        public void TearDown()
        {
            if (_metrics != null)
            {
                _metrics.Detach();
            }
            if (_world != null)
            {
                _world.DespawnAll();
            }
            if (_client != null && _client.ClientManager.Started)
            {
                _client.ClientManager.StopConnection();
            }
            if (_server != null && _server.ServerManager.Started)
            {
                _server.ServerManager.StopConnection(true);
            }
            if (_clientGo != null)
            {
                Object.DestroyImmediate(_clientGo);
            }
            if (_serverGo != null)
            {
                Object.DestroyImmediate(_serverGo);
            }
            SpikeAgent.ResetLiveCount();
            SpikePlayer.ResetLiveCount();
            SpikeAgent.FieldRadius = 120.0f;
        }

        /// <summary>
        /// THE PHASE 0 NUMBER. Quadruple the world's entity count and a
        /// client's inbound traffic should stay roughly put.
        /// </summary>
        [UnityTest]
        public IEnumerator PerClientBandwidthStaysFlatAsEntityCountGrows()
        {
            yield return ConnectServerAndClient();

            NetworkConnection clientConnection = FirstRemoteConnection();
            Assert.IsNotNull(clientConnection, "the server never saw a remote client connect");
            _world.SpawnPlayer(clientConnection, Vector3.zero);

            _metrics.AttachServer(_server);
            _metrics.AttachClient(_client.TransportManager.Transport);

            // --- baseline ---
            _world.SpawnAgents(BaselineEntities);
            yield return new WaitForSeconds(SettleSeconds);
            _metrics.ResetWindow();
            yield return new WaitForSeconds(MeasureSeconds);

            double baselineKbps = _metrics.BytesPerSecond / 1024.0;
            double baselineTick = _metrics.MeanTickMs;
            double baselineWorst = _metrics.WorstTickMs;
            _metrics.Log("baseline", BaselineEntities);

            // --- scaled: 4x the entities, spread over 4x the area ---
            _world.DespawnAgentsOnly();
            yield return new WaitForSeconds(1.0f);

            _world.SpawnAgents(ScaledEntities);
            yield return new WaitForSeconds(SettleSeconds);
            _metrics.ResetWindow();
            yield return new WaitForSeconds(MeasureSeconds);

            double scaledKbps = _metrics.BytesPerSecond / 1024.0;
            double scaledTick = _metrics.MeanTickMs;
            double scaledWorst = _metrics.WorstTickMs;
            _metrics.Log("scaled", ScaledEntities);

            double entityGrowth = ScaledEntities / (double)BaselineEntities;
            double trafficGrowth = baselineKbps <= 0.0 ? 0.0 : scaledKbps / baselineKbps;

            Debug.Log($"[CityX.Spike] SCALE RESULT: entities x{entityGrowth:0.0} -> " +
                      $"client traffic x{trafficGrowth:0.00} " +
                      $"({baselineKbps:0.0} -> {scaledKbps:0.0} KB/s), " +
                      $"server tick {baselineTick:0.000} -> {scaledTick:0.000} ms " +
                      $"(worst {baselineWorst:0.000} -> {scaledWorst:0.000} ms)");

            Assert.Greater(baselineKbps, 0.0,
                "the client received nothing at all -- the measurement is not wired up");

            // Generous: anything well under linear proves culling is working.
            // Pinning a tight number would make this a flaky benchmark rather
            // than a correctness check, and the interesting failure is
            // "traffic grew 4x", not "traffic grew 1.3x instead of 1.1x".
            Assert.Less(trafficGrowth, entityGrowth * 0.5,
                $"client traffic grew x{trafficGrowth:0.00} against an entity growth of " +
                $"x{entityGrowth:0.0}. That is close to linear, which means area-of-interest " +
                "is not actually limiting what each client receives.");
        }

        // --- helpers ---

        private NetworkConnection FirstRemoteConnection()
        {
            foreach (var kvp in _server.ServerManager.Clients)
            {
                return kvp.Value;
            }
            return null;
        }

        private IEnumerator ConnectServerAndClient()
        {
            ushort port = _nextPort++;

            Transport serverTransport = _server.TransportManager.Transport;
            serverTransport.SetPort(port);
            serverTransport.SetServerBindAddress("127.0.0.1", IPAddressType.IPv4);
            _server.ServerManager.StartConnection(port);

            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!_server.IsServerStarted)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail("timed out waiting for the server to start");
                }
                yield return null;
            }

            Transport clientTransport = _client.TransportManager.Transport;
            clientTransport.SetPort(port);
            clientTransport.SetClientAddress("127.0.0.1");
            _client.ClientManager.StartConnection("127.0.0.1", port);

            deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!_client.IsClientStarted || _server.ServerManager.Clients.Count == 0)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail("timed out waiting for the remote client to connect and authenticate");
                }
                yield return null;
            }
        }
    }
}
