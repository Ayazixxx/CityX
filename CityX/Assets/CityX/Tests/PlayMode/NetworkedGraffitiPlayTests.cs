using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// GRAFFITI OVER REAL SOCKETS (owner, 2026-10-15: "have the drawing stay on that wall and visible to other players online"): a dedicated
    /// server and two players with bodies. A piece player A puts up reaches player B; a piece far from A's body, and a second piece a moment
    /// after the first, are refused by the server; a player who joins later is sent every piece already up.
    /// </summary>
    public sealed class NetworkedGraffitiPlayTests
    {
        private const string NetworkedPlayerPrefabPath = "Assets/CityX/Character/NetworkedPlayer.prefab";
        private static ushort _nextPort = 45700;
        private const float TimeoutSeconds = 30.0f;

        private GameObject _ground;
        private readonly List<GameObject> _made = new List<GameObject>();
        private readonly List<NetworkManager> _managers = new List<NetworkManager>();

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "Ground";
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            GraffitiWall.Clear();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = true;
            foreach (NetworkManager m in _managers)
            {
                if (m == null) continue;
                if (m.ClientManager.Started) m.ClientManager.StopConnection();
                if (m.ServerManager.Started) m.ServerManager.StopConnection(true);
            }
            yield return null;
            foreach (NetworkedPlayerIdentity identity in Object.FindObjectsByType<NetworkedPlayerIdentity>(FindObjectsSortMode.None))
            {
                if (identity != null) Object.DestroyImmediate(identity.gameObject);
            }
            foreach (GameObject go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
            _managers.Clear();
            if (_ground != null) Object.Destroy(_ground);
            GraffitiWall.Clear();
            LogAssert.ignoreFailingMessages = false;
        }

        private static IEnumerator WaitUntil(System.Func<bool> done, string what)
        {
            float start = Time.realtimeSinceStartup;
            while (!done())
            {
                Assert.Less(Time.realtimeSinceStartup - start, TimeoutSeconds, $"timed out waiting for {what}");
                yield return null;
            }
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds)
            {
                yield return null;
            }
        }

        /// <summary>A network manager with this machine's GraffitiNetwork bound to it.</summary>
        private (NetworkManager manager, GraffitiNetwork graffiti) Machine(string name)
        {
            GameObject prefab = Resources.Load<GameObject>("CityX.Spike/SpikeNetworkManager");
            Assert.IsNotNull(prefab, "missing SpikeNetworkManager prefab -- run CityX/Spike/Rebuild Spike Assets");
            GameObject go = Object.Instantiate(prefab);
            go.name = name;
            _made.Add(go);
            var manager = go.GetComponent<NetworkManager>();
            _managers.Add(manager);
            var graffitiObject = new GameObject("GraffitiNetwork " + name);
            _made.Add(graffitiObject);
            var graffiti = graffitiObject.AddComponent<GraffitiNetwork>();
            graffiti.Manager = manager;
            return (manager, graffiti);
        }

        private static IEnumerator Join(NetworkManager client, ushort port)
        {
            Transport t = client.TransportManager.Transport;
            t.SetPort(port);
            t.SetClientAddress("127.0.0.1");
            client.ClientManager.StartConnection("127.0.0.1", port);
            yield return WaitUntil(() => client.IsClientStarted && client.ClientManager.Connection.IsAuthenticated, client.name + " to join");
        }

        private static GraffitiPiece Piece(Vector3 centre, Color colour)
        {
            var canvas = new GraffitiCanvas();
            canvas.Clear();
            canvas.Stroke(new Vector2(40.0f, 128.0f), new Vector2(216.0f, 128.0f), 10.0f, colour);
            return new GraffitiPiece
            {
                Id = GraffitiWall.IdFor(centre, Vector3.back), Centre = centre, Normal = Vector3.back, Size = GraffitiWall.PieceSize,
                Paint = canvas.Encode(),
            };
        }

        [UnityTest]
        public IEnumerator APieceReachesTheOtherPlayerTheServerRefusesTheFarAndTheHastyAndALateJoinerIsSentThemAll()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkedPlayerPrefabPath);
            Assert.IsNotNull(playerPrefab, "no NetworkedPlayer.prefab -- run CityX/Rebuild Massing Scene first");

            ushort port = _nextPort++;
            (NetworkManager server, GraffitiNetwork _) = Machine("Graffiti Server");
            Transport st = server.TransportManager.Transport;
            st.SetPort(port);
            st.SetServerBindAddress("127.0.0.1", IPAddressType.IPv4);
            server.ServerManager.StartConnection(port);
            yield return WaitUntil(() => server.IsServerStarted, "the server to start");

            (NetworkManager a, GraffitiNetwork onA) = Machine("Graffiti Client A");
            (NetworkManager b, GraffitiNetwork onB) = Machine("Graffiti Client B");
            yield return Join(a, port);
            yield return Join(b, port);

            // Each player gets a body (the server checks a painter's body is near the piece): A's at the origin, B's 4 m along.
            NetworkConnection aAtServer = null;
            NetworkConnection bAtServer = null;
            foreach (NetworkConnection connection in server.ServerManager.Clients.Values)
            {
                if (connection.ClientId == a.ClientManager.Connection.ClientId) aAtServer = connection;
                if (connection.ClientId == b.ClientManager.Connection.ClientId) bAtServer = connection;
            }
            Assert.IsNotNull(aAtServer);
            Assert.IsNotNull(bAtServer);
            NetworkObject aBody = server.GetPooledInstantiated(playerPrefab, new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity, asServer: true);
            server.ServerManager.Spawn(aBody, aAtServer);
            NetworkObject bBody = server.GetPooledInstantiated(playerPrefab, new Vector3(4.0f, 0.5f, 0.0f), Quaternion.identity, asServer: true);
            server.ServerManager.Spawn(bBody, bAtServer);
            yield return WaitUntil(() => a.ClientManager.Objects.Spawned.Count >= 2 && b.ClientManager.Objects.Spawned.Count >= 2,
                "both players to appear on both clients");
            yield return WaitSeconds(0.3f);

            // Far from A's body (refused), then one in front of it (taken), then another at once (refused: too soon after the first).
            GraffitiPiece far = Piece(new Vector3(300.0f, 2.5f, 3.0f), Color.red);
            GraffitiPiece first = Piece(new Vector3(0.0f, 2.5f, 3.0f), Color.green);
            GraffitiPiece hasty = Piece(new Vector3(2.0f, 2.5f, 3.0f), Color.blue);
            onA.Send(far);
            onA.Send(first);
            onA.Send(hasty);
            yield return WaitUntil(() => onB.LastHeardId == first.Id, "player B to be sent player A's piece");
            Assert.AreEqual(1, onB.PiecesHeard, "the far piece reached player B");
            Assert.IsNotNull(GraffitiWall.Find(first.Id), "the piece did not go up");
            Assert.IsNull(GraffitiWall.Find(far.Id), "a piece 300 m from the painter went up");

            // A second later the next one is taken; the hasty one never arrives.
            yield return WaitSeconds(GraffitiNetwork.MinSecondsBetween + 0.25f);
            GraffitiPiece second = Piece(new Vector3(-2.0f, 2.5f, 3.0f), Color.yellow);
            onA.Send(second);
            yield return WaitUntil(() => onB.LastHeardId == second.Id, "player B to be sent player A's second piece");
            Assert.AreEqual(2, onB.PiecesHeard, "the hasty piece reached player B");
            Assert.IsNull(GraffitiWall.Find(hasty.Id), "a piece sent straight after another went up");
            Assert.AreEqual(2, GraffitiWall.Count);

            // A player who joins now is sent both.
            (NetworkManager c, GraffitiNetwork onC) = Machine("Graffiti Client C");
            yield return Join(c, port);
            yield return WaitUntil(() => onC.PiecesHeard >= 2, "the late joiner to be sent the pieces already up");
            yield return WaitSeconds(0.3f);
            Assert.AreEqual(2, onC.PiecesHeard, "the late joiner was sent something other than the two pieces up");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }
    }
}
