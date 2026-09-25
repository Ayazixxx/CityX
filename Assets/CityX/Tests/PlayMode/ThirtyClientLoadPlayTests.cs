using System.Collections;
using System.Collections.Generic;
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
    /// PORT_PLAN Phase 0's headline exit criterion:
    ///
    ///   "Server holds tick rate with 30 clients + 200 AI"
    ///
    /// Every earlier measurement used a single client, which leaves the
    /// interesting question open: replication work grows with CLIENTS as well
    /// as entities, so 200 entities being cheap for one player says nothing
    /// about thirty. This is the test that closes that gap.
    ///
    /// Thirty real clients, each its own NetworkManager with its own socket,
    /// connected to one server over loopback. Not simulated, not stubbed --
    /// if FishNet serialises per-observer, this pays that cost thirty times
    /// over, which is exactly the point.
    ///
    /// Players are spread across the field rather than stacked at the origin,
    /// so each has a DIFFERENT observed set. Thirty players standing on the
    /// same spot would share one neighbourhood and let the server reuse far
    /// more work than a real, dispersed population ever would.
    /// </summary>
    public class ThirtyClientLoadPlayTests
    {
        private static ushort _nextPort = 42100;

        private GameObject _serverGo;
        private NetworkManager _server;
        private SpikeWorld _world;
        private SpikeMetrics _metrics;
        private readonly List<GameObject> _clientGos = new List<GameObject>();
        private readonly List<NetworkManager> _clients = new List<NetworkManager>();

        private const int ClientCount = 30;
        private const int EntityCount = 200;

        private const float ConnectTimeoutSeconds = 90.0f;
        private const float SettleSeconds = 5.0f;
        private const float MeasureSeconds = 6.0f;

        [SetUp]
        public void SetUp()
        {
            SpikeAgent.ResetLiveCount();
            SpikePlayer.ResetLiveCount();
            _metrics = new SpikeMetrics();
        }

        [TearDown]
        public void TearDown()
        {
            if (_metrics != null)
            {
                _metrics.Detach();
            }
            foreach (NetworkManager client in _clients)
            {
                if (client != null && client.ClientManager.Started)
                {
                    client.ClientManager.StopConnection();
                }
            }
            if (_world != null)
            {
                _world.DespawnAll();
            }
            if (_server != null && _server.ServerManager.Started)
            {
                _server.ServerManager.StopConnection(true);
            }
            foreach (GameObject go in _clientGos)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _clientGos.Clear();
            _clients.Clear();
            if (_serverGo != null)
            {
                Object.DestroyImmediate(_serverGo);
            }
            SpikeAgent.ResetLiveCount();
            SpikePlayer.ResetLiveCount();
            SpikeAgent.FieldRadius = 120.0f;
        }

        [UnityTest]
        public IEnumerator ServerHoldsTickRateWithThirtyClientsAndTwoHundredEntities()
        {
            GameObject prefab = Resources.Load<GameObject>("CityX.Spike/SpikeNetworkManager");
            Assert.IsNotNull(prefab, "missing generated SpikeNetworkManager prefab");

            ushort port = _nextPort++;

            // --- server ---
            _serverGo = Object.Instantiate(prefab);
            _serverGo.name = "Spike Server";
            _server = _serverGo.GetComponent<NetworkManager>();
            _world = _serverGo.AddComponent<SpikeWorld>();
            _world.Initialize(_server);

            Transport serverTransport = _server.TransportManager.Transport;
            serverTransport.SetPort(port);
            serverTransport.SetServerBindAddress("127.0.0.1", IPAddressType.IPv4);
            serverTransport.SetMaximumClients(ClientCount + 4);
            _server.ServerManager.StartConnection(port);

            yield return WaitUntil(() => _server.IsServerStarted, "the server to start");

            // --- thirty clients ---
            for (int i = 0; i < ClientCount; i++)
            {
                GameObject go = Object.Instantiate(prefab);
                go.name = $"Spike Client {i:00}";
                NetworkManager client = go.GetComponent<NetworkManager>();
                _clientGos.Add(go);
                _clients.Add(client);

                Transport t = client.TransportManager.Transport;
                t.SetPort(port);
                t.SetClientAddress("127.0.0.1");
                client.ClientManager.StartConnection("127.0.0.1", port);

                // Stagger slightly: thirty simultaneous handshakes is a
                // thundering herd that measures connection setup, not the
                // steady state this test is about.
                yield return null;
            }

            yield return WaitUntil(
                () => _server.ServerManager.Clients.Count >= ClientCount,
                $"all {ClientCount} clients to connect");

            // --- a player each, spread out so observed sets differ ---
            int index = 0;
            float playerFieldRadius = SpikeWorld.FieldRadiusFor(EntityCount) * 0.8f;
            foreach (KeyValuePair<int, NetworkConnection> kvp in _server.ServerManager.Clients)
            {
                float angle = index * 2.39996323f;
                float radius = playerFieldRadius * Mathf.Sqrt((index + 0.5f) / ClientCount);
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0.0f, Mathf.Sin(angle) * radius);
                _world.SpawnPlayer(kvp.Value, position);
                index++;
            }

            _world.SpawnAgents(EntityCount);

            _metrics.AttachServer(_server);
            _metrics.AttachClient(_clients[0].TransportManager.Transport);

            yield return new WaitForSeconds(SettleSeconds);
            _metrics.ResetWindow();
            yield return new WaitForSeconds(MeasureSeconds);

            // The budget is read from the server rather than hardcoded, so
            // this stays honest if the tick rate is ever retuned.
            double tickBudgetMs = 1000.0 / _server.TimeManager.TickRate;
            double meanTick = _metrics.MeanTickMs;
            double worstTick = _metrics.WorstTickMs;
            double headroom = 100.0 * (1.0 - meanTick / tickBudgetMs);

            Debug.Log($"[CityX.Spike] 30-CLIENT LOAD: {_server.ServerManager.Clients.Count} clients, " +
                      $"{SpikeAgent.LiveCount} entities, {SpikePlayer.LiveCount} players | " +
                      $"tick mean {meanTick:0.000}ms worst {worstTick:0.000}ms " +
                      $"vs {tickBudgetMs:0.0}ms budget at {_server.TimeManager.TickRate}Hz " +
                      $"({headroom:0.#}% headroom) | client[0] in {_metrics.BytesPerSecond / 1024.0:0.0} KB/s");

            Assert.AreEqual(ClientCount, _server.ServerManager.Clients.Count,
                "not every client stayed connected for the measurement");
            Assert.AreEqual(EntityCount, SpikeAgent.LiveCount, "not every entity was simulating");
            Assert.Greater(_metrics.TickSamples, 30, "too few tick samples to mean anything");

            // Half the budget: a server that spends more than half its tick
            // on this, with no combat, no city and no persistence, has no
            // room for the actual game.
            Assert.Less(meanTick, tickBudgetMs * 0.5,
                $"server tick averaged {meanTick:0.000}ms against a {tickBudgetMs:0.0}ms budget " +
                $"with {ClientCount} clients and {EntityCount} entities -- that leaves no headroom " +
                "for combat, world streaming or persistence.");
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
