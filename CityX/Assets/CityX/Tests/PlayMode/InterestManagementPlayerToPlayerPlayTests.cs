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
    /// Requested: "lets say there was 1000 players in the same areas all
    /// wearing outfits do you think the game would be able to handle that" --
    /// answered honestly at the time as no, and this is the fix. Without
    /// this, a player observes every OTHER player regardless of distance
    /// (SpikeAssetBuilder.BuildPlayerPrefab's own OLD comment: "culling
    /// players from each other is a real decision for later" -- that
    /// decision is now made), so a client's traffic, and in the real game
    /// every other player's instantiated body/Animator/garments, would grow
    /// with TOTAL player count in the area rather than with how many are
    /// actually near you. InterestManagementPlayTests already proved the
    /// underlying FishNet mechanism (NetworkObserver + DistanceCondition)
    /// culls NPC-shaped entities correctly; this proves the SAME mechanism,
    /// now also wired onto SpikePlayer, culls PLAYER-shaped entities the
    /// same way -- the one case that was explicitly left untested.
    ///
    /// A DEDICATED SERVER PLUS TWO REAL CLIENTS, THE SAME SpikeNetworkManager
    /// PREFAB ThirtyClientLoadPlayTests/ScaleMeasurementPlayTests ALREADY
    /// USE -- deliberately NOT a hand-rolled host-plus-friend setup (that was
    /// tried first here and hit a real, separate gap: SpikeWorld's own
    /// runtime Resources-based prefab registration, see LoadAndRegister's own
    /// doc comment, only lines up when every NetworkManager involved shares
    /// ONE `SpawnablePrefabs` collection -- the generated SpikeNetworkManager
    /// prefab already carries that shared reference from
    /// SpikeAssetBuilder.BuildNetworkManagerPrefab, whereas two independently
    /// `ScriptableObject.CreateInstance`'d collections never see each other's
    /// registrations, however carefully synchronised by hand). Using a real
    /// dedicated server (no local client of its own) rather than HostedWorld
    /// also makes "the host's own player" and "another player" symmetric --
    /// every player here is just another connected client, which is exactly
    /// what "1000 players in the same area" actually looks like.
    /// </summary>
    public class InterestManagementPlayerToPlayerPlayTests
    {
        private static ushort _nextPort = 41960;
        private const float ConnectTimeoutSeconds = 30.0f;

        /// <summary>DistanceCondition is Timed, not per-frame -- see InterestManagementPlayTests' own comment.</summary>
        private const float ObserverSettleSeconds = 2.5f;

        private GameObject _serverGo;
        private NetworkManager _server;
        private SpikeWorld _world;

        private readonly System.Collections.Generic.List<GameObject> _clientGos = new System.Collections.Generic.List<GameObject>();
        private readonly System.Collections.Generic.List<NetworkManager> _clients = new System.Collections.Generic.List<NetworkManager>();

        [SetUp]
        public void SetUp()
        {
            SpikeAgent.ResetLiveCount();
            SpikePlayer.ResetLiveCount();
        }

        [TearDown]
        public void TearDown()
        {
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
        }

        /// <summary>
        /// THE SAFETY NET. If a player ever stopped observing their OWN
        /// body because of a distance-condition bug, they would not see or
        /// be able to control their own character at all -- the worst
        /// possible failure mode, so it gets its own explicit test rather
        /// than being assumed from the other two passing.
        /// </summary>
        [UnityTest]
        public IEnumerator APlayerAlwaysObservesTheirOwnBodyRegardlessOfDistanceMath()
        {
            yield return StartServerAndClients(1);

            NetworkConnection connection = ConnectionFor(0);
            // Placed well outside SpikeWorld.ObserverRadius of the origin --
            // if self-observation depended on being near (0,0,0) rather than
            // being genuinely distance-zero from one's own body, this would
            // catch it.
            NetworkObject player = _world.SpawnPlayer(connection, new Vector3(500.0f, 0.0f, 0.0f));

            yield return new WaitForSeconds(ObserverSettleSeconds);

            Assert.IsTrue(player.Observers.Contains(connection),
                "a player must always observe their own body, regardless of where in the world it is");
        }

        /// <summary>THE ONE. A player must not receive every other player in the area.</summary>
        [UnityTest]
        public IEnumerator TwoPlayersFarApartDoNotObserveEachOther()
        {
            yield return StartServerAndClients(2);

            NetworkConnection connectionA = ConnectionFor(0);
            NetworkConnection connectionB = ConnectionFor(1);

            NetworkObject playerA = _world.SpawnPlayer(connectionA, Vector3.zero);
            // Twice the view radius: unambiguously outside it, not a
            // borderline case that could pass or fail on tuning noise.
            NetworkObject playerB = _world.SpawnPlayer(
                connectionB, new Vector3(SpikeWorld.ObserverRadius * 2.0f, 0.0f, 0.0f));

            yield return new WaitForSeconds(ObserverSettleSeconds);

            Assert.IsFalse(playerA.Observers.Contains(connectionB),
                "the far-away second player observed the first player -- player-to-player culling is not " +
                "working, so every player's body would be instantiated (Animator, garments and all) on every " +
                "other client's machine regardless of distance");
            Assert.IsFalse(playerB.Observers.Contains(connectionA),
                "the first player observed the far-away second player -- same failure, the other direction");
        }

        /// <summary>The other half of the claim: nearby players must still see each other.</summary>
        [UnityTest]
        public IEnumerator TwoPlayersCloseTogetherDoObserveEachOther()
        {
            yield return StartServerAndClients(2);

            NetworkConnection connectionA = ConnectionFor(0);
            NetworkConnection connectionB = ConnectionFor(1);

            NetworkObject playerA = _world.SpawnPlayer(connectionA, Vector3.zero);
            // Well inside the view radius -- two players standing near each other.
            NetworkObject playerB = _world.SpawnPlayer(
                connectionB, new Vector3(SpikeWorld.ObserverRadius * 0.25f, 0.0f, 0.0f));

            yield return new WaitForSeconds(ObserverSettleSeconds);

            Assert.IsTrue(playerA.Observers.Contains(connectionB),
                "two nearby players should observe each other -- culling everyone within view distance too " +
                "would be as broken as culling nothing");
            Assert.IsTrue(playerB.Observers.Contains(connectionA),
                "same failure, the other direction");
        }

        // --- helpers ---

        /// <summary>
        /// The server's own connection object for client index N. Matched by
        /// SORTED ClientId rather than dictionary enumeration order (which
        /// FishNet does not document as insertion-stable) -- safe here
        /// (unlike a hand-built HostedWorld setup) because this server has
        /// no local client of its own mixed into the same dictionary and
        /// FishNet assigns ClientId in strictly increasing connection order,
        /// so the Nth-smallest id is the Nth client that connected.
        /// </summary>
        private NetworkConnection ConnectionFor(int clientIndex)
        {
            var ids = new System.Collections.Generic.List<int>(_server.ServerManager.Clients.Keys);
            ids.Sort();
            return _server.ServerManager.Clients[ids[clientIndex]];
        }

        private IEnumerator StartServerAndClients(int clientCount)
        {
            GameObject prefab = Resources.Load<GameObject>("CityX.Spike/SpikeNetworkManager");
            Assert.IsNotNull(prefab, "missing generated SpikeNetworkManager prefab -- run CityX/Spike/Rebuild Spike Assets");

            ushort port = _nextPort++;

            _serverGo = Object.Instantiate(prefab);
            _serverGo.name = "Interest Server";
            _server = _serverGo.GetComponent<NetworkManager>();
            _world = _serverGo.AddComponent<SpikeWorld>();
            _world.Initialize(_server);

            Transport serverTransport = _server.TransportManager.Transport;
            serverTransport.SetPort(port);
            serverTransport.SetServerBindAddress("127.0.0.1", IPAddressType.IPv4);
            serverTransport.SetMaximumClients(clientCount + 2);
            _server.ServerManager.StartConnection(port);

            yield return WaitUntil(() => _server.IsServerStarted, "the server to start");

            for (int i = 0; i < clientCount; i++)
            {
                GameObject go = Object.Instantiate(prefab);
                go.name = $"Interest Client {i:00}";
                NetworkManager client = go.GetComponent<NetworkManager>();
                _clientGos.Add(go);
                _clients.Add(client);

                Transport t = client.TransportManager.Transport;
                t.SetPort(port);
                t.SetClientAddress("127.0.0.1");
                client.ClientManager.StartConnection("127.0.0.1", port);

                yield return null;
            }

            yield return WaitUntil(
                () => _server.ServerManager.Clients.Count >= clientCount,
                $"all {clientCount} client(s) to connect");
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
