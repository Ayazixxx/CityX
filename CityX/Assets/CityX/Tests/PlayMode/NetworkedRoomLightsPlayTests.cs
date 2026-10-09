using System;
using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.World;
using FishNet.Managing;
using FishNet.Transporting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Light switches over real sockets (a server and two clients in this process): a flip on one player's machine reaches the server and every other
    /// player (Character.RoomLightsNetwork), and a player who joins later is told which lights are on. The multiplayer-first rule: anything a player
    /// does, the others see.
    /// </summary>
    public sealed class NetworkedRoomLightsPlayTests
    {
        private static ushort _nextPort = 7931;
        private const float TimeoutSeconds = 20.0f;

        private readonly List<GameObject> _made = new List<GameObject>();
        private readonly List<NetworkManager> _managers = new List<NetworkManager>();

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
            foreach (GameObject go in _made) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _made.Clear();
            _managers.Clear();
            RoomLights.Clear();
            RoomLights.Relay = null;
            LogAssert.ignoreFailingMessages = false;
        }

        private static IEnumerator WaitUntil(Func<bool> done, string what)
        {
            float start = Time.realtimeSinceStartup;
            while (!done())
            {
                Assert.Less(Time.realtimeSinceStartup - start, TimeoutSeconds, $"timed out waiting for {what}");
                yield return null;
            }
        }

        private NetworkManager Manager(string name)
        {
            GameObject prefab = Resources.Load<GameObject>("CityX.Spike/SpikeNetworkManager");
            Assert.IsNotNull(prefab, "missing SpikeNetworkManager prefab -- run CityX/Spike/Rebuild Spike Assets");
            GameObject go = UnityEngine.Object.Instantiate(prefab);
            go.name = name;
            _made.Add(go);
            var manager = go.GetComponent<NetworkManager>();
            _managers.Add(manager);
            return manager;
        }

        private RoomLightsNetwork Lights(NetworkManager manager)
        {
            var go = new GameObject("RoomLightsNetwork " + manager.name);
            _made.Add(go);
            var lights = go.AddComponent<RoomLightsNetwork>();
            lights.Manager = manager;
            return lights;
        }

        private IEnumerator Join(NetworkManager client, ushort port)
        {
            Transport t = client.TransportManager.Transport;
            t.SetPort(port);
            t.SetClientAddress("127.0.0.1");
            client.ClientManager.StartConnection("127.0.0.1", port);
            yield return WaitUntil(() => client.IsClientStarted && client.ClientManager.Connection.IsAuthenticated, client.name + " to join");
        }

        [UnityTest]
        public IEnumerator AFlipOnOneMachineReachesTheOthersAndALateJoinerIsTold()
        {
            LogAssert.ignoreFailingMessages = true;
            ushort port = _nextPort++;
            NetworkManager server = Manager("Lights Server");
            Transport st = server.TransportManager.Transport;
            st.SetPort(port);
            st.SetServerBindAddress("127.0.0.1", IPAddressType.IPv4);
            server.ServerManager.StartConnection(port);
            yield return WaitUntil(() => server.IsServerStarted, "the server to start");
            Lights(server);

            NetworkManager a = Manager("Lights Client A");
            NetworkManager b = Manager("Lights Client B");
            RoomLightsNetwork onA = Lights(a);
            RoomLightsNetwork onB = Lights(b);
            yield return Join(a, port);
            yield return Join(b, port);
            yield return WaitUntil(() => onA.StatesHeard > 0 && onB.StatesHeard > 0, "both clients to be told the lights on joining");

            var key = new RoomKey(12, 3, 1);
            onA.Send(key, true);   // what RoomLights.Flip does on A's machine, through its relay
            yield return WaitUntil(() => onB.FlipsHeard > 0, "player B to hear player A's switch");
            Assert.IsTrue(RoomLights.IsOn(key), "the server did not take the flip");

            // A player who joins now is told it is on.
            NetworkManager c = Manager("Lights Client C");
            RoomLightsNetwork onC = Lights(c);
            yield return Join(c, port);
            yield return WaitUntil(() => onC.StatesHeard > 0, "the late joiner to be told which lights are on");
            Assert.GreaterOrEqual(onC.LastStateSize, 1, "the late joiner was not told the light that is on");
        }
    }
}
