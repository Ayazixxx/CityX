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
    /// Balcony doors over real sockets (a server and two clients in this process): a door opened on one player's machine reaches the server and
    /// every other player (Character.BalconyDoorsNetwork), shutting it does too, and a player who joins later is told which doors stand open. The
    /// multiplayer-first rule: anything a player does, the others see.
    /// </summary>
    public sealed class NetworkedBalconyDoorsPlayTests
    {
        private static ushort _nextPort = 7961;
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
            BalconyDoorState.Shared.Clear();
            BalconyDoorState.Shared.Relay = null;
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

        private BalconyDoorsNetwork Doors(NetworkManager manager)
        {
            var go = new GameObject("BalconyDoorsNetwork " + manager.name);
            _made.Add(go);
            var doors = go.AddComponent<BalconyDoorsNetwork>();
            doors.Manager = manager;
            return doors;
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
        public IEnumerator ADoorOpenedOnOneMachineReachesTheOthersAndALateJoinerIsTold()
        {
            LogAssert.ignoreFailingMessages = true;
            BalconyDoorState.Shared.Clear();
            ushort port = _nextPort++;
            NetworkManager server = Manager("Doors Server");
            Transport st = server.TransportManager.Transport;
            st.SetPort(port);
            st.SetServerBindAddress("127.0.0.1", IPAddressType.IPv4);
            server.ServerManager.StartConnection(port);
            yield return WaitUntil(() => server.IsServerStarted, "the server to start");
            Doors(server);

            NetworkManager a = Manager("Doors Client A");
            NetworkManager b = Manager("Doors Client B");
            BalconyDoorsNetwork onA = Doors(a);
            BalconyDoorsNetwork onB = Doors(b);
            yield return Join(a, port);
            yield return Join(b, port);
            yield return WaitUntil(() => onA.StatesHeard > 0 && onB.StatesHeard > 0, "both clients to be told the open doors on joining");

            onA.Send(4321, 2, 4, true);   // what BalconyDoorState.Use does on A's machine, through its relay
            yield return WaitUntil(() => onB.UsesHeard > 0, "player B to hear player A's door");
            Assert.IsTrue(BalconyDoorState.Shared.IsOpen(4321, 2, 4), "the server did not take the door");

            // A player who joins now is told it is open.
            NetworkManager c = Manager("Doors Client C");
            BalconyDoorsNetwork onC = Doors(c);
            yield return Join(c, port);
            yield return WaitUntil(() => onC.StatesHeard > 0, "the late joiner to be told which doors are open");
            Assert.GreaterOrEqual(onC.LastStateSize, 1, "the late joiner was not told the door that is open");

            // Shutting it goes round the same way.
            int heard = onB.UsesHeard;
            onA.Send(4321, 2, 4, false);
            yield return WaitUntil(() => onB.UsesHeard > heard, "player B to hear the door shut");
            Assert.IsFalse(BalconyDoorState.Shared.IsOpen(4321, 2, 4), "the server did not shut it");
        }
    }
}
