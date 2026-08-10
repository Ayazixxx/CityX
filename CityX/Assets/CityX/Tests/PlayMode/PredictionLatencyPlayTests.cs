using System.Collections;
using CityX.Spike;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The last Phase 0 exit criterion, and the only one about how the game
    /// FEELS rather than what it costs:
    ///
    ///   "Movement feels responsive at 100ms simulated latency"
    ///
    /// "Feels" cannot be asserted, but the thing that produces the feeling
    /// can be measured exactly: how long after pressing forward does YOUR
    /// character start moving on YOUR screen.
    ///
    /// Without prediction that is one full round trip -- 100ms here -- because
    /// the input goes to the server, the server moves you, and the result
    /// comes back. With prediction it is one tick, because the client runs
    /// the same movement code immediately and corrects itself later.
    ///
    /// So the test measures owner-side response time under 100ms of real
    /// simulated latency, and separately checks the client has not simply
    /// wandered off on its own -- responsiveness bought by abandoning server
    /// authority would be worthless.
    /// </summary>
    public class PredictionLatencyPlayTests
    {
        private static ushort _nextPort = 42200;

        private GameObject _serverGo;
        private GameObject _clientGo;
        private NetworkManager _server;
        private NetworkManager _client;

        private const long SimulatedLatencyMs = 100;
        private const float TimeoutSeconds = 45.0f;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = Resources.Load<GameObject>("CityX.Spike/SpikeNetworkManager");
            Assert.IsNotNull(prefab, "missing generated SpikeNetworkManager prefab");

            _serverGo = Object.Instantiate(prefab);
            _serverGo.name = "Prediction Server";
            _server = _serverGo.GetComponent<NetworkManager>();

            _clientGo = Object.Instantiate(prefab);
            _clientGo.name = "Prediction Client";
            _client = _clientGo.GetComponent<NetworkManager>();
        }

        [TearDown]
        public void TearDown()
        {
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
        }

        [UnityTest]
        public IEnumerator OwnerMovesImmediatelyAtOneHundredMillisecondLatency()
        {
            yield return ConnectWithLatency();

            NetworkConnection connection = FirstRemoteConnection();
            Assert.IsNotNull(connection, "no remote client connected");

            GameObject playerPrefab = Resources.Load<GameObject>("CityX.Spike/SpikePredictedPlayer");
            Assert.IsNotNull(playerPrefab, "missing generated SpikePredictedPlayer prefab");

            NetworkObject serverPlayer = Object.Instantiate(playerPrefab.GetComponent<NetworkObject>(),
                Vector3.zero, Quaternion.identity);
            serverPlayer.gameObject.SetActive(true);
            _server.ServerManager.Spawn(serverPlayer, connection);

            // Wait for the client's own copy to exist and be owned by it.
            SpikePredictedPlayer ownerCopy = null;
            yield return WaitUntil(() =>
            {
                foreach (SpikePredictedPlayer p in Object.FindObjectsByType<SpikePredictedPlayer>(FindObjectsSortMode.None))
                {
                    if (p.IsOwner)
                    {
                        ownerCopy = p;
                        return true;
                    }
                }
                return false;
            }, "the client to receive and own its predicted player");

            // Let everything settle so the measurement is not catching spawn traffic.
            yield return new WaitForSeconds(2.0f);

            Vector3 startPosition = ownerCopy.transform.position;
            double inputAt = Time.realtimeSinceStartupAsDouble;

            // "Press forward."
            ownerCopy.Input = new Vector2(0.0f, 1.0f);

            // Measure when the OWNER's own copy visibly moves. This is what a
            // player actually experiences -- their own character responding.
            const float MovedThreshold = 0.05f;
            yield return WaitUntil(
                () => (ownerCopy.transform.position - startPosition).sqrMagnitude > MovedThreshold * MovedThreshold,
                "the owner's character to start moving");

            double responseMs = (Time.realtimeSinceStartupAsDouble - inputAt) * 1000.0;

            // Keep moving so there is a real trajectory to compare.
            yield return new WaitForSeconds(2.0f);
            ownerCopy.Input = Vector2.zero;
            yield return new WaitForSeconds(1.5f);

            SpikePredictedPlayer serverCopy = serverPlayer.GetComponent<SpikePredictedPlayer>();
            float finalError = Vector3.Distance(ownerCopy.transform.position, serverCopy.transform.position);
            float travelled = Vector3.Distance(ownerCopy.transform.position, startPosition);

            // Read from the OWNER, not the server. Reconciliation is a
            // client-side correction -- the server never reconciles to
            // anything, so its counter is always zero and would look like
            // evidence of nothing happening while telling you nothing at all.
            int reconciles = ownerCopy.ReconcileCount;

            Debug.Log($"[CityX.Spike] PREDICTION at {SimulatedLatencyMs}ms latency: " +
                      $"owner responded in {responseMs:0}ms (round trip is {SimulatedLatencyMs * 2}ms), " +
                      $"travelled {travelled:0.00}m, " +
                      $"client/server disagreement {finalError:0.000}m, " +
                      $"{reconciles} client reconciles applied");

            Assert.Greater(travelled, 1.0f,
                "the character barely moved, so the timing measurement means nothing");

            // The headline: responding faster than a single one-way trip is
            // only possible if the client simulated the move itself.
            Assert.Less(responseMs, SimulatedLatencyMs,
                $"owner took {responseMs:0}ms to respond to its own input at {SimulatedLatencyMs}ms " +
                "latency. That is round-trip behaviour -- prediction is not working, and the game " +
                "will feel like remote desktop.");

            // ...but responsiveness must not have been bought by ignoring the
            // server. A client that simply moved locally forever would ace the
            // check above and be a cheater's paradise.
            Assert.Less(finalError, 1.0f,
                $"client and server ended {finalError:0.00}m apart. The client is not reconciling " +
                "to server state, so it is not predicting -- it is just doing its own thing.");

            // Agreement alone is not proof of reconciliation: a client running
            // identical deterministic code could match the server by
            // coincidence and never once accept a correction. Requiring real
            // corrections is what distinguishes "predicting and being kept in
            // line" from "happening to agree".
            Assert.Greater(reconciles, 0,
                "the client never applied a single reconcile. It may be matching the server by " +
                "running the same deterministic code rather than by being corrected -- which would " +
                "silently stop working the moment anything diverged.");
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

        private IEnumerator ConnectWithLatency()
        {
            ushort port = _nextPort++;

            // Real simulated latency on BOTH ends, so a round trip costs
            // 2 x SimulatedLatencyMs, exactly as it would across the Atlantic.
            foreach (NetworkManager nm in new[] { _server, _client })
            {
                nm.TransportManager.LatencySimulator.SetEnabled(true);
                nm.TransportManager.LatencySimulator.SetLatency(SimulatedLatencyMs);
            }

            Transport serverTransport = _server.TransportManager.Transport;
            serverTransport.SetPort(port);
            serverTransport.SetServerBindAddress("127.0.0.1", IPAddressType.IPv4);
            _server.ServerManager.StartConnection(port);
            yield return WaitUntil(() => _server.IsServerStarted, "the server to start");

            Transport clientTransport = _client.TransportManager.Transport;
            clientTransport.SetPort(port);
            clientTransport.SetClientAddress("127.0.0.1");
            _client.ClientManager.StartConnection("127.0.0.1", port);

            yield return WaitUntil(
                () => _client.IsClientStarted && _server.ServerManager.Clients.Count > 0,
                "the client to connect through simulated latency");
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, string what)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail($"timed out after {TimeoutSeconds:0.#}s waiting for {what}");
                }
                yield return null;
            }
        }
    }
}
